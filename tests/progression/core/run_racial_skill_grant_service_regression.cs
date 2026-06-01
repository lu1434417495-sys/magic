using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;

public partial class run_racial_skill_grant_service_regression : SceneTree
{
    private readonly List<string> _failures = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestServiceNoLongerRequiresGodotRegistration();
        TestBackfillAndRevokeRaceGrantedSkill();

        if (_failures.Count == 0)
        {
            GD.Print("Racial skill grant service regression: PASS");
            Quit(0);
            return;
        }

        foreach (string failure in _failures)
            GD.PushError(failure);
        GD.Print($"Racial skill grant service regression: FAIL ({_failures.Count})");
        Quit(1);
    }

    private void TestServiceNoLongerRequiresGodotRegistration()
    {
        Type serviceType = typeof(RacialSkillGrantService);
        AssertFalse(
            typeof(GodotObject).IsAssignableFrom(serviceType),
            "RacialSkillGrantService 应是普通 C# static helper，不应继承 GodotObject/RefCounted。"
        );
        AssertFalse(
            serviceType.GetCustomAttributes(typeof(GlobalClassAttribute), inherit: false).Length
                > 0,
            "RacialSkillGrantService 不应继续注册为 Godot GlobalClass。"
        );
    }

    private void TestBackfillAndRevokeRaceGrantedSkill()
    {
        StringName skillId = "race_stone_skin";
        RacialGrantedSkill grant = new()
        {
            skill_id = skillId,
            minimum_skill_level = 2,
        };
        RaceDef race = new()
        {
            race_id = "stonefolk",
            racial_granted_skills = new Godot.Collections.Array<RacialGrantedSkill> { grant },
        };
        SkillDef skillDef = new()
        {
            skill_id = skillId,
            display_name = "Stone Skin",
            max_level = 3,
            learn_source = "race",
        };
        Godot.Collections.Dictionary contentBundle = new()
        {
            ["race_defs"] = new Godot.Collections.Dictionary { [race.race_id] = race },
        };
        Godot.Collections.Dictionary skillDefs = new() { [skillDef.skill_id] = skillDef };
        Godot.Collections.Dictionary professionDefs = new();
        PartyMemberState member = MakeMember("hero", race.race_id);

        AssertTrue(
            RacialSkillGrantService.backfill_member(
                member,
                contentBundle,
                skillDefs,
                professionDefs
            ),
            "身份技能补授应报告发生变化。"
        );
        UnitSkillProgress grantedProgress = member.progression.get_skill_progress(skillId);
        AssertTrue(grantedProgress != null, "身份技能补授应创建 UnitSkillProgress。");
        if (grantedProgress != null)
        {
            AssertTrue(grantedProgress.is_learned, "身份技能补授应标记技能已学会。");
            AssertEq(grantedProgress.skill_level, 2, "身份技能补授应写入 minimum_skill_level。");
            AssertEq(grantedProgress.granted_source_type, new StringName("race"), "来源类型应为 race。");
            AssertEq(grantedProgress.granted_source_id, race.race_id, "来源 id 应为 race id。");
        }

        AssertFalse(
            RacialSkillGrantService.backfill_member(
                member,
                contentBundle,
                skillDefs,
                professionDefs
            ),
            "重复补授已学会身份技能不应报告变化。"
        );

        race.racial_granted_skills.Clear();
        AssertTrue(
            RacialSkillGrantService.revoke_orphan_member(
                member,
                contentBundle,
                skillDefs,
                professionDefs
            ),
            "身份内容移除 grant 后应撤销孤儿身份技能。"
        );
        AssertTrue(
            member.progression.get_skill_progress(skillId) == null,
            "孤儿身份技能应从 UnitProgress 移除。"
        );
    }

    private static PartyMemberState MakeMember(StringName memberId, StringName raceId)
    {
        return new PartyMemberState
        {
            member_id = memberId,
            display_name = memberId.ToString(),
            race_id = raceId,
            subrace_id = "",
            progression = new UnitProgress
            {
                unit_id = memberId,
                display_name = memberId.ToString(),
            },
        };
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
