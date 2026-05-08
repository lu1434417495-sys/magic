# BattleRuntimeModule 拆分方案（修订版）

> 状态：已评审，待执行  
> 涉及文件：`scripts/systems/battle/runtime/battle_runtime_module.gd`（4962行）  
> 决策：采用 Option 2（按现有 sidecar 模式渐进拆分），不采用显式依赖注入或引入新接口

---

## 一、现状分析

### 1.1 文件规模

| 指标 | 数值 |
|------|------|
| 总行数 | 4962 |
| 方法数 | ~180 |
| 字段数 | ~30 |
| preload 常量 | 76 个 |
| 依赖子系统 | 23 个 |

### 1.2 现有 sidecar 模式（拆分基准）

项目中已有多个采用 `setup(runtime)` + `WeakRef` 模式的处理器。新模块**严格沿用此模式**：

```gdscript
class_name BattleChangeEquipmentResolver
extends RefCounted

var _runtime_ref: WeakRef = null
var _runtime = null:
    get:
        return _runtime_ref.get_ref() if _runtime_ref != null else null
    set(value):
        _runtime_ref = weakref(value) if value != null else null

func setup(runtime) -> void:
    _runtime = runtime

func dispose() -> void:
    _runtime = null
```

已在用此模式的 sidecar：

| Sidecar | 文件 | 职责 |
|---------|------|------|
| `BattleChangeEquipmentResolver` | `battle_change_equipment_resolver.gd:22` | 战斗换装 |
| `BattleChargeResolver` | `battle_charge_resolver.gd:33` | 冲锋技能 |
| `BattleRepeatAttackResolver` | `battle_repeat_attack_resolver.gd` | 连击技能 |
| `BattleRuntimeLootResolver` | `battle_runtime_loot_resolver.gd:44` | 战利品 |
| `BattleSkillTurnResolver` | `battle_skill_turn_resolver.gd:68` | 技能消耗/冷却/状态推进 |

**注意**：`BattleMagicBacklashResolver`（`battle_magic_backlash_resolver.gd:1`）当前没有 `setup(runtime)`，不在上述列表中。

### 1.3 测试对私有成员的直接访问

以下测试直接访问 module 的 `_private` 方法/字段，拆分时必须保留过渡 wrapper：

| 被访问成员 | 访问测试 |
|-----------|---------|
| `_apply_unit_shield_effects()` | `run_battle_skill_protocol_regression.gd:1139-1143` |
| `_collect_defeated_unit_loot()` | `run_wild_encounter_regression.gd:1269`, `run_battle_loot_drop_luck_regression.gd:86-202` |
| `_active_loot_entries` (字段) | `run_battle_loot_drop_luck_regression.gd:91-205`, `run_battle_resolution_contract_regression.gd:529-583` |
| `_initialize_battle_metrics()` | `run_battle_skill_protocol_regression.gd:1545` |
| `_battle_metrics` (通过 `get_battle_metrics()`) | 3 个 `*_analysis.gd` 文件 |

---

## 二、拆分策略

### 2.1 总体策略（Option 2：渐进拆分 + wrapper 委托）

```
BattleRuntimeModule (对外入口，方法签名不变)
  ├── 公开方法 → 委托给子模块（内部调用变为 _sidecar.method()）
  ├── _private wrapper → 保留过渡方法，委托给子模块
  └── 状态字段 → 真相源留在 BattleRuntimeModule
```

**原则**：
1. 新模块 `setup(runtime)`，通过 `WeakRef` 访问 runtime
2. `BattleRuntimeModule` 保留同名 wrapper 方法，转发到子模块
3. `_private` 字段的**真相源**不迁移（`_battle_metrics`、`_active_loot_entries`、`calamity_by_member_id` 等保留在 module）
4. 每步行为零回归，wrapper 稳定后再考虑收窄依赖

### 2.2 提取模块清单

```
scripts/systems/battle/runtime/
├── battle_runtime_module.gd                 # 修改（逐步委托）
├── battle_metrics_collector.gd              # 新增 Step 1
├── battle_shield_service.gd                 # 新增 Step 2
├── battle_ground_effect_service.gd          # 新增 Step 3
├── battle_special_skill_resolver.gd         # 新增 Step 4
├── battle_movement_service.gd               # 新增 Step 5
├── battle_timeline_driver.gd                # 新增 Step 6
└── battle_skill_execution_orchestrator.gd   # 新增 Step 7
```

---

## 三、各模块详细定义

### Step 1: `BattleMetricsCollector`（最低风险）

**文件**: `scripts/systems/battle/runtime/battle_metrics_collector.gd`

**职责**：战斗统计数据的初始化与增量更新

**模式**：`setup(runtime)` + `WeakRef`，通过 `_runtime._battle_metrics` 访问共享状态

**提取方法**（从 `BattleRuntimeModule` 移动实现，保留 wrapper）：

```
# 移动到 BattleMetricsCollector
_initialize_battle_metrics()
_build_unit_metric_entry(unit)
_ensure_unit_metric_entry(unit)
_ensure_faction_metric_entry(faction_id)
_record_turn_started(unit)
_record_action_issued(unit, cmd_type, ap_cost)
_record_skill_attempt(unit, skill_id)
_record_skill_success(unit, skill_id)
_record_effect_metrics(source, target, damage, healing, kill_count)
_record_unit_defeated(unit)
_increment_metric_count(map, key, delta)

# 保留在 BattleRuntimeModule 的 wrapper（委托）
func _initialize_battle_metrics() → _metrics_collector._initialize_battle_metrics()
func _record_turn_started(unit) → _metrics_collector._record_turn_started(unit)
# ... 每个方法一个 wrapper
```

**不迁移的字段**：`_battle_metrics: Dictionary` 保留在 `BattleRuntimeModule`，`get_battle_metrics()` 保持不变

---

### Step 2: `BattleShieldService`（低风险）

**文件**: `scripts/systems/battle/runtime/battle_shield_service.gd`

**职责**：护盾施加、叠加、替换、HP 掷骰

**模式**：`setup(runtime)` + `WeakRef`

**提取方法**：

```
# 移动到 BattleShieldService
_apply_unit_shield_effects(source, target, skill, effects, ctx)
_apply_shield_effect_to_target(source, target, skill, effect, ctx)
_write_unit_shield(target, hp, duration, family, source_id, skill_id, params)
_build_unit_shield_result(target, applied)
_resolve_shield_hp(effect, ctx)
_roll_shield_hp(effect)
_has_shield_dice_config(effect)
_get_shield_roll_cache_key(effect)
_roll_battle_effect_die(sides)
_resolve_shield_duration_tu(effect)
_resolve_shield_family(skill, effect)

# 保留在 BattleRuntimeModule 的 wrapper（关键：测试直接调用 _apply_unit_shield_effects）
func _apply_unit_shield_effects(...) → _shield_service._apply_unit_shield_effects(...)
```

**关键约束**：`run_battle_skill_protocol_regression.gd:1139` 直接调用 `runtime._apply_unit_shield_effects()`，wrapper 必须保留。

---

### Step 3: `BattleGroundEffectService`（中风险）

**文件**: `scripts/systems/battle/runtime/battle_ground_effect_service.gd`

**职责**：地面技能的地格效果、地形替换、高度变化、坠落伤害、水域拓扑

**模式**：`setup(runtime)` + `WeakRef`

**提取方法**：

```
# 移动到 BattleGroundEffectService
_apply_ground_unit_effects(source, skill, effects, coords, batch)
_apply_ground_terrain_effects(source, skill, effects, coords, batch)
_apply_ground_cell_effect(source, skill, coord, effect, batch)
_reconcile_water_topology(coords, batch)
_apply_ground_jump_relocation(unit, coords, batch)
_get_ground_jump_effect_def(skill, variant)
_is_ground_jump_effect(effect)
_get_effect_forced_move_mode(effect)
_build_ground_effect_coords(skill, coords, source, unit, variant)
_collect_ground_preview_unit_ids(source, skill, effects, coords)
_resolve_ground_unit_effect_result(source, target, skill, effects)
_should_resolve_ground_effects_as_attack(effects)
_dedupe_effect_defs_by_instance(effects)
_resolve_ground_spell_control_after_cost(unit, skill, spent_mp, batch)
_resolve_unit_spell_control_after_cost(unit, skill, batch)
_get_ground_special_effect_validation_message(unit, skill, variant, coords)
_validate_target_coords_shape(pattern, coords)
_normalize_target_coords(cmd)
```

**依赖关系**：`_apply_ground_terrain_effects` 会调用 `_apply_unit_shield_effects`，拆分后通过 `_runtime._shield_service` 访问。

**在 BattleRuntimeModule 保留的 wrapper**：

```
func _apply_ground_terrain_effects(...) → _ground_effect_service._apply_ground_terrain_effects(...)
func _apply_ground_unit_effects(...) → _ground_effect_service._apply_ground_unit_effects(...)
# ... 每个公开入口保留 wrapper
```

---

### Step 4: `BattleSpecialSkillResolver`（中高风险）

**文件**: `scripts/systems/battle/runtime/battle_special_skill_resolver.gd`

**职责**：特定命途技能的特殊效果（黑星烙印、厄命宣判、折冠、断命换位、黑冠封印、强制位移、体型覆盖、击杀增益、相邻阵亡触发）

**模式**：`setup(runtime)` + `WeakRef`

**提取方法**：

```
# 移动到 BattleSpecialSkillResolver
_apply_black_star_brand_effect(unit, target)
_apply_doom_shift_effect(unit, target, batch)
_apply_forced_move_effect(source, unit, effect, batch)
_apply_body_size_category_override_effect(source, target, effect, batch)
_apply_on_kill_gain_resources_effects(source, defeated, skill, effects, batch)
_swap_unit_positions(first, second, batch)
_pick_forced_move_coord(unit, mode)
_score_forced_move_coord(unit, coord, mode)
_collect_hostile_units_for(unit)
_handle_adjacent_ally_defeat(defeated)
_handle_low_luck_relic_ally_defeat(defeated, batch)
_collect_adjacent_living_allies(defeated)
_are_units_adjacent(first, second)
_blocks_enemy_forced_move(source, target)
_record_vajra_body_mastery_from_incoming_damage(source, target, skill, result, batch)
_set_runtime_status_effect(unit, status_id, duration, source_id, power, params)
_clear_black_star_brand_statuses(unit)
_clear_crown_break_seal_statuses(unit)

# 判断辅助方法
_is_black_star_brand_skill(id)
_is_black_contract_push_skill(id)
_is_doom_shift_skill(id)
_is_black_crown_seal_skill(id)
_is_crown_break_skill(id)
_is_doom_sentence_skill(id)
_is_black_star_brand_elite_target(unit)
_is_elite_or_boss_target(unit)
_is_boss_target(unit)
_is_crown_break_target_eligible(unit, target)
_is_doom_sentence_target_eligible(unit, target)
_is_black_crown_seal_target_eligible(unit, target)
```

**关键约束**：`calamity_by_member_id` **真相源不迁移**，保留在 `BattleRuntimeModule`。`_misfortune_service` 等外部依赖通过 `_runtime._misfortune_service` 访问。

---

### Step 5: `BattleMovementService`（中风险）

**文件**: `scripts/systems/battle/runtime/battle_movement_service.gd`

**职责**：移动可达坐标 BFS、移动路径解析、移动消耗计算、移动命令执行

**模式**：`setup(runtime)` + `WeakRef`

**提取方法**：

```
# 移动到 BattleMovementService
get_unit_reachable_move_coords(unit)                 # BFS 可达坐标
_resolve_move_path_result(unit, target)              # 移动路径解析
_handle_move_command(unit, cmd, batch)               # 移动命令执行
_move_unit_along_validated_path(unit, path, target, batch)
_get_available_move_points(unit)
_is_normal_movement_locked(unit)
_get_move_cost_for_unit_target(unit, target, allow_quickstep)
_get_move_cost_for_unit_target_without_quickstep(unit, target)
_get_move_path_cost(unit, anchor_path)
_get_status_move_cost_delta(unit)
_build_reachable_move_buckets(max_mp)
_build_reachable_move_state_key(coord, has_quickstep)
_collect_dict_vector2i_keys(values)
```

**在 BattleRuntimeModule 保留的 wrapper**：

```
func get_unit_reachable_move_coords(unit) → _movement_service.get_unit_reachable_move_coords(unit)
func _handle_move_command(...) → _movement_service._handle_move_command(...)
func _resolve_move_path_result(...) → _movement_service._resolve_move_path_result(...)
```

**关键约束**：`preview_command(TYPE_MOVE)` 仍走 runtime 外观层，外观层委托给 `_movement_service`。

---

### Step 6: `BattleTimelineDriver`（中风险）

**文件**: `scripts/systems/battle/runtime/battle_timeline_driver.gd`

**职责**：时间线推进、体力恢复、单位就绪收集、行动优先级排序、回合开始/结束

**不承担的职责**（已由 `BattleSkillTurnResolver` 处理）：
- 技能消耗（`consume_skill_costs`）
- 冷却推进（`advance_unit_cooldowns`）
- 状态语义实现（`apply_unit_status_periodic_ticks`、`advance_unit_status_durations`、`apply_turn_start_statuses`）— 这些由 `BattleSkillTurnResolver` 实现，TimelineDriver 只负责**编排调用**

**不承担的职责**（保留在 runtime）：
- AI 回合决策（`_prepare_ai_turn`、`_cleanup_ai_turn`、`_build_ai_action_plans`）— 保留在 `BattleRuntimeModule`
- AI 行动执行（`issue_command` 路径中的 `_ai_service.choose_command`）— 保留在 runtime

**模式**：`setup(runtime)` + `WeakRef`，通过 `_runtime._skill_turn_resolver` 调用状态相关方法

**提取方法**：

```
# 移动到 BattleTimelineDriver
advance(delta_seconds)
_use_discrete_timeline_ticks()
_apply_timeline_step(batch, delta, tu_delta)
_apply_continuous_timeline_seconds(batch, delta)
_resolve_timeline_status_phase(batch, tu)           # 编排调用 _skill_turn_resolver
_collect_timeline_ready_units(batch, tu)
_apply_stamina_recovery(unit, tu)
_get_unit_constitution(unit)
_apply_stamina_recovery_percent_bonus(unit, base)
_get_unit_stamina_max(unit)
_activate_next_ready_unit(batch)
_end_active_turn(batch)
_check_battle_end(batch)
_count_living_units(unit_ids)
_sort_ready_unit_ids_by_action_priority()
_is_left_ready_unit_higher_priority()
_get_unit_turn_order_attribute()
_get_unit_turn_order_action_points()
_resolve_timeline_units_per_second(ctx)
_resolve_timeline_tick_interval_seconds(ctx)
_resolve_timeline_tu_per_tick(ctx)
_initialize_unit_action_thresholds()
_resolve_unit_action_threshold(unit)
_normalize_unit_action_threshold(threshold)
_initialize_unit_trait_hooks()
_get_units_in_order()
```

**在 BattleRuntimeModule 保留的 wrapper**：

```
func advance(...) → _timeline_driver.advance(...)
func _end_active_turn(...) → _timeline_driver._end_active_turn(...)
func _check_battle_end(...) → _timeline_driver._check_battle_end(...)
# ...
```

**常量迁移**：`TU_GRANULARITY`、`STAMINA_RECOVERY_PROGRESS_BASE`、`STAMINA_RECOVERY_PROGRESS_DENOMINATOR`、`STAMINA_RESTING_RECOVERY_MULTIPLIER`、`DEFAULT_TICK_INTERVAL_SECONDS`

---

### Step 7: `BattleSkillExecutionOrchestrator`（最高风险，最后拆）

**文件**: `scripts/systems/battle/runtime/battle_skill_execution_orchestrator.gd`

**职责**：技能命令路由、目标验证、技能执行编排（不负责具体效果计算，具体效果由各 resolver 处理）

**关键约束**：
- `preview_command()` / `issue_command()` 的 preview-first 门禁不能改变
- `BattleState` 的真相源不从 runtime 迁出
- 与 `BattleSkillTurnResolver`、`BattleChargeResolver`、`BattleRepeatAttackResolver`、`BattleMagicBacklashResolver`、`BattleGroundEffectService`、`BattleShieldService`、`BattleSpecialSkillResolver` 全部交叉

**模式**：`setup(runtime)` + `WeakRef`

**提取方法**：

```
# 命令路由
preview_command(cmd)
issue_command(cmd)
_get_battle_interaction_block_message()
_should_block_skill_issue_from_preview(cmd, batch)
_append_batch_logs_to_state(batch)
_append_batch_logs_to_state_from(batch, log_start, report_start)

# 技能预览
_preview_skill_command(unit, cmd)
_preview_unit_skill_command(unit, cmd, skill, variant)
_preview_ground_skill_command(unit, cmd, skill, variant)

# 技能执行
_handle_skill_command(unit, cmd, skill, batch)
_handle_unit_skill_command(unit, cmd, skill, variant, batch)
_handle_ground_skill_command(unit, cmd, skill, variant, batch)

# 单位技能结果应用
_apply_unit_skill_result(unit, target, skill, variant, effects, batch, spell_ctx)
_resolve_unit_skill_effect_result(unit, target, skill, effects)
_should_resolve_unit_skill_as_fate_attack(unit, target, skill, effects)

# 目标验证
_validate_unit_skill_targets(unit, cmd, skill, variant)
_get_unit_skill_target_validation_message(unit, target, skill, variant)
_get_body_size_category_override_validation_message(unit, target, skill, variant)
_skill_grants_guarding(skill)
_can_skill_target_unit(unit, target, skill)
_is_multi_unit_skill(skill)
_should_route_skill_command_to_unit_targeting(skill, cmd)
_normalize_target_unit_ids(cmd)
_sort_target_unit_ids_for_execution(ids)

# cast variant
_resolve_unit_cast_variant(skill, unit, cmd)
_resolve_ground_cast_variant(skill, unit, cmd)
_build_implicit_ground_cast_variant(skill)
_get_cast_variant_target_mode(skill, variant)

# 技能效果收集
_collect_unit_skill_effect_defs(skill, variant, unit)
_collect_ground_unit_effect_defs(skill, variant, unit)
_collect_ground_terrain_effect_defs(skill, variant, unit)
_collect_ground_effect_defs(skill, variant, unit)

# 预览构建
_build_unit_skill_hit_preview(unit, targets, skill, variant)
_build_unit_skill_damage_preview(unit, skill, variant)
_append_damage_preview_line(preview)
_build_unit_skill_resolution_preview_lines(unit, target, skill, variant)
_build_skill_log_subject_label(source, skill, variant)

# 连锁伤害
_apply_chain_damage_effects(source, primary, skill, effects, result, batch, subject, spell_ctx)
_collect_chain_damage_effect_defs(effects)
_get_effect_params(effect)
_build_chain_target_effect_defs(effects, chain)
_collect_chain_damage_targets(source, primary, skill, chain, spell_ctx)
_resolve_chain_damage_radius(primary, chain, spell_ctx)
_unit_stands_on_terrain_effect(unit, effect_id)
_is_unit_in_chain_radius(primary, candidate, radius, chain)
_is_chain_height_valid(from_unit, to_unit)

# 效果判定
_resolve_effect_target_filter(skill, effect)
_is_unit_valid_for_effect(source, target, filter)
_collect_units_in_coords(coords)
_is_unit_effect(effect)
_is_terrain_effect(effect)

# report formatter 委托
summarize_damage_result(result)
build_damage_absorb_reason_text(summary)
append_damage_result_log_lines(batch, subject, target_name, result)

# 技能精通
_format_skill_variant_label(skill, variant)
_get_unit_skill_level(unit, skill_id)
```

**在 BattleRuntimeModule 保留的 wrapper**：

```
func preview_command(...) → _skill_orchestrator.preview_command(...)
func issue_command(...) → _skill_orchestrator.issue_command(...)
func _handle_skill_command(...) → _skill_orchestrator._handle_skill_command(...)
# ... 所有公开入口保留 wrapper
```

---

## 四、实施计划

### 4.1 分步执行（按推荐顺序）

| Step | 模块 | 风险 | 关键约束 |
|------|------|------|---------|
| 1 | `BattleMetricsCollector` | 极低 | `_battle_metrics` 真相源保留在 runtime |
| 2 | `BattleShieldService` | 低 | 保留 `_apply_unit_shield_effects()` wrapper |
| 3 | `BattleGroundEffectService` | 中 | 先保留 `_apply_ground_terrain_effects()` wrapper |
| 4 | `BattleSpecialSkillResolver` | 中高 | `calamity_by_member_id` 不迁出 runtime |
| 5 | `BattleMovementService` | 中 | `preview_command(TYPE_MOVE)` 仍走 runtime 外观 |
| 6 | `BattleTimelineDriver` | 中 | 不接管 AI 决策和 `BattleSkillTurnResolver` 已有逻辑 |
| 7 | `BattleSkillExecutionOrchestrator` | 最高 | preview-first 门禁不变，最后执行 |

### 4.2 每一步的标准流程

1. 创建新 `.gd` 文件，`class_name Xxx`, `extends RefCounted`
2. 添加 `_runtime_ref: WeakRef` + 属性，实现 `setup(runtime)` 和 `dispose()`
3. 从 `BattleRuntimeModule` **移动实现**到新模块（修改 `self.` → `_runtime.`）
4. 在 `BattleRuntimeModule` 中原方法**改为 wrapper 委托**
5. 在 `_ensure_sidecars_ready()` 中添加 `_xxx.setup(self)`
6. 在 `dispose()` 中添加 `_xxx.dispose()`
7. 运行回归测试：
   ```bash
   godot --headless --script tests/battle_runtime/run_battle_runtime_smoke.gd
   godot --headless --script tests/battle_runtime/run_battle_runtime_ai_regression.gd
   godot --headless --script tests/battle_runtime/run_battle_board_regression.gd
   ```
8. 确认对 `battle_runtime_module.gd` 以外的文件**零修改**

### 4.3 私有一致性检查清单

每步完成后验证：
- [ ] `tests/battle_runtime/run_battle_skill_protocol_regression.gd` 仍可访问 `runtime._apply_unit_shield_effects()`
- [ ] `tests/battle_runtime/run_battle_loot_drop_luck_regression.gd` 仍可访问 `runtime._active_loot_entries` 和 `runtime._collect_defeated_unit_loot()`
- [ ] `tests/battle_runtime/run_wild_encounter_regression.gd` 仍可访问 `runtime._collect_defeated_unit_loot()`
- [ ] `tests/battle_runtime/run_battle_resolution_contract_regression.gd` 仍可访问 `runtime._active_loot_entries`
- [ ] `get_battle_metrics()` 返回值格式不变（3 个 `*_analysis.gd` 文件依赖）
- [ ] `configure_damage_resolver_for_tests()` 正确传播到 `_ai_service`、`_fortune_service`、`_misfortune_service`、`_change_equipment_resolver`、`_loot_resolver`、`_skill_turn_resolver`

---

## 五、风险与约束

### 5.1 不修改对外接口

`GameRuntimeFacade` 和所有测试通过 `BattleRuntimeModule` 调用。拆分后保持所有公开方法签名不变，`class_name BattleRuntimeModule` 不变。

### 5.2 真相源不迁移

以下字段**始终保留在 `BattleRuntimeModule`**，子模块通过 `_runtime._xxx` 访问：

| 字段 | 原因 |
|------|------|
| `_state: BattleState` | 战斗全局状态，所有子模块依赖 |
| `_battle_metrics: Dictionary` | 指标数据，`get_battle_metrics()` 返回 |
| `_active_loot_entries: Array` | 测试直接访问 |
| `calamity_by_member_id: Dictionary` | fate/misfortune 共享状态 |
| `_ai_turn_traces: Array[Dictionary]` | AI trace，多个子系统读写 |
| `_ai_action_plans_by_unit_id: Dictionary` | AI 计划 |
| `_battle_rating_stats: Dictionary` | 评分数据 |
| `_pending_post_battle_character_rewards: Array` | 战后奖励 |
| `_terrain_effect_nonce: int` | 地形效果序号 |

### 5.3 不拆分的部分

- 单位放置逻辑（`_place_units` 系列）— 与 `start_battle()` 紧密耦合
- 生成验证（spawn reachability）
- AI 决策逻辑（`_build_ai_action_plans`、`_prepare_ai_turn`、`_cleanup_ai_turn`）
- 战利品收集（已由 `BattleRuntimeLootResolver` 处理，module 仅做 wrapper 委托）
- 批次辅助方法（`_new_batch`、`_merge_batch`、`_append_*`）— 保留在 module 作为共享工具层

### 5.4 BattleTimelineDriver 边界

`BattleTimelineDriver` **只编排调用**，不接管以下已在 `BattleSkillTurnResolver` 中的实现：

- `consume_skill_costs` / `get_effective_skill_costs`
- `advance_unit_cooldowns` / `consume_turn_cooldown_delta`
- `advance_unit_turn_timers`
- `apply_unit_status_periodic_ticks`
- `advance_unit_status_durations`
- `apply_turn_start_statuses`

TimelineDriver 通过 `_runtime._skill_turn_resolver` 调用这些方法。

### 5.5 BattleMagicBacklashResolver 归类修正

`BattleMagicBacklashResolver` 没有 `setup(runtime)` 模式，不在本次拆分的同类 sidecar 列表中。在 `BattleGroundEffectService` 和 `BattleSkillExecutionOrchestrator` 中仍通过 `_runtime._magic_backlash_resolver` 访问。

---

## 六、验收标准

1. **零回归**：所有现有回归测试通过（`run_battle_runtime_smoke.gd`、`run_battle_runtime_ai_regression.gd`、`run_battle_board_regression.gd` 等）
2. **无测试修改**：现有测试文件**零行修改**（`tests/` 目录无变更）
3. **无 facede 修改**：`GameRuntimeFacade` 和 `BattleSessionFacade` 无变更
4. **新增文件可编译**：每个新增 `.gd` 文件的 `class_name` 可被 `preload` 实例化
5. **setup/dispose 完整性**：`_ensure_sidecars_ready()` 正确 setup 所有子模块，`dispose()` 正确清理
6. **`configure_damage_resolver_for_tests()` 行为不变**：传播到所有依赖 `_damage_resolver` 的子模块
