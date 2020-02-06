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