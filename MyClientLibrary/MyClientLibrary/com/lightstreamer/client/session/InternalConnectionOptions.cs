using com.lightstreamer.client.events;
using com.lightstreamer.client.transport.providers;
using com.lightstreamer.util;
using Lightstreamer.DotNet.Logging.Log;
using System;
using System.Collections.Generic;
using System.Globalization;

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
namespace com.lightstreamer.client.session
{

    public class InternalConnectionOptions
    {

        private long contentLength = 50_000_000;
        private bool earlyWSOpenEnabled = false;
        private long firstRetryMaxDelay = 100;
        private long forceBindTimeout = 2000; //not exposed
        private string forcedTransport = null;
        private IDictionary<string, string> httpExtraHeaders = null;
        private bool httpExtraHeadersOnSessionCreationOnly = false; // does not make much sense here, we still keep it, no need to differentiate
        private long idleTimeout = 19000;
        private long keepaliveInterval = 0;
        private double requestedMaxBandwidth = 0;
        private string realMaxBandwidth = null;
        /// <summary>
        /// This attribute is hidden: when the bandwidth is "unmanaged", the client sees it as "unlimited".
        /// </summary>
        private bool unmanagedBandwidth = false;
        private long pollingInterval = 0;
        private long reconnectTimeout = 3000;
        private readonly RetryDelayCounter currentRetryDelay = new RetryDelayCounter(4000);
        private long reverseHeartbeatInterval = 0;
        private bool serverInstanceAddressIgnored = false;
        private bool slowingEnabled = true;
        private long stalledTimeout = 2000;
        private long sessionRecoveryTimeout = 15000;
        private long switchCheckTimeout = 4000; //not exposed
        private Proxy proxy;

        private readonly ILogger log = LogManager.GetLogger(Constants.ACTIONS_LOG);
        private readonly EventDispatcher<ClientListener> eventDispatcher;
        private readonly ClientListener internalListener;

        public InternalConnectionOptions(EventDispatcher<ClientListener> eventDispatcher, ClientListener internalListener)
        {
            this.eventDispatcher = eventDispatcher;
            this.internalListener = internalListener;
            if (TransportFactory<HttpProvider>.DefaultHttpFactory.ResponseBuffered)
            {
                this.contentLength = 4_000_000;
            }
        }

        public virtual long CurrentConnectTimeout
        {
            get
            {
                lock (this)
                {
                    return currentRetryDelay.CurrentRetryDelay;
                }
            }
        }

        public virtual long ContentLength
        {
            get
            {
                lock (this)
                {
                    return contentLength;
                }
            }
            set
            {
                lock (this)
                {
                    Number.verifyPositive(value, Number.DONT_ACCEPT_ZERO);

                    this.contentLength = value;

                    this.eventDispatcher.dispatchEvent(new ClientListenerPropertyChangeEvent("contentLength"));

                    log.Info("Content Length value changed to " + value);
                }
            }
        }

        public virtual long FirstRetryMaxDelay
        {
            get
            {
                lock (this)
                {
                    return firstRetryMaxDelay;
                }
            }
            set
            {
                lock (this)
                {
                    Number.verifyPositive(value, Number.DONT_ACCEPT_ZERO);

                    this.firstRetryMaxDelay = value;

                    this.eventDispatcher.dispatchEvent(new ClientListenerPropertyChangeEvent("firstRetryMaxDelay"));

                    log.Info("First Retry Max Delay value changed to " + value);
                }
            }
        }

        public virtual long ForceBindTimeout
        {
            get
            {
                lock (this)
                {
                    return forceBindTimeout;
                }
            }
            set
            {
                lock (this)
                {
                    this.forceBindTimeout = value;
                }
            }
        }

        public virtual string ForcedTransport
        {
            get
            {
                lock (this)
                {
                    return forcedTransport;
                }
            }
            set
            {
                lock (this)
                {
                    if (!Constants.FORCED_TRANSPORTS.Contains(value))
                    {
                        throw new System.ArgumentException("The given value is not valid. Use one of: HTTP-STREAMING, HTTP-POLLING, WS-STREAMING, WS-POLLING, WS, HTTP, or null");
                    }

                    this.forcedTransport = value;

                    this.eventDispatcher.dispatchEvent(new ClientListenerPropertyChangeEvent("forcedTransport"));
                    this.internalListener.onPropertyChange("forcedTransport");

                    log.Info("Forced Transport value changed to " + value);

                }
            }
        }

        public virtual IDictionary<string, string> HttpExtraHeaders
        {
            get
            {
                lock (this)
                {
                    return httpExtraHeaders;
                }
            }
            set
            {
                lock (this)
                {
                    this.httpExtraHeaders = value;

                    this.eventDispatcher.dispatchEvent(new ClientListenerPropertyChangeEvent("httpExtraHeaders"));

                    log.Info("Extra headers Map changed");
                }
            }
        }

        public virtual long IdleTimeout
        {
            get
            {
                lock (this)
                {
                    return idleTimeout;
                }
            }
            set
            {
                lock (this)
                {
                    Number.verifyPositive(value, Number.ACCEPT_ZERO);

                    this.idleTimeout = value;

                    this.eventDispatcher.dispatchEvent(new ClientListenerPropertyChangeEvent("idleTimeout"));

                    log.Info("Idle Timeout value changed to " + value);
                }
            }
        }

        public virtual long KeepaliveInterval
        {
            get
            {
                lock (this)
                {
                    return keepaliveInterval;
                }
            }
            set
            {
                lock (this)
                {
                    Number.verifyPositive(value, Number.ACCEPT_ZERO);

                    this.keepaliveInterval = value;

                    this.eventDispatcher.dispatchEvent(new ClientListenerPropertyChangeEvent("keepaliveInterval"));

                    log.Info("Keepalive Interval value changed to " + value);
                }
            }
        }

        public virtual string RequestedMaxBandwidth
        {
            get
            {
                lock (this)
                {
                    if (this.requestedMaxBandwidth == 0)
                    {
                        return "unlimited";
                    }
                    return this.requestedMaxBandwidth.ToString();
                }
            }
            set
            {
                lock (this)
                {
                    setMaxBandwidthInternal(value, false);
                }
            }
        }

        public virtual double InternalMaxBandwidth
        {
            get
            {
                lock (this)
                {
                    return requestedMaxBandwidth;
                }
            }
        }

        public virtual string RealMaxBandwidth
        {
            get
            {
                lock (this)
                {
                    return realMaxBandwidth;
                }
            }
        }

        public virtual string InternalRealMaxBandwidth
        {
            set
            {
                lock (this)
                {
                    this.realMaxBandwidth = value;
                    this.eventDispatcher.dispatchEvent(new ClientListenerPropertyChangeEvent("realMaxBandwidth"));
                }
            }
        }

        public virtual bool BandwidthUnmanaged
        {
            get
            {
                lock (this)
                {
                    return unmanagedBandwidth;
                }
            }
            set
            {
                lock (this)
                {
                    this.unmanagedBandwidth = value;
                }
            }
        }


        public virtual long PollingInterval
        {
            get
            {
                lock (this)
                {
                    return pollingInterval;
                }
            }
            set
            {
                lock (this)
                {
                    Number.verifyPositive(value, Number.ACCEPT_ZERO);

                    this.pollingInterval = value;

                    this.eventDispatcher.dispatchEvent(new ClientListenerPropertyChangeEvent("pollingInterval"));

                    log.Info("Polling Interval value changed to " + this.pollingInterval);
                }
            }
        }

        public virtual long ReconnectTimeout
        {
            get
            {
                lock (this)
                {
                    return reconnectTimeout;
                }
            }
            set
            {
                lock (this)
                {
                    Number.verifyPositive(value, Number.DONT_ACCEPT_ZERO);

                    this.reconnectTimeout = value;

                    this.eventDispatcher.dispatchEvent(new ClientListenerPropertyChangeEvent("reconnectTimeout"));

                    log.Info("Reconnect Timeout value changed to " + this.reconnectTimeout);
                }
            }
        }

        public virtual long RetryDelay
        {
            get
            {
                lock (this)
                {
                    return currentRetryDelay.RetryDelay;
                }
            }
            set
            {
                lock (this)
                {
                    Number.verifyPositive(value, Number.DONT_ACCEPT_ZERO);

                    this.currentRetryDelay.reset(value);

                    this.eventDispatcher.dispatchEvent(new ClientListenerPropertyChangeEvent("retryDelay"));

                    log.Info("Retry Delay value changed to " + value);
                }
            }
        }

        public virtual long CurrentRetryDelay
        {
            get
            {
                lock (this)
                {
                    return currentRetryDelay.CurrentRetryDelay;
                }
            }
        }

        public virtual long ReverseHeartbeatInterval
        {
            get
            {
                lock (this)
                {
                    return reverseHeartbeatInterval;
                }
            }
            set
            {
                lock (this)
                {
                    Number.verifyPositive(value, Number.ACCEPT_ZERO);

                    this.reverseHeartbeatInterval = value;

                    this.eventDispatcher.dispatchEvent(new ClientListenerPropertyChangeEvent("reverseHeartbeatInterval"));
                    this.internalListener.onPropertyChange("reverseHeartbeatInterval");

                    log.Info("Reverse Heartbeat Interval value changed to " + this.reverseHeartbeatInterval);

                }
            }
        }

        public virtual long StalledTimeout
        {
            get
            {
                lock (this)
                {
                    return stalledTimeout;
                }
            }
            set
            {
                lock (this)
                {
                    Number.verifyPositive(value, Number.DONT_ACCEPT_ZERO);

                    this.stalledTimeout = value;

                    this.eventDispatcher.dispatchEvent(new ClientListenerPropertyChangeEvent("stalledTimeout"));

                    log.Info("Stalled Timeout value changed to " + this.stalledTimeout);
                }
            }
        }

        public virtual long SessionRecoveryTimeout
        {
            get
            {
                lock (this)
                {
                    return sessionRecoveryTimeout;
                }
            }
            set
            {
                lock (this)
                {
                    Number.verifyPositive(value, Number.ACCEPT_ZERO);

                    this.sessionRecoveryTimeout = value;

                    this.eventDispatcher.dispatchEvent(new ClientListenerPropertyChangeEvent("sessionRecoveryTimeout"));

                    log.Info("Session Recovery Timeout value changed to " + this.sessionRecoveryTimeout);
                }
            }
        }

        public virtual Proxy Proxy
        {
            get
            {
                lock (this)
                {
                    return this.proxy;
                }
            }
            set
            {
                lock (this)
                {
                    this.proxy = value;

                    this.eventDispatcher.dispatchEvent(new ClientListenerPropertyChangeEvent("proxy"));

                    log.Info("Proxy configuration changed " + this.proxy);
                }
            }
        }

        public virtual long SwitchCheckTimeout
        {
            get
            {
                lock (this)
                {
                    return switchCheckTimeout;
                }
            }
            set
            {
                lock (this)
                {
                    this.switchCheckTimeout = value;
                }
            }
        }

        public virtual long TCPConnectTimeout
        {
            get
            {
                lock (this)
                {
                    return currentRetryDelay.CurrentRetryDelay + 1000;
                }
            }
        }

        public virtual long TCPReadTimeout
        {
            get
            {
                lock (this)
                {
                    return this.keepaliveInterval + this.stalledTimeout + 1000;
                }
            }
        }

        public virtual bool EarlyWSOpenEnabled
        {
            get
            {
                lock (this)
                {
                    return earlyWSOpenEnabled;
                }
            }
            set
            {
                lock (this)
                {
                    this.earlyWSOpenEnabled = value;

                    this.eventDispatcher.dispatchEvent(new ClientListenerPropertyChangeEvent("earlyWSOpenEnabled"));

                    log.Info("Early WS Open Enabled value changed to " + value);
                }
            }
        }

        public virtual bool HttpExtraHeadersOnSessionCreationOnly
        {
            get
            {
                lock (this)
                {
                    return httpExtraHeadersOnSessionCreationOnly;
                }
            }
            set
            {
                lock (this)
                {
                    this.httpExtraHeadersOnSessionCreationOnly = value;

                    this.eventDispatcher.dispatchEvent(new ClientListenerPropertyChangeEvent("httpExtraHeadersOnSessionCreationOnly"));

                    log.Info("Extra Headers On Session Creation Only flag changed to " + value);

                }
            }
        }

        public virtual bool ServerInstanceAddressIgnored
        {
            get
            {
                lock (this)
                {
                    return serverInstanceAddressIgnored;
                }
            }
            set
            {
                lock (this)
                {
                    this.serverInstanceAddressIgnored = value;

                    this.eventDispatcher.dispatchEvent(new ClientListenerPropertyChangeEvent("serverInstanceAddressIgnored"));

                    log.Info("Server Instance Address Ignored flag changed to " + this.serverInstanceAddressIgnored);
                }
            }
        }

        public virtual bool SlowingEnabled
        {
            get
            {
                lock (this)
                {
                    return slowingEnabled;
                }
            }
            set
            {
                lock (this)
                {
                    this.slowingEnabled = value;

                    this.eventDispatcher.dispatchEvent(new ClientListenerPropertyChangeEvent("slowingEnabled"));

                    log.Info("Slowing Enabled flag changed to " + this.slowingEnabled);
                }
            }
        }

        public virtual void increaseConnectTimeout()
        {
            lock (this)
            {
                currentRetryDelay.increase();
            }
        }

        public virtual void increaseRetryDelay()
        {
            lock (this)
            {
                currentRetryDelay.increase();
            }
        }

        public virtual void resetConnectTimeout()
        {
            lock (this)
            {
                currentRetryDelay.reset(RetryDelay);
            }
        }

        internal virtual string MaxBandwidthInternal
        {
            set
            {
                lock (this)
                {
                    setMaxBandwidthInternal(value, true);
                }
            }
        }

        private void setMaxBandwidthInternal(string maxBandwidth, bool serverCall)
        {
            lock (this)
            {
                if (maxBandwidth.ToLower().Equals(Constants.UNLIMITED))
                {
                    this.requestedMaxBandwidth = 0;
                    log.Info("Max Bandwidth value changed to unlimited");
                }
                else
                {
                    double tmp = 0;
                    try
                    {

                        Console.WriteLine("Debud - Test (s):" + maxBandwidth);

                        tmp = double.Parse(maxBandwidth, CultureInfo.InvariantCulture);

                        Console.WriteLine("Debud - Test (d):" + tmp);

                    }
                    catch (System.FormatException nfe)
                    {
                        throw new System.ArgumentException("The given value is a not valid value for setRequestedMaxBandwidth. Use a positive number or the string \"unlimited\"", nfe);
                    }

                    //server sends 0.0 to represent UNLIMITED
                    Number.verifyPositive(tmp, serverCall ? Number.ACCEPT_ZERO : Number.DONT_ACCEPT_ZERO);

                    this.requestedMaxBandwidth = tmp;

                    log.Info("Max Bandwidth value changed to " + this.requestedMaxBandwidth);
                }

                this.internalListener.onPropertyChange("requestedMaxBandwidth");
                this.eventDispatcher.dispatchEvent(new ClientListenerPropertyChangeEvent("requestedMaxBandwidth"));
            }
        }

        /// <summary>
        /// Returns the {@code EventDispatcher}.
        /// </summary>
        /// @deprecated This method is meant to be used ONLY as a workaround for iOS implementation, as
        ///             it requires to send a non Unified API and platform specific event through the
        ///             {@code ClientListener} interface.
        /// 
        /// <returns> the {@code EventDispatcher} </returns>
        [Obsolete("This method is meant to be used ONLY as a workaround for iOS implementation, as")]
        public virtual EventDispatcher<ClientListener> EventDisapatcher
        {
            get
            {
                return eventDispatcher;
            }
        }

        // xDomainStreamingEnabled - cookieHandlingRequired - spinFixTimeout - spinFixEnabled -
        // corsXHREnabled -> JS only

    }

}