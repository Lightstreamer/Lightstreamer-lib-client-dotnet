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
    public class VoidTutor : RequestTutor
    {
        public VoidTutor(SessionThread thread, InternalConnectionOptions connectionOptions) : base(thread, connectionOptions)
        {
        }

        public override bool shouldBeSent()
        {
            return true;
        }

        protected internal override bool verifySuccess()
        {
            return true;
        }

        protected internal override void doRecovery()
        {
        }

        public override void notifyAbort()
        {
        }

        protected internal override bool TimeoutFixed
        {
            get
            {
                return false;
            }
        }

        protected internal override long FixedTimeout
        {
            get
            {
                return 0;
            }
        }

        protected internal override void startTimeout()
        {
            /* 
             * doesn't schedule a task on session thread since the void tutor doesn't need retransmissions
             */
        }
    }
}