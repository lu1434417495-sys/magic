# NPC 对话与全屏任务日志

核对日期：2026-09-15。本文描述当前实现；实机记录见 [验证报告](../../reviews/2026-09-15-quest-dialogue-journal.md)。

## 玩家操作

1. 在据点选择村长等任务 NPC，阅读对白，以“交给我吧”等回应接取任务。多项可操作或进行中的委托以“关于……”切换话题。尚未解锁的后续任务不作为话题展示。
2. 返回大地图，点击底部“任务 · J”或按 J 打开任务日志。
3. 页面覆盖整个游戏视口，左侧选择任务，右侧阅读说明、委托来源、目标进度、报酬和下一步。支持全部、进行中（含待交付）、已完成、已失败筛选；空档与空分类有提示。
4. 按 Esc / J 或点击右上返回按钮回到大地图。接取、提交物资、领取奖励仍在对应 NPC / 任务板完成。

全屏指任务页面覆盖游戏视口，不主动切换系统窗口的全屏设置。NPC 交谈保持居中窗口，面板使用不透明底色并禁用透明淡入；地图只在面板外可见。

## 状态与命令所有权

| 层 | 当前 owner 与行为 |
| --- | --- |
| 任务真相 | `PartyState.quest_journal`；经 typed getters 查询 active / claimable / rewarded / failed。内容名称与奖励来自 catalog definitions。 |
| 阅读投影 | `GameRuntimeQuestJournalBuilder` 构建 `QuestJournalWindowData`、`QuestJournalEntryData` 和目标 DTO；不接取、不领奖、不推进目标，不持有可变 `QuestState` 引用。只查询日志内已有记录，不遍历全部任务作为可接列表。 |
| 窗口命令 | `GameRuntimeFacade.QuestJournal.cs` 拥有 `CommandOpenQuestJournalTyped` 和查询；未初始化、战斗、已有 modal 时拒绝打开。`RuntimeModalKind.QuestJournal` 对应 `quest_journal`。 |
| 关闭 | `GameRuntimeRewardFlowHandler` / facade reward-flow port 使用现有关闭通道清除 modal，恢复世界输入。 |
| 场景接线 | `WorldMapRuntimeProxy` 转发命令和 DTO；`WorldMapSystem` 连接按钮与 `closed`，在世界 J 输入前清除持续移动状态，并按 modal 同步可见性。连接在 teardown 时解除。 |
| 阅读 UI | `QuestJournalWindow.cs` 只拥有筛选和所选任务 ID。`ShowWindow` 首次打开复位筛选，刷新保留选择；`HideWindow` 清空 DTO / 选择 / 列表。 |
| NPC UI | `NpcQuestOfferDialog` 使用 `NpcQuestOfferWindowData` 的对白、反馈、确认状态，发出 `action_requested(settlement_id, action_id, payload)` 和 `closed`；payload 只提交来源、任务 ID 和确认意图，规则由原 handler 重查。 |

任务日志不增加存档字段或兼容分支。NPC 的默认选择继续按待领奖、可操作、进行中、剩余首项排序，避免新档落在锁定的后续任务。运行时 context 持有有效提交后的任务 ID；跨话题提交会清除旧确认。

## 场景与输入

`scenes/main/world_map.tscn` 实例化 `scenes/ui/quest_journal_window.tscn`。后者根和 `FullPanel` 均全锚点布局，不透明背景覆盖地图与 HUD；横向为任务列表和详情，详情使用滚动容器，标题、筛选和关闭入口保持可见。窗口复用 Chronicle 字体、列表与按钮主题，不使用普通弹窗的居中面板装饰。

`QuestJournalWindow` 继承 `ModalWindowShell` 的 Esc 语义，额外处理 J，关闭只发信号。runtime modal 门禁阻止移动与其他窗口命令；日志自身没有 gameplay 写入入口。无需在编辑器额外手工连接信号。

## 验证入口

- `tests/world_map/ui/run_quest_journal_window_regression.cs`：正式内容的只读投影、未接任务隐藏、进度、待交付、空态、筛选、全视口范围与关闭信号。
- `tests/world_map/ui/run_npc_quest_offer_dialog_action_regression.cs`：对白不含任务详情、话题切换不提交、回应 ID、确认隔离、提交 / 领奖 / 已完成 / 禁用状态。
- `tests/world_map/runtime/run_npc_quest_offer_regression.cs`：正式初阵接取、NPC 名称、前置 / provider / channel / 确认校验及刷新选择。
- `tests/world_map/runtime/run_world_map_runtime_proxy_regression.cs`：日志打开 / 关闭、阻止移动与冲突窗口。
- 原生 1280 × 720 与 3840 × 2160：新档对白接取、任务按钮 / J / Esc / 返回、读档保留记录、文字和全视口遮盖。自然完成任务和首次晋升不属于本次证据。
