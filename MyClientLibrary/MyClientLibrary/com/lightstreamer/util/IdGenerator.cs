using System.Threading;

namespace com.lightstreamer.util
{
    public class IdGenerator
    {
        private static long requestIdGenerator = 0;

        /// <summary>
        /// Generates the next request id used as the value of the parameter LS_reqId.
        /// </summary>
        public static long NextRequestId
        {
            get
            {
                return Interlocked.Increment(ref requestIdGenerator);
            }
        }

        private static int subscriptionIdGenerator = 0;

        /// <summary>
        /// Generates the next subscription id used as the value of the parameter LS_subId.
        /// </summary>
        public static int NextSubscriptionId
        {
            get
            {
                return Interlocked.Increment(ref subscriptionIdGenerator);
            }
        }
    }
}