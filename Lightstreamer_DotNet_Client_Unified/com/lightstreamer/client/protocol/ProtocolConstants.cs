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

namespace com.lightstreamer.client.protocol
{
    public class ProtocolConstants
    {
        /// <summary>
        /// Answer sent by the Server to signal accepted requests.
        /// </summary>
        public const string conokCommand = "CONOK";

        /// <summary>
        /// Constant sent by the Server in case no data has been sent
        /// for a configured time.
        /// </summary>
        public const string probeCommand = "PROBE";

        /// <summary>
        /// Constant sent by the Server before closing the connection
        /// due to the content length consumption. Upon reception of this
        /// constant, the client will rebind the session in a new connection.
        /// </summary>
        public const string loopCommand = "LOOP";

        /// <summary>
        /// Constant sent by the Server before closing the connection because
        /// of a server-side explicit decision. Upon reception of this constant,
        /// the client should not try to recover by opening a new session.
        /// </summary>
        public const string endCommand = "END";

        /// <summary>
        /// Answer sent by the Server to signal the refusal of a request.
        /// This applies only to legal requests. Malformed requests may receive
        /// an unpredictable answer.
        /// </summary>
        public const string conerrCommand = "CONERR";

        /// <summary>
        /// End of snapshot marker, written after the Item code.
        /// </summary>
        public const string endOfSnapshotMarker = "EOS";

        /// <summary>
        /// Overflow notification marker, written after the Item code.
        /// </summary>
        public const string overflowMarker = "OV";

        /// <summary>
        /// Clear-snapshot notification marker, written after the Item code.
        /// </summary>
        public const string clearSnapshotMarker = "CS,";

        /// <summary>
        /// Message notification marker, written as first thing on message notification messages
        /// </summary>
        public const string msgMarker = "MSG";

        public const string subscribeMarker = "SUB";

        public const string unsubscribeMarker = "UNSUB,";

        public const string constrainMarker = "CONS,";

        public const string syncMarker = "SYNC,";

        public const string updateMarker = "U,";

        public const string configurationMarker = "CONF,";

        public const string serverNameMarker = "SERVNAME,";

        public const string clientIpMarker = "CLIENTIP,";

        public const string progMarker = "PROG,";

        public const string noopMarker = "NOOP,";

        public const string reqokMarker = "REQOK,";

        public const string reqerrMarker = "REQERR,";

        public const string errorMarker = "ERROR,";

        public const string mpnRegisterMarker = "MPNREG,";

        public const string mpnSubscribeMarker = "MPNOK,";

        public const string mpnUnsubscribeMarker = "MPNDEL,";

        public const string mpnResetBadgeMarker = "MPNZERO,";

        public const string UNCHANGED = "UNCHANGED";

        protected internal const bool SYNC_RESPONSE = false;
        protected internal const bool ASYNC_RESPONSE = true;

        public const string END_LINE = "\r\n";
    }
}