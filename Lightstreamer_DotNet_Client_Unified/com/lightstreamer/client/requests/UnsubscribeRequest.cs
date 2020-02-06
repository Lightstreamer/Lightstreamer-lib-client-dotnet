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
    public class UnsubscribeRequest : ControlRequest
    {
        private readonly int subscriptionId;

        public UnsubscribeRequest(int subId)
        {
            this.addParameter("LS_op", "delete");
            this.addParameter("LS_subId", subId);

            this.subscriptionId = subId;

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