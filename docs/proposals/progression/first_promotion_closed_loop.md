# 技能驱动晋升：推荐实现方案

状态：核心功能切片已实施，S4 节奏实测仍待进行。修订日期：2026-09-14。

用户已明确授权修改存档结构。本文件保留方案比较和后续验收目标；当前事实见 [技能驱动晋升](../../design/progression/skill_driven_promotion.md)，本轮证据见 [实现验收记录](../../reviews/2026-09-14-skill-driven-promotion-implementation.md)。原设计中的完整自然游玩和冷进程旅程尚不能由加速夹具证明。

依据：[首次晋升审计](E:/game/magic/docs/reviews/2026-09-13-first-promotion-flow-audit.md)。本版替换上一版偏重最少修改的实施建议。目标是在当前项目范围内同时保证可达性、成长体验、状态一致性与维护成本；不追求脱离约束的绝对最优。

## 1. Problem：目标与不变量

玩家练习技能达到基础门槛后，可以选择该技能推动一次职业晋升。确认时一起结算核心化、职业 rank、人物等级、HP、属性成长及职业授予技能。已经用于晋升的同一技能不因上限扩展而失去基础成长资格，也不能重复推动人物升级。

关键不变量：

- 人物等级仍等于职业 rank 总和，没有第二套人物经验。
- 一次晋升恰有一个 `GrowthTriggerSkillId`；支持技能不等于升级触发技能。
- 同一角色、同一技能 ID 最多作为升级触发技能成功一次；切核心、融合清理、遗忘／重学不能删除这个事实。
- 职业资格仍受标签、来源、学习状态、归属、知识、属性、声望、依赖职业及目标 rank 限制。
- 查看候选不改变真实角色；提交失败不留下核心化、归属或成长的半成品。
- 角色数据决定机会，窗口只表达机会；读档不依赖保存一个 UI prompt。

本轮先保持熟练度曲线、职业需求数量和成长收益数值。完成新档至两次晋升后，再测时间并调数值。

## 2. 设计时的旧实现约束（历史背景）

| 当前所有权 | 事实与影响 |
| --- | --- |
| `UnitProgress`、`UnitSkillProgress` | 保存技能、职业、活跃触发、锁定列表和成长领取标志，存在重复表达 |
| `ProfessionPromotionRecord` | 已有晋升历史，但 `consumed_skill_ids` 实际记录 assigned 集合，缺少唯一升级触发技能 |
| `ProgressionService` | 候选、提交、职业分配、HP／授予技能和局部回滚在同一服务中 |
| `CharacterManagementModule` | 在晋升成功后追加触发技能属性成长、delta、成就事件 |
| `ProfessionRuleService`／`ProfessionAssignmentService` | 多处通过当前有效满级判断职业资格 |
| `BattleSessionFacade`／`GameRuntimeRewardFlowHandler` | 构造、持有及提交临时提示，与领域资格刷新未完全连通 |
| `UnitProgress` 严格解码 | 锁定列表中的技能必须仍存在、已学、为核心，且逐一匹配技能锁定标志 |

尤其不能直接把旧锁定列表改成永久消费历史：技能融合移除源技能后，这样的存档会被当前解码器拒绝。见 [UnitProgress.cs:1148](E:/game/magic/scripts/player/progression/UnitProgress.cs:1148)。

另一个容易遗漏的边界是 [UnitProgress.cs:510](E:/game/magic/scripts/player/progression/UnitProgress.cs:510)：现有 `DuplicateState()` 会先同步源对象，也通过 Godot 字典构造部分复制数据，不能原样视为无副作用的纯 CLR 快照函数。

## 3. Options：实现选择

| 方案 | 状态与逻辑 | 主要代价／失败方式 | 判断 |
| --- | --- | --- | --- |
| A：沿用活跃触发和锁定字段补入口 | 继续依赖几个 bool／列表；补 setter、提示恢复和回滚 | 修改最少，但永久资格与融合、核心切换、严格存档冲突；新增写点越多越难保持一致 | 可作紧急修复，不作为终态 |
| B：新增独立技能成长账本 | `UnitProgress` 再保存一份已消费技能集合，晋升历史继续独立维护 | 可表达永久事实，但每次晋升要同步两份历史；存档仍需调整 | 可行但重复 |
| C：强化既有晋升记录，资格按记录推导 | 历史明确记录唯一触发；typed 资格查询、状态副本提交、临时提示投影 | 需要一次有边界的存档调整和消费方接线，后续真值最少 | 推荐 |

选择 C：复用已有晋升历史承载完成事实，抽出小而明确的规则／查询／提交职责。不上通用事件溯源框架，不另建成长经验池，也不要求整个游戏采用新事务框架。

## 4. Recommended Design：一份完成事实，两个资格判定

### 4.1 晋升历史承载永久完成事实

扩展 `ProfessionPromotionRecord`，明确保存 `growth_trigger_skill_id` 和本次达成的 `growth_trigger_level`。原有职业、目标 rank、支持技能集合、属性快照和时间信息继续服务解释与审计。

记录的含义是“本次核心成长已完整提交”，只有核心化、职业晋升、HP、技能授予和属性成长均完成后才发布它。`core_max_growth_claimed` 不再单独决定是否允许再次结算；合法的无增量结果，例如属性已经到上限，也属于完成的一次成长。

从全部职业晋升历史构造只读的 `UsedGrowthTriggerIds` 索引，索引不另行存档。角色当前技能和职业归属可以变化，历史不随核心切换或融合删除。历史引用允许指向当前已不再学习的技能；它不是当前技能外键列表。

终态移除 active trigger 的玩家前置状态、重复的消费锁定字段和持久化 pending 候选。`is_core`、技能等级、职业归属仍是实际构筑状态。历史只解释发生过什么，不让已移除的技能继续充当当前职业支持技能。

历史变更由一个追加入口管理；它检查角色范围的触发技能唯一性和目标 rank 连续性。已有真实晋升记录的职业不得因普通失活、隐藏或归属变化而被删除。当前回滚删除临时新建职业的路径随副本提交退出正式成功路径。

### 4.2 新增明确的职业条件，不偷换 CoreMax 语义

增加闭集条件 `TagRequirementSkillState.CoreQualified`，authoring 名称为 `core_qualified`：

```text
Used(s) = 晋升历史中存在 GrowthTriggerSkillId == s
Ready(s) = 已学且可参与晋升，达到基础门槛，且 !Used(s)
CoreQualified(s) = 已学且为合法核心，且 (Used(s) 或达到基础门槛)
CoreMax(s) = 已学且为核心，且达到当前实际可练上限
```

把当前七个职业用于基础成长的 `core_max` 条件显式改为 `core_qualified`。真正要求练至扩展上限的将来内容仍可使用 `core_max`。同步 strict JSON DTO／import／Definition／校验／schema 与测试，词表转换只由一个 typed owner 声明。

`ProfessionRuleService`、`ProfessionAssignmentService` 和 required selection 校验共用 `PromotionEligibilityRules`。不要让 assignment 层保留一条无条件的“必须当前满级”，否则新条件仍会在提交时被旧门禁拒绝。

当前代码中的 tag-state 转换在 `TagRequirement` 和 `TagRequirementDefinition` 各有一份，本切片应统一这一个相关词表，避免只给一边添加新值。

### 4.3 实际上限与基础资格分开

基础门槛独立于消费后的扩展上限求值。普通技能采用现有 `non_core_max_level` 与可用绝对上限的合理边界；未配置基础门槛的技能按定义的当前有效上限处理，无正上限时不生成触发机会。动态上限由既有定义规则计算，不写固定等级或技能 ID 特例。

已成功晋升的技能由历史证明完成过基础段，可以继续练扩展段，不再为后续职业资格重复补练。重学同一技能不会恢复第二次升级机会，也不会自动补回技能等级。尚未用于晋升的技能资格仍按当前学习状态和门槛计算；本方案不新增“所有历史练满但未消费技能”的第二套账本。

`SkillEffectiveMaxLevelRules` 继续负责技能实际可练上限，但是否已取得扩展段权限改为查询完成历史，避免核心开关撤销已取得的成长。原有技能命中／检定／DC 奖励也从同一个完成事实投影，维持现有正常晋升的奖励数值。

相关消费方不止窗口：必须接通 [BattleUnitFactory.cs:1408](E:/game/magic/scripts/systems/battle/runtime/BattleUnitFactory.cs:1408)、人物／队伍展示、headless 快照和 BattleSim fixture 装配。这里是装配及语义回归，不要求跑数值平衡模拟。

## 5. 候选算法与玩家选择

领域层提供只读查询，输出职业候选、可用成长技能、支持技能池和 typed 缺少条件；UI 不自己判定技能是否满级、来源是否合法。

推荐界面在同一窗口内展示“成长技能 → 可选职业 → 本次收益和归属”。只有真正有选择时才需要玩家再选：唯一确定的支持组合由领域稳定补全，确认前完整展示；存在不同职业归属后果的组合允许修改。不要让玩家先去操作“设核心”“激活触发”两个内部开关。

算法按角色当前已学技能工作：

1. 收集已学技能、实际核心、归属及历史消费索引，计算每个技能的基础门槛状态。
2. 对职业的下一目标 rank 先检查知识／属性／声望／依赖条件，再筛选 `Ready` 技能。
3. 用 `PromotionEvaluationContext` 将选中的唯一触发技能投影为本次将成为的核心；其他非核心技能不会顺带核心化。首次晋升的核心槽位按提交后的 rank 校验，避免等级 0 的循环门槛。
4. 复用现有标签缺额、来源、归属和 required/subset 校验生成支持组合。先固定必须技能和玩家显式选择，再稳定补全，并校验最终完整集合。
5. 初始只列职业和可用触发技能，不枚举所有技能子集的笛卡尔积。支持组合在玩家选定触发技能后求解。

保留现有“一项技能可满足多个匹配标签”的语义，不能套成一技能只能占一标签槽位的简单匹配。若现有贪心补全不能证明覆盖多标签交叠情况，用按最小缺额分支、带去重及剩余候选剪枝的确定性回溯求一个可行解；无需预先引入通用求解框架。至少用一个存在可行解但会误导朴素贪心的测试约束它。

当前数据只有 7 个职业，基础要求数量较小；扫描已学技能并按需求解足够。候选查询只在成长／条件变化、窗口打开和读档等边界调用，不放入 `_process`、每帧 HUD 或每次伤害预览。最坏的组合搜索仍可能指数增长，不能把此方案宣称为通用线性算法；增加复杂多标签内容时用真实候选规模重新评估。

## 6. typed 提交与原子边界

### 6.1 默认、候选和提交分开表达

建议的最小类型组：

- `PromotionOffer`：职业／目标 rank、可选触发技能、支持池、缺少条件。
- `PromotionSelectionDraft`：窗口中的可编辑选择，允许未完成。
- `PromotionCommitRequest`：member、profession、target rank、唯一 trigger、明确选定的 assigned／qualifier 集合；所有必填值完整。
- `PromotionCommitResult`：typed 成功／失败原因、成长 delta 和实际结算结果。

名称为设计建议。可复用已有值对象的合适部分，但正式提交不再用 `Empty` 隐含选择，也不让三个 `Has...` 与非 null 集合共同猜测意图。现有字段存在性 bug 如需独立先修，可单测修复；它不是终态请求协议。

运行时确认凭据由当前会话的提示实例与冻结请求关联，至少包含 prompt generation 和完整选择。不能仅按 profession ID 找第一项。UI 和文本命令提交同一个请求；按职业的便捷命令仅在唯一明确选项时解析成功。

重新加载或重建提示会使旧凭据失效。成员、rank、技能或归属变化时重算资格；点击确认再校验当前事实，即使一个旧请求看起来仍结构合法，也不能无条件信任旧快照。不要把界面 token 当作唯一的领域校验。

### 6.2 在副本上完成，一次发布成长状态

建议 `CharacterManagementModule` 保持成员对象身份，提交过程仅替换其成长聚合：

```text
读取当前成员与冻结请求
  → 校验请求和当前领域事实
  → 创建无副作用的 UnitProgress 副本
  → 在副本上核心化、分配职业、升 rank、结算 HP/技能/属性
  → 追加明确触发技能的晋升记录并检查聚合不变量
  → 一次发布 member.progression = candidate
  → 发出 delta 和现有成就事件，刷新战斗／世界投影
  → 通过现有运行时持久化入口保存
```

副本创建要修正当前 `DuplicateState()` 对源对象的同步写入，并直接复制 CLR 集合，不能先序列化成 Godot Dictionary 再反序列化。复制只发生在确认提交，不用于枚举每个候选；不复制整支队伍、仓库或装备图。

把本次触发技能属性成长从“发布后补写”移到副本结算中，复用 `AttributeGrowthService`。同一份成功结果用于 delta，避免 UI 再推算数值。晋升服务不再依赖临时改写全局 active trigger；把显式请求贯穿 rule、assignment、growth 和结果。

纯预览不得掷 HP 骰子；展示公式或范围。完整校验后在提交中掷一次，产生确定的结果。保存失败后的重试只重试保存已经发布的状态，不能重新执行晋升或重掷。

副本发布保证的是人物成长聚合的同步原子性，不宣称磁盘和成就奖励队列拥有新的全局事务。成就仍走现有事件／待领奖励链；世界路径保存失败要明确报告内存已应用、落盘失败。战斗中继续遵守保存锁，在既有结算边界写回，不绕过锁强制保存。

## 7. 展示、暂缓与读档

推荐把“存在晋升机会”与“必须打开晋升 modal”分开。界面持续提供可晋升入口；玩家打开窗口后沿用暂停／模态边界，关闭或暂缓后机会仍在。获得熟练度可以产生提示，但不能因为资格一直存在就反复自动开窗或强迫玩家立刻决定职业。

这比上一版保留强制确认更适合先练多个技能再选择构筑，也避免读档恢复后反复被同一选择阻断。暂缓无需持久化新状态：保存技能和晋升历史，读档重新查询可用机会，玩家可以再次打开窗口。

窗口打开、完成、关闭及战斗 timeline 恢复必须配套实现；不能只把 world modal 置空而留下战斗冻结。主角死亡时晋升入口不可操作，已有不可重入的奖励／服务事务仍按当前优先级处理。

查询刷新覆盖：技能学习、熟练度越过门槛、核心／归属变化、职业变化、相关知识／属性／声望变化及 Setup／读档。多个成员按稳定顺序提供机会，奖励仍可继续领取。世界地图和子地图都调用同一角色查询，不保存两套世界／战斗资格。

## 8. 融合与重学：保留玩法，保留事实

不采用上一版“无法保留锁定就禁止融合／替换”的常规处理。当前融合会清理核心与源技能，属于构筑玩法；永久消费历史应独立于这些清理步骤。

- 保留源技能的融合：源技能若曾用于晋升，仍不能再次触发。
- 移除源技能的融合：历史仍保留该 ID；不存在的技能不能作为当前支持资格。
- 重学同一 ID：恢复技能学习状态，不获得第二次人物升级机会，不重复属性奖励。
- 新结果 ID：不继承源技能的晋升次数或职业资格，按自身定义和职业来源限制判断。当前七职业的相关条件均为 `unmerged_only`，保持这一限制，合成继承等级不会绕过它。
- 将来若允许 merged 技能参与晋升，必须同时定义继承等级是否计入新的基础成长；这不是本切片偷偷打开的内容规则。

原有重学阻止、功法等级换算和职业失活规则继续生效。新完成事实只用于成长资格、扩展权限和一次性收益，不能变成绕过其他规则的通行证。

## 9. 存档影响与旧档决定

这是一次明确的持久模型调整，不应伪装成“保持旧字段但换解释”。建议统一升级成长结构及外层存档版本，新 decoder 只接受新精确字段集；存档索引版本是否变化按其实际字段判断，不机械一起升级。

新增：晋升记录中的唯一触发技能和达成等级。移除：用于控制玩家预选的 active trigger、重复消费锁定／领取标志、派生的 pending 选择。锁定奖励数值若仍保留配置表达，也不能继续作为第二个消费真值。同步 `UnitProgress`、`UnitSkillProgress`、`ProfessionPromotionRecord`、`PartyState.SaveSnapshot` 和所有严格解析测试。

旧档不能靠 `consumed_skill_ids` 自动还原唯一触发：该字段记录的是 assigned 集合，某些晋升触发还可能来自 qualifier 角色。旧锁定集合也没有完整的每次职业晋升对应关系，且会被融合清理。

因此，本提案推荐开发期采用明确版本升级，新版拒绝旧版本；不删除旧档。如果实施时要求保留旧档，需先确认这一要求并另做可验证的迁移方案，无法还原的记录不能猜一个技能填入。**2026-09-14 已获用户明确授权：当前 SaveVersion 21、UnitProgress 2，拒绝旧版本，保留旧文件，没有兼容迁移。**

## 10. Minimal Slice：实现顺序

| 阶段 | 工作 | 完成标准 |
| --- | --- | --- |
| S1：领域真值与提交 | 定义新晋升记录、资格规则、完整请求；纯副本结算；新结构 codec | 新档首升、二升、重放拒绝、源状态不变及新结构 round-trip |
| S2：真实入口和恢复 | 统一 query → offer → draft → commit；UI／文本提交；可暂缓；读档恢复机会 | 不调用测试 setter／补奖励，即可完成正常新档两次晋升 |
| S3：邻接消费者接线 | 上限、战斗奖励、HUD、融合、重学、奖励调度与 fixture；旧真值退出 | 全部消费方一致，无残留写路径，战斗保存边界成立 |
| S4：成长节奏测量 | 保持现有曲线测首升、二升、职业差异，分别记录获取／练习／探索时间 | 有实际时间证据后决定数值调整 |

S1—S3 是一个可交付功能切片，不能只完成新 service 或窗口就宣布落地。原来的四处流程缺陷在同一条新链路验收，避免先搭一套旧提示恢复、随后再整套替换。若产品急需先恢复提交，F3 字段存在性修复可独立落地，但不得当作最终闭环。

## 11. Files To Change

| 范围 | 预期文件／责任 |
| --- | --- |
| 当前状态与历史 | `scripts/player/progression/UnitProgress.cs`、`UnitSkillProgress.cs`、`UnitProfessionProgress.cs`、`ProfessionPromotionRecord.cs`、`PartyState.SaveSnapshot.cs` |
| 规则与提交 | `ProgressionService.cs` 抽出小的晋升职责、`ProfessionRuleService.cs`、`ProfessionAssignmentService.cs`、`CharacterManagementModule.cs`、`AttributeGrowthService.cs`；相关 `LevelGrowthEvaluationService` 退出 active 预选职责 |
| typed 规则／请求 | progression 域内 `PromotionEligibilityRules` 及 offer/request/result 值对象；只在必要边界提供查询／提交接口 |
| 内容词表 | `TagRequirement.cs`、`definitions/TagRequirementDefinition.cs`、`ProfessionIdentityJsonContracts.cs`／import、内容校验／schema、七职业 JSON |
| 上限与融合 | `SkillEffectiveMaxLevelRules.cs`、`SkillMergeService.cs`、`PracticeGrowthService.cs` |
| 运行时与场景 | `GameRuntimePromotionPromptContext.cs`、reward-flow handler／port、`BattleSessionFacade.cs`、相关 facade commands、proxy、`PromotionChoiceWindow` 和场景／headless 命令 |
| 战斗与展示 | battle character gateway、`BattleRuntimeModule`、`BattleUnitFactory`、人物／队伍／headless 技能状态投影、BattleSim fixture 装配 |
| 持久化 | `SaveSchemaVersions.cs`、严格成长解码及版本错误处理，实施前落实旧档决定 |

不新增第二个总管理器，不把资格判断塞进 `GameRuntimeFacade` 或 `WorldMapSystem`。当前共享工作区含其他 UI／运行时修改，实际实施需按行为和具体 hunk 保留它们。

## 12. Tests To Add Or Run

验收以行为为准，现有缺陷探针只作为诊断材料。

1. 正常新档 → 学习／熟练度到门槛 → 打开真实选项 → 首升 → 旧核心 3/5＋新技能达标 → 二升；没有测试 setter、额外奖励唤醒或提示替换。
2. 只把本次 trigger 投影为核心；等级 0 能合法首升，不能顺带把所有候选变核心；检查各职业标签／来源／归属与多标签交叠选择。
3. 查看和复制不修改源角色；非法请求、失效选项、重复提交不改变 rank、HP、属性、技能归属或随机结算次数。
4. 核心切换、源保留融合、源移除融合、重学同 ID 后，消费历史与一次性奖励仍一致；历史引用技能不再学习时，新存档 round-trip 仍合法。
5. `CoreQualified` 与 `CoreMax` 在 3/5 上给出不同且正确的结果；实际技能上限、战斗奖励、HUD 和 headless 一致。
6. world／submap 读档后可再打开晋升；关闭窗口、多个成员、并存奖励和战斗 timeline 正常；死亡状态不允许提交。
7. 内存晋升成功但保存失败时，仅重试保存；battle save lock 下不提前落盘，正常结算后状态完整。
8. 新版本严格解析拒绝缺失／多余字段、重复 trigger、非法 rank 记录；旧版行为按已确认的旧档政策验证。

按以上边界新增或扩展 progression、runtime、headless 与 UI 的定向回归。完成代码后运行构建和受影响测试，再按交付范围执行全量回归；不把数值 BattleSim 纳入常规回归。UI 变更另需真实运行、点击确认／暂缓和截图检查。

## 13. Project Context Units Impact 与证据边界

当前已更新 `docs/design/project_context_units.md` 的晋升请求、消费历史、准备／发布链和读集，当前实现细节进入 `docs/design/progression/character_module.md` 与 `skill_driven_promotion.md`。未来阶段仍留在本提案。

原设计依据 2026-09-13 审计；旧版 6 项回归只支持当时实现。2026-09-14 的实现已验证新请求／历史、首次与第二次晋升、磁盘保存重载、战斗暂停恢复和实际窗口输入。熟练度加速夹具不证明自然获取时间、冷进程主场景旅程或成长分钟数；S4 仍待实际测量。没有实现旧档迁移。
