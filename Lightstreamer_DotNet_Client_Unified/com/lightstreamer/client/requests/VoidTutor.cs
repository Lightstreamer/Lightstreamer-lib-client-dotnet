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