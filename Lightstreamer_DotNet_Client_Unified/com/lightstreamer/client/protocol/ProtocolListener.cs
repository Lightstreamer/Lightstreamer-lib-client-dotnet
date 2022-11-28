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
using static com.lightstreamer.client.session.Session;

namespace com.lightstreamer.client.protocol
{
    public interface ProtocolListener
    {
        void onConstrainResponse(ConstrainTutor tutor);

        void onServerSentBandwidth(string maxBandwidth);

        void onTakeover(int specificCode);

        void onExpiry();

        void onKeepalive();

        void onOKReceived(string newSession, string controlLink, long requestLimitLength, long keepaliveIntervalDefault);

        void onLoopReceived(long serverSentPause);

        void onSyncError(bool async);

        void onUpdateReceived(int subscriptionId, int item, List<string> values);

        void onEndOfSnapshotEvent(int subscriptionId, int item);

        void onClearSnapshotEvent(int subscriptionId, int item);

        void onLostUpdatesEvent(int subscriptionId, int item, int lost);

        void onMessageAck(string sequence, int messageNumber, bool async);

        void onMessageOk(string sequence, int messageNumber);

        void onMessageDeny(string sequence, int denyCode, string denyMessage, int messageNumber, bool async); //<= 0 messaggio rifiutato dal Metadata Adapter

        void onMessageDiscarded(string sequence, int messageNumber, bool async); //38 messaggio non giunto in tempo (e forse mai giunto)

        void onMessageError(string sequence, int errorCode, string errorMessage, int messageNumber, bool async);

        void onSubscriptionError(int subscriptionId, int errorCode, string errorMessage, bool async);

        void onServerError(int errorCode, string errorMessage); //AKA onEnd

        void onUnsubscription(int subscriptionId);

        void onSubscription(int subscriptionId, int totalItems, int totalFields, int keyPosition, int commandPosition);

        void onSubscriptionReconf(int subscriptionId, long reconfId, bool async);

        void onSyncMessage(long seconds);

        void onInterrupted(bool wsError, bool unableToOpen);

        void onConfigurationEvent(int subscriptionId, string frequency);

        void onServerName(string serverName);

        void onClientIp(string clientIp);

        void onSubscriptionAck(int subscriptionId);

        void onUnsubscriptionAck(int subscriptionId);

        /// <summary>
        /// Forward the MPN event to the Session Manager.
        /// </summary>
        void onMpnRegisterOK(string deviceId, string adapterName);

        /// <summary>
        /// Forward the MPN event to the Session Manager.
        /// </summary>
        void onMpnRegisterError(int code, string message);

        /// <summary>
        /// Forward the MPN event to the Session Manager.
        /// </summary>
        void onMpnSubscribeOK(string lsSubId, string pnSubId);

        /// <summary>
        /// Forward the MPN event to the Session Manager.
        /// </summary>
        void onMpnSubscribeError(string subId, int code, string message);

        /// <summary>
        /// Forward the MPN event to the Session Manager.
        /// </summary>
        void onMpnUnsubscribeError(string subId, int code, string message);

        /// <summary>
        /// Forward the MPN event to the Session Manager.
        /// </summary>
        void onMpnUnsubscribeOK(string subId);

        /// <summary>
        /// Forward the MPN event to the Session Manager.
        /// </summary>
        void onMpnResetBadgeOK(string deviceId);

        /// <summary>
        /// Forward the MPN event to the Session Manager.
        /// </summary>
        void onMpnBadgeResetError(int code, string message);

        long DataNotificationProg { get; }

        void onDataNotification();

        void onRecoveryError();
    }
}