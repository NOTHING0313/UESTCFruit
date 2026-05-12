using System.Collections.Generic;
using FrameWork.RollBackSystem.Interfaces;

namespace FrameWork.RollBackSystem
{
    public sealed class InputBuffer<TInput>
        : IInputBuffer<TInput>
    {
        private readonly Dictionary<int, TInput> _inputs
            = new Dictionary<int, TInput>();

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
            var removeList = new List<int>();

            foreach (var pair in _inputs)
            {
                if (pair.Key < frame)
                {
                    removeList.Add(pair.Key);
                }
            }

            foreach (var key in removeList)
            {
                _inputs.Remove(key);
            }
        }
    }
}