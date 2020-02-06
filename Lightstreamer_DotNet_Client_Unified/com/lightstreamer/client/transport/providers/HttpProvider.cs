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
namespace com.lightstreamer.client.transport.providers
{

    using LightstreamerRequest = com.lightstreamer.client.requests.LightstreamerRequest;
    using Protocol = com.lightstreamer.client.protocol.Protocol;
    using ThreadShutdownHook = com.lightstreamer.util.threads.ThreadShutdownHook;

    public interface HttpProvider
    {

        /// <summary>
        /// MUST NOT BLOCK
        /// </summary>
        RequestHandle createConnection(Protocol protocol, LightstreamerRequest request, HttpProvider_HttpRequestListener httpListener, IDictionary<string, string> extraHeaders, Proxy proxy, long tcpConnectTimeout, long tcpReadTimeout);

        ThreadShutdownHook ShutdownHook { get; }

    }

    public interface HttpProvider_HttpRequestListener : RequestListener
    {
        //
    }

}