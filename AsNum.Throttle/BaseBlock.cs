using System;
using System.Collections.Generic;
using System.Text;

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
        protected int BoundedCapacity { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        protected TimeSpan ThrottlePeriod { get; private set; }

        /// <summary>
        /// 避免因为客户端失去连接而引起的死锁
        /// </summary>
        protected TimeSpan? BlockTimeout { get; private set; }


        /// <summary>
        /// 是否自动释放, 除 DefaultBlock 外, 其它 Block 都应手动释放.
        /// </summary>
        internal bool AutoDispose { get; set; }



        ///// <summary>
        ///// 阻止队列的长度
        ///// </summary>
        //public abstract int Length { get; }

        /// <summary>
        /// 尝试占用一个位置
        /// </summary>
        public abstract void Acquire(string tag);

        /// <summary>
        /// 释放一个位置
        /// </summary>
        public abstract void Release(string tag);


        /// <summary>
        /// 
        /// </summary>
        /// <param name="throttleName"></param>
        /// <param name="boundedCapacity"></param>
        /// <param name="throttlePeriod"></param>
        /// <param name="blockTimeout"></param>
        internal void Setup(string throttleName, int boundedCapacity, TimeSpan throttlePeriod, TimeSpan? blockTimeout)
        {
            this.ThrottleName = throttleName;
            this.BoundedCapacity = boundedCapacity;
            this.ThrottlePeriod = throttlePeriod;
            this.BlockTimeout = blockTimeout;

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
