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
using DotNetty.Transport.Channels;
using Lightstreamer.DotNet.Logging.Log;

namespace com.lightstreamer.client.transport.providers.netty
{

    /// <summary>
    /// Wraps a <seealso cref="HttpRequestListener"/> and its socket.
    /// When the request has been completed, the socket is returned to the pool.
    /// </summary>
    public class NettyRequestListener : RequestListener
    {

        protected internal static readonly ILogger log = LogManager.GetLogger(Constants.TRANSPORT_LOG);

        private HttpProvider_HttpRequestListener wrapped;
        private bool openFired;
        private bool brokenCalled;
        private bool closedCalled;
        private NettyFullAddress target;
        private IChannel ch;
        private readonly HttpPoolManager channelPool;

        public NettyRequestListener(HttpProvider_HttpRequestListener listener, NettyFullAddress target, IChannel ch, HttpPoolManager channelPool)
        {
            this.wrapped = listener;
            this.target = target;
            this.ch = ch;
            this.channelPool = channelPool;
        }

        public virtual void onOpen()
        {
            if (!this.openFired)
            {
                this.openFired = true;
                wrapped.onOpen();
            }
        }

        public virtual void onBroken()
        {
            if (!this.brokenCalled && !this.closedCalled)
            {
                this.brokenCalled = true;
                wrapped.onBroken();
                this.onClosed();
            }
        }

        /**
        * Notifies the closing and releases the channel to the channel pool.
        */
        public virtual void onClosed()
        {
            if (!this.closedCalled)
            {
                this.closedCalled = true;
                wrapped.onClosed();

                channelPool.release(target, ch);
            }
        }

        public virtual void onMessage(string message)
        {
            if (log.IsDebugEnabled)
            {
                log.Debug(" never ending story of a message " + wrapped.GetType() + " - " + message);
            }

            wrapped.onMessage(message);
        }
    }
}