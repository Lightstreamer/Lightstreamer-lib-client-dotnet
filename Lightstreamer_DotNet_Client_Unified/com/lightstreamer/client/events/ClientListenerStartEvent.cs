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
    public class ClientListenerStartEvent : Event<ClientListener>
    {
        private readonly LightstreamerClient client;

        public ClientListenerStartEvent(LightstreamerClient client)
        {
            this.client = client;
        }

        public virtual void applyTo(ClientListener listener)
        {
            listener.onListenStart(client);
        }
    }
}