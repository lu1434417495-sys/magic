# 玩家成长系统设计

更新日期：`2026-04-15`

## 关联上下文单元

- CU-11：队伍与角色成长运行时数据模型
- CU-12：CharacterManagement、成就记录、奖励归并桥
- CU-14：progression 规则与属性服务

当前实现边界以 [`project_context_units.md`](project_context_units.md) 为准；本文记录玩家成长、职业技能、成就与奖励归并的设计口径。

关联文档：
- [achievement_system_plan.md](archive/achievement_system_plan.md)
- [battle_achievement_stats_plan.md](battle_achievement_stats_plan.md)
- [equipment_system_plan.md](equipment_system_plan.md)
- [playable_vertical_slice_roadmap.md](archive/playable_vertical_slice_roadmap.md)

## Summary

- 本项目的目标成长模型改为“人物等级 + 职业 rank 总和 + 唯一激活核心技能 + 职业专精 + 装备构筑”的多轴成长。
- 角色正式永久成长真源仍固定为 `PartyMemberState.progression -> UnitProgress`；装备属于外部构筑层，战斗状态属于快照层。
- `character_level` 在目标设计中继续等同于“职业 rank 之和”，也不通过经验值触发。
- 人物等级提升时的属性增长，不由等级本身决定，而由“触发这次升级的核心技能”决定。
- 玩家需要手动选择 `active_level_trigger_core_skill_id`；被选中的核心技能达到锁定前上限 `non_core_max_level` 后，会弹出所有符合条件的职业解锁 / rank 晋升选项。
- 玩家只能选择一个职业，确认后立即完成职业 rank、人物等级与升级成长结算。
- 同一时间只能有一个核心技能处于激活态并具有升级触发资格；该核心技能一旦触发升级并完成确认，就进入锁定态。
- 锁定后的核心技能不会退出战斗系统，反而会进入强化态：继续可用，并获得 `bonus_to_hit_from_lock` 提供的攻击命中、检定类法术与目标豁免 DC 加成。
- `bonus_to_hit_from_lock` 按当前项目的战斗命中 / 技能检定 resolver 生效，不再采用旧版独立命中公式。
- 战斗成就不参与“能不能升级”的判定；如果保留，只作为属性结算的弱修正，不能覆盖触发技能的主导方向。
- 所有成长写入仍统一收口到 `CharacterManagementModule` / `ProgressionService`；UI、BattleUnit、窗口不直接改成长真源。
- 战斗中允许“职业晋升后刷新当前 BattleUnitState”，但成就、研究、任务等复合奖励继续走 `PendingCharacterReward` 延迟确认，避免直接污染当前战斗快照。
- 新增“功法成长轴”：`meditation` 解锁魔力，`cultivation` 解锁斗气；二者都是人物成长正式语义，而不是临时战斗特例。
- 魔力 / 斗气直接复用当前 `mp_max/current_mp` 与 `aura_max/current_aura` 资源链；未解锁时所有玩家界面隐藏对应资源。
- 同轨技能同时只能学一个；学习第二个同轨技能时必须先确认替换，再按技能评级差换算新技能等级。
- 世界时间每 `15 world_step` 视为 1 天；跨天时对全队已解锁成员结算一次“上限增长 + 当前回复”的功法成长。

## 当前仓库事实

- `UnitProgress` 已经持有：
  - `unit_base_attributes`
  - `reputation_state`
  - `skills`
  - `professions`
  - `known_knowledge_ids`
  - `achievement_progress`
  - `pending_profession_choices`
  - `blocked_relearn_skill_ids`
  - `merged_skill_source_map`
- `CharacterManagementModule` 已经是正式成长桥接层，负责：
  - 学习技能 / 知识
  - 战斗熟练度入账
  - 职业晋升
  - 成就推进与奖励排队
  - 属性快照构建
  - 开战角色快照生成与战斗中刷新
- `AttributeService` 已经将属性结算拆为：
  - 永久基础属性
  - 职业修正
  - 技能修正
  - 装备修正
  - 被动 / 临时效果修正
- `WorldTimeSystem` 当前以 `15 world_step` 视为 1 天；跨天功法成长需要接入现有步进结果。
- `PartyMemberState` 已经持有 `current_hp/current_mp/current_aura`，斗气当前值具备世界态持久化基础。
- `AttributeService` 已经有 `MP_MAX / AURA_MAX` 常量，但当前只有 `MP_MAX` 具备正式静态派生规则；`AURA_MAX` 还没有正式人物成长基线。
- `PartyManagementWindow` 已经是当前最适合承载成长总览的正式入口；`PromotionChoiceWindow` 与奖励弹窗已经具备模态编排能力。
- `GameSession` 已经缓存 `skill_defs / profession_defs / achievement_defs / item_defs`，说明成长内容当前采用“注册表索引 + 运行时缓存”模式。
- `PartyItemUseService.use_item()` 当前直接调用 `CharacterManagementModule.learn_skill(...)`，学习结果仍是布尔返回，不足以表达“需要确认替换旧功法”的中间态。

## 当前实现与后续目标

- 当前实现里，`character_level` 由 `ProgressionService.recalculate_character_level()` 按职业总 rank 计算；这是本文档的正式等级口径。
- 本文档保留以下目标设计：
  - `character_level` 是显式人物等级，数值等于所有 `UnitProfessionProgress.rank` 的总和
  - `profession.rank` 同时表示职业专精深度，并作为人物等级的正式贡献来源
  - 人物等级完全不依赖经验值累计
  - 每次等级增长都必须由唯一激活中的核心技能触发并认领
  - 该核心技能在触发升级并确认后进入锁定态，不能继续触发下一次升级
  - 战斗成就不参与“能不能升级”的判定
  - 基础属性的主要永久增长来源，改为“由触发升级的核心技能决定的升级结算”而不是零散奖励累加
  - `mp_max / aura_max` 在人物成长语义中改为“功法资源上限”，未解锁时必须隐藏并输出 `0`
  - `SkillDef` 对带 `meditation / cultivation` 标签的技能，需要新增正式 `practice_tier`
  - 世界时间继续采用 `15 step = 1 day` 的正式成长语义
  - 同轨功法学习需要从“直接学习”改为“预览 -> 确认替换 -> 正式学习”两段式
- 这意味着后续真正落地时，需要调整：
  - `UnitProgress.active_core_skill_ids` 的语义
  - 手动激活核心技能达到 `non_core_max_level` 后弹出符合条件的职业解锁 / rank 晋升选项的确认事务
  - 属性永久增长来源的优先级
  - `PartyMemberState.current_aura` 的 UI 快照、战斗回写与资源可见性

## 目标与非目标

### 目标

- 建立一套与现有仓库完全一致的成长总纲，明确各成长维度的状态归属、作用边界与写入时机。
- 让战斗、据点、奖励、仓库、装备最终汇入同一条“培养角色 -> 形成构筑 -> 回到战斗”的主循环。
- 保持成长系统可验证、可持久化、可在 headless 与 UI 双链路下运行。
- 为后续 `training / research / quest / equipment restriction / data-driven content` 预留稳定接口。

### 非目标

- 不使用任何 `XP / EXP / character_exp` 形式的经验值累计。
- 不采用“刷经验 -> 升级 -> 固定加点”的传统单轴升级模型。
- 不做天赋树、随机词缀、洗点、无限循环成就、复杂 DSL 条件。
- 不允许 UI 层或战斗单位绕过服务层直接改 `UnitProgress`。
- 不把装备、临时 buff、战斗状态写成永久成长字段。
- 不在本设计里引入第二套奖励容器或第二套成长真源。

## 成长分层模型

### 1. 永久成长层

真源：`PartyMemberState.progression -> UnitProgress`

- 基础属性：`UnitBaseAttributes`
- 知识：`known_knowledge_ids`
- 技能进度：`UnitSkillProgress`
- 职业进度：`UnitProfessionProgress`
- 声望：`UnitReputationState`
- 成就进度：`AchievementProgressState`

职责：
- 表达角色“已经成为什么样的人”。
- 参与存档 round-trip。
- 只能由成长服务写入。

### 2. 构筑层

真源：
- `PartyMemberState.equipment_state`
- `UnitProgress.active_level_trigger_core_skill_id`（目标设计）
- `UnitProfessionProgress.core_skill_ids / granted_skill_ids`

职责：
- 表达角色“当前怎么带技能、穿什么装备、激活什么职业收益”。
- 可以影响属性快照与战斗可用技能。
- 不是第二份永久角色历史，而是永久成长之上的当前构筑结果。
- 在目标设计里，成长系统只允许一个核心技能处于“激活且可触发升级”的状态。

### 3. 运行时快照层

真源：
- 世界态：`PartyMemberState.current_hp/current_mp/current_aura`
- 战斗态：`BattleUnitState.attribute_snapshot / known_active_skill_ids / known_skill_level_map`
- 临时效果：battle/passive temporary state

职责：
- 表达当前场景下的瞬时资源与临时状态。
- 可以从成长层重建。
- 除明确允许的刷新点外，不反向改写永久成长。

## 核心设计结论

### 一、角色等级以职业 rank 总和为正式主轴

- `character_level` 在目标设计中是显式人物等级，其数值直接来源于“职业 rank 总和”。
- `character_level` 不由 `XP` 驱动，也不按已学职业技能数量计数。
- 角色每解锁职业或提升职业 rank，人物等级就随职业总 rank 增加。
- `profession.rank` 的职责改为：
  - 表示职业深度
  - 控制职业授予技能与职业属性收益
  - 决定该职业体系下的构筑上限
  - 贡献人物等级
- `character_level` 的职责改为：
  - 触发角色基础属性成长
  - 提供全局成长节奏
  - 与职业 rank 总和保持同步
- 设计理由：
  - 当前 `ProgressionService.recalculate_character_level()` 和 `ProfessionAssignmentService` 已经以职业总 rank 作为等级与核心容量依据。
  - 职业 rank 是玩家明确确认后的专精进度，比“已学职业技能数量”更适合作为全局等级节奏。
  - 职业授予技能仍然表达职业内容扩展，但不单独承担人物等级真相源职责。

### 二、升级属性不是固定加点，而是由“触发升级的核心技能”决定

- 每次人物等级提升时，不直接给固定属性点，而是先定位“这次升级由哪个核心技能触发”。
- 该触发核心技能决定本次升级的主成长方向。
- 战斗成就 / 战斗表现如果保留，只能做次级修正，不能覆盖触发核心技能的主导类型。
- 评价输出不是通用平均分，而是围绕这一个触发技能生成本次升级的属性增长结果。
- 例子：
  - 如果这次升级由 `mobility / charge / dodge` 核心技能触发，则本次升级偏向 `敏捷`。
  - 如果这次升级由 `shield / guard / taunt` 核心技能触发，则本次升级偏向 `体质` 或 `意志`。

### 三、技能是成长主引擎，职业是构筑放大器

- `SkillDef` 决定技能类型、最大等级、熟练度曲线、标签、学习来源、战斗配置与属性修正。
- `UnitSkillProgress` 决定该角色是否学会、当前等级、熟练度来源、是否核心、归属哪个职业、是否由融合产生。
- 技能成长分三类：
  - 学会：获得使用权。
  - 熟练：提升技能等级、解锁更强变体或更高收益。
- 核心化：把达到锁定前上限 `non_core_max_level` 且符合条件的技能变成职业核心，成为职业晋升材料与构筑支点。
- 职业的主要价值不是替代技能树，而是：
  - 为某类标签技能提供体系化承接。
  - 提供 rank 驱动的属性修正。
  - 在特定 rank 授予职业技能。
  - 通过 `active_conditions` 决定当前职业收益是否生效。

### 四、知识与声望负责打开新分支，不直接堆面板

- 知识用于：
  - 解锁职业
  - 解锁技能学习条件
  - 作为研究 / 任务 / 成就的奖励目标
- 声望用于：
  - 满足职业与剧情型门槛
  - 驱动据点、阵营、派系相关成长条件
- 这两个维度默认是“门槛资源”，不是直接数值膨胀器。

### 五、基础属性变化要少而重，但升级评价应成为主来源

- 永久基础属性的正式增长来源调整为：
  - 角色升级评价结算
  - 少量成就奖励
  - 训练 / 剧情 / 特殊任务
- 也就是说：
  - 成就与任务更适合做“额外奖励”
  - 升级评价才是角色六维长期分化的主引擎
- `hp_max / mp_max / action_points / attack / defense / resistance` 等非基础属性不持久化缓存，统一由 `AttributeService` 动态结算。
- 这样可以避免职业、技能、装备迭代时出现多处脏缓存。

### 六、装备属于外部构筑，不属于永久成长历史

- 装备可以影响：
  - 属性快照
  - 临时技能授予
  - 标签与抗性
- 装备不应影响：
  - 技能是否永久学会
  - 职业历史
  - 成就进度
  - 基础属性永久值
- 装备相关细则继续以 [equipment_system_plan.md](equipment_system_plan.md) 为准，但总体上归属于成长系统的“外部构筑层”。

### 七、奖励统一排队，避免战斗内乱写永久状态

- 成就、研究、任务、部分战斗结算奖励统一进入 `PartyState.pending_character_rewards`。
- 奖励确认后才正式写回角色。
- 例外：
  - 战斗熟练度可以直接入 `UnitProgress`，因为它是局内局外都成立的连续进度。
  - 职业晋升在已显式弹窗确认后允许立即刷新 `BattleUnitState`，因为这本质上是玩家主动选择的构筑切换。
- 设计原则：
  - “玩家主动确认的构筑变更”可以即时刷新。
  - “系统结算型复合奖励”默认延迟到统一奖励窗口确认。
  - 手动激活核心技能达到 `non_core_max_level` 并触发职业选项后，玩家确认某一个职业解锁 / rank 晋升时，人物等级与属性成长在同一事务内立即结算。

### 八、功法成长轴（冥想 / 修炼）

- `meditation` 标签技能解锁魔力链，`cultivation` 标签技能解锁斗气链。
- 这两条链路直接复用 `mp_max/current_mp` 与 `aura_max/current_aura`，不再另建第三套人物资源字段。
- 未解锁时，不额外持久化布尔开关；轨道状态直接由“当前已学技能里是否存在对应标签技能”导出。
- 未解锁时，所有玩家界面都必须隐藏对应资源链，而不是显示 `0/0` 占位值。
- 同一角色同时最多只能持有 1 个冥想技能与 1 个修炼技能。
- 学习新的同轨技能时，系统不能直接覆盖旧技能；必须先弹确认，再执行替换。
- 冥想 / 修炼技能必须显式配置 `practice_tier`，评级固定为：
  - `basic = 0`
  - `intermediate = 1`
  - `advanced = 2`
  - `ultimate = 3`
- 替换后的新技能等级公式固定为：

```text
new_level = clamp(old_current_level + (old_tier - new_tier), 0, new_skill.max_level)
```

- 本次设计取 `K = 1`，不再额外放大评级差。
- 替换后旧技能会被移出同轨已学状态，并清空其 `skill_level / current_mastery / total_mastery_earned`。
- 新技能不会继承旧技能熟练度；只继承经评级差换算后的 `skill_level`。
- 0 级已学技能是合法状态，替换公式下限保留为 `0`。
- 旧功法不加入 `blocked_relearn_skill_ids`；允许日后再次学习，但再次学习同轨功法时仍必须走替换确认与同一等级换算公式，不能恢复旧熟练度。
- `1 天 = 15 world_step`；跨天时，对全队已解锁成员统一结算一次或多次功法成长。
- 首次学会某轨技能时，立即给予“1 天成长结果”的起始值，保证资源链解锁当下即可见且可用。
- 斗气起始值直接使用首日成长结果，不额外设计独立静态基线公式。
- 每日成长分为两条独立公式：
  - 上限增长
  - 当前回复
- 每日成长默认都吃四类因子：
  - 当前功法技能等级
  - 基础属性
  - 职业白名单加成
  - 知识白名单加成
- 魔力上限增长以 `智力 / 意志` 为主，斗气上限增长以 `力量 / 意志` 为主；回复量再用独立公式计算，不等于当日上限增长值。
- 人物成长视角下，`mp_max / aura_max` 的正式成长语义以本文件为准；旧的独立静态推导只保留兼容参考，不再作为主增长真相源。
- 如果其它设计文档对 `aura / 斗气` 有更早期的独立资源口径，人物成长实现以本文件为准。

## 成长维度设计

### 1. 基础属性

- 真源：`UnitBaseAttributes`
- 用途：
  - 职业解锁与 rank 门槛
  - 派生属性计算基础
  - 部分装备 / 技能条件
- 规则：
  - 只保存基础六维及少量永久值。
  - 派生数值统一由 `AttributeService` 计算。
  - 后续如果新增训练系统，也应通过 `AttributeService.apply_permanent_attribute_change(...)` 写入。

### 2. 知识

- 真源：`UnitProgress.known_knowledge_ids`
- 用途：
  - 职业知识门槛
  - 技能学习条件
  - 据点研究与任务奖励
- 规则：
  - 只记录“拥有/未拥有”，不做分级知识熟练度。
  - 重复知识不会重复入账。
  - 适合作为成长分支解锁器，而不是数值主轴。
  - 对功法成长而言，知识更适合作为每日上限增长 / 每日回复的白名单修正，而不是单独的资源解锁开关。

### 3. 技能

- 内容定义：`SkillDef`
- 角色状态：`UnitSkillProgress`
- 主要来源：
  - 书本 / 奖励 / 训练学习
  - 职业授予
  - 装备临时授予（未来）
  - 技能融合
- 主要状态：
  - `is_learned`
  - `skill_level`
  - `current_mastery`
  - `base_max_level`（目标设计）
  - `is_core`
  - `assigned_profession_id`
  - `profession_granted_by`
  - `merged_from_skill_ids`
  - `is_level_trigger_active`（目标设计）
  - `is_level_trigger_locked`（目标设计）
  - `bonus_to_hit_from_lock`（目标设计）
- 正式规则：
  - 主动学会与职业授予分开建模。
  - 熟练度来源至少区分 `battle` 与 `training`。
  - 达到锁定前上限 `non_core_max_level` 是“核心化”和“职业绑定”的前置条件。
  - 融合技能必须保留来源链，防止重学与追溯断裂。
  - 对带 `meditation / cultivation` 标签的技能，必须显式配置正式 `practice_tier`。
  - 同轨功法技能在正式真源里同一时间只允许一个处于 `is_learned == true`。
  - 同一时间只能有一个核心技能处于 `is_level_trigger_active == true`。
  - 核心技能一旦触发一次等级提升并完成确认，就进入 `is_level_trigger_locked == true`。
  - 已锁定核心技能默认不能再次作为升级触发技能，除非后续另开解锁规则。
  - 锁定后的核心技能仍然是正式战斗技能，并通过 `bonus_to_hit_from_lock` 参与攻击命中、检定类法术与目标豁免 DC 加成。
  - 不新增额外觉醒等级上限字段；已有 `SkillDef.max_level` 是技能最终上限，`SkillDef.non_core_max_level` 是锁定前上限与职业选项触发上限。
  - 锁定后解除 `non_core_max_level` 的锁定前限制，该技能后续可以继续成长到 `SkillDef.max_level` 或动态最终上限。
  - 本设计不承载锁定后的额外伤害字段或觉醒等级上限字段。
- 设计目标：
  - 让技能既能承担战斗手感成长，也能承担职业系统的结构化输入。

#### 功法轨道技能（冥想 / 修炼）

- `meditation` 轨道负责魔力成长，`cultivation` 轨道负责斗气成长。
- 两条轨道都不额外写“已解锁”布尔值；是否解锁由当前已学技能标签直接导出。
- 轨道互斥只作用于同标签内部：
  - 可以同时持有一个冥想技能和一个修炼技能
  - 不能同时持有两个冥想技能
  - 不能同时持有两个修炼技能
- 学第二个同轨技能时，系统必须先生成替换确认，而不是直接学习成功。
- 确认替换后，新技能等级按“旧技能当前等级 + 评级差”换算：
  - `new_level = clamp(old_current_level + (old_tier - new_tier), 0, new_skill.max_level)`
- 0 级已学技能是合法状态；替换结果可以为 `0`。
- 旧技能在替换完成后必须清零并退出已学状态；新技能不继承旧技能熟练度。
- 旧功法允许日后再次学习，但再次学习时必须重新走同轨替换确认与等级换算公式，不允许绕过替换流程或恢复历史熟练度。
- 功法轨道技能既是战斗技能，也是人物资源链的解锁器与按日成长入口。

### 4. 职业

- 内容定义：`ProfessionDef`
- 角色状态：`UnitProfessionProgress`
- 输入条件：
  - 知识
  - 标签技能
  - 属性门槛
  - 声望门槛
  - 前置职业 rank
- 输出收益：
  - `rank`
  - 属性修正
  - 授予技能
  - 构筑身份与可见性
- 正式规则：
  - 初始职业负责基线玩法，不必走知识解锁。
  - 后续职业通过“技能标签 + 知识/声望/属性门槛”打开。
  - `rank` 不只是数值提升，还应代表构筑深度与角色容量。
  - `active_conditions` 让多职业共存但不必同时全部生效。
  - 对功法成长而言，职业更适合作为“每日上限增长 / 每日回复”的白名单加成来源，而不是单独的解锁开关。
- 设计目标：
  - 角色成长不是线性换皮，而是围绕职业身份形成不同战斗结构。

### 5. 成就

- 真源：`AchievementProgressState`
- 奖励载体：`PendingCharacterReward`
- 规则：
  - 成就归属始终是 `member_id`
  - 只在首次达成时发奖
  - 奖励类型以知识、技能、熟练度、属性为主
- 与总成长系统的关系：
  - 成就是“横向激励器”，不替代技能 / 职业主线。
  - 更适合补稀缺知识、特定技能、少量属性增量。
  - 战斗统计型成就继续按 [battle_achievement_stats_plan.md](battle_achievement_stats_plan.md) 扩展，不在这份总纲里重复定义细节。

### 6. 声望

- 真源：`UnitReputationState`
- 当前定位：
  - 作为职业与剧情门槛资源存在
  - 未来可绑定阵营 / 据点 / 派系成长分支
- 设计约束：
  - 不与成就系统重复。
  - 不直接当作另一条人物升级主轴使用。

### 7. 装备

- 真源：`EquipmentState`
- 与成长系统的关系：
  - 作为外部构筑层接入 `AttributeService`
  - 后续可追加职业限制、属性门槛、授予技能
- 设计约束：
  - 战斗中默认不允许换装
  - 卸装后失去授予收益，不保留永久学习痕迹
  - 细则见 [equipment_system_plan.md](equipment_system_plan.md)

### 8. 等级成长评价

- 这是本次改版新增的核心机制。
- 人物等级的数值来源于职业 rank 总和，但每次等级提升时的属性增长，必须由一个“唯一激活中的核心技能”来触发和认领。

#### 升级触发

- 本方案完全不使用经验值。
- 人物等级直接由职业 rank 总和决定。
- 玩家必须先手动选择 `active_level_trigger_core_skill_id`。
- 被手动激活的核心技能达到锁定前上限 `non_core_max_level` 后，会触发所有符合条件的职业解锁 / rank 晋升选项。
- 玩家选择其中一个职业并确认后，系统在同一事务内结算职业 rank、人物等级、属性成长和核心技能锁定。
- 也就是说：
  - “能不能升”取决于手动激活核心技能是否达到 `non_core_max_level` 并打开了符合条件的职业选项。
  - “由谁来触发这次升级”取决于玩家手动选择的当前激活核心技能。
  - “升到哪个方向”取决于玩家选择的那个职业。
  - “升了以后长什么”取决于这个触发技能的类型与本成长周期表现。

#### 唯一激活核心技能规则

- 同一时间只能有一个核心技能处于激活态。
- 这个激活态需要由玩家手动选择或切换。
- 只有这个激活中的核心技能具有“触发升级”的资格。
- 其它核心技能即使已经存在并达到 `non_core_max_level`，只要处于非激活态，就不能触发当前升级或弹出职业选择。
- 推荐新增字段：
  - `active_level_trigger_core_skill_id`

#### 核心技能锁定规则

- 激活中的核心技能一旦触发了一次等级提升，并且玩家完成升级确认，就必须进入锁定态。
- 锁定后的核心技能：
  - 不能再次作为下一次升级的触发技能
  - 继续保留为已学会 / 已核心化技能
  - 继续作为正式战斗技能使用
  - 获得锁定带来的 `bonus_to_hit_from_lock`
  - 不再承担升级触发职责
- 这条规则的目标是：
  - 防止单一核心技能无限重复主导全部升级
  - 强制角色在后续成长中切换新的核心技能焦点
  - 同时让“已经完成一次成长触发的核心技能”在命中与检定稳定性上获得兑现

#### 锁定收益建议

- 锁定收益只承载 `bonus_to_hit_from_lock`。
- 本文档不承载锁定后的额外伤害字段、锁定伤害倍率或觉醒等级上限字段。
- `bonus_to_hit_from_lock` 不仅作用于普通攻击命中，也应能作用于该锁定技能引发的检定类法术与目标豁免 DC。
- 推荐理解：
  - 核心技能先负责“触发角色成长”
  - 锁定后再负责“提高该技能相关命中、检定与豁免难度的稳定性”

#### 命中与检定加成口径

- 旧版独立命中公式已经废弃，本文档不再定义另一套命中公式。
- `bonus_to_hit_from_lock` 必须接入当前项目已有的战斗命中与技能检定 resolver。
- 加成范围：
  - 使用该锁定技能发起的攻击命中判定。
  - 使用该锁定技能触发的检定类法术或控制检定。
  - 使用该锁定技能造成的目标豁免 DC；实现上应提高本技能的有效 `save_dc`，而不是提高目标自己的 save bonus。
- 非加成范围：
  - 不增加该技能伤害。
  - 不作为全局角色命中加成。
- 默认数值以 `LevelGrowthEvaluationService.LOCK_HIT_BONUS_DEFAULT` 为准；当前实现为 `+1`。
- 如果未来要给少数精准型技能更高数值，应通过技能配置或专门规则扩展，不在本设计里新增伤害或觉醒等级字段。

#### 职业选择确认事务

- 玩家手动选择的激活核心技能达到 `non_core_max_level` 后，系统展示当前符合条件的全部职业解锁 / rank 晋升选项。
- 这些选项不是全局职业列表，必须通过职业规则、知识、属性、rank、核心技能绑定等限制校验；具体限制规则可随职业系统后续补全。
- 玩家每次只能选择一个职业。
- 玩家确认后，在同一事务内完成：
  - 对应职业解锁或 rank 晋升
  - `character_level` 按职业 rank 总和重算
  - 读取当前唯一激活核心技能，生成本次等级成长结果
  - 写入本次属性成长
  - 锁定该核心技能并写入 `bonus_to_hit_from_lock`
  - 清空当前 `active_level_trigger_core_skill_id`
- 不引入待结算升级次数；系统不保留“等级已经增长但成长尚未结算”的中间状态。
- 如果某个奖励或操作可能导致多个职业 rank 连续提升，必须拆成多次职业选择确认；每次确认都需要新的激活核心技能。

#### 评价窗口

- 推荐以“上一次升级之后到这一次升级之前”作为一个成长周期。
- 周期内：
  - 职业 rank 累积用于等级数值增长
  - 激活中的核心技能用于认领这次升级
  - 战斗成就 / 战斗表现用于升级后的属性结算微调
- 升级后周期数据清空，避免角色前期打法永久锁死后期属性成长。

#### 评价输入

- 输入 A：战斗成就与战斗表现
  - 来源优先复用 [battle_achievement_stats_plan.md](battle_achievement_stats_plan.md) 的战斗统计真源。
  - 典型输入：
    - `total_damage_done`
    - `total_kill_count`
    - `max_turn_damage_done`
    - `total_damage_taken`
    - `downed_count`
    - `total_healing_done`
    - `successful_skill_count`
    - 以及已解锁的战斗成就
- 输入 B：核心技能评分
  - 只统计“触发这次升级的那个激活核心技能”。
  - 该触发技能评分建议至少考虑：
    - 技能等级
    - 熟练度总量
    - 是否已归属激活职业
    - 技能标签
    - 是否已经进入锁定强化阶段
  - 评分高低反映“这次升级由什么类型的核心技能驱动”。

#### 评价权重

- 推荐默认权重：
  - 核心技能评分：`80%`
  - 战斗成就 / 战斗表现：`20%`
- 原因：
  - 触发升级的核心技能是本次等级提升的正式来源，必须拥有绝对主导权。
  - 战斗成就只负责修正“同类型成长内部偏哪边长”，不能覆盖触发技能的类型结论。

#### 成长倾向维度

- 推荐先固定为六类成长倾向：
  - `mobility`
  - `power`
  - `guard`
  - `precision`
  - `arcane`
  - `support`

#### 核心技能标签到成长倾向的映射

- `mobility / charge / dodge / flank / assassin`
  - 主加 `mobility`
- `melee / sword / axe / weapon / berserker`
  - 主加 `power`
- `shield / guard / taunt / endure / protector`
  - 主加 `guard`
- `ranged / bow / sniper / scout / aim`
  - 主加 `precision`
- `magic / elemental / spell / ritual`
  - 主加 `arcane`
- `heal / prayer / support / command / holy`
  - 主加 `support`

#### 战斗表现到成长倾向的映射

- 高频位移、冲锋、绕后、先手击杀
  - 提升 `mobility`
- 高输出、斩杀、爆发峰值
  - 提升 `power`
- 承伤、生存、嘲讽、保护队友
  - 提升 `guard`
- 远程命中、稳定收割、侦查式打法
  - 提升 `precision`
- 法术输出、地形改造、元素连动
  - 提升 `arcane`
- 治疗、驱散、增益、辅助触发
  - 提升 `support`

#### 成长倾向到基础属性的映射

- `mobility`
  - 主属性：`敏捷`
  - 次属性：`感知`
- `power`
  - 主属性：`力量`
  - 次属性：`体质`
- `guard`
  - 主属性：`体质`
  - 次属性：`意志`
- `precision`
  - 主属性：`感知`
  - 次属性：`敏捷`
- `arcane`
  - 主属性：`智力`
  - 次属性：`意志`
- `support`
  - 主属性：`意志`
  - 次属性：`智力`

#### 升级结算建议

- 每次升级允许增长 `1-3` 项基础属性，最多 `3` 项。
- 分配规则建议为：
  - 第一高倾向：必得 `+1`
  - 第二项：
    - 若第二倾向接近第一倾向，则给第二倾向主属性 `+1`
    - 否则给第一倾向次属性或主属性 `+1`
  - 第三项：
    - 只有当前三倾向分布足够接近，或成长画像确实呈现复合构筑时才发放
    - 若第三倾向过低，则不发第三项增长
- 这样可以保证：
  - 单一专精角色成长更集中
  - 混合构筑角色升级时可以同时长出 2-3 项属性
- 示例：
  - 机动型核心技能显著领先：`敏捷 +2`
  - 机动与精准接近：`敏捷 +1，感知 +1`
  - 守御、支援、奥术接近：`体质 +1，意志 +1，智力 +1`

#### 设计目标

- 同职业角色也能因为核心技能和战斗风格不同，长成不同六维轮廓。
- 玩家不需要手动逐点加属性，但仍能通过构筑和打法“间接塑造”角色成长方向。
- 位移技能作为核心技能时，角色会稳定朝 `敏捷` 偏移，这正符合本次需求。

#### 方案裁定

- 本文档当前主方案采用“职业 rank 总和”的等级语义。
- 也就是：
  - `character_level = sum(UnitProfessionProgress.rank)`
  - 职业解锁或晋升是等级增长的正式来源。
  - 职业授予技能、核心技能、熟练度与战斗表现影响构筑和升级属性评价，但不直接改写等级数值。
  - 不采用“职业技能数量累加”或“达到 base_max_level 的职业技能数量累加”作为等级定义。
- 后续补规则时，重点不再是 `count_as_profession_skill()`，而是：
  - 手动激活核心技能达到 `non_core_max_level` 后的职业选项生成。
  - 玩家选择一个职业后的确认事务。
  - 职业 rank、人物等级、属性成长与核心技能锁定的同事务结算。

## 成长闭环

从玩家视角，正式成长循环固定为：

1. 在世界探索、战斗、聚落服务中获得材料、知识、技能、熟练度与成就进度。
2. 通过战斗和训练把关键技能推到锁定前上限 `non_core_max_level`，形成可核心化的技能池。
3. 玩家手动选择的激活核心技能达到 `non_core_max_level` 后，弹出所有符合条件的职业解锁 / rank 晋升选项，并形成清晰的成长倾向。
4. 玩家只能选择一个职业并确认。
5. 确认后立即读取“触发这次升级的核心技能”与本成长周期战斗成就，同事务结算职业 rank、人物等级、基础属性增长与核心技能锁定。
6. 通过职业 rank、技能等级、升级后的属性与装备搭配重建战斗技能组。
7. 用更强的构筑挑战更高阶敌人、委托和研究内容，再回流新的成长资源。

- 与这条主循环并行，世界时间每跨过 1 天，全队已解锁的冥想 / 修炼轨道都会自动结算一次功法上限增长与当前回复。

关键闭环不是“刷数字”，而是“获得条件 -> 形成选择 -> 改变构筑 -> 打开更高阶内容”。

## 写入时机与运行时规则

### 立即写入

- 技能学习成功
- 功法替换确认成功
- 知识学习成功
- 熟练度入账成功
- 职业晋升确认成功
- 手动激活核心技能达到 `non_core_max_level` 后触发的职业选择确认成功
- 成就进度推进
- 装备变更（世界态下）
- 跨天触发的功法成长结算

说明：
- 这些变化要么是确定性的，要么是玩家显式操作，应立即写回正式真源。
- 尤其是跨天触发的功法成长，必须与 `world_data.world_step` 一起持久化；不能只保存世界时间而遗漏 `party_state`。

### 延迟写入

- 同轨功法学习时的替换确认
- 成就奖励
- 研究奖励
- 任务成长奖励
- 其它需要弹窗确认的复合角色奖励

载体：
- `PartyState.pending_character_rewards`

说明：
- 这些奖励不直接刷新当前战斗单位，避免“战斗内自动升面板”破坏可预期性。

### 战斗内特殊规则

- `BattleUnitState` 默认在开战时由 `CharacterManagementModule.build_battle_party(...)` 生成。
- 职业晋升一旦在战斗中被确认，可通过 `refresh_battle_unit(...)` 局部刷新当前单位快照。
- 成就 / 研究 / 任务奖励在战斗中只入队，不立刻改当前 `BattleUnitState`。
- 战斗结束后才统一提交 `current_hp/current_mp/current_aura` 等资源回世界态。

## 状态归属与接口边界

### 正式真相源

- `PartyState`
  - 队伍编成
  - 待领奖励队列
  - 仓库
- `PartyMemberState`
  - 角色世界态资源（含 `current_aura`）
  - 装备状态
  - 成长真源入口
- `UnitProgress`
  - 永久成长核心数据

### 正式服务层

- `CharacterManagementModule`
  - 队伍级成长入口
  - 战斗 / 世界桥接
  - 奖励排队与入账
- `ProgressionService`
  - 单角色技能 / 职业 / 知识主逻辑
- `ProfessionRuleService`
  - 职业解锁、rank、激活条件判定
- `ProfessionAssignmentService`
  - 核心技能与职业绑定
- `SkillMergeService`
  - 技能融合链管理
- `AttributeService`
  - 属性快照统一计算
- `PracticeGrowthService`
  - 功法轨道识别、替换等级换算、按日成长与回复结算
- `PartyEquipmentService`
  - 装备事务与仓库联动

### 非正式写入方

- `PartyManagementWindow`
- `PromotionChoiceWindow`
- `MasteryRewardWindow`
- `PracticeReplacementWindow`
- `BattleUnitState`
- 其它 UI / snapshot / view model

这些对象只能发请求、展示结果、消费已构建好的 prompt / reward，不能直接改成长真源。

## 新增数据结构建议

为支持“升级评价结算”这条新主轴，建议后续补以下字段或对象：

### `SkillDef`

- `practice_tier`
  - 仅对带 `meditation / cultivation` 标签的技能强制配置
  - 评级固定使用 `basic / intermediate / advanced / ultimate`

### `PartyMemberState`

- `current_aura`
  - 世界态下的正式斗气当前值
  - 需要与 `current_hp/current_mp` 一样参与存档 round-trip 与战后回写

### `UnitBaseAttributes.custom_stats`

- `mp_max`
  - 功法成长系统写入的正式魔力上限持久值
- `aura_max`
  - 功法成长系统写入的正式斗气上限持久值
- 不新增单独的“是否解锁魔力 / 斗气”布尔字段；解锁态由当前已学功法技能标签导出

### `UnitProgress`

- `character_level`
  - 保留，语义继续为“职业 rank 总和后的当前人物等级”
- `active_level_trigger_core_skill_id`
  - 当前唯一具有升级触发资格的核心技能
- `locked_level_trigger_skill_ids`
  - 已经触发过升级并被锁定的核心技能列表

### `UnitSkillProgress`

- `is_level_trigger_active`
  - 当前是否是唯一激活中的升级触发核心技能
- `is_level_trigger_locked`
  - 是否已经完成过一次等级触发并进入锁定强化态
- `bonus_to_hit_from_lock`
  - 锁定后提供的攻击命中 / 检定类法术 / 目标豁免 DC 修正，默认值以 `LevelGrowthEvaluationService.LOCK_HIT_BONUS_DEFAULT` 为准，当前实现为 `+1`

### 新对象：`PendingPracticeReplacementPrompt`

- 作用：
  - 当角色学习第二个同轨功法技能时，缓存待确认替换信息
- 建议字段：
  - `member_id: StringName`
  - `track_type: StringName`
  - `old_skill_id: StringName`
  - `old_skill_label: String`
  - `old_skill_level: int`
  - `old_practice_tier: StringName`
  - `new_skill_id: StringName`
  - `new_skill_label: String`
  - `new_practice_tier: StringName`
  - `predicted_new_level: int`
  - `source_item_id: StringName`
  - `reason_summary: String`

### 新对象：`LevelGrowthCycleState`

- 作用：
  - 记录“自上次升级以来”的成长评价输入
- 建议字段：
  - `battle_profile_scores: Dictionary`
  - `unlocked_battle_achievement_ids: Array[StringName]`
  - `recent_battle_stat_totals: Dictionary`
  - `cycle_started_at_level: int`

### 新对象：`LevelGrowthResult`

- 作用：
  - 表达职业选择确认事务中的升级评价输出结果
- 建议字段：
  - `level_from: int`
  - `level_to: int`
  - `dominant_profile_id: StringName`
  - `secondary_profile_id: StringName`
  - `granted_attribute_changes: Array[Dictionary]`
  - `reason_summary: String`

### 新服务：`LevelGrowthEvaluationService`

- 作用：
  - 输入触发升级的核心技能与成长周期战斗画像
  - 在玩家确认某个职业解锁 / rank 晋升后，同事务输出本次升级的属性增长结果
  - 锁定本次触发核心技能并写入 `bonus_to_hit_from_lock`
- 与现有系统关系：
  - 由 `CharacterManagementModule` 调用
  - 最终属性变更仍通过 `AttributeService.apply_permanent_attribute_change(...)` 落地

### 新服务：`PracticeGrowthService`

- 作用：
  - 识别角色当前冥想 / 修炼轨道技能
  - 生成同轨替换预览
  - 按评级差计算替换后的新技能等级
  - 处理首次解锁起始值注入
  - 按天计算 `mp_max/aura_max` 的增长与 `current_mp/current_aura` 的回复
- 与现有系统关系：
  - 由 `ProgressionService` 调用其学习 / 替换规则
  - 由 `CharacterManagementModule` 或世界时间桥接层调用其按日成长结算
  - `AttributeService` 只消费它已经写入的持久结果，不反向决定成长规则

## UI 入口设计

### 主入口：`PartyManagementWindow`

- 负责展示：
  - 当前成员基础资源与属性摘要
  - 职业与 rank
  - 已学技能与核心技能
  - 已解锁的 `MP / AURA` 资源链摘要
  - 成就摘要
  - 仓库 / 装备入口
- 不直接承载复杂结算逻辑，只负责把选择交给 runtime / service。

### 选择入口：`PromotionChoiceWindow`

- 负责处理职业解锁 / 晋升时的明确选择。
- 属于成长系统的强决策 UI，不是纯展示窗。
- 战斗内与世界态都可复用，但文案和刷新后果需要清晰区分。

### 奖励入口：通用角色奖励弹窗

- 继续复用现有 `PendingCharacterReward` 编排。
- 用于承接成就、研究、任务、战斗后奖励等统一确认。
- 其职责是“确认后入账”，不是额外维护一份成长状态。

### 新入口：功法替换确认弹窗

- 当角色已经持有一个冥想或修炼技能，又尝试学习第二个同轨技能时，系统必须先弹出替换确认。
- 该弹窗至少展示：
  - 旧技能名称 / 评级 / 当前等级
  - 新技能名称 / 评级
  - 按评级差换算后的新技能等级预览
  - 旧技能会被移出已学状态且进度清零的提示
- 只有玩家确认后，系统才正式：
  - 清空旧技能进度
  - 学会新技能
  - 消耗对应技能书或其它学习来源
- 取消时不改角色，也不消耗物品。

### 新入口：升级确认弹窗

- 当角色已满足升级条件时，不自动升级。
- 系统应先弹出升级确认，展示：
  - 目标等级
  - 本次触发升级的核心技能
  - 本次成长画像摘要
  - 预计增长的 1-3 项属性
- 只有玩家确认后，才正式写入：
  - `character_level`
  - 本次基础属性变化
  - 触发核心技能的锁定状态
  - 新一轮成长周期重置

### 轻量信息窗：`CharacterInfoWindow`

- 继续保持轻量，不承担完整成长详情页职责。
- 但在人物成长视角下，已经解锁的 `MP / AURA` 资源必须可见；未解锁时必须隐藏。

## 内容制作与数据驱动路线

### 当前正式策略

- 技能、职业、成就仍由 `ProgressionContentRegistry` 统一注册与校验。
- `GameSession` 负责缓存、分发与只读访问。
- `ItemDef` 已经资源化，说明装备/物品内容更适合继续走资源文件。
- 功法成长相关内容也应继续走 registry / 只读缓存路线，不在 UI 或 runtime coordinator 内部硬编码。

### 下一阶段策略

- 将 `skill_defs / profession_defs / achievement_defs` 逐步迁移到 `data/configs/` 下的资源文件。
- Registry 保留：
  - 扫描
  - 校验
  - 索引
  - 过渡桥接
- 真相源逐步从“硬编码注册”迁到“资源定义 + 启动校验”。
- 功法成长额外需要一份显式内容表：
  - `practice_tier`
  - 职业白名单加成
  - 知识白名单加成
- 这部分应与 `SkillDef / ProfessionDef / known_knowledge_ids` 一起被统一缓存，不应散在多个 service 常量里。

## 分阶段落地建议

### Phase 1：固化当前成长基线

- 以本设计文档作为总纲。
- 明确“人物等级 = 职业 rank 总和”成为正式规则。
- 明确“冥想 / 修炼解锁 `MP / AURA` 资源链、同轨技能互斥、跨天自动成长”成为正式规则。
- 明确“同一时间只有一个核心技能可由玩家手动激活并触发升级”“触发后立即锁定但继续可战斗使用”“激活核心技能达到 `non_core_max_level` 后弹职业选项，确认后同事务结算”这三条规则。
- 把战斗内晋升即时刷新、职业选择确认后立即写回、奖励延迟确认、装备外部构筑这几条边界固定下来。
- 补齐成长相关文档之间的引用关系，避免后续继续各写各的。

### Phase 2：补足成长来源闭环

- 让 `training / research / quest / settlement action` 成为正式成长来源。
- 用同一 `PendingCharacterReward` 链路承接成长型奖励。
- 把声望与知识真正接入职业、研究、据点服务，而不是只停留在字段层。
- 把功法替换确认、按日成长结算、`current_aura` 持久化一起接进正式 runtime / headless 链路。

### Phase 3：加深构筑深度

- 装备限制、装备授予技能、职业激活条件、技能融合内容正式化。
- 让职业差异体现在：
  - 核心技能要求
  - 激活条件
  - rank 授予技能
  - 属性与抗性轮廓
- 保持“技能是输入、职业是框架、装备是外部修饰”的结构不变。

### Phase 4：全面数据驱动

- 职业、技能、成就迁到资源目录。
- 建立统一校验流程：
  - 重复 id
  - 非法引用
  - 错误门槛配置
  - 非法 mastery 曲线
  - 奖励定义不完整

## 测试边界

### 序列化回归

- `PartyState / PartyMemberState / UnitProgress / PendingCharacterReward` round-trip 不丢字段。
- 存档版本严格升级，不要求兼容旧存档缺字段 payload。

### 技能与职业回归

- 学技能、涨熟练、升等级、设核心、分配职业、职业晋升、职业授予技能全链路稳定。
- 融合技能不会破坏来源追踪与重学阻断。

### 功法成长回归

- 学会首个 `meditation` 技能后，`mp_max/current_mp` 立即获得起始值并变为可见。
- 学会首个 `cultivation` 技能后，`aura_max/current_aura` 立即获得起始值并变为可见。
- 未解锁时，队伍界面、人物信息窗、战斗 HUD 都不会显示对应资源链。
- 学第二个同轨功法技能时，会先进入替换确认，而不是直接学习成功。
- 确认替换后，新技能等级应按 `old_current_level + (old_tier - new_tier)` 换算并 clamp。
- 替换结果允许为 `0`，0 级已学技能是合法状态。
- 替换后旧技能的 `skill_level/current_mastery/total_mastery_earned` 必须清零。
- 旧功法允许再次学习，但必须再次走同轨替换确认与等级换算，不能恢复历史熟练度。
- `1 天 = 15 world_step`；跨天时，全队已解锁成员都应执行一次或多次功法成长。
- 每日成长必须同时覆盖“上限增长”和“当前回复”，且回复量不等于当日上限增长值。
- 职业与知识对白名单加成的影响应稳定且可持久化，不因 UI 路径不同而漂移。

### 升级评价回归

- 升级时只读取“本成长周期”的战斗画像，不读取全历史累计，避免旧打法永久锁定属性成长。
- 等级数值增长只跟职业 rank 总和有关。
- 升级资格判定不读取战斗成就数量或战斗画像分值。
- 同一时间只能有一个核心技能处于激活态。
- 非激活核心技能不能触发当前等级提升。
- 触发升级并确认后的核心技能必须进入锁定态。
- 锁定后的核心技能仍然可以在战斗中使用。
- 锁定后的核心技能应获得 `bonus_to_hit_from_lock`。
- `bonus_to_hit_from_lock` 应能加成该技能的攻击命中、检定类法术与目标豁免 DC。
- 位移型核心技能占优时，升级结果应稳定偏向 `敏捷`。
- 守御型核心技能占优时，升级结果应稳定偏向 `体质` / `意志`。
- 核心技能评分与战斗表现冲突时，默认仍以核心技能权重为主。
- 手动激活核心技能达到 `non_core_max_level` 后，应展示全部符合条件的职业解锁 / rank 晋升选项。
- 玩家每次只能选择一个职业；确认后立即写入职业 rank、等级、属性变化和核心技能锁定。
- 单次升级最多只增长 3 项属性。
- 不使用待结算升级次数；如果某个来源会推动多个职业 rank，必须拆成多次职业选择确认。
- 连续两次升级之间，成长周期缓存会被正确清空并重新累计。

### 属性快照回归

- 基础属性、职业、技能、装备四层修正叠加结果稳定。
- 职业激活 / 隐藏切换后，快照能正确重建。
- `mp_max/aura_max` 在未解锁时输出 `0`，解锁后正确读取功法成长持久值。

### 奖励队列回归

- 成就、研究、任务奖励只入队一次。
- 入账顺序稳定。
- 已确认奖励不会重复应用。

### 战斗整合回归

- 战斗熟练度能推进永久进度。
- 战斗中确认职业晋升后，当前单位快照会刷新。
- 战斗中解锁的成就奖励不会立刻污染当前战斗面板。
- 战后 `current_hp/current_mp/current_aura` 提交与角色成长状态不冲突。

## 最终结论

玩家成长系统的正式定位应当是：

- 以 `UnitProgress` 为永久成长真源。
- 以“职业 rank 总和 -> 人物等级增长 -> 唯一激活核心技能触发升级”为等级主轴。
- 以“触发本次升级的核心技能类型”为属性成长主导因素。
- 以“战斗成就表现”为属性成长的次级修正因素。
- 以“核心技能触发后进入锁定态”来强制成长焦点轮换。
- 以“锁定后的核心技能继续战斗使用，并通过 `bonus_to_hit_from_lock` 获得攻击命中、检定类法术与目标豁免 DC 加成”来兑现核心技能的长期强度。
- 以“冥想 / 修炼功法技能”来解锁并持续抬升 `MP / AURA` 资源链。
- 以“15 world_step = 1 day”的跨天结算来承接全队功法成长与当前资源回复。
- 以知识、声望、成就作为分支解锁与横向激励。
- 以装备作为外部构筑层。
- 以 `CharacterManagementModule` 为唯一正式写入口。
- 以 `PendingCharacterReward` 为统一延迟结算容器。

这样既能保持当前仓库已实现链路不被推翻，也能为后续训练、研究、任务、装备深度和数据驱动迁移提供稳定地基。
