using CookieManager;
using Lightstreamer.DotNet.Logging.Log;
using System;
using System.Collections.Generic;
using System.Text;

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
namespace com.lightstreamer.client.transport.providers
{
    public class CookieHelper
    {

        private static readonly ILogger log = LogManager.GetLogger(Constants.UTILS_LOG);

        private static void logCookies(string message, IList<HttpCookie> cookies)
        {
            // usato solo in caso di debug
            foreach (HttpCookie cookie in cookies)
            {
                message += ( "\r\n    " + cookie.ToString() );
                message += ( " - domain " + cookie.Get("Domain") );
                message += ( " - path " + cookie.Get("Path") );
                message += ( " - version " + cookie.Get("Version") );
            }
            log.Debug(message);
        }

        public static void addCookies(Uri uri, IList<HttpCookie> cookies)
        {
            lock (typeof(CookieHelper))
            {

                ICookieManager store = new DefaultCookieManager(null);

                if (log.IsDebugEnabled)
                {
                    //logCookies("Before adding cookies for " + uri, store.Cookies);
                    logCookies("Cookies to be added for " + uri, cookies);
                }
                foreach (HttpCookie cookie in cookies)
                {

                    store.Set(uri.ToString(), cookie);
                }
                if (log.IsDebugEnabled)
                {
                    //logCookies("After adding cookies for " + uri, store.Cookies);
                }

            }
        }

        private static IList<HttpCookie> emptyCookieList = (IList<HttpCookie>)new System.Collections.Generic.List<HttpCookie>();

        public static IList<HttpCookie> getCookies(Uri uri)
        {

            lock (typeof(CookieHelper))
            { 
                //
            }
            return emptyCookieList;
        }

        public static string getCookieHeader(Uri target)
        {
            IList<HttpCookie> cookieList = getCookies(target);
            if (cookieList.Count > 0)
            {

                StringBuilder headerValue = new StringBuilder();

                for (IEnumerator<HttpCookie> iter = cookieList.GetEnumerator(); iter.MoveNext();)
                {
                    if (headerValue.Length != 0)
                    {
                        headerValue.Append("; ");
                    }
                    HttpCookie cookie = iter.Current;
                    headerValue.Append(cookie.ToString()); //cookie toString generates the correct cookie value
                }

                string header = headerValue.ToString();
                log.Info("Cookies to be inserted for " + target + ": " + header);
                return header;

            }
            log.Info("Cookies to be inserted for " + target + ": <none>");
            return null;
        }

        public static void saveCookies(Uri uri, string cookieString)
        {
            //
        }

        /// <summary>
        /// Private cookie handler used when there is no global handler available (see <seealso cref="CookieHandler#setDefault(CookieHandler)"/>).
        /// </summary>
        private static ICookieManager cookieHandler;

        /// <summary>
        /// True if the next call to <seealso cref="CookieHelper#getCookieHandler()"/> will be the first one. False otherwise.
        /// </summary>
        private static bool firstTime = true;

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
                cookieHandler = null;
                firstTime = true;
            }
        }
    }
}