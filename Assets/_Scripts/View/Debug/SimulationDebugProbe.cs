using Contracts;
using BuffSystem;
using ECS;
using System.Collections.Generic;

namespace View
{
    /// <summary>
    /// 调试探针空壳（4号实现）。
    /// 为调试面板提供只读数据，当前返回固定值。
    /// </summary>
    public sealed class SimulationDebugProbe : IDebugProbe
    {
        public int CurrentFrame => 0;
        public bool IsRollbacking => false;
        public int EntityCount => 0;
        public uint CurrentChecksum => 0;
        public IReadOnlyList<BuffViewData> GetBuffs(EntityHandle entity) => new List<BuffViewData>();
    }
}