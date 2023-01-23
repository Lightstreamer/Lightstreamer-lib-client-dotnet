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

// using CookieManager;
using Lightstreamer.DotNet.Logging.Log;
using System;
using System.Collections.Generic;
using System.Net;

namespace com.lightstreamer.client.transport.providers
{
    public class CookieHelper
    {

        private static readonly ILogger log = LogManager.GetLogger(Constants.UTILS_LOG);

        private static CookieContainer cookieHandler = new CookieContainer();

        private static IList<Cookie> custom_cookies = null;

        private static void logCookies(string message, IList<Cookie> cookies)
        {
            
            foreach (Cookie cookie in cookies)
            {
                message += ( "\r\n    " + cookie.ToString() );
                message += ( " - domain " + cookie.Domain );
                message += ( " - path " + cookie.Path );
                message += ( " - version " + cookie.Version );
            }
            log.Debug(message);
        }

        public static void addCookies(Uri uri, IList<Cookie> cookies)
        {
            if (cookies == null)
            {
                log.Warn("Receive null reference for the cookies list to add.");
                return;
            }
            lock (typeof(CookieHelper))
            {
                custom_cookies = cookies;

                if (log.IsDebugEnabled)
                {
                    log.Debug("Before adding cookies for " + uri + ": " + cookieHandler.GetCookieHeader(uri));
                    logCookies("Cookies to be added for " + uri, cookies);
                }
                foreach (Cookie cookie in cookies)
                {
                    string tmpcookie = "";

                    tmpcookie += cookie.Name;
                    tmpcookie += "=";
                    tmpcookie += cookie.Value;
                    tmpcookie += "; ";
                    
                    /// store.Set(uri.ToString(), cookie);
                    cookieHandler.SetCookies(uri, tmpcookie);
                }
                if (log.IsDebugEnabled)
                {
                    log.Debug("After adding cookies for " + uri + ": " + cookieHandler.GetCookieHeader(uri));
                }

            }
        }

        private static IList<Cookie> emptyCookieList = (IList<Cookie>)new System.Collections.Generic.List<Cookie>();

        public static IList<Cookie> getCookies(Uri uri)
        {

            lock (typeof(CookieHelper))
            {
                if (custom_cookies != null)
                {
                    return custom_cookies;
                }
            }
            return emptyCookieList;
        }

        public static string getCookieHeader(Uri target)
        {
            //IList<HttpCookie> cookieList = getCookies(target);
            //if (cookieList.Count > 0)
            //{

            //    StringBuilder headerValue = new StringBuilder();

            //    for (IEnumerator<HttpCookie> iter = cookieList.GetEnumerator(); iter.MoveNext();)
            //    {
            //        if (headerValue.Length != 0)
            //        {
            //            headerValue.Append("; ");
            //        }
            //        HttpCookie cookie = iter.Current;
            //        headerValue.Append(cookie.ToString()); //cookie toString generates the correct cookie value
            //    }

            //    string header = headerValue.ToString();
            //    log.Info("Cookies to be inserted for " + target + ": " + header);
            //    return header;

            //}
            //log.Info("Cookies to be inserted for " + target + ": <none>");
            //return null;
            return cookieHandler.GetCookieHeader(target);
        }

        public static void saveCookies(Uri uri, string cookieString)
        {
            if (cookieString == null)
            {
                log.Info("Cookies to be saved for " + uri + ": <none>");
                return;
            }
            log.Info("Cookies to be saved for " + uri + ": " + cookieString);

            cookieHandler.SetCookies(uri, cookieString);
        }

        /// <summary>
        /// Returns true if the internal CookieManager, to be used
        /// when a default cookie handler is not supplied, is set
        /// </summary>
        public static bool CookieHandlerLocal
        {
            get
            {
                lock (typeof(CookieHelper))
                {
                    return cookieHandler != null;
                }
            }
        }

        /// <summary>
        /// TEST ONLY
        /// resets the state of the CookieHelper class
        /// </summary>
        public static void reset()
        {
            lock (typeof(CookieHelper))
            {
                if (cookieHandler != null)
                {
                    log.Info("Discarding the custom CookieHandler");
                }
                cookieHandler = new CookieContainer();
            }
        }
    }
}