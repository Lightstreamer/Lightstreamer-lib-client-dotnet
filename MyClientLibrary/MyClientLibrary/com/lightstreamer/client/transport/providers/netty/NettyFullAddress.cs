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