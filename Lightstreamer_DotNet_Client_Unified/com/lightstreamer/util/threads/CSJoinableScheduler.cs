using com.lightstreamer.client;
using com.lightstreamer.util.threads.providers;
using Lightstreamer.DotNet.Logging.Log;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Lightstreamer_DotNet_Client_Unified.com.lightstreamer.util.threads
{
    class CSJoinableScheduler : JoinableScheduler
    {
        private Stack<Task> currentTasks = null;
        private string threadName;
        private long keepAliveTime;
        private JoinableExecutor executor;
        private IDictionary<Task, CancellationTokenSource> cancs = new Dictionary<Task, CancellationTokenSource>();
        /// <summary>

        private readonly ILogger log = LogManager.GetLogger(Constants.THREADS_LOG);

        public CSJoinableScheduler()
        {
            currentTasks = new Stack<Task>(); 
        }

        public CSJoinableScheduler(string threadName, long keepAliveTime)
        {
            currentTasks = new Stack<Task>();
            this.threadName = threadName;
            this.keepAliveTime = keepAliveTime;
        }
        public CSJoinableScheduler(string threadName, long keepAliveTime, JoinableExecutor executor)
        {
            currentTasks = new Stack<Task>();
            this.threadName = threadName;
            this.keepAliveTime = keepAliveTime;
            this.executor = executor;
        }

        public void join()
        {
            lock (currentTasks)
            {
                log.Debug("Scheduler count: " + cancs.Count + ", " + cancs.GetHashCode());

                foreach (KeyValuePair<Task, CancellationTokenSource> entry in cancs)
                {
                    if ( entry.Value != null )
                    {
                        entry.Value.Cancel();
                    }
                }
                cancs.Clear();
            }
        }

        private void Dequeue(Task tsk)
        {
            lock (currentTasks)
            {
                if (cancs.ContainsKey(tsk))
                {
                    cancs.Remove(tsk);
                }
            }
        }
        public CancellationTokenSource schedule(Action task, long delayInMillis)
        {
            var source = new CancellationTokenSource();
            CancellationToken ct = source.Token;
            lock (currentTasks)
            {
                Task tsk_p = Task.Factory.StartNew(async (obj) =>
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(delayInMillis));

                    CancellationToken token = (CancellationToken)obj;

                    if (token.IsCancellationRequested)
                    {
                        log.Debug("Cancellation requested, nothing to do.");
                    }
                    else
                    {
                        // await Task.Run(task);
                        executor.execute(task);
                    }

                }, ct);
                cancs[tsk_p] = source;
                tsk_p.ContinueWith((antecedent, fu) =>
                {
                    this.Dequeue(tsk_p);
                }, this);
                // currentTasks.Push(tsk_p);
                // log.Debug("Push +1 task.");
            }

            return source;
        }
    }
}
