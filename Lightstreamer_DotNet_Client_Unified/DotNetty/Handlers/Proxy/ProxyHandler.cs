using DotNetty.Common.Concurrency;
using DotNetty.Common.Utilities;
using DotNetty.Transport.Channels;

using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace DotNetty.Handlers.Proxy
{
    public abstract class ProxyHandler : ChannelDuplexHandler
    {
        private bool InstanceFieldsInitialized = false;

        /// <summary>
        /// The default connect timeout: 10 seconds.
        /// </summary>
        private const int DEFAULT_CONNECT_TIMEOUT_MILLIS = 10000;

        /// <summary>
        /// A string that signifies 'no authentication' or 'anonymous'.
        /// </summary>
        internal const string AUTH_NONE = "none";

        private readonly EndPoint proxyAddress;
        private volatile EndPoint destinationAddress;
        private volatile string originalDestination;
        private int connectTimeoutMillis = DEFAULT_CONNECT_TIMEOUT_MILLIS;

        private volatile IChannelHandlerContext ctx_c;
        private PendingWriteQueue pendingWrites;
        private bool finished;
        private bool suppressChannelReadComplete;
        private bool flushedPrematurely;

        private Task connectPromise;

        private IScheduledTask connectTimeoutFuture;

        protected internal ProxyHandler(EndPoint proxyAddress)
        {
            if (!InstanceFieldsInitialized)
            {
                InstanceFieldsInitialized = true;
            }
            if (proxyAddress == null)
            {
                throw new System.NullReferenceException("proxyAddress");
            }
            this.proxyAddress = proxyAddress;
        }

        /// <summary>
        /// Returns the name of the proxy protocol in use.
        /// </summary>
        public abstract string protocol();

        /// <summary>
        /// Returns the name of the authentication scheme in use.
        /// </summary>
        public abstract string authScheme();

        /// <summary>
        /// Returns {@code true} if and only if the connection to the destination has been established successfully.
        /// </summary>
        public bool Connected
        {
            get
            {
                return connectPromise.IsCompleted;
            }
        }

        /// <summary>
        /// Sets the connect timeout in millis.  If the connection attempt to the destination does not finish within
        /// the timeout, the connection attempt will be failed.
        /// </summary>
        public int ConnectTimeoutMillis
        {
            set
            {
                if (value <= 0)
                {
                    value = 0;
                }

                this.connectTimeoutMillis = value;
            }
            get
            {
                return this.connectTimeoutMillis;
            }

        }

        public override void HandlerAdded(IChannelHandlerContext context)
        {
            // ctx = context;
            addCodec(context);

            if (context.Channel.Active)
            {
                // channelActive() event has been fired already, which means this.channelActive() will
                // not be invoked. We have to initialize here instead.
                sendInitialMessage(context);
            }
            else
            {
                // channelActive() event has not been fired yet.  this.channelOpen() will be invoked
                // and initialization will occur there.
            }
        }

        /// <summary>
        /// Adds the codec handlers required to communicate with the proxy server.
        /// </summary>
        protected internal abstract void addCodec(IChannelHandlerContext ctx);

        /// <summary>
        /// Removes the encoders added in <seealso cref="#addCodec(ChannelHandlerContext)"/>.
        /// </summary>
        protected internal abstract void removeEncoder(IChannelHandlerContext ctx);

        /// <summary>
        /// Removes the decoders added in <seealso cref="#addCodec(ChannelHandlerContext)"/>.
        /// </summary>
        protected internal abstract void removeDecoder(IChannelHandlerContext ctx);


        public override Task ConnectAsync(IChannelHandlerContext context, EndPoint remoteAddress, EndPoint localAddress)
        {

            if (DestinationAddress != null)
            {
                return null;
            }

            DestinationAddress = remoteAddress;
            connectPromise = context.ConnectAsync(ProxyAddress, localAddress);
            return connectPromise;
        }

        public override void ChannelActive(IChannelHandlerContext context)
        {
            sendInitialMessage(context);

            context.FireChannelActive();
        }

        /// <summary>
        /// Sends the initial message to be sent to the proxy server. This method also starts a timeout task which marks
        /// the <seealso cref="#connectPromise"/> as failure if the connection attempt does not success within the timeout.
        /// </summary>
        private void sendInitialMessage(IChannelHandlerContext context)
        {
            long connectTimeoutMillis = this.connectTimeoutMillis;
            if (connectTimeoutMillis > 0)
            {
                connectTimeoutFuture = context.Executor.Schedule(() =>
                {
                    if (!connectPromise.IsCompleted)
                    {
                        ctx_c = context;
                        ConnectFailure = new Exception(exceptionMessage("timeout"));
                    }
                }, new TimeSpan(0, 0, 0, ConnectTimeoutMillis));
            }

            object initialMessage = newInitialMessage(context);
            if (initialMessage != null)
            {
                sendToProxyServer(initialMessage, context);
            }

            readIfNeeded(context);
        }

        /// <summary>
        /// Returns a new message that is sent at first time when the connection to the proxy server has been established.
        /// </summary>
        /// <returns> the initial message, or {@code null} if the proxy server is expected to send the first message instead </returns>
        protected internal abstract object newInitialMessage(IChannelHandlerContext ctx);

        /// <summary>
        /// Sends the specified message to the proxy server.  Use this method to send a response to the proxy server in
        /// <seealso cref="#handleResponse(ChannelHandlerContext, Object)"/>.
        /// </summary>
        protected internal void sendToProxyServer(object msg, IChannelHandlerContext context)
        {
            Task writeTask = context.WriteAndFlushAsync(msg);
            writeTask.ContinueWith(t =>
            {

                if (!( t.Status == TaskStatus.RanToCompletion ))
                {
                    ctx_c = context;
                    ConnectFailure = t.Exception;
                }
            });
        }

        public override void ChannelInactive(IChannelHandlerContext context)
        {
            if (finished)
            {
                context.FireChannelInactive();
            }
            else
            {
                // Disconnected before connected to the destination.
                ctx_c = context;
                ConnectFailure = new ProxyConnectException("Disconnected", new Exception("disconnected"));
            }
        }

        public override void ExceptionCaught(IChannelHandlerContext context, Exception cause)
        {
            if (finished)
            {
                context.FireExceptionCaught(cause);
            }
            else
            {
                // Exception was raised before the connection attempt is finished.
                ctx_c = context;
                ConnectFailure = cause;
            }
        }

        public override void ChannelRead(IChannelHandlerContext context, object msg)
        {

            if (finished)
            {
                // Received a message after the connection has been established; pass through.
                suppressChannelReadComplete = false;
                context.FireChannelRead(msg);
            }
            else
            {
                suppressChannelReadComplete = true;
                Exception cause = null;
                try
                {
                    bool done = handleResponse(context, msg);
                    if (done)
                    {
                        setConnectSuccess(context);
                    }
                }
                catch (Exception t)
                {
                    cause = t;
                }
                finally
                {
                    ReferenceCountUtil.Release(msg);
                    if (cause != null)
                    {
                        ctx_c = context;
                        ConnectFailure = cause;
                    }
                }
            }
        }

        /// <summary>
        /// Handles the message received from the proxy server.
        /// </summary>
        /// <returns> {@code true} if the connection to the destination has been established,
        ///         {@code false} if the connection to the destination has not been established and more messages are
        ///         expected from the proxy server </returns>
        protected internal abstract bool handleResponse(IChannelHandlerContext context, object response);

        private void setConnectSuccess(IChannelHandlerContext context)
        {
            finished = true;

            cancelConnectTimeoutFuture();

            if (connectPromise.IsCompleted)
            {
                bool removedCodec = true;

                removedCodec &= safeRemoveEncoder(context);

                context.FireUserEventTriggered(new ProxyConnectionEvent(protocol(), authScheme(), proxyAddress, destinationAddress));

                removedCodec &= safeRemoveDecoder(context);

                if (removedCodec)
                {
                    writePendingWrites();

                    if (flushedPrematurely)
                    {
                        context.Flush();
                    }
                    //connectPromise.TrySetResult(ctx.Channel);
                }
                else
                {
                    // We are at inconsistent state because we failed to remove all codec handlers.
                    Exception cause = new ProxyConnectException("failed to remove all codec handlers added by the proxy handler; bug?", new Exception("failed to remove all codec handlers added by the proxy handler; bug?"));

                    failPendingWritesAndClose(cause);
                }
            }
        }

        private bool safeRemoveDecoder(IChannelHandlerContext context)
        {
            try
            {
                removeDecoder(context);
                return true;
            }
            catch (Exception e)
            {
                // log.Warn("Failed to remove proxy decoders:", e);
            }

            return false;
        }

        private bool safeRemoveEncoder(IChannelHandlerContext context)
        {
            try
            {
                removeEncoder(context);
                return true;
            }
            catch (Exception e)
            {
                // log.Warn("Failed to remove proxy encoders:", e);
            }

            return false;
        }

        private Exception ConnectFailure
        {
            set
            {
                finished = true;
                cancelConnectTimeoutFuture();

                if (!( connectPromise.Status == TaskStatus.RanToCompletion ))
                {

                    if (!( value is ProxyConnectException ))
                    {
                        value = new ProxyConnectException(exceptionMessage(value.ToString()), value);
                    }

                    safeRemoveDecoder(ctx_c);
                    safeRemoveEncoder(ctx_c);
                    failPendingWritesAndClose(value);
                }
            }
        }

        public EndPoint ProxyAddress => proxyAddress;

        public EndPoint DestinationAddress { get => destinationAddress; set => destinationAddress = value; }
        public int ConnectTimeoutMillis1 { get => this.connectTimeoutMillis; set => this.connectTimeoutMillis = value; }

        private void failPendingWritesAndClose(Exception cause)
        {
            failPendingWrites(cause);
            // connectPromise.TrySetException(cause);
            ctx_c.FireExceptionCaught(cause);
            ctx_c.CloseAsync();
        }

        private void cancelConnectTimeoutFuture()
        {
            if (connectTimeoutFuture != null)
            {
                connectTimeoutFuture.Cancel();
                connectTimeoutFuture = null;
            }
        }

        /// <summary>
        /// Decorates the specified exception message with the common information such as the current protocol,
        /// authentication scheme, proxy address, and destination address.
        /// </summary>
        protected internal string exceptionMessage(string msg)
        {
            if (string.ReferenceEquals(msg, null))
            {
                msg = "";
            }

            StringBuilder buf = ( new StringBuilder(128 + msg.Length) ).Append(protocol()).Append(", ").Append(authScheme()).Append(", ").Append(ProxyAddress).Append(" => ").Append(DestinationAddress);
            if (msg.Length > 0)
            {
                buf.Append(", ").Append(msg);
            }

            return buf.ToString();
        }

        public override void ChannelReadComplete(IChannelHandlerContext ctx)
        {
            if (suppressChannelReadComplete)
            {
                suppressChannelReadComplete = false;

                readIfNeeded(ctx);
            }
            else
            {
                ctx.FireChannelReadComplete();
            }
        }

        public override Task WriteAsync(IChannelHandlerContext context, object message)
        {
            if (finished)
            {
                writePendingWrites();
                return context.WriteAsync(message);
            }
            else
            {
                return addPendingWrite(context, message);
            }
        }

        public override void Flush(IChannelHandlerContext context)
        {
            if (finished)
            {
                writePendingWrites();
                context.Flush();
            }
            else
            {
                flushedPrematurely = true;
            }
        }

        private static void readIfNeeded(IChannelHandlerContext ctx)
        {
            if (!ctx.Channel.Configuration.AutoRead)
            {
                ctx.Read();
            }
        }

        private void writePendingWrites()
        {
            if (pendingWrites != null)
            {
                pendingWrites.RemoveAndWriteAllAsync();
                pendingWrites = null;
            }
        }

        private void failPendingWrites(Exception cause)
        {
            if (pendingWrites != null)
            {
                pendingWrites.RemoveAndFailAll(cause);
                pendingWrites = null;
            }
        }

        private Task addPendingWrite(IChannelHandlerContext ctx, object msg)
        {

            if (pendingWrites == null)
            {
                pendingWrites = new PendingWriteQueue(ctx);
            }
            return pendingWrites.Add(msg);
        }
    }
}