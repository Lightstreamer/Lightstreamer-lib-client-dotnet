using com.lightstreamer.util.threads;
using DotNetty.Common.Utilities;
using Lightstreamer.DotNet.Logging.Log;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

/*
 * Copyright (c) 2004-2019 Lightstreamer s.r.l., Via Campanini, 6 - 20124 Milano, Italy.
 * All rights reserved.
 * www.lightstreamer.com
 * This software is the confidential and proprietary information of
 * Lightstreamer s.r.l.
 * You shall not disclose such Confidential Information and shall use it
 * only in accordance with the terms of the license agreement you entered
 * into with Lightstreamer s.r.l.
 */
namespace com.lightstreamer.client.session
{

    /// <summary>
    /// An instance of this class is used to dispatch calls to the Session and Protocol layer.
    /// Both calls from the API layer (i.e. from the EventsThread) to the Session classes
    /// and from the network layer to the protocol layer are scheduled on this thread.
    /// <para>
    /// If the property "com.lightstreamer.client.session.thread" is set to "dedicated", then there is a session thread per client.
    /// Otherwise a single thread is shared between all the clients.
    /// </para>
    /// </summary>
    public class SessionThread
    {

        private static readonly ILogger log = LogManager.GetLogger(Constants.THREADS_LOG);

        private readonly ThreadMultiplexer<SessionThread> threads;

        private ConcurrentQueue<Task> StackTasks = new ConcurrentQueue<Task>();

        private readonly AtomicReference<ThreadShutdownHook> shutdownHookReference = new AtomicReference<ThreadShutdownHook>();

        private readonly AtomicReference<ThreadShutdownHook> wsShutdownHookReference = new AtomicReference<ThreadShutdownHook>();

        private volatile SessionManager sessionManager;

        // there is a 1-to-1 correspondence between a LightstreamerClient and a SessionThread,
        // so I can use the identifier of the SessionThread as the client identifier
        private readonly string clientId;

        public SessionThread()
        {
            threads = SessionThreadFactory.INSTANCE.SessionThread;
            clientId = this.GetHashCode().ToString("x");
        }

        public virtual void registerShutdownHook(ThreadShutdownHook shutdownHook)
        {
            // This allows to register the passed hook only once and therefore to have only one reference
            shutdownHookReference.CompareAndSet(null, shutdownHook);
        }

        public virtual void registerWebSocketShutdownHook(ThreadShutdownHook shutdownHook)
        {
            // This allows to register the passed hook only once and therefore to have only one reference
            wsShutdownHookReference.CompareAndSet(null, shutdownHook);
        }

        public virtual void await()
        {
            /* close session thread */
            threads.await();
            /* close HTTP provider */
            ThreadShutdownHook hook = shutdownHookReference.Value;
            if (hook != null)
            {
                hook.onShutdown();
            }
            else
            {
                // In case of iOS client, no ThreadShutdownHook is provided
                log.Info("No HTTP Shutdown Hook provided");
            }
            /* close WebSocket provider */
            ThreadShutdownHook wsHook = wsShutdownHookReference.Value;
            if (wsHook != null)
            {
                wsHook.onShutdown();
            }
            else
            {
                log.Info("No WebSocket Shutdown Hook provided");
            }
        }

        public virtual void queue(Task task)
        {
            threads.execute(this, decorateTask(task));
        }

        public virtual CancellationTokenSource schedule(Task task, long delayMillis)
        {
            return threads.schedule(this, decorateTask(task), delayMillis);
        }

        /// <summary>
        /// Sets the SessionManager. 
        /// <para>
        /// <b>NB</b> There is a circular dependency between the classes SessionManager and SessionThread
        /// and further this method is called by the user thread (see the implementation of <seealso cref="LightstreamerClient#LightstreamerClient(String, String)"/>)
        /// but it is used by the session thread, so the attribute {@code sessionManager} must be volatile.
        /// </para>
        /// </summary>
        public virtual SessionManager SessionManager
        {
            set
            {
                this.sessionManager = value;
            }
            get
            {
                Debug.Assert(sessionManager != null);
                return sessionManager;
            }
        }


        /// <summary>
        /// Decorates the task adding the following behavior:
        /// <ol>
        /// <li>sets the Mapped Diagnostic Context before calling the task and removes it after the calling is over</li>
        /// <li>when an escaped exception is caught, closes the session.</li>
        /// </ol>
        /// </summary>
        private Action decorateTask(Task task)
        {
            return () =>
            {
                Debug.Assert(sessionManager != null);
                /*if (MDC.Enabled)
                {
                    MDC.put("sessionId", sessionManager.SessionId);
                    MDC.put("clientId", clientId);
                }*/
                try
                {
                    task.Start();

                }
                catch (Exception e)
                {
                    log.Error("Uncaught exception", e);
                    sessionManager.onFatalError(e);

                }
                finally
                {
                    /*if (MDC.Enabled)
                    {
                        MDC.clear();
                    }*/
                }
            };
        }

        private class SessionThreadFactory
        {

            internal static readonly SessionThreadFactory INSTANCE = new SessionThreadFactory();

            internal readonly bool dedicatedSessionThread;
            internal ThreadMultiplexer<SessionThread> singletonSessionThread;

            internal SessionThreadFactory()
            {
                // dedicatedSessionThread = "dedicated".Equals(System.getProperty("com.lightstreamer.client.session.thread"));
                dedicatedSessionThread = false;

            }

            internal virtual ThreadMultiplexer<SessionThread> SessionThread
            {
                get
                {
                    lock (this)
                    {
                        ThreadMultiplexer<SessionThread> sessionThread;
                        if (dedicatedSessionThread)
                        {
                            sessionThread = new SingleThreadMultiplexer<SessionThread>();
                        }
                        else
                        {
                            if (singletonSessionThread == null)
                            {
                                singletonSessionThread = new SingleThreadMultiplexer<SessionThread>();
                            }
                            sessionThread = singletonSessionThread;
                        }
                        return sessionThread;
                    }
                }
            }
        }
    }
}