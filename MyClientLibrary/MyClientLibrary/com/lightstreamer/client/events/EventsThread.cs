using com.lightstreamer.util.threads.providers;
using Lightstreamer.DotNet.Logging.Log;
using Lightstreamer_DotNet_Client_Unified.com.lightstreamer.util.threads;
using System;

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