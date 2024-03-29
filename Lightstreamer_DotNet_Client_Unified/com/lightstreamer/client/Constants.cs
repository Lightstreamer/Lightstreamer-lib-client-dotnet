﻿#region License
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

using System.Collections.Generic;

namespace com.lightstreamer.client
{
    public class Constants
    {
        public const string TLCP_VERSION = "TLCP-2.1.0";

        public const string ACTIONS_LOG = "lightstreamer.actions";
        public const string SESSION_LOG = "lightstreamer.session";
        public const string SUBSCRIPTIONS_LOG = "lightstreamer.subscribe";
        public const string PROTOCOL_LOG = "lightstreamer.protocol";
        public const string TRANSPORT_LOG = "lightstreamer.stream";
        public const string THREADS_LOG = "lightstreamer.threads";
        public const string NETTY_LOG = "lightstreamer.netty";
        public const string NETTY_POOL_LOG = "lightstreamer.netty.pool";
        public const string REQUESTS_LOG = "lightstreamer.requests";
        public const string UTILS_LOG = "lightstreamer.utils";
        public const string MPN_LOG = "lightstreamer.mpn";
        public const string HEARTBEAT_LOG = "lightstreamer.heartbeat";
        public const string STATS_LOG = "lightstreamer.statistics";


        public const string UNLIMITED = "unlimited";
        public const string AUTO = "auto";

        public static readonly ISet<string> FORCED_TRANSPORTS = new HashSet<string>();
        static Constants()
        {
            FORCED_TRANSPORTS.Add("HTTP");
            FORCED_TRANSPORTS.Add("HTTP-POLLING");
            FORCED_TRANSPORTS.Add("HTTP-STREAMING");
            FORCED_TRANSPORTS.Add("WS");
            FORCED_TRANSPORTS.Add("WS-POLLING");
            FORCED_TRANSPORTS.Add("WS-STREAMING");
            FORCED_TRANSPORTS.Add(null);
            PROXY_TYPES.Add("HTTP");
            // At the moment only HTTP proxy are supported.
            // PROXY_TYPES.Add("SOCKS4");
            // PROXY_TYPES.Add("SOCKS5");
            MODES.Add(MERGE);
            MODES.Add(COMMAND);
            MODES.Add(DISTINCT);
            MODES.Add(RAW);
        }

        public static readonly ISet<string> PROXY_TYPES = new HashSet<string>();

        public const string COMMAND = "COMMAND";
        public const string RAW = "RAW";
        public const string MERGE = "MERGE";
        public const string DISTINCT = "DISTINCT";

        public const string UNORDERED_MESSAGES = "UNORDERED_MESSAGES";

        public static readonly ISet<string> MODES = new HashSet<string>();

        public const string DISCONNECTED = "DISCONNECTED";
        public static readonly string WILL_RETRY = DISCONNECTED + ":WILL-RETRY";
        public static readonly string TRYING_RECOVERY = DISCONNECTED + ":TRYING-RECOVERY";
        public const string CONNECTING = "CONNECTING";
        public const string CONNECTED = "CONNECTED:";
        public const string STALLED = "STALLED";
        public const string HTTP_POLLING = "HTTP-POLLING";
        public const string HTTP_STREAMING = "HTTP-STREAMING";
        public const string SENSE = "STREAM-SENSING";
        public const string WS_STREAMING = "WS-STREAMING";
        public const string WS_POLLING = "WS-POLLING";

        public const string WS_ALL = "WS";
        public const string HTTP_ALL = "HTTP";

        public const string DELETE = "DELETE";
        public const string UPDATE = "UPDATE";
        public const string ADD = "ADD";

        /// <summary>
        /// Number of milliseconds after which an idle socket is closed.
        /// </summary>
        public const int CLOSE_SOCKET_TIMEOUT_MILLIS = 5000;
    }
}