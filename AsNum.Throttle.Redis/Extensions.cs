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
        /// <param name="db"></param>
        /// <param name="key"></param>
        /// <param name="defaultValue"></param>
        /// <param name="flags"></param>
        /// <returns></returns>
        public static async ValueTask<int> StringGetIntAsync(this IDatabase db, RedisKey key, int defaultValue = default, CommandFlags flags = CommandFlags.None)
        {
            var v = await db.StringGetAsync(key, flags);
            if (!v.IsNull)
            {
                try
                {
                    return (int)v;
                }
                catch
                {

                }
            }

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
