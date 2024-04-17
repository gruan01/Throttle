using AsNum.Throttle.Redis;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace AsNum.Throttle.CoreTest
{
    /// <summary>
    /// 
    /// </summary>
    public class Tester : IDisposable
    {

        private readonly BaseCounter Counter;
        private readonly Throttle TS;
        private readonly ConnectionMultiplexer Conn;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="n"></param>
        public Tester(int boundry, TimeSpan period, int batchCount)
        {
            //this.Conn = ConnectionMultiplexer.Connect("localhost:6379");

            //this.Counter = new RedisCounter(this.Conn, batchCount);
            this.TS = new Throttle("test", period, boundry,/* counter: this.Counter,*/ concurrentCount: 2);
            //this.TS = new Throttle("test", period, boundry);
            this.TS.OnPeriodElapsed += Ts_OnPeriodElapsed;
        }


        public async Task Run(int n)
        {
            var tsks = new List<Task>();

            for (var i = 0; i < n; i++)
            {
                //Task<Task>
                var tsk1 = this.TS.Execute(() => AA(i));
                ////参数绑定
                //var tsk2 = this.TS.Execute((o) => AA((int)o), i);

                ////Task<Task<T>>
                //var tsk3 = this.TS.Execute(() => BB(i));
                ////参数绑定
                //var tsk4 = ts.Execute((o) => BB((int)o), i);

                ////Task<T>
                //var tsk5 = ts.Execute(() => CC(i));
                ////参数绑定
                //var tsk6 = ts.Execute((o) => CC((int)o), i);

                tsks.Add(tsk1);
                //tsks.Add(tsk2.Unwrap());
                //tsks.Add(tsk3);
                //tsks.Add(tsk4);
                //tsks.Add(tsk5);
                //tsks.Add(tsk6);
            }

            await Task.WhenAll(tsks);
        }

        private static void Ts_OnPeriodElapsed(object sender, PeriodElapsedEventArgs e)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff}---------------");
            Console.ResetColor();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="i"></param>
        /// <returns></returns>
        private static async Task AA(int i)
        {
            //await Task.Delay(TimeSpan.FromSeconds(6));
            await Task.Delay(TimeSpan.FromSeconds(6));
            //Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff}\tAA:{i}");
            //Console.ResetColor();
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="i"></param>
        /// <returns></returns>
        private static async Task<int> BB(int i)
        {
            //await Task.Delay(TimeSpan.FromSeconds(1));
            Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff}\tBB:{i}");
            return i;
        }


        private static int CC(int i)
        {
            Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff}\tCC:{i}");
            return i;
        }




        #region dispose
        /// <summary>
        /// 
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }


        /// <summary>
        /// 
        /// </summary>
        ~Tester()
        {
            this.Dispose(false);
        }


        private bool isDisposed = false;
        /// <summary>
        /// 
        /// </summary>
        /// <param name="flag"></param>
        private void Dispose(bool flag)
        {
            if (!isDisposed)
            {
                if (flag)
                {
                    this.Counter.Dispose();
                    //this.Block.Dispose();
                    //this.PerformanceCounter.Dispose();
                    this.Conn.Dispose();
                    Console.WriteLine("Tester Disposed");
                }
                isDisposed = true;
            }
        }
        #endregion
    }
}
