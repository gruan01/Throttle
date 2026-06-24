using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace AsNum.Throttle;

/// <summary>
/// 用 BlockingCollection 实现的阻止队列. 不能跨进程
/// </summary>
public sealed class DefaultBlock : BaseBlock, IAutoDispose
{

    /// <summary>
    /// 用 Interlocked 和 Volatile 保证 Initialize 与 Acquire/Release 之间的可见性
    /// </summary>
    private Channel<byte> block = Channel.CreateUnbounded<byte>();

    /// <summary>
    /// 
    /// </summary>
    protected override void Initialize(bool firstLoad)
    {
        var newBlock = Channel.CreateBounded<byte>(this.Frequency);

        // 原子交换，防止 Acquire/Release 读到半初始化的 Channel
        var old = Interlocked.Exchange(ref this.block, newBlock);

        //重新配置时, 把已经占用的空间重新占用.
        var n = Math.Min(old?.Reader.Count ?? 0, this.Frequency);
        for (var i = 0; i < n; i++)
        {
            newBlock.Writer.TryWrite(0);
        }
    }



    /// <summary>
    /// 
    /// </summary>
    internal override async Task Acquire()
    {
        // 读到本地变量，避免在 await 前后 this.block 被 Initialize 交换
        var currentBlock = Volatile.Read(ref this.block);
        if (this.LockTimeout is null)
            await currentBlock.Writer.WriteAsync(0);
        else
        {
            using var cts = new CancellationTokenSource(this.LockTimeout.Value);
            await currentBlock.Writer.WriteAsync(0, cts.Token);
        }
    }



    /// <summary>
    /// 
    /// </summary>
    internal override async Task Release()
    {
        var currentBlock = Volatile.Read(ref this.block);
        if (this.LockTimeout is null)
            await currentBlock.Reader.ReadAsync();
        else
        {
            using var cts = new CancellationTokenSource(this.LockTimeout.Value);
            await currentBlock.Reader.ReadAsync(cts.Token);
        }
    }


    /// <summary>
    /// 
    /// </summary>
    protected override void InnerDispose()
    {
        this.block.Writer.TryComplete();
    }
}
