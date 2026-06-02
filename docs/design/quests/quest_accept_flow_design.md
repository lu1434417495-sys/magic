# 任务接取流程与对话设计

> 基于现有系统约束：无对话树、服务导向交互（按钮触发）、契约板UI已存在。
>
> 目标：在不动架构的前提下，通过 schema 扩展和 UI 流程设计，实现有"对话感"的任务接取体验。

---

## 一、现状分析

### 1.1 已有系统能力

| 组件 | 现状 |
|------|------|
| `QuestDef` | 有 `provider_interaction_id`、`accept_requirements`、`description`，无对话/反馈字段 |
| `QuestProgressService.accept_quest()` | 检查重复/已完成状态，**不检查 `accept_requirements`** |
| 契约板 UI | 已存在弹窗，展示条目列表（名称/目标/奖励/状态），点击条目直接接取 |
| 状态流转 | `available → active → completed → rewarded`（或 `claimable`） |
| 交互约束 | 无对话树，所有 NPC 交互通过 `interaction_script_id` 路由到服务执行器 |

### 1.2 关键缺失

1. **`accept_requirements` 无运行时校验**：配置里有 `"requirement_type": "quest_completed"`，但代码不读。
2. **无接取确认环节**：点击条目直接接取，没有"你确定吗？"的停顿。
3. **无接取"对话"文本**：`description` 是任务背景，不是接取瞬间 NPC 说的话。
4. **无接取反馈**：成功/失败只有系统默认消息，没有任务专属反馈。

---

## 二、接取流程状态机

```
[世界地图]
   │ 进入据点
   ▼
[据点服务面板] ──点击"任务板"──► [契约板弹窗]
                                      │
                                      ▼
                              [任务条目列表]
                              ├─ 可用（白色，可点击）
                              ├─ 条件不足（灰色，显示原因）
                              ├─ 进行中（绿色，显示进度）
                              └─ 待领奖（金色，点击领奖）
                                      │
                         点击可用条目 ▼
                              [详情面板展开]
                              ├─ 任务名称
                              ├─ 背景描述 (description)
                              ├─ 【接取对话】 (accept_dialogue_text)
                              ├─ 目标摘要
                              ├─ 奖励标签
                              └─ [接受契约] 按钮
                                      │
                         点击接受按钮 ▼
                              [接取条件预检]
                              ├─ 失败 ──► 按钮变灰，显示 disabled_reason
                              │            底部反馈：accept_feedback_failure
                              │
                              └─ 通过 ──► [确认弹窗]（若 accept_confirmation_text 非空）
                                           ├─ 取消 ──► 返回详情面板
                                           └─ 确认 ──► [执行接取]
                                                        │
                                                        ▼
                                              [状态更新: inactive → active]
                                                        │
                                                        ▼
                                              [底部反馈: accept_feedback_success]
                                                        │
                                                        ▼
                                              [返回任务板，条目变绿]
```

---

## 三、`accept_requirements` 校验器设计

### 3.1 支持的 requirement_type

```json
{
  "requirement_type": "quest_completed",
  "quest_id": "tutorial_first_blood"
}
```

| requirement_type | 必需字段 | 说明 |
|------------------|---------|------|
| `quest_completed` | `quest_id` | 需已完成指定任务 |
| `quest_active` | `quest_id` | 需已接取且进行中 |
| `quest_not_completed` | `quest_id` | 需未完成过（互斥任务） |
| `item_in_inventory` | `item_id`, `amount` | 需持有指定物品数量 |
| `gold_min` | `amount` | 需持有最少金币 |
| `party_level_min` | `level` | 队伍等级下限（人物等级总和/人数） |
| `settlement_rank_min` | `rank` | 当前据点等级下限 |
| `tag_owned` | `tag` | 需拥有指定标签（如 `empire_soldier`） |
| `world_step_min` | `step` | 世界步数下限（天数） |

### 3.2 校验集成点

在 `QuestProgressService.accept_quest()` 开头添加校验调用：

```csharp
public bool accept_quest(StringName questId, int worldStep = -1, bool allowReaccept = false)
{
    // ... 现有检查 ...

    QuestDef questDef = GetQuestDefObject(questId);
    if (questDef != null)
    {
        string failReason = CheckAcceptRequirements(questDef);
        if (failReason != "")
        {
            _last_accept_failure_reason = failReason;
            return false;
        }
    }
    // ... 继续接取 ...
}
```

`CheckAcceptRequirements` 按 `accept_requirements` 数组逐项检查，**AND 逻辑**（全部满足才通过），返回第一个失败的描述文本。

### 3.3 契约板 UI 预检

在 `_build_contract_board_entry()` 中提前计算，避免玩家点击后才发现接不了：

```csharp
string disabledReason = "";
bool isEnabled = true;
if (stateId == "available" || stateId == "repeatable")
{
    disabledReason = ResolveAcceptRequirementReason(questData);
    isEnabled = disabledReason == "";
}

return new GDictionary
{
    // ...
    ["is_enabled"] = isEnabled,
    ["disabled_reason"] = disabledReason,
};
```

---

## 四、无对话树下的"接取对话"设计

### 4.1 核心原则

没有分支对话树，不代表没有"对话"。用**三层文本叠加**制造对话感：

1. **环境层**（`description`）：任务背景，你站在任务板前看到什么
2. **说话层**（`accept_dialogue_text`）：NPC/任务板对你说的话，接取瞬间展示
3. **反馈层**（`accept_feedback_success/failure`）：操作后的回应

### 4.2 新增字段定义

在 `QuestDef` 中新增：

```csharp
[Export(PropertyHint.MultilineText)]
public string accept_dialogue_text { get; set; } = "";
// 接取对话：NPC/任务板在玩家尝试接取时说的话。
// 支持1-3句，每句换行。例：
// "你看起来能打。"
// "北边狼群啃掉了三个商队。"
// "活着回来，钱是你的。"

[Export]
public string accept_feedback_success { get; set; } = "";
// 接取成功反馈。为空则使用默认："已接取契约 {display_name}。"

[Export]
public string accept_feedback_failure { get; set; } = "";
// 接取失败反馈（条件不足）。为空则使用默认："不满足接取条件：{disabled_reason}"

[Export]
public string accept_confirmation_text { get; set; } = "";
// 确认弹窗文本。为空则不弹窗直接接取。
// 用于高风险/不可逆抉择任务。例：
// "此任务将使你与霜烬帝国为敌。确认接受？"
```

### 4.3 UI 展示方式

**契约板详情面板布局**：

```
┌─────────────────────────────────────┐
│  【狼王挑战】          [进行中]     │  ← display_name + state_label
├─────────────────────────────────────┤
│                                     │
│  村口的狼群出了个首领，毛色灰白，   │  ← description
│  眼睛发红。老猎人说那是被裂隙影响   │
│  的变异种。                         │
│                                     │
│  ── 接取对话 ──                     │  ← 分隔线（淡色）
│  "你杀了那些普通的，很好。"         │  ← accept_dialogue_text
│  "但那只白的……它不一样。"           │
│  "别逞强。"                         │
│                                     │
│  ── 契约目标 ──                     │
│  目标：击败 wolf_alpha ×1           │  ← summary_text
│  奖励：150金 + 解锁 warrior_guard   │  ← cost_label
│                                     │
│  [接受契约]          [返回列表]     │  ← 按钮
│                                     │
└─────────────────────────────────────┘
```

**悬赏板详情面板布局**（更精简）：

```
┌─────────────────────────────────────┐
│  【狼群悬赏】          [可重复]     │
├─────────────────────────────────────┤
│  危险度：★★☆☆☆                     │  ← 从敌人模板推算
│  目标：wolf_pack ×5                 │
│  奖励：60金 + beast_hide            │
│                                     │
│  "五只。不多。但别被围住。"         │  ← accept_dialogue_text（可选）
│                                     │
│  [直接接取]                         │  ← 无确认弹窗
└─────────────────────────────────────┘
```

---

## 五、不同 Provider 的接取流程差异

### 5.1 `service_contract_board`（契约板）

- **展示**：完整详情面板（描述 + 接取对话 + 目标 + 奖励）
- **确认**：若 `accept_confirmation_text` 非空，弹出确认窗口
- **接取**：单任务接取，点击后状态变为 `active`
- **限制**：同时进行中任务上限（建议 5-8 个，超限时新任务 disabled）

### 5.2 `service_bounty_registry`（悬赏板）

- **展示**：精简列表（名称 + 危险度星级 + 目标 + 奖励），无展开详情
- **确认**：无确认弹窗，点击即接取
- **批量**：支持多选复选框，一键接取多个悬赏
- **限制**：悬赏任务不占用进行中任务槽位（独立计数）
- **危险度**：从 `enemy_template` 的 `challenge_rating` 推算 1-5 星

### 5.3 特殊 NPC（未来扩展）

- `provider_interaction_id` = NPC 的 `interaction_script_id`（如 `npc_blacksmith_hrothgar`）
- 需要在 `QuestProviderContentRules` 中注册新的 provider_id
- 接取时展示 NPC 头像 + `accept_dialogue_text`
- 可能绑定 `settlement_action` 前置交互（如先对话再出现任务）

---

## 六、代码变更清单

### 6.1 `QuestDef.cs`（schema 扩展）

新增4个字段，并在 `validate_schema()`、`to_dict()`、`from_dict()` 中处理。

### 6.2 `QuestProgressService.cs`（校验实现）

- 新增 `CheckAcceptRequirements(QuestDef questDef)` 方法
- 在 `accept_quest()` 开头调用
- 暴露 `GetLastAcceptFailureReason()` 供 UI 使用

### 6.3 `GameRuntimeSettlementCommandHandler.cs`（UI 集成）

- `_build_contract_board_entry()`：添加 `disabled_reason` 预计算
- `_submit_contract_board_quest_action()`：添加确认弹窗分支
- 新增 `_resolve_accept_requirement_reason(QuestDef)` 方法

### 6.4 `QuestContentValidator.cs`（配置校验）

- 新增 `AppendAcceptRequirementErrors()`：检查 `requirement_type` 合法性、引用的 `quest_id` 是否存在

### 6.5 `GameTextSnapshotRenderer.cs`（文本渲染）

- `BuildContractBoardLines()`：若新增字段存在，追加到详情面板输出中

---

## 七、现有31个已验证任务的接取对话示例

### 主世界任务（main_world_quests.json）

#### `tutorial_first_blood` — 第一滴血
```json
{
  "accept_dialogue_text": "你看起来还没沾过血。\n北边林子里有三只狼在啃商队的货。\n把它们清理掉，回来找我。",
  "accept_feedback_success": "好。第一个任务。记住：死了就什么都没有了。",
  "accept_feedback_failure": "你连门都出不了？先学会走路再来。",
  "accept_confirmation_text": ""
}
```

#### `tutorial_wolf_alpha` — 狼王挑战
```json
{
  "accept_dialogue_text": "你杀了那些普通的，很好。\n但那只白的……它不一样。\n眼睛是红的，像裂隙里的光。别逞强。",
  "accept_feedback_success": "你身上有血腥味了。不是坏事。",
  "accept_feedback_failure": "那只白狼还在。等你能面对它的时候再来。",
  "accept_confirmation_text": ""
}
```

#### `main_border_patrol` — 边境巡逻
```json
{
  "accept_dialogue_text": "帝国和联邦都在喊缺人手。\n边境上有五批狼骑兵在转悠。\n你去，还是我去？",
  "accept_feedback_success": "边境线又往前推了一步。暂时。",
  "accept_feedback_failure": "巡逻不是散步。等你准备好再说。",
  "accept_confirmation_text": ""
}
```

#### `main_mist_investigation` — 迷雾调查
```json
{
  "accept_dialogue_text": "迷雾不是天气。\n有人在雾里看到了……东西。\n带两份样本回来，我要知道那是什么。",
  "accept_feedback_success": "样本我收下了。别问我要做什么。",
  "accept_feedback_failure": "没有样本，就没有情报。规矩。",
  "accept_confirmation_text": ""
}
```

#### `main_crack_seal` — 裂隙封印
```json
{
  "accept_dialogue_text": "裂隙在扩大。不是比喻，是真的在扩大。\n两只迷雾兽和一只织者守在那里。\n杀了它们，封印能撑多久算多久。",
  "accept_feedback_success": "封印稳住了。但你知道的，这只是补丁。",
  "accept_feedback_failure": "裂隙不会等人。等你觉得能行了再来。",
  "accept_confirmation_text": "此任务涉及迷雾核心区域。确认接受？"
}
```

#### `main_ashen_gate` — 灰烬之门
```json
{
  "accept_dialogue_text": "灰烬交界不是一个地方。是一个选择。\n你可以选择永远不去。\n但如果你想去， Gate 已经开了。",
  "accept_feedback_success": "你踏过了那道门。有些人过去，有些人没回来。",
  "accept_feedback_failure": "门还在。但你的决心不够。",
  "accept_confirmation_text": "进入灰烬交界将改变你的世界线。此选择不可逆。确认？"
}
```

### 灰烬交界任务（ashen_intersection_quests.json）

#### `ashen_awakening` — 灰烬觉醒
```json
{
  "accept_dialogue_text": "你醒了。或者你以为你醒了。\n这里的规则不一样。\n先杀两只迷雾兽，证明你能呼吸这里的空气。",
  "accept_feedback_success": "你还站着。在这里，站着就是胜利。",
  "accept_feedback_failure": "灰烬会教每一个人谦卑。你还没学会。",
  "accept_confirmation_text": ""
}
```

#### `ashen_mist_weaver` — 织者猎杀
```json
{
  "accept_dialogue_text": "织者不编织布。它们编织现实。\n杀两只。别听它们说话。\n听了你就不是你了。",
  "accept_feedback_success": "你的记忆还完整吗？检查一下。",
  "accept_feedback_failure": "织者的低语比刀锋利。你还不够聋。",
  "accept_confirmation_text": ""
}
```

#### `ashen_cathedral_1` — 大教堂之门
```json
{
  "accept_dialogue_text": "那座教堂不应该存在。\n但它在那里，而且门是开的。\n进去。或者转身。选择一个。",
  "accept_feedback_success": "你进了门。很多选择中的一个。",
  "accept_feedback_failure": "门是开的，但你还没准备好走进去。",
  "accept_confirmation_text": "大教堂内部存在精神污染风险。确认进入？"
}
```

#### `ashen_rift_1` — 深渊裂界
```json
{
  "accept_dialogue_text": "裂界深处有人在笑。\n或者那是你自己的声音。\n只有下去才知道。",
  "accept_feedback_success": "你下去了。也上来了。不是每个人都能说这句话。",
  "accept_feedback_failure": "深渊不赶人。它等。",
  "accept_confirmation_text": "深渊裂界为高风险区域。死亡将丢失所有未保存进度。确认？"
}
```

### 悬赏任务（bounty_quests.json）

#### `bounty_wolf_pack` — 狼群悬赏
```json
{
  "accept_dialogue_text": "五只狼。标准活。\n别太骄傲。",
  "accept_feedback_success": "标记完成。下一张。",
  "accept_feedback_failure": "",
  "accept_confirmation_text": ""
}
```

#### `bounty_mist_weaver` — 迷雾织者悬赏
```json
{
  "accept_dialogue_text": "一只织者。别听它说话。\n听了就忘了你来干嘛的。",
  "accept_feedback_success": "织者死了。你的记忆还在吗？",
  "accept_feedback_failure": "",
  "accept_confirmation_text": ""
}
```

#### `bounty_wolf_alpha` — 狼首悬赏
```json
{
  "accept_dialogue_text": "首领级。单只。\n但单只够杀一队人。\n你确定要接？",
  "accept_feedback_success": "首领的皮我收了。酒钱你的。",
  "accept_feedback_failure": "",
  "accept_confirmation_text": ""
}
```

---

## 八、任务板状态标签扩展

当前状态标签：
- `available` → "状态：待接取"
- `active` → "状态：进行中"
- `claimable` → "状态：待领奖励"
- `repeatable` → "状态：可重复接取"
- `completed` → "状态：已完成"

新增（由 `disabled_reason` 驱动）：
- `locked_quest` → "状态：需完成前置任务"
- `locked_level` → "状态：等级不足"
- `locked_gold` → "状态：需保证金"
- `locked_item` → "状态：需携带指定物品"
- `locked_slot` → "状态：任务槽已满"

---

## 九、验收标准

1. 打开契约板时，条件不足的任务显示灰色并标注具体原因（如"需先完成：第一滴血"）
2. 点击可用任务后，详情面板展示 `accept_dialogue_text`（若有）
3. 点击"接受契约"时，若 `accept_confirmation_text` 非空，弹出确认弹窗
4. 接取成功后，底部状态栏显示 `accept_feedback_success`（或默认消息）
5. 接取失败后，底部状态栏显示 `accept_feedback_failure`（或默认消息）
6. 悬赏板无确认弹窗，点击直接接取
7. 全部31个已验证任务配置包含新增的接取对话字段并通过 schema 验证
