using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AdofaiMapConverter
{
    public static class Utils
    {
        public static TValue GetValueSafe<TKey, TValue>(this Dictionary<TKey, TValue> dict, TKey key, TValue defaultValue = default)
        {
            if (dict.TryGetValue(key, out TValue value))
                return value;
            return defaultValue;
        }
        public static TValue GetValueSafeOrAdd<TKey, TValue>(this Dictionary<TKey, TValue> dict, TKey key, TValue defaultValue)
        {
            if (dict.TryGetValue(key, out TValue value))
                return value;
            return dict[key] = defaultValue;
        }
        public static T Parse<T>(this string value) where T : Enum
        {
            try { return (T)Enum.Parse(typeof(T), value); }
            catch { return default(T); }
        }
        public static List<T> CastList<T>(this IList list)
        {
            List<T> castedList = new List<T>();
            foreach (object obj in list)
                castedList.Add((T)obj);
            return castedList;
        }
        public static bool IsFinite(this double d)
            => !IsNaN(d) && !IsInfinity(d);
        public unsafe static bool IsNaN(this double d)
            => (*(long*)&d & 0x7FFFFFFFFFFFFFFFL) > 0x7FF0000000000000L;
        public unsafe static bool IsInfinity(this double d)
            => (*(long*)&d & 0x7FFFFFFFFFFFFFFF) == 0x7FF0000000000000;
        public static bool FuzzyEquals(this double a, double b)
            => CopySign(a - b, 1) <= 0.0000001;
        public static double CopySign(double x, double y)
        {
            long xbits = BitConverter.DoubleToInt64Bits(x);
            long ybits = BitConverter.DoubleToInt64Bits(y);
            if ((xbits ^ ybits) < 0)
                BitConverter.Int64BitsToDouble(xbits ^ long.MinValue);
            return x;
        }
        public static List<T> SubList<T>(this List<T> list, int from, int to) => list.GetRange(from, to - from);
        public static double ToDeg(this double rad) => rad * (180.0 / Math.PI);
        public static double ToRad(this double deg) => Math.PI * deg / 180.0;
    }
}
