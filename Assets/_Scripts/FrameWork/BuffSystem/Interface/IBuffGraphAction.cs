namespace BuffSystem
{
    /// <summary>
    /// 图形化 Buff Authoring 生成 Effect 调用链时使用的 runtime-safe 功能接口。
    /// 实现类不得持有需要回滚的私有状态；需要持久化的状态必须写入 ECS Component。
    /// </summary>
    public interface IBuffGraphAction
    {
        void Execute(in BuffEffectContext context);
    }
}
