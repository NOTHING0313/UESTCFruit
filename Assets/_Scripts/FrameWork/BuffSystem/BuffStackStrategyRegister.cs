using System.Collections.Generic;
using Utility;
namespace BuffSystem
{
    public class BuffStackStrategyRegister : Singleton<BuffStackStrategyRegister>
    {
        protected override bool _isDonDestroyOnLoad => true;
        private readonly Dictionary<string, IBuffStackStrategy> _map = new();
        protected override void Awake()
        {
            base.Awake();
            #region Up
            Register(new ResetRunTimeBuffStackUpStrategy());
            Register(new AddDurationBuffStackUpStrategy());
            Register(new AddStackOnlyBuffStackUpStrategy());
            Register(new CyclicallyAddStackOnlyBuffStackUpStrategy());
            Register(new AddStackAndResetRuntimeBuffStackUpStrategy());
            #endregion
            #region Down
            Register(new ReduceBuffStackDownStrategy());
            Register(new ClearBuffStackDownStrategy());
            #endregion
            #region Parallel Up
            Register(new ParallelAppendStackUpStrategy());
            Register(new ParallelRefreshEarliestUpStrategy());
            Register(new ParallelRefreshAllUpStrategy());
            Register(new ParallelReplaceEarliestWhenFullUpStrategy());
            #endregion
            #region Parallel Down
            Register(new ParallelRemoveEarliestDownStrategy());
            Register(new ParallelRemoveLatestDownStrategy());
            Register(new ParallelClearAllDownStrategy());
            #endregion
        }
        public void Register(IBuffStackStrategy buffStackStrategy)
        {
            if (!_map.ContainsKey(buffStackStrategy.ID))
                _map.Add(buffStackStrategy.ID, buffStackStrategy);
        }
        public bool TryGet(string id, out IBuffStackStrategy s) => _map.TryGetValue(id, out s);
    }
}
