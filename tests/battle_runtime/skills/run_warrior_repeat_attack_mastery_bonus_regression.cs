using System.Collections.Generic;
using Godot;
using GCombatEffectArray = Godot.Collections.Array<CombatEffectDef>;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;

public partial class run_warrior_repeat_attack_mastery_bonus_regression : SceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        int exitCode = Run();
        Quit(exitCode);
    }

    private int Run()
    {
        TestRepeatAttackResolverUsesTypedResourceCosts();
        TestRepeatAttackMasteryBonusStartsOnFifthStageEntry();
        TestWeaponAttackQualityReadsWeaponDiceMaxReasonFromResultPayload();
        TestGuardMasteryGrantReadsSkillDefFromTypedDictionaryKey();

        return _test.Finish("Warrior repeat attack mastery bonus regression");
    }

    private void TestRepeatAttackResolverUsesTypedResourceCosts()
    {
        RepeatAttackFixture fixture = BuildRepeatAttackFixture(new[] { true });
        fixture.CombatProfile.ap_cost = 2;
        fixture.CombatProfile.mp_cost = 3;
        fixture.CombatProfile.stamina_cost = 4;
        fixture.CombatProfile.aura_cost = 5;

        CombatSkillResourceCosts costs = fixture.Resolver._resolve_effective_skill_costs(
            fixture.ActiveUnit,
            fixture.SkillDef
        );
        _test.Eq(costs.ApCost, 2, "repeat attack typed costs 应保留 AP。");
        _test.Eq(costs.MpCost, 3, "repeat attack typed costs 应保留 MP。");
        _test.Eq(costs.StaminaCost, 4, "repeat attack typed costs 应保留 Stamina。");
        _test.Eq(costs.AuraCost, 5, "repeat attack typed costs 应保留 Aura。");

        _test.Eq(
            fixture.Resolver._get_repeat_attack_base_resource_cost(
                fixture.ActiveUnit,
                fixture.SkillDef,
                CombatResourceKind.Aura
            ),
            5,
            "repeat attack base resource cost 应直接来自 typed Aura cost。"
        );
    }

    private void TestRepeatAttackMasteryBonusStartsOnFifthStageEntry()
    {
        RepeatAttackFixture missFixture = BuildRepeatAttackFixture(
            new[] { true, true, true, true, false }
        );
        bool missExecuted = missFixture.Resolver.ApplyRepeatAttackSkillResult(
            missFixture.ActiveUnit,
            missFixture.TargetUnit,
            missFixture.SkillDef,
            missFixture.CombatProfile.effect_defs,
            missFixture.RepeatEffect,
            new BattleEventBatch()
        );
        _test.True(missExecuted, "连击段数熟练度回归前置：应至少执行到第五段。");
        _test.Eq(
            missFixture.DamageResolver.call_count,
            5,
            "连击段数熟练度回归应固定进入第五段后 miss。"
        );
        _test.Eq(
            missFixture.MasteryService.ResolveActiveSkillMasteryAmount(),
            0,
            "连击熟练度 bonus 必须在对应段命中后发放，第五段 miss 不应给 bonus。"
        );

        RepeatAttackFixture hitFixture = BuildRepeatAttackFixture(
            new[] { true, true, true, true, true, false }
        );
        bool hitExecuted = hitFixture.Resolver.ApplyRepeatAttackSkillResult(
            hitFixture.ActiveUnit,
            hitFixture.TargetUnit,
            hitFixture.SkillDef,
            hitFixture.CombatProfile.effect_defs,
            hitFixture.RepeatEffect,
            new BattleEventBatch()
        );
        _test.True(hitExecuted, "连击段数熟练度回归前置：命中夹具应执行。");
        _test.Eq(
            hitFixture.DamageResolver.call_count,
            6,
            "命中夹具应在第五段命中后继续进入第六段 miss。"
        );
        _test.Eq(
            hitFixture.MasteryService.ResolveActiveSkillMasteryAmount(),
            1,
            "第五段命中后应发放 1 点连击段数 bonus。"
        );
    }

    private RepeatAttackFixture BuildRepeatAttackFixture(bool[] stageSuccesses)
    {
        var runtime = new BattleRuntimeModule();
        runtime.setup();
        var damageResolver = new StageOutcomeDamageResolver();
        foreach (bool stageSuccess in stageSuccesses)
        {
            damageResolver.stage_successes.Add(stageSuccess);
        }
        runtime.ConfigureDamageResolverForTests(damageResolver);

        var masteryService = new BattleSkillMasteryService();
        var resolver = new BattleRepeatAttackResolver();
        resolver.Setup(runtime, masteryService);

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
        targetUnit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), 999);

        var damageEffect = new CombatEffectDef
        {
            effect_type = "damage",
            power = 0,
        };
        var repeatEffect = new CombatEffectDef
        {
            effect_type = "repeat_attack_until_fail",
            stop_on_miss = true,
            stop_on_target_down = true,
            @params = new GDictionary
            {
                ["cost_resource"] = "aura",
                ["follow_up_fixed_cost"] = 0,
                ["follow_up_attack_penalty"] = 0,
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
        unit.SetAnchorCoord(coord);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), 40);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.MpMax), 4);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.StaminaMax), 60);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.AuraMax), 8);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.ActionPoints), Mathf.Max(currentAp, 1));
        unit.UnlockCombatResource(CombatResourceIds.ToStringName(CombatResourceIdKind.Mp));
        unit.UnlockCombatResource(CombatResourceIds.ToStringName(CombatResourceIdKind.Aura));
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.AttackBonus), 80);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.ArmorClass), 5);
        return unit;
    }

    private void TestWeaponAttackQualityReadsWeaponDiceMaxReasonFromResultPayload()
    {
        var service = new BattleSkillMasteryService();
        BattleUnitState source = BuildMasteryUnit("mastery_weapon_source", "player", "hero");
        BattleUnitState target = BuildMasteryUnit("mastery_weapon_target", "enemy");
        SkillDef skillDef = BuildMasterySkill("weapon_quality_contract", "weapon_attack_quality");

        var result = new GDictionary
        {
            ["attack_success"] = true,
            ["damage"] = 5,
            ["damage_events"] = new Godot.Collections.Array
            {
                new GDictionary
                {
                    ["weapon_damage_dice_is_max"] = true,
                    ["weapon_damage_dice_is_max_reason"] = "weapon_dice_max",
                    ["hp_damage"] = 5,
                },
            },
        };

        service.RecordTargetResult(source, target, skillDef, result);

        _test.Eq(
            service.ResolveActiveSkillMasteryAmount(),
            1,
            "weapon_attack_quality 应继续通过 payload 中的 weapon_dice_max reason 触发熟练度。"
        );
    }

    private void TestGuardMasteryGrantReadsSkillDefFromTypedDictionaryKey()
    {
        var service = new BattleSkillMasteryService();
        BattleUnitState attacker = BuildMasteryUnit("guard_attacker", "enemy");
        BattleUnitState target = BuildMasteryUnit("guard_target", "player", "hero");
        target.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = "guarding",
            }
        );

        var result = new AttackEffectResolutionResult
        {
            AttackSuccess = true,
            Damage = 7,
        };
        var effectDefs = new GCombatEffectArray
        {
            new CombatEffectDef
            {
                effect_type = "damage",
                damage_tag = "physical_slash",
            },
        };
        SkillDef guardSkill = BuildMasterySkill("warrior_guard", "incoming_physical_hit");
        var skillDefs = new Dictionary<StringName, SkillDef>
        {
            [new StringName("warrior_guard")] = guardSkill,
        };

        BattleSkillMasteryGrant grant = service.BuildGuardMasteryGrantFromIncomingHitTyped(
            attacker,
            target,
            effectDefs,
            result,
            skillDefs
        );

        _test.True(
            grant != null,
            "guard mastery grant 应继续从 typed skill dictionary key 成功读取 warrior_guard。"
        );
        if (grant != null)
        {
            _test.Eq(
                grant.SkillId.ToString(),
                "warrior_guard",
                "guard mastery grant 应保留 warrior_guard skill id。"
            );
            _test.Eq(grant.Amount, 1, "普通敌方命中 guarding 目标时应给予 1 点熟练度。");
            _test.Eq(grant.MemberId.ToString(), "hero", "guard mastery grant 应归属给被保护的成员。");
        }
    }

    private static BattleUnitState BuildMasteryUnit(
        StringName unitId,
        StringName factionId,
        StringName memberId = default
    )
    {
        var unit = new BattleUnitState
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = factionId,
            source_member_id = memberId,
            current_hp = 30,
            is_alive = true,
        };
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), 30);
        return unit;
    }

    private static SkillDef BuildMasterySkill(StringName skillId, string masteryTriggerMode)
    {
        var combatProfile = new CombatSkillDef
        {
            skill_id = skillId,
            mastery_trigger_mode = masteryTriggerMode,
            mastery_amount_mode = "per_target_rank",
        };
        return new SkillDef
        {
            skill_id = skillId,
            display_name = skillId.ToString(),
            combat_profile = combatProfile,
        };
    }

    private sealed class RepeatAttackFixture
    {
        public BattleRuntimeModule Runtime;
        public StageOutcomeDamageResolver DamageResolver;
        internal BattleSkillMasteryService MasteryService;
        public BattleRepeatAttackResolver Resolver;
        public BattleUnitState ActiveUnit;
        public BattleUnitState TargetUnit;
        public SkillDef SkillDef;
        public CombatSkillDef CombatProfile;
        public CombatEffectDef RepeatEffect;
    }
}
