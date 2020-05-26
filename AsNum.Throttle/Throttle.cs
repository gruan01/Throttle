using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
namespace AsNum.Throttle
{
    /// <summary>
    /// 
    /// </summary>
    public class Throttle : IDisposable
    {

        #region 参数
        /// <summary>
        /// 当计数周期重置时触发
        /// </summary>
        public event EventHandler<PeriodElapsedEventArgs> OnPeriodElapsed;


        /// <summary>
        /// 当前 ThrottleID 的 ID, 随机分配.
        /// </summary>
        public string ThrottleID { get; }

        /// <summary>
        /// 
        /// </summary>
        public string ThrottleName { get; }


        /// <summary>
        /// 每个周期内,允许多少次调用
        /// </summary>
        public int MaxCountPerPeriod { get; }


        /// <summary>
        /// 周期
        /// </summary>
        public TimeSpan Period { get; }


        /// <summary>
        /// 
        /// </summary>
        private bool inProcess;

        /// <summary>
        /// 
        /// </summary>
        private readonly object lockObj = new object();


        /// <summary>
        /// 任务队列
        /// </summary>
        private readonly ConcurrentQueue<Task> tsks = new ConcurrentQueue<Task>();

        #endregion



        #region 核心
        /// <summary>
        /// 
        /// </summary>
        private readonly BaseBlock block;

        /// <summary>
        /// 计数器
        /// </summary>
        private readonly BaseCounter counter;


        /// <summary>
        /// 性能计数器
        /// </summary>
        private readonly BasePerformanceCounter performanceCounter;

        #endregion


        /// <summary>
        /// 
        /// </summary>
        /// <param name="period">周期</param>
        /// <param name="maxCountPerPeriod">周期内最大允许执行次数</param>
        /// <param name="block"></param>
        /// <param name="counter"></param>
        /// <param name="performanceCounter"></param>
        /// <param name="blockTimeout"></param>
        /// <param name="throttleName">应该是一个唯一的字符串</param>
        public Throttle(string throttleName,
                        TimeSpan period,
                        int maxCountPerPeriod,
                        BaseBlock block,
                        BaseCounter counter,
                        BasePerformanceCounter performanceCounter = null,
                        TimeSpan? blockTimeout = null)
        {
            if (string.IsNullOrWhiteSpace(throttleName))
            {
                throw new ArgumentException("message", nameof(throttleName));
            }

            if (period <= TimeSpan.Zero)
                throw new ArgumentException($"{nameof(period)} 无效");

            if (maxCountPerPeriod <= 0)
                throw new ArgumentException($"{nameof(maxCountPerPeriod)} 无效");

            this.ThrottleName = throttleName;
            this.ThrottleID = Guid.NewGuid().ToString();


            this.MaxCountPerPeriod = maxCountPerPeriod;
            this.Period = period;


            this.block = block ?? throw new ArgumentNullException(nameof(block));
            this.block.Setup(throttleName, this.ThrottleID, maxCountPerPeriod, period, blockTimeout);


            this.counter = counter ?? throw new ArgumentNullException(nameof(counter));
            this.counter.SetUp(throttleName, this.Period);
            this.counter.OnReset += Counter_OnReset;

            this.performanceCounter = performanceCounter;
            this.performanceCounter?.SetUp(throttleName);

            this.TryProcessQueue();
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="period"></param>
        /// <param name="maxCountPerPeriod"></param>
        /// <param name="blockTimeout"></param>
        /// <param name="throttleName">应该是一个唯一的字符串</param>
        public Throttle(string throttleName, TimeSpan period, int maxCountPerPeriod, TimeSpan? blockTimeout = null)
            : this(
                  throttleName,
                  period,
                  maxCountPerPeriod,
                  new DefaultBlock(),
                  new DefaultCounter(),
                  blockTimeout: blockTimeout
                  )
        {
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Counter_OnReset(object sender, EventArgs e)
        {
            this.OnPeriodElapsed?.Invoke(this, new PeriodElapsedEventArgs());
            this.TryProcessQueue();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="task"></param>
        /// <param name="tag"></param>
        private void Unwrap(Task task, string tag)
        {
            var _tsk = task;
            // 这里对应的是 Throttle.Execute(Action)
            if (task is Task<Task> t)
            {
                //如果 task 是 task 的嵌套, 需要先解包装 
                _tsk = t.Unwrap();
            }
            //Task<Task<T>> 无法通过 is 来判断, 这里用 IUnwrap 来包装一下
            else if (task is IUnwrap tt)
            {
                _tsk = tt.GetUnwrapped();
            }

            _tsk.ContinueWith(async tt =>
            {
                //当任务执行完时, 才能阻止队列的一个空间出来,供下一个任务进入
                await this.block.Release(tag);
                this.performanceCounter?.DecrementQueue();
            });
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="task"></param>
        private async Task Enqueue(Task task)
        {
            //用于标识 Task
            //var tag = $"{this.ThrottleName}#{this.ThrottleID}#{task.Id}";
            var tag = task.Id.ToString();

            this.Unwrap(task, tag);

            //占用一个空间, 如果空间占满, 会无限期等待,直至有空间释放出来
            await this.block.Acquire(tag);
            //占用一个空间后, 才能将任务插入队列
            this.tsks.Enqueue(task);

            this.performanceCounter?.IncrementQueue();

            await Task.Yield();
            //尝试执行任务.因为初始状态下, 任务队列是空的, while 循环已退出.
            this.TryProcessQueue();
        }




        /// <summary>
        /// 
        /// </summary>
        private void TryProcessQueue()
        {
            if (this.inProcess)
                return;

            lock (this.lockObj)
            {
                if (this.inProcess)
                    return;

                this.inProcess = true;
                try
                {
                    ProcessQueue();
                }
                finally
                {
                    inProcess = false;
                }
            }

        }


        /// <summary>
        /// 
        /// </summary>
        private void ProcessQueue()
        {
            //当 当前计数 小于周期内最大允许的任务数
            //且 任务队列中有任务可以取出来
            //
            //如果是分布式的, 多个进程同时获取到 CurrentCount 小于 MaxCountPerPeriod
            //为了保证不超过 MaxCountPerPeriod 的限定, 就需要保证每个进程里已压入的任务总数小于 MaxCountPerPeriod
            //所以, 分布式的, 要保证同一时间内,所有节点压入的任务总数要小于 MaxCountPerPeriod
            //所以, 分布式的 Block 不能用 DefaultBlock 代替.
            while ((this.counter.CurrentCount < this.MaxCountPerPeriod)
                && tsks.TryDequeue(out Task tsk))
            {
                this.counter.IncrementCount();
                //执行任务
                tsk.Start();
            }
        }


        #region execute

        #region Func<T> / Func<object, T>
        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="func"></param>
        /// <param name="cancellation"></param>
        /// <param name="creationOptions"></param>
        /// <returns></returns>
        public async Task<T> Execute<T>(Func<T> func, CancellationToken cancellation = default, TaskCreationOptions creationOptions = TaskCreationOptions.None)
        {
            var t = new Task<T>(func, cancellation, creationOptions);
            await this.Enqueue(t);
            return await t;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="func">要执行的动作</param>
        /// <param name="state">动作的参数</param>
        /// <param name="cancellation"></param>
        /// <param name="creationOptions"></param>
        /// <returns></returns>
        public async Task<T> Execute<T>(Func<object, T> func, object state, CancellationToken cancellation = default, TaskCreationOptions creationOptions = TaskCreationOptions.None)
        {
            var t = new Task<T>(func, state, cancellation, creationOptions);
            await this.Enqueue(t);
            return await t;
        }
        #endregion


        #region Func<Task<T>> / Func<object, Task<T>>
        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="func"></param>
        /// <param name="cancellation"></param>
        /// <param name="creationOptions"></param>
        /// <returns></returns>
        public async Task<T> Execute<T>(Func<Task<T>> func, CancellationToken cancellation = default, TaskCreationOptions creationOptions = TaskCreationOptions.None)
        {
            var t = new WrapFuncTask<T>(func, cancellation, creationOptions);
            await this.Enqueue(t);
            return await t.Result;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="func">要执行的动作</param>
        /// <param name="state">动作的参数</param>
        /// <param name="cancellation"></param>
        /// <param name="creationOptions"></param>
        /// <returns></returns>
        public async Task<T> Execute<T>(Func<object, Task<T>> func, object state, CancellationToken cancellation = default, TaskCreationOptions creationOptions = TaskCreationOptions.None)
        {
            var t = new WrapFuncTask<T>(func, state, cancellation, creationOptions);
            await this.Enqueue(t);
            return await t.Result;
        }
        #endregion


        #region Action
        /// <summary>
        /// 
        /// </summary>
        /// <param name="act"></param>
        /// <param name="cancellation"></param>
        /// <param name="creationOptions"></param>
        /// <returns></returns>
        public async Task Execute(Action act, CancellationToken cancellation = default, TaskCreationOptions creationOptions = TaskCreationOptions.None)
        {
            var t = new Task(act, cancellation, creationOptions);
            await this.Enqueue(t);
            await t;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="act"></param>
        /// <param name="state">要执行的动作</param>
        /// <param name="cancellation">动作的参数</param>
        /// <param name="creationOptions"></param>
        /// <returns></returns>
        public async Task Execute<T>(Action<object> act, object state, CancellationToken cancellation = default, TaskCreationOptions creationOptions = TaskCreationOptions.None)
        {
            var t = new Task(act, state, cancellation, creationOptions);
            await this.Enqueue(t);
            await t;
        }

        #endregion

        #endregion


        #region dispose
        /// <summary>
        /// 
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }


        /// <summary>
        /// 
        /// </summary>
        ~Throttle()
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
                    if (this.block != null && this.block is IAutoDispose)
                    {
                        this.block.Dispose();
                    }

                    if (this.counter != null && this.counter is IAutoDispose)
                    {
                        this.counter.Dispose();
                    }
                }
                isDisposed = true;
            }
        }
        #endregion
    }
}
