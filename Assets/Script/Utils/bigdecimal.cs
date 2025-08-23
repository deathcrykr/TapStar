// BigDecimal.cs
// Unity 2021+ / .NET Standard 2.1 이상에서 동작. (System.Numerics 필요)
// 임의정밀 소수: BigInteger 기반 10진 스케일 방식

using System;
using System.Globalization;
using System.Numerics;
using System.Text;
using UnityEngine;
#if UNITY_UGUI
using UnityEngine.UI;
#endif

namespace LargeNumbers
{
    [Serializable]
    public struct BigDecimal : IComparable<BigDecimal>, IEquatable<BigDecimal>
    {
        private const int DefaultPrecision = 50;

        public BigInteger Mantissa { get; private set; }
        public int Exponent { get; private set; }

        public static readonly BigDecimal Zero = new BigDecimal(0);
        public static readonly BigDecimal One = new BigDecimal(1);

        public BigDecimal(BigInteger mantissa, int exponent = 0)
        {
            Mantissa = mantissa;
            Exponent = exponent;
            Normalize();
        }

        // Add implicit conversion from int to BigDecimal
        public static implicit operator BigDecimal(int value)
        {
            return new BigDecimal(value, 0);
        }

        // Add implicit conversion from long to BigDecimal
        public static implicit operator BigDecimal(long value)
        {
            return new BigDecimal(value, 0);
        }

        public static BigDecimal Parse(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentNullException(nameof(value));

            if (value.Contains("e", StringComparison.OrdinalIgnoreCase))
            {
                var parts = value.Split('e', 'E');
                var baseVal = BigDecimal.Parse(parts[0]);
                int exp = int.Parse(parts[1], CultureInfo.InvariantCulture);
                return baseVal * Pow(new BigDecimal(10), exp);  // Fixed: Convert 10 to BigDecimal
            }

            if (value.Contains('.'))
            {
                var parts = value.Split('.');
                var intPart = BigInteger.Parse(parts[0], CultureInfo.InvariantCulture);
                var fracPart = BigInteger.Parse(parts[1], CultureInfo.InvariantCulture);
                var fracLen = parts[1].Length;
                var mantissa = intPart * BigInteger.Pow(10, fracLen) + fracPart;  // Fixed: Use BigInteger.Pow instead of Pow10
                return new BigDecimal(mantissa, -fracLen);
            }

            return new BigDecimal(BigInteger.Parse(value, CultureInfo.InvariantCulture), 0);
        }

        public override string ToString() => ToStringPlain();

        public string ToStringPlain()
        {
            if (Mantissa.IsZero)
                return "0";

            var str = Mantissa.ToString();
            if (Exponent == 0)
                return str;

            if (Exponent > 0)
                return str + new string('0', Exponent);

            int pointPos = str.Length + Exponent;
            if (pointPos > 0)
                return str.Insert(pointPos, ".");

            return "0." + new string('0', -pointPos) + str;
        }

        public string ToAbbreviation(int digits = 3)
        {
            if (Mantissa.IsZero) return "0";

            double log10 = (BigInteger.Log10(BigInteger.Abs(Mantissa)) + Exponent);
            int exp = (int)(Math.Floor(log10 / 3) * 3);
            if (exp < 3) return ToStringPlain();

            var scaled = this / Pow(new BigDecimal(10), exp);  // Fixed: Convert 10 to BigDecimal
            double val = scaled.ToDouble();
            string suffix = GetSuffix(exp / 3);
            return val.ToString($"F{digits}") + suffix;
        }

        private static string GetSuffix(int index)
        {
            string[] suffixes = { "", "K", "M", "B", "T", "Qa", "Qi", "Sx", "Sp", "Oc", "No", "Dc" };
            return index < suffixes.Length ? suffixes[index] : $"e{index * 3}";
        }

        public double ToDouble()
        {
            return (double)Mantissa * Math.Pow(10, Exponent);
        }

        public static BigDecimal Pow(BigDecimal value, int exp)
        {
            if (exp == 0) return One;
            if (exp < 0) return One / Pow(value, -exp);

            BigDecimal result = One;
            for (int i = 0; i < exp; i++)
                result *= value;
            return result;
        }

        public static BigDecimal Pow10(int exp)
        {
            return new BigDecimal(BigInteger.One, exp);
        }

        public static BigDecimal Sqrt(BigDecimal value, int precision = DefaultPrecision)
        {
            if (value.Mantissa.Sign < 0)
                throw new ArithmeticException("Cannot sqrt negative number");

            BigDecimal guess = new BigDecimal(BigInteger.One, value.Exponent / 2);
            for (int i = 0; i < precision; i++)
            {
                guess = (guess + value / guess) / new BigDecimal(2);  // Fixed: Convert 2 to BigDecimal
            }
            return guess;
        }

        public static BigDecimal operator +(BigDecimal left, BigDecimal right)
        {
            AlignExponents(ref left, ref right, out var l, out var r);
            return new BigDecimal(l + r, left.Exponent).Normalized();
        }

        public static BigDecimal operator -(BigDecimal left, BigDecimal right)
        {
            AlignExponents(ref left, ref right, out var l, out var r);
            return new BigDecimal(l - r, left.Exponent).Normalized();
        }

        public static BigDecimal operator *(BigDecimal left, BigDecimal right)
        {
            return new BigDecimal(left.Mantissa * right.Mantissa, left.Exponent + right.Exponent).Normalized();
        }

        public static BigDecimal operator /(BigDecimal left, BigDecimal right)
        {
            int scale = DefaultPrecision;
            BigInteger dividend = left.Mantissa * BigInteger.Pow(10, scale);  // Fixed: Use BigInteger.Pow instead of Pow10(scale).Mantissa
            BigInteger quotient = dividend / right.Mantissa;
            return new BigDecimal(quotient, left.Exponent - right.Exponent - scale).Normalized();
        }

        public static bool operator >(BigDecimal left, BigDecimal right) => left.CompareTo(right) > 0;
        public static bool operator <(BigDecimal left, BigDecimal right) => left.CompareTo(right) < 0;
        public static bool operator >=(BigDecimal left, BigDecimal right) => left.CompareTo(right) >= 0;
        public static bool operator <=(BigDecimal left, BigDecimal right) => left.CompareTo(right) <= 0;
        public static bool operator ==(BigDecimal left, BigDecimal right) => left.Equals(right);
        public static bool operator !=(BigDecimal left, BigDecimal right) => !left.Equals(right);

        public int CompareTo(BigDecimal other)
        {
            AlignExponents(ref this, ref other, out var l, out var r);
            return l.CompareTo(r);
        }

        public bool Equals(BigDecimal other)
        {
            Normalize();
            other.Normalize();
            return Mantissa.Equals(other.Mantissa) && Exponent == other.Exponent;
        }

        public override bool Equals(object obj) => obj is BigDecimal bd && Equals(bd);
        public override int GetHashCode() => HashCode.Combine(Mantissa, Exponent);

        private void Normalize()
        {
            if (Mantissa.IsZero)
            {
                Exponent = 0;
                return;
            }

            BigInteger remainder;
            while ((remainder = Mantissa % 10) == 0)
            {
                Mantissa /= 10;
                Exponent++;
            }
        }

        private BigDecimal Normalized()
        {
            var copy = this;
            copy.Normalize();
            return copy;
        }

        private static void AlignExponents(ref BigDecimal a, ref BigDecimal b, out BigInteger left, out BigInteger right)
        {
            if (a.Exponent == b.Exponent)
            {
                left = a.Mantissa;
                right = b.Mantissa;
                return;
            }

            if (a.Exponent > b.Exponent)
            {
                int diff = a.Exponent - b.Exponent;
                left = a.Mantissa;
                right = b.Mantissa * BigInteger.Pow(10, diff);
            }
            else
            {
                int diff = b.Exponent - a.Exponent;
                left = a.Mantissa * BigInteger.Pow(10, diff);
                right = b.Mantissa;
            }
        }
    }

#if UNITY_UGUI
    public class BigDecimalText : MonoBehaviour
    {
        public BigDecimal Value;
        public Text TargetText;

        private void Update()
        {
            if (TargetText != null)
            {
                TargetText.text = Value.ToAbbreviation();
            }
        }
    }
#endif
}