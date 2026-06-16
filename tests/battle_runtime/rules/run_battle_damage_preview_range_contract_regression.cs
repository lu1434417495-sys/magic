using System;
using System.Collections.Generic;
using Godot;

public partial class run_battle_damage_preview_range_contract_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestEmptyPreviewContract();
        TestPowerOnlyDamagePreview();
        TestWeaponAndSkillDiceDamageRange();
        TestMultipleDamageEffectsAreSummed();
        TestTwoHandedWeaponDiceIgnoresAliasSkillDiceFields();
        TestDiceBonusWithoutDiceIsIgnored();

        Quit(_test.Finish("Battle damage preview range contract regression"));
    }

    private void TestEmptyPreviewContract()
    {
        BattleDamagePreviewRangeService.SkillDamagePreview preview =
            BattleDamagePreviewRangeService.BuildSkillDamagePreview(
                null,
                Array.Empty<CombatEffectDef>()
            );
        Godot.Collections.Dictionary payload = preview.ToDictionary();

        _test.False(preview.HasDamage, "无伤害效果时 HasDamage 应为 false。");
        _test.Eq(preview.MinDamage, 0, "无伤害效果时 MinDamage 应为 0。");
        _test.Eq(preview.MaxDamage, 0, "无伤害效果时 MaxDamage 应为 0。");
        _test.Eq(preview.SummaryText, "", "无伤害效果时 SummaryText 应为空。");
        _test.Eq(
            payload["damage_ranges"].AsGodotArray().Count,
            0,
            "无伤害效果时 damage_ranges 投影应为空。"
        );
    }

    private void TestPowerOnlyDamagePreview()
    {
        CombatEffectDef effect = BuildDamageEffect(12);
        BattleDamagePreviewRangeService.SkillDamagePreview preview =
            BattleDamagePreviewRangeService.BuildSkillDamagePreview(null, new[] { effect });

        _test.True(preview.HasDamage, "power-only 伤害效果应产生伤害预览。");
        _test.Eq(preview.MinDamage, 12, "power-only MinDamage 应等于 power。");
        _test.Eq(preview.MaxDamage, 12, "power-only MaxDamage 应等于 power。");
        _test.Eq(preview.SummaryText, "伤害 12", "固定伤害摘要应省略范围横杠。");
        _test.Eq(
            BattleDamagePreviewRangeService.FormatDamageRangeText(preview),
            "伤害 12",
            "FormatDamageRangeText 应复用同一固定伤害文案。"
        );
    }

    private void TestWeaponAndSkillDiceDamageRange()
    {
        BattleUnitState source = BuildUnit("preview_weapon_user");
        ApplyWeapon(source, 1, 6, 2);
        CombatEffectDef effect = BuildDamageEffect(5, true, 2, 4, 3);

        BattleDamagePreviewRangeService.SkillDamagePreview preview =
            BattleDamagePreviewRangeService.BuildSkillDamagePreview(source, new[] { effect });
        BattleDamagePreviewRangeService.DamageEffectRange damageRange =
            preview.DamageRanges[0];

        _test.Eq(
            preview.MinDamage,
            13,
            "最小值应为 power + 武器最小骰含 flat_bonus + 技能最小骰含 dice_bonus。"
        );
        _test.Eq(
            preview.MaxDamage,
            24,
            "最大值应为 power + 武器最大骰含 flat_bonus + 技能最大骰含 dice_bonus。"
        );
        _test.Eq(preview.SummaryText, "伤害 13-24", "范围摘要应显示理论最小与最大值。");
        _test.True(damageRange.AddWeaponDice, "显式 add_weapon_dice 应进入 per-effect 预览。");
        _test.Eq(damageRange.WeaponDiceRange.MinDamage, 3, "武器骰最小值应包含 flat_bonus。");
        _test.Eq(damageRange.WeaponDiceRange.MaxDamage, 8, "武器骰最大值应包含 flat_bonus。");
        _test.Eq(damageRange.SkillDiceRange.MinDamage, 5, "技能骰最小值应包含 dice_bonus。");
        _test.Eq(damageRange.SkillDiceRange.MaxDamage, 11, "技能骰最大值应包含 dice_bonus。");
    }

    private void TestMultipleDamageEffectsAreSummed()
    {
        BattleUnitState source = BuildUnit("multi_preview_user");
        ApplyWeapon(source, 2, 6, 0);
        CombatEffectDef weaponEffect = BuildDamageEffect(0, true);
        var statusEffect = new CombatEffectDef
        {
            effect_type = "status",
            power = 99,
        };
        CombatEffectDef skillEffect = BuildDamageEffect(10, false, 1, 8, 1);

        BattleDamagePreviewRangeService.SkillDamagePreview preview =
            BattleDamagePreviewRangeService.BuildSkillDamagePreview(
                source,
                new[] { weaponEffect, statusEffect, skillEffect }
            );

        _test.Eq(preview.MinDamage, 14, "多个 damage effect 的 MinDamage 应求和并跳过非伤害效果。");
        _test.Eq(preview.MaxDamage, 31, "多个 damage effect 的 MaxDamage 应求和并跳过非伤害效果。");
        _test.Eq(preview.DamageRanges.Count, 2, "DamageRanges 应只包含 damage effect。");
        if (preview.DamageRanges.Count >= 2)
        {
            _test.Eq(preview.DamageRanges[0].EffectIndex, 0, "第一段应保留原 effect index。");
            _test.Eq(preview.DamageRanges[1].EffectIndex, 2, "第二段应保留原 effect index。");
        }
    }

    private void TestTwoHandedWeaponDiceIgnoresAliasSkillDiceFields()
    {
        BattleUnitState source = BuildUnit("two_handed_preview_user");
        source.ApplyWeaponProjectionTyped(
            new WeaponProjection
            {
                weapon_profile_kind = "equipped",
                weapon_item_id = "two_handed_preview_weapon",
                weapon_profile_type_id = "greatsword",
                weapon_current_grip = "two_handed",
                weapon_attack_range = 1,
                weapon_one_handed_dice = new WeaponDice
                {
                    dice_count = 1,
                    dice_sides = 4,
                    flat_bonus = 1,
                },
                weapon_two_handed_dice = new WeaponDice
                {
                    dice_count = 2,
                    dice_sides = 6,
                    flat_bonus = 4,
                },
                weapon_uses_two_hands = true,
                weapon_physical_damage_tag = "physical_slash",
            }
        );
        CombatEffectDef effect = BuildDamageEffect(1, true);
        effect.@params["damage_dice_count"] = 3;
        effect.@params["damage_dice_sides"] = 3;
        effect.@params["damage_dice_bonus"] = 2;

        BattleDamagePreviewRangeService.SkillDamagePreview preview =
            BattleDamagePreviewRangeService.BuildSkillDamagePreview(source, new[] { effect });
        BattleDamagePreviewRangeService.DamageEffectRange damageRange = preview.DamageRanges[0];

        _test.Eq(preview.MinDamage, 7, "旧技能骰 alias 不应加入预览最小伤害。");
        _test.Eq(preview.MaxDamage, 17, "旧技能骰 alias 不应加入预览最大伤害。");
        _test.Eq(damageRange.WeaponDiceRange.DiceCount, 2, "双手武器骰数量应来自 two_handed_dice。");
        _test.Eq(damageRange.WeaponDiceRange.DiceSides, 6, "双手武器骰面应来自 two_handed_dice。");
        _test.Eq(damageRange.SkillDiceRange.DiceCount, 0, "旧 damage_dice_count alias 不应再被读取。");
        _test.Eq(damageRange.SkillDiceRange.DiceSides, 0, "旧 damage_dice_sides alias 不应再被读取。");
        _test.Eq(damageRange.SkillDiceRange.DiceBonus, 0, "旧 damage_dice_bonus alias 不应再被读取。");
    }

    private void TestDiceBonusWithoutDiceIsIgnored()
    {
        CombatEffectDef effect = BuildDamageEffect(4);
        effect.dice_bonus = 99;
        BattleDamagePreviewRangeService.SkillDamagePreview preview =
            BattleDamagePreviewRangeService.BuildSkillDamagePreview(null, new[] { effect });
        BattleDamagePreviewRangeService.DamageEffectRange damageRange = preview.DamageRanges[0];

        _test.Eq(preview.MinDamage, 4, "缺少有效技能骰时 dice_bonus 不应单独加入最小伤害。");
        _test.Eq(preview.MaxDamage, 4, "缺少有效技能骰时 dice_bonus 不应单独加入最大伤害。");
        _test.Eq(damageRange.SkillDiceRange.DiceBonus, 0, "无有效技能骰时 damage_dice_bonus 应保持 0。");
    }

    private static CombatEffectDef BuildDamageEffect(
        int power,
        bool addWeaponDice = false,
        int diceCount = 0,
        int diceSides = 0,
        int diceBonus = 0
    )
    {
        var effect = new CombatEffectDef
        {
            effect_type = "damage",
            power = power,
            @params = new Godot.Collections.Dictionary(),
        };
        if (addWeaponDice)
        {
            effect.add_weapon_dice = true;
        }
        if (diceCount > 0 && diceSides > 0)
        {
            effect.dice_count = diceCount;
            effect.dice_sides = diceSides;
            effect.dice_bonus = diceBonus;
        }
        return effect;
    }

    private static BattleUnitState BuildUnit(StringName unitId)
    {
        return new BattleUnitState
        {
            unit_id = unitId,
            current_hp = 100,
        };
    }

    private static void ApplyWeapon(
        BattleUnitState unit,
        int diceCount,
        int diceSides,
        int flatBonus
    )
    {
        unit.ApplyWeaponProjectionTyped(
            new WeaponProjection
            {
                weapon_profile_kind = "equipped",
                weapon_item_id = "preview_range_weapon",
                weapon_profile_type_id = "test_weapon",
                weapon_current_grip = "one_handed",
                weapon_attack_range = 1,
                weapon_one_handed_dice = new WeaponDice
                {
                    dice_count = diceCount,
                    dice_sides = diceSides,
                    flat_bonus = flatBonus,
                },
                weapon_uses_two_hands = false,
                weapon_physical_damage_tag = "physical_slash",
            }
        );
    }
}
