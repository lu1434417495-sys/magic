using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_bloodline_ascension_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestApplyServicesValidateBeforeMutation();
        TestCharacterManagementRejectsInvalidIdentityApplyWithoutMutation();
        TestCharacterManagementAppliesIdentityAndRefreshesGrants();
        TestStageAdvancementRefreshesEffectiveStage();
        TestIdentitySummaryIncludesIdentityProjection();

        RequestTestExit(_test.Finish("Bloodline ascension regression"));
    }

    private void TestApplyServicesValidateBeforeMutation()
    {
        ProgressionIdentityCatalogData catalog = MakeIdentityCatalog();
        PartyMemberState member = MakeMemberState("hero");

        BloodlineApplyService bloodlineService = new();
        bloodlineService.Setup(catalog);
        _test.True(
            bloodlineService.ApplyBloodline(member, "titan", "titan_awakened"),
            "合法 bloodline/stage 组合应写入成员身份。"
        );
        _test.Eq(member.bloodline_id, new StringName("titan"), "apply_bloodline 应写入 bloodline_id。");
        _test.Eq(
            member.bloodline_stage_id,
            new StringName("titan_awakened"),
            "apply_bloodline 应写入 bloodline_stage_id。"
        );
        _test.False(
            bloodlineService.ApplyBloodline(member, "titan", "dragon_awakened"),
            "BloodlineApplyService 应拒绝不属于该 bloodline 的 stage。"
        );
        _test.Eq(
            member.bloodline_stage_id,
            new StringName("titan_awakened"),
            "非法 bloodline apply 不应污染已存在状态。"
        );

        AscensionApplyService ascensionService = new();
        ascensionService.Setup(catalog);
        _test.True(
            ascensionService.ApplyAscension(member, "dragon_ascension", "dragon_awakened", 42),
            "符合 race/subrace/bloodline 条件时应能应用 ascension。"
        );
        _test.Eq(
            member.ascension_id,
            new StringName("dragon_ascension"),
            "apply_ascension 应写入 ascension_id。"
        );
        _test.Eq(
            member.ascension_stage_id,
            new StringName("dragon_awakened"),
            "apply_ascension 应写入 ascension_stage_id。"
        );
        _test.Eq(
            member.original_race_id_before_ascension,
            new StringName("human"),
            "首次 ascension 应保存原始 race。"
        );
        _test.Eq(member.ascension_started_at_world_step, 42, "apply_ascension 应记录开始 world step。");

        StringName beforeStage = member.ascension_stage_id;
        _test.False(
            ascensionService.ApplyAscension(member, "elf_ascension", "elf_awakened", 43),
            "AscensionApplyService 应拒绝不满足 allowed_race_ids 的升华。"
        );
        _test.Eq(member.ascension_stage_id, beforeStage, "非法 ascension apply 不应污染已存在状态。");

        member.race_id = "ascended_dragon";
        _test.True(ascensionService.RevokeAscension(member), "revoke_ascension 应能清除当前升华。");
        _test.Eq(member.race_id, new StringName("human"), "revoke_ascension 默认应恢复原始 race。");
        _test.Eq(member.ascension_id, new StringName(""), "revoke_ascension 应清空 ascension_id。");
        _test.Eq(
            member.ascension_started_at_world_step,
            -1,
            "revoke_ascension 应清空开始 world step。"
        );
        _test.Eq(
            member.original_race_id_before_ascension,
            new StringName(""),
            "revoke_ascension 应清空原始 race 备份。"
        );

        StageAdvancementApplyService stageService = new();
        stageService.Setup(catalog);
        _test.True(
            stageService.AddStageAdvancementModifier(member, "growth_boon"),
            "符合身份条件时应能添加阶段提升 modifier。"
        );
        _test.False(
            stageService.AddStageAdvancementModifier(member, "growth_boon"),
            "重复添加阶段提升 modifier 应被拒绝。"
        );
        AssertIdsEq(
            member.active_stage_advancement_modifier_ids,
            new[] { new StringName("growth_boon") },
            "阶段提升 modifier 应保持去重列表。"
        );
        _test.True(
            stageService.RemoveStageAdvancementModifier(member, "growth_boon"),
            "remove_stage_advancement_modifier 应能移除已存在 modifier。"
        );
        AssertIdsEq(
            member.active_stage_advancement_modifier_ids,
            Array.Empty<StringName>(),
            "移除 modifier 后列表应为空。"
        );
    }

    private void TestCharacterManagementRejectsInvalidIdentityApplyWithoutMutation()
    {
        ProgressionIdentityCatalogData catalog = MakeIdentityCatalog();
        PartyState partyState = MakePartyState();
        CharacterManagementModule manager = BuildManager(
            partyState,
            new Dictionary<StringName, SkillDefinition>(),
            catalog
        );
        PartyMemberState member = partyState.GetMemberState("hero");

        _test.False(
            manager.ApplyBloodline("hero", "titan", "dragon_awakened"),
            "CMM 应拒绝不属于该 bloodline 的 stage。"
        );
        _test.Eq(member.bloodline_id, new StringName(""), "CMM 非法 bloodline apply 不应写入 bloodline_id。");
        _test.Eq(
            member.bloodline_stage_id,
            new StringName(""),
            "CMM 非法 bloodline apply 不应写入 bloodline_stage_id。"
        );

        _test.False(
            manager.ApplyAscension("hero", "elf_ascension", "elf_awakened", 7),
            "CMM 应拒绝不满足 allowed_race_ids 的 ascension。"
        );
        _test.Eq(member.ascension_id, new StringName(""), "CMM 非法 ascension apply 不应写入 ascension_id。");
        _test.Eq(
            member.ascension_stage_id,
            new StringName(""),
            "CMM 非法 ascension apply 不应写入 ascension_stage_id。"
        );
        _test.Eq(
            member.ascension_started_at_world_step,
            -1,
            "CMM 非法 ascension apply 不应写入开始 world step。"
        );
    }

    private void TestCharacterManagementAppliesIdentityAndRefreshesGrants()
    {
        ProgressionIdentityCatalogData catalog = MakeIdentityCatalog();
        SkillDefinition bloodlineSkill = MakeSkill("bloodline_skill", "bloodline");
        SkillDefinition bloodlineStageSkill = MakeSkill("bloodline_stage_skill", "bloodline");
        SkillDefinition ascensionSkill = MakeSkill("ascension_skill", "ascension");
        SkillDefinition ascensionStageSkill = MakeSkill("ascension_stage_skill", "ascension");
        PartyState partyState = MakePartyState();
        CharacterManagementModule manager = BuildManager(
            partyState,
            BuildSkillIndex(
                bloodlineSkill,
                bloodlineStageSkill,
                ascensionSkill,
                ascensionStageSkill
            ),
            catalog
        );

        _test.True(
            manager.ApplyBloodline("hero", "titan", "titan_awakened"),
            "CharacterManagementModule.apply_bloodline 应委托服务并刷新成员。"
        );
        PartyMemberState member = partyState.GetMemberState("hero");
        AssertIdentityGrantedSkill(member, "bloodline_skill", "bloodline", "titan");
        AssertIdentityGrantedSkill(
            member,
            "bloodline_stage_skill",
            "bloodline",
            "titan_awakened"
        );

        _test.True(manager.RevokeBloodline("hero"), "revoke_bloodline 应清空 bloodline 并触发技能撤销。");
        _test.True(
            member.progression.GetSkillProgress("bloodline_skill") == null,
            "revoke_bloodline 后 bloodline 来源技能应被撤销。"
        );
        _test.True(
            member.progression.GetSkillProgress("bloodline_stage_skill") == null,
            "revoke_bloodline 后 bloodline stage 来源技能应被撤销。"
        );

        _test.True(
            manager.ApplyAscension("hero", "dragon_ascension", "dragon_awakened", 11),
            "CharacterManagementModule.apply_ascension 应委托服务并刷新成员。"
        );
        AssertIdentityGrantedSkill(member, "ascension_skill", "ascension", "dragon_ascension");
        AssertIdentityGrantedSkill(
            member,
            "ascension_stage_skill",
            "ascension",
            "dragon_awakened"
        );
        _test.Eq(
            member.effective_age_stage_id,
            new StringName("dragon_awakened"),
            "replaces_age_growth 的升华阶段应接管 effective_age_stage_id。"
        );
        _test.Eq(
            member.effective_age_stage_source_type,
            new StringName("ascension"),
            "升华接管年龄阶段时应记录来源类型。"
        );
        _test.Eq(
            member.effective_age_stage_source_id,
            new StringName("dragon_awakened"),
            "升华接管年龄阶段时应记录具体 ascension stage 来源 id。"
        );
        _test.Eq(member.body_size_category, new StringName("large"), "升华阶段体型 override 应刷新 body_size_category。");
        _test.Eq(member.body_size, 3, "升华阶段体型 override 应通过 BodySizeContentRules 刷新 body_size。");

        _test.True(manager.RevokeAscension("hero", true), "revoke_ascension 应清空 ascension 并触发技能撤销。");
        _test.Eq(member.body_size_category, new StringName("medium"), "撤销升华后体型应回到 race/subrace 解析结果。");
        _test.Eq(member.body_size, 2, "撤销升华后 body_size 应从 medium 重新派生。");
        _test.True(
            member.progression.GetSkillProgress("ascension_skill") == null,
            "revoke_ascension 后 ascension 来源技能应被撤销。"
        );
        _test.True(
            member.progression.GetSkillProgress("ascension_stage_skill") == null,
            "revoke_ascension 后 ascension stage 来源技能应被撤销。"
        );
    }

    private void TestStageAdvancementRefreshesEffectiveStage()
    {
        ProgressionIdentityCatalogData catalog = MakeIdentityCatalog();
        PartyState partyState = MakePartyState();
        CharacterManagementModule manager = BuildManager(
            partyState,
            new Dictionary<StringName, SkillDefinition>(),
            catalog
        );
        PartyMemberState member = partyState.GetMemberState("hero");
        _test.Eq(
            member.effective_age_stage_id,
            new StringName("adult"),
            "测试前置：成员有效阶段应从 adult 开始。"
        );

        _test.True(
            manager.AddStageAdvancementModifier("hero", "growth_boon"),
            "CMM 添加阶段提升 modifier 后应刷新 effective age stage。"
        );
        AssertIdsEq(
            member.active_stage_advancement_modifier_ids,
            new[] { new StringName("growth_boon") },
            "CMM 应通过 service 写入 active_stage_advancement_modifier_ids。"
        );
        _test.Eq(member.effective_age_stage_id, new StringName("old"), "growth_boon 应把 adult 推进到 old。");
        _test.Eq(
            member.effective_age_stage_source_type,
            new StringName("stage_advancement"),
            "阶段提升应记录 effective stage 来源类型。"
        );
        _test.Eq(
            member.effective_age_stage_source_id,
            new StringName("growth_boon"),
            "阶段提升应记录 effective stage 来源 id。"
        );
        _test.Eq(
            ReadString(
                manager.GetIdentitySummaryForMember("hero"),
                "effective_age_stage_label"
            ),
            "old",
            "阶段提升后身份摘要应从 typed age profile 显示刷新后的阶段标签。"
        );

        _test.True(
            manager.RemoveStageAdvancementModifier("hero", "growth_boon"),
            "CMM 移除阶段提升 modifier 后应刷新 effective age stage。"
        );
        _test.Eq(
            member.effective_age_stage_id,
            new StringName("adult"),
            "移除 modifier 后 effective stage 应回到 natural stage。"
        );
        _test.Eq(
            member.effective_age_stage_source_type,
            new StringName(""),
            "移除 modifier 后 effective stage 来源类型应清空。"
        );
    }

    private void TestIdentitySummaryIncludesIdentityProjection()
    {
        ProgressionIdentityCatalogData catalog = MakeIdentityCatalog();
        Dictionary<StringName, SkillDefinition> skillDefinitions = new();
        foreach (StringName skillId in new StringName[]
        {
            "bloodline_skill",
            "bloodline_stage_skill",
            "ascension_skill",
            "ascension_stage_skill",
        })
        {
            SkillDefinition skill = MakeSkill(skillId, "bloodline");
            skillDefinitions[skill.SkillId] = skill;
        }

        PartyState partyState = MakePartyState();
        CharacterManagementModule manager = BuildManager(partyState, skillDefinitions, catalog);
        _test.True(manager.ApplyBloodline("hero", "titan", "titan_awakened"), "身份摘要测试前置：应能应用 bloodline。");
        _test.True(
            manager.ApplyAscension("hero", "dragon_ascension", "dragon_awakened", 11),
            "身份摘要测试前置：应能应用 ascension。"
        );

        GDictionary summary = manager.GetIdentitySummaryForMember("hero");
        _test.Eq(ReadString(summary, "race_label"), "Human", "身份摘要应包含 race display_name。");
        _test.Eq(ReadString(summary, "subrace_label"), "High Human", "身份摘要应包含 subrace display_name。");
        _test.Eq(ReadString(summary, "bloodline_label"), "titan", "身份摘要应包含 bloodline display_name。");
        _test.Eq(
            ReadString(summary, "ascension_label"),
            "dragon_ascension",
            "身份摘要应包含 ascension display_name。"
        );
        _test.Eq(
            ReadString(summary, "effective_age_stage_label"),
            "dragon_awakened",
            "身份摘要应读取刷新后的 effective stage。"
        );
        _test.Eq(
            ReadStringName(summary, "body_size_category"),
            new StringName("large"),
            "身份摘要应包含当前升华后的 body_size_category。"
        );
        _test.Eq(ReadInt(summary, "body_size"), 3, "身份摘要应包含当前升华后的 body_size。");

        GDictionary damageResistances = ReadDictionary(summary, "damage_resistances");
        _test.Eq(
            ReadStringName(damageResistances, "fire"),
            new StringName("half"),
            "身份摘要应合并 race damage_resistances。"
        );
        _test.Eq(
            ReadStringName(damageResistances, "freeze"),
            new StringName("immune"),
            "身份摘要应合并 subrace damage_resistances。"
        );

        GArray saveTags = ReadArray(summary, "save_advantage_tags");
        _test.True(ContainsStringName(saveTags, "charm"), "身份摘要应包含 race save advantage tag。");
        _test.True(ContainsStringName(saveTags, "poison"), "身份摘要应包含 subrace save advantage tag。");

        GArray traitLines = ReadArray(summary, "trait_summary");
        _test.True(ContainsString(traitLines, "Human ambition"), "身份摘要应包含 race trait summary。");
        _test.True(ContainsString(traitLines, "Dragon stage"), "身份摘要应包含 ascension stage trait summary。");

        GArray racialSkillLines = ReadArray(summary, "racial_skill_lines");
        _test.True(
            ArrayContainsText(racialSkillLines, "bloodline_skill"),
            "身份摘要应包含 bloodline grant 技能。"
        );
        _test.True(
            ArrayContainsText(racialSkillLines, "ascension_stage_skill"),
            "身份摘要应包含 ascension stage grant 技能。"
        );
    }

    private static CharacterManagementModule BuildManager(
        PartyState partyState,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions,
        ProgressionIdentityCatalogData catalog
    )
    {
        CharacterManagementModule manager = new();
        manager.setup(
            partyState,
            skillDefinitions,
            new Dictionary<StringName, ProfessionDefinition>(),
            new Dictionary<StringName, AchievementDefinition>(),
            new Dictionary<StringName, ItemDefinition>(),
            new Dictionary<StringName, QuestDefinition>(),
            null,
            catalog
        );
        return manager;
    }

    private static PartyState MakePartyState()
    {
        PartyState partyState = new();
        PartyMemberState member = MakeMemberState("hero");
        partyState.SetMemberState(member);
        partyState.active_member_ids.Add("hero");
        partyState.leader_member_id = "hero";
        partyState.main_character_member_id = "hero";
        return partyState;
    }

    private static PartyMemberState MakeMemberState(StringName memberId)
    {
        PartyMemberState member = new()
        {
            member_id = memberId,
            display_name = "Hero",
            race_id = "human",
            subrace_id = "high_human",
            age_profile_id = "human_age",
            natural_age_stage_id = "adult",
            effective_age_stage_id = "adult",
        };
        member.progression.unit_id = memberId;
        member.progression.display_name = member.display_name;
        member.progression.character_level = 1;
        return member;
    }

    private static ProgressionIdentityCatalogData MakeIdentityCatalog()
    {
        RaceDefinition race = MakeRace();
        SubraceDefinition subrace = MakeSubrace();
        AgeProfileDefinition ageProfile = MakeAgeProfile();
        BloodlineDefinition bloodline = MakeBloodline(
            "titan", new[] { new StringName("titan_awakened") },
            new[] { MakeGrantedSkill("bloodline_skill") }
        );
        BloodlineStageDefinition bloodlineStage = MakeBloodlineStage(
            "titan_awakened", "titan", new[] { MakeGrantedSkill("bloodline_stage_skill") }
        );
        AscensionDefinition ascension = MakeAscension(
            "dragon_ascension", new[] { new StringName("dragon_awakened") },
            new[] { MakeGrantedSkill("ascension_skill") },
            new[] { new StringName("human") }, new[] { new StringName("high_human") },
            Array.Empty<StringName>(), replacesAgeGrowth: true
        );
        AscensionStageDefinition ascensionStage = MakeAscensionStage(
            "dragon_awakened", "dragon_ascension",
            new[] { MakeGrantedSkill("ascension_stage_skill") }
        );
        AscensionDefinition elfAscension = MakeAscension(
            "elf_ascension", new[] { new StringName("elf_awakened") },
            Array.Empty<RacialGrantedSkillDefinition>(), new[] { new StringName("elf") },
            Array.Empty<StringName>(), Array.Empty<StringName>(), replacesAgeGrowth: false
        );
        AscensionStageDefinition elfStage = MakeAscensionStage(
            "elf_awakened", "elf_ascension", Array.Empty<RacialGrantedSkillDefinition>()
        );
        StageAdvancementDefinition growthBoon = MakeStageAdvancement("growth_boon");

        return new ProgressionIdentityCatalogData(
            new Dictionary<StringName, RaceDefinition> { [race.RaceId] = race },
            new Dictionary<StringName, SubraceDefinition> { [subrace.SubraceId] = subrace },
            new Dictionary<StringName, AgeProfileDefinition> { [ageProfile.ProfileId] = ageProfile },
            new Dictionary<StringName, BloodlineDefinition> { [bloodline.BloodlineId] = bloodline },
            new Dictionary<StringName, BloodlineStageDefinition> { [bloodlineStage.StageId] = bloodlineStage },
            new Dictionary<StringName, AscensionDefinition>
            {
                [ascension.AscensionId] = ascension,
                [elfAscension.AscensionId] = elfAscension,
            },
            new Dictionary<StringName, AscensionStageDefinition>
            {
                [ascensionStage.StageId] = ascensionStage,
                [elfStage.StageId] = elfStage,
            },
            new Dictionary<StringName, StageAdvancementDefinition> { [growthBoon.ModifierId] = growthBoon }
        );
    }

    private static RaceDefinition MakeRace() =>
        new(
            "human", "Human", "Fixture race.", "human_age", "high_human",
            new[] { new StringName("high_human") }, "medium", 6,
            Array.Empty<AttributeModifierDefinition>(), Array.Empty<StringName>(),
            Array.Empty<RacialGrantedSkillDefinition>(), Array.Empty<StringName>(),
            Array.Empty<StringName>(), new[] { new StringName("charm") },
            Array.Empty<StringName>(), Array.Empty<StringName>(),
            new Dictionary<StringName, StringName> { ["fire"] = "half" },
            Array.Empty<StringName>(), new[] { "Human ambition" }
        );

    private static SubraceDefinition MakeSubrace() =>
        new(
            "high_human", "human", "High Human", "Fixture subrace.", "", 0,
            Array.Empty<AttributeModifierDefinition>(), Array.Empty<StringName>(),
            Array.Empty<RacialGrantedSkillDefinition>(), Array.Empty<StringName>(),
            Array.Empty<StringName>(), new[] { new StringName("poison") },
            Array.Empty<StringName>(), Array.Empty<StringName>(),
            new Dictionary<StringName, StringName> { ["freeze"] = "immune" },
            Array.Empty<StringName>(), new[] { "High human focus" }
        );

    private static AgeProfileDefinition MakeAgeProfile() =>
        new(
            "human_age", "human", 0, 12, 16, 18, 35, 55, 75, 100,
            new[]
            {
                MakeAgeStageRule("teen"), MakeAgeStageRule("adult"),
                MakeAgeStageRule("middle_age"), MakeAgeStageRule("old"),
            },
            new[] { new StringName("adult") },
            new Dictionary<StringName, int> { ["adult"] = 18 }
        );

    private static AgeStageRuleDefinition MakeAgeStageRule(StringName stageId) =>
        new(
            stageId, stageId.ToString(), "Fixture age stage.",
            Array.Empty<AttributeModifierDefinition>(), Array.Empty<StringName>(),
            new[] { $"Age stage {stageId}" }, true, true
        );

    private static BloodlineDefinition MakeBloodline(
        StringName bloodlineId,
        IEnumerable<StringName> stageIds,
        IEnumerable<RacialGrantedSkillDefinition> grants
    ) =>
        new(
            bloodlineId, bloodlineId.ToString(), "Fixture bloodline.",
            new List<StringName>(stageIds), Array.Empty<StringName>(),
            new List<RacialGrantedSkillDefinition>(grants),
            Array.Empty<AttributeModifierDefinition>(), new[] { $"Bloodline {bloodlineId}" }
        );

    private static BloodlineStageDefinition MakeBloodlineStage(
        StringName stageId,
        StringName bloodlineId,
        IEnumerable<RacialGrantedSkillDefinition> grants
    ) =>
        new(
            stageId, bloodlineId, stageId.ToString(), "Fixture bloodline stage.",
            Array.Empty<AttributeModifierDefinition>(), Array.Empty<StringName>(),
            new List<RacialGrantedSkillDefinition>(grants),
            new[] { $"Bloodline stage {stageId}" }
        );

    private static AscensionDefinition MakeAscension(
        StringName ascensionId,
        IEnumerable<StringName> stageIds,
        IEnumerable<RacialGrantedSkillDefinition> grants,
        IEnumerable<StringName> allowedRaceIds,
        IEnumerable<StringName> allowedSubraceIds,
        IEnumerable<StringName> allowedBloodlineIds,
        bool replacesAgeGrowth
    ) =>
        new(
            ascensionId, ascensionId.ToString(), "Fixture ascension.",
            new List<StringName>(stageIds), Array.Empty<StringName>(),
            new List<RacialGrantedSkillDefinition>(grants),
            new List<StringName>(allowedRaceIds), new List<StringName>(allowedSubraceIds),
            new List<StringName>(allowedBloodlineIds), new[] { $"Ascension {ascensionId}" },
            replacesAgeGrowth, false
        );

    private static AscensionStageDefinition MakeAscensionStage(
        StringName stageId,
        StringName ascensionId,
        IEnumerable<RacialGrantedSkillDefinition> grants
    ) =>
        new(
            stageId, ascensionId, stageId.ToString(), "Fixture ascension stage.",
            Array.Empty<AttributeModifierDefinition>(), Array.Empty<StringName>(),
            new List<RacialGrantedSkillDefinition>(grants), "large", new[] { "Dragon stage" }
        );

    private static StageAdvancementDefinition MakeStageAdvancement(StringName modifierId) =>
        new(
            modifierId, modifierId.ToString(), "full", 2, "old",
            new[] { new StringName("human") }, Array.Empty<StringName>(),
            Array.Empty<StringName>(), Array.Empty<StringName>(), true, true, true
        );

    private static Dictionary<StringName, SkillDefinition> BuildSkillIndex(
        params SkillDefinition[] skillDefinitions
    )
    {
        Dictionary<StringName, SkillDefinition> result = new();
        foreach (SkillDefinition skillDefinition in skillDefinitions ?? System.Array.Empty<SkillDefinition>())
            if (skillDefinition != null && skillDefinition.SkillId != "")
                result[skillDefinition.SkillId] = skillDefinition;
        return result;
    }

    private static SkillDefinition MakeSkill(StringName skillId, StringName learnSource) =>
        TestSkillDefinitionProjection.BuildSkill(
            skillId,
            displayName: skillId.ToString(),
            skillType: "passive",
            learnSource: learnSource,
            maxLevel: 3,
            masteryCurve: new[] { 10, 20, 30 }
        );

    private static RacialGrantedSkillDefinition MakeGrantedSkill(StringName skillId) =>
        new(skillId, 1, "per_battle", 1);

    private void AssertIdentityGrantedSkill(
        PartyMemberState member,
        StringName skillId,
        StringName expectedSourceType,
        StringName expectedSourceId
    )
    {
        UnitSkillProgress skillProgress = member?.progression?.GetSkillProgress(skillId);
        _test.True(
            skillProgress != null && skillProgress.is_learned,
            $"{skillId} 应已被身份授予。"
        );
        if (skillProgress == null)
            return;
        _test.Eq(
            skillProgress.granted_source_type,
            expectedSourceType,
            $"{skillId} 身份技能来源类型应匹配。"
        );
        _test.Eq(
            skillProgress.granted_source_id,
            expectedSourceId,
            $"{skillId} 身份技能来源 id 应匹配。"
        );
    }

    private void AssertIdsEq(
        IEnumerable<StringName> actual,
        IReadOnlyList<StringName> expected,
        string message
    )
    {
        List<StringName> actualList = new();
        foreach (StringName value in actual)
            actualList.Add(value);

        if (actualList.Count != expected.Count)
        {
            _test.Fail(
                $"{message} | actual={FormatIds(actualList)} expected={FormatIds(expected)}"
            );
            return;
        }

        for (int index = 0; index < actualList.Count; index++)
        {
            if (actualList[index] != expected[index])
            {
                _test.Fail(
                    $"{message} | actual={FormatIds(actualList)} expected={FormatIds(expected)}"
                );
                return;
            }
        }
    }

    private static string FormatIds(IEnumerable<StringName> ids)
    {
        List<string> values = new();
        foreach (StringName id in ids)
            values.Add(id.ToString());
        return $"[{string.Join(", ", values)}]";
    }

    private static string ReadString(GDictionary data, string key)
    {
        if (data == null || !data.ContainsKey(key))
            return "";
        Variant value = data[key];
        return value.VariantType switch
        {
            Variant.Type.String => value.AsString(),
            Variant.Type.StringName => value.AsStringName().ToString(),
            _ => "",
        };
    }

    private static StringName ReadStringName(GDictionary data, string key)
    {
        if (data == null || !data.ContainsKey(key))
            return "";
        Variant value = data[key];
        return value.VariantType switch
        {
            Variant.Type.StringName => value.AsStringName(),
            Variant.Type.String => new StringName(value.AsString()),
            _ => "",
        };
    }

    private static int ReadInt(GDictionary data, string key)
    {
        if (data == null || !data.ContainsKey(key))
            return 0;
        Variant value = data[key];
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : 0;
    }

    private static GDictionary ReadDictionary(GDictionary data, string key)
    {
        if (data == null || !data.ContainsKey(key))
            return new GDictionary();
        Variant value = data[key];
        return value.VariantType == Variant.Type.Dictionary
            ? value.AsGodotDictionary()
            : new GDictionary();
    }

    private static GArray ReadArray(GDictionary data, string key)
    {
        if (data == null || !data.ContainsKey(key))
            return new GArray();
        Variant value = data[key];
        return value.VariantType == Variant.Type.Array ? value.AsGodotArray() : new GArray();
    }

    private static bool ContainsStringName(GArray values, StringName expected)
    {
        foreach (Variant value in values)
        {
            if (value.VariantType == Variant.Type.StringName && value.AsStringName() == expected)
                return true;
            if (value.VariantType == Variant.Type.String && new StringName(value.AsString()) == expected)
                return true;
        }
        return false;
    }

    private static bool ContainsString(GArray values, string expected)
    {
        foreach (Variant value in values)
        {
            if (value.AsString() == expected)
                return true;
        }
        return false;
    }

    private static bool ArrayContainsText(GArray values, string needle)
    {
        foreach (Variant value in values)
        {
            if (value.AsString().Contains(needle, StringComparison.Ordinal))
                return true;
        }
        return false;
    }
}
