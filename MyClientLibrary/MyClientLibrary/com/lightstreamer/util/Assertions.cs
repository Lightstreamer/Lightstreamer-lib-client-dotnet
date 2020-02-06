using Lightstreamer.DotNet.Logging.Log;
using System.Threading;

namespace com.lightstreamer.util
{
    public class Assertions
    {
        private static readonly ILogger log = LogManager.GetLogger("ASSERT");

        public static bool SessionThread
        {
            get
            {
                if (!Thread.CurrentThread.Name.StartsWith("Session Thread"))
                {
                    log.Error("The method must be called by Session Thread. Instead the caller is " + Thread.CurrentThread);
                    return false;
                }
                return true;
            }
        }

        public static bool EventThread
        {
            get
            {
                if (!Thread.CurrentThread.Name.StartsWith("Events Thread"))
                {
                    //log.error("The method must be called by Event Thread. Instead the caller is " + Thread.currentThread());
                    return false;
                }
                return true;
            }
        }

        public static bool NettyThread
        {
            get
            {
                if (!Thread.CurrentThread.Name.StartsWith("Netty Thread"))
                {
                    //log.error("The method must be called by Netty Thread. Instead the caller is " + Thread.currentThread());
                    return false;
                }
                return true;
            }
        }

        /// <summary>
        /// Conditional operator.
        /// </summary>
        public static bool implies(bool a, bool b)
        {
            return !a || b;
        }

        /// <summary>
        /// Biconditional operator.
        /// </summary>
        public static bool iff(bool a, bool b)
        {
            return a == b;
        }
    }
}