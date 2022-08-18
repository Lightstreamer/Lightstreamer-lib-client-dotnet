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
using Lightstreamer.DotNet.Logging.Log;
using System;
using System.Net;

namespace com.lightstreamer.client.transport.providers.netty
{

    /// 
    public class NettyProxy : Proxy
    {

        private static readonly ILogger log = LogManager.GetLogger(Constants.NETTY_LOG);


        public NettyProxy(Proxy original) : base(original)
        {
            //
        }

        public virtual ProxyHandler Proxy
        {
            get
            {

                switch (type)
                {

                    case "HTTP":


                        log.Info("Add Proxy: " + host + ":" + port);

                        if (host.ToLower().Equals("localhost"))
                        {
                            host = "127.0.0.1";
                        }

                        try
                        {
                            IPAddress ip;
                            string host4Netty;

                            if ( IPAddress.TryParse(host, out ip) )
                            {
                                host4Netty = host;

                                log.Info("Proxy ip: " + host4Netty);
                            } else
                            {
                                host4Netty = System.Net.Dns.GetHostAddresses(host)[0].ToString();

                                log.Info("Proxy ip:: " + host4Netty);
                            }

                            IPEndPoint ipep = new IPEndPoint(IPAddress.Parse(host4Netty), port);

                            if (!string.ReferenceEquals(user, null) || !string.ReferenceEquals(password, null))
                            {
                                log.Info("Add user and password.");

                                return new HttpProxyHandler(ipep, user, password);
                            }
                            else
                            {
                                return new HttpProxyHandler(ipep);
                            }
                        } catch (Exception ex)
                        {
                            log.Info("Proxy error: " + ex.Message);

                            return null;
                        }
                        
                    case "SOCKS4":


                        throw new Exception("SOCKS4 Not Supported.");
                    //if (!string.ReferenceEquals(user, null) || !string.ReferenceEquals(password, null))
                    //{
                    //  return new Socks4ProxyHandler(new InetSocketAddress(host,port),user);
                    //}
                    //else
                    //{
                    //  return new Socks4ProxyHandler(new InetSocketAddress(host,port));
                    //}

                    case "SOCKS5":

                        throw new Exception("SOCKS5 Not Supported.");
                    //if (!string.ReferenceEquals(user, null) || !string.ReferenceEquals(password, null))
                    //{
                    //  return new Socks5ProxyHandler(new InetSocketAddress(host,port),user,password);
                    //}
                    //else
                    //{
                    //  return new Socks5ProxyHandler(new InetSocketAddress(host,port));
                    //}

                    default:
                        return null;
                }
            }
        }
    }
}