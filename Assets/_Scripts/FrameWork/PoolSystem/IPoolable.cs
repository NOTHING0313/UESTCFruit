namespace PoolSystem
{
    public interface IPoolable
    {
        void OnGetFromPool();
        void OnReleaseToPool();
    }
}