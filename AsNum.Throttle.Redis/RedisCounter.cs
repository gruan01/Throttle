﻿using StackExchange.Redis;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace AsNum.Throttle.Redis;

/// <summary>
/// 
/// </summary>
/// <remarks>
/// 确保 Redis 的 notify-keyspace-events 中开启了 Ex: 
/// config set notify-keyspace-events Ex
/// </remarks>
public class RedisCounter : BaseCounter
{

    /// <summary>
    /// 
    /// </summary>
    private static readonly RedisChannel KEY_EXPIRED_CHANNEL = new("__keyevent@0__:expired", RedisChannel.PatternMode.Auto);


    /// <summary>
    /// 批量占用数,用于减少 Redis 等第三方组件的操作.
    /// </summary>
    public override int BatchCount { get; }


    /// <summary>
    /// 
    /// </summary>
    private readonly IDatabase db;

    /// <summary>
    /// 
    /// </summary>
    private readonly ISubscriber subscriber;


    /// <summary>
    /// 
    /// </summary>
    private string countKey = "";

    /// <summary>
    /// 
    /// </summary>
    private string lockKey = "";


    /// <summary>
    /// 
    /// </summary>
    private static readonly Random rnd = new();


    /// <summary>
    /// 用于随机等待, 如果不等待, 太消耗CPU.
    /// 即然用到了限频, 而且用到了 RedisCounter 说明是多进程同时在运行, 频率一定不会太高,
    /// 所以随机等待对请求速度影响不大.
    /// </summary>
    private readonly int rndSleepInMS = 5;


    //老板版的 Redis 不支持 expire 的第3个参数
    //从 Redis 版本 7.0.0 开始：添加了选项： NX 、 XX 、 GT和LT 。
    private static readonly string incrByLuaScriptOld = """
        local current
        current = redis.call("INCRBY" , @k, @v)
        if(current <= tonumber(@v))
        then
            redis.call("expire" , @k, @e)
        end
        """;

    //Redis 7 以上版本使用
    private static readonly string incrByLuaScript7 = """
        local current
        current = redis.call("INCRBY" , @k, @v)
        redis.call("expire" , @k, @e, "NX")
        """;

    private readonly LuaScript incrByLuaScript;

    /// <summary>
    /// 
    /// </summary>
    private readonly LoadedLuaScript loadedIncrByLuaScript;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="connection"></param>
    /// <param name="batchCount">批大小; 为保证公平, 这个数字越小越好; 但是为了减少与 Redis 之间的通讯, 这个值越大越好.</param>
    /// /// <param name="rndSleepInMS">用于随机等待, 如果不等待, 太消耗CPU.</param>
    public RedisCounter(ConnectionMultiplexer connection, int batchCount = 1, int rndSleepInMS = 5)
    {
        if (connection == null)
            throw new ArgumentNullException(nameof(connection));

        if (batchCount <= 0)
            throw new ArgumentOutOfRangeException($"{nameof(batchCount)} must greate than 0");

        if (rndSleepInMS <= 0)
            throw new ArgumentOutOfRangeException($"{nameof(rndSleepInMS)} must greate than 0");

        this.db = connection.GetDatabase();
        this.subscriber = connection.GetSubscriber();
        this.BatchCount = batchCount;
        this.rndSleepInMS = rndSleepInMS;

        var server = connection
            .GetEndPoints()
            .Select(x => connection.GetServer(x))
            .First(x => !x.IsReplica);

        var v = server.Version;

        if (v.Major < 7)
            this.incrByLuaScript = LuaScript.Prepare(incrByLuaScriptOld);
        else
            this.incrByLuaScript = LuaScript.Prepare(incrByLuaScript7);

        //https://stackexchange.github.io/StackExchange.Redis/Scripting.html#:~:text=StackExchange.Redis%20handles%20Lua%20script%20caching%20internally.%20It%20automatically
        this.loadedIncrByLuaScript = incrByLuaScript.Load(server, CommandFlags.DemandMaster);
    }


    /// <summary>
    /// 
    /// </summary>
    protected override void Initialize(bool firstLoad)
    {
        if (firstLoad)
        {
            this.countKey = this.ThrottleName.ToCounterCountKey();
            this.lockKey = this.ThrottleName.ToCounterLockKey();

            this.subscriber.Subscribe(KEY_EXPIRED_CHANNEL, (channel, value) =>
            {
                var v = (string?)value;
                //if (value == this.countKey)
                if (string.Equals(v, this.countKey))
                {
                    this.ResetFired();
                }
            });
        }
    }


    /// <summary>
    /// 随机待待, 拯救CPU
    /// </summary>
    public override async Task WaitMoment()
    {
        await Task.Delay(t);
    }


    private /*volatile*/ int t = 0;
    /// <summary>
    /// 
    /// </summary>
    public override void Change()
    {
        this.t = rnd.Next(1, this.rndSleepInMS);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public override async Task<uint> CurrentCount()
    {
        try
        {
            var v = await this.db.StringGetAsync(this.countKey, CommandFlags.PreferMaster);//.ToUInt(0);
            return (uint)v;
        }
        catch (Exception ex)
        {
            this.Logger?.Log(ex.Message, ex);
            return 0;
        }
    }



    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public override async Task IncrementCount(uint a)
    {
        try
        {
            _ = await loadedIncrByLuaScript.EvaluateAsync(this.db, new
            {
                k = (RedisKey)this.countKey,
                v = (int)a,
                e = (int)this.Period.TotalSeconds
            });
        }
        catch (Exception ex)
        {
            //await this.db.StringSetAsync(this.countKey, a, this.Period, flags: CommandFlags.DemandMaster);
            this.Logger?.Log(ex.Message, ex);
        }
    }


    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public override async Task<bool> TryLock()
    {
        /*
        StackExchange.Redis.RedisTimeoutException: Timeout awaiting response (outbound=5KiB, inbound=0KiB, 1797ms elapsed, timeout is 1000ms), command=SET, next: UNWATCH, inst: 0, qu: 5, qs: 38, aw: True, bw: WritingMessage, rs: ReadAsync, ws: Flushed, in: 95, in-pipe: 0, out-pipe: 0, last-in: 16, cur-in: 0, sync-ops: 160, async-ops: 1184435, serverEndpoint: 10.89.70.14:8901, conn-sec: 928.97, aoc: 1, mc: 1/1/0, mgr: 10 of 10 available, clientName: WEB13(SE.Redis-v2.8.31.52602), IOCP: (Busy=0,Free=1000,Min=150,Max=1000), WORKER: (Busy=21,Free=32746,Min=20,Max=32767), POOL: (Threads=30,QueuedItems=234,CompletedItems=15512153,Timers=149), v: 2.8.31.52602 (Please take a look at this article for some common client-side issues that can cause timeouts: https://stackexchange.github.io/StackExchange.Redis/Timeouts)
        at AsNum.Throttle.Redis.RedisCounter.TryLock()         
         */
        try
        {
            return await this.db.LockTakeAsync(this.lockKey, this.ThrottleID, this.LockTimeout ?? TimeSpan.FromSeconds(1), CommandFlags.DemandMaster);
        }
        catch (Exception ex)
        {
            this.Logger?.Log(ex.Message, ex);
            return false;
        }
    }


    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public override async Task ReleaseLock()
    {
        try
        {
            await this.db.LockReleaseAsync(this.lockKey, this.ThrottleID, CommandFlags.DemandMaster | CommandFlags.FireAndForget);
        }
        catch (Exception ex)
        {
            this.Logger?.Log(ex.Message, ex);
        }
    }


    /// <summary>
    /// 
    /// </summary>
    protected override void InnerDispose()
    {
        try
        {
            this.subscriber.Unsubscribe(KEY_EXPIRED_CHANNEL);
        }
        catch (Exception ex)
        {
            this.Logger?.Log(ex.Message, ex);
        }
    }
}
