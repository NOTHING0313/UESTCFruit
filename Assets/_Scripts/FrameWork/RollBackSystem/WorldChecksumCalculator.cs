using ECSFrameWork;
using System;
using System.Collections.Generic;

namespace FrameWork.RollBackSystem
{
    public static class WorldChecksumCalculator
    {
        public static uint Calculate(
            World world)
        {
            unchecked
            {
                uint hash = 17;

                var entities =
                    new List<Entity>();

                world.FillAliveEntities(
                    entities);

                for (int i = 0;
                     i < entities.Count;
                     i++)
                {
                    Entity entity =
                        entities[i];

                    //--------------------------------
                    // Entity
                    //--------------------------------

                    hash =
                        hash * 31u +
                        (uint)entity.ID;

                    hash =
                        hash * 31u +
                        (uint)entity.Version;

                    //--------------------------------
                    // Components
                    //--------------------------------

                    var componentTypes =
                        new List<Type>();

                    world.FillEntityComponentTypes(
                        entity,
                        componentTypes);

                    for (int j = 0;
                         j < componentTypes.Count;
                         j++)
                    {
                        Type componentType =
                            componentTypes[j];

                        hash =
                            hash * 31u +
                            (uint)componentType
                                .GetHashCode();

                        bool success =
                            world.TryGetComponentDebugValue(
                                entity,
                                componentType,
                                out object component);

                        if (!success)
                            continue;

                        if (component != null)
                        {
                            hash =
                                hash * 31u +
                                (uint)component
                                    .GetHashCode();
                        }
                    }
                }

                return hash;
            }
        }
    }
}