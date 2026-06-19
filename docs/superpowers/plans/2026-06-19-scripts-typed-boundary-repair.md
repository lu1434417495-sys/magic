# scripts Typed Boundary Repair Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Repair the audited `scripts/` runtime boundary issues by fixing immediate gameplay bugs first, then replacing weak runtime dictionaries with typed request/result/state owners.

**Architecture:** The implementation keeps `Godot.Collections.Dictionary` only at UI projection, save serialization, content import, and final report/export boundaries. Runtime command input becomes typed request DTOs, runtime state becomes typed owner collections, and save-breaking schema changes are allowed because old save compatibility is explicitly out of scope.

**Tech Stack:** Godot 4.6, C#, GodotSharp, existing headless regression runners under `tests/`, `dotnet build magic.csproj`.

## Global Constraints

- Do not add old save compatibility, legacy aliases, fallback migrations, or old payload/schema support.
- Breaking save/schema changes are allowed; bump schema/version where state payload shape changes.
- Runtime business state, runtime command input, battle/world/progression rules, AI scoring, and transaction rollback must not use `Godot.Collections.Dictionary`, raw `Dictionary`, `Variant`, or dynamic `Call/Get` as formal contracts.
- UI, save serializer, content import/validation, and final report/projection boundaries may use Godot dictionaries, but must convert to typed data at the boundary.
- Keep edits scoped to files listed in each task; do not refactor unrelated systems.
- After any ownership-boundary change, update `docs/design/project_context_units.md` if recommended read sets or boundary responsibilities change.
- Routine verification must not run battle simulation or balance runners unless a task explicitly covers `scripts/systems/battle/sim`.

---

## File Structure

Immediate gameplay bug fixes:
- `scripts/enemies/actions/UseMultiUnitSkillAction.cs` owns multi-unit AI command construction.
- `scripts/systems/settlement/SettlementShopService.cs` owns shop stock mutation.
- `scripts/systems/game_runtime/GameRuntimeSettlementCommandHandler.cs` owns settlement command transaction/persist behavior.
- `scripts/systems/inventory/PartyWarehouseService.cs` owns warehouse preview/commit behavior.
- `scripts/systems/battle/runtime/BattleMovementService.cs` owns validated movement execution.
- `scripts/ui/CharacterInfoWindow.cs`, `scripts/ui/BattleBoardController.cs`, `scripts/ui/BattleMapPanel.cs` own narrow UI defects.

Typed command and transaction boundary:
- `scripts/systems/settlement/SettlementActionRequest.cs` will own service action command input.
- `scripts/systems/game_runtime/PromotionSelectionData.cs` will own promotion selection command input.
- `scripts/systems/attributes/AttributePermanentChangeSource.cs` will own protected stat write authorization.
- `scripts/systems/game_runtime/RuntimeTransactionSnapshot.cs` will own rollback snapshots.

Typed runtime state owners:
- `scripts/player/progression/PartyMemberStateCollection.cs`
- `scripts/player/progression/QuestObjectiveProgressState.cs`
- `scripts/player/progression/QuestProgressContext.cs`
- `scripts/player/progression/UnitCustomStatMap.cs`
- `scripts/player/progression/UnitReputationMap.cs`
- `scripts/systems/battle/core/BattleStatusEffectCollection.cs`
- `scripts/systems/battle/core/BattleStatusEffectParams.cs`
- `scripts/systems/world/WorldRuntimeData.cs`

Typed battle rule/runtime owners:
- `scripts/systems/battle/rules/DamageResolutionContext.cs`
- `scripts/systems/battle/rules/FixedMitigationResult.cs`
- `scripts/systems/battle/fate/BattleFateEventPayloads.cs`
- `scripts/systems/battle/fate/MisfortuneTriggerRequest.cs`
- `scripts/systems/battle/runtime/BattleBarrierStore.cs`
- `scripts/systems/battle/runtime/MovementQueryResults.cs`

Simulation/content cleanup:
- `scripts/systems/battle/sim/BattleSimScenarioUnitEntry.cs`
- `scripts/systems/battle/sim/BattleSimMetricsSnapshot.cs`
- `scripts/systems/settlement/SettlementResearchRewardEntry.cs`

## Task 1: Fix Multi-Unit AI Command Target Ownership

**Files:**
- Modify: `scripts/enemies/actions/UseMultiUnitSkillAction.cs`
- Test: `tests/battle_runtime/ai/run_enemy_multi_unit_skill_command_regression.cs`

**Interfaces:**
- Consumes: `BattleCommand.AddTargetUnitId(StringName value)`
- Produces: `UseMultiUnitSkillAction` commands with populated `BattleCommand.TargetUnitIdsTyped`

- [ ] **Step 1: Write the failing regression**

Create `tests/battle_runtime/ai/run_enemy_multi_unit_skill_command_regression.cs`:

```csharp
using Godot;
using System;
using System.Reflection;
using System.Collections.Generic;

public partial class run_enemy_multi_unit_skill_command_regression : SceneTree
{
    public override void _Initialize()
    {
        var method = typeof(UseMultiUnitSkillAction).GetMethod(
            "_build_multi_unit_skill_command",
            BindingFlags.NonPublic | BindingFlags.Static
        );
        if (method == null)
            throw new Exception("missing _build_multi_unit_skill_command");

        var source = new BattleUnitState { unit_id = "enemy_1", coord = new Vector2I(0, 0) };
        var targetA = new BattleUnitState { unit_id = "hero_1", coord = new Vector2I(2, 0) };
        var targetB = new BattleUnitState { unit_id = "hero_2", coord = new Vector2I(3, 0) };
        var context = new BattleAiContext { unit_state = source };
        var variant = new CombatCastVariantDef { variant_id = "multi" };
        var targets = new List<BattleUnitState> { targetA, targetB };

        var command = method.Invoke(null, new object[] { context, new StringName("enemy_chain"), variant, targets }) as BattleCommand;
        if (command == null)
            throw new Exception("command was null");
        if (command.TargetUnitIdsTyped.Count != 2)
            throw new Exception($"expected 2 target ids, got {command.TargetUnitIdsTyped.Count}");
        if (command.TargetUnitIdsTyped[0] != new StringName("hero_1") || command.TargetUnitIdsTyped[1] != new StringName("hero_2"))
            throw new Exception("target ids were not preserved in command backing list");

        GD.Print("PASS enemy multi-unit command target ids persist");
        Quit(0);
    }
}
```

- [ ] **Step 2: Run the regression and verify it fails before the fix**

Run:

```bash
godot --headless -s res://tests/battle_runtime/ai/run_enemy_multi_unit_skill_command_regression.cs
```

Expected before fix: non-zero exit with `expected 2 target ids, got 0`.

- [ ] **Step 3: Implement the fix**

Change `scripts/enemies/actions/UseMultiUnitSkillAction.cs` inside `_build_multi_unit_skill_command`:

```csharp
cmd.AddTargetUnitId(tu.unit_id);
```

Do not mutate `cmd.target_unit_ids` directly because its getter returns a projection copy.

- [ ] **Step 4: Verify**

Run:

```bash
godot --headless -s res://tests/battle_runtime/ai/run_enemy_multi_unit_skill_command_regression.cs
dotnet build magic.csproj
```

Expected: regression prints `PASS enemy multi-unit command target ids persist`; build exits 0.

- [ ] **Step 5: Commit**

```bash
git add scripts/enemies/actions/UseMultiUnitSkillAction.cs tests/battle_runtime/ai/run_enemy_multi_unit_skill_command_regression.cs
git commit -m "fix: preserve enemy multi-unit skill targets"
```

## Task 2: Make Shop Purchases Persist Stock Mutation

**Files:**
- Modify: `scripts/systems/settlement/SettlementShopService.cs`
- Modify: `scripts/systems/game_runtime/GameRuntimeSettlementCommandHandler.cs`
- Test: `tests/world_map/runtime/run_settlement_shop_stock_persistence_regression.cs`

**Interfaces:**
- Consumes: `SettlementShopService.BuyTyped(...)`
- Produces: buy flow mutates the authoritative `settlementState["shop_states"][shop_id]["current_inventory"]`

- [ ] **Step 1: Write the failing regression**

Create `tests/world_map/runtime/run_settlement_shop_stock_persistence_regression.cs`:

```csharp
using Godot;
using System;

public partial class run_settlement_shop_stock_persistence_regression : SceneTree
{
    public override void _Initialize()
    {
        var itemDefs = new Godot.Collections.Dictionary();
        itemDefs[new StringName("potion")] = new ItemDef { item_id = "potion", display_name = "Potion", base_price = 10, stack_limit = 99 };
        var party = new PartyState { gold = 100, warehouse_state = new WarehouseState() };
        var settlementState = new Godot.Collections.Dictionary();
        var shopStates = new Godot.Collections.Dictionary();
        var shopState = new Godot.Collections.Dictionary
        {
            ["current_inventory"] = new Godot.Collections.Array
            {
                new Godot.Collections.Dictionary { ["item_id"] = "potion", ["quantity"] = 1, ["price"] = 10 }
            }
        };
        shopStates["general_store"] = shopState;
        settlementState["shop_states"] = shopStates;

        var service = new SettlementShopService();
        service.Setup(itemDefs, null);
        var result = service.BuyTyped(party, settlementState, "general_store", "potion", 1);
        if (!result.Success)
            throw new Exception("buy should succeed");

        var storedShopState = shopStates["general_store"].AsGodotDictionary();
        var inventory = storedShopState["current_inventory"].AsGodotArray();
        var entry = inventory[0].AsGodotDictionary();
        int quantity = entry["quantity"].AsInt32();
        if (quantity != 0)
            throw new Exception($"expected authoritative stock quantity 0, got {quantity}");

        GD.Print("PASS shop stock mutation persists in settlement state");
        Quit(0);
    }
}
```

- [ ] **Step 2: Run the regression and verify it fails before the fix**

Run:

```bash
godot --headless -s res://tests/world_map/runtime/run_settlement_shop_stock_persistence_regression.cs
```

Expected before fix: non-zero exit with `expected authoritative stock quantity 0`.

- [ ] **Step 3: Implement authoritative shop state mutation**

In `SettlementShopService`, make the buy path mutate the stored shop state from `settlementState["shop_states"]` rather than a deep duplicate. Keep duplicate snapshots only for rollback inside `GameRuntimeSettlementCommandHandler`.

Use this pattern in the method that resolves a shop state for mutation:

```csharp
private static Godot.Collections.Dictionary GetMutableShopState(
    Godot.Collections.Dictionary settlementState,
    StringName shopId
)
{
    if (settlementState == null || shopId == "")
        return new Godot.Collections.Dictionary();
    if (!settlementState.ContainsKey("shop_states") || settlementState["shop_states"].VariantType != Variant.Type.Dictionary)
        settlementState["shop_states"] = new Godot.Collections.Dictionary();
    var shopStates = settlementState["shop_states"].AsGodotDictionary();
    if (!shopStates.ContainsKey(shopId) || shopStates[shopId].VariantType != Variant.Type.Dictionary)
        shopStates[shopId] = new Godot.Collections.Dictionary();
    return shopStates[shopId].AsGodotDictionary();
}
```

Do not call `.Duplicate(true)` on the state returned for commit.

- [ ] **Step 4: Verify**

Run:

```bash
godot --headless -s res://tests/world_map/runtime/run_settlement_shop_stock_persistence_regression.cs
dotnet build magic.csproj
```

Expected: regression prints `PASS shop stock mutation persists in settlement state`; build exits 0.

- [ ] **Step 5: Commit**

```bash
git add scripts/systems/settlement/SettlementShopService.cs scripts/systems/game_runtime/GameRuntimeSettlementCommandHandler.cs tests/world_map/runtime/run_settlement_shop_stock_persistence_regression.cs
git commit -m "fix: persist settlement shop stock changes"
```

## Task 3: Make Warehouse Preview Side-Effect Free

**Files:**
- Modify: `scripts/systems/inventory/PartyWarehouseService.cs`
- Test: `tests/warehouse/run_warehouse_preview_no_side_effect_regression.cs`

**Interfaces:**
- Consumes: `PartyWarehouseService.PreviewBatchSwapEntriesTyped(...)`
- Produces: preview paths that never allocate equipment instance ids or mint trait rolls

- [ ] **Step 1: Write the failing regression**

Create `tests/warehouse/run_warehouse_preview_no_side_effect_regression.cs`:

```csharp
using Godot;
using System;

public partial class run_warehouse_preview_no_side_effect_regression : SceneTree
{
    public override void _Initialize()
    {
        var itemDefs = new Godot.Collections.Dictionary();
        itemDefs[new StringName("sword")] = new ItemDef
        {
            item_id = "sword",
            display_name = "Sword",
            category = "equipment",
            equipment_slot_ids = new Godot.Collections.Array<string> { "main_hand" },
            stack_limit = 1,
        };

        var party = new PartyState { warehouse_state = new WarehouseState() };
        var service = new PartyWarehouseService();
        service.Setup(party, itemDefs);
        party.warehouse_state.next_equipment_instance_serial = 7;

        var deposit = new Godot.Collections.Array
        {
            new Godot.Collections.Dictionary
            {
                ["item_id"] = "sword",
                ["equipment_instance"] = new EquipmentInstanceState { item_id = "sword" }.ToDictionary(),
            }
        };
        var result = service.PreviewBatchSwapEntriesTyped(new Godot.Collections.Array(), deposit);
        if (!result.Allowed)
            throw new Exception("preview should allow adding sword");
        if (party.warehouse_state.next_equipment_instance_serial != 7)
            throw new Exception("preview consumed equipment instance serial");
        if (party.warehouse_state.GetEquipmentInstancesTyped().Count != 0)
            throw new Exception("preview mutated warehouse equipment instances");

        GD.Print("PASS warehouse preview has no allocator or inventory side effects");
        Quit(0);
    }
}
```

- [ ] **Step 2: Run the regression and verify it fails before the fix**

Run:

```bash
godot --headless -s res://tests/warehouse/run_warehouse_preview_no_side_effect_regression.cs
```

Expected before fix: failure reporting serial or equipment instance mutation.

- [ ] **Step 3: Implement preview-safe equipment add**

In `_execute_batch_swap`, branch on `consumeAllocator` for equipment deposits:

```csharp
if (depositEntry.HasEquipmentInstance)
{
    if (!consumeAllocator)
    {
        if (GetTotalCapacity() - GetUsedSlots() <= 0)
            return WarehouseBatchSwapResult.Blocked("warehouse_blocked_swap", itemId, depositEntry.InstanceId);
        continue;
    }
    var addInstanceResult = AddEquipmentInstanceTyped(depositEntry.EquipmentInstance.DuplicateState(), false);
    if (addInstanceResult.AddedQuantity <= 0)
        return WarehouseBatchSwapResult.Blocked("warehouse_blocked_swap", itemId, depositEntry.InstanceId);
}
```

Keep commit behavior unchanged.

- [ ] **Step 4: Verify**

Run:

```bash
godot --headless -s res://tests/warehouse/run_warehouse_preview_no_side_effect_regression.cs
dotnet build magic.csproj
```

Expected: regression prints `PASS warehouse preview has no allocator or inventory side effects`; build exits 0.

- [ ] **Step 5: Commit**

```bash
git add scripts/systems/inventory/PartyWarehouseService.cs tests/warehouse/run_warehouse_preview_no_side_effect_regression.cs
git commit -m "fix: make warehouse swap preview side-effect free"
```

## Task 4: Make Settlement Commands Transactional on Persist Failure

**Files:**
- Modify: `scripts/systems/game_runtime/GameRuntimeSettlementCommandHandler.cs`
- Modify: `scripts/systems/game_runtime/RuntimeTransaction.cs`
- Test: `tests/world_map/runtime/run_settlement_persist_failure_rollback_regression.cs`

**Interfaces:**
- Consumes: `RuntimeTransaction`
- Produces: settlement commands return failure and roll back staged runtime state when persist fails

- [ ] **Step 1: Write the failing regression**

Create `tests/world_map/runtime/run_settlement_persist_failure_rollback_regression.cs` with a test fixture that injects a persist delegate returning false and asserts the command result is failed and party/world/player state are restored. Use the existing world-map runtime proxy test fixtures as the setup pattern.

Core assertion block:

```csharp
if (result.Ok)
    throw new Exception("settlement command returned OK after persist failure");
if (party.gold != goldBefore)
    throw new Exception("party gold was not rolled back");
if (runtime.GetPlayerCoord() != playerCoordBefore)
    throw new Exception("player coord was not rolled back");
```

- [ ] **Step 2: Run the regression and verify it fails before the fix**

Run:

```bash
godot --headless -s res://tests/world_map/runtime/run_settlement_persist_failure_rollback_regression.cs
```

Expected before fix: command returns OK or state remains mutated.

- [ ] **Step 3: Implement rollback on persist failure**

Refactor settlement command paths so state changes are staged through `RuntimeTransaction`. On persist failure:

```csharp
transaction.Rollback();
return RuntimeCommandResultProjection.Fail("persist_failed", "存档提交失败，操作已回滚。");
```

Apply to travel, buy, sell, and service action paths that currently mutate state before calling persist.

- [ ] **Step 4: Verify**

Run:

```bash
godot --headless -s res://tests/world_map/runtime/run_settlement_persist_failure_rollback_regression.cs
dotnet build magic.csproj
```

Expected: regression passes; build exits 0.

- [ ] **Step 5: Commit**

```bash
git add scripts/systems/game_runtime/GameRuntimeSettlementCommandHandler.cs scripts/systems/game_runtime/RuntimeTransaction.cs tests/world_map/runtime/run_settlement_persist_failure_rollback_regression.cs
git commit -m "fix: roll back settlement commands on persist failure"
```

## Task 5: Fix Battle Movement Barrier Interruption Cost

**Files:**
- Modify: `scripts/systems/battle/runtime/BattleMovementService.cs`
- Test: `tests/battle_runtime/runtime/run_battle_barrier_move_cost_regression.cs`

**Interfaces:**
- Consumes: `BattleMovementService.MoveUnitAlongValidatedPathTyped(...)`
- Produces: executed path includes every actually entered anchor; interrupted movement cost reflects actual movement

- [ ] **Step 1: Write the failing regression**

Create `tests/battle_runtime/runtime/run_battle_barrier_move_cost_regression.cs` that sets a unit path crossing a layered barrier whose boundary effect stops or kills movement before the target. Assert `ExecutedPath.Count` and move-point cost match the actual reached anchor.

Core assertion block:

```csharp
if (result.Executed && result.ExecutedPath.Count <= 1 && activeUnit.coord != origin)
    throw new Exception("executed movement changed coord without recording path");
if (activeUnit.current_move_points == movePointsBefore && activeUnit.coord != origin)
    throw new Exception("movement changed coord with zero cost");
```

- [ ] **Step 2: Run the regression and verify it fails before the fix**

Run:

```bash
godot --headless -s res://tests/battle_runtime/runtime/run_battle_barrier_move_cost_regression.cs
```

Expected before fix: failure on path/cost mismatch.

- [ ] **Step 3: Implement path accounting**

In `MoveUnitAlongValidatedPathTyped`, append a coordinate only after the unit has actually reached it. If barrier resolution moves, kills, or blocks the unit before entering `nextCoord`, return an interrupted result with `Executed=false` unless at least one new anchor was reached.

Use this rule:

```csharp
bool reachedNextCoord = active_unit.is_alive && active_unit.coord == nextCoord;
if (reachedNextCoord)
    result.AddExecutedAnchor(nextCoord);
if (barrierResult.Blocked || !active_unit.is_alive || !reachedNextCoord)
    return result.WithInterrupted();
```

- [ ] **Step 4: Verify**

Run:

```bash
godot --headless -s res://tests/battle_runtime/runtime/run_battle_barrier_move_cost_regression.cs
dotnet build magic.csproj
```

Expected: regression passes; build exits 0.

- [ ] **Step 5: Commit**

```bash
git add scripts/systems/battle/runtime/BattleMovementService.cs tests/battle_runtime/runtime/run_battle_barrier_move_cost_regression.cs
git commit -m "fix: account barrier-interrupted movement cost"
```

## Task 6: Tighten UI Defects That Block Correct Runtime Projection

**Files:**
- Modify: `scripts/ui/CharacterInfoWindow.cs`
- Modify: `scripts/ui/BattleBoardController.cs`
- Modify: `scripts/ui/BattleMapPanel.cs`
- Test: `tests/world_map/runtime/run_character_info_payload_schema_regression.cs`
- Test: `tests/battle_runtime/ui/run_battle_board_ui_small_regression.cs`

**Interfaces:**
- Consumes: runtime-generated character info payloads with `source` and `unit_id`
- Produces: UI normalization accepts runtime payload keys and small UI formatting/indexing defects are fixed

- [ ] **Step 1: Write payload schema regression**

Create `tests/world_map/runtime/run_character_info_payload_schema_regression.cs`:

```csharp
using Godot;
using System;

public partial class run_character_info_payload_schema_regression : SceneTree
{
    public override void _Initialize()
    {
        var payload = new Godot.Collections.Dictionary
        {
            ["source"] = "battle",
            ["unit_id"] = "unit_1",
            ["member_id"] = "hero",
            ["display_name"] = "Hero",
        };
        var normalized = CharacterInfoWindow.NormalizeCharacterPayloadForTest(payload);
        if (normalized == null || normalized.Count == 0)
            throw new Exception("runtime character info payload was rejected");
        GD.Print("PASS runtime character info payload schema");
        Quit(0);
    }
}
```

If `NormalizeCharacterPayloadForTest` does not exist, expose a minimal internal test hook that calls the existing normalizer.

- [ ] **Step 2: Write small UI regression**

Create `tests/battle_runtime/ui/run_battle_board_ui_small_regression.cs` with assertions for newline formatting and non-negative variant index:

```csharp
if (BattleBoardController.GetVariantIndexForTest(int.MinValue, 5) < 0)
    throw new Exception("variant index was negative");
if (BattleMapPanel.BuildTimelineTooltipForTest("Hero", 10, 3).Contains("/n"))
    throw new Exception("tooltip contains literal /n");
```

Expose internal test hooks only if no existing public surface exists.

- [ ] **Step 3: Run regressions and verify failure before fixes**

Run:

```bash
godot --headless -s res://tests/world_map/runtime/run_character_info_payload_schema_regression.cs
godot --headless -s res://tests/battle_runtime/ui/run_battle_board_ui_small_regression.cs
```

Expected before fix: character payload rejected, or UI small regression fails.

- [ ] **Step 4: Implement fixes**

In `CharacterInfoWindow`, add `source` and `unit_id` to the allowed top-level payload keys. In `BattleBoardController`, compute non-negative modulo using:

```csharp
return (int)(((long)hashValue - int.MinValue) % optionCount);
```

or another checked non-negative modulo implementation. In `BattleMapPanel`, replace literal `"/n"` with `"\n"`.

- [ ] **Step 5: Verify and commit**

Run:

```bash
godot --headless -s res://tests/world_map/runtime/run_character_info_payload_schema_regression.cs
godot --headless -s res://tests/battle_runtime/ui/run_battle_board_ui_small_regression.cs
dotnet build magic.csproj
```

Expected: both regressions pass; build exits 0.

Commit:

```bash
git add scripts/ui/CharacterInfoWindow.cs scripts/ui/BattleBoardController.cs scripts/ui/BattleMapPanel.cs tests/world_map/runtime/run_character_info_payload_schema_regression.cs tests/battle_runtime/ui/run_battle_board_ui_small_regression.cs
git commit -m "fix: accept runtime character payloads and small battle UI defects"
```

## Task 7: Replace Settlement Action Raw Payload With Typed Request

**Files:**
- Create: `scripts/systems/settlement/SettlementActionRequest.cs`
- Modify: `scripts/systems/game_runtime/GameRuntimeFacade.cs`
- Modify: `scripts/systems/game_runtime/GameRuntimeSettlementCommandHandler.cs`
- Modify: `scripts/systems/game_runtime/headless/GameTextCommandRunner.cs`
- Modify: `scripts/ui/SettlementWindow.cs`
- Test: `tests/world_map/runtime/run_settlement_action_request_boundary_regression.cs`

**Interfaces:**
- Produces: `internal readonly record struct SettlementActionRequest(StringName SettlementId, StringName ServiceId, StringName ActionId, StringName MemberId, int Quantity, SettlementSubmissionSource Source)`
- Consumes: `GameRuntimeSettlementCommandHandler.ExecuteSettlementAction(SettlementActionRequest request)`

- [ ] **Step 1: Add the typed request file**

Create `scripts/systems/settlement/SettlementActionRequest.cs`:

```csharp
using Godot;

internal readonly record struct SettlementActionRequest(
    StringName SettlementId,
    StringName ServiceId,
    StringName ActionId,
    StringName MemberId,
    int Quantity,
    SettlementSubmissionSource Source
)
{
    public bool IsValid => SettlementId != "" && ServiceId != "" && ActionId != "";
}
```

- [ ] **Step 2: Write boundary regression**

Create `tests/world_map/runtime/run_settlement_action_request_boundary_regression.cs` asserting that injected keys such as `pending_character_rewards`, `quest_progress_events`, and `emit_default_quest_progress_event` cannot be supplied through the public command surface.

Expected assertion:

```csharp
if (result.PendingRewardsInjectedFromClient)
    throw new Exception("client payload injected settlement rewards");
```

Represent this with existing command result fields; if no field exposes it, assert party pending reward count and quest progress are unchanged.

- [ ] **Step 3: Run regression and verify failure before fix**

Run:

```bash
godot --headless -s res://tests/world_map/runtime/run_settlement_action_request_boundary_regression.cs
```

Expected before fix: injected payload affects rewards or quest events.

- [ ] **Step 4: Replace raw payload entry points**

Change facade/headless/UI to construct `SettlementActionRequest` from explicit fields. Delete or make private any method that accepts arbitrary action `GDictionary payload` as business input. Keep dictionary building only for UI window projection.

- [ ] **Step 5: Verify and commit**

Run:

```bash
godot --headless -s res://tests/world_map/runtime/run_settlement_action_request_boundary_regression.cs
dotnet build magic.csproj
```

Expected: regression passes; build exits 0.

Commit:

```bash
git add scripts/systems/settlement/SettlementActionRequest.cs scripts/systems/game_runtime/GameRuntimeFacade.cs scripts/systems/game_runtime/GameRuntimeSettlementCommandHandler.cs scripts/systems/game_runtime/headless/GameTextCommandRunner.cs scripts/ui/SettlementWindow.cs tests/world_map/runtime/run_settlement_action_request_boundary_regression.cs
git commit -m "refactor: type settlement action requests"
```

## Task 8: Replace Promotion Selection Dictionary With Typed DTO

**Files:**
- Create: `scripts/systems/game_runtime/PromotionSelectionData.cs`
- Modify: `scripts/systems/game_runtime/GameRuntimeFacade.cs`
- Modify: `scripts/systems/game_runtime/GameRuntimeRewardFlowHandler.cs`
- Modify: `scripts/systems/progression/ProgressionService.cs`
- Modify: `scripts/systems/progression/CharacterManagementModule.cs`
- Modify: `scripts/ui/PromotionChoiceWindow.cs`
- Test: `tests/progression/core/run_promotion_selection_typed_regression.cs`

**Interfaces:**
- Produces: `PromotionSelectionData`
- Consumes: `ProgressionService.PromoteProfession(StringName professionId, PromotionSelectionData selection)`

- [ ] **Step 1: Add typed selection DTO**

Create `scripts/systems/game_runtime/PromotionSelectionData.cs`:

```csharp
using Godot;
using System.Collections.Generic;
using System.Linq;

internal sealed class PromotionSelectionData
{
    public IReadOnlyList<StringName> AssignedCoreSkillIds { get; }
    public IReadOnlyList<StringName> QualifierSkillIds { get; }
    public IReadOnlyList<StringName> TriggerSkillIds { get; }

    public PromotionSelectionData(
        IEnumerable<StringName> assignedCoreSkillIds = null,
        IEnumerable<StringName> qualifierSkillIds = null,
        IEnumerable<StringName> triggerSkillIds = null
    )
    {
        AssignedCoreSkillIds = Normalize(assignedCoreSkillIds);
        QualifierSkillIds = Normalize(qualifierSkillIds);
        TriggerSkillIds = Normalize(triggerSkillIds);
    }

    private static IReadOnlyList<StringName> Normalize(IEnumerable<StringName> values) =>
        values == null
            ? System.Array.Empty<StringName>()
            : values.Select(ProgressionDataUtils.to_string_name).Where(value => value != "").Distinct().ToArray();
}
```

- [ ] **Step 2: Write regression**

Create `tests/progression/core/run_promotion_selection_typed_regression.cs` asserting semantically identical `String`/`StringName` UI choices normalize to the same typed selection and no extra dictionary fields affect promotion.

- [ ] **Step 3: Run regression and verify failure before fix**

Run:

```bash
godot --headless -s res://tests/progression/core/run_promotion_selection_typed_regression.cs
```

Expected before fix: raw dictionary path rejects equivalent selection or accepts extra payload.

- [ ] **Step 4: Replace dictionary signatures**

Replace `GDictionary selection` parameters in promotion flow with `PromotionSelectionData`. UI converts selected ids to typed arrays before calling runtime. Remove `GetSelectionSkillIds(GDictionary selection, string key)` from formal runtime flow.

- [ ] **Step 5: Verify and commit**

Run:

```bash
godot --headless -s res://tests/progression/core/run_promotion_selection_typed_regression.cs
dotnet build magic.csproj
```

Expected: regression passes; build exits 0.

Commit:

```bash
git add scripts/systems/game_runtime/PromotionSelectionData.cs scripts/systems/game_runtime/GameRuntimeFacade.cs scripts/systems/game_runtime/GameRuntimeRewardFlowHandler.cs scripts/systems/progression/ProgressionService.cs scripts/systems/progression/CharacterManagementModule.cs scripts/ui/PromotionChoiceWindow.cs tests/progression/core/run_promotion_selection_typed_regression.cs
git commit -m "refactor: type promotion selection flow"
```

## Task 9: Replace Attribute Source Authorization Dictionary

**Files:**
- Create: `scripts/systems/attributes/AttributePermanentChangeSource.cs`
- Modify: `scripts/systems/attributes/AttributeService.cs`
- Modify: `scripts/systems/progression/CharacterCreationService.cs`
- Modify: `scripts/systems/progression/CharacterManagementModule.cs`
- Test: `tests/progression/identity/run_attribute_source_context_regression.cs`
- Test: `tests/progression/identity/run_protected_custom_stat_regression.cs`

**Interfaces:**
- Produces: `AttributePermanentChangeSource`
- Consumes: `AttributeService.ApplyPermanentAttributeChange(StringName attributeId, int delta, AttributePermanentChangeSource source)`

- [ ] **Step 1: Add typed authorization DTO**

Create `scripts/systems/attributes/AttributePermanentChangeSource.cs`:

```csharp
using Godot;

internal readonly record struct AttributePermanentChangeSource(
    AttributePermanentChangeSourceKind SourceKind,
    StringName SourceId,
    bool AllowProtectedCustomStatWrite
);

internal enum AttributePermanentChangeSourceKind
{
    Unknown = 0,
    CharacterCreation,
    StoryScript,
}
```

- [ ] **Step 2: Update `AttributeService` signature**

Expose:

```csharp
public bool ApplyPermanentAttributeChange(
    StringName attributeId,
    int delta,
    AttributePermanentChangeSource source
)
```

Remove the formal `GDictionary source_context` overload from runtime callers. Keep dictionary parsing only in save/test boundary if a current Godot signal requires it; mark it private/internal boundary-only.

- [ ] **Step 3: Run existing protected-stat tests before implementation**

Run:

```bash
godot --headless -s res://tests/progression/identity/run_attribute_source_context_regression.cs
godot --headless -s res://tests/progression/identity/run_protected_custom_stat_regression.cs
```

Expected before implementation: compile or behavior failure after signature change until callers are migrated.

- [ ] **Step 4: Migrate callers**

Character creation passes:

```csharp
new AttributePermanentChangeSource(
    AttributePermanentChangeSourceKind.CharacterCreation,
    "character_creation",
    true
)
```

Story/script paths pass `StoryScript` and explicit `AllowProtectedCustomStatWrite`.

- [ ] **Step 5: Verify and commit**

Run:

```bash
godot --headless -s res://tests/progression/identity/run_attribute_source_context_regression.cs
godot --headless -s res://tests/progression/identity/run_protected_custom_stat_regression.cs
dotnet build magic.csproj
```

Expected: both regressions pass; build exits 0.

Commit:

```bash
git add scripts/systems/attributes/AttributePermanentChangeSource.cs scripts/systems/attributes/AttributeService.cs scripts/systems/progression/CharacterCreationService.cs scripts/systems/progression/CharacterManagementModule.cs tests/progression/identity/run_attribute_source_context_regression.cs tests/progression/identity/run_protected_custom_stat_regression.cs
git commit -m "refactor: type permanent attribute change sources"
```

## Task 10: Convert Party, Quest, Attribute, and Reputation State Owners

**Files:**
- Create: `scripts/player/progression/PartyMemberStateCollection.cs`
- Create: `scripts/player/progression/QuestObjectiveProgressState.cs`
- Create: `scripts/player/progression/QuestProgressContext.cs`
- Create: `scripts/player/progression/UnitCustomStatMap.cs`
- Create: `scripts/player/progression/UnitReputationMap.cs`
- Modify: `scripts/player/progression/PartyState.cs`
- Modify: `scripts/player/progression/QuestState.cs`
- Modify: `scripts/player/progression/UnitBaseAttributes.cs`
- Modify: `scripts/player/progression/UnitReputationState.cs`
- Modify: `scripts/systems/persistence/SaveSerializer.cs`
- Test: `tests/progression/core/run_typed_party_quest_state_regression.cs`

**Interfaces:**
- Produces typed state owners with strict `ToDictionary()` / `FromDictionary()` save boundary
- Consumes no old save payload compatibility

- [ ] **Step 1: Add typed owner classes**

Implement `PartyMemberStateCollection` with:

```csharp
using Godot;
using System;
using System.Collections.Generic;

internal sealed class PartyMemberStateCollection
{
    private readonly Dictionary<StringName, PartyMemberState> _members = new();
    public IReadOnlyDictionary<StringName, PartyMemberState> Members => _members;
    public PartyMemberState Get(StringName id) => _members.TryGetValue(ProgressionDataUtils.to_string_name(id), out var value) ? value : null;
    public void Set(PartyMemberState member)
    {
        if (member == null || member.member_id == "")
            throw new ArgumentException("member_id is required", nameof(member));
        _members[member.member_id] = member;
    }
    public Godot.Collections.Dictionary ToDictionary()
    {
        var payload = new Godot.Collections.Dictionary();
        foreach ((StringName memberId, PartyMemberState member) in _members)
            payload[memberId] = member;
        return payload;
    }
    public static PartyMemberStateCollection FromDictionary(Godot.Collections.Dictionary payload)
    {
        if (payload == null)
            throw new ArgumentException("member_states payload is required", nameof(payload));
        var result = new PartyMemberStateCollection();
        foreach (Variant rawKey in payload.Keys)
        {
            StringName memberId = ProgressionDataUtils.to_string_name(rawKey);
            if (memberId == "")
                throw new ArgumentException("member_states contains an empty member id");
            Variant rawValue = payload[rawKey];
            if (rawValue.VariantType != Variant.Type.Object)
                throw new ArgumentException($"member_states[{memberId}] is not a PartyMemberState object");
            var member = rawValue.AsGodotObject() as PartyMemberState;
            if (member == null || member.member_id != memberId)
                throw new ArgumentException($"member_states[{memberId}] has mismatched member state");
            result.Set(member);
        }
        return result;
    }
}
```

Implement `QuestObjectiveProgressState`, `UnitCustomStatMap`, and `UnitReputationMap` with the same strict rule: every key normalizes to non-empty `StringName`, every value must be `Variant.Type.Int`, and invalid payloads throw at parse time instead of being stored for later runtime reads.

- [ ] **Step 2: Write state regression**

Create `tests/progression/core/run_typed_party_quest_state_regression.cs` to assert:
- non-`PartyMemberState` values are rejected during parse
- non-int quest objective progress is rejected
- custom stat/reputation non-int values are rejected
- valid new payload roundtrips exactly

- [ ] **Step 3: Run regression and verify failure before migration**

Run:

```bash
godot --headless -s res://tests/progression/core/run_typed_party_quest_state_regression.cs
```

Expected before migration: public dictionary paths accept malformed state or throw at read time.

- [ ] **Step 4: Migrate formal fields**

Replace public live dictionaries with private typed owner fields and public projection properties only where Godot serialization requires them. Do not keep public mutable dictionary state as source of truth.

- [ ] **Step 5: Bump save schema and verify**

Update save schema/version in `SaveSerializer` or owning save constants. Run:

```bash
godot --headless -s res://tests/progression/core/run_typed_party_quest_state_regression.cs
python tests/run_regression_suite.py
dotnet build magic.csproj
```

Expected: typed state regression passes; full routine regression exits 0; build exits 0.

- [ ] **Step 6: Update context map and commit**

If ownership descriptions changed, update `docs/design/project_context_units.md` for CU-02 and CU-11. Commit:

```bash
git add scripts/player/progression/PartyMemberStateCollection.cs scripts/player/progression/QuestObjectiveProgressState.cs scripts/player/progression/QuestProgressContext.cs scripts/player/progression/UnitCustomStatMap.cs scripts/player/progression/UnitReputationMap.cs scripts/player/progression/PartyState.cs scripts/player/progression/QuestState.cs scripts/player/progression/UnitBaseAttributes.cs scripts/player/progression/UnitReputationState.cs scripts/systems/persistence/SaveSerializer.cs tests/progression/core/run_typed_party_quest_state_regression.cs docs/design/project_context_units.md
git commit -m "refactor: type party quest and custom stat state"
```

## Task 11: Convert Battle Status Effects to Typed Collection

**Files:**
- Create: `scripts/systems/battle/core/BattleStatusEffectCollection.cs`
- Create: `scripts/systems/battle/core/BattleStatusEffectParams.cs`
- Modify: `scripts/systems/battle/core/BattleUnitState.cs`
- Modify: `scripts/systems/battle/core/BattleStatusEffectState.cs`
- Modify: `scripts/systems/battle/runtime/BattleSpecialSkillResolver.cs`
- Modify: `scripts/systems/battle/runtime/SkillPassiveResolver.cs`
- Test: `tests/battle_runtime/status/run_battle_status_effect_typed_state_regression.cs`

**Interfaces:**
- Produces: typed status owner and typed status params
- Consumes: save dictionaries only in `ToDictionary()` / `FromDictionary()`

- [ ] **Step 1: Add typed status collection**

Create `BattleStatusEffectCollection`:

```csharp
using Godot;
using System;
using System.Collections.Generic;

internal sealed class BattleStatusEffectCollection
{
    private readonly Dictionary<StringName, BattleStatusEffectState> _effects = new();
    public IReadOnlyDictionary<StringName, BattleStatusEffectState> Effects => _effects;
    public BattleStatusEffectState Get(StringName id) => _effects.TryGetValue(ProgressionDataUtils.to_string_name(id), out var effect) ? effect : null;
    public void Set(BattleStatusEffectState effect)
    {
        if (effect == null || effect.status_id == "")
            throw new ArgumentException("status_id is required", nameof(effect));
        _effects[effect.status_id] = effect;
    }
    public bool Remove(StringName id) => _effects.Remove(ProgressionDataUtils.to_string_name(id));
    public Godot.Collections.Dictionary ToDictionary()
    {
        var payload = new Godot.Collections.Dictionary();
        foreach ((StringName statusId, BattleStatusEffectState effect) in _effects)
            payload[statusId] = effect.ToDictionary();
        return payload;
    }
    public static BattleStatusEffectCollection FromDictionary(Godot.Collections.Dictionary payload)
    {
        if (payload == null)
            throw new ArgumentException("status_effects payload is required", nameof(payload));
        var result = new BattleStatusEffectCollection();
        foreach (Variant rawKey in payload.Keys)
        {
            StringName statusId = ProgressionDataUtils.to_string_name(rawKey);
            if (statusId == "")
                throw new ArgumentException("status_effects contains an empty status id");
            Variant rawValue = payload[rawKey];
            if (rawValue.VariantType != Variant.Type.Dictionary)
                throw new ArgumentException($"status_effects[{statusId}] is not a dictionary payload");
            var effect = BattleStatusEffectState.FromDictionary(rawValue.AsGodotDictionary());
            if (effect == null || effect.status_id != statusId)
                throw new ArgumentException($"status_effects[{statusId}] has mismatched status state");
            result.Set(effect);
        }
        return result;
    }
}
```

- [ ] **Step 2: Add typed params**

Create `BattleStatusEffectParams` with explicit fields currently read by runtime rules. Keep `ResidualSavePayload` only for save/projection if unavoidable; runtime rule readers must use explicit fields.

- [ ] **Step 3: Write regression**

Create `tests/battle_runtime/status/run_battle_status_effect_typed_state_regression.cs` asserting malformed status dictionaries are rejected at parse and runtime `GetStatusEffect` no longer rewrites a live `GDictionary`.

- [ ] **Step 4: Run regression and verify failure before migration**

Run:

```bash
godot --headless -s res://tests/battle_runtime/status/run_battle_status_effect_typed_state_regression.cs
```

Expected before migration: malformed dictionary can enter live status state or parse late.

- [ ] **Step 5: Migrate runtime callers**

Replace `BattleUnitState.status_effects` formal reads/writes with `BattleStatusEffectCollection`. Update special skill and passive resolver status creation to pass typed params.

- [ ] **Step 6: Verify and commit**

Run:

```bash
godot --headless -s res://tests/battle_runtime/status/run_battle_status_effect_typed_state_regression.cs
dotnet build magic.csproj
```

Expected: regression passes; build exits 0.

Commit:

```bash
git add scripts/systems/battle/core/BattleStatusEffectCollection.cs scripts/systems/battle/core/BattleStatusEffectParams.cs scripts/systems/battle/core/BattleUnitState.cs scripts/systems/battle/core/BattleStatusEffectState.cs scripts/systems/battle/runtime/BattleSpecialSkillResolver.cs scripts/systems/battle/runtime/SkillPassiveResolver.cs tests/battle_runtime/status/run_battle_status_effect_typed_state_regression.cs
git commit -m "refactor: type battle status effect state"
```

## Task 12: Type Damage Context, Mitigation, and Dice Dispatch

**Files:**
- Create: `scripts/systems/battle/rules/DamageResolutionContext.cs`
- Create: `scripts/systems/battle/rules/FixedMitigationResult.cs`
- Modify: `scripts/systems/battle/rules/BattleDamageResolver.cs`
- Modify: `scripts/systems/battle/rules/BattleDamageResolver.Dice.cs`
- Modify: `scripts/systems/battle/rules/BattleDamageResolver.Mitigation.cs`
- Modify: `scripts/systems/battle/rules/BattleDamageResolver.Effects.cs`
- Test: `tests/battle_runtime/rules/run_damage_context_typed_regression.cs`

**Interfaces:**
- Produces typed damage context and mitigation result
- Replaces dynamic `Call("_roll_damage_die", ...)` with typed virtual dispatch

- [ ] **Step 1: Add typed rule DTOs**

Create `DamageResolutionContext` with fields: `DamageRollMode`, `CriticalHit`, `AttackSuccess`, `SecondaryHitSuccess`, `SkillId`, `SaveRollOverrides`, `DispatchEvents`.

Create `FixedMitigationResult` with fields: `BuffReduction`, `StanceReduction`, `PassiveReduction`, `ContentDr`, `GuardBlock`, `GuardIgnoreApplied`, `IReadOnlyList<MitigationSource> Sources`.

- [ ] **Step 2: Write regression**

Create `tests/battle_runtime/rules/run_damage_context_typed_regression.cs` asserting:
- missing `critical_hit` cannot silently default through runtime rule API
- mitigation source totals are preserved without dictionary keys
- fixed damage resolver overrides `_roll_damage_die` and typed dispatch calls it

- [ ] **Step 3: Run regression and verify failure before migration**

Run:

```bash
godot --headless -s res://tests/battle_runtime/rules/run_damage_context_typed_regression.cs
```

Expected before migration: dictionary context defaults silently or typed dice override is not directly called.

- [ ] **Step 4: Migrate resolver signatures**

Change formal `ResolveEffects` paths to accept `DamageResolutionContext` instead of `GDictionary damage_context`. Convert external Godot wrappers at the edge only.

Change dice helper:

```csharp
private int RollDamageDieVirtual(int diceSides)
{
    return _roll_damage_die(diceSides);
}
```

- [ ] **Step 5: Verify and commit**

Run:

```bash
godot --headless -s res://tests/battle_runtime/rules/run_damage_context_typed_regression.cs
dotnet build magic.csproj
```

Expected: regression passes; build exits 0.

Commit:

```bash
git add scripts/systems/battle/rules/DamageResolutionContext.cs scripts/systems/battle/rules/FixedMitigationResult.cs scripts/systems/battle/rules/BattleDamageResolver.cs scripts/systems/battle/rules/BattleDamageResolver.Dice.cs scripts/systems/battle/rules/BattleDamageResolver.Mitigation.cs scripts/systems/battle/rules/BattleDamageResolver.Effects.cs tests/battle_runtime/rules/run_damage_context_typed_regression.cs
git commit -m "refactor: type damage context and mitigation"
```

## Task 13: Type Fate and Misfortune Event Payloads

**Files:**
- Create: `scripts/systems/battle/fate/BattleFateEventPayloads.cs`
- Create: `scripts/systems/battle/fate/MisfortuneTriggerRequest.cs`
- Modify: `scripts/systems/battle/fate/BattleFateEventBus.cs`
- Modify: `scripts/systems/battle/fate/FateRuntimeModule.cs`
- Modify: `scripts/systems/battle/fate/MisfortuneService.cs`
- Modify: `scripts/systems/battle/runtime/BattleTimelineDriver.cs`
- Modify: `scripts/systems/battle/runtime/BattleSpecialSkillResolver.cs`
- Test: `tests/battle_runtime/fate/run_fate_typed_event_regression.cs`

**Interfaces:**
- Produces typed fate events and typed misfortune trigger requests
- Consumes no raw event dictionaries inside fate runtime

- [ ] **Step 1: Add typed payloads**

Create `BattleFateEventPayloads.cs` with explicit records:

```csharp
internal readonly record struct FortuneCriticalEventPayload(StringName BattleId, StringName AttackerMemberId, StringName AttackerId, StringName DefenderId, int CritGateDie, bool IsDisadvantage);
internal readonly record struct LowLuckFateEventPayload(StringName BattleId, StringName AttackerMemberId, bool AttackerLowHpHardship, IReadOnlyList<StringName> AttackerStrongAttackDebuffIds, int? HiddenLuckAtBirth);
internal readonly record struct FortunaGuidanceEventPayload(StringName EventType, StringName BattleId, StringName AttackerMemberId, bool DefenderIsEliteOrBoss, bool AttackerLowHpHardship, IReadOnlyList<StringName> AttackerStrongAttackDebuffIds);
```

Create `MisfortuneTriggerRequest` records for strong debuff, adjacent ally defeated, low HP turn end, boss phase changed, ordinary miss, and critical fail.

- [ ] **Step 2: Write regression**

Create `tests/battle_runtime/fate/run_fate_typed_event_regression.cs` asserting a typo in old dictionary key cannot suppress a typed event because no raw dictionary entry point exists.

- [ ] **Step 3: Run regression and verify failure before migration**

Run:

```bash
godot --headless -s res://tests/battle_runtime/fate/run_fate_typed_event_regression.cs
```

Expected before migration: raw dictionary trigger can be malformed and silently skipped.

- [ ] **Step 4: Migrate event bus and handlers**

Replace `BattleFateEventBus.EventDispatched(eventType, Dictionary payload)` with typed event methods or typed event union methods. Keep dictionary projection only for trace/report if required.

- [ ] **Step 5: Verify and commit**

Run:

```bash
godot --headless -s res://tests/battle_runtime/fate/run_fate_typed_event_regression.cs
dotnet build magic.csproj
```

Expected: regression passes; build exits 0.

Commit:

```bash
git add scripts/systems/battle/fate/BattleFateEventPayloads.cs scripts/systems/battle/fate/MisfortuneTriggerRequest.cs scripts/systems/battle/fate/BattleFateEventBus.cs scripts/systems/battle/fate/FateRuntimeModule.cs scripts/systems/battle/fate/MisfortuneService.cs scripts/systems/battle/runtime/BattleTimelineDriver.cs scripts/systems/battle/runtime/BattleSpecialSkillResolver.cs tests/battle_runtime/fate/run_fate_typed_event_regression.cs
git commit -m "refactor: type fate and misfortune events"
```

## Task 14: Type Battle Barrier Store and Movement Query Results

**Files:**
- Create: `scripts/systems/battle/runtime/BattleBarrierStore.cs`
- Create: `scripts/systems/battle/runtime/MovementQueryResults.cs`
- Modify: `scripts/systems/battle/core/BattleState.cs`
- Modify: `scripts/systems/battle/runtime/BattleBarrierService.cs`
- Modify: `scripts/systems/battle/runtime/BattleMovementQueryService.cs`
- Modify: `scripts/systems/battle/ai/BattleAiMoveToRangeCandidateEvaluator.cs`
- Test: `tests/battle_runtime/runtime/run_battle_barrier_store_typed_regression.cs`
- Test: `tests/battle_runtime/runtime/run_movement_query_typed_result_regression.cs`

**Interfaces:**
- Produces typed barrier store and typed movement query results
- Consumes no `Dictionary ok/coords/path` movement business result

- [ ] **Step 1: Add typed classes**

`BattleBarrierStore` owns `Dictionary<StringName, BattleBarrierInstanceState>`. `MovementQueryResults.cs` defines `MovementReachabilityResult`, `MovementDistanceBandResult`, and `MovementPathTargetResult`.

- [ ] **Step 2: Write regressions**

Create barrier regression asserting malformed runtime barrier dictionaries cannot reset live barrier state. Create movement query regression asserting callers consume typed `Ok`, `Coords`, `Path`, `Cost` properties.

- [ ] **Step 3: Run regressions and verify failure before migration**

Run:

```bash
godot --headless -s res://tests/battle_runtime/runtime/run_battle_barrier_store_typed_regression.cs
godot --headless -s res://tests/battle_runtime/runtime/run_movement_query_typed_result_regression.cs
```

Expected before migration: barrier state round-trips through dictionary or movement returns dictionary payload.

- [ ] **Step 4: Migrate owners and callers**

Move barrier payload cache from `BattleState` runtime dictionary store into `BattleBarrierStore`. Convert `BattleMovementQueryService` return types and update AI callers.

- [ ] **Step 5: Verify and commit**

Run:

```bash
godot --headless -s res://tests/battle_runtime/runtime/run_battle_barrier_store_typed_regression.cs
godot --headless -s res://tests/battle_runtime/runtime/run_movement_query_typed_result_regression.cs
dotnet build magic.csproj
```

Expected: both regressions pass; build exits 0.

Commit:

```bash
git add scripts/systems/battle/runtime/BattleBarrierStore.cs scripts/systems/battle/runtime/MovementQueryResults.cs scripts/systems/battle/core/BattleState.cs scripts/systems/battle/runtime/BattleBarrierService.cs scripts/systems/battle/runtime/BattleMovementQueryService.cs scripts/systems/battle/ai/BattleAiMoveToRangeCandidateEvaluator.cs tests/battle_runtime/runtime/run_battle_barrier_store_typed_regression.cs tests/battle_runtime/runtime/run_movement_query_typed_result_regression.cs
git commit -m "refactor: type battle barrier and movement query state"
```

## Task 15: Type World Runtime Data Owner

**Files:**
- Create: `scripts/systems/world/WorldRuntimeData.cs`
- Modify: `scripts/systems/world/WorldMapDataContext.cs`
- Modify: `scripts/systems/world/WorldMapDataProjection.cs`
- Modify: `scripts/systems/persistence/SaveSerializer.cs`
- Modify: `scripts/systems/game_runtime/RuntimeTransaction.cs`
- Test: `tests/world_map/runtime/run_world_runtime_data_typed_regression.cs`

**Interfaces:**
- Produces: `WorldRuntimeData`
- Consumes: world save dictionary only in serializer/projection boundary

- [ ] **Step 1: Add typed world runtime owner**

Create `WorldRuntimeData` with typed fields for world step, active map id, root map, active map, settlement states, submap stack, fog state, mounted submaps, encounter anchors, and world events. Use `ToDictionary()` and `FromDictionary()` only at save/projection boundary.

- [ ] **Step 2: Write regression**

Create `tests/world_map/runtime/run_world_runtime_data_typed_regression.cs` asserting:
- malformed settlement state dictionary is rejected at `FromDictionary`
- typed settlement update is visible through projection
- save roundtrip uses new schema only

- [ ] **Step 3: Run regression and verify failure before migration**

Run:

```bash
godot --headless -s res://tests/world_map/runtime/run_world_runtime_data_typed_regression.cs
```

Expected before migration: world runtime state is owned by raw dictionaries.

- [ ] **Step 4: Migrate context and transaction**

`WorldMapDataContext` owns `WorldRuntimeData root` and `WorldRuntimeData active`. `RuntimeTransaction` snapshots `WorldRuntimeData` instead of `GDictionary WorldData`.

- [ ] **Step 5: Verify and commit**

Run:

```bash
godot --headless -s res://tests/world_map/runtime/run_world_runtime_data_typed_regression.cs
godot --headless -s res://tests/world_map/runtime/run_world_map_runtime_proxy_regression.cs
dotnet build magic.csproj
```

Expected: regressions pass; build exits 0.

Commit:

```bash
git add scripts/systems/world/WorldRuntimeData.cs scripts/systems/world/WorldMapDataContext.cs scripts/systems/world/WorldMapDataProjection.cs scripts/systems/persistence/SaveSerializer.cs scripts/systems/game_runtime/RuntimeTransaction.cs tests/world_map/runtime/run_world_runtime_data_typed_regression.cs
git commit -m "refactor: type world runtime data"
```

## Task 16: Type Simulation Metrics and Research Reward Catalog

**Files:**
- Create: `scripts/systems/battle/sim/BattleSimScenarioUnitEntry.cs`
- Create: `scripts/systems/battle/sim/BattleSimMetricsSnapshot.cs`
- Create: `scripts/systems/settlement/SettlementResearchRewardEntry.cs`
- Modify: `scripts/systems/battle/sim/BattleSimScenarioDef.cs`
- Modify: `scripts/systems/battle/sim/BattleSimUnitSpec.cs`
- Modify: `scripts/systems/battle/sim/BattleSimRunReport.cs`
- Modify: `scripts/systems/battle/sim/BattleSimReportBuilder.cs`
- Modify: `scripts/systems/battle/sim/BattleSimTraceSummaryBuilder.cs`
- Modify: `scripts/systems/settlement/SettlementResearchService.cs`
- Test: `tests/battle_runtime/simulation/run_battle_sim_typed_report_regression.cs`
- Test: `tests/world_map/runtime/run_settlement_research_typed_catalog_regression.cs`

**Interfaces:**
- Produces typed sim scenario entries, typed sim metrics snapshot, typed research reward entry
- Consumes final dictionaries only in report/projection output

- [ ] **Step 1: Add typed DTOs**

Define `BattleSimScenarioUnitEntry` with `BattleSimUnitSpec Spec`, `BattleUnitState UnitState`, and `Vector2I Coord`. Define `BattleSimMetricsSnapshot` with typed unit/faction/skill counters. Define `SettlementResearchRewardEntry` with enum `SettlementResearchRewardKind`.

- [ ] **Step 2: Write regressions**

Sim regression asserts `StringName` skill keys are honored and malformed scenario entries are rejected before spawn extraction. Research regression asserts unknown reward kind fails validation instead of silently skipping.

- [ ] **Step 3: Run regressions and verify failure before migration**

Run:

```bash
godot --headless -s res://tests/battle_runtime/simulation/run_battle_sim_typed_report_regression.cs
godot --headless -s res://tests/world_map/runtime/run_settlement_research_typed_catalog_regression.cs
```

Expected before migration: skill level map ignores `StringName` keys or research typo silently skips.

- [ ] **Step 4: Migrate simulation and research code**

Keep final report dictionaries in `BattleSimReportProjection` only. Keep research catalog as typed entries inside `SettlementResearchService`; convert resource/import data at setup.

- [ ] **Step 5: Verify and commit**

Run:

```bash
godot --headless -s res://tests/battle_runtime/simulation/run_battle_sim_typed_report_regression.cs
godot --headless -s res://tests/world_map/runtime/run_settlement_research_typed_catalog_regression.cs
dotnet build magic.csproj
```

Expected: regressions pass; build exits 0.

Commit:

```bash
git add scripts/systems/battle/sim/BattleSimScenarioUnitEntry.cs scripts/systems/battle/sim/BattleSimMetricsSnapshot.cs scripts/systems/settlement/SettlementResearchRewardEntry.cs scripts/systems/battle/sim/BattleSimScenarioDef.cs scripts/systems/battle/sim/BattleSimUnitSpec.cs scripts/systems/battle/sim/BattleSimRunReport.cs scripts/systems/battle/sim/BattleSimReportBuilder.cs scripts/systems/battle/sim/BattleSimTraceSummaryBuilder.cs scripts/systems/settlement/SettlementResearchService.cs tests/battle_runtime/simulation/run_battle_sim_typed_report_regression.cs tests/world_map/runtime/run_settlement_research_typed_catalog_regression.cs
git commit -m "refactor: type battle sim and research catalog state"
```

## Task 17: Final Boundary Audit and Context Map Update

**Files:**
- Modify: `docs/design/project_context_units.md`
- Test: no new test file

**Interfaces:**
- Consumes completed tasks 1-16
- Produces updated context map and final verification evidence

- [ ] **Step 1: Run weak-boundary scans**

Run:

```bash
rg -n "\\bGDictionary\\b|Godot\\.Collections\\.Dictionary|\\bVariant\\b|\\.Call\\(|\\.Get\\(" scripts --glob '!*.uid'
```

Expected: remaining matches are explainable UI/save/content/projection/final export boundaries. Any non-boundary match must be fixed or documented as a new task before completion.

- [ ] **Step 2: Update context map**

Update `docs/design/project_context_units.md` for changed ownership:
- CU-06 settlement/runtime command request boundaries
- CU-11 typed party/quest/custom state
- CU-15 battle runtime barrier/movement/fate ownership
- CU-16 damage/status/AI typed rule boundaries

- [ ] **Step 3: Run full routine verification**

Run:

```bash
python tests/run_regression_suite.py
dotnet build magic.csproj
godot --headless -s res://tests/text_runtime/headless/run_headless_game_test_session_regression.cs
godot --headless -s res://tests/world_map/runtime/run_world_map_runtime_proxy_regression.cs
godot --headless -s res://tests/world_map/schema/run_world_map_low_level_defensive_regression.cs
```

Expected: all commands exit 0. Do not run battle simulation or balance runners here.

- [ ] **Step 4: Commit**

```bash
git add docs/design/project_context_units.md
git commit -m "docs: update typed boundary context map"
```

## Self-Review

- Spec coverage: covers immediate gameplay bugs, typed runtime command inputs, typed state owners, battle/fate/rules typed contexts, world runtime owner, sim/research cleanup, and context-map update.
- Completeness scan: no deferred migration or old-payload compatibility path remains. Steps that require tests name concrete test files and concrete assertions; broad fixture setup points to the exact subsystem and expected assertion when constructor details depend on current test helpers.
- Type consistency: typed request/result names are introduced before later tasks consume them; no old save compatibility or fallback migration is included.
