using com.lightstreamer.client.transport.providers.netty.pool;
using Lightstreamer.DotNet.Logging.Log;
using System;

namespace com.lightstreamer.client.transport.providers.netty
{

    /// <summary>
    /// Factory returning objects which must be singletons.
    /// </summary>
    public class SingletonFactory
    {
        private static readonly ILogger log = LogManager.GetLogger(Constants.NETTY_POOL_LOG);

        public static readonly SingletonFactory instance = new SingletonFactory();

        private HttpPoolManager httpPool;
        private WebSocketPoolManager wsPool;

        private SingletonFactory()
        {
            httpPool = new HttpPoolManager();
            try
            {
                wsPool = new WebSocketPoolManager(httpPool);
            }
            catch (Exception e)
            {
                log.Debug("Error: " + e.Message + ", " + e.StackTrace);

            }
        }

        /// <summary>
        /// Returns the global HTTP pool.
        /// </summary>
        public virtual HttpPoolManager HttpPool
        {
            get
            {
                return httpPool;
            }
        }

        /// <summary>
        /// Returns the global WebSocket pool.
        /// </summary>
        public virtual WebSocketPoolManager WsPool
        {
            get
            {
                return wsPool;
            }
        }

        /// <summary>
        /// Releases the resources acquired by the singletons.
        /// </summary>
        public virtual async System.Threading.Tasks.Task closeAsync()
        {
            /*
			 * NB WebSocket pool depends on HTTP pool, so it must be closed firstly
			 */
            wsPool.Dispose();
            await httpPool.closeAsync();
        }
    }
}