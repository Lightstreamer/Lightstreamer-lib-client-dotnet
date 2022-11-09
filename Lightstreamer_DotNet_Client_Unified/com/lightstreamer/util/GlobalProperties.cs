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

namespace com.lightstreamer.util
{
    /// <summary>
    /// Singleton class storing global properties affecting the behavior of the library.
    /// </summary>
    public class GlobalProperties
    {
        public static readonly GlobalProperties INSTANCE = new GlobalProperties();

        private System.Net.Security.RemoteCertificateValidationCallback trustManagerFactory;

        private GlobalProperties()
        {
        }

        public virtual System.Net.Security.RemoteCertificateValidationCallback TrustManagerFactory
        {
            get
            {
                lock (this)
                {
                    return trustManagerFactory;
                }
            }
            set
            {
                lock (this)
                {
                    this.trustManagerFactory = value;
                }
            }
        }
    }
}