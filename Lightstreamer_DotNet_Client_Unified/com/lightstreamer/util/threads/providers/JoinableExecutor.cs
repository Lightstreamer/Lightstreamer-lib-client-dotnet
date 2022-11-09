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

namespace com.lightstreamer.util.threads.providers
{
    /// <summary>
    /// Interface which defines a basic thread executor whose internal working
    /// threads are terminated if no task arrive within a specified keep-alive time.
    /// 
    /// </summary>
    public interface JoinableExecutor : Joinable
    {
        /// <summary>
        /// Executes the given command at some time in the future.
        /// </summary>
        /// <seealso cref= Executor#execute(Runnable)
        /// </seealso>
        /// <param name="command">
        ///            the runnable task </param>
        /// <exception cref="RejectedExecutionException">
        ///             if this task cannot be accepted for execution </exception>
        /// <exception cref="NullPointerException">
        ///             if command is null </exception>
        void execute(Action task);
    }
}