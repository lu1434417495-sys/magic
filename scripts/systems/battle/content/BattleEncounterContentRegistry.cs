#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Godot;

internal sealed class BattleEncounterContentRegistry : IDisposable
{
    private readonly IContentJsonSourceReader _sourceReader;
    private readonly Dictionary<StringName, BattleEncounterDefinition> _definitions = new();
    private readonly List<string> _validationErrors = new();
    private bool _disposed;

    internal BattleEncounterContentRegistry()
        : this(new GodotContentJsonSourceReader()) { }

    internal BattleEncounterContentRegistry(IContentJsonSourceReader sourceReader)
    {
        _sourceReader = sourceReader ?? throw new ArgumentNullException(nameof(sourceReader));
    }

    internal void Rebuild(
        IReadOnlyDictionary<StringName, WildEncounterRosterDefinition> rosterDefinitions,
        IReadOnlyDictionary<StringName, EnemyTemplateDefinition> enemyTemplateDefinitions
    )
    {
        ThrowIfDisposed();
        _definitions.Clear();
        _validationErrors.Clear();

        ContentImportBatch<BattleEncounterImportModel> batch =
            BattleEncounterJsonAuthoringDomains
                .CreateDescriptor(BattleEncounterJsonDomain.Directory, _sourceReader)
                .Import();
        AppendDiagnostics(batch.Diagnostics);
        foreach (ContentImportEntry<BattleEncounterImportModel> entry in batch.Entries)
        {
            try
            {
                BattleEncounterDefinition definition =
                    BattleEncounterDefinitionProjector.Project(entry.Import);
                if (!_definitions.TryAdd(definition.EncounterId, definition))
                {
                    _validationErrors.Add(
                        $"Duplicate battle encounter id registered: {definition.EncounterId}."
                    );
                }
            }
            catch (Exception exception)
            {
                _validationErrors.Add(
                    $"Battle encounter projection failed at {entry.Context.SourceLabel}: {exception.Message}"
                );
            }
        }

        AppendDefinitionGraphErrors(rosterDefinitions, enemyTemplateDefinitions);
    }

    internal IReadOnlyList<string> ValidateTyped() => _validationErrors;

    internal IReadOnlyDictionary<StringName, BattleEncounterDefinition> ProjectDefinitions()
    {
        ThrowIfDisposed();
        if (_validationErrors.Count != 0)
        {
            throw new InvalidDataException(
                "Battle encounter JSON content must validate before immutable projection: "
                    + string.Join(" | ", _validationErrors)
            );
        }
        return new ReadOnlyDictionary<StringName, BattleEncounterDefinition>(
            new Dictionary<StringName, BattleEncounterDefinition>(_definitions)
        );
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _definitions.Clear();
        _validationErrors.Clear();
        GC.SuppressFinalize(this);
    }

    private void AppendDefinitionGraphErrors(
        IReadOnlyDictionary<StringName, WildEncounterRosterDefinition>? rosterDefinitions,
        IReadOnlyDictionary<StringName, EnemyTemplateDefinition>? enemyTemplateDefinitions
    )
    {
        rosterDefinitions ??= EmptyRosters;
        enemyTemplateDefinitions ??= EmptyTemplates;
        foreach (
            (StringName encounterId, BattleEncounterDefinition encounter) in
            _definitions.OrderBy(pair => pair.Key.ToString(), StringComparer.Ordinal)
        )
        {
            rosterDefinitions.TryGetValue(
                encounter.RosterProfileId,
                out WildEncounterRosterDefinition? roster
            );
            if (roster is null)
            {
                _validationErrors.Add(
                    $"Battle encounter {encounterId} references missing roster profile {encounter.RosterProfileId}."
                );
            }

            var scenarioActorIds = new HashSet<StringName>();
            foreach (BattleScenarioActorDefinition actor in encounter.ScenarioActors)
            {
                if (!scenarioActorIds.Add(actor.ActorId))
                {
                    _validationErrors.Add(
                        $"Battle encounter {encounterId} declares duplicate scenario actor id {actor.ActorId}."
                    );
                }
                if (!enemyTemplateDefinitions.ContainsKey(actor.TemplateId))
                {
                    _validationErrors.Add(
                        $"Battle encounter {encounterId} scenario actor {actor.ActorId} references missing template {actor.TemplateId}."
                    );
                }
            }

            if (
                encounter.ScenarioActors.Count > 0
                && encounter.Objective.Mode
                    is not BattleObjectiveMode.Rescue
                    and not BattleObjectiveMode.Escort
                    and not BattleObjectiveMode.Defense
            )
            {
                _validationErrors.Add(
                    $"Battle encounter {encounterId} declares scenario actors for objective kind {encounter.Objective.Mode}, but only rescue, escort, and defense support battle-only scenario actors."
                );
            }

            switch (encounter.Objective)
            {
                case BattleBossObjectiveDefinition boss:
                    ValidateRosterTarget(encounterId, "boss", boss.TargetActorId, roster);
                    break;
                case BattleRescueObjectiveDefinition rescue:
                    ValidateScenarioTarget(encounterId, "rescue", rescue.TargetActorId, encounter.ScenarioActors);
                    break;
                case BattleEscortObjectiveDefinition escort:
                    ValidateScenarioTarget(encounterId, "escort", escort.TargetActorId, encounter.ScenarioActors);
                    ValidateEscortRoute(encounterId, escort, encounter.ScenarioActors);
                    break;
                case BattleDefenseObjectiveDefinition defense:
                    ValidateScenarioTarget(encounterId, "defense", defense.TargetActorId, encounter.ScenarioActors);
                    break;
                case BattleInterceptObjectiveDefinition intercept:
                    ValidateRosterTarget(encounterId, "intercept", intercept.TargetActorId, roster);
                    break;
            }
        }
    }

    private void ValidateRosterTarget(
        StringName encounterId,
        string objectiveKind,
        StringName targetActorId,
        WildEncounterRosterDefinition? roster
    )
    {
        if (roster is null)
            return;
        foreach (WildEncounterRosterStageDefinition stage in roster.Stages)
        {
            int count = stage.UnitEntries
                .Where(entry => entry.ActorId == targetActorId)
                .Sum(entry => entry.Count);
            if (count != 1)
            {
                _validationErrors.Add(
                    $"Battle encounter {encounterId} {objectiveKind} target actor {targetActorId} must resolve exactly once in roster {roster.ProfileId} stage {stage.Stage}; found {count}."
                );
            }
        }
    }

    private void ValidateScenarioTarget(
        StringName encounterId,
        string objectiveKind,
        StringName targetActorId,
        IReadOnlyList<BattleScenarioActorDefinition> actors
    )
    {
        int count = actors.Count(actor => actor.ActorId == targetActorId);
        if (count != 1)
        {
            _validationErrors.Add(
                $"Battle encounter {encounterId} {objectiveKind} target actor {targetActorId} must resolve exactly once among scenario actors; found {count}."
            );
        }
    }

    private void ValidateEscortRoute(
        StringName encounterId,
        BattleEscortObjectiveDefinition objective,
        IReadOnlyList<BattleScenarioActorDefinition> actors
    )
    {
        BattleScenarioActorDefinition? actor = actors.FirstOrDefault(
            candidate => candidate.ActorId == objective.TargetActorId
        );
        if (actor is null)
            return;
        if (actor.SpawnZoneId == objective.ExitZoneId)
        {
            _validationErrors.Add(
                $"Battle encounter {encounterId} escort entry and exit zone ids must be distinct."
            );
        }
        if (actor.SpawnEdge == objective.ExitEdge)
        {
            _validationErrors.Add(
                $"Battle encounter {encounterId} escort entry and exit must use different map edges."
            );
        }
    }

    private void AppendDiagnostics(IReadOnlyList<ContentJsonDiagnostic> diagnostics)
    {
        foreach (ContentJsonDiagnostic diagnostic in diagnostics)
        {
            _validationErrors.Add(
                $"[{diagnostic.RuleId}] {diagnostic.SourceLabel}{diagnostic.JsonPointer}: {diagnostic.Message}"
            );
        }
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);

    private static IReadOnlyDictionary<StringName, WildEncounterRosterDefinition> EmptyRosters { get; } =
        new ReadOnlyDictionary<StringName, WildEncounterRosterDefinition>(
            new Dictionary<StringName, WildEncounterRosterDefinition>()
        );

    private static IReadOnlyDictionary<StringName, EnemyTemplateDefinition> EmptyTemplates { get; } =
        new ReadOnlyDictionary<StringName, EnemyTemplateDefinition>(
            new Dictionary<StringName, EnemyTemplateDefinition>()
        );
}
