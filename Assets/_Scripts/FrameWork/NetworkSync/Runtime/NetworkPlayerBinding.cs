using ECSFrameWork;

namespace FrameWork.NetworkSync
{
    /// <summary>网络会话 PlayerID 与 ECS Player Entity 的确定性绑定。</summary>
    public readonly struct NetworkPlayerBinding
    {
        public int PlayerID { get; }
        public Entity Entity { get; }

        public NetworkPlayerBinding(int playerID,Entity entity)
        {
            PlayerID=playerID;
            Entity=entity;
        }
    }
}
