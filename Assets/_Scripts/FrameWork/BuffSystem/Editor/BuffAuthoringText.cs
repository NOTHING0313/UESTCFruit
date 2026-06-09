namespace BuffSystem
{
    /// <summary>
    /// Buff 制作工具的 Editor-only 文案集中管理；不进入 runtime，不承载任何业务逻辑。
    /// </summary>
    internal static class BuffAuthoringText
    {
        internal const string HubTitle = "Buff 制作工具 / Authoring Hub";
        internal const string ValidatorTab = "配置检查器";
        internal const string CreateBuffTab = "创建 Buff";
        internal const string EffectTemplateTab = "Effect 模板";
        internal const string HubHelp = "统一入口仅整合 Buff 制作 Editor 工具；不会自动创建 asset、注册 Effect、修改 whitelist 或修改 runtime。";
        internal const string CandidateGraphLinkTitle = "候选图联动 / Candidate Graph Link";
        internal const string CandidateGraphLinkHelp = "候选图只用于可视化设计、候选审查和表单导入；不会自动创建 BuffConfigData、生成 Effect、注册 Effect、修改 whitelist 或修改 runtime。";
        internal const string CurrentCandidateGraph = "当前候选图";
        internal const string OpenGraph = "打开图";
        internal const string PingGraph = "Ping 图";
        internal const string RefreshCandidateSummary = "刷新候选摘要";
        internal const string NoCandidateGraphSelected = "未选择候选图。可以先通过 Assets / Create / BuffSystem / Buff Candidate Graph 创建审查图。";
        internal const string CandidateSummaryUnavailable = "无法读取候选图摘要。";
        internal const string GraphVersion = "GraphVersion";
        internal const string GraphComplete = "图完整";
        internal const string CanSubmitForReview = "可提交审查";
        internal const string CandidateDiagnosis = "候选诊断";
        internal const string RejectReasons = "拒绝原因";
        internal const string NextActions = "下一步";
        internal const string CandidateGraphCompare = "候选图对照";
        internal const string CandidateGraphConfigId = "候选图 ConfigId";
        internal const string RealBuffConfigExists = "真实 BuffConfigData 存在";

        internal const string ValidatorTitle = "Buff 配置检查器 / Validator";
        internal const string ScanPath = "扫描路径";
        internal const string ValidatorHelp = "该工具只读扫描 BuffConfigData asset，不会修改 asset、whitelist、runtime、scene、prefab 或 .meta。";
        internal const string ScanRefresh = "扫描 / 刷新";
        internal const string ScanResultEmpty = "点击 扫描 / 刷新 后显示扫描结果。";
        internal const string Total = "总数";
        internal const string Eligible = "候选";
        internal const string NearMiss = "接近候选";
        internal const string NotCandidate = "非候选";
        internal const string SmokeDebug = "调试 / 冒烟测试";
        internal const string Invalid = "无效";
        internal const string AssetPath = "资源路径";
        internal const string Storage = "存储模式";
        internal const string Issues = "问题列表";

        internal const string CreateBuffTitle = "Buff 创建向导 / Create Buff";
        internal const string CreateBuffHelp = "该工具只创建 BuffConfigData 草稿 asset，不会修改 runtime、production whitelist、Effect 注册、Scene、Prefab 或 .meta。";
        internal const string BasicInfo = "基础信息";
        internal const string Behavior = "行为配置";
        internal const string Effect = "Effect 配置";
        internal const string ValidationPreview = "校验预览";
        internal const string ConfigId = "Buff 配置 ID / ConfigId";
        internal const string BuffName = "Buff 名称";
        internal const string Description = "描述";
        internal const string SavePath = "保存路径";
        internal const string TargetAsset = "目标资源";
        internal const string BuffType = "Buff 类型";
        internal const string TriggerType = "触发类型";
        internal const string ParallelStorageMode = "并行存储模式";
        internal const string Unlimited = "无限时长";
        internal const string MaxStack = "最大层数";
        internal const string Duration = "持续时间";
        internal const string TickTime = "Tick 间隔";
        internal const string StackUpPolicy = "叠加策略";
        internal const string StackDownPolicy = "移除策略";
        internal const string EffectId = "Effect ID / EffectId";
        internal const string EffectRegistered = "Effect 已注册";
        internal const string EffectNote = "Effect 备注";
        internal const string CanCreate = "可创建";
        internal const string ConfigIdDuplicate = "ConfigId 重复";
        internal const string CompressedEligibility = "压缩并行资格";
        internal const string Category = "分类";
        internal const string Validate = "校验";
        internal const string CreateDraftAsset = "创建草稿配置";
        internal const string ImportCreateBuffFromGraph = "从候选图导入基础字段";
        internal const string OpenAuthoringValidator = "打开配置检查器";
        internal const string CancelClose = "取消 / 关闭";

        internal const string EffectTemplateTitle = "Effect 模板生成器";
        internal const string EffectTemplateHelp = "该面板只生成 Effect .cs 草稿模板，不会自动修改 BuffEffectRegistryBootstrap、runtime、whitelist 或 Buff asset。";
        internal const string EffectClassName = "Effect 类名";
        internal const string EffectDisplayNameNote = "Effect 显示名 / 备注";
        internal const string TargetFolder = "目标文件夹";
        internal const string Namespace = "命名空间";
        internal const string TargetFile = "目标文件";
        internal const string CallbackSelection = "回调选择";
        internal const string EventEffectTemplateHelp = "Event Effect 模板将在后续阶段评估；第一版不生成 OnEvent。";
        internal const string CanGenerate = "可生成";
        internal const string ProductionRegistryStatus = "生产注册表状态";
        internal const string EffectIdUsedByBuffConfigData = "EffectId 已被 BuffConfigData 使用";
        internal const string EffectIdConstFoundInEffects = "Effects 中发现 EffectId 常量";
        internal const string ClassNameValid = "类名合法";
        internal const string FileExists = "文件已存在";
        internal const string EffectIdSourceHits = "EffectId 来源命中";
        internal const string GenerateTemplate = "生成 Effect 模板";
        internal const string ImportEffectFromGraph = "从候选图导入 Effect 字段";
        internal const string CopyRegistrySnippet = "复制注册代码片段";
        internal const string OpenEffectFolder = "打开 Effect 文件夹";
        internal const string Clear = "清空";

        internal const string Errors = "错误";
        internal const string Warnings = "警告";
        internal const string Recommendations = "建议";
        internal const string None = "无";
        internal const string Unknown = "未知";
        internal const string True = "是";
        internal const string False = "否";
        internal const string NotValidated = "未校验";

        internal const string CategoryEligibleCandidate = "候选 / Eligible Candidate";
        internal const string CategoryNearMiss = "接近候选 / Near Miss";
        internal const string CategoryNotCandidate = "非候选 / Not Candidate";
        internal const string CategorySmokeDebugOnly = "调试 / 冒烟测试专用";
        internal const string CategoryInvalid = "无效 / Invalid";
    }
}
