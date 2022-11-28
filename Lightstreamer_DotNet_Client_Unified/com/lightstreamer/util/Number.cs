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

using System.Text.RegularExpressions;

namespace com.lightstreamer.util
{
    public class Number
    {
        public const bool ACCEPT_ZERO = true;
        public const bool DONT_ACCEPT_ZERO = false;
        public static void verifyPositive(double num, bool zeroAccepted)
        {
            bool positive = isPositive(num, zeroAccepted);
            if (!positive)
            {
                if (zeroAccepted)
                {
                    throw new System.ArgumentException("The given value is not valid. Use a positive number or 0");
                }
                else
                {
                    throw new System.ArgumentException("The given value is not valid. Use a positive number");
                }
            }
        }
        public static bool isPositive(double num, bool zeroAccepted)
        {
            if (zeroAccepted)
            {
                if (num < 0)
                {
                    return false;
                }
            }
            else if (num <= 0)
            {
                return false;
            }
            return true;
        }

        private static readonly Regex pattern = new Regex("^[+-]?\\d*\\.?\\d+$");
        public static bool isNumber(string num)
        {
            return pattern.Match(num).Success;
        }
    }
}