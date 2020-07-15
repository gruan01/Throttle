using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace AsNum.Throttle.Redis
{
    /// <summary>
    /// 
    /// </summary>
    public static class Extensions
    {
        ///// <summary>
        ///// 
        ///// </summary>
        ///// <param name="db"></param>
        ///// <param name="key"></param>
        ///// <param name="defaultValue"></param>
        ///// <param name="flags"></param>
        ///// <returns></returns>
        //public static int StringGetInt(this IDatabase db, RedisKey key, int defaultValue = default, CommandFlags flags = CommandFlags.None)
        //{
        //    var v = db.StringGet(key, flags);
        //    if (v.HasValue)
        //    {
        //        if (v.TryParse(out int vv))
        //            return vv;
        //    }

        //    return defaultValue;
        //}

#if !NET451
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
                //if (v.TryParse(out int vv))
                //    return vv;
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
#else
        /// <summary>
        /// 
        /// </summary>
        /// <param name="db"></param>
        /// <param name="key"></param>
        /// <param name="defaultValue"></param>
        /// <param name="flags"></param>
        /// <returns></returns>
        public static async Task<int> StringGetIntAsync(this IDatabase db, RedisKey key, int defaultValue = default, CommandFlags flags = CommandFlags.None)
        {
            var v = await db.StringGetAsync(key, flags);
            if (!v.IsNull)
            {
                //if (v.TryParse(out int vv))
                //    return vv;
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
#endif

        /// <summary>
        /// 
        /// </summary>
        /// <param name="throttleName"></param>
        /// <returns></returns>
        internal static string ToBlockLockKey(this string throttleName)
        {
            return $"{throttleName}:block:lock";
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="throttleName"></param>
        /// <returns></returns>
        internal static string ToBlockCountKey(this string throttleName)
        {
            return $"{throttleName}:block:lockCount";
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





        /// <summary>
        /// 
        /// </summary>
        /// <param name="input"></param>
        /// <param name="toUpper"></param>
        /// <param name="encoding">如果为 NULL, 则默认使用 UTF8</param>
        /// <returns></returns>
        internal static string To16bitMD5(this string input, bool toUpper = false, Encoding encoding = null)
        {
            if (string.IsNullOrEmpty(input))
                return "";

            if (encoding == null)
                encoding = Encoding.UTF8;

            using var md5 = new MD5CryptoServiceProvider();
            string result = BitConverter.ToString(md5.ComputeHash(encoding.GetBytes(input)), 4, 8);
            var s = result.Replace("-", "");
            if (toUpper)
                return s.ToUpper();
            else
                return s.ToLower();
        }
    }
}
