using System;
using System.Collections;
using System.Threading.Tasks;
using StackExchange.Redis;

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
        internal static async Task<int> ToInt(this Task<RedisValue> v, int defaultValue)
        {
            try
            {
                var vv = await v;
                if (vv.TryParse(out int val))
                    return val;
            }
            catch
            {

            }
            return defaultValue;
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
                if (v.TryParse(out int val))
                    return val;
            }
            catch
            {

            }
            return defaultValue;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="v"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        internal static async Task<double> ToDouble(this Task<RedisValue> v, double defaultValue)
        {
            var vv = await v;
            if (vv.TryParse(out double val))
                return val;

            return defaultValue;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="v"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        internal static double ToDouble(this RedisValue v, double defaultValue)
        {
            if (v.TryParse(out double val))
                return val;

            return defaultValue;
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
