using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_battle_runtime_attack_check_smoke : SceneTree
{
    private readonly GStringArray _failures = new();

    public override void _Initialize()
    {
        int exitCode = Run();
        Quit(exitCode);
    }

    private int Run()
    {
        TestHitResolverBoundaryNaturalRulesAreExplicit();
        TestArmorBreakLowersTargetAcWithoutDamageVulnerability();

        if (_failures.Count == 0)
        {
            GD.Print("Battle runtime attack check smoke: PASS");
            return 0;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Battle runtime attack check smoke: FAIL ({_failures.Count})");
        return 1;
    }

    private void TestHitResolverBoundaryNaturalRulesAreExplicit()
    {
        var resolver = new BattleHitResolver();

        BattleUnitState accurateAttacker = BuildUnit("hit_boundary_accurate", Vector2I.Zero, 1);
        accurateAttacker.attribute_snapshot.set_value(AttributeService.ATTACK_BONUS_ID(), 100);
        BattleUnitState easyTarget = BuildEnemyUnit("hit_boundary_easy_target", new Vector2I(1, 0));
        easyTarget.attribute_snapshot.set_value(AttributeService.ARMOR_CLASS_ID(), -10);
        AttackCheckInput easyCheck = resolver.build_skill_attack_check(
            accurateAttacker,
            easyTarget,
            null
        );
        AssertTrue(
            easyCheck.RequiredRoll <= 1,
            "低 required roll 夹具应进入天然 1 边界语义。"
        );
        AssertEq(easyCheck.DisplayRequiredRoll, 2, "低 required roll 预览应稳定显示为 2+。");
        AssertEq(
            easyCheck.HitRatePercent,
            95,
            "低 required roll 在天然 1 语义下应只保留 95% 命中。"
        );
        AssertTrue(
            easyCheck.PreviewText.Contains("天然 1 仍失手"),
            "低 required roll 预览应显式提示天然 1 失手语义。"
        );

        BattleUnitState weakAttacker = BuildUnit("hit_boundary_weak", Vector2I.Zero, 1);
        weakAttacker.attribute_snapshot.set_value(AttributeService.ATTACK_BONUS_ID(), 0);
        BattleUnitState evasiveTarget = BuildEnemyUnit(
            "hit_boundary_evasive_target",
            new Vector2I(1, 0)
        );
        evasiveTarget.attribute_snapshot.set_value(AttributeService.ARMOR_CLASS_ID(), 100);
        AttackCheckInput hardCheck = resolver.build_skill_attack_check(
            weakAttacker,
            evasiveTarget,
            null
        );
        AssertTrue(
            hardCheck.RequiredRoll > 20,
            "高 required roll 夹具应进入仅天然 20 命中语义。"
        );
        AssertEq(hardCheck.DisplayRequiredRoll, 20, "高 required roll 预览应稳定显示为 20+。");
        AssertEq(
            hardCheck.HitRatePercent,
            5,
            "高 required roll 在天然 20 语义下应只保留 5% 命中。"
        );
        AssertTrue(
            hardCheck.PreviewText.Contains("仅天然 20"),
            "高 required roll 预览应显式提示天然 20 语义。"
        );
    }

    private void TestArmorBreakLowersTargetAcWithoutDamageVulnerability()
    {
        var hitResolver = new BattleHitResolver();
        var damageResolver = new FixedHitMaxDamageResolver();
        BattleUnitState attacker = BuildUnit("armor_break_attacker", Vector2I.Zero, 1);
        attacker.attribute_snapshot.set_value(AttributeService.ATTACK_BONUS_ID(), 4);
        BattleUnitState target = BuildEnemyUnit("armor_break_target", new Vector2I(1, 0));
        target.attribute_snapshot.set_value(AttributeService.ARMOR_CLASS_ID(), 16);

        AttackCheckInput baselineCheck = hitResolver.build_skill_attack_check(
            attacker,
            target,
            null
        );
        var armorBreakEffect = new CombatEffectDef
        {
            effect_type = "status",
            status_id = "armor_break",
            power = 1,
            duration_tu = 90,
        };
        damageResolver.resolve_effects(
            attacker,
            target,
            new GArray { armorBreakEffect },
            new GDictionary()
        );
        AttackCheckInput brokenCheck = hitResolver.build_skill_attack_check(attacker, target, null);
        AssertEq(
            brokenCheck.TargetArmorClass,
            baselineCheck.TargetArmorClass - 2,
            "armor_break power 1 应把有效 AC 降低 2。"
        );
        AssertEq(
            brokenCheck.HitRatePercent,
            baselineCheck.HitRatePercent + 10,
            "armor_break 降低 AC 后应提高 10 个百分点命中率。"
        );

        BattleUnitState plainTarget = BuildEnemyUnit("plain_damage_target", new Vector2I(1, 0));
        BattleUnitState brokenTarget = BuildEnemyUnit("broken_damage_target", new Vector2I(1, 0));
        damageResolver.resolve_effects(
            attacker,
            brokenTarget,
            new GArray { armorBreakEffect },
            new GDictionary()
        );
        var damageEffect = new CombatEffectDef
        {
            effect_type = "damage",
            damage_tag = "physical_slash",
            power = 10,
        };
        GDictionary plainResult = damageResolver.resolve_effects(
            attacker,
            plainTarget,
            new GArray { damageEffect },
            new GDictionary()
        );
        GDictionary brokenResult = damageResolver.resolve_effects(
            attacker,
            brokenTarget,
            new GArray { damageEffect },
            new GDictionary()
        );
        AssertEq(
            DictInt(brokenResult, "damage", 0),
            DictInt(plainResult, "damage", 0),
            "armor_break 不应再提供承伤易伤倍率。"
        );
    }

    private static BattleUnitState BuildUnit(StringName unitId, Vector2I coord, int currentAp)
    {
        var unit = new BattleUnitState
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = "player",
            current_ap = currentAp,
            current_move_points = BattleUnitState.DEFAULT_MOVE_POINTS_PER_TURN(),
            current_hp = 10,
            current_stamina = 60,
            is_alive = true,
        };
        unit.set_anchor_coord(coord);
        unit.attribute_snapshot.set_value(
            AttributeService.ARMOR_CLASS_ID(),
            AttributeService.BASE_ARMOR_CLASS_VALUE()
        );
        return unit;
    }

    private static BattleUnitState BuildEnemyUnit(StringName unitId, Vector2I coord)
    {
        BattleUnitState unit = BuildUnit(unitId, coord, 1);
        unit.faction_id = "enemy";
        unit.current_hp = 30;
        return unit;
    }

    private void AssertTrue(bool condition, string message)
    {
        if (!condition)
        {
            _failures.Add(message);
        }
    }

    private void AssertEq<T>(T actual, T expected, string message)
    {
        if (!Equals(actual, expected))
        {
            _failures.Add($"{message} actual={actual} expected={expected}");
        }
    }

    private static int DictInt(GDictionary dictionary, string key, int fallback)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
        {
            return fallback;
        }
        return dictionary[key].AsInt32();
    }
}
