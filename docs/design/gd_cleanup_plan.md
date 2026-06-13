# Pure GD-Type Cleanup Plan

**Target**: Remove `Variant`, `GodotObject`, `Godot.Collections`, and `[GlobalClass]` usage from `scripts/` that exists purely for Godot interop, without changing any functionality.

**Rule**: DO NOT add new features. DO NOT refactor logic. Only replace GD types with C# equivalents at internal boundaries. If a file must read from a Godot `.tres` or `.tscn` boundary, that is NOT a target — only internal call chains that unnecessarily propagate GD types are targets.

---

## Phase 1: Zero-Risk Deletion (no callers, no functional impact)

### Step 1.1: Remove dead `[GlobalClass]` from 3 classes

These classes are never referenced by GDScript or `.tscn`. Remove `[GlobalClass]` attribute.

| File | Action |
|------|--------|
| `scripts/systems/battle/core/AttackPreviewData.cs` | Delete `[GlobalClass]` line |
| `scripts/systems/battle/core/BattlePreview.cs` | Delete `[GlobalClass]` line |
| `scripts/systems/battle/ai/BattleAiScoreProfile.cs` | Delete `[GlobalClass]` line |

Verification: `grep -r "AttackPreviewData\|BattlePreview\|BattleAiScoreProfile" tests/ scenes/ --include="*.gd" --include="*.tscn"` — expect zero results except for dead code strings.

### Step 1.2: Delete 5 dead extension methods from `GodotVariantReadExtensions.cs`

These methods have zero callers across the entire codebase:

| Method to delete | Lines | Search pattern to confirm dead |
|------------------|-------|-------------------------------|
| `TryAsGodotArray` | 41-50 | `grep TryAsGodotArray scripts/ tests/` → 0 results |
| `TryAsVector2I` | 95-104 | `grep TryAsVector2I scripts/ tests/` → 0 results |
| `TryAsBool` | 106-115 | `grep TryAsBool scripts/ tests/` → only the definition itself |
| `GetValueOrDefault` on GDictionary | 9-16 | `grep GetValueOrDefault scripts/ tests/` → only .NET Dictionary calls, not this extension |
| Private `TryRead` + `ToVariant` | 117-175 | Only used by `GetValueOrDefault` above |

**Action**: In `scripts/utils/GodotVariantReadExtensions.cs`, delete lines 9-16 (`GetValueOrDefault`), 41-50 (`TryAsGodotArray`), 95-104 (`TryAsVector2I`), 106-115 (`TryAsBool`), and 117-175 (`TryRead` + `ToVariant`). Keep `TryAsObject`, `TryAsDictionary`, `TryAsInt`, `TryAsString`, `TryAsStringName` — these are used by Phase 2.

---

## Phase 2: Inline `GodotVariantReadExtensions` Call Sites, Then Delete

The file `scripts/utils/GodotVariantReadExtensions.cs` has 18 remaining call sites across 8 files. Each call site is a trivial 3-line pattern. Replace each with the inline equivalent, then delete the file.

### Step 2.1: Replace `TryAsDictionary` call sites (11 occurrences, ~33 lines to replace)

**Pattern to search**: `\.TryAsDictionary\(`

**Files to modify**:

| File | Occurrences |
|------|-------------|
| `scripts/systems/progression/CharacterManagementModule.cs` | 2 |
| `scripts/ui/SettlementWindow.cs` | 4 |
| `scripts/systems/world/WorldMapFogSystem.cs` | 2 |
| `scripts/ui/CharacterInfoWindow.cs` | 2 |
| `scripts/ui/PartyManagementWindow.cs` | 1 |
| `scripts/ui/ShopWindow.cs` | 1 |

**Transformation**:

Replace:
```csharp
if (!value.TryAsDictionary(out GDictionary dict))
    return fallback;
```
With:
```csharp
if (value.VariantType != Variant.Type.Dictionary)
    return fallback;
GDictionary dict = value.AsGodotDictionary();
```

Replace:
```csharp
if (value.TryAsDictionary(out GDictionary dict))
{
    // use dict
}
```
With:
```csharp
if (value.VariantType == Variant.Type.Dictionary)
{
    GDictionary dict = value.AsGodotDictionary();
    // use dict
}
```

### Step 2.2: Replace `TryAsObject` call sites (2 occurrences)

**File**: `scripts/systems/progression/CharacterManagementModule.cs` (lines ~2794, ~3229)

**Pattern to search**: `\.TryAsObject<`

**Transformation**:

Replace:
```csharp
if (!value.TryAsObject<T>(out T result))
    return null;
```
With:
```csharp
if (value.VariantType != Variant.Type.Object || value.AsGodotObject() is not T result)
    return null;
```

### Step 2.3: Replace `TryAsInt` call sites (2 occurrences)

**File 1**: `scripts/systems/game_runtime/GameRuntimeSettlementCommandHandler.cs` — line ~3915, inside private `TryAsInt(object, out int)`
**File 2**: `scripts/systems/world/EncounterRosterBuilder.cs` — line ~1318, inside private `TryAsStrictInt(object, out int)`

**Pattern to search**: `\.TryAsInt\(out`

**Transformation**:

Replace:
```csharp
if (!value.TryAsInt(out int result))
    return false;
result = 0;
```
With:
```csharp
if (value.VariantType != Variant.Type.Int)
    return false;
result = value.AsInt32();
```

### Step 2.4: Replace `TryAsString` call site (1 occurrence)

**File**: `scripts/systems/game_runtime/GameRuntimeSettlementCommandHandler.cs` — line ~3783, inside private `TryAsString(object, out string)`

**Transformation**:

Replace:
```csharp
if (!value.TryAsString(out result))
    return false;
```
With:
```csharp
if (value.VariantType == Variant.Type.String)
    result = value.AsString();
else if (value.VariantType == Variant.Type.StringName)
    result = value.AsStringName().ToString();
else
    return false;
```

### Step 2.5: Replace `TryAsStringName` call site (1 occurrence)

**File**: `scripts/systems/game_runtime/GameRuntimeSettlementCommandHandler.cs` — line ~3864

**Transformation**:

Replace:
```csharp
if (!value.TryAsStringName(out result))
    return false;
```
With:
```csharp
if (value.VariantType == Variant.Type.StringName)
    result = value.AsStringName();
else if (value.VariantType == Variant.Type.String)
    result = new StringName(value.AsString());
else
    return false;
```

### Step 2.6: Delete `GodotVariantReadExtensions.cs`

After all 18 call sites are inlined, delete `scripts/utils/GodotVariantReadExtensions.cs` (176 lines).

**Verification**: `grep -r "GodotVariantReadExtensions\|TryAsObject\|TryAsDictionary\|TryAsInt\|TryAsString\|TryAsStringName" scripts/ tests/ --include="*.cs"` — should return 0 results.

---

## Phase 3: Reduce Duplicate `TryAsXxx` Private Helpers

15 files have their own private `TryAsXxx` methods duplicating the same Variant-unwrapping pattern. Consolidate them to reduce Variant occurrences.

### Step 3.1: Audit each file's private helpers

For each file below, check whether its private `TryAsXxx` methods can be converted to use direct type checks instead of `dynamic` try-catch:

| File | Uses `dynamic` pattern? | Action |
|------|------------------------|--------|
| `BattleCellState.cs` | Yes — TryAsVector2I, TryAsStrictInt, TryAsBool, TryAsDictionary all use `dynamic` try-catch | Replace with direct `VariantType` checks |
| `BattleResolutionResult.cs` | Yes — TryAsInt, TryAsVector2I use `dynamic` | Replace with direct `VariantType` checks |
| `BattleSpecialProfileManifestValidator.cs` | Yes — TryAsInt, TryAsDictionary use `dynamic` | Replace with direct `VariantType` checks |
| `AttributeService.cs` | Has TryAsObject<T>, TryAsInt — check implementation | Convert to direct checks if not already |

**Transformation** (for `dynamic` patterns):

Replace:
```csharp
private static bool TryAsInt(object value, out int result)
{
    try { result = (int)(dynamic)value; return true; }
    catch { result = 0; return false; }
}
```
With:
```csharp
private static bool TryAsInt(object value, out int result)
{
    if (value is Variant v && v.VariantType == Variant.Type.Int)
    {
        result = v.AsInt32();
        return true;
    }
    result = 0;
    return false;
}
```

This replaces ~42 `dynamic` usage sites with explicit `VariantType` checks, reducing reliance on Godot's dynamic dispatch.

---

## Phase 4: Remove Unnecessary `using GDictionary = Godot.Collections.Dictionary` Aliases

### Step 4.1: Audit files that use `GDictionary` alias only in internal code (not at Godot boundary)

Many files import `using GDictionary = Godot.Collections.Dictionary` but only pass typed data internally. The alias can be replaced with C# typed equivalents.

**Files to prioritize** (based on largest reduction):

| File | GDictionary occurrences | Replace with |
|------|------------------------|--------------|
| `BattleAiScoreService.Scoring.cs` | XX | Check if `context.skill_defs` is now `IReadOnlyDictionary<StringName, SkillDef>` — if yes, many GDictionary usages were already migrated. Remove remaining alias if unused. |
| `BattleAiScoreService.Helpers.cs` | XX | Same audit |
| `BattleAiScoreService.Position.cs` | XX | Same audit |
| `BattleAiScoreService.Effects.cs` | XX | Same audit |
| `BattleAiScoreService.Projection.cs` | XX | Same audit |
| `BattleAiContext.cs` | XX | `skill_defs` property still returns GDictionary — check if callers have migrated to the typed `IBattleAiScoreContext.skill_defs` |

**Action for each file**:
1. Search for `GDictionary` usage in the file
2. If every occurrence is at a Godot boundary (e.g., `to_dict()`, `from_dict()`), keep the alias — it's serving its purpose
3. If occurrences are in internal method parameters/returns, replace with `IReadOnlyDictionary<StringName, T>` or `Dictionary<StringName, T>`
4. If NO `GDictionary` usage remains after replacement, delete the `using GDictionary = ...` alias line

---

## Phase 5: Batch `[GlobalClass]` Audit (remaining 192 classes)

With 195 `[GlobalClass]` attributes remaining, batch-audit which can be safely removed.

### Step 5.1: Script to identify candidates

Run this verification for each class with `[GlobalClass]`:

```bash
# For each class named Foo in Foo.cs:
grep -r "\bFoo\b" tests/ scenes/ --include="*.gd" --include="*.tscn" | grep -v "Foo.cs" | grep -v "^Binary"
```

If only matches are in C# files (not .gd, not .tscn), the class is a **candidate** for `[GlobalClass]` removal.

### Step 5.2: Safe candidates (already verified as dead code)

| Class | File |
|-------|------|
| `AttackPreviewData` | `scripts/systems/battle/core/AttackPreviewData.cs` |
| `BattlePreview` | `scripts/systems/battle/core/BattlePreview.cs` |
| `BattleAiScoreProfile` | `scripts/systems/battle/ai/BattleAiScoreProfile.cs` |

### Step 5.3: MUST keep (extensively used by GDScript)

| Class | Reason |
|-------|--------|
| `BattleCommand` | Used bare-class-name in 10+ GDScript files |
| `BattleEventBatch` | Used bare-class-name in GDScript |
| Any class under `scripts/enemies/actions/` | Instantiated via GDScript at runtime |
| Any `*Def` classes | Loaded from `.tres` resource files |
| Any `*State` classes | Serialized into Godot dictionaries |

---

## Execution Order Summary

| Phase | Steps | Risk | Lines saved |
|-------|-------|------|-------------|
| **1.1** | Remove 3 dead `[GlobalClass]` | Zero | 3 |
| **1.2** | Delete 5 dead methods from `GodotVariantReadExtensions` | Zero | ~100 |
| **2** | Inline 18 call sites + delete `GodotVariantReadExtensions.cs` | Low | ~176 file + ~40 inline = ~216 |
| **3** | Replace `dynamic` patterns in 4 files | Low | ~42 |
| **4** | Remove unused `using GDictionary` aliases | Medium | Variable |
| **5** | Batch `[GlobalClass]` audit | Medium | Variable |

**Total guaranteed savings**: ~361 lines of GD compatibility code removed.

**Key insight for Phase 4-5**: These require case-by-case judgment. Execute Phase 1-3 first, then report remaining numbers before proceeding.
