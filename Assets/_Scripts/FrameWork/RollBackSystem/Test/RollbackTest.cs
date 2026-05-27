//using UnityEngine;

//namespace FrameWork.RollBackSystem.Tests
//{
//    public class RollbackTest
//        : MonoBehaviour
//    {
//        private void Start()
//        {
//            Run();
//        }

//        private void Run()
//        {
//            Debug.Log(
//                "==============================");

//            Debug.Log(
//                "Rollback Test Start");

//            Debug.Log(
//                "==============================");

//            //--------------------------------
//            // 创建 World
//            //--------------------------------

//            var world =
//                new FakeWorld();

//            Debug.Log(
//                "[Create] FakeWorld");

//            //--------------------------------
//            // 创建 Coordinator
//            //--------------------------------

//            var coordinator =
//                new RollbackCoordinator
//                    <PlayerInput, FakeSnapshot>(
//                    new InputBuffer<PlayerInput>(),
//                    new AuthoritativeInputBuffer<PlayerInput>(),
//                    new SnapshotRingBuffer<FakeSnapshot>(120),
//                    world,
//                    new PlayerInputComparer(),
//                    new ChecksumBuffer(),
//                    new AuthoritativeChecksumBuffer(),
//                    1f / 60f);

//            Debug.Log(
//                "[Create] RollbackCoordinator");

//            //--------------------------------
//            // frame0
//            //--------------------------------

//            Debug.Log(
//                "--------------------------------");

//            Debug.Log(
//                "[Frame 0] Step");

//            coordinator.Step(
//                new PlayerInput(
//                    1,
//                    0,
//                    false));

//            Debug.Log(
//                $"[Frame 0] Position = {world.GetPosition()}");

//            coordinator.SaveSnapshot();

//            Debug.Log(
//                "[Frame 0] Snapshot Saved");

//            Debug.Log(
//                $"[Frame 0] Checksum = {world.CalculateChecksum()}");

//            //--------------------------------
//            // frame1
//            //--------------------------------

//            Debug.Log(
//                "--------------------------------");

//            Debug.Log(
//                "[Frame 1] Step");

//            coordinator.Step(
//                new PlayerInput(
//                    1,
//                    0,
//                    false));

//            Debug.Log(
//                $"[Frame 1] Position = {world.GetPosition()}");

//            coordinator.SaveSnapshot();

//            Debug.Log(
//                "[Frame 1] Snapshot Saved");

//            Debug.Log(
//                $"[Frame 1] Checksum = {world.CalculateChecksum()}");

//            //--------------------------------
//            // frame2
//            //--------------------------------

//            Debug.Log(
//                "--------------------------------");

//            Debug.Log(
//                "[Frame 2] Step");

//            coordinator.Step(
//                new PlayerInput(
//                    1,
//                    0,
//                    false));

//            Debug.Log(
//                $"[Frame 2] Position = {world.GetPosition()}");

//            coordinator.SaveSnapshot();

//            Debug.Log(
//                "[Frame 2] Snapshot Saved");

//            Debug.Log(
//                $"[Frame 2] Checksum = {world.CalculateChecksum()}");

//            //--------------------------------
//            // rollback前
//            //--------------------------------

//            Debug.Log(
//                "================================");

//            Debug.Log(
//                $"[Before Rollback] Position = {world.GetPosition()}");

//            Debug.Log(
//                $"[Before Rollback] CurrentFrame = {coordinator.CurrentFrame}");

//            Debug.Log(
//                "================================");

//            //--------------------------------
//            // 服务器修正输入
//            //--------------------------------

//            Debug.Log(
//                "[Authoritative Input]");

//            Debug.Log(
//                "Frame 1 Input Corrected");

//            Debug.Log(
//                "Predicted : +1");

//            Debug.Log(
//                "Authoritative : -1");

//            //--------------------------------
//            // rollback
//            //--------------------------------

//            coordinator.ReceiveAuthoritativeInput(
//                1,
//                new PlayerInput(
//                    -1,
//                    0,
//                    false));

//            //--------------------------------
//            // rollback后
//            //--------------------------------

//            Debug.Log(
//                "================================");

//            Debug.Log(
//                $"[After Rollback] Position = {world.GetPosition()}");

//            Debug.Log(
//                $"[After Rollback] CurrentFrame = {coordinator.CurrentFrame}");

//            Debug.Log(
//                $"[After Rollback] Checksum = {world.CalculateChecksum()}");

//            Debug.Log(
//                "================================");

//            //--------------------------------
//            // 验证
//            //--------------------------------

//            int expected = 1;

//            int actual =
//                world.GetPosition();

//            bool success =
//                expected == actual;

//            Debug.Log(
//                $"[Verify] Expected = {expected}");

//            Debug.Log(
//                $"[Verify] Actual = {actual}");

//            Debug.Log(
//                $"[Verify] Result = {success}");

//            if (success)
//            {
//                Debug.Log(
//                    "Rollback Test SUCCESS");
//            }
//            else
//            {
//                Debug.LogError(
//                    "Rollback Test FAILED");
//            }

//            Debug.Log(
//                "==============================");

//            Debug.Log(
//                "Rollback Test End");

//            Debug.Log(
//                "==============================");
//        }
//    }
//}