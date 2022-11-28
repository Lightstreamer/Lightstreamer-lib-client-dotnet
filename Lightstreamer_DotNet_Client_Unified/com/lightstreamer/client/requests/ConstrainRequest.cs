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

namespace com.lightstreamer.client.requests
{
    public class ConstrainRequest : ControlRequest
    {

        private readonly double maxBandwidth;
        /// <summary>
        /// This field was added to distinguish between requests made by the client (where requestId equals clientId) 
        /// and requests made by retransmission algorithm (where requestId is different from clientId).
        /// </summary>
        private readonly long clientRequestId;

        /// <summary>
        /// Change-bandwidth request.
        /// </summary>
        /// <param name="maxBandwidth"> </param>
        /// <param name="parent"> if this is a retransmission, {@code parent} must be the original client request. 
        /// If this is a client request, {@code parent} must be null. </param>
        public ConstrainRequest(double maxBandwidth, ConstrainRequest parent)
        {
            this.addParameter("LS_op", "constrain");

            if (maxBandwidth == 0)
            {
                this.addParameter("LS_requested_max_bandwidth", "unlimited");
            }
            else if (maxBandwidth > 0)
            {
                this.addParameter("LS_requested_max_bandwidth", maxBandwidth);
            }

            this.maxBandwidth = maxBandwidth;
            this.clientRequestId = ( parent == null ? requestId : parent.ClientRequestId );
        }

        public virtual double MaxBandwidth
        {
            get
            {
                return this.maxBandwidth;
            }
        }

        /// <summary>
        /// The requestId of the original request made by the client. 
        /// It may be different from the <seealso cref="#getRequestId()"/> if this is a retransmission request.
        /// </summary>
        public virtual long ClientRequestId
        {
            get
            {
                return clientRequestId;
            }
        }
    }
}