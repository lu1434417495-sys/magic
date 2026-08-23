using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_enemy_template_schema_boundary_regression : LifecycleTestSceneTree
{
    private const string SaveAdvantageRoundTripPath =
        "user://enemy_template_save_advantage_tags_roundtrip_regression.tres";
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestTypedSchemaValidationAcceptsTypedReferenceTables();
        TestDictionaryReferenceIndicesBuildTypedSchemaInputsFromStringNameKeys();
        TestTypedSchemaValidationRejectsMissingTypedItemReferences();
        TestCognitionKindIsRequiredAndClosed();
        TestSaveAdvantageTagsSurviveResourceRoundTrip();
        TestSaveTagFieldsAcceptBareTagsAndRejectSuffixes();
        TestSaveAdvantageTagsRejectEmptyTag();
        TestSaveAdvantageTagsRejectUnsupportedBaseTag();
        TestSaveAdvantageTagsRejectDuplicateTag();
        TestDamageResistancesAcceptSupportedTagsAndTiers();
        TestDamageResistancesRejectUnsupportedDamageTag();
        TestDamageResistancesRejectUnsupportedMitigationTier();
        TestDerivedHpAndAttackBonusFollowLevelFormula();
        TestCreatureLevelAndHitDieValidation();
        TestBattleEquipmentEntriesRequireTypedEquipmentAndValidDurability();
        TestSkillLevelMapValidationRemainsUnchanged();

        RequestTestExit(_test.Finish("Enemy template schema boundary regression"));
    }

    private void TestBattleEquipmentEntriesRequireTypedEquipmentAndValidDurability()
    {
        EnemyTemplateDef template = BuildValidTemplate(
            "battle_equipment_schema_template",
            "battle_equipment_schema_weapon"
        );
        template.battle_equipment_entries.Add(
            new EnemyBattleEquipmentDef
            {
                slot_id = "body",
                item_id = "battle_equipment_schema_armor",
                rarity = 1,
                current_durability = 84,
            }
        );
        var brainIndex = new Dictionary<StringName, EnemyAiBrainDef>
        {
            [template.brain_id] = BuildBrain(template.brain_id, template.initial_state_id),
        };
        var itemDefinitions = new Dictionary<StringName, ItemDefinition>
        {
            [template.attack_equipment_item_id] = MakeWeapon(
                template.attack_equipment_item_id,
                "battle_equipment_schema_weapon_type"
            ),
            ["battle_equipment_schema_armor"] = MakeArmor(
                "battle_equipment_schema_armor"
            ),
        };
        var skillDefinitions = new Dictionary<StringName, SkillDefinition>
        {
            ["typed_schema_skill"] = BuildSkillDefinition("typed_schema_skill", maxLevel: 2),
        };

        GStringArray validErrors = template.ValidateSchemaTyped(
            brainIndex,
            itemDefinitions,
            skillDefinitions
        );
        _test.Eq(
            validErrors.Count,
            0,
            $"typed敌方战斗装备应接受合法身体护甲与对应稀有度耐久。errors={FormatErrors(validErrors)}"
        );

        template.battle_equipment_entries[0].current_durability = 85;
        GStringArray durabilityErrors = template.ValidateSchemaTyped(
            brainIndex,
            itemDefinitions,
            skillDefinitions
        );
        _test.True(
            ContainsError(durabilityErrors, "current_durability must be within 1..84"),
            $"uncommon敌方装备应拒绝超过84的初始耐久。errors={FormatErrors(durabilityErrors)}"
        );

        template.battle_equipment_entries[0].current_durability = 84;
        itemDefinitions.Remove("battle_equipment_schema_armor");
        GStringArray missingItemErrors = template.ValidateSchemaTyped(
            brainIndex,
            itemDefinitions,
            skillDefinitions
        );
        _test.True(
            ContainsError(missingItemErrors, "must reference equipment content"),
            $"敌方战斗装备应拒绝缺失的item定义。errors={FormatErrors(missingItemErrors)}"
        );
    }

    private void TestTypedSchemaValidationAcceptsTypedReferenceTables()
    {
        EnemyTemplateDef template = BuildValidTemplate("typed_schema_template", "typed_schema_weapon");
        var brainIndex = new Dictionary<StringName, EnemyAiBrainDef>
        {
            [template.brain_id] = BuildBrain(template.brain_id, template.initial_state_id),
        };
        var itemDefinitionIndex = new Dictionary<StringName, ItemDefinition>
        {
            [template.attack_equipment_item_id] = MakeWeapon(
                template.attack_equipment_item_id,
                "typed_schema_weapon_type"
            ),
        };
        var skillDefinitionIndex = new Dictionary<StringName, SkillDefinition>
        {
            ["typed_schema_skill"] = BuildSkillDefinition("typed_schema_skill", maxLevel: 2),
        };

        GStringArray errors = template.ValidateSchemaTyped(
            brainIndex,
            itemDefinitionIndex,
            skillDefinitionIndex
        );
        _test.True(
            errors.Count == 0,
            $"typed ValidateSchemaTyped() 应接受正式 typed 引用表。 errors={FormatErrors(errors)}"
        );
    }

    private void TestCognitionKindIsRequiredAndClosed()
    {
        EnemyTemplateDef template = BuildValidTemplate(
            "cognition_schema_template",
            "cognition_schema_weapon"
        );
        template.cognition_kind = "";
        GStringArray missingErrors = ValidateWithReferenceTables(template);
        _test.True(
            ContainsError(missingErrors, "cognition_kind"),
            "enemy template 必须显式声明 cognition_kind。"
        );

        template.cognition_kind = "clever";
        GStringArray unknownErrors = ValidateWithReferenceTables(template);
        _test.True(
            ContainsError(unknownErrors, "cognition_kind"),
            "enemy template cognition_kind 应拒绝开放字符串。"
        );

        template.cognition_kind = "mindless";
        _test.Eq(
            ValidateWithReferenceTables(template).Count,
            0,
            "mindless 应是合法的正式认知类型。"
        );
        template.cognition_kind = "instinctive";
        _test.Eq(
            ValidateWithReferenceTables(template).Count,
            0,
            "instinctive 应是合法的正式认知类型。"
        );
        template.cognition_kind = "sapient";
        _test.Eq(
            ValidateWithReferenceTables(template).Count,
            0,
            "sapient 应是合法的正式认知类型。"
        );
    }

    private void TestDictionaryReferenceIndicesBuildTypedSchemaInputsFromStringNameKeys()
    {
        EnemyTemplateDef template = BuildValidTemplate(
            "dictionary_schema_template",
            "dictionary_schema_weapon"
        );
        GDictionary knownBrains = new()
        {
            [new StringName("dictionary_schema_brain")] = BuildBrain("dictionary_schema_brain", "engage"),
        };
        var itemDefinitions = new Dictionary<StringName, ItemDefinition>
        {
            ["dictionary_schema_weapon"] = MakeWeapon(
                "dictionary_schema_weapon",
                "dictionary_schema_weapon_type"
            ),
        };
        GDictionary skillDefs = new()
        {
            [new StringName("typed_schema_skill")] = BuildSkill("typed_schema_skill", maxLevel: 2),
        };

        GStringArray errors = template.ValidateSchemaTyped(
            EnemyTemplateDef.BuildBrainIndex(knownBrains),
            itemDefinitions,
            BuildSkillDefinitionIndex(skillDefs)
        );
        _test.True(
            errors.Count == 0,
            $"typed ValidateSchemaTyped() 应接受 StringName-key 的正式 item definition 索引。 errors={FormatErrors(errors)}"
        );
    }

    private static Dictionary<StringName, SkillDefinition> BuildSkillDefinitionIndex(
        GDictionary skillDefs
    )
    {
        var result = new Dictionary<StringName, SkillDefinition>();
        if (skillDefs == null)
            return result;
        foreach (Variant rawKey in skillDefs.Keys)
        {
            if (rawKey.VariantType != Variant.Type.StringName)
                continue;
            SkillDefinition skillDefinition =
                SkillDefinition.FromDiagnosticFixture(skillDefs[rawKey].As<SkillDef>());
            if (skillDefinition == null)
                continue;
            StringName keySkillId = rawKey.AsStringName();
            if (keySkillId != "")
                result[keySkillId] = skillDefinition;
        }
        return result;
    }

    private void TestTypedSchemaValidationRejectsMissingTypedItemReferences()
    {
        EnemyTemplateDef template = BuildValidTemplate(
            "missing_item_schema_template",
            "missing_item_schema_weapon"
        );
        template.drop_entries.Clear();
        template.drop_entries.Add(
            new DropEntryDef
            {
                drop_entry_id = "missing_drop",
                drop_type = "item",
                item_id = "missing_drop_item",
                quantity = 1,
            }
        );

        var brainIndex = new Dictionary<StringName, EnemyAiBrainDef>
        {
            [template.brain_id] = BuildBrain(template.brain_id, template.initial_state_id),
        };
        var skillDefinitionIndex = new Dictionary<StringName, SkillDefinition>
        {
            ["typed_schema_skill"] = BuildSkillDefinition("typed_schema_skill", maxLevel: 2),
        };

        GStringArray errors = template.ValidateSchemaTyped(
            brainIndex,
            new Dictionary<StringName, ItemDefinition>(),
            skillDefinitionIndex
        );
        _test.Eq(
            errors.Count,
            2,
            $"缺失 item fixture 应只报告装备与掉落两条引用错误。 errors={FormatErrors(errors)}"
        );
        _test.True(
            ContainsError(
                errors,
                "Enemy template missing_item_schema_template references missing attack_equipment_item_id missing_item_schema_weapon."
            ),
            $"应精确报告缺失攻击装备 missing_item_schema_weapon。 errors={FormatErrors(errors)}"
        );
        _test.True(
            ContainsError(
                errors,
                "Enemy template missing_item_schema_template drop missing_drop references missing item_id missing_drop_item."
            ),
            $"应精确报告 missing_drop 的缺失 item missing_drop_item。 errors={FormatErrors(errors)}"
        );
    }

    private void TestSaveAdvantageTagsSurviveResourceRoundTrip()
    {
        using EnemyTemplateDef template = BuildValidTemplate(
            "save_advantage_projection_template",
            "save_advantage_projection_weapon"
        );
        template.save_advantage_tags = new GStringNameArray { "illusion", "poison" };
        EnemyTemplateDefinition definition = template.ToDefinition(
            new Dictionary<StringName, ItemDefinition>()
        );
        _test.Eq(definition.SaveAdvantageTags.Count, 2, "save_advantage_tags 应完整投影到 immutable Definition。");
        _test.Eq(definition.SaveAdvantageTags[0], new StringName("illusion"), "save_advantage_tags 应保留第一项。");
        _test.Eq(definition.SaveAdvantageTags[1], new StringName("poison"), "save_advantage_tags 应保留第二项。");
    }

    private void TestSaveTagFieldsAcceptBareTagsAndRejectSuffixes()
    {
        EnemyTemplateDef template = BuildValidTemplate(
            "save_tag_schema_template",
            "save_tag_schema_weapon"
        );
        SetSaveAdvantageTags(template, "illusion");
        template.save_disadvantage_tags = new GStringNameArray { "frightened" };
        template.save_immunity_tags = new GStringNameArray { "sleep", "poison" };

        GStringArray errors = ValidateWithReferenceTables(template);
        _test.True(
            errors.Count == 0,
            $"三个豁免标签字段应各自接受裸 save tag。 errors={FormatErrors(errors)}"
        );

        EnemyTemplateDef suffixTemplate = BuildValidTemplate(
            "save_tag_suffix_schema_template",
            "save_tag_suffix_schema_weapon"
        );
        SetSaveAdvantageTags(suffixTemplate, "illusion_immunity");
        GStringArray suffixErrors = ValidateWithReferenceTables(suffixTemplate);
        _test.True(
            ContainsError(suffixErrors, "removed suffix"),
            $"后缀写法已废除,应被 schema 拒绝并提示迁移。 errors={FormatErrors(suffixErrors)}"
        );
    }

    private void TestSaveAdvantageTagsRejectEmptyTag()
    {
        EnemyTemplateDef template = BuildValidTemplate(
            "empty_save_tag_schema_template",
            "empty_save_tag_schema_weapon"
        );
        SetSaveAdvantageTags(template, "");

        GStringArray errors = ValidateWithReferenceTables(template);
        _test.True(
            ContainsError(errors, "save_advantage_tags"),
            $"save_advantage_tags 空元素应被 schema 拒绝。 errors={FormatErrors(errors)}"
        );
    }

    private void TestSaveAdvantageTagsRejectUnsupportedBaseTag()
    {
        EnemyTemplateDef template = BuildValidTemplate(
            "unsupported_save_tag_schema_template",
            "unsupported_save_tag_schema_weapon"
        );
        SetSaveAdvantageTags(template, "not_a_save_tag");

        GStringArray errors = ValidateWithReferenceTables(template);
        _test.True(
            ContainsError(errors, "not_a_save_tag"),
            $"save_advantage_tags 应拒绝不在豁免标签枚举内的裸标签。 errors={FormatErrors(errors)}"
        );
    }

    private void TestSaveAdvantageTagsRejectDuplicateTag()
    {
        EnemyTemplateDef template = BuildValidTemplate(
            "duplicate_save_tag_schema_template",
            "duplicate_save_tag_schema_weapon"
        );
        template.save_advantage_tags = new GStringNameArray { "poison", "poison" };

        GStringArray errors = ValidateWithReferenceTables(template);
        _test.True(
            ContainsError(errors, "duplicates save tag poison"),
            $"save_advantage_tags 应拒绝重复标签。 errors={FormatErrors(errors)}"
        );
    }

    private void TestDamageResistancesAcceptSupportedTagsAndTiers()
    {
        EnemyTemplateDef template = BuildValidTemplate(
            "damage_resist_schema_template",
            "damage_resist_schema_weapon"
        );
        template.damage_resistances = new GDictionary
        {
            [new StringName("physical_pierce")] = new StringName("half"),
            [new StringName("fire")] = new StringName("double"),
            [new StringName("freeze")] = new StringName("immune"),
            [new StringName("magic")] = new StringName("normal"),
        };

        GStringArray errors = ValidateWithReferenceTables(template);
        _test.True(
            errors.Count == 0,
            $"damage_resistances 应接受合法伤害标签与 mitigation tier。 errors={FormatErrors(errors)}"
        );

        IReadOnlyDictionary<StringName, StringName> typed = template.GetDamageResistancesTyped();
        _test.True(
            typed.Count == 4
                && typed[new StringName("physical_pierce")] == new StringName("half")
                && typed[new StringName("fire")] == new StringName("double"),
            "GetDamageResistancesTyped() 应完整投影合法条目。"
        );
    }

    private void TestDamageResistancesRejectUnsupportedDamageTag()
    {
        EnemyTemplateDef template = BuildValidTemplate(
            "damage_resist_bad_tag_template",
            "damage_resist_bad_tag_weapon"
        );
        template.damage_resistances = new GDictionary
        {
            [new StringName("shadow")] = new StringName("half"),
        };

        GStringArray errors = ValidateWithReferenceTables(template);
        _test.True(
            ContainsError(errors, "shadow"),
            $"damage_resistances 应拒绝未知伤害标签。 errors={FormatErrors(errors)}"
        );
    }

    private void TestDamageResistancesRejectUnsupportedMitigationTier()
    {
        EnemyTemplateDef template = BuildValidTemplate(
            "damage_resist_bad_tier_template",
            "damage_resist_bad_tier_weapon"
        );
        template.damage_resistances = new GDictionary
        {
            [new StringName("fire")] = new StringName("quarter"),
        };

        GStringArray errors = ValidateWithReferenceTables(template);
        _test.True(
            ContainsError(errors, "quarter"),
            $"damage_resistances 应拒绝未知 mitigation tier。 errors={FormatErrors(errors)}"
        );
    }

    private void TestDerivedHpAndAttackBonusFollowLevelFormula()
    {
        EnemyTemplateDef template = BuildValidTemplate(
            "formula_schema_template",
            "formula_schema_weapon"
        );
        template.creature_level = 10;
        template.hit_die_sides = 12;
        template.body_size = BattleUnitState.BodySizeLarge;
        template.base_attribute_overrides[new StringName("strength")] = 18;
        template.base_attribute_overrides[new StringName("constitution")] = 16;

        _test.Eq(
            template.GetDerivedHpMaxTyped(),
            520,
            "派生 HP 应为 首级取骰面最大值 (12+6) + 后9级 × (d12均值6.5 + 体质修正3×2)，向下取整后 × 2x2占位4格 = 520。"
        );

        var itemDefinitionIndex = new Dictionary<StringName, ItemDefinition>
        {
            [template.attack_equipment_item_id] = MakeWeapon(
                template.attack_equipment_item_id,
                "formula_schema_weapon_type"
            ),
        };
        _test.Eq(
            template.GetDerivedAttackBonusTyped(itemDefinitionIndex),
            4,
            "近战武器的派生攻击加值应等于力量修正 (18 → +4)。"
        );

        GStringArray errors = ValidateWithReferenceTables(template);
        _test.True(
            errors.Count == 0,
            $"声明 creature_level/hit_die_sides 的模板应通过 schema 校验。 errors={FormatErrors(errors)}"
        );

        EnemyTemplateDef rangedTemplate = BuildValidTemplate(
            "formula_schema_ranged_template",
            "formula_schema_ranged_weapon"
        );
        rangedTemplate.tags = new GStringNameArray { "beast" };
        rangedTemplate.natural_weapon_damage_tag = "physical_pierce";
        rangedTemplate.natural_weapon_attack_range = 5;
        rangedTemplate.base_attribute_overrides[new StringName("perception")] = 12;
        _test.Eq(
            rangedTemplate.GetDerivedAttackBonusTyped(
                new Dictionary<StringName, ItemDefinition>()
            ),
            1,
            "远程(攻击范围>2)天生武器的派生攻击加值应等于感知修正 (12 → +1)。"
        );
    }

    private void TestCreatureLevelAndHitDieValidation()
    {
        EnemyTemplateDef levelTemplate = BuildValidTemplate(
            "bad_level_schema_template",
            "bad_level_schema_weapon"
        );
        levelTemplate.creature_level = -1;
        GStringArray levelErrors = ValidateWithReferenceTables(levelTemplate);
        _test.True(
            ContainsError(levelErrors, "creature_level"),
            $"creature_level < 0 应被 schema 拒绝。 errors={FormatErrors(levelErrors)}"
        );

        EnemyTemplateDef zeroLevelTemplate = BuildValidTemplate(
            "zero_level_schema_template",
            "zero_level_schema_weapon"
        );
        zeroLevelTemplate.creature_level = 0;
        zeroLevelTemplate.hit_die_sides = 8;
        zeroLevelTemplate.base_attribute_overrides[new StringName("constitution")] = 14;
        GStringArray zeroLevelErrors = ValidateWithReferenceTables(zeroLevelTemplate);
        _test.True(
            zeroLevelErrors.Count == 0,
            $"creature_level = 0 应是合法的杂兽等级。 errors={FormatErrors(zeroLevelErrors)}"
        );
        _test.Eq(
            zeroLevelTemplate.GetDerivedHpMaxTyped(),
            12,
            "0 级生物同样享受首级满骰底子：d8满骰8 + 体质修正2×2 = 12。"
        );

        EnemyTemplateDef dieTemplate = BuildValidTemplate(
            "bad_die_schema_template",
            "bad_die_schema_weapon"
        );
        dieTemplate.hit_die_sides = 7;
        GStringArray dieErrors = ValidateWithReferenceTables(dieTemplate);
        _test.True(
            ContainsError(dieErrors, "hit_die_sides"),
            $"非法生命骰面数应被 schema 拒绝。 errors={FormatErrors(dieErrors)}"
        );
    }

    private void TestSkillLevelMapValidationRemainsUnchanged()
    {
        EnemyTemplateDef template = BuildValidTemplate(
            "skill_level_boundary_template",
            "skill_level_boundary_weapon"
        );
        template.skill_level_map[new StringName("typed_schema_skill")] = 3;

        GStringArray errors = ValidateWithReferenceTables(template);
        _test.True(
            ContainsError(errors, "skill_level_map[typed_schema_skill]"),
            $"新增 save_advantage_tags 校验不应改变 skill_level_map 上限校验。 errors={FormatErrors(errors)}"
        );
    }

    private static EnemyTemplateDef BuildValidTemplate(StringName templateId, StringName weaponItemId)
    {
        var template = new EnemyTemplateDef
        {
            template_id = templateId,
            display_name = templateId.ToString(),
            brain_id = "dictionary_schema_brain",
            initial_state_id = "engage",
            cognition_kind = "sapient",
            attack_equipment_item_id = weaponItemId,
            skill_ids = new GStringNameArray { "typed_schema_skill" },
            skill_level_map = new GDictionary { [new StringName("typed_schema_skill")] = 1 },
            base_attribute_overrides = new GDictionary
            {
                [new StringName("strength")] = 10,
                [new StringName("agility")] = 10,
                [new StringName("constitution")] = 10,
                [new StringName("perception")] = 10,
                [new StringName("intelligence")] = 10,
                [new StringName("willpower")] = 10,
            },
        };
        template.drop_entries.Add(
            new DropEntryDef
            {
                drop_entry_id = "typed_schema_drop",
                drop_type = "item",
                item_id = weaponItemId,
                quantity = 1,
            }
        );
        return template;
    }

    private static EnemyAiBrainDef BuildBrain(StringName brainId, StringName stateId)
    {
        return TestResourceOwnership.Own(
            new EnemyAiBrainDef
            {
                brain_id = brainId,
                default_state_id = stateId,
                states = new Godot.Collections.Array<EnemyAiStateDef>
                {
                    new EnemyAiStateDef
                    {
                        state_id = stateId,
                        actions = new Godot.Collections.Array<EnemyAiAction>
                        {
                            new WaitAction { action_id = $"{stateId}_wait" },
                        },
                    },
                },
            },
            "EnemyTemplateSchemaBoundary.BuildBrain"
        );
    }

    private static SkillDef BuildSkill(StringName skillId, int maxLevel)
    {
        return TestResourceOwnership.Own(
            new SkillDef
            {
                skill_id = skillId,
                display_name = skillId.ToString(),
                max_level = maxLevel,
            },
            "EnemyTemplateSchemaBoundary.BuildSkill"
        );
    }

    private static SkillDefinition BuildSkillDefinition(StringName skillId, int maxLevel) =>
        TestSkillDefinitionProjection.BuildSkill(skillId, displayName: skillId.ToString(), maxLevel: maxLevel);

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

    private static TestItemDefinitionBuilder MakeWeaponBuilder(StringName itemId, StringName weaponTypeId)
    {
        var itemDef = new TestItemDefinitionBuilder
        {
            item_id = itemId,
            CategoryKind = ItemCategoryKind.Equipment,
            EquipmentTypeKind = ItemEquipmentTypeKind.Weapon,
            equipment_slot_ids = new Godot.Collections.Array<string> { "main_hand" },
            is_stackable = false,
            max_stack = 1,
        };
        itemDef.weapon_profile = new TestWeaponProfileDefinitionBuilder
        {
            weapon_type_id = weaponTypeId,
            training_group = "martial",
            range_type = "melee",
            family = "sword",
            damage_tag = TestItemDefinitionBuilder.ToStringName(WeaponPhysicalDamageTagKind.Slash),
            attack_range = 1,
            one_handed_dice = new TestWeaponDamageDiceDefinitionBuilder
            {
                dice_count = 1,
                dice_sides = 6,
                flat_bonus = 0,
            },
        };
        return itemDef;
    }

    private static GStringArray ValidateWithReferenceTables(EnemyTemplateDef template)
    {
        var brainIndex = new Dictionary<StringName, EnemyAiBrainDef>
        {
            [template.brain_id] = BuildBrain(template.brain_id, template.initial_state_id),
        };
        var itemDefinitionIndex = new Dictionary<StringName, ItemDefinition>
        {
            [template.attack_equipment_item_id] = MakeWeapon(
                template.attack_equipment_item_id,
                $"{template.attack_equipment_item_id}_type"
            ),
        };
        var skillDefinitionIndex = new Dictionary<StringName, SkillDefinition>
        {
            ["typed_schema_skill"] = BuildSkillDefinition("typed_schema_skill", maxLevel: 2),
        };
        return template.ValidateSchemaTyped(
            brainIndex,
            itemDefinitionIndex,
            skillDefinitionIndex
        );
    }

    private static void SetSaveAdvantageTags(
        EnemyTemplateDef template,
        params StringName[] saveAdvantageTags
    )
    {
        var tags = new GStringNameArray();
        foreach (StringName tag in saveAdvantageTags ?? Array.Empty<StringName>())
        {
            tags.Add(tag);
        }

        template.save_advantage_tags = tags;
    }

    private static void CleanupFile(string virtualPath)
    {
        string absolutePath = ProjectSettings.GlobalizePath(virtualPath);
        if (Godot.FileAccess.FileExists(absolutePath))
        {
            DirAccess.RemoveAbsolute(absolutePath);
        }
    }

    private static bool ContainsError(GStringArray errors, string fragment)
    {
        foreach (string error in errors)
        {
            if ((error ?? "").Contains(fragment, StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    private static string FormatErrors(GStringArray errors) => string.Join(" | ", errors);

}
