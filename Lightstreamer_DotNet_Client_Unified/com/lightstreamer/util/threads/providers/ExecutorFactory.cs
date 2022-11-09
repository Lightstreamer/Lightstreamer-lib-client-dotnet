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

using Lightstreamer_DotNet_Client_Unified.com.lightstreamer.util.threads;

namespace com.lightstreamer.util.threads.providers
{
    /// <summary>
    /// A Factory of <i>joinable</i> Executors or Schedulers.
    /// <para>
    /// The entry point of the factory is the
    /// <seealso cref="#getDefaultExecutorFactory()"/> static method, which provides an
    /// instance of the class <seealso cref="DefaultExecutorFactory"/>. To provide a custom
    /// implementation, it is required to pass it to the
    /// <seealso cref="#setDefaultExecutorFactory(ExecutorFactory)"/>, before the library
    /// is actually used.
    /// </para>
    /// </summary>
    public class ExecutorFactory
    {
        private static readonly AlternativeLoader<ExecutorFactory> loader = new AlternativeLoaderAnonymousInnerClass();

        private class AlternativeLoaderAnonymousInnerClass : AlternativeLoader<ExecutorFactory>
        {

            protected internal override string[] DefaultClassNames
            {
                get
                {
                    string[] classes = new string[] { "com.lightstreamer.util.threads.DefaultExecutorFactory" };
                    return classes;
                }
            }

        }

        private static volatile ExecutorFactory defaultExecutorFactory;

        public static ExecutorFactory DefaultExecutorFactory
        {
            set
            {
                if (value == null)
                {
                    throw new System.ArgumentException("Specify a factory");
                }

                defaultExecutorFactory = value;
            }
            get
            {
                if (defaultExecutorFactory == null)
                {
                    lock (typeof(ExecutorFactory))
                    {
                        defaultExecutorFactory = loader.Alternative;

                        if (defaultExecutorFactory == null)
                        {
                            // Console.Error.WriteLine("NO THREADEXECUTOR FACTORY CLASS AVAILABLE, SOMETHING WENT WRONG AT BUILD TIME, CONTACT LIGHTSTREAMER SUPPORT");

                            defaultExecutorFactory = new DefaultExecutorFactory();

                            //defaultExecutorFactory = new ExecutorFactory();
                        }
                    }

                }

                return defaultExecutorFactory;
            }
        }

        /// <summary>
        /// Configure and returns a new {@code JoinableExecutor} instance, as per the
        /// specified parameters.
        /// </summary>
        /// <param name="nThreads">
        ///            the number of threads of the thread pool </param>
        /// <param name="threadName">
        ///            the suffix to use for the name of every newly created thread </param>
        /// <param name="keepAliveTime">
        ///            the keep-alive time specified in milliseconds. </param>
        /// <returns> a new instance of {@code JoinableExecutor} </returns>
        public virtual JoinableExecutor getExecutor(int nThreads, string threadName, long keepAliveTime)
        {
            return new CSJoinableExecutor(threadName, keepAliveTime);
        }

        /// <summary>
        /// Configure and returns a new {@code JoinableScheduler} instance, as per
        /// the specified parameters.
        /// </summary>
        /// <param name="nThreads">
        ///            the number of threads of the thread pool </param>
        /// <param name="threadName">
        ///            the suffix to use for the name of every newly created thread </param>
        /// <param name="keepAliveTime">
        ///            the keep-alive time specified in milliseconds. </param>
        /// <returns> a new instance of {@code JoinableScheduler} </returns>
        public virtual JoinableScheduler getScheduledExecutor(int nThreads, string threadName, long keepAliveTime)
        {
            return new CSJoinableScheduler(threadName, keepAliveTime); ;
        }

        public virtual JoinableScheduler getScheduledExecutor(int nThreads, string threadName, long keepAliveTime, JoinableExecutor executor)
        {
            return new CSJoinableScheduler(threadName, keepAliveTime, executor); ;
        }
    }
}