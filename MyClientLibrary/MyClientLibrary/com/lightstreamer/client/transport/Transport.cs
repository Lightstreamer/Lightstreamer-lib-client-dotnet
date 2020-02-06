using System.Collections.Generic;

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