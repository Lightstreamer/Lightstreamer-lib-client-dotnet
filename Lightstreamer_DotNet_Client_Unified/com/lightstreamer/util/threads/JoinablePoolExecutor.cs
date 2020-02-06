using com.lightstreamer.util.threads.providers;
using System;
using System.Threading;

namespace com.lightstreamer.util.threads
{

    public class JoinablePoolExecutor : JoinableExecutor
    {

        private object currentThreadLock = new object();

        private volatile Thread currentThread = null;

        public void execute(Action task)
        {
            throw new NotImplementedException();
        }

        public virtual void join()
        {
            try
            {
                lock (currentThreadLock)
                {
                    while (currentThread == null)
                    {
                        Monitor.Wait(currentThreadLock);
                    }

                    currentThread.Join();
                    while (currentThread.IsAlive)
                    {

                    }
                }
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }
        }
    }
}