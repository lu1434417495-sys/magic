using System;
using System.Collections.Generic;
using Godot;

public partial class run_phoenix_rebirth_body_cloak_content_regression
    : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        try
        {
            ContentSnapshot snapshot = GameSessionTestFactory.GetProcessSnapshot();
            TestBodyProjection(snapshot);
            TestFireShieldProjection(snapshot);
            TestCloakProjection(snapshot);
        }
        catch (Exception exception)
        {
            _test.Fail($"Unhandled exception: {exception}");
        }

        RequestTestExit(
            _test.Finish("Phoenix Rebirth body and cloak content regression")
        );
    }

    private void TestBodyProjection(ContentSnapshot snapshot)
    {
        ItemDefinition body = RequireItem(snapshot, "armor_phoenix_rebirth_body");
        _test.True(
            Contains(body?.TraitIds, "equipment.phoenix_rebirth.body.burning_plate"),
            "凤凰板甲应通过正式ItemDefinition挂载燃烧能力trait。"
        );

        EquipmentAbilityBindingDefinition binding = RequireBinding(
            snapshot,
            "binding.phoenix_rebirth.body.burning_plate"
        );
        if (binding == null)
            return;

        EquipmentAbilityReactionDefinition damageReaction = FindReaction(
            binding,
            "reaction.phoenix_rebirth.body.external_fire"
        );
        _test.True(damageReaction != null, "凤凰板甲应声明finalized damage燃烧反应。");
        _test.Eq(damageReaction?.ConditionGroup?.Mode ?? new StringName(""), new StringName("all"), "板甲四项来源条件必须全部满足。");
        _test.Eq(
            damageReaction?.Trigger ?? default,
            EquipmentAbilityTriggerKind.OnDamageTakenFinalized,
            "板甲燃烧必须消费finalized damage。"
        );
        AssertCompareFact(damageReaction, "raw_damage", "gt", intLiteral: 0);
        AssertCompareFact(damageReaction, "damage_tag", "eq", stringLiteral: "fire");
        AssertCompareFact(damageReaction, "is_equipment_generated", "eq", intLiteral: 0);
        AssertCompareFact(damageReaction, "is_self_damage", "eq", intLiteral: 0);

        ApplyStatusActionPayloadDefinition burning =
            FindAction(damageReaction, "action.phoenix_rebirth.body.add_burning_layer")
                ?.PayloadDefinition as ApplyStatusActionPayloadDefinition;
        _test.True(burning != null, "板甲燃烧应投影typed apply_status payload。");
        _test.Eq(burning?.StatusId ?? new StringName(""), new StringName("phoenix_armor_burning_layers"), "板甲燃烧状态ID应稳定。");
        _test.Eq(burning?.DurationTu ?? 0, 60, "板甲燃烧每次应刷新到60 TU。");
        _test.Eq(burning?.StackDelta ?? 0, 1, "每次外部fire事件应增加1层。");
        _test.Eq(burning?.StackBehavior ?? new StringName(""), new StringName("add"), "板甲燃烧应累加层数。");
        _test.Eq(burning?.StackLimit ?? 0, 3, "板甲燃烧最多3层。");
        _test.Eq(burning?.ArmorClassBonusPerStack ?? 0, 1, "板甲燃烧每层应提供AC+1。");

        EquipmentGrantedActionDefinition grant = FindGrant(
            binding,
            "grant.phoenix_rebirth.body.fire_shield"
        );
        _test.True(grant != null, "凤凰板甲应授予释放火盾技能。");
        _test.Eq(grant?.SkillId ?? new StringName(""), new StringName("equipment_phoenix_rebirth_fire_shield"), "板甲授予技能ID应稳定。");
        _test.Eq(grant?.UsagePeriodKind ?? EquipmentAbilityUsagePeriodKind.None, EquipmentAbilityUsagePeriodKind.PerWorldDay, "火盾应按世界日刷新。");
        _test.Eq(grant?.MaxUsesPerPeriod ?? 0, 1, "火盾每个世界日只能使用一次。");
        _test.True(grant?.AvailabilityConditions == null, "火盾不得要求至少一层燃烧才可用。");

        SkillDefinition skill = RequireSkill(snapshot, "equipment_phoenix_rebirth_fire_shield");
        _test.Eq(skill?.CombatProfile?.ApCost ?? -1, 2, "释放火盾应消耗2 AP。");
        _test.Eq(skill?.CombatProfile?.TargetTeamFilter ?? new StringName(""), new StringName("self"), "释放火盾只能选择自身。");
        _test.Eq(skill?.CombatProfile?.EffectDefinitions.Count ?? 0, 1, "释放火盾应有正式状态效果用于预览与执行。");
        if (skill?.CombatProfile?.EffectDefinitions.Count == 1)
        {
            CombatEffectDefinition effect = skill.CombatProfile.EffectDefinitions[0];
            _test.Eq(effect.StatusId, new StringName("phoenix_fire_shield"), "技能本体应应用凤凰火盾状态。");
            _test.Eq(effect.DurationTu, 60, "技能本体火盾状态应持续60 TU。");
        }

        EquipmentAbilityReactionDefinition used = FindReaction(
            binding,
            "reaction.phoenix_rebirth.body.fire_shield_used"
        );
        _test.True(used != null, "板甲应在正式授予技能成功后清层并应用火盾。");
        _test.Eq(used?.Actions.Count ?? 0, 2, "火盾after_skill链应精确包含清层与应用状态。");
        ClearStatusActionPayloadDefinition clear = used?.Actions.Count > 0
            ? used.Actions[0].PayloadDefinition as ClearStatusActionPayloadDefinition
            : null;
        ApplyStatusActionPayloadDefinition shield = used?.Actions.Count > 1
            ? used.Actions[1].PayloadDefinition as ApplyStatusActionPayloadDefinition
            : null;
        _test.Eq(clear?.StatusId ?? new StringName(""), new StringName("phoenix_armor_burning_layers"), "火盾成功后应先清除燃烧层。");
        _test.Eq(shield?.StatusId ?? new StringName(""), new StringName("phoenix_fire_shield"), "火盾成功后应应用正式火盾状态。");
        _test.Eq(shield?.DurationTu ?? 0, 60, "正式火盾状态应持续60 TU。");
        _test.Eq(shield?.ArmorClassBonusPerStack ?? 0, 3, "正式火盾状态应提供AC+3。");
    }

    private void TestFireShieldProjection(ContentSnapshot snapshot)
    {
        EquipmentAbilityBindingDefinition binding = RequireBinding(
            snapshot,
            "binding.phoenix_rebirth.status.fire_shield"
        );
        if (binding == null)
            return;
        _test.Eq(binding.ActivationStatusId, new StringName("phoenix_fire_shield"), "火盾反击必须由状态动态激活。");
        EquipmentAbilityReactionDefinition reaction = FindReaction(
            binding,
            "reaction.phoenix_rebirth.fire_shield.melee_counter"
        );
        _test.Eq(reaction?.Trigger ?? default, EquipmentAbilityTriggerKind.OnHitReceived, "火盾应在成功受击后反击。");
        _test.Eq(reaction?.Timing ?? default, EquipmentAbilityTimingKind.AfterHitReceived, "火盾反击时序应为after_hit_received。");
        _test.Eq(reaction?.ConditionGroup?.Mode ?? new StringName(""), new StringName("all"), "火盾近战筛选条件组必须为all。");
        AssertCompareFact(reaction, "weapon_range_type", "eq", stringLiteral: "melee", subject: "target");
        DealDamageActionPayloadDefinition damage =
            FindAction(reaction, "action.phoenix_rebirth.fire_shield.counter")
                ?.PayloadDefinition as DealDamageActionPayloadDefinition;
        _test.True(damage != null, "火盾反击应投影typed deal_damage payload。");
        _test.Eq(damage?.TargetSelector ?? new StringName(""), new StringName("target"), "火盾应伤害本次攻击者。");
        _test.Eq(damage?.DamageType ?? new StringName(""), new StringName("fire"), "火盾反击应为fire。");
        AssertDice(damage?.Dice, 2, 6, "火盾反击");
    }

    private void TestCloakProjection(ContentSnapshot snapshot)
    {
        ItemDefinition cloak = RequireItem(snapshot, "acc_phoenix_rebirth_cloak");
        _test.True(Contains(cloak?.TraitIds, "equipment.phoenix_rebirth.cloak.fatal_ember"), "凤凰披风应继续挂载原有致死拦截trait。");
        EquipmentAbilityBindingDefinition binding = RequireBinding(
            snapshot,
            "binding.phoenix_rebirth.cloak.fatal_ember"
        );
        if (binding == null)
            return;
        _test.Eq(binding.FatalIntercepts.Count, 1, "披风原有25%致死拦截必须保留。");
        EquipmentFatalInterceptDefinition fatal = binding.FatalIntercepts[0];
        _test.Eq(fatal.ResolutionOrder, 300, "披风致死拦截order 300必须保留。");
        _test.Eq(fatal.ProtectionPriority, 100, "披风只应拦截普通致死层。");
        _test.Eq(fatal.UsagePeriodKind, EquipmentAbilityUsagePeriodKind.PerBattle, "披风致死拦截应每场一次。");
        _test.Eq(fatal.MaxAttemptsPerPeriod, 1, "披风致死拦截每场仅一次正式尝试。");
        _test.Eq(fatal.RecoveryKind, EquipmentFatalInterceptRecoveryKind.HpDice, "披风致死拦截仍应恢复1D12。");
        AssertDice(fatal.RecoveryDice, 1, 12, "披风致死拦截");
        _test.Eq(fatal.RollGate?.Compare ?? new StringName(""), new StringName("lte"), "披风致死检定应为D100小于等于阈值。");
        _test.Eq(fatal.RollGate?.Threshold ?? 0, 25, "披风致死检定阈值应为25。");
        AssertDice(fatal.RollGate?.Roll, 1, 100, "披风致死概率检定");
        _test.Eq(binding.StateSchemas.Count, 1, "披风应声明一个本场凤凰怒火状态键。");
        if (binding.StateSchemas.Count == 1)
        {
            EquipmentAbilityStateSchemaDefinition state = binding.StateSchemas[0];
            _test.Eq(state.StateKey, new StringName("low_hp_burst_used"), "披风本场状态键应稳定。");
            _test.Eq(state.ResetTiming, new StringName("battle"), "披风凤凰怒火应在每场战斗重置。");
            _test.Eq(state.MaxIntValue, 1, "披风凤凰怒火状态应为0/1。");
        }

        EquipmentAbilityReactionDefinition reaction = FindReaction(
            binding,
            "reaction.phoenix_rebirth.cloak.low_hp_burst"
        );
        _test.Eq(reaction?.Trigger ?? default, EquipmentAbilityTriggerKind.OnDamageTakenFinalized, "披风跨阈值必须消费finalized damage。");
        _test.Eq(reaction?.ConditionGroup?.Mode ?? new StringName(""), new StringName("all"), "披风六项跨阈值条件必须全部满足。");
        AssertCompareFact(reaction, "raw_damage", "gt", intLiteral: 0);
        AssertCompareFact(reaction, "is_equipment_generated", "eq", intLiteral: 0);
        AssertCompareFact(reaction, "is_self_damage", "eq", intLiteral: 0);
        AssertCompareFact(reaction, "hp_before_percent_bp", "gt", intLiteral: 5000);
        AssertCompareFact(reaction, "hp_percent_bp", "lte", intLiteral: 5000);
        AssertCompareFact(reaction, "equipment_ability_state", "eq", intLiteral: 0);
        _test.Eq(reaction?.Actions.Count ?? 0, 2, "披风应先爆发再标记本场已用。");
        TriggerSkillActionPayloadDefinition burst = reaction?.Actions.Count > 0
            ? reaction.Actions[0].PayloadDefinition as TriggerSkillActionPayloadDefinition
            : null;
        ModifyAbilityStateActionPayloadDefinition consume = reaction?.Actions.Count > 1
            ? reaction.Actions[1].PayloadDefinition as ModifyAbilityStateActionPayloadDefinition
            : null;
        _test.Eq(burst?.SkillId ?? new StringName(""), new StringName("equipment_phoenix_rebirth_cloak_low_hp_burst"), "披风应触发正式内部范围技能。");
        _test.Eq(burst?.TargetSelector ?? new StringName(""), new StringName("source"), "披风爆发应以佩戴者为圆心。");
        _test.Eq(consume?.StateKey ?? new StringName(""), new StringName("low_hp_burst_used"), "无论命中几个敌人都应标记本场已用。");
        _test.Eq(consume?.Operation ?? new StringName(""), new StringName("set"), "披风本场状态应以set写入。");
        AssertAreaDamageSkill(snapshot, "equipment_phoenix_rebirth_cloak_low_hp_burst", 1, 2, 6);
    }

    private void AssertCompareFact(
        EquipmentAbilityReactionDefinition reaction,
        StringName factId,
        StringName compare,
        int? intLiteral = null,
        StringName stringLiteral = default,
        StringName subject = default
    )
    {
        CompareFactConditionPayloadDefinition found = null;
        foreach (EquipmentAbilityConditionDefinition condition in reaction?.ConditionGroup?.Conditions ?? Array.Empty<EquipmentAbilityConditionDefinition>())
        {
            if (
                condition?.PayloadDefinition is CompareFactConditionPayloadDefinition candidate
                && candidate.Left?.FactId == factId
            )
            {
                found = candidate;
                break;
            }
        }
        _test.True(found != null, $"{reaction?.ReactionId}应声明{factId} typed compare_fact条件。");
        if (found == null)
            return;
        _test.Eq(found.Compare, compare, $"{factId}比较符应精确。");
        if (intLiteral.HasValue)
            _test.Eq(found.Right?.IntLiteral ?? int.MinValue, intLiteral.Value, $"{factId}整数阈值应精确。");
        if (stringLiteral != default && stringLiteral != "")
            _test.Eq(found.Right?.StringNameLiteral ?? new StringName(""), stringLiteral, $"{factId}字符串阈值应精确。");
        if (subject != default && subject != "")
            _test.Eq(found.Left?.Subject ?? new StringName(""), subject, $"{factId}subject应精确。");
    }

    private void AssertAreaDamageSkill(
        ContentSnapshot snapshot,
        StringName skillId,
        int radius,
        int diceCount,
        int diceSides
    )
    {
        SkillDefinition skill = RequireSkill(snapshot, skillId);
        CombatSkillDefinition combat = skill?.CombatProfile;
        _test.Eq(combat?.AreaValue ?? -1, radius, $"{skillId}范围半径应精确。");
        _test.Eq(combat?.EffectDefinitions.Count ?? 0, 1, $"{skillId}应只有一个主伤害效果。");
        if (combat?.EffectDefinitions.Count == 1)
        {
            CombatEffectDefinition damage = combat.EffectDefinitions[0];
            _test.Eq(damage.DamageTag, new StringName("fire"), $"{skillId}应造成fire。");
            _test.Eq(damage.DiceCount, diceCount, $"{skillId}骰数应精确。");
            _test.Eq(damage.DiceSides, diceSides, $"{skillId}骰面应精确。");
            _test.Eq(damage.SaveDc, 0, $"{skillId}不得要求豁免。");
        }
    }

    private void AssertDice(DiceExpressionDefinition dice, int count, int sides, string label)
    {
        _test.Eq(dice?.Terms.Count ?? 0, 1, $"{label}应只有一个骰项。");
        if (dice?.Terms.Count == 1)
        {
            _test.Eq(dice.Terms[0].DiceCount, count, $"{label}骰数应精确。");
            _test.Eq(dice.Terms[0].DiceSides, sides, $"{label}骰面应精确。");
        }
    }

    private EquipmentAbilityBindingDefinition RequireBinding(ContentSnapshot snapshot, StringName id)
    {
        if (snapshot?.EquipmentAbilityBindings != null && snapshot.EquipmentAbilityBindings.TryGetValue(id, out EquipmentAbilityBindingDefinition value))
            return value;
        _test.Fail($"missing equipment binding {id}.");
        return null;
    }

    private SkillDefinition RequireSkill(ContentSnapshot snapshot, StringName id)
    {
        if (snapshot?.Skills != null && snapshot.Skills.TryGetValue(id, out SkillDefinition value))
            return value;
        _test.Fail($"missing skill {id}.");
        return null;
    }

    private ItemDefinition RequireItem(ContentSnapshot snapshot, StringName id)
    {
        if (snapshot?.Items != null && snapshot.Items.TryGetValue(id, out ItemDefinition value))
            return value;
        _test.Fail($"missing item {id}.");
        return null;
    }

    private static EquipmentAbilityReactionDefinition FindReaction(EquipmentAbilityBindingDefinition binding, StringName id)
    {
        foreach (EquipmentAbilityReactionDefinition reaction in binding?.Reactions ?? Array.Empty<EquipmentAbilityReactionDefinition>())
            if (reaction?.ReactionId == id)
                return reaction;
        return null;
    }

    private static EquipmentAbilityActionDefinition FindAction(EquipmentAbilityReactionDefinition reaction, StringName id)
    {
        foreach (EquipmentAbilityActionDefinition action in reaction?.Actions ?? Array.Empty<EquipmentAbilityActionDefinition>())
            if (action?.ActionId == id)
                return action;
        return null;
    }

    private static EquipmentGrantedActionDefinition FindGrant(EquipmentAbilityBindingDefinition binding, StringName id)
    {
        foreach (EquipmentGrantedActionDefinition grant in binding?.GrantedActions ?? Array.Empty<EquipmentGrantedActionDefinition>())
            if (grant?.GrantedActionId == id)
                return grant;
        return null;
    }

    private static bool Contains(IReadOnlyList<StringName> values, StringName expected)
    {
        foreach (StringName value in values ?? Array.Empty<StringName>())
            if (value == expected)
                return true;
        return false;
    }
}
