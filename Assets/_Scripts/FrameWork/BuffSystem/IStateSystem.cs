namespace BuffSystem
{
    public interface IStateSystem
    {
        int AddModifier(string stat, float value, int sourceKey); // ·µ»Ø handleId
        void RemoveModifier(int handleId);
    }
}
