using StackExchange.Redis;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace AsNum.Throttle.Redis
{
    /// <summary>
    /// 原本是想用 BLPOP 来实现 BLOCK 的, 但是 StackExchange.Redis (SR) 中没有实现该方法.
    /// 所以, 这里用订阅发布加上 AutoResetEvent 来模拟了 BLOCK 操作.
    /// 
    /// 原思路:
    /// Redis BLPOP / BRPOP 如果队列为空， 则阻塞连接， 直到队列中的对象可以出队时在返回。
    /// 和要求的入队阻塞刚好向反。
    ///
    /// 入队一次， 从 REDIS 队列中删除（BLPOP / BRPOP ）一个，则直到 REDIS 队列为空， 就不能在入队了。
    /// 出队一次， 向 REDIS 队列中添加一个。
    /// </summary>
    [Obsolete]
    public class RedisBlock : BaseBlock
    {
        /// <summary>
        /// 
        /// </summary>
        public ConnectionMultiplexer Connection { get; }

        /// <summary>
        /// 尝试将压入 block 的重试周期(毫秒)
        /// </summary>
        public int RetryAddInterval { get; }

        ///// <summary>
        ///// 分布锁的过期时间(毫秒), 预防应用程序挂掉, 导至死锁
        ///// </summary>
        //public int LockTimeout { get; }


        /// <summary>
        /// 
        /// </summary>
        private SemaphoreSlim semaphoreSlim;


        /// <summary>
        /// 
        /// </summary>
        private readonly ISubscriber subscriber;

        /// <summary>
        /// 
        /// </summary>
        private readonly IDatabase db;


        /// <summary>
        /// 
        /// </summary>
        private readonly Action<RedisChannel, RedisValue> handler;


        /// <summary>
        /// 模拟分布式阻塞队列的 Redis Channel, 这里使用了 ThrottleID, 保证当前客户端发的消息只有当前客户端能收到.
        /// </summary>
        private string Channel => $"{this.ThrottleName}#{this.ThrottleID}".To16bitMD5();


        /// <summary>
        /// 最后一次是不是该客户端获取了锁
        /// </summary>
        private bool lastPushSucc = true;

        /// <summary>
        /// 
        /// </summary>
        private readonly ConcurrentBag<byte> bag = new ConcurrentBag<byte>();

        /// <summary>
        /// 
        /// </summary>
        private Timer timer;

        /// <summary>
        /// 
        /// </summary>
        private Heart heart;


        /// <summary>
        /// 
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="retryAddInterval">尝试压入 block 的重试周期(毫秒)</param>
        public RedisBlock(ConnectionMultiplexer connection, int retryAddInterval = 100)
        {
            this.Connection = connection ?? throw new ArgumentNullException(nameof(connection));
            this.RetryAddInterval = retryAddInterval;
            this.subscriber = connection.GetSubscriber();
            this.db = connection.GetDatabase();

            this.handler = (c, v) =>
            {
                if (v.TryParse(out int n))
                {
                    this.semaphoreSlim?.Release(n);
                }
            };
        }

        /// <summary>
        /// 
        /// </summary>
        private void Timer_Elapsed(object state)
        {
            //TryPush 会试图加锁, 发布消息, 计数,设置过期时间, 所以耗时会多于 RetryAddInterval,
            //这里不能被上面所说的整体操作时间影响, 否则, 会有空转.
            //Task.Run(() => this.TryPush());
            Task.Factory.StartNew(async () =>
            {
                await this.TryPush();
            }, TaskCreationOptions.LongRunning | TaskCreationOptions.PreferFairness);
            //ThreadPool.QueueUserWorkItem(new WaitCallback(AA));
        }

        /// <summary>
        /// 
        /// </summary>
        protected override void Initialize()
        {
            this.semaphoreSlim = new SemaphoreSlim(this.BoundedCapacity, this.BoundedCapacity);
            this.subscriber.Subscribe(this.Channel, this.handler);
            this.heart = Heart.GetInstance(this.ThrottleName, this.subscriber);

            this.timer = new Timer(new TimerCallback(Timer_Elapsed), null, 0, this.RetryAddInterval);
        }


        /// <summary>
        /// 
        /// </summary>
        public override async Task Release(string tag)
        {
            var lockCountKey = this.ThrottleName.ToBlockCountKey();
            await this.db.StringDecrementAsync(lockCountKey);
            //假如这里不设置过期时间, 而只是在 Increment 那里设置.
            //则, 当没有新任务进入, 老任务又没有执行完
            //KEY 过期, 这个 Decrement 就会默认从0开始. 然后就会出现负数
            await db.KeyExpireAsync(lockCountKey, this.ThrottlePeriod, flags: CommandFlags.DemandMaster);
        }



        /// <summary>
        /// 
        /// </summary>
        public override async Task Acquire(string tag)
        {
            await this.semaphoreSlim.WaitAsync();
            this.bag.Add(0);
        }

        /// <summary>
        /// 
        /// </summary>
        private async Task TryPush()
        {
            //Console.WriteLine("here");
            var succ = false;
            //如果只有一个客户端, 或者上一个轮询中, 不是当前客户端 获取的锁
            //用于模拟公平竞争.
            //假如有 A,B,C 三个客户端, 每个 Task 的运行时间相同
            //启动时间:
            //A XX:00:00.000
            //B XX:00:00.010
            //C XX:00:00.020
            //由于 RetryAddInterval 都是一样的, 所以第一次是 A 获取锁, 第二次还是 A 会获取锁.第N次还是A会获取锁.
            //所以这里, 如果是多客户端, 且最后一次是当前客户端, 就让出一个时间片给其它客户端.
            //让执行看起来公平一点.
            if (this.heart.IsSingleClient || !this.lastPushSucc)
            {
                if (this.bag?.Count > 0)
                {
                    var lockCountKey = this.ThrottleName.ToBlockCountKey();
                    var lockKey = this.ThrottleName.ToBlockLockKey();

                    //if (await db.LockTakeAsync(lockKey, this.ThrottleID, TimeSpan.FromMilliseconds(this.LockTimeout)))
                    if (await db.LockTakeAsync(lockKey, this.ThrottleID, this.LockTimeout ?? TimeSpan.FromSeconds(1)))
                    {
                        try
                        {
                            //怎么会出现小于0的情况??
                            //当没有新任务压入, 旧任务又陆续完成时,
                            var n = Math.Max(await db.StringGetIntAsync(lockCountKey), 0);

                            //还可以塞多少个进来
                            var c = Math.Min(this.BoundedCapacity - n, this.bag.Count);
                            //Console.WriteLine($"here {c}");
                            if (c > 0)
                            {
                                var x = 0;
                                for (var i = 0; i < c; i++)
                                {
                                    if (this.bag.TryTake(out _))
                                    {
                                        x++;
                                    }
                                }

                                await this.subscriber.PublishAsync(this.Channel, x);
                                if (n == 0)
                                    await db.StringSetAsync(lockCountKey, x, flags: CommandFlags.DemandMaster);
                                else
                                    await db.StringIncrementAsync(lockCountKey, x, flags: CommandFlags.DemandMaster);

                                await db.KeyExpireAsync(lockCountKey, this.ThrottlePeriod, flags: CommandFlags.DemandMaster);

                                succ = true;
                            }
                        }
                        finally
                        {
                            //只要进入锁, 无论如何都要释放掉.
                            await db.LockReleaseAsync(lockKey, this.ThrottleID);
                        }
                    }
                }
            }

            this.lastPushSucc = succ;
        }


        /// <summary>
        /// 
        /// </summary>
        protected override void InnerDispose()
        {
            this.semaphoreSlim?.Dispose();
            this.subscriber?.Unsubscribe(this.Channel);
            this.timer?.Dispose();
        }

    }
}
