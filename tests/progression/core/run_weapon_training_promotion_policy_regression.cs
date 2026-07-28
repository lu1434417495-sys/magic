using System;
using System.Collections.Generic;
using Godot;

public partial class run_weapon_training_promotion_policy_regression
    : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestWeaponTrainingCannotBecomeProfessionTrigger();
        RequestTestExit(
            _test.Finish(
                "Weapon training promotion policy regression"
            )
        );
    }

    private void TestWeaponTrainingCannotBecomeProfessionTrigger()
    {
        StringName skillId = "test_sword_training";
        SkillDefinition weaponTraining =
            TestSkillDefinitionProjection.BuildSkill(
                skillId,
                maxLevel: 1,
                tags: new[] { new StringName("weapon_training") }
            );
        var skillDefinitions =
            new Dictionary<StringName, SkillDefinition>
            {
                [skillId] = weaponTraining,
            };
        UnitProgress progress = BuildProgress(skillId);
        var member = new PartyMemberState
        {
            member_id = "weapon_training_member",
            display_name = "Weapon Training Member",
            progression = progress,
        };

        var progression = new ProgressionService();
        progression.SetupDefinitions(
            progress,
            skillDefinitions,
            new Dictionary<StringName, ProfessionDefinition>()
        );
        _test.True(
            progression.SetSkillCore(skillId, true),
            "weapon training can still be learned and marked core."
        );

        var growth = new LevelGrowthEvaluationService();
        growth.Setup(skillDefinitions);
        LevelGrowthTriggerResult setResult =
            growth.SetActiveTriggerCoreSkillTyped(member, skillId);
        _test.False(
            setResult.Ok,
            "weapon training must not become an active level trigger."
        );
        _test.Eq(
            setResult.Error,
            "skill_cannot_trigger_profession_promotion",
            "weapon training rejection must use the stable policy error."
        );
        _test.Eq(
            progress.active_level_trigger_core_skill_id,
            new StringName(""),
            "rejected trigger selection must not mutate active trigger."
        );

        progress.active_level_trigger_core_skill_id = skillId;
        UnitSkillProgress skillProgress =
            progress.GetSkillProgress(skillId);
        skillProgress.is_level_trigger_active = true;
        progress.SetSkillProgress(skillProgress);

        _test.False(
            growth.IsActiveTriggerReadyForLevelUp(member),
            "an illegally injected weapon-training trigger must fail readiness."
        );
        LevelGrowthTriggerResult levelResult =
            growth.ApplyLevelUpTyped(member);
        _test.False(
            levelResult.Ok,
            "an illegally injected weapon-training trigger must not level up."
        );
        _test.Eq(
            levelResult.Error,
            "trigger_skill_not_ready",
            "illegal weapon-training trigger must fail through readiness."
        );
        _test.False(
            skillProgress.is_level_trigger_locked,
            "failed level-up must not lock the weapon-training skill."
        );
        _test.Eq(
            progress.PendingProfessionChoicesTyped.Count,
            0,
            "weapon training must not create profession choices."
        );

        StringName professionId =
            "weapon_training_profession";
        var rankRequirement =
            new ProfessionRankRequirementDefinition(
                2,
                new[]
                {
                    new TagRequirementDefinition(
                        "weapon_training",
                        1,
                        "core_max",
                        "any",
                        "assigned_core"
                    ),
                },
                Array.Empty<
                    ProfessionRankGateDefinition
                >(),
                Array.Empty<
                    AttributeRequirementDefinition
                >(),
                Array.Empty<
                    ReputationRequirementDefinition
                >()
            );
        var professionDefinition =
            new ProfessionDefinition(
                professionId,
                "Weapon Training Profession",
                "Regression-only profession.",
                2,
                6,
                "full",
                true,
                "",
                null,
                new[] { rankRequirement },
                Array.Empty<
                    ProfessionGrantedSkillDefinition
                >(),
                Array.Empty<AttributeModifierDefinition>(),
                Array.Empty<
                    ProfessionActiveConditionDefinition
                >(),
                "auto",
                "count_when_hidden"
            );
        var professionDefinitions =
            new Dictionary<
                StringName,
                ProfessionDefinition
            >
            {
                [professionId] = professionDefinition,
            };
        progress.SetProfessionProgress(
            new UnitProfessionProgress
            {
                profession_id = professionId,
                rank = 1,
                is_active = true,
            }
        );
        var ruleService = new ProfessionRuleService();
        ruleService.Setup(
            progress,
            skillDefinitions,
            professionDefinitions
        );
        _test.False(
            ruleService.CanRankUpProfession(professionId),
            "profession rank-up must not count an illegal weapon-training trigger as its preview-assigned core skill."
        );

        progression.SetupDefinitions(
            progress,
            skillDefinitions,
            professionDefinitions
        );
        int rankBefore =
            progress.GetProfessionProgress(professionId)?.rank
            ?? 0;
        int historyBefore =
            progress.GetProfessionProgress(professionId)
                ?.promotion_history.Count
            ?? 0;
        int hpBefore =
            progress.unit_base_attributes
                .GetAttributeValue("hp_max");
        bool lockBefore =
            progress.GetSkillProgress(skillId)
                ?.is_level_trigger_locked
            ?? false;
        _test.False(
            progression.PromoteProfession(
                professionId,
                new PromotionSelectionData(
                    triggerSkillIds:
                        new object[] { skillId }
                )
            ),
            "direct progression promotion must reject an illegal weapon-training trigger."
        );
        _test.Eq(
            progress.GetProfessionProgress(professionId)?.rank
                ?? 0,
            rankBefore,
            "failed promotion must preserve profession rank."
        );
        _test.Eq(
            progress.GetProfessionProgress(professionId)
                ?.promotion_history.Count
            ?? 0,
            historyBefore,
            "failed promotion must preserve promotion records."
        );
        _test.Eq(
            progress.unit_base_attributes
                .GetAttributeValue("hp_max"),
            hpBefore,
            "failed promotion must preserve HP."
        );
        _test.Eq(
            progress.GetSkillProgress(skillId)
                ?.is_level_trigger_locked
            ?? false,
            lockBefore,
            "failed promotion must preserve trigger lock state."
        );
        _test.Eq(
            progress.PendingProfessionChoicesTyped.Count,
            0,
            "all promotion owners must leave pending choices empty for weapon training."
        );
    }

    private static UnitProgress BuildProgress(StringName skillId)
    {
        var progress = new UnitProgress
        {
            unit_id = "weapon_training_member",
            display_name = "Weapon Training Member",
        };
        progress.SetSkillProgress(
            new UnitSkillProgress
            {
                skill_id = skillId,
                is_learned = true,
                is_core = false,
                skill_level = 1,
            }
        );
        return progress;
    }
}
