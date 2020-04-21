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

        /// <summary>
        /// 
        /// </summary>
        public string ThrottleName { get; private set; }

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
        /// 重置, 并返回之前的计数
        /// </summary>
        public abstract int ResetCount();


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
