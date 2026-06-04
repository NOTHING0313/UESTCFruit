/*
 * RollbackInputTest — 挂到场景中，Space 触发回滚。
 * 依赖：场景中已有 RollbackBootstrap + RollbackDemoSetup 正常运行。
 */

using ECSFrameWork;
using UnityEngine;

namespace FrameWork.RollBackSystem.Tests
{
    public class RollbackInputTest : MonoBehaviour
    {
        [SerializeField] private KeyCode _rollbackKey = KeyCode.Space;

        private RollbackBootstrap _rollback;
        private float _lastNonZeroHorizontal;
        private int _lastNonZeroFrame;

        private void Start()
        {
            _rollback = FindObjectOfType<RollbackBootstrap>();
        }

        private void Update()
        {
            if (_rollback == null) return;

            float h = 0f;
            if (Input.GetKey(KeyCode.D)) h = 1f;
            if (Input.GetKey(KeyCode.A)) h = -1f;

            if (h != 0f)
            {
                _lastNonZeroHorizontal = h;
                _lastNonZeroFrame = _rollback.Coordinator?.CurrentFrame ?? 0;
            }

            if (Input.GetKeyDown(_rollbackKey))
            {
                if (_lastNonZeroFrame < 1)
                {
                    Debug.Log("[RollbackInputTest] No non-zero frame yet.");
                    return;
                }

                float flipped = _lastNonZeroHorizontal > 0f ? -1f : 1f;
                var auth = new PlayerInputSnapshot { moveX = flipped, moveY = 0f };

                Debug.Log($"[RollbackInputTest] f{_lastNonZeroFrame} auth moveX={flipped}");
                _rollback.ReceiveRemoteInput(_lastNonZeroFrame, auth);
            }
        }
    }
}
