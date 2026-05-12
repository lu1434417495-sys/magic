# 怪影杀戮（Phantasmal Kill）落地实现方案

状态：第一轮对抗审查后修订版，可进入实现。本文把原讨论稿收敛为代码级方案，后续实现应按本文拆分提交，并在代码变更后根据影响更新 `docs/design/project_context_units.md`。

## 目标

`怪影杀戮` 是一个 9 环幻术/恐惧/精神系大范围处决法术：

| 项 | 规格 |
| --- | --- |
| 技能 ID | `mage_phantasmal_kill` |
| 目标模式 | 地面选点，影响范围内单位 |
| 射程 | 12 格 |
| 范围 | 7x7，使用现有 `square` 区域，`area_value = 3` |
| 目标队伍 | `any`，会影响敌我双方的有心智单位 |
| 豁免 | `willpower`，`save_tag = &"illusion"`，DC 使用施法者法术 DC |
| 伤害标签 | `psychic` |
| 核心效果 | 四级豁免，低生命阈值处决，高生命造成精神伤害和控制 |

不做 D&D 原版逐字复刻；本技能采用项目原创规则表达。不要添加旧 ID、旧字段、兼容别名或旧 payload/schema 支持，除非用户另行确认。

## 最终技术路线

采用“标准地面技能 + 新效果类型”的路线：

1. 新增 `CombatEffectDef.effect_type = &"graded_save_execute"`。
2. `mage_phantasmal_kill.tres` 走现有地面技能流程：目标校验、消耗、范围收集、预览、AI 候选枚举都继续使用现有 ground skill 管线。
3. `BattleSkillResolutionRules.is_unit_effect()` 必须把 `graded_save_execute` 识别为 unit effect，否则 ground preview 和执行收集不到该效果。
4. `BattleDamageResolver` 增加 `graded_save_execute` 解析分支，负责四级豁免、条件处决、精神伤害和状态附加。
5. 不新增 `special_resolution_profile_id`，不在 `BattleSkillExecutionOrchestrator` 里增加 `_handle_phantasmal_kill_command` 分支。

选择该路线的原因：

- 7x7 地面范围已经由 `BattleGridService` / `BattleGroundEffectService` 覆盖。
- 复用 `BattleGroundEffectService._apply_ground_unit_effects` 可以保留击杀后的掉落、成就、评分、战斗结束检查和清场链路。
- 避免特殊技能分支膨胀；`meteor_swarm` 是多阶段特殊 profile，`怪影杀戮` 的复杂点在单个 per-target effect 内。
- `effect_defs` 非空后，预览、AI、资源校验和内容验证都有可读输入，避免“空 effect_defs 但运行时硬编码”的不可见行为。

## 明确不采用的方案

| 方案 | 结论 | 原因 |
| --- | --- | --- |
| 直接在 orchestrator 中硬编码 `phantasmal_kill` | 不采用 | 会绕开通用地面效果和击杀提交链，容易漏掉掉落、成就、评分、战斗结束检查 |
| 直接把目标 `current_hp = 0` | 不采用 | 会绕开 `death_ward`、last stand、fatal trait、伤害事件和护盾/抗性路径 |
| 使用空 `effect_defs` + special profile | 不采用 | 本技能不需要 Meteor Swarm 级别的多阶段 profile，且会增加 AI/预览额外适配面 |
| 新增 `mental` 或 `psychic` 豁免标签 | 不采用 | 当前 `BattleSaveContentRules` 已有 `illusion`、`charm`、`frightened` 等标签；先复用现有心智/幻术免疫表达 |
| 为旧技能 ID 增加 alias | 不采用 | 兼容策略禁止未确认的 legacy alias |

## 行为规格

### 有心智单位判定

最小实现不新增“mind”生物学字段。目标是否受影响由免疫标签决定：

- 若目标对 `illusion` 免疫，则 `怪影杀戮` 完全无效。
- 若内容作者需要表达“无心智”单位，应在单位或敌人模板上配置 `illusion_immunity`，可同时配置 `charm_immunity`、`frightened_immunity` 以服务其他技能。
- 必须补齐 `EnemyTemplateDef.save_advantage_tags` 到 `EncounterRosterBuilder` 的投影；当前模板字段没有该能力，不应把它当作“确认即可”的可选项。

注意：`BattleSaveResolver.resolve_save()` 对免疫结果会返回 `immune = true`、`success = true`、`natural_roll = 0`。`graded_save_execute` 必须先判断 `immune`，不能把 `natural_roll = 0` 当作大失败。

### 豁免分级

每个目标只解析一次豁免，分级规则固定如下：

| 分级 | 判定 |
| --- | --- |
| `immune` | `save_result.immune == true` |
| `critical_success` | 非免疫且 `natural_roll >= 20` |
| `critical_failure` | 非免疫且 `natural_roll <= 1` |
| `success` | 非免疫、非大成功/大失败、`success == true` |
| `failure` | 非免疫、非大成功/大失败、`success == false` |

### 分级效果

| 分级 | 效果 |
| --- | --- |
| `immune` | 无效果，不造成伤害，不附加状态 |
| `critical_success` | 完全无效 |
| `success` | 附加 `aftershock` 1 轮：不能反击，不能使用守护反应 |
| `failure` | 若当前 HP <= `max(50, max_hp * 25%)`，执行处决；否则造成 `6d6 psychic`，附加 `frightened` 2 轮和 `reaction_lock` 1 轮 |
| `critical_failure` | 若当前 HP <= `max_hp * 35%`，执行处决；否则造成 `10d6 psychic`，附加 `frightened` 3 轮和 `stunned` 1 轮 |

处决不是直接写 HP，而是通过 `BattleDamageResolver.apply_direct_damage_to_target()` 或同等内部提交路径造成“当前 HP 数值”的致命精神伤害：

- `damage_tag = &"psychic"`
- `bypass_shield = true`
- `min_hp_after_damage = 0`
- 保留 `death_ward`、last stand、fatal trait、伤害事件记录和后续 `BattleGroundEffectService` 击杀提交链

非处决伤害走现有伤害解析路径，保留精神抗性、免疫、护盾吸收、伤害事件和预览统计。

## 数据配置

新增文件：

- `data/configs/skills/mage_phantasmal_kill.tres`

核心字段：

```gdscript
skill_id = &"mage_phantasmal_kill"
display_name = "怪影杀戮"
icon_id = &"mage_phantasmal_kill"
skill_type = &"active"
max_level = 7
non_core_max_level = 5
mastery_curve = PackedInt32Array(400, 1000, 2200, 4000, 6500, 9500, 13000)
tags = Array[StringName]([&"mage", &"magic", &"illusion", &"fear", &"psychic", &"output", &"control", &"ultimate"])
growth_tier = &"advanced"
attribute_growth_progress = {
	"intelligence": 110,
	"perception": 20,
	"willpower": 50
}
level_description_template = "射程{range}，7x7 幻术处决范围，失败阈值处决或造成精神伤害并附加恐惧/反应封锁。消耗{ap}AP/{mp}法力/{aura}斗气，冷却{cooldown}TU"
level_description_configs = {
	"0": {"range": "12", "area": "7x7", "ap": "3", "mp": "120", "aura": "2", "cooldown": "20", "dmg": "6D6/10D6", "threshold": "50或25%/35%"},
	"1": {"range": "12", "area": "7x7", "ap": "3", "mp": "120", "aura": "2", "cooldown": "20", "dmg": "6D6/10D6", "threshold": "50或25%/35%"},
	"2": {"range": "12", "area": "7x7", "ap": "3", "mp": "120", "aura": "2", "cooldown": "20", "dmg": "6D6/10D6", "threshold": "50或25%/35%"},
	"3": {"range": "12", "area": "7x7", "ap": "3", "mp": "120", "aura": "2", "cooldown": "20", "dmg": "6D6/10D6", "threshold": "50或25%/35%"},
	"4": {"range": "12", "area": "7x7", "ap": "3", "mp": "120", "aura": "2", "cooldown": "20", "dmg": "6D6/10D6", "threshold": "50或25%/35%"},
	"5": {"range": "12", "area": "7x7", "ap": "3", "mp": "120", "aura": "2", "cooldown": "20", "dmg": "6D6/10D6", "threshold": "50或25%/35%"},
	"6": {"range": "12", "area": "7x7", "ap": "3", "mp": "120", "aura": "2", "cooldown": "20", "dmg": "6D6/10D6", "threshold": "50或25%/35%"},
	"7": {"range": "12", "area": "7x7", "ap": "3", "mp": "120", "aura": "2", "cooldown": "20", "dmg": "6D6/10D6", "threshold": "50或25%/35%"}
}

combat_profile.target_mode = &"ground"
combat_profile.target_team_filter = &"any"
combat_profile.target_selection_mode = &"single_coord"
combat_profile.range_value = 12
combat_profile.area_pattern = &"square"
combat_profile.area_value = 3
combat_profile.ap_cost = 3
combat_profile.mp_cost = 120
combat_profile.aura_cost = 2
combat_profile.cooldown_tu = 20
combat_profile.ai_tags = Array[StringName]([&"large_aoe", &"ultimate", &"execute", &"friendly_fire_risk"])
combat_profile.delivery_categories = Array[StringName]([&"spell", &"illusion", &"fear", &"psychic"])
combat_profile.effect_defs = [graded_save_execute_effect]
```

不要在资源里写 `type`、`class_id`、`required_level`、`spell_rank` 或 `cooldown_turns`。当前 schema 使用 `skill_type` 与 `cooldown_tu`；职业/学习门槛如果后续需要，走现有 `tags`、`learn_requirements`、`knowledge_requirements`、职业授予或技能前置系统，不新增字段。

新增 `mage_` 主动技能还必须满足现有法师技能对齐回归：

- `mastery_curve.size() == max_level`，且默认对齐火球术曲线 `[400, 1000, 2200, 4000, 6500, 9500, 13000]`。
- `growth_tier = &"advanced"`，`attribute_growth_progress` 总和必须为 180。
- `level_description_configs` 必须覆盖 `"0"` 到 `"7"`；实际 `.tres` 中不要使用省略号或缺级配置。
- `effect_defs` 至少有一个在 0..7 级均可用的效果；本方案的单个 `graded_save_execute` effect 不设置 min/max 即满足该要求。

`graded_save_execute_effect`：

```gdscript
effect_type = &"graded_save_execute"
effect_target_team_filter = &"any"
damage_tag = &"psychic"
save_dc_mode = &"caster_spell"
save_dc_source_ability = &"intelligence"
save_ability = &"willpower"
save_tag = &"illusion"
params = {
	"profile_id": "phantasmal_kill",
	"failure_execute_threshold_fixed": 50,
	"failure_execute_threshold_max_hp_percent": 25,
	"failure_damage_dice_count": 6,
	"failure_damage_dice_sides": 6,
	"failure_frightened_duration_tu": 120,
	"failure_reaction_lock_duration_tu": 60,
	"critical_failure_execute_threshold_max_hp_percent": 35,
	"critical_failure_damage_dice_count": 10,
	"critical_failure_damage_dice_sides": 6,
	"critical_failure_frightened_duration_tu": 180,
	"critical_failure_stunned_duration_tu": 60,
	"success_aftershock_duration_tu": 60
}
```

字段校验要求：

- `SkillContentRegistry.VALID_EFFECT_TYPES` 增加 `graded_save_execute`。
- 增加 `_append_graded_save_execute_validation_errors()` 专用校验，不只做通用 effect 校验。
- `profile_id` 必须为 `"phantasmal_kill"`；不要允许空 profile 静默通过。
- params 必须使用白名单，禁止错拼或多余 key 静默通过。
- 必需 key：`failure_execute_threshold_fixed`、`failure_execute_threshold_max_hp_percent`、`failure_damage_dice_count`、`failure_damage_dice_sides`、`failure_frightened_duration_tu`、`failure_reaction_lock_duration_tu`、`critical_failure_execute_threshold_max_hp_percent`、`critical_failure_damage_dice_count`、`critical_failure_damage_dice_sides`、`critical_failure_frightened_duration_tu`、`critical_failure_stunned_duration_tu`、`success_aftershock_duration_tu`。
- 骰子字段必须是正整数；百分比必须是 `1..100`；固定阈值必须 `>= 0`；所有 `*_duration_tu` 必须是 `SkillContentRegistry.TU_GRANULARITY` 的倍数。
- `effect_target_team_filter` 必须是 `any`，`damage_tag` 必须是 `psychic`，`save_dc_mode` 必须是 `caster_spell`，`save_ability` 必须是 `willpower`，`save_tag = &"illusion"` 必须通过现有 `BattleSaveContentRules` 校验。

## 状态语义

新增或确认以下状态语义：

| 状态 | ID | 时长来源 | 语义 |
| --- | --- | --- | --- |
| 余悸 | `aftershock` | 成功分支 60 TU | 有害、可驱散、刷新持续时间；通过 params 锁反击和守护 |
| 反应封锁 | `reaction_lock` | 失败分支 60 TU | 有害、可驱散、刷新持续时间；通过 params 锁反击和守护 |
| 恐惧 | `frightened` | 失败 120 TU / 大失败 180 TU | 有害、可驱散、刷新持续时间；强力攻击判定视为劣势来源 |
| 震慑 | `stunned` | 大失败 60 TU | 有害、可驱散；当前最小实现为回合开始 AP 惩罚，状态 `power` 必须足以清空本轮行动点 |

状态落点：

- `BattleStatusSemanticTable` 增加上述状态的语义、显示名、harmful/dispellable 归类。
- `BattleRuntimeModule.DEBUFF_STATUS_IDS` 与 `BattleRuntimeSkillTurnResolver.DEBUFF_STATUS_IDS` 增加上述状态。
- `BattleState.STRONG_ATTACK_DISADVANTAGE_STATUS_IDS` 增加 `frightened`。
- `BattleRuntimeModule.is_unit_counterattack_locked()` 已支持 `lock_counterattack` param，`aftershock` / `reaction_lock` 的 params 必须使用字符串 key：`"lock_counterattack": true`。
- `BattleRuntimeModule.is_unit_guard_locked()` 增加 `"lock_guard": true` 支持。
- `BattleRuntimeSkillTurnResolver.get_skill_cast_block_reason()` 对守护类技能应调用 `_runtime.is_unit_guard_locked(active_unit)`，不要只检查单个状态 ID。
- `BattleDamageResolver` 内附加这些状态时，构造临时 `CombatEffectDef` 并走现有 `_apply_status_effect()` 路径，复用 `BattleStatusSemanticTable.merge_status()` 的刷新、持续时间和 params 合并语义。

状态 params 示例：

```gdscript
{
	"lock_counterattack": true,
	"lock_guard": true,
	"counts_as_debuff": true
}
```

`stunned` 的临时 status effect 需要设置足够高的 `power`（建议 `99`），因为当前 AP 惩罚读取的是状态语义和 `status_entry.power`，只设置 duration/params 不会清空行动点。

## 运行时实现

### 1. 规则层

新增辅助规则脚本：

- `scripts/systems/battle/rules/battle_graded_save_execution_rules.gd`

职责：

- 从 `save_result` 计算分级，先处理 `immune`。
- 从 params 读取阈值和骰子配置。
- 计算失败分支处决阈值：`max(fixed, floor(max_hp * percent / 100.0))`。
- 计算大失败分支处决阈值：`floor(max_hp * percent / 100.0)`。
- 提供平均伤害估算给 AI 复用。
- 提供 `estimate_grade_distribution(source, target, effect_def, context)`：枚举 normal/advantage/disadvantage 的自然骰分布，返回 `immune`、`critical_success`、`success`、`failure`、`critical_failure` 的 basis points。不要只使用 `BattleSaveResolver.estimate_save_success_probability()` 的二分 success/failure 概率，因为它不区分自然 1/20。

### 2. 伤害解析

修改：

- `scripts/systems/battle/rules/battle_damage_resolver.gd`

新增 `graded_save_execute` 分支：

1. 调用 `BattleSaveResolver.resolve_save(source, target, effect_def, context)`。
2. 把 save result 写入 `result.save_results`。
3. 若分级是 `immune` 或 `critical_success`，返回 no-op 结果。
4. 若分级是 `success`，附加 `aftershock`。
5. 若分级是 `failure`：
   - 当前 HP 在阈值内：提交处决伤害。
   - 否则：提交 `6d6 psychic` 伤害，附加 `frightened` 和 `reaction_lock`。
6. 若分级是 `critical_failure`：
   - 当前 HP 在阈值内：提交处决伤害。
   - 否则：提交 `10d6 psychic` 伤害，附加 `frightened` 和 `stunned`。
7. 返回结构必须填充现有调用方依赖的字段：`applied`、`damage`、`hp_damage`、`shield_absorbed`、`damage_events`、`status_effect_ids`、`save_results`。

实现约束：

- 不直接修改 `target.current_hp`。
- 不在 `BattleGroundEffectService` 之外清理死亡单位。
- 不把免疫目标计入失败或大失败。
- `apply_direct_damage_to_target()` 只用于处决分支；它不做 psychic 抗性/免疫/减伤结算。
- 非处决的 `6d6` / `10d6` 必须走现有 damage outcome 路径或等价临时 `damage` effect，保留精神抗性、免疫、护盾和伤害事件。

### 3. 地面效果服务

需要改 effect 分类，优先不改击杀提交：

- `scripts/systems/battle/runtime/battle_ground_effect_service.gd`
- `scripts/systems/battle/rules/battle_skill_resolution_rules.gd`

`BattleSkillResolutionRules.is_unit_effect()` 必须加入 `graded_save_execute`。`BattleRuntimeModule.is_unit_effect()`、`BattleSkillExecutionOrchestrator._is_unit_effect()` 等包装路径若保留本地列表，也必须同步。否则 ground preview 和 `_apply_ground_unit_effects` 只会拿到空 unit effect 列表。

只要 `BattleDamageResolver.resolve_effects()` 返回的结果字段与现有 damage/status effect 一致，地面服务会继续负责日志、死亡提交、掉落和评分。只有在日志需要显示“处决”分级时，才增加从 `damage_events` 读取 `execution_grade` 的展示逻辑；这不是首批实现的阻塞项。

### 4. AI 和预览

修改：

- `scripts/systems/battle/ai/battle_ai_action_assembler.gd`
- `scripts/systems/battle/ai/battle_ai_score_service.gd`
- `scripts/systems/battle/ai/battle_ai_score_input.gd`
- `scripts/enemies/enemy_ai_action.gd`
- `scripts/enemies/actions/use_ground_skill_action.gd`
- `scripts/systems/battle/core/battle_preview.gd`
- `scripts/systems/battle/presentation/battle_hud_adapter.gd`

要求：

- `_is_offensive_effect()` 把 `graded_save_execute` 视为进攻效果。
- `BattleAiScoreService._is_damage_skill()` 把 `graded_save_execute` 视为伤害/处决技能。
- `EnemyAiAction._effect_list_has_hostile_threat()` 把 `graded_save_execute` 视为 hostile threat。
- AI 评分使用 `BattleGradedSaveExecutionRules.estimate_grade_distribution()`、平均伤害和处决阈值。
- 低 HP 敌人处于处决阈值内时，评分要显著高于普通 6d6/10d6 伤害。
- friendly fire 不可靠“靠扣分解决”。对友方目标必须写入 `BattleAiScoreInput.estimated_friendly_fire_target_count`；对处决阈值内或预计致死友方必须写入 `estimated_friendly_lethal_target_count`；硬拒绝场景必须写入非空 `friendly_fire_reject_reason`。
- `UseGroundSkillAction._passes_friendly_fire_limits()` 依赖 `friendly_fire_reject_reason`、`estimated_friendly_fire_target_count`、`estimated_friendly_lethal_target_count`。默认 `maximum_friendly_fire_target_count = 0` 和 `allow_friendly_lethal = false` 下，任意可受影响友方或可致死友方都必须被拒绝。
- 免疫或无心智目标估算为 no-op，不应吸引 AI。
- 玩家预览是首批阻塞项：`target_team_filter = any` 且 7x7 处决/控制技能，不能只显示单位数。标准 ground preview 仍然可复用，但必须在 `BattlePreview.hit_preview` 或等价 preview payload 中暴露友方受影响数、友方处决风险数、免疫/no-op 数、保存分级风险摘要，并由 HUD tooltip 或 warning 文案展示。
- 不新增独立特殊 profile；preview payload 仍来自标准 ground preview 和 `effect_defs`。

### 5. 敌人模板投影

必做修改：

- `scripts/enemies/enemy_template_def.gd`
- `scripts/systems/world/encounter_roster_builder.gd`

要求：

- `EnemyTemplateDef` 支持 `save_advantage_tags: Array[StringName]`。
- `EnemyTemplateDef.validate_schema()` 校验 `save_advantage_tags` 非空项和合法 tag/mode，避免 `illusion_immune` 这类错拼静默无效。
- 构建 `BattleUnitState` 时把模板的 `save_advantage_tags` 复制到单位状态。
- 内容侧使用 `illusion_immunity` 表达不受 `怪影杀戮` 影响的无心智/幻术免疫敌人。

## 预计修改文件清单

核心实现：

- `data/configs/skills/mage_phantasmal_kill.tres`
- `scripts/player/progression/skill_content_registry.gd`
- `scripts/systems/battle/rules/battle_skill_resolution_rules.gd`
- `scripts/systems/battle/rules/battle_graded_save_execution_rules.gd`
- `scripts/systems/battle/rules/battle_damage_resolver.gd`
- `scripts/systems/battle/rules/battle_status_semantic_table.gd`
- `scripts/systems/battle/core/battle_preview.gd`
- `scripts/systems/battle/core/battle_state.gd`
- `scripts/systems/battle/runtime/battle_runtime_module.gd`
- `scripts/systems/battle/runtime/battle_skill_turn_resolver.gd`
- `scripts/systems/battle/ai/battle_ai_action_assembler.gd`
- `scripts/systems/battle/ai/battle_ai_score_service.gd`
- `scripts/systems/battle/ai/battle_ai_score_input.gd`
- `scripts/enemies/enemy_ai_action.gd`
- `scripts/enemies/actions/use_ground_skill_action.gd`
- `scripts/enemies/enemy_template_def.gd`
- `scripts/systems/world/encounter_roster_builder.gd`
- `scripts/systems/battle/presentation/battle_hud_adapter.gd`

测试：

- `tests/battle_runtime/skills/run_phantasmal_kill_regression.gd`
- `tests/battle_runtime/rules/run_battle_graded_save_execution_rules_regression.gd`
- `tests/battle_runtime/rules/run_status_effect_semantics_regression.gd`
- `tests/battle_runtime/runtime/run_battle_state_disadvantage_regression.gd`
- `tests/battle_runtime/ai/run_phantasmal_kill_ai_regression.gd`
- `tests/battle_runtime/skills/run_mage_skill_alignment_regression.gd`
- `tests/progression/schema/run_skill_schema_regression.gd` 或现有内容验证 runner

文档：

- `docs/design/project_context_units.md`：代码落地后必须检查 CU-14 / CU-16 / CU-19 / CU-20 / CU-21。若新增技能资源、schema/effect type、规则脚本、状态语义、AI friendly-fire 合同、preview payload、敌人模板投影改变 runtime relationships、所有权边界或推荐 read set，必须同步更新。

## 回归测试

新增 `tests/battle_runtime/skills/run_phantasmal_kill_regression.gd` 覆盖：

1. `natural_roll = 20`：无伤害、无状态。
2. 免疫目标：`immune = true` 且 `natural_roll = 0` 时仍然 no-op，不能进入大失败。
3. 普通成功：只获得 `aftershock`，不能反击/守护，不受伤害。
4. 普通失败且低 HP：触发处决，击杀提交链保留掉落/评分/战斗状态。
5. 普通失败且高 HP：造成 `6d6 psychic`，附加 `frightened` 2 轮和 `reaction_lock` 1 轮。
6. 大失败且低 HP：35% 阈值处决。
7. 大失败且高 HP：造成 `10d6 psychic`，附加 `frightened` 3 轮和 `stunned` 1 轮。
8. `death_ward` / last stand：处决伤害不能绕过现有保命逻辑。
9. 精神伤害抗性/免疫：影响非处决伤害，不影响豁免分级本身。
10. 7x7 多目标：范围内敌我可受影响，范围外不受影响。
11. 状态附加走语义合并路径：重复附加刷新时长且保留 params。

扩展现有测试：

- 状态语义：`frightened` 进入强力攻击劣势；`aftershock` / `reaction_lock` 的字符串 key params 能锁反击和守护。
- `lock_guard` 回归：带 `"lock_guard": true` 的状态会阻断守护类技能。
- 规则层：`estimate_grade_distribution()` 在 normal / advantage / disadvantage / override rolls 下给出正确分级概率，且免疫返回 `immune = 10000`。
- AI：低 HP 敌人评分升高；任意可受影响友方导致默认拒绝；友军处决风险写入 lethal count 和 reject reason；免疫目标不吸引 AI；advantage/disadvantage 影响评分方向。
- 预览：玩家选点时能看到友方受影响数、友方处决风险、免疫/no-op 目标统计。
- 敌人模板：`EnemyTemplateDef.save_advantage_tags = [&"illusion_immunity"]` 能投影到 `BattleUnitState.save_advantage_tags` 并让技能 no-op。
- 内容验证：`graded_save_execute` effect、`mage_phantasmal_kill.tres`、保存标签、白名单 params 和骰子参数通过 schema 校验；错拼 key 应被拒绝。
- 法师技能对齐：新增 `mage_phantasmal_kill` 后，`run_mage_skill_alignment_regression.gd` 的 mage 主动技能数量断言必须同步从 `141` 更新为新增后的数量，或改成不依赖硬编码总数；同时断言 mastery/growth/description configs 全部通过。

建议执行顺序：

```bash
godot --headless --script tests/battle_runtime/skills/run_phantasmal_kill_regression.gd
godot --headless --script tests/battle_runtime/rules/run_battle_graded_save_execution_rules_regression.gd
godot --headless --script tests/battle_runtime/rules/run_status_effect_semantics_regression.gd
godot --headless --script tests/battle_runtime/runtime/run_battle_state_disadvantage_regression.gd
godot --headless --script tests/battle_runtime/ai/run_phantasmal_kill_ai_regression.gd
godot --headless --script tests/battle_runtime/skills/run_mage_skill_alignment_regression.gd
godot --headless --script tests/progression/schema/run_skill_schema_regression.gd
python tests/run_regression_suite.py
```

不要把数值模拟/平衡模拟 runner 加入常规全量回归，除非用户明确要求。

## 实现顺序

1. 增加 `BattleGradedSaveExecutionRules` 和纯规则回归测试。
2. 扩展 `SkillContentRegistry` 的 effect type 与 `graded_save_execute` 白名单 params 校验。
3. 扩展 `BattleSkillResolutionRules.is_unit_effect()` 及运行时包装列表，确保 ground preview/执行能收集该 effect。
4. 扩展 `BattleDamageResolver`，让单目标分级效果跑通；状态附加走 `_apply_status_effect()`，处决走 direct damage，非处决走 damage outcome。
5. 增加状态语义和反应锁/守护锁接入。
6. 增加 `mage_phantasmal_kill.tres`，同时补齐 mastery curve、advanced 成长预算、0..7 级描述配置。
7. 增加敌人模板 `save_advantage_tags` 投影和 schema 校验，补免疫目标测试。
8. 扩展 AI 识别、威胁判断、评分、friendly fire 字段和默认拒绝。
9. 扩展玩家预览 payload 与 HUD warning。
10. 更新 `run_mage_skill_alignment_regression.gd` 的 mage 技能数量断言，或移除硬编码数量依赖。
11. 跑窄回归，再跑常规全量回归。
12. 根据实际新增 runtime relationships、所有权边界和 read set 更新 `docs/design/project_context_units.md`。

## 验收标准

- 技能配置通过资源验证。
- 技能在 7x7 范围内对敌我单位按同一规则逐目标解析。
- 免疫目标不会因为 `natural_roll = 0` 被当成大失败。
- 所有死亡都通过现有击杀提交链结算。
- 反击锁、守护锁、恐惧劣势和震慑 AP 惩罚在运行时生效。
- AI 不会默认选择会影响或处决友军的落点；对应 score input 字段可被测试断言。
- 玩家预览能显示友方受影响、友方处决风险和免疫/no-op 目标统计。
- 新技能满足现有 mage 技能对齐回归：`mastery_curve`、`growth_tier`、180 成长预算、0..7 级描述配置和 mage 技能数量断言均已更新。
- 新增测试和常规回归通过。

## 对抗审查记录

首版执行方案已吸收原讨论稿中的主要风险：免疫 natural roll、空 `effect_defs`、直接写 HP、击杀链绕过、状态语义缺失、AI friendly fire、敌人模板免疫投影和兼容性边界。

第一轮子代理审查结论：

- 运行时层面：主路线无阻塞；补充要求是敌人模板投影改为必做、状态附加必须走语义合并路径、`stunned.power` 必须明确、非处决伤害不能用 direct damage。
- AI/平衡层面：原方案有阻塞；已补充 `BattleSkillResolutionRules.is_unit_effect()`、AI hostile threat / damage skill 链路、friendly-fire score input 字段、完整分级概率估算、玩家预览 warning。
- 数据/schema/测试层面：原方案有阻塞；已把资源字段修正为 `skill_type` / `cooldown_tu` 等现有字段，并增加 `graded_save_execute` 白名单 params 校验、敌人模板 tag 校验、规则层测试与 context map 更新要求。

第二轮子代理审查结论：

- 运行时层面：无阻塞，同意该方案。
- AI/平衡层面：无阻塞，同意该方案。
- 数据/schema/测试层面：仍有阻塞；已补齐 `mastery_curve`、advanced 成长预算、0..7 级描述配置、mage 技能数量断言更新要求，以及 `project_context_units.md` 的 CU-14 检查要求。

第三轮子代理审查结论：

- 数据/schema/测试层面：无阻塞，同意该方案。
- 最终共识：运行时、AI/平衡、数据/schema/测试三个审查方向均认为当前方案无阻塞问题。
