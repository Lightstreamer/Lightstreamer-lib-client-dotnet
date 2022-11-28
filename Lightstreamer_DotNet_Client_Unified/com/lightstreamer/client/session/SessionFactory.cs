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
using com.lightstreamer.client.transport;
using com.lightstreamer.client.transport.providers;
using Lightstreamer.DotNet.Logging.Log;
using System;
using System.Threading;

namespace com.lightstreamer.client.session
{

    public class SessionFactory
    {

        private static volatile int objectIdGenerator = 0;

        protected internal readonly ILogger log = LogManager.GetLogger(Constants.SESSION_LOG);

        protected internal virtual Session createNewSession(bool isPolling, bool isComboForced, bool isHTTP, Session prevSession, SessionListener listener, SubscriptionsListener subscriptions, MessagesListener messages, SessionThread sessionThread, InternalConnectionDetails details, InternalConnectionOptions options, int handlerPhase, bool retryAgainIfStreamFails, bool sessionRecovery)
        {
            Interlocked.Increment(ref objectIdGenerator);
            int objectId = objectIdGenerator;

            HttpProvider httpProvider = TransportFactory<HttpProvider>.DefaultHttpFactory.getInstance(sessionThread);
            /*var htf = new NettyHttpProviderFactory();

            if (htf is null)
            {
                log.Debug("NettyHttpProviderFactory error.");
            }

            HttpProvider httpProvider = htf.getInstance(sessionThread);*/

            Http httpTransport = new Http(sessionThread, httpProvider);

            if (isHTTP)
            {
                Protocol txt = new TextProtocolHttp(objectId, sessionThread, options, httpTransport);
                SessionHTTP sessionHTTP = new SessionHTTP(objectId, isPolling, isComboForced, listener, subscriptions, messages, prevSession, sessionThread, txt, details, options, handlerPhase, retryAgainIfStreamFails, sessionRecovery);
                return sessionHTTP;

            }
            else
            {

                Protocol ws;
                try
                {
                    ws = new TextProtocolWS(objectId, sessionThread, options, details, httpTransport);

                }
                catch (Exception e)
                {
                    log.Error("Error TextProtocolWS: " + e.Message + ", " + e.StackTrace);

                    ws = null;
                }

                return new SessionWS(objectId, isPolling, isComboForced, listener, subscriptions, messages, prevSession, sessionThread, ws, details, options, handlerPhase, retryAgainIfStreamFails, sessionRecovery);
            }
        }

    }

}