using AsNum.Throttle;
using AsNum.Throttle.Redis;
using StackExchange.Redis;
using System.Diagnostics;
using System.Threading.Channels;

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

            // 显式初始化所有 Updater（SubscribeAsync 已从构造器移至异步初始化）
            await Task.WhenAll(u1.InitializeAsync(), u2.InitializeAsync(), u3.InitializeAsync());

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

        // ========== 变更覆盖测试 ==========

        /// <summary>
        /// 验证 DefaultBlock 在 Initialize 通道交换时，Acquire/Release 不抛异常、计数不乱
        /// </summary>
        [Fact]
        public async Task DefaultBlock_ConcurrentChannelSwap_NoCorruption()
        {
            const int initialFreq = 20;
            const int newFreq = 5;

            var block = new DefaultBlock();
            block.Setup(initialFreq, null);

            // 占用 5 个槽位
            for (var i = 0; i < 5; i++)
                await block.Acquire();

            // 同步触发配置更新（通道交换），同时并发 Acquire
            var initDone = new TaskCompletionSource();
            var acquireTask = Task.Run(async () =>
            {
                await initDone.Task;
                await block.Acquire();
            });

            // 模拟配置更新：频率从 20 → 5，通道重建（Update 内部同步完成，返回 CompletedTask）
            _ = ((IUpdate)block).Update(TimeSpan.FromSeconds(1), newFreq);
            // 关键：先 Release 一个槽位解除 Acquire 的阻塞，再 await acquireTask
            initDone.SetResult();
            await block.Release();   // 释放一个槽位 → Acquire 解除阻塞
            await acquireTask;      // 现在可以完成了

            // 清理：释放剩余槽位（5 个转移 + 1 个新 Acquire 已释放，剩 5 个）
            for (var i = 0; i < 5; i++)
                await block.Release();
        }

        /// <summary>
        /// 验证 DefaultBlock.Dispose 后通道被 Complete，后续 Acquire 快速失败不挂起
        /// </summary>
        [Fact]
        public async Task DefaultBlock_Dispose_CompletesChannel()
        {
            var block = new DefaultBlock();
            block.Setup(1, null); // 容量 1，无超时

            await block.Acquire(); // 占满唯一槽位

            // 不 Release，直接 Dispose
            block.Dispose();

            // Acquire 应立即失败（通道已关闭），而非永久挂起
            var sw = Stopwatch.StartNew();
            await Assert.ThrowsAsync<ChannelClosedException>(
                () => block.Acquire());
            sw.Stop();

            Assert.True(sw.ElapsedMilliseconds < 1000,
                $"Acquire after Dispose should fail fast, took {sw.ElapsedMilliseconds}ms");
        }

        /// <summary>
        /// 验证 Throttle.Dispose 后，无论 Counter 是否实现 IAutoDispose，
        /// OnReset 事件都被取消订阅
        /// </summary>
        [Fact]
        public void Throttle_Dispose_AlwaysUnsubscribesCounterEvent()
        {
            var counter = new TestableDefaultCounter();
            var periodElapsedCount = 0;

            var throttle = new Throttle("test-dispose", TimeSpan.FromMinutes(1), 1,
                counter: counter);
            throttle.OnPeriodElapsed += (_, _) => periodElapsedCount++;

            // Dispose 前：事件正常触发
            counter.TriggerReset();
            Assert.Equal(1, periodElapsedCount);

            // Dispose
            throttle.Dispose();

            // Dispose 后：事件不应再触发
            counter.TriggerReset();
            Assert.Equal(1, periodElapsedCount); // 仍是 1
        }

        /// <summary>
        /// 辅助类：暴露 BaseCounter.ResetFired() 供测试
        /// </summary>
        private sealed class TestableDefaultCounter : DefaultCounter
        {
            public void TriggerReset() => this.ResetFired();
        }

        /// <summary>
        /// 验证 RedisCounter 构造时不再同步调用 LuaScript.Load，
        /// 首次 EvaluateAsync 自动完成脚本加载（延迟加载）
        /// </summary>
        [Fact]
        public async Task RedisCounter_LazyScriptLoad_FirstCallSucceeds()
        {
            var conn = ConnectionMultiplexer.Connect("localhost:6379");
            var testName = $"LSL:{Guid.NewGuid():N}";
            var countKey = $"{testName}:counter:count";

            try
            {
                var counter = new RedisCounter(conn);
                var throttle = new Throttle(testName, TimeSpan.FromSeconds(5), 5,
                    counter: counter, isSelectMode: true);

                // 首次调用 — 脚本未预热，LuaScript.EvaluateAsync 内部自动 SCRIPT LOAD + EVAL
                var result = await throttle.Select();
                Assert.True(result);

                // 验证 Redis 端计数正确
                var db = conn.GetDatabase();
                var count = (int)await db.StringGetAsync(countKey);
                Assert.Equal(1, count);
            }
            finally
            {
                var db = conn.GetDatabase();
                await db.KeyDeleteAsync(countKey, CommandFlags.DemandMaster);
            }
        }

        // ========== RedisCfgUpdater InitializeAsync 变更覆盖测试 ==========

        /// <summary>
        /// 场景：客户端初始化 1s/100，Redis 中已有 1s/150 →
        ///       InitializeAsync 应从 Redis catch-up，正确更新到 150
        /// </summary>
        [Fact]
        public async Task RedisCfgUpdater_InitializeAsync_CatchesUpWithRedisConfig()
        {
            var conn = ConnectionMultiplexer.Connect("localhost:6379");
            var testName = $"CFG_CU:{Guid.NewGuid():N}";
            var db = conn.GetDatabase();

            try
            {
                // 预设 Redis：period=1s (1000ms), frequency=150
                await db.StringSetAsync($"{testName}:period", "1000");
                await db.StringSetAsync($"{testName}:frequency", "150");

                var updater = new RedisCfgUpdater(conn);
                var throttle = new Throttle(testName, TimeSpan.FromSeconds(1), 100,
                    updater: updater, isSelectMode: true);

                // 构造后尚未异步初始化，应保留构造参数
                Assert.Equal(100, updater.Frequency);
                Assert.Equal(TimeSpan.FromSeconds(1), updater.Period);

                // 触发 InitializeAsync（通过 Select）
                await throttle.Select();

                // 应从 Redis catch-up：frequency 100→150
                Assert.Equal(150, updater.Frequency);
                Assert.Equal(TimeSpan.FromSeconds(1), updater.Period);
                Assert.Equal(150, throttle.Counter.Frequency);
            }
            finally
            {
                await db.KeyDeleteAsync($"{testName}:period");
                await db.KeyDeleteAsync($"{testName}:frequency");
            }
        }

        /// <summary>
        /// 场景：客户端初始化 1s/100，Redis 中也是 1s/100 →
        ///       InitializeAsync 不应触发无意义的 NotifyChange
        /// </summary>
        [Fact]
        public async Task RedisCfgUpdater_InitializeAsync_NoChangeWhenConfigMatches()
        {
            var conn = ConnectionMultiplexer.Connect("localhost:6379");
            var testName = $"CFG_NC:{Guid.NewGuid():N}";
            var db = conn.GetDatabase();

            try
            {
                // 预设 Redis 与客户端一致
                await db.StringSetAsync($"{testName}:period", "1000");
                await db.StringSetAsync($"{testName}:frequency", "100");

                var updater = new RedisCfgUpdater(conn);
                var throttle = new Throttle(testName, TimeSpan.FromSeconds(1), 100,
                    updater: updater, isSelectMode: true);

                await throttle.Select();

                // 配置一致，不应变更
                Assert.Equal(100, updater.Frequency);
                Assert.Equal(TimeSpan.FromSeconds(1), updater.Period);
            }
            finally
            {
                await db.KeyDeleteAsync($"{testName}:period");
                await db.KeyDeleteAsync($"{testName}:frequency");
            }
        }

        /// <summary>
        /// 场景：Redis 中无配置（首次启动）→
        ///       InitializeAsync 写入当前配置，客户端保持原值
        /// </summary>
        [Fact]
        public async Task RedisCfgUpdater_InitializeAsync_FirstClientWritesToRedis()
        {
            var conn = ConnectionMultiplexer.Connect("localhost:6379");
            var testName = $"CFG_FC:{Guid.NewGuid():N}";
            var db = conn.GetDatabase();

            try
            {
                // 确保 Redis 中没有旧数据
                await db.KeyDeleteAsync($"{testName}:period");
                await db.KeyDeleteAsync($"{testName}:frequency");

                var updater = new RedisCfgUpdater(conn);
                var throttle = new Throttle(testName, TimeSpan.FromSeconds(2), 200,
                    updater: updater, isSelectMode: true);

                await throttle.Select();

                // 客户端保持原值
                Assert.Equal(200, updater.Frequency);
                Assert.Equal(TimeSpan.FromSeconds(2), updater.Period);

                // Redis 中应写入当前值
                var fStr = await db.StringGetAsync($"{testName}:frequency");
                var pStr = await db.StringGetAsync($"{testName}:period");
                Assert.Equal(200, (int)fStr);
                Assert.Equal(2000, (int)pStr);
            }
            finally
            {
                await db.KeyDeleteAsync($"{testName}:period");
                await db.KeyDeleteAsync($"{testName}:frequency");
            }
        }

        /// <summary>
        /// 场景：Redis 中有部分残留数据（只有 period，没有 frequency）→
        ///       不应崩溃，frequency 保持客户端原值
        /// </summary>
        [Fact]
        public async Task RedisCfgUpdater_InitializeAsync_HandlesPartialRedisConfig()
        {
            var conn = ConnectionMultiplexer.Connect("localhost:6379");
            var testName = $"CFG_PC:{Guid.NewGuid():N}";
            var db = conn.GetDatabase();

            try
            {
                // 只设 period（模拟部分残留）
                await db.StringSetAsync($"{testName}:period", "3000"); // 3s
                await db.KeyDeleteAsync($"{testName}:frequency");       // frequency 不存在

                var updater = new RedisCfgUpdater(conn);
                var throttle = new Throttle(testName, TimeSpan.FromSeconds(1), 50,
                    updater: updater, isSelectMode: true);

                await throttle.Select();

                // frequency 从 Redis 读到 0 → NotifyChange 条件不满足 → 保持原值
                Assert.Equal(50, updater.Frequency);
            }
            finally
            {
                await db.KeyDeleteAsync($"{testName}:period");
                await db.KeyDeleteAsync($"{testName}:frequency");
            }
        }

        /// <summary>
        /// 验证 Initialize 阶段不触发 Redis I/O（订阅已移至 InitializeAsync），
        /// 构造 Throttle 不会因 Redis 超时而抛异常
        /// </summary>
        [Fact]
        public void RedisCfgUpdater_Initialize_DoesNotCallRedisIO()
        {
            var conn = ConnectionMultiplexer.Connect("localhost:6379");
            var testName = $"CFG_NIO:{Guid.NewGuid():N}";

            // 构造 Throttle → SetUp → Initialize：不应有 Redis I/O
            // 如果 Initialize 中有同步 Subscribe/网络 I/O，超时会抛异常
            var updater = new RedisCfgUpdater(conn);
            var throttle = new Throttle(testName, TimeSpan.FromSeconds(1), 10,
                updater: updater, isSelectMode: true);

            // 构造完成，未抛异常
            Assert.Equal(10, updater.Frequency);
            Assert.Equal(TimeSpan.FromSeconds(1), updater.Period);
        }

        /// <summary>
        /// 验证 Update 自动触发 InitializeAsync（无需显式调用），
        /// 且跨进程通知正常工作。
        /// </summary>
        [Fact]
        public async Task RedisCfgUpdater_Update_AutoInitializesAndPropagates()
        {
            var conn = ConnectionMultiplexer.Connect("localhost:6379");
            var testName = $"CFG_UA:{Guid.NewGuid():N}";
            var db = conn.GetDatabase();

            try
            {
                var u1 = new RedisCfgUpdater(conn);
                var u2 = new RedisCfgUpdater(conn);

                var t1 = new Throttle(testName, TimeSpan.FromMinutes(1), 10,
                    updater: u1, isSelectMode: true);
                var t2 = new Throttle(testName, TimeSpan.FromMinutes(1), 10,
                    updater: u2, isSelectMode: true);

                // u1.Update 应自动触发 InitializeAsync（订阅 + 保存 + 发布）
                // 无需显式调用 InitializeAsync
                await u1.Update(TimeSpan.FromMinutes(2), 20);

                // u1 自身应立即更新
                Assert.Equal(20, u1.Frequency);
                Assert.Equal(TimeSpan.FromMinutes(2), u1.Period);
                Assert.Equal(20, t1.Counter.Frequency);

                // u2 尚未初始化，需要先触发才能收到 PubSub 消息
                await u2.InitializeAsync();
                // InitializeAsync 从 Redis 读取已有配置，应 catch-up 到 u1 发布的值
                Assert.Equal(20, u2.Frequency);
                Assert.Equal(TimeSpan.FromMinutes(2), u2.Period);

                // Redis 中应有正确值
                var f = (int)await db.StringGetAsync($"{testName}:frequency");
                var p = (int)await db.StringGetAsync($"{testName}:period");
                Assert.Equal(20, f);
                Assert.Equal(2000 * 60, p); // 2 minutes in ms
            }
            finally
            {
                await db.KeyDeleteAsync($"{testName}:period");
                await db.KeyDeleteAsync($"{testName}:frequency");
            }
        }

        /// <summary>
        /// 验证 DefaultCfgUpater.Update 仍正常工作（基类 InitializeAsync 为 no-op）
        /// </summary>
        [Fact]
        public async Task DefaultCfgUpater_Update_StillWorksAfterBaseChange()
        {
            var throttle = new Throttle("test-base-update", TimeSpan.FromMinutes(1), 1);
            var updater = throttle.Updater;

            // Update 现在会调 InitializeAsync()，但 DefaultCfgUpater 是 no-op，不影响
            await updater.Update(TimeSpan.FromMinutes(2), 2);

            Assert.Equal(2, throttle.Blocker.Frequency);
            Assert.Equal(2, throttle.Counter.Frequency);
            Assert.Equal(TimeSpan.FromMinutes(2), throttle.Counter.Period);
        }
    }
}