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

using DotNetty.Common.Concurrency;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace com.lightstreamer.util.threads
{
    public class StaticAssignmentMultiplexer<S> : ThreadMultiplexer<S>
    {
        internal static IList<IScheduledExecutorService> threads = (IList<IScheduledExecutorService>)new LinkedList<IScheduledExecutorService>();

        static StaticAssignmentMultiplexer()
        {
            int cores = 0;
            foreach (var item in new System.Management.ManagementObjectSearcher("Select * from Win32_Processor").Get())
            {
                cores += int.Parse(item["NumberOfCores"].ToString());
            }

            for (int i = 1; i <= cores; i++)
            {
                int n = i;
                IScheduledExecutorService thread = new SingleThreadEventExecutor("Session Thread " + n, TimeSpan.FromMilliseconds(5));

                threads.Add(thread);
            }
        }

        private static int nextThreadIdx = -1;

        private static IScheduledExecutorService ThreadByRoundRobin
        {
            get
            {
                int prev, next;

                Interlocked.Increment(ref nextThreadIdx);
                prev = nextThreadIdx;

                next = ( prev + 1 ) % threads.Count;


                IScheduledExecutorService thread = threads[next];
                return thread;
            }
        }

        internal ConcurrentDictionary<S, IScheduledExecutorService> associations = new ConcurrentDictionary<S, IScheduledExecutorService>();

        public virtual void register(S source)
        {
            if (associations.ContainsKey(source))
            {
                throw new System.InvalidOperationException("Must register only once per source: you probably want to do it in the constructor");
            }
            IScheduledExecutorService thread = ThreadByRoundRobin;
            associations[source] = thread;
        }

        public virtual void execute(S source, Action runnable)
        {
            IScheduledExecutorService thread = associations[source];
            thread.Execute(runnable);
        }

        public virtual CancellationTokenSource schedule(S source, Action task, long delayMillis)
        {
            IScheduledExecutorService thread = associations[source];
            IScheduledTask tt = thread.Schedule(task, TimeSpan.FromMilliseconds(delayMillis));

            return null;
        }

        public virtual void await()
        {
            //
        }
    }
}