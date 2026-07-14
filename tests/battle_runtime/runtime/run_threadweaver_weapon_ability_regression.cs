using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_threadweaver_weapon_ability_regression : LifecycleTestSceneTree
{
    private static readonly StringName ThreadweaverItemId =
        "weapon_unique_sword_threadweaver_019";
    private static readonly StringName FateThreadTraitId =
        "weapon.sword.threadweaver.fate_thread";
    private static readonly StringName FateWeavingTraitId =
        "weapon.sword.threadweaver.fate_weaving";
    private static readonly StringName CutFateThreadTraitId =
        "weapon.sword.threadweaver.cut_fate_thread";
    private static readonly StringName ThreadMendingTraitId =
        "weapon.sword.threadweaver.thread_mending";
    private static readonly StringName FateThreadBindingId =
        "binding.weapon.sword.threadweaver.fate_thread";
    private static readonly StringName FateWeavingBindingId =
        "binding.weapon.sword.threadweaver.fate_weaving";
    private static readonly StringName CutFateThreadBindingId =
        "binding.weapon.sword.threadweaver.cut_fate_thread";
    private static readonly StringName ThreadMendingBindingId =
        "binding.weapon.sword.threadweaver.thread_mending";
    private static readonly StringName CutFateThreadSkillId =
        "weapon_sword_threadweaver_cut_fate_thread";
    private static readonly StringName ThreadMendingSkillId =
        "weapon_sword_threadweaver_thread_mending";
    private static readonly StringName CutFateThreadGrantId =
        "grant.threadweaver.cut_fate_thread.skill";
    private static readonly StringName ThreadMendingGrantId =
        "grant.threadweaver.thread_mending.skill";
    private static readonly StringName FateThreadStatusId = "threadweaver_fate_thread";
    private static readonly StringName FateFrayedStatusId = "threadweaver_fate_frayed";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestThreadweaverProjectsRealContentOntoBattleUnitAndClearsOnUnequip();
            TestFateThreadStacksFraysAndAddsSourceBoundAttackBonus();
            TestCutFateThreadExecutesFailedSaveConsumesThreadsAndUsesPerBattleCharge();
            TestCutFateThreadAfterSkillDamageAndCleanupAreGenericEquipmentActions();
            TestThreadMendingHealsFallenAllyAndCostsSourceHp();
            RequestTestExit(_test.Finish("Threadweaver weapon ability regression"));
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
            RequestTestExit(_test.Finish("Threadweaver weapon ability regression"));
        }
    }

    private void TestThreadweaverProjectsRealContentOntoBattleUnitAndClearsOnUnequip()
    {
        using ThreadweaverFixture fixture = ThreadweaverFixture.Build(new FixedRollDamageResolver());

        _test.True(fixture.ItemDefs.ContainsKey(ThreadweaverItemId), "Threadweaver item should load.");
        _test.True(fixture.TraitDefs.ContainsKey(FateThreadTraitId), "Fate Thread trait should load.");
        _test.True(fixture.TraitDefs.ContainsKey(FateWeavingTraitId), "Fate Weaving trait should load.");
        _test.True(fixture.TraitDefs.ContainsKey(CutFateThreadTraitId), "Cut Fate Thread trait should load.");
        _test.True(fixture.TraitDefs.ContainsKey(ThreadMendingTraitId), "Thread Mending trait should load.");
        _test.True(fixture.Bindings.ContainsKey(FateThreadBindingId), "Fate Thread binding should load.");
        _test.True(fixture.Bindings.ContainsKey(FateWeavingBindingId), "Fate Weaving binding should load.");
        _test.True(fixture.Bindings.ContainsKey(CutFateThreadBindingId), "Cut Fate Thread binding should load.");
        _test.True(fixture.Bindings.ContainsKey(ThreadMendingBindingId), "Thread Mending binding should load.");
        _test.True(fixture.SkillDefs.ContainsKey(CutFateThreadSkillId), "Cut Fate Thread skill should load.");
        _test.True(fixture.SkillDefs.ContainsKey(ThreadMendingSkillId), "Thread Mending skill should load.");
        if (!fixture.ItemDefs.ContainsKey(ThreadweaverItemId))
            return;

        ItemDef rawThreadweaver = ResourceLoader.Load<ItemDef>(
            "res://data/configs/items/weapon_unique_rapier_threadweaver.tres"
        );
        _test.True(rawThreadweaver != null, "Threadweaver raw item resource should load.");
        if (rawThreadweaver != null)
        {
            _test.Eq(rawThreadweaver.base_item_id, new StringName("weapon_type_rapier_base"), "Threadweaver should inherit the rapier base item.");
            _test.Eq(rawThreadweaver.base_price, 120000, "Threadweaver should keep the source price.");
        }

        BattleUnitState baseline = fixture.BuildUnitWithoutWeapon("baseline");
        BattleUnitState equipped = fixture.BuildThreadweaverUnit("projection");

        _test.Eq(equipped.weapon_item_id, ThreadweaverItemId, "Equipped unit should carry Threadweaver item id.");
        _test.Eq(equipped.weapon_profile_type_id, new StringName("rapier"), "Threadweaver should project as rapier.");
        _test.Eq(equipped.weapon_attack_range, 1, "Threadweaver attack range should be 1.");
        _test.Eq(equipped.weapon_one_handed_dice?.dice_count ?? 0, 1, "Threadweaver should use 1D8.");
        _test.Eq(equipped.weapon_one_handed_dice?.dice_sides ?? 0, 8, "Threadweaver should use 1D8.");
        _test.Eq(equipped.weapon_one_handed_dice?.flat_bonus ?? 0, 3, "Threadweaver should use +3 damage.");
        AssertUnitHasTraitAndAbilitySource(equipped, FateThreadTraitId, FateThreadBindingId, "eq_threadweaver_projection");
        AssertUnitHasTraitAndAbilitySource(equipped, FateWeavingTraitId, FateWeavingBindingId, "eq_threadweaver_projection");
        AssertUnitHasTraitAndAbilitySource(equipped, CutFateThreadTraitId, CutFateThreadBindingId, "eq_threadweaver_projection");
        AssertUnitHasTraitAndAbilitySource(equipped, ThreadMendingTraitId, ThreadMendingBindingId, "eq_threadweaver_projection");
        AssertCutFateThreadSkillShape(fixture);
        AssertThreadMendingSkillShape(fixture);
        AssertAfterSkillCostAndCleanupShape(fixture);

        equipped.GetEquipmentView().ClearSlot("main_hand");
        fixture.Runtime._unit_factory.RefreshBattleUnit(equipped);
        _test.Eq(equipped.weapon_item_id, new StringName(""), "Removing Threadweaver should clear weapon item id.");
        _test.Eq(equipped.weapon_profile_type_id, baseline.weapon_profile_type_id, "Removing Threadweaver should restore baseline weapon profile.");
        _test.Eq(equipped.equipment_ability_sources.Count, 0, "Removing Threadweaver should clear equipment ability sources.");
    }

    private void TestFateThreadStacksFraysAndAddsSourceBoundAttackBonus()
    {
        using ThreadweaverFixture fixture = ThreadweaverFixture.Build(new FixedRollDamageResolver());
        BattleUnitState holder = fixture.BuildThreadweaverUnit("threads");
        BattleUnitState target = BuildEnemy("thread_target", new Vector2I(1, 0), hp: 100);
        BattleUnitState otherTarget = BuildEnemy("other_target", new Vector2I(2, 0));

        for (int hit = 1; hit <= 3; hit++)
        {
            WeaponAbilityCommandTestSupport.IssueBasicAttack(
                fixture.Runtime,
                holder,
                target,
                $"threadweaver_thread_stack_{hit}",
                previewCommand: false
            );
            BattleStatusEffectState thread = target.GetStatusEffect(FateThreadStatusId);
            _test.True(thread != null, $"Hit {hit} should apply fate thread.");
            if (thread == null)
                continue;
            _test.Eq(thread.stacks, hit, $"Hit {hit} should set fate thread stacks to {hit}.");
            _test.Eq(thread.duration, 30, "Fate thread should last one round when not refreshed.");
            _test.Eq(thread.stack_limit, 3, "Fate thread should cap at 3 stacks.");
            _test.Eq(thread.source_unit_id, holder.unit_id, "Fate thread should remember its source.");
            _test.Eq(
                thread.source_bound_incoming_attack_roll_bonus_per_stack,
                1,
                "Fate thread should grant +1 attack roll per stack to its source."
            );
        }

        BattleStatusEffectState frayed = target.GetStatusEffect(FateFrayedStatusId);
        _test.True(frayed != null, "Three fate thread stacks should fray the target fate line.");
        if (frayed != null)
        {
            _test.Eq(frayed.duration, 30, "Frayed fate should last 30TU.");
            _test.True(frayed.lock_counterattack, "Frayed fate should lock counterattack.");
            _test.True(frayed.lock_guard, "Frayed fate should lock guard.");
            _test.True(frayed.lock_dodge_bonus, "Frayed fate should lock dodge bonus.");
        }

        BattleState attackState = fixture.Runtime.GetState();
        attackState.SetUnit(otherTarget);
        SetUnitOccupants(attackState, otherTarget);
        attackState.enemy_unit_ids.Add(otherTarget.unit_id);
        BattleAttackCheckPolicyService attackPolicy = fixture.Runtime.GetAttackCheckPolicyService();
        SkillDefinition attackSkill = TestSkillDefinitionProjection.BuildSkill("fixture_basic_attack");
        BattleAttackRollModifierBundle againstThreaded = attackPolicy.BuildModifierBundle(
            attackPolicy.BuildSkillDefinitionAttackContext(
                attackState,
                holder,
                target,
                attackSkill,
                "skill_attack_check",
                "threadweaver_thread_bonus",
                force_hit_no_crit: false
            )
        );
        BattleAttackRollModifierBundle againstOtherTarget = attackPolicy.BuildModifierBundle(
            attackPolicy.BuildSkillDefinitionAttackContext(
                attackState,
                holder,
                otherTarget,
                attackSkill,
                "skill_attack_check",
                "threadweaver_thread_bonus",
                force_hit_no_crit: false
            )
        );
        BattleAttackRollModifierBundle otherAttacker = attackPolicy.BuildModifierBundle(
            attackPolicy.BuildSkillDefinitionAttackContext(
                attackState,
                otherTarget,
                target,
                attackSkill,
                "skill_attack_check",
                "threadweaver_thread_bonus",
                force_hit_no_crit: false
            )
        );

        _test.Eq(againstThreaded.GetEffectiveModifierDelta(), 3, "Three fate threads should grant +3 only to the source attacker.");
        _test.True(HasModifier(againstThreaded, FateThreadStatusId, 3), "Fate thread +3 should appear in modifier breakdown.");
        _test.Eq(againstOtherTarget.GetEffectiveModifierDelta(), 0, "Fate thread should not help against other targets.");
        _test.Eq(otherAttacker.GetEffectiveModifierDelta(), 0, "Fate thread should not help unrelated attackers.");
    }

    private void TestCutFateThreadExecutesFailedSaveConsumesThreadsAndUsesPerBattleCharge()
    {
        using ThreadweaverFixture fixture = ThreadweaverFixture.Build(
            new FixedFailedSaveDamageResolver(new GArray(), new GArray())
        );
        BattleUnitState holder = fixture.BuildThreadweaverUnit("cut");
        BattleUnitState target = BuildEnemy("cut_target", new Vector2I(1, 0), hp: 80);
        ApplyFrayedThreads(holder, target);
        BattleState state = BuildState("threadweaver_cut_execute", holder, target);
        fixture.Runtime.SetupStateForTests(state);

        BattleAvailableSkillEntry cutEntry = FindRequiredEquipmentSkill(fixture, holder, CutFateThreadSkillId, state);
        _test.Eq(cutEntry.EquipmentUsagePeriodKind, EquipmentAbilityUsagePeriodKind.PerBattle, "Cut Fate Thread should be per battle.");
        _test.Eq(cutEntry.EquipmentMaxUsesPerPeriod, 1, "Cut Fate Thread should be once per battle.");

        BattleEventBatch batch = IssueUnitSkillInCurrentState(
            fixture.Runtime,
            holder,
            target,
            cutEntry,
            CutFateThreadSkillId
        );
        _test.True(batch != null, "Cut Fate Thread command should return a batch.");
        _test.False(target.is_alive, "Failed save Cut Fate Thread should execute the frayed target.");
        _test.False(target.HasStatusEffect(FateThreadStatusId), "Cut Fate Thread should consume fate threads.");
        _test.False(target.HasStatusEffect(FateFrayedStatusId), "Cut Fate Thread should consume frayed fate.");

        BattleSkillAvailabilityView exhausted = BuildEquipmentSkillAvailability(fixture, holder, state);
        _test.True(TryFindSkillEntry(exhausted, CutFateThreadSkillId, out BattleAvailableSkillEntry exhaustedEntry), "Cut Fate Thread entry should remain visible after use.");
        _test.False(exhaustedEntry.IsSelectable, "Cut Fate Thread should be disabled after its per-battle use.");
    }

    private void TestCutFateThreadAfterSkillDamageAndCleanupAreGenericEquipmentActions()
    {
        using ThreadweaverFixture fixture = ThreadweaverFixture.Build(new FixedRollDamageResolver(new GArray { 10, 10, 10, 10 }));
        BattleUnitState holder = fixture.BuildThreadweaverUnit("cut_success");
        BattleUnitState target = BuildEnemy("cut_success_target", new Vector2I(1, 0), hp: 100);
        ApplyFrayedThreads(holder, target);
        BattleState state = BuildState("threadweaver_cut_success_damage", holder, target);
        fixture.Runtime.SetupStateForTests(state);

        int hpBefore = target.current_hp;
        bool changed = fixture.Runtime.GetEquipmentAbilityRuntimeService().ResolveGrantedSkillUsed(
            new BattleEquipmentAbilityGrantedSkillUsedContext
            {
                SourceUnit = holder,
                TargetUnit = target,
                BattleState = state,
                Batch = new BattleEventBatch(),
                BindingId = CutFateThreadBindingId,
                GrantedActionId = CutFateThreadGrantId,
                SkillId = CutFateThreadSkillId,
            }
        );

        _test.True(changed, "Cut Fate Thread after-skill equipment reaction should mutate state.");
        _test.Eq(hpBefore - target.current_hp, 40, "Saved Cut Fate Thread target should take 4D10 psychic damage.");
        _test.False(target.HasStatusEffect(FateThreadStatusId), "Cut Fate Thread after-skill should clear fate threads.");
        _test.False(target.HasStatusEffect(FateFrayedStatusId), "Cut Fate Thread after-skill should clear frayed fate.");
    }

    private void TestThreadMendingHealsFallenAllyAndCostsSourceHp()
    {
        using ThreadweaverFixture fixture = ThreadweaverFixture.Build(new FixedRollDamageResolver(new GArray { 4, 5 }));
        BattleUnitState holder = fixture.BuildThreadweaverUnit("mending");
        holder.current_hp = 100;
        holder.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
        BattleUnitState ally = BuildAlly("fallen_ally", new Vector2I(1, 0), hp: 0, hpMax: 50);
        ally.is_alive = false;
        BattleState state = BuildState("threadweaver_mending", holder, ally);
        fixture.Runtime.SetupStateForTests(state);

        BattleAvailableSkillEntry mendEntry = FindRequiredEquipmentSkill(fixture, holder, ThreadMendingSkillId, state);
        _test.Eq(mendEntry.EquipmentUsagePeriodKind, EquipmentAbilityUsagePeriodKind.PerBattle, "Thread Mending should be per battle.");
        int holderHpBefore = holder.current_hp;

        BattleEventBatch batch = IssueUnitSkillInCurrentState(
            fixture.Runtime,
            holder,
            ally,
            mendEntry,
            ThreadMendingSkillId
        );

        _test.True(batch != null, "Thread Mending command should return a batch.");
        _test.True(ally.is_alive, "Thread Mending should revive a fallen ally.");
        _test.Eq(ally.current_hp, 19, "Thread Mending should heal 2D8+10 with fixed 4 and 5 rolls.");
        _test.Eq(holderHpBefore - holder.current_hp, 10, "Thread Mending should cost the source 10 HP.");
    }

    private void AssertCutFateThreadSkillShape(ThreadweaverFixture fixture)
    {
        _test.True(fixture.SkillDefs.TryGetValue(CutFateThreadSkillId, out SkillDefinition cutSkill), "Cut Fate Thread skill should project.");
        CombatEffectDefinition effect = FirstEffect(cutSkill);
        _test.Eq(effect?.EffectKind ?? BattleEffectKind.Unknown, BattleEffectKind.Execute, "Cut Fate Thread should use execute.");
        _test.Eq(effect?.RequiredTargetStatusId ?? new StringName(""), FateFrayedStatusId, "Cut Fate Thread should require frayed fate.");
        _test.Eq(effect?.RequiredTargetStatusMinStacks ?? 0, 1, "Cut Fate Thread should require one frayed stack.");
        _test.Eq(effect?.SaveDcModeKind ?? BattleSaveDcMode.Unknown, BattleSaveDcMode.Static, "Cut Fate Thread should use static DC.");
        _test.Eq(effect?.SaveDc ?? 0, 18, "Cut Fate Thread should be DC 18.");
        _test.Eq(effect?.SaveAbility ?? new StringName(""), new StringName("constitution"), "Cut Fate Thread should use constitution save.");
        _test.Eq(effect?.SaveTag ?? new StringName(""), new StringName("execute"), "Cut Fate Thread should use execute save tag.");
        _test.Eq(effect?.DamageTag ?? new StringName(""), new StringName("psychic"), "Cut Fate Thread execute should be psychic tagged.");
        _test.Eq(effect?.ThresholdMaxHpRatioPercent ?? 0, 100, "Cut Fate Thread failed save should execute regardless of current HP.");
    }

    private void AssertThreadMendingSkillShape(ThreadweaverFixture fixture)
    {
        _test.True(fixture.SkillDefs.TryGetValue(ThreadMendingSkillId, out SkillDefinition mendSkill), "Thread Mending skill should project.");
        CombatSkillDefinition combat = mendSkill?.CombatProfile;
        _test.Eq(combat?.TargetTeamFilter ?? new StringName(""), new StringName("ally"), "Thread Mending should target allies.");
        _test.Eq(combat?.RangeValue ?? 0, 2, "Thread Mending should use 10ft/2-cell range.");
        _test.Eq(combat?.ApCost ?? 0, 1, "Thread Mending should cost 1AP.");
        CombatEffectDefinition heal = FirstEffect(mendSkill);
        _test.Eq(heal?.EffectKind ?? BattleEffectKind.Unknown, BattleEffectKind.Heal, "Thread Mending should heal.");
        _test.Eq(heal?.DiceCount ?? 0, 2, "Thread Mending should roll 2 dice.");
        _test.Eq(heal?.DiceSides ?? 0, 8, "Thread Mending should roll D8s.");
        _test.Eq(heal?.DiceBonus ?? 0, 10, "Thread Mending should add +10 healing.");
    }

    private void AssertAfterSkillCostAndCleanupShape(ThreadweaverFixture fixture)
    {
        _test.True(fixture.Bindings.TryGetValue(CutFateThreadBindingId, out EquipmentAbilityBindingDefinition cutBinding), "Cut binding should project.");
        IReadOnlyList<EquipmentAbilityActionDefinition> cutActions =
            cutBinding?.Reactions?[0]?.Actions ?? Array.Empty<EquipmentAbilityActionDefinition>();
        _test.Eq(cutActions.Count, 3, "Cut Fate Thread after-skill should damage and clear two statuses.");
        _test.True(cutActions[0]?.PayloadDefinition is DealDamageActionPayloadDefinition, "Cut after-skill first action should deal damage.");
        DealDamageActionPayloadDefinition cutDamage = cutActions[0]?.PayloadDefinition as DealDamageActionPayloadDefinition;
        _test.Eq(cutDamage?.TargetSelector ?? new StringName(""), new StringName("skill_target"), "Cut after-skill damage should hit the selected target.");
        _test.Eq(cutDamage?.DamageType ?? new StringName(""), new StringName("psychic"), "Cut after-skill damage should be psychic.");
        _test.Eq(cutDamage?.Dice?.Terms?[0]?.DiceCount ?? 0, 4, "Cut after-skill damage should be 4D10.");
        _test.Eq(cutDamage?.Dice?.Terms?[0]?.DiceSides ?? 0, 10, "Cut after-skill damage should be 4D10.");

        _test.True(fixture.Bindings.TryGetValue(ThreadMendingBindingId, out EquipmentAbilityBindingDefinition mendBinding), "Mending binding should project.");
        IReadOnlyList<EquipmentAbilityActionDefinition> mendActions =
            mendBinding?.Reactions?[0]?.Actions ?? Array.Empty<EquipmentAbilityActionDefinition>();
        _test.Eq(mendActions.Count, 1, "Thread Mending after-skill should have one HP cost action.");
        _test.True(mendActions[0]?.PayloadDefinition is DealDamageActionPayloadDefinition, "Thread Mending after-skill should deal source damage.");
        DealDamageActionPayloadDefinition cost = mendActions[0]?.PayloadDefinition as DealDamageActionPayloadDefinition;
        _test.Eq(cost?.TargetSelector ?? new StringName(""), new StringName("source"), "Thread Mending HP cost should target source.");
        _test.Eq(cost?.Dice?.FlatBonus ?? 0, 10, "Thread Mending HP cost should be flat 10.");
    }

    private static CombatEffectDefinition FirstEffect(SkillDefinition skill)
    {
        IReadOnlyList<CombatEffectDefinition> effects =
            skill?.CombatProfile?.EffectDefinitions ?? Array.Empty<CombatEffectDefinition>();
        return effects.Count > 0 ? effects[0] : null;
    }

    private static void ApplyFrayedThreads(BattleUnitState source, BattleUnitState target)
    {
        target.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = FateThreadStatusId,
                source_unit_id = source.unit_id,
                stacks = 3,
                power = 3,
                duration = 30,
                stack_behavior = "add",
                stack_limit = 3,
                display_label = "命运线",
                source_bound_incoming_attack_roll_bonus_per_stack = 1,
                source_bound_incoming_attack_roll_bonus_min_stacks = 1,
            }
        );
        target.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = FateFrayedStatusId,
                source_unit_id = source.unit_id,
                stacks = 1,
                power = 1,
                duration = 30,
                stack_behavior = "refresh",
                stack_limit = 1,
                display_label = "命运脆弱",
                lock_counterattack = true,
                lock_guard = true,
                lock_dodge_bonus = true,
            }
        );
    }

    private static BattleEventBatch IssueUnitSkillInCurrentState(
        BattleRuntimeModule runtime,
        BattleUnitState user,
        BattleUnitState target,
        BattleAvailableSkillEntry entry,
        StringName skillId
    )
    {
        WeaponAbilityCommandTestSupport.PrimeActionResources(user);
        BattleCommand command = WeaponAbilityCommandTestSupport.BuildUnitSkillCommand(
            user,
            target,
            entry,
            skillId
        );
        BattlePreview preview = runtime.PreviewCommand(command);
        if (preview?.allowed != true)
        {
            throw new InvalidOperationException(
                $"skill preview blocked: {string.Join(" | ", preview?.LogLinesTyped ?? Array.Empty<string>())}"
            );
        }
        return runtime.IssueCommand(command);
    }

    private static BattleSkillAvailabilityView BuildEquipmentSkillAvailability(
        ThreadweaverFixture fixture,
        BattleUnitState holder,
        BattleState state = null
    )
    {
        BattleSkillAvailabilityService availabilityService =
            new(fixture.SkillDefs, fixture.Bindings);
        return availabilityService.BuildView(
            new BattleSkillAvailabilityQuery
            {
                User = holder,
                IncludeEquipmentSkills = true,
                IncludeKnownSkills = false,
                Consumer = BattleSkillAvailabilityConsumer.ManualSelection,
                WorldStep = 0,
                BattleState = state,
            }
        );
    }

    private static BattleAvailableSkillEntry FindRequiredEquipmentSkill(
        ThreadweaverFixture fixture,
        BattleUnitState holder,
        StringName skillId,
        BattleState state = null
    )
    {
        BattleSkillAvailabilityView availability =
            BuildEquipmentSkillAvailability(fixture, holder, state);
        if (!TryFindSkillEntry(availability, skillId, out BattleAvailableSkillEntry entry))
            throw new InvalidOperationException($"missing equipment skill {skillId}.");
        return entry;
    }

    private static bool TryFindSkillEntry(
        BattleSkillAvailabilityView view,
        StringName skillId,
        out BattleAvailableSkillEntry result
    )
    {
        result = null;
        foreach (BattleAvailableSkillEntry entry in view?.SkillEntries ?? Array.Empty<BattleAvailableSkillEntry>())
        {
            if (entry?.EntryRef.SkillId == skillId)
            {
                result = entry;
                return true;
            }
        }
        return false;
    }

    private static bool HasModifier(
        BattleAttackRollModifierBundle bundle,
        StringName sourceId,
        int delta
    )
    {
        foreach (BattleAttackRollModifierSpec spec in bundle?.Breakdown ?? Array.Empty<BattleAttackRollModifierSpec>())
        {
            if (spec.source_id == sourceId && spec.modifier_delta == delta)
                return true;
        }
        return false;
    }

    private static void AssertUnitHasTraitAndAbilitySource(
        BattleUnitState unit,
        StringName traitId,
        StringName bindingId,
        StringName expectedInstanceId
    )
    {
        if (unit == null)
            throw new InvalidOperationException("unit is null.");
        if (!unit.effective_trait_ids.Contains(traitId))
            throw new InvalidOperationException($"unit missing trait {traitId}.");
        BattleEquipmentAbilitySourceState source = FindSource(unit, bindingId);
        if (source == null)
            throw new InvalidOperationException($"unit missing equipment ability source {bindingId}.");
        if (source.SourceKind != EquipmentAbilitySourceKind.PlayerPersistentEquipment)
            throw new InvalidOperationException($"{bindingId} should come from persistent equipment.");
        if (source.SourceEquipmentInstanceId != expectedInstanceId)
        {
            throw new InvalidOperationException(
                $"{bindingId} expected instance {expectedInstanceId}, got {source.SourceEquipmentInstanceId}."
            );
        }
    }

    private static BattleEquipmentAbilitySourceState FindSource(
        BattleUnitState unit,
        StringName bindingId
    )
    {
        foreach (BattleEquipmentAbilitySourceState source in unit.equipment_ability_sources)
        {
            if (source?.AbilityIds?.Contains(bindingId) == true)
                return source;
        }
        return null;
    }

    private static BattleState BuildState(
        StringName battleId,
        BattleUnitState holder,
        params BattleUnitState[] units
    )
    {
        BattleState state = new()
        {
            battle_id = battleId,
            map_size = new Vector2I(6, 6),
        };
        state.PhaseKind = BattlePhaseKind.UnitActing;
        state.active_unit_id = holder.unit_id;
        state.ReplaceEnvironmentSnapshot(
            BattleEnvironmentSnapshot.FromBattleStartContext(new GDictionary { ["world_step"] = 0 })
        );
        AddPlainCells(state);
        state.SetUnit(holder);
        SetUnitOccupants(state, holder);
        state.ally_unit_ids.Add(holder.unit_id);
        foreach (BattleUnitState unit in units ?? Array.Empty<BattleUnitState>())
        {
            state.SetUnit(unit);
            SetUnitOccupants(state, unit);
            if (unit.faction_id == holder.faction_id)
                state.ally_unit_ids.Add(unit.unit_id);
            else
                state.enemy_unit_ids.Add(unit.unit_id);
        }
        return state;
    }

    private static void AddPlainCells(BattleState state)
    {
        if (state == null)
            return;
        for (int x = 0; x < state.map_size.X; x++)
        {
            for (int y = 0; y < state.map_size.Y; y++)
            {
                BattleCellState cell = new();
                cell.SetCoord(new Vector2I(x, y));
                state.SetCell(cell);
            }
        }
    }

    private static void SetUnitOccupants(BattleState state, BattleUnitState unit)
    {
        if (state == null || unit == null)
            return;
        unit.RefreshFootprint();
        foreach (Vector2I coord in unit.occupied_coords)
        {
            BattleCellState cell = state.GetCell(coord);
            cell?.SetOccupant(unit.unit_id);
        }
    }

    private static BattleUnitState BuildEnemy(StringName unitId, Vector2I coord, int hp = 30) =>
        BuildUnit(unitId, "enemy", coord, hp, hp);

    private static BattleUnitState BuildAlly(StringName unitId, Vector2I coord, int hp, int hpMax) =>
        BuildUnit(unitId, "player", coord, hp, hpMax);

    private static BattleUnitState BuildUnit(
        StringName unitId,
        StringName factionId,
        Vector2I coord,
        int hp,
        int hpMax
    )
    {
        BattleUnitState unit = new()
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = factionId,
            is_alive = hp > 0,
            current_hp = Math.Max(hp, 0),
            current_ap = 2,
        };
        unit.SetAnchorCoord(coord);
        unit.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS, 14);
        unit.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, 0);
        unit.attribute_snapshot.SetValue(AttributeService.BASE_ATTACK_BONUS, 0);
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, Math.Max(hpMax, 1));
        unit.attribute_snapshot.SetValue("constitution", 10);
        unit.attribute_snapshot.SetValue("constitution_modifier", 0);
        unit.SetEquipmentView(new EquipmentState());
        return unit;
    }

    private sealed class ThreadweaverFixture : IDisposable
    {
        private readonly ItemContentRegistry _itemRegistry;
        private readonly ProgressionContentRegistry _progressionRegistry;
        private readonly PartyState _partyState;

        private ThreadweaverFixture(
            ItemContentRegistry itemRegistry,
            ProgressionContentRegistry progressionRegistry,
            PartyState partyState,
            BattleRuntimeModule runtime
        )
        {
            _itemRegistry = itemRegistry;
            _progressionRegistry = progressionRegistry;
            _partyState = partyState;
            Runtime = runtime;
            ItemDefs = itemRegistry.GetItemDefsTyped();
            SkillDefs = progressionRegistry.GetSkillDefinitionsTyped();
            TraitDefs = progressionRegistry.GetTraitDefsTyped();
            Bindings = progressionRegistry.GetEquipmentAbilityBindingDefinitionsTyped();
        }

        internal BattleRuntimeModule Runtime { get; }
        internal IReadOnlyDictionary<StringName, ItemDefinition> ItemDefs { get; }
        internal IReadOnlyDictionary<StringName, SkillDefinition> SkillDefs { get; }
        internal IReadOnlyDictionary<StringName, TraitDefinition> TraitDefs { get; }
        internal IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> Bindings { get; }

        internal static ThreadweaverFixture Build(BattleDamageResolver damageResolver)
        {
            ItemContentRegistry itemRegistry = new(new TestContentResourceLoader());
            ProgressionContentRegistry progressionRegistry = new(new TestContentResourceLoader());
            PartyState partyState = BuildPartyState("hero");
            CharacterManagementModule characterManagement = new();
            characterManagement.setup(
                partyState,
                progressionRegistry.GetSkillDefinitionsTyped(),
                progressionRegistry.GetProfessionDefsTyped(),
                progressionRegistry.GetAchievementDefsTyped(),
                itemRegistry.GetItemDefsTyped(),
                progressionRegistry.GetQuestDefsTyped(),
                progressionRegistry.GetTraitDefsTyped(),
                null,
                new ProgressionIdentityCatalogData()
            );

            BattleRuntimeModule runtime = new();
            runtime.setup(
                characterManagement,
                progressionRegistry.GetSkillDefinitionsTyped(),
                item_defs: itemRegistry.GetItemDefsTyped(),
                trait_defs: progressionRegistry.GetTraitDefsTyped(),
                equipment_ability_bindings: progressionRegistry.GetEquipmentAbilityBindingDefinitionsTyped()
            );
            if (damageResolver != null)
                runtime.ConfigureDamageResolverForTests(damageResolver);
            runtime.ConfigureHitResolverForTests(new FixedHitResolver(10));
            return new ThreadweaverFixture(itemRegistry, progressionRegistry, partyState, runtime);
        }

        internal BattleUnitState BuildUnitWithoutWeapon(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            return BuildSingleAllyUnit(label);
        }

        internal BattleUnitState BuildThreadweaverUnit(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            member.equipment_state.SetEquippedEntry(
                "main_hand",
                ThreadweaverItemId,
                new GStringNameArray { "main_hand" },
                EquipmentInstanceState.CreateInstance(ThreadweaverItemId, $"eq_threadweaver_{label}")
            );
            BattleUnitState unit = BuildSingleAllyUnit(label);
            unit.SetAnchorCoord(Vector2I.Zero);
            unit.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, 0);
            unit.attribute_snapshot.SetValue(AttributeService.BASE_ATTACK_BONUS, 0);
            unit.attribute_snapshot.SetValue("constitution", 10);
            unit.attribute_snapshot.SetValue("constitution_modifier", 0);
            return unit;
        }

        public void Dispose()
        {
            Runtime?.dispose();
            _itemRegistry?.Dispose();
            _progressionRegistry?.Dispose();
        }

        private BattleUnitState BuildSingleAllyUnit(string label)
        {
            IReadOnlyList<BattleUnitState> units =
                Runtime._unit_factory.BuildAllyUnits(_partyState, new GDictionary());
            if (units.Count != 1)
            {
                throw new InvalidOperationException(
                    $"{label} scenario should build exactly one ally unit."
                );
            }
            return units[0];
        }

        private static PartyState BuildPartyState(StringName memberId)
        {
            PartyState partyState = new();
            PartyMemberState memberState = new()
            {
                member_id = memberId,
                display_name = memberId.ToString(),
                progression = new UnitProgress(),
                equipment_state = new EquipmentState(),
            };
            partyState.SetMemberState(memberState);
            partyState.active_member_ids.Add(memberId);
            partyState.leader_member_id = memberId;
            return partyState;
        }
    }
}
