using System.Collections.Generic;
using ECSFrameWork;
using BuffSystem;

namespace Contracts
{
    public interface IDebugProbe
    {
        int CurrentFrame { get; }
        bool IsRollbacking { get; }
        int EntityCount { get; }
        uint CurrentChecksum { get; }

        int SnapshotCount { get; }
        int LastRollbackFrame { get; }

        IReadOnlyList<BuffViewData> GetBuffs(Entity entity);
    }
}