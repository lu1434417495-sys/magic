using System;
using System.Collections.Generic;
using Godot;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_equipment_fatal_intercept_runtime_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestAuthoringValidationAndProjection();
        TestResolutionOrderAndFailedAttemptConsumption();
        TestLaterFatalEventAdvancesToNextCandidate();
        TestProtectionPriorityDoesNotConsume();
        TestPreviewIsReadOnlyAndDoesNotConsumeForcedRolls();
        TestPerWorldDayAttemptUsage();
        TestPerWorldMonthAttemptUsage();
        TestStatusActivatedFatalLedgerAndSuccessStatusAction();
        TestDamageResolverOrderingBypassAndLastStandLanding();

        RequestTestExit(_test.Finish("Equipment fatal intercept runtime regression"));
    }

    private void TestAuthoringValidationAndProjection()
    {
        var binding = new EquipmentAbilityBindingDef
        {
            binding_id = "binding.test.fatal",
            trait_id = "trait.test.fatal",
            override_mode = "add",
        };
        binding.required_effective_trait_ids.Add("trait.test.threshold");
        var recoveryTerm = new DiceExpressionTermDef { dice_count = 2, dice_sides = 6 };
        var recoveryDice = new DiceExpressionDef();
        recoveryDice.terms.Add(recoveryTerm);
        var intercept = new EquipmentFatalInterceptDef
        {
            intercept_id = "second_wind",
            resolution_order = 20,
            protection_priority = 100,
            usage_period_kind = "per_battle",
            max_attempts_per_period = 1,
            consume_on_attempt = true,
            recovery_kind = "hp_dice",
            recovery_dice = recoveryDice,
        };
        binding.fatal_intercepts.Add(intercept);

        var validator = new EquipmentAbilityBindingValidator(
            EquipmentAbilityBuiltInHandlerSpecs.BuildConditionSpecs(),
            EquipmentAbilityBuiltInHandlerSpecs.BuildActionSpecs(),
            EquipmentAbilityBuiltInHandlerSpecs.BuildTriggerTimingSpecs()
        );
        var errors = new List<string>();
        validator.ValidateBinding(
            binding,
            new EquipmentAbilityContentValidationContext
            {
                KnownTraitIds = new HashSet<StringName>
                {
                    "trait.test.fatal",
                    "trait.test.threshold",
                },
                KnownSkillIds = new HashSet<StringName>(),
                WindupSkillIds = new HashSet<StringName>(),
                KnownStatusIds = new HashSet<StringName>(),
            },
            new Dictionary<StringName, EquipmentAbilityBindingDefinition>(),
            errors
        );
        _test.Eq(errors.Count, 0, $"valid fatal intercept authoring should validate: {string.Join(" | ", errors)}");

        EquipmentAbilityBindingDefinition projected =
            EquipmentAbilityDefinitionProjection.ProjectBinding(binding);
        _test.Eq(projected.FatalIntercepts.Count, 1, "fatal intercept should project into immutable binding definition.");
        _test.Eq(projected.FatalIntercepts[0].InterceptId, new StringName("second_wind"), "projection should preserve intercept_id.");
        _test.Eq(projected.FatalIntercepts[0].ResolutionOrder, 20, "projection should preserve resolution_order.");
        _test.Eq(projected.FatalIntercepts[0].ProtectionPriority, 100, "projection should preserve protection_priority.");
        _test.Eq(projected.FatalIntercepts[0].RecoveryDice.Terms.Count, 1, "projection should preserve recovery dice.");
        _test.True(
            projected.RequiredEffectiveTraitIds.Contains("trait.test.threshold"),
            "projection should preserve required effective trait gates."
        );

        errors.Clear();
        validator.ValidateBinding(
            binding,
            new EquipmentAbilityContentValidationContext
            {
                KnownTraitIds = new HashSet<StringName> { "trait.test.fatal" },
                KnownSkillIds = new HashSet<StringName>(),
                WindupSkillIds = new HashSet<StringName>(),
                KnownStatusIds = new HashSet<StringName>(),
            },
            new Dictionary<StringName, EquipmentAbilityBindingDefinition>(),
            errors
        );
        _test.True(
            ContainsError(errors, "EQA_REFERENCE_MISSING_REQUIRED_EFFECTIVE_TRAIT"),
            "unknown required effective trait gates should fail validation."
        );

        intercept.recovery_kind = "max_hp_percent";
        intercept.recovery_dice = null;
        intercept.recovery_percent_basis_points = 0;
        errors.Clear();
        validator.ValidateBinding(
            binding,
            new EquipmentAbilityContentValidationContext
            {
                KnownTraitIds = new HashSet<StringName>
                {
                    "trait.test.fatal",
                    "trait.test.threshold",
                },
                KnownSkillIds = new HashSet<StringName>(),
                WindupSkillIds = new HashSet<StringName>(),
                KnownStatusIds = new HashSet<StringName>(),
            },
            new Dictionary<StringName, EquipmentAbilityBindingDefinition>(),
            errors
        );
        _test.True(
            ContainsError(errors, "EQA_FATAL_INTERCEPT_RECOVERY_PERCENT_INVALID"),
            "invalid max-HP recovery percentage should fail closed."
        );
    }

    private void TestResolutionOrderAndFailedAttemptConsumption()
    {
        EquipmentFatalInterceptDefinition laterAutomatic = PercentIntercept(
            "later_automatic",
            order: 20,
            protectionPriority: 100,
            usagePeriod: EquipmentAbilityUsagePeriodKind.PerBattle,
            recoveryBasisPoints: 2500
        );
        EquipmentFatalInterceptDefinition earlierRoll = DiceIntercept(
            "earlier_roll",
            order: 10,
            protectionPriority: 100,
            usagePeriod: EquipmentAbilityUsagePeriodKind.PerBattle,
            recoverySides: 8,
            rollGate: D100Gate(50)
        );
        EquipmentAbilityBindingDefinition binding = Binding(
            "binding.test.order",
            laterAutomatic,
            earlierRoll
        );

        using RuntimeFixture fixture = RuntimeFixture.Create(binding);
        fixture.Service.ConfigureRollGateValuesForTests(new[] { 100 });
        BattleFatalInterceptResult result = fixture.Service.ResolveFatalIntercept(
            FatalContext(fixture.Target)
        );

        _test.True(result.Intercepted, "later automatic candidate should intercept after earlier roll failure.");
        _test.Eq(result.Attempts.Count, 2, "arbiter should retain both ordered attempts.");
        _test.Eq(result.Attempts[0].InterceptId, new StringName("earlier_roll"), "lower resolution_order should attempt first regardless authored array order.");
        _test.Eq(result.Attempts[0].Outcome, BattleFatalInterceptAttemptOutcomeKind.RollFailed, "first candidate should record roll failure.");
        _test.True(result.Attempts[0].UsageConsumed, "consume_on_attempt should consume usage on roll failure.");
        _test.Eq(result.Attempts[1].InterceptId, new StringName("later_automatic"), "arbiter should continue to the next candidate after failure.");
        _test.Eq(result.Attempts[1].Outcome, BattleFatalInterceptAttemptOutcomeKind.Intercepted, "second candidate should win.");
        _test.Eq(fixture.Target.GetCurrentHp(), 25, "25 percent max-HP recovery should restore 25 HP from max 100.");

        BattleEquipmentAbilitySourceReadView source = fixture.FindSource(binding.BindingId);
        _test.Eq(
            EquipmentAbilityUsageRuntime.GetFatalInterceptAttemptUsedCount(
                fixture.Target,
                source,
                binding,
                earlierRoll,
                worldStep: 0
            ),
            1,
            "failed attempt should consume its per-battle usage."
        );
        _test.Eq(
            EquipmentAbilityUsageRuntime.GetFatalInterceptAttemptUsedCount(
                fixture.Target,
                source,
                binding,
                laterAutomatic,
                worldStep: 0
            ),
            1,
            "successful candidate should consume its per-battle usage."
        );
    }

    private void TestProtectionPriorityDoesNotConsume()
    {
        EquipmentFatalInterceptDefinition intercept = PercentIntercept(
            "normal_only",
            order: 10,
            protectionPriority: 100,
            usagePeriod: EquipmentAbilityUsagePeriodKind.PerBattle,
            recoveryBasisPoints: 5000
        );
        EquipmentAbilityBindingDefinition binding = Binding(
            "binding.test.priority",
            intercept
        );
        using RuntimeFixture fixture = RuntimeFixture.Create(binding);

        BattleFatalInterceptResult result = fixture.Service.ResolveFatalIntercept(
            FatalContext(
                fixture.Target,
                BattleDeathResolutionRules.PowerWordKillExecuteContext()
            )
        );
        _test.False(result.Intercepted, "priority 100 candidate must not block priority 900 death.");
        _test.Eq(result.Attempts.Count, 1, "blocked candidate should remain visible in typed result.");
        _test.Eq(result.Attempts[0].Outcome, BattleFatalInterceptAttemptOutcomeKind.BlockedByPriority, "priority rejection should be explicit.");
        _test.False(result.Attempts[0].UsageConsumed, "priority rejection must not consume usage.");
        _test.Eq(
            EquipmentAbilityUsageRuntime.GetFatalInterceptAttemptUsedCount(
                fixture.Target,
                fixture.FindSource(binding.BindingId),
                binding,
                intercept,
                worldStep: 0
            ),
            0,
            "blocked candidate should retain all attempts."
        );
    }

    private void TestLaterFatalEventAdvancesToNextCandidate()
    {
        EquipmentFatalInterceptDefinition first = PercentIntercept(
            "first_segment_guard",
            order: 10,
            protectionPriority: 100,
            usagePeriod: EquipmentAbilityUsagePeriodKind.PerBattle,
            recoveryBasisPoints: 1000
        );
        EquipmentFatalInterceptDefinition second = PercentIntercept(
            "second_segment_guard",
            order: 20,
            protectionPriority: 100,
            usagePeriod: EquipmentAbilityUsagePeriodKind.PerBattle,
            recoveryBasisPoints: 2000
        );
        EquipmentAbilityBindingDefinition binding = Binding(
            "binding.test.multi_event",
            first,
            second
        );
        using RuntimeFixture fixture = RuntimeFixture.Create(binding);

        BattleFatalInterceptResult firstEvent = fixture.Service.ResolveFatalIntercept(
            FatalContext(fixture.Target)
        );
        _test.Eq(firstEvent.WinningInterceptId, first.InterceptId, "first fatal event should stop at the first successful candidate.");
        _test.Eq(firstEvent.Attempts.Count, 1, "later candidates must remain untouched after first success.");
        fixture.Target.SetCurrentHp(5);

        BattleFatalInterceptResult laterEvent = fixture.Service.ResolveFatalIntercept(
            FatalContext(fixture.Target)
        );
        _test.True(laterEvent.Intercepted, "a later damage event should independently run fatal arbitration.");
        _test.Eq(laterEvent.Attempts[0].Outcome, BattleFatalInterceptAttemptOutcomeKind.Unavailable, "spent first candidate should be skipped without reuse.");
        _test.Eq(laterEvent.WinningInterceptId, second.InterceptId, "later event should advance to the next authored candidate.");
        _test.Eq(fixture.Target.GetCurrentHp(), 20, "second candidate should apply its own recovery definition.");
    }

    private void TestPreviewIsReadOnlyAndDoesNotConsumeForcedRolls()
    {
        EquipmentFatalInterceptDefinition intercept = DiceIntercept(
            "preview_roll",
            order: 10,
            protectionPriority: 100,
            usagePeriod: EquipmentAbilityUsagePeriodKind.PerBattle,
            recoverySides: 20,
            rollGate: D100Gate(50)
        );
        EquipmentAbilityBindingDefinition binding = Binding(
            "binding.test.preview",
            intercept
        );
        using RuntimeFixture fixture = RuntimeFixture.Create(binding);
        fixture.Service.ConfigureRollGateValuesForTests(new[] { 40 });
        fixture.Service.ConfigureFatalRecoveryValuesForTests(new[] { 17 });
        BattleEquipmentAbilitySourceReadView source = fixture.FindSource(binding.BindingId);

        BattleFatalInterceptPreviewResult projection = fixture.Service.PreviewFatalIntercept(
            FatalContext(fixture.Target)
        );
        _test.Eq(projection.InterceptProbabilityBasisPoints, 5000, "1D100 <= 50 preview should expose 5000 bp success probability.");
        _test.Eq(fixture.Target.GetCurrentHp(), 5, "read-only projection must not alter HP.");
        _test.Eq(
            EquipmentAbilityUsageRuntime.GetFatalInterceptAttemptUsedCount(
                fixture.Target,
                source,
                binding,
                intercept,
                worldStep: 0
            ),
            0,
            "read-only projection must not consume usage."
        );

        fixture.Runtime.GetDamageResolver().PreviewDamageEffectTyped(
            fixture.Source,
            fixture.Target,
            TestSkillDefinitionProjection.BuildEffect(
                "damage",
                power: 10,
                damageTag: "physical_slash"
            ),
            DamageResolutionContext.Empty().WithBattleState(new BattleState())
        );
        _test.Eq(fixture.Target.GetCurrentHp(), 5, "canonical damage preview must keep the real target unchanged.");
        _test.Eq(
            EquipmentAbilityUsageRuntime.GetFatalInterceptAttemptUsedCount(
                fixture.Target,
                source,
                binding,
                intercept,
                worldStep: 0
            ),
            0,
            "canonical damage preview must not consume attempt usage."
        );

        BattleFatalInterceptResult committed = fixture.Service.ResolveFatalIntercept(
            FatalContext(fixture.Target)
        );
        _test.True(committed.Intercepted, "forced roll 40 should remain queued after previews and pass <= 50.");
        _test.Eq(fixture.Target.GetCurrentHp(), 17, "forced recovery value should remain queued after previews.");
    }

    private void TestPerWorldDayAttemptUsage()
    {
        EquipmentFatalInterceptDefinition intercept = DiceIntercept(
            "daily_rebirth",
            order: 10,
            protectionPriority: 100,
            usagePeriod: EquipmentAbilityUsagePeriodKind.PerWorldDay,
            recoverySides: 12
        );
        EquipmentAbilityBindingDefinition binding = Binding(
            "binding.test.daily",
            intercept
        );
        using RuntimeFixture fixture = RuntimeFixture.Create(binding);
        fixture.Service.ConfigureFatalRecoveryValuesForTests(new[] { 9, 11 });

        BattleFatalInterceptResult first = fixture.Service.ResolveFatalIntercept(
            FatalContext(fixture.Target, worldStep: 100)
        );
        _test.True(first.Intercepted, "first per-world-day attempt should be available.");
        fixture.Target.SetCurrentHp(5);
        BattleFatalInterceptResult second = fixture.Service.ResolveFatalIntercept(
            FatalContext(fixture.Target, worldStep: 100)
        );
        _test.False(second.Intercepted, "second attempt in the same world day should be exhausted.");
        _test.Eq(second.Attempts[0].Outcome, BattleFatalInterceptAttemptOutcomeKind.Unavailable, "daily exhaustion should be explicit.");

        EquipmentInstanceState instance = fixture.Target
            .GetEquipmentView()
            .GetEquippedInstance("main_hand");
        _test.Eq(instance.ability_usage_periods.Count, 1, "daily attempt should reuse existing equipment usage persistence owner.");
        _test.Eq(instance.ability_usage_periods[0].UsedCount, 1, "daily attempt should commit exactly once.");

        BattleFatalInterceptResult nextDay = fixture.Service.ResolveFatalIntercept(
            FatalContext(fixture.Target, worldStep: 115)
        );
        _test.True(nextDay.Intercepted, "a new world-day period should make the attempt available again.");
        _test.Eq(fixture.Target.GetCurrentHp(), 11, "same-day exhaustion must not consume the next recovery roll.");
        _test.Eq(instance.ability_usage_periods.Count, 2, "new world day should append a distinct existing-schema usage period.");
    }

    private void TestPerWorldMonthAttemptUsage()
    {
        var authoredBinding = new EquipmentAbilityBindingDef
        {
            binding_id = "binding.test.monthly.authoring",
            trait_id = "trait.test.monthly.authoring",
            override_mode = "add",
        };
        authoredBinding.fatal_intercepts.Add(
            new EquipmentFatalInterceptDef
            {
                intercept_id = "monthly_rebirth",
                resolution_order = 500,
                protection_priority = 100,
                usage_period_kind = "per_world_month",
                max_attempts_per_period = 1,
                consume_on_attempt = true,
                recovery_kind = "max_hp_percent",
                recovery_percent_basis_points = 3000,
            }
        );
        var validator = new EquipmentAbilityBindingValidator(
            EquipmentAbilityBuiltInHandlerSpecs.BuildConditionSpecs(),
            EquipmentAbilityBuiltInHandlerSpecs.BuildActionSpecs(),
            EquipmentAbilityBuiltInHandlerSpecs.BuildTriggerTimingSpecs()
        );
        var errors = new List<string>();
        validator.ValidateBinding(
            authoredBinding,
            new EquipmentAbilityContentValidationContext
            {
                KnownTraitIds = new HashSet<StringName> { "trait.test.monthly.authoring" },
                KnownSkillIds = new HashSet<StringName>(),
                WindupSkillIds = new HashSet<StringName>(),
                KnownStatusIds = new HashSet<StringName>(),
            },
            new Dictionary<StringName, EquipmentAbilityBindingDefinition>(),
            errors
        );
        _test.Eq(
            errors.Count,
            0,
            $"per-world-month fatal intercept authoring should validate: {string.Join(" | ", errors)}"
        );
        EquipmentAbilityBindingDefinition projected =
            EquipmentAbilityDefinitionProjection.ProjectBinding(authoredBinding);
        _test.Eq(
            projected.FatalIntercepts[0].UsagePeriodKind,
            EquipmentAbilityUsagePeriodKind.PerWorldMonth,
            "projection should preserve per_world_month usage."
        );

        EquipmentFatalInterceptDefinition intercept = PercentIntercept(
            "monthly_rebirth",
            order: 500,
            protectionPriority: 100,
            usagePeriod: EquipmentAbilityUsagePeriodKind.PerWorldMonth,
            recoveryBasisPoints: 3000
        );
        EquipmentAbilityBindingDefinition binding = Binding(
            "binding.test.monthly",
            intercept
        );
        using RuntimeFixture fixture = RuntimeFixture.Create(binding);

        BattleFatalInterceptPreviewResult preview = fixture.Service.PreviewFatalIntercept(
            FatalContext(fixture.Target, worldStep: 449)
        );
        _test.True(preview.GuaranteedIntercept, "monthly intercept preview should see the unused month-0 charge.");

        BattleFatalInterceptResult first = fixture.Service.ResolveFatalIntercept(
            FatalContext(fixture.Target, worldStep: 449)
        );
        _test.True(first.Intercepted, "first attempt in world month 0 should be available.");
        _test.Eq(fixture.Target.GetCurrentHp(), 30, "monthly max-HP recovery should restore exactly 30%.");
        fixture.Target.SetCurrentHp(5);

        BattleFatalInterceptResult sameMonth = fixture.Service.ResolveFatalIntercept(
            FatalContext(fixture.Target, worldStep: 449)
        );
        _test.False(sameMonth.Intercepted, "a second attempt in the same world month should be exhausted.");
        _test.Eq(
            sameMonth.Attempts[0].Outcome,
            BattleFatalInterceptAttemptOutcomeKind.Unavailable,
            "monthly exhaustion should be explicit."
        );

        BattleFatalInterceptResult nextMonth = fixture.Service.ResolveFatalIntercept(
            FatalContext(fixture.Target, worldStep: 450)
        );
        _test.True(nextMonth.Intercepted, "the first step of the next world month should refresh availability.");
        EquipmentInstanceState instance = fixture.Target
            .GetEquipmentView()
            .GetEquippedInstance("main_hand");
        _test.Eq(instance.ability_usage_periods.Count, 2, "two used world months should persist as two period records.");
        _test.Eq(instance.ability_usage_periods[0].PeriodKind, "per_world_month", "month-0 usage should persist the monthly kind.");
        _test.Eq(instance.ability_usage_periods[0].PeriodIndex, 0, "step 449 should belong to world month 0.");
        _test.Eq(instance.ability_usage_periods[1].PeriodIndex, 1, "step 450 should belong to world month 1.");
    }

    private void TestStatusActivatedFatalLedgerAndSuccessStatusAction()
    {
        var applyForm = new EquipmentAbilityActionDefinition
        {
            ActionId = "apply_reborn_form",
            Kind = "apply_status",
            PayloadDefinition = new ApplyStatusActionPayloadDefinition
            {
                TargetSelector = "source",
                StatusId = "test_reborn_form",
                DurationTu = 120,
                DamageTag = "fire",
                MitigationTier = "immune",
                DisplayLabel = "Test Reborn Form",
            },
        };
        EquipmentFatalInterceptDefinition intercept = new()
        {
            InterceptId = "status_rebirth",
            ResolutionOrder = 100,
            ProtectionPriority = 100,
            UsagePeriodKind = EquipmentAbilityUsagePeriodKind.PerBattle,
            MaxAttemptsPerPeriod = 1,
            ConsumeOnAttempt = true,
            RecoveryKind = EquipmentFatalInterceptRecoveryKind.HpDice,
            RecoveryDice = Dice(1, 10),
            SuccessActions = new[] { applyForm },
        };
        EquipmentAbilityBindingDefinition binding = new()
        {
            BindingId = "binding.test.status_rebirth",
            TraitId = "trait.test.status_rebirth",
            ActivationStatusId = "test_blessing",
            FatalIntercepts = new[] { intercept },
        };
        using RuntimeFixture fixture = RuntimeFixture.Create(binding);
        fixture.Target.ReplaceEquipmentAbilityProjectionTyped(
            Array.Empty<BattleEquipmentAbilitySourceState>(),
            temporalProgressModifiers: null
        );
        fixture.Target.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = "test_blessing",
                duration = 120,
            }
        );
        fixture.Service.ConfigureFatalRecoveryValuesForTests(new[] { 6, 9 });

        BattleFatalInterceptResult first = fixture.Service.ResolveFatalIntercept(
            FatalContext(fixture.Target)
        );
        _test.True(first.Intercepted, "a status-derived binding should work without an equipment projection source.");
        _test.Eq(fixture.Target.GetCurrentHp(), 6, "status-derived fatal recovery should use the configured recovery roll.");
        BattleStatusEffectState form = fixture.Target.GetStatusEffect("test_reborn_form");
        _test.True(form != null, "fatal success actions should apply the configured status after recovery.");
        _test.Eq(form?.duration ?? -1, 120, "fatal success status should preserve TU duration.");
        _test.Eq(form?.damage_tag ?? "", new StringName("fire"), "fatal success status should preserve its damage tag.");
        _test.Eq(form?.mitigation_tier ?? "", new StringName("immune"), "fatal success status should preserve its mitigation tier.");

        fixture.Target.SetCurrentHp(1);
        fixture.Target.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = "test_blessing",
                duration = 120,
                source_unit_id = "another_badge_owner",
            }
        );
        BattleFatalInterceptResult refreshed = fixture.Service.ResolveFatalIntercept(
            FatalContext(fixture.Target)
        );
        _test.False(refreshed.Intercepted, "refreshing or replacing the activation status must not reset its beneficiary battle ledger.");
        _test.Eq(
            refreshed.Attempts[0].Outcome,
            BattleFatalInterceptAttemptOutcomeKind.Unavailable,
            "a refreshed status should still see the spent per-battle intercept."
        );
    }

    private void TestDamageResolverOrderingBypassAndLastStandLanding()
    {
        using var resolver = new BattleDamageResolver();
        var probe = new ProbeFatalInterceptArbiter { ShouldIntercept = true, RecoveryHp = 7 };
        resolver.SetFatalInterceptArbiter(probe);
        BattleUnitState source = Unit("resolver_source", "enemy", hp: 20, maxHp: 20);
        BattleUnitState traitTarget = Unit("trait_target", "player", hp: 5, maxHp: 20);
        BattleUnitState target = Unit("resolver_target", "player", hp: 5, maxHp: 20);
        BattleUnitState bypassTarget = Unit("bypass_target", "player", hp: 5, maxHp: 20);
        BattleUnitState executeTraitTarget = Unit(
            "execute_trait_target",
            "player",
            hp: 1,
            maxHp: 20
        );
        try
        {
            StringName relentless = TraitContentRules.ToStringName(
                TraitEffectKind.RelentlessEndurance
            );
            traitTarget.ReplaceEffectiveTraitsTyped(
                TraitTestData.EffectiveTraits(
                    TraitTestData.EffectiveTrait(
                        relentless,
                        relentless,
                        TraitTriggerContentRules.ToStringName(
                            TraitTriggerKind.OnFatalDamage
                        ),
                        "per_battle",
                        "battle_start",
                        effectType: relentless
                    )
                )
            );
            resolver.ApplyTaggedDirectDamageToTargetTyped(
                traitTarget,
                10,
                "physical_slash",
                source,
                new BattleState()
            );
            _test.Eq(traitTarget.GetCurrentHp(), 1, "fatal trait should resolve before the fatal arbiter.");
            _test.Eq(probe.ResolveCount, 0, "successful fatal trait must not consume or invoke fatal intercept candidates.");

            target.SetStatusEffect(
                new BattleStatusEffectState
                {
                    status_id = "death_ward",
                    source_skill_id = "missing_last_stand_source",
                    source_skill_level = 1,
                    death_prevention_priority = 100,
                }
            );
            resolver.ApplyTaggedDirectDamageToTargetTyped(
                target,
                10,
                "physical_slash",
                source,
                new BattleState()
            );
            _test.Eq(probe.ResolveCount, 1, "fatal arbiter should run after an unsuccessful Last Stand attempt.");
            _test.False(probe.TargetWasAliveAtResolve, "Last Stand branch should finish before fatal arbiter is entered.");
            _test.True(target.IsAlive(), "fatal arbiter recovery should prevent outer death handling.");
            _test.Eq(target.GetCurrentHp(), 7, "resolver should preserve arbiter recovery HP.");

            executeTraitTarget.ReplaceEffectiveTraitsTyped(
                TraitTestData.EffectiveTraits(
                    TraitTestData.EffectiveTrait(
                        relentless,
                        relentless,
                        TraitTriggerContentRules.ToStringName(
                            TraitTriggerKind.OnFatalDamage
                        ),
                        "per_battle",
                        "battle_start",
                        effectType: relentless
                    )
                )
            );
            CombatEffectDefinition powerWordKill = TestSkillDefinitionProjection.BuildEffect(
                "execute",
                saveDcMode: "static",
                saveDc: 10,
                saveAbility: "willpower",
                saveTag: "magic",
                thresholdMaxHpRatioPercent: 20,
                thresholdLevelAnchor: 17,
                thresholdLevelBonusPerDelta: 5,
                thresholdCapMaxHpRatioPercent: 50,
                soulFractureDurationTu: 60,
                healMultiplierPercent: 50,
                shieldGainMultiplierPercent: 50
            );
            resolver.ResolveEffects(
                source,
                executeTraitTarget,
                new[] { powerWordKill },
                DamageResolutionContext.FromDictionary(
                    new Godot.Collections.Dictionary { ["save_roll_override"] = 1 }
                )
            );
            _test.Eq(
                executeTraitTarget.GetCurrentHp(),
                7,
                "Power Word Kill must bypass normal-priority fatal traits and reach the fatal arbiter."
            );
            _test.Eq(
                probe.ResolveCount,
                2,
                "Power Word Kill should skip the legacy fatal trait instead of stopping before the fatal arbiter."
            );

            using GodotProjectionLease<Godot.Collections.Dictionary> bypassLease =
                DamageApplicationInput.Create(
                    new DamageEventResult
                    {
                        DamageTag = "physical_slash",
                        ResolvedDamage = 10,
                    },
                    resolvedDamage: 10,
                    bypassDeathPrevention: true
                ).ToDictionaryLease();
            resolver.ApplyDirectDamageToTargetTyped(
                bypassTarget,
                bypassLease.Value,
                source,
                new BattleState()
            );
            _test.Eq(probe.ResolveCount, 2, "bypass_death_prevention must not call fatal arbiter.");
            _test.False(bypassTarget.IsAlive(), "bypass death should remain fatal.");
        }
        finally
        {
            resolver.SetFatalInterceptArbiter(null);
            BattleTestFixture.DisposeBattleUnit(source);
            BattleTestFixture.DisposeBattleUnit(traitTarget);
            BattleTestFixture.DisposeBattleUnit(target);
            BattleTestFixture.DisposeBattleUnit(bypassTarget);
            BattleTestFixture.DisposeBattleUnit(executeTraitTarget);
        }
    }

    private static EquipmentAbilityBindingDefinition Binding(
        StringName bindingId,
        params EquipmentFatalInterceptDefinition[] intercepts
    ) =>
        new()
        {
            BindingId = bindingId,
            TraitId = $"trait.{bindingId}",
            FatalIntercepts = intercepts ?? Array.Empty<EquipmentFatalInterceptDefinition>(),
        };

    private static EquipmentFatalInterceptDefinition PercentIntercept(
        StringName id,
        int order,
        int protectionPriority,
        EquipmentAbilityUsagePeriodKind usagePeriod,
        int recoveryBasisPoints
    ) =>
        new()
        {
            InterceptId = id,
            ResolutionOrder = order,
            ProtectionPriority = protectionPriority,
            UsagePeriodKind = usagePeriod,
            MaxAttemptsPerPeriod = 1,
            ConsumeOnAttempt = true,
            RecoveryKind = EquipmentFatalInterceptRecoveryKind.MaxHpPercent,
            RecoveryPercentBasisPoints = recoveryBasisPoints,
        };

    private static EquipmentFatalInterceptDefinition DiceIntercept(
        StringName id,
        int order,
        int protectionPriority,
        EquipmentAbilityUsagePeriodKind usagePeriod,
        int recoverySides,
        EquipmentRollGateDefinition rollGate = null
    ) =>
        new()
        {
            InterceptId = id,
            ResolutionOrder = order,
            ProtectionPriority = protectionPriority,
            UsagePeriodKind = usagePeriod,
            MaxAttemptsPerPeriod = 1,
            ConsumeOnAttempt = true,
            RollGate = rollGate,
            RecoveryKind = EquipmentFatalInterceptRecoveryKind.HpDice,
            RecoveryDice = Dice(1, recoverySides),
        };

    private static EquipmentRollGateDefinition D100Gate(int threshold) =>
        new()
        {
            RngStream = "test.fatal_intercept",
            Roll = Dice(1, 100),
            Compare = "lte",
            Threshold = threshold,
        };

    private static DiceExpressionDefinition Dice(int count, int sides) =>
        new()
        {
            Terms = new[]
            {
                new DiceExpressionTermDefinition
                {
                    DiceCount = count,
                    DiceSides = sides,
                },
            },
        };

    private static BattleFatalInterceptContext FatalContext(
        BattleUnitState target,
        DeathResolutionContext? deathContext = null,
        int worldStep = 0
    ) =>
        new()
        {
            TargetUnit = target,
            DeathContext = deathContext ?? BattleDeathResolutionRules.NormalFatalContext(),
            HpBefore = Math.Max(target?.GetCurrentHp() ?? 0, 0),
            HpDamage = Math.Max(target?.GetCurrentHp() ?? 0, 0) + 1,
            ProjectedHp = -1,
            WorldStep = worldStep,
        };

    private static BattleUnitState Unit(
        StringName unitId,
        StringName factionId,
        int hp,
        int maxHp
    )
    {
        BattleUnitState unit = new BattleUnitState
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = factionId,
        }.WithCombatResourcesForTest(hp: hp, isAlive: true);
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, maxHp);
        return unit;
    }

    private static bool ContainsError(IEnumerable<string> errors, string code)
    {
        foreach (string error in errors ?? Array.Empty<string>())
            if ((error ?? "").Contains(code, StringComparison.Ordinal))
                return true;
        return false;
    }

    private sealed class ProbeFatalInterceptArbiter : IBattleFatalInterceptArbiter
    {
        internal bool ShouldIntercept { get; init; }
        internal int RecoveryHp { get; init; }
        internal int ResolveCount { get; private set; }
        internal bool TargetWasAliveAtResolve { get; private set; }

        public BattleFatalInterceptResult Resolve(BattleFatalInterceptContext context)
        {
            ResolveCount++;
            TargetWasAliveAtResolve = context?.TargetUnit?.IsAlive() == true;
            if (!ShouldIntercept || context?.TargetUnit == null)
                return BattleFatalInterceptResult.None;
            context.TargetUnit.SetCurrentHp(RecoveryHp);
            return new BattleFatalInterceptResult
            {
                Intercepted = context.TargetUnit.IsAlive(),
                RecoveredHp = context.TargetUnit.GetCurrentHp(),
            };
        }

        public BattleFatalInterceptPreviewResult Preview(BattleFatalInterceptContext context) =>
            BattleFatalInterceptPreviewResult.None;
    }

    private sealed class RuntimeFixture : IDisposable
    {
        private RuntimeFixture(
            BattleRuntimeModule runtime,
            BattleEquipmentAbilityRuntimeService service,
            BattleUnitState source,
            BattleUnitState target
        )
        {
            Runtime = runtime;
            Service = service;
            Source = source;
            Target = target;
        }

        internal BattleRuntimeModule Runtime { get; }
        internal BattleEquipmentAbilityRuntimeService Service { get; }
        internal BattleUnitState Source { get; }
        internal BattleUnitState Target { get; }

        internal static RuntimeFixture Create(EquipmentAbilityBindingDefinition binding)
        {
            var runtime = new BattleRuntimeModule();
            runtime.setup(
                equipment_ability_bindings:
                    new Dictionary<StringName, EquipmentAbilityBindingDefinition>
                    {
                        [binding.BindingId] = binding,
                    }
            );
            BattleUnitState source = Unit("fatal_source", "enemy", hp: 100, maxHp: 100);
            BattleUnitState target = Unit("fatal_target", "player", hp: 5, maxHp: 100);
            StringName instanceId = $"eq_{binding.BindingId}";
            var equipment = new EquipmentState();
            equipment.SetEquippedEntry(
                "main_hand",
                "item.test.fatal",
                new GStringNameArray { "main_hand" },
                EquipmentInstanceState.CreateInstance("item.test.fatal", instanceId)
            );
            target.SetEquipmentView(equipment);
            target.ReplaceEquipmentAbilityProjectionTyped(
                new[]
                {
                    new BattleEquipmentAbilitySourceState
                    {
                        EffectiveInstanceKey = $"projected:{instanceId}",
                        EquipmentDefId = "item.test.fatal",
                        SourceEquipmentInstanceId = instanceId,
                        SourceKind = EquipmentAbilitySourceKind.PlayerPersistentEquipment,
                        AbilityIds = new List<StringName> { binding.BindingId },
                    },
                },
                temporalProgressModifiers: null
            );
            return new RuntimeFixture(
                runtime,
                runtime.GetEquipmentAbilityRuntimeService(),
                source,
                target
            );
        }

        internal BattleEquipmentAbilitySourceReadView FindSource(StringName bindingId)
        {
            foreach (
                BattleEquipmentAbilitySourceReadView source
                in Target.GetEquipmentAbilitySourcesReadViewTyped()
            )
            {
                if (source?.AbilityIds?.Contains(bindingId) == true)
                    return source;
            }
            return null;
        }

        public void Dispose()
        {
            Runtime?.dispose();
            BattleTestFixture.DisposeBattleUnit(Source);
            BattleTestFixture.DisposeBattleUnit(Target);
        }
    }
}
