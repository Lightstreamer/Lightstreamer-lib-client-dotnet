using com.lightstreamer.client.requests;
using com.lightstreamer.client.session;
using com.lightstreamer.client.transport;
using com.lightstreamer.util;
using static com.lightstreamer.client.protocol.ControlResponseParser;

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
    public class TextProtocolWS : TextProtocol
    {
        private readonly WebSocketRequestManager wsRequestManager;

        public TextProtocolWS(int objectId, SessionThread thread, InternalConnectionOptions options, InternalConnectionDetails details, Http httpTransport) : base(objectId, thread, options, httpTransport)
        {
            wsRequestManager = new WebSocketRequestManager(thread, this, options);
        }

        public override RequestManager RequestManager
        {
            get
            {
                return this.wsRequestManager;
            }
        }

        public override ListenableFuture openWebSocketConnection(string serverAddress)
        {
            return wsRequestManager.openWS(this, serverAddress, new BindSessionListener(this));
        }

        public override void sendControlRequest(LightstreamerRequest request, RequestTutor tutor, RequestListener reqListener)
        {
            wsRequestManager.addRequest(request, tutor, reqListener);
        }

        public override void processREQOK(string message)
        {
            try
            {
                REQOKParser parser = new REQOKParser(message);
                RequestListener reqListener = wsRequestManager.getAndRemoveRequestListener(parser.RequestId);
                if (reqListener == null)
                {
                    /* discard the response of a request made outside of the current session */
                    log.Warn("Acknowledgement discarded: " + message);
                }
                else
                {
                    // notify the request listener (NB we are on SessionThread)
                    reqListener.onMessage(message);
                    reqListener.onClosed();
                }

            }
            catch (ParsingException e)
            {
                onIllegalMessage(e.Message);
            }
        }

        public override void processREQERR(string message)
        {
            try
            {
                REQERRParser parser = new REQERRParser(message);
                RequestListener reqListener = wsRequestManager.getAndRemoveRequestListener(parser.requestId);
                if (reqListener == null)
                {
                    /* discard the response of a request made outside of the current session */
                    log.Warn("Acknowledgement discarded: " + message);
                }
                else
                {
                    // notify the request listener (NB we are on SessionThread)
                    reqListener.onMessage(message);
                    reqListener.onClosed();
                }

            }
            catch (ParsingException e)
            {
                onIllegalMessage(e.Message);
            }
        }

        public override void processERROR(string message)
        {
            // the error is a serious one and we cannot identify the related request;
            // we can't but close the whole session
            log.Error("Closing the session because of unexpected error: " + message);
            try
            {
                ERRORParser parser = new ERRORParser(message);
                forwardControlResponseError(parser.errorCode, parser.errorMsg, null);

            }
            catch (ParsingException e)
            {
                onIllegalMessage(e.Message);
            }
        }

        public override void stop(bool waitPendingControlRequests, bool forceConnectionClose)
        {
            base.stop(waitPendingControlRequests, forceConnectionClose);
            httpRequestManager.close(waitPendingControlRequests);
            wsRequestManager.close(waitPendingControlRequests);
        }

        protected internal override void onBindSessionForTheSakeOfReverseHeartbeat()
        {
            reverseHeartbeatTimer.onBindSession(true);
        }

        public override string DefaultSessionId
        {
            set
            {
                wsRequestManager.DefaultSessionId = value;
            }
        }
    }
}