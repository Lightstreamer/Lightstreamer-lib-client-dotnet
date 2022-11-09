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

using System;
using System.Threading;

namespace com.lightstreamer.util.threads.providers
{
    /// <summary>
    /// Interface which defines a basic thread scheduler whose internal working threads are terminated if
    /// no task arrive within a specified keep-alive time.
    /// </summary>
    public interface JoinableScheduler : Joinable
    {
        /// <summary>
        /// Creates and executes a one-shot action that becomes enabled after the given delay.
        /// </summary>
        /// <param name="command">
        ///            the task to execute </param>
        /// <param name="delay">
        ///            the time in milliseconds from now to delay execution </param>
        /// <returns> a PendingTask representing pending completion of the task </returns>
        /// <exception cref="RejectedExecutionException">
        ///             if the task cannot be scheduled for execution </exception>
        /// <exception cref="NullPointerException">
        ///             if command is null </exception>
        /// <seealso cref= ScheduledExecutorService#schedule(Runnable, long, java.util.concurrent.TimeUnit) </seealso>
        CancellationTokenSource schedule(Action task, long delayInMillis);
    }
}