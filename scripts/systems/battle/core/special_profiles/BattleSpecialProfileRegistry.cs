#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Godot;

internal sealed class BattleSpecialProfileRegistry : IValidatableRegistry, IDisposable
{
    private readonly IContentJsonSourceReader _sourceReader;
    private string _manifestDirectory = BattleSpecialProfileJsonDomains.ManifestDirectory;
    private string _profileDirectory = BattleSpecialProfileJsonDomains.ProfileDirectory;
    private readonly Dictionary<StringName, BattleSpecialProfileManifestDefinition> _manifests = new();
    private readonly Dictionary<StringName, MeteorSwarmProfileData> _meteorProfiles = new();
    private readonly Dictionary<StringName, StringName> _profileIdBySkillId = new();
    private readonly List<string> _validationErrors = new();
    private bool _disposed;

    internal BattleSpecialProfileRegistry()
        : this(new GodotContentJsonSourceReader()) { }

    internal BattleSpecialProfileRegistry(IContentJsonSourceReader sourceReader)
    {
        _sourceReader = sourceReader ?? throw new ArgumentNullException(nameof(sourceReader));
    }

    public void SetManifestDirectory(string directoryPath)
    {
        _manifestDirectory = string.IsNullOrWhiteSpace(directoryPath)
            ? BattleSpecialProfileJsonDomains.ManifestDirectory
            : directoryPath;
    }

    internal void SetProfileDirectory(string directoryPath)
    {
        _profileDirectory = string.IsNullOrWhiteSpace(directoryPath)
            ? BattleSpecialProfileJsonDomains.ProfileDirectory
            : directoryPath;
    }

    internal void Rebuild(
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions,
        string asOfDate = ""
    )
    {
        ThrowIfDisposed();
        _manifests.Clear();
        _meteorProfiles.Clear();
        _profileIdBySkillId.Clear();
        _validationErrors.Clear();
        skillDefinitions ??= EmptySkills;

        ContentImportBatch<BattleSpecialProfileImportModel> profileBatch =
            BattleSpecialProfileJsonAuthoringDomains
                .CreateProfileDescriptor(_profileDirectory, _sourceReader)
                .Import();
        ContentImportBatch<BattleSpecialProfileManifestImportModel> manifestBatch =
            BattleSpecialProfileJsonAuthoringDomains
                .CreateManifestDescriptor(_manifestDirectory, _sourceReader)
                .Import();
        AppendDiagnostics(profileBatch.Diagnostics);
        AppendDiagnostics(manifestBatch.Diagnostics);

        foreach (ContentImportEntry<BattleSpecialProfileImportModel> entry in profileBatch.Entries)
        {
            try
            {
                MeteorSwarmProfileData definition =
                    BattleSpecialProfileDefinitionProjector.ProjectMeteorSwarm(entry.Import);
                if (!_meteorProfiles.TryAdd(definition.profile_id, definition))
                {
                    _validationErrors.Add(
                        $"Duplicate battle special profile_id registered: {definition.profile_id}."
                    );
                }
            }
            catch (Exception exception)
            {
                _validationErrors.Add(
                    $"Battle special profile projection failed at {entry.Context.SourceLabel}: {exception.Message}"
                );
            }
        }

        foreach (
            ContentImportEntry<BattleSpecialProfileManifestImportModel> entry in manifestBatch.Entries
        )
        {
            try
            {
                BattleSpecialProfileManifestDefinition definition =
                    BattleSpecialProfileDefinitionProjector.ProjectManifest(entry.Import);
                if (!_manifests.TryAdd(definition.ProfileId, definition))
                {
                    _validationErrors.Add(
                        $"Duplicate battle special profile manifest id registered: {definition.ProfileId}."
                    );
                    continue;
                }
                if (!_meteorProfiles.ContainsKey(definition.ProfileId))
                {
                    _validationErrors.Add(
                        $"Battle special profile manifest {definition.ProfileId} references missing profile ID {definition.ProfileId}."
                    );
                }
                ValidateManifestSkillReferences(definition, skillDefinitions);
            }
            catch (Exception exception)
            {
                _validationErrors.Add(
                    $"Battle special profile manifest projection failed at {entry.Context.SourceLabel}: {exception.Message}"
                );
            }
        }

        foreach (StringName profileId in _meteorProfiles.Keys)
        {
            if (!_manifests.ContainsKey(profileId))
                _validationErrors.Add($"Battle special profile {profileId} is missing its manifest.");
        }
        AppendMissingManifestErrors(skillDefinitions);
    }

    public Godot.Collections.Array<string> Validate() => new(_validationErrors);
    public IReadOnlyList<string> ValidateTyped() => _validationErrors;

    internal IBattleSpecialProfileView BuildRuntimeProfileView() =>
        _validationErrors.Count == 0
            ? new BattleSpecialProfileRuntimeView(_meteorProfiles)
            : BattleSpecialProfileRuntimeView.Empty;

    internal IReadOnlyDictionary<StringName, BattleSpecialProfileManifestDefinition>
        GetManifestsTyped() =>
        new ReadOnlyDictionary<StringName, BattleSpecialProfileManifestDefinition>(
            new Dictionary<StringName, BattleSpecialProfileManifestDefinition>(_manifests)
        );

    internal IReadOnlyDictionary<StringName, StringName> GetProfileIdBySkillIdTyped() =>
        new ReadOnlyDictionary<StringName, StringName>(
            new Dictionary<StringName, StringName>(_profileIdBySkillId)
        );

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _manifests.Clear();
        _meteorProfiles.Clear();
        _profileIdBySkillId.Clear();
        _validationErrors.Clear();
        GC.SuppressFinalize(this);
    }

    private void ValidateManifestSkillReferences(
        BattleSpecialProfileManifestDefinition manifest,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions
    )
    {
        foreach (StringName skillId in manifest.OwningSkillIds)
        {
            if (!skillDefinitions.TryGetValue(skillId, out SkillDefinition? skill) || skill is null)
            {
                _validationErrors.Add(
                    $"Battle special profile {manifest.ProfileId} references missing owning skill {skillId}."
                );
                continue;
            }
            CombatSkillDefinition? combatProfile = skill.CombatProfile;
            if (combatProfile is null)
            {
                _validationErrors.Add(
                    $"Battle special profile {manifest.ProfileId} owning skill {skillId} is missing combat_profile."
                );
                continue;
            }
            if (combatProfile.SpecialResolutionProfileId != manifest.ProfileId)
            {
                _validationErrors.Add(
                    $"Battle special profile {manifest.ProfileId} owning skill {skillId} must set matching special_resolution_profile_id."
                );
                continue;
            }
            if (!_profileIdBySkillId.TryAdd(skillId, manifest.ProfileId))
            {
                _validationErrors.Add(
                    $"Duplicate battle special profile owning_skill_id registered: {skillId}."
                );
            }
            if (combatProfile.EffectDefinitions.Count > 0)
            {
                _validationErrors.Add(
                    $"Battle special profile owning skill {skillId} must not declare executable combat_profile.effect_defs."
                );
            }
            for (int index = 0; index < combatProfile.CastVariants.Count; index += 1)
            {
                CombatCastVariantDefinition variant = combatProfile.CastVariants[index];
                if (variant is not null && variant.EffectDefinitions.Count > 0)
                {
                    _validationErrors.Add(
                        $"Battle special profile owning skill {skillId} must not declare executable cast_variants[{index}].effect_defs."
                    );
                }
            }
        }
    }

    private void AppendMissingManifestErrors(
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions
    )
    {
        foreach ((StringName skillId, SkillDefinition skill) in skillDefinitions)
        {
            StringName profileId = skill?.CombatProfile?.SpecialResolutionProfileId ?? "";
            if (profileId == "")
                continue;
            if (!_manifests.ContainsKey(profileId))
            {
                _validationErrors.Add(
                    $"Battle special profile {profileId} is missing manifest for skill {skillId}."
                );
            }
            else if (
                !_profileIdBySkillId.TryGetValue(skillId, out StringName? owner)
                || owner is null
                || owner != profileId
            )
            {
                _validationErrors.Add(
                    $"Battle special profile {profileId} manifest does not own skill {skillId}."
                );
            }
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

    private static IReadOnlyDictionary<StringName, SkillDefinition> EmptySkills { get; } =
        new ReadOnlyDictionary<StringName, SkillDefinition>(
            new Dictionary<StringName, SkillDefinition>()
        );
}
