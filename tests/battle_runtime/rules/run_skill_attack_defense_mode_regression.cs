using System.Collections.Generic;
using Godot;

public partial class run_skill_attack_defense_mode_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        var hitResolver = new BattleHitResolver();
        var policy = new BattleAttackCheckPolicyService();
        policy.Setup(hitResolver, null);
        try
        {
            TestNormalTouchAndFlatFootedAc(policy, hitResolver);
            TestFlatFootedUsesCappedPositiveAgility(policy);
            TestFlatFootedPreservesNegativeAgility(policy);
            TestTouchComposesWithEquipmentAcAdjustment(hitResolver);
            TestEffectiveLevelModeFlowsThroughPolicy(policy);
        }
        finally
        {
            policy.Dispose();
            hitResolver.Dispose();
        }

        RequestTestExit(_test.Finish("Skill attack defense mode regression"));
    }

    private void TestNormalTouchAndFlatFootedAc(
        BattleAttackCheckPolicyService policy,
        BattleHitResolver hitResolver
    )
    {
        BattleState state = BuildState();
        BattleUnitState caster = BuildCaster("mode_caster", "mode_probe", 1);
        BattleUnitState target = BuildTarget("mode_target", agility: 14, armorMaxDexBonus: -1);
        target.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = "dodge_bonus_up",
                power = 1,
                stacks = 1,
                duration = 20,
            }
        );

        SkillDefinition normal = BuildSkill("mode_probe", "normal");
        SkillDefinition touch = BuildSkill("mode_probe", "touch");
        SkillDefinition flatFooted = BuildSkill("mode_probe", "flat_footed");

        AssertPolicyAndDirectTargetAc(
            policy,
            hitResolver,
            state,
            caster,
            target,
            normal,
            27,
            5,
            "Normal AC should retain all components and the temporary dodge bonus."
        );
        AssertPolicyAndDirectTargetAc(
            policy,
            hitResolver,
            state,
            caster,
            target,
            touch,
            15,
            30,
            "Touch AC should ignore armor, shield, and natural armor only."
        );
        AssertPolicyAndDirectTargetAc(
            policy,
            hitResolver,
            state,
            caster,
            target,
            flatFooted,
            22,
            5,
            "Flat-footed AC should remove positive agility plus static and temporary dodge."
        );
    }

    private void TestFlatFootedUsesCappedPositiveAgility(
        BattleAttackCheckPolicyService policy
    )
    {
        BattleState state = BuildState();
        BattleUnitState caster = BuildCaster("capped_caster", "capped_probe", 1);
        BattleUnitState target = BuildTarget(
            "capped_target",
            agility: 18,
            armorMaxDexBonus: 1
        );
        SkillDefinition flatFooted = BuildSkill("capped_probe", "flat_footed");

        AttackCheckInput check = BuildPolicyCheck(policy, state, caster, target, flatFooted);
        _test.Eq(
            check.TargetArmorClass,
            22,
            "Flat-footed AC should remove the armor-capped +1 agility contribution, not raw +4."
        );
    }

    private void TestFlatFootedPreservesNegativeAgility(
        BattleAttackCheckPolicyService policy
    )
    {
        BattleState state = BuildState();
        BattleUnitState caster = BuildCaster("negative_caster", "negative_probe", 1);
        BattleUnitState target = BuildTarget(
            "negative_target",
            agility: 8,
            armorMaxDexBonus: -1
        );
        SkillDefinition flatFooted = BuildSkill("negative_probe", "flat_footed");

        AttackCheckInput check = BuildPolicyCheck(policy, state, caster, target, flatFooted);
        _test.Eq(
            check.TargetArmorClass,
            21,
            "Flat-footed AC should preserve a negative agility adjustment and remove only dodge."
        );
    }

    private void TestTouchComposesWithEquipmentAcAdjustment(BattleHitResolver hitResolver)
    {
        BattleUnitState caster = BuildCaster("compose_caster", "compose_probe", 1);
        BattleUnitState target = BuildTarget(
            "compose_target",
            agility: 14,
            armorMaxDexBonus: -1
        );
        SkillDefinition touch = BuildSkill("compose_probe", "touch");
        var equipmentAdjustment = new EquipmentAttackDefenseAdjustment();
        equipmentAdjustment.AddComponentMultiplier(
            AttributeContentRules.DeflectionBonus,
            50
        );

        AttackCheckInput check = hitResolver.BuildSkillDefinitionAttackCheck(
            caster,
            target,
            touch,
            0,
            0,
            equipmentAdjustment
        );
        _test.Eq(
            check.TargetArmorClass,
            12,
            "Touch mode should compose with equipment AC multipliers in the same canonical check."
        );
    }

    private void TestEffectiveLevelModeFlowsThroughPolicy(
        BattleAttackCheckPolicyService policy
    )
    {
        BattleState state = BuildState();
        BattleUnitState caster = BuildCaster("level_caster", "level_probe", 1);
        BattleUnitState target = BuildTarget(
            "level_target",
            agility: 14,
            armorMaxDexBonus: -1
        );
        SkillDefinition skill = BuildSkill(
            "level_probe",
            "normal",
            new Dictionary<int, CombatSkillLevelOverrideImportModel>
            {
                [2] = new CombatSkillLevelOverrideImportModel(
                    attackDefenseMode: CombatSkillLevelOverrideAttackDefenseMode.Touch
                ),
            }
        );

        _test.Eq(
            BuildPolicyCheck(policy, state, caster, target, skill).TargetArmorClass,
            25,
            "Level 1 should use the base normal AC mode."
        );
        caster.SetKnownSkillLevelTyped("level_probe", 2);
        _test.Eq(
            BuildPolicyCheck(policy, state, caster, target, skill).TargetArmorClass,
            13,
            "Level 2 should use the projected touch-AC override."
        );
    }

    private void AssertPolicyAndDirectTargetAc(
        BattleAttackCheckPolicyService policy,
        BattleHitResolver hitResolver,
        BattleState state,
        BattleUnitState caster,
        BattleUnitState target,
        SkillDefinition skill,
        int expectedAc,
        int expectedHitRatePercent,
        string message
    )
    {
        AttackCheckInput policyCheck = BuildPolicyCheck(policy, state, caster, target, skill);
        AttackCheckInput directCheck = hitResolver.BuildSkillDefinitionAttackCheck(
            caster,
            target,
            skill,
            0,
            0
        );
        AttackPreviewData preview = policy.BuildAttackPreview(
            policy.BuildSkillDefinitionAttackContext(
                state,
                caster,
                target,
                skill,
                "skill_attack_preview",
                "attack_defense_mode_test",
                false
            )
        );

        _test.Eq(policyCheck.TargetArmorClass, expectedAc, message);
        _test.Eq(
            policyCheck.RequiredRoll,
            expectedAc,
            $"{message} Policy check should expose the fixed required roll for the zero-bonus caster."
        );
        _test.Eq(
            policyCheck.SuccessRatePercent,
            expectedHitRatePercent,
            $"{message} Policy check should expose the fixed d20 success rate."
        );
        _test.Eq(
            directCheck.TargetArmorClass,
            expectedAc,
            $"{message} Direct resolver should resolve the fixed target AC."
        );
        _test.Eq(
            directCheck.RequiredRoll,
            expectedAc,
            $"{message} Direct resolver should expose the fixed required roll for the zero-bonus caster."
        );
        _test.Eq(
            directCheck.SuccessRatePercent,
            expectedHitRatePercent,
            $"{message} Direct resolver should expose the fixed d20 success rate."
        );
        _test.Eq(
            preview?.StageCount ?? 0,
            1,
            $"{message} Preview should contain one attack stage."
        );
        _test.Eq(
            preview != null && preview.Stages.Count > 0
                ? preview.Stages[0].RequiredRoll
                : -1,
            expectedAc,
            $"{message} Preview should expose the fixed required roll."
        );
        _test.Eq(
            preview?.SuccessRatePercent ?? -1,
            expectedHitRatePercent,
            $"{message} Preview should expose the fixed d20 success rate."
        );
    }

    private static AttackCheckInput BuildPolicyCheck(
        BattleAttackCheckPolicyService policy,
        BattleState state,
        BattleUnitState caster,
        BattleUnitState target,
        SkillDefinition skill
    )
    {
        BattleAttackCheckPolicyContext context = policy.BuildSkillDefinitionAttackContext(
            state,
            caster,
            target,
            skill,
            "skill_attack_check",
            "attack_defense_mode_test",
            false
        );
        return policy.BuildAttackCheck(context, 0, 0);
    }

    private static SkillDefinition BuildSkill(
        StringName skillId,
        StringName attackDefenseMode,
        IReadOnlyDictionary<int, CombatSkillLevelOverrideImportModel> levelOverrides = null
    )
    {
        return TestSkillDefinitionProjection.BuildSkill(
            skillId,
            combatProfile: TestSkillDefinitionProjection.BuildCombatProfile(
                skillId,
                attackDefenseMode: attackDefenseMode,
                levelOverrides: levelOverrides
            )
        );
    }

    private static BattleState BuildState() => new()
    {
        battle_id = "skill_attack_defense_mode_regression",
        phase = "unit_acting",
        map_size = new Vector2I(5, 1),
    };

    private static BattleUnitState BuildCaster(
        StringName unitId,
        StringName skillId,
        int skillLevel
    )
    {
        var caster = new BattleUnitState
        {
            unit_id = unitId,
            faction_id = "player",
        };
        caster.SetAnchorCoord(new Vector2I(0, 0));
        caster.SetKnownSkillLevelTyped(skillId, skillLevel);
        caster.attribute_snapshot.SetValue(
            AttributeService.ToStringName(AttributeIdKind.BaseAttackBonus),
            0
        );
        caster.attribute_snapshot.SetValue(
            AttributeService.ToStringName(AttributeIdKind.AttackBonus),
            0
        );
        return caster;
    }

    private static BattleUnitState BuildTarget(
        StringName unitId,
        int agility,
        int armorMaxDexBonus
    )
    {
        var target = new BattleUnitState
        {
            unit_id = unitId,
            faction_id = "enemy",
        };
        target.SetAnchorCoord(new Vector2I(2, 0));
        target.attribute_snapshot.SetValue(
            UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Agility),
            agility
        );
        if (armorMaxDexBonus >= 0)
        {
            target.attribute_snapshot.SetValue(
                AttributeService.ToStringName(AttributeIdKind.ArmorMaxDexBonus),
                armorMaxDexBonus
            );
        }
        int agilityModifier = AttributeSnapshot.CalculateScoreModifier(agility);
        if (armorMaxDexBonus >= 0)
            agilityModifier = Mathf.Min(agilityModifier, armorMaxDexBonus);
        target.attribute_snapshot.SetValue(AttributeContentRules.ArmorAcBonus, 4);
        target.attribute_snapshot.SetValue(AttributeContentRules.ShieldAcBonus, 3);
        target.attribute_snapshot.SetValue(AttributeContentRules.DodgeBonus, 1);
        target.attribute_snapshot.SetValue(AttributeContentRules.DeflectionBonus, 2);
        target.attribute_snapshot.SetValue(
            AttributeContentRules.NaturalArmorAcBonus,
            5
        );
        target.attribute_snapshot.SetValue(
            AttributeService.ToStringName(AttributeIdKind.ArmorClass),
            AttributeService.BASE_ARMOR_CLASS + agilityModifier + 15
        );
        return target;
    }
}
