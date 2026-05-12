# 施法时间系统与时间静滞实现方案

本文档是编码前的执行方案，不再作为开放式讨论稿维护。目标是把“非瞬发施法 + 时间静滞”落到当前 Godot 4.6 战斗代码结构中，给后续实现 PR 提供可逐项执行、可审查、可测试的代码计划。

## 目标

M1 交付两个能力：

1. 普通主动技能可以配置 `casting_time_tu > 0`。开始施法后技能进入 pending cast，时间轴继续推进，到点后由技能执行链自动结算。
2. 新增时间系状态语义：`time_stasis` 将目标从个人时间线中隔离，`time_slow` 降低个人节奏，`time_reverberation` 作为被静滞后的反制余波。

硬约束：

- `BattleRuntimeModule` 仍是战斗 command 入口，`preview_command()` / `issue_command()` 的 preview-first 门禁不拆。
- 技能效果仍由 `BattleSkillExecutionOrchestrator` 执行；timeline 只能调度 pending cast 到点，不直接结算技能效果。
- `BattleDamageResolver` 继续只做规则计算，不持有 runtime callback，不主动中断施法。
- 不添加旧存档兼容、旧 payload 兼容、静默字段注入或 fallback migration。当前战斗中保存不支持，pending cast 不进正式 save payload。
- M1 不实现战斗中使用物品命令；当前 `BattleCommand` 只有 move / skill / wait / change_equipment，spell control failure 后的应急行动在 M1 只开放移动和等待。

## 当前归属

必须先读的上下文单元：

- CU-14：`CombatSkillDef`、`SkillContentRegistry`、`BattleSaveContentRules`。
- CU-15：`BattleRuntimeModule`、`BattleTimelineDriver`、`BattleSkillExecutionOrchestrator`、`BattleRuntimeSkillTurnResolver`。
- CU-16：`BattleUnitState`、`BattleState`、`BattleStatusSemanticTable`、`BattleSkillResolutionRules`、`BattleSaveResolver`、`BattleDamageResolver`。
- CU-18 / CU-21：只在展示 pending cast / temporal 状态时读取 HUD、snapshot、text renderer。
- CU-19：新增 focused regression 的承载单元。

本次文档修改不改变 `docs/design/project_context_units.md`。实现 PR 如果改变 runtime 关系、推荐读集或 CU 职责，必须同步更新该文件。

## 设计决策

| 编号 | 决策 | 理由 |
| --- | --- | --- |
| D1 | M1 新增 `BattleCastingTimeSidecar`，pending cast 相关读写都走它 | 避免把 pending cast schema、冷却 anchor、完成排序散落到 runtime / timeline / orchestrator |
| D2 | `BattleUnitState.pending_cast` 保持 runtime-only Dictionary，不进 `TO_DICT_FIELDS` | 当前不支持战斗中保存，避免扩展 save schema；M2 再评估强类型 `BattlePendingCastState` |
| D3 | `BattleState` 保存 runtime-only `next_cast_sequence` 和 `time_stasis_cell_locks` | 序号和格锁是 battle-level 临时状态，不属于单位持久字段 |
| D4 | M1 的 pending cast 只支持普通持久数值成本技能，AP 只作行动门槛 | 读条成功开始后当前行动窗口直接结束；cancel 只返还 MP / stamina / aura，不恢复 AP 或行动窗口 |
| D5 | `TYPE_CANCEL_CAST` 是 runtime interrupt command | pending caster 通常不在 `unit_acting`，取消不能复用当前 active unit command gate |
| D6 | 读条完成时不重查射程、视线或路径 | 当前战斗系统没有遮挡机制；开始时 target/coord 已经由 preview 和 validation 锁定。死亡、离场、`time_stasis` 仍按 binding 规则处理 |
| D7 | `time_stasis_cell_lock` 采用 `BattleState` runtime-only Dictionary | 不改 `BattleCellState` schema，不引入地形回滚；所有 terrain/placement 查询通过 helper 判断锁定格 |
| D8 | M1 不做完整状态过渡总线，但必须集中 temporal 状态释放 hook | 现有状态应用在 `BattleDamageResolver` 内完成。M1 用 sidecar 的 before/after diff 和 owner hook 处理 temporal side effect；M2 再评估通用 StateTransitionGate |

## 数据模型

### CombatSkillDef

文件：`scripts/player/progression/combat_skill_def.gd`

新增导出字段：

```gdscript
@export var casting_time_tu := 0
@export var casting_maintenance_dc := 0
@export var casting_spell_control_dc := 0
@export var pending_cast_binding_mode: StringName = &""
```

新增读取方法：

```gdscript
func get_effective_casting_time_tu(skill_level: int) -> int
func get_effective_casting_maintenance_dc(skill_level: int) -> int
func get_effective_casting_spell_control_dc(skill_level: int) -> int
func get_effective_pending_cast_binding_mode(skill_level: int) -> StringName
```

`level_overrides` 支持同名 key，读取顺序沿用现有 `get_level_override()` 合并规则。`casting_time_tu <= 0` 表示瞬发，走现有路径。

`casting_spell_control_dc` 只影响 `casting_time_tu > 0` 的 readied-cast preflight。默认 0 表示沿用现有 spell fate 语义：只有 `critical_fail` / `critical_success`，普通结果视为 success；大于 0 时，preflight 使用施法者 spell control d20 元数据计算 ordinary failure。

`pending_cast_binding_mode` 只允许：

| 值 | 语义 |
| --- | --- |
| `hard_anchor` | 任一绑定单位目标死亡、离场、进入 `time_stasis` 或变为不可作用，整次读条中断 |
| `soft_anchor` | 失效目标从 `target_unit_ids` 剔除；全部失效才中断 |
| `ground_bind` | 只绑定开始时的 `target_coords`，单位目标失效不影响落地 |

### BattleUnitState

文件：`scripts/systems/battle/core/battle_unit_state.gd`

新增 runtime-only 字段，不加入 `TO_DICT_FIELDS`：

```gdscript
var pending_cast: Dictionary = {}
var turn_casting_exhausted := false
var attempted_spells_this_turn: Dictionary = {}
```

新增 helper：

```gdscript
func is_casting() -> bool
func begin_pending_cast(payload: Dictionary) -> void
func clear_pending_cast() -> Dictionary
func mark_spell_attempted_this_turn(skill_id: StringName) -> void
func has_attempted_spell_this_turn(skill_id: StringName) -> bool
func clear_casting_turn_flags() -> void
```

`clone()` 必须 deep-copy `pending_cast`、`turn_casting_exhausted`、`attempted_spells_this_turn`、`per_battle_charges`、`per_turn_charges`、`per_turn_charge_limits`、`fumble_protection_used`。`to_dict()` / `from_dict()` 必须继续拒绝这些 runtime-only 字段。

### BattleState

文件：`scripts/systems/battle/core/battle_state.gd`

新增 runtime-only 字段：

```gdscript
var next_cast_sequence := 1
var time_stasis_cell_locks: Dictionary = {}
```

新增 helper：

```gdscript
func allocate_cast_sequence() -> int
func lock_time_stasis_cells(unit_state: BattleUnitState) -> void
func unlock_time_stasis_cells(unit_state: BattleUnitState) -> void
func is_time_stasis_cell_locked(coord: Vector2i) -> bool
func get_time_stasis_locked_coords() -> Array[Vector2i]
```

`time_stasis_cell_locks` key 使用稳定字符串 `"x,y"`，value 使用 `unit_id`。锁定格仍算被占用，不替换地形，也不保存。

### Pending Cast Payload

payload 只能由 `BattleCastingTimeSidecar` 生成和解释，其他文件不得裸读 `pending_cast["..."]`。M1 Dictionary schema：

```gdscript
{
	"skill_id": StringName,
	"variant_id": StringName,
	"route": StringName, # unit / ground
	"target_unit_ids": Array[StringName],
	"target_coords": Array[Vector2i],
	"started_at_tu": int,
	"base_casting_time_tu": int,
	"remaining_cast_progress": int, # casting_time_tu * 100
	"estimated_complete_at_tu": int, # snapshot-only estimate
	"cast_transaction": Dictionary,
	"spell_control_context": Dictionary,
	"cast_sequence": int,
	"source_unit_id": StringName,
}
```

权威完成条件是 `remaining_cast_progress <= 0`。`estimated_complete_at_tu` 只给 HUD / snapshot 使用，不参与规则结算，不持久化；每次 snapshot 按当前 `remaining_cast_progress`、施法者 `cast_progress_rate_percent` 和 `BattleTimelineState.current_tu` 重新估算，因此展示文本必须标为 estimate。

### Tags

新增共享常量文件：

- `scripts/player/progression/battle_tag_registry.gd`

职责：

- 管理 `TAG_TIME_STASIS`、`TAG_TIME_SLOW`、`TAG_TIME_REVERBERATION`、`TAG_CAN_TARGET_TIME_STASIS`、`TAG_DISPEL_TIME_STASIS`。
- 提供 `normalize_tag_array(value: Variant) -> Array[StringName]`。
- 提供 params 读取 helper，统一从 `status_entry.params.status_tags` 和 `effect_def.params.effect_tags` 归一化。

M1 不新增 `CombatEffectDef` / `BattleStatusEffectState` 导出字段。tag 仍放在 `params`，但运行时代码只能通过 registry / semantic helper 读取。

## 内容校验

文件：`scripts/player/progression/skill_content_registry.gd`

在 `_append_combat_profile_validation_errors()` 增加硬规则：

- `casting_time_tu`、`casting_maintenance_dc`、`casting_spell_control_dc` 必须为非负 int。
- `casting_time_tu > 0` 时必须是 `TU_GRANULARITY = 5` 的倍数。
- `casting_time_tu > 0` 时 `pending_cast_binding_mode` 必须是 `hard_anchor` / `soft_anchor` / `ground_bind`。
- `casting_time_tu > 0` 时拒绝：
  - `special_resolution_profile_id != ""`
  - `target_selection_mode == "random_chain"`
  - 任一 effect / cast variant effect 的 `effect_type == "charge"`
  - 任一 effect / cast variant effect 的 `effect_type == "path_step_aoe"`
  - 任一 self relocation：`effect_type == "forced_move"` 且 `forced_move_mode in ["jump", "blink"]` 且作用对象是自己
  - `fumble_protection_curve` 非空或 `get_fumble_protection_limit(level) > 0`
  - `params.incompatible_with_casting_time == true`
- `level_overrides` 中的 `casting_time_tu`、`casting_maintenance_dc`、`casting_spell_control_dc`、`pending_cast_binding_mode` 必须通过同一套校验；override 后的 `casting_time_tu > 0` 也触发上述拒绝规则。
- M1 暂不允许 identity-granted per-turn/per-battle charge 技能、misfortune gated 技能、black-contract-push 变体配置 `casting_time_tu > 0`。这些进入 M2 `SkillCostTransaction` 后再开放。
- `params.status_tags`、`params.effect_tags`、`params.save_bonus_by_tag` 必须结构合法：tag 数组元素必须可归一化为非空 `StringName`；`save_bonus_by_tag` 的 key 必须可归一化为非空 `StringName`，value 必须是 int。
- 任一 skill / cast variant 只要含 `dispel_time_stasis` 或 `can_target_time_stasis` effect tag，M1 必须满足 temporal-only 规则：该 skill / variant 只能有一个 executable effect，`effect_type` 只能是 `dispel_magic` 或 `erase_status`，且 params 必须把移除目标限制到 temporal tag / `time_stasis`；不能同列表混入 damage、heal、shield、forced_move、apply_status、terrain、summon 或普通 cleanse。

扩展 `scripts/player/progression/battle_save_content_rules.gd`：

```gdscript
const SAVE_TAG_TIME_STASIS: StringName = &"time_stasis"
const SAVE_ABILITY_HIGHER_OF_CON_OR_WILLPOWER: StringName = &"higher_of_constitution_or_willpower"
```

`VALID_SAVE_TAGS` 和 `VALID_SAVE_ABILITIES` 同步加入上述常量。

## Runtime Sidecar

新增文件：`scripts/systems/battle/runtime/battle_casting_time_sidecar.gd`

基础接口：

```gdscript
class_name BattleCastingTimeSidecar
extends RefCounted

func setup(runtime) -> void
func dispose() -> void

func is_casting_time_skill(skill_def: SkillDef, unit_state: BattleUnitState) -> bool
func get_preview_block_reason(unit_state: BattleUnitState, skill_def: SkillDef, command: BattleCommand) -> String
func begin_casting_time_skill(active_unit: BattleUnitState, command: BattleCommand, batch: BattleEventBatch) -> bool
func cancel_pending_cast(unit_id: StringName, batch: BattleEventBatch) -> bool
func begin_timeline_step(tu_delta: int) -> Dictionary
func advance_pending_casts(tu_delta: int, batch: BattleEventBatch, step_context: Dictionary) -> void
func complete_pending_casts(batch: BattleEventBatch, step_context: Dictionary) -> void
func end_timeline_step(step_context: Dictionary) -> void
func build_pending_cast_resolution_context(unit_state: BattleUnitState, pending_cast: Dictionary) -> Dictionary
func validate_pending_bound_targets(unit_state: BattleUnitState, pending_cast: Dictionary, batch: BattleEventBatch) -> Dictionary

func notify_hp_loss_if_casting(unit_state: BattleUnitState, actual_hp_loss: int, context: Dictionary, batch: BattleEventBatch) -> void
func interrupt_pending_cast(unit_state: BattleUnitState, reason: StringName, context: Dictionary, batch: BattleEventBatch) -> bool
func prune_or_interrupt_invalid_bound_targets(unit_state: BattleUnitState, batch: BattleEventBatch) -> bool

func capture_temporal_status_snapshot(target_unit: BattleUnitState) -> Dictionary
func emit_temporal_status_transitions(target_unit: BattleUnitState, before_snapshot: Dictionary, reason: StringName, batch: BattleEventBatch) -> void
func on_temporal_status_applied(target_unit: BattleUnitState, status_entry: BattleStatusEffectState, batch: BattleEventBatch) -> void
func on_temporal_status_released(target_unit: BattleUnitState, status_entry: BattleStatusEffectState, reason: StringName, batch: BattleEventBatch) -> void
```

`BattleRuntimeModule._ensure_sidecars_ready()` 新建并 setup sidecar。`dispose()` 同步释放。

`build_pending_cast_resolution_context()` 是完成结算唯一入口上下文构造器，负责重新解析 `skill_def`、`skill_level`、`cast_variant`、executable `effect_defs`、绑定 target ids / coords 和 `spell_control_context`。`BattleSkillExecutionOrchestrator` 不直接解释 pending cast payload。

`capture_temporal_status_snapshot()` / `emit_temporal_status_transitions()` 用于 owner 层包住 `BattleDamageResolver`、special resolver、periodic tick、direct erase 等会增删状态的调用。`BattleDamageResolver` 不回调 runtime；owner 在调用前捕获 temporal 状态，调用后 diff，并把新增/移除转给 sidecar。

### 开始施法

`BattleRuntimeModule.issue_command()` 的 skill 分支保持 preview-first。`BattleSkillExecutionOrchestrator._handle_skill_command()` 在现有 block reason 之后、meteor / 普通分支之前插入：

```gdscript
if _runtime._casting_time_sidecar.is_casting_time_skill(skill_def, active_unit):
	_runtime._casting_time_sidecar.begin_casting_time_skill(active_unit, command, batch)
	return
```

M1 要在这里保留注释：

```gdscript
# FIXME(M2): 临时性读条入口。三阶段 validate/commit/apply 管线完成后与瞬发路径合并。
```

begin 流程：

1. 解析 unit / ground cast variant，复用现有 `_validate_unit_skill_targets()` 或 `_validate_ground_skill_command()` 得到锁定目标快照。
2. 执行 readied-cast 专用 spell control preflight。该路径不调用现有 `_resolve_*_spell_control_after_cost()`，而是通过 `BattleDamageResolver.resolve_spell_control_check()` / `BattleHitResolver.resolve_spell_control_metadata()` 取得 metadata，再由 sidecar 解释结果。
3. 普通 spell control failure：
   - 不扣 AP / MP / stamina / aura。
   - 不创建 pending cast。
   - 不启动冷却。
   - 设置 `turn_casting_exhausted = true`，记录 `attempted_spells_this_turn[skill_id] = true`。
   - 不结束当前行动窗口；M1 后续只允许 move / wait。
4. critical failure：
   - 不创建 pending cast。
   - 扣除当前 AP 的 50%，至少 1，最多扣到 0。
   - 先调用 `consume_cooldown_delta_without_turn_start()` 消费旧冷却 elapsed TU，再 `sync_cooldown_anchor_to_current_tu()`，最后 `start_skill_cooldown_from_transaction()` 启动本技能冷却。
   - 设置 `turn_casting_exhausted = true`，记录 `attempted_spells_this_turn[skill_id] = true`；如果 AP 变 0，`issue_command()` 会结束回合，否则本行动窗口仍只能 move / wait。
5. success / critical success：
   - 调用 `BattleRuntimeSkillTurnResolver.consume_skill_costs_without_cooldown()`。
   - 该方法只验证 AP 是否满足技能成本，不扣 AP，不把 AP 写入 `paid_costs`。MP / stamina / aura 立即扣除。
   - 记录 `cast_transaction`，包括 `skill_id`、`paid_costs`、`cooldown_tu`、`refund_policy`。`paid_costs` 只能包含 MP / stamina / aura。
   - `BattleState.allocate_cast_sequence()` 分配序号。
   - `BattleUnitState.begin_pending_cast(payload)`。
   - `active_unit.has_taken_action_this_turn = true`，`active_unit.is_resting = false`，`active_unit.current_ap = 0`，从而让当前行动窗口结束。

`turn_casting_exhausted` 是全局行动门禁，不是 telemetry。它为 true 时，`preview_command()` / `issue_command()` 对该单位只允许 `TYPE_MOVE` 和 `TYPE_WAIT`；拒绝 `TYPE_SKILL`、`TYPE_CHANGE_EQUIPMENT` 和普通 cancel。`attempted_spells_this_turn` 仅用于日志、测试或 M2 UI，不参与 M1 门禁。

spell control preflight 规则改动集中在 rules 层：

- `BattleHitResolver.resolve_spell_control_metadata()` 读取 `attack_context.spell_control_dc`。当 DC > 0 且结果不是 `critical_fail` / `critical_success` / `reverse_fate_downgraded` 时，`effective_hit_roll < spell_control_dc` 记为 `spell_control_resolution = &"failure"`，否则 `&"success"`。
- `BattleDamageResolver.resolve_spell_control_check()` 透传 `spell_control_dc`、`skill_id`、`battle_state` 和测试用 roll override；返回 metadata 中保留 `spell_control_resolution`。
- readied-cast preflight 只解释 `failure`、`critical_fail`、`success`、`critical_success`、`reverse_fate_downgraded`。M1 内容校验拒绝 fumble protection 读条技能，因此 preflight 不调用 `BattleMagicBacklashResolver.apply_spell_control_after_cost()`，也不产生 ground-anchor drift。

取消读条：

- `BattleCommand` 新增 `TYPE_CANCEL_CAST = &"cancel_cast"`。
- `preview_command()` 对 cancel 使用 `command.unit_id` 查 pending caster，不要求该单位是 active unit。
- `issue_command()` 完成 `_state != null` 和 command 基础校验后，必须在 `_state.phase != "unit_acting"` 与 active-unit gate 之前处理 `TYPE_CANCEL_CAST` 并直接 return。允许在 `timeline_running` 或 `unit_acting` 取消，不消耗当前 active unit 的行动。
- cancel 必须复用现有 commandability / player-control 判定；M1 只允许玩家可控 manual party unit 取消自己的 pending cast。未来敌方读条接入后，玩家命令不能按 `unit_id` 免费取消敌方 pending cast。
- cancel 返还 `cast_transaction.paid_costs` 中的持久数值资源；M1 不返还 AP、不恢复已结束的行动窗口，也不把 pending caster 重新塞回 active turn。
- cancel 不启动冷却，写入结构化 report entry：`event_type = "pending_cast_cancelled"`。

### 完成施法

`BattleTimelineDriver._apply_timeline_step()` 的顺序改为：

0. `step_context = _runtime._casting_time_sidecar.begin_timeline_step(tu_delta)`，捕获 step-start 的 `had_time_stasis`、`action_progress_rate_percent`、`cast_progress_rate_percent`、`was_casting`。
1. `current_tu += tu_delta`
2. `_resolve_timeline_status_phase(batch, tu_delta, step_context)`
3. terrain timed effects
4. layered barrier durations
5. `_runtime._casting_time_sidecar.advance_pending_casts(tu_delta, batch, step_context)`
6. `_runtime._casting_time_sidecar.complete_pending_casts(batch, step_context)`
7. `_collect_timeline_ready_units(batch, tu_delta, step_context)`
8. `_runtime._casting_time_sidecar.end_timeline_step(step_context)`

`step_context` 是本 tick 的时间语义快照。`begin_timeline_step()` 初始化 `completed_cast_source_ids = []`，`complete_pending_casts()` 在完成后追加 source unit id。若 `time_stasis` 或 `time_slow` 在第 2 步自然过期，本 tick 仍按 step-start 的冻结 / 减速状态计算；新状态影响从下一次 timeline step 开始。terrain tick、pending cast progress、ready 收集和 cooldown 追算都必须读取该快照，不能在同 tick 因状态已删除而给满额进度。

完成排序：

```text
completed_at_tu asc, cast_sequence asc, source_unit_id asc
```

`BattleSkillExecutionOrchestrator` 新增 no-cost 入口：

```gdscript
func resolve_pending_cast(source_unit: BattleUnitState, resolution_context: Dictionary, batch: BattleEventBatch) -> Dictionary
```

该入口不得调用：

- `_consume_skill_costs()`
- `_resolve_unit_spell_control_after_cost()`
- `_resolve_ground_spell_control_after_cost()`
- `_validate_unit_skill_targets()`
- `_validate_ground_skill_command()`
- `_can_skill_target_unit()` 的 AP / range / LOS 检查

允许复用 effect-only helper：

- `_apply_unit_skill_result()`
- `_apply_ground_unit_effects()`
- `_apply_ground_terrain_effects()`
- `_build_ground_effect_coords()`

完成前检查：

- 施法者仍在 `BattleState.units`。
- 施法者仍存活。
- 施法者没有 `time_stasis`。
- pending cast 仍有合法绑定目标；`hard_anchor` / `soft_anchor` / `ground_bind` 按 schema 清理。
- `BattleCastingTimeSidecar.build_pending_cast_resolution_context()` 能解析出有效 `skill_def`、`cast_variant`、effect_defs 和绑定目标；否则按 `invalid_payload` 中断。

完成后：

- 清除 pending cast。
- 先调用 `consume_cooldown_delta_without_turn_start()` 消费旧冷却 elapsed TU，再 `sync_cooldown_anchor_to_current_tu()`，最后 `start_skill_cooldown_from_transaction()`，避免旧冷却进度丢失或让新冷却吃到开始前的 elapsed TU。
- 写 `pending_cast_completed` report entry。

`_collect_timeline_ready_units(batch, tu_delta)` 对正在读条的单位使用以下规则：

1. 若 `step_context.had_time_stasis[unit_id] == true`，不推进 `action_progress`，也不处理 pending cast；获得 stasis 会在状态应用 hook 中先中断读条。
2. 否则照常按 `tu_delta * step_context.action_progress_rate_percent[unit_id] / 100` 增加 `action_progress`。
3. 若 `step_context.was_casting[unit_id] == true` 或 `step_context.completed_cast_source_ids.has(unit_id)`，每跨过一次 `action_threshold`，先消费 `last_turn_tu -> current_tu` 的 cooldown delta，再同步 cooldown anchor，写 `pending_cast_skipped_ready` report，但不加入 ready 队列、不触发 turn start 状态、不重置 per-turn charges。
4. 读条完成结算发生在 ready 收集之前；同一 tick 内完成的施法者通过 `completed_cast_source_ids` 被视为本 tick 仍在读条，不会立刻获得行动。

HUD / text preview 必须说明“读条完成不重新检查射程或视线；绑定目标失效会按 hard / soft / ground 规则处理中断或剔除”。

### 读条中断

伤害中断由 owner 层调用 sidecar，不能从 `BattleDamageResolver` 回调 runtime。owner 只传实际 HP 损失：

```gdscript
_runtime._casting_time_sidecar.notify_hp_loss_if_casting(target_unit, actual_hp_loss, {
	"source": &"unit_skill",
	"skill_id": skill_def.skill_id,
}, batch)
```

M1 必须覆盖这些 owner：

- `BattleSkillExecutionOrchestrator._apply_unit_skill_result()`
- `BattleSkillExecutionOrchestrator._apply_chain_damage_effects()`
- `BattleRepeatAttackResolver.apply_repeat_attack_skill_result()`
- `BattleGroundEffectService._apply_ground_unit_effects()`
- `BattleRuntimeSkillTurnResolver.apply_unit_status_periodic_ticks()`
- `BattleTerrainEffectSystem.apply_timed_terrain_effect_tick()`
- `BattleChargeResolver._apply_charge_path_step_aoe_effects()` 和 charge fall damage 调用点
- `BattleMeteorSwarmResolver._resolve_target()` / `_apply_concussed_status()`
- `BattleDamageResolver.resolve_fall_damage()` 的调用 owner；当前 collision 没有正式调用点，未来新增 collision caller 时必须同样通知 sidecar
- `BattleBarrierOutcomeResolver` / `BattleBarrierService` 中屏障后仍进入 HP 的结果

检定规则：

| HP 损失 | 结果 |
| --- | --- |
| `<= 3` | 不触发 |
| `4..15` | DC 12 |
| `> 15` | DC 15 |

若 `casting_maintenance_dc > 0`，使用技能配置覆盖动态 DC。能力修正取 CON / WILL 较高者。失败时中断、成本不返还、启动冷却。

非伤害中断直接调用 `interrupt_pending_cast()`，覆盖：

- 施法者死亡 / 离场 / 从 `BattleState.units` 移除。
- 施法者获得 `time_stasis` 或 `blocks_pending_cast` 语义状态。
- 施法者被 forced move / push / pull / teleport / swap / jump / fall / grapple-drag 改变坐标。实际坐标变更 owner 包括 `BattleGroundEffectService`、`BattleChargeResolver`、`BattleMovementService`、`BattleGridService.move_unit()` / `move_unit_force()` 的 runtime 调用点。

死亡 / 离场 cleanup 不要求每个击杀来源各自处理。M1 挂到集中出口：`BattleRuntimeModule._clear_defeated_unit()`、`remove_summoned_unit_from_battle()`、battle end 收尾和 `BattleRuntimeModule.dispose()`。

中断 / 失败成本矩阵：

| 事件 | pending cast | 资源返还 | 冷却 |
| --- | --- | --- | --- |
| player cancel | 清除 | 返还 MP / stamina / aura；不返还 AP | 不启动 |
| ordinary spell control failure | 不创建 | 不扣费 | 不启动 |
| critical spell control failure | 不创建 | 不扣费；扣 AP 惩罚 | 启动 |
| HP maintenance fail | 清除 | 不返还 | 启动 |
| control status / `blocks_pending_cast` | 清除 | 不返还 | 启动 |
| 获得 `time_stasis` | 清除 | 不返还 | 启动 |
| forced movement / fall / swap | 清除 | 不返还 | 启动 |
| `hard_anchor` 目标失效或 `soft_anchor` 全部失效 | 清除 | 不返还 | 启动 |
| 施法者死亡 / battle_end / scene_unload / cleanup | 清除 | 不返还 | 不新启动；已有冷却不处理 |

凡是矩阵中需要启动冷却的路径，都必须按固定顺序执行：`consume_cooldown_delta_without_turn_start()` -> `sync_cooldown_anchor_to_current_tu()` -> `start_skill_cooldown_from_transaction()`。

## Skill Turn Resolver 改动

文件：`scripts/systems/battle/runtime/battle_skill_turn_resolver.gd`

新增方法：

```gdscript
func consume_skill_costs_without_cooldown(
	active_unit: BattleUnitState,
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef,
	batch: BattleEventBatch
) -> Dictionary

func refund_skill_cost_transaction(
	active_unit: BattleUnitState,
	transaction: Dictionary,
	batch: BattleEventBatch
) -> void

func start_skill_cooldown_from_transaction(
	active_unit: BattleUnitState,
	transaction: Dictionary,
	batch: BattleEventBatch
) -> void

func consume_cooldown_delta_without_turn_start(
	unit_state: BattleUnitState,
	batch: BattleEventBatch = null
) -> void

func sync_cooldown_anchor_to_current_tu(
	unit_state: BattleUnitState,
	batch: BattleEventBatch = null
) -> void
```

`consume_skill_costs_without_cooldown()` M1 只扣 MP / stamina / aura，并只验证 AP 是否足够；它不能减少 `current_ap`，不能把 AP 写入 `transaction.paid_costs`。读条成功开始后由 sidecar 统一设置 `current_ap = 0` 表达行动窗口已提交。

该方法必须显式拒绝：

- `_is_black_contract_push_skill(skill_def.skill_id)`
- `MISFORTUNE_SERVICE_SCRIPT.is_misfortune_gated_skill(skill_def.skill_id)`
- identity granted charge skill
- fumble protection 读条技能；内容校验也会提前拒绝

现有 `consume_skill_costs()` 保持不变，瞬发路径行为不变。

`consume_cooldown_delta_without_turn_start()` 只推进技能冷却，不触发 turn start 状态、不重置 per-turn charges、不进入 AI / control status turn resolution。casting unit 跨 action threshold 时调用它，表达“读条耗时让已有冷却自然减少，但施法者没有获得行动窗口”。所有完成 / 中断 / critical failure 这类延迟后启动新冷却的路径，也必须先调用它消费旧 cooldown delta，再同步 anchor 并启动新冷却。`time_stasis` 单位不调用该方法，只同步 anchor。

`BattleTimelineDriver._activate_next_ready_unit()` 在设置行动窗口前调用：

```gdscript
unit_state.clear_casting_turn_flags()
```

## 时间静滞语义

`time_stasis` 是外部时间异常场，持续时间按战场时间减少；目标个人时间线冻结。

文件：`scripts/systems/battle/rules/battle_status_semantic_table.gd`

新增 helper：

```gdscript
static func get_status_tags(status_entry: BattleStatusEffectState) -> Array[StringName]
static func status_has_tag(status_entry: BattleStatusEffectState, tag: StringName) -> bool
static func has_time_stasis(unit_state: BattleUnitState) -> bool
static func has_time_slow(unit_state: BattleUnitState) -> bool
static func blocks_unit_actions(unit_state: BattleUnitState) -> bool
static func freezes_personal_timeline(unit_state: BattleUnitState) -> bool
static func blocks_pending_cast(unit_state: BattleUnitState, pending_cast: Dictionary) -> bool
static func should_advance_status_duration(status_entry: BattleStatusEffectState, owner_unit: BattleUnitState) -> bool
```

`time_stasis` 期间：

- 不加入 ready 队列。
- 不推进 `action_progress`。
- 不触发 turn start、per-turn charge reset、turn start 状态。
- 不推进技能冷却；每个 timeline step 将 `last_turn_tu` 前推到当前 TU，防止解除后补算。
- 不结算普通 DOT / HOT / terrain tick。
- 不降低其他状态 duration。
- 不降低单位私有 shield duration。
- `time_stasis` 自身 duration 正常减少。

格锁接入点：

- `BattleState.lock_time_stasis_cells()` 在 stasis 成功应用后锁定目标 footprint 当前覆盖格；`unlock_time_stasis_cells()` 在 release hook 中释放。
- `BattleGridService.can_place_footprint()`、`collect_blocking_unit_ids()`、`move_unit()`、`move_unit_force()` 以及对应 runtime 调用点必须把 locked coord 视为占用 / 不可穿越。
- `BattleTerrainEffectSystem` 和 ground effect 查询 locked coord 时不对静滞单位结算普通 terrain tick；解除瞬间不补触发 enter-tile 效果，解除后从下一次常规检查恢复。

解除时：

- 同步 `last_turn_tu = current_tu`。
- 解锁 `time_stasis_cell_locks`。
- 仅 `natural_expire` / `dispel` 添加或刷新 `time_reverberation`。
- `death` / `leave_battle` / `battle_end` / `scene_unload` / `replace` / `cleanup` 不触发 `time_reverberation`。

释放 hook 必须覆盖三类路径：

- `BattleRuntimeSkillTurnResolver.advance_unit_status_durations()` 自然过期：删除前把 status entry 传给 sidecar。
- `BattleDamageResolver` / special resolver 间接移除：owner 层用 `capture_temporal_status_snapshot()` 和 `emit_temporal_status_transitions()` 做 before/after diff。
- 死亡、离场、战斗结束、scene unload、cleanup：统一调用 sidecar 的 cleanup release，不添加 `time_reverberation`。

`time_reverberation` 是 M1 防连控状态：

- `params.status_tags` 包含 `time_reverberation`。
- `params.save_bonus_by_tag = {"time_stasis": 4}`。
- `params.reverberation_duration_tu` 默认内容建议 240 TU。
- 不能叠层；再次添加只刷新到 `max(remaining_duration, new_duration)`。
- 不是消耗型状态，不因一次成功豁免而移除。
- 目标已有 `time_reverberation` 时，`time_stasis` 不能再次应用硬控；若 save degree 本应施加 stasis，则降级为 `time_slow`，critical success 仍无效果。

M1 boss 保护：

- 若目标 `attribute_snapshot.boss_target > 0`，不能获得 `time_stasis`。失败或 critical failure 结果降级为 `time_slow`；critical success 仍无效果。
- 当前 enemy `target_rank = elite` 没有稳定写入 battle unit 的独立字段，M1 不做 elite 特判；防连控依靠 `time_reverberation` 和较短 stasis duration。

推荐 `time_stasis` 内容持续时间以 `BattleUnitState.DEFAULT_ACTION_THRESHOLD = 120` 为基准：M1 默认内容建议 60 TU，critical failure 建议 120 TU，最低不低于 60 TU。不要写 3-4 TU；当前 timeline 粒度下那只等于不到一个行动窗口的小片段。

## 时间减速语义

M1 的 `time_slow` 范围收敛为：

- 使用新 status id `time_slow`；现有 `slow` 仍只表达移动成本减速，不改变语义。
- `action_progress` 获取量按 `action_progress_rate_percent` 缩放，默认 50。
- pending cast `remaining_cast_progress` 获取量按 `cast_progress_rate_percent` 缩放，默认 50。

M1 不改变 cooldown、DOT/HOT、shield duration、terrain、barrier 的计时方式。若要让 `time_slow` 同时影响 cooldown / resource recovery，必须在 M2 先重构 personal timer advancement，避免和当前 `last_turn_tu` 回合追算模型双算。

## 目标过滤

文件：`scripts/systems/battle/rules/battle_skill_resolution_rules.gd`

扩展签名：

```gdscript
func is_unit_valid_for_effect(
	source_unit: BattleUnitState,
	target_unit: BattleUnitState,
	target_team_filter: StringName,
	effect_def: CombatEffectDef = null,
	context: Dictionary = {}
) -> bool
```

规则：

- `target_unit == null` 或死亡，仍拒绝。
- 目标有 `time_stasis` 且 effect 没有 `can_target_time_stasis` / `dispel_time_stasis`，拒绝。
- 缺少 `effect_def` 时默认 fail closed，不能打穿静滞隔离。
- 带 `dispel_time_stasis` 的效果只能移除/缩短 temporal 状态，不能顺带伤害、治疗、位移或套其他普通效果。

为避免 preview 阶段因为 `effect_def = null` 把合法解控技能也拒掉，rules 层同时新增 skill-level helper：

```gdscript
func can_skill_target_time_stasis(
	source_unit: BattleUnitState,
	target_unit: BattleUnitState,
	skill_def: SkillDef,
	cast_variant: CombatCastVariantDef = null
) -> bool
```

`_can_skill_target_unit()` 遇到 stasis 目标时只调用这个 helper。helper 必须执行 temporal-only 校验：至少一个 executable effect 带 `dispel_time_stasis`，且所有 executable effects 都是允许的 temporal dispel effect。实际效果应用时仍按 `is_unit_valid_for_effect(..., effect_def, context)` fail closed。

需要同步调整 call sites：

- `BattleSkillExecutionOrchestrator._is_unit_valid_for_effect()`
- `_can_skill_target_unit()`
- `_collect_ground_preview_unit_ids()`
- `_apply_ground_unit_effects()`
- chain / random_chain target pool
- repeat attack resolver
- charge path step AoE
- meteor swarm target/effect collection

M1 不修改 AI action helper / enemy action 评分；在引入敌方读条或静滞解控 AI 前，runtime preview / issue path 必须 fail closed，不能让 AI 命令绕过 stasis 过滤。AI 自行筛目标和评分进入 M2。

## 豁免扩展

文件：`scripts/systems/battle/rules/battle_save_resolver.gd`

返回结果新增字段，旧调用方继续用 `.get()`：

```gdscript
"degree": StringName # critical_failure / failure / success / critical_success
```

规则：

- total < DC 为 failure，否则 success。
- natural 1 降一级，natural 20 升一级。
- 最低 `critical_failure`，最高 `critical_success`。

`SAVE_ABILITY_HIGHER_OF_CON_OR_WILLPOWER` 读取目标 CON / WILL 修正较高者。`time_reverberation` 的 +4 通过 `status_entry.params.save_bonus_by_tag = {"time_stasis": 4}` 实现，只对当前 save tag 生效。

`BattleSaveResolver` 在计算 save total 时新增状态 bonus 扫描：

- 对目标所有 status 读取 `params.save_bonus_by_tag`。
- key 通过 `BattleTagRegistry` 归一化为 `StringName`。
- 仅当 key 等于当前 `save_tag` 时加入 bonus；不同 tag 不串用。
- value 必须是 int，内容校验已保证结构合法。

时间静滞技能效果层根据 `degree` 决定：

| degree | 效果 |
| --- | --- |
| critical_failure | `time_stasis`，使用 critical failure duration |
| failure | `time_stasis`，使用 failure duration |
| success | `time_slow`，使用 success slow duration |
| critical_success | 无效果 |

然后再应用 M1 防护降级：

- 目标是 boss 或已有 `time_reverberation` 时，`critical_failure` / `failure` 的 `time_stasis` 结果降级为 `time_slow`。
- 降级后的 slow 使用 effect params 中的 `stasis_downgrade_slow_duration_tu`，缺省使用 success slow duration。
- `time_reverberation` 的 +4 save bonus 即使本次结果被降级也仍参与 save total。

## UI / Snapshot / Headless

M1 最小展示：

- `scripts/systems/battle/presentation/battle_hud_adapter.gd` 在 active unit / visible unit summary 中加入 `pending_cast` 摘要：`skill_id`、`remaining_cast_progress`、`estimated_complete_at_tu`、`can_cancel`。
- `scripts/systems/game_runtime/game_runtime_snapshot_builder.gd` 和 `scripts/utils/game_text_snapshot_renderer.gd` 增加只读 pending cast / temporal status 输出。
- snapshot key 固定为：
  - `battle.units[*].pending_cast`
  - `battle.units[*].temporal_statuses`
  - `battle.runtime_only.pending_casts_visible`
  - `battle.save_locked`
- text renderer 输出固定前缀：
  - `[PENDING_CAST] unit=<id> skill=<skill_id> remaining=<progress> eta=<estimate> runtime_only=true`
  - `[TEMPORAL] unit=<id> statuses=<status_ids> runtime_only=true`
- `scripts/systems/game_runtime/headless/game_text_command_runner.gd` 新增 `battle cancel_cast <unit_id>`，经 facade 传入 `BattleCommand.TYPE_CANCEL_CAST`。

M1 不要求正式玩家 UI 新增复杂取消面板。必须保证 runtime command、headless command 和 focused tests 可触发 cancel；若 HUD 暴露 `can_cancel` 但没有接入按钮，只能显示只读状态，不能展示一个不可用的可点击控件。

## 文件改动清单

M1 新增：

- `scripts/player/progression/battle_tag_registry.gd`
- `scripts/systems/battle/runtime/battle_casting_time_sidecar.gd`
- `tests/battle_runtime/runtime/run_casting_time_core_regression.gd`
- `tests/battle_runtime/runtime/run_casting_time_interruption_regression.gd`
- `tests/battle_runtime/runtime/run_temporal_status_semantics_regression.gd`
- `tests/text_runtime/commands/run_casting_time_text_command_regression.gd`
- `tests/battle_runtime/skills/run_time_stasis_regression.gd`

M1 修改：

- `scripts/player/progression/combat_skill_def.gd`
- `scripts/player/progression/skill_content_registry.gd`
- `scripts/player/progression/battle_save_content_rules.gd`
- `scripts/systems/battle/core/battle_command.gd`
- `scripts/systems/battle/core/battle_preview.gd`
- `scripts/systems/battle/core/battle_state.gd`
- `scripts/systems/battle/core/battle_unit_state.gd`
- `scripts/systems/battle/runtime/battle_runtime_module.gd`
- `scripts/systems/battle/runtime/battle_timeline_driver.gd`
- `scripts/systems/battle/runtime/battle_skill_execution_orchestrator.gd`
- `scripts/systems/battle/runtime/battle_skill_turn_resolver.gd`
- `scripts/systems/battle/runtime/battle_ground_effect_service.gd`
- `scripts/systems/battle/runtime/battle_repeat_attack_resolver.gd`
- `scripts/systems/battle/runtime/battle_charge_resolver.gd`
- `scripts/systems/battle/runtime/battle_meteor_swarm_resolver.gd`
- `scripts/systems/battle/runtime/battle_movement_service.gd`
- `scripts/systems/battle/runtime/battle_special_skill_resolver.gd`
- `scripts/systems/battle/runtime/battle_barrier_service.gd`
- `scripts/systems/battle/runtime/battle_barrier_outcome_resolver.gd`
- `scripts/systems/battle/runtime/battle_layered_barrier_service.gd`
- `scripts/systems/battle/terrain/battle_grid_service.gd`
- `scripts/systems/battle/terrain/battle_terrain_effect_system.gd`
- `scripts/systems/battle/rules/battle_status_semantic_table.gd`
- `scripts/systems/battle/rules/battle_skill_resolution_rules.gd`
- `scripts/systems/battle/rules/battle_save_resolver.gd`
- `scripts/systems/battle/rules/battle_damage_resolver.gd`
- `scripts/systems/battle/rules/battle_hit_resolver.gd`
- `scripts/systems/battle/presentation/battle_hud_adapter.gd`
- `scripts/systems/game_runtime/game_runtime_snapshot_builder.gd`
- `scripts/utils/game_text_snapshot_renderer.gd`
- `tests/runtime/validation/run_resource_validation_regression.gd`
- `tests/battle_runtime/state_schema/run_battle_unit_state_schema_regression.gd`
- `tests/battle_runtime/runtime/run_battle_save_resolver_regression.gd`

M1 内容数据：

- `data/configs/skills/mage_time_stasis.tres`
- `SkillDef.tags` 至少包含 `mage`、`magic`、`control`、`temporal`。
- `SkillDef.learn_source = &"book"`，M1 只保证资源可加载、可被测试直接授予、可通过 registry 校验；正式职业授予 / 商店 / 掉落可获得性列入 M2。

M1 headless cancel 接入：

- `scripts/systems/game_runtime/headless/game_text_command_runner.gd`
- `scripts/systems/game_runtime/game_runtime_facade.gd`
- `scripts/systems/game_runtime/battle_session_facade.gd`

M2 / 后续才修改：

- `scripts/enemies/actions/*.gd`：AI 读条评分和主动规避。
- `scripts/systems/battle/ai/*.gd`：AI 估值完整接入。
- `scripts/player/warehouse/*`：仅当开放 spell failure 后使用非攻击性道具。
- `docs/design/technical_debt.md`：若文件尚不存在，实现 PR 创建并记录三阶段管线债务。

## 实施顺序

### M0：内容与常量地基

1. 新增 `battle_tag_registry.gd`。
2. 扩展 `CombatSkillDef` 字段和 getter。
3. 扩展 `BattleSaveContentRules`、`BattleSaveResolver.degree` 和 `save_bonus_by_tag`。
4. 扩展 `BattleHitResolver.resolve_spell_control_metadata()` / `BattleDamageResolver.resolve_spell_control_check()`，支持 readied-cast `spell_control_dc` ordinary failure。
5. 扩展 `SkillContentRegistry` 硬规则。
6. 跑：

```bash
godot --headless --script tests/runtime/validation/run_resource_validation_regression.gd
godot --headless --script tests/battle_runtime/runtime/run_battle_save_resolver_regression.gd
```

### M1A：runtime-only 状态

1. `BattleUnitState` 增加 pending cast / casting failure flags / helper / clone deep copy。
2. `BattleState` 增加 cast sequence 和 cell lock helper。
3. `BattleCommand` 增加 `TYPE_CANCEL_CAST`。
4. `BattlePreview` 增加可选字段：

```gdscript
var pending_cast_preview: Dictionary = {}
var command_block_reason_id: StringName = &""
```

5. 跑：

```bash
godot --headless --script tests/battle_runtime/state_schema/run_battle_unit_state_schema_regression.gd
```

### M1B：开始 / 取消 pending cast

1. 新增 `BattleCastingTimeSidecar`。
2. `BattleRuntimeModule` setup sidecar，并在 cancel command 上绕过 active unit gate。
3. `BattleSkillExecutionOrchestrator._handle_skill_command()` 插入 readied-cast 分支。
4. `BattleRuntimeSkillTurnResolver` 增加 no-cooldown 持久资源事务，AP 只校验不扣除。
5. `preview_command()` / `issue_command()` 接入 `turn_casting_exhausted` move / wait 门禁。
6. 测试 begin、failure、critical failure、cancel、refund、no cooldown。

### M1C：timeline 推进 / 完成结算

1. `BattleTimelineDriver._apply_timeline_step()` 插入 pending cast advance/complete。
2. `_collect_timeline_ready_units()` 对 casting unit 消耗 action threshold 但不入 ready。
3. `BattleCastingTimeSidecar.build_pending_cast_resolution_context()` 解析 payload 到 no-cost 结算上下文。
4. `BattleSkillExecutionOrchestrator.resolve_pending_cast()` 实现 no-cost 结算。
5. 完成、中断、critical failure 启动冷却前都先消费旧 cooldown delta，再同步 cooldown anchor。
6. 测试完整 begin -> advance -> complete -> cooldown。

### M1D：中断、time_stasis、target filtering

1. owner 层接入 `notify_hp_loss_if_casting()`。
2. `BattleStatusSemanticTable` 增加 temporal helper。
3. sidecar 增加 temporal status before/after diff，owner 层包住 DamageResolver / special resolver / periodic tick / direct erase。
4. `BattleSkillResolutionRules.is_unit_valid_for_effect()` 扩展 effect context，并新增 `can_skill_target_time_stasis()`。
5. terrain / grid placement / forced movement / charge / repeat / chain / meteor / barrier 调用点接入静滞过滤和中断。
6. `BattleRuntimeSkillTurnResolver.apply_unit_status_periodic_ticks()` 和 `advance_unit_status_durations()` 跳过静滞单位的个人计时，只推进 `time_stasis` 自身。
7. 测试 stasis 应用、解除、锁格、reverberation、boss 降级、slow。

### M1E：展示与 headless

1. HUD adapter 输出 pending cast 摘要。
2. snapshot / text renderer 输出 temporal 状态。
3. facade 和 text command 增加 cancel cast；focused tests 同时直接覆盖 `BattleCommand.TYPE_CANCEL_CAST` runtime path。
4. 新增 text command regression 覆盖 `battle cancel_cast <unit_id>` 从 `GameTextCommandRunner` 经 facade 到 runtime 的完整链路。

### M1F：文档与上下文地图

1. 更新 `docs/design/project_context_units.md`，写入新的 sidecar、runtime-only 字段、测试读集。
2. 若开始实现 M2 债务，创建/更新 `docs/design/technical_debt.md`。

## 测试计划

新增 focused runner：

```bash
godot --headless --script tests/battle_runtime/runtime/run_casting_time_core_regression.gd
godot --headless --script tests/battle_runtime/runtime/run_casting_time_interruption_regression.gd
godot --headless --script tests/battle_runtime/runtime/run_temporal_status_semantics_regression.gd
godot --headless --script tests/battle_runtime/runtime/run_battle_save_resolver_regression.gd
godot --headless --script tests/text_runtime/commands/run_casting_time_text_command_regression.gd
godot --headless --script tests/battle_runtime/skills/run_time_stasis_regression.gd
godot --headless --script tests/runtime/validation/run_resource_validation_regression.gd
godot --headless --script tests/battle_runtime/state_schema/run_battle_unit_state_schema_regression.gd
```

核心断言：

- `casting_time_tu <= 0` 的技能仍走瞬发路径。
- begin pending cast 扣 MP / stamina / aura 但不扣 AP、不启动冷却；成功开始后 `current_ap = 0`。
- ordinary spell control failure 不扣费、不建 pending、不结束当前行动窗口，只允许 move / wait。
- critical failure 扣 AP 惩罚、启动冷却、不建 pending。
- cancel 清 pending、返还持久数值成本、不启动冷却、不恢复已结束行动窗口。
- timeline 推进按 `remaining_cast_progress` 完成，不看 absolute complete TU。
- 同一 TU 多个 pending cast 按 `cast_sequence` 稳定结算。
- casting unit 跨 action threshold 时不入 ready；已有冷却会减少，且 `last_turn_tu` 不双算冷却。
- 完成、中断、critical failure 启动新冷却前先消费旧 cooldown delta，不丢失读条期间已有冷却进度。
- HP loss `<=3` 不检定，`4..15` DC 12，`>15` DC 15，失败中断并启动冷却。
- forced movement、死亡、离场、`time_stasis` 会中断 pending cast。
- terrain tick、charge path/fall、meteor swarm、barrier overflow HP damage 都会通知 sidecar 并按规则中断 pending cast。
- `hard_anchor` / `soft_anchor` / `ground_bind` 行为分别正确。
- `time_stasis` 目标不进 ready、不推进个人计时、不受普通 unit effect / terrain tick / forced movement。
- `time_stasis` 目标不吃 meteor swarm 普通伤害 / concussed status。
- `time_stasis` 自身 duration 正常减少；解除后解锁格子并添加 `time_reverberation`。
- `death` / `battle_end` / cleanup 解除 stasis 不添加 `time_reverberation`。
- `time_reverberation` 只给 `time_stasis` save tag 加值，且刷新不叠层；存在 reverberation 时新 stasis 降级为 slow。
- boss 目标不会获得 `time_stasis`，失败结果降级为 `time_slow`。
- `time_slow` 新 status id 只影响 action progress 和 pending cast progress；旧 `slow` 仍只影响移动语义。
- `dispel_time_stasis` skill / variant 的 temporal-only 校验拒绝混合伤害、治疗、位移或普通状态。
- snapshot / text renderer 输出固定 pending cast / temporal keys，且标记 runtime-only。
- `battle cancel_cast <unit_id>` 文本命令经 `GameRuntimeFacade -> BattleSessionFacade -> BattleRuntimeModule` 触发 cancel、返还资源并在 snapshot/report 中可见。
- `BattleUnitState.to_dict()` 不包含 pending cast；`clone()` 保留 pending cast 和 `fumble_protection_used`。

实现 PR 完成前必须至少跑：

```bash
python tests/run_regression_suite.py
godot --headless --script tests/battle_runtime/runtime/run_battle_runtime_smoke.gd
godot --headless --script tests/battle_runtime/rendering/run_battle_board_regression.gd
godot --headless --script tests/battle_runtime/runtime/run_casting_time_core_regression.gd
godot --headless --script tests/battle_runtime/runtime/run_casting_time_interruption_regression.gd
godot --headless --script tests/battle_runtime/runtime/run_temporal_status_semantics_regression.gd
godot --headless --script tests/battle_runtime/runtime/run_battle_save_resolver_regression.gd
godot --headless --script tests/text_runtime/commands/run_casting_time_text_command_regression.gd
godot --headless --script tests/battle_runtime/skills/run_time_stasis_regression.gd
```

不要把 battle simulation / balance runner 混进默认“全量测试”。

## M2 追踪项

M2 才处理：

- `BattlePendingCastState` 强类型资源或 RefCounted。
- `SkillCostTransaction` 支持 identity charges、misfortune gate、black contract、材料成本。
- 瞬发和读条统一三阶段管线：`validate -> commit_costs -> apply_effects`。
- `time_slow` 影响 cooldown / resource recovery 的 personal timer 重构。
- 通用 `StateTransitionGate`，替代 M1 owner hook。
- AI 读条评分、威胁区读条风险、敌方读条技能。
- 正式玩家 cancel UI 和 AI 对 pending cast 的主动打断 / 规避策略。
- spell control failure 后允许非攻击性道具，需要先设计 battle item command 和 `ItemDef` 标签白名单。

## 合并门槛

- 没有新增兼容逻辑或旧 schema fallback。
- 瞬发技能测试保持通过。
- `BattleDamageResolver` 没有 runtime callback。
- pending cast schema 只有 sidecar 裸读。
- `BattleState.time_stasis_cell_locks` 不进入正式保存。
- 所有新增状态/effect tag 都通过 registry helper 归一化。
- 完成、中断、critical failure 启动冷却前都先消费旧 cooldown delta，再同步 cooldown anchor。
- `time_stasis` 释放路径都有明确 reason，只有 natural expire / dispel 添加 reverberation。
- 实现 PR 同步更新 `docs/design/project_context_units.md`。
