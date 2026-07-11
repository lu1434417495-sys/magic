using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_battle_execute_lethal_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        TestPwkSkipsLowPriorityDeathWard();
        TestPwkAllowsHighPriorityDeathWard();
        TestPwkBypassesShieldWithoutMutatingShieldFields();

        RequestTestExit(_test.Finish("Battle execute lethal regression"));
    }

    private void TestPwkSkipsLowPriorityDeathWard()
    {
        BattleDamageResolver resolver = BuildResolverWithLastStand();
        BattleUnitState source = MakeUnit("mage_source", "player");
        BattleUnitState target = MakeUnit("low_priority_ward_target", "hostile");
        target.current_hp = 5;
        SetDeathWard(target, 100);

        resolver.ResolveEffects(
            source,
            target,
            new[] { MakeExecuteEffect() },
            DamageResolutionContext.FromDictionary(new GDictionary { ["save_roll_override"] = 1 })
        );

        _test.False(target.is_alive, "PWK should skip low-priority death ward.");
        _test.Eq(target.current_hp, 0, "low-priority death ward should not clamp PWK fatal damage.");
    }

    private void TestPwkAllowsHighPriorityDeathWard()
    {
        BattleDamageResolver resolver = BuildResolverWithLastStand();
        BattleUnitState source = MakeUnit("mage_source", "player");
        BattleUnitState target = MakeUnit("high_priority_ward_target", "hostile");
        target.current_hp = 5;
        SetDeathWard(target, 900);

        resolver.ResolveEffects(
            source,
            target,
            new[] { MakeExecuteEffect() },
            DamageResolutionContext.FromDictionary(new GDictionary { ["save_roll_override"] = 1 })
        );

        _test.True(target.is_alive, "PWK should allow same-priority death ward to trigger.");
        _test.True(target.current_hp > 0, "high-priority death ward should restore positive HP.");
        _test.False(target.HasStatusEffect("death_ward"), "triggered death ward should be consumed.");
        _test.True(target.HasStatusEffect("last_stand_active"), "Lv7 last stand should add active status.");
        _test.True(
            target.HasStatusEffect("soul_fracture"),
            "PWK survivor saved by high-priority death ward should receive soul fracture."
        );
    }

    private void TestPwkBypassesShieldWithoutMutatingShieldFields()
    {
        BattleDamageResolver resolver = new();
        BattleUnitState source = MakeUnit("mage_source", "player");
        BattleUnitState target = MakeUnit("shielded_target", "hostile");
        target.current_hp = 5;
        target.current_shield_hp = 20;
        target.shield_max_hp = 20;
        target.shield_duration = 10;

        resolver.ResolveEffects(
            source,
            target,
            new[] { MakeExecuteEffect() },
            DamageResolutionContext.FromDictionary(new GDictionary { ["save_roll_override"] = 1 })
        );

        _test.False(target.is_alive, "failed-save PWK should kill shielded low-HP target.");
        _test.Eq(target.current_shield_hp, 20, "PWK should not drain shield HP.");
        _test.Eq(target.shield_max_hp, 20, "PWK should not mutate shield max HP.");
        _test.Eq(target.shield_duration, 10, "PWK should not mutate shield duration.");
    }

    private static BattleDamageResolver BuildResolverWithLastStand()
    {
        BattleDamageResolver resolver = new();
        SkillDefinition lastStandSkill = TestSkillDefinitionProjection.LoadSkillDefinition(
            "res://data/configs/skills/warrior_last_stand.tres",
            "battle_execute_lethal:warrior_last_stand"
        );
        resolver.SetSkillDefinitions(
            new Dictionary<StringName, SkillDefinition>
            {
                [(StringName)"warrior_last_stand"] = lastStandSkill,
            }
        );
        return resolver;
    }

    private static CombatEffectDefinition MakeExecuteEffect() =>
        TestSkillDefinitionProjection.BuildEffect(
            "execute",
            effectTargetTeamFilter: "enemy",
            damageTag: "negative_energy",
            saveDcMode: "static",
            saveDc: 10,
            saveAbility: "willpower",
            saveTag: "execute",
            thresholdMaxHpRatioPercent: 20,
            thresholdLevelAnchor: 17,
            thresholdLevelBonusPerDelta: 5,
            thresholdCapMaxHpRatioPercent: 50,
            soulFractureDurationTu: 60,
            healMultiplierPercent: 50,
            shieldGainMultiplierPercent: 50
        );

    private static BattleUnitState MakeUnit(StringName unitId, StringName factionId)
    {
        BattleUnitState unit = new()
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = factionId,
            control_mode = "manual",
            current_hp = 30,
            current_ap = 2,
            is_alive = true,
        };
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), 30);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.AttackBonus), 10);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.ArmorClass), 10);
        unit.attribute_snapshot.SetValue("willpower", 10);
        return unit;
    }

    private static void SetDeathWard(BattleUnitState unit, int priority)
    {
        unit.SetStatusEffect(new BattleStatusEffectState
        {
            status_id = "death_ward",
            source_unit_id = "last_stand_source",
            source_skill_id = "warrior_last_stand",
            source_skill_level = 7,
            death_prevention_priority = priority,
            power = 1,
            stacks = 1,
            duration = -1,
            @params = new GDictionary(),
        });
    }
}
