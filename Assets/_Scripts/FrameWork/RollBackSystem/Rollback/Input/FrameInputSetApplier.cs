using ECSFrameWork;
using System;
using System.Collections.Generic;

namespace FrameWork.RollBackSystem
{
    /// <summary>
    /// 将一帧全部玩家输入确定性写入各自 ECS Player Entity。
    /// </summary>
    public sealed class FrameInputSetApplier : IWorldInputApplier<FrameInputSet>
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
            if (!_playerEntities.Remove(playerID)) return false;
            _dirty = true;
            return true;
        }

        public void Apply(World world, FrameInputSet input)
        {
            if (!input.IsCreated) throw new InvalidOperationException("Frame Input Set Is Not Created");

            RebuildSortedPlayerIDs();

            if (input.Count != _sortedPlayerIDs.Count)
                throw new InvalidOperationException($"Frame Input Player Count Mismatch: Frame={input.frameNumber}, InputCount={input.Count}, RegisteredCount={_sortedPlayerIDs.Count}");

            for (int i = 0; i < _sortedPlayerIDs.Count; i++)
            {
                int expectedPlayerID = _sortedPlayerIDs[i];
                PlayerInputSnapshot snapshot = input.GetInputAt(i);

                if (snapshot.playerID != expectedPlayerID)
                    throw new InvalidOperationException($"Frame Input Player Mismatch: Frame={input.frameNumber}, Index={i}, ExpectedPlayerID={expectedPlayerID}, ActualPlayerID={snapshot.playerID}");

                Entity entity = _playerEntities[expectedPlayerID];

                if (!world.IsAlive(entity))
                    throw new InvalidOperationException($"Frame Input Player Entity Is Not Alive: Frame={input.frameNumber}, PlayerID={expectedPlayerID}, Entity={entity}");

                PlayerInputSnapshotComponent component = PlayerInputSnapshotComponent.FromSnapshot(in snapshot);
                world.SetComponent(entity, in component);
            }
        }

        private void RebuildSortedPlayerIDs()
        {
            if (!_dirty) return;

            _sortedPlayerIDs.Clear();
            foreach (int playerID in _playerEntities.Keys) _sortedPlayerIDs.Add(playerID);
            _sortedPlayerIDs.Sort();
            _dirty = false;
        }
    }
}