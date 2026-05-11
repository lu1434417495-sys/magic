# 施法时间系统（Casting Time）与时间静滞技能设计

## 目标与硬约束

本方案要补上两类能力：

1. 技能可以拥有非瞬发的施法时间。施法者开始施法后，时间轴继续推进，施法完成时自动结算技能。
2. 添加高阶控制技能“时间静滞”。被静滞的目标从战场时间中隔离，不能行动、不能被作用，也不推进自身多数计时器。

实现必须遵守当前代码架构：

- 运行时流程仍由 `BattleRuntimeModule` 作为入口，预览和执行走现有 command 管线。
- 技能执行仍由 `BattleSkillExecutionOrchestrator` 负责，不能把技能结算散落到 timeline 或 damage 规则层。
- `BattleDamageResolver` 只做伤害和效果规则计算，不能回调 runtime，也不能直接中断施法。
- 状态含义由共享语义入口表达，不能只在某一个调用点硬编码时间静滞过滤。
- 未经确认，不添加旧存档兼容、旧 payload 兼容、字段静默注入或 fallback migration。

战斗中途保存是明确不支持的能力。最小实现不扩展 battle save payload，施法中的 pending cast 只作为运行时状态存在，并依赖现有 battle save lock：战斗中 `save_game_state()` 只能标记 dirty，`flush_game_state()` 在战斗解锁前返回 busy，不能把半场战斗写盘。

如果后续新增任何绕过 battle save lock 的正式保存、恢复型序列化、battle snapshot 导出或 debug/devtool 持久化入口，只要 `BattleState` 仍处于 battle running 就必须拒绝，并返回可见错误。不能为了保存而自动 cancel pending cast，不能通过保存路径退还本次成本、冷却或使用次数。若未来确实要支持战斗中保存，必须另起完整 battle-state schema 设计并经确认；旧 payload 继续被拒绝，除非用户明确确认需要兼容。

## 当前职责边界

相关模块应保持以下边界：

| 模块 | 现有职责 | 本方案中的新增职责 |
| --- | --- | --- |
| `CombatSkillDef` | 技能数据、消耗、等级覆盖 | 提供 `casting_time_tu` 和等级覆盖读取 |
| `BattleRuntimeModule` | command 入口、preview-first、active unit 管理 | 将有施法时间的技能转入 begin-pending-cast 分支，pending caster 不再进入可命令状态 |
| `BattleRuntimeSkillTurnResolver` | 技能可用性、资源、冷却、回合消耗 | 开始施法、扣费、记录待启动冷却、处理施法中断 |
| `BattleTimelineDriver` | 推进 `current_tu`、状态阶段、ready 队列 | 推进 pending cast 定点进度、触发完成结算，并跳过静滞单位的 ready 收集 |
| `BattleSkillExecutionOrchestrator` | 技能目标验证和效果执行 | 提供 no-cost 的 pending cast 结算入口 |
| `BattleDamageResolver` | 伤害和效果数值规则 | 返回 HP 损失信息，不持有 runtime callback |
| `BattleStatusSemanticTable` / `BattleSkillResolutionRules` | 状态语义、技能目标规则 | 提供时间静滞的共享过滤和计时冻结判断 |
| `BattleSaveResolver` / `BattleSaveContentRules` | 豁免规则和内容校验 | 支持 CON/WILL 取高、豁免成功度、按 tag 的豁免加值 |

## 施法时间数据

`CombatSkillDef` 增加：

```gdscript
@export var casting_time_tu: int = 0
@export var casting_maintenance_dc: int = 0
@export var pending_cast_binding_mode: StringName = &"implicit"
```

并提供读取方法：

```gdscript
func get_effective_casting_time_tu(skill_level: int) -> int
```

`pending_cast_binding_mode` 决定读条期间绑定目标失效时的行为：

| 模式 | 语义 |
|------|------|
| `&"hard_anchor"` | 任一绑定目标死亡/离场/进入 `time_stasis`/不可作用时，**整次读条中断** |
| `&"soft_anchor"` | 单个目标失效时**剔除并继续**，所有目标失效时才中断 |
| `&"ground_bind"` | 忽略单位目标失效，按初始记录坐标结算 |

第一版内容校验要求所有 `casting_time_tu > 0` 的技能**必须显式配置** `binding_mode`，禁止留空依赖隐式推断。

读取顺序与现有等级覆盖保持一致：先看 level override，再回退基础字段。内容校验要拒绝负数；如果项目需要固定粒度，可以在 `SkillContentRegistry` 中校验 `casting_time_tu` 是否能被最小时间步长整除。`casting_maintenance_dc = 0` 表示使用默认 DC 12，正数表示技能显式覆盖。

`casting_time_tu` 只表示基础读条长度，不直接写入绝对完成 TU。实际读条速度由 runtime 的单位个人时间速率决定：正常为 `100`，`time_slow` 为 `50`。该速率以整数百分比参与定点进度计算，不引入浮点数。未来若要做 haste 或装备词缀，也应扩展 runtime 侧 `cast_progress_rate_percent` hook，而不是把状态规则塞进 `CombatSkillDef`。

`casting_time_tu <= 0` 表示瞬发，继续走当前执行路径。只有 `casting_time_tu > 0` 才进入 pending cast 流程。

第一版只允许“开始时确定目标、完成时延迟结算效果”的技能配置 `casting_time_tu > 0`。内容校验必须拒绝以下技能使用读条：

- charge / 冲锋。
- path-step AOE 或依赖移动路径逐格结算的技能。
- ground precast relocation。
- 自身强制位移、跳跃、冲刺、传送、交换位置。
- 任何开始施法时需要移动或改变坐标、但完成时又需要重查路径、射程、视线或落点的技能。

这些技能若未来要支持读条，需要另写“冻结执行计划”设计；不能把它们塞进本方案，否则会和“完成时不重查射程、视线、路径”的口径冲突。

内容校验采用**双层策略**：

**硬规则（必须满足，加载时拒绝）：**
- `casting_time_tu > 0` 的技能若显式标记 `incompatible_with_casting_time = true`，或 `target_mode` / cast variant 显式声明 `self_relocation = true`，直接报错。
- `casting_time_tu > 0` 的技能**必须**显式配置 `pending_cast_binding_mode`，禁止留空。

**软规则（启发式扫描，M2 进 CI 警告）：**
- `SkillContentRegistry` 基于现有字段、`effect_type`、`forced_move_mode`、cast variant 配置做启发式检测。
- 若检测到 charge、path-step AOE、自身位移等疑似不兼容形态，但设计者未显式标记，输出 content warning 供人工 review，不阻塞加载。

第一版不强制禁止“无实际成本、无实际效果、无风险”的读条技能，这类内容是否允许由设计者决定，不作为验证器硬规则。

## Pending Cast 运行时状态

施法中状态必须是完整快照，不能只用一个 `is_casting` 布尔值。最小状态建议放在 `BattleUnitState` 的运行时字段中，先不加入 `TO_DICT_FIELDS`：

```gdscript
var pending_cast: Dictionary = {}
```

结构：

```gdscript
{
	"skill_id": StringName,
	"variant_id": StringName,
	"route": StringName, # unit / ground
	"target_unit_ids": Array[StringName],
	"target_coords": Array[Vector2i],
	"started_at_tu": int,
	"base_casting_time_tu": int,
	"remaining_cast_progress": int,
	"estimated_complete_at_tu": int,
	"cast_transaction": Dictionary,
	"spell_control_context": Dictionary,
	"cast_sequence": int,
	"source_unit_id": StringName
}
```

权威读条状态是 `remaining_cast_progress`，不是 absolute `complete_at_tu`。初始化时：

```gdscript
remaining_cast_progress = casting_time_tu * 100
```

每个 timeline step 按单位个人时间速率扣减：

```gdscript
remaining_cast_progress -= elapsed_tu * cast_progress_rate_percent
```

完成条件是 `remaining_cast_progress <= 0`。`estimated_complete_at_tu` 只允许作为 UI / 文本快照估算，不参与规则结算。

`BattleUnitState` 增加轻量 helper：

- `is_casting() -> bool`
- `begin_pending_cast(payload: Dictionary) -> void`
- `clear_pending_cast() -> void`

这些 helper 只管理数据，不执行技能、不扣资源、不写战报。

`cast_transaction` 记录本次开始施法已经实际支付的成本，以及“待启动冷却”的信息。开始读条时扣 AP / MP / 次数 / 材料等施法成本，但不立刻启动技能冷却；读条完成、读条被打断，或开始阶段 spell control 直接失败时，才从对应 `current_tu` 调用 `start_skill_cooldown_at(skill_id, current_tu)`。该字段不是默认返还承诺；正常结算失败、被打断、被静滞中断都不返还。

`spell_control_context` 记录开始施法时已经结算过的 spell control / 反噬 / 过载等一次性判定结果。pending cast 完成时不能重新跑普通施法入口，也不能再次触发 spell control 或二次扣费。该 context 只能保存可深拷贝纯数据，例如 bool、int、String、StringName、Vector2i、Array、Dictionary；不能保存 `Resource`、`Object`、`Node`、`Callable` 或任何引用生命周期不稳定的对象。

`cast_sequence` 的计数器属于 battle state 运行时，而不是全局 runtime 单例。实现可在 `BattleState` 增加 runtime-only `next_cast_sequence`，并提供 `BattleState.allocate_cast_sequence()` 之类入口；`BattleRuntimeModule` 只能通过该入口拿号，不能自己持有计数器。clone 必须复制该计数器，正式 save payload 不保存它。这样 AI 分支模拟或临时 battle state fork 不会污染主线序号。

`pending_cast` 不进入 save payload，但 `BattleUnitState.clone()` 必须 deep-copy 它。当前 clone 通过 `to_dict()` / `from_dict()` 后手动补 runtime-only 字段；实现时要把 `pending_cast` 加入这段手动复制，保证模拟、预览或临时 battle state fork 不会丢失读条状态。测试要覆盖“序列化不保留、clone 保留”。

任何正式保存或恢复型序列化入口如果在 battle running 时看见 pending cast，必须 fail loud，而不是静默丢弃。`BattleUnitState.to_dict()` 不应保存 `pending_cast`；如果未来有人试图从 battle running 路径调用正式序列化，外层 battle save lock / snapshot guard 必须先拒绝。

## Tag 数据模型

第一版不新增 `CombatEffectDef` 或 `BattleStatusEffectState` 的导出字段，避免扩大资源 schema。所有本方案新增 tag 统一承载在 `params` 中：

- 状态 tag：`status_entry.params.status_tags = ["time_stasis", "time_slow", "time_reverberation"]`
- 效果 tag：`effect_def.params.effect_tags = ["can_target_time_stasis", "dispel_time_stasis"]`
- 豁免 tag：沿用效果/保存配置的 save tag，并新增 `time_stasis` tag 常量。

规则层提供统一读取入口：

```gdscript
static func get_status_tags(status_entry: BattleStatusEffectState) -> Array[StringName]
static func status_has_tag(status_entry: BattleStatusEffectState, tag: StringName) -> bool
static func has_time_slow(unit: BattleUnitState) -> bool
static func get_effect_tags(effect_def: CombatEffectDef) -> Array[StringName]
static func effect_has_tag(effect_def: CombatEffectDef, tag: StringName) -> bool
```

`SkillContentRegistry` 必须校验这些 params 字段只能是字符串 / `StringName` 数组，不能接受空 tag、非数组或混入其他类型。helper 内部必须把所有 tag 归一为 `StringName`；运行时比较、Dictionary key 和 `in` 判断只能使用归一化结果，不能直接拿原始 `params` 数组比较。所有 preview、AI、orchestrator、ground、chain、repeat、terrain tick 和 dispel 入口都只能通过这些 helper 判断 tag。

## 施法流程

开始施法：

1. `BattleRuntimeModule.issue_command(TYPE_SKILL)` 仍然先走当前 preview。
2. 若技能瞬发，走当前路径，不改变行为。
3. 若技能有施法时间，不能进入普通 `_handle_unit_skill_command()` / `_handle_ground_skill_command()`。runtime 必须转入**独立的 begin-pending-cast 分支**（如 `_handle_casting_time_skill_command`）。
4. **先执行 spell control / 反噬 / 过载等门槛检定**：
   - `critical_failure`：扣除 50% AP（或固定反噬值）作为魔力失控惩罚，启动冷却，**不创建 pending cast**；
   - `failure` / `skip_effects = true`：**不扣可返还资源**（AP/MP/次数），不启动冷却，不创建 pending cast；
     - **不结束回合**：`has_taken_action_this_turn` 保持原状，`current_ap` 不清空；
     - 设置 `turn_casting_exhausted = true`，`attempted_spells_this_turn.add(skill_id)`；
     - 自动执行 **安全驱散**（不消耗任何资源，不产生额外副作用，不可被反制/打断）；
     - 战斗日志输出高亮文本（如"魔力失控，施法未能开始。你仍可进行应急行动。"）；
   - `success` / `critical_success`：进入阶段 2。
5. 门槛通过后，`BattleRuntimeSkillTurnResolver` **扣除**本次施法成本和使用次数，但**不启动技能冷却**。随后把已支付成本、待启动冷却和 spell control 结果写入 `cast_transaction` / `spell_control_context`。
6. begin 分支必须记录一次 skill action，并显式设置 `has_taken_action_this_turn = true`、`is_resting = false`。即使该技能 AP 成本为 0，只要进入读条，也清空本回合 AP 并结束 active turn。
7. pending cast 初始化 `remaining_cast_progress = casting_time_tu * 100`；不得把 `battle_state.current_tu + casting_time_tu` 作为权威完成条件。
8. `cast_sequence` 使用 battle state 运行时单调递增序号，用于同一 TU 多个施法完成时稳定排序。

> **M1 工程约束**：读条专用入口必须与瞬发路径物理隔离、自包含，不共享中间状态。瞬发技能路径在 M1 保持现有行为不变（先扣费后 spell control）。M2 统一重构为三阶段管线（`validate→commit→apply`），届时删除读条专用入口。代码中必须标注 `# FIXME(M2): 临时性重复，待三阶段管线重构后统一`。

preflight / block 检查失败时不能留下半扣资源或半写 pending cast。

**spell control failure 后的应急行动（M1）：**

当 `turn_casting_exhausted = true` 时，本回合内：
- **禁止再次尝试任何读条技能**（`casting_time_tu > 0` 的技能 `preview_command` / `issue_command` 均拒绝）；
- **禁止再次尝试同一 `skill_id`**（即使换成瞬发模式也不允许）；
- **允许执行以下行动**：
  - `TYPE_MOVE`：正常移动；
  - `TYPE_WAIT`：等待；
  - `TYPE_ITEM`：使用非攻击性道具（仅限带有 `TAG_HEALING`、`TAG_REMEDY` 等治疗/解状态标签的道具；若现有 `ItemDef` 无此分类，M1 必须显式维护白名单枚举，不允许临时性魔数判定）。
- **M1 不开放**：`TYPE_SKILL`（包括戏法）、`TYPE_CHANGE_EQUIPMENT`，推迟到 M2 基于 `skill.tags` 审计后再扩展。

**非法行动尝试的拒绝规格**：
- UI 层推荐方案：施法失败后，技能栏读条技能按钮**提前置灰禁用**（`preview_command` 返回不可用的原因字符串，UI 根据该字符串置灰）；
- 若玩家通过快捷键/宏等手段绕过 UI 发送指令，`issue_command` 在入口层检测 `turn_casting_exhausted` 或 `attempted_spells_this_turn` 命中，返回 `ERR_ACTION_UNAVAILABLE`，写一行日志提示，**不消耗任何资源**，操作权仍留在当前回合。

`turn_casting_exhausted` 与 `attempted_spells_this_turn` 为**回合级临时状态**，不进入 `TO_DICT_FIELDS`，`BattleTimelineDriver._activate_next_ready_unit()` 在每个回合开始时重置。战斗存档/快照不保留该字段；战斗回放允许 failure 后的行动选择与原始战斗不同，这是可接受的回放分支差异。

施法中：

- pending caster 不会进入 `ready_unit_ids`，也不会进入 `unit_acting`，因此没有移动、等待、换装备、释放其他技能的命令机会。
- **读条期间可随时主动取消**（UI 按钮或 `TYPE_CANCEL_CAST` 命令）。取消时清除 `pending_cast`，**全额返还已扣资源**（AP/MP/次数/材料），**不启动技能冷却**。取消是一个单向、无状态机的操作，不需要处理部分退款或取消后进入 CD 的复杂逻辑。
- 单位仍处于正常战场时间中：状态、地面效果、自然恢复和行动进度按 timeline 推进。已经存在的技能冷却继续按个人时间恢复；本次正在读条的技能冷却尚未启动。
- 每个 timeline step 在状态伤害、地形、屏障等阶段之后，ready 收集之前，推进 pending cast 的 `remaining_cast_progress`。正常速率为 `100`，`time_slow` 速率为 `50`。如果 `time_stasis` 成功施加静滞，pending cast 已被中断；如果目标豁免成功只获得 `time_slow`，pending cast 不会中断，但读条推进会按 50% 速率减慢。
- 当施法中的单位 `action_progress` 达到行动阈值时，消耗一次行动阈值，但不加入 `ready_unit_ids`，不触发 turn start，不给 AP，不重置 per-turn charges，也不执行控制状态跳过逻辑。若一次大步进跨过多个行动阈值，每跨过一次都消耗阈值并跳过 ready；若最后仍有余量，保留余量继续累计，不能无条件清零。
- 第一版不允许内容定义“行动阈值达标但不算 turn start 时触发”的状态或 hook。pending caster 跨阈值只做“消耗阈值 + 跳过 ready + 记录事件”，不触发任何 turn-start-like hook。
- 上述“读条中跳过行动机会”必须同时推进冷却 anchor，等价于把 `last_turn_tu` 和冷却恢复结算到当前 TU，避免因为没有真正进 ready 而让技能冷却停滞。
- 若读条完成或被中断时尚未跨过行动阈值，也要在清除 pending cast 前把该施法者的冷却 anchor 同步到当前 TU。这样读条期间已有冷却的恢复对快照、战报和后续行动都是连续的，也不会在下一次真实 turn start 被重复追算。本次读条技能的冷却仍只在完成/中断/开始阶段失败时启动。
- 如果共享语义判断 `blocks_pending_cast(unit, pending_cast)` 为 true，pending cast 被清除，已消耗资源不返还。该判断至少覆盖死亡、离场、击晕、睡眠、麻痹、石化、恐惧导致无法行动、带 `blocks_pending_cast` 语义/tag 的未来控制，以及 `time_stasis`。当前版本不设计沉默状态；未来的变形、魅惑、致盲、震慑等控制如果会让施法者无法维持施法控制、无法合法执行该类技能，或状态语义没有明确标成“不打断读条”，默认打断 pending cast。
- 施法者进入 `time_stasis` 视为硬中断，而不是冻结读条。`time_stasis` 是 9 环级反制资源；打断 pending cast 不返还成本，并从中断 TU 启动本次技能冷却。
- 强制位移、拉拽、推开、传送、交换、坠落、抓取拖动等外部位移都会中断 pending cast，不区分敌方、友方、地形或陷阱来源。第一版不提供“不打断施法”的内容 tag 例外；若未来要加例外，需要另行确认。
- 绑定单位目标的 pending cast 以开始时的 `target_unit_ids` 作为绑定目标。目标只是移动或被推开时，不中断读条（本战斗系统不设计遮挡机制，读条技能视为已锁定目标）。单目标绑定技能的目标死亡、离场、进入 `time_stasis`、被移出 battle state 或变为不可被该技能作用时，立即中断读条；后续复活不会恢复这次 pending cast。多目标绑定技能中某个目标失效时，只从 `target_unit_ids` 中剔除该目标并写轻量战报/结构化事件；只要仍有至少一个合法绑定目标，读条继续。所有绑定目标都失效时，整次 pending cast 才中断并启动冷却。
- `target_mode = unit` 但带 AoE 的技能仍按绑定单位目标处理，因为锚点是单位而不是坐标；如果设计希望目标死亡后仍按旧位置落地，必须把技能做成 ground target 并在开始时记录坐标。

完成施法：

1. `BattleTimelineDriver` 在每个 timeline step 的状态伤害、地形、屏障等阶段之后，ready 收集之前，先推进所有 pending cast 的 `remaining_cast_progress`，再收集 `remaining_cast_progress <= 0` 的 pending cast。
2. 对到点的 pending cast 调用 `BattleSkillExecutionOrchestrator.resolve_pending_cast(...)`。这是独立 no-cost 完成入口，不得进入 `_validate_unit_skill_targets()`、`_validate_ground_skill_command()`、`_consume_skill_costs()`、`_resolve_unit_spell_control_after_cost()`、`_resolve_ground_spell_control_after_cost()`、`_can_skill_target_unit()` 或普通 `_handle_*_skill_command()`。
3. no-cost 入口不再检查 AP、MP、冷却、次数，也不再次扣费或重新结算 spell control。它使用 pending cast 中快照的 `spell_control_context` 调用 effect-only helper。
4. 同一 timeline step 有多个 pending cast 到点时，按 `completed_at_tu`、`cast_sequence`、`source_unit_id` 字符串排序。这样 A/B 同时完成、互相击杀或打断时结果可复现。前一个结算造成后一个施法者死亡、离场、静滞或控制时，后一个在轮到自己时按中断处理。
5. 结算前再次检查施法者仍存在、存活、仍在 battle state 中，且没有 `time_stasis`。不要复用普通 `actionable` 判断，避免把 AP、turn state 或普通控制状态重新带入完成结算。读条期间会中断 pending cast 的控制状态，应在状态应用时已经通过 `interrupt_pending_cast()` 清理。
6. 完成时**不重新检查射程、视线或路径**。由于本战斗系统不设计遮挡机制，读条技能在完成时视为已锁定目标，不因目标移动、被推挤或离开原始射程而失效。绑定目标的**死亡、离场、进入 `time_stasis` 或变为不可作用**仍按 `binding_mode` 规则中断或剔除；如果仍出现 stale pending cast，先按绑定规则清理目标，没有剩余合法绑定目标时按中断处理并写结构化事件。
7. 地面 / 范围读条技能只绑定记录的 `target_coords`。读条完成后一定按这些坐标发出；范围内完成时有哪些合法单位，就影响哪些单位。即使开始瞄准时范围里的单位已经死亡或离开，法术也照常落地。普通 effect 跳过 `time_stasis` 单位；地形/高度/地面效果跳过 `time_stasis_cell_lock` 锁定格，仍可改变范围内其他未锁定格子。
8. 效果结算完成后清除 pending cast，再写入本次技能冷却；冷却起算 TU 仍为 `completed_at_tu`。中断时先写中断日志/事件并清除 pending cast，再写入冷却；冷却起算 TU 为 `interrupted_at_tu`。begin 阶段 spell control `skip_effects = true` 时先写失败事件、不创建 pending cast，再从当前 TU 启动冷却。

此流程保证技能执行仍集中在 orchestrator，timeline 只负责“到点触发”。

## 读条维持检定与中断

所有 `casting_time_tu > 0` 的技能在读条期间都要做维持检定。维持检定不能由 `BattleDamageResolver` 主动处理。正确做法是 runtime 在拿到伤害结果后统一检查 HP 损失：

```gdscript
func handle_casting_maintenance_damage(unit: BattleUnitState, actual_hp_loss: int, context: Dictionary) -> void
```

触发条件：

- `unit.is_casting()` 为 true。
- `actual_hp_loss > 0`，只看真正扣到 `current_hp` 的伤害，不看被护盾、屏障、临时 HP、护盾型 HP 或免疫完全吸收的部分。
- 反弹、反甲、自伤、过载等回到施法者自身的 HP 损失同样计入读条维持检定。第一版不提供跳过维持检定的内容 tag 例外；若未来要加例外，需要另行确认。

判定（M1 简化三档阈值）：

```gdscript
var dc: int
if skill_def.casting_maintenance_dc > 0:
    dc = skill_def.casting_maintenance_dc
elif actual_hp_loss <= 3:
    dc = 0  # 小额伤害不触发维持检定
elif actual_hp_loss > 15:
    dc = 15 # 大额伤害提高 DC
else:
    dc = 12 # 默认
var total := d20 + max(constitution_modifier, willpower_modifier)
```

| 伤害量级 | 触发规则 | DC |
|---------|---------|-----|
| `actual_hp_loss <= 3` | **不触发**维持检定 | — |
| `4 ~ 15` | 触发维持检定 | **12** |
| `> 15` | 触发维持检定 | **15** |

默认能力值为 CON / WILL 修正取高；若后续有技能需要单一能力或其他伪能力，必须由技能显式配置。技能可通过 `casting_maintenance_dc` 显式覆盖上述动态 DC。

失败时清除 pending cast，资源不返还，从失败时的 `current_tu` 启动本次技能冷却，并写入战报。

所有会造成 HP 损失的 runtime 路径在结算后，必须由 damage source **owner 层**统一通知：

```gdscript
# BattleRuntimeModule 提供的便捷包装
func _notify_hp_loss_if_casting(unit_state: BattleUnitState, actual_hp_loss: int) -> void:
    if actual_hp_loss <= 0 or not unit_state.is_casting():
        return
    resolve_casting_interruption_check(unit_state, {
        "source": &"hp_loss",
        "actual_hp_loss": actual_hp_loss,
        "damage_percent": int(actual_hp_loss * 100 / max_hp),
    })
```

调用方（owner 层）包括：
- 普通技能命中和效果伤害（`BattleSkillExecutionOrchestrator._apply_unit_skill_result()`）。
- 链式伤害（`_apply_chain_damage_effects()`）。
- 随机/重复攻击（`BattleRepeatAttackResolver`）。
- 地面效果伤害（`BattleRuntimeModule._apply_ground_unit_effects()`）。
- timeline 状态 tick 伤害（`BattleSkillTurnResolver.apply_unit_status_periodic_ticks()`）。
- 屏障或特殊防护结算后仍传到 HP 的伤害（`BattleLayeredBarrierService`）。

契约是“每次实际 HP 下降后，在**外层 owner** 正好调用一次”。`actual_hp_loss <= 0` 时不检定；同一段 repeat、chain、terrain tick 或 barrier damage 不能既在内层 `_apply_damage_to_target()` 又在外层 owner 重复调用。实现时应在各 owner 文件靠近 HP 实际写入的位置插入 `_notify_hp_loss_if_casting()`，测试要覆盖漏检和双检。

> **M1 工程约束**：HP Loss 通知走 owner 层 `_notify_hp_loss_if_casting()` 包装，非伤害打断（位移、状态）直接调用 `resolve_casting_interruption_check()`。M2 评估迁移到 `BattleCastingCheckContext` 集中式拦截。

这样不会污染 rules 层，也不会漏掉非普通攻击的中断来源。

非伤害打断走同一个 pending cast 清除入口，但不走维持检定 DC：

- `blocks_pending_cast()` 变为 true 的状态应用，例如睡眠、麻痹、石化、击晕、行动完全恐惧、带 `blocks_pending_cast` 语义/tag 的未来控制、`time_stasis`。当前版本不设计沉默状态；未来的变形、魅惑、致盲、震慑等控制默认偏保守打断，除非状态语义明确声明不影响读条维持。
- 强制位移、拉拽、推开、敌方传送、抓取/缠绕等控制效果。
- 施法者离场、死亡、被替换、被移出 battle state。

开始读条阶段如果 spell control / overload / backlash 造成施法者实际 HP 损失，处理顺序必须是：先创建 pending cast，再应用 HP 损失，再触发维持检定。若 spell control 直接 `skip_effects = true`，没有进入 pending cast，则不触发维持检定，但仍从当前 TU 启动冷却。

因此 runtime 至少需要三个 sidecar 入口：一个处理 HP 伤害后的读条维持检定，一个处理控制/状态导致的硬中断，一个处理绑定目标失效后的剔除或中断。它们最终都复用同一组 pending cast 清理和事件工具，保证清理、启动冷却、战报和结构化事件一致。所有 pending cast 中断事件至少包含 `source_unit_id`、`skill_id`、`reason`、`interrupted_at_tu`、`cast_sequence`、`refund=false`；所有完成事件至少包含 `source_unit_id`、`skill_id`、`started_at_tu`、`completed_at_tu`、`cast_sequence`、`result`。多目标绑定只剔除单个失效目标时写轻量事件，包含 `source_unit_id`、`skill_id`、`removed_target_unit_id`、`reason`、`remaining_target_count`，但不启动冷却、不算中断。

## 状态过渡 Owner

`time_stasis` 的应用和释放不能散落在各个 resolver 里。实现必须提供统一 runtime status transition API，例如：

```gdscript
func apply_runtime_status(source_unit, target_unit, status_entry, context) -> Dictionary
func remove_runtime_status(target_unit, status_id, reason, context) -> Dictionary
func on_status_applied(target_unit, status_entry, context) -> void
func on_status_released(target_unit, status_entry, reason, context) -> void
```

所有状态新增、刷新、移除、驱散和自然过期都要经过这个入口。当前直接 `set_status_effect()` / `erase_status_effect()` 的调用点，如果会触发战斗语义，必须改走该入口。

`time_stasis` 的 release 语义只在自然过期和显式允许的解除/驱散移除时触发：补偿 anchor、添加或刷新 `time_reverberation`、写入战报。release reason 必须使用明确常量/枚举，至少包含 `natural_expire`、`dispel`、`death`、`leave_battle`、`battle_end`、`scene_unload`、`replace`、`cleanup`；只有 `natural_expire` 和 `dispel` 触发 `time_reverberation`。死亡、离场、战斗结束、场景卸载、状态替换或清理不触发 `time_reverberation`。刷新同一个 `time_stasis` 状态不触发 release；正式内容中静滞单位不能再次成为普通目标，因此不支持刷新、延长或叠加新的 `time_stasis`。

所有状态新增、刷新、移除、驱散和原地变更在结算后都必须重新检查一次 `blocks_pending_cast()`。如果单位正在读条且状态变化后变为不可维持读条，立即中断 pending cast，成本不返还，并从当前 TU 启动冷却。

## 时间静滞状态语义

`time_stasis` 是一种**外部时间异常场**（Temporal Stasis Field），而非目标自身的时间冻结。该状态由外部施法者创造并维持，其持续时间由外部战场时间流决定，因此正常减少。受影响的单位被从**个人时间线**中剥离，共享语义必须至少覆盖：

- 不能行动。
- 不能进入 ready 队列。
- 不能触发 reaction、反击、借机攻击或其他离回合响应。
- 不能被普通敌方或友方技能直接选为目标。
- 不能被范围、链式、随机目标、治疗、护盾、强制位移或单位型地面效果作用。
- 不推进 `action_progress`。
- 不推进技能冷却。
- 不结算普通 DOT / HOT / 状态 tick。
- 不降低其他状态的持续时间。
- 不降低单位私有护盾持续时间。
- 不触发 turn start、per-turn charge reset 或回合开始状态效果。

关键例外：

- `time_stasis` 的 duration 计时器不受任何时间系状态影响，包括其他 `time_stasis`。对已经处于 `time_stasis` 的单位，新的 `time_stasis` 尝试既不能刷新、延长，也不能叠加。
- `time_stasis` 自身持续时间必须继续减少，否则状态会永久冻结自己。
- 明确带有 `can_target_time_stasis` 或 `dispel_time_stasis` tag 的驱散/解除类技能可以选中静滞单位，但只能移除、缩短或检查 `time_stasis` 相关状态，不能顺带造成伤害、治疗、位移或套其他效果。
- `time_reverberation` 是时间静滞的反制余波，会被新的 `time_stasis` 冻结。也就是说，第二次静滞期间它不继续流逝，解除静滞后仍保留未消耗的反制窗口。
- 静滞单位身上的有益和有害状态都按同一规则冻结。该状态会保留已有有益 buff，这是设计接受的结果，不做善恶分流。

建议在 `BattleStatusSemanticTable` 或其相邻规则入口提供统一判断：

```gdscript
static func has_time_stasis(unit: BattleUnitState) -> bool # 按 status tag 判定，不只按单一 status_id
static func blocks_unit_actions(unit: BattleUnitState) -> bool
static func blocks_unit_targeting(unit: BattleUnitState, effect_def: CombatEffectDef = null) -> bool
static func freezes_timeline_timers(unit: BattleUnitState) -> bool
static func blocks_pending_cast(unit: BattleUnitState, pending_cast: Dictionary) -> bool
static func can_effect_affect_time_stasis(effect_def: CombatEffectDef) -> bool
```

调用点必须共用这些判断，而不是各自写字符串判断。

## 时间静滞 Timeline 规则

应用 `time_stasis` 时：

- 立即从 `ready_unit_ids` 中移除目标。
- 保留进入静滞前已有的 `action_progress` 余量；静滞期间不再增长，解除后从该余量继续推进。
- 如果目标正在施法，pending cast 立即中断，资源不返还，并从当前 TU 启动本次技能冷却。这里的“目标”包括被时间静滞命中的施法者自己，不能让读条进度在静滞期间继续推进。
- 如果目标未来刚好在同一个 timeline step 到 ready，也不能被加入 ready。
- 立即把目标的冷却 anchor 补到当前时间：`last_turn_tu = max(last_turn_tu, current_tu)`，`last_turn_tu < 0` 时设为 `current_tu`。这样应用静滞前已经流逝的 TU 不会在解除后第一次回合开始时被错误追算。
- 给目标当前全部 `occupied_coords` 加临时 `time_stasis_cell_lock` 标记。该标记不是地形替换，不改变原地形/高度；大体型单位的全部占用格都要锁定。

推进 timeline 时：

- `advance_unit_status_durations()` 对静滞单位只递减 `time_stasis` 自身，跳过包括 `time_reverberation` 在内的其他状态。
- timeline tick 伤害和治疗跳过静滞单位。
- action progress 收集跳过静滞单位。
- 冷却冻结不能只靠“不给回合”。因为当前冷却在回合开始时按 `current_tu - last_turn_tu` 追算，静滞期间必须补偿这个 anchor。最小实现采用每个 timeline step 对静滞单位前推 `last_turn_tu += tu_delta` 的方式，目标是让下一次 `consume_turn_cooldown_delta()` 不把静滞期间的 TU 算进去。
- 单位私有 shield duration 跳过静滞单位。全局 barrier 和 battlefield-level 计时器仍按战场时间推进。
- `time_stasis_cell_lock` 锁定的格子不接受普通地形/高度/地面效果变更，包括 `terrain_replace`、`height_delta`、timed terrain effect、地面破坏/生成等。范围地形技能仍可影响其他格子，但跳过锁定格；静滞期间错过的地形变化不在解除后回补。
- timed terrain 对静滞单位不结算，也不在解除后补 tick。
- **地形效果三阶段语义**：
  1. **静滞期间（`during_stasis`）**：单位从战场时间中剥离，**完全不结算**任何地形效果（伤害、移速、状态）。
  2. **解除瞬间（`release_moment`）**：单位重新接入时间流，但这是一个瞬时事件而非 tick，**不触发**“进入地形”或“首次踏入”效果。
  3. **解除之后（`after_stasis`）**：单位已恢复正常时间流，**按普通规则结算**所在格子的全部地形效果。例如火墙在静滞单位脚下燃烧 30 TU，静滞单位不受伤；解除后若火墙仍在持续，该单位在解除后的第一个 timeline step 正常受到火墙伤害。
- 静滞单位不会因地形、屏障或其他战场对象触发坠落、terrain damage、forced displacement、挤压、碰撞伤害或自动位移。解除后也不回溯补结算静滞期间错过的 tick、坠落、伤害或位移。
- 静滞单位占用格仍视为被占用，不允许其他单位移动进入、传送落入、被推挤进去，或把这些格子作为合法落点。
- 可以把 AoE 落点选在静滞单位占用格；静滞单位和锁定格本身跳过普通 unit / terrain effect，范围内其他未锁定格和合法单位照常处理。
- 独立 barrier / wall / battlefield object 不因为 `time_stasis_cell_lock` 自动失效，它们按自己的 placement / overlap 规则存在和流逝；但任何 barrier 如果会直接作用于静滞单位，例如伤害、推移、包裹、挤压或把单位作为目标，都必须被静滞目标过滤跳过。解除静滞瞬间不自动处理屏障挤压、碰撞或位移，之后按正常移动/技能/屏障规则处理。

解除 `time_stasis` 时：

- 先把目标的个人时间 anchor 同步到当前 `current_tu`，避免静滞期间的 TU 在解除后被冷却、自然恢复或状态计时补结算回来。
- 移除目标占用格上的 `time_stasis_cell_lock`。因为锁只是临时标记，不替换地形，所以解除时不做地形/高度恢复或回滚。
- 仅当 release reason 是自然过期或显式允许的解除/驱散时，添加 `time_reverberation` 状态。
- `time_reverberation` 的持续 TU 由效果/技能内容配置决定，代码不硬编码任何 round 常量。
- 若目标已有 `time_reverberation`，解除静滞时刷新为两者剩余时间的较大值，不叠加总时长。
- `time_reverberation` 只影响再次受到 `time_stasis` 时的豁免，不应给所有豁免通用加值。

## 时间减速状态语义

`time_slow` 是独立的时间系减速状态，不等同于现有普通 `slow`。它不是时间隔离状态：

- 目标仍可行动，只是行动节奏变慢。
- 目标仍可被敌方/友方技能、范围效果、治疗、护盾、强制位移、地面效果、reaction、反击和借机攻击作用。
- 目标不会被移出 ready 队列，已有 pending cast 也不会仅因 `time_slow` 中断。
- `time_slow` 自身持续时间按普通 `duration_tu` 在战场时间中减少，不被冻结。

第一版 `time_slow` 只影响单位主动节奏，避免把成功豁免后的轻惩罚做成半静滞：

- `action_progress` 获得量按 `action_progress_rate_percent` 缩放。
- 技能冷却恢复量按 `cooldown_recovery_rate_percent` 缩放。
- 自然体力/资源恢复量按 `resource_recovery_rate_percent` 缩放。
- pending cast 读条推进量按 `cast_progress_rate_percent` 缩放。权威实现仍使用 `remaining_cast_progress -= elapsed_tu * cast_progress_rate_percent` 的整数定点模型，不引入浮点数。
- 其他状态 duration/tick、单位私有 shield duration、全局 terrain/barrier 仍按正常战场时间推进。

当前 `time_slow` 固定按 50% 个人时间速率设计，不考虑多来源叠加。实现仍通过状态 params 显式配置这些百分比，`SkillContentRegistry` 要求内容填写为整数；运行时不使用浮点数。若未来要让 `time_slow` 同时减速 DOT/HOT、护盾或其他状态计时，需要单独确认，因为这会产生“减速敌人却延长敌方有益护盾 / 减慢敌方 DOT”的双向副作用。

当前时间静滞技能链路下，`time_slow` 只会在目标成功抵抗 `time_stasis` 后出现。若目标正在读条且豁免成功，`time_slow` 不会中断读条，但会把后续读条推进降到 50%；若目标豁免失败并进入 `time_stasis`，pending cast 已经按静滞规则中断，不再存在可减速的读条。

## 时间静滞技能数据

建议技能参数：

| 字段 | 值 |
| --- | --- |
| 技能名 | 时间静滞 |
| 等级 | 9 |
| 目标 | 单体生物 |
| 射程 | 10 格 |
| 施法时间 | 0 或按内容节奏配置 |
| 建议持续时间 | 3 ~ 4 TU（约 1 ~ 2 回合），由 `duration_tu` 内容配置，代码不硬编码 |
| 豁免 | CON / WILL 取较高修正 |
| 豁免 tag | `time_stasis` |

豁免结果：

| 结果 | 效果 |
| --- | --- |
| critical_failure | `time_stasis`，持续 `critical_failure_duration_tu` |
| failure | `time_stasis`，持续 `failure_duration_tu` |
| success | `time_slow`，持续 `success_time_slow_duration_tu` |
| critical_success | 无效果 |

实际持续时间必须由技能/效果内容配置为具体 `duration_tu`，运行时代码不硬编码固定 round 常量。

`time_slow` 必须配置为独立 temporal 状态，带 `time_slow` status tag。不能复用现有普通 `slow` 的“提高移动消耗”语义来代表时间减速。

如果目标带有 `time_reverberation`：

- 对 `time_stasis` tag 的豁免获得 +4。
- 若仍然失败，`time_stasis` 持续时间减少 `reverberation_duration_reduction_tu`，最低不低于内容配置的 `minimum_time_stasis_duration_tu`。

该加值应通过 tag-scoped 机制实现，例如状态参数里记录 `save_bonus_by_tag = {"time_stasis": 4}`，而不是复用全局 `save_bonus`。

叠加规则：

- `time_stasis` 判定按 status tag，不只按固定 status id。内容若增加变体，必须保留 `time_stasis` tag。
- 普通技能不能对已经静滞的目标再次施加 `time_stasis`，因为目标不可被普通效果作用。
- 当前版本不支持对已经静滞的单位刷新、延长或叠加新的 `time_stasis`；测试夹具也应按“不能再次成为普通目标”的语义构造。

## 豁免规则扩展

当前普通成功/失败不足以表达时间静滞。需要扩展 `BattleSaveResolver` 的返回结果，保留旧调用者可忽略的字段：

```gdscript
{
	"success": bool,
	"roll_total": int,
	"natural_roll": int,
	"dc": int,
	"degree": StringName # critical_failure / failure / success / critical_success
}
```

`BattleSaveContentRules` 增加一个明确的伪能力：

```gdscript
const SAVE_ABILITY_HIGHER_OF_CON_OR_WILLPOWER := &"higher_of_constitution_or_willpower"
```

resolver 看到该值时取 CON 和 WILL 修正中的较高者。内容校验必须识别这个伪能力，避免 `.tres` 中写出未处理的 save ability。

新增保存 tag 常量：

```gdscript
const SAVE_TAG_TIME_STASIS := &"time_stasis"
```

编辑器侧建议继续使用 `StringName` / 字符串常量表达 save ability，并同步更新导出 hint 或白名单校验。不要临时塞一个 enum int，避免和现有六维属性枚举冲突。

critical degree 规则建议先集中在 resolver 内：

- natural 1 降一级。
- natural 20 升一级。
- 总值未达 DC 为 failure，达到 DC 为 success。
- 结果被限制在 critical_failure 到 critical_success 范围内。

时间静滞的持续时间修正由技能效果层根据 `degree` 和 `time_reverberation` 参数计算，不让 save resolver 返回持续时间。这能保持 resolver 只负责豁免判定。

`time_reverberation` 的 +4 必须走 tag-scoped 字典，例如 `save_bonus_by_tag = {"time_stasis": 4}`。`BattleSaveResolver` 读取 save bonus 时先匹配当前 save tag；没有匹配项时不能把该加值当成全局 `save_bonus`。同一 save tag 的多个加值默认可以叠加；只有该 tag 另有显式规则配置为取最高时，才改为 `take_highest`。`SkillContentRegistry` 要校验 `save_bonus_by_tag` 是字典、key 是非空字符串 / `StringName`、value 是 int。

## 目标过滤

时间静滞的“不可被作用”必须覆盖所有目标入口：

- 技能 preview 的目标合法性。
- AI 评分和候选目标收集。
- orchestrator 执行时的目标过滤。
- 地面效果对单位的收集。
- 链式、随机、重复攻击。
- 治疗和护盾。
- 强制位移、拉拽、推开、传送。
- reaction、反击、借机攻击和自动触发技能。

唯一默认例外是驱散/解除类效果：效果必须显式带 `can_target_time_stasis` 或 `dispel_time_stasis` tag，并且执行层只允许它影响 `time_stasis` / temporal tag 相关状态。这个例外也要走共享规则，不能让某个 dispel resolver 私下绕过目标过滤。

建议把核心判断放在 `BattleSkillResolutionRules.is_unit_valid_for_effect()` 或相邻共享规则中，并把签名扩展为带 effect context：

```gdscript
func is_unit_valid_for_effect(
	source_unit: BattleUnitState,
	target_unit: BattleUnitState,
	target_team_filter: StringName,
	effect_def: CombatEffectDef = null,
	context: Dictionary = {}
) -> bool
```

如果签名不携带 `effect_def`，规则层无法判断 `can_target_time_stasis` / `dispel_time_stasis` 例外，最终会在某些入口把静滞目标永远锁死，或在另一些入口把静滞隔离打穿。不能只在 `BattleSkillExecutionOrchestrator._can_skill_target_unit()` 里过滤，否则 preview、AI 或特殊 resolver 会出现行为不一致。

当目标处于 `time_stasis` 且调用点没有传入 `effect_def` 时，默认按普通效果处理并拒绝作用；只有明确传入带解除/驱散 tag 的 `effect_def` 才允许例外。这样宁可 fail closed，也不能让缺 context 的旧入口打穿隔离。

## 需要修改的文件

数据与校验：

- `scripts/player/progression/combat_skill_def.gd`
- `scripts/player/progression/combat_effect_def.gd`，仅当后续决定不用 `params.*_tags`、改为新增导出字段时才修改。
- `scripts/player/progression/skill_content_registry.gd`
- `scripts/player/progression/battle_save_content_rules.gd`
- `data/configs/skills/mage_time_stasis.tres`

运行时：

- `scripts/systems/battle/core/battle_state.gd`
- `scripts/systems/battle/core/battle_unit_state.gd`
- `scripts/systems/battle/core/battle_cell_state.gd`，仅当 `time_stasis_cell_lock` 选择落在 cell state schema；若作为 battle state runtime-only set，则不改 cell payload。
- `scripts/systems/battle/runtime/battle_runtime_module.gd`
- `scripts/systems/battle/runtime/battle_skill_turn_resolver.gd`
- `scripts/systems/battle/runtime/battle_timeline_driver.gd`
- `scripts/systems/battle/runtime/battle_skill_execution_orchestrator.gd`
- `scripts/systems/battle/runtime/battle_magic_backlash_resolver.gd`
- `scripts/systems/battle/runtime/battle_ground_effect_service.gd`
- `scripts/systems/battle/runtime/battle_movement_service.gd`
- `scripts/systems/battle/runtime/battle_charge_resolver.gd`
- `scripts/systems/battle/runtime/battle_repeat_attack_resolver.gd`
- `scripts/systems/battle/runtime/battle_target_collection_service.gd`
- `scripts/systems/battle/runtime/battle_layered_barrier_service.gd`
- `scripts/systems/battle/runtime/battle_special_skill_resolver.gd`
- `scripts/systems/battle/runtime/battle_shield_service.gd`
- `scripts/systems/battle/runtime/battle_metrics_collector.gd`
- `scripts/systems/battle/terrain/battle_terrain_effect_system.gd`
- `scripts/enemies/enemy_ai_action.gd`
- `scripts/enemies/enemy_ai_action_helper.gd`
- `scripts/enemies/actions/*.gd`，仅改会自行筛目标或构造目标候选的 action。

展示、快照与 headless：

- `scripts/systems/battle/presentation/battle_hud_adapter.gd`
- `scripts/systems/game_runtime/game_runtime_snapshot_builder.gd`
- `scripts/utils/game_text_snapshot_renderer.gd`
- `scripts/systems/game_runtime/headless/game_text_command_runner.gd`，仅当需要新增文本命令或断言表面时修改。

规则：

- `scripts/systems/battle/rules/battle_status_semantic_table.gd`
- `scripts/systems/battle/rules/battle_skill_resolution_rules.gd`
- `scripts/systems/battle/rules/battle_save_resolver.gd`
- `scripts/systems/battle/rules/battle_damage_resolver.gd`，仅在需要补充返回字段时修改，不添加 runtime callback。

## 测试计划

按**三层测试策略**组织，总计 **38** 个测试点：

| 层级 | 数量 | 范围 | 执行时间目标 |
|------|------|------|-------------|
| **L1 核心状态机单元测试** | 14 | Sidecar 状态流转、PendingCast 序列化、专注检定 DC 计算、资源返还、冷却 anchor、Line of Effect 工具函数 | < 5 秒 |
| **L2 集成冒烟测试** | 16 | 完整读条-完成-结算流程、读条中 HP Loss 打断、cancel 命令、多单位同时读条、Timeline 推进与读条交互 | < 15 秒 |
| **L3 边界回归测试** | 8 | 极端 case（0 TU 读条、被打断时 unit 已死亡、读条期间战斗结束、多段技能部分完成） | < 10 秒 |

新增或扩展 focused headless tests：

- `tests/battle_runtime/runtime/run_casting_time_regression.gd`
- `tests/battle_runtime/skills/run_time_stasis_regression.gd`
- 必要时扩展 save resolver / content registry 规则测试。

### L1 核心状态机（14 个）

1. `pending_cast` Dictionary 序列化往返（`to_dict` / `from_dict`）。
2. `BattleCastingTimeSidecar.register_cast()` 正确设置 `completion_tu` 与 `remaining_cast_progress`。
3. `advance_pending_casts()` 正确识别完成项；不提前识别未完成项。
4. 专注检定：`actual_hp_loss <= 3` **不触发**。
5. 专注检定：`actual_hp_loss = 10` 触发，DC = 12。
6. 专注检定：`actual_hp_loss = 20` 触发，DC = 15。
7. 专注检定：成功时读条继续；失败时读条中断并启动冷却。
8. 可取消读条：cancel 命令清除 `pending_cast`，全额返还 AP/MP/次数/材料，不启动冷却。
9. spell control 普通 failure：不扣可返还资源、不启动冷却、不创建 pending cast。
10. spell control critical failure：扣 50% AP、启动冷却、不创建 pending cast。
11. 冷却 anchor：读条期间跨行动阈值时 `last_turn_tu` 同步到当前 TU，冷却不停滞。
12. 冷却 anchor：完成后冷却起算 TU = `completed_at_tu`；中断后起算 TU = `interrupted_at_tu`。
13. `BattleUnitState.clone()` 深拷贝 `pending_cast`；`BattleState.clone()` 复制 `next_cast_sequence`。
14. `time_stasis_cell_lock`：锁定格拒绝普通地形变更；解除后锁标记移除，地形不回滚。

### L2 集成冒烟（16 个）

15. 瞬发技能行为不变。
16. 读条技能完整流程：preview → begin → timeline 推进 → 自动完成 → 效果结算 → 写冷却。
17. 读条中 HP Loss 触发维持检定（普通伤害、DOT、地面效果、屏障后伤害、自伤/反弹）。
18. 读条中强制位移/控制状态（睡眠、麻痹、石化、`time_stasis`）直接中断，资源不返还。
19. 绑定目标死亡/离场/进入 `time_stasis` 按 `binding_mode` 规则中断或剔除（hard_anchor / soft_anchor / ground_bind）。
20. 完成时**不检查射程**；目标移动/被推挤不导致 fizzle，读条完成后正常结算。
21. 多单位同时读条 → Timeline 正确推进所有；按 `cast_sequence` 排序结算。
22. 读条期间 cancel 命令 → 立即清除 pending_cast，全额退费。
23. `time_stasis` 应用：目标立即从 `ready_unit_ids` 移除；`action_progress` 冻结；冷却 anchor 补偿。
24. `time_stasis` 期间：DOT/地形 tick/状态 duration 跳过目标；`time_stasis` 自身 duration 正常减少。
25. `time_stasis` 解除：anchor 同步；`time_reverberation` 添加/刷新；锁定格移除。
26. `time_reverberation`：对 `time_stasis` tag 豁免 +4；持续时间减少。
27. 静滞单位不能被普通技能/范围/链式/治疗/护盾/强制位移选中；带 `dispel_time_stasis` tag 的驱散例外。
28. `time_slow`：50% 速率减慢 action_progress、冷却恢复、资源恢复、pending cast 推进。
29. `BattleStatusSemanticTable` / `BattleSkillResolutionRules` 共享语义入口一致性（preview、AI、orchestrator、ground 均使用同一判断）。
30. 战斗中保存被 battle save lock 拦截；battle running 时序列化入口 fail-loud。

### L3 边界回归（8 个）

31. 0 TU 读条（瞬发边界）→ 立即完成，不走 pending cast。
32. 读条完成时施法者已死亡 → 静默丢弃，不崩溃。
33. 读条完成时所有绑定目标已死亡 → 按中断处理，启动冷却。
34. 读条期间战斗结束 → `pending_casts` 正确清理，无泄漏。
35. 长读条（>120 TU）跨越多回合 → 冷却 anchor 始终同步，无重复追算。
36. 读条中被控制（AI controlled / 魅惑）→ 按 `blocks_pending_cast()` 中断。
37. 多目标 soft_anchor：逐个剔除失效目标，写轻量事件，不启动冷却；全部失效后才中断。
38. HUD / headless snapshot 正确暴露 pending cast / time stasis 状态。

## Project Context Units 影响

本方案会显著扩展以下 CU 的职责边界，实现 PR **必须**同步更新 `docs/design/project_context_units.md`：

**M1 直接影响的上下文单元：**
- **CU-14** `Skill Content Definitions`：新增 `casting_time_tu`、`casting_maintenance_dc`、`pending_cast_binding_mode` 字段及内容校验硬规则。
- **CU-15** `Battle Runtime Core`：`BattleUnitState` 新增 `pending_cast` 运行时字段；`BattleTimelineDriver` 新增 pending cast 推进、行动阈值跳过、冷却 anchor 同步；`BattleRuntimeModule` 新增读条专用入口与 `resolve_casting_interruption_check()` 统一入口。
- **CU-16** `Battle Resolution Rules`：`BattleStatusSemanticTable` / `BattleSkillResolutionRules` 新增时间静滞共享过滤；`BattleSaveResolver` 新增 `degree` 返回字段和 CON/WILL 取高伪能力。

**M3 / 后续扩展影响的单元：**
- **CU-18** battle 展示主链：HUD 需要显示 pending cast / time stasis 状态。
- **CU-20** 敌方模板、AI brain、行动定义种子内容：AI 候选过滤需避开静滞目标，读条技能需 AI 评分修正。
- **CU-21** Headless runtime、文本命令与快照渲染：文本快照或 headless 断言暴露 pending cast / time stasis。
- **CU-19** 自动化回归与截图辅助：作为新增 focused regression 的承载单元。

**已知技术债务（必须在 `docs/design/technical_debt.md` 中追踪）：**
- M1 读条专用入口采用临时性 spell control 前置，与瞬发路径存在规则分裂。M2 必须将 `_consume_skill_costs()` 重构为三阶段管线（`validate→commit→apply`），统一读条与瞬发的扣费/检定顺序。重构完成后删除读条专用入口及对应 `FIXME(M2)` 注释。

- CU-02 GameSession、存档、序列化、全局内容缓存，仅读取和验证 battle save lock；不扩展 battle save payload。
- CU-15 Battle Runtime Core
- CU-16 Battle Resolution Rules
- CU-14 Skill Content Definitions
- CU-18 battle 展示主链，若 HUD 需要显示 pending cast / time stasis。
- CU-20 敌方模板、AI brain、行动定义种子内容，若 AI 候选过滤要避开静滞目标。
- CU-21 Headless runtime、文本命令与快照渲染，若文本快照或 headless 断言暴露 pending cast / time stasis。
- CU-19 自动化回归与截图辅助，作为新增 focused regression 的承载单元，不是技能执行 owner。

实现 PR 需要在 `docs/design/project_context_units.md` 中同步这些读集和职责边界变化。


---

## 深度攻击性讨论（四方检视汇总）

> 以下内容由四个独立 Agent 分别从架构系统、玩法平衡、工程风险、规格一致性四个视角对本文档进行深度攻击性审视后汇总形成。用于在 Codex 确认前暴露设计缺陷、边界留白和隐性债务。

---

### 一、架构系统视角：在腐烂地基上糊泥

**核心指控：一个基础机制（延迟结算 + 状态隔离）需要动 26+ 个文件，这不是设计规格，是《当前架构无法支撑该特性，因此需要全系统开胸手术》的病理报告。**

| 攻击点 | 具体质疑 |
|--------|----------|
| **修改文件数量灾难** | 26+ 个文件涵盖 runtime、rules、presentation、AI、headless 等几乎所有战斗子系统。为什么一个"施法读条+时间静滞"特性需要动这么多文件？这是否说明当前架构完全无法支持延迟结算和状态隔离，必须靠打补丁实现？ |
| **Pending Cast 状态模型的脆弱性** | `pending_cast` 是一个包含 12 个字段的 Dictionary，放在 `BattleUnitState` 中但不进入 `TO_DICT_FIELDS`。无 schema、无静态类型检查、无版本控制。如果某个实现者漏写了 `spell_control_context` 或 `cast_transaction` 的字段，bug 会在运行时静默暴露。 |
| **Timeline 正在吞噬所有调度职责** | 文档说"timeline 只负责到点触发"，但实际上 timeline 需要处理 pending cast 推进、行动阈值跳过、冷却 anchor 同步、time_stasis 的 `last_turn_tu` 补偿等大量逻辑。timeline 正在变成"什么都管"的上帝对象，而文档还在假装 orchestrator 是执行中心。 |
| **读条维持检定的耦合灾难** | 文档要求"所有会造成 HP 损失的 runtime 路径都必须调用同一个 helper"，列举 8 个来源。在分布式 damage source 架构下，如何保证"每次 actual HP loss 只调用一次"？这是典型的"靠人工约定保证正确性"，任何新 damage source 忘了加调用就是漏检。 |
| **状态过渡 API 的空洞性** | 文档要求提供 `apply_runtime_status()` 统一入口，但当前系统已存在直接 `set_status_effect()` / `erase_status_effect()` 的调用点。"当前直接调用的必须改走该入口"——26+ 个文件的改动列表中，有多少旧调用点需要 refactor？引入成本被严重低估。 |
| **Tag 系统的倒退** | 所有新语义塞进 `params.*_tags` 字符串数组，而不是扩展正式 schema。`params` 是无类型的，运行时比较需要先"归一为 StringName"。核心机制退化为字符串匹配，且 GDScript 没有任何机制阻止开发者直接写 `if "time_stasis" in status_entry.params.status_tags`。 |

**结论：** 这份文档的首要问题不是实现细节，而是**没有延迟结算的抽象层、没有状态变更的统一拦截点、没有类型安全的新语义承载机制**。如果答案是"因为以前没留扩展点"，那么"补扩展点"应该先于"打补丁"发生。

---

### 二、玩法平衡视角：一个让玩家弃用的系统

**核心指控：读条系统体验灾难 + 时间静滞强度失控 + 反制设计错位。**

| 攻击点 | 具体质疑 |
|--------|----------|
| **时间静滞=删除单位** | 9 环单体技能，目标不可行动、不可被作用、不推进计时器、免疫几乎所有效果。在 turn-based 战术游戏中，完全移除一个单位（比死亡还彻底）是否是健康的设计？被静滞的玩家单位只能"看着屏幕发呆"。 |
| **成功豁免的惩罚过于轻微** | 目标豁免成功只获得 `time_slow`（50% 行动/冷却/资源恢复减速），而且不是时间隔离。对于 9 环级技能，成功豁免的回报是否太低？规避了跟没规避差不多。 |
| **读条系统的玩家体验灾难** | 施法者开始读条后"不能移动、不能取消、不能施放其他技能"，且 AP 已被清空。在 turn-based 游戏中，提前支付资源然后等待几个回合才能看到效果，这种"预付费+延迟"的体验是否会让玩家主动避开所有读条技能？ |
| **读条维持检定的体验问题** | DC 12（固定！）的 CON/WILL 取高检定，被任何伤害触发。被弓箭手射了一箭（3-5 点伤害），就有约 30-40% 概率被打断读条，资源全损。这是否会让近战/远程打断成为读条施法者的绝对克星？ |
| **time_reverberation 反制错位** | `time_reverberation` 只给 +4 豁免加值和持续时间减少。如果第一次静滞成功了，目标在静滞期间什么都做不了，解除后获得的"反制"是否真的有价值？一个已经吃过一次亏的目标是否真的有机会第二次面对同一个施法者的 9 环技能？ |
| **多目标绑定剔除继续读条的不合理性** | 多目标绑定技能中某个目标死亡/离场时"只从 target_unit_ids 中剔除该目标并继续读条"。从玩法直觉上，如果一个技能绑定了3个目标，其中1个已经死了，施法者还在对着一个死人"读条"，玩家如何理解？ |
| **AI 方案一句话** | 文档提到 AI 需要避开静滞目标，但没有给出任何 AI 评分调整方案。AI 是否会不断尝试对静滞目标施放普通技能然后被过滤掉？AI 施法者是否会在敌人脸上开始读条然后被打断？ |
| **MVP 不做沉默状态** | 在一个有读条系统的游戏中，没有沉默状态意味着打断读条的唯一方式是造成伤害（DC 12 维持检定）或物理控制。这反而让"打断"变得过于依赖伤害输出，而不是战术性的反制技能，反制生态缺失。 |

**结论：** 当前方案若按此实现，读条系统将被玩家主动弃用，时间静滞将成为 9 环必带技能导致战斗平衡崩坏，维持检定将成为玩家最痛恨的 RNG 机制。建议回炉重造。

---

### 三、工程风险视角：系统性乐观的落地计划

**核心指控：测试可行性泡沫、契约不可实现、核心记账契约被篡改、实施路径缺失。**

| 攻击点 | 具体质疑 |
|--------|----------|
| **38 个测试覆盖点的可行性泡沫** | 在 headless Godot 环境中，如何测试"同一 timeline step 内 DOT/地形/屏障等伤害先于 pending cast 完成"这种精确时序？如何测试"强制位移中断读条"需要 mock 多少系统？38 个点中至少 15 个涉及跨系统时序或 UI 观测，fixture 复杂度被严重低估。 |
| **"每次 actual HP loss 只检定一次"契约不可能实现** | 现有代码中 HP 写入点分散在 `BattleDamageResolver`、`_apply_damage_to_target()`、`BattleTerrainEffectSystem`、`BattleRepeatAttackResolver`、`BattleChargeResolver`、`BattleLayeredBarrierService`、`BattleSkillTurnResolver` 等 7+ 个系统中。Chain/Repeat 天然是多段独立结算，每一段都可能触发 HP 损失。如果在 `_apply_damage_to_target()` 里插维持检定，repeat 的每一段都会触发——正好是文档说的"重复调用"。除非重写所有 damage source 的统一出口。 |
| **cooldown anchor 同步的时序地狱** | 现有 `last_turn_tu` 只在回合开始/结束时更新，`consume_turn_cooldown_delta()` 假设 `elapsed_tu` 反映"从上次行动到现在的全部时间"。文档要求在 timeline step、静滞应用、静滞解除、读条中断等多个新位置插入同步，但没有给出冷却计算的完整状态机分析。会导致"双算"、"漏算"或"非粒度对齐报错"。 |
| **"完成时不重查射程视线"的安全漏洞** | 开始读条时目标在 5 格内，读条期间目标被传送到 30 格外、推进到墙后，完成后技能仍然自动命中。这是"超视距追踪导弹"。文档把"不重查"作为实现简化手段，而不是经过游戏性论证的设计决策。 |
| **time_stasis_cell_lock 的实现留白** | 文档列出两种方案（改 `BattleCellState` schema vs `BattleState` runtime-only set）但不做决定。方案 A 影响所有读取 cell 的代码；方案 B 需要在 5+ 个查询点手动加判断，遗漏即 bug。这种"推给实现者"是不负责任的。 |
| **BattleSaveResolver degree 扩展兼容性** | 新增 `degree: StringName` 字段在动态语言中不是"旧调用方可忽略"的。任何对返回字典做 `keys()` 迭代、`==` 比较或序列化的调用方都会被破坏。文档没有给出调用方审查清单，也没有定义默认值和演化策略。 |
| **"开始施法时扣费但不启动冷却"的事务复杂度** | 现有代码中成本和冷却是原子操作，文档要求拆成异步两阶段提交。在 Godot 没有真正事务系统的环境中，崩溃时 pending cast 丢失但成本已支付、冷却未启动，玩家白白损失资源。`cast_transaction` 的回滚语义也只覆盖最简单的数值成本，对 charges、材料等没有方案。 |
| **26+ 文件缺乏实施顺序** | 没有阻塞链分析、没有编译依赖、没有"最小可运行里程碑"。`BattleUnitState` 必须先有字段才能改 `BattleTimelineDriver`，`BattleSkillTurnResolver` 必须先支持"扣费不启动冷却"才能改 `BattleRuntimeModule`。文档没有指出这个依赖链。 |

**结论：** 在编码前，文档必须补充 `last_turn_tu` 状态机时序图、统一 HP loss 通知机制设计、`cell_lock` 明确架构决策、调用方审查报告、分阶段实施计划。

---

### 四、规格一致性视角：12个隐形炸弹

**核心指控：看起来正确，但每天都在玩家眼前上演系统性 bug。**

| 攻击点 | 具体质疑 |
|--------|----------|
| **"完成时不重查射程视线"与"目标被推开不打断"的组合漏洞** | 目标被友方传送到墙后（完全在视线外），读条完成后技能仍然命中。这和"目标移动、被推开、躲到墙后或离开原射程不打断"组合，创造了"开始锁定=必中"的追踪导弹机制。 |
| **多目标绑定剔除继续读条 vs 单目标绑定目标失效中断的不一致** | 单目标死亡立即中断；多目标中某个目标死亡只剔除并继续。为什么多目标就更"宽容"？如果设计师希望"火球追踪目标，但如果目标跑了就在原地爆炸"，文档的答案是"做成 ground target"——这不是设计师想要的"智能追踪但有限制"。 |
| **time_stasis 对地形效果的"跳过"定义模糊** | 地面效果在静滞单位脚下生成，静滞单位不受伤，但解除静滞后火墙还在，静滞单位是否会立即受到火墙伤害？文档说"解除后也不回溯补结算"，但解除后的正常 tick 呢？ |
| **spell control 失败不退费的资源欺诈** | 玩家支付了大量 AP/MP，spell control 判定失败，技能完全没有生效，但资源不退还、冷却还启动了。这是否会让玩家感觉被系统"诈骗"？为什么不是"spell control 失败 = 施法从未真正开始"？ |
| **time_stasis 自身 duration 减少 vs 其他状态冻结的不一致** | time_stasis 是"时间隔离"状态，但它自己的 duration 计时器却继续流逝。如果 time_stasis 是外部施加的，施加者应该负责管理持续时间，而不是让被隔离的时间自己流逝。 |
| **"战斗中保存不支持"的架构逃避** | 在一个现代游戏中，战斗中途保存是基本功能。如果架构设计从开始就排除了这个可能性，整个 pending cast 的设计（运行时 Dictionary 不进入 save payload）是否是架构偷懒？未来如果要支持战斗保存，整个读条系统需要重写？ |
| **Content 校验扫描"禁止读条形态"的过度野心** | 文档要求基于现有字段、effect type、`forced_move_mode` 和 cast variant params"检出禁止读条形态"，且"不依赖额外 tag"。在动态类型系统中，如何可靠判断"完成时需要重新求路径、射程、视线或施法者落点的 effect"？是否需要为每种 effect type 写专门静态分析器？ |
| **"不改变运行时关系"的自我矛盾** | 文档说"本次是方案文档修正，不改变运行时关系"，但前面列出了 26+ 个需要修改的文件和 38 个测试点。一份需要改 26+ 个文件、38 个测试点的方案，怎么可能"不改变运行时关系"？ |

**问题统计：** 核心玩法逻辑矛盾 2 个（致命）、资源/经济系统欺诈 1 个（严重）、状态语义自我否定 1 个（严重）、架构逃避 1 个（严重）、边界场景遗漏 2 个（中等）、工程可行性幻觉 1 个（中等）、文档诚实性 1 个（低级但恶劣）。

---

### 五、跨视角共识

四个 Agent 在以下问题上形成了一致攻击：

1. **这份文档是工程现状的囚徒，而非玩法需求的表达。** 它在每一个需要突破现有架构的地方都选择了妥协（string tag、Dictionary 快照、不重查射程、无沉默状态）。
2. **"完成时不重查射程视线"是一个为了规避实现复杂度而牺牲 gameplay 合理性的决策。** 三个不同视角都攻击了这一点：规格视角称其为"追踪导弹"，玩法视角称其削弱位移策略价值，工程视角称其没有游戏性论证和回滚策略。
3. **读条维持检定的"全局钩子"设计是不可靠的。** 架构视角称其为"靠人工约定保证正确性"，工程视角称其"在分布式 damage source 架构下无法自动满足"，玩法视角称其为"玩家最痛恨的 RNG 机制"。
4. **26+ 文件改动缺乏实施顺序和里程碑。** 工程视角指出没有编译依赖分析，架构视角指出 resolver 模式没有真正隔离变更，规格视角指出"不改变运行时关系"是谎言。
5. **时间静滞的设计缺乏对玩家体验的尊重。** 玩法视角称其为"删除单位"，规格视角称其为"被控=挂机，极度挫败"。
6. **测试可行性被严重低估。** 工程视角称 38 个测试点中的时序测试需要 mock 半个战场，架构视角称"没有类型安全的新语义承载机制"会导致运行时静默失败。

---

### 六、综合评分

| 维度 | 评分 | 评语 |
|------|------|------|
| 架构合理性 | D | 26+ 文件改动说明架构无法支撑该特性，Tag 系统倒退为字符串匹配 |
| 玩法创新性 | C- | 读条+时间静滞概念经典，但体验设计灾难（不能取消、DC12打断、被控=挂机） |
| 数值平衡性 | D+ | 9环删除单位过于廉价，成功豁免惩罚太轻，体型/反制设计缺失 |
| 工程可落地性 | D+ | 38个测试点可行性泡沫、契约不可实现、核心记账契约被篡改、无实施顺序 |
| 规格完整性 | C- | 边界留白（cell_lock实现、地形解除后伤害、多目标缩水结算）、无版本控制 |
| 文档诚实性 | D | "不改变运行时关系"与26+文件改动直接矛盾 |

**总评：一份设计愿景清晰、但工程实现充满乐观假设、玩法体验充满挫败感、规格边界充满留白的文档。它可能在技术层面被"实现"，但极有可能在玩家测试阶段被要求回炉重造。在投入 26+ 个文件的修改之前，建议先回答：为什么一个基础机制（延迟结算+状态隔离）需要动这么多地方？如果答案是"因为以前没留扩展点"，那么补扩展点应该先于打补丁发生。**


---

## 附录 A：四方 Agent 共识修订（解决尾部攻击点）

以下修订由架构系统、玩法平衡、工程风险、规格一致性四个视角经四轮深度讨论后达成一致。每个修订对应原文档尾部攻击点的具体解决路径。

### A.1 架构系统视角（6 个攻击点）

| # | 原攻击点 | 解决方案 | 责任阶段 |
|---|---------|---------|---------|
| 1 | 26+ 文件修改灾难 | 引入 `BattleCastingTimeSidecar`，读条逻辑收敛到 1 个自包含模块，其他模块只保留 1-2 行委托调用 | M1 |
| 2 | Pending Cast Dictionary 无 schema | M1 封装 Dictionary + 显式 schema 契约（单点访问，禁止裸读 `pending_cast["xxx"]`）；M2 升级为 `BattlePendingCastState` 强类型类 | M1/M2 |
| 3 | Timeline 变成上帝对象 | M1 直接插入 pending cast 推进（现有 6 种职责，新增 1 种可控）；M2 评估 Phase Hook 重构 | M1/M2 |
| 4 | 维持检定 8 来源靠人工约定 | `BattleRuntimeModule` 提供统一 `resolve_casting_interruption_check()` 入口；damage source owner 层通过 `_notify_hp_loss_if_casting()` 包装调用；M2 评估迁移到 `BattleCastingCheckContext` | M1/M2 |
| 5 | 状态过渡 API 空洞 | M1 引入最小 `StateTransitionGate`（Timeline 推进 + 状态转换校验）；M2 扩展为通用状态机 | M1/M2 |
| 6 | Tag 字符串匹配倒退 | `BattleTagRegistry` 集中管理 `StringName` 常量 + 运行时 params 校验 | M1 |

### A.2 玩法平衡视角（8 个攻击点）

| # | 原攻击点 | 解决方案 | 责任阶段 |
|---|---------|---------|---------|
| 1 | 时间静滞 = 删除单位 | 持续时间由内容配置，推荐 **3 ~ 4 TU**（约 1 ~ 2 回合）；"时间气泡" + 同系穿透推迟 M2 | M1 内容配置 |
| 2 | 成功豁免惩罚太轻 | MVP 保留 `time_slow`（50% 减速），不做 `time_disorder` 复合惩罚 | M1 |
| 3 | 读条 = 预付费延迟体验灾难 | M1 **必须支持可取消读条**（全额退费，不启动冷却）；分段付费 / 缓慢移动推迟 M2 | M1 |
| 4 | DC12 维持检定太容易被触发 | 简化三档阈值：`actual_hp_loss <= 3` **不触发**；`4~15` DC12；`>15` DC15 | M1 |
| 5 | time_reverberation 反制错位 | MVP 只做 **被动 +4 豁免** + 持续时间减少；主动层数充能机制推迟 M2 | M1 |
| 6 | 多目标绑定剔除继续读条荒诞 | 引入显式 `binding_mode`（hard_anchor / soft_anchor / ground_bind） | M1 |
| 7 | AI 方案一句话 | AI 评分修正公式（survival_factor、time_reverberation 层数影响）纳入 M3/M4 | M3/M4 |
| 8 | MVP 无沉默状态 | M1 引入"近战威胁区干扰"（邻接格读条 DC+3）；2 环"魔力震荡"技能作为后续内容 | M1 机制 / 后续内容 |

### A.3 工程风险视角（8 个攻击点）

| # | 原攻击点 | 解决方案 | 责任阶段 |
|---|---------|---------|---------|
| 1 | 38 个测试点可行性泡沫 | 三层测试：L1 核心状态机 14 个 + L2 集成冒烟 16 个 + L3 边界回归 8 个 = 38 个 | M1~M4 |
| 2 | "每次 HP loss 只检定一次"不可能实现 | 不集中到 `_apply_damage_to_target` 内部（避免 repeat/chain 内层重复），改在 7 个 damage source owner 外层统一调用 `_notify_hp_loss_if_casting()` | M1 |
| 3 | cooldown anchor 时序地狱 | 统一入口 `sync_cooldown_anchor_to_current_tu()` + 状态机显式化；静滞单位每 step 前推 `last_turn_tu += tu_delta` | M1 |
| 4 | "不重查射程" = 追踪导弹 | 因本战斗系统**不设计遮挡机制**，读条技能完成时**不检查射程**。强力技能带追踪是设计意图；死亡/离场/time_stasis 仍按 `binding_mode` 硬中断 | M1 文档 |
| 5 | cell_lock 实现留白 | **明确采用方案 B**：`BattleState` runtime-only Dictionary，不改 `BattleCellState` schema | M1 |
| 6 | SaveResolver degree 兼容性 | 风险被高估：现有调用方均用 `.get()` 读取，新增 `degree` 字段安全 | M1 |
| 7 | "扣费不启动冷却"事务复杂度 | 读条路径采用**局部前置 spell control**：通过后扣费，无需 refund 逻辑；瞬发路径 M1 不动 | M1 |
| 8 | 26+ 文件缺乏实施顺序 | 四阶段路线图：M0 前置 → M1 核心读条 → M2 生产化 → M3 整合 → M4 验收 | 本文档 |

### A.4 规格一致性视角（8 个攻击点）

| # | 原攻击点 | 解决方案 | 责任阶段 |
|---|---------|---------|---------|
| 1 | 追踪导弹 + 目标被推开不打断 | 因本战斗系统不设计遮挡机制，完成时**不检查射程**。目标移动/被推挤不中断读条，也不导致 fizzle；死亡/离场/time_stasis 仍硬中断 | M1 文档 |
| 2 | 单/多目标绑定行为不一致 | `binding_mode` 显式配置，加载时硬规则校验 | M1 |
| 3 | 地形跳过定义模糊 | 明确三阶段：静滞期间**完全跳过** → 解除瞬间**不触发进入效果** → 解除后**正常结算** | M1 |
| 4 | spell control 资源欺诈 | 读条路径 spell control **前置**，普通失败不扣费、不启动冷却、**不结束回合**；critical failure 扣 50% AP。失败后允许应急行动（移动/等待/非攻击性道具），禁止本回合再次读条 | M1 |
| 5 | time_stasis 自身 duration 流逝悖论 | 语义修正为**"外部时间异常场"**：场由施加者维护、duration 正常流逝；目标个人时间线被冻结 | M1 文档 |
| 6 | "不保存" = 架构逃避 | 明确为 **MVP 范围裁剪**（非架构缺陷）；未来通过 battle-level schema 扩展；当前 fail-loud 行为保持不变 | M1 文档 |
| 7 | 内容校验过度野心 | **硬规则**：显式标记 `incompatible_with_casting_time` 拒绝加载；**软规则**：启发式扫描推迟 M2 CI | M1/M2 |
| 8 | "不改变运行时关系"自我矛盾 | 删除该表述，诚实声明影响 CU-14/15/16/18/20/21，实现 PR 同步更新 `project_context_units.md` | M1 文档 |

### A.5 第二轮讨论共识（硬核施法失败下的回合浪费问题）

第一轮共识确立"读条路径 spell control 前置"后，发现新的玩家体验漏洞：若常规失败率（15~25%）下 spell control failure 直接结束回合，玩家会频繁遭遇"什么都没做就跳过回合"的挫败感。四方 Agent 经第二轮深度讨论后达成以下补充共识：

**前提约束（不可推翻）：**
- 施法失败是常规风险（15~25%），不是边缘惩罚
- 硬核难度是核心设计目标
- 不能允许玩家通过重复尝试来刷概率

**补充方案：**

| 议题 | 第一轮方案 | 第二轮修正 |
|------|-----------|-----------|
| **failure 后回合状态** | 未明确 | **不结束回合**。`has_taken_action_this_turn` 和 `current_ap` 保持原状 |
| **防刷机制** | 未明确 | `turn_casting_exhausted = true`（本回合禁止再次读条）+ `attempted_spells_this_turn.add(skill_id)`（同法术锁定） |
| **failure 后可执行行动** | 未明确 | **白名单**：`TYPE_MOVE` / `TYPE_WAIT` / `TYPE_ITEM`（非攻击性道具）。`TYPE_SKILL` 和 `TYPE_CHANGE_EQUIPMENT` 推迟 M2 |
| **failure 反馈** | 未明确 | 自动执行"安全驱散"（不消耗资源、无副作用、不可打断），战斗日志高亮输出。M1 **不做模态选择面板** |
| **疲劳累积** | 规格 Agent 提出 | **M1 不实现**。防刷完全由回合级锁定承担；M2 根据遥测数据再评估 |
| **UI 交互** | 规格 Agent 要求强制面板 | **M1 不做**。采用 UI 按钮置灰禁用 + 指令入口层拦截并返回 `ERR_ACTION_UNAVAILABLE` |
| **存档/回放** | 未明确 | `turn_casting_exhausted` 与 `attempted_spells_this_turn` 为回合级临时状态，不进入 `TO_DICT_FIELDS`。战斗回放允许 failure 后行动分支与原始战斗不同 |

**规格一致性补充条件（P0-P2）：**

| 优先级 | 条件 | 状态 |
|:---:|---|:---|
| **P0** | 非法行动尝试的拒绝规格必须明确：UI 按钮置灰禁用，或允许点击但入口层拦截并写日志 | 已写入正文 |
| **P0** | "非攻击性道具"必须引用现有 `ItemDef` / `ItemTag` 定义；若无则 M1 显式维护白名单枚举 | 已写入正文 |
| **P1** | "安全驱散"子规格闭环：不消耗资源、不产生额外日志条目（独立于 failure 日志）、不可被反制/打断 | 已写入正文 |
| **P1** | `attempted_spells_this_turn` 回放兼容性：回合开始时重建，不进入快照，允许回放分支差异 | 已写入正文 |
| **P2** | M2 扩展点预留：在 `BattleUnitState` 或 `ActionValidator` 中预留 `## M2_EXT:` 注释标记 | 实现时处理 |

---

## 附录 B：实施路线图

| 阶段 | 目标 | 主要工作 | 新增文件 | 修改文件 | 测试点 |
|------|------|---------|---------|---------|--------|
| **M0** | 前置基础设施 | `CombatSkillDef` 字段、`BattleTagRegistry`、SaveResolver 扩展、SemanticTable 语义、内容校验硬规则 | `battle_tag_registry.gd` | `combat_skill_def.gd`、`battle_save_resolver.gd`、`battle_save_content_rules.gd`、`skill_content_registry.gd`、`battle_status_semantic_table.gd` | 0（编译通过即可） |
| **M1** | 核心读条原型 | `BattleCastingTimeSidecar`、TimelineDriver 插入、`issue_command` 读条分支、可取消、维持检定（简化 DC）、射程检查、`binding_mode`、cell_lock | `battle_casting_time_sidecar.gd` | `battle_runtime_module.gd`、`battle_timeline_driver.gd`、`battle_skill_execution_orchestrator.gd`、`battle_skill_turn_resolver.gd`、`battle_unit_state.gd`、`battle_state.gd`、`battle_skill_resolution_rules.gd` 等 | 20 |
| **M2** | 生产化 + 深度机制 | `BattlePendingCastState` 强类型、专注阈值分层 DC、分段付费、AI 评分修正、time_stasis 完整状态规则、软规则 CI、`SkillCostTransaction` 重构 | `battle_pending_cast_state.gd` | `battle_casting_time_sidecar.gd`、`battle_skill_turn_resolver.gd`、`battle_skill_execution_orchestrator.gd` | 12 |
| **M3** | 系统整合 | AI 避静滞目标、HUD 展示、存档版本迁移、time_reverberation 被动生效、敌方 AI 读条评分 | — | `enemy_ai_action*.gd`、`battle_hud_adapter.gd`、`game_runtime_snapshot_builder.gd` 等 | 6 |
| **M4** | 验收 | 全量回归、性能基准、截图验证、文档定稿、技术债务清偿确认 | — | 测试补充 | — |

### 阻塞链分析

```
M0（无阻塞）
  ↓
M1 阻塞于：BattleUnitState 字段 → BattleTimelineDriver 推进逻辑
            BattleSkillExecutionOrchestrator no-cost 入口 → TimelineDriver 触发逻辑
            BattleSkillTurnResolver "扣费不启动冷却" → RuntimeModule 读条分支
  ↓
M2 阻塞于：M1 读条机制验证通过
            SkillCostTransaction 抽象设计确认
  ↓
M3 阻塞于：M2 生产化完成 + time_stasis 规则稳定
  ↓
M4 阻塞于：M3 全链路集成通过
```

---

## 附录 C：合并条件清单（C1-C7）

进入编码前，以下七个条件必须在设计文档和项目追踪中明确落实：

| 编号 | 条件 | 来源 |
|------|------|------|
| **C1** | 读条入口 `_handle_casting_time_skill_command` 必须物理隔离、自包含，不与瞬发路径共享中间状态或副作用缓存 | 工程 |
| **C2** | `docs/design/technical_debt.md` + `project_context_units.md` 中创建独立章节，M2 三阶段管线标注为 **blocking issue**，附触发重构标准 | 工程 + 玩法 |
| **C3** | 游戏内 UI/Tooltip 明确区分：读条技能提示"检定失败不消耗资源"，瞬发技能提示"检定失败资源不返还" | 工程 + 玩法 |
| **C4** | `_consume_skill_costs()` 原子性在 M1 **不得**被削弱或打补丁；black_contract_push、misfortune 等特殊路径保持 100% 现有行为 | 工程 |
| **C5** | M1 读条专用入口必须有 `# FIXME(M2): 临时性重复，待三阶段管线重构后统一` 注释 | 架构 |
| **C6** | 读条技能取消命令必须全额返还已扣资源（AP/MP/次数/材料），不启动冷却 | 玩法 |
| **C7** | 测试计划中纳入读条/瞬发使用率对比监控；若瞬发使用率相对下降 > 15%，优先数值微调补偿，必要时触发 M2 提前评估 | 玩法 |
| **C8** | spell control failure 时**不结束回合**，`turn_casting_exhausted` 与 `attempted_spells_this_turn` 在 `BattleUnitState` 中管理，回合开始时重置，不进入 `TO_DICT_FIELDS` | 工程 + 架构 |
| **C9** | spell control failure 后仅允许 `TYPE_MOVE` / `TYPE_WAIT` / `TYPE_ITEM`（非攻击性道具），`TYPE_SKILL` / `TYPE_CHANGE_EQUIPMENT` 推迟 M2 | 工程 + 玩法 |
| **C10** | M1 **不做** spell control failure 的模态选择面板；采用自动结算（安全驱散）+ 战斗日志高亮 + UI 按钮置灰禁用 | 工程 + 规格 |
| **C11** | M1 **不实现**疲劳累积；防刷完全由回合级锁定（`turn_casting_exhausted` + `attempted_spells_this_turn`）承担 | 玩法 + 规格 |

---

*本修订版由架构系统、玩法平衡、工程风险、规格一致性四方 Agent 经**两轮**深度讨论后达成一致。第一轮解决原文档尾部的 32 个攻击点；第二轮在"保留常规施法失败率（15~25%）作为硬核核心"的前提下，解决了"spell control failure 导致空过回合"的新发现漏洞。所有修订均以附录 A~C 为最终依据，原尾部"深度攻击性讨论"中的攻击点已全部关闭或转化为带责任阶段的追踪项。*
