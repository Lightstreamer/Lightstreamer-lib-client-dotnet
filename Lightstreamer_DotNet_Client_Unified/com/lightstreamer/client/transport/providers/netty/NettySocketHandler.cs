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

using DotNetty.Buffers;
using DotNetty.Codecs.Http;
using DotNetty.Common.Utilities;
using DotNetty.Transport.Channels;
using Lightstreamer.DotNet.Logging.Log;
using System;
using System.Text;

namespace com.lightstreamer.client.transport.providers.netty
{

    /// <summary>
    /// HTTP channel handler notified when the underlying channel generates an event 
    /// (e.g. the channel is active, there is new data to read etc.).
    /// </summary>
    public class NettySocketHandler : SimpleChannelInboundHandler<object>, IDisposable
    {

        private const int INIT = 0;
        private const int OPEN = 1;
        private const int OCCUPIED = 2;
        private const int CLOSED = 3;

        protected internal static readonly ILogger log = LogManager.GetLogger(Constants.TRANSPORT_LOG);
        protected internal static readonly ILogger logPool = LogManager.GetLogger(Constants.NETTY_POOL_LOG);
        private Uri uri;

        private NettyRequestListener socketListener = null;

        private int status = INIT;
        private NettyInterruptionHandler interruptionHandler;
        private LineAssembler lineAssembler;
        private AtomicReference<IChannel> channelRef = new AtomicReference<IChannel>();

        /**
        * The flag is false when the server sends the header "Connection: close"
        * and it is true when the header is "Connection: keep-alive".
        */
        private volatile bool keepalive = false;

        private void set(int status)
        {
            this.status = status;
        }

        private bool @is(int status)
        {
            return this.status == status;
        }
        private bool isNot(int status)
        {
            return !this.@is(status);
        }

        /// <summary>
        /// Binds the request listener with the socket so that the opening/reading/closing events of the socket are notified to the listener.
        /// Returns false if the socket can't be bound.
        /// </summary>
        public virtual bool switchListener(Uri uri, NettyRequestListener socketListener, NettyInterruptionHandler interruptionHandler)
        {
            lock (this)
            {
                if (this.@is(OPEN))
                {
                    socketListener.onOpen();
                }
                else if (this.@is(OCCUPIED) || this.@is(CLOSED))
                {
                    //should never happen! what do we do? XXX
                    return false;
                } //else if is init we'll get the onOpen later

                this.uri = uri;
                this.socketListener = socketListener;
                switchInterruptionHandler(interruptionHandler);
                this.lineAssembler = new LineAssembler(socketListener);
                return true;
            }
        }

        private void switchInterruptionHandler(NettyInterruptionHandler newInterruptionHandler)
        {
            // Only one interruptionHandler should point to this socketHandler
            // If the old interruptionHandler holds a link, it must be deleted before assigning the new interruptionHandler
            if (this.interruptionHandler != null)
            {
                this.interruptionHandler.connectionRef.Value = null;
            }
            newInterruptionHandler.connectionRef.Value = this;
            this.interruptionHandler = newInterruptionHandler;
        }

        private void errorAsync(IChannel ch)
        {
            if (logPool.IsDebugEnabled)
            {
                logPool.Debug("Socket error.");
            }

            if (this.socketListener != null)
            {
                this.socketListener.onBroken();
            }
            this.socketListener = null;
            this.interruptionHandler = null;

            this.set(CLOSED); //we'll be closed soon anyway

            closeChannel(ch);
                // NOTE: async function not awaited; ensure it doesn't throw in the concurrent part
        }

        private void Close()
        {
            if (logPool.IsDebugEnabled)
            {
                logPool.Debug("Socket Closed.");
            }

            if (this.socketListener != null)
            {
                this.socketListener.onClosed();
            }
            this.socketListener = null;
            this.interruptionHandler = null;


            this.set(CLOSED); //we'll be closed soon anyway
        }

        private void reuse(IChannelHandlerContext ctx)
        {
            
            if (!keepalive)
            {
                log.Debug("Reuse !Keeaplive for: " + ctx.Channel.Id);

                closeChannel(ctx.Channel);
                // NOTE: async function invoked concurrently
            }

            if (this.socketListener != null)
            {
                log.Debug("Reuse socketListener null for:  " + ctx.Channel.Id);

                // this.socketListener.onClosed();
            }
            this.socketListener = null;
            this.interruptionHandler = null;
            this.keepalive = false;

            this.set(OPEN);
        }

        private void open()
        {

            this.set(OPEN);
            if (this.socketListener != null)
            {
                this.socketListener.onOpen();
            }
        }

        private void message(IByteBuffer buf)
        {
            //    if (this.socketListener != null) {
            //      this.socketListener.onMessage(message);
            //    }
            lineAssembler.readBytes(buf);
        }

        private bool Interrupted
        {
            get
            {
                if (this.interruptionHandler != null)
                {
                    return this.interruptionHandler.Interrupted;
                }
                return false;
            }
        }

        public override void ChannelActive(IChannelHandlerContext ctx)
        {
            IChannel ch = (IChannel)ctx.Channel;
            channelRef.Value = ch;

            if (logPool.IsDebugEnabled)
            {
                logPool.Debug("HTTP channel active [" + ctx.Channel.Id + "]");
            }

            lock (this)
            {

                if (this.isNot(INIT))
                {
                    //something wrong
                    errorAsync(ch);
                }
                else
                {
                    open();

                    if (this.Interrupted)
                    {
                        closeChannel(ch);
                            // NOTE: async function invoked concurrently
                    }

                }
            }
        }

        public override void ChannelInactive(IChannelHandlerContext ctx)
        {
            lock (this)
            {

                IChannel ch = ctx.Channel;

                if (logPool.IsDebugEnabled)
                {
                    logPool.Debug("HTTP channel inactive [" + ch.Id + "]");
                    logPool.Debug(" ... " + ch.Open + " - " + ch.Active);

                }

                if (this.@is(OCCUPIED))
                {
                    //if we're still occupied this means the sudden inactivity is not good
                    errorAsync(ch);
                }
                else
                {
                    Close();
                }
            }
        }

        public virtual void messageReceived(IChannelHandlerContext ctx, object msg)
        {
            lock (this)
            {

                IChannel ch = ctx.Channel;

                if (this.Interrupted)
                {
                    if (!( msg is ILastHttpContent ))
                    {
                        log.Info("Force socket close [" + ch.Id + "]");
                        closeChannel(ch);
                            // NOTE: async function invoked concurrently
                    }
                }

                if (msg is IHttpResponse)
                {
                    if (this.isNot(OPEN))
                    {
                        errorAsync(ch);
                        return;
                    }
                    this.set(OCCUPIED);

                    IHttpResponse response = (IHttpResponse)msg;

                    int respCode = response.Status.Code;


                    if (respCode < 200)
                    {
                        //100 family, not expected
                        errorAsync(ch);
                        return;
                    }
                    else if (respCode < 300)
                    {
                        //200 family: all is good
                    }
                    else if (respCode < 400)
                    {
                        //300 family: TODO 1.1 handling redirections
                        log.Warn("Redirection currently not implemented");
                        //String target = response.headers().get(HttpHeaderNames.LOCATION)
                        errorAsync(ch);
                        return;
                    }
                    else
                    {
                        //400 - 500 families: problems
                        errorAsync(ch);
                        return;
                    }

                    keepalive = HttpUtil.IsKeepAlive(response);
                   
                    foreach (string cookie in response.Headers.GetAllAsString(HttpHeaderNames.SetCookie))
                    {
                        Uri uu = new Uri(uri.Scheme + "://" + uri.Host + ":" + uri.Port + uri.Segments[0] + uri.Segments[1].TrimEnd('/'));
                        log.Info("SetCookie received for uri " + uu + ": " + cookie);
                        CookieHelper.saveCookies(uu, cookie);
                    }

                }
                else if (msg is IHttpContent)
                {
                    if (this.isNot(OCCUPIED))
                    {
                        errorAsync(ch);
                        return;
                    }

                    IHttpContent chunk = (IHttpContent)msg;
                    IByteBuffer buf = chunk.Content;

                    if (log.IsDebugEnabled)
                    {
                        if (buf.ReadableBytes > 0)
                        {
                            log.Debug("HTTP transport receiving [" + ch.Id + "]:\n" + buf.ToString(Encoding.UTF8));
                        }
                    }
                    message(buf);

                    if (chunk is ILastHttpContent)
                    {
                        //http complete, go back to open so that it can be reused
                        reuse(ctx);
                    }
                }
            }
        }

        public override void ExceptionCaught(IChannelHandlerContext ctx, Exception cause)
        {
            lock (this)
            {
                IChannel ch = ctx.Channel;
                log.Error("HTTP transport error [" + ch.Id + "]", cause);
                errorAsync(ch);
            }
        }

        protected override void ChannelRead0(IChannelHandlerContext ctx, object msg)
        {
            lock (this)
            {
                messageReceived(ctx, msg);
            }
        }

        protected async void closeChannel(IChannel ch)
        {
            if (log.IsDebugEnabled)
            {
                log.Debug("Channel closed [" + ch.Id + "]");
            }
            await ch.CloseAsync();
        }

        void IDisposable.Dispose()
        {
            IChannel ch = channelRef.Value;
            if (ch != null)
            {
                errorAsync(ch);
            }
        }
    }
}