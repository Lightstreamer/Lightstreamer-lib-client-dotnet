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

namespace com.lightstreamer.client
{
    /// <summary>
    /// Simple class representing a Proxy configuration. <BR>
    /// 
    /// An instance of this class can be used through <seealso cref="ConnectionOptions#setProxy(Proxy)"/> to
    /// instruct a LightstreamerClient to connect to the Lightstreamer Server passing through a proxy.
    /// </summary>
    public class Proxy
    {
        protected internal string host;
        protected internal int port;
        protected internal string user;
        protected internal string password;
        protected internal string type;

        public Proxy(Proxy proxy) : this(proxy.type, proxy.host, proxy.port, proxy.user, proxy.password)
        {
        }

        /// <summary>
        /// This constructor will call <seealso cref="#Proxy(String, String, int, String, String)"/>
        /// specifying null user and null password. </summary>
        /// <param name="type"> the proxy type </param>
        /// <param name="host"> the proxy host </param>
        /// <param name="port"> the proxy port </param>
        public Proxy(string type, string host, int port) : this(type, host, port, null, null)
        {
        }

        /// <summary>
        /// This constructor will call <seealso cref="#Proxy(String, String, int, String, String)"/>
        /// specifying a null null password. </summary>
        /// <param name="type"> the proxy type </param>
        /// <param name="host"> the proxy host </param>
        /// <param name="port"> the proxy port </param>
        /// <param name="user"> the user name to be used to validate against the proxy </param>
        public Proxy(string type, string host, int port, string user) : this(type, host, port, user, null)
        {
        }

        /// <summary>
        /// Creates a Proxy instance containing all the informations required by the <seealso cref="LightstreamerClient"/>
        /// to connect to a Lightstreamer server passing through a proxy. <BR>
        /// Once created the Proxy instance has to be passed to the <seealso cref="LightstreamerClient#connectionOptions"/>
        /// instance using the <seealso cref="ConnectionOptions#setProxy(Proxy)"/> method.
        /// 
        /// BEGIN_ANDROID_DOC_ONLY
        /// <BR><BR>
        /// Note: user and password are ignored. If authentication is required by the proxy in use
        /// it is necessary to replace the default java <seealso cref="java.net.Authenticator"/> with a custom one containing 
        /// the necessary logic to authenticate the user against the proxy.  
        /// END_ANDROID_DOC_ONLY
        /// </summary>
        /// <param name="type"> the proxy type </param>
        /// <param name="host"> the proxy host </param>
        /// <param name="port"> the proxy port </param>
        /// <param name="user"> the user name to be used to validate against the proxy </param>
        /// <param name="password"> the password to be used to validate against the proxy </param>
        public Proxy(string type, string host, int port, string user, string password)
        {
            if (!Constants.PROXY_TYPES.Contains(type))
            {
                throw new System.ArgumentException("The given type is not valid; use HTTP.");
            }

            this.host = host;
            this.port = port;
            this.user = user;
            this.password = password;
            this.type = type;
        }

        public override string ToString()
        {
            return this.host + ":" + this.port;
        }

        public override bool Equals(object obj)
        {

            if (obj == null)
            {
                return false;
            }

            Proxy proxy = (Proxy)obj;
            bool isEqual = true;

            isEqual &= this.port == proxy.port;

            if (string.ReferenceEquals(this.host, null))
            {
                isEqual &= string.ReferenceEquals(proxy.host, null);
            }
            else
            {
                isEqual &= this.host.Equals(proxy.host);
            }

            if (string.ReferenceEquals(this.user, null))
            {
                isEqual &= string.ReferenceEquals(proxy.user, null);
            }
            else
            {
                isEqual &= this.user.Equals(proxy.user);
            }

            if (string.ReferenceEquals(this.password, null))
            {
                isEqual &= string.ReferenceEquals(proxy.password, null);
            }
            else
            {
                isEqual &= this.password.Equals(proxy.password);
            }

            isEqual &= this.type.Equals(proxy.type);

            return isEqual;

        }

        public override int GetHashCode()
        {
            int hash = 17;
            // Suitable nullity checks etc, of course :)
            hash = hash * 23 + this.host.GetHashCode();
            hash = hash * 23 + this.type.GetHashCode();
            hash = hash * 23 + this.port.GetHashCode();
            if (this.user != null)
            {
                hash = hash * 23 + this.user.GetHashCode();
            }
            if (this.password != null)
            {
                hash = hash * 23 + this.password.GetHashCode();
            }

            return hash;

            // return Objects .hash(this.host.GetHashCode(), this.type, this.port, this.user, this.password);
        }
    }
}