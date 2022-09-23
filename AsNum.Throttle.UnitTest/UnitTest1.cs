using AsNum.Throttle.Redis;
using StackExchange.Redis;
using System.Diagnostics;

namespace AsNum.Throttle.UnitTest
{
    public class UnitTest1
    {
        [Fact]
        public async void DefaultUpdaterTest()
        {
            var throttle = new Throttle("test", TimeSpan.FromMinutes(1), 1);
            var updater = throttle.Updater;

            await updater.Update(TimeSpan.FromMinutes(2), 2);
            Assert.Equal(2, throttle.Blocker.Frequency);
            Assert.Equal(2, throttle.Counter.Frequency);
            Assert.Equal(2, throttle.Counter.Period.TotalMinutes);
        }



        [Fact]
        public async Task RedisCfgUpdaterTest()
        {
            var conn = ConnectionMultiplexer.Connect("localhost:6379");
            var u1 = new RedisCfgUpdater(conn);
            var u2 = new RedisCfgUpdater(conn);
            var u3 = new RedisCfgUpdater(conn);

            var logger = new WrapLogger((s, e) => { Debug.WriteLine($"----------------------------{s}"); Debug.WriteLine(e?.Message); });

            var t1 = new Throttle("test", TimeSpan.FromMinutes(1), 1, updater: u1, logger: logger);
            var t2 = new Throttle("test", TimeSpan.FromMinutes(1), 1, updater: u2, logger: logger);
            var t3 = new Throttle("test", TimeSpan.FromMinutes(2), 2, updater: u3, logger: logger);




            var minute = 2;
            var frequency = 2;
            await u1.Update(TimeSpan.FromMinutes(minute), frequency);
            await Task.Delay(1000);
            Assert.Equal(frequency, t1.Blocker.Frequency);
            Assert.Equal(frequency, t2.Blocker.Frequency);
            Assert.Equal(frequency, t3.Blocker.Frequency);
            Assert.Equal(frequency, t1.Counter.Frequency);
            Assert.Equal(frequency, t2.Counter.Frequency);
            Assert.Equal(frequency, t3.Counter.Frequency);
            Assert.Equal(frequency, u1.Frequency);
            Assert.Equal(frequency, u2.Frequency);
            Assert.Equal(frequency, u3.Frequency);
            Assert.Equal(TimeSpan.FromMinutes(minute), t1.Counter.Period);
            Assert.Equal(TimeSpan.FromMinutes(minute), t2.Counter.Period);
            Assert.Equal(TimeSpan.FromMinutes(minute), t3.Counter.Period);
            Assert.Equal(TimeSpan.FromMinutes(minute), u1.Period);
            Assert.Equal(TimeSpan.FromMinutes(minute), u2.Period);
            Assert.Equal(TimeSpan.FromMinutes(minute), u3.Period);


            minute = 3;
            frequency = 3;
            await u1.Update(TimeSpan.FromMinutes(minute), frequency);
            await Task.Delay(1000);
            Assert.Equal(frequency, t1.Blocker.Frequency);
            Assert.Equal(frequency, t2.Blocker.Frequency);
            Assert.Equal(frequency, t3.Blocker.Frequency);
            Assert.Equal(frequency, t1.Counter.Frequency);
            Assert.Equal(frequency, t2.Counter.Frequency);
            Assert.Equal(frequency, t3.Counter.Frequency);
            Assert.Equal(frequency, u1.Frequency);
            Assert.Equal(frequency, u2.Frequency);
            Assert.Equal(frequency, u3.Frequency);
            Assert.Equal(TimeSpan.FromMinutes(minute), t1.Counter.Period);
            Assert.Equal(TimeSpan.FromMinutes(minute), t2.Counter.Period);
            Assert.Equal(TimeSpan.FromMinutes(minute), t3.Counter.Period);
            Assert.Equal(TimeSpan.FromMinutes(minute), u1.Period);
            Assert.Equal(TimeSpan.FromMinutes(minute), u2.Period);
            Assert.Equal(TimeSpan.FromMinutes(minute), u3.Period);


            minute = 4;
            frequency = 4;
            await u2.Update(TimeSpan.FromMinutes(minute), frequency);
            await Task.Delay(1000);
            Assert.Equal(frequency, t1.Blocker.Frequency);
            Assert.Equal(frequency, t2.Blocker.Frequency);
            Assert.Equal(frequency, t3.Blocker.Frequency);
            Assert.Equal(frequency, t1.Counter.Frequency);
            Assert.Equal(frequency, t2.Counter.Frequency);
            Assert.Equal(frequency, t3.Counter.Frequency);
            Assert.Equal(frequency, u1.Frequency);
            Assert.Equal(frequency, u2.Frequency);
            Assert.Equal(frequency, u3.Frequency);
            Assert.Equal(TimeSpan.FromMinutes(minute), t1.Counter.Period);
            Assert.Equal(TimeSpan.FromMinutes(minute), t2.Counter.Period);
            Assert.Equal(TimeSpan.FromMinutes(minute), t3.Counter.Period);
            Assert.Equal(TimeSpan.FromMinutes(minute), u1.Period);
            Assert.Equal(TimeSpan.FromMinutes(minute), u2.Period);
            Assert.Equal(TimeSpan.FromMinutes(minute), u3.Period);
        }


        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task SelectTest()
        {
            var throttle = new Throttle("test", TimeSpan.FromSeconds(1), 1);
            throttle.OnPeriodElapsed += (s, e) => Debug.WriteLine($"-----------------OnPeriodElapsed");
            var updater = throttle.Updater;
            Debug.WriteLine("---------------------start");

            //��û�жϵ�������:
            //ÿ��1��, a = true, b��Ϊ������Ƶ,Ϊ false
            var a = await throttle.Select();
            var b = await throttle.Select();
            //var count = await throttle.Counter.CurrentCount();
            Assert.True(a);
            Assert.False(b);

            Debug.WriteLine("---------------------Update");
            //����Ƶ��Ϊÿ��2��, ��������ֻ�ɹ���1��, c = true, d�ֳ�Ƶ, Ϊ falsae
            await updater.Update(TimeSpan.FromSeconds(1), 2);
            var c = await throttle.Select();
            var d = await throttle.Select();
            var e = await throttle.Select();
            Assert.True(c);
            Assert.False(d);
            Assert.False(e);

            //����ƵΪÿ��4��, ����ɹ���2��, h Ϊ��Ƶ.
            await updater.Update(TimeSpan.FromSeconds(1), 4);
            var f = await throttle.Select();
            var g = await throttle.Select();
            var h = await throttle.Select();
            Assert.True(f);
            Assert.True(g);
            Assert.False(h);
            await Task.Delay(3000);
        }



        [Fact]
        public async Task SelectByRedisCounterTest()
        {
            var conn = ConnectionMultiplexer.Connect("localhost:6379");
            var counter = new RedisCounter(conn);

            var throttle = new Throttle("test", TimeSpan.FromSeconds(1), 1, counter: counter);
            throttle.OnPeriodElapsed += (s, e) => Debug.WriteLine($"-----------------OnPeriodElapsed");
            var updater = throttle.Updater;
            Debug.WriteLine("---------------------start");

            //��û�жϵ�������:
            //ÿ��1��, a = true, b��Ϊ������Ƶ,Ϊ false
            var a = await throttle.Select();
            var b = await throttle.Select();
            //var count = await throttle.Counter.CurrentCount();
            Assert.True(a);
            Assert.False(b);

            Debug.WriteLine("---------------------Update");
            //����Ƶ��Ϊÿ��2��, ��������ֻ�ɹ���1��, c = true, d�ֳ�Ƶ, Ϊ falsae
            await updater.Update(TimeSpan.FromSeconds(1), 2);
            var c = await throttle.Select();
            var d = await throttle.Select();
            var e = await throttle.Select();
            Assert.True(c);
            Assert.False(d);
            Assert.False(e);

            //����ƵΪÿ��4��, ����ɹ���2��, h Ϊ��Ƶ.
            await updater.Update(TimeSpan.FromSeconds(1), 4);
            var f = await throttle.Select();
            var g = await throttle.Select();
            var h = await throttle.Select();
            Assert.True(f);
            Assert.True(g);
            Assert.False(h);
            await Task.Delay(3000);
        }
    }
}