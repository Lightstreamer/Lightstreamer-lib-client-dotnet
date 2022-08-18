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
    using Lightstreamer.DotNet.Logging.Log;
    using InternalConnectionDetails = com.lightstreamer.client.session.InternalConnectionDetails;
    using InternalConnectionOptions = com.lightstreamer.client.session.InternalConnectionOptions;

    public class CreateSessionRequest : SessionRequest
    {
        protected internal readonly ILogger log = LogManager.GetLogger(Constants.PROTOCOL_LOG);

        public CreateSessionRequest(string targetServer, bool polling, string cause, InternalConnectionOptions options, InternalConnectionDetails details, long delay, string password, string oldSession) : base(polling, delay)
        {
            this.Server = targetServer;

            this.addParameter("LS_polling", "true");

            if (!string.ReferenceEquals(cause, null))
            {
                this.addParameter("LS_cause", cause);
            }

            long requestedPollingInterval = 0;
            long requestedIdleTimeout = 0;
            if (polling)
            {
                // we ask this polling interval to the server, but the server might
                // refuse it and specify a smaller value
                // NOTE the client might even wait less than specified by the server: 
                // we don't currently do that though
                requestedPollingInterval = options.PollingInterval + delay;
            }

            this.addParameter("LS_polling_millis", requestedPollingInterval);
            this.addParameter("LS_idle_millis", requestedIdleTimeout);

            this.addParameter("LS_cid", "jqWtj1twChtfDxikwp1ltvcB4CJ9Kj uWur8vy1sj.ks");

            if (options.InternalMaxBandwidth == 0)
            {
                // unlimited: just omit the parameter
            }
            else if (options.InternalMaxBandwidth > 0)
            {
                this.addParameter("LS_requested_max_bandwidth", options.InternalMaxBandwidth);
            }

            if (!string.ReferenceEquals(details.AdapterSet, null))
            {
                this.addParameter("LS_adapter_set", details.AdapterSet);
            }

            if (!string.ReferenceEquals(details.User, null))
            {
                this.addParameter("LS_user", details.User);
            }

            if (!string.ReferenceEquals(password, null))
            {
                this.addParameter("LS_password", password);
            }

            if (!string.ReferenceEquals(oldSession, null))
            {
                this.addParameter("LS_old_session", oldSession);
            }

            log.Debug("Create Request: " + this.TargetServer);
        }

        public override string RequestName
        {
            get
            {
                return "create_session";
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