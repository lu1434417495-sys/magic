# Claude Review: Chain Contingency V1 Implementation Plan

Reviewer: `claude-opus-4-8`  
Run date: `2026-06-23`  
Reviewed document: `docs/discussions/chain_contingency_implementation_prd.md`  
Reference inputs:

- `docs/discussions/chain_contingency_data_structure.md`
- `docs/discussions/chain_contingency_prd.json`
- `docs/design/project_context_units.md`

## 总体结论

- 是否可执行：**基本可执行但需修订**。骨架（schema、MP 封存、事务、hook、回滚）切分扎实，依赖顺序合理；但存在 3 个会让实现者卡住或返工的硬缺口。
- 最大风险一句话：**目标解析（target resolver）这条主链：枚举定义与持久 schema 自相矛盾，且没有任何任务实现它和写它的测试，会直接导致战斗内自动施法无法选目标，V1 的核心玩法跑不起来。**

## P0 阻断问题

### P0-1 目标解析器枚举与持久 schema 互相矛盾

- 位置：`Core Interfaces / Closed Domains` 的 `ContingencyTargetResolverKind`（5 项）vs 源设计第 19 节 `ContingencyTargetResolverState`（9 项）vs 第 8 节（8 项）。
- 问题：执行文档把 resolver 收敛成 `Self / TriggerSource / NearestEnemy / OwnerCenteredArea / SafeCell` 5 个，并改了名（`SafeCell` / `OwnerCenteredArea`）；但源设计第 19 节的 strict schema 是 `self / trigger_source / trigger_target / nearest_enemy_to_owner / nearest_enemy_to_trigger_cell / owner_centered_area / attacker_cell / empty_cell_near_owner`，其中 `empty_cell_near_owner` 还带 `preference`、`max_distance` 两个字段。两个完整示例（反近身、致死逃生）用的都是 `empty_cell_near_owner` 而不是 `safe_cell`。
- 为什么严重：Task 1 要写 strict exact、未知字段 / 未知 type 拒绝的 `ContingencyTargetResolverState.FromDictionary`，但 resolver 的 type 集合与字段集合两份材料给的不一样。实现者无法确定该认 5 个还是 9 个、`SafeCell` 是否要带 `preference/max_distance`。schema 回归会因为基线不确定而无法落地。
- 建议修正：在执行文档里钉死一份 resolver 权威清单（type + 每 type 精确字段），并显式声明它覆盖 / 取代源设计第 8 / 19 节中的差异；`empty_cell_near_owner` 的 `preference/max_distance` 必须进枚举模型还是合并进 `SafeCell` 要写清。

### P0-2 没有任何可被预存的真实法术内容

- 位置：`File Map / Create` 只建 `data/configs/skills/mage_chain_contingency.tres`；`Modify` 里 `SkillDef.cs` / `CombatSkillDef.cs` 只写 add or surface automation profile。
- 问题：全文没有任何任务为 `mage_mirror_image`、`mage_stoneskin`、`mage_thunderwave`、`mage_blink_step`、`priest_cure_wounds` 等可储存法术补 `automation` profile（`can_be_stored_in_contingency=true` 等）。但 Task 2 content validator 用例、Task 5 charge 事务、Task 9 auto-cast、Task 11 damage hook 的端到端都需要至少一个带 automation profile 的真实可储存技能。
- 为什么严重：`mage_chain_contingency` 是矩阵本体技能，不是被储存的法术。没有任何真实 storable spell，Task 5 的 `SaveSetup` / `ChargeSetup` 无法用真实内容跑通，全局约束又禁止 fixture-only 内容作为正式实现，Task 9 release 无技能可放。
- 建议修正：把“为 N 个现有 combat skill 资源补 `ContingencyAutomationDef`（含 `allowed_target_resolvers`、`min_contingency_skill_level`、`tags`）”作为 Task 2 的显式 Step，并在 File Map 列出要改的 `.tres`；明确 V1 首发的可储存法术白名单。

### P0-3 目标解析的执行与测试完全没有归属

- 位置：Task 9 `Interfaces: Consumes ... target resolution`、`AutoCastRequest.Target = ContingencyTargetResolutionResult`；Task 10 / Task 11 也假设 resolver 已产出目标。
- 问题：`ContingencyTargetResolutionResult` 被消费，但没有任何任务步骤实现“按 resolver kind 解析出 unit/coord”，尤其是 `empty_cell_near_owner` 的安全格评分。源设计第 8.4 节要求产出最优合法候选、无完美格仍选最高分、无合法格才部分失败。Task 9 的 autocast 测试只测 origin / 成本绕过，trigger 测试只测匹配，没有一条测 resolver 解析正确性或安全格评分。
- 为什么严重：auto-cast 拿不到目标就无法结算；safe-cell 评分是本特性最复杂、最容易出 bug 的算法之一，却既无实现步骤也无验收。实现者会在 Task 9 才发现要从零造一个评分器，且没有回归保护。
- 建议修正：新增一个独立任务，建议插在 Task 9 前或并入 Task 9，File Map 加 `ContingencyTargetResolverService` 和 `run_contingency_target_resolver_regression.cs`，把第 8 节的硬合法条件、评分项、`away_from_trigger_source`、`max_distance`、致死逃生取消伤害判定逐条写成用例。

## P1 重要问题

### P1-1 一批 request/result DTO 被引用却未定义

- 位置：`PartyContingencySetupService` 接口用到 `ContingencySetupSaveRequest`、`ContingencySetupClearChargeRequest`、`ContingencySetupStatusRequest`、`ContingencySetupStatusResult`，只有 `ContingencySetupChargeRequest` 给了字段。
- 问题：`SaveSetup` 要写整套 setup（trigger / resolver / stored_spells / release_mode），其 request 字段是整个特性的输入契约，却没定义。
- 为什么严重：Task 5 / Task 6 实现者要自己猜 Save / Edit 的输入结构，UI 与 headless 极易各写一套、对不齐。
- 建议修正：补全这 4 个 DTO 字段，明确 `SaveSetup` 是否承载完整 setup payload、`ContingencySetupStatusResult` 暴露哪些稳定字段（要和 Task 6 断言字段一致）。

### P1-2 headless `<setup-payload-name>` 的来源机制未定义

- 位置：Task 6 命令 `party contingency save/edit <member> <setup-payload-name>`。
- 问题：没有说明 payload name 如何映射成一个完整 `ContingencySetupSaveRequest`（trigger、stored spells、resolver 从哪来）。是内置 fixture 表还是文件没有定义。
- 为什么严重：headless 是 V1 发布门槛之一，且测试不解析中文只断言稳定字段；但构造输入的机制缺失，命令无法实现。
- 建议修正：定义 headless setup payload 的来源（建议一张命名 typed fixture 表，仅限测试 / 命令边界），并说明它与 UI 走同一 `ContingencySetupSaveRequest`。

### P1-3 `allowed_parameter_bindings` 字段缺失，但验证用例依赖它

- 位置：Task 2 Step 2 automation 字段列表无 `allowed_parameter_bindings`；但 Task 2 Step 1 case 7 是 unsupported parameter binding key is rejected。
- 问题：源设计第 14 节要求每个技能声明允许的 binding key / 类型 / 枚举；执行文档的 automation 字段表漏了它，验证器没有 allowlist 可查。
- 为什么严重：该测试无法按设计实现，只能退化成拒绝一切 key，与 `energy_resistance` 之类需要 `element` 的设计冲突。
- 建议修正：把 `allowed_parameter_bindings`（key -> 类型 / 枚举）加进 `ContingencyAutomationDef` 字段表，并在 validator 步骤点名引用。

### P1-4 EndBattle / CommitBattleResources 的可失败签名与网关接口未点名

- 位置：Task 7 Modify 列出 `BattleRuntimeModule.cs`、`CharacterBattleWritebackService.cs`，但未列 `IBattleRuntimeCharacterGateway` 文件；源设计要求 `EndBattle()` 返回可失败结果，并通过 `IBattleRuntimeCharacterGateway` 暴露 `CommitContingencyConsumedSetups`。
- 问题：执行文档没说要改 `EndBattle()` / `CommitBattleResources()` 的返回类型，也没把网关接口文件纳入 Modify。
- 为什么严重：Task 7 写回失败 / flush 失败恢复内存依赖这条可失败链；若 `EndBattle` 仍是 void，半提交防线无法表达，测试无法通过。
- 建议修正：在 Task 7 显式加“修改 `IBattleRuntimeCharacterGateway` 增加 `CommitContingencyConsumedSetups`”“把 `EndBattle()` 改为返回可失败结果，写回失败时中止后续 `CommitBattleResources()`”。

### P1-5 任务顺序：Task 7 的 consumed 写回测试早于产出 consumed 的 sidecar

- 位置：Task 7（settlement rollback）在 Task 8（sidecar / consumed overlay）之前。
- 问题：Task 7 用例需要已 consumed 的 setup，而产出 consumed setup ids 的 `BattleContingencySystem` 在 Task 8 才存在。执行文档没说 Task 7 测试如何在无 sidecar 时注入 consumed ids。
- 为什么严重：实现者按顺序做到 Task 7 会发现测试无数据来源，要么被迫提前实现 sidecar，要么改测试，造成返工。
- 建议修正：在 Task 7 明确一个 test-only 注入缝，例如 gateway / 服务接受直接传入 consumed setup ids，或网关层 stub，并说明它与 Task 8 的真实来源如何对接。

### P1-6 `mp_max` 语义全局改写的 caller 覆盖是开放式

- 位置：Task 3 Step 4 “Update current MP clamps in charge, clear, rest/recovery, progression refresh, equipment refresh, battle unit generation, battle refresh, and battle writeback”。
- 问题：这是把现有 `snapshot mp_max` 的含义从 raw 改成 effective，影响面大；“all callers”是开放枚举，源设计第 18 节还点了 `PracticeGrowthService.ApplyDailyGrowthToMember()`、item / settlement 恢复等具体入口，执行文档没逐一钉死。
- 为什么严重：漏一个恢复 / clamp 入口就会出现换装 / 休息把已释放的封存又封回去，或超出 effective 上限回蓝，且不一定有测试覆盖。
- 建议修正：把需要改的 clamp / 恢复入口做成一份显式 checklist（对齐源设计第 18 节列表），每个入口配一条断言或在 Task 3 测试里点名。

### P1-7 `DamageApplicationInput` 依赖的字段未声明

- 位置：Task 11 / 源设计 J2 伪代码用到 `input.LiveCommit`、`input.MinHpAfterDamage`，但 `Core Interfaces` 的 `DamageApplicationInput` 只给了 `SuppressDamageApplicationHook`、`WithResolvedDamage`。
- 问题：`LiveCommit`、`MinHpAfterDamage`、`ShieldAbsorptionPercent` 等是既有字段还是新增没说清。
- 为什么严重：投影 helper 与 hook 触发判定都读这些字段，缺定义会导致接口对不齐、preview / AI 抑制判定走偏。
- 建议修正：在接口块补 `DamageApplicationInput` 与投影相关的全部输入字段，并标注哪些是既有、哪些新增。

## P2 次要问题

- 命名不一致：写回结果在 Task 7 Step 2 叫 `ContingencyConsumedCommitResult`，源设计 G3 叫 `ContingencyCommitResult`；charge 结果在接口块叫 `ContingencySetupMutationResult`，源设计 J1 叫 `ContingencyChargeResult`。需统一，否则跨任务引用混乱。
- 回归套件注册：Task 12 Step 4 直接 `python tests/run_regression_suite.py` 期望包含全部新 runner，但没有任何步骤说明新 runner 是否需要在套件里登记。若非自动发现，会静默漏跑。建议加一步登记新 runner。
- `BeforeDamageResolvedContext.SourceEventFacts` 用 `Godot.Collections.Dictionary`，与全文 / context map 反复强调的 runtime owner 不持有字典业务态基调略有张力；建议注明这是 battle-local frozen facts 的边界例外，避免审查期被当成腐化。
- `SkillCatalogTyped` 作为参数类型名出现在 `ContingencyContentValidator`，但未说明它就是 `GameContentCatalog.GetSkillCatalogTyped()` 的返回类型；建议点名以免实现者另造一个类型。
- Save 版本硬编码 `10/6` 与“实现前版本再次变化则以当前 +1 为准”的实际风险并存。建议把 Task 1 测试里的 `6` / `9` / `10` 字面量改为“实现时当前版本及 +1”的表述，或加一条“实现前先核对当前 `SaveVersion` / `PartyState.version`”的前置检查步骤。

## 覆盖性检查

1. **root save 10 / PartyState 6**：覆盖（Task 1 Step 3 / 4，测试 case 8）。隐患：版本号硬编码、并行迁移可能抢号。
2. **full V1 一起做，不做非伤害限定版**：覆盖且强约束（Global Constraints + Release Matrix + Implementation Notes 明确 first runnable loop 不等于可发布）。
3. **UI 和 headless 都要做**：覆盖（Task 6 双测）。但二者共用的 `ContingencySetupSaveRequest` / payload 来源未定义，存在两边各写一套的风险。
4. **quantity-aware warehouse API**：覆盖得最好（Task 4 含 preview / commit / capture / restore + 原子性 / 数量不足用例）。
5. **real `mage_chain_contingency` resource**：矩阵本体覆盖（Task 2），但被储存法术内容缺失，所以真实可玩层面没闭合。
6. **charge 和 battle finalization 失败回滚**：覆盖（Task 5 事务回滚 + Task 7 finalization 快照恢复），但 Task 7 的可失败链签名与 consumed 来源未钉死。
7. **illegal setup content 在读档失败**：覆盖（Task 2 Step 4 + case 8，state parser 只校验结构、catalog 后由 validator 决定读档成败）。设计边界清晰。

## 执行者视角缺失项

- resolver 到底是 5 个还是 9 个，`empty_cell_near_owner` 字段去哪了。
- 哪些现有技能在 V1 可被储存、它们的 automation profile 怎么写、改哪些 `.tres`。
- 谁实现 target resolver 解析与安全格评分、测试在哪。
- `ContingencySetupSaveRequest` / `ClearChargeRequest` / `StatusRequest` / `StatusResult` 的字段。
- headless `<setup-payload-name>` 怎样变成一个完整 request。
- `ContingencyAutomationDef` 的 `allowed_parameter_bindings` 形态。
- `IBattleRuntimeCharacterGateway` 文件位置、`EndBattle()` / `CommitBattleResources()` 的新签名。
- Task 7 测试在 sidecar 未就绪时如何拿到 consumed ids。
- `mp_max` 改写需要触及的完整 clamp / 恢复入口清单。
- `DamageApplicationInput` 的 `LiveCommit` / `MinHpAfterDamage` / `ShieldAbsorptionPercent` 是否新增。
- `SkillCatalogTyped` 的具体类型、新 runner 是否需登记进 `run_regression_suite.py`。

## 最小修订建议

1. 在 `Core Interfaces` 增加一节 Target Resolvers (authoritative)：列出 V1 全部 resolver kind + 每 kind 精确字段，并声明它取代源设计第 8 / 19 节的差异。
2. Task 2 增加 Step：为 V1 可储存法术补 `ContingencyAutomationDef`，File Map 列出具体 `.tres`（mirror image / stoneskin / thunderwave / blink step / cure wounds 等），定义首发白名单。
3. 新增任务 Target Resolution：`ContingencyTargetResolverService` + `run_contingency_target_resolver_regression.cs`，覆盖第 8 节安全格评分、`max_distance`、致死取消伤害。排在 Task 9 之前。
4. 补全 `ContingencySetupSaveRequest` / `ClearChargeRequest` / `StatusRequest` / `StatusResult` 字段；说明 `SaveSetup` 承载完整 setup，`StatusResult` 字段与 Task 6 断言字段一致。
5. Task 6 增加 headless setup payload 来源定义（命名 typed fixture，仅限边界），并声明 UI / headless 共用同一 request。
6. `ContingencyAutomationDef` 字段表补 `allowed_parameter_bindings`，validator 步骤点名引用。
7. Task 7 显式补：改 `IBattleRuntimeCharacterGateway`（加方法）、`EndBattle()` 返回可失败结果、写回失败中止资源提交；并定义 sidecar 未就绪时 consumed ids 的 test-only 注入缝。
8. Task 3 把需改的 clamp / 恢复入口改成显式 checklist，逐项配断言。
9. 统一结果 / 请求类型命名（consumed commit、charge result、mutation result 各一处），并在接口块补 `DamageApplicationInput` 全字段、点名 `SkillCatalogTyped` 类型。
10. Task 1 把 `9/10/5/6` 字面量改为“以实现时当前版本及 +1 为准”，并加一步开工前核对当前 `SaveVersion` / `PartyState.version`（防并行迁移抢号）。
