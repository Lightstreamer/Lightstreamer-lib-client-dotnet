using com.lightstreamer.client.requests;
using com.lightstreamer.client.session;
using com.lightstreamer.client.transport;
using com.lightstreamer.util;
using System.Diagnostics;

namespace com.lightstreamer.client.protocol
{
    public class TextProtocolHttp : TextProtocol
    {
        public TextProtocolHttp(int objectId, SessionThread thread, InternalConnectionOptions options, Http httpTransport) : base(objectId, thread, options, httpTransport)
        {
        }

        public override RequestManager RequestManager
        {
            get
            {
                return this.httpRequestManager;
            }
        }

        public override void sendControlRequest(LightstreamerRequest request, RequestTutor tutor, RequestListener reqListener)
        {
            httpRequestManager.addRequest(request, tutor, reqListener);
        }

        public override void processREQOK(string message)
        {
            Debug.Assert(false);
        }

        public override void processREQERR(string message)
        {
            Debug.Assert(false);
        }

        public override void processERROR(string message)
        {
            Debug.Assert(false);
        }

        public override void stop(bool waitPendingControlRequests, bool forceConnectionClose)
        {
            base.stop(waitPendingControlRequests, forceConnectionClose);
            this.httpRequestManager.close(waitPendingControlRequests);
        }

        public override ListenableFuture openWebSocketConnection(string serverAddress)
        {
            // should never be called (the actual implementation is in TextProtocolWS)
            Debug.Assert(false);
            return ListenableFuture.rejected();
        }

        protected internal override void onBindSessionForTheSakeOfReverseHeartbeat()
        {
            reverseHeartbeatTimer.onBindSession(false);
        }

        public override string DefaultSessionId
        {
            set
            {
                // http connections don't have a default session id
            }
        }
    }
}