using System;
using System.Threading;

/*
 * Copyright (c) 2004-2019 Lightstreamer s.r.l., Via Campanini, 6 - 20124 Milano,
 * Italy. All rights reserved. www.lightstreamer.com This software is the
 * confidential and proprietary information of Lightstreamer s.r.l. You shall not
 * disclose such Confidential Information and shall use it only in accordance
 * with the terms of the license agreement you entered into with Lightstreamer s.r.l.
 */
namespace com.lightstreamer.util.threads.providers
{
    /// <summary>
    /// Interface which defines a basic thread scheduler whose internal working threads are terminated if
    /// no task arrive within a specified keep-alive time.
    /// </summary>
    public interface JoinableScheduler : Joinable
    {
        /// <summary>
        /// Creates and executes a one-shot action that becomes enabled after the given delay.
        /// </summary>
        /// <param name="command">
        ///            the task to execute </param>
        /// <param name="delay">
        ///            the time in milliseconds from now to delay execution </param>
        /// <returns> a PendingTask representing pending completion of the task </returns>
        /// <exception cref="RejectedExecutionException">
        ///             if the task cannot be scheduled for execution </exception>
        /// <exception cref="NullPointerException">
        ///             if command is null </exception>
        /// <seealso cref= ScheduledExecutorService#schedule(Runnable, long, java.util.concurrent.TimeUnit) </seealso>
        CancellationTokenSource schedule(Action task, long delayInMillis);
    }
}