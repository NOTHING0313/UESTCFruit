using Contracts;
using UnityEngine;
using UnityEngine.UI;

namespace View
{
    /// <summary>
    /// 挂载到 Canvas 下 Panel 的调试面板，实时显示当前帧、实体数、校验和及模式。
    /// 每渲染帧调用 Refresh() 更新 UI。
    /// </summary>
    public sealed class LogicFrameDebugPanel : MonoBehaviour
    {
        [Header("UI 绑定")]
        [SerializeField] private Text _frameText;
        [SerializeField] private Text _entityCountText;
        [SerializeField] private Text _checksumText;
        [SerializeField] private Text _modeText;
        private IDebugProbe _probe;
        /// <summary>由 Bootstrap 调用，注入探针实例。</summary>
        public void Initialize(IDebugProbe probe)
        {
            _probe = probe;
        }
        /// <summary>每渲染帧调用，将探针数据刷新到 UI 文本。</summary>
        public void Refresh()
        {
            if (_probe == null)
                return;
            if (_frameText != null)
                _frameText.text = $"Frame: {_probe.CurrentFrame}";
            if (_entityCountText != null)
                _entityCountText.text = $"Entities: {_probe.EntityCount}";
            if (_checksumText != null)
                _checksumText.text = $"Checksum: {_probe.CurrentChecksum:X8}";
            if (_modeText != null)
                _modeText.text = _probe.IsRollbacking ? "ROLLBACK" : "NORMAL";
        }
    }
}
