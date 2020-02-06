using com.lightstreamer.client;
using com.lightstreamer.util.threads.providers;
using Lightstreamer.DotNet.Logging.Log;
using System;
using System.Threading;

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
namespace com.lightstreamer.util.threads
{
    public class SingleThreadMultiplexer<S> : ThreadMultiplexer<S>
    {
        private static readonly ILogger log = LogManager.GetLogger(Constants.THREADS_LOG);

        private JoinableScheduler scheduler;
        private JoinableExecutor executor;

        public SingleThreadMultiplexer()
        {
            scheduler = ExecutorFactory.DefaultExecutorFactory.getScheduledExecutor(1, "Session Thread", 1000);
            executor = ExecutorFactory.DefaultExecutorFactory.getExecutor(1, "Session Thread", 1000);
        }

        public virtual void await()
        {
            executor.join();
        }

        public virtual void execute(S source, Action runnable)
        {
            executor.execute(runnable);
        }

        public virtual CancellationTokenSource schedule(S source, Action task, long delayMillis)
        {
            return scheduler.schedule(task, delayMillis);
        }
    }
}