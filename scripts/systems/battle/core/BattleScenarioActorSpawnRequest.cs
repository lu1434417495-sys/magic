using Godot;

internal sealed class BattleScenarioActorSpawnRequest
{
    internal BattleScenarioActorSpawnRequest(
        BattleUnitState unit,
        StringName spawnZoneId,
        BattleMapEdge spawnEdge,
        int spawnDepth
    )
    {
        Unit = unit ?? throw new System.ArgumentNullException(nameof(unit));
        SpawnZoneId = spawnZoneId;
        SpawnEdge = spawnEdge;
        SpawnDepth = spawnDepth;
    }

    internal BattleUnitState Unit { get; }
    internal StringName SpawnZoneId { get; }
    internal BattleMapEdge SpawnEdge { get; }
    internal int SpawnDepth { get; }

    internal BattleScenarioActorSpawnRequest DuplicateForBattleStart() =>
        new(Unit.clone(), SpawnZoneId, SpawnEdge, SpawnDepth);
}
