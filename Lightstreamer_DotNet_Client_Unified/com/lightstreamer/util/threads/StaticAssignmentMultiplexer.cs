using DotNetty.Common.Concurrency;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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