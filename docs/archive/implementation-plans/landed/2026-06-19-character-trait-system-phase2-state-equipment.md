# Character Trait System Phase 2 State And Equipment Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the Phase 2 state and equipment layer for the generic character trait system, including strict trait instance payloads, party/equipment save schema changes, item trait references, and deterministic equipment trait rolling.

**Architecture:** `TraitDef` remains the content truth source from Phase 1, while `TraitInstanceState` becomes the persisted instance truth source for character-granted and equipment-roll traits. Fixed equipment traits stay on `ItemDef`; rolled equipment traits live on `EquipmentInstanceState`; RNG is isolated in `EquipmentTraitRollService` and is only used when a stable new equipment instance id exists.

**Tech Stack:** Godot 4.6.2, GodotSharp C# net8.0, `.tres` Resource configs, strict `Godot.Collections.Dictionary` save payloads, headless C# regression runners.

## Global Constraints

- Scope is Phase 2 only: no `CharacterTraitService`, no `EffectiveTraitSet`, no `BattleUnitState.effective_trait_instances`, no `TraitTriggerHooks` runtime migration, and no `RaceTraitDef` deletion.
- No old save fallback, no legacy save aliases, no empty-list migrations, and no old payload/schema support.
- `PartyState.version` must change from `3` to `4`.
- `GameSession.SaveVersion` and `SaveSerializer._save_version` must change from `7` to `8`.
- `SaveSerializer._save_index_version` and `GameSession.SaveIndexVersion` must remain `3`.
- `TraitInstanceState.roll_values` is a payload backing store only; runtime callers use typed readers and `ValidateAgainstDef(TraitDef def)`.
- `EquipmentTraitRollService.MintWithRolls()` is the only Phase 2 API allowed to consume RNG for trait rolling.
- Load, clone, duplicate, equipment equip/unequip, warehouse batch swap, AI projection, and save normalization paths must preserve existing `trait_instances` and consume zero RNG.
- `ItemDef.trait_ids` represent fixed equipment traits and are not copied into `EquipmentInstanceState`.
- `EquipmentInstanceState.trait_instances` represent only `equipment_roll` instances.
- `PartyMemberState.trait_instances` represent only `character` instances.
- Item trait reference validation must use typed `TraitDef` catalogs; do not query a global singleton from `ItemContentRegistry`.
- Do not include battle simulation, balance simulation, benchmark, or auto-tuning runners in Phase 2 verification.
- Do not revert or touch unrelated dirty files in the current worktree.
- Do not commit unless the user explicitly asks for commits in the active implementation session.

---

## File Structure

Create:

- `scripts/player/progression/TraitInstanceState.cs`: strict persisted trait instance schema, typed roll readers, and schema validation against `TraitDef`.
- `scripts/player/progression/TraitInstanceCollection.cs`: shared array serialization, parsing, duplication, and expected-source filtering.
- `scripts/player/warehouse/TraitRollGroupDef.cs`: item roll group resource.
- `scripts/player/warehouse/TraitRollGroupEntryDef.cs`: weighted roll entry resource.
- `scripts/player/warehouse/ItemTraitContentValidator.cs`: cross-catalog validation for item fixed traits and roll groups.
- `scripts/systems/inventory/EquipmentTraitRollService.cs`: deterministic RNG boundary for minting equipment-roll trait instances.
- `tests/progression/schema/run_trait_instance_state_schema_regression.cs`: strict payload and `roll_values` schema coverage.
- `tests/equipment/run_equipment_trait_roll_regression.cs`: deterministic roll service and warehouse minting coverage.

Modify:

- `scripts/player/progression/PartyMemberState.cs`: add `trait_instances` field, strict serialization, strict parse, and deep copy.
- `scripts/player/progression/PartyState.cs`: bump version to `4` and keep strict version rejection.
- `scripts/player/warehouse/EquipmentInstanceState.cs`: add `trait_instances` field to save and transient payload schema.
- `scripts/player/warehouse/ItemDef.cs`: add `trait_ids`, `trait_roll_groups`, and typed getters.
- `scripts/player/warehouse/ItemContentRegistry.cs`: merge template trait fields and keep local item-only validation stable.
- `scripts/player/warehouse/WarehouseState.cs`: continue using `EquipmentInstanceState.FromDictionary`; no separate trait copy layer.
- `scripts/player/warehouse/WarehouseStateItemValidator.cs`: validate equipment instance trait payloads against the supplied trait catalog when provided.
- `scripts/systems/inventory/PartyWarehouseService.cs`: accept optional `EquipmentTraitRollService` and mint only when assigning a new stable equipment instance id.
- `scripts/systems/game_runtime/GameRuntimeFacade.cs`: construct and pass the equipment trait roll service when setting up warehouse services.
- `scripts/systems/game_runtime/GameRuntimeBattleLootCommitService.cs`: keep battle loot transient instances unrolled until warehouse assignment creates a stable id.
- `scripts/systems/persistence/GameSession.cs`: bump top-level save version and wire the trait-roll service setup with typed trait defs.
- `scripts/systems/persistence/SaveSerializer.cs`: bump serializer save version and keep save index version unchanged.
- `tests/runtime/validation/ContentValidationRunner.cs`: validate item trait references with official `trait_defs`.
- `tests/runtime/validation/run_resource_validation_regression.cs`: assert invalid item trait references and official validation remain stable.
- `tests/runtime/persistence/run_save_serializer_quest_round_trip_regression.cs`: update save and party version assertions.
- `tests/runtime/persistence/run_invalid_save_graceful_regression.cs`: keep save index type assertions unchanged.
- `tests/equipment/run_equipment_drop_service_regression.cs`: assert drop service still returns transient instances without trait rolls.
- `tests/equipment/run_party_equipment_regression.cs`: extend round-trip cases to preserve equipment instance `trait_instances`.
- `tests/warehouse/run_party_warehouse_batch_swap_regression.cs`: assert batch swap preserves equipment instance `trait_instances`.
- `tests/warehouse/run_warehouse_state_item_validator_regression.cs`: update equipment payload schema expectations and add trait validation cases.
- `tests/progression/core/run_party_state_duplicate_regression.cs`: assert party/member/equipment trait instances deep copy.
- `docs/design/project_context_units.md`: after code lands, update CU-10 and CU-11 if the final ownership notes need to mention trait instance state and equipment trait rolling.

Leave unchanged in Phase 2:

- `scripts/systems/progression/CharacterManagementModule.cs` trait aggregation behavior.
- `scripts/systems/attributes/*` trait aggregation behavior.
- `scripts/systems/battle/*` effective trait payloads and trigger dispatch.
- `scripts/player/progression/RaceTraitDef.cs` and `RaceTraitContentRegistry.cs`.
- `data/configs/race_traits/*`.

---

### Task 1: Trait Instance Strict Schema

**Files:**
- Create: `scripts/player/progression/TraitInstanceState.cs`
- Create: `scripts/player/progression/TraitInstanceCollection.cs`
- Test: `tests/progression/schema/run_trait_instance_state_schema_regression.cs`

**Interfaces:**
- Consumes: `TraitDef`, `TraitRollValueSchemaEntry`, `TraitContentRules.ToSourceKind(StringName)`, `TraitContentRules.ToStringName(TraitSourceKind)`, `TraitContentRules.ToRollValueType(StringName)`.
- Produces: `TraitInstanceState.Create(StringName, StringName, TraitSourceKind, StringName, int, int, Godot.Collections.Dictionary)`.
- Produces: `TraitInstanceState.NormalizeRollValues(Godot.Collections.Dictionary)`.
- Produces: `TraitInstanceState.GetIntRoll(StringName, int)`, `GetStringNameRoll(StringName, StringName)`, `GetBoolRoll(StringName, bool)`.
- Produces: `TraitInstanceState.ToDictionary()`, `FromDictionary(Godot.Collections.Dictionary)`, `GetPayloadValidationError(Godot.Collections.Dictionary)`, `ValidateAgainstDef(TraitDef)`, `DuplicateState()`.
- Produces: `TraitInstanceCollection.ToPayloadArray(Godot.Collections.Array<TraitInstanceState>)`, `FromPayloadArray(Variant, TraitSourceKind)`, `Duplicate(Godot.Collections.Array<TraitInstanceState>)`.

- [ ] **Step 1: Write the failing schema regression**

Create `tests/progression/schema/run_trait_instance_state_schema_regression.cs` with these exact behavior cases:

```csharp
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_trait_instance_state_schema_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestStrictPayloadRoundTripAndTypedReaders();
        TestRejectsMissingExtraAndWrongTypedPayloadFields();
        TestRejectsWrongSourceForCollection();
        TestValidateAgainstDefRequiresExactRollSchema();
        TestDuplicateDeepCopiesRollValues();
        Quit(_test.Finish("Trait instance state schema regression"));
    }

    private void TestStrictPayloadRoundTripAndTypedReaders()
    {
        TraitInstanceState state = TraitInstanceState.Create(
            "eq_000001_t01",
            "sharp_edge",
            TraitSourceKind.EquipmentRoll,
            "eq_000001",
            rank: 2,
            stacks: 3,
            rollValues: new GDictionary
            {
                ["amount"] = 4,
                [new StringName("damage_tag")] = "physical_slash",
                ["enabled"] = true,
            }
        );

        GDictionary payload = state.ToDictionary();
        TraitInstanceState restored = TraitInstanceState.FromDictionary(payload);

        _test.True(restored != null, "Canonical trait instance payload should parse.");
        _test.Eq(restored.trait_instance_id, new StringName("eq_000001_t01"), "trait_instance_id should round-trip.");
        _test.Eq(restored.GetIntRoll("amount", -1), 4, "int roll reader should read normalized key.");
        _test.Eq(restored.GetStringNameRoll("damage_tag", "missing"), new StringName("physical_slash"), "string_name roll reader should read string values.");
        _test.True(restored.GetBoolRoll("enabled", false), "bool roll reader should read bool values.");
        _test.Eq(restored.GetIntRoll("missing", 9), 9, "missing int roll should use fallback.");
    }

    private void TestRejectsMissingExtraAndWrongTypedPayloadFields()
    {
        GDictionary payload = TraitInstanceState.Create(
            "char_trait_001",
            "battle_hardened",
            TraitSourceKind.Character,
            "reward_intro"
        ).ToDictionary();

        GDictionary missing = (GDictionary)payload.Duplicate(true);
        missing.Remove("roll_values");
        _test.True(TraitInstanceState.FromDictionary(missing) == null, "Missing required field should reject trait instance payload.");

        GDictionary extra = (GDictionary)payload.Duplicate(true);
        extra["extra"] = true;
        _test.True(TraitInstanceState.FromDictionary(extra) == null, "Extra field should reject trait instance payload.");

        GDictionary wrongType = (GDictionary)payload.Duplicate(true);
        wrongType["rank"] = "1";
        _test.True(TraitInstanceState.FromDictionary(wrongType) == null, "rank must be an int.");

        GDictionary missingInstanceId = (GDictionary)payload.Duplicate(true);
        missingInstanceId["trait_instance_id"] = "";
        _test.True(TraitInstanceState.FromDictionary(missingInstanceId) == null, "character source requires a stable trait_instance_id.");
    }

    private void TestRejectsWrongSourceForCollection()
    {
        var array = new Godot.Collections.Array
        {
            TraitInstanceState.Create(
                "char_trait_001",
                "battle_hardened",
                TraitSourceKind.Character,
                "reward_intro"
            ).ToDictionary(),
        };

        _test.True(
            TraitInstanceCollection.FromPayloadArray(Variant.From(array), TraitSourceKind.Character) != null,
            "Expected source kind should parse."
        );
        _test.True(
            TraitInstanceCollection.FromPayloadArray(Variant.From(array), TraitSourceKind.EquipmentRoll) == null,
            "Wrong source kind should reject the full collection."
        );
    }

    private void TestValidateAgainstDefRequiresExactRollSchema()
    {
        TraitDef def = new()
        {
            trait_id = "sharp_edge",
            allowed_source_kinds = new Godot.Collections.Array<StringName> { "equipment_roll" },
            roll_value_schema = new Godot.Collections.Array<TraitRollValueSchemaEntry>
            {
                new() { key = "amount", value_type = "int", min_value = 1, max_value = 6 },
                new()
                {
                    key = "damage_tag",
                    value_type = "string_name",
                    allowed_values = new Godot.Collections.Array<StringName> { "physical_slash", "physical_pierce" },
                },
                new() { key = "enabled", value_type = "bool" },
            },
        };

        TraitInstanceState valid = TraitInstanceState.Create(
            "eq_000001_t01",
            "sharp_edge",
            TraitSourceKind.EquipmentRoll,
            "eq_000001",
            rollValues: new GDictionary
            {
                ["amount"] = 4,
                ["damage_tag"] = "physical_slash",
                ["enabled"] = true,
            }
        );
        _test.Eq(valid.ValidateAgainstDef(def), "", "Exact roll schema should validate.");

        TraitInstanceState missing = valid.DuplicateState();
        missing.roll_values.Remove("enabled");
        _test.True(missing.ValidateAgainstDef(def).Contains("missing roll key"), "Missing schema key should fail.");

        TraitInstanceState outOfRange = valid.DuplicateState();
        outOfRange.roll_values["amount"] = 99;
        _test.True(outOfRange.ValidateAgainstDef(def).Contains("out of range"), "Out-of-range int roll should fail.");

        TraitInstanceState unexpected = valid.DuplicateState();
        unexpected.roll_values["extra"] = 1;
        _test.True(unexpected.ValidateAgainstDef(def).Contains("unexpected roll key"), "Unexpected roll key should fail.");
    }

    private void TestDuplicateDeepCopiesRollValues()
    {
        TraitInstanceState source = TraitInstanceState.Create(
            "eq_000001_t01",
            "sharp_edge",
            TraitSourceKind.EquipmentRoll,
            "eq_000001",
            rollValues: new GDictionary { ["amount"] = 4 }
        );
        TraitInstanceState copy = source.DuplicateState();
        copy.roll_values["amount"] = 6;

        _test.Eq(source.GetIntRoll("amount", -1), 4, "DuplicateState should deep-copy roll_values.");
        _test.Eq(copy.GetIntRoll("amount", -1), 6, "copy should keep its own roll_values.");
    }
}
```

- [ ] **Step 2: Run the regression to verify RED**

Run:

```bash
dotnet build magic.csproj
```

Expected: FAIL because `TraitInstanceState` and `TraitInstanceCollection` do not exist.

- [ ] **Step 3: Implement `TraitInstanceState`**

Create `scripts/player/progression/TraitInstanceState.cs`. Use the field set below exactly; payload keys are string keys only:

```csharp
using Godot;

public partial class TraitInstanceState : RefCounted
{
    private const string SavePayloadLabel = "trait instance payload";

    private static readonly string[] RequiredFields =
    {
        "trait_instance_id",
        "trait_id",
        "source_type",
        "source_id",
        "rank",
        "stacks",
        "roll_values",
    };

    public StringName trait_instance_id = "";
    public StringName trait_id = "";
    public StringName source_type = "";
    public StringName source_id = "";
    public int rank = 1;
    public int stacks = 1;
    public Godot.Collections.Dictionary roll_values = new();

    internal TraitSourceKind SourceKind => TraitContentRules.ToSourceKind(source_type);

    public static TraitInstanceState Create(
        StringName traitInstanceId,
        StringName traitId,
        TraitSourceKind sourceKind,
        StringName sourceId,
        int rank = 1,
        int stacks = 1,
        Godot.Collections.Dictionary rollValues = null)
    {
        return new TraitInstanceState
        {
            trait_instance_id = ProgressionDataUtils.to_string_name(traitInstanceId),
            trait_id = ProgressionDataUtils.to_string_name(traitId),
            source_type = TraitContentRules.ToStringName(sourceKind),
            source_id = ProgressionDataUtils.to_string_name(sourceId),
            rank = Mathf.Max(rank, 1),
            stacks = Mathf.Max(stacks, 1),
            roll_values = NormalizeRollValues(rollValues),
        };
    }
}
```

Complete the class with:

- `NormalizeRollValues`: skip empty keys and store normalized `StringName` keys.
- `GetIntRoll`: return fallback unless the normalized key exists and the value is `Variant.Type.Int`.
- `GetStringNameRoll`: accept `Variant.Type.String` and `Variant.Type.StringName`, normalize with `ProgressionDataUtils.to_string_name`.
- `GetBoolRoll`: return fallback unless value is `Variant.Type.Bool`.
- `ToDictionary`: output all seven required fields.
- `DuplicateState`: deep-copy normalized `roll_values`.
- `FromDictionary`: call `GetPayloadValidationError`; log `GameLog.Error(err, "trait.validation_failed", "progression")` and return null on error.
- `ValidateAgainstDef`: enforce exact schema keys, value types, int ranges, and string_name allowed values.
- `GetPayloadValidationError`: reject null, missing fields, extra fields, non-string keys, non-string ids/source fields, empty `trait_id`, invalid `source_type`, rank/stacks `< 1`, non-dictionary `roll_values`, and empty `trait_instance_id` for `Character` or `EquipmentRoll`.

- [ ] **Step 4: Implement `TraitInstanceCollection`**

Create `scripts/player/progression/TraitInstanceCollection.cs`:

```csharp
using Godot;

internal static class TraitInstanceCollection
{
    internal static Godot.Collections.Array ToPayloadArray(
        Godot.Collections.Array<TraitInstanceState> instances)
    {
        var payload = new Godot.Collections.Array();
        if (instances == null)
            return payload;
        foreach (TraitInstanceState instance in instances)
            if (instance != null)
                payload.Add(instance.ToDictionary());
        return payload;
    }

    internal static Godot.Collections.Array<TraitInstanceState> FromPayloadArray(
        Variant payload,
        TraitSourceKind expectedKind)
    {
        if (payload.VariantType != Variant.Type.Array)
            return null;
        var result = new Godot.Collections.Array<TraitInstanceState>();
        foreach (Variant entry in payload.AsGodotArray())
        {
            if (entry.VariantType != Variant.Type.Dictionary)
                return null;
            TraitInstanceState instance = TraitInstanceState.FromDictionary(entry.AsGodotDictionary());
            if (instance == null || instance.SourceKind != expectedKind)
                return null;
            result.Add(instance);
        }
        return result;
    }

    internal static Godot.Collections.Array<TraitInstanceState> Duplicate(
        Godot.Collections.Array<TraitInstanceState> instances)
    {
        var result = new Godot.Collections.Array<TraitInstanceState>();
        if (instances == null)
            return result;
        foreach (TraitInstanceState instance in instances)
            if (instance != null)
                result.Add(instance.DuplicateState());
        return result;
    }
}
```

- [ ] **Step 5: Run and verify GREEN**

Run:

```bash
dotnet build magic.csproj
godot --headless -s res://tests/progression/schema/run_trait_instance_state_schema_regression.cs
```

Expected: build PASS; headless regression PASS.

- [ ] **Step 6: Record progress**

Update `.git/sdd/progress.md` only if using the existing SDD progress workflow. Do not commit unless explicitly requested.

---

### Task 2: Party And Equipment Host Serialization

**Files:**
- Modify: `scripts/player/progression/PartyMemberState.cs`
- Modify: `scripts/player/progression/PartyState.cs`
- Modify: `scripts/player/warehouse/EquipmentInstanceState.cs`
- Modify: `scripts/player/equipment/EquipmentEntryState.cs` only if compilation requires namespace visibility changes.
- Test: `tests/progression/core/run_party_state_duplicate_regression.cs`
- Test: `tests/equipment/run_party_equipment_regression.cs`
- Test: `tests/warehouse/run_warehouse_state_item_validator_regression.cs`

**Interfaces:**
- Consumes: `TraitInstanceCollection` from Task 1.
- Produces: `PartyMemberState.trait_instances`.
- Produces: `EquipmentInstanceState.trait_instances`.
- Produces: `PartyState.version == 4`.

- [ ] **Step 1: Write failing host serialization tests**

Add focused assertions to existing tests:

1. In `tests/progression/core/run_party_state_duplicate_regression.cs`, extend `BuildMemberState()` with:

```csharp
member.trait_instances.Add(
    TraitInstanceState.Create(
        "char_trait_001",
        "battle_hardened",
        TraitSourceKind.Character,
        "reward_intro",
        rollValues: new GDictionary { ["amount"] = 2 }
    )
);
```

Then mutate the copy in `TestDuplicateStateDeepCopiesBattleWritebackState()`:

```csharp
copyHero.trait_instances[0].roll_values["amount"] = 7;
_test.Eq(
    sourceHero.trait_instances[0].GetIntRoll("amount", -1),
    2,
    "修改 copy 人物 trait_instances 不应影响源队伍。"
);
```

2. In the same test, set a warehouse equipment trait in `BuildPartyState()`:

```csharp
EquipmentInstanceState spare = EquipmentInstanceState.CreateInstance("spare_sword", "eq_000002");
spare.trait_instances.Add(
    TraitInstanceState.Create(
        "eq_000002_t01",
        "sharp_edge",
        TraitSourceKind.EquipmentRoll,
        "eq_000002",
        rollValues: new GDictionary { ["amount"] = 4 }
    )
);
```

Use `spare` in `equipment_instances`, mutate `copy.warehouse_state.equipment_instances[0].trait_instances[0].roll_values["amount"]`, and assert the source remains `4`.

3. In `tests/equipment/run_party_equipment_regression.cs`, add a round-trip assertion to any existing `PartyState.FromDictionary(partyState.ToDictionary())` case:

```csharp
EquipmentInstanceState restoredInstance = restoredPartyState
    .GetMemberState("hero")
    .equipment_state
    .GetEquippedInstance(EquipmentRules.ToStringName(EquipmentSlotKind.MainHand));
_test.Eq(
    restoredInstance.trait_instances[0].GetIntRoll("amount", -1),
    4,
    "Equipped equipment trait_instances should survive PartyState round-trip."
);
```

4. In `tests/warehouse/run_warehouse_state_item_validator_regression.cs`, keep `EquipmentInstanceState.CreateInstance(...).ToDictionary()` as the canonical payload source and add:

```csharp
_test.True(
    instancePayload.ContainsKey("trait_instances"),
    "Canonical equipment instance payload should include trait_instances."
);
```

- [ ] **Step 2: Run tests to verify RED**

Run:

```bash
dotnet build magic.csproj
```

Expected: FAIL because `PartyMemberState.trait_instances`, `EquipmentInstanceState.trait_instances`, or `PartyState.version == 4` assertions are not implemented yet.

- [ ] **Step 3: Update `PartyMemberState`**

In `scripts/player/progression/PartyMemberState.cs`:

- Add `public Godot.Collections.Array<TraitInstanceState> trait_instances = new();`.
- Append `"trait_instances"` to `TO_DICT_FIELDS`.
- In `DuplicateState()`, add `trait_instances = TraitInstanceCollection.Duplicate(trait_instances)`.
- In `ToDictionary()`, add `{ "trait_instances", TraitInstanceCollection.ToPayloadArray(trait_instances) }`.
- In `FromDictionary()`, after parsing scalar fields and before constructing the result, parse:

```csharp
if (!data.ContainsKey("trait_instances"))
    return null;
var traitInstances = TraitInstanceCollection.FromPayloadArray(
    data["trait_instances"],
    TraitSourceKind.Character
);
if (traitInstances == null)
    return null;
```

Assign `trait_instances = traitInstances` in the constructed `PartyMemberState`.

- [ ] **Step 4: Update `EquipmentInstanceState`**

In `scripts/player/warehouse/EquipmentInstanceState.cs`:

- Add `public Godot.Collections.Array<TraitInstanceState> trait_instances = new();`.
- Add `trait_instances` to `ToDictionary()`.
- Add `trait_instances = TraitInstanceCollection.Duplicate(trait_instances)` to `DuplicateState()`.
- Change `requiredFields` from four keys to:

```csharp
var requiredFields = new[]
{
    "instance_id",
    "item_id",
    "rarity",
    "current_durability",
    "trait_instances",
};
```

- In `_get_payload_validation_error`, reject missing/non-array `trait_instances`.
- In `_from_dict`, parse:

```csharp
var traitInstances = TraitInstanceCollection.FromPayloadArray(
    payload["trait_instances"],
    TraitSourceKind.EquipmentRoll
);
if (traitInstances == null)
{
    GameLog.Error(
        $"Corrupt {payloadLabel}: trait_instances contains invalid equipment_roll entries.",
        "equipment.validation_failed",
        "equipment"
    );
    return null;
}
```

Then assign `trait_instances = traitInstances` in the returned object.

- [ ] **Step 5: Bump `PartyState` version only**

In `scripts/player/progression/PartyState.cs`:

- Change `public int version = 3;` to `public int version = 4;`.
- Change `FromDictionary` strict version check from `!= 3` to `!= 4`.
- Do not add a `version == 3` fallback path.

- [ ] **Step 6: Run and verify GREEN**

Run:

```bash
dotnet build magic.csproj
godot --headless -s res://tests/progression/core/run_party_state_duplicate_regression.cs
godot --headless -s res://tests/equipment/run_party_equipment_regression.cs
godot --headless -s res://tests/warehouse/run_warehouse_state_item_validator_regression.cs
```

Expected: build PASS; all three regressions PASS.

- [ ] **Step 7: Record progress**

Update `.git/sdd/progress.md` if using SDD. Do not commit unless explicitly requested.

---

### Task 3: Save Version Bump Without Save Index Migration

**Files:**
- Modify: `scripts/systems/persistence/GameSession.cs`
- Modify: `scripts/systems/persistence/SaveSerializer.cs`
- Modify: `tests/runtime/persistence/run_save_serializer_quest_round_trip_regression.cs`
- Test: `tests/runtime/persistence/run_invalid_save_graceful_regression.cs`
- Test: `tests/battle_runtime/runtime/run_save_index_resilience_regression.cs`

**Interfaces:**
- Consumes: `PartyState.version == 4` from Task 2.
- Produces: top-level save payload version `8`.
- Preserves: save index version `3`.

- [ ] **Step 1: Write failing version assertions**

In `tests/runtime/persistence/run_save_serializer_quest_round_trip_regression.cs`:

- After `GDictionary payload = BuildSavePayloadForSession(...)`, add:

```csharp
_test.Eq(
    DictInt(payload, "version", -1),
    8,
    "Phase 2 trait instance schema should bump top-level save version to 8."
);
```

- Change the restored party assertion to:

```csharp
_test.Eq(
    restoredPartyState.version,
    4,
    "Phase 2 trait instance schema should bump PartyState.version to 4."
);
```

In `tests/battle_runtime/runtime/run_save_index_resilience_regression.cs`, keep the existing `SaveIndexVersion = 3` expectation. Add one assertion after a save index payload is rebuilt if the helper exposes the payload:

```csharp
AssertSaveIndexFileUsesCurrentSchema("Phase 2 save payload bump");
```

- [ ] **Step 2: Run version tests to verify RED**

Run:

```bash
dotnet build magic.csproj
godot --headless -s res://tests/runtime/persistence/run_save_serializer_quest_round_trip_regression.cs
```

Expected: build PASS; test FAIL because payload version is still `7` or party version assertion still sees `3`.

- [ ] **Step 3: Bump save version constants**

In `scripts/systems/persistence/GameSession.cs`:

```csharp
private const int SaveVersion = 8;
private const int SaveIndexVersion = 3;
```

In `scripts/systems/persistence/SaveSerializer.cs`:

```csharp
private int _save_version = 8;
private int _save_index_version = 3;
```

Do not add decode support for version `7`.

- [ ] **Step 4: Run and verify GREEN**

Run:

```bash
dotnet build magic.csproj
godot --headless -s res://tests/runtime/persistence/run_save_serializer_quest_round_trip_regression.cs
godot --headless -s res://tests/runtime/persistence/run_invalid_save_graceful_regression.cs
godot --headless -s res://tests/battle_runtime/runtime/run_save_index_resilience_regression.cs
```

Expected: build PASS; save serializer PASS with version `8`; invalid save test PASS; save index resilience PASS with index version `3`.

- [ ] **Step 5: Record progress**

Update `.git/sdd/progress.md` if using SDD. Do not commit unless explicitly requested.

---

### Task 4: Item Trait Fields And Cross-Catalog Validation

**Files:**
- Create: `scripts/player/warehouse/TraitRollGroupDef.cs`
- Create: `scripts/player/warehouse/TraitRollGroupEntryDef.cs`
- Create: `scripts/player/warehouse/ItemTraitContentValidator.cs`
- Modify: `scripts/player/warehouse/ItemDef.cs`
- Modify: `scripts/player/warehouse/ItemContentRegistry.cs`
- Modify: `tests/runtime/validation/ContentValidationRunner.cs`
- Modify: `tests/runtime/validation/run_resource_validation_regression.cs`
- Modify: `tests/runtime/validation/run_item_recipe_registry_typed_regression.cs`

**Interfaces:**
- Consumes: typed `IReadOnlyDictionary<StringName, TraitDef>` from Phase 1.
- Produces: `ItemDef.trait_ids`, `ItemDef.trait_roll_groups`, `GetTraitIdsTyped()`, `GetTraitRollGroupsTyped()`.
- Produces: `ItemTraitContentValidator.Validate(IReadOnlyDictionary<StringName, ItemDef>, IReadOnlyDictionary<StringName, TraitDef>, string)`.

- [ ] **Step 1: Write failing item validation tests**

In `tests/runtime/validation/run_item_recipe_registry_typed_regression.cs`, add tests that build in-memory `ItemDef` and `TraitDef` catalogs:

```csharp
private void TestItemTraitValidationAcceptsSourceScopedReferences()
{
    Dictionary<StringName, TraitDef> traits = BuildTraitDefs();
    Dictionary<StringName, ItemDef> items = new()
    {
        ["trait_sword"] = BuildEquipmentItem(
            "trait_sword",
            fixedTraits: new[] { "guarded_grip" },
            rollTraits: new[] { "sharp_edge" }
        ),
    };

    List<string> errors = ItemTraitContentValidator.Validate(items, traits, "fixture_items");

    _test.Eq(errors.Count, 0, $"Valid item trait references should pass. errors={FormatErrors(errors)}");
}

private void TestItemTraitValidationRejectsWrongSourceAndUnsatisfiableRollGroups()
{
    Dictionary<StringName, TraitDef> traits = BuildTraitDefs();
    Dictionary<StringName, ItemDef> items = new()
    {
        ["bad_fixed"] = BuildEquipmentItem(
            "bad_fixed",
            fixedTraits: new[] { "identity_only" },
            rollTraits: System.Array.Empty<string>()
        ),
        ["bad_roll"] = BuildEquipmentItem(
            "bad_roll",
            fixedTraits: System.Array.Empty<string>(),
            rollTraits: new[] { "guarded_grip" }
        ),
        ["bad_exclusive"] = BuildEquipmentItem(
            "bad_exclusive",
            fixedTraits: System.Array.Empty<string>(),
            rollTraits: new[] { "sharp_edge", "heavy_head" },
            rollCount: 2,
            exclusiveGroup: "prefix"
        ),
    };

    List<string> errors = ItemTraitContentValidator.Validate(items, traits, "fixture_items");

    AssertContains(errors, "bad_fixed", "equipment_fixed", "fixed trait should require equipment_fixed source.");
    AssertContains(errors, "bad_roll", "equipment_roll", "roll group trait should require equipment_roll source.");
    AssertContains(errors, "bad_exclusive", "unsatisfiable", "exclusive groups should reject impossible roll_count.");
}
```

Add helper methods in the same test file:

```csharp
private static Dictionary<StringName, TraitDef> BuildTraitDefs()
{
    return new Dictionary<StringName, TraitDef>
    {
        ["guarded_grip"] = new TraitDef
        {
            trait_id = "guarded_grip",
            allowed_source_kinds = new Godot.Collections.Array<StringName> { "equipment_fixed" },
        },
        ["sharp_edge"] = new TraitDef
        {
            trait_id = "sharp_edge",
            allowed_source_kinds = new Godot.Collections.Array<StringName> { "equipment_roll" },
            roll_value_schema = new Godot.Collections.Array<TraitRollValueSchemaEntry>
            {
                new() { key = "amount", value_type = "int", min_value = 1, max_value = 6 },
            },
        },
        ["heavy_head"] = new TraitDef
        {
            trait_id = "heavy_head",
            allowed_source_kinds = new Godot.Collections.Array<StringName> { "equipment_roll" },
        },
        ["identity_only"] = new TraitDef
        {
            trait_id = "identity_only",
            allowed_source_kinds = new Godot.Collections.Array<StringName> { "identity" },
        },
    };
}
```

- [ ] **Step 2: Run tests to verify RED**

Run:

```bash
dotnet build magic.csproj
```

Expected: FAIL because `TraitRollGroupDef`, `TraitRollGroupEntryDef`, `ItemDef.trait_ids`, `ItemDef.trait_roll_groups`, and `ItemTraitContentValidator` do not exist.

- [ ] **Step 3: Add roll group resources**

Create `scripts/player/warehouse/TraitRollGroupDef.cs`:

```csharp
using Godot;

[GlobalClass]
public partial class TraitRollGroupDef : Resource
{
    [Export]
    public StringName group_id { get; set; } = "";

    [Export(PropertyHint.Range, "1,99,1")]
    public int roll_count { get; set; } = 1;

    [Export]
    public Godot.Collections.Array<TraitRollGroupEntryDef> entries { get; set; } = new();
}
```

Create `scripts/player/warehouse/TraitRollGroupEntryDef.cs`:

```csharp
using Godot;

[GlobalClass]
public partial class TraitRollGroupEntryDef : Resource
{
    [Export]
    public StringName trait_id { get; set; } = "";

    [Export(PropertyHint.Range, "1,999999,1")]
    public int weight { get; set; } = 1;

    [Export]
    public StringName exclusive_group { get; set; } = "";
}
```

- [ ] **Step 4: Add fields and typed getters to `ItemDef`**

In `scripts/player/warehouse/ItemDef.cs`, add:

```csharp
[Export]
public Godot.Collections.Array<StringName> trait_ids { get; set; } = new();

[Export]
public Godot.Collections.Array<TraitRollGroupDef> trait_roll_groups { get; set; } = new();

public List<StringName> GetTraitIdsTyped() => NormalizeStringNameList(trait_ids);

public List<TraitRollGroupDef> GetTraitRollGroupsTyped()
{
    var result = new List<TraitRollGroupDef>();
    foreach (TraitRollGroupDef group in trait_roll_groups)
        if (group != null)
            result.Add(group);
    return result;
}
```

- [ ] **Step 5: Merge item template trait fields**

In `scripts/player/warehouse/ItemContentRegistry.cs`, update `MergeWithTemplate`:

```csharp
trait_ids = MergeStringNameArray(template.trait_ids, instance.trait_ids),
trait_roll_groups = MergeTraitRollGroups(template.trait_roll_groups, instance.trait_roll_groups),
```

Add helper:

```csharp
private static Godot.Collections.Array<TraitRollGroupDef> MergeTraitRollGroups(
    Godot.Collections.Array<TraitRollGroupDef> templateGroups,
    Godot.Collections.Array<TraitRollGroupDef> instanceGroups)
{
    var result = new Godot.Collections.Array<TraitRollGroupDef>();
    var indexById = new Dictionary<StringName, int>();

    void AddOrReplace(TraitRollGroupDef group)
    {
        if (group == null || group.group_id == "")
            return;
        TraitRollGroupDef copy = group.Duplicate(true) as TraitRollGroupDef;
        if (copy == null)
            return;
        if (indexById.TryGetValue(copy.group_id, out int existingIndex))
        {
            result[existingIndex] = copy;
            return;
        }
        indexById[copy.group_id] = result.Count;
        result.Add(copy);
    }

    foreach (TraitRollGroupDef group in templateGroups)
        AddOrReplace(group);
    foreach (TraitRollGroupDef group in instanceGroups)
        AddOrReplace(group);
    return result;
}
```

- [ ] **Step 6: Implement `ItemTraitContentValidator`**

Create `scripts/player/warehouse/ItemTraitContentValidator.cs`:

```csharp
using System.Collections.Generic;
using Godot;

public static class ItemTraitContentValidator
{
    public static List<string> Validate(
        IReadOnlyDictionary<StringName, ItemDef> itemDefs,
        IReadOnlyDictionary<StringName, TraitDef> traitDefs,
        string contextPath = "item_defs")
    {
        var errors = new List<string>();
        if (itemDefs == null)
            return errors;
        foreach (var kv in itemDefs)
        {
            ItemDef itemDef = kv.Value;
            if (itemDef == null)
            {
                errors.Add($"{contextPath}.{kv.Key} is null.");
                continue;
            }
            ValidateItem(itemDef, traitDefs, $"{contextPath}.{itemDef.item_id}", errors);
        }
        return errors;
    }
}
```

Complete `ValidateItem` with these exact rules:

- Skip non-equipment items unless they declare trait fields; non-equipment trait declarations are errors.
- Each `trait_ids` entry must be non-empty, known in `traitDefs`, and allow `TraitSourceKind.EquipmentFixed`.
- Each `trait_roll_groups` entry must have non-empty `group_id`.
- Each group must have `roll_count >= 1`.
- Each entry must be non-null, have non-empty known `trait_id`, `weight > 0`, and the referenced trait must allow `TraitSourceKind.EquipmentRoll`.
- For each group, calculate maximum satisfiable hits by counting one entry for each non-empty `exclusive_group` plus every entry with empty `exclusive_group`; reject if `roll_count > maxSatisfiableHits`.
- Call `TraitRollValueSchemaEntry.AppendSchemaErrors` for every referenced roll trait schema entry and append returned errors with the item/group context.

- [ ] **Step 7: Wire validation runner**

In `tests/runtime/validation/ContentValidationRunner.cs`, update `ValidateItemDirectories` to optionally accept trait defs:

```csharp
public static ValidationDomainResult ValidateItemDirectories(
    string label,
    string[] itemDirectories,
    string[] templateDirectories = null,
    GDictionary skillDefs = null,
    GDictionary traitDefs = null)
```

After building `combinedErrors`, append:

```csharp
if (traitDefs != null && traitDefs.Count > 0)
{
    AppendUniqueErrors(
        combinedErrors,
        ItemTraitContentValidator.Validate(
            registry.GetItemDefsTyped(),
            BuildTraitDefIndex(traitDefs),
            label
        )
    );
}
```

Add `BuildTraitDefIndex(GDictionary traitDefs)` mirroring existing typed index helpers: accept only `TraitDef` values and `StringName` keys.

In `ValidateOfficialItemContent`, load the official progression registry or pass official trait defs from the caller. Use the least invasive approach already used in this runner.

- [ ] **Step 8: Run and verify GREEN**

Run:

```bash
dotnet build magic.csproj
godot --headless -s res://tests/runtime/validation/run_item_recipe_registry_typed_regression.cs
godot --headless -s res://tests/runtime/validation/run_resource_validation_regression.cs
```

Expected: build PASS; item/recipe typed regression PASS; resource validation PASS with official content errors `0`.

- [ ] **Step 9: Record progress**

Update `.git/sdd/progress.md` if using SDD. Do not commit unless explicitly requested.

---

### Task 5: Equipment Trait Roll Service

**Files:**
- Create: `scripts/systems/inventory/EquipmentTraitRollService.cs`
- Modify: `tests/equipment/run_equipment_trait_roll_regression.cs`

**Interfaces:**
- Consumes: `ItemDef.trait_roll_groups`, `TraitDef.roll_value_schema`, `TraitInstanceState.Create`.
- Produces: `EquipmentTraitRollService.MintWithRolls(EquipmentInstanceState, ItemDef)`.
- Produces: `EquipmentTraitRollService.ValidateRehydrated(EquipmentInstanceState)`.
- Produces: `EquipmentTraitRollService.SetRollHooksForTesting(Func<int, int, int>, Func<float>)`.

- [ ] **Step 1: Write failing roll service regression**

Create `tests/equipment/run_equipment_trait_roll_regression.cs`:

```csharp
using System.Collections.Generic;
using Godot;

public partial class run_equipment_trait_roll_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestMintRequiresStableInstanceId();
        TestMintRollsWeightedEntriesAndRollValues();
        TestDuplicateAndValidateRehydratedConsumeNoRng();
        Quit(_test.Finish("Equipment trait roll regression"));
    }

    private void TestMintRequiresStableInstanceId()
    {
        EquipmentTraitRollService service = BuildService();
        EquipmentInstanceState instance = EquipmentInstanceState.CreateTransientInstance("iron_sword");
        ItemDef item = BuildItem();

        service.MintWithRolls(instance, item);

        _test.Eq(instance.trait_instances.Count, 0, "MintWithRolls should reject empty instance_id.");
    }

    private void TestMintRollsWeightedEntriesAndRollValues()
    {
        EquipmentTraitRollService service = BuildService();
        FixedRolls rolls = new(
            rangeRolls: new[] { 5 },
            unitRolls: new[] { 0.0f }
        );
        service.SetRollHooksForTesting(rolls.RollRange, rolls.RollUnit);

        EquipmentInstanceState instance = EquipmentInstanceState.CreateInstance("iron_sword", "eq_000001");
        service.MintWithRolls(instance, BuildItem());

        _test.Eq(instance.trait_instances.Count, 1, "roll_count=1 should mint one equipment_roll trait.");
        TraitInstanceState trait = instance.trait_instances[0];
        _test.Eq(trait.trait_instance_id, new StringName("eq_000001_t01"), "trait instance id should derive from stable equipment id.");
        _test.Eq(trait.trait_id, new StringName("sharp_edge"), "weighted pick should select the first entry for unit roll 0.");
        _test.Eq(trait.source_type, new StringName("equipment_roll"), "minted trait source should be equipment_roll.");
        _test.Eq(trait.source_id, new StringName("eq_000001"), "minted trait source_id should be equipment instance id.");
        _test.Eq(trait.GetIntRoll("amount", -1), 5, "int roll value should use injected range roll.");
    }

    private void TestDuplicateAndValidateRehydratedConsumeNoRng()
    {
        EquipmentTraitRollService service = BuildService();
        FixedRolls rolls = new(
            rangeRolls: new[] { 4 },
            unitRolls: new[] { 0.0f }
        );
        service.SetRollHooksForTesting(rolls.RollRange, rolls.RollUnit);

        EquipmentInstanceState instance = EquipmentInstanceState.CreateInstance("iron_sword", "eq_000001");
        service.MintWithRolls(instance, BuildItem());
        EquipmentInstanceState copy = instance.DuplicateState();

        _test.True(service.ValidateRehydrated(copy), "rehydrated copy should validate existing equipment_roll trait instances.");
        _test.Eq(rolls.RangeCalls, 1, "ValidateRehydrated should not consume range RNG.");
        _test.Eq(rolls.UnitCalls, 1, "ValidateRehydrated should not consume unit RNG.");
        _test.Eq(copy.trait_instances[0].GetIntRoll("amount", -1), 4, "DuplicateState should preserve roll_values.");
    }
}
```

Add local helpers in the same test file:

- `BuildService()` returns an `EquipmentTraitRollService` with a dictionary containing `sharp_edge` and `heavy_head`.
- `BuildItem()` returns equipment item `iron_sword` with one group `prefix`, `roll_count = 1`, entries `sharp_edge` and `heavy_head`, each weight `1`.
- `FixedRolls` tracks `RangeCalls` and `UnitCalls` and returns queued values.

- [ ] **Step 2: Run test to verify RED**

Run:

```bash
dotnet build magic.csproj
```

Expected: FAIL because `EquipmentTraitRollService` does not exist.

- [ ] **Step 3: Implement roll service**

Create `scripts/systems/inventory/EquipmentTraitRollService.cs`:

```csharp
using System;
using System.Collections.Generic;
using Godot;

public class EquipmentTraitRollService
{
    private readonly IReadOnlyDictionary<StringName, TraitDef> _traitDefs;
    private RandomNumberGenerator _rng;
    private Func<int, int, int> _rollRange;
    private Func<float> _rollUnit;

    public EquipmentTraitRollService(
        IReadOnlyDictionary<StringName, TraitDef> traitDefs,
        RandomNumberGenerator rng = null)
    {
        _traitDefs = traitDefs ?? new Dictionary<StringName, TraitDef>();
        ConfigureRng(rng);
    }
}
```

Complete the class with:

- `ConfigureRng`: use provided RNG or create/randomize one, set `_rollRange = _rng.RandiRange`, `_rollUnit = _rng.Randf`.
- `SetRollHooksForTesting`: override hooks when non-null.
- `MintWithRolls`: return without mutation if instance/item null, if `instance.instance_id == ""`, or if item has no roll groups; clear and repopulate `instance.trait_instances` only after instance id is stable.
- `_rollGroup`: weighted no-replacement pick, remove other entries with matching non-empty `exclusive_group`, reject impossible groups by returning an empty hit list and logging.
- `_weightedPick`: calculate total weight and use `_rollUnit()` to pick `[0,total)`.
- `_rollValuesFor`: for each roll schema entry, roll int range, pick allowed string_name by index, or roll bool from `0/1`.
- `ValidateRehydrated`: return false if any trait instance is null, not `EquipmentRoll`, unknown trait id, or `ValidateAgainstDef(def)` returns an error.

- [ ] **Step 4: Run and verify GREEN**

Run:

```bash
dotnet build magic.csproj
godot --headless -s res://tests/equipment/run_equipment_trait_roll_regression.cs
```

Expected: build PASS; equipment trait roll regression PASS.

- [ ] **Step 5: Record progress**

Update `.git/sdd/progress.md` if using SDD. Do not commit unless explicitly requested.

---

### Task 6: Wire Minting Into Warehouse Creation Paths

**Files:**
- Modify: `scripts/systems/inventory/PartyWarehouseService.cs`
- Modify: `scripts/systems/game_runtime/GameRuntimeFacade.cs`
- Modify: `scripts/systems/game_runtime/GameRuntimeBattleLootCommitService.cs`
- Modify: `tests/equipment/run_equipment_drop_service_regression.cs`
- Modify: `tests/warehouse/run_party_warehouse_batch_swap_regression.cs`
- Test: `tests/equipment/run_equipment_trait_roll_regression.cs`

**Interfaces:**
- Consumes: `EquipmentTraitRollService` from Task 5.
- Produces: warehouse add paths mint equipment-roll traits only when assigning a new stable equipment instance id.
- Preserves: `EquipmentDropService.RollItemInstances` returns typed transient instances without stable trait ids.

- [ ] **Step 1: Write failing warehouse mint assertions**

Extend `tests/equipment/run_equipment_trait_roll_regression.cs` with:

```csharp
private void TestWarehouseAddItemMintsAfterStableInstanceId()
{
    EquipmentTraitRollService rollService = BuildService();
    FixedRolls rolls = new(new[] { 3 }, new[] { 0.0f });
    rollService.SetRollHooksForTesting(rolls.RollRange, rolls.RollUnit);

    PartyState party = BuildPartyWithCapacity(2);
    PartyWarehouseService warehouse = new();
    warehouse.Setup(
        party,
        new Dictionary<StringName, ItemDef> { ["iron_sword"] = BuildItem() },
        () => "eq_000777",
        rollService
    );

    var result = warehouse.AddItemTyped("iron_sword", 1);
    EquipmentInstanceState stored = party.warehouse_state.GetNonEmptyEquipmentInstancesTyped()[0];

    _test.Eq(result.AddedQuantity, 1, "warehouse AddItemTyped should add equipment.");
    _test.Eq(stored.instance_id, new StringName("eq_000777"), "warehouse allocator should assign stable id.");
    _test.Eq(stored.trait_instances.Count, 1, "warehouse AddItemTyped should mint equipment traits after stable id.");
    _test.Eq(stored.trait_instances[0].trait_instance_id, new StringName("eq_000777_t01"), "minted trait id should derive from stable warehouse id.");
}

private void TestWarehouseDepositingExistingInstanceDoesNotReroll()
{
    EquipmentTraitRollService rollService = BuildService();
    FixedRolls rolls = new(new[] { 3 }, new[] { 0.0f });
    rollService.SetRollHooksForTesting(rolls.RollRange, rolls.RollUnit);

    EquipmentInstanceState existing = EquipmentInstanceState.CreateInstance("iron_sword", "eq_existing");
    existing.trait_instances.Add(TraitInstanceState.Create(
        "eq_existing_t01",
        "sharp_edge",
        TraitSourceKind.EquipmentRoll,
        "eq_existing",
        rollValues: new Godot.Collections.Dictionary { ["amount"] = 6 }
    ));

    PartyState party = BuildPartyWithCapacity(2);
    PartyWarehouseService warehouse = new();
    warehouse.Setup(
        party,
        new Dictionary<StringName, ItemDef> { ["iron_sword"] = BuildItem() },
        () => "eq_unused",
        rollService
    );

    var result = warehouse.AddEquipmentInstanceTyped(existing);
    EquipmentInstanceState stored = party.warehouse_state.GetNonEmptyEquipmentInstancesTyped()[0];

    _test.Eq(result.AddedQuantity, 1, "existing equipment instance should deposit.");
    _test.Eq(stored.instance_id, new StringName("eq_existing"), "existing id should be preserved.");
    _test.Eq(stored.trait_instances[0].GetIntRoll("amount", -1), 6, "existing trait roll_values should be preserved.");
    _test.Eq(rolls.RangeCalls, 0, "depositing existing instance should not consume range RNG.");
    _test.Eq(rolls.UnitCalls, 0, "depositing existing instance should not consume unit RNG.");
}
```

Add `BuildPartyWithCapacity(int capacity)` helper by constructing a member with `UnitBaseAttributes.storage_space = capacity`, following the pattern already used by warehouse tests.

In `tests/equipment/run_equipment_drop_service_regression.cs`, add:

```csharp
_test.Eq(instances[0].trait_instances.Count, 0, "EquipmentDropService should leave transient trait rolling to stable warehouse id assignment.");
```

- [ ] **Step 2: Run tests to verify RED**

Run:

```bash
dotnet build magic.csproj
godot --headless -s res://tests/equipment/run_equipment_trait_roll_regression.cs
```

Expected: build FAIL if `PartyWarehouseService.Setup(..., EquipmentTraitRollService)` does not exist, or test FAIL because warehouse add does not mint traits.

- [ ] **Step 3: Extend `PartyWarehouseService.Setup`**

In `scripts/systems/inventory/PartyWarehouseService.cs`:

- Add field:

```csharp
private EquipmentTraitRollService _equipment_trait_roll_service;
```

- Extend both setup overloads that accept item defs:

```csharp
public void Setup(
    PartyState partyState,
    IReadOnlyDictionary<StringName, ItemDef> itemDefs,
    Func<StringName> equipmentInstanceIdAllocator = null,
    EquipmentTraitRollService equipmentTraitRollService = null)
```

Assign `_equipment_trait_roll_service = equipmentTraitRollService;`.

- Do the same for `SetupPartyBackpackView`.

- [ ] **Step 4: Mint only after new stable id allocation**

In `_create_equipment_instance`, after assigning a real id when `consumeAllocator == true`, call:

```csharp
if (consumeAllocator && _equipment_trait_roll_service != null)
{
    ItemDef itemDef = GetItemDef(itemId);
    _equipment_trait_roll_service.MintWithRolls(instance, itemDef);
}
```

In `AddEquipmentInstanceTyped`, track whether the method allocated a new id for a previously transient instance:

```csharp
bool allocatedNewStableId = false;
if (forceNewInstanceId || instance.instance_id == "")
{
    allocatedInstanceId = _allocate_equipment_instance_id(warehouseState);
    instance.instance_id = allocatedInstanceId;
    allocatedNewStableId = allocatedInstanceId != "";
    ...
}
if (allocatedNewStableId && _equipment_trait_roll_service != null)
    _equipment_trait_roll_service.MintWithRolls(instance, itemDef);
```

Do not mint when `instance.instance_id` was already non-empty.

- [ ] **Step 5: Wire runtime service setup**

In `scripts/systems/game_runtime/GameRuntimeFacade.cs`, when setting up `_party_warehouse_service`, create:

```csharp
var equipmentTraitRollService = new EquipmentTraitRollService(
    _game_session.GetTraitDefsTyped()
);
```

Pass it into `PartyWarehouseService.Setup(...)`. Reuse the same service for the active runtime facade; do not create one inside `EquipmentInstanceState`.

If tests construct `PartyWarehouseService` directly and do not pass this service, behavior remains old behavior with no trait minting.

- [ ] **Step 6: Keep battle loot transient behavior**

In `scripts/systems/game_runtime/GameRuntimeBattleLootCommitService.cs`, keep using `equipmentDropService.RollItemInstances(...)` for rarity and quantity. Do not mint there before warehouse assignment unless a stable id is already assigned. The expected Phase 2 path is:

```text
EquipmentDropService.RollItemInstances -> transient equipment instance with rarity -> PartyWarehouseService.AddEquipmentInstanceTyped -> stable id allocation -> EquipmentTraitRollService.MintWithRolls
```

- [ ] **Step 7: Run and verify GREEN**

Run:

```bash
dotnet build magic.csproj
godot --headless -s res://tests/equipment/run_equipment_trait_roll_regression.cs
godot --headless -s res://tests/equipment/run_equipment_drop_service_regression.cs
godot --headless -s res://tests/warehouse/run_party_warehouse_batch_swap_regression.cs
```

Expected: build PASS; roll regression PASS; drop service PASS with transient trait count `0`; batch swap PASS and preserving existing trait instances.

- [ ] **Step 8: Record progress**

Update `.git/sdd/progress.md` if using SDD. Do not commit unless explicitly requested.

---

### Task 7: Final Validation Matrix And Context Notes

**Files:**
- Modify: `docs/design/project_context_units.md` only if final code changes ownership/read-set notes.
- Test: all focused runners listed below.

**Interfaces:**
- Consumes: all tasks above.
- Produces: verified Phase 2 state/equipment layer with no battle runtime migration.

- [ ] **Step 1: Update context units if ownership changed**

If the final implementation adds new durable read-set responsibilities, update:

- CU-10 to mention `TraitRollGroupDef`, `TraitRollGroupEntryDef`, `ItemTraitContentValidator`, and `EquipmentTraitRollService` as equipment trait roll content/runtime boundaries.
- CU-11 to mention `TraitInstanceState` as party member persisted character-trait state.

Do not put regression inventories or field-level migration notes into `docs/design/project_context_units.md`.

- [ ] **Step 2: Run focused schema/content tests**

Run:

```bash
dotnet build magic.csproj
godot --headless -s res://tests/progression/schema/run_trait_instance_state_schema_regression.cs
godot --headless -s res://tests/progression/schema/run_trait_content_rules_regression.cs
godot --headless -s res://tests/progression/identity/run_trait_content_registry_regression.cs
godot --headless -s res://tests/runtime/validation/run_progression_content_registry_typed_regression.cs
godot --headless -s res://tests/runtime/validation/run_item_recipe_registry_typed_regression.cs
godot --headless -s res://tests/runtime/validation/run_resource_validation_regression.cs
```

Expected: all PASS; official content validation errors remain `0`.

- [ ] **Step 3: Run focused state/equipment tests**

Run:

```bash
godot --headless -s res://tests/equipment/run_equipment_trait_roll_regression.cs
godot --headless -s res://tests/equipment/run_equipment_drop_service_regression.cs
godot --headless -s res://tests/equipment/run_party_equipment_regression.cs
godot --headless -s res://tests/warehouse/run_party_warehouse_batch_swap_regression.cs
godot --headless -s res://tests/warehouse/run_warehouse_state_item_validator_regression.cs
godot --headless -s res://tests/progression/core/run_party_state_duplicate_regression.cs
```

Expected: all PASS; trait instances are preserved through duplicate, warehouse, equipment, and save-shaped round trips.

- [ ] **Step 4: Run focused persistence tests**

Run:

```bash
godot --headless -s res://tests/runtime/persistence/run_save_serializer_quest_round_trip_regression.cs
godot --headless -s res://tests/runtime/persistence/run_invalid_save_graceful_regression.cs
godot --headless -s res://tests/battle_runtime/runtime/run_save_index_resilience_regression.cs
```

Expected: all PASS; top-level save version is `8`, `PartyState.version` is `4`, and save index version remains `3`.

- [ ] **Step 5: Run diff hygiene**

Run:

```bash
git diff --check -- \
  scripts/player/progression/TraitInstanceState.cs \
  scripts/player/progression/TraitInstanceCollection.cs \
  scripts/player/progression/PartyMemberState.cs \
  scripts/player/progression/PartyState.cs \
  scripts/player/warehouse/TraitRollGroupDef.cs \
  scripts/player/warehouse/TraitRollGroupEntryDef.cs \
  scripts/player/warehouse/ItemTraitContentValidator.cs \
  scripts/player/warehouse/ItemDef.cs \
  scripts/player/warehouse/ItemContentRegistry.cs \
  scripts/player/warehouse/EquipmentInstanceState.cs \
  scripts/systems/inventory/EquipmentTraitRollService.cs \
  scripts/systems/inventory/PartyWarehouseService.cs \
  scripts/systems/game_runtime/GameRuntimeFacade.cs \
  scripts/systems/game_runtime/GameRuntimeBattleLootCommitService.cs \
  scripts/systems/persistence/GameSession.cs \
  scripts/systems/persistence/SaveSerializer.cs \
  tests/progression/schema/run_trait_instance_state_schema_regression.cs \
  tests/equipment/run_equipment_trait_roll_regression.cs
```

Expected: no whitespace errors.

- [ ] **Step 6: Record final progress**

Update `.git/sdd/progress.md` if using SDD. Do not commit unless explicitly requested.

---

## Self-Review Notes

- Spec coverage: Tasks 1-2 cover `TraitInstanceState`, host serialization, strict source-kind ownership, and deep copies. Task 3 covers save version `8`, party version `4`, and save index version `3`. Tasks 4-6 cover `ItemDef.trait_ids`, `trait_roll_groups`, item validation, template merge, and RNG-only minting after stable instance ids. Task 7 covers focused verification and project context notes.
- Out of scope by design: `CharacterTraitService`, `EffectiveTraitSet`, battle effective payloads, `TraitTriggerHooks`, battle simulations, and `RaceTraitDef` deletion.
- Dictionary boundary: `Godot.Collections.Dictionary` remains limited to strict payload/export/test fixtures; service/runtime access uses typed fields, typed collections, and typed readers.
- Compatibility decision: old save payloads missing `trait_instances` remain invalid because strict field sets and version checks reject them.
