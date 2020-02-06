using System.Net;
using com.lightstreamer.client;
using Lightstreamer.DotNet.Logging.Log;

using static com.lightstreamer.client.platform_data.offline.OfflineStatus;

namespace Lightstreamer_DotNet_Client_Unified.com.lightstreamer.client.platform_data.offline
{
    public class CSOfflineStatus : OfflineStatusInterface
    {

        private static readonly ILogger log = LogManager.GetLogger(Constants.TRANSPORT_LOG);

        public virtual bool isOffline(string server)
        {
            try
            {

                log.Debug("IsOffline check now ... ");

                using (var client = new WebClient())
                using (client.OpenRead("http://clients3.google.com/generate_204"))
                {
                    log.Debug(" ... online, go!");

                    return false;
                }
            }
            catch
            {

                log.Debug(" ... offline!");

                return true;
            }
        }
    }
}
