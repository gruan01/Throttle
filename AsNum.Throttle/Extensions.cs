using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AsNum.Throttle;

internal static class Extensions
{
    /// <summary>
    /// 
    /// </summary>
    /// <param name="manualResetEvent"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public static Task WaitAsync(this ManualResetEventSlim manualResetEvent, CancellationToken cancellationToken = default)
        => WaitAsync(manualResetEvent.WaitHandle, cancellationToken);

    /// <summary>
    /// https://www.meziantou.net/waiting-for-a-manualreseteventslim-to-be-set-asynchronously.htm
    /// </summary>
    /// <param name="waitHandle"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    private static Task WaitAsync(this WaitHandle waitHandle, CancellationToken cancellationToken = default)
    {
        CancellationTokenRegistration cancellationRegistration = default;

        var tcs = new TaskCompletionSource();
        var handle = ThreadPool.RegisterWaitForSingleObject(
            waitObject: waitHandle,
            callBack: (o, timeout) =>
            {
                cancellationRegistration.Unregister();
                tcs.TrySetResult();
            },
            state: null,
            timeout: Timeout.InfiniteTimeSpan,
            executeOnlyOnce: true);

        if (cancellationToken.CanBeCanceled)
        {
            cancellationRegistration = cancellationToken.Register(() =>
            {
                handle.Unregister(waitHandle);
                tcs.TrySetCanceled(cancellationToken);
            });
        }

        return tcs.Task;
    }
}
