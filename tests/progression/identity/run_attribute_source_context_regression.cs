using System;
using System.Collections.Generic;
using System.Reflection;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GResourceArray = Godot.Collections.Array<Godot.Resource>;

public partial class run_attribute_source_context_regression : SceneTree
{
    private readonly List<string> _failures = new();

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
        TestCharacterManagementBuildsAttributeSourceContext();

        if (_failures.Count == 0)
        {
            GD.Print("Attribute source context regression: PASS");
            Quit(0);
            return;
        }

        foreach (string failure in _failures)
            GD.PushError(failure);
        GD.Print($"Attribute source context regression: FAIL ({_failures.Count})");
        Quit(1);
    }

    private void TestAttributeSourceContextNoLongerRequiresGodotRegistration()
    {
        Type contextType = typeof(AttributeSourceContext);
        AssertFalse(
            typeof(GodotObject).IsAssignableFrom(contextType),
            "AttributeSourceContext 应是普通 C# DTO，不应继承 GodotObject/RefCounted。"
        );
        AssertFalse(
            contextType.GetCustomAttributes(typeof(GlobalClassAttribute), inherit: false).Length
                > 0,
            "AttributeSourceContext 不应继续注册为 Godot GlobalClass。"
        );
    }

    private void TestAttributeSnapshotExposesBaseAttributeModifiers()
    {
        AttributeSnapshot directSnapshot = new();
        directSnapshot.set_value("strength", 8);
        AssertEq(
            directSnapshot.get_value(AttributeSnapshot.STRENGTH_MODIFIER()),
            -1,
            "直接写入 snapshot 六维时应同步调整值。"
        );

        UnitProgress progress = MakeProgress("modifier");
        progress.unit_base_attributes.set_attribute_value("strength", 8);
        progress.unit_base_attributes.set_attribute_value("agility", 9);
        progress.unit_base_attributes.set_attribute_value("constitution", 10);
        progress.unit_base_attributes.set_attribute_value("perception", 11);
        progress.unit_base_attributes.set_attribute_value("intelligence", 12);
        progress.unit_base_attributes.set_attribute_value("willpower", 20);

        AttributeService service = new();
        service.setup(progress);
        AttributeSnapshot snapshot = service.get_snapshot();
        AssertEq(
            snapshot.get_value(AttributeService.STRENGTH_MODIFIER_ID()),
            -1,
            "snapshot 应暴露力量调整值。"
        );
        AssertEq(
            snapshot.get_value(AttributeService.AGILITY_MODIFIER_ID()),
            -1,
            "snapshot 应暴露敏捷调整值。"
        );
        AssertEq(
            snapshot.get_value(AttributeService.CONSTITUTION_MODIFIER_ID()),
            0,
            "snapshot 应暴露体质调整值。"
        );
        AssertEq(
            snapshot.get_value(AttributeService.PERCEPTION_MODIFIER_ID()),
            0,
            "snapshot 应暴露感知调整值。"
        );
        AssertEq(
            snapshot.get_value(AttributeService.INTELLIGENCE_MODIFIER_ID()),
            1,
            "snapshot 应暴露智力调整值。"
        );
        AssertEq(
            snapshot.get_value(AttributeService.WILLPOWER_MODIFIER_ID()),
            5,
            "snapshot 应暴露意志调整值。"
        );
        AssertEq(
            snapshot.to_dict()["strength_modifier"].AsInt32(),
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
        service.setup_context(context);
        AttributeSnapshot snapshot = service.get_snapshot();
        AssertEq(snapshot.get_value("strength"), 17, "race/subrace/bloodline stage 修正应叠加到力量。");
        AssertEq(snapshot.get_value("agility"), 11, "versatility_pick 应作为独立 +1 修正进入敏捷。");
        AssertEq(snapshot.get_value("constitution"), 13, "effective age stage 修正应进入体质。");
        AssertEq(snapshot.get_value("perception"), 16, "ascension stage 修正应进入感知。");
        AssertEq(snapshot.get_value("intelligence"), 15, "ascension 修正应进入智力。");
        AssertEq(snapshot.get_value("willpower"), 11, "bloodline 修正应进入意志。");
        AssertEq(service.get_modifier("strength"), 3, "get_modifier 应使用 5e 属性修正公式。");
    }

    private void TestAttributeServiceSetupBoundaryIndexesTypedDefinitions()
    {
        UnitProgress progress = MakeProgress("boundary");
        progress.unit_base_attributes.set_attribute_value(AttributeService.HP_MAX_ID(), 30);

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
            Modifier(AttributeService.CHARACTER_HP_MAX_PERCENT_BONUS_ID(), 20),
            Modifier(AttributeService.STAMINA_RECOVERY_PERCENT_BONUS_ID(), 50)
        );

        UnitProfessionProgress professionProgress = new()
        {
            profession_id = profession.profession_id,
            rank = 2,
            is_active = true,
        };
        progress.set_profession_progress(professionProgress);
        UnitSkillProgress skillProgress = new()
        {
            skill_id = skill.skill_id,
            is_learned = true,
            skill_level = 0,
            profession_granted_by = profession.profession_id,
        };
        progress.set_skill_progress(skillProgress);

        AttributeModifier equipmentHp = Modifier(AttributeService.HP_MAX_ID(), 10);
        AttributeModifier temporaryHp = Modifier(AttributeService.HP_MAX_ID(), 50);

        AttributeService service = new();
        service.setup(
            progress,
            new GDictionary { [skill.skill_id] = skill },
            new GDictionary { [profession.profession_id] = profession },
            new GArray { equipmentHp },
            new GArray(),
            new GArray { temporaryHp }
        );

        AttributeSnapshot snapshot = service.get_snapshot();
        AssertEq(snapshot.get_value("strength"), 12, "职业 rank 修正应通过 typed profession map 生效。");
        AssertEq(
            snapshot.get_value(AttributeService.CHARACTER_HP_MAX_PERCENT_BONUS_ID()),
            20,
            "被动技能应提供人物生命百分比加成。"
        );
        AssertEq(
            snapshot.get_value(AttributeService.STAMINA_RECOVERY_PERCENT_BONUS_ID()),
            50,
            "被动技能应提供体力恢复百分比加成。"
        );
        AssertEq(
            snapshot.get_value(AttributeService.HP_MAX_ID()),
            96,
            "人物生命百分比应只放大 persistent HP，再叠加装备与临时修正。"
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
        partyState.set_member_state(member);
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
            MakeContentBundle()
        );
        AssertTrue(
            manager.add_stage_advancement_modifier("hero", "growth_boon"),
            "CMM 应通过 stage advancement service 写入长期阶段提升。"
        );
        AssertTrue(
            manager.apply_bloodline("hero", "titan", "titan_awakened"),
            "CMM 应通过 bloodline service 写入血脉身份。"
        );

        AttributeSourceContext context = manager.build_attribute_source_context("hero");
        AssertTrue(
            context.age_stage_rule != null && context.age_stage_rule.stage_id == "old",
            "CMM context 应解析 effective age stage rule。"
        );
        AssertEq(
            context.age_stage_source_type,
            new StringName("stage_advancement"),
            "CMM context 应保留 effective stage 来源类型。"
        );
        AssertEq(
            context.age_stage_source_id,
            new StringName("growth_boon"),
            "CMM context 应保留 effective stage 来源 id。"
        );

        AttributeSnapshot snapshot = manager.get_member_attribute_snapshot("hero");
        AssertEq(snapshot.get_value("strength"), 11, "CMM snapshot 应包含 race 属性修正。");
        AssertEq(snapshot.get_value("agility"), 12, "CMM snapshot 应包含 subrace 属性修正。");
        AssertEq(
            snapshot.get_value("constitution"),
            14,
            "CMM snapshot 应包含 stage advancement 推导出的 age stage 修正。"
        );
        AssertEq(snapshot.get_value("perception"), 11, "CMM snapshot 应包含 versatility 修正且不改写 base。");
        AssertEq(snapshot.get_value("willpower"), 13, "CMM snapshot 应包含 bloodline 属性修正。");
        AssertEq(
            member.progression.unit_base_attributes.get_attribute_value("perception"),
            10,
            "versatility 不应持久改写基础属性。"
        );
    }

    private static GDictionary MakeContentBundle()
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

        return new GDictionary
        {
            ["race_defs"] = new GDictionary { [race.race_id] = race },
            ["subrace_defs"] = new GDictionary { [subrace.subrace_id] = subrace },
            ["age_profile_defs"] = new GDictionary { [ageProfile.profile_id] = ageProfile },
            ["bloodline_defs"] = new GDictionary { [bloodline.bloodline_id] = bloodline },
            ["bloodline_stage_defs"] = new GDictionary { [bloodlineStage.stage_id] = bloodlineStage },
            ["ascension_defs"] = new GDictionary(),
            ["ascension_stage_defs"] = new GDictionary(),
            ["stage_advancement_defs"] = new GDictionary { [growthBoon.modifier_id] = growthBoon },
        };
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
            progress.unit_base_attributes.set_attribute_value(attributeId, 10);
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
            mode = mode != "" ? mode : AttributeModifier.MODE_FLAT(),
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
