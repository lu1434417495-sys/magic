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
4. 代码边界必须可审计：新增服务、typed context、common outcome commit adapter、manifest validator、call-site audit runner 都必须有明确 owner 和默认回归。

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
- `scripts/systems/battle/core/special_profiles/battle_special_profile_registry.gd`：新增 manifest registry，只索引 / 校验，不实例化 resolver。
- `scripts/player/progression/combat_skill_def.gd`：新增 `special_resolution_profile_id` 字段；它属于战斗执行语义，不放在顶层 `SkillDef`。
- `data/configs/skills/mage_meteor_swarm.tres`：只保留展示、成本、范围、profile id 与非执行 metadata；执行数据转入 manifest / typed profile。
- `data/configs/skill_special_profiles/manifests/meteor_swarm_special_profile_manifest.tres`：新增独立 manifest 目录，禁止放进 `data/configs/skills`，避免被 `SkillContentRegistry` 当成非 `SkillDef` 技能资源扫描。

### 三、执行顺序

Meteor execute 必须按以下顺序，任何实现不得跳步或把 cost 扣减提前：

1. `BattleRuntimeModule.issue_command()` 保持 preview-first 总入口。
2. `BattleSpecialProfileGate.preflight_skill(skill_def, runtime_state)` 在进入战斗或刷新技能列表时运行，缓存 `BattleSpecialProfileGateResult` 给 HUD / AI。
3. `preview_command()` 读取缓存 gate result；若 `allowed=false`，`BattlePreview.allowed=false` 并附 `player_message`。
4. `execute` 再次调用 gate；失败时在扣 AP / MP / cooldown / mastery 前阻断。
5. 成本、前摇、spell control、fate / backlash 漂移按现有 CU-15 / CU-16 入口运行。
6. 根据最终落点构建 `MeteorSwarmTargetPlan`，禁止复用 drift 前的 preview target list。
7. `BattleMeteorSwarmResolver.resolve(plan)` 生成 typed outcome。
8. outcome 通过 `BattleSpecialProfileCommitAdapter.commit_meteor_swarm_result(result, batch)` 转成 common applied facts，再委托 `BattleSkillTailFinalizer`（若沿用现有类名 `BattleSkillOutcomeCommitter`，其职责也必须按 tail finalizer 理解）进入 common battle outcome tail：changed coords、changed units、log、report、mastery、metrics、rating、defeat、loot。该统一层不负责重新应用 HP / status / equipment / terrain delta，因为当前 damage / special resolver 已经会直接修改 battle state。
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
- future `build_*attack*check*` production helper variants
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
- `scripts/systems/battle/core/special_profiles/battle_special_profile_registry.gd`
- `data/configs/skill_special_profiles/manifests/meteor_swarm_special_profile_manifest.tres`
- `scripts/systems/battle/core/special_profiles/battle_special_profile_manifest_validator.gd`

manifest 独立于 `data/configs/skills`。不得直接放进 `data/configs/skills` 或其子目录，因为现有 `SkillContentRegistry` 会递归扫描 `.tres/.res` 并强校验资源脚本必须是 `SkillDef`；把 manifest 放进去会制造启动校验错误，也会模糊“技能定义”和“特殊执行配置”的事实源边界。

manifest / registry 属于 battle ownership，不属于 progression ownership。`SkillContentRegistry` 只把 `CombatSkillDef.special_resolution_profile_id` 当作 opaque profile id，不维护 profile 白名单、不扫描 manifest、不 import battle runtime、不实例化 resolver、不理解 Meteor 执行配置。`BattleSpecialProfileRegistry` 负责索引和校验 manifest；非空 profile id 必须存在对应 manifest，且 manifest 必须反向 owning 该技能。resolver 实例化只能发生在 battle runtime sidecar。

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
@export var runtime_resolver_id: StringName = &""
@export var profile_resource: Resource = null
@export var runtime_read_policy: StringName = &"forbidden"
@export var presentation_metadata: Dictionary = {}
```

这些字段的硬理由：

- `profile_id` / `owning_skill_ids`：避免 `if skill_id == mage_meteor_swarm` 散落在 HUD / AI / execute。
- `runtime_resolver_id`：把特殊技能绑定到 runtime 侧 allow-list resolver，不让内容资源携带任意 Script。
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
- `runtime_resolver_id == &"meteor_swarm"`；registry/validator 只检查 id / profile 契约，不在 content 层执行 resolver 逻辑。
- `profile_resource` 非空；`profile_id=&"meteor_swarm"` 时必须是 `MeteorSwarmProfile`。
- runtime read policy 为 `forbidden`。
- special skill 的 executable `combat_profile.effect_defs` 和 `cast_variants[*].effect_defs` 默认非法；只允许 manifest 白名单声明的非执行 metadata，并由 alignment runner 只读检查。
- 若启用 `required_regression_tests`，test path 必须存在，且会被 `tests/run_regression_suite.py` 默认发现；该字段只参与 content / dev validation，不参与 exported / player runtime gate。
- `required_regression_tests` 不得匹配 `tests/battle_runtime/simulation/*`、`tests/battle_runtime/benchmarks/*`、`tests/text_runtime/tools/*`。
- `as_of_date` 通过 validator 参数注入测试，不改全局 runner。

推荐 API：

```gdscript
func validate_manifest(manifest: Resource, skill_defs: Dictionary, as_of_date: String = "") -> Array[String]
```

日期测试：

- runner 内部直接调用 validator 跑固定日期 fixture。
- 不修改 `tests/run_regression_suite.py` 参数面。
- 例如 `2026-06-26` 触发 warning，`2026-07-10` hard block。

### 十七、Dictionary Boundary / Commit Adapter

Typed pipeline 禁止接收 Dictionary。common outcome commit payload 只允许在 adapter 内部生成和消费。

新增：

- `scripts/systems/battle/runtime/battle_special_profile_commit_adapter.gd`
- `scripts/systems/battle/core/battle_common_skill_outcome.gd`

接口：

```gdscript
func setup(runtime) -> void
func commit_meteor_swarm_result(result: MeteorSwarmCommitResult, batch: BattleEventBatch) -> bool
```

`BattleSpecialProfileCommitAdapter` 不是 tail owner。它只负责验证 typed result、deep copy、把 special outcome 转成 `BattleSkillTailFinalizer` 接受的 common applied facts，然后委托 finalizer。若实现阶段暂时沿用 `BattleSkillOutcomeCommitter` 类名，该类也必须按 tail finalizer 约束实现：只处理“已经由 resolver/evaluator 改完 state 后”的后处理，不重新应用 HP / status / equipment / terrain delta。唯一允许调用 defeat / loot / mastery / metrics / rating / report tail owner 的模块是：

```gdscript
scripts/systems/battle/runtime/battle_skill_tail_finalizer.gd
```

要求：

- adapter 只接受 typed result，不接受 Dictionary。
- `to_common_outcome_payload()` 只能在 adapter 内部调用。
- commit payload 必须 `duplicate(true)`。
- `BattleSkillTailFinalizer` 正式入口接收 `BattleCommonSkillOutcome` / applied facts；commit payload 是 adapter 内部过渡细节，不得作为 finalizer 的 public API。
- commit payload 必须带：

```gdscript
"commit_schema_id": "meteor_swarm_ground_commit"
"schema_version": int
"boundary_kind": "common_outcome_payload"
```

- audit runner 扫描生产代码，任何 adapter 以外调用 `to_common_outcome_payload()` 都失败。
- typed pipeline 入口发现 Dictionary 直接 hard fail。
- adapter 允许做：验证 typed result、deep copy report/log、转换为 finalizer 可接受的 common applied facts。
- adapter 禁止做：重算伤害、命中、抗性、地形范围、AI 评分，读取 Meteor manifest 或 `effect_defs` 来“补全” outcome，推断掉落规则，直接调用 defeat / loot / mastery / rating / report owner。
- 旧名 `BattleLegacyTailAdapter` 不得用于实现；正式实现使用 `BattleSpecialProfileCommitAdapter`，避免误解为“把旧尾巴继续扩大”。

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
  - preview 阶段的 `BattlePreview.special_profile_preview_facts`、HUD snapshot、AI score trace 共享同一 `preview_fact_id`、`nominal_plan_signature`；此时 `final_plan_signature` 允许等于 nominal，表示“尚未发生 spell control / backlash 后的最终落点”。
  - execute 阶段在 spell control / backlash 后重新构建 final target plan；无 drift 时 execute report 的 `final_plan_signature == nominal_plan_signature`，drift 后 execute report 的 `final_plan_signature != nominal_plan_signature`。
  - HUD / AI 对 nominal 7x7 affected coords 一致；execute / report 对 final 7x7 affected coords 一致。两组都必须维持最外层 `d == 3` 和边界裁剪后的格数契约。
  - poisoned legacy `effect_defs.area_value` / 菱形范围配置不能改变四端 exposed facts。
  - drift 场景同时断言 `nominal_plan_signature` 与 `final_plan_signature` 的区别。
  - HUD / AI 只消费 preview facts；execute / report 只消费 typed final outcome；两者都不读取 Meteor executable `effect_defs`。

- `tests/battle_runtime/rendering/run_battle_board_regression.gd`
  - `cell_state.timed_terrain_effects[*].params.render_overlay_id` 映射 crater / rubble / dust 三类 overlay source。
  - 三类 overlay source 在 `BattleBoardRenderProfile` 注册，重叠优先级稳定。
  - dust 到期后 overlay 消失，crater / rubble battle lifetime overlay 推进 TU 后仍存在。

- `tests/battle_runtime/runtime/run_meteor_swarm_commit_payload_boundary_regression.gd`
  - typed pipeline 拒收 Dictionary。
  - adapter only 调用 `to_common_outcome_payload()`。
  - commit payload exact fields / schema id / deep copy。

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
- adapter 以外调用 `to_common_outcome_payload()`。
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
  - 不在 `_append_combat_profile_validation_errors()` 校验 `special_resolution_profile_id` 合法值。
  - 不在这里扫描 manifest；只把该字段作为 `CombatSkillDef` 的 opaque battle profile id 保留。

- `scripts/systems/battle/core/special_profiles/battle_special_profile_manifest.gd`
- `scripts/systems/battle/core/meteor_swarm/meteor_swarm_profile.gd`
- `scripts/systems/battle/core/special_profiles/battle_special_profile_registry.gd`
- `data/configs/skill_special_profiles/manifests/meteor_swarm_special_profile_manifest.tres`
- `scripts/systems/battle/core/special_profiles/battle_special_profile_manifest_validator.gd`
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

目标：给 `BattleSkillExecutionOrchestrator` 减肥，并先把当前战斗执行链的真实边界钉死。当前 `BattleDamageResolver`、`BattleMeteorSwarmResolver`、ground / charge / repeat resolver 都不是纯函数：它们会在解析过程中直接修改 HP、status、equipment durability、terrain、unit position 等 battle state。因此 Phase 1C 不把统一层设计成“状态 applicator / committer”，而是设计成 **tail finalizer**：resolver / evaluator 负责执行主效果并允许修改 state，applied outcome 只记录“已经发生的事实”，tail finalizer 统一处理 batch、log、report、mastery、metrics、rating、defeat、loot 和 battle-end 顺序。

术语约束：

- `resolver / evaluator`：允许直接修改 battle state 的执行器。当前 Phase 1C 默认保留 `BattleDamageResolver.resolve_effects()` / special resolver 的 mutating 语义。
- `applied outcome / applied facts`：描述已经落地的效果，不是可重放 delta，不能被 finalizer 再次应用 HP / status / equipment / terrain 变化。
- `tail finalizer`：只处理尾部事实归并与战斗批次，不拥有伤害数学，不重复写 state delta。若代码暂时保留 `BattleSkillOutcomeCommitter` 类名，也必须按这个职责实现；更清晰的目标名是 `BattleSkillTailFinalizer`。

当前保留给 orchestrator 的职责：

- 解析 skill / cast variant。
- 选择 unit / ground / special route。
- 构造 `BattleSkillExecutionContext`，保存 active unit、command、skill、cast variant、route kind、validated targets、costs、spell control / drift context。
- 保证顺序：gate / validation -> cost -> spell control / backlash -> mutating resolver/evaluator -> tail finalizer。
- 调用 preview service / route resolver / special resolver / tail finalizer。
- 不拥有命中公式、伤害数学、terrain 规则、chain 规则、战报聚合、loot/defeat/rating 规则，也不把 applied outcome 当成未提交 delta 重新应用。

Phase 1C 必须拆成多个架构 PR，不作为一次性大拆分：

- `1C-a Tail Boundary Contract`：明确 `BattleSkillTailFinalizer` 是 tail owner，不是 state applicator；修正 log/report 写入协议、rating/metrics/mastery 与 battle-end 的顺序。
- `1C-b Applied Outcome DTO`：新增 `AppliedTargetOutcome` / `AppliedGroundOutcome` 等 typed facts，覆盖 damage result、shield、barrier、source status、equipment durability、dispel、special movement、mastery、metrics、defeat、on-kill、log/report facts。
- `1C-c Preview Extraction`：抽 `BattleSkillPreviewService`，但 preview 必须保持只读；先移除 charge preview 对 live unit 的临时坐标突变，并同步更新 attack policy audit 的 owner 文件。
- `1C-d Special Tail Integration`：优先接 Meteor / special common applied facts，因为它已经有 typed result 边界；adapter 只转换 facts，不应用 delta。
- `1C-e Unit Legacy Vertical Slice`：新增 unit-only `BattleLegacySkillEffectResolver`，把非 repeat / 非 chain 的 legacy unit path 转成 applied target outcome，再由 tail finalizer 统一尾部处理。
- `1C-f Route-by-route Cleanup`：repeat、chain、charge、ground 分别独立接入同一 applied outcome / tail finalizer 协议。ground 继续由 `BattleGroundEffectService` owning，不并入 legacy resolver。

新增或拆分：

- `scripts/systems/battle/core/battle_skill_execution_context.gd`
- `scripts/systems/battle/core/battle_applied_target_outcome.gd`
- `scripts/systems/battle/core/battle_applied_ground_outcome.gd`
- `scripts/systems/battle/runtime/battle_skill_preview_service.gd`
- `scripts/systems/battle/runtime/battle_legacy_skill_effect_resolver.gd`
- `scripts/systems/battle/runtime/battle_skill_tail_finalizer.gd`
- `scripts/systems/battle/runtime/battle_special_profile_commit_adapter.gd`
- `scripts/systems/battle/core/battle_common_skill_outcome.gd`
- `scripts/systems/battle/core/battle_legacy_skill_effect_outcome.gd`

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
func resolve_unit_skill(context: BattleSkillExecutionContext, target_unit: BattleUnitState) -> BattleLegacySkillEffectOutcome

# BattleSkillTailFinalizer
func finalize_unit_outcome(outcome: BattleLegacySkillEffectOutcome, batch: BattleEventBatch) -> bool
func finalize_ground_outcome(outcome: BattleAppliedGroundOutcome, batch: BattleEventBatch) -> bool
func finalize_common_outcome(outcome: BattleCommonSkillOutcome, batch: BattleEventBatch) -> bool
```

`BattleLegacySkillEffectOutcome` 可以在过渡期保留现有 damage result dict，但必须同时抽取 typed applied facts，且只能在 legacy 路径内使用；special typed pipeline 不接收 legacy dict。`BattleSkillTailFinalizer` 是唯一 defeat / loot / mastery / metrics / rating / report tail owner；`BattleSpecialProfileCommitAdapter` 只能验证 typed result、deep copy / 转换为 finalizer 可接受的 common applied facts，然后委托 finalizer。

关键顺序：

- `rating / metrics / mastery facts` 必须在处理 defeated ids 和 battle-end check 前入账。
- defeated units 必须批量进入 finalizer，统一执行 loot / clear defeated / battle-end check，避免多目标技能在中途提前 finalize。
- issue-command 路径只写 batch log / batch report；需要即时写 `_state` log 的 advance / timed terrain 路径必须使用独立 API，finalizer 不混用。
- `BattleLegacySkillEffectResolver` 不处理 ground；ground 的 terrain / edge / topology / fall damage 仍由 `BattleGroundEffectService` owning，但 ground 造成的击倒 / rating / log tail 最终必须通过 applied ground outcome 接统一 finalizer。
- `to_common_outcome_payload()` 仍只允许 Meteor typed result 和 special adapter 边界使用；legacy outcome 不得实现同名 API。

验收：

- 现有非 Meteor 技能行为回归不变。
- special route 可以在 Phase 4 接进来，不需要再扩大 orchestrator。
- orchestrator 不直接调用 report / mastery / loot / defeat / rating owner；调用 mutating resolver 后只把 applied facts 交给 tail finalizer。
- orchestrator 不直接解析 Meteor executable `effect_defs`。
- tail finalizer 不重复应用 HP / status / equipment / terrain delta。
- 对应 audit runner 进入默认 suite。

#### Phase 1D：Selected Skill Preview Plumbing

目标：在 Meteor execute 前先把 shared preview facts 打通到 HUD / AI，避免 Phase 5 才发现展示层拿不到 `BattlePreview.special_profile_preview_facts`。

代码落点：

- `scripts/systems/battle/presentation/battle_hud_adapter.gd`
- `scripts/ui/battle_map_panel.gd`
- `scripts/systems/battle/runtime/battle_runtime_module.gd`
- `scripts/systems/battle/ai/battle_ai_score_input.gd`
- `scripts/systems/battle/ai/battle_ai_score_service.gd`
- `scripts/systems/battle/ai/battle_ai_action_assembler.gd`

要求：

- Battle UI 选择技能 / 悬停目标时构造正式 `BattleCommand` preview，拿到同一份 `BattlePreview` 后传入 HUD adapter。
- `BattleHudAdapter` 对 special profile 优先消费 `BattlePreview.special_profile_preview_facts`，禁止用 legacy `effect_defs` 或 `_hit_resolver.build_*preview()` 重算 Meteor。
- `BattleAiScoreInput` 新增可序列化字段：`special_profile_preview_facts`、`friendly_fire_numeric_summary`、`friendly_fire_reject_reason`、`meteor_use_case`、`attack_roll_modifier_breakdown`。
- `BattleAiScoreService` / `BattleAiActionAssembler` 只把 preview facts 复制进 `BattleAiScoreInput.to_dict()` / trace，不调整评分权重、不执行友伤策略。
- 这个阶段只打通数据通道和 snapshot / trace 字段，不调整 AI 权重和 HUD 文案。

验收：

- HUD snapshot / AI trace 能看到同一 `preview_fact_id`、`nominal_plan_signature`；`final_plan_signature` 在 preview 阶段可等于 nominal，真正的 final signature 由 execute report 在 spell control / backlash 后写入。
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
var component_preview: Array[Dictionary] = []
```

`friendly_fire_numeric_summary`、`attack_roll_modifier_breakdown`、`target_coords` 等字段来自 `BattleSpecialProfilePreviewFacts` 基类，Meteor 子类只填充它们，禁止重复声明同名字段。

orchestrator 接入点：

```gdscript
if _has_special_profile(skill_def, &"meteor_swarm"):
	# generic _handle_skill_command() must already have passed special gate before _record_skill_attempt().
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
	target_coords = _extract_validated_target_coords({"target_coords": drift_context.get("target_coords", target_coords)})
	_runtime._magic_backlash_resolver.append_ground_backlash_log(active_unit, skill_def, drift_context, batch)
	var context := _build_meteor_swarm_cast_context(active_unit, command, skill_def, cast_variant, target_coords, spell_control_context, drift_context)
	var plan := _meteor_swarm_resolver.build_target_plan(context)
	var result := _meteor_swarm_resolver.resolve(plan)
	_special_profile_commit_adapter.commit_meteor_swarm_result(result, batch)
	return
```

验收：

- final anchor 在 spell control / backlash drift 后重新构建 target plan。
- 多 component damage、mixed resistance、震眩、尘土、碎石、陨坑、mastery、report aggregation 都来自 typed result。
- commit payload 只在 adapter 内部 deep copy 生成，adapter 之外调用 `to_common_outcome_payload()` hard fail。

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
- adapter-only `to_common_outcome_payload()` audit 进入默认 suite。
- direct hit resolver production call audit 进入默认 suite。
- `docs/design/project_context_units.md` 根据实际新增服务、runtime chain、preferred read set 更新。
- 实现代码不使用旧名 `BattleLegacyTailAdapter`，正式类名固定为 `BattleSpecialProfileCommitAdapter`。

### 二十二、代码级实施蓝图

本节把上面的阶段压到可直接开 PR 的代码粒度。实现时以这里为准；如果本节和前文冲突，以本节为当前执行契约。

#### Phase 0 代码级改动

`scripts/player/progression/combat_skill_def.gd`

- 在 `delivery_categories` 后新增：

```gdscript
@export var special_resolution_profile_id: StringName = &""
```

- 不加旧字段 alias，不读取 `params.special_profile`，不做兼容迁移。

`scripts/player/progression/skill_content_registry.gd`

- 不新增任何 profile id 白名单。
- `_append_combat_profile_validation_errors()` 不判断 profile id 是否为 `meteor_swarm`；它只按 `CombatSkillDef` 的 typed 字段读取 `special_resolution_profile_id`，并把非空值视作 opaque battle profile id。
- `SkillContentRegistry` 禁止 preload / import `battle_special_profile_registry.gd`、`battle_special_profile_manifest.gd`、`meteor_swarm_profile.gd`。它不扫描 manifest、不判断 profile 是否存在。
- profile 是否存在、manifest 是否 owning 对应 skill、skill 是否反向指向同一 profile，全部由 `BattleSpecialProfileRegistry` / `BattleSpecialProfileManifestValidator` 在 `battle_special_profile` validation domain 内 fail-closed。

新增 `scripts/systems/battle/core/special_profiles/battle_special_profile_manifest.gd`

```gdscript
class_name BattleSpecialProfileManifest
extends Resource

@export var profile_id: StringName = &""
@export var schema_version: int = 1
@export var owning_skill_ids: Array[StringName] = []
@export var runtime_resolver_id: StringName = &""
@export var profile_resource: Resource = null
@export var runtime_read_policy: StringName = &"forbidden"
@export var presentation_metadata: Dictionary = {}
@export var required_regression_tests: Array[String] = []
@export var deferred_capabilities: Array[Dictionary] = []
@export var sunset_warning_date: String = ""
@export var sunset_hard_block_date: String = ""
```

新增 `scripts/systems/battle/core/special_profiles/battle_special_profile_registry.gd`

- 常量：`MANIFEST_DIRECTORY := "res://data/configs/skill_special_profiles/manifests"`。
- 字段：

```gdscript
var _manifests_by_profile_id: Dictionary = {}
var _profile_id_by_skill_id: Dictionary = {}
var _validation_errors: Array[String] = []
```

- 方法：

```gdscript
func rebuild(skill_defs: Dictionary, as_of_date: String = "") -> void
func set_manifest_directory(directory_path: String) -> void
func validate() -> Array[String]
func get_manifest(profile_id: StringName)
func get_manifest_for_skill(skill_id: StringName)
func has_profile(profile_id: StringName) -> bool
func get_snapshot() -> Dictionary
```

- `rebuild()` 只扫描 `MANIFEST_DIRECTORY` 下 `.tres/.res`，必须拒绝 duplicate `profile_id`、duplicate `owning_skill_ids`、非 `BattleSpecialProfileManifest` resource；profile resource 必须放在 sibling `profiles/` 目录，禁止放进 manifest 扫描目录。
- `set_manifest_directory()` 只给 tests / validation runner 注入 fixture manifest 目录；正式 `GameSession` 不调用，保持默认 `MANIFEST_DIRECTORY`。
- 若没有任何技能配置非空 `special_resolution_profile_id`，目录不存在不报错；若存在 special skill，目录不存在、manifest 缺失或 owning skill 未覆盖都必须进入 validation errors。
- `get_snapshot()` exact schema：

```gdscript
{
	"ok": _validation_errors.is_empty(),
	"errors": _validation_errors.duplicate(),
	"profiles": {
		"meteor_swarm": {
			"profile_id": "meteor_swarm",
			"runtime_resolver_id": "meteor_swarm",
			"owning_skill_ids": ["mage_meteor_swarm"],
			"profile_resource": manifest.profile_resource,
			"presentation_metadata": manifest.presentation_metadata.duplicate(true),
			"required_regression_tests": manifest.required_regression_tests.duplicate(),
		},
	},
	"profile_id_by_skill_id": {
		"mage_meteor_swarm": "meteor_swarm",
	},
}
```

- snapshot 允许携带已加载 `profile_resource` Resource 引用；不携带 arbitrary Script。runtime 只能按 `runtime_resolver_id` 走 hardcoded allow-list 实例化 resolver。

新增 `scripts/systems/battle/core/special_profiles/battle_special_profile_manifest_validator.gd`

- 方法：

```gdscript
func validate_manifest(manifest: Resource, skill_defs: Dictionary, as_of_date: String = "") -> Array[String]
func validate_meteor_swarm_profile(profile: MeteorSwarmProfile, require_runtime_data: bool = false) -> Array[String]
```

- Meteor 专项 hard fail：
  - `profile_id != &"meteor_swarm"` 时不能使用 `MeteorSwarmProfile`。
  - `profile_id == &"meteor_swarm"` 时 `profile_resource is MeteorSwarmProfile`。
  - `runtime_resolver_id != &"meteor_swarm"` fail；validator 不执行 resolver，不读取 runtime script。
  - `runtime_read_policy != &"forbidden"` fail。
  - `active_fallbacks` / `fallbacks` / `legacy_bridge` 字段出现即 fail。
  - `owning_skill_ids` 中的技能必须存在，且 `skill_def.combat_profile.special_resolution_profile_id == manifest.profile_id`。
  - special skill 的 `combat_profile.effect_defs` 与 `cast_variants[*].effect_defs` 默认必须为空；若未来要保留非执行 metadata，必须先在 manifest exact whitelist 中显式声明。
  - Phase 0 `validate_meteor_swarm_profile(profile, false)` 至少校验 `coverage_shape_id == &"square_7x7"`、`radius == 3`、friendly-fire 阈值为非负 int 且 hard >= soft、`terrain_profiles` / `impact_components` 为 Array；如果这两个数组非空，也必须对已存在 entry 做 exact schema 校验。
  - Phase 4 填入 runtime 数据时必须切到 `validate_meteor_swarm_profile(profile, true)`，在 Phase 0 的 exact schema 基础上额外要求 required runtime entries 非空且完整，禁止空 profile 通过可施放 resolver。

新增 `scripts/systems/battle/core/meteor_swarm/meteor_swarm_profile.gd`

- Phase 0 必须先建立这个 Resource class，否则 manifest validator 无法安全判断 `profile_resource is MeteorSwarmProfile`。

```gdscript
class_name MeteorSwarmProfile
extends Resource

@export var coverage_shape_id: StringName = &"square_7x7"
@export var radius: int = 3
@export var impact_components: Array[Resource] = []
@export var concussed_status_id: StringName = &"meteor_concussed"
@export var terrain_profiles: Array[Dictionary] = []
@export var friendly_fire_soft_expected_hp_percent := 10
@export var friendly_fire_hard_expected_hp_percent := 25
@export var friendly_fire_hard_worst_case_hp_percent := 50
```

- Phase 4 不再新建 class；它开始填满 `meteor_swarm_profile.tres` 的 impact component / terrain profile 数据，并把 resolver 接入 execute。

数据资源同 Phase 0 PR 一起落：

- 新增目录 `data/configs/skill_special_profiles/manifests/` 与 `data/configs/skill_special_profiles/profiles/`。
- 新增 `data/configs/skill_special_profiles/profiles/meteor_swarm_profile.tres`，resource script 为 `MeteorSwarmProfile`。
- 新增 `data/configs/skill_special_profiles/manifests/meteor_swarm_special_profile_manifest.tres`，`profile_id=&"meteor_swarm"`、`runtime_resolver_id=&"meteor_swarm"`、`owning_skill_ids=[&"mage_meteor_swarm"]`、`profile_resource` 指向上面的 profile resource、`runtime_read_policy=&"forbidden"`。
- 修改 `data/configs/skills/mage_meteor_swarm.tres`：设置 `combat_profile.special_resolution_profile_id=&"meteor_swarm"`，清空 executable `combat_profile.effect_defs` 与 `combat_profile.cast_variants[*].effect_defs`；保留展示、成本、范围、目标选择、spell fate / backlash 这类非执行入口 metadata。

`tests/battle_runtime/skills/run_mage_skill_alignment_regression.gd`

- Phase 0 同步更新 mage alignment 的 helper，避免旧“所有 mage 技能必须有 legacy effect_defs”误伤 special profile：

```gdscript
func _has_usable_effect_surface(skill_def) -> bool:
	if skill_def == null or skill_def.combat_profile == null:
		return false
	if skill_def.combat_profile.special_resolution_profile_id != &"":
		return true
	if not skill_def.combat_profile.effect_defs.is_empty():
		return true
	for cast_variant in skill_def.combat_profile.cast_variants:
		if cast_variant != null and not cast_variant.effect_defs.is_empty():
			return true
	return false

func _has_effect_available_at_level(skill_def, skill_level: int) -> bool:
	if skill_def != null and skill_def.combat_profile != null and skill_def.combat_profile.special_resolution_profile_id != &"":
		return true
	# keep existing legacy effect checks below
```

- 该测试仍然要保留对普通 mage 技能 `effect_defs` 的约束；只有 `special_resolution_profile_id != &""` 的技能走 special profile 可执行面。

`tests/runtime/validation/content_validation_runner.gd` 与 `tests/runtime/validation/run_resource_validation_regression.gd`

- 新增 runner 方法：

```gdscript
func validate_battle_special_profile_registry(label: String, skill_defs: Dictionary, manifest_directory: String = "") -> Dictionary
```

- 该方法实例化 `BattleSpecialProfileRegistry`；若 `manifest_directory` 非空，先调用 `set_manifest_directory(manifest_directory)`，再调用 `rebuild(skill_defs)` / `validate()`，返回 domain id `battle_special_profile`。
- official report 必须把 `validate_battle_special_profile_registry("official_battle_special_profiles", skill_defs)` 加进 `build_run_report("official_content", [...])`。
- invalid fixture coverage 至少包含：
  - missing manifest for a skill with `special_resolution_profile_id`。
  - duplicate `profile_id`。
  - duplicate `owning_skill_ids`。
  - manifest `profile_resource` 类型错误。
  - manifest `owning_skill_ids` 指向不存在技能。
  - required regression test path 不存在。
  - Meteor profile exact schema 拼错字段，例如 `accuracy_modifer_spec`。

新增 `scripts/systems/battle/core/battle_special_profile_gate_result.gd`

```gdscript
class_name BattleSpecialProfileGateResult
extends RefCounted

var allowed := false
var profile_id: StringName = &""
var skill_id: StringName = &""
var block_code: StringName = &""
var player_message: String = ""
var debug_details: Dictionary = {}
```

新增 `scripts/systems/battle/core/battle_special_profile_preview_facts.gd`

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
var friendly_fire_numeric_summary: Array[Dictionary] = []
var attack_roll_modifier_breakdown: Array[Dictionary] = []

func to_dict() -> Dictionary:
	return {
		"profile_id": String(profile_id),
		"skill_id": String(skill_id),
		"preview_fact_id": String(preview_fact_id),
		"nominal_plan_signature": nominal_plan_signature,
		"final_plan_signature": final_plan_signature,
		"resolved_anchor_coord": resolved_anchor_coord,
		"target_unit_ids": target_unit_ids.duplicate(),
		"target_coords": target_coords.duplicate(),
		"terrain_summary": terrain_summary.duplicate(true),
		"friendly_fire_numeric_summary": friendly_fire_numeric_summary.duplicate(true),
		"attack_roll_modifier_breakdown": attack_roll_modifier_breakdown.duplicate(true),
	}

func get_friendly_fire_numeric_summary() -> Array[Dictionary]:
	return friendly_fire_numeric_summary.duplicate(true)
```

`scripts/systems/battle/core/battle_preview.gd`

- 新增字段：

```gdscript
var special_profile_gate_result: BattleSpecialProfileGateResult = null
var special_profile_preview_facts: BattleSpecialProfilePreviewFacts = null
```

`scripts/systems/persistence/game_session.gd`

- 新增 preload：

```gdscript
const BATTLE_SPECIAL_PROFILE_REGISTRY_SCRIPT = preload("res://scripts/systems/battle/core/special_profiles/battle_special_profile_registry.gd")
```

- `CONTENT_VALIDATION_DOMAIN_ORDER` 改为包含 `"battle_special_profile"`，位置放在 `"progression"` 后。
- 新增字段：

```gdscript
var _battle_special_profile_registry = BATTLE_SPECIAL_PROFILE_REGISTRY_SCRIPT.new()
```

- `_init()` 中 `_refresh_progression_content()` 后调用 `_refresh_battle_special_profiles()`。
- 新增：

```gdscript
func get_battle_special_profile_registry_snapshot() -> Dictionary:
	return _battle_special_profile_registry.get_snapshot() if _battle_special_profile_registry != null else {}

func _refresh_battle_special_profiles() -> void:
	if _battle_special_profile_registry != null:
		_battle_special_profile_registry.rebuild(_skill_defs)
```

- `_refresh_content_validation_snapshot()` 增加：

```gdscript
"battle_special_profile": _build_content_validation_domain_snapshot(_battle_special_profile_registry)
```

- `_report_content_validation_error()` 的 `match domain_id` 增加：

```gdscript
"battle_special_profile":
	_push_session_error("session.content.battle_special_profile_validation_failed", "Battle special profile content error: %s" % validation_error)
```

`scripts/systems/game_runtime/game_runtime_facade.gd`

- `_battle_runtime.setup(...)` 追加最后一个参数：

```gdscript
_game_session.get_battle_special_profile_registry_snapshot()
```

`scripts/systems/battle/runtime/battle_runtime_module.gd`

- `setup(...)` 追加参数：

```gdscript
battle_special_profile_registry_snapshot: Dictionary = {}
```

- 新增字段：

```gdscript
var _special_profile_registry_snapshot: Dictionary = {}
var _special_profile_gate = null
var _meteor_swarm_resolver = null
var _attack_check_policy_service = null
var _skill_preview_service = null
var _skill_tail_finalizer = null
var _special_profile_commit_adapter = null
```

- `setup()` 内：

```gdscript
_special_profile_registry_snapshot = battle_special_profile_registry_snapshot.duplicate(true)
_setup_special_profile_runtime()
```

- `_setup_special_profile_runtime()` 只根据注入 snapshot 创建 gate / resolver，不扫描资源。
- `_ensure_sidecars_ready()`、`dispose()`、`configure_hit_resolver_for_tests()` 必须同步接线 / 清理 `_attack_check_policy_service`、`_skill_preview_service`、`_skill_tail_finalizer`、`_special_profile_gate`、`_meteor_swarm_resolver`、`_special_profile_commit_adapter`。测试替换 hit resolver 时，policy service 必须重新持有同一个 hit resolver 实例。
- `runtime_resolver_id` 只允许 hardcoded allow-list：`&"meteor_swarm"` -> `BattleMeteorSwarmResolver`。禁止从 manifest snapshot 直接 `new()` arbitrary Script。

`scripts/systems/battle/runtime/battle_special_profile_gate.gd`

- 方法：

```gdscript
func setup(registry_snapshot: Dictionary) -> void
func preflight_skill(skill_def: SkillDef, battle_state: BattleState) -> BattleSpecialProfileGateResult
func preview_skill(skill_def: SkillDef, command: BattleCommand, active_unit: BattleUnitState, battle_state: BattleState) -> BattleSpecialProfileGateResult
func can_execute_skill(skill_def: SkillDef, command: BattleCommand, active_unit: BattleUnitState, battle_state: BattleState) -> BattleSpecialProfileGateResult
```

- fail closed block message 固定：

```gdscript
"该禁咒配置未通过校验，暂时无法施放。"
```

`BattleRuntimeModule.preview_command()` / preview service

- 当前 `issue_command()` 已经 preview-first；因此 special gate 必须先进入 preview，不能只在 execute gate 才阻断。
- `_preview_skill_command()` 或搬出后的 `BattleSkillPreviewService.preview_skill_command()` 在 legacy unit/ground preview 前加入：

```gdscript
if _runtime._has_special_profile(skill_def, &"meteor_swarm"):
	var gate_result = _runtime._special_profile_gate.preview_skill(skill_def, command, active_unit, _runtime._state)
	preview.special_profile_gate_result = gate_result
	if not gate_result.allowed:
		preview.allowed = false
		preview.log_lines.append(gate_result.player_message)
		return
	_runtime._meteor_swarm_resolver.populate_preview(active_unit, command, skill_def, preview)
	return
```

- `populate_preview()` 必须设置 `preview.allowed`、`preview.resolved_anchor_coord`、`preview.target_coords`、`preview.target_unit_ids`、`preview.special_profile_preview_facts`。AI / HUD 读取这份 nominal preview facts，不允许各自重算范围。
- execute 不把 preview facts 当 final plan，也不要求 `_should_block_skill_issue_from_preview()` 把 `BattlePreview` 传入 orchestrator。`issue_command()` 的 preview-first 只负责放行 / 阻断；execute gate 只做 registry/config/command 合法性 gate。Meteor execute 在 spell control / backlash 后用最终 anchor 重新构建 final target plan，并在 typed outcome / report 中写入 final facts。无 drift 时 final signature 应与 nominal signature 一致；drift 后必须不同。

新增 `scripts/systems/battle/runtime/battle_meteor_swarm_resolver.gd` 最小 stub

- Phase 0 若已经激活 `mage_meteor_swarm` 的 profile id，runtime allow-list 必须能实例化 resolver，不能等到 Phase 4 才有文件。
- stub 只负责 fail closed，不做伤害 / 地形 / AI 估值：
- Phase 0 独立合入时接受 `mage_meteor_swarm` 暂时不可施放；PR 描述和 regression 必须明确这是 fail-closed canary，不是 silent regression。若不接受该短暂不可施放窗口，则必须把 `mage_meteor_swarm.tres` 数据切换延后到 Phase 4 同 PR。

```gdscript
func setup(runtime, attack_check_policy_service = null) -> void

func populate_preview(active_unit: BattleUnitState, command: BattleCommand, skill_def: SkillDef, preview: BattlePreview) -> void:
	preview.allowed = false
	preview.log_lines.append("该禁咒结算尚未接入。")
```

- Phase 4 将这个 stub 扩展成完整 `build_preview_facts()` / `build_target_plan()` / `resolve()` 实现。

`scripts/systems/battle/runtime/battle_skill_execution_orchestrator.gd`

- `_handle_skill_command()` 在 `_record_skill_attempt(active_unit, command.skill_id)` 之前加入：

```gdscript
if _runtime._has_special_profile(skill_def, &"meteor_swarm"):
	var gate_result = _runtime._special_profile_gate.can_execute_skill(skill_def, command, active_unit, _runtime._state)
	if not gate_result.allowed:
		_runtime._append_special_profile_gate_block(batch, gate_result)
		return
```

- gate 失败不得记录 skill attempt、不得扣资源、不得触发 mastery。
- Meteor handler 内不重复做第一道 gate；最多 assert `gate_result.allowed` 或复用 preview gate result。generic `_handle_skill_command()` 在 route 分发前永远先 gate，再 `_record_skill_attempt()`。

#### Phase 1A / 1B 代码级改动

新增 `scripts/systems/battle/core/battle_attack_check_policy_context.gd`

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

新增 `scripts/systems/battle/core/battle_repeat_attack_stage_spec.gd`

```gdscript
class_name BattleRepeatAttackStageSpec
extends RefCounted

var stage_index := 0
var attack_roll_bonus := 0
var force_hit_no_crit := false
var source_effect_id: StringName = &""
var trace_label: String = ""
```

- repeat / charge / policy service 只传 typed stage spec，不把 raw effect_def 直接塞进 attack policy。

新增 `scripts/systems/battle/core/battle_attack_roll_modifier_spec.gd`

- exact schema keys：`source_domain`、`label`、`modifier_delta`、`stack_key`、`stack_mode`、`roll_kind_filter`、`endpoint_mode`、`distance_min_exclusive`、`distance_max_inclusive`、`target_team_filter`、`footprint_mode`、`applies_to`。
- `from_dict()` 必须拒绝 extra keys、旧键、非 int modifier。

新增 `scripts/systems/battle/core/battle_attack_roll_modifier_bundle.gd`

```gdscript
class_name BattleAttackRollModifierBundle
extends RefCounted

var flat_bonus := 0
var flat_penalty := 0
var breakdown: Array[Dictionary] = []
```

- 方法：

```gdscript
func add_spec_result(spec: BattleAttackRollModifierSpec, source_instance_id: StringName) -> void
func to_dict() -> Dictionary
```

新增 `scripts/systems/battle/rules/battle_attack_check_policy_service.gd`

```gdscript
func setup(runtime, hit_resolver: BattleHitResolver, terrain_effect_system: BattleTerrainEffectSystem) -> void
func build_modifier_bundle(context: BattleAttackCheckPolicyContext) -> BattleAttackRollModifierBundle
func build_attack_preview(context: BattleAttackCheckPolicyContext) -> Dictionary
func resolve_attack_check(context: BattleAttackCheckPolicyContext) -> Dictionary
func build_repeat_attack_preview(context: BattleAttackCheckPolicyContext, stage_specs: Array[BattleRepeatAttackStageSpec]) -> Dictionary
```

- Phase 1A：`build_modifier_bundle()` 返回空 bundle，只代理现有 `_hit_resolver`，默认测试要求结果零漂移。
- Phase 1A 同 PR 新增 `tests/battle_runtime/rules/run_attack_policy_parity_regression.gd`：在 modifier bundle 为空时，对 unit attack、ground unit attack、repeat stage、charge path hit check 比较旧 `_hit_resolver` 与 `BattleAttackCheckPolicyService` 的 preview / execute check 字典，要求零漂移。
- Phase 1B：替换生产路径：
  - `BattleSkillExecutionOrchestrator._build_unit_skill_hit_preview()`。
  - `BattleSkillExecutionOrchestrator._resolve_unit_skill_effect_result()` 中正式 unit execute hit check。
  - `BattleGroundEffectService._resolve_ground_unit_effect_result()` 中正式 ground unit hit check。
  - `BattleRepeatAttackResolver` preview / execute hit check。
  - `BattleChargeResolver` path / hit check。
  - `BattleHudAdapter._build_selected_skill_hit_preview()`，但 special profile preview facts 存在时必须直接返回 facts，不走 hit resolver。
  - `BattleAiScoreService._resolve_estimated_hit_rate_percent()` 只读 `preview.hit_preview` 或 special facts breakdown。

`BattleHitResolver` 保持纯公式 owner；禁止新增 terrain / Meteor 分支。

#### Phase 1C 代码级改动

Phase 1C 的代码级目标不是把所有 resolver 立刻改成 pure delta。当前 `BattleDamageResolver` / special resolver / ground resolver 都是 mutating evaluator，第一阶段必须承认这一点：统一层处理的是 applied facts 和 tail finalization，不是状态 delta application。

新增 `scripts/systems/battle/core/battle_skill_execution_context.gd`

```gdscript
class_name BattleSkillExecutionContext
extends RefCounted

var active_unit: BattleUnitState = null
var command: BattleCommand = null
var skill_def: SkillDef = null
var cast_variant: CombatCastVariantDef = null
var route_kind: StringName = &"" # unit | ground | repeat | charge | special
var target_units: Array[BattleUnitState] = []
var target_coords: Array[Vector2i] = []
var preview_coords: Array[Vector2i] = []
var costs: Dictionary = {}
var spell_control_context: Dictionary = {}
var drift_context: Dictionary = {}
```

- context 只保存当前命令生命周期内的稳定解析结果，不作为 save payload，不进入 UI snapshot。
- orchestrator 是 context owner；各 resolver 不重新解析 skill / cast variant / validated targets。

新增 `scripts/systems/battle/core/battle_applied_target_outcome.gd`

- 表达单目标“已经应用”的结果，不是待提交 delta。
- 字段必须覆盖：
  - `source_unit_id`
  - `target_unit_id`
  - `skill_id`
  - `damage_result`
  - `shield_result`
  - `barrier_result`
  - `special_result`
  - `equipment_durability_events`
  - `dispel_events`
  - `status_effect_ids`
  - `source_status_effect_ids`
  - `removed_status_effect_ids`
  - `damage_events`
  - `mastery_facts`
  - `metrics_facts`
  - `defeat_facts`
  - `on_kill_resource_facts`
  - `log_lines`
  - `report_entries`

新增 `scripts/systems/battle/core/battle_applied_ground_outcome.gd`

- 表达 ground route 已经应用的地面事实，不是地形 delta applicator。
- 字段必须覆盖：
  - `source_unit_id`
  - `skill_id`
  - `affected_unit_ids`
  - `changed_coords`
  - `target_outcomes: Array[BattleAppliedTargetOutcome]`
  - `terrain_effect_facts`
  - `timed_terrain_effect_facts`
  - `edge_facts`
  - `topology_facts`
  - `fall_damage_facts`
  - `log_lines`
  - `report_entries`

新增 `scripts/systems/battle/runtime/battle_skill_preview_service.gd`

- 从 orchestrator 搬出：
  - `_preview_skill_command()`。
  - `_preview_unit_skill_command()`。
  - `_preview_ground_skill_command()`。
  - `_build_unit_skill_hit_preview()`。
  - `_build_unit_skill_damage_preview()`。
- 对外 API：

```gdscript
func setup(runtime, attack_check_policy_service: BattleAttackCheckPolicyService) -> void
func preview_skill_command(active_unit: BattleUnitState, command: BattleCommand, preview: BattlePreview) -> void
func preview_unit_skill(active_unit: BattleUnitState, command: BattleCommand, skill_def: SkillDef, cast_variant: CombatCastVariantDef, preview: BattlePreview) -> void
func preview_ground_skill(active_unit: BattleUnitState, command: BattleCommand, skill_def: SkillDef, cast_variant: CombatCastVariantDef, preview: BattlePreview) -> void
```

- preview service 必须保持只读；抽出前先移除 charge preview 对 live `active_unit` 的临时坐标突变，改为 clone / pure geometry。
- `run_attack_policy_callsite_audit.gd` 必须同步把 unit preview policy owner 从 orchestrator 改到 preview service。

新增 `scripts/systems/battle/core/battle_common_skill_outcome.gd`

```gdscript
class_name BattleCommonSkillOutcome
extends RefCounted

var source_unit_id: StringName = &""
var skill_id: StringName = &""
var total_damage := 0
var total_healing := 0
var changed_unit_ids: Array[StringName] = []
var changed_coords: Array[Vector2i] = []
var target_outcomes: Array[BattleAppliedTargetOutcome] = []
var target_summaries: Array[Dictionary] = []
var report_entries: Array[Dictionary] = []
var log_lines: Array[String] = []
var mastery_facts: Array[Dictionary] = []
var metrics_facts: Array[Dictionary] = []
var defeat_facts: Array[Dictionary] = []
var terrain_summary: Dictionary = {}
var special_profile_preview_facts: BattleSpecialProfilePreviewFacts = null
```

新增 `scripts/systems/battle/runtime/battle_skill_tail_finalizer.gd`

```gdscript
func setup(runtime) -> void
func finalize_unit_outcome(outcome: BattleLegacySkillEffectOutcome, batch: BattleEventBatch) -> bool
func finalize_ground_outcome(outcome: BattleAppliedGroundOutcome, batch: BattleEventBatch) -> bool
func finalize_common_outcome(outcome: BattleCommonSkillOutcome, batch: BattleEventBatch) -> bool
```

- finalizer 不应用 HP / status / equipment / terrain delta；这些状态已经由 resolver/evaluator 落地。
- `finalize_common_outcome()` 是 special/common applied facts 的 tail owner，负责 changed ids/coords、batch log/report append、mastery、metrics、rating、defeat、loot 调用顺序。
- finalizer 必须先提交 rating / metrics / mastery facts，再统一处理 defeated ids；battle-end check 必须发生在本批 defeated ids 处理完成后。
- issue-command 路径只能写 batch log / batch report；不得调用会立即写 `_state` 的 log helper。
- 如果实现阶段暂时沿用 `battle_skill_outcome_committer.gd` 文件名，必须在文件头注释和 API 中明确它当前是 tail finalizer，不是 state applicator；最终推荐改名为 `battle_skill_tail_finalizer.gd`。
- Phase 1C 不承诺一次性迁完所有 legacy tail。legacy unit / ground / repeat / charge 在对应 PR 迁入 finalizer 前，保留现有 tail；文档中“唯一 owner”只对已经转成 applied outcome 的 path 生效。

新增 `scripts/systems/battle/runtime/battle_legacy_skill_effect_resolver.gd`

- 只处理 legacy unit skill path，不处理 ground。
- 第一版只接受 `BattleSkillExecutionContext` 和明确 target unit，返回 `BattleLegacySkillEffectOutcome`。
- 不接收 special typed result，不调用 `to_common_outcome_payload()`。
- 不拥有 defeat / loot / rating / battle-end check。

`BattleSkillExecutionOrchestrator`

- 保留：
  - `_handle_skill_command()`
  - route 判断
  - cost / spell control / backlash 调度
- 新增 / 使用 `BattleSkillExecutionContext`。
- 不再新增 tail owner 直接调用；新 special path 必须走 resolver -> adapter -> finalizer。legacy path 的 tail 迁移按独立 route PR 逐步收敛。
- ground route 保留 `BattleGroundEffectService` owner；后续通过 `BattleAppliedGroundOutcome` 接入 finalizer，不并入 `BattleLegacySkillEffectResolver`。

#### Phase 1D 代码级改动

`scripts/systems/battle/core/battle_preview.gd`

- special fields 已在 Phase 0 加入；本阶段开始填充。

`scripts/systems/battle/presentation/battle_hud_adapter.gd`

- `build_snapshot(...)` 末尾新增可选参数，避免破坏既有调用：

```gdscript
selected_skill_preview: BattlePreview = null
```

- `_build_selected_skill_hit_preview(...)` 也新增 `selected_skill_preview: BattlePreview = null` 参数，并且 special facts 旁路必须在解析 `target_unit` 之前执行；Meteor 是 ground AoE，不能依赖单体 target 才进入命中预览。
- 普通技能也优先消费 runtime preview：若 `selected_skill_preview != null and not selected_skill_preview.hit_preview.is_empty()`，直接返回 `selected_skill_preview.hit_preview.duplicate(true)`。这样 HUD/headless selected preview 会跟 `BattleAttackCheckPolicyService` 的 terrain / dust modifier 一致。
- 只有 `selected_skill_preview == null` 或 runtime preview 没给 hit preview 时，才允许保留现有 `_hit_resolver` 路径作为临时 fallback；Phase 2B 开启 terrain accuracy modifier 后，production HUD path 不得再依赖这个 fallback。

- 在 `_build_selected_skill_hit_preview()` 开头加入：

```gdscript
if selected_skill_preview != null and selected_skill_preview.special_profile_preview_facts != null:
	return {
		"summary_text": "",
		"modifier_breakdown": selected_skill_preview.special_profile_preview_facts.attack_roll_modifier_breakdown.duplicate(true),
		"source": "special_profile_preview_facts",
	}
if selected_skill_preview != null and not selected_skill_preview.hit_preview.is_empty():
	return selected_skill_preview.hit_preview.duplicate(true)
```

- 新增 `_build_special_profile_hud_summary(preview_facts)`，输出影响格数、预计目标、terrain summary、友伤风险，但不读 `effect_defs`。

`scripts/ui/battle_map_panel.gd`

- 在已有选择技能 / 目标坐标刷新路径里构造 `BattleCommand`，通过现有 `_battle_command_preview_callable` 调 runtime preview。`BattleMapPanel` 没有 `_battle_runtime` 字段，禁止直接引用 runtime：
- 若 helper 使用 `BattlePreview` 返回类型，先新增 `const BattlePreview = preload("res://scripts/systems/battle/core/battle_preview.gd")`。

```gdscript
func _build_selected_skill_preview(
	battle_state: BattleState,
	selected_coord: Vector2i,
	selected_skill_id: StringName,
	selected_skill_target_coords: Array[Vector2i],
	selected_skill_target_unit_ids: Array[StringName],
	selected_skill_variant_id: StringName
) -> BattlePreview:
	if battle_state == null or selected_skill_id == &"":
		return null
	var active_unit := battle_state.units.get(battle_state.active_unit_id) as BattleUnitState
	var preview_callable := _resolve_battle_command_preview_callable()
	if active_unit == null or not preview_callable.is_valid():
		return null
	var command := BattleCommand.new()
	command.command_type = BattleCommand.TYPE_SKILL
	command.unit_id = active_unit.unit_id
	command.skill_id = selected_skill_id
	command.skill_variant_id = selected_skill_variant_id
	command.target_coord = selected_coord
	command.target_coords = selected_skill_target_coords.duplicate()
	command.target_unit_ids = selected_skill_target_unit_ids.duplicate()
	if not command.target_unit_ids.is_empty():
		command.target_unit_id = command.target_unit_ids[0]
	var preview = preview_callable.call(command)
	return preview as BattlePreview
```

- 调用 `BattleHudAdapter.build_snapshot(..., selected_preview)`。

`scripts/systems/game_runtime/game_runtime_snapshot_builder.gd`

- `_build_battle_snapshot()` 也要构造同一份 selected skill preview，并把它作为 `BattleHudAdapter.build_snapshot(..., selected_preview)` 最后一个参数；否则运行时 snapshot / headless HUD 与实际 UI 口径会分叉。
- 这里可以复用 `Callable(_runtime, "preview_battle_command")`，不要绕过 `GameRuntimeFacade.preview_battle_command()`。

`scripts/systems/battle/ai/battle_ai_score_input.gd`

- 新增字段并写入 `to_dict()`：

```gdscript
var special_profile_preview_facts: Dictionary = {}
var friendly_fire_numeric_summary: Array[Dictionary] = []
var friendly_fire_reject_reason: StringName = &""
var meteor_use_case: StringName = &""
var attack_roll_modifier_breakdown: Array[Dictionary] = []
```

`scripts/systems/battle/ai/battle_ai_score_service.gd`

- `build_skill_score_input()` 在 `_copy_target_coords(preview)` 后调用：

```gdscript
_copy_special_profile_preview_facts(score_input, preview)
```

- `_copy_special_profile_preview_facts()` 只复制 facts 到 score input，不改评分：

```gdscript
func _copy_special_profile_preview_facts(score_input: BattleAiScoreInput, preview) -> void:
	if score_input == null or preview == null or preview.special_profile_preview_facts == null:
		return
	var facts = preview.special_profile_preview_facts
	score_input.special_profile_preview_facts = facts.to_dict()
	score_input.attack_roll_modifier_breakdown = facts.attack_roll_modifier_breakdown.duplicate(true)
	if facts.has_method("get_friendly_fire_numeric_summary"):
		score_input.friendly_fire_numeric_summary = facts.get_friendly_fire_numeric_summary()
```

`scripts/systems/battle/ai/battle_ai_action_assembler.gd`

- 生成 Meteor ground action 时不得靠 `effect_defs` 推断友伤上限；只设置 skill/action shell，评分交给 preview facts。

#### Phase 2A 代码级改动

`scripts/systems/battle/terrain/battle_terrain_effect_system.gd`

- 新增常量：

```gdscript
const PARAM_LIFETIME_POLICY := "lifetime_policy"
const LIFETIME_TIMED: StringName = &"timed"
const LIFETIME_BATTLE: StringName = &"battle"
```

- 新增 helper：

```gdscript
func _resolve_lifetime_policy(effect_state_or_def) -> StringName
func _is_battle_lifetime_effect(effect_state) -> bool
func _is_timed_terrain_effect_active(effect_state) -> bool
static func is_terrain_effect_active(effect_state) -> bool
```

- `_build_timed_terrain_effect()` 改为：

```gdscript
var lifetime_policy := ProgressionDataUtils.to_string_name(effect_def.params.get(PARAM_LIFETIME_POLICY, LIFETIME_TIMED))
if lifetime_policy == LIFETIME_BATTLE:
	effect_state.tick_interval_tu = 0
	effect_state.remaining_tu = 0
	effect_state.next_tick_at_tu = 0
else:
	var tick_interval_tu := _normalize_positive_tu_value(int(effect_def.tick_interval_tu), "terrain effect tick_interval_tu")
	var duration_tu := _normalize_positive_tu_value(int(effect_def.duration_tu), "terrain effect duration_tu")
	if tick_interval_tu <= 0 or duration_tu <= 0:
		return null
	effect_state.tick_interval_tu = tick_interval_tu
	effect_state.remaining_tu = maxi(duration_tu, tick_interval_tu)
	effect_state.next_tick_at_tu = current_tu + tick_interval_tu
```

- `process_timed_terrain_effects()` 在 while 前加：

```gdscript
if _is_battle_lifetime_effect(effect_state):
	retained_effects.append(effect_state)
	continue
```

- `_get_timed_terrain_move_cost_delta()` 改为：

```gdscript
if effect_state == null or not _is_timed_terrain_effect_active(effect_state):
	return 0
```

- `_is_timed_terrain_effect_active()` 对 battle lifetime 返回 true，对 timed 要求 `remaining_tu > 0`。
- `is_terrain_effect_active()` 是公开静态口径，movement、timed processing、attack policy、board overlay 都必须使用同一逻辑或调用链，禁止各自用 `remaining_tu > 0` 重写一套。

`scripts/systems/battle/terrain/battle_terrain_effect_state.gd`

- 不新增顶层字段。schema regression 必须确认顶层 `lifetime_policy` extra key 被拒绝。

#### Phase 2B 代码级改动

`scripts/systems/battle/terrain/battle_terrain_effect_system.gd`

- 新增查询 API，供 policy / board 共用：

```gdscript
func collect_active_effects_for_coords(coords: Array[Vector2i]) -> Array[BattleTerrainEffectState]
func collect_active_effects_for_unit_target(unit_state: BattleUnitState, target_coord: Vector2i) -> Array[BattleTerrainEffectState]
```

- 两者都必须过滤 `_is_timed_terrain_effect_active()`。

`scripts/systems/battle/rules/battle_attack_check_policy_service.gd`

- `build_modifier_bundle()`：
  1. 收集 attacker footprint 与 target footprint 上 active terrain effects。
  2. 读取 `effect_state.params.accuracy_modifier_spec`。
  3. 用 exact schema 生成 `BattleAttackRollModifierSpec`。
  4. 按 `roll_kind_filter`、`endpoint_mode`、`distance_min_exclusive` 过滤。
  5. 同 `stack_key` 只取 max penalty。
  6. 输出 `breakdown[]`，不把 `trace_source` 用于 filtering。

`scripts/ui/battle_board_controller.gd`

- `_draw_terrain_layers()` 中 base overlay 后追加：

```gdscript
var terrain_effect_overlay_source_id := _get_terrain_effect_overlay_source_id(cell_state, coord)
if terrain_effect_overlay_source_id >= 0 and height_index < _overlay_layers.size():
	_overlay_layers[height_index].set_cell(coord, terrain_effect_overlay_source_id, Vector2i.ZERO, 0)
```

- 新增：

```gdscript
func _get_terrain_effect_overlay_source_id(cell_state: BattleCellState, coord: Vector2i) -> int
func _resolve_terrain_effect_overlay_priority(effect_state: BattleTerrainEffectState) -> int
```

- 优先级：crater > rubble > dust。读取 `effect_state.params.render_overlay_id` 和 `overlay_priority`。
- overlay 不直接展示 raw `cell_state.timed_terrain_effects`；必须过滤 `BattleTerrainEffectSystem.is_terrain_effect_active(effect_state)`，避免已过期 timed effect 残留渲染。

`scripts/ui/battle_board_render_profile.gd`

- 注册 source：`meteor_crater`、`meteor_rubble`、`meteor_dust_cloud`。资源可先复用现有 rubble/scrub tile，但 source id 必须独立，便于后续换图。

#### Phase 3 代码级改动

`scripts/systems/battle/rules/battle_status_semantic_table.gd`

- 新增常量：

```gdscript
const STATUS_METEOR_CONCUSSED: StringName = &"meteor_concussed"
const AP_PENALTY_GROUP_STAGGERED: StringName = &"staggered"
```

- 新增：

```gdscript
static func get_turn_start_ap_penalty(status_entry: BattleStatusEffectState) -> int
static func get_turn_start_ap_penalty_group(status_entry: BattleStatusEffectState) -> StringName
static func should_consume_after_turn_start_ap_penalty(status_entry: BattleStatusEffectState) -> bool
```

`scripts/systems/battle/runtime/battle_skill_turn_resolver.gd`

- `apply_turn_start_statuses()` 中把逐状态 AP 扣减改成：
  1. 收集 turn start AP penalty statuses。
  2. 按 group 分组。
  3. 每组取最大 penalty。
  4. 一次性扣 AP。
  5. 对 `consume_after_turn_start_ap_penalty` 的状态，在参与 resolution 后移除。
  6. AP 已为 0 时日志不声称少了 AP。

#### Phase 4 代码级改动

扩展 Phase 0 已新增的 `scripts/systems/battle/core/meteor_swarm/meteor_swarm_profile.gd`

- 本阶段填满 `data/configs/skill_special_profiles/profiles/meteor_swarm_profile.tres` 的 impact component / terrain profile 数据，并把 resolver 接入 execute。
- 同 PR 扩展 `BattleSpecialProfileManifestValidator.validate_meteor_swarm_profile(profile, true)`，full schema 必须 hard fail：
  - `impact_components` 元素不是 `MeteorSwarmImpactComponent`。
  - component id 重复或为空。
  - component `damage_tag` 为空，`dice_count/dice_sides/dice_bonus` 非 int 或负数，`ring_min/ring_max` 越界。
  - `terrain_profiles[*]` 不是 Dictionary，或包含 exact keys 以外字段。
  - `terrain_profiles[*].lifetime_policy` 不是 `&"battle"` / `&"timed"`。
  - `terrain_profiles[*].accuracy_modifier_spec` 拼写错误、缺 `modifier_delta`、`modifier_delta` 非 int，或使用未批准 key。
  - `terrain_profiles[*].render_overlay_id` 为空或不是 String/StringName。
  - `terrain_profiles[*].move_cost_delta` 非 int。

新增 typed classes：

- `meteor_swarm_cast_context.gd`
- `meteor_swarm_preview_facts.gd`：必须 `extends BattleSpecialProfilePreviewFacts`，并填充 `friendly_fire_numeric_summary` / `attack_roll_modifier_breakdown`。
- `meteor_swarm_target_plan.gd`
- `meteor_swarm_impact_component.gd`
- `meteor_swarm_target_outcome.gd`
- `meteor_swarm_commit_result.gd`

`meteor_swarm_target_plan.gd` 必须包含：

```gdscript
var coverage_shape_id: StringName = &"square_7x7"
var radius: int = 3
var final_anchor_coord: Vector2i = Vector2i(-1, -1)
var affected_coords: Array[Vector2i] = []
var ring_by_coord: Dictionary = {}
var target_unit_ids: Array[StringName] = []
var nominal_plan_signature: String = ""
var final_plan_signature: String = ""
```

扩展 Phase 0 已新增的 `scripts/systems/battle/runtime/battle_meteor_swarm_resolver.gd`

```gdscript
func setup(runtime, attack_check_policy_service: BattleAttackCheckPolicyService) -> void
func populate_preview(active_unit: BattleUnitState, command: BattleCommand, skill_def: SkillDef, preview: BattlePreview) -> void
func build_preview_facts(context: MeteorSwarmCastContext) -> MeteorSwarmPreviewFacts
func build_target_plan(context: MeteorSwarmCastContext) -> MeteorSwarmTargetPlan
func resolve(plan: MeteorSwarmTargetPlan) -> MeteorSwarmCommitResult
```

- `build_target_plan()` 算法固定：

```gdscript
for dx in range(-3, 4):
	for dy in range(-3, 4):
		var coord := final_anchor + Vector2i(dx, dy)
		if not grid_service.is_inside(state, coord) or grid_service.get_cell(state, coord) == null:
			continue
		var d := maxi(absi(dx), absi(dy))
		if d > 3:
			continue
		plan.affected_coords.append(coord)
		plan.ring_by_coord[coord] = d
```

- `affected_coords` 排序稳定：先 `y` 后 `x` 或使用项目现有 `_sort_coords()`。
- 开放棋盘格数必须为 49；边 / 角只裁剪非法格，不改变 `d`。
- `target_unit_ids` 收集规则：遍历排序后的 `affected_coords`，用现有 grid/unit footprint 查询得到 occupant；大型单位任一 footprint cell 命中即纳入一次，使用 Dictionary 去重，最终按项目现有单位排序规则稳定排序。
- `center_direct` footprint 判定：目标单位 footprint 任一格等于 `final_anchor_coord` 即视为 center direct 命中，不要求单位 origin 在中心。
- `final_plan_signature` 由 `coverage_shape_id`、`final_anchor_coord`、排序后的 `affected_coords`、`target_unit_ids`、profile version 组成，禁止加入随机数。

`build_preview_facts()` 纯函数要求：

- 不修改 `BattleState`。
- 不消费 RNG。
- 不写 batch / log。
- 不触发 mastery / rating / loot / defeat。
- 使用 side-effect-free damage preview API 或 cloned snapshot 计算 component expected / worst-case / save / resistance / shield / guard。

`resolve(plan)`：

- 对每个目标生成 `MeteorSwarmTargetOutcome`。
- component 顺序固定：`area_blast.fire`、`area_blast.physical`、`secondary_impact`、`center_direct`。
- `center_direct` 只对 `ring == 0` 或 footprint 覆盖 final anchor 的目标生效。
- 地形写入按 ring：
  - `d == 0` crater core。
  - `d == 1` crater rim + rubble。
  - `d == 2` rubble + dust。
  - `d == 3` edge rubble。
- terrain result 使用 `BattleTerrainEffectSystem.upsert_timed_terrain_effect()`，其中 crater/rubble params `lifetime_policy=&"battle"`，dust params `lifetime_policy=&"timed"`。

`scripts/systems/battle/runtime/battle_skill_execution_orchestrator.gd`

- 在 legacy ground route 前插入 `_handle_meteor_swarm_skill_command()`。
- `_handle_meteor_swarm_skill_command()` 按以下顺序写：
  1. 不重复第一道 gate；generic `_handle_skill_command()` 已在 `_record_skill_attempt()` 前完成 gate。这里最多 assert gate result allowed。
  2. `_validate_ground_skill_command()`。
  3. `_consume_skill_costs(active_unit, skill_def, cast_variant, batch)`。
  4. `_record_action_issued(...)`。
  5. `_resolve_ground_spell_control_after_cost(...)`，若 `skip_effects` 为 true，直接返回 false。
  6. 不调用 legacy `_apply_ground_precast_special_effects()`；Meteor special profile 的 precast 行为必须在 typed resolver/profile 中显式建模。
  7. 调用现有 `_runtime._magic_backlash_resolver.build_ground_backlash_target_coords(...)`，再从 `drift_context["target_coords"]` 读取偏移后的坐标，并调用 `append_ground_backlash_log(...)`。
  8. `_build_meteor_swarm_cast_context(...)`。
  9. `build_target_plan()`。
  10. `resolve()`。
  11. `commit_meteor_swarm_result()`。

新增 `scripts/systems/battle/runtime/battle_special_profile_commit_adapter.gd`

- `commit_meteor_swarm_result(result, batch)`：
  1. 验证 `result is MeteorSwarmCommitResult`。
  2. deep copy `report_entries` / `log_lines` / facts。
  3. 转成 `BattleCommonSkillOutcome`。
  4. 调用 `_skill_tail_finalizer.finalize_common_outcome(common, batch)`；若实现暂时沿用 `_skill_outcome_committer` 命名，也必须按 tail finalizer 语义执行，不重新应用 HP / status / equipment / terrain delta。

#### Phase 5 代码级改动

`scripts/systems/battle/ai/battle_ai_score_profile.gd`

- 新增 export 字段：

```gdscript
@export var meteor_high_priority_threat_multiplier_bp := 11000
@export var meteor_high_priority_damage_hp_percent := 35
@export var meteor_high_priority_target_priority_score := 250
@export var meteor_top_threat_rank := 1
@export var meteor_friendly_fire_profile: StringName = &"default"
@export var meteor_friendly_fire_soft_expected_hp_percent := 10
@export var meteor_friendly_fire_hard_expected_hp_percent := 25
@export var meteor_friendly_fire_hard_worst_case_hp_percent := 50
```

`scripts/systems/battle/ai/battle_ai_score_service.gd`

- `build_skill_score_input()` 检测 special facts：

```gdscript
if _is_meteor_score_input(score_input):
	_populate_meteor_score_input(score_input, context)
	return score_input
```

- `_populate_meteor_score_input()` 只读 `score_input.special_profile_preview_facts` / `friendly_fire_numeric_summary`：
  - 计算 `meteor_use_case`：cluster / decapitation / zone_denial。
  - 计算 high priority target。
  - 根据 numeric summary 设置 hard reject reason 或 soft penalty。
  - 不读 `effect_defs`。

`scripts/enemies/actions/use_ground_skill_action.gd`

- `_passes_friendly_fire_limits(score_input)` 开头：

```gdscript
if score_input.meteor_use_case != &"":
	return score_input.friendly_fire_reject_reason == &""
```

- Meteor 的友伤由 numeric summary 决定，不使用 `maximum_friendly_fire_target_count` 粗判。

`scripts/systems/battle/rules/battle_report_formatter.gd`

- 新增 `format_meteor_swarm_summary(entry: Dictionary) -> Array[String]` 或在现有 report entry formatter 分支处理 `entry_type == "meteor_swarm_impact_summary"`。
- 默认输出 1 条总览；component 细节只进 payload / debug。

#### Phase 6 代码级审计

新增 audit runner：

- `tests/battle_runtime/runtime/run_meteor_swarm_commit_payload_boundary_regression.gd`
  - 搜索生产代码，adapter 以外出现 `to_common_outcome_payload(` fail。
  - 搜索 special runtime 中读取 `.effect_defs` 并用于 Meteor 伤害 / 状态 / 地形 / AI fail。
- `tests/battle_runtime/rules/run_attack_policy_callsite_audit.gd`
  - 搜索生产代码中 `build_skill_attack_preview(` / `build_repeat_attack_preview(` / `build_skill_attack_check(` / `build_repeat_attack_stage_hit_check(` / `build_fate_aware_repeat_attack_stage_hit_check(` / `build_*attack*check*` 直连 `_hit_resolver`；允许名单只保留 `BattleAttackCheckPolicyService`、`BattleHitResolver` 内部和 tests helper。
  - audit 必须覆盖 unit execute、ground execute、repeat execute、charge path execute、HUD preview、AI score preview 六类路径。
- `tests/battle_runtime/rules/run_attack_policy_parity_regression.gd`
  - Phase 1A/1B 的行为回归：modifier bundle 为空时，policy service 对 unit / ground / repeat / charge 的 preview 与 execute hit check 必须和旧 hit resolver 零漂移。

必须运行的窄回归：

```bash
godot --headless --script tests/battle_runtime/runtime/run_meteor_swarm_manifest_gate_regression.gd
godot --headless --script tests/runtime/validation/run_resource_validation_regression.gd
godot --headless --script tests/progression/schema/run_skill_schema_regression.gd
godot --headless --script tests/battle_runtime/skills/run_mage_skill_alignment_regression.gd
godot --headless --script tests/battle_runtime/state_schema/run_battle_terrain_effect_state_schema_regression.gd
godot --headless --script tests/battle_runtime/terrain/run_meteor_swarm_terrain_modifier_regression.gd
godot --headless --script tests/battle_runtime/skills/run_meteor_swarm_special_profile_regression.gd
godot --headless --script tests/battle_runtime/ai/run_meteor_swarm_ai_regression.gd
godot --headless --script tests/battle_runtime/runtime/run_meteor_swarm_preview_surface_contract_regression.gd
godot --headless --script tests/battle_runtime/runtime/run_meteor_swarm_commit_payload_boundary_regression.gd
godot --headless --script tests/battle_runtime/rules/run_attack_policy_parity_regression.gd
godot --headless --script tests/battle_runtime/rules/run_attack_policy_callsite_audit.gd
godot --headless --script tests/battle_runtime/rendering/run_battle_board_regression.gd
```

### 二十三、Project Context Units 影响

当前只更新设计讨论文档，不改运行时代码，因此本次不修改 `docs/design/project_context_units.md`。

实现时必须按以下 CU 读集推进：

- CU-15：`BattleRuntimeModule`、orchestrator、ground effect、charge、repeat、turn resolver、special resolver、runtime batch、special profile registry / gate / resolver / tail finalizer。
- CU-16：hit resolver、damage resolver、status semantic table、report formatter、AI score service/profile、save/resistance 规则、attack check policy。
- CU-17：terrain effect system、terrain profile、terrain modifier schema、future edge/object dependency。
- CU-18：BattleHudAdapter、BattleMapPanel、BattleBoard overlay；只消费 preview facts，不拥有 manifest / gate。
- CU-19：默认 regression suite、battle runtime/rules/AI/rendering tests、audit runners。
- CU-20：enemy AI brain、action definitions、Meteor AI use-case consumption。

若实现新增 `BattleAttackCheckPolicyService`、`BattleSpecialProfileGate`、Meteor typed classes、common outcome commit adapter，并改变 preferred read set 或 runtime chain，必须同步更新 `docs/design/project_context_units.md`。


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
   v4.9 把 dust 从 resolver 内部转移到 policy service + modifier bundle，v4.10.2 §五-§八继续这一路线。resolver 纯净了，但新增 `BattleAttackCheckPolicyService`、`BattleAttackRollModifierBundle`/`Spec`、call-site audit runner、common outcome commit adapter 等多个新系统。总代码变更量未减，只是分散了。

### 综合评分（历史参考）

| 维度 | v4.7 | v4.9 | 判定 |
|------|------|------|------|
| 架构合理性 | B+（契约硬化，接口钉死） | A-（resolver 纯净，manifest 严格，runner 分层） | ⬆️ 提升 |
| 玩法创新性 | B（数值完整，禁咒感缩水） | B（内容无变化，工程约束变化） | → 持平 |
| 数值平衡性 | A-（公式全部写死，可验收） | A-（与 v4.7 完全一致） | → 持平 |
| 工程可落地性 | B-（测试膨胀，dust 侵入核心系统） | B+（dust 旁路化，runner 分层，1A/1B 解耦） | ⬆️ 提升 |
| 规格完整性 | B+（文档颗粒度提升，公式具体） | A-（schema 写死、fail-closed 明确、硬禁止 9 项） | ⬆️ 提升 |
| 文档严谨性 | B+（诚实但条件间有张力） | A-（承认 tension 转移，但措辞更严格） | ⬆️ 提升 |
