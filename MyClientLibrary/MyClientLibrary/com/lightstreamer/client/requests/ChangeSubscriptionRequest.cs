using System.Diagnostics;

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
    public class ChangeSubscriptionRequest : ControlRequest
    {
        private int reconfId;
        private int subscriptionId;

        public ChangeSubscriptionRequest(int subscriptionId, double requestedMaxFrequency, int reconfId)
        {
            this.reconfId = reconfId;
            this.subscriptionId = subscriptionId;

            this.addParameter("LS_op", "reconf");
            this.addParameter("LS_subId", subscriptionId);

            Debug.Assert(requestedMaxFrequency != -2);
            Debug.Assert(requestedMaxFrequency != -1);
            if (requestedMaxFrequency == 0)
            {
                this.addParameter("LS_requested_max_frequency", "unlimited");
            }
            else if (requestedMaxFrequency > 0)
            {
                this.addParameter("LS_requested_max_frequency", requestedMaxFrequency);
            }

        }

        public virtual int ReconfId
        {
            get
            {
                return this.reconfId;
            }
        }

        public virtual int SubscriptionId
        {
            get
            {
                return this.subscriptionId;
            }
        }
    }
}