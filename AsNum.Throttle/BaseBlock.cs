using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace AsNum.Throttle
{
    /// <summary>
    /// 
    /// </summary>
    public abstract class BaseBlock : IDisposable
    {

        /// <summary>
        /// 
        /// </summary>
        protected string ThrottleName { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        protected string ThrottleID { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        protected int BoundedCapacity { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        protected TimeSpan ThrottlePeriod { get; private set; }

        /// <summary>
        /// 避免因为客户端失去连接而引起的死锁
        /// </summary>
        protected TimeSpan? LockTimeout { get; private set; }


        /// <summary>
        /// 尝试占用一个位置
        /// </summary>
        public abstract Task Acquire(string tag);

        /// <summary>
        /// 释放一个位置
        /// </summary>
        public abstract Task Release(string tag);


        /// <summary>
        /// 
        /// </summary>
        /// <param name="throttleName"></param>
        /// <param name="throttleID"></param>
        /// <param name="boundedCapacity"></param>
        /// <param name="throttlePeriod"></param>
        /// <param name="lockTimeout"></param>
        internal void Setup(string throttleName, string throttleID, int boundedCapacity, TimeSpan throttlePeriod, TimeSpan? lockTimeout)
        {
            this.ThrottleName = throttleName;
            this.ThrottleID = throttleID;
            this.BoundedCapacity = boundedCapacity;
            this.ThrottlePeriod = throttlePeriod;
            this.LockTimeout = lockTimeout;

            this.Initialize();
        }

        /// <summary>
        /// 初始化
        /// </summary>
        protected abstract void Initialize();


        /// <summary>
        /// 
        /// </summary>
        protected abstract void InnerDispose();


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
        ~BaseBlock()
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
                    this.InnerDispose();
                }
                isDisposed = true;
            }
        }
        #endregion
    }
}
