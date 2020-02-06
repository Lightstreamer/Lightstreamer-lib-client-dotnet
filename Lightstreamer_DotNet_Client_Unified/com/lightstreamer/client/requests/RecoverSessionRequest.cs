namespace com.lightstreamer.client.requests
{
    using InternalConnectionOptions = com.lightstreamer.client.session.InternalConnectionOptions;

    /// <summary>
    /// A recovery request is a special type of bind_session request with the additional LS_recovery_from parameter.
    /// <para>
    /// The class was adapted from <seealso cref="CreateSessionRequest"/>.
    /// </para>
    /// </summary>
    public class RecoverSessionRequest : SessionRequest
    {
        public RecoverSessionRequest(string targetServer, string session, string cause, InternalConnectionOptions options, long delay, long sessionRecoveryProg) : base(true, delay)
        {
            this.Server = targetServer;

            this.addParameter("LS_polling", "true");

            if (!string.ReferenceEquals(cause, null))
            {
                this.addParameter("LS_cause", cause);
            }

            long requestedPollingInterval = delay > 0 ? delay : 0; // NB delay can be negative since it is computed by SlowingHandler
            long requestedIdleTimeout = 0;
            this.addParameter("LS_polling_millis", requestedPollingInterval);
            this.addParameter("LS_idle_millis", requestedIdleTimeout);

            if (options.InternalMaxBandwidth == 0)
            {
                // unlimited: just omit the parameter
            }
            else if (options.InternalMaxBandwidth > 0)
            {
                this.addParameter("LS_requested_max_bandwidth", options.InternalMaxBandwidth);
            }

            Session = session;

            addParameter("LS_recovery_from", sessionRecoveryProg);
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