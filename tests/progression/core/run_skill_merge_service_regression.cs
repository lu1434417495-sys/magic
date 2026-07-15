using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class run_skill_merge_service_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestMergeClearsLevelTriggerStateWhenRemovingSources();
        TestCompositeUpgradeRetainsSourcesAndMovesCore();
        TestCompositeUpgradeWithoutProfessionAssignmentStillUnlocksResult();

        RequestTestExit(_test.Finish("Skill merge service regression"));
    }

    private void TestMergeClearsLevelTriggerStateWhenRemovingSources()
    {
        UnitProgress progress = new()
        {
            unit_id = "merge_trigger_clear_hero",
            display_name = "Merge Trigger Clear Hero",
        };
        StringName activeSkillId = "test_merge_active_source";
        StringName lockedSkillId = "test_merge_locked_source";
        foreach (StringName skillId in new[] { activeSkillId, lockedSkillId })
        {
            progress.SetSkillProgress(
                new UnitSkillProgress
                {
                    skill_id = skillId,
                    is_learned = true,
                    is_core = true,
                }
            );
        }
        UnitSkillProgress activeProgress = progress.GetSkillProgress(activeSkillId);
        activeProgress.is_level_trigger_active = true;
        progress.active_level_trigger_core_skill_id = activeSkillId;
        progress.SetSkillProgress(activeProgress);
        UnitSkillProgress lockedProgress = progress.GetSkillProgress(lockedSkillId);
        lockedProgress.is_level_trigger_locked = true;
        progress.AddLockedLevelTriggerSkillId(lockedSkillId);
        progress.SetSkillProgress(lockedProgress);

        SkillMergeService service = new();
        service.Setup(progress, new Dictionary<StringName, SkillDefinition>(), null);
        _test.True(
            service.MergeSkills(
                new[] { activeSkillId, lockedSkillId },
                "test_merge_result",
                false,
                ""
            ),
            "Removing source skills through merge should succeed."
        );

        _test.Eq(
            progress.active_level_trigger_core_skill_id,
            new StringName(""),
            "Merge should clear top-level active trigger id."
        );
        _test.Eq(
            progress.locked_level_trigger_skill_ids.Count,
            0,
            "Merge should clear top-level locked trigger ids."
        );
        _test.True(
            progress.GetSkillProgress(activeSkillId) == null,
            "Active source skill should be removed."
        );
        _test.True(
            progress.GetSkillProgress(lockedSkillId) == null,
            "Locked source skill should be removed."
        );
        _test.True(
            UnitProgress.FromDictionary(progress.ToDictionary()) != null,
            "Merged progress should still pass strict save validation."
        );
    }

    private void TestCompositeUpgradeRetainsSourcesAndMovesCore()
    {
        UnitProgress progress = new()
        {
            unit_id = "hero",
            display_name = "Hero",
        };
        UnitProfessionProgress warriorProgress = new()
        {
            profession_id = "warrior",
            rank = 1,
        };
        progress.SetProfessionProgress(warriorProgress);

        StringName firstSourceId = "warrior_combo_strike";
        StringName secondSourceId = "warrior_aura_slash";
        StringName resultSkillId = "saint_blade_combo";
        foreach (StringName sourceSkillId in new[] { firstSourceId, secondSourceId })
        {
            progress.SetSkillProgress(
                new UnitSkillProgress
                {
                    skill_id = sourceSkillId,
                    is_learned = true,
                    skill_level = 5,
                    is_core = true,
                    assigned_profession_id = warriorProgress.profession_id,
                }
            );
            warriorProgress.AddCoreSkill(sourceSkillId);
        }

        UnitSkillProgress firstSourceProgress = progress.GetSkillProgress(firstSourceId);
        firstSourceProgress.is_level_trigger_active = true;
        progress.active_level_trigger_core_skill_id = firstSourceId;
        progress.SetSkillProgress(firstSourceProgress);
        UnitSkillProgress secondSourceProgress = progress.GetSkillProgress(secondSourceId);
        secondSourceProgress.is_level_trigger_locked = true;
        progress.AddLockedLevelTriggerSkillId(secondSourceId);
        progress.SetSkillProgress(secondSourceProgress);

        SkillMergeService service = new();
        service.Setup(progress, BuildSkillDefinitions(resultSkillId), null);

        _test.True(
            service.ApplyCompositeUpgradeResult(
                resultSkillId,
                new[] { firstSourceId, secondSourceId },
                true,
                CoreSkillTransitionMode.ReplaceSourcesWithResult,
                ""
            ),
            "Composite upgrade should replace source core slots with the result skill."
        );

        UnitSkillProgress resultProgress = progress.GetSkillProgress(resultSkillId);
        _test.True(
            resultProgress != null && resultProgress.is_learned,
            "Composite upgrade result should be learned."
        );
        _test.True(resultProgress != null && resultProgress.is_core, "Result should be core.");
        _test.Eq(
            resultProgress?.assigned_profession_id ?? "",
            warriorProgress.profession_id,
            "Result should inherit the original profession core slot."
        );
        _test.True(
            progress.GetSkillProgress(firstSourceId).is_learned
                && !progress.GetSkillProgress(firstSourceId).is_core,
            "First source should remain learned without occupying a core slot."
        );
        _test.True(
            progress.GetSkillProgress(secondSourceId).is_learned
                && !progress.GetSkillProgress(secondSourceId).is_core,
            "Second source should remain learned without occupying a core slot."
        );
        _test.Eq(
            progress.active_level_trigger_core_skill_id,
            new StringName(""),
            "Composite upgrade should clear active source trigger id."
        );
        _test.Eq(
            progress.locked_level_trigger_skill_ids.Count,
            0,
            "Composite upgrade should remove locked source trigger ids."
        );
        _test.True(
            !progress.GetSkillProgress(firstSourceId).is_level_trigger_active
                && !progress.GetSkillProgress(secondSourceId).is_level_trigger_locked,
            "Sources should not keep trigger flags after leaving core slots."
        );
        _test.True(
            warriorProgress.core_skill_ids.Contains(resultSkillId)
                && !warriorProgress.core_skill_ids.Contains(firstSourceId)
                && !warriorProgress.core_skill_ids.Contains(secondSourceId),
            "Profession core list should move from sources to result."
        );
        _test.True(
            UnitProgress.FromDictionary(progress.ToDictionary()) != null,
            "Composite upgrade progress should still pass strict save validation."
        );
    }

    private void TestCompositeUpgradeWithoutProfessionAssignmentStillUnlocksResult()
    {
        UnitProgress progress = new()
        {
            unit_id = "no_profession_core_hero",
            display_name = "No Profession Core Hero",
        };
        StringName firstSourceId = "warrior_combo_strike";
        StringName secondSourceId = "warrior_aura_slash";
        StringName resultSkillId = "saint_blade_combo";

        foreach (StringName sourceSkillId in new[] { firstSourceId, secondSourceId })
        {
            progress.SetSkillProgress(
                new UnitSkillProgress
                {
                    skill_id = sourceSkillId,
                    is_learned = true,
                    skill_level = 5,
                    is_core = true,
                    assigned_profession_id = null,
                }
            );
        }

        UnitSkillProgress firstSourceProgress = progress.GetSkillProgress(firstSourceId);
        firstSourceProgress.is_level_trigger_locked = true;
        progress.AddLockedLevelTriggerSkillId(firstSourceId);
        progress.SetSkillProgress(firstSourceProgress);

        SkillMergeService service = new();
        service.Setup(progress, BuildSkillDefinitions(resultSkillId), null);

        bool threw = false;
        bool success = false;
        try
        {
            success = service.ApplyCompositeUpgradeResult(
                resultSkillId,
                new[] { firstSourceId, secondSourceId },
                true,
                CoreSkillTransitionMode.ReplaceSourcesWithResult,
                default
            );
        }
        catch (Exception ex)
        {
            threw = true;
            _test.Fail($"Composite upgrade should not throw on null-like profession assignment. | ex={ex.GetType().Name}: {ex.Message}");
        }

        _test.False(
            threw,
            "Composite upgrade with null-like assigned profession ids should stay inside typed StringName handling."
        );
        _test.True(
            success,
            "Composite upgrade should still unlock the result when source core slots lack profession assignment."
        );
        _test.True(
            progress.GetSkillProgress(resultSkillId)?.is_learned == true,
            "Composite upgrade result should still be written to progression."
        );
        _test.False(
            progress.GetSkillProgress(resultSkillId)?.is_core ?? true,
            "Missing profession assignment should downgrade the composite result to non-core instead of failing."
        );
        _test.Eq(
            progress.locked_level_trigger_skill_ids.Count,
            0,
            "Composite upgrade without profession assignment should still clear source trigger locks."
        );
    }

    private static IReadOnlyDictionary<StringName, SkillDefinition> BuildSkillDefinitions(
        StringName resultSkillId
    ) =>
        new Dictionary<StringName, SkillDefinition>
        {
            [resultSkillId] = TestSkillDefinitionProjection.BuildSkill(
                resultSkillId,
                maxLevel: 5,
                nonCoreMaxLevel: 5
            ),
        };


}
