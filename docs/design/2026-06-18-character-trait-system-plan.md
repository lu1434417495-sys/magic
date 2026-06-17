# 通用人物/装备特性系统设计计划

更新日期：`2026-06-18`

## 状态

- 当前状态：`Proposed Design Plan`
- 范围：`TraitDef` / `TraitInstanceState` / 人物永久特性 / 装备随机特性 / 战斗 effective trait 投影。
- 兼容策略：不兼容旧存档；不添加 legacy alias、fallback migration 或旧 payload/schema 支持。
- 验证策略：以 focused regression 为主；battle simulation 只用于后续数值或 AI 行为分析，不用于验证结构正确性。

## 背景与目标

当前项目已有一条身份特性链：

- `RaceTraitDef`
- `RaceTraitEffectKind`
- `TraitTriggerContentRules`
- `TraitTriggerHooks`
- `BattleUnitState.race_trait_ids/subrace_trait_ids/bloodline_trait_ids/ascension_trait_ids`

这条链能承载种族、血脉、升华等身份特性，但不适合直接支持两类新需求：

- 人物可以通过奖励、剧情、成就、修炼等方式永久获得特性。
- 装备实例可以携带随机 roll 出来的特性，并在装备后把这些特性绑定到装备者身上。

目标是新增一个通用特性系统，把身份特性、人物永久特性、装备固定特性和装备随机特性纳入同一套定义、实例、聚合与战斗投影模型。

## 核心原则

- `TraitDef` 是内容定义真相源。
- `TraitInstanceState` 是实例事实源。
- `EffectiveTraitSet` / `EffectiveTraitInstance` 是运行时聚合结果。
- `effective_trait_ids` 只是派生查询投影，不能作为事实源。
- 人物永久特性和装备实例特性必须分开存储。
- 装备特性只绑定当前装备者，不写回人物永久特性。
- 属性型特性进入属性快照；触发型特性进入战斗触发器。
- `CharacterTraitService` 只负责聚合和解析，不负责随机 roll、不负责触发器执行、不直接修改 battle state。

## 内容定义

新增 `TraitDef` 作为通用特性定义资源。建议字段：

- `trait_id`
- `display_name`
- `description`
- `categories`
- `effect_type`
- `trigger_type`
- `stack_policy`
- `params`
- `attribute_modifiers`
- `roll_value_schema`

`trigger_type`、`effect_type`、`stack_policy` 在 Godot resource 边界保持 `StringName` 字段，但正式逻辑必须通过 typed enum/rules 进入：

- `TraitEffectKind`
- `TraitTriggerKind`
- `TraitStackPolicyKind`

不要把正式分发退回松散字符串比较。触发器分发应保持 typed `TraitEffectKind -> handler` 映射。

### RaceTraitDef 迁移策略

`RaceTraitDef` 不应被当成简单可改名类型。现有实现绑定：

- `RaceTraitEffectKind`
- `RaceTraitContentRegistry`
- `race_trait_defs` content bucket
- progression phase2 trait reference validation
- 既有战斗 trigger regression

迁移应分阶段完成：

1. 新增 `TraitDef` / `TraitContentRegistry` / `trait_defs` bucket。
2. 让 race/subrace/bloodline/ascension 等身份内容的 `trait_ids` 引用 `TraitDef`。
3. 保持旧 battle schema 暂时可测，先验证通用 trait 内容加载与引用。
4. 战斗投影稳定后，再删除旧 `RaceTraitDef` / `race_trait_defs` 正式入口。

## 实例状态

新增 `TraitInstanceState`，用于保存人物永久特性和装备随机特性的实例信息。建议字段：

- `trait_instance_id`
- `trait_id`
- `source_type`
- `source_id`
- `rank`
- `stacks`
- `roll_values`

`roll_values` 可以在 save/resource 边界投影为 dictionary，但正式读取应通过 typed helper，例如：

- `GetIntRoll(StringName key, int fallback = 0)`
- `GetStringNameRoll(StringName key, StringName fallback = default)`
- `GetBoolRoll(StringName key, bool fallback = false)`

不要让业务逻辑直接回读 Godot dictionary。

### 人物特性

`PartyMemberState.trait_instances` 只保存人物永久获得的特性，例如奖励、剧情、成就、修炼结果。

身份来源的 trait 不写入 `PartyMemberState.trait_instances`，而是继续从 race/subrace/bloodline/ascension/stage 内容定义派生。

### 装备特性

`EquipmentInstanceState.trait_instances` 只保存装备实例随机 roll 出来的特性。

`ItemDef.trait_ids` 表示固定装备特性，由定义派生，不复制进每个 `EquipmentInstanceState`，避免内容更新后实例 stale。

`ItemDef.trait_roll_groups` 使用 typed 子资源，例如 `TraitRollGroupDef`。它应由 item/content validator 校验：

- trait 引用存在。
- 权重合法。
- roll 数量合法。
- 互斥组合法。
- roll value schema 与 `TraitDef.roll_value_schema` 匹配。

装备实例创建必须统一通过 `EquipmentTraitRollService`。所有创建入口都要接入同一服务，包括掉落、仓库直接创建、战斗 loot、脚本/测试创建 fixture。

## 有效特性聚合

新增 `CharacterTraitService`，放在 progression/character management owner 附近，由 `CharacterManagementModule` setup 和调用。

聚合顺序固定为：

```text
identity -> character -> equipment
```

来源含义：

- `identity`：race/subrace/bloodline/ascension 等内容定义派生特性。
- `character`：`PartyMemberState.trait_instances` 中的人物永久特性。
- `equipment`：当前装备 view 中 `ItemDef.trait_ids` 固定特性和 `EquipmentInstanceState.trait_instances` 随机特性。

输出 DTO：

- `EffectiveTraitSet`
- `EffectiveTraitInstance`

`EffectiveTraitInstance` 至少应包含：

- `trait_id`
- `trait_def`
- `trait_instance`
- `source_kind`
- `source_id`
- `effective_instance_key`
- `stack_policy`
- `roll_values`

`effective_trait_ids` 从 `EffectiveTraitSet` 派生，只用于 UI、查询和 trace，不参与正式叠加或触发判定。

### 叠加策略

必须先定义 stack policy，再实现聚合。默认策略建议：

- `unique_by_trait`：同一 `trait_id` 只保留一个有效实例。
- `highest_roll`：保留数值最高的实例。
- `additive`：允许多实例叠加。
- `stack_by_instance`：每个实例独立生效。

触发次数不能默认继续用 `trait_id` 作为 charge key。只有 `unique_by_trait` 可用 trait id；允许多实例触发的 trait 必须使用 stable `effective_instance_key`。

## 属性系统接入

`AttributeService` 不直接查询 trait catalog、item def、equipment instance 或 character state。

`CharacterTraitService` 先把有效特性解析为属性修正，然后通过 `AttributeSourceContext` 传入：

- `trait_attribute_modifiers`

`AttributeService` 在现有 modifier pipeline 中追加 trait modifier entries。需要明确避免双算：

- 身份内容已有 `attribute_modifiers` 的路径不应和 trait attribute modifiers 重复表达同一效果。
- 装备 `ItemDef.attribute_modifiers` 和装备 trait attribute modifiers 都可以生效，但来源要可区分。

建议 source type：

- `trait_identity`
- `trait_character`
- `trait_equipment_fixed`
- `trait_equipment_roll`

## 战斗系统接入

### BattleUnitState

最终目标是用统一字段替换旧分组 trait 投影：

- `effective_trait_instances`
- `effective_trait_ids`

旧字段：

- `race_trait_ids`
- `subrace_trait_ids`
- `bloodline_trait_ids`
- `ascension_trait_ids`

替换会影响 strict schema、clone、`ToDictionary()`、`FromDictionary()`、AI mutation guard、battle save/load 和现有 tests，因此应放在后期分阶段执行。

`effective_trait_instances` 是 canonical battle payload；`effective_trait_ids` 是派生投影。不要让这两个字段长期各自维护，避免不一致。

### PassiveStatusOrchestrator

现有 identity projection 不只是 trait id，还包括：

- vision tags
- proficiency tags
- save advantage tags
- damage resistances
- racial skill charges

迁移 trait schema 时不能误删这些静态战斗投影。需要保持一个清晰边界：

- 身份静态战斗状态投影继续归 passive/status projection 链。
- trait 实例聚合归 `CharacterTraitService`。

如果后续决定由 `CharacterTraitService` 同时负责全部身份战斗投影，需要单独确认并扩大测试范围。

### TraitTriggerHooks

`TraitTriggerHooks` 改为按 `EffectiveTraitInstance` 分发，而不是只按 trait id。

分发规则：

- `TraitDef.trigger_type` 决定触发时机。
- `TraitDef.effect_type` 经 typed `TraitEffectKind` 映射到 handler。
- handler 可读取 `TraitInstanceState.roll_values` 和 source metadata。
- charge key 根据 stack policy 解析。

第一版需要保持现有三个核心触发行为空间不退化：

- `halfling_luck`：自然 1 重掷。
- `savage_attacks`：暴击额外武器骰。
- `relentless_endurance`：致死伤害保 1 HP。

## Age Stage Trait Policy

当前 age stage trait projection 在内容校验中仍被显式禁止。

默认策略：第一版继续禁止 age stage trait runtime projection。

如需把 age stage 纳入统一 trait 聚合，需要单独扩展：

- age content validation
- `CharacterTraitService` identity source 收集
- effective age source metadata
- battle projection regression

## 存档与兼容策略

本计划不兼容旧存档。

实施时必须：

- bump save/party schema version。
- 更新 `PartyMemberState` strict field list。
- 更新 `EquipmentInstanceState` strict payload fields。
- 更新 `PartyState` round-trip。
- 更新 save payload tests。
- 明确旧 payload 缺少 trait 字段时失败，而不是静默恢复。

不要添加：

- legacy aliases
- old field fallback
- empty-list migration
- old `race_trait_defs` runtime fallback

如果后续需要兼容旧档，必须另开设计并说明具体 breakage 与 migration policy。

## 实施分期

### Phase 1：内容层

- 新增 `TraitDef`、typed enum/rules、`TraitContentRegistry`。
- 接入 `GameContentCatalog` / progression content validation。
- 新增 `trait_defs` bucket。
- 迁移现有 race trait 内容到通用 trait 资源。
- 先不替换 `BattleUnitState` schema。

### Phase 2：状态与装备层

- 新增 `TraitInstanceState` strict schema。
- 给 `PartyMemberState` 增加人物永久 `trait_instances`。
- 给 `EquipmentInstanceState` 增加随机 `trait_instances`。
- 给 `ItemDef` 增加 fixed `trait_ids` 与 `trait_roll_groups`。
- 接入 `ItemContentRegistry.MergeWithTemplate` 和 item validator。
- 新增 `EquipmentTraitRollService`。
- 统一所有装备实例创建路径。

### Phase 3：聚合与属性层

- 新增 `CharacterTraitService`。
- 新增 `EffectiveTraitSet` / `EffectiveTraitInstance`。
- 接入 `CharacterManagementModule.build_attribute_source_context()`。
- 将 trait attribute modifiers 作为明确来源传给 `AttributeSourceContext`。
- 覆盖 identity、人物永久、装备固定、装备随机 trait 的 source metadata 与 stack policy。

### Phase 4：战斗层

- `BattleUnitFactory` 开战投影 effective traits。
- `RefreshBattleUnit()` 重新聚合 traits。
- `RefreshEquipmentProjection()` 在战斗内换装后重新聚合 traits。
- 替换 `BattleUnitState` 旧分组 trait fields。
- 更新 clone、schema、AI mutation guard。
- 改造 `PassiveStatusOrchestrator` 与 `TraitTriggerHooks`。

### Phase 5：清理与文档

- 删除旧 `RaceTraitDef` / `race_trait_defs` 正式入口。
- 更新资源 fixture 和验证脚本。
- 更新 `docs/design/project_context_units.md` 的相关 CU read set 与 ownership 描述。

## 测试计划

### 内容与 Schema

- 新增 `tests/progression/identity/run_trait_content_registry_regression.cs`。
- 新增 `tests/progression/schema/run_trait_instance_state_schema_regression.cs`。
- 扩展 progression content validation，确认 `trait_defs` 是正式 bucket。
- 扩展 `PartyMemberState`、`EquipmentInstanceState`、`PartyState` round-trip tests。
- 旧 `race_trait_defs` 不再作为正式 runtime/content 入口。

### 装备与仓库

- 新增 `tests/equipment/run_equipment_trait_roll_regression.cs`。
- 扩展 equipment drop / warehouse batch swap / warehouse state validator tests。
- 验证装备、卸装、batch swap、warehouse round-trip 保留 `trait_instances`。
- 验证 item template merge 不丢 `trait_ids` / `trait_roll_groups`。
- 固定 RNG 验证 roll 结果，不断言概率分布。

### 聚合与属性

- 新增 `tests/progression/identity/run_character_trait_service_regression.cs`。
- 覆盖聚合顺序 `identity -> character -> equipment`。
- 覆盖 ascension suppress 对原 race/subrace 的影响。
- 覆盖 source metadata、防御性拷贝、stack policy。
- 扩展 attribute context regression，确认 trait modifiers 进入快照。
- 确认 wrong typed key 不 fallback。

### 战斗

- 重写 `tests/battle_runtime/skills/run_trait_trigger_regression.cs`。
- 新增或替换 character trait projection regression。
- 扩展 `tests/battle_runtime/state_schema/run_battle_unit_state_schema_contract_regression.cs`。
- 覆盖开战投影、战斗内换装刷新、trigger dispatch、battle start、turn start。
- 旧 `race_trait_ids/subrace_trait_ids/bloodline_trait_ids/ascension_trait_ids` 在新 schema 中作为 extra field 拒绝。

### 推荐验证命令

```bash
dotnet build magic.csproj
python tests/run_regression_suite.py --pattern tests/progression
python tests/run_regression_suite.py --pattern tests/equipment
python tests/run_regression_suite.py --pattern tests/warehouse
python tests/run_regression_suite.py --pattern tests/battle_runtime
python tests/run_regression_suite.py --pattern tests/runtime
```

不要把 numeric battle simulation 加入常规验证。只有做数值模拟、平衡分析或 AI 行为分析时才单独运行 battle simulation。

## 非目标

- 第一版不实现完整词缀 UI。
- 第一版不做旧存档兼容。
- 第一版不做 age stage trait runtime projection。
- 不用 battle simulation 验证 trait schema、聚合顺序、roll determinism 或 trigger 单点行为。
- 不把 `CharacterTraitService` 扩展成战斗规则总管。

## 项目上下文影响

真正实施代码后，需要更新 `docs/design/project_context_units.md`，至少涉及：

- CU-02：Save / Session / Registry
- CU-10：背包 / 装备 / 物品
- CU-11：队伍与成员状态模型
- CU-12：CharacterManagement 桥接
- CU-13：Progression 内容定义
- CU-14：Progression 规则与属性服务
- CU-15：战斗运行时总编排
- CU-16：战斗规则 / AI / 伤害

本文件只是设计计划；在代码未实施前，不更新上下文索引。
