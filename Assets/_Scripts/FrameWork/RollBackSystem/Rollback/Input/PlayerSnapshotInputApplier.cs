/*
 * PlayerSnapshotInputApplier — implements IWorldInputApplier<PlayerInputSnapshot>
 * to bridge the RollBack system's input pipeline to ECS entities.
 *
 * For each registered player entity, writes the given frame's PlayerInputSnapshot
 * as a PlayerInputSnapshotComponent on the entity.
 */

using ECSFrameWork;
using System.Collections.Generic;

namespace FrameWork.RollBackSystem
{
    public sealed class PlayerSnapshotInputApplier
        : IWorldInputApplier<PlayerInputSnapshot>
    {
        private readonly Dictionary<int, Entity> _playerEntities = new();
        private readonly List<int> _sortedPlayerIDs = new();
        private bool _dirty;

        public int PlayerCount => _playerEntities.Count;

        public void RegisterPlayer(int playerID, Entity entity)
        {
            _playerEntities[playerID] = entity;
            _dirty = true;
        }

        public bool UnregisterPlayer(int playerID)
        {
            if (_playerEntities.Remove(playerID))
            {
                _dirty = true;
                return true;
            }
            return false;
        }

        public void Apply(World world, PlayerInputSnapshot input)
        {
            RebuildSortedList();

            for (int i = 0; i < _sortedPlayerIDs.Count; i++)
            {
                int playerID = _sortedPlayerIDs[i];
                Entity entity = _playerEntities[playerID];

                if (!world.IsAlive(entity))
                    continue;

                var component = PlayerInputSnapshotComponent.FromSnapshot(in input);
                world.SetComponent(entity, in component);
            }
        }

        private void RebuildSortedList()
        {
            if (!_dirty) return;
            _sortedPlayerIDs.Clear();
            foreach (int pid in _playerEntities.Keys)
                _sortedPlayerIDs.Add(pid);
            _sortedPlayerIDs.Sort();
            _dirty = false;
        }
    }
}
