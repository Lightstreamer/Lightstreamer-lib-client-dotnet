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
    public class DestroyRequest : ControlRequest
    {
        private string session;

        /// <param name="sessionId"> </param>
        /// <param name="closeReason"> </param>
        public DestroyRequest(string targetServer, string sessionID, string closeReason)
        {
            this.Server = targetServer;

            this.addParameter("LS_op", "destroy");

            this.session = sessionID;

            this.addParameter("LS_session", sessionID);

            if (!string.ReferenceEquals(closeReason, null))
            {
                this.addParameter("LS_cause", closeReason);
            }
        }

        public override string Session
        {
            get
            {
                return this.session;
            }
        }
    }
}