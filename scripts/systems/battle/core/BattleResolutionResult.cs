using System.Collections.Generic;
using Godot;

internal class BattleResolutionResult
{
    internal StringName battle_id = "";
    internal long seed;
    internal Vector2I world_coord = Vector2I.Zero;
    internal StringName encounter_anchor_id = "";
    internal StringName terrain_profile_id = "default";
    private BattleFinalDecision _finalDecision;
    internal BattleFinalDecision final_decision => _finalDecision;
    internal BattleObjectiveMode objective_mode =>
        _finalDecision?.ObjectiveMode ?? BattleObjectiveMode.Unknown;
    internal BattleOutcomeKind outcome =>
        _finalDecision?.Outcome ?? BattleOutcomeKind.Unknown;
    internal BattleEndReasonKind end_reason =>
        _finalDecision?.EndReason ?? BattleEndReasonKind.None;
    internal StringName winner_faction_id => _finalDecision?.WinnerFactionId ?? "";
    internal StringName encounter_resolution => outcome switch
    {
        BattleOutcomeKind.PlayerSuccess => "player_victory",
        BattleOutcomeKind.PlayerFailure => "hostile_victory",
        BattleOutcomeKind.Draw => "draw",
        _ => "",
    };
    internal bool IsTerminal =>
        _finalDecision != null
        && objective_mode != BattleObjectiveMode.Unknown
        && outcome != BattleOutcomeKind.Unknown
        && end_reason != BattleEndReasonKind.None;
    internal List<BattleLootEntry> loot_entries = new();
    internal List<BattleLootEntry> overflow_entries = new();

    internal bool IsEmpty()
    {
        return battle_id == ""
            && _finalDecision == null
            && loot_entries.Count == 0
            && overflow_entries.Count == 0;
    }

    internal void SetFinalDecision(BattleFinalDecision finalDecision)
    {
        _finalDecision = finalDecision?.DuplicateState();
    }

    internal void SetLootEntries(IEnumerable<BattleLootEntry> lootEntryOptions)
    {
        loot_entries = NormalizeLootEntries(lootEntryOptions);
    }

    internal void SetOverflowEntries(IEnumerable<BattleLootEntry> overflowEntryOptions)
    {
        overflow_entries = NormalizeLootEntries(overflowEntryOptions);
    }

    internal BattleResolutionResult Duplicate()
    {
        BattleResolutionResult result = new()
        {
            battle_id = battle_id,
            seed = seed,
            world_coord = world_coord,
            encounter_anchor_id = encounter_anchor_id,
            terrain_profile_id = terrain_profile_id,
        };
        result.SetFinalDecision(_finalDecision);
        result.SetLootEntries(loot_entries);
        result.SetOverflowEntries(overflow_entries);
        return result;
    }

    internal void RestoreFrom(BattleResolutionResult snapshot)
    {
        if (snapshot == null)
            return;
        battle_id = snapshot.battle_id;
        seed = snapshot.seed;
        world_coord = snapshot.world_coord;
        encounter_anchor_id = snapshot.encounter_anchor_id;
        terrain_profile_id = snapshot.terrain_profile_id;
        SetFinalDecision(snapshot._finalDecision);
        SetLootEntries(snapshot.loot_entries);
        SetOverflowEntries(snapshot.overflow_entries);
    }

    private static List<BattleLootEntry> NormalizeLootEntries(
        IEnumerable<BattleLootEntry> lootEntryOptions
    )
    {
        List<BattleLootEntry> normalizedEntries = new();
        if (lootEntryOptions == null)
            return normalizedEntries;
        foreach (BattleLootEntry lootEntry in lootEntryOptions)
        {
            BattleLootEntry normalizedEntry = lootEntry?.Duplicate();
            if (normalizedEntry != null && !normalizedEntry.IsEmpty)
                normalizedEntries.Add(normalizedEntry);
        }
        return normalizedEntries;
    }
}
