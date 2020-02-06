using DotNetty.Codecs.Http;
using DotNetty.Codecs.Http.WebSockets.Extensions.Compression;
using DotNetty.Handlers.Proxy;
using DotNetty.Handlers.Tls;
using DotNetty.Transport.Channels;
using Lightstreamer.DotNet.Logging.Log;

namespace com.lightstreamer.client.transport.providers.netty
{
    /// <summary>
    /// Utilities managing the pipeline of channels living in a pool where channel created to send HTTP request can be
    /// converted to send WebSocket messages after the channel upgrade 
    /// (see <seealso cref="HttpPoolManager"/> and <seealso cref="WebSocketPoolManager"/>).
    /// <para>
    /// The typical life-cycle of a channel is the following:
    /// <ul>
    /// <li>the channel is created to send a HTTP request and a HTTP user-defined handler is added to the pipeline 
    /// (see <seealso cref="#populateHttpPipeline(Channel, NettyFullAddress, ChannelHandler)"/>)</li>
    /// <li>the channel is used and then released to its HTTP pool</li>
    /// <li>the channel is acquired from the pool and upgraded to WebSocket (see <seealso cref="#populateWSPipelineForHandshake(Channel, ChannelHandler)"/>)</li>
    /// <li>when the handshake is complete, the WebSocket user-defined handler is added to the pipeline (see <seealso cref="#populateWSPipeline(Channel, ChannelHandler)"/>)</li>
    /// <li>the channel is used and then released to its WebSocket pool</li>
    /// </ul>
    /// </para>
    /// </summary>
    public class PipelineUtils
    {
        /// <summary>
        /// Name of the channel handler in a pipeline reading TLCP incoming messages.
        /// </summary>
        private const string READER_KEY = "reader";

        protected internal readonly static ILogger log = LogManager.GetLogger(Constants.TRANSPORT_LOG);


        /// <summary>
        /// Gets the channel handler reading TLCP incoming messages.
        /// </summary>
        public static IChannelHandler getChannelHandler(IChannel ch)
        {
            return ch.Pipeline.Get(READER_KEY);
        }

        /// <summary>
        /// Populates the channel pipeline in order to read data from a HTTP connection. 
        /// </summary>
        public static void populateHttpPipeline(IChannel ch, NettyFullAddress remoteAddress, IChannelHandler httpChHandler)
        {
            IChannelPipeline pipeline = ch.Pipeline;

            ProxyHandler proxy = remoteAddress.Proxy;
            if (proxy != null)
            {

                log.Info("Add Proxy: " + proxy.ProxyAddress);

                pipeline.AddLast("proxy", proxy);
            }

            if (remoteAddress.Secure)
            {
                pipeline.AddLast("tls", new TlsHandler(new ClientTlsSettings(remoteAddress.HostName)));
            }

            pipeline.AddLast("http", new HttpClientCodec());

            pipeline.AddLast(READER_KEY, httpChHandler);
        }

        /// <summary>
        /// Populates the channel pipeline in order to upgrade a connection to WebSocket.
        /// </summary>
        public static void populateWSPipelineForHandshake(IChannel ch, IChannelHandler wsHandshakeHandler)
        {
            IChannelPipeline p = ch.Pipeline;
            /*
			 * Note: since the channel pipeline was filled by populateHttpPipeline(), 
			 * we must remove the HTTP user-defined handler before of upgrading the channel
			 */
            p.Remove(READER_KEY);
            p.AddLast(new HttpObjectAggregator(8192));
            p.AddLast(WebSocketClientCompressionHandler.Instance);
            p.AddLast(wsHandshakeHandler);
        }

        /// <summary>
        /// Populates the channel pipeline in order to read data from a WebSocket connection. 
        /// </summary>
        public static void populateWSPipeline(IChannel ch, IChannelHandler wsChHandler)
        {
            IChannelPipeline chPipeline = ch.Pipeline;
            IChannelHandler reader = chPipeline.Get(READER_KEY);
            if (reader == null)
            {
                /*
				 * there is no reader when the WebSocket channel is fresh, 
				 * i.e the channel was filled by populateWSPipelineForHandshake()
				 */
                chPipeline.AddLast(READER_KEY, wsChHandler);
            }
            else
            {
                /*
				 * the old reader is the WebSocket channel handler used before the channel was released to the pool,
				 * i.e the channel was already filled by populateWSPipeline()
				 */
                chPipeline.Replace(READER_KEY, READER_KEY, wsChHandler);
            }
        }
    }
}