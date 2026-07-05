using BuffSystem;
using ECSFrameWork;
using System.Collections.Generic;

namespace View
{
    /// <summary>
    /// View Debug 面板使用的 Buff 查询快照；只承载展示数据，不作为 BuffSystem 运行时真状态。
    /// </summary>
    internal sealed class BuffDebugSnapshot
    {
        public int ConfigId;
        public Entity Target;
        public Entity Source;
        public bool TargetAlive;
        public bool SourceAlive;
        public bool Found;
        public BuffViewData View;
        public int GetBuffsCount;
        public int MatchingViewCount;
        public int EntityPerStackRuntimeCount;
        public int CompressedRuntimeCount;
        public int ConfigEntityPerStackRuntimeCount;
        public int ConfigCompressedRuntimeCount;
        public List<BuffDebugViewRow> ViewRows;

        public BuffDebugSnapshot()
        {
            ViewRows = new List<BuffDebugViewRow>();
        }
    }
}
