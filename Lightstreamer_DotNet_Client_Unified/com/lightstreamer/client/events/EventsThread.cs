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

using com.lightstreamer.util.threads.providers;
using Lightstreamer.DotNet.Logging.Log;
using Lightstreamer_DotNet_Client_Unified.com.lightstreamer.util.threads;
using System;

namespace com.lightstreamer.client.events
{

    /*
	 * An instance of this class is used to handle client calls and dispatch events as
	 * described in the Thread Safeness section of the Unified Client APIs.
	 */

    public class EventsThread
    {
        /// <summary>
        /// Instance shared by all the <seealso cref="LightstreamerClient"/>.
        /// </summary>
        public static readonly EventsThread instance = new EventsThread();

        private static readonly ILogger log = LogManager.GetLogger(Constants.THREADS_LOG);

        private readonly JoinableExecutor queue_Renamed;

        // only for tests
        public EventsThread()
        {
            queue_Renamed = new CSJoinableExecutor();
        }

        public virtual void queue(Action task)
        {
            queue_Renamed.execute(task);
        }

        public virtual void await()
        {
            if (log.IsDebugEnabled)
            {
                log.Debug("Waiting for tasks of EventsThread to get completed...");
            }

            queue_Renamed.join();

            if (log.IsDebugEnabled)
            {
                log.Debug("Tasks completed");
            }
        }
    }
}