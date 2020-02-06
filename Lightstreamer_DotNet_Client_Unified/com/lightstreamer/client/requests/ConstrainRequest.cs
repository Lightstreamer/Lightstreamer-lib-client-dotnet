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