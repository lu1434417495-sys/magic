# 律令死亡（Power Word, Kill）代码级实现方案 v4.1

> 文档性质：三子代理架构对抗讨论后的共识实现任务包。
> 最后更新：2026-05-12
> 取代范围：全文取代 v2.x、v3.x、v4.0 及“v3.5 代码落地”段落中的不一致结论。
> 硬约束：不做兼容处理、不加旧字段别名、不保留错误中间实现、不把特殊逻辑塞进 UI / `GameRuntimeFacade` / `WorldMapSystem`。

## 1. 共识结论

`mage_power_word_kill` 使用一个受限的 `effect_type = &"execute"`，但本文不把它定义成完全通用处决系统。首版口径是：

> `execute v1 / PWK 执行协议`

未来如果要让其他技能复用不同属性、不同伤害类型、不同附加状态或不同死亡保护策略，必须重新扩展 schema，不允许靠参数别名、旧字段或宽松 fallback 混进去。

三代理共识：

| 议题 | 结论 |
|---|---|
| special profile | 不使用 Meteor Swarm special profile；PWK 是单体 unit effect。 |
| `execute` 口径 | v1 是 PWK 执行协议，不宣称通用即死系统。 |
| 常量归属 | 新增纯 content rules 文件集中保存 execute 常量，schema / rules / AI / runtime 共用。 |
| 资源参数 | 正式 `.tres` 不暴露 `burst_damage`、`finisher_damage`、`apply_status_id`、`death_protection_policy`。 |
| Stage 1 | 直接计算“压到 1 HP”的实际伤害，不使用 9999 魔法数字。 |
| Stage 2 | 固定走标准 fatal trait / `death_ward` / `last_stand` 链；正式资源不能绕过。 |
| Boss 判定 | PWK 只读 `boss_target`，不从 `fortune_mark_target` 推断 Boss。 |
| AI / runtime | 共用纯 execute plan；runtime 负责 mutation，AI 负责概率估值。 |
| `soul_fracture` | 状态倍率读取不放在 `BattleExecutionRules`，改由状态 modifier helper 负责。 |
| `target_rank` | 先作为 `EnemyTemplateDef` 必填字段；未来同模板多 rank 时升级 roster schema。 |

## 2. 玩家语义

`mage_power_word_kill`：

- 单体敌方法术，射程 12。
- 消耗 1 AP / 80 MP，冷却 120 TU。
- 对低血目标先压到 1 HP。
- 低血目标随后进行 `execute` 意志豁免；失败则进入标准致死链。
- 对高血普通目标造成 `threshold * 30%` 的非致命负能量压制。
- 对高血 Boss 造成 `max_hp * 30%` 的非致命负能量压制。
- 目标结算后仍有 `current_hp > 0` 时施加 `soul_fracture`。

非目标：

- 不实现通用即死系统、Saving Throw 大改、暴击倍率重构或 target-aware UI 数值预览。
- 不新增 `special_resolution_profile_id`。
- 不复用 `save_tag = &"magic"`。
- 不新增 `damage_tag = &"true"` 或 `&"execute"`。
- 不保留 `shield_absorption_percent`、`bypass_mitigation`、`true_damage`。
- 不让正式内容配置绕过 `death_ward` / `last_stand`。

## 3. 当前偏差必须替换

当前仓库已有一组未收束的 `execute` 痕迹。实现时按下表替换，不能作为兼容路径保留。

| 当前偏差 | 共识处理 |
|---|---|
| `EnemyTemplateDef.target_rank` 默认 `&"normal"` | 改为默认 `&""`，正式资源必须显式填写。 |
| 只有 `wolf_alpha.tres` 显式写 rank，其余 normal 靠默认 | 所有正式 enemy template 都显式写 `target_rank`。 |
| `execute` 使用 `save_tag = &"magic"` | 新增并使用 `SAVE_TAG_EXECUTE = &"execute"`。 |
| `execute` 使用 `shield_absorption_percent` | 删除该语义；PWK 使用 `bypass_shield = true`，完全不改护盾状态。 |
| `_coerce_damage_outcome()` 补 `bypass_mitigation / true_damage / shield_absorption_percent` | 移除这些字段。 |
| `BattleExecutionRules.resolve_threshold()` 读取等级、属性、floor/cap | 改为只读目标最大生命和比例。 |
| Boss 非致命默认 `12% + floor 25` | 改为 `max_hp * 30%`，不设 floor。 |
| AI 用 `burst_damage = 9999` 估值 | 改为读取共享 execute plan 和 save probability。 |
| 测试使用 `save_dc_mode = &"fixed"` | 改为现有合法值 `&"static"` 或 `&"caster_spell"`；正式 PWK 用 `&"caster_spell"`。 |

## 4. 文件级方案

### 4.1 Execute Content Rules

新增 `scripts/player/progression/battle_execute_content_rules.gd`：

```gdscript
class_name BattleExecuteContentRules
extends RefCounted

const EFFECT_TYPE_EXECUTE: StringName = &"execute"
const SAVE_TAG_EXECUTE: StringName = &"execute"
const DAMAGE_TAG_NEGATIVE_ENERGY: StringName = &"negative_energy"
const STATUS_SOUL_FRACTURE: StringName = &"soul_fracture"

const PARAM_THRESHOLD_MAX_HP_RATIO_PERCENT := "threshold_max_hp_ratio_percent"
const PARAM_NON_LETHAL_DAMAGE_RATIO_PERCENT := "non_lethal_damage_ratio_percent"
const PARAM_BOSS_NON_LETHAL_DAMAGE_RATIO_PERCENT := "boss_non_lethal_damage_ratio_percent"
const PARAM_SOUL_FRACTURE_DURATION_TU := "soul_fracture_duration_tu"
const PARAM_HEAL_MULTIPLIER_PERCENT := "heal_multiplier_percent"
const PARAM_SHIELD_GAIN_MULTIPLIER_PERCENT := "shield_gain_multiplier_percent"

const REQUIRED_PARAM_TYPES := {
	PARAM_THRESHOLD_MAX_HP_RATIO_PERCENT: TYPE_INT,
	PARAM_NON_LETHAL_DAMAGE_RATIO_PERCENT: TYPE_INT,
	PARAM_BOSS_NON_LETHAL_DAMAGE_RATIO_PERCENT: TYPE_INT,
	PARAM_SOUL_FRACTURE_DURATION_TU: TYPE_INT,
	PARAM_HEAL_MULTIPLIER_PERCENT: TYPE_INT,
	PARAM_SHIELD_GAIN_MULTIPLIER_PERCENT: TYPE_INT,
}
```

Rules:

- `SkillContentRegistry`、`BattleExecutionRules`、`BattleDamageResolver`、`BattleAiScoreService` 都引用这里的常量。
- `BattleSaveContentRules.SAVE_TAG_EXECUTE` 直接引用或镜像 `BattleExecuteContentRules.SAVE_TAG_EXECUTE`，并加入 `VALID_SAVE_TAGS`。
- `BattleSaveResolver` 同步 mirror 常量。
- 不把 execute 常量散落在多个文件里硬编码。

### 4.2 Save Tag

修改 `scripts/player/progression/battle_save_content_rules.gd`：

```gdscript
const BATTLE_EXECUTE_CONTENT_RULES = preload("res://scripts/player/progression/battle_execute_content_rules.gd")

const SAVE_TAG_EXECUTE: StringName = BATTLE_EXECUTE_CONTENT_RULES.SAVE_TAG_EXECUTE

const VALID_SAVE_TAGS := {
	# existing tags...
	SAVE_TAG_EXECUTE: true,
}
```

修改 `scripts/systems/battle/rules/battle_save_resolver.gd`：

```gdscript
const SAVE_TAG_EXECUTE: StringName = BATTLE_SAVE_CONTENT_RULES.SAVE_TAG_EXECUTE
```

现有 `_collect_save_tag_state()` 已能识别：

- `&"execute"`
- `&"execute_advantage"`
- `&"execute_disadvantage"`
- `&"execute_immunity"`

回归必须证明：

- `magic_advantage` 不影响 `execute`。
- `execute_immunity` 只阻止 Stage 2，不阻止 Stage 1、非致命伤害或 `soul_fracture`。

### 4.3 Enemy Target Rank

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
	match ProgressionDataUtils.to_string_name(target_rank):
		EnemyTemplateDef.TARGET_RANK_BOSS:
			snapshot.set_value(&"boss_target", 1)
			snapshot.set_value(&"fortune_mark_target", 2)
		EnemyTemplateDef.TARGET_RANK_ELITE:
			snapshot.set_value(&"boss_target", 0)
			snapshot.set_value(&"fortune_mark_target", 1)
		EnemyTemplateDef.TARGET_RANK_NORMAL:
			snapshot.set_value(&"boss_target", 0)
			snapshot.set_value(&"fortune_mark_target", 0)
		_:
			push_error("Invalid enemy target_rank reached runtime: %s" % String(target_rank))
```

Important boundary:

- `fortune_mark_target` 仍可供 Fate / Fortuna 系统使用。
- PWK Boss 判断只读 `boss_target`。
- 未来如果同一个模板需要在不同 roster 中有不同 rank，必须升级 roster schema 增加 rank override；不要复制隐藏字段，不要靠 `attribute_overrides` 覆盖。

### 4.4 Execute Schema

修改 `scripts/player/progression/skill_content_registry.gd`：

- `VALID_EFFECT_TYPES` 加入 `BattleExecuteContentRules.EFFECT_TYPE_EXECUTE`。
- `_append_effect_validation_errors()` 中为 execute 调用 `_append_execute_effect_validation_errors()`。
- `_append_combat_profile_validation_errors()` 增加 profile 级门禁：只要 profile 或任一 cast variant 包含 execute，该技能必须满足单体协议。
- `special_resolution_profile_id != &""` 的技能不得携带 executable execute effect。

Execute 单体协议：

| 字段 | 必须值 |
|---|---|
| `combat_profile.target_mode` | `&"unit"` |
| `combat_profile.target_team_filter` | `&"enemy"` 或 `&"hostile"` |
| `combat_profile.target_selection_mode` | `&"single_unit"` |
| `combat_profile.min_target_count` | `1` |
| `combat_profile.max_target_count` | `1` |
| `combat_profile.allow_repeat_target` | `false` |
| `combat_profile.area_pattern` | `&"single"` |
| `combat_profile.area_value` | `0` |

Cast variant 中出现 execute 时：

- `BattleSkillResolutionRules.get_cast_variant_target_mode(skill_def, cast_variant) == &"unit"`。
- `CombatCastVariantDef.required_coord_count` 不参与 execute 目标数量判定。
- 不检查 cast variant 上不存在的 `target_team_filter / target_selection_mode / min_target_count / max_target_count` 字段。
- 不允许 ground variant 或 multi-unit owning profile 携带 execute。

Execute effect 校验：

- `effect_def.effect_target_team_filter` 只能是空、`&"enemy"` 或 `&"hostile"`。
- `effect_def.save_dc_mode == BattleSaveContentRules.SAVE_DC_MODE_CASTER_SPELL`。
- `effect_def.save_dc == 0`。
- `effect_def.save_dc_source_ability == UnitBaseAttributes.INTELLIGENCE`。
- `effect_def.save_ability == UnitBaseAttributes.WILLPOWER`。
- `effect_def.save_tag == BattleExecuteContentRules.SAVE_TAG_EXECUTE`。
- `effect_def.damage_tag == BattleExecuteContentRules.DAMAGE_TAG_NEGATIVE_ENERGY`。
- `effect_def.save_partial_on_success == false`。
- `trigger_event == &""`，`trigger_condition == &""`。
- `passive_effect_defs` 不允许出现 execute。

Params exact set:

```gdscript
{
	"threshold_max_hp_ratio_percent": int,
	"non_lethal_damage_ratio_percent": int,
	"boss_non_lethal_damage_ratio_percent": int,
	"soul_fracture_duration_tu": int,
	"heal_multiplier_percent": int,
	"shield_gain_multiplier_percent": int,
}
```

Strict validation:

- 必须逐项检查 `typeof(value) == TYPE_INT`；不接受字符串、浮点、布尔转换。
- 不允许缺字段。
- 不允许额外字段。
- 所有 ratio / multiplier 必须在 `0..100`。
- `soul_fracture_duration_tu` 必须 `> 0` 且能被 5 整除。

Explicitly rejected params:

- `burst_damage`
- `finisher_damage`
- `death_protection_policy`
- `apply_status_id`
- `damage_tag`
- `shield_absorption_percent`
- `boss_non_lethal_damage_max_hp_ratio_percent`

Runtime safety gate:

- `BattleSkillResolutionRules.is_unit_effect()` 可以把 execute 视为 unit effect。
- `BattleSkillResolutionRules.collect_ground_unit_effect_defs()` 不应静默执行 execute。
- `BattleSkillExecutionOrchestrator._handle_ground_skill_command()` 必须在 cost 消耗前检查 ground effect defs；若发现 execute，记录错误日志并返回 `false`。
- 不允许用“跳过 execute effect”当兼容处理；非法资源应被 schema 拒绝，非法运行时构造应明确失败。

### 4.5 Execute Plan

新增共享纯计算 plan。可以先放在 `BattleExecutionRules`，后续若出现第二个 staged lethal effect 再拆独立 resolver。

`BattleExecutionRules` 边界：

- 只放无副作用公式和 execute plan 构建。
- 无 RNG。
- 不改 unit。
- 不读 UI/runtime facade。
- 不读取或修改 status collection。

最终 API：

```gdscript
class_name BattleExecutionRules
extends RefCounted

const ATTRIBUTE_SERVICE_SCRIPT = preload("res://scripts/systems/attributes/attribute_service.gd")
const BATTLE_EXECUTE_CONTENT_RULES = preload("res://scripts/player/progression/battle_execute_content_rules.gd")

const BRANCH_LOW_HP: StringName = &"low_hp"
const BRANCH_HIGH_HP_NORMAL: StringName = &"high_hp_normal"
const BRANCH_HIGH_HP_BOSS: StringName = &"high_hp_boss"

static func get_max_hp(unit: BattleUnitState) -> int:
	if unit == null or unit.attribute_snapshot == null:
		return 0
	return maxi(int(unit.attribute_snapshot.get_value(ATTRIBUTE_SERVICE_SCRIPT.HP_MAX)), 0)

static func is_boss_target(unit_state: BattleUnitState) -> bool:
	return unit_state != null \
		and unit_state.attribute_snapshot != null \
		and int(unit_state.attribute_snapshot.get_value(&"boss_target")) > 0

static func resolve_threshold(target_unit: BattleUnitState, params: Dictionary) -> int:
	var target_max_hp := get_max_hp(target_unit)
	var ratio := clampi(int(params[BATTLE_EXECUTE_CONTENT_RULES.PARAM_THRESHOLD_MAX_HP_RATIO_PERCENT]), 0, 100)
	return maxi(target_max_hp * ratio / 100, 0)

static func build_execute_plan(target_unit: BattleUnitState, params: Dictionary) -> Dictionary:
	var current_hp := maxi(int(target_unit.current_hp) if target_unit != null else 0, 0)
	var max_hp := get_max_hp(target_unit)
	var threshold := resolve_threshold(target_unit, params)
	var is_boss := is_boss_target(target_unit)
	var branch := BRANCH_LOW_HP
	var stage1_damage := 0
	var non_lethal_damage := 0
	if current_hp <= threshold:
		stage1_damage = maxi(current_hp - 1, 0)
	elif is_boss:
		branch = BRANCH_HIGH_HP_BOSS
		non_lethal_damage = maxi(max_hp * int(params[BATTLE_EXECUTE_CONTENT_RULES.PARAM_BOSS_NON_LETHAL_DAMAGE_RATIO_PERCENT]) / 100, 1)
	else:
		branch = BRANCH_HIGH_HP_NORMAL
		non_lethal_damage = maxi(threshold * int(params[BATTLE_EXECUTE_CONTENT_RULES.PARAM_NON_LETHAL_DAMAGE_RATIO_PERCENT]) / 100, 1)
	return {
		"branch": branch,
		"current_hp": current_hp,
		"max_hp": max_hp,
		"threshold": threshold,
		"is_boss": is_boss,
		"stage1_damage": stage1_damage,
		"stage2_damage": 1,
		"non_lethal_damage": mini(non_lethal_damage, maxi(current_hp - 1, 0)),
		"min_hp_after_stage1": 1,
		"min_hp_after_non_lethal": 1,
		"bypass_shield": true,
		"soul_fracture_params": {
			"heal_multiplier_percent": int(params[BATTLE_EXECUTE_CONTENT_RULES.PARAM_HEAL_MULTIPLIER_PERCENT]),
			"shield_gain_multiplier_percent": int(params[BATTLE_EXECUTE_CONTENT_RULES.PARAM_SHIELD_GAIN_MULTIPLIER_PERCENT]),
			"duration_tu": int(params[BATTLE_EXECUTE_CONTENT_RULES.PARAM_SOUL_FRACTURE_DURATION_TU]),
		},
	}
```

AI 和 runtime 都必须消费 `build_execute_plan()`，不能各自重写 low HP / Boss / non-lethal 分支。

### 4.6 BattleDamageResolver

修改 `scripts/systems/battle/rules/battle_damage_resolver.gd`：

- `resolve_effects()` 中只保留一行分发：`&"execute": result = _apply_execute_effect(...)`。
- `_apply_execute_effect()` 负责读取 save、execute plan，并调用现有 `_apply_damage_to_target()`。
- 删除当前旧分支里的 `shield_absorption_percent`、`params.damage_tag`、`soul_fracture_status` 嵌套参数读取。
- execute 不调用 `_resolve_damage_outcome()`，不进入通用 mitigation。

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
}
```

Runtime branch:

1. Resolve save once through `BattleSaveResolver.resolve_save()`.
2. Build plan through `BattleExecutionRules.build_execute_plan(target_unit, effect_def.params)`.
3. If `plan.branch == &"low_hp"`:
   - Stage 1 applies `plan.stage1_damage`, `min_hp_after_damage = 1`, `bypass_shield = true`.
   - Stage 2 only if save did not succeed, save is not immune, and `target_unit.current_hp <= 1`.
   - Stage 2 applies fixed `plan.stage2_damage = 1`, `min_hp_after_damage = 0`, `bypass_shield = true`.
   - Stage 2 always uses standard fatal trait / `death_ward` / `last_stand`; no bypass policy.
4. If high HP normal or high HP Boss:
   - Apply `plan.non_lethal_damage`, `min_hp_after_damage = 1`, `bypass_shield = true`.
5. If `target_unit.current_hp > 0`, apply clean `soul_fracture` status.

Damage outcome shape:

```gdscript
{
	"resolved_damage": damage,
	"damage_tag": effect_def.damage_tag,
	"bypass_shield": true,
	"min_hp_after_damage": min_hp,
	"execute_stage": stage_id,
}
```

`_coerce_damage_outcome()` only adds:

```gdscript
if not outcome.has("bypass_shield"):
	outcome["bypass_shield"] = false
if not outcome.has("min_hp_after_damage"):
	outcome["min_hp_after_damage"] = 0
```

Do not add `shield_absorption_percent`, `true_damage`, `bypass_mitigation`, or `death_protection_policy`.

`_apply_damage_to_target()` must return actual HP loss:

- `damage` / `hp_damage` must be actual HP lost after non-lethal clamp.
- Stage 1 must not report 9999 or any theoretical overkill value.
- last stand mastery and fatal hooks must not trigger from Stage 1 non-lethal clamp.
- `bypass_shield = true` must leave `current_shield_hp`, `shield_max_hp`, `shield_duration`, and `shield_family` unchanged.

Clean status builder:

```gdscript
func _build_soul_fracture_effect(plan: Dictionary) -> CombatEffectDef:
	var status_params: Dictionary = plan.get("soul_fracture_params", {})
	var status_effect := CombatEffectDef.new()
	status_effect.effect_type = &"status"
	status_effect.status_id = BattleExecuteContentRules.STATUS_SOUL_FRACTURE
	status_effect.duration_tu = int(status_params["duration_tu"])
	status_effect.params = {
		"heal_multiplier_percent": int(status_params["heal_multiplier_percent"]),
		"shield_gain_multiplier_percent": int(status_params["shield_gain_multiplier_percent"]),
		"source_effect_type": "execute",
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

新增 `scripts/systems/battle/rules/battle_status_modifier_rules.gd`：

```gdscript
class_name BattleStatusModifierRules
extends RefCounted

static func resolve_min_percent_param(
	target_unit: BattleUnitState,
	param_name: String,
	base_percent: int = 100
) -> int:
	var result := base_percent
	if target_unit == null:
		return result
	for status_key in target_unit.status_effects.keys():
		var entry = target_unit.get_status_effect(ProgressionDataUtils.to_string_name(status_key))
		if entry == null or entry.params == null:
			continue
		if not entry.params.has(param_name):
			continue
		var raw_value = entry.params[param_name]
		if typeof(raw_value) != TYPE_INT:
			continue
		result = mini(result, clampi(int(raw_value), 0, 100))
	return result
```

Use sites:

- `BattleDamageResolver` heal branch applies `heal_multiplier_percent`.
- `BattleShieldService._apply_shield_effect_to_target()` applies `shield_gain_multiplier_percent`.

Rules:

- Apply after dice/static amount is resolved.
- Apply before HP cap or shield replace/merge comparison.
- If original amount is `> 0` and multiplier is `> 0`, final amount is at least 1.
- Do not affect `heal_fatal` in this slice.
- Do not retroactively reduce existing shields.

Tests must update `tests/battle_runtime/rules/run_battle_rule_status_param_schema_regression.gd` or equivalent schema runner so `heal_multiplier_percent` and `shield_gain_multiplier_percent` have strict int/range coverage.

### 4.8 Dispatch

Modify:

- `scripts/systems/battle/rules/battle_skill_resolution_rules.gd`
- `scripts/systems/battle/runtime/battle_skill_execution_orchestrator.gd`

Rules:

- Add execute wherever single-target unit effects are accepted.
- Do not add `skill_id == &"mage_power_word_kill"` branches.
- Ground path must reject execute before cost is consumed.
- `special_resolution_profile_id != &""` skills cannot carry execute effects.

### 4.9 AI

Modify `scripts/systems/battle/ai/battle_ai_score_service.gd`:

- `_is_damage_skill()` treats execute as damage.
- `_build_target_effect_metrics()` calls `_estimate_execute_for_target_result()`.
- `_estimate_execute_for_target_result()` consumes `BattleExecutionRules.build_execute_plan()`.
- Do not recompute threshold/Boss/non-lethal branches outside the plan.

AI result contract:

```gdscript
{
	"damage": expected_damage,
	"kill_probability_basis_points": kill_bps,
	"expected_remaining_hp": expected_remaining_hp,
	"save_estimates": [save_estimate],
}
```

AI semantics:

- Low HP Stage 1 expected damage is `max(current_hp - 1, 0)`.
- Stage 2 value is probability-based.
- `execute_immunity` makes Stage 2 kill probability `0`, but Stage 1 and `soul_fracture` remain valid.
- High HP branches return non-lethal expected damage clamped to at most `current_hp - 1`.

Save estimate helper may reuse existing `_build_damage_save_estimate(..., 0, skill_id)` for probability fields, but must not introduce a second save probability formula.

Modify `scripts/systems/battle/ai/battle_ai_action_assembler.gd`:

- `_is_offensive_effect()` treats execute like damage for enemy skills.

### 4.10 Resource

Add `data/configs/skills/mage_power_word_kill.tres`.

Resource skeleton:

```ini
[gd_resource type="Resource" script_class="SkillDef" format=3]

[ext_resource type="Script" path="res://scripts/player/progression/combat_effect_def.gd" id="1_effect"]
[ext_resource type="Script" path="res://scripts/player/progression/combat_skill_def.gd" id="2_combat"]
[ext_resource type="Script" path="res://scripts/player/progression/skill_def.gd" id="3_skill"]

[sub_resource type="Resource" id="execute"]
script = ExtResource("1_effect")
effect_type = &"execute"
effect_target_team_filter = &"enemy"
damage_tag = &"negative_energy"
save_dc_mode = &"caster_spell"
save_dc_source_ability = &"intelligence"
save_ability = &"willpower"
save_tag = &"execute"
params = {
"threshold_max_hp_ratio_percent": 50,
"non_lethal_damage_ratio_percent": 30,
"boss_non_lethal_damage_ratio_percent": 30,
"soul_fracture_duration_tu": 120,
"heal_multiplier_percent": 75,
"shield_gain_multiplier_percent": 75
}

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
mp_cost = 80
cooldown_tu = 120
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
description = "以死亡律令撕裂单个敌人的生命。低血目标被压到濒死并进行意志豁免；失败后进入标准死亡保护链。高血目标承受非致命负能量压制。"
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

| Runner | Required assertions |
|---|---|
| `tests/battle_runtime/runtime/run_battle_execute_effect_regression.gd` | Replace old assertions. Remove `save_dc_mode=&"fixed"`, `save_tag=&"magic"`, `shield_absorption_percent`, `boss_non_lethal_damage_max_hp_ratio_percent`, “Boss never lethal”. Cover low HP save success clamps to 1, low HP save fail kills through Stage 2, high HP normal non-lethal, high HP Boss non-lethal, low HP Boss can die without execute immunity, execute immunity blocks only Stage 2, shield bypass leaves shield unchanged, `damage_events` reports actual HP loss. |
| `tests/battle_runtime/runtime/run_battle_save_resolver_regression.gd` | `execute_advantage`, `execute_disadvantage`, `execute_immunity`; `magic_advantage` does not affect execute. |
| `tests/battle_runtime/rules/run_status_effect_semantics_regression.gd` | `soul_fracture` harmful, cleansable, dispellable harmful, refresh, reduces normal heal and shield gain, does not affect `heal_fatal`. |
| `tests/battle_runtime/rules/run_battle_rule_status_param_schema_regression.gd` | `heal_multiplier_percent` and `shield_gain_multiplier_percent` strict int/range validation. |
| `tests/battle_runtime/ai/run_battle_ai_score_save_probability_regression.gd` | Stage 2 value changes with save probability; execute immunity makes Stage 2 kill probability 0 but does not remove Stage 1 / non-lethal value. |
| `tests/battle_runtime/ai/run_battle_ai_action_assembler_regression.gd` | New runner. execute is offensive and can generate enemy-target actions. |
| `tests/battle_runtime/runtime/run_wild_encounter_regression.gd` | target rank projects normal/elite/boss into `boss_target`; `fortune_mark_target` may also be projected but PWK tests must assert Boss reads `boss_target`. |
| `tests/progression/schema/run_skill_schema_regression.gd` | execute schema accepts PWK resource and rejects magic save tag, wrong damage tag, passive execute, triggered execute, special-profile execute, ground/multi execute, missing/extra execute params, non-int params. |
| `tests/runtime/validation/run_resource_validation_regression.gd` | every formal enemy template has explicit valid `target_rank`; `boss_target`/`fortune_mark_target` in `attribute_overrides` is rejected. |

Focused command set:

```bash
godot --headless --script tests/progression/schema/run_skill_schema_regression.gd
godot --headless --script tests/runtime/validation/run_resource_validation_regression.gd
godot --headless --script tests/battle_runtime/runtime/run_battle_execute_effect_regression.gd
godot --headless --script tests/battle_runtime/runtime/run_battle_save_resolver_regression.gd
godot --headless --script tests/battle_runtime/rules/run_status_effect_semantics_regression.gd
godot --headless --script tests/battle_runtime/rules/run_battle_rule_status_param_schema_regression.gd
godot --headless --script tests/battle_runtime/ai/run_battle_ai_score_save_probability_regression.gd
godot --headless --script tests/battle_runtime/ai/run_battle_ai_action_assembler_regression.gd
godot --headless --script tests/battle_runtime/ai/run_battle_runtime_ai_regression.gd
godot --headless --script tests/battle_runtime/runtime/run_wild_encounter_regression.gd
```

Do not include battle simulation or balance runners unless explicitly requested.

## 6. Implementation Order

1. Remove conflicting partial execute implementation and old test expectations.
2. Add `BattleExecuteContentRules`.
3. Add `SAVE_TAG_EXECUTE` to save content/resolver.
4. Make `EnemyTemplateDef.target_rank` required and update all formal enemy templates.
5. Project target rank in `EncounterRosterBuilder`; PWK Boss logic reads only `boss_target`.
6. Add strict execute schema validation and single-target/special-profile gates.
7. Finalize `BattleExecutionRules` as pure execute formula + plan builder.
8. Refactor `BattleDamageResolver` execute branch into `_apply_execute_effect()`.
9. Fix `_apply_damage_to_target()` so reported damage is actual HP loss after clamp.
10. Add `BattleStatusModifierRules` and wire heal/shield multipliers.
11. Register `soul_fracture` status semantics.
12. Add dispatch support and ground-path pre-cost rejection.
13. Add AI execute estimation using shared plan.
14. Add `mage_power_word_kill.tres`.
15. Add/update regressions and run focused command set.

## 7. Architecture Boundaries

Keep ownership narrow:

- Content constants live in `BattleExecuteContentRules`.
- Enemy rank truth lives in `EnemyTemplateDef.target_rank`.
- Enemy rank projection lives in `EncounterRosterBuilder`.
- Execute formula and plan live in `BattleExecutionRules`.
- Effect mutation lives in `BattleDamageResolver`.
- Status multiplier reading lives in `BattleStatusModifierRules`.
- Shield amount modification lives in `BattleShieldService`.
- AI estimation lives in `BattleAiScoreService`.

Do not modify:

- `GameRuntimeFacade`, `WorldMapRuntimeProxy`, or `WorldMapSystem`.
- Save payload versioning or `SaveSerializer`.
- UI scene structure.
- Meteor Swarm special profile registry.

`docs/design/project_context_units.md` does not need to change for this document-only edit. When code is implemented, update CU-15, CU-16, CU-19 and CU-20 notes if runtime relationships or recommended read sets change.
