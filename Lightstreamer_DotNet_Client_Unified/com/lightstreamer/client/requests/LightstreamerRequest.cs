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
using System.Text;
using System.Threading;

namespace com.lightstreamer.client.requests
{
    public abstract class LightstreamerRequest
    {
        private readonly StringBuilder buffer = new StringBuilder(256);
        private string targetServer;
        private string session;

        protected internal static int unique = 0;

        public virtual string Server
        {
            set
            {
                //value might be ignored (e.g. in case of a websocket connection)
                this.targetServer = value;
            }
        }

        public virtual string Session
        {
            set
            {
                this.session = value;
            }
            get
            {
                return session;
            }
        }


        public abstract string RequestName { get; set; }

        protected internal static string encode(string value)
        {
            return percentEncodeTLCP(value);
        }

        protected internal static void addUnquotedParameter(StringBuilder buffer, string name, string value)
        {
            buffer.Append(name);
            buffer.Append("=");
            buffer.Append(value);
            //buffer.append("&"); supposed to be the last parameter in the request

            //prepend the unquoted size
            int len = buffer.Length;
            buffer.Insert(0, "&");
            buffer.Insert(0, len);
            buffer.Insert(0, "LS_unq=");
        }

        protected internal static void addParameter(StringBuilder buffer, string name, string value)
        {
            buffer.Append(name);
            buffer.Append("=");
            buffer.Append(encode(value));
            buffer.Append("&");
        }

        protected internal static void addParameter(StringBuilder buffer, string name, double value)
        {
            buffer.Append(name);
            buffer.Append("=");
            string doubleString = Convert.ToString(value);
            if (doubleString.EndsWith(".0", StringComparison.Ordinal))
            {
                buffer.Append(doubleString.Substring(0, doubleString.Length - 2));
            }
            else
            {
                buffer.Append(doubleString);
            }
            buffer.Append("&");
        }

        protected internal static void addParameter(StringBuilder buffer, string name, long value)
        {
            buffer.Append(name);
            buffer.Append("=");
            buffer.Append(value);
            buffer.Append("&");
        }

        protected internal virtual void addParameter(string name, string value)
        {
            addParameter(this.buffer, name, value);
        }

        protected internal virtual void addParameter(string name, double value)
        {
            addParameter(this.buffer, name, value);
        }

        protected internal virtual void addParameter(string name, long value)
        {
            addParameter(this.buffer, name, value);
        }

        public virtual void addUnique()
        {
            this.addParameter("LS_unique", Interlocked.Increment(ref unique));
        }

        protected internal virtual StringBuilder getQueryStringBuilder(string defaultSessionId)
        {
            StringBuilder result = new StringBuilder();
            result.Append(buffer);
            if (!string.ReferenceEquals(this.session, null))
            {
                bool sessionUnneeded = ( !string.ReferenceEquals(defaultSessionId, null) && defaultSessionId.Equals(this.session) );
                if (!sessionUnneeded)
                {
                    /*
                     * LS_session is written when there is no default sessionId or the default is different from this.session
                     * (the last case should never happen in practically)
                     */
                    addParameter(result, "LS_session", this.session);
                }
            }
            if (result.Length == 0)
            {
                /* empty query string is not allowed by the server: add an empty line */
                result.Append("\r\n");
            }
            return result;
        }

        public virtual string TransportUnawareQueryString
        {
            get
            {
                return this.getQueryStringBuilder(null).ToString();
            }
        }

        public virtual string getTransportAwareQueryString(string defaultSessionId, bool ackIsForced)
        {
            return this.getQueryStringBuilder(defaultSessionId).ToString();
        }

        public virtual string TargetServer
        {
            get
            {
                return targetServer;
            }
        }

        public virtual bool SessionRequest
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Method percentEncodeTLCP.
        /// Operates percent-encoding, but only on the reserved characters of the TLCP syntax
        /// (in particular, all non-ascii characters are preserved).
        /// </summary>
        /// <param name="str">  ... </param>
        private static string percentEncodeTLCP(string str)
        {
            int specials = 0;
            // preliminary step to determine the amount of memory needed
            int len = str.Length;
            for (int i = 0; i < len; i++)
            {
                char c = str[i];
                if (isSpecial(c))
                {
                    specials++;
                }
            }

            if (specials > 0)
            {
                int quotedLength = len + specials * 2;
                char[] quoted = new char[quotedLength];
                int j = 0;

                for (int i = 0; i < len; i++)
                {
                    char c = str[i];
                    if (isSpecial(c))
                    {
                        // assert((char)(c & 0x7F) == c);
                        // percent-encoding; must be UTF-8, but we only have
                        // to encode simple ascii characters
                        quoted[j++] = '%';
                        quoted[j++] = (char)( hex[( c >> 4 ) & 0xF] );
                        quoted[j++] = (char)( hex[c & 0xF] );
                    }
                    else
                    {
                        quoted[j++] = c;
                    }
                }
                // assert(j == quotedLength);
                return new string(quoted);
            }
            else
            {
                return str;
            }
        }

        private static readonly sbyte[] hex = new sbyte[16];

        static LightstreamerRequest()
        {
            for (int i = 0; i <= 9; i++)
            {
                hex[i] = (sbyte)( '0' + i );
            }
            for (int i = 10; i < 16; i++)
            {
                hex[i] = (sbyte)( 'A' + ( i - 10 ) );
            }
        }

        private static bool isSpecial(int b)
        {
            if (( b == '\r' ) || ( b == '\n' ))
            {
                // line delimiters
                return true;
            }
            else if (b == '%' || b == '+')
            {
                // used for percent-encoding
                return true;
            }
            else if (( b == '&' ) || ( b == '=' ))
            {
                // parameter delimiters
                return true;
            }
            else
            {
                // includes all non-ascii characters
                return false;
            }
        }

        public override string ToString()
        {
            return RequestName + " " + buffer.ToString();
        }
    }
}