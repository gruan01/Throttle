using System;

namespace AsNum.Throttle;

/// <summary>
/// 
/// </summary>
/// <remarks>
/// 
/// </remarks>
/// <param name="func"></param>
public class WrapLogger(Action<string?, Exception?> func) : ILogger
{

    /// <summary>
    /// 
    /// </summary>
    /// <param name="message"></param>
    /// <param name="e"></param>
    public void Log(string? message, Exception? e)
    {
        func?.Invoke(message, e);
    }
}
