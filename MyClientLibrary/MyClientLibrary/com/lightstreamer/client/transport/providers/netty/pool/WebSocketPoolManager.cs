namespace com.lightstreamer.client.transport.providers.netty.pool
{
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Pool;
    using Lightstreamer.DotNet.Logging.Log;

    /// <summary>
    /// A channel pool sharing WebSocket connections.
    /// </summary>
    public class WebSocketPoolManager : System.IDisposable
    {

        private static readonly ILogger log = LogManager.GetLogger(Constants.NETTY_POOL_LOG);

        private readonly AbstractChannelPoolMap<ExtendedNettyFullAddress, WebSocketChannelPool> poolMap;

        public WebSocketPoolManager(HttpPoolManager httpPoolMap)
        {
            this.poolMap = new AbstractChannelPoolMapAnonymousInnerClass(this, httpPoolMap);
        }

        private class AbstractChannelPoolMapAnonymousInnerClass : AbstractChannelPoolMap<ExtendedNettyFullAddress, WebSocketChannelPool>
        {
            private readonly WebSocketPoolManager outerInstance;

            private HttpPoolManager httpPoolMap;

            public AbstractChannelPoolMapAnonymousInnerClass(WebSocketPoolManager outerInstance, HttpPoolManager httpPoolMap)
            {
                this.outerInstance = outerInstance;
                this.httpPoolMap = httpPoolMap;
            }

            protected override WebSocketChannelPool NewPool(ExtendedNettyFullAddress key)
            {
                HttpPoolManager.HttpChannelPool httpPool = httpPoolMap.getChannelPool(key.Address);

                IChannelPoolHandler wsPoolHandler = outerInstance.decorateChannelPoolHandler(new WebSocketChannelPoolHandler());
                WebSocketChannelPool wsPool = new WebSocketChannelPool(httpPool, key, wsPoolHandler);

                return wsPool;
            }
        }

        /// <summary>
        /// Gets a channel from the pool.
        /// </summary>
        public virtual IChannelPool get(ExtendedNettyFullAddress addr)
        {
            return poolMap.Get(addr);
        }

        public virtual void Dispose()
        {
            poolMap.Dispose();
        }

        // TEST ONLY
        protected internal virtual IChannelPoolHandler decorateChannelPoolHandler(IChannelPoolHandler handler)
        {
            return handler;
        }

        /// <summary>
        /// Handler which is called by the pool manager when a channel is acquired or released.
        /// </summary>
        class WebSocketChannelPoolHandler : BaseChannelPoolHandler
        {
            public override void ChannelReleased(IChannel ch)
            {
                base.ChannelReleased(ch);
                if (log.IsDebugEnabled)
                {
                    log.Debug("WebSocket channel released [" + ch.Id + "]");
                }
            }

            public override void ChannelAcquired(IChannel ch)
            {
                base.ChannelAcquired(ch);
                if (log.IsDebugEnabled)
                {
                    log.Debug("WebSocket channel acquired [" + ch.Id + "]");
                }
            }

            public override void ChannelCreated(IChannel ch)
            {
                base.ChannelCreated(ch);
                // PipelineUtils.populateHttpPipeline(ch, key, new NettySocketHandler());
                if (log.IsDebugEnabled)
                {
                    log.Debug("WebSocket channel created [" + ch.Id + "]");
                }
            }
        }
    }
}