using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GResourceArray = Godot.Collections.Array<Godot.Resource>;

public partial class run_attribute_source_context_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        CallDeferred(nameof(Run));
    }

    private void Run()
    {
        TestAttributeSourceContextNoLongerRequiresGodotRegistration();
        TestAttributeSnapshotExposesBaseAttributeModifiers();
        TestAttributeServiceSetupContextAppliesIdentityModifiers();
        TestAttributeServiceSetupBoundaryIndexesTypedDefinitions();
        TestAttributeServiceSetupContextUsesExactDefinitionKeys();
        TestCharacterManagementBuildsAttributeSourceContext();

        Quit(_test.Finish("Attribute source context regression"));
    }

    private void TestAttributeSourceContextNoLongerRequiresGodotRegistration()
    {
        Type contextType = typeof(AttributeSourceContext);
    }

    private void TestAttributeSnapshotExposesBaseAttributeModifiers()
    {
        AttributeSnapshot directSnapshot = new();
        directSnapshot.SetValue("strength", 8);
        _test.Eq(
            directSnapshot.GetValue(AttributeSnapshot.ToStringName(AttributeSnapshotIdKind.StrengthModifier)),
            -1,
            "直接写入 snapshot 六维时应同步调整值。"
        );

        UnitProgress progress = MakeProgress("modifier");
        progress.unit_base_attributes.SetAttributeValue("strength", 8);
        progress.unit_base_attributes.SetAttributeValue("agility", 9);
        progress.unit_base_attributes.SetAttributeValue("constitution", 10);
        progress.unit_base_attributes.SetAttributeValue("perception", 11);
        progress.unit_base_attributes.SetAttributeValue("intelligence", 12);
        progress.unit_base_attributes.SetAttributeValue("willpower", 20);

        AttributeService service = new();
        service.Setup(progress);
        AttributeSnapshot snapshot = service.GetSnapshot();
        _test.Eq(
            snapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.StrengthModifier)),
            -1,
            "snapshot 应暴露力量调整值。"
        );
        _test.Eq(
            snapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.AgilityModifier)),
            -1,
            "snapshot 应暴露敏捷调整值。"
        );
        _test.Eq(
            snapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.ConstitutionModifier)),
            0,
            "snapshot 应暴露体质调整值。"
        );
        _test.Eq(
            snapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.PerceptionModifier)),
            0,
            "snapshot 应暴露感知调整值。"
        );
        _test.Eq(
            snapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.IntelligenceModifier)),
            1,
            "snapshot 应暴露智力调整值。"
        );
        _test.Eq(
            snapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.WillpowerModifier)),
            5,
            "snapshot 应暴露意志调整值。"
        );
        _test.Eq(
            snapshot.ToDictionary()["strength_modifier"].AsInt32(),
            -1,
            "snapshot 字典应包含力量调整值。"
        );
    }

    private void TestAttributeServiceSetupContextAppliesIdentityModifiers()
    {
        UnitProgress progress = MakeProgress("direct");
        AttributeSourceContext context = new()
        {
            unit_progress = progress,
            race_def = MakeRace(Modifier("strength", 1)),
            subrace_def = MakeSubrace(Modifier("strength", 2)),
            age_stage_rule = MakeAgeStageRule("old", Modifier("constitution", 3)),
            age_stage_source_type = "stage_advancement",
            age_stage_source_id = "growth_boon",
            bloodline_def = MakeBloodline(
                "titan",
                new[] { new StringName("titan_awakened") },
                Modifier("willpower", 1)
            ),
            bloodline_stage_def = MakeBloodlineStage(
                "titan_awakened",
                "titan",
                Modifier("strength", 4)
            ),
            ascension_def = MakeAscension(
                "dragon_ascension",
                new[] { new StringName("dragon_awakened") }
            ),
            ascension_stage_def = MakeAscensionStage(
                "dragon_awakened",
                "dragon_ascension",
                Modifier("intelligence", 5),
                Modifier("perception", 6)
            ),
            versatility_pick = "agility",
        };

        AttributeService service = new();
        service.SetupContext(context);
        AttributeSnapshot snapshot = service.GetSnapshot();
        _test.Eq(snapshot.GetValue("strength"), 17, "race/subrace/bloodline stage 修正应叠加到力量。");
        _test.Eq(snapshot.GetValue("agility"), 11, "versatility_pick 应作为独立 +1 修正进入敏捷。");
        _test.Eq(snapshot.GetValue("constitution"), 13, "effective age stage 修正应进入体质。");
        _test.Eq(snapshot.GetValue("perception"), 16, "ascension stage 修正应进入感知。");
        _test.Eq(snapshot.GetValue("intelligence"), 15, "ascension 修正应进入智力。");
        _test.Eq(snapshot.GetValue("willpower"), 11, "bloodline 修正应进入意志。");
        _test.Eq(service.GetModifier("strength"), 3, "get_modifier 应使用 5e 属性修正公式。");
    }

    private void TestAttributeServiceSetupBoundaryIndexesTypedDefinitions()
    {
        UnitProgress progress = MakeProgress("boundary");
        progress.unit_base_attributes.SetAttributeValue(AttributeService.ToStringName(AttributeIdKind.HpMax), 30);

        ProfessionDef profession = new()
        {
            profession_id = "warrior",
            max_rank = 3,
            bab_progression = "full",
        };
        profession.attribute_modifiers = new Godot.Collections.Array<AttributeModifier>
        {
            Modifier("strength", 1, valuePerRank: 1),
        };
        SkillDef skill = new()
        {
            skill_id = "toughness",
            skill_type = "passive",
        };
        skill.attribute_modifiers = ResourceModifiers(
            Modifier(AttributeService.ToStringName(AttributeIdKind.CharacterHpMaxPercentBonus), 20),
            Modifier(AttributeService.ToStringName(AttributeIdKind.StaminaRecoveryPercentBonus), 50)
        );

        UnitProfessionProgress professionProgress = new()
        {
            profession_id = profession.profession_id,
            rank = 2,
            is_active = true,
        };
        progress.SetProfessionProgress(professionProgress);
        UnitSkillProgress skillProgress = new()
        {
            skill_id = skill.skill_id,
            is_learned = true,
            skill_level = 0,
            profession_granted_by = profession.profession_id,
        };
        progress.SetSkillProgress(skillProgress);

        AttributeModifier equipmentHp = Modifier(AttributeService.ToStringName(AttributeIdKind.HpMax), 10);
        AttributeModifier temporaryHp = Modifier(AttributeService.ToStringName(AttributeIdKind.HpMax), 50);

        AttributeService service = new();
        service.SetupContext(
            new AttributeSourceContext
            {
                unit_progress = progress,
                skill_defs = new Dictionary<StringName, SkillDef> { [skill.skill_id] = skill },
                profession_defs = new Dictionary<StringName, ProfessionDef>
                {
                    [profession.profession_id] = profession,
                },
                equipment_state = new List<AttributeModifier> { equipmentHp },
                passive_state = new List<AttributeModifier>(),
                temporary_effects = new List<AttributeModifier> { temporaryHp },
            }
        );

        AttributeSnapshot snapshot = service.GetSnapshot();
        _test.Eq(snapshot.GetValue("strength"), 12, "职业 rank 修正应通过 typed profession map 生效。");
        _test.Eq(
            snapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.CharacterHpMaxPercentBonus)),
            20,
            "被动技能应提供人物生命百分比加成。"
        );
        _test.Eq(
            snapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.StaminaRecoveryPercentBonus)),
            50,
            "被动技能应提供体力恢复百分比加成。"
        );
        _test.Eq(
            snapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.HpMax)),
            96,
            "人物生命百分比应只放大 persistent HP，再叠加装备与临时修正。"
        );
    }

    private void TestAttributeServiceSetupContextUsesExactDefinitionKeys()
    {
        UnitProgress progress = MakeProgress("strict_boundary");
        progress.unit_base_attributes.SetAttributeValue(AttributeService.ToStringName(AttributeIdKind.HpMax), 30);

        SkillDef skill = new()
        {
            skill_id = "strict_toughness",
            skill_type = "passive",
        };
        skill.attribute_modifiers = ResourceModifiers(
            Modifier(AttributeService.ToStringName(AttributeIdKind.CharacterHpMaxPercentBonus), 20)
        );
        progress.SetSkillProgress(
            new UnitSkillProgress
            {
                skill_id = skill.skill_id,
                is_learned = true,
                skill_level = 1,
            }
        );

        ProfessionDef profession = new()
        {
            profession_id = "strict_warrior",
            max_rank = 3,
            bab_progression = "full",
        };
        profession.attribute_modifiers = new Godot.Collections.Array<AttributeModifier>
        {
            Modifier("strength", 2),
        };
        progress.SetProfessionProgress(
            new UnitProfessionProgress
            {
                profession_id = profession.profession_id,
                rank = 2,
                is_active = true,
            }
        );

        AttributeService service = new();
        service.SetupContext(
            new AttributeSourceContext
            {
                unit_progress = progress,
                skill_defs = new Dictionary<StringName, SkillDef>
                {
                    [new StringName("wrong_toughness_key")] = skill,
                },
                profession_defs = new Dictionary<StringName, ProfessionDef>
                {
                    [new StringName("wrong_profession_key")] = profession,
                },
            }
        );

        AttributeSnapshot snapshot = service.GetSnapshot();
        _test.Eq(
            snapshot.GetValue("strength"),
            10,
            "AttributeService 不应从 value.skill_id 恢复错误 key 的 skill/profession def。"
        );
        _test.Eq(
            snapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.CharacterHpMaxPercentBonus)),
            0,
            "错误 key 的 skill_defs 不应进入正式属性修正。"
        );
        _test.Eq(
            snapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.BaseAttackBonus)),
            0,
            "value.profession_id 不应补偿错误的 StringName key。"
        );
    }

    private void TestCharacterManagementBuildsAttributeSourceContext()
    {
        PartyState partyState = new();
        PartyMemberState member = new()
        {
            member_id = "hero",
            display_name = "Hero",
            race_id = "human",
            subrace_id = "high_human",
            age_profile_id = "human_age",
            natural_age_stage_id = "adult",
            effective_age_stage_id = "adult",
            versatility_pick = "perception",
            progression = MakeProgress("hero"),
        };
        partyState.SetMemberState(member);
        partyState.active_member_ids = new Godot.Collections.Array<StringName> { "hero" };
        partyState.leader_member_id = "hero";
        partyState.main_character_member_id = "hero";

        CharacterManagementModule manager = new();
        manager.setup(
            partyState,
            new GDictionary(),
            new GDictionary(),
            new GDictionary(),
            new GDictionary(),
            new GDictionary(),
            null,
            MakeIdentityCatalog()
        );
        _test.True(
            manager.AddStageAdvancementModifier("hero", "growth_boon"),
            "CMM 应通过 stage advancement service 写入长期阶段提升。"
        );
        _test.True(
            manager.ApplyBloodline("hero", "titan", "titan_awakened"),
            "CMM 应通过 bloodline service 写入血脉身份。"
        );

        AttributeSourceContext context = manager.build_attribute_source_context("hero");
        _test.True(
            context.age_stage_rule != null && context.age_stage_rule.stage_id == "old",
            "CMM context 应解析 effective age stage rule。"
        );
        _test.Eq(
            context.age_stage_source_type,
            new StringName("stage_advancement"),
            "CMM context 应保留 effective stage 来源类型。"
        );
        _test.Eq(
            context.age_stage_source_id,
            new StringName("growth_boon"),
            "CMM context 应保留 effective stage 来源 id。"
        );

        AttributeSnapshot snapshot = manager.GetMemberAttributeSnapshot("hero");
        _test.Eq(snapshot.GetValue("strength"), 11, "CMM snapshot 应包含 race 属性修正。");
        _test.Eq(snapshot.GetValue("agility"), 12, "CMM snapshot 应包含 subrace 属性修正。");
        _test.Eq(
            snapshot.GetValue("constitution"),
            14,
            "CMM snapshot 应包含 stage advancement 推导出的 age stage 修正。"
        );
        _test.Eq(snapshot.GetValue("perception"), 11, "CMM snapshot 应包含 versatility 修正且不改写 base。");
        _test.Eq(snapshot.GetValue("willpower"), 13, "CMM snapshot 应包含 bloodline 属性修正。");
        _test.Eq(
            member.progression.unit_base_attributes.GetAttributeValue("perception"),
            10,
            "versatility 不应持久改写基础属性。"
        );
    }

    private static ProgressionIdentityCatalogData MakeIdentityCatalog()
    {
        RaceDef race = MakeRace(Modifier("strength", 1));
        SubraceDef subrace = MakeSubrace(Modifier("agility", 2));
        AgeProfileDef ageProfile = new()
        {
            profile_id = "human_age",
            race_id = "human",
            creation_stage_ids = new Godot.Collections.Array<StringName> { "adult" },
            default_age_by_stage = new GDictionary { ["adult"] = 18 },
        };
        ageProfile.stage_rules = new Godot.Collections.Array<AgeStageRule>
        {
            MakeAgeStageRule("adult"),
            MakeAgeStageRule("middle_age"),
            MakeAgeStageRule("old", Modifier("constitution", 4)),
        };
        BloodlineDef bloodline = MakeBloodline(
            "titan",
            new[] { new StringName("titan_awakened") },
            Modifier("willpower", 3)
        );
        BloodlineStageDef bloodlineStage = MakeBloodlineStage("titan_awakened", "titan");
        StageAdvancementModifier growthBoon = new()
        {
            modifier_id = "growth_boon",
            display_name = "Growth Boon",
            target_axis = "full",
            stage_offset = 2,
            max_stage_id = "old",
            applies_to_race_ids = new Godot.Collections.Array<StringName> { "human" },
        };

        return new ProgressionIdentityCatalogData(
            new Dictionary<StringName, RaceDef> { [race.race_id] = race },
            new Dictionary<StringName, SubraceDef> { [subrace.subrace_id] = subrace },
            new Dictionary<StringName, AgeProfileDef> { [ageProfile.profile_id] = ageProfile },
            new Dictionary<StringName, BloodlineDef> { [bloodline.bloodline_id] = bloodline },
            new Dictionary<StringName, BloodlineStageDef> { [bloodlineStage.stage_id] = bloodlineStage },
            new Dictionary<StringName, AscensionDef>(),
            new Dictionary<StringName, AscensionStageDef>(),
            new Dictionary<StringName, StageAdvancementModifier>
            {
                [growthBoon.modifier_id] = growthBoon,
            }
        );
    }

    private static UnitProgress MakeProgress(StringName unitId)
    {
        UnitProgress progress = new()
        {
            unit_id = unitId,
            display_name = unitId.ToString().Capitalize(),
        };
        foreach (
            StringName attributeId in new[]
            {
                new StringName("strength"),
                new StringName("agility"),
                new StringName("constitution"),
                new StringName("perception"),
                new StringName("intelligence"),
                new StringName("willpower"),
            }
        )
            progress.unit_base_attributes.SetAttributeValue(attributeId, 10);
        return progress;
    }

    private static RaceDef MakeRace(params AttributeModifier[] modifiers)
    {
        RaceDef race = new()
        {
            race_id = "human",
            display_name = "Human",
            description = "Fixture race.",
            age_profile_id = "human_age",
            default_subrace_id = "high_human",
            subrace_ids = new Godot.Collections.Array<StringName> { "high_human" },
            body_size_category = "medium",
            base_speed = 6,
            attribute_modifiers = ResourceModifiers(modifiers),
        };
        return race;
    }

    private static SubraceDef MakeSubrace(params AttributeModifier[] modifiers)
    {
        return new SubraceDef
        {
            subrace_id = "high_human",
            parent_race_id = "human",
            display_name = "High Human",
            description = "Fixture subrace.",
            attribute_modifiers = TypedModifiers(modifiers),
        };
    }

    private static AgeStageRule MakeAgeStageRule(
        StringName stageId,
        params AttributeModifier[] modifiers
    )
    {
        return new AgeStageRule
        {
            stage_id = stageId,
            display_name = stageId.ToString(),
            description = "Fixture age stage.",
            attribute_modifiers = ResourceModifiers(modifiers),
        };
    }

    private static BloodlineDef MakeBloodline(
        StringName bloodlineId,
        IEnumerable<StringName> stageIds,
        params AttributeModifier[] modifiers
    )
    {
        BloodlineDef bloodline = new()
        {
            bloodline_id = bloodlineId,
            display_name = bloodlineId.ToString(),
            description = "Fixture bloodline.",
            attribute_modifiers = ResourceModifiers(modifiers),
        };
        foreach (StringName stageId in stageIds)
            bloodline.stage_ids.Add(stageId);
        return bloodline;
    }

    private static BloodlineStageDef MakeBloodlineStage(
        StringName stageId,
        StringName bloodlineId,
        params AttributeModifier[] modifiers
    )
    {
        return new BloodlineStageDef
        {
            stage_id = stageId,
            bloodline_id = bloodlineId,
            display_name = stageId.ToString(),
            description = "Fixture bloodline stage.",
            attribute_modifiers = ResourceModifiers(modifiers),
        };
    }

    private static AscensionDef MakeAscension(StringName ascensionId, IEnumerable<StringName> stageIds)
    {
        AscensionDef ascension = new()
        {
            ascension_id = ascensionId,
            display_name = ascensionId.ToString(),
            description = "Fixture ascension.",
        };
        foreach (StringName stageId in stageIds)
            ascension.stage_ids.Add(stageId);
        return ascension;
    }

    private static AscensionStageDef MakeAscensionStage(
        StringName stageId,
        StringName ascensionId,
        params AttributeModifier[] modifiers
    )
    {
        return new AscensionStageDef
        {
            stage_id = stageId,
            ascension_id = ascensionId,
            display_name = stageId.ToString(),
            description = "Fixture ascension stage.",
            attribute_modifiers = ResourceModifiers(modifiers),
        };
    }

    private static AttributeModifier Modifier(
        StringName attributeId,
        int value,
        StringName mode = default,
        int valuePerRank = 0
    )
    {
        return new AttributeModifier
        {
            attribute_id = attributeId,
            mode = mode != ""
                ? mode
                : AttributeModifier.ToStringName(AttributeModifierMode.Flat),
            value = value,
            value_per_rank = valuePerRank,
        };
    }

    private static GResourceArray ResourceModifiers(params AttributeModifier[] modifiers)
    {
        GResourceArray result = new();
        foreach (AttributeModifier modifier in modifiers)
            if (modifier != null)
                result.Add(modifier);
        return result;
    }

    private static Godot.Collections.Array<AttributeModifier> TypedModifiers(
        params AttributeModifier[] modifiers
    )
    {
        var result = new Godot.Collections.Array<AttributeModifier>();
        foreach (AttributeModifier modifier in modifiers)
            if (modifier != null)
                result.Add(modifier);
        return result;
    }
}
