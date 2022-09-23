using System;

namespace AsNum.Throttle
{
    /// <summary>
    /// 
    /// </summary>
    public interface ILogger
    {

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <param name="e"></param>
        void Log(string message, Exception e);

    }
}
