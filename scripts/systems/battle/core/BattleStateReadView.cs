using System.Collections.Generic;
using Godot;

internal readonly struct BattleStateReadView
{
    private readonly BattleState _state;

    internal BattleStateReadView(BattleState state)
    {
        _state = state;
    }

    internal bool IsValid => _state != null;
    internal StringName BattleId => _state?.battle_id ?? "";
    internal Vector2I MapSize => _state?.map_size ?? Vector2I.Zero;
    internal StringName ActiveUnitId => _state?.active_unit_id ?? "";
    internal BattlePhaseKind PhaseKind => _state?.PhaseKind ?? BattlePhaseKind.Unknown;
    internal BattleModalStateKind ModalStateKind =>
        _state?.ModalStateKind ?? BattleModalStateKind.None;

    internal BattleUnitReadView GetUnit(StringName unitId) =>
        new(_state?.GetUnit(unitId));

    internal BattleUnitReadView GetAliveUnit(StringName unitId) =>
        new(_state?.GetAliveUnit(unitId));

    internal BattleUnitReadView GetActiveUnit() => GetAliveUnit(ActiveUnitId);

    internal BattleCellReadView GetCell(Vector2I coord) =>
        new(_state?.GetCell(coord));

    internal bool ContainsCell(Vector2I coord) =>
        _state?.ContainsCell(coord) == true;

    internal IEnumerable<BattleUnitReadView> Units()
    {
        if (_state == null)
            yield break;
        foreach (BattleUnitState unitState in _state.Units())
            yield return new BattleUnitReadView(unitState);
    }

    internal IEnumerable<BattleUnitReadView> AliveUnits()
    {
        if (_state == null)
            yield break;
        foreach (BattleUnitState unitState in _state.AliveUnits())
            yield return new BattleUnitReadView(unitState);
    }

    internal IEnumerable<BattleCellReadView> Cells()
    {
        if (_state == null)
            yield break;
        foreach (BattleCellState cellState in _state.Cells())
            yield return new BattleCellReadView(cellState);
    }
}

internal readonly struct BattleUnitReadView
{
    private readonly BattleUnitState _unit;

    internal BattleUnitReadView(BattleUnitState unit)
    {
        _unit = unit;
    }

    internal bool IsValid => _unit != null;
    internal StringName UnitId => _unit?.unit_id ?? "";
    internal StringName SourceMemberId => _unit?.source_member_id ?? "";
    internal string DisplayName => _unit?.display_name ?? "";
    internal StringName FactionId => _unit?.faction_id ?? "";
    internal StringName ControlMode => _unit?.control_mode ?? "";
    internal bool MadnessTargetAnyTeam => _unit?.ai_blackboard?.madness_target_any_team == true;
    internal Vector2I Coord => _unit?.coord ?? Vector2I.Zero;
    internal Vector2I FootprintSize => _unit?.footprint_size ?? Vector2I.One;
    internal int BodySize => _unit?.body_size ?? BattleUnitState.BodySizeMedium;
    internal StringName BodySizeCategory => _unit?.body_size_category ?? "";
    internal bool IsAlive => _unit?.is_alive ?? false;
    internal int CurrentHp => _unit?.current_hp ?? 0;
    internal int CurrentShieldHp => _unit?.current_shield_hp ?? 0;
    internal int CurrentMp => _unit?.current_mp ?? 0;
    internal int CurrentStamina => _unit?.current_stamina ?? 0;
    internal int CurrentAura => _unit?.current_aura ?? 0;
    internal int CurrentAp => _unit?.current_ap ?? 0;
    internal int CurrentMovePoints => _unit?.current_move_points ?? 0;
    internal bool HasTakenActionThisTurn => _unit?.has_taken_action_this_turn ?? false;
    internal bool HasMovedThisTurn => _unit?.has_moved_this_turn ?? false;
    internal bool CanUseLockedMovePointsThisTurn =>
        _unit?.can_use_locked_move_points_this_turn ?? false;
    internal bool TurnCastingExhausted => _unit?.turn_casting_exhausted ?? false;
    internal StringName WeaponFamily => _unit?.weapon_family ?? "";
    internal StringName WeaponProfileTypeId => _unit?.weapon_profile_type_id ?? "";
    internal StringName WeaponProfileKind => _unit?.weapon_profile_kind ?? "";
    internal StringName WeaponCurrentGrip => _unit?.weapon_current_grip ?? "";
    internal int WeaponAttackRange => _unit?.weapon_attack_range ?? 0;
    internal bool WeaponUsesTwoHands => _unit?.weapon_uses_two_hands ?? false;
    internal StringName WeaponPhysicalDamageTag => _unit?.weapon_physical_damage_tag ?? "";
    internal int CurrentWeaponDamageDiceCount =>
        CurrentWeaponDamageDice().DiceCount;
    internal int CurrentWeaponDamageDiceSides =>
        CurrentWeaponDamageDice().DiceSides;
    internal int CurrentWeaponDamageFlatBonus =>
        CurrentWeaponDamageDice().FlatBonus;

    internal bool OccupiesCoord(Vector2I coord) =>
        _unit != null && _unit.OccupiesCoord(coord);

    internal bool HasKnownSkillLevel(StringName skillId) =>
        _unit?.HasKnownSkillLevelTyped(skillId) == true;

    internal int GetKnownSkillLevel(StringName skillId, int fallback = 0) =>
        _unit?.GetKnownSkillLevelTyped(skillId, fallback) ?? fallback;

    internal int GetKnownSkillLockHitBonus(StringName skillId, int fallback = 0) =>
        _unit?.GetKnownSkillLockHitBonusTyped(skillId, fallback) ?? fallback;

    internal int GetCooldown(StringName skillId) =>
        _unit?.GetCooldownTyped(skillId) ?? 0;

    internal bool HasStatusEffect(StringName statusId) =>
        _unit?.HasStatusEffect(statusId) == true;

    internal int GetStatusPower(StringName statusId)
    {
        BattleStatusEffectState status = _unit?.GetStatusEffect(statusId);
        return status?.power ?? 0;
    }

    internal int GetStatusStacks(StringName statusId)
    {
        BattleStatusEffectState status = _unit?.GetStatusEffect(statusId);
        return status?.stacks ?? 0;
    }

    internal int GetStatusRangeBonus(StringName statusId)
    {
        BattleStatusEffectState status = _unit?.GetStatusEffect(statusId);
        return status?.range_bonus ?? 0;
    }

    internal StringName GetStatusSourceUnitId(StringName statusId)
    {
        BattleStatusEffectState status = _unit?.GetStatusEffect(statusId);
        return status?.source_unit_id ?? "";
    }

    internal bool HasLockDodgeBonusStatus()
    {
        if (_unit == null)
            return false;
        foreach (StringName statusId in _unit.GetSortedStatusEffectIdsTyped())
        {
            BattleStatusEffectState status = _unit.GetStatusEffect(statusId);
            if (status?.lock_dodge_bonus == true)
                return true;
        }
        return false;
    }

    internal bool HasLockCritStatus()
    {
        if (_unit == null)
            return false;
        foreach (StringName statusId in _unit.GetSortedStatusEffectIdsTyped())
        {
            BattleStatusEffectState status = _unit.GetStatusEffect(statusId);
            if (status?.lock_crit == true)
                return true;
        }
        return false;
    }

    internal int GetAttackRollPenalty()
    {
        if (_unit == null)
            return 0;
        int penalty = 0;
        foreach (StringName statusId in _unit.GetSortedStatusEffectIdsTyped())
        {
            BattleStatusEffectState status = _unit.GetStatusEffect(statusId);
            if (status == null)
                continue;
            penalty = Mathf.Max(
                penalty,
                BattleStatusSemanticTable.GetAttackRollPenalty(status)
            );
        }
        return penalty;
    }

    internal bool HasCombatResourceUnlocked(StringName resourceId) =>
        _unit != null
        && resourceId != ""
        && _unit.unlocked_combat_resource_ids != null
        && _unit.unlocked_combat_resource_ids.Contains(resourceId);

    internal EquipmentState DuplicateEquipmentView() =>
        _unit?.GetEquipmentView()?.DuplicateState();

    internal StringName FirstKnownActiveSkillId =>
        _unit?.known_active_skill_ids != null && _unit.known_active_skill_ids.Count > 0
            ? _unit.known_active_skill_ids[0]
            : "";

    internal bool KnowsActiveSkill(StringName skillId) =>
        _unit?.known_active_skill_ids != null
        && skillId != ""
        && _unit.known_active_skill_ids.Contains(skillId);

    internal bool HasAttributeValue(StringName attributeId)
    {
        AttributeSnapshot snapshot = _unit?.attribute_snapshot;
        return snapshot != null && snapshot.HasValue(attributeId);
    }

    internal int GetAttributeValue(StringName attributeId, int fallback = 0)
    {
        AttributeSnapshot snapshot = _unit?.attribute_snapshot;
        if (snapshot == null || !snapshot.HasValue(attributeId))
            return fallback;
        return snapshot.GetValue(attributeId);
    }

    internal int GetResourceValue(CombatResourceKind resourceKind)
    {
        return resourceKind switch
        {
            CombatResourceKind.Ap => CurrentAp,
            CombatResourceKind.Aura => CurrentAura,
            CombatResourceKind.Mp => CurrentMp,
            CombatResourceKind.Stamina => CurrentStamina,
            _ => 0,
        };
    }

    internal bool IsBossTarget =>
        GetAttributeValue(BattleExecutionRules.BossTargetStatId) > 0
        || GetAttributeValue(BattleExecutionRules.FortuneMarkTargetStatId) > 1;

    internal bool IsEliteOrBossTarget =>
        GetAttributeValue(BattleExecutionRules.BossTargetStatId) > 0
        || GetAttributeValue(BattleExecutionRules.FortuneMarkTargetStatId) > 0;

    internal bool HasPerBattleCharge(StringName chargeKey) =>
        _unit?.HasPerBattleChargeTyped(chargeKey) == true;

    internal int GetPerBattleCharge(StringName chargeKey) =>
        _unit?.GetPerBattleChargeTyped(chargeKey) ?? 0;

    internal bool HasPerTurnCharge(StringName chargeKey) =>
        _unit?.HasPerTurnChargeTyped(chargeKey) == true;

    internal int GetPerTurnCharge(StringName chargeKey) =>
        _unit?.GetPerTurnChargeTyped(chargeKey) ?? 0;

    internal bool HasMovementTag(StringName tag) =>
        _unit != null && tag != "" && _unit.movement_tags != null && _unit.movement_tags.Contains(tag);

    internal IReadOnlyList<Vector2I> GetOccupiedCoords()
    {
        var result = new List<Vector2I>();
        if (_unit?.occupied_coords == null)
            return result;
        foreach (Vector2I coord in _unit.occupied_coords)
            result.Add(coord);
        return result;
    }

    internal IReadOnlyList<Vector2I> GetTargetCoords(Vector2I anchorCoord)
    {
        var result = new List<Vector2I>();
        Vector2I size = FootprintSize;
        if (size == Vector2I.Zero)
            size = BattleUnitState.GetFootprintSizeForBodySize(BodySize);
        size = new Vector2I(Mathf.Max(size.X, 1), Mathf.Max(size.Y, 1));
        for (int y = 0; y < size.Y; y++)
        {
            for (int x = 0; x < size.X; x++)
                result.Add(anchorCoord + new Vector2I(x, y));
        }
        return result;
    }

    internal IReadOnlyList<StringName> GetMovementTags()
    {
        var result = new List<StringName>();
        if (_unit?.movement_tags == null)
            return result;
        foreach (StringName tag in _unit.movement_tags)
            result.Add(tag);
        return result;
    }

    internal IReadOnlyList<StringName> GetSortedStatusEffectIds()
    {
        var result = new List<StringName>();
        if (_unit == null)
            return result;
        foreach (StringName statusId in _unit.GetSortedStatusEffectIdsTyped())
            result.Add(statusId);
        return result;
    }

    internal IEnumerable<BattleStatusReadView> StatusEffects()
    {
        if (_unit == null)
            yield break;
        foreach (BattleStatusEffectState statusState in _unit.GetStatusEffectsTyped())
            yield return new BattleStatusReadView(statusState);
    }

    internal int GetStatusMoveCostDelta()
    {
        if (_unit == null)
            return 0;
        int totalDelta = 0;
        foreach (StringName statusId in _unit.GetSortedStatusEffectIdsTyped())
            totalDelta += BattleStatusSemanticTable.GetMoveCostDelta(_unit.GetStatusEffect(statusId));
        return Mathf.Max(totalDelta, 0);
    }

    internal BattleWeaponDamageDiceReadData CurrentWeaponDamageDice() =>
        BattleWeaponDamageDiceReadData.FromUnit(_unit);
}

internal readonly record struct BattleWeaponDamageDiceReadData(
    int DiceCount,
    int DiceSides,
    int FlatBonus
)
{
    internal static BattleWeaponDamageDiceReadData Empty => new(0, 0, 0);

    internal static BattleWeaponDamageDiceReadData FromUnit(BattleUnitState unit)
    {
        if (unit == null)
            return Empty;
        return FromDice(unit.GetActiveWeaponDiceTyped());
    }

    internal static BattleWeaponDamageDiceReadData FromDice(WeaponDice dice)
    {
        if (dice == null || dice.IsEmpty())
            return Empty;
        return new BattleWeaponDamageDiceReadData(
            Mathf.Max(dice.dice_count, 0),
            Mathf.Max(dice.dice_sides, 0),
            dice.flat_bonus
        );
    }
}

internal readonly struct BattleStatusReadView
{
    private readonly BattleStatusEffectState _status;

    internal BattleStatusReadView(BattleStatusEffectState status)
    {
        _status = status;
    }

    internal bool IsValid => _status != null;
    internal StringName StatusId => _status?.status_id ?? "";
    internal bool CountsAsDebuffOverride => _status?.counts_as_debuff_override ?? false;
    internal bool CountsAsDebuff => _status?.counts_as_debuff ?? false;
    internal int MainSkillLockOtherDebuffCount =>
        _status?.main_skill_lock_other_debuff_count ?? 0;
    internal bool LockCounterattack => _status?.lock_counterattack ?? false;
    internal bool LockGuard => _status?.lock_guard ?? false;
    internal int Power => _status?.power ?? 0;
    internal int RangeBonus => _status?.range_bonus ?? 0;
}

internal readonly struct BattleCellReadView
{
    private readonly BattleCellState _cell;

    internal BattleCellReadView(BattleCellState cell)
    {
        _cell = cell;
    }

    internal bool IsValid => _cell != null;
    internal Vector2I Coord => _cell?.coord ?? Vector2I.Zero;
    internal int CurrentHeight => _cell?.current_height ?? 0;
    internal bool Passable => _cell?.passable ?? false;
    internal int MoveCost => _cell?.move_cost ?? 0;
    internal StringName BaseTerrain => _cell?.base_terrain ?? "";
    internal StringName OccupantUnitId => _cell?.occupant_unit_id ?? "";
}
