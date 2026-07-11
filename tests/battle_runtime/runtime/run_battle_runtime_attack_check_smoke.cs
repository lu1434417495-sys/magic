using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_battle_runtime_attack_check_smoke : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestHitResolverBoundaryNaturalRulesAreExplicit();
        TestArmorBreakLowersTargetAcWithoutDamageVulnerability();
        RequestTestExit(_test.Finish("Battle runtime attack check smoke"));
    }

    private void TestHitResolverBoundaryNaturalRulesAreExplicit()
    {
        var resolver = new BattleHitResolver();

        BattleUnitState accurateAttacker = BuildUnit("hit_boundary_accurate", Vector2I.Zero, 1);
        accurateAttacker.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.AttackBonus), 100);
        BattleUnitState easyTarget = BuildEnemyUnit("hit_boundary_easy_target", new Vector2I(1, 0));
        easyTarget.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.ArmorClass), -10);
        AttackCheckInput easyCheck = resolver.BuildSkillAttackCheck(
            accurateAttacker,
            easyTarget,
            null
        );
        _test.True(
            easyCheck.RequiredRoll <= 1,
            "低 required roll 夹具应进入天然 1 边界语义。"
        );
        _test.Eq(easyCheck.DisplayRequiredRoll, 2, "低 required roll 预览应稳定显示为 2+。");
        _test.Eq(
            easyCheck.HitRatePercent,
            95,
            "低 required roll 在天然 1 语义下应只保留 95% 命中。"
        );
        _test.True(easyCheck.NaturalOneAutoMiss, "低 required roll 预览应保留天然 1 失手语义。");

        BattleUnitState weakAttacker = BuildUnit("hit_boundary_weak", Vector2I.Zero, 1);
        weakAttacker.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.AttackBonus), 0);
        BattleUnitState evasiveTarget = BuildEnemyUnit(
            "hit_boundary_evasive_target",
            new Vector2I(1, 0)
        );
        evasiveTarget.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.ArmorClass), 100);
        AttackCheckInput hardCheck = resolver.BuildSkillAttackCheck(
            weakAttacker,
            evasiveTarget,
            null
        );
        _test.True(
            hardCheck.RequiredRoll > 20,
            "高 required roll 夹具应进入仅天然 20 命中语义。"
        );
        _test.Eq(hardCheck.DisplayRequiredRoll, 20, "高 required roll 预览应稳定显示为 20+。");
        _test.Eq(
            hardCheck.HitRatePercent,
            5,
            "高 required roll 在天然 20 语义下应只保留 5% 命中。"
        );
        _test.True(hardCheck.NaturalTwentyAutoHit, "高 required roll 预览应保留天然 20 自动命中语义。");
    }

    private void TestArmorBreakLowersTargetAcWithoutDamageVulnerability()
    {
        var hitResolver = new BattleHitResolver();
        var damageResolver = new FixedHitMaxDamageResolver();
        BattleUnitState attacker = BuildUnit("armor_break_attacker", Vector2I.Zero, 1);
        attacker.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.AttackBonus), 4);
        BattleUnitState target = BuildEnemyUnit("armor_break_target", new Vector2I(1, 0));
        target.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.ArmorClass), 16);

        AttackCheckInput baselineCheck = hitResolver.BuildSkillAttackCheck(
            attacker,
            target,
            null
        );
        CombatEffectDefinition armorBreakEffect = TestSkillDefinitionProjection.BuildEffect(
            "status",
            statusId: "armor_break",
            power: 1,
            durationTu: 90
        );
        damageResolver.ResolveEffects(
            attacker,
            target,
            new[] { armorBreakEffect },
            DamageResolutionContext.Empty()
        );
        AttackCheckInput brokenCheck = hitResolver.BuildSkillAttackCheck(attacker, target, null);
        _test.Eq(
            brokenCheck.TargetArmorClass,
            baselineCheck.TargetArmorClass - 2,
            "armor_break power 1 应把有效 AC 降低 2。"
        );
        _test.Eq(
            brokenCheck.HitRatePercent,
            baselineCheck.HitRatePercent + 10,
            "armor_break 降低 AC 后应提高 10 个百分点命中率。"
        );

        BattleUnitState plainTarget = BuildEnemyUnit("plain_damage_target", new Vector2I(1, 0));
        BattleUnitState brokenTarget = BuildEnemyUnit("broken_damage_target", new Vector2I(1, 0));
        damageResolver.ResolveEffects(
            attacker,
            brokenTarget,
            new[] { armorBreakEffect },
            DamageResolutionContext.Empty()
        );
        CombatEffectDefinition damageEffect = TestSkillDefinitionProjection.BuildEffect(
            "damage",
            damageTag: "physical_slash",
            power: 10
        );
        GDictionary plainResult = AttackEffectResolutionResultReader.BuildGodotPayload(damageResolver.ResolveEffects(
            attacker,
            plainTarget,
            new[] { damageEffect },
            DamageResolutionContext.Empty()
        ));
        GDictionary brokenResult = AttackEffectResolutionResultReader.BuildGodotPayload(damageResolver.ResolveEffects(
            attacker,
            brokenTarget,
            new[] { damageEffect },
            DamageResolutionContext.Empty()
        ));
        _test.Eq(
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
            current_move_points = BattleUnitState.DefaultMovePointsPerTurn,
            current_hp = 10,
            current_stamina = 60,
            is_alive = true,
        };
        unit.SetAnchorCoord(coord);
        unit.attribute_snapshot.SetValue(
            AttributeService.ToStringName(AttributeIdKind.ArmorClass),
            AttributeService.BASE_ARMOR_CLASS
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

    private static int DictInt(GDictionary dictionary, string key, int fallback)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
        {
            return fallback;
        }
        return dictionary[key].AsInt32();
    }
}
