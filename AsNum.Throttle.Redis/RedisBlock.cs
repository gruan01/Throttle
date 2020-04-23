using StackExchange.Redis;
using System;
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



        ///// <summary>
        ///// 
        ///// </summary>
        //public override int Length => throw new NotImplementedException();


        /// <summary>
        /// 
        /// </summary>
        private readonly IDatabase db;


        /// <summary>
        /// 
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="retryAddInterval">尝试将压入 block 的重试周期(毫秒)</param>
        /// <param name="lockTimeout">分布锁的过期时间(毫秒), 预防应用程序挂掉, 导至死锁</param>
        public RedisBlock(ConnectionMultiplexer connection, int retryAddInterval = 100, int lockTimeout = 5000)
        {
            if (connection is null)
            {
                throw new ArgumentNullException(nameof(connection));
            }

            this.Connection = connection;
            this.db = connection.GetDatabase();
            this.RetryAddInterval = retryAddInterval;
            LockTimeout = lockTimeout;
        }


        /// <summary>
        /// 
        /// </summary>
        protected override void Initialize()
        {
        }


        /// <summary>
        /// 
        /// </summary>
        public override void Release(string tag)
        {
            var n = db.StringDecrement(this.ThrottleName.LockCountKey());
            //Console.WriteLine($"................Release {n}");
        }



        /// <summary>
        /// 
        /// </summary>
        public override void Acquire(string tag)
        {
            using var holder = new PubSubHolder(
                this.Connection,
                this.ThrottleName,
                this.ThrottlePeriod,
                this.BoundedCapacity,
                this.RetryAddInterval,
                this.LockTimeout);

            holder.Wait(tag);
        }




        /// <summary>
        /// 
        /// </summary>
        protected override void InnerDispose()
        {
            //if (this.conn != null)
            //{
            //    this.conn.Dispose();
            //}
        }

    }
}
