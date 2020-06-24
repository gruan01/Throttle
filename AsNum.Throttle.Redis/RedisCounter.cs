using StackExchange.Redis;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace AsNum.Throttle.Redis
{
    /// <summary>
    /// 
    /// </summary>
    /// <remarks>
    /// 确保 Redis 的 notify-keyspace-events 中开启了 Ex
    /// </remarks>
    public class RedisCounter : BaseCounter
    {

        /// <summary>
        /// 
        /// </summary>
        private static readonly string KEY_EXPIRED_CHANNEL = "__keyevent@0__:expired";


        /// <summary>
        /// 
        /// </summary>
        private readonly int? _batchCount;

        /// <summary>
        /// 
        /// </summary>
        public override int BatchCount => this._batchCount ?? Math.Max(this.BoundedCapacity / (int)(this.heart?.SubscriberCount ?? 1), 1);

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
        private string countKey;

        /// <summary>
        /// 
        /// </summary>
        private string lockKey;


        /// <summary>
        /// 
        /// </summary>
        private Heart heart;


        /// <summary>
        /// 最后一次是不是该客户端获取了锁
        /// </summary>
        private bool lastPushSucc = true;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="batchCount"></param>
        /// <remarks>
        /// 在多进程下, 同时读取到的计数可能是相同的, 然后就会造成竞争, 从而会多出最多 N (N个进程) 个出来.
        /// </remarks>
        public RedisCounter(ConnectionMultiplexer connection, int? batchCount = null)
        {
            if (connection is null)
            {
                throw new ArgumentNullException(nameof(connection));
            }

            this.db = connection.GetDatabase();
            this.subscriber = connection.GetSubscriber();
            this._batchCount = batchCount;// batchCount ?? Math.Max(this.BoundedCapacity / (int)this.heart.SubscriberCount, 1);
        }


        /// <summary>
        /// 
        /// </summary>
        protected override void Initialize()
        {
            this.countKey = this.ThrottleName.ToCounterCountKey();
            this.lockKey = this.ThrottleName.ToCounterLockKey();

            this.heart = Heart.GetInstance(this.ThrottleName, this.subscriber);

            this.subscriber.Subscribe(KEY_EXPIRED_CHANNEL, (channel, value) =>
           {
               if (value == this.countKey)
               {
                   this.ResetFired();
               }
           });
        }


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override async ValueTask<int> CurrentCount()
        {
            return await this.db.StringGetIntAsync(this.countKey, flags: CommandFlags.DemandMaster);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override async ValueTask<int> IncrementCount(int a)
        {
            try
            {
                var n = (int)await this.db.StringIncrementAsync(this.countKey, a, flags: CommandFlags.DemandMaster);
                if (n == a)
                {
                    //只有第一次时, 才对该值做 TTL
                    await db.KeyExpireAsync(this.countKey, this.ThrottlePeriod, CommandFlags.DemandMaster);
                }
                return n;
            }
            catch
            {
                await this.db.StringSetAsync(this.countKey, a, flags: CommandFlags.DemandMaster);
                await db.KeyExpireAsync(this.countKey, this.ThrottlePeriod, flags: CommandFlags.DemandMaster);
                return 0;
            }
        }



        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override async ValueTask<bool> TryLock()
        {
            var succ = false;
            if (this.heart.IsSingleClient || !this.lastPushSucc)
            {
                succ = await this.db.LockTakeAsync(this.lockKey, this.ThrottleID, TimeSpan.FromSeconds(1));
            }

            this.lastPushSucc = succ;
            return succ;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override async Task ReleaseLock()
        {
            await this.db.LockReleaseAsync(this.lockKey, this.ThrottleID);
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
