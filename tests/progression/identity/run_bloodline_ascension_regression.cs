using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_bloodline_ascension_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestApplyServicesNoLongerRequireGodotRegistration();
        TestApplyServicesValidateBeforeMutation();
        TestCharacterManagementRejectsInvalidIdentityApplyWithoutMutation();
        TestCharacterManagementAppliesIdentityAndRefreshesGrants();
        TestStageAdvancementRefreshesEffectiveStage();
        TestIdentitySummaryIncludesIdentityProjection();

        Quit(_test.Finish("Bloodline ascension regression"));
    }

    private void TestApplyServicesNoLongerRequireGodotRegistration()
    {
        AssertPlainService(typeof(BloodlineApplyService), nameof(BloodlineApplyService));
        AssertPlainService(typeof(AscensionApplyService), nameof(AscensionApplyService));
        AssertPlainService(
            typeof(StageAdvancementApplyService),
            nameof(StageAdvancementApplyService)
        );
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
        GDictionary bundle = MakeIdentityBundle();
        PartyState partyState = MakePartyState();
        CharacterManagementModule manager = BuildManager(
            partyState,
            new Dictionary<StringName, SkillDefinition>(),
            bundle
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
        GDictionary bundle = MakeIdentityBundle();
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
            bundle
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
        GDictionary bundle = MakeIdentityBundle();
        PartyState partyState = MakePartyState();
        CharacterManagementModule manager = BuildManager(
            partyState,
            new Dictionary<StringName, SkillDefinition>(),
            bundle
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
        GDictionary bundle = MakeIdentityBundle();
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
        CharacterManagementModule manager = BuildManager(partyState, skillDefinitions, bundle);
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
        GDictionary bundle
    )
    {
        CharacterManagementModule manager = new();
        manager.setup(
            partyState,
            skillDefinitions,
            new Dictionary<StringName, ProfessionDef>(),
            new Dictionary<StringName, AchievementDef>(),
            new Dictionary<StringName, ItemDef>(),
            new Dictionary<StringName, QuestDef>(),
            null,
            MakeIdentityCatalog(bundle)
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

    private static GDictionary MakeIdentityBundle()
    {
        RacialGrantedSkill bloodlineSkillGrant = MakeGrantedSkill("bloodline_skill");
        RacialGrantedSkill bloodlineStageSkillGrant = MakeGrantedSkill("bloodline_stage_skill");
        RacialGrantedSkill ascensionSkillGrant = MakeGrantedSkill("ascension_skill");
        RacialGrantedSkill ascensionStageSkillGrant = MakeGrantedSkill("ascension_stage_skill");
        RaceDef race = MakeRace();
        SubraceDef subrace = MakeSubrace();
        AgeProfileDef ageProfile = MakeAgeProfile();
        BloodlineDef bloodline = MakeBloodline(
            "titan",
            new[] { new StringName("titan_awakened") },
            new[] { bloodlineSkillGrant }
        );
        BloodlineStageDef bloodlineStage = MakeBloodlineStage(
            "titan_awakened",
            "titan",
            new[] { bloodlineStageSkillGrant }
        );
        AscensionDef ascension = MakeAscension(
            "dragon_ascension",
            new[] { new StringName("dragon_awakened") },
            new[] { ascensionSkillGrant },
            new[] { new StringName("human") },
            new[] { new StringName("high_human") },
            Array.Empty<StringName>()
        );
        ascension.replaces_age_growth = true;
        AscensionStageDef ascensionStage = MakeAscensionStage(
            "dragon_awakened",
            "dragon_ascension",
            new[] { ascensionStageSkillGrant }
        );
        AscensionDef elfAscension = MakeAscension(
            "elf_ascension",
            new[] { new StringName("elf_awakened") },
            Array.Empty<RacialGrantedSkill>(),
            new[] { new StringName("elf") },
            Array.Empty<StringName>(),
            Array.Empty<StringName>()
        );
        AscensionStageDef elfStage = MakeAscensionStage(
            "elf_awakened",
            "elf_ascension",
            Array.Empty<RacialGrantedSkill>()
        );
        StageAdvancementModifier growthBoon = MakeStageAdvancement("growth_boon");

        return new GDictionary
        {
            ["race_defs"] = new GDictionary { [race.race_id] = race },
            ["subrace_defs"] = new GDictionary { [subrace.subrace_id] = subrace },
            ["age_profile_defs"] = new GDictionary { [ageProfile.profile_id] = ageProfile },
            ["bloodline_defs"] = new GDictionary { [bloodline.bloodline_id] = bloodline },
            ["bloodline_stage_defs"] = new GDictionary
            {
                [bloodlineStage.stage_id] = bloodlineStage,
            },
            ["ascension_defs"] = new GDictionary
            {
                [ascension.ascension_id] = ascension,
                [elfAscension.ascension_id] = elfAscension,
            },
            ["ascension_stage_defs"] = new GDictionary
            {
                [ascensionStage.stage_id] = ascensionStage,
                [elfStage.stage_id] = elfStage,
            },
            ["stage_advancement_defs"] = new GDictionary
            {
                [growthBoon.modifier_id] = growthBoon,
            },
        };
    }

    private static ProgressionIdentityCatalogData MakeIdentityCatalog()
    {
        return MakeIdentityCatalog(MakeIdentityBundle());
    }

    private static ProgressionIdentityCatalogData MakeIdentityCatalog(GDictionary bundle)
    {
        return new ProgressionIdentityCatalogData(
            ReadTypedMap<RaceDef>(bundle, "race_defs"),
            ReadTypedMap<SubraceDef>(bundle, "subrace_defs"),
            ReadTypedMap<AgeProfileDef>(bundle, "age_profile_defs"),
            ReadTypedMap<BloodlineDef>(bundle, "bloodline_defs"),
            ReadTypedMap<BloodlineStageDef>(bundle, "bloodline_stage_defs"),
            ReadTypedMap<AscensionDef>(bundle, "ascension_defs"),
            ReadTypedMap<AscensionStageDef>(bundle, "ascension_stage_defs"),
            ReadTypedMap<StageAdvancementModifier>(bundle, "stage_advancement_defs")
        );
    }

    private static Dictionary<StringName, T> ReadTypedMap<T>(GDictionary bundle, string key)
        where T : class
    {
        var result = new Dictionary<StringName, T>();
        foreach (Variant rawKey in ReadDictionary(bundle, key).Keys)
        {
            StringName id = ReadStringNameKey(rawKey);
            if (id == "")
                continue;
            Variant rawValue = ReadDictionary(bundle, key)[rawKey];
            if (rawValue.VariantType == Variant.Type.Object && rawValue.AsGodotObject() is T typed)
                result[id] = typed;
        }
        return result;
    }

    private static StringName ReadStringNameKey(Variant value)
    {
        return value.VariantType switch
        {
            Variant.Type.StringName => value.AsStringName(),
            Variant.Type.String => new StringName(value.AsString()),
            _ => "",
        };
    }

    private static RaceDef MakeRace()
    {
        RaceDef race = new()
        {
            race_id = "human",
            display_name = "Human",
            description = "Fixture race.",
            age_profile_id = "human_age",
            default_subrace_id = "high_human",
            body_size_category = "medium",
            base_speed = 6,
            damage_resistances = new GDictionary { [new StringName("fire")] = new StringName("half") },
        };
        race.subrace_ids.Add("high_human");
        race.save_advantage_tags.Add("charm");
        race.racial_trait_summary.Add("Human ambition");
        return race;
    }

    private static SubraceDef MakeSubrace()
    {
        SubraceDef subrace = new()
        {
            subrace_id = "high_human",
            parent_race_id = "human",
            display_name = "High Human",
            description = "Fixture subrace.",
            damage_resistances = new GDictionary
            {
                [new StringName("freeze")] = new StringName("immune"),
            },
        };
        subrace.save_advantage_tags.Add("poison");
        subrace.racial_trait_summary.Add("High human focus");
        return subrace;
    }

    private static AgeProfileDef MakeAgeProfile()
    {
        AgeProfileDef ageProfile = new()
        {
            profile_id = "human_age",
            race_id = "human",
            default_age_by_stage = new GDictionary { ["adult"] = 18 },
        };
        ageProfile.stage_rules.Add(MakeAgeStageRule("teen"));
        ageProfile.stage_rules.Add(MakeAgeStageRule("adult"));
        ageProfile.stage_rules.Add(MakeAgeStageRule("middle_age"));
        ageProfile.stage_rules.Add(MakeAgeStageRule("old"));
        ageProfile.creation_stage_ids.Add("adult");
        return ageProfile;
    }

    private static AgeStageRule MakeAgeStageRule(StringName stageId)
    {
        AgeStageRule rule = new()
        {
            stage_id = stageId,
            display_name = stageId.ToString(),
            description = "Fixture age stage.",
        };
        rule.trait_summary.Add($"Age stage {stageId}");
        return rule;
    }

    private static BloodlineDef MakeBloodline(
        StringName bloodlineId,
        IEnumerable<StringName> stageIds,
        IEnumerable<RacialGrantedSkill> grants
    )
    {
        BloodlineDef bloodline = new()
        {
            bloodline_id = bloodlineId,
            display_name = bloodlineId.ToString(),
            description = "Fixture bloodline.",
        };
        AddStringNames(bloodline.stage_ids, stageIds);
        AddGrants(bloodline.racial_granted_skills, grants);
        bloodline.trait_summary.Add($"Bloodline {bloodlineId}");
        return bloodline;
    }

    private static BloodlineStageDef MakeBloodlineStage(
        StringName stageId,
        StringName bloodlineId,
        IEnumerable<RacialGrantedSkill> grants
    )
    {
        BloodlineStageDef stage = new()
        {
            stage_id = stageId,
            bloodline_id = bloodlineId,
            display_name = stageId.ToString(),
            description = "Fixture bloodline stage.",
        };
        AddGrants(stage.racial_granted_skills, grants);
        stage.trait_summary.Add($"Bloodline stage {stageId}");
        return stage;
    }

    private static AscensionDef MakeAscension(
        StringName ascensionId,
        IEnumerable<StringName> stageIds,
        IEnumerable<RacialGrantedSkill> grants,
        IEnumerable<StringName> allowedRaceIds,
        IEnumerable<StringName> allowedSubraceIds,
        IEnumerable<StringName> allowedBloodlineIds
    )
    {
        AscensionDef ascension = new()
        {
            ascension_id = ascensionId,
            display_name = ascensionId.ToString(),
            description = "Fixture ascension.",
        };
        AddStringNames(ascension.stage_ids, stageIds);
        AddGrants(ascension.racial_granted_skills, grants);
        AddStringNames(ascension.allowed_race_ids, allowedRaceIds);
        AddStringNames(ascension.allowed_subrace_ids, allowedSubraceIds);
        AddStringNames(ascension.allowed_bloodline_ids, allowedBloodlineIds);
        ascension.trait_summary.Add($"Ascension {ascensionId}");
        return ascension;
    }

    private static AscensionStageDef MakeAscensionStage(
        StringName stageId,
        StringName ascensionId,
        IEnumerable<RacialGrantedSkill> grants
    )
    {
        AscensionStageDef stage = new()
        {
            stage_id = stageId,
            ascension_id = ascensionId,
            display_name = stageId.ToString(),
            description = "Fixture ascension stage.",
            body_size_category_override = "large",
        };
        AddGrants(stage.racial_granted_skills, grants);
        stage.trait_summary.Add("Dragon stage");
        return stage;
    }

    private static StageAdvancementModifier MakeStageAdvancement(StringName modifierId)
    {
        StageAdvancementModifier modifier = new()
        {
            modifier_id = modifierId,
            display_name = modifierId.ToString(),
            target_axis = StageAdvancementModifier.ToStringName(StageAdvancementTargetAxis.Full),
            stage_offset = 2,
            max_stage_id = "old",
        };
        modifier.applies_to_race_ids.Add("human");
        return modifier;
    }

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

    private static RacialGrantedSkill MakeGrantedSkill(StringName skillId) =>
        new()
        {
            skill_id = skillId,
            minimum_skill_level = 1,
            charge_kind = "per_battle",
            charges = 1,
        };

    private static void AddStringNames(
        GStringNameArray target,
        IEnumerable<StringName> source
    )
    {
        foreach (StringName value in source)
            target.Add(value);
    }

    private static void AddGrants(
        Godot.Collections.Array<RacialGrantedSkill> target,
        IEnumerable<RacialGrantedSkill> source
    )
    {
        foreach (RacialGrantedSkill value in source)
            target.Add(value);
    }

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

    private void AssertPlainService(Type serviceType, string typeName)
    {
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
