# Quest Accept Flow Phase 2 — NPC Quest Offer Modal Implementation Plan

> **Status:** Phase 2 implemented in branch `codex/phantasmal-kill-tdd`. This plan is kept for reference.
>
> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Implement the NPC quest offer modal: when a settlement NPC interaction is selected and that NPC has associated `provider_kind = "npc"` quests, open a dedicated `NpcQuestOfferDialog` that displays the NPC name, accept dialogue, objectives, rewards, and supports accept success/failure/confirmation flows.

**Architecture:** Reuse the Phase 1 `QuestAcceptRequirementEvaluator` and typed accept command. Add a new `RuntimeModalKind.NpcQuestOffer` with its own active context, window data builder, snapshot, and text renderer. Insert an explicit NPC branch into `GameRuntimeSettlementCommandHandler._dispatch_settlement_action` before the generic `QuestProviderContentRules` service-provider branch so `npc` providers are not swallowed by the contract-board modal.

**Tech Stack:** Godot 4.6 C#, GDScript/C# UI scenes, dotnet CLI, headless Godot regression runners.

## Global Constraints

- No branch dialogue trees.
- No NPC portraits, art assets, or reputation systems in Phase 2.
- Do not push quest rules into UI nodes.
- `QuestProgressService` must not directly depend on warehouse, gold, settlement, or world state.
- Old quest payloads are not auto-inferred; all live quest content must explicitly contain the new schema fields.
- Quest content source is `data/configs/quests/*.tres`.
- NPC provider must NOT be added to `QuestProviderContentRules.SupportedProviderIds()`; the NPC branch must run before the generic service-provider branch.
- Follow existing file organization and naming: tabs for indentation, `snake_case` files/methods, `PascalCase` public classes.
- All code changes must be covered by headless regression tests.

---

## File Structure

### New files

| File | Responsibility |
|---|---|
| `scripts/ui/NpcQuestOfferDialog.cs` | Control-root script for NPC quest offer modal: renders NPC name, accept dialogue, objectives, rewards, accept/return buttons, confirmation sub-state. |
| `scenes/ui/npc_quest_offer_dialog.tscn` | Godot scene for the modal (Control + Shade + layout containers). |
| `tests/world_map/runtime/run_npc_quest_offer_regression.cs` | Headless regression for NPC quest offer open/accept/failure/confirmation/close paths. |
| `data/configs/quests/npc_blacksmith_hrothgar_cave_beasts.tres` | Example NPC quest for regression and documentation. |

### Modified files

| File | Responsibility |
|---|---|
| `scripts/systems/game_runtime/RuntimeModalKind.cs` | Add `NpcQuestOffer` enum value and `ToPayloadValue` branch. |
| `scripts/systems/game_runtime/GameRuntimeFacade.cs` | Add active NPC quest offer context field; add `GetNpcQuestOfferWindowData`, `SetActiveNpcQuestOfferContext`, `ClearActiveNpcQuestOfferContext`, `GetActiveNpcQuestOfferContext`. |
| `scripts/systems/game_runtime/WorldMapRuntimeProxy.cs` | Add `GetNpcQuestOfferWindowData` passthrough. |
| `scripts/systems/game_runtime/GameRuntimeSnapshotBuilder.cs` | Add `BuildNpcQuestOfferSnapshot` and include it in headless snapshot. |
| `scripts/utils/GameTextSnapshotRenderer.cs` | Add `BuildNpcQuestOfferLines` and append `"NPC_QUEST_OFFER"` section. |
| `scripts/systems/game_runtime/WorldMapSystem.cs` | Add `npc_quest_offer_dialog` field, bind node, connect signals, add `RenderWindows` branch, add close handler. |
| `scripts/systems/game_runtime/GameRuntimeSettlementCommandHandler.cs` | Insert NPC branch in `_dispatch_settlement_action`; add `_try_open_npc_quest_offer`, `_build_npc_quest_offer_window_data`, `_submit_npc_quest_offer_action`. |
| `scripts/systems/game_runtime/GameRuntimeRewardFlowHandler.cs` | Add `NpcQuestOffer` case to modal close switch if present. |
| `docs/design/project_context_units.md` | Update CU-06 with `NpcQuestOffer` modal/context owner and WorldMapSystem show/hide chain. |

---

## Task 1: Add `RuntimeModalKind.NpcQuestOffer` and facade/proxy context plumbing

**Files:**
- Modify: `scripts/systems/game_runtime/RuntimeModalKind.cs`
- Modify: `scripts/systems/game_runtime/GameRuntimeFacade.cs`
- Modify: `scripts/systems/game_runtime/WorldMapRuntimeProxy.cs`
- Test: `tests/runtime/facade/run_game_runtime_snapshot_builder_regression.cs` (if it asserts modal kinds)

**Interfaces:**
- Consumes: existing contract-board context pattern
- Produces:
  - `RuntimeModalKind.NpcQuestOffer`
  - `GameRuntimeFacade.GetNpcQuestOfferWindowData()`
  - `GameRuntimeFacade.SetActiveNpcQuestOfferContext(GDictionary)`
  - `GameRuntimeFacade.ClearActiveNpcQuestOfferContext()`
  - `GameRuntimeFacade.GetActiveNpcQuestOfferContext()`
  - `WorldMapRuntimeProxy.GetNpcQuestOfferWindowData()`

- [x] **Step 1: Extend enum and payload mapping**

In `RuntimeModalKind.cs`, add `NpcQuestOffer` to the enum and add a `"npc_quest_offer"` branch in `ToPayloadValue`.

- [x] **Step 2: Add active context fields and methods to facade**

Mirror the contract-board block in `GameRuntimeFacade.cs`:

```csharp
internal readonly Dictionary<string, object> _active_npc_quest_offer_context = new(StringComparer.Ordinal);

public GDictionary GetNpcQuestOfferWindowData() =>
    _settlement_command_handler.GetNpcQuestOfferWindowData();

internal void SetActiveNpcQuestOfferContext(GDictionary context) =>
    ReplacePlainPayload(
        _active_npc_quest_offer_context,
        context,
        "GameRuntimeFacade.active_npc_quest_offer_context"
    );

internal void ClearActiveNpcQuestOfferContext() => _active_npc_quest_offer_context.Clear();

public GDictionary GetActiveNpcQuestOfferContext() =>
    ProjectPlainPayload(
        _active_npc_quest_offer_context,
        "GameRuntimeFacade.active_npc_quest_offer_context"
    );
```

- [x] **Step 3: Add proxy passthrough**

In `WorldMapRuntimeProxy.cs`, add:

```csharp
public GDictionary GetNpcQuestOfferWindowData() =>
    _runtime.GetNpcQuestOfferWindowData();
```

- [x] **Step 4: Build and run existing regressions**

```bash
dotnet build magic.csproj
godot --headless -s res://tests/runtime/facade/run_game_runtime_snapshot_builder_regression.cs
```

Expected: PASS.

- [x] **Step 5: Commit**

```bash
git add scripts/systems/game_runtime/RuntimeModalKind.cs scripts/systems/game_runtime/GameRuntimeFacade.cs scripts/systems/game_runtime/WorldMapRuntimeProxy.cs
git commit -m "feat: add NpcQuestOffer modal kind and facade context plumbing"
```

---

## Task 2: Add NPC quest offer window data builder in settlement command handler

**Files:**
- Modify: `scripts/systems/game_runtime/GameRuntimeSettlementCommandHandler.cs`
- Test: `tests/world_map/runtime/run_npc_quest_offer_regression.cs` (new)

**Interfaces:**
- Consumes: `QuestAcceptRequirementEvaluator`, `QuestAcceptContext`, `QuestDef` with `provider_kind == "npc"`
- Produces:
  - `GetNpcQuestOfferWindowData()`
  - `_try_open_npc_quest_offer(...)`
  - `_build_npc_quest_offer_window_data(...)`

- [x] **Step 1: Add NPC branch in `_dispatch_settlement_action`**

Before the generic `QuestProviderContentRules.IsSupportedProviderId` branch, insert:

```csharp
if (_try_open_npc_quest_offer(settlement_id, action_id, payload, out GDictionary npcResult))
    return npcResult;
```

- [x] **Step 2: Implement `_try_open_npc_quest_offer`**

```csharp
private bool _try_open_npc_quest_offer(
    string settlement_id,
    string action_id,
    GDictionary payload,
    out GDictionary result
)
{
    result = new GDictionary();
    string interactionScriptId = ReadString(payload, "interaction_script_id");
    if (interactionScriptId == "")
        return false;

    var npcQuests = new List<QuestDef>();
    foreach (QuestDef questDef in GetQuestDefsTyped().Values)
    {
        if (questDef.provider_kind != "npc")
            continue;
        if (questDef.provider_interaction_id != interactionScriptId)
            continue;
        if (!questDef.listing_channels.Contains("npc_offer"))
            continue;
        npcQuests.Add(questDef);
    }

    if (npcQuests.Count == 0)
        return false;

    GDictionary windowData = _build_npc_quest_offer_window_data(settlement_id, interactionScriptId, npcQuests);
    SetActiveNpcQuestOfferContext(windowData);
    SetActiveModalKind(RuntimeModalKind.NpcQuestOffer);
    result = CommandOk($"已打开 {interactionScriptId} 的委托。");
    return true;
}
```

- [x] **Step 3: Implement `_build_npc_quest_offer_window_data`**

```csharp
private GDictionary _build_npc_quest_offer_window_data(
    string settlement_id,
    string npcInteractionId,
    List<QuestDef> npcQuests
)
{
    var entries = new Godot.Collections.Array<GDictionary>();
    foreach (QuestDef questDef in npcQuests)
    {
        QuestAcceptAvailabilityResult availability = _quest_accept_evaluator.Evaluate(
            questDef,
            _build_quest_accept_context()
        );
        entries.Add(new GDictionary
        {
            ["quest_id"] = questDef.quest_id.ToString(),
            ["display_name"] = questDef.display_name,
            ["description"] = questDef.description,
            ["accept_dialogue_text"] = questDef.accept_dialogue_text,
            ["summary_text"] = _build_contract_board_objective_summary(
                _build_contract_board_quest_data(questDef)
            ),
            ["cost_label"] = _build_contract_board_reward_label(questDef.reward_entries),
            ["is_enabled"] = availability.CanAccept,
            ["disabled_reason"] = availability.DisabledReason,
            ["lock_reason_id"] = availability.LockReasonId,
            ["accept_feedback_success"] = questDef.accept_feedback_success,
            ["accept_feedback_failure"] = questDef.accept_feedback_failure,
            ["accept_confirmation_text"] = questDef.accept_confirmation_text,
        });
    }

    QuestDef selectedQuest = npcQuests[0];
    return new GDictionary
    {
        ["settlement_id"] = settlement_id,
        ["action_id"] = "", // no service action id for NPC
        ["npc_interaction_id"] = npcInteractionId,
        ["npc_name"] = _resolve_npc_display_name(npcInteractionId),
        ["selected_quest_id"] = selectedQuest.quest_id.ToString(),
        ["entries"] = entries,
        ["feedback_text"] = "",
    };
}

private string _resolve_npc_display_name(string npcInteractionId)
{
    // Strip "npc_" prefix and convert underscores to spaces for a readable title.
    if (npcInteractionId.StartsWith("npc_"))
        npcInteractionId = npcInteractionId.Substring(4);
    return npcInteractionId.Replace("_", " ");
}
```

- [x] **Step 4: Build and run existing regressions**

```bash
dotnet build magic.csproj
godot --headless -s res://tests/world_map/runtime/run_game_runtime_settlement_command_handler_regression.cs
```

Result: PASS.

- [x] **Step 5: Commit**

```bash
git add scripts/systems/game_runtime/GameRuntimeSettlementCommandHandler.cs
git commit -m "feat: add NPC quest offer window data builder and dispatch branch"
```

---

## Task 3: Add NPC quest offer submission handler

**Files:**
- Modify: `scripts/systems/game_runtime/GameRuntimeSettlementCommandHandler.cs`
- Test: `tests/world_map/runtime/run_npc_quest_offer_regression.cs`

**Interfaces:**
- Consumes: `NpcQuestOfferDialog` submit payload with `quest_id` and optional `confirm_accept`
- Produces: quest accept result, refreshed NPC quest offer context, feedback text

- [x] **Step 1: Add submit route**

In `_dispatch_settlement_action`, after the open branch, add a submission branch:

```csharp
if (_is_npc_quest_offer_modal_submission(payload))
    return _submit_npc_quest_offer_action(settlement_id, action_id, payload);
```

Implement:

```csharp
private bool _is_npc_quest_offer_modal_submission(GDictionary payload) =>
    ReadString(payload, "submission_source") == "npc_quest_offer";

private GDictionary _submit_npc_quest_offer_action(
    string settlement_id,
    string action_id,
    GDictionary payload
)
{
    if (GetActiveModalKind() != RuntimeModalKind.NpcQuestOffer)
        return CommandError("当前没有打开 NPC 委托面板。");

    GDictionary npcContext = GetActiveNpcQuestOfferContext();
    if (ReadString(npcContext, "settlement_id").Trim() != settlement_id)
        return CommandError("当前 NPC 委托面板与请求的据点不一致。");

    StringName questId = ReadStringName(payload, "quest_id");
    if (questId == "")
        return CommandError("NPC 委托提交缺少 quest_id。");

    QuestDef questDef = GetQuestDefTyped(questId);
    if (questDef == null || questDef.provider_kind != "npc")
        return CommandError("该任务不是 NPC 委托。");

    if (questDef.provider_interaction_id != ReadString(npcContext, "npc_interaction_id"))
        return CommandError("该任务不属于当前 NPC。");

    if (!questDef.listing_channels.Contains("npc_offer"))
        return CommandError("该任务未配置为 NPC 委托。");

    QuestAcceptAvailabilityResult availability = _quest_accept_evaluator.Evaluate(
        questDef,
        _build_quest_accept_context()
    );

    if (!availability.CanAccept)
    {
        string feedback = !string.IsNullOrEmpty(questDef.accept_feedback_failure)
            ? questDef.accept_feedback_failure
            : $"不满足接取条件：{availability.DisabledReason}";
        _refresh_active_npc_quest_offer_context(feedback);
        return CommandError(feedback);
    }

    bool isConfirmationSubmission = ReadBool(payload, "confirm_accept", false);
    bool hasPendingConfirmation = ReadStringName(npcContext, "pending_confirmation_quest_id") == questId;

    if (!string.IsNullOrEmpty(questDef.accept_confirmation_text) && !isConfirmationSubmission && !hasPendingConfirmation)
    {
        _set_npc_quest_offer_confirmation_context(questId, questDef.accept_confirmation_text);
        return CommandOk("请确认是否接受该委托。");
    }

    if (hasPendingConfirmation)
        _clear_npc_quest_offer_confirmation_context();

    var commandResult = Runtime.CommandAcceptQuestTyped(questId, questDef.is_repeatable);
    if (!commandResult.success)
    {
        _refresh_active_npc_quest_offer_context(commandResult.message);
        return commandResult;
    }

    string successFeedback = !string.IsNullOrEmpty(questDef.accept_feedback_success)
        ? questDef.accept_feedback_success
        : $"已接受委托 {questDef.display_name}。";
    _refresh_active_npc_quest_offer_context(successFeedback);
    return CommandOk(successFeedback);
}
```

Add helper methods (mirror contract-board helpers):

```csharp
private void _refresh_active_npc_quest_offer_context(string feedback_text)
{
    GDictionary context = GetActiveNpcQuestOfferContext();
    if (context.Count == 0)
        return;
    context["feedback_text"] = feedback_text;
    SetActiveNpcQuestOfferContext(context);
}

private void _set_npc_quest_offer_confirmation_context(StringName questId, string confirmationText)
{
    GDictionary context = GetActiveNpcQuestOfferContext();
    context["pending_confirmation_quest_id"] = questId.ToString();
    context["pending_confirmation_text"] = confirmationText;
    context["pending_confirmation_source"] = "npc_quest_offer";
    SetActiveNpcQuestOfferContext(context);
}

private void _clear_npc_quest_offer_confirmation_context()
{
    GDictionary context = GetActiveNpcQuestOfferContext();
    context.Remove("pending_confirmation_quest_id");
    context.Remove("pending_confirmation_text");
    context.Remove("pending_confirmation_source");
    SetActiveNpcQuestOfferContext(context);
}
```

- [x] **Step 2: Build and run existing regressions**

```bash
dotnet build magic.csproj
godot --headless -s res://tests/world_map/runtime/run_game_runtime_settlement_command_handler_regression.cs
```

Expected: PASS.

- [x] **Step 3: Commit**

```bash
git add scripts/systems/game_runtime/GameRuntimeSettlementCommandHandler.cs
git commit -m "feat: add NPC quest offer submit handler with confirmation flow"
```

---

## Task 4: Create `NpcQuestOfferDialog` scene and script

**Files:**
- Create: `scripts/ui/NpcQuestOfferDialog.cs`
- Create: `scenes/ui/npc_quest_offer_dialog.tscn`
- Modify: `scenes/main/world_map.tscn` (add node instance)
- Test: `tests/world_map/runtime/run_npc_quest_offer_regression.cs`

**Interfaces:**
- Consumes: window data from `GameRuntimeFacade.GetNpcQuestOfferWindowData()`
- Produces: `action_requested` signal with `quest_id`, `confirm_accept`, `submission_source = "npc_quest_offer"`

- [x] **Step 1: Create the script**

`scripts/ui/NpcQuestOfferDialog.cs`:

```csharp
using Godot;

[GlobalClass]
public partial class NpcQuestOfferDialog : Control
{
    [Signal]
    public delegate void action_requestedEventHandler(
        string settlement_id,
        string action_id,
        GDictionary payload,
        string member_id,
        int quantity,
        string submission_source
    );

    [Signal]
    public delegate void closedEventHandler();

    private GDictionary _windowData = new();
    private bool _isShowingConfirmation = false;

    public void ShowDialog(GDictionary windowData)
    {
        _windowData = windowData ?? new GDictionary();
        _isShowingConfirmation = false;
        _refresh_ui();
        Show();
    }

    public void HideDialog()
    {
        Hide();
        _windowData.Clear();
        _isShowingConfirmation = false;
    }

    private void _refresh_ui()
    {
        // TODO: bind to actual UI nodes in the scene
        // For now, this method exists as the integration point.
    }

    private void _on_accept_button_pressed()
    {
        string selectedQuestId = ReadString(_windowData, "selected_quest_id");
        if (selectedQuestId == "")
            return;

        if (_isShowingConfirmation)
        {
            _emit_accept(selectedQuestId, true);
            return;
        }

        GDictionary selectedEntry = _find_entry(selectedQuestId);
        if (selectedEntry == null)
            return;

        string confirmationText = ReadString(selectedEntry, "accept_confirmation_text");
        if (!string.IsNullOrEmpty(confirmationText) &&
            ReadString(_windowData, "pending_confirmation_quest_id") != selectedQuestId)
        {
            _show_confirmation(confirmationText);
            return;
        }

        _emit_accept(selectedQuestId, false);
    }

    private void _emit_accept(string questId, bool confirmAccept)
    {
        GDictionary payload = new()
        {
            ["quest_id"] = questId,
            ["confirm_accept"] = confirmAccept,
            ["submission_source"] = "npc_quest_offer",
        };
        EmitSignal(SignalName.action_requested,
            ReadString(_windowData, "settlement_id"),
            "",
            payload,
            "",
            0,
            "npc_quest_offer"
        );
    }

    private void _on_return_button_pressed()
    {
        if (_isShowingConfirmation)
        {
            _hide_confirmation();
            return;
        }
        EmitSignal(SignalName.closed);
    }

    private void _show_confirmation(string text)
    {
        _isShowingConfirmation = true;
        // TODO: swap detail label to confirmation text
    }

    private void _hide_confirmation()
    {
        _isShowingConfirmation = false;
        // TODO: restore detail label
    }

    private GDictionary _find_entry(string questId)
    {
        foreach (Variant entry in GetArray(_windowData, "entries"))
        {
            if (entry.VariantType != Variant.Type.Dictionary)
                continue;
            GDictionary dict = (GDictionary)entry;
            if (ReadString(dict, "quest_id") == questId)
                return dict;
        }
        return null;
    }

    private static string ReadString(GDictionary dict, string key) =>
        dict.ContainsKey(key) ? dict[key].AsString() : "";

    private static Godot.Collections.Array GetArray(GDictionary dict, string key) =>
        dict.ContainsKey(key) ? dict[key].AsGodotArray() : new Godot.Collections.Array();
}
```

- [x] **Step 2: Create the scene**

`scenes/ui/npc_quest_offer_dialog.tscn`:

```ini
[gd_scene load_steps=2 format=3 uid="uid://npc_quest_offer_dialog"]

[ext_resource type="Script" path="res://scripts/ui/NpcQuestOfferDialog.cs" id="1_npcoffer"]

[node name="NpcQuestOfferDialog" type="Control" node_paths=PackedStringArray("shade", "title_label", "dialogue_label", "summary_label", "reward_label", "accept_button", "return_button")]
layout_mode = 3
anchors_preset = 15
anchor_right = 1.0
anchor_bottom = 1.0
grow_horizontal = 2
grow_vertical = 2
script = ExtResource("1_npcoffer")
shade = NodePath("Shade")
title_label = NodePath("Panel/Margin/VBox/TitleLabel")
dialogue_label = NodePath("Panel/Margin/VBox/DialogueLabel")
summary_label = NodePath("Panel/Margin/VBox/SummaryLabel")
reward_label = NodePath("Panel/Margin/VBox/RewardLabel")
accept_button = NodePath("Panel/Margin/VBox/ButtonRow/AcceptButton")
return_button = NodePath("Panel/Margin/VBox/ButtonRow/ReturnButton")

[node name="Shade" type="ColorRect" parent="."]
layout_mode = 1
anchors_preset = 15
anchor_right = 1.0
anchor_bottom = 1.0
color = Color(0, 0, 0, 0.5)

[node name="Panel" type="PanelContainer" parent="."]
layout_mode = 1
anchors_preset = 8
anchor_left = 0.5
anchor_top = 0.5
anchor_right = 0.5
anchor_bottom = 0.5
offset_left = -300.0
offset_top = -250.0
offset_right = 300.0
offset_bottom = 250.0
grow_horizontal = 2
grow_vertical = 2

[node name="Margin" type="MarginContainer" parent="Panel"]
layout_mode = 2
theme_override_constants/margin_left = 20
theme_override_constants/margin_top = 20
theme_override_constants/margin_right = 20
theme_override_constants/margin_bottom = 20

[node name="VBox" type="VBoxContainer" parent="Panel/Margin"]
layout_mode = 2
theme_override_constants/separation = 12

[node name="TitleLabel" type="Label" parent="Panel/Margin/VBox"]
layout_mode = 2
text = "NPC Name"
horizontal_alignment = 1

[node name="DialogueLabel" type="Label" parent="Panel/Margin/VBox"]
layout_mode = 2
text = "Dialogue text goes here."
autowrap_mode = 3

[node name="SummaryLabel" type="Label" parent="Panel/Margin/VBox"]
layout_mode = 2
text = "Objectives"

[node name="RewardLabel" type="Label" parent="Panel/Margin/VBox"]
layout_mode = 2
text = "Rewards"

[node name="ButtonRow" type="HBoxContainer" parent="Panel/Margin/VBox"]
layout_mode = 2
size_flags_vertical = 10
alignment = 1

[node name="AcceptButton" type="Button" parent="Panel/Margin/VBox/ButtonRow"]
layout_mode = 2
text = "接受委托"

[node name="ReturnButton" type="Button" parent="Panel/Margin/VBox/ButtonRow"]
layout_mode = 2
text = "返回"
```

**Note:** The script above declares exported node paths but does not include the `[Export]` fields for brevity. In the actual implementation, add:

```csharp
[Export] private ColorRect shade;
[Export] private Label title_label;
[Export] private Label dialogue_label;
[Export] private Label summary_label;
[Export] private Label reward_label;
[Export] private Button accept_button;
[Export] private Button return_button;
```

and bind `_refresh_ui` to update them.

- [x] **Step 3: Add instance to `world_map.tscn`**

Open `scenes/main/world_map.tscn` in Godot Editor or edit the text to add a child instance of `NpcQuestOfferDialog` next to `ContractBoardServiceModal`.

- [x] **Step 4: Build and run**

```bash
dotnet build magic.csproj
```

Expected: PASS.

- [x] **Step 5: Commit**

```bash
git add scripts/ui/NpcQuestOfferDialog.cs scenes/ui/npc_quest_offer_dialog.tscn scenes/main/world_map.tscn
git commit -m "feat: add NpcQuestOfferDialog scene and script"
```

---

## Task 5: Wire `NpcQuestOfferDialog` into `WorldMapSystem`

**Files:**
- Modify: `scripts/systems/game_runtime/WorldMapSystem.cs`
- Modify: `scenes/main/world_map.tscn`
- Test: `tests/world_map/runtime/run_npc_quest_offer_regression.cs`

**Interfaces:**
- Consumes: `RuntimeModalKind.NpcQuestOffer`, `WorldMapRuntimeProxy.GetNpcQuestOfferWindowData()`
- Produces: `action_requested` / `closed` signal handling

- [x] **Step 1: Add field and bind node**

In `WorldMapSystem.cs`:

```csharp
public NpcQuestOfferDialog npc_quest_offer_dialog;
```

In `BindNodes()`:

```csharp
npc_quest_offer_dialog = GetNode<NpcQuestOfferDialog>("NpcQuestOfferDialog");
```

In `ConnectSignals()`:

```csharp
npc_quest_offer_dialog.action_requested += _on_npc_quest_offer_dialog_action_requested;
npc_quest_offer_dialog.closed += _on_npc_quest_offer_dialog_closed;
```

In `DisconnectSignals()`:

```csharp
if (npc_quest_offer_dialog != null)
{
    npc_quest_offer_dialog.action_requested -= _on_npc_quest_offer_dialog_action_requested;
    npc_quest_offer_dialog.closed -= _on_npc_quest_offer_dialog_closed;
}
```

- [x] **Step 2: Add RenderWindows branch**

```csharp
if (modalId == "npc_quest_offer")
{
    npc_quest_offer_dialog.ShowDialog(_runtime_proxy.GetNpcQuestOfferWindowData());
    _settlement_window.HideWindow();
}
else
{
    npc_quest_offer_dialog.HideDialog();
}
```

- [x] **Step 3: Add signal handlers**

```csharp
public void _on_npc_quest_offer_dialog_action_requested(
    string settlement_id,
    string action_id,
    GDictionary payload,
    string member_id,
    int quantity,
    string submission_source
)
{
    if (_runtime != null)
    {
        _runtime_proxy.CommandExecuteSettlementAction(
            new SettlementActionRequest(
                new StringName(settlement_id ?? ""),
                new StringName(action_id ?? ""),
                new StringName(action_id ?? ""),
                new StringName(member_id ?? ""),
                quantity,
                submission_source ?? ""
            )
        );
    }
}

public void _on_npc_quest_offer_dialog_closed()
{
    if (_runtime != null)
        _runtime_proxy.CommandCloseActiveModal();
}
```

- [x] **Step 4: Add to finalizer/null-clear lists**

Add `npc_quest_offer_dialog` to `SuppressNodeFieldFinalizers()` and `ClearNodeRefs()` if those methods exist.

- [x] **Step 5: Build and run regressions**

```bash
dotnet build magic.csproj
godot --headless -s res://tests/world_map/runtime/run_game_runtime_settlement_command_handler_regression.cs
godot --headless -s res://tests/text_runtime/headless/run_headless_game_test_session_regression.cs
```

Expected: PASS.

- [x] **Step 6: Commit**

```bash
git add scripts/systems/game_runtime/WorldMapSystem.cs scenes/main/world_map.tscn
git commit -m "feat: wire NpcQuestOfferDialog into WorldMapSystem"
```

---

## Task 6: Add snapshot and text renderer support

**Files:**
- Modify: `scripts/systems/game_runtime/GameRuntimeSnapshotBuilder.cs`
- Modify: `scripts/utils/GameTextSnapshotRenderer.cs`
- Test: `tests/runtime/facade/run_game_runtime_snapshot_builder_regression.cs`
- Test: `tests/text_runtime/headless/run_headless_game_test_session_regression.cs`

**Interfaces:**
- Consumes: active NPC quest offer context
- Produces: `npc_quest_offer` snapshot + text lines

- [x] **Step 1: Add snapshot builder**

In `GameRuntimeSnapshotBuilder.cs`:

```csharp
private Dictionary BuildNpcQuestOfferSnapshot()
{
    var windowData = ResolveNpcQuestOfferWindowData();
    windowData.Remove("party_state");
    return new Dictionary
    {
        ["visible"] = _runtime.GetActiveModalKind() == RuntimeModalKind.NpcQuestOffer,
        ["window_data"] = RuntimePayloadCopy.Dictionary(windowData),
    };
}

private Dictionary ResolveNpcQuestOfferWindowData()
{
    var windowData = GetWindowDataFromRuntime("GetNpcQuestOfferWindowData");
    if (windowData.Count > 0)
        return windowData;
    return GetWindowDataFromRuntime("GetActiveNpcQuestOfferContext");
}
```

Add `["npc_quest_offer"] = BuildNpcQuestOfferSnapshot()` in `BuildHeadlessSnapshot`.

- [x] **Step 2: Add text renderer**

In `GameTextSnapshotRenderer.cs`:

```csharp
AppendSection(
    sections,
    "NPC_QUEST_OFFER",
    BuildNpcQuestOfferLines(GetDictionary(snapshot, "npc_quest_offer"))
);
```

```csharp
private static List<string> BuildNpcQuestOfferLines(GDictionary npcQuestOfferSnapshot)
{
    if (IsEmpty(npcQuestOfferSnapshot))
        return new List<string>();

    var windowData = GetDictionary(npcQuestOfferSnapshot, "window_data");
    var lines = new List<string>
    {
        $"visible={FormatBool(ReadExactBool(npcQuestOfferSnapshot, "visible"))}",
        $"npc_name={GetExactString(windowData, "npc_name")}",
        $"selected_quest_id={GetExactString(windowData, "selected_quest_id")}",
        $"feedback_text={GetExactString(windowData, "feedback_text")}",
    };

    foreach (GDictionary entry in GetArray(windowData, "entries"))
    {
        lines.Add($"entry={GetExactString(entry, "quest_id")}");
        lines.Add($"  display_name={GetExactString(entry, "display_name")}");
        lines.Add($"  is_enabled={FormatBool(ReadExactBool(entry, "is_enabled"))}");
        lines.Add($"  disabled_reason={GetExactString(entry, "disabled_reason")}");
        lines.Add($"  accept_dialogue_text={GetExactString(entry, "accept_dialogue_text")}");
    }

    return lines;
}
```

- [x] **Step 3: Update regressions if snapshots changed**

Run:

```bash
dotnet build magic.csproj
godot --headless -s res://tests/runtime/facade/run_game_runtime_snapshot_builder_regression.cs
godot --headless -s res://tests/text_runtime/headless/run_headless_game_test_session_regression.cs
```

Expected: PASS.

- [x] **Step 4: Commit**

```bash
git add scripts/systems/game_runtime/GameRuntimeSnapshotBuilder.cs scripts/utils/GameTextSnapshotRenderer.cs
git commit -m "feat: add NPC quest offer snapshot and text renderer output"
```

---

## Task 7: Add NPC quest `.tres` example

**Files:**
- Create: `data/configs/quests/npc_blacksmith_hrothgar_cave_beasts.tres`
- Test: `tests/world_map/runtime/run_npc_quest_offer_regression.cs`

**Interfaces:**
- Consumes: existing `QuestDef` schema
- Produces: a live NPC quest that can be offered by `npc_blacksmith_hrothgar`

- [x] **Step 1: Create the `.tres` file**

```ini
[gd_resource type="Resource" script_class="QuestDef" load_steps=2 format=3]

[ext_resource type="Script" path="res://scripts/player/progression/QuestDef.cs" id="1_questdef"]

[resource]
script = ExtResource("1_questdef")
quest_id = &"npc_blacksmith_hrothgar_cave_beasts"
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

- [x] **Step 2: Validate**

```bash
godot --headless -s res://tests/runtime/validation/run_quest_config_validation.cs
```

Expected: PASS.

- [x] **Step 3: Commit**

```bash
git add data/configs/quests/npc_blacksmith_hrothgar_cave_beasts.tres
git commit -m "feat: add example NPC quest for blacksmith Hrothgar"
```

---

## Task 8: Add headless NPC quest offer regression test

**Files:**
- Create: `tests/world_map/runtime/run_npc_quest_offer_regression.cs`

**Interfaces:**
- Consumes: `GameRuntimeFacade`, settlement action execution, NPC quest offer window data
- Produces: PASS/FAIL verdict

- [x] **Step 1: Create the test runner**

The test should:
1. Set up a `GameRuntimeFacade` / `GameSession` with the example NPC quest loaded.
2. Execute a settlement action with `interaction_script_id = "npc_blacksmith_hrothgar"`.
3. Assert active modal kind is `NpcQuestOffer` and window data contains the quest.
4. Submit accept for the quest and assert success feedback.
5. Assert quest is now active in `PartyState`.
6. Close modal and assert clean return.

Use the existing settlement command handler regression as a template for session setup.

- [x] **Step 2: Run the test**

```bash
dotnet build magic.csproj
godot --headless -s res://tests/world_map/runtime/run_npc_quest_offer_regression.cs
```

Expected: PASS.

- [x] **Step 3: Commit**

```bash
git add tests/world_map/runtime/run_npc_quest_offer_regression.cs
git commit -m "test: add NPC quest offer headless regression"
```

---

## Task 9: Update `project_context_units.md`

**Files:**
- Modify: `docs/design/project_context_units.md`

- [x] **Step 1: Update CU-06**

Add to CU-06 read set:
- `scripts/ui/NpcQuestOfferDialog.cs`
- `scenes/ui/npc_quest_offer_dialog.tscn`
- `scripts/systems/game_runtime/WorldMapSystem.cs` (NPC modal node binding / `RenderWindows`)

- [x] **Step 2: Commit**

```bash
git add docs/design/project_context_units.md
git commit -m "docs: update context units for NpcQuestOffer modal"
```

---

## Spec Coverage Check

| Spec Section | Implementing Task |
|---|---|
| §7.1 `RuntimeModalKind.NpcQuestOffer` | Task 1 |
| §7.2 NPC open flow | Task 2 |
| §7.3 NPC submit flow | Task 3 |
| §7.4 Multi-task展示 | Task 4 / Task 5 |
| §3.6 Confirmation state reused | Task 3 / Task 4 |
| §8.2 NPC config example | Task 7 |
| §11 Phase 2 tests | Task 8 |
| §12 Context units | Task 9 |

## Execution Handoff

Plan complete and archived at `docs/archive/implementation-plans/landed/2026-06-28-quest-accept-flow-phase2.md`.

**Two execution options:**

**1. Subagent-Driven (recommended)** - Dispatch a fresh subagent per task, review between tasks.

**2. Inline Execution** - Execute tasks in this session with checkpoints.

**Which approach?**
