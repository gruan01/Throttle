using System;
using System.Collections.Concurrent;
using System.Data.OleDb;
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
        private BlockingCollection<byte>? block;

        /// <summary>
        /// 
        /// </summary>
        protected override void Initialize(bool firstLoad)
        {
            var old = this.block;
            this.block = new BlockingCollection<byte>(this.Frequency);

            //重新配置时, 把已经占用的空间重新占用.
            var n = Math.Min(old?.Count ?? 0, this.Frequency);
            for (var i = 0; i < n; i++)
            {
                this.block.Add(0);
            }
            old?.Dispose();
        }



        /// <summary>
        /// 
        /// </summary>
        internal override Task Acquire(string tag)
        {
            if (this.LockTimeout.HasValue)
                this.block!.TryAdd(0, this.LockTimeout.Value);
            else
                this.block!.Add(0);

            return Task.CompletedTask;
        }



        /// <summary>
        /// 
        /// </summary>
        internal override Task Release(string tag)
        {
            if (this.LockTimeout.HasValue)
                this.block!.TryTake(out _, this.LockTimeout.Value);
            else
                this.block!.Take();

            return Task.CompletedTask;
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
