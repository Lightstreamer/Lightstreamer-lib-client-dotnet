/*
 * Copyright (c) 2004-2019 Lightstreamer s.r.l., Via Campanini, 6 - 20124 Milano, Italy.
 * All rights reserved.
 * www.lightstreamer.com
 *
 * This software is the confidential and proprietary information of
 * Lightstreamer s.r.l.
 * You shall not disclose such Confidential Information and shall use it
 * only in accordance with the terms of the license agreement you entered
 * into with Lightstreamer s.r.l.
 */
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