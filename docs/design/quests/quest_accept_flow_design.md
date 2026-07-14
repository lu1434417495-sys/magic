# 任务接取流程与对话设计

> 修订目标：在不破坏当前 Godot 4.6 C# 项目的 typed runtime 边界、服务导向交互、严格 schema 校验和无对话树约束的前提下，实现更有“对话感”的任务接取体验，并支持 NPC 作为任务发布者。
>
> 重要约束：本文不引入旧 payload/schema 兼容、legacy alias、fallback migration。若后续确实需要兼容旧 quest 配置，必须先单独确认原因和影响范围。
>
> **实现状态**：Phase 2（NPC 委托面板 `NpcQuestOfferDialog`）已实现。

---

## 一、问题定义

### 1.1 目标行为

- 契约板、悬赏板、NPC 委托都能在接取前展示任务背景、接取文本、目标和奖励。
- 接取前能预判是否满足条件，条件不足时显示稳定的禁用原因。
- 接取成功或失败后显示任务专属反馈文本。
- 高风险任务可要求确认，但确认流程仍保持按钮式服务交互，不引入对话树。
- NPC 可以发布任务，但 NPC 任务的 provider、列表渠道和 UI 入口必须有清晰 ownership。

### 1.2 非目标

- 不实现分支对话树。
- 不引入 NPC 头像、美术资源或好感度系统。
- 不把任务规则塞进 UI 节点。
- 不让 `GameRuntimeFacade`、`GameRuntimeSettlementCommandHandler` 或 `QuestProgressService` 承担不属于自己的大而全规则校验。
- 不为旧 quest payload 自动推断新增字段，除非用户明确要求兼容。

---

## 二、当前 Ownership

### 2.1 内容与 schema

- `QuestDef` 是 quest 内容 resource/schema owner。
- `ProgressionContentRegistry` / `GameSession` / `GameContentCatalog` 是 typed quest catalog 的正式读链。
- `QuestContentValidator` 校验 quest 内容引用和 schema。
- `QuestProviderContentRules` 当前只拥有固定 service provider 白名单：`service_contract_board`、`service_bounty_registry`。
- `data/configs/quests/*.json` 当前不是 live runtime content。现有代码中只有 `tests/runtime/validation/run_quest_config_validation.cs` 读取这些 JSON；运行时实际 quest 来自 `ProgressionContentRegistry._register_seed_quests()` 的 seed definitions。本设计将 quest 内容来源迁移为 `data/configs/quests/*.tres`，与项目中其他 893 个 `.tres` 配置保持一致。

### 2.2 运行时状态

- `PartyState` 拥有 `active_quests`、`claimable_quests`、`completed_quest_ids`。
- `QuestProgressService` 负责 quest 状态流转：接取、进度、完成、领奖。
- 背包、金币、世界步数、据点状态分别由 warehouse、party/world/settlement runtime owner 提供，不属于 `QuestProgressService`。

### 2.3 UI 与命令

- 据点服务入口走 `SettlementActionRequest` typed request。
- 契约板和悬赏板当前复用 settlement service modal 数据链，并通过 active modal context 校验提交来源。
- `GameRuntimeSnapshotBuilder` 和 `GameTextSnapshotRenderer` 是 headless/text surface 的稳定投影边界。

---

## 三、核心架构决策

### 3.1 接取条件必须独立成 typed evaluator

不要在 `QuestProgressService.AcceptQuest(...)` 内直接读取 `accept_requirements`。它没有足够上下文，也不应该依赖仓库、金币、据点或世界状态。

新增规则服务：

```csharp
internal sealed class QuestAcceptRequirementEvaluator
{
    internal QuestAcceptAvailabilityResult Evaluate(
        QuestDef questDef,
        QuestAcceptContext context
    );
}
```

新增上下文 DTO：

```csharp
internal sealed class QuestAcceptContext
{
    public PartyState PartyState { get; init; }
    public PartyWarehouseService WarehouseService { get; init; }
    public int PartyGold { get; init; }
    public int WorldStep { get; init; }
    public string SettlementId { get; init; } = "";
    public int SettlementTier { get; init; }
    public IReadOnlyDictionary<StringName, QuestDef> QuestDefs { get; init; }
}
```

新增结果 DTO：

```csharp
internal sealed class QuestAcceptAvailabilityResult
{
    public bool CanAccept { get; init; }
    public StringName LockReasonId { get; init; } = "";
    public string DisabledReason { get; init; } = "";
}
```

`QuestProgressService` 仍只做状态级防线：

- quest id 非空。
- quest 存在于当前 typed catalog。
- 不能重复接取 active / claimable。
- 非 repeatable 不能重新接取 completed。
- 成功后写入 `PartyState`。

运行时入口必须在调用 `AcceptQuest` 前用 evaluator 评估；提交时要重新评估一次，不能只相信打开面板时的 UI 预检。

### 3.2 接取状态与生命周期状态分离

任务生命周期状态仍只表达 quest progress：

- `available`
- `active`
- `claimable`
- `repeatable`
- `completed`
- `empty`

条件不足不是生命周期状态。UI 投影使用独立字段：

- `is_enabled`
- `disabled_reason`
- `lock_reason_id`

状态标签可以展示“状态：需完成前置任务”，但它应由 availability result 派生，不应写回 quest lifecycle state。

### 3.3 Provider 与展示渠道分离

`provider_kind` 表示任务发布者类型，不负责决定任务出现在哪些 UI 列表里。

新增字段：

```csharp
[Export]
public StringName provider_kind { get; set; } = "";

[Export]
public Godot.Collections.Array<StringName> listing_channels { get; set; } = new();
```

合法 provider kind：

- `service_contract_board`
- `service_bounty_registry`
- `npc`

合法 listing channel：

- `contract_board`
- `bounty_registry`
- `npc_offer`

字段语义：

| 字段 | 含义 |
|---|---|
| `provider_kind` | 发布者类型，决定接取入口规则和默认 UI 语气 |
| `provider_interaction_id` | 具体 service id 或 NPC `interaction_script_id` |
| `listing_channels` | 任务可出现在哪些列表/面板 |

示例：

```json
{
  "provider_kind": "npc",
  "provider_interaction_id": "npc_blacksmith_hrothgar",
  "listing_channels": ["npc_offer"]
}
```

若一个 NPC 任务也要出现在契约板，应显式写：

```json
{
  "provider_kind": "npc",
  "provider_interaction_id": "npc_blacksmith_hrothgar",
  "listing_channels": ["npc_offer", "contract_board"]
}
```

不要通过“契约板查询时不排除 NPC provider”来实现汇总；那会让 provider id 同时承担发布者和列表渠道两种含义。

### 3.4 Schema 保持严格

`QuestDef.FromDictionary(...)` 当前使用 exact field 校验。新增字段后应更新正式字段集合，而不是改成允许任意额外字段。

新增正式字段：

- `provider_kind`
- `listing_channels`
- `accept_dialogue_text`
- `accept_feedback_success`
- `accept_feedback_failure`
- `accept_confirmation_text`

所有 quest 配置需要一次性补齐这些字段。没有 `provider_kind` 的旧配置不自动推断为 Service，除非后续明确批准兼容逻辑。

### 3.5 Live quest 内容加载路径是 P0 前置

由于 `data/configs/quests/*.json` 当前不是 live content，第一阶段不能只更新 JSON 和 validator。必须先选择正式内容来源。

**推荐方案：迁移到 `.tres`，并迁移 seed quests。**

项目其他 893 个配置已经使用 `.tres`，`QuestDef` 本身也是 `[GlobalClass] Resource`，因此 quest 配置没有理由继续用 JSON。推荐加载路径：

```text
ProgressionContentRegistry.Build()
  -> QuestContentRegistry.LoadFromDirectory("res://data/configs/quests")
  -> DirAccess 遍历 *.tres
  -> ResourceLoader.Load<QuestDef>(path)
  -> _register_quest(...)
  -> GameSession / GameContentCatalog expose typed quest catalog
```

实施原则：

- `data/configs/quests/*.tres` 成为正式 quest authoring source；每个 quest 一个 `.tres` 文件。
- 现有的 3 个 JSON 文件（31 条 quest）需要一次性转换为 `.tres`。
- `ProgressionContentRegistry._register_seed_quests()` 中的 4 个硬编码 quest seed 应迁移为 `.tres` 或删除，避免双源真相。
- `QuestDef.FromDictionary(...)` 的 exact-field JSON schema 校验退化为仅用于可能的过渡测试；正式 schema 强校验由 `QuestDef.ValidateSchema()` 和 `QuestContentValidator` 承担。
- 严格 schema 和 31 条 quest `.tres` 更新必须与加载路径同一阶段完成，否则会出现“测试通过但 runtime 不变”的断层。

备选方案：如果短期不实现 `.tres` 迁移，则第一阶段只能更新 `ProgressionContentRegistry` 里的 seed quest definitions 和 runtime 投影；此时不要把“更新 JSON/`.tres`”写作 runtime 验收标准。该方案属于临时妥协，后续仍需迁移到 `.tres`。

### 3.6 确认流程是 modal context 状态，不是 quest state

`accept_confirmation_text` 只影响 UI 提交流程，不改变 quest state。

当前没有可直接复用的 settlement service 确认 modal。`SubmapEntryWindow` 只服务 `SubmapConfirm` / `BattleStartConfirm` / `GameOver`，`ShopWindow` 的 confirm 按钮会直接提交。因此确认流程必须明确新增 UI 状态。

推荐实现：在 `ShopWindow` / 后续 `NpcQuestOfferDialog` 内增加确认子状态，而不是切换到 `RuntimeModalKind.SubmapConfirm`。

原因：

- 契约板、悬赏板、锻造、驿站已共用 `ShopWindow` 数据结构；接取确认属于当前服务 modal 的局部状态。
- 切到 `SubmapConfirm` 会要求保存并恢复 contract board context，还会让通用确认窗口承担 settlement service 语义。
- 内置确认子状态可以通过 entry payload 中的 `pending_confirmation_quest_id` / `pending_confirmation_text` 控制按钮文案和二次提交 payload，影响面更局部。

推荐流程：

1. 玩家在契约板或 NPC 委托面板点击接取。
2. runtime 重新评估 accept requirements。
3. 如果不满足，刷新当前 modal context 并显示失败反馈。
4. 如果满足且 `accept_confirmation_text` 非空，当前 modal context 设置：
   - `pending_confirmation_quest_id`
   - `pending_confirmation_text`
   - `pending_confirmation_source`
5. `ShopWindow` 或 `NpcQuestOfferDialog` 显示当前 modal 内部的确认子面板。
6. 玩家确认后再次提交，payload 显式带 `confirm_accept=true`。
7. runtime 再次评估条件，通过后才调用 `CommandAcceptQuestTyped(...)`。

这保证确认弹窗是 presentation detail，真正的接取仍由 runtime typed command 执行。

---

## 四、Accept Requirement 设计

### 4.1 最小切片支持

当前正式 quest 配置中已经出现的需求是 `quest_completed`。第一阶段只实现并验证：

| requirement_type | 字段 | owner |
|---|---|---|
| `quest_completed` | `quest_id` | `PartyState.completed_quest_ids` |
| `quest_active` | `quest_id` | `PartyState.active_quests` |
| `quest_not_completed` | `quest_id` | `PartyState.completed_quest_ids` |

这些规则只依赖 `PartyState` 和 quest catalog，风险最低。

### 4.2 后续可扩展规则

下列规则应在确实有内容需求时再实现：

| requirement_type | 字段 | 依赖 owner | 注意事项 |
|---|---|---|---|
| `item_in_inventory` | `item_id`, `amount` | `PartyWarehouseService` | 需要 item reference validation |
| `gold_min` | `amount` | `PartyState.GetGold()` | 不扣费，只做下限检查 |
| `party_level_min` | `level` | `PartyState` member progress | 需先定义“队伍等级”的正式算法 |
| `settlement_tier_min` | `tier` | `WorldMapSettlementRecordData.Tier` / settlement payload `tier` | 使用现有 tier，不新增 rank 概念 |
| `world_step_min` | `step` | world time/runtime | 只读 world step，不修改时间 |

`tag_owned` 不进入当前设计的可扩展规则清单。项目当前没有 party-level owned tag 系统；若未来要支持，必须先设计 tag owner、写入来源、存档字段和 validator。

### 4.3 内容校验

`QuestContentValidator` 增加：

- `AppendAcceptRequirementErrors(...)`
- `AppendProviderKindErrors(...)`
- `AppendListingChannelErrors(...)`

校验原则：

- `requirement_type` 必须能转成 `QuestAcceptRequirementKind`。
- `quest_id` 引用必须存在于当前 quest catalog。
- `item_id` 引用必须存在于 item catalog。
- `amount/level/tier/step` 必须是正整数或非负整数，按规则定义。
- 暂未实现的 requirement type 不允许进入正式内容。

---

## 五、接取文本字段

新增字段：

```csharp
[Export(PropertyHint.MultilineText)]
public string accept_dialogue_text { get; set; } = "";

[Export]
public string accept_feedback_success { get; set; } = "";

[Export]
public string accept_feedback_failure { get; set; } = "";

[Export(PropertyHint.MultilineText)]
public string accept_confirmation_text { get; set; } = "";
```

字段语义：

| 字段 | 用途 | 默认行为 |
|---|---|---|
| `description` | 任务背景 | 仍用于详情正文 |
| `accept_dialogue_text` | 接取前展示的发布者文本 | 空则不展示该段 |
| `accept_feedback_success` | 接取成功反馈 | 空则使用 runtime 默认成功消息 |
| `accept_feedback_failure` | 条件不足反馈 | 空则使用 `disabled_reason` 生成默认失败消息 |
| `accept_confirmation_text` | 接取确认文本 | 空则不进入确认态 |

文本字段是内容层数据，不参与规则判断。

---

## 六、契约板与悬赏板流程

### 6.1 窗口数据构建

契约板/悬赏板 entries 继续由 `GameRuntimeSettlementCommandHandler` 从 typed `QuestDef` 构建，但构建时只做投影和 evaluator 调用：

```text
GetQuestDefsTyped()
  -> filter by listing_channels + provider rules
  -> build ContractBoardQuestData
  -> QuestAcceptRequirementEvaluator.Evaluate(...)
  -> project entry dictionary for ShopWindow
```

entry 增加字段：

- `lock_reason_id`
- `accept_dialogue_text`
- `accept_feedback_success`
- `accept_feedback_failure`
- `accept_confirmation_text`

`ShopWindow.ShopEntry` 当前允许 entry payload 携带额外字段，因此这些字段可以留在 entry payload 中；顶层 window data 若新增字段，需要同步更新对应 schema。

`GameRuntimeSettlementCommandHandler._build_contract_board_window_data(...)` 需要保留反馈文本。当前构建函数会重新计算 `state_summary_text`；修订后应在 payload / context 存在非空 `feedback_text` 时优先使用它作为 `state_summary_text`，否则才使用 `_build_contract_board_state_summary(entries)`。

### 6.2 提交流程

提交时沿用 active modal context 校验：

```text
UI submit
  -> SettlementActionRequest / modal payload
  -> active contract board context check
  -> provider/listing/source check
  -> evaluator re-check
  -> optional confirmation context
  -> CommandAcceptQuestTyped
  -> refresh modal context
```

失败时：

- 不修改 quest state。
- 刷新当前 modal context。
- `feedback_text` 使用 `accept_feedback_failure` 或默认“不满足接取条件：{disabled_reason}”。

成功时：

- 接取命令持久化 party state。
- 刷新当前 modal context。
- `feedback_text` 使用 `accept_feedback_success` 或 runtime 默认成功消息。

### 6.3 悬赏板差异

悬赏板可共用同一数据链，但通过 `provider_kind/listing_channels` 区分展示：

- `provider_kind = service_bounty_registry`
- `listing_channels` 包含 `bounty_registry`

第一阶段不实现批量接取和危险度星级。危险度需要正式 enemy difficulty 字段，当前不能从不存在的 `challenge_rating` 推断。

---

## 七、NPC 委托流程

### 7.1 新增 modal owner

NPC 委托不是普通 settlement service entry 的附加文本，而是一个新的 runtime modal。

新增：

- `RuntimeModalKind.NpcQuestOffer`
- active NPC quest offer context
- `GameRuntimeFacade.GetNpcQuestOfferWindowData()`
- `GameRuntimeFacade.SetActiveNpcQuestOfferContext(...)`
- `GameRuntimeFacade.ClearActiveNpcQuestOfferContext()`
- `GameRuntimeSnapshotBuilder.BuildNpcQuestOfferSnapshot()`
- `GameTextSnapshotRenderer.BuildNpcQuestOfferLines()`
- `WorldMapRuntimeProxy.GetNpcQuestOfferWindowData()`
- `WorldMapSystem` 节点绑定与 show/hide 分支
- `scenes/ui/npc_quest_offer_dialog.tscn`
- `scripts/ui/NpcQuestOfferDialog.cs`

### 7.2 打开流程

```text
Settlement service/NPC action selected
  -> resolve interaction_script_id
  -> before generic QuestProviderContentRules service-provider branch:
       TryOpenNpcQuestOffer(settlement_id, action_id, payload)
  -> find quests where:
       provider_kind == npc
       provider_interaction_id == interaction_script_id
       listing_channels contains npc_offer
  -> if none: continue existing service/default interaction
  -> if found: build NpcQuestOffer context and set RuntimeModalKind.NpcQuestOffer
```

NPC 委托面板不替代默认 NPC 交互：只有存在可展示 NPC quest 时才打开。

NPC 委托入口仍是 settlement `available_services` 中的一项，通过 service entry 的 `interaction_script_id` 路由；不是新增世界地图 NPC 点击通道。

不要把 `npc` 加入 `QuestProviderContentRules.SupportedProviderIds()` 后直接复用 generic service-provider 分支。当前 dispatch 中的 service provider 分支会打开 contract board modal；NPC 专属分支必须排在该分支之前。

### 7.3 提交流程

NPC 委托接取和契约板共享 evaluator 与最终 accept command，但拥有独立 modal context 校验：

```text
NpcQuestOfferDialog submit
  -> active npc quest context check
  -> provider_kind == npc
  -> provider_interaction_id matches current context
  -> listing_channels contains npc_offer
  -> evaluator re-check
  -> optional confirmation context
  -> CommandAcceptQuestTyped
  -> refresh NPC quest context
```

### 7.4 多任务展示

一个 NPC 可以有多个任务。窗口数据使用列表投影：

- `npc_name`
- `npc_interaction_id`
- `entries`
- `selected_quest_id`
- `feedback_text`
- `pending_confirmation_quest_id`
- `pending_confirmation_text`

UI 单任务时直接展开，多任务时左侧列表、右侧详情。

---

## 八、配置示例

### 8.1 契约板任务

`data/configs/quests/tutorial_wolf_alpha.tres`：

```ini
[gd_resource type="Resource" script_class="QuestDef" load_steps=2 format=3]

[ext_resource type="Script" path="res://scripts/player/progression/QuestDef.cs" id="1_questdef"]

[resource]
script = ExtResource("1_questdef")
quest_id = &"tutorial_wolf_alpha"
display_name = "狼王挑战"
description = "村口的狼群出了个首领，毛色灰白，眼睛发红。"
provider_kind = &"service_contract_board"
provider_interaction_id = &"service_contract_board"
listing_channels = Array[StringName]([&"contract_board"])
tags = Array[StringName]([&"main_quest"])
accept_requirements = Array[Dictionary]([{
&"requirement_type": &"quest_completed",
&"quest_id": &"tutorial_first_blood"
}])
accept_dialogue_text = "你杀了那些普通的，很好。\n但那只白的……它不一样。\n别逞强。"
accept_feedback_success = "你身上有血腥味了。不是坏事。"
accept_feedback_failure = "先完成第一滴血，再考虑狼王。"
accept_confirmation_text = ""
objective_defs = Array[Dictionary]([{
&"objective_id": &"defeat_wolf_alpha",
&"objective_type": &"defeat_enemy",
&"target_id": &"wolf_alpha",
&"target_value": 1
}])
reward_entries = Array[Dictionary]([{
&"reward_type": &"gold",
&"amount": 150
}])
is_repeatable = false
```

### 8.2 NPC 委托

`data/configs/quests/npc_hrothgar_cave_beasts.tres`：

```ini
[gd_resource type="Resource" script_class="QuestDef" load_steps=2 format=3]

[ext_resource type="Script" path="res://scripts/player/progression/QuestDef.cs" id="1_questdef"]

[resource]
script = ExtResource("1_questdef")
quest_id = &"npc_hrothgar_cave_beasts"
display_name = "清理穴居兽"
description = "矿坑东边的塌方区里钻出了几只穴居兽，工匠们不敢下井。"
provider_kind = &"npc"
provider_interaction_id = &"npc_blacksmith_hrothgar"
listing_channels = Array[StringName]([&"npc_offer"])
tags = Array[StringName]([&"side_quest", &"settlement"])
accept_requirements = Array[Dictionary]([])
accept_dialogue_text = "矿坑东边塌方了。\n你去清理一下里面的穴居兽，\n我把上好的铁料留给你。"
accept_feedback_success = "干净利落。铁料是你的了。"
accept_feedback_failure = "等你准备好再来吧。"
accept_confirmation_text = ""
objective_defs = Array[Dictionary]([{
&"objective_id": &"defeat_cave_beasts",
&"objective_type": &"defeat_enemy",
&"target_id": &"cave_beast",
&"target_value": 3
}])
reward_entries = Array[Dictionary]([{
&"reward_type": &"gold",
&"amount": 80
}, {
&"reward_type": &"item",
&"item_id": &"iron_ingot",
&"quantity": 2
}])
is_repeatable = false
```

---

## 九、实施选项

### 选项 A：只扩契约板文本

- 改动最小。
- 只能解决接取对话和反馈。
- 不能完整支持 NPC 发布者。

失败模式：后续补 NPC 时仍要重做 provider/listing/modal 架构。

### 选项 B：新增 evaluator + 契约板接取体验

- 先建立正确规则 owner。
- 覆盖现有 `accept_requirements` 中最重要的 `quest_completed`。
- 不碰 NPC modal，风险适中。

失败模式：NPC 委托仍未实现，但后续能自然接上。

### 选项 C：完整 provider/listing/evaluator/NPC modal

- 架构最完整。
- 一次性覆盖契约板、悬赏板、NPC 委托和文本快照。
- 改动面最大，需要更多回归。

失败模式：实施周期更长，UI 和 headless 链路都要同步。

推荐：先执行选项 B，再执行选项 C。选项 B 建好 typed evaluator 和 schema 后，NPC modal 只是新增消费方，不会反复改规则层。

---

## 十、最小可交付切片

第一阶段：

1. 扩展 `QuestDef` 严格 schema。
2. 新增 `QuestContentRegistry`（或在 `ProgressionContentRegistry` 内实现 scanner），从 `res://data/configs/quests/*.tres` 加载 live quest catalog，并迁移 4 个 seed quests。
3. 将现有 3 个 JSON 文件中的 31 条 quest 转换为 `.tres`，并补齐新增字段；同步更新/删除 `ProgressionContentRegistry` 中的 seed definitions。
4. 新增 `QuestProviderKind` / `QuestListingChannel` typed rules。
5. 新增 `QuestAcceptRequirementEvaluator`，只实现 quest-state 类 requirement。
6. 契约板 entry 构建时预评估 requirements。
7. 契约板提交时重新评估 requirements。
8. 契约板详情展示 `accept_dialogue_text`。
9. 接取成功/失败使用 quest feedback 文本，并确保 refresh 后反馈不被 state summary 覆盖。
10. `QuestContentValidator` 覆盖新增字段和 requirement 引用。
11. 更新 text snapshot 输出关键字段。

第二阶段：

1. 新增 `RuntimeModalKind.NpcQuestOffer`。
2. 新增 NPC quest offer context/window/snapshot/text renderer。
3. 新增 `NpcQuestOfferDialog.cs/.tscn`。
4. 接入 settlement NPC interaction 分支。
5. 增加 NPC 委托回归测试。

第三阶段：

1. 按实际内容需求扩展 item/gold/world/settlement requirement。
2. 若 enemy 内容新增正式 difficulty 字段，再做悬赏危险度。
3. 评估悬赏多选批量接取。

---

## 十一、测试计划

第一阶段至少覆盖：

- `.tres` quest loader 将 `data/configs/quests/*.tres` 注册进 typed quest catalog，并覆盖 seed quest runtime source。
- `QuestDef.ValidateSchema()` 和 `QuestContentValidator` 对从 `.tres` 加载的 `QuestDef` 执行强校验；如保留 JSON 过渡测试，则 `QuestDef.FromDictionary` 也接受新增正式字段并拒绝缺字段。
- `QuestContentValidator` 检出未知 requirement type、缺 `quest_id`、引用不存在 quest。
- evaluator 判断 `quest_completed/quest_active/quest_not_completed`。
- 契约板打开时 locked 任务 `is_enabled=false` 且有 `disabled_reason`。
- 提交 locked 任务不改变 `PartyState`。
- 提交满足条件任务成功接取并刷新 modal context。
- 接取成功/失败反馈文本优先生效。
- contract board refresh 后 `feedback_text` 不被 `_build_contract_board_state_summary(...)` 覆盖。
- text snapshot 输出 provider、listing、dialogue 或 disabled reason。

第二阶段至少覆盖：

- NPC interaction 有匹配任务时打开 `NpcQuestOffer` modal。
- 无匹配任务时继续走原默认交互。
- NPC quest offer 分支在 generic quest service-provider 分支之前执行，不会被 contract board modal 吞掉。
- NPC modal provider/listing/context mismatch 时拒绝提交。
- NPC 任务接取成功、失败、确认三条路径。
- 关闭 NPC modal 后返回 settlement modal 或世界侧状态符合现有窗口约定。

推荐命令（迁移到 `.tres` 后，`run_quest_config_validation.cs` 需要改为加载 `.tres` 而非 JSON）：

```bash
dotnet build magic.csproj
godot --headless -s res://tests/runtime/validation/run_quest_config_validation.cs
godot --headless -s res://tests/world_map/runtime/run_game_runtime_settlement_command_handler_regression.cs
godot --headless -s res://tests/text_runtime/headless/run_headless_game_test_session_regression.cs
```

---

## 十二、Project Context Units Impact

当前设计涉及：

- CU-02：`QuestDef` schema、content registry、content validation。
- CU-06：settlement/runtime command、contract board modal、NPC quest modal。
- CU-19：回归测试。
- CU-21：headless/text snapshot。

实现第一阶段后，`docs/design/project_context_units.md` 需要更新 CU-02 和 CU-06：记录 quest 内容来源改为 `data/configs/quests/*.tres`、`QuestContentRegistry` 加载路径、`QuestAcceptRequirementEvaluator`、contract board accept availability，以及 text snapshot 的新读图范围。

实现第二阶段后，还需要在 CU-06 中加入 `NpcQuestOffer` modal/context owner 和 WorldMapSystem 展示链。

---

## 十三、验收标准

第一阶段完成时：

1. 全部正式 quest 配置显式包含新增 schema 字段并通过 validation。
2. `.tres` quest 内容被 live runtime catalog 消费；原 seed quest definitions 已迁移或删除，避免双源真相。
3. 不满足前置任务的 quest 在契约板中禁用并显示稳定原因。
4. 接取提交会重新评估条件，不能通过伪造 UI payload 绕过。
5. 详情文本展示 `description`、`accept_dialogue_text`、目标、奖励。
6. 成功/失败反馈优先使用 quest 专属文本，且刷新 modal 后仍可见。
7. `QuestProgressService` 不直接依赖仓库、金币、据点或世界上下文。
8. 未经确认不引入旧配置兼容逻辑。

第二阶段完成时：

1. NPC interaction 能打开 `NpcQuestOfferDialog`。
2. NPC provider/listing/context 校验稳定。
3. NPC 委托接取成功/失败/确认流程都有回归覆盖。
4. snapshot 和 text renderer 能显示 NPC 委托 modal 状态。

---

*文档版本：2026-06-28，架构修订版*
