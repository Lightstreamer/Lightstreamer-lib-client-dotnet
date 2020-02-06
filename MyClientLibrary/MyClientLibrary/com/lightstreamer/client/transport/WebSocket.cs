using com.lightstreamer.client.transport.providers;
using Lightstreamer.DotNet.Logging.Log;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace com.lightstreamer.client.transport
{

    using CookieHelper = com.lightstreamer.client.transport.providers.CookieHelper;
    using InternalConnectionOptions = com.lightstreamer.client.session.InternalConnectionOptions;
    using LightstreamerRequest = com.lightstreamer.client.requests.LightstreamerRequest;
    using Protocol = com.lightstreamer.client.protocol.Protocol;
    using SessionThread = com.lightstreamer.client.session.SessionThread;
    using StreamListener = com.lightstreamer.client.protocol.TextProtocol.StreamListener;
    using ThreadShutdownHook = com.lightstreamer.util.threads.ThreadShutdownHook;
    using WebSocketProvider = com.lightstreamer.client.transport.providers.WebSocketProvider;

    /// <summary>
    /// A WebSocket transport implemented using <seealso cref="WebSocketProvider"/>.
    /// <br>
    /// Its main responsibility are:
    /// <ol>
    /// <li>exposing a method to write frames into the connection (see <seealso cref="#sendRequest"/>)</li>
    /// <li>notifying the listeners of the events coming from the connection</li>
    /// <li>assuring that the notifications are executed by the SessionThread.</li>
    /// </ol>
    /// </summary>
    public class WebSocket : Transport
    {

        private static readonly ILogger log = LogManager.GetLogger(Constants.TRANSPORT_LOG);

        private readonly SessionThread sessionThread;
        private readonly InternalConnectionOptions options;
        private readonly WebSocketProvider wsClient;
        private readonly MySessionRequestListener sessionListener;
        /// <summary>
        /// When not null, the requests may omit the parameter LS_session because it is implicitly equal to this value.<br>
        /// This value is always equal to the LS_session parameter of the last sent bind_session request.
        /// </summary>
        private string defaultSessionId;

        public WebSocket(SessionThread sessionThread, InternalConnectionOptions options, string serverAddress, StreamListener streamListener, ConnectionListener connListener)
        {
            this.sessionThread = sessionThread;
            this.options = options;
            if (TransportFactory<WebSocketProvider>.DefaultWebSocketFactory == null)
            {
                /* Note:
				 * this is a temporary hack. If the WebSocket support is not available, the transport uses a void implementation
				 * which just ignores the requests.
				 * The goal is to emulate the behavior of the old (non TLCP) Android compact client.
				 * That client doesn't support WebSocket, so when a user forces WebSocket transport
				 * the client simply ignores the requests.
				 * In the future we must address this question and work out a more user-friendly API behavior. 
				 */
                this.wsClient = new DummyWebSocketClient();
            }
            else
            {
                this.wsClient = TransportFactory<WebSocketProvider>.DefaultWebSocketFactory.getInstance(sessionThread);

            }

            this.sessionListener = new MySessionRequestListener(sessionThread, streamListener, connListener);
            open(serverAddress, streamListener, connListener);

            if (log.IsDebugEnabled)
            {
                log.Debug("WebSocket transport - : " + sessionListener.state);
            }
        }

        /// <summary>
        /// Opens a WebSocket connection.
        /// </summary>
        /// <param name="serverAddress"> target address </param>
        /// <param name="streamListener"> is exposed to the following connection events: opening, closing, reading a message, catching an error. 
        /// For each event the corresponding listener method is executed on the SessionThread. </param>
        /// <param name="connListener"> is only exposed to the event opening connection. The listener method is executed on the SessionThread. </param>
        private void open(string serverAddress, StreamListener streamListener, ConnectionListener connListener)
        {

            Debug.Assert(sessionListener.state == InternalState.NOT_CONNECTED);

            sessionThread.registerWebSocketShutdownHook(wsClient.ThreadShutdownHook);
            Uri uri;
            try
            {
                uri = new Uri(serverAddress + "lightstreamer");
            }
            catch (Exception e)
            {
                throw new System.InvalidOperationException(e.Message); // should never happen
            }
            string cookies = CookieHelper.getCookieHeader(uri);
            wsClient.connect(uri.ToString(), sessionListener, options.HttpExtraHeadersOnSessionCreationOnly ? null : options.HttpExtraHeaders, cookies, options.Proxy);
            sessionListener.state = InternalState.CONNECTING;

        }

        public virtual RequestHandle sendRequest(Protocol protocol, LightstreamerRequest request, RequestListener listener, IDictionary<string, string> extraHeaders, Proxy proxy, long tcpConnectTimeout, long tcpReadTimeout)
        {
            /* the parameters protocol, extraHeaders, proxy, tcpConnectTimeout and tcpReadTimeout 
			 * have no meaning for WebSocket connections */
            Debug.Assert(extraHeaders == null && proxy == null && tcpConnectTimeout == 0 && tcpReadTimeout == 0);

            string frame = request.RequestName + "\r\n" + request.getTransportAwareQueryString(defaultSessionId, false);
            wsClient.send(frame, listener);
            return new RequestHandleAnonymousInnerClass(this);
        }

        private class RequestHandleAnonymousInnerClass : RequestHandle
        {
            private readonly WebSocket outerInstance;

            public RequestHandleAnonymousInnerClass(WebSocket outerInstance)
            {
                this.outerInstance = outerInstance;
            }

            public void close(bool forceConnectionClose)
            {
                /* Note:
				 * this method must not be used. 
				 * In order to close the connection, use WebSocket.close() instead.
				 */
                Debug.Assert(false);
            }
        }

        /// <summary>
        /// Closes the connection.
        /// </summary>
        public virtual void close()
        {
            // Debug.Assert(Assertions.SessionThread);
            sessionListener.close();
            wsClient.disconnect();
        }

        public virtual InternalState State
        {
            get
            {
                return sessionListener.state;
            }
        }

        public virtual string DefaultSessionId
        {
            set
            {
                defaultSessionId = value;
            }
        }

        /// <summary>
        /// Forwards the messages coming from the data stream to the connection listeners.
        /// <para>
        /// NB All the methods must be called by SessionThread in order to fulfill the contract of <seealso cref="WebSocket#open"/>.
        /// </para>
        /// </summary>
        private class MySessionRequestListener : SessionRequestListener
        {

            internal readonly SessionThread sessionThread;
            internal readonly StreamListener streamListener;
            internal readonly ConnectionListener connectionListener;
            /// <summary>
            /// <b>NB</b> state must be volatile because it is read by methods of <seealso cref="MySessionRequestListener"/> 
            /// which are NOT called by Session Thread. 
            /// </summary>
            internal volatile InternalState state = InternalState.NOT_CONNECTED;

            internal MySessionRequestListener(SessionThread sessionThread, StreamListener streamListener, ConnectionListener connListener)
            {
                this.sessionThread = sessionThread;
                this.streamListener = streamListener;
                this.connectionListener = connListener;
            }

            /*
			 * Note 1
			 * Methods below are called by WebSocketProvider internal threads.
			 * Their bodies must be delegated to SessionThread.
			 * 
			 * Note 2
			 * It can happen that the socket is disconnected but the listeners, notified by Netty,
			 * still receive events (for example when the socket is closed because the control link has changed). 
			 * These events must be ignored.
			 */

            public virtual void onOpen()
            {

                sessionThread.queue(new Task(() =>
               {
                   if (state.Equals(InternalState.DISCONNECTED))
                   {
                       log.Warn("onOpen event discarded");
                       return;
                   }
                   state = InternalState.CONNECTED;
                   if (log.IsDebugEnabled)
                   {
                       log.Debug("WebSocket transport onOpen: " + state);
                   }
                   connectionListener.onOpen();
               }));
            }

            public virtual void onMessage(string frame)
            {
                sessionThread.queue(new Task(() =>
               {
                   if (state.Equals(InternalState.DISCONNECTED))
                   {
                       log.Warn("onMessage event discarded: " + frame);
                       return;
                   }
                   streamListener.onMessage(frame);
               }));
            }

            public virtual void onClosed()
            {
                sessionThread.queue(new Task(() =>
               {
                   if (state.Equals(InternalState.DISCONNECTED))
                   {
                       log.Warn("onClosed event discarded");
                       return;
                   }
                   streamListener.onClosed();
               }));
            }

            public virtual void onBroken()
            {
                sessionThread.queue(new Task(() =>
               {
                   if (state.Equals(InternalState.DISCONNECTED))
                   {
                       log.Warn("onBroken event discarded");
                       return;
                   }
                   state = InternalState.BROKEN;
                   connectionListener.onBroken();
                   streamListener.onBrokenWS();
               }));
            }

            internal virtual void close()
            {
                state = InternalState.DISCONNECTED;
                if (streamListener != null)
                {
                    streamListener.disable();
                    streamListener.onClosed();
                }
                if (log.IsDebugEnabled)
                {
                    log.Debug("WebSocket transport (close): " + state);
                }
            }
        }

        public enum InternalState
        {
            /// <summary>
            /// Initial state.
            /// </summary>
            NOT_CONNECTED,
            /// <summary>
            /// State after calling <seealso cref="WebSocket#open(String, StreamListener, ConnectionListener)"/>.
            /// </summary>
            CONNECTING,
            /// <summary>
            /// State after the method <seealso cref="WebSocketRequestListener#onOpen()"/> is called.
            /// </summary>
            CONNECTED,
            /// <summary>
            /// State after calling <seealso cref="WebSocket#close()"/>. In this state, the listeners are disabled.
            /// </summary>
            DISCONNECTED,
            /// <summary>
            /// Transport can't connect to the server.
            /// </summary>
            BROKEN
        }

        /// <summary>
        /// Callback interface to capture connection opening event.
        /// </summary>
        public interface ConnectionListener
        {
            /// <summary>
            /// Fired when the connection is established.
            /// </summary>
            void onOpen();
            /// <summary>
            /// Fired when the connection can't be established.
            /// </summary>
            void onBroken();
        }

        private class DummyWebSocketClient : WebSocketProvider
        {
            public virtual void connect(string address, SessionRequestListener networkListener, IDictionary<string, string> extraHeaders, string cookies, Proxy proxy)
            {
            }
            public virtual void disconnect()
            {
            }
            public virtual void send(string message, RequestListener listener)
            {
            }
            public virtual ThreadShutdownHook ThreadShutdownHook
            {
                get
                {
                    return new ThreadShutdownHookAnonymousInnerClass(this);
                }
            }

            private class ThreadShutdownHookAnonymousInnerClass : ThreadShutdownHook
            {
                private readonly DummyWebSocketClient outerInstance;

                public ThreadShutdownHookAnonymousInnerClass(DummyWebSocketClient outerInstance)
                {
                    this.outerInstance = outerInstance;
                }

                public void onShutdown()
                {
                }
            }
        }

        /*
		 * --------------------------------------------------------------------
		 * Other stuff
		 * --------------------------------------------------------------------
		 */

        private static bool disabled = false;

        public static bool Disabled
        {
            get
            {
                return disabled;
            }
        }

        public static void restore()
        {
            disabled = false;
        }

        public static void disable()
        {
            disabled = true;
        }
    }
}