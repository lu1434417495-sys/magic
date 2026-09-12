using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class run_enemy_template_schema_boundary_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestLocalSchemaAcceptsValidJsonDto();
        TestProjectedGraphAcceptsTypedReferenceTables();
        TestProjectedGraphRejectsMissingItemReferences();
        TestCognitionKindIsRequiredAndClosed();
        TestSaveTagsProjectAndValidate();
        TestDamageResistancesProjectAndValidate();
        TestDerivedHpAndAttackBonusFollowLevelFormula();
        TestCreatureLevelAndHitDieValidation();
        TestBattleEquipmentEntriesRequireTypedEquipmentAndValidDurability();
        TestSkillLevelMapUsesProjectedGraphSkillBounds();

        RequestTestExit(_test.Finish("Enemy template schema boundary regression"));
    }

    private void TestLocalSchemaAcceptsValidJsonDto()
    {
        TemplateDtoBuilder template = BuildValidTemplate(
            "typed_schema_template",
            "typed_schema_weapon"
        );
        IReadOnlyList<ContentJsonDiagnostic> diagnostics = ValidateLocal(template);
        _test.Eq(
            diagnostics.Count,
            0,
            $"typed JSON template 应通过本地域校验。 diagnostics={FormatDiagnostics(diagnostics)}"
        );
    }

    private void TestProjectedGraphAcceptsTypedReferenceTables()
    {
        TemplateDtoBuilder template = BuildValidTemplate(
            "projected_graph_template",
            "projected_graph_weapon"
        );
        var items = new Dictionary<StringName, ItemDefinition>
        {
            ["projected_graph_weapon"] = MakeWeapon(
                "projected_graph_weapon",
                "projected_graph_weapon_type"
            ),
        };
        IReadOnlyList<string> errors = ValidateProjectedGraph(template, items);
        _test.Eq(
            errors.Count,
            0,
            $"immutable Definition graph 应接受 typed 引用表。 errors={FormatErrors(errors)}"
        );
    }

    private void TestProjectedGraphRejectsMissingItemReferences()
    {
        TemplateDtoBuilder template = BuildValidTemplate(
            "missing_item_schema_template",
            "missing_item_schema_weapon"
        );
        template.DropEntries.Clear();
        template.DropEntries.Add(
            new EnemyDropEntryJsonDto
            {
                DropEntryId = "missing_drop",
                DropType = "item",
                ItemId = "missing_drop_item",
                Quantity = 1,
            }
        );

        IReadOnlyList<string> errors = ValidateProjectedGraph(
            template,
            new Dictionary<StringName, ItemDefinition>()
        );
        _test.Eq(
            errors.Count,
            2,
            $"缺失 item fixture 应只报告攻击装备与掉落两条引用错误。 errors={FormatErrors(errors)}"
        );
        AssertError(
            errors,
            "references missing attack equipment missing_item_schema_weapon",
            "应报告缺失攻击装备。"
        );
        AssertError(
            errors,
            "drop missing_drop references missing item missing_drop_item",
            "应报告缺失掉落物品。"
        );
    }

    private void TestCognitionKindIsRequiredAndClosed()
    {
        TemplateDtoBuilder template = BuildValidTemplate(
            "cognition_schema_template",
            "cognition_schema_weapon"
        );
        template.CognitionKind = "";
        AssertDiagnostic(
            ValidateLocal(template),
            EnemyContentImportRules.ValueUnsupported,
            "/cognition_kind",
            "cognition_kind 不能为空。"
        );

        template.CognitionKind = "clever";
        AssertDiagnostic(
            ValidateLocal(template),
            EnemyContentImportRules.ValueUnsupported,
            "/cognition_kind",
            "cognition_kind 应拒绝开放字符串。"
        );

        foreach (string kind in new[] { "mindless", "instinctive", "sapient" })
        {
            template.CognitionKind = kind;
            _test.Eq(
                ValidateLocal(template).Count,
                0,
                $"{kind} 应是合法的正式认知类型。"
            );
        }
    }

    private void TestSaveTagsProjectAndValidate()
    {
        TemplateDtoBuilder template = BuildValidTemplate(
            "save_tag_schema_template",
            "save_tag_schema_weapon"
        );
        template.SaveAdvantageTags.AddRange(new[] { "illusion", "poison" });
        template.SaveDisadvantageTags.Add("frightened");
        template.SaveImmunityTags.AddRange(new[] { "sleep", "poison" });

        IReadOnlyList<ContentJsonDiagnostic> diagnostics = ValidateLocal(template);
        _test.Eq(
            diagnostics.Count,
            0,
            $"三个豁免标签字段应接受闭集裸标签。 diagnostics={FormatDiagnostics(diagnostics)}"
        );
        EnemyTemplateDefinition definition = Project(
            template,
            new Dictionary<StringName, ItemDefinition>()
        );
        _test.Eq(definition.SaveAdvantageTags.Count, 2, "save_advantage_tags 应完整投影。");
        _test.Eq(definition.SaveAdvantageTags[0], new StringName("illusion"), "应保留第一项。");
        _test.Eq(definition.SaveAdvantageTags[1], new StringName("poison"), "应保留第二项。");

        TemplateDtoBuilder suffix = BuildValidTemplate(
            "save_tag_suffix_schema_template",
            "save_tag_suffix_schema_weapon"
        );
        suffix.SaveAdvantageTags.Add("illusion_immunity");
        AssertDiagnostic(
            ValidateLocal(suffix),
            EnemyContentImportRules.ValueUnsupported,
            "/save_advantage_tags/0",
            "后缀式旧标签应被拒绝。"
        );

        TemplateDtoBuilder empty = BuildValidTemplate(
            "empty_save_tag_schema_template",
            "empty_save_tag_schema_weapon"
        );
        empty.SaveAdvantageTags.Add("");
        AssertDiagnostic(
            ValidateLocal(empty),
            EnemyContentImportRules.ValueUnsupported,
            "/save_advantage_tags/0",
            "空 save tag 应被拒绝。"
        );

        TemplateDtoBuilder unsupported = BuildValidTemplate(
            "unsupported_save_tag_schema_template",
            "unsupported_save_tag_schema_weapon"
        );
        unsupported.SaveAdvantageTags.Add("not_a_save_tag");
        AssertDiagnostic(
            ValidateLocal(unsupported),
            EnemyContentImportRules.ValueUnsupported,
            "/save_advantage_tags/0",
            "未知 save tag 应被拒绝。"
        );

        TemplateDtoBuilder duplicate = BuildValidTemplate(
            "duplicate_save_tag_schema_template",
            "duplicate_save_tag_schema_weapon"
        );
        duplicate.SaveAdvantageTags.AddRange(new[] { "poison", "poison" });
        AssertDiagnostic(
            ValidateLocal(duplicate),
            EnemyContentImportRules.DuplicateId,
            "/save_advantage_tags/1",
            "重复 save tag 应被拒绝。"
        );
    }

    private void TestDamageResistancesProjectAndValidate()
    {
        TemplateDtoBuilder template = BuildValidTemplate(
            "damage_resist_schema_template",
            "damage_resist_schema_weapon"
        );
        template.DamageResistances["physical_pierce"] = "half";
        template.DamageResistances["fire"] = "double";
        template.DamageResistances["freeze"] = "immune";
        template.DamageResistances["magic"] = "normal";
        _test.Eq(
            ValidateLocal(template).Count,
            0,
            "damage_resistances 应接受闭集伤害标签与 mitigation tier。"
        );
        EnemyTemplateDefinition definition = Project(
            template,
            new Dictionary<StringName, ItemDefinition>()
        );
        _test.True(
            definition.DamageResistances.Count == 4
                && definition.DamageResistances["physical_pierce"] == (StringName)"half"
                && definition.DamageResistances["fire"] == (StringName)"double",
            "immutable Definition 应完整保留 damage_resistances。"
        );

        TemplateDtoBuilder badTag = BuildValidTemplate(
            "damage_resist_bad_tag_template",
            "damage_resist_bad_tag_weapon"
        );
        badTag.DamageResistances["shadow"] = "half";
        AssertDiagnostic(
            ValidateLocal(badTag),
            EnemyContentImportRules.ValueUnsupported,
            "/damage_resistances/shadow",
            "未知伤害标签应被拒绝。"
        );

        TemplateDtoBuilder badTier = BuildValidTemplate(
            "damage_resist_bad_tier_template",
            "damage_resist_bad_tier_weapon"
        );
        badTier.DamageResistances["fire"] = "quarter";
        AssertDiagnostic(
            ValidateLocal(badTier),
            EnemyContentImportRules.ValueUnsupported,
            "/damage_resistances/fire",
            "未知 mitigation tier 应被拒绝。"
        );
    }

    private void TestDerivedHpAndAttackBonusFollowLevelFormula()
    {
        TemplateDtoBuilder template = BuildValidTemplate(
            "formula_schema_template",
            "formula_schema_weapon"
        );
        template.CreatureLevel = 10;
        template.HitDieSides = 12;
        template.BodySize = BattleUnitState.BodySizeLarge;
        template.BaseAttributeOverrides["strength"] = 18;
        template.BaseAttributeOverrides["constitution"] = 16;
        var items = new Dictionary<StringName, ItemDefinition>
        {
            ["formula_schema_weapon"] = MakeWeapon(
                "formula_schema_weapon",
                "formula_schema_weapon_type"
            ),
        };
        EnemyTemplateDefinition definition = Project(template, items);
        _test.Eq(
            definition.DerivedHpMax,
            520,
            "派生 HP 应按等级、生命骰、体质与 2x2 占位公式得到 520。"
        );
        _test.Eq(
            definition.DerivedAttackBonus,
            4,
            "近战武器的派生攻击加值应使用力量修正。"
        );
        _test.Eq(ValidateLocal(template).Count, 0, "合法公式字段应通过 schema 校验。");

        TemplateDtoBuilder ranged = BuildValidTemplate(
            "formula_schema_ranged_template",
            ""
        );
        ranged.Tags.Add("beast");
        ranged.NaturalWeaponDamageTag = "physical_pierce";
        ranged.NaturalWeaponAttackRange = 5;
        ranged.BaseAttributeOverrides["perception"] = 12;
        EnemyTemplateDefinition rangedDefinition = Project(
            ranged,
            new Dictionary<StringName, ItemDefinition>()
        );
        _test.Eq(
            rangedDefinition.DerivedAttackBonus,
            1,
            "远程天生武器的派生攻击加值应使用感知修正。"
        );
    }

    private void TestCreatureLevelAndHitDieValidation()
    {
        TemplateDtoBuilder negativeLevel = BuildValidTemplate(
            "bad_level_schema_template",
            "bad_level_schema_weapon"
        );
        negativeLevel.CreatureLevel = -1;
        AssertDiagnostic(
            ValidateLocal(negativeLevel),
            EnemyContentImportRules.ValueOutOfRange,
            "/creature_level",
            "creature_level < 0 应被拒绝。"
        );

        TemplateDtoBuilder zeroLevel = BuildValidTemplate(
            "zero_level_schema_template",
            "zero_level_schema_weapon"
        );
        zeroLevel.CreatureLevel = 0;
        zeroLevel.HitDieSides = 8;
        zeroLevel.BaseAttributeOverrides["constitution"] = 14;
        _test.Eq(ValidateLocal(zeroLevel).Count, 0, "creature_level = 0 应合法。");
        _test.Eq(
            Project(zeroLevel, new Dictionary<StringName, ItemDefinition>()).DerivedHpMax,
            12,
            "0 级生物仍应享受首级满骰底子。"
        );

        TemplateDtoBuilder invalidDie = BuildValidTemplate(
            "bad_die_schema_template",
            "bad_die_schema_weapon"
        );
        invalidDie.HitDieSides = 7;
        AssertDiagnostic(
            ValidateLocal(invalidDie),
            EnemyContentImportRules.ValueUnsupported,
            "/hit_die_sides",
            "非法生命骰面数应被拒绝。"
        );
    }

    private void TestBattleEquipmentEntriesRequireTypedEquipmentAndValidDurability()
    {
        TemplateDtoBuilder template = BuildValidTemplate(
            "battle_equipment_schema_template",
            "battle_equipment_schema_weapon"
        );
        template.BattleEquipmentEntries.Add(
            new EnemyBattleEquipmentJsonDto
            {
                SlotId = "body",
                ItemId = "battle_equipment_schema_armor",
                Rarity = 1,
                CurrentDurability = 84,
            }
        );
        var items = new Dictionary<StringName, ItemDefinition>
        {
            ["battle_equipment_schema_weapon"] = MakeWeapon(
                "battle_equipment_schema_weapon",
                "battle_equipment_schema_weapon_type"
            ),
            ["battle_equipment_schema_armor"] = MakeArmor("battle_equipment_schema_armor"),
        };
        _test.Eq(ValidateLocal(template).Count, 0, "合法装备耐久应通过本地域校验。");
        _test.Eq(
            ValidateProjectedGraph(template, items).Count,
            0,
            "合法身体护甲应通过 Definition graph 校验。"
        );

        template.BattleEquipmentEntries[0] = new EnemyBattleEquipmentJsonDto
        {
            SlotId = "body",
            ItemId = "battle_equipment_schema_armor",
            Rarity = 1,
            CurrentDurability = 85,
        };
        AssertDiagnostic(
            ValidateLocal(template),
            EnemyContentImportRules.ValueOutOfRange,
            "/battle_equipment_entries/0/current_durability",
            "uncommon 装备应拒绝超过 84 的初始耐久。"
        );

        template.BattleEquipmentEntries[0] = new EnemyBattleEquipmentJsonDto
        {
            SlotId = "body",
            ItemId = "battle_equipment_schema_armor",
            Rarity = 1,
            CurrentDurability = 84,
        };
        items.Remove("battle_equipment_schema_armor");
        AssertError(
            ValidateProjectedGraph(template, items),
            "references non-equipment item battle_equipment_schema_armor",
            "缺失 battle equipment item 应被 graph validator 拒绝。"
        );
    }

    private void TestSkillLevelMapUsesProjectedGraphSkillBounds()
    {
        TemplateDtoBuilder template = BuildValidTemplate(
            "skill_level_boundary_template",
            "skill_level_boundary_weapon"
        );
        template.SkillLevelMap["typed_schema_skill"] = 3;
        var items = new Dictionary<StringName, ItemDefinition>
        {
            ["skill_level_boundary_weapon"] = MakeWeapon(
                "skill_level_boundary_weapon",
                "skill_level_boundary_weapon_type"
            ),
        };
        AssertError(
            ValidateProjectedGraph(template, items),
            "skill typed_schema_skill level 3 is outside 1..2",
            "skill_level_map 应遵守 SkillDefinition.MaxLevel。"
        );
    }

    private static TemplateDtoBuilder BuildValidTemplate(
        string templateId,
        string weaponItemId
    )
    {
        var template = new TemplateDtoBuilder
        {
            TemplateId = templateId,
            DisplayName = templateId,
            BrainId = "dictionary_schema_brain",
            InitialStateId = "engage",
            CognitionKind = "sapient",
            AttackEquipmentItemId = weaponItemId,
        };
        template.SkillIds.Add("typed_schema_skill");
        template.SkillLevelMap["typed_schema_skill"] = 1;
        template.DropEntries.Add(
            new EnemyDropEntryJsonDto
            {
                DropEntryId = "typed_schema_drop",
                DropType = "item",
                ItemId = weaponItemId,
                Quantity = 1,
            }
        );
        return template;
    }

    private static IReadOnlyList<ContentJsonDiagnostic> ValidateLocal(
        TemplateDtoBuilder template
    ) =>
        EnemyContentImportValidator.ValidateTemplate(
            new JsonContentEntryContext(
                EnemyContentJsonDomains.TemplateDomainId,
                template.TemplateId,
                $"{template.TemplateId}.json",
                "/entries/0"
            ),
            template.Build()
        );

    private static EnemyTemplateDefinition Project(
        TemplateDtoBuilder template,
        IReadOnlyDictionary<StringName, ItemDefinition> items
    ) => EnemyContentDefinitionProjector.ProjectTemplate(template.Build(), items);

    private static IReadOnlyList<string> ValidateProjectedGraph(
        TemplateDtoBuilder template,
        IReadOnlyDictionary<StringName, ItemDefinition> items
    )
    {
        EnemyTemplateDefinition definition = Project(template, items);
        EnemyAiBrainDefinition brain = TestEnemyDefinitionFactory.Brain(
            "dictionary_schema_brain",
            "engage",
            TestEnemyDefinitionFactory.Wait("engage_wait")
        );
        var skills = new Dictionary<StringName, SkillDefinition>
        {
            ["typed_schema_skill"] = BuildSkillDefinition("typed_schema_skill", 2),
        };
        return EnemyContentRegistry.ValidateProjectedGraph(
            new Dictionary<StringName, EnemyTemplateDefinition>
            {
                [definition.TemplateId] = definition,
            },
            new Dictionary<StringName, EnemyAiBrainDefinition>
            {
                [brain.BrainId] = brain,
            },
            new Dictionary<StringName, WildEncounterRosterDefinition>(),
            new EnemyContentValidationContext(items, skills, "")
        );
    }

    private static SkillDefinition BuildSkillDefinition(string skillId, int maxLevel) =>
        TestSkillDefinitionProjection.BuildSkill(skillId, displayName: skillId, maxLevel: maxLevel);

    private static ItemDefinition MakeWeapon(StringName itemId, StringName weaponTypeId) =>
        MakeWeaponBuilder(itemId, weaponTypeId).ToDefinition();

    private static ItemDefinition MakeArmor(StringName itemId) =>
        new TestItemDefinitionBuilder
        {
            item_id = itemId,
            CategoryKind = ItemCategoryKind.Equipment,
            EquipmentTypeKind = ItemEquipmentTypeKind.Armor,
            equipment_slot_ids = new Godot.Collections.Array<string> { "body" },
            is_stackable = false,
            max_stack = 1,
        }.ToDefinition();

    private static TestItemDefinitionBuilder MakeWeaponBuilder(
        StringName itemId,
        StringName weaponTypeId
    )
    {
        return new TestItemDefinitionBuilder
        {
            item_id = itemId,
            CategoryKind = ItemCategoryKind.Equipment,
            EquipmentTypeKind = ItemEquipmentTypeKind.Weapon,
            equipment_slot_ids = new Godot.Collections.Array<string> { "main_hand" },
            is_stackable = false,
            max_stack = 1,
            weapon_profile = new TestWeaponProfileDefinitionBuilder
            {
                weapon_type_id = weaponTypeId,
                training_group = "martial",
                range_type = "melee",
                family = "sword",
                damage_tag = TestItemDefinitionBuilder.ToStringName(
                    WeaponPhysicalDamageTagKind.Slash
                ),
                attack_range = 1,
                one_handed_dice = new TestWeaponDamageDiceDefinitionBuilder
                {
                    dice_count = 1,
                    dice_sides = 6,
                    flat_bonus = 0,
                },
            },
        };
    }

    private void AssertDiagnostic(
        IEnumerable<ContentJsonDiagnostic> diagnostics,
        string ruleId,
        string pointerSuffix,
        string message
    )
    {
        if (
            diagnostics.Any(value =>
                value.RuleId == ruleId
                && value.JsonPointer.EndsWith(pointerSuffix, StringComparison.Ordinal)
            )
        )
        {
            return;
        }
        _test.Fail($"{message} diagnostics={FormatDiagnostics(diagnostics)}");
    }

    private void AssertError(IEnumerable<string> errors, string fragment, string message)
    {
        if (errors.Any(value => (value ?? "").Contains(fragment, StringComparison.Ordinal)))
            return;
        _test.Fail($"{message} errors={FormatErrors(errors)}");
    }

    private static string FormatDiagnostics(IEnumerable<ContentJsonDiagnostic> diagnostics) =>
        string.Join(
            " | ",
            diagnostics.Select(value => $"{value.RuleId}@{value.JsonPointer}: {value.Message}")
        );

    private static string FormatErrors(IEnumerable<string> errors) =>
        string.Join(" | ", errors);

    private sealed class TemplateDtoBuilder
    {
        internal string TemplateId { get; set; } = "";
        internal string DisplayName { get; set; } = "";
        internal string BattleSpriteAssetId { get; set; } = "";
        internal string BrainId { get; set; } = "";
        internal string InitialStateId { get; set; } = "";
        internal int EnemyCount { get; set; } = 1;
        internal int BodySize { get; set; } = BattleUnitState.BodySizeMedium;
        internal int CreatureLevel { get; set; } = 1;
        internal int HitDieSides { get; set; } = 8;
        internal string CognitionKind { get; set; } = "sapient";
        internal List<string> Tags { get; } = new();
        internal List<string> SaveAdvantageTags { get; } = new();
        internal List<string> SaveDisadvantageTags { get; } = new();
        internal List<string> SaveImmunityTags { get; } = new();
        internal Dictionary<string, string> DamageResistances { get; } = new();
        internal string AttackEquipmentItemId { get; set; } = "";
        internal List<EnemyBattleEquipmentJsonDto> BattleEquipmentEntries { get; } = new();
        internal string NaturalWeaponDamageTag { get; set; } = "";
        internal int NaturalWeaponAttackRange { get; set; } = 1;
        internal Dictionary<string, int> BaseAttributeOverrides { get; } = new()
        {
            ["strength"] = 10,
            ["agility"] = 10,
            ["constitution"] = 10,
            ["perception"] = 10,
            ["intelligence"] = 10,
            ["willpower"] = 10,
        };
        internal List<string> SkillIds { get; } = new();
        internal Dictionary<string, int> SkillLevelMap { get; } = new();
        internal int GeneratedCoreSkillCount { get; set; }
        internal Dictionary<string, int> AttributeOverrides { get; } = new();
        internal string TargetRank { get; set; } = "normal";
        internal List<EnemyDropEntryJsonDto> DropEntries { get; } = new();

        internal EnemyTemplateJsonDto Build() =>
            new()
            {
                TemplateId = TemplateId,
                DisplayName = DisplayName,
                BattleSpriteAssetId = BattleSpriteAssetId,
                BrainId = BrainId,
                InitialStateId = InitialStateId,
                EnemyCount = EnemyCount,
                BodySize = BodySize,
                CreatureLevel = CreatureLevel,
                HitDieSides = HitDieSides,
                CognitionKind = CognitionKind,
                Tags = Tags,
                SaveAdvantageTags = SaveAdvantageTags,
                SaveDisadvantageTags = SaveDisadvantageTags,
                SaveImmunityTags = SaveImmunityTags,
                DamageResistances = DamageResistances,
                AttackEquipmentItemId = AttackEquipmentItemId,
                BattleEquipmentEntries = BattleEquipmentEntries,
                NaturalWeaponDamageTag = NaturalWeaponDamageTag,
                NaturalWeaponAttackRange = NaturalWeaponAttackRange,
                BaseAttributeOverrides = BaseAttributeOverrides,
                SkillIds = SkillIds,
                SkillLevelMap = SkillLevelMap,
                GeneratedCoreSkillCount = GeneratedCoreSkillCount,
                AttributeOverrides = AttributeOverrides,
                TargetRank = TargetRank,
                DropEntries = DropEntries,
            };
    }
}
