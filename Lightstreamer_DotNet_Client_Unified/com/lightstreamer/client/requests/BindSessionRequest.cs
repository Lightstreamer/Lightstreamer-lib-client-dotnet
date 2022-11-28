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