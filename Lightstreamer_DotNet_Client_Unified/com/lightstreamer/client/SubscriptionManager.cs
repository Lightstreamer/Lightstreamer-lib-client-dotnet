using Lightstreamer.DotNet.Logging.Log;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

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
namespace com.lightstreamer.client
{
    using ChangeSubscriptionRequest = com.lightstreamer.client.requests.ChangeSubscriptionRequest;
    using IdGenerator = com.lightstreamer.util.IdGenerator;
    using InternalConnectionOptions = com.lightstreamer.client.session.InternalConnectionOptions;
    using RequestTutor = com.lightstreamer.client.requests.RequestTutor;
    using SessionManager = com.lightstreamer.client.session.SessionManager;
    using SessionThread = com.lightstreamer.client.session.SessionThread;
    using SubscribeRequest = com.lightstreamer.client.requests.SubscribeRequest;
    using SubscriptionsListener = com.lightstreamer.client.session.SubscriptionsListener;
    using UnsubscribeRequest = com.lightstreamer.client.requests.UnsubscribeRequest;

    internal class SubscriptionManager
    {
        private bool InstanceFieldsInitialized = false;

        private void InitializeInstanceFields()
        {
            eventsListener = new EventsListener(this);
        }

        protected internal readonly ILogger log = LogManager.GetLogger(Constants.SUBSCRIPTIONS_LOG);

        private readonly IDictionary<int, Subscription> subscriptions = new ConcurrentDictionary<int, Subscription>();
        /// <summary>
        /// Map recording unsubscription requests which have been sent but whose corresponding REQOK/SUBOK messages 
        /// have not yet been received.
        /// </summary>
        private readonly ISet<int> pendingDelete = new HashSet<int>();
        /// <summary>
        /// Map recording unsubscription requests which have not yet been sent because the corresponding items are still subscribing. 
        /// </summary>
        private readonly ISet<int> pendingUnsubscribe = new HashSet<int>();
        private readonly IDictionary<int, int> pendingSubscriptionChanges = new Dictionary<int, int>();

        private bool sessionAlive = false;
        private readonly SessionThread sessionThread;
        private readonly InternalConnectionOptions options;

        private SubscriptionsListener eventsListener;

        private SessionManager manager;

        internal SubscriptionManager(SessionThread sessionThread, SessionManager manager, InternalConnectionOptions options)
        {
            if (!InstanceFieldsInitialized)
            {
                InitializeInstanceFields();
                InstanceFieldsInitialized = true;
            }
            this.sessionThread = sessionThread;
            this.options = options;
            this.manager = manager;

            manager.SubscriptionsListener = this.eventsListener;
        }

        private long fixedTimeout = 0;
        //ATM used only for testing purposes
        internal virtual long FixedTimeout
        {
            set
            {
                fixedTimeout = value;
            }
            get
            {
                return fixedTimeout;
            }
        }

        //ATM used only for testing purposes
        internal virtual SubscriptionsListener Listener
        {
            get
            {
                return this.eventsListener;
            }
        }

        //this method is called from the eventsThread
        internal virtual void add(Subscription subscription)
        {
            sessionThread.queue(new System.Threading.Tasks.Task(() =>
            {
                doAdd(subscription);
            }));
        }

        public virtual void doAdd(Subscription subscription)
        {
            try
            {
                int subId = IdGenerator.NextSubscriptionId;

                subscriptions[subId] = subscription;

                log.Info("Adding subscription " + subId);

                subscription.onAdd(subId, this, sessionThread);

                log.Debug("Do Add for subscription " + subscriptions[subId] + " completed.");

                if (sessionAlive)
                {
                    subscribe(subscription);
                }
                else
                {
                    subscription.onPause();
                }
            }
            catch (Exception e)
            {
                log.Warn("Something wrong in doAdd for a subscription - " + e.Message);
            }
        }

        //this method is called from the eventsThread
        internal virtual void remove(Subscription subscription)
        {
            sessionThread.queue(new System.Threading.Tasks.Task(() =>
            {
                doRemove(subscription);
            }));
        }

        internal virtual void doRemove(Subscription subscription)
        {
            int subId = subscription.SubscriptionId;
            log.Info("removing subscription " + subId);
            if (sessionAlive)
            {
                if (subscription.Subscribing)
                {
                    pendingUnsubscribe.Add(subId);
                }
                else if (subscription.Subscribed)
                {
                    unsubscribe(subId);
                }
            }
            subscriptions.Remove(subId);
            subscription.onRemove();
        }

        internal virtual void changeFrequency(Subscription subscription)
        {
            log.Info("Preparing subscription frequency change: " + subscription.SubscriptionId);

            ChangeSubscriptionRequest request = subscription.generateFrequencyRequest();
            ChangeSubscriptionTutor tutor = new ChangeSubscriptionTutor(this, 0, sessionThread, options, request);

            pendingSubscriptionChanges[subscription.SubscriptionId] = request.ReconfId; //if reconfId is newer we don't care about the older one

            manager.sendSubscriptionChange(request, tutor);
        }

        private void changeFrequency(Subscription subscription, long timeoutMs, int reconfId)
        {
            log.Info("Preparing subscription frequency change again: " + subscription.SubscriptionId);

            ChangeSubscriptionRequest request = subscription.generateFrequencyRequest(reconfId);
            ChangeSubscriptionTutor tutor = new ChangeSubscriptionTutor(this, timeoutMs, sessionThread, options, request);

            pendingSubscriptionChanges[subscription.SubscriptionId] = request.ReconfId; //if reconfId is newer we don't care about the older one

            manager.sendSubscriptionChange(request, tutor);
        }

        private void subscribe(Subscription subscription)
        {
            //can't be off but might be inactive: to check that we have to synchronize, we probably don't want to do that
            //we might want to introduce a method shouldSend to the RequestTutor, better relay on the batch algorithm to abort 
            //useless requests

            log.Info("Preparing subscription: " + subscription.SubscriptionId);

            SubscribeRequest request = subscription.generateSubscribeRequest();

            log.Debug("Preparing subscription 2: " + request + ", " + subscription.getPhase() + ", " + sessionThread.SessionManager.SessionId + ", " + this.options);

            try
            {
                SubscribeTutor tutor = new SubscribeTutor(this, subscription.SubscriptionId, subscription.getPhase(), sessionThread, 0);

                log.Debug("Preparing subscription 3: " + tutor.session.SessionId);

                manager.sendSubscription(request, tutor);

                log.Debug("Preparing subscription 4.");
            }
            catch (Exception e)
            {
                log.Error("Something wrong in Subscription preparation: " + e.Message);
                log.Debug(e.StackTrace);
            }
        }

        private void resubscribe(Subscription subscription, long timeoutMs)
        {
            log.Info("Preparing to send subscription again: " + subscription.SubscriptionId);

            SubscribeRequest request = subscription.generateSubscribeRequest();

            SubscribeTutor tutor = new SubscribeTutor(this, subscription.SubscriptionId, subscription.getPhase(), sessionThread, timeoutMs);

            manager.sendSubscription(request, tutor);
        }

        internal virtual void sendAllSubscriptions()
        {
            try
            {
                //we clone just to avoid unexpected issues as in the pauseAllSubscriptions case
                //(see comment there for details)

                log.Debug("sendAllSubscriptions: " + subscriptions.Count);

                IDictionary<int, Subscription> copy = new ConcurrentDictionary<int, Subscription>(subscriptions);

                foreach (KeyValuePair<int, Subscription> subscriptionPair in copy.SetOfKeyValuePairs())
                {
                    Subscription subscription = subscriptionPair.Value;

                    log.Debug("sendAllSubscriptions - " + subscriptionPair.Key + " - " + subscriptionPair.Value);

                    if (subscription.SubTable)
                    {
                        log.Error("Second level subscriptions should not be in the list of paused subscriptions");
                        return;
                    }

                    subscription.onStart(); //wake up

                    subscribe(subscription);
                }

                log.Debug("sendAllSubscriptions done! ");
            } catch (Exception e)
            {
                log.Error("SendAllSubscriptions error: " + e.Message);
                log.Debug(" - ", e);
                try
                {
                    log.Debug("sendAllSubscriptions try recovery.");

                    foreach (KeyValuePair<int, Subscription> subscriptionPair in subscriptions.SetOfKeyValuePairs())
                    {
                        Subscription subscription = subscriptionPair.Value;

                        if (subscription.SubTable)
                        {
                            log.Error("Second level subscriptions should not be in the list of paused subscriptions");
                            return;
                        }

                        subscription.onStart(); //wake up

                        subscribe(subscription);
                    }

                    log.Debug("sendAllSubscriptions recovery done.");

                } catch (Exception ex)
                {
                    log.Error("SendAllSubscriptions recovery try error: " + ex.Message);
                    log.Debug(" - ", ex);
                }
            }
        }

        internal virtual void pauseAllSubscriptions()
        {
            //NOTE calling onPause on a two level subscriptions triggers doRemove calls 
            //for second-level subscriptions. 
            //To avoid unexpected behavior caused by remove calls while iterating
            //we either clone the list of subscriptions before iterating, or we 
            //iterate first to remove second-level subscriptions from the collection
            //and then iterate again to call the onPause on first-level subscriptions.
            //In the second case we should also avoid calling remove on the doRemove 
            //methods. 
            //To avoid complications I chose to go down the clone path.
            try {

                log.Debug("pauseAllSubscriptions: " + subscriptions.Count);

                IDictionary<int, Subscription> copy = new ConcurrentDictionary<int, Subscription>(subscriptions);

                foreach (KeyValuePair<int, Subscription> subscriptionPair in copy.SetOfKeyValuePairs())
                {
                    Subscription subscription = subscriptionPair.Value;

                    if (subscription.SubTable)
                    {
                        //no need to pause these, will be removed soon
                        return;
                    }

                    subscription.onPause();
                }

                log.Debug("pauseAllSubscriptions done!");
            } catch (Exception e)
            {
                log.Error("pauseAllSubscriptions error: " + e.Message);
                log.Debug(" - ", e);
                try
                {
                    log.Debug("pauseAllSubscriptions try recovery.");

                    foreach (KeyValuePair<int, Subscription> subscriptionPair in subscriptions.SetOfKeyValuePairs())
                    {
                        Subscription subscription = subscriptionPair.Value;

                        if (subscription.SubTable)
                        {
                            //no need to pause these, will be removed soon
                            return;
                        }

                        subscription.onPause();
                    }

                    log.Debug("pauseAllSubscriptions recovery done.");

                }
                catch (Exception ex)
                {
                    log.Error("pauseAllSubscriptions recovery try error: " + ex.Message);
                    log.Debug(" - ", ex);
                }
            }
        }

        internal virtual void clearAllPending()
        {
            this.pendingSubscriptionChanges.Clear();
            this.pendingDelete.Clear();
            this.pendingUnsubscribe.Clear();
        }

        internal virtual void unsubscribe(int subscriptionId)
        {
            log.Info("Preparing to send unsubscription: " + subscriptionId);
            pendingDelete.Add(subscriptionId);
            pendingUnsubscribe.Remove(subscriptionId);

            UnsubscribeRequest request = new UnsubscribeRequest(subscriptionId);
            UnsubscribeTutor tutor = new UnsubscribeTutor(this, subscriptionId, sessionThread, 0);

            manager.sendUnsubscription(request, tutor);
        }

        internal virtual void reunsubscribe(int subscriptionId, long timeoutMs)
        {
            log.Info("Preparing to send unsubscription again: " + subscriptionId);

            UnsubscribeRequest request = new UnsubscribeRequest(subscriptionId);
            UnsubscribeTutor tutor = new UnsubscribeTutor(this, subscriptionId, sessionThread, timeoutMs);

            manager.sendUnsubscription(request, tutor);
        }

        private class EventsListener : SubscriptionsListener
        {
            private readonly SubscriptionManager outerInstance;

            public EventsListener(SubscriptionManager outerInstance)
            {
                this.outerInstance = outerInstance;
            }

            public virtual void onSessionStart()
            {
                outerInstance.log.Debug("SubscriptionManager sessionAlive set to true.");

                outerInstance.sessionAlive = true;
                outerInstance.sendAllSubscriptions();
            }

            public virtual void onSessionClose()
            {
                outerInstance.log.Debug("SubscriptionManager sessionAlive set to false.");

                outerInstance.sessionAlive = false;
                outerInstance.pauseAllSubscriptions();
                outerInstance.clearAllPending();
            }

            internal virtual Subscription extractSubscriptionOrUnsubscribe(int subscriptionId)
            {
                try
                {
                    Subscription subscription = outerInstance.subscriptions[subscriptionId];
                    if (subscription != null)
                    {
                        return subscription;
                    }
                } catch (KeyNotFoundException)
                {
                    //the subscription was removed
                    //either we have a delete that is now pending
                    //or we skipped the unsubscribe because we didn't know 
                    //the status of the subscription (may only happen in case of
                    //synchronous onSubscription events or during
                    //onSubscription events)
                }

                if (!outerInstance.pendingDelete.Contains(subscriptionId))
                {
                    outerInstance.unsubscribe(subscriptionId);
                }
                return null;
            }

            public virtual void onUpdateReceived(int subscriptionId, int item, List<string> args)
            {
                Subscription subscription = extractSubscriptionOrUnsubscribe(subscriptionId);
                if (subscription == null)
                {
                    outerInstance.log.Debug(subscriptionId + " missing subscription, discarding update");
                    return;
                }

                if (outerInstance.log.IsDebugEnabled)
                {
                    outerInstance.log.Info(subscriptionId + " received an update");
                }

                subscription.update(args, item, false);
            }

            public virtual void onEndOfSnapshotEvent(int subscriptionId, int item)
            {
                Subscription subscription = extractSubscriptionOrUnsubscribe(subscriptionId);
                if (subscription == null)
                {
                    outerInstance.log.Debug(subscriptionId + " missing subscription, discarding end of snapshot event");
                    return;
                }

                if (outerInstance.log.IsDebugEnabled)
                {
                    outerInstance.log.Info(subscriptionId + " received end of snapshot event");
                }

                subscription.endOfSnapshot(item);
            }

            public virtual void onClearSnapshotEvent(int subscriptionId, int item)
            {
                Subscription subscription = extractSubscriptionOrUnsubscribe(subscriptionId);
                if (subscription == null)
                {
                    outerInstance.log.Debug(subscriptionId + " missing subscription, discarding clear snapshot event");
                    return;
                }

                if (outerInstance.log.IsDebugEnabled)
                {
                    outerInstance.log.Info(subscriptionId + " received clear snapshot event");
                }

                subscription.clearSnapshot(item);
            }

            public virtual void onLostUpdatesEvent(int subscriptionId, int item, int lost)
            {
                Subscription subscription = extractSubscriptionOrUnsubscribe(subscriptionId);
                if (subscription == null)
                {
                    outerInstance.log.Debug(subscriptionId + " missing subscription, discarding lost updates event");
                    return;
                }

                if (outerInstance.log.IsDebugEnabled)
                {
                    outerInstance.log.Info(subscriptionId + " received lost updates event");
                }

                subscription.lostUpdates(item, lost);
            }

            public virtual void onConfigurationEvent(int subscriptionId, string frequency)
            {
                Subscription subscription = extractSubscriptionOrUnsubscribe(subscriptionId);
                if (subscription == null)
                {
                    outerInstance.log.Debug(subscriptionId + " missing subscription, discarding configuration event");
                    return;
                }

                if (outerInstance.log.IsDebugEnabled)
                {
                    outerInstance.log.Info(subscriptionId + " received configuration event");
                }

                subscription.configure(frequency);
            }

            public virtual void onUnsubscriptionAck(int subscriptionId)
            {
                /* this method was extracted from onUnsubscription() to stop the retransmissions when a REQOK is received */
                outerInstance.pendingDelete.Remove(subscriptionId);
                if (outerInstance.pendingUnsubscribe.Contains(subscriptionId))
                {
                    outerInstance.unsubscribe(subscriptionId);
                }
            }

            public virtual void onUnsubscription(int subscriptionId)
            {
                outerInstance.log.Info(subscriptionId + " succesfully unsubscribed");
                outerInstance.pendingDelete.Remove(subscriptionId);
                if (outerInstance.pendingUnsubscribe.Contains(subscriptionId))
                {
                    outerInstance.unsubscribe(subscriptionId);
                }

                if (outerInstance.subscriptions.ContainsKey(subscriptionId))
                {
                    outerInstance.log.Error("Unexpected unsubscription event");
                    return;
                }
            }

            public virtual void onSubscriptionAck(int subscriptionId)
            {
                /* this method was extracted from onSubscription() to stop the retransmissions when a REQOK is received */
                Subscription subscription = extractSubscriptionOrUnsubscribe(subscriptionId);
                if (subscription == null)
                {
                    outerInstance.log.Debug(subscriptionId + " missing subscription, discarding subscribed event");
                    return;
                }
                subscription.onSubscriptionAck();
            }

            public virtual void onSubscription(int subscriptionId, int totalItems, int totalFields, int keyPosition, int commandPosition)
            {
                Subscription subscription = extractSubscriptionOrUnsubscribe(subscriptionId);
                if (subscription == null)
                {
                    outerInstance.log.Debug(subscriptionId + " missing subscription, discarding subscribed event");
                    return;
                }
                outerInstance.log.Info(subscriptionId + " succesfully subscribed");
                subscription.onSubscribed(commandPosition, keyPosition, totalItems, totalFields);
            }

            public virtual void onSubscription(int subscriptionId, long reconfId)
            {
                int? waitingId = outerInstance.pendingSubscriptionChanges[subscriptionId];
                if (waitingId == null)
                {
                    //don't care anymore
                    return;
                }

                //if lower we're still waiting the newer one
                //if equal we're done
                //higher is not possible
                if (reconfId == waitingId)
                {
                    outerInstance.pendingSubscriptionChanges.Remove(subscriptionId);
                }
            }

            public virtual void onSubscriptionError(int subscriptionId, int errorCode, string errorMessage)
            {
                Subscription subscription = extractSubscriptionOrUnsubscribe(subscriptionId);
                if (subscription == null)
                {
                    outerInstance.log.Debug(subscriptionId + " missing subscription, discarding error");
                    return;
                }
                outerInstance.log.Info(subscriptionId + " subscription error");
                subscription.onSubscriptionError(errorCode, errorMessage);
            }
        }

        private abstract class SubscriptionsTutor : RequestTutor
        {
            private readonly SubscriptionManager outerInstance;

            public SubscriptionsTutor(SubscriptionManager outerInstance, long currentTimeout, SessionThread thread, InternalConnectionOptions connectionOptions) : base(currentTimeout, thread, connectionOptions, outerInstance.fixedTimeout > 0)
            {
                this.outerInstance = outerInstance;
            }

            protected internal override bool TimeoutFixed
            {
                get
                {
                    return outerInstance.fixedTimeout > 0;
                }
            }
        }

        private class UnsubscribeTutor : SubscriptionsTutor
        {
            private readonly SubscriptionManager outerInstance;
            protected internal override long FixedTimeout => throw new System.NotImplementedException();

            internal int subscriptionId;

            public UnsubscribeTutor(SubscriptionManager outerInstance, int subscriptionId, SessionThread thread, long timeoutMs) : base(outerInstance, timeoutMs, thread, outerInstance.options)
            {
                this.outerInstance = outerInstance;
                this.subscriptionId = subscriptionId;
            }

            protected internal override bool verifySuccess()
            {
                return !outerInstance.pendingDelete.Contains(this.subscriptionId);
            }

            protected internal override void doRecovery()
            {
                outerInstance.reunsubscribe(this.subscriptionId, this.timeoutMs);
            }

            public override void notifyAbort()
            {
                //get rid of it
                outerInstance.pendingDelete.Remove(this.subscriptionId);
                outerInstance.pendingUnsubscribe.Remove(this.subscriptionId);
            }

            public override bool shouldBeSent()
            {
                return outerInstance.pendingDelete.Contains(this.subscriptionId);
            }
        }

        private class SubscribeTutor : SubscriptionsTutor
        {
            private readonly SubscriptionManager outerInstance;

            internal int subscriptionId;
            internal int subscriptionPhase;

            protected internal override long FixedTimeout => throw new System.NotImplementedException();

            public SubscribeTutor(SubscriptionManager outerInstance, int subscriptionId, int subscriptionPhase, SessionThread thread, long timeoutMs) : base(outerInstance, timeoutMs, thread, outerInstance.options)
            {
                this.outerInstance = outerInstance;
                this.subscriptionId = subscriptionId;
                this.subscriptionPhase = subscriptionPhase;
            }

            public override void notifySender(bool failed)
            {
                Subscription subscription = outerInstance.subscriptions[subscriptionId];
                if (subscription == null)
                {
                    log.Warn("Subscription not found [" + subscriptionId + "/" + outerInstance.manager.SessionId + "]");
                    return;
                }
                if (!subscription.checkPhase(subscriptionPhase))
                {
                    //we don't care
                    return;
                }

                base.notifySender(failed);
                if (!failed)
                {
                    subscription.onSubscriptionSent();
                    this.subscriptionPhase = subscription.getPhase();
                }
            }

            protected internal override bool verifySuccess()
            {
                Subscription subscription = outerInstance.subscriptions[subscriptionId];
                if (subscription == null)
                {
                    //subscription was removed, no need to keep going, let's say it's a success
                    return true;
                }
                if (!subscription.checkPhase(subscriptionPhase))
                {
                    //something else happened, consider it a success
                    return true;
                }
                return subscription.Subscribed; //== return false
            }

            protected internal override void doRecovery()
            {
                Subscription subscription = outerInstance.subscriptions[subscriptionId];
                if (subscription == null)
                {
                    //subscription was removed, no need to keep going
                    return;
                }
                if (!subscription.checkPhase(subscriptionPhase))
                {
                    //something else happened
                    return;
                }
                outerInstance.resubscribe(subscription, this.timeoutMs);
            }

            public override void notifyAbort()
            {
                // we don't have anything to do, it means that a 
                //delete was queued before the add was sent
                //so the subscription should not exists anymore

                /*if (subscriptions.containsKey(this.subscriptionId)) {
                  //might actually happen if we stop a 2nd subscription effort
                  log.error("Was not expecting to find the subscription as it was supposedly removed");
                }*/
            }

            public override bool shouldBeSent()
            {
                Subscription subscription = outerInstance.subscriptions[subscriptionId];
                if (subscription == null)
                {
                    //subscription was removed, no need to send the request
                    return false;
                }
                if (!subscription.checkPhase(subscriptionPhase))
                {
                    return false;
                }
                return true;
            }
        }

        private class ChangeSubscriptionTutor : SubscriptionsTutor
        {
            private readonly SubscriptionManager outerInstance;

            internal ChangeSubscriptionRequest request;

            protected internal override long FixedTimeout => throw new System.NotImplementedException();

            public ChangeSubscriptionTutor(SubscriptionManager outerInstance, long currentTimeout, SessionThread thread, InternalConnectionOptions connectionOptions, ChangeSubscriptionRequest request) : base(outerInstance, currentTimeout, thread, connectionOptions)
            {
                this.outerInstance = outerInstance;

                this.request = request;
            }

            protected internal override bool verifySuccess()
            {
                int? waitingId = outerInstance.pendingSubscriptionChanges[this.request.SubscriptionId];
                if (waitingId == null)
                {
                    return true;
                }

                int? reconfId = this.request.ReconfId;

                //if lower we don't care about this anymore
                //if equal we're still waiting
                //higher is not possible
                return reconfId < waitingId;
            }

            protected internal override void doRecovery()
            {
                Subscription subscription = outerInstance.subscriptions[this.request.SubscriptionId];
                if (subscription == null)
                {
                    //subscription was removed, no need to keep going
                    return;
                }
                outerInstance.changeFrequency(subscription, this.timeoutMs, this.request.ReconfId);
            }

            public override void notifyAbort()
            {
                int? waitingId = outerInstance.pendingSubscriptionChanges[this.request.SubscriptionId];
                if (waitingId == null)
                {
                    return;
                }

                int? reconfId = this.request.ReconfId;
                if (waitingId.Equals(reconfId))
                {
                    outerInstance.pendingSubscriptionChanges.Remove(this.request.SubscriptionId);
                }
            }

            public override bool shouldBeSent()
            {
                Subscription subscription = outerInstance.subscriptions[this.request.SubscriptionId];
                if (subscription == null)
                {
                    //subscription was removed, no need to keep going
                    return false;
                }

                int? waitingId = outerInstance.pendingSubscriptionChanges[this.request.SubscriptionId];
                if (waitingId == null)
                {
                    return false;
                }

                int? reconfId = this.request.ReconfId;

                //if lower we don't care about this anymore
                //if equal we're still waiting
                //higher is not possible
                return reconfId.Equals(waitingId);
            }
        }
    }
}