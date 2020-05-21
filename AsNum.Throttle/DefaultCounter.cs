using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Timers;

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
        private System.Timers.Timer timer;


        /// <summary>
        /// 
        /// </summary>
        protected override void Initialize()
        {
            this.timer = new System.Timers.Timer(this.ThrottlePeriod.TotalMilliseconds)
            {
                AutoReset = true
            };
            this.timer.Elapsed += Timer_Elapsed;
            this.timer.Start();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            Interlocked.Exchange(ref this._currentCount, 0);
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
