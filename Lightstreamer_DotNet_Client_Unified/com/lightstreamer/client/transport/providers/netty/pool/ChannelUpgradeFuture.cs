#region License
/*
 * Copyright (c) Lightstreamer Srl
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
#endregion License

using DotNetty.Transport.Channels;
using System;

namespace com.lightstreamer.client.transport.providers.netty.pool
{

    /// <summary>
    /// The result of an asynchronous I/O operation of upgrading of a channel.
    /// </summary>
    public interface ChannelUpgradeFuture
    {

        /// <summary>
        /// Listens to the result of a <seealso cref="ChannelUpgradeFuture"/>.
        /// </summary>

        /// <summary>
        /// Returns {@code true} if this task completed.
        /// </summary>
        bool Done { get; }

        /// <summary>
        /// Returns {@code true} if and only if the I/O operation was completed
        /// successfully.
        /// </summary>
        bool Success { get; }

        /// <summary>
        /// Sets the specified listener to this future.  The
        /// specified listener is notified when this future is
        /// <seealso cref="#isDone() done"/>.  If this future is already
        /// completed, the specified listener is notified immediately.
        /// </summary>
        void addListener(ChannelUpgradeFuture_ChannelUpgradeFutureListener fl);

        /// <summary>
        /// Returns a channel where the I/O operation associated with this
        /// future takes place.
        /// </summary>
        IChannel channel();

        /// <summary>
        /// Returns the cause of the failed I/O operation if the I/O operation has
        /// failed.
        /// </summary>
        Exception cause();

    }

    public interface ChannelUpgradeFuture_ChannelUpgradeFutureListener
    {
        void operationComplete(ChannelUpgradeFuture future);
    }
}