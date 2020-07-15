using System;
using System.Collections.Concurrent;
using System.Diagnostics;
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
        /// 
        /// </summary>
        public event EventHandler<ErrorEventArgs> OnError;

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


        ///// <summary>
        ///// 性能计数器
        ///// </summary>
        //private readonly BasePerformanceCounter performanceCounter;

        #endregion


        /// <summary>
        /// 
        /// </summary>
        private readonly SemaphoreSlim semaphoreSlim = null;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="period">周期</param>
        /// <param name="maxCountPerPeriod">周期内最大允许执行次数</param>
        /// <param name="block"></param>
        /// <param name="counter"></param>
        /// <param name="lockTimeout">避免因为客户端失去连接而引起的死锁</param>
        /// <param name="concurrentCount">并发数</param>
        /// <param name="throttleName">应该是一个唯一的字符串</param>
        public Throttle(string throttleName,
                        TimeSpan period,
                        int maxCountPerPeriod,
                        BaseBlock block,
                        BaseCounter counter,
                        TimeSpan? lockTimeout = null,
                        int? concurrentCount = null
                        )
        {
            if (string.IsNullOrWhiteSpace(throttleName))
            {
                throw new ArgumentException("message", nameof(throttleName));
            }

            if (period <= TimeSpan.Zero)
                throw new ArgumentException($"{nameof(period)} 无效");

            if (maxCountPerPeriod <= 0)
                throw new ArgumentException($"{nameof(maxCountPerPeriod)} 无效");

            if (concurrentCount.HasValue && concurrentCount <= 0)
                throw new ArgumentException($"{nameof(concurrentCount)} 无效");

            if (concurrentCount.HasValue)
                this.semaphoreSlim = new SemaphoreSlim(concurrentCount.Value, concurrentCount.Value);

            this.ThrottleName = throttleName;
            this.ThrottleID = Guid.NewGuid().ToString();


            this.MaxCountPerPeriod = maxCountPerPeriod;
            this.Period = period;


            this.block = block ?? throw new ArgumentNullException(nameof(block));
            this.block.Setup(throttleName, this.ThrottleID, maxCountPerPeriod, period, lockTimeout);


            this.counter = counter ?? throw new ArgumentNullException(nameof(counter));
            this.counter.SetUp(throttleName, this.ThrottleID, maxCountPerPeriod, period, lockTimeout);
            this.counter.OnReset += Counter_OnReset;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="throttleName"></param>
        /// <param name="period"></param>
        /// <param name="maxCountPerPeriod"></param>
        /// <param name="counter"></param>
        /// <param name="blockTimeout"></param>
        /// <param name="concurrentCount"></param>
        public Throttle(string throttleName,
                        TimeSpan period,
                        int maxCountPerPeriod,
                        BaseCounter counter,
                        TimeSpan? blockTimeout = null,
                        int? concurrentCount = null
                        )
            : this(throttleName,
                  period,
                  maxCountPerPeriod,
                  new DefaultBlock(),
                  counter,
                  blockTimeout,
                  concurrentCount
                  )
        {
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="period"></param>
        /// <param name="maxCountPerPeriod"></param>
        /// <param name="blockTimeout"></param>
        /// <param name="concurrentCount"></param>
        /// <param name="throttleName">应该是一个唯一的字符串</param>
        public Throttle(string throttleName,
            TimeSpan period,
            int maxCountPerPeriod,
            TimeSpan? blockTimeout = null,
            int? concurrentCount = null) : this(
                  throttleName,
                  period,
                  maxCountPerPeriod,
                  new DefaultBlock(),
                  new DefaultCounter(),
                  blockTimeout,
                  concurrentCount
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
            this.ProcessQueue();
            this.OnPeriodElapsed?.Invoke(this, new PeriodElapsedEventArgs());
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
                try
                {
                    //当任务执行完时, 才能阻止队列的一个空间出来,供下一个任务进入
                    await this.block.Release(tag);
                    //this.performanceCounter?.DecrementQueue();
                }
                catch (Exception e)
                {
                    this.OnError?.Invoke(this, new ErrorEventArgs() { Ex = e });
                }
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

            try
            {
                //占用一个空间, 如果空间占满, 会无限期等待,直至有空间释放出来
                await this.block.Acquire(tag);
            }
            catch (Exception e)
            {
                this.OnError?.Invoke(this, new ErrorEventArgs() { Ex = e });
            }

            //占用一个空间后, 才能将任务插入队列
            this.tsks.Enqueue(task);

            //this.performanceCounter?.IncrementQueue();

            //尝试执行任务.因为初始状态下, 任务队列是空的, while 循环已退出.
            this.ProcessQueue();
        }



        /// <summary>
        /// 
        /// </summary>
        private void ProcessQueue()
        {
            if (this.inProcess)
                return;

            lock (lockObj)
            {
                if (this.inProcess)
                    return;

                this.inProcess = true;
            }

            Task.Factory.StartNew(async () =>
            {
                while (!this.tsks.IsEmpty)
                {
                    try
                    {
                        //当前计数
                        var currCount = await this.counter.CurrentCount();
                        if (currCount < this.MaxCountPerPeriod)
                        {
                            if (await this.counter.TryLock())
                            {
                                // currCount = await this.counter.CurrentCount();
                                //还有多少位置
                                var space = Math.Max(this.MaxCountPerPeriod - currCount, 0);
                                //可以插入几个
                                var n = Math.Min(this.counter.BatchCount, space);

                                var x = 0;
                                for (var i = 0; i < n; i++)
                                {
                                    if (tsks.TryDequeue(out Task tsk))
                                    {
                                        x++;
                                        try
                                        {
                                            tsk.Start();
                                        }
                                        catch (Exception e)
                                        {
                                            this.OnError?.Invoke(this, new ErrorEventArgs() { Ex = e });
                                        }
                                    }
                                    else
                                        break;
                                }

                                if (x > 0)
                                    await this.counter.IncrementCount(n);

                                await this.counter.ReleaseLock();
                            }

                        }
                    }
                    catch (Exception e)
                    {
                        this.OnError?.Invoke(this, new ErrorEventArgs() { Ex = e });
                    }
                }

                this.inProcess = false;

            }, TaskCreationOptions.LongRunning | TaskCreationOptions.PreferFairness);
        }

        #region execute


        #region Wrap
        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="org"></param>
        /// <returns></returns>
        private Func<object, T> Wrap<T>(Func<object, T> org)
        {
            if (this.semaphoreSlim != null)
            {
                return (o) =>
                {
                    try
                    {
                        this.semaphoreSlim.WaitAsync();
                        return org.Invoke(o);
                    }
                    finally
                    {
                        this.semaphoreSlim.Release();
                    }
                };
            }
            else
                return org;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="org"></param>
        /// <returns></returns>
        private Func<T> Wrap<T>(Func<T> org)
        {
            if (this.semaphoreSlim != null)
            {
                return () =>
                {
                    try
                    {
                        this.semaphoreSlim.WaitAsync();
                        return org.Invoke();
                    }
                    finally
                    {
                        this.semaphoreSlim.Release();
                    }
                };
            }
            else
                return org;
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="org"></param>
        /// <returns></returns>
        private Action Wrap(Action org)
        {
            if (this.semaphoreSlim != null)
            {
                return () =>
                {
                    try
                    {
                        this.semaphoreSlim.WaitAsync();
                        org.Invoke();
                    }
                    finally
                    {
                        this.semaphoreSlim.Release();
                    }
                };
            }
            else
                return org;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="org"></param>
        /// <returns></returns>
        private Action<T> Wrap<T>(Action<T> org)
        {
            if (this.semaphoreSlim != null)
            {
                return (o) =>
                {
                    try
                    {
                        this.semaphoreSlim.WaitAsync();
                        org.Invoke(o);
                    }
                    finally
                    {
                        this.semaphoreSlim.Release();
                    }
                };
            }
            else
                return org;
        }
        #endregion


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
            var t = new Task<T>(this.Wrap(func), cancellation, creationOptions);
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
            var t = new Task<T>(this.Wrap(func), state, cancellation, creationOptions);
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
            var t = new WrapFuncTask<T>(this.Wrap(func), cancellation, creationOptions);
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
            var t = new WrapFuncTask<T>(this.Wrap(func), state, cancellation, creationOptions);
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
            var t = new Task(this.Wrap(act), cancellation, creationOptions);
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
            var t = new Task(this.Wrap(act), state, cancellation, creationOptions);
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

                    this.semaphoreSlim?.Dispose();
                }
                isDisposed = true;
            }
        }
        #endregion
    }
}
