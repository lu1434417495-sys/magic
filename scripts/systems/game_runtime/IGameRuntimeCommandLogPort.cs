using System;
using System.Collections.Generic;
using Godot;

internal interface IGameRuntimeCommandLogPort
{
    string CaptureCommandLogStatusMessage();

    CommandLogRuntimeSnapshot CaptureCommandLogRuntimeSnapshot();

    IReadOnlyList<CommandLogBattleUnitSnapshot> CaptureCommandLogBattleUnits(
        IEnumerable<StringName> unitIds
    );

    void RecordCommandLogEvent(
        GameLogLevel level,
        string domain,
        string eventId,
        string message,
        string context
    );
}

internal sealed class CommandLogRuntimeSnapshot
{
    internal string SaveId { get; }
    internal string MapId { get; }
    internal string MapDisplayName { get; }
    internal string PlayerCoord { get; }
    internal string SelectedCoord { get; }
    internal string ActiveModalId { get; }
    internal CommandLogBattleSnapshot Battle { get; }
    internal bool BattleActive => Battle != null;

    internal CommandLogRuntimeSnapshot(
        string saveId,
        string mapId,
        string mapDisplayName,
        string playerCoord,
        string selectedCoord,
        string activeModalId,
        CommandLogBattleSnapshot battle
    )
    {
        SaveId = saveId ?? "";
        MapId = mapId ?? "";
        MapDisplayName = mapDisplayName ?? "";
        PlayerCoord = playerCoord ?? "";
        SelectedCoord = selectedCoord ?? "";
        ActiveModalId = activeModalId ?? "";
        Battle = battle;
    }

    internal static CommandLogRuntimeSnapshot Empty { get; } =
        new("", "", "", "", "", "", null);
}

internal sealed class CommandLogBattleSnapshot
{
    internal string EncounterId { get; }
    internal string EncounterName { get; }
    internal string BattleId { get; }
    internal long Seed { get; }
    internal string TerrainProfileId { get; }
    internal Vector2I MapSize { get; }
    internal string Phase { get; }
    internal string ModalState { get; }
    internal string ObjectiveMode { get; }
    internal string Outcome { get; }
    internal string EndReason { get; }
    internal int DecisionTu { get; }
    internal string WinnerFactionId { get; }
    internal string ActiveUnitId { get; }
    internal string ActiveUnitName { get; }
    internal string SelectedCoord { get; }
    internal string SelectedSkillEntryId { get; }
    internal string SelectedSkillId { get; }
    internal string SelectedSkillVariantId { get; }
    internal int SelectedTargetCoordCount { get; }
    internal int SelectedTargetUnitCount { get; }
    internal IReadOnlyDictionary<string, object> TerrainCounts { get; }
    internal int AllyAliveCount { get; }
    internal int HostileAliveCount { get; }
    internal IReadOnlyList<CommandLogBattleUnitSnapshot> Units { get; }

    internal CommandLogBattleSnapshot(
        string encounterId,
        string encounterName,
        string battleId,
        long seed,
        string terrainProfileId,
        Vector2I mapSize,
        string phase,
        string modalState,
        string objectiveMode,
        string outcome,
        string endReason,
        int decisionTu,
        string winnerFactionId,
        string activeUnitId,
        string activeUnitName,
        string selectedCoord,
        string selectedSkillEntryId,
        string selectedSkillId,
        string selectedSkillVariantId,
        int selectedTargetCoordCount,
        int selectedTargetUnitCount,
        IReadOnlyDictionary<string, object> terrainCounts,
        int allyAliveCount,
        int hostileAliveCount,
        IReadOnlyList<CommandLogBattleUnitSnapshot> units
    )
    {
        EncounterId = encounterId ?? "";
        EncounterName = encounterName ?? "";
        BattleId = battleId ?? "";
        Seed = seed;
        TerrainProfileId = terrainProfileId ?? "";
        MapSize = mapSize;
        Phase = phase ?? "";
        ModalState = modalState ?? "";
        ObjectiveMode = objectiveMode ?? "";
        Outcome = outcome ?? "";
        EndReason = endReason ?? "";
        DecisionTu = decisionTu;
        WinnerFactionId = winnerFactionId ?? "";
        ActiveUnitId = activeUnitId ?? "";
        ActiveUnitName = activeUnitName ?? "";
        SelectedCoord = selectedCoord ?? "";
        SelectedSkillEntryId = selectedSkillEntryId ?? "";
        SelectedSkillId = selectedSkillId ?? "";
        SelectedSkillVariantId = selectedSkillVariantId ?? "";
        SelectedTargetCoordCount = Math.Max(selectedTargetCoordCount, 0);
        SelectedTargetUnitCount = Math.Max(selectedTargetUnitCount, 0);
        TerrainCounts = RuntimePlainPayload.CloneDictionary(
            terrainCounts
                ?? new Dictionary<string, object>(StringComparer.Ordinal)
        );
        AllyAliveCount = Math.Max(allyAliveCount, 0);
        HostileAliveCount = Math.Max(hostileAliveCount, 0);
        Units =
            units != null
                ? new List<CommandLogBattleUnitSnapshot>(units)
                : Array.Empty<CommandLogBattleUnitSnapshot>();
    }
}

internal sealed class CommandLogBattleUnitSnapshot
{
    internal string UnitId { get; }
    internal string DisplayName { get; }
    internal string FactionId { get; }
    internal string ControlMode { get; }
    internal bool IsAlive { get; }
    internal Vector2I Coord { get; }
    internal int CurrentHp { get; }
    internal int CurrentMp { get; }
    internal int CurrentStamina { get; }
    internal int CurrentAura { get; }
    internal int CurrentAp { get; }
    internal int CurrentMovePoints { get; }

    internal CommandLogBattleUnitSnapshot(
        string unitId,
        string displayName,
        string factionId,
        string controlMode,
        bool isAlive,
        Vector2I coord,
        int currentHp,
        int currentMp,
        int currentStamina,
        int currentAura,
        int currentAp,
        int currentMovePoints
    )
    {
        UnitId = unitId ?? "";
        DisplayName = displayName ?? "";
        FactionId = factionId ?? "";
        ControlMode = controlMode ?? "";
        IsAlive = isAlive;
        Coord = coord;
        CurrentHp = currentHp;
        CurrentMp = currentMp;
        CurrentStamina = currentStamina;
        CurrentAura = currentAura;
        CurrentAp = currentAp;
        CurrentMovePoints = currentMovePoints;
    }
}
