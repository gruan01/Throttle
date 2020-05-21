using StackExchange.Redis;
using System;

namespace AsNum.Throttle.Redis
{
    /// <summary>
    /// 
    /// </summary>
    public class RedisCounter : BaseCounter
    {
        /// <summary>
        /// 
        /// </summary>
        public override int CurrentCount => this.db.StringGetInt(this.ThrottleName, flags: CommandFlags.DemandMaster);// this.Get();

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
        /// <param name="connection"></param>
        public RedisCounter(ConnectionMultiplexer connection)
        {
            if (connection is null)
            {
                throw new ArgumentNullException(nameof(connection));
            }

            this.db = connection.GetDatabase();
            this.subscriber = connection.GetSubscriber();
        }


        /// <summary>
        /// 
        /// </summary>
        protected override void Initialize()
        {
            this.subscriber.Subscribe("__keyevent@0__:expired", (channel, value) =>
            {
                if (value == this.ThrottleName)
                {
                    this.ResetFired();
                }
            });
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override int IncrementCount()
        {
            try
            {
                var n = (int)this.db.StringIncrement(this.ThrottleName, flags: CommandFlags.DemandMaster);
                if (n == 1)
                {
                    //只有第一次时, 才对该值做 TTL
                    db.KeyExpire(this.ThrottleName, this.ThrottlePeriod, CommandFlags.DemandMaster);
                }
                return n;
            }
            catch
            {
                this.db.StringSet(this.ThrottleName, 0, flags: CommandFlags.DemandMaster);
                db.KeyExpire(this.ThrottleName, this.ThrottlePeriod, flags: CommandFlags.DemandMaster);
                return 0;
            }
        }


        /// <summary>
        /// 
        /// </summary>
        protected override void InnerDispose()
        {
        }
    }
}
