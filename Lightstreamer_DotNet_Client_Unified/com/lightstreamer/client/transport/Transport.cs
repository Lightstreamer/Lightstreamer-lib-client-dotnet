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

namespace com.lightstreamer.client.transport
{

    using LightstreamerRequest = com.lightstreamer.client.requests.LightstreamerRequest;
    using Protocol = com.lightstreamer.client.protocol.Protocol;

    /// <summary>
    /// Interface to be implemented to offer a Transport to the Lightstreamer client library.
    /// </summary>
    public interface Transport
    {

        /// <summary>
        /// Sends a request to a the server specified by <seealso cref="LightstreamerRequest#getTargetServer()"/>.
        /// The request name to be used is specified by <seealso cref="LightstreamerRequest#getRequestName()"/>.
        /// The querystring to be used is specified by <seealso cref="LightstreamerRequest#getQueryString()"/>
        /// Some decoration might be required depending on the nature of the transport for the request to
        /// be correctly interpreted by the receiving server. As an example a standard HTTP GET request
        /// can be obtained like this:
        /// <BR>
        /// <code> 
        /// request.getTargetServer()+"lightstreamer/"+request.getRequestName()+".txt?"+request.getQueryString();
        /// </code>
        /// <BR>
        /// Optional extra headers and optional Proxy coordinates are specified through the dedicated parameters. 
        /// <BR>
        /// This call must execute fast. Network/blocking operations must be asynchronously executed on a separated
        /// thread.
        /// <BR>
        /// This method returns a <seealso cref="RequestHandle"/> </summary>
        /// <param name="protocol"> the Protocol starting the request </param>
        /// <param name="request"> the request to be sent </param>
        /// <param name="listener"> the listener that will receive the various events for this request </param>
        /// <param name="extraHeaders"> HTTP headers to be specified in the request to the server (can be null) </param>
        /// <param name="proxy"> coordinates to a proxy to be used to connect to the server (can be null) </param>
        /// <param name="tcpConnectTimeout"> if the APIs used by the transport allow to specify a connect timeout, then 
        /// specify this value. A 0 value means to keep the underlying transport default.
        /// <BR>
        /// DO NOT IMPLEMENT THE TIMEOUT YOURSELF: higher levels already handle the application-level timeouts
        /// by calling close on the <seealso cref="RequestHandle"/>. This value is meant 
        /// for blocking transports that do not provide an hook until the connection is actually established
        /// (e.g.: the OIO) </param>
        /// <param name="tcpReadTimeout"> if the APIs used by the transport allow to specify a read timeout, then 
        /// specify this value. A 0 value means to keep the underlying transport default.
        /// <BR>
        /// The same remarks of the tcpConnectTimeout parameter apply. </param>
        /// <param name="protocol"> the Protocol instance sending the request </param>
        /// <returns> an object that permits the caller to notify to the Transport that
        /// he is not interested to request responses anymore. Can't be null. </returns>
        RequestHandle sendRequest(Protocol protocol, LightstreamerRequest request, RequestListener listener, IDictionary<string, string> extraHeaders, Proxy proxy, long tcpConnectTimeout, long tcpReadTimeout);
    }
}