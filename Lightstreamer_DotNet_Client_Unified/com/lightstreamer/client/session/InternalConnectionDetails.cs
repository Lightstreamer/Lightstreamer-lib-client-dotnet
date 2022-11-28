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

using com.lightstreamer.client.events;
using Lightstreamer.DotNet.Logging.Log;
using System;

namespace com.lightstreamer.client.session
{

    public class InternalConnectionDetails
    {

        private EventDispatcher<ClientListener> eventDispatcher;
        private readonly ILogger log = LogManager.GetLogger(Constants.ACTIONS_LOG);

        private string serverInstanceAddress = null;
        private string serverSocketName = null;
        private string clientIp = null;
        private string password = null;
        private string adapterSet = null;
        private string serverAddress = null;
        private string user = null;
        private string sessionId = null;


        public InternalConnectionDetails(EventDispatcher<ClientListener> eventDispatcher)
        {
            this.eventDispatcher = eventDispatcher;
        }

        public virtual string AdapterSet
        {
            get
            {
                lock (this)
                {
                    return adapterSet;
                }
            }
            set
            {
                lock (this)
                {
                    this.adapterSet = value;

                    this.eventDispatcher.dispatchEvent(new ClientListenerPropertyChangeEvent("adapterSet"));

                    log.Info("Adapter Set value changed to " + value);
                }
            }
        }

        public virtual string ServerAddress
        {
            get
            {
                lock (this)
                {
                    return serverAddress;
                }
            }
            set
            {
                lock (this)
                {
                    if (!value.EndsWith("/", StringComparison.Ordinal))
                    {
                        value += "/";
                    }
                    verifyServerAddress(value); //will throw IllegalArgumentException if not

                    this.serverAddress = value;

                    this.eventDispatcher.dispatchEvent(new ClientListenerPropertyChangeEvent("serverAddress"));

                    log.Info("Server Address value changed to " + value);
                }
            }
        }

        public virtual string User
        {
            get
            {
                lock (this)
                {
                    return user;
                }
            }
            set
            {
                lock (this)
                {
                    this.user = value;

                    this.eventDispatcher.dispatchEvent(new ClientListenerPropertyChangeEvent("user"));

                    log.Info("User value changed to " + value);
                }
            }
        }

        public virtual string ServerInstanceAddress
        {
            get
            {
                lock (this)
                {
                    return serverInstanceAddress;
                }
            }
            set
            {
                lock (this)
                {
                    this.serverInstanceAddress = value;

                    this.eventDispatcher.dispatchEvent(new ClientListenerPropertyChangeEvent("serverInstanceAddress"));

                    log.Info("Server Instance Address value changed to " + value);
                }
            }
        }

        public virtual string ServerSocketName
        {
            get
            {
                lock (this)
                {
                    return serverSocketName;
                }
            }
            set
            {
                lock (this)
                {
                    this.serverSocketName = value;

                    this.eventDispatcher.dispatchEvent(new ClientListenerPropertyChangeEvent("serverSocketName"));

                    log.Info("Server Socket Name value changed to " + value);
                }
            }
        }

        public virtual string ClientIp
        {
            get
            {
                lock (this)
                {
                    return clientIp;
                }
            }
            set
            {
                lock (this)
                {
                    this.clientIp = value;

                    this.eventDispatcher.dispatchEvent(new ClientListenerPropertyChangeEvent("clientIp"));

                    log.Info("Client IP value changed to " + value);
                }
            }
        }

        public virtual string SessionId
        {
            get
            {
                lock (this)
                {
                    return sessionId;
                }
            }
            set
            {
                lock (this)
                {
                    this.sessionId = value;

                    this.eventDispatcher.dispatchEvent(new ClientListenerPropertyChangeEvent("sessionId"));

                    log.Info("Session ID value changed to " + value);
                }
            }
        }

        public virtual string Password
        {
            set
            {
                lock (this)
                {
                    this.password = value;

                    this.eventDispatcher.dispatchEvent(new ClientListenerPropertyChangeEvent("password"));

                    log.Info("Password value changed");
                }
            }
            get
            {
                lock (this)
                {
                    return this.password;
                }
            }
        }

        private static void verifyServerAddress(string serverAddress)
        {

            Uri url;
            try
            {
                url = new Uri(serverAddress);
            }
            catch (Exception e)
            {
                throw new System.ArgumentException("The given server address is not valid", e);
            }

            string protocol = url.Scheme;
            if (!protocol.Equals("http") && !protocol.Equals("https"))
            {
                throw new System.ArgumentException("The given server address has not a valid scheme");
            }


            if (url.Query != null)
            {
                if (!url.Query.Equals(""))
                {
                    throw new System.ArgumentException("The given server address is not valid, remove the query");
                }
            }


            if (url.UserInfo != null)
            {
                if (!url.UserInfo.Equals(""))
                {
                    throw new System.ArgumentException("The given server address is not valid, remove the user info");
                }


            }
        }
    }

}