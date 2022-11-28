﻿#region License
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

namespace com.lightstreamer.client.protocol
{
    public interface ControlRequestHandler
    {
        /// <summary>
        /// Adds a control/message request.
        /// </summary>
        void addRequest(LightstreamerRequest request, RequestTutor tutor, RequestListener reqListener);

        long RequestLimit { set; }

        void copyTo(ControlRequestHandler newHandler);

        void close(bool waitPending);
    }
}