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

using System.Collections.Generic;

namespace com.lightstreamer.client.session
{

    public interface SubscriptionsListener
    {

        void onSessionStart();
        void onSessionClose();

        void onUpdateReceived(int subscriptionId, int item, List<string> args);

        void onEndOfSnapshotEvent(int subscriptionId, int item);

        void onClearSnapshotEvent(int subscriptionId, int item);

        void onLostUpdatesEvent(int subscriptionId, int item, int lost);

        void onUnsubscription(int subscriptionId);

        void onSubscription(int subscriptionId, int totalItems, int totalFields, int keyPosition, int commandPosition);

        void onSubscription(int subscriptionId, long reconfId);

        void onSubscriptionError(int subscriptionId, int errorCode, string errorMessage);

        void onConfigurationEvent(int subscriptionId, string frequency);

        void onSubscriptionAck(int subscriptionId);

        void onUnsubscriptionAck(int subscriptionId);
    }

}