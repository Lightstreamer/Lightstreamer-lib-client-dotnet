using com.lightstreamer.util.threads.providers;
using Lightstreamer_DotNet_Client_Unified.com.lightstreamer.util.threads;

namespace com.lightstreamer.util.threads
{
    /// <summary>
    /// The default implementation of an {@code ExecutorFactory}.
    /// 
    /// </summary>
    public class DefaultExecutorFactory : ExecutorFactory
    {
        public override JoinableExecutor getExecutor(int nThreads, string threadName, long keepAliveTime)
        {
            return new CSJoinableExecutor(threadName, keepAliveTime);
        }

        public override JoinableScheduler getScheduledExecutor(int nThreads, string threadName, long keepAliveTime)
        {
            //return new JoinableSchedulerPoolExecutor(nThreads, threadName, keepAliveTime, TimeUnit.MILLISECONDS);
            return new CSJoinableScheduler(threadName, keepAliveTime); ;
        }
    }
}