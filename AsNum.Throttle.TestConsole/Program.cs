using AsNum.Throttle.CoreTest;
using System;
using System.Threading.Tasks;

namespace AsNum.Throttle.TestConsole
{
    class Program
    {
        static async Task Main(string[] args)
        {
            //AppDomain.CurrentDomain.ProcessExit += CurrentDomain_ProcessExit;
            TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

            //var rnd = new Random();
            Console.WriteLine("请输入要测试的循环次数:[10000]");
            var str = Console.ReadLine();
            if (!int.TryParse(str, out int n))
                n = 10000;

            Console.WriteLine("请输入 BatchCount:[2]");
            str = Console.ReadLine();
            if (!int.TryParse(str, out int b))
                b = 2;

            using var Tester = new Tester(10, TimeSpan.FromSeconds(3), b);

            while (true)
            {
                await Tester.Run(n);

                Console.WriteLine("Complete....");

                var x = Console.ReadKey();
                if (x.Key == ConsoleKey.Escape)
                    break;
            }

        }

        //private static void CurrentDomain_ProcessExit(object sender, EventArgs e)
        //{
        //    Tester.Dispose();
        //}


        private static void TaskScheduler_UnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs e)
        {
            Console.WriteLine(e.Exception.Message);
            e.SetObserved();
        }
    }
}
