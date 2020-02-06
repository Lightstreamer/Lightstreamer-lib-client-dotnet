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
namespace com.lightstreamer.client.events
{
    public class ClientMessageAbortEvent : Event<ClientMessageListener>
    {
        private string originalMessage;
        private bool sentOnNetwork;

        public ClientMessageAbortEvent(string originalMessage, bool sentOnNetwork)
        {
            this.originalMessage = originalMessage;
            this.sentOnNetwork = sentOnNetwork;
        }

        public virtual void applyTo(ClientMessageListener listener)
        {
            listener.onAbort(originalMessage, sentOnNetwork);
        }
    }
}