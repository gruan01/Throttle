using System;
using System.Threading.Tasks;

namespace AsNum.Throttle;


/// <summary>
/// 
/// </summary>
public abstract class BaseCounter : IUpdate, IDisposable
{

    /// <summary>
    /// 
    /// </summary>
    internal event EventHandler<EventArgs>? OnReset;

    /// <summary>
    /// 
    /// </summary>
    protected string ThrottleName { get; private set; } = "";

    /// <summary>
    /// 
    /// </summary>
    protected string ThrottleID { get; private set; } = "";

    /// <summary>
    /// 
    /// </summary>
    protected ILogger? Logger { get; private set; }

    /// <summary>
    /// 
    /// </summary>
    public TimeSpan Period { get; private set; }

    /// <summary>
    /// 
    /// </summary>
    public TimeSpan? LockTimeout { get; private set; }

    /// <summary>
    /// 
    /// </summary>
    public int Frequency { get; private set; }


    /// <summary>
    /// 批量占用数,用于减少 Redis 等第三方组件的操作.
    /// </summary>
    public abstract int BatchCount { get; }

    /// <summary>
    /// 
    /// </summary>
    public abstract ValueTask<uint> CurrentCount();


    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public abstract Task IncrementCount(uint n);


    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public abstract ValueTask<bool> TryLock();



    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public abstract Task ReleaseLock();


    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public virtual void WaitMoment()
    {
    }

    /// <summary>
    /// 
    /// </summary>
    public virtual void Change()
    {
    }

    /// <summary>
    /// 
    /// </summary>
    protected void ResetFired()
    {
        this.OnReset?.Invoke(this, EventArgs.Empty);
    }


    /// <summary>
    /// 
    /// </summary>
    /// <param name="throttleName"></param>
    /// <param name="throttleID"></param>
    /// <param name="frequency"></param>
    /// <param name="period"></param>
    /// <param name="lockTimeout"></param>
    internal void SetUp(string throttleName, string throttleID, int frequency, TimeSpan period, TimeSpan? lockTimeout, ILogger? logger)
    {
        this.ThrottleName = throttleName;
        this.ThrottleID = throttleID;

        this.Period = period;
        this.Frequency = frequency;


        this.LockTimeout = lockTimeout;

        this.Logger = logger;

        this.Initialize(true);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="period"></param>
    /// <param name="frequency"></param>
    Task IUpdate.Update(TimeSpan period, int frequency)
    {
        //值不一致时， 才重新初始化
        if (this.Period != period || this.Frequency != frequency)
        {
            this.Frequency = frequency;
            this.Period = period;
            this.Initialize(false);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// 
    /// </summary>
    protected abstract void Initialize(bool firstLoaded);



    /// <summary>
    /// 
    /// </summary>
    protected abstract void InnerDispose();


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
    ~BaseCounter()
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
                this.InnerDispose();
            }
            isDisposed = true;
        }
    }
    #endregion
}
