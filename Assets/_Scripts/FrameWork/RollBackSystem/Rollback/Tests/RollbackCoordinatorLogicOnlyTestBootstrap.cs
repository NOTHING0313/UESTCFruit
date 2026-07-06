/*
 * 文件说明：RBS-Fix-A1-A6 logic-only 手动验证入口。
 * 使用方式：在 Unity 中挂到临时 GameObject，右键组件菜单执行 Run Logic-Only Rollback Tests。
 */

using Contracts;
using ECSFrameWork;
using FrameWork.RollBackSystem.Interfaces;
using Simulation.Contracts;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace FrameWork.RollBackSystem
{
    public sealed class RollbackCoordinatorLogicOnlyTestBootstrap : MonoBehaviour
    {
        [ContextMenu("Run Logic-Only Rollback Tests")]
        public void RunLogicOnlyRollbackTests()
        {
            TestFrameMismatchDoesNotAdvance();
            TestTryStepDoesNotTick();
            TestRollbackMissingSnapshotDoesNotAdvance();
            TestWorldRollbackAdapterRejectsUnsupportedSnapshot();
            TestResimulateChecksumUsesResimulatedFrame();
            TestAuthoritativePreArrivalUsesAuthoritativeInput();
            TestSingleInputMultiplePlayersGuard();
            TestTickMultipleDoesNotAdvanceProductionState();
            TestResimulateFailsWithoutFrameCommandReplayBoundary();
            TestWorldRollbackAdapterReplaysThroughRealApplier();
            TestConfirmFrameCleansFrameCommandHistory();

            Debug.Log("[RollbackCoordinatorLogicOnlyTestBootstrap] All logic-only rollback checks passed.");
        }

        private static void TestFrameMismatchDoesNotAdvance()
        {
            var env = CreateCoordinator();

            RollbackStepResult result = env.Coordinator.TryStep(2, 10);

            Expect(!result.Succeeded, "Frame mismatch should fail.");
            Expect(result.FailureKind == RollbackStepFailureKind.FrameMismatch, "Frame mismatch should return FrameMismatch.");
            Expect(env.Coordinator.CurrentFrame == 0, "Frame mismatch must not advance CurrentFrame.");
            Expect(env.World.SimulateCount == 0, "Frame mismatch must not call Simulate.");
        }

        private static void TestTryStepDoesNotTick()
        {
            var env = CreateCoordinator();

            RollbackStepResult result = env.Coordinator.TryStep(1, 10);

            Expect(result.Succeeded, "TryStep frame 1 should succeed.");
            Expect(env.World.SimulateCount == 1, "TryStep should call Simulate once.");
            Expect(env.World.TickCount == 0, "TryStep must not call Tick.");
            Expect(env.World.CaptureCount == 0, "TryStep must not capture snapshot.");
            Expect(env.World.CalculateChecksumFrames.Count == 0, "TryStep must not save checksum.");
        }

        private static void TestRollbackMissingSnapshotDoesNotAdvance()
        {
            var env = CreateCoordinator();
            env.Coordinator.TryStep(1, 10);

            RollbackRestoreResult result = env.Coordinator.TryRollbackTo(1);

            Expect(!result.Succeeded, "Rollback without snapshot should fail.");
            Expect(result.FailureKind == RollbackRestoreFailureKind.MissingSnapshot, "Missing snapshot should be explicit.");
            Expect(env.Coordinator.CurrentFrame == 1, "Missing snapshot must not change CurrentFrame.");
        }

        private static void TestWorldRollbackAdapterRejectsUnsupportedSnapshot()
        {
            var provider = new TestSnapshotProvider();
            var world = new World();
            var adapter = new WorldRollbackAdapter<int>(
                provider,
                world,
                new IntInputApplier(),
                null);

            RollbackRestoreResult result = adapter.TryRestore(new TestSnapshot(1));

            Expect(!result.Succeeded, "Unsupported snapshot should fail.");
            Expect(result.FailureKind == RollbackRestoreFailureKind.UnsupportedSnapshotType, "Unsupported snapshot type should be explicit.");

            world.Dispose();
        }

        private static void TestResimulateChecksumUsesResimulatedFrame()
        {
            var env = CreateCoordinator();

            for (int frame = 1; frame <= 8; frame++)
            {
                RollbackStepResult step = env.Coordinator.TryStep(frame, frame);
                Expect(step.Succeeded, $"TryStep {frame} should succeed.");

                if (frame == 5)
                    env.Coordinator.SaveSnapshot();
            }

            env.World.CalculateChecksumFrames.Clear();

            RollbackRestoreResult rollback = env.Coordinator.TryRollbackTo(5);
            Expect(rollback.Succeeded, "Rollback to frame 5 should succeed.");

            RollbackResimulateResult resimulate = env.Coordinator.TryResimulateTo(8);
            Expect(resimulate.Succeeded, "Resimulate to frame 8 should succeed.");

            Expect(env.World.CalculateChecksumFrames.Count == 3, "Resimulate should save three checksums.");
            Expect(env.World.CalculateChecksumFrames[0] == 6, "First resimulated checksum should be frame 6.");
            Expect(env.World.CalculateChecksumFrames[1] == 7, "Second resimulated checksum should be frame 7.");
            Expect(env.World.CalculateChecksumFrames[2] == 8, "Third resimulated checksum should be frame 8.");
        }

        private static void TestAuthoritativePreArrivalUsesAuthoritativeInput()
        {
            var env = CreateCoordinator();

            env.Coordinator.ReceiveAuthoritativeInput(1, 99);
            RollbackStepResult result = env.Coordinator.TryStep(1, 10);

            Expect(result.Succeeded, "Pre-arrived authoritative input should be resolved before Tick.");
            Expect(env.World.SimulatedInputs.Count == 1, "TryStep should simulate once.");
            Expect(env.World.SimulatedInputs[0] == 99, "Authoritative input should replace predicted input before Tick.");
        }

        private static void TestSingleInputMultiplePlayersGuard()
        {
            var applier = new PlayerSnapshotInputApplier();
            applier.RegisterPlayer(1, new Entity(1, 1));
            applier.RegisterPlayer(2, new Entity(2, 1));

            bool blocked = false;
            var world = new World();

            try
            {
                applier.Apply(
                    world,
                    new PlayerInputSnapshot(1, 1));
            }
            catch (SingleInputAppliedToMultiplePlayersException)
            {
                blocked = true;
            }
            finally
            {
                world.Dispose();
            }

            Expect(blocked, "Single PlayerInputSnapshot must not be applied to multiple registered players.");
        }

        private static void TestTickMultipleDoesNotAdvanceProductionState()
        {
            var env = CreateCoordinator();

            env.Coordinator.TickMultiple(2, frame => frame);

            Expect(env.Coordinator.CurrentFrame == 0, "TickMultiple must not advance Coordinator in logic-only production path.");
            Expect(env.World.SimulateCount == 0, "TickMultiple must not simulate in logic-only production path.");
        }

        private static void TestResimulateFailsWithoutFrameCommandReplayBoundary()
        {
            var inputBuffer = new InputBuffer<int>();
            inputBuffer.Save(1, 10);
            var world = new NoReplayRollbackWorld();

            var coordinator = new RollbackCoordinator<int, TestSnapshot>(
                inputBuffer,
                new AuthoritativeInputBuffer<int>(),
                new SnapshotRingBuffer<TestSnapshot>(32),
                world,
                new IntInputComparer(),
                new ChecksumBuffer(),
                new AuthoritativeChecksumBuffer());

            RollbackResimulateResult result = coordinator.TryResimulateTo(1);

            Expect(!result.Succeeded, "Resimulate must fail when frame command replay boundary is missing.");
            Expect(result.FailureKind == RollbackResimulateFailureKind.FrameCommandReplayUnavailable, "Missing replay boundary should return FrameCommandReplayUnavailable.");
            Expect(world.SimulateCount == 1, "Resimulate should apply input before detecting missing replay boundary.");
            Expect(world.TickCount == 0, "Resimulate must not tick when before-tick frame command replay is unavailable.");
        }

        private static void TestWorldRollbackAdapterReplaysThroughRealApplier()
        {
            var world = new World();
            var buffer = new SimulationFrameCommandBuffer();
            var applier = new SimulationFrameCommandApplier(world, buffer);
            var adapter = new WorldRollbackAdapter<int>(world, world, new IntInputApplier(), null);
            adapter.SetFrameCommandReplayBinding(new RollbackFrameCommandReplayBinding(buffer, applier));

            var command = new CountingFrameCommand(1);
            buffer.AddCommand(command, SimulationFrameCommandTiming.BeforeTick);

            var replay = (IRollbackFrameCommandReplay)adapter;
            bool replayed = replay.TryReplayFrameCommands(
                new SimulationContext(1, 1f / 60f, true),
                SimulationFrameCommandTiming.BeforeTick,
                out string message);

            Expect(replay.HasFrameCommandSource, "Adapter should expose the real frame command binding.");
            Expect(replayed, $"Real applier replay should succeed. {message}");
            Expect(command.ExecuteCount == 1, "Real applier replay should execute the buffered command once.");

            world.Dispose();
        }

        private static void TestConfirmFrameCleansFrameCommandHistory()
        {
            var world = new World();
            var buffer = new SimulationFrameCommandBuffer();
            var applier = new SimulationFrameCommandApplier(world, buffer);
            var adapter = new WorldRollbackAdapter<int>(world, world, new IntInputApplier(), null);
            adapter.SetFrameCommandReplayBinding(new RollbackFrameCommandReplayBinding(buffer, applier));

            var command = new CountingFrameCommand(1);
            buffer.AddCommand(command, SimulationFrameCommandTiming.BeforeTick);
            applier.ApplyCommandsToWorld(1, SimulationFrameCommandTiming.BeforeTick);

            var coordinator = new RollbackCoordinator<int, EcsWorldSnapshot>(
                new InputBuffer<int>(),
                new AuthoritativeInputBuffer<int>(),
                new SnapshotRingBuffer<EcsWorldSnapshot>(32),
                adapter,
                new IntInputComparer(),
                new ChecksumBuffer(),
                new AuthoritativeChecksumBuffer());

            coordinator.ConfirmFrame(2);

            Expect(!buffer.TryGetCommands(1, SimulationFrameCommandTiming.BeforeTick, out _), "ConfirmFrame should remove frame command history before the confirmed frame.");
            Expect(applier.AppliedFrameCount == 0, "ConfirmFrame should remove applied frame command markers before the confirmed frame.");

            world.Dispose();
        }

        private static TestEnvironment CreateCoordinator()
        {
            var inputBuffer = new InputBuffer<int>();
            var authoritativeInputBuffer = new AuthoritativeInputBuffer<int>();
            var snapshotBuffer = new SnapshotRingBuffer<TestSnapshot>(32);
            var world = new TestRollbackWorld();

            var coordinator = new RollbackCoordinator<int, TestSnapshot>(
                inputBuffer,
                authoritativeInputBuffer,
                snapshotBuffer,
                world,
                new IntInputComparer(),
                new ChecksumBuffer(),
                new AuthoritativeChecksumBuffer());

            return new TestEnvironment(coordinator, world);
        }

        private static void Expect(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private readonly struct TestEnvironment
        {
            public readonly RollbackCoordinator<int, TestSnapshot> Coordinator;
            public readonly TestRollbackWorld World;

            public TestEnvironment(RollbackCoordinator<int, TestSnapshot> coordinator, TestRollbackWorld world)
            {
                Coordinator = coordinator;
                World = world;
            }
        }

        private sealed class TestRollbackWorld : IRollbackableWorld<int>, IRollbackFrameCommandReplay, IRollbackWorldRestoreNotifier
        {
            public readonly List<int> SimulatedInputs = new List<int>();
            public readonly List<int> CalculateChecksumFrames = new List<int>();
            public readonly List<string> ReplayedFrameCommands = new List<string>();
            public int SimulateCount { get; private set; }
            public int TickCount { get; private set; }
            public int CaptureCount { get; private set; }
            public bool HasFrameCommandSource => true;

            private int _lastTickFrame;

            public void Simulate(int input, SimulationContext context)
            {
                SimulateCount++;
                SimulatedInputs.Add(input);
            }

            public void Tick(SimulationContext context)
            {
                TickCount++;
                _lastTickFrame = context.frameNumber;
            }

            public ISnapshot Capture(int frame)
            {
                CaptureCount++;
                return new TestSnapshot(frame);
            }

            public void Restore(ISnapshot snapshot)
            {
                TryRestore(snapshot);
            }

            public RollbackRestoreResult TryRestore(ISnapshot snapshot)
            {
                if (snapshot == null)
                {
                    return RollbackRestoreResult.Failure(
                        -1,
                        -1,
                        RollbackRestoreFailureKind.NullSnapshot,
                        "Snapshot is null.");
                }

                if (!(snapshot is TestSnapshot testSnapshot))
                {
                    return RollbackRestoreResult.Failure(
                        snapshot.Frame,
                        -1,
                        RollbackRestoreFailureKind.UnsupportedSnapshotType,
                        "Unsupported test snapshot.");
                }

                _lastTickFrame = testSnapshot.Frame;

                return RollbackRestoreResult.Success(
                    testSnapshot.Frame,
                    testSnapshot.Frame);
            }

            public uint CalculateChecksum()
            {
                CalculateChecksumFrames.Add(_lastTickFrame);
                return (uint)_lastTickFrame;
            }

            public bool TryReplayFrameCommands(SimulationContext context, SimulationFrameCommandTiming timing, out string message)
            {
                ReplayedFrameCommands.Add($"{context.frameNumber}:{timing}");
                message = string.Empty;
                return true;
            }

            public void NotifyRollbackResimulated(int currentFrame)
            {
            }
        }

        private sealed class NoReplayRollbackWorld : IRollbackableWorld<int>
        {
            public int SimulateCount { get; private set; }
            public int TickCount { get; private set; }

            public void Simulate(int input, SimulationContext context)
            {
                SimulateCount++;
            }

            public void Tick(SimulationContext context)
            {
                TickCount++;
            }

            public ISnapshot Capture(int frame)
            {
                return new TestSnapshot(frame);
            }

            public void Restore(ISnapshot snapshot)
            {
            }

            public RollbackRestoreResult TryRestore(ISnapshot snapshot)
            {
                return RollbackRestoreResult.Success(snapshot != null ? snapshot.Frame : -1, snapshot != null ? snapshot.Frame : -1);
            }

            public uint CalculateChecksum()
            {
                return 0;
            }
        }

        private sealed class CountingFrameCommand : ISimulationFrameCommand
        {
            public int FrameNumber { get; }
            public int ExecuteCount { get; private set; }

            public CountingFrameCommand(int frameNumber)
            {
                FrameNumber = frameNumber;
            }

            public void Execute(World world)
            {
                ExecuteCount++;
            }
        }

        private sealed class TestSnapshot : ISnapshot
        {
            public int Frame { get; }

            public TestSnapshot(int frame)
            {
                Frame = frame;
            }

            public void Release()
            {
            }
        }

        private sealed class IntInputComparer : IInputComparer<int>
        {
            public bool IsEqual(int a, int b)
            {
                return a == b;
            }
        }

        private sealed class IntInputApplier : IWorldInputApplier<int>
        {
            public void Apply(World world, int input)
            {
            }
        }

        private sealed class TestSnapshotProvider : IEcsWorldSnapshotProvider
        {
            public bool TryCaptureSnapshot(int frameNumber, out EcsWorldSnapshot snapshot, out EcsWorldSnapshotCaptureResult result)
            {
                snapshot = null;
                result = EcsWorldSnapshotCaptureResult.Failure("Capture is not used by this test.");
                return false;
            }

            public bool TryRestoreSnapshot(EcsWorldSnapshot snapshot, out EcsWorldSnapshotRestoreResult result)
            {
                result = EcsWorldSnapshotRestoreResult.Failure("Restore should not be reached for unsupported snapshot.");
                return false;
            }
        }
    }
}
