using System;
using System.Threading;

namespace AsNum.Throttle
{
    /// <summary>
    /// 
    /// </summary>
    internal sealed class DefaultCounter : BaseCounter, IAutoDispose
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
        /// 用于 周期性的 重置计数
        /// </summary>
        private Timer timer;


        /// <summary>
        /// 
        /// </summary>
        protected override void Initialize()
        {
            this.timer = new Timer(new TimerCallback(Timer_Elapsed), null, TimeSpan.Zero, this.ThrottlePeriod);
        }

        /// <summary>
        /// 
        /// </summary>
        private void Timer_Elapsed(object state)
        {
            Interlocked.Exchange(ref this._currentCount, 0);
            this.ResetFired();
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
        protected override void InnerDispose()
        {
            this.timer?.Dispose();
        }
    }
}
