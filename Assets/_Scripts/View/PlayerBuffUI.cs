using BuffSystem;
using ECSFrameWork;
using System.Text;
using UnityEngine;
using UnityEngine.UI;      // 需要 Text 组件

namespace View
{
    /// <summary>
    /// 玩家头顶 Buff 列表（Milestone V1 文本版）。
    /// 挂在玩家视图 GameObject 上，由 SimulationInitializer 注入依赖并手动调用 Initialize。
    /// 每渲染帧更新 UI，数据来源为 IBuffSystem.GetBuffs（只读）。
    /// </summary>
    public class PlayerBuffUI : MonoBehaviour
    {
        [SerializeField] private Text _buffText;          // 拖入一个 Text 组件，用于显示 Buff 列表
        [SerializeField] private Vector3 _offset = new Vector3(0, 2f, 0);  // 世界空间偏移

        private IBuffSystem _buffSystem;
        private Entity _ownerEntity;
        private Transform _ownerTransform;               // 用于世界坐标转屏幕坐标
        private Camera _mainCamera;

        private readonly StringBuilder _sb = new StringBuilder();
        private readonly Vector3[] _worldCorners = new Vector3[4];

        public void Initialize(IBuffSystem buffSystem, Entity ownerEntity, Camera camera = null)
        {
            _buffSystem = buffSystem;
            _ownerEntity = ownerEntity;
            _ownerTransform = transform;                 // 该脚本挂载的 GameObject 就是实体视图
            _mainCamera = camera != null ? camera : Camera.main;

            if (_buffText == null)
                _buffText = GetComponentInChildren<Text>() ?? CreateDefaultText();
        }

        private void LateUpdate()
        {
            if (_buffSystem == null || !_ownerEntity.IsValid || _buffText == null)
                return;

            // 只读获取 Buff 列表
            var buffs = _buffSystem.GetBuffs(_ownerEntity);

            _sb.Clear();
            if (buffs.Count == 0)
            {
                _buffText.text = "";
                return;
            }

            _sb.AppendLine("Buffs:");
            for (int i = 0; i < buffs.Count; i++)
            {
                var b = buffs[i];
                // 简单文本：ConfigId、层数、剩余帧（若永久则显示 ∞）
                string timeStr = b.RemainingFrames < 0 ? "∞" : b.RemainingFrames.ToString();
                _sb.AppendLine($"  ID:{b.ConfigId} x{b.Stack} {timeStr}f");
            }

            _buffText.text = _sb.ToString();

            // 世界坐标转屏幕坐标，使 UI 跟随实体
            if (_ownerTransform != null && _mainCamera != null)
            {
                Vector3 worldPos = _ownerTransform.position + _offset;
                Vector3 screenPos = _mainCamera.WorldToScreenPoint(worldPos);
                if (screenPos.z > 0)
                {
                    _buffText.rectTransform.position = screenPos;
                }
            }
        }

        private Text CreateDefaultText()
        {
            GameObject textGO = new GameObject("BuffText");
            textGO.transform.SetParent(transform, false);
            textGO.transform.localPosition = _offset;
            Text text = textGO.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");   // 修正
            text.fontSize = 24;
            text.color = Color.yellow;
            text.alignment = TextAnchor.MiddleCenter;
            return text;
        }
    }
}