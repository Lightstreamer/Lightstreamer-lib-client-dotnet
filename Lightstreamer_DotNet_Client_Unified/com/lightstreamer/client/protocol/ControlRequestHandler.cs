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
using com.lightstreamer.client.requests;
using com.lightstreamer.client.transport;

namespace com.lightstreamer.client.protocol
{
    public interface ControlRequestHandler
    {
        /// <summary>
        /// Adds a control/message request.
        /// </summary>
        void addRequest(LightstreamerRequest request, RequestTutor tutor, RequestListener reqListener);

        long RequestLimit { set; }

        void copyTo(ControlRequestHandler newHandler);

        void close(bool waitPending);
    }
}