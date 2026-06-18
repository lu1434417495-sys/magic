# 长剑家族 · 卷一：审判与誓约（1-20）

---

## 系统落地架构要求

本卷的 20 把剑不是普通 `ItemDef` 数值表。它们应作为“装备能力系统”的第一批验收内容：`WeaponProfileDef` 继续只描述武器本体，如武器类型、单双手骰、伤害标签、射程和 properties；唯一装备带来的命中触发、叠层、主动能力、反应、召唤、环境条件、每战限制和跨休息限制，应进入独立的装备能力架构。

本节是实现 contract，不是风味描述。不要把下列机制写进 `attribute_modifiers` 文本后由运行时解析，也不要在战斗流程中按 `item_id` 写单件武器分支。每把剑必须成为同一套 typed 能力语言的配置。

### 总体边界

- `WeaponProfileDef` 只承载武器物理 profile，不承载唯一装备能力。
- `ItemDef` 只挂装备能力 profile 引用，不直接承载复杂战斗逻辑。
- 装备能力 profile 是独立 typed content，必须进入 `GameSession` / `GameContentCatalog` 的正式内容快照。
- `BattleRuntimeModule` 在 `setup` 边界接收 typed 装备能力 catalog；战斗期间禁止目录扫描和 `ResourceLoader` 动态查找。
- UI、AI、headless 文本命令都必须走同一个 preview / commit 链；展示层不重算命中、伤害、条件或目标合法性。
- 任何跨战斗、短休、长休或永久代价，在保存 schema 和兼容策略确认前禁止实现为持久字段。

### 内容层

新增装备能力内容类型，推荐使用 `EquipmentPower` 命名，而不是局限为 `WeaponEffect`。该能力系统应可服务当前已有装备类型：武器、护甲、饰品。不要在装备规则扩展前引入 `relic` 这类当前 `ItemDef.equipment_type_id` 不支持的 source kind。

建议结构：

```text
ItemDef
  equipment_power_profile_id

EquipmentPowerProfileDef
  profile_id
  allowed_equipment_type_ids: weapon / armor / accessory
  powers: EquipmentPowerDef[]

EquipmentPowerDef
  power_id
  display_name
  triggers: EquipmentPowerTriggerDef[]
  conditions: EquipmentPowerConditionDef[]
  costs: EquipmentPowerCostDef[]
  actions: EquipmentPowerActionDef[]
  limits: EquipmentPowerLimitDef[]
  state_keys: EquipmentPowerStateKeyDef[]
  preview_tags: StringName[]
```

归属边界：

- `ItemDef` 只挂 `equipment_power_profile_id` 引用，不直接承载复杂战斗逻辑。
- `WeaponProfileDef` 不承载唯一装备能力；它仍是武器物理 profile 的真相源。
- `EquipmentPowerProfileDef` / `EquipmentPowerDef` 放在装备能力内容目录，由 `EquipmentPowerContentRegistry` 加载和校验。
- 唯一武器数据仍放在 `data/configs/items/`；能力配置单独放在类似 `data/configs/equipment_powers/weapons/swords/` 的目录。
- `ItemContentRegistry` 不负责扫描装备能力目录；它只解析 item/template。`equipment_power_profile_id` 的引用校验应在拥有 item catalog 和 equipment power catalog 的组合根或专门 validator 中完成。
- `ItemDef` 模板继承链若新增 `equipment_power_profile_id`，`ResolveWithTemplateChain` 必须显式复制/继承该字段，避免模板合并后引用丢失。
- `GameSession` 刷新装备能力内容后必须触发 `GameContentCatalog` 重建；`GameContentCatalog` 应暴露只读 typed equipment power profile 快照。

### 能力语言

装备能力语言需要覆盖本卷全部机制，且所有固定值都应由 C# enum、typed converter、typed DTO 或规则 utility 拥有，不能散落为公开字符串集合。

Godot 资源 schema 必须由具体 `[GlobalClass] Resource` 子类组成，例如：

```text
EquipmentPowerTriggerDef
EquipmentPowerConditionDef
EquipmentPowerActionDef
EquipmentPowerCostDef
EquipmentPowerLimitDef
EquipmentPowerStateKeyDef
```

这些资源可以在 Godot 边界导出 `int kind` 或 `StringName` 字段，但运行时必须先转成内部 enum / typed DTO。禁止使用自由 `Dictionary`、自由 action 字符串、运行时参数包作为正式业务 schema。若某个 action 表达伤害、状态、地形或装备耐久伤害，应优先复用受限的 `CombatEffectDef`，由装备能力 resolver 转成正式战斗 resolver 输入；`BattleAttackRollModifierSpec` 这类运行时 DTO 只能由 resolver 构造，不能直接作为 Godot Resource 导出字段。

Trigger 至少需要覆盖：

```text
BattleStart
TurnStart
TurnEnd
BeforeAttackRoll
AfterAttackRoll
OnHit
OnCrit
BeforeDamageApply
AfterDamageApply
OnKill
OnMoveCommitted
OnIncomingDamage
OnAllyDamagedNear
ActiveCommand
ReactionWindow
```

Condition 至少需要覆盖：

```text
TargetHasTag
TargetAlignment
SourceAlignment
TargetHpPercentBelow
SourceMovedDistanceAtLeast
SourceDidNotMoveThisTurn
BattleRoundAtLeast
EnvironmentTimeOfDay
EnvironmentWeather
TargetHasStatus
SourceHasState
EquippedTargetMaterialTag
TargetIsCurrentMarked
```

Action 至少需要覆盖：

```text
AddAttackRollModifier
AddAdvantage
AddDamageDice
AddFlatDamage
ApplyStatus
AddOrConsumeStack
Heal
Shield
ForcedMove
TerrainEffect
SummonUnit
ModifyIncomingDamage
DamageEquipmentDurability
ExecuteOrDeathRule
LogOnly
```

### 战斗运行时

新增 `BattleEquipmentPowerService`，由 `BattleRuntimeModule` 拥有。它是装备能力调度器，不替代现有伤害、状态、移动、地形、召唤、行动经济、AI 或展示系统。

推荐内部结构：

```text
BattleEquipmentPowerService
  BattleEquipmentPowerIndex
  BattleEquipmentPowerStateStore
  BattleEquipmentPowerResolver
  BattleEquipmentPowerPreviewBuilder
```

战斗开始时，服务从 `BattleRuntimeModule.setup` 注入的 typed item catalog 和 typed equipment power catalog 读取内容，扫描每个单位的 battle-local 装备 view，建立装备能力索引。战斗过程中，攻击、伤害、移动、回合、死亡等阶段显式调用该服务，服务返回 typed resolution，再交给正式 damage/status/terrain/summon/economy 服务执行。

不要使用全局 Godot signal 式事件总线。战斗编排应在固定阶段显式调用：

```text
BuildAttackModifiers(context) -> attack roll modifier specs
BeforeDamageResolve(context) -> extra combat effects / damage modifiers
AfterDamageApplied(context) -> equipment power resolution
OnKill(context) -> equipment power resolution
MoveCommitted(context) -> equipment power resolution
TurnStart(context) -> equipment power resolution
TurnEnd(context) -> equipment power resolution
```

主要接入点：

- 攻击命中修正：`BattleAttackCheckPolicyService`
- 命中、暴击、击杀、伤害后处理：`BattleSkillExecutionOrchestrator`
- 回合开始、回合结束、状态衰减：`BattleRuntimeSkillTurnResolver`
- 预览、命令提交、AI 候选：`BattleRuntimeModule` / `BattleSessionFacade`

每个 hook 必须声明 phase contract：

| Hook | Preview 调用 | Commit 是否可变更状态 | 顺序要求 |
| --- | --- | --- | --- |
| `BuildAttackModifiers` | 是 | 否 | 构建 modifier bundle 时、生成最终 `AttackCheckInput` 前 |
| `BeforeDamageResolve` | 可用于估算 | Commit 可返回额外 effect，但不直接改 HP | attack check 结果确定后、damage resolver 应用前 |
| `AfterDamageApplied` | 否 | 是 | damage/status/shield 已应用、死亡处理前 |
| `OnKill` | 否 | 是 | 目标降至 0 后、loot/writeback/最终 defeat 处理前 |
| `MoveCommitted` | 可用于预览路径收益 | 是 | 移动成功写入坐标后 |
| `TurnStart` | 否 | 是 | per-turn reset 后、单位可行动前 |
| `TurnEnd` | 否 | 是 | 行动结算后、timeline 推进前 |

装备能力 resolver 返回 typed `BattleEquipmentPowerResolution`，其中只包含状态变更请求、额外 `CombatEffectDef`、attack modifier specs、summon request、action economy cost、report/log facts 等 typed 结果；具体 HP、状态、地形、单位创建和 AP 消耗由对应正式 service 执行。

### 能力状态

本卷大量能力依赖运行时状态：正义烙印、誓约目标、情感撕裂、锈蚀层数、龙魂怒、海之记忆、星尘、乌鸦数量、教诲、上回合是否造成伤害、移动距离、闪现后的首次攻击等。

这些状态应放入 battle-local 的 typed sidecar，而不是全部塞进 `BattleUnitState.status_effects`。

推荐模型：

```text
BattleEquipmentPowerStateKey
  source_equipment_instance_id
  power_id
  state_kind
  target_unit_id

BattleEquipmentPowerStateValue
  stacks
  remaining_rounds
  used_this_turn
  used_this_battle
  linked_unit_id
  numeric_value
```

只有需要被通用状态系统、AI、HUD 或规则层统一识别的结果，才投影成正式 status，例如 `stunned`、`fear`、`slow`、`armor_broken`、`fragile`、`silenced`、`difficult_terrain`。

状态生命周期必须显式定义：

- 主 owner 是 battle-local equipment instance；`source_unit_id` 是当前持有者索引，不应作为唯一身份来源。
- 卸装时，所有要求“仍装备中”的 target-linked 和 pending 状态默认失效；per-battle 使用次数默认保留在该 battle-local equipment instance 上，防止换装重置次数。
- 装备换到其他单位时，默认不转移旧目标叠层；只有状态定义显式声明可转移时才允许。
- 源单位死亡时，默认清理该单位持有装备产生的 active states；召唤物或持续地形由对应 service 按 source policy 处理。
- 目标死亡或 despawn 时，清理包含该 `target_unit_id` 的状态。
- 召唤单位 despawn 时，`BattleSummonService` 必须通知装备能力 state store 清理 owner/source/target 索引。
- State store 必须支持 clone / simulation snapshot，并纳入 AI mutation guard；它不进入 battle save payload。
- Runtime hook 查找必须通过 source/target/trigger 索引，不能每个 hook 全量扫描所有装备能力状态。

### 事实查询

`Pale Justice`、`Wyrmbreak`、`Twilight's Edge`、`Black Sail`、`Bookburn` 等能力依赖目标事实和环境事实。需要新增统一查询层，例如 `BattleFactResolver`。

它应提供：

```text
GetCreatureTags(unit)
GetAlignment(unit)
GetRaceTags(unit)
GetEquipmentMaterialTags(unit)
GetEnvironmentFacts()
GetAwarenessFacts(source, target)
GetConcentrationFacts(unit)
```

事实 schema 需要正式 owner：

```text
BattleFactKind
BattleFactRequirementDef
BattleFactProvider
```

事实来源包括敌人模板 tags、玩家 race/subrace/bloodline、装备材质 tags、battle environment、当前 status、专注/施法状态等。`EquipmentPowerContentRegistry` 或组合根 validator 必须在加载期校验 ability 引用的 fact kind 是否有 provider、引用值是否在对应 catalog / enum / typed rule 中合法。运行时不能悄悄把缺失 fact 当作普通 false 导致装备能力被静默削弱；缺失 provider 应是内容校验错误。

### 行动经济

本卷使用 `action`、`bonus action`、`reaction`。当前战斗以 AP 为主，单位没有 standard / bonus / reaction 状态；因此不能在能力文档里直接宣称支持 bonus action 或 reaction，除非先落地正式行动经济层。

优秀架构应新增统一行动经济层，并让技能、换装、装备主动能力逐步收敛到同一扣费入口：

```text
BattleActionEconomyState
  standard_action_available
  bonus_action_available
  reaction_available
  reaction_window_id

BattleActionCostDef
  ap_cost
  consumes_standard_action
  consumes_bonus_action
  consumes_reaction
  ends_turn_when_ap_empty
```

行动经济状态是 battle runtime state，必须随 turn reset、clone、AI simulation、headless snapshot 和 mutation guard 一起维护。若某阶段只实现 AP，则文档里的 `action` / `bonus action` / `reaction` 必须映射为明确 AP cost 和 per-turn/per-battle limit，不能伪装成已支持 D&D 行动类型。

装备主动能力通过新增 `UseEquipmentPower` 命令进入 battle command 链。不要把装备主动能力伪装成角色永久学会的技能；可以复用技能 preview 的基础设施，但来源和生命周期必须仍然属于装备实例。

命令层 contract：

```text
BattleCommandKind.UseEquipmentPower

BattleCommand
  equipment_power_id
  equipment_power_source_instance_id
  equipment_power_source_slot_id
  target_unit_ids / target_coords
```

`PreviewCommand` 和 `IssueCommand` 必须增加与 skill / change equipment 同级的分支。提交前应使用共同 preview gate，避免 skill 有合法性预览阻断而装备能力绕过。UI 选择态、headless text command、AI command fingerprint、payload guard、trace DTO 都必须识别 `UseEquipmentPower`。

### 召唤与临时单位

`Frostmourne` 和 `Ravenplume` 需要正式召唤子系统，不能只做成状态文本。

建议新增 `BattleSummonService` 和 `BattleSummonProfileDef`：

```text
BattleSummonProfileDef
  unit_template
  faction_policy
  controller_policy
  duration
  max_count
  occupies_cell
  can_be_targeted
  grants_loot
  timeline_policy
  ai_brain_id
```

召唤物必须通过统一工厂创建 `BattleUnitState`，分配 battle-local unit id、faction、control mode、AI brain、出生坐标和 body footprint，并接入 grid occupancy、timeline / initiative、AI action plan rebuild、death/despawn 清理。默认 policy 应为 `non_writeback`、`non_loot`、`non_progression`，除非 summon profile 明确声明其他行为。乌鸦若要符合本卷描述，应能被攻击；若后续做成光环计数器，需要在具体实现文档中标明与原设计的偏差。

### 持久化与休息

装备能力状态需要分级：

```text
BattleScope      战斗内状态，进入 BattleEquipmentPowerStateStore
EncounterScope   战后清空，但可影响本场结算
RestScope        短休/长休刷新
PermanentScope   永久代价或永久标记
```

本卷中以下机制不是 battle-only 状态：

- `Pale Justice` 的同一目标每长休限制。
- `Frostmourne` 的被献祭仆从长休前无法再次召唤。
- `Bloodvine` 的下一场战斗伤害惩罚。
- `Threadweaver` 的永久最大 HP 损失。

这些会触碰 party/member/equipment persistent state 或休息系统，属于存档 schema 风险。未确认保存格式和兼容策略前，不应实现为持久化字段；若实现，需要按仓库兼容策略先确认是否接受新 schema，以及旧存档缺字段时的处理方式。

推荐持久化 owner 边界：

- `BattleScope`：只进入 `BattleEquipmentPowerStateStore`。
- `EncounterScope`：可进入战斗结算结果，但战后清空。
- `RestScope`：新增 typed persistent owner，例如 `PartyEquipmentPowerState`，key 默认为 `equipment_instance_id + power_id`；短休/长休 reset 必须接入正式休息流程。
- `PermanentScope`：优先落到角色/队伍已有 typed 状态 owner；例如最大 HP 永久损失不应藏在装备实例里，而应成为成员状态或成长惩罚的一部分。

### 预览、AI 与自动化

装备能力必须通过同一套 preview/commit 链服务玩家 UI、AI 和 headless 文本命令。展示层不应自行重算命中、伤害、条件或目标合法性。

建议 preview DTO：

```text
BattleEquipmentPowerPreview
  legal
  cost
  target_score_facts
  damage_preview
  status_preview
  summon_preview
  state_delta_preview
  risk_preview
```

AI 应把 `UseEquipmentPower` 当作正式候选行动，并能读取装备能力造成的伤害、控制、召唤、风险和状态收益。

推荐 AI contract：

```text
BattleAiActionKind.EquipmentPower
BattleAiEquipmentPowerCandidateProvider
BattleAiEquipmentPowerScoreInput
BattleAiEquipmentPowerTraceFact
```

AI action assembler / candidate evaluator / score input / trace export / mutation guard 都必须识别装备能力候选。Headless 应支持文本命令触发装备能力，并能在快照中显示关键装备能力状态。

### 实现分期与代码门槛

本卷不能直接进入“完整战斗行为”实现。实现必须按 code contract 分期推进，每一阶段只能使用已经正式接入的 owner，不允许用 `item_id` 分支、运行时目录扫描、临时 `Dictionary` 参数包或 UI 直调 resolver 绕过 typed 链。

#### Phase 0：内容与 catalog 基础层

允许先实现无战斗行为的基础切片：

- `ItemDef.equipment_power_profile_id`
- `EquipmentPowerContentRegistry`
- `GameSession` 刷新 / getter / validation domain
- `GameContentCatalog` typed snapshot / getter / revision 覆盖
- `GameRuntimeFacade -> BattleRuntimeModule.setup` typed catalog 注入
- `ItemDef` 模板合并中的 `equipment_power_profile_id` 继承 / 覆盖
- item -> equipment power profile 交叉校验

Phase 0 不允许解析、预览或执行任何装备能力。若只完成 Phase 0，唯一可验收行为是：资源能被加载、校验、catalog 缓存、runtime setup 接收，并且非法引用能在内容验证中失败。

Phase 0 必测：

```text
resource validation:
  duplicate equipment power profile id
  item references missing power profile
  non-equipment item references power profile
  equipment type not allowed by power profile
  template inherited power profile id
  item override power profile id

catalog/runtime:
  GameContentCatalog caches equipment power profiles
  catalog revision changes after equipment power refresh
  GameRuntimeFacade passes typed equipment power catalog to BattleRuntimeModule
  BattleRuntimeModule does not scan resource directories at battle time
```

#### Phase 1：命令、preview 和 runtime sidecar

主动装备能力进入 runtime 前，必须先补齐命令链和状态链：

- `BattleCommandKind.UseEquipmentPower`
- `BattleCommand.equipment_power_id`
- `BattleCommand.equipment_power_source_instance_id`
- `BattleCommand.equipment_power_source_slot_id`
- `BattleRuntimeModule.PreviewCommand(...)` 同级分支
- `BattleRuntimeModule.IssueCommand(...)` 同级分支
- 与 skill / change equipment 共享的 preview gate
- `BattleEquipmentPowerStateStore` clone / snapshot / AI mutation guard
- 换装、死亡、despawn 触发 state cleanup
- `BattlePreview` 中装备能力 preview 的 typed DTO
- `BattleSessionFacade` / `GameRuntimeFacade` / headless / HUD 的 command surface

Phase 1 可以只支持“合法性检查 + 空效果命令 + 状态读写测试”，仍不需要实现伤害或召唤。

Phase 1 必测：

```text
command:
  preview rejects missing source instance
  preview rejects unequipped source instance
  issue cannot bypass failed preview
  AP insufficient command fails without mutation
  UI/headless command builds the same BattleCommand fields

state:
  state store clones for AI simulation
  AI mutation guard catches sidecar mutation
  changing equipment clears still-equipped target-linked state
  per-battle usage remains on battle-local equipment instance
  target death clears target-linked state
```

#### Phase 2：攻击、伤害和叠层能力

命中、伤害、暴击、击杀触发必须接入正式 phase hook，且 preview 与 commit 使用同一 typed 解析规则。

必须补齐：

- `BattleEquipmentPowerResolution`
- attack modifier hook 接入 `BattleAttackCheckPolicyService`
- before damage hook 输出额外 `CombatEffectDef` / damage modifier
- after damage hook 处理 on-hit / on-crit / state delta
- on-kill hook 在 defeat/writeback/loot 前执行
- `CombatEffectDef` 校验抽出可复用 `CombatEffectContentValidator`
- 装备能力 action 使用受限 `CombatEffectDef` 时必须通过同一校验器

Phase 2 首批只应覆盖不依赖召唤、环境、专注或持久化的能力族，例如：命中叠层、按目标 HP 条件命中修正、暴击额外伤害、每战一次主动消耗层数。

Phase 2 必测：

```text
attack:
  hit preview and commit use same equipment modifier
  miss does not trigger on-hit stack
  crit triggers crit-only effect once
  low-HP condition updates hit preview

damage:
  extra dice preview matches commit range
  add_weapon_dice behavior is not duplicated accidentally
  equipment-generated CombatEffectDef passes shared validator

ordering:
  after-damage state applies before on-kill cleanup
  on-kill summon/resource request runs before final defeat cleanup
```

#### Phase 3：事实、召唤、行动经济和持久化

以下能力族必须等对应基础设施完成后才能实现：

- 环境 / 阵营 / 材质 / 专注 / 察觉类事实条件
- summon / familiar / crow / undead minion
- `action` / `bonus action` / `reaction`
- RestScope / PermanentScope / 下一场战斗惩罚

如果不先实现完整行动经济，则所有 `action` / `bonus action` / `reaction` 必须在能力内容中显式映射为：

```text
ap_cost
trigger_window
per_turn_limit
per_battle_limit
ends_turn_when_ap_empty
```

不能把 bonus action 或 reaction 解释成免费能力。

RestScope / PermanentScope 的保存格式必须在实现前确认。未确认前，这类效果只能作为未实现能力保留在文档中，不能落为临时 battle state，也不能写进 `EquipmentInstanceState`、`EquipmentState` 或 `WarehouseState`。

### 用例设计与测试分层

装备能力不是单个战斗特效，应按内容、catalog、runtime setup、命令、状态、规则 hook、AI 和文本自动化分层验收。每层测试只验证自己拥有的 contract，不用一个大场景覆盖所有行为；跨层测试只用于证明正式接线存在。

测试分层：

| 层级 | 推荐位置 | 主要断言 |
| --- | --- | --- |
| 内容 schema / registry | `tests/runtime/validation/` + `tests/fixtures/resource_validation/equipment_power_*` | `.tres` 能加载为 typed Resource；非法 trigger / condition / action / cost / limit / state scope 被拒绝；错误信息包含 `profile_id`、`power_id`、action/condition index 和资源路径 |
| session / headless validation surface | `GameSession` validation snapshot + `tests/text_runtime/commands/` | official validation 输出必须包含 `equipment_power` domain；`InstallTestContentDef("equipment_power", ...)` 可注入测试 profile；headless/text validation 能看到同一错误域 |
| item 交叉校验 | `tests/runtime/validation/` | `ItemDef.equipment_power_profile_id` 只允许装备引用；缺失 profile、装备类型不匹配、模板继承遗漏、实例覆盖都能被定位到具体 `item_id` |
| catalog / session refresh | `tests/runtime/validation/run_game_root_content_catalog_regression.cs` 或同域新 runner | `GameContentCatalog` 缓存装备能力 profile；刷新后 revision 自增；只读视图不可 downcast 修改；session dispose 后旧 catalog 失效 |
| runtime setup contract | `tests/runtime/facade/` 或 `tests/battle_runtime/runtime/` | `GameRuntimeFacade -> BattleRuntimeModule.setup` 注入 typed equipment power catalog；`BattleRuntimeModule` 战斗期间不扫描目录、不调用 `ResourceLoader` 查能力 |
| command / preview gate | `tests/battle_runtime/runtime/` | `PreviewCommand` 与 `IssueCommand` 同源；非法 preview 不能被 issue 绕过；AP/cost/source/target 失败不产生任何 state delta |
| state store / lifecycle | `tests/battle_runtime/runtime/` | sidecar 支持 clone/snapshot；AI mutation guard 覆盖；换装、源死亡、目标死亡、despawn、目标切换和 per-battle limit 生命周期稳定 |
| rules hook | `tests/battle_runtime/rules/` | hit preview 与 commit 使用同一 modifier/effect resolver；miss、crit、on-hit、on-kill、cleanup 顺序分别有 focused case |
| AI candidate / trace | `tests/battle_runtime/ai/` | `UseEquipmentPower` 进入候选、评分、payload guard、trace fingerprint；AI 预演不能污染 runtime state |
| headless / text command / snapshot | `tests/text_runtime/commands/` + `tests/text_runtime/headless/` | 文本命令构造的 `BattleCommand` 字段与 UI/facade surface 一致；快照只展示只读摘要，不作为业务状态来源 |
| save / rest / permanent | `tests/runtime/persistence/`，仅在 schema 确认后 | RestScope / PermanentScope 有明确 persistent owner、reset 流程和旧存档策略；未确认前测试应断言这类 scope 被内容校验拒绝 |

fixture 策略：

- Phase 0 使用 probe fixtures，不把半成品 `swords_01` 正式内容塞进 `data/configs`。推荐 fixture 根沿用 resource validation 模式：`tests/fixtures/resource_validation/equipment_power_valid/`、`tests/fixtures/resource_validation/equipment_power_invalid/`、`tests/fixtures/resource_validation/equipment_power_cross_reference/`。
- Phase 0 的 official content run 要么没有正式装备能力引用，要么只允许完整闭环的 profile；不能把未实现的本卷能力当作 official validation 的一部分。
- 每个 invalid fixture 只表达一个失败原因；测试断言 domain、错误数量和关键错误文本，避免一个坏资源同时触发十几个无关错误。
- 内容校验错误必须能定位到 `profile_id` / `power_id` / action 或 condition index / `item_id` / `base_item_id` / 资源路径。缺少这些定位信息的校验即使能失败，也不算可维护。
- 装备能力 validation 应提供稳定 error code，例如 `duplicate_profile_id`、`missing_profile`、`invalid_allowed_equipment_type`、`invalid_action_cost`、`rest_scope_without_persistent_owner`。测试优先断 error code，再断必要上下文文本。
- 测试数据不要依赖正式内容顺序；新增测试用 item/profile id 使用独立 probe 前缀，避免与官方资源或其他测试共享 mutable state。
- 对 preview、AI 预演、非法命令、cost 不足、缺失 source instance、未装备 source slot 等负面用例，必须保存 before/after typed snapshot，并断言 HP、AP、grid、sidecar、equipment view、event batch 都没有变化。
- 常规装备能力回归不使用 battle simulation、balance simulation 或 benchmark runner。模拟器只用于后续数值平衡，不作为 schema、命令或 hook contract 的验收入口。
- UI 视觉或截图只在 HUD 真正接入装备能力 preview 后补；Phase 0 不需要视觉测试。
- Phase 1 以后应提供小型 battle fixture factory，固定生成已装备实例、重复实例、低 AP、低 HP、可死亡目标、必命中/必 miss/必 crit roll，避免每个测试脚本手搓长场景。

阶段用例门槛：

| 阶段 | 必须新增或扩展的用例 |
| --- | --- |
| Phase 0 | registry 接受最小合法 profile；拒绝 duplicate profile id、missing `power_id`、未知 trigger/action enum、非法 RestScope/PermanentScope；official validation 输出 `equipment_power` domain 且零错误；`InstallTestContentDef("equipment_power", ...)` 后 catalog revision 变化且 getter 可读；invalid 注入后 `RefreshContentValidationSnapshot` 失败；item 交叉校验覆盖 missing profile、非装备 item、装备类型不允许、模板继承、实例覆盖；catalog 刷新、非 live-forward、只读视图、revision、dispose 失效；runtime setup 注入 typed catalog 且不扫描目录 |
| Phase 1 | `UseEquipmentPower` command value-object 字段、enum/StringName round-trip、payload guard、preview/issue gate、source instance 与 slot 绑定、重复同名装备实例、目标合法性、AP/cost 不足无 mutation；直接调用 `IssueCommand` 也不能绕过失败 preview；空效果 commit 只消耗合法 cost 并产生 typed report；state store deep clone、AI mutation guard restore、换装/死亡/despawn cleanup、per-battle usage 不因换装重置；text command、facade/UI、AI candidate 组装字段一致 |
| Phase 2 | `BuildAttackModifiers`、`BeforeDamageResolve`、`AfterDamageApplied`、`OnKill` 各有 focused regression；固定 roll/RNG 下同一 probe power 在 preview 与 commit 中产出同一 attack modifier breakdown 和 damage/effect breakdown；miss 不叠层、crit-only 只触发一次、on-hit state 在 damage 后写入、on-kill 在最终 defeat cleanup 前执行、死亡单位不能继续获得 pending hook；`add_weapon_dice` 在 report 中只计一次；装备生成的 `CombatEffectDef` 必须走共享 validator |
| Phase 3 | fact resolver 对缺失 provider fail fast；summon 创建接入 unit factory、grid、timeline、AI brain、despawn cleanup；行动经济 reset/消耗/反应窗口进入 clone 与 mutation guard；Phase 0-2 save round-trip 不得出现 `equipment_power` persistent key；RestScope/PermanentScope 只有在 save schema 和兼容策略确认后才允许通过内容校验 |

用例命名应表达 owner 和 contract，例如 `run_equipment_power_content_registry_regression.cs`、`run_equipment_power_catalog_regression.cs`、`run_battle_equipment_power_command_regression.cs`、`run_battle_equipment_power_state_store_regression.cs`、`run_battle_equipment_power_attack_hook_regression.cs`、`run_battle_ai_equipment_power_candidate_regression.cs`、`run_battle_equipment_power_text_command_regression.cs`。若扩展现有 runner，必须保持单个测试方法名能指出失败 contract；不要把 Phase 0 内容校验、Phase 2 伤害 hook 和 Phase 3 召唤行为塞进同一个长脚本。

关键用例细化：

- `GameSession` 必须把 `equipment_power` 加入 validation domain order、content validation snapshot、headless/text validation surface 和 test content injection surface。测试应覆盖：official snapshot 包含该 domain；invalid profile 注入后 domain 失败；错误携带 `profile_id`、`power_id`、action index；成功注入后 catalog getter 能读到 profile。
- `GameContentCatalog` 测试不能只看 revision。必须证明 registry 变更在 refresh 前不会 live-forward 到 catalog；refresh 后 revision 增加且新 profile 可见；持有的旧只读 snapshot 不会被 registry 后续修改污染；session/root dispose 后旧 catalog 清空或失效；runtime setup 后可通过测试快照确认 `BattleRuntimeModule` 收到同一 profile。
- 运行时扫描禁令要有代码级回归：装备能力战斗 runtime owner 不允许出现 `ResourceLoader`、`DirAccess` 或目录路径扫描。能力内容只能来自 `BattleRuntimeModule.setup` 注入的 typed catalog。
- `ItemDef` 模板测试必须覆盖 grandparent -> parent -> item 多层继承、item 非空 override、空值继承模板、missing template 不被全局 template cache 污染。若未来需要“显式清空 inherited profile”，必须新增独立清空字段或策略并单独测试，不能让空 `StringName` 悄悄改变继承语义。
- `UseEquipmentPower` 身份测试必须使用两个相同 `item_id`、不同 `instance_id` 的装备：只装备 A 时引用背包 B 必须失败；`source_slot_id` 与实际槽位不符必须失败；`power_id` 不属于该实例 profile 必须失败；换装成同名新实例后旧 sidecar 不得迁移。
- UI、headless、AI parity 不能只断“都能执行”。测试必须比较三条入口生成的 `BattleCommand` 字段：`unit_id`、`equipment_power_id`、`equipment_power_source_instance_id`、`equipment_power_source_slot_id`、`target_unit_ids`、`target_coords`。AI fingerprint / trace 也必须包含这些字段，payload guard 必须拒绝 Resource、live `EquipmentInstanceState` 或可变 sidecar 混入 command/preview/score input。
- sidecar mutation guard 测试必须故意在 AI candidate/scoring 阶段写入装备能力状态，断言 guard fail-fast、恢复原状态，并在错误路径中包含 `equipment_power_state_store`、`source_instance_id`、`power_id`、state key。clone 测试必须证明 clone 深拷贝，修改 clone 不影响原 battle。
- `CombatEffectDef` 共享 validator 要做等价测试：同一个 invalid effect 放在 skill 和 equipment power 下都失败，error category 一致；equipment power 的错误 owner 必须是 `profile_id/power_id/actions[index]`，不能沿用 skill 文案；legacy params、非法 `damage_tag`、非法 status/save 字段在装备能力下同样拒绝。

### 字段级 schema 门槛

开工实现装备能力内容前，必须先为下列 Resource / DTO 写字段表和校验规则；没有字段表时，不允许新增 `.tres` 能力资源。

| 类型 | 必填字段 | 关键校验 |
| --- | --- | --- |
| `EquipmentPowerTriggerDef` | `kind`, `phase`, `target_policy` | `kind` 必须可转 enum；phase 必须对应正式 hook；preview-only trigger 不得产生 mutation |
| `EquipmentPowerConditionDef` | `kind`, `subject`, `comparator`, `expected_value` | fact 条件必须引用有效 `BattleFactKind` 和 provider；target/source/self 语义必须唯一 |
| `EquipmentPowerActionDef` | `kind`, `target_policy`, `order` | 伤害/状态/地形优先封装为受限 `CombatEffectDef`；禁止自由 action string 和 params dictionary |
| `EquipmentPowerCostDef` | `kind`, `amount`, `scope` | AP/HP/stack/state cost 必须在 preview 与 commit 使用同一 resolver；失败不得部分扣费 |
| `EquipmentPowerLimitDef` | `scope`, `max_uses`, `reset_policy` | `RestScope` / `PermanentScope` 必须有 persistent owner；未确认 save schema 时非法 |
| `EquipmentPowerStateKeyDef` | `state_kind`, `owner_policy`, `target_policy`, `clear_policy` | 必须声明换装、死亡、despawn、目标切换和回合结束时的生命周期 |
| `BattleEquipmentPowerResolution` | `state_deltas`, `combat_effects`, `attack_modifiers`, `summon_requests`, `costs`, `reports` | resolution 本身不直接改 HP、AP、grid 或 unit collection；只向正式 service 提交 typed request |

### Hook 测试矩阵

每个 hook 落地时都要有 focused headless 测试，覆盖 preview / commit 一致性和 mutation 边界：

| Hook | 最小测试 |
| --- | --- |
| `BuildAttackModifiers` | 装备命中修正出现在 hit preview 和实际 attack check；AI 预览不污染 state |
| `BeforeDamageResolve` | 装备额外伤害骰进入 damage preview 和 commit；未命中不结算 |
| `AfterDamageApplied` | 命中叠层只在造成命中结果后增加；同目标/换目标 lifecycle 正确 |
| `OnKill` | 击杀触发发生在 defeat cleanup 前；目标死亡后 target-linked state 清理 |
| `MoveCommitted` | 移动距离状态只在成功移动后更新；移动预览不写 state |
| `TurnStart` | per-turn limit reset 与状态 tick 顺序稳定 |
| `TurnEnd` | 未造成伤害、未移动、持续回合衰减等状态只在行动结算后更新 |

### 本卷机制到系统能力的映射

| 机制类型 | 本卷示例 | 需要的系统能力 |
| --- | --- | --- |
| 条件命中加成 | `Pale Justice`、`Heartbane`、`Twilight's Edge` | attack roll modifier + fact resolver |
| 命中叠层 | `Oathscar`、`Rustoath`、`Starfell`、`Saltpillar`、`Threadweaver` | target-linked stack state |
| 层数消耗主动能力 | `Oathscar`、`Rustoath`、`Black Sail`、`Bookburn`、`Starfell` | `UseEquipmentPower` command + state cost |
| 暴击/击杀触发 | `Heartbane`、`Frostmourne`、`Gravelight`、`Ravenplume` | OnCrit / OnKill trigger |
| 环境依赖 | `Twilight's Edge`、`Black Sail` | battle environment facts |
| 移动依赖 | `Windwhisper`、`Nameless` | movement tracking + turn state |
| 反应/护卫 | `Mother's Blade` | reaction window + ally damaged hook |
| 召唤/随从 | `Frostmourne`、`Ravenplume` | summon service + temporary unit lifecycle |
| 专注/法术干扰 | `Bookburn` | concentration/spell facts and status rules |
| 死亡与复活规则 | `Heartbane`、`Gravelight`、`Threadweaver` | execute/death rule result |
| 跨战斗/长休/永久代价 | `Bloodvine`、`Frostmourne`、`Threadweaver` | persistent equipment/member state policy |

### 验收口径

当本卷正式落地时，应能满足以下条件：

- 每把剑的静态武器数据仍由 `ItemDef.weapon_profile` 和基础 weapon template 承载。
- 每把剑的特殊性质由 `EquipmentPowerProfileDef` 配置，不需要按 `item_id` 写运行时分支。
- 内容校验能拒绝未知 trigger、condition、action、damage tag、status id、fact id、summon profile 和非法 state scope。
- 战斗中所有装备能力状态都是 typed runtime state；只有明确需要跨战斗的状态才进入保存模型。
- UI、AI、headless 文本命令共享同一个 preview/commit 入口。
- 召唤、行动消耗、状态、伤害、地形和死亡规则分别交给已有或新增的正式 service，不由装备能力服务直接改写多个 owner 的内部状态。
- 实现落地时必须更新 `docs/design/project_context_units.md` 的读集和职责边界，至少覆盖 CU-02、CU-10、CU-15、CU-16、CU-21；在相关代码文件实际存在前，不要把不存在的 planned 文件提前写进 context units。

### 1. 苍白的正义（Pale Justice）
- **item_id**: `weapon_unique_sword_pale_justice_001`
- **display_name**: `苍白的正义`
- **base_item_id**: `weapon_type_longsword_base`
- **family**: `sword`
- **training_group**: `martial`
- **range_type**: `melee`
- **damage_tag**: `physical_slash`
- **attack_range**: `1`
- **one_handed_dice**: `1D8+3`
- **two_handed_dice**: `1D10+3`
- **properties**: `[versatile]`
- **base_price**: `85000`
- **attribute_modifiers**: `[真视之刃: 对邪恶生物攻击检定+2，命中额外1D6 radiant; 正义烙印: 命中邪恶生物叠加1层，最多5层，每层使其对你伤害-1; 黎明裁决: 消耗5层正义烙印，造成3D6 radiant范围伤害并恐惧; 誓约反噬: 主动攻击非邪恶目标时本回合失去烙印加成并受2D6 psychic]`

#### 外观描述
这把长剑的剑身呈现出一种病态的灰白色，仿佛被抽干了所有生气。没有华丽的纹饰，没有宝石镶嵌，剑柄只是用最普通的皮革缠绕，握在手中甚至有些割手。剑刃上布满细密的划痕，那是无数次劈砍留下的痕迹，却出奇地没有卷刃。最奇特的是，当持有者的内心真正坚定于某个正义信念时，剑身会泛起极淡的、几乎无法察觉的白色微光——不是辉煌的神圣光辉，而是像黎明前最黑暗时刻天边那一缕倔强的鱼肚白。剑鞘由一块无名的灰色岩石凿成，沉重、笨拙，背在身后如同背负着一座小山。

#### 历史渊源
"苍白的正义"诞生于一个被遗忘的时代，锻造者不是矮人工匠，不是精灵法师，而是一位无名的人类老农。在那个诸神尚未划定善恶界限的年代，老农的村庄被一支自诩"神圣"的远征军洗劫，理由仅仅是村民不愿交出最后的存粮供奉神庙。老农用犁铧的碎片、田垄里的石块和仇恨锻成了这把剑——没有祝福，没有仪式，只有一个老人对"何为真正正义"的质问。

剑成之日，老农持它冲入远征军营帐，斩杀了十二名骑士，然后在敌人的包围中力竭而死。他没有留下名字，但留下了这把剑。此后千年，"苍白的正义"辗转于无数持有者之手，它从不选择英雄，只选择那些在绝境中依然能分辨对错的人。曾有暴君得到它，剑身在他手中锈蚀成废铁；曾有乞丐握着它，斩杀了吞噬城市的恶龙。最著名的一次，是一位盲眼修女在围城之战中持此剑站在城门之前，独自面对三千敌军。她没有挥出一剑，只是静静站立，敌军却在黎明到来前悄然退去——后来降兵的供词说，他们看见她身后站着无数模糊的灰色影子，像一片沉默的麦田。

#### 特殊性质
- **真视之刃**：对 alignment 为邪恶的生物，攻击检定获得 +2 加值，命中时额外造成 1D6 radiant 伤害；若持有者 alignment 偏离善良，此效果失效且武器伤害降低为 1D6/1D8
- **正义烙印**：命中邪恶生物时叠加 1 层"正义烙印"（最多 5 层），每层使该生物对你造成的伤害降低 1 点；烙印仅对被你当前回合攻击过的目标生效，切换目标则旧目标烙印立即消失
- **黎明裁决**：当同一邪恶生物身上叠加至 5 层正义烙印时，可消耗全部烙印作为一次 action，对 10 尺半径内所有邪恶生物造成 3D6 radiant 伤害，失败则恐惧 1 回合；同一目标每长休限一次
- **誓约反噬**：若持有者主动攻击一个非邪恶生物，本回合内失去所有正义烙印加成，并受到 2D6 psychic 伤害（误伤无辜的良心惩罚）

---

### 2. 霜之哀伤（Frostmourne）
- **item_id**: `weapon_unique_sword_frostmourne_002`
- **display_name**: `霜之哀伤`
- **base_item_id**: `weapon_type_greatsword_base`
- **family**: `sword`
- **training_group**: `martial`
- **range_type**: `melee`
- **damage_tag**: `physical_slash`
- **attack_range**: `1`
- **two_handed_dice**: `2D6+5`
- **properties**: `[two_handed, heavy]`
- **base_price**: `180000`
- **attribute_modifiers**: `[霜魂吞噬: 击杀敌人时召唤冰霜亡灵，HP为击杀目标最大HP的25%; 凛冬领域: 每个仆从存在时10尺内敌方每回合开始受1D4寒冷; 亡者之握: 仆从可对进入其威胁范围的敌人进行借机攻击，命中则减速至下回合; 冰封献祭: 主动献祭一个仆从，回复其剩余HP的50%并对5尺内敌人造成等量寒冷伤害]`

#### 外观描述
一柄符文巨剑，剑身长达五尺，由一块坠落于永冻冰川深处的陨铁锻造而成。剑身呈现出深邃的幽蓝色，内部仿佛有暴风雪在永不停歇地旋转。无数如尼符文蚀刻在剑脊两侧，那些符文并非静止，而是像冬眠的蛇一样缓慢蠕动，当剑饮血时，符文的蓝光会骤然明亮，如同濒死者的最后一声尖叫。剑格设计成扭曲的龙爪形状，中央镶嵌着一块从不融化的冰晶——那不是普通的冰，而是第一位锻造者冻结的眼泪。剑柄缠绕着某种白色生物的筋腱，触感冰冷刺骨，握久了会在掌心留下霜冻的痕迹。整把剑散发着肉眼可见的寒气，周围空气的温度会降低十度以上，呼出的气息瞬间凝成白雾。

#### 历史渊源
霜之哀伤不是被"锻造"出来的，它是被"诅咒"出来的。

在极北之地的纳克萨玛斯冰川深处，居住着最后一位符文锻造大师——矮人穆拉丁·霜锤。他毕生追求铸造一把能够"终结一切战争"的武器。为了获得足够的材料，他深入冰川核心，在那里发现了一块散发着诡异蓝光的陨石。那不是星辰的碎片，而是某位陨落冰神的指甲。穆拉丁用了三十年的时间，以自己的右眼、左手的三根手指和全部学徒的生命为代价，将这块陨铁锻造成剑。

然而剑成之夜，穆拉丁发现了一件恐怖的事：这把剑拥有意志。它渴望灵魂，如同沙漠渴望雨水。穆拉丁试图毁掉它，却在举锤的瞬间被剑中涌出的寒冰冻成了冰雕，至今仍然矗立在纳克萨玛斯的锻造炉旁，保持着举锤的姿势，眼中凝固着恐惧。

此后，霜之哀伤被一位北地王子获得。王子用它拯救了自己的王国免于瘟疫，却在胜利当天屠杀了全城百姓——因为剑告诉他，"真正的和平是寂静"。王子成为了第一代"巫妖王"，坐在冰封王座上千年，直到另一位持剑者将他击败。但击败者接过霜之哀伤的那一刻，王座上的冰开始重新生长。现在，霜之哀伤已经历了七任主人，每一任都是英雄，每一任都变成了魔王。它现在在某处等待第八任——或者说，下一位牺牲品。

#### 特殊性质
- **霜魂吞噬**：击杀一个生物时，召唤一个冰霜亡灵仆从（HP = 击杀目标最大 HP 的 25%，最低 10），持续至战斗结束或仆从被摧毁；同时存在仆从上限为 3 个
- **凛冬领域**：每个仆从存在时，其周围 10 尺内的敌方生物每回合开始时受到 1D4 寒冷伤害；多个仆从光环不叠加，取最高值
- **亡者之握**：每个仆从在其威胁范围内可对敌人进行借机攻击，命中时目标移动力减半（最低 5 尺），持续至其下回合开始
- **冰封献祭**：作为一个 bonus action，可主动献祭一个仆从，回复其剩余 HP 50% 的生命值，并对 5 尺内所有敌人造成等同于回复量的寒冷伤害；被献祭的仆从无法再次召唤，直至完成一次长休

---

### 3. 誓约之痕（Oathscar）
- **item_id**: `weapon_unique_sword_oathscar_003`
- **display_name**: `誓约之痕`
- **base_item_id**: `weapon_type_longsword_base`
- **family**: `sword`
- **training_group**: `martial`
- **range_type**: `melee`
- **damage_tag**: `physical_slash`
- **attack_range**: `1`
- **one_handed_dice**: `1D8+4`
- **two_handed_dice**: `1D10+4`
- **properties**: `[versatile]`
- **base_price**: `65000`
- **attribute_modifiers**: `[誓约绑定: 指定一个敌人作为誓言目标，对其伤害+1D6 radiant; 誓约之印: 命中誓言目标叠加1层，最多4层，每层使其AC-1; 誓约裁决: 消耗4层，本次攻击额外4D6 radiant并震慑目标1回合; 背誓反噬: 攻击非誓言目标时失去所有层数并受2D6 psychic]`

#### 外观描述
一把看似普通的长剑，精钢剑身，十字剑格，皮质剑柄。唯一的不寻常之处在于剑身中央有一道贯穿始终的裂痕——不是锻造缺陷，而是一道极细、极直的缝隙，从中透出微弱的金色光芒。那光芒不是恒定的，而是像呼吸一样明灭，当持有者立誓时，光芒会变得炽烈；当誓言被违背时，光芒会化为血红。剑鞘内侧刻满了密密麻麻的小字，那是历任持有者立下的誓言，有些已经实现，有些已经破碎，字迹从金色褪成黑色，如同结痂的伤疤。

#### 历史渊源
誓约之痕最初属于一位名叫塞拉斯的流浪骑士。塞拉斯并非贵族出身，他原本是一名刽子手，专门处决违背誓约的骑士。某个雨夜，他处决了自己的挚友——挚友违背了保护领主的誓言，在战斗中火速撤离。行刑后，塞拉斯在刑场上捡到一把被遗弃的长剑，剑身上已经有一道裂痕，那是被挚友的头盔磕出的缺口。

塞拉斯带着这把剑开始了赎罪之旅。他向每一个需要帮助的人立下誓言：保护商队、护送孤儿、守卫村庄。每实现一个誓言，剑身上的裂痕就愈合一分，光芒就明亮一分。但每有一个誓言未能实现——无论是因为能力不足还是命运捉弄——裂痕就会加深，光芒就会黯淡。二十年后，塞拉斯死在一个他誓言守卫的村庄门前，身上插着十七支箭，仍然保持着持剑站立的姿势。他的最后一道誓言是"此剑将永远守护誓言"，剑身因此永远留下了那道金色的裂痕——既不是完全愈合，也不是彻底破碎。

此后，誓约之痕只在立誓者手中发光。曾有佣兵得到它，因为从未立过任何誓言，剑身始终黯淡如凡铁；曾有君主得到它，因为违背了太多加冕誓言，在一次刺杀中剑身突然断裂，君主当场毙命。

#### 特殊性质
- **誓约绑定**：作为一个 bonus action，可指定视野内一个敌人作为"誓言目标"；对其造成的伤害额外增加 1D6 radiant；仅可同时存在一个誓言目标
- **誓约之印**：命中誓言目标时叠加 1 层"誓约之印"（最多 4 层），每层使该目标对你的 AC 降低 1 点；切换誓言目标时旧层数全部消失
- **誓约裁决**：当誓言目标叠加至 4 层誓约之印时，可消耗全部层数，使下一次命中它的攻击额外造成 4D6 radiant 伤害，并使其震慑 1 回合；每场战斗限一次
- **背誓反噬**：若持有者主动攻击非誓言目标，所有誓约之印立即消失，且持有者受到 2D6 psychic 伤害（违背誓言的惩罚）

---

### 4. 噬心者（Heartbane）
- **item_id**: `weapon_unique_sword_heartbane_004`
- **display_name**: `噬心者`
- **base_item_id**: `weapon_type_rapier_base`
- **family**: `sword`
- **training_group**: `martial`
- **range_type**: `melee`
- **damage_tag**: `physical_pierce`
- **attack_range**: `1`
- **one_handed_dice**: `1D8+3`
- **properties**: `[finesse]`
- **base_price**: `80000`
- **attribute_modifiers**: `[心碎之刺: 暴击额外造成目标已损失HP12%的psychic伤害; 情感撕裂: 连续命中同一目标3次后其对你攻击检定-2; 心脏渴求: 目标HP≤30%时攻击检定+3; 心碎爆发: 对带3层情感撕裂的目标消耗层数造成4D6 psychic并眩晕1回合]`

#### 外观描述
一柄细剑，剑身比寻常的刺剑更窄、更长，呈现出一种病态的银白色，仿佛月光照在尸骨上的颜色。剑身是中空的——不是锻造失误，而是刻意设计成一道极细的管道，从剑尖贯通至剑格。当刺入生物体内时，会有极细的、近乎黑色的血丝顺着中空管道回流，在剑格处汇聚成一滴墨色的液体，然后蒸发成一缕带着甜腥味的烟雾。剑柄由某种骨质材料制成，握上去能感受到微弱的脉动，像是一颗极小的心脏在跳动。剑格设计成两扇微张的肋骨形状，中央镶嵌着一颗已经石化的人类心脏，那心脏的腔室结构清晰可见。

#### 历史渊源
噬心者的锻造者是费洛伦的宫廷医师艾德里安，他同时也是一个被秘密处决的连环杀手。艾德里安相信心脏不仅是血液的泵，更是灵魂的居所。他用九十九个"心碎而死"的受害者的心脏炼成了一种合金——那些死者有的是被抛弃的情人，有的是失去孩子的母亲，有的是战败后自杀的将军——他们的共同点是死前经历了极致的情感撕裂。

剑成之后，艾德里安用它刺杀了费洛伦公爵本人，因为公爵抛弃了与他相爱二十年的骑士团长。艾德里安在公爵的卧室墙上用血写下："我取走了你的心，因为你早已不用它。"然后他将剑刺入自己的心脏，成为了第一百个祭品。

此后三百年，噬心者被称为"寡妇之剑"，因为它的每一任持有者最终都会爱上一个注定失去的人，然后在心碎中挥剑自刎。最著名的一位持有者是海洋女巫玛格丽特，她用噬心者刺穿了海王波塞冬之子的心脏，然后坐在他的尸体旁等了七天七夜，直到自己也化为泡沫。剑插在礁石上，随着潮汐起伏，脉动了整整一年才停止。

现在的持有者是一位不知姓名的影舞者，他/她从不露出真面目，但有人说在月圆之夜能听见他/她在对着剑说话，声音轻柔得像是在安慰一个哭泣的孩子。

#### 特殊性质
- **心碎之刺**：暴击时，额外造成目标已损失 HP 的 12% 作为 psychic 伤害（向下取整，最低 1）
- **情感撕裂**：连续 3 次命中同一目标后，该目标获得 3 层"情感撕裂"，其对你的攻击检定承受 -2 惩罚；若连续 2 回合未命中该目标，层数消失
- **心脏渴求**：当目标 HP 不超过其最大值的 30% 时，你对它的攻击检定获得 +3 加值（对濒死心脏的精准感知）
- **心碎爆发**：作为一个 action，可消耗带有 3 层情感撕裂的目标全部层数，造成 4D6 psychic 伤害并使其眩晕（stunned）1 回合；若目标因此降至 0 HP，其无法被常规复活术召回（每场战斗限一次）

---

### 5. 暮光之刃（Twilight's Edge）
- **item_id**: `weapon_unique_sword_twilight_edge_005`
- **display_name**: `暮光之刃`
- **base_item_id**: `weapon_type_scimitar_base`
- **family**: `sword`
- **training_group**: `martial`
- **range_type**: `melee`
- **damage_tag**: `physical_slash`
- **attack_range**: `1`
- **one_handed_dice**: `1D6+3`
- **properties**: `[finesse, light]`
- **base_price**: `55000`
- **attribute_modifiers**: `[暮影步: 战斗第3回合起每回合bonus action瞬移15尺; 昼夜平衡: 黄昏/夜间伤害+2且攻击检定advantage，白天效果减半; 暮光切割: 瞬移后首次攻击额外2D6 psychic; 暮光守护: 黄昏/夜间AC+1]`

#### 外观描述
一柄弯刀，弧度如同新月。剑身由一种被称为"暮光钢"的合金锻造，这种合金只能在日出前和日落后的各一小时内锻打，因此一柄这样的武器通常需要数十年才能完成。剑身表面不是光滑的，而是布满了细密的层状纹理，像是一本被压缩成刀刃的书，每一层都记录着一个黄昏的颜色——橘红、绛紫、靛青、墨黑。当在黄昏时分拔出此剑，剑身会逐渐变得半透明，最终融入周围的光线中，只剩下一道几乎看不见的扭曲空气。剑柄包裹着某种夜行生物的皮革，触感冰凉但干燥，握上去会闻到淡淡的薰衣草和灰烬混合的气息。

#### 历史渊源
暮光之刃是精灵族"黄昏守望者"组织的圣物。这个组织不效忠任何王国，只在昼夜交替的缝隙中行动，猎杀那些利用黑暗逃避正义的罪犯。第一柄暮光之刃由初代守望者大师瑟拉娜锻造，她是一位活了四千岁的月精灵，却选择在三千岁那年放弃永生，将自己的剩余寿命注入剑中——因此每一柄暮光之刃都蕴含着一位精灵的"放弃"。

瑟拉娜用这把剑斩杀了"夜之王"阿兹雷尔，一个统治了北方大陆五百年的吸血鬼君主。战斗发生在永恒的黄昏中——瑟拉娜用最后的力量将一片森林的时间锁定在日落前的一刻，然后在这个静止的黄昏中与夜之王周旋了七天。最终，阿兹雷尔被一剑穿心，他在化为灰烬前问瑟拉娜："为什么放弃永生？"瑟拉娜回答："因为永恒的白天是地狱，永恒的夜晚也是。只有黄昏，才值得守护。"然后她也化为光点消散，只留下暮光之刃插在阿兹雷尔的王座扶手上。

此后，每一代黄昏守望者大师都会在临终前锻造一柄新的暮光之刃，将自己的"暮年"注入其中。现在已经有了十七柄暮光之刃，分散在世界各地，有的失落，有的被毁，有的仍在某位无名守望者手中。最年轻的一柄只有八十岁，它的锻造者——一位三百岁的年轻精灵——在与深渊恶魔的战斗中阵亡，剑被他的学徒继承。

#### 特殊性质
- **暮影步**：战斗进入第 3 回合后，每回合可作为一个 bonus action 移动至多 15 尺，不引发借机攻击；战斗前 2 回合无法使用
- **昼夜平衡**：在黄昏/夜间环境中，武器伤害 +2 且攻击检定获得 advantage；在完全白天环境中，此效果减半（仅 +1 伤害，无 advantage）
- **暮光切割**：使用暮影步后的首次攻击，若命中则额外造成 2D6 psychic 伤害
- **暮光守护**：在黄昏/夜间环境中，持有者 AC 获得 +1 加值；若在白天使用暮影步，则本回合 AC 承受 -1 惩罚

---

### 6. 锈蚀之誓（Rustoath）
- **item_id**: `weapon_unique_sword_rustoath_006`
- **display_name**: `锈蚀之誓`
- **base_item_id**: `weapon_type_shortsword_base`
- **family**: `sword`
- **training_group**: `martial`
- **range_type**: `melee`
- **damage_tag**: `physical_pierce`
- **attack_range**: `1`
- **one_handed_dice**: `1D6+2`
- **properties**: `[finesse, light]`
- **base_price**: `40000`
- **attribute_modifiers**: `[锈毒: 命中叠加1层锈蚀最多10层，每层移速-5%; 护甲碎裂: 达到10层时目标金属护甲损坏，AC-3; 腐朽之刃: 对护甲碎裂目标伤害额外+2D6; 锈粉风暴: 消耗10层锈蚀对5尺锥形范围造成3D6腐蚀伤害]`

#### 外观描述
一把短剑，剑身覆盖着厚重的铁锈，仿佛刚从海底沉船中打捞上来。锈蚀最严重的部位已经形成了坑洼，但剑刃 somehow 依然保持着可怕的锋利——不是正常的锋利，而是一种"腐朽的锋利"，像朽木断裂时的边缘一样不规则，却同样致命。当剑劈开空气时，会洒落细碎的锈粉，那些粉末落在金属表面会立即开始腐蚀，落在皮肤上则会引起持续的刺痛和红肿。剑格已经锈蚀得看不出原本的形状，只剩下两块扭曲的铁片，像是一对干枯的手掌试图握住什么。剑柄的皮革早就烂光了，露出下面被锈蚀成蜂窝状的木质内芯，握上去会有一种奇怪的吸力，仿佛剑在吸走掌心的汗水——或者别的什么。

#### 历史渊源
锈蚀之誓原本叫做"海蛇之牙"，是海盗王雷德·肖的佩剑。雷德·肖统治了破碎群岛四十年，他的船队劫掠了七大王国的海岸线，却从未损失过一艘船。不是因为他的战术多么高明，而是因为"海蛇之牙"拥有一种诅咒：任何被它伤害的船只，船体都会在三天内开始锈蚀，直到化为铁渣沉入海底。

雷德·肖的最终覆灭来自他手下的叛变。大副玛丽·风暴——一个他以为已经死在某个荒岛上的女人——带着一支由七艘 rusted hulks（锈蚀船壳）组成的幽灵舰队归来。那些船明明已经锈穿了船底，却 somehow 漂浮在海面上，船帆是破烂的渔网，船员是骷髅和 seaweed 的混合物。玛丽没有直接攻击雷德·肖，她只是站在旗舰的船头，用一把普通的匕首割破自己的手掌，让血滴入海水中。三天后，雷德·肖的旗舰"海蛇号"从龙骨开始锈蚀，在十分钟内解体。雷德·肖抱着"海蛇之牙"沉入海底，在溺亡前的最后一刻，他对着剑发下最后一个誓言："若有人捞起你，替我杀了她。"剑因此获得了"锈蚀之誓"的名字——它不仅锈蚀金属，还锈蚀誓言，将每一个持有者临终的怨恨变成永恒的诅咒。

三十年后，一位年轻的渔夫在退潮后的礁石缝里发现了这把剑。他卖掉它换了十枚金币，却在三天后发现自己的渔船底部出现了一个锈斑。那个锈斑每天扩大一点，无论怎么修补都无法阻止。第七天，渔船沉了。渔夫在海底看见了雷德·肖的骷髅，仍然紧握着剑柄，眼窝里有两团暗红色的光。

现在，锈蚀之誓在某个沿海城市的黑市上流转，每一任持有者都在第七天遭遇与"水"或"金属"相关的厄运，然后死去。有人说，只有找到玛丽·风暴的后裔，让剑刺入她的心脏，诅咒才能解除。但玛丽·风暴是否真的有过后裔，已经无人知晓。

#### 特殊性质
- **锈毒**：每次命中敌人叠加 1 层"锈蚀"（最多 10 层），每层使其移动力降低 5%；锈蚀仅对单一目标累积，切换目标则旧目标层数保留 1 回合后衰减 2 层
- **护甲碎裂**：当目标锈蚀达到 10 层时，其穿戴的一件金属护甲立即损坏，AC 降低 3 点，持续至战斗结束或修复；每场战斗限触发一次
- **腐朽之刃**：对处于"护甲碎裂"状态的目标，每次命中额外造成 2D6 伤害
- **锈粉风暴**：作为一个 action，可消耗目标身上 10 层锈蚀，对 5 尺锥形范围内的所有生物造成 3D6 腐蚀伤害，并使金属护甲生物额外叠加 2 层锈蚀

---

### 7. 龙骨断剑（Wyrmbreak）
- **item_id**: `weapon_unique_sword_wyrmbreak_007`
- **display_name**: `龙骨断剑`
- **base_item_id**: `weapon_type_longsword_base`
- **family**: `sword`
- **training_group**: `martial`
- **range_type**: `melee`
- **damage_tag**: `physical_slash`
- **attack_range**: `1`
- **one_handed_dice**: `1D8+5`
- **two_handed_dice**: `1D10+5`
- **properties**: `[versatile]`
- **base_price**: `140000`
- **attribute_modifiers**: `[屠龙: 对龙类生物额外3D6伤害且无视火焰免疫; 窃骨之怒: 命中非龙类目标充能1层最多5层; 龙魂延伸: 消耗3层充能攻击范围+5尺; 龙魂爆发: 消耗5层充能释放20尺线形火焰吐息6D6火焰]`

#### 外观描述
一把断裂过的长剑。原本的剑身大约有四尺长，现在只剩下三尺——断口不是整齐的，而是呈现出锯齿状的撕裂，仿佛被某种巨力硬生生咬断。但正是这截断剑，成为了屠龙者最恐惧也最渴望的武器。断口处裸露着剑芯，那不是普通的钢，而是一根细长的、已经玉化的龙骨，呈现出乳白色与淡金色交织的纹理，像是一截凝固的闪电。当剑靠近龙类生物时，龙骨芯会开始发热，从乳白转为炽红，断口处甚至会喷射出细碎的火星。剑格由两片对称的龙鳞熔铸而成，每片都有巴掌大小，边缘仍然保持着天然的锋利。剑柄缠绕着龙筋，干燥而坚韧，握上去能感受到一种古老生物残留的不甘与愤怒。

#### 历史渊源
龙骨断剑的故事开始于一场欺骗。

矮人王国卡兹莫丹的王子索林，为了迎娶人类王国的公主，承诺为她打造一件"连龙都能斩杀的神器"。他潜入火山深处，偷取了一枚正在孵化的龙蛋——不是普通的龙蛋，而是火龙之王拉格纳罗斯的直系后裔。索林用龙蛋中的胚胎骨骼作为剑芯，用龙母的鳞片打造剑格，用龙筋缠绕剑柄，锻造成了一柄光芒夺目的长剑，命名为"龙陨"。

公主很满意，婚礼如期举行。但在新婚之夜，龙蛋的母亲——一条活了三千年的上古红龙——找到了索林。她没有直接杀死他，而是用一种可怕的方式复仇：她让索林眼睁睁看着"龙陨"在她口中断裂，然后她将那截断剑刺入索林的心脏，说："用我孩子的骨头杀死我？你甚至不配握住它。"然后她焚毁了整个卡兹莫丹，只留下了那截断剑插在索林的尸体上——因为那是她孩子的骨头，她不愿毁掉。

三百年后，一位名叫艾拉的龙裔雇佣兵捡起了这截断剑。她身上流淌着红龙的血液——她是那条上古红龙与人类奴隶的后代。当她握住断剑时，龙骨芯第一次发出了温暖而不是灼热的光芒。艾拉用这柄断剑斩杀了十七条龙，包括她自己的曾祖母——那条焚毁卡兹莫丹的红龙，在临死前终于闭上了眼睛，说："很好，我孩子的骨头，终于回家了。"

艾拉死后，龙骨断剑被存放在一座没有名字的神庙里，由一群龙裔僧侣看守。他们相信，这柄剑中沉睡着一个龙魂——不是愤怒的龙魂，而是一个从未孵化、从未看过世界的胚胎的灵魂。每当月圆之夜，僧侣们会听见剑身发出微弱的心跳声。

#### 特殊性质
- **屠龙**：对 dragon 类型生物造成额外 3D6 伤害，无视火焰免疫/抗性；对龙裔（dragonborn）造成额外 1D6 伤害
- **窃骨之怒**：命中非龙类目标时充能 1 层"龙魂怒"（最多 5 层）；对龙类攻击时不会充能
- **龙魂延伸**：消耗 3 层龙魂怒，本次攻击攻击范围 +5 尺，并视为触及额外 5 尺
- **龙魂爆发**：消耗 5 层龙魂怒，释放一道 20 尺长、5 尺宽的线形火焰吐息，范围内生物须通过 DC 16 敏捷检定，失败受到 6D6 火焰伤害，成功减半；对龙类生物使用此能力时，伤害额外 +2D6

---

### 8. 无铭（Nameless）
- **item_id**: `weapon_unique_sword_nameless_008`
- **display_name**: `无铭`
- **base_item_id**: `weapon_type_longsword_base`
- **family**: `sword`
- **training_group**: `martial`
- **range_type**: `melee`
- **damage_tag**: `physical_slash`
- **attack_range**: `1`
- **one_handed_dice**: `1D8+3`
- **two_handed_dice**: `1D10+3`
- **properties**: `[versatile]`
- **base_price**: `70000`
- **attribute_modifiers**: `[模糊之刃: 可选择本回合不移动，首次攻击检定+5; 无名之击: 上回合未造成伤害时本回合首次攻击额外3D6 force; 难以定位: 被目标锁定后首次对其攻击检定advantage; 存在之轻: 每有一个未察觉你的敌人，移动速度+5尺]`

#### 外观描述
一把真正意义上的"没有任何特征"的长剑。精钢剑身，标准十字剑格，缠皮剑柄，皮制剑鞘。没有任何花纹，没有任何铭文，没有任何宝石，没有任何装饰。把它扔进一堆普通制式长剑中，你根本找不出来。更诡异的是，无论你多么仔细地观察它，只要移开视线十秒钟，你就会忘记它的具体样子——只记得"那是一把普通的长剑"。甚至连它的重量都给人一种"模糊"的感觉，有时觉得它轻如柳枝，有时觉得它重如铁砧，但永远说不出具体的数字。唯一的不寻常之处是，如果你盯着它看超过一分钟，会在剑身表面的反光中看见一张陌生的脸——那不是你的脸，而是一个你从未见过的人，他/她也在看着你，表情悲伤而平静。

#### 历史渊源
无铭的来历是一个悖论：所有试图记录它来历的人，都忘记了为什么要记录。

第一位已知的持有者是历史学家凡尔纳，他花了四十年搜集关于这把剑的资料，写了一本七百页的专著。书成之日，他翻开第一页，发现自己完全不认识上面的字——那确实是他自己的笔迹，但他对这本书没有任何记忆。他翻到最后一页，上面写着："记住，不要记住。"然后他在极度困惑中自杀了，书被他的学生当废纸卖掉。

第二位持有者是预言家梅林娜（与那位著名的大法师无关），她试图用占卜术追溯无铭的源头。水晶球显示了一个画面：一位铁匠在锻造这把剑，铁匠的脸就是凡尔纳在剑身上看见的那张脸。梅林娜兴奋地问："他是谁？"水晶球回答："他是下一位持有者。"梅林娜不解，直到三天后一位年轻男子走进她的塔，那张脸与水晶球中的一模一样。男子说他不记得自己是怎么来的，只记得要"把剑交给能回答一个问题的人"。梅林娜问："什么问题？"男子说："我是谁？"然后男子化为烟雾消散，只留下无铭插在地板上。梅林娜从此再也不敢触碰它。

现在，无铭在一个流动的武器商人手中。商人声称自己"只是暂时保管"，因为他也忘了自己是怎么得到它的。他说这把剑每七年会自己消失，然后出现在某个即将经历重大人生转折的人面前——不是英雄，不是恶棍，只是普通人。那些普通人握住它之后，会完成一件极其重要但无人知晓的事，然后死去，被世界遗忘。

#### 特殊性质
- **模糊之刃**：每回合开始时，可选择本回合不移动；若如此做，本回合首次攻击检定获得 +5 加值
- **无名之击**：若你上回合未对任何目标造成伤害（包括未命中、未攻击、或被控制），本回合首次命中攻击额外造成 3D6 force 伤害
- **难以定位**：当一个敌人首次对你进行攻击（无论是否命中）后，你对它的下一次攻击检定获得 advantage
- **存在之轻**：战斗中，每有一个尚未察觉你存在的敌人，你的移动速度增加 5 尺（最高 +15 尺）；一旦被发现，此加成消失 1 回合后重新计算

---

### 9. 血藤（Bloodvine）
- **item_id**: `weapon_unique_sword_bloodvine_009`
- **display_name**: `血藤`
- **base_item_id**: `weapon_type_rapier_base`
- **family**: `sword`
- **training_group**: `martial`
- **range_type**: `melee`
- **damage_tag**: `physical_pierce`
- **attack_range**: `1`
- **one_handed_dice**: `1D8+3`
- **properties**: `[finesse]`
- **base_price**: `75000`
- **attribute_modifiers**: `[嗜血生长: 每次命中后武器伤害+1最多+10; 共生吸血: 造成伤害的20%转化为自身HP; 故事绽放: 达到+5时攻击额外1D6 necrotic，达到+10时恢复2D8+10 HP; 褪色: 连续2回合未命中层数清零]`

#### 外观描述
一柄活着的刺剑。剑身不是金属，而是一根被炼金术固化成精钢硬度的藤蔓，呈现出深绿与暗红交织的颜色。藤蔓表面布满了微小的、类似叶脉的纹路，那些纹路实际上是极细的血管，当剑饮血时，它们会鼓胀起来，从暗红变为鲜红，像是一条条饥渴的虫子在皮肤下蠕动。剑尖不是尖锐的，而是分成三片细小的卷须，在静止时微微开合，像是在呼吸。剑格由两片硬化的叶子构成，叶缘锋利如刀。剑柄是一截较粗的藤茎，表面覆盖着细密的绒毛，握上去会有一种奇怪的吸附感，仿佛有无数细小的根须正在试图扎入掌心吸取养分。

#### 历史渊源
血藤来自被毁灭的精灵王国埃拉西亚的"活体锻造坊"。埃拉西亚的精灵不挖矿、不冶炼，他们用炼金术将植物硬化成金属，用生长咒语让武器自我修复。血藤原本是埃拉西亚皇家花园中的一株"哀悼藤"——一种只在皇室成员死亡时才会开花的寄生植物。当埃拉西亚的最后一位王子在城破之日战死，哀悼藤吸收了王子的血，变异成了一种前所未有的形态：它不再哀悼，而是渴望。

逃亡的宫廷炼金师带走了这株变异藤蔓的种子，花了二十年将它培育成一柄剑的形态。但炼金师犯了一个错误：他在锻造的最后一步，用自己的血浇灌了剑胚。血藤从此与他建立了共生关系——不是主仆，而是寄生。炼金师发现自己越来越瘦，无论吃多少都无法增重；而血藤越来越鲜艳，剑身上的红色越来越饱满。最终，炼金师明白了真相：血藤不是在吸收他的血，而是在吸收他的"生命故事"——他的记忆、他的情感、他的人际关系。当血藤开出一朵血红色的花时，炼金师彻底消失了，连他的家人都不再记得有过这样一个人。

此后，血藤经历了十七任持有者。每一任都在使用它的过程中逐渐"褪色"——不是身体的衰老，而是存在感的淡化。他们的朋友开始忘记他们，他们的成就被归功于别人，他们的名字从记录中消失。与此同时，血藤越来越强大，剑身上的红色越来越深沉，现在已经接近黑色。现在的持有者是一位刺客公会的首领，他发现了延缓"褪色"的方法：让血藤吸收别人的故事。每次刺杀，他都将剑留在尸体中额外十秒钟，让血藤"读取"死者的记忆。他不知道的是，血藤正在用这些碎片化的记忆编织一个属于自己的"人格"，当记忆足够多时，它可能会醒来。

#### 特殊性质
- **嗜血生长**：每次命中敌人后，血藤的伤害永久增加 +1（对当前战斗有效，最多 +10）；若连续 2 回合未命中任何敌人，所有叠加层数清零
- **共生吸血**：每次命中时，将造成伤害的 20% 转化为自身 HP（不超过最大 HP）
- **故事绽放**：当嗜血生长达到 +5 时，每次命中额外造成 1D6 necrotic 伤害；当达到 +10 时，立即恢复 2D8+10 HP，但之后层数清零
- **褪色**：当血藤在单场战斗中叠满 +10 后，下一场战斗开始时以 -2 伤害开始（藤蔓过度消耗后的虚弱），直至完成一次命中

---

### 10. 守墓人之灯（Gravelight）
- **item_id**: `weapon_unique_sword_gravelight_010`
- **display_name**: `守墓人之灯`
- **base_item_id**: `weapon_type_longsword_base`
- **family**: `sword`
- **training_group**: `martial`
- **range_type**: `melee`
- **damage_tag**: `physical_slash`
- **attack_range**: `1`
- **one_handed_dice**: `1D8+3`
- **two_handed_dice**: `1D10+3`
- **properties**: `[versatile]`
- **base_price**: `72000`
- **attribute_modifiers**: `[冥灯: 对亡灵额外2D8 radiant; 磷火充能: 击杀亡灵或命中亡灵暴击时充能1点最多3点; 姓名之焰: 消耗1点磷火，对亡灵攻击无视半身掩体和闪避; 灵魂净化: 消耗3点磷火对30尺内亡灵造成6D6 radiant并震慑1回合]`

#### 外观描述
一把看似沉重的长剑，剑身比标准的制式长剑略宽、略厚，呈现出一种死寂的铅灰色，仿佛被埋在地下数百年后刚刚挖出。剑身表面布满了苔藓般的绿色斑驳，但那不是真的苔藓，而是某种发光的菌丝，在黑暗中会发出幽绿色的荧光。剑格是一个中空的圆环，环内悬浮着一颗永不熄灭的磷火——那不是附魔的效果，而是一颗被压缩成珍珠大小的灵魂之火，据说属于这把剑的第一任持有者。当剑挥动时，磷火会在圆环内急速旋转，拖出一道绿色的光轨，像是一只被困在笼中的萤火虫在疯狂挣扎。剑柄由某种骨质材料制成，但不是动物的骨头，而是一种被称为"墓土骨"的炼金材料——将人类骨灰与陶土混合烧制而成，握上去有一种奇怪的温热感，像是一个人的体温。

#### 历史渊源
守墓人之灯的第一任主人是"无名守墓人"，一个真实存在过但姓名被刻意从历史中抹去的人。他生活在"大瘟疫"时期，那场瘟疫在三年内杀死了大陆三分之一的人口。尸体太多，无法一一安葬，于是出现了"万人坑"——巨大的深坑，层层叠叠堆满尸体，然后浇上石灰掩埋。无名守墓人就是负责管理其中一个万人坑的人，他独自住在坑边的一间石屋里，每天的工作就是将新的尸体推入坑中，然后祈祷。

但祈祷没有用。万人坑中的怨气太重，死者开始复活——不是变成有意识的亡灵，而是变成纯粹的、饥饿的负能量集合体。无名守墓人最初试图用铁锹对抗它们，但铁锹很快就断了。于是他用万人坑中的骨头、石灰、磷火和死者们的绝望，锻造了这把剑。他没有锻造技术，他只是一个农民，但他有一样锻造师没有的东西：他记得每一个被推入坑中的人的名字——至少是一部分。他将这些名字念给剑听，剑因此获得了"识别"亡灵的能力。

无名守墓人持守墓人之灯在万人坑边战斗了七年，斩杀了一万只复活的尸体。第七年末，最后一个亡灵被消灭，无名守墓人也倒下了——不是因为受伤，而是因为他终于用完了记忆中的名字。他死后，人们将他与守墓人之灯一起封入石屋，然后在石屋周围种满了白杨树。三百年后，石屋被一场地震震裂，守墓人之灯重见天日，但无名守墓人的尸体不见了，只在剑柄上多了一道浅浅的握痕——比他生前更深、更紧，仿佛有人在死后仍然握着它战斗了很久。

#### 特殊性质
- **冥灯**：剑格中的磷火自动照亮半径 20 尺内的隐形亡灵（无法被熄灭）；对 undead 类型生物造成额外 2D8 radiant 伤害
- **磷火充能**：击杀一个亡灵或命中亡灵暴击时，磷火获得 1 点充能（最多 3 点）；对非亡灵生物无法充能
- **姓名之焰**：消耗 1 点磷火，本次对亡灵的攻击无视半身掩体、四分之三掩体以及"闪避"动作带来的 disadvantage
- **灵魂净化**：消耗 3 点磷火，作为一个 action，对 30 尺内一个亡灵目标造成 6D6 radiant 伤害，并使其震慑 1 回合；若目标因此降至 0 HP，其无法被死灵法术复活

---

### 11. 风语（Windwhisper）
- **item_id**: `weapon_unique_sword_windwhisper_011`
- **display_name**: `风语`
- **base_item_id**: `weapon_type_scimitar_base`
- **family**: `sword`
- **training_group**: `martial`
- **range_type**: `melee`
- **damage_tag**: `physical_slash`
- **attack_range**: `1`
- **one_handed_dice**: `1D6+2`
- **properties**: `[finesse, light]`
- **base_price**: `58000`
- **attribute_modifiers**: `[风步: 基础移速+10尺，每移动15尺后下次攻击+1D6 force; 风元素瞬身: 每短休一次30尺直线瞬移; 旋风斩: 瞬移后首次攻击可额外攻击一个相邻敌人; 风压: 每移动15尺获得+1 AC最多+3，持续至下回合]`

#### 外观描述
一柄轻若无物的弯刀，拿在手中几乎没有重量，仿佛握住的是一缕被固化的风。剑身由一种被称为"云钢"的稀有合金锻造，呈现出半透明的银白色，只有在快速移动时才能看清它的轮廓——静止时，它几乎与空气融为一体。剑身表面没有任何纹路，但当它划过空气时，会发出一种奇特的哨音，那不是金属的尖啸，而是像有人在远处吹奏一支骨笛，音调随着挥剑的速度变化：慢速时是低沉的呜咽，中速时是清越的长吟，极速时则变成刺耳的尖啸。剑格由两片扭曲如旋风的银片构成，中央镂空。剑柄缠着某种飞行生物的羽翼茎，握上去能感受到微弱的上升气流，仿佛剑随时都想脱手飞走。

#### 历史渊源
风语是天空之城"泽法拉"的遗物。泽法拉不是一座用石头建造的城市，而是一座漂浮在云层中的岛屿，由风元素领主们用永不停歇的上升气流托举。岛上的居民——"风之子"——不是人类、精灵或矮人，而是一种半实体的风元素混血种族，他们能够在空气中行走，如同鱼在水中游动。

风语由最后一位风之子锻造师"西罗科"打造。西罗科活了九百岁，见证了泽法拉的兴衰。最初，风之子们用风语与天空对话，请求风暴的庇护、季风的指引。但随着时间推移，一些风之子开始"下降"——他们羡慕地面上那些沉重的、有质感的生命，开始偷偷潜入地面世界，与固体生物交往、通婚、甚至生育后代。西罗科本人就爱上了一位地面上的游侠女子，她无法飞行，但能在森林中无声穿行。

风语是西罗科为爱人锻造的礼物。他将自己九百年对风的理解压缩进一柄刀中：如何让风托起沉重的东西，如何让风切割坚硬的东西，如何让风传递遥远的声音。但他犯了一个错误：风语太完美了，完美到开始"说话"。它向每一个持有者低语，告诉他们如何飞翔，如何摆脱重力的束缚，如何成为风的一部分。那位游侠女子在使用风语三十年后，身体逐渐变得透明，最终在一个暴风雨之夜化为一缕青烟消散。西罗科悲痛欲绝，他将风语封印在泽法拉的风眼核心，然后自己跳下云端——他没有化为一缕烟，而是像一只普通的鸟一样摔死了，因为他是风之子中第一个完全放弃飞行能力的人。

泽法拉在大约五百年前消失了，有人说它坠入了深海，有人说它飘向了太阳。风语在消失前被一位冒险者带出，现在它的低语依然继续，但内容变了：它不再教人们如何飞翔，而是教他们如何聆听——聆听风的悲伤，因为那是整个天空的哭声。

#### 特殊性质
- **风步**：持有者基础移动速度 +10 尺；每移动至少 15 尺后，下一次攻击命中时额外造成 1D6 force 伤害
- **风元素瞬身**：每短休一次，可作为一个 bonus action 进行一次至多 30 尺的直线瞬移；瞬移路径上的生物须通过 DC 14 敏捷检定，失败则被推离 5 尺并受到 1D6 force 伤害
- **旋风斩**：使用风元素瞬身后的首次攻击，若命中，可立即对另一个相邻敌人进行一次额外攻击（不消耗 action）
- **风压**：每移动 15 尺，AC 获得 +1 加值（最多 +3），持续至下回合开始；若本回合未移动，则 AC 承受 -1 惩罚

---

### 12. 铁匠的悔恨（Smith's Regret）
- **item_id**: `weapon_unique_sword_smith_regret_012`
- **display_name**: `铁匠的悔恨`
- **base_item_id**: `weapon_type_longsword_base`
- **family**: `sword`
- **training_group**: `martial`
- **range_type**: `melee`
- **damage_tag**: `physical_slash`
- **attack_range**: `1`
- **one_handed_dice**: `1D8+2`
- **two_handed_dice**: `1D10+2`
- **properties**: `[versatile]`
- **base_price**: `52000`
- **attribute_modifiers**: `[缺陷之美: 每次攻击20%概率触发极寒/炽焰/麻痹/中毒之一; 不完美共鸣: 触发缺陷后2回合内再次触发不同缺陷触发元素超载; 元素超载: 组合两种缺陷造成额外2D6对应伤害并附加双重状态; 摩拉丁的谅解: 触发缺陷后本回合下次攻击检定+2]`

#### 外观描述
一把"失败品"长剑。剑身有明显的锻造缺陷：中段微微弯曲，不是设计如此，而是淬火时冷却不均造成的；剑刃左侧比右侧略厚，导致重心偏左；剑身上有一块拳头大小的暗斑，那是混入的杂质没有被完全锻打出去。但这些缺陷 somehow 赋予了它一种奇异的力量——那块暗斑会在随机时刻发光，颜色也不固定，有时是红色，有时是蓝色，有时是绿色；弯曲的剑身在挥动时会发出不规则的震颤，那种震颤能够干扰敌人的防御节奏；重心偏移则让它的攻击轨迹难以预测，连持有者自己都不知道下一剑会偏向哪里。剑格是标准的十字形，但一侧的横臂比另一侧长了约一寸，那是铁匠在最后打磨时手抖了。剑柄的皮革缠绕得松散而凌乱，握上去有一种"被握住"的感觉，仿佛这把剑在试图纠正你的握姿。

#### 历史渊源
铁匠的悔恨属于一位名叫托林·锤心的矮人铁匠。托林是铁炉堡最杰出的武器锻造师，他一生只打造过九十九把剑，每一把都是完美无瑕的杰作，被国王和英雄们争相收藏。第一百把剑，他打算留给自己——作为退休之作，作为一生的总结，作为献给锻造之神摩拉丁的最后祭品。

他用了三年时间准备材料：陨铁、龙血淬火液、神圣祝福石粉。他断食七天净化灵魂，然后在熔炉前工作了整整四十天四十夜。但在最后淬火的瞬间，他睡着了——连续四十天的工作终于压垮了他。他在睡梦中梦见自己的母亲（一位在他幼年就死于矿难的普通妇女），母亲对他说："完美是死的，托林。活的东西都有缺陷。"

他惊醒时，淬火已经错过了最佳时机。剑身弯曲了，杂质凝固了，重心偏移了。托林看着这把"失败品"，第一次感受到一种奇怪的情绪：不是愤怒，不是失望，而是一种解脱。他给自己倒了一杯酒，第一次没有为了工作而是为了庆祝而喝酒。然后他拿起这把缺陷之剑，在剑身上刻下了他一生中的第一次——也是最后一次——不完美铭文："我累了。"

托林没有退休，他继续打铁，但不再追求完美。他开始故意在作品中留下小缺陷：一个不对称的剑格、一道浅浅的划痕、一个略微歪斜的铭文。他的新客户认为他"年老昏聩"，不再找他订做，但一些年轻的铁匠开始偷偷拜访他，学习"不完美的艺术"。铁匠的悔恨在他死后被一位学徒继承，那位学徒后来成为了第一位"缺陷锻造师"，创立了一个持续至今的铁匠流派。

现在，铁匠的悔恨在一个巡回铁匠手中，他用它来测试自己作品的强度——如果一把剑能在与铁匠的悔恨对砍中不折断，就算合格。他说这把剑教会了他最重要的一课："武器是拿来用的，不是拿来供的。"

#### 特殊性质
- **缺陷之美**：每次攻击检定成功后，掷 D100：01-20 触发缺陷效果（掷 D4：1=极寒额外 1D6 寒冷+减速；2=炽焰额外 1D6 火焰+点燃；3=麻痹 DC 13 体质检定失败则麻痹至下回合；4=中毒 DC 13 体质检定失败则中毒 1 分钟）；21-100 无额外效果
- **不完美共鸣**：触发缺陷效果后，若在接下来的 2 回合内再次触发不同的缺陷效果，则触发"元素超载"
- **元素超载**：当两种不同缺陷组合时，目标额外受到 2D6 伤害（类型为后触发的缺陷类型），并同时承受两种缺陷的状态效果；若先寒冷后炽焰，则额外造成 2D6 force 伤害（热胀冷缩）
- **摩拉丁的谅解**：触发任何缺陷效果后，本回合内下一次攻击检定获得 +2 加值（缺陷反而成为助力）

---

### 13. 黑帆（Black Sail）
- **item_id**: `weapon_unique_sword_black_sail_013`
- **display_name**: `黑帆`
- **base_item_id**: `weapon_type_shortsword_base`
- **family**: `sword`
- **training_group**: `martial`
- **range_type**: `melee`
- **damage_tag**: `physical_pierce`
- **attack_range**: `1`
- **one_handed_dice**: `1D6+3`
- **properties**: `[finesse, light]`
- **base_price**: `60000`
- **attribute_modifiers**: `[海潮: 水环境或雨天攻击检定+2; 刻名之刃: 命中叠加1层海之铭记最多5层; 萨拉之歌: 消耗3层释放15尺锥形海浪推离并倒地; 溺亡之握: 对倒地/水中目标伤害额外2D6]`

#### 外观描述
一把带有浓厚海洋气息的短剑。剑身呈现出深海般的墨蓝色，不是涂装，而是钢材在盐水与海藻汁液中反复淬炼后自然形成的色泽。剑身表面布满了细密的、像波浪一样的纹路，那些纹路在特定角度的光线下会闪烁，仿佛剑身内部有一片微缩的海洋在起伏。剑格设计成船首像的形状——不是常见的女神或海龙，而是一张没有眼睛的脸，张着嘴，像是在呐喊，又像是在呼吸。剑柄缠着浸过焦油的缆绳，握上去能感受到盐粒的粗糙和焦油的粘性。最奇特的是剑鞘，它是由一块完整的黑檀木挖空制成，内部衬着鲨鱼的皮，插入或拔出时会发出一种类似风帆鼓动的"噗"声。

#### 历史渊源
黑帆属于海盗女王"萨拉·黑帆"——不是绰号，她的姓氏真的是"黑帆"，因为她出生在一艘挂着黑帆的私掠船上，她的母亲是一名被俘的贵族女子，父亲是一个从未露面的"声音"（据她母亲说是船长，但船员们都说船长在那场战斗中死了）。萨拉从小在甲板上长大，她的摇篮是火药桶，她的玩具是弯刀和指虎，她的摇篮曲是海风和绞盘的歌。

十五岁那年，萨拉用一把从死者手中捡来的短剑杀死了一名试图侵犯她的船员。那名船员是船上的大副，力量和技巧都远胜于她，但萨拉有一种天赋：她能读懂海。她知道什么时候浪会大，什么时候风会变，什么时候船会晃。在那场战斗中，她利用了每一次船的颠簸，每一次帆的阴影，每一次浪花的飞溅。最终，大副的喉咙被刺穿，尸体被扔下海。船长——一位瞎了一只眼的老海盗——看着她说："你有黑帆家族的血，女孩。从今天起，这把剑就是你的。"

那把剑就是黑帆的前身——一把普通的短剑，但萨拉在它身上刻下了她杀死的每一个敌人的名字。三十年后，当萨拉成为七海最可怕的海盗女王时，剑身上已经刻满了密密麻麻的名字，从剑格一直延伸到剑尖，几乎找不到空隙。最后一场战斗发生在"无风带"——一片没有风、没有洋流、连海鸟都不愿进入的诡异海域。萨拉被联合舰队包围，她的船"黑帆号"被炮火撕裂。在船沉没前的最后一刻，萨拉将剑插进船舵，说："既然不能征服海，就成为海的一部分。"

黑帆号沉没了，但黑帆剑在三百年后被一位深海潜水者发现，它插在一条巨鱿鱼的触手上——那条鱿鱼缠绕着黑帆号的船舵。潜水者带回剑后发现，剑身上多了一个新的名字：那条鱿鱼的名字，用某种他无法辨认的文字刻着。从此之后，每一位黑帆的持有者都能在梦中听见海浪声和萨拉的歌声，歌词永远是同一句话："海不拒绝任何人，只是不归还。"

#### 特殊性质
- **海潮**：在水环境（游泳、船上、涉水）或雨天/暴雨天气中，攻击检定获得 +2 加值；在完全干燥的环境（沙漠、火山内部）中，攻击检定承受 -1 惩罚
- **刻名之刃**：命中一个生物时叠加 1 层"海之铭记"（最多 5 层）；对该生物的伤害每层额外 +1
- **萨拉之歌**：消耗目标身上的 3 层"海之铭记"，释放一道 15 尺锥形海浪，范围内生物须通过 DC 14 力量检定，失败则被推离 10 尺并倒地，成功则只被推离 5 尺
- **溺亡之握**：对倒地状态或处于水中的目标，每次命中额外造成 2D6 伤害

---

### 14. 焚书（Bookburn）
- **item_id**: `weapon_unique_sword_bookburn_014`
- **display_name**: `焚书`
- **base_item_id**: `weapon_type_longsword_base`
- **family**: `sword`
- **training_group**: `martial`
- **range_type**: `melee`
- **damage_tag**: `physical_slash`
- **attack_range**: `1`
- **one_handed_dice**: `1D8+3`
- **two_handed_dice**: `1D10+3`
- **properties**: `[versatile]`
- **base_price**: `78000`
- **attribute_modifiers**: `[焚咒: 命中施法者打断专注并禁止下一轮专注; 知识焚烧: 命中任意目标叠加1层焚印最多4层; 沉默之火: 消耗2层使目标本回合无法施法; 终末烈焰: 消耗4层造成6D6火焰并焚烧一个法术位]`

#### 外观描述
一把被图书馆熏香浸泡过的长剑。剑身呈现出陈旧的羊皮纸颜色，表面布满了类似文字的蚀刻——那些不是装饰性花纹，而是真正可以被阅读的句子，用已经灭绝的古代语言写成，内容似乎是某种哲学论辩的片段。当剑身靠近火焰时，那些文字会开始发光，从暗红变为金黄，仿佛被点燃的纸张；当靠近水源时，文字会模糊、褪色，像被水浸泡的墨迹。剑格由两片对称的"书页"形状构成，每片书页上都刻着不同的文字——左页是"知"，右页是"焚"，用的不是同一种文字系统。剑柄缠着某种纤维材料，触感像是非常老旧的麻绳，但闻起来有一种混合了墨水、松脂和轻微焦糊的复杂气味。剑鞘是一个中空的木盒，形状像一本合上的厚书，打开时会有轻微的纸张摩擦声。

#### 历史渊源
焚书的锻造者是"灰烬图书馆"的最后一位馆长——不是一位战士，而是一位学者，名叫埃拉斯谟。灰烬图书馆不是普通的图书馆，它收藏的不是书籍，而是"被焚毁的知识"——通过特殊的占卜和通灵技术，从已经化为灰烬的书页中重建内容。埃拉斯谟的工作就是从灰烬中"读"出那些被故意毁灭的思想。

但随着时间推移，埃拉斯谟发现了一个可怕的事实：有些知识确实不应该存在。不是因为它邪恶，而是因为它"太正确"——正确到会让所有读到它的人放弃一切追求，陷入一种永恒的、无法动弹的绝望。灰烬图书馆的地下密室中收藏着三十七卷这样的"绝望之书"，它们是从各大宗教审判所的焚书堆中抢救出来的，内容涉及宇宙的真实结构、神明的本质、以及死亡的真正含义。

埃拉斯谟决定毁掉这些书——真正的毁掉，不是焚烧（因为灰烬图书馆可以从灰烬中重建），而是用一种更彻底的方式。他花了十年时间，将三十七卷绝望之书的内容逐字逐句地蚀刻在一柄剑上，然后用一种极其复杂的仪式将剑与书的内容"绑定"：书存在，剑就有力量；书被毁，剑就变成废铁。然后他将三十七卷书的原稿投入熔炉，与剑一起淬火。原稿化为灰烬的瞬间，剑身上的文字开始发光——不是被点燃的光，而是"知识被释放"的光。

埃拉斯谟在完成这一切后，用焚书刺穿了自己的心脏。他的遗言是："我读了它们，所以我必须成为它们的牢笼。"他的尸体与焚书一起被封入一个铅棺，沉入深海。但铅棺在途中破裂，焚书被一位深海生物带上岸，卖给了一位不识货的古董商。

现在，焚书在一个秘密组织"守秘人"手中，他们的工作是追踪和销毁危险的魔法知识。他们不知道的是，埃拉斯谟当年只销毁了三十七卷中的三十六卷——最后一卷"终末之页"被他藏在了某个地方，而焚书上的文字，就是找到它的地图。

#### 特殊性质
- **焚咒**：命中正在进行法术专注（concentration）的目标时，自动打断其专注，且该目标在下一轮无法开始新的专注法术；对魔法物品或构造体（construct）造成额外 1D6 伤害
- **知识焚烧**：命中任意目标时叠加 1 层"焚印"（最多 4 层）；焚印仅对单一目标累积，切换目标则旧目标焚印保留 1 回合后消失
- **沉默之火**：消耗目标身上的 2 层焚印，使其本回合内无法施法（包括反应法术），并受到 2D6 psychic 伤害
- **终末烈焰**：消耗目标身上的 4 层焚印，对其造成 6D6 火焰伤害，并焚烧其一个可用法术位（由 DM 决定最低可用环位）；每场战斗限一次

---

### 15. 母亲之刃（Mother's Blade）
- **item_id**: `weapon_unique_sword_mothers_blade_015`
- **display_name**: `母亲之刃`
- **base_item_id**: `weapon_type_longsword_base`
- **family**: `sword`
- **training_group**: `martial`
- **range_type**: `melee`
- **damage_tag**: `physical_slash`
- **attack_range**: `1`
- **one_handed_dice**: `1D8+3`
- **two_handed_dice**: `1D10+3`
- **properties**: `[versatile]`
- **base_price**: `68000`
- **attribute_modifiers**: `[守护本能: 5尺内盟友受伤时获得reaction反击; 母性光辉: 每次成功守护为盟友提供1D8临时HP; 摇篮曲: 每成功守护2次可释放30尺平静波; 非攻之约: 主动攻击无威胁目标时本回合所有守护失效并受2D8 radiant]`

#### 外观描述
一把温暖的长剑。剑身呈现出柔和的银白色，不像普通精钢那样冷硬，而是有一种类似珍珠的温润光泽。剑身比标准长剑略窄，但厚度增加，给人一种"坚固但不笨重"的感觉——像是一位母亲的手臂，既能温柔环抱，也能坚定阻挡。剑格设计成展开的翅膀形状，但不是鹰或龙的翅膀，而是某种更接近蝙蝠或天使的膜翼，边缘柔软而圆润，没有锋利的棱角。剑柄由某种温热的骨质材料制成，握上去会感受到一种规律的脉动，那脉动的频率与正常人类心跳不同，稍快一些，像是一个焦虑的母亲的心率。剑鞘由多层皮革缝制而成，内部衬着柔软的羊绒，拔出和插入时几乎没有声音，像是一个不想吵醒婴儿的动作。

#### 历史渊源
母亲之刃没有传奇的锻造者，没有陨落的神明材料，它来自一位真正的母亲。

在"兽潮"时期——大陆南方连续三年遭受兽人部落入侵——一个名叫玛拉的农妇失去了丈夫和两个儿子。她的第三个孩子，一个刚出生三个月的女婴，是她唯一的幸存者。当兽人的先遣队烧到她所在的村庄时，玛拉没有逃跑。她用厨房的菜刀、纺车的铁轴、婴儿摇篮的铜铃和一位母亲的全部愤怒，在熔炉边工作了三天三夜。她不是铁匠，她不知道如何锻造武器，但她知道如何缝合——她把金属片像缝补丁一样拼在一起，用乳汁淬火（传说如此），用摇篮曲代替锻造祷文。

成果就是母亲之刃——一把看起来像拼凑物但实际上坚固无比的长剑。玛拉用它在村口独自抵挡了十七名兽人战士，直到援军赶到。战斗结束后，人们发现她身中二十三处伤口，但仍然站在原地，剑插在地上，左手抱着安然无恙的女婴。她没有死于伤口，而是死于精疲力竭——她在战斗中从未放下过孩子，左臂抱了整整六个小时，肌肉彻底坏死。

玛拉的女婴被一位骑士收养，长大后成为著名的"圣盾骑士"塞西莉亚。塞西莉亚一生从未使用过母亲之刃战斗——她把它挂在卧室墙上，作为提醒自己"为何而战"的信物。她在遗嘱中说："这把剑不属于战场，它属于每一个需要保护孩子的地方。"

此后五百年，母亲之刃只在"保护无辜者"的场合显现其真正力量。曾有强盗得到它，在试图用它抢劫一家农户时，剑身突然变得滚烫，烫伤了他的手；曾有士兵在屠杀平民的暴乱中捡到它，剑身在他手中锈蚀成碎片。现在，母亲之刃在一个流浪的奶妈手中，她用它在荒原上保护过路的商队免受狼群袭击。她说剑教会了她一件事："最锋利的刃，是绝不挥向无辜者的决心。"

#### 特殊性质
- **守护本能**：当 5 尺内有盟友受到伤害时，持有者获得一次 reaction 机会，可以立即对该伤害来源进行一次攻击（不消耗常规 reaction）
- **母性光辉**：每次通过守护本能成功命中攻击来源时，被保护的盟友获得 1D8 临时 HP（持续至下回合开始）
- **摇篮曲**：每成功触发 2 次守护本能，可作为一个 action 释放一道 30 尺半径的"平静波"，范围内所有生物须通过 DC 14 意志检定，失败则陷入"平静"状态 1 分钟（无法攻击，但可防御和移动；受到伤害则解除）
- **非攻之约**：若持有者主动攻击一个未持武器、未施法且明显无威胁的目标（如平民、儿童、投降者），本回合内失去所有守护本能机会，并受到 2D8 radiant 伤害

---

### 16. 群星之末（Starfell）
- **item_id**: `weapon_unique_sword_starfell_016`
- **display_name**: `群星之末`
- **base_item_id**: `weapon_type_greatsword_base`
- **family**: `sword`
- **training_group**: `martial`
- **range_type**: `melee`
- **damage_tag**: `physical_slash`
- **attack_range**: `1`
- **two_handed_dice**: `2D6+4`
- **properties**: `[two_handed, heavy]`
- **base_price**: `160000`
- **attribute_modifiers**: `[陨星之力: 命中目标叠加1层星尘最多5层; 星图指引: 消耗2层使下次攻击检定advantage; 星坠: 消耗5层召唤陨石10尺半径6D6力场+火焰; 宇宙恐惧: 对带5层星尘目标伤害额外+2D6]`

#### 外观描述
一柄仿佛由凝固的夜空锻造的巨剑。剑身长达五尺半，呈现出深邃的墨蓝色到漆黑色的渐变，表面镶嵌着无数细小的银白色光点——那不是装饰性的宝石或涂料，而是真正的星尘，来自一颗坠落在此剑锻造现场的流星。那些光点不是静止的，当在黑暗中注视它们时，会发现它们在极其缓慢地移动，像真正的星星一样沿着某种宇宙轨迹漂移。剑脊中央有一道凹槽，凹槽内填充着一种在白天看起来是黑色、在月光下会发出幽蓝荧光的物质，据说是流星的核心残骸。剑格巨大而复杂，设计成星云旋涡的形状，中央镂空处可以看到后面的景物，但景物会被扭曲成星图般的图案。剑柄由某种陨石合金制成，握上去有一种奇异的冰凉感，不是寒冷，而是"真空"——仿佛握住的是宇宙深处的孤独。

#### 历史渊源
群星之末锻造于"陨落之夜"——一个被天文学家们称为"大断裂"的事件。在那之前的夜空中，有一条被称为"织女星桥"的星带，由数千颗紧密排列的星星组成，横跨半个天穹。但在某一夜，星桥突然断裂，数百颗星星同时熄灭，其中最大的一颗化为流星，坠落在西部荒野。

三位不同种族的锻造大师——矮人布罗克、精灵塞拉菲娜和人类李淳——同时梦见了那颗流星。他们互不相识，却同时出发，同时到达坠落点。那里已经形成了一个直径三里的陨石坑，坑底的陨石仍在燃烧着蓝色的冷火。三位大师在坑底相遇，没有争斗，因为他们都知道：这颗陨石太大，任何一人都无法独自处理；这颗陨石太危险，任何一人都无法独自承受它的力量。

他们合作了八十一天。布罗克负责锻造骨架，塞拉菲娜负责引导星尘，李淳负责淬火——用的不是水，而是他们自己的血，每人贡献了十八品脱，几乎致命。剑成之夜，三位大师同时死去，不是因为失血，而是因为"被看见"——群星之末在完成的瞬间向宇宙发送了一道信号，那信号被某个遥远存在接收，作为回应，三位大师被"看了一眼"。没人知道那是什么，但目击者说，三位大师死时脸上带着同样的表情：不是恐惧，而是恍然大悟，仿佛终于解答了一个困扰一生的谜题，而答案比他们想象的更可怕也更美丽。

群星之末在世间流传了四百年，每一任持有者都会在某个夜晚梦见星桥断裂的画面，然后醒来时发现剑身上的星星排列发生了变化——似乎在以某种速度"记录"真实的星空。最近十年，剑身上的星星移动速度明显加快，天文学家们对此忧心忡忡，因为按照当前的速度，群星之末将在大约三十年后显示出一片完全陌生的星空——不是未来的星空，而是某个遥远过去的星空，或者某个遥远地方的星空。

#### 特殊性质
- **陨星之力**：每次命中目标时叠加 1 层"星尘"（最多 5 层）；每层使目标下次被你命中时额外受到 1D6 force 伤害
- **星图指引**：消耗目标身上 2 层星尘，使下一次对该目标的攻击检定获得 advantage
- **星坠**：消耗目标身上 5 层星尘，召唤一颗小型陨石坠落至其位置（10 尺半径），范围内生物须通过 DC 16 敏捷检定，失败受到 6D6 力场+火焰伤害，成功减半；建筑和非魔法构造物受到双倍伤害；每场战斗限一次
- **宇宙恐惧**：对带有 5 层星尘的目标，你对其造成的所有伤害额外增加 2D6（星尘满层时的压制效果）

---

### 17. 鸦羽（Ravenplume）
- **item_id**: `weapon_unique_sword_ravenplume_017`
- **display_name**: `鸦羽`
- **base_item_id**: `weapon_type_shortsword_base`
- **family**: `sword`
- **training_group**: `martial`
- **range_type**: `melee`
- **damage_tag**: `physical_pierce`
- **attack_range**: `1`
- **one_handed_dice**: `1D6+2`
- **properties**: `[finesse, light]`
- **base_price**: `62000`
- **attribute_modifiers**: `[鸦群召唤: 击杀生物召唤2D6只乌鸦持续1分钟; 鸦羽遮蔽: 每只乌鸦使5尺内敌人攻击检定-1最多-4; 群鸦之宴: 消耗4只乌鸦对30尺内目标造成4D6 necrotic; 群鸦喧嚣: 乌鸦数量≥6时持有者攻击检定+2]`

#### 外观描述
一把轻盈得像羽毛的短剑。剑身由一种被称为"鸦钢"的合金锻造——将陨铁与乌鸦羽毛在强酸中溶解后提炼出的物质——呈现出哑光黑色，表面有极其细密的绒毛状纹理，触感不像金属而更像某种生物的角质。剑身极薄，薄到在正面看去几乎只能看见一道黑线，仿佛它只存在于侧面。当快速挥动时，剑身会发出一种类似乌鸦振翅的"扑扑"声，而不是正常的破空声。剑格由三根弯曲的"羽毛"构成，每根羽毛的末端都尖锐如针。剑柄缠着黑色的丝线，那些丝线来自某种巨型蜘蛛，握上去会有一种微微的吸附感，仿佛剑在试图粘在你的掌心。剑鞘由中空的乌鸦股骨制成，上面烫印着无数只展翅飞翔的乌鸦，从某个角度看去，那些乌鸦似乎在动。

#### 历史渊源
鸦羽属于"鸦群"——一个已经消亡的刺客组织。这个组织的成员不效忠任何君主或神明，他们只效忠死亡本身。他们的信条是："每一次死亡都是神圣的，我们的工作是确保死亡来得准时、安静、有尊严。"他们不杀无辜者，不杀目标之外的人，甚至不收取报酬——他们只接受"一个秘密"作为代价：委托人必须告诉他们一个自己从未对任何人说过的秘密，然后鸦群会用这个秘密来确保委托的"纯洁性"（如果委托人被发现有其他动机，秘密会被公开）。

鸦羽是鸦群创始人"无面者"的佩剑。无面者不是一个人，而是一个头衔——每一任鸦群首领都会放弃自己的名字和面孔（通过一种特殊的炼金术），变成"无面者"。第一任无面者是一位公主，她的王国被瘟疫毁灭，她本人是唯一幸存者。她在废墟中与一群乌鸦生活了三年，学会了它们的语言、它们的社交方式、它们对死亡的态度。她发现乌鸦不像人类那样恐惧死亡——它们将死亡视为一种必要的清理，让生态系统保持平衡。

她用乌鸦的骨头、自己的头发和一把生锈的匕首锻造了鸦羽。这把剑的设计目的不是"杀死"，而是"引导"——引导目标的灵魂平静地离开身体，不留下怨恨，不变成亡灵。无面者用鸦羽执行了一千次"神圣刺杀"，每一次目标都在微笑中死去。第三任无面者时，鸦群遭遇了灭顶之灾：一个试图成为巫妖的法师将整座城市变成了亡灵，鸦群全员出动，试图"解放"那些被困在腐烂身体中的灵魂。战斗持续了七天，无面者用鸦羽刺穿了法师的心脏，但法师在临死前对整把剑下了诅咒："既然你爱死亡，那就永远陪伴它。"

从此，鸦羽的每一任持有者都会在某个时刻梦见自己的死亡——不是威胁，而是一种"预览"。有人梦见自己在三十年后老死，有人梦见自己在三天后被刺杀，有人梦见自己从未存在过。最可怕的是，这些梦总是成真，但持有者因此获得了一种奇异的平静：他们知道结局，所以可以专注于过程。

现在的持有者是一位年轻的医生，她用鸦羽不是为了杀人，而是为了"安乐死"——给那些无法治愈且极度痛苦的病人一个安静的终结。她说鸦羽教会了她最重要的事："死亡不是敌人，痛苦才是。"

#### 特殊性质
- **鸦群召唤**：击杀一个生物后，可立即召唤 2D6 只乌鸦（视为 familiars，持续 1 分钟或直至被驱散）；同时存在乌鸦上限为 12 只
- **鸦羽遮蔽**：每只存活的乌鸦使 5 尺内敌人攻击检定承受 -1 惩罚（最多 -4）；乌鸦可被攻击（AC 12，HP 1）
- **群鸦之宴**：作为一个 bonus action，可消耗 4 只存活的乌鸦，对 30 尺内一个目标造成 4D6 necrotic 伤害；若目标因此降至 0 HP，其无法被转化为亡灵
- **群鸦喧嚣**：当存活的乌鸦数量 ≥ 6 时，持有者攻击检定获得 +2 加值（鸦群嗡鸣带来的死亡预感转化为战斗优势）

---

### 18. 盐柱（Saltpillar）
- **item_id**: `weapon_unique_sword_saltpillar_018`
- **display_name**: `盐柱`
- **base_item_id**: `weapon_type_longsword_base`
- **family**: `sword`
- **training_group**: `martial`
- **range_type**: `melee`
- **damage_tag**: `physical_slash`
- **attack_range**: `1`
- **one_handed_dice**: `1D8+3`
- **two_handed_dice**: `1D10+3`
- **properties**: `[versatile]`
- **base_price**: `58000`
- **attribute_modifiers**: `[盐化: 命中后15%概率叠加1层锈蚀最多10层，每层移速-5%; 盐晶爆发: 达到10层时目标敏捷检定disadvantage 1分钟; 索多玛之触: 消耗5层锈蚀使10尺半径地面变困难地形; 结晶诅咒: 对盐化目标伤害+1D6，满10层额外+2D6]`

#### 外观描述
一把仿佛由凝固海水锻造的长剑。剑身呈现出半透明的乳白色，内部可以看到无数细小的、像雪花一样的结晶在缓慢旋转——那不是装饰，而是真正的盐晶，被一种炼金术永久悬浮在剑身内部。剑身表面不光滑，而是布满了细小的颗粒感，像是一块被海风侵蚀了千年的岩石。当剑劈开空气时，会洒落极细的盐粉，那些粉末落在皮肤上会引起轻微的刺痛和干涩感，落在金属上会立即形成一层薄薄的白霜。剑格由两片对称的"浪花"形状构成，边缘有结晶化的锐齿。剑柄由一块被海水浸透后风化的浮木制成，握上去能感受到一种持久的、令人口渴的咸涩。剑鞘由多层羊皮纸包裹海盐制成，拔出时会发出一种类似踏雪的"咯吱"声。

#### 历史渊源
盐柱来自"索多玛遗址"——一个被毁灭的古代城市，不是被火，而是被盐。根据残存的碑文记载，索多玛的毁灭不是因为罪恶，而是因为"好奇"：城中的炼金师们试图从海水中提炼出"生命的本质"，他们认为盐是生命的防腐剂，因此一定是生命的源泉。他们建造了巨大的蒸发池，日夜不停地煮海，直到海水下降了整整三丈，露出了从未见过天日的海底峡谷。

在峡谷深处，他们发现了一座城市——不是人类的建筑，而是某种更古老、更巨大的存在留下的遗迹。炼金师们欣喜若狂，他们认为自己找到了"前人类文明"的证据。他们带回了遗迹中的样本、文献和器物，其中就包括一块奇怪的盐晶——那块盐晶不是白色的，而是半透明的乳白色，内部有某种类似血管的结构。首席炼金师埃布尔将这块盐晶与精钢融合，锻造成了盐柱的原型。

但盐晶是活的。不是有意识的生命，而是一种"过程"——一种将有机物转化为无机盐的过程。索多玛的炼金师们一个接一个地开始"结晶"：他们的皮肤变得苍白、坚硬，他们的血液变成盐水，最终他们化为一座座盐柱，保持着生前最后的姿势。埃布尔是最后一个，他用盐柱刺穿了自己的心脏，试图以死阻止结晶的传播。他部分成功了——结晶止于索多玛城界，但城内的三万人全部变成了盐柱。

三百年后，一支探险队进入索多玛遗址，发现整座城市像一片苍白的森林，每一根盐柱都是一个曾经的人。在城市的中央，他们发现了埃布尔的盐柱——仍然保持着持剑刺心的姿势。探险队中带队的法师试图取走盐柱，但在触碰的瞬间，他自己的手指开始结晶。他当机立断砍断了自己的三根手指，然后用盐柱——现在握在埃布尔的盐柱手中——将断指旁边的地面盐化，止住了结晶的蔓延。

盐柱现在被密封在一个铅盒中，由"盐之兄弟会"保管。这个兄弟会的成员都失去了至少一根肢体——这是他们接触盐柱的代价，也是他们"被选中"的证明。兄弟会的宗旨是研究如何逆转结晶，或者至少找到控制它的方法。他们还没有成功，但已经能够将结晶限制在极小的范围内——比如剑刃接触的表面。

#### 特殊性质
- **盐化**：每次命中敌人，掷 D100：01-15 触发"盐化"，叠加 1 层（最多 10 层），每层使其移动力降低 5%；盐化仅对单一目标累积，切换目标则旧目标层数保留 1 回合后衰减 2 层
- **盐晶爆发**：当目标盐化达到 10 层时，其所有敏捷检定承受 disadvantage，持续 1 分钟；每场战斗限触发一次
- **索多玛之触**：消耗目标身上 5 层盐化，将 10 尺半径内的地面盐化（困难地形），持续 1 分钟；处于该地形中的敌人每回合开始受到 1D6 腐蚀伤害
- **结晶诅咒**：对带有盐化层数的目标，每次命中额外造成 1D6 伤害；当目标达到 10 层时，额外伤害提升至 2D6

---

### 19. 织命（Threadweaver）
- **item_id**: `weapon_unique_sword_threadweaver_019`
- **display_name**: `织命`
- **base_item_id**: `weapon_type_rapier_base`
- **family**: `sword`
- **training_group**: `martial`
- **range_type**: `melee`
- **damage_tag**: `physical_pierce`
- **attack_range**: `1`
- **one_handed_dice**: `1D8+3`
- **properties**: `[finesse]`
- **base_price**: `120000`
- **attribute_modifiers**: `[命运之线: 命中目标叠加1层命运线最多3层，每层使你对其攻击检定+1; 命运编织: 连续命中同一目标3次后其命运线脆弱化; 线断命绝: 对脆弱化目标尝试即死DC 18体质，失败则受4D10 psychic; 织线修复: 消耗10HP为10尺内濒死盟友恢复2D8 HP并稳定]`

#### 外观描述
一柄纤细得近乎脆弱的刺剑。剑身不是实心金属，而是成千上万根极细的金属丝编织而成，像是一根被拉长的、极硬的绳索。那些金属丝呈现出从银白到淡金的渐变，每一根的颜色都略有不同，当剑身转动时，会闪烁出一种类似丝绸的光泽。剑身是中空的——或者说，是"编织的"——在特定角度的光线下，可以看到剑身内部有某种东西在流动：不是液体，而是光，一种像液态黄金一样的光芒，沿着金属丝的缝隙缓缓流淌。剑格设计成展开的梭子形状，两端尖锐，可以用来格挡或刺击。剑柄由某种丝质材料缠绕，触感温暖而柔软，不像武器而更像一件衣物。最奇特的是，当持剑者静止不动时，剑尖会自然下垂，像是一根被重力牵引的线头，指向地面上的某个点——那个点据说就是"命运的节点"。

#### 历史渊源
织命不是被锻造的，而是被"织"出来的。

在世界的边缘，有一个被称为"纺织者之塔"的地方，那里居住着三位女巫——不是邪恶的那种，而是"命运的技工"。她们不预言未来，她们编织未来。每一个人的命运都是一根线，从出生到死亡，所有的选择、所有的机遇、所有的意外，都是这根线上的结和分叉。三位女巫的工作就是确保这些线不会纠缠得太厉害，不会在某个人身上打太多死结，不会让整个"织物"因为某一根线太紧而撕裂。

织命是三位女巫中的大姐"乌尔德"（Urd，意为"过去"）在临终前编织的最后一件作品。她没有用普通的线，而是用自己的生命线——从纺锤上抽出自己的线，一根一根地编织进剑身。编织完成后，她微笑着死去，因为她终于理解了命运的本质：不是控制，而是连接。每一个生命都与其他生命相连，伤害他人就是伤害自己，帮助他人就是帮助自己。

但二姐"薇尔丹蒂"（Verdandi，意为"现在"）不同意大姐的解读。她认为命运需要修剪，需要决断，需要有人勇敢地剪断那些已经腐烂的线。她将织命带离纺织者之塔，在人间流转。织命从此变成了"裁决之剑"——不是裁决善恶，而是裁决"是否该继续"。薇尔丹蒂用织命斩断了无数暴君、恶魔和瘟疫的命运线，让他们在巅峰时刻突然暴毙。但每一次斩断，她都会发现自己的线也被削弱一分。

最终，薇尔丹蒂在斩杀一位试图毁灭世界的邪神时，发现邪神的命运线与自己的线纠缠在一起——她们是双生子，一明一暗。剪断邪神的线，就是剪断自己的线。薇尔丹蒂犹豫了三天三夜，最终选择了第三条路：她没有剪断任何一根线，而是将自己的线与邪神的线编织在一起，形成了一个"结"。然后她将自己与邪神一起封印在时间的夹缝中，永远纠缠，永远无法分离。

织命现在被三妹"诗蔻蒂"（Skuld，意为"未来"）保管，她很少使用它，只是在每个月圆之夜将它放在塔顶的窗台上，让月光穿过编织的剑身，在地板上投下复杂的阴影。她说那些阴影是未来的地图，但她承认自己读不懂——"未来不是用来读的，是用来活的。"

#### 特殊性质
- **命运之线**：命中目标时叠加 1 层"命运线"（最多 3 层），每层使你对该目标的攻击检定获得 +1 加值；命运线仅对单一目标累积，切换目标则旧目标线保留 1 回合后消失
- **命运编织**：连续命中同一目标 3 次（即叠满 3 层命运线）后，其命运线进入"脆弱"状态
- **线断命绝**：作为一个 action，可对处于脆弱状态的目标尝试"剪断命运线"，目标须通过 DC 18 体质检定，失败则立即死亡（无法被常规复活术召回）；成功则受到 4D10 psychic 伤害；每场战斗限一次
- **织线修复**：消耗自身 10 HP，为 10 尺内一个濒死（HP ≤ 0）盟友恢复 2D8 HP 并稳定其伤势；每次使用使自身最大 HP 永久减少 1

---

### 20. 最后一课（Last Lesson）
- **item_id**: `weapon_unique_sword_last_lesson_020`
- **display_name**: `最后一课`
- **base_item_id**: `weapon_type_longsword_base`
- **family**: `sword`
- **training_group**: `martial`
- **range_type**: `melee`
- **damage_tag**: `physical_slash`
- **attack_range**: `1`
- **one_handed_dice**: `1D8+2`
- **two_handed_dice**: `1D10+2`
- **properties**: `[versatile]`
- **base_price**: `55000`
- **attribute_modifiers**: `[老陈的遗训: 本回合未造成伤害时获得1层教诲最多3层; 最后一课: 消耗1层教诲使下次攻击检定advantage; 传承之印: 消耗3层教诲使本回合所有攻击伤害取最大值; 教室气息: 10尺内每有一个敌人AC+1最多+3]`

#### 外观描述
一把朴素到极点的训练用长剑。剑身是普通的精钢，没有任何装饰，剑刃上布满了细小的豁口和卷边——那是无数次对练留下的痕迹。剑格是简单的十字形，一侧的横臂比另一侧稍短，那是被一次重击削掉了一小块，但持有者从未修理。剑柄的皮革缠绕已经磨损得露出了下面的木头，握上去能感受到前人留下的握痕——不是一个人的握痕，而是至少十几个人的，层层叠叠，深浅不一，像是一本用触觉写成的历史书。剑鞘是最普通的木鞘，表面被手汗浸染成了深褐色，开口处已经磨出了圆润的弧度。整把剑散发着一种特殊的气息：不是铁锈味，不是皮革味，而是一种混合了粉笔灰、汗水、木屑和某种难以名状的"教导"气息——像是一间使用了百年的剑术教室的味道。

#### 历史渊源
最后一课属于"无名剑术学院"——一个从来没有固定地址、从来没有官方认证、从来没有毕业证的组织。它的历史可以追溯到第一把铁剑被锻造出来的那一天，因为它的本质不是"学校"，而是"传承"。每一个真正理解剑术的人，在某个时刻都会成为这所学校的"老师"，将毕生所学教给一个有潜力的学生，然后把自己的佩剑传给他/她。

这把特定的"最后一课"已经传承了四百七十二年，经历了三十七位老师。第一位老师是一位名叫"老陈"的东方剑客，他在临终前将这把剑和一句话传给了弟子："剑术不是为了赢，是为了不死。"第二位老师是一位独臂骑士，他在传授了自己的马上剑术后，在剑柄上缠上了自己的护臂皮带。第三位老师是一位盲眼女剑士，她将"听风"技巧刻进了剑身表面的微小纹路中。每一位老师都在这把剑上留下了自己的痕迹：一道特殊的磨痕、一种独特的缠柄方式、一个只有内行才能看出的重心调整。

第三十七位老师是"灰狼"格雷，一位在北方边境独自对抗兽潮三十年的老兵。他没有教弟子任何华丽的招式，只教了一件事："什么时候不该挥剑。"他的弟子——一位曾经渴望成为英雄的年轻女子——在跟了他五年后终于理解了这句话。格雷的最后一次授课是在他自己的葬礼上：他预知了自己的死亡，提前立下遗嘱，要求弟子在他死后用这把剑刺穿他的心脏——不是为了亵渎，而是为了"确认死亡"，确保他不会被死灵法术复活成为敌人。弟子照做了，然后她成为了第三十八位老师。

现在，最后一课在一个十四岁的孤儿手中，他/她甚至还没有剑高。第三十八位老师说："你现在不配用这把剑战斗，你只配带着它。每天擦拭它，感受那些握痕，想象那些老师的故事。当你终于明白为什么剑术是为了不死而不是为了赢，你就可以用它来战斗了。"

#### 特殊性质
- **老陈的遗训**：当本回合结束时，若你本回合未对任何敌人造成伤害（包括选择不攻击、未命中、或仅进行防御动作），获得 1 层"教诲"（最多 3 层）
- **最后一课**：消耗 1 层教诲，使下一次攻击检定获得 advantage
- **传承之印**：消耗 3 层教诲，使本回合内所有攻击伤害骰取最大值；使用此能力后，下回合无法获得教诲
- **教室气息**：10 尺内每存在一个敌人，你的 AC 获得 +1 加值（最多 +3）；若 10 尺内没有敌人，则本回合攻击检定承受 -1 惩罚（剑只 teach 你如何在包围中生存）

---

*卷一 · 完结*
*下一卷：巨剑家族 · 裂地与苍穹*
