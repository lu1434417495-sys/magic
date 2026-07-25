using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public sealed partial class GameRuntimeFacade : IGameRuntimeCommandLogPort
{
    string IGameRuntimeCommandLogPort.CaptureCommandLogStatusMessage() =>
        _current_status_message;

    CommandLogRuntimeSnapshot IGameRuntimeCommandLogPort.CaptureCommandLogRuntimeSnapshot()
    {
        CommandLogBattleSnapshot battleSnapshot =
            IsBattleActive() ? CaptureCommandLogBattleSnapshot() : null;
        return new CommandLogRuntimeSnapshot(
            _game_session?.GetActiveSaveId() ?? "",
            _world_map_data_context?.active_map_id ?? "",
            _world_map_data_context?.active_map_display_name ?? "",
            _player_coord.ToString(),
            _selected_coord.ToString(),
            GetActiveModalId(),
            battleSnapshot
        );
    }

    IReadOnlyList<CommandLogBattleUnitSnapshot>
        IGameRuntimeCommandLogPort.CaptureCommandLogBattleUnits(
            IEnumerable<StringName> unitIds
        ) => CaptureCommandLogBattleUnitSnapshots(unitIds);

    void IGameRuntimeCommandLogPort.RecordCommandLogEvent(
        GameLogLevel level,
        string domain,
        string eventId,
        string message,
        string context
    )
    {
        _game_session?.RecordLogEvent(
            level,
            domain,
            eventId,
            message,
            context ?? ""
        );
    }

    private CommandLogBattleSnapshot CaptureCommandLogBattleSnapshot()
    {
        BattleState battleState = _battle_state;
        if (battleState == null)
            return null;

        IReadOnlyList<CommandLogBattleUnitSnapshot> units =
            CaptureCommandLogBattleUnitSnapshots(null);
        int allyAliveCount = 0;
        int hostileAliveCount = 0;
        foreach (CommandLogBattleUnitSnapshot unit in units)
        {
            if (unit == null || !unit.IsAlive)
                continue;
            if (unit.FactionId == _player_faction_id)
                allyAliveCount++;
            else
                hostileAliveCount++;
        }

        IReadOnlyDictionary<string, object> terrainCounts;
        using (GDictionary rawTerrainCounts = _count_battle_terrain_types())
        {
            terrainCounts = RuntimePlainPayload.NormalizeDictionary(
                rawTerrainCounts,
                "GameRuntimeFacade.CommandLogPort.terrain_counts"
            );
        }

        return new CommandLogBattleSnapshot(
            _active_battle_encounter_id.ToString(),
            _active_battle_encounter_name,
            battleState.battle_id.ToString(),
            battleState.seed,
            battleState.terrain_profile_id.ToString(),
            battleState.map_size,
            battleState.phase.ToString(),
            battleState.modal_state.ToString(),
            BattleObjectiveRuntimeCodec.ToWireValue(
                battleState.ObjectiveRuntimeState?.Mode
                    ?? BattleObjectiveMode.Unknown
            ),
            BattleObjectiveRuntimeCodec.ToWireValue(
                battleState.FinalDecision?.Outcome
                    ?? BattleOutcomeKind.Unknown
            ),
            BattleObjectiveRuntimeCodec.ToWireValue(
                battleState.FinalDecision?.EndReason
                    ?? BattleEndReasonKind.None
            ),
            battleState.FinalDecision?.DecisionTu ?? -1,
            battleState.winner_faction_id.ToString(),
            battleState.active_unit_id.ToString(),
            _get_battle_active_unit_name(),
            _battle_selected_coord.ToString(),
            _selected_battle_skill_entry_id.ToString(),
            _selected_battle_skill_id.ToString(),
            _selected_battle_skill_variant_id.ToString(),
            GetBattleSelectionTargetCoordsStateTyped().Count,
            GetBattleSelectionTargetUnitIdsStateTyped().Count,
            terrainCounts,
            allyAliveCount,
            hostileAliveCount,
            units
        );
    }

    private IReadOnlyList<CommandLogBattleUnitSnapshot>
        CaptureCommandLogBattleUnitSnapshots(IEnumerable<StringName> unitIds)
    {
        var result = new List<CommandLogBattleUnitSnapshot>();
        BattleState battleState = _battle_state;
        if (battleState == null)
            return result;

        var normalizedIds = new List<StringName>();
        if (unitIds == null)
        {
            foreach (
                (StringName unitId, BattleUnitState _) in
                battleState.UnitEntries(sorted: true)
            )
                normalizedIds.Add(unitId);
        }
        else
        {
            foreach (StringName unitId in unitIds)
            {
                StringName normalizedUnitId =
                    ProgressionDataUtils.to_string_name(unitId);
                if (
                    normalizedUnitId == ""
                    || normalizedIds.Contains(normalizedUnitId)
                )
                    continue;
                normalizedIds.Add(normalizedUnitId);
            }
            if (normalizedIds.Count == 0)
            {
                foreach (
                    (StringName unitId, BattleUnitState _) in
                    battleState.UnitEntries(sorted: true)
                )
                    normalizedIds.Add(unitId);
            }
        }

        foreach (StringName unitId in normalizedIds)
        {
            BattleUnitState unitState = battleState.GetUnit(unitId);
            if (unitState == null)
                continue;
            BattleUnitCombatResourceValues combatResources =
                unitState.GetCombatResourcesReadViewTyped().Values;
            result.Add(
                new CommandLogBattleUnitSnapshot(
                    unitState.unit_id.ToString(),
                    !string.IsNullOrEmpty(unitState.display_name)
                        ? unitState.display_name
                        : unitState.unit_id.ToString(),
                    unitState.faction_id.ToString(),
                    unitState.control_mode.ToString(),
                    combatResources.IsAlive,
                    unitState.GetAnchorCoord(),
                    combatResources.Hp,
                    combatResources.Mp,
                    combatResources.Stamina,
                    combatResources.Aura,
                    combatResources.Ap,
                    combatResources.MovePoints
                )
            );
        }
        return result;
    }
}
