using ECSFrameWork;
using System;
using System.Reflection;

namespace FrameWork.RollBackSystem
{
    /// <summary>
    /// 多玩家 FrameInputSet 与 Rollback 本地性能基线。
    /// </summary>
    public static class MultiPlayerPerformanceBaselineTestBootstrap
    {
        private const uint Seed = 20260817u;
        private const float TickLength = 1f / 60f;
        private const int NormalWarmupFrames = 1000;
        private const int NormalMeasureFrames = 10000;
        private const int RollbackWarmupCount = 10;
        private const int RollbackMeasureCount = 100;
        private const int RollbackGapFrames = 10;

        /// <summary>
        /// 测量多人正常帧的 Build、Compare、Step/Apply、Tick、Snapshot 与总成本。
        /// </summary>
        public static NormalPerformanceReport RunNormalBaseline(int playerCount)
        {
            ValidatePlayerCount(playerCount);

            using var env = CreateEnvironment(playerCount, false);
            var comparer = new FrameInputSetComparer();

            for (int frame = 1; frame <= NormalWarmupFrames; frame++)
                DriveFrame(env, frame, CreateFrameInputSet(frame, playerCount), true);

            CollectGarbageForMeasurement();

            var buildMetric = new MetricAccumulator(NormalMeasureFrames);
            var compareMetric = new MetricAccumulator(NormalMeasureFrames);
            var stepMetric = new MetricAccumulator(NormalMeasureFrames);
            var tickMetric = new MetricAccumulator(NormalMeasureFrames);
            var snapshotMetric = new MetricAccumulator(NormalMeasureFrames);
            var totalMetric = new MetricAccumulator(NormalMeasureFrames);

            int endFrame = NormalWarmupFrames + NormalMeasureFrames;

            for (int frame = NormalWarmupFrames + 1; frame <= endFrame; frame++)
            {
                long totalAllocBefore = AllocationCounter.Read();

                long allocBefore = AllocationCounter.Read();
                long begin = Timestamp();
                FrameInputSet input = CreateFrameInputSet(frame, playerCount);
                long end = Timestamp();
                long allocAfter = AllocationCounter.Read();
                long buildTicks = end - begin;
                buildMetric.Add(buildTicks, AllocationDelta(allocBefore, allocAfter));

                allocBefore = AllocationCounter.Read();
                begin = Timestamp();
                bool equal = comparer.IsEqual(input, input);
                end = Timestamp();
                allocAfter = AllocationCounter.Read();
                long compareTicks = end - begin;
                if (!equal) throw new InvalidOperationException($"Performance Compare Error: Frame={frame}");
                compareMetric.Add(compareTicks, AllocationDelta(allocBefore, allocAfter));

                allocBefore = AllocationCounter.Read();
                begin = Timestamp();
                RollbackStepResult stepResult = env.Coordinator.TryStep(frame, input);
                end = Timestamp();
                allocAfter = AllocationCounter.Read();
                long stepTicks = end - begin;

                if (!stepResult.Succeeded)
                    throw new InvalidOperationException($"Performance TryStep Error: Frame={frame}, Kind={stepResult.FailureKind}, Message={stepResult.Message}");

                stepMetric.Add(stepTicks, AllocationDelta(allocBefore, allocAfter));

                allocBefore = AllocationCounter.Read();
                begin = Timestamp();
                var context = new SimulationContext(frame, TickLength, false);
                env.CommandApplier.ApplyCommandsToWorld(frame, SimulationFrameCommandTiming.BeforeTick);
                env.World.Tick(in context);
                env.CommandApplier.ApplyCommandsToWorld(frame, SimulationFrameCommandTiming.AfterTick);
                end = Timestamp();
                allocAfter = AllocationCounter.Read();
                long tickTicks = end - begin;
                tickMetric.Add(tickTicks, AllocationDelta(allocBefore, allocAfter));

                allocBefore = AllocationCounter.Read();
                begin = Timestamp();
                env.Coordinator.SaveSnapshot();
                end = Timestamp();
                allocAfter = AllocationCounter.Read();
                long snapshotTicks = end - begin;
                snapshotMetric.Add(snapshotTicks, AllocationDelta(allocBefore, allocAfter));

                long totalAllocAfter = AllocationCounter.Read();
                totalMetric.Add(
                    buildTicks + compareTicks + stepTicks + tickTicks + snapshotTicks,
                    AllocationDelta(totalAllocBefore, totalAllocAfter));
            }

            return new NormalPerformanceReport(
                playerCount,
                NormalWarmupFrames,
                NormalMeasureFrames,
                buildMetric.Summarize(),
                compareMetric.Summarize(),
                stepMetric.Summarize(),
                tickMetric.Summarize(),
                snapshotMetric.Summarize(),
                totalMetric.Summarize());
        }

        /// <summary>
        /// 测量一次 ReceiveAuthoritativeInput 所包含的 Restore + Resimulation 成本。
        /// </summary>
        public static RollbackPerformanceReport RunRollbackBaseline(int playerCount, int rollbackDepth)
        {
            ValidatePlayerCount(playerCount);
            if (rollbackDepth <= 0) throw new ArgumentOutOfRangeException(nameof(rollbackDepth));

            using var reference = CreateEnvironment(playerCount, true);
            using var predicted = CreateEnvironment(playerCount, true);

            var metric = new MetricAccumulator(RollbackMeasureCount);
            int currentFrame = 0;
            int totalRollbackCount = RollbackWarmupCount + RollbackMeasureCount;

            for (int rollbackIndex = 0; rollbackIndex < totalRollbackCount; rollbackIndex++)
            {
                if (rollbackIndex == RollbackWarmupCount) CollectGarbageForMeasurement();

                int correctionFrame = currentFrame + RollbackGapFrames;
                int receiveFrame = correctionFrame + rollbackDepth;
                FrameInputSet authoritativeCorrection = default;

                for (int frame = currentFrame + 1; frame <= receiveFrame; frame++)
                {
                    FrameInputSet authoritative = CreateFrameInputSet(frame, playerCount);
                    FrameInputSet local = authoritative;

                    if (frame == correctionFrame)
                    {
                        authoritativeCorrection = authoritative;
                        local = CreateWrongLastPlayerFrameInputSet(frame, playerCount);
                    }

                    DriveFrame(reference, frame, authoritative, true);
                    DriveFrame(predicted, frame, local, true);
                }

                bool measured = rollbackIndex >= RollbackWarmupCount;

                if (measured)
                {
                    long allocBefore = AllocationCounter.Read();
                    long begin = Timestamp();

                    predicted.Coordinator.ReceiveAuthoritativeInput(correctionFrame, authoritativeCorrection);

                    long end = Timestamp();
                    long allocAfter = AllocationCounter.Read();
                    metric.Add(end - begin, AllocationDelta(allocBefore, allocAfter));
                }
                else predicted.Coordinator.ReceiveAuthoritativeInput(correctionFrame, authoritativeCorrection);

                if (predicted.Coordinator.CurrentFrame != receiveFrame)
                    throw new InvalidOperationException($"Performance Rollback CurrentFrame Error: Players={playerCount}, Depth={rollbackDepth}, Expected={receiveFrame}, Actual={predicted.Coordinator.CurrentFrame}");

                AssertWorldEqual(reference, predicted, receiveFrame, playerCount, rollbackDepth, rollbackIndex);
                currentFrame = receiveFrame;
            }

            return new RollbackPerformanceReport(
                playerCount,
                rollbackDepth,
                rollbackDepth + 1,
                RollbackWarmupCount,
                RollbackMeasureCount,
                metric.Summarize());
        }

        private static TestEnvironment CreateEnvironment(int playerCount, bool saveInitialSnapshot)
        {
            var world = new World { EnableSystemProfile = false };
            var players = new Entity[playerCount];
            var inputApplier = new FrameInputSetApplier();

            for (int i = 0; i < playerCount; i++)
            {
                int playerID = i + 1;
                Entity player = world.CreateEntity();
                players[i] = player;

                world.SetComponent(player, new PlayerTagComponent());
                world.SetComponent(player, new PlayerInputSnapshotComponent(0, playerID, 0f, 0f));
                world.SetComponent(player, new MoveSpeedComponent(2.5f + i * 0.1f));
                world.SetComponent(player, new VelocityComponent(0f, 0f, 0f));
                world.SetComponent(player, new PositionComponent(i * 3f, 0f, 0f));
                inputApplier.RegisterPlayer(playerID, player);
            }

            world.AddSystem(new InputMoveSystem());
            world.AddSystem(new MovementSystem());

            var commandBuffer = new SimulationFrameCommandBuffer(512);
            var commandApplier = new SimulationFrameCommandApplier(world, commandBuffer, 512);
            var rollbackAdapter = new WorldRollbackAdapter<FrameInputSet>(world, world, inputApplier, null);
            rollbackAdapter.SetFrameCommandReplayBinding(new RollbackFrameCommandReplayBinding(commandBuffer, commandApplier));

            var snapshotBuffer = new SnapshotRingBuffer<EcsWorldSnapshot>(512);
            var coordinator = new RollbackCoordinator<FrameInputSet, EcsWorldSnapshot>(
                new InputBuffer<FrameInputSet>(),
                new AuthoritativeInputBuffer<FrameInputSet>(),
                snapshotBuffer,
                rollbackAdapter,
                new FrameInputSetComparer(),
                new ChecksumBuffer(),
                new AuthoritativeChecksumBuffer())
            {
                TickLength = TickLength
            };

            var environment = new TestEnvironment(world, players, coordinator, commandBuffer, commandApplier, snapshotBuffer);
            if (saveInitialSnapshot) coordinator.SaveSnapshot();
            return environment;
        }

        private static void DriveFrame(TestEnvironment env, int frame, FrameInputSet input, bool saveSnapshot)
        {
            RollbackStepResult result = env.Coordinator.TryStep(frame, input);

            if (!result.Succeeded)
                throw new InvalidOperationException($"Performance DriveFrame Error: Frame={frame}, Kind={result.FailureKind}, Message={result.Message}");

            var context = new SimulationContext(frame, TickLength, false);
            env.CommandApplier.ApplyCommandsToWorld(frame, SimulationFrameCommandTiming.BeforeTick);
            env.World.Tick(in context);
            env.CommandApplier.ApplyCommandsToWorld(frame, SimulationFrameCommandTiming.AfterTick);

            if (saveSnapshot) env.Coordinator.SaveSnapshot();
        }

        private static FrameInputSet CreateFrameInputSet(int frame, int playerCount)
        {
            var inputs = new PlayerInputSnapshot[playerCount];

            for (int i = 0; i < playerCount; i++)
                inputs[i] = CreatePlayerInput(frame, i + 1);

            return new FrameInputSet(frame, inputs);
        }

        private static FrameInputSet CreateWrongLastPlayerFrameInputSet(int frame, int playerCount)
        {
            var inputs = new PlayerInputSnapshot[playerCount];

            for (int i = 0; i < playerCount; i++)
                inputs[i] = CreatePlayerInput(frame, i + 1);

            PlayerInputSnapshot wrong = inputs[playerCount - 1];
            wrong.moveX = wrong.moveX == 1f ? -1f : 1f;
            wrong.moveY = wrong.moveY == -1f ? 1f : -1f;
            inputs[playerCount - 1] = wrong;

            return new FrameInputSet(frame, inputs);
        }

        private static PlayerInputSnapshot CreatePlayerInput(int frame, int playerID)
        {
            uint state = Seed ^ unchecked((uint)frame * 0x9E3779B9u) ^ unchecked((uint)playerID * 0x85EBCA6Bu);
            state = NextRandom(state);
            float moveX = (int)(state % 3u) - 1;
            state = NextRandom(state);
            float moveY = (int)(state % 3u) - 1;

            return new PlayerInputSnapshot(frame, playerID)
            {
                moveX = moveX,
                moveY = moveY
            };
        }

        private static uint NextRandom(uint value)
        {
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
            return value;
        }

        private static void AssertWorldEqual(TestEnvironment reference, TestEnvironment predicted, int frame, int playerCount, int rollbackDepth, int rollbackIndex)
        {
            if (reference.World.AliveEntityCount != predicted.World.AliveEntityCount)
                throw new InvalidOperationException($"Performance Rollback AliveEntityCount Error: Players={playerCount}, Depth={rollbackDepth}, Rollback={rollbackIndex}, Frame={frame}");

            for (int i = 0; i < reference.Players.Length; i++)
            {
                Entity a = reference.Players[i], b = predicted.Players[i];

                if (!reference.World.TryGetComponent(a, out PositionComponent positionA) || !predicted.World.TryGetComponent(b, out PositionComponent positionB))
                    throw new InvalidOperationException($"Performance Rollback Position Missing Error: Player={i + 1}, Frame={frame}");

                if (FloatBits(positionA.x) != FloatBits(positionB.x) || FloatBits(positionA.y) != FloatBits(positionB.y) || FloatBits(positionA.z) != FloatBits(positionB.z))
                    throw new InvalidOperationException($"Performance Rollback Position Error: Players={playerCount}, Depth={rollbackDepth}, Rollback={rollbackIndex}, Frame={frame}, Player={i + 1}");

                if (!reference.World.TryGetComponent(a, out VelocityComponent velocityA) || !predicted.World.TryGetComponent(b, out VelocityComponent velocityB))
                    throw new InvalidOperationException($"Performance Rollback Velocity Missing Error: Player={i + 1}, Frame={frame}");

                if (FloatBits(velocityA.x) != FloatBits(velocityB.x) || FloatBits(velocityA.y) != FloatBits(velocityB.y) || FloatBits(velocityA.z) != FloatBits(velocityB.z))
                    throw new InvalidOperationException($"Performance Rollback Velocity Error: Players={playerCount}, Depth={rollbackDepth}, Rollback={rollbackIndex}, Frame={frame}, Player={i + 1}");

                if (!reference.World.TryGetComponent(a, out PlayerInputSnapshotComponent inputA) || !predicted.World.TryGetComponent(b, out PlayerInputSnapshotComponent inputB))
                    throw new InvalidOperationException($"Performance Rollback Input Missing Error: Player={i + 1}, Frame={frame}");

                if (inputA.inputFrame != inputB.inputFrame || inputA.playerID != inputB.playerID ||
                   FloatBits(inputA.moveX) != FloatBits(inputB.moveX) || FloatBits(inputA.moveY) != FloatBits(inputB.moveY))
                    throw new InvalidOperationException($"Performance Rollback Input Error: Players={playerCount}, Depth={rollbackDepth}, Rollback={rollbackIndex}, Frame={frame}, Player={i + 1}");
            }

            uint checksumA = WorldChecksumCalculator.Calculate(reference.World);
            uint checksumB = WorldChecksumCalculator.Calculate(predicted.World);

            if (checksumA != checksumB)
                throw new InvalidOperationException($"Performance Rollback Checksum Error: Players={playerCount}, Depth={rollbackDepth}, Rollback={rollbackIndex}, Frame={frame}, A=0x{checksumA:X8}, B=0x{checksumB:X8}");
        }

        private static void ValidatePlayerCount(int playerCount)
        {
            if (playerCount <= 0) throw new ArgumentOutOfRangeException(nameof(playerCount));
        }

        private static long Timestamp() => System.Diagnostics.Stopwatch.GetTimestamp();

        private static int FloatBits(float value) => BitConverter.SingleToInt32Bits(value);

        private static long AllocationDelta(long before, long after)
            => before < 0 || after < 0 ? -1 : Math.Max(0, after - before);

        private static void CollectGarbageForMeasurement()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        private sealed class MetricAccumulator
        {
            private readonly long[] _samples;
            private int _count;
            private long _totalTicks;
            private long _maxTicks;
            private long _allocatedBytes;
            private int _allocationSampleCount;

            public MetricAccumulator(int capacity) => _samples = new long[capacity];

            public void Add(long ticks, long allocatedBytes)
            {
                _samples[_count++] = ticks;
                _totalTicks += ticks;
                if (ticks > _maxTicks) _maxTicks = ticks;

                if (allocatedBytes < 0) return;
                _allocatedBytes += allocatedBytes;
                _allocationSampleCount++;
            }

            public PerformanceMetricSummary Summarize()
            {
                if (_count == 0) return default;

                Array.Sort(_samples, 0, _count);
                int p95Index = Math.Max(0, (int)Math.Ceiling(_count * 0.95) - 1);

                double tickToUs = 1_000_000d / System.Diagnostics.Stopwatch.Frequency;
                double allocation = _allocationSampleCount == 0 ? -1d : (double)_allocatedBytes / _allocationSampleCount;

                return new PerformanceMetricSummary(
                    _count,
                    _totalTicks / (double)_count * tickToUs,
                    _samples[p95Index] * tickToUs,
                    _maxTicks * tickToUs,
                    allocation);
            }
        }

        private static class AllocationCounter
        {
            private static readonly Func<long> Getter = CreateGetter();

            public static long Read() => Getter != null ? Getter() : -1;

            private static Func<long> CreateGetter()
            {
                MethodInfo method = typeof(GC).GetMethod("GetAllocatedBytesForCurrentThread", BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
                if (method == null || method.ReturnType != typeof(long)) return null;

                try { return (Func<long>)Delegate.CreateDelegate(typeof(Func<long>), method); }
                catch { return null; }
            }
        }

        /// <summary>单项性能统计。</summary>
        public readonly struct PerformanceMetricSummary
        {
            public readonly int SampleCount;
            public readonly double AverageUs;
            public readonly double P95Us;
            public readonly double MaxUs;
            public readonly double AllocatedBytesPerSample;

            public PerformanceMetricSummary(int sampleCount, double averageUs, double p95Us, double maxUs, double allocatedBytesPerSample)
            {
                SampleCount = sampleCount;
                AverageUs = averageUs;
                P95Us = p95Us;
                MaxUs = maxUs;
                AllocatedBytesPerSample = allocatedBytesPerSample;
            }

            public string ToDisplayString()
            {
                string alloc = AllocatedBytesPerSample < 0 ? "N/A" : $"{AllocatedBytesPerSample:F1} B";
                return $"Avg={AverageUs:F3} us | P95={P95Us:F3} us | Max={MaxUs:F3} us | Alloc={alloc}";
            }
        }

        /// <summary>正常多人逻辑帧性能结果。</summary>
        public sealed class NormalPerformanceReport
        {
            public readonly int PlayerCount;
            public readonly int WarmupFrames;
            public readonly int MeasuredFrames;
            public readonly PerformanceMetricSummary Build;
            public readonly PerformanceMetricSummary Compare;
            public readonly PerformanceMetricSummary StepApply;
            public readonly PerformanceMetricSummary Tick;
            public readonly PerformanceMetricSummary Snapshot;
            public readonly PerformanceMetricSummary Total;

            public NormalPerformanceReport(int playerCount, int warmupFrames, int measuredFrames, PerformanceMetricSummary build, PerformanceMetricSummary compare, PerformanceMetricSummary stepApply, PerformanceMetricSummary tick, PerformanceMetricSummary snapshot, PerformanceMetricSummary total)
            {
                PlayerCount = playerCount;
                WarmupFrames = warmupFrames;
                MeasuredFrames = measuredFrames;
                Build = build;
                Compare = compare;
                StepApply = stepApply;
                Tick = tick;
                Snapshot = snapshot;
                Total = total;
            }

            public string ToDisplayString()
            {
                return
                    $"[NORMAL {PlayerCount}P] Warmup={WarmupFrames}, Measure={MeasuredFrames}\n" +
                    $"Build FrameInputSet : {Build.ToDisplayString()}\n" +
                    $"Compare             : {Compare.ToDisplayString()}\n" +
                    $"TryStep + Apply     : {StepApply.ToDisplayString()}\n" +
                    $"World.Tick          : {Tick.ToDisplayString()}\n" +
                    $"Snapshot + Checksum : {Snapshot.ToDisplayString()}\n" +
                    $"Measured Total      : {Total.ToDisplayString()}";
            }
        }

        /// <summary>多人 Rollback 性能结果。</summary>
        public sealed class RollbackPerformanceReport
        {
            public readonly int PlayerCount;
            public readonly int RollbackDepth;
            public readonly int ResimulatedFrames;
            public readonly int WarmupCount;
            public readonly int MeasuredCount;
            public readonly PerformanceMetricSummary Rollback;

            public RollbackPerformanceReport(int playerCount, int rollbackDepth, int resimulatedFrames, int warmupCount, int measuredCount, PerformanceMetricSummary rollback)
            {
                PlayerCount = playerCount;
                RollbackDepth = rollbackDepth;
                ResimulatedFrames = resimulatedFrames;
                WarmupCount = warmupCount;
                MeasuredCount = measuredCount;
                Rollback = rollback;
            }

            public string ToDisplayString()
            {
                double avgPerFrame = ResimulatedFrames > 0 ? Rollback.AverageUs / ResimulatedFrames : 0d;

                return
                    $"[ROLLBACK {PlayerCount}P / Depth={RollbackDepth}] Warmup={WarmupCount}, Measure={MeasuredCount}\n" +
                    $"Actual Resimulated Frames={ResimulatedFrames}\n" +
                    $"Restore + Resimulate : {Rollback.ToDisplayString()}\n" +
                    $"Avg / Resim Frame     : {avgPerFrame:F3} us";
            }
        }

        private sealed class TestEnvironment : IDisposable
        {
            public readonly World World;
            public readonly Entity[] Players;
            public readonly RollbackCoordinator<FrameInputSet, EcsWorldSnapshot> Coordinator;
            public readonly SimulationFrameCommandBuffer CommandBuffer;
            public readonly SimulationFrameCommandApplier CommandApplier;
            public readonly SnapshotRingBuffer<EcsWorldSnapshot> SnapshotBuffer;

            public TestEnvironment(World world, Entity[] players, RollbackCoordinator<FrameInputSet, EcsWorldSnapshot> coordinator, SimulationFrameCommandBuffer commandBuffer, SimulationFrameCommandApplier commandApplier, SnapshotRingBuffer<EcsWorldSnapshot> snapshotBuffer)
            {
                World = world;
                Players = players;
                Coordinator = coordinator;
                CommandBuffer = commandBuffer;
                CommandApplier = commandApplier;
                SnapshotBuffer = snapshotBuffer;
            }

            public void Dispose()
            {
                SnapshotBuffer.Clear();
                CommandBuffer.Clear();
                World.Dispose();
            }
        }
    }
}