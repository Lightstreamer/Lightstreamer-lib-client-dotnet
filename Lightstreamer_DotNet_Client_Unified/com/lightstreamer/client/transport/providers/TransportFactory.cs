using com.lightstreamer.client.transport.providers.netty;
using com.lightstreamer.util;

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
    using SessionThread = com.lightstreamer.client.session.SessionThread;
    using ThreadShutdownHook = com.lightstreamer.util.threads.ThreadShutdownHook;

    /// <summary>
    /// A transport factory creates instances of a specific transport implementation.
    /// <para>
    /// <b>NB</b>
    /// I use an abstract class instead of an interface because I need to add static methods returning
    /// the default factory implementations.
    /// 
    /// </para>
    /// </summary>
    /// @param <T> either <seealso cref="HttpProvider"/> or <seealso cref="WebSocketProvider"/>  </param>
    public abstract class TransportFactory<T>
    {

        /*
		 * Instance methods of a generic transport factory.
		 */

        /// <summary>
        /// Returns a new instance of a transport.
        /// </summary>
        public abstract T getInstance(SessionThread thread);

        /// <summary>
        /// Returns true if the transport implementation reads the whole response before passing it to the client.
        /// When the response is buffered, the content-length should be small (about 4Mb).
        /// </summary>
        public abstract bool ResponseBuffered { get; }

        /*
		 * Below there are a few static methods providing the default factories for HTTP and WebSocket transports.
		 * 
		 * Default HTTP factory 
		 */

        private static TransportFactory<HttpProvider> defaultHttpFactory;

        public static TransportFactory<HttpProvider> DefaultHttpFactory
        {
            get
            {
                lock (typeof(TransportFactory<T>))
                {
                    if (defaultHttpFactory == null)
                    {
                        defaultHttpFactory = httpClassLoader.Alternative;
                        if (defaultHttpFactory == null)
                        {
                            defaultHttpFactory = new NettyHttpProviderFactory();

                            // Console.Error.WriteLine("NO HTTP PROVIDER CLASS AVAILABLE, SOMETHING WENT WRONG AT BUILD TIME, CONTACT LIGHTSTREAMER SUPPORT");
                        }
                    }
                    return defaultHttpFactory;
                }
            }
            set
            {
                lock (typeof(TransportFactory<T>))
                {
                    if (value == null)
                    {
                        throw new System.ArgumentException("Specify a factory");
                    }
                    defaultHttpFactory = value;
                }
            }
        }

        private static readonly AlternativeLoader<TransportFactory<HttpProvider>> httpClassLoader = new AlternativeLoaderAnonymousInnerClass();

        private class AlternativeLoaderAnonymousInnerClass : AlternativeLoader<TransportFactory<HttpProvider>>
        {
            protected internal override string[] DefaultClassNames
            {
                get
                {
                    string[] classes = new string[] { "com.lightstreamer.client.transport.providers.netty.NettyHttpProviderFactory" };
                    return classes;
                }
            }
        }

        /*
		 * Default WebSocket factory
		 */

        private static TransportFactory<WebSocketProvider> defaultWSFactory;

        public static TransportFactory<WebSocketProvider> DefaultWebSocketFactory
        {
            get
            {
                lock (typeof(TransportFactory<T>))
                {
                    if (defaultWSFactory == null)
                    {
                        defaultWSFactory = new WebSocketProviderFactory();
                    }
                    return defaultWSFactory;
                }
            }
            set
            {
                lock (typeof(TransportFactory<T>))
                {
                    defaultWSFactory = value;
                }
            }
        }


        private static readonly AlternativeLoader<TransportFactory<WebSocketProvider>> wsClassLoader = new AlternativeLoaderAnonymousInnerClass2();

        private class AlternativeLoaderAnonymousInnerClass2 : AlternativeLoader<TransportFactory<WebSocketProvider>>
        {
            protected internal override string[] DefaultClassNames
            {
                get
                {
                    string[] classes = new string[] { "com.lightstreamer.client.transport.providers.netty.WebSocketProviderFactory" };
                    return classes;
                }
            }
        }

        /*
		 * Global transport shutdown hook
		 */

        private static ThreadShutdownHook transportShutdownHook;

        /// <summary>
        /// Returns the shutdown hook releasing the resources shared by the transport providers (e.g. socket pools).
        /// </summary>
        public static ThreadShutdownHook TransportShutdownHook
        {
            get
            {
                lock (typeof(TransportFactory<T>))
                {
                    if (transportShutdownHook == null)
                    {
                        transportShutdownHook = transportShutdownHookClassLoader.Alternative;
                    }
                    return transportShutdownHook;
                }
            }
        }

        public static ThreadShutdownHook TranpsortShutdownHook
        {
            set
            {
                lock (typeof(TransportFactory<T>))
                {
                    transportShutdownHook = value;
                }
            }
        }

        private static readonly AlternativeLoader<ThreadShutdownHook> transportShutdownHookClassLoader = new AlternativeLoaderAnonymousInnerClass3();

        private class AlternativeLoaderAnonymousInnerClass3 : AlternativeLoader<ThreadShutdownHook>
        {
            protected internal override string[] DefaultClassNames
            {
                get
                {
                    string[] classes = new string[] { "com.lightstreamer.client.transport.providers.netty.NettyShutdownHook" };
                    return classes;
                }
            }
        }
    }
}