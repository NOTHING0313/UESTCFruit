/*
 * RollbackVisualTest — 回滚系统可视化测试工具。
 *
 * 挂到场景中任意 GameObject 即可（与 RollbackBootstrap 同场景）。
 *
 * 测试流程：
 *   1. Play 后按 WASD 移动玩家，屏幕左上实时显示帧号/位置/Checksum
 *   2. 按 Space：在最近一次有方向输入的帧，注入一个"翻转方向"的权威输入
 *      （比如你按了 D 向右，权威输入改为 A 向左）
 *   3. 系统回滚到该帧之前的快照，用翻转后的输入重模拟到当前帧
 *   4. 观察：位置是否跳回正确值？Checksum 是否变化？
 *
 * 通过 Console 日志可验证完整回滚链路：
 *   [FXX] auth moveX=-1  ← 收到权威输入
 *   [RollbackCoordinator] Rollback → Resimulate N frames
 *   [Checksum] Verify frame XX: match/mismatch
 */

using ECSFrameWork;
using UnityEngine;

namespace FrameWork.RollBackSystem
{
    public sealed class RollbackVisualTest : MonoBehaviour
    {
        [Header("Trigger")]
        [SerializeField] private KeyCode _rollbackKey = KeyCode.Space;

        [Header("Display")]
        [SerializeField] private bool _showGUI = true;
        [SerializeField] private int _guiFontSize = 18;

        private RollbackBootstrap _rb;
        private World _world;
        private Entity _player;

        private float _lastNonZeroMoveX;
        private int _lastNonZeroFrame;

        private string _status = "Ready. Move with WASD, press Space to trigger rollback.";
        private uint _lastChecksum;
        private uint _rollbackChecksum;

        private void Start()
        {
            _rb = FindObjectOfType<RollbackBootstrap>();
            if (_rb == null)
            {
                Debug.LogError("[RollbackVisualTest] RollbackBootstrap not found!");
                enabled = false;
                return;
            }
        }

        private void Update()
        {
            if (_rb == null || _rb.Coordinator == null) return;

            // 自动发现玩家 Entity
            if (!_player.IsValid)
            {
                _world = _rb.World;
                FindPlayerEntity();
            }

            // 记录最近一次有方向输入的帧
            if (_player.IsValid && _world.TryGetComponent<PlayerInputSnapshotComponent>(_player, out var inp))
            {
                if (inp.moveX != 0f)
                {
                    _lastNonZeroMoveX = inp.moveX;
                    _lastNonZeroFrame = inp.inputFrame;
                }
            }

            // Space 触发回滚
            if (Input.GetKeyDown(_rollbackKey))
            {
                TriggerRollback();
            }
        }

        private void TriggerRollback()
        {
            if (_rb.Coordinator == null)
            {
                Debug.LogWarning("[RollbackVisualTest] Coordinator not ready.");
                return;
            }

            if (_lastNonZeroFrame < 1)
            {
                Debug.LogWarning("[RollbackVisualTest] No movement frame recorded yet. Move first!");
                _status = "No movement frame yet. Press WASD first.";
                return;
            }

            int currentFrame = _rb.Coordinator.CurrentFrame;
            _lastChecksum = _rb.Coordinator.CalculateChecksum();

            // 翻转方向
            float flipped = _lastNonZeroMoveX > 0f ? -1f : 1f;
            var authInput = new PlayerInputSnapshot(_lastNonZeroFrame, 1)
            {
                moveX = flipped,
                moveY = 0f
            };

            Debug.Log($"[RollbackVisualTest] ═══ Triggering rollback at frame {_lastNonZeroFrame} ═══");
            Debug.Log($"[RollbackVisualTest] Predicted moveX={_lastNonZeroMoveX:F1}, Authoritative moveX={flipped:F1}");
            Debug.Log($"[RollbackVisualTest] Pre-rollback: frame={currentFrame}, checksum={_lastChecksum}");
            LogPlayerPosition("pre-rollback");

            _rb.ReceiveRemoteInput(_lastNonZeroFrame, authInput);

            _rollbackChecksum = _rb.Coordinator.CalculateChecksum();
            Debug.Log($"[RollbackVisualTest] Post-rollback: frame={_rb.Coordinator.CurrentFrame}, checksum={_rollbackChecksum}");
            LogPlayerPosition("post-rollback");
            Debug.Log($"[RollbackVisualTest] Checksum changed: {_lastChecksum != _rollbackChecksum} (expected: true if position changed)");

            _status = _lastChecksum != _rollbackChecksum
                ? $"Rollback OK! Position corrected. Checksum: {_lastChecksum} → {_rollbackChecksum}"
                : "Rollback triggered but checksum unchanged (position may be same).";

            _lastNonZeroFrame = 0;
        }

        private void FindPlayerEntity()
        {
            if (_world == null) return;
            var entities = new System.Collections.Generic.List<Entity>();
            _world.FillAliveEntities(entities);

            for (int i = 0; i < entities.Count; i++)
            {
                if (_world.HasComponent<PlayerTagComponent>(entities[i]))
                {
                    _player = entities[i];
                    return;
                }
            }
        }

        private void LogPlayerPosition(string tag)
        {
            if (!_player.IsValid) return;
            if (_world.TryGetComponent(_player, out PositionComponent pos))
                Debug.Log($"[RollbackVisualTest] {tag} player pos=({pos.x:F2}, {pos.y:F2}, {pos.z:F2})");
        }

        private void OnGUI()
        {
            if (!_showGUI || _rb == null) return;

            var style = new GUIStyle(GUI.skin.label) { fontSize = _guiFontSize };
            var coord = _rb.Coordinator;

            float y = 10;
            GUI.Label(new Rect(10, y, 600, 30), "=== Rollback Visual Test ===", style); y += 28;

            if (coord != null)
            {
                GUI.Label(new Rect(10, y, 600, 30), $"Frame: {coord.CurrentFrame}", style); y += 28;

                if (_player.IsValid && _world.TryGetComponent(_player, out PositionComponent pos))
                    GUI.Label(new Rect(10, y, 600, 30), $"Player Pos: ({pos.x:F2}, {pos.y:F2}, {pos.z:F2})", style);
                y += 28;

                GUI.Label(new Rect(10, y, 600, 30), $"Checksum: {coord.CalculateChecksum()}", style); y += 28;

                if (_lastNonZeroFrame > 0)
                    GUI.Label(new Rect(10, y, 600, 30), $"Last input: F{_lastNonZeroFrame} moveX={_lastNonZeroMoveX:F1}", style);
                y += 28;
            }

            y += 10;
            GUI.Label(new Rect(10, y, 600, 30), _status, style); y += 28;
            GUI.Label(new Rect(10, y, 600, 30), "Press SPACE to trigger rollback", style);
        }
    }
}
