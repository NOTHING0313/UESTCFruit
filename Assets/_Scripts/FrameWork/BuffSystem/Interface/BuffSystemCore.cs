using Contracts;
using ECS;
using System.Collections.Generic;

namespace BuffSystem
{
    public class BuffSystemCore : IBuffSystem
    {
        public void Tick(World world, SimulationContext context) { }
        public void AddBuff(AddBuffCommand command) { }
        public void RemoveBuff(RemoveBuffCommand command) { }
        public bool TryGetBuff(EntityHandle target, int configId, EntityHandle source, out BuffViewData data) { data = default; return false; }
        public IReadOnlyList<BuffViewData> GetBuffs(EntityHandle target) => new List<BuffViewData>();
        public void Dispose() { }
    }
}