using Contracts;
using UnityEngine;

namespace View
{
    /// <summary>
    /// 调试面板空壳（4号实现，挂载到 Canvas）。
    /// 后续通过 IDebugProbe 刷新数据显示帧号、实体数等。
    /// </summary>
    public sealed class LogicFrameDebugPanel : MonoBehaviour
    {
        public void Initialize(IDebugProbe probe) { }
        public void Refresh() { }
    }
}