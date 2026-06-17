# 角色成长与 CharacterManagement 模块可重建规格说明

更新日期：`2026-06-17`

## 目标与边界

本文描述 PartyState、PartyMemberState、UnitProgress、技能/职业/成就/任务、属性服务、CharacterManagementModule 与奖励流的可重建规格。它覆盖世界、战斗、仓库、任务和 UI 共同依赖的角色成长真相源。

## 模块拓扑

```text
GameContentCatalog(typed skill/profession/achievement/quest/item/identity)
  -> CharacterManagementModule
    -> PartyState / PartyMemberState / UnitProgress
    -> Attribute services / Equipment projection / Progression rules
    -> QuestProgressService / Achievement progress / PendingCharacterReward
GameRuntimeFacade sidecars
  -> PartyManagementWindow / PromotionChoiceWindow / MasteryRewardWindow / CharacterInfoWindow
```

`PartyState` 是运行期真相源；content catalog 只提供只读定义。不要把角色运行态塞回 content catalog。

## PartyState 契约

PartyState 至少包含：active members、reserve members、leader member id、warehouse、quest state、achievement progress、pending rewards、货币/声望等队伍级字段。active/reserve 编成必须保持成员 id 唯一，leader 必须指向存在成员或归一 fallback。

PartyMemberState 包含 member id、display name、identity（race/subrace/age/bloodline/faith 等）、base attributes、equipment state、UnitProgress、当前资源/状态摘要。

## UnitProgress 契约

UnitProgress 是技能/职业成长 owner：

- skill progress 和 profession progress 正式承载面是 internal typed dictionary；Godot dictionary 只作边界投影。
- combat resource id 合法性由 `CombatResourceIds` 统一拥有，不在 UnitProgress/BattleUnitState 复制 HashSet。
- `merged_skill_source_map` 等来源映射应保持 typed owner，避免 UI dictionary 成为业务态。

## 内容定义

- `SkillDef` / `CombatSkillDef`：技能 id、等级上限、消耗、目标、效果、special profile id。
- `ProfessionDef`：职业 id、rank gate、promotion requirement、granted skills、属性成长。
- `AchievementDef`：触发条件、progress key、reward。
- `QuestDef`：provider、objectives、rewards、accept/complete/claim 条件。
- identity catalog：race/subrace/age/bloodline/faith/barrier/ascension/stage advancement 等 typed 定义。

所有 runtime 查询都应使用 typed `StringName` key。registry/content seed 可以把资源字典投成 typed catalog，但 runtime 不再用 string-key fallback。

## CharacterManagementModule Setup

setup 输入：PartyState、typed skill defs、profession defs、achievement defs、item defs、quest defs、equipment allocator、progression identity catalog。setup 后模块负责：

- 读取/写回 party state。
- 计算属性快照、装备视图、武器投影。
- 授予技能 mastery、职业经验、成就进度、任务进度。
- 创建 pending promotion / pending mastery reward。
- 处理技能书学习、任务奖励、战斗结算成长。

## 成长事件流

常见入口：

1. 战斗贡献/胜利 -> battle runtime 生成 mastery/contribution/loot -> writeback service 调 CharacterManagement。
2. 世界服务/任务 -> QuestProgressCommandPayloadData -> QuestProgressService / CharacterManagement。
3. 物品使用 -> PartyItemUseService -> LearnSkillOptionsData。
4. 世界时间跨天 -> daily practice growth。

所有成长结果应返回 typed delta/result；UI 只展示结果，不直接改 UnitProgress。

## 转职与奖励

- promotion choice 由 CharacterManagement 生成 pending prompt，UI 提交 profession id 和 selection。
- 成功转职写 UnitProfessionProgress、授予技能/属性/奖励，并清 pending prompt。
- mastery reward 进入 PendingCharacterReward 队列；确认奖励后再写 party state。
- reward 队列真源在 PartyState，不能只放 WorldMapSystem 内存。

## 任务与成就

Quest runtime setup 直接消费 typed `QuestDef`。任务 accept/progress/complete/claim 都必须校验 quest id/objective id、状态、提交物品、奖励空间。contract board 只投影 UI 字典，不反向驱动任务业务。

Achievement progress 应根据 typed event 更新，并把 reward 交给统一 reward flow。

## 属性与装备桥接

属性快照由 base attributes、identity、profession、equipment、status/reward modifiers 合并。装备视图必须通过 PartyEquipmentService/CharacterManagement 获取，战斗单位工厂从 CharacterManagement 读取正式快照和 known skill map。

## 回归入口

```bash
godot --headless -s res://tests/world_map/ui/run_party_management_window_regression.cs
godot --headless -s res://tests/world_map/ui/run_promotion_choice_window_schema_regression.cs
godot --headless -s res://tests/world_map/ui/run_character_info_identity_regression.cs
godot --headless -s res://tests/world_map/runtime/run_game_runtime_reward_flow_handler_regression.cs
```

## 安全约束

- 不恢复 public Godot dictionary 作为正式技能/职业/任务业务态。
- 不在 UI 或 GameRuntimeFacade 手写成长规则。
- 不做旧 schema compatibility，除非用户明确要求。
- save/schema 变更高风险，必须同步 serializer 和回归。

## 实现级补充：CharacterManagement 内部索引

setup 后应建立并持有以下 typed 索引：

- `Dictionary<StringName, SkillDef>`。
- `Dictionary<StringName, ProfessionDef>`。
- `Dictionary<StringName, AchievementDef>`。
- `Dictionary<StringName, ItemDef>`。
- `Dictionary<StringName, QuestDef>`。
- `ProgressionIdentityCatalogData`。

这些索引只从 catalog typed view 初始化。不要在运行中扫描 public Godot dictionary projection 补 key。

## 实现级补充：Party 编成不变量

- active/reserve member id 不得重复。
- member id 必须存在于 party member map。
- leader id 必须存在；若不存在，优先 active[0]，再 reserve[0]，再空。
- active 队伍容量由规则服务决定，UI 不能绕过。
- 移动成员 active/reserve 是队伍状态变更，必须持久化 PartyState。

## 实现级补充：技能成长

技能进度应包含：skill id、level/mastery exp、learn source、锁定/替换信息、source map。授予技能时：

1. 校验 skill def 存在。
2. 校验 learn requirements / knowledge / achievement / profession gate。
3. 如果已知技能，按规则提升等级或忽略重复。
4. 如果需要替换/选择，生成 pending reward/choice，不直接覆盖。
5. 写 UnitSkillProgress typed dictionary。

战斗 mastery grant 必须包含 source unit、skill id、source kind、贡献值，结果进入 CharacterProgressionDelta。

## 实现级补充：职业成长与转职

职业进度应包含 profession id、rank、exp、promotion records。转职流程：

1. 根据 ProfessionDef.rank gates 和 requirements 计算候选。
2. 生成 PendingProfessionChoice，记录 target rank map、trigger skill ids、候选职业。
3. UI 提交 profession id 后再次校验候选仍合法。
4. 写 UnitProfessionProgress、授予 profession granted skills、属性成长和奖励。
5. 清 pending choice，返回 progression delta。

不要让 UI 构造职业结果；UI 只提交选择。

## 实现级补充：Quest 状态机

QuestState 至少包含：quest id、status、objective progress、accepted/completed/claimed flags。状态流：

```text
not accepted -> active -> completed -> claimed
```

- accept 校验 provider/前置/是否允许 reaccept。
- progress 校验 objective id 和 payload source。
- submit item 先 preview warehouse 扣除，再更新 objective；失败不扣物品。
- complete 校验所有 required objectives。
- claim 发奖励，奖励入 reward flow/warehouse/party，再置 claimed。

## 实现级补充：Achievement 状态机

AchievementProgressState 记录 progress、unlocked、claimed/reward state。事件推进必须幂等；已解锁 achievement 不重复发奖励，除非定义允许 repeat。

## 实现级补充：属性快照

属性快照合并顺序建议固定：

1. UnitBaseAttributes。
2. identity modifiers（race/subrace/age/bloodline/faith 等）。
3. profession/rank modifiers。
4. skill passive / achievement modifiers。
5. equipment modifiers。
6. temporary status/reward modifiers。

同类 modifier 的叠加、乘法、上限由 AttributeService 拥有；调用方只请求 snapshot。

## 实现级补充：奖励队列

PendingCharacterReward 必须可序列化到 PartyState。奖励确认流程：

1. peek active reward。
2. UI 展示 reward choices。
3. confirm/choose 后调用 reward handler。
4. CharacterManagement 应用奖励。
5. 从队列移除并持久化。

如果持久化失败，不能丢失 reward queue。

## 实现级补充：回归映射

| 行为 | 回归 |
|---|---|
| party window/schema | `run_party_management_window_regression.cs` |
| promotion prompt schema | `run_promotion_choice_window_schema_regression.cs` |
| character info identity | `run_character_info_identity_regression.cs` |
| reward flow | `run_game_runtime_reward_flow_handler_regression.cs` |
| quest direct progress | `run_world_map_low_level_defensive_regression.cs` |
