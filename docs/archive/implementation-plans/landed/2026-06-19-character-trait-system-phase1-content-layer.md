# Character Trait System Phase 1 Content Layer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement the Phase 1 content layer for the generic character trait system without touching equipment rolls, save schema, attribute aggregation, or battle payload migration.

**Architecture:** Add `TraitDef` as the formal generic trait content resource and `TraitContentRegistry` as its typed registry. Wire `trait_defs` into `ProgressionContentRegistry`, `GameSession`, `GameContentCatalog`, and validation runners while keeping `RaceTraitDef` / `RaceTraitContentRegistry` as a temporary bridge for current battle and character creation callers.

**Tech Stack:** Godot 4.6.2, GodotSharp C# net8.0, `.tres` Resource configs, headless C# regression runners.

## Global Constraints

- Scope is Phase 1 only: no `TraitInstanceState`, no equipment trait roll service, no party/equipment save schema changes, no `CharacterTraitService`, no `BattleUnitState.effective_trait_instances`, and no `TraitTriggerHooks` runtime migration.
- Compatibility policy from the design docs still applies to future save/schema work: no old save fallback, no legacy save aliases, no fallback migrations.
- Phase 1 keeps `RaceTraitDef`, `RaceTraitEffectKind`, `RaceTraitContentRegistry`, and existing `data/configs/race_traits/*.tres` as bridge content until Phase 5.
- Formal identity `trait_ids` reference validation must switch to `TraitDef` / `trait_defs`.
- Existing race trait resources are copied to `data/configs/traits/*.tres` with `script_class="TraitDef"` and `res://scripts/player/progression/TraitDef.cs`.
- `TraitEffectKind` must cover all 43 current `RaceTraitEffectKind` values, not just the three trigger handlers.
- All new fixed value sets use typed enum/rules conversion in `TraitContentRules`; runtime logic must not duplicate string whitelist sets.
- Dictionary usage is intentionally boundary-scoped: `Dictionary<StringName, T>` is allowed for private content indexes and cloned typed getters; `GDictionary` is allowed for existing Godot resource/export fields, content bucket projection, and test fixtures only. Phase 1 must not introduce runtime business logic that reads mutable `GDictionary` payloads directly.
- `TraitDef.@params` remains an export boundary field for effect-specific static config, but Phase 1 validation must at least enforce non-empty string/StringName keys and any runtime-consumed param must be routed through a typed helper/handler before later phases use it.
- Do not include battle simulation or numeric balance runners in Phase 1 verification.
- Do not revert or touch unrelated dirty files in the current worktree.

---

## File Structure

Create:

- `scripts/player/progression/TraitContentRules.cs`: typed enum and string conversion owner for generic traits.
- `scripts/player/progression/TraitDef.cs`: generic trait resource definition.
- `scripts/player/progression/TraitRollValueSchemaEntry.cs`: typed roll schema subresource.
- `scripts/player/progression/TraitContentRegistry.cs`: registry and validation for `data/configs/traits`.
- `data/configs/traits/*.tres`: 43 copied/converted official trait resources.
- `tests/progression/schema/run_trait_content_rules_regression.cs`: enum/rules regression.
- `tests/progression/identity/run_trait_content_registry_regression.cs`: registry/resource/parity regression.
- `tests/progression/fixtures/trait_registry_invalid/*.tres`: invalid trait fixtures for registry validation.

Modify:

- `scripts/player/progression/ProgressionContentRegistry.cs`: add `trait_defs` bucket/index/getter/lifecycle and switch identity `trait_ids` validation to generic traits.
- `scripts/systems/persistence/GameSession.cs`: expose `GetTraitDefsTyped()` for content catalog rebuilds.
- `scripts/systems/content/GameContentCatalog.cs`: cache and expose trait defs typed snapshot.
- `tests/runtime/validation/ContentValidationRunner.cs`: include `TraitContentRegistry` and `trait_defs` in identity validation sources.
- `tests/runtime/validation/run_progression_content_registry_typed_regression.cs`: cover `trait_defs` replacement and typed getter.
- `tests/runtime/validation/run_game_root_content_catalog_regression.cs`: cover trait defs catalog snapshot, defensive read-only view, and invalidation.
- `tests/runtime/validation/run_resource_validation_regression.cs`: update official resource validation expectations for trait content.
- `docs/design/project_context_units.md`: after code lands, update CU-02 and CU-13 read sets / ownership notes to include `TraitDef` and `TraitContentRegistry`.

Leave unchanged in Phase 1:

- `scripts/player/progression/RaceTraitDef.cs`
- `scripts/player/progression/RaceTraitContentRegistry.cs`
- `scripts/player/progression/TraitTriggerContentRules.cs`
- `scripts/systems/battle/runtime/TraitTriggerHooks.cs`
- `scripts/ui/CharacterCreationWindow.cs`
- `data/configs/race_traits/*.tres`

---

### Task 1: Add Trait Typed Rules And Resource Types

**Files:**
- Create: `scripts/player/progression/TraitContentRules.cs`
- Create: `scripts/player/progression/TraitDef.cs`
- Create: `scripts/player/progression/TraitRollValueSchemaEntry.cs`
- Test: `tests/progression/schema/run_trait_content_rules_regression.cs`

**Interfaces:**
- Produces: `TraitEffectKind`, `TraitStackPolicyKind`, `TraitSourceKind`, `TraitRollValueType`, `TraitChargeScopeKind`, `TraitChargeResetTimingKind`
- Produces: `TraitContentRules.ToEffectKind(StringName)`, `TraitContentRules.ToStringName(TraitEffectKind)`, `TraitContentRules.IsValidEffectType(StringName)`
- Produces: `TraitContentRules.ToStackPolicyKind(StringName)`, `TraitContentRules.ToStringName(TraitStackPolicyKind)`, `TraitContentRules.IsValidStackPolicy(StringName)`
- Produces: `TraitContentRules.ToSourceKind(StringName)`, `TraitContentRules.ToStringName(TraitSourceKind)`, `TraitContentRules.IsValidSourceType(StringName)`, `TraitContentRules.IsSourceKindAllowed(TraitDef, TraitSourceKind)`
- Produces: `TraitContentRules.ToRollValueType(StringName)`, `TraitContentRules.IsValidRollValueType(StringName)`
- Produces: `TraitContentRules.ToChargeScopeKind(StringName)`, `TraitContentRules.ToStringName(TraitChargeScopeKind)`, `TraitContentRules.IsValidChargeScope(StringName)`
- Produces: `TraitContentRules.ToChargeResetTimingKind(StringName)`, `TraitContentRules.ToStringName(TraitChargeResetTimingKind)`, `TraitContentRules.IsValidChargeResetTiming(StringName)`
- Produces: `TraitDef` with exported fields from the design docs.
- Produces: `TraitRollValueSchemaEntry.AppendSchemaErrors(List<string>, string)`.

- [ ] **Step 1: Write the failing enum/rules regression**

Create `tests/progression/schema/run_trait_content_rules_regression.cs`:

```csharp
using System;
using System.Collections.Generic;
using Godot;

public partial class run_trait_content_rules_regression : SceneTree
{
    private readonly TestHarness _test = new();

    private static readonly StringName[] EffectIds =
    {
        "darkvision", "superior_darkvision", "fey_ancestry", "brave",
        "halfling_luck", "savage_attacks", "relentless_endurance",
        "gnome_cunning", "dwarven_resilience", "duergar_resilience",
        "human_versatility", "small_body", "fleet_of_foot", "dragon_breath",
        "racial_spell_grant", "damage_resistance", "save_advantage",
        "civil_militia", "keen_senses", "trance", "elven_weapon_training",
        "drow_weapon_training", "dwarven_combat_training",
        "shield_dwarf_armor_training", "dwarven_toughness", "menacing",
        "halfling_nimbleness", "naturally_stealthy", "mask_of_the_wild",
        "stonecunning", "forest_gnome_magic", "deep_gnome_camouflage",
        "artificers_lore", "duergar_magic", "githyanki_martial_prodigy",
        "astral_knowledge", "githyanki_psionics", "infernal_legacy",
        "asmodeus_legacy", "mephistopheles_legacy", "zariel_legacy",
        "drow_magic", "draconic_ancestry",
    };

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestEffectMappingCoversCurrentRaceTraitEffects();
        TestPolicyMappingsRejectUnknownValues();
        TestSourceKindAllowedUsesTraitDefDeclaration();
        TestRollSchemaValidation();
        Quit(_test.Finish("Trait content rules regression"));
    }

    private void TestEffectMappingCoversCurrentRaceTraitEffects()
    {
        foreach (StringName effectId in EffectIds)
        {
            TraitEffectKind kind = TraitContentRules.ToEffectKind(effectId);
            _test.True(kind != TraitEffectKind.Unknown, $"effect {effectId} should map to TraitEffectKind.");
            _test.Eq(effectId, TraitContentRules.ToStringName(kind), $"effect {effectId} should round-trip.");
        }
        _test.Eq("", TraitContentRules.ToStringName(TraitEffectKind.Unknown), "Unknown effect should serialize to empty.");
        _test.False(TraitContentRules.IsValidEffectType("not_a_trait_effect"), "unknown effect string should be invalid.");
    }

    private void TestPolicyMappingsRejectUnknownValues()
    {
        _test.Eq(TraitStackPolicyKind.UniqueByTrait, TraitContentRules.ToStackPolicyKind("unique_by_trait"), "unique_by_trait stack policy.");
        _test.Eq(TraitStackPolicyKind.HighestRoll, TraitContentRules.ToStackPolicyKind("highest_roll"), "highest_roll stack policy.");
        _test.Eq(TraitStackPolicyKind.Additive, TraitContentRules.ToStackPolicyKind("additive"), "additive stack policy.");
        _test.Eq(TraitStackPolicyKind.StackByInstance, TraitContentRules.ToStackPolicyKind("stack_by_instance"), "stack_by_instance stack policy.");
        _test.False(TraitContentRules.IsValidStackPolicy("stack_everything"), "unknown stack policy should be invalid.");

        _test.Eq(TraitChargeScopeKind.None, TraitContentRules.ToChargeScopeKind("none"), "none charge scope.");
        _test.Eq(TraitChargeScopeKind.PerTurn, TraitContentRules.ToChargeScopeKind("per_turn"), "per_turn charge scope.");
        _test.Eq(TraitChargeScopeKind.PerBattle, TraitContentRules.ToChargeScopeKind("per_battle"), "per_battle charge scope.");
        _test.False(TraitContentRules.IsValidChargeScope("per_scene"), "unknown charge scope should be invalid.");

        _test.Eq(TraitChargeResetTimingKind.None, TraitContentRules.ToChargeResetTimingKind("none"), "none reset timing.");
        _test.Eq(TraitChargeResetTimingKind.BattleStart, TraitContentRules.ToChargeResetTimingKind("battle_start"), "battle_start reset timing.");
        _test.Eq(TraitChargeResetTimingKind.TurnStart, TraitContentRules.ToChargeResetTimingKind("turn_start"), "turn_start reset timing.");
        _test.False(TraitContentRules.IsValidChargeResetTiming("round_start"), "unknown reset timing should be invalid.");

        _test.Eq(TraitRollValueType.Int, TraitContentRules.ToRollValueType("int"), "int roll type.");
        _test.Eq(TraitRollValueType.StringName, TraitContentRules.ToRollValueType("string_name"), "string_name roll type.");
        _test.Eq(TraitRollValueType.Bool, TraitContentRules.ToRollValueType("bool"), "bool roll type.");
        _test.False(TraitContentRules.IsValidRollValueType("float"), "unknown roll type should be invalid.");
    }

    private void TestSourceKindAllowedUsesTraitDefDeclaration()
    {
        TraitDef def = new();
        def.allowed_source_kinds.Add("identity");
        def.allowed_source_kinds.Add("equipment_roll");

        _test.True(TraitContentRules.IsSourceKindAllowed(def, TraitSourceKind.Identity), "identity source should be allowed.");
        _test.True(TraitContentRules.IsSourceKindAllowed(def, TraitSourceKind.EquipmentRoll), "equipment_roll source should be allowed.");
        _test.False(TraitContentRules.IsSourceKindAllowed(def, TraitSourceKind.Character), "character source should not be allowed.");
        _test.False(TraitContentRules.IsSourceKindAllowed(def, TraitSourceKind.Unknown), "unknown source should not be allowed.");
    }

    private void TestRollSchemaValidation()
    {
        List<string> errors = new();
        TraitRollValueSchemaEntry badInt = new()
        {
            key = "amount",
            value_type = "int",
            min_value = 5,
            max_value = 3,
        };
        badInt.AppendSchemaErrors(errors, "Trait test_trait");
        _test.True(errors.Count == 1 && errors[0].Contains("min_value"), "invalid int range should report an error.");

        errors.Clear();
        TraitRollValueSchemaEntry badStringName = new()
        {
            key = "damage_tag",
            value_type = "string_name",
        };
        badStringName.AppendSchemaErrors(errors, "Trait test_trait");
        _test.True(errors.Count == 1 && errors[0].Contains("allowed_values"), "string_name roll needs allowed values.");
    }
}
```

- [ ] **Step 2: Run the regression to verify it fails before implementation**

Run:

```bash
dotnet build magic.csproj
```

Expected: FAIL with missing `TraitContentRules`, `TraitDef`, `TraitRollValueSchemaEntry`, and trait enum type errors.

- [ ] **Step 3: Implement `TraitContentRules.cs`**

Create `scripts/player/progression/TraitContentRules.cs` with the enums and conversion methods used by the test. The `TraitEffectKind` enum must contain these values in addition to `Unknown`: `Darkvision`, `SuperiorDarkvision`, `FeyAncestry`, `Brave`, `HalflingLuck`, `SavageAttacks`, `RelentlessEndurance`, `GnomeCunning`, `DwarvenResilience`, `DuergarResilience`, `HumanVersatility`, `SmallBody`, `FleetOfFoot`, `DragonBreath`, `RacialSpellGrant`, `DamageResistance`, `SaveAdvantage`, `CivilMilitia`, `KeenSenses`, `Trance`, `ElvenWeaponTraining`, `DrowWeaponTraining`, `DwarvenCombatTraining`, `ShieldDwarfArmorTraining`, `DwarvenToughness`, `Menacing`, `HalflingNimbleness`, `NaturallyStealthy`, `MaskOfTheWild`, `Stonecunning`, `ForestGnomeMagic`, `DeepGnomeCamouflage`, `ArtificersLore`, `DuergarMagic`, `GithyankiMartialProdigy`, `AstralKnowledge`, `GithyankiPsionics`, `InfernalLegacy`, `AsmodeusLegacy`, `MephistophelesLegacy`, `ZarielLegacy`, `DrowMagic`, `DraconicAncestry`.

Use the exact string names from `RaceTraitDef.ToStringName(...)`; do not invent display strings.

- [ ] **Step 4: Implement `TraitDef.cs`**

Create `scripts/player/progression/TraitDef.cs`:

```csharp
using Godot;

[GlobalClass]
public partial class TraitDef : Resource
{
    [Export] public StringName trait_id { get; set; } = "";
    [Export] public string display_name { get; set; } = "";
    [Export(PropertyHint.MultilineText)] public string description { get; set; } = "";
    [Export] public Godot.Collections.Array<StringName> categories { get; set; } = new();
    [Export] public Godot.Collections.Array<StringName> allowed_source_kinds { get; set; } = new();
    [Export] public StringName effect_type { get; set; } = "";
    [Export] public StringName trigger_type { get; set; } = "passive";
    [Export] public StringName stack_policy { get; set; } = "unique_by_trait";
    [Export] public StringName charge_scope { get; set; } = "none";
    [Export] public StringName charge_reset_timing { get; set; } = "none";
    [Export] public Godot.Collections.Dictionary @params { get; set; } = new();
    [Export] public Godot.Collections.Array<AttributeModifier> attribute_modifiers { get; set; } = new();
    [Export] public Godot.Collections.Array<TraitRollValueSchemaEntry> roll_value_schema { get; set; } = new();

    internal TraitEffectKind EffectKind => TraitContentRules.ToEffectKind(effect_type);
    internal TraitTriggerKind TriggerKind => TraitTriggerContentRules.ToTriggerKind(trigger_type);
    internal TraitStackPolicyKind StackPolicyKind => TraitContentRules.ToStackPolicyKind(stack_policy);
    internal TraitChargeScopeKind ChargeScopeKind => TraitContentRules.ToChargeScopeKind(charge_scope);
    internal TraitChargeResetTimingKind ChargeResetTimingKind =>
        TraitContentRules.ToChargeResetTimingKind(charge_reset_timing);
}
```

- [ ] **Step 5: Implement `TraitRollValueSchemaEntry.cs`**

Create `scripts/player/progression/TraitRollValueSchemaEntry.cs`:

```csharp
using Godot;
using System.Collections.Generic;

[GlobalClass]
public partial class TraitRollValueSchemaEntry : Resource
{
    [Export] public StringName key { get; set; } = "";
    [Export] public StringName value_type { get; set; } = "int";
    [Export] public int min_value { get; set; }
    [Export] public int max_value { get; set; }
    [Export] public Godot.Collections.Array<StringName> allowed_values { get; set; } = new();

    internal TraitRollValueType ValueTypeKind => TraitContentRules.ToRollValueType(value_type);

    internal void AppendSchemaErrors(List<string> errors, string ownerLabel)
    {
        if (key == "")
            errors.Add($"{ownerLabel}: roll_value_schema entry missing key.");
        switch (ValueTypeKind)
        {
            case TraitRollValueType.Int:
                if (min_value > max_value)
                    errors.Add($"{ownerLabel}.{key}: min_value {min_value} > max_value {max_value}.");
                break;
            case TraitRollValueType.StringName:
                if (allowed_values.Count == 0)
                    errors.Add($"{ownerLabel}.{key}: string_name roll needs non-empty allowed_values.");
                break;
            case TraitRollValueType.Bool:
                break;
            default:
                errors.Add($"{ownerLabel}.{key}: unsupported value_type {value_type}.");
                break;
        }
    }
}
```

- [ ] **Step 6: Run the regression and build**

Run:

```bash
dotnet build magic.csproj
godot --headless -s res://tests/progression/schema/run_trait_content_rules_regression.cs
```

Expected: build PASS; regression PASS.

- [ ] **Step 7: Commit Task 1**

```bash
git add scripts/player/progression/TraitContentRules.cs \
  scripts/player/progression/TraitDef.cs \
  scripts/player/progression/TraitRollValueSchemaEntry.cs \
  tests/progression/schema/run_trait_content_rules_regression.cs
git commit -m "feat: add typed trait content rules"
```

If the working tree still contains unrelated pre-existing changes, commit only the files listed above.

---

### Task 2: Add TraitContentRegistry And Official Trait Resources

**Files:**
- Create: `scripts/player/progression/TraitContentRegistry.cs`
- Create: `data/configs/traits/*.tres`
- Create: `tests/progression/fixtures/trait_registry_invalid/missing_source_kind_trait.tres`
- Create: `tests/progression/fixtures/trait_registry_invalid/identity_attribute_trait.tres`
- Create: `tests/progression/fixtures/trait_registry_invalid/invalid_charge_scope_trait.tres`
- Test: `tests/progression/identity/run_trait_content_registry_regression.cs`

**Interfaces:**
- Consumes: `TraitDef`, `TraitContentRules`, `TraitRollValueSchemaEntry`.
- Produces: `TraitContentRegistry.GetTraitDefsTyped(): IReadOnlyDictionary<StringName, TraitDef>`.
- Produces: `TraitContentRegistry.GetTraitDef(StringName): TraitDef`.
- Produces: `TraitContentRegistry.HasTrait(StringName): bool`.
- Produces: `TraitContentRegistry.LoadFromDirectory(string)` and `LoadFromDirectories(Array<string>)`.

- [ ] **Step 1: Write the failing trait registry regression**

Create `tests/progression/identity/run_trait_content_registry_regression.cs`:

```csharp
using System.Collections.Generic;
using Godot;

public partial class run_trait_content_registry_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestOfficialTraitRegistryValidatesWithoutErrors();
        TestOfficialTraitRegistryMatchesRaceTraitBridgeIds();
        TestInvalidTraitFixturesAreRejected();
        Quit(_test.Finish("Trait content registry regression"));
    }

    private void TestOfficialTraitRegistryValidatesWithoutErrors()
    {
        using TraitContentRegistry registry = new();
        List<string> errors = ToList(registry.Validate());
        _test.Eq(0, errors.Count, $"Official trait content should validate without errors: {Format(errors)}");
        _test.Eq(43, registry.GetTraitDefsTyped().Count, "Official trait registry should load 43 migrated race traits.");
    }

    private void TestOfficialTraitRegistryMatchesRaceTraitBridgeIds()
    {
        using TraitContentRegistry traitRegistry = new();
        using RaceTraitContentRegistry raceTraitRegistry = new();
        IReadOnlyDictionary<StringName, TraitDef> traitDefs = traitRegistry.GetTraitDefsTyped();
        IReadOnlyDictionary<StringName, RaceTraitDef> raceTraitDefs = raceTraitRegistry.GetRaceTraitDefsTyped();

        _test.Eq(raceTraitDefs.Count, traitDefs.Count, "trait_defs should match bridge race_trait_defs count during Phase 1.");
        foreach ((StringName traitId, RaceTraitDef raceTraitDef) in raceTraitDefs)
        {
            _test.True(traitDefs.TryGetValue(traitId, out TraitDef traitDef), $"trait_defs should contain {traitId}.");
            _test.Eq(raceTraitDef.effect_type, traitDef.effect_type, $"{traitId} effect_type should match bridge resource.");
            _test.Eq(raceTraitDef.trigger_type, traitDef.trigger_type, $"{traitId} trigger_type should match bridge resource.");
            _test.True(TraitContentRules.IsSourceKindAllowed(traitDef, TraitSourceKind.Identity), $"{traitId} should allow identity source.");
            _test.Eq(TraitStackPolicyKind.UniqueByTrait, traitDef.StackPolicyKind, $"{traitId} should default to unique_by_trait.");
            _test.Eq(TraitChargeScopeKind.None, traitDef.ChargeScopeKind, $"{traitId} should default to no charge in Phase 1.");
            _test.Eq(TraitChargeResetTimingKind.None, traitDef.ChargeResetTimingKind, $"{traitId} should default to no reset timing in Phase 1.");
        }
    }

    private void TestInvalidTraitFixturesAreRejected()
    {
        using TraitContentRegistry registry = new();
        registry.LoadFromDirectories(new Godot.Collections.Array<string>
        {
            "res://tests/progression/fixtures/trait_registry_invalid",
        });
        List<string> errors = ToList(registry.Validate());
        _test.True(errors.Count >= 3, $"invalid fixtures should produce errors: {Format(errors)}");
        _test.True(Contains(errors, "allowed_source_kind"), "missing/invalid source kind should be rejected.");
        _test.True(Contains(errors, "attribute_modifiers"), "identity attribute modifier should be rejected.");
        _test.True(Contains(errors, "charge_scope"), "invalid charge scope should be rejected.");
    }

    private static bool Contains(IEnumerable<string> errors, string needle)
    {
        foreach (string error in errors)
            if ((error ?? "").Contains(needle))
                return true;
        return false;
    }

    private static List<string> ToList(IEnumerable<string> values)
    {
        List<string> result = new();
        foreach (string value in values)
            result.Add(value);
        return result;
    }

    private static string Format(IEnumerable<string> values)
    {
        List<string> result = new();
        foreach (string value in values)
            result.Add(value ?? "");
        return result.Count == 0 ? "[]" : $"[{string.Join(" | ", result)}]";
    }
}
```

- [ ] **Step 2: Run the regression to verify it fails**

Run:

```bash
dotnet build magic.csproj
```

Expected: FAIL because `TraitContentRegistry` does not exist.

- [ ] **Step 3: Implement `TraitContentRegistry.cs`**

Create `scripts/player/progression/TraitContentRegistry.cs` by mirroring `RaceTraitContentRegistry` and changing the resource type to `TraitDef`. Use `TraitConfigDirectoryPath = "res://data/configs/traits"`.

Validation rules:

- `trait_id`, `display_name`, `description`, `effect_type`, `trigger_type`, `stack_policy`, `charge_scope`, and `charge_reset_timing` are validated.
- `effect_type` must pass `TraitContentRules.IsValidEffectType`.
- `trigger_type` must map through `TraitTriggerContentRules.ToTriggerKind`.
- `stack_policy` must pass `TraitContentRules.IsValidStackPolicy`.
- `charge_scope` must pass `TraitContentRules.IsValidChargeScope`.
- `charge_reset_timing` must pass `TraitContentRules.IsValidChargeResetTiming`.
- `allowed_source_kinds` must be non-empty and every value must pass `TraitContentRules.IsValidSourceType`.
- If `allowed_source_kinds` includes `identity`, `attribute_modifiers.Count` must be `0`.
- `params` keys must be `String` or `StringName` and must not be empty.
- `roll_value_schema` must not contain null entries, duplicate keys, invalid ranges, or unsupported value types.
- `highest_roll` must have a compare key available when that stack policy is used.

- [ ] **Step 4: Create official `data/configs/traits/*.tres` resources**

Create the target directory:

```bash
mkdir -p data/configs/traits
```

For each `data/configs/race_traits/*.tres`, copy to the same basename under `data/configs/traits/` and change:

```text
[gd_resource type="Resource" script_class="RaceTraitDef" format=3]
[ext_resource type="Script" path="res://scripts/player/progression/RaceTraitDef.cs" id="1_trait"]
```

to:

```text
[gd_resource type="Resource" script_class="TraitDef" format=3]
[ext_resource type="Script" path="res://scripts/player/progression/TraitDef.cs" id="1_trait"]
```

Add these fields to every copied resource:

```text
categories = Array[StringName]([])
allowed_source_kinds = Array[StringName]([&"identity"])
stack_policy = &"unique_by_trait"
charge_scope = &"none"
charge_reset_timing = &"none"
attribute_modifiers = Array[Resource]([])
roll_value_schema = Array[Resource]([])
```

Keep `trait_id`, `display_name`, `description`, `trigger_type`, `effect_type`, and `params` exactly as in the original bridge resource.

- [ ] **Step 5: Create invalid fixtures**

Create `tests/progression/fixtures/trait_registry_invalid/missing_source_kind_trait.tres`:

```text
[gd_resource type="Resource" script_class="TraitDef" format=3]

[ext_resource type="Script" path="res://scripts/player/progression/TraitDef.cs" id="1_trait"]

[resource]
script = ExtResource("1_trait")
trait_id = &"missing_source_kind_trait"
display_name = "Missing Source Kind Trait"
description = "Invalid fixture."
effect_type = &"brave"
trigger_type = &"passive"
stack_policy = &"unique_by_trait"
charge_scope = &"none"
charge_reset_timing = &"none"
allowed_source_kinds = Array[StringName]([])
params = {}
attribute_modifiers = Array[Resource]([])
roll_value_schema = Array[Resource]([])
```

Create `tests/progression/fixtures/trait_registry_invalid/invalid_charge_scope_trait.tres` with the same fields except:

```text
trait_id = &"invalid_charge_scope_trait"
display_name = "Invalid Charge Scope Trait"
allowed_source_kinds = Array[StringName]([&"identity"])
charge_scope = &"per_scene"
```

Create `tests/progression/fixtures/trait_registry_invalid/identity_attribute_trait.tres` with one `AttributeModifier` subresource:

```text
[gd_resource type="Resource" script_class="TraitDef" load_steps=3 format=3]

[ext_resource type="Script" path="res://scripts/player/progression/TraitDef.cs" id="1_trait"]
[ext_resource type="Script" path="res://scripts/player/progression/AttributeModifier.cs" id="2_attr"]

[sub_resource type="Resource" id="AttributeModifier_1"]
script = ExtResource("2_attr")
attribute_id = &"strength"
mode = &"add"
value = 1
value_per_rank = 0

[resource]
script = ExtResource("1_trait")
trait_id = &"identity_attribute_trait"
display_name = "Identity Attribute Trait"
description = "Invalid fixture."
effect_type = &"brave"
trigger_type = &"passive"
stack_policy = &"unique_by_trait"
charge_scope = &"none"
charge_reset_timing = &"none"
allowed_source_kinds = Array[StringName]([&"identity"])
params = {}
attribute_modifiers = Array[Resource]([SubResource("AttributeModifier_1")])
roll_value_schema = Array[Resource]([])
```

- [ ] **Step 6: Run registry regression**

Run:

```bash
dotnet build magic.csproj
godot --headless -s res://tests/progression/identity/run_trait_content_registry_regression.cs
```

Expected: build PASS; regression PASS.

- [ ] **Step 7: Commit Task 2**

```bash
git add scripts/player/progression/TraitContentRegistry.cs \
  data/configs/traits \
  tests/progression/identity/run_trait_content_registry_regression.cs \
  tests/progression/fixtures/trait_registry_invalid
git commit -m "feat: add trait content registry"
```

---

### Task 3: Wire Trait Definitions Into ProgressionContentRegistry

**Files:**
- Modify: `scripts/player/progression/ProgressionContentRegistry.cs`
- Test: `tests/runtime/validation/run_progression_content_registry_typed_regression.cs`
- Test: `tests/progression/identity/run_trait_content_registry_regression.cs`

**Interfaces:**
- Consumes: `TraitContentRegistry.GetTraitDefsTyped()`.
- Produces: `ProgressionContentRegistry._trait_defs` public Godot bucket.
- Produces: `ProgressionContentRegistry.GetTraitDefsTyped(): IReadOnlyDictionary<StringName, TraitDef>`.
- Preserves: `ProgressionContentRegistry.GetRaceTraitDefsTyped()` bridge getter.

- [ ] **Step 1: Add failing typed registry assertions**

Modify `tests/runtime/validation/run_progression_content_registry_typed_regression.cs`:

- In `Run()`, add `TestTraitDefinitionBucketsSyncIntoTypedIndexes();`.
- Add this method:

```csharp
private void TestTraitDefinitionBucketsSyncIntoTypedIndexes()
{
    using ProgressionContentRegistry registry = new();
    _test.True(
        registry.GetTraitDefsTyped().ContainsKey("human_versatility"),
        "official progression registry should expose generic trait_defs."
    );
    _test.True(
        registry.GetRaceTraitDefsTyped().ContainsKey("human_versatility"),
        "Phase 1 should keep race_trait_defs bridge available."
    );

    TraitDef customTrait = new()
    {
        trait_id = "custom_identity_trait",
        display_name = "Custom Identity Trait",
        description = "Custom test trait.",
        effect_type = "brave",
        trigger_type = "passive",
        stack_policy = "unique_by_trait",
        charge_scope = "none",
        charge_reset_timing = "none",
    };
    customTrait.allowed_source_kinds.Add("identity");

    RaceDef race = new()
    {
        race_id = "custom_race",
        display_name = "Custom Race",
        description = "Custom test race.",
    };
    race.trait_ids.Add("custom_identity_trait");

    registry.ReplaceDefinitionBuckets(new GDictionary
    {
        ["skill_defs"] = new GDictionary(),
        ["profession_defs"] = new GDictionary(),
        ["achievement_defs"] = new GDictionary(),
        ["quest_defs"] = new GDictionary(),
        ["race_defs"] = new GDictionary { [new StringName("custom_race")] = race },
        ["subrace_defs"] = new GDictionary(),
        ["race_trait_defs"] = new GDictionary(),
        ["trait_defs"] = new GDictionary { [new StringName("custom_identity_trait")] = customTrait },
        ["age_profile_defs"] = new GDictionary(),
        ["bloodline_defs"] = new GDictionary(),
        ["bloodline_stage_defs"] = new GDictionary(),
        ["ascension_defs"] = new GDictionary(),
        ["ascension_stage_defs"] = new GDictionary(),
        ["stage_advancement_defs"] = new GDictionary(),
    });

    _test.True(
        registry.GetTraitDefsTyped().ContainsKey("custom_identity_trait"),
        "typed trait getter should see replacement bucket content."
    );
    _test.Eq(
        0,
        CountErrorsContaining(registry.CollectValidationErrorsTyped(), "custom_identity_trait"),
        "identity trait_ids should validate against trait_defs, not race_trait_defs."
    );
}

private static int CountErrorsContaining(IEnumerable<string> errors, string needle)
{
    int count = 0;
    foreach (string error in errors)
        if ((error ?? "").Contains(needle))
            count++;
    return count;
}
```

- [ ] **Step 2: Run the typed registry regression to verify it fails**

Run:

```bash
dotnet build magic.csproj
```

Expected: FAIL because `ProgressionContentRegistry.GetTraitDefsTyped()` and `CollectValidationErrorsTyped()` accessibility for the test are not yet correct. If `CollectValidationErrorsTyped()` is private, make the test use `ValidateTyped()` instead and assert no `custom_identity_trait` missing-trait error appears.

- [ ] **Step 3: Add trait bucket fields and lifecycle to `ProgressionContentRegistry`**

Modify `scripts/player/progression/ProgressionContentRegistry.cs`:

- Add `private GDictionary _traitDefs = new();`.
- Add `private readonly Dictionary<StringName, TraitDef> _traitDefIndex = new();`.
- Add `private readonly TraitContentRegistry _traitContentRegistry = new();`.
- Add public property:

```csharp
public GDictionary _trait_defs
{
    get => _traitDefs;
    set
    {
        _traitDefs = value ?? new GDictionary();
        SyncTypedDefinitionIndexes();
    }
}
```

- In `DisposeManagedRegistry()`, call `_traitContentRegistry.Dispose();`.
- In `Rebuild()`, after `_raceTraitContentRegistry.Rebuild()` bridge loading, call:

```csharp
_traitContentRegistry.Rebuild();
_traitDefs = ProjectTypedDictionary(_traitContentRegistry.GetTraitDefsTyped());
```

- In `ValidateTyped()`, append `_traitContentRegistry.Validate()`.
- In `ReplaceDefinitionBuckets(GDictionary sources)`, read `"trait_defs"`:

```csharp
_traitDefs = DuplicateDictionary(GetDictionary(sources, "trait_defs"));
```

- In `ClearRuntimeCaches()`, clear `_traitDefs` and `_traitDefIndex`.
- In `SyncTypedDefinitionIndexes()`, call `ReplaceTypedIndex(_traitDefIndex, _traitDefs);`.
- Add typed getter:

```csharp
public IReadOnlyDictionary<StringName, TraitDef> GetTraitDefsTyped()
{
    SyncTypedDefinitionIndexes();
    return CloneTypedDictionary(_traitDefIndex);
}
```

- [ ] **Step 4: Switch identity trait reference validation to generic trait defs**

In `_append_trait_reference_errors(...)`, replace `_raceTraitDefIndex.ContainsKey(traitId)` with `_traitDefIndex.TryGetValue(traitId, out TraitDef traitDef)` and add source-scope validation:

```csharp
if (!_traitDefIndex.TryGetValue(traitId, out TraitDef traitDef))
{
    errors.Add($"{ownerLabel} {fieldLabel} references missing trait {traitId}.");
    continue;
}
if (!TraitContentRules.IsSourceKindAllowed(traitDef, TraitSourceKind.Identity))
{
    errors.Add($"{ownerLabel} {fieldLabel} references trait {traitId} that does not allow identity source.");
}
```

Keep `_append_race_trait_phase2_errors(...)` and `_raceTraitDefIndex` bridge logic unchanged in Phase 1.

- [ ] **Step 5: Run focused registry tests**

Run:

```bash
dotnet build magic.csproj
godot --headless -s res://tests/runtime/validation/run_progression_content_registry_typed_regression.cs
godot --headless -s res://tests/progression/identity/run_trait_content_registry_regression.cs
godot --headless -s res://tests/progression/identity/run_race_trait_content_registry_regression.cs
```

Expected: build PASS; all three regressions PASS.

- [ ] **Step 6: Commit Task 3**

```bash
git add scripts/player/progression/ProgressionContentRegistry.cs \
  tests/runtime/validation/run_progression_content_registry_typed_regression.cs
git commit -m "feat: wire trait defs into progression registry"
```

---

### Task 4: Expose Trait Definitions Through GameSession And GameContentCatalog

**Files:**
- Modify: `scripts/systems/persistence/GameSession.cs`
- Modify: `scripts/systems/content/GameContentCatalog.cs`
- Test: `tests/runtime/validation/run_game_root_content_catalog_regression.cs`

**Interfaces:**
- Consumes: `ProgressionContentRegistry.GetTraitDefsTyped()`.
- Produces: `GameSession.GetTraitDefsTyped(): IReadOnlyDictionary<StringName, TraitDef>`.
- Produces: `GameContentCatalog.GetTraitDefsTyped(): IReadOnlyDictionary<StringName, TraitDef>`.

- [ ] **Step 1: Add failing catalog assertions**

Modify `tests/runtime/validation/run_game_root_content_catalog_regression.cs`:

- In the initial catalog ownership test, assert `catalog.GetTraitDefsTyped().Count == gameSession.GetTraitDefsTyped().Count`.
- In the defensive read-only test, attempt downcast mutation on `catalog.GetTraitDefsTyped()` the same way skill/item views are tested and assert the catalog count does not change.
- In invalidation after session dispose, assert `catalog.GetTraitDefsTyped().Count == 0`.

Use the same helper style already present in that test file.

- [ ] **Step 2: Run build to verify failure**

Run:

```bash
dotnet build magic.csproj
```

Expected: FAIL because `GameSession.GetTraitDefsTyped()` and `GameContentCatalog.GetTraitDefsTyped()` do not exist.

- [ ] **Step 3: Add `GameSession.GetTraitDefsTyped()`**

Modify `scripts/systems/persistence/GameSession.cs` near the other typed content getters:

```csharp
public IReadOnlyDictionary<StringName, TraitDef> GetTraitDefsTyped()
{
    return _progression_content_registry?.GetTraitDefsTyped()
        ?? new Dictionary<StringName, TraitDef>();
}
```

The file already uses `System.Collections.Generic`; keep the method next to `GetSkillDefsTyped()` / `GetProfessionDefsTyped()`.

- [ ] **Step 4: Cache trait defs in `GameContentCatalog`**

Modify `scripts/systems/content/GameContentCatalog.cs`:

- Add field:

```csharp
private IReadOnlyDictionary<StringName, TraitDef> _traitDefs;
```

- In `ResetSnapshot()`, add:

```csharp
_traitDefs = EmptyTyped<TraitDef>();
```

- In `Rebuild(GameSession session)`, after `_progressionIdentityCatalog`, add:

```csharp
_traitDefs = SnapshotTyped(session.GetTraitDefsTyped());
```

- Add getter:

```csharp
public IReadOnlyDictionary<StringName, TraitDef> GetTraitDefsTyped() => _traitDefs;
```

- [ ] **Step 5: Run catalog tests**

Run:

```bash
dotnet build magic.csproj
godot --headless -s res://tests/runtime/validation/run_game_root_content_catalog_regression.cs
```

Expected: build PASS; regression PASS.

- [ ] **Step 6: Commit Task 4**

```bash
git add scripts/systems/persistence/GameSession.cs \
  scripts/systems/content/GameContentCatalog.cs \
  tests/runtime/validation/run_game_root_content_catalog_regression.cs
git commit -m "feat: expose trait defs through content catalog"
```

---

### Task 5: Wire TraitContentRegistry Into Content Validation

**Files:**
- Modify: `tests/runtime/validation/ContentValidationRunner.cs`
- Modify: `tests/runtime/validation/run_resource_validation_regression.cs`
- Test: `tests/runtime/validation/run_resource_validation_regression.cs`

**Interfaces:**
- Consumes: `TraitContentRegistry.GetTraitDefsTyped()`.
- Produces: official identity validation using `trait_defs` for Phase 2 identity `trait_ids` checks.
- Preserves: `RaceTraitContentRegistry` bridge validation until Phase 5.

- [ ] **Step 1: Add failing resource validation expectation**

In `tests/runtime/validation/run_resource_validation_regression.cs`, add an assertion that official resource validation includes trait content without errors and that invalid trait fixture validation reports errors. Use the same assertion style already used for race trait invalid fixture coverage.

- [ ] **Step 2: Run the validation regression to verify failure**

Run:

```bash
dotnet build magic.csproj
godot --headless -s res://tests/runtime/validation/run_resource_validation_regression.cs
```

Expected: FAIL because `ContentValidationRunner` does not yet load `TraitContentRegistry`.

- [ ] **Step 3: Update `ContentValidationRunner.ValidateIdentityContent(...)`**

Modify `tests/runtime/validation/ContentValidationRunner.cs`:

- Add a `traitDirectories` parameter to `ValidateIdentityDirectories(...)` after `raceTraitDirectories`.
- In `ValidateIdentityContent(...)`, pass `["res://data/configs/traits"]`.
- Add `using TraitContentRegistry traitRegistry = BuildTraitRegistry(traitDirectories);`.
- Add `AppendUniqueErrors(errors, traitRegistry.Validate());`.
- Pass `traitRegistry` into `PrepareIdentityPhase2Registry(...)`.

- [ ] **Step 4: Add `BuildTraitRegistry(...)`**

Add:

```csharp
private static TraitContentRegistry BuildTraitRegistry(string[] directoryPaths)
{
    TraitContentRegistry registry = new();
    registry.LoadFromDirectories(ToGodotStringArray(directoryPaths));
    return registry;
}
```

- [ ] **Step 5: Add `trait_defs` to `PrepareIdentityPhase2Registry(...)`**

Change the signature to include `TraitContentRegistry traitRegistry`. Add this entry to `ReplaceValidationSources(...)`:

```csharp
["trait_defs"] = ProjectDefs(traitRegistry.GetTraitDefsTyped()),
```

Keep the bridge entry:

```csharp
["race_trait_defs"] = ProjectDefs(raceTraitRegistry.GetRaceTraitDefsTyped()),
```

- [ ] **Step 6: Run validation regressions**

Run:

```bash
dotnet build magic.csproj
godot --headless -s res://tests/runtime/validation/run_resource_validation_regression.cs
godot --headless -s res://tests/runtime/validation/run_progression_content_registry_typed_regression.cs
```

Expected: build PASS; both regressions PASS.

- [ ] **Step 7: Commit Task 5**

```bash
git add tests/runtime/validation/ContentValidationRunner.cs \
  tests/runtime/validation/run_resource_validation_regression.cs
git commit -m "feat: validate generic trait content"
```

---

### Task 6: Update Context Map And Run Phase 1 Verification

**Files:**
- Modify: `docs/design/project_context_units.md`

**Interfaces:**
- Consumes: completed Phase 1 code.
- Produces: updated architecture loading index for future trait work.

- [ ] **Step 1: Update CU-02**

In `docs/design/project_context_units.md`, update CU-02 to mention:

- `GameContentCatalog` caches typed trait defs.
- `GameSession.GetTraitDefsTyped()` is the formal session getter for trait content.
- `ProgressionContentRegistry.GetTraitDefsTyped()` is the formal progression trait content source.

- [ ] **Step 2: Update CU-13**

In CU-13, update the progression content definition responsibility text to include:

- `TraitDef`
- `TraitContentRegistry`
- `data/configs/traits/*.tres`
- generic trait source scope validation through `TraitContentRules`

Do not put Phase 1 implementation status or task history into `project_context_units.md`.

- [ ] **Step 3: Run Phase 1 verification**

Run:

```bash
dotnet build magic.csproj
godot --headless -s res://tests/progression/schema/run_trait_content_rules_regression.cs
godot --headless -s res://tests/progression/identity/run_trait_content_registry_regression.cs
godot --headless -s res://tests/progression/identity/run_race_trait_content_registry_regression.cs
godot --headless -s res://tests/runtime/validation/run_progression_content_registry_typed_regression.cs
godot --headless -s res://tests/runtime/validation/run_game_root_content_catalog_regression.cs
godot --headless -s res://tests/runtime/validation/run_resource_validation_regression.cs
```

Expected: all commands PASS.

- [ ] **Step 4: Confirm excluded checks**

Do not run numeric battle simulation. Do not run Phase 2-4 tests because no save schema, equipment roll, attribute aggregation, or battle effective payload has been implemented.

- [ ] **Step 5: Commit Task 6**

```bash
git add docs/design/project_context_units.md
git commit -m "docs: update context map for trait content"
```

---

## Self-Review

**Spec coverage:** This plan covers Phase 1 content-layer requirements from the design docs: typed trait rules, `TraitDef`, roll schema resource, `TraitContentRegistry`, official migrated trait resources, progression registry bucket/index integration, content catalog snapshot integration, content validation runner integration, focused tests, and context map update.

**Intentional gaps:** This plan does not implement `TraitInstanceState`, equipment fixed/random traits, item template merge, save version bumps, `CharacterTraitService`, attribute service integration, battle effective payload, `TraitTriggerHooks` migration, or Phase 5 deletion of `RaceTraitDef`. Those are Phase 2-5 work.

**Bridge decision:** Phase 1 keeps `RaceTraitDef` and `RaceTraitContentRegistry` in place. This is not save compatibility logic; it is a temporary internal compile/runtime bridge for current battle trigger tests and character creation UI. Phase 5 deletes it after battle and UI callers move to generic traits.

**No placeholder scan:** The plan avoids open-ended implementation steps; each task has exact file paths, expected interfaces, commands, and pass/fail criteria.

**Type consistency:** `TraitDef`, `TraitContentRules`, `TraitContentRegistry`, `ProgressionContentRegistry.GetTraitDefsTyped()`, `GameSession.GetTraitDefsTyped()`, and `GameContentCatalog.GetTraitDefsTyped()` are named consistently across tasks.
