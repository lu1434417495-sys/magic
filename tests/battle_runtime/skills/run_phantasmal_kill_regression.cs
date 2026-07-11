using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_phantasmal_kill_regression : LifecycleTestSceneTree
{
    private static readonly StringName SkillId = "test_phantasmal_kill";
    private static readonly StringName PhantasmalKillDeathSource = "phantasmal_kill_execute";
    private const int PhantasmalKillDeathPriority = 300;
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestImmuneTargetIsNoOpWithSaveResult();
            TestCriticalSuccessIsNoOpWithSaveResult();
            TestSuccessAppliesOnlyAftershockAndRefreshes();
            TestFailureBelowThresholdExecutesThroughDamageEvent();
            TestFailureAboveThresholdDealsPsychicDamageAndLocksReactions();
            TestCriticalFailureBelowThresholdExecutesThroughDamageEvent();
            TestCriticalFailureAboveThresholdDealsPsychicDamageAndStunsImmediately();
            TestDeathWardCanInterceptExecuteDamage();
            TestPsychicResistanceAndImmunityAffectOnlyNonExecuteDamage();
            TestGroundSkillAffectsAnyTeamInSevenBySevenOnly();

            RequestTestExit(_test.Finish("Phantasmal Kill regression"));
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
            RequestTestExit(_test.Finish("Phantasmal Kill regression"));
        }
    }

    private void TestImmuneTargetIsNoOpWithSaveResult()
    {
        BattleUnitState source = MakeUnit("immune_source", "player", 200, 200);
        BattleUnitState target = MakeUnit("immune_target", "enemy", 200, 40);
        target.save_advantage_tags.Add("illusion_immunity");

        AttackEffectResolutionResult result = new FixedRollDamageResolver().ResolveEffects(
            source,
            target,
            new[] { MakePhantasmalKillEffect() },
            DamageResolutionContext.FromDictionary(
                new GDictionary { ["save_roll_override"] = 1, ["skill_id"] = SkillId }
            )
        );

        _test.False(result.Applied, "immune target should be a no-op.");
        _test.Eq(result.Damage, 0, "immune target should take no damage.");
        _test.Eq(target.current_hp, 40, "immune target HP should not change.");
        _test.False(target.HasStatusEffect("stunned"), "immune natural roll 0 must not become critical failure.");
        AssertSingleSave(result, immune: true, degree: "CriticalSuccess", naturalRoll: 0);
    }

    private void TestCriticalSuccessIsNoOpWithSaveResult()
    {
        BattleUnitState source = MakeUnit("crit_success_source", "player", 200, 200);
        BattleUnitState target = MakeUnit("crit_success_target", "enemy", 200, 100);

        AttackEffectResolutionResult result = new FixedRollDamageResolver().ResolveEffects(
            source,
            target,
            new[] { MakePhantasmalKillEffect() },
            DamageResolutionContext.FromDictionary(
                new GDictionary { ["save_roll_override"] = 20, ["skill_id"] = SkillId }
            )
        );

        _test.False(result.Applied, "critical success should be a no-op.");
        _test.Eq(result.Damage, 0, "critical success should take no damage.");
        _test.Eq(target.GetSortedStatusEffectIdsTyped().Count, 0, "critical success should not apply statuses.");
        AssertSingleSave(result, immune: false, degree: "CriticalSuccess", naturalRoll: 20);
    }

    private void TestSuccessAppliesOnlyAftershockAndRefreshes()
    {
        BattleUnitState source = MakeUnit("success_source", "player", 200, 200);
        BattleUnitState target = MakeUnit("success_target", "enemy", 200, 100);
        target.SetStatusEffect(new BattleStatusEffectState
        {
            status_id = "aftershock",
            source_unit_id = "old_source",
            power = 1,
            stacks = 1,
            duration = 5,
            @params = new GDictionary(),
        });

        AttackEffectResolutionResult result = new FixedRollDamageResolver().ResolveEffects(
            source,
            target,
            new[] { MakePhantasmalKillEffect() },
            DamageResolutionContext.FromDictionary(
                new GDictionary { ["save_roll_override"] = 15, ["skill_id"] = SkillId }
            )
        );

        _test.True(result.Applied, "ordinary success should apply aftershock.");
        _test.Eq(result.Damage, 0, "ordinary success should not deal damage.");
        AssertHasStatusId(result, "aftershock", "ordinary success should report aftershock.");
        _test.Eq(result.StatusEffectIds.Count, 1, "ordinary success should report only one status.");
        _test.True(target.HasStatusEffect("aftershock"), "ordinary success should apply aftershock.");
        _test.False(target.HasStatusEffect("frightened"), "ordinary success should not apply frightened.");
        _test.False(target.HasStatusEffect("reaction_lock"), "ordinary success should not apply reaction_lock.");
        _test.False(target.HasStatusEffect("stunned"), "ordinary success should not apply stunned.");
        _test.Eq(
            target.GetStatusEffect("aftershock")?.duration ?? -1,
            30,
            "repeated aftershock should refresh through semantic merge."
        );
        _test.Eq(
            target.GetStatusEffect("aftershock")?.stacks ?? -1,
            1,
            "aftershock refresh semantic should keep one stack."
        );
        AssertSingleSave(result, immune: false, degree: "Success", naturalRoll: 15);
    }

    private void TestFailureBelowThresholdExecutesThroughDamageEvent()
    {
        BattleUnitState source = MakeUnit("failure_execute_source", "player", 200, 200);
        BattleUnitState target = MakeUnit("failure_execute_target", "enemy", 200, 49);

        AttackEffectResolutionResult result = new FixedRollDamageResolver().ResolveEffects(
            source,
            target,
            new[] { MakePhantasmalKillEffect() },
            DamageResolutionContext.FromDictionary(
                new GDictionary { ["save_roll_override"] = 5, ["skill_id"] = SkillId }
            )
        );

        _test.True(result.Applied, "failure below threshold should apply execute damage.");
        _test.False(target.is_alive, "failure below threshold should kill.");
        _test.Eq(result.Damage, 49, "execute damage should equal current HP snapshot.");
        DamageEventResult damageEvent = FirstDamageEvent(result);
        AssertFatalPhantasmalKillEvent(damageEvent, expectedDamage: 49);
        AssertSingleSave(result, immune: false, degree: "Failure", naturalRoll: 5);
    }

    private void TestFailureAboveThresholdDealsPsychicDamageAndLocksReactions()
    {
        BattleUnitState source = MakeUnit("failure_damage_source", "player", 200, 200);
        BattleUnitState target = MakeUnit("failure_damage_target", "enemy", 200, 100);

        AttackEffectResolutionResult result = new FixedRollDamageResolver(Ones(6)).ResolveEffects(
            source,
            target,
            new[] { MakePhantasmalKillEffect() },
            DamageResolutionContext.FromDictionary(
                new GDictionary { ["save_roll_override"] = 5, ["skill_id"] = SkillId }
            )
        );

        _test.True(result.Applied, "failure above threshold should apply damage and statuses.");
        _test.Eq(result.Damage, 6, "failure above threshold should roll 6d6 psychic damage.");
        _test.Eq(target.current_hp, 94, "failure damage should go through HP damage application.");
        DamageEventResult damageEvent = FirstDamageEvent(result);
        _test.Eq(damageEvent.DamageTag, new StringName("psychic"), "failure damage should be psychic.");
        _test.Eq(damageEvent.DamageDice.Count, 6, "failure branch should roll six damage dice.");
        _test.Eq(damageEvent.DamageDice.Sides, 6, "failure branch should roll d6 damage dice.");
        AssertHasStatusId(result, "frightened", "failure should report frightened.");
        AssertHasStatusId(result, "reaction_lock", "failure should report reaction_lock.");
        _test.True(target.HasStatusEffect("frightened"), "failure should apply frightened.");
        _test.True(target.HasStatusEffect("reaction_lock"), "failure should apply reaction_lock.");
        BattleStatusEffectState reactionLock = target.GetStatusEffect("reaction_lock");
        _test.True(reactionLock?.lock_counterattack == true, "reaction_lock should lock counterattack.");
        _test.True(reactionLock?.lock_guard == true, "reaction_lock should lock guard.");
        AssertSingleSave(result, immune: false, degree: "Failure", naturalRoll: 5);
    }

    private void TestCriticalFailureBelowThresholdExecutesThroughDamageEvent()
    {
        BattleUnitState source = MakeUnit("critical_execute_source", "player", 200, 200);
        BattleUnitState target = MakeUnit("critical_execute_target", "enemy", 200, 69);

        AttackEffectResolutionResult result = new FixedRollDamageResolver().ResolveEffects(
            source,
            target,
            new[] { MakePhantasmalKillEffect() },
            DamageResolutionContext.FromDictionary(
                new GDictionary { ["save_roll_override"] = 1, ["skill_id"] = SkillId }
            )
        );

        _test.True(result.Applied, "critical failure below threshold should apply execute damage.");
        _test.False(target.is_alive, "critical failure below threshold should kill.");
        _test.Eq(result.Damage, 69, "critical execute damage should equal current HP snapshot.");
        AssertFatalPhantasmalKillEvent(FirstDamageEvent(result), expectedDamage: 69);
        AssertSingleSave(result, immune: false, degree: "CriticalFailure", naturalRoll: 1);
    }

    private void TestCriticalFailureAboveThresholdDealsPsychicDamageAndStunsImmediately()
    {
        BattleUnitState source = MakeUnit("critical_damage_source", "player", 200, 200);
        BattleUnitState target = MakeUnit("critical_damage_target", "enemy", 200, 100);
        target.SetCurrentAp(3);
        target.SetCurrentMovePoints(4);

        AttackEffectResolutionResult result = new FixedRollDamageResolver(Ones(10)).ResolveEffects(
            source,
            target,
            new[] { MakePhantasmalKillEffect() },
            DamageResolutionContext.FromDictionary(
                new GDictionary { ["save_roll_override"] = 1, ["skill_id"] = SkillId }
            )
        );

        _test.True(result.Applied, "critical failure above threshold should apply damage and statuses.");
        _test.Eq(result.Damage, 10, "critical failure above threshold should roll 10d6 psychic damage.");
        _test.Eq(target.current_hp, 90, "critical failure damage should go through HP damage application.");
        DamageEventResult damageEvent = FirstDamageEvent(result);
        _test.Eq(damageEvent.DamageTag, new StringName("psychic"), "critical failure damage should be psychic.");
        _test.Eq(damageEvent.DamageDice.Count, 10, "critical failure branch should roll ten damage dice.");
        AssertHasStatusId(result, "frightened", "critical failure should report frightened.");
        AssertHasStatusId(result, "stunned", "critical failure should report stunned.");
        _test.True(target.HasStatusEffect("frightened"), "critical failure should apply frightened.");
        _test.True(target.HasStatusEffect("stunned"), "critical failure should apply stunned.");
        BattleStatusEffectState stunned = target.GetStatusEffect("stunned");
        _test.True(stunned?.lock_counterattack == true, "stunned should lock counterattack.");
        _test.True(stunned?.lock_guard == true, "stunned should lock guard.");
        _test.Eq(target.current_ap, 0, "stunned should clear current AP in the same resolver pass.");
        _test.Eq(
            target.current_move_points,
            0,
            "stunned should clear current move points in the same resolver pass."
        );
        AssertSingleSave(result, immune: false, degree: "CriticalFailure", naturalRoll: 1);
    }

    private void TestDeathWardCanInterceptExecuteDamage()
    {
        FixedRollDamageResolver resolver = new();
        SkillDefinition lastStandSkill = TestSkillDefinitionProjection.LoadSkillDefinition(
            "res://data/configs/skills/warrior_last_stand.tres",
            "phantasmal_kill:warrior_last_stand"
        );
        resolver.SetSkillDefinitions(
            new Dictionary<StringName, SkillDefinition>
            {
                [(StringName)"warrior_last_stand"] = lastStandSkill,
            }
        );
        BattleUnitState source = MakeUnit("ward_source", "player", 200, 200);
        BattleUnitState target = MakeUnit("ward_target", "enemy", 200, 49);
        SetLastStandDeathWard(target, priority: PhantasmalKillDeathPriority);

        AttackEffectResolutionResult result = resolver.ResolveEffects(
            source,
            target,
            new[] { MakePhantasmalKillEffect() },
            DamageResolutionContext.FromDictionary(
                new GDictionary { ["save_roll_override"] = 5, ["skill_id"] = SkillId }
            )
        );

        _test.True(target.is_alive, "matching-priority death ward should intercept execute damage.");
        _test.True(target.current_hp > 0, "death ward interception should leave positive HP.");
        _test.False(target.HasStatusEffect("death_ward"), "triggered death ward should be consumed.");
        _test.True(target.HasStatusEffect("last_stand_active"), "last stand should add its active status.");
        _test.True(result.DamageEvents.Length > 0, "intercepted execute should still produce a damage event.");
        _test.Eq(
            FirstDamageEvent(result).DeathSource,
            PhantasmalKillDeathSource,
            "intercepted execute event should keep the Phantasmal Kill death source."
        );
    }

    private void TestPsychicResistanceAndImmunityAffectOnlyNonExecuteDamage()
    {
        BattleUnitState source = MakeUnit("resist_source", "player", 200, 200);
        BattleUnitState resistant = MakeUnit("psychic_resistant", "enemy", 200, 100);
        resistant.damage_resistances["psychic"] = new StringName("half");
        AttackEffectResolutionResult resistantResult =
            new FixedRollDamageResolver(Ones(6)).ResolveEffects(
                source,
                resistant,
                new[] { MakePhantasmalKillEffect() },
                DamageResolutionContext.FromDictionary(
                    new GDictionary { ["save_roll_override"] = 5, ["skill_id"] = SkillId }
                )
            );

        _test.Eq(resistantResult.Damage, 3, "psychic half resistance should halve failure damage.");
        _test.Eq(
            FirstDamageEvent(resistantResult).MitigationTier,
            MitigationTierKind.Half,
            "psychic half resistance should be recorded on the damage event."
        );
        _test.True(
            resistant.HasStatusEffect("frightened") && resistant.HasStatusEffect("reaction_lock"),
            "psychic resistance should not change the save grade status branch."
        );
        AssertSingleSave(resistantResult, immune: false, degree: "Failure", naturalRoll: 5);

        BattleUnitState immune = MakeUnit("psychic_immune", "enemy", 200, 100);
        immune.damage_resistances["psychic"] = new StringName("immune");
        AttackEffectResolutionResult immuneResult =
            new FixedRollDamageResolver(Ones(6)).ResolveEffects(
                source,
                immune,
                new[] { MakePhantasmalKillEffect() },
                DamageResolutionContext.FromDictionary(
                    new GDictionary { ["save_roll_override"] = 5, ["skill_id"] = SkillId }
                )
            );

        _test.Eq(immuneResult.Damage, 0, "psychic immunity should absorb non-execute damage.");
        _test.Eq(
            FirstDamageEvent(immuneResult).MitigationTier,
            MitigationTierKind.Immune,
            "psychic immunity should be recorded on the damage event."
        );
        _test.True(
            immune.HasStatusEffect("frightened") && immune.HasStatusEffect("reaction_lock"),
            "psychic immunity should not change the save grade status branch."
        );
        AssertSingleSave(immuneResult, immune: false, degree: "Failure", naturalRoll: 5);
    }

    private void TestGroundSkillAffectsAnyTeamInSevenBySevenOnly()
    {
        SkillDefinition skill = MakeGroundPhantasmalKillSkill();
        BattleUnitState source = MakeUnit("ground_source", "player", 200, 200, new Vector2I(0, 5));
        source.known_active_skill_ids.Add(SkillId);
        source.known_skill_level_map[SkillId] = 1;
        source.SetCurrentAp(3);
        source.SetCurrentMp(2000);
        source.UnlockCombatResource("mp");

        BattleUnitState enemyInRange = MakeUnit("enemy_in_range", "enemy", 200, 100, new Vector2I(4, 5));
        BattleUnitState allyInRange = MakeUnit("ally_in_range", "player", 200, 100, new Vector2I(6, 5));
        BattleUnitState enemyOutOfRange = MakeUnit("enemy_out_of_range", "enemy", 200, 100, new Vector2I(9, 5));
        BattleUnitState allyOutOfRange = MakeUnit("ally_out_of_range", "player", 200, 100, new Vector2I(1, 4));

        using BattleTestFixture fixture = BattleTestFixture.CreateFlatBattle(
            "phantasmal_ground_area",
            new Vector2I(11, 11),
            new[] { source, allyInRange, allyOutOfRange },
            new[] { enemyInRange, enemyOutOfRange }
        );
        fixture.Runtime.setup(
            null,
            new Dictionary<StringName, SkillDefinition> { [SkillId] = skill }
        );
        fixture.Runtime.SetupStateForTests(fixture.State);
        BattleTestFixture.ConfigureDamageResolverForTests(
            fixture.Runtime,
            new FixedFailedSaveDamageResolver(Ones(20), new GArray())
        );
        BattleCommand command = MakeGroundCommand(source, new Vector2I(5, 5));
        BattleEventBatch batch = fixture.Runtime.IssueCommand(command);

        _test.True(batch != null, "ground Phantasmal Kill command should execute.");
        _test.True(enemyInRange.HasStatusEffect("stunned"), "7x7 ground skill should affect in-range enemies.");
        _test.True(allyInRange.HasStatusEffect("stunned"), "7x7 ground skill should affect in-range allies.");
        _test.Eq(enemyInRange.current_hp, 90, "in-range enemy should take critical-failure damage.");
        _test.Eq(allyInRange.current_hp, 90, "in-range ally should take critical-failure damage.");
        _test.Eq(enemyOutOfRange.current_hp, 100, "out-of-range enemy should not be affected.");
        _test.Eq(allyOutOfRange.current_hp, 100, "out-of-range ally should not be affected.");
        _test.False(enemyOutOfRange.HasStatusEffect("stunned"), "out-of-range enemy should not gain stunned.");
        _test.False(allyOutOfRange.HasStatusEffect("stunned"), "out-of-range ally should not gain stunned.");

        batch?.Dispose();
        BattleTestFixture.DisposeBattleCommand(command);
    }

    private static CombatEffectDefinition MakePhantasmalKillEffect() =>
        TestSkillDefinitionProjection.BuildEffect(
            "graded_save_execute",
            effectTargetTeamFilter: "any",
            damageTag: "psychic",
            saveDcMode: "static",
            saveDc: 10,
            saveDcSourceAbility: "intelligence",
            saveAbility: "willpower",
            saveTag: "illusion",
            savePartialOnSuccess: false,
            parameters: new Dictionary<string, object>
            {
                ["profile_id"] = "phantasmal_kill",
                ["failure_execute_threshold_fixed"] = 50,
                ["failure_execute_threshold_max_hp_percent"] = 25,
                ["failure_damage_dice_count"] = 6,
                ["failure_damage_dice_sides"] = 6,
                ["failure_frightened_duration_tu"] = 60,
                ["failure_reaction_lock_duration_tu"] = 30,
                ["critical_failure_execute_threshold_max_hp_percent"] = 35,
                ["critical_failure_damage_dice_count"] = 10,
                ["critical_failure_damage_dice_sides"] = 6,
                ["critical_failure_frightened_duration_tu"] = 90,
                ["critical_failure_stunned_duration_tu"] = 30,
                ["success_aftershock_duration_tu"] = 30,
            }
        );

    private static IReadOnlyDictionary<string, object> MakePhantasmalKillParameters() =>
        new Dictionary<string, object>
        {
            ["profile_id"] = "phantasmal_kill",
            ["failure_execute_threshold_fixed"] = 50,
            ["failure_execute_threshold_max_hp_percent"] = 25,
            ["failure_damage_dice_count"] = 6,
            ["failure_damage_dice_sides"] = 6,
            ["failure_frightened_duration_tu"] = 60,
            ["failure_reaction_lock_duration_tu"] = 30,
            ["critical_failure_execute_threshold_max_hp_percent"] = 35,
            ["critical_failure_damage_dice_count"] = 10,
            ["critical_failure_damage_dice_sides"] = 6,
            ["critical_failure_frightened_duration_tu"] = 90,
            ["critical_failure_stunned_duration_tu"] = 30,
            ["success_aftershock_duration_tu"] = 30,
        };

    private static SkillDefinition MakeGroundPhantasmalKillSkill()
    {
        CombatEffectDefinition effect = TestSkillDefinitionProjection.BuildEffect(
            "graded_save_execute",
            effectTargetTeamFilter: "any",
            damageTag: "psychic",
            saveDcMode: "static",
            saveDc: 10,
            saveDcSourceAbility: "intelligence",
            saveAbility: "willpower",
            saveTag: "illusion",
            savePartialOnSuccess: false,
            parameters: MakePhantasmalKillParameters()
        );
        return TestSkillDefinitionProjection.BuildSkill(
            SkillId,
            displayName: "Test Phantasmal Kill",
            maxLevel: 9,
            nonCoreMaxLevel: 7,
            combatProfile: TestSkillDefinitionProjection.BuildCombatProfile(
                SkillId,
                effects: new[] { effect },
                targetMode: "ground",
                targetTeamFilter: "any",
                targetSelectionMode: "single_coord",
                rangeValue: 10,
                areaPattern: "square",
                areaValue: 3,
                apCost: 0,
                mpCost: 0,
                cooldownTu: 0
            )
        );
    }

    private static BattleUnitState MakeUnit(
        StringName unitId,
        StringName factionId,
        int maxHp,
        int currentHp,
        Vector2I coord = default
    )
    {
        BattleUnitState unit = new()
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = factionId,
            control_mode = "manual",
            current_hp = currentHp,
            current_mp = 0,
            current_ap = 2,
            current_move_points = 2,
            current_stamina = 20,
            is_alive = currentHp > 0,
        };
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.HpMax), maxHp);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.MpMax), 2000);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.ActionPoints), 3);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.AttackBonus), 10);
        unit.attribute_snapshot.SetValue(AttributeService.ToStringName(AttributeIdKind.ArmorClass), 10);
        unit.attribute_snapshot.SetValue("intelligence", 10);
        unit.attribute_snapshot.SetValue("intelligence_modifier", 0);
        unit.attribute_snapshot.SetValue("willpower", 10);
        unit.attribute_snapshot.SetValue("willpower_modifier", 0);
        unit.SetAnchorCoord(coord);
        return unit;
    }

    private static void SetLastStandDeathWard(BattleUnitState unit, int priority)
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

    private static BattleCommand MakeGroundCommand(BattleUnitState source, Vector2I targetCoord)
    {
        BattleCommand command = new()
        {
            command_type = "skill",
            unit_id = source.unit_id,
            skill_id = SkillId,
            skill_entry_id = BattleSkillEntryIds.KnownSkill(SkillId),
            target_coord = targetCoord,
        };
        command.AddTargetCoord(targetCoord);
        return command;
    }

    private static GArray Ones(int count)
    {
        GArray rolls = new();
        for (int index = 0; index < count; index++)
            rolls.Add(1);
        return rolls;
    }

    private static DamageEventResult FirstDamageEvent(AttackEffectResolutionResult result)
    {
        return result.DamageEvents != null && result.DamageEvents.Length > 0
            ? result.DamageEvents[0]
            : new DamageEventResult();
    }

    private void AssertSingleSave(
        AttackEffectResolutionResult result,
        bool immune,
        string degree,
        int naturalRoll
    )
    {
        int saveCount = result.SaveResults?.Length ?? 0;
        _test.Eq(saveCount, 1, "graded save execute should include exactly one save result.");
        if (saveCount <= 0)
            return;
        SaveResolutionResult save = result.SaveResults[0];
        _test.True(save.HasSave, "save result should be marked as a save.");
        _test.Eq(save.Immune, immune, "save result should preserve immunity.");
        _test.Eq(save.Degree, degree, "save result should preserve save degree.");
        _test.Eq(save.NaturalRoll, naturalRoll, "save result should preserve natural roll.");
    }

    private void AssertFatalPhantasmalKillEvent(
        DamageEventResult damageEvent,
        int expectedDamage
    )
    {
        _test.Eq(damageEvent.DamageTag, new StringName("psychic"), "execute damage should be psychic.");
        _test.Eq(damageEvent.Damage, expectedDamage, "execute damage event should record HP damage.");
        _test.Eq(damageEvent.MinHpAfterDamage, 0, "execute damage should allow HP to reach zero.");
        _test.True(damageEvent.BypassShield, "execute damage should bypass shield.");
        _test.False(damageEvent.BypassDeathPrevention, "execute damage should allow death prevention.");
        _test.Eq(
            damageEvent.DeathSource,
            PhantasmalKillDeathSource,
            "execute death source should be Phantasmal Kill-specific."
        );
        _test.Ne(
            damageEvent.DeathSource,
            BattleDeathResolutionRules.PowerWordKillExecuteDeathSource,
            "execute death source should not reuse Power Word Kill."
        );
        _test.Eq(
            damageEvent.DeathSourcePriority,
            PhantasmalKillDeathPriority,
            "Phantasmal Kill is an area execute and should use lower death authority than Power Word Kill."
        );
    }

    private void AssertHasStatusId(
        AttackEffectResolutionResult result,
        StringName statusId,
        string message
    )
    {
        _test.True(result.StatusEffectIds != null && result.StatusEffectIds.Contains(statusId), message);
    }
}
