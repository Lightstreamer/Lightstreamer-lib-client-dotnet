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

namespace com.lightstreamer.client.events
{
    public class SubscriptionListenerCommandSecondLevelSubscriptionErrorEvent : Event<SubscriptionListener>
    {
        private readonly string key;
        private readonly int code;
        private readonly string message;

        public SubscriptionListenerCommandSecondLevelSubscriptionErrorEvent(int code, string message, string key)
        {
            this.key = key;
            this.code = code;
            this.message = message;
        }

        public virtual void applyTo(SubscriptionListener listener)
        {
            listener.onCommandSecondLevelSubscriptionError(code, message, key);
        }
    }
}