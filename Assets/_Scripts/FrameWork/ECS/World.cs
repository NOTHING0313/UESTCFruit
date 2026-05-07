using Contracts;   // SimulationContext

namespace ECS
{
    public class World
    {
        public EntityManager Entities = new EntityManager();
        public void Tick(SimulationContext context) { }
        public void Dispose() { }
    }

    public class EntityManager
    {
        public int Count => 0;
    }
}