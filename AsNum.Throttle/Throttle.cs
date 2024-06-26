﻿using System;
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
        /// 任务队列
        /// </summary>
        private readonly ConcurrentQueue<Task> tskQueue = new();

        /// <summary>
        /// 用于观察任务队列中是否有数据。
        /// </summary>
        private readonly BlockingCollection<byte> tskBlock = new();

        #endregion



        #region 核心

        /// <summary>
        /// 
        /// </summary>
        public string ThrottleName { get; }

        /// <summary>
        /// 阻塞器
        /// </summary>
#pragma warning disable IDISP008 // Don't assign member with injected and created disposables
        public BaseBlock Blocker { get; }
#pragma warning restore IDISP008 // Don't assign member with injected and created disposables

        /// <summary>
        /// 计数器
        /// </summary>
#pragma warning disable IDISP008 // Don't assign member with injected and created disposables
        public BaseCounter Counter { get; }
#pragma warning restore IDISP008 // Don't assign member with injected and created disposables

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
        private readonly CancellationTokenSource cts = new();

        /// <summary>
        /// 
        /// </summary>
        private readonly CancellationToken token;

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


            this.token = this.cts.Token;

            this.StartProcess();
        }




        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Counter_OnReset(object? sender, EventArgs e)
        {
            this.OnPeriodElapsed?.Invoke(this, new PeriodElapsedEventArgs());
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="task"></param>
        private void Unwrap(Task task)
        {
            Task _tsk;
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
            else
            {
                _tsk = task;
            }

            _tsk.ContinueWith(tt =>
            {
                //RunLoop 循环阻止锁，如果不放到这，就要放到 RunLoop 里去。
                //放到这，会因为线程切换，稍有延迟。
                //this.tskBlock.TryTake(out _);
                try
                {
                    //当任务执行完时, 才能阻止队列的一个空间出来,供下一个任务进入
                    this.Blocker.Release();
                }
                catch (Exception e)
                {
                    this.logger?.Log(null, e);
                }
            });
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="task"></param>
        private void Enqueue(Task task)
        {
            this.Unwrap(task);

            try
            {
                //占用一个空间, 如果空间占满, 会无限期等待,直至有空间释放出来
                this.Blocker.Acquire();
            }
            catch (Exception e)
            {
                this.logger?.Log(null, e);
            }

            //占用一个空间后, 才能将任务插入队列
            this.tskQueue.Enqueue(task);
            //同时给阻塞队列加一个， 使Take 得以执行。
            this.tskBlock.Add(0);
        }


        /// <summary>
        /// 
        /// </summary>
        private void StartProcess()
        {
            Task.Factory.StartNew(async () =>
            {
                await RunLoop();
            }, this.token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="tsk"></param>
        private void StartTask(Task tsk)
        {
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

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private async Task RunLoop()
        {
            while (!token.IsCancellationRequested)
            {

                //tskBlock 用于防止队列中没有数据， 导致的空转，耗费CPU
                // Take 不到， 会一直停留在这里。
                _ = this.tskBlock.Take();

                //var pass = false;
                //本次总共执行了几个 tsk
                var x = 0U;

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
                            //还有多少位置
                            var space = Math.Max(this.Counter.Frequency - currCount, 0);

                            //可以插入几个. 
                            var n = Math.Min(this.Counter.BatchCount, space);

                            ////本次总共执行了几个 tsk
                            //var x = 0U;

                            for (var i = 0; i < n; i++)
                            {
                                if (tskQueue.TryDequeue(out Task? tsk) && tsk != null)
                                {
                                    x++;
                                    this.StartTask(tsk);

                                    //因为上面已经 Take 过了，这里在 Take， 会造成执行一个 Task, 去掉了两个占位符。
                                    //会导致压入的任务,因为被上面的 tskBlock.Take 阻塞，进不到这里，而导致 Task 不被执行。。。
                                    //但是，如果是批量的，就会造成多运转N-1次。。。

                                    ////任务队列减少一个，阻塞队列也要相应的减少一个。
                                    //this.tskBlock.TryTake(out _);
                                }
                                else
                                    break;
                            }
                        }
                    }

                    if (x > 1)
                    {
                        //如果不放到这，需要放到 Unwrap 的 ContinueWith 里去。
                        //大于1时，说明是批量执行了。
                        //从1开始，因为上面已经 Take 过了一个了。
                        for (var i = 1; i < x; i++)
                            this.tskBlock.TryTake(out _);

                        await this.Counter.IncrementCount(x);
                    }
                    else if (x > 0)
                    {
                        //进到这里，说明只执行了一个。
                        await this.Counter.IncrementCount(x);
                    }
                    else
                    {
                        //进到这里，说明没有执行，没获取到锁。
                        //如果没有获得锁, 或者空间不够， 还要把 阻塞数 还回去。
                        //因为是无上限的 BlockingCollection, 所以这里不会被阻塞
                        this.tskBlock.Add(0);
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

                this.Counter.WaitMoment();
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
                        this.semaphoreSlim.Wait();
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
                        this.semaphoreSlim.Wait();
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
                        this.semaphoreSlim.Wait();
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
                        this.semaphoreSlim.Wait();
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
        public Task<T> Execute<T>(Func<T> func, CancellationToken cancellation = default, TaskCreationOptions creationOptions = TaskCreationOptions.None)
        {
            var t = new Task<T>(this.Wrap(func), cancellation, creationOptions);
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
        public Task<T> Execute<T>(Func<object?, T> func, object? state, CancellationToken cancellation = default, TaskCreationOptions creationOptions = TaskCreationOptions.None)
        {
            var t = new Task<T>(this.Wrap(func), state, cancellation, creationOptions);
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
            var t = new WrapFuncTask<T>(this.Wrap(func), cancellation, creationOptions);
            this.Enqueue(t);
            //return t.Result;
            return t.Unwrap();
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
        public Task<T> Execute<T>(Func<object?, Task<T>> func, object? state, CancellationToken cancellation = default, TaskCreationOptions creationOptions = TaskCreationOptions.None)
        {
            var t = new WrapFuncTask<T>(this.Wrap(func), state, cancellation, creationOptions);
            this.Enqueue(t);
            //return t.Result;
            return t.Unwrap();
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
            var t = new Task(this.Wrap(act), cancellation, creationOptions);
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
        public Task Execute<T>(Action<object?> act, object? state, CancellationToken cancellation = default, TaskCreationOptions creationOptions = TaskCreationOptions.None)
        {
            var t = new Task(this.Wrap(act), state, cancellation, creationOptions);
            this.Enqueue(t);
            return t;
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
                    this.cts.Cancel();

                    if (this.Blocker != null && this.Blocker is IAutoDispose)
                    {
                        this.Blocker.Dispose();
                    }

                    if (this.Counter != null && this.Counter is IAutoDispose)
                    {
                        this.Counter.OnReset -= Counter_OnReset;
                        this.Counter.Dispose();
                    }

                    this.semaphoreSlim?.Dispose();
                    this.tskBlock?.Dispose();
                    this.cts.Dispose();
                }
                isDisposed = true;
            }
        }
        #endregion
    }
}
