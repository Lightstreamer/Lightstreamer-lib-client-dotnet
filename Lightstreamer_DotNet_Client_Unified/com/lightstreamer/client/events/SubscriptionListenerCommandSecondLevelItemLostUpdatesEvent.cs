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
    public class SubscriptionListenerCommandSecondLevelItemLostUpdatesEvent : Event<SubscriptionListener>
    {
        private readonly string key;
        private readonly int lostUpdates;

        public SubscriptionListenerCommandSecondLevelItemLostUpdatesEvent(int lostUpdates, string key)
        {
            this.key = key;
            this.lostUpdates = lostUpdates;
        }

        public virtual void applyTo(SubscriptionListener listener)
        {
            listener.onCommandSecondLevelItemLostUpdates(lostUpdates, key);
        }
    }
}