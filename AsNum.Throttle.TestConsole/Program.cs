using AsNum.Throttle.CoreTest;
using System;
using System.Threading.Tasks;

namespace AsNum.Throttle.TestConsole
{
    class Program
    {
        static readonly Tester Tester = new Tester(100, TimeSpan.FromSeconds(3));

        static async Task Main(string[] args)
        {
            AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

            //var rnd = new Random();
            Console.WriteLine("请输入要测试的循环次数:[10000]");
            var str = Console.ReadLine();

            if (!int.TryParse(str, out int n))
            {
                n = 10000;
            }

            await Tester.Run(n);

            Console.WriteLine("Complete....");

            Console.Read();
        }

        private static void CurrentDomain_ProcessExit(object sender, EventArgs e)
        {
            Tester.Dispose();
        }


        private static void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            Console.WriteLine(e.Exception.Message);
            e.SetObserved();
        }
    }
}
