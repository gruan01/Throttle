using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AsNum.Throttle
{

    /// <summary>
    /// 
    /// </summary>
    public abstract class BaseCounter : IDisposable
    {

        /// <summary>
        /// 
        /// </summary>
        internal event EventHandler<EventArgs> OnReset;

        /// <summary>
        /// 
        /// </summary>
        public string ThrottleName { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        public string ThrottleID { get; private set; }


        /// <summary>
        /// 
        /// </summary>
        public TimeSpan ThrottlePeriod { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        public TimeSpan? LockTimeout { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        public int BoundedCapacity { get; private set; }


        /// <summary>
        /// 批量占用数,用于减少 Redis 等第三方组件的操作.
        /// </summary>
        public abstract int BatchCount { get; }

        /// <summary>
        /// 
        /// </summary>
        public abstract ValueTask<int> CurrentCount();


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public abstract ValueTask<int> IncrementCount(int n);


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public abstract ValueTask<bool> TryLock();


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public abstract Task ReleaseLock();


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public virtual Task WaitMoment()
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// 
        /// </summary>
        protected void ResetFired()
        {
            this.OnReset?.Invoke(this, EventArgs.Empty);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="throttleName"></param>
        /// <param name="throttleID"></param>
        /// <param name="boundedCapacity"></param>
        /// <param name="throttlePeriod"></param>
        /// <param name="lockTimeout"></param>
        internal void SetUp(string throttleName, string throttleID, int boundedCapacity, TimeSpan throttlePeriod, TimeSpan? lockTimeout)
        {
            this.ThrottleName = throttleName;
            this.ThrottleID = throttleID;

            this.ThrottlePeriod = throttlePeriod;
            this.BoundedCapacity = boundedCapacity;


            this.LockTimeout = lockTimeout;

            this.Initialize();
        }

        /// <summary>
        /// 
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
        ~BaseCounter()
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
