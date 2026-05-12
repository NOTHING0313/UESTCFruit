using System.Collections.Generic;

namespace FrameWork.RollBackSystem
{
    public sealed class AuthoritativeInputBuffer<TInput>
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
    }
}