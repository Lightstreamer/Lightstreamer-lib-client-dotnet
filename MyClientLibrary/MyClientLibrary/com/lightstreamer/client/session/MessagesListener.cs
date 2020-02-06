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
namespace com.lightstreamer.client.session
{
    /// 
    public interface MessagesListener
    {

        void onSessionStart();
        void onSessionClose();

        void onMessageAck(string sequence, int number);

        void onMessageOk(string sequence, int number);

        void onMessageDeny(string sequence, int denyCode, string denyMessage, int number);

        void onMessageDiscarded(string sequence, int number);

        void onMessageError(string sequence, int errorCode, string errorMessage, int number);
    }

}