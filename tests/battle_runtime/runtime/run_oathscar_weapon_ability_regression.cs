using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_oathscar_weapon_ability_regression : LifecycleTestSceneTree
{
    private static readonly StringName OathscarItemId = "weapon_unique_sword_oathscar_003";
    private static readonly StringName OathBindTraitId = "weapon.sword.oathscar.oath_bind";
    private static readonly StringName OathMarkTraitId = "weapon.sword.oathscar.oath_mark";
    private static readonly StringName OathJudgmentTraitId =
        "weapon.sword.oathscar.oath_judgment";
    private static readonly StringName OathBacklashTraitId =
        "weapon.sword.oathscar.oath_backlash";
    private static readonly StringName OathBindBindingId =
        "binding.weapon.sword.oathscar.oath_bind";
    private static readonly StringName OathMarkBindingId =
        "binding.weapon.sword.oathscar.oath_mark";
    private static readonly StringName OathJudgmentBindingId =
        "binding.weapon.sword.oathscar.oath_judgment";
    private static readonly StringName OathBacklashBindingId =
        "binding.weapon.sword.oathscar.oath_backlash";
    private static readonly StringName OathBindSkillId =
        "weapon_sword_oathscar_bind_oath";
    private static readonly StringName OathJudgmentSkillId =
        "weapon_sword_oathscar_oath_judgment";
    private static readonly StringName OathBindGrantId =
        "grant.oathscar.oath_bind.skill";
    private static readonly StringName OathJudgmentGrantId =
        "grant.oathscar.oath_judgment.skill";
    private static readonly StringName OathTargetStatusId = "oathscar_oath_target";
    private static readonly StringName OathMarkStatusId = "oathscar_oath_mark";
    private static readonly StringName StunnedStatusId = "stunned";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestOathscarProjectsRealContentOntoBattleUnit();
            TestOathBindRuntimeServiceSynchronizesMirrorStatus();
            TestOathBindSelectsOneCurrentTargetAndClearsOldMarksThroughRealCommand();
            TestOathTargetDeathClearsOathAndPreventsBacklash();
            TestTargetDeathDoesNotClearUnconfiguredTargetMark();
            TestOathMarkAfterHitConditionResolvesBoundTargetMark();
            TestOathMarkStacksOnlyOnCurrentOathTargetAndModifiesAttackCheck();
            TestOathJudgmentIsGuaranteedSkillAndConsumesCurrentOathMarks();
            TestGeneratedDealDamageEffectCanDamageSource();
            TestOathBacklashDirectAfterHitDealsDamage();
            TestOathBacklashPunishesWeaponHitAgainstNonOathTarget();
            RequestTestExit(_test.Finish("Oathscar weapon ability regression"));
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
            RequestTestExit(_test.Finish("Oathscar weapon ability regression"));
        }
    }

    private void TestOathscarProjectsRealContentOntoBattleUnit()
    {
        using OathscarFixture fixture = OathscarFixture.Build(new GArray());
        _test.True(fixture.ItemDefs.ContainsKey(OathscarItemId), "真实物品内容应包含誓约之痕。");
        _test.True(fixture.TraitDefs.ContainsKey(OathBindTraitId), "真实 trait 应包含誓约绑定。");
        _test.True(fixture.TraitDefs.ContainsKey(OathMarkTraitId), "真实 trait 应包含誓约之印。");
        _test.True(fixture.TraitDefs.ContainsKey(OathJudgmentTraitId), "真实 trait 应包含誓约裁决。");
        _test.True(fixture.TraitDefs.ContainsKey(OathBacklashTraitId), "真实 trait 应包含背誓反噬。");
        _test.True(fixture.Bindings.ContainsKey(OathBindBindingId), "真实装备能力内容应包含誓约绑定 binding。");
        _test.True(fixture.Bindings.ContainsKey(OathMarkBindingId), "真实装备能力内容应包含誓约之印 binding。");
        _test.True(fixture.Bindings.ContainsKey(OathJudgmentBindingId), "真实装备能力内容应包含誓约裁决 binding。");
        _test.True(fixture.Bindings.ContainsKey(OathBacklashBindingId), "真实装备能力内容应包含背誓反噬 binding。");
        AssertOathBindMarkTargetPayload(fixture.Bindings[OathBindBindingId], "registry");
        AssertOathBindMarkTargetPayload(
            fixture.Runtime.GetEquipmentAbilityBindingIndexTyped()[OathBindBindingId],
            "battle runtime"
        );
        AssertOathMarkApplyStatusPayload(fixture.Bindings[OathMarkBindingId], "registry");
        AssertOathMarkApplyStatusPayload(
            fixture.Runtime.GetEquipmentAbilityBindingIndexTyped()[OathMarkBindingId],
            "battle runtime"
        );
        AssertOathBacklashPayload(
            fixture.Runtime.GetEquipmentAbilityBindingIndexTyped()[OathBacklashBindingId],
            "battle runtime"
        );
        _test.True(fixture.SkillDefs.ContainsKey(OathBindSkillId), "真实技能内容应包含誓约绑定装备技能。");
        _test.True(fixture.SkillDefs.ContainsKey(OathJudgmentSkillId), "真实技能内容应包含誓约裁决装备技能。");
        _test.Eq(
            fixture.SkillDefs[OathBindSkillId].CombatProfile?.AttackResolutionModeKind,
            CombatSkillAttackResolutionMode.DirectEffect,
            "誓约绑定是必中装备技能，应显式配置 direct_effect。"
        );
        _test.Eq(
            fixture.SkillDefs[OathJudgmentSkillId].CombatProfile?.AttackResolutionModeKind,
            CombatSkillAttackResolutionMode.DirectEffect,
            "誓约裁决是必中装备技能，应显式配置 direct_effect。"
        );

        BattleUnitState equipped = fixture.BuildOathscarUnit("projection");
        _test.Eq(equipped.weapon_item_id, OathscarItemId, "誓约之痕装备后 unit 应保留真实 item_id。");
        _test.Eq(equipped.weapon_profile_type_id, new StringName("longsword"), "誓约之痕应投影为 longsword。");
        _test.True(equipped.weapon_is_versatile, "誓约之痕应保留 versatile 投影。");
        _test.Eq(equipped.weapon_one_handed_dice?.dice_count ?? 0, 1, "誓约之痕单手应为 1D8+4。");
        _test.Eq(equipped.weapon_one_handed_dice?.dice_sides ?? 0, 8, "誓约之痕单手应为 1D8+4。");
        _test.Eq(equipped.weapon_one_handed_dice?.flat_bonus ?? 0, 4, "誓约之痕单手应为 1D8+4。");
        _test.Eq(equipped.weapon_two_handed_dice?.dice_sides ?? 0, 10, "誓约之痕双手应为 1D10+4。");
        AssertUnitHasTraitAndAbilitySource(equipped, OathBindTraitId, OathBindBindingId, "eq_oathscar_projection");
        AssertUnitHasTraitAndAbilitySource(equipped, OathMarkTraitId, OathMarkBindingId, "eq_oathscar_projection");
        AssertUnitHasTraitAndAbilitySource(equipped, OathJudgmentTraitId, OathJudgmentBindingId, "eq_oathscar_projection");
        AssertUnitHasTraitAndAbilitySource(equipped, OathBacklashTraitId, OathBacklashBindingId, "eq_oathscar_projection");
    }

    private void TestOathBindSelectsOneCurrentTargetAndClearsOldMarksThroughRealCommand()
    {
        using OathscarFixture fixture = OathscarFixture.Build(new GArray());
        BattleUnitState holder = fixture.BuildOathscarUnit("bind");
        BattleUnitState firstTarget = BuildTarget("oath_first_target", new Vector2I(1, 0));
        BattleUnitState secondTarget = BuildTarget("oath_second_target", new Vector2I(0, 1));

        BattleSkillAvailabilityService availabilityService =
            new(fixture.SkillDefs, fixture.Bindings);
        BattleSkillAvailabilityView availability =
            availabilityService.BuildView(
                new BattleSkillAvailabilityQuery
                {
                    User = holder,
                    IncludeEquipmentSkills = true,
                    IncludeKnownSkills = false,
                    Consumer = BattleSkillAvailabilityConsumer.ManualSelection,
                    WorldStep = 0,
                }
            );
        _test.True(
            TryFindSkillEntry(availability, OathBindSkillId, out BattleAvailableSkillEntry bindEntry),
            "装备誓约之痕后应出现誓约绑定装备技能入口。"
        );
        if (bindEntry == null)
            return;
        _test.Eq(bindEntry.EquipmentGrantedActionId, OathBindGrantId, "誓约绑定入口应保留 grant id。");

        WeaponAbilityCommandTestSupport.PrimeActionResources(holder, ap: 2);
        BattleState state = WeaponAbilityCommandTestSupport.BuildFlatState(
            "oathscar_bind",
            holder,
            firstTarget
        );
        AddEnemyToState(state, secondTarget);
        fixture.Runtime.SetupStateForTests(state);
        BattleEventBatch firstBatch = IssueOathBind(fixture.Runtime, holder, firstTarget, bindEntry);
        _test.True(
            ContainsStringName(firstBatch?.ChangedUnitIdsTyped, firstTarget.unit_id),
            "第一次绑定应通过真实命令改写目标 A。"
        );
        _test.True(firstTarget.HasStatusEffect(OathTargetStatusId), "第一次绑定后目标 A 应获得誓言目标标记。");
        IReadOnlyList<BattleEquipmentTargetMarkState> firstMarks = state.GetEquipmentTargetMarksTyped();
        _test.Eq(
            firstMarks.Count > 0 ? firstMarks[0].TargetUnitId : new StringName(""),
            firstTarget.unit_id,
            $"第一次绑定后 typed target mark 应指向目标 A。 | logs={JoinLogs(firstBatch)}"
        );
        _test.Eq(
            firstTarget.GetStatusEffect(OathTargetStatusId)?.source_unit_id ?? new StringName(""),
            holder.unit_id,
            "誓言目标标记应记录持有者来源。"
        );
        firstTarget.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = OathMarkStatusId,
                source_unit_id = holder.unit_id,
                stacks = 4,
                power = 4,
                duration = 10000,
            }
        );

        holder.ResetPerTurnCharges();
        WeaponAbilityCommandTestSupport.PrimeActionResources(holder, ap: 2);
        BattleAvailableSkillEntry nextTurnBindEntry =
            FindRequiredEquipmentSkill(fixture, holder, OathBindSkillId, state);
        _test.True(
            nextTurnBindEntry.IsSelectable,
            "下一行动回合誓约绑定入口应恢复可用，之后才能切换誓言目标。"
        );

        BattleEventBatch secondBatch = IssueOathBind(
            fixture.Runtime,
            holder,
            secondTarget,
            nextTurnBindEntry
        );
        _test.True(
            ContainsStringName(secondBatch?.ChangedUnitIdsTyped, secondTarget.unit_id),
            $"第二次绑定应通过真实命令改写目标 B。 | logs={JoinLogs(secondBatch)}"
        );
        BattleUnitState stateFirstTarget = state.GetUnit(firstTarget.unit_id);
        BattleUnitState stateSecondTarget = state.GetUnit(secondTarget.unit_id);
        _test.True(ReferenceEquals(firstTarget, stateFirstTarget), "目标 A 引用应与 BattleState 内对象一致。");
        _test.True(ReferenceEquals(secondTarget, stateSecondTarget), "目标 B 引用应与 BattleState 内对象一致。");
        _test.Eq(
            state.EquipmentTargetMarkCount,
            1,
            $"切换后 typed target mark store 应只保留一个标记。 | logs={JoinLogs(secondBatch)}"
        );
        IReadOnlyList<BattleEquipmentTargetMarkState> marks = state.GetEquipmentTargetMarksTyped();
        _test.Eq(
            marks.Count > 0 ? marks[0].TargetUnitId : new StringName(""),
            secondTarget.unit_id,
            $"切换后 typed target mark 应指向目标 B。 | logs={JoinLogs(secondBatch)}"
        );
        _test.False(
            stateFirstTarget.HasStatusEffect(OathTargetStatusId),
            $"切换誓言目标后旧目标 A 的誓言目标标记应清除。 | logs={JoinLogs(secondBatch)}"
        );
        _test.False(
            stateFirstTarget.HasStatusEffect(OathMarkStatusId),
            $"切换誓言目标后旧目标 A 的誓约之印应清除。 | logs={JoinLogs(secondBatch)}"
        );
        _test.True(
            stateSecondTarget.HasStatusEffect(OathTargetStatusId),
            $"切换誓言目标后目标 B 应成为唯一誓言目标。 | logs={JoinLogs(secondBatch)}"
        );
        _test.Eq(
            stateSecondTarget.GetStatusEffect(OathTargetStatusId)?.source_unit_id ?? new StringName(""),
            holder.unit_id,
            "新誓言目标标记应记录同一个持有者来源。"
        );
    }

    private void TestOathBindRuntimeServiceSynchronizesMirrorStatus()
    {
        using OathscarFixture fixture = OathscarFixture.Build(new GArray());
        BattleUnitState holder = fixture.BuildOathscarUnit("service");
        BattleUnitState firstTarget = BuildTarget("oath_service_first_target", new Vector2I(1, 0));
        BattleUnitState secondTarget = BuildTarget("oath_service_second_target", new Vector2I(0, 1));
        BattleState state = WeaponAbilityCommandTestSupport.BuildFlatState(
            "oathscar_service_bind",
            holder,
            firstTarget
        );
        AddEnemyToState(state, secondTarget);
        fixture.Runtime.SetupStateForTests(state);

        BattleEventBatch firstBatch = new();
        bool firstChanged = fixture.Runtime.GetEquipmentAbilityRuntimeService().ResolveGrantedSkillUsed(
            new BattleEquipmentAbilityGrantedSkillUsedContext
            {
                SourceUnit = holder,
                TargetUnit = firstTarget,
                BattleState = state,
                Batch = firstBatch,
                BindingId = OathBindBindingId,
                GrantedActionId = OathBindGrantId,
                SkillId = OathBindSkillId,
                SkillEntryId = "",
            }
        );
        _test.True(firstChanged, $"runtime service 第一次绑定应返回 changed。 | logs={JoinLogs(firstBatch)}");
        _test.True(firstTarget.HasStatusEffect(OathTargetStatusId), "runtime service 第一次绑定应写入目标 A 镜像状态。");
        firstTarget.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = OathMarkStatusId,
                source_unit_id = holder.unit_id,
                stacks = 4,
                power = 4,
                duration = 10000,
            }
        );

        BattleEventBatch secondBatch = new();
        bool secondChanged = fixture.Runtime.GetEquipmentAbilityRuntimeService().ResolveGrantedSkillUsed(
            new BattleEquipmentAbilityGrantedSkillUsedContext
            {
                SourceUnit = holder,
                TargetUnit = secondTarget,
                BattleState = state,
                Batch = secondBatch,
                BindingId = OathBindBindingId,
                GrantedActionId = OathBindGrantId,
                SkillId = OathBindSkillId,
                SkillEntryId = "",
            }
        );
        _test.True(secondChanged, $"runtime service 第二次绑定应返回 changed。 | logs={JoinLogs(secondBatch)}");
        _test.False(firstTarget.HasStatusEffect(OathTargetStatusId), "runtime service 切换后应清除旧目标誓言目标。");
        _test.False(firstTarget.HasStatusEffect(OathMarkStatusId), "runtime service 切换后应清除旧目标誓约之印。");
        _test.True(secondTarget.HasStatusEffect(OathTargetStatusId), "runtime service 切换后应写入目标 B 镜像状态。");
    }

    private void TestOathTargetDeathClearsOathAndPreventsBacklash()
    {
        using OathscarFixture fixture = OathscarFixture.Build(new GArray { 2, 2 });
        BattleUnitState holder = fixture.BuildOathscarUnit("dead_oath_holder");
        holder.current_hp = 100;
        holder.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
        BattleUnitState oathTarget = BuildTarget("dead_oath_target", new Vector2I(1, 0));
        BattleUnitState otherTarget = BuildTarget("dead_oath_other", new Vector2I(0, 1));
        BattleState state = WeaponAbilityCommandTestSupport.BuildFlatState(
            "oathscar_dead_oath",
            holder,
            oathTarget
        );
        AddEnemyToState(state, otherTarget);
        fixture.Runtime.SetupStateForTests(state);

        BattleAvailableSkillEntry bindEntry = FindRequiredEquipmentSkill(
            fixture,
            holder,
            OathBindSkillId
        );
        IssueOathBind(fixture.Runtime, holder, oathTarget, bindEntry);
        IssueBasicAttackInCurrentState(fixture.Runtime, holder, oathTarget, "dead_oath_mark");
        _test.True(oathTarget.HasStatusEffect(OathTargetStatusId), "死亡前目标应有誓言目标镜像状态。");
        _test.True(oathTarget.HasStatusEffect(OathMarkStatusId), "死亡前目标应有誓约之印。");
        _test.Eq(state.EquipmentTargetMarkCount, 1, "死亡前 typed target mark 应存在。");

        oathTarget.current_hp = 0;
        oathTarget.is_alive = false;
        using BattleEventBatch deathBatch = new();
        fixture.Runtime.ClearDefeatedUnit(oathTarget, deathBatch);

        _test.Eq(state.EquipmentTargetMarkCount, 0, "誓言目标死亡清理后 typed target mark 应立即解除。");
        _test.False(oathTarget.HasStatusEffect(OathTargetStatusId), "誓言目标死亡清理后镜像誓言目标状态应立即清除。");
        _test.False(oathTarget.HasStatusEffect(OathMarkStatusId), "誓言目标死亡清理后誓约之印应立即清除。");

        int holderHpBefore = holder.current_hp;
        BattleEventBatch batch = IssueBasicAttackInCurrentState(
            fixture.Runtime,
            holder,
            otherTarget,
            "dead_oath_wrong_target"
        );

        _test.Eq(state.EquipmentTargetMarkCount, 0, $"誓言目标死亡后 typed target mark 应保持解除。 logs={JoinLogs(batch)}");
        _test.Eq(
            holder.current_hp,
            holderHpBefore,
            $"誓言目标死亡解除后，命中其他目标不应再触发背誓反噬。 logs={JoinLogs(batch)}"
        );
    }

    private void TestTargetDeathDoesNotClearUnconfiguredTargetMark()
    {
        using OathscarFixture fixture = OathscarFixture.Build(new GArray());
        BattleUnitState holder = fixture.BuildOathscarUnit("unconfigured_mark_holder");
        BattleUnitState target = BuildTarget("unconfigured_mark_target", new Vector2I(1, 0));
        BattleState state = WeaponAbilityCommandTestSupport.BuildFlatState(
            "oathscar_unconfigured_mark_death",
            holder,
            target
        );
        fixture.Runtime.SetupStateForTests(state);
        BattleEquipmentAbilitySourceState source = FindSource(holder, OathBindBindingId);
        _test.True(source != null, "测试前提：持有者应有誓约绑定来源。");
        state.SetEquipmentTargetMark(
            new BattleEquipmentTargetMarkState
            {
                SourceUnitId = holder.unit_id,
                SourceEquipmentInstanceId = source?.SourceEquipmentInstanceId ?? "",
                BindingId = OathBindBindingId,
                StateKey = "non_oath_target",
                TargetUnitId = target.unit_id,
                Stacks = 1,
                RemoveOnSourceMissing = true,
            },
            uniquePerSource: true,
            out _
        );
        _test.Eq(state.EquipmentTargetMarkCount, 1, "测试前提：非誓约 target mark 应存在。");

        target.current_hp = 0;
        target.is_alive = false;
        using BattleEventBatch deathBatch = new();
        fixture.Runtime.ClearDefeatedUnit(target, deathBatch);

        _test.Eq(
            state.EquipmentTargetMarkCount,
            1,
            $"没有配置目标死亡解除的 target mark 不应被全局死亡清理移除。 marks={DumpMarks(state)}"
        );
    }

    private void TestOathMarkStacksOnlyOnCurrentOathTargetAndModifiesAttackCheck()
    {
        using OathscarFixture fixture = OathscarFixture.Build(new GArray());
        BattleUnitState holder = fixture.BuildOathscarUnit("mark_holder");
        BattleUnitState oathTarget = BuildTarget("oath_mark_target", new Vector2I(1, 0));
        BattleUnitState otherTarget = BuildTarget("oath_mark_other", new Vector2I(0, 1));
        BattleState state = WeaponAbilityCommandTestSupport.BuildFlatState(
            "oathscar_mark_stack",
            holder,
            oathTarget
        );
        AddEnemyToState(state, otherTarget);
        fixture.Runtime.SetupStateForTests(state);

        BattleAvailableSkillEntry bindEntry = FindRequiredEquipmentSkill(
            fixture,
            holder,
            OathBindSkillId
        );
        IssueOathBind(fixture.Runtime, holder, oathTarget, bindEntry);
        _test.True(
            state.TryGetEquipmentTargetMark(
                holder.unit_id,
                new StringName("eq_oathscar_mark_holder"),
                OathBindBindingId,
                new StringName("oath_target"),
                out BattleEquipmentTargetMarkState resolvedMark
            ),
            $"绑定后应能通过装备实例查询到 oath_target target mark。 | marks={DumpMarks(state)}"
        );
        _test.Eq(
            resolvedMark?.TargetUnitId ?? new StringName(""),
            oathTarget.unit_id,
            $"绑定后的 oath_target target mark 应指向当前目标。 | marks={DumpMarks(state)}"
        );

        for (int hit = 1; hit <= 5; hit++)
        {
            int hpBefore = oathTarget.current_hp;
            BattleEventBatch batch = IssueBasicAttackInCurrentState(
                fixture.Runtime,
                holder,
                oathTarget,
                $"oath_mark_hit_{hit}"
            );
            _test.True(
                oathTarget.current_hp < hpBefore,
                $"第 {hit} 次基础攻击应真实造成武器伤害。 | logs={JoinLogs(batch)}"
            );
            BattleStatusEffectState mark = oathTarget.GetStatusEffect(OathMarkStatusId);
            _test.True(mark != null, $"第 {hit} 次命中当前誓言目标后应叠加誓约之印。");
            if (mark == null)
                continue;
            _test.Eq(mark.stacks, Math.Min(hit, 4), "誓约之印应最多叠加到 4 层。");
            _test.Eq(mark.source_unit_id, holder.unit_id, "誓约之印应记录持有者来源。");
            _test.Eq(
                mark.source_bound_incoming_attack_roll_bonus_per_stack,
                1,
                "誓约之印状态实例应保留每层 +1 来源绑定攻击检定加值。"
            );
        }

        BattleAttackCheckPolicyService attackPolicy =
            fixture.Runtime.GetAttackCheckPolicyService();
        SkillDefinition attackSkill = TestSkillDefinitionProjection.BuildSkill("fixture_basic_attack");
        BattleAttackRollModifierBundle againstMarkedTarget = attackPolicy.BuildModifierBundle(
            attackPolicy.BuildSkillDefinitionAttackContext(
                state,
                holder,
                oathTarget,
                attackSkill,
                "skill_attack_check",
                "oathscar_mark_bonus",
                force_hit_no_crit: false
            )
        );
        BattleAttackRollModifierBundle againstOtherTarget = attackPolicy.BuildModifierBundle(
            attackPolicy.BuildSkillDefinitionAttackContext(
                state,
                holder,
                otherTarget,
                attackSkill,
                "skill_attack_check",
                "oathscar_mark_bonus",
                force_hit_no_crit: false
            )
        );
        BattleAttackRollModifierBundle otherAttacker = attackPolicy.BuildModifierBundle(
            attackPolicy.BuildSkillDefinitionAttackContext(
                state,
                otherTarget,
                oathTarget,
                attackSkill,
                "skill_attack_check",
                "oathscar_mark_bonus",
                force_hit_no_crit: false
            )
        );

        _test.Eq(
            againstMarkedTarget.TotalBonus,
            4,
            "4 层誓约之印应让持有者攻击当前誓言目标时获得 +4 攻击检定。"
        );
        _test.True(
            HasModifier(againstMarkedTarget, OathMarkStatusId, 4),
            "誓约之印 +4 应进入 modifier breakdown。"
        );
        _test.Eq(againstOtherTarget.TotalBonus, 0, "誓约之印不应加成非誓言目标。");
        _test.Eq(otherAttacker.TotalBonus, 0, "誓约之印不应加成非来源持有者。");

        IssueBasicAttackInCurrentState(fixture.Runtime, holder, otherTarget, "oath_mark_wrong_target");
        _test.False(otherTarget.HasStatusEffect(OathMarkStatusId), "命中非当前誓言目标不应叠加誓约之印。");
    }

    private void TestOathMarkAfterHitConditionResolvesBoundTargetMark()
    {
        using OathscarFixture fixture = OathscarFixture.Build(new GArray());
        BattleUnitState holder = fixture.BuildOathscarUnit("mark_direct");
        BattleUnitState oathTarget = BuildTarget("oath_mark_direct_target", new Vector2I(1, 0));
        BattleState state = WeaponAbilityCommandTestSupport.BuildFlatState(
            "oathscar_mark_direct",
            holder,
            oathTarget
        );
        fixture.Runtime.SetupStateForTests(state);

        BattleAvailableSkillEntry bindEntry = FindRequiredEquipmentSkill(
            fixture,
            holder,
            OathBindSkillId
        );
        IssueOathBind(fixture.Runtime, holder, oathTarget, bindEntry);

        BattleEquipmentAbilityAfterHitResult result = fixture
            .Runtime
            .GetEquipmentAbilityRuntimeService()
            .ResolveAfterHit(
                new BattleEquipmentAbilityAfterHitContext
                {
                    SourceUnit = holder,
                    TargetUnit = oathTarget,
                    BattleState = state,
                    AttackSucceeded = true,
                    CriticalHit = false,
                    ApplyDamageDiceActions = false,
                    SaveContext = BattleSaveContext.Empty,
                }
            );

        _test.True(result.Resolved, "直接 after-hit 服务应能解析誓约之印 reaction。");
        _test.True(oathTarget.HasStatusEffect(OathMarkStatusId), "直接 after-hit 服务应通过绑定目标 fact 写入誓约之印。");
    }

    private void TestOathJudgmentIsGuaranteedSkillAndConsumesCurrentOathMarks()
    {
        using OathscarFixture fixture = OathscarFixture.Build(new GArray { 2, 2, 2, 2 });
        BattleUnitState holder = fixture.BuildOathscarUnit("judgment_holder");
        BattleUnitState target = BuildTarget("judgment_target", new Vector2I(1, 0));
        target.current_hp = 100;
        target.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
        BattleState state = WeaponAbilityCommandTestSupport.BuildFlatState(
            "oathscar_judgment",
            holder,
            target
        );
        fixture.Runtime.SetupStateForTests(state);

        BattleAvailableSkillEntry bindEntry = FindRequiredEquipmentSkill(
            fixture,
            holder,
            OathBindSkillId
        );
        IssueOathBind(fixture.Runtime, holder, target, bindEntry);
        BattleAvailableSkillEntry judgmentEntry = FindRequiredEquipmentSkill(
            fixture,
            holder,
            OathJudgmentSkillId
        );
        _test.Eq(judgmentEntry.EquipmentGrantedActionId, OathJudgmentGrantId, "誓约裁决入口应保留 grant id。");
        _test.Eq(
            judgmentEntry.EquipmentUsagePeriodKind,
            EquipmentAbilityUsagePeriodKind.PerBattle,
            "誓约裁决应每场战斗限次，而不是靠技能冷却表达。"
        );
        _test.Eq(judgmentEntry.EquipmentMaxUsesPerPeriod, 1, "誓约裁决每场战斗限 1 次。");

        fixture.Runtime.ConfigureHitResolverForTests(new FixedMissResolver());
        int blockedHp = target.current_hp;
        WeaponAbilityCommandTestSupport.PrimeActionResources(holder);
        ForceUnitActing(state, holder);
        BattleCommand blockedCommand = WeaponAbilityCommandTestSupport.BuildUnitSkillCommand(
            holder,
            target,
            judgmentEntry,
            OathJudgmentSkillId
        );
        BattleEventBatch blockedBatch = fixture.Runtime.IssueCommand(blockedCommand);
        _test.True(
            HasLogLineContaining(blockedBatch, OathMarkStatusId.ToString()),
            $"不足 4 层誓约之印时誓约裁决应被目标状态门禁拦截。logs={JoinLogs(blockedBatch)}"
        );
        _test.Eq(target.current_hp, blockedHp, "不足 4 层誓约之印时，誓约裁决不应造成伤害。");
        _test.False(target.HasStatusEffect(StunnedStatusId), "不足 4 层誓约之印时，誓约裁决不应震慑。");
        BattleSkillAvailabilityView afterBlocked =
            BuildEquipmentSkillAvailability(fixture, holder, state);
        _test.True(
            TryFindSkillEntry(afterBlocked, OathJudgmentSkillId, out BattleAvailableSkillEntry afterBlockedEntry),
            "目标状态门禁失败后仍应保留誓约裁决入口用于 UI 展示。"
        );
        _test.True(
            afterBlockedEntry?.IsSelectable == true,
            "目标状态门禁失败不应消耗誓约裁决每场战斗次数或行动回合使用次数。"
        );

        fixture.Runtime.ConfigureHitResolverForTests(new FixedHitResolver(10));
        for (int hit = 1; hit <= 4; hit++)
        {
            IssueBasicAttackInCurrentState(fixture.Runtime, holder, target, $"oath_judgment_mark_{hit}");
        }
        _test.Eq(target.GetStatusEffect(OathMarkStatusId)?.stacks ?? 0, 4, "裁决前目标应有 4 层誓约之印。");

        fixture.Runtime.ConfigureHitResolverForTests(new FixedMissResolver());
        int hpBefore = target.current_hp;
        IssueUnitSkillInCurrentState(
            fixture.Runtime,
            holder,
            target,
            judgmentEntry,
            OathJudgmentSkillId,
            "oath_judgment_applied"
        );
        _test.True(
            target.current_hp < hpBefore,
            "誓约裁决应作为必中装备技能直接造成 radiant 伤害，即使命中 resolver 被设为 miss。"
        );
        _test.True(target.HasStatusEffect(StunnedStatusId), "誓约裁决应施加 stunned。");
        _test.False(target.HasStatusEffect(OathMarkStatusId), "誓约裁决应消耗全部誓约之印。");

        BattleSkillAvailabilityView exhausted =
            BuildEquipmentSkillAvailability(fixture, holder, state);
        _test.True(
            TryFindSkillEntry(exhausted, OathJudgmentSkillId, out BattleAvailableSkillEntry exhaustedEntry),
            "次数耗尽后仍应保留誓约裁决入口用于 UI 展示。"
        );
        _test.False(exhaustedEntry.IsSelectable, "誓约裁决每场战斗用过后应禁用。");
    }

    private void TestOathBacklashPunishesWeaponHitAgainstNonOathTarget()
    {
        using OathscarFixture fixture = OathscarFixture.Build(new GArray { 2, 2 });
        BattleUnitState holder = fixture.BuildOathscarUnit("backlash_holder");
        holder.current_hp = 100;
        holder.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
        BattleUnitState oathTarget = BuildTarget("backlash_oath_target", new Vector2I(1, 0));
        BattleUnitState otherTarget = BuildTarget("backlash_other_target", new Vector2I(0, 1));
        BattleState state = WeaponAbilityCommandTestSupport.BuildFlatState(
            "oathscar_backlash",
            holder,
            oathTarget
        );
        AddEnemyToState(state, otherTarget);
        fixture.Runtime.SetupStateForTests(state);

        BattleAvailableSkillEntry bindEntry = FindRequiredEquipmentSkill(
            fixture,
            holder,
            OathBindSkillId
        );
        IssueOathBind(fixture.Runtime, holder, oathTarget, bindEntry);
        IssueBasicAttackInCurrentState(fixture.Runtime, holder, oathTarget, "oath_backlash_mark");
        _test.True(oathTarget.HasStatusEffect(OathMarkStatusId), "反噬测试前誓言目标应已有誓约之印。");

        int holderHpBefore = holder.current_hp;
        int holderShieldBefore = holder.current_shield_hp;
        BattleEventBatch backlashBatch = IssueBasicAttackInCurrentState(
            fixture.Runtime,
            holder,
            otherTarget,
            "oath_backlash_wrong_target"
        );
        _test.True(
            holder.current_hp < holderHpBefore,
            $"攻击非誓言目标命中后，持有者应受到 psychic 反噬伤害。 hp_before={holderHpBefore} hp_after={holder.current_hp} shield_before={holderShieldBefore} shield_after={holder.current_shield_hp} oath_mark={oathTarget.HasStatusEffect(OathMarkStatusId)} logs={JoinLogs(backlashBatch)}"
        );
        _test.False(oathTarget.HasStatusEffect(OathMarkStatusId), "背誓反噬应清除当前誓言目标上的誓约之印。");
        _test.True(oathTarget.HasStatusEffect(OathTargetStatusId), "背誓反噬不应解除誓言目标本身。");
    }

    private void TestGeneratedDealDamageEffectCanDamageSource()
    {
        using OathscarFixture fixture = OathscarFixture.Build(new GArray { 2, 2 });
        BattleUnitState holder = fixture.BuildOathscarUnit("deal_damage_direct_holder");
        holder.current_hp = 100;
        holder.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);

        AttackEffectResolutionResult result = fixture.Runtime.GetDamageResolver().ResolveEffects(
            holder,
            holder,
            new[]
            {
                BattleRuntimeEffectDefinitions.Damage(
                    "psychic",
                    diceCount: 2,
                    diceSides: 6,
                    diceBonus: 0,
                    damageTags: new[] { new StringName("psychic") }
                ),
            },
            DamageResolutionContext.Empty()
        );

        _test.True(
            holder.current_hp < 100,
            $"runtime 生成的 psychic direct damage effect 应能扣除 source HP。 hp_after={holder.current_hp} damage={result.Damage}"
        );
    }

    private void TestOathBacklashDirectAfterHitDealsDamage()
    {
        using OathscarFixture fixture = OathscarFixture.Build(new GArray { 2, 2 });
        BattleUnitState holder = fixture.BuildOathscarUnit("backlash_direct_holder");
        holder.current_hp = 100;
        holder.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
        BattleUnitState oathTarget = BuildTarget("backlash_direct_oath_target", new Vector2I(1, 0));
        BattleUnitState otherTarget = BuildTarget("backlash_direct_other_target", new Vector2I(0, 1));
        BattleState state = WeaponAbilityCommandTestSupport.BuildFlatState(
            "oathscar_backlash_direct",
            holder,
            oathTarget
        );
        AddEnemyToState(state, otherTarget);
        fixture.Runtime.SetupStateForTests(state);

        BattleAvailableSkillEntry bindEntry = FindRequiredEquipmentSkill(
            fixture,
            holder,
            OathBindSkillId
        );
        IssueOathBind(fixture.Runtime, holder, oathTarget, bindEntry);
        oathTarget.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = OathMarkStatusId,
                source_unit_id = holder.unit_id,
                stacks = 1,
                power = 1,
                duration = 10000,
            }
        );

        int hpBefore = holder.current_hp;
        int shieldBefore = holder.current_shield_hp;
        fixture
            .Runtime
            .GetEquipmentAbilityRuntimeService()
            .ResolveAfterHit(
                new BattleEquipmentAbilityAfterHitContext
                {
                    SourceUnit = holder,
                    TargetUnit = otherTarget,
                    BattleState = state,
                    AttackSucceeded = true,
                    CriticalHit = false,
                    ApplyDamageDiceActions = false,
                    SaveContext = BattleSaveContext.Empty,
                }
            );

        _test.True(
            holder.current_hp < hpBefore,
            $"直接 after-hit 背誓反噬应扣除持有者 HP。 hp_before={hpBefore} hp_after={holder.current_hp} shield_before={shieldBefore} shield_after={holder.current_shield_hp}"
        );
        _test.False(oathTarget.HasStatusEffect(OathMarkStatusId), "直接 after-hit 背誓反噬应清除誓约之印。");
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

    private static BattleSkillAvailabilityView BuildEquipmentSkillAvailability(
        OathscarFixture fixture,
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
        OathscarFixture fixture,
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

    private void AssertOathBindMarkTargetPayload(EquipmentAbilityBindingDefinition binding, string ownerLabel)
    {
        EquipmentAbilityActionDefinition action = binding?.Reactions?[0]?.Actions?[0];
        _test.True(
            action?.PayloadDefinition is MarkTargetActionPayloadDefinition,
            $"{ownerLabel} 誓约绑定 reaction 应投影为 typed mark_target payload。"
        );
        MarkTargetActionPayloadDefinition payload =
            action?.PayloadDefinition as MarkTargetActionPayloadDefinition;
        if (payload == null)
            return;
        _test.Eq(payload.TargetSelector, new StringName("skill_target"), $"{ownerLabel} mark_target 应读取技能目标。");
        _test.Eq(payload.StateKey, new StringName("oath_target"), $"{ownerLabel} mark_target 应写入 oath_target 状态键。");
        _test.True(payload.RemoveOnTargetDefeated, $"{ownerLabel} 誓约绑定应配置为目标死亡解除誓约。");
        _test.Eq(payload.MirrorStatusId, OathTargetStatusId, $"{ownerLabel} mark_target 应配置镜像誓言目标状态。");
        _test.True(
            ContainsStringName(payload.ClearStatusIdsOnReplace, OathMarkStatusId),
            $"{ownerLabel} mark_target 切换目标时应清除旧目标誓约之印。"
        );
    }

    private void AssertOathMarkApplyStatusPayload(EquipmentAbilityBindingDefinition binding, string ownerLabel)
    {
        _test.True(
            binding?.Reactions?.Count == 1,
            $"{ownerLabel} 誓约之印 binding 应包含 after-hit reaction。"
        );
        EquipmentAbilityActionDefinition action = binding?.Reactions?[0]?.Actions?[0];
        _test.True(
            action?.PayloadDefinition is ApplyStatusActionPayloadDefinition,
            $"{ownerLabel} 誓约之印 reaction 应投影为 typed apply_status payload。"
        );
        ApplyStatusActionPayloadDefinition payload =
            action?.PayloadDefinition as ApplyStatusActionPayloadDefinition;
        if (payload == null)
            return;
        _test.Eq(payload.TargetSelector, new StringName("attack_target"), $"{ownerLabel} 誓约之印应写入攻击目标。");
        _test.Eq(payload.StatusId, OathMarkStatusId, $"{ownerLabel} 誓约之印 status_id 应正确。");
        _test.Eq(payload.StackLimit, 4, $"{ownerLabel} 誓约之印上限应为4层。");
        _test.Eq(
            payload.SourceBoundIncomingAttackRollBonusPerStack,
            1,
            $"{ownerLabel} 誓约之印每层应给来源持有者+1攻击检定。"
        );
    }

    private void AssertOathBacklashPayload(EquipmentAbilityBindingDefinition binding, string ownerLabel)
    {
        _test.True(
            binding?.Reactions?.Count == 1,
            $"{ownerLabel} 背誓反噬 binding 应包含 after-hit reaction。"
        );
        IReadOnlyList<EquipmentAbilityActionDefinition> actions =
            binding?.Reactions?[0]?.Actions ?? Array.Empty<EquipmentAbilityActionDefinition>();
        _test.Eq(actions.Count, 2, $"{ownerLabel} 背誓反噬应配置清印和自伤两个 action。");
        EquipmentAbilityActionDefinition clearAction = actions.Count > 0 ? actions[0] : null;
        EquipmentAbilityActionDefinition damageAction = actions.Count > 1 ? actions[1] : null;
        _test.Eq(clearAction?.Kind ?? new StringName(""), new StringName("clear_status"), $"{ownerLabel} 背誓反噬第一个 action kind 应为 clear_status。");
        _test.Eq(damageAction?.Kind ?? new StringName(""), new StringName("deal_damage"), $"{ownerLabel} 背誓反噬第二个 action kind 应为 deal_damage。");
        _test.True(
            clearAction?.PayloadDefinition is ClearStatusActionPayloadDefinition,
            $"{ownerLabel} 背誓反噬第一个 action 应投影为 typed clear_status payload。"
        );
        _test.True(
            damageAction?.PayloadDefinition is DealDamageActionPayloadDefinition,
            $"{ownerLabel} 背誓反噬第二个 action 应投影为 typed deal_damage payload。"
        );
        DealDamageActionPayloadDefinition damagePayload =
            damageAction?.PayloadDefinition as DealDamageActionPayloadDefinition;
        if (damagePayload == null)
            return;
        _test.Eq(damagePayload.TargetSelector, new StringName("source"), $"{ownerLabel} 背誓反噬自伤目标应为 source。");
        _test.Eq(damagePayload.DamageType, new StringName("psychic"), $"{ownerLabel} 背誓反噬伤害类型应为 psychic。");
        _test.Eq(damagePayload.Dice?.Terms?.Count ?? 0, 1, $"{ownerLabel} 背誓反噬应投影 2D6 伤害骰。");
        _test.Eq(
            damagePayload.Dice?.Terms?[0]?.DiceCount ?? 0,
            2,
            $"{ownerLabel} 背誓反噬应为 2D6。"
        );
        _test.Eq(
            damagePayload.Dice?.Terms?[0]?.DiceSides ?? 0,
            6,
            $"{ownerLabel} 背誓反噬应为 2D6。"
        );
    }

    private static BattleEventBatch IssueOathBind(
        BattleRuntimeModule runtime,
        BattleUnitState holder,
        BattleUnitState target,
        BattleAvailableSkillEntry bindEntry
    )
    {
        BattleCommand command = WeaponAbilityCommandTestSupport.BuildUnitSkillCommand(
            holder,
            target,
            bindEntry,
            OathBindSkillId
        );
        BattlePreview preview = runtime.PreviewCommand(command);
        try
        {
            if (preview?.allowed != true)
            {
                BattleUnitState stateHolder =
                    runtime?.GetState()?.GetUnit(holder?.unit_id ?? new StringName(""));
                throw new InvalidOperationException(
                    $"oath bind preview blocked for {target?.unit_id}: {JoinLogs(preview)} | entry_selectable={bindEntry?.IsSelectable} entry_disabled={bindEntry?.DisabledReason} holder_charges={SummarizePerTurnCharges(holder)} state_holder_charges={SummarizePerTurnCharges(stateHolder)}"
                );
            }
        }
        finally
        {
        }
        return runtime.IssueCommand(command);
    }

    private static BattleEventBatch IssueBasicAttackInCurrentState(
        BattleRuntimeModule runtime,
        BattleUnitState attacker,
        BattleUnitState target,
        StringName label
    )
    {
        WeaponAbilityCommandTestSupport.PrimeBasicAttack(attacker);
        ForceUnitActing(runtime?.GetState(), attacker);
        BattleCommand command = WeaponAbilityCommandTestSupport.BuildBasicAttackCommand(attacker, target);
        BattlePreview preview = runtime.PreviewCommand(command);
        if (preview?.allowed != true)
        {
            throw new InvalidOperationException(
                $"{label} basic_attack preview blocked: {JoinLogs(preview)}"
            );
        }
        return runtime.IssueCommand(command);
    }

    private static BattleEventBatch IssueUnitSkillInCurrentState(
        BattleRuntimeModule runtime,
        BattleUnitState user,
        BattleUnitState target,
        BattleAvailableSkillEntry entry,
        StringName skillId,
        StringName label
    )
    {
        WeaponAbilityCommandTestSupport.PrimeActionResources(user);
        ForceUnitActing(runtime?.GetState(), user);
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
                $"{label} unit skill preview blocked: {JoinLogs(preview)}"
            );
        }
        return runtime.IssueCommand(command);
    }

    private static void ForceUnitActing(BattleState state, BattleUnitState unit)
    {
        if (state == null || unit == null)
            return;
        state.PhaseKind = BattlePhaseKind.UnitActing;
        state.active_unit_id = unit.unit_id;
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

    private static bool ContainsStringName(IEnumerable<StringName> values, StringName expected)
    {
        if (values == null)
            return false;
        foreach (StringName value in values)
        {
            if (value == expected)
                return true;
        }
        return false;
    }

    private static string JoinLogs(BattlePreview preview) =>
        string.Join(" | ", preview?.LogLinesTyped ?? Array.Empty<string>());

    private static string JoinLogs(BattleEventBatch batch) =>
        string.Join(" | ", batch?.LogLinesTyped ?? Array.Empty<string>());

    private static bool HasLogLineContaining(BattleEventBatch batch, string expected)
    {
        foreach (string line in batch?.LogLinesTyped ?? Array.Empty<string>())
        {
            if (!string.IsNullOrEmpty(line) && line.Contains(expected, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private static string DumpMarks(BattleState state)
    {
        var entries = new List<string>();
        foreach (BattleEquipmentTargetMarkState mark in state?.GetEquipmentTargetMarksTyped() ?? Array.Empty<BattleEquipmentTargetMarkState>())
        {
            entries.Add(
                $"{mark.SourceUnitId}/{mark.SourceEquipmentInstanceId}/{mark.BindingId}/{mark.StateKey}->{mark.TargetUnitId}:{mark.Stacks}"
            );
        }
        return string.Join(", ", entries);
    }

    private static string SummarizePerTurnCharges(BattleUnitState unit)
    {
        var entries = new List<string>();
        foreach ((StringName key, int value) in unit?.GetPerTurnChargesTyped() ?? new Dictionary<StringName, int>())
        {
            entries.Add($"{key}:{value}");
        }
        entries.Sort(StringComparer.Ordinal);
        return entries.Count == 0 ? "<none>" : string.Join(",", entries);
    }

    private static void AddEnemyToState(BattleState state, BattleUnitState unit)
    {
        if (state == null || unit == null)
            return;
        state.SetUnit(unit);
        unit.RefreshFootprint();
        foreach (Vector2I coord in unit.occupied_coords)
        {
            state.GetCell(coord)?.SetOccupant(unit.unit_id);
        }
        if (!state.enemy_unit_ids.Contains(unit.unit_id))
            state.enemy_unit_ids.Add(unit.unit_id);
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

    private static BattleUnitState BuildTarget(StringName unitId, Vector2I coord)
    {
        BattleUnitState unit = new()
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = "enemy",
            is_alive = true,
            current_hp = 80,
        };
        unit.SetAnchorCoord(coord);
        unit.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS, 14);
        unit.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, 0);
        unit.attribute_snapshot.SetValue(AttributeService.BASE_ATTACK_BONUS, 0);
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, 80);
        unit.SetEquipmentView(new EquipmentState());
        return unit;
    }

    private sealed class OathscarFixture : IDisposable
    {
        private readonly ItemContentRegistry _itemRegistry;
        private readonly ProgressionContentRegistry _progressionRegistry;
        private readonly PartyState _partyState;

        private OathscarFixture(
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

        internal static OathscarFixture Build(GArray damageRolls)
        {
            ItemContentRegistry itemRegistry = new();
            ProgressionContentRegistry progressionRegistry = new();
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
            runtime.ConfigureDamageResolverForTests(new FixedRollDamageResolver(damageRolls));
            runtime.ConfigureHitResolverForTests(new FixedHitResolver(10));
            return new OathscarFixture(itemRegistry, progressionRegistry, partyState, runtime);
        }

        internal BattleUnitState BuildOathscarUnit(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            member.equipment_state.SetEquippedEntry(
                "main_hand",
                OathscarItemId,
                new GStringNameArray { "main_hand" },
                EquipmentInstanceState.CreateInstance(OathscarItemId, $"eq_oathscar_{label}")
            );
            IReadOnlyList<BattleUnitState> units =
                Runtime._unit_factory.BuildAllyUnits(_partyState, new GDictionary());
            if (units.Count != 1)
                throw new InvalidOperationException($"{label} scenario should build exactly one ally unit.");
            BattleUnitState unit = units[0];
            unit.SetAnchorCoord(Vector2I.Zero);
            unit.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, 0);
            unit.attribute_snapshot.SetValue(AttributeService.BASE_ATTACK_BONUS, 0);
            return unit;
        }

        public void Dispose()
        {
            Runtime?.dispose();
            _itemRegistry?.Dispose();
            _progressionRegistry?.Dispose();
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
