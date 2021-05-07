using System;
using System.Threading;
using System.Threading.Tasks;

namespace AsNum.Throttle.CrossProcess
{
    /// <summary>
    /// 使用 Semaphore 实现 跨进程的 Block
    /// </summary>
    [Obsolete]
    public class CrossProcessBlock : BaseBlock
    {
        /// <summary>
        /// 
        /// </summary>
        private Semaphore semaphore;


        /// <summary>
        /// 
        /// </summary>
        protected override void Initialize()
        {
            if (!Semaphore.TryOpenExisting(this.ThrottleName, out Semaphore s))
            {
                s = new Semaphore(this.BoundedCapacity, this.BoundedCapacity, this.ThrottleName);
            }
            this.semaphore = s;
        }


        /// <summary>
        /// 
        /// </summary>
        public override Task Acquire(string tag)
        {
            if (this.LockTimeout.HasValue)
                this.semaphore.WaitOne(this.LockTimeout.Value);
            else
                this.semaphore.WaitOne();

#if !NET451
            return Task.CompletedTask;
#else
            return Task.FromResult(true);
#endif
        }



        /// <summary>
        /// 
        /// </summary>
        public override Task Release(string tag)
        {
            this.semaphore.Release();
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
            this.semaphore?.Close();
            this.semaphore?.Dispose();
        }

    }
}
