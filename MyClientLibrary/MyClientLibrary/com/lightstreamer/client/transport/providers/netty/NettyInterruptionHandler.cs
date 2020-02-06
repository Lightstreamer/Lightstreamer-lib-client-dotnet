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
using DotNetty.Common.Utilities;
using System;

namespace com.lightstreamer.client.transport.providers.netty
{

    public class NettyInterruptionHandler : RequestHandle
    {

        private bool interrupted = false;
        public AtomicReference<IDisposable> connectionRef = new AtomicReference<IDisposable>(); // written by Netty thread but read by Session thread

        public virtual void close(bool forceConnectionClose)
        {
            this.interrupted = true;
            if (forceConnectionClose)
            {
                IDisposable ch = (IDisposable)connectionRef.Value;
                if (ch != null)
                {
                    try
                    {
                        ch.Dispose();

                    }
                    catch (Exception e)
                    {
                        // ignore
                    }
                }
            }
        }


        internal virtual bool Interrupted
        {
            get
            {
                return this.interrupted;
            }
        }

    }
}