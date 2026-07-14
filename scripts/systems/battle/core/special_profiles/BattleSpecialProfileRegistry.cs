using System.Collections.Generic;
using Godot;

internal class BattleSpecialProfileRegistry : IValidatableRegistry, System.IDisposable
{
    private const string ManifestDirectory = "res://data/configs/skill_special_profiles/manifests";
    private static readonly StringName MeteorSwarmProfileId = "meteor_swarm";

    private readonly IContentResourceLoader _loader;
    private string _manifestDirectory = ManifestDirectory;
    private readonly Dictionary<StringName, BattleSpecialProfileManifest> _manifestsByProfileId = new();
    private readonly Dictionary<StringName, StringName> _profileIdBySkillId = new();
    private readonly List<string> _validationErrors = new();
    private readonly BattleSpecialProfileManifestValidator _validator = new();
    private bool _disposed;

    internal BattleSpecialProfileRegistry(IContentResourceLoader loader)
    {
        _loader = loader ?? throw new System.ArgumentNullException(nameof(loader));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        System.GC.SuppressFinalize(this);
        DisposeManagedRegistry();
    }

    private void DisposeManagedRegistry()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _manifestsByProfileId.Clear();
        _profileIdBySkillId.Clear();
        _validationErrors.Clear();
    }

    public void SetManifestDirectory(string directoryPath)
    {
        _manifestDirectory = string.IsNullOrEmpty(directoryPath)
            ? ManifestDirectory
            : directoryPath;
    }

    internal void Rebuild(
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions,
        string asOfDate = ""
    )
    {
        _manifestsByProfileId.Clear();
        _profileIdBySkillId.Clear();
        _validationErrors.Clear();

        skillDefinitions ??= new Dictionary<StringName, SkillDefinition>();
        Dictionary<StringName, StringName> specialProfileIdBySkillId = CollectSpecialProfileIds(
            skillDefinitions
        );
        bool hasSpecialSkills = specialProfileIdBySkillId.Count > 0;

        if (!DirAccess.DirExistsAbsolute(ProjectSettings.GlobalizePath(_manifestDirectory)))
        {
            if (hasSpecialSkills)
            {
                _validationErrors.Add(
                    $"BattleSpecialProfileRegistry could not find {_manifestDirectory}."
                );
                AppendMissingManifestErrors(specialProfileIdBySkillId);
            }
            return;
        }

        DirAccess directory = DirAccess.Open(_manifestDirectory);
        if (directory == null)
        {
            _validationErrors.Add(
                $"BattleSpecialProfileRegistry could not open {_manifestDirectory}."
            );
            return;
        }

        try
        {
            directory.ListDirBegin();
            while (true)
            {
                string entryName = directory.GetNext();
                if (string.IsNullOrEmpty(entryName))
                    break;
                if (entryName == "." || entryName == ".." || directory.CurrentIsDir())
                    continue;
                if (!entryName.EndsWith(".tres") && !entryName.EndsWith(".res"))
                    continue;
                RegisterManifestResource(
                    $"{_manifestDirectory}/{entryName}",
                    skillDefinitions,
                    specialProfileIdBySkillId,
                    asOfDate
                );
            }
            directory.ListDirEnd();
        }
        finally
        {
            GodotObjectLifecycle.DisposeGodotObject(directory);
        }

        AppendMissingManifestErrors(specialProfileIdBySkillId);
    }

    public Godot.Collections.Array<string> Validate()
    {
        var result = new Godot.Collections.Array<string>();
        foreach (string error in _validationErrors)
            result.Add(error);
        return result;
    }

    private BattleSpecialProfileManifest GetManifest(StringName profileId)
    {
        return _manifestsByProfileId.TryGetValue(profileId, out BattleSpecialProfileManifest manifest)
            ? manifest
            : null;
    }

    internal IBattleSpecialProfileView BuildRuntimeProfileView()
    {
        if (_validationErrors.Count != 0)
        {
            return BattleSpecialProfileRuntimeView.Empty;
        }
        var meteorProfiles = new Dictionary<StringName, MeteorSwarmProfileData>();
        foreach (var profileIdValue in _manifestsByProfileId.Keys)
        {
            var profileId = ProgressionDataUtils.to_string_name(profileIdValue);
            var manifest = GetManifest(profileId);
            if (manifest == null || profileId != MeteorSwarmProfileId)
            {
                continue;
            }
            MeteorSwarmProfile meteorProfile =
                manifest.RequireMeteorSwarmProfileForProjection();
            MeteorSwarmProfileData data =
                MeteorSwarmProfileData.FromResource(profileId, meteorProfile);
            meteorProfiles[profileId] = data;
        }
        return new BattleSpecialProfileRuntimeView(meteorProfiles);
    }

    internal IReadOnlyDictionary<StringName, BattleSpecialProfileManifest> GetManifestsTyped() =>
        _manifestsByProfileId;

    internal IReadOnlyDictionary<StringName, StringName> GetProfileIdBySkillIdTyped() =>
        _profileIdBySkillId;

    public IReadOnlyList<string> ValidateTyped() => _validationErrors;

    private void RegisterManifestResource(
        string resourcePath,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions,
        IReadOnlyDictionary<StringName, StringName> specialProfileIdBySkillId,
        string asOfDate
    )
    {
        var resource = _loader.LoadCanonical<Resource>(resourcePath);
        if (resource == null)
        {
            _validationErrors.Add($"BattleSpecialProfileRegistry failed to load {resourcePath}.");
            return;
        }
        var manifest = resource as BattleSpecialProfileManifest;
        if (manifest == null)
        {
            _validationErrors.Add(
                $"BattleSpecialProfileRegistry {resourcePath} is not a BattleSpecialProfileManifest."
            );
            return;
        }
        if (manifest.profile_id == "")
        {
            _validationErrors.Add(
                $"BattleSpecialProfileRegistry {resourcePath} is missing profile_id."
            );
            return;
        }
        if (_manifestsByProfileId.ContainsKey(manifest.profile_id))
        {
            _validationErrors.Add(
                $"Duplicate battle special profile_id registered: {manifest.profile_id}"
            );
            return;
        }

        _manifestsByProfileId[manifest.profile_id] = manifest;
        AppendProfileResourcePathErrors(manifest);
        foreach (var error in _validator.ValidateManifest(manifest, skillDefinitions, asOfDate))
            _validationErrors.Add(error);

        foreach (var skillId in manifest.owning_skill_ids)
        {
            if (skillId == "")
                continue;
            if (
                specialProfileIdBySkillId == null
                || !specialProfileIdBySkillId.TryGetValue(skillId, out StringName configuredProfileId)
                || configuredProfileId != manifest.profile_id
            )
            {
                continue;
            }
            if (_profileIdBySkillId.ContainsKey(skillId))
            {
                _validationErrors.Add(
                    $"Duplicate battle special profile owning_skill_id registered: {skillId}"
                );
                continue;
            }
            _profileIdBySkillId[skillId] = manifest.profile_id;
        }
    }

    private void AppendProfileResourcePathErrors(BattleSpecialProfileManifest manifest)
    {
        if (manifest.profile_resource == null)
            return;
        string profilePath = manifest.profile_resource.ResourcePath;
        if (string.IsNullOrEmpty(profilePath))
        {
            _validationErrors.Add(
                $"Battle special profile {manifest.profile_id} profile_resource must be saved under the sibling profiles directory."
            );
            return;
        }
        string expectedPrefix = $"{_manifestDirectory.GetBaseDir()}/profiles/";
        if (!profilePath.StartsWith(expectedPrefix))
            _validationErrors.Add(
                $"Battle special profile {manifest.profile_id} profile_resource must be under {expectedPrefix}."
            );
    }

    private void AppendMissingManifestErrors(
        IReadOnlyDictionary<StringName, StringName> specialProfileIdBySkillId
    )
    {
        foreach (StringName skillId in specialProfileIdBySkillId.Keys)
        {
            StringName profileId = specialProfileIdBySkillId[skillId];
            if (profileId == "")
                continue;
            if (!_manifestsByProfileId.ContainsKey(profileId))
            {
                _validationErrors.Add(
                    $"Battle special profile {profileId} is missing manifest for skill {skillId}."
                );
                continue;
            }
            if (
                !_profileIdBySkillId.ContainsKey(skillId)
                || _profileIdBySkillId[skillId] != profileId
            )
                _validationErrors.Add(
                    $"Battle special profile {profileId} manifest does not own skill {skillId}."
                );
        }
    }

    private static Dictionary<StringName, StringName> CollectSpecialProfileIds(
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions
    )
    {
        var result = new Dictionary<StringName, StringName>();
        if (skillDefinitions == null)
            return result;
        foreach (StringName skillId in skillDefinitions.Keys)
        {
            SkillDefinition skillDefinition = skillDefinitions[skillId];
            CombatSkillDefinition combatProfile = skillDefinition?.CombatProfile;
            if (combatProfile == null || combatProfile.SpecialResolutionProfileId == "")
                continue;
            result[skillId] = combatProfile.SpecialResolutionProfileId;
        }
        return result;
    }
}
