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

namespace com.lightstreamer.client.transport.providers.netty.pool
{
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Pool;
    using System.Threading.Tasks;
    using HttpChannelPool = com.lightstreamer.client.transport.providers.netty.HttpPoolManager.HttpChannelPool;

    /// <summary>
    /// A pool of WebSocket connections having the same <seealso cref="ExtendedNettyFullAddress"/> remote address.
    /// </summary>
    public class WebSocketChannelPool : ChildChannelPool
    {

        private readonly ExtendedNettyFullAddress address;

        public WebSocketChannelPool(HttpChannelPool parentPool, ExtendedNettyFullAddress address, IChannelPoolHandler handler) : base(parentPool.Bootstrap, parentPool, handler)
        {
            this.address = address;
            if (log.IsDebugEnabled)
            {
                log.Debug("New WS channel pool created. Remote address: " + address.Address.Address);
            }
        }

        protected override Task ConnectChannel(IChannel chnl, long timeout)
        {

            var wcuf = new WebSocketChannelUpgradeFuture(chnl, address);

            if (log.IsDebugEnabled)
            {
                log.Debug("Wait WS upgrade completition.");
            }

            wcuf.AwaitChannel(timeout);

            return wcuf.UpgradeTask;
        }
    }
}