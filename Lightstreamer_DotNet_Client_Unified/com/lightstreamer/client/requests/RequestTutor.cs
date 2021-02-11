using com.lightstreamer.client.session;
using Lightstreamer.DotNet.Logging.Log;
using System;
using System.Configuration;
using System.Diagnostics;

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
namespace com.lightstreamer.client.requests
{
    public abstract class RequestTutor
    {
        protected internal static readonly ILogger log = LogManager.GetLogger(Constants.REQUESTS_LOG);

        public static readonly long MIN_TIMEOUT;

        static RequestTutor()
        {
            /*
             * The retransmission timeout should not be changed in normal conditions.
             * It was introduced for the sake of cascading clustering applications.
             */
            MIN_TIMEOUT = 4000;
        }

        protected internal long timeoutMs;
        protected internal readonly SessionThread sessionThread;
        protected internal readonly InternalConnectionOptions connectionOptions;
        protected internal readonly Session session;
        protected internal readonly ServerSession serverSession;
        /// <summary>
        /// Flag assuring that only one timeout at once is pending.
        /// When the flag is false <seealso cref="#startTimeout()"/> has no effect.
        /// </summary>
        private bool timeoutIsRunning = false;

        protected bool discarded = false;

        public RequestTutor(SessionThread thread, InternalConnectionOptions connectionOptions) : this(0, thread, connectionOptions, false)
        {
        }

        public RequestTutor(long currentTimeout, SessionThread thread, InternalConnectionOptions connectionOptions, bool fixedTO)
        {
            try
            {
                this.sessionThread = thread;

                this.connectionOptions = connectionOptions;

                this.session = sessionThread.SessionManager.Session;

                this.serverSession = sessionThread.SessionManager.ServerSession;

                if (fixedTO)
                {
                    this.timeoutMs = this.FixedTimeout;
                }
                else
                {
                    this.timeoutMs = currentTimeout > 0 ? currentTimeout * 2 : MIN_TIMEOUT;
                }
            }
            catch (Exception e)
            {
                log.Warn("warn - " + e.Message);
                if (log.IsDebugEnabled)
                {
                    log.Debug(e.StackTrace);
                }
            }
        }

        internal virtual long Timeout
        {
            get
            {
                return this.timeoutMs;
            }
        }

        /// <summary>
        /// When the argument is false, the tutor starts a timer whose purpose is to send again the request 
        /// if a response doesn't arrive after the timeout elapsed. 
        /// Generally the method is called with false argument when <seealso cref="RequestListener#onOpen()"/> fires.
        /// <para>
        /// When the argument is true, the tutor sends again the request.
        /// Generally the method is called with true argument when <seealso cref="RequestListener#onBroken()"/> or
        /// <seealso cref="RequestListener#onClosed()"/> fires and there is a problem.
        /// </para>
        /// </summary>
        public virtual void notifySender(bool failed)
        {
            if (failed)
            {
                this.doRecovery();
            }
            else
            {
                this.startTimeout();

            }
        }

        protected internal virtual void startTimeout()
        {
            if (!timeoutIsRunning)
            {
                timeoutIsRunning = true;
                sessionThread.schedule(new System.Threading.Tasks.Task(() =>
               {

                   onTimeout();

               }), Timeout);
            }
        }

        public void discard()
        {
            discarded = true;
        }

        protected internal void onTimeout()
        {
            timeoutIsRunning = false;
            /*
             * The method is responsible for retransmitting requests which have no response within a timeout interval.
             * The main rules are:
             * 1) when the session transport is HTTP or the request is a force_rebind, 
             * the request must be retransmitted as soon as the timeout expires
             * 2) when the session transport is WebSocket, the request must be retransmitted only if the transport has changed
             *    (since WebSocket is a reliable transport, it would be useless to transmit on the same WebSocket).
             */
            bool success = verifySuccess();
            if (discarded || success)
            {
                // stop retransmissions
                // discard the tutor

            }
            else if (serverSession.Closed)
            {
                Debug.Assert(!success);
                // stop retransmissions
                // discard the tutor

            }
            else if (serverSession.TransportHttp || this is Session.ForceRebindTutor)
            {
                Debug.Assert(!success);
                Debug.Assert(serverSession.Open);
                // always retransmit when the transport is HTTP
                // discard the tutor
                doRecovery();

            }
            else if (!serverSession.isSameStreamConnection(session))
            {
                Debug.Assert(!success);
                Debug.Assert(serverSession.Open);
                Debug.Assert(serverSession.TransportWS);
                // session has changed: retry the transmission
                // discard the tutor
                doRecovery();

            }
            else
            {
                Debug.Assert(!success);
                Debug.Assert(serverSession.Open);
                Debug.Assert(serverSession.TransportWS);
                Debug.Assert(serverSession.isSameStreamConnection(session));
                // reschedule the tutor
                if (!TimeoutFixed)
                {
                    // Debug.Assert(timeoutMs >= MIN_TIMEOUT);
                    timeoutMs *= 2;
                }
                startTimeout();
            }
        }

        public abstract bool shouldBeSent();
        protected internal abstract bool verifySuccess();
        protected internal abstract void doRecovery();
        /// <summary>
        /// called if the request will be willingly not sent (e.g. ADD not sent because a REMOVE was received before the ADD was on the net)
        /// </summary>
        public abstract void notifyAbort();
        protected internal virtual bool TimeoutFixed
        {
            get
            {
                return false;
            }
        }

        protected internal virtual long FixedTimeout
        {
            get
            {
                return 0;
            }
        }
    }
}