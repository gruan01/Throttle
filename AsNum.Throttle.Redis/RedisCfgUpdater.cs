using StackExchange.Redis;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace AsNum.Throttle.Redis
{
    /// <summary>
    /// 
    /// </summary>
    public class RedisCfgUpdater : BaseCfgUpdater, IDisposable, IAutoDispose
    {

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
        private string keyPeriod = "";

        /// <summary>
        /// 
        /// </summary>
        private string keyFrequency = "";


        private bool firstLoad = true;

        /// <summary>
        /// 防止 InitializeAsync 重试时重复订阅（SubscribeAsync 成功但后续操作失败时）
        /// </summary>
        private int subscribed = 0;

        private RedisChannel channnel;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="connection"></param>
        public RedisCfgUpdater(ConnectionMultiplexer connection)
        {
            if (connection == null)
                throw new ArgumentNullException(nameof(connection));

            this.db = connection.GetDatabase();
            this.subscriber = connection.GetSubscriber();
        }


        /// <summary>
        /// 同步初始化：仅设置 key 名，不做 Redis I/O。
        /// PubSub 订阅移至 <see cref="InitializeAsync"/> 中异步完成。
        /// </summary>
        protected override (TimeSpan exPeriod, int exFrquency) Initialize(TimeSpan period, int frequency)
        {
            if (!firstLoad)
                return (period, frequency);

            this.firstLoad = false;

            this.keyFrequency = $"{this.ThrottleName}:frequency";
            this.keyPeriod = $"{this.ThrottleName}:period";

            // 此时 ThrottleName 已由 SetUp 赋值，channel 名正确
            this.channnel = new($"{this.ThrottleName}CfgChanged", RedisChannel.PatternMode.Auto);

            // SetUp 阶段不做 Redis I/O，值由调用方 SetUp 直接赋给 this.Period / this.Frequency
            return (period, frequency);
        }

        /// <summary>
        /// 异步初始化：订阅 PubSub、从 Redis 读取已有配置、保存当前配置。
        /// 由 Throttle 在首次 Select/Execute 时自动调用，消费方无需感知。
        /// </summary>
        public override async Task InitializeAsync()
        {
            //订阅消息 — 异步完成，避免启动时同步 Subscribe 超时
            // Interlocked 守卫：即使 InitializeAsync 被重试，也只订阅一次，防止重复 handler
            if (Interlocked.CompareExchange(ref subscribed, 1, 0) == 0)
            {
                await this.subscriber.SubscribeAsync(this.channnel, async (c, v) => await UpdateFromSubscribe(v));
            }

            //把初始化配置保存到 redis 中；如果存在则跳过。
            var (exPeriod, exFrequency) = await this.SaveToRedisAsync(this.Period, this.Frequency, false);

            //监控是否存在重新配置的值, 如果存在，则使用重新配置的值更新
            if ((exPeriod > TimeSpan.Zero && exFrequency > 0) && (exPeriod != this.Period || exFrequency != this.Frequency))
            {
                this.NotifyChange(exPeriod, exFrequency, this.Period, this.Frequency, "Initialize");
            }
        }





        /// <summary>
        /// 
        /// </summary>
        private async Task UpdateFromSubscribe(RedisValue v)
        {
            //不是自己发的订阅消息才处理, 避免重复重更改.
            if (!v.IsNullOrEmpty && !string.Equals(v, this.ThrottleID, StringComparison.OrdinalIgnoreCase))
            {
                //从 Redis 中拿出最新的配置
                var (period, frequency) = await this.Get();
                if (period > TimeSpan.Zero && frequency > 0)
                {
                    //接收到订阅消息时, redis 中是新的, 本地是老的
                    this.NotifyChange(period, frequency, this.Period, this.Frequency, "Redis Pubsub");
                }
            }
        }




        /// <summary>
        /// 保存并返回之前的数据
        /// </summary>
        /// <returns></returns>
        /// <param name="force">当 force = false 时, 如果 redis 中存在, 则不保存; 当 force = true 时， 无论如何都保存</param>
        /// <param name="frequency"></param>
        /// <param name="period"></param>
        private async Task<(TimeSpan exPeriod, int exFrequency)> SaveToRedisAsync(TimeSpan period, int frequency, bool force)
        {
            var when = force ? When.Always : When.NotExists;

            // 并行获取两个 key 的当前值，避免同步阻塞线程
            var fTask = db.StringGetAsync(keyFrequency, CommandFlags.DemandMaster);
            var pTask = db.StringGetAsync(keyPeriod, CommandFlags.DemandMaster);
            await Task.WhenAll(fTask, pTask);

            var f = (await fTask).ToInt(0);
            var p = (await pTask).ToInt(0);

            //when = When.NotExists 时, 只有 Redis 中不存在时才写。
            var setTasks = new System.Collections.Generic.List<Task>(2);
            if (f != frequency)
                setTasks.Add(db.StringSetAsync(keyFrequency, frequency, when: when, flags: CommandFlags.DemandMaster));
            if (p != period.TotalMilliseconds)
                setTasks.Add(db.StringSetAsync(keyPeriod, period.TotalMilliseconds, when: when, flags: CommandFlags.DemandMaster));

            if (setTasks.Count > 0)
                await Task.WhenAll(setTasks);

            return (TimeSpan.FromMilliseconds(p), f);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private async ValueTask<(TimeSpan period, int frequency)> Get()
        {
            var frequency = await db.StringGetAsync(keyFrequency).ToInt(0);
            var periodInMS = await db.StringGetAsync(keyPeriod).ToDouble(0);

            return (TimeSpan.FromMilliseconds(periodInMS), frequency);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        protected override async Task Save(TimeSpan newPeriod, int newFrequency)
        {
            await this.SaveToRedisAsync(newPeriod, newFrequency, true);
            await this.subscriber.PublishAsync(this.channnel, this.ThrottleID);
        }




        #region dispose
        /// <summary>
        /// 
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }


        /// <summary>
        /// 
        /// </summary>
        ~RedisCfgUpdater()
        {
            this.Dispose(false);
        }


        private bool isDisposed = false;
        /// <summary>
        /// 
        /// </summary>
        /// <param name="flag"></param>
        private void Dispose(bool flag)
        {
            if (!isDisposed)
            {
                if (flag)
                {
                    this.subscriber?.Unsubscribe(this.channnel);
                }
                isDisposed = true;
            }
        }
        #endregion
    }
}
