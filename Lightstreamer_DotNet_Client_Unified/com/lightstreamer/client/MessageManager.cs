using com.lightstreamer.client.events;
using com.lightstreamer.util;
using Lightstreamer.DotNet.Logging.Log;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

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
    using ClientMessageAbortEvent = com.lightstreamer.client.events.ClientMessageAbortEvent;
    using ClientMessageDenyEvent = com.lightstreamer.client.events.ClientMessageDenyEvent;
    using ClientMessageDiscardedEvent = com.lightstreamer.client.events.ClientMessageDiscardedEvent;
    using ClientMessageErrorEvent = com.lightstreamer.client.events.ClientMessageErrorEvent;
    using ClientMessageProcessedEvent = com.lightstreamer.client.events.ClientMessageProcessedEvent;
    using EventsThread = com.lightstreamer.client.events.EventsThread;
    using InternalConnectionOptions = com.lightstreamer.client.session.InternalConnectionOptions;
    using MessageRequest = com.lightstreamer.client.requests.MessageRequest;
    using MessagesListener = com.lightstreamer.client.session.MessagesListener;
    using RequestTutor = com.lightstreamer.client.requests.RequestTutor;
    using SessionManager = com.lightstreamer.client.session.SessionManager;
    using SessionThread = com.lightstreamer.client.session.SessionThread;

    internal class MessageManager
    {
        private bool InstanceFieldsInitialized = false;

        private void InitializeInstanceFields()
        {
            eventsListener = new EventsListener(this);
        }

        private MessagesListener eventsListener;
        private SessionThread sessionThread;
        private SessionManager manager;
        private InternalConnectionOptions options;

        private Matrix<string, int, MessageWrap> forwardedMessages = new Matrix<string, int, MessageWrap>();
        private Matrix<string, int, MessageWrap> pendingMessages = new Matrix<string, int, MessageWrap>();
        private IDictionary<string, int> sequences = new Dictionary<string, int>();

        private readonly ILogger log = LogManager.GetLogger(Constants.SUBSCRIPTIONS_LOG);

        private int phase = 0;

        private bool sessionAlive = false;
        private EventDispatcher<ClientMessageListener> dispatcher;

        internal MessageManager(EventsThread eventsThread, SessionThread sessionThread, SessionManager manager, InternalConnectionOptions options)
        {
            if (!InstanceFieldsInitialized)
            {
                InitializeInstanceFields();
                InstanceFieldsInitialized = true;
            }
            this.sessionThread = sessionThread;
            this.manager = manager;
            this.options = options;

            this.dispatcher = new EventDispatcher<ClientMessageListener>(eventsThread);

            manager.MessagesListener = this.eventsListener;
        }

        private long fixedTimeout = 0;

        //ATM used only for testing purposes
        internal virtual long FixedTimeout
        {
            set
            {
                fixedTimeout = value;
            }
            get => fixedTimeout;
        }

        private T getFromSessionThread<T>(Task<T> fun)
        {
            // TODO which library class should we leverage instead?
            if (Thread.CurrentThread.Name.IndexOf("Session Thread") > -1)
            {
                // TODO use a more direct way to identify the session thread
                try
                {
                    fun.Start();
                    return fun.Result;
                }
                catch (Exception e)
                {
                    throw new Exception(e.Message);
                }
            }

            Container<T> container = new Container<T>();
            Semaphore sem = new Semaphore(0, 1);
            sessionThread.queue(new Task(() =>
            {
                try
                {
                    fun.Start();
                    container.value = fun.Result;
                }
                catch (Exception)
                {
                }
                sem.Release();
            }));
            try
            {
                sem.WaitOne();
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }
            return container.value;
        }

        //ATM used only for testing purposes;
        // may be invoked in any thread
        internal virtual bool ForwardedListEmpty => getFromSessionThread(new Task<Boolean>(() => { return this.forwardedMessages.Empty; }));

        //ATM used only for testing purposes;
        // may be invoked in any thread
        internal virtual bool PendingListEmpty => getFromSessionThread(new Task<Boolean>(() => { return this.pendingMessages.Empty; }));


        //ATM used only for testing purposes
        internal virtual MessagesListener Listener
        {
            get
            {
                return this.eventsListener;
            }
        }


        /// <summary>
        /// this is the only method called by the EventsThread, everything else comes from the SessionThread
        /// </summary>
        public virtual void send(string message, string seq, int delayTimeout, ClientMessageListener listener, bool enqueueWhileDisconnected)
        {
            if (log.IsDebugEnabled)
            {
                log.Debug("Evaluating message to be sent to server: " + message + ", " + sessionAlive);
            }

            sessionThread.queue(new Task(() =>
            {
                if (sessionAlive)
                {
                    sendMessage(message, seq, delayTimeout, listener);
                }
                else if (enqueueWhileDisconnected)
                {
                    queueMessage(message, seq, delayTimeout, listener);

                }
                else if (listener != null)
                {
                    if (log.IsDebugEnabled)
                    {
                        log.Debug("Client is disconnected, abort message: " + message);
                    }
                dispatcher.dispatchSingleEvent(new ClientMessageAbortEvent(message, false), listener);
                }
            }));
        }

        private int getNextSequenceNumber(string sequence)
        {
            int num = 0;
            if (!sequences.TryGetValue(sequence, out num))
            {
                sequences.Add(sequence, 2);
                return 1;
            }

            int next = num + 1;
            sequences[sequence] = next;
            return num;
        }

        internal virtual void resendMessage(MessageWrap envelope)
        {
            string sequence = envelope.request.Sequence;
            int number = envelope.request.MessageNumber;
            if (log.IsDebugEnabled)
            {
                log.Debug("No ack was received for a message; preparing it again: " + sequence + "|" + number);
            }

            //replace envelope
            envelope = envelope.makeClone();
            forwardMessage(sequence, number, envelope);
        }

        private void sendMessage(string message, string sequence, int delayTimeout, ClientMessageListener listener)
        {
            if (log.IsDebugEnabled)
            {
                log.Debug("Acquiring sequence number for the message: " + sequence );
            }

            int number;

            lock (sequences)
            {
                number = getNextSequenceNumber(sequence);
            }
            
            MessageRequest request = new MessageRequest(message, sequence, number, delayTimeout, listener != null);


            MessageWrap envelope = new MessageWrap(this, request, message, listener);

            if (log.IsDebugEnabled)
            {
                log.Debug("Forwarding message: " + sequence + "|" + number);
            }

            forwardMessage(sequence, number, envelope);
        }

        private void forwardMessage(string sequence, int number, MessageWrap envelope)
        {
            try
            {
                lock (forwardedMessages)
                {
                    forwardedMessages.insert(envelope, envelope.request.Sequence, envelope.request.MessageNumber);
                }
                
                log.Debug("Matrix Count: " + forwardedMessages.Count(envelope.request.Sequence) + " - " + envelope.request.Sequence + " < " + envelope.request.MessageNumber);

            }
            catch (Exception e)
            {
                log.Warn("Matrix error: " + e.Message);
            }

            RequestTutor messageTutor = new MessageTutor(this, sessionThread, 0, envelope, phase);

            if (log.IsDebugEnabled)
            {
                log.Debug("Sending message: " + sequence + "|" + number);
            }

            manager.sendMessage(envelope.request, messageTutor);
        }


        private void queueMessage(string message, string sequence, int delayTimeout, ClientMessageListener listener)
        {
            int number;

            lock (sequences)
            {
                number = getNextSequenceNumber(sequence);
            }

            if (log.IsDebugEnabled)
            {
                log.Debug("Client is disconnected, queue message for later use: " + sequence + "|" + number);
            }

            MessageRequest request = new MessageRequest(message, sequence, number, delayTimeout, listener != null);
            MessageWrap envelope = new MessageWrap(this, request, message, listener);

            pendingMessages.insert(envelope, sequence, number);
        }

        private void cleanMessage(string sequence, int number)
        {
            if (log.IsDebugEnabled)
            {
                log.Debug("Message handled, cleaning structures: " + sequence + "|" + number);
            }
            lock (forwardedMessages)
            {
                forwardedMessages.del(sequence, number);
            }
        }

        internal virtual void onSent(MessageWrap envelope)
        {
            envelope.sentOnNetwork = true;

            if (!envelope.request.needsAck())
            {
                //we will receive no other notifications for this message
                //dismiss related structures

                string sequence = envelope.request.Sequence;
                int number = envelope.request.MessageNumber;
                if (log.IsDebugEnabled)
                {
                    log.Debug("Not waiting for ack, message lifecycle reached its end: " + sequence + "|" + number);
                }
                cleanMessage(sequence, number);
            }
        }

        internal virtual void onAck(string sequence, int number)
        {
            if (log.IsDebugEnabled)
            {
                log.Debug("Ack received for message: " + sequence + "|" + number);
            }

            MessageWrap envelope;
            lock (forwardedMessages) {
                envelope = forwardedMessages.get(sequence, number);
            }
            
            if (envelope != null)
            {
                if (envelope.ack)
                {
                    log.Warn("Unexpected double ack for message: " + sequence + "|" + number);
                }
                else
                {
                    envelope.ack = true;
                }

                if (envelope.listener == null)
                {
                    if (log.IsDebugEnabled)
                    {
                        log.Debug("Ack received, no outcome expected, message lifecycle reached its end: " + sequence + "|" + number);
                    }
                    cleanMessage(sequence, number);
                }
            }
            else
            {
                log.Warn("Unexpected pair LS_sequence|LS_msg_prog: " + sequence + "|" + number);
            }

            log.Debug("Matrix Count -3-: " + forwardedMessages.Count(envelope.request.Sequence));
        }

        internal virtual void onOk(string sequence, int number)
        {
            if (log.IsDebugEnabled)
            {
                log.Debug("OK received for message: " + sequence + "|" + number);
            }

            MessageWrap envelope;
            lock (forwardedMessages)
            {
                envelope = forwardedMessages.get(sequence, number);
            }
            if (envelope != null)
            {
                if (envelope.listener != null)
                {
                    dispatcher.dispatchSingleEvent(new ClientMessageProcessedEvent(envelope.message), envelope.listener);
                }
                cleanMessage(sequence, number);
            }
            else
            {
                log.Warn("Unexpected pair LS_sequence|LS_msg_prog: " + sequence + "|" + number);
            }

            log.Debug("Matrix Count -2-: " + forwardedMessages.Count(envelope.request.Sequence));
        }

        internal virtual void onDeny(string sequence, int number, string denyMessage, int code)
        {
            if (log.IsDebugEnabled)
            {
                log.Debug("Denial received for message: " + sequence + "|" + number);
            }

            MessageWrap envelope;
            lock (forwardedMessages)
            {
                envelope = forwardedMessages.get(sequence, number);
            }
            if (envelope != null)
            {
                if (envelope.listener != null)
                {
                    dispatcher.dispatchSingleEvent(new ClientMessageDenyEvent(envelope.message, code, denyMessage), envelope.listener);
                }
                cleanMessage(sequence, number);
            }
            else
            {
                log.Warn("Unexpected pair LS_sequence|LS_msg_prog: " + sequence + "|" + number);
            }

            log.Debug("Matrix Count -4-: " + forwardedMessages.Count(envelope.request.Sequence));
        }

        internal virtual void onDiscarded(string sequence, int number)
        {
            if (log.IsDebugEnabled)
            {
                log.Debug("Discard received for message: " + sequence + "|" + number);
            }

            MessageWrap envelope;
            lock (forwardedMessages)
            {
                envelope = forwardedMessages.get(sequence, number);
            }
            if (envelope != null)
            {
                if (envelope.listener != null)
                {
                    dispatcher.dispatchSingleEvent(new ClientMessageDiscardedEvent(envelope.message), envelope.listener);
                }
                cleanMessage(sequence, number);
            }
            else
            {
                log.Warn("Unexpected pair LS_sequence|LS_msg_prog: " + sequence + "|" + number);
            }

            log.Debug("Matrix Count -5-: " + forwardedMessages.Count(envelope.request.Sequence));
        }

        internal virtual void onError(string sequence, int number, string errorMessage, int code)
        {
            if (log.IsDebugEnabled)
            {
                log.Debug("Error received for message: " + sequence + "|" + number);
            }

            MessageWrap envelope;
            lock (forwardedMessages)
            {
                envelope = forwardedMessages.get(sequence, number);
            }
            // envelope may not be in forwardedMessages because it has been removed (for example when LS_ack=false)
            if (envelope != null)
            {
                if (envelope.listener != null)
                {
                    if (code != 32 && code != 33)
                    {
                        /* errors 32 and 33 must not be notified to the user
                         * because they are due to late responses of the server */
                        dispatcher.dispatchSingleEvent(new ClientMessageErrorEvent(envelope.message), envelope.listener);
                    }
                }
                cleanMessage(sequence, number);
            }
            else
            {
                log.Warn("Unexpected pair LS_sequence|LS_msg_prog: " + sequence + "|" + number);
            }

            log.Debug("Matrix Count -6-: " + forwardedMessages.Count(envelope.request.Sequence));
        }

        private void reset()
        {
            log.Info("Reset message handler");
            sessionAlive = false;

            log.Debug("Aborting pending messages");
            abortAll(pendingMessages);
            abortAll(forwardedMessages);

            //reset the counters
            sequences = new Dictionary<string, int>();

            //these maps should already be empty
            if (!forwardedMessages.Empty || !pendingMessages.Empty)
            {
                log.Error("Unexpected: there are still messages in the structures");
                forwardedMessages = new Matrix<string, int, MessageWrap>();
                pendingMessages = new Matrix<string, int, MessageWrap>();
            }

            //avoid the Tutors late checks
            phase++;
        }

        private void start()
        {
            log.Info("Start message handler");
            sessionAlive = true;
            sendPending();
        }

        private void abortAll(Matrix<string, int, MessageWrap> messages)
        {
            // called at session end: we have to call abort on all the messages we had no answer for

            // we have to call the listeners in the proper order (within each sequence)
            lock (messages)
            {
                IList<MessageWrap> forwarded = messages.sortAndCleanMatrix();

                foreach (MessageWrap envelope in forwarded)
                {
                    if (envelope.listener != null)
                    {
                        dispatcher.dispatchSingleEvent(new ClientMessageAbortEvent(envelope.message, envelope.sentOnNetwork), envelope.listener);
                    }
                }
            }
        }

        private void sendPending()
        {
            // called at session start: we have to forward all the enqueued messages
            log.Debug("Sending queued messages");
            try
            {
                IList<MessageWrap> pendings = pendingMessages.sortAndCleanMatrix();

                foreach (MessageWrap envelope in pendings)
                {
                    forwardMessage(envelope.request.Sequence, envelope.request.MessageNumber, envelope);
                }
            }
            catch (Exception e)
            {
                log.Warn("Something went wrong: " + e.Message);
            }
        }

        private bool checkMessagePhase(int phase)
        {
            return this.phase == phase;
        }

        private class EventsListener : MessagesListener
        {
            private readonly MessageManager outerInstance;

            public EventsListener(MessageManager outerInstance)
            {
                this.outerInstance = outerInstance;
            }


            public virtual void onSessionStart()
            {
                outerInstance.start();
            }

            public virtual void onSessionClose()
            {
                outerInstance.reset();
            }

            public virtual void onMessageAck(string sequence, int number)
            {
                outerInstance.onAck(sequence, number);
            }

            public virtual void onMessageOk(string sequence, int number)
            {
                outerInstance.onOk(sequence, number);
            }

            public virtual void onMessageDeny(string sequence, int denyCode, string denyMessage, int number)
            {
                outerInstance.onDeny(sequence, number, denyMessage, denyCode);
            }

            public virtual void onMessageDiscarded(string sequence, int number)
            {
                outerInstance.onDiscarded(sequence, number);
            }

            public virtual void onMessageError(string sequence, int errorCode, string errorMessage, int number)
            {
                outerInstance.onError(sequence, number, errorMessage, errorCode);
            }

        }

        internal class MessageWrap
        {
            private readonly MessageManager outerInstance;

            public bool sentOnNetwork = false;
            public MessageRequest request;
            public ClientMessageListener listener;
            public string message;
            public bool ack = false;

            public MessageWrap(MessageManager outerInstance, MessageRequest request, string message, ClientMessageListener listener)
            {
                this.outerInstance = outerInstance;
                this.request = request;
                this.listener = listener;
                this.message = message;
            }

            public virtual MessageWrap makeClone()
            {
                // Note: the cloning is necessary so the clone gets a new requestId
                MessageRequest requestClone = new MessageRequest(this.request);
                return new MessageWrap(outerInstance, requestClone, this.message, this.listener);
            }
        }

        internal class MessageTutor : RequestTutor
        {
            private readonly MessageManager outerInstance;


            internal MessageWrap envelope;
            internal int phase;

            public MessageTutor(MessageManager outerInstance, SessionThread thread, int timeoutMs, MessageWrap envelope, int phase) : base(timeoutMs, thread, outerInstance.options, outerInstance.FixedTimeout > 0)
            {
                this.outerInstance = outerInstance;
                this.envelope = envelope;
                this.phase = phase;
            }

            public override void notifySender(bool failed)
            {
                base.notifySender(failed);

                if (!failed)
                {
                    outerInstance.onSent(envelope);
                }
            }

            protected internal override bool verifySuccess()
            {
                if (outerInstance.checkMessagePhase(this.phase))
                {
                    //phase is correct
                    string sequence = envelope.request.Sequence;
                    int number = envelope.request.MessageNumber;

                    if (outerInstance.forwardedMessages.get(sequence, number) != null)
                    {
                        //the message is still in the queue
                        if (!envelope.ack)
                        {
                            //the message has not been acknowledged yet
                            return false;
                        }
                    }
                }

                return true;
            }

            protected internal override void doRecovery()
            {
                outerInstance.resendMessage(envelope);
            }

            public override void notifyAbort()
            {
                // nothing to do (can't happen, this is called if
                // the request is dismissed because useless (e.g.: an 
                // unsubsription request can be aborted this way if
                // the related subscription request was not actually sent)
            }

            protected internal override bool TimeoutFixed
            {
                get
                {
                    return outerInstance.fixedTimeout > 0;
                }
            }

            protected internal override long FixedTimeout => throw new NotImplementedException();

            public override bool shouldBeSent()
            {
                return true;
            }
        }
    }

    internal class Container<T>
    {
        public T value;

        public Container()
        {
        }
    }
}