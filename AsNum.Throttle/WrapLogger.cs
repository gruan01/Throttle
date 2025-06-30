using System;

namespace AsNum.Throttle;

/// <summary>
/// 
/// </summary>
public class WrapLogger : ILogger
{
    /// <summary>
    /// 
    /// </summary>
    private readonly Action<string?, Exception?> func;

    /// <summary>
    /// 
    /// </summary>
    /// <param name="func"></param>
    public WrapLogger(Action<string?, Exception?> func)
    {
        this.func = func;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="message"></param>
    /// <param name="e"></param>
    public void Log(string? message, Exception? e)
    {
        this.func?.Invoke(message, e);
    }
}
