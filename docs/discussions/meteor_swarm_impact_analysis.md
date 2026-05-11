# 陨星雨（Meteor Swarm）特殊结算修订规格

> 当前有效方案：**v4.10.3 实现级分步方案（2026-05-11）**。  
> 旧版 v4.1 / v4.3 / v4.5 / v4.7 / v4.9 方案正文已移除，避免实现时误读过时口径。  
> 文档尾部保留历轮四方审查意见，供 Kimi 继续追踪批评脉络与方案演进。

---

## 当前最新方案：v4.10.3 实现级分步方案（2026-05-11）

> 本节是当前唯一有效实施方案。v4.1 / v4.3 / v4.5 / v4.7 / v4.9 / v4.10.2 的方案正文已移除或被本节吸收，原因见后续历史审查记录。历史审查意见保留，用于给 Kimi 继续复审问题演进。

### 一、总判定

v4.10.3 不再把陨星雨视为“在现有 ground skill 上追加若干例外”的技能。它是一个由 `Special Profile` 驱动的战斗结算子系统切片，核心目标是：

1. 禁咒体验必须可见：多段伤害、中心直击、震眩、尘土、碎石、陨坑、AI 使用意图和战报摘要都要进入第一阶段验收。
2. 架构边界必须可执行：普通技能路径不能私下读取旧 `effect_defs` 当第二事实源，不能让 HUD / AI / execute 各自重算命中率，也不能靠多处 wrapper 自觉传 `flat_penalty`。
3. 失败必须 fail-closed 且玩家可理解：配置不合法时技能不可用，HUD 灰显并给稳定文案，AI 跳过候选，execute 不扣 cost，不回退 legacy，不 no-op，不崩溃。
4. 代码边界必须可审计：新增服务、typed context、legacy adapter、manifest validator、call-site audit runner 都必须有明确 owner 和默认回归。

本方案采用 `Single Attack Check Policy + Typed Modifier Bundle + Player-Safe Contract Gate + Playability First`。

### 二、Special Profile 与运行时边界

`mage_meteor_swarm` 必须使用 `CombatSkillDef.special_resolution_profile_id = &"meteor_swarm"`。只有该技能进入特殊结算；普通 ground skill、龙息、火球、冲锋、连击等技能继续走既有路径。

特殊 Profile 的运行时边界如下：

- `SkillDef` 仍是技能身份、展示、成本、范围、等级、掌握曲线的入口。
- `effect_defs` 对 special profile runtime 是禁止执行事实源。preview / execute / HUD / report / AI 不得读取 executable `effect_defs` 推导伤害、状态、地形、命中修正、AI 估值或战报。
- `effect_defs` 只允许在 content validation、profile alignment runner、离线迁移检查中只读使用，用于确认旧资源已经不再承载执行事实。
- 展示说明从 `SkillDef.description`、`level_description_template`、manifest presentation metadata 或 typed payload 读取，不从 legacy executable `effect_defs` 推导。
- 如果 manifest 保留 `allowed_legacy_effect_types_when_special`，它只允许非执行 metadata，并必须声明 `runtime_read_policy = &"forbidden"`。

新增或调整文件边界：

- `scripts/systems/battle/runtime/battle_skill_execution_orchestrator.gd`：只负责识别 special profile、调用 gate、调用 Meteor resolver，不承载伤害数学。
- `scripts/systems/battle/runtime/battle_meteor_swarm_resolver.gd`：新增，只负责 Meteor typed plan / preview facts / impact resolution，不负责 commit、report、defeat、loot、mastery、rating tail。
- `scripts/systems/battle/runtime/battle_special_profile_gate.gd`：新增，负责 special profile preflight / preview / execute gate。
- `scripts/systems/battle/core/battle_special_profile_gate_result.gd`：新增 typed gate result。
- `scripts/systems/battle/core/special_profiles/battle_special_profile_manifest.gd`：新增 manifest resource，属于 battle contract，不属于 progression content。
- `scripts/systems/battle/core/meteor_swarm/meteor_swarm_profile.gd`：新增 Meteor 执行 profile，承载伤害、状态、地形与 AI 估值所需配置。
- `scripts/systems/battle/runtime/battle_special_profile_registry.gd`：新增 manifest registry，只索引 / 校验，不实例化 resolver。
- `scripts/player/progression/combat_skill_def.gd`：新增 `special_resolution_profile_id` 字段；它属于战斗执行语义，不放在顶层 `SkillDef`。
- `data/configs/skills/mage_meteor_swarm.tres`：只保留展示、成本、范围、profile id 与非执行 metadata；执行数据转入 manifest / typed profile。
- `data/configs/skill_special_profiles/meteor_swarm_special_profile_manifest.tres`：新增独立 manifest 目录，禁止放进 `data/configs/skills`，避免被 `SkillContentRegistry` 当成非 `SkillDef` 技能资源扫描。

### 三、执行顺序

Meteor execute 必须按以下顺序，任何实现不得跳步或把 cost 扣减提前：

1. `BattleRuntimeModule.issue_command()` 保持 preview-first 总入口。
2. `BattleSpecialProfileGate.preflight_skill(skill_def, runtime_state)` 在进入战斗或刷新技能列表时运行，缓存 `BattleSpecialProfileGateResult` 给 HUD / AI。
3. `preview_command()` 读取缓存 gate result；若 `allowed=false`，`BattlePreview.allowed=false` 并附 `player_message`。
4. `execute` 再次调用 gate；失败时在扣 AP / MP / cooldown / mastery 前阻断。
5. 成本、前摇、spell control、fate / backlash 漂移按现有 CU-15 / CU-16 入口运行。
6. 根据最终落点构建 `MeteorSwarmTargetPlan`，禁止复用 drift 前的 preview target list。
7. `BattleMeteorSwarmResolver.resolve(plan)` 生成 typed outcome。
8. outcome 通过 `BattleSpecialProfileCommitAdapter.commit_meteor_swarm_result(result, batch)` 转成 common typed outcome，再委托 `BattleSkillOutcomeCommitter` 进入 common battle outcome tail：changed coords、changed units、log、report、mastery、rating、defeat、loot。
9. runtime 批次落地，HUD / report 只消费 typed outcome 生成的结构化 fact，不重新解析旧 effect_defs。

### 四、Player-Safe Gate Result

新增 `BattleSpecialProfileGateResult`，建议路径：

`res://scripts/systems/battle/core/battle_special_profile_gate_result.gd`

字段必须精确：

```gdscript
class_name BattleSpecialProfileGateResult
extends RefCounted

var allowed: bool = false
var profile_id: StringName = &""
var skill_id: StringName = &""
var block_code: StringName = &""
var player_message: String = ""
var developer_detail: String = ""
var severity: StringName = &"error" # info | warning | error | hard_block
var checked_at_unix_time: int = 0
var manifest_schema_version: int = 0
```

Gate 行为：

- manifest 合法：`allowed=true`，`block_code=&""`。
- manifest 错误：`allowed=false`，`block_code=&"meteor_contract_invalid"`，玩家文案固定为“陨星雨暂不可用，内容配置未通过校验。”，开发细节只进 validation log / debug surface。
- 未经用户明确确认的 fallback / legacy bridge 字段出现：`allowed=false`，`block_code=&"meteor_unapproved_fallback"`。
- required regression 缺失只在 content / dev validation runner 中失败，不作为玩家 runtime gate；exported / player runtime 不读取 `tests/`。
- HUD 技能槽提前灰显，不等点击才发现。
- AI candidate build 阶段直接跳过，并在 action trace 写 `blocked_reason`。
- execute 不回退 legacy、不降级 no-op、不崩溃、不阻止存档加载。

### 五、Single Attack Check Policy

v4.10.3 必须新增统一攻击检定入口，建议路径：

- `scripts/systems/battle/rules/battle_attack_check_policy_service.gd`
- `scripts/systems/battle/core/battle_attack_check_policy_context.gd`
- `scripts/systems/battle/core/battle_repeat_attack_stage_spec.gd`

生产代码禁止直接调用以下 resolver 方法族，例外只允许 `BattleHitResolver` 内部、`BattleAttackCheckPolicyService`、测试：

- `build_skill_attack_check`
- `build_skill_attack_preview`
- `build_repeat_attack_stage_hit_check`
- `build_fate_aware_repeat_attack_stage_hit_check`
- `build_repeat_attack_preview`
- 后续新增的同类 `build_*attack*check*` / `build_*attack*preview*` 方法

必须纳入 call-site audit 的生产文件包括但不限于：

- `scripts/systems/battle/runtime/battle_skill_execution_orchestrator.gd`
- `scripts/systems/battle/runtime/battle_ground_effect_service.gd`
- `scripts/systems/battle/runtime/battle_charge_resolver.gd`
- `scripts/systems/battle/runtime/battle_repeat_attack_resolver.gd`
- `scripts/systems/battle/presentation/battle_hud_adapter.gd`
- `scripts/systems/battle/ai/battle_ai_score_service.gd`
- `scripts/systems/battle/ai/battle_ai_action_assembler.gd`

### 六、Attack Check Policy Context

`BattleAttackCheckPolicyContext` 必须是 typed class，禁止 Dictionary 作为 policy 输入。

字段必须精确：

```gdscript
class_name BattleAttackCheckPolicyContext
extends RefCounted

var battle_state: BattleState = null
var attacker: BattleUnitState = null
var target: BattleUnitState = null
var skill_def: SkillDef = null
var cast_variant: CombatCastVariantDef = null
var roll_kind: StringName = &""
var check_route: StringName = &""
var trace_source: StringName = &""
var distance: int = -1
var force_hit_no_crit: bool = false
var source_coord: Vector2i = Vector2i(-1, -1)
var target_coord: Vector2i = Vector2i(-1, -1)
var repeat_stage_spec: BattleRepeatAttackStageSpec = null
```

`roll_kind` 只表示攻击 / 骰检定语义，供 modifier filter 使用。固定枚举：

- `&"weapon_attack"`
- `&"spell_attack"`
- `&"fate_weapon_attack"`
- `&"fate_spell_attack"`
- `&"repeat_weapon_stage"`
- `&"ground_weapon_effect"`
- `&"charge_path_weapon"`

`check_route` 只表示 policy 要调用的 resolver 路由。固定枚举：

- `&"skill_attack_check"`
- `&"skill_attack_preview"`
- `&"repeat_attack_stage_check"`
- `&"repeat_attack_preview"`
- `&"force_hit_no_crit_preview"`

`trace_source` 只表示调用来源和 trace。固定枚举：

- `&"hud_preview"`
- `&"ai_score_preview"`
- `&"execute"`
- `&"test"`

重要不变量：modifier filtering 严禁读取 `trace_source`。同一次攻击在 HUD preview、AI score、execute 必须得到同一 `BattleAttackRollModifierBundle`。默认回归必须覆盖三种来源一致。

### 七、Repeat Stage Typed Contract

repeat 路由不能只传 `repeat_stage:int`。当 `check_route` 是 repeat 相关路由时，`BattleAttackCheckPolicyContext.repeat_stage_spec` 必填；缺失时 validation hard fail。

新增 `BattleRepeatAttackStageSpec`：

```gdscript
class_name BattleRepeatAttackStageSpec
extends RefCounted

var stage_index: int = 0
var stage_count: int = 0
var skill_level: int = 0
var stage_base_attack_bonus: int = 0
var follow_up_attack_penalty: int = 0
var penalty_free_stages: int = 0
var exponential_penalty: bool = false
var fate_aware: bool = false
var stage_label: StringName = &""
```

边界：

- repeat policy 只读取 `BattleRepeatAttackStageSpec`，不能重新读取 raw `repeat_attack_effect` 计算阶段惩罚。
- 构建 spec 的代码归 `BattleRepeatAttackResolver` 或 policy adapter 所有。
- call-site audit 必须确认 repeat preview / execute 都走同一 spec。

### 八、Attack Roll Modifier Bundle

新增 typed bundle，建议路径：

- `scripts/systems/battle/core/battle_attack_roll_modifier_bundle.gd`
- `scripts/systems/battle/core/battle_attack_roll_modifier_spec.gd`

`BattleAttackRollModifierBundle` 字段：

```gdscript
class_name BattleAttackRollModifierBundle
extends RefCounted

var total_bonus: int = 0
var total_penalty: int = 0
var breakdown: Array[BattleAttackRollModifierSpec] = []
```

`BattleAttackRollModifierSpec` exact 字段：

```gdscript
class_name BattleAttackRollModifierSpec
extends RefCounted

var source_domain: StringName = &""          # terrain | status | skill | item | debug
var source_id: StringName = &""
var source_instance_id: String = ""
var label: String = ""
var modifier_delta: int = 0
var stack_key: StringName = &""
var stack_mode: StringName = &"add"          # add | max | min | exclusive
var roll_kind_filter: StringName = &""
var endpoint_mode: StringName = &"either"    # attacker | target | either | both
var distance_min_exclusive: int = -1
var distance_max_inclusive: int = -1
var target_team_filter: StringName = &"any"  # ally | enemy | any
var footprint_mode: StringName = &"any_cell" # anchor | any_cell | all_cells
var applies_to: StringName = &"attack_roll"
```

正负号语义：

- `modifier_delta > 0` 计入 `total_bonus`。
- `modifier_delta < 0` 的绝对值计入 `total_penalty`。
- `modifier_delta == 0` 不进入 post-stack breakdown。
- 同一 spec 不允许同时表达 bonus 和 penalty。

Resolution order 必须固定：

1. collect candidates
2. validate exact schema
3. filter by `roll_kind_filter` / team / distance / endpoint / footprint
4. group by `stack_key`
5. resolve `stack_mode`
6. stable sort
7. produce post-stack `breakdown[]`
8. bundle totals 从 post-stack breakdown 计算

Stacking 对负数 penalty 的 exact 语义：

- 同一 `stack_key` 内不得混合 bonus 与 penalty；同时出现正负 `modifier_delta` 时 validation hard fail。
- `stack_mode=add`：同号求和。
- `stack_mode=max`：bonus 取最大正值；penalty 取绝对值最大的惩罚，也就是最负的 `modifier_delta`。
- `stack_mode=min`：bonus 取最小正值；penalty 取绝对值最小的惩罚，也就是最接近 0 的负值。
- `stack_mode=exclusive`：同组只能有一个候选；多于一个 validation hard fail。

`flat_bonus` / `flat_penalty` 只允许作为 policy service 调用 `BattleHitResolver` 的内部汇总通道。外部规格、HUD、AI、report 一律读取 bundle breakdown。

统一 payload 字段：

```gdscript
"attack_roll_modifier_breakdown": Array[Dictionary]
```

该字段必须出现在：

- `BattlePreview.hit_preview`
- attack result dict
- `BattleAiScoreInput.to_dict()` / AI trace
- Meteor report component fact

每个 item 字段与 `BattleAttackRollModifierSpec` 对齐，并额外包含：

```gdscript
"effective_modifier_delta": int
```

### 九、Terrain Modifier Schema

尘土、碎石、陨坑都使用 battle terrain effect 的声明式 schema，并暂存于现有 `cell_state.timed_terrain_effects` 集合；`lifetime_policy` 决定它是 timed 还是 battle lifetime。任何服务不得写 `if source_id == &"meteor_swarm_dust"` 这类技能特例分支。

`accuracy_modifier_spec` exact schema：

```gdscript
{
  "source_domain": &"terrain",
  "label": "尘土",
  "modifier_delta": -2,
  "stack_key": &"dust_attack_roll_penalty",
  "stack_mode": &"max",
  "roll_kind_filter": &"spell_attack",
  "endpoint_mode": &"either",
  "distance_min_exclusive": 1,
  "distance_max_inclusive": -1,
  "target_team_filter": &"any",
  "footprint_mode": &"any_cell",
  "applies_to": &"attack_roll"
}
```

规则：

- service 只识别 schema，不识别具体 Meteor source id。
- `source_id` / `source_instance_id` 只用于 breakdown 展示和 debug。
- footprint / cell 查询必须复用或抽出 `BattleTerrainEffectSystem` 的 timed effect footprint 查询模式，避免复制移动成本遍历逻辑。
- distance gate 使用 `BattleAttackCheckPolicyContext.distance`。`distance_min_exclusive=1` 表示相邻或近身不受尘土惩罚。
- endpoint mode `either` 表示攻击者 footprint 或目标 footprint 任一被 dust 覆盖就生效；同一 stack_key 下不重复叠加。

### 十、范围与 Dust / Rubble / Crater 体验底线

Meteor 效果范围固定为 7x7 方形灾害区：

- `coverage_shape_id=&"square_7x7"`，`radius=3`。
- `final_anchor_coord` 是 7x7 的中心格。
- 距离 `d` 使用 Chebyshev distance：`max(abs(x - anchor.x), abs(y - anchor.y))`。
- 受影响格为 `d <= 3` 的所有合法棋盘格；开放棋盘上总计 49 格，棋盘边缘只做边界裁剪，不改变形状语义。
- 环带定义：`d == 0` 中心格，`d == 1` 内环，`d == 2` 中环，`d == 3` 最外层。
- 伤害、友伤、HUD、AI、战报和地形覆盖必须共用这份 7x7 target plan；禁止任何子系统自行按菱形半径、圆形半径或旧 `area_value` 重算范围。

Phase 1 不接受“字段存在但玩家感知不到”的地形效果。九环地形破坏默认是“战斗内永久”，即持续到本场战斗结束，不跨战斗 / 不跨世界地图持久化。默认数值：

- `meteor_swarm_crater_core`：中心格，`move_cost_delta=3`，`lifetime_policy=&"battle"`，高视觉优先级。
- `meteor_swarm_crater_rim`：`d == 1`，`move_cost_delta=1`，`lifetime_policy=&"battle"`。
- `meteor_swarm_rubble`：`d <= 2` 时 `move_cost_delta=2`，`d == 3` 时 `move_cost_delta=1`，`lifetime_policy=&"battle"`。
- `meteor_swarm_dust`：`d <= 2`，attack-roll `modifier_delta=-2`，`lifetime_policy=&"timed"`，`duration_tu=50`。

Terrain effect 必须同时提供：

```gdscript
"lifetime_policy": &"timed" | &"battle"
"duration_tu": int > 0 when lifetime_policy == &"timed"; 0 allowed when lifetime_policy == &"battle"
"tick_interval_tu": int > 0 and multiple of TU_GRANULARITY(5) when lifetime_policy == &"timed"; 0 allowed when lifetime_policy == &"battle"
"tick_effect_type": &"none" | &"movement_cost" | existing valid type
```

`lifetime_policy` 第一版放在 `BattleTerrainEffectState.params`，不新增顶层序列化字段。`battle` lifetime effect 使用 `remaining_tu=0`、`tick_interval_tu=0`，被 timed decay / removal 跳过，并且在移动成本 / terrain 查询中视为 active；它只随 battle state 存活，不写回世界持久状态。`tick_effect_type=&"none"` exact 定义：`timed` 只推进 duration / expiry，不触发 tick damage / status / log；`battle` 不推进 duration，也不触发 tick damage / status / log。accuracy-only terrain effect 默认 `tick_interval_tu=5`。

Move-cost stacking：

- Phase 1 move-cost 使用当前 battle terrain effect 查询的全局 max 行为；`move_cost_stack_key=&"meteor_impact_move_cost"` / `move_cost_stack_mode=&"max"` 只作为 audit / report metadata，不引入通用 move-cost stack resolver。
- core crater + rubble 最终 `+3`。
- rim + rubble 最终取较大值。
- edge rubble 最终 `+1`。
- 不允许叠成 `+4` 或 `+5`。
- 非 Meteor 既有 slow/status 仍归 `BattleStatusSemanticTable`，不伪装成 crater/rubble。

展示要求：

- board overlay 必须区分 crater / rubble / dust 至少三类视觉层。
- HUD / preview 必须显示影响格数。
- report summary 必须显示本次生成的地形覆盖摘要。

### 十一、Typed Meteor Plan / Outcome

新增 typed classes，建议路径：

- `scripts/systems/battle/core/meteor_swarm/meteor_swarm_profile.gd`
- `scripts/systems/battle/core/meteor_swarm/meteor_swarm_target_plan.gd`
- `scripts/systems/battle/core/meteor_swarm/meteor_swarm_impact_component.gd`
- `scripts/systems/battle/core/meteor_swarm/meteor_swarm_target_outcome.gd`
- `scripts/systems/battle/core/meteor_swarm/meteor_swarm_commit_result.gd`

关键字段：

```gdscript
class_name MeteorSwarmTargetPlan
extends RefCounted

var skill_id: StringName = &"mage_meteor_swarm"
var source_unit_id: StringName = &""
var final_anchor_coord: Vector2i = Vector2i(-1, -1)
var coverage_shape_id: StringName = &"square_7x7"
var radius: int = 3
var affected_coords: Array[Vector2i] = []
var target_unit_ids: Array[StringName] = []
var drift_applied: bool = false
var drift_from_coord: Vector2i = Vector2i(-1, -1)
```

```gdscript
class_name MeteorSwarmImpactComponent
extends RefCounted

var component_id: StringName = &""       # area_blast | secondary_impact | center_direct
var role_label: StringName = &""         # blast_fire | blast_physical | secondary | direct
var damage_tag: StringName = &""         # fire | bludgeoning | force, etc.
var base_power: int = 0
var dice_count: int = 0
var dice_sides: int = 0
var ring_weight: float = 1.0
var save_profile_id: StringName = &""
var can_crit: bool = false
var mastery_weight: float = 1.0
```

```gdscript
class_name MeteorSwarmTargetOutcome
extends RefCounted

var target_unit_id: StringName = &""
var target_coord: Vector2i = Vector2i(-1, -1)
var distance_from_anchor: int = 0
var components: Array[MeteorSwarmImpactComponent] = []
var damage_events: Array[Dictionary] = []
var status_effect_ids: Array[StringName] = []
var terrain_effect_ids: Array[StringName] = []
var attack_roll_modifier_breakdown: Array[Dictionary] = []
var report_component_breakdown: Array[Dictionary] = []
```

```gdscript
class_name MeteorSwarmCommitResult
extends RefCounted

var plan: MeteorSwarmTargetPlan = null
var target_outcomes: Array[MeteorSwarmTargetOutcome] = []
var terrain_effects: Array[Dictionary] = []
var report_entries: Array[Dictionary] = []
var log_lines: Array[String] = []
var changed_unit_ids: Array[StringName] = []
var changed_coords: Array[Vector2i] = []
```

### 十二、伤害数学与混合抗性

保留 v4.9 的组件方向，但必须由 typed component 表达，不从 effect_defs 推导。

默认组件：

- `area_blast.fire`：范围火焰主伤害，吃火焰抗性 / 易伤 / 免疫。
- `area_blast.physical`：范围冲击伤害，吃钝击或指定物理标签抗性。
- `secondary_impact`：次级碎片 / 冲击伤害，按距离或 ring weight 衰减。
- `center_direct`：中心直击，只对最终 anchor 覆盖的中心目标或中心 footprint 目标生效。

混合抗性结算顺序：

1. 按 component 拆分 damage event。
2. 每个 component 独立读取 damage tag。
3. 独立应用 immunity / half / normal / double 档位。
4. half 与 double 抵消遵循现有 CU-16 规则。
5. 固定减伤 / shield / guard 仍由 `BattleDamageResolver` 的正式路径处理。
6. component result 带 `role_label`、`damage_tag`、`mitigation_tier`、`fixed_mitigation_sources`。
7. mastery 只读取 typed result，不读 legacy hp delta。
8. report formatter 基于 component fact 聚合展示。

中心直击非目标：

- 不做独立 attack roll。
- 不触发武器特效。
- 不读取旧 effect_defs。
- 对被 drift 改变的 final anchor 生效，不对原 preview anchor 生效。

### 十三、`meteor_concussed`

`meteor_concussed` 是正式状态，不是一次性日志或手搓 AP 扣减。它必须注册到 `BattleStatusSemanticTable`。

建议常量：

```gdscript
const STATUS_METEOR_CONCUSSED: StringName = &"meteor_concussed"
```

语义字段：

```gdscript
{
  "stack_mode": STACK_REFRESH,
  "max_stacks": 1,
  "tick_mode": TICK_TURN_START_AP_PENALTY,
  "attack_roll_penalty": 2,
  "ap_penalty_group": &"staggered",
  "consume_after_ap_penalty": true,
  "display_label": "震眩",
  "turn_start_log_reason_id": &"meteor_concussed_ap_consumed"
}
```

`BattleSkillTurnResolver.apply_turn_start_statuses()` 必须从逐状态扣 AP 改成 group resolution：

1. 收集所有 `tick_mode == TICK_TURN_START_AP_PENALTY` 的 status。
2. 按 `ap_penalty_group` 分组。
3. 每组取最大 AP penalty，不相加。
4. 应用 AP penalty。
5. 只要 `meteor_concussed` 参与本次 group resolution，就在 resolution 后移除；目标 AP 是否实际减少不影响移除。
6. 若 AP 实际减少，日志写“受震眩影响，本回合少 X AP；震眩消散。”
7. 若 AP 已为 0，日志只写“震眩消散。”
8. 与 `staggered` 共存时只扣一次最高值，`meteor_concussed` 移除，`staggered` 按自身 duration / stack 规则保留。

### 十四、AI 评分与使用门槛

AI owner：

- CU-16 `BattleAiScoreProfile` 持有阈值和评分规则。
- CU-20 enemy brain / action 只消费 use-case 和 score，不另写高威胁判定。

新增或调整字段：

```gdscript
@export var meteor_high_priority_threat_multiplier_bp := 11000
@export var meteor_high_priority_damage_hp_percent := 35
@export var meteor_high_priority_target_priority_score := 250
@export var meteor_top_threat_rank := 1
@export var meteor_friendly_fire_profile: StringName = &"default" # default | reckless
@export var meteor_friendly_fire_soft_expected_hp_percent := 10
@export var meteor_friendly_fire_hard_expected_hp_percent := 25
@export var meteor_friendly_fire_hard_worst_case_hp_percent := 50
```

`high_priority_target` 定义为满足任一条件：

- 目标是 elite / boss。
- 目标 role 是 healer / controller / ranged / artillery，且 threat multiplier >= `meteor_high_priority_threat_multiplier_bp`。
- center direct 预计伤害 >= 目标 max HP 的 `meteor_high_priority_damage_hp_percent`，且目标具备高威胁 role。
- target priority score >= `meteor_high_priority_target_priority_score`。
- 由同一 score service helper 计算为 top threat rank <= `meteor_top_threat_rank`。

Meteor use case：

- `cluster`：3 个或以上有效敌方目标。
- `decapitation`：center direct 可打到 high priority target。
- `zone_denial`：dust / rubble / crater 实际压住敌方路径、远程点位或 choke。

只有三者都不满足时才施加 low-value penalty。AI trace 必须输出：

- `meteor_use_case`
- `high_priority_target_ids`
- `high_priority_reasons`
- `low_value_penalty_reason`
- `friendly_fire_reject_reason`
- `friendly_fire_numeric_summary`
- `attack_roll_modifier_breakdown`

友伤必须走全量数值评估：

- 几何覆盖只负责枚举 nominal plan 和 drift envelope 候选，不作为最终拒绝依据。
- 对每个候选落点、每个受影响友军，必须复用 Meteor typed preview / target outcome 规则，计算 `area_blast.fire`、`area_blast.physical`、`secondary_impact`、`center_direct`、save 概率、抗性 / 易伤 / 免疫、固定减伤、shield、guard、status、AP 惩罚、terrain hostile consequence。
- `build_preview_facts()` / `friendly_fire_numeric_summary` 必须是纯 preview：不修改 `BattleState`，不消费 RNG，不写 batch / log，不触发 mastery / rating / loot / defeat；必须使用 side-effect-free damage preview API 或 cloned snapshot 生成与 execute 同结构的 mitigation / shield / guard / status / AP / terrain 字段。
- 友伤评估禁止用“外圈 / 中心 / 命中友军”这类标签代替数值；AI score 只能读取完整 numeric summary。
- `friendly_fire_risk_percent` 表示“超过友伤阈值的候选概率”，不是“任意友军被几何覆盖的概率”。
- protected ally 任意非零伤害、status、AP 惩罚或敌对地形后果：hard reject。
- 任意友军致死概率 `> 0`、worst-case damage >= 当前 HP、expected damage >= `meteor_friendly_fire_hard_expected_hp_percent`、或 worst-case damage >= `meteor_friendly_fire_hard_worst_case_hp_percent`：hard reject。
- `expected damage <= meteor_friendly_fire_soft_expected_hp_percent` 且无致死、无 protected ally、无中心直击重伤时，允许进入 soft penalty；超过 soft 但未触发 hard 时按数值线性加重 penalty。
- reckless 必须由 enemy brain 显式配置，默认不能启用。

### 十五、战报与 HUD

默认战报必须聚合，避免 5 个目标产生 15 到 20 条 component 日志。

新增 report entry：

```gdscript
{
  "entry_type": "meteor_swarm_impact_summary",
  "skill_id": "mage_meteor_swarm",
  "source_unit_id": String,
  "anchor_coord": Vector2i,
  "target_count": int,
  "terrain_effect_count": int,
  "total_damage": int,
  "defeated_count": int,
  "component_breakdown": Array[Dictionary],
  "target_summaries": Array[Dictionary]
}
```

规则：

- 每次施放默认 1 条总览。
- 每个目标最多 1 条摘要。
- component 明细进入 `component_breakdown[]`，供 HUD tooltip / detail panel / debug text 展开。
- `role_label` 必须进入 component fact，但不默认逐条刷屏。
- terrain summary 必须显示 crater / rubble / dust 覆盖格数。
- 命中修饰 breakdown 只展示生效项，来源于 policy service，不由 report formatter 重算。

### 十六、Manifest Schema / Runtime Gate / Sunset

新增 manifest resource，建议路径：

- `scripts/systems/battle/core/special_profiles/battle_special_profile_manifest.gd`
- `scripts/systems/battle/core/meteor_swarm/meteor_swarm_profile.gd`
- `scripts/systems/battle/runtime/battle_special_profile_registry.gd`
- `data/configs/skill_special_profiles/meteor_swarm_special_profile_manifest.tres`
- `scripts/systems/battle/runtime/battle_special_profile_manifest_validator.gd`

manifest 独立于 `data/configs/skills`。不得直接放进 `data/configs/skills` 或其子目录，因为现有 `SkillContentRegistry` 会递归扫描 `.tres/.res` 并强校验资源脚本必须是 `SkillDef`；把 manifest 放进去会制造启动校验错误，也会模糊“技能定义”和“特殊执行配置”的事实源边界。

manifest / registry 属于 battle ownership，不属于 progression ownership。`SkillContentRegistry` 只校验 `CombatSkillDef.special_resolution_profile_id` 字段和允许枚举，不扫描 manifest、不 import battle runtime、不实例化 resolver、不理解 Meteor 执行配置。`BattleSpecialProfileRegistry` 负责索引和校验 manifest，但 resolver 实例化只能发生在 battle runtime sidecar。

加载 / 注入边界：

- `GameSession` content bootstrap 只负责加载并验证 `BattleSpecialProfileRegistry` 一次，把 `battle_special_profile` 纳入 content validation domain，并暴露不可变 validated registry / snapshot。
- `GameRuntimeFacade.setup()` 把 validated registry / snapshot 传入 `BattleRuntimeModule.setup(...)`。
- `BattleRuntimeModule` 用同一份 registry / snapshot 创建 `_special_profile_gate` 与 resolver 实例；runtime gate 只消费已校验快照，玩家 runtime 不重新扫描 manifest，也不读取 `tests/`。

第一版 manifest 必须小而硬，只包含运行时必须知道且无法从 `SkillDef` 安全推导的字段：

```gdscript
class_name BattleSpecialProfileManifest
extends Resource

@export var profile_id: StringName = &""
@export var schema_version: int = 1
@export var owning_skill_ids: Array[StringName] = []
@export var runtime_resolver_script: Script
@export var profile_resource: Resource = null
@export var runtime_read_policy: StringName = &"forbidden"
@export var presentation_metadata: Dictionary = {}
```

这些字段的硬理由：

- `profile_id` / `owning_skill_ids`：避免 `if skill_id == mage_meteor_swarm` 散落在 HUD / AI / execute。
- `runtime_resolver_script`：把特殊技能绑定到 resolver，不靠 orchestrator 猜测。
- `profile_resource`：给特殊技能执行数据唯一事实源；Meteor 必须指向 `MeteorSwarmProfile`，resolver 禁止硬编码伤害 / 状态 / 地形 / AI 估值数值。
- `runtime_read_policy`：让 special runtime 禁止读取 executable `effect_defs` 变成可验证配置。
- `presentation_metadata`：给 HUD / report 稳定展示入口，不从旧效果链反推文案。
- `presentation_metadata` 禁止参与执行数学、目标选择、状态、地形、AI 估值。

以下字段只在对应阶段启用，不要求 Phase 0 一次塞满：

```gdscript
@export var required_regression_tests: Array[String] = []
@export var deferred_capabilities: Array[Dictionary] = []
@export var sunset_warning_date: String = ""
@export var sunset_hard_block_date: String = ""
```

v4.10.3 未批准任何 compatibility fallback、legacy fallback 或旧 payload bridge。manifest 出现 `active_fallbacks`、`fallbacks`、`legacy_bridge` 等 fallback 字段时 validation 必须 hard fail，并返回 `block_code=&"meteor_unapproved_fallback"`。未来若确实需要兼容路径，必须先按 `AGENTS.md` 说明为什么需要、没有兼容会具体破坏什么、兼容路径会带来什么风险，并取得用户明确确认后单独修订方案。

`@export` 只提供编辑器体验，不算运行时安全。validator 必须手动校验：

- 每个字段 `typeof()`。
- Array 元素类型。
- Dictionary exact keys。
- `owning_skill_ids` 中每个技能存在，且 `skill_def.combat_profile.special_resolution_profile_id == manifest.profile_id`。
- `runtime_resolver_script` 有效，且提供 manifest 声明的 resolver API；registry/validator 只检查脚本契约，不在 content 层执行 resolver 逻辑。
- `profile_resource` 非空；`profile_id=&"meteor_swarm"` 时必须是 `MeteorSwarmProfile`。
- runtime read policy 为 `forbidden`。
- special skill 的 executable `combat_profile.effect_defs` 和 `cast_variants[*].effect_defs` 默认非法；只允许 manifest 白名单声明的非执行 metadata，并由 alignment runner 只读检查。
- 若启用 `required_regression_tests`，test path 必须存在，且会被 `tests/run_regression_suite.py` 默认发现；该字段只参与 content / dev validation，不参与 exported / player runtime gate。
- `required_regression_tests` 不得匹配 `tests/battle_runtime/simulation/*`、`tests/battle_runtime/benchmarks/*`、`tests/text_runtime/tools/*`。
- `as_of_date` 通过 validator 参数注入测试，不改全局 runner。

推荐 API：

```gdscript
func validate_manifest(manifest: Resource, as_of_date: String = "") -> BattleSpecialProfileGateResult
```

日期测试：

- runner 内部直接调用 validator 跑固定日期 fixture。
- 不修改 `tests/run_regression_suite.py` 参数面。
- 例如 `2026-06-26` 触发 warning，`2026-07-10` hard block。

### 十七、Dictionary Boundary / Commit Adapter

Typed pipeline 禁止接收 Dictionary。legacy dict 只允许在 adapter 内部生成和消费。

新增：

- `scripts/systems/battle/runtime/battle_special_profile_commit_adapter.gd`
- `scripts/systems/battle/core/battle_common_skill_outcome.gd`

接口：

```gdscript
func setup(runtime) -> void
func commit_meteor_swarm_result(result: MeteorSwarmCommitResult, batch: BattleEventBatch) -> bool
```

`BattleSpecialProfileCommitAdapter` 不是 tail owner。它只负责验证 typed result、deep copy、把 special outcome 转成 `BattleSkillOutcomeCommitter` 接受的 common typed outcome，然后委托 committer。唯一允许调用 defeat / loot / mastery / rating / report tail owner 的模块是：

```gdscript
scripts/systems/battle/runtime/battle_skill_outcome_committer.gd
```

要求：

- adapter 只接受 typed result，不接受 Dictionary。
- `to_legacy_result_dict()` 只能在 adapter 内部调用。
- legacy dict 必须 `duplicate(true)`。
- `BattleSkillOutcomeCommitter` 正式入口接收 `BattleCommonSkillOutcome`；legacy dict 是 adapter / legacy resolver 内部过渡细节，不得作为 committer 的 public API。
- legacy dict 必须带：

```gdscript
"legacy_schema_id": "meteor_swarm_ground_commit"
"schema_version": int
"boundary_kind": "legacy_result"
```

- audit runner 扫描生产代码，任何 adapter 以外调用 `to_legacy_result_dict()` 都失败。
- typed pipeline 入口发现 Dictionary 直接 hard fail。
- adapter 允许做：验证 typed result、deep copy report/log、转换为 committer 可接受的 common typed outcome。
- adapter 禁止做：重算伤害、命中、抗性、地形范围、AI 评分，读取 Meteor manifest 或 `effect_defs` 来“补全” outcome，推断掉落规则，直接调用 defeat / loot / mastery / rating / report owner。
- 旧的 legacy tail adapter 命名不得用于实现；正式实现使用 `BattleSpecialProfileCommitAdapter`，避免误解为“把旧尾巴继续扩大”。

### 十八、Deferred Capabilities

deferred capability 必须绑定外部依赖 owner，不能只写一个“未来如果有系统就启用”的伪触发。

每个 deferred item exact schema：

```gdscript
{
  "capability_id": StringName,
  "capability_owner_cu": StringName,
  "depends_on_systems": [
    {
      "system_id": StringName,
      "owner_cu": StringName,
      "owner_role_or_issue": String,
      "activation_probe": String,
      "planned_status": StringName,
      "dependency_deadline": String
    }
  ],
  "activation_condition": String,
  "exit_criteria": String
}
```

规则：

- 无 owner / issue / probe 的能力不能叫 deferred。
- 没有建设计划的能力只能写入 `future_opportunities` 或转为 `accepted_limitation`。
- deferred validation 缺字段 hard fail。
- 1B 任何 drift cache / HUD payload / AI payload 的生产行为必须和对应默认 suite runner 同 PR 进入；compatibility fallback 仍需用户明确确认，不属于默认 deferred capability。

### 十九、Runner 分层

新增或调整默认 suite runner：

- `tests/battle_runtime/rules/run_attack_check_policy_contract_regression.gd`
  - direct resolver call audit。
  - HUD / AI / execute modifier bundle 一致。
  - repeat stage spec 必填。
  - `roll_kind` 不受 `trace_source` 影响。

- `tests/battle_runtime/rules/run_attack_roll_modifier_bundle_regression.gd`
  - positive / negative stacking。
  - max / min 对 penalty 的 exact 语义。
  - mixed sign same stack_key hard fail。
  - post-stack breakdown totals。

- `tests/battle_runtime/terrain/run_meteor_swarm_terrain_modifier_regression.gd`
  - dust schema 不靠 source id。
  - attacker / target footprint endpoint。
  - adjacent distance 不生效。
  - double endpoint 不叠加。
  - move-cost max stacking。
  - crater / rubble 使用 `lifetime_policy=&"battle"`，通过 builder 建出 battle lifetime terrain effect，推进 55 / 500 TU 后仍存在。
  - dust 使用 `lifetime_policy=&"timed"` / `duration_tu=50`，到期后消失。
  - `tick_effect_type=&"none"` 对 timed 只推进 duration；对 battle 不推进 duration。
  - 战斗结束 / 新战斗不携带 crater / rubble / dust，不写回世界持久状态。
  - 中心 / d1 / d2 / d3 move-cost exact 值分别覆盖 `+3` / `+2 or +1 max` / `+2` / `+1`。

- `tests/battle_runtime/state_schema/run_battle_terrain_effect_state_schema_regression.gd`
  - `params.lifetime_policy` roundtrip。
  - 顶层 `lifetime_policy` 字段被 strict schema 拒绝。
  - 不新增 terrain effect 顶层序列化字段。

- `tests/battle_runtime/rules/run_meteor_concussed_status_regression.gd`
  - attack_roll_penalty=2。
  - AP group max。
  - 与 staggered 共存只扣一次。
  - concussed 参与后移除。
  - AP=0 日志不声称扣 AP。

- `tests/battle_runtime/skills/run_meteor_swarm_special_profile_regression.gd`
  - target plan 使用 `coverage_shape_id=&"square_7x7"` / `radius=3` / Chebyshev distance；开放棋盘 affected coords 必须为 49 格。
  - 边界裁剪 fixture：中心落边时 28 格，落角时 16 格；ring counts 与 `d == 3` 最外层必须正确。
  - poisoned legacy `area_value` / 菱形配置不影响 typed target plan。
  - typed plan / damage / resistance / status / terrain / mastery / report aggregation。
  - final anchor drift 后重新构建 target plan。
  - effect_defs runtime forbidden。

- `tests/battle_runtime/ai/run_meteor_swarm_ai_regression.gd`
  - cluster / decapitation / zone_denial use-case。
  - high_priority_target 阈值。
  - `friendly_fire_numeric_summary` exact keys：candidate anchor、ally id、component expected / worst-case、lethal probability、save profile、resistance tier、shield、guard、status、AP penalty、hostile terrain consequence。
  - poisoned geometry 用例：友军被几何覆盖但全量数值为 0 时不能 hard reject；数值超过阈值时必须 hard reject。
  - nominal / drift envelope 只枚举候选；hard reject / soft penalty 必须来自全量友伤数值 summary。
  - protected ally 任意非零伤害 / status / AP 惩罚 / 敌对地形 hard reject。
  - 致死概率、expected damage、worst-case damage 超阈值 hard reject。
  - 低 expected damage 且无重伤风险时 soft penalty。
  - trace 字段完整。

- `tests/battle_runtime/runtime/run_meteor_swarm_manifest_gate_regression.gd`
  - manual typeof validation。
  - exact keys。
  - required tests default-suite membership，只在 content / dev validation 中失败。
  - fallback / legacy bridge 字段默认非法。
  - player-safe gate result。
  - preflight cache 给 HUD / AI。

- `tests/battle_runtime/runtime/run_meteor_swarm_preview_surface_contract_regression.gd`
  - 同一 cast 的 `preview_fact_id` / `nominal_plan_signature` / `final_plan_signature` 在 `BattlePreview.special_profile_preview_facts`、HUD snapshot、AI score trace、execute report entry 中一致。
  - 无 drift 时 nominal / final plan signature 一致；drift 后 execute / report 使用 final plan。
  - HUD / AI / execute / report 对 7x7 affected coords、最外层 `d == 3` 和边界裁剪后的格数一致。
  - poisoned legacy `effect_defs.area_value` / 菱形范围配置不能改变四端 exposed facts。
  - drift 场景同时断言 `nominal_plan_signature` 与 `final_plan_signature` 的区别。
  - HUD / AI / report 只消费 preview facts / typed outcome，不读取 Meteor executable `effect_defs`。

- `tests/battle_runtime/rendering/run_battle_board_regression.gd`
  - `cell_state.timed_terrain_effects[*].params.render_overlay_id` 映射 crater / rubble / dust 三类 overlay source。
  - 三类 overlay source 在 `BattleBoardRenderProfile` 注册，重叠优先级稳定。
  - dust 到期后 overlay 消失，crater / rubble battle lifetime overlay 推进 TU 后仍存在。

- `tests/battle_runtime/runtime/run_meteor_swarm_legacy_boundary_regression.gd`
  - typed pipeline 拒收 Dictionary。
  - adapter only 调用 `to_legacy_result_dict()`。
  - legacy dict exact fields / schema id / deep copy。

不修改 `tests/run_regression_suite.py`，除非未来需要通用 test args。当前 as-of-date 注入在 validator runner 内完成。验收时必须在 Godot executable 可解析环境附 `python tests/run_regression_suite.py --list` 输出，证明 `required_regression_tests` 指向默认 suite 成员；不得把 `tests/battle_runtime/simulation/*`、`tests/battle_runtime/benchmarks/*`、`tests/text_runtime/tools/*` 纳入默认 Meteor 验收。AI 友伤概率和数值阈值必须使用确定性 drift envelope / 固定 RNG fixture 与完整 typed component preview，不使用 battle simulation 或 balance runner。

### 二十、非目标与禁止事项

硬禁止：

- `BattleHitResolver` 查 terrain、识别 dust、识别 Meteor。
- HUD / AI / execute 各自重算命中修饰。
- `trace_source` 参与 modifier filtering。
- 生产代码绕过 `BattleAttackCheckPolicyService` 直连 hit resolver build 方法。
- special profile runtime 读取 executable `effect_defs`。
- `flat_penalty` 成为外部规格通道。
- Dictionary 进入 typed pipeline。
- adapter 以外调用 `to_legacy_result_dict()`。
- manifest 错误时 fallback legacy、no-op 或崩溃。
- 未经用户明确确认的 compatibility fallback / legacy bridge。
- deferred capability 无 owner / issue / activation probe。
- 默认 AI 赌友伤。
- report 默认按 component 刷屏。

非目标：

- Phase 1 不实现 LOS 阻挡。
- Phase 1 不实现结构摧毁。
- Phase 1 不实现高度变形 / fall damage。
- Phase 1 不实现 edge destruction，除非对应外部系统有 owner / issue / probe。
- Phase 1 不改全局 regression runner 参数。

### 二十一、实现级分步方案

本节是可开 issue / PR 的实现包粒度：全量架构一次设计清楚，但代码按阶段合入。每一阶段都必须可运行、可回归，且不得新增未确认的兼容路径、旧 schema fallback 或旧 payload 支持。

依赖 DAG：

```text
0 Gate / Manifest
  -> 1A Attack Policy Parity Layer
  -> 1B Attack Policy Full Migration
  -> 1C Skill Orchestrator Boundary Split
  -> 1D Selected Skill Preview Plumbing
  -> {2A Terrain Lifetime Contract, 2B Terrain Modifier / Overlay, 3 Status AP Group}
  -> 4 Meteor Typed Resolver + Special Outcome Committer
  -> 5 HUD / AI / Report Playable Finish
  -> 6 Legacy Cleanup / Audit / Docs
```

#### Phase 0：Gate / Manifest / EffectDefs Boundary

目标：先建立特殊技能身份、manifest 注册、fail-closed gate 与 preview / execute / AI 可共享的阻断结果。此阶段允许 Meteor 还不可施放，但不能让旧 `effect_defs` 继续作为运行时事实源。

代码落点：

- `scripts/player/progression/combat_skill_def.gd`
  - 新增：

```gdscript
@export var special_resolution_profile_id: StringName = &""
```

- `scripts/player/progression/skill_content_registry.gd`
  - 在 `_append_combat_profile_validation_errors()` 校验 `special_resolution_profile_id` 类型 / 合法值。
  - 不在这里扫描 manifest，只校验 `CombatSkillDef` 字段本身。

- `scripts/systems/battle/core/special_profiles/battle_special_profile_manifest.gd`
- `scripts/systems/battle/core/special_profiles/meteor_swarm_profile.gd`
- `scripts/systems/battle/runtime/battle_special_profile_registry.gd`
- `data/configs/skill_special_profiles/meteor_swarm_special_profile_manifest.tres`
- `scripts/systems/battle/runtime/battle_special_profile_manifest_validator.gd`
- `scripts/systems/battle/core/battle_special_profile_gate_result.gd`
- `scripts/systems/battle/core/battle_special_profile_preview_facts.gd`
- `scripts/systems/battle/runtime/battle_special_profile_gate.gd`
- `scripts/systems/battle/core/battle_preview.gd`
  - 新增稳定承载面：

```gdscript
var special_profile_gate_result: BattleSpecialProfileGateResult = null
var special_profile_preview_facts: BattleSpecialProfilePreviewFacts = null
```

`BattleSpecialProfilePreviewFacts` 最少字段：

```gdscript
class_name BattleSpecialProfilePreviewFacts
extends RefCounted

var profile_id: StringName = &""
var skill_id: StringName = &""
var preview_fact_id: StringName = &""
var nominal_plan_signature: String = ""
var final_plan_signature: String = ""
var resolved_anchor_coord: Vector2i = Vector2i(-1, -1)
var target_unit_ids: Array[StringName] = []
var target_coords: Array[Vector2i] = []
var terrain_summary: Dictionary = {}
var attack_roll_modifier_breakdown: Array[Dictionary] = []
```

建议 API：

```gdscript
func preflight_skill(skill_def: SkillDef, battle_state: BattleState) -> BattleSpecialProfileGateResult
func preview_skill(skill_def: SkillDef, command: BattleCommand, active_unit: BattleUnitState, battle_state: BattleState) -> BattleSpecialProfileGateResult
func can_execute_skill(skill_def: SkillDef, command: BattleCommand, active_unit: BattleUnitState, battle_state: BattleState) -> BattleSpecialProfileGateResult
```

Runtime 接线：

- `GameSession` content bootstrap 加载 / 校验 `BattleSpecialProfileRegistry`，`GameRuntimeFacade.setup()` 把 validated registry / snapshot 传入 `BattleRuntimeModule.setup(...)`。
- `BattleRuntimeModule.setup(...)` 基于注入的 validated registry / snapshot 创建 `_special_profile_gate` 与 resolver 实例，不重新扫描 manifest。
- `BattleRuntimeModule.start_battle()` 或技能列表刷新点建立 `_special_profile_gate_cache`。
- `BattleRuntimeModule.preview_command()` 通过 `_skill_orchestrator._preview_skill_command()` 前置或内部读取 gate；失败时写入 `BattlePreview.special_profile_gate_result`、`allowed=false`、`log_lines += player_message`。
- `BattleRuntimeModule.issue_command()` 当前已有 preview-first block；special gate 失败必须在 `_consume_skill_costs()` 前返回 batch。
- special execute gate 必须在 `BattleSkillExecutionOrchestrator` 记录 skill attempt 前执行；gate 失败不记录 attempt、不扣 AP / MP / cooldown、不触发 mastery。
- `BattleAiService` 候选构建读取 preview / gate，不再自己判断 Meteor 配置是否可用。
- `GameSession` content validation snapshot 增加 `battle_special_profile` domain，使用 `BattleSpecialProfileRegistry` / validator 检查 manifest；runtime gate 只消费已校验 registry/cache，不在玩家 runtime 读取 `tests/`。

验收：

- `mage_meteor_swarm` 配了 `special_resolution_profile_id=&"meteor_swarm"` 且 manifest 合法时 preflight allowed。
- manifest 错误时 HUD 灰显、AI 跳过、execute 不扣 AP / MP / cooldown。
- special profile runtime 读取 executable `effect_defs` 的测试必须失败。
- special skill 的 executable `combat_profile.effect_defs` / `cast_variants[*].effect_defs` 非空时默认 content validation 失败；只允许 manifest 白名单中的非执行 metadata。

回归：

- `tests/battle_runtime/runtime/run_meteor_swarm_manifest_gate_regression.gd`
- `tests/battle_runtime/skills/run_meteor_swarm_special_profile_regression.gd` 中先放 forbidden `effect_defs` runtime audit 子用例。

#### Phase 1A：Attack Policy Parity Layer

目标：先新增统一攻击检定入口，但内部仍代理现有 `BattleHitResolver`，保证行为零漂移。这个阶段不引入 dust / terrain modifier，只建立未来所有命中修饰都必须通过 policy 的路径。

代码落点：

- `scripts/systems/battle/rules/battle_attack_check_policy_service.gd`
- `scripts/systems/battle/core/battle_attack_check_policy_context.gd`
- `scripts/systems/battle/core/battle_repeat_attack_stage_spec.gd`
- `scripts/systems/battle/core/battle_attack_roll_modifier_spec.gd`
- `scripts/systems/battle/core/battle_attack_roll_modifier_bundle.gd`

建议 API：

```gdscript
func setup(hit_resolver: BattleHitResolver, terrain_effect_system: BattleTerrainEffectSystem = null) -> void
func build_attack_check(context: BattleAttackCheckPolicyContext) -> Dictionary
func build_attack_preview(context: BattleAttackCheckPolicyContext) -> Dictionary
func build_repeat_attack_stage_hit_check(context: BattleAttackCheckPolicyContext) -> Dictionary
func build_repeat_attack_preview(context: BattleAttackCheckPolicyContext, stage_specs: Array[BattleRepeatAttackStageSpec]) -> Dictionary
func build_modifier_bundle(context: BattleAttackCheckPolicyContext) -> BattleAttackRollModifierBundle
```

`BattleAttackCheckPolicyContext` 的唯一字段清单以 §六为准，Phase 1A 不再另建第二套最小字段。repeat 路径只读 `BattleRepeatAttackStageSpec`；`BattleRepeatAttackResolver` 负责从 legacy `repeat_attack_effect` 构建 stage specs，policy API 不接收 `CombatEffectDef`。

验收：

- policy 输出与现有 resolver 输出逐字段一致。
- `trace_source` 只进 context trace / audit，不参与 modifier filtering。
- `BattleHitResolver` 仍只负责公式和掷骰，不知道 terrain / Meteor / dust。

回归：

- `tests/battle_runtime/rules/run_attack_check_policy_contract_regression.gd`
- `tests/battle_runtime/rules/run_attack_roll_modifier_bundle_regression.gd`

#### Phase 1B：Attack Policy Full Migration

目标：把生产路径的命中 preview / check 调用迁到 `BattleAttackCheckPolicyService`。迁完后，生产代码绕过 policy 直连 `BattleHitResolver.build_*attack*` 即 hard fail。

必须迁移的触点：

- `scripts/systems/battle/runtime/battle_skill_execution_orchestrator.gd`
- `scripts/systems/battle/runtime/battle_ground_effect_service.gd`
- `scripts/systems/battle/runtime/battle_charge_resolver.gd`
- `scripts/systems/battle/runtime/battle_repeat_attack_resolver.gd`
- `scripts/systems/battle/presentation/battle_hud_adapter.gd`
- `scripts/systems/battle/ai/battle_ai_score_service.gd`

`BattleRuntimeModule` 新增：

```gdscript
var _attack_check_policy_service = BATTLE_ATTACK_CHECK_POLICY_SERVICE_SCRIPT.new()
```

并在 `setup()` 中注入 `_hit_resolver`、`_terrain_effect_system`。测试 helper 可以继续直调 `BattleHitResolver`，但生产脚本不允许。

验收：

- unit skill / ground skill / repeat attack / charge / HUD / AI 的 hit preview 与 execute 走同一 policy。
- direct resolver call audit 加入默认 suite。

#### Phase 1C：Skill Orchestrator Boundary Split

目标：给 `BattleSkillExecutionOrchestrator` 减肥。它保留“技能命令生命周期调度”的职责，不再继续承载 preview 细节、legacy 效果解析、damage/report/mastery/defeat/loot 尾部提交。

当前保留给 orchestrator 的职责：

- 解析 skill / cast variant。
- 选择 unit / ground / special route。
- 保证顺序：gate / validation -> cost -> spell control / backlash -> resolver -> committer。
- 调用 preview service / legacy resolver / special resolver / committer。
- 不拥有命中公式、伤害数学、terrain 规则、chain 规则、战报聚合、loot/defeat/rating 规则。

Phase 1C 必须拆成三个小 PR，不作为一次性大拆分：

- `1C-a Preview Extraction`：只抽 `BattleSkillPreviewService`，保持 execute 行为不变。
- `1C-b Common Outcome Committer`：新增 `BattleSkillOutcomeCommitter`，并把 defeat / loot / mastery / rating / report tail owner 收口到这里。
- `1C-c Legacy Resolver Cleanup`：新增 `BattleLegacySkillEffectResolver` 与 legacy outcome classes，让 orchestrator 只调度 resolver / committer。

新增或拆分：

- `scripts/systems/battle/runtime/battle_skill_preview_service.gd`
- `scripts/systems/battle/runtime/battle_legacy_skill_effect_resolver.gd`
- `scripts/systems/battle/runtime/battle_skill_outcome_committer.gd`
- `scripts/systems/battle/runtime/battle_special_profile_commit_adapter.gd`
- `scripts/systems/battle/core/battle_common_skill_outcome.gd`
- `scripts/systems/battle/core/battle_legacy_skill_effect_outcome.gd`
- `scripts/systems/battle/core/battle_legacy_ground_skill_outcome.gd`

建议 API：

```gdscript
# BattleSkillExecutionOrchestrator
func setup(runtime) -> void
func handle_skill_command(active_unit: BattleUnitState, command: BattleCommand, batch: BattleEventBatch) -> void
func preview_skill_command(active_unit: BattleUnitState, command: BattleCommand, preview: BattlePreview) -> void

# BattleSkillPreviewService
func preview_unit_skill(active_unit: BattleUnitState, command: BattleCommand, skill_def: SkillDef, cast_variant: CombatCastVariantDef, preview: BattlePreview) -> void
func preview_ground_skill(active_unit: BattleUnitState, command: BattleCommand, skill_def: SkillDef, cast_variant: CombatCastVariantDef, preview: BattlePreview) -> void
func build_unit_skill_hit_preview(active_unit: BattleUnitState, target_units: Array, skill_def: SkillDef, cast_variant: CombatCastVariantDef) -> Dictionary

# BattleLegacySkillEffectResolver
func resolve_unit_skill(active_unit: BattleUnitState, target_unit: BattleUnitState, skill_def: SkillDef, cast_variant: CombatCastVariantDef) -> BattleLegacySkillEffectOutcome
func resolve_ground_skill(active_unit: BattleUnitState, skill_def: SkillDef, cast_variant: CombatCastVariantDef, effect_coords: Array[Vector2i]) -> BattleLegacyGroundSkillOutcome

# BattleSkillOutcomeCommitter
func commit_unit_outcome(outcome: BattleLegacySkillEffectOutcome, batch: BattleEventBatch) -> bool
func commit_ground_outcome(outcome: BattleLegacyGroundSkillOutcome, batch: BattleEventBatch) -> bool
func commit_common_outcome(outcome: BattleCommonSkillOutcome, batch: BattleEventBatch) -> bool
```

允许第一步的 `BattleLegacySkillEffectOutcome` 内部包裹现有 damage result dict，但只能在 legacy 路径内使用；special typed pipeline 不接收 dict。`BattleSkillOutcomeCommitter` 是唯一 defeat / loot / mastery / rating / report tail owner；`BattleSpecialProfileCommitAdapter` 只能验证 typed result、deep copy / 转换为 committer 可接受的 common typed outcome，然后委托 committer。

验收：

- 现有非 Meteor 技能行为回归不变。
- special route 可以在 Phase 4 接进来，不需要再扩大 orchestrator。
- orchestrator 不直接调用 damage / report / mastery / loot / defeat owner。
- orchestrator 不直接解析 Meteor executable `effect_defs`。
- 对应 audit runner 进入默认 suite。

#### Phase 1D：Selected Skill Preview Plumbing

目标：在 Meteor execute 前先把 shared preview facts 打通到 HUD / AI，避免 Phase 5 才发现展示层拿不到 `BattlePreview.special_profile_preview_facts`。

代码落点：

- `scripts/systems/battle/presentation/battle_hud_adapter.gd`
- `scripts/ui/battle_map_panel.gd`
- `scripts/systems/battle/runtime/battle_runtime_module.gd`
- `scripts/systems/battle/ai/battle_ai_score_input.gd`

要求：

- Battle UI 选择技能 / 悬停目标时构造正式 `BattleCommand` preview，拿到同一份 `BattlePreview` 后传入 HUD adapter。
- `BattleHudAdapter` 对 special profile 优先消费 `BattlePreview.special_profile_preview_facts`，禁止用 legacy `effect_defs` 或 `_hit_resolver.build_*preview()` 重算 Meteor。
- `BattleAiScoreInput` 新增可序列化字段：`special_profile_preview_facts`、`friendly_fire_numeric_summary`、`friendly_fire_reject_reason`、`meteor_use_case`、`attack_roll_modifier_breakdown`。
- 这个阶段只打通数据通道和 snapshot / trace 字段，不调整 AI 权重和 HUD 文案。

验收：

- HUD snapshot / AI trace 能看到同一 `preview_fact_id`、`nominal_plan_signature`、`final_plan_signature`。
- 没有 special preview facts 时普通技能保持现有 preview 行为。

#### Phase 2A：Terrain Lifetime Contract

目标：先让 battle lifetime terrain effect 在现有 `cell_state.timed_terrain_effects` 集合内可表达、可序列化、可推进，再接 dust / crater / rubble 的命中与 overlay。

代码落点：

- `scripts/systems/battle/terrain/battle_terrain_effect_system.gd`
- `scripts/systems/battle/terrain/battle_terrain_effect_state.gd`
- `tests/battle_runtime/state_schema/run_battle_terrain_effect_state_schema_regression.gd`

要求：

- `params.lifetime_policy == &"battle"` 使用 `remaining_tu=0`、`tick_interval_tu=0`；builder 不要求 duration / tick 为正。
- `process_timed_terrain_effects()` 对 battle lifetime effect 不 tick、不扣 remaining、不移除。
- move-cost / terrain modifier / overlay 查询必须把 battle lifetime effect 视为 active。
- `params.lifetime_policy` 必须 roundtrip；顶层 `lifetime_policy` 字段必须被 strict schema 拒绝。
- battle lifetime terrain 不跨战斗、不写回世界持久状态。

#### Phase 2B：Terrain Modifier / Dust Accuracy / Overlay

目标：让 dust / crater / rubble 作为地形系统能力进入命中 policy，而不是让 `BattleHitResolver` 或 Meteor resolver 私读地形。

代码落点：

- `scripts/systems/battle/terrain/battle_terrain_effect_system.gd`
- `scripts/systems/battle/terrain/battle_terrain_effect_state.gd`
- `scripts/player/progression/combat_effect_def.gd`
- `scripts/systems/battle/core/battle_attack_roll_modifier_spec.gd`
- `scripts/systems/battle/core/battle_attack_roll_modifier_bundle.gd`
- `scripts/systems/battle/rules/battle_attack_check_policy_service.gd`
- `scripts/ui/battle_board_controller.gd`
- `scripts/ui/battle_board_render_profile.gd`

配置规则：

- `accuracy_modifier_spec` 第一版放在 battle terrain effect 的 `params`，它是 content schema，不是 Meteor typed pipeline 输入，例如：

```gdscript
params = {
	"lifetime_policy": &"timed",
	"accuracy_modifier_spec": {
		"source_domain": &"terrain",
		"label": "尘土",
		"modifier_delta": -2,
		"stack_key": &"dust_attack_roll_penalty",
		"stack_mode": &"max",
		"roll_kind_filter": &"spell_attack",
		"endpoint_mode": &"either",
		"distance_min_exclusive": 1,
		"distance_max_inclusive": -1,
		"target_team_filter": &"any",
		"footprint_mode": &"any_cell",
		"applies_to": &"attack_roll"
	},
	"render_overlay_id": "meteor_dust_cloud"
}
```

禁止在 v4.10.3 正文使用 `amount`、`max_penalty`、`max_abs_penalty`、`attacker_or_target_footprint`、`attacker_or_target`、`distance_min`、`report_label` 等旧示例字段；content validator 对这些旧字段和未声明 extra keys hard fail，不提供 compatibility fallback。

Crater / rubble battle lifetime 示例：

```gdscript
params = {
	"lifetime_policy": &"battle",
	"move_cost_delta": 2,
	"move_cost_stack_key": &"meteor_impact_move_cost",
	"move_cost_stack_mode": &"max",
	"render_overlay_id": "meteor_rubble"
}
```

规则：

- board overlay 从 `BattleTerrainEffectState.params.render_overlay_id` 或等价字段读取。
- `BattleBoardController` 从 `cell_state.timed_terrain_effects` 映射 dust / rubble / crater overlay source。
- `BattleBoardRenderProfile` 注册 dust / rubble / crater 三类 source / asset。
- 不给 `BattleTerrainEffectState` 加顶层字段，除非另开 terrain state schema 变更。
- `lifetime_policy` 放在 `BattleTerrainEffectState.params`；crater / rubble 为 `battle`，dust 为 `timed`。
- `tick_effect_type=&"none"` 合法化；timed 只推进 duration，battle lifetime 不推进也不触发 tick。
- move-cost 保持当前 battle terrain effect 查询的全局 max 行为，不在 Phase 2 引入通用 `stack_key` move-cost resolver。

新增 API：

```gdscript
func collect_attack_roll_modifier_specs(context: BattleAttackCheckPolicyContext) -> Array[BattleAttackRollModifierSpec]
```

验收：

- dust 从 attacker / target footprint endpoint 收集。
- 相邻距离不生效，双 endpoint 按 stack key 不重复叠加。
- crater / rubble / dust move-cost 采用当前全局 max stacking。
- dust / rubble / crater 在 battle board overlay 中可见，rendering regression 覆盖。

#### Phase 3：Status AP Group / Consume-After

目标：实现 `meteor_concussed`，同时不破坏 `staggered` 旧语义。AP 扣减必须通过 group resolution 统一处理，并支持“参与扣减后移除”。

代码落点：

- `scripts/systems/battle/rules/battle_status_semantic_table.gd`
- `scripts/systems/battle/runtime/battle_skill_turn_resolver.gd`
- `scripts/systems/battle/core/battle_status_effect_state.gd`

状态参数建议：

```gdscript
params = {
	"turn_start_ap_penalty": 1,
	"ap_penalty_group": "staggered",
	"consume_after_ap_penalty": true,
	"ap_penalty_log_key": "meteor_concussed"
}
```

新增或调整 API：

```gdscript
static func get_turn_start_ap_penalty_group(status_entry: BattleStatusEffectState) -> StringName
static func should_consume_after_turn_start_ap_penalty(status_entry: BattleStatusEffectState) -> bool
func resolve_turn_start_ap_penalty(unit_state: BattleUnitState, batch: BattleEventBatch) -> void
```

验收：

- `meteor_concussed` 与 `staggered` 共存时同组只扣一次。
- `meteor_concussed` 参与扣减后移除。
- AP=0 时不输出“扣了 AP”的误导日志。

#### Phase 4：Meteor Typed Resolver + Special Outcome Committer

目标：正式接入 Meteor 行为。这个阶段必须包含 `BattleSpecialProfileCommitAdapter`，不能把 adapter 留到清理阶段，否则 typed outcome 没有受控落地边界。

代码落点：

- `scripts/systems/battle/runtime/battle_meteor_swarm_resolver.gd`
- `scripts/systems/battle/runtime/battle_special_profile_commit_adapter.gd`
- `scripts/systems/battle/core/meteor_swarm/meteor_swarm_cast_context.gd`
- `scripts/systems/battle/core/meteor_swarm/meteor_swarm_profile.gd`
- `scripts/systems/battle/core/meteor_swarm/meteor_swarm_preview_facts.gd`
- `scripts/systems/battle/core/meteor_swarm/meteor_swarm_target_plan.gd`
- `scripts/systems/battle/core/meteor_swarm/meteor_swarm_impact_component.gd`
- `scripts/systems/battle/core/meteor_swarm/meteor_swarm_target_outcome.gd`
- `scripts/systems/battle/core/meteor_swarm/meteor_swarm_commit_result.gd`
- `scripts/systems/battle/runtime/battle_skill_execution_orchestrator.gd`

建议 API：

```gdscript
# BattleMeteorSwarmResolver
func setup(runtime, attack_check_policy_service: BattleAttackCheckPolicyService) -> void
func build_preview_facts(context: MeteorSwarmCastContext) -> MeteorSwarmPreviewFacts
func build_target_plan(context: MeteorSwarmCastContext) -> MeteorSwarmTargetPlan
func resolve(plan: MeteorSwarmTargetPlan) -> MeteorSwarmCommitResult

# BattleSpecialProfileCommitAdapter
func commit_meteor_swarm_result(result: MeteorSwarmCommitResult, batch: BattleEventBatch) -> bool
```

`MeteorSwarmPreviewFacts` 必须包含 `BattleSpecialProfilePreviewFacts` 的所有公共字段，并补充 Meteor 专用字段：

```gdscript
class_name MeteorSwarmPreviewFacts
extends BattleSpecialProfilePreviewFacts

var impact_count: int = 0
var expected_target_count: int = 0
var expected_terrain_effect_count: int = 0
var friendly_fire_risk_percent: int = 0
var friendly_fire_numeric_summary: Array[Dictionary] = []
var component_preview: Array[Dictionary] = []
```

orchestrator 接入点：

```gdscript
if _has_special_profile(skill_def, &"meteor_swarm"):
	var gate_result := _special_profile_gate.can_execute_skill(skill_def, command, active_unit, _runtime._state)
	if not gate_result.allowed:
		_append_gate_block_to_batch(gate_result, batch)
		return
	var validation := _validate_ground_skill_command(active_unit, skill_def, cast_variant, command)
	var target_coords := _extract_validated_target_coords(validation)
	var mp_before_cost := int(active_unit.current_mp)
	if not _consume_skill_costs(active_unit, skill_def, cast_variant, batch):
		return
	var spent_mp := maxi(mp_before_cost - int(active_unit.current_mp), 0)
	var spell_control_context := _resolve_ground_spell_control_after_cost(active_unit, skill_def, spent_mp, batch)
	if bool(spell_control_context.get("skip_effects", false)):
		return
	var drift_context := _runtime._magic_backlash_resolver.build_ground_backlash_target_coords(
		skill_def,
		target_coords,
		_runtime._state,
		_runtime._grid_service,
		spell_control_context
	)
	target_coords = _resolve_drifted_target_coords(target_coords, drift_context, batch)
	var context := _build_meteor_swarm_cast_context(active_unit, command, skill_def, cast_variant, target_coords, spell_control_context, drift_context)
	var plan := _meteor_swarm_resolver.build_target_plan(context)
	var result := _meteor_swarm_resolver.resolve(plan)
	_special_profile_commit_adapter.commit_meteor_swarm_result(result, batch)
	return
```

验收：

- final anchor 在 spell control / backlash drift 后重新构建 target plan。
- 多 component damage、mixed resistance、震眩、尘土、碎石、陨坑、mastery、report aggregation 都来自 typed result。
- legacy dict 只在 adapter 内部 deep copy 生成，adapter 之外调用 `to_legacy_result_dict()` hard fail。

#### Phase 5：HUD / AI / Report Playable Finish

目标：让禁咒在玩家和 AI 侧真正可用，而不是只有 execute 能跑。

代码落点：

- `scripts/systems/battle/presentation/battle_hud_adapter.gd`
- `scripts/systems/battle/ai/battle_ai_score_input.gd`
- `scripts/systems/battle/ai/battle_ai_score_service.gd`
- `scripts/systems/battle/ai/battle_ai_action_assembler.gd`
- `scripts/systems/battle/rules/battle_report_formatter.gd`
- `scripts/systems/battle/runtime/battle_runtime_module.gd`

要求：

- HUD 读取 `BattlePreview.special_profile_preview_facts`，展示影响格、预计目标、dust / crater / rubble 摘要、命中修饰 breakdown。
- `BattleAiScoreInput` / `to_dict()` 输出 `friendly_fire_numeric_summary`、`friendly_fire_reject_reason`、`meteor_use_case`、`attack_roll_modifier_breakdown`，供 trace / tests 稳定断言。
- AI 读取 typed preview facts 与 `friendly_fire_numeric_summary`，不读取 Meteor `effect_defs` 估值。
- AI 友伤默认按全量数值 hard reject；只有 high-priority use case 且友军 expected / worst-case / status / terrain 后果均低于阈值时进入 soft penalty。
- report 默认 1 条总览 + 目标摘要，component 细节只进折叠 payload / debug detail。

回归：

- `tests/battle_runtime/ai/run_meteor_swarm_ai_regression.gd`
- `tests/battle_runtime/rendering/run_battle_board_regression.gd` 增加 overlay / HUD 快照用例。

#### Phase 6：Legacy Cleanup / Audit / Docs

目标：收口所有临时桥接与文档上下文。

必须完成：

- 确认无 fallback / legacy bridge 字段进入正式 manifest；如未来确需兼容，必须先取得用户明确确认并单独修订。
- special runtime 对 executable `effect_defs` 的读取 audit 进入默认 suite。
- adapter-only `to_legacy_result_dict()` audit 进入默认 suite。
- direct hit resolver production call audit 进入默认 suite。
- `docs/design/project_context_units.md` 根据实际新增服务、runtime chain、preferred read set 更新。
- 实现代码不使用旧的 legacy tail adapter 命名，正式类名固定为 `BattleSpecialProfileCommitAdapter`。

### 二十二、Project Context Units 影响

当前只更新设计讨论文档，不改运行时代码，因此本次不修改 `docs/design/project_context_units.md`。

实现时必须按以下 CU 读集推进：

- CU-15：`BattleRuntimeModule`、orchestrator、ground effect、charge、repeat、turn resolver、special resolver、runtime batch、special profile registry / gate / resolver / committer。
- CU-16：hit resolver、damage resolver、status semantic table、report formatter、AI score service/profile、save/resistance 规则、attack check policy。
- CU-17：terrain effect system、terrain profile、terrain modifier schema、future edge/object dependency。
- CU-18：BattleHudAdapter、BattleMapPanel、BattleBoard overlay；只消费 preview facts，不拥有 manifest / gate。
- CU-19：默认 regression suite、battle runtime/rules/AI/rendering tests、audit runners。
- CU-20：enemy AI brain、action definitions、Meteor AI use-case consumption。

若实现新增 `BattleAttackCheckPolicyService`、`BattleSpecialProfileGate`、Meteor typed classes、legacy adapter，并改变 preferred read set 或 runtime chain，必须同步更新 `docs/design/project_context_units.md`。


## 历史审查记录

> 以下章节保留历轮 Kimi / 多方审查意见，方便继续追踪问题来源。它们是历史审查材料，不再代表当前有效方案。当前唯一有效实现依据是上方 v4.10.3 方案。
> 
> **v4.10.2 已修正、v4.10.3 继续收口的内容**：以下汇总表列出历史审查中提出的问题及其在当前方案中的状态。已被修正的意见不再重复展开，仅保留仍未解决的核心风险。

---

### 历史问题修正状态汇总

| 历史问题 | 首次提出 | 当前方案状态 | 说明 |
|----------|----------|-------------|------|
| "只取最内层"反直觉 / 体型豁免修正反向 | 深度讨论 | ✅ 已修正 | v4.3 `area_blast` 全覆盖 + `ring_weight` 消灭层级坍缩 |
| 中心直击无豁免缺乏对标 | 深度讨论 | ✅ 已修正 | v4.3+ 引入 save 机制与 component 分离 |
| Coverage 上限悬空 / Medium 体型 0d6 | v4.2 | ✅ 已修正 | v4.3 `coverage_dice_cap` 线性有上限 |
| AI 友好火评估空白 / 使用率趋近零 | 深度讨论 | ✅ 已修正 | v4.10.2 §十四明确定义友伤 hard reject 规则与 use case |
| `high_priority_target` 未定义 | v4.9 | ✅ 已修正 | v4.10.2 §十四给出 5 条判定标准 + 3 个 use case |
| 战报 component 刷屏 / role label 膨胀 | v4.9 | ✅ 已修正 | v4.10.2 §十五默认 1 条总览 + 目标摘要 + 折叠明细 |
| `meteor_concussed` 触发后移除语义缺失 | v4.9 | ✅ 已修正 | v4.10.2 §十三 `consume_after_ap_penalty` + group resolution |
| manifest schema 不严格 / 字段类型未定义 | v4.4/v4.9 | ✅ 已修正 | v4.10.2 §十六 validator 手动 `typeof()` 逐字段校验 |
| runtime fail-closed UX 真空 | v4.9 | ✅ 已修正 | v4.10.2 §四 `player_message` 固定文案、HUD 灰显、AI 跳过、execute 不回退 |
| Dictionary 禁令不可执行 | v4.4/v4.9 | ✅ 已修正 | v4.10.2 §十七 typed pipeline 拒收 Dictionary + adapter only + audit runner |
| `flat_penalty` 通道与 breakdown 缺口 | v4.9 | ✅ 已修正 | v4.10.2 §八 `BattleAttackRollModifierBundle` + breakdown 完整携带 |
| deferred capabilities 伪 trigger | v4.9 | ✅ 已修正 | v4.10.2 §十八 `depends_on_systems` + `owner_cu` + `activation_probe` 绑定 |
| as_of_date 需改造 `run_regression_suite.py` | v4.9 | ✅ 已修正 | v4.10.2 §十六 validator 参数注入，不改全局 runner |
| 实施顺序倒挂 / 160 skill 全量改道 | v4.2/v4.4 | ✅ 已修正 | v4.5+ Meteor-only canary，v4.10.2 §二十一拆小 Phase 1A |
| 物理拆副作用链 / StageRunner 过度设计 | v4.4 | ✅ 已修正 | v4.5+ 放弃物理拆分，v4.10.2 未要求 StageRunner |
| Commit Adapter 耦合陷阱 | v4.2 | ✅ 已修正 | v4.10.3 §十七 `BattleSpecialProfileCommitAdapter` 明确边界 |
| Preview/Execute/AI 三方一致性 | v4.2/v4.9 | ✅ 已修正 | v4.10.2 §五-§九 `BattleAttackCheckPolicyService` 统一入口 |
| dust 侵入 `BattleHitResolver` 核心 | v4.8/v4.9 | ✅ 已修正 | v4.10.2 §二十硬禁止 resolver 查 terrain，§五-§八 policy service 旁路化 |
| Sunset 过期处理不明确 | v4.6 | ✅ 已修正 | v4.10.2 §十六 `sunset_hard_block_date` + hard block |
| **建筑/固定目标留白** | 深度讨论 | ⚠️ **仍未解决** | v4.10.2 §二十仍列为非目标，无明确规则 |
| **禁咒感缩水 / Phase 2/3 体验补回** | 全部版本 | ⚠️ **仍未解决** | v4.10.2 §二十 Phase 1 不实现 LOS/结构摧毁/高度变形 |
| **文档无机器可读版本追踪** | 深度讨论 | ⚠️ **仍未解决** | 仍是纯 Markdown，manifest `schema_version` 只覆盖 manifest 自身 |
| **状态系统仍需手动硬编码** | 深度讨论/v4.9 | ⚠️ **仍未解决** | `BattleStatusSemanticTable` 340 行 match，新增状态必须手动注册 |
| **Godot 弱类型环境根本性限制** | v4.4/v4.9 | ⚠️ **无法消除** | Dictionary 边界 enforcement 仍依赖纪律，无编译期/AST 能力 |

---

## 深度攻击性讨论（四方检视汇总）

> 对 v4.1 前身方案的最早审查。大量具体批评已在 v4.3–v4.10.2 中逐条修正，以下仅保留仍未解决的核心问题。

### 仍未解决的核心问题

1. **建筑/固定目标致命留白**  
   Meteor Swarm 是典型的"砸建筑"技能，但文档从最早版本到 v4.10.2 都未定义陨石对建筑/固定目标的效果。v4.10.2 §二十仍将结构摧毁列为 Phase 1 非目标。

2. **MVP 暂缓清单过度保守 = 禁咒感缩水**  
   不做推离、不做新地形、不做多 damage_tag、不做 LOS 烟尘。最终得到的是"大范围混合伤害火球术 + 碎石困难地形"，与"陨星雨"的期望差距较大。

3. **状态系统架构失败**  
   "每个新状态必须同步 4-5 个消费端"是状态系统架构失败的直接证据。v4.10.2 §十三仍需手动注册 `meteor_concussed` 到 `BattleStatusSemanticTable`（340 行硬编码 match），问题未根治。

4. **没有版本控制的自我引用循环**  
   文档本身仍是纯 Markdown、无机器可读版本号、无"已实施/待实施/已废弃"标记。v4.10.2 的 manifest `schema_version` 只覆盖 manifest 自身，不覆盖 Markdown 设计文档。

### 综合评分（历史参考）

| 维度 | 评分 | 评语 |
|------|------|------|
| 架构合理性 | D+ | 在过度耦合的系统上继续打补丁，无扩展性设计 |
| 玩法创新性 | D | 所有特色暂缓，最终是火伤+困难地形 |
| 数值平衡性 | C- | 中心直击无豁免缺乏对标，体型修正反向设计 |
| 工程可落地性 | C | 实施顺序倒挂、通用路径无隔离、测试未验证 |
| 规格完整性 | C- | 边界留白（建筑、友好火、地形消费者）、无版本控制 |
| 文档严谨性 | B | 否定性规范列得很细，但回避了核心难题 |

---

## v4.2 四方审查追加意见（2026-05-11）

> 对 v4.1 的审查。大量工程与数值批评已在后续版本中修正。

### 仍未解决的核心问题

1. **建筑/固定目标仍是最大规格的炸弹**  
   v4.1 用架构动作（新增 component）掩盖了玩法规则缺失。陨石雨砸中城墙的效果未定义，v4.10.2 仍未补全。

2. **Dust / Edge / Object 体验缺口**  
   纸面恢复禁咒体验，实际改写的只有 HP 和移动力；视野和结构破坏短期内无法兑现。v4.10.2 将 dust 真实消费纳入 Phase 1，但 edge/object/LOS 仍 deferred。

3. **双轨制隐藏债务**  
   新旧系统并行 + Godot duck typing = 极易出错的维护表面积翻倍。v4.10.2 通过 call-site audit runner 和 typed pipeline 缓解，但未消除。

### 综合评分（历史参考）

| 维度 | 评分 | 判定 |
|------|------|------|
| 架构合理性 | C+ | 有愿景的补丁堆，双轨制风险高 |
| 玩法创新性 | B- | 边际改善，dust/edge/object 仍依赖下游系统 |
| 数值平衡性 | C+ | coverage 上限悬空，低等级层级坍缩，AI 使用率存疑 |
| 工程可落地性 | B | 顺序正确，技术可行，但 commit adapter 和 preview/execute 不对称是硬骨头 |
| 规格完整性 | C | 建筑规则留白，条件分支迷宫，enforcement 缺失 |
| 文档严谨性 | B | 否定性规范列得细，但关键边界仍回避 |

---

## v4.4 四方审查追加意见（2026-05-11）

> 对 v4.3 的审查。v4.3 的激进工程承诺（160 skill 全量改道、物理拆副作用链、CI/static scan）在 v4.5+ 中已全部降级或取消。

### 仍未解决的核心问题

1. **Edge 摧毁表格 vs 实际系统能力**  
   v4.3 给出 Edge 默认规则表格，但 `BattleEdgeFeatureState` 无"摧毁"字段、无 `INTERACT_DESTROY` 语义。v4.10.2 §二十诚实降级为 no-op，但未给出 Phase 2 补回路径。

2. **`mastery_role` 的 Meteor 烙印**  
   `secondary_damage` 和 `direct_hit` 枚举仍带有明显的几何/落点语义，当第 2 个特殊技能到来时仍可能不匹配。v4.10.2 §十一改用 typed component（`MeteorSwarmImpactComponent`），但组件 ID（`area_blast`/`secondary_impact`/`center_direct`）仍带几何语义。

### 综合评分（历史参考）

| 维度 | v4.1 | v4.3 | 判定 |
|------|------|------|------|
| 架构合理性 | C+ | B- | 愿景更干净，但 DoD 过于激进，物理拆分风险被低估 |
| 玩法创新性 | B- | B+ | 数值曲线质变，规则直觉改善，混合伤害有策略深度 |
| 数值平衡性 | C+ | B+ | 平滑、有上限、无断崖，但混合抗性结算未明确 |
| 工程可落地性 | B | C+ | 从"乐观但可行"滑向"复杂度螺旋上升"，全 skill 改道风险高 |
| 规格完整性 | C | C+ | 表格更完整，但大量规则无系统支撑，enforcement 仍依赖人工 |
| 文档严谨性 | B | B+ | 否定性规范+确定 fallback+manifest，但基础设施假设未验证 |

---

## v4.6 四方审查追加意见（2026-05-11）

> 对 v4.5 的审查。v4.5 从 v4.3 的激进愿景收敛为务实路线，大量工程风险已收敛。

### 仍未解决的核心问题

1. **禁咒体验有可感知的缩水**  
   LOS dust、edge 摧毁、height crater/fall damage、推离全部被砍，禁咒感主要依赖伤害数字。v4.10.2 §二十 Phase 1 非目标清单仍包含这些项目。

2. **否定清单过长**  
   v4.5 列出 8 项"不再承诺的内容"，v4.10.2 §二十非目标仍存在。通过砍掉体验要素来消除规格炸弹，是务实的但不是令人兴奋的。

3. **manifest 与 Markdown 双向绑定缺失**  
   manifest 记录 `contract_version`，但 Markdown 设计文档仍无机器可读版本号。三个月后各自变旧的风险仍高。

### 综合评分（历史参考）

| 维度 | v4.3 | v4.5 | 判定 |
|------|------|------|------|
| 架构合理性 | B-（愿景好，DoD 过激） | B（承认套壳，聚焦 canary） | ⬆️ 收敛 |
| 玩法创新性 | B+（数值质变，但工程激进） | B（数值保留，禁咒感缩水） | ⬇️ 缩水 |
| 数值平衡性 | B+ | B+（完整保留 v4.3 数学） | → 持平 |
| 工程可落地性 | C+（物理拆分+全 skill 改道） | **B+**（Meteor-only + 窄 helper） | ⬆️⬆️ 显著收敛 |
| 规格完整性 | C+（表格完整但无系统支撑） | **B-**（降级为现实能力内，自洽但体验缩水） | ⬆️ 收敛 |
| 文档严谨性 | B+ | **B+**（明确"不再承诺"清单，诚实） | → 持平 |

---

## v4.8 四方审查追加意见（2026-05-11）

> 对 v4.7 的审查。v4.7 是 v4.5 的严格化完善版，将松口径补成可验证契约。

### 仍未解决的核心问题

1. **Phase 2/3 体验补回缺失**  
   只是把"否定清单"重新包装成了"延期清单"。Phase 1 实际交付 = 大范围混合伤害 + 碎石移动力惩罚 + 远程命中-2 + 轻 debuff。玩家感受不到"战场被陨石改写"。v4.10.2 §二十和 §十八保留了 deferred capabilities，但 Phase 2/3 的触发条件和补回时间表仍不明确。

2. **"没有 CI 的自动化"在组织纪律上不可持续**  
   hard fail、manifest validation、sunset 校验全部依赖人工运行 runner。v4.10.2 通过 runtime fail-closed gate（§四）和 call-site audit（§五）部分缓解，但入仓漏洞仍未消除——有问题的 manifest 仍可能在代码审查中被遗漏。

### 综合评分（历史参考）

| 维度 | v4.5 | v4.7 | 判定 |
|------|------|------|------|
| 架构合理性 | B（承认套壳，聚焦 canary） | B+（契约硬化，接口钉死） | ⬆️ 提升 |
| 玩法创新性 | B（数值保留，禁咒感缩水） | B（数值完整，禁咒感仍缩水） | → 持平 |
| 数值平衡性 | B+ | **A-**（公式全部写死，可验收） | ⬆️ 提升 |
| 工程可落地性 | B+（Meteor-only + 窄 helper） | **B-**（测试膨胀，dust 侵入核心系统） | ⬇️ 扩散 |
| 规格完整性 | B-（降级为现实能力内） | **B+**（文档颗粒度提升，公式具体） | ⬆️ 提升 |
| 文档严谨性 | B+（明确"不再承诺"清单） | **B+**（诚实但条件间有张力） | → 持平 |

---

## 审查附录：v4.9 可行性分析（2026-05-11）

> 对 v4.9 的审查。v4.9 的大部分具体批评（Dictionary 边界、flat_penalty 缺口、UX 真空、manifest schema、meteor_concussed 触发移除、high_priority_target、战报聚合、deferred capabilities、as_of_date）在 v4.10.2 中已逐条修正。以下仅保留仍未消除的根本性约束。

### 仍未解决的核心问题

1. **Godot 4.6 弱类型环境的根本性限制**  
   Dictionary 边界 enforcement、typed pipeline 白名单、adapter-only 调用——这些在 GDScript 中都无法编译期或运行期强制。v4.10.2 §十七增加了 `duplicate(true)`、audit runner、字段白名单等运行时措施，但** enforcement 基座仍是开发者纪律**，不是语言/引擎能力。

2. **状态系统硬编码未根治**  
   `BattleStatusSemanticTable` 仍是 340 行硬编码 match。v4.10.2 §十三要求注册 `meteor_concussed` 语义，但注册方式是**手动修改** match 分支，不是声明式注册。当第 3、第 4 个特殊技能需要新状态时，同样的问题会重复发生。

3. **复杂度从"单点 deep"变为"多点 wide"**  
   v4.9 把 dust 从 resolver 内部转移到 policy service + modifier bundle，v4.10.2 §五-§八继续这一路线。resolver 纯净了，但新增 `BattleAttackCheckPolicyService`、`BattleAttackRollModifierBundle`/`Spec`、call-site audit runner、legacy adapter 等多个新系统。总代码变更量未减，只是分散了。

### 综合评分（历史参考）

| 维度 | v4.7 | v4.9 | 判定 |
|------|------|------|------|
| 架构合理性 | B+（契约硬化，接口钉死） | A-（resolver 纯净，manifest 严格，runner 分层） | ⬆️ 提升 |
| 玩法创新性 | B（数值完整，禁咒感缩水） | B（内容无变化，工程约束变化） | → 持平 |
| 数值平衡性 | A-（公式全部写死，可验收） | A-（与 v4.7 完全一致） | → 持平 |
| 工程可落地性 | B-（测试膨胀，dust 侵入核心系统） | B+（dust 旁路化，runner 分层，1A/1B 解耦） | ⬆️ 提升 |
| 规格完整性 | B+（文档颗粒度提升，公式具体） | A-（schema 写死、fail-closed 明确、硬禁止 9 项） | ⬆️ 提升 |
| 文档严谨性 | B+（诚实但条件间有张力） | A-（承认 tension 转移，但措辞更严格） | ⬆️ 提升 |
