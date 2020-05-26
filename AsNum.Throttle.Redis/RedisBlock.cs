using StackExchange.Redis;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
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

        /// <summary>
        /// 分布锁的过期时间(毫秒), 预防应用程序挂掉, 导至死锁
        /// </summary>
        public int LockTimeout { get; }


        /// <summary>
        /// 本地阻止队列, 用于批量操作 Redis
        /// </summary>
        public int LocalBlockCapacity { get; }


        /// <summary>
        /// 本地队列
        /// </summary>
        private BlockingCollection<string> localBlocks;


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
        /// 心跳 Channel , 用于确定有多少客户端同时运行
        /// </summary>
        private string HeartChannel => $"{this.ThrottleName}#heart";


        /// <summary>
        /// 是否只有一个客户端
        /// </summary>
        private bool isSingleClient = true;

        /// <summary>
        /// 最后一次是不是该客户端获取了锁
        /// </summary>
        private bool lastPushSucc = true;

        /// <summary>
        /// 当 Task 压入时, 立即生成一个对应的 AutoResetEvent, 并发布一个含有该 Task 标识的消息
        /// 当消息处理器获取到该消息时, 将该 AutoResetEvent 设置为绿灯, 从而模拟 BlockingCollection 的特性.
        /// </summary>
        private readonly ConcurrentDictionary<string, AutoResetEvent> autoResetEvents = new ConcurrentDictionary<string, AutoResetEvent>();

        /// <summary>
        /// 
        /// </summary>
        private System.Timers.Timer timer;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="retryAddInterval">尝试压入 block 的重试周期(毫秒)</param>
        /// <param name="lockTimeout">分布锁的过期时间(毫秒), 预防应用程序挂掉, 导至死锁</param>
        /// <param name="localBlockCapacity">本地阻止队列的容量</param>
        public RedisBlock(ConnectionMultiplexer connection, int retryAddInterval = 100, int lockTimeout = 5000, int localBlockCapacity = 20)
        {
            this.Connection = connection ?? throw new ArgumentNullException(nameof(connection));
            this.RetryAddInterval = retryAddInterval;
            this.LockTimeout = lockTimeout;
            this.LocalBlockCapacity = localBlockCapacity;
            this.subscriber = connection.GetSubscriber();
            this.db = connection.GetDatabase();

            this.handler = (c, v) =>
            {
                var ts = ((string)v).Split(',');

                foreach (var t in ts)
                {
                    if (this.autoResetEvents.TryGetValue(t, out AutoResetEvent ae))
                    {
                        ae.Set();
                    }
                }
            };
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Timer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            //TryPush 会试图加锁, 发布消息, 计数,设置过期时间, 所以耗时会多于 RetryAddInterval,
            //这里不能被上面所说的整体操作时间影响, 否则, 会有空转.
            Task.Run(() => this.TryPush());
        }


        /// <summary>
        /// 
        /// </summary>
        protected override void Initialize()
        {
            this.localBlocks = new BlockingCollection<string>(Math.Min(this.BoundedCapacity, this.LocalBlockCapacity));
            this.subscriber.Subscribe(this.Channel, this.handler);
            this.subscriber.Subscribe(this.HeartChannel, (c, v) => { });

            this.Heart();

            this.timer = new System.Timers.Timer(this.RetryAddInterval)
            {
                AutoReset = true
            };
            this.timer.Elapsed += Timer_Elapsed;
            this.timer.Start();
        }


        /// <summary>
        /// 
        /// </summary>
        public override async Task Release(string tag)
        {
            var lockCountKey = this.ThrottleName.LockCountKey();
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
            this.localBlocks.Add(tag);
            //生成一个对应的信号灯, 等待绿灯放行.
            using var ar = this.autoResetEvents.GetOrAdd(tag, new AutoResetEvent(false));
            //Dely 会使 localBlocks 里的数量多于1个, 因为该方法是异步方法.
            //从而节省跟 Redis 的操作次数.
            //如果不用 Delay , 很可能就是每请求一次 Acquire , 就会操作 Redis 一次, 严重影响性能.
            await Task.Delay(this.RetryAddInterval);
            //等待订阅处理方法里的绿灯
            ar.WaitOne();
            this.autoResetEvents.TryRemove(tag, out _);
        }


        /// <summary>
        /// 定时发布一个空消息, 确定有多少个客户端在同时运行.
        /// </summary>
        private void Heart()
        {
            Task.Run(async () =>
            {
                while (true)
                {
                    //发布空消息
                    var clients = await this.subscriber.PublishAsync(this.HeartChannel, "");
                    this.isSingleClient = clients <= 1;
                    await Task.Delay(this.RetryAddInterval);
                }
            });
        }


        /// <summary>
        /// 
        /// </summary>
        private async Task TryPush()
        {
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
            if (this.isSingleClient || !this.lastPushSucc)
            {
                if (this.localBlocks?.Count > 0)
                {
                    var lockCountKey = this.ThrottleName.LockCountKey();
                    var lockKey = this.ThrottleName.LockKey();

                    if (await db.LockTakeAsync(lockKey, this.ThrottleID, TimeSpan.FromMilliseconds(this.LockTimeout)))
                    {
                        try
                        {
                            //怎么会出现小于0的情况??
                            //当没有新任务压入, 旧任务又陆续完成时,
                            var n = Math.Max(await db.StringGetIntAsync(lockCountKey), 0);

                            //还可以塞多少个进来
                            var c = Math.Min(this.BoundedCapacity - n, this.localBlocks.Count);
                            if (c > 0)
                            {
                                var ts = new List<string>();
                                for (var i = 0; i < c; i++)
                                {
                                    if (this.localBlocks.TryTake(out string tag))
                                    {
                                        ts.Add(tag);
                                    }
                                }

                                await this.subscriber.PublishAsync(this.Channel, string.Join(",", ts));
                                if (n == 0)
                                    await db.StringSetAsync(lockCountKey, ts.Count, flags: CommandFlags.DemandMaster);
                                else
                                    await db.StringIncrementAsync(lockCountKey, ts.Count, flags: CommandFlags.DemandMaster);

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
            this.localBlocks?.Dispose();
            this.subscriber?.Unsubscribe(this.Channel);
            this.subscriber?.Unsubscribe(this.HeartChannel);
        }

    }
}
