# 祈愿术（Wish）可执行代码方案

## 目标

把“祈愿术，9环”实现为一个 9 环万能法术技能，施法消耗 1 个主要动作，每场战斗每名角色最多使用 1 次。技能在施放时选择 1 个愿望模式，并通过战斗运行时真实改写状态，而不是只写日志或临时提示。

本方案遵守当前仓库边界：

- 本文档是实现前的可执行代码方案，不表示相关 `wish` 源文件和测试 runner 已经存在。
- 下文列出的“新增文件/新增测试”都是实现任务的一部分；实现完成后这些路径必须落地并通过验证。
- 采纳 Kimi 审查后的最终范围为 **MVP + 二期**：
  - **MVP**：`copy_spell`、`reverse_casualty`、`team_transfer`、`absolute_sanctuary`、`reality_repair`、`break_desperation`（仅当前 active unit）。
  - **二期**：`fate_rewrite`、`battle_reset`、`hostile_denial`、任意单位 `break_desperation`、完整 reaction/replay/rollback、外部副作用延迟。
- MVP 不实现完整时间旅行系统，不改写永久 progression / achievement / battle rating / post-battle reward 的提交路径。
- 先接入现有 `BattleRuntimeModule` / `BattleSkillExecutionOrchestrator` / special profile 架构。
- 不新增旧 payload/schema 兼容逻辑、 legacy alias 或 fallback migration。
- 新增的战斗历史、每战使用次数、愿望反应窗口均为战斗内瞬态状态，不写入长期存档。
- 代码完成后，如果运行时关系或推荐读集改变，需要同步更新 `docs/design/project_context_units.md`。

## 当前系统结论

已读取 `docs/design/project_context_units.md`，祈愿术主要落在：

- `CU-15 BattleRuntimeModule`：运行时状态改写、特殊技能、结算提交。
- `CU-16 Battle Runtime Rules And Elemental Support`：伤害、状态、地形、命中/检定语义。
- `CU-18 Battle Presentation And Targeting`：HUD 预览、目标选择、命令载荷。
- `CU-19 Battle Runtime Regression Tests`：战斗运行时和技能回归。

当前代码的关键事实：

- `CombatSkillDef.special_resolution_profile_id` 已经被陨石雨使用，适合承载祈愿术这种跨系统技能。
- `SkillContentRegistry.VALID_SPECIAL_RESOLUTION_PROFILE_IDS` 目前只允许 `meteor_swarm`，需要加入 `wish`。
- `BattleSkillExecutionOrchestrator` 目前对特殊档案是硬编码分发，需要新增 `wish` 分支，或在同一次改动中抽出小型 special profile dispatcher。
- `BattleCommand` 目前只有目标单位/坐标/变体等固定字段，没有任意特殊载荷；祈愿术需要新增 `special_payload: Dictionary`，并用专用 validator 约束字段。
- 当前没有“刚刚发生事件”的统一反应历史，也没有“上一己方回合结束”的快照。命运改写、逆转伤亡、战局重置、敌意否决必须新增战斗内历史服务。
- `BattleUnitState.per_battle_charges` 已存在但语义是“剩余次数”，不适合直接承载通用每战施法次数；祈愿术需要新增已使用次数语义。
- 当前技能数据没有法术环阶字段。祈愿术需要新增明确的愿望交互等级，不能靠名称或标签猜测 8/9 环。

## 方案选择

采用 **special profile + typed payload + 愿望 resolver + 分期 sidecar**。

MVP 只实现不依赖完整 reaction rollback 的 6 个模式。MVP 需要最小 sidecar：死亡记录、友方回合结束 serial、每战施法次数、庇护到期、当前 active unit 的额外动作窗口。二期才引入完整 `BattleWishHistoryService` reaction event、确定性重放、状态快照和外部副作用延迟。

不把祈愿术拆成普通 `CombatEffectDef` 的原因：

- 9 个模式会同时触达复活、状态清除、地图位移、地形、反应事件撤销、复制施法和额外动作。
- 普通 effect pipeline 无法表达复制施法、跨单位传送和庇护抵消；二期的“刚刚发生事件改写”和“上一己方回合结束快照”更需要 special profile 管控。
- special profile 已经有 manifest、gate、资源验证和回归模式，适合承载这种高风险技能。

## 新增数据契约

### SkillDef 扩展

文件：`scripts/player/progression/skill_def.gd`

新增字段：

```gdscript
@export var wish_interaction_rank: int = 0
@export var wish_copyable: bool = false
@export var wish_deniable: bool = true
@export var wish_forbidden_tags: Array[StringName] = []
```

语义：

- `wish_interaction_rank = 0`：非环阶/非愿望交互对象。
- `1..8`：可被敌意否决直接取消；如果 `wish_copyable == true` 且没有禁用标签，也可被复制。
- `9`：敌意否决需要愿望对抗检定；不可被复制。
- `wish_forbidden_tags` 使用固定值：`legendary_ritual`、`permanent_creation`、`non_replicable_special_profile`。

资源验证：

- `wish_interaction_rank` 必须在 `0..9`。
- `wish_copyable == true` 时必须满足 `1 <= wish_interaction_rank <= 8`。
- `wish_copyable == true` 时不能包含 `legendary_ritual` 或 `permanent_creation`。
- `special_resolution_profile_id != &""` 的技能在本版祈愿术中一律不可复制；后续若要放行某个特殊档案，需要为该 profile 单独补目标、预览和提交契约审查。

### CombatSkillDef 扩展

文件：`scripts/player/progression/combat_skill_def.gd`

新增字段：

```gdscript
@export var per_battle_cast_limit: int = 0
```

语义：

- `0` 表示无限制。
- `1` 用于祈愿术。
- 本版只允许 `special_resolution_profile_id == &"wish"` 的技能设置非 0 值；资源验证必须拒绝其他技能设置 `per_battle_cast_limit > 0`。
- 不复用 `BattleUnitState.per_battle_charges`，因为现有身份技能逻辑把它当“剩余次数”且只在 key 已存在时生效。
- 新增 `BattleUnitState.per_battle_cast_counts: Dictionary = {}` 作为瞬态已使用次数，key 为 `skill_id`，缺 key 等价于已使用 `0` 次。
- `per_battle_cast_counts` 不加入长期 `BattleUnitState.TO_DICT_FIELDS`；如果未来 battle snapshot 必须持久化它，需要先确认是否接受破坏性 schema 变更。

运行时接入：

- `BattleRuntimeSkillTurnResolver.get_skill_cast_block_reason()` 在 AP/MP/冷却等检查同层追加每战次数检查：`used_count >= per_battle_cast_limit` 时返回 block reason。
- `BattleRuntimeSkillTurnResolver.consume_skill_costs()` 不直接递增该计数；祈愿术通过 `BattleWishCommitTransaction` 在“验证完成、即将提交”时递增，提交失败则回滚。
- 如果未来要让普通技能也使用该字段，必须先补普通 unit/ground/random_chain/meteor 路径的统一递增/回滚钩子和测试矩阵。
- `BattleRuntimeSkillTurnResolver` 暴露同源 helper，例如 `_get_per_battle_cast_limit_block_reason(unit, skill_def)`，HUD preview 和 execute 都调用它。
- `BattleHudAdapter` 的技能槽可用性同步读取该 block reason，避免 UI 显示可用但运行时拒绝。

### 目标选择模式扩展

祈愿术需要新增目标选择模式：

```gdscript
const TARGET_SELECTION_SPECIAL_PAYLOAD := &"special_payload"
```

接入点：

- `BattleTargetCollectionService`：该模式不从普通单格/单体规则推导目标，而是把命令交给 `WishCommandPayload` 校验。
- `BattleHudAdapter`：选中祈愿术时暴露 `selected_skill_target_selection_mode = "special_payload"`，并提供 9 种愿望的 payload 草案。
- `BattleBoardController`：根据当前愿望模式切换目标选择交互；模式 2 和模式 9 选择 reaction event，其他模式选择单位/坐标/区域。
- `GameRuntimeBattleSelection` / `battle_session_facade.gd`：保留、预览并提交 `special_payload`，让 headless 和自动化路径不依赖手动 UI。
- `BattleAIActionAssembler`：默认不自动选择祈愿术，除非后续显式加入 AI 愿望策略；但必须能识别并跳过 `special_payload` 技能，避免构造坏命令。
- 资源验证不允许普通技能误用 `special_payload`；只有 `special_resolution_profile_id == &"wish"` 的技能可以使用。

### WishProfile

新增文件：

- `scripts/systems/battle/core/wish/wish_profile.gd`
- `data/configs/skill_special_profiles/profiles/wish_profile.tres`
- `data/configs/skill_special_profiles/manifests/wish_special_profile_manifest.tres`

建议字段：

```gdscript
class_name WishProfile
extends Resource

@export var profile_id: StringName = &"wish"
@export var schema_version: int = 1
@export var skill_id: StringName = &"wish"
@export var max_team_transfer_targets: int = 6
@export var team_transfer_max_distance: int = 12
@export var sanctuary_area_size: Vector2i = Vector2i(5, 5)
@export var repair_area_size: Vector2i = Vector2i(7, 7)
@export var revive_hp_ratio: float = 0.5
@export var revive_window_friendly_turn_ends: int = 3
@export var major_damage_ratio_threshold: float = 0.35
@export var sanctuary_status_id: StringName = &"wish_sanctuary"
@export var extra_action_status_id: StringName = &"wish_extra_main_action"
@export var rank_lock_status_id: StringName = &"wish_no_rank_9_cast"
@export var beneficial_terrain_ids: Array[StringName] = [&"scrub", &"high_ground"]
@export var removable_hazard_effect_ids: Array[StringName] = []
```

Manifest：

- `profile_id = &"wish"`
- `owning_skill_ids = [&"wish"]`
- `runtime_resolver_id = &"wish"`
- `required_regression_tests` 至少列出：
  - `tests/runtime/validation/run_resource_validation_regression.gd`
  - `tests/progression/schema/run_skill_schema_regression.gd`
  - `tests/progression/schema/run_wish_schema_regression.gd`
  - `tests/battle_runtime/runtime/run_battle_skill_protocol_regression.gd`
  - MVP 的每战一次、payload、复制、复活、传送、庇护、地形、当前 active unit 额外动作专项测试脚本。
  - 二期实现时再补 reaction event、重置、命运改写、敌意否决专项测试脚本。
- `BattleSpecialProfileManifestValidator` 对 `WishProfile` 做强校验：区域尺寸必须为正奇数，ratio 必须在 `0..1`，status id 不为空，beneficial terrain 必须来自普通可通行地形白名单，required tests 必须存在。

### Wish 命令载荷

文件：`scripts/systems/battle/core/battle_command.gd`

新增：

```gdscript
var special_payload: Dictionary = {}
```

新增 validator/helper：

- `scripts/systems/battle/core/wish/wish_command_payload.gd`

通用字段：

```gdscript
{
	"wish_mode": StringName,
	"target_unit_id": StringName,
	"target_unit_ids": Array[StringName],
	"target_coord": Vector2i,
	"target_coords": Array[Vector2i],
	"reaction_event_id": int,
	"copy_skill_id": StringName,
	"copy_payload": Dictionary,
	"terrain_effect_id": StringName,
	"terrain_id": StringName,
	"grade_delta": int
}
```

允许的 `wish_mode`：

- MVP 启用：`copy_spell`、`reverse_casualty`、`team_transfer`、`absolute_sanctuary`、`reality_repair`、`break_desperation`。
- 二期启用：`fate_rewrite`、`battle_reset`、`hostile_denial`，以及任意单位版 `break_desperation`。
- MVP 阶段如果收到二期模式，validator 必须返回明确失败原因，不消耗 AP/MP/每战次数。

Validator 负责：

- 不允许未知字段静默通过。
- 按模式校验必需字段。
- 坐标数组和目标数组长度必须匹配。
- `copy_payload` 只能包含被复制技能运行时需要的目标字段，不能包含 `wish_mode`、`special_payload`、`grade_delta`、`reaction_event_id` 或其他敏感字段，避免祈愿术复制祈愿术或嵌套特殊载荷。
- 对外保留 `BattleCommand.special_payload` 作为 UI/headless/facade 传输字典，对内必须先调用 `WishCommandPayload.from_command(command)` 或 `BattleCommand.get_wish_payload_or_error()` 转成 typed DTO；resolver 和 committer 不直接读取裸 `Dictionary`。
- DTO 构建时统一把 String/StringName 归一为 `StringName`，缺失或空 payload 安全失败，不做旧 payload fallback。
- `WishCommandPayload.to_internal_command_for_copy(copied_skill_def)` 只按被复制技能的 `target_selection_mode` 白名单生成内部 `BattleCommand`，例如 `single_unit` 只允许 `target_unit_id`，`single_coord` 只允许 `target_coord`，`multi_unit` 只允许 `target_unit_ids`。

## 运行时新增模块

### BattleWishBattleWindowService / BattleWishHistoryService

MVP 新增文件：`scripts/systems/battle/runtime/battle_wish_battle_window_service.gd`

MVP 职责：

- 记录 3 个友方回合结束窗口内的死亡事件。
- 记录每个阵营的 `friendly_turn_end_serial`，供复活窗口和庇护到期使用。
- 记录庇护状态的到期 serial。
- 在 `BattleRuntimeModule.start_battle()` 开头清空所有战斗内状态；`dispose()` 释放引用；`_ensure_sidecars_ready()` 重新 setup。

接入点：

- `BattleRuntimeModule.setup()` / `start_battle()` / dispose：初始化、重置和释放服务。
- `BattleTimelineDriver._end_active_turn(batch)`：通过语义 hook 调用 `advance_friendly_turn_end_serial(faction_id)`；通用模块不写 `wish` 专名。
- `_clear_defeated_unit(unit_state, batch)`：记录清格前单位状态和清格后 occupancy 结果，保存死亡坐标、状态、死亡时的 friendly turn-end serial。

二期新增文件：`scripts/systems/battle/runtime/battle_wish_history_service.gd`

二期职责：

- 记录“刚刚发生”的可反应事件。
- 为 `fate_rewrite`、`battle_reset`、`hostile_denial` 保存纯数据 snapshot / delta，允许完整回滚最近一次可交互事件。
- 记录确定性重放输入和支持的检定类型。

二期事务范围：

- `begin_current_event()` 必须早于 `BattleRuntimeSkillTurnResolver.consume_skill_costs()`，因为 AP/MP/cooldown 也需要被敌意否决或命运改写回滚。
- 任何验证/提交失败都必须显式调用 `cancel_current_event()`；不要依赖 GDScript 异常式控制流。
- `complete_current_event()` 必须晚于 `BattleSkillOutcomeCommitter.commit_common_outcome()`、`_clear_defeated_unit()`、loot/report/mastery sidecar 和 changed unit/coord 登记。
- 不可逆的纯日志、纯展示、bookkeeping 事件不占用 reaction 窗口；可交互事件仍不得跨过另一个可交互事件回滚旧状态。

二期 snapshot 原则：

- 一期不引入完整反应事件快照。
- 二期 snapshot 必须是纯数据 Dictionary / DTO，不含 `RefCounted` 或 `Object` 引用，不依赖 Godot `duplicate(true)` 深拷贝对象图。
- `pre_snapshot` 只记录受影响单位/cell/sidecar delta；相关单位为 `affected_unit_ids` 加 `indirectly_affected_unit_ids` 的闭包。
- `post_snapshot` 只记录 checksum / 摘要。
- `max_completed_events` 限制完整 snapshot 数量；调试 UI 的最近事件列表只保留轻量摘要。
- 外部副作用延迟、achievement/mastery/battle rating/reward flush 只属于二期；MVP 不实现会回滚永久外部副作用的模式，因此不改永久 progression 提交流程。

核心结构：

```gdscript
class_name BattleWishReactionEvent
extends RefCounted

var event_id: int
var completed_event_serial: int
var source_unit_id: StringName
var source_skill_id: StringName
var wish_interaction_rank: int
var has_source_contest_bonus_snapshot: bool
var source_contest_bonus_snapshot: int
var event_type: StringName
var created_turn_serial: int
var pre_snapshot: Dictionary
var post_snapshot: Dictionary
var check_entries: Array[Dictionary]
var replay_inputs: Dictionary
var replay_supported_check_types: Array[StringName]
var affected_unit_ids: Array[StringName]
var indirectly_affected_unit_ids: Array[StringName]
var affected_coords: Array[Vector2i]
var reversible: bool
var consumed_by_wish: bool
```

“刚刚发生”的定义：

- 只允许使用全局最新一个已完成、未被消费的 `BattleWishReactionEvent`，且该事件本身必须可逆。
- `reaction_event_id` 必须等于 `history.get_latest_completed_event_id()`，并且该 event 的 `reversible == true`，否则 validator 拒绝。
- `fate_rewrite` 只接受带 `check_entries` 且 `reversible == true` 的事件。
- `hostile_denial` 只接受 `source_skill_id != &"wish"` 且 `reversible == true` 的事件。
- UI 只展示可操作的 latest reaction event；最近 5 个事件仅调试显示，避免玩家看到不可选择的旧事件。

“3 轮内死亡”的当前引擎定义：

- 仓库当前是时间线单位回合模型，不存在完整 round 对象。
- MVP 把“3轮内”落地为死亡目标所属阵营之后完成的 `friendly_turn_end_serial` 差值不超过 3，因为当前仓库没有全局 round 真相源，这是一条可测试、可展示的引擎定义。
- 死亡瞬间记录 `death_friendly_turn_end_serial = history.get_faction_turn_end_serial(target.faction_id)`；有效条件为 `current_serial - death_serial <= revive_window_friendly_turn_ends`，包含边界值 3。
- 该阈值由 `WishProfile.revive_window_friendly_turn_ends` 控制。
- UI/文本快照显示“剩余 X 次友方回合结束”。

### BattleWishResolver

新增文件：`scripts/systems/battle/runtime/battle_wish_resolver.gd`

公开接口：

```gdscript
func setup(runtime) -> void
func preview_wish(command: BattleCommand, caster: BattleUnitState, skill_def: SkillDef) -> BattlePreview
func build_wish_result(command: BattleCommand, caster: BattleUnitState, skill_def: SkillDef) -> BattleWishCommitResult
```

职责：

- 调用 `BattleSpecialProfileGate` 校验 manifest / owning skill / resolver id。
- 调用 `WishCommandPayload.from_command()` 校验命令并得到 typed payload。
- 按 9 种模式验证目标、历史事件、距离、LOS、阵营和环阶。
- 在验证阶段构建 typed delta/result，不直接改写 `BattleState`、grid、terrain 或单位资源。
- 返回 `BattleWishCommitResult`，由 commit adapter 在受控事务内统一应用。

Resolver 生命周期：

- `BattleRuntimeModule` 新增 `_wish_resolver`、`_wish_battle_window_service` 和 MVP 额外动作 sidecar 字段；二期再新增 `_wish_history_service`。
- `_setup_special_profile_runtime()` 创建并 `setup()` sidecar；`_ensure_sidecars_ready()`、test reconfigure、`start_battle()`、dispose 路径必须同时处理 wish sidecar。
- 新增最小 special profile router/dispatcher：`BattleSkillExecutionOrchestrator` 只判断 `special_resolution_profile_id != &""` 并路由到 profile handler；`meteor_swarm` 与 `wish` 共用该路由，但不在本次做更大的 framework 重构。
- `BattleSpecialProfileRouter` 维护一个小型 handler 表，key 为 `StringName profile_id`，初始化时注册 `meteor_swarm` 与 `wish`；未知 profile 返回明确错误。后续新增 special profile 只新增 handler 注册，不再改 orchestrator 分支。
- 依赖方向固定为 runtime -> profile handler/service。`BattleWishResolver` 不反向调用 orchestrator 私有 helper；`copy_spell` 使用独立 `BattleWishCopyExecutor` 或 profile handler 注入的普通技能验证/构建服务。

### BattleWishCommitResult

新增文件：`scripts/systems/battle/core/wish/wish_commit_result.gd`

建议字段：

```gdscript
class_name BattleWishCommitResult
extends RefCounted

var ok: bool = false
var reason: String = ""
var mode: StringName = &""
var precondition_snapshot: Dictionary = {}
var state_delta: BattleWishStateDelta
var changed_unit_ids: Array[StringName] = []
var changed_coords: Array[Vector2i] = []
var log_lines: Array[String] = []
var report_entries: Array[Dictionary] = []
var consumed_reaction_event_ids: Array[int] = []
```

提交路径：

- `BattleSpecialProfileCommitAdapter.commit_wish_result(result, batch)`。
- 新增 `BattleWishStateDelta`，只描述要应用的变更：单位 patch/restore、grid occupancy 操作、terrain 操作、状态增删、额外动作窗口、reaction event 消费、日志和 report entry。
- Commit adapter 创建 `BattleWishCommitTransaction`：
  1. 建立 MVP restore point，只覆盖 wish delta 会触达的单位、grid/cell、terrain/status、AP/MP/每战次数和 buffer。
  2. 应用祈愿术成本和 `per_battle_cast_counts` 递增。
  3. 应用 `BattleWishStateDelta`。
  4. 把 changed unit/coord、日志、report 等副作用先写入 transaction buffer，成功后 flush。
  5. 任一步失败则恢复快照，并且不消耗祈愿术成本/每战次数。
- 可以转换成 `BattleCommonSkillOutcome` 的简单结果仍走 `BattleSkillOutcomeCommitter.commit_common_outcome()`；不能表达为 common outcome 的回滚/传送/地形 restore 由 `BattleWishCommitTransaction` 应用。

MVP 事务只覆盖祈愿术自身状态变更、AP/MP/每战次数、grid/terrain/status/log/report buffer，不尝试撤销已提交普通技能事件。MVP transaction 使用 `begin_mvp_restore_point()`、`rollback_mvp_delta()`、`flush_buffers()` 一类只服务 wish delta 的接口；二期 reaction rollback 才引入 `pre_snapshot` / event rollback 契约，不在 MVP 暴露空的二期 API。

## 九种愿望模式的具体落地

本节的“执行”描述均指 `BattleWishResolver` 构建的 `BattleWishStateDelta` 内容；实际状态变更只发生在 `BattleWishCommitTransaction` 中。

部分成功防范原则：

- 所有多单位、多 cell、多步愿望必须先在验证阶段确认全部子操作可行。
- 提交阶段任一步失败都整体回滚，不允许留下半数单位移动、半片地形修改、复活但无合法坐标等中间状态。
- 不做提交时降级策略。需要备用坐标时，必须在验证阶段确定并写入 delta；找不到合法方案则整个愿望失败且不消耗祈愿术。
- MVP 的 `copy_spell` 只允许被复制技能能构建单个可提交 outcome；被复制技能本身若只支持部分提交，则不能被 `wish_copyable` 标记。

### 1. 复制法术：`copy_spell`

输入：

- `copy_skill_id`
- `copy_payload`

验证：

- `copy_skill_id != &"wish"`。
- 目标技能存在。
- `SkillDef.wish_copyable == true`。
- `1 <= SkillDef.wish_interaction_rank <= 8`。
- `wish_forbidden_tags` 不含 `legendary_ritual`、`permanent_creation`、`non_replicable_special_profile`。
- `copied_skill_def.combat_profile.special_resolution_profile_id == &""`。
- 被复制技能不能消耗材料、不能触发被复制技能自身冷却、不能学习要求。
- 被复制技能不能依赖 `consumed_mp` / `spent_mp` / cooldown state / self-charge state 等“成本已消耗后状态”；若存在这类公式，MVP 直接拒绝复制。
- `copy_payload` 能被被复制技能的目标选择 validator 完整接受。

执行：

- 构造一个内部 `BattleCommand`，使用 `copy_payload` 作为目标信息。
- 通过 `BattleWishCopyExecutor` 先验证、再构建 copied outcome：

```gdscript
validate_skill_as_wish_copy(caster, copied_skill_def, copied_command) -> String
build_skill_outcome_as_wish_copy(caster, copied_skill_def, copied_command) -> BattleCommonSkillOutcome
```

这些 helper：

- 跳过被复制技能的 learn/material/cost/cooldown 检查。
- 跳过被复制技能自身的 `per_battle_cast_limit/per_battle_cast_counts` 检查和递增；复制只消耗祈愿术自己的 AP/MP/每战次数。
- 保留目标合法性、LOS、阵营、地格和 effect resolver 检查。
- 不允许复制另一个 special profile。
- 在 `build_wish_result()` 阶段只构建 copied outcome/delta，不改写状态。
- 祈愿术的 AP/MP/每战次数只在 copied outcome 已成功构建、且 commit transaction 开始应用时消耗。
- 日志记录“祈愿术复制了 X”。

测试：

- 可复制 8 环普通技能，目标与效果正确。
- 不能复制 9 环、传奇仪式、永久创造、未标记 copyable、祈愿术自身。
- 被复制技能不扣自己的 MP/冷却，但祈愿术每战次数会消耗。

### 2. 命运改写：`fate_rewrite`（二期）

MVP 不实现该模式；validator 收到该 mode 时返回“命运改写属于二期，当前不可用”，不消耗祈愿术。

输入：

- `reaction_event_id`
- `grade_delta`，只允许 `-1` 或 `1`

验证：

- 事件存在、未消费、`reversible == true`。
- `reaction_event_id` 必须等于 `history.get_latest_completed_event_id()`，且该事件 `reversible == true`。
- 事件包含至少一个可改写检定。
- 只能把最近一次检定提高或降低一个等级。
- 新等级不能超过该检定定义的上下限。

执行：

- 将事件回滚到 `pre_snapshot`。
- 使用改写后的检定等级重新提交该事件的确定性 outcome。
- 标记该 reaction event 已被消费。

实现要求：

- 新增 `BattleCheckGrade` 常量：`critical_failure`、`failure`、`success`、`critical_success`。
- Attack/save/spell-control resolver 在事件中记录原始随机结果、等级、DC、修正和可重放输入。
- 如果某个检定类型尚未支持确定性重放，validator 必须让它不可选，而不是用随机再投一次。
- 二期开始前必须先做确定性重放审计表：
  - attack check：是否已记录 d20、modifier、DC/target defense、grade。
  - save check：是否已记录 DC、save attribute、modifier、grade。
  - spell-control：是否已记录 caster bonus、spent resource、threshold、grade。
  - status trigger / terrain trigger：未支持 replay 前不可选。
- 不采纳“部分改写可重放子检定”的降级策略；一个事件只有当目标检定可完整重放时才可被选择。

测试：

- 命中失败提升为成功后产生伤害。
- 成功降低为失败后撤销已造成结果。
- 同一事件不能被两次愿望改写。
- 不支持确定性重放的事件不会出现在可选列表。

### 3. 逆转伤亡：`reverse_casualty`

输入：

- `target_unit_id`

验证：

- 目标是友军。
- 目标当前死亡或 `is_alive == false`。
- 有死亡记录。
- 死亡记录在 `revive_window_friendly_turn_ends` 内。

执行：

- 目标 `is_alive = true`。
- `current_hp = ceil(max_hp * revive_hp_ratio)`。
- 清除主要负面状态。
- 复位到验证阶段确定的合法坐标；默认使用死亡坐标。
- 如果死亡坐标被占用或不合法，validator 可以在提交前确定一个备用合法相邻格；找不到合法格则整个愿望失败，不复活。
- 恢复 grid occupancy。

主要负面状态策略：

- 新增 `BattleStatusSemanticTable.is_wish_major_negative_status(status_id)`。
- 包含普通 harmful、强控、濒死类、即死前置类状态。
- 不移除正面状态和中立标记。

测试：

- 3 个窗口内死亡友军复活到 50% 最大生命。
- 死亡格被占用时，验证阶段有备用合法格才复活；没有合法格则失败且不消耗祈愿术。
- 过期死亡记录不可选。
- 主要负面状态清除，正面状态保留。

### 4. 战局重置：`battle_reset`（二期）

MVP 不实现该模式；validator 收到该 mode 时返回“战局重置依赖二期快照系统，当前不可用”，不消耗祈愿术。

输入：

- `target_unit_id`

验证：

- 目标是友军。
- 存在目标阵营最近一次友方回合结束快照。
- 目标仍存在于 `BattleState.units`。

执行：

- 还原目标在“目标阵营最近一次己方单位回合结束快照”中的生命、位置、状态、护盾、资源和主要战斗标记。
- 先清理目标当前占位，再尝试还原快照坐标。
- 快照坐标不可用时，必须在验证阶段确定备用合法格；找不到合法格则整个愿望失败。
- 不还原装备、已学技能、永久成长、掉落、队伍背包。

测试：

- 目标 HP/位置/状态恢复到上一个友方回合结束。
- 当前坐标和快照坐标 occupancy 都保持一致。
- 无快照时不可施放。

### 5. 全队转移：`team_transfer`

输入：

- `target_unit_ids`
- `target_coords`

验证：

- 1 到 `max_team_transfer_targets` 个友军。
- 数组长度一致。
- 每个目标坐标在视野内、合法、可站立。
- 每个目标移动距离不超过 `team_transfer_max_distance`。
- 多目标目标格不能重复。

执行：

- 按目标数组顺序清除旧 occupancy。
- 全部合法后统一放置新坐标；避免前一个目标占住后一个目标的旧位置。
- 记录 changed unit/coord。

测试：

- 6 名以内友军传送成功。
- 第 7 名、超距离、视野外、非法格、重复目标格都会失败且不产生部分位移。

### 6. 绝对庇护：`absolute_sanctuary`

输入：

- `target_coord` 作为 5x5 区域中心。

验证：

- 区域内至少 1 名友军。

执行：

- 给区域内所有友军添加 `wish_sanctuary`，持续 1 个所属阵营友方回合结束窗口。
- 状态写入 `expires_at_friendly_turn_end_serial = current_faction_serial + 1`，当所属阵营 serial 达到 `>= expires_at_friendly_turn_end_serial` 时移除；如果先触发抵消，则立即移除。
- `wish_sanctuary` 抵消下一次重大伤害、即死或强控，触发后移除。

规则接入：

- `BattleDamageResolver` 在最终扣血前检查：
  - 伤害会致死，或
  - 伤害大于 `max_hp * major_damage_ratio_threshold`
  - 则伤害归零并消费状态。
- `BattleDamageResolver` 只调用通用状态语义查询/消费接口，例如 `BattleStatusSemanticTable.find_pre_damage_prevention(unit, damage_context)`；该接口按 tag / semantic 返回要抵消的 status 和原因，不能在 damage resolver 中直接判断 `wish_sanctuary`。
- 致死判定发生在扣血前；多段伤害逐段独立判定，第一段满足重大伤害/致死即触发并消费庇护。
- 即死和强控使用 tag / semantic table 驱动：`strong_control`、`instant_death`、`execute`。新增状态只要带对应 tag 即被庇护识别。

测试：

- 区域内友军获得庇护，区域外没有。
- 下一次重大伤害被抵消并移除状态。
- 小伤害不会消耗状态。
- 即死和强控被抵消。
- 边界值覆盖：刚好等于重大伤害阈值、低于阈值 1 点、高于阈值 1 点、多段伤害第一段/第二段触发位置。
- 未触发庇护时，在 `current_serial >= expires_at_friendly_turn_end_serial` 的回合结束处理中到期移除。

### 7. 现实修补：`reality_repair`

输入二选一：

- 清除异常：`terrain_effect_id` + `target_coord`
- 生成有利地形：`terrain_id` + `target_coord`

验证：

- 区域为 7x7。
- 清除模式要求目标区域内存在该异常地形或 timed terrain effect。
- 生成模式要求 `terrain_id` 在 `WishProfile.beneficial_terrain_ids` 内。
- 不允许生成不可通行、永久创造或剧情地形。

执行：

- 清除模式：
  - 从 `BattleGridCell.terrain_effect_ids` 移除指定效果。
  - 从 `timed_terrain_effects` 移除指定 effect。
  - 调用地形拓扑修正，例如水体重分类。
- 生成模式：
  - 使用 `BattleGridService.set_base_terrain()` 设置普通有利地形。
  - 写入 changed coords。

测试：

- 清除 7x7 内指定 hazard。
- 不清除区域外 hazard。
- 生成允许列表内有利地形。
- 不允许生成永久/剧情地形。

### 8. 破除绝境：`break_desperation`

MVP 只允许目标为当前 active unit；任意单位插队版属于二期。

输入：

- `target_unit_id`

验证：

- 目标是友军。
- MVP 要求 `target_unit_id == state.active_unit_id`。

执行：

- 移除目标所有负面状态。
- 给予一次祈愿术额外主要动作窗口。
- 该窗口内不能施放 `wish_interaction_rank >= 9` 的技能；窗口外不限制目标使用自己的正常 AP 施放 9 环。

额外动作实现：

- MVP 新增 `BattleWishExtraActionService`，只管理当前 active unit 的 AP 豁免窗口：

```gdscript
var active_extra_action_context: Dictionary = {}
```

- 如果目标就是当前 active unit，窗口立即覆盖其下一次主要动作命令。
- 非当前 active unit 的临时插队、暂停/恢复 timeline、嵌套 interrupt 互操作全部后置到二期。
- 该窗口不推进 TU，不触发普通回合开始/结束 tick，不重置 AP/move/cooldown。
- 窗口中的主要动作不消耗目标原有 AP；窗口消费后关闭。
- AP 豁免只通过 `BattleWishExtraActionService.is_ap_free_window(unit, skill_def)` 判断，避免多个 resolver 各自绕过成本。
- `BattleRuntimeSkillTurnResolver` 在 `active_extra_action_context.source_skill_id == &"wish"` 时检查 `wish_interaction_rank >= 9` 并给出 block reason。

测试：

- 所有负面状态移除。
- 当前行动单位获得额外动作时，下一次主要动作不扣普通 AP。
- 非当前行动单位在 MVP 会被拒绝且不消耗祈愿术。
- 额外动作窗口不能施放 9 环，但窗口外正常 AP 不受该禁令影响。
- 窗口只消费一次，跳过也会关闭。

### 9. 敌意否决：`hostile_denial`（二期）

MVP 不实现该模式；validator 收到该 mode 时返回“敌意否决依赖二期 reaction rollback，当前不可用”，不消耗祈愿术。

输入：

- `reaction_event_id`

验证：

- 事件存在、未消费、`reversible == true`。
- `reaction_event_id` 必须等于 `history.get_latest_completed_event_id()`，且该事件 `reversible == true`。
- 来源事件不是祈愿术。
- 来源技能 `wish_deniable == true`。
- 来源技能 `wish_interaction_rank <= 9`。

执行：

- `wish_interaction_rank <= 8`：直接回滚到事件 `pre_snapshot`，标记事件被消费。
- `wish_interaction_rank == 9`：进行愿望对抗检定。
  - 双方各投一次 `d20 + wish_contest_bonus`。
  - `wish_contest_bonus` 优先使用 spell-control bonus；没有 spell-control 时使用主施法属性 bonus；原施法者的 bonus 从 reaction event 的 `source_contest_bonus_snapshot` 读取。
  - 原施法者死亡或不可行动时仍使用事件快照中的 bonus；如果 `has_source_contest_bonus_snapshot == false`，则该 9 环事件不可被否决。
  - 祈愿术总值大于或等于原施法者即胜出；平局归祈愿术方，因为这是 9 环、每战一次且消耗主要动作的防守性反制。
  - 胜出则回滚；失败则祈愿术消耗但不回滚。

对抗检定必须记录为新的 reaction event，但不可再被同一次祈愿术否决，避免反应递归。

测试：

- 8 环及以下技能/法术被完整取消。
- 9 环事件胜出或平局时取消，失败时保留。
- 不可否决祈愿术自身或不可逆事件。

## 数据文件

新增技能资源：

- `data/configs/skills/mage_wish.tres`

建议配置：

- `skill_id = &"wish"`
- `display_name = "祈愿术"`
- `skill_type = &"combat"`
- `tags = [&"mage", &"magic", &"wish", &"rank_9"]`
- `wish_interaction_rank = 9`
- `wish_copyable = false`
- `combat_profile.special_resolution_profile_id = &"wish"`
- `combat_profile.target_selection_mode = &"special_payload"`
- `combat_profile.per_battle_cast_limit = 1`
- `combat_profile.ap_cost = 1`

需要同步标注现有可交互技能：

- 给可被复制/否决的法术和技能设置 `wish_interaction_rank`。
- 只有经过内容审核的 1 到 8 环法术设置 `wish_copyable = true`。
- 传奇仪式、永久创造类效果写入 `wish_forbidden_tags`。

## 代码改动清单

核心资源和 schema：

- `scripts/player/progression/skill_def.gd`
- `scripts/player/progression/combat_skill_def.gd`
- `scripts/player/progression/skill_content_registry.gd`
- `scripts/systems/battle/core/special_profiles/battle_special_profile_manifest_validator.gd`
- `scripts/systems/battle/core/wish/wish_profile.gd`
- `scripts/systems/battle/core/wish/wish_command_payload.gd`
- `scripts/systems/battle/core/wish/wish_commit_result.gd`
- `scripts/systems/battle/core/wish/wish_state_delta.gd`

运行时：

- `scripts/systems/battle/core/battle_command.gd`
- `scripts/systems/battle/core/battle_unit_state.gd`
- `scripts/systems/battle/runtime/battle_runtime_module.gd`
- `scripts/systems/battle/runtime/battle_skill_execution_orchestrator.gd`
- `scripts/systems/battle/runtime/battle_skill_turn_resolver.gd`
- `scripts/systems/battle/runtime/battle_special_profile_router.gd`
- `scripts/systems/battle/runtime/battle_special_profile_commit_adapter.gd`
- `scripts/systems/battle/runtime/battle_wish_commit_transaction.gd`（MVP 只做 wish delta 原子提交；二期扩展 reaction rollback）
- `scripts/systems/battle/runtime/battle_wish_copy_executor.gd`
- `scripts/systems/battle/runtime/battle_wish_extra_action_service.gd`
- `scripts/systems/battle/runtime/battle_wish_battle_window_service.gd`
- `scripts/systems/battle/runtime/battle_wish_history_service.gd`（二期）
- `scripts/systems/battle/runtime/battle_wish_resolver.gd`
- `scripts/systems/battle/runtime/battle_timeline_driver.gd`

规则和地形：

- `scripts/systems/battle/rules/battle_damage_resolver.gd`
- `scripts/systems/battle/rules/battle_status_semantic_table.gd`
- `scripts/systems/battle/terrain/battle_terrain_effect_system.gd`
- `scripts/systems/battle/terrain/battle_grid_service.gd`

展示和输入：

- `scripts/systems/battle/presentation/battle_hud_adapter.gd`
- `scripts/systems/battle/ai/battle_ai_action_assembler.gd`
- `scripts/systems/game_runtime/game_runtime_battle_selection.gd`
- `scripts/systems/game_runtime/battle_session_facade.gd`
- `scripts/systems/game_runtime/headless/game_text_command_runner.gd`
- `scripts/ui/battle_board_controller.gd`
- 文本命令回归覆盖 `tests/text_runtime/commands/` 下的战斗命令 runner。

数据：

- `data/configs/skills/mage_wish.tres`
- `data/configs/skill_special_profiles/profiles/wish_profile.tres`
- `data/configs/skill_special_profiles/manifests/wish_special_profile_manifest.tres`

文档：

- `docs/design/project_context_units.md`

## 回归测试计划

MVP 新增测试：

- `tests/battle_runtime/runtime/run_wish_per_battle_limit_regression.gd`
- `tests/battle_runtime/runtime/run_wish_special_payload_protocol_regression.gd`
- `tests/battle_runtime/runtime/run_wish_hud_preview_protocol_regression.gd`
- `tests/battle_runtime/skills/run_wish_special_profile_regression.gd`
- `tests/battle_runtime/skills/run_wish_copy_spell_regression.gd`
- `tests/battle_runtime/skills/run_wish_reverse_casualty_regression.gd`
- `tests/battle_runtime/skills/run_wish_team_transfer_regression.gd`
- `tests/battle_runtime/skills/run_wish_break_desperation_active_unit_regression.gd`
- `tests/battle_runtime/terrain/run_wish_reality_repair_regression.gd`
- `tests/battle_runtime/rules/run_wish_sanctuary_regression.gd`
- `tests/battle_runtime/ai/run_wish_ai_skip_special_payload_regression.gd`
- `tests/progression/schema/run_wish_schema_regression.gd`
- `tests/text_runtime/commands/run_wish_text_command_regression.gd`

二期新增测试：

- `tests/battle_runtime/runtime/run_wish_history_service_regression.gd`
- `tests/battle_runtime/runtime/run_wish_reaction_event_regression.gd`
- `tests/battle_runtime/runtime/run_wish_commit_transaction_rollback_regression.gd`
- `tests/battle_runtime/runtime/run_wish_external_side_effect_delay_regression.gd`
- `tests/battle_runtime/skills/run_wish_fate_rewrite_regression.gd`
- `tests/battle_runtime/skills/run_wish_hostile_denial_regression.gd`
- `tests/battle_runtime/skills/run_wish_battle_reset_regression.gd`

需要覆盖的断言：

- manifest gate 接受 `wish`，拒绝非 owning skill。
- `WishProfile` 资源验证覆盖 area size 为正奇数、ratio 范围、status id 非空、terrain id 白名单、required regression tests 存在。
- `per_battle_cast_limit = 1` 后同角色第二次施放被拦截，且缺少 count key 时按已使用 0 次处理。
- `per_battle_cast_limit > 0` 只允许 `wish` 使用，普通技能资源设置该字段会被资源验证拒绝。
- 非法 payload、非法目标、MVP 禁用的二期模式都不消耗 AP/MP/冷却/`per_battle_cast_counts`。
- 九种 mode 的 payload validator 都拒绝缺字段、未知字段和非法目标。
- `special_payload` 在 HUD snapshot、`GameRuntimeBattleSelection`、facade、headless/text 命令、preview、execute、失败反馈和选择清空路径中都会保留或清理正确。
- 复制法术不扣被复制技能成本，不允许复制 9 环或禁用标签。
- 复制法术不允许复制任何 `special_resolution_profile_id != &""` 的技能。
- 复制法术不检查也不递增被复制技能自己的 `per_battle_cast_counts`。
- MVP 禁用 `fate_rewrite`、`battle_reset`、`hostile_denial` 时必须返回明确原因且不消耗祈愿术。
- 二期命运改写只改写最新可重放检定，并消费事件。
- 二期如果全局最新 completed event 不可逆，即使前一个事件可逆，命运改写/敌意否决也必须失败且不消耗祈愿术。
- 二期敌意否决/命运改写的完整回滚断言覆盖 HP/MP/AP、status、shield、terrain/timed effect、cooldown、死亡清格、changed units/coords、log/report 长度、loot/report sidecar 不残留半提交。
- 二期外部副作用在 reaction window 未关闭前不会永久落地，会进入 `pending_external_side_effects`；window 关闭或结算前只 flush 一次；被愿望回滚/否决时 pending 被清掉且永久侧无残留。
- 逆转伤亡保持 grid occupancy 一致；二期战局重置也必须保持 grid occupancy 一致。
- 全队转移不会产生部分成功。
- 绝对庇护抵消重大伤害/即死/强控，并只触发一次。
- 绝对庇护未触发时按 `current_serial >= expires_at_friendly_turn_end_serial` 到期移除。
- 现实修补只影响 7x7 内目标地形/异常。
- MVP 破除绝境只允许当前 active unit；额外动作只消费一次，且不能施放 9 环。
- 二期敌意否决能回滚 8 环及以下事件；9 环走对抗检定，平局祈愿术胜出。
- 二期 9 环敌意否决在 `has_source_contest_bonus_snapshot == false` 时被拒绝；原施法者死亡/不可行动时仍使用 `source_contest_bonus_snapshot`；对抗使用事件快照 bonus，而不是当前动态 bonus。

现有基线命令：

这些 runner 当前已经存在；开始实现前和实现后都要运行，用来确认基础资源和技能协议没有被破坏。

```bash
godot --headless --script tests/runtime/validation/run_resource_validation_regression.gd
godot --headless --script tests/progression/schema/run_skill_schema_regression.gd
godot --headless --script tests/battle_runtime/runtime/run_battle_skill_protocol_regression.gd
```

实现祈愿术时必须新增并运行的命令：

这些 runner 在方案阶段可以不存在；它们是 MVP 实现阶段新增的验收面。

```bash
godot --headless --script tests/progression/schema/run_wish_schema_regression.gd
godot --headless --script tests/battle_runtime/runtime/run_wish_per_battle_limit_regression.gd
godot --headless --script tests/battle_runtime/runtime/run_wish_special_payload_protocol_regression.gd
godot --headless --script tests/battle_runtime/runtime/run_wish_hud_preview_protocol_regression.gd
godot --headless --script tests/battle_runtime/skills/run_wish_special_profile_regression.gd
godot --headless --script tests/battle_runtime/skills/run_wish_copy_spell_regression.gd
godot --headless --script tests/battle_runtime/skills/run_wish_reverse_casualty_regression.gd
godot --headless --script tests/battle_runtime/skills/run_wish_team_transfer_regression.gd
godot --headless --script tests/battle_runtime/skills/run_wish_break_desperation_active_unit_regression.gd
godot --headless --script tests/battle_runtime/terrain/run_wish_reality_repair_regression.gd
godot --headless --script tests/battle_runtime/rules/run_wish_sanctuary_regression.gd
godot --headless --script tests/battle_runtime/ai/run_wish_ai_skip_special_payload_regression.gd
godot --headless --script tests/text_runtime/commands/run_wish_text_command_regression.gd
```

按 touched surface 追加现有相关 runner：

- 修改 runtime/orchestrator：运行 `tests/battle_runtime/runtime/run_battle_runtime_smoke.gd`、`tests/battle_runtime/runtime/run_battle_skill_protocol_regression.gd` 和相关 `tests/battle_runtime/skills/run_*_regression.gd`。
- 修改 damage/status：运行 `tests/battle_runtime/rules/run_battle_damage_resolver_preview_contract_regression.gd`、`tests/battle_runtime/rules/run_status_effect_semantics_regression.gd`。
- 修改 HUD/board：运行 `tests/battle_runtime/rendering/run_battle_ui_regression.gd`；涉及 `BattleBoardController` 的 PR 需要手动截图或短视频。
- 修改 state schema：运行现有 `tests/battle_runtime/state_schema/` 相关 runner；不要把瞬态 `per_battle_cast_counts` 加入长期 `TO_DICT_FIELDS`，除非先确认破坏性 schema 变更。

不把 battle simulation / balance runner 纳入常规验证，除非后续明确要求数值模拟。

## 实施顺序

MVP：

1. 加 schema 字段和资源验证：`SkillDef`、`CombatSkillDef`、`SkillContentRegistry`、manifest validator。
2. 新增 `WishProfile`、manifest、`mage_wish.tres`，先让资源验证通过。
3. 新增 `BattleCommand.special_payload`、typed `WishCommandPayload` 和 `special_payload` 目标选择协议。
4. 加 `per_battle_cast_limit` / 瞬态 `per_battle_cast_counts`，并补 HUD 可用性；资源验证拒绝非 wish 技能设置该字段。
5. 接入最小 special profile router、`_wish_resolver` 生命周期和 `_wish_battle_window_service`，确保 `start_battle()` 清空战斗内状态。
6. 新增 `BattleWishCommitTransaction` 的 MVP 原子提交：只覆盖 wish delta、AP/MP/每战次数、grid/terrain/status/log/report buffer。
7. 补 HUD/facade/headless/text 命令，让 `special_payload` 能走完整自动化协议。
8. 实现 MVP modes，顺序建议：
   - `team_transfer`
   - `reality_repair`
   - `reverse_casualty`
   - `absolute_sanctuary`
   - `break_desperation`（仅当前 active unit）
   - `copy_spell`
9. 补齐 MVP 回归脚本。
10. 更新 `docs/design/project_context_units.md`。

二期进入条件：

1. 完成确定性重放审计表，确认 attack/save/spell-control 等检定的 replay 输入记录。
2. 设计并验证纯数据 snapshot / delta，不依赖 `duplicate(true)`。
3. 设计 reaction event 上限、性能基准和外部副作用延迟策略。
4. 先通过 `run_wish_history_service_regression.gd` 与 `run_wish_commit_transaction_rollback_regression.gd` 的最小回滚场景。
5. 再实现 `fate_rewrite`、`hostile_denial`、`battle_reset` 和任意单位 `break_desperation`。

工作量估算：

- MVP：约 1 到 1.5 周，主要是 special profile、payload、6 个非时间旅行模式和回归。
- 二期完整版：约 3 到 4 周，主要是 deterministic replay、reaction rollback、外部副作用延迟和复杂交互测试。

## 审查关注点

架构审查：

- special profile 是否被局限在 `wish`，有没有污染普通 skill pipeline。
- `BattleCommand.special_payload` 是否有足够强的 validator，避免任意字典穿透运行时。
- history service 的快照范围是否足以回滚，又不会保存长期存档。

规则审查：

- “刚刚发生”“3轮内”“上一个己方回合结束”在当前时间线模型里的定义是否一致。
- 九种模式是否都有明确失败条件，失败时不产生部分状态变更。
- 9 环对抗检定、额外动作禁止 9 环、庇护强控列表是否有可测试定义。

测试审查：

- 是否覆盖回滚、occupancy、一战一次、payload 拒绝、资源 schema。
- 是否避免把 battle simulation 加进 routine suite。
- 是否有足够的 headless 用例保护 UI 之外的核心运行时。


---

## 多层面检视意见汇总

> 以下意见由 4 个子代理分别从架构耦合、状态事务、规则边界、测试风险四个层面独立审阅后汇总，供实现前参考与决策。
>
> 处理结果见本节末尾“ Kimi 审查意见处理裁决”表；不可采纳或部分采纳的原因已写在对应条目中，正文方案已按采纳项更新。

---

### 一、架构与耦合层面

#### 1. special profile 对核心 orchestrator 的侵入性过重
`BattleSkillExecutionOrchestrator` 当前对 special profile 采用硬编码分支。wish.md 选择"只新增 `wish` 分支，不重构全部 special profile dispatcher"，这意味着 orchestrator 的两个核心入口方法将被继续硬编码侵入。每新增一个 special profile，orchestrator 的核心流程就必须被修改一次；wish 的 `copy_spell` 模式还要求在 orchestrator 上新增两个 helper，进一步加重 orchestrator 的职责负担。

**建议**：在同一次改动中实现一个小型 special profile dispatcher（或 registry），让 orchestrator 只负责检测 `special_resolution_profile_id != &""`，将 command 路由给对应 resolver 的 `preview()` / `execute()`，并统一处理通用收尾。

#### 2. wish sidecar 生命周期管理存在 dispose/reset 遗漏风险
`BattleRuntimeModule` 目前没有显式 `reset()` 方法，战斗间重置依赖 `start_battle()` 和 `dispose()`。`_setup_special_profile_runtime()` 仅在 `setup()` 中调用一次。若连续运行多场战斗（如 headless 回归测试连续调用 `start_battle()`），`BattleWishHistoryService` 可能残留上一场战斗的事件，导致 `fate_rewrite` / `hostile_denial` 错误地消费旧事件。

**建议**：在 `BattleRuntimeModule.start_battle()` 开头显式重置 `_wish_history_service`；将 `_setup_special_profile_runtime()` 纳入 `_ensure_sidecars_ready()` 的调用链；若需在测试中 mock，应通过 `configure_xxx_for_tests()` 方法暴露，保持与现有测试配置 pattern 一致。

#### 3. `BattleCommand.special_payload` 作为通用 Dictionary 穿透运行时，类型安全与验证负担显著
`BattleCommand` 是贯穿 AI、HUD、headless、text 命令、存档回放全链路的核心传输对象。方案要求"对外保留 `BattleCommand.special_payload` 作为传输字典，对内必须先调用 `WishCommandPayload.from_command()`"，这意味着每个 wish 入口点都必须手动调用转换，任何遗漏都会导致裸 Dictionary 穿透到 resolver/committer。此外，`BattleCommand` 若被用于战斗日志或回放序列化，`special_payload` 中的任意键值对会带来版本兼容隐患；GDScript Dictionary 的键是 Variant，`wish_mode` 等字段在传输过程中可能出现 String/StringName 混用。

**建议**：优先考虑将 wish 的特定载荷从 `BattleCommand` 中剥离（如使用 `WishContext` 对象）；若必须保留，则应在 `BattleCommand` 上提供强类型只读 accessor，在 accessor 内部强制完成校验与转换；为 `BattleCommand` 补充与 `BattleUnitState` 同等严格的序列化白名单。

#### 4. 与 meteor_swarm 的共存关系：若干修改迫使通用模块承担 wish-specific 逻辑
wish 的落地方式迫使多个原本通用的核心模块引入 wish-specific 分支或钩子，例如：`BattleDamageResolver` 扣血前检查 `wish_sanctuary`；`BattleTimelineDriver._end_active_turn` 调用 `capture_friendly_turn_end_snapshot`；`BattleSkillExecutionOrchestrator` 新增 copy_spell 的 bypass 专用通道。

**建议**：将 `wish_sanctuary` 的抵消逻辑抽象为通用的 `PreDamageInterceptor` 钩子链；将 turn-end snapshot 需求抽象为 `BattleEventObserver` 或 `TurnEndHook` 接口；将 `break_desperation` 的额外动作抽象为通用的 `InterruptActionContext` 机制；copy_spell 的验证与构建应在 `BattleWishResolver` 内部完成，避免在 orchestrator 上开 bypass 专用通道。

#### 5. 新增模块间依赖方向存在循环依赖风险
- `BattleWishResolver` ↔ `BattleSkillExecutionOrchestrator`：copy_spell 模式要求 resolver "通过 orchestrator 的两个新 helper 先验证、再提交"，可能形成 `resolver → orchestrator → runtime → resolver` 的循环。
- `BattleWishCommitTransaction` ↔ `BattleRuntimeModule`：Commit transaction 需要应用变更并调用 death/loot/log/report 收尾，形成 `runtime → adapter → transaction → runtime` 的运行时循环。
- `BattleWishExtraActionService` ↔ `BattleTimelineDriver`：Extra action service 需要暂停/恢复 timeline driver 的状态，若两者互相持有引用，容易形成循环。

**建议**：`BattleWishResolver` 只应依赖 `BattleRuntimeModule` 作为服务提供者，不应反向调用 orchestrator；`BattleWishCommitTransaction` 应只操作 `BattleState`、grid service、terrain service 等纯数据/基础设施层对象，业务收尾应由 adapter 在 transaction 成功后再统一调用；`BattleWishHistoryService` 应设计为纯粹的事件记录者，通过 `BattleRuntimeModule` 提供的通用事件钩子收集信息。

---

### 二、状态一致性与事务层面

#### 1. BattleWishHistoryService 快照范围：完整性与内存/性能权衡
快照面覆盖了 `BattleState.units` 完整 clone、grid occupancy、terrain、log/report 长度、changed lists、loot resolver 待提交状态、battle metrics、active unit、timeline TU、interrupt context 等，范围极广。派生状态（如 log/report 长度、loot resolver 待提交状态）如果被完整 snapshot 但实际回滚时只恢复到长度/计数，可能导致底层数据与长度不一致。此外，每场战斗的每个技能/法术都产生一个 `BattleWishReactionEvent`，在 Godot GDScript 环境下可能导致显著的 GC 压力和内存占用。文档说"相关单位的完整 clone"，但未定义何为"相关"。

**建议**：明确"相关"为 `affected_unit_ids` 的闭包，并记录 `indirectly_affected_unit_ids`；对 `pre_snapshot` 做深拷贝（回滚用），`post_snapshot` 做轻量校验和或引用比较；增加历史事件数量上限（如保留最近 10 个 completed reversible events），超出时清理旧 snapshot。

#### 2. pre_snapshot / post_snapshot 克隆深度与深拷贝可行性
文档要求保存 `BattleUnitState` 的"完整 clone"，但未明确 clone 策略。Godot/GDScript 的 `Dictionary.duplicate(true)` 和 `Array.duplicate(true)` 对 `RefCounted`/`Object` 实例只复制引用，不会递归深拷贝对象图。若 `BattleUnitState` 内部包含 `RefCounted` 子对象（如 `BattleStatusInstance`、shield 对象），`duplicate(true)` 只会复制引用，回滚时修改这些子对象会导致 snapshot 也被污染，回滚失效。

**建议**：在文档中明确要求 `BattleWishHistoryService` 实现自定义深拷贝方法，对 `BattleUnitState` 的每个标量/容器/子对象做显式序列化（如转为扁平 Dictionary），而非依赖 Godot 原生 `duplicate(true)`；规定 snapshot 格式为"纯数据 Dictionary"（不含 `RefCounted` 引用）；删除或弱化 `post_snapshot` 的深拷贝要求，改为记录 `post_event_checksum`。

#### 3. BattleWishCommitTransaction 的原子性保证与 per_battle_cast_counts 回滚
文档描述 commit 步骤为：①捕获快照 → ②应用成本 + per_battle_cast_counts 递增 → ③应用 delta → ④统一收尾。任一步失败则恢复快照且不消耗成本/次数。但未定义"失败"的检测边界和回滚的具体实现。步骤 ④ 的"death/loot/report 收尾"可能涉及向外部系统追加数据，若这些系统没有参与 snapshot，回滚时只会恢复 `BattleState`，但 report formatter 内部状态已经膨胀。此外，`BattleWishStateDelta` 若包含多个子操作，中途抛异常则已写入的部分无法自动撤销。

**建议**：明确 `BattleWishCommitTransaction` 采用"快照替换"而非"patch 撤销"策略；对 `per_battle_cast_counts` 的递增操作要求必须在同一个 `Dictionary` 对象上进行，且该对象必须被包含在 snapshot 范围内；定义所有 sidecar（log、report、death、loot）必须实现 `transactional_append()` 接口，在 transaction 成功后再真正写入，或这些 sidecar 必须被纳入 snapshot 范围。

#### 4. reaction event "begin → complete" 窗口与现有 sidecar 时序严谨性
文档规定了 begin 早于 consume_skill_costs，complete 晚于 commit_common_outcome、_clear_defeated_unit、loot/report/mastery sidecar。但存在隐含的时序漏洞：若 `begin_reaction_event()` 与 `consume_skill_costs()` 之间出现异常，history service 会记录一个未消耗成本的 skeleton event，而该事件不会被 complete，也不会被清理。若 loot/mastery sidecar 是异步或回调驱动的，`complete_reaction_event()` 可能在 sidecar 尚未完全执行时就已被调用。此外，`_clear_defeated_unit()` 在清除格子前调用 `record_unit_death()`，若死亡记录保存了死亡时的状态 snapshot，而后续的 `_clear_defeated_unit` 修改了 grid occupancy，则 death record 中的坐标与 grid 状态可能不一致。

**建议**：在 `BattleSkillExecutionOrchestrator` 中明确 `begin_reaction_event()` 和 `consume_skill_costs()` 必须在同一个 try/except 块内，任何异常都触发 `history_service.cancel_current_event()`；若 loot/mastery 存在异步路径，要求它们在 `complete_reaction_event()` 之前同步返回或提供 `await` 点；修正死亡记录时序：`_clear_defeated_unit()` 应先完成所有状态变更，再调用 `record_unit_death()`，或在 `record_unit_death` 中显式保存清格前后的两个视角。

#### 5. pending_external_side_effects 延迟策略的实现复杂度与 flush 可靠性
文档要求外部副作用必须延迟到 reaction window 关闭后 flush。但"下一个 completed event 覆盖反应窗口"不意味着祈愿术不能再改写前一个事件——根据文档，祈愿术只能改写"最新一个 completed event"，所以只要新事件完成，旧事件自然关闭。但如果新事件就是祈愿术本身呢？祈愿术作为 reaction event 被记录后，它自己是否又开启一个新的 reaction window？这会导致 flush 无限推迟。此外，若战斗结算（如一方全灭）发生在某个 reaction event 之后，且结算逻辑立即计算 battle end result 和 post-battle rewards，此时 pending 中的副作用可能尚未 flush 就被结算逻辑覆盖或重复计算。

**建议**：明确定义 `pending_external_side_effects` 的数据结构，并为每个副作用记录 `trigger_event_id` 和 `flush_condition`；在 `BattleWishHistoryService.complete_reaction_event()` 中，当新事件完成时，自动将"倒数第二个事件"的 pending side effects 标记为可 flush；在 `BattleRuntimeModule` 的战斗结算入口中，强制调用 `flush_all_pending_external_side_effects()`，并断言队列为空后才允许计算 battle end result。

#### 6. 九种愿望模式的"部分成功"风险与防范充分性
逐模式分析显示，部分模式的防范存在不足：
- **copy_spell**：若被复制技能自身允许部分成功，则 copy_spell 也会继承该风险，文档未明确要求被复制技能的 outcome 必须整体提交。
- **fate_rewrite**：文档未讨论重新提交失败的情况，应要求重新提交走同一个 `BattleWishCommitTransaction`，确保"回滚 + 重写"整体原子。
- **reverse_casualty**：若复活和清状态成功但 grid occupancy 失败（如所有相邻格都被占且不可通行），则单位存活但无合法位置，状态矛盾。文档应规定：若找不到合法格，则整个 reverse_casualty 失败，不复活。
- **battle_reset**：若还原 HP/状态成功但 grid placement 失败（且找不到合法格），状态会矛盾。文档应规定：若无法为复活单位找到任何合法格，整个 reset 失败。
- **team_transfer**：验证通过后在实际放置前发生异常，已清除的旧格无法自动恢复。应在 transaction 内先保存所有旧 occupancy 状态，再统一应用变更。
- **reality_repair**：多 cell 修改中部分成功会导致地形不一致。应要求整个 7x7 区域的修改在一个 transaction 内完成。

**建议**：在文档中新增一节"部分成功防范原则"，统一规定：所有涉及多目标/多 cell/多步操作的愿望模式，要么在验证阶段确认全部子操作可行，要么在 transaction 内支持全量回滚，禁止验证后、提交中的降级策略（如"找不到格就放最近合法格"应改为"找不到格就整体失败"）。

---

### 三、规则定义与边界条件层面

#### 1. "刚刚发生"事件只能选最新一个可逆事件
文档规定 `fate_rewrite` 和 `hostile_denial` 只能选择全局最新一个已完成且 `reversible == true` 的事件。若敌方连续行动，先释放了一个不可逆的被动/环境效果，紧接着释放了一个可逆的 8 环大招，按当前规则祈愿术无法否决那个 8 环大招。这会导致玩家在面对"夹杂被动触发"的复杂回合时，祈愿术形同虚设。UI 可以展示最近 5 个事件，但玩家只能操作最新一个，当最新事件不可用时，玩家看到下方有可操作事件却无法选择，会产生强烈困惑。

**建议**：明确列出哪些事件类型属于"不可逆"，并给出判断标准；考虑允许"跳过"某些类型的非技能事件，直接锁定前一个技能事件；或放宽为"最近 5 个 completed 事件中的任意一个可逆事件"。

#### 2. "3轮内"死亡复活的 `friendly_turn_end_serial` 定义
文档将"3轮内"落地为"死亡目标所属阵营之后完成的 `friendly_turn_end_serial` 差值不超过 3"。玩家通常理解"3轮"为全局时间流逝。若某阵营在单位密度少的场景下，友军死亡后可能要等待敌方多个单位全部行动完，友军才完成 1 次 `friendly_turn_end_serial`，导致"3轮"实际时间被拉长，玩家会感觉复活窗口异常短或异常长。

**建议**：在文档中增加"为什么采用阵营 serial 而非全局 serial"的设计 rationale；考虑引入全局 `battle_turn_end_serial` 作为辅助计数器；在 `WishProfile` 中增加 `revive_window_mode` 枚举，为后续平衡调整留后门；在 UI 中明确向玩家展示"还剩 X 个友方回合结束"。

#### 3. 9环敌意否决对抗检定的平局处理
文档规定 9 环敌意否决时，祈愿术方必须"严格高于"原施法者才算胜出；平局保留原事件。祈愿术消耗了 9 环法术位、每战仅限 1 次、1 个主要动作，平局时不仅事件保留，祈愿术的成本仍然被消耗，这意味着祈愿术方在 50% 以上的对局中都处于劣势。作为 9 环终极技能，在对抗同环技能时反而可能因为 RNG 完全失效。

**建议**：平局改为祈愿术方胜出；或平局时返还成本（不消耗 `per_battle_cast_counts`、不消耗 AP/MP，仅浪费一个主要动作）；或增加祈愿术方固定加值（如 +2 或 +4 wish_contest_bonus）。无论选择哪种，必须在文档中明确说明这是有意设计的"高风险高回报"机制。

#### 4. `fate_rewrite` 的"确定性重放"支持范围不明
文档要求"如果某个检定类型尚未支持确定性重放，validator 必须让它不可选"，但未列出当前已有哪些检定支持、哪些不支持。若当前大量技能事件的检定未记录 `replay_inputs`，则 `fate_rewrite` 的可用事件池可能非常狭窄，甚至只剩普通攻击和少数法术，导致该模式在实战中几乎无法使用。文档将该模式排在实施顺序第 8 位（倒数第二），但其依赖的改造实际上需要大规模修改多个系统。

**建议**：在文档中补充当前检定覆盖率审计表；增加降级策略（若某事件包含可重放和不可重放两种检定，允许只改写可重放的部分）；将 `fate_rewrite` 拆分为"先支持攻击/豁免重放"的 MVP 和"支持全部检定"的完整版；在 `BattleWishReactionEvent` 中增加 `replay_supported_check_types: Array[StringName]`。

#### 5. `break_desperation` 额外动作窗口与现有 interrupt action 框架兼容性
`break_desperation` 需要新增 `BattleWishExtraActionService`，若目标不是当前 active unit，则 `BattleTimelineDriver` 需"暂停当前 active unit，临时切换 active_unit_id"。仓库已有 interrupt action 机制（如借机攻击、反应技能），文档未说明 `active_extra_action_context` 如何与现有 interrupt queue 交互。如果当前已有一个 interrupt action pending，额外动作是插队、排队还是被忽略？"暂停当前 active unit，临时切换，执行完恢复"涉及多字段联动，文档未定义恢复时的精确状态边界。若额外动作窗口中目标再次触发其他 interrupt，是否会产生嵌套？

**建议**：明确与现有 interrupt framework 的互操作协议；定义状态恢复清单，列出恢复时必须精确还原的字段；增加 `allow_nested_interrupt` 标记，明确禁止嵌套额外动作窗口；将 AP 豁免逻辑集中化，新增 `BattleWishExtraActionService.is_ap_free_window()` 统一判断。

#### 6. `copy_spell` 跳过成本检查的辅助函数安全漏洞
`_validate_skill_as_wish_copy()` 和 `_build_skill_outcome_as_wish_copy()` 跳过被复制技能的 learn/material/cost/cooldown 检查和 `per_battle_cast_limit`。若未来有其他系统也使用 `per_battle_cast_limit`（如某些 BOSS 的限定技能），`copy_spell` 可能无意中成为通用绕过手段。若被复制技能的效果本身依赖于"已消耗 MP"的状态，复制出来的 outcome 可能与原版不一致。`copy_payload` 虽然要求不能再包含 `wish_mode`，但未明确限制其他敏感字段（如 `special_payload` 嵌套、`grade_delta`）。

**建议**：建立复制白名单机制，要求被复制技能必须通过显式的 `wish_copy_validation()` 契约检查；限制可复制的技能类型，明确枚举不可复制技能的特征（如依赖自身 cooldown 状态、依赖消耗后状态）；`copy_payload` 深度校验只允目标字段，拒绝任何其他字段；若被复制技能效果公式中包含 `consumed_mp` 变量，helper 应将其设为 0 或报错拒绝复制。

#### 7. `absolute_sanctuary` 的"重大伤害"阈值与强控列表定义
固定 35% 阈值在高 HP 坦克和低 HP 脆皮身上差异巨大。致死伤害的判定边界模糊：是在伤害计算后、HP 扣减前，还是在 HP 已扣减后发现 `current_hp <= 0` 时？这直接影响多段伤害场景。强控列表 `frozen`、`stunned`、`petrified`、`madness` 是硬编码枚举，若后续新增控制状态容易遗漏。"即死效果"未给出明确列表。

**建议**：在文档中明确"致死"判定发生在伤害扣减前，并说明多段伤害时每段独立判定；强控列表和即死效果改为 tag 驱动（`&"strong_control"`、`&"instant_death"`、`&"execute"`），新状态自动兼容；在 `WishProfile` 中增加 `major_damage_mode` 枚举，允许未来调整阈值模式；在回归测试计划中明确列出阈值边界值测试、多段伤害庇护触发位置测试。

---

### 四、可测试性与实施风险层面

#### 1. 实施顺序（10步）存在前置遗漏，后期存在阻塞风险
步骤 5（`BattleWishHistoryService` + `BattleWishCommitTransaction`）与步骤 8（九种 mode 实现）之间，有多个隐性前置条件未被覆盖：
- **确定性重放缺失会阻塞 `fate_rewrite` 与 `hostile_denial`**：现有 resolver 中未见记录 replay input 的机制，在步骤 8 之前必须先验证或补完。
- **Timeline Driver 的 turn-end snapshot 在步骤 5 无法独立测试**：步骤 5 的单元测试只能断言"调用了 capture"，无法验证快照内容是否足以支撑 `battle_reset`。
- **Commit Transaction 的回滚机制缺乏底层验证**：`BattleCommonSkillOutcome` 和 `BattleSkillOutcomeCommitter` 当前不支持撤销已提交的 death/loot/report/mastery，必须等到步骤 8 才能测试。

**建议**：在实施顺序中插入步骤 4.5：验证现有 resolver 是否已支持 deterministic replay；如不支持，先补充记录机制并补测试。将 `fate_rewrite`、`hostile_denial`、`battle_reset` 后移至二期，或优先实现不依赖完整快照的 `reverse_casualty`、`team_transfer`、`reality_repair` 等模式。步骤 5 的验收标准应增加一个最小可运行回滚场景。

#### 2. 新增的 9 个回归测试 runner 覆盖不足，存在重复与遗漏
遗漏的关键场景包括：
- `copy_spell` 无独立 runner：复制法术逻辑最复杂，仅靠 `run_wish_special_profile_regression.gd` 笼统覆盖，失败时难以定位。
- `break_desperation` 无独立 runner：额外动作窗口涉及 timeline 暂停/恢复、9 环 block，缺少独立回归测试。
- Commit Transaction 回滚无独立 runner：这是一个跨模式的关键事务机制。
- 外部副作用延迟（`pending_external_side_effects`）无独立 runner：高风险全局约束。
- AI 对 `special_payload` 的跳过行为无测试。

此外，`run_wish_reaction_event_regression.gd` 与 `run_wish_history_service_regression.gd` 的断言边界模糊，`fate_rewrite` 和 `hostile_denial` 都同时依赖两者，容易导致单个 runner 过于庞大。

**建议**：新增 `run_wish_copy_spell_regression.gd`、`run_wish_break_desperation_regression.gd`、`run_wish_commit_transaction_rollback_regression.gd`、`run_wish_external_side_effect_delay_regression.gd`、`run_wish_ai_skip_special_payload_regression.gd`；将 reaction event runner 按模式拆分。

#### 3. Headless 测试对 UI 相关路径覆盖严重不足
文档涉及大量 UI/交互层改动，但 headless 回归套件几乎完全绕过这些路径：`BattleBoardController` 目标选择模式切换在 headless 中完全不被执行；`BattleHudAdapter` 技能槽可用性同步的 block reason 不一致无法在 headless 中发现；`GameRuntimeBattleSelection` / `battle_session_facade.gd` 的 `special_payload` 保留与清理未被基线 runner 验证。

**建议**：新增 `run_wish_hud_preview_protocol_regression.gd`，用程序方式模拟 HUD 的 preview → issue 全链路；在 `tests/text_runtime/commands/` 下新增覆盖 `special_payload` 的文本命令场景；文档应明确要求：任何修改 `BattleHudAdapter` 或 `BattleBoardController` 的 PR，必须附加手动 UI 截图或短视频。

#### 4. `BattleWishHistoryService` 完整快照机制在大规模模拟中的性能风险极高
在 12v6 的大规模战斗中，假设 200 个事件，每个事件的 `pre_snapshot` + `post_snapshot` 都对 `BattleState.units` 做 `duplicate(true)`。`BattleUnitState` 包含 attribute_snapshot、equipment_view、status_effects 等重型嵌套对象。单次完整 deep clone 可能达到数 MB，200 个事件即数百 MB 峰值。Godot 的 `Dictionary.duplicate(true)` 会创建大量临时对象，在 headless AI vs AI 大规模模拟中会导致严重的 GC stutter 甚至 OOM。文档未定义 completed reaction events 何时释放。

**建议**：`pre_snapshot` 应记录增量 delta 而非完整 `duplicate(true)`；若必须完整克隆，应使用对象池复用 `BattleUnitState` 结构，并对 grid/terrain 使用共享引用 + copy-on-write；在 `run_wish_history_service_regression.gd` 中增加性能断言；为 history service 设置 `max_completed_events` 上限（如 50）。

#### 5. 现有 3 个基线 runner 相对于 20+ 改动文件过于薄弱
改动清单涉及 schema、runtime、rules、terrain、presentation、AI、facade、text commands 共 8 个层面，但基线只覆盖 resource validation、skill schema、skill protocol。具体风险：普通技能 pipeline 被破坏未被充分覆盖；`BattleUnitState.to_dict()` 格式破坏导致旧存档失败；Timeline Driver 被破坏无专项回归；Damage Resolver 被全局修改无足够基线。

**建议**：在基线中增加 `run_battle_timeline_driver_regression.gd`、`run_battle_unit_state_round_trip_regression.gd`、`run_battle_damage_resolver_global_regression.gd`；修改 `BattleSkillExecutionOrchestrator` 后，必须运行所有 `tests/battle_runtime/skills/` 下的 runner。

#### 6. "不新增旧 payload/schema 兼容逻辑"的政策会导致存档/配置断裂
`BattleUnitState.from_dict()` 使用 `_has_exact_fields` 严格匹配，新增 `per_battle_cast_counts` 到 `TO_DICT_FIELDS` 后，所有旧存档在加载时会因为缺少字段而 `from_dict` 返回 `null`。`BattleCommand` 若被 replay 系统以字典形式持久化，新增 `special_payload` 后旧 replay 命令反序列化时会丢失 `special_payload`。资源验证器要求"只有 `wish` 可以设置 `per_battle_cast_limit > 0`"，必须遍历所有现有 `.tres` 并确认它们未设置该字段。

**建议**：将 `per_battle_cast_counts` 设为可选字段（不加入 `TO_DICT_FIELDS`，在 `from_dict` 中 `get("per_battle_cast_counts", {})` 读取）；或明确声明这是破坏性格式变更，并提供一次性 save migration runner。在 `WishCommandPayload.from_command()` 中增加降级处理：如果 `special_payload` 缺失或为空，按非法 payload 处理（安全失败）。在 `run_resource_validation_regression.gd` 中增加断言：扫描所有现有 `data/configs/skills/*.tres`，确认没有任何非 wish 技能设置了 `per_battle_cast_limit > 0`。

#### 7. 文档精度极高，但实现复杂度与工作量严重不匹配，存在过度设计
842 行设计方案实现的几乎是"战斗运行时的小型时间旅行系统"，工程量远超一个技能的合理范围。`fate_rewrite` + `hostile_denial` + `battle_reset` 要求完整的状态回滚、确定性重放、外部副作用延迟、快照一致性，相当于在现有战斗运行时之上再包一层事务管理器。`break_desperation` 的额外动作触及核心回合模型，与现有的相位假设冲突，引入死锁或状态不一致的风险极高。"延迟外部副作用"策略要求 achievement、mastery、battle rating rewards 全部接入 `pending_external_side_effects`，波及面超出祈愿术本身。

**建议**：将方案拆分为两期：
- **一期（MVP）**：`copy_spell`、`reverse_casualty`、`team_transfer`、`absolute_sanctuary`、`reality_repair`、`break_desperation`（仅当前 active unit）。不引入完整快照回滚，不修改外部副作用提交路径。
- **二期（完整版）**：`fate_rewrite`、`hostile_denial`、`battle_reset`、`break_desperation`（任意单位）、延迟外部副作用。

文档应补充工作量估算：按现有团队速率，完整方案预计需要 3-4 周，MVP 方案预计 1-1.5 周。若坚持一期完整实现，则必须将 `run_wish_history_service_regression.gd` 和 `run_wish_reaction_event_regression.gd` 的优先级提升至阻塞性，在编写任何 mode 逻辑之前先通过这两个 runner 验证快照与回滚基础设施的可靠性。

---

> **总体结论**：`wish.md` 是一份设计精度极高但实施风险被系统性低估的方案。核心问题不在于逻辑不正确，而在于基础设施前置条件（确定性重放、快照性能、存档兼容）未在实施顺序中显式解决；测试矩阵对高复杂度模式拆分不足；基线过薄，20+ 文件的改动仅靠 3 个现有 runner 兜底；方案规模与产出比失衡。强烈建议按 MVP + 二期拆分，优先交付无回滚的 6 个模式，将命运改写与敌意否决后置。

---

## Kimi 审查意见处理裁决

最终裁决：采纳 Kimi 的总体风险判断，将方案改为 **MVP + 二期**。MVP 不做完整时间旅行系统；二期再做 `fate_rewrite`、`hostile_denial`、`battle_reset`、任意单位额外动作、确定性重放、完整 reaction rollback 和外部副作用延迟。

| Kimi 条目 | 处理 | 已更新方案 / 不采纳原因 |
|---|---|---|
| 架构 1：special profile 侵入 orchestrator | 部分采纳 | 已改为最小 special profile router。未采纳完整框架化重构，因为当前只有 `meteor_swarm` 和 `wish`，一次性平台化会扩大 MVP 风险。 |
| 架构 2：wish sidecar 生命周期残留 | 采纳 | 已要求 `start_battle()` 清空 wish sidecar，`dispose()` 释放，`_ensure_sidecars_ready()` 重新 setup。 |
| 架构 3：裸 `special_payload` Dictionary | 部分采纳 | 保留 `BattleCommand.special_payload` 作为传输面，但新增 typed accessor/validator。未采纳剥离成独立 `WishContext`，因为会破坏 HUD/facade/headless/text 共用命令链。 |
| 架构 4：通用模块混入 wish-specific 逻辑 | 部分采纳 | 已要求通用模块只接语义 hook/tag，不写 `wish_sanctuary` 等硬编码。未采纳完整 `PreDamageInterceptor` / `BattleEventObserver` 平台化框架，避免 MVP 过大。 |
| 架构 5：循环依赖风险 | 采纳 | 已要求 resolver 不反调 orchestrator 私有 helper，`copy_spell` 改用 `BattleWishCopyExecutor` / 注入服务。 |
| 状态 1：快照范围过大 | 采纳 | 已把完整 reaction snapshot 移到二期，并要求相关单位/cell delta、event 上限和轻量摘要。 |
| 状态 2：`duplicate(true)` 深拷贝不可靠 | 采纳 | 已要求二期 snapshot 使用纯数据 DTO / Dictionary，不含 `RefCounted/Object`，`post_snapshot` 用 checksum。 |
| 状态 3：CommitTransaction 原子性不清 | 采纳 | 已新增 MVP transaction buffer / 成功后 flush / 失败回滚原则；二期再扩展 reaction rollback。 |
| 状态 4：begin/complete 时序漏洞 | 部分采纳 | 已改成显式 begin/cancel/complete 状态机。未采纳 try/except 表述，因为 GDScript 不适合把该流程设计成异常控制流。 |
| 状态 5：`pending_external_side_effects` 复杂 | 部分采纳 | 已移到二期。MVP 不实现会回滚永久外部副作用的模式，因此不改 achievement/mastery/reward 提交流程。 |
| 状态 6：部分成功风险 | 采纳 | 已新增“部分成功防范原则”，多单位/多 cell/多步操作必须先全验证，提交失败整体回滚。 |
| 规则 1：最新事件窗口会被不可逆事件卡住 | 部分采纳 | 已允许纯日志/展示/bookkeeping 不占用 reaction 窗口。未采纳最近 5 个任意事件回滚，因为会重新引入跨事件时间旅行风险。 |
| 规则 2：3 轮死亡窗口 serial | 部分采纳 | 已补采用阵营 serial 的理由和 UI 文案。未采纳 `revive_window_mode` 预留枚举，避免未使用兼容面。 |
| 规则 3：9 环否决平局 | 采纳 | 已改为二期 9 环对抗平局祈愿术胜出，并写明这是 9 环、每战一次的防守性反制。 |
| 规则 4：`fate_rewrite` replay 范围不明 | 采纳 | 已把 `fate_rewrite` 移入二期，并要求确定性重放审计表与 `replay_supported_check_types`。 |
| 规则 5：`break_desperation` interrupt 兼容 | 采纳 | 已把 MVP 限定为当前 active unit；任意单位插队和嵌套 interrupt 互操作后置。 |
| 规则 6：`copy_spell` 绕过成本安全漏洞 | 部分采纳 | 已加白名单、payload 深度白名单、拒绝 special profile 和成本后状态依赖。未采纳每个技能新增 `wish_copy_validation()` 契约，MVP 用已有字段组合降低改动面。 |
| 规则 7：`absolute_sanctuary` 阈值/强控 | 部分采纳 | 已明确扣血前判定、多段逐段、tag/semantic table 驱动和边界测试。未采纳 `major_damage_mode` 预留枚举，先保持固定阈值。 |
| 测试 1：实施顺序前置遗漏 | 采纳 | 已重写为 MVP 与二期实施顺序，并把 replay/history/rollback 前置为二期进入条件。 |
| 测试 2：runner 覆盖不足 | 部分采纳 | 已补 MVP 独立 runner 和二期 runner。未采纳 MVP 中新增外部副作用延迟 runner，因为该机制已后置。 |
| 测试 3：headless 不覆盖 UI | 部分采纳 | 已补 HUD preview protocol / text command 覆盖，并要求 BattleBoardController 改动提供截图或短视频。纯协议改动不强制视频。 |
| 测试 4：历史快照性能 | 采纳 | 已规定一期不做完整历史快照，二期使用 delta snapshot、event 上限和性能基准；不把大规模模拟纳入 routine full regression。 |
| 测试 5：基线 runner 太薄 | 部分采纳 | 已改为按 touched surface 追加现有相关 runner。未采纳虚构未存在的 timeline/unit round-trip/damage global runner 名称，除非实现时确实新增。 |
| 测试 6：兼容/迁移风险 | 部分采纳 | 已明确 `per_battle_cast_counts` 为战斗瞬态，不加入长期 `TO_DICT_FIELDS`。未采纳自动迁移/旧 schema 兼容，因为仓库政策禁止未确认的兼容路径。 |
| 测试 7：整体过度设计 | 采纳 | 已作为最大裁决写入目标和实施顺序：MVP + 二期，优先交付 6 个非时间旅行模式。 |


---

## 新方案可行性评估（Kimi）

> 本评估基于对"Kimi 审查意见处理裁决"及更新后正文的审阅，给出是否可按更新方案进入实施的判断。

### 总体判断：可行，风险已降至可控范围

最大的正确决策是将方案拆分为 **MVP + 二期**。MVP 只包含 6 个正向修改模式（`team_transfer`、`reality_repair`、`reverse_casualty`、`absolute_sanctuary`、`break_desperation` 仅 active unit、`copy_spell`），彻底回避了完整时间旅行系统（跨事件回滚、确定性重放、外部副作用延迟）。这使得 MVP 从"在战斗运行时之上再造一层事务管理器"降维为"一个复杂 special profile 的正向状态提交"，工作量估算（1–1.5 周）与复杂度基本匹配。

---

### 关键改进认可

| 改进项 | 评价 |
|---|---|
| **MVP 不做完整 snapshot / history service** | 消除了 `duplicate(true)` 深拷贝不可靠、内存爆炸、GC 压力等核心基础设施风险。 |
| **`per_battle_cast_counts` 不加入 `TO_DICT_FIELDS`** | 正确遵循了仓库政策，避免旧存档断裂。瞬态状态与长期存档的边界清晰。 |
| **新增"部分成功防范原则"** | `reverse_casualty`、`battle_reset`、`team_transfer` 的"找不到格就整体失败"策略消除了验证后降级的原子性漏洞。 |
| **copy_spell 改用 `BattleWishCopyExecutor` / 注入服务** | 切断了 resolver ↔ orchestrator 的循环依赖风险，避免了在 orchestrator 上开 bypass 通道。 |
| **二期进入条件前置化** | "先通过确定性重放审计 + 纯数据 snapshot 设计 + 最小回滚场景 runner，再做 fate_rewrite/hostile_denial"的顺序严谨，避免了之前"基础设施未就绪就实现依赖模式"的阻塞风险。 |
| **runner 按 MVP/二期分层** | 补了 `copy_spell`、`break_desperation_active_unit`、`hud_preview`、`ai_skip` 等独立 runner，定位精度足够。 |

---

### 仍需在 MVP 实施中留意的 residual 风险

以下不是方案层面的否决项，而是实现阶段需要关注的具体边界：

#### 1. `copy_spell` 仍是 MVP 内复杂度最高的模式
需要构造内部 `BattleCommand`、跳过被复制技能的 cost/cooldown、禁止复制 special profile、深度校验 `copy_payload` 白名单。建议在实施时把这个模式放在 MVP 最后（文档已建议），确保 special profile router、payload validator、commit transaction 都先跑通。

#### 2. `absolute_sanctuary` 的 DamageResolver hook 实现
文档说"通用模块只接语义 hook/tag，不写 `wish_sanctuary` 硬编码"，但 MVP 仍需某种机制让 DamageResolver 在扣血前识别并消费"抵消重大伤害/即死/强控"的状态。如果仓库目前没有 `PreDamageInterceptor` 框架，MVP 中可能需要一次最小化的临时 hook（如 `BattleStatusSemanticTable` 提供的查询函数），需确保该临时 hook 不会扩散成隐含的 wish-specific 分支。建议在实现时把该 hook 设计为"状态标签驱动"的通用接口，即使第一期只被 sanctuary 使用。

#### 3. 最小 special profile router 的设计边界
裁决表说"已改为最小 special profile router，未采纳完整框架化重构"。需要确保这个"最小 router"不会变成第三种形态（比硬编码好，但比完整 registry 又缺少扩展性），导致下一个 special profile 还是需要改 router。建议 router 至少保留一个 `Dictionary[StringName, Callable]` 的注册面，哪怕当前只预填 `meteor_swarm` 和 `wish`。

#### 4. `break_desperation`（仅 active unit）的 AP 豁免
MVP 限定为仅当前 active unit，避免了 timeline 暂停/恢复的复杂度，但"该动作不消耗目标原有 AP"仍需在 `BattleRuntimeSkillTurnResolver` 中识别 `active_extra_action_context` 并跳过 AP 检查。建议在实现时把这个判断集中在一个 helper（如 `is_ap_free_window()`），避免在 resolver 多处散布条件分支。

#### 5. 二期对一期代码的兼容性
MVP 的 `BattleWishCommitTransaction` 采用"buffer + 成功后 flush"机制，二期会扩展为支持 reaction rollback 的完整 transaction。需要确保 MVP 的 transaction 接口预留了二期扩展的钩子（如 `pre_snapshot`、`rollback()` 方法先留空或抛 `NOT_IMPLEMENTED`），避免二期时大规模重构 MVP 已跑通的提交路径。

---

### 结论与推进建议

**更新后的 `wish.md` 可以作为 MVP 实施的依据。**

建议按文档中的 MVP 实施顺序推进，重点保证：
1. `special_payload` 的 validator / typed accessor 在所有入口点（HUD、facade、headless、text command、AI assembler）都被调用，不遗漏；
2. `copy_spell` 的复制白名单和 payload 深度校验在代码层面强制执行；
3. `per_battle_cast_counts` 严格保持瞬态，任何情况下不进入存档序列化路径；
4. 每个 MVP mode 的回归 runner 在合并前通过。

二期的时间旅行模式（`fate_rewrite`、`hostile_denial`、`battle_reset`、任意单位额外动作）建议等 MVP 全部跑通、基线稳定后，再按"二期进入条件"逐一解锁。

---

## 新方案可行性评估处理裁决

本轮只处理“新方案可行性评估（Kimi）”中的 5 个 residual 风险，不重新打开上一轮已经裁决过的意见。

| Kimi residual 风险 | 处理 | 已更新方案 / 不采纳原因 |
|---|---|---|
| 1. `copy_spell` 仍是 MVP 内最高复杂度模式 | 不采纳新增修改 | 已由正文覆盖：MVP 实施顺序已将 `copy_spell` 放在最后，且已写明复制白名单、禁止 special profile、拒绝成本后状态依赖、使用 `BattleWishCopyExecutor` 构建 outcome，并配置独立 regression runner。该意见作为实现提醒保留，不再重复扩写正文。 |
| 2. `absolute_sanctuary` 的 DamageResolver hook | 部分采纳 | 已补充：`BattleDamageResolver` 只能调用通用状态语义查询/消费接口，例如 `BattleStatusSemanticTable.find_pre_damage_prevention(unit, damage_context)`，按 tag / semantic 判断并消费状态，不直接判断 `wish_sanctuary`。未采纳完整 `PreDamageInterceptor` 框架，因为 MVP 只需要状态语义表的最小通用接口，完整 hook 链会扩大规则层改动面。 |
| 3. 最小 special profile router 的注册面 | 部分采纳 | 已补充：`BattleSpecialProfileRouter` 维护小型 `StringName profile_id -> handler` 表，启动时注册 `meteor_swarm` 与 `wish`，未知 profile 返回明确错误。未采纳完整 registry/framework 化设计；该 handler 表只是防止 router 退化为 orchestrator 内的新硬编码分支。 |
| 4. `break_desperation` active unit 的 AP 豁免 | 不采纳新增修改 | 已由正文覆盖：`break_desperation` 小节已规定 AP 豁免只通过 `BattleWishExtraActionService.is_ap_free_window(unit, skill_def)` 判断，且不推进 TU、不触发普通 tick、不重置 AP/move/cooldown、只消费一次。无需重复追加。 |
| 5. 二期对一期 `BattleWishCommitTransaction` 的兼容性 | 部分采纳 | 已补充：MVP transaction 使用 `begin_mvp_restore_point()`、`rollback_mvp_delta()`、`flush_buffers()` 这类只覆盖 wish delta 的接口。未采纳在 MVP 中预留空的 `pre_snapshot` / reaction rollback API，因为那会让调用方误以为二期回滚能力已可用；二期在确定 snapshot/rollback 契约后再扩展接口。 |


---

## 新方案可行性最终确认（Kimi）

> 本确认基于对"新方案可行性评估处理裁决"的审阅，给出是否可按更新后方案进入实施的最终判断。

### 本轮处理质量评估：合理、务实、无新增阻塞风险

裁决表对 5 项 residual 风险的处理策略是恰当的：

| residual 风险 | 处理策略评价 |
|---|---|
| **1. `copy_spell` 复杂度** | 不采纳新增修改是合理的。该条本质上是实施顺序提醒而非方案缺陷，正文已将 `copy_spell` 放在 MVP 末尾并配置了独立 runner，实施提醒可以保留在评估段落中供开发者参考。 |
| **2. `absolute_sanctuary` DamageResolver hook** | **部分采纳策略优秀**。采用 `BattleStatusSemanticTable.find_pre_damage_prevention(unit, damage_context)` 这一最小通用语义接口，既避免了 `wish_sanctuary` 硬编码污染 DamageResolver，又没有在 MVP 中引入完整的 `PreDamageInterceptor` 框架。这是一个"恰到好处"的中间方案。 |
| **3. 最小 router 注册面** | **部分采纳恰到好处**。`BattleSpecialProfileRouter` 维护小型 `profile_id -> handler` 表，当前只注册 `meteor_swarm` 与 `wish`，未知 profile 返回明确错误。这确保了 router 不会退化为 orchestrator 内的第三种硬编码形态，又避免了过度框架化。 |
| **4. `break_desperation` AP 豁免** | 不采纳新增修改合理。正文已覆盖 `is_ap_free_window()` 的集中判断逻辑。 |
| **5. 二期对 MVP transaction 的兼容性** | **部分采纳是正确的设计决策**。使用 `begin_mvp_restore_point()` / `rollback_mvp_delta()` / `flush_buffers()` 等 MVP 专属接口，不在 MVP 中预留空的 `pre_snapshot` / `rollback()` API，避免了"调用方误以为能力已可用"的误导风险。只要二期通过**新增方法或子类**扩展 transaction（而非修改现有 MVP 接口的语义），回滚路径就不会破坏 MVP 基线。 |

---

### 一项需要二期规划时提前确认的细微边界

residual 5 的处理策略成立的前提是：**二期扩展 transaction 时，MVP 接口的现有方法签名和行为语义保持不变**。

如果二期设计 snapshot/rollback 契约后，发现必须修改 `BattleWishCommitTransaction.flush_buffers()` 或 `rollback_mvp_delta()` 的内部语义才能支持 reaction rollback，则仍可能波及 MVP 的回归基线。建议在 MVP 落地后、二期启动前，先写一个**接口契约冻结文档**（哪怕是简短的注释），明确以下约定：

- `flush_buffers()` 只负责将已验证的 wish delta 应用到 `BattleState`，不处理外部副作用；
- `rollback_mvp_delta()` 只撤销本次 wish delta，不涉及跨事件状态；
- 二期的 reaction rollback 将通过新的 `BattleWishReactionRollbackTransaction extends BattleWishCommitTransaction` 或新增 `restore_from_snapshot()` 方法实现，不改写上述 MVP 方法的契约。

这是低成本的前置动作，但能确保 MVP 代码在二期期间作为**稳定基线**存在。

---

### 最终结论

**本轮更新后的 `wish.md` 方案可行，可以进入 MVP 实施。**

所有重大风险已通过 MVP/二期拆分、瞬态状态隔离、最小通用接口、小型 router 注册面和事务接口边界化等手段消除。唯一的二期前置依赖（transaction 接口语义冻结）不需要阻塞 MVP，可在 MVP 验收后、二期启动前完成。

---

### 编码前建议快速确认的 3 个现有代码事实

建议在开始编码前，由负责开发者快速确认以下基线假设，避免实施时发现基础设施不成立：

1. **`BattleUnitState` 的序列化白名单机制**：确认当前已有 `to_dict()` / `from_dict()` 的严格字段白名单（如 `_has_exact_fields` 或 `TO_DICT_FIELDS`），从而确认 `per_battle_cast_counts` 以纯瞬态 Dictionary 管理、不加入白名单的策略可直接落地。
2. **`BattleSkillExecutionOrchestrator` 的 meteor_swarm 分支位置**：确认当前硬编码分支的具体行号和上下文，从而确定最小 `BattleSpecialProfileRouter` 的插入点和注册时机。
3. **`BattleStatusSemanticTable` 的存在性与归属**：确认该语义表当前是否已存在于仓库中，以及其所在路径。若尚未存在，需评估是新建该表还是将 `find_pre_damage_prevention` 先放在其他已有语义/规则模块中，避免 MVP 初期引入新的全局依赖。
