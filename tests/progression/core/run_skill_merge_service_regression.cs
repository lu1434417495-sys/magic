using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_skill_merge_service_regression : SceneTree
{
    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestSkillMergeServiceNoLongerRequiresGodotRegistration();
        TestMergeClearsLevelTriggerStateWhenRemovingSources();
        TestCompositeUpgradeRetainsSourcesAndMovesCore();

        if (_failures.Count == 0)
        {
            GD.Print("Skill merge service regression: PASS");
            Quit(0);
            return;
        }

        foreach (string failure in _failures)
            GD.PushError(failure);
        GD.Print($"Skill merge service regression: FAIL ({_failures.Count})");
        Quit(1);
    }

    private void TestSkillMergeServiceNoLongerRequiresGodotRegistration()
    {
        Type serviceType = typeof(SkillMergeService);
        AssertFalse(
            typeof(GodotObject).IsAssignableFrom(serviceType),
            "SkillMergeService should be a plain C# service, not a GodotObject/RefCounted."
        );
        AssertFalse(
            serviceType.GetCustomAttributes(typeof(GlobalClassAttribute), inherit: false).Length
                > 0,
            "SkillMergeService should not remain registered as a Godot GlobalClass."
        );
        AssertEq(
            serviceType.GetField("_skillDefs", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.FieldType,
            typeof(Dictionary<StringName, SkillDef>),
            "SkillMergeService should cache skill defs in a typed C# dictionary."
        );
        AssertEq(
            serviceType
                .GetMethod("NormalizeSourceSkillIds", BindingFlags.NonPublic | BindingFlags.Static)
                ?.ReturnType,
            typeof(List<StringName>),
            "SkillMergeService should normalize source ids into a C# List."
        );
        AssertEq(
            serviceType
                .GetMethod(nameof(SkillMergeService.merge_skills))
                ?.GetParameters()[0]
                .ParameterType,
            typeof(IEnumerable<StringName>),
            "SkillMergeService merge input should consume typed source id sequences."
        );
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
            progress.set_skill_progress(
                new UnitSkillProgress
                {
                    skill_id = skillId,
                    is_learned = true,
                    is_core = true,
                }
            );
        }
        UnitSkillProgress activeProgress = progress.get_skill_progress(activeSkillId);
        activeProgress.is_level_trigger_active = true;
        progress.active_level_trigger_core_skill_id = activeSkillId;
        progress.set_skill_progress(activeProgress);
        UnitSkillProgress lockedProgress = progress.get_skill_progress(lockedSkillId);
        lockedProgress.is_level_trigger_locked = true;
        progress.locked_level_trigger_skill_ids.Add(lockedSkillId);
        progress.set_skill_progress(lockedProgress);

        SkillMergeService service = new();
        service.setup(progress, new GDictionary(), null);
        AssertTrue(
            service.merge_skills(
                new[] { activeSkillId, lockedSkillId },
                "test_merge_result",
                false,
                ""
            ),
            "Removing source skills through merge should succeed."
        );

        AssertEq(
            progress.active_level_trigger_core_skill_id,
            new StringName(""),
            "Merge should clear top-level active trigger id."
        );
        AssertEq(
            progress.locked_level_trigger_skill_ids.Count,
            0,
            "Merge should clear top-level locked trigger ids."
        );
        AssertTrue(
            progress.get_skill_progress(activeSkillId) == null,
            "Active source skill should be removed."
        );
        AssertTrue(
            progress.get_skill_progress(lockedSkillId) == null,
            "Locked source skill should be removed."
        );
        AssertTrue(
            UnitProgress.from_dict(progress.to_dict()) != null,
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
        progress.set_profession_progress(warriorProgress);

        StringName firstSourceId = "warrior_combo_strike";
        StringName secondSourceId = "warrior_aura_slash";
        StringName resultSkillId = "saint_blade_combo";
        foreach (StringName sourceSkillId in new[] { firstSourceId, secondSourceId })
        {
            progress.set_skill_progress(
                new UnitSkillProgress
                {
                    skill_id = sourceSkillId,
                    is_learned = true,
                    skill_level = 5,
                    is_core = true,
                    assigned_profession_id = warriorProgress.profession_id,
                }
            );
            warriorProgress.add_core_skill(sourceSkillId);
        }

        UnitSkillProgress firstSourceProgress = progress.get_skill_progress(firstSourceId);
        firstSourceProgress.is_level_trigger_active = true;
        progress.active_level_trigger_core_skill_id = firstSourceId;
        progress.set_skill_progress(firstSourceProgress);
        UnitSkillProgress secondSourceProgress = progress.get_skill_progress(secondSourceId);
        secondSourceProgress.is_level_trigger_locked = true;
        progress.locked_level_trigger_skill_ids.Add(secondSourceId);
        progress.set_skill_progress(secondSourceProgress);

        SkillMergeService service = new();
        service.setup(progress, BuildSkillDefs(resultSkillId), null);

        AssertTrue(
            service.apply_composite_upgrade_result(
                resultSkillId,
                new[] { firstSourceId, secondSourceId },
                true,
                "replace_sources_with_result",
                ""
            ),
            "Composite upgrade should replace source core slots with the result skill."
        );

        UnitSkillProgress resultProgress = progress.get_skill_progress(resultSkillId);
        AssertTrue(
            resultProgress != null && resultProgress.is_learned,
            "Composite upgrade result should be learned."
        );
        AssertTrue(resultProgress != null && resultProgress.is_core, "Result should be core.");
        AssertEq(
            resultProgress?.assigned_profession_id ?? "",
            warriorProgress.profession_id,
            "Result should inherit the original profession core slot."
        );
        AssertTrue(
            progress.get_skill_progress(firstSourceId).is_learned
                && !progress.get_skill_progress(firstSourceId).is_core,
            "First source should remain learned without occupying a core slot."
        );
        AssertTrue(
            progress.get_skill_progress(secondSourceId).is_learned
                && !progress.get_skill_progress(secondSourceId).is_core,
            "Second source should remain learned without occupying a core slot."
        );
        AssertEq(
            progress.active_level_trigger_core_skill_id,
            new StringName(""),
            "Composite upgrade should clear active source trigger id."
        );
        AssertEq(
            progress.locked_level_trigger_skill_ids.Count,
            0,
            "Composite upgrade should remove locked source trigger ids."
        );
        AssertTrue(
            !progress.get_skill_progress(firstSourceId).is_level_trigger_active
                && !progress.get_skill_progress(secondSourceId).is_level_trigger_locked,
            "Sources should not keep trigger flags after leaving core slots."
        );
        AssertTrue(
            warriorProgress.core_skill_ids.Contains(resultSkillId)
                && !warriorProgress.core_skill_ids.Contains(firstSourceId)
                && !warriorProgress.core_skill_ids.Contains(secondSourceId),
            "Profession core list should move from sources to result."
        );
        AssertTrue(
            UnitProgress.from_dict(progress.to_dict()) != null,
            "Composite upgrade progress should still pass strict save validation."
        );
    }

    private static GDictionary BuildSkillDefs(StringName resultSkillId) =>
        new()
        {
            [resultSkillId] = new SkillDef
            {
                skill_id = resultSkillId,
                max_level = 5,
                non_core_max_level = 5,
            },
        };

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
