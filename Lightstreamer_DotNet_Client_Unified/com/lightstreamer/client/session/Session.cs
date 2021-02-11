using com.lightstreamer.client.protocol;
using com.lightstreamer.client.requests;
using com.lightstreamer.util;
using com.lightstreamer.util.mdc;
using DotNetty.Common.Concurrency;
using Lightstreamer.DotNet.Logging.Log;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
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

    /// <summary>
    /// Calls to this class are all performed through the Session Thread 
    /// </summary>
    public abstract class Session
    {


        protected internal const string OFF = "OFF";
        protected internal const string CREATING = "CREATING";
        protected internal const string CREATED = "CREATED";
        protected internal const string FIRST_PAUSE = "FIRST_PAUSE";
        protected internal const string FIRST_BINDING = "FIRST_BINDING";
        protected internal const string PAUSE = "PAUSE";
        protected internal const string BINDING = "BINDING";
        protected internal const string RECEIVING = "RECEIVING";
        protected internal const string STALLING = "STALLING";
        protected internal const string STALLED = "STALLED";
        protected internal const string SLEEP = "SLEEP";

        protected internal const bool GO_TO_SLEEP = true;
        protected internal const bool GO_TO_OFF = false;

        protected internal const bool CLOSED_ON_SERVER = true;
        protected internal const bool OPEN_ON_SERVER = false;
        protected internal const bool NO_RECOVERY_SCHEDULED = true;
        protected internal const bool RECOVERY_SCHEDULED = false;

        private const int PERMISSION_TO_FAIL = 1;

        protected internal readonly ILogger log = LogManager.GetLogger(Constants.SESSION_LOG);

        /// <summary>
        /// Address of the server for the current session.
        /// It can be the control-link (carried by CONOK message), 
        /// or <seealso cref="ConnectionDetails#getServerAddress()"/> if the control-link is not set.
        /// It can also be null before receiving the first CONOK message.
        /// </summary>
        protected internal string sessionServerAddress = null;
        /// <summary>
        /// Copy of <seealso cref="ConnectionDetails#getServerAddress()"/>.
        /// </summary>
        protected internal string serverAddressCache = null;
        /// <summary>
        /// Copy of <seealso cref="ConnectionOptions#isServerInstanceAddressIgnored()"/>.
        /// </summary>
        protected internal bool ignoreServerAddressCache = false;

        protected internal bool isPolling;
        protected internal bool isForced;
        private string sessionId;
        protected internal int bindCount = 0;
        private long dataNotificationCount = 0;

        private bool switchRequired = false;
        private bool slowRequired = false;
        private bool switchForced = false;
        private string switchCause = "";
        /// <summary>
        /// WebSocket support has been enabled again because the client IP has changed. 
        /// Next bind_session must try WebSocket as transport.
        /// </summary>
        private bool switchToWebSocket = false;

        private bool cachedRequiredBW;

        private int workedBefore = 0;
        private long sentTime = 0;
        private static CancellationTokenSource lastKATask = null;
        private long reconnectTimeout = 0;

        protected internal string phase = OFF;
        protected internal int phaseCount = 0;

        protected internal SessionListener handler;
        protected internal int handlerPhase;

        protected internal InternalConnectionDetails details;
        protected internal InternalConnectionOptions options;
        private SlowingHandler slowing;
        private SubscriptionsListener subscriptions;
        private MessagesListener messages;

        private SessionThread thread;
        protected internal readonly Protocol protocol;
        private bool retryAgainIfStreamFails;
        private OfflineCheck offlineCheck;

        /// <summary>
        /// Recovery status bean.
        /// </summary>
        protected internal readonly RecoveryBean recoveryBean;

        protected internal readonly int objectId;

        protected internal Session(int objectId, bool isPolling, bool forced, SessionListener handler, SubscriptionsListener subscriptions, MessagesListener messages, Session originalSession, SessionThread thread, Protocol protocol, InternalConnectionDetails details, InternalConnectionOptions options, int callerPhase, bool retryAgainIfStreamFails, bool sessionRecovery)
        {

            this.objectId = objectId;
            if (log.IsDebugEnabled)
            {
                log.Debug("New session oid=" + this.objectId);
            }

            this.isPolling = isPolling;
            this.isForced = forced;

            this.handler = handler;
            this.handlerPhase = callerPhase;

            this.details = details;
            this.options = options;

            this.slowing = new SlowingHandler(this.options);

            this.subscriptions = subscriptions;
            this.messages = messages;

            this.thread = thread;

            this.protocol = protocol;
            this.protocol.Listener = new TextProtocolListener(this);

            this.retryAgainIfStreamFails = retryAgainIfStreamFails;
            this.offlineCheck = new OfflineCheck(thread);

            if (originalSession != null)
            {

                SessionId = originalSession.sessionId;
                this.sessionServerAddress = originalSession.sessionServerAddress;
                this.bindCount = originalSession.bindCount;
                this.dataNotificationCount = originalSession.dataNotificationCount;

                Debug.Assert(!string.ReferenceEquals(originalSession.serverAddressCache, null));
                this.serverAddressCache = originalSession.serverAddressCache;
                this.ignoreServerAddressCache = originalSession.ignoreServerAddressCache;

                this.slowing.MeanElaborationDelay = originalSession.slowing.MeanElaborationDelay;

                originalSession.protocol.copyPendingRequests(this.protocol);

                this.recoveryBean = new RecoveryBean(sessionRecovery, originalSession.recoveryBean);

            }
            else
            {
                Debug.Assert(!sessionRecovery);
                this.recoveryBean = new RecoveryBean();
            }

        }


        private void reset()
        {
            SessionId = null;
            this.sessionServerAddress = null;
            this.bindCount = 0;
            this.dataNotificationCount = 0;

            this.serverAddressCache = null;
            this.ignoreServerAddressCache = false;

            this.switchRequired = false;
            this.switchForced = false;
            this.slowRequired = false;
            this.switchCause = "";

            this.cachedRequiredBW = false; //this is set only if a changeBW request is received while in CREATING status (too late to send it via create_session, too early to issue a control)
                                           //note that when the session number is received the control handler is reset, so that put it there is not an option
        }


        ///////////////////////////////////phase handling

        protected internal virtual bool @is(string phaseToCheck)
        {
            return this.phase.Equals(phaseToCheck);
        }

        protected internal virtual bool isNot(string phaseToCheck)
        {
            return !this.@is(phaseToCheck);
        }

        protected internal virtual bool changePhaseType(string newType, bool startRecovery)
        {
            string oldType = this.phase;
            int ph = this.phaseCount;

            if (isNot(newType))
            {
                this.phase = newType;
                this.phaseCount++;
                ph = this.phaseCount;

                this.handler.sessionStatusChanged(this.handlerPhase, this.phase, startRecovery);

                if (log.IsDebugEnabled)
                {
                    log.Debug("Session state change (" + objectId + "): " + oldType + " -> " + newType);
                    log.Debug(" phasing : " + ph + " - " + this.phaseCount);
                }
            }

            //XXX this check should be useless, this.handler.statusChanged should never change our status: verify and adapt
            return ph == this.phaseCount;
        }

        protected internal virtual bool changePhaseType(string newType)
        {
            return changePhaseType(newType, false);
        }

        internal virtual string getHighLevelStatus(bool startRecovery)
        {
            if (@is(OFF))
            {
                return Constants.DISCONNECTED;

            }
            else if (@is(SLEEP))
            {
                if (startRecovery)
                {
                    return Constants.TRYING_RECOVERY;
                }
                else
                {
                    return Constants.WILL_RETRY;
                }

            }
            else if (@is(CREATING))
            {
                if (recoveryBean.Recovery)
                {
                    return Constants.TRYING_RECOVERY;
                }
                else
                {
                    return Constants.CONNECTING;
                }

            }
            else if (@is(CREATED) || @is(FIRST_PAUSE) || @is(FIRST_BINDING))
            {
                return Constants.CONNECTED + this.FirstConnectedStatus;

            }
            else if (@is(STALLED))
            {
                return Constants.STALLED;

                /*} else if (is(RECEIVING) && (this.switchRequired || this.slowRequired)) {
                  return Constants.CONNECTED + Constants.SENSE;

                  this would avoid the SENSE->STREAMING->POLLING case but introduces the
                  STREAMING->STALLED->SENSE->POLLING one (problem is the client would be receiving data while in SENSE status)

                  */

            }
            else
            { //BINDING RECEIVING STALLING PAUSE
                return Constants.CONNECTED + this.ConnectedHighLevelStatus;
            }
        }

        protected internal abstract string ConnectedHighLevelStatus { get; }
        protected internal abstract string FirstConnectedStatus { get; }

        protected internal virtual void handleReverseHeartbeat(bool force)
        {
            this.protocol.handleReverseHeartbeat();
        }

        protected internal abstract bool shouldAskContentLength();

        internal virtual bool Open
        {
            get
            {
                return isNot(OFF) && isNot(CREATING) && isNot(SLEEP);
            }
        }

        internal virtual bool StreamingSession
        {
            get
            {
                return !this.isPolling;
            }
        }

        internal virtual string PushServerAddress
        {
            get
            {
                // use the control-link address if available, otherwise use the address configured at startup 
                return string.ReferenceEquals(sessionServerAddress, null) ? serverAddressCache : sessionServerAddress;
            }
        }

        public virtual string SessionId
        {
            get
            {
                return string.ReferenceEquals(sessionId, null) ? "" : sessionId;
            }
            set
            {
                this.sessionId = value;
                if (MDC.Enabled)
                {
                    MDC.put("sessionId", SessionId);
                }
            }
        }

        //////////////////////////////////external calls

        protected internal virtual void createSession(string oldSessionId, string reconnectionCause)
        {
            bool openOnServer = isNot(OFF) && isNot(SLEEP) ? OPEN_ON_SERVER : CLOSED_ON_SERVER;

            //JS client here tests the mad timeouts, returns false if it fails, here we always return true

            if (openOnServer == OPEN_ON_SERVER)
            {
                reconnectionCause = !string.ReferenceEquals(reconnectionCause, null) ? reconnectionCause : "";
                this.closeSession("new." + reconnectionCause, OPEN_ON_SERVER, RECOVERY_SCHEDULED);
            }

            this.reset();

            this.details.SessionId = null;
            this.details.ServerSocketName = null;
            this.details.ClientIp = null;
            this.details.ServerInstanceAddress = null;

            this.serverAddressCache = this.details.ServerAddress;

            this.ignoreServerAddressCache = this.options.ServerInstanceAddressIgnored;

            this.options.InternalRealMaxBandwidth = null;

            log.Info("Opening new session ... ");

            bool sent = this.createSessionExecution(this.phaseCount, oldSessionId, reconnectionCause);
            if (sent)
            {
                this.createSent();
            } //else we're offline and we set a timeout to try again in OFFLINE_TIMEOUT millis
        }

        protected internal virtual bool createSessionExecution(int ph, string oldSessionId, string cause)
        {
            if (ph != this.phaseCount)
            {
                return false;
            }


            string server = this.PushServerAddress;

            if (this.offlineCheck.shouldDelay(server))
            {
                log.Info("Client is offline, delaying connection to server");

                this.thread.schedule(new Task(() =>
               {
                   createSessionExecution(ph, oldSessionId, "offline");
               }), this.offlineCheck.Delay);

                return false;
            }


            CreateSessionRequest request = new CreateSessionRequest(server, this.isPolling, cause, this.options, this.details, this.slowing.Delay, this.details.Password, oldSessionId);

            this.protocol.sendCreateRequest(request);

            return true;
        }


        protected internal virtual void bindSession(string bindCause)
        {
            //JS client here tests the mad timeouts, returns false if it fails, here we always return true

            this.bindCount++;

            if (isNot(PAUSE) && isNot(FIRST_PAUSE) && isNot(OFF))
            {
                //OFF is valid if we bind to someone else's phase
                log.Error("Unexpected phase during binding of session");
                this.shutdown(GO_TO_OFF);
                return;
            }

            if (@is(OFF))
            {
                //bind someonelse's session

                if (!this.changePhaseType(FIRST_PAUSE))
                {
                    return;
                }

            }

            if (this.isPolling)
            {
                // log.Debug("Binding session");
            }
            else
            {
                // log.Info("Binding session");
            }

            ListenableFuture bindFuture = this.bindSessionExecution(bindCause);
            bindFuture.onFulfilled(new MyRunnableA(this));
        }


        private class MyRunnableA : IRunnable
        {
            private Session session;

            public MyRunnableA(Session session)
            {
                this.session = session;
            }

            public void Run()
            {

                // // Debug.Assert(Assertions.SessionThread);
                session.bindSent();

            }
        }

        protected internal virtual ListenableFuture bindSessionExecution(string bindCause)
        {
            /*if (ph != this.phaseCount) {
              return false; //on the JS client there was the possibility to get this called asynchronously
              //we don't have that case here
            }*/
            BindSessionRequest request = new BindSessionRequest(this.PushServerAddress, this.SessionId, this.isPolling, bindCause, this.options, this.slowing.Delay, this.shouldAskContentLength(), protocol.MaxReverseHeartbeatIntervalMs);

            return this.protocol.sendBindRequest(request);
        }

        protected internal virtual void recoverSession()
        {
            RecoverSessionRequest request = new RecoverSessionRequest(PushServerAddress, SessionId, "network.error", options, slowing.Delay, dataNotificationCount);
            protocol.sendRecoveryRequest(request);
            /*
             * Start a timeout. If the server doesn't answer to the recovery request,
             * the recovery request is sent again.
             */
            createSent();
        }

        protected internal virtual void requestSwitch(int newHPhase, string switchCause, bool forced, bool startRecovery)
        {
            this.handlerPhase = newHPhase;

            if (this.switchRequired)
            {
                //switch already requested!
                return;
            }

            if (log.IsDebugEnabled)
            {
                log.Debug("Switch requested phase=" + phase + " cause=" + switchCause);
            }

            //in case we were waiting a slow-switch we have to override that command
            this.slowRequired = false;

            if (@is(CREATING) || @is(SLEEP) || @is(OFF))
            {
                //Session Machine: during these statuses we do not have a session id,
                //we're switch ready but the switch is not possible
                this.handler.streamSense(this.handlerPhase, switchCause, forced);

            }
            else if (@is(PAUSE) || @is(FIRST_PAUSE))
            {
                this.handler.switchReady(this.handlerPhase, switchCause, forced, startRecovery);

            }
            else
            {
                //Session Machine: during these statuses a control to ask for an immediate loop is sent if switch 
                //or slow are requested
                this.switchRequired = true;
                this.switchForced = forced;
                this.switchCause = switchCause;

                this.sendForceRebind(switchCause);
            }

        }

        protected internal virtual void requestSlow(int newHPhase)
        {
            this.handlerPhase = newHPhase;

            if (this.slowRequired)
            {
                //slow already requested
                return;
            }

            log.Debug("Slow requested");

            if (@is(CREATING) || @is(SLEEP) || @is(OFF))
            {
                log.Error("Unexpected phase during slow handling");
                this.shutdown(GO_TO_OFF);
                return;
            }

            if (@is(PAUSE) || @is(FIRST_PAUSE))
            {
                this.handler.slowReady(this.handlerPhase);

            }
            else
            {
                this.slowRequired = true;
                this.sendForceRebind("slow");
            }
        }

        protected internal virtual void closeSession(string closeReason, bool alreadyClosedOnServer, bool noRecoveryScheduled)
        { 
            closeSession(closeReason, alreadyClosedOnServer, noRecoveryScheduled, false);
        }

        protected void closeSession(String closeReason, bool alreadyClosedOnServer, bool noRecoveryScheduled, bool forceConnectionClose)
        {
            log.Info("Closing session " + closeReason);

            if (this.Open)
            {
                //control link is now obsolete

                if (!alreadyClosedOnServer)
                {
                    this.sendDestroySession(closeReason);
                }

                this.subscriptions.onSessionClose();
                this.messages.onSessionClose();
                this.handlerPhase = this.handler.onSessionClose(this.handlerPhase, noRecoveryScheduled);

                this.details.SessionId = null;
                this.details.ServerSocketName = null;
                this.details.ClientIp = null;
                this.details.ServerInstanceAddress = null;

                this.options.InternalRealMaxBandwidth = null;
            }
            else
            {
                this.subscriptions.onSessionClose();
                this.messages.onSessionClose();
                this.handlerPhase = this.handler.onSessionClose(this.handlerPhase, noRecoveryScheduled);
            }
            this.shutdown(!noRecoveryScheduled, forceConnectionClose);
        }

        protected internal virtual void shutdown(bool goToSleep)
        {
            shutdown(goToSleep, false);
        }

        /// <summary>
        /// can be used from the outside to stop this Session without killing the (server) session
        /// </summary>
        protected internal virtual void shutdown(bool goToSleep, bool forceConnectionClose)
        {
            this.reset();
            this.changePhaseType(goToSleep ? SLEEP : OFF);
            this.protocol.stop(goToSleep, forceConnectionClose); //if we sleep we will still be interested in pending control response
        }

        /////////////////////////////////////////SESSION MACHINE EVENTS 

        protected internal virtual void onTimeout(string timeoutType, int phaseCount, long usedTimeout, string coreCause, bool startRecovery)
        {
            if (phaseCount != this.phaseCount)
            {
                return;
            }

            if (log.IsDebugEnabled)
            {
                log.Debug("Timeout event [" + timeoutType + "] while " + this.phase + " cause=" + coreCause);
            }

            //in case of sleep we lose information in the LS_cause
            string tCause = "timeout." + this.phase + "." + this.bindCount;
            if (@is(SLEEP) && !string.ReferenceEquals(coreCause, null))
            {
                tCause = coreCause;
            }

            if (@is(CREATING))
            {
                long timeLeftMs = recoveryBean.timeLeftMs(options.SessionRecoveryTimeout);
                if (recoveryBean.Recovery && timeLeftMs > 0)
                {
                    /*
                     * POINT OF RECOVERY (1/2):
                     * a previous recovery request has received no response within the established timeout, 
                     * so we send another request in loop.
                     */
                    if (log.IsDebugEnabled)
                    {
                        log.Debug("Start session recovery. Cause: no response timeLeft=" + timeLeftMs);
                    }
                    this.options.increaseConnectTimeout();
                    handler.recoverSession(this.handlerPhase, tCause, this.isForced, this.workedBefore > 0);

                }
                else
                {
                    log.Debug("Start new session. Cause: no response");

                    string sleepCause = "create.timeout";
                    //send to SLEEP
                    this.closeSession(sleepCause, CLOSED_ON_SERVER, RECOVERY_SCHEDULED, true/*forceConnectionClose*/);
                    
                    this.options.increaseConnectTimeout();
                    this.launchTimeout("zeroDelay", 0, sleepCause, false);
                }

            }
            else if (@is(CREATED) || @is(BINDING) || @is(STALLED) || @is(SLEEP))
            {

                if (this.slowRequired || this.switchRequired)
                {

                    log.Debug("Timeout: switch transport");

                    this.handler.streamSense(this.handlerPhase, tCause + ".switch", this.switchForced);
                }
                else if (!this.isPolling || this.isForced)
                {
                    //this.createSession(this.sessionId,tCause); //THIS is bad, because it forces us to reuse stuff
                    if (startRecovery)
                    {
                        /*
                         * POINT OF RECOVERY (2/2):
                         * 
                         * This point is reached 
                         * 1) after the method onErrorEvent has detected a socket failure,
                         *    set the phase to SLEEP and scheduled the onTimeout task; or
                         * 2) when the session is STALLED because the client doesn't receive any data from the server
                         *    (see method timeoutForReconnect)
                         */
                        this.handler.recoverSession(this.handlerPhase, tCause, this.isForced, this.workedBefore > 0);
                    }
                    else
                    {
                        log.Debug("Timeout: new session");

                        this.handler.retry(this.handlerPhase, tCause, this.isForced, this.workedBefore > 0);
                    }
                }
                else
                {
                    /*
                     * Branch reserved for polling.
                     * 
                     * NOTE 
                     * In the past, when an error occurred during polling, the new session was created not in polling
                     * but in streaming (probably because polling was seen as sub-optimal transport).
                     * With the introduction of the recovery, we are faced with 3 options:
                     * 1) recovering the session in polling
                     * 2) recovering the session in streaming
                     * 3) creating a new session in streaming.
                     * The second option is probably the best one, but, since the client falls-back rarely to polling,
                     * in order to ease the implementation, I have decided to follow the third path.
                     */
                    log.Debug(startRecovery ? "Timeout: switch transport from polling (ignore recovery)" : "Timeout: switch transport from polling");

                    this.handler.streamSense(this.handlerPhase, tCause, false);
                }

            }
            else if (@is(FIRST_BINDING))
            {

                if (this.slowRequired || this.switchRequired)
                {
                    this.handler.streamSense(this.handlerPhase, tCause + ".switch", this.switchForced);
                }
                else if (this.workedBefore > 0 || this.isForced || this.retryAgainIfStreamFails)
                {
                    this.handler.retry(this.handlerPhase, tCause, this.isForced, this.workedBefore > 0);
                    //this.createSession(this.sessionId,tCause); //THIS is bad, because it forces us to reuse stuff
                }
                else if (this.createNewOnFirstBindTimeout())
                {
                    this.handler.streamSense(this.handlerPhase, tCause + ".switch", this.switchForced);
                }
                else
                {
                    //NOTE: the JS implementation is different because it has onSessionGivesUp
                    //calls based on not-working connections (i.e.: browser does not support WebSockets)
                    this.handler.streamSenseSwitch(this.handlerPhase, tCause, phase, recoveryBean.Recovery);
                }

            }
            else if (@is(PAUSE))
            {
                if (this.isPolling)
                {
                    this.slowing.testPollSync(usedTimeout, ( DateTime.Now ).Ticks);
                }
                this.bindSession("loop");

            }
            else if (@is(FIRST_PAUSE))
            {
                if (switchToWebSocket)
                {
                    /*
                     * We must bind the session returned by
                     * create_session command, but firstly
                     * we must change the transport from
                     * HTTP to WebSocket.
                     */
                    handler.switchToWebSocket(recoveryBean.Recovery);
                    switchToWebSocket = false; // reset the flag
                }
                else
                {
                    this.bindSession("loop1");
                }

            }
            else if (@is(RECEIVING))
            {
                this.timeoutForStalled();

            }
            else if (@is(STALLING))
            {
                this.timeoutForReconnect();

            }
            else
            { //_OFF
                log.Error("Unexpected timeout event while session is OFF");
                this.shutdown(GO_TO_OFF);
            }
        }

        private bool createNewOnFirstBindTimeout()
        {
            return this.isPolling;
        }

        ////////////////////////////////////////////////////////actions

        protected internal virtual void createSent()
        {
            this.sentTime = ( DateTime.Now ).Ticks;

            if (isNot(OFF) && isNot(SLEEP))
            {
                log.Error("Unexpected phase after create request sent: " + this.phase);
                this.shutdown(GO_TO_OFF);
                return;
            }
            if (!this.changePhaseType(CREATING))
            {
                return;
            }

            //will be executed if create does not return, no need to specify the cause
            this.launchTimeout("currentConnectTimeout", this.options.CurrentConnectTimeout, null, false);
        }

        protected internal virtual void bindSent()
        {

            this.sentTime = DateTimeHelper.CurrentUnixTimeMillis();

            if (isNot(PAUSE) && isNot(FIRST_PAUSE))
            {
                log.Error("Unexpected phase after bind request sent: " + this.phase);
                this.shutdown(GO_TO_OFF);
                return;
            }
            if (!this.changePhaseType(@is(PAUSE) ? BINDING : FIRST_BINDING))
            {
                return;
            }

            this.launchTimeout("bindTimeout", this.BindTimeout, null, false); //will be executed if the bind does not return no need to specify the cause

        }

        //////////////////////timeouts   

        internal virtual CancellationTokenSource launchTimeout(string timeoutType, long pauseToUse, string cause, bool startRecovery)
        {
            int pc = this.phaseCount;

            if (log.IsDebugEnabled)
            {
                log.Debug("Status timeout in " + pauseToUse + " [" + timeoutType + "] due to " + cause);
            }

            return this.thread.schedule(new Task(() =>
           {

               //XXX instead of checking the phase we might cancel pending tasks when phaseCount changes
               if (pc != phaseCount)
               {
                   return;
               }

               onTimeout(timeoutType, pc, pauseToUse, cause, startRecovery);

           }), pauseToUse + 50);

        }

        private void timeoutForStalling()
        {
            if (this.options.KeepaliveInterval > 0)
            {

                //there is probably a timeout already scheduled that has no need to execute, 
                //we cancel it
                if (lastKATask != null && !lastKATask.IsCancellationRequested)
                {
                    lastKATask.Cancel();

                }

                //we won't reconnect if this executes (we go to STALLING), so no need to add a cause
                lastKATask = this.launchTimeout("keepaliveInterval", this.options.KeepaliveInterval, null, false);

            }
        }

        private void timeoutForStalled()
        {
            if (!this.changePhaseType(STALLING))
            {
                return;
            }
            //we won't reconnect if this executes (we go to STALLED), so no need to add a cause
            this.launchTimeout("stalledTimeout", this.options.StalledTimeout + 500, null, false);
        }

        private void timeoutForReconnect()
        {
            if (!this.changePhaseType(STALLED))
            {
                return;
            }
            long timeLeftMs = recoveryBean.timeLeftMs(options.SessionRecoveryTimeout);
            bool startRecovery = timeLeftMs > 0;
            //the onTimeout already knows the cause for this because we're STALLED
            this.launchTimeout("reconnectTimeout", this.options.ReconnectTimeout, null, startRecovery);
        }

        private void timeoutForExecution()
        {
            //we won't reconnect if this executes, so no need to add a cause
            try
            {
                this.launchTimeout("executionTimeout", this.options.StalledTimeout, null, false);
            }
            catch (Exception e)
            {
                log.Warn("Something went wrong: " + e.Message);
            }
            log.Debug("Check Point 1a120ak.");
        }

        private long BindTimeout
        {
            get
            {
                if (this.isPolling)
                {
                    return this.options.CurrentConnectTimeout + this.options.IdleTimeout;
                }
                else
                {
                    return this.workedBefore > 0 && this.reconnectTimeout > 0 ? this.reconnectTimeout : this.options.CurrentConnectTimeout;
                }
            }
        }

        private long RealPollingInterval
        {
            get
            {
                if (@is(FIRST_PAUSE))
                {
                    return this.options.PollingInterval;

                }
                else
                {

                    long spent = DateTimeHelper.CurrentUnixTimeMillis() - this.sentTime;
                    return spent > this.options.PollingInterval ? 0 : this.options.PollingInterval - spent;
                }
            }
        }


        private long calculateRetryDelay()
        {
            long spent = ( DateTime.Now ).Ticks - this.sentTime;
            long currentRetryDelay = options.CurrentRetryDelay;
            return spent > currentRetryDelay ? 0 : currentRetryDelay - spent;
        }

        //////////////////////////Requests to protocol  

        private void sendForceRebind(string rebindCause)
        {
            log.Info("Sending request to the server to force a rebind on the current connection during " + this.phase);

            ForceRebindRequest request = new ForceRebindRequest(this.PushServerAddress, this.sessionId, rebindCause, this.slowing.Delay);
            ForceRebindTutor tutor = new ForceRebindTutor(this, this.phaseCount, rebindCause);

            this.protocol.sendForceRebind(request, tutor);
        }

        private void sendDestroySession(string closeReason)
        {
            log.Info("Sending request to the server to destroy the current session during " + this.phase);

            DestroyRequest request = new DestroyRequest(this.PushServerAddress, this.sessionId, closeReason);
            this.protocol.sendDestroy(request, new VoidTutor(thread, options));

            //we do not retry destroy requests: just fire and forget    
        }

        internal virtual void sendMessage(MessageRequest request, RequestTutor tutor)
        {
            request.Server = this.PushServerAddress;
            request.Session = this.sessionId;
            this.protocol.sendMessageRequest(request, tutor);
        }

        public virtual void sendSubscription(SubscribeRequest request, RequestTutor tutor)
        {
            request.Server = this.PushServerAddress;
            request.Session = this.sessionId;
            this.protocol.sendSubscriptionRequest(request, tutor);
        }

        public virtual void sendUnsubscription(UnsubscribeRequest request, RequestTutor tutor)
        {
            request.Server = this.PushServerAddress;
            request.Session = this.sessionId;
            this.protocol.sendUnsubscriptionRequest(request, tutor);
        }

        public virtual void sendSubscriptionChange(ChangeSubscriptionRequest request, RequestTutor tutor)
        {
            request.Server = this.PushServerAddress;
            request.Session = this.sessionId;
            this.protocol.sendConfigurationRequest(request, tutor);
        }

        public virtual void sendReverseHeartbeat(ReverseHeartbeatRequest request, RequestTutor tutor)
        {
            request.Server = this.PushServerAddress;
            request.Session = this.sessionId;
            this.protocol.sendReverseHeartbeat(request, tutor);
        }

        /// <summary>
        /// Send a bandwidth request to the transport layer.
        /// </summary>
        /// <param name="timeoutMs"> </param>
        /// <param name="clientRequest"> If the request is a retransmission, {@code clientRequest} is the original
        /// client request. If the request is a client request, {@code clientRequest} is null. </param>
        internal virtual void sendConstrain(long timeoutMs, ConstrainRequest clientRequest)
        {
            if (@is(OFF) || @is(SLEEP))
            {
                return;
            }
            else if (options.BandwidthUnmanaged)
            {
                // if the bandwidth is unmanaged, it is useless to try to change it
                return;
            }
            else if (@is(CREATING))
            {
                //too late to send it via create_session
                //too early to send it via control
                this.cachedRequiredBW = true;
                return;
            }

            ConstrainRequest request = new ConstrainRequest(this.options.InternalMaxBandwidth, clientRequest);
            request.Session = this.sessionId;
            ConstrainTutor tutor = new ConstrainTutor(timeoutMs, request, thread, options);
            request.Server = this.PushServerAddress;
            if (bwRetransmissionMonitor.canSend(request))
            {
                this.protocol.sendConstrainRequest(request, tutor);
            }
        }

        /// <summary>
        /// Closes the session and notifies the error to <seealso cref="ClientListener"/>.
        /// </summary>
        public virtual void notifyServerError(int errorCode, string errorMessage)
        {
            closeSession("end", CLOSED_ON_SERVER, NO_RECOVERY_SCHEDULED);
            handler.onServerError(errorCode, errorMessage);
        }

        private readonly BandwidthRetransmissionMonitor bwRetransmissionMonitor = new BandwidthRetransmissionMonitor();

        /// <summary>
        /// The monitor handles, together with <seealso cref="ConstrainTutor"/>, the bandwidth requests made by the client 
        /// and the retransmissions due to expiration of timeouts.
        /// <para>
        /// Retransmissions are regulated by these rules:
        /// <ol>
        /// <li>it is forbidden to retransmit a request when a more recent request has been (re)transmitted</li>
        /// <li>it is forbidden to retransmit a request when a response to a more recent request has been received</li>
        /// </ol>
        /// NB We say that a request is more recent than another if it was issued later by the client
        /// (i.e. it has a greater value of <seealso cref="ConstrainRequest#getClientRequestId()"/>).
        /// </para>
        /// <para>
        /// The rules above ensures that when two bandwidth requests are successively issued, it is not possible
        /// that the older one "overrides" and cancels out the effect of the newer one.
        /// </para>
        /// </summary>
        internal class BandwidthRetransmissionMonitor
        {
            /// <summary>
            /// ClientRequestId of the more recent request which received a response.
            /// </summary>
            internal long lastReceivedRequestId = -1;
            /// <summary>
            /// ClientRequestId of the more recent request which was (re)transmitted.
            /// </summary>
            internal long lastPendingRequestId = -1;

            /// <summary>
            /// Must be checked before sending a request to the transportation layer.
            /// It ensures that when two bandwidth requests are successively issued, it is not possible
            /// that the older one "overrides" and cancels out the effect of the newer one.
            /// </summary>
            internal virtual bool canSend(ConstrainRequest request)
            {
                lock (this)
                {
                    long clientId = request.ClientRequestId;
                    bool forbidden = ( clientId < lastPendingRequestId || clientId <= lastReceivedRequestId );
                    if (!forbidden)
                    {
                        lastPendingRequestId = clientId;
                    }
                    return !forbidden;
                }
            }

            /// <summary>
            /// Must be checked after receiving a REQOK/REQERR/ERROR from a bandwidth request.
            /// It ensures that when two bandwidth requests are successively issued, it is not possible
            /// that the older one "overrides" and cancels out the effect of the newer one.
            /// </summary>
            internal virtual void onReceivedResponse(ConstrainRequest request)
            {
                lock (this)
                {
                    long clientId = request.ClientRequestId;
                    if (clientId > lastReceivedRequestId)
                    {
                        lastReceivedRequestId = clientId;
                    }
                }
            }
        }

        public class TextProtocolListener : ProtocolListener
        {
            private readonly Session outerInstance;

            public TextProtocolListener(Session outerInstance)
            {
                this.outerInstance = outerInstance;
            }


            public virtual void onInterrupted(bool wsError, bool unableToOpen)
            {
                /*
                 * An interruption triggers an attempt to recover the session.
                 * The sequence of actions is roughly as follow:
                 * - StreamListener.onClosed
                 * - Session.onInterrupted
                 * - Session.onErrorEvent
                 * - Session.launchTimeout
                 * - Session.onTimeout
                 * - SessionManager.recoverSession (creates a new session object inheriting from this session)
                 * - Session.recoverSession
                 * - TextProtocol.protocol.sendRecoveryRequest
                 */
                onErrorEvent("network.error", false, unableToOpen, true, wsError);
            }

            public virtual void onConstrainResponse(ConstrainTutor tutor)
            {
                outerInstance.bwRetransmissionMonitor.onReceivedResponse(tutor.Request);
            }

            public virtual void onServerSentBandwidth(string maxBandwidth)
            {
                // onEvent();

                if (maxBandwidth.Equals("unmanaged", StringComparison.OrdinalIgnoreCase))
                {
                    outerInstance.options.BandwidthUnmanaged = true;
                    maxBandwidth = "unlimited";
                }
                outerInstance.options.InternalRealMaxBandwidth = maxBandwidth;
            }

            public virtual void onTakeover(int specificCode)
            {
                onErrorEvent("error" + specificCode, CLOSED_ON_SERVER, false, false, false);
            }

            public virtual void onKeepalive()
            {
                onEvent();
            }

            public virtual void onOKReceived(string newSession, string controlLink, long requestLimitLength, long keepaliveIntervalDefault)
            {
                outerInstance.log.Debug("OK event while " + outerInstance.phase);

                if (outerInstance.isNot(CREATING) && outerInstance.isNot(FIRST_BINDING) && outerInstance.isNot(BINDING))
                {
                    outerInstance.log.Error("Unexpected OK event while session is in status: " + outerInstance.phase);
                    outerInstance.shutdown(GO_TO_OFF);
                    return;
                }

                string lastUsedAddress = outerInstance.PushServerAddress;
                string addressToUse = lastUsedAddress;
                if (!string.ReferenceEquals(controlLink, null) && !outerInstance.ignoreServerAddressCache)
                {
                    controlLink = RequestsHelper.completeControlLink(addressToUse, controlLink);
                    addressToUse = controlLink;
                }
                outerInstance.sessionServerAddress = addressToUse;

                outerInstance.log.Debug("Address to use after create: " + outerInstance.sessionServerAddress);


                if (!lastUsedAddress.Equals(outerInstance.sessionServerAddress))
                {
                    if (outerInstance.@is(CREATING))
                    {
                        /*
                         * Close the WebSocket open because of wsEarlyOpen flag and 
                         * open a new WebSocket using the given control-link.
                         * 
                         * NB This operation affects only create_session requests.
                         * Bind_session requests ignore the control-link.
                         */
                        if (outerInstance.log.IsDebugEnabled)
                        {
                            outerInstance.log.Debug("Control-Link has changed: " + lastUsedAddress + " -> " + outerInstance.sessionServerAddress);
                        }
                        outerInstance.changeControlLink(outerInstance.sessionServerAddress);
                    }
                }

                if (keepaliveIntervalDefault > 0)
                {
                    if (outerInstance.isPolling)
                    {
                        //on polling sessions the longest inactivity permitted is sent instead of the keepalive setting
                        outerInstance.options.IdleTimeout = keepaliveIntervalDefault;
                    }
                    else
                    {
                        outerInstance.options.KeepaliveInterval = keepaliveIntervalDefault;
                    }
                }

                if (outerInstance.@is(CREATING))
                {
                    //New session!
                    if (!string.ReferenceEquals(outerInstance.sessionId, null) && !( outerInstance.sessionId.Equals(newSession) ))
                    {
                        // nothing can be trusted here
                        outerInstance.log.Debug("Unexpected session " + outerInstance.sessionId + " found while initializing " + newSession);
                        outerInstance.reset();
                    }
                    outerInstance.SessionId = newSession;

                }
                else
                {
                    if (!outerInstance.sessionId.Equals(newSession))
                    {
                        outerInstance.log.Error("Bound unexpected session: " + newSession + " (was waiting for " + outerInstance.sessionId + ")");
                        outerInstance.shutdown(GO_TO_OFF);
                        return;
                    }
                    /* calculate reconnect timeout, i.e. the actual time we spent to send the request and receive the reponse (the roundtirp) */
                    long spentTime = ( DateTime.Now ).Ticks - outerInstance.sentTime;
                    //we add to our connectTimeout the spent roundtrip and we'll use that time as next connectCheckTimeout
                    //ok, we wanna give enough time to the client to connect if necessary, but we should not exaggerate :)
                    //[obviously if spentTime can't be > this.policyBean.connectTimeout after the first connection, 
                    //but it may grow connection after connection if we give him too much time]
                    long ct = outerInstance.options.CurrentConnectTimeout;
                    outerInstance.reconnectTimeout = ( spentTime > ct ? ct : spentTime ) + ct;
                }

                outerInstance.slowing.startSync(outerInstance.isPolling, outerInstance.isForced, ( DateTime.Now ).Ticks);
                onEvent();

                if (outerInstance.@is(CREATED))
                {

                    if (outerInstance.recoveryBean.Recovery)
                    {
                        /* 
                         * branch reserved for recovery responses 
                         * (i.e. bind_session requests with LS_recovery_from parameter)
                         */
                        outerInstance.recoveryBean.restoreTimeLeft();

                    }
                    else
                    {
                        /* 
                         * branch reserved for create_session responses 
                         */

                        outerInstance.handler.onSessionStart();

                        outerInstance.subscriptions.onSessionStart();

                        outerInstance.messages.onSessionStart();

                        outerInstance.details.SessionId = newSession;

                        outerInstance.details.ServerInstanceAddress = outerInstance.sessionServerAddress;

                        if (outerInstance.cachedRequiredBW)
                        {
                            outerInstance.sendConstrain(0, null);
                            outerInstance.cachedRequiredBW = false;
                        }
                    }

                }
                else
                {
                    /* 
                     * branch reserved for bind_session responses (recovery responses excluded) 
                     */
                    outerInstance.handler.onSessionBound();
                    outerInstance.options.resetConnectTimeout();
                    outerInstance.protocol.DefaultSessionId = newSession;
                }

            }

            public virtual void onLoopReceived(long serverSentPause)
            {

                if (outerInstance.@is(RECEIVING) || outerInstance.@is(STALLING) || outerInstance.@is(STALLED) || outerInstance.@is(CREATED))
                {
                    if (outerInstance.switchRequired)
                    {
                        outerInstance.handler.switchReady(outerInstance.handlerPhase, outerInstance.switchCause, outerInstance.switchForced, false);
                    }
                    else if (outerInstance.slowRequired)
                    {
                        outerInstance.handler.slowReady(outerInstance.handlerPhase);
                    }
                    else
                    {
                        doPause(serverSentPause);
                    }
                }
                else
                {
                    outerInstance.log.Error("Unexpected loop event while session is an non-active status: " + outerInstance.phase);
                    outerInstance.shutdown(GO_TO_OFF);
                }
            }

            public virtual void onSyncError(bool async)
            {
                string cause = async ? "syncerror" : "control.syncerror";
                onErrorEvent(cause, true, false, false, false);
            }

            public virtual void onRecoveryError()
            {
                // adapted from method onSyncError
                onErrorEvent("recovery.error", true, false, false, false);
            }

            public virtual void onExpiry()
            {
                onErrorEvent("expired", true, false, false, false);
            }

            public virtual void onUpdateReceived(int subscriptionId, int item, List<string> args)
            {
                onEvent();
                outerInstance.subscriptions.onUpdateReceived(subscriptionId, item, args);
            }

            public virtual void onEndOfSnapshotEvent(int subscriptionId, int item)
            {
                onEvent();
                outerInstance.subscriptions.onEndOfSnapshotEvent(subscriptionId, item);
            }

            public virtual void onClearSnapshotEvent(int subscriptionId, int item)
            {
                onEvent();
                outerInstance.subscriptions.onClearSnapshotEvent(subscriptionId, item);
            }

            public virtual void onLostUpdatesEvent(int subscriptionId, int item, int lost)
            {
                onEvent();
                outerInstance.subscriptions.onLostUpdatesEvent(subscriptionId, item, lost);
            }

            public virtual void onConfigurationEvent(int subscriptionId, string frequency)
            {
                onEvent();
                outerInstance.subscriptions.onConfigurationEvent(subscriptionId, frequency);
            }

            public virtual void onMessageAck(string sequence, int number, bool async)
            {
                if (async)
                {
                    onEvent();
                }
                outerInstance.messages.onMessageAck(sequence, number);
            }

            public virtual void onMessageOk(string sequence, int number)
            {
                onEvent();
                outerInstance.messages.onMessageOk(sequence, number);
            }

            public virtual void onMessageDeny(string sequence, int denyCode, string denyMessage, int number, bool async)
            {
                if (async)
                {
                    onEvent();
                }
                outerInstance.messages.onMessageDeny(sequence, denyCode, denyMessage, number);
            }

            public virtual void onMessageDiscarded(string sequence, int number, bool async)
            {
                if (async)
                {
                    onEvent();
                }
                outerInstance.messages.onMessageDiscarded(sequence, number);
            }

            public virtual void onMessageError(string sequence, int errorCode, string errorMessage, int number, bool async)
            {
                if (async)
                {
                    onEvent();
                }
                outerInstance.messages.onMessageError(sequence, errorCode, errorMessage, number);
            }

            public virtual void onSubscriptionError(int subscriptionId, int errorCode, string errorMessage, bool async)
            {
                if (async)
                {
                    onEvent();
                }
                outerInstance.subscriptions.onSubscriptionError(subscriptionId, errorCode, errorMessage);
            }

            public virtual void onServerError(int errorCode, string errorMessage)
            {
                outerInstance.notifyServerError(errorCode, errorMessage);
            }

            public virtual void onUnsubscriptionAck(int subscriptionId)
            {
                onEvent();
                outerInstance.subscriptions.onUnsubscriptionAck(subscriptionId);
            }

            public virtual void onUnsubscription(int subscriptionId)
            {
                onEvent();
                outerInstance.subscriptions.onUnsubscription(subscriptionId);
            }

            public virtual void onSubscriptionAck(int subscriptionId)
            {
                outerInstance.subscriptions.onSubscriptionAck(subscriptionId);
            }

            public virtual void onSubscription(int subscriptionId, int totalItems, int totalFields, int keyPosition, int commandPosition)
            {
                onEvent();
                outerInstance.subscriptions.onSubscription(subscriptionId, totalItems, totalFields, keyPosition, commandPosition);
            }

            public virtual void onSubscriptionReconf(int subscriptionId, long reconfId, bool async)
            {
                if (async)
                {
                    onEvent();
                }
                outerInstance.subscriptions.onSubscription(subscriptionId, reconfId);
            }

            public virtual void onSyncMessage(long seconds)
            {
                onEvent();

                if (outerInstance.log.IsDebugEnabled)
                {
                    outerInstance.log.Debug("Sync event while " + outerInstance.phase);
                }

                bool syncOk = outerInstance.slowing.syncCheck(seconds, !outerInstance.isPolling, (double)( ( DateTime.Now ).Ticks / TimeSpan.TicksPerMillisecond ));
                if (syncOk)
                {
                    if (outerInstance.@is(RECEIVING))
                    {
                        //with XHRStreamingConnection we've seen cases (e.g.: Chrome on Android 2.2 / Opera 10.64 on Kubuntu)
                        //where the first part of the connection is sent as expected while its continuation is not sent at all (Opera)
                        //or is sent in blocks (Chrome) so we wait for the sync method before remembering that the streaming actually works
                        outerInstance.workedBefore = PERMISSION_TO_FAIL; //XXX this will only correclty work with future servers
                    }
                }
                else
                {
                    //we're late, let's fix the issue
                    //if already slowing or switching I should not ask to slow again.
                    if (outerInstance.switchRequired || outerInstance.slowRequired)
                    {
                        //this Session is already changing, we do not act
                        return;
                    }

                    outerInstance.handler.onSlowRequired(outerInstance.handlerPhase, outerInstance.slowing.Delay);
                }
            }

            public virtual void onServerName(string serverName)
            {
                outerInstance.details.ServerSocketName = serverName;
            }

            public virtual void onClientIp(string clientIp)
            {
                outerInstance.details.ClientIp = clientIp;
                outerInstance.handler.onIPReceived(clientIp);
            }

            internal virtual void onEvent()
            {
                if (outerInstance.log.IsDebugEnabled)
                {
                    outerInstance.log.Debug("Data event while " + outerInstance.phase);
                }

                if (outerInstance.@is(CREATING))
                {
                    if (!outerInstance.changePhaseType(CREATED))
                    {
                        return;
                    }
                    outerInstance.timeoutForExecution();

                }
                else if (outerInstance.@is(CREATED))
                {
                    //stay created

                }
                else if (outerInstance.@is(FIRST_BINDING))
                {
                    if (!outerInstance.changePhaseType(RECEIVING))
                    {
                        return;
                    }
                    outerInstance.offlineCheck.resetMaybeOnline();
                    outerInstance.timeoutForStalling();

                }
                else if (outerInstance.@is(BINDING) || outerInstance.@is(STALLING) || outerInstance.@is(STALLED) || outerInstance.@is(RECEIVING))
                {
                    if (!outerInstance.changePhaseType(RECEIVING))
                    {
                        return;
                    }

                    outerInstance.timeoutForStalling();

                }
                else
                { //FIRST_PAUSE PAUSE SLEEP _OFF
                    outerInstance.log.Error("Unexpected push event while session is an non-active status: " + outerInstance.phase);
                    outerInstance.shutdown(GO_TO_OFF);
                }
            }

            /// <param name="tryRecovery"> the flag is true when the method is called from onInterrupted </param>
            /// <param name="wsError"> unable to open WS </param>
            internal virtual void onErrorEvent(string reason, bool closedOnServer, bool unableToOpen, bool tryRecovery, bool wsError)
            {
                long timeLeftMs = outerInstance.recoveryBean.timeLeftMs(outerInstance.options.SessionRecoveryTimeout);
                if (outerInstance.@is(OFF))
                {
                    return;
                }
                else
                {
                    outerInstance.log.Error("Error event while " + outerInstance.phase + " reason: " + reason + " tryRecovery=" + tryRecovery + " timeLeft=" + timeLeftMs + " closedOnServer=" + closedOnServer + " unableToOpen=" + unableToOpen + " wsError=" + wsError);
                    bool startRecovery = tryRecovery && timeLeftMs > 0;

                    outerInstance.doOnErrorEvent(reason, closedOnServer, unableToOpen, startRecovery, timeLeftMs, wsError);
                }
                
            }

            internal virtual void doPause(long serverSentPause)
            {

                if (!outerInstance.changePhaseType(outerInstance.@is(CREATED) ? FIRST_PAUSE : PAUSE))
                {
                    return;
                }

                long pauseToUse = serverSentPause;
                if (outerInstance.isPolling && outerInstance.isNot(FIRST_PAUSE))
                {
                    /* 
                     * Pausing after a poll cycle.
                     * 
                     * The check on the state is needed to distinguish create_session requests
                     * (characterized by having FIRST_PAUSE as state) which must ignore polling interval
                     * and bind_session requests (with state different from FIRST_PAUSE) 
                     * which use polling interval.
                     */

                    if (serverSentPause >= outerInstance.options.PollingInterval)
                    {
                        // we're likely delaying because of the slowing algorithm
                        // nothing to do
                    }
                    else
                    {
                        //the server didn't like our request, let's adapt 
                        outerInstance.options.PollingInterval = serverSentPause;
                    }

                    pauseToUse = outerInstance.RealPollingInterval;

                }

                if (outerInstance.isNot(FIRST_PAUSE) && pauseToUse > 0)
                {
                    outerInstance.launchTimeout("pause", pauseToUse, null, false);
                }
                else
                {
                    outerInstance.onTimeout("noPause", outerInstance.phaseCount, 0, null, false);
                }
            }

            public virtual void onMpnRegisterOK(string deviceId, string adapterName)
            {
                onEvent();
                outerInstance.handler.onMpnRegisterOK(deviceId, adapterName);
            }

            public virtual void onMpnRegisterError(int code, string message)
            {
                outerInstance.handler.onMpnRegisterError(code, message);
            }

            public virtual void onMpnSubscribeOK(string lsSubId, string pnSubId)
            {
                outerInstance.handler.onMpnSubscribeOK(lsSubId, pnSubId);
            }

            public virtual void onMpnSubscribeError(string subId, int code, string message)
            {
                outerInstance.handler.onMpnSubscribeError(subId, code, message);
            }

            public virtual void onMpnUnsubscribeError(string subId, int code, string message)
            {
                outerInstance.handler.onMpnUnsubscribeError(subId, code, message);
            }

            public virtual void onMpnUnsubscribeOK(string subId)
            {
                outerInstance.handler.onMpnUnsubscribeOK(subId);
            }

            public virtual void onMpnResetBadgeOK(string deviceId)
            {
                outerInstance.handler.onMpnResetBadgeOK(deviceId);
            }

            public virtual void onMpnBadgeResetError(int code, string message)
            {
                outerInstance.handler.onMpnBadgeResetError(code, message);
            }

            public virtual long DataNotificationProg
            {
                get
                {
                    return outerInstance.dataNotificationCount;
                }
            }

            public virtual void onDataNotification()
            {
                outerInstance.dataNotificationCount++;
            }
        }


        public class ForceRebindTutor : RequestTutor
        {
            private readonly Session outerInstance;


            internal readonly int currentPhase;
            internal readonly string cause;

            internal ForceRebindTutor(Session outerInstance, int currentPhase, string cause) : base(outerInstance.thread, outerInstance.options)
            {
                this.outerInstance = outerInstance;

                this.currentPhase = currentPhase;
                this.cause = cause;
            }

            protected internal override bool verifySuccess()
            {
                return this.currentPhase != outerInstance.phaseCount;
            }

            protected internal override void doRecovery()
            {
                outerInstance.sendForceRebind(this.cause);
            }

            public override void notifyAbort()
            {
                //nothing to do
            }

            protected internal override bool TimeoutFixed
            {
                get
                {
                    return true;
                }
            }

            protected internal override long FixedTimeout
            {
                get
                {
                    return this.connectionOptions.ForceBindTimeout;
                }
            }

            public override bool shouldBeSent()
            {
                return this.currentPhase == outerInstance.phaseCount;
            }

        }

        public class ConstrainTutor : RequestTutor
        {

            internal readonly ConstrainRequest request;

            public ConstrainTutor(long timeoutMs, ConstrainRequest request, SessionThread sessionThread, InternalConnectionOptions options) : base(timeoutMs, sessionThread, options, false)
            {
                this.request = request;
            }

            protected internal override bool verifySuccess()
            {
                return false; // NB the real check is made inside the method Session.changeBandwidth
            }

            protected internal override void doRecovery()
            {
                Session session = sessionThread.SessionManager.Session;
                if (session != null)
                {
                    session.sendConstrain(this.timeoutMs, request);
                }
            }

            public override void notifyAbort()
            {
                // nothing to do
            }

            protected internal override bool TimeoutFixed
            {
                get
                {
                    return false;
                }
            }

            protected internal override long FixedTimeout
            {
                get
                {
                    return 0;
                }
            }

            public override bool shouldBeSent()
            {
                return true;
            }

            public virtual ConstrainRequest Request
            {
                get
                {
                    return request;
                }
            }

        }


        /// <summary>
        /// This method is called by <seealso cref="SessionManager"/> to notify the session that WebSocket support has been enabled again
        /// because the client IP has changed. So next bind_session must try WebSocket as transport 
        /// (except in the case of forced transport).
        /// </summary>
        public virtual void restoreWebSocket()
        {
            if (string.ReferenceEquals(options.ForcedTransport, null))
            {
                switchToWebSocket = true;
            }
            else
            {
                /*
                 * If the transport is forced, it is either HTTP or WebSocket.
                 * If it is HTTP, we must not switch to WebSocket. So the flag must remain false.
                 * If it is WebSocket, the switch is useless. So the flag must remain false.
                 * In either case, we don't need to change it.
                 */
            }
        }

        public virtual void onFatalError(Exception e)
        {
            log.Error("A fatal error has occurred. The session will be closed. Cause: " + e);
            protocol.onFatalError(e);
        }

        protected internal virtual void doOnErrorEvent(string reason, bool closedOnServer, bool unableToOpen, bool startRecovery, long timeLeftMs, bool wsError)
        {

            log.Debug("Error event for " + reason + " while " + phase );

            if (@is(RECEIVING) || @is(STALLED) || @is(STALLING) || @is(BINDING) || @is(PAUSE))
            {
                if (startRecovery)
                {
                    /*
                     * POINT OF RECOVERY (1/2):
                     * the socket failure has happened while we were receiving data.
                     * 
                     * To recover the session after a socket failure, set the phase to SLEEP 
                     * and schedule the onTimeout task (see launchTimeout below), 
                     * where the recovery will be performed.
                     */

                    if (log.IsDebugEnabled)
                    {
                        log.Debug("Start session recovery. Cause: socket failure while receiving");
                    }

                    changePhaseType(SLEEP, startRecovery);

                }
                else
                {
                    closeSession(reason, closedOnServer, RECOVERY_SCHEDULED);
                }
                Debug.Assert(@is(SLEEP));
                //we used to retry immediately here, now we wait a random time <= firstRetryMaxDelay ms
                long pause = (long)Math.Round(GlobalRandom.NextDouble * options.FirstRetryMaxDelay, MidpointRounding.AwayFromZero);
                launchTimeout("firstRetryMaxDelay", pause, reason, startRecovery);

            }
            else if (@is(CREATING) || @is(CREATED) || @is(FIRST_BINDING))
            {
                if (recoveryBean.Recovery && timeLeftMs > 0 && !closedOnServer)
                {
                    /*
                     * POINT OF RECOVERY (2/2):
                     * the socket failure has happened while we were trying to do a recovery.
                     * 
                     * When a recovery request fails we send another one in loop until a recovery succeeds or
                     * the server replies with a sync error.
                     */

                    if (log.IsDebugEnabled)
                    {
                        log.Debug("Start session recovery. Cause: socket failure while recovering");
                    }

                    changePhaseType(SLEEP, true);
                    launchTimeout("currentRetryDelay", calculateRetryDelay(), reason, startRecovery);
                    this.options.increaseRetryDelay();

                }
                else if (switchRequired && !isForced)
                {
                    //connection is broken but we already requested a change in session type, so move on
                    handler.streamSense(handlerPhase, switchCause + ".error", switchForced);

                }
                else
                {
                    String cause = (closedOnServer ? "closed on server" : "socket error");
                    long crd = calculateRetryDelay();
                    log.Debug("Start new session. Cause: " + cause + " in " + crd);

                    closeSession(reason, closedOnServer, RECOVERY_SCHEDULED);
                    // Debug.Assert(@is(SLEEP));

                    launchTimeout("currentRetryDelay", crd, reason, false);
                    this.options.increaseRetryDelay();
                }

            }
            else
            { //FIRST_PAUSE || OFF || SLEEP
              /*
               * 19/11/2018
               * I think that it is logically possible that errors can occur during non-active phase, 
               * so I commented out the shutdown.
               */
              //log.error("(" +reason+ ") Unexpected error event while session is an non-active status: " + phase);
              //shutdown(GO_TO_OFF);

                log.Error("Unexpected error event while session is an non-active status: " + phase);

            }
        }

        protected internal virtual void changeControlLink(string controlLink)
        {
            // do nothing in HTTP session
        }
    }

}