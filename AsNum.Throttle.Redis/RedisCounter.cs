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
        /// <param name="connection"></param>
        public RedisCounter(ConnectionMultiplexer connection)
        {
            if (connection is null)
            {
                throw new ArgumentNullException(nameof(connection));
            }

            this.db = connection.GetDatabase();
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
        /// <returns></returns>
        public override int IncrementCount()
        {
            try
            {
                var n = (int)this.db.StringIncrement(this.ThrottleName, flags: CommandFlags.DemandMaster);
                db.KeyExpire(this.ThrottleName, this.ThrottlePeriod, CommandFlags.DemandMaster);
                return n;
            }
            catch
            {
                this.ResetCount();
                return 0;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public override int ResetCount()
        {
            var v = this.db.StringGetSet(this.ThrottleName, 0, flags: CommandFlags.DemandMaster);
            if (v.HasValue)
            {
                db.KeyExpire(this.ThrottleName, this.ThrottlePeriod, flags: CommandFlags.DemandMaster);
                if (v.TryParse(out int i))
                    return i;
            }

            return 0;
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
