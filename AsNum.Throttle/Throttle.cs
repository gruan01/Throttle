﻿using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;


namespace AsNum.Throttle;

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
    /// 用 tskQueue 来判断是不是为空，消耗CPU
    /// </summary>
    private volatile int tskCount = 0;

    ///// <summary>
    ///// 
    ///// </summary>
    //private readonly ManualResetEventSlim mres = new(false);
    #endregion



    #region 核心

    /// <summary>
    /// 
    /// </summary>
    public string ThrottleName { get; }


#pragma warning disable IDISP008 // Don't assign member with injected and created disposables
    /// <summary>
    /// 阻塞器
    /// </summary>
    public BaseBlock Blocker { get; }


    /// <summary>
    /// 计数器
    /// </summary>

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
    private readonly bool tasklongRunningOption;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="period">周期</param>
    /// <param name="frequency">周期内最大允许执行次数</param>
    /// <param name="block"></param>
    /// <param name="counter"></param>
    /// <param name="lockTimeout">避免因为客户端失去连接而引起的死锁</param>
    /// <param name="concurrentCount">并发数, 废弃，不在使用</param>
    /// <param name="isSelectMode">是否是选择模式，选择模式下，不需要走 RunLoop, 用以节省CPU</param>
    /// <param name="tasklongRunningOption">是否使用 TaskCreationOptions.LongRunning</param>
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
                    int? concurrentCount = null,
                    bool isSelectMode = false,
                    bool tasklongRunningOption = true
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

        this.ThrottleName = throttleName;

        var throttleID = ToMD5(Guid.NewGuid().ToString("N"));

        this.logger = logger;

        this.Blocker = block ?? new DefaultBlock();
        this.Blocker.Setup(frequency, lockTimeout);


        this.Counter = counter ?? new DefaultCounter();
        this.Counter.SetUp(throttleName, throttleID, frequency, period, lockTimeout, logger);
        this.Counter.OnReset += Counter_OnReset;

        this.Updater = updater ?? new DefaultCfgUpater();
        this.Updater.Subscribe(this.Counter);
        this.Updater.Subscribe(this.Blocker);
        this.Updater.SetUp(throttleName, throttleID, period, frequency, logger);


        this.token = this.cts.Token;

        this.tasklongRunningOption = tasklongRunningOption;

        if (!isSelectMode)
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
        var _tsk = task is Task<Task> tt
                        ? tt.Unwrap()
                        : task;

        _tsk.ContinueWith(tt =>
        {
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
    private async Task Enqueue(Task task)
    {
        this.Unwrap(task);

        try
        {
            //占用一个空间, 如果空间占满, 会无限期等待,直至有空间释放出来
            await this.Blocker.Acquire();
        }
        catch (Exception e)
        {
            this.logger?.Log(null, e);
        }

        //占用一个空间后, 才能将任务插入队列
        this.tskQueue.Enqueue(task);
        Interlocked.Increment(ref this.tskCount);
        //this.mres.Set();
    }


    /// <summary>
    /// 
    /// </summary>
    private void StartProcess()
    {
        if (this.tasklongRunningOption)
            Task.Factory.StartNew(async () =>
            {
                await RunLoop();
            }, this.token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        else
            Task.Run(async () =>
            {
                await RunLoop();
            });
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
        this.Counter.Change();

        //如果在1秒内没有新 task 进来，就 Delay ，释放CPU (SpinUntil 耗CPU)。
        var d = 0;
        while (!token.IsCancellationRequested)
        {
            //this.mres.Wait(token);
            if (!SpinWait.SpinUntil(() => tskCount > 0, 10))
            {
                d = Math.Min(10, d + 1);
                await Task.Delay(d * 100);
                continue;
            }
            d = 0;

            try
            {
                //先锁, 在获取计数,
                //如果先获取计数, 在锁, 会造成超频的情况.
                var lockSucc = await this.Counter.TryLock();
                if (lockSucc)
                {
                    //当前计数
                    var currCount = await this.Counter.CurrentCount();
                    if (currCount < this.Counter.Frequency)
                    {
                        //还有多少位置
                        var space = Math.Max(this.Counter.Frequency - currCount, 0);

                        //可以插入几个. 
                        var n = Math.Min(this.Counter.BatchCount, space);

                        //本次总共执行了几个 tsk
                        var x = 0U;

                        for (var i = 0; i < n; i++)
                        {
                            if (tskQueue.TryDequeue(out Task? tsk) && tsk != null)
                            {
                                x++;
                                this.StartTask(tsk);
                            }
                            else
                            {
                                break;
                            }

                        }

                        if (x > 0)
                        {
                            await this.Counter.IncrementCount(x);
                            this.Counter.Change();
                            Interlocked.Add(ref this.tskCount, (int)-x);
                        }
                    }

                    await this.Counter.ReleaseLock();
                }
            }
            catch (Exception e)
            {
                this.logger?.Log(null, e);
            }

            await this.Counter.WaitMoment();

            //if (!SpinWait.SpinUntil(() => this.tskCount > 0, 10))
            //{
            //    this.mres.Reset();
            //}
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
    public async Task<T> Execute<T>(Func<T> func, TaskCreationOptions creationOptions = TaskCreationOptions.None, CancellationToken cancellation = default)
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
    public async Task<T> Execute<T>(Func<object?, T> func, object? state, TaskCreationOptions creationOptions = TaskCreationOptions.None, CancellationToken cancellation = default)
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
    public async Task<T> Execute<T>(Func<Task<T>> func, TaskCreationOptions creationOptions = TaskCreationOptions.None, CancellationToken cancellation = default)
    {
        //var t = new WrapFuncTask<T>(func, cancellation, creationOptions);
        var t = new Task<Task<T>>(func, cancellation, creationOptions);
        await this.Enqueue(t);
        //return t.Result;
        return await t.Unwrap();
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
    public async Task<T> Execute<T>(Func<object?, Task<T>> func, object? state, TaskCreationOptions creationOptions = TaskCreationOptions.None, CancellationToken cancellation = default)
    {
        //var t = new WrapFuncTask<T>(func, state, cancellation, creationOptions);
        var t = new Task<Task<T>>(func, state, cancellation, creationOptions);
        await this.Enqueue(t);
        //return t.Result;
        return await t.Unwrap();
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
    public async Task Execute(Action act, TaskCreationOptions creationOptions = TaskCreationOptions.None, CancellationToken cancellation = default)
    {
        var t = new Task(act, cancellation, creationOptions);
        await this.Enqueue(t);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="act"></param>
    /// <param name="state">要执行的动作</param>
    /// <param name="cancellation">动作的参数</param>
    /// <param name="creationOptions"></param>
    /// <returns></returns>
    public async Task Execute<T>(Action<object?> act, object? state, TaskCreationOptions creationOptions = TaskCreationOptions.None, CancellationToken cancellation = default)
    {
        var t = new Task(act, state, cancellation, creationOptions);
        await this.Enqueue(t);
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
            return true;
        }
    }

    #endregion


    /// <summary>
    /// 
    /// </summary>
    /// <param name="input"></param>
    /// <returns></returns>
    private static string ToMD5(string input)
    {
        if (string.IsNullOrEmpty(input))
            return "";

        var bs = MD5.HashData(Encoding.UTF8.GetBytes(input));
        var str = Convert.ToHexString(bs, 4, 8);
        return str;
    }


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

                this.cts.Dispose();
                //this.mres.Dispose();
            }
            isDisposed = true;
        }
    }
    #endregion
}
