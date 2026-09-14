using System;
using System.Collections.Generic;
using Godot;

public partial class
    run_battle_skill_weapon_requirement_rules_regression
    : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();
    private readonly List<BattleUnitState> _ownedUnits = new();

    public override void _Initialize()
    {
        try
        {
            TestRequirementOrderAndAllowedCase();
            TestCurrentAndMeleeWeaponRequirements();
            TestExcludedWeaponRequirements();
            TestInvalidInputsStayTyped();
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }
        finally
        {
            foreach (BattleUnitState unit in _ownedUnits)
            {
                BattleTestFixture.DisposeBattleUnit(unit);
            }
        }

        RequestTestExit(
            _test.Finish(
                "Battle skill weapon requirement rules regression"
            )
        );
    }

    private void TestRequirementOrderAndAllowedCase()
    {
        SkillDefinition skill = BuildSkill(
            "ordered_weapon_requirements",
            requiredWeaponFamilies: new[] { new StringName("sword") },
            requiredWeaponTypeIds:
                new[] { new StringName("longsword") },
            requiresEquippedShield: true
        );
        AssertReason(
            BuildUnit("hammer", "warhammer", "melee"),
            skill,
            BattleSkillCastBlockReasonKind
                .RequiredWeaponFamilyMissing,
            "武器家族必须先于类型和盾牌返回。"
        );
        AssertReason(
            BuildUnit("sword", "shortsword", "melee"),
            skill,
            BattleSkillCastBlockReasonKind
                .RequiredWeaponTypeMissing,
            "家族通过后必须返回精确武器类型缺失。"
        );
        AssertReason(
            BuildUnit("sword", "longsword", "melee"),
            skill,
            BattleSkillCastBlockReasonKind.ShieldRequired,
            "家族和类型通过后必须检查副手盾牌。"
        );
        AssertReason(
            BuildUnit(
                "sword",
                "longsword",
                "melee",
                hasShield: true
            ),
            skill,
            BattleSkillCastBlockReasonKind.None,
            "完整满足家族、类型和盾牌时必须放行。"
        );
    }

    private void TestCurrentAndMeleeWeaponRequirements()
    {
        SkillDefinition currentWeaponSkill = BuildSkill(
            "current_weapon_requirement",
            requiresWeapon: true
        );
        AssertReason(
            BuildUnit(
                "unarmed",
                "unarmed",
                "melee",
                equipped: false
            ),
            currentWeaponSkill,
            BattleSkillCastBlockReasonKind.MeleeWeaponRequired,
            "requires_weapon 效果必须要求真实装备武器。"
        );
        AssertReason(
            BuildUnit("bow", "longbow", "ranged"),
            currentWeaponSkill,
            BattleSkillCastBlockReasonKind.None,
            "非近战 requires_weapon 技能允许有效远程武器。"
        );

        SkillDefinition meleeWeaponSkill = BuildSkill(
            "melee_weapon_requirement",
            tags: new[] { new StringName("melee") },
            requiresWeapon: true
        );
        AssertReason(
            BuildUnit("bow", "longbow", "ranged"),
            meleeWeaponSkill,
            BattleSkillCastBlockReasonKind.MeleeWeaponRequired,
            "melee + requires_weapon 必须拒绝有效但非近战的武器。"
        );
    }

    private void TestExcludedWeaponRequirements()
    {
        SkillDefinition excludedFamilySkill = BuildSkill(
            "excluded_family_requirement",
            excludedWeaponFamilies:
                new[] { new StringName("sword") }
        );
        AssertReason(
            BuildUnit("sword", "longsword", "melee"),
            excludedFamilySkill,
            BattleSkillCastBlockReasonKind.ExcludedWeaponFamily,
            "命中排除家族时必须返回家族阻断。"
        );

        SkillDefinition excludedTypeSkill = BuildSkill(
            "excluded_type_requirement",
            excludedWeaponTypeIds:
                new[] { new StringName("longsword") }
        );
        AssertReason(
            BuildUnit("sword", "longsword", "melee"),
            excludedTypeSkill,
            BattleSkillCastBlockReasonKind.ExcludedWeaponType,
            "命中排除类型时必须返回类型阻断。"
        );
    }

    private void TestInvalidInputsStayTyped()
    {
        SkillDefinition skill = BuildSkill("invalid_input_requirement");
        _test.Eq(
            BattleSkillWeaponRequirementRules.GetBlockReason(
                (BattleUnitState)null,
                skill,
                ShieldItemDefinitions()
            ),
            BattleSkillCastBlockReasonKind.InvalidSkillOrTarget,
            "无效单位必须返回 typed invalid reason。"
        );
        _test.Eq(
            BattleSkillWeaponRequirementRules.GetBlockReason(
                BuildUnit("sword", "longsword", "melee"),
                null,
                ShieldItemDefinitions()
            ),
            BattleSkillCastBlockReasonKind.InvalidSkillOrTarget,
            "无效技能必须返回 typed invalid reason。"
        );
    }

    private void AssertReason(
        BattleUnitState unit,
        SkillDefinition skill,
        BattleSkillCastBlockReasonKind expected,
        string message
    )
    {
        _test.Eq(
            BattleSkillWeaponRequirementRules.GetBlockReason(
                unit,
                skill,
                ShieldItemDefinitions()
            ),
            expected,
            message
        );
    }

    private BattleUnitState BuildUnit(
        StringName weaponFamily,
        StringName weaponTypeId,
        StringName rangeType,
        bool equipped = true,
        bool hasShield = false
    )
    {
        BattleUnitState unit = BattleTestFixture.BuildUnit(
            $"weapon_rule_unit_{_ownedUnits.Count}",
            "player",
            Vector2I.Zero,
            currentHp: 20
        );
        unit.RestoreWeaponProjectionForMutationSnapshotExact(
            BattleUnitWeaponProjectionSnapshot.Present(
                new BattleWeaponProjectionValues(
                    equipped ? "equipped" : "unarmed",
                    $"test_{weaponFamily}",
                    weaponTypeId,
                    rangeType,
                    weaponFamily,
                    "one_handed",
                    rangeType == new StringName("ranged")
                        ? 6
                        : 1,
                    new BattleWeaponDiceValues(
                        true,
                        1,
                        6,
                        0
                    ),
                    BattleWeaponDiceValues.PresentEmpty,
                    false,
                    false,
                    "physical_slash"
                )
            )
        );
        if (hasShield)
        {
            var equipment = new EquipmentState();
            equipment.SetEquippedEntry(
                "off_hand",
                "training_shield",
                new[] { new StringName("off_hand") },
                EquipmentInstanceState.CreateInstance(
                    "training_shield",
                    $"eq_training_shield_{_ownedUnits.Count}"
                )
            );
            unit.SetEquipmentView(equipment);
        }
        _ownedUnits.Add(unit);
        return unit;
    }

    private static SkillDefinition BuildSkill(
        StringName skillId,
        IReadOnlyList<StringName> tags = null,
        IReadOnlyList<StringName> requiredWeaponFamilies = null,
        IReadOnlyList<StringName> requiredWeaponTypeIds = null,
        IReadOnlyList<StringName> excludedWeaponFamilies = null,
        IReadOnlyList<StringName> excludedWeaponTypeIds = null,
        bool requiresEquippedShield = false,
        bool requiresWeapon = false
    )
    {
        IReadOnlyList<CombatEffectDefinition> effects =
            requiresWeapon
                ? new[]
                {
                    TestSkillDefinitionProjection.BuildEffect(
                        "damage",
                        requiresWeapon: true
                    ),
                }
                : Array.Empty<CombatEffectDefinition>();
        return TestSkillDefinitionProjection.BuildSkill(
            skillId,
            tags: tags ?? Array.Empty<StringName>(),
            combatProfile:
                TestSkillDefinitionProjection.BuildCombatProfile(
                    skillId,
                    effects,
                    requiredWeaponFamilies:
                        requiredWeaponFamilies,
                    requiredWeaponTypeIds:
                        requiredWeaponTypeIds,
                    excludedWeaponFamilies:
                        excludedWeaponFamilies,
                    excludedWeaponTypeIds:
                        excludedWeaponTypeIds,
                    requiresEquippedShield:
                        requiresEquippedShield
                )
        );
    }

    private static IReadOnlyDictionary<
        StringName,
        ItemDefinition
    > ShieldItemDefinitions() =>
        new Dictionary<StringName, ItemDefinition>
        {
            ["training_shield"] =
                new ItemDefinition(
                    "training_shield",
                    "Training Shield",
                    "",
                    "",
                    false,
                    0,
                    0,
                    0,
                    true,
                    1,
                    ItemDefinition.ToStringName(
                        ItemCategoryKind.Misc
                    ),
                    new[] { new StringName("shield") },
                    Array.Empty<StringName>(),
                    Array.Empty<StringName>(),
                    Array.Empty<StringName>(),
                    Array.Empty<TraitRollGroupDefinition>(),
                    new[] { "off_hand" },
                    Array.Empty<
                        AttributeModifierDefinition
                    >(),
                    "",
                    Array.Empty<string>(),
                    null,
                    "",
                    null,
                    -1
                ),
        };
}
