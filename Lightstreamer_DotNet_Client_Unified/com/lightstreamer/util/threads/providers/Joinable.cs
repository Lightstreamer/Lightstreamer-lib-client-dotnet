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

namespace com.lightstreamer.util.threads.providers
{
    /// <summary>
    /// Root interface for <i>joinable</i> executors and schedulers.
    /// <para>
    /// Executors and Schedulers are said <i>joinable</i> if their internal working threads are
    /// terminated if no more task arrive, therefore allowing a graceful completion of involved threads
    /// without no need to explicitly invoke <seealso cref="ExecutorService.shutdown"/> or
    /// <seealso cref="ScheduledExecutorService.shutdown"/>
    /// method.
    /// 
    /// </para>
    /// </summary>
    public interface Joinable
    {
        /// <summary>
        /// Waits forever for this joinable executor (or scheduler) to die.
        /// </summary>
        /// <exception cref="RuntimeException">
        ///             which wraps an <seealso cref="InterruptedExetpion"/> if any thread has
        ///             interrupted the current thread. </exception>
        void join();
    }
}