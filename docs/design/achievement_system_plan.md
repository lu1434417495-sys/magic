# 角色成就系统 v1 方案

更新日期：`2026-04-06`

## Summary

- 新增“按角色独立追踪”的成就系统。每名角色只累计自己的成就进度，只领取作用于自己的奖励。
- v1 覆盖全游戏现有主链路：战斗、据点动作、成长事件都可以推进角色成就。
- 奖励类型固定支持：永久基础属性提升、技能熟练度、技能解锁、知识解锁。
- v1 不直接给予职业 rank，不直接跳过现有职业选择或晋升流程。
- 达成成就后进入统一奖励队列，使用弹窗确认后再正式入账；队伍管理窗口提供成就历史与进度查看入口。

## 当前仓库事实

- 角色成长真源当前挂在 `PartyMemberState.progression -> UnitProgress`。
- `GameSession` 当前通过 `party_state` 统一持久化角色成长数据，因此成就状态也应跟随 `party_state` 保存，而不是另开全局存档。
- 仓库已经存在“待处理奖励 -> 世界地图统一弹窗 -> 确认后入账 -> 存档”的成熟链路：
  - `BattleRuntimeModule.consume_battle_resolution_result()`
  - `GameRuntimeFacade._enqueue_pending_character_rewards(...)`
  - `MasteryRewardWindow.show_reward(...)`
  - `CharacterManagementModule.apply_pending_character_reward(...)`
- 当前 `CharacterInfoWindow` 只是轻量信息浮窗；真正适合承载角色成长详情的是 `PartyManagementWindow`。
- 当前 `tests/` 目录存在，但 progression 专项测试入口尚未落地，成就系统需要一起补建。

## 目标与非目标

### 目标

- 为每个角色记录独立的成就进度、达成状态和达成奖励。
- 让战斗、据点与成长事件都能复用同一套成就推进入口。
- 复用现有奖励弹窗编排能力，避免在战斗内即时修改角色并破坏当前运行时状态。
- 让成就奖励在持久化、重载、延迟确认、多弹窗排队下都保持稳定。

### 非目标

- 不做队伍共享成就。
- 不做可重复刷新的循环成就。
- 不做复合条件 DSL，不做 AND / OR 组合成就，不做时间窗成就。
- 不做成就直升职业 rank，也不做直接跳过职业选择的特殊路径。
- 不扩展 `CharacterInfoWindow` 作为成就主入口。

## 核心设计结论

### 成就归属

- 成就归属单位固定为 `member_id`。
- 任何成就事件都必须显式带 `member_id`；没有角色归属的事件不进入 v1 成就系统。
- 同一事件只允许推进一个角色或一组明确角色，不做“队伍自动平均分配”。

### 触发范围

- v1 覆盖以下三类事件源：
  - 战斗事件：`battle_won`、`enemy_defeated`、`skill_used`
  - 据点事件：`settlement_action_completed`
  - 成长事件：`skill_mastery_gained`、`skill_learned`、`knowledge_learned`、`profession_promoted`
- 所有事件统一收口到 `CharacterManagementModule.record_achievement_event(...)`。
- UI 层和窗口层不直接改成就状态，只允许发动作或消费已排队奖励。

### 奖励边界

- v1 奖励类型固定为：
  - `knowledge_unlock`
  - `skill_unlock`
  - `skill_mastery`
  - `attribute_delta`
- `attribute_delta` 只作用于角色自己的 `UnitBaseAttributes`。
- `skill_mastery` 只作用于该角色自己已拥有或本奖励先行解锁的技能。
- `knowledge_unlock` 和 `skill_unlock` 只作用于该角色自己的 `UnitProgress`。
- v1 不支持：
  - `profession_unlock`
  - `profession_rank_up`
  - 全队奖励
  - 装备发放
  - 世界资源发放

## 数据模型与持久化

### 内容定义

- 新增 `AchievementDef`，建议路径：`scripts/player/progression/achievement_def.gd`
- 新增 `AchievementRewardDef`，建议路径：`scripts/player/progression/achievement_reward_def.gd`
- v1 继续沿用当前 progression 内容的代码注册模式，由 `ProgressionContentRegistry` 统一持有 `_achievement_defs`，不单独引入新的资源加载器。
- `GameSession` 新增 `get_achievement_defs() -> Dictionary` 只读访问接口，与 `get_skill_defs()`、`get_profession_defs()` 对齐。

### 成就定义结构

- 每条成就固定包含：
  - `achievement_id: StringName`
  - `display_name: String`
  - `description: String`
  - `event_type: StringName`
  - `subject_id: StringName`
  - `threshold: int`
  - `rewards: Array[AchievementRewardDef]`
- `subject_id` 为空时表示该事件类型下的通用累计；非空时表示只统计某个技能、知识或动作对象。
- v1 的阈值判断固定为“累计值 `>= threshold` 即解锁一次”。

### 角色成就状态

- 新增 `AchievementProgressState`，建议路径：`scripts/player/progression/achievement_progress_state.gd`
- `UnitProgress` 新增：
  - `achievement_progress: Dictionary`
- `achievement_progress` 以 `achievement_id` 为 key，value 为 `AchievementProgressState`。
- `AchievementProgressState` 字段固定为：
  - `achievement_id: StringName`
  - `current_value: int`
  - `is_unlocked: bool`
  - `unlocked_at_unix_time: int`

### 待领奖励队列

- 新增 `PendingCharacterReward`，建议路径：`scripts/systems/pending_character_reward.gd`
- 新增 `PendingCharacterRewardEntry`，建议路径：`scripts/systems/pending_character_reward_entry.gd`
- `PartyState` 新增：
  - `pending_character_rewards: Array[PendingCharacterReward]`
- 队列真源固定放在 `PartyState`，而不是 `WorldMapSystem` 的纯内存数组。这样奖励在存档、重载、战斗结束后都不会丢。

### 奖励实例结构

- `PendingCharacterReward` 字段固定为：
  - `reward_id: StringName`
  - `member_id: StringName`
  - `member_name: String`
  - `source_type: StringName`
  - `source_id: StringName`
  - `source_label: String`
  - `summary_text: String`
  - `entries: Array[PendingCharacterRewardEntry]`
- `PendingCharacterRewardEntry` 字段固定为：
  - `entry_type: StringName`
  - `target_id: StringName`
  - `target_label: String`
  - `amount: int`
  - `reason_text: String`

### 存档策略

- 成就状态和待领奖励全部跟随 `party_state` 进入现有 `GameSession` 持久化链路。
- `ProgressionSerialization` 需要补齐以下对象的序列化与反序列化：
  - `AchievementProgressState`
  - `PendingCharacterReward`
  - `PendingCharacterRewardEntry`
- 旧存档兼容策略：
  - 旧 `UnitProgress` 没有 `achievement_progress` 时，默认空字典。
  - 旧 `PartyState` 没有 `pending_character_rewards` 时，默认空数组。
- v1 不额外提高 `SAVE_VERSION`，按字段缺省兼容处理。

## 事件来源与触发链路

### 战斗链路

- `BattleRuntimeModule` 在以下真实运行时节点发成就事件：
  - 技能成功施放后：发 `skill_used`
  - 击杀成立后：发 `enemy_defeated`
  - 玩家阵营获胜结算后：对存活或参与的玩家角色发 `battle_won`
  - 已有战斗内熟练度增长成功后：发 `skill_mastery_gained`
  - 战斗内职业晋升被确认后：发 `profession_promoted`
- 战斗中达成成就时：
  - 只更新 `UnitProgress.achievement_progress`
  - 只向 `PartyState.pending_character_rewards` 排队
  - 不立即把成就属性奖励反写到当前 `BattleUnitState`
- 成就奖励统一在战后回到世界地图后由奖励弹窗确认，再在下一次属性快照和后续战斗中体现。

### 据点链路

- `GameRuntimeSettlementCommandHandler.execute_settlement_action(...)` 返回成功结果后，由 runtime handler 归并奖励并推进后续据点完成链。
- `subject_id` 固定使用当前 `action_id` 或标准化 `service_type`，实现时二选一，但全仓必须统一。
- 如果据点动作本身还带来 mastery 奖励、技能或知识变化，仍继续走成长链路补发对应事件，不做据点层重复结算。

### 成长链路

- `CharacterManagementModule` 在以下成功动作后补发成就事件：
  - `grant_battle_mastery(...)` 或其它 mastery 成功入账后：发 `skill_mastery_gained`
  - `learn_skill(...)` 成功后：发 `skill_learned`
  - `learn_knowledge(...)` 成功后：发 `knowledge_learned`
  - `promote_profession(...)` 成功后：发 `profession_promoted`
- 所有成长类事件都以最终成功修改 `UnitProgress` 为准，不能基于“尝试过”发事件。

## 奖励模型与入账顺序

### 排队规则

- 一条成就首次达成时只允许入队一次奖励。
- 同一 `achievement_id` 再次收到事件时，如果 `is_unlocked == true`，只忽略，不再重复发奖。
- 一次事件允许推动多条成就同时达成；每条成就各自产生一条 `PendingCharacterReward`。

### 入账顺序

- `CharacterManagementModule.apply_pending_character_reward(...)` 的固定顺序为：
  1. `knowledge_unlock`
  2. `skill_unlock`
  3. `skill_mastery`
  4. `attribute_delta`
- 这样可以保证：
  - 先解锁知识，再允许后续职业规则刷新
  - 先解锁技能，再给该技能加熟练度
  - 属性变化最后统一落到角色持久状态

### 各奖励类型的执行规则

- `knowledge_unlock`
  - 调 `ProgressionService.learn_knowledge(...)`
  - 重复知识自动忽略
- `skill_unlock`
  - 调 `ProgressionService.learn_skill(...)`
  - 已学会技能自动忽略
- `skill_mastery`
  - 调 `ProgressionService.grant_skill_mastery(...)`
  - 如果技能尚未学会，且本奖励前序也未成功解锁，则该条目忽略
- `attribute_delta`
  - 调 `AttributeService.apply_permanent_attribute_change(...)`
  - 只改角色自己的 `UnitBaseAttributes`

### 奖励结果反馈

- `CharacterProgressionDelta` 需要扩展成就奖励相关反馈，至少补齐：
  - `unlocked_achievement_ids`
  - `knowledge_changes`
  - `attribute_changes`
- 现有 `mastery_changes`、`leveled_skill_ids`、`changed_profession_ids` 继续保留。

## UI 与交互编排

### 奖励弹窗

- 现有 `MasteryRewardWindow` 泛化为通用角色奖励弹窗，建议保留场景位置和弹窗编排职责。
- 文案从“技能感悟”改成更中性的角色奖励标题，但仍兼容 mastery-only 奖励展示。
- 弹窗正文按 `entry_type` 渲染，不再假设所有条目都是熟练度。
- 奖励弹窗仍保持“确认后生效”的强边界。

### 弹窗队列规则

- 世界地图成为所有待领奖励的统一消费入口。
- 当以下任一窗口打开时，奖励弹窗必须等待：
  - 据点窗口
  - 人物信息窗
  - 队伍管理窗
  - 职业选择窗
  - 当前奖励窗
- 关闭上述窗口后，再尝试展示下一条待领奖励。

### 队伍管理窗口

- `PartyManagementWindow` 新增成就摘要区，至少展示：
  - 已解锁成就数
  - 进行中成就数
  - 最近解锁成就名
  - 当前选中角色的前若干条进行中成就进度
- 队伍管理窗是 v1 唯一正式的成就浏览入口。

### 人物信息窗口

- `CharacterInfoWindow` 不扩展成就展示。
- 原因是该窗口当前只是轻量 hover/检查面板，不适合承载成长历史与列表信息。

## 与现有熟练度奖励链路的兼容策略

- `PendingCharacterReward` / `PendingCharacterRewardEntry` 是当前唯一真源。
- 统一读取策略：
  - `pending_character_rewards`
  - `entries`
- 训练、战后评分奖励、据点奖励都直接产出 canonical `PendingCharacterReward`。

## Public Interfaces / Runtime Data

- `GameSession`
  - `get_achievement_defs() -> Dictionary`
- `CharacterManagementModule`
  - `record_achievement_event(member_id: StringName, event_type: StringName, amount := 1, subject_id: StringName = &"", meta: Dictionary = {})`
  - `build_pending_character_reward(...)`
  - `enqueue_pending_character_rewards(reward_variants: Array) -> void`
  - `apply_pending_character_reward(reward: PendingCharacterReward) -> CharacterProgressionDelta`
  - `get_member_achievement_summary(member_id: StringName) -> Dictionary`
- `UnitProgress`
  - `achievement_progress: Dictionary`
- `PartyState`
  - `pending_character_rewards: Array`

### 事件枚举

- `battle_won`
- `enemy_defeated`
- `skill_used`
- `settlement_action_completed`
- `skill_mastery_gained`
- `skill_learned`
- `knowledge_learned`
- `profession_promoted`

## 实现顺序建议

1. 补数据结构与序列化。
2. 扩 `ProgressionContentRegistry`，接通 achievement defs。
3. 在 `CharacterManagementModule` 完成成就事件推进、解锁判定、奖励入队、奖励应用。
4. 把 `WorldMapSystem` 的奖励队列切到通用角色奖励。
5. 泛化奖励弹窗并补 `PartyManagementWindow` 成就摘要。
6. 最后接战斗事件、据点事件和成长事件埋点。

## Test Plan

- 新增独立的 achievement/progression headless 测试入口，不依赖当前空白的 progression 测试目录。
- 至少覆盖以下场景：
  - 同一事件只推进指定 `member_id` 的成就，不影响其他角色
  - 成就达到阈值后只解锁一次、只发一份奖励
  - 一次事件可同时解锁多条成就，奖励按队列顺序展示
  - `knowledge_unlock -> skill_unlock -> skill_mastery -> attribute_delta` 顺序稳定
  - 未确认的待领奖励会被 `party_state` 保存并在重载后恢复
  - 战斗中达成成就不会即时改当前 `BattleUnitState`
  - `pending_character_rewards` 在重载后仍可被正确结算
  - 队伍管理窗口能正确显示角色成就摘要

## 默认假设

- v1 的成就内容先使用代码注册，不在本次范围内额外建设外部资源编辑器。
- 成就奖励全部是角色自作用，不提供对其他角色、全队、世界状态的修改能力。
- 战斗中成就达成只做“记账和排队”，不做即时属性回写。
- 因成就奖励导致的职业可晋升，只通过现有职业选择链路暴露，不做特殊捷径。
