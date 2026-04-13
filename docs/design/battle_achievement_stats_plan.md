# 战斗统计与成就接入方案

更新日期：`2026-04-07`

关联文档：`docs/design/achievement_system_plan.md`

## Summary

- 为战斗运行时补一套正式的角色战绩采集，直接服务成就系统，不进入战斗地图 UI。
- 统计对象固定为玩家阵营中 `control_mode == manual` 的角色，与成就归属 `member_id` 对齐。
- “单个回合”固定定义为一次完整行动，从角色进入 `unit_acting` 到 AP 用尽或主动待机结束。
- 成就侧继续保留现有事件入口 `record_achievement_event(...)`，但扩展为既支持累计值，也支持读取 `meta` 中的峰值/汇总字段。
- 除既有 `skill_used`、`enemy_defeated`、`battle_won` 外，新增统一结算事件 `battle_finished`，用于承接完整战斗统计与峰值成就。

## 当前仓库事实

- `BattleRuntimeModule` 目前已经维护 `_battle_rating_stats`，但只记录：
  - `successful_skill_count`
  - `total_damage_done`
  - `total_healing_done`
  - `kill_count`
- 这组数据只用于 battle rating 熟练度奖励，不会进入 `BattleState`，也不会对外导出成成就可直接消费的完整汇总。
- `CharacterManagementModule.record_achievement_event(...)` 当前只支持：
  - 用 `amount` 累加 `AchievementProgressState.current_value`
  - 达到 `threshold` 后解锁一次
- `AchievementDef` 当前只有：
  - `event_type`
  - `subject_id`
  - `threshold`
- `WorldMapSystem.setup()` 目前调用 `CharacterManagementModule.setup(_party_state, _game_session.get_skill_defs(), _game_session.get_profession_defs())`，尚未把 `achievement_defs` 接入。

## 统计口径

### 统计对象

- 仅统计玩家阵营中的手动角色。
- 敌方单位与 AI 控制的我方单位不进入这套角色成就统计真源。
- 所有统计以 `member_id` 为主键；没有 `source_member_id` 的运行时单位不参与。

### 首版指标

- 输出与行动：
  - `action_turn_count`
  - `skill_cast_count`
  - `successful_skill_count`
  - `total_damage_done`
  - `total_healing_done`
  - `total_kill_count`
  - `max_turn_damage_done`
  - `max_turn_healing_done`
  - `max_turn_kill_count`
- 生存：
  - `total_damage_taken`
  - `total_healing_received`
  - `downed_count`

### 单行动回合定义

- 角色进入 `unit_acting` 时，创建或重置本回合临时统计：
  - `turn_damage_done`
  - `turn_healing_done`
  - `turn_kill_count`
- 该角色在本次行动中产生的全部有效技能、AOE、地面持续效果输出，都累计到这组临时值。
- 角色离开 `unit_acting` 时，将本回合临时值回写到：
  - `max_turn_damage_done`
  - `max_turn_healing_done`
  - `max_turn_kill_count`
- 峰值比较逻辑固定为历史最大值，不做平均、不做最近若干回合窗口。

### 记账规则

- `skill_cast_count`：命令进入真实执行路径并消耗行动资源时增加，不统计预览、非法命令或未成功执行的请求。
- `successful_skill_count`：沿用当前 battle rating 口径，在技能成功施放并进入熟练度结算时增加。
- `total_damage_done` / `total_healing_done` / `total_kill_count`：
  - 单体技能命中后增加。
  - 地面/AOE 技能按整次结算的总和增加。
  - 来源明确的定时地形效果也记到原施放者名下。
- `total_damage_taken` / `total_healing_received`：
  - 角色每次受到真实伤害或治疗时增加，不区分来源阵营。
- `downed_count`：
  - 仅在该角色 `is_alive` 从 `true` 变为 `false` 的瞬间增加一次。
  - 后续清场、重复检查或已倒地状态下的再次处理不得重复累计。

## 实现方案

### BattleRuntimeModule

- 将 `_battle_rating_stats` 扩成通用战斗统计真源，继续按 `member_id` 聚合。
- 初始化时为每个手动角色建立完整统计字典，并附加本回合临时统计容器。
- 在以下节点更新统计：
  - `_activate_next_ready_unit(...)`
    - 角色进入行动时增加 `action_turn_count`
    - 重置本回合临时统计
  - `_handle_unit_skill_command(...)`
    - 更新施放次数、成功施放后的输出累计与击杀数
  - `_apply_ground_effects_to_units(...)`
    - 汇总整次地面/AOE 结算的伤害、治疗、击杀
  - `_apply_timed_terrain_effect_to_unit(...)`
    - 若 `source_unit.source_member_id != ""`，把效果记入来源角色输出
  - 所有角色受伤/受疗/倒地结算点
    - 同步写入目标角色的生存累计
  - `_end_active_turn(...)`
    - 把本回合临时统计刷新到 `max_turn_*`
- 当前 battle rating 的熟练度奖励与评分逻辑改为直接读取这套通用统计，不再维护重复口径。

### 成就事件与结算时机

- 保留以下现有事件：
  - `skill_used`
  - `enemy_defeated`
  - `battle_won`
- 新增：
  - `battle_finished`
- 事件分工固定为：
  - `skill_used`
    - 继续用于“累计施放次数”类成就
    - 可附带轻量 `meta`，但不承担完整战报职责
  - `enemy_defeated`
    - 继续用于“累计击杀数”类成就
  - `battle_won`
    - 只在玩家胜利时发
  - `battle_finished`
    - 无论胜负，对每个手动角色各发一次
    - 用于峰值统计、完整战绩统计和战斗结算类成就
- `battle_finished.meta` 固定包含：
  - `battle_id`
  - `encounter_anchor_id`
  - `winner_faction_id`
  - `player_victory`
  - `survived_battle`
  - `stats`
- `stats` 内固定放完整统计字段：
  - `action_turn_count`
  - `skill_cast_count`
  - `successful_skill_count`
  - `total_damage_done`
  - `total_healing_done`
  - `total_kill_count`
  - `max_turn_damage_done`
  - `max_turn_healing_done`
  - `max_turn_kill_count`
  - `total_damage_taken`
  - `total_healing_received`
  - `downed_count`

### CharacterManagementModule

- `setup(...)` 必须始终接入 `achievement_defs`；`WorldMapSystem` 初始化时同步补传 `GameSession.get_achievement_defs()`。
- `record_achievement_event(...)` 扩展规则：
  - 仍保留 `amount` 入参，兼容旧累计成就。
  - 若成就定义未声明 `meta_value_key`，继续使用 `amount`。
  - 若成就定义声明 `meta_value_key`，则从 `meta` 中按点路径读取数值，例如：
    - `stats.max_turn_healing_done`
    - `stats.total_damage_taken`
  - 读取失败、值非数字、值小于等于 0 时，本次事件不推进该成就。
- 进度更新模式新增两种：
  - `accumulate`
    - `current_value += resolved_value`
  - `max`
    - `current_value = max(current_value, resolved_value)`
- 旧成就默认：
  - `progress_mode = accumulate`
  - `meta_value_key = ""`

### AchievementDef / AchievementProgressState / Registry

- `AchievementDef` 新增字段：
  - `progress_mode: StringName`
  - `meta_value_key: String`
- 兼容默认值：
  - `progress_mode` 缺省为 `accumulate`
  - `meta_value_key` 缺省为 `""`
- `AchievementProgressState` 不新增字段：
  - 累计型成就的 `current_value` 仍表示累计值
  - 峰值型成就的 `current_value` 改表示历史最大值
- `ProgressionContentRegistry`：
  - `_build_achievement(...)` 支持新增字段
  - 校验规则补充：
    - `progress_mode` 只能是 `accumulate` 或 `max`
    - `meta_value_key` 允许为空
    - 使用 `max` 时不要求必须有 `meta_value_key`，但约定峰值成就应通过 `meta_value_key` 取值

## 对现有内容的影响

- battle rating 奖励逻辑继续保留，但数据源统一到新的战斗统计真源。
- 旧的累计型成就定义不需要迁移；它们继续走 `amount` 累加逻辑。
- 新的战斗峰值成就建议统一走：
  - `event_type = battle_finished`
  - `progress_mode = max`
  - `meta_value_key = stats.<metric_name>`
- 若后续需要“累计承伤”“累计受治疗”“累计行动回合”等战斗统计成就，也统一走：
  - `event_type = battle_finished`
  - `progress_mode = accumulate`
  - `meta_value_key = stats.<metric_name>`

## 测试计划

- 战斗统计回归：
  - 同一行动回合内多次出手会合并到一次 `max_turn_*` 候选值。
  - 新行动回合开始会重置临时统计，不污染下一回合。
  - AOE 技能能正确累计总伤害、总治疗与总击杀。
  - 定时地形效果在有明确施放者时，能记到施放者输出统计。
  - 角色承伤、受治疗、倒地次数会在真实结算点更新，且倒地不会重复记账。
- 成就回归：
  - 旧的 `skill_used` / `enemy_defeated` 累计型成就行为不变。
  - `meta_value_key + accumulate` 能从 `battle_finished.meta.stats.*` 正常累计。
  - `meta_value_key + max` 能按历史峰值解锁。
  - `meta_value_key` 不存在时，本次事件不会错误推进进度。
- 结算回归：
  - `battle_finished` 对每个手动角色只发一次。
  - `battle_won` 只在玩家胜利时发。
  - 战斗中解锁的成就奖励会进入 `PartyState.pending_character_rewards`，并参与战后统一保存。
  - battle rating 熟练度奖励结果与现有行为保持一致。

## 默认约束

- 本方案不新增战斗地图内展示。
- 本方案不统计 AI 我方单位或敌方单位的角色成就战绩。
- 本方案不引入新的复杂条件 DSL；峰值/累计差异仅通过 `progress_mode` 控制。
- 本方案默认 `battle_finished` 是战斗统计型成就的唯一完整汇总事件，避免把完整战绩拆散到多个旧事件里。
