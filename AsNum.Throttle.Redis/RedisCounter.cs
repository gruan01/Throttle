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
        public override int CurrentCount => this.Get();

        /// <summary>
        /// 
        /// </summary>
        private readonly ConnectionMultiplexer conn;


        /// <summary>
        /// 
        /// </summary>
        /// <param name="connection"></param>
        public RedisCounter(ConnectionMultiplexer connection)
        {
            this.conn = connection ?? throw new ArgumentNullException(nameof(connection));
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
        private int Get()
        {
            var db = this.conn.GetDatabase();

            var v = db.StringGet(this.ThrottleName, CommandFlags.PreferMaster);
            if (v.HasValue)
            {
                if (v.TryParse(out int i))
                    return i;
            }


            return 0;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override int IncrementCount()
        {
            var db = this.conn.GetDatabase();
            try
            {
                return (int)db.StringIncrement(this.ThrottleName, flags: CommandFlags.PreferMaster);
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
            var db = this.conn.GetDatabase();
            //db.StringSet(this.Key, 0, flags: CommandFlags.PreferMaster);
            var v = db.StringGetSet(this.ThrottleName, 0, flags: CommandFlags.PreferMaster);
            if (v.HasValue)
            {
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
