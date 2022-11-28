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

namespace com.lightstreamer.client.session
{

    public interface SessionListener
    {

        void sessionStatusChanged(int handlerPhase, string phase, bool sessionRecovery);

        void streamSense(int handlerPhase, string switchCause, bool forced);

        void switchReady(int handlerPhase, string switchCause, bool forced, bool startRecovery);

        void slowReady(int handlerPhase);

        int onSessionClose(int handlerPhase, bool noRecoveryScheduled);

        void streamSenseSwitch(int handlerPhase, string reason, string sessionPhase, bool startRecovery); //we want to stream-sense but we have a pending connection, try to force the switch

        void onIPReceived(string clientIP);

        void onSessionBound();

        void onSessionStart();

        void onServerError(int errorCode, string errorMessage);

        void onSlowRequired(int handlerPhase, long delay);

        void retry(int handlerPhase, string retryCause, bool forced, bool retryAgainIfStreamFails);

        /// <summary>
        /// Since the client IP has changed and WebSocket support
        /// was enabled again, we ask the <seealso cref="SessionManager"/>
        /// to use a WebSocket transport for this session.
        /// </summary>
        void switchToWebSocket(bool startRecovery);

        /// <summary>
        /// Forward the MPN event to the MPN manager.
        /// </summary>
        void onMpnRegisterOK(string deviceId, string adapterName);

        /// <summary>
        /// Forward the MPN event to the MPN manager.
        /// </summary>
        void onMpnRegisterError(int code, string message);

        /// <summary>
        /// Forward the MPN event to the MPN manager.
        /// </summary>
        void onMpnSubscribeOK(string lsSubId, string pnSubId);

        /// <summary>
        /// Forward the MPN event to the MPN manager.
        /// </summary>
        void onMpnSubscribeError(string subId, int code, string message);

        /// <summary>
        /// Forward the MPN event to the MPN manager.
        /// </summary>
        void onMpnUnsubscribeError(string subId, int code, string message);

        /// <summary>
        /// Forward the MPN event to the MPN manager.
        /// </summary>
        void onMpnUnsubscribeOK(string subId);

        /// <summary>
        /// Forward the MPN event to the MPN manager.
        /// </summary>
        void onMpnResetBadgeOK(string deviceId);

        /// <summary>
        /// Forward the MPN event to the MPN manager.
        /// </summary>
        void onMpnBadgeResetError(int code, string message);

        void recoverSession(int handlerPhase, string retryCause, bool forced, bool retryAgainIfStreamFails);
    }

}