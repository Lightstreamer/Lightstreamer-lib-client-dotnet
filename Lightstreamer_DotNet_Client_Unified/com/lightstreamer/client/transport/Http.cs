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

using com.lightstreamer.client.protocol;
using com.lightstreamer.client.requests;
using com.lightstreamer.client.session;
using com.lightstreamer.client.transport.providers;
using Lightstreamer.DotNet.Logging.Log;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace com.lightstreamer.client.transport
{

    public class Http : Transport
    {

        protected internal readonly ILogger log = LogManager.GetLogger(Constants.TRANSPORT_LOG);

        private readonly HttpProvider httpProvider;

        private readonly SessionThread sessionThread;

        public Http(SessionThread thread, HttpProvider httpProvider)
        {
            this.sessionThread = thread;
            this.httpProvider = httpProvider;
            this.sessionThread.registerShutdownHook(httpProvider.ShutdownHook);
        }

        public RequestHandle sendRequest(Protocol protocol, LightstreamerRequest request, RequestListener protocolListener, IDictionary<string, string> extraHeaders, Proxy proxy, long tcpConnectTimeout, long tcpReadTimeout)
        {
            if (httpProvider == null)
            {
                log.Fatal("There is no default HttpProvider, can't connect");
                return null;
            }

            RequestHandle connection;
            try
            {
                HttpProvider_HttpRequestListener httpListener = new MyHttpListener(protocolListener, request, sessionThread);
                connection = httpProvider.createConnection(protocol, request, httpListener, extraHeaders, proxy, tcpConnectTimeout, tcpReadTimeout);
            }
            catch (Exception e)
            {
                log.Error("Error - " + e.Message + " - " + e.StackTrace);

                sessionThread.queue(new Task(() =>
                {
                    protocolListener.onBroken();
                }));
                return null;
            }
            if (connection == null)
            {
                // we expect that a closed/broken event will be fired soon
                return null;
            }

            return new RequestHandleAnonymousInnerClass(this, connection);
        }

        private class RequestHandleAnonymousInnerClass : RequestHandle
        {
            private readonly Http outerInstance;

            private com.lightstreamer.client.transport.RequestHandle connection;

            public RequestHandleAnonymousInnerClass(Http outerInstance, com.lightstreamer.client.transport.RequestHandle connection)
            {
                this.outerInstance = outerInstance;
                this.connection = connection;
            }

            public void close(bool forceConnectionClose)
            {
                connection.close(forceConnectionClose);
            }
        }

        /// <summary>
        /// Wraps a request listener created by <seealso cref="TextProtocol"/> and forwards the calls to the session thread.
        /// </summary>
        private class MyHttpListener : HttpProvider_HttpRequestListener
        {

            protected internal readonly ILogger log = LogManager.GetLogger(Constants.TRANSPORT_LOG);

            internal readonly RequestListener listener;
            internal readonly LightstreamerRequest request;
            internal readonly SessionThread sessionThread;

            public MyHttpListener(RequestListener listener, LightstreamerRequest request, SessionThread sessionThread)
            {
                this.listener = listener;
                this.request = request;
                this.sessionThread = sessionThread;
            }

            internal virtual LightstreamerRequest LightstreamerRequest
            {
                get
                {
                    return request;
                }
            }

            public virtual void onMessage(string message)
            {
                sessionThread.queue(new Task(() =>
                {
                    listener.onMessage(message);
                }));
            }

            public virtual void onOpen()
            {
                sessionThread.queue(new Task(() =>
                {
                    listener.onOpen();
                }));
            }

            public virtual void onClosed()
            {
                sessionThread.queue(new Task(() =>
                {
                    listener.onClosed();
                }));
            }

            public virtual void onBroken()
            {
                sessionThread.queue(new Task(() =>
                {
                    listener.onBroken();
                }));
            }
        }
    }
}