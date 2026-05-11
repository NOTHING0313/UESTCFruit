using Contracts;
using ECSFrameWork;
using System.Collections.Generic;

namespace BuffSystem
{
    public interface IBuffSystem
    {
        void Tick(World world, SimulationContext context);
        void AddBuff(AddBuffCommand command);
        void RemoveBuff(RemoveBuffCommand command);
        bool TryGetBuff(Entity target, int configId, Entity source, out BuffViewData data);
        IReadOnlyList<BuffViewData> GetBuffs(Entity target);
    }

    public readonly struct AddBuffCommand { }
    public readonly struct RemoveBuffCommand { }
}