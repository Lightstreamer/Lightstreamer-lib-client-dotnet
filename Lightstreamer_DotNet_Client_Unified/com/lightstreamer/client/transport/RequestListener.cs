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

namespace com.lightstreamer.client.transport
{
    using InternalConnectionOptions = com.lightstreamer.client.session.InternalConnectionOptions;


    /// <summary>
    /// Interface to be called by <seealso cref="Transport"/> implementations in order to communicate with lower layers.
    /// There are no constraint on the thread that needs to call these methods (e.g. it is completely fine to use the
    /// same thread that actually reads from the socket) as the implementation of this interface will take care of any
    /// thread-switching requirement.
    /// The documentation of this Interface addresses how to call the interface methods on an instance of an implementing
    /// class rather than documenting how to implement the interface itself (how to implement the interface can be 
    /// inferred though).
    /// </summary>
    public interface RequestListener
    {

        /// <summary>
        /// Event to be called to notify of new data on the connection. The request HTTP status and all the headers in general
        /// must not be sent through here: only the actual payload has to be notified. 
        /// This method is safe to be called with partial data, assembling and interpretation is performed on lower levels. 
        /// <BR>
        /// NOTE: If the associated socket closure was requested via the <seealso cref="RequestHandle#close()"/> method before 
        /// this event was fired, it is not necessary to fire this event at all. 
        /// </summary>
        /// <param name="message"> a piece of the response contents (placing the full response content in a single message is also fine). </param>
        void onMessage(string message);

        /// <summary>
        /// Event to be called as soon as the socket was opened, and before the request is written on the net.
        /// If the used APIs do not permit to separate "socket open" and "write request" in two different steps, 
        /// then this method must be called before both operations; in that case, if the socket fails to open
        /// simply continue by calling the <seealso cref="#onBroken"/> method.
        /// <BR>
        /// NOTE: If the associated socket closure was requested via the <seealso cref="RequestHandle#close()"/> method before 
        /// this event was fired, it is not necessary to fire this event at all. 
        /// </summary>
        void onOpen();

        /// <summary>
        /// Method to be called at the natural end of the connection after all the received data has been forwarded
        /// through the <seealso cref="#onMessage(String)"/> event. It is also legal and, although not mandatory, somewhat suggested,
        /// to call this method after an <seealso cref="#onBroken"/> event has been fired.   
        /// No other event must be fired after this one.
        /// <BR>
        /// NOTE: If the associated socket closure was requested via the <seealso cref="RequestHandle#close()"/> method before 
        /// this event was fired, it is not necessary to fire this event at all. 
        /// </summary>
        void onClosed();

        /// <summary>
        /// Method to be called if one of the following conditions arises:
        /// <ul>
        /// <li>The socket is abruptly closed while the full response was not yet received</li>
        /// <li>The received HTTP status is in the 100, 400 or 500 families </li>
        /// <li>The received HTTP status is in the 300 family and it is not possible to follow the redirection</li>
        /// <li>It wasn't possible to open the socket to the server (no need to call <seealso cref="#onOpen"/> beforehand)</li>
        /// <li>It wasn't possible to write on the socket (i.e. to send the request)</li>
        /// <li>An internal timeout on the connection opening/read is reached; NOTE: if possible use the read timeout and 
        /// connect timeout as set in the <seealso cref="InternalConnectionOptions#getTCPConnectTimeout()"/> and 
        /// <seealso cref="InternalConnectionOptions#getTCPReadTimeout()"/>. These timeouts are provided through the 
        /// <seealso cref="Transport#sendRequest(Protocol, com.lightstreamer.client.requests.LightstreamerRequest, RequestListener, java.util.Map, com.lightstreamer.client.Proxy, long, long)"/>
        /// method</li> 
        /// </li>
        /// </ul>
        /// <BR>
        /// NOTE: If the associated socket closure was requested via the <seealso cref="RequestHandle#close()"/> method before 
        /// this event was fired, it is not necessary to fire this event at all. 
        /// </summary>
        void onBroken();
    }
}