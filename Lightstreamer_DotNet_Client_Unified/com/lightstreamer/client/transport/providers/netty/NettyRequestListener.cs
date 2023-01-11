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

using DotNetty.Transport.Channels;
using Lightstreamer.DotNet.Logging.Log;

namespace com.lightstreamer.client.transport.providers.netty
{

    /// <summary>
    /// Wraps a <seealso cref="HttpProvider_HttpRequestListener"/> and its socket.
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