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

using com.lightstreamer.client.requests;
using com.lightstreamer.client.session;
using com.lightstreamer.client.transport;
using com.lightstreamer.util;
using Lightstreamer.DotNet.Logging.Log;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using static com.lightstreamer.client.protocol.ControlResponseParser;
using static com.lightstreamer.client.session.Session;

namespace com.lightstreamer.client.protocol
{
    public abstract class TextProtocol : Protocol
    {
        public abstract string DefaultSessionId { set; }
        public abstract ListenableFuture openWebSocketConnection(string serverAddress);
        public abstract RequestManager RequestManager { get; }

        protected internal readonly ILogger log = LogManager.GetLogger(Constants.PROTOCOL_LOG);

        public enum StreamStatus
        {
            /// <summary>
            /// Before create_session command or after LOOP message.
            /// </summary>
            NO_STREAM,
            /// <summary>
            /// After create_session or bind_session command but before CONOK/CONERR message.
            /// </summary>
            OPENING_STREAM,
            /// <summary>
            /// After CONOK message.
            /// </summary>
            READING_STREAM,
            /// <summary>
            /// After a fatal error or END message.
            /// </summary>
            STREAM_CLOSED
        }

        protected internal readonly SessionThread sessionThread;
        protected internal readonly HttpRequestManager httpRequestManager;
        private ProtocolListener session;
        private StreamListener activeListener;

        public StreamStatus status = StreamStatus.NO_STREAM;
        private long? currentProg = null;

        private RequestHandle activeConnection;

        protected internal readonly InternalConnectionOptions options;

        /// <summary>
        /// The maximum time between two heartbeats.
        /// It is the value of the parameter LS_inactivity_millis sent with a bind_session request.
        /// It doesn't change during the life of a session.
        /// </summary>
        protected internal readonly ReverseHeartbeatTimer reverseHeartbeatTimer;

        protected internal readonly int objectId;
        protected internal readonly Http httpTransport;

        public TextProtocol(int objectId, SessionThread thread, InternalConnectionOptions options, Http httpTransport)
        {
            this.httpTransport = httpTransport;
            this.objectId = objectId;
            if (log.IsDebugEnabled)
            {
                log.Debug("New protocol oid=" + this.objectId);
            }
            this.sessionThread = thread;
            this.options = options;
            this.httpRequestManager = new HttpRequestManager(thread, this, this.httpTransport, options, new FatalErrorListenerAnonymousInnerClass(this));
            reverseHeartbeatTimer = new ReverseHeartbeatTimer(thread, options);
        }

        private class FatalErrorListenerAnonymousInnerClass : HttpRequestManager.FatalErrorListener
        {
            private readonly TextProtocol outerInstance;

            public FatalErrorListenerAnonymousInnerClass(TextProtocol outerInstance)
            {
                this.outerInstance = outerInstance;
            }

            public void onError(int errorCode, string errorMessage)
            {
                outerInstance.log.Error("The server has generated an error. The session will be closed");
                outerInstance.forwardControlResponseError(errorCode, errorMessage, null);
            }
        }

        public virtual StreamStatus Status
        {
            set
            {
                this.status = value;
                if (statusIs(StreamStatus.STREAM_CLOSED) || statusIs(StreamStatus.NO_STREAM))
                {
                    //we now expect the onClose event, but we're not interested in it 
                    this.stopActive(false);
                }
            }
        }

        public virtual void SetStatus(StreamStatus value, bool forceConnectionClose)
        {
            this.status = value;
            if (statusIs(StreamStatus.STREAM_CLOSED) || statusIs(StreamStatus.NO_STREAM))
            {
                //we now expect the onClose event, but we're not interested in it 
                this.stopActive(forceConnectionClose);
            }
        }

        private bool statusIs(StreamStatus what)
        {
            return status.Equals(what);
        }

        /// <summary>
        /// Returns the {@code InternalConnectionOptions}.
        /// </summary>
        /// @deprecated This method is meant to be used ONLY as a workaround for iOS implementation, as
        ///             it requires to send a non Unified API and platform specific event through the
        ///             {@code ClientListener} interface, whose instances can be accessed through the
        ///             {@code EventDispatcher} reference inside the {@code InternalConnectionOptions}.
        ///             embedded in the
        /// 
        /// <returns> the {@code InternalConnectionOptions} </returns>
        public virtual InternalConnectionOptions Options
        {
            get
            {
                return options;
            }
        }

        public virtual void stopActive(bool force)
        {
            if (this.activeListener != null)
            {
                this.activeListener.disable();
            }
            if (this.activeConnection != null)
            {
                this.activeConnection.close(force);
            }
        }

        public virtual void copyPendingRequests(Protocol protocol)
        {
            RequestManager.copyTo(protocol.RequestManager);
            if (protocol is TextProtocol)
            {
                // ((TextProtocol) protocol).currentProg = this.currentProg;
                // optionally can be enabled, for testing purpose
            }
        }

        public virtual ProtocolListener Listener
        {
            set
            {
                this.session = value;
            }
        }

        /// <summary>
        /// Dispatches a control request to the transport layer (HTTP or WebSocket).
        /// <br/>
        /// NB All control/message requests which don't depend on the transport implementation must call this method.
        /// </summary>
        public abstract void sendControlRequest(LightstreamerRequest request, RequestTutor tutor, RequestListener reqListener);

        public virtual void handleReverseHeartbeat()
        {
            reverseHeartbeatTimer.onChangeInterval();
        }

        public virtual void sendForceRebind(ForceRebindRequest request, RequestTutor tutor)
        {
            RequestListener reqListener = new ControlRequestListenerAnonymousInnerClass(this, tutor);

            // force_rebind is always sent via HTTP
            httpRequestManager.addRequest(request, tutor, reqListener);
        }

        private class ControlRequestListenerAnonymousInnerClass : ControlRequestListener
        {
            private readonly TextProtocol outerInstance;

            public ControlRequestListenerAnonymousInnerClass(TextProtocol outerInstance, RequestTutor tutor) : base(outerInstance, tutor)
            {
                this.outerInstance = outerInstance;
            }

            public override void onOK()
            {
            }
            public override void onError(int code, string message)
            {
                tutor.discard();
                outerInstance.log.Error("force_rebind request caused the error: " + code + " " + message + " - The error will be silently ignored.");
            }
        }

        public virtual void sendDestroy(DestroyRequest request, RequestTutor tutor)
        {
            RequestListener reqListener = new ControlRequestListenerAnonymousInnerClass2(this, tutor);
            // destroy is always sent via HTTP
            // httpRequestManager.addRequest(request, tutor, reqListener);
            forwardDestroyRequest(request, tutor, reqListener);
        }

        protected abstract void forwardDestroyRequest(DestroyRequest request, RequestTutor tutor, RequestListener reqListener);
        private class ControlRequestListenerAnonymousInnerClass2 : ControlRequestListener
        {
            private readonly TextProtocol outerInstance;

            public ControlRequestListenerAnonymousInnerClass2(TextProtocol outerInstance, RequestTutor tutor) : base(outerInstance, tutor)
            {
                this.outerInstance = outerInstance;
            }

            public override void onOK()
            {
            }
            public override void onError(int code, string message)
            {
                outerInstance.log.Error("destroy request caused the error: " + code + " " + message + " - The error will be silently ignored.");
            }
        }

        public virtual void sendMessageRequest(MessageRequest request, RequestTutor tutor)
        {

            RequestListener reqListener = new ControlRequestListenerAnonymousInnerClass3(this, tutor, request);

            sendControlRequest(request, tutor, reqListener);
        }

        private class ControlRequestListenerAnonymousInnerClass3 : ControlRequestListener
        {
            private readonly TextProtocol outerInstance;

            private MessageRequest request;

            public ControlRequestListenerAnonymousInnerClass3(TextProtocol outerInstance, RequestTutor tutor, MessageRequest request) : base(outerInstance, tutor)
            {
                this.outerInstance = outerInstance;
                this.request = request;
            }


            public override void onOK()
            {
                if (request.needsAck())
                {
                    outerInstance.session.onMessageAck(request.Sequence, request.MessageNumber, ProtocolConstants.SYNC_RESPONSE);
                }
                else
                {
                    // unneeded acks are possible, for instance with HTTP transport
                }
            }

            public override void onError(int code, string message)
            {
                outerInstance.session.onMessageError(request.Sequence, code, message, request.MessageNumber, ProtocolConstants.SYNC_RESPONSE);
            }
        }

        public virtual void sendSubscriptionRequest(SubscribeRequest request, RequestTutor tutor)
        {
            if (log.IsDebugEnabled)
            {
                log.Debug("subscription parameters: " + request.TransportUnawareQueryString);
            }
            RequestListener reqListener = new ControlRequestListenerAnonymousInnerClass4(this, tutor, request);

            try
            {
                sendControlRequest(request, tutor, reqListener);
            }
            catch (Exception e)
            {
                log.Warn("Something wen wrong here: " + e.Message);
                if (log.IsDebugEnabled)
                {
                    log.Debug(" - " + e.StackTrace);
                }

            }
        }

        private class ControlRequestListenerAnonymousInnerClass4 : ControlRequestListener
        {
            private readonly TextProtocol outerInstance;

            private SubscribeRequest request;

            public ControlRequestListenerAnonymousInnerClass4(TextProtocol outerInstance, RequestTutor tutor, SubscribeRequest request) : base(outerInstance, tutor)
            {
                this.outerInstance = outerInstance;
                this.request = request;
            }


            public override void onOK()
            {
                outerInstance.session.onSubscriptionAck(request.SubscriptionId);
            }

            public override void onError(int code, string message)
            {
                outerInstance.session.onSubscriptionError(request.SubscriptionId, code, message, ProtocolConstants.SYNC_RESPONSE);
            }
        }

        public virtual void sendConfigurationRequest(ChangeSubscriptionRequest request, RequestTutor tutor)
        {

            RequestListener reqListener = new ControlRequestListenerAnonymousInnerClass5(this, tutor, request);

            sendControlRequest(request, tutor, reqListener);
        }

        private class ControlRequestListenerAnonymousInnerClass5 : ControlRequestListener
        {
            private readonly TextProtocol outerInstance;

            private ChangeSubscriptionRequest request;

            public ControlRequestListenerAnonymousInnerClass5(TextProtocol outerInstance, RequestTutor tutor, ChangeSubscriptionRequest request) : base(outerInstance, tutor)
            {
                this.outerInstance = outerInstance;
                this.request = request;
            }


            public override void onOK()
            {
                outerInstance.session.onSubscriptionReconf(request.SubscriptionId, request.ReconfId, ProtocolConstants.SYNC_RESPONSE);
            }

            public override void onError(int code, string message)
            {
                tutor.discard();
                outerInstance.log.Error("configuration request [" + request.TransportUnawareQueryString + "] caused the error: " + code + " " + message + " - The request will be retransmitted.");
            }
        }

        public virtual void sendUnsubscriptionRequest(UnsubscribeRequest request, RequestTutor tutor)
        {

            RequestListener reqListener = new ControlRequestListenerAnonymousInnerClass6(this, tutor, request);

            sendControlRequest(request, tutor, reqListener);
        }

        private class ControlRequestListenerAnonymousInnerClass6 : ControlRequestListener
        {
            private readonly TextProtocol outerInstance;

            private UnsubscribeRequest request;

            public ControlRequestListenerAnonymousInnerClass6(TextProtocol outerInstance, RequestTutor tutor, UnsubscribeRequest request) : base(outerInstance, tutor)
            {
                this.outerInstance = outerInstance;
                this.request = request;
            }


            public override void onOK()
            {
                outerInstance.session.onUnsubscriptionAck(request.SubscriptionId);
            }

            public override void onError(int code, string message)
            {
                tutor.discard();
                outerInstance.log.Error("unsubscription request [" + request.TransportUnawareQueryString + "] caused the error: " + code + " " + message);
            }
        }

        public virtual void sendConstrainRequest(ConstrainRequest request, ConstrainTutor tutor)
        {
            RequestListener reqListener = new ControlRequestListenerAnonymousInnerClass7(this, tutor, request);
            sendControlRequest(request, tutor, reqListener);
        }

        private class ControlRequestListenerAnonymousInnerClass7 : ControlRequestListener
        {
            private readonly TextProtocol outerInstance;

            private new readonly ConstrainTutor tutor;

            private ConstrainRequest request;

            public ControlRequestListenerAnonymousInnerClass7(TextProtocol outerInstance, ConstrainTutor tutor, ConstrainRequest request) : base(outerInstance, tutor)
            {
                this.outerInstance = outerInstance;
                this.tutor = tutor;
                this.request = request;
            }

            public override void onOK()
            {
                outerInstance.session.onConstrainResponse((ConstrainTutor)tutor);
            }

            public override void onError(int code, string message)
            {
                outerInstance.log.Error("constrain request [" + request.TransportUnawareQueryString + "] caused the error: " + code + " " + message);
                // bandwidth requests should not generate REQERR/ERROR
                // anyway we stop retransmissions
                outerInstance.session.onConstrainResponse((ConstrainTutor)tutor);
            }
        }

        public virtual void sendReverseHeartbeat(ReverseHeartbeatRequest request, RequestTutor tutor)
        {
            sendControlRequest(request, tutor, new BaseControlRequestListenerAnonymousInnerClass(this, tutor));
        }

        private class BaseControlRequestListenerAnonymousInnerClass : BaseControlRequestListener
        {
            private readonly TextProtocol outerInstance;

            public BaseControlRequestListenerAnonymousInnerClass(TextProtocol outerInstance, RequestTutor tutor) : base(outerInstance, tutor)
            {
                this.outerInstance = outerInstance;

                /*
                 * NB
                 * as a little optimization to avoid unnecessary rescheduling of heartbeat messages
                 * don't wait for onOpen() to call setLastSentTime() as in the other control request listeners
                 * (see ControlRequestListener) but call it immediately during the initialization
                 */
                outerInstance.reverseHeartbeatTimer.onControlRequest();
            }


            public override void onOK()
            {
                /* heartbeat doesn't care for REQOK */
            }

            public override void onError(int code, string message)
            {
                /* heartbeat doesn't care for REQERR */
            }
        }

        public virtual void sendCreateRequest(CreateSessionRequest request)
        {
            this.activeListener = new OpenSessionListener(this);

            long connectDelay = request.Delay;
            long readDelay = request.Delay;
            if (request.Polling)
            {
                readDelay += this.options.IdleTimeout;
                connectDelay += this.options.PollingInterval;

            }

            // create_session is always sent over HTTP
            this.activeConnection = httpRequestManager.createSession(request, activeListener, options.TCPConnectTimeout + connectDelay, options.TCPReadTimeout + readDelay);

            this.Status = StreamStatus.OPENING_STREAM;
        }

        public virtual ListenableFuture sendBindRequest(BindSessionRequest request)
        {
            this.activeListener = new BindSessionListener(this);

            long connectDelay = request.Delay;
            long readDelay = request.Delay;

            if (request.Polling)
            {
                readDelay += this.options.IdleTimeout;
                connectDelay += this.options.PollingInterval;

            }

            ListenableFuture bindFuture = new ListenableFuture();

            this.activeConnection = RequestManager.bindSession(request, activeListener, options.TCPConnectTimeout + connectDelay, options.TCPReadTimeout + readDelay, bindFuture);

            this.Status = StreamStatus.OPENING_STREAM;
            return bindFuture;
        }

        public virtual void sendRecoveryRequest(RecoverSessionRequest request)
        {
            this.activeListener = new OpenSessionListener(this);

            long connectDelay = request.Delay;
            long readDelay = request.Delay;
            if (request.Polling)
            {
                readDelay += this.options.IdleTimeout;
                connectDelay += this.options.PollingInterval;
            }

            // recovery is always sent over HTTP
            this.activeConnection = httpRequestManager.recoverSession(request, activeListener, options.TCPConnectTimeout + connectDelay, options.TCPReadTimeout + readDelay);

            this.Status = StreamStatus.OPENING_STREAM;
        }

        /// <summary>
        /// Pattern of a subscription message.
        /// This message has the following form
        /// {@code SUBOK,<table>,<total items>,<total fields>}. 
        /// </summary>
        public static readonly Regex SUBOK_REGEX = new Regex("SUBOK,(\\d+),(\\d+),(\\d+)");
        /// <summary>
        /// Pattern of a command-mode subscription message.
        /// This message has the following form
        /// {@code SUBCMD,<table>,<total items>,<total fields>,<key field>,<command field>}.
        /// </summary>
        public static readonly Regex SUBCMD_REGEX = new Regex("SUBCMD,(\\d+),(\\d+),(\\d+),(\\d+),(\\d+)");
        /// <summary>
        /// Pattern of an unsubscription message.
        /// This message has the form {@code UNSUB,<table>}.
        /// </summary>
        public static readonly Regex UNSUBSCRIBE_REGEX = new Regex("UNSUB,(\\d+)");
        /// <summary>
        /// Pattern of a message to change the server bandwidth.
        /// This message has the form {@code CONS,<bandwidth>}. 
        /// </summary>
        public static readonly Regex CONSTRAIN_REGEX = new Regex("CONS,(unmanaged|unlimited|(\\d+(?:\\.\\d+)?))");
        /// <summary>
        /// Pattern of a synchronization message.
        /// This message has the form {@code SYNC,<seconds>}.
        /// </summary>
        public static readonly Regex SYNC_REGEX = new Regex("SYNC,(\\d+)");
        /// <summary>
        /// Pattern of a clear-snapshot message.
        /// This message has the form {@code CS,<table>,<item>}.
        /// </summary>
        public static readonly Regex CLEAR_SNAPSHOT_REGEX = new Regex("CS,(\\d+),(\\d+)");
        /// <summary>
        /// Pattern of a end-of-snapshot message.
        /// This message has the form {@code EOS,<table>,<item>}.
        /// </summary>
        public static readonly Regex END_OF_SNAPSHOT_REGEX = new Regex("EOS,(\\d+),(\\d+)");
        /// <summary>
        /// Pattern of an overflow message.
        /// This message has the form {@code OV,<table>,<item>,<lost updates>}.
        /// </summary>
        public static readonly Regex OVERFLOW_REGEX = new Regex("OV,(\\d+),(\\d+),(\\d+)");
        /// <summary>
        /// Pattern of a configuration message.
        /// This message has the form {@code CONF,<table>,<frequency>,("filtered"|"unfiltered")}.
        /// </summary>
        public static readonly Regex CONFIGURATION_REGEX = new Regex("CONF,(\\d+),(unlimited|(\\d+(?:\\.\\d+)?)),(filtered|unfiltered)");
        /// <summary>
        /// Pattern of a server-name message.
        /// This message has the form {@code SERVNAMR,<server name>}.
        /// </summary>
        public static readonly Regex SERVNAME_REGEX = new Regex("SERVNAME,(.+)");
        /// <summary>
        /// Pattern of a client-ip message.
        /// This message has the form {@code CLIENTIP,<client ip>}.
        /// </summary>
        public static readonly Regex CLIENTIP_REGEX = new Regex("CLIENTIP,(.+)");
        /// <summary>
        /// Pattern of a current-progressive message.
        /// This message has the form {@code PROG,<number>}.
        /// </summary>
        public static readonly Regex PROG_REGEX = new Regex("PROG,(\\d+)");

        /// <summary>
        /// CONOK message has the form {@literal CONOK,<session id>,<request limit>,<keep alive>,<control link>}.
        /// </summary>
        public static readonly Regex CONOK_REGEX = new Regex("CONOK,([^,]+),(\\d+),(\\d+),([^,]+)");
        /// <summary>
        /// CONERR message has the form {@literal CONERR,<error code>,<error message>}.
        /// </summary>
        public static readonly Regex CONERR_REGEX = new Regex("CONERR,([-]?\\d+),(.*)");
        /// <summary>
        /// END message has the form {@literal END,<error code>,<error message>}.
        /// </summary>
        public static readonly Regex END_REGEX = new Regex("END,([-]?\\d+),(.*)");
        /// <summary>
        /// LOOP message has the form {@literal LOOP,<holding time>}.
        /// </summary>
        public static readonly Regex LOOP_REGEX = new Regex("LOOP,(\\d+)");

        internal virtual void onProtocolMessage(string message)
        {
            if (log.IsDebugEnabled)
            {
                log.Debug("New message (" + objectId + " - " + status + "): " + message);
            }

            switch (status)
            {
                case com.lightstreamer.client.protocol.TextProtocol.StreamStatus.READING_STREAM:
                    if (message.StartsWith(ProtocolConstants.reqokMarker, StringComparison.Ordinal))
                    {
                        processREQOK(message);

                    }
                    else if (message.StartsWith(ProtocolConstants.reqerrMarker, StringComparison.Ordinal))
                    {
                        processREQERR(message);

                    }
                    else if (message.StartsWith(ProtocolConstants.errorMarker, StringComparison.Ordinal))
                    {
                        processERROR(message);

                    }
                    else if (message.StartsWith(ProtocolConstants.updateMarker, StringComparison.Ordinal))
                    {
                        processUpdate(message);

                    }
                    else if (message.StartsWith(ProtocolConstants.msgMarker, StringComparison.Ordinal))
                    {
                        processUserMessage(message);

                    }
                    else if (message.StartsWith(ProtocolConstants.probeCommand, StringComparison.Ordinal))
                    {
                        session.onKeepalive();

                    }
                    else if (message.StartsWith(ProtocolConstants.loopCommand, StringComparison.Ordinal))
                    {
                        Status = StreamStatus.NO_STREAM; // NB status must be changed before processLOOP is called
                        processLOOP(message);

                    }
                    else if (message.StartsWith(ProtocolConstants.endCommand, StringComparison.Ordinal))
                    {
                        processEND(message);
                        Status = StreamStatus.STREAM_CLOSED;

                    }
                    else if (message.StartsWith(ProtocolConstants.subscribeMarker, StringComparison.Ordinal))
                    {
                        processSUBOK(message);

                    }
                    else if (message.StartsWith(ProtocolConstants.unsubscribeMarker, StringComparison.Ordinal))
                    {
                        processUNSUB(message);

                    }
                    else if (message.StartsWith(ProtocolConstants.constrainMarker, StringComparison.Ordinal))
                    {
                        processCONS(message);
                    }
                    else if (message.StartsWith(ProtocolConstants.syncMarker, StringComparison.Ordinal))
                    {
                        processSYNC(message);

                    }
                    else if (message.StartsWith(ProtocolConstants.clearSnapshotMarker, StringComparison.Ordinal))
                    {
                        processCS(message);

                    }
                    else if (message.StartsWith(ProtocolConstants.endOfSnapshotMarker, StringComparison.Ordinal))
                    {
                        processEOS(message);

                    }
                    else if (message.StartsWith(ProtocolConstants.overflowMarker, StringComparison.Ordinal))
                    {
                        processOV(message);

                    }
                    else if (message.StartsWith(ProtocolConstants.configurationMarker, StringComparison.Ordinal))
                    {
                        processCONF(message);

                    }
                    else if (message.StartsWith(ProtocolConstants.serverNameMarker, StringComparison.Ordinal))
                    {
                        processSERVNAME(message);

                    }
                    else if (message.StartsWith(ProtocolConstants.clientIpMarker, StringComparison.Ordinal))
                    {
                        processCLIENTIP(message);

                    }
                    else if (message.StartsWith(ProtocolConstants.mpnRegisterMarker, StringComparison.Ordinal))
                    {
                        processMPNREG(message);

                    }
                    else if (message.StartsWith(ProtocolConstants.mpnSubscribeMarker, StringComparison.Ordinal))
                    {
                        processMPNOK(message);

                    }
                    else if (message.StartsWith(ProtocolConstants.mpnUnsubscribeMarker, StringComparison.Ordinal))
                    {
                        processMPNDEL(message);

                    }
                    else if (message.StartsWith(ProtocolConstants.mpnResetBadgeMarker, StringComparison.Ordinal))
                    {
                        processMPNZERO(message);

                    }
                    else if (message.StartsWith(ProtocolConstants.progMarker, StringComparison.Ordinal))
                    {
                        processPROG(message);

                    }
                    else if (message.StartsWith(ProtocolConstants.noopMarker, StringComparison.Ordinal))
                    {
                        // skip

                    }
                    else
                    {
                        onIllegalMessage("Unexpected message in state " + status + ": " + message);
                    }
                    break;

                case com.lightstreamer.client.protocol.TextProtocol.StreamStatus.OPENING_STREAM:
                    if (message.StartsWith(ProtocolConstants.reqokMarker, StringComparison.Ordinal))
                    {
                        processREQOK(message);

                    }
                    else if (message.StartsWith(ProtocolConstants.reqerrMarker, StringComparison.Ordinal))
                    {
                        processREQERR(message);

                    }
                    else if (message.StartsWith(ProtocolConstants.errorMarker, StringComparison.Ordinal))
                    {
                        processERROR(message);

                    }
                    else if (message.StartsWith(ProtocolConstants.conokCommand, StringComparison.Ordinal))
                    {
                        processCONOK(message);
                        Status = StreamStatus.READING_STREAM;
                    }
                    else if (message.StartsWith(ProtocolConstants.conerrCommand, StringComparison.Ordinal))
                    {
                        processCONERR(message);
                        Status = StreamStatus.STREAM_CLOSED;

                    }
                    else if (message.StartsWith(ProtocolConstants.endCommand, StringComparison.Ordinal))
                    {
                        processEND(message);
                        Status = StreamStatus.STREAM_CLOSED;

                    }
                    else
                    {

                        log.Debug("Unexpected message in state " + status + ": " + message);

                        // onIllegalMessage("Unexpected message in state " + status + ": " + message);
                    }
                    break;

                default:
                    Debug.Assert(status.Equals(StreamStatus.STREAM_CLOSED));
                    //          onIllegalMessage("Unexpected message in state " + status + ": " + message);
                    break;
            }
        }

        protected internal MatchCollection matchLine(Regex pattern, string message)
        {
            MatchCollection matcher = pattern.Matches(message);
            if (matcher.Count < 1)
            {
                onIllegalMessage("Malformed message received: " + message);
            }
            return matcher;
        }

        protected internal int myParseInt(string field, string description, string orig)
        {
            try
            {
                return Int32.Parse(field, NumberStyles.Number);
            }
            catch (System.FormatException fe)
            {

                log.Debug("myParseInt failure ... " + field + " - " + fe.Message);

                onIllegalMessage("Malformed " + description + " in message: " + orig);
                return 0; // but onIllegalMessage only throws
            }
        }

        protected internal long myParseLong(string field, string description, string orig)
        {
            try
            {
                return long.Parse(field);
            }
            catch (System.FormatException)
            {
                onIllegalMessage("Malformed " + description + " in message: " + orig);
                return 0; // but onIllegalMessage only throws
            }
            catch (System.OverflowException)
            {
                onIllegalMessage("Overflow " + description + " in message: " + orig);
                return 0; // but onIllegalMessage only throws
            }
            catch (Exception)
            {
                onIllegalMessage("Overflow " + description + " in message: " + orig);
                return 0; // but onIllegalMessage only throws
            }
        }

        protected internal double myParseDouble(string field, string description, string orig)
        {
            try
            {
                return double.Parse(field, CultureInfo.InvariantCulture);
            }
            catch (System.FormatException)
            {

                log.Error("Malformed double for " + field + ", " + description);

                onIllegalMessage("Malformed " + description + " in message: " + orig);
                return 0; // but onIllegalMessage only throws
            }
        }

        /// <summary>
        /// Processes a REQOK message received on the stream connection.
        /// It only matters for WebSocket transport, because in HTTP this message is sent over a control connection.
        /// </summary>
        public abstract void processREQOK(string message);

        /// <summary>
        /// Processes a REQERR message received on the stream connection.
        /// It only matters for WebSocket transport, because in HTTP this message is sent over a control connection.
        /// </summary>
        public abstract void processREQERR(string message);

        /// <summary>
        /// Processes a ERROR message received on the stream connection.
        /// It only matters for WebSocket transport, because in HTTP this message is sent over a control connection.
        /// </summary>
        public abstract void processERROR(string message);

        private void processCLIENTIP(string message)
        {
            MatchCollection matcher = matchLine(CLIENTIP_REGEX, message);

            Match match = matcher[0];
            GroupCollection groupCollection = match.Groups;
            string clientIp = groupCollection[1].ToString();
            session.onClientIp(clientIp);
        }

        private void processSERVNAME(string message)
        {
            MatchCollection matcher = matchLine(SERVNAME_REGEX, message);

            Match match = matcher[0];
            GroupCollection groupCollection = match.Groups;
            string serverName = EncodingUtils.unquote(groupCollection[1].ToString());
            session.onServerName(serverName);
        }

        private void processPROG(string message)
        {
            MatchCollection matcher = matchLine(PROG_REGEX, message);

            Match match = matcher[0];
            GroupCollection groupCollection = match.Groups;
            long prog = myParseLong(groupCollection[1].ToString(), "prog", message);
            if (currentProg == null)
            {
                currentProg = prog;
                long sessionProg = session.DataNotificationProg;
                if (currentProg.Value > sessionProg)
                {
                    onIllegalMessage("Message prog higher than expected. Expected: " + sessionProg + " but found: " + currentProg);
                }
            }
            else
            {
                // not allowed by the protocol, but we handle the case for testing scenarios;
                // these extra invocations of PROG can be enabled on the Server
                // through the <PROG_NOTIFICATION_GAP> private flag
                if (currentProg.Value != prog)
                {
                    onIllegalMessage("Message prog different than expected: " + message + " <> " + currentProg.Value);
                }
                if (prog != session.DataNotificationProg)
                {
                    onIllegalMessage("Session prog different than expected: " + prog);
                }
            }
        }

        private void processCONF(string message)
        {
            MatchCollection matcher = matchLine(CONFIGURATION_REGEX, message);

            Match match = matcher[0];
            GroupCollection groupCollection = match.Groups;
            if (!processCountableNotification())
            {
                return;
            }
            int table = myParseInt(groupCollection[1].ToString(), "subscription", message);
            if (( groupCollection[3].ToString() != null ) && !( groupCollection[3].ToString().Equals("") ))
            {
                string frequency = groupCollection[3].ToString();
                myParseDouble(frequency, "frequency", message); // preliminary check
                session.onConfigurationEvent(table, frequency);
            }
            else
            {
                Debug.Assert(groupCollection[2].ToString().Equals("unlimited")); // ensured by the regexp check
                session.onConfigurationEvent(table, "unlimited");
            }
            // assert matcher.group(4) corresponds to the filtered/unfiltered flag of the table
        }

        private void processEND(string message)
        {
            MatchCollection matcher = matchLine(END_REGEX, message);

            Match match = matcher[0];
            GroupCollection groupCollection = match.Groups;
            int errorCode = myParseInt(groupCollection[1].ToString(), "error code", message);
            string errorMessage = EncodingUtils.unquote(groupCollection[2].ToString());
            forwardError(errorCode, errorMessage);
        }

        private void processLOOP(string message)
        {
            MatchCollection matcher = matchLine(LOOP_REGEX, message);

            Match match = matcher[0];
            GroupCollection groupCollection = match.Groups;
            int millis = myParseInt(groupCollection[1].ToString(), "holding time", message);
            session.onLoopReceived(millis);
        }

        private void processOV(string message)
        {
            MatchCollection matcher = matchLine(OVERFLOW_REGEX, message);

            Match match = matcher[0];
            GroupCollection groupCollection = match.Groups;
            if (!processCountableNotification())
            {
                return;
            }
            int table = myParseInt(groupCollection[1].ToString(), "subscription", message);
            int item = myParseInt(groupCollection[2].ToString(), "item", message);
            int overflow = myParseInt(groupCollection[3].ToString(), "count", message);
            session.onLostUpdatesEvent(table, item, overflow);
        }

        private void processEOS(string message)
        {
            MatchCollection matcher = matchLine(END_OF_SNAPSHOT_REGEX, message);

            Match match = matcher[0];
            GroupCollection groupCollection = match.Groups;
            if (!processCountableNotification())
            {
                return;
            }
            int table = myParseInt(groupCollection[1].ToString(), "subscription", message);
            int item = myParseInt(groupCollection[2].ToString(), "item", message);
            session.onEndOfSnapshotEvent(table, item);
        }

        private void processCS(string message)
        {
            MatchCollection matcher = matchLine(CLEAR_SNAPSHOT_REGEX, message);

            Match match = matcher[0];
            GroupCollection groupCollection = match.Groups;
            if (!processCountableNotification())
            {
                return;
            }
            int table = myParseInt(groupCollection[1].ToString(), "subscription", message);
            int item = myParseInt(groupCollection[2].ToString(), "item", message);
            session.onClearSnapshotEvent(table, item);
        }

        private void processSYNC(string message)
        {
            MatchCollection matcher = matchLine(SYNC_REGEX, message);

            Match match = matcher[0];
            GroupCollection groupCollection = match.Groups;
            long seconds = myParseLong(groupCollection[1].ToString(), "prog", message);
            session.onSyncMessage(seconds);
        }

        private void processCONS(string message)
        {
            string resultbw = ".";
            MatchCollection matcher = matchLine(CONSTRAIN_REGEX, message);

            try
            {
                Match match = matcher[0];
                GroupCollection groupCollection = match.Groups;

                if (( groupCollection[2].ToString() != null ) && !( groupCollection[2].ToString().Equals("") ))
                {
                    string bandwidth = groupCollection[2].ToString();

                    myParseDouble(bandwidth, "bandwidth", message); // preliminary check
                    session.onServerSentBandwidth(bandwidth);

                    resultbw = bandwidth;
                }
                else
                {
                    string bwType = groupCollection[1].ToString();
                    Debug.Assert(bwType.Equals("unmanaged", StringComparison.OrdinalIgnoreCase) || bwType.Equals("unlimited", StringComparison.OrdinalIgnoreCase)); // ensured by the regexp check
                    session.onServerSentBandwidth(bwType);

                    resultbw = bwType;
                }
            }
            catch (Exception e)
            {
                log.Warn("Something went wrong: " + e.Message);
            }
        }

        private void processUNSUB(string message)
        {
            MatchCollection matcher = matchLine(UNSUBSCRIBE_REGEX, message);

            Match match = matcher[0];
            GroupCollection groupCollection = match.Groups;
            if (!processCountableNotification())
            {
                return;
            }
            int table = myParseInt(groupCollection[1].ToString(), "subscription", message);
            session.onUnsubscription(table);
        }

        private void processSUBOK(string message)
        {
            if (!processCountableNotification())
            {
                return;
            }
            if (message.StartsWith("SUBOK", StringComparison.Ordinal))
            {
                MatchCollection matcher = matchLine(SUBOK_REGEX, message);

                Match match = matcher[0];
                GroupCollection groupCollection = match.Groups;
                int table = myParseInt(groupCollection[1].ToString(), "subscription", message);
                int totalItems = myParseInt(groupCollection[2].ToString(), "item count", message);
                int totalFields = myParseInt(groupCollection[3].ToString(), "field count", message);
                session.onSubscription(table, totalItems, totalFields, -1, -1);

            }
            else if (message.StartsWith("SUBCMD", StringComparison.Ordinal))
            {
                MatchCollection matcher = matchLine(SUBCMD_REGEX, message);

                Match match = matcher[0];
                GroupCollection groupCollection = match.Groups;
                int table = myParseInt(groupCollection[1].ToString(), "subscription", message);
                int totalItems = myParseInt(groupCollection[2].ToString(), "item count", message);
                int totalFields = myParseInt(groupCollection[3].ToString(), "field count", message);
                int key = myParseInt(groupCollection[4].ToString(), "key position", message);
                int command = myParseInt(groupCollection[5].ToString(), "command position", message);
                session.onSubscription(table, totalItems, totalFields, key, command);

            }
            else
            {
                onIllegalMessage("Malformed message received: " + message);
            }
        }

        private void processUserMessage(string message)
        {
            // a message notification can have the following forms:
            // 1) MSGDONE,<sequence>,<prog>
            // 2) MSGFAIL,<sequence>,<prog>,<error-code>,<error-message>

            string[] splitted = message.Split(',');

            log.Debug("Process User Message: " + message);

            if (splitted.Length == 3)
            {
                if (!splitted[0].Equals("MSGDONE"))
                {
                    onIllegalMessage("MSGDONE expected: " + message);
                }
                if (!processCountableNotification())
                {
                    return;
                }
                string sequence = splitted[1];
                if (sequence.Equals("*"))
                {
                    sequence = Constants.UNORDERED_MESSAGES;
                }

                log.Debug("Process User Message (2): " + splitted[0] + ", " + splitted[1] + ", " + splitted[2]);

                int messageNumber = myParseInt(splitted[2], "prog", message);
                session.onMessageOk(sequence, messageNumber);

            }
            else if (splitted.Length == 5)
            {
                if (!splitted[0].Equals("MSGFAIL"))
                {
                    onIllegalMessage("MSGFAIL expected: " + message);
                }
                if (!processCountableNotification())
                {
                    return;
                }
                string sequence = splitted[1];
                if (sequence.Equals("*"))
                {
                    sequence = Constants.UNORDERED_MESSAGES;
                }
                int messageNumber = myParseInt(splitted[2], "prog", message);
                int errorCode = myParseInt(splitted[3], "error code", message);
                string errorMessage = EncodingUtils.unquote(splitted[4]);
                onMsgErrorMessage(sequence, messageNumber, errorCode, errorMessage, message);
            }
            else
            {
                onIllegalMessage("Wrong number of fields in message: " + message);
            }
        }

        private void processUpdate(string message)
        {
            // update message has the form U,<table>,<item>|<field1>|...|<fieldN>
            // or U,<table>,<item>,<field1>|^<number of unchanged fields>|...|<fieldN>
            try
            {
                /* parse table and item */
                int tableIndex = message.IndexOf(',') + 1;

                log.Debug("Process update, Table Index: " + tableIndex);

                Debug.Assert(tableIndex == 2); // tested by the caller
                int itemIndex = message.IndexOf(',', tableIndex) + 1;
                if (itemIndex <= 0)
                {
                    onIllegalMessage("Missing subscription field in message: " + message);
                }
                int fieldsIndex = message.IndexOf(',', itemIndex) + 1;
                if (fieldsIndex <= 0)
                {
                    onIllegalMessage("Missing item field in message: " + message);
                }
                Debug.Assert(message.Substring(0, tableIndex).Equals("U,")); // tested by the caller
                int table = myParseInt(message.Substring(tableIndex, ( itemIndex - 1 ) - tableIndex), "subscription", message);
                int item = myParseInt(message.Substring(itemIndex, ( fieldsIndex - 1 ) - itemIndex), "item", message);

                if (!processCountableNotification())
                {
                    return;
                }


                log.Debug("Process update -- Table N. " + table);

                /* parse fields */
                List<string> values = new List<string>();
                int fieldStart = fieldsIndex - 1; // index of the separator introducing the next field
                Debug.Assert(message[fieldStart] == ','); // tested above
                while (fieldStart < message.Length)
                {

                    int fieldEnd = message.IndexOf('|', fieldStart + 1);
                    if (fieldEnd == -1)
                    {
                        fieldEnd = message.Length;
                    }
                    /*
                      Decoding algorithm:
                          1) Set a pointer to the first field of the schema.
                          2) Look for the next pipe "|" from left to right and take the substring to it, or to the end of the line if no pipe is there.
                          3) Evaluate the substring:
                                 A) If its value is empty, the pointed field should be left unchanged and the pointer moved to the next field.
                                 B) Otherwise, if its value corresponds to a single "#" (UTF-8 code 0x23), the pointed field should be set to a null value and the pointer moved to the next field.
                                 C) Otherwise, If its value corresponds to a single "$" (UTF-8 code 0x24), the pointed field should be set to an empty value ("") and the pointer moved to the next field.
                                 D) Otherwise, if its value begins with a caret "^" (UTF-8 code 0x5E):
                                         - take the substring following the caret and convert it to an integer number;
                                         - for the corresponding count, leave the fields unchanged and move the pointer forward;
                                         - e.g. if the value is "^3", leave unchanged the pointed field and the following two fields, and move the pointer 3 fields forward;
                                 E) Otherwise, the value is an actual content: decode any percent-encoding and set the pointed field to the decoded value, then move the pointer to the next field.
                                    Note: "#", "$" and "^" characters are percent-encoded if occurring at the beginning of an actual content.
                          4) Return to the second step, unless there are no more fields in the schema.
                     */
                    string value = message.Substring(fieldStart + 1, fieldEnd - ( fieldStart + 1 ));
                    if (value.Length == 0)
                    { // step A
                        values.Add(ProtocolConstants.UNCHANGED);

                    }
                    else if (value[0] == '#')
                    { // step B
                        if (value.Length != 1)
                        {
                            onIllegalMessage("Wrong field quoting in message: " + message);
                        } // a # followed by other text should have been quoted
                        values.Add(null);

                    }
                    else if (value[0] == '$')
                    { // step C
                        if (value.Length != 1)
                        {
                            onIllegalMessage("Wrong field quoting in message: " + message);
                        } // a $ followed by other text should have been quoted
                        values.Add("");

                    }
                    else if (value[0] == '^')
                    { // step D
                        int count = myParseInt(value.Substring(1), "compression", message);
                        while (count-- > 0)
                        {
                            values.Add(ProtocolConstants.UNCHANGED);
                        }

                    }
                    else
                    { // step E
                        string unquoted = EncodingUtils.unquote(value);
                        values.Add(unquoted);

                        log.Debug("Values: " + unquoted);
                    }
                    fieldStart = fieldEnd;
                }

                log.Debug("Process update --- Item N. " + item);

                /* notify listener */
                session.onUpdateReceived(table, item, values);
            }
            catch (Exception e)
            {
                log.Warn("Error while processing update - " + e.Message);
                log.Warn("Error while processing update -- : " + e.StackTrace);
            }
        }

        private void processCONERR(string message)
        {
            MatchCollection matcher = matchLine(CONERR_REGEX, message);

            Match match = matcher[0];
            GroupCollection groupCollection = match.Groups;
            int errorCode = myParseInt(groupCollection[1].ToString(), "error code", message);
            string errorMessage = EncodingUtils.unquote(groupCollection[2].ToString());
            this.forwardError(errorCode, errorMessage);
        }

        private void processCONOK(string message)
        {
            MatchCollection matcher = matchLine(CONOK_REGEX, message);
            Match match = matcher[0];
            GroupCollection groupCollection = match.Groups;
            // process session id
            string sessionId = groupCollection[1].ToString();

            // process request limit
            long requestLimitLength = myParseLong(groupCollection[2].ToString(), "request limit", message);

            RequestManager.RequestLimit = requestLimitLength;

            // process keep alive
            long keepaliveIntervalDefault = myParseLong(groupCollection[3].ToString(), "keepalive time", message);
            string controlLink;

            // process control link (when unknown, server sends *)
            if (groupCollection[4].ToString().Equals("*"))
            {
                controlLink = null;
            }
            else
            {
                controlLink = EncodingUtils.unquote(groupCollection[4].ToString());
            }

            // notify listeners

            session.onOKReceived(sessionId, controlLink, requestLimitLength, keepaliveIntervalDefault);
        }

        private void processMPNREG(string message)
        {
            if (!processCountableNotification())
            {
                return;
            }
            // MPNREG,<device-id>,<mpn-adapter-name>
            int firstComma = message.IndexOf(',');
            if (firstComma == -1)
            {
                onIllegalMessage(message);
            }
            int secondComma = message.IndexOf(',', firstComma + 1);
            if (secondComma == -1)
            {
                onIllegalMessage(message);
            }
            string deviceId = message.Substring(firstComma + 1, secondComma - ( firstComma + 1 ));
            if (deviceId.Length == 0)
            {
                onIllegalMessage(message);
            }
            string adapterName = message.Substring(secondComma + 1);
            if (adapterName.Length == 0)
            {
                onIllegalMessage(message);
            }
            session.onMpnRegisterOK(deviceId, adapterName);
        }

        private void processMPNOK(string message)
        {
            if (!processCountableNotification())
            {
                return;
            }
            // MPNOK,<subscription-id>,<pn-subscription-id>
            int firstComma = message.IndexOf(',');
            if (firstComma == -1)
            {
                onIllegalMessage(message);
            }
            int secondComma = message.IndexOf(',', firstComma + 1);
            if (secondComma == -1)
            {
                onIllegalMessage(message);
            }
            string lsSubId = message.Substring(firstComma + 1, secondComma - ( firstComma + 1 ));
            if (lsSubId.Length == 0)
            {
                onIllegalMessage(message);
            }
            string pnSubId = message.Substring(secondComma + 1);
            if (pnSubId.Length == 0)
            {
                onIllegalMessage(message);
            }
            session.onMpnSubscribeOK(lsSubId, pnSubId);
        }

        private void processMPNDEL(string message)
        {
            if (!processCountableNotification())
            {
                return;
            }
            // MPNDEL,<subscription-id>
            int firstComma = message.IndexOf(',');
            if (firstComma == -1)
            {
                onIllegalMessage(message);
            }
            string subId = message.Substring(firstComma + 1);
            if (subId.Length == 0)
            {
                onIllegalMessage(message);
            }
            session.onMpnUnsubscribeOK(subId);
        }

        private void processMPNZERO(string message)
        {
            if (!processCountableNotification())
            {
                return;
            }
            // MPNZERO,<device-id>
            int firstComma = message.IndexOf(',');
            if (firstComma == -1)
            {
                onIllegalMessage(message);
            }
            string deviceId = message.Substring(firstComma + 1);
            if (deviceId.Length == 0)
            {
                onIllegalMessage(message);
            }
            session.onMpnResetBadgeOK(deviceId);
        }

        internal virtual void onMsgErrorMessage(string sequence, int messageNumber, int errorCode, string errorMessage, string orig)
        {
            if (errorCode == 39)
            { // code 39: list of discarded messages, the message is actually a counter
                int count = myParseInt(errorMessage, "number of messages", orig);
                for (int i = messageNumber - count + 1; i <= messageNumber; i++)
                {
                    session.onMessageDiscarded(sequence, i, ProtocolConstants.ASYNC_RESPONSE);
                }

            }
            else if (errorCode == 38)
            {
                //just discarded
                session.onMessageDiscarded(sequence, messageNumber, ProtocolConstants.ASYNC_RESPONSE);
            }
            else if (errorCode <= 0)
            {
                // Metadata Adapter has refused the message
                session.onMessageDeny(sequence, errorCode, errorMessage, messageNumber, ProtocolConstants.ASYNC_RESPONSE);
            }
            else
            {
                // 32 / 33 The specified progressive number is too low
                // 34 NotificationException from metadata 
                // 35 unexpected processing error
                // 68 Internal server error
                session.onMessageError(sequence, errorCode, errorMessage, messageNumber, ProtocolConstants.ASYNC_RESPONSE);

            }
        }

        /// <summary>
        /// Checks if a data notification can be forwarded to session.
        /// In fact, in case of recovery, the initial notifications may be redundant.
        /// </summary>
        private bool processCountableNotification()
        {
            if (currentProg != null)
            {
                long sessionProg = session.DataNotificationProg;
                // assert(currentProg.Value <= sessionProg); // ensured since processPROG
                currentProg++;
                if (currentProg.Value <= sessionProg)
                {
                    // already seen: to be skipped
                    return false;
                }
                else
                {
                    session.onDataNotification();
                    sessionProg = session.DataNotificationProg;
                    // assert(currentProg.Value == sessionProg);
                    return true;
                }
            }
            else
            {
                session.onDataNotification();
                return true;
            }
        }

        /// <summary>
        /// Manages CONERR errors.
        /// </summary>
        protected internal virtual void forwardError(int code, string message)
        {
            if (code == 41)
            {
                session.onTakeover(code);
            }
            else if (code == 40)
            {
                // manual or spurious rebind: let's recover
                session.onTakeover(code);
            }
            else if (code == 48)
            {
                session.onExpiry();
            }
            else if (code == 20)
            {
                // Answer sent by the Server to signal that a control or rebind
                // request has been refused because the indicated session has not
                // been found.
                session.onSyncError(ProtocolConstants.ASYNC_RESPONSE);
            }
            else if (code == 4)
            {
                session.onRecoveryError();
            }
            else
            {
                /*
                 * fall-back case handles fatal errors: 
                 * close current session, don't create a new session, notify client listeners
                 */

                log.Debug("On Server Error - 1 - " + code + " - " + message);

                session.onServerError(code, message);
            }
        }

        /// <summary>
        /// Manages REQERR/ERROR errors.
        /// </summary>
        protected internal virtual void forwardControlResponseError(int code, string message, BaseControlRequestListener listener)
        {
            if (code == 20)
            {
                session.onSyncError(ProtocolConstants.SYNC_RESPONSE);
                //Actually we're already END because
                //onSyncError will call errorEvent->closeSession->shutdown 
                //and finally stop on the protocol, thus this call is superfluous
                Status = StreamStatus.STREAM_CLOSED;
            }
            else if (code == 11)
            {
                // error 11 is managed as CONERR 21

                log.Debug("On Server Error - 21 - " + message);

                session.onServerError(21, message);
            }
            else if (listener != null && code != 65 /*65 is a fatal error*/)
            {
                /*
                 * since there is a listener (because it is a REQERR message), 
                 * don't fall-back to fatal error case
                 */
                listener.onError(code, message);
            }
            else
            {
                /*
                 * fall-back case handles fatal errors, i.e. ERROR messages:
                 * close current session, don't create a new session, notify client listeners
                 */

                log.Debug("On Server Error - 3 - " + code + " - " + message);

                this.session.onServerError(code, message);
                this.Status = StreamStatus.STREAM_CLOSED;
            }
        }

        protected internal void onIllegalMessage(string description)
        {
            forwardControlResponseError(61, description, null);
        }

        public virtual void onFatalError(Exception e)
        {

            log.Debug("On Server Error - 61.");

            this.session.onServerError(61, "Internal error");
            this.Status = StreamStatus.STREAM_CLOSED;
        }

        public virtual void stop(bool waitPendingControlRequests, bool forceConnectionClose)
        {
            log.Info("Protocol dismissed");
            SetStatus(StreamStatus.STREAM_CLOSED, forceConnectionClose);
            reverseHeartbeatTimer.onClose();
        }

        public virtual long MaxReverseHeartbeatIntervalMs
        {
            get
            {
                return reverseHeartbeatTimer.MaxIntervalMs;
            }
        }

        /// <summary>
        /// calls from Transport are made using the SessionThread
        /// </summary>
        public abstract class StreamListener : SessionRequestListener
        {
            private readonly TextProtocol outerInstance;

            public StreamListener(TextProtocol outerInstance)
            {
                this.outerInstance = outerInstance;
            }


            internal bool disabled = false;
            internal bool isOpen = false;
            internal bool isInterrupted = false;

            public virtual void disable()
            {
                disabled = true;
            }

            public void onMessage(string message)
            {
                if (disabled)
                {

                    outerInstance.log.Warn("Message discarded oid=" + outerInstance.objectId + ": " + message);

                    return;
                }
                doMessage(message);
            }

            protected internal virtual void doMessage(string message)
            {
                outerInstance.onProtocolMessage(message);
            }

            public void onOpen()
            {
                if (disabled)
                {
                    return;
                }
                doOpen();
            }

            protected internal virtual void doOpen()
            {
                this.isOpen = true;
            }

            public void onClosed()
            {
                if (disabled)
                {
                    return;
                }
                doClosed();
            }

            protected internal virtual void doClosed()
            {
                interruptSession(false);
            }

            public void onBroken()
            {
                if (disabled)
                {
                    return;
                }
                doBroken(false);
            }

            public void onBrokenWS()
            {
                if (disabled)
                {
                    return;
                }
                doBroken(true);
            }

            protected internal virtual void doBroken(bool wsError)
            {
                interruptSession(wsError);
            }

            /// <summary>
            /// Interrupts the current session if an error occurs or the session is closed unexpectedly. </summary>
            /// <param name="wsError"> unable to open WS </param>
            protected internal virtual void interruptSession(bool wsError)
            {
                if (!isInterrupted)
                {
                    outerInstance.session.onInterrupted(wsError, !this.isOpen);
                    isInterrupted = true;
                }
            }
        } // StreamListener

        /// <summary>
        /// Stream listener for create_session and recovery requests. 
        /// </summary>
        public class OpenSessionListener : StreamListener
        {
            private readonly TextProtocol outerInstance;

            public OpenSessionListener(TextProtocol outerInstance) : base(outerInstance)
            {
                this.outerInstance = outerInstance;
            }

        }

        /// <summary>
        /// Stream listener for bind_session requests supporting reverse heartbeats.
        /// </summary>
        public class BindSessionListener : StreamListener
        {
            private readonly TextProtocol outerInstance;

            public BindSessionListener(TextProtocol outerInstance) : base(outerInstance)
            {
                this.outerInstance = outerInstance;
            }


            protected internal override void doOpen()
            {
                base.doOpen();
                outerInstance.onBindSessionForTheSakeOfReverseHeartbeat();
            }
        }

        /// <summary>
        /// Allows to customize the behavior of <seealso cref="ReverseHeartbeatTimer.onBindSession(bool)"/>
        /// with respect to the transport: if the transport is HTTP, bind requests don't matter in the measuring
        /// of heartbeat distance; but if the transport is WebSocket bind requests matter. 
        /// </summary>
        protected internal abstract void onBindSessionForTheSakeOfReverseHeartbeat();

        public abstract class BaseControlRequestListener : RequestListener
        {
            private readonly TextProtocol outerInstance;

            internal bool opened = false;
            internal bool completed = false;
            protected internal readonly RequestTutor tutor;
            internal readonly StringBuilder response = new StringBuilder();

            public BaseControlRequestListener(TextProtocol outerInstance, RequestTutor tutor)
            {
                this.outerInstance = outerInstance;
                this.tutor = tutor;
            }

            public abstract void onOK();

            public abstract void onError(int code, string message);

            public virtual void onOpen()
            {
                if (tutor != null)
                {
                    opened = true;
                    tutor.notifySender(false);
                }
            }

            public virtual void onMessage(string message)
            {
                response.Append(message);
            }

            public virtual void onClosed()
            {
                if (completed)
                {
                    return;
                }
                completed = true;
                if (!opened)
                {
                    if (tutor != null)
                    {
                        tutor.notifySender(true);
                    }
                }
                else
                {
                    this.onComplete(response.ToString());
                }
            }

            public virtual void onComplete(string message)
            {
                if (string.ReferenceEquals(message, null) || message.Length == 0)
                {
                    // an empty message means that the sever has probably closed the socket.
                    // ignore it and await the request timeout expires and the request is transmitted again.
                    return;
                }

                try
                {
                    ControlResponseParser parser = ControlResponseParser.parseControlResponse(message);
                    if (parser is REQOKParser)
                    {

                        outerInstance.log.Debug("OnOk - " + message);

                        this.onOK();

                    }
                    else if (parser is REQERRParser)
                    {
                        REQERRParser request = (REQERRParser)parser;
                        outerInstance.forwardControlResponseError(request.errorCode, request.errorMsg, this);

                    }
                    else if (parser is ERRORParser)
                    {
                        ERRORParser request = (ERRORParser)parser;
                        outerInstance.forwardControlResponseError(request.errorCode, request.errorMsg, this);

                    }
                    else
                    {
                        // should not happen
                        outerInstance.onIllegalMessage("Unexpected response to control request: " + message);
                    }

                }
                catch (ParsingException e)
                {
                    outerInstance.onIllegalMessage(e.Message);
                }
            }

            public virtual void onBroken()
            {
                if (completed)
                {
                    return;
                }
                completed = true;
                if (!opened && tutor != null)
                {
                    tutor.notifySender(true);
                }
            }

        }

        /// <summary>
        /// Control request listener supporting reverse heartbeats.
        /// </summary>
        public abstract class ControlRequestListener : BaseControlRequestListener
        {
            private readonly TextProtocol outerInstance;


            public ControlRequestListener(TextProtocol outerInstance, RequestTutor tutor) : base(outerInstance, tutor)
            {
                this.outerInstance = outerInstance;
            }

            public override void onOpen()
            {
                base.onOpen();
                outerInstance.reverseHeartbeatTimer.onControlRequest();
            }
        }
    }
}