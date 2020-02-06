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
    using Lightstreamer.DotNet.Logging.Log;
    using System.Threading.Tasks;
    using OfflineStatus = com.lightstreamer.client.platform_data.offline.OfflineStatus;

    /// 
    public class OfflineCheck
    {

        private const int MAYBE_ONLINE_TIMEOUT = 20000;
        private const int OFFLINE_CHECKS_PROTECTION = 1;
        private const long OFFLINE_TIMEOUT = 1000;
        internal int maybeOnline = 1;
        internal int maybePhase = 1;
        private SessionThread thread;

        private static readonly ILogger log = LogManager.GetLogger(Constants.TRANSPORT_LOG);

        public OfflineCheck(SessionThread thread)
        {
            this.thread = thread;
        }

        public virtual bool shouldDelay(string server)
        {
            if (OfflineStatus.isOffline(server))
            {

                log.Debug("Offline check: " + maybeOnline);

                if (maybeOnline <= 0)
                { //first time (1) we try anyway
                    return true;
                }
                else
                {
                    maybeOnline--;
                    if (maybeOnline == 0)
                    {
                        int ph = this.maybePhase;

                        log.Debug("Offline check 0.");

                        //avoid to lock on the offline flag, once in MAYBE_ONLINE_TIMEOUT seconds reset the flag
                        thread.schedule(new Task(() =>
                       {
                           resetMaybeOnline(ph);
                       }), MAYBE_ONLINE_TIMEOUT);

                    }
                }

            }
            return false;
        }

        public virtual void resetMaybeOnline()
        {
            this.resetMaybeOnline(this.maybePhase);
        }

        private void resetMaybeOnline(int mp)
        {
            if (mp != maybePhase)
            {
                return;
            }

            log.Debug("Offline check 1.");

            maybePhase++;
            maybeOnline = OFFLINE_CHECKS_PROTECTION;
        }

        public virtual long Delay
        {
            get
            {
                return OFFLINE_TIMEOUT;
            }
        }

    }

}