# 律令死亡（Power Word, Kill）v3.4 共识实现草案

> **文档性质**：经子代理攻击性审查后的实现共识。本文已经收束到可编码契约；实现前仍需按文件清单重新读对应源码，落地后以 headless 回归为准。
> **当前版本**：v3.4
> **最后更新**：2026-05-12
> **取代范围**：全文取代 v2.0、v3.0、v3.1、v3.2、v3.3 及中间审查草案。

---

## 一、共识结论

| 项 | 结论 | 落地要求 |
|----|------|----------|
| effect type | 正式新增 `&"execute"` | 通用效果类型，不做 `skill_id == mage_power_word_kill` 特判 |
| save tag | 正式新增 `&"execute"` | `BattleSaveContentRules` 与 `BattleSaveResolver` 都要有常量；不复用 `&"magic"` |
| damage tag | 首版 execute 固定 `&"negative_energy"` | 拒绝 `&"magic"`、`&"true"`、`&"execute"` 及其他 damage tag |
| Boss / Elite 身份 | 正式升级 `EnemyTemplateDef.target_rank` | 资源必填 `normal / elite / boss`，由 `EncounterRosterBuilder` 投影运行时属性 |
| Boss 规则 | Boss 不天然免疫致死阶段 | 低血 Boss 可以被 PWK 处决；只有显式 `execute_immunity` / 即死免疫特性才阻止 Stage 2 |
| death protection | 默认不绕过 | Stage 2 走标准 fatal trait / `death_ward` / `last_stand`；只有 `death_protection_policy = "bypass"` 才能绕过 |
| 非致命伤害 | 普通高血目标 `threshold * 30%`；Boss `max_hp * 30%` | 后期敌人和 Boss 都不使用 50% 压制 |
| 护盾 | execute 无视护盾 | 只加 `bypass_shield`，不做护盾效率百分比 |
| 抗性/减伤 | execute 不走通用 damage mitigation | 不实现通用 `bypass_mitigation` 字段；execute 构造的 `resolved_damage` 已是最终数值 |
| `soul_fracture` | 有害、可驱散、刷新时长 | 120 TU，治疗获取和护盾获取各降至 75% |
| 预览 | 首版不显示目标相关数值预览 | 不把 target-dependent execute 塞进 `BattleDamagePreviewRangeService`；只保证 UI/执行不崩 |

---

## 二、技能概述

| 参数 | 值 |
|------|-----|
| 技能 ID | `&"mage_power_word_kill"` |
| 显示名称 | 律令死亡 |
| 环位 | 9 环 |
| 射程 | 12 格 |
| 目标 | 单个敌方可见单位 |
| AP / MP | 1 AP / 80 MP |
| 冷却 | 120 TU |
| effect type | `&"execute"` |
| 豁免 | 意志豁免，对抗施法者法术 DC |
| save tag | `&"execute"` |
| damage tag | `&"negative_energy"` |

一句话机制：对低血目标先压到 1 HP，再按意志豁免或显式即死免疫决定是否进入标准致死链；对高血目标造成非致命负能量压制，并施加可驱散的灵魂裂痕。Boss 身份只改变高血非致命伤害公式，不自带即死免疫。

---

## 三、execute 运行时契约

### 3.1 阈值

首版 `mage_power_word_kill` 是 `max_level = 1`，不放等级加成，也不放会被 cap 吞掉的 ability bonus。阈值只由目标最大生命比例决定。

```gdscript
static func resolve_threshold(target_unit: BattleUnitState, params: Dictionary) -> int:
	var target_max_hp := _get_target_max_hp(target_unit)
	var ratio := clampi(int(params.get("threshold_max_hp_ratio_percent", 50)), 0, 100)
	return maxi(target_max_hp * ratio / 100, 0)
```

默认验算：

| 目标 | hp_max | threshold | 满血是否处决 |
|------|--------|-----------|--------------|
| wolf_shaman | 22 | 11 | 否 |
| wolf_raider | 26 | 13 | 否 |
| wolf_vanguard | 42 | 21 | 否 |

### 3.2 save 语义

execute effect 如果配置了 save，就在每次结算时解析一次 save，并把结果放进 `save_results`。save 只控制低血目标的 Stage 2，不影响 Stage 1、普通高血非致命伤害或高血 Boss 非致命伤害。

| 分支 | 是否解析 save | save 成功/免疫影响 | damage_event |
|------|---------------|--------------------|--------------|
| `current_hp <= threshold` | 是 | 阻止 Stage 2；Stage 1 仍发生 | Stage 1 总是生成；Stage 2 仅 save 失败生成 |
| 非 Boss，`current_hp > threshold` | 是 | 不改变非致命伤害 | 生成 1 个非致命事件 |
| Boss，`current_hp > threshold` | 是 | 不改变 Boss 非致命伤害 | 生成 1 个非致命事件 |

原因：save 结果要进入战报、AI 估值和 immunity 测试，但不能让高血目标通过豁免绕过 `soul_fracture` 与非致命压制。Boss 若低血，同样进入 Stage 1 / Stage 2；Boss 是否免疫即死只看显式 `execute_immunity` 或等价即死免疫特性。

### 3.3 低血目标双段结算

| 阶段 | 条件 | outcome | 死亡保护 |
|------|------|---------|----------|
| Stage 1 Fracture Burst | `current_hp <= threshold` | `burst_damage = 9999`，`min_hp_after_damage = 1`，`bypass_shield = true` | 不进入 fatal chain，不消耗 `death_ward` |
| Stage 2 Execution Finisher | Stage 1 后 `current_hp <= 1` 且 save 失败 | `finisher_damage = 1`，`min_hp_after_damage = 0`，`bypass_shield = true` | 默认走标准 fatal chain |

关键点：

- Stage 1 的聚合伤害是实际 HP 损失，不是 9999。
- Stage 1 必须在进入 fatal trait / `death_ward` / `last_stand` 前夹血到 `min_hp_after_damage`。
- Stage 2 只有 `min_hp_after_damage = 0`，因此才允许进入当前标准死亡保护链。
- Boss 身份不跳过本分支；低血 Boss 可以被 Stage 2 杀死，除非它有显式即死免疫。
- 若目标原本已经是 1 HP，Stage 1 的 `hp_damage` 可以是 0，但仍记录一次 execute damage event，便于战报解释“律令压制未再造成生命损失”。

### 3.4 普通高血非致命分支

```gdscript
static func resolve_non_boss_non_lethal_damage(target_unit: BattleUnitState, params: Dictionary) -> int:
	var threshold := resolve_threshold(target_unit, params)
	var ratio := clampi(int(params.get("non_lethal_damage_ratio_percent", 30)), 0, 100)
	return maxi(threshold * ratio / 100, 1)
```

该分支使用：

- `min_hp_after_damage = 1`
- `bypass_shield = true`
- 不触发 Stage 2
- 不消耗 `death_ward` / `last_stand`

### 3.5 高血 Boss 非致命分支

```gdscript
static func resolve_boss_non_lethal_damage(target_unit: BattleUnitState, params: Dictionary) -> int:
	var target_max_hp := _get_target_max_hp(target_unit)
	var ratio := clampi(int(params.get("boss_non_lethal_damage_ratio_percent", 30)), 0, 100)
	return maxi(target_max_hp * ratio / 100, 1)
```

Boss 只有在 `current_hp > threshold` 时进入本非致命分支：

- Boss rank 不提供即死免疫。
- 低血 Boss 走 3.3 双段结算。
- 不消耗 `death_ward` / `last_stand`。
- 无视护盾。
- 伤害夹到至少 1 HP。
- 若存活，施加 `soul_fracture`。

### 3.6 `_apply_execute_effect()` 返回契约

`BattleDamageResolver.resolve_effects()` 不应把 execute 拆成外层特殊聚合。新增内部 helper：

```gdscript
func _apply_execute_effect(source_unit, target_unit, effect_def, context: Dictionary) -> Dictionary:
	return {
		"applied": false,
		"damage": 0,
		"shield_absorbed": 0,
		"shield_broken": false,
		"damage_events": [],
		"save_results": [],
		"status_effect_ids": [],
	}
```

要求：

- `damage` 是实际 HP 损失合计。
- `shield_absorbed` 对 PWK 恒为 0。
- `damage_events` 包含每个执行阶段的 `_apply_damage_to_target()` 结果。
- `save_results` 只追加一次 save 解析结果。
- `status_effect_ids` 只追加干净 `soul_fracture` 状态的应用结果。
- `applied` 只要有伤害事件、状态成功应用或有效 save 结果即可为 true。

---

## 四、伤害 outcome 与护盾

execute 不调用 `_resolve_damage_outcome()`，因此不会进入通用抗性、减伤和固定减伤计算。`damage_tag = &"negative_energy"` 只作为内容语义、战报和后续状态交互标签，不参与 mitigation lookup。

execute 构造的伤害 outcome：

```gdscript
{
	"resolved_damage": damage,
	"damage_tag": &"negative_energy",
	"bypass_shield": true,
	"min_hp_after_damage": min_hp,
	"death_protection_policy": "standard",
}
```

`_coerce_damage_outcome()` 只补安全默认值：

```gdscript
if not outcome.has("bypass_shield"):
	outcome["bypass_shield"] = false
if not outcome.has("min_hp_after_damage"):
	outcome["min_hp_after_damage"] = 0
if not outcome.has("death_protection_policy"):
	outcome["death_protection_policy"] = "standard"
```

`_apply_damage_to_target()` 的新增顺序必须是：

1. 归一化 `resolved_damage`。
2. 若 `bypass_shield == false`，按旧逻辑吸收护盾；否则 `shield_absorbed = 0` 且不扣护盾。
3. 计算 `hp_damage`。
4. 若 `min_hp_after_damage > 0`，先把 `hp_damage` 夹到不会低于该 HP 的数值。
5. 只有 `min_hp_after_damage <= 0` 且 projected HP 小于等于 0 时，才进入 fatal trait / `death_ward` / `last_stand`。
6. 若 `death_protection_policy == "bypass"`，只有 Stage 2 可跳过标准死亡保护链；`mage_power_word_kill.tres` 首版必须写 `"standard"`。

伪代码：

```gdscript
var min_hp_after_damage := maxi(int(damage_outcome.get("min_hp_after_damage", 0)), 0)
var bypass_shield := bool(damage_outcome.get("bypass_shield", false))

if not bypass_shield and target_unit.has_shield():
	shield_absorbed = mini(normalized_damage, target_unit.current_shield_hp)
	# existing shield drain / break logic

var hp_damage := maxi(normalized_damage - shield_absorbed, 0)
if min_hp_after_damage > 0:
	hp_damage = mini(hp_damage, maxi(target_unit.current_hp - min_hp_after_damage, 0))

var projected_hp := target_unit.current_hp - hp_damage
if projected_hp <= 0 and min_hp_after_damage <= 0:
	# existing fatal chain, unless explicit death_protection_policy == "bypass"
```

---

## 五、`soul_fracture` 状态

新增状态 ID：

```gdscript
const STATUS_SOUL_FRACTURE: StringName = &"soul_fracture"
```

语义：

| 项 | 值 |
|----|----|
| harmful | 是 |
| dispellable harmful | 是 |
| cleansable harmful | 默认随 harmful 可 cleanse |
| stack mode | refresh |
| max stacks | 1 |
| duration | 120 TU |
| heal multiplier | 75% |
| shield gain multiplier | 75% |
| tick | 无 |

实现要求：

- `BattleStatusSemanticTable.is_harmful_status()` 包含 `STATUS_SOUL_FRACTURE`。
- `BattleStatusSemanticTable.is_dispellable_harmful_status()` 包含 `STATUS_SOUL_FRACTURE`。
- `BattleStatusSemanticTable.get_semantic()` 返回 refresh timeline 语义。
- execute 分支不要把原始 execute `effect_def.params` 直接交给 `_apply_status_effect()`。
- 应构造干净的运行时 `CombatEffectDef`：`effect_type=&"status"`、`status_id=&"soul_fracture"`、`duration_tu=120`，`params` 只保留倍率和来源元数据。

干净状态 params：

```gdscript
{
	"heal_multiplier_percent": 75,
	"shield_gain_multiplier_percent": 75,
	"source_skill_id": "mage_power_word_kill",
}
```

治疗倍率：

- `BattleDamageResolver` 的 `&"heal"` 分支读取目标有害状态中的 `heal_multiplier_percent`，多个状态取最小值。
- 对最终治疗量乘百分比后向下取整。
- 原治疗量大于 0 且倍率大于 0 时，结果至少为 1。
- 不影响 `heal_fatal`，除非未来显式新增配置和测试。

护盾获取倍率：

- `BattleShieldService` 在 `_resolve_shield_hp()` 后、替换/叠加比较前应用 `shield_gain_multiplier_percent`。
- 多个状态取最小值。
- 同样向下取整，正数倍率下至少保留 1 点。
- 不回溯减少目标已有护盾。

---

## 六、Boss / Elite 身份升级

### 6.1 内容真相源

在 `scripts/enemies/enemy_template_def.gd` 正式新增必填字段：

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

`target_rank` 默认空值，资源必须显式填写。这样新旧资源漏填会被 schema 抓住，不会静默当作 `normal`。

`EnemyTemplateDef.validate_schema()` 必须校验：

- `target_rank` 非空，且只能是 `normal / elite / boss`。
- `attribute_overrides` 不允许声明 `boss_target` 或 `fortune_mark_target`。
- `base_attribute_overrides` 仍只表达六维基础属性，不承载目标阶级。

### 6.2 首批正式模板标注

当前正式模板需要随实现一次性补齐：

| 模板 | target_rank |
|------|-------------|
| `mist_beast.tres` | `normal` |
| `mist_harrier.tres` | `normal` |
| `mist_weaver.tres` | `normal` |
| `wolf_alpha.tres` | `elite` |
| `wolf_pack.tres` | `normal` |
| `wolf_raider.tres` | `normal` |
| `wolf_shaman.tres` | `normal` |
| `wolf_vanguard.tres` | `normal` |

当前正式资源没有 Boss 模板，不为测试伪造正式 Boss。Boss 行为用测试夹具覆盖；未来新增 Boss 模板必须显式写 `target_rank = &"boss"`。

### 6.3 运行时投影

投影归 `scripts/systems/world/encounter_roster_builder.gd`，因为正式敌人从模板进入 `BattleUnitState.attribute_snapshot` 的桥在这里。不要把正式 Boss rank 注入放进 `BattleUnitFactory` fallback。

```gdscript
func _apply_enemy_target_rank(snapshot, target_rank: StringName) -> void:
	match ProgressionDataUtils.to_string_name(target_rank):
		&"boss":
			snapshot.set_value(&"fortune_mark_target", 2)
			snapshot.set_value(&"boss_target", 1)
		&"elite":
			snapshot.set_value(&"fortune_mark_target", 1)
			snapshot.set_value(&"boss_target", 0)
		&"normal":
			snapshot.set_value(&"fortune_mark_target", 0)
			snapshot.set_value(&"boss_target", 0)
		_:
			snapshot.set_value(&"fortune_mark_target", 0)
			snapshot.set_value(&"boss_target", 0)
```

调用顺序：先 `_apply_enemy_attribute_overrides(snapshot, template.attribute_overrides)`，再 `_apply_enemy_target_rank(snapshot, template.target_rank)`。这样 `target_rank` 是最终真相源。

### 6.4 规则读取

`BattleExecutionRules.is_boss_target(unit)` 不读取模板，只读取运行时快照，保持与特殊技能、loot、mastery 等目标阶级消费者一致：

```gdscript
static func is_boss_target(unit_state: BattleUnitState) -> bool:
	return unit_state != null \
		and unit_state.attribute_snapshot != null \
		and (
			int(unit_state.attribute_snapshot.get_value(&"boss_target")) > 0
			or int(unit_state.attribute_snapshot.get_value(&"fortune_mark_target")) > 1
		)
```

重要边界：`is_boss_target()` 只用于高血 Boss 的非致命伤害公式和目标阶级语义，不允许作为 execute 即死免疫判断。即死免疫必须来自 `BattleSaveResolver` 能识别的显式 execute immunity。

---

## 七、save tag 与 schema

### 7.1 新增 execute save tag

在 `scripts/player/progression/battle_save_content_rules.gd` 新增：

```gdscript
const SAVE_TAG_EXECUTE: StringName = &"execute"
```

并加入 `VALID_SAVE_TAGS`，不要加入 `CONTROL_SAVE_TAGS`。

在 `scripts/systems/battle/rules/battle_save_resolver.gd` 同步 mirror 常量：

```gdscript
const SAVE_TAG_EXECUTE: StringName = BattleSaveContentRules.SAVE_TAG_EXECUTE
```

原因：

- `&"magic"` 是来源/法术泛标签，已有种族和状态可能给它优势。
- `&"execute"` 是死亡律令独立防护轴，应允许 `execute_advantage`、`execute_disadvantage`、`execute_immunity` 独立配置。
- `gnome` 的 `magic` 优势不应自动影响 PWK。
- Boss 如果需要免疫 PWK 即死，应通过模板、特性、状态或装备投影出 `save_immunity_tags` 包含 `&"execute"`，而不是依赖 `target_rank = &"boss"`。

### 7.2 execute effect schema

`SkillContentRegistry` 需要：

- 把 `&"execute"` 加入 `VALID_EFFECT_TYPES`。
- 对 active `effect_defs`、cast variant `effect_defs`、`passive_effect_defs` 都跑 effect type 校验。
- 被动或触发式 execute 一律拒绝。

execute effect 的校验：

| 字段 | 规则 |
|------|------|
| `save_dc_mode` | 必须能产生 save DC，首版资源用 `&"caster_spell"` |
| `save_dc_source_ability` | 首版资源用 `&"intelligence"` |
| `save_ability` | 必须合法，首版资源用 `&"willpower"` |
| `save_tag` | 必须是 `&"execute"` |
| `damage_tag` | 必须是 `&"negative_energy"` |
| `save_partial_on_success` | 拒绝 |
| `trigger_event` / `trigger_condition` | 拒绝 |
| `passive_effect_defs` | 不允许出现 execute |

明确拒绝：

- `save_tag = &"magic"`
- `damage_tag = &"magic"`
- `damage_tag = &"true"`
- `damage_tag = &"execute"`
- `damage_tag = &"fire"` 等其他已存在但非首版语义的标签

---

## 八、AI 与预览

### 8.1 AI 接入点

需要改：

- `scripts/systems/battle/ai/battle_ai_score_service.gd`
- `scripts/systems/battle/ai/battle_ai_action_assembler.gd`

`BattleAiScoreService` 不新增一套旁路伪代码，直接在 `_build_target_effect_metrics()` / `_estimate_damage_for_target_result()` 接入 `effect_type == &"execute"`，并沿用现有返回结构里的 `damage` 与 `save_estimates`。

低血目标估值：

```gdscript
var stage1_damage := maxi(target_unit.current_hp - 1, 0)
var save_estimate := BattleSaveResolver.estimate_save_success_probability(...)
var failed_save_bps := int(save_estimate.get("failure_probability_basis_points", 10000))
if bool(save_estimate.get("immune", false)):
	failed_save_bps = 0
var stage2_expected := int(round(float(finisher_damage) * float(failed_save_bps) / 10000.0))
var expected_damage := stage1_damage + stage2_expected
```

要求：

- save immune 或高成功率必须降低 Stage 2 价值，不能无条件按 `current_hp` 当作必杀。
- `magic_advantage` 不影响 execute save probability。
- 低血 Boss 与低血普通目标共用 Stage 1 / Stage 2 概率估值；显式 execute immunity 会把 Stage 2 期望降为 0。
- 高血普通目标和高血 Boss 用各自非致命分支估值，并夹到最多 `current_hp - 1`。
- `_is_damage_skill()` 把 `&"execute"` 视为 damage skill，用于威胁范围和目标价值。
- `BattleAiActionAssembler._is_offensive_effect()` 把 `&"execute"` 视为 offensive effect。

### 8.2 数值预览

`BattleDamagePreviewRangeService` 当前没有目标参数。execute 的数值依赖目标当前 HP、max HP、Boss rank、execute immunity 和 shield bypass；把它伪装成 target-independent damage preview 会误导 UI。

首版结论：

- 不改 `BattleDamagePreviewRangeService`。
- execute 技能可以不显示数值 damage preview。
- 只要求技能选择、目标悬停和施放执行不崩。
- 后续若要最佳 UI，再单独做 target-aware preview service。

---

## 九、资源草案

核心 execute effect：

```ini
[sub_resource type="Resource" id="Resource_pwk_execute"]
script = ExtResource("3_effect")
effect_type = &"execute"
effect_target_team_filter = &"enemy"
damage_tag = &"negative_energy"
save_dc_mode = &"caster_spell"
save_dc_source_ability = &"intelligence"
save_ability = &"willpower"
save_tag = &"execute"
params = {
"burst_damage": 9999,
"finisher_damage": 1,
"death_protection_policy": "standard",
"threshold_max_hp_ratio_percent": 50,
"non_lethal_damage_ratio_percent": 30,
"boss_non_lethal_damage_ratio_percent": 30,
"apply_status_id": "soul_fracture",
"soul_fracture_duration_tu": 120,
"heal_multiplier_percent": 75,
"shield_gain_multiplier_percent": 75
}
```

Combat profile：

```ini
[sub_resource type="Resource" id="Resource_pwk_combat"]
script = ExtResource("4_combat")
skill_id = &"mage_power_word_kill"
target_mode = &"unit"
target_team_filter = &"enemy"
range_pattern = &"single"
range_value = 12
area_pattern = &"single"
area_value = 0
requires_los = true
ap_cost = 1
mp_cost = 80
cooldown_tu = 120
target_selection_mode = &"single_unit"
min_target_count = 1
max_target_count = 1
selection_order_mode = &"stable"
effect_defs = Array[ExtResource("3_effect")]([SubResource("Resource_pwk_execute")])
ai_tags = Array[StringName]([&"execute", &"single_target", &"finisher"])
delivery_categories = Array[StringName]([&"spell", &"necromancy"])
```

SkillDef 关键字段：

```ini
[resource]
script = ExtResource("5_skill")
skill_id = &"mage_power_word_kill"
display_name = "律令死亡"
icon_id = &"mage_power_word_kill"
description = "以死亡律令撕裂单个敌人的生命。低血量目标被压到濒死并进行意志豁免；失败后进入标准死亡保护链。高血量目标承受非致命负能量压制，Boss 仅在高血时使用专属非致命公式。"
max_level = 1
non_core_max_level = 1
mastery_curve = PackedInt32Array(1)
tags = Array[StringName]([&"mage", &"magic", &"necromancy", &"execution", &"single_target", &"output"])
growth_tier = &"legendary"
combat_profile = SubResource("Resource_pwk_combat")
```

---

## 十、文件清单

### 10.1 新建文件

| 文件 | 职责 |
|------|------|
| `scripts/systems/battle/rules/battle_execution_rules.gd` | execute 阈值、Boss 判定、非致命伤害计算、death policy 解析 |
| `data/configs/skills/mage_power_word_kill.tres` | 正式技能资源 |
| `tests/battle_runtime/rules/run_battle_execute_effect_regression.gd` | execute 核心结算回归 |

### 10.2 修改文件

| # | 文件 | 改动 |
|---|------|------|
| 1 | `scripts/enemies/enemy_template_def.gd` | 新增必填 `target_rank`、schema 校验、拒绝 rank 属性写入 `attribute_overrides` |
| 2 | `data/configs/enemies/templates/*.tres` | 全部显式补 `target_rank` |
| 3 | `scripts/systems/world/encounter_roster_builder.gd` | 投影 `target_rank -> fortune_mark_target / boss_target` |
| 4 | `scripts/player/progression/battle_save_content_rules.gd` | 新增 `SAVE_TAG_EXECUTE` 并加入 `VALID_SAVE_TAGS` |
| 5 | `scripts/systems/battle/rules/battle_save_resolver.gd` | 新增 mirror 常量，确保 probability estimator 识别 execute tag |
| 6 | `scripts/player/progression/skill_content_registry.gd` | 注册 `&"execute"`，校验 active/cast/passive effect defs |
| 7 | `scripts/systems/battle/rules/battle_skill_resolution_rules.gd` | `is_unit_effect()` 接受 execute |
| 8 | `scripts/systems/battle/runtime/battle_skill_execution_orchestrator.gd` | `_is_unit_effect()` 接受 execute |
| 9 | `scripts/systems/battle/rules/battle_damage_resolver.gd` | execute 分支、damage outcome clamp、shield bypass、heal multiplier |
| 10 | `scripts/systems/battle/runtime/battle_shield_service.gd` | shield gain multiplier |
| 11 | `scripts/systems/battle/rules/battle_status_semantic_table.gd` | 注册 `soul_fracture` harmful/dispellable/refresh 语义 |
| 12 | `scripts/systems/battle/ai/battle_ai_score_service.gd` | execute 估值、save probability、damage skill 识别 |
| 13 | `scripts/systems/battle/ai/battle_ai_action_assembler.gd` | execute 视为 offensive effect |

不做：

- 不复用 `save_tag = &"magic"`。
- 不新增 `damage_tag = &"true"` 或 `&"execute"`。
- 不实现通用 `bypass_mitigation` 字段。
- 不做 `shield_absorption_percent`。
- 不让 `BattleUnitFactory` 成为正式 Boss rank 注入路径。
- 不做旧资源兼容别名或 fallback migration。

---

## 十一、测试矩阵

### 11.1 必补/必改 runner

| Runner | 覆盖 |
|--------|------|
| `tests/battle_runtime/rules/run_battle_execute_effect_regression.gd` | 阈值、skill level 1 仍结算 execute、低血 save success/fail、death_ward 默认不绕过、显式 bypass fixture、高血 Boss 30%、低血 Boss 可被处决、Boss 显式 execute immunity 阻止 Stage 2、无视护盾、damage_events/save_results |
| `tests/progression/schema/run_battle_save_skill_schema_regression.gd` | `save_tag=execute` 通过；`save_tag=magic` 被拒绝；`damage_tag=true/execute/magic/fire` 被拒绝 |
| `tests/progression/schema/run_skill_schema_regression.gd` | `effect_type=execute` 注册；被动/触发式 execute 被拒绝 |
| `tests/battle_runtime/runtime/run_battle_save_resolver_regression.gd` | `execute_advantage/disadvantage/immunity` 生效；`magic_advantage` 不影响 execute |
| `tests/battle_runtime/rules/run_status_effect_semantics_regression.gd` | `soul_fracture` harmful、dispellable、refresh、治疗/护盾倍率 |
| `tests/battle_runtime/ai/run_battle_ai_score_save_probability_regression.gd` | AI 按 execute save probability 调整 Stage 2 估值 |
| `tests/battle_runtime/ai/run_battle_runtime_ai_regression.gd` | execute 进入伤害技能/威胁范围识别 |
| `tests/battle_runtime/ai/run_battle_ai_action_assembler_regression.gd` | execute effect 被 action assembler 视为 offensive |
| `tests/battle_runtime/runtime/run_wild_encounter_regression.gd` | `target_rank` normal/elite/boss 投影 |
| `tests/runtime/validation/run_resource_validation_regression.gd` | 正式 enemy templates 必填合法 `target_rank` |

### 11.2 关键断言

| 场景 | 断言 |
|------|------|
| 低血 + save success | Stage 1 压到 1 HP，shield 不变，`death_ward` 不消耗 |
| 低血 + execute immunity | `save_results` 显示 immune/success，Stage 2 不触发 |
| 低血 + save fail + 无 death protection | Stage 2 致死 |
| 低血 + save fail + `death_ward` | 默认进入标准死亡保护链，不绕过 |
| 低血 + save fail + 显式 bypass fixture | 跳过 `death_ward`，并有专门断言说明这是显式配置 |
| 低血 Boss + 无 execute immunity + save fail | Stage 2 进入标准死亡保护链，可以死亡 |
| 低血 Boss + execute immunity | `save_results` 显示 immune/success，Stage 2 不触发 |
| 目标只有 magic advantage | PWK 不获得 advantage |
| 高血普通目标 | 造成 `threshold * 30%`，夹到至少 1 HP |
| 高血 Boss | 造成 `max_hp * 30%`，夹到至少 1 HP |
| 目标有护盾 | `shield_absorbed = 0`，护盾值不变，HP 直接变化 |
| `soul_fracture` 被 dispel | 状态移除后治疗/护盾倍率恢复 |

---

## 十二、实现顺序

1. 升级 `EnemyTemplateDef.target_rank` 与所有正式 enemy templates。
2. 在 `EncounterRosterBuilder` 投影 target rank，并补 normal/elite/boss 测试。
3. 增加 `SAVE_TAG_EXECUTE` 和 `BattleSaveResolver` mirror。
4. 注册 `effect_type = &"execute"` 与 schema 校验，覆盖 passive/cast variants。
5. 新建 `BattleExecutionRules`。
6. 扩展 `BattleDamageResolver`：execute 分支、`min_hp_after_damage`、`bypass_shield`、death policy、heal multiplier。
7. 扩展 `BattleShieldService` shield gain multiplier。
8. 注册 `soul_fracture` 状态语义。
9. 扩展 skill resolution/orchestrator dispatch。
10. 扩展 AI 估值和 action assembler。
11. 新增 `mage_power_word_kill.tres`。
12. 新建/扩展回归 runner 并跑窄测试。

步骤 12 通过前，不应合并到主线。

---

## 十三、Project Context Units 影响

本次只更新设计讨论文档，尚未改 runtime 代码，`docs/design/project_context_units.md` 暂不需要更新。

实现本设计时会改变 CU-16（战斗规则层）、CU-20（敌方模板与 roster bridge）、CU-19（测试入口）和 CU-15（战斗 runtime dispatch）的推荐读集；代码落地同一任务中需要同步更新 `docs/design/project_context_units.md`。
