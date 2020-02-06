using com.lightstreamer.client.requests;
using com.lightstreamer.util;
using System;
using static com.lightstreamer.client.session.Session;

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
namespace com.lightstreamer.client.protocol
{
    public interface Protocol
    {
        ProtocolListener Listener { set; }

        void sendForceRebind(ForceRebindRequest request, RequestTutor tutor);

        void sendDestroy(DestroyRequest request, RequestTutor tutor);

        void sendConstrainRequest(ConstrainRequest request, ConstrainTutor tutor);

        void sendCreateRequest(CreateSessionRequest request);

        ListenableFuture sendBindRequest(BindSessionRequest request);

        /// <summary>
        /// Closes the stream connection. 
        /// </summary>
        void stop(bool waitPendingControlRequests, bool forceConnectionClose);

        void sendMessageRequest(MessageRequest request, RequestTutor tutor);

        void sendSubscriptionRequest(SubscribeRequest request, RequestTutor tutor);

        void sendUnsubscriptionRequest(UnsubscribeRequest request, RequestTutor tutor);

        void sendConfigurationRequest(ChangeSubscriptionRequest request, RequestTutor tutor);

        void sendReverseHeartbeat(ReverseHeartbeatRequest request, RequestTutor tutor);

        void copyPendingRequests(Protocol protocol);

        RequestManager RequestManager { get; }

        void handleReverseHeartbeat();

        /// <summary>
        /// A non-recoverable error causing the closing of the session
        /// and the notification of the error 61 to the method <seealso cref="ClientListener#onServerError(int, String)"/>.
        /// </summary>
        void onFatalError(Exception cause);

        /// <summary>
        /// Opens a WebSocket connection. If a connection is already open (this can happen when the flag isEarlyWSOpenEnabled is set),
        /// the connection is closed and a new connection is opened.
        /// </summary>
        ListenableFuture openWebSocketConnection(string serverAddress);

        /// <summary>
        /// Forward the session recovery request to the transport layer.
        /// </summary>
        void sendRecoveryRequest(RecoverSessionRequest request);

        /// <summary>
        /// Set the default sessionId so the protocol can omit parameter LS_session from requests.
        /// </summary>
        string DefaultSessionId { set; }

        /// <summary>
        /// The maximum time between two heartbeats.
        /// It is the value of the parameter LS_inactivity_millis sent with a bind_session request.
        /// It doesn't change during the life of a session.
        /// </summary>
        long MaxReverseHeartbeatIntervalMs { get; }

        void stopActive(bool forceConnectionClose);
    }
}