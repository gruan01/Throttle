using StackExchange.Redis;
using System;
using System.Diagnostics;

namespace AsNum.Throttle.Performance
{
    /// <summary>
    /// 
    /// </summary>
    public class RedisPerformanceCounter : BasePerformanceCounter
    {
        /// <summary>
        /// 
        /// </summary>
        private ConnectionMultiplexer Conn { get; }

        /// <summary>
        /// 
        /// </summary>
        public string InstanceID { get; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="instanceID">如果不传, 取当前进程的ID</param>
        public RedisPerformanceCounter(ConnectionMultiplexer conn, string instanceID = null)
        {
            if (string.IsNullOrWhiteSpace(instanceID))
                instanceID = Process.GetCurrentProcess().Id.ToString();

            this.Conn = conn ?? throw new ArgumentNullException(nameof(conn));
            this.InstanceID = instanceID;
        }


        public override void AddExecuted()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 
        /// </summary>
        public override void DecrementQueue()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// 
        /// </summary>
        public override void IncrementQueue()
        {
            throw new NotImplementedException();
        }

        protected override void Initialize()
        {
            throw new NotImplementedException();
        }

        protected override void InnerDispose()
        {
            throw new NotImplementedException();
        }
    }
}
