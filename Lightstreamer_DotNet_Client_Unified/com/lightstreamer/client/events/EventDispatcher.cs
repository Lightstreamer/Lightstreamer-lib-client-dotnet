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

using Lightstreamer.DotNet.Logging.Log;
using System;
using System.Collections.Generic;

namespace com.lightstreamer.client.events
{
    public class EventDispatcher<T>
    {
        private readonly IDictionary<T, ListenerWrapper> listeners = new Dictionary<T, ListenerWrapper>();
        private readonly EventsThread eventThread;

        private readonly ILogger log = LogManager.GetLogger(Constants.ACTIONS_LOG);

        public EventDispatcher(EventsThread thread)
        {
            if (thread == null)
            {
                throw new System.NullReferenceException("an EventsThread is required");
            }
            this.eventThread = thread;
        }

        public virtual void AddListener(T listener, Event<T> startEvent)
        {
            lock (this)
            {
                if (listeners.ContainsKey(listener))
                {
                    return;
                }

                ListenerWrapper wrapper = new ListenerWrapper(this, listener);
                listeners[listener] = wrapper;

                this.dispatchEventToListener(startEvent, wrapper, true);
            }
        }

        public virtual void removeListener(T listener, Event<T> endEvent)
        {
            lock (this)
            {
                ListenerWrapper wrapper = listeners.GetValueOrNull(listener);
                if (wrapper == null)
                {
                    //wrapper does not exists
                    return;
                }

                listeners.Remove(listener);
                wrapper.alive = false;

                this.dispatchEventToListener(endEvent, wrapper, true);
            }
        }

        public virtual void dispatchEvent(Event<T> @event)
        {
            lock (this)
            {
                foreach (KeyValuePair<T, ListenerWrapper> entry in listeners.SetOfKeyValuePairs())
                {
                    this.dispatchEventToListener(@event, entry.Value, false);
                }

            }
        }

        public virtual int size()
        {
            lock (this)
            {
                return listeners.Count;
            }
        }

        public virtual IList<T> Listeners
        {
            get
            {
                lock (this)
                {
                    ISet<KeyValuePair<T, ListenerWrapper>> listenerEntries = listeners.SetOfKeyValuePairs();
                    List<T> listenerList = new List<T>(listenerEntries.Count);

                    foreach (KeyValuePair<T, ListenerWrapper> entry in listenerEntries)
                    {
                        listenerList.Add(entry.Value.listener);
                    }

                    return listenerList;
                }
            }
        }

        private void dispatchEventToListener(Event<T> @event, ListenerWrapper wrapper, bool forced)
        {

            if (@event == null)
            {
                //should not happen, widely used during tests
                return;
            }
            eventThread.queue(() =>
            {
                if (wrapper.alive || forced)
                {
                    try
                    {
                        @event.applyTo(wrapper.listener);
                    }
                    catch (Exception e) when (e is Exception || e is Exception)
                    {
                        log.Error("Exception caught while executing event on custom code: " + e.Message);
                        if (log.IsDebugEnabled)
                        {
                            log.Debug(" - " + e.StackTrace);
                        }
                    }
                }
            });

        }

        public virtual void dispatchSingleEvent(Event<T> @event, T listener)
        {
            eventThread.queue(() =>
            {
                try
                {
                    @event.applyTo(listener);
                }
                catch (Exception e) when (e is Exception || e is Exception)
                {
                    log.Error("Exception caught while executing event on custom code: " + e.Message);
                    if (log.IsDebugEnabled)
                    {
                        log.Debug(" - " + e.StackTrace);
                    }
                }
            });
        }

        internal class ListenerWrapper
        {
            private readonly EventDispatcher<T> outerInstance;

            internal T listener;
            internal volatile bool alive = true;

            public ListenerWrapper(EventDispatcher<T> outerInstance, T listener) : base()
            {
                this.outerInstance = outerInstance;
                this.listener = listener;
            }
        }
    }
}