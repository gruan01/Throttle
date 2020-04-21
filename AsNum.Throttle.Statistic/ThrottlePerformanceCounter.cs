using System;
using System.Diagnostics;

namespace AsNum.Throttle.Statistic
{
    /// <summary>
    /// 
    /// </summary>
    public class ThrottlePerformanceCounter : BasePerformanceCounter
    {

        /// <summary>
        /// 
        /// </summary>
        public static readonly string PERFORMANCE_COUNTER_CATEGORY_INSTANCE = "AsNum.Throttle";

        ///// <summary>
        ///// 
        ///// </summary>
        //public static readonly string PERFORMANCE_COUNTER_CATEGORY_ALL = "AsNum.Throttle.All";


        /// <summary>
        /// 
        /// </summary>
        private PerformanceCounter queueCounter;

        ///// <summary>
        ///// 
        ///// </summary>
        //private PerformanceCounter totalQueueCounter;

        /// <summary>
        /// 
        /// </summary>
        private PerformanceCounter totalCounter;

        /// <summary>
        /// 
        /// </summary>
        public string InstanceID { get; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="instanceID">如果不传, 取当前进程的ID</param>
        public ThrottlePerformanceCounter(string instanceID = null)
        {
            if (string.IsNullOrWhiteSpace(instanceID))
                instanceID = Process.GetCurrentProcess().Id.ToString();

            InstanceID = instanceID;
        }

        /// <summary>
        /// 
        /// </summary>
        protected override void Initialize()
        {
            //PerformanceCounterCategory.Delete(PERFORMANCE_COUNTER_CATEGORY_INSTANCE);
            //PerformanceCounterCategory.Delete(PERFORMANCE_COUNTER_CATEGORY_ALL);
            if (!PerformanceCounterCategory.Exists(PERFORMANCE_COUNTER_CATEGORY_INSTANCE))
            {
                var cdc = new CounterCreationDataCollection
                {
                    new CounterCreationData($"Queue", "", PerformanceCounterType.NumberOfItems32),
                    new CounterCreationData($"Executed", "", PerformanceCounterType.NumberOfItems32)
                };
                PerformanceCounterCategory.Create(PERFORMANCE_COUNTER_CATEGORY_INSTANCE
                    , "AsNum.Throttle"
                    , PerformanceCounterCategoryType.MultiInstance
                    , cdc);
            }

            //if (!PerformanceCounterCategory.Exists(PERFORMANCE_COUNTER_CATEGORY_ALL))
            //{
            //    var cdc = new CounterCreationDataCollection
            //    {
            //        new CounterCreationData($"AllQueue", "", PerformanceCounterType.NumberOfItems32),
            //    };
            //    PerformanceCounterCategory.Create(PERFORMANCE_COUNTER_CATEGORY_ALL
            //        , "AsNum.Throttle.All"
            //        , PerformanceCounterCategoryType.SingleInstance
            //        , cdc);
            //}

            this.queueCounter = this.Create($"Queue", PERFORMANCE_COUNTER_CATEGORY_INSTANCE, this.InstanceID);
            this.totalCounter = this.Create($"Executed", PERFORMANCE_COUNTER_CATEGORY_INSTANCE, this.InstanceID);
            //this.totalQueueCounter = this.Create($"AllQueue", PERFORMANCE_COUNTER_CATEGORY_ALL);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="counterName"></param>
        /// <returns></returns>
        private PerformanceCounter Create(string counterName, string category, string instanceID = null)
        {
            //if (!string.IsNullOrWhiteSpace(instanceID))
            return new PerformanceCounter()
            {
                CategoryName = category,
                CounterName = counterName,
                InstanceName = $"{this.ThrottleName}#{instanceID}",
                ReadOnly = false,
                InstanceLifetime = PerformanceCounterInstanceLifetime.Process
            };
            //else
            //    return new PerformanceCounter()
            //    {
            //        CategoryName = category,
            //        CounterName = counterName,
            //        ReadOnly = false,
            //        InstanceLifetime = PerformanceCounterInstanceLifetime.Global
            //    };
        }


        /// <summary>
        /// 
        /// </summary>
        public override void IncrementQueue()
        {
            this.queueCounter.Increment();
            //this.totalQueueCounter.Increment();
        }

        /// <summary>
        /// 
        /// </summary>
        public override void DecrementQueue()
        {
            this.queueCounter.Decrement();
            //this.totalQueueCounter.Decrement();
        }

        /// <summary>
        /// 
        /// </summary>
        public override void AddExecuted()
        {
            this.totalCounter.Increment();
        }



        /// <summary>
        /// 
        /// </summary>
        protected override void InnerDispose()
        {
            if (this.totalCounter != null)
            {
                this.totalCounter.Dispose();
            }

            if (this.queueCounter != null)
            {
                this.queueCounter.Dispose();
            }
        }

    }
}
