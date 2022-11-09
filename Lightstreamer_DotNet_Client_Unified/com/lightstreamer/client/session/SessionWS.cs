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

using com.lightstreamer.client.protocol;
using com.lightstreamer.client.requests;
using com.lightstreamer.client.transport;
using com.lightstreamer.util;
using DotNetty.Common.Concurrency;
using System;
using System.Diagnostics;

namespace com.lightstreamer.client.session
{

    public class SessionWS : Session
    {
        private bool InstanceFieldsInitialized = false;

        private void InitializeInstanceFields()
        {
            wsMachine = new StateMachine(this);
        }


        private enum WsState
        {
            WS_NOT_CONNECTED,
            WS_CONNECTING,
            WS_CONNECTED,
            WS_BROKEN
        }

        private class StateMachine
        {
            public readonly SessionWS outerInstance;

            public StateMachine(SessionWS outerInstance)
            {
                this.outerInstance = outerInstance;
            }

            internal WsState state = WsState.WS_NOT_CONNECTED;
            internal string controlLink;
            internal ListenableFuture openWsFuture;

            internal virtual void createSent()
            {
                switch (state)
                {
                    case com.lightstreamer.client.session.SessionWS.WsState.WS_NOT_CONNECTED:
                        if (outerInstance.earlyOpen)
                        {
                            next(WsState.WS_CONNECTING, "createSent");
                            Debug.Assert(string.ReferenceEquals(controlLink, null));
                            openWS();
                        }
                        break;

                    default:
                        Debug.Assert(false);
                        break;
                }
            }

            internal virtual ListenableFuture sendBind(string bindCause)
            {

                switch (state)
                {
                    case com.lightstreamer.client.session.SessionWS.WsState.WS_NOT_CONNECTED:
                        next(WsState.WS_CONNECTING, "sendBind");
                        openWS();
                        return outerInstance.bindSessionExecution(bindCause);

                    case com.lightstreamer.client.session.SessionWS.WsState.WS_BROKEN:
                        next(WsState.WS_BROKEN, "sendBind");
                        outerInstance.handler.streamSenseSwitch(outerInstance.handlerPhase, "ws.error", outerInstance.phase, outerInstance.recoveryBean.Recovery);
                        return ListenableFuture.rejected();

                    default:
                        Debug.Assert(state == WsState.WS_CONNECTED || state == WsState.WS_CONNECTING);
                        next(state, "sendBind");
                        return outerInstance.bbindSessionExecution(bindCause);
                }
            }

            internal virtual void changeControlLink(string newControlLink)
            {
                switch (state)
                {
                    case com.lightstreamer.client.session.SessionWS.WsState.WS_NOT_CONNECTED:
                        Debug.Assert(!outerInstance.earlyOpen);
                        next(WsState.WS_NOT_CONNECTED, "clink");
                        controlLink = newControlLink;
                        break;

                    case com.lightstreamer.client.session.SessionWS.WsState.WS_CONNECTING:
                    case com.lightstreamer.client.session.SessionWS.WsState.WS_CONNECTED:
                    case com.lightstreamer.client.session.SessionWS.WsState.WS_BROKEN:
                        Debug.Assert(openWsFuture != null);
                        next(WsState.WS_CONNECTING, "clink");
                        controlLink = newControlLink;
                        openWsFuture.abort();
                        openWS();
                        break;
                }
            }

            internal virtual void connectionOK()
            {
                Debug.Assert(state == WsState.WS_CONNECTING);
                next(WsState.WS_CONNECTED, "ok");
                // sendBind("loop1");
            }

            internal virtual void connectionError()
            {
                Debug.Assert(state == WsState.WS_CONNECTING);
                next(WsState.WS_BROKEN, "error");
                if (outerInstance.@is(OFF) || outerInstance.@is(CREATING) || outerInstance.@is(STALLED) || outerInstance.@is(CREATED))
                {
                    //this is an error on a early open, we can't act now as we must wait for the loop from the create
                    //otherwise we would waste the entire session
                    //NOPPING!
                }
                else
                {
                    outerInstance.launchTimeout("zeroDelay", 0, "ws.broken.wait", false);
                }
            }

            internal async virtual void openWS()
            {
                string cLink = ( string.ReferenceEquals(controlLink, null) ? outerInstance.PushServerAddress : controlLink );
                Debug.Assert(openWsFuture == null || openWsFuture.getState() == ListenableFuture.State.ABORTED);

                openWsFuture = outerInstance.protocol.openWebSocketConnection(cLink).onFulfilled(new MyRunnableConnectOK(this)).onRejected(new MyRunnableError(this));
            }

            internal virtual void next(WsState nextState, string @event)
            {
                if (outerInstance.log.IsDebugEnabled)
                {
                    outerInstance.log.Debug("SessionWS state change (" + outerInstance.objectId + ") (" + @event + "): " + state + ( state != nextState ? " -> " + nextState : "" ));
                }
                state = nextState;
            }
        }

        /// <summary>
        /// iOS hack to avoid strong back references between Runnable and StateMachine.
        /// </summary>
        private class MyRunnableConnectOK : IRunnable
        {
            internal readonly WeakReference<StateMachine> @ref;

            internal MyRunnableConnectOK(StateMachine sm)
            {
                @ref = new WeakReference<StateMachine>(sm);
            }

            public void Run()
            {


                StateMachine sm;
                @ref.TryGetTarget(out sm);

                if (sm != null)
                {
                    sm.connectionOK();
                }
            }
        }

        /// <summary>
        /// iOS hack to avoid strong back references between Runnable and StateMachine.
        /// </summary>
        private class MyRunnableError : IRunnable
        {
            internal readonly WeakReference<StateMachine> @ref;

            internal MyRunnableError(StateMachine sm)
            {
                @ref = new WeakReference<StateMachine>(sm);
            }

            public void Run()
            {
                StateMachine sm;
                @ref.TryGetTarget(out sm);
                if (sm != null)
                {
                    sm.connectionError();
                }
            }
        }

        private readonly bool earlyOpen;
        private StateMachine wsMachine;

        public SessionWS(int objectId, bool isPolling, bool forced, SessionListener handler, SubscriptionsListener subscriptions, MessagesListener messages, Session originalSession, SessionThread thread, Protocol protocol, InternalConnectionDetails details, InternalConnectionOptions options, int callerPhase, bool retryAgainIfStreamFails, bool sessionRecovery) : base(objectId, isPolling, forced, handler, subscriptions, messages, originalSession, thread, protocol, details, options, callerPhase, retryAgainIfStreamFails, sessionRecovery)
        {
            if (!InstanceFieldsInitialized)
            {
                InitializeInstanceFields();
                InstanceFieldsInitialized = true;
            }
            this.earlyOpen = options.EarlyWSOpenEnabled && !WebSocket.Disabled;
        }

        protected internal override void createSent()
        {
            // create_session request has been sent
            base.createSent();
            wsMachine.createSent();
        }

        protected internal override string ConnectedHighLevelStatus
        {
            get
            {
                return this.isPolling ? Constants.WS_POLLING : Constants.WS_STREAMING;
            }
        }

        protected internal override string FirstConnectedStatus
        {
            get
            {
                return Constants.SENSE;
            }
        }

        protected internal override bool shouldAskContentLength()
        {
            return false;
        }

        public override void sendReverseHeartbeat(ReverseHeartbeatRequest request, RequestTutor tutor)
        {
            base.sendReverseHeartbeat(request, tutor);
        }

        protected ListenableFuture bbindSessionExecution(string bindCause)
        {
            return base.bindSessionExecution(bindCause);
        }

        protected internal override ListenableFuture bindSessionExecution(string bindCause)
        {
            // LOOP received from create_session response

            return wsMachine.sendBind(bindCause);

        }

        protected internal override void changeControlLink(string controlLink)
        {
            // CONOK received from create_session response and control link changed
            wsMachine.changeControlLink(controlLink);
        }

        protected internal override void doOnErrorEvent(string reason, bool closedOnServer, bool unableToOpen, bool startRecovery, long timeLeftMs, bool wsError)
        {
            if (wsError)
            {
                if (@is(OFF) || @is(CREATING) || @is(CREATED))
                {
                    log.Info("WebSocket was broken before it was used");
                    //this is an error on a early open, we can't act now as we must wait for the loop from the create
                    //otherwise we would waste the entire session
                    //NOPPING!

                }
                else if (@is(FIRST_PAUSE))
                {
                    log.Info("WebSocket was broken while we were waiting the first bind");
                    //as the bind was not yet sent (otherwise we would be in the FIRST_BINDING phase) we can recover
                    //binding via HTTP
                    handler.streamSenseSwitch(handlerPhase, reason, phase, recoveryBean.Recovery);

                }
                else
                {
                    base.doOnErrorEvent(reason, closedOnServer, unableToOpen, startRecovery, timeLeftMs, wsError);
                }

            }
            else
            {
                base.doOnErrorEvent(reason, closedOnServer, unableToOpen, startRecovery, timeLeftMs, wsError);
            }
        }
    }
}