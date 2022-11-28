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
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace com.lightstreamer.util
{ 
    public class LsUtils
    {
        /// <summary>
        /// Returns URI from string.
        /// </summary>
        public static Uri uri(string uri)
        {
            try
            {
                return new Uri(uri);
            }
            catch (Exception e)
            {
                throw new System.ArgumentException(e.Message);
            }
        }

        /// <summary>
        /// Returns whether the URI is secured.
        /// </summary>
        public static bool isSSL(Uri uri)
        {
            string scheme = uri.Scheme;
            return "https".Equals(scheme, StringComparison.OrdinalIgnoreCase) || "wss".Equals(scheme, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Returns the port of an URI.
        /// </summary>
        public static int port(Uri uri)
        {
            int port = uri.Port;
            if (port == -1)
            {
                return isSSL(uri) ? 443 : 80;
            }
            else
            {
                return port;
            }
        }

        /// <summary>
        /// Implementation from http://commons.apache.org/proper/commons-lang/javadocs/api-3.8.1/org/apache/commons/lang3/StringUtils.html
        /// </summary>
        
        public static string join(object[] array, char separator)
        {
            if (array == null)
            {
                return null;
            }
            int startIndex = 0;
            int endIndex = array.Length;
            
            int noOfItems = endIndex - startIndex;
            if (noOfItems <= 0)
            {
                return "";
            }

            StringBuilder buf = new StringBuilder(noOfItems);
            for (int i = startIndex; i < endIndex; i++)
            {
                if (i > startIndex)
                {
                    buf.Append(separator);
                }
                if (array[i] != null)
                {
                    buf.Append(array[i]);
                }
            }
            return buf.ToString();
        }

        /// <summary>
        /// Implementation from http://commons.apache.org/proper/commons-lang/javadocs/api-3.8.1/org/apache/commons/lang3/StringUtils.html
        /// </summary>
        public static string[] split(string str, char separatorChar)
        {
            bool preserveAllTokens = false;
            if (string.ReferenceEquals(str, null))
            {
                return null;
            }
            
            int len = str.Length;
            if (len == 0)
            {
                return new string[0];
            }

            IList<string> list = new List<string>();
            int i = 0, start = 0;
            bool match = false;
            bool lastMatch = false;
            while (i < len)
            {
                if (str[i] == separatorChar)
                {
                    if (match || preserveAllTokens)
                    {
                        list.Add(str.Substring(start, i - start));
                        match = false;
                        lastMatch = true;
                    }
                    start = ++i;
                    continue;
                }
                lastMatch = false;
                match = true;
                i++;
            }
            if (match || preserveAllTokens && lastMatch)
            {
                list.Add(str.Substring(start, i - start));
            }
            return list.ToArray();
        }

        public static bool Equals(object o1, object o2)
        {
            return o1 == o2 || ( o1 != null && o1.Equals(o2) );
        }

        public static bool notEquals(object o1, object o2)
        {
            return !Equals(o1, o2);
        }
    }
}