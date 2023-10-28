using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace AsNum.Throttle
{
    /// <summary>
    /// 用 BlockingCollection 实现的阻止队列. 不能跨进程
    /// </summary>
    public sealed class DefaultBlock : BaseBlock, IAutoDispose, IDisposable
    {

        /// <summary>
        /// 
        /// </summary>
        private BlockingCollection<byte> block = new();

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
        internal override void Acquire()
        {
            if (this.LockTimeout is null)
                this.block.Add(0);
            else
                this.block.TryAdd(0, this.LockTimeout.Value);
        }



        /// <summary>
        /// 
        /// </summary>
        internal override void Release()
        {
            if (this.LockTimeout is null)
                this.block.Take();
            else
                this.block.TryTake(out _, this.LockTimeout.Value);
        }


        /// <summary>
        /// 
        /// </summary>
        protected override void InnerDispose()
        {
            this.block?.Dispose();
        }



        #region dispose

        /// <summary>
        /// 
        /// </summary>
        ~DefaultBlock()
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
                    this.block?.Dispose();
                }
                isDisposed = true;
            }
        }
        #endregion
    }
}
