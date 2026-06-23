using System.Collections.Generic;
using Godot;

internal class BattleResolutionResult
{
    internal StringName battle_id = "";
    internal long seed;
    internal Vector2I world_coord = Vector2I.Zero;
    internal StringName encounter_anchor_id = "";
    internal StringName terrain_profile_id = "default";
    internal StringName winner_faction_id = "";
    internal StringName encounter_resolution = "";
    internal List<BattleLootEntry> loot_entries = new();
    internal List<BattleLootEntry> overflow_entries = new();

    internal bool IsEmpty()
    {
        return battle_id == ""
            && winner_faction_id == ""
            && encounter_resolution == ""
            && loot_entries.Count == 0
            && overflow_entries.Count == 0;
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
            winner_faction_id = winner_faction_id,
            encounter_resolution = encounter_resolution,
        };
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
        winner_faction_id = snapshot.winner_faction_id;
        encounter_resolution = snapshot.encounter_resolution;
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
