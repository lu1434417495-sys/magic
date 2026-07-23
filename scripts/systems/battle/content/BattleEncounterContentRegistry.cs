using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Godot;

internal sealed class BattleEncounterContentRegistry : IDisposable
{
    private const string SeedPath =
        "res://data/configs/battle_encounters/battle_encounter_content_seed.tres";
    private const string EncounterDirectory = "res://data/configs/battle_encounters";

    private readonly IContentResourceLoader _loader;
    private readonly Dictionary<StringName, BattleEncounterDef> _authored = new();
    private readonly List<string> _validationErrors = new();
    private readonly HashSet<string> _seedEncounterPaths = new(StringComparer.Ordinal);
    private bool _disposed;

    internal BattleEncounterContentRegistry(IContentResourceLoader loader)
    {
        _loader = loader ?? throw new ArgumentNullException(nameof(loader));
    }

    internal void Rebuild(
        IReadOnlyDictionary<StringName, WildEncounterRosterDefinition> rosterDefinitions,
        IReadOnlyDictionary<StringName, EnemyTemplateDefinition> enemyTemplateDefinitions
    )
    {
        ThrowIfDisposed();
        _authored.Clear();
        _validationErrors.Clear();
        _seedEncounterPaths.Clear();

        Resource resource = _loader.LoadCanonical<Resource>(SeedPath);
        if (resource is not BattleEncounterContentSeed seed)
        {
            _validationErrors.Add(
                $"Battle encounter content seed {SeedPath} is missing or has the wrong type."
            );
            return;
        }

        foreach (Resource entry in seed.battle_encounters)
        {
            RememberSeedPath(entry);
            Register(entry);
        }

        AppendCompletenessErrors();
        AppendDefinitionErrors(rosterDefinitions, enemyTemplateDefinitions);
    }

    internal IReadOnlyList<string> ValidateTyped() => _validationErrors;

    internal IReadOnlyDictionary<StringName, BattleEncounterDefinition> ProjectDefinitions()
    {
        ThrowIfDisposed();
        if (_validationErrors.Count != 0)
        {
            throw new InvalidDataException(
                "Battle encounter content must validate before immutable projection: "
                    + string.Join(" | ", _validationErrors)
            );
        }

        var projected = new Dictionary<StringName, BattleEncounterDefinition>();
        foreach (StringName encounterId in SortedKeys(_authored.Keys))
        {
            BattleEncounterDefinition definition = _authored[encounterId].ToDefinition();
            if (!projected.TryAdd(encounterId, definition))
            {
                throw new InvalidDataException(
                    $"Duplicate battle encounter id projected: {encounterId}."
                );
            }
        }
        return new ReadOnlyDictionary<StringName, BattleEncounterDefinition>(projected);
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _authored.Clear();
        _validationErrors.Clear();
        _seedEncounterPaths.Clear();
        GC.SuppressFinalize(this);
    }

    private void Register(Resource resource)
    {
        if (resource is not BattleEncounterDef encounter)
        {
            _validationErrors.Add("Battle encounter seed contains a non-BattleEncounterDef entry.");
            return;
        }
        if (encounter.encounter_id == "")
        {
            _validationErrors.Add("Battle encounter definition is missing encounter_id.");
            return;
        }
        if (!_authored.TryAdd(encounter.encounter_id, encounter))
        {
            _validationErrors.Add(
                $"Duplicate battle encounter id registered: {encounter.encounter_id}."
            );
        }
    }

    private void AppendDefinitionErrors(
        IReadOnlyDictionary<StringName, WildEncounterRosterDefinition> rosterDefinitions,
        IReadOnlyDictionary<StringName, EnemyTemplateDefinition> enemyTemplateDefinitions
    )
    {
        foreach ((StringName encounterId, BattleEncounterDef encounter) in _authored)
        {
            if (string.IsNullOrWhiteSpace(encounter.display_name))
                _validationErrors.Add($"Battle encounter {encounterId} is missing display_name.");
            WildEncounterRosterDefinition rosterDefinition = null;
            if (encounter.roster_profile_id == "")
                _validationErrors.Add($"Battle encounter {encounterId} is missing roster_profile_id.");
            else if (
                rosterDefinitions == null
                || !rosterDefinitions.TryGetValue(
                    encounter.roster_profile_id,
                    out rosterDefinition
                )
            )
            {
                _validationErrors.Add(
                    $"Battle encounter {encounterId} references missing roster profile {encounter.roster_profile_id}."
                );
            }
            ValidateScenarioActors(
                encounterId,
                encounter.scenario_actors,
                enemyTemplateDefinitions
            );
            if (encounter.objective == null)
                _validationErrors.Add($"Battle encounter {encounterId} is missing objective.");
            else
            {
                ValidateObjective(
                    encounterId,
                    encounter.objective,
                    rosterDefinition,
                    encounter.scenario_actors
                );
            }
            if (encounter.world_resolution == null)
            {
                _validationErrors.Add($"Battle encounter {encounterId} is missing world_resolution.");
                continue;
            }
            ValidateWorldResolution(encounterId, encounter.world_resolution);
        }
    }

    private void ValidateObjective(
        StringName encounterId,
        BattleObjectiveDef objective,
        WildEncounterRosterDefinition rosterDefinition,
        IReadOnlyList<BattleScenarioActorDef> scenarioActors
    )
    {
        if (
            (scenarioActors?.Count ?? 0) > 0
            && objective is not BattleRescueObjectiveDef
            && objective is not BattleEscortObjectiveDef
            && objective is not BattleDefenseObjectiveDef
        )
        {
            _validationErrors.Add(
                $"Battle encounter {encounterId} declares scenario actors for objective type {objective.GetType().Name}, but only rescue, escort, and defense currently support battle-only scenario actors."
            );
        }

        switch (objective)
        {
            case BattleEliminationObjectiveDef:
                return;
            case BattleBossObjectiveDef bossObjective:
                ValidateRosterActorObjectiveTarget(
                    encounterId,
                    "boss",
                    bossObjective.target_actor_id,
                    rosterDefinition
                );
                return;
            case BattleRescueObjectiveDef rescueObjective:
                ValidateScenarioActorObjectiveTarget(
                    encounterId,
                    "rescue",
                    rescueObjective.target_actor_id,
                    scenarioActors
                );
                return;
            case BattleEscapeObjectiveDef escapeObjective:
                ValidateEscapeObjective(encounterId, escapeObjective);
                return;
            case BattleEscortObjectiveDef escortObjective:
                ValidateScenarioActorObjectiveTarget(
                    encounterId,
                    "escort",
                    escortObjective.target_actor_id,
                    scenarioActors
                );
                ValidateExitZone(
                    encounterId,
                    "escort",
                    escortObjective.exit_zone_id,
                    escortObjective.exit_edge,
                    escortObjective.exit_depth
                );
                ValidateEscortRoute(
                    encounterId,
                    escortObjective,
                    scenarioActors
                );
                return;
            case BattleDefenseObjectiveDef defenseObjective:
                ValidateScenarioActorObjectiveTarget(
                    encounterId,
                    "defense",
                    defenseObjective.target_actor_id,
                    scenarioActors
                );
                ValidateDefenseDuration(encounterId, defenseObjective.duration_tu);
                return;
            case BattleInterceptObjectiveDef interceptObjective:
                ValidateRosterActorObjectiveTarget(
                    encounterId,
                    "intercept",
                    interceptObjective.target_actor_id,
                    rosterDefinition
                );
                ValidateExitZone(
                    encounterId,
                    "intercept",
                    interceptObjective.exit_zone_id,
                    interceptObjective.exit_edge,
                    interceptObjective.exit_depth
                );
                return;
            case BattleNodeOperationObjectiveDef nodeOperationObjective:
                ValidateNodeOperationObjective(
                    encounterId,
                    nodeOperationObjective
                );
                return;
            default:
                _validationErrors.Add(
                    $"Battle encounter {encounterId} uses unsupported objective type {objective.GetType().Name}."
                );
                return;
        }
    }

    private void ValidateDefenseDuration(
        StringName encounterId,
        int durationTu
    )
    {
        if (
            durationTu <= 0
            || durationTu % BattleTimelineState.TuGranularity != 0
        )
        {
            _validationErrors.Add(
                $"Battle encounter {encounterId} defense duration_tu must be a positive multiple of {BattleTimelineState.TuGranularity}."
            );
        }
    }

    private void ValidateNodeOperationObjective(
        StringName encounterId,
        BattleNodeOperationObjectiveDef objective
    )
    {
        IReadOnlyList<BattleOperationNodeDef> nodes =
            objective.operation_nodes
            ?? new Godot.Collections.Array<BattleOperationNodeDef>();
        if (nodes.Count == 0)
        {
            _validationErrors.Add(
                $"Battle encounter {encounterId} node operation objective requires at least one operation node."
            );
            return;
        }

        var seenNodeIds = new HashSet<StringName>();
        foreach (BattleOperationNodeDef node in nodes)
        {
            if (node == null)
            {
                _validationErrors.Add(
                    $"Battle encounter {encounterId} node operation objective contains a null operation node."
                );
                continue;
            }
            if (node.node_id == "")
            {
                _validationErrors.Add(
                    $"Battle encounter {encounterId} node operation objective contains a node without node_id."
                );
            }
            else if (!seenNodeIds.Add(node.node_id))
            {
                _validationErrors.Add(
                    $"Battle encounter {encounterId} node operation objective declares duplicate node id {node.node_id}."
                );
            }
            if (string.IsNullOrWhiteSpace(node.display_name))
            {
                _validationErrors.Add(
                    $"Battle encounter {encounterId} operation node {node.node_id} is missing display_name."
                );
            }
            ValidateExitZone(
                encounterId,
                $"operation node {node.node_id}",
                node.zone_id,
                node.placement_edge,
                node.placement_depth
            );
        }
    }

    private void ValidateRosterActorObjectiveTarget(
        StringName encounterId,
        string objectiveKind,
        StringName targetActorId,
        WildEncounterRosterDefinition rosterDefinition
    )
    {
        if (targetActorId == "")
        {
            _validationErrors.Add(
                $"Battle encounter {encounterId} {objectiveKind} objective is missing target_actor_id."
            );
            return;
        }
        if (rosterDefinition == null)
            return;

        foreach (WildEncounterRosterStageDefinition stage in rosterDefinition.Stages)
        {
            int matchCount = 0;
            foreach (WildEncounterRosterUnitEntryDefinition entry in stage.UnitEntries)
            {
                if (entry.ActorId == targetActorId)
                    matchCount += entry.Count;
            }
            if (matchCount != 1)
            {
                _validationErrors.Add(
                    $"Battle encounter {encounterId} {objectiveKind} target actor {targetActorId} must resolve exactly once in roster {rosterDefinition.ProfileId} stage {stage.Stage}; found {matchCount}."
                );
            }
        }
    }

    private void ValidateEscapeObjective(
        StringName encounterId,
        BattleEscapeObjectiveDef objective
    )
    {
        ValidateExitZone(
            encounterId,
            "escape",
            objective.exit_zone_id,
            objective.exit_edge,
            objective.exit_depth
        );
    }

    private void ValidateScenarioActors(
        StringName encounterId,
        IReadOnlyList<BattleScenarioActorDef> scenarioActors,
        IReadOnlyDictionary<StringName, EnemyTemplateDefinition> enemyTemplateDefinitions
    )
    {
        var seenActorIds = new HashSet<StringName>();
        foreach (
            BattleScenarioActorDef scenarioActor in
            scenarioActors ?? Array.Empty<BattleScenarioActorDef>()
        )
        {
            if (scenarioActor == null)
            {
                _validationErrors.Add(
                    $"Battle encounter {encounterId} contains a non-scenario-actor resource."
                );
                continue;
            }
            if (scenarioActor.actor_id == "")
            {
                _validationErrors.Add(
                    $"Battle encounter {encounterId} contains a scenario actor without actor_id."
                );
            }
            else if (!seenActorIds.Add(scenarioActor.actor_id))
            {
                _validationErrors.Add(
                    $"Battle encounter {encounterId} declares duplicate scenario actor id {scenarioActor.actor_id}."
                );
            }
            if (scenarioActor.template_id == "")
            {
                _validationErrors.Add(
                    $"Battle encounter {encounterId} scenario actor {scenarioActor.actor_id} is missing template_id."
                );
            }
            else if (
                enemyTemplateDefinitions == null
                || !enemyTemplateDefinitions.ContainsKey(scenarioActor.template_id)
            )
            {
                _validationErrors.Add(
                    $"Battle encounter {encounterId} scenario actor {scenarioActor.actor_id} references missing template {scenarioActor.template_id}."
                );
            }
            ValidateExitZone(
                encounterId,
                $"scenario actor {scenarioActor.actor_id} spawn",
                scenarioActor.spawn_zone_id,
                scenarioActor.spawn_edge,
                scenarioActor.spawn_depth
            );
        }
    }

    private void ValidateScenarioActorObjectiveTarget(
        StringName encounterId,
        string objectiveKind,
        StringName targetActorId,
        IReadOnlyList<BattleScenarioActorDef> scenarioActors
    )
    {
        if (targetActorId == "")
        {
            _validationErrors.Add(
                $"Battle encounter {encounterId} {objectiveKind} objective is missing target_actor_id."
            );
            return;
        }
        int matchCount = 0;
        foreach (
            BattleScenarioActorDef scenarioActor in
            scenarioActors ?? Array.Empty<BattleScenarioActorDef>()
        )
        {
            if (scenarioActor?.actor_id == targetActorId)
                matchCount++;
        }
        if (matchCount != 1)
        {
            _validationErrors.Add(
                $"Battle encounter {encounterId} {objectiveKind} target actor {targetActorId} must resolve exactly once among scenario actors; found {matchCount}."
            );
        }
    }

    private void ValidateExitZone(
        StringName encounterId,
        string ownerLabel,
        StringName zoneId,
        BattleMapEdge edge,
        int depth
    )
    {
        if (zoneId == "")
        {
            _validationErrors.Add(
                $"Battle encounter {encounterId} {ownerLabel} is missing zone id."
            );
        }
        if (!Enum.IsDefined(edge) || edge == BattleMapEdge.Unknown)
        {
            _validationErrors.Add(
                $"Battle encounter {encounterId} {ownerLabel} uses an invalid map edge."
            );
        }
        if (depth <= 0)
        {
            _validationErrors.Add(
                $"Battle encounter {encounterId} {ownerLabel} depth must be positive."
            );
        }
    }

    private void ValidateEscortRoute(
        StringName encounterId,
        BattleEscortObjectiveDef objective,
        IReadOnlyList<BattleScenarioActorDef> scenarioActors
    )
    {
        BattleScenarioActorDef targetActor = null;
        foreach (
            BattleScenarioActorDef scenarioActor in
            scenarioActors ?? Array.Empty<BattleScenarioActorDef>()
        )
        {
            if (scenarioActor?.actor_id == objective.target_actor_id)
            {
                targetActor = scenarioActor;
                break;
            }
        }
        if (targetActor == null)
            return;
        if (targetActor.spawn_zone_id == objective.exit_zone_id)
        {
            _validationErrors.Add(
                $"Battle encounter {encounterId} escort entry and exit zone ids must be distinct."
            );
        }
        if (
            targetActor.spawn_edge != BattleMapEdge.Unknown
            && targetActor.spawn_edge == objective.exit_edge
        )
        {
            _validationErrors.Add(
                $"Battle encounter {encounterId} escort entry and exit must use different map edges."
            );
        }
    }

    private void AppendCompletenessErrors()
    {
        string absoluteDirectory = ProjectSettings.GlobalizePath(EncounterDirectory);
        if (!DirAccess.DirExistsAbsolute(absoluteDirectory))
        {
            _validationErrors.Add(
                $"Battle encounter content directory {EncounterDirectory} does not exist."
            );
            return;
        }

        using DirAccess directory = DirAccess.Open(EncounterDirectory);
        if (directory == null)
        {
            _validationErrors.Add(
                $"Battle encounter content directory {EncounterDirectory} could not be opened."
            );
            return;
        }

        directory.ListDirBegin();
        while (true)
        {
            string fileName = directory.GetNext();
            if (string.IsNullOrEmpty(fileName))
                break;
            if (directory.CurrentIsDir() || (!fileName.EndsWith(".tres") && !fileName.EndsWith(".res")))
                continue;
            string path = $"{EncounterDirectory}/{fileName}";
            if (string.Equals(path, SeedPath, StringComparison.Ordinal))
                continue;
            if (!_seedEncounterPaths.Contains(path))
            {
                _validationErrors.Add(
                    $"Battle encounter seed {SeedPath} is missing entry for {path}."
                );
            }
        }
        directory.ListDirEnd();
    }

    private void RememberSeedPath(Resource resource)
    {
        string path = (resource?.ResourcePath ?? "").Replace('\\', '/');
        if (!string.IsNullOrWhiteSpace(path))
            _seedEncounterPaths.Add(path);
    }

    private void ValidateWorldResolution(
        StringName encounterId,
        BattleEncounterWorldResolutionDef resolution
    )
    {
        ValidateWorldResolutionMode(
            encounterId,
            nameof(resolution.player_success_mode),
            resolution.player_success_mode
        );
        ValidateWorldResolutionMode(
            encounterId,
            nameof(resolution.player_failure_mode),
            resolution.player_failure_mode
        );
        ValidateWorldResolutionMode(
            encounterId,
            nameof(resolution.draw_mode),
            resolution.draw_mode
        );
        bool usesSuppression =
            resolution.player_success_mode == BattleWorldResolutionMode.Suppress
            || resolution.player_failure_mode == BattleWorldResolutionMode.Suppress
            || resolution.draw_mode == BattleWorldResolutionMode.Suppress;
        if (usesSuppression && resolution.suppression_steps <= 0)
        {
            _validationErrors.Add(
                $"Battle encounter {encounterId} uses suppression but suppression_steps is not positive."
            );
        }
        else if (!usesSuppression && resolution.suppression_steps != 0)
        {
            _validationErrors.Add(
                $"Battle encounter {encounterId} declares unused suppression_steps."
            );
        }
    }

    private void ValidateWorldResolutionMode(
        StringName encounterId,
        string fieldName,
        BattleWorldResolutionMode mode
    )
    {
        if (!Enum.IsDefined(mode))
        {
            _validationErrors.Add(
                $"Battle encounter {encounterId} uses unsupported {fieldName} value {(int)mode}."
            );
        }
    }

    private static IEnumerable<StringName> SortedKeys(IEnumerable<StringName> keys) =>
        keys
            .Select(key => key.ToString())
            .OrderBy(key => key, StringComparer.Ordinal)
            .Select(key => new StringName(key));

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
