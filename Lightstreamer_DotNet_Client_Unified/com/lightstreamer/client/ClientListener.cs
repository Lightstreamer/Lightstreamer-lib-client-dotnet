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

namespace com.lightstreamer.client
{
    /// <summary>
    /// Interface to be implemented to listen to <seealso cref="LightstreamerClient"/> events comprehending notifications of 
    /// connection activity and errors. <br/>
    /// Events for these listeners are dispatched by a different thread than the one that generates them. 
    /// This means that, upon reception of an event, it is possible that the internal state of the client has changed.
    /// On the other hand, all the notifications for a single LightstreamerClient, including notifications to
    /// <seealso cref="ClientListener"/>s, <seealso cref="SubscriptionListener"/>s and <seealso cref="ClientMessageListener"/>s will be dispatched by the 
    /// same thread.
    /// </summary>
    public interface ClientListener
    {
        /// <summary>
        /// Event handler that receives a notification when the ClientListener instance is removed from a LightstreamerClient 
        /// through <seealso cref="LightstreamerClient.removeListener(ClientListener)"/>. This is the last event to be fired on the listener. </summary>
        /// <param name="client"> the LightstreamerClient this instance was removed from.  </param>
        void onListenEnd(LightstreamerClient client);

        /// <summary>
        /// Event handler that receives a notification when the ClientListener instance is added to a LightstreamerClient 
        /// through <seealso cref="LightstreamerClient.addListener(ClientListener)"/>. This is the first event to be fired on the listener. </summary>
        /// <param name="client"> the LightstreamerClient this instance was added to. </param>
        void onListenStart(LightstreamerClient client);

        /// <summary>
        /// Event handler that is called when the Server notifies a refusal on the client attempt to open
        /// a new connection or the interruption of a streaming connection.
        /// In both cases, the <seealso cref="ClientListener.onStatusChange"/> event handler has already been invoked
        /// with a "DISCONNECTED" status and no recovery attempt has been performed.
        /// By setting a custom handler, however, it is possible to override this and perform custom recovery actions.
        /// </summary>
        /// <param name="errorCode"> The error code. It can be one of the following:
        /// <ul>
        ///   <li>1 - user/password check failed </li>
        ///   <li>2 - requested Adapter Set not available</li>
        ///   <li>7 - licensed maximum number of sessions reached (this can only happen with some licenses)</li>
        ///   <li>8 - configured maximum number of sessions reached</li>
        ///   <li>9 - configured maximum server load reached</li>
        ///   <li>10 - new sessions temporarily blocked</li>
        ///   <li>11 - streaming is not available because of Server license restrictions (this can only happen with special licenses).</li>
        ///   <li>21 - a request for this session has unexpectedly reached a wrong Server instance, which suggests that a routing issue may be in place.</li>
        ///   <li>30-41 - the current connection or the whole session has been closed by external agents; the possible cause may be:
        ///     <ul>
        ///       <li>The session was closed by the administrator, through JMX (32) or through a "destroy" request (31);</li>
        ///       <li>The Metadata Adapter imposes limits on the overall open sessions for the current user and has requested 
        ///           the closure of the current session upon opening of a new session for the same user on a different browser 
        ///           window (35);</li>
        ///       <li>An unexpected error occurred on the Server while the session was in activity (33, 34);</li>
        ///       <li>An unknown or unexpected cause; any code different from the ones identified in the above cases could be 
        ///           issued. A detailed description for the specific cause is currently not supplied (i.e. errorMessage is 
        ///           null in this case).</li>
        ///   </ul></li>
        ///   <li>60 - this version of the client is not allowed by the current license terms.</li>
        ///   <li>61 - there was an error in the parsing of the server response thus the client cannot continue with the current session.</li>
        ///   <li>66 - an unexpected exception was thrown by the Metadata Adapter while authorizing the connection.</li>
        ///   <li>68 - the Server could not open or continue with the session because of an internal error.</li>
        ///   <li>71 - this kind of client is not allowed by the current license terms.</li>
        ///   <li>&lt;= 0 - the Metadata Adapter has refused the user connection; the code value is dependent on the specific 
        ///       Metadata Adapter implementation</li>
        /// </ul>
        /// </param>
        /// <param name="errorMessage"> The description of the error as sent by the Server.
        /// </param>
        /// <seealso cref="ClientListener.onStatusChange" />
        /// <seealso cref="ConnectionDetails.AdapterSet" />
        void onServerError(int errorCode, string errorMessage);

        /// <summary>
        /// Event handler that receives a notification each time the LightstreamerClient status has changed. The status changes 
        /// may be originated either by custom actions (e.g. by calling <seealso cref="LightstreamerClient.disconnect"/>) or by internal 
        /// actions.
        /// The normal cases are the following:
        /// <ul>
        ///   <li>After issuing connect() when the current status is "DISCONNECTED*", the client will switch to "CONNECTING" 
        ///       first and to "CONNECTED:STREAM-SENSING" as soon as the pre-flight request receives its answer.<br/> 
        ///       As soon as the new session is established, it will switch to "CONNECTED:WS-STREAMING" if the environment 
        ///       permits WebSockets; otherwise it will switch to "CONNECTED:HTTP-STREAMING" if the environment permits streaming 
        ///       or to "CONNECTED:HTTP-POLLING" as a last resort.</li>
        ///   <li>On the other hand, after issuing connect when the status is already "CONNECTED:*" a switch to "CONNECTING"
        ///       is usually not needed and the current session is kept.</li>
        ///   <li>After issuing <seealso cref="LightstreamerClient.disconnect"/>, the status will switch to "DISCONNECTED".</li>
        ///   <li>In case of a server connection refusal, the status may switch from "CONNECTING" directly to "DISCONNECTED". 
        ///       After that, the <seealso cref="ClientListener.onServerError"/> event handler will be invoked.</li>
        /// </ul>
        /// Possible special cases are the following:
        /// <ul>
        ///   <li>In case of Server unavailability during streaming, the status may switch from "CONNECTED:*-STREAMING" 
        ///       to "STALLED" (see <seealso cref="ConnectionOptions.StalledTimeout"/>). If the unavailability ceases, the status 
        ///       will switch back to "CONNECTED:*-STREAMING"; otherwise, if the unavailability persists 
        ///       (see <seealso cref="ConnectionOptions.ReconnectTimeout"/>), the status will switch to "DISCONNECTED:TRYING-RECOVERY"
        ///       and eventually to "CONNECTED:*-STREAMING".</li>
        ///   <li>In case the connection or the whole session is forcibly closed by the Server, the status may switch 
        ///       from "CONNECTED:*-STREAMING" or "CONNECTED:*-POLLING" directly to "DISCONNECTED". After that, 
        ///       the <seealso cref="ClientListener.onServerError"/> event handler will be invoked.</li>
        ///   <li>Depending on the setting in <seealso cref="ConnectionOptions.SlowingEnabled"/>, in case of slow update processing, 
        ///       the status may switch from "CONNECTED:WS-STREAMING" to "CONNECTED:WS-POLLING" or from "CONNECTED:HTTP-STREAMING" 
        ///       to "CONNECTED:HTTP-POLLING".</li>
        ///   <li>If the status is "CONNECTED:*-POLLING" and any problem during an intermediate poll occurs, the status may 
        ///       switch to "CONNECTING" and eventually to "CONNECTED:*-POLLING". The same may hold for the "CONNECTED:*-STREAMING" case, 
        ///       when a rebind is needed.</li>
        ///   <li>In case a forced transport was set through <seealso cref="ConnectionOptions.ForcedTransport"/>, only the 
        ///       related final status or statuses are possible.</li>
        ///   <li>In case of connection problems, the status may switch from any value
        ///       to "DISCONNECTED:WILL-RETRY" (see <seealso cref="ConnectionOptions.RetryDelay"/>),
        ///       then to "CONNECTING" and a new attempt will start.
        ///       However, in most cases, the client will try to recover the current session;
        ///       hence, the "DISCONNECTED:TRYING-RECOVERY" status will be used and the recovery attempt will start.</li>
        ///   <li>In case of connection problems during a recovery attempt, the status may stay
        ///       in "DISCONNECTED:TRYING-RECOVERY" for long time, while further attempts are made.
        ///       If the recovery is no longer possible, the current session will be abandoned
        ///       and the status will switch to "DISCONNECTED:WILL-RETRY" before the next attempts.</li>
        /// </ul>
        /// By setting a custom handler it is possible to perform actions related to connection and disconnection occurrences. 
        /// Note that <seealso cref="LightstreamerClient.connect"/> and <seealso cref="LightstreamerClient.disconnect"/>, as any other method, can 
        /// be issued directly from within a handler.
        /// </summary>
        /// <param name="status"> The new status. It can be one of the following values:
        /// <ul>
        ///   <li>"CONNECTING" the client has started a connection attempt and is waiting for a Server answer.</li>
        ///   <li>"CONNECTED:STREAM-SENSING" the client received a first response from the server and is now evaluating if 
        ///   a streaming connection is fully functional.</li>
        ///   <li>"CONNECTED:WS-STREAMING" a streaming connection over WebSocket has been established.</li>
        ///   <li>"CONNECTED:HTTP-STREAMING" a streaming connection over HTTP has been established.</li>
        ///   <li>"CONNECTED:WS-POLLING" a polling connection over WebSocket has been started. Note that, unlike polling over 
        ///   HTTP, in this case only one connection is actually opened (see <seealso cref="ConnectionOptions.SlowingEnabled"/>).</li>
        ///   <li>"CONNECTED:HTTP-POLLING" a polling connection over HTTP has been started.</li>
        ///   <li>"STALLED" a streaming session has been silent for a while, the status will eventually return to its previous 
        ///   CONNECTED:*-STREAMING status or will switch to "DISCONNECTED:WILL-RETRY".</li>
        ///   <li>"DISCONNECTED:WILL-RETRY" a connection or connection attempt has been closed; a new attempt will be 
        ///   performed after a timeout.</li>
        ///   <li>"DISCONNECTED:TRYING-RECOVERY" a connection has been closed and
        ///   the client has started a connection attempt and is waiting for a Server answer;
        ///   if successful, the underlying session will be kept.</li>
        ///   <li>"DISCONNECTED" a connection or connection attempt has been closed. The client will not connect anymore until 
        ///   a new <seealso cref="LightstreamerClient.connect"/> call is issued.</li>
        /// </ul>
        /// </param>
        /// <seealso cref="LightstreamerClient.connect" />
        /// <seealso cref="LightstreamerClient.disconnect" />
        /// <seealso cref="LightstreamerClient.Status" />
        void onStatusChange(string status);

        /// <summary>
        /// Event handler that receives a notification each time  the value of a property of 
        /// <seealso cref="LightstreamerClient.connectionDetails"/> or <seealso cref="LightstreamerClient.connectionOptions"/> 
        /// is changed. <br/>
        /// Properties of these objects can be modified by direct calls to them or
        /// by server sent events.
        /// </summary>
        /// <param name="property"> the name of the changed property.
        /// <br/>Possible values are:
        /// <ul>
        /// <li>adapterSet</li>
        /// <li>serverAddress</li>
        /// <li>user</li>
        /// <li>password</li>
        /// <li>serverInstanceAddress</li>
        /// <li>serverSocketName</li>
        /// <li>clientIp</li>
        /// <li>sessionId</li>
        /// <li>contentLength</li>
        /// <li>idleTimeout</li>
        /// <li>keepaliveInterval</li>
        /// <li>requestedMaxBandwidth</li>
        /// <li>realMaxBandwidth</li>
        /// <li>pollingInterval</li>
        /// <li>reconnectTimeout</li>
        /// <li>stalledTimeout</li>
        /// <li>connectTimeout</li>
        /// <li>currentConnectTimeout</li>
        /// <li>retryDelay</li>
        /// <li>firstRetryMaxDelay</li>
        /// <li>slowingEnabled</li>
        /// <li>forcedTransport</li>
        /// <li>serverInstanceAddressIgnored</li>
        /// <li>reverseHeartbeatInterval</li>
        /// <li>earlyWSOpenEnabled</li>
        /// <li>httpExtraHeaders</li>
        /// <li>httpExtraHeadersOnSessionCreationOnly</li>
        /// 
        /// </ul>
        /// </param>
        /// <seealso cref="LightstreamerClient.connectionDetails" />
        /// <seealso cref="LightstreamerClient.connectionOptions" />
        void onPropertyChange(string property);
    }
}