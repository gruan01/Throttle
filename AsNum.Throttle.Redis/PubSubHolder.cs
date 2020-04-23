using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AsNum.Throttle.Redis
{
    /// <summary>
    /// 
    /// </summary>
    public class PubSubHolder : IDisposable
    {
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
        private readonly string throttleName;

        /// <summary>
        /// Throttle 的周期, 用于设置 redis 键的有效期, 预防死锁
        /// </summary>
        private readonly TimeSpan period;

        /// <summary>
        /// 
        /// </summary>
        private readonly int boundedCapacity;


        /// <summary>
        /// 检查周期(毫秒), 用于周期性的重试将 tag 压入 block 中
        /// </summary>
        private readonly int retryAddInterval;

        /// <summary>
        /// 分布锁的有效期(毫秒),预防死锁
        /// </summary>
        private readonly int lockTimeout;


        /// <summary>
        /// 
        /// </summary>
        private readonly AutoResetEvent autoReset;


        /// <summary>
        /// 
        /// </summary>
        private Action<RedisChannel, RedisValue> handler;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="throttleName"></param>
        /// <param name="period">Throttle 的周期, 用于设置 redis 键的有效期, 预防死锁</param>
        /// <param name="boundedCapacity">block 的大小</param>
        /// <param name="retryAddInterval">尝试将压入 block 的重试周期(毫秒)</param>
        /// <param name="lockTimeout">分布锁的有效期(毫秒),预防死锁</param>
        public PubSubHolder(ConnectionMultiplexer conn,
                            string throttleName,
                            TimeSpan period,
                            int boundedCapacity,
                            int retryAddInterval,
                            int lockTimeout
                            )
        {
            this.subscriber = conn.GetSubscriber();
            this.db = conn.GetDatabase();
            this.throttleName = throttleName;
            this.period = period;
            this.boundedCapacity = boundedCapacity;
            this.retryAddInterval = retryAddInterval;
            this.lockTimeout = lockTimeout;
            this.autoReset = new AutoResetEvent(false);
        }


        /// <summary>
        /// 
        /// </summary>
        public void Wait(string tag)
        {
            this.handler = (c, v) =>
            {
                //Add 的时候, 会把 tag 做为参数 publish
                if (v == tag)
                {
                    this.subscriber.Unsubscribe(throttleName, this.handler);
                    autoReset.Set();
                    //Console.WriteLine($"................ Acquire");
                }
            };

            //
            this.subscriber.Subscribe(this.throttleName, this.handler);

            //确保 tag 被 publish
            this.SureAdd(tag);

            //
            this.autoReset.WaitOne();
        }






        /// <summary>
        /// 
        /// </summary>
        /// <param name="tag"></param>
        private void SureAdd(string tag)
        {
            //Console.WriteLine($"...................Sure {tag}");
            Task.Run(async () =>
            {
                while (!this.Add(tag))
                {
                    await Task.Delay(this.retryAddInterval);
                }
            });
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="tag"></param>
        /// <returns></returns>
        private bool Add(string tag)
        {
            //获得锁后, 正常情况下都会删除锁, 应该是几毫秒内的事,
            //综合考虑到可能应用程序挂掉的情况, 这里将锁的有效期设置为 5秒.
            if (db.LockTake(this.throttleName.LockKey(), tag, TimeSpan.FromMilliseconds(this.lockTimeout)))
            {

                try
                {
                    var n = db.StringGetInt(this.throttleName.LockCountKey());
                    if (n < this.boundedCapacity)
                    {
                        var a = db.StringIncrement(this.throttleName.LockCountKey());
                        //每次进入到这里, 都要重置一下这个键的有效期.
                        //如果这个键已经失效了, 说明任务已经全部运行完了.
                        //如果不设置有效期, 则下次在运行的时候, 会因为这个键有值, 而被迫等待.
                        db.KeyExpire(this.throttleName.LockCountKey(), this.period);
                        //Console.WriteLine($"................Add {a} {tag}");
                        //
                        this.subscriber.Publish(this.throttleName, tag);

                        return true;
                    }
                }
                finally
                {
                    //只要进入锁, 无论如何都要释放掉.
                    db.LockRelease(this.throttleName.LockKey(), tag);
                }
            }

            return false;
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
        ~PubSubHolder()
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
                    this.autoReset?.Close();
                    this.autoReset?.Dispose();
                }
                isDisposed = true;
            }
        }
        #endregion
    }
}
