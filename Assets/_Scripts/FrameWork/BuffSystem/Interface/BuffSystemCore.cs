using Contracts;
using ECSFrameWork;
using System.Collections.Generic;

namespace BuffSystem
{
    public class BuffSystemCore : IBuffSystem
    {
        public void Tick(World world, SimulationContext context) { }
        public void AddBuff(AddBuffCommand command) { }
        public void RemoveBuff(RemoveBuffCommand command) { }
        public bool TryGetBuff(Entity target, int configId, Entity source, out BuffViewData data) { data = default; return false; }
        public IReadOnlyList<BuffViewData> GetBuffs(Entity target) => new List<BuffViewData>();
        public void Dispose() { }
    }
}