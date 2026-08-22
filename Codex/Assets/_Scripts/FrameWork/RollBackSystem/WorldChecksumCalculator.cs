using ECSFrameWork;
using System;
using System.Collections.Generic;
using BuffSystem;

namespace FrameWork.RollBackSystem
{
    public static class WorldChecksumCalculator
    {
        // 静态缓冲区，避免 Calculate 中频繁分配 GC
        private static readonly List<Entity> _entityBuffer = new List<Entity>();
        private static readonly List<Type> _typeBuffer = new List<Type>();

        /// <summary>
        /// 计算当前 ECS World 的确定性状态校验值。
        ///
        /// 确定性保证：
        /// - Entity 按 (ID, Version) 排序后再遍历。
        /// - Component 类型按 FullName 稳定排序。
        /// - 不使用 object.GetHashCode()，只对已知值类型字段做 hash。
        /// </summary>
        public static uint Calculate(
            World world)
        {
            unchecked
            {
                uint hash = 17;

                _entityBuffer.Clear();

                world.FillAliveEntities(
                    _entityBuffer);

                // 稳定排序：先 ID 后 Version
                _entityBuffer.Sort(EntityComparer.Instance);

                hash = hash * 31u + (uint)_entityBuffer.Count;

                for (int i = 0;
                     i < _entityBuffer.Count;
                     i++)
                {
                    Entity entity =
                        _entityBuffer[i];

                    hash = hash * 31u + (uint)entity.ID;
                    hash = hash * 31u + (uint)entity.Version;

                    _typeBuffer.Clear();

                    world.FillEntityComponentTypes(
                        entity,
                        _typeBuffer);

                    // 稳定排序：按 FullName
                    _typeBuffer.Sort(
                        TypeNameComparer.Instance);

                    for (int j = 0;
                         j < _typeBuffer.Count;
                         j++)
                    {
                        Type componentType =
                            _typeBuffer[j];

                        // ViewID / ViewRequest 等表现瞬时组件不属于逻辑权威状态。
                        // 必须在类型名进入 Hash 之前跳过，否则即使不 Hash 字段值也会污染逻辑 Checksum。
                        if (typeof(ILogicChecksumIgnoredComponent).IsAssignableFrom(componentType))
                            continue;

                        // FullName 确定性 hash
                        string name = componentType.FullName;
                        if (name != null)
                        {
                            for (int k = 0; k < name.Length; k++)
                                hash = hash * 31u + (uint)name[k];
                        }

                        bool success =
                            world.TryGetComponentDebugValue(
                                entity,
                                componentType,
                                out object component);

                        if (!success || component == null)
                            continue;

                        AppendComponentHash(
                            componentType,
                            component,
                            ref hash);
                    }
                }

                return hash;
            }
        }

        /// <summary>
        /// 对已知业务组件的值类型字段做确定性 hash。
        /// 新增业务组件时在 AppendKnownComponentHash 中显式添加。
        /// 未识别的组件会输出警告，避免静默漂移。
        /// </summary>
        private static void AppendComponentHash(
            Type componentType,
            object component,
            ref uint hash)
        {
            unchecked
            {
                // 优先尝试 IDeterministicHash，组件自行定义 Hash 逻辑
                if (component is IDeterministicHash dh)
                {
                    dh.AppendHash(ref hash);
                    return;
                }

                if (AppendKnownComponentHash(componentType, component, ref hash))
                    return;

                UnityEngine.Debug.LogWarning(
                    $"[Checksum] Unhashed component: {componentType.FullName}. " +
                    $"Add it to WorldChecksumCalculator.AppendKnownComponentHash.");
            }
        }

        //--------------------------------------------------------------
        // 已知组件 Hash（仅值类型字段，不使用 object.GetHashCode）
        //--------------------------------------------------------------

        private static bool AppendKnownComponentHash(Type t, object c, ref uint h)
        {
            string name = t.FullName;

            // ---- ECS Unity Components ----
            if (c is PositionComponent pos)
            {
                h = HashFloat(h, pos.x);
                h = HashFloat(h, pos.y);
                h = HashFloat(h, pos.z);
                return true;
            }
            if (c is VelocityComponent vel)
            {
                h = HashFloat(h, vel.x);
                h = HashFloat(h, vel.y);
                h = HashFloat(h, vel.z);
                return true;
            }
            if (c is MoveSpeedComponent spd)
            {
                h = HashFloat(h, spd.value);
                return true;
            }
            if (c is ViewComponent view)
            {
                h = h * 31u + (uint)view.viewID;
                return true;
            }
            if (c is PrefabViewRequestComponent pvr)
            {
                h = h * 31u + (uint)pvr.prefabID;
                return true;
            }
            if (c is ViewDestroyRequestComponent)
                return true;
            if (c is EntityDestroyRequestComponent)
                return true;
            if (c is PlayerTagComponent)
                return true;

            // ---- ECS Gameplay Components ----
            if (c is HealthComponent hp)
            {
                h = h * 31u + (uint)hp.current;
                h = h * 31u + (uint)hp.max;
                return true;
            }
            if (c is StatComponent stat)
            {
                h = h * 31u + (uint)stat.attack;
                h = h * 31u + (uint)stat.defense;
                h = h * 31u + (uint)stat.moveSpeed;
                return true;
            }
            if (c is DeadTagComponent)
                return true;
            if (c is DamageRequestComponent dmg)
            {
                h = h * 31u + (uint)dmg.source.ID;
                h = h * 31u + (uint)dmg.source.Version;
                h = h * 31u + (uint)dmg.target.ID;
                h = h * 31u + (uint)dmg.target.Version;
                h = h * 31u + (uint)dmg.amount;
                return true;
            }

            // ---- Player Input Component ----
            if (c is PlayerInputSnapshotComponent input)
            {
                h = h * 31u + (uint)input.inputFrame;
                h = h * 31u + (uint)input.playerID;
                h = HashFloat(h, input.moveX);
                h = HashFloat(h, input.moveY);
                h = HashFloat(h, input.mouseX);
                h = HashFloat(h, input.mouseY);
                h = HashFloat(h, input.mouseDeltaX);
                h = HashFloat(h, input.mouseDeltaY);
                h = HashFloat(h, input.scrollX);
                h = HashFloat(h, input.scrollY);
                h = h * 31u + (uint)input.pressedButtons;
                h = h * 31u + (uint)input.heldButtons;
                h = h * 31u + (uint)input.releasedButtons;
                return true;
            }

            // ---- Buff System Components ----
            if (c is BuffRuntimeComponent buff)
            {
                h = h * 31u + (uint)buff.target.ID;
                h = h * 31u + (uint)buff.target.Version;
                h = h * 31u + (uint)buff.source.ID;
                h = h * 31u + (uint)buff.source.Version;
                h = h * 31u + (uint)buff.configId;
                h = h * 31u + (uint)buff.runtimeHandle;
                h = h * 31u + (uint)buff.stack;
                h = h * 31u + (uint)buff.durationFrames;
                h = h * 31u + (uint)buff.remainingFrames;
                h = h * 31u + (uint)buff.tickIntervalFrames;
                h = h * 31u + (uint)buff.elapsedFrames;
                h = h * 31u + (uint)buff.ticks;
                h = h * 31u + (uint)buff.maxStack;
                h = h * 31u + (uint)buff.priority;
                h = h * 31u + (buff.unlimited ? 1u : 0u);
                h = h * 31u + (buff.isForever ? 1u : 0u);
                h = h * 31u + (uint)(int)buff.buffType;
                return true;
            }
            if (c is CompressedParallelBuffRuntimeComponent cpr)
            {
                h = h * 31u + (uint)cpr.target.ID;
                h = h * 31u + (uint)cpr.target.Version;
                h = h * 31u + (uint)cpr.source.ID;
                h = h * 31u + (uint)cpr.source.Version;
                h = h * 31u + (uint)cpr.configId;
                h = h * 31u + (uint)cpr.compressedRuntimeHandle;
                h = h * 31u + (uint)cpr.priority;
                h = h * 31u + (uint)cpr.layerCount;
                h = h * 31u + (uint)cpr.nextLayerId;
                for (int i = 0; i < cpr.layerCount; i++)
                {
                    var layer = cpr.layers.Get(i);
                    h = h * 31u + (uint)layer.layerId;
                    h = h * 31u + (uint)layer.expireFrame;
                    h = h * 31u + (uint)layer.elapsedFrames;
                    h = h * 31u + (uint)layer.ticks;
                    h = h * 31u + (uint)layer.layerRuntimeHandle;
                }
                return true;
            }
            if (c is AddBuffRequestComponent addReq)
            {
                h = h * 31u + (uint)addReq.command.Target.ID;
                h = h * 31u + (uint)addReq.command.Target.Version;
                h = h * 31u + (uint)addReq.command.Source.ID;
                h = h * 31u + (uint)addReq.command.Source.Version;
                h = h * 31u + (uint)addReq.command.ConfigId;
                h = h * 31u + (uint)addReq.command.Stack;
                return true;
            }
            if (c is RemoveBuffRequestComponent remReq)
            {
                h = h * 31u + (uint)remReq.command.Target.ID;
                h = h * 31u + (uint)remReq.command.Target.Version;
                h = h * 31u + (uint)remReq.command.Source.ID;
                h = h * 31u + (uint)remReq.command.Source.Version;
                h = h * 31u + (uint)remReq.command.ConfigId;
                h = h * 31u + (uint)remReq.command.StackCount;
                return true;
            }

            return false;
        }

        private static uint HashFloat(uint hash, float v)
        {
            unchecked
            {
                return hash * 31u + (uint)(int)(v * 10000f + 0.5f);
            }
        }

        /// <summary>
        /// Entity 按 (ID, Version) 稳定排序。
        /// </summary>
        private sealed class EntityComparer
            : IComparer<Entity>
        {
            public static readonly EntityComparer Instance =
                new EntityComparer();

            public int Compare(Entity x, Entity y)
            {
                int cmp = x.ID.CompareTo(y.ID);
                if (cmp != 0) return cmp;
                return x.Version.CompareTo(y.Version);
            }
        }

        /// <summary>
        /// Type 按 FullName 稳定排序。
        /// </summary>
        private sealed class TypeNameComparer
            : IComparer<Type>
        {
            public static readonly TypeNameComparer Instance =
                new TypeNameComparer();

            public int Compare(Type x, Type y)
            {
                return string.CompareOrdinal(
                    x.FullName,
                    y.FullName);
            }
        }
    }
}
