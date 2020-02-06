using DotNetty.Buffers;
using Lightstreamer.DotNet.Logging.Log;
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
                        }
                        else
                        {
                            // case C)
                            hasHead = false;
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
                    string line = linePart.toLine();
                    networkListener.onMessage(line);

                    log.Debug(" :.: " + networkListener.GetType() + " - " + line);

                    startIndex = eolIndex + 1;
                    eolIndex = findEol(buf, startIndex, endIndex);
                    linePart.Reset();
                }
                /* body */
                while (eolIndex != -1)
                {
                    string line = byteBufToString(buf, startIndex, eolIndex - 1); // exclude CR LF chars
                    networkListener.onMessage(line);
                    if (log.IsDebugEnabled)
                    {
                        log.Debug(" .:. " + networkListener.GetType() + " - " + line);
                    }

                    startIndex = eolIndex + 1;
                    eolIndex = findEol(buf, startIndex, endIndex);
                }
                /* tail */
                bool hasTail = startIndex != endIndex;

                if (log.IsDebugEnabled)
                {
                    log.Debug(" .:.: " + hasTail + "(" + ( endIndex - startIndex ) + ")");
                }

                if (hasTail)
                {
                    copyLinePart(buf, startIndex, endIndex);
                }
                else
                {
                    linePart.Reset();

                    log.Debug("linePart: " + linePart.Size());
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
            if (startIndex >= endIndex)
            {
                return eolIndex;
            }
            int crIndex = buf.IndexOf(startIndex, endIndex, CR);
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
                Debug.Assert(base.Length >= 2);
                byte[] b = base.GetBuffer();
                Debug.Assert(b[base.Length - 2] == '\r' && b[base.Length - 1] == '\n');

                return System.Text.Encoding.Default.GetString(b).Substring(0, (int)base.Length - 2);
            }
        }

    }
}