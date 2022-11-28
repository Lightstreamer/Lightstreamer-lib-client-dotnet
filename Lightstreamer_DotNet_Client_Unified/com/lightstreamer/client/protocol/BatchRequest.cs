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

using com.lightstreamer.client.requests;
using com.lightstreamer.client.transport;
using Lightstreamer.DotNet.Logging.Log;
using System.Collections.Generic;

namespace com.lightstreamer.client.protocol
{
    public class BatchRequest
    {
        public const int MESSAGE = 1;
        public const int HEARTBEAT = 2;
        //public static final int LOG = 3;
        public const int CONTROL = 4;

        private const string CONSTRAINT_KEY = "C";
        private const string FORCE_REBIND_KEY = "F";
        private const string CHANGE_SUB_KEY = "X";
        private const string MPN_KEY = "M";

        internal IDictionary<string, RequestObjects> keys = new Dictionary<string, RequestObjects>();
        IList<string> queue = new List<string>();

        protected internal readonly ILogger log = LogManager.GetLogger(Constants.SUBSCRIPTIONS_LOG);

        private int batchType;
        private int messageNextKey = 0;


        public BatchRequest(int type)
        {
            this.batchType = type;
        }

        public virtual int Length
        {
            get
            {
                return queue.Count;
            }
        }

        public virtual string RequestName
        {
            get
            {
                if (this.Length <= 0)
                {
                    return null;
                }
                return keys[queue[0]].request.RequestName;
            }
        }

        public virtual long NextRequestLength
        {
            get
            {
                if (this.Length <= 0)
                {
                    return 0;
                }
                return keys[queue[0]].request.TransportUnawareQueryString.Length;
                // TODO we use the longest estimate, as we have no transport information here 
            }
        }

        public virtual RequestObjects shift()
        {
            if (this.Length <= 0)
            {
                return null;
            }


            string key = queue[0];
            queue.RemoveAt(0);
            RequestObjects k = keys.GetValueOrNull(key);
            keys.Remove(key);
            return k;
        }

        private void addRequestInternal(int key, RequestObjects request)
        {
            this.addRequestInternal(key.ToString(), request);
        }
        private void addRequestInternal(string key, RequestObjects request)
        {
            this.keys[key] = request;
            this.queue.Add(key);
        }
        private void substituteRequest(string key, RequestObjects newRequest)
        {
            this.keys[key] = newRequest;
        }

        public virtual bool addRequestToBatch(MessageRequest request, RequestTutor tutor, RequestListener listener)
        {
            if (this.batchType != MESSAGE)
            {
                log.Error("Unexpected request type was given to batch");
                return false;
            }

            //I should only add to queue, the sendMessages are always sent to the server
            RequestObjects message = new RequestObjects(request, tutor, listener);
            this.addRequestInternal(this.messageNextKey++, message);
            return true;
        }

        public virtual bool addRequestToBatch(ReverseHeartbeatRequest request, RequestTutor tutor, RequestListener listener)
        {
            if (this.batchType != HEARTBEAT)
            {
                log.Error("Unexpected request type was given to batch");
                return false;
            }

            //I should only add to queue, the heart-beats are always sent to the server
            RequestObjects hb = new RequestObjects(request, tutor, listener);
            this.addRequestInternal(this.messageNextKey++, hb);
            return true;
        }

        public virtual bool addRequestToBatch(ConstrainRequest request, RequestTutor tutor, RequestListener listener)
        {
            if (this.batchType != CONTROL)
            {
                log.Error("Unexpected request type was given to batch");
                return false;
            }

            //can we queue costrain rebind for 2 different sessions? (NO)

            string key = CONSTRAINT_KEY;

            RequestObjects requestObj = new RequestObjects(request, tutor, listener);

            RequestObjects queuedRequest = null;
            keys.TryGetValue(key, out queuedRequest);
            if (queuedRequest != null)
            {
                queuedRequest.tutor.notifyAbort();
                this.substituteRequest(key, requestObj);
            }
            else
            {
                this.addRequestInternal(key, requestObj);
            }

            return true;
        }

        public virtual bool addRequestToBatch(ForceRebindRequest request, RequestTutor tutor, RequestListener listener)
        {
            if (this.batchType != CONTROL)
            {
                log.Error("Unexpected request type was given to batch");
                return false;
            }

            //can we queue force rebind for 2 different sessions? (NO)

            string key = FORCE_REBIND_KEY;

            RequestObjects requestObj = new RequestObjects(request, tutor, listener);

            RequestObjects queuedRequest = null;
            keys.TryGetValue(key, out queuedRequest);
            if (queuedRequest != null)
            {
                queuedRequest.tutor.notifyAbort();
                this.substituteRequest(key, requestObj);
            }
            else
            {
                this.addRequestInternal(key, requestObj);
            }

            return true;
        }

        public virtual bool addRequestToBatch(UnsubscribeRequest request, RequestTutor tutor, RequestListener listener)
        {
            if (this.batchType != CONTROL)
            {
                log.Error("Unexpected request type was given to batch");
                return false;
            }

            string key = request.SubscriptionId.ToString();

            RequestObjects requestObj = new RequestObjects(request, tutor, listener);

            RequestObjects queuedRequest = null;
            keys.TryGetValue(key, out queuedRequest);
            if (queuedRequest != null)
            {
                if (queuedRequest.request is SubscribeRequest)
                { //can't be the first attempt, otherwise the unsubscribe request would not be here

                    log.Debug("Substituting SUBSCRIBE request with UNSUBSCRIBE");
                    queuedRequest.tutor.notifyAbort();
                    this.substituteRequest(key, requestObj);

                }
                else
                {
                    //delete already queued, should not happen, still, we don't have nothing to do
                }
            }
            else
            {
                this.addRequestInternal(key, requestObj);
            }

            return true;
        }

        public virtual bool addRequestToBatch(SubscribeRequest request, RequestTutor tutor, RequestListener listener)
        {
            if (this.batchType != CONTROL)
            {
                log.Error("Unexpected request type was given to batch");
                return false;
            }

            string key = request.SubscriptionId.ToString();

            RequestObjects requestObj = new RequestObjects(request, tutor, listener);

            RequestObjects queuedRequest = null;
            keys.TryGetValue(key, out queuedRequest);
            if (queuedRequest != null)
            {

                //can never happen that an ADD request substitutes a REMOVE request for 2 reasons:
                //  *if those requests are part of the same session than to remove and re-add a table
                //   changes its key.
                //  *if those requests are not part of the same session than during session change
                //   all pending request are removed.
                //so, all cases should pass from the if (requestType == ControlRequest.REMOVE) case

                // thus, this is an unexpected case, let's handle it anyway
                queuedRequest.tutor.notifyAbort();
                this.substituteRequest(key, requestObj);
            }
            else
            {
                this.addRequestInternal(key, requestObj);
            }

            return true;
        }

        public virtual bool addRequestToBatch(ChangeSubscriptionRequest request, RequestTutor tutor, RequestListener listener)
        {
            if (this.batchType != CONTROL)
            {
                log.Error("Unexpected request type was given to batch");
                return false;
            }

            string key = CHANGE_SUB_KEY + request.SubscriptionId;

            RequestObjects requestObj = new RequestObjects(request, tutor, listener);

            RequestObjects queuedRequest = null;
            keys.TryGetValue(key, out queuedRequest);
            if (queuedRequest != null)
            {
                //this change frequency request is newer, replace the old one
                queuedRequest.tutor.notifyAbort();
                this.substituteRequest(key, requestObj);
            }
            else
            {
                this.addRequestInternal(key, requestObj);
            }

            return true;
        }

        public virtual bool addRequestToBatch(DestroyRequest request, RequestTutor tutor, RequestListener listener)
        {
            if (this.batchType != CONTROL)
            {
                log.Error("Unexpected request type was given to batch");
                return false;
            }

            string key = request.Session;

            RequestObjects requestObj = new RequestObjects(request, tutor, listener);

            RequestObjects queuedRequest = null;
            keys.TryGetValue(key, out queuedRequest);

            if (queuedRequest != null)
            {
                log.Debug("Substituting DESTROY request");
                queuedRequest.tutor.notifyAbort();
                this.substituteRequest(key, requestObj);
            }
            else
            {
                this.addRequestInternal(key, requestObj);
            }

            return true;
        }
    }
}