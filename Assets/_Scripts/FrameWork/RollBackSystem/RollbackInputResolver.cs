using FrameWork.RollBackSystem.Interfaces;

namespace FrameWork.RollBackSystem
{
    public sealed class RollbackInputResolver<TInput>
    {
        private readonly IInputBuffer<TInput> _predictedBuffer;

        private readonly AuthoritativeInputBuffer<TInput>
            _authoritativeBuffer;

        private readonly IInputComparer<TInput> _comparer;

        public RollbackInputResolver(
            IInputBuffer<TInput> predictedBuffer,
            AuthoritativeInputBuffer<TInput> authoritativeBuffer,
            IInputComparer<TInput> comparer)
        {
            _predictedBuffer = predictedBuffer;
            _authoritativeBuffer = authoritativeBuffer;
            _comparer = comparer;
        }

        public InputComparisonResult Compare(int frame)
        {
            bool predictedFound =
                _predictedBuffer.TryGet(
                    frame,
                    out var predicted);

            bool authoritativeFound =
                _authoritativeBuffer.TryGet(
                    frame,
                    out var authoritative);

            if (!predictedFound || !authoritativeFound)
            {
                return new InputComparisonResult(
                    false,
                    frame);
            }

            bool isEqual =
                _comparer.IsEqual(
                    predicted,
                    authoritative);

            return new InputComparisonResult(
                !isEqual,
                frame);
        }
    }
}