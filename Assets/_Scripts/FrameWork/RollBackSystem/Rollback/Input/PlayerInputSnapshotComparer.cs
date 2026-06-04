using ECSFrameWork;
using FrameWork.RollBackSystem.Interfaces;

namespace FrameWork.RollBackSystem
{
    public sealed class PlayerInputSnapshotComparer
        : IInputComparer<PlayerInputSnapshot>
    {
        public bool IsEqual(PlayerInputSnapshot a, PlayerInputSnapshot b)
        {
            return a.playerID == b.playerID
                && Approximately(a.moveX, b.moveX)
                && Approximately(a.moveY, b.moveY)
                && Approximately(a.mouseX, b.mouseX)
                && Approximately(a.mouseY, b.mouseY)
                && Approximately(a.mouseDeltaX, b.mouseDeltaX)
                && Approximately(a.mouseDeltaY, b.mouseDeltaY)
                && Approximately(a.scrollX, b.scrollX)
                && Approximately(a.scrollY, b.scrollY)
                && a.heldButtons == b.heldButtons
                && a.pressedButtons == b.pressedButtons
                && a.releasedButtons == b.releasedButtons;
        }

        private static bool Approximately(float a, float b)
        {
            if (a == b) return true;
            float diff = a - b;
            return diff < 0.0001f && diff > -0.0001f;
        }
    }
}
