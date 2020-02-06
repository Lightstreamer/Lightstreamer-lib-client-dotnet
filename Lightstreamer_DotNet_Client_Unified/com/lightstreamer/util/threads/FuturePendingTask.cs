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
using System;
using System.Threading;
using System.Threading.Tasks;

namespace com.lightstreamer.util.threads
{
    internal class FuturePendingTask : PendingTask
    {
        private Task<IAsyncResult> pending;
        private CancellationTokenSource token;

        public FuturePendingTask(Task<IAsyncResult> pending, CancellationTokenSource token)
        {
            this.pending = pending;
            this.token = token;
        }

        public void Cancel()
        {
            token.Cancel();
        }

        public bool IsCancellationRequested
        {
            get
            {
                return pending.IsCanceled;
            }
        }
    }
}