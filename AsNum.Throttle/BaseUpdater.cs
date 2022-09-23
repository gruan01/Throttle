using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AsNum.Throttle
{

    /// <summary>
    /// 
    /// </summary>
    public abstract class BaseCfgUpdater
    {
        /// <summary>
        /// 
        /// </summary>
        protected string ThrottleName { get; private set; } = "";

        /// <summary>
        /// 
        /// </summary>
        protected string ThrottleID { get; private set; } = "";

        /// <summary>
        /// 
        /// </summary>
        public TimeSpan Period { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        public int Frequency { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        protected ILogger? Logger { get; private set; }


        /// <summary>
        /// 
        /// </summary>
        private readonly List<IUpdate> subscribers = new List<IUpdate>();

        /// <summary>
        /// 通知更新
        /// </summary>
        protected void NotifyChange(TimeSpan newPeriod, int newFrequency, TimeSpan oldPeriod, int oldFrequency, string tag)
        {
            if (newPeriod != oldPeriod || newFrequency != oldFrequency)
            {
                this.Logger?.Log($"Update({tag}) {this.ThrottleName}({this.ThrottleID}) [{oldPeriod},{oldFrequency}]->[{newPeriod},{newFrequency}]", null);

                //更新订阅者
                foreach (var s in this.subscribers)
                    s.Update(newPeriod, newFrequency);

                //更新自己的, 没有放到 Update 中， 是因为从 Redis 订阅消息进来的， 不会不会执行 Update 方法。
                this.Period = newPeriod;
                this.Frequency = newFrequency;
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="update"></param>
        internal void Subscribe(IUpdate update)
        {
            this.subscribers.Add(update);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        internal void SetUp(string throttleName, string throttleID, TimeSpan period, int frequency, ILogger? logger)
        {
            this.Logger = logger;
            this.ThrottleName = throttleName;
            this.ThrottleID = throttleID;
            var (exPeriod, exFrequency) = this.Initialize(period, frequency);
            this.Period = exPeriod;
            this.Frequency = exFrequency;
        }

        /// <summary>
        /// 
        /// </summary>
        protected abstract (TimeSpan exPeriod, int exFrquency) Initialize(TimeSpan period, int frequency);


        /// <summary>
        /// 用于外部更新配置的接口.
        /// </summary>
        /// <param name="newPeriod"></param>
        /// <param name="newFrequency"></param>
        public async Task Update(TimeSpan newPeriod, int newFrequency)
        {
            if (newPeriod == this.Period && newFrequency == this.Frequency)
                return;

            var oldPeriod = this.Period;
            var oldFrequency = this.Frequency;

            await this.Save(newPeriod, newFrequency);
            this.NotifyChange(newPeriod, newFrequency, oldPeriod, oldFrequency, "Update");
        }


        /// <summary>
        /// 接收配置更改, 并保存
        /// </summary>
        protected abstract Task Save(TimeSpan newPeriod, int newFrequency);
    }
}
