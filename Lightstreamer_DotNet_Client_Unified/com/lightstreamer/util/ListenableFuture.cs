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

using DotNetty.Common.Concurrency;
using System.Collections.Generic;

namespace com.lightstreamer.util
{
    /// <summary>
    /// A future to which listeners can be attached.
    /// </summary>
    public class ListenableFuture
    {

        private readonly IList<IRunnable> onFulfilledListeners = new List<IRunnable>();
        private readonly IList<IRunnable> onRejectedListeners = new List<IRunnable>();
        private State state = State.NOT_RESOLVED;

        /// <summary>
        /// Returns a fulfilled future.
        /// </summary>
        public static ListenableFuture fulfilled()
        {
            return ( new ListenableFuture() ).fulfill();
        }

        /// <summary>
        /// Returns a rejected future.
        /// </summary>
        public static ListenableFuture rejected()
        {
            return ( new ListenableFuture() ).reject();
        }

        /// <summary>
        /// Adds a handler for the successful case. 
        /// </summary>
        public virtual ListenableFuture onFulfilled(IRunnable listener)
        {
            lock (this)
            {
                onFulfilledListeners.Add(listener);
                if (state == State.FULFILLED)
                {
                    listener.Run();
                }
                return this;
            }
        }

        /// <summary>
        /// Adds a handler for the error case.
        /// </summary>
        public virtual ListenableFuture onRejected(IRunnable listener)
        {
            lock (this)
            {
                onRejectedListeners.Add(listener);
                if (state == State.REJECTED)
                {
                    listener.Run();
                }
                return this;
            }
        }

        /// <summary>
        /// Sets the future as fulfilled.
        /// </summary>
        public virtual ListenableFuture fulfill()
        {
            lock (this)
            {
                if (state == State.NOT_RESOLVED)
                {
                    state = State.FULFILLED;
                    foreach (IRunnable runnable in onFulfilledListeners)
                    {
                        runnable.Run();
                    }
                }
                return this;
            }
        }

        /// <summary>
        /// Sets the future as rejected.
        /// </summary>
        public virtual ListenableFuture reject()
        {
            lock (this)
            {
                if (state == State.NOT_RESOLVED)
                {
                    state = State.REJECTED;
                    foreach (IRunnable runnable in onRejectedListeners)
                    {
                        runnable.Run();
                    }
                }
                return this;
            }
        }

        /// <summary>
        /// Aborts the operation. Attached handlers are not executed.
        /// </summary>
        public virtual ListenableFuture abort()
        {
            lock (this)
            {
                state = State.ABORTED;
                return this;
            }
        }

        public virtual State getState()
        {
            lock (this)
            {
                return state;
            }
        }

        public enum State
        {
            NOT_RESOLVED,
            FULFILLED,
            REJECTED,
            ABORTED
        }
    }
}