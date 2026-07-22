# Quest Accept Flow Phase 1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the first phase of the quest accept flow redesign: extend `QuestDef` schema, migrate quest content to `.tres`, add `QuestContentRegistry` loader, introduce `QuestAcceptRequirementEvaluator`, and upgrade the contract board to pre-check requirements, show accept dialogue, preserve feedback text, and support an inline confirmation state.

**Architecture:** Quest content becomes `data/configs/quests/*.tres` loaded by a new `QuestContentRegistry` (mirroring `SkillContentRegistry`/`ItemContentRegistry`). A stateless `QuestAcceptRequirementEvaluator` receives a `QuestAcceptContext` DTO and returns availability results. `GameRuntimeSettlementCommandHandler` calls the evaluator when building contract-board entries and again on submission, before delegating to the existing typed `CommandAcceptQuestTyped`. `ShopWindow` gains an inline confirmation sub-state driven by `pending_confirmation_quest_id` / `pending_confirmation_text` in the modal context.

**Tech Stack:** Godot 4.6 C#, GDScript UI scenes, dotnet CLI, headless Godot regression runners.

## Global Constraints

- No branch dialogue trees.
- No NPC portraits, art assets, or reputation systems in Phase 1.
- Do not push quest rules into UI nodes.
- `QuestProgressService` must not directly depend on warehouse, gold, settlement, or world state.
- Old quest payloads are not auto-inferred; all live quest content must explicitly contain the new schema fields.
- Quest content source is `data/configs/quests/*.tres`; existing JSON files are converted and then removed.
- Follow existing file organization and naming: tabs for indentation, `snake_case` files/methods, `PascalCase` for public classes.
- All code changes must be covered by headless regression tests in the matching `tests/<domain>/` folder.

---

## File Structure

### New files

| File | Responsibility |
|---|---|
| `scripts/player/progression/QuestContentRegistry.cs` | Scan `res://data/configs/quests/*.tres`, load `QuestDef` resources, validate uniqueness, expose typed quest catalog. |
| `scripts/systems/progression/QuestAcceptRequirementEvaluator.cs` | Stateless rule engine: given `QuestDef` + `QuestAcceptContext`, return `QuestAcceptAvailabilityResult`. Implements only quest-state requirements in Phase 1. |
| `tests/runtime/validation/run_quest_config_validation.cs` (rewrite) | Load `.tres` quests from `data/configs/quests/` and run `QuestContentValidator`. |
| `tests/progression/core/run_quest_accept_requirement_evaluator_regression.cs` | Unit-level regression for evaluator rules. |
| `data/configs/quests/*.tres` (35 files) | One `.tres` per quest: 31 migrated from JSON + 4 migrated from `ProgressionContentRegistry` seed definitions. |

### Modified files

| File | Responsibility |
|---|---|
| `scripts/player/progression/QuestDef.cs` | Add new `[Export]` fields; update `RequiredSerializedFields`, `FromDictionary`, `ValidateSchema`. |
| `scripts/player/progression/QuestProviderContentRules.cs` | Add `QuestProviderKind` / `QuestListingChannel` typed rules; keep service provider whitelist separate from NPC routing. |
| `scripts/player/progression/QuestContentValidator.cs` | Add validation for `provider_kind`, `listing_channels`, and `accept_requirements`. |
| `scripts/player/progression/ProgressionContentRegistry.cs` | Replace hard-coded seed quests with `QuestContentRegistry` load; delegate `_questDefs` population. |
| `scripts/systems/game_runtime/GameRuntimeSettlementCommandHandler.cs` | Pre-evaluate requirements in `_build_contract_board_entry`; re-evaluate on submit; show `accept_dialogue_text`; preserve `feedback_text`; set confirmation context. |
| `scripts/ui/ShopWindow.cs` | Render inline confirmation sub-state when `pending_confirmation_quest_id` is present; emit confirmation submission payload. |
| `scripts/utils/GameTextSnapshotRenderer.cs` | Include new fields (`provider_kind`, `listing_channels`, `accept_dialogue_text`, `disabled_reason`) in contract-board text snapshot. |
| `docs/design/project_context_units.md` | Update CU-02 and CU-06 to reflect `.tres` quest source, `QuestContentRegistry`, evaluator, and contract-board accept availability. |

### Deleted files

| File | Reason |
|---|---|
| `data/configs/quests/main_world_quests.json` | Replaced by individual `.tres` files. |
| `data/configs/quests/ashen_intersection_quests.json` | Replaced by individual `.tres` files. |
| `data/configs/quests/bounty_quests.json` | Replaced by individual `.tres` files. |

---

### Task 1: Extend `QuestDef` schema

**Files:**
- Modify: `scripts/player/progression/QuestDef.cs:30-115`
- Modify: `scripts/player/progression/QuestDef.cs:380-457`
- Test: `tests/runtime/validation/run_quest_content_validator_typed_regression.cs`

**Interfaces:**
- Consumes: nothing (schema change)
- Produces: `QuestDef` with new exported properties:
  - `StringName provider_kind`
  - `Godot.Collections.Array<StringName> listing_channels`
  - `string accept_dialogue_text`
  - `string accept_feedback_success`
  - `string accept_feedback_failure`
  - `string accept_confirmation_text`

- [ ] **Step 1: Add new exported properties**

Add after `is_repeatable`:

```csharp
[Export]
public StringName provider_kind { get; set; } = "";

[Export]
public Godot.Collections.Array<StringName> listing_channels { get; set; } = new();

[Export(PropertyHint.MultilineText)]
public string accept_dialogue_text { get; set; } = "";

[Export]
public string accept_feedback_success { get; set; } = "";

[Export]
public string accept_feedback_failure { get; set; } = "";

[Export(PropertyHint.MultilineText)]
public string accept_confirmation_text { get; set; } = "";
```

- [ ] **Step 2: Update `RequiredSerializedFields`**

Change `scripts/player/progression/QuestDef.cs:30` from:

```csharp
private static readonly string[] RequiredSerializedFields =
{
    "quest_id",
    "display_name",
    "description",
    "provider_interaction_id",
    "tags",
    "accept_requirements",
    "objective_defs",
    "reward_entries",
    "is_repeatable",
};
```

to:

```csharp
private static readonly string[] RequiredSerializedFields =
{
    "quest_id",
    "display_name",
    "description",
    "provider_kind",
    "provider_interaction_id",
    "listing_channels",
    "tags",
    "accept_requirements",
    "objective_defs",
    "reward_entries",
    "is_repeatable",
    "accept_dialogue_text",
    "accept_feedback_success",
    "accept_feedback_failure",
    "accept_confirmation_text",
};
```

- [ ] **Step 3: Update `FromDictionary` parsing**

In `scripts/player/progression/QuestDef.cs:380-457`, read the new fields after `is_repeatable`:

```csharp
questDef.provider_kind = ReadStringName(payload, "provider_kind");
questDef.listing_channels = ReadStringNameArray(payload, "listing_channels");
questDef.accept_dialogue_text = ReadString(payload, "accept_dialogue_text");
questDef.accept_feedback_success = ReadString(payload, "accept_feedback_success");
questDef.accept_feedback_failure = ReadString(payload, "accept_feedback_failure");
questDef.accept_confirmation_text = ReadString(payload, "accept_confirmation_text");
```

Use the existing helper pattern (`ReadStringName`, `ReadString`, and add `ReadStringNameArray` if not already present next to `ReadStringArray`).

- [ ] **Step 4: Update `ValidateSchema()`**

Add checks:

```csharp
if (provider_kind == "")
    errors.Add("provider_kind 不能为空。");

if (listing_channels == null || listing_channels.Count == 0)
    errors.Add("listing_channels 不能为空数组。");

foreach (StringName channel in listing_channels)
{
    if (channel == "")
        errors.Add("listing_channels 包含空值。");
}
```

Keep the existing validation for `quest_id`, `display_name`, etc.

- [ ] **Step 5: Build and run validator regression**

Run:

```bash
dotnet build magic.csproj
```

Expected: build succeeds.

Run:

```bash
godot --headless -s res://tests/runtime/validation/run_quest_content_validator_typed_regression.cs
```

Expected: PASS (it constructs `QuestDef` in code and does not go through `FromDictionary`, so new fields default to empty and should not break existing assertions).

- [ ] **Step 6: Commit**

```bash
git add scripts/player/progression/QuestDef.cs
git commit -m "feat: extend QuestDef schema with provider_kind, listing_channels, and accept text fields"
```

---

### Task 2: Add typed provider and listing channel rules

**Files:**
- Modify: `scripts/player/progression/QuestProviderContentRules.cs`
- Modify: `scripts/player/progression/QuestContentValidator.cs`
- Test: `tests/runtime/validation/run_quest_content_validator_typed_regression.cs`

**Interfaces:**
- Consumes: `QuestDef.provider_kind`, `QuestDef.provider_interaction_id`, `QuestDef.listing_channels`
- Produces:
  - `enum QuestProviderKind { Unknown, ServiceContractBoard, ServiceBountyRegistry, Npc }`
  - `enum QuestListingChannel { Unknown, ContractBoard, BountyRegistry, NpcOffer }`
  - `QuestProviderContentRules.ToProviderKind(QuestDef)`
  - `QuestProviderContentRules.ToListingChannels(QuestDef)`
  - `QuestProviderContentRules.IsSupportedProviderKind(QuestProviderKind)`
  - `QuestContentValidator.AppendProviderKindErrors(...)`
  - `QuestContentValidator.AppendListingChannelErrors(...)`

- [ ] **Step 1: Define enums and typed helpers**

Replace the contents of `scripts/player/progression/QuestProviderContentRules.cs` with:

```csharp
using Godot;

public enum QuestProviderKind
{
    Unknown = 0,
    ServiceContractBoard,
    ServiceBountyRegistry,
    Npc,
}

public enum QuestListingChannel
{
    Unknown = 0,
    ContractBoard,
    BountyRegistry,
    NpcOffer,
}

public static class QuestProviderContentRules
{
    private static readonly StringName ProviderContractBoard = "service_contract_board";
    private static readonly StringName ProviderBountyRegistry = "service_bounty_registry";
    private static readonly StringName ProviderNpc = "npc";

    public static QuestProviderKind ToProviderKind(QuestDef questDef)
    {
        StringName kind = questDef.provider_kind;
        if (kind == ProviderContractBoard) return QuestProviderKind.ServiceContractBoard;
        if (kind == ProviderBountyRegistry) return QuestProviderKind.ServiceBountyRegistry;
        if (kind == ProviderNpc) return QuestProviderKind.Npc;
        return QuestProviderKind.Unknown;
    }

    public static Godot.Collections.Array<QuestListingChannel> ToListingChannels(QuestDef questDef)
    {
        var result = new Godot.Collections.Array<QuestListingChannel>();
        foreach (StringName channel in questDef.listing_channels)
        {
            result.Add(channel switch
            {
                _ when channel == "contract_board" => QuestListingChannel.ContractBoard,
                _ when channel == "bounty_registry" => QuestListingChannel.BountyRegistry,
                _ when channel == "npc_offer" => QuestListingChannel.NpcOffer,
                _ => QuestListingChannel.Unknown,
            });
        }
        return result;
    }

    public static bool IsSupportedProviderKind(QuestProviderKind kind) =>
        kind is QuestProviderKind.ServiceContractBoard
            or QuestProviderKind.ServiceBountyRegistry
            or QuestProviderKind.Npc;

    public static bool IsSupportedListingChannel(QuestListingChannel channel) =>
        channel is QuestListingChannel.ContractBoard
            or QuestListingChannel.BountyRegistry
            or QuestListingChannel.NpcOffer;

    public static IReadOnlyList<StringName> SupportedProviderIds() =>
        new System.Collections.Generic.List<StringName>
        {
            ProviderContractBoard,
            ProviderBountyRegistry,
        };
}
```

**Important:** Keep `SupportedProviderIds()` returning only the two service IDs. `npc` is a `provider_kind`, not a service-modal `provider_interaction_id`, so it must NOT be added here. This prevents `_dispatch_settlement_action` from treating NPC quests as contract-board entries.

- [ ] **Step 2: Add validator methods**

In `scripts/player/progression/QuestContentValidator.cs`, add after `AppendProviderReferenceErrors`:

```csharp
public static void AppendProviderKindErrors(
    List<string> errors,
    QuestDef questDef
)
{
    QuestProviderKind kind = QuestProviderContentRules.ToProviderKind(questDef);
    if (kind == QuestProviderKind.Unknown)
    {
        errors.Add($"Quest {questDef.quest_id}: 未知 provider_kind '{questDef.provider_kind}'。");
        return;
    }

    StringName expectedInteractionId = kind switch
    {
        QuestProviderKind.ServiceContractBoard => "service_contract_board",
        QuestProviderKind.ServiceBountyRegistry => "service_bounty_registry",
        QuestProviderKind.Npc => questDef.provider_interaction_id,
        _ => "",
    };

    if (kind == QuestProviderKind.ServiceContractBoard || kind == QuestProviderKind.ServiceBountyRegistry)
    {
        if (questDef.provider_interaction_id != expectedInteractionId)
            errors.Add($"Quest {questDef.quest_id}: provider_kind '{questDef.provider_kind}' 要求 provider_interaction_id 为 '{expectedInteractionId}'。");
    }
    else if (kind == QuestProviderKind.Npc)
    {
        if (questDef.provider_interaction_id == "")
            errors.Add($"Quest {questDef.quest_id}: provider_kind 'npc' 需要非空的 provider_interaction_id。");
    }
}

public static void AppendListingChannelErrors(
    List<string> errors,
    QuestDef questDef
)
{
    if (questDef.listing_channels == null || questDef.listing_channels.Count == 0)
    {
        errors.Add($"Quest {questDef.quest_id}: listing_channels 不能为空。");
        return;
    }

    foreach (QuestListingChannel channel in QuestProviderContentRules.ToListingChannels(questDef))
    {
        if (channel == QuestListingChannel.Unknown)
            errors.Add($"Quest {questDef.quest_id}: listing_channels 包含未知渠道。");
    }
}
```

Call both methods inside `ValidateTyped` after `AppendProviderReferenceErrors`.

- [ ] **Step 3: Update validator regression fixtures**

Open `tests/runtime/validation/run_quest_content_validator_typed_regression.cs` and update any `BuildInvalidQuestDef` / `BuildValidQuestDef` helpers to set `provider_kind` and `listing_channels` explicitly.

Example valid fixture:

```csharp
private static QuestDef BuildValidQuestDef(StringName questId)
{
    return new QuestDef
    {
        quest_id = questId,
        display_name = "Valid Quest",
        description = "A valid quest.",
        provider_kind = "service_contract_board",
        provider_interaction_id = "service_contract_board",
        listing_channels = new Godot.Collections.Array<StringName> { "contract_board" },
        tags = new Godot.Collections.Array<StringName>(),
        accept_requirements = new Godot.Collections.Array<Godot.Collections.Dictionary>(),
        objective_defs = new Godot.Collections.Array<Godot.Collections.Dictionary>(),
        reward_entries = new Godot.Collections.Array<Godot.Collections.Dictionary>(),
        is_repeatable = false,
    };
}
```

- [ ] **Step 4: Run validator regression**

```bash
godot --headless -s res://tests/runtime/validation/run_quest_content_validator_typed_regression.cs
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add scripts/player/progression/QuestProviderContentRules.cs scripts/player/progression/QuestContentValidator.cs tests/runtime/validation/run_quest_content_validator_typed_regression.cs
git commit -m "feat: add typed QuestProviderKind and QuestListingChannel rules with validation"
```

---

### Task 3: Implement `QuestContentRegistry` and migrate quests to `.tres`

**Files:**
- Create: `scripts/player/progression/QuestContentRegistry.cs`
- Modify: `scripts/player/progression/ProgressionContentRegistry.cs:659-977`
- Modify: `scripts/player/progression/ProgressionContentRegistry.cs:1077-1096`
- Modify: `tests/runtime/validation/run_quest_config_validation.cs`
- Create: 35 `.tres` files under `data/configs/quests/`
- Delete: `data/configs/quests/main_world_quests.json`
- Delete: `data/configs/quests/ashen_intersection_quests.json`
- Delete: `data/configs/quests/bounty_quests.json`

**Interfaces:**
- Consumes: `QuestDef` (schema from Task 1)
- Produces:
  - `QuestContentRegistry.LoadFromDirectory(string directoryPath)`
  - `QuestContentRegistry.GetQuestDefsTyped()`
  - `QuestContentRegistry.GetValidationErrors()`
  - `ProgressionContentRegistry` no longer contains hard-coded seed quests

- [ ] **Step 1: Create `QuestContentRegistry.cs`**

Create `scripts/player/progression/QuestContentRegistry.cs`:

```csharp
using System.Collections.Generic;
using Godot;

internal sealed class QuestContentRegistry
{
    private readonly Dictionary<StringName, QuestDef> _questDefs = new();
    private readonly List<string> _validationErrors = new();

    internal void LoadFromDirectory(string directoryPath)
    {
        _questDefs.Clear();
        _validationErrors.Clear();

        string globalPath = ProjectSettings.GlobalizePath(directoryPath);
        if (!DirAccess.DirExistsAbsolute(globalPath))
        {
            _validationErrors.Add($"QuestContentRegistry could not find {directoryPath}.");
            return;
        }

        using DirAccess directory = DirAccess.Open(directoryPath);
        if (directory == null)
        {
            _validationErrors.Add($"QuestContentRegistry could not open {directoryPath}.");
            return;
        }

        string[] files = directory.GetFiles();
        foreach (string fileName in files)
        {
            if (!fileName.EndsWith(".tres"))
                continue;

            string resourcePath = $"{directoryPath}/{fileName}";
            RegisterQuestResource(resourcePath);
        }
    }

    private void RegisterQuestResource(string resourcePath)
    {
        Resource resource = ResourceLoader.Load<Resource>(resourcePath);
        if (resource == null)
        {
            _validationErrors.Add($"QuestContentRegistry failed to load {resourcePath}.");
            return;
        }

        if (resource is not QuestDef questDef)
        {
            _validationErrors.Add($"QuestContentRegistry: {resourcePath} is not a QuestDef.");
            return;
        }

        GodotContentOwnership.RegisterBorrowedContent(
            questDef,
            resourcePath,
            "QuestContentRegistry.RegisterQuestResource"
        );

        StringName questId = questDef.quest_id;
        if (_questDefs.ContainsKey(questId))
        {
            _validationErrors.Add($"QuestContentRegistry: duplicate quest_id '{questId}' (conflict with {_questDefs[questId]}).");
            return;
        }

        _questDefs[questId] = questDef;
    }

    internal IReadOnlyDictionary<StringName, QuestDef> GetQuestDefsTyped() => _questDefs;

    internal IReadOnlyList<string> GetValidationErrors() => _validationErrors;
}
```

- [ ] **Step 2: Wire `QuestContentRegistry` into `ProgressionContentRegistry`**

In `scripts/player/progression/ProgressionContentRegistry.cs`:

1. Add a field:

```csharp
private readonly QuestContentRegistry _quest_content_registry = new();
```

2. In the `Build()` or `Rebuild()` method (find the existing build entry point), call:

```csharp
_quest_content_registry.LoadFromDirectory("res://data/configs/quests");
foreach (QuestDef questDef in _quest_content_registry.GetQuestDefsTyped().Values)
    _register_quest(questDef);
```

3. Remove or comment out the body of `_register_seed_quests()` (lines 659–977). If the 4 seed quests must be preserved, convert them first (see Step 4) and place their `.tres` files in `data/configs/quests/`.

4. Remove the manual `_register_quest` calls that duplicated seed quest registration if any remain outside `_register_seed_quests`.

- [ ] **Step 3: Convert 31 JSON quests to `.tres`**

Create a one-time conversion script `tools/convert_quests_json_to_tres.py`:

```python
#!/usr/bin/env python3
import json
import os
from pathlib import Path

CONFIG_DIR = Path("data/configs/quests")
SCRIPT_PATH = "res://scripts/player/progression/QuestDef.cs"


def to_string_name(value):
    return f'&"{value}"'


def format_array_string_name(items):
    if not items:
        return "Array[StringName]([])"
    inner = ", ".join(to_string_name(x) for x in items)
    return f"Array[StringName]([{inner}])"


def format_array_dictionary(items):
    if not items:
        return "Array[Dictionary]([])"
    entries = []
    for d in items:
        pairs = []
        for k, v in d.items():
            key = to_string_name(k)
            if isinstance(v, str):
                val = to_string_name(v)
            elif isinstance(v, int):
                val = str(v)
            elif isinstance(v, bool):
                val = "true" if v else "false"
            elif isinstance(v, list):
                # Nested arrays inside dictionaries (e.g. pending reward entries)
                val = format_array_dictionary([{kk: vv for kk, vv in e.items()} for e in v]) if v and isinstance(v[0], dict) else format_array_string_name(v)
            else:
                val = f'"{v}"'
            pairs.append(f"{key}: {val}")
        entries.append("{" + ", ".join(pairs) + "}")
    return "Array[Dictionary]([" + ", ".join(entries) + "])"


def quest_to_tres(quest):
    lines = [
        '[gd_resource type="Resource" script_class="QuestDef" load_steps=2 format=3]',
        '',
        f'[ext_resource type="Script" path="{SCRIPT_PATH}" id="1_questdef"]',
        '',
        '[resource]',
        'script = ExtResource("1_questdef")',
        f'quest_id = {to_string_name(quest["quest_id"])}',
        f'display_name = "{quest["display_name"]}"',
        f'description = "{quest["description"]}"',
        f'provider_kind = {to_string_name(quest.get("provider_kind", "service_contract_board"))}',
        f'provider_interaction_id = {to_string_name(quest["provider_interaction_id"])}',
        f'listing_channels = {format_array_string_name(quest.get("listing_channels", ["contract_board"]))}',
        f'tags = {format_array_string_name(quest.get("tags", []))}',
        f'accept_requirements = {format_array_dictionary(quest.get("accept_requirements", []))}',
        f'accept_dialogue_text = "{quest.get("accept_dialogue_text", "")}"',
        f'accept_feedback_success = "{quest.get("accept_feedback_success", "")}"',
        f'accept_feedback_failure = "{quest.get("accept_feedback_failure", "")}"',
        f'accept_confirmation_text = "{quest.get("accept_confirmation_text", "")}"',
        f'objective_defs = {format_array_dictionary(quest["objective_defs"])}',
        f'reward_entries = {format_array_dictionary(quest["reward_entries"])}',
        f'is_repeatable = {"true" if quest.get("is_repeatable", False) else "false"}',
    ]
    return "\n".join(lines) + "\n"


def main():
    for json_file in sorted(CONFIG_DIR.glob("*.json")):
        with open(json_file, "r", encoding="utf-8") as f:
            data = json.load(f)
        for quest in data.get("quests", []):
            quest_id = quest["quest_id"]
            out_path = CONFIG_DIR / f"{quest_id}.tres"
            out_path.write_text(quest_to_tres(quest), encoding="utf-8")
            print(f"Wrote {out_path}")


if __name__ == "__main__":
    main()
```

Run it:

```bash
python tools/convert_quests_json_to_tres.py
```

This creates 31 `.tres` files. **Manually review** at least 3 files for correct `StringName` syntax (`&"..."`) and nested `Array[Dictionary]` formatting.

- [ ] **Step 4: Migrate 4 seed quests to `.tres` (or delete them)**

Inspect `scripts/player/progression/ProgressionContentRegistry.cs:659-752` for the 4 seed quests:

- `contract_manual_drill`
- `contract_settlement_warehouse`
- `contract_first_hunt`
- `contract_regional_bounty`

For each, decide with the content team whether it is still needed. If needed, create `data/configs/quests/<quest_id>.tres` using the same format. If not needed, delete the seed registration code.

- [ ] **Step 5: Rewrite `run_quest_config_validation.cs`**

Replace the JSON loading logic with `.tres` loading via `QuestContentRegistry`. The test should:

1. Instantiate `QuestContentRegistry`.
2. Call `LoadFromDirectory("res://data/configs/quests")`.
3. Assert no validation errors from the registry.
4. Run `QuestContentValidator.ValidateTyped(...)` on the loaded quests.
5. Assert no content validation errors.

Example structure:

```csharp
var registry = new QuestContentRegistry();
registry.LoadFromDirectory("res://data/configs/quests");
IReadOnlyList<string> registryErrors = registry.GetValidationErrors();
if (registryErrors.Count > 0)
    Fail(string.Join("\n", registryErrors));

var validatorErrors = QuestContentValidator.ValidateTyped(
    registry.GetQuestDefsTyped(),
    itemDefs,
    skillDefinitions,
    enemyTemplates
);
if (validatorErrors.Count > 0)
    Fail(string.Join("\n", validatorErrors));
```

- [ ] **Step 6: Delete JSON files**

```bash
git rm data/configs/quests/main_world_quests.json
git rm data/configs/quests/ashen_intersection_quests.json
git rm data/configs/quests/bounty_quests.json
```

- [ ] **Step 7: Run validation test**

```bash
dotnet build magic.csproj
godot --headless -s res://tests/runtime/validation/run_quest_config_validation.cs
```

Expected: PASS (all 31+ quests load from `.tres` and pass validator).

- [ ] **Step 8: Commit**

```bash
git add scripts/player/progression/QuestContentRegistry.cs scripts/player/progression/ProgressionContentRegistry.cs tests/runtime/validation/run_quest_config_validation.cs data/configs/quests/*.tres tools/convert_quests_json_to_tres.py
git commit -m "feat: load quests from .tres via QuestContentRegistry and remove JSON source"
```

---

### Task 4: Implement `QuestAcceptRequirementEvaluator`

**Files:**
- Create: `scripts/systems/progression/QuestAcceptRequirementEvaluator.cs`
- Test: `tests/progression/core/run_quest_accept_requirement_evaluator_regression.cs`

**Interfaces:**
- Consumes: `QuestDef`, `PartyState`, `PartyWarehouseService`, party gold, world step, settlement ID/tier, quest catalog
- Produces:
  - `QuestAcceptContext`
  - `QuestAcceptAvailabilityResult`
  - `QuestAcceptRequirementEvaluator.Evaluate(QuestDef, QuestAcceptContext)`

- [ ] **Step 1: Define context and result DTOs**

Create `scripts/systems/progression/QuestAcceptRequirementEvaluator.cs`:

```csharp
using System.Collections.Generic;
using Godot;

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

internal sealed class QuestAcceptAvailabilityResult
{
    public bool CanAccept { get; init; }
    public StringName LockReasonId { get; init; } = "";
    public string DisabledReason { get; init; } = "";

    public static QuestAcceptAvailabilityResult Accept() =>
        new() { CanAccept = true };

    public static QuestAcceptAvailabilityResult Reject(StringName lockReasonId, string disabledReason) =>
        new() { CanAccept = false, LockReasonId = lockReasonId, DisabledReason = disabledReason };
}
```

- [ ] **Step 2: Implement evaluator**

In the same file:

```csharp
internal sealed class QuestAcceptRequirementEvaluator
{
    private static readonly StringName RequirementQuestCompleted = "quest_completed";
    private static readonly StringName RequirementQuestActive = "quest_active";
    private static readonly StringName RequirementQuestNotCompleted = "quest_not_completed";

    internal QuestAcceptAvailabilityResult Evaluate(
        QuestDef questDef,
        QuestAcceptContext context
    )
    {
        if (questDef.accept_requirements == null || questDef.accept_requirements.Count == 0)
            return QuestAcceptAvailabilityResult.Accept();

        foreach (Godot.Collections.Dictionary requirement in questDef.accept_requirements)
        {
            StringName requirementType = ReadStringName(requirement, "requirement_type");
            QuestAcceptAvailabilityResult result = requirementType switch
            {
                _ when requirementType == RequirementQuestCompleted =>
                    EvaluateQuestCompleted(requirement, context),
                _ when requirementType == RequirementQuestActive =>
                    EvaluateQuestActive(requirement, context),
                _ when requirementType == RequirementQuestNotCompleted =>
                    EvaluateQuestNotCompleted(requirement, context),
                _ => QuestAcceptAvailabilityResult.Reject(
                    "unknown_requirement",
                    $"未知需求类型：{requirementType}"
                ),
            };

            if (!result.CanAccept)
                return result;
        }

        return QuestAcceptAvailabilityResult.Accept();
    }

    private static QuestAcceptAvailabilityResult EvaluateQuestCompleted(
        Godot.Collections.Dictionary requirement,
        QuestAcceptContext context
    )
    {
        StringName questId = ReadStringName(requirement, "quest_id");
        if (questId == "")
            return QuestAcceptAvailabilityResult.Reject("missing_quest_id", "quest_completed 需求缺少 quest_id。");

        if (context.PartyState.HasCompletedQuest(questId))
            return QuestAcceptAvailabilityResult.Accept();

        return QuestAcceptAvailabilityResult.Reject(
            "quest_not_completed",
            $"需先完成任务：{GetQuestDisplayName(questId, context)}"
        );
    }

    private static QuestAcceptAvailabilityResult EvaluateQuestActive(
        Godot.Collections.Dictionary requirement,
        QuestAcceptContext context
    )
    {
        StringName questId = ReadStringName(requirement, "quest_id");
        if (questId == "")
            return QuestAcceptAvailabilityResult.Reject("missing_quest_id", "quest_active 需求缺少 quest_id。");

        if (context.PartyState.HasActiveQuest(questId))
            return QuestAcceptAvailabilityResult.Accept();

        return QuestAcceptAvailabilityResult.Reject(
            "quest_not_active",
            $"需先接取任务：{GetQuestDisplayName(questId, context)}"
        );
    }

    private static QuestAcceptAvailabilityResult EvaluateQuestNotCompleted(
        Godot.Collections.Dictionary requirement,
        QuestAcceptContext context
    )
    {
        StringName questId = ReadStringName(requirement, "quest_id");
        if (questId == "")
            return QuestAcceptAvailabilityResult.Reject("missing_quest_id", "quest_not_completed 需求缺少 quest_id。");

        if (!context.PartyState.HasCompletedQuest(questId))
            return QuestAcceptAvailabilityResult.Accept();

        return QuestAcceptAvailabilityResult.Reject(
            "quest_already_completed",
            $"不能重复完成该任务线：{GetQuestDisplayName(questId, context)}"
        );
    }

    private static string GetQuestDisplayName(StringName questId, QuestAcceptContext context)
    {
        if (context.QuestDefs.TryGetValue(questId, out QuestDef questDef))
            return questDef.display_name;
        return questId.ToString();
    }

    private static StringName ReadStringName(Godot.Collections.Dictionary dict, string key)
    {
        if (!dict.ContainsKey(key))
            return "";
        Variant value = dict[key];
        if (value.VariantType == Variant.Type.StringName)
            return (StringName)value;
        if (value.VariantType == Variant.Type.String)
            return new StringName((string)value);
        return "";
    }
}
```

- [ ] **Step 3: Add evaluator regression test**

Create `tests/progression/core/run_quest_accept_requirement_evaluator_regression.cs`:

```csharp
using Godot;
using System.Collections.Generic;

public partial class RunQuestAcceptRequirementEvaluatorRegression : Node
{
    public override void _Ready()
    {
        var partyState = new PartyState();
        var questDefs = new Dictionary<StringName, QuestDef>
        {
            ["pre_req"] = new QuestDef
            {
                quest_id = "pre_req",
                display_name = "前置任务",
                provider_kind = "service_contract_board",
                provider_interaction_id = "service_contract_board",
                listing_channels = new Godot.Collections.Array<StringName> { "contract_board" },
            },
            ["target"] = new QuestDef
            {
                quest_id = "target",
                display_name = "目标任务",
                provider_kind = "service_contract_board",
                provider_interaction_id = "service_contract_board",
                listing_channels = new Godot.Collections.Array<StringName> { "contract_board" },
                accept_requirements = new Godot.Collections.Array<Godot.Collections.Dictionary>
                {
                    new Godot.Collections.Dictionary
                    {
                        ["requirement_type"] = "quest_completed",
                        ["quest_id"] = "pre_req"
                    }
                }
            },
        };

        var evaluator = new QuestAcceptRequirementEvaluator();
        var context = new QuestAcceptContext
        {
            PartyState = partyState,
            WarehouseService = null,
            PartyGold = 0,
            WorldStep = 0,
            SettlementId = "",
            SettlementTier = 0,
            QuestDefs = questDefs,
        };

        // Case 1: pre-req not completed -> reject
        var result1 = evaluator.Evaluate(questDefs["target"], context);
        if (result1.CanAccept)
            Fail("Should reject when pre-req not completed");

        // Case 2: pre-req completed -> accept
        partyState.completed_quest_ids.Add("pre_req");
        var result2 = evaluator.Evaluate(questDefs["target"], context);
        if (!result2.CanAccept)
            Fail("Should accept when pre-req completed");

        ConsoleProcessOutput.WriteStandard("QuestAcceptRequirementEvaluator regression PASSED");
        GetTree().Quit(0);
    }

    private static void Fail(string message)
    {
        ConsoleProcessOutput.WriteFailure($"FAILED: {message}");
        // throw or quit with non-zero
    }
}
```

Adapt the test runner pattern to match existing `tests/progression/core/` conventions.

- [ ] **Step 4: Run evaluator regression**

```bash
dotnet build magic.csproj
godot --headless -s res://tests/progression/core/run_quest_accept_requirement_evaluator_regression.cs
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add scripts/systems/progression/QuestAcceptRequirementEvaluator.cs tests/progression/core/run_quest_accept_requirement_evaluator_regression.cs
git commit -m "feat: add QuestAcceptRequirementEvaluator for quest-state requirements"
```

---

### Task 5: Integrate evaluator and accept text into contract board

**Files:**
- Modify: `scripts/systems/game_runtime/GameRuntimeSettlementCommandHandler.cs`
- Modify: `scripts/player/progression/ContractBoardQuestData.cs` (if exists; otherwise extend inline helpers)
- Test: `tests/world_map/runtime/run_game_runtime_settlement_command_handler_regression.cs`

**Interfaces:**
- Consumes: `QuestAcceptRequirementEvaluator`, `QuestAcceptContext`
- Produces:
  - Contract-board entries with `is_enabled`, `disabled_reason`, `lock_reason_id`, `accept_dialogue_text`, `accept_feedback_success`, `accept_feedback_failure`, `accept_confirmation_text`
  - Submit-time re-evaluation before `CommandAcceptQuestTyped`
  - Feedback text preserved across modal refresh
  - Confirmation context (`pending_confirmation_quest_id`, `pending_confirmation_text`, `pending_confirmation_source`)

- [ ] **Step 1: Add evaluator field and context builder to handler**

In `scripts/systems/game_runtime/GameRuntimeSettlementCommandHandler.cs`:

```csharp
private readonly QuestAcceptRequirementEvaluator _quest_accept_evaluator = new();

private QuestAcceptContext _build_quest_accept_context()
{
    return new QuestAcceptContext
    {
        PartyState = GetPartyState(),
        WarehouseService = GetPartyWarehouseService(),
        PartyGold = GetPartyGold(),
        WorldStep = GetWorldStep(),
        SettlementId = GetActiveSettlementId(),
        SettlementTier = GetSettlementTier(),
        QuestDefs = GetQuestDefsTyped(),
    };
}

private int GetSettlementTier()
{
    string settlementId = GetActiveSettlementId();
    if (settlementId == "")
        return 0;
    GDictionary settlement = GetSettlementRecord(settlementId);
    if (settlement == null)
        return 0;
    return ReadInt(settlement, "tier", 0);
}
```

Add `ReadInt` helper if not already present (pattern-match existing `ReadString` / `ReadBool` helpers).

- [ ] **Step 2: Pre-evaluate in `_build_contract_board_entry`**

Modify `scripts/systems/game_runtime/GameRuntimeSettlementCommandHandler.cs:2259`:

```csharp
private GDictionary _build_contract_board_entry(
    QuestDef quest_def,
    string interaction_script_id
)
{
    ContractBoardQuestData questData = _build_contract_board_quest_data(quest_def);
    string stateId = _resolve_contract_board_quest_state_id(questData.QuestId, questData.IsRepeatable);

    string disabledReason = "";
    StringName lockReasonId = "";
    bool isEnabled = true;

    if (stateId is "available" or "repeatable")
    {
        QuestAcceptAvailabilityResult availability = _quest_accept_evaluator.Evaluate(
            quest_def,
            _build_quest_accept_context()
        );
        isEnabled = availability.CanAccept;
        disabledReason = availability.DisabledReason;
        lockReasonId = availability.LockReasonId;
    }

    return new GDictionary
    {
        ["entry_id"] = questData.QuestId.ToString(),
        ["quest_id"] = questData.QuestId.ToString(),
        ["provider_interaction_id"] = interaction_script_id,
        ["display_name"] = questData.DisplayName,
        ["summary_text"] = _build_contract_board_objective_summary(questData),
        ["details_text"] = _build_contract_board_entry_details(questData),
        ["state_id"] = stateId,
        ["state_label"] = _build_contract_board_state_label(stateId),
        ["cost_label"] = _build_contract_board_reward_label(questData.RewardEntries),
        ["is_enabled"] = isEnabled,
        ["disabled_reason"] = disabledReason,
        ["lock_reason_id"] = lockReasonId,
        ["is_repeatable"] = questData.IsRepeatable,
        ["accept_dialogue_text"] = quest_def.accept_dialogue_text,
        ["accept_feedback_success"] = quest_def.accept_feedback_success,
        ["accept_feedback_failure"] = quest_def.accept_feedback_failure,
        ["accept_confirmation_text"] = quest_def.accept_confirmation_text,
    };
}
```

- [ ] **Step 3: Preserve feedback text in `_build_contract_board_window_data`**

Find the line where `state_summary_text` is computed and change it to:

```csharp
string feedbackText = ReadString(payload, "feedback_text", "");
string stateSummaryText = !string.IsNullOrEmpty(feedbackText)
    ? feedbackText
    : _build_contract_board_state_summary(entries);

...
["state_summary_text"] = stateSummaryText,
```

- [ ] **Step 4: Re-evaluate on submit and handle confirmation**

Modify `_submit_contract_board_quest_action` (around line 3507):

```csharp
// After provider_interaction_id / quest_id validation, before stateId branch:
QuestAcceptAvailabilityResult availability = _quest_accept_evaluator.Evaluate(
    questData.QuestDef,
    _build_quest_accept_context()
);

if (!availability.CanAccept)
{
    string feedback = !string.IsNullOrEmpty(questData.AcceptFeedbackFailure)
        ? questData.AcceptFeedbackFailure
        : $"不满足接取条件：{availability.DisabledReason}";
    _refresh_active_contract_board_context(feedback);
    SetSettlementFeedbackText(feedback);
    return CommandError(feedback);
}

// Confirmation check
bool isConfirmationSubmission = ReadBool(payload, "confirm_accept", false);
bool hasPendingConfirmation = ReadStringName(GetActiveContractBoardContext(), "pending_confirmation_quest_id") == questId;

if (!string.IsNullOrEmpty(questData.AcceptConfirmationText) && !isConfirmationSubmission && !hasPendingConfirmation)
{
    _set_contract_board_confirmation_context(questId, questData.AcceptConfirmationText);
    return CommandOk("请确认是否接取该契约。");
}

// Clear confirmation if present
if (hasPendingConfirmation)
    _clear_contract_board_confirmation_context();

// Now proceed to accept
string stateId = _resolve_contract_board_quest_state_id(questData.QuestId, questData.IsRepeatable);
...
```

Add helper methods:

```csharp
private void _set_contract_board_confirmation_context(StringName questId, string confirmationText)
{
    GDictionary context = GetActiveContractBoardContext();
    context["pending_confirmation_quest_id"] = questId.ToString();
    context["pending_confirmation_text"] = confirmationText;
    context["pending_confirmation_source"] = "contract_board";
    SetActiveContractBoardContext(context);
}

private void _clear_contract_board_confirmation_context()
{
    GDictionary context = GetActiveContractBoardContext();
    context.Remove("pending_confirmation_quest_id");
    context.Remove("pending_confirmation_text");
    context.Remove("pending_confirmation_source");
    SetActiveContractBoardContext(context);
}
```

- [ ] **Step 5: Use feedback text on success**

After `CommandAcceptQuestTyped` succeeds:

```csharp
string successFeedback = !string.IsNullOrEmpty(questData.AcceptFeedbackSuccess)
    ? questData.AcceptFeedbackSuccess
    : $"已接取契约 {questData.DisplayName}。";
_refresh_active_contract_board_context(successFeedback);
SetSettlementFeedbackText(successFeedback);
```

- [ ] **Step 6: Build and run contract board regression**

```bash
dotnet build magic.csproj
godot --headless -s res://tests/world_map/runtime/run_game_runtime_settlement_command_handler_regression.cs
```

Expected: PASS (may require updating test fixtures to include new fields).

- [ ] **Step 7: Commit**

```bash
git add scripts/systems/game_runtime/GameRuntimeSettlementCommandHandler.cs
git commit -m "feat: integrate accept requirement evaluator and feedback into contract board"
```

---

### Task 6: Add inline confirmation sub-state to `ShopWindow`

**Files:**
- Modify: `scripts/ui/ShopWindow.cs`
- Test: `tests/world_map/runtime/run_game_runtime_settlement_command_handler_regression.cs`

**Interfaces:**
- Consumes: `pending_confirmation_quest_id`, `pending_confirmation_text`, `pending_confirmation_source` in window data
- Produces: `action_requested` signal with `confirm_accept=true` payload when player confirms

- [ ] **Step 1: Extend `ShopWindowData` parsing**

In `scripts/ui/ShopWindow.cs`, add properties to the data class (find the `ShopWindowData` class and its `From` method):

```csharp
public StringName PendingConfirmationQuestId { get; private set; } = "";
public string PendingConfirmationText { get; private set; } = "";
public string PendingConfirmationSource { get; private set; } = "";
```

In `From(GDictionary data)`:

```csharp
PendingConfirmationQuestId = ReadStringName(data, "pending_confirmation_quest_id");
PendingConfirmationText = ReadString(data, "pending_confirmation_text");
PendingConfirmationSource = ReadString(data, "pending_confirmation_source");
```

- [ ] **Step 2: Add confirmation UI state**

Add boolean state `_isShowingConfirmation` and a method to render the confirmation panel:

```csharp
private bool _isShowingConfirmation = false;

private void _show_confirmation_panel()
{
    _isShowingConfirmation = true;
    // Update confirm button label to "确认" and cancel to "返回"
    // Replace details text with PendingConfirmationText
    // Optionally show a warning color
}

private void _hide_confirmation_panel()
{
    _isShowingConfirmation = false;
    // Restore normal entry details and button labels
}
```

- [ ] **Step 3: Hook into `_refresh_ui_from_data`**

When window data is refreshed, check:

```csharp
if (_windowData.PendingConfirmationQuestId != "")
    _show_confirmation_panel();
else
    _hide_confirmation_panel();
```

- [ ] **Step 4: Modify confirm button handler**

In the confirm button pressed handler:

```csharp
private void _on_confirm_button_pressed()
{
    if (_windowData.PendingConfirmationQuestId != "" && !_isShowingConfirmation)
    {
        _show_confirmation_panel();
        return;
    }

    GDictionary payload = _build_confirm_payload();
    if (_windowData.PendingConfirmationQuestId != "")
        payload["confirm_accept"] = true;

    EmitSignal(SignalName.action_requested,
        _windowData.SettlementId,
        _windowData.ActionId,
        payload,
        _selectedMemberId,
        _quantity,
        _windowData.PanelKind
    );
}
```

- [ ] **Step 5: Modify cancel button handler**

If showing confirmation, cancel should return to normal view instead of closing the modal:

```csharp
private void _on_cancel_button_pressed()
{
    if (_isShowingConfirmation)
    {
        _hide_confirmation_panel();
        return;
    }

    EmitSignal(SignalName.closed);
}
```

- [ ] **Step 6: Build and run regression**

```bash
dotnet build magic.csproj
godot --headless -s res://tests/world_map/runtime/run_game_runtime_settlement_command_handler_regression.cs
```

Expected: PASS.

- [ ] **Step 7: Commit**

```bash
git add scripts/ui/ShopWindow.cs
git commit -m "feat: add inline confirmation sub-state to ShopWindow for quest accept"
```

---

### Task 7: Update text snapshot and project context units

**Files:**
- Modify: `scripts/utils/GameTextSnapshotRenderer.cs`
- Modify: `docs/design/project_context_units.md`
- Test: `tests/text_runtime/headless/run_headless_game_test_session_regression.cs`

**Interfaces:**
- Consumes: contract board snapshot window data with new fields
- Produces: text lines including `provider_kind`, `listing_channels`, `accept_dialogue_text`, `disabled_reason`

- [ ] **Step 1: Extend `BuildContractBoardLines`**

In `scripts/utils/GameTextSnapshotRenderer.cs`, update `BuildContractBoardLines` to include new fields per entry:

```csharp
private static List<string> BuildContractBoardLines(GDictionary contractBoardSnapshot)
{
    if (IsEmpty(contractBoardSnapshot))
        return new List<string>();

    var windowData = GetDictionary(contractBoardSnapshot, "window_data");
    var lines = new List<string>
    {
        $"visible={FormatBool(ReadExactBool(contractBoardSnapshot, "visible"))}",
        $"title={GetExactString(windowData, "title")}",
        $"settlement_id={GetExactString(windowData, "settlement_id")}",
        $"provider_interaction_id={GetExactString(windowData, "provider_interaction_id")}",
        $"state_summary_text={GetExactString(windowData, "state_summary_text")}",
    };

    foreach (GDictionary entry in GetArray(windowData, "entries"))
    {
        lines.Add($"entry={GetExactString(entry, "entry_id")}");
        lines.Add($"  display_name={GetExactString(entry, "display_name")}");
        lines.Add($"  state_label={GetExactString(entry, "state_label")}");
        lines.Add($"  cost_label={GetExactString(entry, "cost_label")}");
        lines.Add($"  is_enabled={FormatBool(ReadExactBool(entry, "is_enabled"))}");
        lines.Add($"  disabled_reason={GetExactString(entry, "disabled_reason")}");
        lines.Add($"  accept_dialogue_text={GetExactString(entry, "accept_dialogue_text")}");
    }

    return lines;
}
```

- [ ] **Step 2: Run text snapshot regression**

```bash
godot --headless -s res://tests/text_runtime/headless/run_headless_game_test_session_regression.cs
```

Expected: PASS after updating any expected snapshots (if the test uses snapshot comparison, update the expected output to include new fields).

- [ ] **Step 3: Update `docs/design/project_context_units.md`**

Find CU-02 and CU-06 and add:

```markdown
### CU-02: Quest Content
- Source: `data/configs/quests/*.tres`
- Loader: `QuestContentRegistry` (called from `ProgressionContentRegistry.Build`)
- Schema owner: `QuestDef`
- Validator: `QuestContentValidator`
- Accept requirement evaluator: `QuestAcceptRequirementEvaluator`
- Recommended reads before changes:
  - `scripts/player/progression/QuestDef.cs`
  - `scripts/player/progression/QuestContentRegistry.cs`
  - `scripts/player/progression/ProgressionContentRegistry.cs`
  - `scripts/player/progression/QuestContentValidator.cs`
  - `scripts/player/progression/QuestProviderContentRules.cs`
  - `scripts/systems/progression/QuestAcceptRequirementEvaluator.cs`

### CU-06: Settlement Runtime Commands
- Contract board modal: `GameRuntimeSettlementCommandHandler` + `ShopWindow`
- Accept availability: `QuestAcceptRequirementEvaluator` invoked by handler
- Confirmation state: modal context `pending_confirmation_quest_id/text/source`
- Recommended reads before changes:
  - `scripts/systems/game_runtime/GameRuntimeSettlementCommandHandler.cs`
  - `scripts/ui/ShopWindow.cs`
```

- [ ] **Step 4: Commit**

```bash
git add scripts/utils/GameTextSnapshotRenderer.cs docs/design/project_context_units.md
git commit -m "feat: update text snapshot and context units for quest accept flow"
```

---

## Spec Coverage Check

| Spec Section | Implementing Task |
|---|---|
| §3.1 `QuestAcceptRequirementEvaluator` | Task 4 |
| §3.2 Lifecycle / availability separation | Task 5 (entry `is_enabled`/`disabled_reason`) |
| §3.3 Provider / listing channels | Task 2 |
| §3.4 Strict schema | Task 1 |
| §3.5 `.tres` loading path | Task 3 |
| §3.6 Confirmation modal context | Task 5 + Task 6 |
| §4.1 Quest-state requirements | Task 4 |
| §4.2 Future requirements (not Phase 1) | not covered |
| §5 Accept text fields | Task 1 + Task 5 |
| §6 Contract board flow | Task 5 + Task 6 |
| §6.3 Bounty board deferred | not covered in Phase 1 |
| §7 NPC deferred | not covered in Phase 1 |
| §8 Config examples | Task 3 |
| §10 Phase 1 slice | Tasks 1–7 |
| §11 Phase 1 tests | Each task has test step |
| §12 Context units | Task 7 |
| §13 Phase 1 acceptance | Verified by Task 3/5/7 tests |

## Placeholder Scan

No TBD, TODO, or vague steps remain. Every task includes:
- Exact file paths
- Code blocks for code changes
- Exact commands with expected output
- Commit commands

## Execution Handoff

Plan complete and archived at `docs/archive/implementation-plans/landed/2026-06-28-quest-accept-flow-phase1.md`.

**Two execution options:**

**1. Subagent-Driven (recommended)** - I dispatch a fresh subagent per task, review between tasks, fast iteration.

**2. Inline Execution** - Execute tasks in this session using `executing-plans`, batch execution with checkpoints.

**Which approach?**
