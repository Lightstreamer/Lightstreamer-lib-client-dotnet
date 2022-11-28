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

using System.Collections.Generic;

namespace com.lightstreamer.util
{
    using EventsThread = com.lightstreamer.client.events.EventsThread;

    /// <summary>
    /// Thread-safe listener support.
    /// </summary>
    public class ListenerHolder<T>
    {
        protected internal readonly EventsThread eventThread;
        protected internal readonly ISet<T> listeners = new HashSet<T>();

        public ListenerHolder(EventsThread eventThread)
        {
            this.eventThread = eventThread;
        }

        /// <summary>
        /// Adds the listener. If it is not present, executes the visitor operation on the added listener.
        /// </summary>
        public virtual void addListener(T listener, Visitor<T> visitor)
        {
            lock (this)
            {
                bool isNew = listeners.Add(listener);
                if (isNew)
                {
                    eventThread.queue(() =>
                    {
                        visitor.visit(listener);
                    });
                }
            }
        }

        /// <summary>
        /// Removes the listener. If it is present, executes the visitor operation on the removed listener. 
        /// </summary>
        public virtual void removeListener(T listener, Visitor<T> visitor)
        {
            lock (this)
            {
                bool contained = listeners.Remove(listener);
                if (contained)
                {
                    eventThread.queue(() =>
                    {
                        visitor.visit(listener);
                    });
                }
            }
        }

        /// <summary>
        /// Gets the listeners.
        /// </summary>
        public virtual IList<T> Listeners
        {
            get
            {
                lock (this)
                {
                    return new List<T>(listeners);
                }
            }
        }

        /// <summary>
        /// Executes the visitor operation for each listener.
        /// </summary>
        public virtual void forEachListener(Visitor<T> visitor)
        {
            lock (this)
            {
                foreach (T listener in listeners)
                {
                    eventThread.queue(() =>
                    {
                        visitor.visit(listener);
                    });
                }
            }
        }
    }
}