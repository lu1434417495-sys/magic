# Architecture analyzer tooling

`Magic.ArchitectureAnalyzers` is the semantic dependency-gate spike described by
`docs/proposals/migrations/code_structure_refactoring_plan.md`.

Current scope:

- loads exactly one `layer_rules.json` and `layer_baseline.json` from Roslyn
  `AdditionalFiles`;
- classifies declarations by ordered repository-relative path globs;
- fails with `MAGICARCH003` when any compiled C# file under `sourceRoot` is not
  classified;
- detects signature, inheritance, invocation, object-creation, member-access,
  conversion, generic-type, `typeof`, and `nameof` dependencies;
- deduplicates on `(rule, source documentation id, target documentation id)`;
- reports partial types declared across multiple layers;
- can emit the complete cross-layer semantic inventory as opt-in
  `MAGICARCH100` info diagnostics;
- fails closed when either configuration is missing, duplicated, malformed, or
  unsupported.

Run the isolated semantic checks:

```powershell
dotnet run --project tools/architecture/Magic.ArchitectureAnalyzers.Tests/Magic.ArchitectureAnalyzers.Tests.csproj
```

The analyzer, rules, and reviewed baseline are referenced by `magic.csproj`, so
the normal game build rejects newly forbidden dependencies:

```powershell
dotnet build magic.csproj
```

Generate the complete current repository inventory on demand:

```powershell
$architectureInventoryTargetPath = (
  Resolve-Path tools/architecture/Magic.ArchitectureInventory.targets
).Path
$architectureInventorySarifPath = Join-Path (
  Resolve-Path .
).Path "tools/architecture/.inventory/dependency-inventory.sarif"
$architectureInventoryJsonPath = Join-Path (
  Resolve-Path .
).Path "tools/architecture/.inventory/dependency-inventory.json"

Remove-Item -LiteralPath $architectureInventorySarifPath -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath $architectureInventoryJsonPath -Force -ErrorAction SilentlyContinue

dotnet build magic.csproj -t:Rebuild `
  "-p:CustomAfterMicrosoftCommonTargets=$architectureInventoryTargetPath" `
  "-p:MagicArchitectureInventorySarif=$architectureInventorySarifPath" `
  -clp:ErrorsOnly
if ($LASTEXITCODE -ne 0) {
  throw "Architecture inventory build failed with exit code $LASTEXITCODE."
}

python tools/architecture/export_inventory.py `
  $architectureInventorySarifPath `
  $architectureInventoryJsonPath
if ($LASTEXITCODE -ne 0) {
  throw "Architecture inventory export failed with exit code $LASTEXITCODE."
}
```

`Magic.ArchitectureInventory.targets` injects only the opt-in inventory request
for the `magic` project; the analyzer, rules, and baseline already come from
`magic.csproj`. Generated SARIF/JSON files live under the ignored
`tools/architecture/.inventory/` directory. The exporter
rejects configuration, ownership, unclassified-path, and compiler failures; it
allows `MAGICARCH001` only so a pre-baseline discovery build can still be
converted into a reviewable inventory, and rejects SARIF with no
`MAGICARCH100` entries. The command deletes both generated outputs before the
build and stops before export on any nonzero build result. `Rebuild` is required
because an up-to-date `Build` may skip `CoreCompile`; the target deletes the
requested old SARIF before reference resolution, so a skipped compiler cannot
silently feed the exporter a stale inventory.

The reviewed 2026-07-22 snapshot contains 39,341 cross-layer symbol pairs and
172 forbidden pairs. All 172 are exact baseline tuples: 72 child-to-composition
root dependencies, 61 pending-reward DTO owner dependencies, 30 runtime-to-
authoring dependencies, and 9 authoring-to-runtime dependencies. A baseline
entry is not a directory exemption: it suppresses only one
`(rule, source symbol, target symbol)` tuple, while `MAGICARCH100` continues to
report and annotate it.

The inventory is complete for the semantic reference kinds implemented by the
analyzer; the deny-rule surface is intentionally limited to boundaries that can
currently be classified with high confidence. A cross-layer pair that is not
forbidden is not automatically an endorsed architecture direction—the full
inventory remains the review source for expanding rules later.

`misplaced_progression_state` is a temporary quarantine layer for exactly
`PendingCharacterReward.cs` and `PendingCharacterRewardEntry.cs`. It makes the
save graph's physical owner error visible without banning every normal
`domain_state -> domain_runtime` call. Remove the layer and its baseline tuples
when those DTO files move to the player state/schema owner. Symbol overrides
serve the other unavoidable mixed-file case: a Godot authoring `Resource` and
a small runtime-facing enum or DTO currently share one source file. Keep those
overrides type-specific; do not reclassify the whole authoring file.

Do not generate baseline entries from every diagnostic. First correct path
classification and the small set of mixed-file symbol overrides, then inspect
each remaining owner relationship in current code. New debt must not be added;
when an owner is fixed, remove its now-stale exact tuples.

The analyzer tests run as an explicit CI step, while the existing main-project
build executes the dependency gate. `magic.csproj` also excludes
`tools/architecture/**/*.cs`, so the tool and its generated sources cannot leak
into the Godot game assembly.
