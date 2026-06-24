using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_power_word_kill_execute_schema_regression : SceneTree
{
    private const string TempSkillDirectory = "user://power_word_kill_execute_schema_regression";
    private readonly TestHarness _test = new();
    private int _validationCaseIndex;

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestFormalPwkShapePasses();
        TestExecuteRejectsWrongSaveAndTargeting();
        TestExecuteRejectsSiblingPassiveSpecialAndGroundShapes();
        TestExecuteRejectsOldFieldsAndHiddenSiblings();
        TestFormalResourceLoadsAndValidates();

        Quit(_test.Finish("Power Word Kill execute schema regression"));
    }

    private void TestFormalPwkShapePasses()
    {
        GStringArray errors = ValidateSkill(FormalPwkSkill());
        _test.Eq(errors.Count, 0, $"formal PWK execute shape should pass. errors={FormatErrors(errors)}");
    }

    private void TestExecuteRejectsWrongSaveAndTargeting()
    {
        SkillDef skill = FormalPwkSkill();
        skill.combat_profile.target_mode = "ground";
        skill.combat_profile.target_team_filter = "any";
        skill.combat_profile.target_selection_mode = "random_chain";
        skill.combat_profile.min_target_count = 2;
        skill.combat_profile.max_target_count = 2;
        skill.combat_profile.allow_repeat_target = true;
        skill.combat_profile.area_pattern = "circle";
        skill.combat_profile.area_value = 2;
        CombatEffectDef effect = skill.combat_profile.effect_defs[0];
        effect.effect_target_team_filter = "ally";
        effect.save_dc_mode = "static";
        effect.save_dc = 12;
        effect.save_dc_source_ability = "fortune";
        effect.save_ability = "constitution";
        effect.save_tag = "magic";
        effect.damage_tag = "fire";
        effect.save_partial_on_success = true;

        string errors = FormatErrors(ValidateSkill(skill));

        AssertContains(errors, "combat_profile.target_mode", "execute should require unit target_mode.");
        AssertContains(errors, "combat_profile.target_team_filter", "execute should require enemy target filter.");
        AssertContains(errors, "combat_profile.target_selection_mode", "execute should require single target selection.");
        AssertContains(errors, "combat_profile.min_target_count", "execute should require min target count 1.");
        AssertContains(errors, "combat_profile.max_target_count", "execute should require max target count 1.");
        AssertContains(errors, "combat_profile.allow_repeat_target", "execute should disallow repeat targets.");
        AssertContains(errors, "combat_profile.area_pattern", "execute should require single area pattern.");
        AssertContains(errors, "combat_profile.area_value", "execute should require zero area value.");
        AssertContains(errors, "effect_target_team_filter", "execute effect should target enemies.");
        AssertContains(errors, "save_dc_mode", "execute should use caster_spell save DC mode.");
        AssertContains(errors, "save_dc", "execute should not set static save_dc.");
        AssertContains(errors, "save_dc_source_ability", "execute should use intelligence as save source.");
        AssertContains(errors, "save_ability", "execute should save against willpower.");
        AssertContains(errors, "save_tag", "execute should use execute save tag.");
        AssertContains(errors, "damage_tag", "execute should use negative energy.");
        AssertContains(errors, "save_partial_on_success", "execute should not use partial save.");
    }

    private void TestExecuteRejectsSiblingPassiveSpecialAndGroundShapes()
    {
        SkillDef skill = FormalPwkSkill();
        skill.combat_profile.special_resolution_profile_id = "meteor_swarm";
        skill.combat_profile.effect_defs.Add(new CombatEffectDef
        {
            effect_type = "damage",
            power = 1,
            damage_tag = "fire",
        });
        skill.combat_profile.passive_effect_defs.Add(FormalExecuteEffect());
        skill.combat_profile.cast_variants.Add(new CombatCastVariantDef
        {
            variant_id = "ground_execute",
            target_mode = "ground",
            effect_defs = new Godot.Collections.Array<CombatEffectDef>
            {
                FormalExecuteEffect(),
            },
        });

        string errors = FormatErrors(ValidateSkill(skill));

        AssertContains(errors, "exactly one execute effect", "execute should not allow sibling effects.");
        AssertContains(errors, "passive_effect_defs", "execute should not be legal in passive effects.");
        AssertContains(errors, "special_resolution_profile_id", "execute should not combine with special profile.");
        AssertContains(errors, "cast_variants[0]", "execute should validate merged cast variant shape.");
    }

    private void TestExecuteRejectsOldFieldsAndHiddenSiblings()
    {
        SkillDef skill = FormalPwkSkill();
        CombatEffectDef effect = skill.combat_profile.effect_defs[0];
        effect.threshold_cap_max_hp_ratio_percent = 10;
        effect.threshold_max_hp_ratio_percent = 20;
        effect.soul_fracture_duration_tu = 61;
        effect.heal_multiplier_percent = 101;
        effect.shield_gain_multiplier_percent = -1;
        effect.trigger_event = "critical_hit";
        effect.trigger_condition = "battle_start";
        effect.@params = new GDictionary
        {
            ["staged_execution"] = true,
            ["threshold_ability_mod"] = "intelligence_modifier",
        };

        string errors = FormatErrors(ValidateSkill(skill));

        AssertContains(errors, "threshold_cap_max_hp_ratio_percent", "execute cap should be >= threshold ratio.");
        AssertContains(errors, "soul_fracture_duration_tu", "soul fracture duration should be positive TU granularity.");
        AssertContains(errors, "heal_multiplier_percent", "heal multiplier should be clamped by schema.");
        AssertContains(errors, "shield_gain_multiplier_percent", "shield multiplier should be clamped by schema.");
        AssertContains(errors, "trigger_event", "execute should not carry hidden trigger event.");
        AssertContains(errors, "trigger_condition", "execute should not carry hidden trigger condition.");
        AssertContains(errors, "must not use params payload", "execute should reject old params payload.");
    }

    private void TestFormalResourceLoadsAndValidates()
    {
        SkillDef skill = ResourceLoader.Load<SkillDef>(
            "res://data/configs/skills/mage_power_word_kill.tres"
        );
        _test.True(skill != null, "formal mage_power_word_kill resource should load.");
        if (skill == null)
        {
            return;
        }
        GStringArray errors = ValidateSkill(skill);
        _test.Eq(
            errors.Count,
            0,
            $"formal mage_power_word_kill should validate. errors={FormatErrors(errors)}"
        );
    }

    private static CombatEffectDef FormalExecuteEffect() => new()
    {
        effect_type = "execute",
        effect_target_team_filter = "enemy",
        damage_tag = "negative_energy",
        save_dc_mode = "caster_spell",
        save_dc = 0,
        save_dc_source_ability = "intelligence",
        save_ability = "willpower",
        save_tag = "execute",
        threshold_max_hp_ratio_percent = 20,
        threshold_level_anchor = 17,
        threshold_level_bonus_per_delta = 5,
        threshold_cap_max_hp_ratio_percent = 50,
        soul_fracture_duration_tu = 60,
        heal_multiplier_percent = 50,
        shield_gain_multiplier_percent = 50,
    };

    private static SkillDef FormalPwkSkill()
    {
        SkillDef skill = new()
        {
            skill_id = "mage_power_word_kill",
            display_name = "Power Word Kill",
            icon_id = "power_word_kill",
            skill_type = "active",
            max_level = 5,
            non_core_max_level = 3,
            mastery_curve = new[] { 10, 20, 30, 40, 50 },
        };
        CombatSkillDef combat = new()
        {
            skill_id = skill.skill_id,
            target_mode = "unit",
            target_team_filter = "enemy",
            target_selection_mode = "single_unit",
            min_target_count = 1,
            max_target_count = 1,
            allow_repeat_target = false,
            area_pattern = "single",
            area_value = 0,
            effect_defs = new Godot.Collections.Array<CombatEffectDef> { FormalExecuteEffect() },
        };
        skill.combat_profile = combat;
        return skill;
    }

    private GStringArray ValidateSkill(SkillDef skill)
    {
        CleanupTempSkillDirectory();
        _test.Eq(
            DirAccess.MakeDirRecursiveAbsolute(ProjectSettings.GlobalizePath(TempSkillDirectory)),
            Error.Ok,
            "should create temp PWK schema directory."
        );
        _validationCaseIndex++;
        string path = $"{TempSkillDirectory}/{skill.skill_id}_{_validationCaseIndex}.tres";
        _test.Eq(ResourceSaver.Save(skill, path), Error.Ok, "should save temp skill resource.");

        using SkillContentRegistry registry = new();
        registry.LoadFromDirectory(TempSkillDirectory);
        return registry.Validate();
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
