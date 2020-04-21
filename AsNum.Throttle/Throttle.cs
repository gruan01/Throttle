using System;
using System.Collections.Concurrent;
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


        /// <summary>
        /// 用于 周期性的 重置计数
        /// </summary>
        private readonly System.Timers.Timer timer;

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


        #region 数据
        ///// <summary>
        ///// 总执行条数
        ///// </summary>
        //private long _totalExecuted = 0;

        ///// <summary>
        ///// 总执行条数
        ///// </summary>
        //public long TotalExecuted => this._totalExecuted;


        ///// <summary>
        ///// 总共压入队列数
        ///// </summary>
        //private long _totalEnqueued = 0;

        ///// <summary>
        ///// 总共压入队列数
        ///// </summary>
        //public long TotalEnqueued => this._totalEnqueued;


        ///// <summary>
        ///// 当前周期内, 可用空间 (阴止队列可插入数: block.BoundedCapacity - block.Count)
        ///// </summary>
        //public int FreeSpace => this.MaxCountPerPeriod - this.block?.Length ?? 0;


        ///// <summary>
        ///// 当前计数
        ///// </summary>
        //public int CurrentCount => this.counter.CurrentCount;


        ///// <summary>
        ///// 当前任务数
        ///// </summary>
        //public int TaskCount => this.tsks.Count;


        ///// <summary>
        ///// 周期数
        ///// </summary>
        //public long PeriodNumber { get; private set; }
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


            this.block = block;
            this.block.Setup(throttleName, maxCountPerPeriod, period, blockTimeout);


            this.counter = counter;
            this.counter.SetUp(throttleName);

            this.performanceCounter = performanceCounter;
            this.performanceCounter.SetUp(throttleName);


            this.timer = new System.Timers.Timer(period.TotalMilliseconds)
            {
                AutoReset = true
            };
            this.timer.Elapsed += Timer_Elapsed;
            this.timer.Start();

            //this.TryProcessQueue();
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
                  new DefaultBlock() { AutoDispose = true },
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
        private void Timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            var n = this.counter.ResetCount();
            //this.PeriodNumber++;
            this.OnPeriodElapsed?.Invoke(this, new PeriodElapsedEventArgs()
            {
                //Statistic = new ThrottleStatistic()
                //{
                //    CurrentExecutedCount = n,
                //    CurrentFreeSpace = this.FreeSpace,
                //    CurrentTaskCount = this.tsks.Count,
                //    TotalEnqueued = this.TotalEnqueued,
                //    TotalExecuted = this.TotalExecuted,
                //    PeriodNumber = this.PeriodNumber
                //}
            });
            this.TryProcessQueue();

        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="task"></param>
        private void Enqueue(Task task)
        {
            //用于标识 Task
            var tag = $"{this.ThrottleName}#{this.ThrottleID}#{task.Id}";

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

            _tsk.ContinueWith(tt =>
            {
                //当任务执行完时, 才能阻止队列的一个空间出来,供下一个任务进入
                this.block.Release(tag);
                this.performanceCounter?.DecrementQueue();
                this.performanceCounter?.AddExecuted();

                //Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff}..................Release");
                //Interlocked.Increment(ref this._totalExecuted);

            }, TaskContinuationOptions.AttachedToParent);


            //占用一个空间, 如果空间占满, 会无限期等待,直至有空间释放出来
            this.block.Acquire(tag);
            this.performanceCounter?.IncrementQueue();

            //Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff}..................Add");

            //占用一个空间后, 才能将任务插入队列
            this.tsks.Enqueue(task);
            //Interlocked.Increment(ref this._totalEnqueued);

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
                //Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff}..................TryProcess {this.FreeSpace}");
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
            while ((this.counter.CurrentCount < this.MaxCountPerPeriod)
                && tsks.TryDequeue(out Task tsk))
            {
                //Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff}..................Dequeue");
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
        public Task<T> Execute<T>(Func<T> func, CancellationToken cancellation = default, TaskCreationOptions creationOptions = TaskCreationOptions.None)
        {
            var t = new Task<T>(func, cancellation, creationOptions);
            this.Enqueue(t);
            return t;
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
        public Task<T> Execute<T>(Func<object, T> func, object state, CancellationToken cancellation = default, TaskCreationOptions creationOptions = TaskCreationOptions.None)
        {
            var t = new Task<T>(func, state, cancellation, creationOptions);
            this.Enqueue(t);
            return t;
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
        public Task<T> Execute<T>(Func<Task<T>> func, CancellationToken cancellation = default, TaskCreationOptions creationOptions = TaskCreationOptions.None)
        {
            var t = new WrapFuncTask<T>(func, cancellation, creationOptions);
            this.Enqueue(t);
            return t.Result;
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
        public Task<T> Execute<T>(Func<object, Task<T>> func, object state, CancellationToken cancellation = default, TaskCreationOptions creationOptions = TaskCreationOptions.None)
        {
            var t = new WrapFuncTask<T>(func, state, cancellation, creationOptions);
            this.Enqueue(t);
            return t.Result;
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
        public Task Execute(Action act, CancellationToken cancellation = default, TaskCreationOptions creationOptions = TaskCreationOptions.None)
        {
            var t = new Task(act, cancellation, creationOptions);
            this.Enqueue(t);
            return t;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="act"></param>
        /// <param name="state">要执行的动作</param>
        /// <param name="cancellation">动作的参数</param>
        /// <param name="creationOptions"></param>
        /// <returns></returns>
        public Task Execute<T>(Action<object> act, object state, CancellationToken cancellation = default, TaskCreationOptions creationOptions = TaskCreationOptions.None)
        {
            var t = new Task(act, state, cancellation, creationOptions);
            this.Enqueue(t);
            return t;
        }

        #endregion

        #endregion


        #region
        ///// <summary>
        ///// 
        ///// </summary>
        ///// <returns></returns>
        //public ThrottleStatistic GetStatistic()
        //{
        //    return new ThrottleStatistic()
        //    {
        //        CurrentExecutedCount = this.CurrentCount,
        //        CurrentFreeSpace = this.FreeSpace,
        //        CurrentTaskCount = this.tsks.Count,
        //        TotalEnqueued = this.TotalEnqueued,
        //        TotalExecuted = this.TotalExecuted,
        //        PeriodNumber = this.PeriodNumber
        //    };
        //}
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

                    if (this.timer != null)
                    {
                        this.timer.Dispose();
                    }

                    if (this.block != null && this.block.AutoDispose && this.block is IDisposable disposableObj)
                    {
                        disposableObj.Dispose();
                    }
                }
                isDisposed = true;
            }
        }
        #endregion
    }
}
