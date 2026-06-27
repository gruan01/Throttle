using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AsNum.Throttle;


/// <summary>
/// 
/// </summary>
public abstract class BaseCfgUpdater
{
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
    public TimeSpan Period { get; private set; }

    /// <summary>
    /// 
    /// </summary>
    public int Frequency { get; private set; }

    /// <summary>
    /// 
    /// </summary>
    protected ILogger? Logger { get; private set; }


    /// <summary>
    /// 
    /// </summary>
    private readonly List<IUpdate> subscribers = [];

    /// <summary>
    /// 通知更新
    /// </summary>
    protected void NotifyChange(TimeSpan newPeriod, int newFrequency, TimeSpan oldPeriod, int oldFrequency, string tag)
    {
        if (newPeriod != oldPeriod || newFrequency != oldFrequency)
        {
            this.Logger?.Log($"Update({tag}) {this.ThrottleName}({this.ThrottleID}) [{oldPeriod},{oldFrequency}]->[{newPeriod},{newFrequency}]", null);

            //更新订阅者
            foreach (var s in this.subscribers)
                s.Update(newPeriod, newFrequency);

            //更新自己的, 没有放到 Update 中， 是因为从 Redis 订阅消息进来的， 不会不会执行 Update 方法。
            this.Period = newPeriod;
            this.Frequency = newFrequency;
        }
    }


    /// <summary>
    /// 
    /// </summary>
    /// <param name="update"></param>
    internal void Subscribe(IUpdate update)
    {
        this.subscribers.Add(update);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    internal void SetUp(string throttleName, string throttleID, TimeSpan period, int frequency, ILogger? logger)
    {
        this.Logger = logger;
        this.ThrottleName = throttleName;
        this.ThrottleID = throttleID;
        var (exPeriod, exFrequency) = this.Initialize(period, frequency);
        this.Period = exPeriod;
        this.Frequency = exFrequency;
    }

    /// <summary>
    /// 同步初始化（仅赋值属性，不做 I/O）。
    /// 子类如需异步初始化（如 Redis），应重写 <see cref="InitializeAsync"/>。
    /// </summary>
    protected virtual (TimeSpan exPeriod, int exFrquency) Initialize(TimeSpan period, int frequency)
    {
        return (period, frequency);
    }

    /// <summary>
    /// 异步初始化。默认无操作。
    /// 需要异步 I/O 的子类（如 RedisCfgUpdater）应重写此方法。
    /// </summary>
    public virtual Task InitializeAsync()
    {
        return Task.CompletedTask;
    }


    /// <summary>
    /// 用于外部更新配置的接口.
    /// </summary>
    /// <param name="newPeriod"></param>
    /// <param name="newFrequency"></param>
    public async Task Update(TimeSpan newPeriod, int newFrequency)
    {
        if (newPeriod == this.Period && newFrequency == this.Frequency)
            return;

        // 确保异步初始化已完成（如 Redis 订阅），再执行保存和跨进程通知
        await this.InitializeAsync();

        var oldPeriod = this.Period;
        var oldFrequency = this.Frequency;

        await this.Save(newPeriod, newFrequency);
        this.NotifyChange(newPeriod, newFrequency, oldPeriod, oldFrequency, "Update");
    }


    /// <summary>
    /// 接收配置更改, 并保存
    /// </summary>
    protected abstract Task Save(TimeSpan newPeriod, int newFrequency);
}
