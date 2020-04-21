using System;
using System.Threading;

namespace AsNum.Throttle.CrossProcess
{
    /// <summary>
    /// 使用 Semaphore 实现 跨进程的 Block
    /// </summary>
    public class CrossProcessBlock : BaseBlock
    {

        ///// <summary>
        ///// 
        ///// </summary>
        //private int _length = 0;

        ///// <summary>
        ///// 
        ///// </summary>
        //public override int Length => this._length;


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
        public override void Acquire(string tag)
        {
            if (this.BlockTimeout.HasValue)
                this.semaphore.WaitOne(this.BlockTimeout.Value);
            else
                this.semaphore.WaitOne();

            //Interlocked.Increment(ref this._length);
        }



        /// <summary>
        /// 
        /// </summary>
        public override void Release(string tag)
        {
            //Interlocked.Decrement(ref this._length);

            this.semaphore.Release();
        }

        /// <summary>
        /// 
        /// </summary>
        protected override void InnerDispose()
        {
            this.semaphore.Close();
            this.semaphore.Dispose();
        }

    }
}
