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