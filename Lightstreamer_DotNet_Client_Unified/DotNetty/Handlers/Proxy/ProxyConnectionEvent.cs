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

using DotNetty.Common.Utilities;
using System.Net;
using System.Text;

namespace DotNetty.Handlers.Proxy
{
    public sealed class ProxyConnectionEvent
    {
        private readonly string protocol;

        private readonly string authScheme;

        private readonly EndPoint proxyAddress;

        private readonly EndPoint destinationAddress;

        private string strVal;

        /// <summary>
        /// Creates a new event that indicates a successful connection attempt to the destination address.
        /// </summary>
        public ProxyConnectionEvent(string protocol, string authScheme, EndPoint proxyAddress, EndPoint destinationAddress)
        {
            if (string.ReferenceEquals(protocol, null))
            {
                throw new System.NullReferenceException("protocol");
            }
            if (string.ReferenceEquals(authScheme, null))
            {
                throw new System.NullReferenceException("authScheme");
            }
            if (proxyAddress == null)
            {
                throw new System.NullReferenceException("proxyAddress");
            }
            if (destinationAddress == null)
            {
                throw new System.NullReferenceException("destinationAddress");
            }

            this.protocol = protocol;
            this.authScheme = authScheme;
            this.proxyAddress = proxyAddress;
            this.destinationAddress = destinationAddress;
        }

        public string Protocol => protocol;

        public string AuthScheme => authScheme;

        public EndPoint ProxyAddress => proxyAddress;

        public EndPoint DestinationAddress => destinationAddress;

        public override string ToString()
        {
            if (!string.ReferenceEquals(strVal, null))
            {
                return strVal;
            }

            StringBuilder buf = ( new StringBuilder(128) ).Append(StringUtil.SimpleClassName(this)).Append('(').Append(Protocol).Append(", ").Append(AuthScheme).Append(", ").Append(ProxyAddress).Append(" => ").Append(DestinationAddress).Append(')');

            return strVal = buf.ToString();
        }
    }
}