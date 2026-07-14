# Chain Contingency V1 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox syntax for tracking; implementation and verification steps are checked when local evidence exists, while git checkpoint steps remain explicitly deferred until the final commit scope is confirmed.

**Goal:** 落地完整 V1 连锁应急术：玩家可在战斗外配置、充能和清除自用应急矩阵，战斗中由固定 hook 自动释放预存法术，并在战后可靠写回消耗状态。

**Architecture:** 持久状态只放在 `PartyMemberState` 的 setup schema 中；战斗内监听、触发、释放队列、压制和 consumed overlay 全部由 battle-local `BattleContingencySystem` sidecar 承担。充能、清除和写回分别通过窄服务和事务快照处理，自动施法通过内部 `AutoCastRequest` 路径进入既有技能效果结算，不复用玩家主动指令链。

**Tech Stack:** Godot 4.6, C#, Godot Resource `.tres`, typed DTO/service, headless C# regression runners, existing `GameSession` / `GameRuntimeFacade` / `CharacterManagementModule` / `BattleRuntimeModule` architecture.

## Execution Status

- Status date: 2026-06-23.
- Functional status: V1 implementation and documentation context updates are complete in the local working tree.
- Verification evidence:
  - `dotnet build magic.csproj`: PASS.
  - `python tests/run_regression_suite.py --jobs 16 --finalizer-crash-retries 0 --stop-on-failure`: PASS, repeated 10/10; each run reported `Passed: 284 Failed: 0`.
  - `git diff --check`: PASS.
  - Remaining-item closure: `dotnet build magic.csproj` + `godot --headless -s res://tests/battle_runtime/runtime/run_contingency_damage_hook_contract_regression.cs`: PASS after source/damage event id separation, fatal blink damage cancellation, and execute / graded-save execute hook-context propagation.
- Lifecycle status: lifecycle regressions now rely on owned `Dispose()` / `using` / `try/finally` cleanup and do not use broad `try/catch` to hide test failures.
- Git status: checkpoint commits are deferred; no commit has been created from this execution document yet.

## Global Constraints

- Source design: `docs/discussions/chain_contingency_data_structure.md`
- Source PRD: `docs/discussions/chain_contingency_prd.json`
- Context map: `docs/design/project_context_units.md`
- V1 release gate: full trigger set, player UI, headless commands, real `mage_chain_contingency` skill resource, quantity-aware warehouse API, auto-cast origin suppression, damage hooks, and failure rollback all complete.
- Partial release disallowed: no non-damage-trigger-only gameplay slice, no headless-only setup flow, no fixture-only skill, no material quantity expansion instead of quantity-aware API, no battle settlement without rollback.
- Save schema target: current root save `9` and `PartyState.version` `5` upgrade to root save `10` and `PartyState.version` `6`.
- Compatibility policy: no old payload compatibility, no legacy alias, no fallback migration, no default-filled old contingency payload.
- Save load policy: after content catalog is available, illegal contingency setup content fails the load.
- Runtime save policy: V1 does not support battle-time save and does not persist battle-local instance, release context, queue, suppression state, or consumed overlay.
- Ownership policy: V1 is strict self-use; owner, caster, and source party member are the same character.
- Resource policy: charged setup reserves max MP through effective MP calculation; raw `mp_max` remains owned by attributes, growth, equipment, and effects.
- Transaction policy: charge and battle finalization either fully commit or restore the pre-command / pre-finalization memory snapshot.
- Testing policy: write focused regression first for each task, run the narrowest relevant runner, then run broader build/regression gates after integration.

---

## Required Context Units

- CU-02 Save / Session / Registry: save version, `GameSession`, catalog, content validation.
- CU-06 Runtime 总编排: runtime command, battle finalization, persist / flush rollback.
- CU-09 队伍管理窗口: player-facing party UI entry.
- CU-10 背包 / 装备 / 物品: warehouse material debit and quantity-aware batch API.
- CU-11 队伍与成员状态模型: `PartyState`, `PartyMemberState`, duplicated state, battle writeback.
- CU-12 CharacterManagement 桥接: command-facing progression service and battle gateway.
- CU-13 Progression 内容定义: skill resources and automation content schema.
- CU-14 Progression 规则与属性服务: effective MP, current MP clamp, snapshot refresh.
- CU-15 战斗运行时总编排: battle lifecycle, event batch, runtime sidecar setup.
- CU-16 战斗规则 / AI / 伤害: damage projection, hook suppression, auto-cast effect origin.
- CU-18 战斗展示: HUD / report projection if status display reaches battle UI.
- CU-19 回归与截图辅助: runner placement and focused coverage.
- CU-21 Headless runtime: text commands and structured snapshots.

## File Map

### Create

- `scripts/player/progression/ContingencyAutomationDef.cs`: typed skill automation profile imported from skill content.
- `scripts/player/progression/ContingencyMatrixSetupState.cs`: root persistent setup state for one party member.
- `scripts/player/progression/ContingencyTriggerState.cs`: typed trigger state with strict parameter validation.
- `scripts/player/progression/ContingencyTargetResolverState.cs`: typed resolver state with strict parameter validation.
- `scripts/player/progression/ContingencyStoredSpellEntryState.cs`: typed stored spell entry.
- `scripts/player/progression/ContingencyMaterialCostState.cs`: typed charge receipt / material cost entry.
- `scripts/systems/progression/ContingencyContentValidator.cs`: catalog-aware validator used during save load and setup mutation.
- `scripts/systems/progression/PartyContingencySetupService.cs`: battle-outside save/edit/clear/status/charge mutation service.
- `scripts/systems/progression/ContingencySetupMutationResult.cs`: stable result DTO for UI and headless commands.
- `scripts/systems/inventory/WarehouseBatchQuantityEntry.cs`: quantity-aware item entry.
- `scripts/systems/inventory/WarehouseBatchSwapResult.cs`: preview / commit result for quantity-aware warehouse batch mutations.
- `scripts/systems/battle/core/AutoCastRequest.cs`: internal automatic spell execution request.
- `scripts/systems/battle/core/BattleEffectOrigin.cs`: origin facts that suppress recursive contingency triggers.
- `scripts/systems/battle/core/ContingencyReleaseContext.cs`: battle-local release context and source facts.
- `scripts/systems/battle/core/ContingencyFrozenTriggerFacts.cs`: typed immutable trigger facts used by target resolution and reports.
- `scripts/systems/battle/core/ContingencyTargetResolutionResult.cs`: typed target resolution result consumed by auto-cast.
- `scripts/systems/battle/runtime/BattleContingencySystem.cs`: battle-local sidecar that owns instances, hook matching, release queue, suppression, consumed overlay.
- `scripts/systems/battle/runtime/ContingencyTargetResolverService.cs`: battle-local resolver service for units, cells, areas, and safe-cell scoring.
- `scripts/systems/battle/rules/DamageApplicationProjection.cs`: no-side-effect projection used before shield / HP mutation.
- `scripts/systems/battle/rules/IBattleDamageApplicationHook.cs`: narrow hook implemented by `BattleContingencySystem`.
- `scenes/ui/contingency_setup_window.tscn`: battle-outside player setup / charge / clear UI.
- `scripts/ui/ContingencySetupWindow.cs`: UI adapter that calls `PartyContingencySetupService`.
- `data/configs/skills/mage_chain_contingency.tres`: real chain contingency skill content.
- `tests/progression/schema/run_contingency_setup_schema_regression.cs`
- `tests/progression/schema/run_contingency_content_validator_regression.cs`
- `tests/progression/run_effective_mp_reservation_regression.cs`
- `tests/warehouse/run_party_warehouse_quantity_batch_regression.cs`
- `tests/progression/run_contingency_charge_transaction_regression.cs`
- `tests/text_runtime/commands/run_contingency_text_commands_regression.cs`
- `tests/world_map/ui/run_contingency_setup_window_regression.cs`
- `tests/battle_runtime/runtime/run_contingency_battle_lifecycle_regression.cs`
- `tests/battle_runtime/runtime/run_contingency_autocast_origin_regression.cs`
- `tests/battle_runtime/runtime/run_contingency_target_resolver_regression.cs`
- `tests/battle_runtime/runtime/run_contingency_trigger_contract_regression.cs`
- `tests/battle_runtime/rules/run_damage_application_projection_regression.cs`
- `tests/battle_runtime/runtime/run_contingency_damage_hook_contract_regression.cs`

### Modify

- `scripts/player/progression/SkillDef.cs`: add or surface automation profile for skill content.
- `scripts/player/progression/CombatSkillDef.cs`: carry automation profile if combat skills are the formal stored spell source.
- `data/configs/skills/mage_mirror_image.tres`: V1 storable defensive self-buff profile.
- `data/configs/skills/mage_rock_armor.tres`: V1 storable defensive mitigation profile.
- `data/configs/skills/mage_magic_shield.tres`: V1 storable shield / magic defense profile.
- `data/configs/skills/mage_blink.tres`: V1 storable mobility profile using `empty_cell_near_owner`.
- `data/configs/skills/mage_thunderclap.tres`: V1 storable owner-centered area damage profile.
- `data/configs/skills/priest_aid.tres`: V1 storable support / shield profile for characters who legally know it.
- `scripts/systems/content/GameContentCatalog.cs`: expose current typed skill catalog to contingency content validator.
- `scripts/systems/persistence/SaveSerializer.cs`: strict schema acceptance for new save / party versions.
- `scripts/systems/persistence/GameSession.cs`: save version gate, load-time content validator call, real skill content availability.
- `scripts/player/progression/PartyState.cs`: bump version and include party-level validation path if current owner requires it.
- `scripts/player/progression/PartyMemberState.cs`: own `contingency_matrix_setups` persistence and duplication.
- `scripts/systems/attributes/AttributeSourceContext.cs`: accept `reserved_mp_max`.
- `scripts/systems/attributes/AttributeService.cs`: expose `mp_max_unreserved`, `reserved_mp_max`, and effective `mp_max`.
- `scripts/systems/progression/CharacterManagementModule.cs`: setup service gateway, effective MP clamp, consumed writeback.
- `scripts/systems/progression/CharacterBattleWritebackService.cs`: battle settlement ordering and result reporting.
- `scripts/systems/inventory/PartyWarehouseService.cs`: quantity-aware preview / commit and transaction state capture / restore.
- `scripts/systems/game_runtime/GameRuntimeFacade.cs`: command wiring and battle finalization rollback.
- `scripts/systems/game_runtime/WorldMapSystem.cs`: party UI opening, modal wiring, refresh.
- `scripts/ui/PartyManagementWindow.cs`: entry button / status indicator for contingency setup.
- `scenes/ui/party_management_window.tscn`: add player-facing entry point.
- `scripts/systems/game_runtime/headless/GameTextCommandRunner.cs`: text commands for save/edit/clear/status/charge.
- `scripts/systems/game_runtime/headless/HeadlessGameTestSession.cs`: test helpers for setup flow and battle assertions.
- `scripts/utils/GameTextSnapshotRenderer.cs`: structured contingency status and battle report snapshot.
- `scripts/systems/battle/runtime/BattleRuntimeModule.cs`: sidecar lifecycle, hook emission, end battle consumed writeback.
- `scripts/systems/battle/runtime/BattleRuntimeServices.cs`: service ownership if sidecar is centralized there.
- `scripts/systems/battle/runtime/BattleUnitFactory.cs`: initial effective MP and overlay refresh for battle units.
- `scripts/systems/battle/runtime/BattleSkillExecutionOrchestrator.cs`: internal auto-cast execution path.
- `scripts/systems/battle/rules/BattleDamageResolver.cs`: projection helper, hook, suppress flag, cancellation / modification semantics.
- `scripts/systems/battle/core/DamageApplicationInput.cs`: suppress hook and resolved damage replacement.
- `docs/design/project_context_units.md`: update after code files are added, not for this document-only change.

## Core Interfaces

### Closed Domains

```csharp
internal enum ContingencyTriggerKind
{
    CombatStarted,
    HpBelowPercent,
    IncomingDamagePercent,
    FatalDamageIncoming,
    StatusApplied,
    EnemyEnterRadius,
    AffectedBySpell,
    OwnerTurnStarted
}

internal enum ContingencyTimingKind
{
    OnBattleConfirmed,
    AfterHpChanged,
    BeforeDamageResolved,
    AfterStatusApplied,
    AfterUnitPositionChanged,
    AfterSpellAffected,
    OwnerTurnStarted
}

internal enum ContingencyReleaseModeKind
{
    BurstRelease,
    SequentialRelease
}

internal enum ContingencyTargetResolverKind
{
    Self,
    TriggerSource,
    TriggerTarget,
    NearestEnemyToOwner,
    NearestEnemyToTriggerCell,
    OwnerCenteredArea,
    AttackerCell,
    EmptyCellNearOwner
}

internal enum ContingencyEmptyCellPreferenceKind
{
    AwayFromTriggerSource,
    SafeCell
}

internal enum ContingencyFallbackPolicyKind
{
    SkipIfInvalid,
    AbortRemainingIfInvalid
}

internal enum ContingencyEffectCategoryKind
{
    DefensiveSelfBuff,
    Mobility,
    Cleanse,
    Healing,
    Shield,
    Area,
    Damage,
    Control,
    StrongControl
}
```

### Target Resolvers (Authoritative)

This section is the authoritative V1 resolver contract. It replaces the looser `safe_cell` wording in earlier design discussion: `safe_cell` is a preference / UI unlock label, not a persisted `target_resolver.type`.

| Persisted `target_resolver.type` | Exact fields | Output shape |
|---|---|---|
| `self` | `type` | owner live unit |
| `trigger_source` | `type` | source unit from frozen trigger facts |
| `trigger_target` | `type` | target unit from frozen trigger facts |
| `nearest_enemy_to_owner` | `type` | hostile unit nearest to owner current cell |
| `nearest_enemy_to_trigger_cell` | `type` | hostile unit nearest to frozen trigger cell |
| `owner_centered_area` | `type` | owner current cell as area anchor |
| `attacker_cell` | `type` | source / attacker cell from frozen damage or attack facts |
| `empty_cell_near_owner` | `type`, `preference`, `max_distance` | best legal empty cell near owner |

`empty_cell_near_owner.preference` accepts exactly:

```text
away_from_trigger_source
safe_cell
```

`empty_cell_near_owner.max_distance` must be an integer `1..8`. V1 does not support `fixed_cell`, battle-local saved coordinates, saved paths, saved cell arrays, or arbitrary best-target AI. Any persisted resolver outside this table is a schema error. Any content profile that omits a resolver used by a setup is a content validation error.

`empty_cell_near_owner` scoring must always choose the highest-scoring legal cell when at least one legal cell exists. Hard legality is: empty, standable, placeable, inside battle bounds, not blocked by current unit occupancy or terrain. Scoring priorities are: outside current damage area, away from trigger source, not dangerous terrain, not adjacent to hostile unit, near allied unit, and moderate distance from original owner cell. If no legal cell exists, the stored spell fails according to its fallback policy; the matrix remains consumed if release context already exists.

### Persistent State

```csharp
internal sealed class ContingencyMatrixSetupState
{
    public StringName SetupId { get; }
    public string DisplayName { get; }
    public bool Enabled { get; }
    public bool Charged { get; }
    public StringName SourceSkillId { get; }
    public int SourceSkillLevel { get; }
    public int MatrixLoad { get; }
    public int ReservedMpMax { get; }
    public ContingencyTriggerState Trigger { get; }
    public ContingencyReleaseModeKind ReleaseMode { get; }
    public IReadOnlyList<ContingencyStoredSpellEntryState> StoredSpells { get; }
    public IReadOnlyList<ContingencyMaterialCostState> MaterialCosts { get; }
}
```

Required `PartyMemberState` surface:

```csharp
internal IReadOnlyList<ContingencyMatrixSetupState> GetContingencySetupsTyped();
internal bool TryGetContingencySetupTyped(StringName setupId, out ContingencyMatrixSetupState setup);
internal int GetTotalReservedMpMax();
internal int GetChargedContingencySetupCount();
internal PartyMemberState WithContingencySetupsForMutation(IReadOnlyList<ContingencyMatrixSetupState> setups);
```

### Content Validation

```csharp
internal sealed class ContingencyContentValidator
{
    internal ContingencyContentValidationResult ValidateSetup(
        ContingencyMatrixSetupState setup,
        PartyMemberState owner,
        SkillCatalogTyped skillCatalog);

    internal ContingencyContentValidationResult ValidateAllSetupsForSaveLoad(
        PartyState partyState,
        SkillCatalogTyped skillCatalog);
}
```

### Warehouse Quantity API

```csharp
internal readonly struct WarehouseBatchQuantityEntry
{
    public readonly StringName ItemId;
    public readonly int Quantity;
}

internal sealed class PartyWarehouseService
{
    internal WarehouseBatchSwapResult PreviewBatchQuantitySwapTyped(
        IReadOnlyList<WarehouseBatchQuantityEntry> itemsToWithdraw,
        IReadOnlyList<WarehouseBatchQuantityEntry> itemsToDeposit);

    internal WarehouseBatchSwapResult CommitBatchQuantitySwapTyped(
        IReadOnlyList<WarehouseBatchQuantityEntry> itemsToWithdraw,
        IReadOnlyList<WarehouseBatchQuantityEntry> itemsToDeposit);

    internal WarehouseState CaptureWarehouseStateForTransaction();
    internal void RestoreWarehouseStateForTransaction(WarehouseState snapshot);
}
```

### Setup Service

```csharp
internal sealed class PartyContingencySetupService
{
    internal ContingencySetupMutationResult SaveSetup(ContingencySetupSaveRequest request);
    internal ContingencySetupMutationResult ClearCharge(ContingencySetupClearChargeRequest request);
    internal ContingencySetupMutationResult ChargeSetup(ContingencySetupChargeRequest request);
    internal ContingencySetupStatusResult BuildStatus(ContingencySetupStatusRequest request);
}
```

The `ChargeSetup` request references an existing setup only:

```csharp
internal sealed class ContingencySetupChargeRequest
{
    public StringName MemberId { get; }
    public StringName SetupId { get; }
    public bool BattleMutationBlocked { get; }
}
```

### Battle Auto-Cast

```csharp
internal sealed class AutoCastRequest
{
    public StringName CasterUnitId { get; }
    public StringName OwnerMemberId { get; }
    public StringName StoredSkillId { get; }
    public int CastLevel { get; }
    public BattleEffectOrigin Origin { get; }
    public ContingencyTargetResolutionResult Target { get; }
    public IReadOnlyDictionary<StringName, Variant> ParameterBindings { get; }
}

internal readonly struct BattleEffectOrigin
{
    public readonly StringName OriginKind;
    public readonly bool CanTriggerContingencies;
    public readonly long SourceEventId;
    public readonly long DamageEventId;
}
```

### Target Resolution Service

```csharp
internal sealed class ContingencyTargetResolverService
{
    internal ContingencyTargetResolutionResult ResolveTarget(
        ContingencyTargetResolutionRequest request);
}

internal sealed class ContingencyTargetResolutionRequest
{
    public BattleState BattleState { get; }
    public ContingencyTargetResolverState Resolver { get; }
    public StringName OwnerMemberId { get; }
    public StringName OwnerUnitId { get; }
    public ContingencyFrozenTriggerFacts SourceFacts { get; }
    public StringName StoredSkillId { get; }
    public int CastLevel { get; }
}

internal sealed class ContingencyTargetResolutionResult
{
    public bool Ok { get; }
    public StringName ReasonId { get; }
    public StringName TargetUnitId { get; }
    public Vector2I TargetCell { get; }
    public bool IsGroundTarget { get; }
    public IReadOnlyList<Vector2I> AreaCells { get; }
    public bool MovedOutsideCurrentDamageEvent { get; }
}
```

`MovedOutsideCurrentDamageEvent` is only meaningful for `fatal_damage_incoming` / `BeforeDamageResolved` flows after resolving `empty_cell_near_owner`; the damage hook rechecks the current source event before cancelling damage.

### Damage Hook

```csharp
internal interface IBattleDamageApplicationHook
{
    BeforeDamageResolvedResult BeforeDamageResolved(BeforeDamageResolvedContext context);
}

internal sealed class DamageApplicationInput
{
    public bool SuppressDamageApplicationHook { get; }
    public DamageApplicationInput WithResolvedDamage(int resolvedDamage);
}
```

## Task 1: Persistent Schema And Save Version

**Files:**
- Create: `scripts/player/progression/ContingencyMatrixSetupState.cs`
- Create: `scripts/player/progression/ContingencyTriggerState.cs`
- Create: `scripts/player/progression/ContingencyTargetResolverState.cs`
- Create: `scripts/player/progression/ContingencyStoredSpellEntryState.cs`
- Create: `scripts/player/progression/ContingencyMaterialCostState.cs`
- Modify: `scripts/player/progression/PartyMemberState.cs`
- Modify: `scripts/player/progression/PartyState.cs`
- Modify: `scripts/systems/persistence/SaveSerializer.cs`
- Modify: `scripts/systems/persistence/GameSession.cs`
- Test: `tests/progression/schema/run_contingency_setup_schema_regression.cs`

**Interfaces:**
- Consumes: no previous contingency code.
- Produces: `ContingencyMatrixSetupState`, strict `FromDictionary`, strict `ToDictionary`, `DuplicateState`, `PartyMemberState.GetContingencySetupsTyped()`, root save `10`, `PartyState.version` `6`.

- [x] **Step 1: Write failing schema regression**

```text
Test cases:
1. current-version party payload accepts one uncharged setup with exact keys.
2. current-version party payload accepts one charged setup with material receipt and reserved_mp_max > 0.
3. charged=false with reserved_mp_max > 0 fails.
4. charged=false with non-empty material_costs fails.
5. two charged setups on one member fail.
6. missing contingency_matrix_setups fails for current PartyState.version 6.
7. unknown field inside setup / trigger / resolver / stored spell / material cost fails.
8. all authoritative resolver types parse with exact fields.
9. persisted target_resolver.type=safe_cell fails; safe_cell is only an empty_cell_near_owner.preference.
10. old root save 9 or PartyState.version 5 fails without migration.
```

Run: `godot --headless -s res://tests/progression/schema/run_contingency_setup_schema_regression.cs`
Expected: FAIL because state classes and version gates do not exist.

- [x] **Step 2: Implement state classes**

Use immutable or copy-on-write plain C# state. Boundary dictionary conversion must be exact:

```text
setup_id, display_name, enabled, charged, source_skill_id, source_skill_level,
matrix_load, reserved_mp_max, material_costs, trigger, release_mode, stored_spells
```

All closed values parse through enum converters. `parameter_bindings` accepts only bool, int, float, String/StringName, and Array[StringName].

- [x] **Step 3: Add party/member persistence**

Add `contingency_matrix_setups` to `PartyMemberState.ToDictionary`, `FromDictionary`, `DuplicateState`, and typed accessors. Add party version gate to reject `PartyState.version != 6`.

- [x] **Step 4: Bump save version**

Update the owning root save version to `10`. Do not add migration from `9` to `10`.

- [x] **Step 5: Run focused regression**

Run: `godot --headless -s res://tests/progression/schema/run_contingency_setup_schema_regression.cs`
Expected: PASS.

- [x] **Step 6: Build**

Run: `dotnet build magic.csproj`
Expected: PASS.

- [ ] **Step 7: Commit checkpoint (deferred)**

```bash
git add scripts/player/progression scripts/systems/persistence tests/progression/schema/run_contingency_setup_schema_regression.cs
git commit -m "feat: add contingency setup save schema"
```

## Task 2: Content Automation And Load-Time Validator

**Files:**
- Create: `scripts/player/progression/ContingencyAutomationDef.cs`
- Create: `scripts/systems/progression/ContingencyContentValidator.cs`
- Create: `data/configs/skills/mage_chain_contingency.tres`
- Modify: `data/configs/skills/mage_mirror_image.tres`
- Modify: `data/configs/skills/mage_rock_armor.tres`
- Modify: `data/configs/skills/mage_magic_shield.tres`
- Modify: `data/configs/skills/mage_blink.tres`
- Modify: `data/configs/skills/mage_thunderclap.tres`
- Modify: `data/configs/skills/priest_aid.tres`
- Modify: `scripts/player/progression/SkillDef.cs`
- Modify: `scripts/player/progression/CombatSkillDef.cs`
- Modify: `scripts/systems/content/GameContentCatalog.cs`
- Modify: `scripts/systems/persistence/GameSession.cs`
- Test: `tests/progression/schema/run_contingency_content_validator_regression.cs`

**Interfaces:**
- Consumes: `ContingencyMatrixSetupState`.
- Produces: `ContingencyAutomationDef`, `ContingencyContentValidator.ValidateAllSetupsForSaveLoad(...)`, real `mage_chain_contingency`.

- [x] **Step 1: Write failing content regression**

```text
Test cases:
1. catalog contains real mage_chain_contingency.
2. catalog contains V1 storable skills `mage_mirror_image`, `mage_rock_armor`, `mage_magic_shield`, `mage_blink`, `mage_thunderclap`, and `priest_aid` with automation profiles.
3. stored skill without automation profile is rejected.
4. can_be_stored_in_contingency=false is rejected.
5. min_contingency_skill_level greater than source skill level is rejected.
6. forbidden tag intersection is rejected before allowlist success.
7. target resolver not present in allowed_target_resolvers is rejected.
8. unsupported parameter binding key is rejected.
9. save load fails when a persisted setup references an invalid stored skill.
```

Run: `godot --headless -s res://tests/progression/schema/run_contingency_content_validator_regression.cs`
Expected: FAIL because content schema and validator do not exist.

- [x] **Step 2: Implement automation content**

Expose typed fields:

```text
can_be_stored_in_contingency
min_contingency_skill_level
effect_category
tags
contingency_load_override
allowed_target_resolvers
requires_manual_targeting
```

Skills without this profile default to not storable.

- [x] **Step 3: Add real chain contingency skill resource**

Create `data/configs/skills/mage_chain_contingency.tres` with the special setup identity, `contingency` / `meta_spell` tags, and content needed by the validator. It must be registered through the same catalog path as other formal skill resources.

- [x] **Step 4: Add V1 storable skill profiles**

Add `ContingencyAutomationDef` profiles to these real resources:

| Skill resource | Purpose | Required automation profile |
|---|---|---|
| `data/configs/skills/mage_mirror_image.tres` | defensive self-buff | `can_be_stored_in_contingency=true`, `min_contingency_skill_level=1`, `effect_category=defensive_self_buff`, `allowed_target_resolvers=[self]`, `requires_manual_targeting=false` |
| `data/configs/skills/mage_rock_armor.tres` | mitigation self/ally buff | `can_be_stored_in_contingency=true`, `min_contingency_skill_level=1`, `effect_category=defensive_self_buff`, `allowed_target_resolvers=[self]`, `requires_manual_targeting=false` |
| `data/configs/skills/mage_magic_shield.tres` | shield / magic defense | `can_be_stored_in_contingency=true`, `min_contingency_skill_level=3`, `effect_category=shield`, `allowed_target_resolvers=[self]`, `requires_manual_targeting=false` |
| `data/configs/skills/mage_blink.tres` | mobility / fatal escape | `can_be_stored_in_contingency=true`, `min_contingency_skill_level=7`, `effect_category=mobility`, `allowed_target_resolvers=[empty_cell_near_owner]`, `requires_manual_targeting=false` |
| `data/configs/skills/mage_thunderclap.tres` | owner-centered area damage | `can_be_stored_in_contingency=true`, `min_contingency_skill_level=5`, `effect_category=damage`, `allowed_target_resolvers=[owner_centered_area]`, `requires_manual_targeting=false` |
| `data/configs/skills/priest_aid.tres` | support shield for legal multiclass owners | `can_be_stored_in_contingency=true`, `min_contingency_skill_level=3`, `effect_category=shield`, `allowed_target_resolvers=[owner_centered_area]`, `requires_manual_targeting=false` |

The validator must still require the owner to know each stored skill. Listing a skill here only makes it content-eligible; it does not bypass the strict self-use owner/caster rule.

- [x] **Step 5: Wire load-time validation**

After `GameContentCatalog` has the typed skill catalog for a loaded save, call `ContingencyContentValidator.ValidateAllSetupsForSaveLoad(...)`. A validator failure aborts save load and surfaces a stable failure reason.

- [x] **Step 6: Run focused regression**

Run: `godot --headless -s res://tests/progression/schema/run_contingency_content_validator_regression.cs`
Expected: PASS.

- [x] **Step 7: Build**

Run: `dotnet build magic.csproj`
Expected: PASS.

- [ ] **Step 8: Commit checkpoint (deferred)**

```bash
git add scripts/player/progression scripts/systems/progression scripts/systems/content scripts/systems/persistence data/configs/skills/mage_chain_contingency.tres data/configs/skills/mage_mirror_image.tres data/configs/skills/mage_rock_armor.tres data/configs/skills/mage_magic_shield.tres data/configs/skills/mage_blink.tres data/configs/skills/mage_thunderclap.tres data/configs/skills/priest_aid.tres tests/progression/schema/run_contingency_content_validator_regression.cs
git commit -m "feat: validate contingency skill content"
```

## Task 3: Effective MP Reservation

**Files:**
- Modify: `scripts/systems/attributes/AttributeSourceContext.cs`
- Modify: `scripts/systems/attributes/AttributeService.cs`
- Modify: `scripts/systems/progression/CharacterManagementModule.cs`
- Modify: `scripts/systems/progression/CharacterBattleWritebackService.cs`
- Modify: `scripts/systems/battle/runtime/BattleUnitFactory.cs`
- Test: `tests/progression/run_effective_mp_reservation_regression.cs`

**Interfaces:**
- Consumes: `PartyMemberState.GetTotalReservedMpMax()`.
- Produces: `AttributeSourceContext.reserved_mp_max`, `mp_max_unreserved`, effective `mp_max`, consistent current MP clamp.

- [x] **Step 1: Write failing MP regression**

```text
Test cases:
1. raw mp_max 30 with reserved_mp_max 12 yields mp_max_unreserved 30, reserved_mp_max 12, mp_max 18.
2. charging clamps current_mp from 25 to 18.
3. clearing charge raises mp_max to 30 and leaves current_mp unchanged.
4. restore/rest uses effective mp_max, not raw mp_max.
5. battle unit generated from charged member uses effective mp_max.
6. battle resource writeback clamps against post-consumption effective mp_max after consumed setup is released.
```

Run: `godot --headless -s res://tests/progression/run_effective_mp_reservation_regression.cs`
Expected: FAIL because effective MP reservation is not wired.

- [x] **Step 2: Extend attribute source**

Add `reserved_mp_max` to `AttributeSourceContext` and include it in the snapshot build input.

- [x] **Step 3: Build effective snapshot**

Calculate:

```text
mp_max_unreserved = raw final mp_max before contingency reservation
reserved_mp_max = max(sum charged setup reservation, 0)
mp_max = max(mp_max_unreserved - reserved_mp_max, 0)
```

- [x] **Step 4: Replace MP clamp callers**

Update current MP clamps in charge, clear, rest / recovery, progression refresh, equipment refresh, battle unit generation, battle refresh, and battle writeback to use effective `mp_max`.

- [x] **Step 5: Run focused regression**

Run: `godot --headless -s res://tests/progression/run_effective_mp_reservation_regression.cs`
Expected: PASS.

- [x] **Step 6: Build**

Run: `dotnet build magic.csproj`
Expected: PASS.

- [ ] **Step 7: Commit checkpoint (deferred)**

```bash
git add scripts/systems/attributes scripts/systems/progression scripts/systems/battle/runtime tests/progression/run_effective_mp_reservation_regression.cs
git commit -m "feat: reserve max mp for contingency charge"
```

## Task 4: Quantity-Aware Warehouse API

**Files:**
- Create: `scripts/systems/inventory/WarehouseBatchQuantityEntry.cs`
- Create: `scripts/systems/inventory/WarehouseBatchSwapResult.cs`
- Modify: `scripts/systems/inventory/PartyWarehouseService.cs`
- Test: `tests/warehouse/run_party_warehouse_quantity_batch_regression.cs`

**Interfaces:**
- Consumes: existing `WarehouseState` and item stack model.
- Produces: `PreviewBatchQuantitySwapTyped(...)`, `CommitBatchQuantitySwapTyped(...)`, `CaptureWarehouseStateForTransaction()`, `RestoreWarehouseStateForTransaction(...)`.

- [x] **Step 1: Write failing warehouse regression**

```text
Test cases:
1. preview withdraws item_id with quantity 3 from one stack and reports success.
2. preview fails when total quantity across stacks is below requested quantity.
3. commit withdraws exact total quantity and preserves unrelated items.
4. commit is atomic when one requested item is insufficient.
5. deposit can be previewed and committed with explicit quantity.
6. capture and restore returns warehouse state to the exact pre-commit quantities.
```

Run: `godot --headless -s res://tests/warehouse/run_party_warehouse_quantity_batch_regression.cs`
Expected: FAIL because quantity-aware API does not exist.

- [x] **Step 2: Implement typed result and entry**

`WarehouseBatchQuantityEntry.Quantity` must be `> 0`; invalid entries return a stable error result and do not mutate state.

- [x] **Step 3: Implement preview**

Preview must scan current warehouse quantities, build a deterministic result, and not mutate `WarehouseState`.

- [x] **Step 4: Implement commit**

Commit must internally preview first, then mutate only after the full batch is known valid. It must not expand one quantity into N single-item entries.

- [x] **Step 5: Implement capture / restore**

Capture returns a deep duplicated `WarehouseState`. Restore replaces the service-owned state consistently with current service view expectations and does not persist, signal, or log.

- [x] **Step 6: Run focused regression**

Run: `godot --headless -s res://tests/warehouse/run_party_warehouse_quantity_batch_regression.cs`
Expected: PASS.

- [x] **Step 7: Build**

Run: `dotnet build magic.csproj`
Expected: PASS.

- [ ] **Step 8: Commit checkpoint (deferred)**

```bash
git add scripts/systems/inventory tests/warehouse/run_party_warehouse_quantity_batch_regression.cs
git commit -m "feat: add quantity-aware warehouse batches"
```

## Task 5: Setup Service And Charge Transaction

**Files:**
- Create: `scripts/systems/progression/PartyContingencySetupService.cs`
- Create: `scripts/systems/progression/ContingencySetupMutationResult.cs`
- Modify: `scripts/systems/progression/CharacterManagementModule.cs`
- Modify: `scripts/systems/game_runtime/GameRuntimeFacade.cs`
- Test: `tests/progression/run_contingency_charge_transaction_regression.cs`

**Interfaces:**
- Consumes: `ContingencyContentValidator`, quantity-aware warehouse API, effective MP snapshot.
- Produces: `SaveSetup`, `ClearCharge`, `ChargeSetup`, `BuildStatus`, stable result codes.

- [x] **Step 1: Write failing transaction regression**

```text
Test cases:
1. material insufficient leaves warehouse, setup, and current_mp unchanged.
2. content validator failure leaves warehouse, setup, and current_mp unchanged.
3. forced setup write failure after warehouse commit restores warehouse.
4. successful charge deducts material, sets charged=true, stores material receipt, sets reserved_mp_max, clamps current_mp.
5. clear charge sets charged=false, reserved_mp_max=0, material_costs=[], does not refund materials, and does not increase current_mp.
6. runtime persist failure restores command-start PartyState and runtime service references.
7. charge request with inline trigger / stored spell payload is rejected; charge references existing setup only.
```

Run: `godot --headless -s res://tests/progression/run_contingency_charge_transaction_regression.cs`
Expected: FAIL because setup service does not exist.

- [x] **Step 2: Implement service mutation flow**

For `ChargeSetup`:

```text
1. reject battle mutation path.
2. load existing setup by member_id and setup_id.
3. validate content, trigger, resolver, stored spells, material cost, matrix load, and reserved MP.
4. preview material quantity through PartyWarehouseService.
5. capture warehouse, setup list, current_mp.
6. commit material quantity.
7. replace setup with charged copy and material receipt.
8. rebuild effective MP and clamp current_mp.
9. verify postconditions.
10. on any failure restore all captured state and return stable error_code.
```

- [x] **Step 3: Implement save and clear**

`SaveSetup` writes uncharged configuration only. `ClearCharge` releases reserved MP, clears charge receipt, keeps material spent, and does not restore current MP.

- [x] **Step 4: Wire runtime command rollback**

Wrap service calls from runtime commands in a command-level snapshot. If party persistence or flush fails, restore session/runtime party state, world/runtime snapshot, selected member, and service references.

- [x] **Step 5: Run focused regression**

Run: `godot --headless -s res://tests/progression/run_contingency_charge_transaction_regression.cs`
Expected: PASS.

- [x] **Step 6: Build**

Run: `dotnet build magic.csproj`
Expected: PASS.

- [ ] **Step 7: Commit checkpoint (deferred)**

```bash
git add scripts/systems/progression scripts/systems/game_runtime tests/progression/run_contingency_charge_transaction_regression.cs
git commit -m "feat: transact contingency setup charge"
```

## Task 6: Player UI And Headless Commands

**Files:**
- Create: `scenes/ui/contingency_setup_window.tscn`
- Create: `scripts/ui/ContingencySetupWindow.cs`
- Modify: `scripts/ui/PartyManagementWindow.cs`
- Modify: `scenes/ui/party_management_window.tscn`
- Modify: `scripts/systems/game_runtime/WorldMapSystem.cs`
- Modify: `scripts/systems/game_runtime/headless/GameTextCommandRunner.cs`
- Modify: `scripts/systems/game_runtime/headless/HeadlessGameTestSession.cs`
- Modify: `scripts/utils/GameTextSnapshotRenderer.cs`
- Test: `tests/text_runtime/commands/run_contingency_text_commands_regression.cs`
- Test: `tests/world_map/ui/run_contingency_setup_window_regression.cs`

**Interfaces:**
- Consumes: `PartyContingencySetupService`.
- Produces: battle-outside player setup window and text commands for save/edit/clear/status/charge.

- [x] **Step 1: Write failing headless command regression**

```text
Commands:
1. party contingency status <member>
2. party contingency save <member> <setup-payload-name>
3. party contingency charge <member> <setup-id>
4. party contingency clear <member> <setup-id>
5. party contingency edit <member> <setup-payload-name>

Assertions:
1. status snapshot includes charged, reserved_mp_max, material quantity, trigger, release_mode, stored_spells.
2. charge and clear report stable code and reason_id.
3. editing charged setup fails with reason_id requiring clear first.
4. all mutation commands fail while battle mutation is locked.
```

Run: `godot --headless -s res://tests/text_runtime/commands/run_contingency_text_commands_regression.cs`
Expected: FAIL because commands do not exist.

- [x] **Step 2: Write failing UI regression**

```text
UI assertions:
1. PartyManagementWindow exposes a contingency setup entry for eligible member.
2. ContingencySetupWindow can show uncharged and charged states.
3. charged state disables direct edit and exposes clear-charge confirmation.
4. clear-charge confirmation states material is not refunded and current MP is not restored.
5. save, charge, and clear call PartyContingencySetupService rather than mutating PartyMemberState directly.
```

Run: `godot --headless -s res://tests/world_map/ui/run_contingency_setup_window_regression.cs`
Expected: FAIL because UI does not exist.

- [x] **Step 3: Implement headless commands**

Route all command mutations through `PartyContingencySetupService`. Snapshot output uses stable fields; tests do not parse localized text.

- [x] **Step 4: Implement UI**

Use compact controls inside the party management flow:

```text
member status
trigger selector
release mode selector
stored spell list
target resolver selector
matrix load / reserved MP preview
material cost preview
save
charge
clear charge
```

No nested card layout. The UI must block battle-time mutation using the same service result codes as headless.

- [x] **Step 5: Run focused regressions**

Run: `godot --headless -s res://tests/text_runtime/commands/run_contingency_text_commands_regression.cs`
Expected: PASS.

Run: `godot --headless -s res://tests/world_map/ui/run_contingency_setup_window_regression.cs`
Expected: PASS.

- [x] **Step 6: Build**

Run: `dotnet build magic.csproj`
Expected: PASS.

- [ ] **Step 7: Commit checkpoint (deferred)**

```bash
git add scenes/ui/contingency_setup_window.tscn scenes/ui/party_management_window.tscn scripts/ui scripts/systems/game_runtime scripts/utils tests/text_runtime/commands/run_contingency_text_commands_regression.cs tests/world_map/ui/run_contingency_setup_window_regression.cs
git commit -m "feat: add contingency setup UI and commands"
```

## Task 7: Battle Settlement Rollback

**Files:**
- Modify: `scripts/systems/progression/CharacterManagementModule.cs`
- Modify: `scripts/systems/progression/CharacterBattleWritebackService.cs`
- Modify: `scripts/systems/battle/runtime/BattleRuntimeModule.cs`
- Modify: `scripts/systems/game_runtime/GameRuntimeFacade.cs`
- Test: `tests/battle_runtime/runtime/run_contingency_battle_lifecycle_regression.cs`

**Interfaces:**
- Consumes: persistent setup state and effective MP clamp.
- Produces: consumed setup writeback before battle resource commit, finalization rollback.

- [x] **Step 1: Write failing lifecycle regression**

```text
Test cases:
1. battle consumed setup is written to member before CommitBattleResources clamps MP.
2. victory finalization persists charged=false after consumed release.
3. escape finalization persists charged=false after consumed release when current battle rules commit escape state.
4. retry / loss path leaves charged setup unchanged.
5. dead member follows death rule and does not run contingency special writeback.
6. forced CommitContingencyConsumedSetups failure restores finalization-start memory.
7. forced FlushGameState failure restores finalization-start memory and returns retryable failure.
```

Run: `godot --headless -s res://tests/battle_runtime/runtime/run_contingency_battle_lifecycle_regression.cs`
Expected: FAIL because consumed writeback does not exist.

- [x] **Step 2: Add writeback method**

Expose:

```csharp
internal ContingencyConsumedCommitResult CommitContingencyConsumedSetups(
    StringName memberId,
    IReadOnlyCollection<StringName> consumedSetupIds);
```

It clears charge, reserved MP, and material receipt for consumed setups without refunding material or increasing current MP.

- [x] **Step 3: Order EndBattle**

For living members:

```text
1. commit consumed contingency setups.
2. rebuild effective MP.
3. commit battle resources using the post-consumption effective snapshot.
```

- [x] **Step 4: Guard finalization**

`GameRuntimeFacade.FinalizeBattleResolution()` captures memory snapshot before writing. On any consumed writeback, resource commit, party set, world persist, or flush failure, restore the snapshot and return stable failure.

- [x] **Step 5: Run focused regression**

Run: `godot --headless -s res://tests/battle_runtime/runtime/run_contingency_battle_lifecycle_regression.cs`
Expected: PASS.

- [x] **Step 6: Build**

Run: `dotnet build magic.csproj`
Expected: PASS.

- [ ] **Step 7: Commit checkpoint (deferred)**

```bash
git add scripts/systems/progression scripts/systems/battle/runtime scripts/systems/game_runtime tests/battle_runtime/runtime/run_contingency_battle_lifecycle_regression.cs
git commit -m "feat: rollback contingency battle settlement"
```

## Task 8: Battle-Local Sidecar And Overlay

**Files:**
- Create: `scripts/systems/battle/runtime/BattleContingencySystem.cs`
- Create: `scripts/systems/battle/core/ContingencyReleaseContext.cs`
- Modify: `scripts/systems/battle/runtime/BattleRuntimeModule.cs`
- Modify: `scripts/systems/battle/runtime/BattleRuntimeServices.cs`
- Modify: `scripts/systems/battle/runtime/BattleUnitFactory.cs`
- Modify: `scripts/utils/GameTextSnapshotRenderer.cs`
- Test: `tests/battle_runtime/runtime/run_contingency_trigger_contract_regression.cs`

**Interfaces:**
- Consumes: charged setup state from active party members.
- Produces: battle-local instances, consumed overlay, suppression state, trigger queue foundation.

- [x] **Step 1: Write failing sidecar regression**

```text
Test cases:
1. active charged member creates one battle-local instance.
2. reserve member with charged setup creates no battle-local instance and keeps party reservation.
3. instance stores owner_member_id and owner_unit_id, with caster_unit_id equal to owner_unit_id.
4. entering release_context marks setup consumed in overlay and refreshes owner effective MP for battle.
5. battle-local instance is absent from BattleUnitState.ToDictionary and save payload.
6. uncommitted battle disposal drops overlay and leaves PartyMemberState unchanged.
```

Run: `godot --headless -s res://tests/battle_runtime/runtime/run_contingency_trigger_contract_regression.cs`
Expected: FAIL because sidecar does not exist.

- [x] **Step 2: Build sidecar lifecycle**

`BattleRuntimeModule` creates and disposes the sidecar with the battle runtime. Sidecar reads setup state during battle setup, then owns battle-local state only.

- [x] **Step 3: Add consumed overlay**

Overlay records consumed setup IDs by member. Attribute refresh sees released reservation for battle-local owner without mutating persistent `PartyMemberState`.

- [x] **Step 4: Add first hook loop**

Implement `combat_started` / `OnBattleConfirmed` and internal `owner_turn_started` for sequential release queue progression. This is a debugging and foundation loop, not a release slice.

- [x] **Step 5: Run focused regression**

Run: `godot --headless -s res://tests/battle_runtime/runtime/run_contingency_trigger_contract_regression.cs`
Expected: PASS for sidecar foundation cases.

- [x] **Step 6: Build**

Run: `dotnet build magic.csproj`
Expected: PASS.

- [ ] **Step 7: Commit checkpoint (deferred)**

```bash
git add scripts/systems/battle scripts/utils tests/battle_runtime/runtime/run_contingency_trigger_contract_regression.cs
git commit -m "feat: add battle contingency sidecar"
```

## Task 9: Target Resolution Service

**Files:**
- Create: `scripts/systems/battle/core/ContingencyFrozenTriggerFacts.cs`
- Create: `scripts/systems/battle/core/ContingencyTargetResolutionResult.cs`
- Create: `scripts/systems/battle/runtime/ContingencyTargetResolverService.cs`
- Modify: `scripts/systems/battle/runtime/BattleContingencySystem.cs`
- Modify: `scripts/systems/battle/runtime/BattleRuntimeModule.cs`
- Test: `tests/battle_runtime/runtime/run_contingency_target_resolver_regression.cs`

**Interfaces:**
- Consumes: authoritative `ContingencyTargetResolverState`, frozen trigger facts, battle state, board occupancy / terrain query helpers.
- Produces: `ContingencyTargetResolverService.ResolveTarget(ContingencyTargetResolutionRequest request)` and `ContingencyTargetResolutionResult`.

- [x] **Step 1: Write failing target resolver regression**

```text
Test cases:
1. self resolves to the owner live unit and fails with reason_id=owner_unit_missing when owner has no live unit.
2. trigger_source resolves the frozen source unit and does not follow later source movement.
3. trigger_target resolves the frozen target unit and fails if the target is gone before release.
4. nearest_enemy_to_owner chooses the hostile unit nearest to owner current cell with deterministic tie-break by unit id.
5. nearest_enemy_to_trigger_cell chooses the hostile unit nearest to the frozen trigger cell with deterministic tie-break by unit id.
6. owner_centered_area returns owner current cell as ground anchor and the current affected area cells for the stored skill.
7. attacker_cell returns the frozen attacker/source cell from damage or attack facts.
8. empty_cell_near_owner rejects occupied, non-standable, blocked, and out-of-bounds cells.
9. empty_cell_near_owner preference=away_from_trigger_source chooses the highest-scoring legal cell away from the source inside max_distance.
10. empty_cell_near_owner preference=safe_cell chooses the highest-scoring legal cell outside current damage area when one exists.
11. empty_cell_near_owner still chooses the highest-scoring legal cell when no perfect safe cell exists.
12. empty_cell_near_owner returns reason_id=no_legal_cell when no legal cell exists; release context remains consumed if already entered.
13. fatal_damage_incoming plus empty_cell_near_owner marks MovedOutsideCurrentDamageEvent=true only when the resolved cell is outside the current damage event effective area.
```

Run: `godot --headless -s res://tests/battle_runtime/runtime/run_contingency_target_resolver_regression.cs`
Expected: FAIL because target resolver service does not exist.

- [x] **Step 2: Implement result and request DTOs**

`ContingencyTargetResolutionResult` must carry stable `Ok`, `ReasonId`, `TargetUnitId`, `TargetCell`, `IsGroundTarget`, `AreaCells`, and `MovedOutsideCurrentDamageEvent` fields. Do not expose mutable Godot dictionaries as the formal service result.

- [x] **Step 3: Implement unit and cell resolvers**

Implement all authoritative resolver types:

```text
self
trigger_source
trigger_target
nearest_enemy_to_owner
nearest_enemy_to_trigger_cell
owner_centered_area
attacker_cell
empty_cell_near_owner
```

Facts from `ContingencyFrozenTriggerFacts` are read-only for the current release. Later auto-cast movement, death, terrain mutation, or source displacement must not change the trigger reason or source cell used by the current resolver request.

- [x] **Step 4: Implement empty-cell legality and scoring**

Hard legality:

```text
empty
standable
placeable
inside battle bounds
not blocked by current unit occupancy
not blocked by terrain
distance from owner current cell <= max_distance
```

Score in this order, using stable deterministic tie-breaks:

```text
outside current damage area
farther from trigger source when preference=away_from_trigger_source
not dangerous terrain
not adjacent to hostile unit
near allied unit
moderate distance from original owner cell
cell coordinate order as final deterministic tie-break
```

- [x] **Step 5: Wire sidecar to use resolver service**

`BattleContingencySystem` asks `ContingencyTargetResolverService` for each stored spell before building an `AutoCastRequest`. If resolution fails, apply the stored spell fallback policy; if the release context already exists, do not un-consume the setup.

- [x] **Step 6: Run focused regression**

Run: `godot --headless -s res://tests/battle_runtime/runtime/run_contingency_target_resolver_regression.cs`
Expected: PASS.

- [x] **Step 7: Build**

Run: `dotnet build magic.csproj`
Expected: PASS.

- [ ] **Step 8: Commit checkpoint (deferred)**

```bash
git add scripts/systems/battle tests/battle_runtime/runtime/run_contingency_target_resolver_regression.cs
git commit -m "feat: resolve contingency targets"
```

## Task 10: Auto-Cast Execution And Origin Suppression

**Files:**
- Create: `scripts/systems/battle/core/AutoCastRequest.cs`
- Create: `scripts/systems/battle/core/BattleEffectOrigin.cs`
- Modify: `scripts/systems/battle/runtime/BattleSkillExecutionOrchestrator.cs`
- Modify: `scripts/systems/battle/runtime/BattleRuntimeModule.cs`
- Modify: `scripts/systems/battle/runtime/BattleContingencySystem.cs`
- Test: `tests/battle_runtime/runtime/run_contingency_autocast_origin_regression.cs`

**Interfaces:**
- Consumes: `ContingencyReleaseContext`, stored spell entries, `ContingencyTargetResolverService`, `ContingencyTargetResolutionResult`.
- Produces: internal `ExecuteAutoCast(AutoCastRequest, BattleEventBatch)` and non-recursive origin facts.

- [x] **Step 1: Write failing auto-cast regression**

```text
Test cases:
1. auto-cast does not require active turn owner or player command phase.
2. auto-cast does not consume AP, MP, stamina, aura, cooldown, identity charge, or spell-control resource.
3. auto-cast does not grant mastery or ordinary skill-used achievements.
4. auto-cast still applies target legality, range, LOS, hit, save, resistance, immunity, shield, mitigation, special profile, and effect commit.
5. damage/status/position/spell facts created by auto-cast carry CanTriggerContingencies=false.
6. contingency scanner ignores facts with CanTriggerContingencies=false.
```

Run: `godot --headless -s res://tests/battle_runtime/runtime/run_contingency_autocast_origin_regression.cs`
Expected: FAIL because auto-cast path does not exist.

- [x] **Step 2: Add request and origin**

`BattleEffectOrigin` must travel through effect commit facts. The default player command origin has `CanTriggerContingencies=true`; auto-cast origin has `false`.

- [x] **Step 3: Implement auto-cast path**

Add an internal execution method that bypasses command issuance and cost gates but reuses formal effect resolution. It must append to the current `BattleEventBatch`.

- [x] **Step 4: Wire sidecar release**

`BattleContingencySystem` creates `AutoCastRequest` entries from release context. Burst release executes in order immediately; sequential release executes one queued spell per owner-turn hook.

- [x] **Step 5: Run focused regression**

Run: `godot --headless -s res://tests/battle_runtime/runtime/run_contingency_autocast_origin_regression.cs`
Expected: PASS.

- [x] **Step 6: Build**

Run: `dotnet build magic.csproj`
Expected: PASS.

- [ ] **Step 7: Commit checkpoint (deferred)**

```bash
git add scripts/systems/battle tests/battle_runtime/runtime/run_contingency_autocast_origin_regression.cs
git commit -m "feat: execute contingency auto casts"
```

## Task 11: Non-Damage Hooks And Trigger Arbitration

**Files:**
- Modify: `scripts/systems/battle/runtime/BattleContingencySystem.cs`
- Modify: `scripts/systems/battle/runtime/BattleRuntimeModule.cs`
- Modify: status, movement, and spell-effect hook emitters in `scripts/systems/battle/runtime/` and `scripts/systems/battle/rules/`
- Modify: `scripts/utils/GameTextSnapshotRenderer.cs`
- Test: `tests/battle_runtime/runtime/run_contingency_trigger_contract_regression.cs`

**Interfaces:**
- Consumes: sidecar, auto-cast origin, release context.
- Produces: `hp_below_percent`, `status_applied`, `enemy_enter_radius`, `affected_by_spell`, stable source facts and trigger arbitration.

- [x] **Step 1: Extend failing trigger regression**

```text
Test cases:
1. hp_below_percent triggers after HP changed and only for owner.
2. status_applied triggers when owner receives configured status.
3. enemy_enter_radius triggers when hostile source enters owner radius.
4. affected_by_spell triggers for direct target and AoE affected owner.
5. owner summon / ally events do not trigger owner contingency.
6. hostile summon as source can trigger owner contingency.
7. same owner and same source_event_id produce one stable release queue.
8. source facts are frozen before auto-cast release begins.
```

Run: `godot --headless -s res://tests/battle_runtime/runtime/run_contingency_trigger_contract_regression.cs`
Expected: FAIL for newly added trigger cases.

- [x] **Step 2: Emit typed hook facts**

Add hook emissions at existing status, movement, HP change, and spell affected boundaries. Facts carry source event ID, owner / target IDs, source unit ID, origin, and timing kind.

- [x] **Step 3: Implement trigger indexes**

Sidecar keeps trigger-indexed candidate lists. Each hook scans only relevant kind candidates, checks origin, owner live gate, suppression, and deterministic tie-breaks.

- [x] **Step 4: Implement source fact freeze**

Before release begins, copy the hook facts needed by target resolvers and report entries. Auto-cast mutations cannot alter the trigger reason for the current release.

- [x] **Step 5: Run focused regression**

Run: `godot --headless -s res://tests/battle_runtime/runtime/run_contingency_trigger_contract_regression.cs`
Expected: PASS for non-damage triggers.

- [x] **Step 6: Build**

Run: `dotnet build magic.csproj`
Expected: PASS.

- [ ] **Step 7: Commit checkpoint (deferred)**

```bash
git add scripts/systems/battle scripts/utils tests/battle_runtime/runtime/run_contingency_trigger_contract_regression.cs
git commit -m "feat: trigger contingency from battle facts"
```

## Task 12: Damage Projection And BeforeDamageResolved

**Files:**
- Create: `scripts/systems/battle/rules/DamageApplicationProjection.cs`
- Create: `scripts/systems/battle/rules/IBattleDamageApplicationHook.cs`
- Modify: `scripts/systems/battle/core/DamageApplicationInput.cs`
- Modify: `scripts/systems/battle/rules/BattleDamageResolver.cs`
- Modify: `scripts/systems/battle/runtime/BattleRuntimeModule.cs`
- Modify: `scripts/systems/battle/runtime/BattleContingencySystem.cs`
- Test: `tests/battle_runtime/rules/run_damage_application_projection_regression.cs`
- Test: `tests/battle_runtime/runtime/run_contingency_damage_hook_contract_regression.cs`

**Interfaces:**
- Consumes: sidecar hook implementation and auto-cast executor.
- Produces: `ProjectDamageApplication(...)`, `BeforeDamageResolved`, `CancelDamage`, `ModifiedResolvedDamage`, `StateMayHaveChanged`, `incoming_damage_percent`, `fatal_damage_incoming`.

- [x] **Step 1: Write failing projection regression**

```text
Test cases:
1. hook null produces same applied shield / HP result as old resolver path.
2. shield_absorption_percent=50 uses projection values for actual shield drain and HP damage.
3. shield_absorption_percent=100 uses projection values for actual shield drain and HP damage.
4. CancelDamage returns hp_damage=0 and mutates neither shield nor HP.
5. ModifiedResolvedDamage recomputes projection before applying shield and HP.
6. StateMayHaveChanged recomputes projection after auto-cast side effects.
7. preview and AI scoring suppress hook and do not consume setup.
8. death ward / fatal trait / last stand / min_hp_after_damage semantics remain unchanged.
```

Run: `godot --headless -s res://tests/battle_runtime/rules/run_damage_application_projection_regression.cs`
Expected: FAIL because projection and hook do not exist.

- [x] **Step 2: Write failing hook contract regression**

```text
Test cases:
1. incoming_damage_percent can trigger before shield and HP mutation.
2. fatal_damage_incoming reads projected fatal state before death prevention.
3. hook cancel does not stop later effects in the same skill.
4. hook report entries are appended to BattleEventBatch and visible in headless snapshot.
5. damage=0 does not trigger contingency, on-hit status, or mastery side effects.
6. damage-hook reports expose distinct `source_event_id` and `damage_event_id`.
7. fatal blink that resolves outside the current damage area cancels only the triggering damage.
8. execute and graded-save execute damage branches preserve `DamageApplicationHookBatch` and auto-cast `BattleEffectOrigin`.
```

Run: `godot --headless -s res://tests/battle_runtime/runtime/run_contingency_damage_hook_contract_regression.cs`
Expected: FAIL because damage hook is not wired.

- [x] **Step 3: Extract no-side-effect projection**

Projection reads target state and `DamageApplicationInput` only. Actual mutation uses projection values and does not recompute shield drain independently.

- [x] **Step 4: Add suppress flag**

`DamageApplicationInput.SuppressDamageApplicationHook` is explicit. Preview, AI scoring, and clone paths set it to true. Do not infer suppression from clone identity.

- [x] **Step 5: Wire hook**

`BattleDamageResolver` calls hook only on live commit path and passes commit context with current batch, auto-cast executor, and report sink. Hook null preserves current behavior.

- [x] **Step 6: Implement cancellation and modification**

`CancelDamage=true` skips shield and HP mutation for the current damage effect only. `ModifiedResolvedDamage` calls `WithResolvedDamage(...)`, synchronizes typed field and payload `resolved_damage`, and recomputes projection.

- [x] **Step 7: Add damage triggers**

`BattleContingencySystem` handles `incoming_damage_percent` and `fatal_damage_incoming` through `BeforeDamageResolved`. Fatal uses `WouldBeFatalBeforeDeathPrevention`.

For `empty_cell_near_owner` fatal escape, `IBattleDamageApplicationHook` carries the current damage event area into `ContingencyFrozenTriggerFacts`; if an executed auto-cast moves the owner outside that area, the hook returns `CancelDamage` for the current damage effect. Pure ground relocation auto-casts count as applied when their precast relocation changes the caster coordinate.

- [x] **Step 8: Run focused regressions**

Run: `godot --headless -s res://tests/battle_runtime/rules/run_damage_application_projection_regression.cs`
Expected: PASS.

Run: `godot --headless -s res://tests/battle_runtime/runtime/run_contingency_damage_hook_contract_regression.cs`
Expected: PASS.

- [x] **Step 9: Build**

Run: `dotnet build magic.csproj`
Expected: PASS.

- [ ] **Step 10: Commit checkpoint (deferred)**

```bash
git add scripts/systems/battle tests/battle_runtime/rules/run_damage_application_projection_regression.cs tests/battle_runtime/runtime/run_contingency_damage_hook_contract_regression.cs
git commit -m "feat: resolve contingency damage hooks"
```

## Task 13: Reports, Snapshots, And Full V1 Acceptance

**Files:**
- Modify: `scripts/systems/battle/runtime/BattleContingencySystem.cs`
- Modify: `scripts/systems/battle/runtime/BattleRuntimeModule.cs`
- Modify: `scripts/utils/GameTextSnapshotRenderer.cs`
- Modify: `scripts/systems/game_runtime/headless/HeadlessGameTestSession.cs`
- Modify: `scripts/ui/ContingencySetupWindow.cs`
- Test: all contingency runners from this document.

**Interfaces:**
- Consumes: all previous task outputs.
- Produces: complete V1 report vocabulary, structured snapshots, full release gate evidence.

- [x] **Step 1: Add report assertions**

Structured report entries include:

```text
entry_type = contingency_triggered | contingency_suppressed | contingency_released | contingency_spell_skipped | contingency_depleted
decision
reason_id
owner_member_id
owner_unit_id
setup_id
source_event_id
damage_event_id
trigger_type
release_mode
stored_skill_id
target_resolver
```

For damage-hook reports, `source_event_id` identifies the contingency source event and `damage_event_id` identifies the triggering damage event; these IDs must be non-empty and distinct.

- [x] **Step 2: Add snapshot assertions**

Headless snapshots expose:

```text
party member: charged, reserved_mp_max, effective mp_max, setup status
battle unit: contingency state, suppressed flag, release queue count, consumed overlay
battle report: structured contingency entries
```

Structured snapshot paths include `battle.contingency`, unit overlay fields `contingency_state`, `contingency_suppressed`, `contingency_release_queue_count`, `consumed_contingency_setup_ids`, and structured entries under `battle.report_entries`.

- [x] **Step 3: Run all focused contingency regressions**

Run:

```bash
godot --headless -s res://tests/progression/schema/run_contingency_setup_schema_regression.cs
godot --headless -s res://tests/progression/schema/run_contingency_content_validator_regression.cs
godot --headless -s res://tests/progression/run_effective_mp_reservation_regression.cs
godot --headless -s res://tests/warehouse/run_party_warehouse_quantity_batch_regression.cs
godot --headless -s res://tests/progression/run_contingency_charge_transaction_regression.cs
godot --headless -s res://tests/text_runtime/commands/run_contingency_text_commands_regression.cs
godot --headless -s res://tests/world_map/ui/run_contingency_setup_window_regression.cs
godot --headless -s res://tests/battle_runtime/runtime/run_contingency_battle_lifecycle_regression.cs
godot --headless -s res://tests/battle_runtime/runtime/run_contingency_target_resolver_regression.cs
godot --headless -s res://tests/battle_runtime/runtime/run_contingency_autocast_origin_regression.cs
godot --headless -s res://tests/battle_runtime/runtime/run_contingency_trigger_contract_regression.cs
godot --headless -s res://tests/battle_runtime/rules/run_damage_application_projection_regression.cs
godot --headless -s res://tests/battle_runtime/runtime/run_contingency_damage_hook_contract_regression.cs
```

Expected: PASS for every command.

- [x] **Step 4: Run baseline gates**

Run:

```bash
dotnet build magic.csproj
python tests/run_regression_suite.py
godot --headless -s res://tests/text_runtime/headless/run_headless_game_test_session_regression.cs
```

Expected: PASS for every command.

- [ ] **Step 5: Commit checkpoint (deferred)**

```bash
git add scripts tests scenes/ui data/configs/skills
git commit -m "feat: complete contingency v1 acceptance"
```

## Task 14: Project Context Units Update

**Files:**
- Modify: `docs/design/project_context_units.md`

**Interfaces:**
- Consumes: final file ownership from Tasks 1-13.
- Produces: updated context loading map for future contingency work.

- [x] **Step 1: Update CU-11**

Add `ContingencyMatrixSetupState` and child state files to the party/member state model read set. Mention `PartyMemberState` owns persistent setup state and only stores battle-outside setup facts.

- [x] **Step 2: Update CU-12**

Add `PartyContingencySetupService` and consumed setup writeback to CharacterManagement read set. Mention charge mutation and battle writeback stay out of UI nodes.

- [x] **Step 3: Update CU-15**

Add `BattleContingencySystem`, `ContingencyTargetResolverService`, `AutoCastRequest`, and battle finalization rollback to battle runtime orchestration read set.

- [x] **Step 4: Update CU-16**

Add `DamageApplicationProjection`, `IBattleDamageApplicationHook`, and hook suppression to damage/rules read set.

- [x] **Step 5: Update CU-21**

Add contingency text commands and snapshot fields to headless read set.

- [x] **Step 6: Verify docs**

Run: `git diff --check -- docs/design/project_context_units.md`
Expected: PASS.

- [ ] **Step 7: Commit checkpoint (deferred)**

```bash
git add docs/design/project_context_units.md
git commit -m "docs: update context units for contingency"
```

## Release Verification Matrix

| Gate | Command / Evidence | Expected |
|---|---|---|
| C# build | `dotnet build magic.csproj` | PASS |
| Routine regression | `python tests/run_regression_suite.py` | PASS |
| Headless baseline | `godot --headless -s res://tests/text_runtime/headless/run_headless_game_test_session_regression.cs` | PASS |
| Setup schema | `godot --headless -s res://tests/progression/schema/run_contingency_setup_schema_regression.cs` | PASS |
| Content validator | `godot --headless -s res://tests/progression/schema/run_contingency_content_validator_regression.cs` | PASS |
| Effective MP | `godot --headless -s res://tests/progression/run_effective_mp_reservation_regression.cs` | PASS |
| Warehouse quantity | `godot --headless -s res://tests/warehouse/run_party_warehouse_quantity_batch_regression.cs` | PASS |
| Charge transaction | `godot --headless -s res://tests/progression/run_contingency_charge_transaction_regression.cs` | PASS |
| Text commands | `godot --headless -s res://tests/text_runtime/commands/run_contingency_text_commands_regression.cs` | PASS |
| Player UI | `godot --headless -s res://tests/world_map/ui/run_contingency_setup_window_regression.cs` | PASS |
| Battle lifecycle | `godot --headless -s res://tests/battle_runtime/runtime/run_contingency_battle_lifecycle_regression.cs` | PASS |
| Target resolver | `godot --headless -s res://tests/battle_runtime/runtime/run_contingency_target_resolver_regression.cs` | PASS |
| Auto-cast origin | `godot --headless -s res://tests/battle_runtime/runtime/run_contingency_autocast_origin_regression.cs` | PASS |
| Trigger contract | `godot --headless -s res://tests/battle_runtime/runtime/run_contingency_trigger_contract_regression.cs` | PASS |
| Damage projection | `godot --headless -s res://tests/battle_runtime/rules/run_damage_application_projection_regression.cs` | PASS |
| Damage hook | `godot --headless -s res://tests/battle_runtime/runtime/run_contingency_damage_hook_contract_regression.cs` | PASS |

Battle simulation / balance runners are not part of the release gate unless explicitly requested for balance analysis.

## Spec Coverage Self-Check

- Full V1 scope: covered by Global Constraints and Tasks 1-13.
- Save version and no compatibility: covered by Task 1.
- Load-time content failure: covered by Task 2.
- Real skill resource: covered by Task 2.
- Effective MP: covered by Task 3.
- Quantity-aware warehouse API: covered by Task 4.
- Charge transaction rollback: covered by Task 5.
- Player UI and headless parity: covered by Task 6.
- Battle settlement rollback: covered by Task 7.
- Battle-local instance and consumed overlay: covered by Task 8.
- Target resolver execution and safe-cell scoring: covered by Task 9.
- Auto-cast origin suppression: covered by Task 10.
- Non-damage triggers: covered by Task 11.
- Damage projection and fatal / incoming damage hooks: covered by Task 12.
- Reports and snapshots: covered by Task 13.
- Context map update after code ownership changes: covered by Task 14.

## Implementation Notes

- Internal task order is dependency order only. It is not a staged product release plan.
- The first runnable battle loop may use `combat_started` and `owner_turn_started` to shake out auto-cast, but V1 cannot ship until target resolution, damage, status, radius, spell-affect, UI, headless, content, warehouse, and rollback gates pass.
- Avoid adding a general battle event bus. Use explicit hook points and narrow DTOs.
- Avoid adding a general party-wide transaction framework. Use the narrow snapshots named in this document.
- Avoid storing battle-local contingency state in save payloads or `BattleUnitState.ToDictionary()`.
- Avoid reintroducing Godot dictionary business state in runtime owners; dictionaries stay at resource, save, scene projection, and test payload boundaries.
