# BuffSystem Authoring Guide
## Phase 3I-11V CompositeEffect 文档 closeout

CompositeEffect 图形化生成的完整使用流程、推荐图结构、失败路径、验证清单和清理清单已集中归档到：

```text
Assets/_Scripts/FrameWork/BuffSystem/Docs/BuffSystem_CompositeEffectAuthoring.md
```

当前推荐入口：

```text
Tools / BuffSystem / Authoring Hub
-> 图形化编辑
-> Graph Generate / CompositeEffect 区域
```

数值编辑仍用于手动创建 Buff / Effect 模板；图形化编辑用于候选图审查、CompositeEffect 预览、CompositeEffect 草稿生成，以及 Buff + CompositeEffect 一键草稿生成。

推荐流程：

```text
1. 创建 / 打开 BuffCandidateGraph
2. 放置 CandidateStart
3. 并列连接 BuffShapeNode / Eligibility / Risk / Decision
4. 放置 EffectCompositionRootNode
5. 从 EffectCompositionRootNode 连接多个 EffectNode
6. 在 EffectNode 生命周期端口连接 ScriptActionNode
7. 设置 EffectNode.Next 或 ExecutionOrder
8. 设置 ScriptActionNode.Next 或 ExecutionOrder
9. 预览 CompositeEffect 代码
10. 从图创建 CompositeEffect 草稿
11. 或一键创建 Buff + CompositeEffect 草稿
12. 等待 Unity 编译
13. 使用 Validator / Runner / 场景验证
14. 人工决定是否进入 whitelist
```

一键 Buff + CompositeEffect 写入顺序：

```text
1. CompositeEffect preflight
2. Buff preflight
3. 写 CompositeEffect.cs
4. 写 Effect ID Registry
5. Bootstrap auto 注册 CompositeEffect
6. 创建 BuffConfigData asset
7. BuffConfigData.EffectId = CompositeEffectId
8. 写 Buff ID Registry
9. AssetDatabase Save / Refresh
```

自动注册关闭或失败时，一键流程不会创建 `BuffConfigData`，避免 Buff 指向未注册 Effect。生成 CompositeEffect 不等于 production-ready，创建 BuffConfigData 不等于进入 whitelist，自动注册不等于 rollback-ready；Graph 只是 authoring artifact，不是 runtime truth。

## Phase 3I-11U 一键创建 Buff + CompositeEffect 草稿

Authoring Hub 的 `图形化编辑 -> Graph Generate -> CompositeEffect 预览 / Composite Preview` 区域提供 `从图一键创建 Buff + CompositeEffect 草稿`。该按钮用于把一个候选图落地为最终 CompositeEffect 草稿和一个指向该 CompositeEffect 的 `BuffConfigData` 草稿。

一键流程：

```text
BuffCandidateGraph
-> BuffGraphGeneratePlan
-> BuffGraphCompositeEffectPlan
-> BuffGraphCompositeEffectEmitter
-> 写入 CompositeEffect.cs
-> 写入 Effect ID Registry
-> Settings 开启时维护 BuffEffectRegistryBootstrap auto 区块
-> 自动注册成功后创建 BuffConfigData asset
-> BuffConfigData.EffectId = CompositeEffectId
-> 写入 Buff ID Registry
```

关键边界：

```text
BuffConfigData.EffectId 必须指向最终 CompositeEffectId。
一键流程只注册 CompositeEffect，不注册 child EffectNode。
EffectBindingNode 是 legacy 信息，Composite 一键流程会忽略它。
自动注册关闭时不会创建 BuffConfigData，避免 Buff 指向未注册 Effect。
自动注册失败时不会创建 BuffConfigData，也不会回滚已生成的 CompositeEffect.cs 或 Effect ID Registry。
生成 BuffConfigData 不等于进入 production whitelist。
```

生成后需要等待 Unity 编译，并运行 Validator / Runner / 场景验证。是否进入 whitelist 仍需要单独人工审批；该流程不修改 runtime，不证明 rollback-ready。

## Phase 3I-11T CompositeEffect 真实生成

Authoring Hub 的 `图形化编辑 -> Graph Generate -> CompositeEffect 预览 / Composite Preview` 区域提供 `从图创建 CompositeEffect 草稿`。该按钮会把当前 `BuffCandidateGraph` 的多个 `EffectNode` / `ScriptActionNode` 合成为一个普通 `BuffEffectExecutorBase` 派生类 `.cs` 草稿。

真实生成链路：

```text
BuffCandidateGraph
-> BuffGraphGeneratePlan
-> BuffGraphCompositeEffectPlan
-> BuffGraphCompositeEffectEmitter
-> 写入 CompositeEffect.cs
-> 写入 Effect ID Registry
-> 按 Settings 可选维护 BuffEffectRegistryBootstrap auto 区块
```

`CompositeEffectId` 来源规则：

```text
1. 优先使用 EffectCompositionRootNode.FinalEffectId。
2. 如果 FinalEffectId <= 0 且 Settings 开启自动分配，则分配正式段 EffectId。
3. 如果 FinalEffectId <= 0 且自动分配关闭，则阻止生成。
4. 如果 EffectId 已被占用，或位于 990000+ Debug / Smoke / Reserved 保留段，则阻止生成。
```

`CompositeEffectClassName` 来源规则：

```text
1. 优先使用 EffectCompositionRootNode.FinalEffectClassName。
2. 其次使用 BuffName + CompositeEffect。
3. 最后使用 GraphName + CompositeEffect。
```

目标路径使用 `Settings.EffectScriptDefaultFolder`，目标文件为 `<CompositeEffectClassName>.cs`。目标 `.cs` 已存在时会阻止生成，不覆盖已有文件。

自动注册关闭时，工具不会修改 `BuffEffectRegistryBootstrap.cs`，只在生成结果中显示 `registry.Register(...)` 手动注册片段。生成后仍需等待 Unity 编译，并运行 Validator / Runner / 场景验证。该流程不会创建 `BuffConfigData`，不会加入 whitelist，不证明 rollback-ready。

## Phase 3I-11R CompositeEffect 预览

Authoring Hub 的 `图形化编辑 -> Graph Generate` 区域提供 `CompositeEffect 预览 / Composite Preview`。该区域用于在真实生成前检查多个 `EffectNode` 与 `ScriptActionNode` 合成后的 CompositeEffect 代码结构。

预览按钮只执行以下只读链路：

```text
BuffCandidateGraph
-> BuffGraphGeneratePlan
-> BuffGraphCompositeEffectPlan
-> BuffGraphCompositeEffectEmitter
-> 代码文本预览
```

预览不会占用 EffectId，不会写 `.cs` 文件，不会创建 `BuffConfigData`，不会写 ID Registry，不会修改 `BuffEffectRegistryBootstrap`，也不会自动注册 Effect。复制按钮只把完整预览代码复制到剪贴板。

如果预览报 Error，应先修正候选图中的 EffectNode 顺序、ScriptActionNode 顺序、Action 类型或 CompositeEffectClassName。Warning / Info 只用于提示设计边界，例如单 EffectNode 可能不需要 CompositeEffect、`OnStackChanged` Action 当前不接收 delta，以及真实生成后只应注册 CompositeEffectId。

## Phase 3I-11Q CompositeEffect 说明

CompositeEffect 用于把多个 `EffectNode` 合成为一个普通 `BuffEffectExecutorBase` 派生类。它只发生在 Editor authoring / codegen 阶段，不改变 BuffSystem runtime。

当前 runtime 结构保持不变：

```text
BuffConfigData 仍然只绑定一个 EffectId。
BuffDefinition 仍然只暴露一个 EffectId。
BuffSystemCore 仍然按单 EffectId 查找 IBuffEffectExecutor。
```

CompositeEffect 的推荐落地方式：

```text
多个 EffectNode
-> Editor 生成一个 CompositeEffect 代码文本
-> 后续阶段由用户确认后生成一个 CompositeEffect.cs 草稿
-> BuffConfigData.EffectId 指向 CompositeEffectId
-> 只注册 CompositeEffectId，不注册子 EffectNode 的 EffectId
```

Phase 3I-11Q 只新增 Editor-only plan / builder / emitter，不接入 Hub 按钮，不生成 `.cs` 文件，不创建 BuffConfigData，不写 ID Registry，也不自动注册 Effect。真正生成文件和一键创建流程留到后续阶段。

CompositeEffect 顺序规则：

```text
EffectNode.Next 完整链优先表示跨 Effect 的显式顺序。
未使用 EffectNode.Next 时，按 EffectNode.ExecutionOrder 升序。
ScriptActionNode.Next 完整链优先表示同 Effect + 同 lifecycle 内的 Action 顺序。
未使用 ScriptActionNode.Next 时，按 ScriptActionNode.ExecutionOrder 升序。
Next 链与 ExecutionOrder 冲突、重复顺序、分叉或环都会阻止 CompositeEffect plan 通过。
CompositeEffect 合并同一生命周期时，先按 Effect 顺序，再按 Action 顺序。
OnStackChanged 第一版仍只调用 Execute(in context)，不向 Action 传递 delta。
```

旧 `EffectBindingNode` 只保留为 legacy fallback。新图应使用 `EffectCompositionRootNode + EffectNode`；当图中存在新组合结构时，Bridge / Generate / CompositeEffect 计划都会优先读取新结构，旧 `EffectBindingNode` 不作为 CompositeEffect 组成部分。

## Phase 3I-11O-UXCleanup 数值页与图形化入口

数值编辑页用于手动创建 Buff 草稿与 Effect 模板。候选图相关创建流程已经统一迁移到 `Tools / BuffSystem / Authoring Hub -> 图形化编辑 -> Graph Generate`。

Create Buff 页不再显示“从候选图导入基础字段”。Effect Template 页不再显示“从候选图导入 Effect 字段 / 调用链”。如需从候选图生成 Buff / Effect 草稿，请使用 Graph Generate 区域。
## Phase 3I-11O Effect 自动注册说明

Authoring Hub Settings 中新增 `自动注册 Effect 到 Bootstrap` 开关，默认开启。开启后，Effect Template 或 Graph Generate 在成功生成 Effect `.cs` 草稿并写入 ID Registry 后，会维护 `BuffEffectRegistryBootstrap.cs` 中的 auto 区块。

```text
// <buffsystem-auto-effect-registry>
registry.Register(200001, new GeneratedPoisonEffect());
// </buffsystem-auto-effect-registry>
```

该能力只维护 auto 区块，不会修改手工注册区。自动注册失败时，已生成的 Effect 草稿和 ID Registry 不会回滚，工具会显示失败原因和可手动复制的 `registry.Register(...)` 片段。

边界保持不变：自动注册不加入 whitelist，不修改 BuffSystem runtime core，不修改 compressed eligibility，不代表 Validator / Runner / Unity 场景验证通过，也不证明 rollback-ready。

## Phase 3I-11M 鍥惧舰鍖栦竴閿敓鎴愭祦绋?
鍦?`Tools / BuffSystem / Authoring Hub` 鐨?`鍥惧舰鍖栫紪杈慲 妯″紡涓紝閫夋嫨 `BuffCandidateGraph` 鍚庡彲浠ヤ娇鐢?`鍥惧舰鍖栫敓鎴?/ Graph Generate` 鍖哄煙銆?
褰撳墠鏀寔涓変釜鎿嶄綔锛?
```text
浠庡浘鍒涘缓涓?Effect 鑽夌
浠庡浘鍒涘缓 Buff 鑽夌
浠庡浘涓€閿垱寤?Buff + 涓?Effect 鑽夌
```

鐢熸垚鍓嶅伐鍏蜂細鍏堟瀯寤?`Graph Generate Plan`锛屽苟渚濇鎵ц鐜版湁鏍￠獙閾捐矾锛?
```text
ID 鑷姩鍒嗛厤 / 鍞竴鎬ф牎楠?Graph codegen preflight
Effect preflight
Buff preflight
```

浠讳綍 Error 閮戒細闃绘鍐欏叆銆俉arning / Info 鍙綔涓烘彁绀烘樉绀哄湪鐢熸垚璁″垝鎴栫粨鏋滀腑銆?
涓€閿垱寤烘祦绋嬩細鍏堢敓鎴愪富 Effect `.cs` 鑽夌锛屽啀鍒涘缓 `BuffConfigData` 鑽夌 asset銆備袱涓楠ゆ垚鍔熷悗锛屽伐鍏蜂細鍦ㄥ唴閮ㄩ粦绠辨洿鏂?ID Registry銆傝嫢 Effect 鑽夌宸茬敓鎴愪絾 Buff 鑽夌鍒涘缓澶辫触锛屽伐鍏蜂笉浼氳嚜鍔ㄥ垹闄ゅ凡鐢熸垚鐨?Effect 鑽夌锛岄渶瑕佺敤鎴锋牴鎹粨鏋滄彁绀烘墜鍔ㄦ鏌ユ垨娓呯悊銆?
涓?Effect 閫夋嫨瑙勫垯锛?
```text
鍙€夋嫨 ExecutionOrder 鏈€灏忕殑 EffectNode
澶?EffectNode 浠呮樉绀?warning
绗竴鐗堜笉鐢熸垚 CompositeEffect
```

杈圭晫锛?
```text
涓嶄細鑷姩娉ㄥ唽 Effect
涓嶄細淇敼 BuffEffectRegistryBootstrap
涓嶄細鑷姩鍔犲叆 whitelist
涓嶄細淇敼 BuffSystem runtime
涓嶄細璇佹槑 rollback-ready
鐢熸垚鐨?Buff / Effect 浠嶉渶 Validator銆丷unner銆乁nity 鍦烘櫙楠岃瘉鍜屼汉宸ュ鎵?```

### Graph Effect 璋冪敤閾捐鏄?
濡傛灉涓?`EffectNode` 鐨勭敓鍛藉懆鏈熺鍙ｈ繛鎺ヤ簡鏈夋晥 `ScriptActionNode`锛岀敓鎴愮殑 Effect 鑽夌浼氳嚜鍔ㄥ寘鍚細

```text
readonly action 瀛楁
瀵瑰簲鐢熷懡鍛ㄦ湡 override
action.Execute(in context) 璋冪敤
```

绀轰緥璋冪敤閾鹃瑙堬細

```text
OnApply: <none>
OnTick: 0 TestGraphAction
OnRemove: <none>
OnRefresh: <none>
OnStackChanged: <none>
```

闇€瑕佹墜鍐欑殑鏄?Action 鑴氭湰鍐呴儴鐨勭帺娉曢€昏緫锛岃€屼笉鏄?Effect 鐢熷懡鍛ㄦ湡璋冪敤閾撅細

```text
Effect 璋冪敤閾撅細鐢卞伐鍏风敓鎴?Action.Execute(in context)锛氱敱鐢ㄦ埛瀹炵幇鍏蜂綋鐜╂硶閫昏緫
Effect 娉ㄥ唽锛氫粛闇€浜哄伐瀹℃壒骞舵墜鍔ㄦ帴鍏?registry
```

濡傛灉鐢熷懡鍛ㄦ湡绔彛杩炴帴鐨勬槸 `EmptyActionPlaceholderNode`锛屽畠鍙細浜х敓 warning锛屼笉浼氱敓鎴愬彲杩愯 Action 璋冪敤銆傝灏嗗崰浣嶈妭鐐规浛鎹负鏈夋晥 `ScriptActionNode`銆?
## 1. 宸ュ叿鍏ュ彛

BuffSystem authoring 宸ュ叿缁熶竴鍏ュ彛涓猴細

```text
Tools / BuffSystem / Authoring Hub
```

褰撳墠 Hub 鍖呭惈涓変釜 tab锛?
```text
Validator
Create Buff
Effect Template
```

- `Validator`锛氭壂鎻?`Assets/Resources/BuffSystem/Buff` 涓嬬殑 BuffConfigData锛屾鏌ュ瓧娈点€丒ffect 娉ㄥ唽鐘舵€併€乧ompressed eligibility 鍜屽€欓€夊垎绫汇€?- `Create Buff`锛氬垱寤?BuffConfigData 鑽夌 asset锛屾彁渚?ConfigId / EffectId / compressed eligibility 棰勬鏌ャ€?- `Effect Template`锛氱敓鎴?Effect `.cs` 鑽夌妯℃澘锛屾彁渚?EffectId 娉ㄥ唽妫€鏌ュ拰 registry snippet 澶嶅埗銆?
Hub 椤堕儴褰撳墠鍖呭惈涓変釜妯″紡锛?
```text
鏁板€肩紪杈?鍥惧舰鍖栫紪杈?Settings
```

- `鏁板€肩紪杈慲锛氫繚鐣?`閰嶇疆妫€鏌ュ櫒 / 鍒涘缓 Buff / Effect 妯℃澘` 涓変釜鍘熸湁宸ュ叿銆?- `鍥惧舰鍖栫紪杈慲锛氱敤浜庨€夋嫨銆佸垱寤恒€佹墦寮€鍜屽鏌?`BuffCandidateGraph`銆?- `Settings`锛氱敤浜庨厤缃?Authoring Hub 鐨勫伐鍏疯矾寰勫亸濂姐€?
## xNode 鍊欓€夊浘宸ヤ綔娴?
濡傛灉闇€瑕佸厛鍋氬浘褰㈠寲鍊欓€夎璁″拰椋庨櫓瀹℃煡锛屽彲浠ヤ娇鐢細

```text
Assets / Create / BuffSystem / Buff Candidate Graph
```

鐒跺悗鍦細

```text
Tools / BuffSystem / Authoring Hub
```

椤堕儴鐨?`鍊欓€夊浘鑱斿姩 / Candidate Graph Link` 涓€夋嫨璇ュ浘銆?
涔熷彲浠ュ湪 Authoring Hub 鐨?`鍥惧舰鍖栫紪杈慲 妯″紡涓偣鍑伙細

```text
鍒涘缓鍥?```

璇ユ寜閽細鍦?Settings 鐨?`鍥鹃粯璁ょ洰褰昤 涓垱寤烘柊鐨?`BuffCandidateGraph`锛屽苟鑷姩璁句负褰撳墠鍊欓€夊浘銆傞粯璁ょ洰褰曚负锛?
```text
Assets/_Scripts/FrameWork/BuffSystem/AuthoringGraphs
```

鍒涘缓鍥句笉浼氬垱寤?`BuffConfigData`锛屼笉浼氱敓鎴?Effect `.cs`锛屼笉浼氫慨鏀?runtime銆乺egistry 鎴?whitelist銆?
鎺ㄨ崘鍒嗗伐锛?
```text
BuffCandidateGraph锛氱敤浜庡彲瑙嗗寲璁捐銆佸€欓€夊鏌ャ€侀闄╂彁绀恒€?Authoring Hub锛氱敤浜庢妸鍊欓€夊浘瀛楁瀵煎叆 Create Buff / Effect Template 琛ㄥ崟銆?Validator锛氱敤浜庢壂鎻忕湡瀹?BuffConfigData asset銆?```

璇︾粏娴佺▼瑙侊細

```text
Assets/_Scripts/FrameWork/BuffSystem/Docs/BuffSystem_xNodeAuthoringGraph.md
```

鍊欓€夊浘涓嶄細鑷姩鍒涘缓 BuffConfigData锛屼笉浼氳嚜鍔ㄧ敓鎴?Effect锛屼笉浼氳嚜鍔ㄦ敞鍐?Effect锛屼笉浼氳嚜鍔ㄥ姞鍏?whitelist锛屼篃涓嶄細璇佹槑 rollback-ready銆?
## Settings 璺緞璁剧疆

`Settings` 妯″紡褰撳墠鍙厤缃細

```text
鍥鹃粯璁ょ洰褰?Buff 閰嶇疆榛樿鐩綍
Effect 鑴氭湰榛樿鐩綍
ID Registry 璺緞
鑷姩鍒嗛厤 Buff / Effect ID
```

榛樿鍊硷細

```text
鍥鹃粯璁ょ洰褰?= Assets/_Scripts/FrameWork/BuffSystem/AuthoringGraphs
Buff 閰嶇疆榛樿鐩綍 = Assets/Resources/BuffSystem/Buff
Effect 鑴氭湰榛樿鐩綍 = Assets/_Scripts/FrameWork/BuffSystem/Effects/Generated
ID Registry 璺緞 = Assets/_Scripts/FrameWork/BuffSystem/AuthoringData/BuffSystemAuthoringIdRegistry.json
鑷姩鍒嗛厤 Buff / Effect ID = true
```

Settings 浣跨敤 `EditorPrefs` 淇濆瓨鏈満 Editor 宸ュ叿鍋忓ソ锛屼笉杩涘叆 runtime銆?
### ID Registry 鍙鏍￠獙

`Settings` 妯″紡涓彁渚涳細

```text
ID Registry 鍙鏍￠獙
鎵弿 ID 鍗犵敤
澶嶅埗鎵弿鎶ュ憡
```

璇ュ尯鍩熶細鍙鎵弿褰撳墠椤圭洰涓殑 Buff / Effect ID 鍗犵敤锛屽苟鏄剧ず锛?
```text
Registry 璺緞
Registry 鏄惁瀛樺湪
Registry 瑙ｆ瀽鐘舵€?鎺ㄨ崘涓嬩竴涓?Buff ConfigId
鎺ㄨ崘涓嬩竴涓?EffectId
Buff ID 鍗犵敤鏁伴噺
Effect ID 鍗犵敤鏁伴噺
Errors / Warnings / Infos
```

鎵弿鏉ユ簮鍖呮嫭锛?
```text
BuffConfigData 璧勬簮
Effect 鑴氭湰
BuffEffectRegistryBootstrap
宸叉湁 ID Registry JSON锛堝鏋滃瓨鍦級
```

### ID 鑷姩鍒嗛厤

```text
鑷姩鍒嗛厤 Buff / Effect ID
```

榛樿寮€鍚€傚紑鍚悗锛?
```text
Create Buff 浼氳嚜鍔ㄥ～鍏ヤ笅涓€涓湭鍗犵敤 Buff ConfigId銆?Effect Template 浼氳嚜鍔ㄥ～鍏ヤ笅涓€涓湭鍗犵敤 EffectId銆?浠?Graph 瀵煎叆鏃讹紝濡傛灉 Graph 涓殑 ID 缂哄け鎴栧啿绐侊紝Hub 浼氳嚜鍔ㄦ浛鎹负鍙敤 ID銆?鐢ㄦ埛浠嶅彲浠ユ墜鍔ㄤ慨鏀?ID锛屼絾淇敼鍚庡繀椤婚€氳繃鍞竴鎬ф牎楠屻€?```

ID 鏍￠獙浼氭鏌ワ細

```text
ID 蹇呴』澶т簬 0銆?ID 涓嶈兘涓庣幇鏈?BuffConfigData / Effect 鑴氭湰 / RegistryBootstrap / ID Registry 鍐茬獊銆?990000+ 灞炰簬 Debug / Smoke / Reserved 娈碉紝鏅€?Buff / Effect 鍒涘缓娴佺▼涓細浣滀负閿欒澶勭悊锛屼笉浣滀负鑷姩鍒嗛厤鐩爣銆?```

鐢ㄦ埛涓嶉渶瑕佹墜鍔ㄥ垱寤恒€侀噸寤烘垨棰勭暀 ID Registry銆侷D Registry JSON 浣滀负 Authoring Hub 鐨勫唴閮ㄩ粦绠辨満鍒朵繚鐣欙細鐪熷疄鍒涘缓 BuffConfigData 鑽夌鎴栫敓鎴?Effect `.cs` 鑽夌鎴愬姛鍚庯紝鐢卞伐鍏峰唴閮ㄨ嚜鍔ㄥ垱寤烘垨鏇存柊銆?
褰撳墠榛戠鏈哄埗鐨勮竟鐣岋細

```text
鑷姩鍒嗛厤涓嶄細鍒涘缓 BuffConfigData銆?鑷姩鍒嗛厤涓嶄細鐢熸垚 Effect .cs銆?鑷姩鍒嗛厤涓嶄細鑷姩娉ㄥ唽 Effect銆?鑷姩鍒嗛厤涓嶄細鍔犲叆 whitelist銆?鑷姩鍒嗛厤涓嶄細淇敼 runtime銆?鍒涘缓 / 鐢熸垚鎸夐挳浼氬厛鎵ц Preflight锛汦rror 浼氶樆姝㈠垱寤猴紝Warning / Fixup / Info 涓嶉渶瑕佺敤鎴蜂簩娆＄‘璁ゃ€?鍒涘缓 / 鐢熸垚鎴愬姛鍚庢墠浼氶粦绠卞啓鍏?ID Registry JSON銆?```

濡傛灉鐢ㄦ埛鎵嬪姩杈撳叆淇濈暀娈?ID锛?
```text
ConfigId 浣嶄簬 990000+ Debug / Smoke / Reserved 淇濈暀娈碉紝鏅€?Buff 涓嶈兘浣跨敤璇?ID銆傝鐐瑰嚮鈥滈噸鏂板垎閰?Buff ID鈥濄€?EffectId 浣嶄簬 990000+ Debug / Smoke / Reserved 淇濈暀娈碉紝鏅€?Effect 涓嶈兘浣跨敤璇?ID銆傝鐐瑰嚮鈥滈噸鏂板垎閰?Effect ID鈥濄€?```

Create Buff 椤甸潰涓殑 `EffectId = 0` 鍙〃绀?Effect 灏氭湭閰嶇疆锛屼細浣滀负 warning 鏄剧ず锛氬彲浠ュ垱寤洪厤缃崏绋匡紝浣嗚 Buff 鏆備笉鑳戒綔涓哄彲杩愯 production Buff銆傚畠涓嶄細璁?ConfigId 鍚堟硶鎬ф牎楠屽彉鎴愰敊璇€?
## 2. 鎺ㄨ崘鍒朵綔娴佺▼锛氫粠闆跺埗浣滀竴涓?Buff

### 閫夋嫨鏁板€肩紪杈戣繕鏄浘褰㈠寲缂栬緫

```text
绠€鍗?Buff锛氬彲浠ョ户缁娇鐢ㄦ暟鍊肩紪杈戞ā寮忎腑鐨?Validator / Create Buff / Effect Template銆?澶嶆潅 Buff锛氭帹鑽愬厛鍦ㄥ浘褰㈠寲缂栬緫妯″紡涓娇鐢?BuffRootNode + EffectNode 琛ㄨ揪 Buff 缁撴瀯銆佸涓?Effect 鍜岀敓鍛藉懆鏈熷叧绯汇€?```

鍥惧舰鍖栫紪杈戝綋鍓嶄粛鏄?authoring / review 杈撳叆锛屼笉鏄?production 閰嶇疆婧愩€俙EffectNode` 鐨?`OnApply / OnTick / OnRemove / OnRefresh / OnStackChanged` 鐢熷懡鍛ㄦ湡绔彛鍙湁鍦ㄧ敤鎴锋墽琛?`Effect Template` 鐨?`浠庡€欓€夊浘瀵煎叆 Effect 璋冪敤閾綻锛屾垨鍦ㄥ浘褰㈠寲缂栬緫妯″紡涓墽琛?`Graph Generate` 鐢熸垚涓?Effect 鑽夌骞堕€氳繃 Preflight 鍚庯紝鎵嶄細鍐欏叆 Effect `.cs` 鑽夌涓殑 action 瀛楁鍜?`Execute(in context)` 璋冪敤銆傚涓?`EffectNode` 绗竴鐗堝彧浼氶€夋嫨 `ExecutionOrder` 鏈€灏忕殑涓?Effect锛屼笉浼氳嚜鍔ㄥ悎鎴愪负 production Effect銆傜湡姝ｈ惤鍦颁粛闇€瑕佸垱寤?BuffConfigData 鑽夌銆佺敓鎴?Effect 鑽夌銆佷汉宸ュ疄鐜?Action 鍐呴儴鐜╂硶閫昏緫锛屽苟缁忚繃 Validator銆丷unner銆佸満鏅獙璇佸拰浜哄伐瀹℃壒銆?
`ScriptActionNode` 鍙互杩炴帴鍒?`EffectNode` 鐨勭敓鍛藉懆鏈熺鍙ｏ紝鐢ㄤ簬琛ㄨ揪鏌愪釜鐢熷懡鍛ㄦ湡瑕佹墽琛岀殑鑴氭湰鍔熻兘銆傛妸鑴氭湰鎷栧叆 `ActionScript` 鍚庯紝宸ュ叿浼氬仛 Editor-only 绫诲瀷璇嗗埆鍜岄闄╂彁绀猴紝骞跺湪 Hub 鍥惧舰鍖栨憳瑕佷腑鏄剧ず Action 鏁伴噺銆佹湁鏁?/ 鏃犳晥鏁伴噺鍜?warning 鎽樿銆?
`ScriptActionNode` 缁戝畾鐨勮剼鏈繀椤诲疄鐜?`IBuffGraphAction`锛屾墠鑳借瑙嗕负鏈夋晥 Action銆傛渶灏忚剼鏈舰鎬佸涓嬶細

```csharp
namespace BuffSystem
{
    public sealed class ExampleGraphAction : IBuffGraphAction
    {
        public void Execute(in BuffEffectContext context)
        {
            // TODO: 鍙啓 ECS 鐘舵€侊紱涓嶈渚濊禆 View 鎴?Unity 瀵硅薄銆?        }
    }
}
```

鎺ㄨ崘 Action 鑴氭湰鐩綍锛?
```text
Assets/_Scripts/FrameWork/BuffSystem/Actions
```

褰撳墠宸ュ叿涓嶄細鑷姩鍒涘缓璇ョ洰褰曘€傝矾寰勪笉鍖归厤鍙細浣滀负 warning銆?
Action 鑴氭湰搴旈伩鍏嶏細

```text
GameObject / MonoBehaviour / UnityEngine.Object
Transform / Camera / Input
Time.time / Time.deltaTime
UnityEngine.Random / 鏈敞鍏ョ瀛愮殑 System.Random
View 灞傚紩鐢?闇€瑕佸洖婊氬嵈涓嶅啓鍏?ECS Component 鐨勭鏈夌姸鎬?```

褰撳墠 `ScriptActionNode` 鍙敤浜庤璁°€佸鏌ワ紝浠ュ強 Effect Template 鎵嬪姩鐢熸垚鑽夌璋冪敤閾撅細

```text
宸插瓨鍦ㄦ渶灏?runtime-safe 鎺ュ彛 IBuffGraphAction銆?鍙湁鐢ㄦ埛鎵嬪姩鐐瑰嚮鈥滀粠鍊欓€夊浘瀵煎叆 Effect 璋冪敤閾锯€濆苟鐢熸垚妯℃澘鏃讹紝鎵嶄細鐢熸垚 Effect 鑽夌璋冪敤浠ｇ爜銆?涓嶄細娉ㄥ唽 Effect銆?涓嶄細淇敼 BuffEffectRegistryBootstrap銆?涓嶄細鍔犲叆 whitelist銆?涓嶄細璇佹槑 rollback-ready銆?OnStackChanged 绗竴鐗堜笉浼氭妸 delta 浼犵粰 Action銆?```

### Step 1锛氳鍒?Buff

鍒朵綔鍓嶅厛纭浠ヤ笅淇℃伅锛?
```text
ConfigId
Buff Name
BuffType
TriggerType
ParallelStorageMode
Unlimited
MaxStack
Duration
TickTime
EffectId
鏄惁闇€瑕?compressed storage
鏄惁鍙槸 Debug / Smoke
鏄惁闇€瑕?View 琛ㄧ幇
鏄惁渚濊禆 EventTrigger
鏄惁闇€瑕?rollback 鏀寔
```

娉ㄦ剰锛歚rollback-ready` 涓嶈兘鐢卞崟涓?Buff 鎴?Effect 鑷澹版槑銆傚畠渚濊禆澶栭儴 RollBackSystem銆乄orld snapshot / restore 璇箟銆丒ntity ID / Version 绋冲畾鎬э紝浠ュ強 BuffSystem restore hook 瀵规帴缁撴灉銆?
### Step 2锛氱敤 Effect Template 鐢熸垚 Effect 鑽夌

鍏ュ彛锛?
```text
Authoring Hub -> Effect Template
```

褰撳墠瀛楁锛?
```text
EffectId
Effect Class Name
Effect Display Name / Note
Target Folder
Namespace
Target File
Callback Selection
OnApply
OnTick
OnRemove
OnRefresh
OnStackChanged
```

褰撳墠榛樿鍊硷細

```text
EffectId = 0
Effect Class Name = NewBuffEffect
Target Folder = Assets/_Scripts/FrameWork/BuffSystem/Effects
Namespace = BuffSystem
Target File = Assets/_Scripts/FrameWork/BuffSystem/Effects/NewBuffEffect.cs
OnApply = true
OnTick = true
OnRemove = true
OnRefresh = false
OnStackChanged = false
```

褰撳墠鎸夐挳 / 鎿嶄綔椤癸細

```text
Validate
Generate Template
Copy Registry Snippet
Open Effect Folder
Clear
```

鎺ㄨ崘娴佺▼锛?
1. 杈撳叆 `EffectId`銆?2. 杈撳叆 `Effect Class Name`銆?3. 鍕鹃€夐渶瑕佺敓鎴愮殑 callbacks銆?4. 濡傛灉褰撳墠 Hub 椤堕儴宸茬粡閫夋嫨 `BuffCandidateGraph`锛屽彲浠ョ偣鍑?`浠庡€欓€夊浘瀵煎叆 Effect 瀛楁` 瀵煎叆涓?Effect 鐨?`EffectId / ClassName / Note`銆?5. 濡傞渶浠庡浘鐢熸垚璋冪敤閾撅紝鐐瑰嚮 `浠庡€欓€夊浘瀵煎叆 Effect 璋冪敤閾綻銆傚伐鍏蜂細璇诲彇涓?`EffectNode` 鐨勭敓鍛藉懆鏈熺鍙ｏ紝鏀堕泦鐩存帴杩炴帴鐨?`ScriptActionNode`锛屾寜 `ExecutionOrder` 鐢熸垚棰勮銆?6. 鐐瑰嚮 `Validate`銆?7. 纭 `EffectId` 鏈敞鍐岋紝涓斿彲浠ョ敓鎴愩€?8. 鐐瑰嚮 `Generate Template`銆傚伐鍏蜂細鑷姩鎵ц Effect Preflight锛涘鏋滃惎鐢ㄤ簡鍥捐皟鐢ㄩ摼锛岃繕浼氭墽琛?Graph Codegen Preflight銆侲rror 浼氶樆姝㈢敓鎴愶紝Warning / Fixup / Info 浼氭樉绀哄湪鎻愮ず鍖轰絾涓嶉渶瑕佷簩娆＄‘璁ゃ€?9. 鎵嬪姩瀹炵幇鎴栧鏌ョ敓鎴愮殑 Effect 閫昏緫銆?10. 浣跨敤 `Copy Registry Snippet` 澶嶅埗娉ㄥ唽浠ｇ爜銆?
杈圭晫锛?
```text
宸ュ叿涓嶄細鑷姩淇敼 BuffEffectRegistryBootstrap銆?宸ュ叿涓嶄細鑷姩娉ㄥ唽 Effect銆?宸ュ叿涓嶄細鑷姩鍔犲叆 whitelist銆?宸ュ叿涓嶄細鍒涘缓 BuffConfigData asset銆?鐢熸垚 Effect 妯℃澘涓嶄唬琛?production 鍙敤銆?浠庡€欓€夊浘瀵煎叆璋冪敤閾惧彧褰卞搷鐢熸垚鐨?Effect 鑽夌锛屼笉浼氫慨鏀瑰浘銆乺untime銆乺egistry 鎴?whitelist銆?OnStackChanged 绗竴鐗堢敓鎴?`Execute(in context)`锛屼笉浼氭妸 `delta` 浼犵粰 Action銆?```

`Copy Registry Snippet` 鍙鍒?`registry.Register(...)` 鐗囨鍒板壀璐存澘锛屼笉浼氫慨鏀逛换浣曚唬鐮併€俙Open Effect Folder` 鍙墦寮€鐩爣鐩綍锛屼笉浠ｈ〃鐢熸垚妯℃澘锛屼笉浼氭敞鍐?Effect锛屼篃涓嶄細淇敼 `BuffEffectRegistryBootstrap`銆?
`Generate Template` 鎴愬姛鐢熸垚 `.cs` 鑽夌鍚庯紝浼氬湪鍐呴儴鑷姩鍐欏叆 / 鏇存柊 ID Registry JSON锛岃褰?`effectId`銆丒ffect 鍚嶇О銆佺被鍚嶃€乬raphGuid銆乻criptPath 鍜?`Generated` 鐘舵€併€俁egistry 鍐欏叆澶辫触涓嶄細鍒犻櫎宸茬粡鐢熸垚鐨?`.cs`锛屽伐鍏蜂細鏄剧ず warning锛岀敤鎴烽渶瑕佹鏌?Registry 璺緞銆?
### Step 3锛氫汉宸ユ敞鍐?Effect

Effect 妯℃澘鐢熸垚骞跺疄鐜板悗锛岄渶瑕佷汉宸ュ皢 registry snippet 鍔犲叆锛?
```text
BuffEffectRegistryBootstrap.RegisterProductionEffects(...)
```

娉ㄥ唽鍚庨渶瑕侀噸鏂扮紪璇戯紝骞堕噸鏂拌繍琛?`Authoring Hub -> Validator` 妫€鏌?Effect 娉ㄥ唽鐘舵€併€?
### Step 4锛氱敤 Create Buff 鍒涘缓 BuffConfigData 鑽夌

鍏ュ彛锛?
```text
Authoring Hub -> Create Buff
```

褰撳墠瀛楁锛?
```text
ConfigId
Buff Name
Description
Save Path
Target Asset
BuffType
TriggerType
ParallelStorageMode
Unlimited
MaxStack
Duration
TickTime
StackUpPolicy
StackDownPolicy
EffectId
EffectRegistered
```

褰撳墠榛樿鍊硷細

```text
ConfigId = 100001
Buff Name = NewBuff
Save Path = Assets/Resources/BuffSystem/Buff
Target Asset = Assets/Resources/BuffSystem/Buff/100001_NewBuff.asset
BuffType = parallel
TriggerType = Tick
ParallelStorageMode = EntityPerStack
Unlimited = false
MaxStack = 1
Duration = 1
TickTime = 1
EffectId = 0
EffectRegistered = Unknown
```

褰撳墠鎸夐挳 / 鎿嶄綔椤癸細

```text
Validate
Create Draft Asset
Open Authoring Validator
Cancel / Close
```

褰撳墠宸ュ叿瀹為檯鎸夐挳鏄?`Cancel / Close`锛屾病鏈夐噸缃瓧娈电殑 `Clear` 鎸夐挳銆傝嫢鏈潵闇€瑕佺湡姝ｇ殑閲嶇疆鎸夐挳锛屽簲鍙﹀紑 UX 鏀硅繘闃舵銆?
鐐瑰嚮 `Create Draft Asset` 鏃讹紝宸ュ叿浼氳嚜鍔ㄦ墽琛?Buff Preflight銆侾reflight 浼氳ˉ榛樿鍊硷紝渚嬪绌?BuffName銆佺己澶?ID銆侀潪姝?MaxStack / Duration / TickTime锛涘鏋滃彂鐜?ConfigId 鍐茬獊銆佷繚鐣欐 ID 鎴栫洰鏍?asset 宸插瓨鍦紝浼氶樆姝㈠垱寤恒€侾reflight 閫氳繃鍚庣洿鎺ュ垱寤猴紝涓嶉渶瑕?warning 浜屾纭銆?
`Create Draft Asset` 鎴愬姛鍒涘缓 BuffConfigData 鑽夌鍚庯紝浼氬湪鍐呴儴鑷姩鍐欏叆 / 鏇存柊 ID Registry JSON锛岃褰?`configId`銆丅uff 鍚嶇О銆乬raphGuid銆乤ssetPath 鍜?`Generated` 鐘舵€併€俁egistry 鍐欏叆澶辫触涓嶄細鍥炴粴宸茬粡鍒涘缓鐨?BuffConfigData锛屽伐鍏蜂細鏄剧ず warning锛岀敤鎴烽渶瑕佹鏌?Registry 璺緞銆?
鎺ㄨ崘娴佺▼锛?
1. 杈撳叆 `ConfigId`銆?2. 杈撳叆 `Buff Name / Description`銆?3. 璁剧疆 Buff 琛屼负瀛楁銆?4. 濉啓 `EffectId`銆?5. 鐐瑰嚮 `Validate`銆?6. 纭娌℃湁 blocking error銆?7. 鍒涘缓 BuffConfigData asset銆?
杈圭晫锛?
```text
宸ュ叿涓嶄細鑷姩鍔犲叆 whitelist銆?宸ュ叿涓嶄細鑷姩鎶?Debug / Smoke Buff 鍙樻垚姝ｅ紡 Buff銆?宸ュ叿涓嶄細鑷姩娉ㄥ唽 Effect銆?宸ュ叿涓嶄細淇敼 runtime銆?宸ュ叿涓嶄細淇濆瓨 scene銆?```

### Step 5锛氱敤 Validator 妫€鏌?
鍏ュ彛锛?
```text
Authoring Hub -> Validator
```

妫€鏌ラ噸鐐癸細

```text
ConfigId 鏄惁閲嶅
Effect 鏄惁宸叉敞鍐?compressed eligibility 鏄惁婊¤冻
鏄惁 Smoke / Debug
鏄惁 Eligible Candidate
鏄惁 Invalid
```

Validator 鏄?authoring 杈呭姪宸ュ叿锛屼笉鏇夸唬 Runner銆佸満鏅獙璇佹垨璐熻矗浜哄鎵广€?
## 3. Compressed Parallel Buff 鍊欓€夋爣鍑?
褰撳墠 Editor 妫€鏌?compressed eligibility 鐨勫彛寰勪负锛?
```text
BuffType == parallel
ParallelStorageMode == CompressedExpiryFrameList
TriggerType == Tick
Unlimited == false
MaxStack <= CompressedParallelBuffLayerBuffer.Capacity
```

婊¤冻 eligibility 涓嶇瓑浜庤嚜鍔ㄨ繘鍏?production whitelist銆傝繘鍏?whitelist 鍓嶄粛闇€鍊欓€夊鏌ャ€丷unner銆佺湡瀹?View production path 鍦烘櫙楠岃瘉鍜屼汉宸ユ壒鍑嗐€?
浠ヤ笅绫诲瀷褰撳墠涓嶅簲杩涘叆 compressed whitelist锛?
```text
EventTrigger Buff
Unlimited Buff
MaxStack 瓒呰繃 compressed capacity 鐨?Buff
闈?Tick Buff
闈?parallel Buff
渚濊禆閫愬眰 runtime entity 鐨?Buff
渚濊禆 View 灞傜洿鎺ユ灇涓?runtime entity 鐨?Buff
```

## 4. Effect 缂栧啓绾︽潫

Effect 缂栧啓搴旈伒瀹堬細

```text
浣跨敤 Buff runtime / SimulationContext 鐨勫抚淇℃伅浣滀负閫昏緫鏃堕棿渚濇嵁
浼樺厛鍐?ECS 鐘舵€?涓嶈鐩存帴渚濊禆 View 琛ㄧ幇灞?涓嶈鐩存帴渚濊禆 Unity 瀵硅薄缁勪欢
涓嶈鍦?Effect 涓绉?rollback-ready
涓嶈鎶?Debug / Smoke Effect 褰撲綔姝ｅ紡鐜╂硶 Effect
```

Effect 杩涘叆 production 鍓嶏紝闇€瑕佸畬鎴愬疄鐜板鏌ャ€乺egistry 娉ㄥ唽瀹℃壒銆乂alidator 妫€鏌ュ拰鐩稿叧 Runner / 鍦烘櫙楠岃瘉銆?
## 5. ID 寤鸿

褰撳墠宸茬煡 ID锛?
```text
991001 褰撳墠鏄?production smoke pilot Buff
990101 褰撳墠鏄?DebugNoOpTickEffect
```

娉ㄦ剰锛?
```text
涓嶈澶嶇敤宸叉湁 ConfigId
涓嶈澶嶇敤宸叉敞鍐?EffectId
Debug / Smoke ID 涓嶅簲鐩存帴浣滀负姝ｅ紡 gameplay ID
```

姝ｅ紡 ID 鍒嗘瑙勮寖寰呴」鐩礋璐ｄ汉纭銆傚綋鍓嶆枃妗ｄ笉鎿呰嚜鍙戞槑 production ID 瑙勫垯銆?
## 6. 宸ュ叿涓嶄細鑷姩鍋氫粈涔?
Authoring 宸ュ叿涓嶄細鑷姩鎵ц浠ヤ笅鎿嶄綔锛?
```text
涓嶄細鑷姩娉ㄥ唽 Effect
涓嶄細鑷姩淇敼 BuffEffectRegistryBootstrap
涓嶄細鑷姩鍔犲叆 whitelist
涓嶄細鑷姩淇敼 runtime
涓嶄細鑷姩淇濆瓨 scene
涓嶄細鑷姩鍒涘缓姝ｅ紡鐜╂硶 Buff
涓嶄細璇佹槑 rollback-ready
涓嶄細鏇夸唬 Runner / 鍦烘櫙楠岃瘉
```

## 7. 甯歌閿欒涓庡鐞?
### ConfigId duplicate

璇存槑锛氱洰鏍?ConfigId 宸茶鐜版湁 BuffConfigData 浣跨敤銆?
澶勭悊锛氭洿鎹?ConfigId锛屾垨纭鏃?asset 鏄惁搴斿簾寮冦€備笉瑕佺洿鎺ヨ鐩栧凡鏈夌敓浜?ID銆?
### EffectId <= 0

璇存槑锛欵ffectId 鏈～鍐欐垨鏃犳晥銆?
澶勭悊锛氬～鍐欐湁鏁?EffectId锛屽苟纭璇?Effect 宸插疄鐜版垨鍑嗗鐢熸垚妯℃澘銆?
### EffectId 鏈敞鍐?
璇存槑锛欱uff 寮曠敤浜?EffectId锛屼絾 production registry 涓湭鍙戠幇娉ㄥ唽銆?
澶勭悊锛氬疄鐜?Effect 鍚庯紝浜哄伐灏?registry snippet 鍔犲叆 `BuffEffectRegistryBootstrap.RegisterProductionEffects(...)`锛岄噸鏂扮紪璇戝苟閲嶆柊杩愯 Validator銆?
### EffectId 宸叉敞鍐屼絾绫诲悕涓嶅悓

璇存槑锛欵ffectId 鍙兘宸茬粡缁戝畾鍒板叾浠?Effect 绫伙紝缁х画鐢熸垚鍚?ID 妯℃澘浼氶€犳垚璇箟鍐茬獊銆?
澶勭悊锛氫笉瑕佸鐢ㄨ EffectId銆傞渶瑕佽礋璐ｄ汉纭鏄惁鏀圭敤鏂?EffectId锛屾垨鏄惁澶嶇敤宸叉湁 Effect銆?
### Buff 琚瘑鍒负 Smoke / Debug

璇存槑锛欳onfigId 鎴栧悕绉版樉绀哄畠鏄皟璇?/ smoke 鐢ㄨ祫浜с€?
澶勭悊锛氫笉瑕佸皢璇?asset 褰撲綔姝ｅ紡 gameplay Buff銆傛寮?Buff 闇€瑕佺嫭绔嬪€欓€夊鏌ャ€?
### CompressedEligibility = false

璇存槑锛氬綋鍓嶅瓧娈电粍鍚堜笉婊¤冻 compressed storage 鏉′欢銆?
澶勭悊锛氭煡鐪?Validator 杈撳嚭鐨勪笉婊¤冻鍘熷洜銆備笉瑕佷负浜嗚繘鍏?whitelist 鐩茬洰淇敼鐜╂硶璇箟瀛楁銆?
### EventTrigger 鎯宠繘鍏?compressed whitelist

璇存槑锛欵ventTrigger 褰撳墠鎸夎璁?fallback EntityPerStack銆?
澶勭悊锛氫繚鎸?EntityPerStack锛屼笉杩涘叆 compressed whitelist銆?
### Unlimited Buff 鎯宠繘鍏?compressed whitelist

璇存槑锛歎nlimited 涓庡綋鍓?compressed eligibility 涓嶅吋瀹广€?
澶勭悊锛氫繚鎸?EntityPerStack锛屾垨鍙﹀紑璁捐闃舵璇勪及璇箟锛屼笉瑕佺洿鎺ュ姞鍏?whitelist銆?
### 鐢熸垚 Effect 妯℃澘鍚庡繕璁版敞鍐?
璇存槑锛欵ffect `.cs` 瀛樺湪涓嶄唬琛?production registry 宸叉敞鍐屻€?
澶勭悊锛氭墜鍔ㄦ敞鍐屽埌 `BuffEffectRegistryBootstrap.RegisterProductionEffects(...)`锛岄噸鏂扮紪璇戝苟杩愯 Validator銆?
### 娉ㄥ唽 Effect 鍚庡繕璁伴噸鏂拌繍琛?Validator

璇存槑锛欰uthoring 鐘舵€佸彲鑳戒粛鏄棫缁撴灉銆?
澶勭悊锛氶噸鏂版墦寮€鎴栧埛鏂?`Authoring Hub -> Validator`锛岀‘璁?`EffectRegistered=True`銆?
## 8. 褰撳墠宸茬煡杈圭晫

```text
褰撳墠 Resources Buff 鎵弿璺緞锛欰ssets/Resources/BuffSystem/Buff
褰撳墠 production smoke pilot锛?91001
褰撳墠 DebugNoOpTickEffectId锛?90101
EffectId const 闈欐€佹壂鎻忓彧鏄緟鍔╂鏌ワ紝涓嶈兘瑕嗙洊鎵€鏈夊姩鎬佹敞鍐屾潵婧?Validator 鏄?authoring 杈呭姪锛屼笉鏄?runtime 瀹夊叏璇佹槑
BuffSystem 浠嶄笉鑳藉绉?rollback-ready
```

褰撳墠鍞竴宸茬‘璁?View production smoke pilot 鏄?`991001 Debug_CompressedParallel_TickSmoke`銆傚畠涓嶆槸姝ｅ紡鐜╂硶 Buff锛屼篃涓嶄唬琛?production whitelist 鍙互鐩存帴鎵╁ぇ銆?
## 9. 鏈€灏忕ず渚嬫祦绋?
浠ヤ笅绀轰緥鍙鏄庢祦绋嬶紝涓嶄唬琛ㄥ綋鍓嶅凡缁忓垱寤哄搴?Buff 鎴?Effect銆?
鐩爣锛氬埗浣滀竴涓?`PoisonTickEffect` + `Poison Buff` 鑽夌銆?
1. 鍦?`Effect Template` 杈撳叆 `EffectId=100001`锛宍ClassName=PoisonTickEffect`銆?2. 鐐瑰嚮 `Validate`銆?3. 鐐瑰嚮 `Generate Template`銆?4. 鎵嬪姩瀹炵幇 `OnTick` 绛夐渶瑕佺殑 Effect 閫昏緫銆?5. 鐐瑰嚮 `Copy Registry Snippet`銆?6. 浜哄伐灏?snippet 鍔犲叆 `BuffEffectRegistryBootstrap.RegisterProductionEffects(...)`銆?7. 閲嶆柊缂栬瘧 Unity銆?8. 鍦?`Create Buff` 杈撳叆 `ConfigId=100001`锛宍EffectId=100001`銆?9. 鐐瑰嚮 `Validate`銆?10. 鐐瑰嚮 `Create Draft Asset`銆?11. 杩愯 `Validator Scan`銆?12. 濡傛灉甯屾湜杩涘叆 compressed whitelist锛屽彟寮€鍊欓€夊鏌ラ樁娈点€?
鍐嶆寮鸿皟锛氬垱寤鸿崏绋?asset銆佺敓鎴?Effect 妯℃澘銆佹弧瓒?eligibility锛岄兘涓嶇瓑浜庤繘鍏?production whitelist銆?
