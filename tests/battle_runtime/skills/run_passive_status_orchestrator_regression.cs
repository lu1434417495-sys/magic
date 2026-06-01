using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_passive_status_orchestrator_regression : SceneTree
{
    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestPassiveContextAndResolversNoLongerRequireGodotRegistration();
        TestFactoryProjectsIdentityPassivesFromCharacterGateway();
        TestOrchestratorProjectsRaceAndSubracePassives();
        TestOrchestratorSuppressesOriginalRacePassivesForAscension();
        TestOrchestratorProjectsShootingSpecializationBowOnlyRangeBonus();

        if (_failures.Count == 0)
        {
            GD.Print("Passive status orchestrator regression: PASS");
            Quit(0);
            return;
        }

        foreach (string failure in _failures)
            GD.PushError(failure);
        GD.Print($"Passive status orchestrator regression: FAIL ({_failures.Count})");
        Quit(1);
    }

    private void TestPassiveContextAndResolversNoLongerRequireGodotRegistration()
    {
        AssertPlainType(typeof(PassiveSourceContext), nameof(PassiveSourceContext));
        AssertPlainType(typeof(PassiveStatusOrchestrator), nameof(PassiveStatusOrchestrator));
        AssertPlainType(typeof(RaceTraitResolver), nameof(RaceTraitResolver));
        AssertPlainType(typeof(AscensionTraitResolver), nameof(AscensionTraitResolver));
        AssertPlainType(typeof(SkillPassiveResolver), nameof(SkillPassiveResolver));
    }

    private void TestFactoryProjectsIdentityPassivesFromCharacterGateway()
    {
        ProgressionContentRegistry registry = new();
        PartyState partyState = MakePartyState(new[] { new StringName("hero") });
        CharacterManagementModule gateway = new();
        gateway.setup(
            partyState,
            registry.get_skill_defs(),
            registry.get_profession_defs(),
            new GDictionary(),
            new GDictionary(),
            new GDictionary(),
            null,
            registry.get_bundle()
        );

        BattleRuntimeModule runtime = new();
        runtime.setup(gateway, registry.get_skill_defs());
        Godot.Collections.Array units = runtime._unit_factory.build_ally_units(
            partyState,
            new GDictionary()
        );

        AssertEq(units.Count, 1, "factory should build one ally for passive projection.");
        if (units.Count == 0)
            return;
        var unit = units[0].As<BattleUnitState>();
        AssertTrue(
            unit.race_trait_ids.Contains("human_versatility"),
            "race trait ids should include human_versatility from RaceDef."
        );
        AssertTrue(
            unit.race_trait_ids.Contains("civil_militia"),
            "race trait ids should include civil_militia from RaceDef."
        );
        AssertFalse(unit.race_trait_ids.Contains("darkvision"), "humans should not project darkvision.");
        AssertEq(unit.subrace_trait_ids.Count, 0, "common_human should not add placeholder subrace traits.");
        AssertTrue(
            unit.vision_tags.Contains("normal_vision"),
            "vision tags should include normal_vision from RaceDef."
        );
        AssertTrue(
            unit.proficiency_tags.Contains("civilian"),
            "proficiency tags should include civilian from RaceDef."
        );
        AssertTrue(
            unit.proficiency_tags.Contains("weapon_type_spear"),
            "civil_militia should project spear proficiency tag."
        );

        runtime.dispose();
        gateway.Dispose();
        registry.dispose();
    }

    private void TestOrchestratorProjectsRaceAndSubracePassives()
    {
        BattleUnitState unit = MakeBattleUnit("race_projection_unit");
        PassiveSourceContext context = new()
        {
            race_def = MakeRaceDef(),
            subrace_def = MakeSubraceDef(),
        };

        PassiveStatusOrchestrator.apply_to_unit(unit, context, new Dictionary<StringName, SkillDef>());

        AssertTrue(unit.race_trait_ids.Contains("test_race_trait"), "race trait should be projected.");
        AssertTrue(
            unit.subrace_trait_ids.Contains("test_subrace_trait"),
            "subrace trait should be projected."
        );
        AssertTrue(unit.vision_tags.Contains("darkvision"), "race vision tag should be projected.");
        AssertTrue(
            unit.save_advantage_tags.Contains("poison"),
            "subrace save advantage tag should be projected."
        );
        AssertEq(
            GetStringName(unit.damage_resistances, "fire"),
            new StringName("half"),
            "race damage resistance should be projected."
        );
        AssertEq(
            GetInt(unit.per_battle_charges, "racial_skill_dragon_breath_test", 0),
            2,
            "race per-battle charge should be initialized."
        );
        AssertEq(
            GetInt(unit.per_turn_charges, "racial_skill_nimble_escape_test", 0),
            1,
            "subrace per-turn charge should be initialized."
        );
    }

    private void TestOrchestratorSuppressesOriginalRacePassivesForAscension()
    {
        BattleUnitState unit = MakeBattleUnit("ascension_projection_unit");
        PassiveSourceContext context = new()
        {
            race_def = MakeRaceDef(),
            subrace_def = MakeSubraceDef(),
            ascension_def = MakeAscensionDef(true),
        };

        PassiveStatusOrchestrator.apply_to_unit(unit, context, new Dictionary<StringName, SkillDef>());

        AssertFalse(
            unit.race_trait_ids.Contains("test_race_trait"),
            "suppressed race trait should not be projected."
        );
        AssertFalse(
            unit.subrace_trait_ids.Contains("test_subrace_trait"),
            "suppressed subrace trait should not be projected."
        );
        AssertFalse(
            unit.per_battle_charges.ContainsKey("racial_skill_dragon_breath_test"),
            "suppressed race charge should not be initialized."
        );
        AssertFalse(
            unit.per_turn_charges.ContainsKey("racial_skill_nimble_escape_test"),
            "suppressed subrace charge should not be initialized."
        );
        AssertTrue(
            unit.ascension_trait_ids.Contains("ascended_trait"),
            "ascension trait should still be projected."
        );
        AssertEq(
            GetInt(unit.per_battle_charges, "racial_skill_ascension_ray_test", 0),
            3,
            "ascension charge should be initialized."
        );
    }

    private void TestOrchestratorProjectsShootingSpecializationBowOnlyRangeBonus()
    {
        ProgressionContentRegistry registry = new();
        BattleUnitState unit = MakeBattleUnit("shooting_specialization_unit");
        PassiveSourceContext context = new()
        {
            unit_progress = new UnitProgress(),
        };
        UnitSkillProgress skillProgress = new()
        {
            skill_id = "archer_shooting_specialization",
            is_learned = true,
            skill_level = 0,
            profession_granted_by = "archer",
            granted_source_type = "profession",
            granted_source_id = "archer",
        };
        context.unit_progress.set_skill_progress(skillProgress);
        UnitProfessionProgress professionProgress = new()
        {
            profession_id = "archer",
            rank = 1,
            is_active = true,
        };
        context.unit_progress.set_profession_progress(professionProgress);

        PassiveStatusOrchestrator.apply_to_unit(unit, context, IndexSkillDefs(registry.get_skill_defs()));

        BattleStatusEffectState status = unit.get_status_effect("archer_shooting_specialization");
        AssertTrue(status != null, "shooting specialization should project a battle status.");
        if (status != null)
        {
            AssertEq(
                GetInt(status.@params, "skill_level", -1),
                0,
                "shooting specialization status should keep learned level 0."
            );
            AssertEq(
                GetInt(status.@params, "range_bonus", 0),
                1,
                "shooting specialization status should carry range_bonus=1."
            );
        }

        SkillDef weaponSkill = MakeWeaponRangeSkill();
        unit.apply_weapon_projection(
            new GDictionary
            {
                ["weapon_profile_kind"] = "equipped",
                ["weapon_item_id"] = "test_shortbow",
                ["weapon_profile_type_id"] = "shortbow",
                ["weapon_family"] = "bow",
                ["weapon_current_grip"] = "two_handed",
                ["weapon_attack_range"] = 4,
                ["weapon_two_handed_dice"] = new GDictionary
                {
                    ["dice_count"] = 1,
                    ["dice_sides"] = 6,
                    ["flat_bonus"] = 0,
                },
                ["weapon_uses_two_hands"] = true,
                ["weapon_physical_damage_tag"] = "physical_pierce",
            }
        );
        AssertEq(
            BattleRangeService.get_effective_skill_range(unit, weaponSkill),
            5,
            "shooting specialization should add +1 range for bow weapons."
        );

        unit.apply_weapon_projection(
            new GDictionary
            {
                ["weapon_profile_kind"] = "equipped",
                ["weapon_item_id"] = "test_crossbow",
                ["weapon_profile_type_id"] = "light_crossbow",
                ["weapon_family"] = "crossbow",
                ["weapon_current_grip"] = "two_handed",
                ["weapon_attack_range"] = 5,
                ["weapon_two_handed_dice"] = new GDictionary
                {
                    ["dice_count"] = 1,
                    ["dice_sides"] = 8,
                    ["flat_bonus"] = 0,
                },
                ["weapon_uses_two_hands"] = true,
                ["weapon_physical_damage_tag"] = "physical_pierce",
            }
        );
        AssertEq(
            BattleRangeService.get_effective_skill_range(unit, weaponSkill),
            5,
            "shooting specialization must not add range for crossbows."
        );

        registry.dispose();
    }

    private static BattleUnitState MakeBattleUnit(StringName unitId)
    {
        return new BattleUnitState
        {
            unit_id = unitId,
            source_member_id = unitId,
            faction_id = "player",
            control_mode = "manual",
        };
    }

    private static SkillDef MakeWeaponRangeSkill()
    {
        SkillDef skill = new()
        {
            skill_id = "test_weapon_range_skill",
            skill_type = "active",
            tags = new Godot.Collections.Array<StringName> { "archer", "ranged", "bow" },
        };
        CombatSkillDef combatProfile = new()
        {
            skill_id = skill.skill_id,
            target_mode = "unit",
            target_team_filter = "enemy",
            target_selection_mode = "single_unit",
            selection_order_mode = "stable",
            range_value = 1,
        };
        skill.combat_profile = combatProfile;
        return skill;
    }

    private static RaceDef MakeRaceDef()
    {
        RaceDef race = new()
        {
            race_id = "test_race",
            display_name = "Test Race",
            damage_resistances = new GDictionary { ["fire"] = "half" },
        };
        race.trait_ids.Add("test_race_trait");
        race.vision_tags.Add("darkvision");
        race.racial_granted_skills.Add(MakeRacialGrant("dragon_breath_test", "per_battle", 2));
        return race;
    }

    private static SubraceDef MakeSubraceDef()
    {
        SubraceDef subrace = new()
        {
            subrace_id = "test_subrace",
            parent_race_id = "test_race",
            display_name = "Test Subrace",
        };
        subrace.trait_ids.Add("test_subrace_trait");
        subrace.save_advantage_tags.Add("poison");
        subrace.racial_granted_skills.Add(MakeRacialGrant("nimble_escape_test", "per_turn", 1));
        return subrace;
    }

    private static AscensionDef MakeAscensionDef(bool suppressesOriginalRaceTraits)
    {
        AscensionDef ascension = new()
        {
            ascension_id = "test_ascension",
            display_name = "Test Ascension",
            suppresses_original_race_traits = suppressesOriginalRaceTraits,
        };
        ascension.trait_ids.Add("ascended_trait");
        ascension.racial_granted_skills.Add(MakeRacialGrant("ascension_ray_test", "per_battle", 3));
        return ascension;
    }

    private static RacialGrantedSkill MakeRacialGrant(
        StringName skillId,
        StringName chargeKind,
        int charges
    )
    {
        return new RacialGrantedSkill
        {
            skill_id = skillId,
            minimum_skill_level = 1,
            charge_kind = chargeKind,
            charges = charges,
        };
    }

    private static PartyState MakePartyState(IEnumerable<StringName> memberIds)
    {
        PartyState partyState = new();
        foreach (StringName memberId in memberIds)
        {
            PartyMemberState memberState = new()
            {
                member_id = memberId,
                display_name = memberId.ToString().Capitalize(),
                race_id = "human",
                subrace_id = "common_human",
            };
            memberState.progression.unit_id = memberId;
            memberState.progression.display_name = memberState.display_name;
            partyState.set_member_state(memberState);
            partyState.active_member_ids.Add(memberId);
            if (partyState.leader_member_id == "")
                partyState.leader_member_id = memberId;
            if (partyState.main_character_member_id == "")
                partyState.main_character_member_id = memberId;
        }
        return partyState;
    }

    private static Dictionary<StringName, SkillDef> IndexSkillDefs(GDictionary skillDefs)
    {
        var result = new Dictionary<StringName, SkillDef>();
        if (skillDefs == null)
            return result;
        foreach (Variant key in skillDefs.Keys)
        {
            SkillDef skillDef = skillDefs[key].As<SkillDef>();
            if (skillDef == null)
                continue;
            StringName id =
                skillDef.skill_id != ""
                    ? skillDef.skill_id
                    : ProgressionDataUtils.to_string_name(key);
            if (id != "")
                result[id] = skillDef;
        }
        return result;
    }

    private static int GetInt(GDictionary source, StringName key, int fallback)
    {
        if (source == null || !source.ContainsKey(key))
            return fallback;
        Variant value = source[key];
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : fallback;
    }

    private static StringName GetStringName(GDictionary source, StringName key)
    {
        if (source == null || !source.ContainsKey(key))
            return "";
        return ProgressionDataUtils.to_string_name(source[key]);
    }

    private void AssertPlainType(Type type, string typeName)
    {
        AssertFalse(
            typeof(GodotObject).IsAssignableFrom(type),
            $"{typeName} 应是普通 C# 类型，不应继承 GodotObject/RefCounted。"
        );
        AssertFalse(
            type.GetCustomAttributes(typeof(GlobalClassAttribute), inherit: false).Length > 0,
            $"{typeName} 不应继续注册为 Godot GlobalClass。"
        );
    }

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
            _failures.Add(message);
    }

    private void AssertFalse(bool condition, string message)
    {
        if (condition)
            _failures.Add(message);
    }

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!EqualityComparer<T>.Default.Equals(actual, expected))
            _failures.Add($"{message} | actual={actual} expected={expected}");
    }
}
