/*
 * Copyright (c) 2004-2019 Lightstreamer s.r.l., Via Campanini, 6 - 20124 Milano, Italy.
 * All rights reserved.
 * www.lightstreamer.com
 *
 * This software is the confidential and proprietary information of
 * Lightstreamer s.r.l.
 * You shall not disclose such Confidential Information and shall use it
 * only in accordance with the terms of the license agreement you entered
 * into with Lightstreamer s.r.l.
 */
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