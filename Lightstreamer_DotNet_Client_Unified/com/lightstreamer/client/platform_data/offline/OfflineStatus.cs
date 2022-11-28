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

using com.lightstreamer.util;
using Lightstreamer_DotNet_Client_Unified.com.lightstreamer.client.platform_data.offline;
using System;

namespace com.lightstreamer.client.platform_data.offline
{
    public class OfflineStatus
    {
        private static readonly AlternativeLoader<OfflineStatusInterface> loader = new AlternativeLoaderAnonymousInnerClass();

        private class AlternativeLoaderAnonymousInnerClass : AlternativeLoader<OfflineStatusInterface>
        {
            protected internal override string[] DefaultClassNames
            {
                get
                {
                    string[] classes = new string[] { "com.lightstreamer.client.platform_data.offline.JavaSEOfflineStatus", "com.lightstreamer.client.platform_data.offline.AndroidOfflineStatus", "com.lightstreamer.client.platform_data.offline.CSOfflineStatus" };

                    return classes;
                }
            }
        }

        private static OfflineStatusInterface implementation;

        public static OfflineStatusInterface Default
        {
            set
            {
                if (value == null)
                {
                    throw new System.ArgumentException("Specify an implementation");
                }
                implementation = value;
            }
        }

        public static bool isOffline(string server)
        {
            if (implementation == null)
            {
                lock (typeof(OfflineStatus))
                {
                    implementation = new CSOfflineStatus();

                    if (implementation == null)
                    {
                        Console.Error.WriteLine("NO OFFLINE-CHECK CLASS AVAILABLE, SOMETHING WENT WRONG AT BUILD TIME, CONTACT LIGHTSTREAMER SUPPORT");
                        implementation = new OfflineStatusInterfaceAnonymousInnerClass(server);
                    }
                }
            }

            return implementation.isOffline(server);
        }

        private class OfflineStatusInterfaceAnonymousInnerClass : OfflineStatusInterface
        {
            private string server;

            public OfflineStatusInterfaceAnonymousInnerClass(string server)
            {
                this.server = server;
            }

            public bool isOffline(string server)
            {
                return false;
            }
        }


        public interface OfflineStatusInterface
        {
            bool isOffline(string server);
        }
    }
}