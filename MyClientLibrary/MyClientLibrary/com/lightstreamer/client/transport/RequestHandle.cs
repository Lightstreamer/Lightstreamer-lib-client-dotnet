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
namespace com.lightstreamer.client.transport
{
    /// <summary>
    /// Interface to be implemented to permit to Transport users to notify the transport their
    /// lost interest in a request.
    /// </summary>
    public interface RequestHandle
    {

        /// <summary>
        /// Although the suggested implementation is to stop event notifications and to close the 
        /// associated socket, it is also possible to ignore this call: obviously that would be a 
        /// waste of resources but can help during development. 
        /// @param forceConnectionClose if true, closes the underlying socket;
        /// otherwise marks the connection as closed but keeps the socket open.
        /// </summary>
        void close(bool forceConnectionClose);
    }
}