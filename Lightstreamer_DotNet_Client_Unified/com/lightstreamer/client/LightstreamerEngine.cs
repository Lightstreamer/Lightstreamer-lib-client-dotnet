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
namespace com.lightstreamer.client
{
    using EventsThread = com.lightstreamer.client.events.EventsThread;
    using InternalConnectionOptions = com.lightstreamer.client.session.InternalConnectionOptions;
    using SessionManager = com.lightstreamer.client.session.SessionManager;
    using SessionsListener = com.lightstreamer.client.session.SessionsListener;
    using SessionThread = com.lightstreamer.client.session.SessionThread;
    using WebSocket = com.lightstreamer.client.transport.WebSocket;

    /// <summary>
    /// this class moves calls from the LightstreamerClient (from the EventsThread) to the SessionHandler (to the SessionThread)
    /// </summary>
    internal class LightstreamerEngine
    {

        private const bool FROM_API = true;
        private const bool NO_TRANSPORT_FORCED = false;
        private const bool NO_COMBO_FORCED = false;
        private const bool NO_POLLING = false;
        private const bool CAN_SWITCH = false;
        private const bool NO_RECOVERY = true;


        protected internal readonly ILogger log = LogManager.GetLogger(Constants.SESSION_LOG);


        private readonly SessionManager sessionManager;
        private readonly InternalConnectionOptions connectionOptions;
        private readonly SessionThread sessionThread;
        private readonly EventsThread eventsThread;
        private readonly ClientListener clientListener;

        private bool connectionRequested = false;

        public LightstreamerEngine(InternalConnectionOptions options, SessionThread sessionThread, EventsThread eventsThread, ClientListener listener, SessionManager manager)
        {

            this.connectionOptions = options;
            this.sessionThread = sessionThread;
            this.clientListener = listener;
            this.eventsThread = eventsThread;

            this.sessionManager = manager;
            manager.SessionsListener = new SessionsListenerImpl(this);

        }


        //////////////////Client --> Session

        //from EventsThread
        public virtual void connect()
        {
            this.connect(false);
        }

        private void connect(bool forced)
        {
            this.connectionRequested = true;

            sessionThread.queue(new Task(() =>
            {
            
                string currentStatus = sessionManager.getHighLevelStatus(false);
                if (!forced && ( currentStatus.Equals(Constants.CONNECTING) || currentStatus.Equals(Constants.STALLED) || currentStatus.StartsWith(Constants.CONNECTED, StringComparison.Ordinal) ))
                {
                    return;
                }

                string ft = connectionOptions.ForcedTransport;

                if (string.ReferenceEquals(ft, null))
                {
                    /*
                     * If the WebSocket support is disabled, we must use HTTP (isHttp = true).
                     * On the other hand if the WebSocket support is available, we must use it (isHttp = false).
                     */
                    bool isHttp = WebSocket.Disabled;

                    sessionManager.createSession(FROM_API, NO_TRANSPORT_FORCED, NO_COMBO_FORCED, NO_POLLING, isHttp, null, CAN_SWITCH, false, false);
                }
                else
                {
                    bool isPolling = ft.Equals(Constants.WS_POLLING) || ft.Equals(Constants.HTTP_POLLING);
                    bool isHTTP = ft.Equals(Constants.HTTP_POLLING) || ft.Equals(Constants.HTTP_STREAMING) || ft.Equals(Constants.HTTP_ALL);
                    bool isTransportForced = ft.Equals(Constants.WS_ALL) || ft.Equals(Constants.HTTP_ALL);
                    bool isComboForced = !isTransportForced;

                    sessionManager.createSession(FROM_API, isTransportForced, isComboForced, isPolling, isHTTP, null, CAN_SWITCH, false, false);
                }
            }));
        }

        //from EventsThread
        public virtual void disconnect()
        {
            this.connectionRequested = false;

            sessionThread.queue(new Task(() =>
            {
                 log.Debug("Closing a new session and stopping automatic reconnections");
                 sessionManager.closeSession(FROM_API, "api", NO_RECOVERY);
            }));
        }

        //from EventsThread
        public virtual void onRequestedMaxBandwidthChanged()
        {
            sessionThread.queue(new Task(() =>
            {
                sessionManager.changeBandwidth();
            }));
        }

        //from EventsThread
        public virtual void onReverseHeartbeatIntervalChanged()
        {
            sessionThread.queue(new Task(() =>
            {
                sessionManager.handleReverseHeartbeat(false);
            }));
        }

        //from EventsThread
        public virtual void onForcedTransportChanged()
        {
            if (this.connectionRequested)
            {
                this.connect(true);
            }
        }


        //////////////////Session -> Client

        private class SessionsListenerImpl : SessionsListener
        {
            private readonly LightstreamerEngine outerInstance;

            public SessionsListenerImpl(LightstreamerEngine outerInstance)
            {
                this.outerInstance = outerInstance;
            }


            public virtual void onStatusChanged(string status)
            {
                outerInstance.eventsThread.queue(() =>
                {
                    outerInstance.clientListener.onStatusChange(status);
                });
            }

            public virtual void onServerError(int errorCode, string errorMessage)
            {
                outerInstance.eventsThread.queue(() =>
                {
                    outerInstance.clientListener.onServerError(errorCode, errorMessage);
                });
            }

        }
    }
}