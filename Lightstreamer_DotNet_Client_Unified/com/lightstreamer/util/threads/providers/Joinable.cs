/*
 * Copyright (c) 2004-2015 Lightstreamer s.r.l., Via Campanini, 6 - 20124 Milano,
 * Italy. All rights reserved. www.lightstreamer.com This software is the
 * confidential and proprietary information of Lightstreamer s.r.l. You shall not
 * disclose such Confidential Information and shall use it only in accordance
 * with the terms of the license agreement you entered into with Lightstreamer s.r.l.
 */
namespace com.lightstreamer.util.threads.providers
{
    /// <summary>
    /// Root interface for <i>joinable</i> executors and schedulers.
    /// <para>
    /// Executors and Schedulers are said <i>joinable</i> if their internal working threads are
    /// terminated if no more task arrive, therefore allowing a graceful completion of involved threads
    /// without no need to explicitly invoke <seealso cref="ExecutorService.shutdown"/> or
    /// <seealso cref="ScheduledExecutorService.shutdown"/>
    /// method.
    /// 
    /// </para>
    /// </summary>
    public interface Joinable
    {
        /// <summary>
        /// Waits forever for this joinable executor (or scheduler) to die.
        /// </summary>
        /// <exception cref="RuntimeException">
        ///             which wraps an <seealso cref="InterruptedExetpion"/> if any thread has
        ///             interrupted the current thread. </exception>
        void join();
    }
}