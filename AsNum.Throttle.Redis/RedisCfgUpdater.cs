using StackExchange.Redis;
using System;
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


        private RedisChannel Channnel { get; }

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
            this.Channnel = new($"{this.ThrottleName}CfgChanged", RedisChannel.PatternMode.Auto);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        protected override (TimeSpan exPeriod, int exFrquency) Initialize(TimeSpan period, int frequency)
        {
            if (!firstLoad)
                return (period, frequency);

            this.firstLoad = false;


            this.keyFrequency = $"{this.ThrottleName}:frequency";
            this.keyPeriod = $"{this.ThrottleName}:period";

            //订阅消息
            this.subscriber.Subscribe(this.Channnel, async (c, v) => await UpdateFromSubscribe(v));

            //把初始化配置保存到 redis 中；如果存在则跳过。
            var (exPeriod, exFrequency) = this.SaveToRedis(period, frequency, false);

            //监控是否存在重新配置的值, 如果存在，则使用重新配置的值更新
            if ((exPeriod > TimeSpan.Zero && exFrequency > 0) && (exPeriod != period || exFrequency != frequency))
            {
                //Redis 中是新的， 初始值是老的, Initialize 阶段，this.Period / this.Frequency 还没有赋值
                this.NotifyChange(exPeriod, exFrequency, period, frequency, "Initialize");
            }

            //用这两个值赋给 this.Period / this.Frequency
            return (exPeriod, exFrequency);
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
        private (TimeSpan exPeriod, int exFrequency) SaveToRedis(TimeSpan period, int frequency, bool force)
        {
            var when = force ? When.Always : When.NotExists;

            // StringSetAndGetAsnyc 要求 Redis 6.2.0
            //var ff = await db.StringSetAndGetAsync(keyFrequency, frequency, when: when, flags: CommandFlags.DemandMaster);
            //var pp = await db.StringSetAndGetAsync(keyPeriod, period.TotalMilliseconds, when: when, flags: CommandFlags.DemandMaster);

            var f = db.StringGet(keyFrequency, CommandFlags.DemandMaster).ToInt(0);
            var p = db.StringGet(keyPeriod, CommandFlags.DemandMaster).ToInt(0);

            //when = When.NotExists 时, 只有 Redis 中不存在时才写。
            if (f != frequency)
                db.StringSet(keyFrequency, frequency, when: when, flags: CommandFlags.DemandMaster);
            if (p != period.TotalMilliseconds)
                db.StringSet(keyPeriod, period.TotalMilliseconds, when: when, flags: CommandFlags.DemandMaster);

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
            this.SaveToRedis(newPeriod, newFrequency, true);
            await this.subscriber.PublishAsync(this.Channnel, this.ThrottleID);
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
                    this.subscriber?.Unsubscribe(this.Channnel);
                }
                isDisposed = true;
            }
        }
        #endregion
    }
}
