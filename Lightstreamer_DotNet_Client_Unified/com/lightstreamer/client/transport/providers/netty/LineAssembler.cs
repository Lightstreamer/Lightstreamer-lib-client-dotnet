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

using DotNetty.Buffers;
using Lightstreamer.DotNet.Logging.Log;
using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace com.lightstreamer.client.transport.providers.netty
{

    /// <summary>
    /// Extracts the lines from a byte frame.
    /// </summary>
    public class LineAssembler
    {

        private static readonly ILogger log = LogManager.GetLogger(Constants.TRANSPORT_LOG);

        /* Note:
		 * this class is synchronized because the HTTP facility may call the constructor and 
		 * the method readBytes() in different threads.
		 * On the contrary the WebSocket facility calls both methods in the same thread.
		 */

        private readonly RequestListener networkListener;
        private readonly PeekableByteArrayOutputStream linePart;

        private const byte LF = (byte)'\n';
        private const byte CR = (byte)'\r';

        public LineAssembler(RequestListener networkListener)
        {
            //        assert (this.owner = Thread.currentThread()) != null;
            Debug.Assert(networkListener != null);
            this.networkListener = networkListener;
            linePart = new PeekableByteArrayOutputStream();
        }

        /// <summary>
        /// Reads the available bytes and extracts the contained lines. 
        /// For each line found the method <seealso cref="RequestListener#onMessage(String)"/> is notified.
        /// </summary>
        public virtual void readBytes(IByteBuffer buf)
        {
            lock (this)
            {
                //        assert this.owner == Thread.currentThread();
                /*
				 * A frame has the following structure:
				 * <frame> ::= <head><body><tail>
				 * 
				 * The head of a frame (if present) is the rest of a line started in a previous frame.
				 * <head> ::= <rest-previous-line>?
				 * <rest-previous-line> ::= <line-part><LF> 
				 * NB line-part can be empty. In that case the char CR is in the previous frame.
				 * 
				 * The body consists of a sequence of whole lines.
				 * <body> ::= <line>*
				 * <line> ::= <line-body><EOL>
				 * 
				 * The tail of a frame (if present) is a line lacking the EOL terminator (NB it can span more than one frame).
				 * <tail> ::= <line-part>?
				 * 
				 * EOL is the sequence \r\n.
				 * <EOL> ::= <CR><LF>
				 * 
				 */
                /*
				 * Note: 
				 * startIndex and eolIndex are the most important variables (and the only non-final)
				 * and they must be updated together since they represents the next part of frame to elaborate. 
				 */

                log.Debug(" Readeable bytes: " + buf.ReadableBytes);
                log.Debug(" Start: " + buf.ReaderIndex);

                int endIndex = buf.ReaderIndex + buf.ReadableBytes; // ending index of the byte buffer (exclusive)
                int startIndex = buf.ReaderIndex; // starting index of the current line/part of line (inclusive)
                int eolIndex; // ending index of the current line/part of line (inclusive) (it points to EOL)
                if (startIndex >= endIndex)
                {
                    return; // byte buffer is empty: nothing to do
                }
                /* head */
                bool hasHead;
                bool prevLineIsIncomplete = linePart.Size() != 0;

                if (prevLineIsIncomplete)
                {
                    /* 
					 * Since the previous line is incomplete (it lacks the line terminator), 
					 * is the rest of the line in this frame?
					 * We have three cases:
					 * A) the char CR is in the previous frame and the char LF is in this one;
					 * B) the chars CR and LF are in this frame;
					 * C) the sequence CR LF is not in this frame (maybe there is CR but not LF).
					 * 
					 * If case A) or B) holds, the next part to compute is <head> (see grammar above).
					 * In case C) we must compute <tail>.
					 */
                    if (linePart.PeekAtLastByte() == CR && buf.GetByte(startIndex) == LF)
                    {
                        // case A) EOL is across the previous and the current frame
                        hasHead = true;
                        eolIndex = startIndex;
                    }
                    else
                    {
                        eolIndex = findEol(buf, startIndex, endIndex);
                        if (eolIndex != -1)
                        {
                            // case B)
                            hasHead = true;
                            log.Debug("prev line incomplete case B: " + eolIndex);
                        }
                        else
                        {
                            // case C)
                            hasHead = false;
                            log.Debug("prev line incomplete case C: " + eolIndex);
                        }
                    }

                }
                else
                {
                    /* 
					 * The previous line is complete.
					 * We must consider two cases:
					 * D) the sequence CR LF is in this frame;
					 * E) the sequence CR LF is not in this frame (maybe there is CR but not LF).
					 * 
					 * If case D) holds, the next part to compute is <body>.
					 * If case E) holds, the next part is <tail>.
					 */
                    hasHead = false;
                    eolIndex = findEol(buf, startIndex, endIndex);
                }

                if (hasHead)
                {
                    copyLinePart(buf, startIndex, eolIndex + 1);

                    try
                    {
                        string line = linePart.toLine();
                        networkListener.onMessage(line);
                    } catch (Exception e)
                    {
                        log.Warn("Error in retrieving a message: " + e.Message);
                        log.Debug(e + " - " + e.StackTrace);
                    }

                    startIndex = eolIndex + 1;
                    eolIndex = findEol(buf, startIndex, endIndex);
                    linePart.Reset();
                }
                /* body */
                while (eolIndex != -1)
                {
                    string line = byteBufToString(buf, startIndex, eolIndex - 1); // exclude CR LF chars
                    networkListener.onMessage(line);

                    startIndex = eolIndex + 1;
                    eolIndex = findEol(buf, startIndex, endIndex);
                }
                /* tail */
                bool hasTail = startIndex != endIndex;

                if (hasTail)
                {
                    copyLinePart(buf, startIndex, endIndex);
                }
                else
                {
                    linePart.Reset();
                }
            }
        }

        /// <summary>
        /// Finds the index of a CR LF sequence (EOL). The index points to LF.
        /// Returns -1 if there is no EOL. </summary>
        /// <param name="startIndex"> starting index (inclusive) </param>
        /// <param name="endIndex"> ending index (exclusive) </param>
        private int findEol(IByteBuffer buf, int startIndex, int endIndex)
        {
            int eolIndex = -1;

            // log.Debug("findEol - buf:" + buf.GetString(startIndex, endIndex, Encoding.UTF8));

            try
            {
                IByteBuffer debuffer = buf.Copy();
            } catch (Exception ex)
            {
                log.Debug("Error in read buffer: " + ex.Message);
            }
            

            if (startIndex >= endIndex)
            {
                return eolIndex;
            }
            int crIndex = buf.IndexOf(startIndex, endIndex, CR);

            if (crIndex < 0)
            {
                log.Debug("No CR.");
            }


            if (crIndex != -1 && crIndex != endIndex - 1 && buf.GetByte(crIndex + 1) == LF)
            {
                eolIndex = crIndex + 1;
            }

            return eolIndex;
        }

        /// <summary>
        /// Copies a slice of a frame representing a part of a bigger string in a temporary buffer to be reassembled. </summary>
        /// <param name="startIndex"> starting index (inclusive) </param>
        /// <param name="endIndex"> ending index (exclusive) </param>
        private void copyLinePart(IByteBuffer buf, int startIndex, int endIndex)
        {
            try
            {
                buf.GetBytes(startIndex, linePart, endIndex - startIndex);
            }
            catch (IOException e)
            {
                log.Error("Unexpected exception", e); // should not happen
            }
        }

        /// <summary>
        /// Converts a line to a UTF-8 string. </summary>
        /// <param name="startIndex"> starting index (inclusive) </param>
        /// <param name="endIndex"> ending index (exclusive) </param>
        private string byteBufToString(IByteBuffer buf, int startIndex, int endIndex)
        {
            return buf.ToString(startIndex, endIndex - startIndex, Encoding.UTF8);
        }

        private class PeekableByteArrayOutputStream : MemoryStream
        {


            internal PeekableByteArrayOutputStream() : base(1024)
            {
            }

            /// <summary>
            /// Returns the last byte written.
            /// </summary>
            internal virtual byte PeekAtLastByte()
            {
                Debug.Assert(base.Length > 0);
                byte[] b = base.GetBuffer();
                return b[base.Length - 1];
            }

            internal virtual long Size()
            {
                return base.Length;
            }

            internal virtual void Reset()
            {
                base.SetLength(0);
            }

            /// <summary>
            /// Converts the bytes in a UTF-8 string. The last two bytes (which are always '\r' '\n') are excluded.
            /// </summary>
            internal virtual string toLine()
            {
                int end_b = (int)base.Length;

                Debug.Assert(base.Length >= 2);
                byte[] b = base.GetBuffer();

                Debug.Assert(b[base.Length - 2] == '\r' && b[base.Length - 1] == '\n');

                string temp_s = System.Text.Encoding.UTF8.GetString(b, 0, end_b);

                char[] trailers = new char[2];
                trailers[0] = '\n';
                trailers[1] = '\r';
                
                temp_s = temp_s.TrimEnd(trailers);

                return temp_s;

                /*
                String stemp = System.Text.Encoding.UTF8.GetString(b);

                log.Debug("toLine s: " + stemp.Length);

                return stemp.Substring(0, (int)end_b - 2);
                */
            }
        }

    }
}