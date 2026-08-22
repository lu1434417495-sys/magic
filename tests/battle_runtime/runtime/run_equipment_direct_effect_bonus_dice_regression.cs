using System;
using System.Collections.Generic;
using Godot;

// §8.4：每次主直接伤害结算 +1D4（per-main-direct-effect bonus dice）。
// 覆盖：weapon/非武器 attack spell/save 直接伤害三类触发；逐段（repeat/chain 每段重新
// 进入 resolver）不跨段去重；miss 不触发、save-only 不误判 miss；extra_damage_segments
// 不重复查询；timeline/terrain/reflection/self/equipment bonus/direct reaction/
// trigger-skill origin 不触发；inherit_primary 继承主 tag 并进入相同倍率/save/mitigation；
// crit 不复制装备骰；weapon-hit 旧 query 与新 query 同段叠加；execute/preview/AI 一致。
public partial class run_equipment_direct_effect_bonus_dice_regression : LifecycleTestSceneTree
{
    private static readonly StringName PerEffectBindingId = "binding.test.per_effect_dice";
    private static readonly StringName PerEffectBinding2Id = "binding.test.per_effect_dice_b";
    private static readonly StringName WeaponHitBindingId = "binding.test.weapon_hit_dice";
    private static readonly StringName ExplicitForceBindingId = "binding.test.explicit_force_dice";
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestDamageTypeModeClosedDomain();
        TestHandlerSpecDeclaresPreviewAndAiSupport();
        TestContentValidationAcceptsAndRejects();
        TestTriggerMatrix();
        TestPerSegmentReEntryDoesNotDeduplicate();
        TestExtraDamageSegmentsDoNotRetrigger();
        TestOriginExclusionsFailClosed();
        TestInheritPrimaryEntersSamePipeline();
        TestCriticalDoesNotDuplicateEquipmentDice();
        TestWeaponHitAndPerEffectStacking();
        TestExecutePreviewAiParity();

        RequestTestExit(_test.Finish("Equipment direct effect bonus dice regression"));
    }

    private void TestDamageTypeModeClosedDomain()
    {
        _test.Eq(
            EquipmentAbilityDamageTypeModeContentRules.ToKind("explicit"),
            EquipmentAbilityDamageTypeModeKind.Explicit,
            "explicit should map to the explicit damage type mode."
        );
        _test.Eq(
            EquipmentAbilityDamageTypeModeContentRules.ToKind(""),
            EquipmentAbilityDamageTypeModeKind.Explicit,
            "an empty damage_type_mode should default to explicit."
        );
        _test.Eq(
            EquipmentAbilityDamageTypeModeContentRules.ToKind("inherit_primary"),
            EquipmentAbilityDamageTypeModeKind.InheritPrimary,
            "inherit_primary should map to the inherit damage type mode."
        );
        _test.Eq(
            EquipmentAbilityDamageTypeModeContentRules.ToKind("bogus_mode"),
            EquipmentAbilityDamageTypeModeKind.Unknown,
            "an unknown damage_type_mode should fail closed."
        );
        _test.Eq(
            EquipmentAbilityDamageTypeModeContentRules.ToStringName(
                EquipmentAbilityDamageTypeModeKind.InheritPrimary
            ),
            new StringName("inherit_primary"),
            "inherit_primary should round-trip to its stable id."
        );
        _test.False(
            EquipmentAbilityDamageTypeModeContentRules.IsValid(
                EquipmentAbilityDamageTypeModeKind.Unknown
            ),
            "Unknown should never be a valid damage type mode."
        );
    }

    private void TestHandlerSpecDeclaresPreviewAndAiSupport()
    {
        using var loader = new TestContentResourceLoader();
        using var registry = new EquipmentAbilityContentRegistry(loader);
        EquipmentAbilityHandlerSpec spec = registry.GetActionHandlerSpecsTyped()["add_damage_dice"];
        _test.True(
            spec.SupportsConsumer(EquipmentAbilityConsumerKind.Execution),
            "add_damage_dice should declare execution support."
        );
        _test.True(
            spec.SupportsConsumer(EquipmentAbilityConsumerKind.Preview),
            "add_damage_dice should declare preview support."
        );
        _test.True(
            spec.SupportsConsumer(EquipmentAbilityConsumerKind.AiScoring),
            "add_damage_dice should declare AI scoring support."
        );
    }

    private void TestContentValidationAcceptsAndRejects()
    {
        using var loader = new TestContentResourceLoader();
        using var registry = new EquipmentAbilityContentRegistry(loader);

        EquipmentAbilityRegistryBuildResult explicitResult = registry.Rebuild(
            new[] { BuildDiceAuthoringPack("explicit", "on_hit", "after_hit", "physical_slash", null) },
            BuildValidationContext()
        );
        _test.True(
            explicitResult.Success,
            $"an explicit add_damage_dice pack should keep building: {FormatErrors(explicitResult.Errors)}"
        );

        EquipmentAbilityRegistryBuildResult inheritResult = registry.Rebuild(
            new[] { BuildDiceAuthoringPack("inherit_primary", "on_damage_roll", "before_damage", "", null) },
            BuildValidationContext()
        );
        _test.True(
            inheritResult.Success,
            $"an inherit_primary pack without an explicit tag should build: {FormatErrors(inheritResult.Errors)}"
        );
        EquipmentAbilityBindingDefinition definition =
            registry.GetBindingDefinitionsTyped()["binding.test.direct_effect_dice"];
        AddDamageDiceActionPayloadDefinition payload =
            definition.Reactions[0].Actions[0].PayloadDefinition
                as AddDamageDiceActionPayloadDefinition;
        _test.Eq(
            payload?.DamageTypeMode ?? "",
            new StringName("inherit_primary"),
            "the projected payload should preserve damage_type_mode."
        );

        EquipmentAbilityRegistryBuildResult unknownModeResult = registry.Rebuild(
            new[] { BuildDiceAuthoringPack("bogus_mode", "on_damage_roll", "before_damage", "", null) },
            BuildValidationContext()
        );
        _test.False(unknownModeResult.Success, "an unknown damage_type_mode should be rejected.");
        AssertErrorContains(
            unknownModeResult.Errors,
            "EQA_DAMAGE_TYPE_MODE_INVALID",
            "damage_type_mode"
        );

        EquipmentAbilityRegistryBuildResult inheritTypeResult = registry.Rebuild(
            new[] { BuildDiceAuthoringPack("inherit_primary", "on_damage_roll", "before_damage", "fire", null) },
            BuildValidationContext()
        );
        _test.False(
            inheritTypeResult.Success,
            "inherit_primary with an explicit damage_type should be rejected."
        );
        AssertErrorContains(
            inheritTypeResult.Errors,
            "EQA_DAMAGE_TYPE_MODE_INHERIT_CONFLICT",
            "damage_type_mode"
        );

        EquipmentAbilityRegistryBuildResult inheritTagsResult = registry.Rebuild(
            new[] { BuildDiceAuthoringPack("inherit_primary", "on_damage_roll", "before_damage", "", new[] { "fire" }) },
            BuildValidationContext()
        );
        _test.False(
            inheritTagsResult.Success,
            "inherit_primary with explicit damage_tags should be rejected."
        );
        AssertErrorContains(
            inheritTagsResult.Errors,
            "EQA_DAMAGE_TYPE_MODE_INHERIT_CONFLICT",
            "damage_type_mode"
        );

        EquipmentAbilityRegistryBuildResult inheritTriggerResult = registry.Rebuild(
            new[] { BuildDiceAuthoringPack("inherit_primary", "on_hit", "after_hit", "", null) },
            BuildValidationContext()
        );
        _test.False(
            inheritTriggerResult.Success,
            "inherit_primary outside the main-direct-effect query trigger should be rejected."
        );
        AssertErrorContains(
            inheritTriggerResult.Errors,
            "EQA_DAMAGE_TYPE_MODE_INHERIT_TRIGGER_UNSUPPORTED",
            "damage_type_mode"
        );

        EquipmentAbilityRegistryBuildResult explicitMissingTypeResult = registry.Rebuild(
            new[] { BuildDiceAuthoringPack("explicit", "on_damage_roll", "before_damage", "", null) },
            BuildValidationContext()
        );
        _test.False(
            explicitMissingTypeResult.Success,
            "explicit mode without a damage_type should be rejected."
        );
        AssertErrorContains(
            explicitMissingTypeResult.Errors,
            "EQA_ACTION_REQUIRED_FIELD_MISSING",
            "add_damage_dice"
        );
    }

    private void TestTriggerMatrix()
    {
        using DiceFixture fixture = DiceFixture.Create();
        using var resolver = new FixedRollDamageResolver();
        resolver.SetEquipmentAbilityPorts(
            fixture.Runtime.GetEquipmentAbilityRuntimeService().DamageQuery,
            reactionSink: null
        );
        fixture.Attach(fixture.Wielder, "matrix", PerEffectBindingId);

        // 非武器 attack spell：有攻击检定且命中，无武器骰，仍触发。
        AttackEffectResolutionResult spellResult = resolver.ResolveEffects(
            fixture.Wielder,
            fixture.Target,
            new[] { BuildDamageEffect(power: 8, damageTag: "fire") },
            BuildMainContext(fixture, attackSuccess: true, hasAttackCheck: true)
        );
        _test.Eq(spellResult.Damage, 12, "a non-weapon attack spell should add 1D4 (max 4).");
        _test.Eq(
            spellResult.DamageEvents[0].BonusDamageDice.Total,
            4,
            "the spell segment should report the equipment bonus dice."
        );
        _test.Eq(
            spellResult.DamageEvents[0].DamageTag,
            new StringName("fire"),
            "inherit_primary should inherit the primary fire tag."
        );

        // save-only 主直接伤害：无攻击检定，不得被误判为 miss。
        fixture.ResetTarget();
        AttackEffectResolutionResult saveOnlyResult = resolver.ResolveEffects(
            fixture.Wielder,
            fixture.Target,
            new[] { BuildDamageEffect(power: 8, damageTag: "fire") },
            BuildMainContext(fixture, attackSuccess: false, hasAttackCheck: false)
        );
        _test.Eq(
            saveOnlyResult.Damage,
            12,
            "a save-only direct damage effect without an attack check must still trigger."
        );

        // miss 段：有攻击检定且未命中，不触发。
        fixture.ResetTarget();
        AttackEffectResolutionResult missResult = resolver.ResolveEffects(
            fixture.Wielder,
            fixture.Target,
            new[] { BuildDamageEffect(power: 8, damageTag: "fire") },
            BuildMainContext(fixture, attackSuccess: false, hasAttackCheck: true)
        );
        _test.Eq(missResult.Damage, 8, "a missed segment must not trigger the per-effect dice.");
        _test.Eq(
            missResult.DamageEvents[0].BonusDamageDice.Total,
            0,
            "a missed segment should report no equipment bonus dice."
        );

        // 武器近战命中：新 query 在无旧绑定时仍单独触发。
        fixture.ResetTarget();
        fixture.EquipWielderWeapon();
        AttackEffectResolutionResult weaponResult = resolver.ResolveEffects(
            fixture.Wielder,
            fixture.Target,
            new[] { BuildWeaponEffect(power: 4) },
            BuildMainContext(fixture, attackSuccess: true, hasAttackCheck: true)
        );
        _test.Eq(
            weaponResult.Damage,
            14,
            "a weapon hit with only the per-effect binding should add 1D4 on top of 4+1D6(max 6)."
        );
        _test.Eq(
            weaponResult.DamageEvents[0].WeaponDamageDice.Total,
            6,
            "the weapon segment should include the 1D6 weapon dice."
        );
    }

    private void TestPerSegmentReEntryDoesNotDeduplicate()
    {
        using DiceFixture fixture = DiceFixture.Create();
        using var resolver = new FixedRollDamageResolver();
        resolver.SetEquipmentAbilityPorts(
            fixture.Runtime.GetEquipmentAbilityRuntimeService().DamageQuery,
            reactionSink: null
        );
        fixture.Attach(fixture.Wielder, "repeat", PerEffectBindingId);

        // fixed repeat / repeat-until-fail / random chain 每段重新进入 resolver：
        // 同一 skill、同一 SourceEffectOrdinal(0)、同一 batch 语义的重复执行不得去重。
        int total = 0;
        for (int segment = 0; segment < 3; segment++)
        {
            AttackEffectResolutionResult result = resolver.ResolveEffects(
                fixture.Wielder,
                fixture.Target,
                new[] { BuildDamageEffect(power: 8, damageTag: "fire") },
                BuildMainContext(fixture, attackSuccess: true, hasAttackCheck: true)
            );
            _test.Eq(
                result.Damage,
                12,
                $"segment {segment} should trigger the per-effect dice again."
            );
            total += result.Damage;
        }
        _test.Eq(total, 36, "three re-entered segments should accumulate 3x(8+1D4max)=36.");
    }

    private void TestExtraDamageSegmentsDoNotRetrigger()
    {
        using DiceFixture fixture = DiceFixture.Create();
        using var resolver = new FixedRollDamageResolver();
        resolver.SetEquipmentAbilityPorts(
            fixture.Runtime.GetEquipmentAbilityRuntimeService().DamageQuery,
            reactionSink: null
        );
        fixture.Attach(fixture.Wielder, "segments", PerEffectBindingId);

        AttackEffectResolutionResult result = resolver.ResolveEffects(
            fixture.Wielder,
            fixture.Target,
            new[] { BuildEffectWithExtraSegment() },
            BuildMainContext(fixture, attackSuccess: true, hasAttackCheck: true)
        );
        _test.Eq(result.DamageEvents.Length, 2, "main effect plus one extra segment expected.");
        if (result.DamageEvents.Length != 2)
            return;
        _test.Eq(
            result.DamageEvents[0].ResolvedDamage,
            12,
            "the main segment should include the per-effect 1D4."
        );
        _test.Eq(
            result.DamageEvents[1].ResolvedDamage,
            5,
            "the extra damage segment must not query the per-effect dice again."
        );
        _test.Eq(result.Damage, 17, "total should stay 12+5 without a second per-effect roll.");
    }

    private void TestOriginExclusionsFailClosed()
    {
        using DiceFixture fixture = DiceFixture.Create();
        using var resolver = new FixedRollDamageResolver();
        resolver.SetEquipmentAbilityPorts(
            fixture.Runtime.GetEquipmentAbilityRuntimeService().DamageQuery,
            reactionSink: null
        );
        fixture.Attach(fixture.Wielder, "origin", PerEffectBindingId);

        BattleDamageOriginKind[] excluded =
        {
            BattleDamageOriginKind.Unknown,
            BattleDamageOriginKind.TimelineUpkeep,
            BattleDamageOriginKind.Terrain,
            BattleDamageOriginKind.Reflection,
            BattleDamageOriginKind.SelfDamage,
            BattleDamageOriginKind.EquipmentBonus,
            BattleDamageOriginKind.EquipmentDirectReaction,
            BattleDamageOriginKind.EquipmentTriggeredSkill,
        };
        foreach (BattleDamageOriginKind origin in excluded)
        {
            fixture.ResetTarget();
            AttackEffectResolutionResult result = resolver.ResolveEffects(
                fixture.Wielder,
                fixture.Target,
                new[] { BuildDamageEffect(power: 8, damageTag: "fire") },
                BuildMainContext(fixture, attackSuccess: true, hasAttackCheck: true)
                    .WithDamageOriginKind(origin)
            );
            _test.Eq(
                result.Damage,
                8,
                $"origin {origin} must not trigger the per-effect dice."
            );
        }

        // 自伤：主直接伤害段 source==target 时经 canonical 分类归 self_damage，不触发。
        fixture.ResetTarget();
        AttackEffectResolutionResult selfResult = resolver.ResolveEffects(
            fixture.Wielder,
            fixture.Wielder,
            new[] { BuildDamageEffect(power: 8, damageTag: "fire") },
            BuildMainContext(
                fixture,
                fixture.Wielder,
                fixture.Wielder,
                attackSuccess: true,
                hasAttackCheck: true
            )
        );
        _test.Eq(selfResult.Damage, 8, "self damage must be reclassified and not trigger.");
    }

    private void TestInheritPrimaryEntersSamePipeline()
    {
        using DiceFixture fixture = DiceFixture.Create();
        using var resolver = new FixedRollDamageResolver();
        resolver.SetEquipmentAbilityPorts(
            fixture.Runtime.GetEquipmentAbilityRuntimeService().DamageQuery,
            reactionSink: null
        );
        fixture.Attach(fixture.Wielder, "pipeline", PerEffectBindingId);

        // mitigation：继承主 fire tag 的装备骰进入同一 half tier。
        fixture.Target.SetDamageResistanceTyped("fire", "half");
        AttackEffectResolutionResult resisted = resolver.ResolveEffects(
            fixture.Wielder,
            fixture.Target,
            new[] { BuildDamageEffect(power: 8, damageTag: "fire") },
            BuildMainContext(fixture, attackSuccess: true, hasAttackCheck: true)
        );
        _test.Eq(
            resisted.Damage,
            6,
            "the inherited fire 1D4 should share the primary segment's half mitigation (8+4)/2=6."
        );
        fixture.Target.ResetDamageResistancesTyped();

        // save：继承骰进入同一豁免管线，豁免成功减半。
        fixture.ResetTarget();
        AttackEffectResolutionResult saved = resolver.ResolveEffects(
            fixture.Wielder,
            fixture.Target,
            new[]
            {
                TestSkillDefinitionProjection.BuildEffect(
                    "damage",
                    damageTag: "fire",
                    power: 8,
                    saveDc: 10,
                    saveAbility: "agility",
                    saveTag: "magic",
                    savePartialOnSuccess: true
                ),
            },
            BuildMainContext(fixture, attackSuccess: true, hasAttackCheck: true)
                .WithSaveRollOverrides(new[] { 20 })
        );
        _test.True(saved.DamageEvents[0].SaveSuccess, "a natural 20 should succeed the save.");
        _test.Eq(
            saved.Damage,
            6,
            "the inherited dice should enter the same save pipeline: (8+4)/2=6 on a successful save."
        );

        // 分段倍率：pre-resistance multiplier 同时作用于主伤害与继承骰。
        fixture.ResetTarget();
        AttackEffectResolutionResult multiplied = resolver.ResolveEffects(
            fixture.Wielder,
            fixture.Target,
            new[] { BuildResourceDamageEffect("fire", power: 8, multiplier: 2.0) },
            BuildMainContext(fixture, attackSuccess: true, hasAttackCheck: true)
        );
        _test.Eq(
            multiplied.Damage,
            24,
            "the inherited dice should share the segment multiplier: (8+4)x2=24."
        );

        // equipment bonus 追加段不递归：显式 force tag 的装备骰产生独立 outcome，
        // 该 outcome 不得再次触发 per-effect query。
        fixture.ResetTarget();
        fixture.Attach(fixture.Wielder, "pipeline", PerEffectBindingId, ExplicitForceBindingId);
        AttackEffectResolutionResult bonusSegment = resolver.ResolveEffects(
            fixture.Wielder,
            fixture.Target,
            new[] { BuildDamageEffect(power: 8, damageTag: "fire") },
            BuildMainContext(fixture, attackSuccess: true, hasAttackCheck: true)
        );
        _test.Eq(
            bonusSegment.DamageEvents.Length,
            2,
            "the explicit force-tag dice should produce a separate equipment bonus outcome."
        );
        if (bonusSegment.DamageEvents.Length != 2)
            return;
        _test.Eq(
            bonusSegment.DamageEvents[0].ResolvedDamage,
            12,
            "the main fire segment should carry only the inherited 1D4."
        );
        _test.Eq(
            bonusSegment.DamageEvents[1].ResolvedDamage,
            4,
            "the equipment bonus outcome must not recursively add another 1D4."
        );
        _test.Eq(bonusSegment.Damage, 16, "total should stay 12+4 without recursion.");
    }

    private void TestCriticalDoesNotDuplicateEquipmentDice()
    {
        using DiceFixture fixture = DiceFixture.Create();
        using var resolver = new FixedRollDamageResolver();
        resolver.SetEquipmentAbilityPorts(
            fixture.Runtime.GetEquipmentAbilityRuntimeService().DamageQuery,
            reactionSink: null
        );
        fixture.Attach(fixture.Wielder, "crit", PerEffectBindingId);

        AttackEffectResolutionResult result = resolver.ResolveEffects(
            fixture.Wielder,
            fixture.Target,
            new[] { BuildDamageEffect(power: 0, damageTag: "fire", diceCount: 1, diceSides: 6) },
            BuildMainContext(fixture, attackSuccess: true, hasAttackCheck: true, criticalHit: true)
        );
        DamageEventResult damageEvent = result.DamageEvents[0];
        _test.True(damageEvent.CriticalHit, "the segment should be a critical hit.");
        _test.Eq(damageEvent.DamageDice.Total, 6, "the main 1D6 should roll max.");
        _test.Eq(
            damageEvent.CriticalExtraDamageDice.Total,
            6,
            "the critical extra pool should duplicate only the skill dice."
        );
        _test.Eq(
            damageEvent.BonusDamageDice.Total,
            4,
            "the equipment 1D4 should land exactly once."
        );
        _test.Eq(
            damageEvent.CriticalExtraBonusDamageDice.Total,
            0,
            "a critical hit must not duplicate the equipment dice."
        );
        _test.Eq(result.Damage, 16, "crit total should be 6+6+4=16, not 20.");
    }

    private void TestWeaponHitAndPerEffectStacking()
    {
        using DiceFixture fixture = DiceFixture.Create();
        using var resolver = new FixedRollDamageResolver();
        resolver.SetEquipmentAbilityPorts(
            fixture.Runtime.GetEquipmentAbilityRuntimeService().DamageQuery,
            reactionSink: null
        );
        fixture.Attach(fixture.Wielder, "stacking", PerEffectBindingId, WeaponHitBindingId);
        fixture.EquipWielderWeapon();

        AttackEffectResolutionResult result = resolver.ResolveEffects(
            fixture.Wielder,
            fixture.Target,
            new[] { BuildWeaponEffect(power: 4) },
            BuildMainContext(fixture, attackSuccess: true, hasAttackCheck: true)
        );
        DamageEventResult damageEvent = result.DamageEvents[0];
        _test.Eq(
            damageEvent.BonusDamageDice.Count,
            2,
            "the weapon-hit 1D4 and per-effect 1D4 should merge into 2D4 on the same tag."
        );
        _test.Eq(
            damageEvent.BonusDamageDice.Total,
            8,
            "the merged 2D4 should roll max 8 under the fixed-roll seam."
        );
        _test.Eq(
            result.Damage,
            18,
            "melee hit total should be power 4 + weapon 1D6(max 6) + 2D4(max 8) = 18."
        );
    }

    private void TestExecutePreviewAiParity()
    {
        using DiceFixture fixture = DiceFixture.Create();
        using var resolver = new FixedRollDamageResolver();
        resolver.SetEquipmentAbilityPorts(
            fixture.Runtime.GetEquipmentAbilityRuntimeService().DamageQuery,
            reactionSink: null
        );
        CombatEffectDefinition effect = BuildDamageEffect(power: 8, damageTag: "fire");

        // 两份 1D4 绑定 => 每份平均模式期望 RoundToInt(2.5)=3，合计 8+3+3=14。
        fixture.Attach(fixture.Wielder, "parity", PerEffectBindingId, PerEffectBinding2Id);
        DamageResolutionContext averageContext = BuildMainContext(
            fixture,
            attackSuccess: true,
            hasAttackCheck: true
        ).WithDamageRollMode("average");
        AttackEffectResolutionResult executeResult = resolver.ResolveEffects(
            fixture.Wielder,
            fixture.Target,
            new[] { effect },
            averageContext
        );
        _test.Eq(executeResult.Damage, 14, "execute should resolve 8+2x1D4(avg 3)=14.");

        BattleDamagePreviewResult preview = resolver.PreviewDamageEffectTyped(
            fixture.Wielder,
            fixture.Target,
            effect,
            averageContext,
            BattleDamagePreviewRollMode.Average,
            BattleDamagePreviewSaveMode.Expected
        );
        _test.Eq(preview.Damage, executeResult.Damage, "preview should match execute damage.");

        // AI lane：与 BattleAiScoreService 相同的 detached working set + 逐 effect 累计。
        int aiTotal = 0;
        for (int segment = 0; segment < 2; segment++)
        {
            BattleDamagePreviewWorkingSet workingSet =
                BattleDamagePreviewWorkingSet.CreateDetached(
                    fixture.Wielder,
                    fixture.Target,
                    fixture.State
                );
            DamageResolutionContext aiContext = DamageResolutionContext
                .ForSkill("test_direct_effect_dice")
                .WithBattleState(workingSet?.BattleState)
                .WithDamageOriginKind(
                    BattleDamageOriginContentRules.ResolveProducerOrigin(
                        BattleDamageOriginKind.MainDirectEffect,
                        fixture.Wielder,
                        fixture.Target
                    )
                );
            BattleDamagePreviewScoreResult score = resolver.PreviewDamageScoreOnWorkingSetTyped(
                workingSet,
                effect,
                aiContext,
                BattleDamagePreviewRollMode.Average,
                BattleDamagePreviewSaveMode.Expected
            );
            aiTotal += score.Damage;
        }
        _test.Eq(
            aiTotal,
            28,
            "AI expected value should accumulate per resolved main damage effect: 2x14=28."
        );

        // control：无绑定时三条链路都不应加骰。
        fixture.ClearWielderProjection();
        AttackEffectResolutionResult controlExecute = resolver.ResolveEffects(
            fixture.Wielder,
            fixture.Target,
            new[] { effect },
            averageContext
        );
        _test.Eq(controlExecute.Damage, 8, "without bindings execute should stay at power 8.");
        BattleDamagePreviewResult controlPreview = resolver.PreviewDamageEffectTyped(
            fixture.Wielder,
            fixture.Target,
            effect,
            averageContext,
            BattleDamagePreviewRollMode.Average,
            BattleDamagePreviewSaveMode.Expected
        );
        _test.Eq(
            controlPreview.Damage,
            controlExecute.Damage,
            "without bindings preview should match execute."
        );
    }

    private static CombatEffectDefinition BuildDamageEffect(
        int power,
        StringName damageTag,
        int diceCount = 0,
        int diceSides = 0
    )
    {
        return TestSkillDefinitionProjection.BuildEffect(
            "damage",
            damageTag: damageTag,
            power: power,
            diceCount: diceCount,
            diceSides: diceSides
        );
    }

    private static CombatEffectDefinition BuildResourceDamageEffect(
        StringName damageTag,
        int power,
        double multiplier
    )
    {
        var resource = new CombatEffectDef
        {
            effect_type = "damage",
            damage_tag = damageTag,
            power = power,
            pre_resistance_damage_multiplier = multiplier,
        };
        CombatEffectDefinition definition = CombatEffectDefinition.FromResource(
            resource,
            "test://direct_effect_bonus_dice_multiplier"
        );
        resource.Dispose();
        return definition;
    }

    private static CombatEffectDefinition BuildWeaponEffect(int power) =>
        TestSkillDefinitionProjection.BuildEffect(
            "damage",
            damageTag: "physical_slash",
            power: power,
            addWeaponDice: true
        );

    private static CombatEffectDefinition BuildEffectWithExtraSegment()
    {
        var segment = new CombatDamageSegmentDef
        {
            damage_tag = "freeze",
            power = 5,
        };
        var resource = new CombatEffectDef
        {
            effect_type = "damage",
            damage_tag = "fire",
            power = 8,
        };
        resource.extra_damage_segments.Add(segment);
        CombatEffectDefinition definition = CombatEffectDefinition.FromResource(
            resource,
            "test://direct_effect_bonus_dice_segments"
        );
        resource.Dispose();
        segment.Dispose();
        return definition;
    }

    private static DamageResolutionContext BuildMainContext(
        DiceFixture fixture,
        bool attackSuccess,
        bool hasAttackCheck,
        bool criticalHit = false
    ) =>
        BuildMainContext(
            fixture,
            fixture.Wielder,
            fixture.Target,
            attackSuccess,
            hasAttackCheck,
            criticalHit
        );

    private static DamageResolutionContext BuildMainContext(
        DiceFixture fixture,
        BattleUnitState source,
        BattleUnitState target,
        bool attackSuccess,
        bool hasAttackCheck,
        bool criticalHit = false
    )
    {
        DamageResolutionContext context = DamageResolutionContext
            .Create(
                criticalHit: criticalHit,
                attackSuccess: attackSuccess,
                secondaryHitSuccess: false,
                skillId: "test_direct_effect_dice"
            )
            .WithBattleState(fixture.State)
            .WithDamageOriginKind(
                BattleDamageOriginContentRules.ResolveProducerOrigin(
                    BattleDamageOriginKind.MainDirectEffect,
                    source,
                    target
                )
            );
        return hasAttackCheck ? context.WithAttackCheck() : context;
    }

    private static EquipmentAbilityContentPackDef BuildDiceAuthoringPack(
        StringName damageTypeMode,
        StringName trigger,
        StringName timing,
        StringName damageType,
        IReadOnlyList<string> damageTags
    )
    {
        EquipmentAbilityContentPackDef pack = new()
        {
            pack_id = "pack.test.direct_effect_dice",
            schema_version = 1,
            load_order = 10,
        };
        EquipmentAbilityBindingDef binding = new()
        {
            binding_id = "binding.test.direct_effect_dice",
            trait_id = "trait.weapon.flame",
            override_mode = "add",
        };
        binding.allowed_source_kinds.Add("equipment_fixed");
        AddDamageDiceActionPayloadDef payload = new()
        {
            target_selector = "holder",
            damage_type = damageType,
            damage_type_mode = damageTypeMode,
            require_weapon_damage = false,
            dice = new DiceExpressionDef
            {
                terms = { new DiceExpressionTermDef { dice_count = 1, dice_sides = 4 } },
            },
        };
        foreach (string damageTag in damageTags ?? Array.Empty<string>())
        {
            payload.damage_tags.Add(damageTag);
        }
        binding.reactions.Add(
            new EquipmentAbilityReactionDef
            {
                reaction_id = "reaction.direct_effect_dice",
                trigger = trigger,
                timing = timing,
                actions =
                {
                    new EquipmentAbilityActionDef
                    {
                        action_id = "action.direct_effect_dice",
                        kind = "add_damage_dice",
                        payload = payload,
                    },
                },
            }
        );
        pack.bindings.Add(binding);
        return pack;
    }

    private static EquipmentAbilityContentValidationContext BuildValidationContext()
    {
        return new EquipmentAbilityContentValidationContext
        {
            KnownTraitIds = new HashSet<StringName> { "trait.weapon.flame" },
            KnownSkillIds = new HashSet<StringName> { "known_skill" },
            KnownStatusIds = new HashSet<StringName> { "burning" },
        };
    }

    private void AssertErrorContains(
        IReadOnlyList<string> errors,
        string code,
        string pathFragment
    )
    {
        foreach (string error in errors)
        {
            if ((error ?? "").Contains(code) && (error ?? "").Contains(pathFragment))
                return;
        }
        _test.Fail(
            $"Expected error containing code={code} path={pathFragment}. errors={FormatErrors(errors)}"
        );
    }

    private static string FormatErrors(IEnumerable<string> errors)
    {
        List<string> values = new();
        foreach (string error in errors ?? Array.Empty<string>())
            values.Add(error ?? "");
        return values.Count == 0 ? "[]" : $"[{string.Join(" | ", values)}]";
    }

    private sealed class DiceFixture : IDisposable
    {
        private DiceFixture(
            BattleRuntimeModule runtime,
            BattleState state,
            BattleUnitState wielder,
            BattleUnitState target
        )
        {
            Runtime = runtime;
            State = state;
            Wielder = wielder;
            Target = target;
        }

        internal BattleRuntimeModule Runtime { get; }
        internal BattleState State { get; }
        internal BattleUnitState Wielder { get; }
        internal BattleUnitState Target { get; }

        internal static DiceFixture Create()
        {
            var bindings = new Dictionary<StringName, EquipmentAbilityBindingDefinition>
            {
                [PerEffectBindingId] = BuildPerEffectBinding(
                    PerEffectBindingId,
                    "action.per_effect_dice"
                ),
                [PerEffectBinding2Id] = BuildPerEffectBinding(
                    PerEffectBinding2Id,
                    "action.per_effect_dice_b"
                ),
                [WeaponHitBindingId] = BuildWeaponHitBinding(),
                [ExplicitForceBindingId] = BuildExplicitForceBinding(),
            };
            var runtime = new BattleRuntimeModule();
            runtime.setup(equipment_ability_bindings: bindings);

            BattleUnitState wielder = Unit("dice_wielder", "heroes");
            BattleUnitState target = Unit("dice_target", "enemies");
            var state = new BattleState { battle_id = "direct_effect_bonus_dice_test" };
            state.SetUnit(wielder);
            state.SetUnit(target);
            return new DiceFixture(runtime, state, wielder, target);
        }

        internal void Attach(
            BattleUnitState unit,
            StringName suffix,
            params StringName[] bindingIds
        )
        {
            StringName instanceId = $"eq_dice_{suffix}";
            unit.ReplaceEquipmentAbilityProjectionTyped(
                new[]
                {
                    new BattleEquipmentAbilitySourceState
                    {
                        EffectiveInstanceKey = $"projected:{instanceId}",
                        EquipmentDefId = "item.test.direct_effect_dice",
                        SourceEquipmentInstanceId = instanceId,
                        SourceKind = EquipmentAbilitySourceKind.PlayerPersistentEquipment,
                        AbilityIds = new List<StringName>(bindingIds),
                    },
                },
                temporalProgressModifiers: null
            );
        }

        internal void ClearWielderProjection()
        {
            Wielder.ReplaceEquipmentAbilityProjectionTyped(
                Array.Empty<BattleEquipmentAbilitySourceState>(),
                temporalProgressModifiers: null
            );
        }

        internal void EquipWielderWeapon()
        {
            Wielder.SetUnarmedWeaponProjectionTyped(
                "physical_slash",
                new WeaponDice
                {
                    dice_count = 1,
                    dice_sides = 6,
                    flat_bonus = 0,
                },
                1
            );
        }

        internal void ResetTarget()
        {
            Target.SetCurrentHp(100);
            Wielder.SetCurrentHp(100);
        }

        private static BattleUnitState Unit(StringName unitId, StringName factionId)
        {
            BattleUnitState unit = new BattleUnitState
            {
                unit_id = unitId,
                display_name = unitId.ToString(),
                faction_id = factionId,
            }.WithCombatResourcesForTest(hp: 100, isAlive: true);
            unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
            unit.attribute_snapshot.SetValue("agility", 10);
            return unit;
        }

        private static EquipmentAbilityBindingDefinition BuildPerEffectBinding(
            StringName bindingId,
            StringName actionId
        )
        {
            return new EquipmentAbilityBindingDefinition
            {
                BindingId = bindingId,
                TraitId = "trait.test.direct_effect_dice",
                Reactions = new[]
                {
                    new EquipmentAbilityReactionDefinition
                    {
                        ReactionId = "reaction.per_effect_dice",
                        Trigger = EquipmentAbilityTriggerKind.OnDamageRoll,
                        Timing = EquipmentAbilityTimingKind.BeforeDamage,
                        Actions = new[]
                        {
                            new EquipmentAbilityActionDefinition
                            {
                                ActionId = actionId,
                                Kind = BattleEquipmentAbilityRuntimeService.ActionKindAddDamageDice,
                                PayloadDefinition = new AddDamageDiceActionPayloadDefinition
                                {
                                    TargetSelector = "holder",
                                    DamageTypeMode =
                                        EquipmentAbilityDamageTypeModeContentRules.InheritPrimary,
                                    RequireWeaponDamage = false,
                                    Dice = new DiceExpressionDefinition
                                    {
                                        Terms = new[]
                                        {
                                            new DiceExpressionTermDefinition
                                            {
                                                DiceCount = 1,
                                                DiceSides = 4,
                                            },
                                        },
                                    },
                                },
                            },
                        },
                    },
                },
            };
        }

        private static EquipmentAbilityBindingDefinition BuildWeaponHitBinding()
        {
            return new EquipmentAbilityBindingDefinition
            {
                BindingId = WeaponHitBindingId,
                TraitId = "trait.test.direct_effect_dice",
                Reactions = new[]
                {
                    new EquipmentAbilityReactionDefinition
                    {
                        ReactionId = "reaction.weapon_hit_dice",
                        Trigger = EquipmentAbilityTriggerKind.OnHit,
                        Timing = EquipmentAbilityTimingKind.AfterHit,
                        Actions = new[]
                        {
                            new EquipmentAbilityActionDefinition
                            {
                                ActionId = "action.weapon_hit_dice",
                                Kind = BattleEquipmentAbilityRuntimeService.ActionKindAddDamageDice,
                                PayloadDefinition = new AddDamageDiceActionPayloadDefinition
                                {
                                    TargetSelector = "holder",
                                    DamageType = "physical_slash",
                                    RequireWeaponDamage = true,
                                    Dice = new DiceExpressionDefinition
                                    {
                                        Terms = new[]
                                        {
                                            new DiceExpressionTermDefinition
                                            {
                                                DiceCount = 1,
                                                DiceSides = 4,
                                            },
                                        },
                                    },
                                },
                            },
                        },
                    },
                },
            };
        }

        private static EquipmentAbilityBindingDefinition BuildExplicitForceBinding()
        {
            return new EquipmentAbilityBindingDefinition
            {
                BindingId = ExplicitForceBindingId,
                TraitId = "trait.test.direct_effect_dice",
                Reactions = new[]
                {
                    new EquipmentAbilityReactionDefinition
                    {
                        ReactionId = "reaction.explicit_force_dice",
                        Trigger = EquipmentAbilityTriggerKind.OnDamageRoll,
                        Timing = EquipmentAbilityTimingKind.BeforeDamage,
                        Actions = new[]
                        {
                            new EquipmentAbilityActionDefinition
                            {
                                ActionId = "action.explicit_force_dice",
                                Kind = BattleEquipmentAbilityRuntimeService.ActionKindAddDamageDice,
                                PayloadDefinition = new AddDamageDiceActionPayloadDefinition
                                {
                                    TargetSelector = "holder",
                                    DamageType = "force",
                                    RequireWeaponDamage = false,
                                    Dice = new DiceExpressionDefinition
                                    {
                                        Terms = new[]
                                        {
                                            new DiceExpressionTermDefinition
                                            {
                                                DiceCount = 1,
                                                DiceSides = 4,
                                            },
                                        },
                                    },
                                },
                            },
                        },
                    },
                },
            };
        }

        public void Dispose()
        {
            BattleTestFixture.DisposeBattleFixture(Runtime, State);
        }
    }
}
