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
    public class ClientListenerServerErrorEvent : Event<ClientListener>
    {
        private readonly string errorMessage;
        private readonly int errorCode;

        public ClientListenerServerErrorEvent(int errorCode, string errorMessage)
        {
            this.errorCode = errorCode;
            this.errorMessage = errorMessage;
        }

        public virtual void applyTo(ClientListener listener)
        {
            listener.onServerError(errorCode, errorMessage);
        }
    }
}