using FrameWork.RollBackSystem.Interfaces;
using System.Collections.Generic;

namespace FrameWork.RollBackSystem
{
    /// <summary>
    /// ¿˙ ∑ ‰»Îª∫¥Ê°£
    /// </summary>
    public sealed class InputBuffer<TInput> : IInputBuffer<TInput>
    {
        private readonly Dictionary<int, TInput> _inputs =
            new Dictionary<int, TInput>();

        public void Save(int frame, in TInput input)
        {
            _inputs[frame] = input;
        }

        public bool TryGet(int frame, out TInput input)
        {
            return _inputs.TryGetValue(frame, out input);
        }

        public void ClearBefore(int frame)
        {
            if (_inputs.Count == 0)
                return;

            List<int> removeList = null;

            foreach (KeyValuePair<int, TInput> pair in _inputs)
            {
                if (pair.Key >= frame)
                    continue;

                removeList ??= new List<int>();
                removeList.Add(pair.Key);
            }

            if (removeList == null)
                return;

            for (int i = 0; i < removeList.Count; i++)
                _inputs.Remove(removeList[i]);
        }
    }
}