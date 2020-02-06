using DotNetty.Common.Concurrency;
using DotNetty.Common.Utilities;
using DotNetty.Transport.Channels;
using DotNetty.Transport.Channels.Pool;
using Lightstreamer.DotNet.Logging.Log;
using System;
using System.Configuration;
using System.Diagnostics;

namespace com.lightstreamer.client.transport.providers.netty
{

    /// <summary>
    /// A channel pool handler which closes a socket when the socket it is idle for a while.
    /// <para>
    /// The strategy of automatic closing of the idle sockets rests on the following assumptions:
    /// <ol>
    /// <li>each channel has its own instance of <seealso cref="IdleStateTimer"/> as a channel attribute with key <seealso cref="#IDLE_KEY"/></li>
    /// <li>a timer is started when a channel is released to the pool (see <seealso cref="IdleStateTimer#setIdle()"/>)</li>
    /// <li>the timer is stopped when the channel is created/acquired (see <seealso cref="IdleStateTimer#setActive()"/>)</li>
    /// </ol>
    /// </para>
    /// </summary>
    public class BaseChannelPoolHandler : IChannelPoolHandler
    {

        private static readonly ILogger log = LogManager.GetLogger(Constants.NETTY_POOL_LOG);

        /// <summary>
        /// Name of the attribute pointing to the idle state handler of a channel.
        /// </summary>
        private static readonly AttributeKey<IdleStateTimer> IDLE_KEY = AttributeKey<IdleStateTimer>.NewInstance("idleStateTimer");

        /// <summary>
        /// Number of nanoseconds after which an idle socket is closed.
        /// </summary>
        private static readonly long closeSocketTimeoutNs;

        static BaseChannelPoolHandler()
        {
            /*
			 * If the system variable "com.lightstreamer.socket.pooling" is set to false, the sockets are closed
			 * immediately after they are released to the pool.
			 */

            try
            {
                System.Collections.Specialized.NameValueCollection appSettings = ConfigurationManager.AppSettings;
                string socketpooling = appSettings["com.lightstreamer.socket.pooling"];

                if ("false".Equals(socketpooling) || "".Equals(socketpooling))
                {
                    log.Warn("Socket pooling is disabled");
                    closeSocketTimeoutNs = 0;
                }
                else
                {
                    closeSocketTimeoutNs = Constants.CLOSE_SOCKET_TIMEOUT_MILLIS * 1_000_000L;
                }
            }
            catch (Exception e)
            {
                log.Debug("Error " + e.Message + " - " + e.StackTrace);
                log.Warn("Socket pooling is disabled");
                closeSocketTimeoutNs = 0;
            }
        }

        public virtual void ChannelReleased(IChannel channel)
        {
            IdleStateTimer idls = (IdleStateTimer)channel.GetAttribute(IDLE_KEY);
            idls.setIdle();
        }

        public virtual void ChannelAcquired(IChannel channel)
        {
            IdleStateTimer idls = (IdleStateTimer)channel.GetAttribute(IDLE_KEY);
            idls.setActive();
        }

        public virtual void ChannelCreated(IChannel channel)
        {

            if (!channel.HasAttribute(IDLE_KEY))
            {
                channel.GetAttribute(IDLE_KEY).Set(new IdleStateTimer(channel));

            }
            IAttribute<IdleStateTimer> ikey = (IAttribute<IdleStateTimer>)channel.GetAttribute(IDLE_KEY);
            IdleStateTimer idls = (IdleStateTimer)ikey.Get();
            idls.setActive();
        }


        /// <summary>
        /// Timer closing idle channels.
        /// </summary>
        private class IdleStateTimer : IRunnable
        {

            internal bool idle;
            internal readonly IChannel ch;
            Stopwatch stopwatch = new Stopwatch();

            public IdleStateTimer(IChannel ch)
            {
                this.ch = ch;
            }

            public IdleStateTimer()
            {
                //this.ch = ch;
            }

            /// <summary>
            /// Sets the channel as active to disable the automatic closing.
            /// </summary>
            public virtual void setActive()
            {
                lock (this)
                {
                    idle = false;
                }
            }

            /// <summary>
            /// Sets the channel as idle. If the channel stays idle longer than <seealso cref="Constants#CLOSE_SOCKET_TIMEOUT_MILLIS"/>,
            /// the channel is closed.
            /// </summary>
            public virtual void setIdle()
            {
                lock (this)
                {
                    idle = true;
                    if (stopwatch.IsRunning)
                    {
                        stopwatch.Restart();
                    }
                    else
                    {
                        stopwatch.Start();
                    }

                    // the scheduler runs the method run() below
                    if (closeSocketTimeoutNs > 0)
                    {
                        ch.EventLoop.Schedule(this, new System.TimeSpan(closeSocketTimeoutNs / 1_000_000L));

                    }
                    else
                    {
                        /* socket pooling is disabled */
                        ch.CloseAsync();
                        if (log.IsDebugEnabled)
                        {
                            log.Debug("Channel closed [" + ch.Id + "]");
                        }
                    }
                }
            }

            public void Run()
            {
                lock (this)
                {


                    long elapsedNs = stopwatch.ElapsedTicks;
                    if (idle && elapsedNs >= closeSocketTimeoutNs)
                    {
                        ch.CloseAsync();
                        if (log.IsDebugEnabled)
                        {
                            log.Debug("Channel closed [" + ch.Id + "]");
                        }

                    }
                    else
                    {
                        if (log.IsDebugEnabled)
                        {
                            log.Debug("Postpone close [" + ch.Id + "] idle=" + idle + " elapsed=" + elapsedNs);
                        }
                    }
                }
            }
        }
    } // IdleStateTimer
}
