using Godot;

[GlobalClass]
public partial class BattleSpecialProfileRegistry : RefCounted
{
    private const string ManifestDirectory = "res://data/configs/skill_special_profiles/manifests";

    private string _manifestDirectory = ManifestDirectory;
    private readonly Godot.Collections.Dictionary _manifestsByProfileId = new();
    private readonly Godot.Collections.Dictionary _profileIdBySkillId = new();
    private readonly Godot.Collections.Array<string> _validationErrors = new();
    private readonly BattleSpecialProfileManifestValidator _validator = new();

    public void set_manifest_directory(string directoryPath)
    {
        _manifestDirectory = string.IsNullOrEmpty(directoryPath) ? ManifestDirectory : directoryPath;
    }

    public void rebuild(Godot.Collections.Dictionary skillDefs)
    {
        rebuild(skillDefs, "");
    }

    public void rebuild(Godot.Collections.Dictionary skillDefs, string asOfDate)
    {
        _manifestsByProfileId.Clear();
        _profileIdBySkillId.Clear();
        _validationErrors.Clear();

        skillDefs ??= new Godot.Collections.Dictionary();
        var specialProfileIdBySkillId = CollectSpecialProfileIds(skillDefs);
        bool hasSpecialSkills = specialProfileIdBySkillId.Count > 0;

        if (!DirAccess.DirExistsAbsolute(ProjectSettings.GlobalizePath(_manifestDirectory)))
        {
            if (hasSpecialSkills)
            {
                _validationErrors.Add($"BattleSpecialProfileRegistry could not find {_manifestDirectory}.");
                AppendMissingManifestErrors(specialProfileIdBySkillId);
            }
            return;
        }

        var directory = DirAccess.Open(_manifestDirectory);
        if (directory == null)
        {
            _validationErrors.Add($"BattleSpecialProfileRegistry could not open {_manifestDirectory}.");
            return;
        }

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
            RegisterManifestResource($"{_manifestDirectory}/{entryName}", skillDefs, asOfDate);
        }
        directory.ListDirEnd();

        AppendMissingManifestErrors(specialProfileIdBySkillId);
    }

    public Godot.Collections.Array<string> validate()
    {
        return new Godot.Collections.Array<string>(_validationErrors);
    }

    public BattleSpecialProfileManifest get_manifest(StringName profileId)
    {
        return _manifestsByProfileId.ContainsKey(profileId)
            ? _manifestsByProfileId[profileId].AsGodotObject() as BattleSpecialProfileManifest
            : null;
    }

    public BattleSpecialProfileManifest get_manifest_for_skill(StringName skillId)
    {
        var profileId = _profileIdBySkillId.ContainsKey(skillId)
            ? ProgressionDataUtils.to_string_name(_profileIdBySkillId[skillId])
            : new StringName("");
        return profileId == "" ? null : get_manifest(profileId);
    }

    public bool has_profile(StringName profileId)
    {
        return _manifestsByProfileId.ContainsKey(profileId);
    }

    public Godot.Collections.Dictionary get_snapshot()
    {
        var profiles = new Godot.Collections.Dictionary();
        foreach (var profileIdVariant in _manifestsByProfileId.Keys)
        {
            var profileId = ProgressionDataUtils.to_string_name(profileIdVariant);
            var manifest = get_manifest(profileId);
            if (manifest == null)
                continue;

            var owningSkillIds = new Godot.Collections.Array<string>();
            foreach (var skillId in manifest.owning_skill_ids)
                owningSkillIds.Add(skillId.ToString());

            profiles[profileId.ToString()] = new Godot.Collections.Dictionary
            {
                ["profile_id"] = manifest.profile_id.ToString(),
                ["runtime_resolver_id"] = manifest.runtime_resolver_id.ToString(),
                ["owning_skill_ids"] = owningSkillIds,
                ["profile_resource"] = manifest.profile_resource,
                ["presentation_metadata"] = manifest.presentation_metadata.Duplicate(true),
                ["required_regression_tests"] = new Godot.Collections.Array<string>(manifest.required_regression_tests),
            };
        }

        var profileIdBySkillId = new Godot.Collections.Dictionary();
        foreach (var skillIdVariant in _profileIdBySkillId.Keys)
        {
            var skillId = ProgressionDataUtils.to_string_name(skillIdVariant);
            profileIdBySkillId[skillId.ToString()] = ProgressionDataUtils.to_string_name(_profileIdBySkillId[skillId]).ToString();
        }

        return new Godot.Collections.Dictionary
        {
            ["ok"] = _validationErrors.Count == 0,
            ["errors"] = new Godot.Collections.Array<string>(_validationErrors),
            ["profiles"] = profiles,
            ["profile_id_by_skill_id"] = profileIdBySkillId,
        };
    }

    private void RegisterManifestResource(string resourcePath, Godot.Collections.Dictionary skillDefs, string asOfDate)
    {
        var resource = GD.Load<Resource>(resourcePath);
        if (resource == null)
        {
            _validationErrors.Add($"BattleSpecialProfileRegistry failed to load {resourcePath}.");
            return;
        }

        var manifest = resource as BattleSpecialProfileManifest;
        if (manifest == null)
        {
            _validationErrors.Add($"BattleSpecialProfileRegistry {resourcePath} is not a BattleSpecialProfileManifest.");
            return;
        }
        if (manifest.profile_id == "")
        {
            _validationErrors.Add($"BattleSpecialProfileRegistry {resourcePath} is missing profile_id.");
            return;
        }
        if (_manifestsByProfileId.ContainsKey(manifest.profile_id))
        {
            _validationErrors.Add($"Duplicate battle special profile_id registered: {manifest.profile_id}");
            return;
        }

        _manifestsByProfileId[manifest.profile_id] = manifest;
        AppendProfileResourcePathErrors(manifest);
        foreach (var error in _validator.validate_manifest(manifest, skillDefs, asOfDate))
            _validationErrors.Add(error);

        foreach (var skillId in manifest.owning_skill_ids)
        {
            if (skillId == "")
                continue;
            if (_profileIdBySkillId.ContainsKey(skillId))
            {
                _validationErrors.Add($"Duplicate battle special profile owning_skill_id registered: {skillId}");
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
            _validationErrors.Add($"Battle special profile {manifest.profile_id} profile_resource must be saved under the sibling profiles directory.");
            return;
        }
        string expectedPrefix = $"{_manifestDirectory.GetBaseDir()}/profiles/";
        if (!profilePath.StartsWith(expectedPrefix))
            _validationErrors.Add($"Battle special profile {manifest.profile_id} profile_resource must be under {expectedPrefix}.");
    }

    private void AppendMissingManifestErrors(Godot.Collections.Dictionary specialProfileIdBySkillId)
    {
        foreach (var skillIdVariant in specialProfileIdBySkillId.Keys)
        {
            var skillId = ProgressionDataUtils.to_string_name(skillIdVariant);
            var profileId = ProgressionDataUtils.to_string_name(specialProfileIdBySkillId[skillIdVariant]);
            if (profileId == "")
                continue;
            if (!_manifestsByProfileId.ContainsKey(profileId))
            {
                _validationErrors.Add($"Battle special profile {profileId} is missing manifest for skill {skillId}.");
                continue;
            }
            if (!_profileIdBySkillId.ContainsKey(skillId) || ProgressionDataUtils.to_string_name(_profileIdBySkillId[skillId]) != profileId)
                _validationErrors.Add($"Battle special profile {profileId} manifest does not own skill {skillId}.");
        }
    }

    private static Godot.Collections.Dictionary CollectSpecialProfileIds(Godot.Collections.Dictionary skillDefs)
    {
        var result = new Godot.Collections.Dictionary();
        if (skillDefs == null)
            return result;
        foreach (var skillIdVariant in skillDefs.Keys)
        {
            var skillId = ProgressionDataUtils.to_string_name(skillIdVariant);
            if (skillId == "")
                continue;
            var skillDef = skillDefs[skillIdVariant].AsGodotObject() as SkillDef;
            var combatProfile = skillDef?.combat_profile as CombatSkillDef;
            if (combatProfile == null || combatProfile.special_resolution_profile_id == "")
                continue;
            result[skillId] = combatProfile.special_resolution_profile_id;
        }
        return result;
    }
}
