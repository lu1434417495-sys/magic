using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_phantasmal_kill_schema_regression : LifecycleTestSceneTree
{
    private const string TempSkillDirectory = "user://phantasmal_kill_schema_regression";
    private readonly TestHarness _test = new();
    private readonly List<SkillDef> _validationSkillRoots = new();
    private readonly List<SkillContentRegistry> _validationRegistries = new();
    private readonly List<TestContentResourceLoader> _validationLoaders = new();
    private readonly List<GStringArray> _validationResults = new();
    private int _validationCaseIndex;

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestFormalResourceLoadsAndValidates();
        if (_test.Failures.Count == 0)
            TestFormalPhantasmalKillShapePasses();
        if (_test.Failures.Count == 0)
        {
            TestGradedSaveExecuteRejectsWrongSaveAndTargeting();
            TestPhantasmalKillRejectsWrongBindingCombatProfileShape();
            TestGradedSaveExecuteRejectsUnknownOrMissingParams();
            TestPhantasmalKillRequiresNineLevelDescriptionCoverage();
        }

        TestResult result = _test.Finish("Phantasmal Kill schema regression");
        ReleaseValidationResources();
        RequestTestExit(result);
    }

    private void TestFormalPhantasmalKillShapePasses()
    {
        GStringArray errors = ValidateSkill(FormalPhantasmalKillSkill());
        _test.Eq(
            errors.Count,
            0,
            $"formal Phantasmal Kill schema shape should pass. errors={FormatErrors(errors)}"
        );
    }

    private void TestFormalResourceLoadsAndValidates()
    {
        SkillDef skill = ResourceLoader.Load<SkillDef>(
            "res://data/configs/skills/mage_phantasmal_kill.tres",
            cacheMode: ResourceLoader.CacheMode.IgnoreDeep
        );
        _test.True(skill != null, "formal mage_phantasmal_kill resource should load.");
        if (skill == null)
            return;

        GStringArray errors = ValidateSkill(skill);
        _test.Eq(
            errors.Count,
            0,
            $"formal mage_phantasmal_kill should validate. errors={FormatErrors(errors)}"
        );
    }

    private void TestGradedSaveExecuteRejectsWrongSaveAndTargeting()
    {
        SkillDef skill = FormalPhantasmalKillSkill();
        CombatEffectDef effect = skill.combat_profile.effect_defs[0];
        effect.effect_target_team_filter = "enemy";
        effect.save_dc_mode = "static";
        effect.save_dc = 12;
        effect.save_dc_source_ability = "fortune";
        effect.save_ability = "constitution";
        effect.save_tag = "magic";
        effect.damage_tag = "fire";
        effect.save_partial_on_success = true;

        string errors = FormatErrors(ValidateSkill(skill));

        AssertContains(
            errors,
            "effect_target_team_filter",
            "graded save execute should require any target filter."
        );
        AssertContains(errors, "save_dc_mode", "graded save execute should use caster_spell.");
        AssertContains(errors, "save_dc", "graded save execute should not set static save_dc.");
        AssertContains(
            errors,
            "save_dc_source_ability",
            "graded save execute should use intelligence as save source."
        );
        AssertContains(
            errors,
            "save_ability",
            "graded save execute should save against willpower."
        );
        AssertContains(errors, "save_tag", "graded save execute should use illusion save tag.");
        AssertContains(errors, "damage_tag", "graded save execute should use psychic damage.");
        AssertContains(
            errors,
            "save_partial_on_success",
            "graded save execute should not use partial save."
        );
    }

    private void TestPhantasmalKillRejectsWrongBindingCombatProfileShape()
    {
        SkillDef skill = FormalPhantasmalKillSkill();
        skill.combat_profile.target_mode = "unit";
        skill.combat_profile.target_team_filter = "enemy";
        skill.combat_profile.target_selection_mode = "single_unit";
        skill.combat_profile.area_pattern = "single";
        skill.combat_profile.area_value = 0;
        skill.combat_profile.special_resolution_profile_id = "meteor_swarm";

        string errors = FormatErrors(ValidateSkill(skill));

        AssertContains(
            errors,
            "combat_profile.target_mode",
            "Phantasmal Kill should require ground target_mode."
        );
        AssertContains(
            errors,
            "combat_profile.target_team_filter",
            "Phantasmal Kill should require any target filter."
        );
        AssertContains(
            errors,
            "combat_profile.target_selection_mode",
            "Phantasmal Kill should require single_coord target selection."
        );
        AssertContains(
            errors,
            "combat_profile.area_pattern",
            "Phantasmal Kill should require square area pattern."
        );
        AssertContains(
            errors,
            "combat_profile.area_value",
            "Phantasmal Kill should require area value 3."
        );
        AssertContains(
            errors,
            "combat_profile.special_resolution_profile_id",
            "Phantasmal Kill should not bind a special resolution profile."
        );
    }

    private void TestGradedSaveExecuteRejectsUnknownOrMissingParams()
    {
        SkillDef skill = FormalPhantasmalKillSkill();
        CombatEffectDef effect = skill.combat_profile.effect_defs[0];
        effect.@params.Remove("failure_damage_dice_count");
        effect.@params["profile_id"] = "other_profile";
        effect.@params["failure_execute_threshold_fixed"] = -1;
        effect.@params["failure_execute_threshold_max_hp_percent"] = 0;
        effect.@params["failure_damage_dice_sides"] = 0;
        effect.@params["failure_frightened_duration_tu"] = 7;
        effect.@params["critical_failure_execute_threshold_max_hp_percent"] = 101;
        effect.@params["critical_failure_damage_dice_count"] = 0;
        effect.@params["critical_failure_stunned_duration_tu"] = 0;
        effect.@params["unexpected_payload"] = 1;

        string errors = FormatErrors(ValidateSkill(skill));

        AssertContains(errors, "params.profile_id", "profile_id must be the formal profile.");
        AssertContains(
            errors,
            "params.failure_damage_dice_count",
            "missing dice count should be rejected."
        );
        AssertContains(
            errors,
            "params.unexpected_payload",
            "unknown params should be rejected."
        );
        AssertContains(
            errors,
            "params.failure_execute_threshold_fixed",
            "fixed execute threshold should be non-negative."
        );
        AssertContains(
            errors,
            "params.failure_execute_threshold_max_hp_percent",
            "failure threshold percent should be 1..100."
        );
        AssertContains(
            errors,
            "params.failure_damage_dice_sides",
            "failure damage dice sides should be positive."
        );
        AssertContains(
            errors,
            "params.failure_frightened_duration_tu",
            "failure frightened duration should use TU granularity."
        );
        AssertContains(
            errors,
            "params.critical_failure_execute_threshold_max_hp_percent",
            "critical failure threshold percent should be 1..100."
        );
        AssertContains(
            errors,
            "params.critical_failure_damage_dice_count",
            "critical failure dice count should be positive."
        );
        AssertContains(
            errors,
            "params.critical_failure_stunned_duration_tu",
            "critical failure stunned duration should be positive TU."
        );
    }

    private void TestPhantasmalKillRequiresNineLevelDescriptionCoverage()
    {
        SkillDef skill = FormalPhantasmalKillSkill();
        GDictionary configs = BuildLevelDescriptionConfigs();
        configs.Remove("0");
        configs.Remove("9");
        skill.level_description_configs = configs;

        string errors = FormatErrors(ValidateSkill(skill));

        AssertContains(
            errors,
            "level_description_configs must include level 0",
            "Phantasmal Kill should require a level 0 description config."
        );
        AssertContains(
            errors,
            "level_description_configs must include level 9",
            "Phantasmal Kill should require a level 9 description config."
        );
    }

    private static SkillDef FormalPhantasmalKillSkill()
    {
        SkillDef skill = new()
        {
            skill_id = "mage_phantasmal_kill",
            display_name = "Phantasmal Kill",
            icon_id = "mage_phantasmal_kill",
            skill_type = "active",
            max_level = 9,
            non_core_max_level = 7,
            mastery_curve = new[] { 10, 20, 30, 40, 50, 60, 70, 80, 90 },
            growth_tier = "ultimate",
            attribute_growth_progress = new GDictionary
            {
                ["intelligence"] = 160,
                ["willpower"] = 80,
            },
            level_description_template = "range {range}",
            level_description_configs = BuildLevelDescriptionConfigs(),
        };
        CombatSkillDef combat = new()
        {
            skill_id = skill.skill_id,
            target_mode = "ground",
            target_team_filter = "any",
            target_selection_mode = "single_coord",
            selection_order_mode = "stable",
            range_value = 12,
            area_pattern = "square",
            area_value = 3,
            effect_defs = new Godot.Collections.Array<CombatEffectDef>
            {
                FormalGradedSaveExecuteEffect(),
            },
        };
        skill.combat_profile = combat;
        return skill;
    }

    private static CombatEffectDef FormalGradedSaveExecuteEffect() => new()
    {
        effect_type = "graded_save_execute",
        effect_target_team_filter = "any",
        damage_tag = "psychic",
        save_dc_mode = "caster_spell",
        save_dc = 0,
        save_dc_source_ability = "intelligence",
        save_ability = "willpower",
        save_tag = "illusion",
        save_partial_on_success = false,
        @params = new GDictionary
        {
            ["profile_id"] = "phantasmal_kill",
            ["failure_execute_threshold_fixed"] = 50,
            ["failure_execute_threshold_max_hp_percent"] = 25,
            ["failure_damage_dice_count"] = 6,
            ["failure_damage_dice_sides"] = 6,
            ["failure_frightened_duration_tu"] = 60,
            ["failure_reaction_lock_duration_tu"] = 30,
            ["critical_failure_execute_threshold_max_hp_percent"] = 35,
            ["critical_failure_damage_dice_count"] = 10,
            ["critical_failure_damage_dice_sides"] = 6,
            ["critical_failure_frightened_duration_tu"] = 90,
            ["critical_failure_stunned_duration_tu"] = 30,
            ["success_aftershock_duration_tu"] = 30,
        },
    };

    private static GDictionary BuildLevelDescriptionConfigs()
    {
        GDictionary configs = new();
        for (int level = 0; level <= 9; level++)
        {
            configs[level.ToString()] = new GDictionary { ["range"] = 12 };
        }
        return configs;
    }

    private GStringArray ValidateSkill(SkillDef skill)
    {
        _validationSkillRoots.Add(skill);
        CleanupTempSkillDirectory();
        _test.Eq(
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(TempSkillDirectory)),
            Error.Ok,
            "should create temp Phantasmal Kill schema directory."
        );
        _validationCaseIndex++;
        string path = $"{TempSkillDirectory}/{skill.skill_id}_{_validationCaseIndex}.tres";
        _test.Eq(ResourceSaver.Save(skill, path), Error.Ok, "should save temp skill resource.");

        var loader = new TestContentResourceLoader();
        var registry = new SkillContentRegistry(loader, loadDefaultContent: false);
        _validationLoaders.Add(loader);
        _validationRegistries.Add(registry);
        registry.LoadFromDirectory(TempSkillDirectory);
        GStringArray validationResult = registry.Validate();
        _validationResults.Add(validationResult);
        return validationResult;
    }

    private void ReleaseValidationResources()
    {
        _validationResults.Clear();
        for (int index = _validationRegistries.Count - 1; index >= 0; index--)
            _validationRegistries[index].Dispose();
        _validationRegistries.Clear();
        for (int index = _validationLoaders.Count - 1; index >= 0; index--)
            _validationLoaders[index].Dispose();
        _validationLoaders.Clear();
        _validationSkillRoots.Clear();
        CleanupTempSkillDirectory();
    }

    private static void CleanupTempSkillDirectory()
    {
        string absolute = ProjectSettings.GlobalizePath(TempSkillDirectory);
        if (!DirAccess.DirExistsAbsolute(absolute))
            return;
        using DirAccess dir = DirAccess.Open(TempSkillDirectory);
        if (dir == null)
            return;
        dir.ListDirBegin();
        while (true)
        {
            string entry = dir.GetNext();
            if (string.IsNullOrEmpty(entry))
                break;
            if (entry == "." || entry == "..")
                continue;
            dir.Remove(entry);
        }
        dir.ListDirEnd();
        DirAccess.RemoveAbsolute(absolute);
    }

    private void AssertContains(string haystack, string needle, string message)
    {
        _test.True(haystack.Contains(needle), $"{message} errors={haystack}");
    }

    private static string FormatErrors(GStringArray errors)
    {
        return string.Join(" | ", errors);
    }
}
