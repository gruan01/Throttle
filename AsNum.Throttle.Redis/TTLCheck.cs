using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace AsNum.Throttle.Redis
{
    /// <summary>
    /// 
    /// </summary>
    public class TTLCheck
    {
        /// <summary>
        /// 
        /// </summary>
        private readonly IDatabase db;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="db"></param>
        public TTLCheck(IDatabase db)
        {
            this.db = db;
        }

        /// <summary>
        /// 
        /// </summary>
        public void Check(string key)
        {
            Task.Factory.StartNew(async () =>
            {
                while (true)
                {
                    var t = await this.db.KeyTimeToLiveAsync(key, CommandFlags.DemandMaster);
                    Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff}\t{t}");
                    if (t == null)
                    {
                        await this.db.KeyDeleteAsync(key, CommandFlags.DemandMaster);
                    }

                    await Task.Delay(2000);
                }
            }
            , TaskCreationOptions.LongRunning);
        }
    }
}
