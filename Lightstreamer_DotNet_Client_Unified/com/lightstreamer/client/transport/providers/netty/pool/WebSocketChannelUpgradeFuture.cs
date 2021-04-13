using DotNetty.Codecs.Http;
using DotNetty.Codecs.Http.WebSockets;
using DotNetty.Common.Utilities;
using DotNetty.Transport.Channels;
using Lightstreamer.DotNet.Logging.Log;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace com.lightstreamer.client.transport.providers.netty.pool
{

    using LsUtils = com.lightstreamer.util.LsUtils;

    /// <summary>
    /// Result of an asynchronous operation of upgrading a channel to WebSocket.
    /// </summary>
    public class WebSocketChannelUpgradeFuture : ChannelUpgradeFuture
    {
        private bool InstanceFieldsInitialized = false;

        private void InitializeInstanceFields()
        {
            machine = new StateMachine(this);
        }


        private static readonly ILogger log = LogManager.GetLogger(Constants.NETTY_POOL_LOG);

        private IChannel chnl;
        private static Task futureTask;
        private volatile bool _hsws;

        private StateMachine machine;
        private ExtendedNettyFullAddress address;

        /// <summary>
        /// Upgrades the given channel.
        /// </summary>
        /// <param name="channelFuture"> the channel to upgrade </param>
        /// <param name="address"> the address to which the channel connects  </param>
        public WebSocketChannelUpgradeFuture(IChannel channel, ExtendedNettyFullAddress address)
        {
            if (!InstanceFieldsInitialized)
            {
                InitializeInstanceFields();
                InstanceFieldsInitialized = true;
            }

            this.chnl = channel;
            this.address = address;
        }

        public async void AwaitChannel(long timeout)
        {

            _hsws = false;
            if (chnl.Active)
            {
                machine.setChannel(chnl, Phase.CONNECTION_OK);
                upgrade(address);

                while (futureTask == null)
                {
                    Thread.Sleep(5);
                }

                await futureTask;

                int maxTimes = 1;

                while (!_hsws)
                {
                    Thread.Sleep(5);

                    if ((maxTimes*10) >= (timeout))
                    {
                        _hsws = true;
                        machine.next(Phase.UPGRADE_FAILURE);
                    }
                    else
                    {
                        maxTimes++;
                    }
                }
            }
            else
            {
                machine.setErrorCause(new Exception("Channel inactive."), Phase.CONNECTION_FAILURE);
            }

        }

        internal void wshsfinished()
        {
            machine.next(Phase.UPGRADE_OK);
            _hsws = true;
        }

        internal void wshsfailed()
        {
            machine.next(Phase.UPGRADE_FAILURE);
            _hsws = true;
        }

        /// <summary>
        /// Upgrade the channel to WebSocket.
        /// </summary>
        private void upgrade(ExtendedNettyFullAddress address)
        {
            /*
			 * ========================================= Note =================================================
			 * Operations on the channel must happen in the thread associated with the channel.
			 * Otherwise subtle bugs can appear. 
			 * For example the method WebSocketClientHandshaker.finishHandshake can return before
			 * the method WebSocketClientHandshaker.handshake returns leaving the channel pipeline in a mess.
			 * ================================================================================================
			 */

            chnl.EventLoop.Execute(() =>
             {
                /*
                 * If the eventLoop is overloaded, when this task is executed the channel can be broken
                 * (for example because the server has closed it).
                 * So the first thing to do is to check if the channel is healthy.
                 */
                 if (chnl.Active)
                 {
                    /* set cookies and extra headers */
                     string cookies = address.Cookies;
                     IDictionary<string, string> extraHeaders = address.ExtraHeaders;
                     DefaultHttpHeaders customHeaders = new DefaultHttpHeaders();
                     if (extraHeaders != null)
                     {
                         foreach (KeyValuePair<string, string> entry in extraHeaders)
                         {
                             customHeaders.Add(new AsciiString(entry.Key), entry.Value);
                         }
                     }
                     if (!string.ReferenceEquals(cookies, null) && cookies.Length > 0)
                     {
                         customHeaders.Set(HttpHeaderNames.Cookie, cookies);
                     }
                    /* build url */
                     NettyFullAddress remoteAddress = address.Address;
                     string scheme = remoteAddress.Secure ? "wss" : "ws";
                     string host = remoteAddress.Host;
                     string url;

                     int port = remoteAddress.Port;
                     if (host.Equals("::1"))
                     {
                         url = scheme + "://localhost:" + port + "/lightstreamer";
                     }
                     else
                     {
                         url = scheme + "://" + host + ":" + port + "/lightstreamer";
                     }

                     Uri uri = LsUtils.uri(url);
                     string subprotocol = Constants.TLCP_VERSION + ".lightstreamer.com";
                    /* build pipeline */

                     WebSocketHandshakeHandler wsHandshakeHandler = new WebSocketHandshakeHandler(uri, subprotocol, customHeaders, this);

                     PipelineUtils.populateWSPipelineForHandshake(chnl, wsHandshakeHandler);

                    /* WS handshake */
                     futureTask = wsHandshakeHandler.handshake(chnl);

                 }
                 else
                 {
                     futureTask = Task.Factory.StartNew(() => Thread.Sleep(2));
                 }
             });

            return ;
        }

        public virtual bool Done
        {
            get
            {
                return machine.Done;
            }
        }

        public virtual bool Success
        {
            get
            {
                return machine.Success;
            }
        }

        public Task UpgradeTask { get => futureTask; set => futureTask = value; }

        public virtual void addListener(ChannelUpgradeFuture_ChannelUpgradeFutureListener fl)
        {
            machine.addListener(fl);
        }

        public virtual IChannel channel()
        {
            return machine.Channel;
        }

        public virtual Exception cause()
        {
            return machine.Cause;
        }

        /*
		 * ===============
		 * Support classes
		 * ===============
		 */
        private sealed class Phase
        {

            public static readonly Phase CONNECTING = new Phase("CONNECTING", InnerEnum.CONNECTING, false, false);
            public static readonly Phase CONNECTION_OK = new Phase("CONNECTION_OK", InnerEnum.CONNECTION_OK, false, false);
            public static readonly Phase CONNECTION_FAILURE = new Phase("CONNECTION_FAILURE", InnerEnum.CONNECTION_FAILURE, true, false);
            public static readonly Phase UPGRADE_OK = new Phase("UPGRADE_OK", InnerEnum.UPGRADE_OK, true, true);
            public static readonly Phase UPGRADE_FAILURE = new Phase("UPGRADE_FAILURE", InnerEnum.UPGRADE_FAILURE, true, false);

            private static readonly IList<Phase> valueList = new List<Phase>();

            static Phase()
            {
                valueList.Add(CONNECTING);
                valueList.Add(CONNECTION_OK);
                valueList.Add(CONNECTION_FAILURE);
                valueList.Add(UPGRADE_OK);
                valueList.Add(UPGRADE_FAILURE);
            }

            public enum InnerEnum
            {
                CONNECTING,
                CONNECTION_OK,
                CONNECTION_FAILURE,
                UPGRADE_OK,
                UPGRADE_FAILURE
            }

            public readonly InnerEnum innerEnumValue;
            private readonly string nameValue;
            private readonly int ordinalValue;
            private static int nextOrdinal = 0;

            internal readonly bool isDone;
            internal readonly bool isSuccess;

            /// <summary>
            /// A phase in the process of upgrading a channel.
            /// </summary>
            /// <param name="isDone"> true if and only if <seealso cref="ChannelUpgradeFuture#isDone()"/> is true </param>
            /// <param name="isSuccess"> true if and only if <seealso cref="ChannelUpgradeFuture#isSuccess()"/> is true </param>
            internal Phase(string name, InnerEnum innerEnum, bool isDone, bool isSuccess)
            {
                this.isDone = isDone;
                this.isSuccess = isSuccess;

                nameValue = name;
                ordinalValue = nextOrdinal++;
                innerEnumValue = innerEnum;
            }

            public override string ToString()
            {
                return nameValue;
            }

            public static IList<Phase> values()
            {
                return valueList;
            }

            public int ordinal()
            {
                return ordinalValue;
            }

            public static Phase valueOf(string name)
            {
                foreach (Phase enumInstance in Phase.valueList)
                {
                    if (enumInstance.nameValue == name)
                    {
                        return enumInstance;
                    }
                }
                throw new System.ArgumentException(name);
            }
        } // Phase

        private class StateMachine
        {
            private readonly WebSocketChannelUpgradeFuture outerInstance;

            public StateMachine(WebSocketChannelUpgradeFuture outerInstance)
            {
                this.outerInstance = outerInstance;
            }


            internal Phase phase = Phase.CONNECTING;
            internal ChannelUpgradeFuture_ChannelUpgradeFutureListener listener;
            internal IChannel channel;
            internal Exception cause;

            /// <summary>
            /// Changes the future state. 
            /// Fire the listener if the phase is final.
            /// </summary>
            internal virtual void next(Phase target)
            {
                lock (this)
                {
                    if (log.IsDebugEnabled)
                    {
                        object chId = channel != null ? channel.Id.ToString() : "";
                    }
                    phase = target;
                    if (target.isDone && listener != null)
                    {
                        listener.operationComplete(outerInstance);
                    }
                }
            }

            internal virtual bool Done
            {
                get
                {
                    lock (this)
                    {
                        return phase.isDone;
                    }
                }
            }

            internal virtual bool Success
            {
                get
                {
                    lock (this)
                    {
                        return phase.isSuccess;
                    }
                }
            }

            internal virtual void addListener(ChannelUpgradeFuture_ChannelUpgradeFutureListener fl)
            {
                lock (this)
                {
                    Debug.Assert(listener == null);
                    listener = fl;
                    if (phase.isDone)
                    {
                        listener.operationComplete(outerInstance);
                    }
                }
            }

            internal virtual IChannel Channel
            {
                get
                {
                    lock (this)
                    {
                        Debug.Assert(phase.isDone && phase.isSuccess);
                        return channel;
                    }
                }
            }

            internal virtual void setChannel(IChannel ch, Phase target)
            {
                lock (this)
                {
                    channel = ch;
                    next(target);
                }
            }

            internal virtual Exception Cause
            {
                get
                {
                    lock (this)
                    {
                        Debug.Assert(phase.isDone && !phase.isSuccess);
                        return cause;
                    }
                }
            }

            internal virtual void setErrorCause(Exception ex, Phase target)
            {
                lock (this)
                {
                    cause = ex;
                    next(target);
                }
            }
        } // StateMachine

        /// <summary>
        /// Upgrades a HTTP request to WebSocket.<br>
        /// The code was adapted from <a href="http://netty.io/4.1/xref/io/netty/example/http/websocketx/client/package-summary.html">this Netty example</a>. 
        /// </summary>
        private class WebSocketHandshakeHandler : SimpleChannelInboundHandler<object>
        {

            internal static readonly ILogger logStream = LogManager.GetLogger(Constants.TRANSPORT_LOG);

            internal readonly WebSocketChannelUpgradeFuture cbwshs;

            internal readonly WebSocketClientHandshaker handshaker;

            public WebSocketHandshakeHandler(Uri uri, string subprotocol, HttpHeaders headers, WebSocketChannelUpgradeFuture cbwshs_)
            {
                handshaker = WebSocketClientHandshakerFactory.NewHandshaker(uri, WebSocketVersion.V13, subprotocol, false, headers);
                cbwshs = cbwshs_;
            }

            /// <summary>
            /// Starts the handshake protocol.
            /// </summary>
            public virtual Task handshake(IChannel ch)
            {

                Task t = handshaker.HandshakeAsync(ch);
                t.ContinueWith((antecedent, fu) =>
                {
                    if (antecedent.IsFaulted)
                    {
                        log.Error("WS channel handshake failed [" + ch.Id + "]", antecedent.Exception);
                        ch.DisconnectAsync();

                    }
                }, this);
                return t;
            }

            public void channelActive(IChannelHandlerContext ctx)
            {
                if (log.IsDebugEnabled)
                {
                    IChannel ch = ctx.Channel;
                }
                ctx.FireChannelActive();
            }

            public void channelInactive(IChannelHandlerContext ctx)
            {

                ctx.CloseAsync();
                if (log.IsDebugEnabled)
                {
                    log.Debug("WS channel inactive [" + ctx.Channel.Id + "]");
                }
                ctx.FireChannelInactive();
            }

            protected override void ChannelRead0(IChannelHandlerContext ctx, object msg)
            {

                IChannel ch = ctx.Channel;

                if (log.IsDebugEnabled)
                {
                    log.Debug("WS Read0 - " + handshaker.IsHandshakeComplete  + " - Msg:" + msg);
                }

                if (!handshaker.IsHandshakeComplete)
                {
                    IFullHttpResponse resp = (IFullHttpResponse)msg;

                    if (log.IsDebugEnabled)
                    {
                        log.Debug("WS Read0 -- " + resp.Status);
                    }

                    try
                    {
                        handshaker.FinishHandshake(ch, resp);
                    }
                    catch (Exception e)
                    {
                        log.Info("WS upgrade error: " + e.Message);
                    }

                    /* save cookies */
                    
                    foreach (string cookie in resp.Headers.GetAllAsString(HttpHeaderNames.SetCookie))
                    {
                        log.Info("SetCookie received for uri " + handshaker.Uri + ": " + cookie);
                        CookieHelper.saveCookies(handshaker.Uri, cookie);
                    }

                    if (resp.Status != HttpResponseStatus.SwitchingProtocols)
                    {

                        if (log.IsDebugEnabled)
                        {
                            log.Debug("WS Read0 ----- " + resp.Status);
                        }

                        cbwshs.wshsfailed();
                        ch.DisconnectAsync();
                    } else
                    {
                        cbwshs.wshsfinished();
                    }
                    
                    return;
                }

                if (msg is IFullHttpResponse)
                {
                    IFullHttpResponse response = (IFullHttpResponse)msg;
                    throw new System.InvalidOperationException("Unexpected FullHttpResponse (getStatus=" + response.Status + ", content=" + response.Content.ToString(Encoding.UTF8) + ")");
                }

                WebSocketFrame frame = (WebSocketFrame)msg;
                if (frame is TextWebSocketFrame)
                {
                    TextWebSocketFrame textFrame = (TextWebSocketFrame)frame;

                    ctx.FireChannelRead(textFrame.Content.Retain());

                    log.Debug("WS Read rc: " + textFrame.ReferenceCount);

                }
                else if (frame is ContinuationWebSocketFrame)
                {
                    ContinuationWebSocketFrame textFrame = (ContinuationWebSocketFrame)frame;

                    ctx.FireChannelRead(textFrame.Content.Retain());

                    log.Debug("WS Read rc: " + textFrame.ReferenceCount);

                }
                else if (frame is PongWebSocketFrame)
                {
                    log.Debug("WS received pong");

                }
                else if (frame is CloseWebSocketFrame)
                {
                    ch.CloseAsync();
                }
            }

            public void exceptionCaught(IChannelHandlerContext ctx, Exception cause)
            {
                ctx.CloseAsync();
            }

        } // WebSocketHandshakeHandler
    }

}