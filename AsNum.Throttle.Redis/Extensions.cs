using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Text;

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
        ///// <param name=""></param>
        ///// <param name="key"></param>
        ///// <param name="timeoutInSeconds">等待时间, 如果小于等于0, 则无限等待</param>
        ///// <returns></returns>
        //[Obsolete("ConnectionMultiplexer 多路复用, 会因为 BLPOP 被阻塞, 而影响其它REDIS的操作")]
        //public static RedisResult BLPop(this IDatabase db, RedisKey key, int timeoutInSeconds = 0)
        //{
        //    //StackExchange.Redis 有执行超时时间, 取连接字符串中的 syncTimeout , 默认为 5秒
        //    //如果等待时间大于执行超时间, 则报 TimeoutException

        //    var rst = RedisResult.Create(RedisValue.Null);
        //    var syncTimeoutInSeconds = db.Multiplexer.TimeoutMilliseconds / 1000;

        //    //无限等待
        //    if (timeoutInSeconds <= 0)
        //    {
        //        while (true)
        //        {
        //            rst = db.Execute("BLPOP", key.ToString(), syncTimeoutInSeconds);
        //            if (!rst.IsNull)
        //            {
        //                break;
        //            }
        //        }
        //    }
        //    else
        //    {
        //        //有限等待
        //        var n = timeoutInSeconds;
        //        while (n > 0)
        //        {
        //            var wait = Math.Min(n, syncTimeoutInSeconds);
        //            rst = db.Execute("BLPOP", key.ToString(), wait);
        //            if (!rst.IsNull)
        //            {
        //                break;
        //            }
        //            n -= wait;
        //        }
        //    }

        //    if (!rst.IsNull)
        //    {
        //        //RedisResult
        //        var dic = rst.ToDictionary();
        //        if (dic.ContainsKey(key))
        //            return dic[key];
        //    }

        //    return rst;
        //}



        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="db"></param>
        /// <param name="key"></param>
        /// <param name="defaultValue"></param>
        /// <param name="flags"></param>
        /// <returns></returns>
        public static int StringGetInt(this IDatabase db, RedisKey key, int defaultValue = default, CommandFlags flags = CommandFlags.None)
        {
            var v = db.StringGet(key, flags);
            if (v != RedisValue.Null)
            {
                if (v.TryParse(out int vv))
                    return vv;
            }

            return defaultValue;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="throttleName"></param>
        /// <returns></returns>
        internal static string LockKey(this string throttleName)
        {
            return $"{throttleName}:lock";
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="throttleName"></param>
        /// <returns></returns>
        internal static string LockCountKey(this string throttleName)
        {
            return $"{throttleName}:lockCount";
        }
    }
}
