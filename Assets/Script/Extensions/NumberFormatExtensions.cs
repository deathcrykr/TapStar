using System.Numerics;
using UnityEngine;

namespace TapStar.Extensions
{
    public static class NumberFormatExtensions
    {
        // 단위 배열 (20개 지원)
        private static readonly (BigInteger Value, string Unit)[] Units = new[]
        {
            (BigInteger.Parse("1000000000000000000000000000000000000000000000000000000000000"), "Nd"),  // Novemdecillion
            (BigInteger.Parse("1000000000000000000000000000000000000000000000000000000000"), "Od"),     // Octodecillion
            (BigInteger.Parse("1000000000000000000000000000000000000000000000000000000"), "Spd"),      // Septendecillion
            (BigInteger.Parse("1000000000000000000000000000000000000000000000000000"), "Sxd"),        // Sexdecillion
            (BigInteger.Parse("1000000000000000000000000000000000000000000000000"), "Qid"),           // Quindecillion
            (BigInteger.Parse("1000000000000000000000000000000000000000000000"), "Qad"),              // Quattuordecillion
            (BigInteger.Parse("1000000000000000000000000000000000000000000"), "Td"),                 // Tredecillion
            (BigInteger.Parse("1000000000000000000000000000000000000000"), "Dd"),                    // Duodecillion
            (BigInteger.Parse("1000000000000000000000000000000000000"), "Ud"),                       // Undecillion
            (BigInteger.Parse("1000000000000000000000000000000000"), "Dc"),                          // Decillion
            (BigInteger.Parse("1000000000000000000000000000000"), "No"),                             // Nonillion
            (BigInteger.Parse("1000000000000000000000000000"), "Oc"),                                // Octillion
            (BigInteger.Parse("1000000000000000000000000"), "Sp"),                                   // Septillion
            (BigInteger.Parse("1000000000000000000000"), "Sx"),                                      // Sextillion
            (BigInteger.Parse("1000000000000000000"), "Qi"),                                         // Quintillion
            (BigInteger.Parse("1000000000000000"), "Qa"),                                            // Quadrillion
            (BigInteger.Parse("1000000000000"), "T"),                                                // Trillion
            (BigInteger.Parse("1000000000"), "B"),                                                   // Billion
            (BigInteger.Parse("1000000"), "M"),                                                      // Million
            (BigInteger.Parse("1000"), "K")                                                          // Thousand
        };

        /// <summary>
        /// BigInteger 형식의 문자열을 K, M, B, T 등의 단위로 포맷팅합니다.
        /// </summary>
        /// <param name="numberString">포맷팅할 숫자 문자열</param>
        /// <param name="decimalPlaces">소수점 자리수 (0-3, 기본값: 2)</param>
        public static string FormatBigInt(this string numberString, int decimalPlaces = 2)
        {
            if (string.IsNullOrEmpty(numberString)) return "0";

            if (!BigInteger.TryParse(numberString, out BigInteger number))
                return "0";

            return FormatBigInteger(number, decimalPlaces);
        }

        /// <summary>
        /// int 값을 K, M, B, T 등의 단위로 포맷팅합니다.
        /// </summary>
        public static string FormatBigInt(this int value, int decimalPlaces = 2)
        {
            return FormatBigInteger(new BigInteger(value), decimalPlaces);
        }

        /// <summary>
        /// long 값을 K, M, B, T 등의 단위로 포맷팅합니다.
        /// </summary>
        public static string FormatBigInt(this long value, int decimalPlaces = 2)
        {
            return FormatBigInteger(new BigInteger(value), decimalPlaces);
        }

        /// <summary>
        /// float 값을 K, M, B, T 등의 단위로 포맷팅합니다.
        /// </summary>
        public static string FormatBigInt(this float value, int decimalPlaces = 2)
        {
            return FormatBigInteger(new BigInteger(value), decimalPlaces);
        }

        /// <summary>
        /// double 값을 K, M, B, T 등의 단위로 포맷팅합니다.
        /// </summary>
        public static string FormatBigInt(this double value, int decimalPlaces = 2)
        {
            return FormatBigInteger(new BigInteger(value), decimalPlaces);
        }

        private static string FormatBigInteger(BigInteger number, int decimalPlaces)
        {
            // 소수점 자리수 제한 (0-3)
            decimalPlaces = Mathf.Clamp(decimalPlaces, 0, 3);

            // 음수 처리
            string sign = number < 0 ? "-" : "";
            number = BigInteger.Abs(number);

            // 1000 미만은 그대로 표시
            if (number < 1000)
                return sign + number.ToString();

            // 적절한 단위 찾기
            foreach (var (value, unit) in Units)
            {
                if (number >= value)
                {
                    if (decimalPlaces == 0)
                    {
                        // 소수점 없이 반올림
                        BigInteger rounded = (number + value / 2) / value;
                        return $"{sign}{rounded}{unit}";
                    }

                    // 소수점 계산을 위한 배수
                    BigInteger multiplier = BigInteger.Pow(10, decimalPlaces);

                    // 정수부와 소수부 계산
                    BigInteger quotient = number / value;
                    BigInteger remainder = (number % value) * multiplier / value;

                    // 불필요한 0 제거
                    string decimalPart = remainder.ToString().TrimEnd('0');

                    if (string.IsNullOrEmpty(decimalPart))
                        return $"{sign}{quotient}{unit}";

                    return $"{sign}{quotient}.{decimalPart}{unit}";
                }
            }

            return sign + number.ToString();
        }
    }
}
