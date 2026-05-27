using Contracts;
using UnityEngine;
using UnityEngine.UI;          // Text 组件所在命名空间

namespace View
{
    public class LogicFrameDebugPanel : MonoBehaviour
    {
        [SerializeField] private Text _frameText;
        [SerializeField] private Text _entityCountText;
        [SerializeField] private Text _checksumText;
        [SerializeField] private Text _modeText;

        private IDebugProbe _probe;

        public void Initialize(IDebugProbe probe)
        {
            _probe = probe;
        }

        public void Refresh()
        {
            if (_probe == null) return;

            // Text 组件赋值使用 .text 属性
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