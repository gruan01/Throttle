using AsNum.Throttle.Redis;
using StackExchange.Redis;
using System.Diagnostics;

namespace AsNum.Throttle.UnitTest
{
    public class UnitTest1
    {
        [Fact]
        public async Task DefaultUpdaterTest()
        {
            var a = new TimeSpan(100);
            var b = TimeSpan.FromMicroseconds(10);

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
            const int frequency = 10;

            // ========== 1. 基础串行：精确限频 ==========
            var t = new Throttle("test", TimeSpan.FromMinutes(10), frequency,
                isSelectMode: true);

            var results = new List<bool>();
            for (var i = 0; i < frequency + 3; i++)
                results.Add(await t.Select());

            Assert.Equal(frequency, results.Count(r => r));
            Assert.Equal(3, results.Count(r => !r));

            // 验证内部计数恰好等于 frequency
            var count = await t.Counter.CurrentCount();
            Assert.Equal((uint)frequency, count);

            // ========== 2. 周期重置 ==========
            t.Dispose();
            var t2 = new Throttle("test2", TimeSpan.FromMilliseconds(300), frequency,
                isSelectMode: true);

            for (var i = 0; i < frequency; i++)
                Assert.True(await t2.Select());

            Assert.False(await t2.Select()); // 名额已用完

            await Task.Delay(500); // 等待 Timer 重置 _currentCount
            Assert.True(await t2.Select());   // 新周期，恢复通过

            // ========== 3. 并发 ==========
            t2.Dispose();
            var t3 = new Throttle("test3", TimeSpan.FromMinutes(10), frequency,
                isSelectMode: true);

            var passed = 0;
            var totalTasks = frequency * 20; // 200 并发抢 10 个名额
            var tasks = new List<Task>();
            for (var i = 0; i < totalTasks; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    if (await t3.Select())
                        Interlocked.Increment(ref passed);
                }));
            }
            await Task.WhenAll(tasks);

            Assert.Equal(frequency, passed);

            // ========== 4. 运行时更新频率 ==========
            t3.Dispose();
            var t4 = new Throttle("test4", TimeSpan.FromMinutes(10), 2,
                isSelectMode: true);

            Assert.True(await t4.Select());  // 1/2
            Assert.True(await t4.Select());  // 2/2
            Assert.False(await t4.Select()); // 已满

            // 频率从 2 扩到 5，之前已用 2，还有 3 个名额
            await t4.Updater.Update(TimeSpan.FromMinutes(10), 5);
            Assert.True(await t4.Select());
            Assert.True(await t4.Select());
            Assert.True(await t4.Select());
            Assert.False(await t4.Select()); // 已满 5
        }



        [Fact]
        public async Task SelectByRedisCounterTest()
        {
            var conn = ConnectionMultiplexer.Connect("localhost:6379");
            var db = conn.GetDatabase();
            var testName = $"ST:{Guid.NewGuid():N}";
            var countKey = $"{testName}:counter:count";

            try
            {
                const int frequency = 10;
                const int nodeCount = 8;       // 模拟 8 个集群节点
                const int callsPerNode = 25;   // 每个节点发 25 次请求
                const int totalCalls = nodeCount * callsPerNode; // 共 200 次

                // ========== 1. 基础串行：精确限频 ==========
                var c1 = new RedisCounter(conn);
                var t1 = new Throttle(testName, TimeSpan.FromSeconds(5), frequency,
                    counter: c1, isSelectMode: true);

                var serialResults = new List<bool>();
                for (var i = 0; i < frequency + 3; i++)
                    serialResults.Add(await t1.Select());

                Assert.Equal(frequency, serialResults.Count(r => r));
                Assert.Equal(3, serialResults.Count(r => !r));
                Assert.Equal(frequency, (int)await db.StringGetAsync(countKey));

                // ========== 2. 周期重置 ==========
                await db.KeyDeleteAsync(countKey, CommandFlags.DemandMaster);
                await Task.Delay(500);

                var c2 = new RedisCounter(conn);
                var t2 = new Throttle(testName, TimeSpan.FromSeconds(2), frequency,
                    counter: c2, isSelectMode: true);

                await t2.Select(); // INCRBY=1, EXPIRE 2s
                await Task.Delay(2500);           // 等待 key 过期

                Assert.True(await t2.Select());   // key 已过期，重新通过
                await db.KeyDeleteAsync(countKey, CommandFlags.DemandMaster);
                await Task.Delay(300);

                // ========== 3. 集群并发：原子性验证 ==========
                // 模拟 8 节点集群同时高频调用，共享同一 Redis key
                // 每个节点有独立的 RedisCounter 实例
                var globalPassed = 0;
                var globalRejected = 0;

                var nodeTasks = new List<Task>();
                for (var node = 0; node < nodeCount; node++)
                {
                    nodeTasks.Add(Task.Run(async () =>
                    {
                        var nodeCounter = new RedisCounter(conn);
                        var nodeThrottle = new Throttle(testName,
                            TimeSpan.FromSeconds(30), frequency,
                            counter: nodeCounter,
                            isSelectMode: true);

                        var localPassed = 0;
                        var localRejected = 0;
                        for (var i = 0; i < callsPerNode; i++)
                        {
                            if (await nodeThrottle.Select())
                                localPassed++;
                            else
                                localRejected++;
                        }

                        Interlocked.Add(ref globalPassed, localPassed);
                        Interlocked.Add(ref globalRejected, localRejected);
                    }));
                }

                await Task.WhenAll(nodeTasks);

                Assert.Equal(frequency, globalPassed);
                Assert.Equal(totalCalls - frequency, globalRejected);

                // Redis 端计数也应等于 frequency
                var finalCount = (int)await db.StringGetAsync(countKey);
                Assert.Equal(frequency, finalCount);
            }
            finally
            {
                await db.KeyDeleteAsync(countKey, CommandFlags.DemandMaster);
            }
        }
    }
}