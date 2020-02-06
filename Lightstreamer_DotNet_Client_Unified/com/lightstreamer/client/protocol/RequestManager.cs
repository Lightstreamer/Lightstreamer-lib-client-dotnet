using com.lightstreamer.client.requests;
using com.lightstreamer.client.transport;
using com.lightstreamer.util;
using static com.lightstreamer.client.protocol.TextProtocol;

namespace com.lightstreamer.client.protocol
{
    /// <summary>
    /// Encapsulates the transport (HTTP or WebSocket) and provides services such as batching, serialization, 
    /// buffering if the transport is not ready ...
    /// </summary>
    public interface RequestManager : ControlRequestHandler
    {
        /// <summary>
        /// Binds a session. </summary>
        /// <param name="bindFuture"> a future which is fulfilled when the bind_session request is sent by the transport. </param>
        RequestHandle bindSession(BindSessionRequest request, StreamListener reqListener, long tcpConnectTimeout, long tcpReadTimeout, ListenableFuture bindFuture);
    }
}