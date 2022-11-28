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

using com.lightstreamer.client;
using Lightstreamer.DotNet.Logging.Log;

namespace com.lightstreamer.util.mdc
{
    /// <summary>
    /// The Mapped Diagnostic Context stores the context of the application and makes it available to the configured loggers.
    /// It must be manually enabled by suppling the system property "com.lightstreamer.logging.mdc". 
    /// <para>
    /// <b>NB 1</b>
    /// The current implementation relays on Log4J facilities so it is available only if the logger provider 
    /// (set by <seealso cref="LogManager#setLoggerProvider(com.lightstreamer.log.LoggerProvider)"/>) is {@code Log4jWrapper}.
    /// <br>
    /// <b>NB 2</b>
    /// Since a MDC provider is not mandatory, it is better to guard each method call with the check <seealso cref="#isEnabled()"/>.
    /// </para>
    /// </summary>
    public class MDC
    {
        private static readonly ILogger log = LogManager.GetLogger(Constants.UTILS_LOG);

        private static readonly MDCProvider provider;

        static MDC()
        {
            //
        }

        public static bool Enabled
        {
            get
            {
                return provider != null;
            }
        }

        public static void put(string key, string value)
        {
            provider.put(key, value);
        }

        public static string get(string key)
        {
            return provider.get(key);
        }

        public static void remove(string key)
        {
            provider.remove(key);
        }

        public static void clear()
        {
            provider.clear();
        }
    }
}