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

        protected override Task ConnectChannel(IChannel chnl)
        {

            var wcuf = new WebSocketChannelUpgradeFuture(chnl, address);

            if (log.IsDebugEnabled)
            {
                log.Debug("Wait WS upgrade completition.");
            }

            wcuf.AwaitChannel();

            return wcuf.UpgardeTask;
        }
    }
}