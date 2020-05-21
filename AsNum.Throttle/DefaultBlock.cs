using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace AsNum.Throttle
{
    /// <summary>
    /// 用 BlockingCollection 实现的阻止队列. 不能跨进程
    /// </summary>
    internal sealed class DefaultBlock : BaseBlock, IAutoDispose
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
        public override void Acquire(string tag)
        {
            if (this.BlockTimeout.HasValue)
                this.block.TryAdd(0, this.BlockTimeout.Value);
            else
                this.block.Add(0);
        }



        /// <summary>
        /// 
        /// </summary>
        public override void Release(string tag)
        {
            if (this.BlockTimeout.HasValue)
                this.block.TryTake(out _, this.BlockTimeout.Value);
            else
                this.block.Take();
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
