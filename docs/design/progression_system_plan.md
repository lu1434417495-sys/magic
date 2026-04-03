# 玩家升级与职业体系设计

## 概述

本设计将玩家成长拆成三条独立但关联的进度线：

- 技能成长：学习技能后，通过训练场或战斗获取熟练度，提升技能等级。
- 职业成长：通过核心满级技能推动转职与职业升级。
- 人物等级：不单独累计经验，直接等于所有职业等级之和。

系统目标：

- 支持基础技能、职业技能、技能标签、核心技能、多职业、兼职职业。
- 区分转职、非转职升级、职业失效三套不同规则。
- 保持配置驱动，便于后续扩展更多职业、属性和技能。

## 核心规则理解

### 技能成长

- 玩家进入游戏时，人物等级为 `0`。
- 玩家可学习基础技能。
- 普通技能通常通过技能书学习。
- 职业技能不能通过技能书学习，只能由职业授予。
- 技能通过两类来源获取熟练度：
  - 训练场
  - 战斗
- 熟练度达到阈值后，技能升级，直到达到技能最大等级。

### 核心技能

- 玩家可同时将多个技能设置为核心技能。
- 只有“核心且满级”的技能可以参与职业相关判定。
- 一个技能可以有多个标签，因此理论上可以同时满足多个职业条件。
- 但一个技能一旦首次用于某个职业升级，就会绑定到该职业。
- 绑定后的技能只能继续为该职业提供升级贡献，不能再用于其他职业。

### 转职

- 转职定义为职业等级从 `0 -> 1`。
- 转职时检查四类条件：
  - `required_skill_ids`
  - `required_tag_rules`
  - `required_profession_ranks`
  - `required_attribute_rules`
- 转职要求的指定技能必须是核心且满级。
- 转职要求的属性默认检查基础属性，不检查装备和临时效果。
- 善恶值等倾向属性以区间方式判定。

### 非转职升级

- 非转职升级定义为职业等级从 `1 -> 2`、`2 -> 3` 等后续升级。
- 非转职升级时只检查：
  - `required_tag_rules`
  - `required_profession_ranks`
- 非转职升级时不检查：
  - `required_skill_ids`
  - `required_attribute_rules`
- 也就是说，职业入门看“准入条件”，后续升级看“流派深造条件”。

### 职业失效

- 职业升级成功后，并不代表职业永远处于生效状态。
- 部分职业会长期检查持续生效条件，例如善恶值区间。
- 如果持续条件不满足：
  - 职业失效
  - 职业等级隐藏
  - 该职业授予的职业技能不可用
  - 人物等级不变
  - 职业历史等级不丢失
- 职业失效不等于掉级，而是暂时封印或停用。
- 职业是否自动恢复，以及失效职业是否还能作为其他职业前置，按职业配置决定。

## 数据结构设计

### SkillDef

技能静态定义。

```gdscript
class_name SkillDef
extends Resource

@export var skill_id: StringName
@export var display_name: String
@export_multiline var description: String
@export var skill_type: StringName
@export var max_level: int = 1
@export var mastery_curve: PackedInt32Array
@export var tags: Array[StringName] = []
@export var learn_source: StringName
@export var learn_requirements: Array[StringName] = []
@export var mastery_sources: Array[StringName] = []
```

字段约定：

- `skill_type`: `active` / `passive`
- `learn_source`: `book` / `profession`

说明：

- 普通技能用 `book` 表示可通过技能书学习。
- 职业技能用 `profession` 表示只能通过职业授予。
- `tags` 用于职业判定，不直接表示职业归属。

### PlayerBaseAttributes

玩家基础属性。

```gdscript
class_name PlayerBaseAttributes
extends RefCounted

var hp_max: int
var strength: int
var agility: int
var intelligence: int
var morality: int
var custom_stats: Dictionary = {}
```

说明：

- 转职时职业门槛检查这里的基础属性。
- 善恶值建议使用连续整数轴，例如 `-100 ~ 100`。
- `custom_stats` 预留给未来可扩展属性。

### ProfessionDef

职业静态定义。

```gdscript
class_name ProfessionDef
extends Resource

@export var profession_id: StringName
@export var display_name: String
@export_multiline var description: String
@export var max_rank: int = 1
@export var unlock_requirement: ProfessionPromotionRequirement
@export var rank_requirements: Array[ProfessionRankRequirement] = []
@export var granted_skills: Array[ProfessionGrantedSkill] = []
@export var active_conditions: Array[ProfessionActiveCondition] = []
@export var reactivation_mode: StringName
@export var dependency_visibility_mode: StringName
```

### ProfessionPromotionRequirement

职业晋升条件。

```gdscript
class_name ProfessionPromotionRequirement
extends Resource

@export var required_skill_ids: Array[StringName] = []
@export var required_tag_rules: Array[TagRequirement] = []
@export var required_profession_ranks: Array[ProfessionRankGate] = []
@export var required_attribute_rules: Array[AttributeRequirement] = []
```

### ProfessionRankRequirement

职业某一级的升级条件。

```gdscript
class_name ProfessionRankRequirement
extends Resource

@export var target_rank: int
@export var required_tag_rules: Array[TagRequirement] = []
@export var required_profession_ranks: Array[ProfessionRankGate] = []
```

说明：

- 非转职升级不包含指定技能与属性门槛字段。
- 这样可以在结构层面防止实现时误用。

### TagRequirement

标签条件。

```gdscript
class_name TagRequirement
extends Resource

@export var tag: StringName
@export var count: int = 1
```

### ProfessionRankGate

前置职业等级条件。

```gdscript
class_name ProfessionRankGate
extends Resource

@export var profession_id: StringName
@export var min_rank: int = 1
@export var check_mode: StringName
```

字段约定：

- `check_mode`: `historical` / `active_only`

说明：

- `historical` 表示只看是否曾达到该等级，即使当前职业失效也算满足。
- `active_only` 表示只有当前处于生效状态的职业等级才算满足。

### AttributeRequirement

属性门槛。

```gdscript
class_name AttributeRequirement
extends Resource

@export var attribute_id: StringName
@export var min_value: int
@export var max_value: int
```

说明：

- 用区间统一表达普通属性与善恶值属性。
- 例如：
  - 力量至少 10：`min_value = 10`, `max_value = 999999`
  - 善恶值偏善：`min_value = 40`, `max_value = 100`
  - 善恶值偏恶：`min_value = -100`, `max_value = -40`

### ProfessionGrantedSkill

职业授予技能定义。

```gdscript
class_name ProfessionGrantedSkill
extends Resource

@export var skill_id: StringName
@export var unlock_rank: int
@export var skill_type: StringName
```

说明：

- 用于定义职业在特定等级授予哪些主动或被动技能。
- 这些技能不能通过技能书学习。

### ProfessionActiveCondition

职业持续生效条件。

```gdscript
class_name ProfessionActiveCondition
extends Resource

@export var condition_type: StringName
@export var attribute_id: StringName
@export var min_value: int
@export var max_value: int
```

字段约定：

- 当前主要支持 `condition_type = attribute_range`

说明：

- 用于长期检查善恶值等条件。
- 与职业升级条件分离，避免混淆“能不能升级”和“当前是否生效”。

### PlayerSkillProgress

玩家技能进度。

```gdscript
class_name PlayerSkillProgress
extends RefCounted

var skill_id: StringName
var is_learned: bool = false
var skill_level: int = 0
var current_mastery: int = 0
var total_mastery_earned: int = 0
var is_core: bool = false
var assigned_profession_id: StringName
var merged_from_skill_ids: Array[StringName] = []
var mastery_from_training: int = 0
var mastery_from_battle: int = 0
var profession_granted_by: StringName
```

说明：

- `assigned_profession_id` 是关键字段。
- 核心技能必须显式挂接到一个职业。
- 技能合并或重挂接时，需要更新这个字段。
- `merged_from_skill_ids` 记录该技能由哪些旧技能合并而来。
- 这些来源技能在合并完成后应被标记为不可再次学习。
- 合并来源查询必须支持递归展开整条合并链。
- `profession_granted_by` 用于标记此技能是否是职业技能及其来源职业。

### PlayerProfessionProgress

玩家职业进度。

```gdscript
class_name PlayerProfessionProgress
extends RefCounted

var profession_id: StringName
var rank: int = 0
var is_active: bool = true
var is_hidden: bool = false
var core_skill_ids: Array[StringName] = []
var granted_skill_ids: Array[StringName] = []
var promotion_history: Array[ProfessionPromotionRecord] = []
var inactive_reason: StringName
```

说明：

- `rank` 记录职业历史等级。
- `is_active` 表示职业当前是否生效。
- `is_hidden` 表示职业等级在 UI 中是否隐藏。
- `core_skill_ids` 表示当前挂接在该职业下的核心技能。
- 职业后续升级只从 `core_skill_ids` 中判断标签覆盖。

### ProfessionPromotionRecord

职业升级记录。

```gdscript
class_name ProfessionPromotionRecord
extends RefCounted

var new_rank: int
var consumed_skill_ids: Array[StringName] = []
var snapshot_base_attributes: Dictionary = {}
var timestamp: int = 0
```

说明：

- 转职时记录属性快照，便于回放、日志和调试。
- 非转职升级虽然不检查属性，也可以统一记录当时属性快照，方便追踪问题。

### PlayerProgress

玩家总进度。

```gdscript
class_name PlayerProgress
extends RefCounted

var character_level: int = 0
var base_attributes: PlayerBaseAttributes
var skills: Dictionary = {}
var professions: Dictionary = {}
var active_core_skill_ids: Array[StringName] = []
var pending_profession_choices: Array[PendingProfessionChoice] = []
var blocked_relearn_skill_ids: Array[StringName] = []
var version: int = 1
```

说明：

- `character_level = sum(all profession.rank)`
- 人物等级不受职业失效影响。
- 某职业即使被隐藏和失效，其历史等级仍计入人物等级。
- `blocked_relearn_skill_ids` 记录因技能合并而被禁止重新学习的旧技能。

### PendingProfessionChoice

待选职业候选。

```gdscript
class_name PendingProfessionChoice
extends RefCounted

var trigger_skill_ids: Array[StringName] = []
var candidate_profession_ids: Array[StringName] = []
var target_rank_map: Dictionary = {}
```

说明：

- 当某核心满级技能同时满足多个职业候选时，需要显式选择。
- 一旦选择并完成升级，相关技能将绑定到目标职业。

## 判定逻辑

### 技能是否可参与职业成长

一个技能必须同时满足以下条件：

- 已学习
- 已设为核心技能
- 已达到技能最大等级
- 已挂接到当前目标职业

### 转职判定

目标：职业从 `0 -> 1`

检查顺序：

1. 目标职业当前等级是否为 `0`
2. `required_skill_ids` 是否全部满足
3. `required_tag_rules` 是否全部满足
4. `required_profession_ranks` 是否全部满足
5. `required_attribute_rules` 是否全部满足

满足后：

- 职业等级变为 `1`
- 参与本次升级的技能挂接到目标职业
- 职业授予该等级可获得的职业技能
- 人物等级重算

### 非转职升级判定

目标：职业从 `1 -> N`

检查顺序：

1. 目标职业当前等级是否大于等于 `1`
2. 读取 `target_rank = current_rank + 1` 的升级条件
3. 检查 `required_tag_rules`
4. 检查 `required_profession_ranks`

满足后：

- 职业等级加 `1`
- 发放该等级新增职业技能
- 人物等级重算

### 核心技能补位判定

目标：当核心技能数量不足以支撑当前人物等级时，为某职业补一个核心技能

触发前提：

1. `当前核心技能总数 < 人物等级`
2. 某职业满足 `core_skill_ids.size() < rank`

允许补位的技能必须满足：

1. 已学习
2. 当前不是核心技能
3. 已达到技能最大等级
4. 技能标签符合该职业可接受的标签范围

满足后：

- 该技能被设为核心技能
- 该技能挂接到目标职业
- 该职业的 `core_skill_ids` 增加该技能

### 技能合并后的职业处理

目标：在支持技能合并后，保持职业体系中的核心技能记录一致

处理规则：

1. 若参与合并的旧技能属于某职业的 `core_skill_ids`，则先从对应职业中移除
2. 若旧技能的 `assigned_profession_id` 有值，则清除旧挂接关系
3. 生成合并后的新技能后：
   - 若新技能不是核心技能，则不挂接任何职业
   - 若新技能是核心技能，则必须显式指定其目标职业
4. 指定后：
   - 写入新技能的 `assigned_profession_id`
   - 将新技能加入目标职业的 `core_skill_ids`
5. 新技能必须记录 `merged_from_skill_ids`
6. 所有被吞并的旧技能都要加入 `blocked_relearn_skill_ids`

### 合并技能后的再学习限制

目标：保证已经作为合并素材消耗过的技能，后续不能再次学习

处理规则：

1. 任意技能参与合并后，其 `skill_id` 进入 `blocked_relearn_skill_ids`
2. `learn_skill(skill_id)` 在执行前必须检查：
   - 该技能是否已存在于 `blocked_relearn_skill_ids`
3. 如果存在，则学习失败
4. 合并后的结果技能保留 `merged_from_skill_ids`，用于：
   - UI 展示技能来源
   - 调试和存档追踪
   - 阻止玩家重新学习被吞并的旧技能
5. 查询某个技能的合并来源时，必须递归展开其来源链
6. 例如：`A + B => C`，`C + D => E`，则查询 `E` 的递归来源时，至少应能返回 `A, B, C, D`

### 职业持续生效判定

目标：判断职业当前是否生效

检查顺序：

1. 读取职业的 `active_conditions`
2. 逐条检查当前玩家属性是否满足
3. 若全部满足，则职业生效
4. 若任一不满足，则职业失效

职业失效后的结果：

- `is_active = false`
- `is_hidden = true`
- 职业技能不可用
- 职业等级不丢失
- 人物等级不变化

恢复逻辑：

- `reactivation_mode = auto` 时，条件恢复后自动重新生效
- `reactivation_mode = manual` 时，条件恢复后仍需玩家手动恢复

## 典型示例

### 示例一：战士转职

职业“战士”配置：

- 转职要求指定技能：`basic_sword`
- 标签要求：`melee x1`
- 属性要求：`strength >= 10`

玩家状态：

- `basic_sword` 已学习、已核心、已满级
- 有一个 `melee` 标签的核心满级技能
- `strength = 12`

结论：

- 可从 `战士 0 -> 1`

### 示例二：战士后续升级

职业“战士 2 级”配置：

- 标签要求：`melee x2`
- 无属性要求
- 无指定技能要求

玩家状态：

- 已经是战士 1 级
- 有两个符合条件的核心满级 `melee` 技能

结论：

- 可从 `战士 1 -> 2`

### 示例三：圣骑士转职与失效

职业“圣骑士”配置：

- 转职要求前置职业：`战士 >= 1`
- 标签要求：`holy x1`
- 属性要求：`morality in [40, 100]`
- 持续生效条件：`morality in [40, 100]`

玩家状态：

- 战士 1 级
- 有一个 `holy` 标签的核心满级技能
- 当前 `morality = 60`

结论：

- 可转职为圣骑士 1 级

之后若：

- `morality = 10`

则：

- 圣骑士失效
- 圣骑士等级隐藏
- 圣骑士技能不可用
- 人物等级不变

## 接口固化

以下接口视为当前版本的固定接口边界，后续实现按这些职责拆分。

### ProgressionService

负责技能熟练度、技能升级、职业升级、人物等级重算。

```gdscript
class_name ProgressionService
extends RefCounted

func learn_skill(skill_id: StringName) -> bool
func grant_skill_mastery(skill_id: StringName, amount: int, source_type: StringName) -> bool
func set_skill_core(skill_id: StringName, enabled: bool) -> bool
func recalculate_character_level() -> int
func can_promote_profession(profession_id: StringName) -> bool
func promote_profession(profession_id: StringName) -> bool
func get_profession_upgrade_candidates() -> Array[PendingProfessionChoice]
func is_skill_relearn_blocked(skill_id: StringName) -> bool
```

接口约束：

- `learn_skill` 只负责学习技能，不负责挂接职业
- `learn_skill` 执行前必须检查 `blocked_relearn_skill_ids`
- `grant_skill_mastery` 只负责增加熟练度和触发技能升级
- `set_skill_core` 只改变核心状态；若要挂接职业，必须走专门接口
- `can_promote_profession` 根据当前职业等级自动判断是转职还是非转职升级
- `promote_profession` 执行升级成功后的职业等级变化、职业技能发放和人物等级重算

### ProfessionAssignmentService

负责核心技能与职业之间的挂接、重挂接、补位。

```gdscript
class_name ProfessionAssignmentService
extends RefCounted

func assign_core_skill_to_profession(skill_id: StringName, profession_id: StringName) -> bool
func remove_core_skill_from_profession(skill_id: StringName, profession_id: StringName) -> bool
func can_promote_non_core_to_core(skill_id: StringName, profession_id: StringName) -> bool
func promote_non_core_to_core(skill_id: StringName, profession_id: StringName) -> bool
func get_profession_core_skills(profession_id: StringName) -> Array[StringName]
func get_skill_assigned_profession(skill_id: StringName) -> StringName
```

接口约束：

- `assign_core_skill_to_profession` 仅允许挂接“已核心且满级”的技能
- 一个技能同一时刻只能挂接一个职业
- `remove_core_skill_from_profession` 用于技能合并、职业重组或调试修正
- `can_promote_non_core_to_core` 用于判断是否满足核心技能补位规则
- `promote_non_core_to_core` 成功后必须同时完成三件事：
  - 将技能设为核心
  - 写入 `assigned_profession_id`
  - 将技能加入职业的 `core_skill_ids`

### ProfessionRuleService

负责职业条件判定，包括转职、非转职升级、持续生效检查。

```gdscript
class_name ProfessionRuleService
extends RefCounted

func can_unlock_profession(profession_id: StringName) -> bool
func can_rank_up_profession(profession_id: StringName) -> bool
func can_satisfy_tag_rules(profession_id: StringName, tag_rules: Array[TagRequirement]) -> bool
func can_satisfy_profession_gates(gates: Array[ProfessionRankGate]) -> bool
func can_satisfy_attribute_rules(rules: Array[AttributeRequirement]) -> bool
func evaluate_profession_active_state(profession_id: StringName) -> bool
func refresh_all_profession_states() -> void
```

接口约束：

- `can_unlock_profession` 仅用于 `0 -> 1`
- `can_rank_up_profession` 仅用于 `1 -> N`
- `can_satisfy_tag_rules` 只统计该职业 `core_skill_ids` 中的核心技能
- `can_satisfy_attribute_rules` 只检查基础属性
- `evaluate_profession_active_state` 只检查持续生效条件，不参与职业升级

### SkillMergeService

负责技能合并后对职业体系的清理与重挂接。

```gdscript
class_name SkillMergeService
extends RefCounted

func merge_skills(source_skill_ids: Array[StringName], result_skill_id: StringName, keep_core: bool, target_profession_id: StringName) -> bool
func detach_merged_source_skills(source_skill_ids: Array[StringName]) -> void
func attach_merged_result_skill(result_skill_id: StringName, keep_core: bool, target_profession_id: StringName) -> bool
func get_merged_source_skill_ids(skill_id: StringName) -> Array[StringName]
func get_merged_source_skill_ids_recursive(skill_id: StringName) -> Array[StringName]
```

接口约束：

- 合并前必须先把旧技能从所属职业的 `core_skill_ids` 中移除
- 若 `keep_core = false`，合并结果不得挂接任何职业
- 若 `keep_core = true`，必须提供 `target_profession_id`
- 合并完成后，后续职业升级只认合并后的新技能，不认旧技能历史
- 合并完成后，必须把 `source_skill_ids` 写入结果技能的 `merged_from_skill_ids`
- 合并完成后，必须把 `source_skill_ids` 加入 `blocked_relearn_skill_ids`
- `get_merged_source_skill_ids` 用于职业系统、UI 和存档查询合并来源
- `get_merged_source_skill_ids_recursive` 必须递归展开整条合并链
- 递归查询结果必须去重，并保持稳定顺序
- 递归查询结果不包含当前查询技能自身，只包含其历史来源技能

## 配置与脚本落位建议

遵循仓库目录规则，推荐放置位置如下：

- 技能配置：`data/configs/skills/`
- 职业配置：`data/configs/professions/`
- 玩家进度脚本：`scripts/player/`
- 升级与职业判定系统：`scripts/systems/`
- UI 相关展示与交互：`scripts/ui/`

## 实现优先级

建议按以下顺序落地：

1. 先实现基础数据模型与序列化结构
2. 实现技能熟练度与技能升级
3. 实现转职判定
4. 实现非转职升级判定
5. 实现职业技能授予与禁用
6. 实现职业持续生效检查与恢复逻辑
7. 最后接训练场、战斗结算和 UI
