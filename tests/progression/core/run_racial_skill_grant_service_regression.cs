using System.Collections.Generic;
using Godot;

public partial class run_racial_skill_grant_service_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        try
        {
            TestBackfillAndRevokeRaceGrantedSkill();
        }
        finally
        {
            GodotSharpCleanup.CollectPendingFinalizers();
        }

        Quit(_test.Finish("Racial skill grant service regression"));
    }

    private void TestBackfillAndRevokeRaceGrantedSkill()
    {
        StringName skillId = "race_stone_skin";
        RacialGrantedSkill grant = null;
        RaceDef race = null;
        SkillDef skillDef = null;
        PartyMemberState member = null;
        bool grantDetached = false;
        try
        {
            grant = new()
            {
                skill_id = skillId,
                minimum_skill_level = 2,
            };
            race = new()
            {
                race_id = "stonefolk",
                racial_granted_skills = new Godot.Collections.Array<RacialGrantedSkill> { grant },
            };
            skillDef = new()
            {
                skill_id = skillId,
                display_name = "Stone Skin",
                max_level = 3,
                learn_source = "race",
            };
            ProgressionIdentityCatalogData identityCatalog = new(
                new Dictionary<StringName, RaceDef> { [race.race_id] = race },
                new Dictionary<StringName, SubraceDef>(),
                new Dictionary<StringName, AgeProfileDef>(),
                new Dictionary<StringName, BloodlineDef>(),
                new Dictionary<StringName, BloodlineStageDef>(),
                new Dictionary<StringName, AscensionDef>(),
                new Dictionary<StringName, AscensionStageDef>(),
                new Dictionary<StringName, StageAdvancementModifier>()
            );
            Dictionary<StringName, SkillDef> skillDefs = new() { [skillDef.skill_id] = skillDef };
            Dictionary<StringName, ProfessionDef> professionDefs = new();
            member = MakeMember("hero", race.race_id);

            _test.True(
                RacialSkillGrantService.BackfillMember(
                    member,
                    identityCatalog,
                    skillDefs,
                    professionDefs
                ),
                "身份技能补授应报告发生变化。"
            );
            UnitSkillProgress grantedProgress = member.progression.GetSkillProgress(skillId);
            _test.True(grantedProgress != null, "身份技能补授应创建 UnitSkillProgress。");
            if (grantedProgress != null)
            {
                _test.True(grantedProgress.is_learned, "身份技能补授应标记技能已学会。");
                _test.Eq(grantedProgress.skill_level, 2, "身份技能补授应写入 minimum_skill_level。");
                _test.Eq(
                    grantedProgress.granted_source_type,
                    UnitSkillProgress.ToStringName(UnitSkillGrantSourceType.Race),
                    "来源类型应为 race。"
                );
                _test.Eq(grantedProgress.granted_source_id, race.race_id, "来源 id 应为 race id。");
            }

            _test.False(
                RacialSkillGrantService.BackfillMember(
                    member,
                    identityCatalog,
                    skillDefs,
                    professionDefs
                ),
                "重复补授已学会身份技能不应报告变化。"
            );

            race.racial_granted_skills.Clear();
            grantDetached = true;
            _test.True(
                RacialSkillGrantService.RevokeOrphanMember(
                    member,
                    identityCatalog,
                    skillDefs,
                    professionDefs
                ),
                "身份内容移除 grant 后应撤销孤儿身份技能。"
            );
            _test.True(
                member.progression.GetSkillProgress(skillId) == null,
                "孤儿身份技能应从 UnitProgress 移除。"
            );
        }
        finally
        {
            if (grantDetached)
                GodotSharpCleanup.DisposeGodotObject(grant);
            BattleTestFixture.DisposeRace(race);
            BattleTestFixture.DisposeSkill(skillDef);
            member?.Dispose();
        }
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


}
