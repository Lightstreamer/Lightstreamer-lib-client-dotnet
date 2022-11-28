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

using Lightstreamer.DotNet.Logging.Log;
using System.Diagnostics;

namespace com.lightstreamer.client.session
{

    /// <summary>
    /// Bean about the status of the recovery attempt.
    /// <para>
    /// State graph of the bean. The event start=T means the client wants to recover the current session.
    /// Transitions not depicted should not happen.
    /// <pre>
    ///       start=F                            start=T
    ///       +--+                               +--+
    ///       |  |                               |  |
    ///       |  |                               |  |
    ///    +--+--v------+   start=T/set ts    +--+--v-----+
    ///    |recovery=F  +--------------------->recovery=T |
    ///    |            +<--------------------+           |
    ///    +------------+   start=F/reset ts  +-----------+
    /// </pre>
    /// </para>
    /// </summary>
    public class RecoveryBean
    {
        protected internal readonly ILogger log = LogManager.GetLogger(Constants.SESSION_LOG);

        /// <summary>
        /// The flag is true when the session has been created to recover the previous session,
        /// which was discarded because of a network error.
        /// The first request sent by this session is a <seealso cref="RecoverSessionRequest"/>.
        /// </summary>
        private bool recovery = false;
        /// <summary>
        /// Start time of the recovery process.
        /// </summary>
        //GC_____ private long recoveryTimestampNs;
        private Stopwatch stopWatch = new Stopwatch();

        private bool invariant()
        {
            //GC_____ return recovery ? recoveryTimestampNs != -1 : recoveryTimestampNs == -1;
            return recovery ? stopWatch.IsRunning : !stopWatch.IsRunning;
        }

        /// <summary>
        /// Initial state. No recovery.
        /// </summary>
        public RecoveryBean()
        {
            recovery = false;
            stopWatch = new Stopwatch();

            Debug.Assert(invariant());
        }

        /// <summary>
        /// Next state.
        /// </summary>
        public RecoveryBean(bool startRecovery, RecoveryBean old)
        {
            if (old.recovery)
            {
                if (startRecovery)
                {
                    recovery = true;
                    stopWatch = old.stopWatch;
                }
                else
                {
                    /*
					 * This case can occur when, for example, after a recovery
					 * the client rebinds in HTTP because the opening of Websockets takes too long. 
					 */
                    recovery = false;
                    //GC____ recoveryTimestampNs = -1;
                    stopWatch.Reset();
                }

            }
            else
            {
                if (startRecovery)
                {
                    recovery = true;
                    stopWatch.Start();

                }
                else
                {
                    recovery = false;
                    stopWatch.Reset();
                }
            }
            Debug.Assert(invariant());
        }

        /// <summary>
        /// Restore the time left to complete a recovery, i.e. calling timeLeftMs(maxTimeMs) returns maxTimeMs.
        /// The method must be called when a recovery is successful.
        /// </summary>
        internal virtual void restoreTimeLeft()
        {
            recovery = false;
            stopWatch.Reset();
        }

        /// <summary>
        /// True when the session has been created to recover the previous session,
        /// which was discarded because of a network error.
        /// </summary>
        internal virtual bool Recovery
        {
            get
            {
                return recovery;
            }
        }

        /// <summary>
        /// Time left to recover the session.
        /// When zero or a negative value, the session must be discarded.
        /// </summary>
        internal virtual long timeLeftMs(long maxTimeMs)
        {
            if (recovery)
            {
                return ( maxTimeMs - ( stopWatch.ElapsedMilliseconds ) );
            }
            else
            {
                return maxTimeMs;
            }
        }
    }

}