# 技能驱动晋升

状态：Current / Implemented。代码核对：2026-09-14。

## 成长模型

人物等级是职业 rank 总和。每次晋升选择一个未用于成长的已学技能，达到其基础门槛并满足职业其他条件后，人物等级增加 1，目标职业 rank 增加 1。没有额外人物经验或职业经验池。

基础门槛由 `PromotionEligibilityRules.GetMilestoneLevel` 求值：配置了正 `non_core_max_level` 时取其与有效绝对上限的较小值，否则取有效绝对上限；无正门槛则不产生机会。`IsReadyTrigger` 要求当前已学、达到门槛且历史未消费，来源／标签／知识／属性／声望／职业依赖由职业规则继续检查。

| 判定 | 条件 |
| --- | --- |
| 可作本次成长技能 | 已学，达到基础门槛，未作为历史触发技能，符合目标职业规则 |
| `core_qualified` | 已学且为核心或本次投影核心，并且已完成过晋升或达到基础门槛 |
| `core_max` | 已学、核心且达到当前实际可练上限 |

当前七个职业的基础成长条件配置为 `core_qualified`。已经用 3 级技能完成晋升后，该技能即使变成 3/5，也保留支持后续晋升的资格；继续练至 5 级仍有技能本身的战斗收益。同一技能 ID 不能再次推动人物等级，核心切换、遗忘、融合移除和重学都不重置消费记录。

首次晋升不要求玩家提前设置核心或活跃触发。系统只把所选 trigger 投影为核心，在提交后的槽位容量下验证首升；其他非核心支持技能保持非核心。各职业原有技能数量、来源和归属要求仍适用，例如战士首次晋升要求已学 3 个战士技能，其中 1 个达基础成长门槛。

## 所有权与调用链

```text
成长变更 / 命令完成 / 读档后首次 advance
  -> GameRuntimePromotionNotifications（会话内展示去重与排队）
  -> 安全 modal 边界自动打开
PartyManagementWindow / 常驻晋升提示 / G / promotion open
  -> GameRuntimeFacade.Promotion
  -> CharacterManagementModule.GetPromotionOffers
  -> ProgressionService + ProfessionRuleService + PromotionEligibilityRules
  -> 临时 PendingProfessionChoice（含完整默认请求）
  -> BattleSessionFacade.BuildPromotionPrompt
  -> GameRuntimePromotionPromptContext（冻结请求 + 新 token）
  -> UI / 文本确认
  -> GameRuntimeRewardFlowHandler（校验当前 prompt）
  -> CharacterManagementModule.PromoteProfession
  -> ProgressionService.PreparePromotion（重新校验事实，在副本结算）
  -> member.progression = candidate
  -> delta / 成就 / 战斗投影 / 现有持久化边界
```

按成员 ID、职业 ID、技能 ID 的稳定顺序查询，不在每帧或每次战斗预览枚举组合。候选查询不修改角色。`PromotionSelectionDraft` 只用于内部补全过程；提交必须是完整 `PromotionCommitRequest`，不接受缺字段、重复技能、任意覆盖 HP 或空请求自动挑选。

补全按角色当前候选技能求解。一个技能可同时满足多个标签；算法在各标签计数封顶后的覆盖状态上做动态规划，先最小化新增职业槽位，再最小化技能数量，相同成本保留稳定顺序。状态数上界是各标签 `(需求数 + 1)` 的乘积，并非一般情况下的线性算法。当前职业每角色规模较小。界面展示每个 trigger／职业的完整默认方案；当前职业的其他支持选择不新增不同的职业归属后果。

## 一次性结算和存档

`ProfessionPromotionRecord` 明确持有 `growth_trigger_skill_id`、`growth_trigger_level`、连续目标 rank、assigned／qualifier 集合和晋升前属性快照。所有职业记录共同构成消费事实；不另存已消费技能集合。公开历史读取返回深复制，追加由 `UnitProgress.TryAppendPromotionRecord` 管理。有历史的职业不会被普通删除入口移除。

准备阶段先验证完整选择、容量、历史和属性成长配置，再复制 UnitProgress，在副本核心化、分配、追加历史、掷 HP、授予技能、应用属性并刷新派生状态。复制只同步副本。拒绝结果不发布部分成长。HP 规则沿用 `max(1, 1d职业生命骰 + 2 × 体质修正)`；预览显示公式，不掷骰。

历史还统一提供已完成技能的扩展等级权限，以及既有命中／检定／DC 的 +1 奖励。BattleUnitFactory、技能被动、人物管理和 headless 快照从同一历史推导；BattleSim 夹具按显式历史装配，不再伪造已移除的锁定字段。

存档版本为 **SaveVersion 21 / UnitProgress 2 / PartyState 9 / SaveIndexVersion 5**。严格解码要求职业 rank 连续、记录数等于 rank、所有触发技能唯一、等级为正、触发存在于本次 assigned／qualifier 并集、rank 总和等于人物等级。历史技能允许已从当前技能表移除。旧 active／locked／claimed／pending 字段不接受，旧版存档明确拒绝；不迁移、不删除旧文件。

成长副本的一次发布不等于全游戏磁盘事务。世界保存失败会返回 `PersistenceFailure`，说明内存已应用、落盘未完成；重试保存现有状态，不重新执行晋升。战斗遵守保存锁，晋升在内存生效，结算后走既有写回。

## 入口、暂缓和恢复

人物管理提供当前成员的职业晋升按钮，无 modal 时 G 查询可晋升成员；世界和子地图使用同一 runtime owner。死亡成员不生成或提交机会。冲突 modal 优先完成后才允许打开。

达到完整晋升条件后自动开窗。`GameRuntimePromotionNotifications` 只持有会话内的可用／已展示机会，以成员、职业、目标 rank、成长技能 ID 标识一项机会；同一窗口展示的方案一并标记为已展示。暂缓清临时 prompt 并恢复操作，不消费成长，也不在后续帧反复弹出已展示的机会。新增成长技能、职业或下一 rank 产生新机会时再次自动提示；失去资格的机会移除，重新满足后可再次提示。

命令完成、战斗 batch 完成后重新检查机会；直接学习／知识学习／成长 delta 与队伍替换使 CharacterManagement 的可用性 revision 失效。空闲 advance 只检查 revision 和已排队的成员，不重新枚举职业组合。弹窗仅在无其他 modal、无待生成战斗且战斗未结束时打开；冲突窗口结束后继续调度，不覆盖已有奖励、确认或其他成员的晋升窗。`needs_promotion_modal` 不因资格长期存在而保持 true，自动呈现由 runtime owner 决定。

世界与战斗共同显示可点击的“可晋升（N人）· G”提示，保留到没有合法机会为止；状态／文本快照持续附带可晋升成员与重新打开方式，资格变化记录 `promotion.available` 日志。读档建立新 facade 后，首个安全 advance 重新发现并自动展示剩余机会，不保存已展示队列或恢复旧 token。重复提交、已消费技能、旧窗口 token 和含糊的“只选职业”请求均被拒绝。

战斗仅允许在无冲突 modal、成员存活时打开，打开沿既有晋升边界暂停 timeline；暂缓和成功均恢复 timeline，不释放战斗保存锁。

文本入口：`promotion open [member_id]`、`promotion choose <profession_id> [growth_skill_id]`、`promotion defer`。选择职业但不指定技能仅在该职业恰有一个方案时有效。

## 验证边界

领域回归覆盖首升／二升、3/5 资格保留、重复触发、无副作用复制、严格 schema、融合历史和多标签交叠。`run_first_promotion_closed_loop_regression.cs` 通过正式学习／熟练度／文本命令／磁盘读写边界完成两次晋升，覆盖战斗暂停恢复、保存锁和保存失败重试。`run_promotion_window_flow_regression.cs` 使用实际世界场景、Godot 输入、人物管理按钮、暂缓、G 和确认，并支持真实 720p／4K 截图。

`run_promotion_auto_prompt_regression.cs` 覆盖完整门槛、冲突窗口排队、暂缓去重、状态提示保留、新技能与下一 rank 自动提示、读档恢复以及战斗暂停／恢复／保存锁。场景回归覆盖自动弹窗、暂缓、常驻提示点击与 G 的真实输入。

上述夹具加速授予熟练度，用于验证流程；不是从主场景完整游玩的 application E2E，也不能证明技能获取时间或每次晋升所需分钟数。成长节奏的实际测量仍见提案 S4；本轮不改熟练度曲线。
