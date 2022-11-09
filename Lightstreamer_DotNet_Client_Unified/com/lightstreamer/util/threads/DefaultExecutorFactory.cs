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