using StackExchange.Redis;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AsNum.Throttle.Redis
{
    /// <summary>
    /// 
    /// </summary>
    /// <remarks>
    /// 确保 Redis 的 notify-keyspace-events 中开启了 Ex: 
    /// config set notify-keyspace-events Ex
    /// </remarks>
    public class RedisCounter : BaseCounter
    {

        /// <summary>
        /// 
        /// </summary>
        private static readonly RedisChannel KEY_EXPIRED_CHANNEL = new("__keyevent@0__:expired", RedisChannel.PatternMode.Auto);


        /// <summary>
        /// 批量占用数,用于减少 Redis 等第三方组件的操作.
        /// </summary>
        public override int BatchCount { get; }


        /// <summary>
        /// 
        /// </summary>
        private readonly IDatabase db;

        /// <summary>
        /// 
        /// </summary>
        private readonly ISubscriber subscriber;


        /// <summary>
        /// 
        /// </summary>
        private string countKey = "";

        /// <summary>
        /// 
        /// </summary>
        private string lockKey = "";


        /// <summary>
        /// 
        /// </summary>
        private static readonly Random rnd = new();


        /// <summary>
        /// 用于随机等待, 如果不等待, 太消耗CPU.
        /// 即然用到了限频, 而且用到了 RedisCounter 说明是多进程同时在运行, 频率一定不会太高,
        /// 所以随机等待对请求速度影响不大.
        /// </summary>
        private readonly int rndSleepInMS = 5;


        //private static readonly string incrByLuaScriptStr = """
        //    local current
        //    current = redis.call("INCRBY", @key, @value)
        //    if tonumber(current) == tonumber(@value) then
        //        redis.call("expire", @key, @expire, "NX")
        //    end
        //    return current
        //    """;

        private static readonly string incrByLuaScriptStr = """
            local current
            current = redis.call("INCRBY", @key, @value)
            redis.call("expire", @key, @expire, "NX")
            return current
            """;

        private static readonly LuaScript incrByLuaScript = LuaScript.Prepare(incrByLuaScriptStr);


        private readonly LoadedLuaScript loadedIncrByLuaScript;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="batchCount">批大小; 为保证公平, 这个数字越小越好; 但是为了减少与 Redis 之间的通讯, 这个值越大越好.</param>
        /// /// <param name="rndSleepInMS">用于随机等待, 如果不等待, 太消耗CPU.</param>
        public RedisCounter(ConnectionMultiplexer connection, int batchCount = 1, int rndSleepInMS = 5)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

            if (batchCount <= 0)
                throw new ArgumentOutOfRangeException($"{nameof(batchCount)} must greate than 0");

            if (rndSleepInMS <= 0)
                throw new ArgumentOutOfRangeException($"{nameof(rndSleepInMS)} must greate than 0");


            this.db = connection.GetDatabase();
            this.subscriber = connection.GetSubscriber();
            this.BatchCount = batchCount;
            this.rndSleepInMS = rndSleepInMS;

            var server = connection
                .GetEndPoints()
                .Select(x => connection.GetServer(x))
                .First(x => !x.IsReplica);

            //https://stackexchange.github.io/StackExchange.Redis/Scripting.html#:~:text=StackExchange.Redis%20handles%20Lua%20script%20caching%20internally.%20It%20automatically
            this.loadedIncrByLuaScript = incrByLuaScript.Load(server);
        }


        /// <summary>
        /// 
        /// </summary>
        protected override void Initialize(bool firstLoad)
        {
            if (firstLoad)
            {
                this.countKey = this.ThrottleName.ToCounterCountKey();
                this.lockKey = this.ThrottleName.ToCounterLockKey();

                this.subscriber.Subscribe(KEY_EXPIRED_CHANNEL, (channel, value) =>
                {
                    var v = (string?)value;
                    //if (value == this.countKey)
                    if (string.Equals(v, this.countKey))
                    {
                        this.ResetFired();
                    }
                });
            }
        }


        /// <summary>
        /// 随机待待, 拯救CPU
        /// </summary>
        public override void WaitMoment()
        {
            SpinWait.SpinUntil(() => false, t);
        }


        private /*volatile*/ int t = 0;
        /// <summary>
        /// 
        /// </summary>
        public override void Change()
        {
            this.t = rnd.Next(1, this.rndSleepInMS);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override async ValueTask<uint> CurrentCount()
        {
            return await this.db.StringGetAsync(this.countKey, CommandFlags.DemandMaster).ToUInt(0);
        }




        ///// <summary>
        ///// 
        ///// </summary>
        ///// <returns></returns>
        //public override async ValueTask<uint> IncrementCount(uint a)
        //{
        //    //INCR / INCRBY 的 BUG, TTL 会丢失
        //    await this.CheckTTL();
        //    try
        //    {
        //        var nt = this.db.StringIncrementAsync(this.countKey, a, flags: CommandFlags.DemandMaster);

        //        var et = this.db.KeyExpireAsync(
        //            key: this.countKey,
        //            expiry: this.Period,
        //            when: ExpireWhen.HasNoExpiry,
        //            flags: CommandFlags.DemandMaster /*| CommandFlags.FireAndForget*/);

        //        await Task.WhenAll(nt, et);

        //        var n = await nt;

        //        return (uint)n;
        //    }
        //    catch
        //    {
        //        await this.db.StringSetAsync(this.countKey, a, this.Period, flags: CommandFlags.DemandMaster /*| CommandFlags.FireAndForget*/);
        //        return a;
        //    }
        //}


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override async ValueTask<uint> IncrementCount(uint a)
        {
            try
            {
                var n = await loadedIncrByLuaScript.EvaluateAsync(this.db, new
                {
                    key = this.countKey,
                    value = (int)a,
                    expire = (int)this.Period.TotalSeconds
                });

                return (uint)(int)n;
            }
            catch (Exception ex)
            {
                await this.db.StringSetAsync(this.countKey, a, this.Period, flags: CommandFlags.DemandMaster);
                return a;
            }
        }


        //private volatile int inCheck = 0;
        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private async Task CheckTTL()
        {
            try
            {
                var t = await this.db.KeyTimeToLiveAsync(this.countKey, CommandFlags.DemandMaster);
                if (t is null)
                {
                    await this.db.KeyDeleteAsync(this.countKey, CommandFlags.DemandMaster);
                }
            }
            catch
            {
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override async ValueTask<bool> TryLock()
        {
            return await this.db.LockTakeAsync(this.lockKey, this.ThrottleID, this.LockTimeout ?? TimeSpan.FromSeconds(1), CommandFlags.DemandMaster);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override async Task ReleaseLock()
        {
            await this.db.LockReleaseAsync(this.lockKey, this.ThrottleID, CommandFlags.DemandMaster | CommandFlags.FireAndForget);
        }


        /// <summary>
        /// 
        /// </summary>
        protected override void InnerDispose()
        {
            this.subscriber.Unsubscribe(KEY_EXPIRED_CHANNEL);
        }
    }
}
