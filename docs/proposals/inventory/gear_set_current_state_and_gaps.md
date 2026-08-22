# 装备套装系统现状与缺口设计输入

> 状态：现状审计与方案设计输入，不是当前实现规范。
>
> 核对日期：2026-08-16
>
> 核对基线：`codex/tactical-skills-world-quest` @ `07c3bd37` 的当前工作树；审计时工作树存在其他未提交修改，因此后续实现前必须重新核对当前 checkout。
>
> 当前实现真相文档：[`docs/design/progression/equipment_sets.md`](../../design/progression/equipment_sets.md)。

## 1. 目的

本文向方案设计者自我完备地说明当前装备套装系统、通用装备能力框架、已验证行为和仍存在的问题。它不直接给出实现方案；目标是让后续设计能够在不重新阅读全部源码的情况下，正确选择 owner、内容 schema、运行时接口、验证路径和落地顺序。

本文只描述已核实的当前 checkout。`docs/proposals/` 中的历史方案不代表实现，尤其不要恢复旧 GDScript registry、tag occurrence 计数、弱 `Dictionary` DTO 或 opaque `special_effect_ids`。

## 2. 总体结论

套装核心引擎已经落地，且实现形态优于早期方案：

- 内容经 C# `GearSetDef` / `GearSetThresholdDef` 授权，加载后投影为 immutable typed definition。
- 套装按显式 `member_item_ids` 中的不同有效物品计数，不按 tag occurrence 计数。
- 阈值可叠加；支持任意阈值形状和必需成员。
- 阈值属性保留 `gear_set` / threshold provenance；阈值 trait 保留 `gear_set_threshold` provenance。
- 角色属性、战斗投影、战斗内换装、UI 摘要和凤凰套装回归已经接通。
- 当前正式内容只有 `phoenix_rebirth_set` 一套，尚未达到早期规划的 100 套目标。

主要问题不在“套装计数引擎是否存在”，而在：

1. 套装内容规模严重不足；
2. 旧装备文案中的大量属性没有正式语义 owner；
3. damage tag、光环、传送、伤害转化和 D&D 状态等通用能力仍不完整；
4. 若把系统推广到更多套装，threshold usage anchor、UI 可用性和内容校验还需要更明确的通用合同。

## 3. 明确的设计约束

后续方案必须遵守以下约束：

- 不恢复 `set_tag` 计数；套装成员真相是显式 typed membership。
- 不让重复装备副本重复计数；同一成员 item id 只计一次。
- 不让破损装备计数；`current_durability <= 0` 的实例无效。
- 不让装备声明第二份槽位表；实际占用槽位必须与 item definition 的 canonical footprint 完全一致。
- 不用 opaque `special_effect_ids` 或弱 `conditions: Array[Dictionary]` 表达战斗行为。
- 不实现全局 `spell_dc_bonus`。该字段只是旧内容占位；正式语义应按伤害类型或豁免标签区分豁免 DC 加值。
- 不把 `resistance_* +N` 伪装成百分比、固定 DR 或 mitigation tier；抗性正式路径是 trait `damage_resistance_entries`。
- 不擅自添加 legacy alias、兼容迁移或旧 schema fallback；任何兼容路径必须先得到用户确认。
- preview、正式执行、AI 估值、mutation guard、UI/headless 和测试必须共享同一 typed 语义。

## 4. 当前实现架构

### 4.1 内容模型

Authoring Resource：

- `scripts/player/progression/gear_sets/GearSetDef.cs`
  - `gear_set_id`
  - `display_name`
  - `description`
  - `member_item_ids`
  - `usage_anchor_item_id`
  - `thresholds`
- `scripts/player/progression/gear_sets/GearSetThresholdDef.cs`
  - `threshold_id`
  - `required_piece_count`
  - `display_name`
  - `description`
  - `mandatory_member_item_ids`
  - `attribute_modifiers`
  - `granted_trait_ids`

Resource 在加载期投影为：

- `GearSetDefinition`
- `GearSetThresholdDefinition`

投影代码：`scripts/player/progression/gear_sets/GearSetDefinition.cs:103-160`。

阈值直接属性在投影时被规范为：

```text
source_type = gear_set
source_id   = gear_set::<gear_set_id>::<threshold_id>
```

见 `GearSetDefinition.cs:126-135`。

### 4.2 加载、校验与发布

- Registry：`scripts/player/progression/gear_sets/GearSetContentRegistry.cs`
- 正式目录：`res://data/configs/gear_sets/**/*.tres`
- 构建入口：`scripts/systems/content/ContentSnapshotBuilder.cs:33-65`
- 发布入口：`ContentSnapshot.GearSets` → `GameContentCatalog.GetGearSetDefinitionsTyped()`

当前 registry 已校验：

- 空或重复 set/threshold id；
- 成员必须存在且是非堆叠装备；
- usage anchor 必须是成员；
- 阈值件数必须为正且严格递增；
- 阈值不能超过成员总数；
- mandatory members 必须是成员且不重复；
- 直接阈值属性和授予 trait 不得重复声明同一 `attribute_id`；
- threshold trait 必须允许 `gear_set_threshold` source；
- 非满套阈值授予 per-world-day action 时，usage anchor 必须 mandatory。

主要校验实现见 `GearSetContentRegistry.cs:158-401`。

### 4.3 套装计数与阈值计算

唯一计算 owner 是：

- `scripts/systems/inventory/GearSetEvaluationService.cs`

当前语义：

- 遍历 `EquipmentState.GetEntrySlotIdsTyped()`；
- 每个 entry 只计算一次；
- 只接受未破损、存在 item definition、允许当前 entry slot 且 footprint 完全匹配的装备；
- 按 `member_item_ids` 查找不同有效 item id；
- 同一 item id 的重复副本不重复计数；
- `contributing count >= required_piece_count` 且 mandatory members 全部存在时阈值激活；
- 高阈值激活时，低阈值继续激活；
- 结果包含 active set summary、direct attributes 和 derived trait instances。

关键实现见 `GearSetEvaluationService.cs:133-247` 和 `:249-324`。

### 4.4 角色与战斗集成

角色侧：

- `CharacterManagementModule.EvaluateGearSets(...)` 对外提供 typed evaluation。
- `CharacterManagementModule.build_attribute_source_context(...)` 将单件属性和套装阈值属性一起放入 `context.equipment_state`。
- `CharacterTraitService.BuildEffectiveTraits(...)` 将 threshold trait 加入有效 trait 集，source kind 为 `GearSetThreshold`。

关键代码：

- `scripts/systems/progression/CharacterManagementModule.cs:380-392`
- `scripts/systems/progression/CharacterManagementModule.cs:563-587`
- `scripts/systems/progression/CharacterTraitService.cs:58-65`
- `scripts/systems/progression/CharacterTraitService.cs:179-218`

战斗侧：

- threshold trait 可投影为 `EquipmentAbilitySourceKind.PlayerPersistentGearSetThreshold`。
- `BattleUnitFactory.RefreshEquipmentProjection(...)` 在战斗内换装后重算属性、有效 trait、装备能力来源、武器投影和资源钳制。

关键代码：

- `scripts/systems/battle/core/BattleEquipmentAbilitySourceState.cs:13-22`
- `scripts/systems/battle/runtime/BattleEquipmentAbilityProjectionService.cs:87-135`
- `scripts/systems/battle/runtime/BattleUnitFactory.cs:394-460`

### 4.5 UI 与展示

当前 UI 显示套装名、当前件数/总件数、每个阈值的激活状态和描述：

- 共享投影：`scripts/systems/game_runtime/GameRuntimeCharacterInfoBuilder.cs:300-338`
- 战外队伍装备页：`scripts/ui/PartyManagementWindow.cs:709-743`
- 战斗人物信息复用 battle-local equipment view。

当前套装摘要还不承担完整的“阈值授予动作可用性、剩余次数和 disabled reason”展示合同；这些能力目前主要经技能入口或装备能力 UI 间接呈现。

## 5. 当前正式内容

唯一正式套装：

- `data/configs/gear_sets/phoenix_rebirth_set.tres`

内容：

- set id：`phoenix_rebirth_set`
- 10 个成员；
- 阈值：3/5/7/10；
- usage anchor：`armor_phoenix_rebirth_head`。

阈值摘要：

| 件数 | 当前效果 |
|---:|---|
| 3 | freeze half，constitution save +2 |
| 5 | `hp_max +20` |
| 7 | fire immune；低血后治疗并进入余烬形态，附加移动和 fire 伤害 |
| 10 | 每日一次的太阳涅槃主动效果，含治疗、范围 fire、友方治疗和金焰形态 |

完整当前行为见 `docs/design/progression/equipment_sets.md:46-99`。

凤凰套装还验证了世界唯一实例池、商店/掉落转移和 10 个单件能力，但这些是凤凰内容链，不代表其余 99 套已经落地。

## 6. 已验证基线

以下命令在本次审计中通过：

```powershell
dotnet build magic.csproj
godot --headless -s res://tests/equipment/run_gear_set_evaluation_regression.cs
godot --headless -s res://tests/battle_runtime/runtime/run_phoenix_rebirth_set_regression.cs
godot --headless -s res://tests/runtime/validation/run_resource_validation_regression.cs
```

`run_resource_validation_regression.cs` 内部会故意加载 invalid fixtures 并列出预期错误，runner 最终结果为 PASS。

## 7. 重点问题清单

### 7.1 P0 — 套装内容规模不足

当前正式 gear-set 资源只有 1 套：

- `data/configs/gear_sets/phoenix_rebirth_set.tres`

早期目标是 100 套。`dawn_paladin_set` 不存在；龙鳞套装也没有正式 gear-set 资源或四件 `armor_dragon_scale_*` 物品。`dragon_scale_set_full_landing.md` 仍是 proposal。

影响：

- 当前引擎只被一个高度定制的大型套装验证；
- 普通 2/4 件套、非战斗主动、被动光环、条件抗性、召唤、传送、伤害转化等内容路径没有规模化验证；
- 如果直接批量生成 99 套，很容易把旧文案中的无效属性和语义冲突批量带进正式内容。

需要先解决的问题不是“再写 99 个 `.tres`”，而是先冻结通用语义和校验规则。

### 7.2 P0 — 旧属性 ID 没有统一语义 owner

`AttributeService` 的核心识别集合是封闭的，见 `scripts/systems/attributes/AttributeService.cs:5-33`。未知/custom attribute 仍可作为 additional attribute 进入 snapshot，见 `AttributeService.cs:296-308` 和 `:921-960`；装备能力也可通过 `attribute_value` fact 读取它们。

这造成一个危险中间态：旧内容可以“看起来有数值”，但核心战斗并不消费它。

当前主要类别：

| 旧内容写法 | 当前状态 | 设计问题 |
|---|---|---|
| `resistance_* +N` | 无核心消费者 | 不应实现为属性处理器；正式路径是 damage-tag mitigation tier |
| `saving_throw_*` | 无统一豁免消费者 | 应明确按 save tag、damage tag 或 ability 归属 |
| `spell_dc_bonus` | 无消费者；且已确认不应成为全局法术 DC | 旧内容占位，应归一为具体伤害类型/豁免标签的 DC 加值 |
| `critical_threat_range` | 无数值型消费者 | 当前 crit threshold 只由幸运公式得出；装备只有 `critical_hit_override` 这类强制暴击 |
| `movement_speed`、`stealth_bonus`、技能 bonus 等 | 无正式 owner | 需要先定义属性语义和战斗/世界查询入口 |

特别注意：`docs/content/equipment_sets/existing_set_attribute_optimization.md:126` 仍写着将 `spell_dc / spell_dc_bonus` 映射为全局 `spell_save_dc(+N)`。这与最新决策冲突，后续内容归一化时必须废弃这条指导，改为具体伤害类型或豁免标签。

### 7.3 P0 — damage tag taxonomy 不完整

当前合法 damage tag 由 `scripts/player/progression/DamageTagContentRules.cs:48-70` 唯一拥有。

已覆盖早期缺口中的：

- `acid`
- `force`
- `poison`
- `cold` 的正式映射 `freeze`

仍缺：

- `necrotic`：当前只有语义近似的 `negative_energy`，没有 `necrotic` 别名；
- `silver`：完全没有对应 tag。

设计必须先决定这是内容归一化问题还是 taxonomy 扩展问题：

- `necrotic` 是否正式映射为 `negative_energy`；
- `silver` 是 damage tag、material tag，还是只对特定 creature type 生效的条件规则。

不要在运行时增加字符串 fallback 或双写映射。

### 7.4 P1 — 通用增益光环缺失

已有能力：

- mitigation aura typed definition 已存在，见 `EquipmentMitigationAuraDefinition`（`EquipmentAbilityRuntimeDefinitions.cs:266-274`）；
- 凤凰徽章已经使用 fire mitigation aura；
- 有对应运行时回归。

仍缺：

- 属性增益光环；
- 攻击/豁免/移动等功能性增益光环；
- 光环进入 preview、AI 估值、HUD 来源说明的统一合同。

目前只能用 reaction + `apply_status` 近似，容易产生来源清理、重复叠加、范围刷新和 AI 估值不一致问题。

### 7.5 P1 — 装备原生传送/瞬移 action 缺失

技能侧已有 blink forced-move 管线，但 equipment ability 没有原生 teleport/blink action。当前只能：

- 授予一个 blink 技能；或
- 触发一个内部技能。

如果大量装备只需要“按装备规则瞬移”，强制经过技能会造成可用性、费用、冷却、preview 和 AI affordance 的语义噪音。

需要设计一个 equipment action 是否能直接复用技能侧 blink 规则，以及目标选择、费用、触发时机和 preview/AI 的边界。

### 7.6 P1 — 伤害吸收/转化不完整

已有：

- mitigation tier：immune/half/double；
- 护盾吸收；
- fixed damage reduction；
- mitigation aura；
- fatal intercept。

仍缺：

- 将实际伤害转化为治疗或其他资源；
- 装备驱动的吸收池；
- 多来源吸收/转化的排序、上限、元素过滤和 provenance；
- preview/AI 对转化期望值的统一表达。

旧文案中的“受到元素伤害转化为治疗”不能只用 fatal intercept 或固定减伤近似。

### 7.7 P1 — D&D 状态语义映射不完整

当前 `BattleStatusSemanticTable` 已覆盖部分状态：

- `blind`
- `poisoned`
- `frightened`
- `stunned`
- `paralyzed`
- `prone`
- `petrified`

仍缺常见 D&D 状态：

- `charmed`
- `restrained`
- `grappled`
- `invisible`
- `deafened`

问题不是简单增加状态字符串。每个状态都必须定义：

- 对命中、防御、移动、行动、目标选择或感知的影响；
- 与现有 hard-control / cognition / status stack 规则的关系；
- 驱散、免疫、save tag 和 UI 显示；
- preview 与 AI 估值。

### 7.8 P2 — threshold usage anchor 的规模化语义需要复核

当前 threshold ability 必须绑定到一个真实装备实例：

- 优先 `usage_anchor_item_id`；
- anchor 无效时使用套装成员顺序中的第一个有效成员。

见 `GearSetEvaluationService.cs:187-229`。

这对凤凰套装已经可用，但推广到更多套装时存在设计问题：

- 非满套阈值授予 per-world-day action 时，registry 要求 anchor mandatory；
- anchor 破损或卸下后，能力来源会切换；
- usage 账本跟随物理装备实例，而不是抽象套装阈值；
- 套装转交另一角色时，次数语义需要明确是“装备实例账本”还是“穿戴者账本”。

后续方案需要确认这是可接受的长期合同，还是应该引入角色/装备视图上的 threshold usage owner。该决策会影响 save schema、战斗 writeback 和兼容策略，不能隐式变更。

### 7.9 P2 — UI/headless 对阈值动作可见性不足

当前套装摘要只稳定展示：

- 套装名；
- 当前件数/总件数；
- 阈值是否激活；
- 阈值描述。

尚无统一的套装级 granted-action summary：

- remaining uses；
- disabled reason；
- AP/资源费用；
- 当前为何不可用；
- battle-local 与 world snapshot 的一致性。

单个装备技能可能已在技能列表中可见，但这不能替代套装摘要中的阈值动作状态。

### 7.10 P3 — 特定计数回归仍可补强

当前计数规则已经有较完整测试，但早期计划中的“双手武器只计 1 件”没有专门的双手套装成员回归。

现有架构通过单 entry + canonical footprint 已经表达该语义；建议后续新增一个真实双手套装成员 fixture，防止 footprint 规则变化时静默回归。

## 8. 已经解决、不要重新设计的能力

以下能力已由通用 equipment-ability framework 或 battle runtime 落地，后续方案应复用而不是新建平行系统：

| 能力 | 当前机制 |
|---|---|
| on-hit / on-kill | `EquipmentAbilityTriggerKind.OnHit` / `OnKill` |
| on-crit | hit/damage trigger + `critical_hit` fact |
| 受击/低血反应 | `OnDamageTakenFinalized`、`OnHitReceived`、`hp_percent_bp` |
| 周期次数 | `per_battle`、`per_world_day`、`per_world_month` |
| 武器元素附伤 | typed `add_damage_dice` + damage tags + replacement group |
| 吸血 | `heal_from_fact` 按 `hp_damage` 回血 |
| 召唤 | `summon_units`，含 AI、生命周期和数量上限 |
| 致死拦截 | typed fatal intercept |
| 减伤光环 | mitigation aura |
| 固定减伤 | `damage_reduction` action |

这些机制的正式定义集中在 `scripts/player/progression/equipment_abilities/EquipmentAbilityRuntimeDefinitions.cs`；运行时说明见 `docs/design/battle/equipment_ability_runtime.md`。

## 9. 需要方案设计回答的问题

### 9.1 伤害类型/豁免标签 DC 加值

需要定义：

- authoring schema：挂在 trait、equipment binding action，还是独立 typed definition；
- 作用域：damage tag、save tag、delivery category，还是三者组合；
- 进攻方加成与防守方 save bonus 的叠加顺序；
- 多来源叠加规则：add、highest、replacement group；
- preview、AI、伤害报告和来源显示；
- 旧 `spell_dc_bonus` 内容如何归一化。

不得输出“新增全局 `spell_dc_bonus` 属性处理器”的方案。

### 9.2 旧属性归一化

需要给每类旧字段一个明确归宿：

- 删除；
- 改为正式 typed 字段；
- 改为 trait passive；
- 改为 equipment ability condition/action；
- 新增真实 runtime owner。

输出应包含字段级映射表，而不是只给原则。

### 9.3 damage tag 扩展或映射

需要决定：

- `necrotic` → `negative_energy` 还是新增 tag；
- `silver` 的正式语义；
- 如果新增 tag，需要同步哪些 validator、UI、抗性、装备能力和测试。

### 9.4 通用光环 ABI

需要定义：

- aura 是持续 projection 还是 reaction；
- 支持哪些 modifier 类型；
- 进出范围的刷新时机；
- source death、换装、破损、stacks 和同类光环互斥；
- preview/AI/HUD 可见性。

### 9.5 装备传送 action

需要定义：

- 是否新增原生 equipment action；
- 如何复用 blink forced-move 规则；
- 费用由 action、granted skill 还是 usage period 承担；
- 手动目标选择、AI 候选和 preview 如何接入。

### 9.6 伤害转化

需要定义：

- 转化发生在 mitigation、save、shield、HP commit 的哪一步；
- 转化上限、元素过滤、来源过滤；
- 多来源排序和防递归；
- 报告、preview、AI 如何显示“吸收量”和“转化量”。

### 9.7 状态扩展

需要逐状态定义战斗语义，不接受只增加状态名或别名。

### 9.8 下一批内容切片

需要选择一个或少数几个套装作为下一批落地切片，用它们反向验证上述 ABI。候选切片应覆盖：

- 静态属性；
- damage-tag save/DC；
- 光环；
- 传送；
- 伤害转化；
- 状态免疫或控制。

龙鳞套装可以仍是候选，但当前 proposal 未实现且需要按最新约束重新核对。

## 10. 方案验收标准

任何后续方案至少要给出：

1. typed authoring schema 和 immutable definition 投影；
2. 内容 validator 的 fail-closed 规则；
3. runtime owner 与调用顺序；
4. preview、正式执行、AI 的共享语义；
5. save/writeback 影响及是否需要 schema version；
6. UI/headless snapshot 的可见性；
7. focused regression 列表；
8. 旧内容归一化策略；
9. 明确的非目标，避免重建已废弃的 GDScript/tag/special-effect 方案。

如果方案涉及旧存档或旧内容兼容，必须单独列出并等待用户批准，不能默认加入 migration/fallback。

## 11. 推荐源码阅读顺序

后续设计者可按以下顺序加载上下文：

1. `docs/design/progression/equipment_sets.md`
2. `scripts/player/progression/gear_sets/GearSetDef.cs`
3. `scripts/player/progression/gear_sets/GearSetThresholdDef.cs`
4. `scripts/player/progression/gear_sets/GearSetDefinition.cs`
5. `scripts/player/progression/gear_sets/GearSetContentRegistry.cs`
6. `scripts/systems/inventory/GearSetEvaluationService.cs`
7. `scripts/systems/progression/CharacterManagementModule.cs`
8. `scripts/systems/progression/CharacterTraitService.cs`
9. `scripts/systems/battle/runtime/BattleUnitFactory.cs`
10. `scripts/systems/battle/runtime/BattleEquipmentAbilityProjectionService.cs`
11. `scripts/player/progression/equipment_abilities/EquipmentAbilityRuntimeDefinitions.cs`
12. `docs/design/battle/equipment_ability_runtime.md`
13. `tests/equipment/run_gear_set_evaluation_regression.cs`
14. `tests/battle_runtime/runtime/run_phoenix_rebirth_set_regression.cs`

## 12. 相关但已过时的文档

- `docs/proposals/inventory/gear_set_system.md`：历史 GDScript 方案；第 4–6 节已补当前状态，但第 1–3 节不可按字面执行。
- `docs/proposals/inventory/dragon_scale_set_full_landing.md`：龙鳞套装 proposal，尚未实现。
- `docs/content/equipment_sets/existing_set_attribute_optimization.md`：其中全局 `spell_save_dc` 映射与最新决策冲突，后续需修订。
