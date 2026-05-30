using Godot;
using GCombatEffectArray = Godot.Collections.Array<CombatEffectDef>;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_warrior_repeat_attack_mastery_bonus_regression : SceneTree
{
    private readonly GStringArray _failures = new();

    public override void _Initialize()
    {
        int exitCode = Run();
        Quit(exitCode);
    }

    private int Run()
    {
        TestRepeatAttackMasteryBonusStartsOnFifthStageEntry();

        if (_failures.Count == 0)
        {
            GD.Print("Warrior repeat attack mastery bonus regression: PASS");
            return 0;
        }

        foreach (string failure in _failures)
        {
            GD.PushError(failure);
        }
        GD.Print($"Warrior repeat attack mastery bonus regression: FAIL ({_failures.Count})");
        return 1;
    }

    private void TestRepeatAttackMasteryBonusStartsOnFifthStageEntry()
    {
        RepeatAttackFixture missFixture = BuildRepeatAttackFixture(
            new[] { true, true, true, true, false }
        );
        bool missExecuted = missFixture.Resolver.apply_repeat_attack_skill_result(
            missFixture.ActiveUnit,
            missFixture.TargetUnit,
            missFixture.SkillDef,
            missFixture.CombatProfile.effect_defs,
            missFixture.RepeatEffect,
            new BattleEventBatch()
        );
        AssertTrue(missExecuted, "连击段数熟练度回归前置：应至少执行到第五段。");
        AssertEq(
            missFixture.DamageResolver.call_count,
            5,
            "连击段数熟练度回归应固定进入第五段后 miss。"
        );
        AssertEq(
            missFixture.MasteryService.ResolveActiveSkillMasteryAmount(),
            0,
            "连击熟练度 bonus 必须在对应段命中后发放，第五段 miss 不应给 bonus。"
        );

        RepeatAttackFixture hitFixture = BuildRepeatAttackFixture(
            new[] { true, true, true, true, true, false }
        );
        bool hitExecuted = hitFixture.Resolver.apply_repeat_attack_skill_result(
            hitFixture.ActiveUnit,
            hitFixture.TargetUnit,
            hitFixture.SkillDef,
            hitFixture.CombatProfile.effect_defs,
            hitFixture.RepeatEffect,
            new BattleEventBatch()
        );
        AssertTrue(hitExecuted, "连击段数熟练度回归前置：命中夹具应执行。");
        AssertEq(
            hitFixture.DamageResolver.call_count,
            6,
            "命中夹具应在第五段命中后继续进入第六段 miss。"
        );
        AssertEq(
            hitFixture.MasteryService.ResolveActiveSkillMasteryAmount(),
            1,
            "第五段命中后应发放 1 点连击段数 bonus。"
        );
    }

    private RepeatAttackFixture BuildRepeatAttackFixture(bool[] stageSuccesses)
    {
        var runtime = new BattleRuntimeModule();
        runtime.setup(null, new GDictionary(), new GDictionary(), new GDictionary());
        var damageResolver = new StageOutcomeDamageResolver();
        foreach (bool stageSuccess in stageSuccesses)
        {
            damageResolver.stage_successes.Add(stageSuccess);
        }
        runtime.configure_damage_resolver_for_tests(damageResolver);

        var masteryService = new BattleSkillMasteryService();
        var resolver = new BattleRepeatAttackResolver();
        resolver.setup(runtime, masteryService);

        BattleUnitState activeUnit = BuildUnit("combo_mastery_user", new Vector2I(1, 1), 2);
        activeUnit.source_member_id = "hero";
        activeUnit.current_aura = 99;
        activeUnit.known_active_skill_ids = new Godot.Collections.Array<StringName>
        {
            "combo_mastery_stage_test",
        };
        activeUnit.known_skill_level_map["combo_mastery_stage_test"] = 1;

        BattleUnitState targetUnit = BuildUnit("combo_mastery_target", new Vector2I(2, 1), 2);
        targetUnit.faction_id = "enemy";
        targetUnit.current_hp = 999;
        targetUnit.attribute_snapshot.set_value(AttributeService.HP_MAX_ID(), 999);

        var damageEffect = new CombatEffectDef
        {
            effect_type = "damage",
            power = 0,
        };
        var repeatEffect = new CombatEffectDef
        {
            effect_type = "repeat_attack_until_fail",
            @params = new GDictionary
            {
                ["cost_resource"] = "aura",
                ["follow_up_fixed_cost"] = 0,
                ["follow_up_attack_penalty"] = 0,
                ["stop_on_miss"] = true,
                ["stop_on_target_down"] = true,
            },
        };
        var skillDef = new SkillDef
        {
            skill_id = "combo_mastery_stage_test",
            display_name = "连击熟练度段数测试",
        };
        var combatProfile = new CombatSkillDef
        {
            skill_id = skillDef.skill_id,
            mastery_amount_mode = "per_target_rank",
            mastery_trigger_mode = "damage_dealt",
            aura_cost = 0,
            effect_defs = new GCombatEffectArray { damageEffect, repeatEffect },
        };
        skillDef.combat_profile = combatProfile;

        return new RepeatAttackFixture
        {
            Runtime = runtime,
            DamageResolver = damageResolver,
            MasteryService = masteryService,
            Resolver = resolver,
            ActiveUnit = activeUnit,
            TargetUnit = targetUnit,
            SkillDef = skillDef,
            CombatProfile = combatProfile,
            RepeatEffect = repeatEffect,
        };
    }

    private static BattleUnitState BuildUnit(StringName unitId, Vector2I coord, int currentAp)
    {
        var unit = new BattleUnitState
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = "player",
            current_ap = currentAp,
            current_hp = 40,
            current_mp = 4,
            current_stamina = 60,
            current_aura = 0,
            is_alive = true,
        };
        unit.set_anchor_coord(coord);
        unit.attribute_snapshot.set_value(AttributeService.HP_MAX_ID(), 40);
        unit.attribute_snapshot.set_value(AttributeService.MP_MAX_ID(), 4);
        unit.attribute_snapshot.set_value(AttributeService.STAMINA_MAX_ID(), 60);
        unit.attribute_snapshot.set_value(AttributeService.AURA_MAX_ID(), 8);
        unit.attribute_snapshot.set_value(AttributeService.ACTION_POINTS_ID(), Mathf.Max(currentAp, 1));
        unit.unlock_combat_resource(BattleUnitState.COMBAT_RESOURCE_MP());
        unit.unlock_combat_resource(BattleUnitState.COMBAT_RESOURCE_AURA());
        unit.attribute_snapshot.set_value(AttributeService.ATTACK_BONUS_ID(), 80);
        unit.attribute_snapshot.set_value(AttributeService.ARMOR_CLASS_ID(), 5);
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

    private sealed class RepeatAttackFixture
    {
        public BattleRuntimeModule Runtime;
        public StageOutcomeDamageResolver DamageResolver;
        public BattleSkillMasteryService MasteryService;
        public BattleRepeatAttackResolver Resolver;
        public BattleUnitState ActiveUnit;
        public BattleUnitState TargetUnit;
        public SkillDef SkillDef;
        public CombatSkillDef CombatProfile;
        public CombatEffectDef RepeatEffect;
    }
}
