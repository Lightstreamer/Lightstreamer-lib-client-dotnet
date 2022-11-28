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

using System;

namespace com.lightstreamer.client.session
{
    /// 
    internal class RequestsHelper
    {

        private const string HTTPS = "https://";
        private const string HTTP = "http://";

        /// <param name="addressToUse"> </param>
        /// <param name="controlLink">
        /// @return </param>
        public static string completeControlLink(string extractFrom, string controlLink)
        {

            string port = extractPort(extractFrom, extractFrom.IndexOf("://", StringComparison.Ordinal));

            if (!string.ReferenceEquals(port, null))
            {
                int slIndex = controlLink.IndexOf("/", StringComparison.Ordinal);
                if (slIndex <= -1)
                {
                    controlLink += port;
                }
                else
                {
                    controlLink = controlLink.Substring(0, slIndex) + port + controlLink.Substring(slIndex);
                }
            }

            if (extractFrom.ToLower().IndexOf(HTTPS, StringComparison.Ordinal) == 0)
            {
                controlLink = HTTPS + controlLink;
            }
            else
            {
                controlLink = HTTP + controlLink;
            }

            if (!controlLink.EndsWith("/", StringComparison.Ordinal))
            {
                controlLink += "/";
            }

            return controlLink;

        }

        private static string extractPort(string extractFrom, int protLoc)
        {
            int portStarts = extractFrom.IndexOf(":", protLoc + 1, StringComparison.Ordinal);
            if (portStarts <= -1)
            {
                return null;
            }

            if (extractFrom.IndexOf("]", StringComparison.Ordinal) > -1)
            {
                portStarts = extractFrom.IndexOf("]:", StringComparison.Ordinal);
                if (portStarts <= -1)
                {
                    return null;
                }
                portStarts += 1;

            }
            else if (portStarts != extractFrom.LastIndexOf(":", StringComparison.Ordinal))
            {
                return null;
            }


            int portEnds = extractFrom.IndexOf("/", protLoc + 3, StringComparison.Ordinal);

            return portEnds > -1 ? extractFrom.Substring(portStarts, portEnds - portStarts) : extractFrom.Substring(portStarts);
        }

    }

}