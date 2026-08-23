using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_attribute_source_context_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestDerivedAttributeRuleUsesPlainCoefficientMaps();
        TestAttributeSnapshotExposesBaseAttributeModifiers();
        TestAttributeModifierOverlayCanTargetDerivedAbilityModifier();
        TestAttributeServiceSetupContextAppliesIdentityModifiers();
        TestAttributeServiceSetupBoundaryIndexesTypedDefinitions();
        TestEquipmentRuntimeModifierProjectionUsesDefinitions();
        TestAttributeServiceSetupContextUsesExactDefinitionKeys();
        TestCharacterManagementBuildsAttributeSourceContext();

        RequestTestExit(_test.Finish("Attribute source context regression"));
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

    private void TestDerivedAttributeRuleUsesPlainCoefficientMaps()
    {
        Dictionary<StringName, int> coefficients = new() { ["strength"] = 1 };
        DerivedAttributeRule rule = new(
            "half_strength",
            0,
            coefficients,
            2,
            -10,
            10,
            0
        );
        coefficients["strength"] = 99;

        _test.Eq(
            rule.coefficients["strength"],
            1,
            "DerivedAttributeRule should snapshot its managed coefficient input."
        );
        _test.Eq(
            rule.evaluate(new Dictionary<StringName, int> { ["strength"] = -1 }),
            -1,
            "DerivedAttributeRule should preserve floor rounding for negative fractional values."
        );
    }

    private void TestAttributeModifierOverlayCanTargetDerivedAbilityModifier()
    {
        UnitProgress progress = MakeProgress("modifier_overlay");
        progress.unit_base_attributes.SetAttributeValue("perception", 12);
        AttributeModifierDefinition equipmentPerceptionModifier =
            Definition(AttributeService.ToStringName(AttributeIdKind.PerceptionModifier), 3);

        AttributeService service = new();
        service.SetupContext(
            new AttributeSourceContext
            {
                unit_progress = progress,
                equipment_state = new[] { equipmentPerceptionModifier },
            }
        );

        AttributeSnapshot snapshot = service.GetSnapshot();
        _test.Eq(snapshot.GetValue("perception"), 12, "调整值加值不应改写基础感知。");
        _test.Eq(
            snapshot.GetValue(AttributeService.ToStringName(AttributeIdKind.PerceptionModifier)),
            4,
            "perception_modifier 应等于基础感知 12 的 +1 再叠加装备 +3。"
        );
        _test.Eq(
            progress.unit_base_attributes.GetAttributeValue("perception"),
            12,
            "装备调整值加值不应持久改写 UnitProgress 基础感知。"
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

        ProfessionDefinition profession = MakeProfession(
            "warrior", BuildAttributeModifierDefinitions(Modifier("strength", 1, valuePerRank: 1))
        );
        SkillDefinition skill = TestSkillDefinitionProjection.BuildSkill(
            "toughness",
            skillType: "passive",
            attributeModifiers: BuildAttributeModifierDefinitions(
                Modifier(AttributeService.ToStringName(AttributeIdKind.CharacterHpMaxPercentBonus), 20),
                Modifier(AttributeService.ToStringName(AttributeIdKind.StaminaRecoveryPercentBonus), 50)
            )
        );

        UnitProfessionProgress professionProgress = new()
        {
            profession_id = profession.ProfessionId,
            rank = 2,
            is_active = true,
        };
        progress.SetProfessionProgress(professionProgress);
        UnitSkillProgress skillProgress = new()
        {
            skill_id = skill.SkillId,
            is_learned = true,
            skill_level = 0,
            profession_granted_by = profession.ProfessionId,
        };
        progress.SetSkillProgress(skillProgress);

        AttributeModifierDefinition equipmentHp =
            Definition(AttributeService.ToStringName(AttributeIdKind.HpMax), 10);
        AttributeModifierDefinition temporaryHp =
            Definition(AttributeService.ToStringName(AttributeIdKind.HpMax), 50);

        AttributeService service = new();
        service.SetupContext(
            new AttributeSourceContext
            {
                unit_progress = progress,
                skill_definitions = new Dictionary<StringName, SkillDefinition>
                {
                    [skill.SkillId] = skill,
                },
                profession_defs = new Dictionary<StringName, ProfessionDefinition>
                {
                    [profession.ProfessionId] = profession,
                },
                equipment_state = new[] { equipmentHp },
                passive_state = System.Array.Empty<AttributeModifierDefinition>(),
                temporary_effects = new[] { temporaryHp },
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

        SkillDefinition skill = TestSkillDefinitionProjection.BuildSkill(
            "strict_toughness",
            skillType: "passive",
            attributeModifiers: BuildAttributeModifierDefinitions(
                Modifier(AttributeService.ToStringName(AttributeIdKind.CharacterHpMaxPercentBonus), 20)
            )
        );
        progress.SetSkillProgress(
            new UnitSkillProgress
            {
                skill_id = skill.SkillId,
                is_learned = true,
                skill_level = 1,
            }
        );

        ProfessionDefinition profession = MakeProfession(
            "strict_warrior", BuildAttributeModifierDefinitions(Modifier("strength", 2))
        );
        progress.SetProfessionProgress(
            new UnitProfessionProgress
            {
                profession_id = profession.ProfessionId,
                rank = 2,
                is_active = true,
            }
        );

        AttributeService service = new();
        service.SetupContext(
            new AttributeSourceContext
            {
                unit_progress = progress,
                skill_definitions = new Dictionary<StringName, SkillDefinition>
                {
                    [new StringName("wrong_toughness_key")] = skill,
                },
                profession_defs = new Dictionary<StringName, ProfessionDefinition>
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

    private void TestEquipmentRuntimeModifierProjectionUsesDefinitions()
    {
        TestItemDefinitionBuilder armor = new()
        {
            item_id = "runtime_armor",
            item_category = "equipment",
            equipment_type_id = "armor",
            equipment_slot_ids = new Godot.Collections.Array<string> { "body" },
            max_dex_bonus = 2,
            attribute_modifiers = new List<AttributeModifierDefinition>
            {
                Definition(
                    "strength",
                    3,
                    sourceType: "equipment",
                    sourceId: "runtime_armor"
                ),
            },
        };
        ItemDefinition armorDefinition = armor.ToDefinition();
        EquipmentState equipmentState = new();
        _test.True(
            equipmentState.SetEquippedEntry(
                "body",
                armor.item_id,
                new[] { new StringName("body") },
                EquipmentInstanceState.CreateInstance(
                    armor.item_id,
                    "runtime_armor_instance"
                )
            ),
            "Equipment modifier fixture should equip the runtime armor."
        );

        PartyEquipmentService service = new();
        try
        {
            service.Setup(
                new PartyState(),
                new Dictionary<StringName, ItemDefinition>
                {
                    [armorDefinition.ItemId] = armorDefinition,
                }
            );
            IReadOnlyList<AttributeModifierDefinition> definitions =
                service.BuildAttributeModifiersTyped(equipmentState);

            _test.Eq(
                definitions.Count,
                2,
                "Equipment projection should include authored and armor cap definitions."
            );
            if (definitions.Count != 2)
                return;
            _test.Eq(
                definitions[0].AttributeId,
                new StringName("strength"),
                "Authored equipment modifier order should be preserved."
            );
            _test.Eq(
                definitions[1].AttributeId,
                AttributeService.ToStringName(AttributeIdKind.ArmorMaxDexBonus),
                "Dynamic armor max-dex projection should append a plain definition."
            );
            _test.Eq(
                definitions[1].Value,
                2,
                "Armor max-dex definition should preserve its numeric value."
            );
        }
        finally
        {
            service.Dispose();
        }
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
            new Dictionary<StringName, SkillDefinition>(),
            new Dictionary<StringName, ProfessionDefinition>(),
            new Dictionary<StringName, AchievementDefinition>(),
            new Dictionary<StringName, ItemDefinition>(),
            new Dictionary<StringName, QuestDefinition>(),
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
            context.age_stage_rule != null && context.age_stage_rule.StageId == "old",
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
        RaceDefinition race = MakeRace(Modifier("strength", 1));
        SubraceDefinition subrace = MakeSubrace(Modifier("agility", 2));
        AgeProfileDefinition ageProfile = new(
            "human_age", "human", 0, 12, 16, 18, 35, 55, 75, 100,
            new[]
            {
                MakeAgeStageRule("adult"), MakeAgeStageRule("middle_age"),
                MakeAgeStageRule("old", Modifier("constitution", 4)),
            },
            new[] { new StringName("adult") },
            new Dictionary<StringName, int> { ["adult"] = 18 }
        );
        BloodlineDefinition bloodline = MakeBloodline(
            "titan",
            new[] { new StringName("titan_awakened") },
            Modifier("willpower", 3)
        );
        BloodlineStageDefinition bloodlineStage = MakeBloodlineStage("titan_awakened", "titan");
        StageAdvancementDefinition growthBoon = new(
            "growth_boon", "Growth Boon", "full", 2, "old",
            new[] { new StringName("human") }, Array.Empty<StringName>(),
            Array.Empty<StringName>(), Array.Empty<StringName>(), true, false, false
        );

        return new ProgressionIdentityCatalogData(
            new Dictionary<StringName, RaceDefinition> { [race.RaceId] = race },
            new Dictionary<StringName, SubraceDefinition> { [subrace.SubraceId] = subrace },
            new Dictionary<StringName, AgeProfileDefinition>
                {
                    [ageProfile.ProfileId] = ageProfile,
                },
            new Dictionary<StringName, BloodlineDefinition>
                {
                    [bloodline.BloodlineId] = bloodline,
                },
            new Dictionary<StringName, BloodlineStageDefinition>
                {
                    [bloodlineStage.StageId] = bloodlineStage,
                },
            new Dictionary<StringName, AscensionDefinition>(),
            new Dictionary<StringName, AscensionStageDefinition>(),
            new Dictionary<StringName, StageAdvancementDefinition>
                {
                    [growthBoon.ModifierId] = growthBoon,
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

    private static RaceDefinition MakeRace(params AttributeModifier[] modifiers) =>
        new(
            "human", "Human", "Fixture race.", "human_age", "high_human",
            new[] { new StringName("high_human") }, "medium", 6,
            BuildAttributeModifierDefinitions(modifiers), Array.Empty<StringName>(),
            Array.Empty<RacialGrantedSkillDefinition>(), Array.Empty<StringName>(), Array.Empty<StringName>(),
            Array.Empty<StringName>(), Array.Empty<StringName>(), Array.Empty<StringName>(),
            new Dictionary<StringName, StringName>(), Array.Empty<StringName>(), Array.Empty<string>()
        );

    private static SubraceDefinition MakeSubrace(params AttributeModifier[] modifiers) =>
        new(
            "high_human", "human", "High Human", "Fixture subrace.", "", 0,
            BuildAttributeModifierDefinitions(modifiers), Array.Empty<StringName>(),
            Array.Empty<RacialGrantedSkillDefinition>(), Array.Empty<StringName>(), Array.Empty<StringName>(),
            Array.Empty<StringName>(), Array.Empty<StringName>(), Array.Empty<StringName>(),
            new Dictionary<StringName, StringName>(), Array.Empty<StringName>(), Array.Empty<string>()
        );

    private static AgeStageRuleDefinition MakeAgeStageRule(
        StringName stageId,
        params AttributeModifier[] modifiers
    )
    {
        return new AgeStageRuleDefinition(
            stageId, stageId.ToString(), "Fixture age stage.",
            BuildAttributeModifierDefinitions(modifiers), Array.Empty<StringName>(),
            Array.Empty<string>(), true, true
        );
    }

    private static BloodlineDefinition MakeBloodline(
        StringName bloodlineId,
        IEnumerable<StringName> stageIds,
        params AttributeModifier[] modifiers
    )
    {
        return new BloodlineDefinition(
            bloodlineId, bloodlineId.ToString(), "Fixture bloodline.",
            new List<StringName>(stageIds), Array.Empty<StringName>(),
            Array.Empty<RacialGrantedSkillDefinition>(), BuildAttributeModifierDefinitions(modifiers),
            Array.Empty<string>()
        );
    }

    private static BloodlineStageDefinition MakeBloodlineStage(
        StringName stageId,
        StringName bloodlineId,
        params AttributeModifier[] modifiers
    )
    {
        return new BloodlineStageDefinition(
            stageId, bloodlineId, stageId.ToString(), "Fixture bloodline stage.",
            BuildAttributeModifierDefinitions(modifiers), Array.Empty<StringName>(),
            Array.Empty<RacialGrantedSkillDefinition>(), Array.Empty<string>()
        );
    }

    private static AscensionDefinition MakeAscension(StringName ascensionId, IEnumerable<StringName> stageIds)
    {
        return new AscensionDefinition(
            ascensionId, ascensionId.ToString(), "Fixture ascension.", new List<StringName>(stageIds),
            Array.Empty<StringName>(), Array.Empty<RacialGrantedSkillDefinition>(), Array.Empty<StringName>(),
            Array.Empty<StringName>(), Array.Empty<StringName>(), Array.Empty<string>(), false, false
        );
    }

    private static AscensionStageDefinition MakeAscensionStage(
        StringName stageId,
        StringName ascensionId,
        params AttributeModifier[] modifiers
    )
    {
        return new AscensionStageDefinition(
            stageId, ascensionId, stageId.ToString(), "Fixture ascension stage.",
            BuildAttributeModifierDefinitions(modifiers), Array.Empty<StringName>(),
            Array.Empty<RacialGrantedSkillDefinition>(), "", Array.Empty<string>()
        );
    }

    private static ProfessionDefinition MakeProfession(
        StringName professionId,
        IReadOnlyList<AttributeModifierDefinition> modifiers
    ) =>
        new(
            professionId, professionId.ToString(), "Fixture profession.", 3, 8,
            "full", true, "", null,
            Array.Empty<ProfessionRankRequirementDefinition>(),
            Array.Empty<ProfessionGrantedSkillDefinition>(), modifiers,
            Array.Empty<ProfessionActiveConditionDefinition>(), "auto", "count_when_hidden"
        );

    private static AttributeModifier Modifier(
        StringName attributeId,
        int value,
        StringName mode = default,
        int valuePerRank = 0,
        StringName sourceType = default,
        StringName sourceId = default
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
            source_type = sourceType,
            source_id = sourceId,
        };
    }

    private static AttributeModifierDefinition Definition(
        StringName attributeId,
        int value,
        StringName mode = default,
        int valuePerRank = 0,
        StringName sourceType = default,
        StringName sourceId = default
    ) =>
        new(
            attributeId,
            mode != null && mode != ""
                ? mode
                : AttributeModifier.ToStringName(AttributeModifierMode.Flat),
            value,
            valuePerRank,
            sourceType,
            sourceId
        );

    private static AttributeModifierDefinition[] BuildAttributeModifierDefinitions(
        params AttributeModifier[] modifiers
    )
    {
        if (modifiers == null || modifiers.Length == 0)
            return System.Array.Empty<AttributeModifierDefinition>();
        List<AttributeModifierDefinition> result = new();
        foreach (AttributeModifier modifier in modifiers)
        {
            AttributeModifierDefinition definition = AttributeModifierDefinition.FromDiagnosticFixture(
                modifier
            );
            if (definition != null)
                result.Add(definition);
        }
        return result.ToArray();
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
