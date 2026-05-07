using Contracts;
using ECS;
using System.Collections.Generic;

namespace BuffSystem
{
    public interface IBuffSystem
    {
        void Tick(World world, SimulationContext context);
        void AddBuff(AddBuffCommand command);
        void RemoveBuff(RemoveBuffCommand command);
        bool TryGetBuff(EntityHandle target, int configId, EntityHandle source, out BuffViewData data);
        IReadOnlyList<BuffViewData> GetBuffs(EntityHandle target);
    }

    public readonly struct AddBuffCommand { }
    public readonly struct RemoveBuffCommand { }
}