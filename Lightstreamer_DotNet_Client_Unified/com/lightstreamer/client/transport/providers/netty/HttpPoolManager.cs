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

using DotNetty.Common;
using DotNetty.Common.Utilities;
using DotNetty.Transport.Bootstrapping;
using DotNetty.Transport.Channels;
using DotNetty.Transport.Channels.Pool;
using DotNetty.Transport.Channels.Sockets;
using Lightstreamer.DotNet.Logging.Log;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace com.lightstreamer.client.transport.providers.netty
{

    public class HttpPoolManager
    {

        private static readonly ILogger log = LogManager.GetLogger(Constants.NETTY_POOL_LOG);

        private AtomicReference<ChannelPoolMapWrapper> poolMapRef;

        private int nioThreadCounter = 0;

        private int poolWrapperCounter = 0;

        public HttpPoolManager()
        {
            poolMapRef = new AtomicReference<ChannelPoolMapWrapper>();
        }

        /// <summary>
        /// Returns a socket pool map wrapper assuring that only one instance is created.
        /// </summary>
        private ChannelPoolMapWrapper PoolMapWrapper
        {
            get
            {
                lock (this)
                {
                    while (true)
                    {
                        ChannelPoolMapWrapper poolMapWrapper = poolMapRef.Value;
                        if (poolMapWrapper == null)
                        {
                            ChannelPoolMapWrapper poolWrapperNew = new ChannelPoolMapWrapper(this);
                            if (poolMapRef.CompareAndSet(null, poolWrapperNew))
                            {
                                poolWrapperNew.init();
                                return poolWrapperNew;
                            }
                            continue;
                        }
                        return poolMapWrapper;
                    }
                }
            }
        }

        // thread-safe
        public virtual async Task closeAsync()
        {
            ChannelPoolMapWrapper currentPoolWrapper = poolMapRef.Value;
            if (currentPoolWrapper != null)
            {
                await currentPoolWrapper.shutdownAsync();
            }
            else
            {
                log.Info("No available ChannelPool at the moment");
            }
        }

        public virtual ValueTask<IChannel> acquire(NettyFullAddress address)
        {
            SimpleChannelPool pool = PoolMapWrapper.PoolMap.Get(address);
            return pool.AcquireAsync();
        }

        public virtual void release(NettyFullAddress address, IChannel ch)
        {
            ChannelPoolMapWrapper poolMap = poolMapRef.Value;
            if (poolMap != null)
            {
                SimpleChannelPool pool = poolMap.PoolMap.Get(address);
                pool.ReleaseAsync(ch);
                    // NOTE: async function not awaited; ensure it doesn't throw in the concurrent part
            }
        }

        public virtual HttpChannelPool getChannelPool(NettyFullAddress address)
        {
            return PoolMapWrapper.PoolMap.Get(address);
        }

        // TEST ONLY
        protected internal virtual IChannelPoolHandler decorateChannelPoolHandler(IChannelPoolHandler handler)
        {
            return handler;
        }

        /*
		 * Support classes
		 */

        /// <summary>
        /// Wraps a socket pool map and assures that it is properly initialized when returned by <seealso cref="PoolMap"/>.
        /// </summary>
        private class ChannelPoolMapWrapper
        {
            private readonly HttpPoolManager outerInstance;

            public ChannelPoolMapWrapper(HttpPoolManager outerInstance)
            {
                this.outerInstance = outerInstance;
            }

            internal volatile IEventLoopGroup group;

            internal bool closing = false;

            internal bool initLock = true;

            internal volatile AbstractChannelPoolMap<NettyFullAddress, HttpChannelPool> poolMap;

            /*
			 * Thread-safe spinlock: the lock is released by method init()
			 */
            public virtual IChannelPoolMap<NettyFullAddress, HttpChannelPool> PoolMap
            {
                get
                {

                    initLock = false;
                    return poolMap;
                }
            }

            internal virtual void init()
            {

                Interlocked.Increment(ref outerInstance.poolWrapperCounter);

                group = new MultithreadEventLoopGroup();
                Bootstrap cb = new Bootstrap();

                cb.Group(group).Option(ChannelOption.TcpNodelay, true);
                cb.Channel<TcpSocketChannel>();

                poolMap = new HttpChannelPoolMap(outerInstance, cb);
                initLock = false;
            }

            internal virtual async Task shutdownAsync()
            {
                if (!closing)
                {
                    closing = true;
                    try
                    {
                        await this.group.ShutdownGracefullyAsync();

                        if (ThreadDeathWatcher.AwaitInactivity(TimeSpan.FromSeconds(2)))
                        {
                            log.Debug("Global event executor finished shutting down.");
                        }
                        else
                        {
                            log.Debug("Global event executor failed to shut down.");
                        }
                    }
                    catch (Exception e)
                    {
                        log.Error("Netty shutdown error", e);
                    }

                    FastThreadLocal.Destroy();
                }
                else
                {
                    log.Debug("Pool already shutting down");
                }
            }
        }

        /// <summary>
        /// A map of socket pools. Each pool is tied to a distinct server address.
        /// </summary>
        private class HttpChannelPoolMap : AbstractChannelPoolMap<NettyFullAddress, HttpChannelPool>
        {
            private readonly HttpPoolManager outerInstance;

            internal readonly Bootstrap cb;

            internal HttpChannelPoolMap(HttpPoolManager outerInstance, Bootstrap cb)
            {
                this.outerInstance = outerInstance;
                this.cb = cb;
            }

            protected override HttpChannelPool NewPool(NettyFullAddress key)
            {
                /*
				 * When there is a proxy configured, the client must not try to resolve the server address 
				 * because the server could be on a network unreachable to the client 
				 */
                Bootstrap poolBootstrap = cb.Clone();
                poolBootstrap.RemoteAddress(key.Address);
                if (log.IsDebugEnabled)
                {
                    log.Debug("New HTTP channel pool created. Remote address: " + key.Address);
                }
                IChannelPoolHandler handler = outerInstance.decorateChannelPoolHandler(new HttpChannelPoolHandler(key));
                return new HttpChannelPool(key, poolBootstrap, handler);
            }

        }

        /// <summary>
        /// Decorates the acquiring and releasing operations of the sockets.
        /// </summary>
        private class HttpChannelPoolHandler : BaseChannelPoolHandler
        {
            internal readonly NettyFullAddress key;

            internal HttpChannelPoolHandler(NettyFullAddress key)
            {
                this.key = key;
            }

            public override void ChannelReleased(IChannel ch)
            {
                base.ChannelReleased(ch);
                if (log.IsDebugEnabled)
                {
                    log.Debug("HTTP channel released [" + ch.Id + "]");
                }
            }

            public override void ChannelAcquired(IChannel ch)
            {
                base.ChannelAcquired(ch);
                if (log.IsDebugEnabled)
                {
                    log.Debug("HTTP channel acquired [" + ch.Id + "]");
                }
            }

            public override void ChannelCreated(IChannel ch)
            {
                base.ChannelCreated(ch);
                PipelineUtils.populateHttpPipeline(ch, key, new NettySocketHandler());
                if (log.IsDebugEnabled)
                {
                    log.Debug("HTTP channel created [" + ch.Id + ", " + key + "]: " + ch.Active);
                }
            }
        }

        public class HttpChannelPool : SimpleChannelPool
        {

            internal readonly Bootstrap bootstrap;
            internal readonly NettyFullAddress remoteAddress;

            public HttpChannelPool(NettyFullAddress remoteAddress, Bootstrap bootstrap, IChannelPoolHandler handler) : base(bootstrap, handler)
            {
                this.remoteAddress = remoteAddress;
                this.bootstrap = bootstrap;
            }

            public virtual NettyFullAddress RemoteAddress
            {
                get
                {
                    return remoteAddress;
                }
            }

            public virtual Bootstrap Bootstrap
            {
                get
                {
                    return bootstrap;
                }
            }
        }
    }
}