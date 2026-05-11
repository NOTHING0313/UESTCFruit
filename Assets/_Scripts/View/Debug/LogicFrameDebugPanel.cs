using Contracts;
using UnityEngine;

namespace View
{
    /// <summary>
    /// 逻辑帧调试面板空壳。
    /// 后续可以接入 Text / TMP_Text，把 IDebugProbe 中的帧号、实体数量、校验值等数据显示到 Canvas。
    /// </summary>
    public sealed class LogicFrameDebugPanel : MonoBehaviour
    {
        private IDebugProbe _probe;

        /// <summary>绑定调试数据读取接口。</summary>
        public void Initialize(IDebugProbe probe)
        {
            _probe = probe;
        }

        /// <summary>刷新调试面板数据。</summary>
        public void Refresh()
        {
            if (_probe == null)
                return;

            Debug.Log($"[LogicFrameDebugPanel] Frame={_probe.CurrentFrame}, EntityCount={_probe.EntityCount}, Checksum={_probe.CurrentChecksum}");
        }
    }
}
