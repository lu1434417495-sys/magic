# 任务链领取条件落地方案

> 更新日期：2026-06-15  
> 适配范围：`QuestDef.accept_requirements`、任务接取、契约板显示、任务内容校验

## Problem

当前 `QuestDef` 已经有 `accept_requirements` 字段，但它只是原始 `Array<Dictionary>`：

- `QuestDef.ValidateSchema()` 不校验领取条件。
- `QuestProgressService.AcceptQuest(...)` 不读取领取条件。
- 契约板始终把有效任务条目投影为 `is_enabled = true`。
- 文档中的旧条件表包含不应落地的字段，如 `settlement_rank_min`、`tag_owned`。

本次目标是把任务链解锁落在现有 `accept_requirements` 上，不新增 `QuestChainDef`。任务领取条件必须由运行时接取链统一执行，契约板只负责展示过滤后的结果。

## Fixed Decisions

- `quest_completed` 表示前置任务奖励已领取，必须使用 `PartyState.HasCompletedQuest(requiredQuestId)` 语义；`QuestState.IsCompleted()` 不能作为链式解锁依据，因为 claimable 但未领奖不算解锁。
- 未满足领取条件的任务在任务板完全隐藏，不显示灰色禁用态。
- 可重复任务用于悬赏和资源循环，不能作为 `quest_*` 条件的被引用目标；内容校验遇到这种引用直接失败。
- `quest_not_completed` 采用“未接未完成”语义：目标任务 active、claimable、rewarded/completed 任一状态都使条件不满足。
- 据点等级使用已有 6 级 `SettlementConfig.SettlementTier` / settlement `tier` 体系，条件字段命名为 `settlement_tier_min`；不引入 `settlement_rank_min`。
- `party_level_min` 使用主角等级：`PartyState.GetResolvedMainCharacterMemberId()` 对应成员的 `UnitProgress.character_level`。
- `tag_owned` 不实现；配置中出现 `tag_owned`、`settlement_rank_min` 或未知 requirement type 都是内容校验错误。
- 不做兼容别名、旧字段迁移或 fallback schema；错误配置应在内容校验阶段暴露。

## Current Ownership

- 静态任务定义归 `QuestDef` / `ProgressionContentRegistry` / `QuestContentValidator`。
- 接取状态和已领奖事实归 `PartyState`：active、claimable 与 `completed_quest_ids` 分离。
- 接取执行归 `QuestProgressService.AcceptQuest(...)`，`CharacterManagementModule.AcceptQuest(...)` 和 runtime command 只是上层入口。
- 契约板条目归 `GameRuntimeSettlementCommandHandler` 构建，条目来自 typed `QuestDef`，最终提交仍调用 runtime quest command。
- 据点等级在 world settlement payload 顶层 `tier`，不是 `settlement_state` 内字段。

## Options

### Option A: 只在契约板过滤

在 `GameRuntimeSettlementCommandHandler` 里读取 `accept_requirements`，不改 `QuestProgressService`。

失败模式：文本命令、事件 auto-accept、测试或其他 runtime 入口仍能绕过条件，不满足“领取必须统一校验”。

### Option B: 只在 `QuestProgressService` 校验

把条件全部接入 `AcceptQuest(...)`，但契约板仍显示锁定任务。

失败模式：运行时安全，但任务板会暴露未解锁链路，违背“锁定任务隐藏”的产品结论。

### Option C: typed 条件模型 + 中央接取判定 + 契约板隐藏

在 progression 层为 `accept_requirements` 增加 typed 解析和评估结果。`AcceptQuest(...)` 统一拒绝未满足条件，契约板调用同一判定做隐藏。

这是推荐方案。它保持任务规则在 progression owner 内，UI 不复制规则，且所有入口共享同一结果。

## Recommended Design

### Requirement Schema

`accept_requirements` 继续作为资源导出字段存在，但 `QuestDef` 增加 typed requirement entry 读侧，不让 runtime 正式链直接回读 Godot dictionary。

支持的 `requirement_type`：

| type | 字段 | 判定 |
| --- | --- | --- |
| `quest_completed` | `quest_id` | `PartyState.HasCompletedQuest(quest_id)` 为 true，必须已领奖 |
| `quest_active` | `quest_id` | `PartyState.HasActiveQuest(quest_id)` 为 true |
| `quest_not_completed` | `quest_id` | 目标任务不在 active、claimable、completed 任一集合中 |
| `item_in_inventory` | `item_id`, `amount` | 共享仓库非空 stack 中该物品总数 >= amount |
| `gold_min` | `amount` | `PartyState.GetGold()` >= amount |
| `party_level_min` | `level` | 主角成员 `progression.character_level` >= level |
| `settlement_tier_min` | `tier` | 当前接取上下文 settlement tier >= tier，tier 范围 0..5 |
| `world_step_min` | `step` | 当前 world step >= step |

字段规则：

- 数值字段必须是显式 int，`amount` / `level` 为正数，`step` >= 0，`tier` 在 0..5。
- `quest_id` / `item_id` 必须是非空 `StringName`，并在对应 typed catalog 中存在。
- `quest_*` 条件引用 repeatable quest 是校验错误。
- 当前 quest 不能引用自身；任务条件链中出现 cycle 是校验错误。

### Runtime Evaluation

新增小型 typed owner，例如 `QuestAcceptRequirementEvaluator`、`QuestAcceptRequirementContextData`、`QuestAcceptAvailabilityResultData`：

- evaluator 接收 `PartyState`、typed quest index、当前 world step、可选 settlement tier。
- `QuestProgressService.AcceptQuest(...)` 改为先解析 availability，再执行现有 active/claimable/completed/reaccept 检查和状态写入。
- 对外提供只读查询方法，例如 `ResolveAcceptAvailability(...)`，供契约板和文本命令复用失败码/失败文案。
- `settlement_tier_min` 在缺少 settlement context 时 fail closed；文本命令若未绑定当前据点，不允许绕过该条件。
- `item_in_inventory` 只检查持有数量，不消费物品；消耗仍由 submit-item objective 或其他服务负责。
- repeatable quest 重新接取也必须重新评估自身领取条件。

### Contract Board Behavior

契约板构建条目时：

- active、claimable、completed/repeatable 状态按现有规则展示。
- available/repeatable 且领取条件未满足时，不向 entries 添加该任务。
- 如果所有真实任务都被隐藏，沿用现有空态条目“暂无可查看任务。”。
- 提交时再次调用中央接取判定；即使前端 payload 伪造了隐藏任务，也必须失败。

### Content Validation

`QuestContentValidator.ValidateTyped(...)` 扩展为正式校验入口：

- 校验 requirement type、字段类型、字段范围和跨内容引用。
- 拒绝 `tag_owned`、`settlement_rank_min`、未知 type、缺字段、空 id。
- 拒绝 `quest_*` 引用 repeatable quest。
- 拒绝 self reference 和 cycle。
- 保持 strict typed catalog：不按 string key fallback，不按 resource 内 value id 补索引。

## Minimal Slice

1. 在 `QuestDef` 中增加 accept requirement enum、typed entry、`GetAcceptRequirementEntriesTyped()` 和 schema 校验。
2. 在 progression 层增加领取条件 evaluator/result/context，并接入 `QuestProgressService.AcceptQuest(...)`。
3. 让 `CharacterManagementModule` 和 runtime quest command 透传 world step / settlement tier context，并使用 availability 结果生成失败消息。
4. 让契约板构建和提交复用同一 availability 判定，隐藏未解锁任务。
5. 扩展 `QuestContentValidator`，让错误配置在内容验证中失败，而不是运行时静默锁定。

## Files To Change

- `scripts/player/progression/QuestDef.cs`
- `scripts/player/progression/QuestContentValidator.cs`
- `scripts/systems/progression/QuestProgressService.cs`
- `scripts/systems/progression/CharacterManagementModule.cs`
- `scripts/systems/game_runtime/GameRuntimeQuestCommandHandler.cs`
- `scripts/systems/game_runtime/GameRuntimeSettlementCommandHandler.cs`
- 对应 regression tests：progression core、runtime validation、world map runtime、text runtime。

## Tests To Add Or Run

新增或扩展：

- `tests/progression/core/run_quest_progress_service_regression.cs`
  - `quest_completed` 在 claimable 未领奖时不满足，领奖后满足。
  - `quest_not_completed` 在 active / claimable / completed 三种状态都不满足。
  - gold、item、main character level、world step 条件分别阻止/允许接取。
  - repeatable quest 重新接取时仍重新评估条件。
- `tests/runtime/validation/run_quest_content_validator_typed_regression.cs`
  - 未知 type、`tag_owned`、`settlement_rank_min`、缺字段、非法数值失败。
  - `quest_*` 引用缺失 quest、repeatable quest、自身、cycle 失败。
  - `item_in_inventory` 引用缺失 item 失败。
  - `settlement_tier_min` 的 tier 超出 0..5 失败。
- `tests/world_map/runtime/run_game_runtime_settlement_command_handler_regression.cs`
  - 锁定任务不出现在契约板。
  - 前置任务奖励领取后，后续任务出现在同一任务板。
  - `settlement_tier_min` 使用当前 settlement 顶层 `tier`。
  - 伪造 payload 提交隐藏任务仍失败。
- `tests/text_runtime/headless/run_text_command_quest_progress_regression.cs`
  - 文本接取命令不能绕过领取条件，并返回可读失败原因。

验证命令：

```bash
dotnet build magic.csproj
godot --headless -s res://tests/progression/core/run_quest_progress_service_regression.cs
godot --headless -s res://tests/runtime/validation/run_quest_content_validator_typed_regression.cs
godot --headless -s res://tests/world_map/runtime/run_game_runtime_settlement_command_handler_regression.cs
godot --headless -s res://tests/text_runtime/headless/run_text_command_quest_progress_regression.cs
```

## Project Context Units Impact

`docs/design/project_context_units.md` 当前仍有效。该方案落在已有 CU-06、CU-11、CU-12、CU-13、CU-19、CU-21 边界内，不新增架构 owner，不改变推荐读集。实现时只有在新增独立 owner 改变这些单元职责或 runtime 主链后，才需要更新该上下文索引。
