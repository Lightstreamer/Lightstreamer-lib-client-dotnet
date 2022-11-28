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

using com.lightstreamer.util;
using System;

namespace com.lightstreamer.client.protocol
{
    /// <summary>
    /// Parses REQOK/REQERR/ERROR responses to control requests.
    /// </summary>
    public class ControlResponseParser
    {
        /// <summary>
        /// Parses a response to a control request.
        /// </summary>
        public static ControlResponseParser parseControlResponse(string message)
        {
            if (message.StartsWith("REQOK", StringComparison.Ordinal))
            {
                return new REQOKParser(message);

            }
            else if (message.StartsWith("REQERR", StringComparison.Ordinal))
            {
                return new REQERRParser(message);

            }
            else if (message.StartsWith("ERROR", StringComparison.Ordinal))
            {
                return new ERRORParser(message);

            }
            else
            {
                throw new ParsingException("Unexpected response to control request: " + message);
            }
        }

        /// <summary>
        /// Parses REQOK message.
        /// </summary>
        public class REQOKParser : ControlResponseParser
        {
            internal readonly long requestId;

            public REQOKParser(string message)
            {
                // REQOK,<requestId>
                int reqIndex = message.IndexOf(',') + 1;
                if (reqIndex <= 0)
                {
                    // heartbeat REQOKs have no requestId 
                    requestId = -1;
                }
                else
                {
                    requestId = myParseLong(message.Substring(reqIndex), "request field", message);
                }
            }

            public virtual long RequestId
            {
                get
                {
                    if (requestId == -1)
                    {
                        throw new System.InvalidOperationException("Invalid request identifier");
                    }
                    return requestId;
                }
            }
        }

        /// <summary>
        /// Parses REQERR message.
        /// </summary>
        public class REQERRParser : ControlResponseParser
        {
            public readonly long requestId;
            public readonly int errorCode;
            public readonly string errorMsg;

            public REQERRParser(string message)
            {
                // REQERR,<requestId>,<error code>,<error message>
                string[] pieces = message.Trim().Split(',');
                if (pieces.Length != 4)
                {
                    throw new ParsingException("Unexpected response to control request: " + message);
                }
                requestId = myParseLong(pieces[1], "request identifier", message);
                errorCode = myParseInt(pieces[2], "error code", message);
                errorMsg = EncodingUtils.unquote(pieces[3]);
            }
        }

        /// <summary>
        /// Parses ERROR message.
        /// </summary>
        public class ERRORParser : ControlResponseParser
        {
            public readonly int errorCode;
            public readonly string errorMsg;

            public ERRORParser(string message)
            {
                // ERROR,<error code>,<error message>
                string[] pieces = message.Trim().Split(',');
                if (pieces.Length != 3)
                {
                    throw new ParsingException("Unexpected response to control request: " + message);
                }
                errorCode = myParseInt(pieces[1], "error code", message);
                errorMsg = EncodingUtils.unquote(pieces[2]);
            }
        }

        private static long myParseLong(string field, string description, string orig)
        {
            try
            {
                return long.Parse(field);
            }
            catch (System.FormatException)
            {
                throw new ParsingException("Malformed " + description + " in message: " + orig);
            }
        }

        private static int myParseInt(string field, string description, string orig)
        {
            try
            {
                return int.Parse(field);
            }
            catch (System.FormatException)
            {
                throw new ParsingException("Malformed " + description + " in message: " + orig);
            }
        }

        public class ParsingException : Exception
        {
            internal const long serialVersionUID = 1L;

            public ParsingException(string @string) : base(@string)
            {
            }
        }
    }
}