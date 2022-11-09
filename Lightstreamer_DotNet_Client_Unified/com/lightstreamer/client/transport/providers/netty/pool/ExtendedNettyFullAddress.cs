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

using System.Collections.Generic;

namespace com.lightstreamer.client.transport.providers.netty.pool
{


    /// <summary>
    /// The address of a remote Lightstreamer server storing also the configurations about proxy, extra headers and cookies
    /// (see <seealso cref="ConnectionOptions#setProxy(com.lightstreamer.client.Proxy)"/>, <seealso cref="ConnectionOptions#setHttpExtraHeaders(Map)"/>
    /// and <seealso cref="LightstreamerClient#addCookies(java.net.URI, java.util.List)"/>).
    /// <para>
    /// <b>NB</b> To be equal, two objects of this type must have addresses, proxies, extra headers and cookies equal.
    /// 
    /// </para>
    /// </summary>
    public class ExtendedNettyFullAddress
    {

        /*
		 * NB
		 * Since these objects are used as keys in maps,
		 * it is important to regenerate the methods hashCode and equals 
		 * if the attributes change.
		 */

        private readonly NettyFullAddress address;
        private readonly IDictionary<string, string> extraHeaders;
        private readonly string cookies;

        public ExtendedNettyFullAddress(NettyFullAddress address, IDictionary<string, string> extraHeaders, string cookies)
        {
            this.address = address;
            this.extraHeaders = extraHeaders;
            this.cookies = cookies;
        }

        public virtual NettyFullAddress Address
        {
            get
            {
                return address;
            }
        }

        public virtual IDictionary<string, string> ExtraHeaders
        {
            get
            {
                return extraHeaders;
            }
        }

        public virtual string Cookies
        {
            get
            {
                return cookies;
            }
        }

        public override int GetHashCode()
        {
            const int prime = 31;
            int result = 1;
            result = prime * result + ( ( address == null ) ? 0 : address.GetHashCode() );
            result = prime * result + ( ( string.ReferenceEquals(cookies, null) ) ? 0 : cookies.GetHashCode() );
            result = prime * result + ( ( extraHeaders == null ) ? 0 : extraHeaders.GetHashCode() );
            return result;
        }

        public override bool Equals(object obj)
        {
            if (this == obj)
            {
                return true;
            }
            if (obj == null)
            {
                return false;
            }
            if (this.GetType() != obj.GetType())
            {
                return false;
            }
            ExtendedNettyFullAddress other = (ExtendedNettyFullAddress)obj;
            if (address == null)
            {
                if (other.address != null)
                {
                    return false;
                }
            }
            else if (!address.Equals(other.address))
            {
                return false;
            }
            if (string.ReferenceEquals(cookies, null))
            {
                if (!string.ReferenceEquals(other.cookies, null))
                {
                    return false;
                }
            }
            else if (!cookies.Equals(other.cookies))
            {
                return false;
            }
            if (extraHeaders == null)
            {
                if (other.extraHeaders != null)
                {
                    return false;
                }
            }
            else if (!extraHeaders.Equals(other.extraHeaders))
            {
                return false;
            }
            return true;
        }
    }

}