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
namespace com.lightstreamer.client.requests
{
    public class ForceRebindRequest : ControlRequest
    {
        public ForceRebindRequest(string targetServer, string sessionID, string rebindCause, double delay)
        {
            this.Server = targetServer;

            this.addParameter("LS_op", "force_rebind");

            this.addParameter("LS_session", sessionID);

            if (!string.ReferenceEquals(rebindCause, null))
            {
                this.addParameter("LS_cause", rebindCause);
            }

            if (delay > 0)
            {
                this.addParameter("LS_polling_millis", delay);
            }
        }
    }
}