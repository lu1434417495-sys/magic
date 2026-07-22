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
        IReadOnlyDictionary<StringName, WildEncounterRosterDefinition> rosterDefinitions
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
        AppendDefinitionErrors(rosterDefinitions);
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
        IReadOnlyDictionary<StringName, WildEncounterRosterDefinition> rosterDefinitions
    )
    {
        foreach ((StringName encounterId, BattleEncounterDef encounter) in _authored)
        {
            if (string.IsNullOrWhiteSpace(encounter.display_name))
                _validationErrors.Add($"Battle encounter {encounterId} is missing display_name.");
            if (encounter.roster_profile_id == "")
                _validationErrors.Add($"Battle encounter {encounterId} is missing roster_profile_id.");
            else if (
                rosterDefinitions == null
                || !rosterDefinitions.ContainsKey(encounter.roster_profile_id)
            )
            {
                _validationErrors.Add(
                    $"Battle encounter {encounterId} references missing roster profile {encounter.roster_profile_id}."
                );
            }
            if (encounter.objective == null)
                _validationErrors.Add($"Battle encounter {encounterId} is missing objective.");
            else if (encounter.objective is not BattleEliminationObjectiveDef)
            {
                _validationErrors.Add(
                    $"Battle encounter {encounterId} uses unsupported P0 objective type {encounter.objective.GetType().Name}."
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
