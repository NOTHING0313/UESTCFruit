/*
 * RollbackTest — 纯逻辑测试，不依赖 Unity。
 *
 * 测试流程：
 *   f1: +1 → pos=1
 *   f2: +1 → pos=2
 *   f3: +1 → pos=3
 *   收到 f1 权威输入(-1) → 回滚到 f0 (pos=0)
 *   重模拟 f1(-1)→f2(+1)→f3(+1) → 最终 pos=1
 *
 * 在 Unity 中作为 MonoBehaviour 挂载，结果通过 Debug.Log 输出。
 */

using System.Text;
using UnityEngine;

namespace FrameWork.RollBackSystem.Tests
{
    public class RollbackTest : MonoBehaviour
    {
        private void Start()
        {
            Run();
        }

        private void Run()
        {
            var log = new StringBuilder();
            bool allPassed = true;

            //--------------------------------
            // Setup
            //--------------------------------

            var world = new FakeWorld();
            var coordinator = new RollbackCoordinator<PlayerInput, FakeSnapshot>(
                inputBuffer:             new InputBuffer<PlayerInput>(),
                authoritativeInputBuffer: new AuthoritativeInputBuffer<PlayerInput>(),
                snapshotBuffer:          new SnapshotRingBuffer<FakeSnapshot>(120),
                world:                   world,
                inputComparer:           new PlayerInputComparer(),
                checksumBuffer:          new ChecksumBuffer(),
                authoritativeChecksumBuffer: new AuthoritativeChecksumBuffer()
            );

            log.AppendLine("=== Rollback Test ===");

            //--------------------------------
            // Frame 1: predict +1
            //--------------------------------
            coordinator.Step(new PlayerInput(1, 0, false));
            coordinator.SaveSnapshot();
            log.AppendLine($"f1 Step(+1) → pos={world.Position} (expect 1)");
            allPassed &= AssertEquals(1, world.Position, "f1 pos", log);

            //--------------------------------
            // Frame 2: predict +1
            //--------------------------------
            coordinator.Step(new PlayerInput(1, 0, false));
            coordinator.SaveSnapshot();
            log.AppendLine($"f2 Step(+1) → pos={world.Position} (expect 2)");
            allPassed &= AssertEquals(2, world.Position, "f2 pos", log);

            //--------------------------------
            // Frame 3: predict +1
            //--------------------------------
            coordinator.Step(new PlayerInput(1, 0, false));
            coordinator.SaveSnapshot();
            log.AppendLine($"f3 Step(+1) → pos={world.Position} (expect 3)");
            allPassed &= AssertEquals(3, world.Position, "f3 pos", log);

            //--------------------------------
            // Authoritative: f1 was actually -1
            //--------------------------------
            log.AppendLine("--- Receive authoritative f1(-1) ---");
            coordinator.ReceiveAuthoritativeInput(1, new PlayerInput(-1, 0, false));

            log.AppendLine($"After rollback → pos={world.Position} frame={coordinator.CurrentFrame}");
            log.AppendLine($"Expect pos=1 (f1:-1 + f2:+1 + f3:+1)");

            //--------------------------------
            // Verify
            //--------------------------------
            allPassed &= AssertEquals(1, world.Position, "final pos", log);
            allPassed &= AssertEquals(3, coordinator.CurrentFrame, "final frame", log);

            //--------------------------------
            // Checksum verify
            //--------------------------------
            coordinator.ReceiveAuthoritativeChecksum(3, world.CalculateChecksum());
            var result = coordinator.VerifyChecksum(3);
            log.AppendLine($"Checksum match: {result.IsMatch}");
            allPassed &= AssertTrue(result.IsMatch, "checksum match", log);

            //--------------------------------
            // Result
            //--------------------------------
            log.AppendLine(allPassed ? "=== ALL PASSED ===" : "=== SOME FAILED ===");
            Debug.Log(log.ToString());
        }

        private bool AssertEquals(int expected, int actual, string label, StringBuilder log)
        {
            if (expected == actual) return true;
            log.AppendLine($"  FAIL [{label}]: expected {expected}, got {actual}");
            return false;
        }

        private bool AssertTrue(bool condition, string label, StringBuilder log)
        {
            if (condition) return true;
            log.AppendLine($"  FAIL [{label}]: expected true");
            return false;
        }
    }
}
