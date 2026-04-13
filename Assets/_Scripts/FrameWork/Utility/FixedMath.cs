using System;
using System.Runtime.CompilerServices;
namespace Utility
{
    public struct Fixed32
    {
        private const int MAX_VALUE = 0x7FFFFFFF;
        private const int MIN_VALUE = -0x80000000;
        private const int SHIFT = 16;
        private const int ONE = 1 << SHIFT;

        public readonly int raw;

        public static readonly Fixed32 Zero = new Fixed32(0);
        public static readonly Fixed32 One = new Fixed32(ONE);
        public static Fixed32 FromRaw(int v) => new Fixed32(v);

        public static implicit operator Fixed32(int v) => new Fixed32(v << SHIFT);
        public static implicit operator Fixed32(float f) => new Fixed32((int)(f * ONE));
        public static implicit operator float(Fixed32 f) => f.raw / (float)ONE;

        public static Fixed32 operator +(Fixed32 a, Fixed32 b) => new Fixed32(SaturateToInt((long)a.raw + b.raw));
        public static Fixed32 operator -(Fixed32 a, Fixed32 b) => new Fixed32(SaturateToInt((long)a.raw - b.raw));

        public static Fixed32 operator *(Fixed32 a, Fixed32 b)
        {
            long prod = (long)a.raw * b.raw;
            long r = prod >> SHIFT;
            return new Fixed32(SaturateToInt(r));
        }

        public static Fixed32 operator /(Fixed32 a, Fixed32 b)
        {
            if (b.raw == 0)
            {
                if (a.raw == 0) return Zero;
                return new Fixed32(a.raw > 0 ? MAX_VALUE : MIN_VALUE);
            }

            long num = (long)a.raw << SHIFT;
            return new Fixed32(SaturateToInt(num / b.raw));
        }

        public static Fixed32 operator %(Fixed32 a, Fixed32 b)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (b.raw == 0)
                throw new DivideByZeroException("Fixed32 modulo by zero.");
#else
        if (b.raw == 0)
            return Fixed32.Zero;
#endif
            return new Fixed32(a.raw % b.raw);
        }

        public static bool operator ==(Fixed32 a, Fixed32 b) => a.raw == b.raw;
        public static bool operator !=(Fixed32 a, Fixed32 b) => a.raw != b.raw;
        public static bool operator >(Fixed32 a, Fixed32 b) => a.raw > b.raw;
        public static bool operator <(Fixed32 a, Fixed32 b) => a.raw < b.raw;
        public static bool operator >=(Fixed32 a, Fixed32 b) => a.raw >= b.raw;
        public static bool operator <=(Fixed32 a, Fixed32 b) => a.raw <= b.raw;

        public static Fixed32 operator ++(Fixed32 a) => new Fixed32(SaturateToInt((long)a.raw + ONE));
        public static Fixed32 operator --(Fixed32 a) => new Fixed32(SaturateToInt((long)a.raw - ONE));

        private static int SaturateToInt(long v) => v > MAX_VALUE ? MAX_VALUE : (v < MIN_VALUE ? MIN_VALUE : (int)v);

        private Fixed32(int v) => raw = v;

        public override bool Equals(object obj) => obj is Fixed32 f && f.raw == raw;
        public override int GetHashCode() => raw;
        public override string ToString() => (raw / (float)ONE).ToString();

        public int CompareTo(Fixed32 other) => raw.CompareTo(other.raw);
    }
    public struct Fixed32LCG
    {
        private uint state;
        public Fixed32LCG(uint seed)
        {
            state = seed == 0 ? 350234 : seed;//避免为0时种子退化
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public uint NextUInt()
        {
            state = state * 1664525u + 1013904223u;
            return state;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Fixed32 NextFixed01()
        {
            uint x = NextUInt();
            int raw = (int)((x >> 16) & 0xFFFF);//为了只保留后16位，得到[0,1)的随机数
            return Fixed32.FromRaw(raw);
        }
        public Fixed32 Range(Fixed32 min, Fixed32 max)
        {
            if (max <= min)
                return min;
            return min + (max - min) * NextFixed01();
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int RangeUnbiased(int min, int max)
        {
            if (max <= min) return min;
            uint span = (uint)(max - min);
            uint threshold = (uint)(0u - span) % span;
            // 等价于：threshold = 2^32 % span
            // 只接受 x >= threshold 的值，使得可用范围长度是 span 的整数倍
            uint x;
            do
            {
                x = NextUInt();
            } while (x < threshold);

            return (int)(min + (int)(x % span));
        }

        public bool NextBool() => (NextUInt() & 1u) != 0u;

    }
    public static class Fixed32Math
    {
        public static readonly Fixed32 Pi = Fixed32.FromRaw(205887);
        public static readonly Fixed32 TwoPi = Fixed32.FromRaw(411775);
        public static readonly Fixed32 HalfPi = Fixed32.FromRaw(102944);
        private static readonly int MASK = -Fixed32.One.raw;     // 0xFFFF0000
        private static readonly int HALF = Fixed32.One.raw >> 1; // 0x00008000
        private static readonly int[] CordicAtan = new int[]
        {
        51472, 30386, 16055,  8150,
         4091,  2047,  1024,   512,
          256,   128,    64,    32,
           16,     8,     4,     2
        };
        private const int CordicK = 39797;
        public static void SinCos(Fixed32 angleRad, out Fixed32 sin, out Fixed32 cos)
        {
            //归一化到 [-pi, pi]
            Fixed32 a = NormalizeRadians(angleRad);
            //利用对称性把角度映射到 [-pi/2, pi/2]，提高精度并扩大可用范围
            bool flipCos = false;
            int z = a.raw;
            if (z > HalfPi.raw)
            {
                // a' = pi - a, sin(a)=sin(a'), cos(a)=-cos(a')
                z = Pi.raw - z;
                flipCos = true;
            }
            else if (z < -HalfPi.raw)
            {
                // a' = -pi - a, sin(a)=sin(a'), cos(a)=-cos(a')
                z = -Pi.raw - z;
                flipCos = true;
            }
            //CORDIC rotation：从 (K,0) 旋转到 angle
            int x = CordicK;
            int y = 0;
            for (int i = 0; i < 16; i++)
            {
                int xShift = x >> i;
                int yShift = y >> i;

                if (z >= 0)
                {
                    //旋转 -atan(2^-i)
                    int xNew = x - yShift;
                    int yNew = y + xShift;
                    x = xNew; y = yNew;
                    z -= CordicAtan[i];
                }
                else
                {
                    //旋转 +atan(2^-i)
                    int xNew = x + yShift;
                    int yNew = y - xShift;
                    x = xNew; y = yNew;
                    z += CordicAtan[i];
                }
            }
            // x,y 已经是 Q16.16 的 cos/sin
            if (flipCos) x = -x;

            cos = Fixed32.FromRaw(x);
            sin = Fixed32.FromRaw(y);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Fixed32 Sin(Fixed32 angleRad) { SinCos(angleRad, out var s, out _); return s; }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Fixed32 Cos(Fixed32 angleRad) { SinCos(angleRad, out _, out var c); return c; }
        public static Fixed32 Atan2(Fixed32 x, Fixed32 y)
        {
            int xi = x.raw;
            int yi = y.raw;
            if (xi == 0)
            {
                if (yi > 0) return HalfPi;
                if (yi < 0) return Fixed32.FromRaw(-HalfPi.raw);
                return Fixed32.Zero;
            }
            // CORDIC vectoring：把向量旋到 x 轴上，累计角度
            int z = 0;
            int vx = xi < 0 ? -xi : xi;
            int vy = yi;

            for (int i = 0; i < 16; i++)
            {
                int xShift = vx >> i;
                int yShift = vy >> i;

                if (vy > 0)
                {
                    // 旋转 -atan(2^-i)
                    int xNew = vx + yShift;
                    int yNew = vy - xShift;
                    vx = xNew; vy = yNew;
                    z += CordicAtan[i];
                }
                else
                {
                    // 旋转 +atan(2^-i)
                    int xNew = vx - yShift;
                    int yNew = vy + xShift;
                    vx = xNew; vy = yNew;
                    z -= CordicAtan[i];
                }
            }

            // 象限修正：如果原 x<0，角度要加/减 pi
            if (xi < 0)
            {
                if (yi >= 0) z = Pi.raw - z;
                else z = -Pi.raw - z;
            }

            return Fixed32.FromRaw(z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Fixed32 Atan(Fixed32 t) => Atan2(Fixed32.One, t);
        public static Fixed32 Tan(Fixed32 angleRad)
        {
            SinCos(angleRad, out var s, out var c);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (c.raw == 0) throw new DivideByZeroException("Fixed32 Tan: cos=0");
#endif
            return s / c;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Fixed32 Deg2Rad(Fixed32 deg) => deg * (Pi / 180);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Fixed32 Rad2Deg(Fixed32 rad) => rad * (180 / Pi);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Fixed32 NormalizeRadians(Fixed32 rad)
        {
            int raw = rad.raw;
            int twoPi = TwoPi.raw;
            raw %= twoPi;

            if (raw > Pi.raw) raw -= twoPi;
            else if (raw < -Pi.raw) raw += twoPi;

            return Fixed32.FromRaw(raw);
        }
        public static Fixed32 Asin(Fixed32 x)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (x < -Fixed32.One || x > Fixed32.One) throw new ArgumentOutOfRangeException(nameof(x), "asin domain [-1,1]");
#endif
            Fixed32 oneMinus = Fixed32.One - x * x;
            Fixed32 d = Sqrt(oneMinus);
            return Atan2(x, d);
        }

        public static Fixed32 Acos(Fixed32 x)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (x < -Fixed32.One || x > Fixed32.One) throw new ArgumentOutOfRangeException(nameof(x), "acos domain [-1,1]");
#endif
            Fixed32 oneMinus = Fixed32.One - x * x;
            Fixed32 d = Sqrt(oneMinus);
            return Atan2(d, x);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Fixed32 Abs(Fixed32 f)
        {
            if (f.raw == int.MinValue) return Fixed32.FromRaw(int.MaxValue);
            return f.raw >= 0 ? f : Fixed32.FromRaw(-f.raw);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Fixed32 Min(Fixed32 a, Fixed32 b) => a > b ? b : a;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Fixed32 Max(Fixed32 a, Fixed32 b) => a < b ? b : a;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Fixed32 Clamp(Fixed32 f, Fixed32 min, Fixed32 max) => f < min ? min : (f > max ? max : f);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Fixed32 Sign(Fixed32 f) => (f > 0 ? 1 : 0) - (f < 0 ? 1 : 0);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Fixed32 Floor(Fixed32 f)
        {
            return Fixed32.FromRaw(f.raw & MASK);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Fixed32 Ceil(Fixed32 f)
        {
            int floorRaw = f.raw & MASK;
            if (floorRaw == f.raw) return f;
            return Fixed32.FromRaw(floorRaw + Fixed32.One.raw);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Fixed32 Round(Fixed32 f)
        {
            int floorRaw = f.raw & MASK;
            int frac = f.raw - floorRaw;

            if (frac > HALF) return Fixed32.FromRaw(floorRaw + Fixed32.One.raw);
            if (frac < HALF) return Fixed32.FromRaw(floorRaw);

            return f.raw >= 0 ? Fixed32.FromRaw(floorRaw + Fixed32.One.raw) : Fixed32.FromRaw(floorRaw);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Fixed32 Frac(Fixed32 f) => f - Floor(f);

        public static Fixed32 Pow(Fixed32 x, int n)
        {
            if (n == 0) return Fixed32.One;

            if (n < 0)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                if (x.raw == 0) throw new DivideByZeroException("Fixed32 Pow: 0 cannot be raised to a negative power.");
#endif
                return Fixed32.One / Pow(x, -n);
            }

            Fixed32 result = Fixed32.One;
            Fixed32 baseVal = x;

            while (n > 0)
            {
                if ((n & 1) != 0) result *= baseVal;
                n >>= 1;
                if (n != 0) baseVal *= baseVal;
            }
            return result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Fixed32 Lerp(Fixed32 a, Fixed32 b, Fixed32 t) => a == b ? a : a + (b - a) * Clamp(t, 0, 1);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Fixed32 LerpUnclamped(Fixed32 a, Fixed32 b, Fixed32 t) => a == b ? a : a + (b - a) * t;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Fixed32 InverseLerp(Fixed32 a, Fixed32 b, Fixed32 v) => a == b ? a : (Clamp(v, a, b) - a) / (b - a);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Fixed32 SmoothStep(Fixed32 edge0, Fixed32 edge1, Fixed32 f)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (edge0 == edge1)
                throw new DivideByZeroException("Fixed32Math SmoothStep:edge0==edge1");
#else
        if (edge0 == edge1)
            return 0;
#endif
            Fixed32 temp = Clamp((f - edge0) / (edge1 - edge0), 0, 1);
            return temp * temp * (3 - 2 * temp);
        }

        public static Fixed32 MulDiv(Fixed32 a, Fixed32 b, Fixed32 c)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (c == 0)
                throw new DivideByZeroException("Fixed32Math MulDiv: Fixed32 modulo by zero.");
#else
        if (c.raw == 0)
            return Fixed32.Zero;
#endif
            long temp = (long)a.raw * (long)b.raw << 16;
            return Fixed32.FromRaw((int)(temp / c.raw));
        }
        public static Fixed32 Sqrt(Fixed32 v)
        {
            if (v.raw <= 0) return v.raw == 0 ? Fixed32.Zero :
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                throw new ArgumentOutOfRangeException(nameof(v), "Fixed32 Sqrt: negative");
#else
            Fixed32.Zero;
#endif

            ulong n = (ulong)(uint)v.raw;     // v >= 0，转无符号
            n <<= 16;                           // Q16.16 -> 需要开方的 Q32.32

            ulong r = ISqrt(n);                 // r 是 Q16.16 的 raw（无符号）
            if (r > int.MaxValue) return Fixed32.FromRaw(int.MaxValue);
            return Fixed32.FromRaw((int)r);
        }

        // 64-bit 整数平方根（floor）
        private static ulong ISqrt(ulong n)
        {
            ulong x = n;
            ulong y = (x + 1) >> 1;
            while (y < x)
            {
                x = y;
                y = (x + n / x) >> 1;
            }
            return x;
        }


    }
}
