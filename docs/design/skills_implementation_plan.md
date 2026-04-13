# 战士战棋技能系统落地方案

> 源需求文档：`docs/design/skills.md`

## 1. 结论摘要

这份 `skills.md` 不适合在当前项目里另起一套平行系统，应该直接落到现有的资源驱动战斗链路中：

- 技能定义链路沿用 `SkillDef -> CombatSkillDef -> CombatEffectDef`
- 战斗执行链路沿用 `BattleRuntimeModule -> BattleDamageResolver -> BattleGridService`
- 战斗主入口以 `scripts/systems/world_map_system.gd` 为准
- `scripts/systems/game_runtime_facade.gd` 仍需保持同等行为，因为测试与工具链还会走这条链路

本次落地采用以下边界：

- 文档中的战士技能作为正式技能体系引入
- 这些技能全部按“技能书 / 通用技能”模式接入，`learn_source = &"book"`
- 不绑定职业主动授予，不塞回 warrior 主动职业树
- 保留现有基础武器技能和职业被动
- 旧 warrior 主动技能进入兼容保留态，不再作为新内容继续扩展
- 文档中的 `aura / 斗气` 新增为独立资源，不与 `MP` 或 `Stamina` 混用

## 2. 当前项目现状

### 已有能力

- 已有技能资源结构：`SkillDef`、`CombatSkillDef`、`CombatEffectDef`、`CombatCastVariantDef`
- 已有战斗指令与预览：`BattleCommand`、`preview_command()`、`issue_command()`
- 已有地面技能、范围技能、冲锋技能雏形
- 已有 `化石为泥` 这类地形改造技能，已经通过 `ground + cast_variants + terrain_replace / height_delta` 链路运行
- 已有 AI 决策与战斗 HUD
- 已有技能注册中心：`scripts/player/progression/progression_content_registry.gd`

### 当前缺口

- `cooldown_tu`、`stamina_cost` 已有字段，但运行时没有完整扣费与推进闭环
- 没有独立 `Aura` 资源链
- `hit` 命中率只是设计字段，运行时尚未真正结算命中/闪避
- `status_effects` 目前只是松散字典，没有统一状态语义表
- 单位状态当前虽然可写 `duration`，但实际是在 `BattleRuntimeModule._advance_unit_turn_timers()` 中按单位激活时 `-1`，本质仍是按行动轮次，不是按 `TU` 时间轴
- 范围图形目前主要支持 `single / diamond / square / cross`，尚未完整支持文档中的 `line / cone / radius / self`
- AI 还不是按文档中的评分模型做技能候选打分
- 现有单位技能执行链一次命令只做一次静态结算，不支持“命中后递归再次攻击，并在每段动态修改伤害倍率、资源消耗和命中率”的循环型追击语义
- 现有技能效果链还没有正式支持“击杀目标后返还行动点并授予当前回合免费移动额度”的击杀刷新语义
- 现有技能效果链还没有正式支持“命中后按装备槽位概率破坏目标装备，并受装备稀有度保护”的装备破坏语义
- 现有 `mage_chain_lightning` 只是 `ground + cross AOE` 的占位实现，尚未具备“单体锁定后递归连锁、潮湿扩展半径、不可重复命中”的真实语义
- 现有单位技能指令协议只稳定覆盖“单目标单位”与“地面多格”，尚未正式支持“单次施法选择多个敌方单位”
- 现有冲锋链路只负责移动、阻挡、击退与陷阱，不支持“每前进一步触发一次路径 AOE”的步进攻击语义
- `SkillDef.learn_requirements` 当前只适合表达“已学会某些前置技能”，还不足以直接表达“知识 + 指定技能等级 + 指定成就解锁”的复合升级条件

## 3. 设计落点

### 3.1 文档字段到现有结构的映射

| 设计文档字段 | 项目落点 |
| --- | --- |
| `name` | `SkillDef.display_name` |
| `range` | `CombatSkillDef.range_value` |
| `area` | `CombatSkillDef.area_pattern / area_value`，必要时补 `area_origin_mode / area_direction_mode` |
| `hit` | 新增 `CombatSkillDef.hit_rate` |
| `damage` | `CombatEffectDef.power` + 新增 `damage_ratio_percent` |
| `cost` | `CombatSkillDef.stamina_cost / aura_cost` |
| `cooldown` | `CombatSkillDef.cooldown_tu` |
| `effects` | `CombatSkillDef.effect_defs` 或 `CombatCastVariantDef.effect_defs` |
| `duration` | 单位状态与地形效果统一映射为 `duration_tu`；单位状态不再使用回合数 |

### 3.2 技能来源策略

- 文档里的 80 个技能全部注册为通用技能书技能
- warrior 仍然可以学，但不是职业专属授予
- 职业体系继续负责被动、属性倾向和少量基础授予
- 技能书投放、奖励和掉落后续再接入，不放在这次战斗落地里

### 3.3 资源策略

- 保留现有 `MP` 资源，供法系和旧技能继续使用
- 新增 `Aura` 资源，专门承接文档里的斗气系技能
- 保留 `Stamina`，承接近战体力类技能
- 战士文档技能根据描述分流到 `stamina_cost` 或 `aura_cost`

### 3.4 现有地形技能兼容性

- 新框架必须同时覆盖三类技能：单位技能、地形改造技能、持续地场技能
- `化石为泥` 作为兼容基准，继续沿用 `target_mode = ground`、`cast_variants`、`footprint_pattern`、`allowed_base_terrains`、`terrain_replace`、`height_delta`
- `StatusDef` 只统一单位状态语义，不吞并 `terrain_replace`、`height_delta`、`terrain_effect` 这些地形效果类型
- `hit_rate` 视为可选语义，纯地形改造技能默认不强制参与命中/闪避结算
- 需要明确区分两层形状语义：
  - `footprint_pattern` 负责玩家选点形状，继续兼容 `化石为泥` 已有的 `single / line2 / square2`
  - `area_pattern` 负责选点后的影响扩散，用于本次补齐的 `line / cone / radius / self`

## 4. 需要新增或扩展的系统

### 4.1 数据层

需要扩展以下资源或状态对象：

- `SkillDef`
  - 新增 `unlock_mode`
  - 新增 `knowledge_requirements`
  - 新增 `skill_level_requirements`
  - 新增 `achievement_requirements`
  - 新增 `upgrade_source_skill_ids`
  - 新增 `retain_source_skills_on_unlock`
  - 新增 `core_skill_transition_mode`
- `CombatSkillDef`
  - 新增 `hit_rate`
  - 新增 `aura_cost`
  - 新增 `area_origin_mode`
  - 新增 `area_direction_mode`
  - 新增 `ai_tags`
  - 新增 `target_selection_mode`
  - 新增 `min_target_count`
  - 新增 `max_target_count`
  - 新增 `selection_order_mode`
- `CombatEffectDef`
  - 新增 `damage_ratio_percent`
  - 新增 `forced_move_mode`
  - 新增 `forced_move_distance`
  - 新增 `stack_limit`
  - 新增 `bonus_condition`
  - 新增 `trigger_event`
  - 新增 `effect_type = chain_damage`
  - `chain_damage` 通过 `params` 约定 `chain_shape`、`base_chain_radius`、`wet_chain_radius`、`bonus_terrain_effect_id`、`prevent_repeat_target`
  - 新增 `effect_type = path_step_aoe`
  - `path_step_aoe` 通过 `params` 约定 `step_shape`、`step_radius`、`allow_repeat_hits_across_steps`、`apply_on_successful_step_only`
  - 新增 `effect_type = repeat_attack_until_fail`
  - `repeat_attack_until_fail` 通过 `params` 约定 `same_target_only`、`base_hit_rate`、`follow_up_hit_rate_penalty`、`follow_up_damage_multiplier`、`follow_up_cost_multiplier`、`cost_resource`、`stop_on_miss`、`stop_on_insufficient_resource`、`stop_on_target_down`、`consume_cost_on_attempt`、`damage_multiplier_stage = pre_resistance`
  - 新增 `effect_type = on_kill_gain_resources`
  - `on_kill_gain_resources` 通过 `params` 约定 `ap_gain`、`free_move_points_gain`、`grant_scope = current_turn`、`stack_on_multiple_kills`、`require_target_defeated_by_same_skill`
  - 新增 `effect_type = break_equipment_on_hit`
  - `break_equipment_on_hit` 通过 `params` 约定 `base_break_chance`、`max_broken_items`、`slot_weight_map`、`slot_break_chance_map`、`rarity_resistance_mode`、`break_resistance_override_field`、`require_damage_applied`、`destroy_instead_of_unequip`
- `BattleUnitState`
  - 新增 `current_aura`
  - 新增 `current_free_move_points`
  - 规范化 `cooldowns`
  - 规范化 `status_effects`
  - 新增 `combo_state`
- `BattleCommand`
  - 新增 `target_unit_ids`
  - 保留 `target_unit_id` 作为单目标技能与兼容链路
- `UnitSkillProgress`
  - `merged_from_skill_ids` 改为纯血缘记录，不再隐含“源技能已被删除”
- `UnitProgress`
  - 合并后只刷新 `active_core_skill_ids / profession core skill` 分配，不移除旧技能进度

补充约束：

- `SkillMergeService.merge_skills()` 调整为“非破坏式合并”语义
- 合并后源技能继续保留在角色的已学会技能列表中，原有熟练度、等级与可用性不丢失
- 合并后不再对源技能执行 `remove_skill_progress()` 与 `block_skill_relearn()`
- 合并行为主要影响角色的核心技能集合与职业核心技能分配：源技能可退为非核心，结果技能进入新的核心位
- `merged_from_skill_ids` 只承担来源血缘、UI 展示与追溯职责，不再表示源技能已被吞并删除

建议新增 `StatusDef` 注册表，集中定义以下状态语义：

- `bleeding`
- `staggered`
- `armor_break`
- `taunted`
- `rooted`
- `disarmed`
- `knockup_delay`
- `guarding`
- `counter_stance`
- `damage_reduction_up`
- `attack_up`
- `hit_rate_up`
- `evasion_up`

补充约束：

- `StatusDef` 注册表只管理单位状态，不替代地形效果注册
- `CombatEffectDef.effect_type` 中已有的 `terrain_replace`、`height_delta`、`terrain_effect` 继续保留为一等能力
- `StatusDef` 需要补齐 `default_duration_tu`、`tick_interval_tu`、`timing_basis = timeline_tu` 一类字段，明确单位状态一律按时间轴计时
- 单位状态文档与数据层不再接受 `1 回合 / 2 回合` 的持续时间写法；旧文档若仍写 `duration`，也必须解释为 `duration_tu`

### 4.2 属性与角色构建

需要让以下链路都认识 Aura：

- `AttributeService`
- `CharacterManagementModule`
- `BattleUnitState`
- `GameSession` 存档序列化
- 战斗 HUD 展示

默认规则：

- `aura_max` 默认为 0
- 只有属性、装备、被动或技能显式授予时，单位才拥有 Aura
- `current_aura` 初始等于 `aura_max`

### 4.3 战斗执行层

`BattleRuntimeModule` 需要补全统一执行顺序：

1. 校验指令和目标
2. 校验 AP / Stamina / Aura / 冷却
3. 扣资源
4. 写入冷却
5. 结算命中
6. 结算伤害 / 治疗 / 状态 / 地形 / 强制位移
7. 处理击杀、行动刷新、追击、日志

需要补的核心能力：

- 技能冷却按 TU 推进
- 单位状态、持续增益、持续减益、控制效果统一按 TU 推进
- 命中与闪避结算
- 地形技能继续沿用 `terrain_replace / height_delta / timed terrain` 执行链，不改成临时特判
- 强制位移：击退、拉拽、击飞、后撤、跳斩落点
- 条件伤害：低血加成、无视护甲、暴击强化、斩杀刷新
- 追击次数上限：2 次
- 链式递归效果：从首目标开始按稳定顺序展开，维护 `visited_unit_ids`，直到没有新的合法目标
- 地形联动条件：链式效果直接读取 `BattleCellState.terrain_effect_ids / timed_terrain_effects`，不把“潮湿扩散”下沉到属性层
- 多目标单位技能：允许一次施法锁定多个单位，逐个结算命中/伤害/状态，并保持预览、日志与实际执行顺序一致
- 路径步进效果：在 charge / dash 类位移过程中，于每次成功前进后触发一次局部 AOE，支持同一敌人在不同步段被重复命中
- 击杀资源刷新：允许技能在确认“该技能本次结算击倒目标”后，立即返还施法者 `AP` 与“当前回合免费移动额度”
- 免费移动额度语义：`free_move_points` 只服务当前回合内的移动命令，移动时优先消耗 `free_move_points`，不足部分才继续扣 `AP`
- 免费移动额度生命周期：默认在回合结束时清空，不带入下一轮时间轴，也不等同于 `speed`
- 装备破坏：允许技能在命中后，对目标的已装备物品执行一次受槽位权重与稀有度抗性影响的破坏判定
- 装备破坏规则：装备破坏默认作为显式战斗特效例外，可在战斗结算中直接改写目标的装备状态，而不是走普通奖励队列
- 单位状态计时改造：`status_effects` 需要从当前的 `duration -= 1 / 每次单位激活` 语义，迁移为基于 `BattleTimelineState.current_tu` 的 `remaining_tu / expires_at_tu / next_tick_at_tu`
- 单位状态与地形持续效果共用时间基准：地形持续效果已使用 `duration_tu + tick_interval_tu + current_tu`，单位状态应对齐这一套时序模型
- `_advance_unit_turn_timers()` 不再负责递减单位状态持续时间，只保留与“单位轮到行动”强相关的刷新逻辑

### 4.3.1 链式闪电技能定义

本节用于覆盖当前项目中 `mage_chain_lightning` 的占位实现，确认它后续应回归为真正的链式闪电，而不是保留 `ground + cross AOE` 的临时语义。

- 技能标识：暂沿用 `mage_chain_lightning`
- 施放方式：`target_mode = unit`，锁定单个敌方单位作为首目标
- 数值边界：在未单独评审数值前，沿用当前技能的 AP / MP / 伤害 / 附带 `shocked` 状态等基础数值，不在这次规则确认里改动
- 首跳结算：首目标直接承受一次技能伤害与附加状态
- 连锁模型：使用递归或等价的队列遍历，不是“只选一个下家”的单路径跳跃
- 扩散中心：每一跳都以“刚刚被命中的单位所在格”作为新的搜索中心
- 默认扩散形状：`square`
- 默认扩散半径：`1`
- 潮湿扩散半径：若当前被命中的单位所在格带有 `wet` 地格效果，则该次扩散半径改为 `2`
- 邻接解释：`square` 半径判定包含斜对角，等价于切比雪夫距离
- 高差限制：当前跳点与候选目标之间要求高低差 `<= 1`
- 候选过滤：
  - 只攻击敌方单位
  - 只攻击存活单位
  - 已经进入 `visited_unit_ids` 的单位不可再次命中
- 多目标扩散：若当前节点周围存在多个合法敌人，则这些敌人全部进入后续连锁，而不是只取其中一个
- 稳定顺序：为保证预览、日志与实战一致，候选扩散顺序固定为“先距离近，再按 `y/x` 坐标顺序”
- 停止条件：当前节点找不到新的合法敌人时，该分支终止；当所有分支都终止时，整次技能结算结束
- 地形语义：`wet` 视为地格效果标识，不新增为 base terrain，也不通过 `DerivedAttributeRule` 表达

### 4.3.2 连珠箭技能定义

本节用于定义弓箭手的“多目标单位技能”基准实现，避免后续再为单个技能补一次单位多选协议。

- 技能标识：建议使用 `archer_multishot`
- 技能名称：连珠箭
- 施放方式：`target_mode = unit`
- 目标选择模式：`target_selection_mode = multi_unit`
- 目标数量：至少选择 `2` 个敌方单位，具体上限由 `max_target_count` 配置；首版建议默认 `3`
- 选择方式：玩家在技能释放阶段依次点击多个敌方单位，达到最小数量后允许确认施放
- 目标约束：
  - 每个目标都必须是敌方存活单位
  - 每个目标都必须单独通过射程校验
  - 若技能要求视线，则每个目标都必须单独通过 LOS 校验
  - 同一单位不可重复选择
- 多体型单位：对大体型单位点击任一占位格，都归并到同一个 `unit_id`
- 结算顺序：按玩家选择顺序逐个结算，日志、预览高亮与实际伤害顺序保持一致
- 结算语义：同一次施法对每个选中目标各执行一次独立的单体攻击结算，不共享命中结果，也不自动扩散到周围单位
- 资源语义：整次技能只扣一次 AP / Stamina / Aura / CD，不按目标数重复扣费
- 失败语义：若确认时未达到最小目标数，则技能不可释放；若确认时某个目标已失效，则整次命令重新校验并拒绝执行
- AI 语义：AI 使用该技能时应能产出稳定的 `target_unit_ids` 列表，优先选择可击杀、低血或高威胁目标组合

### 4.3.3 旋风斩技能定义

本节用于定义战士“冲锋 + 旋斩”合并后的升级技能，作为路径触发型位移 AOE 的标准样例。

- 技能标识：建议使用 `warrior_whirlwind_slash`
- 技能名称：旋风斩
- 技能定位：战士对 `charge` 与近身旋斩技能的合并升级技能
- 施放方式：沿用冲锋类技能的选点方式，玩家选择同一行或同一列的目标格
- 位移语义：只要当前步成功前进 `1` 格，就立刻触发一次旋斩；如果总共前进 `N` 格，就总共触发 `N` 次旋斩
- 触发时机：每一步成功落位后触发，不在起始格预先触发，也不对失败步或被中断后未走出的步数补发
- 旋斩范围：每次触发都以战士当前所在格为中心执行一次近身 AOE；首版建议沿用现有旋斩类技能的 `diamond radius 1`
- 目标过滤：
  - 只攻击敌方存活单位
  - 每一步内，同一单位至多结算一次该步旋斩
  - 不维护整次技能级别的 `visited_unit_ids`
- 多重命中：允许同一名敌人在不同步段被重复命中；如果敌人沿冲锋路径持续处于旋斩范围内，可以吃到多次伤害
- 资源语义：整次技能只扣一次 AP / Stamina / Aura / CD，不按步数重复扣费
- 中断语义：若冲锋被地形、阻挡、陷阱或其他事件中断，只保留已经成功走出的步数及其已触发的旋斩，不补后续步段伤害
- 日志语义：日志需要同时体现“向某方向冲锋了多少格”与“沿途共触发多少次旋斩、命中了多少个单位”
- AI 语义：AI 评估该技能时，不只看终点收益，还要累加整条路径上每一步旋斩的总收益

### 4.3.4 圣剑连斩技能定义

本节用于定义“连击 + 斗气斩”的复合升级技能，并明确它遵循新的非破坏式技能合并规则。

- 技能标识：建议使用 `saint_blade_combo`
- 技能名称：圣剑连斩
- 技能定位：`连击` 与 `斗气斩` 的合并升级技能
- 解锁模式：`unlock_mode = composite_upgrade`
- 解锁前提必须同时满足：
  - 已解锁知识 `康普尼亚家族传承`
  - `连击` 技能等级达到 `5`
  - `斗气斩` 技能等级达到 `5`
  - 已完成战斗成就“6连击”
- 成就语义：这里要求的是“对应成就已经解锁”，不是单场临时打出过一次后立即直接授予
- 成就标识：首版建议在成就表中为“6连击”建立独立 `achievement_id`，供技能升级条件直接查询
- 角色范围：知识、技能等级、成就解锁状态全部按角色独立计算，只读取该角色自己的 `UnitProgress`
- 升级结果：满足前提后，`圣剑连斩` 进入可解锁状态；解锁动作本身可以走升级入口、奖励入口或统一的技能合并入口
- 源技能保留：解锁 `圣剑连斩` 后，`连击` 与 `斗气斩` 仍然保留，可继续使用、继续积累熟练度，也可继续作为其他系统的前置条件
- 核心技能变化：若 `连击` 与 `斗气斩` 原本占用了角色的核心技能位，则升级后它们可以退为非核心，`圣剑连斩` 进入新的核心技能集合
- 系统约束：该技能可以复用调整后的 `SkillMergeService.merge_skills()`，前提是该服务已经改成“保留源技能、只切核心技能”的非破坏式语义
- 血缘记录：如需在 UI 中展示“由哪些技能升级而来”，应记录 `upgrade_source_skill_ids`，不要复用 `merged_from_skill_ids` 的旧含义
- 战斗目标：`target_mode = unit`，锁定单个敌方单位
- 结算模型：采用“命中后追击”的循环型单体技能，而不是静态双段或固定多段技能
- 推荐运行时语义：`effect_type = repeat_attack_until_fail`
- 追击目标：所有后续追击都只作用于首个被选中的同一目标，不自动跳转其他敌人
- 首段结算：
  - 先扣整次技能的 `AP`
  - 再校验并扣除首段 `Aura`
  - 然后对当前目标做一次独立命中判定
  - 若首段未命中，则本次技能立即结束，不再产生后续追击
- 追击触发：
  - 只有上一段成功命中，才允许进入下一段追击
  - 每一段都是一次新的独立攻击结算，重新计算命中、伤害、状态与击倒
  - 若当前段命中且目标仍存活，则继续尝试下一段
- 停止条件：
  - 任意一段未命中
  - 目标已被击倒
  - 当前单位 `Aura` 不足以支付下一段追击消耗
  - 施法者失去行动资格或该技能被外部规则强制中断
- 段数上限：本技能默认不设固定段数上限，只受“未命中 / 目标倒地 / Aura 不足”控制
- 伤害倍率：
  - 第 1 段使用技能基准伤害
  - 从第 2 段开始，每一段都在“上一段的抗性结算前伤害”基础上乘 `2`
  - 也就是伤害倍率按 `1.0 -> 2.0 -> 4.0 -> 8.0 ...` 递增
  - `2x` 的作用阶段固定为抗性之前，不绕过目标抗性与减伤结算
- 伤害顺序：
  - 先按技能段倍率计算抗性前伤害
  - 再进入目标抗性 / 减伤结算
  - 最后做取整与最小伤害钳制
- 斗气消耗：
  - 第 1 段消耗技能基础 `Aura` 成本
  - 从第 2 段开始，每一段的 `Aura` 消耗都在上一段基础上翻倍
  - 也就是 `1x -> 2x -> 4x -> 8x ...`
  - 当前段只有在可支付该段 `Aura` 成本时才允许尝试
- 消耗时机：
  - 每一段在发起攻击判定前先扣除该段 `Aura`
  - 即使该段最终未命中，只要已经发起该段攻击，该段 `Aura` 也不会返还
- 命中率变化：
  - 第 1 段使用技能基础命中率
  - 从第 2 段开始，每一段都在上一段基础命中率上叠加固定递减惩罚
  - 建议通过 `follow_up_hit_rate_penalty` 配置控制每段递减值，而不是写死在技能执行分支
  - 命中率仍需走统一的 `hit_rate vs evasion` 命中/闪避结算，不单独绕过通用算法
- 命中率钳制：
  - 每一段命中率都继续按通用命中公式与系统上下限钳制
  - 如果递减后命中率已经压到不可命中，本质上会在该段自动停止连斩
- 资源语义：
  - `AP` 只在整次技能开始时扣一次
  - `Aura` 按段扣除并逐段翻倍
  - `CD` 只在整次技能开始时写入一次
- 日志语义：
  - 日志应明确显示第几段命中 / 未命中
  - 日志应明确显示该段伤害倍率、该段 `Aura` 消耗与连斩终止原因
- 预览语义：
  - 预览只保证首段目标合法、首段资源可支付和基础命中信息
  - 不要求在普通预览层精确展开“最多能追几段”，因为后续段数受逐段命中和动态 `Aura` 消耗影响
- AI 语义：
  - AI 评估该技能时，至少要考虑首段命中率、目标血量、当前 `Aura` 储量与前两到三段的理论收益
  - AI 不应把该技能当作固定双段技能评分

### 4.3.5 死亡收割技能定义

本节用于定义“高耗魔单体收割技”的标准语义，作为“击杀后返行动点 + 返当前回合免费移动额度”的样例技能。

- 技能标识：建议使用 `death_reap`
- 技能名称：死亡收割
- 技能定位：高魔耗单体斩杀技，主要价值不在稳定输出，而在成功收割后立刻重获节奏
- 施放方式：`target_mode = unit`
- 目标类型：敌方单体单位
- 资源语义：
  - 该技能使用 `MP` 作为主成本
  - `mp_cost` 设为明显高于同阶普通单体技能的高耗水平
  - 若后续需要职业差异，也可以允许装备或被动降低其 `mp_cost`，但基础设计仍按“极高魔耗”处理
- 伤害定位：
  - 首版建议作为单体高伤收割技，不强制附带 AOE 或连锁
  - 其核心奖励来自击杀后的资源刷新，而不是额外控制或持续伤害
- 推荐运行时语义：在常规单体伤害效果之外，叠加 `effect_type = on_kill_gain_resources`
- 触发条件：
  - 只有当前这次技能结算直接击倒目标时，才触发资源返还
  - 若目标在该技能结算后仍存活，则不给任何 `AP` 或移动力奖励
  - 若目标是被其他延迟伤害、地形效果或第三方追击补刀，则不视为本技能触发
- 行动点奖励：
  - 击杀成功后立刻返还固定数量 `AP`
  - 返回值由 `params.ap_gain` 配置
  - 返还后的 `AP` 立即进入当前行动回合，可用于继续施法、移动或等待
- 移动力奖励：
  - 文档中的“移动力”固定解释为“当前回合免费移动额度”，不直接改 `speed`，也不改时间轴推进
  - 运行时建议字段为 `current_free_move_points`
  - 击杀成功后立刻增加固定数量的 `free_move_points`
  - 返回值由 `params.free_move_points_gain` 配置
- 移动消耗顺序：
  - 角色在当前回合执行移动时，先消耗 `current_free_move_points`
  - 若移动成本超过剩余免费移动额度，差额部分再继续扣 `AP`
  - 这样该技能获得的“移动力”既独立于 `AP`，又不要求立即重构完整移动系统
- 生命周期：
  - `current_free_move_points` 只在当前回合有效
  - 回合结束后未用完的免费移动额度自动清空
  - 该额度不会带入下一轮，也不会转化为永久属性
- 叠加规则：
  - 同一回合内若多次通过合法来源触发该技能的击杀奖励，`AP` 与 `free_move_points` 可按配置叠加
  - 若后续发现节奏过强，可在 `params` 中补 `max_ap_gain_per_turn` 或 `max_free_move_points_per_turn`
- 日志语义：
  - 击杀成功时，日志需明确显示返还了多少 `AP`
  - 同时明确显示获得了多少“免费移动额度”
  - 若未击杀，则日志只记录伤害，不追加资源返还文本
- 预览语义：
  - 常规预览层只展示目标合法性、技能范围、基础伤害与高额 `MP` 消耗
  - 不把“是否能触发资源返还”当作确定性预览结果，因为它依赖目标剩余生命与最终伤害结算
- AI 语义：
  - AI 对该技能的评分应显著提高“可击杀目标”的优先级
  - 若目标大概率无法被本技能击杀，则其评分应明显低于稳定高收益技能
  - AI 还应把击杀后返还的 `AP + free_move_points` 视为后续接敌、脱离或二次施法的节奏收益

### 4.3.6 裂解术技能定义

本节用于定义“伤害 + 装备破坏”的法术技能，作为装备破坏语义的标准样例。

- 技能标识：建议使用 `spell_disjunction`
- 技能名称：裂解术
- 技能定位：高风险单体法术，核心价值在于瓦解目标构筑，而不只是造成一次法术伤害
- 施放方式：`target_mode = unit`
- 目标类型：敌方单体单位
- 资源语义：
  - 该技能使用 `MP` 作为主成本
  - `mp_cost` 应高于同阶常规单体伤害法术，但不要求像 `死亡收割` 那样极端高耗
- 基础效果：
  - 对目标先结算一次法术伤害
  - 在命中且伤害成功应用后，再进入装备破坏判定
  - 若技能未命中，则本次不触发任何装备破坏效果
- 推荐运行时语义：常规法术伤害效果 + `effect_type = break_equipment_on_hit`
- 目标前提：
  - 只有目标存在可追踪装备状态且当前至少装备了 1 件物品时，装备破坏判定才有意义
  - 若目标没有任何已装备物品，则 `裂解术` 仅造成伤害，不追加装备文本
- 破坏数量：
  - 单次技能结算默认最多破坏 `1` 件装备
  - 不做“一次命中同时碎多件装备”的首版语义
- 装备槽位差异：
  - 不同部位装备的被破坏概率必须不同
  - 推荐通过 `params.slot_weight_map` 与 `params.slot_break_chance_map` 双层表达：
    - `slot_weight_map` 决定更容易被抽中的部位
    - `slot_break_chance_map` 决定被选中后该部位的最终破坏难度
  - 默认建议让 `main_hand / off_hand / head` 高于 `body / accessory`
- 推荐首版槽位权重：
  - `main_hand = 30`
  - `off_hand = 20`
  - `head = 20`
  - `body = 10`
  - `accessory_1 = 10`
  - `accessory_2 = 10`
- 推荐首版槽位破坏系数：
  - `main_hand = 1.0`
  - `off_hand = 0.9`
  - `head = 0.85`
  - `body = 0.6`
  - `accessory_1 = 0.7`
  - `accessory_2 = 0.7`
- 稀有度抗性：
  - 装备稀有度越高，越难被破坏
  - 首版建议默认按装备 `rarity_tier` 映射破坏抗性，而不是在技能里硬编码
  - 推荐默认映射：
    - `common = 0%`
    - `uncommon = 15%`
    - `rare = 35%`
    - `epic = 60%`
    - `legendary = 80%`
  - 最终破坏率应在基础破坏率与槽位系数之后，再乘上 `(1 - rarity_break_resistance)`
- 破坏结算顺序：
  - 第一步：从目标当前已装备槽位中按 `slot_weight_map` 选出一个候选槽位
  - 第二步：读取该槽位装备的稀有度与可选 `break_resistance_override_field`
  - 第三步：按 `base_break_chance * slot_break_chance * rarity_modifier` 计算最终破坏率
  - 第四步：若判定成功，则销毁该槽位装备
- 破坏结果：
  - 破坏成功时，目标槽位装备直接从 `EquipmentState` 中移除
  - 被破坏装备不回到共享仓库，不视为普通卸装
  - 若未来启用唯一实例装备，则应销毁对应 `instance_id`
- 战斗与持久化边界：
  - 若目标是可持久化角色成员，则破坏结果应作为显式战斗后果写回其正式装备状态
  - 若目标是运行时敌方模板且没有正式装备状态，则该效果默认只做运行时 no-op 或日志提示，不要求伪造持久装备
- 保护规则：
  - 裂解术不应破坏“空槽”
  - 裂解术不应一次同时破坏多个同槽位装备
  - 若未来存在 `indestructible` 标签或特殊剧情装备，则应在装备层先拦截，直接使本次破坏失败
- 日志语义：
  - 命中但未抽中有效装备时，应明确提示“未影响装备”
  - 命中且判定失败时，应明确提示“装备承受冲击但未破坏”
  - 破坏成功时，应明确显示被破坏的部位和装备名
- 预览语义：
  - 常规预览层展示法术伤害、射程、资源消耗
  - 可以附带“有几率破坏装备”的说明，但不需要精确展开每个槽位概率
- AI 语义：
  - AI 使用该技能时，应优先选择“目标已装备高价值低稀有度装备”的对象
  - 若目标没有装备或全身高稀有度装备，技能评分应回落为普通单体法术伤害评分

### 4.4 范围与选点

`BattleGridService` 需要正式支持：

- `self`
- `line`
- `cone`
- `radius`

单位技能的目标协议补充如下：

- `single_unit`：沿用现有单目标单位技能协议
- `multi_unit`：允许单次施法选择多个单位目标，目标集合通过 `BattleCommand.target_unit_ids` 传入运行时
- `target_coords` 继续服务地面技能与地格多选，不拿来替代多目标单位技能的主协议
- `selection_order_mode = manual` 时，执行顺序与玩家点击顺序一致
- `selection_order_mode = stable` 时，执行顺序按运行时稳定排序规则归一化，供 AI 或自动补全技能复用

落地规则固定如下：

- 现有 `ground + cast_variant` 选点技能继续保留 `footprint_pattern` 校验，不与 `area_pattern` 混用
- `self` 以施法者占位为中心
- `line` 方向由施法者到目标格的向量确定
- `cone` 方向由施法者到目标格的向量确定
- `radius` 以目标格为中心
- 不引入单位朝向系统

### 4.5 AI

`BattleAiService` 改为候选技能评分制，优先级固定为：

1. 击杀目标
2. 控制高威胁单位
3. 保命
4. 位移接敌
5. AOE 最大收益

目标评分模型落为：

`击杀价值 + 威胁值 + 命中收益 + 地形收益 - 风险`

其中必须接入的技能语义：

- `挑衅` 会改变敌方目标选择
- 控制技能优先打高威胁单位
- 治疗/防御技能进入低血保命窗口
- 位移技能要同时评估接敌收益和落点风险

### 4.6 UI

`BattleHudAdapter` 与 `BattleMapPanel` 需要补充：

- Aura 展示
- 技能命中率展示
- 技能 CD 展示
- 技能资源不足禁用态
- 范围预览说明
- 多目标单位技能的选敌进度说明
- 地形技能的地格变化预览说明
- 状态与冷却数量之外的具体状态提示

## 5. 分阶段落地

### 阶段一：规则底座

目标：先让文档字段在现有系统中“有真实语义”。

本阶段完成：

- Aura 资源链
- 命中/闪避结算
- 冷却推进
- 状态注册表
- 强制位移框架
- 范围图形扩展
- AI 评分骨架
- HUD 资源与 CD 展示

### 阶段二：Demo 技能池

先实现文档推荐的 15 个技能：

- 重击
- 横扫
- 穿刺
- 裂甲斩
- 断头斩
- 冲锋
- 跳斩
- 后撤步
- 格挡
- 护盾墙
- 战斗回复
- 盾击
- 挑衅
- 战吼
- 真龙斩

这批技能用于验证：

- 单体输出
- 范围攻击
- 位移攻击
- 防御姿态
- 治疗
- 控制
- 团队增益
- 斗气资源

额外修正现有法师占位技能：

- `mage_chain_lightning` 由临时 `cross AOE` 改为本方案定义的真实链式闪电

额外新增弓箭手目标协议验证技能：

- `archer_multishot`

额外新增战士路径触发验证技能：

- `warrior_whirlwind_slash`

额外新增技能合并验证技能：

- `saint_blade_combo`

额外新增击杀刷新验证技能：

- `death_reap`

额外新增装备破坏验证技能：

- `spell_disjunction`

### 阶段三：补齐 80 技能

原则：

- 不再为单个技能新增临时 if/else 分支
- 全部通过统一 `effect/status/shape/condition` 模板继续扩展
- 连击类统一走 `combo_state`
- 控制类统一走状态语义
- 位移类统一走强制位移与特殊落点校验

## 6. 与现有内容的兼容策略

- 旧 warrior 主动技能不直接删除
- 旧技能 ID 继续保留在注册表里，避免旧存档或旧进度崩溃
- 新版本不再继续扩展旧 warrior 主动技能目录
- 新内容全部走通用技能书技能池
- 基础武器技能和职业被动维持现状
- 现有法师地形技能如 `化石为泥` 不迁移到临时兼容分支，后续继续复用统一的 `effect / shape / condition` 能力集

## 7. 推荐文件落点

核心修改会集中在以下区域：

- `scripts/player/progression/`
- `scripts/systems/`
- `scripts/ui/`
- `docs/design/`

其中最关键的文件会是：

- `scripts/player/progression/combat_skill_def.gd`
- `scripts/player/progression/combat_effect_def.gd`
- `scripts/player/progression/progression_content_registry.gd`
- `scripts/systems/battle_runtime_module.gd`
- `scripts/systems/battle_damage_resolver.gd`
- `scripts/systems/battle_grid_service.gd`
- `scripts/systems/battle_ai_service.gd`
- `scripts/systems/world_map_system.gd`
- `scripts/ui/battle_hud_adapter.gd`

## 8. 测试清单

### 规则测试

- 命中/闪避是否正确生效
- Aura / Stamina / AP / CD 是否正确校验与扣除
- 状态持续、刷新、叠层、控制递减是否正确
- 击退、拉拽、跳斩、后撤是否正确更新坐标与占位
- 击杀刷新行动是否只在满足条件时触发

### 技能测试

- 每个 Demo 技能至少覆盖“合法释放”与“非法目标”两类用例
- `挑衅` 必须验证 AI 转火
- `格挡 / 护盾墙` 必须验证减伤
- `断头斩` 必须验证低血增伤
- `真龙斩` 必须验证直线大范围预览与命中
- `mage_chain_lightning` 必须验证单体锁定、递归扩散、包含斜对角、不可重复命中、潮湿地格半径从 1 变 2
- `archer_multishot` 必须验证多单位选择、重复目标拒绝、选择顺序与结算顺序一致、单次施法只扣一次资源
- `warrior_whirlwind_slash` 必须验证每前进 1 格触发 1 次旋斩、冲锋中断时不补后续伤害、同一敌人可在不同步段重复命中
- `saint_blade_combo` 必须验证知识、双技能等级、成就解锁三类前置条件缺一不可，且解锁后 `连击` 与 `斗气斩` 仍保留、核心技能集合发生预期切换
- `saint_blade_combo` 必须验证首段命中后才会尝试下一段、任意一段未命中即停止、`Aura` 不足时不会继续发起下一段
- `saint_blade_combo` 必须验证每段伤害按抗性前 `2x` 递增、每段 `Aura` 消耗翻倍、每段命中率按配置递减
- `saint_blade_combo` 必须验证 `AP` 与 `CD` 整次技能只结算一次，而 `Aura` 按段独立扣除且未命中不返还
- `death_reap` 必须验证只有本技能直接击杀目标时才返还 `AP` 与免费移动额度，目标存活时不触发
- `death_reap` 必须验证免费移动额度只在当前回合有效，移动时优先消耗免费移动额度，不足部分再扣 `AP`
- `death_reap` 必须验证高额 `MP` 消耗会被正确校验与扣除，且免费移动额度不会改写 `speed` 或时间轴
- `spell_disjunction` 必须验证命中且伤害生效后才会进入装备破坏判定，未命中时不破坏任何装备
- `spell_disjunction` 必须验证不同槽位的装备被破坏概率不同，且高稀有度装备显著更难被破坏
- `spell_disjunction` 必须验证破坏成功后装备直接从目标槽位移除、不回仓；目标无装备时技能只造成伤害
- `化石为泥` 必须验证单格改泥、单格降层、双格连续降层、`2x2` 改泥四类现有形态仍可合法预览与施放
- `化石为泥` 必须验证地形限制校验、地格变化和坠落伤害仍正确生效

### UI 测试

- 技能槽禁用态是否正确反映 AP / Stamina / Aura / CD
- 选点技能是否正确显示选点进度
- 形态切换后预览是否正确刷新
- Aura 与状态展示是否可读
- 地形技能预览是否能显示地格影响信息，而不是只显示命中率文案

### AI 测试

- 击杀优先
- 控制高威胁优先
- 低血保命逻辑
- AOE 最大收益逻辑
- 位移接敌逻辑

## 9. 本方案的默认约束

- 本轮不做新的技能树 UI
- 本轮不做技能书掉落/商店/奖励完整投放系统
- 本轮只完成战斗内可用、可学、可显示、可测试的技能系统
- 运行时主逻辑以 `world_map_system.gd` 为准，但 `game_runtime_facade.gd` 必须保持同步
