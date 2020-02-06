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

        private readonly ILogger log = LogManager.GetLogger(Constants.THREADS_LOG);

        public CSJoinableScheduler()
        {
            currentTasks = new Stack<Task>(); ;
        }

        public CSJoinableScheduler(string threadName, long keepAliveTime)
        {
            currentTasks = new Stack<Task>();
            this.threadName = threadName;
            this.keepAliveTime = keepAliveTime;
        }

        public void join()
        {
            lock (currentTasks)
            {
                while (currentTasks.Count > 0)
                {
                    currentTasks.Pop().Wait();
                }
            }
        }

        public CancellationTokenSource schedule(Action task, long delayInMillis)
        {
            var ts = new CancellationTokenSource();
            CancellationToken ct = ts.Token;
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
                        await Task.Run(task);
                    }

                }, ct);
                currentTasks.Push(tsk_p);
            }

            return ts;
        }
    }
}
