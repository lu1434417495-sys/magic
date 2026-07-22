# Character Trait System Phase 3 Aggregation And Attributes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the Phase 3 runtime aggregation layer for generic traits and feed trait-derived attribute modifiers into the existing attribute pipeline.

**Architecture:** Add plain C# runtime DTOs (`EffectiveTraitInstance`, `EffectiveTraitSet`) plus a focused `CharacterTraitService` that aggregates identity, character, fixed equipment, and equipment-roll traits. Keep `AttributeService` ignorant of trait catalogs by passing pre-resolved `trait_attribute_modifiers` through `AttributeSourceContext`, matching the existing equipment/passive/temporary modifier pattern.

**Tech Stack:** Godot 4.6 C#, Godot `StringName` resource boundaries, plain CLR `List<T>`/`Dictionary<TKey,TValue>` for runtime indexes, focused Godot headless regression runners.

## Global Constraints

- No compatibility logic, legacy aliases, fallback migrations, or old payload/schema support.
- Phase 3 must not change `BattleUnitState`, `TraitTriggerHooks`, battle save schema, charge lifecycle, or AI mutation guard. Those are Phase 4.
- `TraitDef` is the content definition truth source; `TraitInstanceState` is the persisted instance truth source.
- `EffectiveTraitSet` / `EffectiveTraitInstance` are runtime aggregation results only; they are not party/equipment save facts.
- `effective_trait_ids` is a derived query projection only and must not become a fact source.
- Character permanent traits and equipment instance traits remain stored separately.
- Equipment fixed traits come from `ItemDef.trait_ids`; equipment roll traits come from `EquipmentInstanceState.trait_instances`.
- `equipment_fixed` raw keys must use equipment instance id, not item id.
- `unique_by_trait`, `highest_roll`, and `additive` collapse to final `effective_instance_key == trait_id`; `stack_by_instance` keeps per-instance raw keys.
- `highest_roll` uses `TraitDef.GetHighestRollCompareKey()`.
- Attribute modifiers from trait definitions are pre-resolved by `CharacterTraitService`; `AttributeService` must not query trait catalogs, item defs, equipment instances, or character state.
- `roll_values` remain accessed through `TraitInstanceState` typed helpers and `ValidateAgainstDef(TraitDef)`.
- Do not run battle simulation or balance runners for this phase.
- Do not commit unless the user explicitly requests it.

---

### Task 1: EffectiveTrait DTOs And Battle Payload Projection

**Files:**
- Create: `scripts/systems/progression/EffectiveTrait.cs`
- Test: `tests/progression/core/run_effective_trait_set_regression.cs`

**Interfaces:**
- Consumes: `TraitDef`, `TraitInstanceState`, `TraitContentRules.ToStringName(TraitSourceKind)`.
- Produces: `EffectiveTraitInstance`, `EffectiveTraitSet`, `EffectiveTraitSet.DeriveTraitIds()`, `EffectiveTraitSet.ToBattlePayload()`.

- [ ] **Step 1: Write the failing DTO regression**

Create `tests/progression/core/run_effective_trait_set_regression.cs` with a runner that constructs two `EffectiveTraitInstance` objects out of order, asserts sorted derived trait ids, keyed lookup, trait lookup, and sorted battle payload:

```csharp
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_effective_trait_set_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestTraitSetIndexesAndSortedPayload();
        GodotSharpCleanup.CollectPendingFinalizers();
        Quit(_test.Finish("Effective trait set regression"));
    }

    private void TestTraitSetIndexesAndSortedPayload()
    {
        TraitDef sharp = BuildTrait("sharp_edge", "equipment_roll");
        TraitDef guarded = BuildTrait("guarded_grip", "equipment_fixed");
        EffectiveTraitSet set = new(new System.Collections.Generic.List<EffectiveTraitInstance>
        {
            new()
            {
                TraitId = "sharp_edge",
                TraitDef = sharp,
                SourceKind = TraitSourceKind.EquipmentRoll,
                SourceId = "eq_000002",
                EffectiveInstanceKey = "eq_000002_t01",
                StackPolicy = TraitStackPolicyKind.StackByInstance,
                ChargeScope = TraitChargeScopeKind.PerTurn,
                ChargeResetTiming = TraitChargeResetTimingKind.TurnStart,
                Rank = 2,
                Stacks = 1,
                RollValues = new GDictionary { ["amount"] = 4 },
            },
            new()
            {
                TraitId = "guarded_grip",
                TraitDef = guarded,
                SourceKind = TraitSourceKind.EquipmentFixed,
                SourceId = "eq_000001",
                EffectiveInstanceKey = "guarded_grip",
                StackPolicy = TraitStackPolicyKind.UniqueByTrait,
                ChargeScope = TraitChargeScopeKind.None,
                ChargeResetTiming = TraitChargeResetTimingKind.None,
                Rank = 1,
                Stacks = 1,
                RollValues = new GDictionary(),
            },
        });

        _test.True(set.TryGetByKey("eq_000002_t01", out EffectiveTraitInstance byKey), "Trait set should index by effective key.");
        _test.Eq(byKey.TraitId, new StringName("sharp_edge"), "Key lookup should return matching trait.");
        _test.Eq(set.GetByTraitId("sharp_edge").Count, 1, "Trait id lookup should return matching instances.");

        var ids = set.DeriveTraitIds();
        _test.Eq(ids.Count, 2, "Derived ids should be unique.");
        _test.Eq(ids[0], new StringName("guarded_grip"), "Derived ids should sort by ordinal text.");
        _test.Eq(ids[1], new StringName("sharp_edge"), "Derived ids should sort by ordinal text.");

        GArray payload = set.ToBattlePayload();
        _test.Eq(payload.Count, 2, "Battle payload should include both instances.");
        GDictionary first = payload[0].AsGodotDictionary();
        GDictionary second = payload[1].AsGodotDictionary();
        _test.Eq(DictString(first, "effective_instance_key"), "eq_000002_t01", "Battle payload should sort by effective key.");
        _test.Eq(DictString(second, "effective_instance_key"), "guarded_grip", "Battle payload should sort by effective key.");
        _test.Eq(DictString(first, "effect_type"), "attribute_modifier", "Payload should denormalize effect type.");
        _test.Eq(DictString(first, "source_type"), "equipment_roll", "Payload should project source kind.");
        _test.Eq(first["roll_values"].AsGodotDictionary()["amount"].AsInt32(), 4, "Payload should duplicate roll values.");
    }

    private static TraitDef BuildTrait(string traitId, string sourceKind) =>
        new()
        {
            trait_id = traitId,
            display_name = traitId,
            description = traitId,
            allowed_source_kinds = new Godot.Collections.Array<StringName> { sourceKind },
            effect_type = "attribute_modifier",
            trigger_type = "passive",
            stack_policy = sourceKind == "equipment_roll" ? "stack_by_instance" : "unique_by_trait",
            charge_scope = sourceKind == "equipment_roll" ? "per_turn" : "none",
            charge_reset_timing = sourceKind == "equipment_roll" ? "turn_start" : "none",
        };

    private static string DictString(GDictionary dictionary, string key) =>
        dictionary != null && dictionary.ContainsKey(key) ? dictionary[key].AsString() : "";
}
```

- [ ] **Step 2: Run the DTO test to verify RED**

Run:

```bash
dotnet build magic.csproj
```

Expected: build FAIL because `EffectiveTraitSet` and `EffectiveTraitInstance` do not exist.

- [ ] **Step 3: Implement `EffectiveTrait.cs`**

Create `scripts/systems/progression/EffectiveTrait.cs`:

```csharp
using System.Collections.Generic;
using Godot;

internal sealed class EffectiveTraitInstance
{
    public StringName TraitId;
    public TraitDef TraitDef;
    public TraitInstanceState TraitInstance;
    public TraitSourceKind SourceKind;
    public StringName SourceId;
    public StringName EffectiveInstanceKey;
    public TraitStackPolicyKind StackPolicy;
    public TraitChargeScopeKind ChargeScope;
    public TraitChargeResetTimingKind ChargeResetTiming;
    public int Rank = 1;
    public int Stacks = 1;
    public Godot.Collections.Dictionary RollValues = new();
}

internal sealed class EffectiveTraitSet
{
    private readonly List<EffectiveTraitInstance> _instances;
    private readonly Dictionary<StringName, EffectiveTraitInstance> _byKey;
    private readonly Dictionary<StringName, List<EffectiveTraitInstance>> _byTrait;

    public EffectiveTraitSet(List<EffectiveTraitInstance> instances)
    {
        _instances = instances ?? new List<EffectiveTraitInstance>();
        _byKey = new Dictionary<StringName, EffectiveTraitInstance>();
        _byTrait = new Dictionary<StringName, List<EffectiveTraitInstance>>();
        foreach (EffectiveTraitInstance instance in _instances)
        {
            if (instance == null || instance.TraitId == "" || instance.EffectiveInstanceKey == "")
                continue;
            _byKey[instance.EffectiveInstanceKey] = instance;
            if (!_byTrait.TryGetValue(instance.TraitId, out List<EffectiveTraitInstance> list))
            {
                list = new List<EffectiveTraitInstance>();
                _byTrait[instance.TraitId] = list;
            }
            list.Add(instance);
        }
    }

    public IReadOnlyList<EffectiveTraitInstance> Instances => _instances;

    public bool TryGetByKey(StringName key, out EffectiveTraitInstance instance) =>
        _byKey.TryGetValue(key, out instance);

    public IReadOnlyList<EffectiveTraitInstance> GetByTraitId(StringName traitId) =>
        _byTrait.TryGetValue(traitId, out List<EffectiveTraitInstance> list)
            ? list
            : System.Array.Empty<EffectiveTraitInstance>();

    public IReadOnlyList<StringName> DeriveTraitIds()
    {
        List<StringName> ids = new(_byTrait.Keys);
        ids.Sort((a, b) => string.CompareOrdinal(a.ToString(), b.ToString()));
        return ids;
    }

    public Godot.Collections.Array ToBattlePayload()
    {
        List<EffectiveTraitInstance> sorted = new(_instances);
        sorted.Sort((a, b) => string.CompareOrdinal(
            a?.EffectiveInstanceKey.ToString() ?? "",
            b?.EffectiveInstanceKey.ToString() ?? ""
        ));
        Godot.Collections.Array result = new();
        foreach (EffectiveTraitInstance instance in sorted)
        {
            if (instance == null || instance.TraitDef == null)
                continue;
            result.Add(new Godot.Collections.Dictionary
            {
                { "trait_id", (string)instance.TraitId },
                { "effective_instance_key", (string)instance.EffectiveInstanceKey },
                { "source_type", (string)TraitContentRules.ToStringName(instance.SourceKind) },
                { "source_id", (string)instance.SourceId },
                { "effect_type", (string)instance.TraitDef.effect_type },
                { "trigger_type", (string)instance.TraitDef.trigger_type },
                { "charge_scope", (string)instance.TraitDef.charge_scope },
                { "charge_reset_timing", (string)instance.TraitDef.charge_reset_timing },
                { "rank", Mathf.Max(instance.Rank, 1) },
                { "stacks", Mathf.Max(instance.Stacks, 1) },
                {
                    "roll_values",
                    instance.RollValues != null
                        ? instance.RollValues.Duplicate(true)
                        : new Godot.Collections.Dictionary()
                },
            });
        }
        return result;
    }
}
```

- [ ] **Step 4: Run the DTO test to verify GREEN**

Run:

```bash
dotnet build magic.csproj
godot --headless -s res://tests/progression/core/run_effective_trait_set_regression.cs
```

Expected: build PASS and `Effective trait set regression: PASS`.

---

### Task 2: CharacterTraitService Aggregation And Stack Policies

**Files:**
- Create: `scripts/systems/progression/CharacterTraitService.cs`
- Test: `tests/progression/core/run_character_trait_service_regression.cs`

**Interfaces:**
- Consumes: `EffectiveTraitSet`, `PartyMemberState.trait_instances`, `EquipmentState.GetEntrySlotIdsTyped()`, `EquipmentEntryState.equipment_instance`, `ItemDef.GetTraitIdsTyped()`, `TraitDef.GetHighestRollCompareKey()`.
- Produces: `CharacterTraitService.ICharacterTraitGateway`, `BuildEffectiveTraits(StringName, EquipmentState)`, `ResolveTraitAttributeModifiers(EffectiveTraitSet)`.

- [ ] **Step 1: Write the failing aggregation regression**

Create `tests/progression/core/run_character_trait_service_regression.cs` with a fake gateway and tests for:

```csharp
private void Run()
{
    TestAggregatesIdentityCharacterAndEquipmentSources();
    TestStackPoliciesCollapseOrPreserveKeys();
    TestTraitAttributeModifiersUseEffectiveSourceKeys();
    GodotSharpCleanup.CollectPendingFinalizers();
    Quit(_test.Finish("Character trait service regression"));
}
```

Use helper data:

```csharp
private static Dictionary<StringName, TraitDef> BuildTraitDefs() => new()
{
    ["identity_watch"] = BuildTrait("identity_watch", "identity", "unique_by_trait"),
    ["character_grit"] = BuildTrait("character_grit", "character", "unique_by_trait"),
    ["fixed_guard"] = BuildTrait("fixed_guard", "equipment_fixed", "unique_by_trait"),
    ["sharp_edge"] = BuildRollTrait("sharp_edge", "stack_by_instance", compareKey: "amount"),
    ["lucky_roll"] = BuildRollTrait("lucky_roll", "highest_roll", compareKey: "amount"),
    ["additive_power"] = BuildTrait("additive_power", "character", "additive", attributeId: "attack_bonus", value: 2),
};
```

Assertions:

```csharp
EffectiveTraitSet set = service.BuildEffectiveTraits("hero");
_test.True(set.TryGetByKey("identity_watch", out _), "unique identity trait should collapse to trait_id key.");
_test.True(set.TryGetByKey("character_grit", out _), "character unique trait should collapse to trait_id key.");
_test.True(set.TryGetByKey("fixed_guard", out _), "equipment fixed unique trait should collapse to trait_id key.");
_test.True(set.TryGetByKey("eq_000001_t01", out _), "stack_by_instance equipment roll should keep trait instance id key.");
_test.Eq(set.GetByTraitId("lucky_roll")[0].TraitInstance.GetIntRoll("amount", -1), 6, "highest_roll should keep the highest roll value.");
_test.Eq(set.GetByTraitId("additive_power")[0].Stacks, 2, "additive should sum stacks into one effective instance.");
```

Do not assert raw keys for collapsed policies; only `stack_by_instance` retains raw per-instance keys.

Attribute assertions:

```csharp
List<AttributeModifier> modifiers = service.ResolveTraitAttributeModifiers(set);
AttributeModifier attack = FindModifier(modifiers, "attack_bonus");
_test.Eq(attack.value, 4, "additive trait modifier should multiply base value by effective stacks.");
_test.Eq(attack.source_type, new StringName("trait_character"), "character trait modifier should use trait source type.");
_test.Eq(attack.source_id, new StringName("additive_power"), "collapsed modifier source_id should be final effective key.");
```

- [ ] **Step 2: Run aggregation test to verify RED**

Run:

```bash
dotnet build magic.csproj
```

Expected: build FAIL because `CharacterTraitService` does not exist.

- [ ] **Step 3: Implement `CharacterTraitService.cs`**

Create `scripts/systems/progression/CharacterTraitService.cs` with:

```csharp
using System.Collections.Generic;
using Godot;

internal sealed class CharacterTraitService
{
    public interface ICharacterTraitGateway
    {
        RaceDef GetRaceDefForTraitAggregation(StringName memberId);
        SubraceDef GetSubraceDefForTraitAggregation(StringName memberId);
        BloodlineDef GetBloodlineDefForTraitAggregation(StringName memberId);
        BloodlineStageDef GetBloodlineStageDefForTraitAggregation(StringName memberId);
        AscensionDef GetAscensionDefForTraitAggregation(StringName memberId);
        AscensionStageDef GetAscensionStageDefForTraitAggregation(StringName memberId);
        PartyMemberState GetMemberStateForTraitAggregation(StringName memberId);
        EquipmentState GetEquipmentStateForTraitAggregation(StringName memberId);
        ItemDef GetItemDefForTraitAggregation(StringName itemId);
    }

    private readonly IReadOnlyDictionary<StringName, TraitDef> _traitDefs;
    private readonly ICharacterTraitGateway _gateway;

    public CharacterTraitService(
        IReadOnlyDictionary<StringName, TraitDef> traitDefs,
        ICharacterTraitGateway gateway
    )
    {
        _traitDefs = traitDefs ?? new Dictionary<StringName, TraitDef>();
        _gateway = gateway;
    }

    public EffectiveTraitSet BuildEffectiveTraits(StringName memberId, EquipmentState equipmentOverride = null)
    {
        List<EffectiveTraitInstance> raw = new();
        CollectIdentity(memberId, raw);
        CollectCharacter(memberId, raw);
        CollectEquipment(memberId, equipmentOverride, raw);
        return new EffectiveTraitSet(ApplyStackPolicies(raw));
    }

    public List<AttributeModifier> ResolveTraitAttributeModifiers(EffectiveTraitSet set)
    {
        List<AttributeModifier> result = new();
        if (set == null)
            return result;
        foreach (EffectiveTraitInstance instance in set.Instances)
            AppendAttributeModifiers(result, instance);
        return result;
    }
}
```

Complete the helper methods exactly according to the design:

- `CollectIdentity` reads race/subrace unless `AscensionDef.suppresses_original_race_traits` is true, then bloodline, bloodline stage, ascension, ascension stage.
- `CollectCharacter` reads `PartyMemberState.trait_instances` with expected `TraitSourceKind.Character`.
- `CollectEquipment` reads fixed traits from `ItemDef.GetTraitIdsTyped()` and roll traits from `EquipmentInstanceState.trait_instances` with expected `TraitSourceKind.EquipmentRoll`.
- `RawKey` returns `trait_instance_id` for `Character` and `EquipmentRoll`; otherwise returns `{source_type}::{source_id}::{trait_id}`.
- `ApplyStackPolicies` groups by `TraitId` in encounter order.
- `UniqueByTrait` collapses to final key `trait_id` with stacks `1`.
- `HighestRoll` selects the instance with the highest `TraitDef.GetHighestRollCompareKey()` int roll; tie-break by ordinal `EffectiveInstanceKey`; collapse to final key `trait_id`.
- `Additive` sums `Stacks`, collapses to final key `trait_id`.
- `StackByInstance` keeps each raw effective instance and its raw key.
- `AppendAttributeModifiers` clones each `TraitDef.attribute_modifiers` into new `AttributeModifier` objects with `value = baseMod.GetValueForRank(instance.Rank) * Mathf.Max(instance.Stacks, 1)`, `value_per_rank = 0`, `source_type = TraitContentRules.ToAttributeSourceType(instance.SourceKind)`, and `source_id = instance.EffectiveInstanceKey`.

- [ ] **Step 4: Run aggregation test to verify GREEN**

Run:

```bash
dotnet build magic.csproj
godot --headless -s res://tests/progression/core/run_character_trait_service_regression.cs
```

Expected: build PASS and `Character trait service regression: PASS`.

---

### Task 3: AttributeService Trait Modifier Pipeline

**Files:**
- Modify: `scripts/systems/attributes/AttributeSourceContext.cs`
- Modify: `scripts/systems/attributes/AttributeService.cs`
- Test: `tests/progression/core/run_attribute_trait_modifier_regression.cs`

**Interfaces:**
- Consumes: `AttributeSourceContext.trait_attribute_modifiers`.
- Produces: trait modifiers included in `AttributeService.GetSnapshot()` using each modifier's own `source_type` and `source_id`.

- [ ] **Step 1: Write failing AttributeService regression**

Create `tests/progression/core/run_attribute_trait_modifier_regression.cs`:

```csharp
using System.Collections.Generic;
using Godot;

public partial class run_attribute_trait_modifier_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestTraitAttributeModifiersAffectSnapshot();
        GodotSharpCleanup.CollectPendingFinalizers();
        Quit(_test.Finish("Attribute trait modifier regression"));
    }

    private void TestTraitAttributeModifiersAffectSnapshot()
    {
        UnitProgress progress = new();
        progress.unit_id = "hero";
        progress.display_name = "Hero";
        progress.unit_base_attributes.SetAttributeValue("strength", 10);

        AttributeSourceContext context = new()
        {
            unit_progress = progress,
            trait_attribute_modifiers = new List<AttributeModifier>
            {
                new()
                {
                    attribute_id = "strength",
                    mode = "flat",
                    value = 3,
                    source_type = "trait_character",
                    source_id = "character_grit",
                },
            },
        };

        AttributeService service = new();
        service.SetupContext(context);

        _test.Eq(service.GetSnapshot().GetValue("strength"), 13, "Trait attribute modifiers should affect snapshots.");
        _test.True(
            context.trait_attribute_modifiers != null && context.trait_attribute_modifiers.Count == 1,
            "SetupContext should keep a copied trait modifier list on the context."
        );
    }
}
```

- [ ] **Step 2: Run AttributeService test to verify RED**

Run:

```bash
dotnet build magic.csproj
```

Expected: build FAIL because `AttributeSourceContext.trait_attribute_modifiers` does not exist.

- [ ] **Step 3: Add trait modifier context field and pipeline hook**

In `scripts/systems/attributes/AttributeSourceContext.cs`, add after `equipment_state`:

```csharp
public List<AttributeModifier> trait_attribute_modifiers = new();
```

In `scripts/systems/attributes/AttributeService.cs`:

- Add field:

```csharp
private List<AttributeModifier> _trait_attribute_modifiers = new();
```

- In `SetupContext`, after `_equipment_state = CopyAttributeModifierList(...)`, add:

```csharp
_trait_attribute_modifiers = CopyAttributeModifierList(_context.trait_attribute_modifiers);
```

- In the context assignment block, add:

```csharp
_context.trait_attribute_modifiers = _trait_attribute_modifiers;
```

- In `CollectAllModifierEntries`, after equipment entries and before passive entries, add:

```csharp
AppendTraitModifierEntries(entries, _trait_attribute_modifiers);
```

- Add helper near `AppendExternalModifierEntries`:

```csharp
private static void AppendTraitModifierEntries(
    List<AttributeModifierEntry> entries,
    List<AttributeModifier> state
)
{
    if (state == null || state.Count == 0)
        return;
    AppendModifierEntries(entries, state, "", "", 1);
}
```

- [ ] **Step 4: Run AttributeService test to verify GREEN**

Run:

```bash
dotnet build magic.csproj
godot --headless -s res://tests/progression/core/run_attribute_trait_modifier_regression.cs
```

Expected: build PASS and `Attribute trait modifier regression: PASS`.

---

### Task 4: CharacterManagementModule Integration

**Files:**
- Modify: `scripts/systems/progression/CharacterManagementModule.cs`
- Modify: `scripts/systems/game_runtime/GameRuntimeFacade.cs`
- Test: `tests/progression/core/run_character_management_trait_attribute_regression.cs`

**Interfaces:**
- Consumes: `CharacterTraitService`, `CharacterTraitService.ICharacterTraitGateway`, `AttributeSourceContext.trait_attribute_modifiers`.
- Produces: `CharacterManagementModule` passes trait defs from runtime setup and fills trait modifiers in `build_attribute_source_context(...)`.

- [ ] **Step 1: Write failing CharacterManagement integration regression**

Create `tests/progression/core/run_character_management_trait_attribute_regression.cs`:

```csharp
using System.Collections.Generic;
using Godot;

public partial class run_character_management_trait_attribute_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestCharacterTraitModifiersFlowIntoMemberSnapshot();
        GodotSharpCleanup.CollectPendingFinalizers();
        Quit(_test.Finish("Character management trait attribute regression"));
    }

    private void TestCharacterTraitModifiersFlowIntoMemberSnapshot()
    {
        PartyState party = BuildParty();
        TraitDef trait = new()
        {
            trait_id = "battle_hardened",
            display_name = "Battle Hardened",
            description = "Battle Hardened",
            allowed_source_kinds = new Godot.Collections.Array<StringName> { "character" },
            effect_type = "attribute_modifier",
            trigger_type = "passive",
            stack_policy = "unique_by_trait",
            charge_scope = "none",
            charge_reset_timing = "none",
            attribute_modifiers = new Godot.Collections.Array<AttributeModifier>
            {
                new()
                {
                    attribute_id = "strength",
                    mode = "flat",
                    value = 2,
                },
            },
        };

        party.GetMemberState("hero").trait_instances.Add(
            TraitInstanceState.Create(
                "char_trait_001",
                "battle_hardened",
                TraitSourceKind.Character,
                "fixture_reward"
            )
        );

        CharacterManagementModule module = new();
        module.setup(
            party,
            new Dictionary<StringName, SkillDef>(),
            new Dictionary<StringName, ProfessionDef>(),
            new Dictionary<StringName, AchievementDef>(),
            new Dictionary<StringName, ItemDef>(),
            new Dictionary<StringName, QuestDef>(),
            new Dictionary<StringName, TraitDef> { ["battle_hardened"] = trait },
            null,
            new ProgressionIdentityCatalogData()
        );

        AttributeSnapshot snapshot = module.GetMemberAttributeSnapshot("hero");

        _test.Eq(snapshot.GetValue("strength"), 12, "Character trait attribute modifier should affect member snapshot.");
    }

    private static PartyState BuildParty()
    {
        PartyState party = new()
        {
            leader_member_id = "hero",
            main_character_member_id = "hero",
            active_member_ids = new Godot.Collections.Array<StringName> { "hero" },
            warehouse_state = new WarehouseState(),
        };
        PartyMemberState member = new()
        {
            member_id = "hero",
            display_name = "Hero",
        };
        member.progression.unit_id = "hero";
        member.progression.display_name = "Hero";
        member.progression.unit_base_attributes.SetAttributeValue("strength", 10);
        party.SetMemberState(member);
        return party;
    }
}
```

- [ ] **Step 2: Run integration test to verify RED**

Run:

```bash
dotnet build magic.csproj
```

Expected: build FAIL because the typed `CharacterManagementModule.setup(...)` overload does not accept trait defs.

- [ ] **Step 3: Extend `CharacterManagementModule` setup and gateway**

In `scripts/systems/progression/CharacterManagementModule.cs`:

- Add to class declaration:

```csharp
public partial class CharacterManagementModule : RefCounted, IBattleRuntimeCharacterGateway, CharacterTraitService.ICharacterTraitGateway
```

- Add field near other indexes:

```csharp
private Dictionary<StringName, TraitDef> _trait_def_index = new();
```

- Clear it in `Dispose()`:

```csharp
_trait_def_index.Clear();
```

- Add a typed setup overload with trait defs before allocator:

```csharp
public void setup(
    PartyState party_state,
    IReadOnlyDictionary<StringName, SkillDef> skill_defs,
    IReadOnlyDictionary<StringName, ProfessionDef> profession_defs,
    IReadOnlyDictionary<StringName, AchievementDef> achievement_defs,
    IReadOnlyDictionary<StringName, ItemDef> item_defs,
    IReadOnlyDictionary<StringName, QuestDef> quest_defs,
    IReadOnlyDictionary<StringName, TraitDef> trait_defs,
    Func<StringName> equipment_instance_id_allocator,
    ProgressionIdentityCatalogData progression_identity_catalog
) =>
    setup(
        party_state,
        skill_defs,
        profession_defs,
        achievement_defs,
        item_defs,
        quest_defs,
        quest_defs != null && quest_defs.Count > 0,
        trait_defs,
        equipment_instance_id_allocator,
        progression_identity_catalog
    );
```

- Extend the canonical setup signature to include `IReadOnlyDictionary<StringName, TraitDef> trait_defs` before allocator, set `_trait_def_index = CloneContentDefIndex(trait_defs);`, and keep existing overloads forwarding `new Dictionary<StringName, TraitDef>()`.
- In `SetPartyState`, do not clear `_trait_def_index`; only refresh party-bound services as before.
- Implement gateway methods by forwarding existing private methods:

```csharp
RaceDef CharacterTraitService.ICharacterTraitGateway.GetRaceDefForTraitAggregation(StringName memberId) => GetRaceDefForMember(memberId);
SubraceDef CharacterTraitService.ICharacterTraitGateway.GetSubraceDefForTraitAggregation(StringName memberId) => GetSubraceDefForMember(memberId);
BloodlineDef CharacterTraitService.ICharacterTraitGateway.GetBloodlineDefForTraitAggregation(StringName memberId) => GetBloodlineDefForMember(memberId);
BloodlineStageDef CharacterTraitService.ICharacterTraitGateway.GetBloodlineStageDefForTraitAggregation(StringName memberId) => GetBloodlineStageDefForMember(memberId);
AscensionDef CharacterTraitService.ICharacterTraitGateway.GetAscensionDefForTraitAggregation(StringName memberId) => GetAscensionDefForMember(memberId);
AscensionStageDef CharacterTraitService.ICharacterTraitGateway.GetAscensionStageDefForTraitAggregation(StringName memberId) => GetAscensionStageDefForMember(memberId);
PartyMemberState CharacterTraitService.ICharacterTraitGateway.GetMemberStateForTraitAggregation(StringName memberId) => GetMemberState(memberId);
EquipmentState CharacterTraitService.ICharacterTraitGateway.GetEquipmentStateForTraitAggregation(StringName memberId) => GetMemberState(memberId)?.equipment_state;
ItemDef CharacterTraitService.ICharacterTraitGateway.GetItemDefForTraitAggregation(StringName itemId) => GetItemDef(itemId);
```

- Add helper:

```csharp
private CharacterTraitService BuildCharacterTraitService() =>
    new(_trait_def_index, this);
```

- In `build_attribute_source_context(...)`, after `context.equipment_state = ...`, add:

```csharp
EffectiveTraitSet effectiveTraits = BuildCharacterTraitService()
    .BuildEffectiveTraits(member_id, equipment_state);
context.trait_attribute_modifiers = BuildCharacterTraitService()
    .ResolveTraitAttributeModifiers(effectiveTraits);
```

Store the service in a local variable to avoid building twice.

- [ ] **Step 4: Pass trait defs from runtime setup**

In `scripts/systems/game_runtime/GameRuntimeFacade.cs`, update `_character_management.setup(...)` to pass `_content_catalog.GetTraitDefsTyped()` before `GetEquipmentInstanceIdAllocator()`.

- [ ] **Step 5: Run integration test to verify GREEN**

Run:

```bash
dotnet build magic.csproj
godot --headless -s res://tests/progression/core/run_character_management_trait_attribute_regression.cs
```

Expected: build PASS and `Character management trait attribute regression: PASS`.

---

### Task 5: Phase 3 Verification And Context Notes

**Files:**
- Modify: `docs/design/project_context_units.md` only if ownership/read-set notes changed.
- Test: focused runners listed below.

**Interfaces:**
- Consumes: Tasks 1-4.
- Produces: verified Phase 3 aggregation + attribute layer, with Phase 4 battle work still untouched.

- [ ] **Step 1: Update context map if needed**

If not already represented, update:

- CU-11 to mention `EffectiveTrait.cs` as runtime aggregation DTOs.
- CU-12 to mention `CharacterTraitService` and trait-derived `AttributeSourceContext.trait_attribute_modifiers`.
- CU-14 to mention `AttributeService` consumes pre-resolved trait modifiers and does not query trait catalogs.

- [ ] **Step 2: Run focused Phase 3 tests**

Run:

```bash
dotnet build magic.csproj
godot --headless -s res://tests/progression/core/run_effective_trait_set_regression.cs
godot --headless -s res://tests/progression/core/run_character_trait_service_regression.cs
godot --headless -s res://tests/progression/core/run_attribute_trait_modifier_regression.cs
godot --headless -s res://tests/progression/core/run_character_management_trait_attribute_regression.cs
```

Expected: build PASS and all four Phase 3 regressions PASS.

- [ ] **Step 3: Run existing affected regressions**

Run:

```bash
godot --headless -s res://tests/progression/core/run_party_state_duplicate_regression.cs
godot --headless -s res://tests/equipment/run_party_equipment_regression.cs
godot --headless -s res://tests/runtime/validation/run_resource_validation_regression.cs
```

Expected: all PASS; official validation remains `errors=0`.

- [ ] **Step 4: Run diff hygiene**

Run:

```bash
git diff --check -- \
  scripts/systems/progression/EffectiveTrait.cs \
  scripts/systems/progression/CharacterTraitService.cs \
  scripts/systems/progression/CharacterManagementModule.cs \
  scripts/systems/game_runtime/GameRuntimeFacade.cs \
  scripts/systems/attributes/AttributeSourceContext.cs \
  scripts/systems/attributes/AttributeService.cs \
  tests/progression/core/run_effective_trait_set_regression.cs \
  tests/progression/core/run_character_trait_service_regression.cs \
  tests/progression/core/run_attribute_trait_modifier_regression.cs \
  tests/progression/core/run_character_management_trait_attribute_regression.cs \
  docs/design/project_context_units.md
```

Expected: no whitespace errors.

- [ ] **Step 5: Record progress**

Append one line to `.git/sdd/progress.md` summarizing Phase 3 completion and the verification commands that passed. Do not commit unless explicitly requested.

---

## Self-Review Notes

- Spec coverage: Tasks 1-2 cover implementation spec sections 7-8. Task 3 covers section 9. Task 4 wires the service into `CharacterManagementModule` and runtime setup. Task 5 covers context notes and verification.
- Explicit non-goal: This plan does not implement `BattleUnitState.effective_trait_instances`, `TraitTriggerHooks`, charge lifecycle, or Phase 4a/4b migration.
- Dictionary boundary: `Godot.Collections.Dictionary` appears only in roll values and battle payload projection; runtime indexes are private CLR dictionaries.
- Compatibility: no old schema fallback or migration path is added.
