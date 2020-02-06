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