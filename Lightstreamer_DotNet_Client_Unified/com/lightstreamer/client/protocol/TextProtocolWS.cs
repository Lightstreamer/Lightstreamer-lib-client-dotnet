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

using com.lightstreamer.client.requests;
using com.lightstreamer.client.session;
using com.lightstreamer.client.transport;
using com.lightstreamer.util;
using static com.lightstreamer.client.protocol.ControlResponseParser;

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


            log.Info("Stop Protocol");

            base.stop(waitPendingControlRequests, forceConnectionClose);
            httpRequestManager.close(waitPendingControlRequests);
            wsRequestManager.close(waitPendingControlRequests);
        }

        protected internal override void onBindSessionForTheSakeOfReverseHeartbeat()
        {
            reverseHeartbeatTimer.onBindSession(true);
        }

        protected override void forwardDestroyRequest(DestroyRequest request, RequestTutor tutor, RequestListener reqListener)
        {
            wsRequestManager.addRequest(request, tutor, reqListener);
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