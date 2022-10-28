using System;
using System.Threading.Tasks;

namespace AsNum.Throttle
{
    /// <summary>
    /// 
    /// </summary>
    public abstract class BaseBlock : IUpdate, IDisposable
    {
        /// <summary>
        /// 
        /// </summary>
        public int Frequency { get; private set; }

        /// <summary>
        /// 避免因为客户端失去连接而引起的死锁
        /// </summary>
        protected TimeSpan? LockTimeout { get; private set; }


        /// <summary>
        /// 尝试占用一个位置
        /// </summary>
        internal abstract Task Acquire(string tag);

        /// <summary>
        /// 释放一个位置
        /// </summary>
        internal abstract Task Release(string tag);


        /// <summary>
        /// 
        /// </summary>
        /// <param name="frequency"></param>
        /// <param name="lockTimeout"></param>
        internal void Setup(int frequency, TimeSpan? lockTimeout)
        {
            this.Frequency = frequency;
            this.LockTimeout = lockTimeout;

            this.Initialize(true);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="period"></param>
        /// <param name="frequency"></param>
        Task IUpdate.Update(TimeSpan period, int frequency)
        {
            //值不一致时， 才重新 Initialize
            if (this.Frequency != frequency)
            {
                this.Frequency = frequency;
                this.Initialize(false);
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// 初始化
        /// </summary>
        /// <param name="firstLoad">是否是第一次初始化；如果为 false 则是通过配置更新器重新初始化的</param>
        protected abstract void Initialize(bool firstLoad);


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
