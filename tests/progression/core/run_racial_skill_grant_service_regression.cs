using System.Collections.Generic;
using Godot;

public partial class run_racial_skill_grant_service_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestBackfillAndRevokeRaceGrantedSkill();

        RequestTestExit(_test.Finish("Racial skill grant service regression"));
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
        SkillDefinition skillDefinition = BuildSkillDefinition(skillId, "Stone Skin", 3, "race");
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
        Dictionary<StringName, SkillDefinition> skillDefinitions =
            new() { [skillDefinition.SkillId] = skillDefinition };
        Dictionary<StringName, ProfessionDef> professionDefs = new();
        PartyMemberState member = MakeMember("hero", race.race_id);

        _test.True(
            RacialSkillGrantService.BackfillMember(
                member,
                identityCatalog,
                skillDefinitions,
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
                skillDefinitions,
                professionDefs
            ),
            "重复补授已学会身份技能不应报告变化。"
        );

        race.racial_granted_skills.Clear();
        _test.True(
            RacialSkillGrantService.RevokeOrphanMember(
                member,
                identityCatalog,
                skillDefinitions,
                professionDefs
            ),
            "身份内容移除 grant 后应撤销孤儿身份技能。"
        );
        _test.True(
            member.progression.GetSkillProgress(skillId) == null,
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

    private static SkillDefinition BuildSkillDefinition(
        StringName skillId,
        string displayName,
        int maxLevel,
        StringName learnSource
    )
    {
        return new SkillDefinition(
            skillId,
            displayName,
            skillId,
            "",
            "passive",
            maxLevel,
            0,
            "",
            0,
            0,
            System.Array.Empty<int>(),
            System.Array.Empty<StringName>(),
            learnSource,
            System.Array.Empty<StringName>(),
            "standard",
            System.Array.Empty<StringName>(),
            new Dictionary<StringName, int>(),
            new Dictionary<StringName, int>(),
            System.Array.Empty<StringName>(),
            System.Array.Empty<StringName>(),
            false,
            "",
            System.Array.Empty<StringName>(),
            "",
            new Dictionary<StringName, int>(),
            "",
            System.Array.Empty<AttributeModifierDefinition>(),
            "",
            new Dictionary<int, IReadOnlyDictionary<string, object>>(),
            null
        );
    }

}
