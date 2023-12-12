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
using Lightstreamer.DotNet.Logging.Log;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace com.lightstreamer.client.protocol
{
    /// <summary>
    /// A timer sending reverse heartbeats.
    /// <para>
    /// A heartbeat is sent only if the time elapsed from the last sending of a control request is bigger than heartbeat interval.
    /// </para>
    /// <para>
    /// The maximum interval between heartbeats is determined by the parameter LS_inactivity_millis set by the bind_session request
    /// and doesn't change during the life of the corresponding session.
    /// However the interval can be diminished by the user.
    /// </para>
    /// <para>
    /// <i>To build well-formed heartbeat requests, heartbeats are sent only after the bind session request has been issued 
    /// so we are sure that {@literal sessionId} and {@literal serverInstanceAddress} properties are set.</i>
    /// </para>
    /// </summary>
    public class ReverseHeartbeatTimer
    {
        private static readonly ILogger log = LogManager.GetLogger(Constants.HEARTBEAT_LOG);

        private readonly SessionThread sessionThread;
        private readonly InternalConnectionOptions options;
        /// <summary>
        /// Maximum interval. Value of LS_inactivity_millis.
        /// </summary>
        private readonly long maxIntervalMs;
        /// <summary>
        /// It is the minimum between LS_inactivity_millis and the interval chosen by the user.
        /// </summary>
        private long currentIntervalMs = -1;
        private bool disableHeartbeats = false;
        private bool closed = false;
        /// <summary>
        /// Last time a request has been sent to the server.
        /// </summary>
        private long lastSentTimeNs = -1;
        /// <summary>
        /// The timer assures that there is at most one scheduled task by keeping a phase counter
        /// (there is no scheduled task when heartbeats are disabled).
        /// When the user changes the interval (see method onChangeInterval), the counter is incremented
        /// so that if there is a scheduled task, it is discarded since the task phase is less than the phase counter
        /// (see class ScheduledTask).
        /// </summary>
        private int currentPhase = 0;
        /// <summary>
        /// True when the bind session request is sent.
        /// <br>
        /// NB Heartbeats can be sent only when this flag is set.
        /// </summary>
        private bool bindSent = false;

        public ReverseHeartbeatTimer(SessionThread sessionThread, InternalConnectionOptions options)
        {
            // // Debug.Assert(Assertions.SessionThread);
            this.sessionThread = sessionThread;
            this.options = options;
            this.maxIntervalMs = options.ReverseHeartbeatInterval;
            if (log.IsDebugEnabled)
            {
                log.Debug("rhb max interval " + maxIntervalMs);
            }
            CurrentInterval = maxIntervalMs;
        }

        /// <summary>
        /// Must be called just before the sending of a bind session request. </summary>
        /// <param name="bindAsControl"> when true the time a bind_session request is sent is recorded as it is a control request
        /// (see <seealso cref="#onControlRequest()"/>) </param>
        public virtual void onBindSession(bool bindAsControl)
        {
            // // Debug.Assert(Assertions.SessionThread);
            
            if (bindAsControl)
            {
                /*
				 * since schedule() uses lastSentTimeNs,
				 * it is important to set lastSentTimeNs before 
				 */
                lastSentTimeNs = Stopwatch.GetTimestamp();
            }
            if (!bindSent)
            {
                bindSent = true;
                schedule();
            }
        }

        /// <summary>
        /// Must be called when the user modifies the interval.
        /// </summary>
        public virtual void onChangeInterval()
        {
            // // Debug.Assert(Assertions.SessionThread);
            long newInterval = options.ReverseHeartbeatInterval;
            CurrentInterval = newInterval;
        }

        /// <summary>
        /// Must be called when a control request is sent.
        /// </summary>
        public virtual void onControlRequest()
        {
            // Debug.Assert(Assertions.SessionThread);
            lastSentTimeNs = Stopwatch.GetTimestamp();
        }

        /// <summary>
        /// Must be called when the session is closed.
        /// </summary>
        public virtual void onClose()
        {
            // Debug.Assert(Assertions.SessionThread);
            closed = true;
        }

        public virtual long MaxIntervalMs
        {
            get
            {
                return maxIntervalMs;
            }
        }

        private void schedule()
        {
            if (disableHeartbeats || closed)
            {
                return;
            }
            if (lastSentTimeNs == -1)
            {
                /*
				 * If lastSentTimeNs was not already set, 
				 * assume that this is the point from which measuring heartbeat distance.
				 * This can happen when the onBindSession() is called with bindAsControl set to false.
				 */
                lastSentTimeNs = Stopwatch.GetTimestamp();
                submitTask(currentIntervalMs);
            }
            else
            {
                long timeLeftMs = TimeLeftMs;
                if (timeLeftMs <= 0)
                {
                    sendHeartbeat();
                    submitTask(currentIntervalMs);
                }
                else
                {
                    submitTask(timeLeftMs);
                }
            }
        }

        private long TimeLeftMs
        {
            get
            {
                Debug.Assert(lastSentTimeNs != -1);
                Debug.Assert(currentIntervalMs != -1);
                long timeElapsedMs = (Stopwatch.GetTimestamp() - lastSentTimeNs) / (Stopwatch.Frequency / 1000); // convert to millis
                long timeLeftMs = currentIntervalMs - timeElapsedMs;
                return timeLeftMs;
            }
        }

        /// <summary>
        /// Sends a heartbeat message.
        /// </summary>
        private void sendHeartbeat()
        {
            sessionThread.SessionManager.sendReverseHeartbeat(new ReverseHeartbeatRequest(), new VoidTutor(sessionThread, options));
        }

        /// <summary>
        /// Sets the heartbeat interval and schedules a task sending heartbeats if necessary.
        /// </summary>
        private long CurrentInterval
        {
            set
            {
                Debug.Assert(maxIntervalMs != -1);
                long oldIntervalMs = currentIntervalMs;
                /*
				 * Change the current interval with respect to the user defined value and the maximum interval.
				 * 
				 * newInterval      currentIntervalMs   maxIntervalMs   new currentIntervalMs
				 * --------------------------------------------------------------------------------------------------
				 * ∞                ∞                   ∞               ∞
				 * ∞                ∞                   m               impossible: currentIntervalMs > maxIntervalMs
				 * ∞                c                   ∞               ∞
				 * ∞                c                   m               m
				 * u                ∞                   ∞               u
				 * u                ∞                   m               impossible: currentIntervalMs > maxIntervalMs
				 * u                c                   ∞               u
				 * u                c                   m               minimum(u, m)
				 * 
				 * ∞ = interval is 0
				 * u, c, m = interval bigger than 0
				 */
                if (value == 0)
                {
                    currentIntervalMs = maxIntervalMs;
                }
                else if (maxIntervalMs == 0)
                {
                    Debug.Assert(value > 0);
                    currentIntervalMs = value;
                }
                else
                {
                    Debug.Assert(value > 0 && maxIntervalMs > 0);
                    currentIntervalMs = Math.Min(value, maxIntervalMs);
                }
                disableHeartbeats = ( currentIntervalMs == 0 );
                if (oldIntervalMs != currentIntervalMs)
                {
                    if (log.IsDebugEnabled)
                    {
                        log.Debug("rhb current interval " + currentIntervalMs);
                    }
                    if (bindSent)
                    {
                        /* 
						 * since currentIntervalMs has changed,
						 * increment phase to discard already scheduled tasks 
						 */
                        currentPhase++;
                        schedule();
                    }
                }
            }
        }

        /// <summary>
        /// Adds a heartbeat task on session thread after the given delay.
        /// </summary>
        private void submitTask(long scheduleTimeMs)
        {
            if (log.IsDebugEnabled)
            {
                log.Debug("rhb scheduled +" + scheduleTimeMs + " ph " + currentPhase);
            }
            sessionThread.schedule(new Task(() =>
            {
                int phase = currentPhase;
                if (log.IsDebugEnabled)
                {
                    log.Debug("rhb task fired ph " + phase);
                }
                if (phase < this.currentPhase)
                {
                    if (log.IsDebugEnabled)
                    {
                        log.Debug("rhb task discarded ph " + phase);
                    }
                    return;
                }
                Debug.Assert(phase == this.currentPhase);
                this.schedule();

            }), scheduleTimeMs);
        }
    }
}