using ECSFrameWork;
using Contracts;   // PlayerInputSnapshot

namespace Drivers
{
    /// <summary>
    /// ӿڣ4ṩڲʹã
    /// лʵʱģʽ / عģʽϲ Bootstrap ֻ˽ӿڡ
    /// </summary>
    public interface ISimulationDriver
    {
        int CurrentFrame { get; }
        void Step(in PlayerInputSnapshot input);
    }
}