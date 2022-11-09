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
