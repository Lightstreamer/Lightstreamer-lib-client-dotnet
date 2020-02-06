using System;

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
    /// Interface which defines a basic thread executor whose internal working
    /// threads are terminated if no task arrive within a specified keep-alive time.
    /// 
    /// </summary>
    public interface JoinableExecutor : Joinable
    {
        /// <summary>
        /// Executes the given command at some time in the future.
        /// </summary>
        /// <seealso cref= Executor#execute(Runnable)
        /// </seealso>
        /// <param name="command">
        ///            the runnable task </param>
        /// <exception cref="RejectedExecutionException">
        ///             if this task cannot be accepted for execution </exception>
        /// <exception cref="NullPointerException">
        ///             if command is null </exception>
        void execute(Action task);
    }
}