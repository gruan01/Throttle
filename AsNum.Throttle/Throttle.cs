using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
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
        public event EventHandler<PeriodElapsedEventArgs>? OnPeriodElapsed;

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
        public string ThrottleName { get; }

        /// <summary>
        /// 阻塞器
        /// </summary>
        public BaseBlock Blocker { get; }

        /// <summary>
        /// 计数器
        /// </summary>
        public BaseCounter Counter { get; }

        /// <summary>
        /// 配置更新器
        /// </summary>
        public BaseCfgUpdater Updater { get; }

        #endregion

        /// <summary>
        /// 
        /// </summary>
        private readonly ILogger? logger;


        /// <summary>
        /// 用于控制并发数
        /// </summary>
        private readonly SemaphoreSlim? semaphoreSlim = null;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="period">周期</param>
        /// <param name="frequency">周期内最大允许执行次数</param>
        /// <param name="block"></param>
        /// <param name="counter"></param>
        /// <param name="lockTimeout">避免因为客户端失去连接而引起的死锁</param>
        /// <param name="concurrentCount">并发数</param>
        /// <param name="logger"></param>
        /// <param name="updater">用于更新周期/频率</param>
        /// <param name="throttleName">应该是一个唯一的字符串, 这个参数做为 Redis 的 key 的一部份, 因此要符合 redis key 的规则</param>
        public Throttle(string throttleName,
                        TimeSpan period,
                        int frequency,
                        BaseBlock? block = null,
                        BaseCounter? counter = null,
                        BaseCfgUpdater? updater = null,
                        ILogger? logger = null,
                        TimeSpan? lockTimeout = null,
                        int? concurrentCount = null
                        )
        {
            if (string.IsNullOrWhiteSpace(throttleName))
                throw new ArgumentException($"{nameof(throttleName)} 必填");

            if (period <= TimeSpan.Zero)
                throw new ArgumentException($"{nameof(period)} 无效");

            if (frequency <= 0)
                throw new ArgumentException($"{nameof(frequency)} 无效");

            if (concurrentCount.HasValue && concurrentCount <= 0)
                throw new ArgumentException($"{nameof(concurrentCount)} 无效");

            if (concurrentCount.HasValue)
                this.semaphoreSlim = new SemaphoreSlim(concurrentCount.Value, concurrentCount.Value);

            this.ThrottleName = throttleName;

            var throttleID = Guid.NewGuid().ToString("N");

            this.logger = logger;

            this.Blocker = block ?? new DefaultBlock();
            this.Blocker.Setup(frequency, lockTimeout);


            this.Counter = counter ?? new DefaultCounter();
            this.Counter.SetUp(throttleName, throttleID, frequency, period, lockTimeout);
            this.Counter.OnReset += Counter_OnReset;

            this.Updater = updater ?? new DefaultCfgUpater();
            this.Updater.Subscribe(this.Counter);
            this.Updater.Subscribe(this.Blocker);
            this.Updater.SetUp(throttleName, throttleID, period, frequency, logger);
        }




        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Counter_OnReset(object? sender, EventArgs e)
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
                    await this.Blocker.Release(tag);
                }
                catch (Exception e)
                {
                    //this.OnMessage?.Invoke(this, new MsgEventArgs() { Ex = e });
                    this.logger?.Log(null, e);
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
                await this.Blocker.Acquire(tag);
            }
            catch (Exception e)
            {
                //this.OnMessage?.Invoke(this, new MsgEventArgs() { Ex = e });
                this.logger?.Log(null, e);
            }

            //占用一个空间后, 才能将任务插入队列
            this.tsks.Enqueue(task);


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
                await this.RunLoop();
                this.inProcess = false;
            }, TaskCreationOptions.LongRunning | TaskCreationOptions.PreferFairness);
        }



        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private async Task RunLoop()
        {
            while (!this.tsks.IsEmpty)
            {
                await this.Counter.WaitMoment();

                try
                {
                    //先锁, 在获取计数,
                    //如果先获取计数, 在锁, 会造成超频的情况.
                    if (await this.Counter.TryLock())
                    {
                        //当前计数
                        var currCount = await this.Counter.CurrentCount();
                        if (currCount < this.Counter.Frequency)
                        {
                            //if (await this.counter.TryLock())
                            {

                                //还有多少位置
                                var space = Math.Max(this.Counter.Frequency - currCount, 0);

                                //可以插入几个
                                var n = Math.Min(this.Counter.BatchCount, space);

                                var x = 0;
                                for (var i = 0; i < n; i++)
                                {
                                    if (tsks.TryDequeue(out Task? tsk) && tsk != null)
                                    {
                                        x++;
                                        try
                                        {
                                            //被取消的任务,不能在 start
                                            if (!tsk.IsCanceled && !tsk.IsCompleted)
                                                tsk.Start();
                                        }
                                        catch (Exception e)
                                        {
                                            this.logger?.Log(null, e);
                                        }
                                    }
                                    else
                                        break;
                                }

                                if (x > 0)
                                    await this.Counter.IncrementCount(x);

                                //await this.counter.ReleaseLock();
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    this.logger?.Log(null, e);
                }
                finally
                {
                    try
                    {
                        await this.Counter.ReleaseLock();
                    }
                    catch (Exception e)
                    {
                        //RedisCounter 在 ReleaseLock 时, 可能会因为连接超时而报错.
                        this.logger?.Log(null, e);
                    }
                }
            }
        }

        #region execute


        #region Wrap
        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="org"></param>
        /// <returns></returns>
        private Func<object?, T> Wrap<T>(Func<object?, T> org)
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
        public async Task<T> Execute<T>(Func<object?, T> func, object? state, CancellationToken cancellation = default, TaskCreationOptions creationOptions = TaskCreationOptions.None)
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
        public async Task<T> Execute<T>(Func<object?, Task<T>> func, object? state, CancellationToken cancellation = default, TaskCreationOptions creationOptions = TaskCreationOptions.None)
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
        public async Task Execute<T>(Action<object?> act, object? state, CancellationToken cancellation = default, TaskCreationOptions creationOptions = TaskCreationOptions.None)
        {
            var t = new Task(this.Wrap(act), state, cancellation, creationOptions);
            await this.Enqueue(t);
            await t;
        }

        #endregion

        #endregion



        #region Select

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public async Task<bool> Select()
        {
            try
            {
                await this.Counter.TryLock();
                //当前计数
                var currCount = await this.Counter.CurrentCount();
                var flag = currCount < this.Counter.Frequency;
                if (flag)
                {
                    await this.Counter.IncrementCount(1);
                }
                return flag;
            }
            catch (Exception e)
            {
                this.logger?.Log(null, e);
            }
            finally
            {
                await this.Counter.ReleaseLock();
            }

            return true;
        }

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
                    if (this.Blocker != null && this.Blocker is IAutoDispose)
                    {
                        this.Blocker.Dispose();
                    }

                    if (this.Counter != null && this.Counter is IAutoDispose)
                    {
                        this.Counter.OnReset -= Counter_OnReset;
                        this.Counter.Dispose();
                    }

                    //if (this.Updater != null && this.Updater is IAutoDispose)
                    //{
                    //    this.Updater.CfgChanged -= Updater_CfgChanged;
                    //}

                    this.semaphoreSlim?.Dispose();
                }
                isDisposed = true;
            }
        }
        #endregion
    }
}
