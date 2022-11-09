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

using System.Threading;

namespace com.lightstreamer.util
{
    public class IdGenerator
    {
        private static long requestIdGenerator = 0;

        /// <summary>
        /// Generates the next request id used as the value of the parameter LS_reqId.
        /// </summary>
        public static long NextRequestId
        {
            get
            {
                return Interlocked.Increment(ref requestIdGenerator);
            }
        }

        private static int subscriptionIdGenerator = 0;

        /// <summary>
        /// Generates the next subscription id used as the value of the parameter LS_subId.
        /// </summary>
        public static int NextSubscriptionId
        {
            get
            {
                return Interlocked.Increment(ref subscriptionIdGenerator);
            }
        }
    }
}