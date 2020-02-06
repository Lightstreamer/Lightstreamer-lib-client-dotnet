using System;

/*
 * Copyright (c) 2004-2019 Lightstreamer s.r.l., Via Campanini, 6 - 20124 Milano, Italy.
 * All rights reserved.
 * www.lightstreamer.com
 *
 * This software is the confidential and proprietary information of
 * Lightstreamer s.r.l.
 * You shall not disclose such Confidential Information and shall use it
 * only in accordance with the terms of the license agreement you entered
 * into with Lightstreamer s.r.l.
 */
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