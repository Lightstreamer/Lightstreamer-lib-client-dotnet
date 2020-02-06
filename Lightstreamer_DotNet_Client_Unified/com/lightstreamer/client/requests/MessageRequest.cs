using System.Text;

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
namespace com.lightstreamer.client.requests
{
    /// 
    /// <summary>
    /// <b>NB1</b> A fire-and-forget behavior is enacted when the user calls <seealso cref="LightstreamerClient#sendMessage(String)"/>
    /// or <seealso cref="LightstreamerClient#sendMessage(String, String, int, com.lightstreamer.client.ClientMessageListener, boolean)"/>
    /// having (1) "UNORDERED_MESSAGES" as a sequence and (2) no listener. 
    /// In this scenario the server doesn't send acknowledgments (i.e. REQOK) and the client doesn't do retransmissions.
    /// In a fire-and-forget message request both the parameters LS_ack and LS_outcome are set to false.
    /// 
    /// <para>
    /// Flags set to true with respect to the presence of a listener and a ordered sequence.
    /// 
    /// <table border="1">
    /// <tr>
    ///  <th></th>
    ///  <th>listener</th>
    ///  <th>no listener</th>
    /// </tr>
    /// <tr>
    ///  <th>sequence</th>
    ///  <td>prog outcome ack</td>
    ///  <td>prog ack</td>
    /// </tr>
    /// <tr>
    ///  <th>no sequence</th>
    ///  <td>prog outcome ack</td>
    ///  <td>ack (only in HTTP)</td>
    /// </tr>
    /// </table>
    /// </para>
    /// </summary>
    public class MessageRequest : NumberedRequest
    {
        private string sequence;
        private string message;
        private int number;
        /// <summary>
        /// When false we have a fire-and-forget request.
        /// </summary>
        private bool needsProg = false;
        private long timeout;
        private bool hasListener;

        public MessageRequest(MessageRequest mr) : this(mr.message, mr.sequence, mr.number, mr.timeout, mr.hasListener)
        {
        }

        public MessageRequest(string message, string sequence, int number, long timeout, bool hasListener) : base()
        {
            this.message = message;
            this.number = number;
            this.sequence = sequence;
            this.timeout = timeout;
            this.hasListener = hasListener;

            this.addParameter("LS_message", message);

            //we add the prog if
            //1 - we expect an outcome (because we have a listener)
            //2 - the sequence is not unordered

            if (hasListener)
            {
                // this.addParameter("LS_outcome", "true"); (it's the default)
                needsProg = true;
            }
            else
            {
                this.addParameter("LS_outcome", "false");
            }

            if (!sequence.Equals(Constants.UNORDERED_MESSAGES))
            {
                this.addParameter("LS_sequence", sequence);
                if (timeout >= 0)
                {
                    this.addParameter("LS_max_wait", timeout);
                }
                needsProg = true;
            }
        }

        public override string RequestName
        {
            set { }
            get
            {
                return "msg";
            }
        }

        public virtual int MessageNumber
        {
            get
            {
                return this.number;
            }
        }

        public virtual string Sequence
        {
            get
            {
                return this.sequence;
            }
        }

        /// <summary>
        /// When true the client is expecting an acknowledgment (i.e. REQOK) from the server.
        /// When false we have a fire-and-forget request.
        /// </summary>
        public virtual bool needsAck()
        {
            return needsProg;
        }

        protected internal virtual StringBuilder getQueryStringBuilder(string defaultSessionId, bool includeProg, bool ackIsForced)
        {
            StringBuilder query = base.getQueryStringBuilder(defaultSessionId);
            if (includeProg)
            {
                // addParameter(query,"LS_ack", "true"); (it's the default)
                addParameter(query, "LS_msg_prog", number);
            }
            else
            {
                if (ackIsForced)
                {
                    // LS_ack is not supported, as the ack is always sent by the transport
                }
                else
                {
                    addParameter(query, "LS_ack", "false");
                }
            }
            return query;
        }

        public override string TransportUnawareQueryString
        {
            get
            {
                StringBuilder query = this.getQueryStringBuilder(null, this.needsProg, false);
                return query.ToString();
            }
        }

        public override string getTransportAwareQueryString(string defaultSessionId, bool ackIsForced)
        {
            StringBuilder query = this.getQueryStringBuilder(defaultSessionId, this.needsProg, ackIsForced);
            return query.ToString();
        }
    }
}