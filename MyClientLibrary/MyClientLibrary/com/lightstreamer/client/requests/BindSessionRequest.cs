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
using com.lightstreamer.client.session;

namespace com.lightstreamer.client.requests
{
    public class BindSessionRequest : SessionRequest
    {
        public BindSessionRequest(string targetServer, string session, bool polling, string cause, InternalConnectionOptions options, long delay, bool addContentLength, long maxReverseHeartbeatIntervalMs) : base(polling, delay)
        {
            this.Server = targetServer;

            Session = session;
            // the session ID can still be omitted from the request, depending on the transport

            if (polling)
            {
                this.addParameter("LS_polling", "true");
                this.addParameter("LS_polling_millis", options.PollingInterval + delay);
                this.addParameter("LS_idle_millis", options.IdleTimeout);
            }
            else
            {
                if (options.KeepaliveInterval > 0)
                {
                    this.addParameter("LS_keepalive_millis", options.KeepaliveInterval);
                }

                if (maxReverseHeartbeatIntervalMs > 0)
                {
                    this.addParameter("LS_inactivity_millis", maxReverseHeartbeatIntervalMs);
                }

                if (addContentLength)
                {
                    this.addParameter("LS_content_length", options.ContentLength);
                }
            }

            if (!string.ReferenceEquals(cause, null))
            {
                this.addParameter("LS_cause", cause);
            }
        }

        public override string RequestName
        {
            get
            {
                return "bind_session";
            }
        }

        public override bool SessionRequest
        {
            get
            {
                return true;
            }
        }
    }
}