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