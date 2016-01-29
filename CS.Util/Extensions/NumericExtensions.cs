﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CS.Util.Extensions
{
    public static class NumericExtensions
    {
        /// <summary>
        /// Converts the integer to a textual representation using English words. 
        /// For example, 142.AsPrettyString() is "one hundred and forty-two".
        /// </summary>
        public static string AsPrettyString(this ushort number)
        {
            return ((long)number).AsPrettyString();
        }
        public static string AsPrettyString(this short number)
        {
            return ((long)number).AsPrettyString();
        }
        public static string AsPrettyString(this int number)
        {
            return ((long)number).AsPrettyString();
        }
        public static string AsPrettyString(this uint number)
        {
            return ((long)number).AsPrettyString();
        }
        public static string AsPrettyString(this long number)
        {
            if (number == 0)
                return "zero";

            if (number < 0)
                return "minus " + AsPrettyString(-number);

            string words = "";

            if ((number / 1000000000000000000) > 0)
            {
                words += " " + AsPrettyString(number / 1000000000000000000) + " quintillion";
                number %= 1000000000000000000;
            }

            if ((number / 1000000000000000) > 0)
            {
                words += " " + AsPrettyString(number / 1000000000000000) + " quadrillon";
                number %= 1000000000000000;
            }

            if ((number / 1000000000000) > 0)
            {
                words += " " + AsPrettyString(number / 1000000000000) + " trillion";
                number %= 1000000000000;
            }

            if ((number / 1000000000) > 0)
            {
                words += " " + AsPrettyString(number / 1000000000) + " billion";
                number %= 1000000000;
            }

            if ((number / 1000000) > 0)
            {
                words += " " + AsPrettyString(number / 1000000) + " million";
                number %= 1000000;
            }

            if ((number / 1000) > 0)
            {
                words += " " + AsPrettyString(number / 1000) + " thousand";
                number %= 1000;
            }

            if ((number / 100) > 0)
            {
                words += " " + AsPrettyString(number / 100) + " hundred";
                number %= 100;
            }

            if (number > 0)
            {
                if (words != "")
                    words += " and";

                var unitsMap = new[] { null, "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen" };
                var tensMap = new[] { null, "ten", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety" };

                if (number < 20)
                    words += " " + unitsMap[number];
                else
                {
                    words += " " + tensMap[number / 10];
                    if ((number % 10) > 0)
                        words += "-" + unitsMap[number % 10];
                }
            }

            return words.Substring(1);
        }
        public static string AsPrettyString(this DateTime date)
        {
            return PrettyTime.Format(date);
        }

        public static byte[] GetBytes(this bool value, bool inLittleEndian = true)
        {
            var converter = inLittleEndian ? EndianBitConverter.Little : EndianBitConverter.Big;
            return converter.GetBytes(value);
        }
        public static byte[] GetBytes(this char value, bool inLittleEndian = true)
        {
            var converter = inLittleEndian ? EndianBitConverter.Little : EndianBitConverter.Big;
            return converter.GetBytes(value);
        }
        public static byte[] GetBytes(this short value, bool inLittleEndian = true)
        {
            var converter = inLittleEndian ? EndianBitConverter.Little : EndianBitConverter.Big;
            return converter.GetBytes(value);
        }
        public static byte[] GetBytes(this int value, bool inLittleEndian = true)
        {
            var converter = inLittleEndian ? EndianBitConverter.Little : EndianBitConverter.Big;
            return converter.GetBytes(value);
        }
        public static byte[] GetBytes(this long value, bool inLittleEndian = true)
        {
            var converter = inLittleEndian ? EndianBitConverter.Little : EndianBitConverter.Big;
            return converter.GetBytes(value);
        }
        public static byte[] GetBytes(this ushort value, bool inLittleEndian = true)
        {
            var converter = inLittleEndian ? EndianBitConverter.Little : EndianBitConverter.Big;
            return converter.GetBytes(value);
        }
        public static byte[] GetBytes(this uint value, bool inLittleEndian = true)
        {
            var converter = inLittleEndian ? EndianBitConverter.Little : EndianBitConverter.Big;
            return converter.GetBytes(value);
        }
        public static byte[] GetBytes(this ulong value, bool inLittleEndian = true)
        {
            var converter = inLittleEndian ? EndianBitConverter.Little : EndianBitConverter.Big;
            return converter.GetBytes(value);
        }
        public static byte[] GetBytes(this float value, bool inLittleEndian = true)
        {
            var converter = inLittleEndian ? EndianBitConverter.Little : EndianBitConverter.Big;
            return converter.GetBytes(value);
        }
        public static byte[] GetBytes(this double value, bool inLittleEndian = true)
        {
            var converter = inLittleEndian ? EndianBitConverter.Little : EndianBitConverter.Big;
            return converter.GetBytes(value);
        }
    }
}
