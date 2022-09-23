using System;

namespace AsNum.Throttle
{
    /// <summary>
    /// 
    /// </summary>
    public class DefaultleLogger : ILogger
    {

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <param name="e"></param>
        /// <exception cref="NotImplementedException"></exception>
        public void Log(string? message, Exception? e)
        {
            Console.WriteLine($"{DateTime.Now:HH:mm:ss} {message} {e?.Message}\r\n{e?.StackTrace}");
        }
    }
}
