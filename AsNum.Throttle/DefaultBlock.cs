using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AsNum.Throttle
{
    /// <summary>
    /// 用 BlockingCollection 实现的阻止队列. 不能跨进程
    /// </summary>
    public sealed class DefaultBlock : BaseBlock, IAutoDispose
    {

        /// <summary>
        /// 
        /// </summary>
        private BlockingCollection<byte> block;

        /// <summary>
        /// 
        /// </summary>
        protected override void Initialize()
        {
            this.block = new BlockingCollection<byte>(this.BoundedCapacity);
        }



        /// <summary>
        /// 
        /// </summary>
        public override Task Acquire(string tag)
        {
            if (this.LockTimeout.HasValue)
                this.block.TryAdd(0, this.LockTimeout.Value);
            else
                this.block.Add(0);

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
            if (this.LockTimeout.HasValue)
                this.block.TryTake(out _, this.LockTimeout.Value);
            else
                this.block.Take();

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
            if (this.block != null)
            {
                this.block.Dispose();
            }
        }


    }
}
