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

using Lightstreamer.DotNet.Logging.Log;
using System.Threading;

namespace com.lightstreamer.util
{
    public class Assertions
    {
        private static readonly ILogger log = LogManager.GetLogger("ASSERT");

        public static bool SessionThread
        {
            get
            {
                if (!Thread.CurrentThread.Name.StartsWith("Session Thread"))
                {
                    log.Error("The method must be called by Session Thread. Instead the caller is " + Thread.CurrentThread);
                    return false;
                }
                return true;
            }
        }

        public static bool EventThread
        {
            get
            {
                if (!Thread.CurrentThread.Name.StartsWith("Events Thread"))
                {
                    //log.error("The method must be called by Event Thread. Instead the caller is " + Thread.currentThread());
                    return false;
                }
                return true;
            }
        }

        public static bool NettyThread
        {
            get
            {
                if (!Thread.CurrentThread.Name.StartsWith("Netty Thread"))
                {
                    //log.error("The method must be called by Netty Thread. Instead the caller is " + Thread.currentThread());
                    return false;
                }
                return true;
            }
        }

        /// <summary>
        /// Conditional operator.
        /// </summary>
        public static bool implies(bool a, bool b)
        {
            return !a || b;
        }

        /// <summary>
        /// Biconditional operator.
        /// </summary>
        public static bool iff(bool a, bool b)
        {
            return a == b;
        }
    }
}