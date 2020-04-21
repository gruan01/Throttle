using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace AsNum.Throttle.Redis
{
    /// <summary>
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
        public int CheckIntervalInMillseconds { get; }

        ///// <summary>
        ///// 
        ///// </summary>
        //public override int Length => throw new NotImplementedException();


        /// <summary>
        /// 
        /// </summary>
        private readonly ConnectionMultiplexer conn;

        /// <summary>
        /// 
        /// </summary>
        private readonly ISubscriber subscriber;


        /// <summary>
        /// 
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="checkIntervalInMillseconds"></param>
        public RedisBlock(ConnectionMultiplexer connection, int checkIntervalInMillseconds = 100)
        {
            this.conn = connection ?? throw new ArgumentNullException(nameof(connection));
            this.subscriber = this.conn.GetSubscriber();

            this.CheckIntervalInMillseconds = checkIntervalInMillseconds;
        }


        /// <summary>
        /// 
        /// </summary>
        protected override void Initialize()
        {
            throw new NotImplementedException();
        }


        /// <summary>
        /// 
        /// </summary>
        public override void Release(string tag)
        {
            //release 时, 发布一个消息
            this.subscriber.Publish(this.ThrottleName, RedisValue.Null);
        }

        /// <summary>
        /// 
        /// </summary>
        public override void Acquire(string tag)
        {
            var pass = false;
            //收到消息后, 将 waiting 置为 false,
            this.subscriber.Subscribe(this.ThrottleName, (channel, value) =>
            {
                pass = value == tag;
                Console.WriteLine($"pass:{pass} tag:{tag}, value:{value}");
            });

            //
            this.SureAdd(tag);

            //然后跳出循环
            while (!pass)
            {

            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="tag"></param>
        private void SureAdd(string tag)
        {
            Task.Run(async () =>
            {
                while (!this.Add(tag))
                {
                    await Task.Delay(this.CheckIntervalInMillseconds);
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
            var lockKey = $"{this.ThrottleName}:lock";
            var db = this.conn.GetDatabase();
            //TODO timeout
            if (db.LockTake(lockKey, tag, TimeSpan.FromMinutes(1)))
            {
                //Key 对应的Item 为当前队列大小
                //lockKey 对应的 Item 为锁
                var n = db.StringIncrement(this.ThrottleName);
                if (n < this.BoundedCapacity)
                {
                    var sub = this.conn.GetSubscriber();
                    sub.Publish(this.ThrottleName, tag);

                    return true;
                }
                db.LockRelease(lockKey, tag);
            }

            return false;
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
