using System;
using System.Threading;
using System.Threading.Tasks;

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
        public override int BatchCount => 1;


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
        public override ValueTask<int> CurrentCount()
        {
            return new ValueTask<int>(this._currentCount);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override ValueTask<int> IncrementCount(int n)
        {
            var a = Interlocked.Add(ref this._currentCount, n);
            return new ValueTask<int>(a);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override ValueTask<bool> TryLock()
        {
            return new ValueTask<bool>(true);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override Task ReleaseLock()
        {
#if !NET451
            return Task.CompletedTask;
#else
            return Task.FromResult(true);
#endif
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
