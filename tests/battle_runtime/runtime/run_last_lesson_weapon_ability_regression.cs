using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_last_lesson_weapon_ability_regression : LifecycleTestSceneTree
{
    private static readonly StringName LastLessonItemId = "weapon_unique_sword_last_lesson_020";
    private static readonly StringName OldChenLegacyTraitId =
        "weapon.sword.last_lesson.old_chen_legacy";
    private static readonly StringName LastLessonSkillTraitId =
        "weapon.sword.last_lesson.last_lesson";
    private static readonly StringName InheritanceSealTraitId =
        "weapon.sword.last_lesson.inheritance_seal";
    private static readonly StringName ClassroomAuraTraitId =
        "weapon.sword.last_lesson.classroom_aura";
    private static readonly StringName OldChenLegacyBindingId =
        "binding.weapon.sword.last_lesson.old_chen_legacy";
    private static readonly StringName LastLessonBindingId =
        "binding.weapon.sword.last_lesson.last_lesson";
    private static readonly StringName InheritanceSealBindingId =
        "binding.weapon.sword.last_lesson.inheritance_seal";
    private static readonly StringName ClassroomAuraBindingId =
        "binding.weapon.sword.last_lesson.classroom_aura";
    private static readonly StringName LastLessonSkillId =
        "weapon_sword_last_lesson_last_lesson";
    private static readonly StringName InheritanceSealSkillId =
        "weapon_sword_last_lesson_inheritance_seal";
    private static readonly StringName LastLessonGrantId =
        "grant.last_lesson.last_lesson.skill";
    private static readonly StringName InheritanceSealGrantId =
        "grant.last_lesson.inheritance_seal.skill";
    private static readonly StringName TeachingsStateKey = "teachings";
    private static readonly StringName NextAttackAdvantageStateKey = "next_attack_advantage";
    private static readonly StringName MaximizeDamageCurrentTurnStateKey =
        "maximize_damage_current_turn";
    private static readonly StringName SkipTeachingNextTurnStateKey = "skip_teaching_next_turn";
    private static readonly StringName DealtDamageCurrentTurnStateKey =
        "dealt_damage_current_turn";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        try
        {
            TestLastLessonProjectsRealContentOntoBattleUnitAndClearsOnUnequip();
            TestOldChenLegacyGainsTeachingsOnlyWhenTurnEndsWithoutDamage();
            TestGrantedSkillsConsumeTeachingsAndAffectNextAttackOrCurrentTurnDamage();
            TestClassroomAuraUsesNearbyEnemyCountWithoutMutatingBaseArmorClass();
            RequestTestExit(_test.Finish("Last Lesson weapon ability regression"));
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
            RequestTestExit(_test.Finish("Last Lesson weapon ability regression"));
        }
    }

    private void TestLastLessonProjectsRealContentOntoBattleUnitAndClearsOnUnequip()
    {
        using LastLessonFixture fixture = LastLessonFixture.Build(new GArray());
        _test.True(fixture.ItemDefs.ContainsKey(LastLessonItemId), "真实物品内容应包含最后一课。");
        _test.True(
            fixture.TraitDefs.ContainsKey(OldChenLegacyTraitId),
            "真实 trait 内容应包含老陈的遗训。"
        );
        _test.True(
            fixture.TraitDefs.ContainsKey(LastLessonSkillTraitId),
            "真实 trait 内容应包含最后一课主动能力。"
        );
        _test.True(
            fixture.TraitDefs.ContainsKey(InheritanceSealTraitId),
            "真实 trait 内容应包含传承之印。"
        );
        _test.True(
            fixture.TraitDefs.ContainsKey(ClassroomAuraTraitId),
            "真实 trait 内容应包含教室气息。"
        );
        _test.True(
            fixture.Bindings.ContainsKey(OldChenLegacyBindingId),
            "真实装备能力内容应包含老陈的遗训 binding。"
        );
        _test.True(
            fixture.Bindings.ContainsKey(LastLessonBindingId),
            "真实装备能力内容应包含最后一课 binding。"
        );
        _test.True(
            fixture.Bindings.ContainsKey(InheritanceSealBindingId),
            "真实装备能力内容应包含传承之印 binding。"
        );
        _test.True(
            fixture.Bindings.ContainsKey(ClassroomAuraBindingId),
            "真实装备能力内容应包含教室气息 binding。"
        );
        _test.True(fixture.SkillDefs.ContainsKey(LastLessonSkillId), "真实技能内容应包含最后一课装备技能。");
        _test.True(
            fixture.SkillDefs.ContainsKey(InheritanceSealSkillId),
            "真实技能内容应包含传承之印装备技能。"
        );
        if (!fixture.ItemDefs.ContainsKey(LastLessonItemId))
            return;

        ItemDef raw = ResourceLoader.Load<ItemDef>(
            "res://data/configs/items/weapon_unique_longsword_last_lesson.tres"
        );
        _test.True(raw != null, "最后一课原始物品资源应能加载。");
        if (raw != null)
        {
            _test.Eq(raw.base_item_id, new StringName("weapon_type_longsword_base"), "最后一课应继承 longsword 模板。");
        }

        // 机制文本落在 trait（老陈的遗训）里，物品说明只保留风味文字。
        if (fixture.TraitDefs.TryGetValue(OldChenLegacyTraitId, out TraitDefinition legacyTrait)
            && legacyTrait != null)
        {
            _test.True(
                legacyTrait.Description.Contains("教诲"),
                "老陈的遗训 trait 文本应描述真实配置中的教诲层数。"
            );
        }

        BattleUnitState baseline = fixture.BuildUnitWithoutWeapon("baseline");
        BattleUnitState equipped = fixture.BuildLastLessonUnit("projection");

        _test.Eq(equipped.weapon_item_id, LastLessonItemId, "最后一课装备后 unit 应保留真实 item_id。");
        _test.Eq(equipped.weapon_profile_type_id, new StringName("longsword"), "最后一课应投影为 longsword。");
        _test.Eq(equipped.weapon_attack_range, 1, "最后一课攻击距离应为 1。");
        _test.True(equipped.weapon_is_versatile, "最后一课应保留 versatile。");
        _test.Eq(equipped.weapon_one_handed_dice?.dice_count ?? 0, 1, "最后一课单手骰应为 1D8+2。");
        _test.Eq(equipped.weapon_one_handed_dice?.dice_sides ?? 0, 8, "最后一课单手骰应为 1D8+2。");
        _test.Eq(equipped.weapon_one_handed_dice?.flat_bonus ?? 0, 2, "最后一课单手骰固定加值应为 +2。");
        _test.Eq(equipped.weapon_two_handed_dice?.dice_count ?? 0, 1, "最后一课双手骰应为 1D10+2。");
        _test.Eq(equipped.weapon_two_handed_dice?.dice_sides ?? 0, 10, "最后一课双手骰应为 1D10+2。");
        _test.Eq(equipped.weapon_two_handed_dice?.flat_bonus ?? 0, 2, "最后一课双手骰固定加值应为 +2。");
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            OldChenLegacyTraitId,
            OldChenLegacyBindingId,
            "eq_last_lesson_projection"
        );
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            LastLessonSkillTraitId,
            LastLessonBindingId,
            "eq_last_lesson_projection"
        );
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            InheritanceSealTraitId,
            InheritanceSealBindingId,
            "eq_last_lesson_projection"
        );
        AssertUnitHasTraitAndAbilitySource(
            equipped,
            ClassroomAuraTraitId,
            ClassroomAuraBindingId,
            "eq_last_lesson_projection"
        );

        equipped.GetEquipmentView().ClearSlot("main_hand");
        fixture.Runtime._unit_factory.RefreshBattleUnit(equipped);
        _test.Eq(equipped.weapon_item_id, new StringName(""), "移除最后一课后 weapon_item_id 应清空。");
        _test.Eq(equipped.equipment_ability_sources.Count, 0, "移除最后一课后装备能力源应清空。");
        _test.Eq(
            equipped.effective_trait_instances.Count,
            baseline.effective_trait_instances.Count,
            "移除最后一课后装备 trait 实例应回到装备前状态。"
        );
    }

    private void TestOldChenLegacyGainsTeachingsOnlyWhenTurnEndsWithoutDamage()
    {
        using LastLessonFixture fixture = LastLessonFixture.Build(new GArray { 3 });
        BattleUnitState holder = fixture.BuildLastLessonUnit("legacy");
        BattleUnitState target = BuildTarget("legacy_target", new Vector2I(1, 0));
        BattleState state = BuildState("last_lesson_legacy", holder, target);
        fixture.Runtime.SetupStateForTests(state);

        IssueWait(fixture.Runtime, holder.unit_id);
        _test.Eq(
            GetAbilityState(holder, OldChenLegacyBindingId, TeachingsStateKey),
            1,
            "未造成伤害并结束行动后，老陈的遗训应给持有者 1 层教诲。"
        );

        state.PhaseKind = BattlePhaseKind.UnitActing;
        state.active_unit_id = holder.unit_id;
        holder.ResetPerTurnCharges();
        target.current_hp = 100;
        target.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            fixture.Runtime,
            holder,
            target,
            "last_lesson_legacy_damage",
            previewCommand: false
        );
        state = fixture.Runtime.GetState() ?? state;
        IssueWait(fixture.Runtime, holder.unit_id);
        _test.Eq(
            GetAbilityState(holder, OldChenLegacyBindingId, TeachingsStateKey),
            1,
            "本行动轮造成过伤害后结束行动，不应获得新的教诲层数。"
        );

        for (int index = 0; index < 5; index++)
        {
            state.PhaseKind = BattlePhaseKind.UnitActing;
            state.active_unit_id = holder.unit_id;
            holder.ResetPerTurnCharges();
            IssueWait(fixture.Runtime, holder.unit_id);
        }
        _test.Eq(
            GetAbilityState(holder, OldChenLegacyBindingId, TeachingsStateKey),
            3,
            "老陈的遗训教诲层数最多应为 3。"
        );
    }

    private void TestGrantedSkillsConsumeTeachingsAndAffectNextAttackOrCurrentTurnDamage()
    {
        using LastLessonFixture fixture = LastLessonFixture.Build(new GArray { 2, 4 });
        BattleUnitState holder = fixture.BuildLastLessonUnit("skills");
        BattleUnitState target = BuildTarget("skills_target", new Vector2I(1, 0));
        BattleState state = BuildState("last_lesson_skills", holder, target);
        fixture.Runtime.SetupStateForTests(state);

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
                    BattleState = state,
                }
            );
        _test.True(
            TryFindSkillEntry(availability, LastLessonSkillId, out BattleAvailableSkillEntry lessonEntry),
            "装备最后一课后应出现最后一课装备技能入口。"
        );
        _test.True(
            TryFindSkillEntry(availability, InheritanceSealSkillId, out BattleAvailableSkillEntry sealEntry),
            "装备最后一课后应出现传承之印装备技能入口。"
        );
        _test.False(lessonEntry?.IsSelectable ?? true, "没有教诲层数时，最后一课技能应禁用。");
        _test.False(sealEntry?.IsSelectable ?? true, "没有 3 层教诲时，传承之印技能应禁用。");

        SetAbilityState(holder, OldChenLegacyBindingId, TeachingsStateKey, 3);

        BattleSkillAvailabilityView ready =
            availabilityService.BuildView(
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
        _test.True(TryFindSkillEntry(ready, LastLessonSkillId, out lessonEntry), "最后一课技能入口应存在。");
        _test.True(TryFindSkillEntry(ready, InheritanceSealSkillId, out sealEntry), "传承之印技能入口应存在。");
        _test.True(lessonEntry.IsSelectable, "有教诲层数时，最后一课技能应可用。");
        _test.True(sealEntry.IsSelectable, "3 层教诲时，传承之印技能应可用。");

        _test.True(
            fixture.Runtime.CommitEquipmentSkillUsageIfNeeded(holder, BuildSkillCommand(holder.unit_id, lessonEntry)),
            "使用最后一课装备技能应提交 granted skill 触发。"
        );
        _test.Eq(
            GetAbilityState(holder, OldChenLegacyBindingId, TeachingsStateKey),
            2,
            "最后一课应消耗 1 层教诲。"
        );
        _test.Eq(
            GetAbilityState(holder, LastLessonBindingId, NextAttackAdvantageStateKey),
            1,
            "最后一课应写入下一次攻击 advantage 状态。"
        );

        BattleAttackCheckPolicyService attackPolicy = fixture.Runtime.GetAttackCheckPolicyService();
        SkillDefinition attackSkill = TestSkillDefinitionProjection.BuildSkill("fixture_basic_attack");
        BattleAttackRollModifierBundle advantageBundle = attackPolicy.BuildModifierBundle(
            attackPolicy.BuildSkillDefinitionAttackContext(
                state,
                holder,
                target,
                attackSkill,
                "skill_attack_check",
                "last_lesson_advantage",
                force_hit_no_crit: false
            )
        );
        _test.True(
            HasAdvantageModifier(advantageBundle, LastLessonBindingId),
            "最后一课技能应使下一次攻击检定获得 advantage。"
        );

        int beforeAdvantageHitHp = target.current_hp;
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            fixture.Runtime,
            holder,
            target,
            "last_lesson_after_advantage_hit",
            previewCommand: false
        );
        _test.True(target.current_hp < beforeAdvantageHitHp, "真实基础攻击应造成伤害。");
        _test.Eq(
            GetAbilityState(holder, LastLessonBindingId, NextAttackAdvantageStateKey),
            0,
            "最后一课 advantage 应在一次命中后清除。"
        );

        SetAbilityState(holder, OldChenLegacyBindingId, TeachingsStateKey, 3);
        _test.True(
            fixture.Runtime.CommitEquipmentSkillUsageIfNeeded(holder, BuildSkillCommand(holder.unit_id, sealEntry)),
            "使用传承之印装备技能应提交 granted skill 触发。"
        );
        _test.Eq(
            GetAbilityState(holder, OldChenLegacyBindingId, TeachingsStateKey),
            0,
            "传承之印应消耗 3 层教诲。"
        );
        _test.Eq(
            GetAbilityState(holder, InheritanceSealBindingId, MaximizeDamageCurrentTurnStateKey),
            1,
            "传承之印应写入当前行动轮次伤害骰最大化状态。"
        );
        _test.Eq(
            GetAbilityState(holder, OldChenLegacyBindingId, SkipTeachingNextTurnStateKey),
            1,
            "传承之印应使下回合不能获得教诲。"
        );
        target.current_hp = 100;
        target.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
        int beforeMaxedHp = target.current_hp;
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            fixture.Runtime,
            holder,
            target,
            "last_lesson_inheritance_seal_maxed",
            previewCommand: false
        );
        int maxedDamage = beforeMaxedHp - target.current_hp;

        using LastLessonFixture controlFixture = LastLessonFixture.Build(new GArray { 1, 1, 1, 1, 1, 1 });
        BattleUnitState controlHolder = controlFixture.BuildLastLessonUnit("skills_control");
        BattleUnitState controlTarget = BuildTarget("skills_control_target", new Vector2I(1, 0));
        controlTarget.current_hp = 100;
        controlTarget.attribute_snapshot.SetValue(AttributeService.HP_MAX, 100);
        WeaponAbilityCommandTestSupport.IssueBasicAttack(
            controlFixture.Runtime,
            controlHolder,
            controlTarget,
            "last_lesson_inheritance_seal_control",
            previewCommand: false
        );
        int controlDamage = 100 - controlTarget.current_hp;
        _test.True(
            maxedDamage > controlDamage,
            "传承之印应让下一次真实基础攻击伤害高于同武器未最大化的低骰攻击。"
        );
    }

    private void TestClassroomAuraUsesNearbyEnemyCountWithoutMutatingBaseArmorClass()
    {
        using LastLessonFixture fixture = LastLessonFixture.Build(new GArray());
        BattleUnitState holder = fixture.BuildLastLessonUnit("aura");
        holder.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS, 14);
        BattleUnitState attacker = BuildTarget("attacker", new Vector2I(4, 4));
        BattleUnitState enemy1 = BuildTarget("enemy1", new Vector2I(1, 0));
        BattleUnitState enemy2 = BuildTarget("enemy2", new Vector2I(0, 2));
        BattleUnitState enemy3 = BuildTarget("enemy3", new Vector2I(2, 0));
        BattleUnitState enemy4 = BuildTarget("enemy4", new Vector2I(2, 1));
        BattleState state = BuildState("last_lesson_aura", holder, attacker, enemy1, enemy2, enemy3, enemy4);
        fixture.Runtime.SetupStateForTests(state);

        BattleAttackCheckPolicyService attackPolicy = fixture.Runtime.GetAttackCheckPolicyService();
        SkillDefinition attackSkill = TestSkillDefinitionProjection.BuildSkill("fixture_basic_attack");
        BattleAttackRollModifierBundle crowded = attackPolicy.BuildModifierBundle(
            attackPolicy.BuildSkillDefinitionAttackContext(
                state,
                attacker,
                holder,
                attackSkill,
                "skill_attack_check",
                "last_lesson_classroom_aura",
                force_hit_no_crit: false
            )
        );
        _test.Eq(
            crowded.GetEffectiveModifierDelta(),
            -3,
            "10 尺内 4 个敌人时，教室气息应封顶为等价 AC +3，也就是攻击者 -3。"
        );
        _test.True(
            HasModifierSum(crowded, ClassroomAuraBindingId, -3),
            "教室气息三条 -1 应合计进入 modifier breakdown。"
        );
        _test.Eq(
            holder.attribute_snapshot.GetValue(AttributeService.ARMOR_CLASS),
            14,
            "教室气息不应改写 unit 基础 armor_class。"
        );

        enemy1.SetAnchorCoord(new Vector2I(4, 0));
        enemy2.SetAnchorCoord(new Vector2I(4, 1));
        enemy3.SetAnchorCoord(new Vector2I(4, 2));
        enemy4.SetAnchorCoord(new Vector2I(4, 3));
        BattleAttackRollModifierBundle isolated = attackPolicy.BuildModifierBundle(
            attackPolicy.BuildSkillDefinitionAttackContext(
                state,
                attacker,
                holder,
                attackSkill,
                "skill_attack_check",
                "last_lesson_classroom_aura_empty",
                force_hit_no_crit: false
            )
        );
        _test.Eq(
            isolated.GetEffectiveModifierDelta(),
            0,
            "近处没有敌人时，攻击持有者不应获得防御加值。"
        );

        BattleAttackRollModifierBundle emptyRoomPenalty = attackPolicy.BuildModifierBundle(
            attackPolicy.BuildSkillDefinitionAttackContext(
                state,
                holder,
                attacker,
                attackSkill,
                "skill_attack_check",
                "last_lesson_empty_room_penalty",
                force_hit_no_crit: false
            )
        );
        _test.Eq(
            emptyRoomPenalty.GetEffectiveModifierDelta(),
            -1,
            "10 尺内没有敌人时，持有者攻击检定应承受 -1。"
        );
    }

    private static BattleCommand BuildSkillCommand(
        StringName unitId,
        BattleAvailableSkillEntry entry
    ) =>
        new()
        {
            CommandKind = BattleCommandKind.Skill,
            unit_id = unitId,
            skill_entry_id = entry?.EntryRef.SkillEntryId ?? "",
            skill_id = entry?.EntryRef.SkillId ?? "",
        };

    private static void IssueWait(BattleRuntimeModule runtime, StringName unitId)
    {
        runtime.IssueCommand(
            new BattleCommand
            {
                CommandKind = BattleCommandKind.Wait,
                unit_id = unitId,
            }
        );
    }

    private static int GetAbilityState(
        BattleUnitState unit,
        StringName bindingId,
        StringName stateKey
    )
    {
        StringName key = FindChargeKey(unit, bindingId, stateKey);
        if (key == "")
            return 0;
        if (unit.HasPerBattleChargeTyped(key))
            return unit.GetPerBattleChargeTyped(key, 0);
        return unit.GetPerTurnChargeTyped(key, 0);
    }

    private static void SetAbilityState(
        BattleUnitState unit,
        StringName bindingId,
        StringName stateKey,
        int value
    )
    {
        StringName key = FindChargeKey(unit, bindingId, stateKey);
        if (key == "")
            throw new InvalidOperationException($"missing state key {bindingId}/{stateKey}");
        if (IsBattleScopeState(stateKey))
            unit.SetPerBattleChargeTyped(key, value);
        else
            unit.SetPerTurnChargeTyped(key, value);
    }

    private static StringName FindChargeKey(
        BattleUnitState unit,
        StringName bindingId,
        StringName stateKey
    )
    {
        string suffix = $"|{stateKey}";
        foreach (StringName key in unit.GetPerBattleChargesTyped().Keys)
        {
            string text = key.ToString();
            if (text.EndsWith(suffix, StringComparison.Ordinal))
                return key;
        }
        foreach (StringName key in unit.GetPerTurnChargesTyped().Keys)
        {
            string text = key.ToString();
            if (text.EndsWith(suffix, StringComparison.Ordinal))
                return key;
        }
        foreach (StringName key in unit.GetPerTurnChargeLimitsTyped().Keys)
        {
            string text = key.ToString();
            if (text.EndsWith(suffix, StringComparison.Ordinal))
                return key;
        }
        BattleEquipmentAbilitySourceState source = FindSource(unit, bindingId);
        StringName sourceKey = source?.SourceEquipmentInstanceId ?? new StringName("");
        if (sourceKey == "")
            sourceKey = source?.EquipmentDefId ?? new StringName("");
        if (sourceKey == "")
            sourceKey = source?.EffectiveInstanceKey ?? new StringName("");
        return sourceKey == ""
            ? new StringName("")
            : new StringName($"equipment_ability|state|{sourceKey}|{stateKey}");
    }

    private static bool IsBattleScopeState(StringName stateKey) =>
        stateKey == TeachingsStateKey || stateKey == SkipTeachingNextTurnStateKey;

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

    private static bool HasModifierSum(
        BattleAttackRollModifierBundle bundle,
        StringName sourceId,
        int expectedDelta
    )
    {
        int total = 0;
        foreach (BattleAttackRollModifierSpec spec in bundle?.Breakdown ?? Array.Empty<BattleAttackRollModifierSpec>())
        {
            if (spec.source_id == sourceId)
                total += spec.modifier_delta;
        }
        return total == expectedDelta;
    }

    private static bool HasAdvantageModifier(
        BattleAttackRollModifierBundle bundle,
        StringName sourceId
    )
    {
        foreach (BattleAttackRollModifierSpec spec in bundle?.Breakdown ?? Array.Empty<BattleAttackRollModifierSpec>())
        {
            if (spec.source_id == sourceId && spec.applies_to == "attack_advantage")
                return true;
        }
        return false;
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

    private static BattleUnitState BuildTarget(StringName unitId, Vector2I coord)
    {
        BattleUnitState unit = new()
        {
            unit_id = unitId,
            display_name = unitId.ToString(),
            faction_id = "enemy",
            is_alive = true,
            current_hp = 30,
        };
        unit.SetAnchorCoord(coord);
        unit.attribute_snapshot.SetValue(AttributeService.ARMOR_CLASS, 14);
        unit.attribute_snapshot.SetValue(AttributeService.ATTACK_BONUS, 0);
        unit.attribute_snapshot.SetValue(AttributeService.BASE_ATTACK_BONUS, 0);
        unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, 30);
        unit.SetEquipmentView(new EquipmentState());
        return unit;
    }

    private sealed class LastLessonFixture : IDisposable
    {
        private readonly ItemContentRegistry _itemRegistry;
        private readonly ProgressionContentRegistry _progressionRegistry;
        private readonly PartyState _partyState;

        private LastLessonFixture(
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
        internal IReadOnlyDictionary<StringName, ItemDef> ItemDefs { get; }
        internal IReadOnlyDictionary<StringName, SkillDefinition> SkillDefs { get; }
        internal IReadOnlyDictionary<StringName, TraitDefinition> TraitDefs { get; }
        internal IReadOnlyDictionary<StringName, EquipmentAbilityBindingDefinition> Bindings { get; }

        internal static LastLessonFixture Build(GArray damageRolls)
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
            return new LastLessonFixture(itemRegistry, progressionRegistry, partyState, runtime);
        }

        internal BattleUnitState BuildUnitWithoutWeapon(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            return BuildSingleAllyUnit(label);
        }

        internal BattleUnitState BuildLastLessonUnit(string label)
        {
            PartyMemberState member = _partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            member.equipment_state.SetEquippedEntry(
                "main_hand",
                LastLessonItemId,
                new GStringNameArray { "main_hand" },
                EquipmentInstanceState.CreateInstance(LastLessonItemId, $"eq_last_lesson_{label}")
            );
            BattleUnitState unit = BuildSingleAllyUnit(label);
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
