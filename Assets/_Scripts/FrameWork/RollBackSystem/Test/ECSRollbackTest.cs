/*
 * ECSRollbackTest — 使用真实 ECS World + WorldRollbackAdapter 测试回滚流程。
 *
 * 测试流程：
 *   f1: input(+1,0) → pos.x=1
 *   f2: input(+1,0) → pos.x=2
 *   f3: input(+1,0) → pos.x=3
 *   收到 f1 权威输入(-1,0) → 回滚到 f0 → 重模拟 f1(-1,0)→f2(+1,0)→f3(+1,0)
 *   期望最终 pos.x = 1
 */

using ECSFrameWork;
using FrameWork.RollBackSystem.Interfaces;
using Simulation.Contracts;
using UnityEngine;

namespace FrameWork.RollBackSystem.Tests
{
    public class ECSRollbackTest : MonoBehaviour
    {
        private void Start()
        {
            Run();
        }

        private void Run()
        {
            bool allPassed = true;

            //--------------------------------
            // Setup ECS World
            //--------------------------------

            var world = new World();
            world.AddSystem(new InputMoveSystem());
            world.AddSystem(new MovementSystem());

            // Player entity
            var playerEntity = world.CreateEntity();
            world.SetComponent(playerEntity, new PositionComponent(0, 0, 0));
            world.SetComponent(playerEntity, new VelocityComponent(0, 0, 0));
            world.SetComponent(playerEntity, new MoveSpeedComponent(1f));
            world.SetComponent(playerEntity, new PlayerInputSnapshotComponent(0f, 0f));

            // Runner — used by WorldRollbackAdapter to drive World.Tick
            var runner = new SimulateRunner(world, 1f, 10); // tickLength=1 so pos += velocity * 1

            //--------------------------------
            // Setup Rollback Pipeline
            //--------------------------------

            var inputApplier = new PlayerSnapshotInputApplier();
            inputApplier.RegisterPlayer(1, playerEntity);

            var rollbackAdapter = new WorldRollbackAdapter<PlayerInputSnapshot>(
                snapshotProvider: world,
                world: world,
                runner: runner,
                inputApplier: inputApplier);

            var coordinator = new RollbackCoordinator<PlayerInputSnapshot, EcsWorldSnapshot>(
                inputBuffer: new InputBuffer<PlayerInputSnapshot>(),
                authoritativeInputBuffer: new AuthoritativeInputBuffer<PlayerInputSnapshot>(),
                snapshotBuffer: new SnapshotRingBuffer<EcsWorldSnapshot>(120),
                world: rollbackAdapter,
                runner: null,
                inputComparer: new PlayerInputSnapshotComparer(),
                checksumBuffer: new ChecksumBuffer(),
                authoritativeChecksumBuffer: new AuthoritativeChecksumBuffer());

            //--------------------------------
            // Capture initial snapshot (f0)
            //--------------------------------
            coordinator.SaveSnapshot();

            //--------------------------------
            // f1: +1 right
            //--------------------------------
            coordinator.Step(new PlayerInputSnapshot { moveX = 1f, moveY = 0f });
            coordinator.SaveSnapshot();
            world.TryGetComponent(playerEntity, out PositionComponent p1);
            allPassed &= AssertApprox(1f, p1.x, "f1 pos.x", "f1 : +1 right");

            //--------------------------------
            // f2: +1 right
            //--------------------------------
            coordinator.Step(new PlayerInputSnapshot { moveX = 1f, moveY = 0f });
            coordinator.SaveSnapshot();
            world.TryGetComponent(playerEntity, out PositionComponent p2);
            allPassed &= AssertApprox(2f, p2.x, "f2 pos.x", "f2 : +1 right");

            //--------------------------------
            // f3: +1 right
            //--------------------------------
            coordinator.Step(new PlayerInputSnapshot { moveX = 1f, moveY = 0f });

            coordinator.SaveSnapshot();
            world.TryGetComponent(playerEntity, out PositionComponent p3);
            allPassed &= AssertApprox(3f, p3.x, "f3 pos.x", "f3 : +1 right");

            //--------------------------------
            // Receive authoritative f1 = (-1, 0)
            //--------------------------------
            Debug.Log("--- Receive authoritative f1(-1,0) ---");
            coordinator.ReceiveAuthoritativeInput(1, new PlayerInputSnapshot { moveX = -1f, moveY = 0f });

            //--------------------------------
            // Verify result
            //--------------------------------
            world.TryGetComponent(playerEntity, out PositionComponent final);
            Debug.Log($"After rollback → pos=({final.x},{final.y}) frame={coordinator.CurrentFrame}");

            allPassed &= AssertApprox(1f, final.x, "final pos.x", "f1:-1 + f2:+1 + f3:+1");

            //--------------------------------
            // Checksum
            //--------------------------------
            coordinator.ReceiveAuthoritativeChecksum(3, WorldChecksumCalculator.Calculate(world));
            var checkResult = coordinator.VerifyChecksum(3);
            Debug.Log($"Checksum match: {checkResult.IsMatch}");
            allPassed &= checkResult.IsMatch;

            Debug.Log(allPassed ? "<color=green>[ECS Rollback Test] ALL PASSED</color>" : "<color=red>[ECS Rollback Test] SOME FAILED</color>");

            world.Dispose();
        }

        private static bool AssertApprox(float expected, float actual, string label, string context)
        {
            bool ok = Mathf.Abs(expected - actual) < 0.001f;
            if (ok)
                Debug.Log($"<color=green>[PASS]</color> {label}: {context} → {actual} (expected {expected})");
            else
                Debug.LogError($"[FAIL] {label}: expected {expected}, got {actual}  [{context}]");
            return ok;
        }
    }
}
