using System;
using System.Collections.Generic;
using System.Text;

namespace AsNum.Throttle
{
    /// <summary>
    /// 
    /// </summary>
    public class ThrottleStatistic
    {
        /// <summary>
        /// 总共压入队列数
        /// </summary>
        public long TotalEnqueued { get; set; }

        /// <summary>
        /// 总执行条数
        /// </summary>
        public long TotalExecuted { get; set; }


        /// <summary>
        /// 当前周期内执行次数
        /// </summary>
        public int CurrentExecutedCount { get; set; }

        /// <summary>
        /// 当前周期内, 可用空间 (阴止队列可插入数: block.BoundedCapacity - block.Count)
        /// </summary>
        public int CurrentFreeSpace { get; set; }

        /// <summary>
        /// 当前周期内, 未执行任务数
        /// </summary>
        public int CurrentTaskCount { get; set; }

        /// <summary>
        /// 周期数
        /// </summary>
        public long PeriodNumber { get; set; }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return $"TotalEnqueued:{TotalEnqueued}, TotalExecuted:{TotalExecuted}, CurrentExecutedCount:{CurrentExecutedCount}, CurrentFreeSpace:{CurrentFreeSpace}, CurrentTaskCount:{CurrentTaskCount}, PeriodNumber:{PeriodNumber}";
        }
    }
}
