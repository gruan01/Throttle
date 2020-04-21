using System;
using System.Collections.Generic;
using System.Text;

namespace AsNum.Throttle
{
    /// <summary>
    /// 
    /// </summary>
    public interface IBlock
    {

        /// <summary>
        /// 阻止队列的长度
        /// </summary>
        int Length { get; }

        /// <summary>
        /// 尝试占用一个位置
        /// </summary>
        void Wait(string tag);

        /// <summary>
        /// 释放一个位置
        /// </summary>
        void Release(string tag);

        /// <summary>
        /// 是否自动释放
        /// </summary>
        bool AutoDispose { get; set; }

        /// <summary>
        /// 
        /// </summary>
        void Dispose();
    }
}
