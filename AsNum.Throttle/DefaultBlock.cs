using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace AsNum.Throttle;

/// <summary>
/// 用 BlockingCollection 实现的阻止队列. 不能跨进程
/// </summary>
public sealed class DefaultBlock : BaseBlock
{

    /// <summary>
    /// 
    /// </summary>
    private Channel<byte> block = Channel.CreateUnbounded<byte>();

    /// <summary>
    /// 
    /// </summary>
    protected override void Initialize(bool firstLoad)
    {
        var old = this.block;
        //this.block = new BlockingCollection<byte>(this.Frequency);
        this.block = Channel.CreateBounded<byte>(this.Frequency);

        //重新配置时, 把已经占用的空间重新占用.
        var n = Math.Min(old?.Reader.Count ?? 0, this.Frequency);
        for (var i = 0; i < n; i++)
        {
            this.block.Writer.TryWrite(0);
        }
    }



    /// <summary>
    /// 
    /// </summary>
    internal override async Task Acquire()
    {
        if (this.LockTimeout is null)
            await this.block.Writer.WriteAsync(0);
        else
        {
            using var cts = new CancellationTokenSource(this.LockTimeout.Value);
            await this.block.Writer.WriteAsync(0, cts.Token);
        }
    }



    /// <summary>
    /// 
    /// </summary>
    internal override async Task Release()
    {
        if (this.LockTimeout is null)
            await this.block.Reader.ReadAsync();
        else
        {
            using var cts = new CancellationTokenSource(this.LockTimeout.Value);
            await this.block.Reader.ReadAsync(cts.Token);
        }
    }


    /// <summary>
    /// 
    /// </summary>
    protected override void InnerDispose()
    {
    }
}
