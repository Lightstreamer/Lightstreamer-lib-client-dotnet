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