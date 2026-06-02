using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_bloodline_ascension_regression : SceneTree
{
    private readonly List<string> _failures = new();

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

        if (_failures.Count == 0)
        {
            GD.Print("Bloodline ascension regression: PASS");
            Quit(0);
            return;
        }

        foreach (string failure in _failures)
            GD.PushError(failure);
        GD.Print($"Bloodline ascension regression: FAIL ({_failures.Count})");
        Quit(1);
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
        GDictionary bundle = MakeIdentityBundle();
        PartyMemberState member = MakeMemberState("hero");

        BloodlineApplyService bloodlineService = new();
        bloodlineService.setup(bundle);
        AssertTrue(
            bloodlineService.apply_bloodline(member, "titan", "titan_awakened"),
            "合法 bloodline/stage 组合应写入成员身份。"
        );
        AssertEq(member.bloodline_id, new StringName("titan"), "apply_bloodline 应写入 bloodline_id。");
        AssertEq(
            member.bloodline_stage_id,
            new StringName("titan_awakened"),
            "apply_bloodline 应写入 bloodline_stage_id。"
        );
        AssertFalse(
            bloodlineService.apply_bloodline(member, "titan", "dragon_awakened"),
            "BloodlineApplyService 应拒绝不属于该 bloodline 的 stage。"
        );
        AssertEq(
            member.bloodline_stage_id,
            new StringName("titan_awakened"),
            "非法 bloodline apply 不应污染已存在状态。"
        );

        AscensionApplyService ascensionService = new();
        ascensionService.setup(bundle);
        AssertTrue(
            ascensionService.apply_ascension(member, "dragon_ascension", "dragon_awakened", 42),
            "符合 race/subrace/bloodline 条件时应能应用 ascension。"
        );
        AssertEq(
            member.ascension_id,
            new StringName("dragon_ascension"),
            "apply_ascension 应写入 ascension_id。"
        );
        AssertEq(
            member.ascension_stage_id,
            new StringName("dragon_awakened"),
            "apply_ascension 应写入 ascension_stage_id。"
        );
        AssertEq(
            member.original_race_id_before_ascension,
            new StringName("human"),
            "首次 ascension 应保存原始 race。"
        );
        AssertEq(member.ascension_started_at_world_step, 42, "apply_ascension 应记录开始 world step。");

        StringName beforeStage = member.ascension_stage_id;
        AssertFalse(
            ascensionService.apply_ascension(member, "elf_ascension", "elf_awakened", 43),
            "AscensionApplyService 应拒绝不满足 allowed_race_ids 的升华。"
        );
        AssertEq(member.ascension_stage_id, beforeStage, "非法 ascension apply 不应污染已存在状态。");

        member.race_id = "ascended_dragon";
        AssertTrue(ascensionService.revoke_ascension(member), "revoke_ascension 应能清除当前升华。");
        AssertEq(member.race_id, new StringName("human"), "revoke_ascension 默认应恢复原始 race。");
        AssertEq(member.ascension_id, new StringName(""), "revoke_ascension 应清空 ascension_id。");
        AssertEq(
            member.ascension_started_at_world_step,
            -1,
            "revoke_ascension 应清空开始 world step。"
        );
        AssertEq(
            member.original_race_id_before_ascension,
            new StringName(""),
            "revoke_ascension 应清空原始 race 备份。"
        );

        StageAdvancementApplyService stageService = new();
        stageService.setup(bundle);
        AssertTrue(
            stageService.add_stage_advancement_modifier(member, "growth_boon"),
            "符合身份条件时应能添加阶段提升 modifier。"
        );
        AssertFalse(
            stageService.add_stage_advancement_modifier(member, "growth_boon"),
            "重复添加阶段提升 modifier 应被拒绝。"
        );
        AssertIdsEq(
            member.active_stage_advancement_modifier_ids,
            new[] { new StringName("growth_boon") },
            "阶段提升 modifier 应保持去重列表。"
        );
        AssertTrue(
            stageService.remove_stage_advancement_modifier(member, "growth_boon"),
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
        CharacterManagementModule manager = BuildManager(partyState, new GDictionary(), bundle);
        PartyMemberState member = partyState.get_member_state("hero");

        AssertFalse(
            manager.apply_bloodline("hero", "titan", "dragon_awakened"),
            "CMM 应拒绝不属于该 bloodline 的 stage。"
        );
        AssertEq(member.bloodline_id, new StringName(""), "CMM 非法 bloodline apply 不应写入 bloodline_id。");
        AssertEq(
            member.bloodline_stage_id,
            new StringName(""),
            "CMM 非法 bloodline apply 不应写入 bloodline_stage_id。"
        );

        AssertFalse(
            manager.apply_ascension("hero", "elf_ascension", "elf_awakened", 7),
            "CMM 应拒绝不满足 allowed_race_ids 的 ascension。"
        );
        AssertEq(member.ascension_id, new StringName(""), "CMM 非法 ascension apply 不应写入 ascension_id。");
        AssertEq(
            member.ascension_stage_id,
            new StringName(""),
            "CMM 非法 ascension apply 不应写入 ascension_stage_id。"
        );
        AssertEq(
            member.ascension_started_at_world_step,
            -1,
            "CMM 非法 ascension apply 不应写入开始 world step。"
        );
    }

    private void TestCharacterManagementAppliesIdentityAndRefreshesGrants()
    {
        GDictionary bundle = MakeIdentityBundle();
        SkillDef bloodlineSkill = MakeSkill("bloodline_skill", "bloodline");
        SkillDef bloodlineStageSkill = MakeSkill("bloodline_stage_skill", "bloodline");
        SkillDef ascensionSkill = MakeSkill("ascension_skill", "ascension");
        SkillDef ascensionStageSkill = MakeSkill("ascension_stage_skill", "ascension");
        PartyState partyState = MakePartyState();
        CharacterManagementModule manager = BuildManager(
            partyState,
            new GDictionary
            {
                [bloodlineSkill.skill_id] = bloodlineSkill,
                [bloodlineStageSkill.skill_id] = bloodlineStageSkill,
                [ascensionSkill.skill_id] = ascensionSkill,
                [ascensionStageSkill.skill_id] = ascensionStageSkill,
            },
            bundle
        );

        AssertTrue(
            manager.apply_bloodline("hero", "titan", "titan_awakened"),
            "CharacterManagementModule.apply_bloodline 应委托服务并刷新成员。"
        );
        PartyMemberState member = partyState.get_member_state("hero");
        AssertIdentityGrantedSkill(member, "bloodline_skill", "bloodline", "titan");
        AssertIdentityGrantedSkill(
            member,
            "bloodline_stage_skill",
            "bloodline",
            "titan_awakened"
        );

        AssertTrue(manager.revoke_bloodline("hero"), "revoke_bloodline 应清空 bloodline 并触发技能撤销。");
        AssertTrue(
            member.progression.get_skill_progress("bloodline_skill") == null,
            "revoke_bloodline 后 bloodline 来源技能应被撤销。"
        );
        AssertTrue(
            member.progression.get_skill_progress("bloodline_stage_skill") == null,
            "revoke_bloodline 后 bloodline stage 来源技能应被撤销。"
        );

        AssertTrue(
            manager.apply_ascension("hero", "dragon_ascension", "dragon_awakened", 11),
            "CharacterManagementModule.apply_ascension 应委托服务并刷新成员。"
        );
        AssertIdentityGrantedSkill(member, "ascension_skill", "ascension", "dragon_ascension");
        AssertIdentityGrantedSkill(
            member,
            "ascension_stage_skill",
            "ascension",
            "dragon_awakened"
        );
        AssertEq(
            member.effective_age_stage_id,
            new StringName("dragon_awakened"),
            "replaces_age_growth 的升华阶段应接管 effective_age_stage_id。"
        );
        AssertEq(
            member.effective_age_stage_source_type,
            new StringName("ascension"),
            "升华接管年龄阶段时应记录来源类型。"
        );
        AssertEq(member.body_size_category, new StringName("large"), "升华阶段体型 override 应刷新 body_size_category。");
        AssertEq(member.body_size, 3, "升华阶段体型 override 应通过 BodySizeContentRules 刷新 body_size。");

        AssertTrue(manager.revoke_ascension("hero", true), "revoke_ascension 应清空 ascension 并触发技能撤销。");
        AssertEq(member.body_size_category, new StringName("medium"), "撤销升华后体型应回到 race/subrace 解析结果。");
        AssertEq(member.body_size, 2, "撤销升华后 body_size 应从 medium 重新派生。");
        AssertTrue(
            member.progression.get_skill_progress("ascension_skill") == null,
            "revoke_ascension 后 ascension 来源技能应被撤销。"
        );
        AssertTrue(
            member.progression.get_skill_progress("ascension_stage_skill") == null,
            "revoke_ascension 后 ascension stage 来源技能应被撤销。"
        );
    }

    private void TestStageAdvancementRefreshesEffectiveStage()
    {
        GDictionary bundle = MakeIdentityBundle();
        PartyState partyState = MakePartyState();
        CharacterManagementModule manager = BuildManager(partyState, new GDictionary(), bundle);
        PartyMemberState member = partyState.get_member_state("hero");
        AssertEq(
            member.effective_age_stage_id,
            new StringName("adult"),
            "测试前置：成员有效阶段应从 adult 开始。"
        );

        AssertTrue(
            manager.add_stage_advancement_modifier("hero", "growth_boon"),
            "CMM 添加阶段提升 modifier 后应刷新 effective age stage。"
        );
        AssertIdsEq(
            member.active_stage_advancement_modifier_ids,
            new[] { new StringName("growth_boon") },
            "CMM 应通过 service 写入 active_stage_advancement_modifier_ids。"
        );
        AssertEq(member.effective_age_stage_id, new StringName("old"), "growth_boon 应把 adult 推进到 old。");
        AssertEq(
            member.effective_age_stage_source_type,
            new StringName("stage_advancement"),
            "阶段提升应记录 effective stage 来源类型。"
        );
        AssertEq(
            member.effective_age_stage_source_id,
            new StringName("growth_boon"),
            "阶段提升应记录 effective stage 来源 id。"
        );

        AssertTrue(
            manager.remove_stage_advancement_modifier("hero", "growth_boon"),
            "CMM 移除阶段提升 modifier 后应刷新 effective age stage。"
        );
        AssertEq(
            member.effective_age_stage_id,
            new StringName("adult"),
            "移除 modifier 后 effective stage 应回到 natural stage。"
        );
        AssertEq(
            member.effective_age_stage_source_type,
            new StringName(""),
            "移除 modifier 后 effective stage 来源类型应清空。"
        );
    }

    private void TestIdentitySummaryIncludesIdentityProjection()
    {
        GDictionary bundle = MakeIdentityBundle();
        GDictionary skillDefs = new();
        foreach (StringName skillId in new StringName[]
        {
            "bloodline_skill",
            "bloodline_stage_skill",
            "ascension_skill",
            "ascension_stage_skill",
        })
        {
            SkillDef skill = MakeSkill(skillId, "bloodline");
            skillDefs[skill.skill_id] = skill;
        }

        PartyState partyState = MakePartyState();
        CharacterManagementModule manager = BuildManager(partyState, skillDefs, bundle);
        AssertTrue(manager.apply_bloodline("hero", "titan", "titan_awakened"), "身份摘要测试前置：应能应用 bloodline。");
        AssertTrue(
            manager.apply_ascension("hero", "dragon_ascension", "dragon_awakened", 11),
            "身份摘要测试前置：应能应用 ascension。"
        );

        GDictionary summary = manager.get_identity_summary_for_member("hero");
        AssertEq(ReadString(summary, "race_label"), "Human", "身份摘要应包含 race display_name。");
        AssertEq(ReadString(summary, "subrace_label"), "High Human", "身份摘要应包含 subrace display_name。");
        AssertEq(ReadString(summary, "bloodline_label"), "titan", "身份摘要应包含 bloodline display_name。");
        AssertEq(
            ReadString(summary, "ascension_label"),
            "dragon_ascension",
            "身份摘要应包含 ascension display_name。"
        );
        AssertEq(
            ReadString(summary, "effective_age_stage_label"),
            "dragon_awakened",
            "身份摘要应读取刷新后的 effective stage。"
        );
        AssertEq(
            ReadStringName(summary, "body_size_category"),
            new StringName("large"),
            "身份摘要应包含当前升华后的 body_size_category。"
        );
        AssertEq(ReadInt(summary, "body_size"), 3, "身份摘要应包含当前升华后的 body_size。");

        GDictionary damageResistances = ReadDictionary(summary, "damage_resistances");
        AssertEq(
            ReadStringName(damageResistances, "fire"),
            new StringName("half"),
            "身份摘要应合并 race damage_resistances。"
        );
        AssertEq(
            ReadStringName(damageResistances, "freeze"),
            new StringName("immune"),
            "身份摘要应合并 subrace damage_resistances。"
        );

        GArray saveTags = ReadArray(summary, "save_advantage_tags");
        AssertTrue(ContainsStringName(saveTags, "charm"), "身份摘要应包含 race save advantage tag。");
        AssertTrue(ContainsStringName(saveTags, "poison"), "身份摘要应包含 subrace save advantage tag。");

        GArray traitLines = ReadArray(summary, "trait_summary");
        AssertTrue(ContainsString(traitLines, "Human ambition"), "身份摘要应包含 race trait summary。");
        AssertTrue(ContainsString(traitLines, "Dragon stage"), "身份摘要应包含 ascension stage trait summary。");

        GArray racialSkillLines = ReadArray(summary, "racial_skill_lines");
        AssertTrue(
            ArrayContainsText(racialSkillLines, "bloodline_skill"),
            "身份摘要应包含 bloodline grant 技能。"
        );
        AssertTrue(
            ArrayContainsText(racialSkillLines, "ascension_stage_skill"),
            "身份摘要应包含 ascension stage grant 技能。"
        );
    }

    private static CharacterManagementModule BuildManager(
        PartyState partyState,
        GDictionary skillDefs,
        GDictionary bundle
    )
    {
        CharacterManagementModule manager = new();
        manager.setup(
            partyState,
            skillDefs,
            new GDictionary(),
            new GDictionary(),
            new GDictionary(),
            new GDictionary(),
            null,
            bundle
        );
        return manager;
    }

    private static PartyState MakePartyState()
    {
        PartyState partyState = new();
        PartyMemberState member = MakeMemberState("hero");
        partyState.set_member_state(member);
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
            target_axis = StageAdvancementModifier.TARGET_AXIS_FULL(),
            stage_offset = 2,
            max_stage_id = "old",
        };
        modifier.applies_to_race_ids.Add("human");
        return modifier;
    }

    private static SkillDef MakeSkill(StringName skillId, StringName learnSource)
    {
        SkillDef skill = new()
        {
            skill_id = skillId,
            display_name = skillId.ToString(),
            icon_id = skillId,
            description = "Fixture skill.",
            skill_type = "passive",
            learn_source = learnSource,
            max_level = 3,
            mastery_curve = new[] { 10, 20, 30 },
        };
        return skill;
    }

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
        UnitSkillProgress skillProgress = member?.progression?.get_skill_progress(skillId);
        AssertTrue(
            skillProgress != null && skillProgress.is_learned,
            $"{skillId} 应已被身份授予。"
        );
        if (skillProgress == null)
            return;
        AssertEq(
            skillProgress.granted_source_type,
            expectedSourceType,
            $"{skillId} 身份技能来源类型应匹配。"
        );
        AssertEq(
            skillProgress.granted_source_id,
            expectedSourceId,
            $"{skillId} 身份技能来源 id 应匹配。"
        );
    }

    private void AssertPlainService(Type serviceType, string typeName)
    {
        AssertFalse(
            typeof(GodotObject).IsAssignableFrom(serviceType),
            $"{typeName} 应是普通 C# service，不应继承 GodotObject/RefCounted。"
        );
        AssertFalse(
            serviceType.GetCustomAttributes(typeof(GlobalClassAttribute), inherit: false).Length
                > 0,
            $"{typeName} 不应继续注册为 Godot GlobalClass。"
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
            _failures.Add(
                $"{message} | actual={FormatIds(actualList)} expected={FormatIds(expected)}"
            );
            return;
        }

        for (int index = 0; index < actualList.Count; index++)
        {
            if (actualList[index] != expected[index])
            {
                _failures.Add(
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
