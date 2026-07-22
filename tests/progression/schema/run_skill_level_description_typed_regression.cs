using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_skill_level_description_typed_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestLevelDescriptionSchemaValidationUsesTypedEntries();
        TestLevelDescriptionFormatterUsesTypedConfigs();
        TestLevelDescriptionFormatterUsesTypedEffectParameters();

        RequestTestExit(_test.Finish("Skill level description typed regression"));
    }

    private void TestLevelDescriptionSchemaValidationUsesTypedEntries()
    {
        SkillDef validSkill = new()
        {
            skill_id = "valid_level_description_skill",
            level_description_template = "模板{value}",
            max_level = 1,
            level_description_configs = new GDictionary
            {
                ["0"] = new GDictionary { ["value"] = "零级" },
                ["1"] = new GDictionary { ["value"] = "一级" },
            },
        };
        List<string> validErrors = SkillLevelDescriptionContentRules.CollectValidationErrors(
            validSkill.skill_id,
            validSkill
        );
        _test.Eq(validErrors.Count, 0, "合法 level_description_configs 应通过 typed schema 校验。");

        SkillDef validLiteralExpressionSkill = new()
        {
            skill_id = "valid_literal_level_description_expression_skill",
            level_description_template = "模板{=1 + 2}",
            max_level = 0,
            level_description_configs = new GDictionary { ["0"] = new GDictionary() },
        };
        List<string> validLiteralExpressionErrors =
            SkillLevelDescriptionContentRules.CollectValidationErrors(
                validLiteralExpressionSkill.skill_id,
                validLiteralExpressionSkill
            );
        _test.Eq(
            validLiteralExpressionErrors.Count,
            0,
            "合法字面量表达式应通过加载期语法校验。"
        );

        SkillDef validRuntimeExpressionSkill = new()
        {
            skill_id = "valid_runtime_level_description_expression_skill",
            level_description_template = "模板{=con_mod + 1}",
            max_level = 0,
            level_description_configs = new GDictionary { ["0"] = new GDictionary() },
        };
        List<string> validRuntimeExpressionErrors =
            SkillLevelDescriptionContentRules.CollectValidationErrors(
                validRuntimeExpressionSkill.skill_id,
                validRuntimeExpressionSkill
            );
        _test.Eq(
            validRuntimeExpressionErrors.Count,
            0,
            "加载期语法校验不应因运行时变量尚未绑定而拒绝表达式。"
        );

        SkillDef invalidExpressionSkill = new()
        {
            skill_id = "invalid_level_description_expression_skill",
            level_description_template = "模板{=(}",
            max_level = 0,
            level_description_configs = new GDictionary { ["0"] = new GDictionary() },
        };
        List<string> invalidExpressionErrors =
            SkillLevelDescriptionContentRules.CollectValidationErrors(
                invalidExpressionSkill.skill_id,
                invalidExpressionSkill
            );
        _test.True(
            invalidExpressionErrors.Exists(error => error.Contains("expression '{=(}' is invalid")),
            $"非法表达式应在内容加载期被拒绝。实际错误：{string.Join(" | ", invalidExpressionErrors)}"
        );

        SkillDef emptyExpressionSkill = new()
        {
            skill_id = "empty_level_description_expression_skill",
            level_description_template = "模板{=}",
            max_level = 0,
            level_description_configs = new GDictionary { ["0"] = new GDictionary() },
        };
        List<string> emptyExpressionErrors =
            SkillLevelDescriptionContentRules.CollectValidationErrors(
                emptyExpressionSkill.skill_id,
                emptyExpressionSkill
            );
        _test.True(
            emptyExpressionErrors.Exists(error => error.Contains("must not be empty")),
            "空表达式应在内容加载期被拒绝。"
        );

        SkillDef unclosedExpressionSkill = new()
        {
            skill_id = "unclosed_level_description_expression_skill",
            level_description_template = "模板{=1 + 2",
            max_level = 0,
            level_description_configs = new GDictionary { ["0"] = new GDictionary() },
        };
        List<string> unclosedExpressionErrors =
            SkillLevelDescriptionContentRules.CollectValidationErrors(
                unclosedExpressionSkill.skill_id,
                unclosedExpressionSkill
            );
        _test.True(
            unclosedExpressionErrors.Exists(error => error.Contains("missing a closing brace")),
            "未闭合表达式应在内容加载期被拒绝。"
        );

        SkillDef invalidIntKeySkill = new()
        {
            skill_id = "invalid_level_description_int_key_skill",
            level_description_template = "模板{value}",
            max_level = 1,
            level_description_configs = new GDictionary
            {
                [0] = new GDictionary { ["value"] = "零级" },
            },
        };
        List<string> invalidIntKeyErrors =
            SkillLevelDescriptionContentRules.CollectValidationErrors(
                invalidIntKeySkill.skill_id,
                invalidIntKeySkill
            );
        _test.True(
            invalidIntKeyErrors.Count > 0,
            "level_description_configs int key 应被 typed schema entry 拒绝。"
        );

        SkillDef invalidShapeSkill = new()
        {
            skill_id = "invalid_level_description_shape_skill",
            level_description_template = "模板{value}",
            max_level = 1,
            level_description_configs = new GDictionary
            {
                ["0"] = new GDictionary { ["value"] = "零级" },
                ["2"] = "旧格式",
            },
        };
        List<string> invalidErrors = SkillLevelDescriptionContentRules.CollectValidationErrors(
            invalidShapeSkill.skill_id,
            invalidShapeSkill
        );
        _test.True(
            invalidErrors.Count >= 3,
            "非法 level_description_configs shape 应保持非法。"
        );
    }

    private void TestLevelDescriptionFormatterUsesTypedConfigs()
    {
        SkillDefinition skill = BuildSkillDefinition(
            "typed_level_description_formatter_skill",
            "模板{value}{{?bonus}}+{bonus}{{/bonus}}",
            levelDescriptionConfigs: new Dictionary<int, IReadOnlyDictionary<string, object>>
            {
                [0] = new Dictionary<string, object> { ["value"] = "零级" },
                [1] = new Dictionary<string, object>
                {
                    ["value"] = "一级",
                    ["bonus"] = 2,
                },
            }
        );

        _test.Eq(
            SkillLevelDescriptionFormatter.BuildLevelDescription(skill, 0, new GDictionary()),
            "模板零级",
            "formatter 应从 typed level description config 读取 0 级描述。"
        );
        _test.Eq(
            SkillLevelDescriptionFormatter.BuildLevelDescription(skill, 1, new GDictionary()),
            "模板一级+2",
            "formatter 应从 typed level description config 读取 1 级描述。"
        );
    }

    private void TestLevelDescriptionFormatterUsesTypedEffectParameters()
    {
        SkillDefinition skill = BuildSkillDefinition(
            "typed_level_description_effect_params_skill",
            "连锁半径{base_chain_radius}，湿地{wet_chain_radius}",
            combatProfile: BuildCombatProfile(
                "typed_level_description_effect_params_skill",
                new CombatEffectDefinition(
                    effectType: "chain_damage",
                    effectTargetTeamFilter: "",
                    statusId: "",
                    saveFailureStatusId: "",
                    terrainEffectId: "",
                    terrainReplaceTo: "",
                    heightDelta: 0,
                    requiresWeapon: false,
                    addWeaponDice: false,
                    preventRepeatTarget: true,
                    forcedMoveMode: "",
                    minSkillLevel: 0,
                    maxSkillLevel: -1,
                    damageTag: "",
                    damageRatioPercent: 100,
                    preResistanceDamageMultiplier: 1.0,
                    bonusCondition: "",
                    hpRatioThresholdPercent: 0,
                    damageCategory: "",
                    drBypassTag: "",
                    diceCount: 0,
                    diceSides: 0,
                    diceBonus: 0,
                    bonusDamageDiceCount: 0,
                    bonusDamageDiceSides: 0,
                    bonusDamageDiceBonus: 0,
                    saveDc: 0,
                    saveDcMode: "",
                    saveDcSourceAbility: "",
                    saveAbility: "",
                    savePartialOnSuccess: false,
                    saveTag: "",
                    thresholdBaseValue: 0,
                    thresholdLevelAnchor: 17,
                    thresholdLevelBonusPerDelta: 5,
                    thresholdMaxHpRatioPercent: 20,
                    thresholdCapMaxHpRatioPercent: 50,
                    soulFractureDurationTu: 0,
                    healMultiplierPercent: 100,
                    shieldGainMultiplierPercent: 100,
                    appliedStatusDurationTu: 0,
                    durationTu: 0,
                    tickIntervalTu: 0,
                    effectTags: System.Array.Empty<StringName>(),
                    parameters: new Dictionary<string, object>
                    {
                        ["base_chain_radius"] = 1,
                        ["wet_chain_radius"] = 2,
                    }
                )
            )
        );

        _test.Eq(
            SkillLevelDescriptionFormatter.BuildLevelDescription(skill, 0, new GDictionary()),
            "连锁半径1，湿地2",
            "formatter 应从纯 SkillDefinition effect parameters 渲染描述。"
        );
    }

    private static SkillDefinition BuildSkillDefinition(
        StringName skillId,
        string levelDescriptionTemplate,
        IReadOnlyDictionary<int, IReadOnlyDictionary<string, object>> levelDescriptionConfigs = null,
        CombatSkillDefinition combatProfile = null
    )
    {
        return new SkillDefinition(
            skillId: skillId,
            displayName: (string)skillId,
            iconId: skillId,
            description: "",
            skillType: "active",
            maxLevel: 1,
            nonCoreMaxLevel: 0,
            dynamicMaxLevelStatId: "",
            dynamicMaxLevelBase: 0,
            dynamicMaxLevelPerStat: 0,
            masteryCurve: System.Array.Empty<int>(),
            tags: System.Array.Empty<StringName>(),
            learnSource: "book",
            learnRequirements: System.Array.Empty<StringName>(),
            unlockMode: "",
            knowledgeRequirements: System.Array.Empty<StringName>(),
            skillLevelRequirements: new Dictionary<StringName, int>(),
            attributeRequirements: new Dictionary<StringName, int>(),
            achievementRequirements: System.Array.Empty<StringName>(),
            upgradeSourceSkillIds: System.Array.Empty<StringName>(),
            retainSourceSkillsOnUnlock: false,
            coreSkillTransitionMode: "",
            masterySources: System.Array.Empty<StringName>(),
            growthTier: "",
            attributeGrowthProgress: new Dictionary<StringName, int>(),
            practiceTier: "",
            attributeModifiers: System.Array.Empty<AttributeModifierDefinition>(),
            levelDescriptionTemplate: levelDescriptionTemplate,
            levelDescriptionConfigs: levelDescriptionConfigs
                ?? new Dictionary<int, IReadOnlyDictionary<string, object>>(),
            combatProfile: combatProfile
        );
    }

    private static CombatSkillDefinition BuildCombatProfile(
        StringName skillId,
        params CombatEffectDefinition[] effects
    )
    {
        return new CombatSkillDefinition(
            skillId: skillId,
            targetMode: "unit",
            targetTeamFilter: "",
            rangePattern: "single",
            rangeValue: 1,
            areaPattern: "",
            areaValue: 0,
            requiresLos: true,
            apCost: 1,
            mpCost: 0,
            staminaCost: 0,
            cooldownTu: 0,
            castingTimeTu: 0,
            castingMaintenanceDc: 0,
            castingSpellControlDc: 0,
            pendingCastBindingMode: "",
            attackRollBonus: 0,
            attackResolutionMode: "",
            auraCost: 0,
            levelOverrides: new Dictionary<int, IReadOnlyDictionary<string, object>>(),
            masteryTriggerMode: "",
            masteryAmountMode: "",
            spellFateMode: "",
            spellCriticalMode: "",
            spellCriticalMpRefundPercent: 0,
            fumbleProtectionCurve: System.Array.Empty<int>(),
            fumbleProtectionExtraMpPercent: 0,
            backlashMode: "",
            backlashTargetFilter: "",
            backlashOffsetRadius: 0,
            areaOriginMode: "",
            areaDirectionMode: "",
            aiTags: System.Array.Empty<StringName>(),
            deliveryCategories: System.Array.Empty<StringName>(),
            specialResolutionProfileId: "",
            targetSelectionMode: "",
            minTargetCount: 0,
            maxTargetCount: 0,
            allowRepeatTarget: false,
            maxHitsPerTarget: 0,
            selectionOrderMode: "",
            effectDefinitions: effects,
            passiveEffectDefinitions: System.Array.Empty<CombatEffectDefinition>(),
            castVariants: System.Array.Empty<CombatCastVariantDefinition>(),
            requiredWeaponFamilies: System.Array.Empty<StringName>(),
            excludedWeaponFamilies: System.Array.Empty<StringName>(),
            excludedWeaponTypeIds: System.Array.Empty<StringName>(),
            requiresEquippedShield: false,
            masteryLowHpBonusMultiplier: 0,
            masteryLowHpThresholdPercent: 0
        );
    }
}
