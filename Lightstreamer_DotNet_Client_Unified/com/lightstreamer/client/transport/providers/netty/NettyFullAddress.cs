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

using DotNetty.Handlers.Proxy;
using System.Net;

namespace com.lightstreamer.client.transport.providers.netty
{

    public class NettyFullAddress
    {

        private readonly IPEndPoint addressObj;
        private readonly Proxy proxy;
        private readonly bool secure;
        private readonly string host;
        private readonly string hostname;
        private readonly int port;


        public NettyFullAddress(bool secure, string host, int port, string hostname, Proxy proxy)
        {
            // let the transport layer resolve the address
            IPAddress.Parse(host);
            this.addressObj = new IPEndPoint(IPAddress.Parse(host), port);
            this.proxy = proxy;

            this.hostname = hostname;

            this.host = host;
            this.port = port;
            this.secure = secure;
        }

        public virtual IPEndPoint Address
        {
            get
            {
                return addressObj;
            }
        }

        public virtual bool Secure
        {
            get
            {
                return this.secure;
            }
        }

        public virtual ProxyHandler Proxy
        {
            get
            {
                if (proxy == null)
                {
                    return null;
                }
                return ( new NettyProxy(proxy) ).Proxy;
            }
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
            {
                return false;
            }
            NettyFullAddress check = (NettyFullAddress)obj;
            if (( this.proxy == null && check.proxy == null ) || ( this.proxy != null && this.proxy.Equals(check.proxy) ))
            {
                return addressObj.Equals(check.addressObj);
            }
            return false;
        }

        public override int GetHashCode()
        {
            int hash = 17;
            // Suitable nullity checks etc, of course :)
            if (this.proxy == null)
            {
                hash = hash * 23 + 0;
            }
            else
            {
                hash = hash * 23 + this.proxy.GetHashCode();
            }
            hash = hash * 23 + this.addressObj.GetHashCode();

            return hash;

        }

        public virtual string Host
        {
            get
            {
                return host;
            }
        }

        public virtual int Port
        {
            get
            {
                return port;
            }
        }

        public virtual string HostName
        {
            get
            {
                return hostname;
            }
        }
    }
}