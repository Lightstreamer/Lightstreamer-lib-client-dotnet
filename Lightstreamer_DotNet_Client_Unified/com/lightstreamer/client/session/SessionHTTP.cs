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

using com.lightstreamer.client.protocol;
using com.lightstreamer.client.requests;

namespace com.lightstreamer.client.session
{

    public class SessionHTTP : Session
    {

        public SessionHTTP(int objectId, bool isPolling, bool forced, SessionListener handler, SubscriptionsListener subscriptions, MessagesListener messages, Session originalSession, SessionThread thread, Protocol protocol, InternalConnectionDetails details, InternalConnectionOptions options, int callerPhase, bool retryAgainIfStreamFails, bool sessionRecovery) : base(objectId, isPolling, forced, handler, subscriptions, messages, originalSession, thread, protocol, details, options, callerPhase, retryAgainIfStreamFails, sessionRecovery)
        {
        }

        protected internal override string ConnectedHighLevelStatus
        {
            get
            {
                return this.isPolling ? Constants.HTTP_POLLING : Constants.HTTP_STREAMING;
            }
        }

        protected internal override string FirstConnectedStatus
        {
            get
            {
                return this.isPolling ? Constants.HTTP_POLLING : Constants.SENSE;
            }
        }

        protected internal override bool shouldAskContentLength()
        {
            return !this.isPolling;
        }

        public override void sendReverseHeartbeat(ReverseHeartbeatRequest request, RequestTutor tutor)
        {
            request.addUnique();
            base.sendReverseHeartbeat(request, tutor);
        }
    }

}