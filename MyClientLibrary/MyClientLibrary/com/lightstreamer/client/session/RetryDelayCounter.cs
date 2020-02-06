using Lightstreamer.DotNet.Logging.Log;
using System;

namespace com.lightstreamer.client.session
{

    /// <summary>
    /// Computes <code>currentRetryDelay</code> (which is the same as <code>currentConnectTimeout</code>) 
    /// in the following way:
    /// <ul>
    /// <li>the first 10 times when increase() is called, currentRetryDelay equals retryDelay</li>
    /// <li>the next times, currentRetryDelay is doubled until it reaches the value of 60s</li>
    /// </ul>
    /// </summary>
    public class RetryDelayCounter
    {

        private static readonly ILogger log = LogManager.GetLogger(Constants.SESSION_LOG);

        private int attempt;
        private long minDelay;
        private long maxDelay;
        private long currentDelay;

        public RetryDelayCounter(long delay)
        {
            init(delay);
        }

        /// <summary>
        /// Resets retryDelay and currentRetryDelay (and currentConnectTimeout).
        /// </summary>
        public virtual void reset(long delay)
        {
            init(delay);
            if (log.IsDebugEnabled)
            {
                log.Debug("Reset currentRetryDelay: " + currentDelay);
            }
        }

        /// <summary>
        /// Increase currentRetryDelay (and currentConnectTimeout).
        /// </summary>
        public virtual void increase()
        {
            if (attempt >= 9 && currentDelay < maxDelay)
            {
                currentDelay *= 2;
                if (currentDelay > maxDelay)
                {
                    currentDelay = maxDelay;
                }
                if (log.IsDebugEnabled)
                {
                    log.Debug("Increase currentRetryDelay: " + currentDelay);
                }
            }
            attempt++;
        }

        public virtual long CurrentRetryDelay
        {
            get
            {
                return currentDelay;
            }
        }

        public virtual long RetryDelay
        {
            get
            {
                return minDelay;
            }
        }

        /// <summary>
        /// Initializes retryDelay and currentRetryDelay (and currentConnectTimeout).
        /// </summary>
        private void init(long retryDelay)
        {
            this.currentDelay = retryDelay;
            this.minDelay = retryDelay;
            this.maxDelay = Math.Max(60_000, retryDelay);
            this.attempt = 0;
        }
    }

}