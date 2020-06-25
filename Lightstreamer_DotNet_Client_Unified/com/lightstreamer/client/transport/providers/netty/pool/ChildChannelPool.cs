using DotNetty.Buffers;
using DotNetty.Common.Internal;
using DotNetty.Common.Utilities;
using DotNetty.Transport.Bootstrapping;
using DotNetty.Transport.Channels;
using DotNetty.Transport.Channels.Pool;
using Lightstreamer.DotNet.Logging.Log;
using System;
using System.Collections.Concurrent;
using System.Diagnostics.Contracts;
using System.Threading.Tasks;

namespace com.lightstreamer.client.transport.providers.netty.pool
{
    /// <summary>
    /// Simple <seealso cref="ChannelPool"/> implementation which will acquire a channel from the parent pool if someone tries to acquire
    /// a <seealso cref="Channel"/> but none is in the pool at the moment. No limit on the maximal concurrent <seealso cref="Channel"/>s is enforced.
    /// 
    /// This implementation uses LIFO order for <seealso cref="Channel"/>s in the <seealso cref="ChannelPool"/>.
    /// <para>
    /// <b>NB</b> The code was adapted from <seealso cref="SimpleChannelPool"/>.
    /// </para>
    /// </summary>
    public abstract class ChildChannelPool : IChannelPool
    {
        protected internal static readonly ILogger log = LogManager.GetLogger(Constants.NETTY_POOL_LOG);
        protected internal static readonly ILogger logStream = LogManager.GetLogger(Constants.TRANSPORT_LOG);

        private static readonly AttributeKey<ChildChannelPool> PoolKey = AttributeKey<ChildChannelPool>.NewInstance("myChannelPool");
        private static readonly InvalidOperationException FULL_EXCEPTION = new InvalidOperationException("ChannelPool full");
        private static readonly InvalidOperationException UNHEALTHY_NON_OFFERED_TO_POOL = new InvalidOperationException("Channel is unhealthy not offering it back to pool");

        // private static readonly System.InvalidOperationException FULL_EXCEPTION = ThrowableUtil.unknownStackTrace(new System.InvalidOperationException("ChannelPool full"), typeof(ChildChannelPool), "releaseAndOffer(...)");
        // private static readonly System.InvalidOperationException UNHEALTHY_NON_OFFERED_TO_POOL = ThrowableUtil.unknownStackTrace(new System.InvalidOperationException("Channel is unhealthy not offering it back to pool"), typeof(ChildChannelPool), "releaseAndOffer(...)");

        private readonly IQueue<IChannel> store;
        private readonly IChannelPoolHandler Handler;
        private readonly IChannelHealthChecker HealthChecker;
        private readonly Bootstrap bootstrap;
        private readonly bool ReleaseHealthCheck;
        private readonly IChannelPool parentPool;

        private long timeout = 4000;

        /// <summary>
        /// Creates a new channel pool.
        /// </summary>
        /// <param name="bootstrap"> the <seealso cref="Bootstrap"/> used to create promises </param>
        /// <param name="parentPool"> the parent pool providing the channels to the child </param>
        /// <param name="handler"> the <seealso cref="ChannelPoolHandler"/> that will be notified for the different pool actions </param>
        public ChildChannelPool(Bootstrap bootstrap, IChannelPool parentPool, IChannelPoolHandler handler)
        {
            Contract.Requires(handler != null);
            Contract.Requires(parentPool != null);
            Contract.Requires(bootstrap != null);

            this.Handler = handler;
            this.parentPool = parentPool;
            this.bootstrap = bootstrap;

            this.HealthChecker = ChannelActiveHealthChecker.Instance;
            this.ReleaseHealthCheck = true;

            this.store = (IQueue<IChannel>)new CompatibleConcurrentStack<IChannel>();
        }

        public async virtual ValueTask<IChannel> AcquireNewOr(long timeout)
        {
            IChannel chl;
            bool b = this.store.TryDequeue(out chl);


            this.timeout = timeout;
            if (!b)
            {
                Bootstrap bs = this.bootstrap.Clone();
                //Bootstrap bs =  new Bootstrap();
                bs.Attribute(PoolKey, this);

                log.Debug(" ... try get parent pool channel ... " + parentPool);
                try
                {
                    IChannel chnl = await parentPool.AcquireAsync();
                    log.Debug(" ... Channel " + chnl);

                    await ConnectChannel(chnl, timeout);

                    return chnl;
                }
                catch (Exception e)
                {
                    log.Error("Something went wromg here: .. " + e.Message);
                }

                return null;
            }

            IEventLoop eventLoop = chl.EventLoop;
            if (eventLoop.InEventLoop)
            {
                return await this.DoHealthCheck(chl, timeout);
            }
            else
            {
                var completionSource = new TaskCompletionSource<IChannel>();
                eventLoop.Execute(this.DoHealthCheck, chl, completionSource);
                return await completionSource.Task;
            }
            // return acquire(bootstrap.Config().group().next().newPromise<IChannel>());
        }

        protected abstract Task ConnectChannel(IChannel chnl, long timeout);

        async void DoHealthCheck(object channel, object state)
        {
            var promise = state as TaskCompletionSource<IChannel>;
            try
            {
                var result = await this.DoHealthCheck((IChannel)channel, this.timeout);
                promise.TrySetResult(result);
            }
            catch (Exception ex)
            {
                promise.TrySetException(ex);
            }
        }

        async ValueTask<IChannel> DoHealthCheck(IChannel channel, long timeout)
        {
            Contract.Assert(channel.EventLoop.InEventLoop);
            try
            {
                if (await this.HealthChecker.IsHealthyAsync(channel))
                {
                    try
                    {
                        channel.GetAttribute(PoolKey).Set(this);
                        this.Handler.ChannelAcquired(channel);
                        return channel;
                    }
                    catch (Exception)
                    {
                        CloseChannel(channel);
                        throw;
                    }
                }
                else
                {
                    CloseChannel(channel);
                    return await AcquireNewOr(timeout);
                }
            }
            catch
            {
                CloseChannel(channel);
                return await AcquireNewOr(timeout);
            }
        }

        static void CloseChannel(IChannel channel)
        {
            channel.GetAttribute(PoolKey).GetAndSet(null);
            channel.CloseAsync();
        }

        public async ValueTask<bool> ReleaseAsync(IChannel channel)
        {
            Contract.Requires(channel != null);

            log.Debug("ReleaseAsync for " + channel.Id);

            try
            {
                IEventLoop loop = channel.EventLoop;

                log.Debug("ReleaseAsync -0- for " + loop.InEventLoop);

                if (loop.InEventLoop)
                {
                    return await this.DoReleaseChannel(channel);
                }
                else
                {
                    var promise = new TaskCompletionSource<bool>();
                    loop.Execute(this.DoReleaseChannel, channel, promise);
                    return await promise.Task;
                }
            }
            catch (Exception)
            {
                CloseChannel(channel);
                throw;
            }
        }

        async void DoReleaseChannel(object channel, object state)
        {
            var promise = state as TaskCompletionSource<bool>;
            try
            {
                var result = await this.DoReleaseChannel((IChannel)channel);
                promise.TrySetResult(result);
            }
            catch (Exception ex)
            {
                promise.TrySetException(ex);
            }
        }

        async ValueTask<bool> DoReleaseChannel(IChannel channel)
        {

            log.Debug("DoReleaseChannel -0- for " + channel.Id);

            Contract.Assert(channel.EventLoop.InEventLoop);

            log.Debug("DoReleaseChannel -1- for " + PoolKey);

            try
            {

                log.Debug("DoReleaseChannel -- " + this.ReleaseHealthCheck);

                if (this.ReleaseHealthCheck)
                {
                    return await this.DoHealthCheckOnRelease(channel);
                }
                else
                {
                    this.ReleaseAndOffer(channel);
                    return true;
                }
            }
            catch
            {
                CloseChannel(channel);
                throw;
            }
        }

        /// <summary>
        /// Releases the channel back to the pool only if the channel is healthy.
        /// </summary>
        /// <param name="channel">The <see cref="IChannel"/> to put back to the pool.</param>
        /// <returns>
        /// <c>true</c> if the <see cref="IChannel"/> was healthy, released, and offered back to the pool.
        /// <c>false</c> if the <see cref="IChannel"/> was NOT healthy and was simply released.
        /// </returns>
        async ValueTask<bool> DoHealthCheckOnRelease(IChannel channel)
        {
            if (await this.HealthChecker.IsHealthyAsync(channel))
            {
                //channel turns out to be healthy, offering and releasing it.
                this.ReleaseAndOffer(channel);
                return true;
            }
            else
            {
                //channel not healthy, just releasing it.
                this.Handler.ChannelReleased(channel);
                return false;
            }
        }

        void ReleaseAndOffer(IChannel channel)
        {
            if (this.TryOfferChannel(channel))
            {

                log.Debug("ChannelReleased " + channel.Id);

                this.Handler.ChannelReleased(channel);
            }
            else
            {
                CloseChannel(channel);
                throw FULL_EXCEPTION;
            }
        }


        /// <summary>
        /// Polls an <see cref="IChannel"/> out of the internal storage to reuse it.
        /// </summary>
        /// <remarks>
        /// Sub-classes may override <see cref="TryPollChannel"/> and <see cref="TryOfferChannel"/>.
        /// Be aware that implementations of these methods needs to be thread-safe!
        /// </remarks>
        /// <param name="channel">
        /// An output parameter that will contain the <see cref="IChannel"/> obtained from the pool.
        /// </param>
        /// <returns>
        /// <c>true</c> if an <see cref="IChannel"/> was retrieved from the pool, otherwise <c>false</c>.
        /// </returns>
        protected virtual bool TryPollChannel(out IChannel channel) => this.store.TryDequeue(out channel);

        /// <summary>
        /// Offers a <see cref="IChannel"/> back to the internal storage. This will return 
        /// </summary>
        /// <remarks>
        /// Sub-classes may override <see cref="TryPollChannel"/> and <see cref="TryOfferChannel"/>.
        /// Be aware that implementations of these methods needs to be thread-safe!
        /// </remarks>
        /// <param name="channel"></param>
        /// <returns><c>true</c> if the <see cref="IChannel"/> could be added, otherwise <c>false</c>.</returns>
        protected virtual bool TryOfferChannel(IChannel channel) => this.store.TryEnqueue(channel);

        public void close()
        {
            for (; ; )
            {
                IChannel channel;
                TryPollChannel(out channel);
                if (channel == null)
                {
                    break;
                }
                channel.CloseAsync();
            }
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        ValueTask<IChannel> IChannelPool.AcquireAsync()
        {
            return AcquireNewOr(this.timeout);
        }

        void IDisposable.Dispose()
        {
            while (this.TryPollChannel(out IChannel channel))
            {
                channel.CloseAsync();
            }
        }
    }

    class CompatibleConcurrentStack<T> : ConcurrentStack<T>, IQueue<T>
    {
        public bool TryEnqueue(T item)
        {
            this.Push(item);
            return true;
        }

        public bool TryDequeue(out T item) => this.TryPop(out item);
    }

}