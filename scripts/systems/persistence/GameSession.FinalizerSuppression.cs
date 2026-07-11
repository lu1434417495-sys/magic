using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GDictionaryArray = Godot.Collections.Array<Godot.Collections.Dictionary>;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

// Partial slice of GameSession — Dispose graph + Godot-object finalizer suppression for shutdown/drain.
// Pure physical split: same class, no behavior change. See GameSession.cs.
public partial class GameSession
{

    private static void DisposeCapturedPartyState(GDictionary runtimeState)
    {
        if (runtimeState == null || !runtimeState.ContainsKey("party_state"))
            return;
        if (PartyState.TryReadPartyPayload(runtimeState["party_state"], out PartyState partyState))
            DisposePartyStateGraph(partyState);
        runtimeState["party_state"] = default(Variant);
    }

    private static void DisposePartyStateGraph(
        PartyState state,
        PartyState preservedState = null
    )
    {
        if (state == null || ReferenceEquals(state, preservedState))
            return;

        var disposed = new HashSet<GodotObject>();
        foreach (PartyMemberState memberState in state.GetMemberStates())
            DisposePartyMemberStateGraph(memberState, disposed);
        DisposeWarehouseStateGraph(state.warehouse_state, disposed);
        state.member_states.Clear();
        state.active_member_ids.Clear();
        state.reserve_member_ids.Clear();
        state.pending_character_rewards.Clear();
        state.active_quests.Clear();
        state.claimable_quests.Clear();
        state.completed_quest_ids.Clear();
    }

    private static void DisposePartyMemberStateGraph(
        PartyMemberState memberState,
        HashSet<GodotObject> disposed
    )
    {
        if (memberState == null)
            return;
        DisposeUnitProgressGraph(memberState.progression, disposed);
        DisposeEquipmentStateGraph(memberState.equipment_state, disposed);
        foreach (
            ContingencyMatrixSetupState setup in memberState.ReleaseContingencySetupsForDispose()
        )
        {
            DisposeContingencySetupGraph(setup, disposed);
        }
        memberState.trait_instances.Clear();
        memberState.active_stage_advancement_modifier_ids.Clear();
    }

    private static void DisposeContingencySetupGraph(
        ContingencyMatrixSetupState setup,
        HashSet<GodotObject> disposed
    )
    {
        // Contingency setup state is plain C#; this method remains as the
        // ownership boundary for callers that release setup lists during teardown.
    }

    private static void DisposeUnitProgressGraph(
        UnitProgress progress,
        HashSet<GodotObject> disposed
    )
    {
        if (progress == null)
            return;
        foreach (UnitProfessionProgress professionProgress in progress.ProfessionsTyped.Values)
            DisposeProfessionProgressGraph(professionProgress, disposed);
        progress.SetPendingProfessionChoices(null);
    }

    private static void DisposeProfessionProgressGraph(
        UnitProfessionProgress professionProgress,
        HashSet<GodotObject> disposed
    )
    {
        if (professionProgress == null)
            return;
        professionProgress.promotion_history.Clear();
        professionProgress.core_skill_ids.Clear();
        professionProgress.granted_skill_ids.Clear();
    }

    private static void DisposeEquipmentStateGraph(
        EquipmentState equipmentState,
        HashSet<GodotObject> disposed
    )
    {
        if (equipmentState == null)
            return;
        foreach (StringName entrySlotId in equipmentState.GetEntrySlotIdsTyped())
            equipmentState.ClearEntrySlot(entrySlotId);
    }

    private static void DisposeWarehouseStateGraph(
        WarehouseState warehouseState,
        HashSet<GodotObject> disposed
    )
    {
        if (warehouseState == null)
            return;
        warehouseState.stacks.Clear();
        warehouseState.equipment_instances.Clear();
    }

    private bool IsSessionInTree()
    {
        try
        {
            return GodotObject.IsInstanceValid(this) && IsInsideTree();
        }
        catch (ObjectDisposedException)
        {
            return false;
        }
    }

    private void ClearSessionGodotObjectReferences()
    {
        _generation_definition = null;
        _bound_generation_definition = null;
        _bound_generation_definition_path = "";
        _contentValidationSnapshotData = new ContentValidationSnapshotData();
    }
}
