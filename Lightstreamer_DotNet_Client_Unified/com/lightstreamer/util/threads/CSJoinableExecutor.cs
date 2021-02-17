using com.lightstreamer.client;
using com.lightstreamer.util.threads.providers;
using Lightstreamer.DotNet.Logging.Log;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Lightstreamer_DotNet_Client_Unified.com.lightstreamer.util.threads
{
    class CSJoinableExecutor : JoinableExecutor
    {

        private ConcurrentQueue<Task> currentTasks = null;
        private string threadName;
        private long keepAliveTime;

        private readonly ILogger log = LogManager.GetLogger(Constants.THREADS_LOG);

        public CSJoinableExecutor()
        {
            currentTasks = new ConcurrentQueue<Task>();
        }

        public CSJoinableExecutor(string threadName, long keepAliveTime)
        {
            currentTasks = new ConcurrentQueue<Task>();
            this.threadName = threadName;
            this.keepAliveTime = keepAliveTime;
        }
        public void execute(Action task)
        {
            lock (this)
            {
                if (currentTasks.Count == 0)
                {
                    Task t = new Task(task);
                    currentTasks.Enqueue(t);

                    t.ContinueWith((antecedent, fu) =>
                    {
                        this.Dequeue();
                    }, this);

                    t.Start();
                }
                else
                {
                    currentTasks.Enqueue(new Task(task));
                }
            }
        }

        private void Dequeue()
        {
            lock (this)
            {
                currentTasks.TryDequeue(out Task tmp);
                if (currentTasks.Count > 0)
                {
                    Task nextT;
                    currentTasks.TryPeek(out nextT);
                    nextT.ContinueWith((antecedent, fu) =>
                    {
                        this.Dequeue();
                    }, this);

                    nextT.Start();

                }
                else
                {
                    // log.Debug("No more tasks waiting.");
                }
            }
        }

        public void join()
        {
            lock (this)
            {
                log.Info("Executor count (pre-join): " + currentTasks.Count + ", " + currentTasks.GetHashCode());
                if (currentTasks.Count > 0)
                {
                    Task waitfor;
                    currentTasks.TryDequeue(out waitfor);
                    if (waitfor != null)
                    {
                        Task.WhenAny(waitfor, Task.Delay(150)).Wait();
                        if (!waitfor.IsCompleted)
                        {
                            log.Debug("Task not completed before Disconnection end: " + waitfor.Id);
                        }
                    }

                    while (currentTasks.Count > 0)
                    {
                        currentTasks.TryDequeue(out waitfor);
                        if (waitfor != null)
                        {
                            waitfor.Start();
                            Task.WhenAny(waitfor, Task.Delay(150)).Wait();
                            if (!waitfor.IsCompleted)
                            {
                                log.Debug("Task not completed before Disconnection end: " + waitfor.Id);
                            }
                        }
                    }
                }
                
                log.Info("Executor count (post join): " + currentTasks.Count + ", " + currentTasks.GetHashCode());
            }   
        }

        private void goLoop()
        {
            // Not implemented.
        }
    }
}
