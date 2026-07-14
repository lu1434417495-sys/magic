# Equipment Ability Subagent Review Findings

记录时间：2026-06-30

本文档只归档子代理返回的检视意见，避免后续上下文压缩或会话切换丢失。这里的条目尚未经过主代理逐条反驳/确认；后续处理流程仍是：先结合代码和现有文档尽可能反驳，无法反驳的再进入 `equipment_ability_system.md` 或对应子文档。

## 子代理状态

| 子代理 | 关注面 | 状态 |
| --- | --- | --- |
| `019f1447-ebcd-76b0-98ff-d987035b9b27` | battle runtime / resolver / preview / AI / writeback | 已返回 |
| `019f1448-5c30-7733-8b28-6278f6b5eaa2` | 玩法覆盖 / by_family / AI 玩法一致性 / 验收 | 已返回 |
| `019f1448-1f6e-7770-807d-854cb3e594e3` | 内容结构 / C# Resource ABI / MOD / validation / schema | 已返回 |

## 主代理判定摘要

判定时间：2026-06-30

结论：这轮没有发现可以完全反驳的伪问题。多数问题不是“现有文档完全没提”，而是“现有文档提到了方向，但没有写到足以约束实现的 owner、字段、顺序、validator 或验收台账”。这些问题如果不处理，后续实现仍会出现旧命令不失效、敌方装备能力不可见、battle-end 写回漏失、V1 能力矩阵与实际 ABI 脱节等问题。

| 编号 | 子代理意见 | 主代理判定 | 处理优先级 | 处理方向 |
| --- | --- | --- | --- | --- |
| R1 | `ScopedAutoCast` 未落到 auto-cast 命令身份链 | 真问题。总文档已写“AutoCast consumer 只接受 ScopedAutoCast”且禁止装备 grant 生成 `AutoCastRequest`，但没有明确当前 `ExecuteAutoCast(...)` / `BuildAutoCastCommand(...)` 如何产生 `skill_entry_id`；当前代码仍临时写 `known_active_skill_ids`，且 `TryGetLearnedSkillLevel(...)` 只看 `is_learned`，会放过 race/bloodline/ascension 等授予来源。 | P0 | availability 子文档已补 auto-cast 迁移：`scoped_auto:{owner_member_id}:{setup_id}:{instance_id}:{skill_id}`、`CastLevel` entry level、true-learned source gate、command context、禁止临时 known mutation 和回归要求。 |
| R2 | 敌方 `attack_equipment_item_id` 只投影武器，不具备装备能力 source | 真问题，已确认 V1 支持敌方装备能力，但只做 battle-only synthetic equipment source。当前敌方链路确实只有 `WeaponProjection`，必须在 battle setup materialize `EquipmentState` / `EquipmentInstanceState` / effective equipment trait。 | P0 | 主文档已补：enemy attack equipment 生成战斗内真装备实例，不做战斗外持久敌装，不写回、不自动 loot，使用 `enemy_attack_equipment:{unit_id}:{item_id}` 稳定 source id。 |
| R3 | `on_battle_end` 与装备 writeback 顺序冲突 | 真问题，已确认 B 必须完整支持提交：当前 `FinalizeBattleResolution(...)` 先 `CommitBattleLocalViewsToPartyStateTyped(...)`，之后 `EndBattle(...)` 提交资源/contingency；battle-end hook 若改装备会错过 writeback，若只 trace/no-op 又是假支持。 | P0 | 主文档已改为 staged commit pipeline：`on_battle_end` mutating action 必须声明 `BattleEndCommitStage` + commit owner，并在 `PrePartyWriteback` / `PreLootCommit` / `PreProgressionCommit` 中完整提交；缺 adapter 或缺测试即 blocking。 |
| G1 | 破甲、穿盾、无视闪避/掩体没有攻击防御模型 | 真问题。现有 `BattleHitResolver` 使用聚合 `armor_class`，没有每次攻击的 AC component / cover policy 修正；总文档把它列为 V1 补强但没有 DTO/API。 | 已处理 | 主文档已补 `EquipmentAttackDefenseModifierDef`、`EquipmentAttackDefenseAdjustment`、AC component snapshot、`BattleAttackCheckPolicyService -> BattleHitResolver` 接线、validator 和回归要求。 |
| G2 | 环境事实缺 owner/schema | 真问题。文档有 `environment_tag`，但当前 battle state 主要是 `terrain_profile_id`，没有 `BattleEnvironmentSnapshot`、全局/局部 tag、注入和 AI restore 规则。 | 已处理 | 主文档已补 battle-local `BattleEnvironmentSnapshot`、`BattleEnvironmentContextProvider`、`battle/coord/path_environment_tag`、validator/test 要求；全局环境仅本场战斗生效，世界地图不引入 weather owner。 |
| G3 | 强制目标、随机敌我、禁打目标未成为 UI/AI/执行共同规则 | 部分真问题，但范围需要收窄。用户已确认：若能力建模为装备授予技能/攻击变体，则走技能选择控制；若建模为固定特性，则条件不满足时该特性不生效。只有普通攻击/行动本身被强制随机、禁止或 AI 接管时，才可能需要目标策略系统。 | 暂缓 / 待确认 | 先不设计 `EquipmentTargetingPolicyDef`。先记录分类和不确定项，等具体武器落内容时逐条确认。 |
| G4 | 多射、装填、射后移动、同回合施法等行动经济能力没有 owner | 真问题但已按用户决策裁剪。当前目标是先搭建装备能力框架，疑问玩法内容暂不处理；Availability 只解决“入口可用”，不解决一个入口的 AP/TU、reload、post-shot movement、multi-stage plan。 | 已裁剪 / 延期 | 主文档已明确 V1 不支持复杂行动经济、装填、射后移动、同动作攻击+施法、连续射击疲劳、死亡前免费攻击、stealth/reveal、逃跑/投降等 owner 缺失能力；这些 bullet 只能 `deferred` / `content-cut` 或 validator owner-missing。 |
| G5 | 休息/每日/月度/持久代价延期但缺 V1 validator/UI 策略 | 真问题，且范围已调整。每日、月度和装备实例永久计数必须进入 V1，并明确绑定 world map；短休/长休没有项目概念，仍必须拒绝。 | P1 | 主文档已改：V1 支持 `per_world_day`、`per_world_month`、`persistent_counter`，owner 是 `EquipmentInstanceState`，period 由 `WorldTimeSystem` 派生；`per_rest` / `per_short_rest` / `per_long_rest` blocking。 |
| G6 | by_family 覆盖停在文件/机制层，没有 per-bullet ledger | 真问题，但按用户决策暂不做全量检查。框架期只要求文件级/机制族覆盖矩阵和 binding 来源追踪 metadata；不把每个自然语言 bullet 缺 ledger entry 作为阻塞。 | 框架保留 / 全量延期 | 主文档已补 `source_traces` 结构：`source_file`、`item_id`、bullet、机制族、状态、phase、test id；validator 只校验已填写 trace 的格式，不扫描全量缺口。 |
| A1 | 装备授予技能 EntryId 不含装备实例 | 已处理。`equipment_skill` 格式已改为包含 `source_equipment_instance_id`，并补充同 binding/trait/action/skill 换装时旧 command 必须 stale 的规则。 | 已处理 | 见 [battle_skill_availability_migration.md](battle_skill_availability_migration.md) 和 [equipment_ability_system.md](../equipment_ability_system.md)。 |
| A2 | Binding Resource ABI 与 DTO 不一致，`weapon_profile_overlays` 权威块缺失 | 已处理。详细 `EquipmentAbilityBindingDef` Resource ABI 已补 `weapon_profile_overlays`，并明确 DTO 映射、projection 排序和 replace 粒度。 | 已处理 | 见 [equipment_ability_system.md](../equipment_ability_system.md)。 |
| A3 | 多个 Resource 示例缺 `[Export]` / `[GlobalClass]` | 已处理。主文档已增加 Authoring Resource ABI 导出规则和逐类字段清单，并补齐 condition/action payload、overlay、roll/outcome/state/granted/world 等 Resource 示例的 `[GlobalClass]` / `[Export]`。 | 已处理 | 见 [equipment_ability_system.md](../equipment_ability_system.md)。 |
| A4 | typed diagnostics 未接现有 content validation snapshot/schema | 真问题，但已降级为 MOD-ready 硬化项，不作为 basic V1 基本功能 blocker。basic V1 只要求 registry build `Success/Errors` fail-fast，invalid pack/binding 不进入索引和 projection。 | 延期 / MOD-ready | 完整 `equipment_ability` validation domain、typed diagnostic public schema、string fallback、headless/text validation 输出延后；主文档已补 basic V1 与 MOD-ready 边界。 |
| A5 | creature type tag 的 MOD 扩展/校验 schema 没落点 | 已处理。生物分类由 unit / progression taxonomy 控制；装备能力只消费 `BattleUnitState.creature_type_tags` 与 `KnownCreatureTypeTags`。原先“pack ABI 声明 taxonomy”的方向已纠正。 | 已处理 | 主文档已补 unit taxonomy registry / `CreatureTypeTagContentRules`、`KnownCreatureTypeTags` validation context、battle unit projection、enemy/party materialize 规则和运行时禁止回查 catalog。 |
| A6 | `project_context_units.md` 未纳入拆分后的装备能力文档 | 已处理。现在有主文档和子文档，context map 已加入装备能力 read-set，避免后续实现者漏读 ABI、availability 和 durability selector 拆分。 | 已处理 | 已更新 CU-10、CU-13、CU-15、CU-16，并新增“只改装备能力框架、内容 ABI、validator、战斗投影或装备授予技能”推荐装载组合。 |

### 非完全阻塞但必须先决策的范围项

- R2 敌方装备能力：已决策为 V1 支持 battle-only synthetic equipment source，不做战斗外持久敌方装备资产。
- G4 行动经济：已决策为 V1 框架期不处理复杂多射/装填/射后移动/同动作攻击施法等疑问玩法；后续内容 ledger 只能按 bullet 标为 V2/deferred/content-cut 或 validator-rejected。
- G5 rest/day/month/persistent：每日/月度/装备实例永久计数已决策进入 V1；短休/长休没有正式概念，必须 validator 拒绝或内容裁剪。

### 可立即修文档的低争议项

- A1：已处理，`equipment_skill` id 已包含 `source_equipment_instance_id`。
- A2/A3：已处理，Authoring Resource ABI 已补 `weapon_profile_overlays`、导出规则和逐类 `[Export]` 清单。
- A4：已降级为 MOD-ready；basic V1 只补 registry build `Success/Errors` fail-fast。
- A5：已处理，creature type taxonomy owner / validation context / battle unit projection 已写入主文档。
- A6：已处理，`project_context_units.md` 已加入装备能力 read-set 和推荐装载组合。

## Runtime / Code Integration 意见

### 1. 阻塞：`ScopedAutoCast` 没有落到当前 auto-cast 命令身份链

为什么可能是真问题：

- `battle_skill_availability_migration.md` 要求 skill command 缺 `skill_entry_id` 时被拒绝。
- 当前 `ExecuteAutoCast` / `BuildAutoCastCommand` 只生成 `skill_id`。
- auto-cast 当前还会临时写入 `known_active_skill_ids` / level 后直接执行。
- 严格 access gate 落地后，这条路径要么绕过新契约，要么直接失效。

关联代码：

- `scripts/systems/battle/runtime/BattleSkillExecutionOrchestrator.AutoCast.cs`
- `ExecuteAutoCast(...)`
- `BuildAutoCastCommand(...)`
- `scripts/systems/battle/core/AutoCastRequest.cs`

原先文档缺口：

- 文档提到 `ScopedAutoCast` source kind，但没有明确当前 contingency auto-cast 如何生成 `skill_entry_id`。
- 没有明确 auto-cast 如何构造 `BattleSkillCommandContext`。
- 没有明确 auto-cast 是否允许绕过 `PreviewCommand` / `IssueCommand` 的统一 access gate。

子代理建议补充方向：

- 在 availability 子文档加入 auto-cast 迁移段。
- 用 `AutoCastRequest.SetupId` / `InstanceId` / `StoredSkillId` 生成稳定 `scoped_auto:{scope}:{skill_id}`。
- 以 `CastLevel` 作为 entry level。
- `BuildAutoCastCommand(...)` 必须带 entry id。
- 或明确 auto-cast 使用专用 context，而不是普通 skill command。
- 增加回归证明 auto-cast 不污染 known-only 列表。

用户确认后的补强约束：

- `AutoCastRequest.StoredSkillId` 和 auto-cast 规则中用于触发/充能/释放资格的来源技能，必须是使用者自己已经真实学会的技能。
- 当前代码里的 `GameRuntimeFacade.Contingency.TryGetLearnedSkillLevel(...)` 只检查 `UnitSkillProgress.is_learned`，不够；R1 实现需要显式检查 `UnitSkillProgress.granted_source_type`，V1 true-learned 默认只允许 `player` 来源。
- 装备、race/subrace/bloodline/ascension、profession grant、状态或临时 battle-only entry 让技能“当前可用”时，也不能满足 auto-cast source gate。
- `ScopedAutoCast` 是执行 scope，不是临时授予技能的入口；实现时要移除 `ExecuteAutoCast(...)` 对 `known_active_skill_ids` / level map 的临时写入。

### 2. 阻塞/范围缺口：敌方 `attack_equipment_item_id` 只投影武器，不具备装备能力 source

为什么可能是真问题：

- 装备能力 source 设计依赖 `effective_trait_instances + equipment_view` 反查装备实例、槽位、instance id。
- 敌方模板当前只是把 `attack_equipment_item_id` 转成 `WeaponProjection`。
- 当前没有为敌方攻击武器 materialize `EquipmentState`，也没有装备 trait aggregation。
- 结果是敌人拿同一把带能力的武器时，AI availability、preview、execution 都看不到装备能力，除非 V1 明确不支持敌方装备能力。

关联代码：

- `scripts/enemies/EnemyTemplateDef.cs`
- `EnemyTemplateDef.GetWeaponProjectionTyped(...)`
- `scripts/systems/world/EncounterRosterBuilder.cs`
- `ApplyEnemyWeaponProjection(...)`
- `scripts/systems/battle/runtime/BattleUnitFactory.cs`
- `_build_runtime_enemy_unit(...)`

原先文档缺口：

- 文档讲了 enemy `creature_type_tags` 投影。
- 文档也讲 AI 消费 availability view。
- 但没有定义 enemy `attack_equipment_item_id` 是否参与装备能力系统。
- 如果参与，没有定义 source instance id、writeback、loot 边界。

子代理建议补充方向：

- 方案 A：V1 禁止 enemy attack equipment item 携带 equipment ability binding，并由 validator 报 blocking diagnostic。
- 方案 B：battle setup 为敌方 attack equipment 创建 battle-only synthetic `EquipmentState` / `EquipmentInstanceState` 与 effective trait projection，使用稳定 source key，不进入 party writeback / loot。

用户确认后的处理结论：

- 采用方案 B。
- V1 做战斗内真装备实例：enemy `attack_equipment_item_id` 在 battle setup materialize 为 battle-only `EquipmentInstanceState` / `EquipmentEntryState` / `EquipmentState`。
- 不做战斗外持久敌方装备资产：synthetic enemy equipment 不写回 enemy template、world runtime、party、warehouse，也不自动成为 loot。
- instance id 必须 battle-local 且稳定，推荐 `enemy_attack_equipment:{unit_id}:{item_id}`；`unit_id` 必须进入 id，避免同一模板多敌人 source key 冲突。
- synthetic enemy equipment 可被装备能力 source resolver、weapon overlay、granted skill availability、AI scoring 和耐久 action 使用；被摧毁只影响当前 battle unit。
- 默认不滚随机装备词条；V1 只支持 item 固定 trait / sidecar binding。未来若要敌方随机词条或掉落同一件被打坏装备，需要单独设计 encounter seed、loot ownership 和耐久继承。
- 主文档已按该结论更新，见 [equipment_ability_system.md](../equipment_ability_system.md) 的 `Enemy attack equipment battle-only source`。

### 3. 高风险未明确：`on_battle_end` 与装备 writeback 顺序冲突

为什么可能是真问题：

- 文档 trigger taxonomy 包含 `on_battle_end`。
- 文档同时说耐久/摧毁复用现有 writeback。
- 当前结算链路先 `CommitBattleLocalViewsToPartyStateTyped(...)`，之后才 `_battle_runtime.EndBattle(...)`。
- 如果 `on_battle_end` 在 `EndBattle` 或 battle-ended 阶段修改 `equipment_view`，这些改动已经错过 party writeback。

关联代码：

- `scripts/systems/game_runtime/GameRuntimeFacade.BattleResolution.cs`
- `FinalizeBattleResolution(...)`
- `scripts/systems/battle/runtime/BattleRuntimeModule.cs`
- `EndBattle(...)`
- `scripts/systems/battle/runtime/BattleTimelineDriver.cs`
- `CheckBattleEnd(...)`

当前文档缺口：

- V1 范围写“核心 triggers”时未明确 `on_battle_end` 是可执行、只保留 taxonomy，还是 validator blocking。
- 没有定义 battle-end hook 相对 writeback、loot、resource commit 的顺序。

子代理建议补充方向：

- 原始选项 A：V1 中 `on_battle_end` 默认不可执行。该选项已因“必须真实扣耐久”被否决。
- 原始选项 B 如果解释成“只允许非 writeback mutation”同样不能满足真实扣耐久，也会产生文档支持但 runtime 不提交的假能力。
- 采用方向：B 必须解释成 `on_battle_end` 的 staged commit pipeline。凡是 V1 validator 允许的 battle-end action，都必须声明 stage、commit owner、adapter 和测试，并真实提交。

用户确认后的处理结论：

- 选完整提交方案：`on_battle_end` 是 `GameRuntimeFacade.FinalizeBattleResolution(...)` 内的正式 staged commit pipeline，不是 `_battle_runtime.EndBattle(...)` 内的普通 hook。
- 新增 `EquipmentAbilityBattleEndCommitStage`：`PrePartyWriteback`、`PreLootCommit`、`PreProgressionCommit`。
- 装备耐久/摧毁必须声明 `PrePartyWriteback`，插入 rollback capture 之后、`CommitBattleLocalViewsToPartyStateTyped(...)` 之前，确保玩家装备真实写回 party equipment state。
- 其它 battle-end action 只有在声明 stage + commit owner、存在 adapter 和回归覆盖时才允许；缺一项即 blocking diagnostic，不能只生成 trace/no-op。
- 失败必须走 battle finalization rollback，不能出现装备、loot、resource、party 状态部分提交。
- 主文档已按该结论更新，见 [equipment_ability_system.md](../equipment_ability_system.md) 的 `Battle-end staged commit pipeline`。

## Gameplay / By-Family 意见

### 1. P1：破甲、穿盾、无视闪避/掩体没有可落地的攻击防御模型

为什么可能是真问题：

- 装备耐久可以“打坏盾/甲”，但很多能力要求的是“本次攻击计算命中时无视某类 AC 或掩体”。
- 这不是耐久效果，也不是简单 `+hit`。
- 当前战斗命中解析主要读聚合 AC。
- 如果没有每次攻击的防御组件修正，玩家预览、执行、AI 评分会无法一致。

by_family / 玩法证据：

- `bows_01.md`：Armorcrush Crossbow 无视金属护甲 AC、50% 盾 AC 无效。
- `bows_01.md`：Shadowbow / Light Crossbow 让目标 AC 无敏捷。
- `bows_01.md`：Phantom Crossbow 穿墙/掩体。
- `maces_morningstars_01.md`：Armorcrush Mace 无视金属护甲并压扁盾。
- `axes_01.md`：绕盾、无视骨甲等效果。

原先文档缺口：

- `equipment_ability_system.md` 明确说 V1 weapon overlay 不支持 shield bypass / attack mode。
- 但后面的文件矩阵又把破甲穿盾列为 V1 补强方向。
- 缺少统一 DTO、preview trace、AI 消费点和测试口径。

子代理建议补充方向：

- 增加 `EquipmentAttackDefenseModifierDef` 之类的模型。
- 明确 `ignored_ac_components`、`lock_dodge_bonus`、`cover_policy`、`projectile_obstacle_policy` 如何进入 preview / execution / AI。
- 或把这些 by_family 能力明确降级为 V2 / content-cut。

用户确认后的处理结论：

- 选择 V1 补强，不降级到 V2。
- `EquipmentAttackDefenseModifierDef` 是 `before_attack_roll` / attack check 阶段 action payload，不是 weapon profile overlay，也不是普通 `+hit`。
- 新增 runtime `EquipmentAttackDefenseAdjustment` / `EquipmentDefenseComponentSnapshot`，在单次 attack check 中忽略或缩放 `armor_ac_bonus`、`shield_ac_bonus`、`dodge_bonus`、`deflection_bonus`，并支持临时 dodge lock。
- 目标装备材质/类型过滤通过 `required_target_equipment_selector` + item tags/type 走 `EquipmentAbilityEquipmentTargetSelector`，未命中时 no-op trace。
- `cover_policy` / `projectile_obstacle_policy` 必须有真实 owner；当前没有 owner 时 validator blocking，不能静默变成命中加值。
- preview、execution、AI scoring 都必须通过 `BattleAttackCheckPolicyService -> BattleHitResolver` 同一 adjusted AC 路径，不能各自重算。
- 主文档已按该结论更新，见 [equipment_ability_system.md](../equipment_ability_system.md) 的 `EquipmentAttackDefenseModifierDef` 和 `BattleAttackCheckPolicyService`。

### 2. P1：Battle 环境事实只列了类别，没有 owner/schema，夜晚、风暴、水域等能力不可验收

为什么可能是真问题：

- 很多装备强度依赖夜晚、黑暗、风暴、水域、森林、寒冷、炎热。
- 文档说有 `EquipmentAbilityEnvironmentContext`，但没有战斗状态字段、tag 枚举、刷新时机、格子局部环境和世界时间/天气投影规则。
- 实现时容易每个系统各判各的，AI 和玩家预览不一致。

by_family / 玩法证据：

- `axes_01.md`：雷暴、月相、水上载具、寒暖环境。
- `bows_01.md`：夜晚无限射程、黑暗、风、阳光充能、风暴。
- `clubs_staves_01.md`：冻结水面、森林/树根。
- `maces_morningstars_01.md`：雷电/风暴。

当前文档缺口：

- `equipment_ability_system.md` 只是列出 `environment_tag` 和分类。
- 当前代码层面的 `BattleState` 主要只有 `terrain_profile_id` 这种入口。
- 没有文档化的 `BattleEnvironmentSnapshot` 或测试 fixture。

子代理建议补充方向：

- 补环境事实契约。
- 明确全局 tags、格子/局部 tags、来源优先级、战斗开始后是否冻结、AI restore、headless 测试如何注入。
- 月相/长期天气若 V3，就在 V1 内容验收里明确拒绝或降级。

处理结论：

- 主文档已把旧的单一 `environment_tag` 拆成 `battle_environment_tag`、`coord_environment_tag`、`path_environment_tag`。
- 新增 battle-only `BattleEnvironmentSnapshot`，由 `BattleState` 持有，battle setup 时物化 `night`、`storm`、`moonlit`、`cold`、`heat` 等全局 tag，战斗结束即失效。
- 局部环境由 `BattleEnvironmentContextProvider` 从 `BattleCellState.base_terrain/current_height/terrain_effect_ids/timed_terrain_effects` 和路径上下文派生，不从 `terrain_profile_id` 字符串硬猜。
- `BattleEnvironmentTagContentRules` 负责 known tag set；装备 ability pack 不声明环境 tag，未知 tag、缺 coord/path context、需要 world weather owner 的 tag 都是 blocking validation error。
- 世界地图不引入天气系统；按日期推导月相、长期天气、跨战斗天气预报不属于 V1。月相类 V1 文案只能映射成 battle-local `moonlit` / `full_moon_light` 显式 tag。
- preview、execution、AI scoring、headless 都读同一 provider；AI mutation guard 必须验证 snapshot 不被污染。

### 3. P1：强制目标、随机敌我、禁打目标只被承认，未设计成 UI/AI/执行共同规则

为什么可能是真问题：

- 狂暴乱打、普通攻击被强制随机、只能追猎标记目标、治疗武器是否替代普通攻击等，可能不是命中后的效果，而是目标候选集和命令合法性。
- 若只在执行时随机/拒绝，玩家选择、预览高亮、AI 选招和实际命中会脱节。

by_family / 玩法证据：

- `axes_01.md`：Berserker's Bite 低血量随机攻击含友军。
- `axes_02.md`：Berserker's Axe 不能区分敌友。
- `bows_01.md`：Dragonbreath Bow 的 `龙息矢` 不能对 `dragon` 类型生物使用；普通攻击不应因此被禁止。
- `bows_02.md`：Hunter's Bow / Prey Bow 有追猎目标限制。
- `maces_morningstars_01.md` 和 `clubs_staves_01.md`：只能治疗/不能伤害类武器。

当前文档缺口：

- `equipment_ability_system.md` 只列 `force_target` / `override_targeting` 动作类型。
- 矩阵也标注“target policy 需补强”。
- 但没有定义它修改 selection validation 还是 execution retarget。
- 没有定义 RNG 时机、友军误伤 AI 策略或 UI 警告。

子代理建议补充方向：

- 补 `EquipmentTargetingPolicyDef`。
- 至少覆盖 `allow_any_team`、`force_random_from_scope`、`forbid_target_if_fact`、`requires_mark/judgement`。
- 写清 preview 文案、AI friendly-fire 评分、execution trace、headless 验收。

用户确认后的暂缓记录：

- 当前不落地完整 `EquipmentTargetingPolicyDef`。
- 建模为装备授予技能 / 攻击变体的能力，全部走 `BattleSkillAvailabilityService`、技能选择、skill target validation、preview 和 execution gate。例：`龙息弓` 的 `龙息矢` 如果做成授予技能/攻击变体，对 `dragon` 目标时该入口灰掉或目标不可选，但普通攻击仍可用。
- 建模为固定生效特性的能力，不改变技能入口；条件不满足时该特性不生效。例：对 `dragon` 额外伤害、对 `undead` 额外 radiant、命中附加毒/火/恐惧等，都走 condition/action gate。
- 只有“普通攻击/行动本身”被装备或状态强制随机、强制 miss、禁止、重定向或 AI 接管时，才保留为后续 G3 目标策略候选。

暂时无法确认、后续逐条问用户：

| 能力 | 不确定点 | 暂定处理 |
| --- | --- | --- |
| `Berserker's Bite` 低血随机攻击含友军 | 是普通攻击自动重定向，还是进入 AI 接管/狂乱状态？ | 暂缓；若是 AI 接管，优先复用 madness/turn control；若是自动重定向，再考虑 target retarget policy。 |
| `Berserker's Axe` 狂暴无法区分敌我 | 玩家仍点目标但系统随机，还是角色由 AI 接管？ | 暂缓；倾向做成状态/AI 接管，而不是装备 target policy。 |
| `Hunter's Bow` / `Hunter's Axe` 不能攻击幼崽、孕妇、虚弱野兽 | `juvenile`、`pregnant`、`weak_beast` 是否是正式 creature/status fact？ | 暂缓；没有正式 fact owner 时不能 V1 真支持，只能内容裁剪或叙事。 |
| `Prey's Bow` 不能射击逃跑/投降目标 | `fleeing`、`surrendering` 是否是正式 battle status？ | 暂缓；若没有状态 owner，不能 V1 真支持。 |
| `Life Hammer` 命中不造成伤害而是治疗 | 是主动治疗技能，还是普通攻击被替换成治疗？ | 暂缓；主动治疗走技能选择，普通攻击替换治疗属于 damage-to-heal replacement，不归 target policy。 |

### 4. P2：多射、装填、射后移动、同回合施法等行动经济能力没有 owner

当前处理状态：已按用户决策裁剪为 V1 延期项。框架期不继续逐件处理这些疑问能力，也不补行动经济契约；对应 by_family bullet 后续只能在内容 ledger 中标为 `deferred` / `content-cut`，或由 V1 validator 报 owner-missing。

为什么可能是真问题：

- 这些能力决定武器好不好玩。
- 若当作普通 granted skill，很可能绕过 AP/TU、冷却、装填、on-hit 触发次数。
- AI 可能只按一击评分但执行三击。

by_family / 玩法证据：

- `bows_01.md`：Chain Crossbow 三连射并带递增惩罚和卡壳。
- `bows_01.md`：Armorcrush Crossbow 两动作装填。
- `bows_02.md`：Cheetah Crossbow 双射、快速装填、连续三回合后休息。
- `bows_02.md`：Swan Crossbow 射后移动/击倒追加射击。
- `clubs_staves_02.md`：Battle Mage's Club 近战攻击并同动作施法。

当前文档缺口：

- `battle_skill_availability_migration.md` 解决的是技能入口可用性，不解决一个入口如何消耗行动并产生多段攻击。
- 主文档只列 `add_extra_attack`、`disable_action` 等动作名。
- 没有 AP/TU owner、repeat attack 集成、触发计数和 AI plan 形状。

子代理建议补充方向：

- 把这些能力明确归为 V2。
- 或补行动经济契约：使用现有 repeat/random-chain profile，还是新增 equipment action economy modifier。
- 定义每段攻击惩罚、on-hit 触发次数、装填计数、AI 评分和回归测试。

### 5. P1：每日/月度/永久计数必须 world-map 绑定；短休/长休仍无 owner

为什么可能是真问题：

- 很多 by_family 能力是每日、长休、每月、24 小时、每 10 发等资源。
- 如果 V1 偷换成每战斗，文本会误导。
- 如果不限制次数，平衡会坏。
- 如果直接持久化，又踩兼容/存档高风险。

by_family / 玩法证据：

- `clubs_staves_01.md`：Worldtree / Heal Staff 有每日治疗或树根。
- `maces_morningstars_01.md`：Light / Heal Morningstar 有每日治疗。
- `bows_01.md`：Viper Crossbow 每 10 发补毒、Starbow 每射老化、Stormbow / Thunderbow 长休。
- `axes_01.md`：Phoenix、Night Axe、Lunareclipse 有长周期代价或月相。

当前文档缺口：

- 原主文档把 rest/day/month 和持久状态整体延后，不符合最新决策。
- 当前代码有 `WorldTimeSystem.StepToDay(world_step)`，但没有 month owner；V1 需要把 month index 也放进 `WorldTimeSystem`。
- `EquipmentInstanceState` 是 exact fields payload，新增 world-bound usage / persistent counter 必须明确 save schema 和校验范围。

已确认补充方向：

- V1 允许 `per_battle`、`per_turn`、`cooldown_tu`、`per_world_day`、`per_world_month`、`persistent_counter`。
- `per_world_day` 绑定 `WorldTimeSystem.StepToDay(world_step)`；`per_world_month` 绑定 `WorldTimeSystem.StepToMonth(world_step)`，默认 `30 world days = 1 world month`。
- `per_world_day` / `per_world_month` / `persistent_counter` 的 owner 是 `EquipmentInstanceState`，状态随装备实例移动、保存和战后 writeback。
- `per_day` / `per_month` 这类不带 world 前缀的模糊策略必须报错并提示改用 world 版本。
- `per_rest`、`per_short_rest`、`per_long_rest` 必须 validator 报 owner-missing，因为当前项目没有短休/长休概念。
- 永久计数只表示装备实例自己的累计值；永久改角色/世界/剧情仍需要 V3 party/world consequence owner。

### 6. P2：覆盖验收停在文件/机制层，无法证明每个 by_family 能力 bullet 已分类

为什么可能是真问题：

- 文档要求所有 by_family 文件进入矩阵，但文件级矩阵会让“看起来覆盖完整”。
- 一个装备常同时有 V1 战斗效果、V2 地形/召唤、V3 世界或持久代价。
- 只标文件机制会漏掉具体 bullet，后续 `.tres` 绑定也无法验收。

by_family / 玩法证据：

- `axes_01.md`：Berserker's Bite 同时有低血增伤、随机友军、永久理智/属性损失。
- `bows_01.md`：Armorcrush Crossbow 同时有破甲和装填。
- `clubs_staves_02.md`：Scholar Staff 有智力战斗加成和每日知识优势。
- `maces_morningstars_01.md`：Heal Morningstar 是治疗武器而非普通伤害武器。

原始文档缺口：

- `equipment_ability_system.md` 有覆盖原则和代表性测试。
- 没有必需的 per-item / per-bullet ledger：`item_id`、自然语言 bullet、阶段、handler、deferred reason、测试 ID。

原子代理建议补充方向（已按用户决策收窄，不作为当前框架期要求）：

- 原建议是增加可机读或固定格式的 coverage ledger，并让每个 bullet 都进入 `bound`、`validator-rejected`、`deferred` 或 `content-cut`。
- 该方向适合内容制作阶段；当前框架期不做全量 completeness gate。
- 当前只保留 binding 级 `source_traces`，确保已制作的资源能追到设计来源。

处理结果（2026-06-30）：

- 用户明确：暂时不考虑全量检查，先做框架。
- 主文档保留文件级/机制族覆盖矩阵，不再要求 V1 框架期建立完整 per-item / per-bullet ledger。
- 新增 `EquipmentAbilitySourceTraceDef` / `EquipmentAbilitySourceTraceDefinition`，作为 binding 的作者追踪 metadata：`source_file`、`item_id`、`bullet_index`、`bullet_title`、`bullet_text`、`mechanism_family`、`coverage_status`、`phase`、`test_id`。
- V1 validator 只校验已填写 `source_traces` 的格式和 enum 转换；不扫描 by_family 全量 bullet，也不因缺少某条 bullet 的 ledger entry 阻塞 PR。
- 独立 coverage ledger 和全量 completeness check 延后到内容制作阶段。

## Content ABI / Validation 意见

### 1. P0：装备授予技能的 EntryId 没有携带装备实例，可能无法判定旧指令失效

为什么可能是真问题：

- 文档要求同一个 `SkillId` 从装备 A 切到装备 B 时，旧选择 / 旧指令必须失效。
- 当前建议的 `equipment_skill:{binding_id}:{effective_instance_key}:{granted_action_id}:{skill_id}` 不含 `source_equipment_instance_id`。
- 现有 trait stack 逻辑会把 `effective_instance_key` 折叠成 trait 级 key，所以它不足以代表具体装备实例。

关联证据：

- `docs/design/weapons/equipment_ability/battle_skill_availability_migration.md`
- `docs/design/weapons/equipment_ability_system.md`
- `scripts/systems/progression/CharacterTraitService.cs`

当前文档缺口：

- 没有明确 `SkillEntryId` 或 `BattleCommand` / selection 必须保存并校验 `EquipmentInstanceId`。

子代理建议补充方向：

- 方案 A：把 `source_equipment_instance_id` 纳入 `SkillEntryId`。
- 方案 B：要求命令和选择持有完整 `BattleSkillEntryRef`，并在执行前校验 `EquipmentInstanceId` 未变化。

处理结果（2026-06-30）：

- 采纳方案 A。
- `battle_skill_availability_migration.md` 和 `equipment_ability_system.md` 中的 equipment granted skill id 改为 `equipment_skill:{binding_id}:{source_equipment_instance_id}:{effective_instance_key}:{granted_action_id}:{skill_id}`。
- 文档已明确 `effective_instance_key` 可能被 trait stack policy 折叠，不能单独代表装备实例。
- 文档已要求卸下装备 A 后换上同 binding、同 trait、同 granted action、同 `SkillId` 的装备 B 时，A 的旧 command stale，不按 `SkillId` 或折叠后的 `effective_instance_key` 静默切到 B。

### 2. P1：Binding Resource ABI 与 DTO / 能力目标不一致，`weapon_profile_overlays` 在权威 Resource 类中缺失

为什么可能是真问题：

- V1 包含武器面板 overlay。
- DTO 和 overlay service 都依赖 `weapon_profile_overlays`。
- 但详细 `EquipmentAbilityBindingDef` Resource 形状没有这个字段。
- 按当前 ABI 实现后，MOD 无法写 overlay，运行时 DTO 也会永远为空。

关联证据：

- `docs/design/weapons/equipment_ability_system.md`
- 高层结构提到 `WeaponProfileOverlay`。
- DTO 和服务章节提到 `EquipmentWeaponProfileOverlayDef` / `EquipmentWeaponProfileOverlayService`。

当前文档缺口：

- 高层结构、详细 Resource ABI、DTO 三者没有单一可信来源。

子代理建议补充方向：

- 在详细 `EquipmentAbilityBindingDef` 中补 `Godot.Collections.Array<EquipmentWeaponProfileOverlayDef> weapon_profile_overlays`。
- 同步声明覆盖顺序、唯一性、replace 规则和 DTO 投影规则。

处理结论：

- 已在主文档详细 `EquipmentAbilityBindingDef` Resource ABI 中补 `[Export] Godot.Collections.Array<EquipmentWeaponProfileOverlayDef> weapon_profile_overlays`。
- 已明确 `weapon_profile_overlays` 是 projection-only 子资源，Resource projection 映射到 `EquipmentAbilityBindingDefinition.WeaponProfileOverlays`，不进入 dispatcher action。
- 已明确排序沿用 weapon profile overlay projection 顺序：`priority`、equipment slot order、binding load order、source key、`overlay_id`、projection ordinal；字段覆盖由 `EquipmentWeaponProfileOverlayService` 顺序 apply。
- 已明确 `override_mode = replace_binding` 时整体替换 binding，不做 reaction/action/overlay/world effect 局部 merge。

### 3. P1：多个 C# Resource 示例缺 `[Export]` / `[GlobalClass]`，作为 MOD ABI 会不可序列化

为什么可能是真问题：

- 项目现有 `.tres` ABI 依赖 `[GlobalClass]` 和 `[Export]`。
- 设计文档把 Resource 形状定义为 V1 数据 ABI。
- 但 condition / action payload、state schema、granted action、overlay 字段等大量示例没有导出标记。
- 照抄实现可能导致 Godot 编辑器 / 资源加载拿不到字段。

关联证据：

- 项目惯例：`scripts/player/warehouse/WeaponProfileDef.cs`
- 项目惯例：`scripts/player/progression/SkillDef.cs`
- 项目惯例：`scripts/player/progression/CombatEffectDef.cs`
- 文档中的多个 Resource 示例代码块。

当前文档缺口：

- 没有说明哪些代码块是伪代码，哪些是稳定 ABI。
- 没有完整列出每个可序列化字段的导出标记。

子代理建议补充方向：

- 增加 “Authoring Resource ABI” 小节。
- 逐类列出精确 C# Resource 定义。
- 所有可写入 `.tres` 的字段都标 `[Export]`。
- 所有可作为子资源创建的类都标 `[GlobalClass]`。
- 运行时字段明确标为非 ABI。

处理结论：

- 已在主文档 `Resource 层结构` 开头新增 `Authoring Resource ABI 导出规则`。
- 已逐类列出必须 `[Export]` 的 authoring resource 字段，并明确 runtime definition DTO、handler spec、projection entry、selector result、mutation result、trace 和 runtime state 不属于 `.tres` ABI，不应 `[Export]`。
- 已把 condition payload、action payload、weapon profile overlay、roll/outcome、state schema、granted action、world effect 示例补成 `[GlobalClass]` + 字段级 `[Export]`。
- 主文档早段 `EquipmentAbilityBindingDef` 概要代码已标明只是概念边界，不是 Authoring Resource ABI，避免和详细 ABI 冲突。

### 4. P1：typed validation diagnostics 没接入现有 ContentValidation snapshot / schema

为什么可能是真问题：

- 文档要求装备能力诊断稳定包含 `Code` / `Severity` / `Path`。
- 当前 `GameSession` 验证快照只保存 `List<string> Errors`。
- domain 顺序也没有 `equipment_ability`。
- 如果直接塞字符串，会破坏 MOD 稳定诊断。
- 如果新增 domain，会牵动保存 / 显示 / 回归快照 schema。

关联证据：

- `docs/design/weapons/equipment_ability_system.md`
- `scripts/systems/persistence/GameSession.cs`
- `scripts/systems/persistence/GameSession.ContentValidation.cs`

当前文档缺口：

- 没有定义 typed diagnostics 如何投影到现有 validation snapshot、text snapshot、运行时阻断和测试输出。
- 没有定义稳定 path 语法，只给了局部示例。

子代理建议补充方向：

- 明确新增 `equipment_ability` domain 的数据形状。
- 或明确 typed diagnostics 在现有结构中的兼容投影。
- 同时定义稳定 path 语法。

处理结论：

- 这是工程维护和 MOD 体验上的真问题，但不是 basic V1 基本玩法 blocker。
- basic V1 不要求新增 save version、正式 save schema、`ContentValidationSnapshot` typed schema 或 headless/text validation typed projection。
- basic V1 必须保留 registry build 最小契约：`EquipmentAbilityRegistryBuildResult.Success`、`Revision`、`Errors`。存在 blocking error 时，invalid pack / binding 不得进入 `_packsById`、`_bindingsById`、trait/source/category 索引或 battle projection。
- basic V1 的 `Errors` 可以是 string，但必须可定位：优先包含 diagnostic code、resource path、binding id、handler id；缺上下文时至少包含 pack/binding 定位和错误原因。
- `EquipmentAbilityContentDiagnostic` 可以先作为内部结构或测试辅助存在，但不作为 basic V1 public ABI。
- MOD-ready 阶段再新增 `equipment_ability` validation domain，把 typed diagnostics 投影为 `diagnostics[]`，同时保留 `errors[]` string fallback，并补 headless/text validation 回归。
- 主文档已按该结论更新，见 [equipment_ability_system.md](../equipment_ability_system.md) 的 `Diagnostic and trace spec`、`EquipmentAbilityContentValidator`、`V1 落地范围` 和 `内容校验测试`。

### 5. P1：creature type tag 的 MOD 扩展 / 校验 schema 没有落点

为什么可能是真问题：

- 文档要求 `creature_type_tag` 可由 MOD 扩展且能报 undeclared tag。
- 当前 validation context 草案没有 `KnownCreatureTypeTags`。
- 生物分类应由 unit / progression taxonomy 控制，而不是由装备能力 pack 反向声明。
- 现有敌人模板只有泛用 `tags`，不能承担这个 taxonomy；它只能作为 battle setup materialize `BattleUnitState.creature_type_tags` 的输入来源之一。

关联证据：

- `docs/design/weapons/equipment_ability_system.md`
- `scripts/enemies/EnemyTemplateDef.cs`

当前文档缺口：

- 没有 unit/progression taxonomy Resource / schema 说明 creature type tag 从哪里声明。
- 没有定义如何合并、如何按 unit taxonomy pack / load order 处理重复和依赖。

子代理建议补充方向：

- 补一个 unit/progression 侧 `CreatureTypeTagContentRules` / taxonomy registry 合同。
- 在 unit taxonomy 内容中声明 tag 列表；`EquipmentAbilityContentPackDef` 不声明 `declared_creature_type_tags`。
- `EquipmentAbilityContentValidationContext` 增加 `KnownCreatureTypeTags`，由 unit taxonomy owner 输出。

处理结论：

- 用户确认生物分类应在 unit 侧控制。主文档已按这个方向修正：runtime fact owner 是 `BattleUnitState.creature_type_tags`，static validation owner 是 unit/progression taxonomy 输出的 `KnownCreatureTypeTags`。
- 装备能力系统不拥有 creature type taxonomy，不在 equipment ability pack 里声明扩展 tag。
- 装备能力 validator 只消费 `EquipmentAbilityContentValidationContext.KnownCreatureTypeTags`；未声明的 `creature_type_tag` literal 仍然是 blocking diagnostic。
- `EnemyTemplateDef.tags`、race/subrace/bloodline/ascension/变身等只允许作为 battle setup/刷新时的输入来源，必须先 materialize 到 `BattleUnitState.creature_type_tags`，运行时 fact provider 禁止回查这些 catalog。

### 6. P2：`project_context_units.md` 尚未把拆分后的装备能力文档纳入读取地图

为什么可能是真问题：

- 仓库规范要求改设计 / 代码前先读 context map。
- 当前 map 没有 `equipment_ability` 入口，相关 CU 仍指向旧武器 / 装备文档。
- 后续实现者容易跳过 ABI、availability、durability 子文档，重复踩已识别的边界问题。

关联证据：

- `docs/design/project_context_units.md`
- `docs/design/weapons/equipment_ability/README.md`

当前文档缺口：

- 没有装备能力专属 CU / read-set。
- 没有推荐读取组合覆盖 Resource ABI、validator、battle projection、skill availability、durability selector。

子代理建议补充方向：

- 把主文档和两个子文档挂进 CU-10 / CU-16 等相关单元。
- 新增“只改装备能力内容 / ABI / validator / 战斗投影”的推荐读取组合。

处理结果（2026-06-30）：

- 已更新 `docs/design/project_context_units.md`。
- CU-10 加入装备能力主文档、README 和 durability selector/commit 子文档。
- CU-13 加入装备能力主文档和 README，覆盖 content ABI / validator 入口。
- CU-15 加入装备能力主文档、README、battle skill availability 迁移和 durability selector/commit 子文档。
- CU-16 加入装备能力主文档、battle skill availability 迁移和 durability selector/commit 子文档。
- 推荐装载组合新增“只改装备能力框架、内容 ABI、validator、战斗投影或装备授予技能”。

## 下一步处理规则

这些意见只是子代理原始检视归档，不自动等于采纳。后续应按以下顺序逐条处理：

1. 结合当前代码和既有文档尝试反驳。
2. 能被现有设计覆盖的，回到本文档标记为“已由现有设计覆盖”。
3. 无法反驳的，移入 `equipment_ability_system.md` 或对应子文档，并补 owner、字段、测试和 V1/V2 边界。
4. 已落地后，在本文档对应条目下补处理结果，避免重复审查。
