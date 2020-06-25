using com.lightstreamer.client.protocol;
using com.lightstreamer.client.transport.providers.netty.pool;
using com.lightstreamer.util;
using com.lightstreamer.util.threads;
using DotNetty.Buffers;
using DotNetty.Codecs.Http.WebSockets;
using DotNetty.Transport.Channels;
using DotNetty.Transport.Channels.Pool;
using Lightstreamer.DotNet.Logging.Log;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace com.lightstreamer.client.transport.providers.netty
{

    /// <summary>
    /// WebSocket client based on Netty.
    /// The implementation is modeled after <a href="https://github.com/netty/netty/tree/4.1/example/src/main/java/io/netty/example/http/websocketx/client">
    /// this example</a>.
    /// <br>
    /// This class notifies a <seealso cref="SessionRequestListener"/> when the following events happen:
    /// <ul>
    /// <li>onOpen: fires when the connection is established and the WebSocket handshake is complete</li>
    /// <li>onMessage: fires when a new text frame is received</li>
    /// <li>onClosed: fires when the connection is closed</li>
    /// <li>onBroken: fires when there is an error.</li>
    /// </ul>
    /// <para>
    /// <b>NB1</b>
    /// The current implementation allows the sending of cookies in the handshake request but doesn't
    /// support the setting of cookies in the handshake response. A contrived solution is explained
    /// <a href="http://stackoverflow.com/questions/38306203/netty-set-cookie-in-websocket-handshake">here</a>. 
    /// </para>
    /// <para>
    /// <b>NB2</b>
    /// The actual implementation limits to 64Kb the maximum frame size. 
    /// This is not a problem because the Lightstreamer server sends frames whose size is at most 8Kb. 
    /// The limit can be modified specifying a different size at the creation of the <seealso cref="WebSocketClientHandshaker"/>
    /// (see the method <seealso cref="WebSocketClientHandshakerFactory#newHandshaker(URI, WebSocketVersion, String, boolean, io.netty.handler.codec.http.HttpHeaders, int)"/>
    /// in the inner class {@code WebSocketHandshakeHandler} of <seealso cref="WebSocketChannelPool"/>).
    /// </para>
    /// </summary>
    public class NettyWebSocketProvider : WebSocketProvider
    {

        private static readonly ILogger log = LogManager.GetLogger(Constants.NETTY_LOG);
        private static readonly ILogger logStream = LogManager.GetLogger(Constants.TRANSPORT_LOG);
        private static readonly ILogger logPool = LogManager.GetLogger(Constants.NETTY_POOL_LOG);

        private readonly WebSocketPoolManager wsPoolManager;

        private volatile MyChannel channel;

        public NettyWebSocketProvider()
        {
            this.wsPoolManager = SingletonFactory.instance.WsPool;
        }

        // TEST ONLY
        public NettyWebSocketProvider(WebSocketPoolManager channelPool)
        {
            this.wsPoolManager = channelPool;
        }

        public virtual async void connect(string address, SessionRequestListener networkListener, IDictionary<string, string> extraHeaders, string cookies, Proxy proxy, long timeout)
        {
            Uri uri = LsUtils.uri(address);
            string host = uri.Host;
            int port = LsUtils.port(uri);
            bool secure = LsUtils.isSSL(uri);

            string host4Netty = System.Net.Dns.GetHostAddresses(host)[0].ToString();

            NettyFullAddress remoteAddress = new NettyFullAddress(secure, host4Netty, port, host, proxy);
            ExtendedNettyFullAddress extendedRemoteAddress = new ExtendedNettyFullAddress(remoteAddress, extraHeaders, cookies);

            WebSocketChannelPool wsPool = (WebSocketChannelPool)wsPoolManager.get(extendedRemoteAddress);

            IChannel ch = await wsPool.AcquireNewOr(timeout);

            if (ch != null)
            {
                if (ch.Active)
                {
                    this.channel = new MyChannel(ch, wsPool, networkListener);
                    WebSocketChannelHandler chHandler = new WebSocketChannelHandler(networkListener, channel);
                    PipelineUtils.populateWSPipeline(ch, chHandler);
                    networkListener.onOpen();
                }
                else
                {
                    log.Error("WebSocket handshake error");
                    networkListener.onBroken();
                }
            }
            else
            {
                log.Error("WebSocket handshake error");
                networkListener.onBroken();
            }
        }

        public virtual void send(string message, RequestListener listener)
        {
            if (logStream.IsDebugEnabled)
            {
                logStream.Debug("WS transport sending [" + channel + "]: " + message);
            }
            channel.write(message, listener);
        }

        public virtual void disconnect()
        {
            if (logPool.IsDebugEnabled)
            {
                logPool.Debug("WS disconnect [" + channel + "]");
            }
            if (channel != null)
            {
                channel.close();
                channel = null;
            }
        }

        public virtual ThreadShutdownHook ThreadShutdownHook
        {
            get
            {
                return null; // nothing to do
            }
        }

        /// <summary>
        /// Netty channel wrapper.
        /// <para>
        /// <b>NB</b> The class is synchronized because its methods are called from both session thread and Netty thread.
        /// </para>
        /// </summary>
        private class MyChannel
        {

            internal readonly IChannel ch;
            internal readonly IChannelPool pool;
            internal readonly SessionRequestListener networkListener;
            internal bool closed = false;
            internal bool released = false;

            public MyChannel(IChannel ch, IChannelPool pool, SessionRequestListener networkListener)
            {
                this.ch = ch;
                this.pool = pool;
                this.networkListener = networkListener;
            }

            public virtual void write(string message, RequestListener listener)
            {

                lock (this)
                {
                    if (closed || released)
                    {
                        log.Warn("Message discarded because the channel [" + ch.Id + "] is closed: " + message);
                        return;
                    }
                    if (listener != null)
                    {
                        /*
						 * NB 
						 * I moved the onOpen call outside of operationComplete write callback 
						 * because sometimes the write callback of a request was fired after
						 * the read callback of the response.
						 * The effect of calling onOpen after the method onMessage/onClosed of a RequestListener
						 * was the retransmission of the request.
						 * Probably this behavior was caused by the tests running on localhost, but who knows?
						 */
                        listener.onOpen();
                    }
                    Task chf = ch.WriteAndFlushAsync(new TextWebSocketFrame(message));
                    chf.ContinueWith((antecedent, outerInstance) =>
                    {

                        if (antecedent.IsFaulted || antecedent.IsCanceled)
                        {
                            ( (MyChannel)outerInstance ).onBroken(message, antecedent.Exception);
                        }
                    }, ch);

                }
            }

            /// <summary>
            /// Releases the channel to its pool.
            /// </summary>
            public virtual void release()
            {
                lock (this)
                {

                    log.Debug("Release .... ");

                    if (!closed)
                    {
                        if (!released)
                        {
                            /*
							 * NB 
							 * It seems that Netty closes a channel if it is released twice!
							 */
                            released = true;
                            pool.ReleaseAsync(ch);
                        }
                    }
                }
            }

            /// <summary>
            /// Closes the channel if it has not been released yet.
            /// </summary>
            public virtual void close()
            {
                lock (this)
                {
                    if (!released)
                    {
                        if (!closed)
                        {
                            if (logPool.IsDebugEnabled)
                            {
                                logPool.Debug("WS channel closed [" + ch.Id + "]");
                            }
                            closed = true;
                            ch.CloseAsync();
                            
                            // NB putting a closed channel in the pool has no bad effect and further completes its life cycle
                            pool.ReleaseAsync(ch);
                        }
                    }
                }
            }

            public virtual void onBroken(string message, Exception cause)
            {
                lock (this)
                {
                    // Debug.Assert(ch.EventLoop.InEventLoop);
                    log.Error("Websocket write failed [" + ch.Id + "]: " + message, cause);
                    close();
                    networkListener.onBroken();
                }
            }

            public override string ToString()
            {
                lock (this)
                {
                    return "" + ch.Id;
                }
            }
        }

        /// <summary>
        /// Parses the messages coming from a channel and forwards them to the corresponding <seealso cref="RequestListener"/>. 
        /// </summary>
        private class WebSocketChannelHandler : SimpleChannelInboundHandler<IByteBuffer>
        {

            internal readonly LineAssembler lineAssembler;
            internal readonly RequestListenerDecorator reqListenerDecorator;

            public WebSocketChannelHandler(RequestListener networkListener, MyChannel ch)
            {
                this.reqListenerDecorator = new RequestListenerDecorator(networkListener, ch);
                this.lineAssembler = new LineAssembler(reqListenerDecorator);
            }

            protected override void ChannelRead0(IChannelHandlerContext ctx, IByteBuffer msg)
            {
                lineAssembler.readBytes(msg);
            }

            public void handlerAdded(IChannelHandlerContext ctx)
            {
                if (log.IsDebugEnabled)
                {
                    IChannel ch = ctx.Channel;
                }
                reqListenerDecorator.onOpen();
            }

            public void channelActive(IChannelHandlerContext ctx)
            {
                if (log.IsDebugEnabled)
                {
                    IChannel ch = ctx.Channel;
                    log.Debug("WebSocket active [" + ch.Id + "]");
                }
                ctx.FireChannelActive();
            }

            public void channelInactive(IChannelHandlerContext ctx)
            {
                if (log.IsDebugEnabled)
                {
                    log.Debug("WebSocket disconnected [" + ctx.Channel.Id + "]");
                }
                reqListenerDecorator.onClosed();
            }

            public void exceptionCaught(IChannelHandlerContext ctx, Exception cause)
            {
                if (log.IsDebugEnabled)
                {
                    log.Error("WebSocket error [" + ctx.Channel.Id + "]", cause);
                }
                reqListenerDecorator.onBroken();
            }
        } // end WebSocketChannelHandler

        /// <summary>
        /// A <seealso cref="RequestListener"/> which releases the connection to its pool when the method {@code onMessage} encounters
        /// the message {@code LOOP} or {@code END}. 
        /// </summary>
        private class RequestListenerDecorator : RequestListener
        {

            internal readonly RequestListener listener;
            internal readonly MyChannel ch;

            public RequestListenerDecorator(RequestListener listener, MyChannel ch)
            {
                this.listener = listener;
                this.ch = ch;
            }

            public virtual void onMessage(string message)
            {
                listener.onMessage(message);
                MatchCollection mLoop = TextProtocol.LOOP_REGEX.Matches(message);
                MatchCollection mEnd = TextProtocol.END_REGEX.Matches(message);
                if (( mLoop.Count > 0 ) || ( mEnd.Count > 0 ))
                {
                    ch.release();
                }
            }

            public virtual void onOpen()
            {
                listener.onOpen();
            }

            public virtual void onClosed()
            {
                listener.onClosed();
            }

            public virtual void onBroken()
            {
                listener.onBroken();
            }
        } // end RequestListenerDecorator
    }
}