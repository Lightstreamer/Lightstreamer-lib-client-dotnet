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

using DotNetty.Common.Utilities;
using System;

namespace com.lightstreamer.client.transport.providers.netty
{

    public class NettyInterruptionHandler : RequestHandle
    {

        private bool interrupted = false;
        public AtomicReference<IDisposable> connectionRef = new AtomicReference<IDisposable>(); // written by Netty thread but read by Session thread

        public virtual void close(bool forceConnectionClose)
        {
            this.interrupted = true;
            if (forceConnectionClose)
            {
                IDisposable ch = (IDisposable)connectionRef.Value;
                if (ch != null)
                {
                    try
                    {
                        ch.Dispose();

                    }
                    catch (Exception e)
                    {
                        // ignore
                    }
                }
            }
        }


        internal virtual bool Interrupted
        {
            get
            {
                return this.interrupted;
            }
        }

    }
}