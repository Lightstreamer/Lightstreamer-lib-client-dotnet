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
    public class SubscriptionListenerItemLostUpdatesEvent : Event<SubscriptionListener>
    {
        private readonly int itemPos;
        private readonly int lostUpdates;
        private readonly string itemName;

        public SubscriptionListenerItemLostUpdatesEvent(string itemName, int itemPos, int lostUpdates)
        {
            this.itemPos = itemPos;
            this.lostUpdates = lostUpdates;
            this.itemName = itemName;
        }

        public virtual void applyTo(SubscriptionListener listener)
        {
            listener.onItemLostUpdates(itemName, itemPos, lostUpdates);
        }
    }
}