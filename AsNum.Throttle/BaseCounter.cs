using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace AsNum.Throttle
{

    /// <summary>
    /// 
    /// </summary>
    public abstract class BaseCounter : IDisposable
    {

        internal event EventHandler<EventArgs> OnReset;

        /// <summary>
        /// 
        /// </summary>
        public string ThrottleName { get; private set; }


        /// <summary>
        /// 
        /// </summary>
        public TimeSpan ThrottlePeriod { get; private set; }


        /// <summary>
        /// 
        /// </summary>
        public abstract int CurrentCount { get; }


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public abstract int IncrementCount();



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
        /// <param name="throttlePeriod"></param>
        internal void SetUp(string throttleName, TimeSpan throttlePeriod)
        {
            this.ThrottleName = throttleName;

            this.ThrottlePeriod = throttlePeriod;

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
