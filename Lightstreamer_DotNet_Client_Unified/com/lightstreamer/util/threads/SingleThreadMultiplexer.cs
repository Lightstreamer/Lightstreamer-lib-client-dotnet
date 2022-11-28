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

using com.lightstreamer.client;
using com.lightstreamer.util.threads.providers;
using Lightstreamer.DotNet.Logging.Log;
using System;
using System.Threading;

namespace com.lightstreamer.util.threads
{
    public class SingleThreadMultiplexer<S> : ThreadMultiplexer<S>
    {
        private static readonly ILogger log = LogManager.GetLogger(Constants.THREADS_LOG);

        private JoinableScheduler scheduler;
        private JoinableExecutor executor;

        public SingleThreadMultiplexer()
        {
            executor = ExecutorFactory.DefaultExecutorFactory.getExecutor(1, "Session Thread", 1000);
            scheduler = ExecutorFactory.DefaultExecutorFactory.getScheduledExecutor(1, "Session Thread", 1000, executor);
            
        }

        public virtual void await()
        {
            log.Info("Await executor ... ");
            executor.join();
            log.Info("Await scheduler ... ");
            scheduler.join();
            log.Info("Await done.");
        }

        public virtual void execute(S source, Action runnable)
        {
            executor.execute(runnable);
            // scheduler.schedule(runnable, 0);
        }

        public virtual CancellationTokenSource schedule(S source, Action task, long delayMillis)
        {
            return scheduler.schedule(task, delayMillis);
        }
    }
}