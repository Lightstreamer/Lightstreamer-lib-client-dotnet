using com.lightstreamer.client.requests;
using com.lightstreamer.client.transport;
using Lightstreamer.DotNet.Logging.Log;
using System;
using System.Threading.Tasks;

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
namespace com.lightstreamer.client.session
{

    public class SessionManager : SessionListener
    {

        private static SessionFactory sessionFactory = new SessionFactory();
        public static SessionFactory CustomFactory
        {
            set
            { //just for testing
                sessionFactory = value;
            }
        }

        private enum Status
        {
            OFF,
            STREAMING_WS,
            SWITCHING_STREAMING_WS,
            POLLING_WS,
            SWITCHING_POLLING_WS,
            STREAMING_HTTP,
            SWITCHING_STREAMING_HTTP,
            POLLING_HTTP,
            SWITCHING_POLLING_HTTP,
            END,
            ERROR
        }

        private const bool STREAMING_SESSION = false;
        private const bool POLLING_SESSION = true;
        private const bool WS_SESSION = false;
        private const bool HTTP_SESSION = true;

        private const bool AVOID_SWITCH = true;
        //private static final boolean NO_RECOVERY = true;
        private const bool YES_RECOVERY = false;

        private static string statusToString(Status type)
        {
            if (type == Status.ERROR)
            {
                return null;
            }

            switch (type)
            {
                case com.lightstreamer.client.session.SessionManager.Status.OFF:
                    return "No session";
                case com.lightstreamer.client.session.SessionManager.Status.STREAMING_WS:
                    return "WS Streaming";
                case com.lightstreamer.client.session.SessionManager.Status.SWITCHING_STREAMING_WS:
                    return "prepare WS Streaming";
                case com.lightstreamer.client.session.SessionManager.Status.POLLING_WS:
                    return "WS Polling";
                case com.lightstreamer.client.session.SessionManager.Status.SWITCHING_POLLING_WS:
                    return "prepare WS Polling";
                case com.lightstreamer.client.session.SessionManager.Status.STREAMING_HTTP:
                    return "HTTP Streaming";
                case com.lightstreamer.client.session.SessionManager.Status.SWITCHING_STREAMING_HTTP:
                    return "prepare HTTP Streaming";
                case com.lightstreamer.client.session.SessionManager.Status.POLLING_HTTP:
                    return "HTTP Polling";
                case com.lightstreamer.client.session.SessionManager.Status.SWITCHING_POLLING_HTTP:
                    return "prepare HTTP Polling";
                case com.lightstreamer.client.session.SessionManager.Status.END:
                    return "Shutting down";
                default:
                    return "Error";
            }
        }

        protected internal readonly ILogger log = LogManager.GetLogger(Constants.SESSION_LOG);
        private Status status = Status.OFF;
        private int statusPhase = 0;
        protected internal Session session = null;
        protected internal ServerSession serverSession = null;
        private bool isFrozen = false;
        private string clientIP = null;
        protected internal InternalConnectionOptions options;
        protected internal InternalConnectionDetails details;
        protected internal SubscriptionsListener subscriptions;
        protected internal MessagesListener messages;
        protected internal SessionsListener listener;
        private readonly SessionThread thread;

        /// <summary>
        /// Counts the bind_session requests following the corresponding create_session.
        /// </summary>
        private int nBindAfterCreate = 0;

        public SessionManager(InternalConnectionOptions options, InternalConnectionDetails details, SessionThread thread)
        {
            this.options = options;
            this.details = details;
            this.thread = thread;
        }

        // only for test
        public SessionManager(InternalConnectionOptions options, InternalConnectionDetails details, SessionsListener listener, SubscriptionsListener subscriptions, MessagesListener messages, SessionThread thread) : this(options, details, thread)
        {
            this.subscriptions = subscriptions;
            this.messages = messages;
            this.listener = listener;
        }

        public virtual SessionsListener SessionsListener
        {
            set
            {
                this.listener = value;
            }
        }

        public virtual SubscriptionsListener SubscriptionsListener
        {
            set
            {
                this.subscriptions = value;
            }
        }

        public virtual MessagesListener MessagesListener
        {
            set
            {
                this.messages = value;
            }
        }

        public virtual string SessionId
        {
            get
            {
                return session == null ? "" : session.SessionId;
            }
        }

        private void changeStatus(Status newStatus)
        {
            if (log.IsDebugEnabled)
            {
                log.Debug("SessionManager state change: " + status + " -> " + newStatus);
            }
            this.status = newStatus;
            this.statusPhase++;
        }

        private bool @is(Status check)
        {
            return this.status.Equals(check);
        }

        private bool isNot(Status check)
        {
            return !@is(check);
        }

        private bool Alive
        {
            get
            {
                return isNot(Status.OFF) && isNot(Status.END);
            }
        }

        private Status NextSensePhase
        {
            get
            {
                switch (this.status)
                {
                    case com.lightstreamer.client.session.SessionManager.Status.STREAMING_WS:
                        if (this.isFrozen)
                        {
                            return Status.SWITCHING_STREAMING_WS;
                        }
                        else
                        {
                            return Status.SWITCHING_STREAMING_HTTP;
                        }

                    case com.lightstreamer.client.session.SessionManager.Status.STREAMING_HTTP:
                        return Status.SWITCHING_POLLING_HTTP;

                    case com.lightstreamer.client.session.SessionManager.Status.POLLING_WS:
                        return Status.SWITCHING_STREAMING_WS;

                    case com.lightstreamer.client.session.SessionManager.Status.POLLING_HTTP:
                        if (this.isFrozen)
                        {
                            return Status.SWITCHING_POLLING_HTTP;
                        }
                        else
                        {
                            return Status.SWITCHING_STREAMING_WS;
                        }

                    default: //already switching
                        return this.status;

                }
            }
        }

        private Status NextSlowPhase
        {
            get
            {
                switch (this.status)
                {
                    case com.lightstreamer.client.session.SessionManager.Status.STREAMING_WS:
                        return Status.SWITCHING_POLLING_WS;

                    case com.lightstreamer.client.session.SessionManager.Status.STREAMING_HTTP:
                    case com.lightstreamer.client.session.SessionManager.Status.SWITCHING_STREAMING_HTTP:
                    case com.lightstreamer.client.session.SessionManager.Status.SWITCHING_POLLING_HTTP:
                        //a slow command can be issued while we're switching (while we wait for the loop command)
                        return Status.SWITCHING_POLLING_HTTP;

                    //it's still not possible during SWITCHING_STREAMING_WS (unless we manually ask to switch) 
                    //and during SWITCHING_POLLING_WS (unless we manually ask to switch we only switch to polling ws because
                    //of a slowing so that a second call is not possible because of the isSlowRequired check.)
                    default: //already switching
                        return Status.ERROR;
                }
            }
        }

        private String getSessionId()
        {
            return session == null ? "" : session.SessionId;
        }

        /////////////////////////////////////////////API Calls

        /// <summary>
        /// A non-recoverable error causing the closing of the session.
        /// </summary>
        public virtual void onFatalError(Exception e)
        {
            if (session != null)
            {
                session.onFatalError(e);
            }
        }

        public virtual void changeBandwidth()
        {
            if (this.session != null)
            {
                this.session.sendConstrain(0, null);
            }
        }

        public virtual void handleReverseHeartbeat(bool force)
        {
            if (this.session != null)
            {
                this.session.handleReverseHeartbeat(force);
            }
        }

        public virtual void closeSession(bool fromAPI, string reason, bool noRecoveryScheduled)
        {
            try
            {
                if (noRecoveryScheduled)
                {
                    /*
                     * Since, when noRecoveryScheduled is true, this method is called by Lightstreamer.disconnect();
                     * then we are sure that the server session can be safely discarded.
                     */
                    if (serverSession != null)
                    {
                        serverSession.close();
                    }
                }
                if (this.@is(Status.OFF) || this.@is(Status.END))
                {
                    return;
                }

                if (this.session != null)
                {
                    log.Debug("Closing the session.");

                    this.session.closeSession(fromAPI ? "api" : reason, false, noRecoveryScheduled);
                }
            }
            catch (Exception e)
            {
                log.Warn("Something went wrong here ::: " + e.Message);
                if (log.IsDebugEnabled)
                {
                    log.Debug(e.StackTrace);
                }
            }

        }

        public virtual void createSession(bool fromAPI, bool isTransportForced, bool isComboForced, bool isPolling, bool isHTTP, string reason, bool avoidSwitch, bool retryAgainIfStreamFails, bool startRecovery)
        {
            reason = fromAPI ? "api" : reason;

            this.isFrozen = isTransportForced;

            if (!avoidSwitch && this.Alive)
            {
                Status nextPH = isPolling ? ( isHTTP ? Status.SWITCHING_POLLING_HTTP : Status.SWITCHING_POLLING_WS ) : ( isHTTP ? Status.SWITCHING_STREAMING_HTTP : Status.SWITCHING_STREAMING_WS );
                this.changeStatus(nextPH);
                this.startSwitchTimeout(reason, 0);
                this.session.requestSwitch(this.statusPhase, reason, isComboForced, startRecovery);

            }
            else
            {
                //there is no session active or we want to start from scratch

                string currSessionId = this.session != null ? this.session.SessionId : null;
                reason = "new." + reason;
                this.closeSession(false, reason, YES_RECOVERY);

                Status nextPH = isPolling ? ( isHTTP ? Status.POLLING_HTTP : Status.POLLING_WS ) : ( isHTTP ? Status.STREAMING_HTTP : Status.STREAMING_WS );
                this.changeStatus(nextPH);

                this.prepareNewSessionInstance(isPolling, isComboForced, isHTTP, null, retryAgainIfStreamFails, false);

                this.session.createSession(currSessionId, reason);
            }

        }

        private void prepareNewSessionInstance(bool isPolling, bool isComboForced, bool isHTTP, Session prevSession, bool retryAgainIfStreamFails, bool sessionRecovery)
        {

            this.session = sessionFactory.createNewSession(isPolling, isComboForced, isHTTP, prevSession, this, subscriptions, messages, thread, details, options, this.statusPhase, retryAgainIfStreamFails, sessionRecovery);

            if (prevSession == null)
            {
                // create_session
                if (serverSession != null)
                {
                    serverSession.close();
                }
                serverSession = new ServerSession(session);

            }
            else
            {
                // bind_session
                serverSession.NewStreamConnection = session;
            }

            if (prevSession != null)
            {
                prevSession.shutdown(false); //close it without killing the session, the new session is taking its place
            }
        }

        private void bindSession(bool isForced, bool isPolling, bool isHTTP, string switchCause, bool startRecovery)
        {
            Status nextPH = isPolling ? ( isHTTP ? Status.POLLING_HTTP : Status.POLLING_WS ) : ( isHTTP ? Status.STREAMING_HTTP : Status.STREAMING_WS );
            this.changeStatus(nextPH);

            this.prepareNewSessionInstance(isPolling, isForced, isHTTP, this.session, false, startRecovery);

            this.session.bindSession(switchCause);
        }

        private void startSwitchTimeout(string reason, long delay)
        {
            long timeout = this.options.SwitchCheckTimeout + delay; //we could add the delay from the slowing, but that will probably make things worse
                                                                    //we might take into account how much the connect timeout has been increased
            int ph = this.statusPhase;

            thread.schedule(new Task(() =>
           {
               switchTimeout(ph, reason);
           }), timeout);
        }

        public virtual void retry(int handlerPhase, string retryCause, bool forced, bool retryAgainIfStreamFails)
        {
            if (handlerPhase != this.statusPhase)
            {
                return;
            }

            bool strOrPoll = this.@is(Status.STREAMING_WS) || this.@is(Status.STREAMING_HTTP) ? STREAMING_SESSION : POLLING_SESSION;
            bool wsOrHttp = this.@is(Status.STREAMING_WS) || this.@is(Status.POLLING_WS) ? WS_SESSION : HTTP_SESSION;


            this.createSession(false, this.isFrozen, forced, strOrPoll, wsOrHttp, retryCause, AVOID_SWITCH, retryAgainIfStreamFails, false);
        }

        /// <summary>
        /// The method is similar to {@code bindSession} but there are the following differences:
        /// <ul>
        /// <li>{@code prepareNewSessionInstance} is called with the argument {@code sessionRecovery} set to true</li>
        /// <li>a special bind_session request is sent (see <seealso cref="RecoverSessionRequest"/>)</li>
        /// </ul>
        /// </summary>
        public virtual void recoverSession(int handlerPhase, string retryCause, bool forced, bool retryAgainIfStreamFails)
        {

            log.Debug("Start recover session (virtual); handler: " + handlerPhase + " != " + this.statusPhase);

            if (handlerPhase != this.statusPhase)
            {
                return;
            }

            bool isPolling = this.@is(Status.STREAMING_WS) || this.@is(Status.STREAMING_HTTP) ? STREAMING_SESSION : POLLING_SESSION;
            bool isHTTP = this.@is(Status.STREAMING_WS) || this.@is(Status.POLLING_WS) ? WS_SESSION : HTTP_SESSION;
            Status nextPH = isPolling ? ( isHTTP ? Status.POLLING_HTTP : Status.POLLING_WS ) : ( isHTTP ? Status.STREAMING_HTTP : Status.STREAMING_WS );
            this.changeStatus(nextPH);

            this.prepareNewSessionInstance(isPolling, forced, isHTTP, this.session, retryAgainIfStreamFails, true);
            this.session.recoverSession();
        }

        private void switchTimeout(int ph, string reason)
        {
            if (ph != this.statusPhase)
            {
                return;
            }

            log.Info("Failed to switch session type. Starting new session " + statusToString(this.status));

            Status switchType = this.status;

            if (isNot(Status.SWITCHING_STREAMING_WS) && isNot(Status.SWITCHING_STREAMING_HTTP) && isNot(Status.SWITCHING_POLLING_HTTP) && isNot(Status.SWITCHING_POLLING_WS))
            {
                log.Error("Unexpected fallback type switching because of a failed force rebind");
                return;
            }
            reason = "switch.timeout." + reason;

            bool strOrPoll = switchType.Equals(Status.SWITCHING_STREAMING_WS) || switchType.Equals(Status.SWITCHING_STREAMING_HTTP) ? STREAMING_SESSION : POLLING_SESSION;
            bool wsOrHttp = switchType.Equals(Status.SWITCHING_STREAMING_WS) || switchType.Equals(Status.SWITCHING_POLLING_WS) ? WS_SESSION : HTTP_SESSION;

            this.createSession(false, this.isFrozen, false, strOrPoll, wsOrHttp, reason, AVOID_SWITCH, false, false);

        }

        public virtual void streamSenseSwitch(int handlerPhase, string reason, string sessionPhase, bool startRecovery)
        {
            if (handlerPhase != this.statusPhase)
            {
                return;
            }

            Status switchType = NextSensePhase;

            if (switchType.Equals(Status.OFF) || switchType.Equals(Status.END))
            {
                log.Warn("Unexpected fallback type switching with new session");
                return;
            }

            log.Info("Unable to establish session of the current type. Switching session type " + statusToString(this.status) + "->" + statusToString(switchType));

            /*
             * Since WebSocket transport is not working, we disable it for this session and the next ones.
             * WebSocket will be enabled again, if the client IP changes.
             */
            if (sessionPhase.Equals("FIRST_BINDING") && status.Equals(Status.STREAMING_WS) && switchType.Equals(Status.SWITCHING_STREAMING_HTTP))
            {

                log.Info("WebSocket support has been disabled.");
                WebSocket.disable();
            }

            this.changeStatus(switchType);

            this.startSwitchTimeout(reason, 0);
            this.session.requestSwitch(this.statusPhase, reason, false, startRecovery);
        }

        public virtual void onSlowRequired(int handlerPhase, long delay)
        {
            if (handlerPhase != this.statusPhase)
            {
                return;
            }

            Status switchType = this.NextSlowPhase;

            log.Info("Slow session detected. Switching session type " + statusToString(this.status) + "->" + statusToString(switchType));

            if (switchType == Status.ERROR)
            {
                log.Error("Unexpected fallback type; switching because of a slow connection was detected" + statusToString(this.status) + ", " + this.session);
                return;
            }
            this.changeStatus(switchType);

            this.startSwitchTimeout("slow", delay);
            this.session.requestSlow(this.statusPhase);

        }

        ////////////////////////////PUSH EVENTS  

        public virtual void sessionStatusChanged(int handlerPhase, string phase, bool sessionRecovery)
        {
            if (log.IsDebugEnabled)
            {
                log.Debug("sessionStatusChanged: " + handlerPhase + " = " + this.statusPhase);
            }

            if (handlerPhase != this.statusPhase)
            {
                return;
            }
            this.listener.onStatusChanged(this.getHighLevelStatus(sessionRecovery));
        }

        public virtual void onIPReceived(string newIP)
        {
            if (!string.ReferenceEquals(this.clientIP, null) && !newIP.Equals(this.clientIP) && WebSocket.Disabled)
            {
                WebSocket.restore();
                session.restoreWebSocket();
            }
            this.clientIP = newIP;
        }

        public virtual void onSessionBound()
        {
            if (nBindAfterCreate == 0)
            {
                /*
                 * The check is needed to distinguish a true change of session (i.e. a new session id)
                 * from a change of transport preserving the session.
                 * We are only interested in true change of session.
                 */
                // mpnEventManager.onSessionStart();
            }
            nBindAfterCreate++;

        }

        public virtual void onSessionStart()
        {
            nBindAfterCreate = 0;
        }

        public virtual void onServerError(int errorCode, string errorMessage)
        {
            this.listener.onServerError(errorCode, errorMessage);
        }

        public virtual int onSessionClose(int handlerPhase, bool noRecoveryScheduled)
        {
            if (handlerPhase != this.statusPhase)
            {
                return 0;
            }

            log.Debug("Session closed: " + getSessionId());

            if (noRecoveryScheduled)
            {
                this.changeStatus(Status.OFF);
            }
            else
            {
                this.changeStatus(this.status); //so that the statusPhase changes
            }

            // mpnEventManager.onSessionClose(!noRecoveryScheduled);

            return this.statusPhase;
        }


        /////////////////////////////////////////////////GETTERS

        public virtual string getHighLevelStatus(bool sessionRecovery)
        {
            string hlStatus = this.session == null ? Constants.DISCONNECTED : this.session.getHighLevelStatus(sessionRecovery);
            return hlStatus;
        }

        public virtual Session Session
        {
            get
            {
                return session; // may be null
            }
        }

        public virtual ServerSession ServerSession
        {
            get
            {
                return serverSession;
            }
        }

        //////////////////////////////////////////////MESSAGE API CALLS

        public virtual void sendMessage(MessageRequest request, RequestTutor tutor)
        {
            if (this.session != null)
            {
                this.session.sendMessage(request, tutor);
            }
        }

        /////////////////////////////////////////////////SUBSCRIPTIONS API CALLS  

        public virtual void sendSubscription(SubscribeRequest request, RequestTutor tutor)
        {
            if (this.session != null)
            {
                this.session.sendSubscription(request, tutor);
            }
        }

        public virtual void sendUnsubscription(UnsubscribeRequest request, RequestTutor tutor)
        {
            if (this.session != null)
            {
                this.session.sendUnsubscription(request, tutor);
            }
        }

        public virtual void sendSubscriptionChange(ChangeSubscriptionRequest request, RequestTutor tutor)
        {
            if (this.session != null)
            {
                this.session.sendSubscriptionChange(request, tutor);
            }
        }

        public virtual void sendReverseHeartbeat(ReverseHeartbeatRequest request, RequestTutor tutor)
        {
            if (this.session != null)
            {
                this.session.sendReverseHeartbeat(request, tutor);
            }
        }

        //////////////////////

        public virtual void switchToWebSocket(bool startRecovery)
        {
            createSession(false, this.isFrozen, false, false, false, "ip", false, false, startRecovery);
        }

        void SessionListener.sessionStatusChanged(int handlerPhase, string phase, bool sessionRecovery)
        {
            // throw new NotImplementedException();
            log.Debug("sessionStatusChanged " + handlerPhase + ", " + this.statusPhase);

            if (handlerPhase != this.statusPhase)
            {
                return;
            }
            this.listener.onStatusChanged(this.getHighLevelStatus(sessionRecovery));
        }

        public virtual void streamSense(int handlerPhase, string switchCause, bool forced)
        {
            if (handlerPhase != this.statusPhase)
            {

                log.Warn("Mismatching pahse; handler: " + handlerPhase + " != " + this.statusPhase);

                return;
            }

            Status switchType = this.NextSensePhase;
            log.Info("Setting up new session type " + statusToString(this.status) + "->" + statusToString(switchType));

            if (switchType.Equals(Status.OFF) || switchType.Equals(Status.END))
            {
                log.Warn("Unexpected fallback type switching with new session");
                return;
            }

            bool strOrPoll = switchType.Equals(Status.SWITCHING_STREAMING_WS) || switchType.Equals(Status.SWITCHING_STREAMING_HTTP) ? STREAMING_SESSION : POLLING_SESSION;
            bool wsOrHttp = switchType.Equals(Status.SWITCHING_STREAMING_WS) || switchType.Equals(Status.SWITCHING_POLLING_WS) ? WS_SESSION : HTTP_SESSION;

            //if we enter the createMachine the status of the session is unknown, so we can't recover by switching so 
            //we ask to AVOID_SWITCH
            this.createSession(false, this.isFrozen, forced, strOrPoll, wsOrHttp, switchCause, AVOID_SWITCH, false, false);
        }

        public virtual void switchReady(int handlerPhase, string switchCause, bool forced, bool startRecovery)
        {
            if (handlerPhase != this.statusPhase)
            {
                return;
            }

            Status switchType = this.status;
            log.Info("Switching current session type " + statusToString(this.status));

            if (isNot(Status.SWITCHING_STREAMING_WS) && isNot(Status.SWITCHING_STREAMING_HTTP) && isNot(Status.SWITCHING_POLLING_HTTP) && isNot(Status.SWITCHING_POLLING_WS))
            {
                log.Error("Unexpected fallback type switching with a force rebind");
                return;
            }


            bool strOrPoll = switchType.Equals(Status.SWITCHING_STREAMING_WS) || switchType.Equals(Status.SWITCHING_STREAMING_HTTP) ? STREAMING_SESSION : POLLING_SESSION;
            bool wsOrHttp = switchType.Equals(Status.SWITCHING_STREAMING_WS) || switchType.Equals(Status.SWITCHING_POLLING_WS) ? WS_SESSION : HTTP_SESSION;

            this.bindSession(forced, strOrPoll, wsOrHttp, switchCause, startRecovery);
        }

        public virtual void slowReady(int handlerPhase)
        {
            if (handlerPhase != this.statusPhase)
            {
                return;
            }
            log.Info("Slow session switching");
            this.switchReady(handlerPhase, "slow", false, false);
        }

        int SessionListener.onSessionClose(int handlerPhase, bool noRecoveryScheduled)
        {
            if (handlerPhase != this.statusPhase)
            {
                return 0;
            }

            if (noRecoveryScheduled)
            {
                this.changeStatus(Status.OFF);
            }
            else
            {
                this.changeStatus(this.status); //so that the statusPhase changes
            }

            // mpnEventManager.onSessionClose(!noRecoveryScheduled);

            return this.statusPhase;
        }

        void SessionListener.streamSenseSwitch(int handlerPhase, string reason, string sessionPhase, bool startRecovery)
        {
            if (handlerPhase != this.statusPhase)
            {
                return;
            }

            Status switchType = NextSensePhase;

            if (switchType.Equals(Status.OFF) || switchType.Equals(Status.END))
            {
                log.Warn("Unexpected fallback type switching with new session");
                return;
            }

            log.Info("Unable to establish session of the current type. Switching session type " + statusToString(this.status) + "->" + statusToString(switchType));

            /*
             * Since WebSocket transport is not working, we disable it for this session and the next ones.
             * WebSocket will be enabled again, if the client IP changes.
             */
            if (sessionPhase.Equals("FIRST_BINDING") && status.Equals(Status.STREAMING_WS) && switchType.Equals(Status.SWITCHING_STREAMING_HTTP))
            {
                log.Debug("WebSocket support has been disabled.");
                WebSocket.disable();
            }

            this.changeStatus(switchType);

            this.startSwitchTimeout(reason, 0);
            this.session.requestSwitch(this.statusPhase, reason, false, startRecovery);
        }

        void SessionListener.onIPReceived(string clientIP)
        {
            if (!string.ReferenceEquals(this.clientIP, null) && !clientIP.Equals(this.clientIP) && WebSocket.Disabled)
            {
                WebSocket.restore();
                session.restoreWebSocket();
            }
            this.clientIP = clientIP;
        }

        void SessionListener.onSessionBound()
        {
            // throw new NotImplementedException();
            nBindAfterCreate++;

        }

        void SessionListener.onSessionStart()
        {
            nBindAfterCreate = 0;
        }

        void SessionListener.onServerError(int errorCode, string errorMessage)
        {
            this.listener.onServerError(errorCode, errorMessage);
        }

        void SessionListener.onSlowRequired(int handlerPhase, long delay)
        {
            if (handlerPhase != this.statusPhase)
            {
                return;
            }

            Status switchType = this.NextSlowPhase;

            log.Info("Slow session detected. Switching session type " + statusToString(this.status) + "->" + statusToString(switchType));

            if (switchType == Status.ERROR)
            {
                log.Error("Unexpected fallback type; switching because of a slow connection was detected" + statusToString(this.status) + ", " + this.session);
                return;
            }
            this.changeStatus(switchType);

            this.startSwitchTimeout("slow", delay);
            this.session.requestSlow(this.statusPhase);
        }

        void SessionListener.retry(int handlerPhase, string retryCause, bool forced, bool retryAgainIfStreamFails)
        {
            if (handlerPhase != this.statusPhase)
            {
                return;
            }

            bool strOrPoll = this.@is(Status.STREAMING_WS) || this.@is(Status.STREAMING_HTTP) ? STREAMING_SESSION : POLLING_SESSION;
            bool wsOrHttp = this.@is(Status.STREAMING_WS) || this.@is(Status.POLLING_WS) ? WS_SESSION : HTTP_SESSION;


            this.createSession(false, this.isFrozen, forced, strOrPoll, wsOrHttp, retryCause, AVOID_SWITCH, retryAgainIfStreamFails, false);
        }

        void SessionListener.switchToWebSocket(bool startRecovery)
        {
            createSession(false, this.isFrozen, false, false, false, "ip", false, false, startRecovery);
        }

        void SessionListener.onMpnRegisterOK(string deviceId, string adapterName)
        {
            throw new NotImplementedException();
        }

        void SessionListener.onMpnRegisterError(int code, string message)
        {
            throw new NotImplementedException();
        }

        void SessionListener.onMpnSubscribeOK(string lsSubId, string pnSubId)
        {
            throw new NotImplementedException();
        }

        void SessionListener.onMpnSubscribeError(string subId, int code, string message)
        {
            throw new NotImplementedException();
        }

        void SessionListener.onMpnUnsubscribeError(string subId, int code, string message)
        {
            throw new NotImplementedException();
        }

        void SessionListener.onMpnUnsubscribeOK(string subId)
        {
            throw new NotImplementedException();
        }

        void SessionListener.onMpnResetBadgeOK(string deviceId)
        {
            throw new NotImplementedException();
        }

        void SessionListener.onMpnBadgeResetError(int code, string message)
        {
            throw new NotImplementedException();
        }

        void SessionListener.recoverSession(int handlerPhase, string retryCause, bool forced, bool retryAgainIfStreamFails)
        {

            if (handlerPhase != this.statusPhase)
            {
                return;
            }
            try
            {
                bool isPolling = this.@is(Status.STREAMING_WS) || this.@is(Status.STREAMING_HTTP) ? STREAMING_SESSION : POLLING_SESSION;
                bool isHTTP = this.@is(Status.STREAMING_WS) || this.@is(Status.POLLING_WS) ? WS_SESSION : HTTP_SESSION;
                Status nextPH = isPolling ? ( isHTTP ? Status.POLLING_HTTP : Status.POLLING_WS ) : ( isHTTP ? Status.STREAMING_HTTP : Status.STREAMING_WS );
                this.changeStatus(nextPH);

                this.prepareNewSessionInstance(isPolling, forced, isHTTP, this.session, retryAgainIfStreamFails, true);

                if (this.session != null)
                {
                    this.session.recoverSession();
                }
                
            }
            catch (Exception e)
            {
                log.Warn("Something went wrong here ::::: " + e.Message);
                if (log.IsDebugEnabled)
                {
                    log.Debug(e.StackTrace);
                }
            }

        }
    }

}