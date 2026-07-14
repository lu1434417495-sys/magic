using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_executioner_axe_weapon_ability_regression : LifecycleTestSceneTree
{
    private static readonly StringName ItemId = "weapon_unique_greataxe_executioner_384";
    private static readonly StringName ExecutionTraitId = "weapon.axe.executioner.execution";
    private static readonly StringName DeathSentenceTraitId = "weapon.axe.executioner.death_sentence";
    private static readonly StringName SelfExecutionTraitId = "weapon.axe.executioner.self_execution";
    private static readonly StringName ExecutionBindingId = "binding.weapon.axe.executioner.execution";
    private static readonly StringName DeathSentenceBindingId =
        "binding.weapon.axe.executioner.death_sentence";
    private static readonly StringName SelfExecutionBindingId =
        "binding.weapon.axe.executioner.self_execution";
    private static readonly StringName DeathSentenceSkillId =
        "weapon_axe_executioner_death_sentence";
    private static readonly StringName JudgmentResolutionSkillId =
        "weapon_axe_executioner_judgment_resolution";
    private static readonly StringName JudgmentFallbackSkillId =
        "weapon_axe_executioner_judgment_fallback";
    private static readonly StringName SelfExecutionSkillId =
        "weapon_axe_executioner_self_execution";
    private static readonly StringName ExecutionFearSkillId =
        "weapon_axe_executioner_execution_fear";
    private static readonly StringName DeathSentenceGrantId =
        "grant.executioner_axe.death_sentence.skill";
    private static readonly StringName DeathSentenceStatusId = "executioner_death_sentence";
    private static readonly StringName FrightenedStatusId = "frightened";
    private static readonly StringName JudgmentMarkStateKey = "death_sentence_target";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            if (!RequiredContentExists())
            {
                _test.Fail("处刑者之斧正式内容尚未落地。测试应先以缺少内容失败。 ");
                RequestTestExit(_test.Finish("Executioner Axe weapon ability regression"));
                return;
            }

            TestContentProjectionAndInternalSkillVisibility();
            TestDeathSentenceCostsCooldownAndMissPreservesMark();
            TestCritLockSuppressesForcedCriticalPreviewAndResolution();
            TestCritLockedLethalHitDoesNotCountAsExecution();
            TestNonCriticalKillProvenanceDropsForcedCriticalSource();
            TestForcedCriticalProvenanceOverridesOuterEquipmentAttack();
            TestMarkedHitForcesCriticalAndSuccessfulJudgmentTriggersResistedBacklash();
            TestOrdinaryJudgmentExecutionFrightensOnlyNearbyEnemies();
            TestEliteAndBossPostHitThresholdBranches();
            TestDeathPreventionSurvivalTriggersSelfExecution();
            TestSelfExecutionCanDefeatWielder();
            TestUnusedMarkExpiryTriggersSelfExecutionOnce();
            TestUnequippingExecutionerClearsMarkWithoutBacklash();
            TestDurabilityDestructionClearsMarkWithoutBacklash();
            TestConcurrentSourceMarksExpireIndependently();
            TestConsumingLatestSourceRestoresRemainingMirror();
            TestUnmarkedKillDoesNotTriggerExecutionFear();
            RequestTestExit(_test.Finish("Executioner Axe weapon ability regression"));
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
            RequestTestExit(_test.Finish("Executioner Axe weapon ability regression"));
        }
    }

    private bool RequiredContentExists()
    {
        using ItemContentRegistry items = new(new TestContentResourceLoader());
        using ProgressionContentRegistry progression = new(new TestContentResourceLoader());
        return items.GetItemDefsTyped().ContainsKey(ItemId)
            && progression.GetTraitDefsTyped().ContainsKey(ExecutionTraitId)
            && progression.GetTraitDefsTyped().ContainsKey(DeathSentenceTraitId)
            && progression.GetTraitDefsTyped().ContainsKey(SelfExecutionTraitId)
            && progression.GetSkillDefinitionsTyped().ContainsKey(DeathSentenceSkillId)
            && progression.GetSkillDefinitionsTyped().ContainsKey(JudgmentResolutionSkillId)
            && progression.GetSkillDefinitionsTyped().ContainsKey(JudgmentFallbackSkillId)
            && progression.GetSkillDefinitionsTyped().ContainsKey(SelfExecutionSkillId)
            && progression.GetSkillDefinitionsTyped().ContainsKey(ExecutionFearSkillId)
            && progression
                .GetEquipmentAbilityBindingDefinitionsTyped()
                .ContainsKey(ExecutionBindingId)
            && progression
                .GetEquipmentAbilityBindingDefinitionsTyped()
                .ContainsKey(DeathSentenceBindingId)
            && progression
                .GetEquipmentAbilityBindingDefinitionsTyped()
                .ContainsKey(SelfExecutionBindingId);
    }

    private void TestContentProjectionAndInternalSkillVisibility()
    {
        using ExecutionerFixture fixture = ExecutionerFixture.Build();
        using TestContentResourceLoader contentLoader = new();
        ItemDef rawItem = contentLoader.LoadCanonical<ItemDef>(
            "res://data/configs/items/weapon_unique_greataxe_executioner.tres"
        );
        _test.True(rawItem != null, "处刑者之斧物品资源应能加载。");
        if (rawItem != null)
        {
            _test.Eq(rawItem.item_id, ItemId, "物品 id 应保留源设计编号。");
            _test.Eq(rawItem.display_name, "处刑者之斧", "物品显示名应匹配设计。");
            _test.Eq(rawItem.base_item_id, new StringName("weapon_type_greataxe_base"), "应继承巨斧模板。");
            _test.Eq(rawItem.base_price, 55000, "基础价格应为 55000。");
            _test.True(rawItem.trait_ids.Contains(ExecutionTraitId), "物品应声明处刑特性。");
            _test.True(rawItem.trait_ids.Contains(DeathSentenceTraitId), "物品应声明死亡判决特性。");
            _test.True(rawItem.trait_ids.Contains(SelfExecutionTraitId), "物品应声明自我处刑特性。");
        }

        SkillDefinition deathSentence = fixture.SkillDefs[DeathSentenceSkillId];
        _test.Eq(deathSentence.MaxLevel, 1, "死亡判决应固定为 1 级装备技能。");
        _test.Eq(deathSentence.CombatProfile.RangeValue, 1, "死亡判决射程应为 1。");
        _test.Eq(deathSentence.CombatProfile.ApCost, 1, "死亡判决应消耗 1AP。");
        _test.Eq(deathSentence.CombatProfile.StaminaCost, 60, "死亡判决应消耗 60 体力。");
        _test.Eq(deathSentence.CombatProfile.CooldownTu, 300, "死亡判决冷却应为 300TU。");
        _test.True(
            HasStatusEffectDefinition(
                deathSentence,
                DeathSentenceStatusId,
                durationTu: 60,
                saveDc: 0
            ),
            "死亡判决应施加无豁免的 60TU 判决标记。"
        );

        SkillDefinition fear = fixture.SkillDefs[ExecutionFearSkillId];
        _test.Eq(fear.CombatProfile.AreaPattern, new StringName("diamond"), "内部恐惧技能应使用菱形范围。");
        _test.Eq(fear.CombatProfile.AreaValue, 3, "内部恐惧技能半径应为 3 格。");
        _test.Eq(fear.CombatProfile.MinTargetCount, 1, "内部恐惧技能至少需要一个合法目标。");
        _test.True(
            HasStatusEffectDefinition(fear, FrightenedStatusId, durationTu: 60, saveDc: 14),
            "内部恐惧技能应进行 DC14 检定并施加 60TU frightened。"
        );
        CombatEffectDefinition fearEffect = FindFirstEffectDefinition(fear, "status");
        _test.Eq(
            fearEffect?.SaveTag ?? new StringName(""),
            new StringName("frightened"),
            "内部恐惧技能必须使用 frightened 保存标签。"
        );

        CombatEffectDefinition selfExecutionEffect = FindFirstEffectDefinition(
            fixture.SkillDefs[SelfExecutionSkillId],
            "damage"
        );
        _test.Eq(
            selfExecutionEffect?.SaveTag ?? new StringName(""),
            new StringName("willpower"),
            "自我处刑必须使用 willpower 保存标签。"
        );

        SkillDefinition judgment = fixture.SkillDefs[JudgmentResolutionSkillId];
        IReadOnlyList<CombatEffectDefinition> judgmentEffects =
            judgment.CombatProfile?.EffectDefinitions ?? Array.Empty<CombatEffectDefinition>();
        _test.Eq(judgmentEffects.Count, 1, "判决结算 SkillDef 只能包含一个 execute effect。");
        if (judgmentEffects.Count == 1)
        {
            _test.Eq(judgmentEffects[0].EffectType, new StringName("execute"), "判决结算唯一效果应为 execute。");
            _test.Eq(
                judgmentEffects[0].SoulFractureDurationTu,
                0,
                "判决结算应显式禁用灵魂裂隙。"
            );
        }

        BattleUnitState holder = fixture.BuildExecutionerUnit("projection");
        _test.Eq(holder.weapon_item_id, ItemId, "装备后应投影处刑者之斧 item_id。");
        _test.Eq(holder.weapon_profile_type_id, new StringName("greataxe"), "装备后应投影 greataxe。");
        _test.Eq(holder.weapon_attack_range, 1, "处刑者之斧基础射程应为 1。");
        _test.Eq(holder.weapon_two_handed_dice?.dice_count ?? 0, 1, "武器伤害应为 1D12+2。");
        _test.Eq(holder.weapon_two_handed_dice?.dice_sides ?? 0, 12, "武器伤害应为 1D12+2。");
        _test.Eq(holder.weapon_two_handed_dice?.flat_bonus ?? 0, 2, "武器伤害应为 1D12+2。");
        AssertUnitHasTraitAndAbilitySource(holder, ExecutionTraitId, ExecutionBindingId);
        AssertUnitHasTraitAndAbilitySource(holder, DeathSentenceTraitId, DeathSentenceBindingId);
        AssertUnitHasTraitAndAbilitySource(holder, SelfExecutionTraitId, SelfExecutionBindingId);

        BattleSkillAvailabilityView availability = BuildEquipmentSkillView(fixture, holder);
        _test.True(
            TryFindSkillEntry(availability, DeathSentenceSkillId, out BattleAvailableSkillEntry entry),
            "装备后应出现死亡判决装备技能。"
        );
        if (entry != null)
        {
            _test.Eq(entry.SkillLevel, 1, "死亡判决装备入口应固定为 1 级。");
            _test.Eq(entry.EquipmentBindingId, DeathSentenceBindingId, "死亡判决入口应保留 binding。");
            _test.Eq(entry.EquipmentGrantedActionId, DeathSentenceGrantId, "死亡判决入口应保留 grant id。");
        }
        foreach (
            StringName hiddenSkillId in new[]
            {
                JudgmentResolutionSkillId,
                JudgmentFallbackSkillId,
                SelfExecutionSkillId,
                ExecutionFearSkillId,
            }
        )
        {
            _test.False(
                TryFindSkillEntry(availability, hiddenSkillId, out _),
                $"内部技能 {hiddenSkillId} 不得出现在玩家可用技能列表。"
            );
            _test.False(
                ContainsStringName(holder.known_active_skill_ids, hiddenSkillId),
                $"内部技能 {hiddenSkillId} 不得写入已知技能。"
            );
        }

        ProgressionService progressionService = new();
        progressionService.SetupDefinitions(
            new UnitProgress(),
            fixture.SkillDefs,
            new Dictionary<StringName, ProfessionDefinition>()
        );
        foreach (
            StringName hiddenSkillId in new[]
            {
                JudgmentResolutionSkillId,
                JudgmentFallbackSkillId,
                SelfExecutionSkillId,
                ExecutionFearSkillId,
            }
        )
        {
            _test.Eq(
                fixture.SkillDefs[hiddenSkillId].LearnSource,
                new StringName("internal"),
                $"内部技能 {hiddenSkillId} 应声明 internal 学习源。"
            );
            _test.False(
                progressionService.CanLearnSkill(hiddenSkillId),
                $"内部技能 {hiddenSkillId} 不得通过成长服务学习。"
            );
            _test.False(
                progressionService.LearnSkill(hiddenSkillId),
                $"内部技能 {hiddenSkillId} 的学习入口必须拒绝写入。"
            );
        }
    }

    private void TestDeathSentenceCostsCooldownAndMissPreservesMark()
    {
        using ExecutionerFixture fixture = ExecutionerFixture.Build();
        fixture.UseFixedDamageAndHit(new FixedMissResolver());
        BattleUnitState holder = fixture.BuildExecutionerUnit("miss");
        BattleUnitState target = BuildEnemy("miss_target", new Vector2I(2, 1), 100, 100);
        BattleState state = BuildState("executioner_miss", holder, target);
        fixture.Runtime.SetupStateForTests(state);

        BattleAvailableSkillEntry entry = FindDeathSentenceEntry(fixture, holder, state);
        BattleEventBatch markBatch = IssueDeathSentence(fixture.Runtime, holder, target, entry);
        _test.Eq(holder.current_ap, 1, "死亡判决应支付 1AP。");
        _test.Eq(holder.current_stamina, 40, "死亡判决应从 100 体力中支付 60。");
        _test.Eq(holder.GetCooldownTyped(DeathSentenceSkillId), 300, "死亡判决应启动 300TU 冷却。");
        AssertDeathSentenceMark(state, holder, target, "施放后应建立 typed 判决标记。");
        _test.Eq(
            target.GetStatusEffect(DeathSentenceStatusId)?.duration ?? -1,
            60,
            "死亡判决镜像状态应持续 60TU。"
        );

        BattleEventBatch missBatch = IssueBasicAttackInCurrentState(
            fixture.Runtime,
            holder,
            target,
            "executioner_marked_miss"
        );
        _test.True(target.is_alive, "固定未命中不应伤害目标。");
        AssertDeathSentenceMark(state, holder, target, "未命中不得消费判决标记。");
        _test.True(target.HasStatusEffect(DeathSentenceStatusId), "未命中后 60TU 镜像状态应保留。");
        AssertNoInternalSkillIdentity(markBatch);
        AssertNoInternalSkillIdentity(missBatch);
    }

    private void TestMarkedHitForcesCriticalAndSuccessfulJudgmentTriggersResistedBacklash()
    {
        using ExecutionerFixture fixture = ExecutionerFixture.Build();
        fixture.UseFixedDamageAndHit(new FixedHitResolver(10));
        BattleUnitState holder = fixture.BuildExecutionerUnit("critical_success");
        SetSaveAbility(holder, "willpower", 100);
        BattleUnitState target = BuildEnemy("critical_success_target", new Vector2I(2, 1), 100, 100);
        SetSaveAbility(target, "constitution", 100);
        BattleState state = BuildState("executioner_critical_success", holder, target);
        fixture.Runtime.SetupStateForTests(state);

        IssueDeathSentence(fixture.Runtime, holder, target, FindDeathSentenceEntry(fixture, holder, state));
        int targetHpBefore = target.current_hp;
        int holderHpBefore = holder.current_hp;
        WeaponAbilityCommandTestSupport.PrimeBasicAttack(holder);
        ForceUnitActing(state, holder);
        BattlePreview markedPreview = fixture.Runtime.PreviewCommand(
            WeaponAbilityCommandTestSupport.BuildBasicAttackCommand(holder, target)
        );
        _test.True(
            markedPreview?.hit_preview?.ForceCriticalOnHit == true,
            "死亡判决目标的攻击预览应声明命中后必定暴击。"
        );
        BattleEventBatch batch = IssueBasicAttackInCurrentState(
            fixture.Runtime,
            holder,
            target,
            "executioner_forced_critical"
        );

        _test.Eq(targetHpBefore - target.current_hp, 4, "固定每骰 1 时，1D12+2 的暴击应造成 4 点伤害。");
        _test.False(target.HasStatusEffect("soul_fracture"), "通过死亡判决后不得附带灵魂裂隙。");
        _test.Eq(holder.current_hp, holderHpBefore, "持有者通过 DC14 自我处刑检定后不应受伤。");
        _test.Eq(state.EquipmentTargetMarkCount, 0, "成功命中完整结算后应消费 typed 判决标记。");
        _test.False(target.HasStatusEffect(DeathSentenceStatusId), "成功命中后应清除判决镜像状态。");
        _test.True(
            HasLogLineContaining(batch, "自我处刑意志检定"),
            "目标通过死亡判决后应默认进行自我处刑检定。"
        );
        AssertNoInternalSkillIdentity(batch);
    }

    private void TestCritLockSuppressesForcedCriticalPreviewAndResolution()
    {
        using ExecutionerFixture fixture = ExecutionerFixture.Build();
        fixture.UseFixedDamageAndHit(new FixedCritLockAwareHitResolver(10));
        BattleUnitState holder = fixture.BuildExecutionerUnit("crit_lock");
        holder.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = "executioner_test_crit_lock",
                source_unit_id = holder.unit_id,
                power = 1,
                stacks = 1,
                duration = -1,
                lock_crit = true,
            }
        );
        BattleUnitState target = BuildEnemy("crit_lock_target", new Vector2I(2, 1), 100, 100);
        SetSaveAbility(target, "constitution", -100);
        BattleState state = BuildState("executioner_crit_lock", holder, target);
        fixture.Runtime.SetupStateForTests(state);

        IssueDeathSentence(fixture.Runtime, holder, target, FindDeathSentenceEntry(fixture, holder, state));
        WeaponAbilityCommandTestSupport.PrimeBasicAttack(holder);
        ForceUnitActing(state, holder);
        BattleCommand command = WeaponAbilityCommandTestSupport.BuildBasicAttackCommand(holder, target);
        BattlePreview preview = fixture.Runtime.PreviewCommand(command);
        _test.True(preview?.allowed == true, "禁暴击状态下的普通攻击仍应允许执行。");
        _test.True(preview?.hit_preview?.CritLocked == true, "攻击预览应暴露当前禁暴击状态。");
        _test.False(
            preview?.hit_preview?.ForceCriticalOnHit == true,
            "禁暴击状态下不得预告命中后必定暴击。"
        );
        _test.False(
            preview?.hit_preview?.SummaryText?.Contains("命中后必定暴击", StringComparison.Ordinal)
                == true,
            "禁暴击状态下摘要不得承诺必定暴击。"
        );

        BattleEventBatch batch = fixture.Runtime.IssueCommand(command);
        _test.Eq(target.current_hp, 97, "禁暴击状态下固定每骰 1 的普通命中应造成 1D12+2 共 3 点伤害。");
        AssertDeathSentenceMark(state, holder, target, "未形成暴击时不得消费判决标记。");
        _test.False(HasLogLineContaining(batch, "处刑成功"), "禁暴击普通命中不得触发判决处刑。");
        AssertNoInternalSkillIdentity(batch);
    }

    private void TestCritLockedLethalHitDoesNotCountAsExecution()
    {
        using ExecutionerFixture fixture = ExecutionerFixture.Build();
        fixture.UseFixedDamageAndHit(new FixedCritLockAwareHitResolver(10));
        BattleUnitState holder = fixture.BuildExecutionerUnit("crit_lock_lethal");
        holder.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = "executioner_test_crit_lock_lethal",
                source_unit_id = holder.unit_id,
                power = 1,
                stacks = 1,
                duration = -1,
                lock_crit = true,
            }
        );
        BattleUnitState target = BuildEnemy(
            "crit_lock_lethal_target",
            new Vector2I(2, 1),
            3,
            100
        );
        BattleUnitState witness = BuildEnemy(
            "crit_lock_lethal_witness",
            new Vector2I(3, 1),
            40,
            40
        );
        SetSaveAbility(witness, "willpower", -100);
        BattleState state = BuildState(
            "executioner_crit_lock_lethal",
            holder,
            target,
            witness
        );
        fixture.Runtime.SetupStateForTests(state);

        IssueDeathSentence(fixture.Runtime, holder, target, FindDeathSentenceEntry(fixture, holder, state));
        BattleEventBatch batch = IssueBasicAttackInCurrentState(
            fixture.Runtime,
            holder,
            target,
            "executioner_crit_lock_lethal_attack"
        );

        _test.False(target.is_alive, "测试前提：禁暴击普通命中的 3 点伤害应恰好击倒目标。");
        _test.False(
            witness.HasStatusEffect(FrightenedStatusId),
            "禁暴击普通击杀不得冒充判决处刑并触发恐惧。"
        );
        _test.False(
            HasLogLineContaining(batch, "处刑成功"),
            "禁暴击普通击杀日志不得宣称处刑成功。"
        );
        _test.Eq(state.EquipmentTargetMarkCount, 0, "目标死亡后仍应清理判决标记。");
        _test.False(target.HasStatusEffect(DeathSentenceStatusId), "目标死亡后仍应清理判决镜像状态。");
        AssertNoInternalSkillIdentity(batch);
    }

    private void TestNonCriticalKillProvenanceDropsForcedCriticalSource()
    {
        using ExecutionerFixture fixture = ExecutionerFixture.Build();
        BattleUnitState holder = fixture.BuildExecutionerUnit("noncritical_provenance");
        StringName equipmentInstanceId = holder
            .GetEquipmentView()
            ?.GetEquippedInstanceId("main_hand") ?? new StringName("");
        AttackEffectResolutionResult result = new()
        {
            CriticalHit = false,
            AttackCheck = new AttackCheckInput(
                forceCriticalOnHit: true,
                forcedCriticalSourceEquipmentInstanceId: equipmentInstanceId,
                forcedCriticalSourceBindingId: ExecutionBindingId,
                forcedCriticalSourceActionId: "action.executioner.execution.force_critical"
            ),
            DamageEvents = new[]
            {
                new DamageEventResult
                {
                    AddWeaponDice = true,
                    WeaponDamageDice = new DamageDiceRollDetail
                    {
                        Count = 1,
                        Sides = 12,
                    },
                },
            },
        };

        BattleKillProvenance provenance = BattleKillProvenance.FromWeaponAttackResult(
            holder,
            result,
            "basic_attack"
        );

        _test.True(provenance.IsAttack, "非暴击武器击杀仍应保留普通攻击来源。");
        _test.Eq(
            provenance.SourceEquipmentInstanceId,
            equipmentInstanceId,
            "非暴击武器击杀仍应归属于实际主手装备。"
        );
        _test.Eq(
            provenance.SourceBindingId,
            new StringName(""),
            "最终未形成暴击时不得保留必暴击 binding 来源。"
        );
        _test.Eq(
            provenance.SourceActionId,
            new StringName("basic_attack"),
            "最终未形成暴击时应回退到实际攻击 action。"
        );
    }

    private void TestForcedCriticalProvenanceOverridesOuterEquipmentAttack()
    {
        using ExecutionerFixture fixture = ExecutionerFixture.Build();
        BattleUnitState holder = fixture.BuildExecutionerUnit("outer_attack_provenance");
        StringName executionerInstanceId = holder
            .GetEquipmentView()
            ?.GetEquippedInstanceId("main_hand") ?? new StringName("");
        BattleKillProvenance outerAttack = BattleKillProvenance.ForEquipmentAttack(
            executionerInstanceId,
            "binding.weapon.test.outer_immediate_attack",
            "action.weapon.test.outer_immediate_attack"
        );
        AttackEffectResolutionResult forcedCriticalResult = new()
        {
            CriticalHit = true,
            AttackCheck = new AttackCheckInput(
                forceCriticalOnHit: true,
                forcedCriticalSourceEquipmentInstanceId: executionerInstanceId,
                forcedCriticalSourceBindingId: ExecutionBindingId,
                forcedCriticalSourceActionId: "action.executioner.execution.force_critical"
            ),
            DamageEvents = BuildWeaponDamageEvents(),
        };

        BattleKillProvenance judgmentKill = BattleKillProvenance.FromWeaponAttackResult(
            holder,
            forcedCriticalResult,
            outerAttack
        );
        _test.Eq(
            judgmentKill.SourceBindingId,
            ExecutionBindingId,
            "最终形成判决强制暴击时应覆盖外层即时攻击 binding。"
        );
        _test.Eq(
            judgmentKill.SourceActionId,
            new StringName("action.executioner.execution.force_critical"),
            "最终形成判决强制暴击时应覆盖外层即时攻击 action。"
        );

        forcedCriticalResult.CriticalHit = false;
        BattleKillProvenance ordinaryOuterKill = BattleKillProvenance.FromWeaponAttackResult(
            holder,
            forcedCriticalResult,
            outerAttack
        );
        _test.Eq(
            ordinaryOuterKill.SourceBindingId,
            outerAttack.SourceBindingId,
            "最终未形成强制暴击时应保留外层即时攻击 binding。"
        );
        _test.Eq(
            ordinaryOuterKill.SourceActionId,
            outerAttack.SourceActionId,
            "最终未形成强制暴击时应保留外层即时攻击 action。"
        );
    }

    private void TestOrdinaryJudgmentExecutionFrightensOnlyNearbyEnemies()
    {
        using ExecutionerFixture fixture = ExecutionerFixture.Build();
        fixture.UseFixedDamageAndHit(new FixedHitResolver(10));
        BattleUnitState holder = fixture.BuildExecutionerUnit("ordinary_execute");
        BattleUnitState target = BuildEnemy("ordinary_execute_target", new Vector2I(2, 1), 100, 100);
        SetSaveAbility(target, "constitution", -100);
        BattleUnitState nearbyEnemy = BuildEnemy("nearby_enemy", new Vector2I(5, 1), 40, 40);
        BattleUnitState outsideEnemy = BuildEnemy("outside_enemy", new Vector2I(6, 1), 40, 40);
        BattleUnitState immuneEnemy = BuildEnemy("immune_enemy", new Vector2I(3, 2), 40, 40);
        BattleUnitState nearbyAlly = BuildAlly("nearby_ally", new Vector2I(2, 3), 40, 40);
        SetSaveAbility(nearbyEnemy, "willpower", -100);
        SetSaveAbility(outsideEnemy, "willpower", -100);
        SetSaveAbility(immuneEnemy, "willpower", -100);
        SetSaveAbility(nearbyAlly, "willpower", -100);
        immuneEnemy.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = "executioner_test_fear_immunity",
                source_unit_id = immuneEnemy.unit_id,
                power = 1,
                stacks = 1,
                duration = -1,
                save_immunity_tags = new List<StringName> { "frightened" },
            }
        );

        BattleState state = BuildState(
            "executioner_ordinary_execute",
            holder,
            target,
            nearbyEnemy,
            outsideEnemy,
            immuneEnemy,
            nearbyAlly
        );
        fixture.Runtime.SetupStateForTests(state);
        IssueDeathSentence(fixture.Runtime, holder, target, FindDeathSentenceEntry(fixture, holder, state));
        BattleEventBatch batch = IssueBasicAttackInCurrentState(
            fixture.Runtime,
            holder,
            target,
            "executioner_ordinary_execute_attack"
        );

        _test.False(target.is_alive, "普通目标未通过 DC16 体质检定后应被处刑。");
        _test.Eq(holder.current_hp, 100, "判决击杀成功时不得触发自我处刑。");
        _test.Eq(
            nearbyEnemy.GetStatusEffect(FrightenedStatusId)?.duration ?? -1,
            60,
            "中心 3 格内敌人失败后应获得 60TU frightened。"
        );
        _test.False(outsideEnemy.HasStatusEffect(FrightenedStatusId), "中心距离 4 的敌人不应进行恐惧结算。");
        _test.False(immuneEnemy.HasStatusEffect(FrightenedStatusId), "恐惧免疫敌人不应获得 frightened。");
        _test.False(nearbyAlly.HasStatusEffect(FrightenedStatusId), "处刑恐惧不得影响友方单位。");
        _test.True(HasLogLineContaining(batch, "处刑成功"), "玩家日志应显示处刑成功。");
        _test.True(HasLogLineContaining(batch, "恐惧检定"), "玩家日志应显示恐惧检定结果。");
        AssertNoInternalSkillIdentity(batch);
    }

    private void TestEliteAndBossPostHitThresholdBranches()
    {
        RunRankBranchCase(
            "elite_above",
            fortuneMarkTarget: 1,
            bossTarget: 0,
            currentHp: 80,
            expectExecution: false
        );
        RunRankBranchCase(
            "elite_below_after_hit",
            fortuneMarkTarget: 1,
            bossTarget: 0,
            currentHp: 50,
            expectExecution: true
        );
        RunRankBranchCase(
            "boss_above",
            fortuneMarkTarget: 2,
            bossTarget: 0,
            currentHp: 80,
            expectExecution: false
        );
        RunRankBranchCase(
            "boss_boundary_after_hit",
            fortuneMarkTarget: 2,
            bossTarget: 0,
            currentHp: 29,
            expectExecution: true
        );
    }

    private void RunRankBranchCase(
        string label,
        int fortuneMarkTarget,
        int bossTarget,
        int currentHp,
        bool expectExecution
    )
    {
        using ExecutionerFixture fixture = ExecutionerFixture.Build();
        fixture.UseFixedDamageAndHit(new FixedHitResolver(10));
        BattleUnitState holder = fixture.BuildExecutionerUnit(label);
        SetSaveAbility(holder, "willpower", -100);
        BattleUnitState target = BuildEnemy($"{label}_target", new Vector2I(2, 1), currentHp, 100);
        target.attribute_snapshot.SetValue("fortune_mark_target", fortuneMarkTarget);
        target.attribute_snapshot.SetValue("boss_target", bossTarget);
        SetSaveAbility(target, "constitution", -100);
        BattleState state = BuildState($"executioner_{label}", holder, target);
        fixture.Runtime.SetupStateForTests(state);

        IssueDeathSentence(fixture.Runtime, holder, target, FindDeathSentenceEntry(fixture, holder, state));
        int holderHpBefore = holder.current_hp;
        BattleEventBatch batch = IssueBasicAttackInCurrentState(
            fixture.Runtime,
            holder,
            target,
            $"executioner_{label}_attack"
        );

        _test.Eq(target.is_alive, !expectExecution, $"{label} 的处决分支不符。");
        if (expectExecution)
        {
            _test.Eq(holder.current_hp, holderHpBefore, $"{label} 处决成功后不应自我处刑。");
        }
        else
        {
            _test.Eq(target.current_hp, currentHp - 7, $"{label} 应受到 4 点暴击武器伤害和 3 点 3D12 fallback。");
            _test.Eq(holder.current_hp, holderHpBefore - 4, $"{label} 存活后持有者应承受 2D12+2 自我处刑。");
        }
        _test.Eq(state.EquipmentTargetMarkCount, 0, $"{label} 完整命中结算后应消费判决标记。");
        AssertNoInternalSkillIdentity(batch);
    }

    private void TestDeathPreventionSurvivalTriggersSelfExecution()
    {
        using ExecutionerFixture fixture = ExecutionerFixture.Build();
        fixture.UseFixedDamageAndHit(new FixedHitResolver(10));
        BattleUnitState holder = fixture.BuildExecutionerUnit("death_prevention");
        SetSaveAbility(holder, "willpower", -100);
        BattleUnitState target = BuildEnemy("death_prevention_target", new Vector2I(2, 1), 100, 100);
        SetSaveAbility(target, "constitution", -100);
        target.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = "death_ward",
                source_unit_id = target.unit_id,
                power = 1,
                stacks = 1,
                duration = -1,
                death_prevention_priority = 1000,
                source_skill_id = "warrior_last_stand",
                source_skill_level = 7,
            }
        );
        BattleUnitState witness = BuildEnemy("death_prevention_witness", new Vector2I(3, 1), 40, 40);
        SetSaveAbility(witness, "willpower", -100);
        BattleState state = BuildState("executioner_death_prevention", holder, target, witness);
        fixture.Runtime.SetupStateForTests(state);

        IssueDeathSentence(fixture.Runtime, holder, target, FindDeathSentenceEntry(fixture, holder, state));
        BattleEventBatch batch = IssueBasicAttackInCurrentState(
            fixture.Runtime,
            holder,
            target,
            "executioner_death_prevention_attack"
        );

        _test.True(target.is_alive, "高优先级死亡保护应阻止判决处决。");
        _test.False(target.HasStatusEffect("death_ward"), "已触发的死亡保护应被消耗。");
        _test.False(target.HasStatusEffect("soul_fracture"), "死亡保护救下目标后不得附带灵魂裂隙。");
        _test.Eq(holder.current_hp, 96, "判决被死亡保护阻止后应触发 4 点固定自我处刑伤害。");
        _test.False(witness.HasStatusEffect(FrightenedStatusId), "被死亡保护阻止的处刑不得触发恐惧。");
        AssertNoInternalSkillIdentity(batch);
    }

    private void TestUnusedMarkExpiryTriggersSelfExecutionOnce()
    {
        using ExecutionerFixture fixture = ExecutionerFixture.Build();
        fixture.UseFixedDamageAndHit(new FixedHitResolver(10));
        BattleUnitState holder = fixture.BuildExecutionerUnit("expiry");
        SetSaveAbility(holder, "willpower", -100);
        BattleUnitState target = BuildEnemy("expiry_target", new Vector2I(2, 1), 100, 100);
        BattleState state = BuildState("executioner_expiry", holder, target);
        fixture.Runtime.SetupStateForTests(state);
        IssueDeathSentence(fixture.Runtime, holder, target, FindDeathSentenceEntry(fixture, holder, state));
        AssertDeathSentenceMark(state, holder, target, "到期测试前应存在判决标记。");

        using BattleEventBatch firstBatch = new();
        bool firstChanged = fixture.Runtime._advance_unit_status_durations(target, 60, firstBatch);
        _test.True(firstChanged, "推进 60TU 应使判决状态到期。");
        _test.Eq(holder.current_hp, 96, "未使用的判决到期应触发一次 2D12+2 自我处刑。");
        _test.Eq(state.EquipmentTargetMarkCount, 0, "判决到期后应清除 typed target mark。");
        _test.False(target.HasStatusEffect(DeathSentenceStatusId), "判决到期后应清除镜像状态。");
        _test.True(HasLogLineContaining(firstBatch, "自我处刑意志检定"), "到期日志应显示自我处刑检定。");

        using BattleEventBatch secondBatch = new();
        fixture.Runtime._advance_unit_status_durations(target, 60, secondBatch);
        _test.Eq(holder.current_hp, 96, "已清理的判决继续推进时间不得重复自我处刑。");
        AssertNoInternalSkillIdentity(firstBatch);
        AssertNoInternalSkillIdentity(secondBatch);
    }

    private void TestUnequippingExecutionerClearsMarkWithoutBacklash()
    {
        using ExecutionerFixture fixture = ExecutionerFixture.Build();
        fixture.UseFixedDamageAndHit(new FixedHitResolver(10));
        BattleUnitState holder = fixture.BuildExecutionerUnit("unequip_mark_cleanup");
        SetSaveAbility(holder, "willpower", -100);
        BattleUnitState target = BuildEnemy(
            "unequip_mark_cleanup_target",
            new Vector2I(2, 1),
            100,
            100
        );
        BattleState state = BuildState("executioner_unequip_mark_cleanup", holder, target);
        fixture.Runtime.SetupStateForTests(state);

        IssueDeathSentence(fixture.Runtime, holder, target, FindDeathSentenceEntry(fixture, holder, state));
        AssertDeathSentenceMark(state, holder, target, "卸装前应存在判决标记。");
        StringName equipmentInstanceId = holder
            .GetEquipmentView()
            ?.GetEquippedInstanceId("main_hand") ?? new StringName("");
        int holderHpBefore = holder.current_hp;
        holder.SetCurrentAp(2);
        ForceUnitActing(state, holder);
        BattleCommand command = BuildUnequipCommand(holder.unit_id, "main_hand", equipmentInstanceId);
        BattlePreview preview = fixture.Runtime.PreviewCommand(command);
        if (preview?.allowed != true)
        {
            throw new InvalidOperationException(
                $"executioner unequip preview blocked: {string.Join(" | ", preview?.LogLinesTyped ?? Array.Empty<string>())}"
            );
        }
        BattleEventBatch unequipBatch = fixture.Runtime.IssueCommand(command);

        _test.Eq(
            holder.GetEquipmentView()?.GetEquippedInstanceId("main_hand") ?? new StringName(""),
            new StringName(""),
            "正式卸装命令应移除处刑者之斧。"
        );
        _test.False(
            UnitHasAbilitySource(holder, DeathSentenceBindingId),
            "卸装后持有者不应继续投影死亡判决 binding。"
        );
        _test.Eq(state.EquipmentTargetMarkCount, 0, "装备来源消失时应立即清理 typed 判决标记。");
        _test.False(target.HasStatusEffect(DeathSentenceStatusId), "装备来源消失时应立即清理判决镜像状态。");
        _test.Eq(holder.current_hp, holderHpBefore, "来源消失清理不得触发自我处刑伤害。");
        _test.True(
            unequipBatch.ContainsChangedUnitId(target.unit_id),
            "清理目标镜像状态时事件批次应包含目标单位。"
        );

        using BattleEventBatch afterExpiryWindow = new();
        fixture.Runtime._advance_unit_status_durations(target, 60, afterExpiryWindow);
        _test.Eq(holder.current_hp, holderHpBefore, "卸装清理后再推进 60TU 也不得延迟触发反噬。");
        _test.Eq(state.EquipmentTargetMarkCount, 0, "卸装清理后不得残留延迟到期的 typed mark。");
        AssertNoInternalSkillIdentity(unequipBatch);
        AssertNoInternalSkillIdentity(afterExpiryWindow);
    }

    private void TestDurabilityDestructionClearsMarkWithoutBacklash()
    {
        using ExecutionerFixture fixture = ExecutionerFixture.Build();
        BattleUnitState holder = fixture.BuildExecutionerUnit("durability_mark_cleanup");
        SetSaveAbility(holder, "willpower", -100);
        BattleUnitState markedTarget = BuildEnemy(
            "durability_mark_cleanup_target",
            new Vector2I(2, 1),
            100,
            100
        );
        BattleUnitState disjunctionCaster = BuildEnemy(
            "durability_mark_cleanup_caster",
            new Vector2I(3, 1),
            100,
            100
        );
        disjunctionCaster.attribute_snapshot.SetValue("intelligence", 100);
        disjunctionCaster.attribute_snapshot.SetValue(
            AttributeService.SPELL_PROFICIENCY_BONUS,
            20
        );
        BattleState state = BuildState(
            "executioner_durability_mark_cleanup",
            holder,
            markedTarget,
            disjunctionCaster
        );
        fixture.Runtime.SetupStateForTests(state);
        IssueDeathSentence(
            fixture.Runtime,
            holder,
            markedTarget,
            FindDeathSentenceEntry(fixture, holder, state)
        );
        AssertDeathSentenceMark(state, holder, markedTarget, "耐久摧毁前应存在判决标记。");
        EquipmentInstanceState executionerInstance = holder
            .GetEquipmentView()
            ?.GetEquippedInstance("main_hand");
        if (executionerInstance == null)
            throw new InvalidOperationException("executioner durability fixture missing main hand instance");
        executionerInstance.current_durability = 1;
        int holderHpBefore = holder.current_hp;

        using BattleEventBatch batch = new();
        fixture.Runtime.GetDamageResolver().ResolveEffects(
            disjunctionCaster,
            holder,
            new[]
            {
                TestSkillDefinitionProjection.BuildEffect(
                    "damage",
                    power: 1,
                    damageTag: "magic"
                ),
                BuildEquipmentDurabilityEffect(100),
            },
            DamageResolutionContext
                .FromDictionary(
                    new GDictionary
                    {
                        ["save_roll_override"] = 1,
                        ["equipment_slot_override"] = "main_hand",
                    }
                )
                .WithDamageApplicationHookContext(batch, BattleEffectOrigin.PlayerCommand())
        );

        _test.Eq(
            holder.GetEquipmentView()?.GetEquippedInstanceId("main_hand") ?? new StringName(""),
            new StringName(""),
            "耐久归零应从 battle-local 装备视图移除处刑者之斧。"
        );
        _test.False(
            UnitHasAbilitySource(holder, DeathSentenceBindingId),
            "耐久摧毁后不应继续投影死亡判决 binding。"
        );
        _test.Eq(state.EquipmentTargetMarkCount, 0, "耐久摧毁应立即清理 typed 判决标记。");
        _test.False(
            markedTarget.HasStatusEffect(DeathSentenceStatusId),
            "耐久摧毁应立即清理判决镜像状态。"
        );
        _test.Eq(holder.current_hp, holderHpBefore - 1, "来源清理不得叠加自我处刑伤害。");
        _test.True(
            batch.ContainsChangedUnitId(markedTarget.unit_id),
            "耐久摧毁清理镜像时事件批次应包含被标记目标。"
        );
    }

    private void TestConcurrentSourceMarksExpireIndependently()
    {
        using ExecutionerFixture fixture = ExecutionerFixture.Build();
        fixture.UseFixedDamageAndHit(new FixedHitResolver(10));
        BattleUnitState firstHolder = fixture.BuildExecutionerUnit("concurrent_first");
        SetSaveAbility(firstHolder, "willpower", -100);
        BattleUnitState secondHolder = fixture.BuildExecutionerUnit("concurrent_second");
        secondHolder.unit_id = "hero_second";
        secondHolder.display_name = "hero_second";
        secondHolder.SetAnchorCoord(new Vector2I(2, 2));
        SetSaveAbility(secondHolder, "willpower", -100);
        BattleUnitState target = BuildEnemy("concurrent_target", new Vector2I(2, 1), 100, 100);
        BattleState state = BuildState(
            "executioner_concurrent_marks",
            firstHolder,
            target,
            secondHolder
        );
        fixture.Runtime.SetupStateForTests(state);

        IssueDeathSentence(
            fixture.Runtime,
            firstHolder,
            target,
            FindDeathSentenceEntry(fixture, firstHolder, state)
        );
        using BattleEventBatch firstHalf = new();
        fixture.Runtime._advance_unit_status_durations(target, 30, firstHalf);
        _test.Eq(firstHolder.current_hp, 100, "首个判决推进 30TU 时不应提前反噬。");

        IssueDeathSentence(
            fixture.Runtime,
            secondHolder,
            target,
            FindDeathSentenceEntry(fixture, secondHolder, state)
        );
        _test.Eq(state.EquipmentTargetMarkCount, 2, "两个装备来源应能同时标记同一目标。");
        MoveDeathSentenceMarkToEnd(state, firstHolder);

        using BattleEventBatch firstExpiry = new();
        fixture.Runtime._advance_unit_status_durations(target, 30, firstExpiry);
        _test.Eq(firstHolder.current_hp, 96, "首个来源累计 60TU 后应独立触发自我处刑。");
        _test.Eq(secondHolder.current_hp, 100, "后施放来源只经过 30TU，不应提前反噬。");
        _test.Eq(state.EquipmentTargetMarkCount, 1, "首个来源到期后应仅保留第二个判决标记。");
        AssertDeathSentenceMark(state, secondHolder, target, "第二个来源的判决标记应继续存在。");

        using BattleEventBatch secondExpiry = new();
        fixture.Runtime._advance_unit_status_durations(target, 30, secondExpiry);
        _test.Eq(secondHolder.current_hp, 96, "第二个来源累计 60TU 后应触发自己的自我处刑。");
        _test.Eq(state.EquipmentTargetMarkCount, 0, "两个来源分别到期后不应残留 typed mark。");
        AssertNoInternalSkillIdentity(firstHalf);
        AssertNoInternalSkillIdentity(firstExpiry);
        AssertNoInternalSkillIdentity(secondExpiry);
    }

    private void TestConsumingLatestSourceRestoresRemainingMirror()
    {
        using ExecutionerFixture fixture = ExecutionerFixture.Build();
        fixture.UseFixedDamageAndHit(new FixedHitResolver(10));
        BattleUnitState firstHolder = fixture.BuildExecutionerUnit("mirror_first");
        SetSaveAbility(firstHolder, "willpower", -100);
        BattleUnitState secondHolder = fixture.BuildExecutionerUnit("mirror_second");
        secondHolder.unit_id = "hero_mirror_second";
        secondHolder.display_name = "hero_mirror_second";
        secondHolder.SetAnchorCoord(new Vector2I(2, 2));
        SetSaveAbility(secondHolder, "willpower", 100);
        BattleUnitState target = BuildEnemy("mirror_target", new Vector2I(2, 1), 100, 100);
        SetSaveAbility(target, "constitution", 100);
        BattleState state = BuildState(
            "executioner_mirror_restore",
            firstHolder,
            target,
            secondHolder
        );
        fixture.Runtime.SetupStateForTests(state);

        IssueDeathSentence(
            fixture.Runtime,
            firstHolder,
            target,
            FindDeathSentenceEntry(fixture, firstHolder, state)
        );
        using BattleEventBatch firstHalf = new();
        fixture.Runtime._advance_unit_status_durations(target, 30, firstHalf);
        IssueDeathSentence(
            fixture.Runtime,
            secondHolder,
            target,
            FindDeathSentenceEntry(fixture, secondHolder, state)
        );

        BattleEventBatch consumeBatch = IssueBasicAttackInCurrentState(
            fixture.Runtime,
            secondHolder,
            target,
            "executioner_mirror_second_consumes"
        );
        _test.Eq(state.EquipmentTargetMarkCount, 1, "后施放来源消费后应保留首个来源的判决。");
        AssertDeathSentenceMark(state, firstHolder, target, "首个来源的 typed 判决应继续存在。");
        BattleStatusEffectState restoredMirror = target.GetStatusEffect(DeathSentenceStatusId);
        _test.True(restoredMirror != null, "后施放来源清理后应恢复剩余判决的镜像状态。");
        _test.Eq(
            ProgressionDataUtils.to_string_name(restoredMirror?.source_unit_id),
            firstHolder.unit_id,
            "恢复后的镜像状态应归属于仍有效的首个来源。"
        );
        _test.Eq(restoredMirror?.duration ?? -1, 30, "恢复后的镜像状态应显示首个来源剩余 30TU。");

        using BattleEventBatch firstExpiry = new();
        fixture.Runtime._advance_unit_status_durations(target, 30, firstExpiry);
        _test.Eq(firstHolder.current_hp, 96, "首个来源剩余 30TU 到期后应正常自我处刑。");
        _test.Eq(state.EquipmentTargetMarkCount, 0, "恢复显示的最后一份判决到期后应完整清理。");
        _test.False(target.HasStatusEffect(DeathSentenceStatusId), "最后一份判决到期后镜像应消失。");
        AssertNoInternalSkillIdentity(consumeBatch);
        AssertNoInternalSkillIdentity(firstExpiry);
    }

    private void TestSelfExecutionCanDefeatWielder()
    {
        using ExecutionerFixture fixture = ExecutionerFixture.Build();
        fixture.UseFixedDamageAndHit(new FixedHitResolver(10));
        BattleUnitState holder = fixture.BuildExecutionerUnit("self_execution_fatal");
        SetSaveAbility(holder, "willpower", -100);
        BattleUnitState target = BuildEnemy(
            "self_execution_fatal_target",
            new Vector2I(2, 1),
            100,
            100
        );
        SetSaveAbility(target, "constitution", 100);
        BattleState state = BuildState("executioner_self_execution_fatal", holder, target);
        fixture.Runtime.SetupStateForTests(state);

        IssueDeathSentence(fixture.Runtime, holder, target, FindDeathSentenceEntry(fixture, holder, state));
        WeaponAbilityCommandTestSupport.PrimeBasicAttack(holder);
        holder.SetCurrentHp(4);
        ForceUnitActing(state, holder);
        BattleCommand command = WeaponAbilityCommandTestSupport.BuildBasicAttackCommand(
            holder,
            target
        );
        BattlePreview preview = fixture.Runtime.PreviewCommand(command);
        if (preview?.allowed != true)
            throw new InvalidOperationException("self execution fatal preview should be allowed");
        BattleEventBatch batch = fixture.Runtime.IssueCommand(command);

        _test.False(holder.is_alive, "自我处刑伤害应能将持有者降至 0 并完成击倒。");
        _test.Eq(holder.current_hp, 0, "致死自我处刑后持有者生命应为 0。");
        _test.True(target.is_alive, "通过死亡判决的目标应继续存活。");
        _test.Eq(state.EquipmentTargetMarkCount, 0, "致死自我处刑后判决标记仍应清理。");
        AssertNoInternalSkillIdentity(batch);
    }

    private void TestUnmarkedKillDoesNotTriggerExecutionFear()
    {
        using ExecutionerFixture fixture = ExecutionerFixture.Build();
        fixture.UseFixedDamageAndHit(new FixedHitResolver(10));
        BattleUnitState holder = fixture.BuildExecutionerUnit("unmarked_kill");
        BattleUnitState target = BuildEnemy("unmarked_target", new Vector2I(2, 1), 1, 10);
        BattleUnitState witness = BuildEnemy("unmarked_witness", new Vector2I(3, 1), 40, 40);
        SetSaveAbility(witness, "willpower", -100);
        BattleState state = BuildState("executioner_unmarked_kill", holder, target, witness);
        fixture.Runtime.SetupStateForTests(state);

        BattleEventBatch batch = IssueBasicAttackInCurrentState(
            fixture.Runtime,
            holder,
            target,
            "executioner_unmarked_attack"
        );

        _test.False(target.is_alive, "测试前提：1HP 未标记目标应被普通攻击击倒。");
        _test.False(witness.HasStatusEffect(FrightenedStatusId), "未标记击杀不得触发处刑恐惧。");
        _test.False(HasLogLineContaining(batch, "处刑成功"), "未标记击杀日志不得宣称处刑成功。");
        AssertNoInternalSkillIdentity(batch);
    }

    private void AssertDeathSentenceMark(
        BattleState state,
        BattleUnitState holder,
        BattleUnitState target,
        string message
    )
    {
        StringName equipmentInstanceId = holder
            .GetEquipmentView()
            ?.GetEquippedInstanceId("main_hand") ?? new StringName("");
        bool found = state.TryGetEquipmentTargetMark(
            holder.unit_id,
            equipmentInstanceId,
            DeathSentenceBindingId,
            JudgmentMarkStateKey,
            out BattleEquipmentTargetMarkState mark
        );
        _test.True(found, message);
        if (found)
        {
            _test.Eq(mark.TargetUnitId, target.unit_id, $"{message} 目标不符。");
        }
    }

    private static void MoveDeathSentenceMarkToEnd(
        BattleState state,
        BattleUnitState holder
    )
    {
        StringName equipmentInstanceId = holder
            .GetEquipmentView()
            ?.GetEquippedInstanceId("main_hand") ?? new StringName("");
        if (
            state.TryGetEquipmentTargetMark(
                holder.unit_id,
                equipmentInstanceId,
                DeathSentenceBindingId,
                JudgmentMarkStateKey,
                out BattleEquipmentTargetMarkState mark
            )
        )
        {
            state.SetEquipmentTargetMark(mark, uniquePerSource: true, out _);
        }
    }

    private void AssertUnitHasTraitAndAbilitySource(
        BattleUnitState unit,
        StringName traitId,
        StringName bindingId
    )
    {
        _test.True(unit.effective_trait_ids.Contains(traitId), $"unit 应投影 trait {traitId}。");
        foreach (BattleEquipmentAbilitySourceState source in unit.equipment_ability_sources)
        {
            if (source?.AbilityIds?.Contains(bindingId) == true)
            {
                return;
            }
        }
        _test.Fail($"unit 应投影 equipment binding {bindingId}。");
    }

    private static bool UnitHasAbilitySource(BattleUnitState unit, StringName bindingId)
    {
        foreach (
            BattleEquipmentAbilitySourceState source in unit?.equipment_ability_sources
                ?? new List<BattleEquipmentAbilitySourceState>()
        )
        {
            if (source?.AbilityIds?.Contains(bindingId) == true)
                return true;
        }
        return false;
    }

    private static bool HasStatusEffectDefinition(
        SkillDefinition skill,
        StringName statusId,
        int durationTu,
        int saveDc
    )
    {
        foreach (
            CombatEffectDefinition effect in skill?.CombatProfile?.EffectDefinitions
                ?? Array.Empty<CombatEffectDefinition>()
        )
        {
            if (
                effect?.EffectType == "status"
                && effect.StatusId == statusId
                && effect.DurationTu == durationTu
                && effect.SaveDc == saveDc
            )
            {
                return true;
            }
        }
        return false;
    }

    private static CombatEffectDefinition FindFirstEffectDefinition(
        SkillDefinition skill,
        StringName effectType
    )
    {
        foreach (
            CombatEffectDefinition effect in skill?.CombatProfile?.EffectDefinitions
                ?? Array.Empty<CombatEffectDefinition>()
        )
        {
            if (effect?.EffectType == effectType)
                return effect;
        }
        return null;
    }

    private static CombatEffectDefinition BuildEquipmentDurabilityEffect(int power) =>
        CombatEffectDefinition.FromResource(
            new CombatEffectDef
            {
                effect_type = "equipment_durability_damage",
                power = Math.Max(power, 1),
                effect_target_team_filter = "enemy",
                save_dc_mode = "caster_spell",
                save_ability = "willpower",
                save_dc_source_ability = "intelligence",
                save_tag = "equipment_disjunction",
                require_damage_applied = true,
                equipment_durability_slot_weights =
                    new Godot.Collections.Array<CombatEffectSlotWeightDef>
                    {
                        new() { slot_id = "main_hand", weight = 1 },
                    },
                @params = new GDictionary
                {
                    ["max_damaged_items"] = 1,
                    ["target_slots"] = new GStringNameArray { "main_hand" },
                },
            },
            "test.executioner_axe.equipment_durability_effect"
        );

    private static DamageEventResult[] BuildWeaponDamageEvents() =>
        new[]
        {
            new DamageEventResult
            {
                AddWeaponDice = true,
                WeaponDamageDice = new DamageDiceRollDetail
                {
                    Count = 1,
                    Sides = 12,
                },
            },
        };

    private static BattleSkillAvailabilityView BuildEquipmentSkillView(
        ExecutionerFixture fixture,
        BattleUnitState holder,
        BattleState state = null
    )
    {
        BattleSkillAvailabilityService service = new(fixture.SkillDefs, fixture.Bindings);
        return service.BuildView(
            new BattleSkillAvailabilityQuery
            {
                User = holder,
                BattleState = state,
                Consumer = BattleSkillAvailabilityConsumer.ManualSelection,
                IncludeKnownSkills = true,
                IncludeEquipmentSkills = true,
                WorldStep = 0,
            }
        );
    }

    private static BattleAvailableSkillEntry FindDeathSentenceEntry(
        ExecutionerFixture fixture,
        BattleUnitState holder,
        BattleState state = null
    )
    {
        BattleSkillAvailabilityView view = BuildEquipmentSkillView(fixture, holder, state);
        if (TryFindSkillEntry(view, DeathSentenceSkillId, out BattleAvailableSkillEntry entry))
        {
            return entry;
        }
        throw new InvalidOperationException("death sentence equipment skill entry missing");
    }

    private static bool TryFindSkillEntry(
        BattleSkillAvailabilityView view,
        StringName skillId,
        out BattleAvailableSkillEntry entry
    )
    {
        foreach (BattleAvailableSkillEntry candidate in view?.SkillEntries ?? Array.Empty<BattleAvailableSkillEntry>())
        {
            if (candidate != null && candidate.EntryRef.SkillId == skillId)
            {
                entry = candidate;
                return true;
            }
        }
        entry = null;
        return false;
    }

    private static BattleEventBatch IssueDeathSentence(
        BattleRuntimeModule runtime,
        BattleUnitState holder,
        BattleUnitState target,
        BattleAvailableSkillEntry entry
    )
    {
        ForceUnitActing(runtime?.GetState(), holder);
        BattleCommand command = WeaponAbilityCommandTestSupport.BuildUnitSkillCommand(
            holder,
            target,
            entry,
            DeathSentenceSkillId
        );
        BattlePreview preview = runtime.PreviewCommand(command);
        if (preview?.allowed != true)
        {
            throw new InvalidOperationException(
                $"death sentence preview blocked: {string.Join(" | ", preview?.LogLinesTyped ?? Array.Empty<string>())}"
            );
        }
        return runtime.IssueCommand(command);
    }

    private static BattleEventBatch IssueBasicAttackInCurrentState(
        BattleRuntimeModule runtime,
        BattleUnitState holder,
        BattleUnitState target,
        StringName label
    )
    {
        WeaponAbilityCommandTestSupport.PrimeBasicAttack(holder);
        ForceUnitActing(runtime?.GetState(), holder);
        BattleCommand command = WeaponAbilityCommandTestSupport.BuildBasicAttackCommand(holder, target);
        BattlePreview preview = runtime.PreviewCommand(command);
        if (preview?.allowed != true)
        {
            throw new InvalidOperationException(
                $"{label} preview blocked: {string.Join(" | ", preview?.LogLinesTyped ?? Array.Empty<string>())}"
            );
        }
        return runtime.IssueCommand(command);
    }

    private static BattleCommand BuildUnequipCommand(
        StringName unitId,
        StringName slotId,
        StringName equipmentInstanceId
    )
    {
        return new BattleCommand
        {
            command_type = BattleTypedNames.ToStringName(BattleCommandKind.ChangeEquipment),
            unit_id = unitId,
            target_unit_id = unitId,
            equipment_operation = BattleTypedNames.ToStringName(
                BattleEquipmentOperationKind.Unequip
            ),
            equipment_slot_id = slotId,
            equipment_instance_id = equipmentInstanceId,
        };
    }

    private static void ForceUnitActing(BattleState state, BattleUnitState unit)
    {
        if (state == null || unit == null)
            return;
        state.PhaseKind = BattlePhaseKind.UnitActing;
        state.active_unit_id = unit.unit_id;
    }

    private static BattleState BuildState(
        StringName battleId,
        BattleUnitState holder,
        BattleUnitState primaryTarget,
        params BattleUnitState[] additionalUnits
    )
    {
        BattleState state = WeaponAbilityCommandTestSupport.BuildFlatState(
            battleId,
            holder,
            primaryTarget,
            mapSize: new Vector2I(10, 8)
        );
        foreach (BattleUnitState unit in additionalUnits ?? Array.Empty<BattleUnitState>())
        {
            AddUnitToState(state, unit);
        }
        return state;
    }

    private static void AddUnitToState(BattleState state, BattleUnitState unit)
    {
        if (state == null || unit == null)
            return;
        state.SetUnit(unit);
        unit.RefreshFootprint();
        foreach (Vector2I coord in unit.occupied_coords)
        {
            state.GetCell(coord)?.SetOccupant(unit.unit_id);
        }
        if (unit.faction_id == "player")
        {
            if (!state.ally_unit_ids.Contains(unit.unit_id))
                state.ally_unit_ids.Add(unit.unit_id);
        }
        else if (!state.enemy_unit_ids.Contains(unit.unit_id))
        {
            state.enemy_unit_ids.Add(unit.unit_id);
        }
    }

    private static BattleUnitState BuildEnemy(
        StringName unitId,
        Vector2I coord,
        int currentHp,
        int maxHp
    ) => BuildUnit(unitId, "enemy", coord, currentHp, maxHp);

    private static BattleUnitState BuildAlly(
        StringName unitId,
        Vector2I coord,
        int currentHp,
        int maxHp
    ) => BuildUnit(unitId, "player", coord, currentHp, maxHp);

    private static BattleUnitState BuildUnit(
        StringName unitId,
        StringName factionId,
        Vector2I coord,
        int currentHp,
        int maxHp
    )
    {
        BattleUnitState unit = BattleTestFixture.BuildUnit(
            unitId,
            factionId,
            coord,
            currentAp: 2,
            currentHp: currentHp
        );
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, maxHp);
        unit.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS, 10);
        unit.attribute_snapshot.SetValue(AttributeService.ACTION_POINTS, 2);
        unit.current_hp = currentHp;
        unit.is_alive = currentHp > 0;
        return unit;
    }

    private static void SetSaveAbility(BattleUnitState unit, StringName abilityId, int score)
    {
        if (unit?.attribute_snapshot == null)
            return;
        unit.attribute_snapshot.SetValue(abilityId, score);
        unit.attribute_snapshot.SetValue(new StringName($"{abilityId}_modifier"), (score - 10) / 2);
    }

    private void AssertNoInternalSkillIdentity(BattleEventBatch batch)
    {
        string logs = string.Join(" | ", batch?.LogLinesTyped ?? Array.Empty<string>());
        foreach (
            StringName internalSkillId in new[]
            {
                JudgmentResolutionSkillId,
                JudgmentFallbackSkillId,
                SelfExecutionSkillId,
                ExecutionFearSkillId,
            }
        )
        {
            _test.False(
                logs.Contains(internalSkillId.ToString(), StringComparison.Ordinal),
                $"玩家日志不得显示内部技能 id {internalSkillId}。"
            );
        }
        foreach (
            string internalDisplayName in new[]
            {
                "判决结算",
                "判决重创",
                "自我处刑结算",
                "处刑余威",
            }
        )
        {
            _test.False(
                logs.Contains(internalDisplayName, StringComparison.Ordinal),
                $"玩家日志不得显示内部技能名 {internalDisplayName}。"
            );
        }
        _test.False(logs.Contains("自动施放", StringComparison.Ordinal), "玩家日志不得描述自动施放内部技能。");
    }

    private static bool HasLogLineContaining(BattleEventBatch batch, string expected)
    {
        foreach (string line in batch?.LogLinesTyped ?? Array.Empty<string>())
        {
            if (!string.IsNullOrEmpty(line) && line.Contains(expected, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    private static bool ContainsStringName(IEnumerable<StringName> values, StringName expected)
    {
        foreach (StringName value in values ?? Array.Empty<StringName>())
        {
            if (value == expected)
                return true;
        }
        return false;
    }

    private sealed class ExecutionerFixture : IDisposable
    {
        private readonly ItemContentRegistry _itemRegistry;
        private readonly ProgressionContentRegistry _progressionRegistry;
        private readonly PartyState _partyState;

        private ExecutionerFixture(
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

        internal static ExecutionerFixture Build()
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
            return new ExecutionerFixture(itemRegistry, progressionRegistry, partyState, runtime);
        }

        internal BattleUnitState BuildExecutionerUnit(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            member.equipment_state.SetEquippedEntry(
                "main_hand",
                ItemId,
                new GStringNameArray { "main_hand", "off_hand" },
                EquipmentInstanceState.CreateInstance(ItemId, $"eq_executioner_{label}")
            );
            IReadOnlyList<BattleUnitState> units =
                Runtime._unit_factory.BuildAllyUnits(_partyState, new GDictionary());
            if (units.Count != 1)
                throw new InvalidOperationException($"{label} should build exactly one ally unit");
            BattleUnitState unit = units[0];
            unit.SetAnchorCoord(new Vector2I(1, 1));
            unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
            unit.attribute_snapshot.SetValue(AttributeService.STAMINA_MAX, 100);
            unit.attribute_snapshot.SetValue(AttributeService.ACTION_POINTS, 2);
            unit.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, 20);
            unit.attribute_snapshot.SetValue(AttributeService.BASE_ATTACK_BONUS, 20);
            unit.SetCombatResources(100, 0, 100, 0, 2, 2);
            unit.is_alive = true;
            return unit;
        }

        internal void UseFixedDamageAndHit(BattleHitResolver hitResolver)
        {
            BattleTestFixture.ConfigureDamageResolverForTests(
                Runtime,
                new FixedTenSaveOneDamageResolver()
            );
            BattleTestFixture.ConfigureHitResolverForTests(Runtime, hitResolver);
        }

        public void Dispose()
        {
            BattleTestFixture.DisposeBattleFixture(Runtime, Runtime?.GetState());
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
            memberState.progression.unit_base_attributes.SetAttributeValue(
                PartyWarehouseService.StorageSpaceAttributeId,
                10
            );
            memberState.progression.unit_base_attributes.SetAttributeValue(
                AttributeService.HP_MAX,
                100
            );
            memberState.progression.unit_base_attributes.SetAttributeValue(
                AttributeService.STAMINA_MAX,
                100
            );
            memberState.progression.unit_base_attributes.SetAttributeValue(
                AttributeService.ACTION_POINTS,
                2
            );
            partyState.SetMemberState(memberState);
            partyState.active_member_ids.Add(memberId);
            partyState.leader_member_id = memberId;
            return partyState;
        }
    }

    private sealed class FixedTenSaveOneDamageResolver : FixedHitOneDamageResolver
    {
        internal override AttackEffectResolutionResult ResolveEffects(
            BattleUnitState sourceUnit,
            BattleUnitState targetUnit,
            IEnumerable<CombatEffectDefinition> effectDefinitions,
            DamageResolutionContext damageContext
        )
        {
            return base.ResolveEffects(
                sourceUnit,
                targetUnit,
                effectDefinitions,
                (damageContext ?? DamageResolutionContext.Empty()).WithSaveRollOverrides(
                    new[] { 10 }
                )
            );
        }
    }

    private sealed class FixedCritLockAwareHitResolver : FixedHitResolver
    {
        internal FixedCritLockAwareHitResolver(int fixedRoll)
            : base(fixedRoll) { }

        public override AttackResolutionMetadata ResolveAttackMetadata(
            BattleUnitState sourceUnit,
            BattleUnitState targetUnit,
            AttackCheckInput attackCheck,
            AttackContext attackContext
        )
        {
            if (!BattleFateAttackRules.IsAttackCritLocked(sourceUnit))
                return base.ResolveAttackMetadata(sourceUnit, targetUnit, attackCheck, attackContext);
            return BuildFixedAttackMetadata(
                attackCheck,
                attackContext,
                AttackResolutionHit,
                attackSuccess: true,
                criticalHit: false,
                ordinaryMiss: false
            );
        }
    }
}
