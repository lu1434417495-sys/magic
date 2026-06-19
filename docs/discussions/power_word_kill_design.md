# 律令死亡（Power Word, Kill）代码级实现方案 v4.8

> 文档性质：三子代理架构对抗讨论后、补齐剩余架构意见的共识实现任务包。
> 最后更新：2026-06-19
> 取代范围：全文取代 v2.x、v3.x、v4.0 及"v3.5 代码落地"段落中的不一致结论。
> 硬约束：不做兼容处理、不加旧字段别名、不保留错误中间实现、不把特殊逻辑塞进 UI / `GameRuntimeFacade` / `WorldMapSystem`。`GameRuntimeFacade` / `BattleSessionFacade` 只允许新增 battle runtime 纯透传 API。
> 当前工程已迁移为 Godot 4.6 C#；本文中的旧 GDScript 片段只保留为协议形状说明，落地时必须以现有 `.cs` resource / rules / test owner 为准。

## 1. 共识结论

`mage_power_word_kill` 使用一个受限的 `effect_type = &"execute"`，但本文不把它定义成完全通用处决系统。首版口径是：

> `execute v1 / PWK 执行协议`

未来如果要让其他技能复用不同属性、不同伤害类型、不同附加状态或不同死亡保护策略，必须重新扩展 schema，不允许靠参数别名、旧字段或宽松 fallback 混进去。

三代理共识：

| 议题 | 结论 |
|---|---|
| special profile | 不使用 Meteor Swarm special profile；PWK 是单体 unit effect。 |
| `execute` 口径 | v1 是 PWK 执行协议，不宣称通用即死系统。 |
| 常量归属 | 新增纯 content rules 文件集中保存 execute 专属常量；save tag / status id 等跨系统常量由各自子系统作为 canonical source。 |
| 资源参数 | 正式 `.tres` 不暴露 `burst_damage`、`finisher_damage`、`apply_status_id`、`death_protection_policy`。 |
| 低血门槛 | 只有 `current_hp <= threshold` 的有效目标允许进入 execute 判定；高血目标不可释放且不消耗成本。 |
| 致死链 | 豁免失败直接造成 fatal execute，使用 `death_source_priority` 分级死亡权威；低级别 / 权威不足的免死会被跳过，高级别死亡保护仍可拦截。正式资源不能配置 blanket bypass。 |
| 目标判定 | PWK 不区分普通 / Elite / Boss；所有敌方单位使用同一血线、同一豁免、同一成功/失败规则。 |
| Boss / rank | `target_rank` 仍服务于 encounter projection、Fate / Fortuna 等系统；PWK 不读取 `boss_target` 或 `fortune_mark_target` 决定分支。 |
| AI / runtime | 共用纯 execute plan；runtime 负责 mutation，AI 负责概率估值。 |
| `soul_fracture` | 状态倍率读取不放在 `BattleExecutionRules`，改由状态 modifier helper 负责。 |
| `target_rank` | 先作为 `EnemyTemplateDef` 必填字段；未来同模板多 rank 时升级 roster schema。 |

### 1.1 2026-06-19 复核确认结论

以下结论优先于本文早期段落和旧 GDScript 示例；若后文仍有表达冲突，以本节为准并在实现时同步清理。

| 议题 | 已确认结论 |
|---|---|
| 兼容策略 | 不保留旧 staged execute，不做旧字段别名或 payload/schema fallback。 |
| 阈值公式 | PWK 的 HP 阈值必须读取施法者该技能等级；目标 rank / Boss 标记不影响阈值。 |
| 属性修正 | 施法者属性修正不进入 HP 阈值；属性只通过 `save_dc_mode = "caster_spell"` 影响豁免难度。 |
| `threshold_ability_mod` | 该字段及 multiplier 对 PWK 语义错误，应从正式 PWK 协议中移除 / 拒绝。 |
| 豁免配置 | 正式 PWK 使用 `save_dc_mode = "caster_spell"`、`save_dc = 0`、`save_dc_source_ability = "intelligence"`、`save_ability = "willpower"`、`save_tag = "execute"`。 |
| 高血目标 | `current_hp > threshold` 的目标必须在 preview / command issue 阶段拒绝，且不能消耗 AP / MP / cooldown；运行时只保留 developer safety gate。 |
| 目标等级 | PWK 不读取 `target_rank`、`boss_target` 或 `fortune_mark_target`；Boss 与普通目标使用同一血线、同一豁免、同一死亡规则。 |
| 敌人 rank schema | `EnemyTemplateDef.target_rank` 默认应为空；正式 enemy template 必须显式填写合法 rank，缺字段 / 空值应报 schema 错。 |
| 死亡保护 | PWK 不应 `BypassDeathPrevention=true` 跳过所有免死，也不是普通标准链；它应携带高死亡权威，例如 `DeathSourcePriority=900`，跳过权威不足的免死，高级别保护可生效。 |
| 护盾 | fatal execute 穿透护盾但不改护盾字段；`bypass_shield=true` 时不得 normalize、drain、clear 或 refresh shield state。 |
| 伤害数值 | fatal execute 伤害取目标当前 HP 快照，不报告 9999 或理论过量值；damage / hp_damage 应为实际 HP loss。 |
| `soul_fracture` | 低血 execute 分支结算后，只要目标仍 alive 且 `current_hp > 0` 就施加弱化 `soul_fracture`；高血非法分支和已死亡目标不施加。 |
| 状态语义 | `soul_fracture` 应注册明确语义：有害、可清除、可驱散、refresh、max stack 1、无 tick、显示名“灵魂裂解”。 |
| 预览 / HUD | 玩家应看到低血合法目标的命中率和豁免分支结果；命中率是玩家展示字段，不等同于 AI 内部 `kill_probability_basis_points`。 |
| AI | AI 使用共享 execute plan 和 save probability；非法高血目标不可进入可执行候选；kill value 使用 `kill_probability_basis_points`，并考虑死亡保护降权和 `soul_fracture_applied`。 |
| 资源形态 | 正式资源是 C# `.tres` 资源，不能使用旧 GDScript script path；运行时不写 `skill_id == "mage_power_word_kill"` 分支，只按 `effect_type = "execute"` 协议分发。 |
| Schema | execute v1 必须是单体敌方 unit effect；不能混入 sibling effect、passive execute、special profile execute、ground execute 或通过 skill-level gate 隐藏 sibling。 |
| Ground path | ground preview / validation / issue 都必须在成本消耗前拒绝 execute。 |

## 2. 玩家语义

`mage_power_word_kill`：

- 单体敌方法术，射程 12。
- 消耗 1 AP / 2000 MP，冷却 600 TU。
- 对所有有效敌方单位（包括 Boss）先按同一阈值判断血线；阈值由目标最大生命比例加技能等级增量构成，并受最大生命比例上限约束。施法者属性不影响血线阈值。
- 目标 `current_hp > threshold` 时不是合法释放目标；preview / command issue 必须在 AP、MP、冷却消耗前拒绝，AI 不考虑该目标。
- 目标 `current_hp <= threshold` 时进入 execute 分支，进行一次 `execute` 意志豁免。
- 豁免失败且没有 `execute_immunity` 时，直接造成 fatal execute 伤害，令目标进入分级死亡保护链。
- fatal execute 携带高死亡权威：低级别 / 权威不足的免死保护会被跳过，高级别保护仍可拦截。
- 豁免成功或拥有 `execute_immunity` 时不造成 HP 伤害。
- execute 结算后目标仍有 `current_hp > 0` 时施加弱化 `soul_fracture`：持续 60 TU，治疗倍率 50%，护盾获得倍率 50%。

非目标：

- 不实现通用即死系统、Saving Throw 大改、暴击倍率重构或 target-aware UI 数值预览。
- 不新增 `special_resolution_profile_id`。
- 不复用 `save_tag = &"magic"`。
- 不新增 `damage_tag = &"true"` 或 `&"execute"`。
- 不保留 `shield_absorption_percent`、`bypass_mitigation`、`true_damage`。
- 不让正式内容配置 blanket death-prevention bypass；死亡保护是否生效由死亡源权威与保护等级比较决定。

## 3. 当前偏差必须替换

当前仓库已有一组未收束的 `execute` 痕迹。实现时按下表替换，不能作为兼容路径保留。

| 当前偏差 | 共识处理 |
|---|---|
| `EnemyTemplateDef.target_rank` 默认 `&"normal"` | 改为默认 `&""`，正式资源必须显式填写。 |
| 只有 `wolf_alpha.tres` 显式写 rank，其余 normal 靠默认 | 所有正式 enemy template 都显式写 `target_rank`。 |
| `execute` 使用 `save_tag = &"magic"` | 新增并使用 `SAVE_TAG_EXECUTE = &"execute"`。 |
| `execute` 使用 `shield_absorption_percent` | 删除该语义；PWK 使用 `bypass_shield = true`，完全不改护盾状态。 |
| `_coerce_damage_outcome()` 补 `bypass_mitigation / true_damage / shield_absorption_percent` | 移除这些字段。 |
| `BattleExecutionRules.resolve_threshold()` 读取属性修正 | 删除属性阈值修正；保留技能等级对阈值的影响，属性只进入 caster spell save DC。 |
| Boss 非致命默认 `12% + floor 25` | 删除 Boss 专属非致命分支；高血 Boss 与所有目标一样不是合法释放目标，低血 Boss 与所有目标一样走 execute save，失败进入分级死亡保护链，成功无 HP 伤害。 |
| AI 用 `burst_damage = 9999` 估值 | 改为读取共享 execute plan 和 save probability。 |
| 测试使用 `save_dc_mode = &"fixed"` | 改为现有合法值 `&"static"` 或 `&"caster_spell"`；正式 PWK 用 `&"caster_spell"`。 |

## 4. 文件级方案

### 4.1 Execute Content Rules

当前工程不新增 GDScript `battle_execute_content_rules.gd`。执行协议常量必须收束到 C# owner：

- `effect_type = "execute"` 的 canonical source 是 `BattleTypedEnums` / `BattleEffectKind.Execute`。
- `save_tag = "execute"` 的 canonical source 是 `BattleSaveContentRules` / `BattleSaveTagKind.Execute`。
- `STATUS_SOUL_FRACTURE` 的 canonical source 是 `BattleStatusSemanticTable`。
- PWK 专属 schema 字段集合由 C# `CombatEffectDef` typed fields 和 `SkillContentRegistry` execute validation 共同约束。
- 如果实现需要 execute 专属字段名 helper，应新增 C# helper，例如 `BattleExecuteContentRules.cs`，且只放 PWK execute 协议字段名 / validation 辅助，不反向成为 save tag、status id 或通用 damage tag 权威源。

Rules:

- `SkillContentRegistry`、`BattleExecutionRules`、`BattleDamageResolver`、`BattleAiScoreService` 必须通过 C# canonical source 或 execute helper 读取 execute 协议字段，不散落 ad hoc string。
- `BattleDamageResolver` 构造 `soul_fracture` 时直接引用 `BattleStatusSemanticTable.STATUS_SOUL_FRACTURE`。
- `BattleSaveResolver` 通过 `BattleSaveContentRules` 识别 execute save tag，不同步另一套 mirror 常量。
- 不把 execute 常量散落在多个文件里硬编码。

### 4.2 Save Tag

`scripts/player/progression/BattleSaveContentRules.cs` owns `BattleSaveTagKind.Execute` and the `"execute"` string. If a local branch lacks it, add it there and make `BattleSaveResolver` consume that canonical value.

现有 `_collect_save_tag_state()` 已能识别：

- `&"execute"`
- `&"execute_advantage"`
- `&"execute_disadvantage"`
- `&"execute_immunity"`

回归必须证明：

- `magic_advantage` 不影响 `execute`。
- 对任意低血目标（包括 Boss），`execute_immunity` 按豁免成功处理：不造成 HP 伤害，但可施加弱化 `soul_fracture`。
- 对任意高血目标（包括 Boss），不会解析 `execute` 豁免；目标门禁在成本消耗前拒绝。

### 4.3 Adjacent Cleanup: Enemy Target Rank Projection

This section is not PWK skill logic. It is included because PWK implementation and tests touch enemy rank fixtures, and the design must lock the boundary: enemy rank may serve Fate / Fortuna and other rank-aware systems, but PWK must not read rank-derived attributes for branch selection.

修改 `scripts/enemies/enemy_template_def.gd`：

```gdscript
const TARGET_RANK_NORMAL: StringName = &"normal"
const TARGET_RANK_ELITE: StringName = &"elite"
const TARGET_RANK_BOSS: StringName = &"boss"
const VALID_TARGET_RANKS := {
	TARGET_RANK_NORMAL: true,
	TARGET_RANK_ELITE: true,
	TARGET_RANK_BOSS: true,
}

@export var target_rank: StringName = &""
```

Editor constraint:

- `target_rank` 必须通过 `_validate_property()` 在 Inspector 侧提供 `PROPERTY_HINT_ENUM`，hint string 固定为 `"normal,elite,boss"`。
- Inspector 约束只是编辑器提示；运行时仍以 `validate_schema()` 的硬校验作为最终防线。

`validate_schema()` 必须硬校验：

- `target_rank` 非空。
- `target_rank` 只能是 `normal / elite / boss`。
- `attribute_overrides` 不允许出现 `boss_target` 或 `fortune_mark_target`，无论 key 是 `String` 还是 `StringName`。

正式模板一次性补齐：

| 模板 | target_rank |
|---|---|
| `mist_beast.tres` | `normal` |
| `mist_harrier.tres` | `normal` |
| `mist_weaver.tres` | `normal` |
| `wolf_alpha.tres` | `elite` |
| `wolf_pack.tres` | `normal` |
| `wolf_raider.tres` | `normal` |
| `wolf_shaman.tres` | `normal` |
| `wolf_vanguard.tres` | `normal` |

修改 `scripts/systems/world/encounter_roster_builder.gd`：

```gdscript
func _apply_enemy_target_rank(snapshot, target_rank: StringName) -> void:
	if snapshot == null:
		return
	snapshot.set_value(&"boss_target", 0)
	snapshot.set_value(&"fortune_mark_target", 0)
	match ProgressionDataUtils.to_string_name(target_rank):
		EnemyTemplateDef.TARGET_RANK_BOSS:
			snapshot.set_value(&"boss_target", 1)
			snapshot.set_value(&"fortune_mark_target", 2)
		EnemyTemplateDef.TARGET_RANK_ELITE:
			snapshot.set_value(&"fortune_mark_target", 1)
		EnemyTemplateDef.TARGET_RANK_NORMAL:
			pass
		_:
			push_error("Invalid enemy target_rank reached runtime: %s" % String(target_rank))
```

Important boundary:

- `fortune_mark_target` 仍可供 Fate / Fortuna 系统使用。
- PWK 不读取 `target_rank`、`boss_target` 或 `fortune_mark_target` 作为分支条件。
- 正式资源的非法 rank 必须由 `EnemyTemplateDef.validate_schema()` 在内容加载阶段失败；`_apply_enemy_target_rank()` 的 `_` 分支只是 runtime 防御，不是正常路径。
- `_apply_enemy_target_rank()` 必须先把 `boss_target` / `fortune_mark_target` 清到 0，再按合法 rank 写入；非法 rank 路径只 `push_error`，不能留下复用 snapshot 的旧值。
- 不新增 `BattleUnitState.is_boss_target` 作为 v4.8 真相源；避免与 `attribute_snapshot.boss_target` 形成双源。若未来其他系统 profiling 证明 snapshot lookup 是热点，再以派生缓存形式评估。
- 未来如果同一个模板需要在不同 roster 中有不同 rank，必须升级 roster schema 增加 rank override；不要复制隐藏字段，不要靠 `attribute_overrides` 覆盖。

### 4.4 Execute Schema

修改 `scripts/player/progression/skill_content_registry.gd`：

- Effect type validation must recognize `BattleEffectKind.Execute` / `BattleTypedEnums` execute mapping.
- `_append_effect_validation_errors()` 中为 execute 调用 `_append_execute_effect_validation_errors()`。
- `_append_combat_profile_validation_errors()` 增加 profile 级门禁：只要 profile 或任一 cast variant 包含 execute，该技能必须满足单体协议。
- `special_resolution_profile_id != &""` 的技能不得携带 executable execute effect。
- 每个可执行 effect set 中只能有 1 个 execute effect，且不能混入其他 unit / ground / passive / triggered effect；`soul_fracture` 由 runtime clean builder 生成，不作为 `.tres` sibling effect 配置。
- Schema validation must inspect the same raw executable effect set shape that runtime merges before target resolution:
  - no cast variant: `combat_profile.effect_defs`
  - with cast variant: `combat_profile.effect_defs + cast_variant.effect_defs`
- If any execute effect is present in that merged set, the merged set must contain exactly one non-null effect, and that effect must be execute.
- Do not allow `min_skill_level` / `max_skill_level` gates to hide sibling effects for execute v1.
- `SkillContentRegistry` must not preload `BattleSkillResolutionRules`; implement a local schema helper with the same merge order and target-mode fallback semantics.

Execute 单体协议：

| 字段 | 必须值 |
|---|---|
| `combat_profile.target_mode` | `&"unit"` |
| `combat_profile.target_team_filter` | 必须是 `&"enemy"` |
| `combat_profile.target_selection_mode` | `&"single_unit"` |
| `combat_profile.min_target_count` | `1` |
| `combat_profile.max_target_count` | `1` |
| `combat_profile.allow_repeat_target` | `false` |
| `combat_profile.area_pattern` | `&"single"` |
| `combat_profile.area_value` | `0` |

Cast variant 中出现 execute 时：

- Schema uses a local helper equivalent to `BattleSkillResolutionRules.get_cast_variant_target_mode()`: `cast_variant.target_mode` when non-empty, otherwise `combat_profile.target_mode`. The resolved mode must be `&"unit"` for any merged set containing execute.
- `CombatCastVariantDef.required_coord_count` 不参与 execute 目标数量判定。
- 不检查 cast variant 上不存在的 `target_team_filter / target_selection_mode / min_target_count / max_target_count` 字段。
- 不允许 ground variant 或 multi-unit owning profile 携带 execute。

Execute effect 校验：

- `effect_def.effect_target_team_filter` 必须是 `&"enemy"`。
- `&"hostile"` 是 battle unit faction id，不是 target team filter；PWK schema must reject it instead of treating it as an alias.
- `effect_def.save_dc_mode == BattleSaveContentRules.SAVE_DC_MODE_CASTER_SPELL`。
- `effect_def.save_dc == 0`。
- `effect_def.save_dc_source_ability == UnitBaseAttributes.INTELLIGENCE`。
- `effect_def.save_ability == UnitBaseAttributes.WILLPOWER`。
- `effect_def.save_tag == BattleSaveContentRules` execute save tag。
- `effect_def.damage_tag == "negative_energy"` unless a C# damage-tag rules owner is added first.
- `effect_def.save_partial_on_success == false`。
- `trigger_event == &""`，`trigger_condition == &""`。
- `passive_effect_defs` 不允许出现 execute。

PWK execute typed field set:

- `threshold_max_hp_ratio_percent: int`
- `threshold_level_anchor: int`
- `threshold_level_bonus_per_delta: int`
- `threshold_cap_max_hp_ratio_percent: int`
- `soul_fracture_duration_tu: int`
- `heal_multiplier_percent: int`
- `shield_gain_multiplier_percent: int`

Strict validation:

- 必须逐项检查 `typeof(value) == TYPE_INT`；不接受字符串、浮点、布尔转换。
- 不允许缺字段。
- 不允许额外字段。
- 所有 ratio / multiplier 必须在 `0..100`；`threshold_cap_max_hp_ratio_percent` 必须大于等于 `threshold_max_hp_ratio_percent`。
- `threshold_level_anchor`、`threshold_level_bonus_per_delta` 必须为非负整数。
- `soul_fracture_duration_tu` 必须 `> 0` 且能被 5 整除。

Explicitly rejected params:

- `staged_execution`
- `burst_damage`
- `finisher_damage`
- `death_protection_policy`
- `apply_status_id`
- `damage_tag`
- `shield_absorption_percent`
- `threshold_ability_mod`
- `threshold_ability_mod_multiplier`
- `soul_fracture_status`
- `soul_fracture_status_id`
- `non_lethal_damage_ratio_percent`
- `boss_save_success_non_lethal_damage_ratio_percent`
- `boss_non_lethal_damage_ratio_percent`
- `boss_non_lethal_damage_max_hp_ratio_percent`

Runtime safety gate:

- `BattleSkillResolutionRules.is_unit_effect()` 可以把 execute 视为 unit effect。
- `BattleSkillResolutionRules.collect_ground_unit_effect_defs()` 不应静默执行 execute。
- `BattleSkillExecutionOrchestrator._handle_ground_skill_command()` 必须在 cost 消耗前检查 ground effect defs；若发现 execute，记录错误日志并返回 `false`。
- 不允许用"跳过 execute effect"当兼容处理；非法资源应被 schema 拒绝，非法运行时构造应明确失败。

### 4.5 Execute Plan

新增共享纯计算 plan，首版放在 `BattleExecutionRules`。后续若出现第二个 staged lethal effect，再用单独任务拆独立 resolver。

`BattleExecutionRules` 边界：

- 只放无副作用公式和 execute plan 构建。
- 无 RNG。
- 不改 unit。
- 不读 UI/runtime facade。
- 不读取或修改 status collection。
- `build_execute_plan(source_unit, target_unit, effect_def)` 接收 `source_unit` 用于读取施法者该技能等级；当前 PWK 的 threshold 不得读取施法者属性或目标 rank。
- 阈值公式使用目标最大生命比例 floor + 技能等级增量，并受目标最大生命比例 cap 限制。属性修正只影响 `caster_spell` 豁免 DC，不影响 HP 阈值。
- 当前仓库中非 PWK 系统仍可能调用 `BattleExecutionRules.is_boss_target()` / `is_elite_or_boss_target()` 等 rank helper；本切片不得删除或改名这些 helper。它们是既有 rank projection helper，不是 PWK 分支输入，也不是旧 schema 兼容路径；`build_execute_plan()` 不得调用它们。

Plan key contract:

| key | type | semantics |
|---|---|---|
| `branch` | `StringName` | `invalid_target` / `low_hp_execute`。 |
| `current_hp` / `max_hp` / `threshold` | `int` | 构建 plan 时的目标生命快照和执行阈值；`threshold` 已包含技能等级增量，不包含属性修正。 |
| `fatal_damage` | `int` | 豁免失败时提交给标准伤害链的 fatal execute 伤害，低血有效目标为当前 HP。 |
| `min_hp_after_fatal` | `int` | fatal execute 固定为 `0`，允许分级死亡保护链接管。 |
| `bypass_shield` | `bool` | fatal execute 固定 `true`，但不修改护盾状态。 |
| `soul_fracture_params` | `Dictionary` | 只包含 `heal_multiplier_percent`、`shield_gain_multiplier_percent`、`duration_tu`。 |

Threshold formula contract:

```text
hp_floor = max_hp * threshold_max_hp_ratio_percent / 100
level_bonus = max(skill_level - threshold_level_anchor, 0) * threshold_level_bonus_per_delta
raw_threshold = max(hp_floor, 1 when max_hp > 0) + level_bonus
cap = max_hp * threshold_cap_max_hp_ratio_percent / 100
threshold = min(raw_threshold, cap) when cap > 0 else raw_threshold
```

`threshold_ability_mod` and `threshold_ability_mod_multiplier` are not part of PWK v1.

最终 API：

```gdscript
class_name BattleExecutionRules
extends RefCounted

const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attributes/attribute_service.gd")
const BATTLE_EXECUTE_CONTENT_RULES = preload("res://scripts/player/progression/battle_execute_content_rules.gd")

const BRANCH_LOW_HP_EXECUTE: StringName = &"low_hp_execute"
const BRANCH_INVALID_TARGET: StringName = &"invalid_target"

static func get_max_hp(unit: BattleUnitState) -> int:
	if unit == null or unit.attribute_snapshot == null:
		return 0
	return maxi(int(unit.attribute_snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.HP_MAX)), 0)

static func resolve_threshold(_source_unit: BattleUnitState, target_unit: BattleUnitState, params: Dictionary) -> int:
	var target_max_hp := get_max_hp(target_unit)
	var ratio := clampi(int(params[BATTLE_EXECUTE_CONTENT_RULES.PARAM_THRESHOLD_MAX_HP_RATIO_PERCENT]), 0, 100)
	var threshold := maxi(target_max_hp * ratio / 100, 0)
	if target_max_hp > 0:
		return maxi(threshold, 1)
	return 0

static func build_execute_plan(source_unit: BattleUnitState, target_unit: BattleUnitState, params: Dictionary) -> Dictionary:
	var current_hp := maxi(int(target_unit.current_hp) if target_unit != null else 0, 0)
	var max_hp := get_max_hp(target_unit)
	var threshold := resolve_threshold(source_unit, target_unit, params)
	var branch := BRANCH_INVALID_TARGET
	var fatal_damage := 0
	if max_hp > 0 and current_hp > 0 and current_hp <= threshold:
		branch = BRANCH_LOW_HP_EXECUTE
		fatal_damage = current_hp
	return {
		"branch": branch,
		"current_hp": current_hp,
		"max_hp": max_hp,
		"threshold": threshold,
		"fatal_damage": fatal_damage,
		"min_hp_after_fatal": 0,
		"bypass_shield": true,
		"soul_fracture_params": {
			"heal_multiplier_percent": int(params[BATTLE_EXECUTE_CONTENT_RULES.PARAM_HEAL_MULTIPLIER_PERCENT]),
			"shield_gain_multiplier_percent": int(params[BATTLE_EXECUTE_CONTENT_RULES.PARAM_SHIELD_GAIN_MULTIPLIER_PERCENT]),
			"duration_tu": int(params[BATTLE_EXECUTE_CONTENT_RULES.PARAM_SOUL_FRACTURE_DURATION_TU]),
		},
	}
```

AI、target gate 和 runtime 都必须消费 `build_execute_plan()`，不能各自重写 threshold / low HP 分支。

Shared execute plan gate:

- `BattleExecutionRules.build_execute_plan()` is the single source of truth for HP-threshold branch selection.
- Player preview and player command issue must reach the HP gate through `_validate_unit_skill_targets()` -> `_get_execute_target_validation_message()` -> `BattleExecutionRules.build_execute_plan()`.
- Runtime mutation must call `build_execute_plan()` again inside `_apply_execute_effect()` only as a developer safety gate. In normal command flow, `invalid_target` must already have been rejected before cost consumption.
- AI candidate validity must use the public runtime preview path, `context.preview_command(command)`, not `_validate_unit_skill_targets()` directly.
- AI scoring must independently consume `BattleExecutionRules.build_execute_plan()` and must not infer execute branch from preview log text, fixed damage preview, target rank, or duplicated threshold math.

Performance note:

- `build_execute_plan()` 当前返回 `Dictionary`，以实现简单和 schema 可读性优先。
- AI 估值路径会高频调用该函数；只有 profiling 证明 `Dictionary` 分配成为热点时，才迁移到 `RefCounted` plan object 或对象池。
- 本轮不提前做 plan 池化，避免生命周期复用和残留 key 污染 AI/runtime。

### 4.6 BattleDamageResolver

修改 `scripts/systems/battle/rules/battle_damage_resolver.gd`：

- `resolve_effects()` 中只保留一行分发：`&"execute": result = _apply_execute_effect(...)`。
- `_apply_execute_effect()` 负责读取 save、execute plan，并调用现有 `_apply_damage_to_target()`。
- 删除当前旧分支里的 `shield_absorption_percent`、`params.damage_tag`、`soul_fracture_status` 嵌套参数读取。
- execute 不调用 `_resolve_damage_outcome()`，不进入通用 mitigation。
- 引用 `BattleTypedEnums` / `BattleSaveContentRules` / C# damage-tag owner 获取 execute/save/damage 常量，引用 `BattleStatusSemanticTable` 获取 `STATUS_SOUL_FRACTURE`；不要通过 execute helper 间接取得状态 id。

同步修改 `scripts/systems/battle/rules/battle_report_formatter.gd`：

- `append_damage_result_log_lines()` / damage summary path must preserve and consume `execute_stage`.
- Stage-specific PWK log wording is part of the runtime contract, not optional presentation polish.

Helper 返回契约：

```gdscript
{
	"applied": bool,
	"damage": int,
	"shield_absorbed": 0,
	"shield_broken": false,
	"damage_events": Array[Dictionary],
	"save_results": Array[Dictionary],
	"status_effect_ids": Array[StringName],
	"execute_plan": Dictionary,
	"execute_stage": int,
	"execute_outcome": StringName,
}
```

Runtime branch:

1. Build plan through `BattleExecutionRules.build_execute_plan(source_unit, target_unit, effect_def)`; this is the single HP-threshold decision for every target rank.
2. If `plan.branch == &"invalid_target"`:
   - Do not resolve an `execute` save.
   - Do not apply damage or `soul_fracture`.
   - Return `applied = false` and record a developer-facing error if this reached runtime after target validation.
   - Player command issue must reject this branch before AP / MP / cooldown cost consumption; AI must not assemble actions for it.
3. If `plan.branch == &"low_hp_execute"`:
   - Resolve one `execute` save through `BattleSaveResolver.resolve_save()`.
   - PWK branch logic consumes only `bool(save_result.get("success", false))`.
   - `execute_immunity` must be represented by `BattleSaveResolver` as `success = true`; PWK does not separately branch on `immune`.
   - Other save result fields are retained only in `save_results` for log / test / debug output.
   - If save succeeded, apply no HP damage, emit no damage event, and set top-level `execute_stage = 0`, `execute_outcome = &"resisted"`.
   - If save failed, apply `plan.fatal_damage`, `min_hp_after_damage = 0`, `bypass_shield = true`, `execute_stage = 2`.
   - Fatal execute carries a high death source priority. Death prevention whose protection authority is lower than the PWK death source is skipped; sufficiently high-tier death prevention may still save the target.
4. If `target_unit.is_alive == true and target_unit.current_hp > 0`, apply clean weak `soul_fracture` status.

Execute event flow contract:

- HP-threshold branch selection must happen before save resolution. Invalid/high-HP targets do not roll or log an `execute` save.
- In the low HP branch, combat log presentation must use `execute_stage` and `save_results` to render a coherent narrative order: save result, fatal execute damage if applicable, then `soul_fracture` if the target survives.
- Fatal execute is a damage-buffer flush point. If future runtime introduces deferred HP batching, fatal execute damage must be committed before checking `is_alive` / `current_hp > 0` for `soul_fracture`.
- After fatal execute and any `death_ward` / `last_stand` nested resolution, `_apply_execute_effect()` must re-check `target_unit.is_alive == true` and `target_unit.current_hp > 0` before applying `soul_fracture`.
- `soul_fracture` application and log lines must appear after all PWK damage events.
- `status_effect_ids` means actually applied status ids only. Append `soul_fracture` after `_apply_status_effect()` reports success; if the status system rejects it, do not append the id and do not log a gained-status line.
- `bypass_shield = true` means the damage application must not change `current_shield_hp`, `shield_max_hp`, `shield_duration`, or `shield_family`; the full PWK HP damage is applied directly to `current_hp`. If expired shield cleanup is needed, perform it as turn/status maintenance outside the bypass damage path, not as a side effect of PWK damage.

Damage outcome shape:

```gdscript
{
	"resolved_damage": damage,
	"damage_tag": effect_def.damage_tag,
	"bypass_shield": true,
	"min_hp_after_damage": 0,
	"execute_stage": 2,
}
```

`execute_stage` is a shared runtime / formatter / test contract:

| value | meaning |
|---|---|
| `0` | Top-level helper result only; no HP damage event; save success / immunity branch may still apply `soul_fracture`. |
| `2` | Damage event stage; fatal execute damage; runs the tiered death-protection chain. |

Combat log contract:

- `BattleReportFormatter.append_damage_result_log_lines()` must inspect `execute_stage`.
- `execute_stage == 2`: log as "死亡律令生效 / 触发分级死亡保护链", not merely ordinary damage.
- If `execute_stage == 2` and `bypass_shield == true`, include the fixed phrase "死亡律令穿透护盾" before the damage amount / death protection wording.
- Top-level `execute_stage == 0`: log exactly "目标抵抗死亡律令。"; no damage line should be emitted.
- Formatter may reorder rendered PWK lines by stage for readability; it must not rely only on the physical order of `damage_events`.

`_coerce_damage_outcome()` boundary:

- Do not globally narrow the existing shared damage result normalization; non-execute damage must keep its current generic defaults and event shape.
- Execute-created outcomes must explicitly set `bypass_shield`, `min_hp_after_damage`, and `execute_stage` before entering `_apply_damage_to_target()`.
- Execute must not introduce or depend on `shield_absorption_percent`, `true_damage`, `bypass_mitigation`, or `death_protection_policy`.
- Move `target_unit.normalize_shield_state()` behind the `not bypass_shield` branch. When `bypass_shield == true`, `_apply_damage_to_target()` must not normalize, drain, clear, or refresh shield fields as a side effect of PWK.

`_apply_damage_to_target()` must return actual HP loss:

- `damage` / `hp_damage` must be actual HP lost after shield bypass, HP application, and any death-protection result.
- PWK must not report 9999 or any theoretical overkill value; fatal execute damage is based on the target's current HP snapshot.
- `execute_stage == 2` is the only PWK damage stage and must run fatal trait hooks, `death_ward`, `last_stand`, and last-stand mastery through the tiered death-protection chain.
- `bypass_shield = true` must leave `current_shield_hp`, `shield_max_hp`, `shield_duration`, and `shield_family` unchanged, and must calculate HP loss from the full fatal damage amount without shield absorption.

Actual HP loss must be calculated after all clamps:

```gdscript
var hp_before := maxi(int(target_unit.current_hp), 0)
# apply shield / HP / min_hp_after_damage logic here
var hp_after := maxi(int(target_unit.current_hp), 0)
var actual_hp_lost := maxi(hp_before - hp_after, 0)
result["damage"] = actual_hp_lost
result["hp_damage"] = actual_hp_lost
```

Clean status builder:

```gdscript
func _build_soul_fracture_effect(plan: Dictionary) -> CombatEffectDef:
	var status_params: Dictionary = plan.get("soul_fracture_params", {})
	var status_effect := CombatEffectDef.new()
	status_effect.effect_type = &"status"
	status_effect.status_id = BattleStatusSemanticTable.STATUS_SOUL_FRACTURE
	status_effect.duration_tu = int(status_params["duration_tu"])
	status_effect.params = {
		"heal_multiplier_percent": int(status_params["heal_multiplier_percent"]),
		"shield_gain_multiplier_percent": int(status_params["shield_gain_multiplier_percent"]),
			"source_effect_type": &"execute",
	}
	return status_effect
```

Do not pass raw execute params into `_apply_status_effect()`.

### 4.7 Soul Fracture And Status Modifiers

修改 `scripts/systems/battle/rules/battle_status_semantic_table.gd`：

```gdscript
const STATUS_SOUL_FRACTURE: StringName = &"soul_fracture"
```

Rules:

- harmful: true
- cleansable harmful: true through harmful default
- dispellable harmful: true
- stack mode: refresh
- max stacks: 1
- tick: none
- PWK applies the weak variant through params: 60 TU, heal multiplier 50%, shield gain multiplier 50%.
- semantic includes `display_label = "灵魂裂解"` and `description_text = "由律令死亡施加：治疗与护盾获得降低。"` so the source remains visible without changing UI scene structure.

当前实现为 plain static C# `scripts/systems/battle/rules/BattleStatusModifierRules.cs`，不作为 Godot `class_name` / `RefCounted` 暴露：

```csharp
BattleStatusModifierRules.ResolveHealMultiplierPercent(targetUnit);
BattleStatusModifierRules.ResolveShieldGainMultiplierPercent(targetUnit);
BattleStatusModifierRules.ApplyHealMultiplier(targetUnit, amount);
BattleStatusModifierRules.ApplyShieldGainMultiplier(targetUnit, amount);
```

The helper reads status entries through `BattleUnitState.GetStatusEffectsTyped()` and typed multiplier fields through `BattleStatusEffectState.TryGetHealMultiplierPercentTyped()` / `TryGetShieldGainMultiplierPercentTyped()`.

Use sites:

- `BattleDamageResolver` heal branch applies `heal_multiplier_percent`.
- `BattleShieldService._apply_shield_effect_to_target()` applies `shield_gain_multiplier_percent`.

Rules:

- Runtime modifier truth is the typed fields on `BattleStatusEffectState`; legacy `params` keys must not drive heal / shield multipliers. Serialization may project typed fields to params at payload boundaries only.
- Apply after dice/static amount is resolved.
- Apply before HP cap or shield replace/merge comparison.
- If original amount is `> 0` and multiplier is `> 0`, final amount is at least 1.
- Do not affect `heal_fatal` in this slice.
- Do not retroactively reduce existing shields.

Tests must cover both runtime typed-field behavior and schema/payload boundaries: `tests/battle_runtime/rules/run_battle_status_modifier_rules_regression.cs` for runtime multipliers, plus the relevant status/effect schema runner for strict int/range coverage.

Performance note:

- First implementation may use the helper scan above because `soul_fracture` is a narrow status slice and the ownership is explicit.
- If profiling shows heal/shield modifier lookup is a hotspot, add derived runtime caches on `BattleUnitState` such as `heal_multiplier_percent_cache` and `shield_gain_multiplier_percent_cache`.
- Such caches must be derived from `status_effects`, rebuilt after `set_status_effect()` / `erase_status_effect()` / status tick cleanup / `from_dict()`, and must not become serialized truth or a replacement for status params.

### 4.8 Dispatch

Modify:

- `scripts/systems/battle/core/battle_preview.gd`
- `scripts/systems/battle/rules/battle_skill_resolution_rules.gd`
- `scripts/systems/battle/runtime/battle_skill_execution_orchestrator.gd`
- `scripts/systems/battle/presentation/battle_hud_adapter.gd`
- `scripts/systems/game_runtime/game_runtime_battle_selection.gd`
- `scripts/systems/game_runtime/battle_session_facade.gd`
- `scripts/systems/game_runtime/game_runtime_facade.gd`

Rules:

- Add execute wherever single-target unit effects are accepted.
- Do not add `skill_id == &"mage_power_word_kill"` branches.
- Unit-target command issue must build the execute plan before cost consumption; `invalid_target` is rejected before AP / MP / cooldown are consumed.
- Preview / target validation must mark high-HP targets as invalid for PWK instead of allowing a no-effect cast.
- Legal low-HP preview must not show a fixed damage number. It must fill `BattlePreview.save_branch_preview` and show the branch text "豁免失败：死亡律令；豁免成功：灵魂裂解。"
- Legal low-HP preview must show a player-facing hit chance for the fatal PWK branch. This is presentation probability, not AI scoring truth.
- Ground path must reject execute in preview / validation and before cost is consumed.
- `special_resolution_profile_id != &""` skills cannot carry execute effects.

Target gate ownership:

- `BattleSkillResolutionRules` owns route / variant / effect-set resolution only.
- `BattleSkillExecutionOrchestrator._get_unit_skill_target_validation_message()` owns the pre-cost execute target gate.
- The gate must call `BattleExecutionRules.build_execute_plan()` and must not duplicate threshold math.
- `_validate_unit_skill_targets()` and `_preview_unit_skill_command_impl()` / `_handle_unit_skill_command()` do not add separate PWK branches; they share the existing validation path.
- `_can_skill_target_unit()` does not contain PWK-specific logic; it keeps calling `_get_unit_skill_target_validation_message()` so boolean target affordance stays consistent with preview/issue messages.
- The execute helper must only return the high-HP threshold message after `target_unit != null`, `target_unit.is_alive == true`, and `_is_unit_valid_for_effect(active_unit, target_unit, skill_def.combat_profile.target_team_filter)` are true. Null targets return the execute invalid-target message; wrong-team and dead targets keep the existing generic validation messages. AP / MP / cooldown remain owned by `_get_skill_command_block_reason()` before target validation; range remains owned by `_can_skill_target_unit()`.
- `GameRuntimeBattleSelection` must not duplicate unit-skill target rules for selected-skill highlights, selected-click eligibility, auto unit-skill command building, or multi-target queueing. Add a runtime/facade target-affordance API delegated to `BattleSkillExecutionOrchestrator._can_skill_target_unit()` / validation, and make `GameRuntimeBattleSelection` consume that API with the selected/default cast variant. `BattleSessionFacade` and `GameRuntimeFacade` may expose this API as pure pass-through methods only; they must not inspect execute effects, compute HP thresholds, or contain PWK-specific logic.
- `BattleDamageResolver._apply_execute_effect()` is only a developer safety fallback if `invalid_target` reaches mutation; it must not be the user-facing gate.

Target affordance API shape:

```gdscript
func get_unit_skill_target_affordance(
	active_unit: BattleUnitState,
	target_unit: BattleUnitState,
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef = null,
	require_ap: bool = true
) -> Dictionary:
	# delegates to the same validation / _can_skill_target_unit path as preview and issue
	return {
		"allowed": bool,
		"reason": String,
		"target_coords": Array[Vector2i],
	}
```

`GameRuntimeBattleSelection._collect_valid_unit_skill_target_coords()` must collect `target_coords` only from `allowed == true` affordance results. High-HP PWK targets must not appear in `valid_target_coords` or battle overlay coords.

Unit target pre-cost validation shape:

```gdscript
func _get_execute_target_validation_message(
	active_unit: BattleUnitState,
	target_unit: BattleUnitState,
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef = null
) -> String:
	var execute_lookup := _find_single_execute_effect(
		_collect_unit_skill_effect_defs(skill_def, cast_variant, active_unit)
	)
	var lookup_error := String(execute_lookup.get("error_message", ""))
	if not lookup_error.is_empty():
		return lookup_error
	var execute_effect = execute_lookup.get("effect", null) as CombatEffectDef
	if execute_effect == null:
		return ""
	if target_unit == null:
		return "律令死亡目标无效。"
	if not target_unit.is_alive:
		return ""
	if not _is_unit_valid_for_effect(active_unit, target_unit, skill_def.combat_profile.target_team_filter):
		return ""
	var plan := BattleExecutionRules.build_execute_plan(active_unit, target_unit, execute_effect.params)
	if ProgressionDataUtils.to_string_name(plan.get("branch", &"")) != BattleExecutionRules.BRANCH_INVALID_TARGET:
		return ""
	return "%s 当前生命高于律令死亡阈值。" % target_unit.display_name
```

Call order:

- `_get_unit_skill_target_validation_message()` must call `_get_execute_target_validation_message()` before returning `""`.
- `_validate_unit_skill_targets()` and `_preview_unit_skill_command_impl()` already share this validation path, so preview and command issue reject the same high-HP target before cost consumption.
- `_handle_unit_skill_command()` must not add a second post-cost high-HP gate; if `invalid_target` reaches `_apply_execute_effect()`, treat it as a developer error and return `applied = false`.
- `_find_single_execute_effect()` returns `{"effect": CombatEffectDef or null, "error_message": String}`. If runtime receives more than one execute effect, return an explicit validation error instead of picking the first.

Preview surface contract:

- Add `var save_branch_preview: Dictionary = {}` to `BattlePreview`.
- `_preview_unit_skill_command_impl()` fills `save_branch_preview` after target validation succeeds and exactly one execute effect is found.
- Do not put execute branch text into `damage_preview.summary_text`. `BattleDamagePreviewRangeService` remains the ordinary non-critical damage-range preview and only handles `effect_type == &"damage"`.
- `BattleReportFormatter` formats real post-resolution battle log lines only. It must not be reused to format preview.
- `BattlePreview.log_lines`, HUD `damage_text`, and selected/hover text are presentation-only; no runtime or AI logic may parse them.

`save_branch_preview` shape for legal low-HP PWK:

```gdscript
{
	"kind": &"execute",
	"plan_branch": BattleExecutionRules.BRANCH_LOW_HP_EXECUTE,
	"target_unit_id": target_unit.unit_id,
	"current_hp": int(plan["current_hp"]),
	"max_hp": int(plan["max_hp"]),
	"threshold": int(plan["threshold"]),
	"save_tag": &"execute",
	"save_ability": effect_def.save_ability,
	"save_success_chance_basis_points": save_success_bps,
	"hit_chance_basis_points": clampi(10000 - save_success_bps, 0, 10000),
	"failure_outcome_id": &"fatal_execute",
	"failure_text": "豁免失败：死亡律令",
	"success_outcome_id": BattleStatusSemanticTable.STATUS_SOUL_FRACTURE,
	"success_text": "豁免成功：灵魂裂解",
	"summary_text": "豁免失败：死亡律令；豁免成功：灵魂裂解。",
}
```

Preview probability is in scope for v4.8 because players need to see the PWK hit chance. `_preview_unit_skill_command_impl()` must compute it through the battle save probability owner and write structured fields into `save_branch_preview`; HUD / hover may display those fields but must not derive probability itself. AI still consumes its own structured save estimates and `kill_probability_basis_points`, not HUD strings.

HUD consumption:

- `BattleHudAdapter` must consume `selected_skill_runtime_preview.save_branch_preview` for selected-skill subtitle / tooltip and hover preview payloads.
- Add snapshot keys such as `selected_skill_save_branch_preview_payload` and `selected_skill_save_branch_preview_text`; hover preview returns equivalent `save_branch_preview` / `save_branch_preview_text`.
- `BattleHoverPreviewOverlay` displays the branch preview and player-facing hit chance from those structured keys for low-HP targets. High-HP targets are invalid hover targets through `valid_target_coords`, so they do not show branch preview, hit chance, or fixed damage text.

Ground pre-cost rejection shape:

```gdscript
var ground_effect_defs := _collect_ground_unit_effect_defs(skill_def, cast_variant, active_unit)
for effect_def in ground_effect_defs:
		if effect_def != null and effect_def.effect_type == &"execute":
		push_error("Ground execute effect is invalid and was rejected before cost consumption: %s" % String(skill_def.skill_id))
		return false
# consume AP/MP/cooldown only after this gate passes
```

The same ground execute check must run in ground preview / validation before `preview.allowed = true`. Illegal ground execute must not be presented as an allowed preview and then rejected only during issue.

### 4.9 AI

AI has three separate responsibilities:

1. Runtime action plan generation:
   - `BattleAiSkillAffordanceClassifier._is_damage_effect()` treats execute as damage / finisher.
   - `BattleAiActionAssembler._is_offensive_effect()` treats execute like damage for enemy skills.
   - This layer must not run HP-threshold checks because it has no per-target battle-state candidate.
2. Per-target candidate gating:
   - `UseUnitSkillAction` and any future unit-target AI action must keep calling `context.preview_command(command)` before scoring.
   - Candidates with `preview.allowed == false` are dropped before score input construction.
   - A high-HP PWK target may appear only as a preview rejection trace, never as a low-scored executable candidate.
3. Score estimation:
   - `BattleAiScoreService._estimate_execute_for_target_result()` consumes `BattleExecutionRules.build_execute_plan(source_unit, target_unit, effect_def)`.
   - If `plan.branch == invalid_target`, return `actionable = false`, `is_empty = true`, `damage = 0`, `kill_probability_basis_points = 0`, `save_estimates = []`, and do not request save probability.

Modify `scripts/systems/battle/ai/battle_ai_score_service.gd`:

- `_is_damage_skill()` treats execute as damage.
- `_build_target_effect_metrics()` calls `_estimate_execute_for_target_result()`.
- `_estimate_execute_for_target_result()` consumes `BattleExecutionRules.build_execute_plan(source_unit, target_unit, effect_def)`.
- Do not recompute threshold or target eligibility outside the plan.
- If `plan.branch == &"invalid_target"`, return no offensive action candidate / zero metrics; do not request a save estimate.
- Execute lethal bonus must consume `kill_probability_basis_points`; it must not use `expected_damage >= current_hp`.
- If the target has known death-prevention protection, reduce `kill_probability_basis_points` conservatively according to protection authority instead of simulating the full death-protection chain.
- If the target is expected to remain alive after PWK, expose `soul_fracture_applied = true` so the scoring layer can count the harmful status value.
- The scoring layer must consume `soul_fracture_applied`, either by incrementing harmful-control value or by applying a dedicated capped PWK debuff payoff. This value must remain below true kill probability in priority.

AI result contract:

```gdscript
{
	"actionable": bool,
	"is_execute": true,
	"damage": expected_damage,
	"kill_probability_basis_points": kill_bps,
	"expected_remaining_hp": expected_remaining_hp,
	"save_estimates": Array[Dictionary],
	"soul_fracture_applied": bool,
}
```

AI semantics:

- Low HP execute branch is probability-based for every target rank.
- Save failure expected damage is based on `plan.fatal_damage`, but lethal priority must come from `kill_probability_basis_points`.
- Save success or `execute_immunity` sets HP damage and kill probability to `0`, but may preserve bounded `soul_fracture_applied` value.
- Invalid/high-HP targets return `actionable = false`, `damage = 0`, `kill_probability_basis_points = 0`, `save_estimates = []`, and do not build or consume an `execute` save estimate.
- Known death-prevention protection reduces kill probability according to protection authority but does not erase the status value if the target is expected to remain alive.
- `soul_fracture_applied` should add harmful-status value without overpowering true kill probability.
- AI scoring must be invariant under changes to `BattlePreview.log_lines`, `BattlePreview.save_branch_preview.*_text`, HUD `damage_text`, and selected/hover preview strings. It consumes structured battle state, effect defs, save estimates, and `BattleExecutionRules.build_execute_plan()`, not presentation text.

Execute lethal bonus consumption:

```gdscript
if bool(effect_metrics.get("is_execute", false)):
	var kill_bps := clampi(int(effect_metrics.get("kill_probability_basis_points", 0)), 0, 10000)
	lethal_bonus += _resolve_execute_lethal_bonus_from_bps(kill_bps, target_unit)
else:
	lethal_bonus += _resolve_lethal_target_bonus(estimated_damage, target_unit)
```

Save estimate helper may reuse existing `_build_damage_save_estimate(..., 0, skill_id)` for probability fields, but must not introduce a second save probability formula.

Modify `scripts/systems/battle/ai/battle_ai_action_assembler.gd`:

- `_is_offensive_effect()` treats execute like damage for enemy skills.

Modify `scripts/systems/battle/ai/battle_ai_skill_affordance_classifier.gd`:

- `_is_damage_effect()` treats execute as damage / finisher so PWK can generate hostile unit-skill actions.

### 4.10 Resource

Add `data/configs/skills/mage_power_word_kill.tres`.

Resource skeleton must use the C# resource scripts. The exact `.tres` serialization may differ after Godot re-saves it; the field intent is:

```ini
[gd_resource type="Resource" script_class="SkillDef" format=3]

[ext_resource type="Script" path="res://scripts/player/progression/CombatEffectDef.cs" id="1_effect"]
[ext_resource type="Script" path="res://scripts/player/progression/CombatSkillDef.cs" id="2_combat"]
[ext_resource type="Script" path="res://scripts/player/progression/SkillDef.cs" id="3_skill"]

[sub_resource type="Resource" id="execute"]
script = ExtResource("1_effect")
effect_type = &"execute"
effect_target_team_filter = &"enemy"
damage_tag = &"negative_energy"
save_dc_mode = &"caster_spell"
save_dc = 0
save_dc_source_ability = &"intelligence"
save_ability = &"willpower"
save_tag = &"execute"
threshold_max_hp_ratio_percent = 20
threshold_level_anchor = 17
threshold_level_bonus_per_delta = 5
threshold_cap_max_hp_ratio_percent = 50
soul_fracture_duration_tu = 60
heal_multiplier_percent = 50
shield_gain_multiplier_percent = 50

[sub_resource type="Resource" id="combat"]
script = ExtResource("2_combat")
skill_id = &"mage_power_word_kill"
target_mode = &"unit"
target_team_filter = &"enemy"
target_selection_mode = &"single_unit"
range_pattern = &"single"
range_value = 12
area_pattern = &"single"
area_value = 0
requires_los = true
ap_cost = 1
mp_cost = 2000
cooldown_tu = 600
min_target_count = 1
max_target_count = 1
selection_order_mode = &"stable"
ai_tags = Array[StringName]([&"execute", &"single_target", &"finisher"])
delivery_categories = Array[StringName]([&"spell", &"necromancy"])
effect_defs = Array[ExtResource("1_effect")]([SubResource("execute")])

[resource]
script = ExtResource("3_skill")
skill_id = &"mage_power_word_kill"
display_name = "律令死亡"
icon_id = &"mage_power_word_kill"
description = "以死亡律令裁决濒死敌人。仅可对生命不高于阈值的目标施放；目标进行意志豁免，失败则触发高权威死亡判定，低级免死难以拦截，成功或免疫时不受伤害但仍会短暂受到灵魂裂解。"
skill_type = &"active"
max_level = 1
non_core_max_level = 1
mastery_curve = PackedInt32Array(2400)
tags = Array[StringName]([&"mage", &"magic", &"necromancy", &"execute", &"single_target", &"output"])
learn_source = &"book"
growth_tier = &"ultimate"
attribute_growth_progress = {
"intelligence": 160,
"willpower": 80
}
combat_profile = SubResource("combat")
```

## 5. Test Matrix

Minimum regressions:

| Runner | Set | Required assertions |
|---|---|---|
| `tests/progression/schema/run_skill_schema_regression.gd` | Quick | Add an isolated `_test_execute_effect_schema_validation()` section: formal PWK resource passes; magic save tag, wrong damage tag, passive execute, triggered execute, special-profile execute, ground/multi execute, missing/extra execute params, and non-int params fail. Do not scatter execute assertions through unrelated schema tests. |
| `tests/runtime/validation/run_resource_validation_regression.gd` | Quick | Every formal enemy template has explicit valid `target_rank`; `boss_target` / `fortune_mark_target` in `attribute_overrides` is rejected. Traverse formal enemy `.tres` files under `res://data/configs/enemies/` with `DirAccess`, excluding documented non-formal fixtures such as `_base` templates rather than hard-coding only the current template list. |
| `tests/battle_runtime/rules/run_battle_execution_rules_regression.gd` | Quick, new | Pure execute plan contract: threshold math includes skill-level bonus, invalid vs low-HP branch, `fatal_damage == current_hp`, normal/Boss/rank projection does not affect branch selection, and source attributes do not affect HP threshold. |
| `tests/battle_runtime/runtime/run_battle_save_resolver_regression.gd` | Quick | `execute_advantage`, `execute_disadvantage`, `execute_immunity`; `magic_advantage` does not affect execute. |
| `tests/battle_runtime/runtime/run_battle_execute_target_gate_regression.gd` | Quick, new | High-HP targets are rejected before AP / MP / cooldown cost consumption; no save is rolled, no damage/status is applied, and normal/Boss fixtures use the same threshold gate. Low-HP PWK preview is allowed, fills `save_branch_preview` including player-facing hit chance, and does not emit fixed `damage_preview.summary_text` such as `伤害 N`. |
| `tests/battle_runtime/runtime/run_game_runtime_battle_selection_regression.gd` | Quick | Selected PWK excludes high-HP enemies from `get_selected_battle_skill_valid_target_coords()` / battle overlay coords; low-HP enemies appear; clicking a high-HP enemy does not queue target selection, issue the command, or consume AP/MP/cooldown. |
| `tests/battle_runtime/runtime/run_battle_execute_lethal_regression.gd` | Quick, new | Low-HP save fail -> fatal execute; tiered death-protection authority skips insufficient protections but allows sufficiently high-tier protection; shield bypass leaves shield fields unchanged; normal and Boss fixtures use the same lethal rules. |
| `tests/battle_runtime/runtime/run_battle_execute_save_branch_regression.gd` | Quick, new | Low-HP save success and `execute_immunity` cause no HP damage, apply weak `soul_fracture` only if the target survives, and preserve execute advantage/disadvantage/immunity branch consistency. |
| `tests/battle_runtime/rules/run_status_effect_semantics_regression.cs` | Quick | `soul_fracture` harmful, cleansable, dispellable harmful, refresh, PWK weak variant 60 TU, reduces normal heal and shield gain to 50%, and does not affect `heal_fatal`. |
| `tests/battle_runtime/rules/run_battle_rule_status_param_schema_regression.gd` | Quick | `heal_multiplier_percent` and `shield_gain_multiplier_percent` strict int/range validation. |
| `tests/battle_runtime/rules/run_battle_execute_damage_resolver_regression.gd` | Quick, new | Execute-specific resolver contract only: invalid branch safety fallback, `save_results`, `execute_stage`, `execute_outcome`, actually applied `status_effect_ids`, `soul_fracture` ordering, and no old high-HP non-lethal / Boss non-lethal behavior. |
| `tests/battle_runtime/rules/run_battle_damage_resolver_mutation_regression.gd` | Quick, new | Non-execute mutation stability after `_apply_damage_to_target()` changes: shield absorption, actual HP loss after clamps, fatal hooks / death protection, and ordinary damage contracts. Do not use preview contract runners for this. |
| `tests/battle_runtime/ai/run_battle_ai_score_save_probability_regression.gd` | Quick | Fatal execute value changes with supplied save probability; normal and Boss targets with the same HP/save state use the same target gate and low-HP plan branch; all high-HP PWK candidates are rejected by preview before scoring; score service invalid branch produces no save estimate and no effective target value even if called directly; execute immunity makes kill probability and HP damage 0 while preserving bounded `soul_fracture_applied` payoff; execute lethal bonus follows `kill_probability_basis_points` instead of `expected_damage >= current_hp`; `death_ward` / `last_stand` reduce kill bps. Poison `preview.log_lines`, `preview.damage_preview.summary_text`, and `preview.save_branch_preview.*_text` to prove AI score / kill bps / save estimates / `soul_fracture_applied` do not parse presentation text. |
| `tests/battle_runtime/runtime/run_battle_execute_effect_regression.cs` | Full | Rewrite as 1-2 formal PWK resource end-to-end smoke paths. Remove old execute assertions for fixed save, `magic` save tag, `shield_absorption_percent`, high-HP non-lethal, and Boss non-lethal behavior. |
| `tests/battle_runtime/runtime/run_battle_execute_ground_protocol_regression.gd` | Full, new | Illegal ground execute preview / validation / issue are rejected before AP / MP / cooldown consumption; no damage/status mutation. Keep this focused instead of adding more assertions to `run_battle_skill_protocol_regression.gd`. |
| `tests/battle_runtime/rendering/run_battle_pwk_hover_preview_regression.gd` | Full, new | Focused PWK HUD/hover contract: high-HP hover is invalid and shows no branch/hit-chance/damage preview; low-HP hover consumes structured `save_branch_preview`, shows branch text plus player-facing hit chance, and does not show fixed damage. Poison `log_lines` and `damage_preview.summary_text` to prove HUD does not consume presentation fallbacks. |
| `tests/battle_runtime/rendering/run_battle_ui_regression.gd` | Full | Broad UI regression only; do not use it as the focused PWK hover contract. |
| `tests/battle_runtime/ai/run_battle_ai_skill_affordance_classifier_regression.cs` | Full | Execute effect is classified as a generatable hostile unit skill / finisher affordance. |
| `tests/battle_runtime/ai/run_battle_ai_action_assembler_plan_regression.gd` | Full | Execute skill passes through generation slots into a `UseUnitSkillAction` plan with stable metadata and without mutating brain/state resources. Do not create a near-duplicate `run_battle_ai_action_assembler_regression.gd`. |
| `tests/battle_runtime/ai/run_battle_runtime_ai_regression.gd` | Full | Real AI in mixed high/low HP target situations chooses only low-HP PWK targets; high-HP targets may appear only as preview rejection traces, not scored executable candidates. |
| `tests/battle_runtime/runtime/run_wild_encounter_regression.gd` | Full | Rank projection only: normal/elite/boss project to `boss_target` / `fortune_mark_target`. PWK rank-invariant assertions live in `run_battle_execution_rules_regression.gd`, not here. |
| `tests/battle_runtime/runtime/run_battle_skill_protocol_regression.gd` | Full conditional | Run only when shared skill protocol code changed; PWK ground execute protocol is covered by the focused ground runner above. |

Execution strategy:

- Quick set: schema/resource validation, pure execute rules, save resolver, focused execute target-gate/lethal/save-branch runners, game runtime selection, status semantics, execute/non-execute damage resolver contracts, and AI score probability. Keep each focused runner small enough for local iteration.
- Full set: quick set plus formal PWK smoke, ground execute protocol, focused PWK HUD hover, broad UI, classifier/action assembler, runtime AI, wild encounter, and any affected broad protocol runner.
- Static validation runners can run in parallel. Do not include battle simulation or balance runners unless explicitly requested.
- Local iteration can use a temporary aggregate runner that `load()`s the focused execute runner scripts in sequence to avoid repeated Godot startup. Keep the individual runner paths as the CI contract.

Quick focused command set:

```bash
godot --headless --script tests/progression/schema/run_skill_schema_regression.gd
godot --headless --script tests/runtime/validation/run_resource_validation_regression.gd
godot --headless --script tests/battle_runtime/rules/run_battle_execution_rules_regression.gd
godot --headless --script tests/battle_runtime/runtime/run_battle_save_resolver_regression.gd
godot --headless --script tests/battle_runtime/runtime/run_battle_execute_target_gate_regression.gd
godot --headless --script tests/battle_runtime/runtime/run_game_runtime_battle_selection_regression.gd
godot --headless --script tests/battle_runtime/runtime/run_battle_execute_lethal_regression.gd
godot --headless --script tests/battle_runtime/runtime/run_battle_execute_save_branch_regression.gd
godot --headless --script tests/battle_runtime/rules/run_status_effect_semantics_regression.cs
godot --headless --script tests/battle_runtime/rules/run_battle_rule_status_param_schema_regression.gd
godot --headless --script tests/battle_runtime/rules/run_battle_execute_damage_resolver_regression.gd
godot --headless --script tests/battle_runtime/rules/run_battle_damage_resolver_mutation_regression.gd
godot --headless --script tests/battle_runtime/ai/run_battle_ai_score_save_probability_regression.gd
```

Full add-ons:

```bash
godot --headless --script tests/battle_runtime/runtime/run_battle_execute_effect_regression.cs
godot --headless --script tests/battle_runtime/runtime/run_battle_execute_ground_protocol_regression.gd
godot --headless --script tests/battle_runtime/rendering/run_battle_pwk_hover_preview_regression.gd
godot --headless --script tests/battle_runtime/rendering/run_battle_ui_regression.gd
godot --headless --script tests/battle_runtime/ai/run_battle_ai_skill_affordance_classifier_regression.cs
godot --headless --script tests/battle_runtime/ai/run_battle_ai_action_assembler_plan_regression.gd
godot --headless --script tests/battle_runtime/ai/run_battle_runtime_ai_regression.gd
godot --headless --script tests/battle_runtime/runtime/run_wild_encounter_regression.gd
# Only when shared skill protocol code changed:
godot --headless --script tests/battle_runtime/runtime/run_battle_skill_protocol_regression.gd
```

## 6. Implementation Order

1. In one compiling slice, ensure C# execute/save/status canonical owners are present, add an execute helper only if validation needs it, make `EnemyTemplateDef.target_rank` required with Inspector enum hint / `_validate_property()` support, update all formal enemy templates, and remove conflicting partial execute constants / old test expectations touched by those files.
   - `BattleStatusModifierRules` is now a plain static C# helper, not a Godot `class_name` script. Do not restore class registration or GDScript preload access for it.
   - After each compiling slice, run `godot --headless --script tests/battle_runtime/runtime/run_battle_runtime_smoke.gd` or an equally narrow parser-loading smoke runner to surface GDScript parse/class registration errors immediately.
2. Project target rank in `EncounterRosterBuilder` for systems that need rank-derived attributes; PWK execute plan must ignore `target_rank`, `boss_target`, and `fortune_mark_target`.
3. Add strict execute schema validation and single-target/special-profile gates.
4. Finalize `BattleExecutionRules` as pure execute formula + explicit plan contract.
5. Refactor `BattleDamageResolver` execute branch into `_apply_execute_effect()`.
6. Fix `_apply_damage_to_target()` so reported damage is actual HP loss after clamp, fatal execute follows the tiered death-protection authority chain, and existing damage resolver regressions prove non-execute damage behavior is unchanged.
7. Add `BattleStatusModifierRules`, register `soul_fracture` semantics, and wire heal/shield multipliers through typed status reads. Keep the helper scan implementation unless profiling proves a derived `BattleUnitState` cache is needed.
8. Add dispatch support, low-HP target gate before cost consumption, runtime target-affordance API for selection/highlight, and ground-path preview + pre-cost rejection.
9. Add `BattlePreview.save_branch_preview` and HUD/hover consumption from structured preview fields. Keep `damage_preview` reserved for ordinary damage ranges and include player-facing PWK hit chance through explicit structured preview fields.
10. Add AI execute estimation using shared plan, `kill_probability_basis_points` lethal bonus consumption, death-protection kill-bps reduction, and `soul_fracture_applied` status value. Decouple AI weighting tests from the exact save DC formula and prove AI ignores presentation text.
11. Add `mage_power_word_kill.tres`.
12. Add/update split execute regressions, keep the old execute runner as smoke, and run the focused command set.

## 7. Architecture Boundaries

Keep ownership narrow:

- Execute effect kind lives in `BattleTypedEnums`; execute save tag lives in `BattleSaveContentRules`; optional execute helper owns only PWK field/schema helpers; `soul_fracture` status id and semantics live in `BattleStatusSemanticTable`.
- Enemy rank truth lives in `EnemyTemplateDef.target_rank`.
- Enemy rank projection lives in `EncounterRosterBuilder`.
- PWK branch selection ignores enemy rank and projected `boss_target`; v4.8 does not add a separate Boss bool truth source.
- Existing rank helper APIs used by non-PWK systems must remain available unless their callers are migrated atomically; they are not PWK branch inputs.
- Execute formula and plan live in `BattleExecutionRules`.
- Effect mutation lives in `BattleDamageResolver`.
- Status multiplier reading lives in plain static C# `BattleStatusModifierRules`.
- Shield amount modification lives in `BattleShieldService`.
- AI estimation lives in `BattleAiScoreService`.

Allowed facade change:

- `BattleSessionFacade` and `GameRuntimeFacade` may add pure pass-through target-affordance methods that delegate to battle runtime / orchestrator. This is not a rule owner change: no execute-effect lookup, HP-threshold calculation, save-branch construction, or PWK-specific branching may live in either facade.

Do not modify:

- `WorldMapRuntimeProxy` or `WorldMapSystem`.
- Save payload versioning or `SaveSerializer`.
- UI scene structure.
- Meteor Swarm special profile registry.
- Do not move PWK protocol back into untyped `CombatEffectDef.params`; plan object pooling remains future performance work, not a v4.8 implementation requirement.

Replay / serialization decision:

- This slice does not add battle replay support and does not modify `SaveSerializer`.
- PWK adds no new serialized `BattleUnitState` fields; `soul_fracture` uses typed `BattleStatusEffectState` multiplier fields, projected through the existing status payload boundary when serialized.
- Existing strict load behavior remains authoritative. Old saves or fixtures containing rejected execute params are not migrated and are allowed to fail existing validation.
- `execute_plan` is runtime/debug output, not persisted truth. If a future replay system needs deterministic PWK reproduction, that work must add a dedicated replay contract and versioned event payload in a separate design.

`docs/design/project_context_units.md` does not need to change for this document-only edit. When code is implemented, update CU-15, CU-16, CU-19 and CU-20 notes if runtime relationships or recommended read sets change.
