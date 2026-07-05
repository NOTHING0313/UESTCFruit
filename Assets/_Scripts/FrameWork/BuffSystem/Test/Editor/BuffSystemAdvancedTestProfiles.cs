namespace BuffSystem.EditorTesting
{
    /// <summary>
    /// BuffSystem 高强度测试档位。默认只运行 Quick，避免 Unity Editor 被重负载测试卡住。
    /// </summary>
    internal enum BuffSystemAdvancedTestProfile
    {
        Quick,
        Standard,
        Heavy
    }

    internal readonly struct BuffSystemAdvancedTestProfileSettings
    {
        public readonly int EntityCount;
        public readonly int BuffPerEntity;
        public readonly int TickFrames;
        public readonly int FuzzIterations;
        public readonly int SoakFrames;
        public readonly int QueryIterations;
        public readonly int ChurnIterations;

        public int TotalBuffCount => EntityCount * BuffPerEntity;

        public BuffSystemAdvancedTestProfileSettings(
            int entityCount,
            int buffPerEntity,
            int tickFrames,
            int fuzzIterations,
            int soakFrames,
            int queryIterations,
            int churnIterations)
        {
            EntityCount = entityCount;
            BuffPerEntity = buffPerEntity;
            TickFrames = tickFrames;
            FuzzIterations = fuzzIterations;
            SoakFrames = soakFrames;
            QueryIterations = queryIterations;
            ChurnIterations = churnIterations;
        }

        public static BuffSystemAdvancedTestProfileSettings Create(BuffSystemAdvancedTestProfile profile)
        {
            switch (profile)
            {
                case BuffSystemAdvancedTestProfile.Standard:
                    return new BuffSystemAdvancedTestProfileSettings(2000, 10, 5000, 50000, 20000, 100000, 50000);
                case BuffSystemAdvancedTestProfile.Heavy:
                    return new BuffSystemAdvancedTestProfileSettings(10000, 20, 10000, 200000, 100000, 500000, 200000);
                default:
                    return new BuffSystemAdvancedTestProfileSettings(500, 5, 1000, 5000, 5000, 10000, 5000);
            }
        }

        public string ToParameterString()
        {
            return $"EntityCount={EntityCount}, BuffPerEntity={BuffPerEntity}, TotalBuffCount={TotalBuffCount}, TickFrames={TickFrames}, FuzzIterations={FuzzIterations}, SoakFrames={SoakFrames}, QueryIterations={QueryIterations}, ChurnIterations={ChurnIterations}";
        }
    }
}
