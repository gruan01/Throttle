using System;
using System.Collections.Generic;
using System.Text;

namespace AsNum.Throttle
{
    /// <summary>
    /// 
    /// </summary>
    [Obsolete]
    public abstract class BasePerformanceCounter : IDisposable
    {

        /// <summary>
        /// 
        /// </summary>
        public string ThrottleName { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        public abstract void IncrementQueue();

        /// <summary>
        /// 
        /// </summary>
        public abstract void DecrementQueue();

        /// <summary>
        /// 
        /// </summary>
        public abstract void AddExecuted();


        /// <summary>
        /// 
        /// </summary>
        /// <param name="throttleName"></param>
        internal void SetUp(string throttleName)
        {
            this.ThrottleName = throttleName;
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
        ~BasePerformanceCounter()
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
