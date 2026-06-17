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

## 源码级重建清单：成长/角色文件与 surface

以下清单用于弥补纯设计文档遗漏：重建时必须逐项恢复这些 owner 文件、公开/内部 typed surface 与职责边界；若实现拆文件，仍要保留等价 API 与行为。

### `scripts/systems/progression/CharacterManagementModule.cs`

- `public partial class CharacterManagementModule : RefCounted, IBattleRuntimeCharacterGateway`
- `internal sealed class LearnSkillOptionsData`
- `public LearnSkillOptionsData(bool confirmPracticeReplacement = false)`
- `private sealed class AttributeGrowthEntryData`
- `public AttributeGrowthEntryData(StringName attributeId, int amount)`
- `private sealed class AchievementProgressSummaryEntry`
- `public GDictionary ToDictionary() =>`
- `public sealed class DailyPracticeGrowthResult`
- `public GDictionary ToDictionary()`
- `public new void Dispose()`
- `public PartyState GetPartyState() => _party_state;`
- `public IReadOnlyDictionary<StringName, ItemDef> GetItemDefsTyped() => _item_def_index;`
- `public bool HasItemDefCatalog() => _item_def_index.Count > 0;`
- `public void SetPartyState(PartyState party_state)`
- `internal AttributeSourceContext build_attribute_source_context(StringName member_id) =>`
- `internal PassiveSourceContext BuildPassiveSourceContext(StringName member_id) =>`
- `public GDictionary GetIdentitySummaryForMember(StringName member_id)`
- `public bool RevokeBloodline(StringName member_id)`
- `public bool RevokeAscension(StringName member_id) => RevokeAscension(member_id, true);`
- `public bool RevokeAscension(StringName member_id, bool restore_original_race)`
- `public bool AddStageAdvancementModifier(StringName member_id, StringName modifier_id)`
- `public bool RemoveStageAdvancementModifier(StringName member_id, StringName modifier_id)`
- `public PartyMemberState GetMemberState(StringName member_id) =>`
- `public void SetMemberState(PartyMemberState member_state)`
- `public Godot.Collections.Array<PendingCharacterReward> GetPendingCharacterRewards() =>`
- `public Godot.Collections.Array<QuestState> GetActiveQuestStates() =>`
- `public Godot.Collections.Array<QuestState> GetClaimableQuestStates() =>`
- `public GStringNameArray GetClaimableQuestIds() =>`
- `public GStringNameArray GetCompletedQuestIds() =>`
- `public bool AcceptQuest(StringName quest_id) => AcceptQuest(quest_id, -1, false);`
- `public bool AcceptQuest(StringName quest_id, int world_step) =>`
- `public bool AcceptQuest(StringName quest_id, int world_step, bool allow_reaccept)`
- `public bool CompleteQuest(StringName quest_id) => CompleteQuest(quest_id, -1);`
- `public bool CompleteQuest(StringName quest_id, int world_step)`
- `internal QuestClaimResultData ClaimQuestRewardTyped(StringName quest_id, int world_step)`
- `public AttributeSnapshot GetMemberAttributeSnapshot(StringName member_id)`
- `internal WeaponProjection GetMemberWeaponProjectionTyped(StringName member_id)`
- `public StringName GetMemberWeaponPhysicalDamageTag(StringName member_id)`
- `public bool LearnSkill(StringName member_id, StringName skill_id) =>`
- `public bool LearnKnowledge(StringName member_id, StringName knowledge_id) =>`
- `public LevelGrowthTriggerResult ClearActiveLevelTriggerCoreSkillTyped(StringName member_id)`
- `public DailyPracticeGrowthResult ApplyDailyPracticeGrowthTyped(int days_elapsed)`
- `public GStringNameArray RecordAchievementEvent(StringName member_id, StringName event_type) =>`
- `public bool UnlockAchievement(StringName member_id, StringName achievement_id) =>`
- `public CharacterProgressionDelta ApplyPendingCharacterReward(PendingCharacterReward reward)`
- `public GDictionary GetMemberAchievementSummary(StringName member_id)`
- `public void CommitBattleDeath(StringName member_id)`
- `public void CommitBattleKo(StringName member_id) => CommitBattleDeath(member_id);`
- `public int FlushAfterBattle() => (int)Error.Ok;`
- `public ItemDef GetItemDef(StringName itemId) =>`
- `private sealed class PendingCharacterRewardEntryData`
- `private sealed class QuestSubmitItemPreviewData`
- `private sealed class QuestObjectiveDefData`
- `public static QuestObjectiveDefData FromVariant(Variant value)`
- `public static QuestObjectiveDefData FromDictionary(GDictionary data)`
- `private sealed class QuestRewardData`
- `public static QuestRewardData Missing() =>`
- `public static QuestRewardData FromDictionary(GDictionary questData)`
- `public static QuestRewardData FromQuestDef(QuestDef questDef)`
- `private sealed class QuestRewardEntryData`
- `internal GArray CloneEntries() => _entries.Duplicate(true);`
- `public static IReadOnlyList<QuestRewardEntryData> FromArray(GArray rewardEntries)`
- `public static QuestRewardEntryData FromVariant(Variant value)`
- `public static QuestRewardEntryData FromDictionary(GDictionary data)`
- `public static QuestRewardEntryData FromQuestRewardEntry(QuestDef.RewardEntryData entry)`
- `private sealed class QuestRewardPreviewData`
- `internal GArray CloneItemRewards() => _itemRewards.Duplicate(true);`
- `public List<StringName> CloneWarehouseDepositItemIds() =>`
- `internal Godot.Collections.Array<PendingCharacterReward> ClonePendingCharacterRewards() =>`
- `public GStringNameArray CloneUnsupportedRewardTypes() =>`
- `private sealed class QuestItemRewardPreviewData`
- `internal GDictionary CloneItemReward() => _itemReward.Duplicate(true);`
- `public List<StringName> CloneWarehouseDepositItemIds() =>`
- `public static QuestItemRewardPreviewData Failed(string errorCode) =>`
- `private sealed class QuestPendingCharacterRewardPreviewData`
- `public static QuestPendingCharacterRewardPreviewData Failed(string errorCode) =>`
- `private static class CharacterQuestDataReader`
- `internal static string ReadString(GDictionary data, string key)`
- `internal static bool TryReadString(GDictionary data, string key, out string result)`
- `internal static string ReadTrimmedString(GDictionary data, string key) =>`
- `internal static StringName ReadStringName(GDictionary data, string key)`
- `internal static bool TryReadInt(GDictionary data, string key, out int result)`
- `internal static GArray ReadArray(GDictionary data, string key)`
- `internal static GStringNameArray ReadStringNameArray(GDictionary data, string key)`

### `scripts/systems/progression/ProgressionService.cs`

- `public partial class ProgressionService : RefCounted`
- `public void RefreshRuntimeState()`
- `public bool LearnKnowledge(StringName knowledgeId)`
- `public bool LearnSkill(StringName skillId)`
- `public bool CanLearnSkill(StringName skillId)`
- `public bool GrantSkillMastery(StringName skillId, int amount, StringName sourceType)`
- `public bool SetSkillCore(StringName skillId, bool enabled)`
- `public int RecalculateCharacterLevel()`
- `public bool CanPromoteProfession(StringName professionId)`
- `public bool PromoteProfession(StringName professionId, GDictionary selection = null)`
- `public static int CalculateProfessionHitPointGain(int hitDieRoll, int constitutionValue)`
- `public static int CalculateConstitutionModifier(int constitutionValue)`
- `public Godot.Collections.Array<PendingProfessionChoice> GetProfessionUpgradeCandidates()`
- `public bool IsSkillRelearnBlocked(StringName skillId)`

### `scripts/systems/progression/QuestProgressService.cs`

- `public sealed class QuestProgressService`
- `internal static StringName ToStringName(QuestProgressEventKind kind)`
- `internal static QuestProgressEventKind ToEventKind(StringName eventType)`
- `public void Dispose()`
- `public PartyState GetPartyState() => _party_state;`
- `public List<QuestState> GetActiveQuestsTyped()`
- `public List<QuestState> GetClaimableQuestsTyped()`
- `public List<StringName> GetClaimableQuestIdsTyped()`
- `public List<StringName> GetCompletedQuestIdsTyped()`
- `public bool AcceptQuest(StringName questId, int worldStep = -1, bool allowReaccept = false)`
- `public bool CompleteQuest(StringName questId, int worldStep = -1)`
- `public bool MarkCompleted(StringName questId)`
- `public bool ClaimReward(StringName questId, GDictionary claimContext = null)`
- `public Godot.Collections.Array<GDictionary> GetQuestProgressEvents(StringName questId)`
- `internal static IEnumerable<QuestProgressEventData> ReadEventOptions(GArray eventOptions)`
- `private sealed class QuestActiveObjectiveMatch`
- `public QuestActiveObjectiveMatch(QuestState questState, QuestObjectiveDefData objectiveDef)`
- `internal sealed class QuestProgressEventData`
- `public static QuestProgressEventData FromVariant(Variant value)`
- `internal static QuestProgressEventData FromDictionary(GDictionary data)`
- `internal GDictionary ToDictionary() => _sourceData.Duplicate(true);`
- `internal GDictionary BuildContext()`
- `private sealed class QuestObjectiveDefData`
- `public static QuestObjectiveDefData FromVariant(Variant value)`
- `public static QuestObjectiveDefData FromDictionary(GDictionary data)`
- `private static class QuestProgressDataReader`
- `public static bool HasKey(GDictionary data, string key)`
- `public static bool TryGet(GDictionary data, string key, out Variant value)`
- `internal static StringName ReadStringName(GDictionary data, string key)`
- `internal static bool TryReadInt(GDictionary data, string key, out int result)`
- `internal static bool TryReadBool(GDictionary data, string key, out bool result)`
- `internal static bool HasDictionary(GDictionary data, string key)`
- `internal static GDictionary ReadDictionary(GDictionary data, string key)`
- `internal sealed class QuestProgressApplyResultData`
- `public bool ContainsProgressedQuest(StringName questId) =>`
- `public void AppendAcceptedQuestId(StringName questId) => AppendUnique(_acceptedQuestIds, questId);`
- `public void AppendProgressedQuestId(StringName questId) =>`
- `public void AppendClaimableQuestId(StringName questId) =>`
- `public void AppendCompletedQuestId(StringName questId) =>`
- `public GStringNameArray CloneAcceptedQuestIds() => _acceptedQuestIds.Duplicate();`
- `public GStringNameArray CloneProgressedQuestIds() => _progressedQuestIds.Duplicate();`
- `public GStringNameArray CloneClaimableQuestIds() => _claimableQuestIds.Duplicate();`
- `public GStringNameArray CloneCompletedQuestIds() => _completedQuestIds.Duplicate();`
- `public GDictionary ToDictionary()`
- `internal sealed class QuestProgressEventContextData`
- `public GDictionary ToDictionary()`

### `scripts/systems/progression/ProfessionAssignmentService.cs`

- `public sealed class ProfessionAssignmentService`
- `public bool CanAssignCoreSkillToProfession(StringName skill_id, StringName profession_id)`
- `public bool AssignCoreSkillToProfession(StringName skill_id, StringName profession_id)`
- `public bool RemoveCoreSkillFromProfession(StringName skill_id, StringName profession_id)`
- `public bool CanPromoteNonCoreToCore(StringName skill_id, StringName profession_id)`
- `public bool PromoteNonCoreToCore(StringName skill_id, StringName profession_id)`
- `public IReadOnlyList<StringName> GetProfessionCoreSkillIds(StringName profession_id)`
- `public StringName GetSkillAssignedProfession(StringName skill_id)`

### `scripts/systems/progression/PracticeGrowthService.cs`

- `public sealed class PracticeGrowthService`
- `internal static StringName ToStringName(PracticeTrackKind kind)`
- `internal static PracticeTrackKind ToTrackKind(StringName trackType)`
- `public StringName GetTrackTypeForSkill(StringName skillId)`
- `public int GetPracticeTier(StringName skillId)`
- `public static int ResolveTierValue(StringName tierName)`
- `public static StringName ResolveTierName(int tierValue)`
- `public StringName GetActivePracticeSkill(UnitProgress unitProgress, StringName trackType)`
- `public bool ApplyReplacement(StringName newSkillId, UnitProgress unitProgress)`
- `public void ApplyDailyGrowthToMember(PartyMemberState memberState, int daysElapsed)`
- `public static string GetTrackDisplayName(StringName trackType)`
- `public static string GetTierDisplayName(int tierValue)`

### `scripts/systems/progression/AttributeGrowthService.cs`

- `public sealed class AttributeGrowthService`
- `public void Setup(UnitProgress unitProgress)`
- `public static int GetTierBudget(StringName growthTier) =>`
- `public static bool IsValidGrowthTier(StringName growthTier) =>`
- `public static bool IsValidAttributeId(StringName attributeId) =>`

### `scripts/systems/progression/CharacterProgressionDelta.cs`

- `public partial class CharacterProgressionDelta : RefCounted`
- `public void SetLeveledSkillIds(IEnumerable values)`
- `public void AddLeveledSkillId(StringName skillId)`
- `public void SetGrantedSkillIds(IEnumerable values)`
- `public void AddGrantedSkillId(StringName skillId)`
- `public void SetChangedProfessionIds(IEnumerable values)`
- `public void AddChangedProfessionId(StringName professionId)`
- `public bool HasChangedProfessionId(StringName professionId)`
- `public void SetPendingProfessionChoices(IEnumerable values)`
- `public void AddPendingProfessionChoice(PendingProfessionChoice choice)`
- `public void SetMasteryChanges(IEnumerable values)`
- `public void AddMasteryChange(CharacterMasteryChangeFact change)`
- `public void AppendMasteryChanges(IEnumerable<CharacterMasteryChangeFact> values)`
- `public void SetUnlockedAchievementIds(IEnumerable values)`
- `public void AddUnlockedAchievementId(StringName achievementId)`
- `public void AppendUnlockedAchievementIds(IEnumerable<StringName> values)`
- `public void SetKnowledgeChanges(IEnumerable values)`
- `public void AddKnowledgeChange(CharacterKnowledgeChangeFact change)`
- `public void AppendKnowledgeChanges(IEnumerable<CharacterKnowledgeChangeFact> values)`
- `public void SetAttributeChanges(IEnumerable values)`
- `public void AddAttributeChange(CharacterAttributeChangeFact change)`
- `public void AppendAttributeChanges(IEnumerable<CharacterAttributeChangeFact> values)`

### `scripts/player/progression/PartyState.cs`

- `public partial class PartyState : RefCounted`
- `public PartyMemberState GetMemberState(StringName id)`
- `public bool HasMemberState(StringName id) => GetMemberState(id) != null;`
- `public List<PartyMemberState> GetMemberStates()`
- `public bool IsMemberDead(StringName id)`
- `public StringName GetResolvedMainCharacterMemberId() =>`
- `public bool GetFateRunFlag(StringName id, bool defVal = false)`
- `public bool HasFateRunFlag(StringName id) => GetFateRunFlag(id);`
- `public void SetFateRunFlag(StringName id, bool en = true)`
- `public void ClearFateRunFlag(StringName id)`
- `public Godot.Collections.Dictionary CaptureFateRunFlags()`
- `public void ApplyFateRunFlags(Godot.Collections.Dictionary flags)`
- `public bool GetMetaFlag(StringName id, bool defVal = false)`
- `public bool HasMetaFlag(StringName id) => GetMetaFlag(id);`
- `public void SetMetaFlag(StringName id, bool en = true)`
- `public void ClearMetaFlag(StringName id)`
- `public void RemoveMemberFromRosters(StringName id)`
- `public List<QuestState> GetActiveQuestsTyped() => new(active_quests);`
- `public List<QuestState> GetClaimableQuestsTyped() => new(claimable_quests);`
- `public List<StringName> GetCompletedQuestIdsTyped() => new(completed_quest_ids);`
- `public int GetGold() => Mathf.Max(gold, 0);`
- `public PartyState DuplicateState()`
- `public void SetGold(int v) => gold = Mathf.Max(v, 0);`
- `public int AddGold(int a)`
- `public bool CanAfford(int amount) => GetGold() >= Mathf.Max(amount, 0);`
- `public bool SpendGold(int amount)`
- `public void SetMemberState(PartyMemberState ms)`
- `public void RemoveMemberState(StringName id) => member_states.Remove(id);`
- `public void EnqueuePendingCharacterReward(PendingCharacterReward r)`
- `public PendingCharacterReward GetPendingCharacterReward(StringName rid)`
- `public PendingCharacterReward GetNextPendingCharacterReward() =>`
- `public bool RemovePendingCharacterReward(StringName rid)`
- `public QuestState GetActiveQuestState(StringName qid)`
- `public bool HasActiveQuest(StringName qid) => GetActiveQuestState(qid) != null;`
- `public QuestState GetClaimableQuestState(StringName qid)`
- `public bool HasClaimableQuest(StringName qid) => GetClaimableQuestState(qid) != null;`
- `public QuestState GetQuestState(StringName qid)`
- `public void SetQuestState(StringName qid, QuestState q)`
- `public void SetActiveQuestState(QuestState q)`
- `public void SetClaimableQuestState(QuestState q)`
- `public bool RemoveActiveQuest(StringName qid)`
- `public bool RemoveClaimableQuest(StringName qid)`
- `public List<StringName> GetActiveQuestIdsTyped()`
- `public List<StringName> GetClaimableQuestIdsTyped()`
- `public bool HasCompletedQuest(StringName qid) => completed_quest_ids.Contains(qid);`
- `public void AddCompletedQuestId(StringName qid)`
- `public bool MarkQuestClaimable(StringName qid, int ws = -1)`
- `public bool MarkQuestCompleted(StringName qid, int ws = -1) => MarkQuestClaimable(qid, ws);`
- `public bool MarkQuestRewardClaimed(StringName qid, int ws = -1)`
- `public Godot.Collections.Dictionary ToDictionary()`
- `public static PartyState FromDictionary(Godot.Collections.Dictionary data)`

### `scripts/player/progression/PartyMemberState.cs`

- `public partial class PartyMemberState : RefCounted`
- `public PartyMemberState()`
- `public PartyMemberState DuplicateState()`
- `public int GetHiddenLuckAtBirth()`
- `public int GetFaithLuckBonus()`
- `public int GetEffectiveLuck()`
- `public int GetCombatLuckScore()`
- `public int GetDropLuck()`
- `public Godot.Collections.Dictionary ToDictionary()`
- `public static PartyMemberState FromDictionary(Godot.Collections.Dictionary data)`

### `scripts/player/progression/UnitProgress.cs`

- `public partial class UnitProgress : RefCounted`
- `public void SetSkillProgress(UnitSkillProgress sp)`
- `public UnitSkillProgress GetSkillProgress(StringName sid) =>`
- `public void RemoveSkillProgress(StringName sid)`
- `public void SetProfessionProgress(UnitProfessionProgress pp)`
- `public UnitProfessionProgress GetProfessionProgress(StringName pid) =>`
- `public void RemoveProfessionProgress(StringName pid)`
- `public void SetAchievementProgressState(AchievementProgressState aps)`
- `public AchievementProgressState GetAchievementProgressState(StringName aid) =>`
- `public bool HasKnowledge(StringName kid) => kid != "" && HasStringName(_knownKnowledgeIds, kid);`
- `public bool LearnKnowledge(StringName kid)`
- `public void SyncActiveCoreSkillIds()`
- `public bool IsSkillRelearnBlocked(StringName sid) =>`
- `public void BlockSkillRelearn(StringName sid)`
- `internal List<StringName> GetMergedSourceSkillIdsTyped(StringName sid)`
- `internal List<StringName> GetMergedSourceSkillIdsRecursiveTyped(StringName sid)`
- `public void SyncDefaultCombatResourceUnlocks()`
- `public bool HasCombatResourceUnlocked(StringName rid) =>`
- `public bool UnlockCombatResource(StringName rid)`
- `public void SetKnownKnowledgeIds(IEnumerable values) => SetUniqueStringNames(_knownKnowledgeIds, values);`
- `public void SetActiveCoreSkillIds(IEnumerable values) =>`
- `public void SetAttributeGrowthProgress(IEnumerable<KeyValuePair<StringName, int>> values)`
- `public bool TryGetAttributeGrowthProgressAmount(StringName attributeId, out int amount)`
- `public void SetAttributeGrowthProgressAmount(StringName attributeId, int amount)`
- `public void SetPendingProfessionChoices(System.Collections.IEnumerable values)`
- `public void AddPendingProfessionChoice(PendingProfessionChoice choice)`
- `public void SetBlockedRelearnSkillIds(IEnumerable values) =>`
- `public void SetUnlockedCombatResourceIds(IEnumerable values) =>`
- `public bool HasLockedLevelTriggerSkillId(StringName skillId) =>`
- `public void SetLockedLevelTriggerSkillIds(IEnumerable values) =>`
- `public void AddLockedLevelTriggerSkillId(StringName skillId) =>`
- `public void RemoveLockedLevelTriggerSkillId(StringName skillId) =>`
- `public UnitProgress DuplicateState()`
- `public Godot.Collections.Dictionary ToDictionary()`
- `public static UnitProgress FromDictionary(Godot.Collections.Dictionary data)`
- `internal List<StringName> GetSortedSkillIdsTyped()`
- `internal List<StringName> GetSortedProfessionIdsTyped()`

### `scripts/player/progression/QuestState.cs`

- `public partial class QuestState : RefCounted`
- `public bool IsActive() => status_id == StatusActive;`
- `public bool IsCompleted() => status_id == StatusCompleted || status_id == StatusRewarded;`
- `public bool IsTerminal() => status_id == StatusRewarded || status_id == StatusFailed;`
- `internal static StringName ToStringName(QuestStatusKind kind)`
- `internal static QuestStatusKind ToStatusKind(StringName statusId)`
- `public int GetObjectiveProgress(StringName objectiveId)`
- `public int RecordObjectiveProgress(StringName objectiveId, int delta)`
- `public bool IsObjectiveComplete(StringName objectiveId, int targetValue = 0)`
- `public bool IsObjectiveComplete(StringName objectiveId)`
- `public bool HasCompletedAllObjectives(QuestDef questDef)`
- `public void MarkAccepted(int worldStep = -1)`
- `public void MarkCompleted(int worldStep = -1)`
- `public void MarkRewardClaimed(int worldStep = -1)`
- `public void MarkFailed()`
- `public QuestState DuplicateState()`
- `public Godot.Collections.Dictionary ToDictionary()`
- `public static QuestState FromDictionary(Godot.Collections.Dictionary payload)`

### `scripts/player/progression/PendingProfessionChoice.cs`

- `public partial class PendingProfessionChoice : RefCounted`
- `public void SetTriggerSkillIds(IEnumerable values) => SetUniqueStringNames(_triggerSkillIds, values);`
- `public void AddTriggerSkillId(StringName skillId) => AddUniqueStringName(_triggerSkillIds, skillId);`
- `public void SetCandidateProfessionIds(IEnumerable values) =>`
- `public void AddCandidateProfessionId(StringName professionId) =>`
- `public void SetQualifierSkillPoolIds(IEnumerable values) =>`
- `public void AddQualifierSkillPoolId(StringName skillId) =>`
- `public void SetAssignableSkillCandidateIds(IEnumerable values) =>`
- `public void AddAssignableSkillCandidateId(StringName skillId) =>`
- `public void SetTargetRank(StringName professionId, int targetRank)`
- `public bool TryGetTargetRank(StringName professionId, out int targetRank)`
- `public PendingProfessionChoice DuplicateState()`
- `public GDictionary ToDictionary() =>`
- `public static PendingProfessionChoice FromDictionary(GDictionary data)`

