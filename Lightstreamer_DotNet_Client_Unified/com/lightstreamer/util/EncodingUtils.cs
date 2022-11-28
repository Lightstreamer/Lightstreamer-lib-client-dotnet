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

using System;
using System.Diagnostics;
using System.Text;

namespace com.lightstreamer.util
{
    public class EncodingUtils
    {

        /// <summary>
        /// Converts a string containing sequences as {@code %<hex digit><hex digit>} into a new string 
        /// where such sequences are transformed in UTF-8 encoded characters. <br> 
        /// For example the string "a%C3%A8" is converted to "aè" because the sequence 'C3 A8' is 
        /// the UTF-8 encoding of the character 'è'.
        /// </summary>
        public static string unquote(string s)
        {
            Debug.Assert(!string.ReferenceEquals(s, null));
            try
            {
                // to save space and time the input byte sequence is also used to store the converted byte sequence.
                // this is possible because the length of the converted sequence is equal to or shorter than the original one.
                sbyte[] bb = s.GetBytes(Encoding.UTF8);
                int i = 0, j = 0;
                while (i < bb.Length)
                {
                    Debug.Assert(i >= j);
                    if (bb[i] == (sbyte)'%')
                    {
                        int firstHexDigit = hexToNum(bb[i + 1]);
                        int secondHexDigit = hexToNum(bb[i + 2]);
                        bb[j++] = (sbyte)( ( firstHexDigit << 4 ) + secondHexDigit ); // i.e (firstHexDigit * 16) + secondHexDigit
                        i += 3;

                    }
                    else
                    {
                        bb[j++] = bb[i++];
                    }
                }
                // j contains the length of the converted string
                string ss = StringHelper.NewString(bb, 0, j, "UTF-8");
                return ss;

            }
            catch (Exception e)
            {
                throw new System.InvalidOperationException(e.Message); // should not happen
            }
        }

        /// <summary>
        /// Converts an ASCII-encoded hex digit in its numeric value.
        /// </summary>
        private static int hexToNum(int ascii)
        {
            Debug.Assert("0123456789abcdefABCDEF".IndexOf(char.ConvertFromUtf32(ascii)) != -1); // ascii is a hex digit
            int hex;
            // NB ascii characters '0', 'A', 'a' have codes 30, 41 and 61
            if (( hex = ascii - 'a' + 10 ) > 9)
            {
                // NB (ascii - 'a' + 10 > 9) <=> (ascii >= 'a')
                // and thus ascii is in the range 'a'..'f' because
                // '0' and 'A' have codes smaller than 'a'
                Debug.Assert('a' <= ascii && ascii <= 'f');
                Debug.Assert(10 <= hex && hex <= 15);

            }
            else if (( hex = ascii - 'A' + 10 ) > 9)
            {
                // NB (ascii - 'A' + 10 > 9) <=> (ascii >= 'A')
                // and thus ascii is in the range 'A'..'F' because
                // '0' has a code smaller than 'A' 
                // and the range 'a'..'f' is excluded
                Debug.Assert('A' <= ascii && ascii <= 'F');
                Debug.Assert(10 <= hex && hex <= 15);

            }
            else
            {
                // NB ascii is in the range '0'..'9'
                // because the ranges 'a'..'f' and 'A'..'F' are excluded
                hex = ascii - '0';
                Debug.Assert('0' <= ascii && ascii <= '9');
                Debug.Assert(0 <= hex && hex <= 9);
            }
            Debug.Assert(0 <= hex && hex <= 15);
            return hex;
        }
    }
}