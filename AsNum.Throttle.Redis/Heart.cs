using StackExchange.Redis;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace AsNum.Throttle.Redis
{
    /// <summary>
    /// 
    /// </summary>
    public class Heart
    {

        /// <summary>
        /// 心跳包的频率，毫秒
        /// </summary>
        public static int Interval { get; private set; } = 5000;

        /// <summary>
        /// 
        /// </summary>
        private static readonly ConcurrentDictionary<string, Heart> Instances = new ConcurrentDictionary<string, Heart>();


        /// <summary>
        /// 
        /// </summary>
        public string ThrottleName { get; }

        /// <summary>
        /// 心跳 Channel , 用于确定有多少客户端同时运行
        /// </summary>
        public string HeartChannel => $"{this.ThrottleName}#heart";

        /// <summary>
        /// 
        /// </summary>
        private ISubscriber subscriber = null;

        /// <summary>
        /// 
        /// </summary>
        private bool initialized = false;


        /// <summary>
        /// 是否只有一个客户端
        /// </summary>
        public bool IsSingleClient => this.SubscriberCount == 1;

        /// <summary>
        /// 订阅客户端的数量
        /// </summary>
        public long SubscriberCount { get; private set; } = 1;


        /// <summary>
        /// 
        /// </summary>
        /// <param name="interval">心跳包的频率，毫秒, 不建议改的太快</param>
        public static void SetInterval(int interval = 5000)
        {
            if (interval <= 0)
                throw new ArgumentOutOfRangeException(nameof(interval));

            Interval = interval;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public static Heart GetInstance(string throttleName, ISubscriber subscriber)
        {
            var heart = Instances.GetOrAdd(throttleName, new Heart(throttleName));
            heart.SetUp(subscriber);
            return heart;
        }


        /// <summary>
        /// 
        /// </summary>
        public void Release()
        {
            this.subscriber?.Unsubscribe(this.HeartChannel);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="throttleName"></param>
        private Heart(string throttleName)
        {
            this.ThrottleName = throttleName;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="subscriber"></param>
        private void SetUp(ISubscriber subscriber)
        {
            if (!this.initialized)
            {
                this.initialized = true;
                this.subscriber = subscriber;
                this.subscriber.Ping();

                this.subscriber.Subscribe(this.HeartChannel, (c, v) => { });
                this.Start();
            }
        }


        /// <summary>
        /// 定时发布一个空消息, 确定有多少个客户端在同时运行.
        /// </summary>
        private void Start()
        {
            Task.Run(async () =>
            {
                while (true)
                {
                    //发布空消息
                    var clients = await this.subscriber.PublishAsync(this.HeartChannel, "");
                    this.SubscriberCount = clients;
                    await Task.Delay(Interval);
                }
            });
        }

    }
}
