using System;
using System.Collections.Generic;

namespace BuffSystem
{
    /// <summary>
    /// Buff / Effect ID Registry 的 Editor-only JSON schema。
    /// 该 schema 只服务 Editor authoring，不进入 runtime。
    /// </summary>
    [Serializable]
    internal sealed class BuffAuthoringIdRegistryData
    {
        public int version = 1;
        public int nextBuffConfigId = BuffAuthoringIdRegistryScanner.DefaultNextBuffConfigId;
        public int nextEffectId = BuffAuthoringIdRegistryScanner.DefaultNextEffectId;
        public List<BuffAuthoringIdRegistryBuffEntry> buffs = new List<BuffAuthoringIdRegistryBuffEntry>();
        public List<BuffAuthoringIdRegistryEffectEntry> effects = new List<BuffAuthoringIdRegistryEffectEntry>();
    }

    [Serializable]
    internal sealed class BuffAuthoringIdRegistryBuffEntry
    {
        public int configId;
        public string buffName;
        public string graphGuid;
        public string assetPath;
        public string status;
        public string createdAt;
        public string updatedAt;
    }

    [Serializable]
    internal sealed class BuffAuthoringIdRegistryEffectEntry
    {
        public int effectId;
        public string effectName;
        public string className;
        public string scriptPath;
        public string graphGuid;
        public string status;
        public string createdAt;
        public string updatedAt;
    }

    internal static class BuffAuthoringIdRegistryStatus
    {
        internal const string Reserved = "Reserved";
        internal const string Generated = "Generated";
        internal const string Imported = "Imported";
        internal const string Unknown = "Unknown";
    }
}
