using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AsNum.Throttle
{
    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    internal class WrapFuncTask<T> : Task<Task<T>>, IUnwrap
    {
        /// <summary>
        /// 
        /// </summary>
        /// <param name="function"></param>
        /// <param name="cancellationToken"></param>
        /// <param name="creationOptions"></param>
        public WrapFuncTask(Func<Task<T>> function, CancellationToken cancellationToken, TaskCreationOptions creationOptions)
            : base(function, cancellationToken, creationOptions)
        {
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="function"></param>
        /// <param name="state"></param>
        /// <param name="cancellationToken"></param>
        /// <param name="creationOptions"></param>
        public WrapFuncTask(Func<object?, Task<T>> function, object? state, CancellationToken cancellationToken, TaskCreationOptions creationOptions)
            : base(function, state, cancellationToken, creationOptions)
        {
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public Task GetUnwrapped()
        {
            return this.Unwrap();
        }
    }
}
