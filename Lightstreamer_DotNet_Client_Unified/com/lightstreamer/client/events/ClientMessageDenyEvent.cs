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
    public class ClientMessageDenyEvent : Event<ClientMessageListener>
    {
        private string originalMessage;
        private int code;
        private string error;

        public ClientMessageDenyEvent(string originalMessage, int code, string error)
        {
            this.originalMessage = originalMessage;
            this.code = code;
            this.error = error;
        }

        public virtual void applyTo(ClientMessageListener listener)
        {
            listener.onDeny(originalMessage, code, error);
        }
    }
}