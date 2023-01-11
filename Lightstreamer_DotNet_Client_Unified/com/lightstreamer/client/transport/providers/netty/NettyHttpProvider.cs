#region License
/*
 * Copyright (c) Lightstreamer Srl
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
#endregion License

using com.lightstreamer.client.protocol;
using com.lightstreamer.client.requests;
using com.lightstreamer.client.session;
using com.lightstreamer.util.threads;
using DotNetty.Buffers;
using DotNetty.Codecs;
using DotNetty.Codecs.Http;
using DotNetty.Common.Utilities;
using DotNetty.Transport.Channels;
using Lightstreamer.DotNet.Logging.Log;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace com.lightstreamer.client.transport.providers.netty
{

    /// 
    public class NettyHttpProvider : HttpProvider
    {

        private static readonly string ua;
        static NettyHttpProvider()
        {
            if (LightstreamerClient.LIB_NAME.Contains("placeholder"))
            {
                ua = "Lightstreamer .Net Client over DotNetty";
            }
            else
            {
                ua = LightstreamerClient.LIB_NAME + " " + LightstreamerClient.LIB_VERSION;
            }

        }


        private SessionThread sessionThread;
        private HttpPoolManager httpPoolManager;

        private static int objectIdCounter = 0;
        private int objectId;

        public NettyHttpProvider(SessionThread thread)
        {

            this.sessionThread = thread;

            this.httpPoolManager = SingletonFactory.instance.HttpPool;

            this.objectId = Interlocked.Increment(ref objectIdCounter);

        }

        // TEST ONLY
        public NettyHttpProvider(SessionThread thread, HttpPoolManager channelPool)
        {
            this.sessionThread = thread;
            this.httpPoolManager = channelPool;
            this.objectId = Interlocked.Increment(ref objectIdCounter);
        }

        internal static void debugLogHeaders(DotNetty.Codecs.Http.HttpHeaders headers, ILogger log, string type)
        {
            if (log.IsDebugEnabled && !headers.GetEnumerator().MoveNext())
            {

                List<HeaderEntry<AsciiString, ICharSequence>>.Enumerator e = (List<HeaderEntry<AsciiString, ICharSequence>>.Enumerator)headers.GetEnumerator();

                while (e.MoveNext())
                {
                    HeaderEntry<AsciiString, ICharSequence> l = e.Current;


                }
            }
        }

        protected internal readonly ILogger log = LogManager.GetLogger(Constants.TRANSPORT_LOG);
        protected internal readonly ILogger logPool = LogManager.GetLogger(Constants.NETTY_POOL_LOG);

        public virtual ThreadShutdownHook ShutdownHook
        {
            get
            {
                return null; // nothing to do
            }
        }

        public virtual RequestHandle createConnection(Protocol protocol, LightstreamerRequest request, com.lightstreamer.client.transport.providers.HttpProvider_HttpRequestListener httpListener, IDictionary<string, string> extraHeaders, Proxy proxy, long tcpConnectTimeout, long tcpReadTimeout)
        {
            string address = request.TargetServer + "lightstreamer/" + request.RequestName + ".txt" + "?LS_protocol=" + Constants.TLCP_VERSION;
            Uri uri;
            try
            {
                uri = new Uri(address);
            }
            catch (Exception e)
            {
                log.Fatal("Unexpectedly invalid URI: " + address, e);
                throw new System.ArgumentException(e.Message);
            }

            bool secure = isSSL(address);
            int port = uri.Port == -1 ? ( secure ? 443 : 80 ) : uri.Port;

            IFullHttpRequest httpRequest = new DefaultFullHttpRequest(HttpVersion.Http11, HttpMethod.Post, uri.PathAndQuery);
            httpRequest.Headers.Set(HttpHeaderNames.Host, uri.Host);

            string cookies = CookieHelper.getCookieHeader(uri);
            log.Info("Requested cookies for uri " + uri + ": " + cookies);
            if (!string.ReferenceEquals(cookies, null) && cookies.Length > 0)
            {
                httpRequest.Headers.Set(HttpHeaderNames.Cookie, cookies);
            }

            httpRequest.Headers.Set(HttpHeaderNames.UserAgent, ua);
            httpRequest.Headers.Set(HttpHeaderNames.ContentType, "text/plain; charset=UTF-8");

            if (extraHeaders != null)
            {
                foreach (KeyValuePair<string, string> header in extraHeaders.SetOfKeyValuePairs())
                {
                    httpRequest.Headers.Set(new AsciiString(header.Key), header.Value);
                }
            }

            IByteBuffer bbuf = Unpooled.CopiedBuffer(request.getTransportAwareQueryString(null, true) + "\r\n", Encoding.UTF8);
            httpRequest.Headers.Set(HttpHeaderNames.ContentLength, bbuf.ReadableBytes);

            httpRequest.Content.Clear().WriteBytes(bbuf);

            string host4Netty = System.Net.Dns.GetHostAddresses(uri.Host)[0].ToString();

            log.Debug("cs ---- address: " + address + ", " + host4Netty);

            NettyFullAddress target = new NettyFullAddress(secure, host4Netty, port, uri.Host, proxy);

            NettyInterruptionHandler interruptionHandler = new NettyInterruptionHandler();

            bindAsync(uri, target, httpListener, httpRequest, interruptionHandler);
                // NOTE: async function not awaited; ensure it doesn't throw in the concurrent part

            return interruptionHandler;
        }

        private async void bindAsync(Uri uri, NettyFullAddress target, com.lightstreamer.client.transport.providers.HttpProvider_HttpRequestListener httpListener, IFullHttpRequest httpRequest, NettyInterruptionHandler interruptionHandler)
        {
            if (log.IsDebugEnabled)
            {
                log.Debug("HTTP transport connection establishing (oid=" + objectId + "): " + format(uri, httpRequest));
                log.Debug(" -  target: " + format(target));
            }
            try
            {
                IChannel channel_e = await httpPoolManager.acquire(target);

                log.Debug(" - acquired! " + channel_e.Id);

                // IChannel ch2 = channelFuture.Result;
                NettySocketHandler socketHandler2 = (NettySocketHandler)PipelineUtils.getChannelHandler(channel_e);

                if (socketHandler2 == null)
                {
                    log.Debug("Socket Handler null.");
                }
                else
                {

                    NettyRequestListener requestListener2 = new NettyRequestListener(httpListener, target, channel_e, httpPoolManager);

                    bool listenerBound = socketHandler2.switchListener(uri, requestListener2, interruptionHandler);
                    if (!listenerBound)
                    {

                        this.sessionThread.schedule(new Task(() =>
                        {

                            bindAsync(uri, target, httpListener, httpRequest, interruptionHandler);
                                // NOTE: async function not awaited; ensure it doesn't throw in the concurrent part
                        }), 500);

                        if (log.IsDebugEnabled)
                        {
                            log.Debug("Go Bind!");
                        }
                    }
                    else
                    {
                        // Thread.Sleep(400);
                        Task snd_req = channel_e.WriteAndFlushAsync(httpRequest).ContinueWith((snd_req_task, fu2) =>
                        {

                            log.Debug("Send Request Task status = " + snd_req_task.Status);

                            if (snd_req_task.IsFaulted)
                            {
                                log.Error("HTTP write failed [" + channel_e.Id + "]: " + httpRequest.Uri + ", " + snd_req_task.Exception);
                                channel_e.CloseAsync();
                                requestListener2.onBroken();
                            }
                        }, this);

                        if (log.IsDebugEnabled)
                        {
                            log.Debug("Go with the request " + channel_e.Active);
                        }

                    }
                }

                return;
            }
            catch (ConnectException ce)
            {

                log.Info("Connection error: " + ce.Message);

                if (log.IsDebugEnabled)
                {
                    log.Debug("HTTP transport connection error (Couldn't get a socket, try again) (oid=" + objectId + "): " + format(uri, httpRequest));
                    log.Debug(" - " + ce.StackTrace);
                }
            }
            catch (ConnectTimeoutException cte)
            {
                log.Info("Timeout error: " + cte.Message);

                if (log.IsDebugEnabled)
                {
                    log.Debug("HTTP transport timeout error (Couldn't get a socket, try again) (oid=" + objectId + "): " + format(uri, httpRequest));
                    log.Debug(" - " + cte.StackTrace);
                }
            }
            catch (Exception e)
            {
                log.Info("Error: " + e.Message);

                if (log.IsDebugEnabled)
                {
                    log.Debug("HTTP transport error (Couldn't get a socket, try again) (oid=" + objectId + "): " + format(uri, httpRequest));
                    log.Debug(" - " + e.StackTrace);
                }
            }
        }

        private string format(Uri uri, IFullHttpRequest req)
        {
            return format(uri) + "\n" + format(req);
        }

        private string format(Uri uri)
        {
            return uri.Scheme + "://" + uri.Host + ":" + uri.Port;
        }

        private string format(IFullHttpRequest req)
        {
            return req.Uri + "\n" + req.Content.ToString(Encoding.UTF8);
        }
        private string format(NettyFullAddress target)
        {
            return target.Host + ":" + target.Port + " - " + target.Secure + "(" + target.ToString() + ").";
        }


        private static bool isSSL(string address)
        {
            return address.ToLower().IndexOf("https", StringComparison.Ordinal) == 0;
        }
    }
}