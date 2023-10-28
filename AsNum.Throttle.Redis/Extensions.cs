using StackExchange.Redis;
using System;
using System.Threading.Tasks;

namespace AsNum.Throttle.Redis
{
    /// <summary>
    /// 
    /// </summary>
    public static class Extensions
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="v"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        internal static async ValueTask<int> ToInt(this Task<RedisValue> v, int defaultValue)
        {
            try
            {
                var vv = await v;
                return Convert.ToInt32(vv);
            }
            catch
            {
                return defaultValue;
            }
        }

        internal static async ValueTask<uint> ToUInt(this Task<RedisValue> v, uint defaultValue)
        {
            try
            {
                var vv = await v;
                return Convert.ToUInt32(vv);
            }
            catch
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="v"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        internal static int ToInt(this RedisValue v, int defaultValue)
        {
            try
            {
                return Convert.ToInt32(v);
            }
            catch
            {
                return defaultValue;
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="v"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        internal static async ValueTask<double> ToDouble(this Task<RedisValue> v, double defaultValue)
        {
            var vv = await v;
            try
            {
                return Convert.ToDouble(vv);
            }
            catch
            {
                return defaultValue;
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="throttleName"></param>
        /// <returns></returns>
        internal static string ToCounterLockKey(this string throttleName)
        {
            return $"{throttleName}:counter:lock";
        }


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        internal static string ToCounterCountKey(this string throttleName)
        {
            return $"{throttleName}:counter:count";
        }


    }
}
