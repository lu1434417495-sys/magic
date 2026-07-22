# Equipment Ability 检视剩余问题

原始检视日期：`2026-05-11`  
当前代码复核：`2026-07-20`

本文只保留对当前 C# authoring → definition → projection → preview / AI / execution 链重新核验后仍存在的缺口和显式延期边界。已落地的 ScopedAutoCast、攻击防御组件修正、world day/month/persistent counter、equipment skill entry identity、authoring ABI 导出字段以及 context-unit 读集已从检视意见中移除。

当前 runtime 契约见 [`../design/battle/equipment_ability_runtime.md`](../design/battle/equipment_ability_runtime.md)；仍未落地的扩展边界见 [`../proposals/battle/equipment_ability/system_expansion.md`](../proposals/battle/equipment_ability/system_expansion.md)。

## 当前实现缺口

### P1：binding 在 validation 成功前进入 definition projection

- `EquipmentAbilityContentRegistry.cs:101-109` 调用 `ValidateBinding(...)` 后立即 `ProjectBinding(...)`，之后才检查 `errors.Count`。
- 对只应记录 validation error 的非法嵌套 condition/payload，projection 仍可能先解引用无效字段或抛异常，使单个坏 `.tres` 中断整个 registry rebuild。
- 应以单个 binding 的 error checkpoint 为边界：只有该 binding 校验通过才允许投影和注册；坏 binding 必须产生稳定 diagnostic，不能阻止其它 pack 的错误收集。

### P0：敌方 attack equipment 没有稳定实例态 source

已落地的部分：

- `EncounterRosterBuilder` 会为敌人投影 attack equipment 的装备能力来源。
- `BattleEquipmentAbilityProjectionService` 可把这些来源投影到战斗单位。

仍未完成的部分：

- 敌方 attack equipment 没有 materialize 为 battle-only `EquipmentState` / `EquipmentInstanceState`。
- `BattleEquipmentAbilityProjectionService` 当前仍把 `SourceEquipmentInstanceId` 投影为空值。
- 因此需要实例 owner 的 persistent counter、耐久和其它实例态 action 无法按设计工作，同模板多敌人也没有稳定区分的 source identity。

要求：使用包含 `unit_id` 和 `item_id` 的 battle-local 稳定 instance id；不写回 enemy template / world / party / warehouse，也不自动进入 loot。

### P0 / 范围契约：`on_battle_end` mutating action 尚未有 staged commit

- 当前 `FinalizeBattleResolution(...)` 仍先把 battle-local view 写回 party，然后才执行 `EndBattle(...)`。
- 当前 validator 会拒绝 mutating `on_battle_end`，这避免了静默丢失写回，但没有实现 proposal 要求的 `PrePartyWriteback` / `PreLootCommit` / `PreProgressionCommit` staged commit。
- 在 staged commit、rollback owner 和对应回归落地前，不得让 validator 放行这类 action。

这是已承诺范围的实现缺口，不是当前已放行内容的运行时 bug。

### P1：环境事实只落地了全局 tag / night

已落地的部分：

- battle state 已有 `BattleEnvironmentSnapshot`。
- 装备能力 condition 可消费全局 environment tags 和 night 事实。

仍缺失：

- coord-local environment tag。
- path environment tag。
- storm / water / forest 等局部地形与路径事实的统一 provider。
- preview、AI 和 execution 共用同一坐标/路径环境上下文的回归。

在这些 owner 落地前，需要局部环境的装备内容必须保持 validator-rejected 或 deferred，不能退化为全局 tag。

### P1：creature type taxonomy 缺生产 owner

- `BattleUnitState.creature_type_tags` 及敌人投影已存在。
- `KnownCreatureTypeTags` 仍主要是 validation context 的 DTO 字段；未找到生产 taxonomy registry / `CreatureTypeTagContentRules` 或对该集合的完整生产注入。
- 结果是运行时可以携带 tag，但 MOD/content validation 尚无稳定真相源判定哪些 creature tag 合法。

## 明确延期，不是当前 bug

### 目标策略系统

强制目标、随机敌我、禁止普通攻击目标和 AI 接管普通行动尚未被定义为 UI / AI / execution 共用的 targeting policy。装备授予技能或 cast variant 继续走现有技能目标链；只有普通行动被改写时才需新 owner。

### 复杂行动经济

装填、射后移动、同动作攻击+施法、多阶段计划、连续射击疲劳、死亡前免费攻击、stealth/reveal、逃跑/投降等仍属 deferred / content-cut / owner-missing，不应为了放行内容而塞进 availability gate。

### By-family per-bullet ledger

`source_traces` 已保留来源 metadata，但当前不做所有自然语言 bullet 的强制全量 ledger。这是已确认的框架期范围裁剪，不是 runtime defect。

### MOD-ready typed diagnostics

Basic V1 只要求 registry build fail-fast，invalid pack / binding 不进入索引和 projection。完整 `equipment_ability` typed diagnostic public schema、headless/text validation 输出属于 MOD-ready 延期项。

## 验收原则

- 任何新 ability 都必须证明 authoring、definition projection、preview、AI、execution 和 commit/writeback 使用同一 typed 契约。
- owner 缺失的 payload 必须由 validator blocking，不得变成 trace-only / no-op 或静默 fallback。
- 涉及敌方实例、battle-end commit 或持久计数时，必须补原子 rollback 和实例隔离回归。

## Project Context Units Impact

本次只清理过时检视结论，没有改变 runtime owner 或推荐读集，不需要修改 `docs/design/project_context_units.md`。
