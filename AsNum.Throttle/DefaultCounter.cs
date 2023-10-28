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
        private volatile uint _currentCount;


        /// <summary>
        /// 
        /// </summary>
        public override int BatchCount => 1;


        /// <summary>
        /// 用于 周期性的 重置计数
        /// </summary>
        private Timer? timer;

        /// <summary>
        /// 
        /// </summary>
        private int avg = 0;


        private readonly Random rnd = new();



        /// <summary>
        /// 
        /// </summary>
        protected override void Initialize(bool firstLoad)
        {
            if (this.timer != null)
            {
                //TimeSpan.Zero 会立即执行 TimerCallback, 换成 Timeout.InfiniteTimeSpan 后, TimerCallback 又不执行了...
                //this.timer.Change(TimeSpan.Zero, this.Period);
                //this.timer.Change(Timeout.InfiniteTimeSpan, this.Period);

                this.timer.Change(this.Period, this.Period);
            }
            else
            {
                this.timer?.Dispose();
                this.timer = new Timer(new TimerCallback(Timer_Elapsed), null, this.Period, this.Period);
            }
            //一般如果是限制执行频率的， 频率根本不会太大。
            //如果不暂停的话， 会一直执行循环，导致 CPU 空转浪费。
            //假设1秒钟允许执行60次，合16毫秒执行一次，
            //如果加上暂停，应该是 1 秒钟内 60次执行，60次暂停， 合 8 毫秒一次。
            this.avg = (int)this.Period.TotalMilliseconds / this.Frequency / 2;
        }


        /// <summary>
        /// 
        /// </summary>
        private void Timer_Elapsed(object? state)
        {
            Interlocked.Exchange(ref this._currentCount, 0);
            this.ResetFired();
        }



        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override async Task WaitMoment()
        {
            if (this.avg > 1)
            {
                var n = rnd.Next(0, this.avg);
                if (n > 0)
                {
                    await Task.Delay(n);
                }
            }

        }


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override ValueTask<uint> CurrentCount()
        {
            return new ValueTask<uint>(this._currentCount);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override ValueTask<uint> IncrementCount(uint n)
        {
            var a = Interlocked.Add(ref this._currentCount, n);
            return new ValueTask<uint>(a);
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
            return Task.CompletedTask;
        }


        /// <summary>
        /// 
        /// </summary>
        protected override void InnerDispose()
        {
            this.timer?.Dispose();
        }


        #region dispose

        /// <summary>
        /// 
        /// </summary>
        ~DefaultCounter()
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
                    this.timer?.Dispose();
                }
                isDisposed = true;
            }
        }
        #endregion
    }
}
