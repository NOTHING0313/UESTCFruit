
# BuffSystem xNode 鍊欓€夊浘浣跨敤璇存槑

## Phase 3I-11V：CompositeEffect 图形化生成收口

CompositeEffect 图形化生成的完整说明已归档到：

```text
Assets/_Scripts/FrameWork/BuffSystem/Docs/BuffSystem_CompositeEffectAuthoring.md
```

推荐图结构：

```text
CandidateStart
 ├─ BuffShapeNode
 ├─ CompressedEligibilityNode
 ├─ RuntimeDependencyRiskNode
 ├─ CandidateDecisionNode
 └─ EffectCompositionRootNode
      ├─ EffectNode order=0
      │    ├─ OnApply -> ScriptActionNode order=0
      │    └─ OnTick  -> ScriptActionNode order=0
      ├─ EffectNode order=1
      │    └─ OnTick  -> ScriptActionNode order=0
      └─ EffectNode order=2
           └─ OnRemove -> ScriptActionNode order=0
```

节点职责：

```text
CandidateStart：候选 Buff 的图入口。
BuffShapeNode：BuffConfigData 行为字段来源。
CompressedEligibilityNode：压缩并行资格审查维度。
RuntimeDependencyRiskNode：runtime 风险审查维度。
CandidateDecisionNode：候选决策审查维度。
EffectCompositionRootNode：最终 CompositeEffectId / ClassName 来源。
EffectNode：生命周期分组。
ScriptActionNode：真正参与代码生成的功能节点。
```

顺序规则：

```text
EffectCompositionRootNode.Effects 并联只表示成员关系，不表示执行顺序。
EffectNode.Next 链优先表示跨 EffectNode 顺序；没有 Next 链时使用 EffectNode.ExecutionOrder。
EffectNode.Next 链与 ExecutionOrder 冲突时报错。
生命周期端口并联多个 ScriptActionNode 时，优先使用 ScriptActionNode.Next 链；没有 Next 链时使用 ScriptActionNode.ExecutionOrder。
ScriptActionNode.Next 链与 ExecutionOrder 冲突时报错。
```

legacy / deprecated 节点：

```text
BuffRootNode：legacy，旧图兼容，新图推荐 EffectCompositionRootNode。
EffectBindingNode：legacy，仅用于旧图或已有注册 Effect 引用，Composite 一键流程会忽略它。
Action Placeholder：deprecated，不生成代码，请使用 ScriptActionNode。
```

CompositeEffect 落地语义：

```text
多个 EffectNode 最终合成为一个 CompositeEffect。
BuffConfigData 仍然只有一个 EffectId。
BuffConfigData.EffectId 指向 CompositeEffectId。
只注册 CompositeEffect，不注册 child EffectNode。
```

## Phase 3I-11P：BuffCandidateGraph 图语义收口

候选图当前采用并行审查维度，而不是线性流程链。推荐结构为：

```text
CandidateStart
  -> BuffShape
  -> CompressedEligibility
  -> RuntimeDependencyRisk
  -> CandidateDecision
  -> EffectCompositionRoot
```

这些连接表达审查维度和结构关系，不代表 runtime Tick 顺序，也不代表 production whitelist 已通过。

Effect 组合语义：

```text
EffectCompositionRootNode 是新图推荐的 Effect 组合入口。
EffectCompositionRootNode.Effects 表示 EffectNode 成员关系，不表示执行顺序。
EffectNode.Next 如果形成完整链，则表示显式执行顺序。
未使用 EffectNode.Next 时，使用 EffectNode.ExecutionOrder 升序。
如果 Next 链和 ExecutionOrder 同时存在且顺序冲突，Evaluation / Graph Generate 应报 Error。
BuffRootNode 仅保留旧图兼容，新图不再推荐。
```

Action 节点语义：

```text
EmptyActionPlaceholderNode 已废弃，只保留旧图兼容；不会生成可运行调用。
ScriptActionNode.Next 可用于表达同一生命周期内 Action 链。
ScriptActionNode.Next 必须与 ScriptActionNode.ExecutionOrder 保持一致；冲突会阻止 Graph codegen。
```

边界保持不变：

```text
Graph 不是 production config source。
Graph 不自动创建 BuffConfigData。
Graph 不自动生成正式 gameplay Effect。
Graph 不自动加入 whitelist。
Graph 不证明 rollback-ready。
```

## Phase 3I-11U：一键创建 Buff + CompositeEffect 草稿

Authoring Hub 的 `图形化编辑 -> Graph Generate -> CompositeEffect 预览` 区域支持 `从图一键创建 Buff + CompositeEffect 草稿`。该操作用于多 EffectNode 候选图的最小落地：

```text
BuffCandidateGraph
-> CompositeEffect.cs
-> Effect ID Registry
-> BuffEffectRegistryBootstrap auto 区块（仅 Settings 开启自动注册）
-> BuffConfigData asset（仅自动注册成功后）
-> BuffConfigData.EffectId = CompositeEffectId
-> Buff ID Registry
```

`EffectCompositionRootNode.FinalEffectId` 是最终 CompositeEffectId 的优先来源；缺失时可按 Settings 自动分配正式段 EffectId。第一版不会把自动分配结果写回 Graph asset，仍保持 Graph -> Authoring Hub 单向生成。

`EffectBindingNode` 是 legacy 节点，Composite 一键流程会忽略它，不能用它决定最终 EffectId。自动注册关闭或失败时，一键流程会停止在 Buff 创建前，只保留 CompositeEffect `.cs` 与 Effect ID Registry，避免生成指向未注册 Effect 的 BuffConfigData。

该流程不会注册 child EffectNode，不会加入 whitelist，不会修改 runtime core，也不证明 rollback-ready。

## Phase 3I-11T：CompositeEffect Graph Generate 真实生成

Authoring Hub 的 `图形化编辑 -> Graph Generate -> CompositeEffect 预览` 区域现在支持 `从图创建 CompositeEffect 草稿`。该操作会把当前图中的多个 `EffectNode` / `ScriptActionNode` 合成为一个普通 `BuffEffectExecutorBase` 派生类 `.cs` 草稿。

真实生成会写入：

```text
CompositeEffect.cs
Effect ID Registry
BuffEffectRegistryBootstrap auto 区块（仅当 Settings 开启自动注册）
```

真实生成不会写入：

```text
BuffConfigData
Buff ID Registry
production whitelist
runtime core
child EffectNode 注册
Graph asset 自动回写
```

`CompositeEffectId` 优先来自 `EffectCompositionRootNode.FinalEffectId`；缺失时可按 Settings 自动分配正式段 EffectId。显式填写 990000+ Debug / Smoke / Reserved 保留段、已占用 ID、非法类名或已存在目标 `.cs` 都会阻止生成。

该能力仍保持 Graph -> Authoring Hub 单向生成：`BuffCandidateGraph` 是设计 / 审查输入，不是 production config source。生成后的 CompositeEffect 必须经过 Unity 编译、Validator、Runner、场景验证和人工审批，才能进入真实玩法流程。

## Phase 3I-11R：CompositeEffect Graph Generate 预览

多个 `EffectNode` 的 CompositeEffect 生成现在可以先通过 Authoring Hub 的 `图形化编辑 -> Graph Generate -> CompositeEffect 预览` 检查代码结构。预览会构建 `BuffGraphGeneratePlan` 与 `BuffGraphCompositeEffectPlan`，再调用 `BuffGraphCompositeEffectEmitter` 输出代码文本。

预览阶段只显示代码，不写 `.cs` 文件，不创建 `BuffConfigData`，不写 ID Registry，不自动注册 Effect，不修改 whitelist，也不修改 runtime。复制按钮只把完整预览代码放入剪贴板。

如果预览报顺序冲突，需要修正：

```text
EffectNode.Next / EffectNode.ExecutionOrder
ScriptActionNode.Next / ScriptActionNode.ExecutionOrder
```

如果预览报 Action 无效，需要先让对应脚本实现 `IBuffGraphAction`，并提供 public parameterless constructor。`EffectBindingNode` 仍是 legacy fallback；Composite 模式会忽略旧绑定节点。

## Phase 3I-11Q：CompositeEffect 生成计划

多个 `EffectNode` 的推荐落地方式是 CompositeEffect：Editor 工具先读取多个 `EffectNode`，再把它们合成为一个普通 `BuffEffectExecutorBase` 派生类的代码文本。`BuffConfigData` 最终仍然只绑定一个 `EffectId`，该 `EffectId` 指向 CompositeEffect。

执行顺序规则：

```text
EffectNode.Next 如果形成完整链，决定跨 EffectNode 的显式执行顺序。
未使用 EffectNode.Next 时，使用 EffectNode.ExecutionOrder 升序。
EffectNode.Next 与 ExecutionOrder 冲突、重复顺序、分叉或环会作为 Error。
ScriptActionNode.Next 如果形成完整链，决定同一个 EffectNode、同一个生命周期内 Action 的显式执行顺序。
未使用 ScriptActionNode.Next 时，使用 ScriptActionNode.ExecutionOrder 升序。
ScriptActionNode.Next 与 ExecutionOrder 冲突、重复顺序、分叉或环会作为 Error。
CompositeEffect 合并同一生命周期时，先按 Effect 顺序，再按 Action 顺序。
```

边界：

```text
CompositeEffect 只注册 CompositeEffectId，不注册子 EffectNode。
CompositeEffect 不让 BuffCandidateGraph 成为 production source。
CompositeEffect 不自动加入 whitelist。
CompositeEffect 不证明 rollback-ready。
Phase 3I-11Q 只生成 plan 和 code string，不写 .cs 文件，不接 Hub 按钮。
EffectBindingNode 仅保留旧图 fallback；存在 EffectCompositionRootNode / EffectNode 时，新结构优先，旧节点会被忽略。
EffectBindingNode 菜单位于 BuffSystem/Deprecated/Effect Binding。
```

## Phase 3I-11M锛氫粠鍥剧敓鎴?Buff / 涓?Effect 鑽夌

Authoring Hub 鐨?`鍥惧舰鍖栫紪杈慲 妯″紡鏂板 `鍥惧舰鍖栫敓鎴?/ Graph Generate` 鍖哄煙銆傞€変腑 `BuffCandidateGraph` 鍚庯紝鍙互鎵ц锛?
```text
浠庡浘鍒涘缓涓?Effect 鑽夌
浠庡浘鍒涘缓 Buff 鑽夌
浠庡浘涓€閿垱寤?Buff + 涓?Effect 鑽夌
```

鐢熸垚閾捐矾浼氬鐢ㄥ綋鍓?Authoring 宸ュ叿鐨勭粺涓€瑙勫垯锛?
```text
鑷姩鍒嗛厤缂哄け鎴栧啿绐佺殑 ConfigId / EffectId
鎵ц Graph codegen preflight
鎵ц Effect preflight
鎵ц Buff preflight
Error 闃绘鍐欏叆
鎴愬姛鍚庨粦绠辨洿鏂?ID Registry
```

涓?Effect 閫夋嫨浠嶇劧鍙鐞?`ExecutionOrder` 鏈€灏忕殑 `EffectNode`銆傚鏋滃浘涓湁澶氫釜 `EffectNode`锛屽伐鍏蜂細鏄剧ず warning锛涚涓€鐗堜笉浼氱敓鎴?`CompositeEffect`锛屼篃涓嶄細鎶婂涓?Effect 鑷姩鍚堟垚涓轰竴涓?production Effect銆?
涓€閿垱寤烘椂锛屽伐鍏蜂細鍏堝啓鍏ヤ富 Effect `.cs` 鑽夌锛屽啀鍒涘缓 `BuffConfigData` 鑽夌 asset銆傚鏋?Effect 鑽夌鐢熸垚鎴愬姛浣?Buff 鑽夌鍒涘缓澶辫触锛屽伐鍏蜂笉浼氳嚜鍔ㄥ垹闄ゅ凡缁忕敓鎴愮殑 `.cs` 鏂囦欢锛岀敤鎴烽渶瑕佹牴鎹敓鎴愮粨鏋滄墜鍔ㄦ鏌ユ垨娓呯悊銆?
杈圭晫淇濇寔涓嶅彉锛?
```text
Graph 涓嶆槸 production config source
鐢熸垚 Effect 鑽夌涓嶇瓑浜庡凡娉ㄥ唽 Effect
鍒涘缓 Buff 鑽夌涓嶇瓑浜庤繘鍏?whitelist
涓嶄細淇敼 BuffEffectRegistryBootstrap
涓嶄細淇敼 BuffSystem runtime
涓嶄細璇佹槑 rollback-ready
```

### Effect 璋冪敤閾惧畬鏁存€?
褰撲富 `EffectNode` 鐨勭敓鍛藉懆鏈熺鍙ｈ繛鎺ユ湁鏁?`ScriptActionNode` 鏃讹紝Graph 鐢熸垚鐨?Effect 鑽夌搴斿寘鍚細

```text
private readonly XxxAction _xxxAction = new XxxAction();
public override void OnTick(in BuffEffectContext context)
{
    _xxxAction.Execute(in context);
}
```

Authoring Hub 浼氭樉绀鸿皟鐢ㄩ摼棰勮锛屼緥濡傦細

```text
OnApply: <none>
OnTick: 0 ApplyDamageAction, 1 AddSlowAction
OnRemove: <none>
OnRefresh: <none>
OnStackChanged: <none>
```

濡傛灉鍥句腑瀛樺湪鏈夋晥 Action锛屼絾鐢熸垚缁撴灉娌℃湁浠讳綍 `Execute(in context)` 璋冪敤锛岃涓?Graph codegen 閿欒锛屽簲闃绘鐢熸垚鎴栨姤鍛婂け璐ャ€?
`EmptyActionPlaceholderNode` 鍙槸璁捐鍗犱綅锛屼笉浼氱敓鎴愬彲杩愯璋冪敤浠ｇ爜銆傞渶瑕佺敓鎴愯皟鐢ㄩ摼鏃讹紝璇锋浛鎹负瀹炵幇 `IBuffGraphAction` 鐨?`ScriptActionNode`銆?
## 1. 杩欎釜鍔熻兘瑙ｅ喅浠€涔堥棶棰?
`BuffCandidateGraph` 鐢ㄤ簬鎶婄湡瀹?gameplay Buff 鍊欓€夌殑璁捐銆侀闄╁拰鍑嗗叆鍒ゆ柇鐢绘垚鍙鍖栧鏌ュ浘銆傚畠瑙ｅ喅鐨勬槸鈥滃厛鎶婅璁¤娓呮锛屽啀钀藉湴涓?BuffConfigData / Effect 鑽夌鈥濈殑闂銆?
瀹冧笉鏄?production 閰嶇疆婧愶紝涔熶笉浼氳繘鍏?runtime 鍔犺浇娴佺▼銆?
## 2. 鍥惧舰鍖栫紪杈戜笌蹇嵎缂栬緫鐨勫垎宸?
```text
BuffCandidateGraph锛氳礋璐ｅ彲瑙嗗寲璁捐銆佸€欓€夊鏌ャ€侀闄╂彁绀恒€?Authoring Hub锛氳礋璐ｅ揩閫熷垱寤?BuffConfigData 鑽夌銆佺敓鎴?Effect 妯℃澘銆佹壂鎻忕湡瀹為厤缃€?Validator锛氬彧鎵弿鐪熷疄 BuffConfigData asset锛屽府鍔╃‘璁よ惤鍦伴厤缃姸鎬併€?```

Graph 鍜?Authoring Hub 褰撳墠鍙仛鍗曞悜鑱斿姩锛?
```text
BuffCandidateGraph -> Authoring Hub 琛ㄥ崟
```

Authoring Hub 涓嶄細鑷姩鎶婅〃鍗曟敼鍔ㄥ啓鍥?Graph锛岄伩鍏嶅嚭鐜板弻婧愭紓绉汇€?
## 3. 鎺ㄨ崘宸ヤ綔娴?
1. 鍏堢敤 `BuffCandidateGraph` 鐢诲嚭鍊欓€?Buff 璁捐鍜岄闄┿€?2. 鍦?`Tools / BuffSystem / Authoring Hub` 椤堕儴閫夋嫨鍊欓€夊浘銆?3. 鏌ョ湅鍊欓€夋憳瑕併€佹嫆缁濆師鍥犮€佽鍛婂拰涓嬩竴姝ュ缓璁€?4. 瀵瑰鏉?Buff锛屼娇鐢?`BuffRootNode + EffectNode` 琛ㄨ揪 Buff 涓庡涓?Effect 鐨勭粨鏋勩€?5. 灏嗗浘涓殑瀛楁瀵煎叆 `Create Buff` 琛ㄥ崟銆?6. 鍦?`Create Buff` 涓牎楠屽苟鐢变汉宸ョ偣鍑诲垱寤?BuffConfigData 鑽夌銆?7. 灏嗗浘涓殑 Effect 瀛楁瀵煎叆 `Effect Template` 琛ㄥ崟銆?8. 鍦?`Effect Template` 涓牎楠屽苟鐢变汉宸ョ偣鍑荤敓鎴?Effect 妯℃澘銆?9. 鍥炲埌 `Validator` 鎵弿鐪熷疄 BuffConfigData銆?10. 閫氳繃 Runner / Unity 鎵嬪姩楠岃瘉鍚庯紝鍐嶇敱璐熻矗浜哄喅瀹氭槸鍚︾敵璇疯繘鍏?whitelist銆?
## 4. 濡備綍鍒涘缓 BuffCandidateGraph

鍦?Project 绐楀彛涓娇鐢細

```text
Assets / Create / BuffSystem / Buff Candidate Graph
```

寤鸿涓嶈鎶婂€欓€夊浘鏀惧叆锛?
```text
Assets/Resources/BuffSystem/Buff
```

璇ョ洰褰曞彧鐢ㄤ簬鐪熷疄 `BuffConfigData` 璧勬簮鎵弿銆?
## 5. 濡備綍娣诲姞鑺傜偣骞惰繛鎺?
绗竴鐗堝缓璁寘鍚互涓嬭妭鐐癸細

```text
Candidate Start
Buff Shape
Effect Binding
Compressed Eligibility
Runtime Dependency Risk
Candidate Decision
```

鑺傜偣鍙互鍦?xNode 鍥句腑鍒涘缓銆佺紪杈戝拰杩炴帴銆傚綋鍓嶆渶灏?evaluation 鍙鏌ヨ妭鐐规暟閲忓畬鏁存€э紝灏氫笉璇佹槑杩炴帴璺緞瀹屾暣銆?
## 6. 濡備綍鍦?Authoring Hub 涓€夋嫨鍊欓€夊浘

鎵撳紑锛?
```text
Tools / BuffSystem / Authoring Hub
```

鍒囨崲鍒伴《閮ㄦā寮忥細

```text
鍥惧舰鍖栫紪杈?```

鍦ㄥ尯鍩燂細

```text
鍊欓€夊浘鑱斿姩 / Candidate Graph Link
```

閫夋嫨涓€涓?`BuffCandidateGraph`銆傝鍖哄煙浼氭樉绀猴細

```text
GraphVersion
ConfigId
BuffName
EffectId
鍥惧畬鏁?鍙彁浜ゅ鏌?鎷掔粷鍘熷洜
璀﹀憡
涓嬩竴姝?```

涔熷彲浠ヤ娇鐢?`鎵撳紑鍥綻銆乣Ping 鍥綻銆乣鍒锋柊鍊欓€夋憳瑕乣 杈呭姪鏌ョ湅銆?
濡傛灉杩樻病鏈夊€欓€夊浘锛屽彲浠ョ偣鍑伙細

```text
鍒涘缓鍥?```

鍒涘缓浣嶇疆鏉ヨ嚜 Settings 涓殑 `鍥鹃粯璁ょ洰褰昤銆傞粯璁ゅ€间负锛?
```text
Assets/_Scripts/FrameWork/BuffSystem/AuthoringGraphs
```

璇ョ洰褰曚笉鍦?Resources 涓嬨€傜偣鍑诲垱寤哄浘鍙垱寤?`BuffCandidateGraph`锛屼笉浼氬垱寤?`BuffConfigData`锛屼笉浼氱敓鎴?Effect `.cs`锛屼笉浼氫慨鏀?runtime銆乺egistry 鎴?whitelist銆?
## 6.1 Settings 璺緞璇存槑

Authoring Hub 鐨?`Settings` 妯″紡鐢ㄤ簬閰嶇疆宸ュ叿璺緞锛?
```text
鍥鹃粯璁ょ洰褰?Buff 閰嶇疆榛樿鐩綍
Effect 鑴氭湰榛樿鐩綍
ID Registry 璺緞
```

Settings 浣跨敤 EditorPrefs 淇濆瓨鏈満鍋忓ソ銆係ettings 涓嶆彁渚涙墜鍔ㄥ垱寤?/ 閲嶅缓 / 棰勭暀 ID Registry 鐨勫鏉傛搷浣滐紱ID Registry 鍙細鍦ㄧ敤鎴锋墜鍔ㄥ垱寤?BuffConfigData 鎴栫敓鎴?Effect 鑴氭湰鎴愬姛鍚庯紝鐢卞伐鍏峰唴閮ㄩ粦绠辩淮鎶ゃ€?
Authoring Hub 鐨?Settings 涓繕鎻愪緵 `ID Registry 鍙鏍￠獙`銆傚畠浼氭壂鎻忓綋鍓嶉」鐩腑鐨?BuffConfigData銆丒ffect 鑴氭湰銆乣BuffEffectRegistryBootstrap` 鍜屽凡鏈?Registry JSON锛屽苟鏄剧ず鎺ㄨ崘鐨勪笅涓€涓?Buff ConfigId / EffectId銆?
Settings 涓殑 `鑷姩鍒嗛厤 Buff / Effect ID` 榛樿寮€鍚€傚浘褰㈠寲璺緞涓庢暟鍊艰矾寰勫叡鐢ㄥ悓涓€濂?ID 绯荤粺锛?
```text
Graph 鐨?ConfigId / EffectId 缂哄け鏃讹紝瀵煎叆琛ㄥ崟鍚庝細鑷姩鍒嗛厤鍙敤 ID銆?Graph 鐨?ConfigId / EffectId 鍐茬獊鏃讹紝鑷姩鍒嗛厤寮€鍚細灏濊瘯鏇挎崲涓哄彲鐢?ID銆?Graph 鐨?ConfigId / EffectId 浣嶄簬 990000+ 淇濈暀娈垫椂锛屾櫘閫氬垱寤烘祦绋嬩細瑙嗕负涓嶅彲鎺ュ彈锛涜嚜鍔ㄥ垎閰嶅紑鍚細灏濊瘯鏇挎崲涓烘寮忔鍙敤 ID銆?鑷姩鍒嗛厤鍏抽棴鏃讹紝浼氫繚鐣?Graph 鍊煎苟鍦ㄨ〃鍗曟牎楠屼腑鏄剧ず閿欒銆?```

ID Registry JSON 浣滀负鍐呴儴榛戠鏈哄埗淇濈暀锛岀敤鎴蜂笉闇€瑕佹墜鍔ㄥ垱寤恒€侀噸寤烘垨棰勭暀銆傝嚜鍔ㄥ垎閰?ID 涓嶄唬琛?BuffConfigData 宸插垱寤猴紝涓嶄唬琛?Effect `.cs` 宸茬敓鎴愶紝涓嶄唬琛?Effect 宸叉敞鍐岋紝涔熶笉浠ｈ〃鍙互杩涘叆 whitelist銆傜敤鎴锋渶缁堢偣鍑?`Create Draft Asset` 鎴?`Generate Template` 鏃讹紝Graph 瀵煎叆瀛楁浠嶄細缁忚繃鍚屼竴濂?Preflight锛涘垱寤?/ 鐢熸垚鎴愬姛鍚庢墠浼氱敱宸ュ叿鍐呴儴鍐欏叆 Registry JSON銆?
## 6.2 BuffRootNode 涓?EffectNode

澶嶆潅 Buff 鎺ㄨ崘浣跨敤鏂拌妭鐐硅〃杈剧粨鏋勶細

```text
BuffRootNode锛氳〃绀轰竴涓?Buff 鍥剧殑鏍癸紝鍖呭惈 ConfigId銆丅uffName銆丏escription銆丱wner銆丯otes锛屽苟閫氳繃 Effects 绔彛杩炴帴 EffectNode銆?EffectNode锛氳〃绀轰竴涓?Effect 璁捐鍗曞厓锛屽寘鍚?EffectId銆丒ffectName銆丒ffectClassName銆丒xecutionOrder銆丏escription銆丯otes銆?EmptyActionPlaceholderNode锛氱敓鍛藉懆鏈熺鍙ｇ殑鍗犱綅鍔熻兘鑺傜偣锛屽悗缁彲鏇挎崲涓?ScriptActionNode銆?```

`EffectNode` 褰撳墠鏀寔鐢熷懡鍛ㄦ湡杈撳嚭绔彛锛?
```text
OnApply
OnTick
OnRemove
OnRefresh
OnStackChanged
```

澶氫釜 `EffectNode` 浣跨敤 `ExecutionOrder` 琛ㄨ揪鎵ц椤哄簭銆侫uthoring Hub 鍥惧舰鍖栨憳瑕佷細鏄剧ず EffectNode 鏁伴噺銆佹墽琛岄『搴忓拰鐢熷懡鍛ㄦ湡杩炴帴鎽樿銆?
褰撳墠闄愬埗锛?
```text
鐢熷懡鍛ㄦ湡绔彛鍙槸鍥剧粨鏋勮〃杈撅紝涓嶄細鍦ㄥ浘涓嚜鍔ㄧ敓鎴?production 浠ｇ爜锛涢渶瑕佸湪 Authoring Hub 鐨?Effect Template 涓墜鍔ㄧ偣鍑烩€滀粠鍊欓€夊浘瀵煎叆 Effect 璋冪敤閾锯€濆苟閫氳繃 Preflight 鍚庯紝鎵嶄細鐢熸垚 Effect 鑽夌璋冪敤浠ｇ爜銆?澶氫釜 EffectNode 绗竴鐗堝彧浼氶€夋嫨 ExecutionOrder 鏈€灏忕殑涓?Effect 鐢熸垚鑽夌锛涘悗缁渶瑕?CompositeEffect 鐢熸垚闃舵鎵嶈兘鐪熸钀藉湴涓哄崟涓?BuffConfigData EffectId銆?Graph 涓嶆槸 production Buff锛屼笉鑳界洿鎺ヨ繘鍏?runtime 鎴?whitelist銆?```

## 6.3 ScriptActionNode

`ScriptActionNode` 鐢ㄤ簬鍦ㄥ浘涓〃杈锯€滄煇涓敓鍛藉懆鏈熶細鎵ц鏌愪釜鑴氭湰鍔熻兘鈥濄€傚畠鏄?Editor-only 鑺傜偣锛屽彧鏈嶅姟浜庤璁°€佸鏌ュ拰鎽樿鏄剧ず銆?
娣诲姞鏂瑰紡锛?
```text
鍙抽敭鍥剧┖鐧藉 / BuffSystem / Script Action
```

甯哥敤杩炴帴鏂瑰紡锛?
```text
EffectNode.OnApply -> ScriptActionNode.Previous
EffectNode.OnTick -> ScriptActionNode.Previous
EffectNode.OnRemove -> ScriptActionNode.Previous
EffectNode.OnRefresh -> ScriptActionNode.Previous
EffectNode.OnStackChanged -> ScriptActionNode.Previous
```

鑺傜偣瀛楁锛?
```text
ActionName
ActionScript
ActionTypeName
ActionDisplayName
IsValidAction
ValidationMessage
Description
ExecutionOrder
```

鎶?C# 鑴氭湰鎷栧叆 `ActionScript` 鍚庯紝鑺傜偣浼氬湪 Editor 涓皾璇曡鍙?`MonoScript.GetClass()`锛屽苟鑷姩濉厖绫诲瀷鍚嶅拰鏄剧ず鍚嶃€傝剼鏈繀椤诲疄鐜?`BuffSystem.IBuffGraphAction`锛屽惁鍒欎細琚涓烘棤鏁?Action銆傚綋鍓嶆牎楠屽彧鍋?Editor-only 鎻愮ず鍜岄樆鏂垎绫伙紝鍖呮嫭锛?
```text
鑴氭湰鏄惁涓虹┖
GetClass() 鏄惁涓虹┖
绫诲瀷鏄惁 abstract
绫诲瀷鏄惁 generic type definition
鏄惁缁ф壙 MonoBehaviour / UnityEngine.Object
鏄惁瀹炵幇 IBuffGraphAction
绫诲悕 / namespace 鏄惁鍚堟硶
鏄惁鏈?public parameterless constructor
婧愮爜鏄惁鍖呭惈 Time.time / Time.deltaTime / GameObject / Transform 绛夐珮椋庨櫓瀛楃涓?```

杩欎簺妫€鏌ヤ笉鏄畬鏁?C# 璇箟鍒嗘瀽锛屽彧鐢ㄤ簬鎻愬墠鍙戠幇 authoring 椋庨櫓銆傚嚭鐜?warning 鏃堕渶瑕佷汉宸ョ‘璁?deterministic / rollback / View 渚濊禆杈圭晫銆?
鎺ㄨ崘灏嗗彲澶嶇敤鐨?Buff Graph Action 鑴氭湰鏀惧湪锛?
```text
Assets/_Scripts/FrameWork/BuffSystem/Actions
```

鏈樁娈典笉浼氳嚜鍔ㄥ垱寤鸿鐩綍銆傝矾寰勪笉鍖归厤鍙綔涓?warning锛屼笉浼氶樆姝㈠浘褰㈠寲璁捐銆?
褰撳墠闄愬埗锛?
```text
ScriptActionNode 缁戝畾鑴氭湰蹇呴』瀹炵幇 IBuffGraphAction 鎵嶈兘瑙嗕负鏈夋晥 Action銆?ScriptActionNode 鍙湁鍦?Effect Template 鎵嬪姩瀵煎叆璋冪敤閾撅紝鎴?Graph Generate 鐢熸垚涓?Effect 鑽夌鏃讹紝鎵嶄細鍙備笌 Effect .cs 鑽夌鐢熸垚銆?ScriptActionNode 涓嶄細鑷姩娉ㄥ唽 Effect銆?OnStackChanged 绔彛杩炴帴 Action 鏃讹紝绗竴鐗堜笉浼氭妸 delta 浼犵粰 Action銆?ScriptActionNode 涓嶄細淇敼 runtime core / registry / whitelist銆?```

## 6.4 浠庣敓鍛藉懆鏈熺鍙ｇ敓鎴?Effect 鑽夌璋冪敤閾?
绗竴鐗堢敓鎴愯鍒欙細

```text
Effect Template 鍙鍙栦富 EffectNode銆?涓?EffectNode = ExecutionOrder 鏈€灏忕殑 EffectNode銆?澶氫釜 EffectNode 浼氭樉绀?warning锛屼笉浼氱敓鎴?CompositeEffect銆?姣忎釜鐢熷懡鍛ㄦ湡绔彛鍙敹闆嗙洿鎺ヨ繛鎺ョ殑 ScriptActionNode銆?EmptyActionPlaceholderNode 浼氳蹇界暐骞剁敓鎴?warning / TODO銆?ScriptActionNode 鎸?ExecutionOrder 鍗囧簭鐢熸垚璋冪敤銆?鍚屼竴鐢熷懡鍛ㄦ湡鍐?ExecutionOrder 閲嶅浼氶樆姝㈢敓鎴愩€?```

鐢熸垚鍚庣殑鑽夌褰㈡€侊細

```csharp
private readonly SomeAction _someAction = new SomeAction();

public override void OnTick(in BuffEffectContext context)
{
    _someAction.Execute(in context);
}
```

`ScriptActionNode` 鍙備笌鐢熸垚鏃跺繀椤绘弧瓒筹細

```text
ActionScript 涓嶄负绌恒€?MonoScript.GetClass() 鍙鍙栥€?绫诲瀷瀹炵幇 IBuffGraphAction銆?绫诲瀷涓嶆槸 MonoBehaviour / UnityEngine.Object銆?绫诲瀷涓嶆槸 abstract / 娉涘瀷绫诲瀷瀹氫箟銆?绫诲悕鍜?namespace 鍚堟硶銆?瀛樺湪 public parameterless constructor銆?```

杈圭晫锛?
```text
鐢熸垚鐨勬槸 Effect 鑽夌銆?涓嶄細鑷姩娉ㄥ唽 Effect銆?涓嶄細淇敼 BuffEffectRegistryBootstrap銆?涓嶄細鍔犲叆 whitelist銆?涓嶄細淇敼 runtime core銆?OnStackChanged 绗竴鐗堜笉浼氭妸 delta 浼犵粰 Action銆?```

## 7. 濡備綍鎶婂€欓€夊浘瀵煎叆 Create Buff 琛ㄥ崟

鍦?Authoring Hub 椤堕儴閫夋嫨鍊欓€夊浘鍚庯紝杩涘叆锛?
```text
鍒涘缓 Buff
```

鐐瑰嚮锛?
```text
浠庡€欓€夊浘瀵煎叆鍩虹瀛楁
```

浼氬鍏ワ細

```text
ConfigId
BuffName
Description / DesignPurpose
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
```

瀵煎叆鍚庝細瑙﹀彂鐜版湁鏍￠獙棰勮銆?
璇ユ寜閽笉浼氬垱寤?`BuffConfigData` asset锛屼笉浼氬姞鍏?whitelist锛屼篃涓嶄細淇敼 runtime銆?
## 8. 濡備綍鎶婂€欓€夊浘瀵煎叆 Effect Template 琛ㄥ崟

鍦?Authoring Hub 椤堕儴閫夋嫨鍊欓€夊浘鍚庯紝杩涘叆锛?
```text
Effect 妯℃澘
```

鐐瑰嚮锛?
```text
浠庡€欓€夊浘瀵煎叆 Effect 瀛楁
```

浼氬鍏ワ細

```text
EffectId
EffectClassName
Effect Note / EffectRiskNotes
```

瀵煎叆鍚庝細瑙﹀彂鐜版湁鏍￠獙棰勮銆?
璇ユ寜閽笉浼氱敓鎴?`.cs` 鏂囦欢锛屼笉浼氫慨鏀?`BuffEffectRegistryBootstrap`锛屼篃涓嶄細娉ㄥ唽 Effect銆?
## 9. 濡備綍鐢?Validator 妫€鏌ョ湡瀹?BuffConfigData

杩涘叆锛?
```text
閰嶇疆妫€鏌ュ櫒
```

Validator 浠嶇劧鍙壂鎻忕湡瀹?`BuffConfigData` 璧勬簮銆傚€欓€夊浘鍙彁渚涘鐓ф彁绀猴細

```text
褰撳墠鍊欓€夊浘 ConfigId
鐪熷疄 BuffConfigData 鏄惁瀛樺湪
```

濡傛灉鍚?ConfigId 宸插瓨鍦ㄧ湡瀹為厤缃紝鍙互鐢?Validator 瀵圭収妫€鏌ャ€傝嫢涓嶅瓨鍦紝鍒欒〃绀哄€欓€夊浘灏氭湭钀藉湴涓?BuffConfigData銆?
## 10. 鍝簺浜嬫儏涓嶄細鑷姩鍙戠敓

```text
涓嶄細鑷姩鍒涘缓 production Buff銆?涓嶄細鑷姩鍒涘缓 BuffConfigData锛岄櫎闈炵敤鎴峰湪 Create Buff 涓墜鍔ㄧ偣鍑诲垱寤恒€?涓嶄細鑷姩鐢熸垚 Effect 浠ｇ爜锛岄櫎闈炵敤鎴峰湪 Effect Template 涓墜鍔ㄧ偣鍑荤敓鎴愩€?涓嶄細鑷姩娉ㄥ唽 Effect銆?涓嶄細鑷姩鍔犲叆 whitelist銆?涓嶄細鑷姩淇敼 runtime銆?涓嶄細鑷姩淇濆瓨 scene銆?涓嶄細璇佹槑 rollback-ready銆?涓嶄細鏇夸唬 Runner銆?涓嶄細鏇夸唬 Unity 鎵嬪姩楠岃瘉銆?```

Graph 涓嶆槸 production Buff锛孏raph 瀛楁瀵煎叆涔熶笉鏄渶缁堜骇鐗┿€傚彧鏈?Preflight 閫氳繃鍚庣敱 Authoring Hub 鍒涘缓鍑虹殑 BuffConfigData / Effect `.cs` 鑽夌锛屾墠鏄悗缁?Validator銆佷汉宸ユ敞鍐屽拰鍊欓€夊鏌ヨ妫€鏌ョ殑鐪熷疄鏂囦欢銆?
## 11. 甯歌闂

### 鍊欓€夊浘瀹屾暣鏄惁绛変簬鍙互杩?whitelist

涓嶆槸銆傚€欓€夊浘瀹屾暣鍙鏄庡浘涓殑蹇呰鑺傜偣瀛樺湪銆傝繘鍏?whitelist 浠嶉渶鐪熷疄 BuffConfigData銆丒ffect 娉ㄥ唽銆佽涓轰竴鑷存€ч獙璇併€佸満鏅獙璇併€佹€ц兘瑙傚療鍜岃礋璐ｄ汉瀹℃壒銆?
### 瀵煎叆瀛楁鏄惁浼氬垱寤鸿祫婧?
涓嶄細銆傚鍏ュ彧濉厖 Authoring Hub 琛ㄥ崟銆傜湡姝ｅ垱寤?BuffConfigData 鎴?Effect `.cs` 蹇呴』鐢辩敤鎴锋墜鍔ㄧ偣鍑诲搴旀寜閽€?
### 鍊欓€夊浘鑳藉惁鏇夸唬 Validator

涓嶈兘銆傚€欓€夊浘鏄璁″拰瀹℃煡鍏ュ彛锛孷alidator 鎵弿鐨勭湡瀹?BuffConfigData 鎵嶄唬琛ㄨ惤鍦伴厤缃姸鎬併€?
### 鍊欓€夊浘鑳藉惁鏇夸唬 Runner

涓嶈兘銆俁unner 鍜?Unity 鍦烘櫙楠岃瘉浠嶆槸琛屼负姝ｇ‘鎬у拰闆嗘垚鐘舵€佺殑楠岃瘉鍏ュ彛銆?
## 12. 褰撳墠闄愬埗

```text
褰撳墠鍙敮鎸?Graph -> Authoring Hub 鍗曞悜瀵煎叆銆?褰撳墠涓嶆敮鎸?Authoring Hub 琛ㄥ崟鑷姩鍐欏洖 Graph銆?褰撳墠 evaluation 鍙鏌ヨ妭鐐规暟閲忓畬鏁存€э紝涓嶆牎楠屽畬鏁磋繛鎺ヨ矾寰勩€?褰撳墠涓嶈嚜鍔ㄧ敓鎴愬€欓€夊鏌ユ姤鍛娿€?褰撳墠涓嶅０鏄?rollback-ready銆?褰撳墠涓嶆墿澶?production whitelist銆?```

## Phase 3I-11O Graph Generate 与 Effect 自动注册

Graph Generate 生成主 Effect 草稿后，会在 ID Registry 写入成功且 Settings 开启 `自动注册 Effect 到 Bootstrap` 时，尝试维护 `BuffEffectRegistryBootstrap.cs` 的 auto 区块。一键创建 Buff + 主 Effect 时，只有 Effect 草稿、BuffConfigData 草稿和 ID Registry 写入均成功后才会尝试自动注册。

单独从 Graph 创建 Buff 草稿不会触发 Effect 自动注册。auto 注册不改变 Graph 作为候选设计图的定位：Graph 仍不是 production config source，auto 注册不等于进入 whitelist，不代表 runtime 验证通过，也不证明 rollback-ready。
## Phase 3I-11O-UXCleanup Graph Generate 入口收口

Graph Generate 是候选图生成 Buff / Effect 草稿的唯一推荐入口。旧的数值编辑页导入按钮已移除，避免与图形化生成流程形成重复入口。
