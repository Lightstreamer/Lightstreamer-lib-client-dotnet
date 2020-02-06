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
using com.lightstreamer.util;

namespace com.lightstreamer.client.requests
{
    public class SubscribeRequest : ControlRequest
    {
        private int id;

        public SubscribeRequest(int subId, string mode, Descriptor items, Descriptor fields, string dataAdapter, string selector, string requiredSnapshot, double requestedMaxFrequency, int requestedBufferSize)
        {
            this.id = subId;

            this.addParameter("LS_op", "add");
            this.addParameter("LS_subId", subId);

            this.addParameter("LS_mode", mode);
            this.addParameter("LS_group", items.ComposedString);
            this.addParameter("LS_schema", fields.ComposedString);

            if (!string.ReferenceEquals(dataAdapter, null))
            {
                this.addParameter("LS_data_adapter", dataAdapter);
            }

            if (!string.ReferenceEquals(selector, null))
            {
                this.addParameter("LS_selector", selector);
            }

            if (!string.ReferenceEquals(requiredSnapshot, null))
            {
                if (requiredSnapshot.Equals("yes"))
                {
                    this.addParameter("LS_snapshot", "true");
                }
                else if (requiredSnapshot.Equals("no"))
                {
                    this.addParameter("LS_snapshot", "false");
                }
                else
                {
                    this.addParameter("LS_snapshot", requiredSnapshot);
                }
            }

            if (requestedMaxFrequency == -2)
            {
                // server default: just omit the parameter
            }
            else if (requestedMaxFrequency == -1)
            {
                this.addParameter("LS_requested_max_frequency", "unfiltered");
            }
            else if (requestedMaxFrequency == 0)
            {
                this.addParameter("LS_requested_max_frequency", "unlimited");
            }
            else if (requestedMaxFrequency > 0)
            {
                this.addParameter("LS_requested_max_frequency", requestedMaxFrequency);
            }

            if (requestedBufferSize == -1)
            {
                // server default: just omit the parameter
            }
            else if (requestedBufferSize == 0)
            {
                this.addParameter("LS_requested_buffer_size", "unlimited");
            }
            else if (requestedBufferSize > 0)
            {
                this.addParameter("LS_requested_buffer_size", requestedBufferSize);
            }

            //LS_start & LS_end are obsolete, removed from APIs
        }

        public virtual int SubscriptionId
        {
            get
            {
                return this.id;
            }
        }
    }
}