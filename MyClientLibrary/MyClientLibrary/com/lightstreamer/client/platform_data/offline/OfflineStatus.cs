using com.lightstreamer.util;
using Lightstreamer_DotNet_Client_Unified.com.lightstreamer.client.platform_data.offline;
using System;

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