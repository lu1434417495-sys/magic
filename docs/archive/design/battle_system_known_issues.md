# 战斗系统已知问题 / Known Issues

日期：2026-04-26

覆盖范围：

- `scripts/systems/battle_runtime_module.gd`
- `scripts/systems/battle_damage_resolver.gd`
- `scripts/systems/battle_hit_resolver.gd`
- `scripts/systems/battle_repeat_attack_resolver.gd`
- `scripts/systems/battle_session_facade.gd`
- `tests/battle_runtime/`

本文只保留当前仍需要后续跟进的问题；已经修复或经复核已过期的旧条目移到下方状态表。

---

## 仍需关注

当前列表中没有仍需处理的旧 known issue。后续新增问题应继续按“现象 / 风险 / 建议修法 / 回归覆盖”记录。

---

## 本轮已修复

### ISSUE-BATTLE-01：地面跳跃技能在前摇失败前扣费

- **处理**：地面技能执行路径现在会在 `_consume_skill_costs()` 前重新读取 `target_coords` 并调用 `_get_ground_special_effect_validation_message()`，确保跳跃落点等 precast 条件在扣 AP / stamina / cooldown 前通过。
- **回归覆盖**：`tests/battle_runtime/run_battle_runtime_smoke.gd::_test_ground_jump_precast_failure_does_not_consume_costs`

### ISSUE-BATTLE-03：双方同时清场被判为敌方胜利

- **处理**：`_check_battle_end()` 在 `living_allies <= 0 and living_enemies <= 0` 时写入 `winner_faction_id = &"draw"`；`_resolve_encounter_resolution()` 对应返回 `&"draw"`。
- **回归覆盖**：`tests/battle_runtime/run_battle_resolution_contract_regression.gd::_test_battle_runtime_draws_when_both_sides_are_cleared`

### ISSUE-BATTLE-11：战斗结束 / 回合结束后追加的日志没有写入 state

- **处理**：`issue_command()` 现在记录首次 flush 的日志 / report 下标，在 `_check_battle_end()` 或 `_end_active_turn()` 追加内容后只补写新增条目，避免重复写入。
- **回归覆盖**：`tests/battle_runtime/run_battle_runtime_smoke.gd::_test_issue_command_flushes_battle_end_logs_to_state`

### ISSUE-BATTLE-08：状态 TU duration 与 tick 口径

- **处理**：状态 duration 统一按 TU 时间轴结算，包括自己回合内施加的 self buff；burning 这类持续伤害不再由 turn-start 隐式保证触发，改为依赖状态自身的 `tick_interval_tu` 周期。
- **回归覆盖**：`tests/battle_runtime/run_status_effect_semantics_regression.gd::_test_burning_stacks_and_ticks_on_timeline_interval`
- **回归覆盖**：`tests/battle_runtime/run_status_effect_semantics_regression.gd::_test_short_burning_can_expire_before_first_tick`

### ISSUE-BATTLE-09：效果骰 / 护盾骰仍使用 `String.hash()` 种子

- **处理**：`_roll_battle_effect_die()` 改为直接使用 `TrueRandomSeedService.randi_range()`，不再维护效果骰游标或哈希种子复现口径。
- **回归覆盖**：`tests/battle_runtime/run_battle_skill_protocol_regression.gd::_test_shield_dice_roll_is_random_and_shared_per_cast`

### ISSUE-BATTLE-10：`_get_living_units_in_order` 命名误导

- **处理**：函数已重命名为 `_get_units_in_order()`，调用方继续自行过滤存活状态。

---

## 已修复或已过期

- **ISSUE-BATTLE-02**：连斩段数扣 AU 的旧描述已过期；当前重复攻击尝试会消耗阶段资源，相关测试已覆盖。
- **ISSUE-BATTLE-04**：`preview_command()` 已检查 `modal_state`，模态锁定期间预览会明确拒绝。
- **ISSUE-BATTLE-05**：伤害减免已改为最终 floor，`MIN_DAMAGE_FLOOR := 0`，旧的“每个乘数后 floor 到 1”问题不再成立。
- **ISSUE-BATTLE-06**：行动进度已按每单位 `action_threshold` 推进，`run_battle_runtime_smoke.gd::_test_timeline_tick_uses_per_unit_action_threshold` 覆盖该行为。
- **ISSUE-BATTLE-07**：相邻移动路径包含起点和目标格，语义成本重算路径会覆盖单步移动；旧问题描述不再准确。

---

## 建议后续顺序

1. 跑更宽的 battle runtime 回归集合，确认现有大量未提交改动之间没有新的交叉回归。
2. 若后续新增状态 / 随机口径问题，继续补到本文。
