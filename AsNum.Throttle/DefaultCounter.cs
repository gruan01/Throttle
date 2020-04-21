using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace AsNum.Throttle
{
    /// <summary>
    /// 
    /// </summary>
    internal sealed class DefaultCounter : BaseCounter
    {

        /// <summary>
        /// 
        /// </summary>
        private int _currentCount;

        /// <summary>
        /// 
        /// </summary>
        public override int CurrentCount => this._currentCount;

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
            return Interlocked.Increment(ref this._currentCount);
        }

        /// <summary>
        /// 
        /// </summary>
        public override int ResetCount()
        {
            return Interlocked.Exchange(ref this._currentCount, 0);
        }

        /// <summary>
        /// 
        /// </summary>
        protected override void InnerDispose()
        {
        }
    }
}
