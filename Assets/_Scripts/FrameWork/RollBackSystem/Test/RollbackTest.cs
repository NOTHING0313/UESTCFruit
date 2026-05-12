using UnityEngine;

namespace FrameWork.RollBackSystem.Test
{
    public class RollbackTest : MonoBehaviour
    {
        private void Start()
        {
            var inputBuffer =
                new InputBuffer<PlayerInput>();

            var snapshotBuffer =
                new SnapshotRingBuffer<TestSnapshot>(120);

            var world =
                new FakeWorld();

            var rollback =
                new RollbackCoordinator
                    <PlayerInput, TestSnapshot>(
                    inputBuffer,
                    snapshotBuffer,
                    world,
                    world);

            Debug.Log("=== 初始模拟 ===");

            // 前10帧预测输入
            for (int i = 0; i < 10; i++)
            {
                rollback.Step(
                    new PlayerInput(
                        1,
                        false));

                Debug.Log(
                    $"Frame={rollback.CurrentFrame} HP={world.HP}");
            }

            Debug.Log("=== 服务器修正第5帧输入 ===");

            // 原本第5帧：
            // damage=1 crit=false
            //
            // 服务器告诉你：
            // damage=10 crit=true

            rollback.ReplaceInput(
                5,
                new PlayerInput(
                    10,
                    true));

            Debug.Log("=== 回滚到第5帧 ===");

            rollback.RollbackTo(5);

            Debug.Log(
                $"Rollback后 Frame={rollback.CurrentFrame} HP={world.HP}");

            Debug.Log("=== 重跑到10帧 ===");

            rollback.ResimulateTo(10);

            Debug.Log(
                $"Resimulate后 Frame={rollback.CurrentFrame} HP={world.HP}");
        }
    }
}