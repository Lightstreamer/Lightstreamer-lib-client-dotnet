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