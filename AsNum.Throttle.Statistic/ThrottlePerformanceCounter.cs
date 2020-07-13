using System;
using System.Diagnostics;
using System.Linq;

namespace AsNum.Throttle.Statistic
{
    /// <summary>
    /// 
    /// </summary>
    [Obsolete]
    public class ThrottlePerformanceCounter : BasePerformanceCounter
    {

        /// <summary>
        /// 
        /// </summary>
        public static readonly string CATEGORY = "AsNum.Throttle";

        /// <summary>
        /// 
        /// </summary>
        public static readonly string CATEGORY_SUMMARY = "AsNum.Throttle.Summary";


        /// <summary>
        /// 
        /// </summary>
        private PerformanceCounter queueCounter;

        /// <summary>
        /// 
        /// </summary>
        private PerformanceCounter totalQueueCounter;


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
            //PerformanceCounterCategory.Delete(CATEGORY);
            //PerformanceCounterCategory.Delete(CATEGORY_SUMMARY);
            if (!PerformanceCounterCategory.Exists(CATEGORY))
            {
                var cdc = new CounterCreationDataCollection
                {
                    new CounterCreationData($"Queue", "", PerformanceCounterType.NumberOfItems32),
                };
                PerformanceCounterCategory.Create(CATEGORY
                    , ""
                    , PerformanceCounterCategoryType.MultiInstance
                    , cdc);
            }

            if (!PerformanceCounterCategory.Exists(CATEGORY_SUMMARY))
            {
                var cdc = new CounterCreationDataCollection
                {
                    new CounterCreationData($"__total#{this.ThrottleName}", "", PerformanceCounterType.NumberOfItems32),
                };
                PerformanceCounterCategory.Create(CATEGORY_SUMMARY
                    , ""
                    , PerformanceCounterCategoryType.SingleInstance
                    , cdc);
            }

            this.queueCounter = this.Create($"Queue", CATEGORY, $"{this.ThrottleName}_{this.InstanceID}");
            this.totalQueueCounter = this.Create($"__total#{this.ThrottleName}", CATEGORY_SUMMARY, "");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="counterName"></param>
        /// <param name="category"></param>
        /// <param name="instanceID"></param>
        /// <returns></returns>
        private PerformanceCounter Create(string counterName, string category, string instanceID)
        {
            return new PerformanceCounter()
            {
                CategoryName = category,
                CounterName = counterName,
                InstanceName = instanceID,
                ReadOnly = false,
                InstanceLifetime = !string.IsNullOrEmpty(instanceID) ? PerformanceCounterInstanceLifetime.Process : PerformanceCounterInstanceLifetime.Global,
                //RawValue = 0
            };

        }


        /// <summary>
        /// 
        /// </summary>
        public override void IncrementQueue()
        {
            this.queueCounter.Increment();
            var n = this.totalQueueCounter.Increment();
            //Console.WriteLine($"I .........{n}");
        }

        /// <summary>
        /// 
        /// </summary>
        public override void DecrementQueue()
        {
            this.queueCounter.Decrement();
            var n = this.totalQueueCounter.Decrement();
            //Console.WriteLine($"D .........{n}");
        }

        /// <summary>
        /// 
        /// </summary>
        public override void AddExecuted()
        {
            //this.totalCounter.Increment();
        }



        /// <summary>
        /// 
        /// </summary>
        protected override void InnerDispose()
        {
            if (this.queueCounter != null)
            {
                this.queueCounter.Dispose();
            }

            if (this.totalQueueCounter != null)
            {
                this.totalQueueCounter.Dispose();
            }
        }

    }
}
