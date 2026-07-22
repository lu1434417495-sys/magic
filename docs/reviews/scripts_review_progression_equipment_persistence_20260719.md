# scripts/ 逐行代码检视报告：进度 / 装备 / 持久化

- 日期：2026-07-19
- 当前代码复核：2026-07-20
- 范围：`scripts/player/progression/`（含 `definitions/`、`equipment_abilities/`）、`scripts/player/equipment/`、`scripts/player/warehouse/`、`scripts/systems/progression/`、`scripts/systems/inventory/`、`scripts/systems/persistence/`，共约 284 个 `.cs` 文件。
- 方法：12 个批次逐行通读 + 跨文件调用契约双向核对（Grep/ReadFile 追踪消费方与被调方），仅报告已核实的正确性与缺陷问题，不含风格意见。
- 维度：正确性与缺陷（状态一致性、存档序列化、边界条件、异常路径、GodotSharp 互操作、跨文件契约）。
- 范围外说明：`BattleEquipmentAbilityRuntimeService.cs`（282KB）未逐行审，仅核对契约两侧；preview/AI scoring 与 execution 的数值一致性是主要残留风险区。

## 总览

| 严重度 | 数量 | 说明 |
|---|---|---|
| critical | 0 | — |
| high | 1 | 非法装备能力内容可在 validation 报错前使 registry rebuild 抛异常 |
| medium | 21 | 状态一致性、契约缺口、校验失效；其中部分 API 当前无生产调用 |
| low | 约 50 | 潜伏陷阱、死代码、边界隐患 |

**最重要的系统性模式**：仓库的存档反序列化是严格模式（任何不合规即 `return null` 整档拒载），而部分公开 API 仍能构造“能写出、读不回”的状态。当前正式事件流规避了其中若干路径，因此必须区分生产可达问题与潜伏 API 不变量，不能只看代码片段定级。

---

## 当前重点 findings

### [中，潜伏 API] H2. `RecordProgress` 就地 `MarkCompleted`，任务留在 `active_quests` → 读档拒收
`scripts/systems/progression/QuestProgressService.cs:142-169 + scripts/player/progression/PartyState.cs:573`
`RecordProgress`（public）在目标全部完成时对活引用就地 `MarkCompleted`，但不移入 `claimable_quests`（正确路径是 `CompleteQuest → MarkQuestClaimable`）。`active_quests` 中出现 `status="completed"` 的任务后，读档时 `FromDictionary` 对 active 列表中非 Active 状态直接 `return null` → 整档拒载。同类缺口：`PartyState.SetQuestState`（:274-288）把 `Failed` 路由进 active 列表，failed 任务同样无法通过校验。当前 `RecordProgress` 与 `MarkFailed` 均无调用方（事件流走 `ApplyProgressEvent`，路径正确），属潜伏 public API 炸弹——任何新调用方即毁档。建议删除或修复这两个 API。

### [中] H3. 非法 `last_progress_context` 抛未捕获异常 → 读档崩溃而非优雅失败
`scripts/player/progression/QuestState.cs:346-348`
`QuestProgressContext.FromDictionary` 对非法 payload **抛 `ArgumentException`**，而 `QuestState.FromDictionary` 只对 objective 条目包了 try/catch，对 `last_progress_context` 没有。异常向上穿透 `PartyState.FromDictionary` → `SaveSerializer.TryDecodePayload` → `GameSession.LoadCurrentPayload`，全链无 catch。手改/损坏/旧工具生成的存档读档时直接崩溃，违反本文件"invalid → return null"的既定约定。

### [高] H4. 装备能力 registry 在校验错误检查前调用会 throw 的投影 → 一个坏 .tres 崩掉整个内容加载
`scripts/player/progression/equipment_abilities/EquipmentAbilityContentRegistry.cs:101-103（配合 3072-3077）`
`Rebuild` 在 `ValidateBinding` 之后、检查 `errors.Count` 之前无条件 `ProjectBinding`。`ValidateConditionGroup` 对非法嵌套 group（`Array[Resource]` 混入非 `EquipmentAbilityConditionGroupDef`）只记录错误；但 `ProjectConditionGroup` 对同一情形直接 `throw InvalidOperationException`。结果：单个格式错误的 pack 让 `ProgressionContentRegistry.Rebuild`（启动/内容重载）抛未处理异常，所有其它 pack 也不加载。修复只需把 `ProjectBinding` 移入 `errors.Count == 0` 分支。现有回归未覆盖。

---

## Medium

### 存档/状态一致性

1. **`UnitProgress.cs:234-248` — `merged_skill_source_map` 成环 → `StackOverflowException`**。`_append_recursive_merge_source` 的 `visited.Add` 在递归之后执行，2-环（A↔B）无限递归；`RememberMergeSources` 只挡自环。唯一入口 `GetMergedSourceSkillIdsRecursiveTyped` 当前无调用方（dead code），一旦接线即 critical。
2. **任务时间戳校验不对称 → 毁档路径**（`QuestState.cs:322-336 + PartyState.cs:379-401`；另见 `QuestProgressService.cs:654`、`CharacterManagementModule.cs:782`）。`Completed` 允许 `completed_at_world_step=-1`，`Rewarded` 要求 ≥0；事件字典 `world_step` 只校验是 int 不校验 ≥0（`CreateProgress` 却拒绝负值）。以 -1 完成再领奖 → Rewarded + completed=-1 → 读档整包拒收。当前运行时调用方均传非负，潜伏。
3. **`UnitProgress.cs:120-137` — `_mergedSkillSourceMap` 派生状态残留**：技能合并来源被清空或技能被移除时旧 map 条目不删，且随 `ToDictionary` 持久化。下游消费者目前仅有 dead 方法，影响有限。
4. **`PartyEquipmentService.cs:483` — `EquipItemTyped` 忽略 `SetEquippedEntry` 返回值**：实例已从仓库取出后 Set 失败（item 不匹配/空 instance_id）→ 实例永久丢失却上报成功。当前不可触发（隐式不变量），无防御。同模式：`BattleChangeEquipmentResolver.cs:742`。
5. **`PartyWarehouseService.cs:689` — batch swap commit 丢弃 `_process_add` 返回值**：allocator 失败时 withdraw 已发生、deposit 未发生，批次仍报 Success → 物品丢失。同函数显式实例路径（674-685）有检查，纯 id 路径漏检。
6. **`GameSession.cs:1552-1564` — 存档两段式写入（payload → index）非原子**：payload 写成功、index 写失败/崩溃时，用户被告知"创建失败"的存档会被 `RebuildSaveIndexEntriesFromSaveFilesPlain` 自动复活进列表甚至自动加载。
7. **`GameSession.cs:1545-1550 + SaveSerializer.cs:340-342,397-398` — `_generation_definition == null` 时静默写出永远无法解码的存档**（meta 归一化失败 → `world_size_cells=0` → 空 meta），并清空 `_activeSaveMeta` 使后续每次保存重蹈覆辙。触发路径：`RestoreRuntimeState` 的 generation_config_path 错配。建议 meta 归一化失败时保存直接报错。
8. **`CharacterManagementModule.cs:1923-1956` — `PromoteProfession` 失败路径 delta 违反 before/after 不变量**：失败时 `character_level_after` 保持 0，同文件同类方法均对称处理（`after = before`）。现有消费方有 guard，潜伏。
9. **`BattleRuntimeModule.EndBattle`（:2161-2208）与 `CommitBattleResources` 契约缺口**：注释声称"提交前只读校验所有成员"，但只校验了 contingency；第 N 个成员 `member_not_found` 时前 N-1 个已写回 → half-committed。正常路径不易触发，契约与注释不符。

### 校验失效 / 校验-运行时双轨

10. **`SkillContentRegistry.cs:2799` — `IsAttackDamage` 的 `save_dc_mode == ""` 是死条件**（默认 `"static"`，校验层不允许空）→ `magical_missile` 必配校验完全失效。应改为 `SaveDcModeKind == BattleSaveDcMode.Static`。
11. **[2026-07-20 已修复] `SkillLevelDescriptionFormatter.cs` — `{=...}` 表达式渲染死循环**：表达式和变量改为单次替换；parse/execute 失败、自引用或间接循环字段显示 `[描述配置错误]`，后续字段继续渲染。内容校验同步拒绝空、未闭合及语法错误表达式，并已有 focused regression。
12. **`ProgressionContentRegistry.cs:719-726` — 装备能力生产校验 context 只填 `KnownTraitIds`/`KnownSkillIds`**：`status_id`/`damage_type`/槽位/tag 拼错在生产构建中完全不报（只有测试的合成 context 填全）。
13. **`ProgressionContentRegistry.cs:1979,2002` — 纯 Definition 校验路径交叉引用误报**：`ReplaceDefinitionsForValidation` 清空 `_skillDefs` 但交叉引用检查查它 → 只走 Definition 路径时合法引用被误报 missing（现有测试断言"错误数 ≥4"掩盖了误报）。
14. **装备能力 `reset_timing` 契约两侧不一致**（`EquipmentAbilityContentRegistry.cs:429-456` vs `BattleEquipmentAbilityRuntimeService.cs:5576-5589`）：registry 放行 `per_world_day`/`per_world_month`，运行时不认识 → 落入 per-turn 分支。当前内容未用，潜伏。
15. **`add_damage_dice` 多 term + flat_bonus 重复叠加**（`BattleEquipmentAbilityRuntimeService.cs:5899` vs `deal_damage` 的 `usedFlatBonus` 保护）：同一 `DiceExpressionDef` 两种 action 语义不同，registry 不限制组合。当前内容未触发。
16. **`grant_skill` action 运行时零消费者**（`EquipmentAbilityBuiltInHandlerSpecs.cs:149-153`）：校验投影齐全但无任何 trigger 分发处理它 → 作者写了静默无效。

### 内容-运行时断链 / 服务行为

17. **`warrior_aura_slash.tres` ↔ `SkillEffectiveMaxLevelRules.cs:100-136` — `aura_transformation_count` 生产零写入**：斗气斩动态上限（每次质变 +2 级）永不触发，永远等于 base 7。规则与测试正确，是接线缺口。
18. **`AgeStageResolver.cs:67-80` — `StageIndex == -1` 修饰符使"取最高进阶"守卫失效**：多修饰符共存时结果依赖枚举顺序，可能选到更低阶段。
19. **`RacialSkillGrantService.cs:140-141` — `RevokeOrphanMember` 不清理 profession `core_skill_ids` 悬空引用**：被回收的种族技能残留使 `core_skill_ids.Count >= rank` 虚增，阻塞后续合法核心晋升。

> 存疑（两批结论冲突，降级备查）：`PendingCharacterRewardPayload` 不序列化条目级 `mastery_source_type`。一批认定是存档丢失 + 预览/发放口径不一致；另一批核验后认为该字段仅 build 期过滤用、发放用 reward 级 `source_type`，非 bug。真实问题收敛为"预览门控（条目级）与实际发放（reward 级）口径可能不一致"，建议负责人确认设计意图。

---

## Low（按主题归并，均附 file:line）

**返回值忽略 / 失败静默**
- `PartyWarehouseService.cs:574-591` — `DepositEquipmentInstance` 无容量检查且 `CharacterBattleWritebackService.cs:228` 忽略其返回值，allocator 失败时装备静默丢失。
- `PartyWarehouseService.cs:230-291,443-483` — item def 缺失的装备实例永远无法移除（幽灵实例占格）。
- `PartyWarehouseService.cs:485-552` — `AddEquipmentInstanceTyped` 不查重复 instance_id。
- `PartyItemUseService.cs:146-151` — 先学技能后扣物品，扣减失败即"白学"（当前预检使不可达，顺序脆弱）。
- `ProgressionService.cs:203-211` — `GrantSkillMastery` 在 `effectiveMaxLevel<=0` 分支先改状态再返回 false。
- `CharacterManagementModule.cs:1671-1834` — `ApplyPendingCharacterReward` 对 member 缺失/全部条目失败时**无日志删除** pending reward（唯一会丢玩家奖励且无 GameLog 的路径，建议确认是否有意）。

**空引用 / 边界**
- `QuestContentValidator.cs:190,199,222` — null `itemDefs`/`enemyTemplates` NRE（入口容忍 null，内部不防）。
- `PendingProfessionChoice.cs:88` — `SetTargetRankMap` 对解析失败（null）直接 foreach → NRE；同文件 `FromDictionary` 有检查，不一致。
- `QuestDef.cs:318,323,429` — GDScript 侧置 null export 属性时 NRE。
- `QuestProviderContentRules.cs:30-32` — `ToProviderKind` 不防 null questDef。
- `UnitBaseAttributes.cs:201-204 / UnitProfessionProgress.cs:103 / UnitReputationState.cs:54 / AchievementProgressState.cs:31-34` — `FromDictionary` 对 `data==null` NRE（当前调用方均有守卫）。
- `QuestState.cs:111-114` — 2 参 `RecordObjectiveProgress` 重载 `targetValue=0` 转发，永远静默不记录（public API 陷阱）。
- `FaithRankDef.cs:46-49` — 配了 stat id 但 min_value=0 时门槛静默失效且无校验。
- `DerivedAttributeRule.cs:50-52` — `max<min` 配置错误静默退化为只夹下限。
- `EquipmentRequirementDefinition.cs:16-17,53-54` — `min_body_size>max_body_size` 无校验；`min_value=0` 的属性需求静默跳过（忘填即形同虚设）。
- `ContingencySetupTemplateDefinition.cs:81-96` — 必填 `timing` 只读不校验，非法值一路带成 `Unknown`。
- `AchievementRewardDef.cs:76-78` — amount 强转 int 无溢出检查，非数值型奖励静默接受回绕值。
- `ItemDefinition.cs:545-551 / ItemDef.cs:392-398` — `ApplyPriceBasisPoints` int32 溢出（价格 >214748 回绕为负）。

**序列化 / 存档**
- `PartyMemberState.cs:470-661` — 不交叉校验 `is_dead` 与 `current_hp`，不同步状态载入后行为分裂。
- `UnitProgress.cs:914-915` — `active_core_skill_ids` 读档后被静默重算覆盖，掩盖上游写档 bug。
- `UnitProgress.cs:507-510,561-564 + PartyState.SaveSnapshot.cs:121-122` — `DuplicateState`/`ToDictionary` 对源对象有写副作用（非纯读）。
- `UnitProgress.cs:276-294` — GDScript `skills` setter 不同步派生状态。
- `PartyState.cs:213-228,550-560` — `pending_character_rewards` 不查重 reward_id，可重复领取。
- `ContingencyMatrixSetupState.cs:144-146` — null Trigger/TargetResolver 序列化成功但反序列化必然失败。
- `EquipmentState.cs:185,196` — exact-field 严格模式爆炸半径大：任一多余字段/StringName key → 整个队伍成员加载失败。
- `UnitCustomStatMap.cs:74-92` — 接受与基础属性同名的自定义键，形成不可达的第二数据源。
- `WorldRuntimeData.cs:620-626` vs `SaveSerializer.cs:1423-1447` — Vector2I 校验接受 `{x,y}` 字典但读取只认原生类型 → 校验通过的数据静默置零。
- `SaveRepository.cs:49-55` — 读路径对"文件消失"竞态抛异常而非跳过候选，可中断自动加载。
- `SaveSerializer.cs:57` — 默认 `_save_version=12` 与 `GameSession.SaveVersion=13` 不一致，未 `Setup` 的新调用点会静默坏掉。
- `GameSession.cs:423-438` — `CloseNormal` 不冲刷 `HasPendingSave`；`NormalizeParsedPartyState` 的读档修复不标 dirty，修复落盘依赖后续偶然保存。`FlushPostDecodeSave`（:1638-1643）疑似本应用的冲刷点，现为死代码。
- `GameSession.cs:648-707` — `CreateNewSave` 异常路径跳过状态回滚，会话停在"新世界半装配"态。
- `GameSession.SaveIndexAndFileIO.cs:169-172` — `Peek` 填充的缓存抑制孤儿存档恢复。

**死代码 / 潜伏断链**
- `PartyWarehouseService.cs:82-97 + PartyEquipmentService.cs:164-168` — 3 参 `Setup` 重置 `_equipment_trait_roll_service`，商店购买/批量存入的装备不执行 `MintWithRolls`（战利品路径有 roll）；当前无 roll group 内容，潜伏分化。
- `ItemDefinition.cs:307-347` — 模板合并中 `MaxStack/IsStackable/Sellable` 永远被实例默认值覆盖，模板配置无法继承。
- `ItemDef.cs:172-177` — `GetSellPrice` 缺少 50% 防套利钳制（无生产调用方）。
- `SkillContentRegistry.cs:1028-1038 ↔ CombatSkillDef.cs:196-215` — 钳制 getter 使两处范围校验永不可达，非法值静默钳制。
- `CombatSkillDef.cs:148-161,370-380` — `level_overrides` getter 暴露可变字典，原地改写绕过缓存失效。
- `SkillDefinition.cs:326-332` — 运行时投影接受负 level 键（校验层拒绝），双轨语义。
- `IdentityContentRegistryBase.cs:203-228` — `_append_*_field_error` 空方法，7 个派生注册表 20 余处调用全是误导性死代码；display_name 非空校验标准与 TraitContentRegistry 不一致。
- `ProfessionContentRegistry.cs:77-94` — 默认 `Setup(null)` 静默跳过全部技能引用校验；`Rebuild` 不刷新技能快照。
- `QuestContentRegistry.cs:46-50` — 任务扫描只取顶层 `.tres`（其它注册表均递归 + 接受 `.res`），子目录任务静默忽略。
- `ContingencyTriggerState.cs:19-29` — `ContingencyTimingKind` 枚举无映射/消费方，死代码，存在与字符串双真相漂移风险。
- `CombatResourceIds.cs:20-21` — 静态共享可变 `List` 以 `IReadOnlyList` 暴露。
- `BarrierContentRegistry.cs:45-50` — Dispose 后无守卫可"复活"。
- `BarrierLayerDef.cs:31 / BarrierOutcomeDef.cs:60` — def 级 `ToRuntimeDict` 无生产调用方，与运行时 schema 漂移无人校验。
- `CharacterManagementModule` 死代码群 — `CollectKnownActiveSkillIds/CollectKnownSkillLevelMap`（:1996-2039）、`Helpers.cs:36-55` 的 `GetIntParam/GetFloatParam`（含 `(int)dict[key]` 强转陷阱）、`ContentDefs.cs:792`、`NestedTypes.cs:45-76` 大部分。
- `CharacterBattleWritebackService.cs:185` — `CommitKo` 直接别名 `CommitDeath`（KO=永久死亡语义），无生产调用方，未来误用即角色数据永久损失。
- `CharacterBattleWritebackService.cs:81-98` — `SetVitals(dead:false)` 无条件清死亡标记，可写出不一致态。
- `CharacterCreationService.cs:184-196` — `reroll_count` 校验失败在属性写入后才 return false，留部分变更。
- `PartyContingencySetupService.cs:71` — `SaveSetup` 成功结果把 `current_mp` 当 `EffectiveMpMax`（被 facade 二次推导掩盖）。
- `QuestProgressService.cs:317-323` — `ApplyProgressEvent` 对 no-op 进度也汇报"已推进"。
- `PracticeGrowthService.cs:312-315` — 未知 track 显示名返回"修炼"，误导性文案。
- `AttributeGrowthResult.cs:54-73` — 阈值转换时 `ProgressDelta ≠ After-Before`，三元组自相矛盾（仅测试读取）。
- `AttributeGrowthService.cs:55-65` — 属性到顶后 progress 无限累积不消耗。
- `PracticeSkillLearnStatus.cs:69-90` — 两分支输出键集合不对称（practice 分支缺 `is_learned_direct`）。
- `AscensionApplyService.cs:18-43` — 重复 Apply 重置 `started_at_world_step`；`RevokeAscension` 的 race/subrace 恢复有错配风险（当前无路径）。
- `EffectiveTrait.cs:95-98` — 投影取 Definition 而非实例字段（当前一致，未来覆写即过期）。
- `ContingencySetupMutationResult.cs:35-46` — `Failure` 默认参数致 `StringName` 为 null（下游有兜底）。
- 装备能力：`ConsumerSupport` 元数据全套无调用方（`EquipmentAbilityRuntimeDefinitions.cs:862-873`）；`rng_stream`/`preview_policy` 传递完整但运行时忽略（4 个 pack 填了死值）；`roll_gate`/`once_scope` 无校验（compare 拼错静默哑火）；action kind × trigger 无兼容性校验；`OnBattleEnd` trigger 完全不可用却暴露；`ResolveStatusDurationTu` 的 `duration_turns` 无回合→TU 换算（单位混淆潜伏）；`AllowedSourceKinds` 空集 binding 静默失效；三处 owner key 回退顺序互不一致。

**2038 时间戳（多处，远期）**
- `CharacterManagementModule.cs:1614`、`ProgressionService.cs:385`、`AchievementProgressState.cs:9,59` — Unix 时间戳 int32 截断，2038 后写负值，"recent unlocked"比较选错。

---

## 已排除或降级的旧结论

- 旧 H1“TPK 后必然写出不可读存档”不成立。`PartyState` 的空 leader 与反序列化规则确有不变量冲突，但正式主角死亡路径在 `GameRuntimeFacade.BattleResolution.cs:250-252,318-327,384-397` 会跳过并丢弃 pending save，因此当前不是生产可达的坏档路径。只有未来允许全灭/主角死亡后保存时，才需要先收紧这项不变量。

## Open questions / assumptions（需负责人确认）

1. `ApplyPendingCharacterReward` 无日志删除 pending reward 是否有意？这是唯一会丢玩家奖励且无 GameLog 的路径。
2. `RecordProgress`（public）、`QuestState.MarkFailed`、`GetMergedSourceSkillIdsRecursiveTyped`、`PeekSaveSlotsPlain`、`FlushPostDecodeSave` 等无调用方 API 是预留还是死代码？若是死代码，建议删除而非保留毁档/崩溃陷阱。
3. 装备能力 `OnBattleEnd`/`grant_skill`/`per_world_day` 是否为规划中的 V2 功能？`{=...}` 等级描述表达式已确认保留并补齐校验与失败降级。
4. `CommitKo == CommitDeath`（战斗中 KO 即永久死亡）是否为硬性设计？仓库内无文档/测试佐证。
5. 事件字典来源的 `world_step` 是否全部保证 ≥0？`QuestProgressDataReader.TryReadInt` 不做范围校验。
6. 装备需求职业检查用"历史学过即可"（`GetProfessionProgress(id) != null`）、空任务目录允许接受任意 quest id、死亡回收装备允许超容——均假定为有意设计。
7. 版本策略（UnitProgress v1、PartyState v7、存档 v13 严格相等拒绝、无迁移）按 AGENTS.md 兼容性政策推定为有意。

## Residual risks / 建议补充的回归测试（按优先级）

1. **active_quests 含 completed/failed 任务的读档拒绝行为**（H2）；**非法 `last_progress_context` 优雅失败**（H3）；**装备能力非法嵌套 condition group 不崩 Rebuild**（H4）。
2. `world_step=-1` → Rewarded → 读档的端到端用例（M2）；`merged_skill_source_map` 成环输入（M1）。`{=...}` 非法模板终止性回归已补齐。
3. 装备能力全量真实 pack 用**完整 validation context** 的冒烟测试（M12）；`magical_missile` 漏配负例（M10）。
4. batch swap commit 中 allocator 失败的物品守恒（M5-6）；`SetEquippedEntry` 失败分支（M4）；EndBattle mid-loop `member_not_found`（M9）。
5. `EquipmentState.FromDictionary` 腐败 payload 负向用例；`Peek` 后 `List` 缓存一致性；`.bak` 两次 rename 间崩溃的端到端恢复。
6. 死代码清理：确认后可删除 `RecordProgress`/`MarkFailed`/`GetIntParam` 系/`CollectKnownActiveSkillIds` 系/`FlushPostDecodeSave` 等，消除潜伏炸弹。

## 各批次覆盖率备注

- `scripts/systems/persistence/`：10/10 逐行 + 主存取链路端到端字段核对（PartyState 14 键、PartyMemberState 34 键、UnitProgress 18 键、QuestState 7 键、UnitSkillProgress 17 键、TraitInstanceState 7 键均一致）；深层嵌套记录（`WorldMapSettlementRecordData`、`fog_states` 等）依赖现有 schema 回归兜底。
- `scripts/player/progression/`：187 文件全覆盖；`EquipmentState`、`WarehouseState` 等 6 个状态类的 FromDictionary/snapshot 字段匹配未逐行 diff（见测试缺口 5）。
- `equipment_abilities/`：49 文件全覆盖；`BattleEquipmentAbilityRuntimeService.cs`（282KB）仅契约抽查。
- 三批在 `mastery_source_type` 上结论冲突，已在上文"存疑"条目标注。
