using com.lightstreamer.client.requests;
using com.lightstreamer.client.session;
using com.lightstreamer.client.transport;
using com.lightstreamer.util;
using Lightstreamer.DotNet.Logging.Log;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using static com.lightstreamer.client.protocol.ControlResponseParser;
using static com.lightstreamer.client.protocol.TextProtocol;

/*
 * Copyright (c) 2004-2019 Lightstreamer s.r.l., Via Campanini, 6 - 20124 Milano, Italy.
 * All rights reserved.
 * www.lightstreamer.com
 *
 * This software is the confidential and proprietary information of
 * Lightstreamer s.r.l.
 * You shall not disclose such Confidential Information and shall use it
 * only in accordance with the terms of the license agreement you entered
 * into with Lightstreamer s.r.l.
 */
namespace com.lightstreamer.client.protocol
{
    /// <summary>
    /// From "Unified Client API" document:
    /// <blockquote>
    /// <h3>Control Request Batching</h3>
    /// Control connections are automatically serialized and batched:
    /// the first request is sent as soon as possible, subsequent requests are batched together while
    /// the previous connection is open (the concept of "open" may vary depending on the
    /// technology in use; the purpose is to always have at max 1 open socket dedicated to control
    /// requests). Note that during websocket sessions, there is no need to batch, nor there is need
    /// to wait for a roundtrip before issuing a new control request, so that if a websocket is in use
    /// control requests are all sent "as soon as possible" and only batched if the dequeing thread
    /// finds more than one request ready when executing.
    /// <para>
    /// Note that as the server specifies a maximum length for control requests body contents a
    /// batch may not contain all the available requests. Such limit must always be respected unless
    /// one single request surpasses the limit: in that case the requests is sent on its own even if we
    /// already know that the server will refuse it.
    /// </para>
    /// <para>
    /// Note that each control request is always bound to a session. As a consequence if the related
    /// session ends while the request is on the wire such request becomes completely useless:
    /// when the related session is closed any socket that is currently used to send control
    /// request(s) MUST be closed (it obviously does not apply to session running over websocket
    /// since such sockets are closed together with the session end).
    /// </para>
    /// <para>
    /// Some kind of Control Requests may not be compatible to be sent in the same batch. Due to
    /// this the client will keep different lists and will choose which one to dequeue from via
    /// roundrobin.
    /// These are the different kinds of batches:
    /// <ol>
    /// <li>control: subscription, unsubscription and constraint (currently only bandwidth change
    /// is performed through constraint requests)</li>
    /// <li>msg: messages</li>
    /// <li>heartbeat: reverse heartbeats. These are never batched and only sent if there was
    /// silence on the control channel for a configurable time.</li>
    /// <li>send_log: remote client logging; it is not mandatory to implement these messages</li>
    /// <li>control: destroy requests are compatible with the first category but, while usually
    /// control requests are sent to the currently active server instance address (unless it is
    /// specified to ignore the server instance address), these requests must be sent to the
    /// server where the old session was open. For this reason this requests are never
    /// batched</li>
    /// </ol>
    /// 
    /// <h3>Control Connection timeout algorithm</h3>
    /// In case no response, either synchronous or asynchronous, for a certain control connection,
    /// is not received within 4 seconds, the missing control request will be sent again to the
    /// batching algorithm (note that the 4 second timeout starts when the request is sent on the
    /// net, not when the request is sent to the batching algorithm). The timeout is then doubled
    /// each time a request is sent again. Also the timeout is extended with the pollingInterval value
    /// to prevent sending useless requests during "short polling" sessions.
    /// IMPLEMENTATION NOTE: the WebSocket case has no synchronous request/responses.
    /// IMPLEMENTATION NOTE: if any control response, excluding destroy requests, returns with
    /// a "sync error", the client will drop the current session and will open a new one.
    /// IMPLEMENTATION NOTE: the Web/Node.js clients currently only handle the sync error
    /// from synchronous responses (i.e. ignores ok or other kind of errors, including network errors
    /// and waits for such notifications on the stream connection)
    /// IMPLEMENTATION NOTE: the HTML might not have the chance to read the synchronous
    /// responses (control.html cases and JSONP cases).
    /// </blockquote> 
    /// 
    /// </para>
    /// </summary>
    public class HttpRequestManager : RequestManager
    {
        private bool InstanceFieldsInitialized = false;

        private void InitializeInstanceFields()
        {
            requestQueues = new BatchRequest[] { messageQueue, controlQueue, destroyQueue, hbQueue };
        }

        private const string IDLE = "IDLE";
        private const string WAITING = "WAITING";
        private const string END = "END";
        private const string ENDING = "ENDING";


        private readonly ILogger log = LogManager.GetLogger(Constants.REQUESTS_LOG);

        private readonly BatchRequest messageQueue = new BatchRequest(BatchRequest.MESSAGE);
        private readonly BatchRequest controlQueue = new BatchRequest(BatchRequest.CONTROL);
        private readonly BatchRequest destroyQueue = new BatchRequest(BatchRequest.CONTROL);
        private readonly BatchRequest hbQueue = new BatchRequest(BatchRequest.HEARTBEAT);

        private BatchRequest[] requestQueues;

        private long requestLimit = 0;
        private int nextQueue = 0; // handles turns (control-sendMessage-sendLog)

        private string status = IDLE;
        private int statusPhase = 1;
        private SessionThread sessionThread;
        private Transport transport;
        private Protocol protocol;
        private InternalConnectionOptions options;

        private RequestHandle activeConnection;

        private readonly FatalErrorListener errorListener;
        /// <summary>
        /// List of requests that the manager has sent but no response has still arrived.
        /// Must be cleared when <seealso cref="RequestListener#onClosed()"/> or <seealso cref="RequestListener#onBroken()"/>
        /// is called.
        /// </summary>
        private readonly LinkedList<RequestObjects> ongoingRequests = new LinkedList<RequestObjects>();

        internal HttpRequestManager(SessionThread thread, Transport transport, InternalConnectionOptions options) : this(thread, null, transport, options, null)
        {
            if (!InstanceFieldsInitialized)
            {
                InitializeInstanceFields();
                InstanceFieldsInitialized = true;
            }
        }

        internal HttpRequestManager(SessionThread thread, Protocol protocol, Transport transport, InternalConnectionOptions options, FatalErrorListener errListener)
        {
            if (!InstanceFieldsInitialized)
            {
                InitializeInstanceFields();
                InstanceFieldsInitialized = true;
            }
            this.sessionThread = thread;
            this.transport = transport;
            this.protocol = protocol;
            this.options = options;
            this.errorListener = errListener;
        }

        private bool @is(string status)
        {
            return this.status.Equals(status);
        }

        private bool isNot(string status)
        {
            return !this.@is(status);
        }

        public virtual void close(bool waitPending)
        {
            if (!waitPending || this.activeConnection == null)
            {
                if (this.activeConnection != null)
                {
                    if (requestQueues[this.nextQueue] != destroyQueue)
                    {
                        this.activeConnection.close(false);
                    } //else do not bother destroy requests
                }
                this.changeStatus(END);
            }
            else
            {
                this.changeStatus(ENDING);
            }
        }

        private void changeStatus(string newStatus)
        {
            this.statusPhase++; //used to verify dequeue and sendHeartbeats calls
            this.status = newStatus;
        }

        public virtual long RequestLimit
        {
            set
            {
                this.requestLimit = value;
            }
        }

        private bool addToProperBatch(LightstreamerRequest request, RequestTutor tutor, RequestListener listener)
        {
            if (request is MessageRequest)
            {
                if (log.IsDebugEnabled)
                {
                    log.Debug("New Message request: " + request.RequestName + ", " + listener);
                }

                return this.messageQueue.addRequestToBatch((MessageRequest)request, tutor, listener);
            }
            else if (request is ReverseHeartbeatRequest)
            {
                return this.hbQueue.addRequestToBatch((ReverseHeartbeatRequest)request, tutor, listener);
            }
            else if (request is ConstrainRequest)
            {
                return this.controlQueue.addRequestToBatch((ConstrainRequest)request, tutor, listener);
            }
            else if (request is ForceRebindRequest)
            {
                return this.controlQueue.addRequestToBatch((ForceRebindRequest)request, tutor, listener);
            }
            else if (request is UnsubscribeRequest)
            {
                return this.controlQueue.addRequestToBatch((UnsubscribeRequest)request, tutor, listener);
            }
            else if (request is SubscribeRequest)
            {
                return this.controlQueue.addRequestToBatch((SubscribeRequest)request, tutor, listener);
            }
            else if (request is ChangeSubscriptionRequest)
            {
                return this.controlQueue.addRequestToBatch((ChangeSubscriptionRequest)request, tutor, listener);
            }
            else if (request is DestroyRequest)
            {
                return this.destroyQueue.addRequestToBatch((DestroyRequest)request, tutor, listener);
            }
            else
            {
                return false;
            }
        }

        public virtual void copyTo(ControlRequestHandler newHandler)
        {
            // We might want to skip destroy requests and send them on the network instead

            if (ongoingRequests.Count > 0)
            {
                foreach (RequestObjects req in ongoingRequests)
                {
                    newHandler.addRequest(req.request, req.tutor, req.listener);
                }
                ongoingRequests.Clear();
            }
            for (int i = 0; i < this.requestQueues.Length; i++)
            {
                RequestObjects migrating;
                while (( migrating = this.requestQueues[i].shift() ) != null)
                {
                    newHandler.addRequest(migrating.request, migrating.tutor, migrating.listener);
                }
            }

            newHandler.RequestLimit = this.requestLimit;
        }

        public virtual void addRequest(LightstreamerRequest request, RequestTutor tutor, RequestListener listener)
        {

            Debug.Assert(request is ControlRequest || request is MessageRequest || request is ReverseHeartbeatRequest);

            if (this.@is(END) || this.@is(ENDING))
            {
                log.Error("Unexpected call on dismissed batch manager: " + request.TransportUnawareQueryString);
                throw new System.InvalidOperationException("Unexpected call on dismissed batch manager");
            }

            this.addToProperBatch(request, tutor, listener);

            if (this.@is(IDLE))
            {
                this.dequeue(SYNC_DEQUEUE, "add");
            }
            else
            {
                //we're already busy, we'll dequeue when we'll be back
                log.Debug("Request manager busy: the request will be sent later " + request);
            }
        }

        public virtual RequestHandle createSession(CreateSessionRequest request, StreamListener reqListener, long tcpConnectTimeout, long tcpReadTimeout)
        {
            return transport.sendRequest(protocol, request, reqListener, options.HttpExtraHeaders, options.Proxy, tcpConnectTimeout, tcpReadTimeout);
        }

        public virtual RequestHandle bindSession(BindSessionRequest request, StreamListener reqListener, long tcpConnectTimeout, long tcpReadTimeout, ListenableFuture requestFuture)
        {
            RequestHandle handle = transport.sendRequest(protocol, request, reqListener, options.HttpExtraHeadersOnSessionCreationOnly ? null : options.HttpExtraHeaders, options.Proxy, tcpConnectTimeout, tcpReadTimeout);
            requestFuture.fulfill();
            return handle;
        }

        public virtual RequestHandle recoverSession(RecoverSessionRequest request, StreamListener reqListener, long tcpConnectTimeout, long tcpReadTimeout)
        {
            return transport.sendRequest(protocol, request, reqListener, options.HttpExtraHeadersOnSessionCreationOnly ? null : options.HttpExtraHeaders, options.Proxy, tcpConnectTimeout, tcpReadTimeout);
        }

        private static long SYNC_DEQUEUE = -1;
        private static long ASYNC_DEQUEUE = 0;

        private void dequeue(long delay, string who)
        {
            if (delay == SYNC_DEQUEUE)
            {
                if (log.IsDebugEnabled)
                {
                    log.Debug("Ready to dequeue control requests to be sent to server");
                }

                this.dequeueControlRequests(this.statusPhase, who);

            }
            else
            {
                int sc = this.statusPhase;
                Task task = new Task(() =>
               {
                   dequeueControlRequests(sc, "async." + who);
               });

                if (delay == ASYNC_DEQUEUE)
                {
                    sessionThread.queue(task);
                }
                else
                {
                    sessionThread.schedule(task, delay);
                }
            }
        }

        private void dequeueControlRequests(int statusPhase, string who)
        {
            if (statusPhase != this.statusPhase)
            {
                return;
            }

            if (this.isNot(IDLE))
            {
                if (this.@is(WAITING))
                {
                    //might happen if an async dequeue is surpassed by a sync one
                    return;
                }
                else if (this.@is(END))
                {
                    //game over
                    return;
                }
                else if (@is(ENDING))
                {
                    log.Error("dequeue call on unexpected status");
                    this.changeStatus(END);
                    return;
                }
            }

            int c = 0;
            while (c < this.requestQueues.Length)
            {

                //switch the flag to change turn
                nextQueue = nextQueue < requestQueues.Length - 1 ? nextQueue + 1 : 0;

                if (requestQueues[nextQueue].Length > 0)
                {
                    bool sent = sendBatch(requestQueues[nextQueue]);
                    if (sent)
                    {
                        changeStatus(WAITING);
                        return;
                    }
                }
                c++;
            }

            //nothing to send, we're still IDLE

        }

        private bool sendBatch(BatchRequest batch)
        {
            if (batch.Length <= 0)
            {
                //something wrong o_O
                log.Error("Unexpected call");

                //XXX exit here??
            }

            BatchedListener combinedRequestListener = new BatchedListener(this);
            BatchedRequest combinedRequest = new BatchedRequest(this);

            /* find the first request to be sent: it provides the server address and the request name for the whole combined request */
            RequestObjects first = null;
            while (first == null && batch.Length > 0)
            {
                first = batch.shift();
                if (first.tutor.shouldBeSent())
                {

                    combinedRequest.Server = first.request.TargetServer;
                    combinedRequest.RequestName = first.request.RequestName;

                    combinedRequest.add(first.request);
                    combinedRequestListener.add(first.listener);
                    ongoingRequests.AddLast(first);
                }
                else
                {
                    first.tutor.notifyAbort();
                    first = null;
                }
            }
            if (combinedRequest.length() == 0)
            {
                //nothing to send
                return false;
            }
            /* add the other requests to the combined request: they share the server address and the request name */
            while (( requestLimit == 0 || ( combinedRequest.length() + batch.NextRequestLength ) < requestLimit ) && batch.Length > 0)
            {
                RequestObjects next = batch.shift();
                if (next.tutor.shouldBeSent())
                {
                    combinedRequest.add(next.request);
                    combinedRequestListener.add(next.listener);
                    ongoingRequests.AddLast(next);
                }
                else
                {
                    next.tutor.notifyAbort();
                }
            }

            activeConnection = transport.sendRequest(protocol, combinedRequest, combinedRequestListener, options.HttpExtraHeadersOnSessionCreationOnly ? null : options.HttpExtraHeaders, options.Proxy, options.TCPConnectTimeout, options.TCPReadTimeout);

            return true;
        }

        private bool onComplete(string why)
        {
            if (this.@is(END))
            {
                //don't care
                return false;
            }
            else if (this.@is(ENDING))
            {
                changeStatus(END);
            }
            else
            {
                //should be waiting
                if (this.@is(IDLE))
                {
                    log.Error("Unexpected batch manager status at connection end");
                }

                log.Info("Batch completed");

                changeStatus(IDLE);

                dequeue(ASYNC_DEQUEUE, "closed"); //prepare the future
            }
            activeConnection = null;
            return true;
        }

        public interface FatalErrorListener
        {

            void onError(int errorCode, string errorMessage);
        }

        /// <summary>
        /// The exception is thrown when a control request returns an ERROR message.
        /// When this happens, usually the right action is to close the current session without recovery.
        /// </summary>
        private class ProtocolErrorException : Exception
        {
            internal const long serialVersionUID = 1L;
            internal readonly int errorCode;

            public ProtocolErrorException(string errorCode, string errorMessage) : base(errorMessage)
            {
                this.errorCode = int.Parse(errorCode);
            }

            public virtual int ErrorCode
            {
                get
                {
                    return errorCode;
                }
            }
        }

        private class BatchedRequest : LightstreamerRequest
        {
            private readonly HttpRequestManager outerInstance;

            public BatchedRequest(HttpRequestManager outerInstance)
            {
                this.outerInstance = outerInstance;
            }


            internal StringBuilder fullRequest = new StringBuilder();
            internal string requestName;


            public override string RequestName
            {
                set => this.requestName = value;
                get
                {
                    return this.requestName;
                }
            }

            public virtual void add(LightstreamerRequest request)
            {
                if (fullRequest.Length > 0)
                {
                    fullRequest.AppendLine();
                }
                fullRequest.Append(request.getTransportAwareQueryString(null, true));
            }


            public virtual long length()
            {
                return fullRequest.Length;
            }

            public override string TransportUnawareQueryString
            {
                get
                {
                    // the caller isn't aware of the transport, but we are
                    return this.fullRequest.ToString();
                }
            }

            public override string getTransportAwareQueryString(string defaultSessionId, bool ackIsForced)
            {
                // assert(ackIsForced);
                // the caller must be aligned with the transport assumed here
                return this.fullRequest.ToString();
            }
        }

        private class BatchedListener : RequestListener
        {
            private readonly HttpRequestManager outerInstance;

            public BatchedListener(HttpRequestManager outerInstance)
            {
                this.outerInstance = outerInstance;
            }

            internal bool completed = false;
            internal readonly List<string> messages = new List<string>();
            internal readonly IList<RequestListener> listeners = new List<RequestListener>();

            public virtual int size()
            {
                return listeners.Count;
            }

            public virtual void onMessage(string message)
            {
                messages.Add(message);
            }

            public virtual void add(RequestListener listener)
            {
                listeners.Add(listener);
            }

            public virtual void onOpen()
            {
                if (outerInstance.@is(END))
                {
                    //don't care
                    return;
                }
                foreach (RequestListener listener in listeners)
                {
                    listener.onOpen();
                }
            }

            internal virtual void dispatchMessages()
            {
                /* Abnormal conditions are:
                 * - the presence of ERROR messages
                 * - an unexpected number of responses
                 */
                if (messages.Count == 1 && messages[0].StartsWith("ERROR", StringComparison.Ordinal))
                {
                    outerInstance.log.Error("Control request returned an ERROR message: " + messages);
                    string message = messages[0];
                    try
                    {
                        ERRORParser parser = new ERRORParser(message);
                        throw new ProtocolErrorException("" + parser.errorCode, parser.errorMsg);

                    }
                    catch (ParsingException)
                    {
                        throw new ProtocolErrorException("61", "Unexpected response to control request: " + message);
                    }

                }
                else if (messages.Count != listeners.Count)
                {
                    outerInstance.log.Error("Control request returned an unexpected number of responses: " + messages);
                    throw new ProtocolErrorException("61", "The number of received responses is different from the number of batched requests");

                }
                else
                {
                    // check whether there is an ERROR message
                    foreach (string msg in messages)
                    {
                        if (msg.StartsWith("ERROR", StringComparison.Ordinal))
                        {
                            outerInstance.log.Error("Control request returned at least an ERROR message: " + messages);
                            throw new ProtocolErrorException("61", "A batch of requests returned at least an ERROR message");
                        }
                    }
                }
                /* no ERROR message: process the responses */
                for (int i = 0; i < messages.Count; i++)
                {
                    listeners[i].onMessage(messages[i]);
                }
            }

            public virtual void onClosed()
            {

                outerInstance.ongoingRequests.Clear();
                if (outerInstance.@is(END))
                {
                    //don't care
                    return;
                }
                try
                {
                    if (!completed)
                    {
                        if (outerInstance.onComplete("closed"))
                        {
                            if (this.messages.Count > 0)
                            {
                                dispatchMessages();
                            }
                        }
                        completed = true;
                    }

                    foreach (RequestListener listener in listeners)
                    {
                        listener.onClosed();
                    }
                }
                catch (ProtocolErrorException e)
                {
                    if (outerInstance.errorListener != null)
                    {
                        outerInstance.errorListener.onError(e.ErrorCode, e.Message);
                    }
                }
            }

            public virtual void onBroken()
            {
                outerInstance.ongoingRequests.Clear();
                if (outerInstance.@is(END))
                {
                    //don't care
                    return;
                }
                try
                {
                    if (!completed)
                    {
                        if (outerInstance.onComplete("broken"))
                        {
                            //we might be able to salvage something if size() > 0
                            if (this.messages.Count > 0)
                            {
                                dispatchMessages();
                            }
                        }
                        completed = true;
                    }

                    foreach (RequestListener listener in listeners)
                    {
                        listener.onBroken();
                    }
                }
                catch (ProtocolErrorException e)
                {
                    if (outerInstance.errorListener != null)
                    {
                        outerInstance.errorListener.onError(e.ErrorCode, e.Message);
                    }
                }
            }
        }
    }
}