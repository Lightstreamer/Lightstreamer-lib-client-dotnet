using com.lightstreamer.client.requests;
using com.lightstreamer.client.session;
using com.lightstreamer.client.transport;
using com.lightstreamer.util;
using Lightstreamer.DotNet.Logging.Log;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using static com.lightstreamer.client.protocol.TextProtocol;
using static com.lightstreamer.client.transport.WebSocket;

namespace com.lightstreamer.client.protocol
{
    /// <summary>
    /// The manager forwards the requests to the WebSocket transport ensuring that if the underlying connection is not ready,
    /// the requests are buffered and sent later.
    /// <para>
    /// <b>Note 1</b>
    /// Method <seealso cref="#openSocket(String, StreamListener)"/> is used when the flag isEarlyWSOpenEnabled is set. If the method is not called explicitly,
    /// method <seealso cref="#bindSession(SessionRequest, RequestListener, long, long, ListenableFuture)"/> will call it.
    /// </para>
    /// <para>
    /// <b>Note 2</b>
    /// If method <seealso cref="#openSocket(String, StreamListener)"/> is called twice in a row (this can happen if the server sends a control-link), 
    /// the final effect is to close the old socket and to open a new one.
    /// </para>
    /// </summary>
    public class WebSocketRequestManager : RequestManager
    {
        private readonly ILogger log = LogManager.GetLogger(Constants.REQUESTS_LOG);
        private readonly ILogger sessionLog = LogManager.GetLogger(Constants.SESSION_LOG);

        private WebSocket wsTransport;
        private Protocol protocol;
        private readonly SessionThread sessionThread;
        private readonly InternalConnectionOptions options;
        private readonly LinkedList<PendingRequest> controlRequestQueue = new LinkedList<PendingRequest>();
        private PendingBind bindRequest;
        /// <summary>
        /// Request that the manager has sent but it has not been written on WebSocket.
        /// Must be cleared when <seealso cref="RequestListener#onOpen()"/> is called 
        /// (we assume that WebSocket is reliable).
        /// </summary>
        private PendingRequest ongoingRequest;
        /// <summary>
        /// Maps the LS_reqId of a request to the listener of the request.
        /// </summary>
        private readonly IDictionary<long, RequestListener> pendingRequestMap = new Dictionary<long, RequestListener>();
        private ListenableFuture openWsFuture;

        public WebSocketRequestManager(SessionThread sessionThread, Protocol protocol, InternalConnectionOptions options)
        {
            this.options = options;
            this.sessionThread = sessionThread;
            this.protocol = protocol;
        }

        /// <summary>
        /// Opens a WebSocket connection without binding a session (see the flag isEarlyWSOpenEnabled). 
        /// If a connection is already open, the connection is closed and a new connection is opened. </summary>
        /// <param name="serverAddress"> server address </param>
        /// <param name="streamListener"> stream connection listener </param>
        public virtual ListenableFuture openWS(Protocol protocol, string serverAddress, StreamListener streamListener)
        {
            if (wsTransport != null)
            {
                // close old connection
                wsTransport.close();
            }
            wsTransport = new WebSocket(sessionThread, options, serverAddress, streamListener, new MyConnectionListener(this));

            Debug.Assert(wsTransport.State.Equals(WebSocket.InternalState.CONNECTING));
            openWsFuture = new ListenableFuture();
            /* abort connection if opening takes too long */
            WebSocket _wsTransport = wsTransport;
            ListenableFuture _openWsFuture = openWsFuture;

            if (log.IsDebugEnabled)
            {
                log.Debug("Status timeout in " + options.CurrentConnectTimeout + " [currentConnectTimeoutWS]");
            }
            sessionThread.schedule(new Task(() =>
           {
               if (log.IsDebugEnabled)
               {
                   log.Debug("Timeout event [currentConnectTimeoutWS]");
               }
               if ( (_wsTransport.State.Equals(InternalState.CONNECTING)) || (_wsTransport.State.Equals(InternalState.UNEXPECTED_ERROR)) )
               {
                   sessionLog.Debug("WS connection: aborted");
                   _openWsFuture.reject();
                   _wsTransport.close();
                   options.increaseConnectTimeout();
               }
           }), options.CurrentConnectTimeout);
            return openWsFuture;
        }

        /// <summary>
        /// {@inheritDoc}
        /// If the socket is not open, calls <seealso cref="#openSocket(String, StreamListener)"/>.
        /// </summary>
        public virtual RequestHandle bindSession(BindSessionRequest request, StreamListener reqListener, long tcpConnectTimeout, long tcpReadTimeout, ListenableFuture bindFuture)
        {
            if (wsTransport == null)
            {
                // no transport: this case can happen when transport is polling
                bindRequest = new PendingBind(request, reqListener, bindFuture);
                openWS(protocol, request.TargetServer, reqListener);

            }
            else
            {
                // there is a transport, so openSocket was already called: the state is CONNECTED or CONNECTING 
                WebSocket.InternalState state = wsTransport.State;
                switch (state)
                {
                    case WebSocket.InternalState.CONNECTED:
                        sendBindRequest(request, reqListener, bindFuture);
                        break;

                    case WebSocket.InternalState.CONNECTING:
                        // buffer the request, which will be flushed when the client state is CONNECTED
                        Debug.Assert(bindRequest == null);
                        bindRequest = new PendingBind(request, reqListener, bindFuture);
                        break;

                    case WebSocket.InternalState.BROKEN:
                        // discard bind request: must be sent in HTTP
                        break;

                    default:
                        // Debug.Assert(false, state);
                        sessionLog.Warn("Unexpected bind request in state " + state);
                        break;
                }
            }
            // this request handle close the stream connection
            return new RequestHandleAnonymousInnerClass(this);
        }

        private class RequestHandleAnonymousInnerClass : RequestHandle
        {
            private readonly WebSocketRequestManager outerInstance;

            public RequestHandleAnonymousInnerClass(WebSocketRequestManager outerInstance)
            {
                this.outerInstance = outerInstance;
            }

            public void close(bool forceConnectionClose)
            {
                outerInstance.close(false);
            }
        }

        public virtual void addRequest(LightstreamerRequest request, RequestTutor tutor, RequestListener reqListener)
        {
            Debug.Assert(request is ControlRequest || request is MessageRequest || request is ReverseHeartbeatRequest);
            if (request is NumberedRequest)
            {
                /*
				 * for numbered requests (i.e. having a LS_reqId) the client expects a REQOK/REQERR notification from the server 
				 */

                NumberedRequest numberedReq = (NumberedRequest)request;
                Debug.Assert(!pendingRequestMap.ContainsKey(numberedReq.RequestId));
                pendingRequestMap[numberedReq.RequestId] = reqListener;

                sessionLog.Debug("Pending request - post - " + numberedReq.RequestId);
            }
            if (wsTransport == null)
            {
                // no transport: this case can happen for example when the flag isEarlyWSOpenEnabled is off.
                // buffer the request and await the binding of the session
                controlRequestQueue.AddLast(new PendingRequest(request, reqListener, tutor));

            }
            else
            {
                // there is a transport, so openSocket was already called: the state is CONNECTED or CONNECTING 
                InternalState state = wsTransport.State;
                switch (state)
                {
                    case InternalState.CONNECTED:
                        sendControlRequest(request, reqListener, tutor);
                        break;

                    case InternalState.CONNECTING:
                        // buffer the requests, which will be flushed when the client state is CONNECTED
                        controlRequestQueue.AddLast(new PendingRequest(request, reqListener, tutor));
                        break;

                    default:
                        // Debug.Assert(false);
                        sessionLog.Warn("Unexpected request " + request.RequestName + " in state " + state);
                        break;
                }
            }
        }

        private void sendControlRequest(LightstreamerRequest request, RequestListener reqListener, RequestTutor tutor)
        {
            ongoingRequest = new PendingRequest(request, reqListener, tutor);

            wsTransport.sendRequest(protocol, request, new ListenerWrapperAnonymousInnerClass(this, reqListener)
           , null, null, 0, 0);
        }

        private class ListenerWrapperAnonymousInnerClass : ListenerWrapper
        {
            private readonly WebSocketRequestManager outerInstance;

            public ListenerWrapperAnonymousInnerClass(WebSocketRequestManager outerInstance, RequestListener reqListener) : base(outerInstance, reqListener)
            {
                this.outerInstance = outerInstance;
            }

            public override void doOpen()
            {
                /* the request has been sent: clear the field */
                outerInstance.ongoingRequest = null;
            }
        }

        private void sendBindRequest(LightstreamerRequest request, RequestListener reqListener, ListenableFuture bindFuture)
        {
            wsTransport.sendRequest(protocol, request, new ListenerWrapper(this, reqListener), null, null, 0, 0);
            bindFuture.fulfill();
        }

        public virtual void close(bool waitPending)
        {
            if (log.IsDebugEnabled)
            {
                log.Debug("ws closing :" + wsTransport );
            }
            if (wsTransport != null)
            {

                wsTransport.close();
                wsTransport = null;
            }
        }

        public virtual long RequestLimit
        {
            set
            {
                /*
				 * The limit is important when a manager sends the requests in batch to limit the dimension of the batch.
				 * Since this manager sends requests one by one, the limit is useless.
				 * Note that if a single request is bigger than the limit, the manager
				 * sends it anyway but the server will refuse it.
				 */
            }
        }

        public virtual void copyTo(ControlRequestHandler newHandler)
        {
            if (ongoingRequest != null)
            {
                newHandler.addRequest(ongoingRequest.request, ongoingRequest.tutor, ongoingRequest.reqListener);
            }
            foreach (PendingRequest pendingRequest in controlRequestQueue)
            {
                newHandler.addRequest(pendingRequest.request, pendingRequest.tutor, pendingRequest.reqListener);
            }
            /* clear memory */
            ongoingRequest = null;
            controlRequestQueue.Clear();
        }

        /// <summary>
        /// Sets the default session id of a WebSocket connection.
        /// The default id is the id returned in the CONOK response of a bind_session.
        /// It lasts until the receiving of a LOOP or END message.
        /// </summary>
        public virtual string DefaultSessionId
        {
            set
            {
                Debug.Assert(wsTransport != null);
                wsTransport.DefaultSessionId = value;
            }
        }

        /// <summary>
        /// Finds the listener associated with the request.
        /// If found, removes it from the list of pending requests.
        /// </summary>
        public virtual RequestListener getAndRemoveRequestListener(long reqId)
        {
            RequestListener reqListener = pendingRequestMap.GetValueOrNull(reqId);
            pendingRequestMap.Remove(reqId);


            return reqListener;
        }

        /// <summary>
        /// Sends the requests (when the state is CONNECTED) which were buffered because the connection wasn't ready.
        /// </summary>
        private class MyConnectionListener : WebSocket.ConnectionListener
        {
            private readonly WebSocketRequestManager outerInstance;

            public MyConnectionListener(WebSocketRequestManager outerInstance)
            {

                this.outerInstance = outerInstance;

            }

            public virtual void onOpen()
            {
                outerInstance.openWsFuture.fulfill();
                /* send bind_session */
                if (outerInstance.bindRequest != null)
                {
                    // bind request has precedence over control requests
                    outerInstance.sendBindRequest(outerInstance.bindRequest.request, outerInstance.bindRequest.reqListener, outerInstance.bindRequest.bindFuture);
                }
                /* send control requests */
                foreach (PendingRequest controlRequest in outerInstance.controlRequestQueue)
                {
                    outerInstance.sendControlRequest(controlRequest.request, controlRequest.reqListener, controlRequest.tutor);
                }
                /* release memory */
                outerInstance.bindRequest = null;
                outerInstance.controlRequestQueue.Clear();
            }

            public virtual void onBroken()
            {
                // NB the callback caller must assure the execution on SessionThread
                outerInstance.openWsFuture.reject();
            }
        }

        private class PendingRequest
        {
            internal readonly LightstreamerRequest request;
            internal readonly RequestListener reqListener;
            internal readonly RequestTutor tutor;

            public PendingRequest(LightstreamerRequest request, RequestListener reqListener, RequestTutor tutor)
            {
                this.request = request;
                this.reqListener = reqListener;
                this.tutor = tutor;
            }
        }

        private class PendingBind : PendingRequest
        {
            internal readonly ListenableFuture bindFuture;

            public PendingBind(LightstreamerRequest request, RequestListener reqListener, ListenableFuture bindFuture) : base(request, reqListener, null)
            {
                this.bindFuture = bindFuture;
            }
        }

        /// <summary>
        /// A wrapper assuring that the method <seealso cref="RequestListener#onOpen()"/> is executed
        /// in the SessionThread.
        /// </summary>
        private class ListenerWrapper : RequestListener
        {
            private readonly WebSocketRequestManager outerInstance;

            internal readonly RequestListener reqListener;

            public ListenerWrapper(WebSocketRequestManager outerInstance, RequestListener listener)
            {
                this.outerInstance = outerInstance;
                reqListener = listener;
            }

            /// <summary>
            /// Extra-operations to perform before executing <seealso cref="RequestListener#onOpen()"/>.
            /// </summary>
            public virtual void doOpen()
            {

            }

            public void onOpen()
            {
                outerInstance.sessionThread.queue(new Task(() =>
               {
                   doOpen();
                   reqListener.onOpen(); // onOpen fires the retransmission timeout
                }));
            }

            public void onMessage(string message)
            {
                reqListener.onMessage(message);
            }

            public void onClosed()
            {
                reqListener.onClosed();
            }

            public void onBroken()
            {
                reqListener.onBroken();
            }
        }
    }
}