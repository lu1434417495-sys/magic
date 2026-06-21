using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GEffectiveTraitArray = Godot.Collections.Array<BattleEffectiveTraitInstanceState>;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;

internal enum BattleWeaponProfileKind
{
    Unknown = 0,
    None,
    Unarmed,
    Natural,
    Equipped,
}

internal enum BattleWeaponGripKind
{
    Unknown = 0,
    None,
    OneHanded,
    TwoHanded,
}

public partial class BattleUnitState : RefCounted
{
    private static readonly StringName WeaponProfileKindNone = "none";
    private static readonly StringName WeaponProfileKindUnarmed = "unarmed";
    private static readonly StringName WeaponProfileKindNatural = "natural";
    private static readonly StringName WeaponProfileKindEquipped = "equipped";
    private static readonly StringName WeaponGripNone = "none";
    private static readonly StringName WeaponGripOneHanded = "one_handed";
    private static readonly StringName WeaponGripTwoHanded = "two_handed";
    private static readonly StringName BodySizeCategoryMedium = "medium";
    private static readonly string[] EffectiveTraitFields =
    {
        "trait_id",
        "effective_instance_key",
        "source_type",
        "source_id",
        "effect_type",
        "trigger_type",
        "charge_scope",
        "charge_reset_timing",
        "rank",
        "stacks",
        "roll_values",
    };

    internal const int DefaultMovePointsPerTurn = 2;
    internal const int DefaultActionThreshold = 120;
    internal const int BodySizeTiny = 1;
    internal const int BodySizeSmall = 1;
    internal const int BodySizeMedium = 2;
    internal const int BodySizeLarge = 3;
    internal const int BodySizeHuge = 4;
    internal const int BodySizeGargantuan = 5;
    internal const int BodySizeBoss = 6;

    private static readonly string[] ToDictFields =
    {
        "unit_id",
        "source_member_id",
        "enemy_template_id",
        "display_name",
        "faction_id",
        "control_mode",
        "ai_brain_id",
        "ai_state_id",
        "coord",
        "body_size",
        "body_size_category",
        "footprint_size",
        "occupied_coords",
        "is_alive",
        "attribute_snapshot",
        "equipment_view",
        "current_hp",
        "current_mp",
        "current_stamina",
        "current_aura",
        "aura_max",
        "current_ap",
        "current_move_points",
        "unlocked_combat_resource_ids",
        "stamina_recovery_progress",
        "is_resting",
        "has_taken_action_this_turn",
        "can_use_locked_move_points_this_turn",
        "current_shield_hp",
        "shield_max_hp",
        "shield_duration",
        "shield_family",
        "shield_source_unit_id",
        "shield_source_skill_id",
        "action_progress",
        "action_threshold",
        "known_active_skill_ids",
        "known_skill_level_map",
        "known_skill_lock_hit_bonus_map",
        "movement_tags",
        "vision_tags",
        "proficiency_tags",
        "save_advantage_tags",
        "damage_resistances",
        "effective_trait_instances",
        "effective_trait_ids",
        "versatility_pick",
        "weapon_profile_kind",
        "weapon_item_id",
        "weapon_profile_type_id",
        "weapon_family",
        "weapon_current_grip",
        "weapon_attack_range",
        "weapon_one_handed_dice",
        "weapon_two_handed_dice",
        "weapon_is_versatile",
        "weapon_uses_two_hands",
        "weapon_physical_damage_tag",
        "cooldowns",
        "last_turn_tu",
        "status_effects",
    };

    internal static IReadOnlyList<StringName> DefaultUnlockedCombatResourceIdsTyped =>
        CombatResourceIds.DefaultUnlocked;

    internal static GStringNameArray CreateDefaultUnlockedCombatResourceProjection() =>
        new(CombatResourceIds.DefaultUnlocked);

    internal static bool IsValidCombatResourceId(StringName resourceId) =>
        CombatResourceIds.ToResourceKind(resourceId) != CombatResourceIdKind.Unknown;

    internal static StringName ToStringName(BattleWeaponProfileKind kind)
    {
        return kind switch
        {
            BattleWeaponProfileKind.None => WeaponProfileKindNone,
            BattleWeaponProfileKind.Unarmed => WeaponProfileKindUnarmed,
            BattleWeaponProfileKind.Natural => WeaponProfileKindNatural,
            BattleWeaponProfileKind.Equipped => WeaponProfileKindEquipped,
            _ => new StringName(""),
        };
    }

    internal static BattleWeaponProfileKind ToWeaponProfileKind(StringName value)
    {
        if (value == WeaponProfileKindNone)
            return BattleWeaponProfileKind.None;
        if (value == WeaponProfileKindUnarmed)
            return BattleWeaponProfileKind.Unarmed;
        if (value == WeaponProfileKindNatural)
            return BattleWeaponProfileKind.Natural;
        if (value == WeaponProfileKindEquipped)
            return BattleWeaponProfileKind.Equipped;
        return BattleWeaponProfileKind.Unknown;
    }

    internal static StringName ToStringName(BattleWeaponGripKind kind)
    {
        return kind switch
        {
            BattleWeaponGripKind.None => WeaponGripNone,
            BattleWeaponGripKind.OneHanded => WeaponGripOneHanded,
            BattleWeaponGripKind.TwoHanded => WeaponGripTwoHanded,
            _ => new StringName(""),
        };
    }

    internal static BattleWeaponGripKind ToWeaponGripKind(StringName value)
    {
        if (value == WeaponGripNone)
            return BattleWeaponGripKind.None;
        if (value == WeaponGripOneHanded)
            return BattleWeaponGripKind.OneHanded;
        if (value == WeaponGripTwoHanded)
            return BattleWeaponGripKind.TwoHanded;
        return BattleWeaponGripKind.Unknown;
    }

    public StringName unit_id = "";
    public StringName source_member_id = "";
    public StringName enemy_template_id = "";
    public string display_name = "";
    public Texture2D battle_sprite_texture;
    public StringName faction_id = "";
    public StringName control_mode = "manual";
    public StringName ai_brain_id = "";
    public StringName ai_state_id = "";
    internal BattleAiBlackboard ai_blackboard = new();
    public Vector2I coord { get; internal set; } = Vector2I.Zero;
    public int body_size { get; internal set; } = BodySizeMedium;
    public StringName body_size_category { get; internal set; } = BodySizeCategoryMedium;
    public Vector2I footprint_size { get; internal set; } = Vector2I.One;
    public GVector2IArray occupied_coords { get; internal set; } = new();
    public bool is_alive { get; internal set; } = true;
    public AttributeSnapshot attribute_snapshot = NewAttributeSnapshot();
    public EquipmentState equipment_view = NewEquipmentState();
    public bool equipment_view_initialized;
    public int current_hp { get; internal set; }
    public int current_mp { get; internal set; }
    public int current_stamina { get; internal set; }
    public int current_aura { get; internal set; }
    public int current_ap { get; internal set; }
    public int current_move_points { get; internal set; } = DefaultMovePointsPerTurn;
    public GStringNameArray unlocked_combat_resource_ids =
        CreateDefaultUnlockedCombatResourceProjection();
    public int stamina_recovery_progress;
    public bool is_resting;
    public bool has_taken_action_this_turn;
    public bool has_moved_this_turn;
    public bool can_use_locked_move_points_this_turn;
    public int current_shield_hp;
    public int shield_max_hp;
    public int shield_duration = -1;
    public StringName shield_family = "";
    public StringName shield_source_unit_id = "";
    public StringName shield_source_skill_id = "";
    public int action_progress;
    public int action_threshold = DefaultActionThreshold;
    public GStringNameArray known_active_skill_ids { get; internal set; } = new();
    public BattleStringNameIntMap known_skill_level_map = new();
    public BattleStringNameIntMap known_skill_lock_hit_bonus_map = new();
    public GStringNameArray movement_tags = new();
    public GStringNameArray vision_tags = new();
    public GStringNameArray proficiency_tags = new();
    public GStringNameArray save_advantage_tags = new();
    public BattleStringNameMap damage_resistances = new();
    public GEffectiveTraitArray effective_trait_instances = new();
    public GStringNameArray effective_trait_ids = new();
    internal BattleUnitControlMode ControlModeKind
    {
        get => BattleTypedNames.ToControlMode(control_mode);
        set => control_mode = BattleTypedNames.ToStringName(value);
    }
    public StringName versatility_pick = "";
    public StringName weapon_profile_kind = WeaponProfileKindNone;
    public StringName weapon_item_id = "";
    public StringName weapon_profile_type_id = "";
    public StringName weapon_family = "";
    public StringName weapon_current_grip = WeaponGripNone;
    public int weapon_attack_range;
    public WeaponDice weapon_one_handed_dice = new();
    public WeaponDice weapon_two_handed_dice = new();
    public bool weapon_is_versatile;
    public bool weapon_uses_two_hands;
    public StringName weapon_physical_damage_tag = "";
    public BattleStringNameIntMap cooldowns = new();
    public int last_turn_tu = -1;
    private BattleStatusEffectCollection _statusEffects = new();
    private BattleStatusEffectCollection StatusEffectCollection
    {
        get => _statusEffects;
        set => _statusEffects = value ?? new();
    }
    public BattleStringNameIntMap per_battle_charges = new();
    public BattleStringNameIntMap per_turn_charges = new();
    public BattleStringNameIntMap per_turn_charge_limits = new();
    public BattleStringNameIntMap fumble_protection_used = new();
    public bool death_ward_consumed_this_battle;
    internal BattlePendingCastState pending_cast;
    internal bool turn_casting_exhausted;
    // M2 temporal：time_slow 进度获取率的 runtime-only 余数累加器，不进入 save payload。
    internal int action_progress_rate_remainder;
    internal int cast_progress_rate_remainder;

    public BattleUnitState()
    {
        RefreshFootprint();
    }

    internal bool HasPendingCast() => pending_cast != null;

    internal bool IsCasting() => is_alive && pending_cast != null;

    internal void SetPendingCast(BattlePendingCastState pendingCast)
    {
        pending_cast = pendingCast?.Clone();
    }

    internal BattlePendingCastState ClearPendingCast()
    {
        BattlePendingCastState pendingCast = pending_cast;
        pending_cast = null;
        return pendingCast;
    }

    internal void ClearCastingTurnFlags()
    {
        turn_casting_exhausted = false;
    }

    public void SetAnchorCoord(Vector2I anchor_coord)
    {
        coord = anchor_coord;
        RefreshFootprint();
    }

    public void RefreshFootprint()
    {
        footprint_size = GetFootprintSizeForBodySize(body_size);
        occupied_coords = BuildOccupiedCoords(coord, footprint_size);
    }

    public bool OccupiesCoord(Vector2I target_coord)
    {
        return occupied_coords.Contains(target_coord);
    }

    public bool HasMovementTag(StringName tag)
    {
        return movement_tags.Contains(tag);
    }

    public int GetCurrentHp() => current_hp;

    public int GetCurrentMp() => current_mp;

    public int GetCurrentStamina() => current_stamina;

    public int GetCurrentAura() => current_aura;

    public int GetCurrentAp() => current_ap;

    public int GetCurrentMovePoints() => current_move_points;

    public bool IsAlive() => is_alive;

    public void SetCurrentHp(int value)
    {
        current_hp = Math.Max(value, 0);
        is_alive = current_hp > 0;
    }

    public void SetCurrentHpClamped(int value, int hpMax)
    {
        int normalizedMax = Math.Max(hpMax, 0);
        SetCurrentHp(normalizedMax > 0 ? Math.Clamp(value, 0, normalizedMax) : 0);
    }

    public int ApplyHpDamage(int damage)
    {
        int previousHp = current_hp;
        SetCurrentHp(current_hp - Math.Max(damage, 0));
        return previousHp - current_hp;
    }

    public int ApplyHealing(int amount, int hpMax)
    {
        int previousHp = current_hp;
        SetCurrentHpClamped(current_hp + Math.Max(amount, 0), hpMax);
        return current_hp - previousHp;
    }

    public void MarkDead()
    {
        current_hp = 0;
        is_alive = false;
    }

    public void ReviveWithHp(int hp, int hpMax)
    {
        int normalizedMax = Math.Max(hpMax, 1);
        SetCurrentHpClamped(Math.Max(hp, 1), normalizedMax);
    }

    public void SetCurrentMp(int value)
    {
        current_mp = Math.Max(value, 0);
    }

    public void SetCurrentStamina(int value)
    {
        current_stamina = Math.Max(value, 0);
    }

    public void SetCurrentAura(int value)
    {
        current_aura = Math.Max(value, 0);
    }

    public void SetCurrentAp(int value)
    {
        current_ap = Math.Max(value, 0);
    }

    public void SetCurrentMovePoints(int value)
    {
        current_move_points = Math.Max(value, 0);
    }

    public void SetCombatResources(
        int hp,
        int mp,
        int stamina,
        int aura,
        int ap,
        int movePoints
    )
    {
        SetCurrentHp(hp);
        SetCurrentMp(mp);
        SetCurrentStamina(stamina);
        SetCurrentAura(aura);
        SetCurrentAp(ap);
        SetCurrentMovePoints(movePoints);
    }

    internal void RestoreCombatResourceProjection(
        int hp,
        int mp,
        int stamina,
        int aura,
        int ap,
        int movePoints,
        bool alive
    )
    {
        current_hp = Math.Max(hp, 0);
        current_mp = Math.Max(mp, 0);
        current_stamina = Math.Max(stamina, 0);
        current_aura = Math.Max(aura, 0);
        current_ap = Math.Max(ap, 0);
        current_move_points = Math.Max(movePoints, 0);
        is_alive = alive;
    }

    internal void ClampCombatResources(BattleResourceCaps caps)
    {
        SetCurrentHpClamped(current_hp, caps.HpMax);
        current_mp = ClampResource(current_mp, caps.MpMax);
        current_stamina = ClampResource(current_stamina, caps.StaminaMax);
        current_aura = ClampResource(current_aura, caps.AuraMax);
        current_ap = ClampResource(current_ap, caps.ApMax);
        current_move_points = ClampResource(current_move_points, caps.MovePointMax);
    }

    internal void SpendSkillCosts(
        SkillCostTransaction costs,
        bool includeAp = true,
        bool includeCooldown = true
    )
    {
        if (costs == null)
        {
            return;
        }
        if (includeAp)
            SetCurrentAp(current_ap - costs.ApCost);
        SetCurrentMp(current_mp - costs.MpCost);
        SetCurrentStamina(current_stamina - costs.StaminaCost);
        SetCurrentAura(current_aura - costs.AuraCost);
        if (includeCooldown && costs.SkillId != "" && costs.CooldownTurns > 0)
            SetCooldownTyped(costs.SkillId, costs.CooldownTurns);
    }

    internal void RefundSkillCosts(SkillCostTransaction costs, BattleResourceCaps caps)
    {
        if (costs == null)
        {
            return;
        }
        SetCurrentMp(Math.Min(current_mp + Math.Max(costs.MpCost, 0), Math.Max(caps.MpMax, 0)));
        SetCurrentStamina(
            Math.Min(current_stamina + Math.Max(costs.StaminaCost, 0), Math.Max(caps.StaminaMax, 0))
        );
        SetCurrentAura(
            Math.Min(current_aura + Math.Max(costs.AuraCost, 0), Math.Max(caps.AuraMax, 0))
        );
    }

    internal void RefundSkillResources(int mp, int stamina, int aura, BattleResourceCaps caps)
    {
        SetCurrentMp(Math.Min(current_mp + Math.Max(mp, 0), Math.Max(caps.MpMax, 0)));
        SetCurrentStamina(
            Math.Min(current_stamina + Math.Max(stamina, 0), Math.Max(caps.StaminaMax, 0))
        );
        SetCurrentAura(Math.Min(current_aura + Math.Max(aura, 0), Math.Max(caps.AuraMax, 0)));
    }

    public IReadOnlyList<Vector2I> GetOccupiedCoordsTyped()
    {
        var results = new List<Vector2I>();
        foreach (Vector2I occupiedCoord in occupied_coords ?? new GVector2IArray())
            results.Add(occupiedCoord);
        return results;
    }

    public bool SetBodySizeCategory(StringName category)
    {
        if (!IsValidBodySizeCategory(category))
        {
            return false;
        }
        body_size_category = category;
        body_size = GetBodySizeForCategory(category);
        RefreshFootprint();
        return true;
    }

    public bool SetBodySizeProjection(int size)
    {
        if (!IsValidBodySize(size))
        {
            return false;
        }
        body_size = size;
        body_size_category = GetBodySizeCategoryForSize(size);
        RefreshFootprint();
        return true;
    }

    internal void RestoreBodyShapeProjection(
        StringName category,
        int size,
        Vector2I footprint,
        GVector2IArray occupiedCoords
    )
    {
        body_size_category = IsValidBodySizeCategory(category)
            ? category
            : GetBodySizeCategoryForSize(Math.Clamp(size, BodySizeSmall, BodySizeBoss));
        body_size = IsValidBodySize(size) ? size : GetBodySizeForCategory(body_size_category);
        footprint_size = footprint;
        occupied_coords = DuplicateVector2IArray(occupiedCoords);
    }

    public void NormalizeBodySizeProjection()
    {
        if (BodySizeMatchesCategory(body_size_category, body_size))
        {
            RefreshFootprint();
            return;
        }
        throw new InvalidOperationException(
            $"BattleUnitState body_size/body_size_category 不一致: " +
            $"body_size={body_size}, body_size_category='{body_size_category}'。 " +
            $"请检查数据构造路径是否绕过 SetBodySizeCategory()。"
        );
    }

    public bool HasStatusEffect(StringName status_id)
    {
        return GetStatusEffect(status_id) != null;
    }

    public bool HasShield()
    {
        return current_shield_hp > 0 && shield_max_hp > 0 && shield_duration > 0;
    }

    public int GetAuraMax()
    {
        return attribute_snapshot?.GetValue("aura_max") ?? 0;
    }

    public void SyncDefaultCombatResourceUnlocks()
    {
        foreach (StringName resourceId in CombatResourceIds.DefaultUnlocked)
        {
            UnlockCombatResource(resourceId);
        }
    }

    public bool HasCombatResourceUnlocked(StringName resource_id)
    {
        return unlocked_combat_resource_ids.Contains(resource_id);
    }

    internal int GetKnownSkillLevelTyped(StringName skillId, int fallback = 0)
    {
        if (IsEmpty(skillId) || known_skill_level_map == null || !known_skill_level_map.ContainsKey(skillId))
        {
            return fallback;
        }

        return known_skill_level_map.Get(skillId, fallback);
    }

    internal bool HasKnownSkillLevelTyped(StringName skillId)
    {
        if (IsEmpty(skillId) || known_skill_level_map == null || !known_skill_level_map.ContainsKey(skillId))
        {
            return false;
        }

        return true;
    }

    internal int GetCooldownTyped(StringName skillId, int fallback = 0)
    {
        if (IsEmpty(skillId) || cooldowns == null || !cooldowns.ContainsKey(skillId))
        {
            return fallback;
        }

        return cooldowns.Get(skillId, fallback);
    }

    internal void SetCooldownTyped(StringName skillId, int value)
    {
        if (IsEmpty(skillId))
        {
            return;
        }

        int normalizedValue = Math.Max(value, 0);
        if (normalizedValue <= 0)
        {
            cooldowns.Remove(skillId);
            return;
        }

        cooldowns.Put(skillId, normalizedValue);
    }

    internal void SetCooldownsTyped(IReadOnlyDictionary<StringName, int> values)
    {
        cooldowns = new BattleStringNameIntMap();
        if (values == null)
        {
            return;
        }

        foreach (KeyValuePair<StringName, int> entry in values)
        {
            if (IsEmpty(entry.Key))
            {
                continue;
            }

            int normalizedValue = Math.Max(entry.Value, 0);
            if (normalizedValue > 0)
            {
                cooldowns.Put(entry.Key, normalizedValue);
            }
        }
    }

    internal int GetPerBattleChargeTyped(StringName chargeKey, int fallback = 0)
    {
        if (IsEmpty(chargeKey) || per_battle_charges == null || !per_battle_charges.ContainsKey(chargeKey))
        {
            return fallback;
        }

        return per_battle_charges.Get(chargeKey, fallback);
    }

    internal bool HasPerBattleChargeTyped(StringName chargeKey)
    {
        return !IsEmpty(chargeKey)
            && per_battle_charges != null
            && per_battle_charges.ContainsKey(chargeKey);
    }

    internal void SetPerBattleChargeTyped(StringName chargeKey, int value)
    {
        if (IsEmpty(chargeKey))
        {
            return;
        }

        per_battle_charges.Put(chargeKey, Math.Max(value, 0));
    }

    internal int GetPerTurnChargeTyped(StringName chargeKey, int fallback = 0)
    {
        if (IsEmpty(chargeKey) || per_turn_charges == null || !per_turn_charges.ContainsKey(chargeKey))
        {
            return fallback;
        }

        return per_turn_charges.Get(chargeKey, fallback);
    }

    internal bool HasPerTurnChargeTyped(StringName chargeKey)
    {
        return !IsEmpty(chargeKey)
            && per_turn_charges != null
            && per_turn_charges.ContainsKey(chargeKey);
    }

    internal void SetPerTurnChargeTyped(StringName chargeKey, int value)
    {
        if (IsEmpty(chargeKey))
        {
            return;
        }

        per_turn_charges.Put(chargeKey, Math.Max(value, 0));
    }

    internal int GetPerTurnChargeLimitTyped(StringName chargeKey, int fallback = 0)
    {
        if (
            IsEmpty(chargeKey)
            || per_turn_charge_limits == null
            || !per_turn_charge_limits.ContainsKey(chargeKey)
        )
        {
            return fallback;
        }

        return per_turn_charge_limits.Get(chargeKey, fallback);
    }

    internal bool HasPerTurnChargeLimitTyped(StringName chargeKey)
    {
        return !IsEmpty(chargeKey)
            && per_turn_charge_limits != null
            && per_turn_charge_limits.ContainsKey(chargeKey);
    }

    internal void SetPerTurnChargeLimitTyped(StringName chargeKey, int value)
    {
        if (IsEmpty(chargeKey))
        {
            return;
        }

        per_turn_charge_limits.Put(chargeKey, Math.Max(value, 0));
    }

    internal int GetFumbleProtectionUsedTyped(StringName skillId, int fallback = 0)
    {
        if (
            IsEmpty(skillId)
            || fumble_protection_used == null
            || !fumble_protection_used.ContainsKey(skillId)
        )
        {
            return fallback;
        }

        return fumble_protection_used.Get(skillId, fallback);
    }

    internal void SetFumbleProtectionUsedTyped(StringName skillId, int value)
    {
        if (IsEmpty(skillId))
        {
            return;
        }

        fumble_protection_used.Put(skillId, Math.Max(value, 0));
    }

    internal Dictionary<StringName, int> GetKnownSkillLevelsTyped()
    {
        return known_skill_level_map?.ToTypedDictionary() ?? new Dictionary<StringName, int>();
    }

    internal Dictionary<StringName, int> GetKnownSkillLockHitBonusesTyped()
    {
        return known_skill_lock_hit_bonus_map?.ToTypedDictionary()
            ?? new Dictionary<StringName, int>();
    }

    internal List<StringName> GetKnownActiveSkillIdsTyped()
    {
        return CopyStringNameListTyped(known_active_skill_ids);
    }

    internal bool KnowsActiveSkill(StringName skillId)
    {
        return !IsEmpty(skillId)
            && known_active_skill_ids != null
            && known_active_skill_ids.Contains(skillId);
    }

    internal void SetKnownActiveSkillIds(IEnumerable<StringName> skillIds)
    {
        known_active_skill_ids = new GStringNameArray();
        if (skillIds == null)
        {
            return;
        }

        HashSet<StringName> seen = new();
        foreach (StringName skillId in skillIds)
        {
            StringName normalized = ToStringName(skillId);
            if (IsEmpty(normalized) || !seen.Add(normalized))
            {
                continue;
            }
            known_active_skill_ids.Add(normalized);
        }
    }

    internal void AddKnownActiveSkill(StringName skillId)
    {
        StringName normalized = ToStringName(skillId);
        if (IsEmpty(normalized))
        {
            return;
        }
        known_active_skill_ids ??= new GStringNameArray();
        if (!known_active_skill_ids.Contains(normalized))
        {
            known_active_skill_ids.Add(normalized);
        }
    }

    internal void SetKnownSkillLevelsTyped(
        IReadOnlyDictionary<StringName, int> values,
        bool preserveZero = false
    )
    {
        known_skill_level_map = new BattleStringNameIntMap();
        if (values == null)
        {
            return;
        }

        foreach (KeyValuePair<StringName, int> entry in values)
        {
            SetKnownSkillLevelTyped(entry.Key, entry.Value, preserveZero);
        }
    }

    internal void SetKnownSkillLevelTyped(StringName skillId, int level, bool preserveZero = false)
    {
        if (IsEmpty(skillId))
        {
            return;
        }
        int normalizedLevel = Math.Max(level, 0);
        if (normalizedLevel <= 0 && !preserveZero)
        {
            known_skill_level_map.Remove(skillId);
            return;
        }
        known_skill_level_map.Put(skillId, normalizedLevel);
    }

    internal void RemoveKnownSkillLevelTyped(StringName skillId)
    {
        if (!IsEmpty(skillId))
            known_skill_level_map?.Remove(skillId);
    }

    internal void SetKnownSkillLockHitBonusesTyped(IReadOnlyDictionary<StringName, int> values)
    {
        known_skill_lock_hit_bonus_map = new BattleStringNameIntMap();
        if (values == null)
        {
            return;
        }

        foreach (KeyValuePair<StringName, int> entry in values)
        {
            StringName skillId = ToStringName(entry.Key);
            int normalizedBonus = Math.Max(entry.Value, 0);
            if (IsEmpty(skillId) || normalizedBonus <= 0)
            {
                continue;
            }
            known_skill_lock_hit_bonus_map.Put(skillId, normalizedBonus);
        }
    }

    internal void SetVersatilityPick(StringName value)
    {
        versatility_pick = value;
    }

    internal int GetKnownSkillLockHitBonusTyped(StringName skillId, int fallback = 0)
    {
        if (IsEmpty(skillId) || known_skill_lock_hit_bonus_map == null)
        {
            return fallback;
        }

        if (known_skill_lock_hit_bonus_map.TryGetValue(skillId, out int value))
        {
            return value;
        }
        return fallback;
    }

    internal Dictionary<StringName, int> GetCooldownsTyped()
    {
        return cooldowns?.ToTypedDictionary() ?? new Dictionary<StringName, int>();
    }

    internal Dictionary<StringName, int> GetPerBattleChargesTyped()
    {
        return per_battle_charges?.ToTypedDictionary() ?? new Dictionary<StringName, int>();
    }

    internal Dictionary<StringName, int> GetPerTurnChargesTyped()
    {
        return per_turn_charges?.ToTypedDictionary() ?? new Dictionary<StringName, int>();
    }

    internal Dictionary<StringName, int> GetPerTurnChargeLimitsTyped()
    {
        return per_turn_charge_limits?.ToTypedDictionary()
            ?? new Dictionary<StringName, int>();
    }

    internal Dictionary<StringName, int> GetFumbleProtectionUsedTyped()
    {
        return fumble_protection_used?.ToTypedDictionary()
            ?? new Dictionary<StringName, int>();
    }

    internal Dictionary<StringName, StringName> GetDamageResistancesTyped()
    {
        return damage_resistances?.ToTypedDictionary()
            ?? new Dictionary<StringName, StringName>();
    }

    internal WeaponDice GetWeaponOneHandedDiceTyped()
    {
        return CopyWeaponDiceTyped(weapon_one_handed_dice);
    }

    internal WeaponDice GetWeaponTwoHandedDiceTyped()
    {
        return CopyWeaponDiceTyped(weapon_two_handed_dice);
    }

    internal WeaponDice GetActiveWeaponDiceTyped()
    {
        return weapon_uses_two_hands ? GetWeaponTwoHandedDiceTyped() : GetWeaponOneHandedDiceTyped();
    }

    public bool UnlockCombatResource(StringName resource_id)
    {
        if (IsEmpty(resource_id))
        {
            return false;
        }
        if (!IsValidCombatResourceId(resource_id))
        {
            return false;
        }
        if (unlocked_combat_resource_ids.Contains(resource_id))
        {
            return false;
        }
        unlocked_combat_resource_ids.Add(resource_id);
        return true;
    }

    public void SetUnlockedCombatResourceIds(GStringNameArray resource_ids)
    {
        unlocked_combat_resource_ids = new GStringNameArray();
        if (resource_ids != null)
        {
            foreach (StringName resourceId in resource_ids)
            {
                UnlockCombatResource(resourceId);
            }
        }
        SyncDefaultCombatResourceUnlocks();
    }

    public void ClearShield()
    {
        current_shield_hp = 0;
        shield_max_hp = 0;
        shield_duration = -1;
        shield_family = "";
        shield_source_unit_id = "";
        shield_source_skill_id = "";
    }

    public void NormalizeShieldState()
    {
        if (current_shield_hp <= 0 || shield_max_hp <= 0 || shield_duration <= 0)
        {
            ClearShield();
            return;
        }
        shield_max_hp = Math.Max(shield_max_hp, 1);
        current_shield_hp = Math.Clamp(current_shield_hp, 0, shield_max_hp);
        if (current_shield_hp <= 0)
        {
            ClearShield();
        }
    }

    public EquipmentState GetEquipmentView()
    {
        if (equipment_view == null)
        {
            equipment_view = NewEquipmentState();
        }
        return equipment_view;
    }

    public void SetEquipmentView(EquipmentState source_equipment_state)
    {
        equipment_view_initialized = true;
        EquipmentState nextEquipmentView =
            source_equipment_state?.DuplicateState() ?? NewEquipmentState();
        GodotRefCountedDisposer.DisposeIfValid(equipment_view);
        equipment_view = nextEquipmentView;
    }

    public void ClearWeaponProjection()
    {
        weapon_profile_kind = WeaponProfileKindNone;
        weapon_item_id = "";
        weapon_profile_type_id = "";
        weapon_family = "";
        weapon_current_grip = WeaponGripNone;
        weapon_attack_range = 0;
        weapon_one_handed_dice = new WeaponDice();
        weapon_two_handed_dice = new WeaponDice();
        weapon_is_versatile = false;
        weapon_uses_two_hands = false;
        weapon_physical_damage_tag = "";
    }

    public void SetUnarmedWeaponProjection(
        StringName damage_tag = default,
        GDictionary dice = null,
        int attack_range = 1
    )
    {
        SetUnarmedWeaponProjectionTyped(damage_tag, WeaponDice.FromDictionary(dice), attack_range);
    }

    internal void SetUnarmedWeaponProjectionTyped(
        StringName damageTag = default,
        WeaponDice dice = null,
        int attackRange = 1
    )
    {
        if (IsEmpty(damageTag))
        {
            damageTag = "physical_blunt";
        }
        dice = dice == null || dice.IsEmpty()
            ? new WeaponDice
            {
                dice_count = 1,
                dice_sides = 4,
                flat_bonus = 0,
            }
            : dice;
        ApplyWeaponProjectionTyped(
            new WeaponProjection
            {
                weapon_profile_kind = WeaponProfileKindUnarmed,
                weapon_profile_type_id = "unarmed",
                weapon_family = "unarmed",
                weapon_current_grip = WeaponGripOneHanded,
                weapon_attack_range = attackRange,
                weapon_one_handed_dice = dice,
                weapon_uses_two_hands = false,
                weapon_physical_damage_tag = damageTag,
            }
        );
    }

    public void SetNaturalWeaponProjection(
        StringName profile_type_id,
        StringName damage_tag,
        int attack_range,
        GDictionary dice = null,
        StringName family = default
    )
    {
        SetNaturalWeaponProjectionTyped(
            profile_type_id,
            damage_tag,
            attack_range,
            WeaponDice.FromDictionary(dice),
            family
        );
    }

    internal void SetNaturalWeaponProjectionTyped(
        StringName profileTypeId,
        StringName damageTag,
        int attackRange,
        WeaponDice dice = null,
        StringName family = default
    )
    {
        ApplyWeaponProjectionTyped(
            new WeaponProjection
            {
                weapon_profile_kind = WeaponProfileKindNatural,
                weapon_profile_type_id = !IsEmpty(profileTypeId) ? profileTypeId : "natural_weapon",
                weapon_family = family,
                weapon_current_grip = attackRange > 0 ? WeaponGripOneHanded : WeaponGripNone,
                weapon_attack_range = attackRange,
                weapon_one_handed_dice = dice ?? new WeaponDice(),
                weapon_uses_two_hands = false,
                weapon_physical_damage_tag = damageTag,
            }
        );
    }

    internal void ApplyWeaponProjectionTyped(WeaponProjection projection)
    {
        if (projection == null || projection.IsEmpty())
        {
            ClearWeaponProjection();
            return;
        }
        weapon_profile_kind = NormalizeWeaponProfileKind(
            ToStringName(projection.weapon_profile_kind)
        );
        weapon_item_id = ToStringName(projection.weapon_item_id);
        weapon_profile_type_id = ToStringName(projection.weapon_profile_type_id);
        weapon_family = ToStringName(projection.weapon_family);
        weapon_current_grip = NormalizeWeaponGrip(ToStringName(projection.weapon_current_grip));
        weapon_attack_range = Math.Max(projection.weapon_attack_range, 0);
        weapon_one_handed_dice = NormalizeWeaponDice(projection.weapon_one_handed_dice);
        weapon_two_handed_dice = NormalizeWeaponDice(projection.weapon_two_handed_dice);
        weapon_is_versatile = projection.weapon_is_versatile;
        weapon_uses_two_hands = projection.weapon_uses_two_hands;
        if (weapon_uses_two_hands)
        {
            weapon_current_grip = WeaponGripTwoHanded;
        }
        else if (weapon_current_grip == WeaponGripTwoHanded)
        {
            weapon_current_grip =
                HasWeaponDice(weapon_one_handed_dice) ? WeaponGripOneHanded : WeaponGripNone;
        }
        weapon_physical_damage_tag = ToStringName(projection.weapon_physical_damage_tag);
        if (weapon_profile_kind == WeaponProfileKindNone)
        {
            ClearWeaponProjection();
            return;
        }
        if (weapon_attack_range <= 0)
        {
            weapon_current_grip = WeaponGripNone;
            weapon_uses_two_hands = false;
        }
    }

    public int GetWeaponAttackRange()
    {
        return Math.Max(weapon_attack_range, 0);
    }

    public BattleStatusEffectState GetStatusEffect(StringName status_id)
    {
        return _statusEffects.Get(status_id);
    }

    public List<BattleStatusEffectState> GetStatusEffectsTyped()
    {
        return _statusEffects.GetStatusEffects();
    }

    public List<StringName> GetSortedStatusEffectIdsTyped()
    {
        return _statusEffects.GetSortedStatusEffectIds();
    }

    public void SetStatusEffect(BattleStatusEffectState effect_state)
    {
        if (effect_state == null || effect_state.IsEmpty())
        {
            return;
        }
        _statusEffects.Set(effect_state);
    }

    public void EraseStatusEffect(StringName status_id)
    {
        StringName normalized = ToStringName(status_id);
        if (!IsEmpty(normalized))
        {
            _statusEffects.Remove(normalized);
        }
    }

    public void ClearStatusEffects()
    {
        _statusEffects.Clear();
    }

    internal IReadOnlyDictionary<StringName, BattleStatusEffectState> CaptureStatusEffectsTyped()
    {
        var results = new Dictionary<StringName, BattleStatusEffectState>();
        foreach (StringName statusId in GetSortedStatusEffectIdsTyped())
        {
            BattleStatusEffectState effectState = GetStatusEffect(statusId);
            if (effectState != null)
                results[statusId] = effectState.DuplicateState();
        }
        return results;
    }

    public void ResetPerTurnCharges()
    {
        per_turn_charges.Clear();
        foreach (KeyValuePair<StringName, int> entry in GetPerTurnChargeLimitsTyped())
        {
            StringName chargeKey = entry.Key;
            int chargeLimit = Math.Max(entry.Value, 0);
            if (IsEmpty(chargeKey) || chargeLimit <= 0)
            {
                continue;
            }
            SetPerTurnChargeTyped(chargeKey, chargeLimit);
        }
    }

    public BattleUnitState clone()
    {
        NormalizeBodySizeProjection();
        NormalizeShieldState();
        NormalizeWeaponProjection();
        SyncDefaultCombatResourceUnlocks();

        return new BattleUnitState
        {
            unit_id = unit_id,
            source_member_id = source_member_id,
            enemy_template_id = enemy_template_id,
            display_name = display_name,
            faction_id = faction_id,
            control_mode = control_mode,
            ai_brain_id = ai_brain_id,
            ai_state_id = ai_state_id,
            coord = coord,
            body_size = body_size,
            body_size_category = body_size_category,
            footprint_size = footprint_size,
            occupied_coords = DuplicateVector2IArray(occupied_coords),
            is_alive = is_alive,
            attribute_snapshot = DuplicateAttributeSnapshot(attribute_snapshot),
            equipment_view = GetEquipmentView()?.DuplicateState() ?? NewEquipmentState(),
            equipment_view_initialized = true,
            current_hp = current_hp,
            current_mp = current_mp,
            current_stamina = current_stamina,
            current_aura = current_aura,
            current_ap = current_ap,
            current_move_points = current_move_points,
            unlocked_combat_resource_ids = DuplicateStringNameArray(
                unlocked_combat_resource_ids
            ),
            stamina_recovery_progress = stamina_recovery_progress,
            is_resting = is_resting,
            has_taken_action_this_turn = has_taken_action_this_turn,
            has_moved_this_turn = has_moved_this_turn,
            can_use_locked_move_points_this_turn = can_use_locked_move_points_this_turn,
            current_shield_hp = current_shield_hp,
            shield_max_hp = shield_max_hp,
            shield_duration = shield_duration,
            shield_family = shield_family,
            shield_source_unit_id = shield_source_unit_id,
            shield_source_skill_id = shield_source_skill_id,
            action_progress = action_progress,
            action_threshold = action_threshold,
            known_active_skill_ids = DuplicateStringNameArray(known_active_skill_ids),
            known_skill_level_map = known_skill_level_map?.Clone() ?? new BattleStringNameIntMap(),
            known_skill_lock_hit_bonus_map =
                known_skill_lock_hit_bonus_map?.Clone() ?? new BattleStringNameIntMap(),
            movement_tags = DuplicateStringNameArray(movement_tags),
            vision_tags = DuplicateStringNameArray(vision_tags),
            proficiency_tags = DuplicateStringNameArray(proficiency_tags),
            save_advantage_tags = DuplicateStringNameArray(save_advantage_tags),
            damage_resistances = damage_resistances?.Clone() ?? new BattleStringNameMap(),
            effective_trait_instances = DuplicateEffectiveTraitInstances(effective_trait_instances),
            effective_trait_ids = DeriveEffectiveTraitIdsFromInstances(effective_trait_instances),
            versatility_pick = versatility_pick,
            weapon_profile_kind = weapon_profile_kind,
            weapon_item_id = weapon_item_id,
            weapon_profile_type_id = weapon_profile_type_id,
            weapon_family = weapon_family,
            weapon_current_grip = weapon_current_grip,
            weapon_attack_range = weapon_attack_range,
            weapon_one_handed_dice = CopyWeaponDiceTyped(weapon_one_handed_dice),
            weapon_two_handed_dice = CopyWeaponDiceTyped(weapon_two_handed_dice),
            weapon_is_versatile = weapon_is_versatile,
            weapon_uses_two_hands = weapon_uses_two_hands,
            weapon_physical_damage_tag = weapon_physical_damage_tag,
            cooldowns = cooldowns?.Clone() ?? new BattleStringNameIntMap(),
            last_turn_tu = last_turn_tu,
            StatusEffectCollection = _statusEffects.DuplicateState(),
            per_battle_charges = per_battle_charges?.Clone() ?? new BattleStringNameIntMap(),
            per_turn_charges = per_turn_charges?.Clone() ?? new BattleStringNameIntMap(),
            per_turn_charge_limits =
                per_turn_charge_limits?.Clone() ?? new BattleStringNameIntMap(),
            fumble_protection_used =
                fumble_protection_used?.Clone() ?? new BattleStringNameIntMap(),
            death_ward_consumed_this_battle = death_ward_consumed_this_battle,
            pending_cast = pending_cast?.Clone(),
            turn_casting_exhausted = turn_casting_exhausted,
            action_progress_rate_remainder = action_progress_rate_remainder,
            cast_progress_rate_remainder = cast_progress_rate_remainder,
        };
    }

    public static Vector2I GetFootprintSizeForBodySize(int size_value)
    {
        return GetFootprintForBodySize(Math.Max(size_value, BodySizeSmall));
    }

    public GDictionary ToDictionary()
    {
        NormalizeBodySizeProjection();
        NormalizeShieldState();
        NormalizeWeaponProjection();
        SyncDefaultCombatResourceUnlocks();

        GDictionary statusPayloads = _statusEffects.ToDictionary();

        return new GDictionary
        {
            ["unit_id"] = unit_id.ToString(),
            ["source_member_id"] = source_member_id.ToString(),
            ["enemy_template_id"] = enemy_template_id.ToString(),
            ["display_name"] = display_name,
            ["faction_id"] = faction_id.ToString(),
            ["control_mode"] = control_mode.ToString(),
            ["ai_brain_id"] = ai_brain_id.ToString(),
            ["ai_state_id"] = ai_state_id.ToString(),
            // ai_blackboard is runtime-only and not serialized
            ["coord"] = coord,
            ["body_size"] = body_size,
            ["body_size_category"] = body_size_category.ToString(),
            ["footprint_size"] = footprint_size,
            ["occupied_coords"] = DuplicateVector2IArray(occupied_coords),
            ["is_alive"] = is_alive,
            ["attribute_snapshot"] = AttributeSnapshotToDict(attribute_snapshot),
            ["equipment_view"] = EquipmentViewToDict(GetEquipmentView()),
            ["current_hp"] = current_hp,
            ["current_mp"] = current_mp,
            ["current_stamina"] = current_stamina,
            ["current_aura"] = current_aura,
            ["aura_max"] = GetAuraMax(),
            ["current_ap"] = current_ap,
            ["current_move_points"] = current_move_points,
            ["unlocked_combat_resource_ids"] = StringNameArrayToStrings(
                unlocked_combat_resource_ids
            ),
            ["stamina_recovery_progress"] = stamina_recovery_progress,
            ["is_resting"] = is_resting,
            ["has_taken_action_this_turn"] = has_taken_action_this_turn,
            ["can_use_locked_move_points_this_turn"] = can_use_locked_move_points_this_turn,
            ["current_shield_hp"] = current_shield_hp,
            ["shield_max_hp"] = shield_max_hp,
            ["shield_duration"] = shield_duration,
            ["shield_family"] = shield_family.ToString(),
            ["shield_source_unit_id"] = shield_source_unit_id.ToString(),
            ["shield_source_skill_id"] = shield_source_skill_id.ToString(),
            ["action_progress"] = action_progress,
            ["action_threshold"] = action_threshold,
            ["known_active_skill_ids"] = StringNameArrayToStrings(known_active_skill_ids),
            ["known_skill_level_map"] =
                known_skill_level_map?.ProjectStringKeyPayload() ?? new GDictionary(),
            ["known_skill_lock_hit_bonus_map"] =
                known_skill_lock_hit_bonus_map?.ProjectStringKeyPayload() ?? new GDictionary(),
            ["movement_tags"] = StringNameArrayToStrings(movement_tags),
            ["vision_tags"] = StringNameArrayToStrings(vision_tags),
            ["proficiency_tags"] = StringNameArrayToStrings(proficiency_tags),
            ["save_advantage_tags"] = StringNameArrayToStrings(save_advantage_tags),
            ["damage_resistances"] =
                damage_resistances?.ProjectStringKeyPayload() ?? new GDictionary(),
            ["effective_trait_instances"] = EffectiveTraitInstancesToPayloadArray(
                effective_trait_instances
            ),
            ["effective_trait_ids"] = StringNameArrayToStrings(
                DeriveEffectiveTraitIdsFromInstances(effective_trait_instances)
            ),
            ["versatility_pick"] = versatility_pick.ToString(),
            ["weapon_profile_kind"] = weapon_profile_kind.ToString(),
            ["weapon_item_id"] = weapon_item_id.ToString(),
            ["weapon_profile_type_id"] = weapon_profile_type_id.ToString(),
            ["weapon_family"] = weapon_family.ToString(),
            ["weapon_current_grip"] = weapon_current_grip.ToString(),
            ["weapon_attack_range"] = weapon_attack_range,
            ["weapon_one_handed_dice"] = WeaponDiceToDictionary(weapon_one_handed_dice),
            ["weapon_two_handed_dice"] = WeaponDiceToDictionary(weapon_two_handed_dice),
            ["weapon_is_versatile"] = weapon_is_versatile,
            ["weapon_uses_two_hands"] = weapon_uses_two_hands,
            ["weapon_physical_damage_tag"] = weapon_physical_damage_tag.ToString(),
            ["cooldowns"] = cooldowns?.ProjectPayload() ?? new GDictionary(),
            ["last_turn_tu"] = last_turn_tu,
            ["status_effects"] = statusPayloads,
        };
    }

    public static BattleUnitState FromDictionary(GDictionary payload)
    {
        if (payload == null)
            return null;
        if (payload.Count == 0)
        {
            return null;
        }
        if (!HasExactFields(payload, ToDictFields))
        {
            return null;
        }

        var coordValue = payload["coord"];
        var bodySizeValue = payload["body_size"];
        var bodySizeCategoryValue = payload["body_size_category"];
        var footprintSizeValue = payload["footprint_size"];
        var occupiedCoordsValue = payload["occupied_coords"];
        if (
            coordValue.VariantType.ToString() != "Vector2I"
            || bodySizeValue.VariantType.ToString() != "Int"
            || footprintSizeValue.VariantType.ToString() != "Vector2I"
        )
        {
            return null;
        }
        int bodySizeInt = bodySizeValue.AsInt32();
        if (bodySizeInt < 1)
        {
            return null;
        }
        if (
            !IsStringNamePayloadType(bodySizeCategoryValue.VariantType.ToString())
            || IsEmpty(ToStringName(bodySizeCategoryValue))
        )
        {
            return null;
        }
        StringName parsedBodySizeCategory = ToStringName(bodySizeCategoryValue);
        if (!IsValidBodySizeCategory(parsedBodySizeCategory))
        {
            return null;
        }
        if (GetBodySizeForCategory(parsedBodySizeCategory) != bodySizeInt)
        {
            return null;
        }
        Vector2I expectedFootprint = GetFootprintSizeForBodySize(bodySizeInt);
        GVector2IArray expectedOccupied = BuildOccupiedCoords(
            coordValue.AsVector2I(),
            expectedFootprint
        );
        if (footprintSizeValue.AsVector2I() != expectedFootprint)
        {
            return null;
        }
        if (occupiedCoordsValue.VariantType.ToString() != "Array")
        {
            return null;
        }
        GVector2IArray parsedOccupiedCoords = new();
        foreach (var occupiedCoordValue in occupiedCoordsValue.AsGodotArray())
        {
            if (occupiedCoordValue.VariantType.ToString() != "Vector2I")
            {
                return null;
            }
            parsedOccupiedCoords.Add(occupiedCoordValue.AsVector2I());
        }
        if (!Vector2IArraysEqual(parsedOccupiedCoords, expectedOccupied))
        {
            return null;
        }

        foreach (
            string fieldName in new[] { "unit_id", "display_name", "faction_id", "control_mode" }
        )
        {
            if (
                !IsStringNamePayloadType(payload[fieldName].VariantType.ToString())
                || IsEmpty(ToStringName(payload[fieldName]))
            )
            {
                return null;
            }
        }
        foreach (
            string fieldName in new[]
            {
                "source_member_id",
                "enemy_template_id",
                "ai_brain_id",
                "ai_state_id",
                "shield_family",
                "shield_source_unit_id",
                "shield_source_skill_id",
                "weapon_item_id",
                "weapon_profile_type_id",
                "weapon_family",
                "weapon_physical_damage_tag",
                "versatility_pick",
            }
        )
        {
            if (!IsStringNamePayloadType(payload[fieldName].VariantType.ToString()))
            {
                return null;
            }
        }
        foreach (
            string fieldName in new[]
            {
                "current_hp",
                "current_mp",
                "current_stamina",
                "current_aura",
                "aura_max",
                "current_ap",
                "current_move_points",
                "stamina_recovery_progress",
                "current_shield_hp",
                "shield_max_hp",
                "shield_duration",
                "action_progress",
                "action_threshold",
                "weapon_attack_range",
                "last_turn_tu",
            }
        )
        {
            if (payload[fieldName].VariantType.ToString() != "Int")
            {
                return null;
            }
        }
        if (payload["current_move_points"].AsInt32() < 0)
        {
            return null;
        }
        foreach (
            string fieldName in new[]
            {
                "is_alive",
                "is_resting",
                "has_taken_action_this_turn",
                "can_use_locked_move_points_this_turn",
                "weapon_is_versatile",
                "weapon_uses_two_hands",
            }
        )
        {
            if (payload[fieldName].VariantType.ToString() != "Bool")
            {
                return null;
            }
        }
        foreach (
            string fieldName in new[]
            {
                "attribute_snapshot",
                "equipment_view",
                "weapon_one_handed_dice",
                "weapon_two_handed_dice",
                "cooldowns",
                "known_skill_level_map",
                "known_skill_lock_hit_bonus_map",
                "status_effects",
                "damage_resistances",
            }
        )
        {
            if (payload[fieldName].VariantType.ToString() != "Dictionary")
            {
                return null;
            }
        }

        AttributeSnapshot parsedAttributeSnapshot = AttributeSnapshotFromDictionary(
            payload["attribute_snapshot"].AsGodotDictionary()
        );
        if (parsedAttributeSnapshot == null)
        {
            return null;
        }
        BattleStringNameIntMap parsedKnownSkillLevelMap = BattleStringNameIntMap.FromPayloadOrNull(
            payload["known_skill_level_map"].AsGodotDictionary(),
            true
        );
        if (parsedKnownSkillLevelMap == null)
        {
            return null;
        }
        BattleStringNameIntMap parsedKnownSkillLockHitBonusMap =
            BattleStringNameIntMap.FromPayloadOrNull(
                payload["known_skill_lock_hit_bonus_map"].AsGodotDictionary(),
                true
            );
        if (parsedKnownSkillLockHitBonusMap == null)
        {
            return null;
        }
        foreach (StringName skillId in parsedKnownSkillLockHitBonusMap.Keys)
        {
            if (parsedKnownSkillLockHitBonusMap.Get(skillId) < 0)
            {
                return null;
            }
        }
        GStringNameArray parsedUnlockedResources = _combat_resource_array_from_payload(
            GetArray(payload, "unlocked_combat_resource_ids")
        );
        if (parsedUnlockedResources.Count == 0)
        {
            return null;
        }
        GStringNameArray parsedKnownActiveSkillIds = _unique_string_name_array_from_payload(
            GetArray(payload, "known_active_skill_ids")
        );
        if (parsedKnownActiveSkillIds == null)
        {
            return null;
        }
        GStringNameArray parsedMovementTags = _unique_string_name_array_from_payload(
            GetArray(payload, "movement_tags")
        );
        if (parsedMovementTags == null)
        {
            return null;
        }
        GStringNameArray parsedVisionTags = _unique_string_name_array_from_payload(
            GetArray(payload, "vision_tags")
        );
        if (parsedVisionTags == null)
        {
            return null;
        }
        GStringNameArray parsedProficiencyTags = _unique_string_name_array_from_payload(
            GetArray(payload, "proficiency_tags")
        );
        if (parsedProficiencyTags == null)
        {
            return null;
        }
        GStringNameArray parsedSaveAdvantageTags = _unique_string_name_array_from_payload(
            GetArray(payload, "save_advantage_tags")
        );
        if (parsedSaveAdvantageTags == null)
        {
            return null;
        }
        GEffectiveTraitArray parsedEffectiveTraitInstances = EffectiveTraitInstancesFromPayloadArray(
            GetArray(payload, "effective_trait_instances")
        );
        if (parsedEffectiveTraitInstances == null)
        {
            return null;
        }
        GStringNameArray parsedEffectiveTraitIds = _unique_string_name_array_from_payload(
            GetArray(payload, "effective_trait_ids")
        );
        if (
            parsedEffectiveTraitIds == null
            || !StringNameSetEquals(
                parsedEffectiveTraitIds,
                DeriveEffectiveTraitIdsFromInstances(parsedEffectiveTraitInstances)
            )
        )
        {
            return null;
        }
        BattleStringNameMap parsedDamageResistances = _damage_resistance_map_from_dict(
            payload["damage_resistances"].AsGodotDictionary()
        );
        if (parsedDamageResistances == null)
        {
            return null;
        }

        StringName parsedWeaponProfileKind = ToStringName(payload["weapon_profile_kind"]);
        if (!IsValidWeaponProfileKind(parsedWeaponProfileKind))
        {
            return null;
        }
        StringName parsedWeaponCurrentGrip = ToStringName(payload["weapon_current_grip"]);
        if (!IsValidWeaponGrip(parsedWeaponCurrentGrip))
        {
            return null;
        }
        WeaponDice parsedWeaponOneHandedDice = StrictWeaponDiceFromDictionary(
            payload["weapon_one_handed_dice"].AsGodotDictionary()
        );
        if (parsedWeaponOneHandedDice == null)
        {
            return null;
        }
        WeaponDice parsedWeaponTwoHandedDice = StrictWeaponDiceFromDictionary(
            payload["weapon_two_handed_dice"].AsGodotDictionary()
        );
        if (parsedWeaponTwoHandedDice == null)
        {
            return null;
        }

        EquipmentState parsedEquipmentState = EquipmentFromDict(
            payload["equipment_view"].AsGodotDictionary()
        );
        if (parsedEquipmentState == null)
        {
            return null;
        }
        BattleStatusEffectCollection parsedStatusEffects;
        try
        {
            parsedStatusEffects = BattleStatusEffectCollection.FromDictionary(
                payload["status_effects"].AsGodotDictionary()
            );
        }
        catch (ArgumentException)
        {
            return null;
        }

        BattleUnitState unitState = new()
        {
            unit_id = ToStringName(payload["unit_id"]),
            source_member_id = ToStringName(payload["source_member_id"]),
            enemy_template_id = ToStringName(payload["enemy_template_id"]),
            display_name = payload["display_name"].AsString(),
            faction_id = ToStringName(payload["faction_id"]),
            control_mode = ToStringName(payload["control_mode"]),
            ai_brain_id = ToStringName(payload["ai_brain_id"]),
            ai_state_id = ToStringName(payload["ai_state_id"]),
            ai_blackboard = new BattleAiBlackboard(),
            coord = coordValue.AsVector2I(),
            body_size = bodySizeInt,
            body_size_category = parsedBodySizeCategory,
            footprint_size = footprintSizeValue.AsVector2I(),
            occupied_coords = parsedOccupiedCoords,
            is_alive = ReadBool(payload, "is_alive"),
            attribute_snapshot = parsedAttributeSnapshot,
            equipment_view = parsedEquipmentState,
            equipment_view_initialized = true,
            current_hp = payload["current_hp"].AsInt32(),
            current_mp = payload["current_mp"].AsInt32(),
            current_stamina = payload["current_stamina"].AsInt32(),
            current_aura = payload["current_aura"].AsInt32(),
            current_ap = payload["current_ap"].AsInt32(),
            current_move_points = payload["current_move_points"].AsInt32(),
            unlocked_combat_resource_ids = parsedUnlockedResources,
            stamina_recovery_progress = payload["stamina_recovery_progress"].AsInt32(),
            is_resting = ReadBool(payload, "is_resting"),
            has_taken_action_this_turn = ReadBool(payload, "has_taken_action_this_turn"),
            can_use_locked_move_points_this_turn = ReadBool(
                payload,
                "can_use_locked_move_points_this_turn"
            ),
            current_shield_hp = payload["current_shield_hp"].AsInt32(),
            shield_max_hp = payload["shield_max_hp"].AsInt32(),
            shield_duration = payload["shield_duration"].AsInt32(),
            shield_family = ToStringName(payload["shield_family"]),
            shield_source_unit_id = ToStringName(payload["shield_source_unit_id"]),
            shield_source_skill_id = ToStringName(payload["shield_source_skill_id"]),
            action_progress = payload["action_progress"].AsInt32(),
            action_threshold = payload["action_threshold"].AsInt32(),
            known_active_skill_ids = parsedKnownActiveSkillIds,
            known_skill_level_map = parsedKnownSkillLevelMap,
            known_skill_lock_hit_bonus_map = parsedKnownSkillLockHitBonusMap,
            movement_tags = parsedMovementTags,
            vision_tags = parsedVisionTags,
            proficiency_tags = parsedProficiencyTags,
            save_advantage_tags = parsedSaveAdvantageTags,
            damage_resistances = parsedDamageResistances,
            effective_trait_instances = parsedEffectiveTraitInstances,
            effective_trait_ids = parsedEffectiveTraitIds,
            versatility_pick = ToStringName(payload["versatility_pick"]),
            weapon_profile_kind = parsedWeaponProfileKind,
            weapon_item_id = ToStringName(payload["weapon_item_id"]),
            weapon_profile_type_id = ToStringName(payload["weapon_profile_type_id"]),
            weapon_family = ToStringName(payload["weapon_family"]),
            weapon_current_grip = parsedWeaponCurrentGrip,
            weapon_attack_range = payload["weapon_attack_range"].AsInt32(),
            weapon_one_handed_dice = parsedWeaponOneHandedDice,
            weapon_two_handed_dice = parsedWeaponTwoHandedDice,
            weapon_is_versatile = ReadBool(payload, "weapon_is_versatile"),
            weapon_uses_two_hands = ReadBool(payload, "weapon_uses_two_hands"),
            weapon_physical_damage_tag = ToStringName(payload["weapon_physical_damage_tag"]),
            cooldowns =
                BattleStringNameIntMap.FromPayloadOrNull(
                    payload["cooldowns"].AsGodotDictionary(),
                    false
                ) ?? new BattleStringNameIntMap(),
            last_turn_tu = payload["last_turn_tu"].AsInt32(),
            StatusEffectCollection = parsedStatusEffects,
        };
        unitState.attribute_snapshot.SetValue("aura_max", payload["aura_max"].AsInt32());
        unitState.NormalizeShieldState();
        unitState.RefreshFootprint();
        return unitState;
    }

    private static AttributeSnapshot AttributeSnapshotFromDictionary(GDictionary values)
    {
        if (values == null)
            return null;
        AttributeSnapshot snapshot = NewAttributeSnapshot();
        if (snapshot == null)
        {
            return null;
        }
        foreach (var key in values.Keys)
        {
            if (!IsStringNamePayloadType(key.VariantType.ToString()))
            {
                return null;
            }
            if (values[key].VariantType.ToString() != "Int")
            {
                return null;
            }
            snapshot.SetValue(ToStringName(key), values[key].AsInt32());
        }
        return snapshot;
    }

    private static bool ReadBool(GDictionary payload, string key)
    {
        if (payload == null || !payload.ContainsKey(key))
            return false;
        var value = payload[key];
        return value.VariantType.ToString() == "Bool" && value.AsBool();
    }

    private static GDictionary StatusEffectsFromDictionary(GDictionary values)
    {
        GDictionary results = new();
        if (values == null)
            return null;
        foreach (var statusKey in values.Keys)
        {
            if (
                !IsStringNamePayloadType(statusKey.VariantType.ToString())
                || IsEmpty(ToStringName(statusKey))
            )
            {
                return null;
            }
            var effectValue = values[statusKey];
            if (effectValue.VariantType.ToString() != "Dictionary")
            {
                return null;
            }
            BattleStatusEffectState effectState = BattleStatusEffectState.FromDictionary(
                effectValue.AsGodotDictionary()
            );
            if (effectState == null || effectState.IsEmpty())
            {
                return null;
            }
            if (ToStringName(statusKey) != effectState.status_id)
            {
                return null;
            }
            results[effectState.status_id] = effectState;
        }
        return results;
    }

    private static GVector2IArray BuildOccupiedCoords(Vector2I anchor_coord, Vector2I footprint)
    {
        GVector2IArray results = new();
        for (int y = 0; y < footprint.Y; y++)
        {
            for (int x = 0; x < footprint.X; x++)
            {
                results.Add(anchor_coord + new Vector2I(x, y));
            }
        }
        return results;
    }

    private static bool HasExactFields(GDictionary data, string[] expected_fields)
    {
        if (data.Count != expected_fields.Length)
        {
            return false;
        }
        HashSet<string> expected = new(expected_fields);
        HashSet<string> seen = new();
        foreach (var key in data.Keys)
        {
            if (!IsStringNamePayloadType(key.VariantType.ToString()))
            {
                return false;
            }
            string keyString = key.AsString();
            if (!expected.Contains(keyString) || !seen.Add(keyString))
            {
                return false;
            }
        }
        return seen.Count == expected.Count;
    }

    private static bool IsStringNamePayloadType(string valueType)
    {
        return valueType == "String" || valueType == "StringName";
    }

    private static GDictionary _string_name_int_map_from_dict(
        GDictionary values,
        bool require_non_empty_key
    )
    {
        if (values == null)
        {
            return null;
        }
        GDictionary result = new();
        foreach (var key in values.Keys)
        {
            if (!IsStringNamePayloadType(key.VariantType.ToString()))
            {
                return null;
            }
            StringName keyName = ToStringName(key);
            if (require_non_empty_key && IsEmpty(keyName))
            {
                return null;
            }
            if (values[key].VariantType.ToString() != "Int")
            {
                return null;
            }
            result[keyName] = values[key].AsInt32();
        }
        return result;
    }

    private static BattleStringNameMap _damage_resistance_map_from_dict(GDictionary values)
    {
        if (values == null)
            return null;
        BattleStringNameMap result = new();
        foreach (var key in values.Keys)
        {
            if (
                !IsStringNamePayloadType(key.VariantType.ToString())
                || IsEmpty(ToStringName(key))
            )
            {
                return null;
            }
            StringName damageTag = ToStringName(key);
            if (result.ContainsKey(damageTag))
            {
                return null;
            }
            if (!IsStringNamePayloadType(values[key].VariantType.ToString()))
            {
                return null;
            }
            StringName mitigationTier = ToStringName(values[key]);
            if (
                IsEmpty(mitigationTier)
                || DamageTagContentRules.ToMitigationTierKind(mitigationTier)
                    == DamageMitigationTierKind.Unknown
            )
            {
                return null;
            }
            result.Put(damageTag, mitigationTier);
        }
        return result;
    }

    private static GStringNameArray _unique_string_name_array_from_payload(GArray values)
    {
        if (values == null)
        {
            return null;
        }
        GStringNameArray result = new();
        HashSet<StringName> seen = new();
        foreach (var value in values)
        {
            if (
                !IsStringNamePayloadType(value.VariantType.ToString())
                || IsEmpty(ToStringName(value))
            )
            {
                return null;
            }
            StringName normalized = ToStringName(value);
            if (!seen.Add(normalized))
            {
                return null;
            }
            result.Add(normalized);
        }
        return result;
    }

    internal static GEffectiveTraitArray EffectiveTraitInstancesFromPayloadArray(GArray values)
    {
        if (values == null)
            return null;

        GEffectiveTraitArray result = new();
        List<StringName> seenKeys = new();
        foreach (Variant entry in values)
        {
            if (entry.VariantType != Variant.Type.Dictionary)
                return null;

            BattleEffectiveTraitInstanceState parsed =
                BattleEffectiveTraitInstanceState.FromDictionary(entry.AsGodotDictionary());
            if (parsed == null)
                return null;
            if (ContainsStringName(seenKeys, parsed.effective_instance_key))
                return null;
            seenKeys.Add(parsed.effective_instance_key);
            result.Add(parsed);
        }
        return result;
    }

    internal static GEffectiveTraitArray DuplicateEffectiveTraitInstances(
        GEffectiveTraitArray source)
    {
        GEffectiveTraitArray result = new();
        if (source == null)
            return result;
        foreach (BattleEffectiveTraitInstanceState entry in source)
            if (entry != null)
                result.Add(entry.DuplicateState());
        return result;
    }

    internal static GArray EffectiveTraitInstancesToPayloadArray(GEffectiveTraitArray source)
    {
        GArray result = new();
        if (source == null)
            return result;
        foreach (BattleEffectiveTraitInstanceState entry in source)
            if (entry != null)
                result.Add(entry.ToDictionary());
        return result;
    }

    internal static GStringNameArray DeriveEffectiveTraitIdsFromInstances(
        GEffectiveTraitArray source)
    {
        List<StringName> values = new();
        if (source != null)
        {
            foreach (BattleEffectiveTraitInstanceState entry in source)
            {
                if (entry == null)
                    continue;
                if (IsEmpty(entry.trait_id) || ContainsStringName(values, entry.trait_id))
                    continue;
                values.Add(entry.trait_id);
            }
        }
        values.Sort((a, b) => string.CompareOrdinal(a.ToString(), b.ToString()));

        GStringNameArray result = new();
        foreach (StringName value in values)
            result.Add(value);
        return result;
    }

    private static bool ContainsStringName(List<StringName> values, StringName expected)
    {
        foreach (StringName value in values)
            if (value == expected)
                return true;
        return false;
    }

    private static bool IsStringNameField(GDictionary data, string key)
    {
        return data != null
            && data.ContainsKey(key)
            && IsStringNamePayloadType(data[key].VariantType.ToString());
    }

    private static bool StringNameSetEquals(GStringNameArray left, GStringNameArray right)
    {
        if (left == null || right == null || left.Count != right.Count)
            return false;
        foreach (StringName value in left)
        {
            if (!right.Contains(value))
                return false;
        }
        return true;
    }

    private static GStringNameArray _combat_resource_array_from_payload(GArray values)
    {
        GStringNameArray parsed = _unique_string_name_array_from_payload(values);
        if (parsed == null)
        {
            return new GStringNameArray();
        }
        if (
            !parsed.Contains(CombatResourceIds.ToStringName(CombatResourceIdKind.Hp))
            || !parsed.Contains(CombatResourceIds.ToStringName(CombatResourceIdKind.Stamina))
        )
        {
            return new GStringNameArray();
        }
        foreach (StringName resourceId in parsed)
        {
            if (!IsValidCombatResourceId(resourceId))
            {
                return new GStringNameArray();
            }
        }
        return parsed;
    }

    private static bool IsValidWeaponProfileKind(StringName value)
    {
        return value == WeaponProfileKindNone
            || value == WeaponProfileKindUnarmed
            || value == WeaponProfileKindNatural
            || value == WeaponProfileKindEquipped;
    }

    private static bool IsValidWeaponGrip(StringName value)
    {
        return value == WeaponGripNone
            || value == WeaponGripOneHanded
            || value == WeaponGripTwoHanded;
    }

    private static WeaponDice StrictWeaponDiceFromDictionary(GDictionary diceData)
    {
        if (diceData == null)
            return null;
        if (diceData.Count == 0)
        {
            return new WeaponDice();
        }
        if (!HasExactFields(diceData, new[] { "dice_count", "dice_sides", "flat_bonus" }))
        {
            return null;
        }
        foreach (string fieldName in new[] { "dice_count", "dice_sides", "flat_bonus" })
        {
            if (diceData[fieldName].VariantType.ToString() != "Int")
            {
                return null;
            }
        }
        int diceCount = diceData["dice_count"].AsInt32();
        int diceSides = diceData["dice_sides"].AsInt32();
        if (diceCount <= 0 || diceSides <= 0)
        {
            return null;
        }
        return new WeaponDice
        {
            dice_count = diceCount,
            dice_sides = diceSides,
            flat_bonus = diceData["flat_bonus"].AsInt32(),
        };
    }

    private static WeaponDice NormalizeWeaponDice(WeaponDice dice)
    {
        if (dice == null || dice.IsEmpty())
        {
            return new WeaponDice();
        }
        return dice.DuplicateState();
    }

    private void NormalizeWeaponProjection()
    {
        weapon_profile_kind = NormalizeWeaponProfileKind(weapon_profile_kind);
        weapon_current_grip = NormalizeWeaponGrip(weapon_current_grip);
        weapon_attack_range = Math.Max(weapon_attack_range, 0);
        weapon_one_handed_dice = NormalizeWeaponDice(weapon_one_handed_dice);
        weapon_two_handed_dice = NormalizeWeaponDice(weapon_two_handed_dice);
        if (weapon_uses_two_hands)
        {
            weapon_current_grip = WeaponGripTwoHanded;
        }
        else if (weapon_current_grip == WeaponGripTwoHanded)
        {
            weapon_current_grip =
                HasWeaponDice(weapon_one_handed_dice) ? WeaponGripOneHanded : WeaponGripNone;
        }
        if (weapon_profile_kind == WeaponProfileKindNone)
        {
            ClearWeaponProjection();
            return;
        }
        if (weapon_attack_range <= 0)
        {
            weapon_current_grip = WeaponGripNone;
            weapon_uses_two_hands = false;
        }
    }

    private static StringName NormalizeWeaponProfileKind(StringName value)
    {
        StringName normalized = ToStringName(value);
        if (
            normalized == WeaponProfileKindUnarmed
            || normalized == WeaponProfileKindNatural
            || normalized == WeaponProfileKindEquipped
        )
        {
            return normalized;
        }
        return WeaponProfileKindNone;
    }

    private static StringName NormalizeWeaponGrip(StringName value)
    {
        StringName normalized = ToStringName(value);
        if (normalized == WeaponGripOneHanded || normalized == WeaponGripTwoHanded)
        {
            return normalized;
        }
        return WeaponGripNone;
    }

    private static GStringArray StringNameArrayToStrings(GStringNameArray values)
    {
        GStringArray results = new();
        if (values == null)
        {
            return results;
        }
        foreach (StringName value in values)
        {
            results.Add(value.ToString());
        }
        return results;
    }

    private static int ClampResource(int value, int maxValue)
    {
        int normalizedMax = Math.Max(maxValue, 0);
        return normalizedMax > 0 ? Math.Clamp(value, 0, normalizedMax) : 0;
    }

    private static List<StringName> CopyStringNameListTyped(GStringNameArray values)
    {
        var results = new List<StringName>();
        foreach (StringName value in values ?? new GStringNameArray())
        {
            if (!IsEmpty(value))
                results.Add(value);
        }
        return results;
    }

    private static GDictionary StringNameMapToStringDict(GDictionary values)
    {
        GDictionary results = new();
        if (values == null)
        {
            return results;
        }
        foreach (var key in values.Keys)
        {
            results[key.AsString()] = values[key].AsString();
        }
        return results;
    }

    private static GStringNameArray StringsToStringNameArray(GArray values)
    {
        GStringNameArray results = new();
        if (values == null)
        {
            return results;
        }
        foreach (var value in values)
        {
            results.Add(ToStringName(value));
        }
        return results;
    }

    private static bool IsEmpty(StringName value)
    {
        return value == null || string.IsNullOrEmpty(value.ToString());
    }

    private static StringName ToStringName(StringName value)
    {
        return value ?? new StringName("");
    }

    private static StringName ToStringName<TValue>(TValue rawValue)
    {
        return ProgressionDataUtils.to_string_name(rawValue);
    }

    private static GArray GetArray(GDictionary values, string key)
    {
        if (values == null || !values.ContainsKey(key))
            return new GArray();
        var value = values[key];
        return value.VariantType == Variant.Type.Array ? value.AsGodotArray() : null;
    }

    private static int GetInt(GDictionary values, string key, int fallback)
    {
        if (values == null || !values.ContainsKey(key))
            return fallback;
        var value = values[key];
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : fallback;
    }

    private static StringName GetStringNameValue(
        GDictionary values,
        string key,
        StringName fallback = default
    )
    {
        if (values == null || !values.ContainsKey(key))
            return fallback ?? "";
        return ToStringName(values[key]);
    }

    private static GDictionary DuplicateDictionary(GDictionary source, bool deep)
    {
        return source != null ? source.Duplicate(deep) : new GDictionary();
    }

    private static bool TryGetIntMapValue(
        GDictionary source,
        StringName key,
        out int parsedValue
    )
    {
        parsedValue = 0;
        if (source == null || IsEmpty(key))
        {
            return false;
        }

        foreach (Variant rawKey in source.Keys)
        {
            if (ToStringName(rawKey) != key)
            {
                continue;
            }
            return TryReadVariantInt(source[rawKey], out parsedValue);
        }
        return false;
    }

    private static Dictionary<StringName, int> CopyStringNameIntMapTyped(GDictionary source)
    {
        var result = new Dictionary<StringName, int>();
        if (source == null)
        {
            return result;
        }

        foreach (Variant rawKey in source.Keys)
        {
            StringName normalizedKey = ToStringName(rawKey);
            if (IsEmpty(normalizedKey))
            {
                continue;
            }

            Variant value = source[rawKey];
            if (!TryReadVariantInt(value, out int parsedValue))
            {
                continue;
            }

            result[normalizedKey] = parsedValue;
        }

        return result;
    }

    private static Dictionary<StringName, StringName> CopyStringNameMapTyped(GDictionary source)
    {
        var result = new Dictionary<StringName, StringName>();
        if (source == null)
        {
            return result;
        }

        foreach (Variant rawKey in source.Keys)
        {
            StringName normalizedKey = ToStringName(rawKey);
            if (IsEmpty(normalizedKey))
            {
                continue;
            }

            StringName normalizedValue = ToStringName(source[rawKey]);
            if (IsEmpty(normalizedValue))
            {
                continue;
            }

            result[normalizedKey] = normalizedValue;
        }

        return result;
    }

    private static WeaponDice CopyWeaponDiceTyped(WeaponDice source)
    {
        if (source == null || source.IsEmpty())
        {
            return new WeaponDice();
        }

        return source.DuplicateState();
    }

    private static bool HasWeaponDice(WeaponDice dice) => dice != null && !dice.IsEmpty();

    private static GDictionary WeaponDiceToDictionary(WeaponDice dice) =>
        WeaponDiceProjection.Project(dice);

    private static bool TryReadVariantInt(Variant value, out int parsedValue)
    {
        if (value.VariantType == Variant.Type.Int)
        {
            parsedValue = value.AsInt32();
            return true;
        }

        return int.TryParse(value.ToString(), out parsedValue);
    }

    private static GVector2IArray DuplicateVector2IArray(GVector2IArray source)
    {
        GVector2IArray result = new();
        if (source == null)
        {
            return result;
        }
        foreach (Vector2I coordValue in source)
        {
            result.Add(coordValue);
        }
        return result;
    }

    private static GStringNameArray DuplicateStringNameArray(GStringNameArray source)
    {
        GStringNameArray result = new();
        if (source == null)
        {
            return result;
        }
        foreach (StringName value in source)
        {
            result.Add(value);
        }
        return result;
    }

    private static GDictionary DuplicateStatusEffects(GDictionary source)
    {
        GDictionary result = new();
        if (source == null)
        {
            return result;
        }
        foreach (StringName normalizedStatusId in SortedStatusEffectIds(source))
        {
            Variant value = source[normalizedStatusId];
            BattleStatusEffectState effectState = null;
            if (value.VariantType.ToString() == "Object")
            {
                effectState = value.As<BattleStatusEffectState>();
            }
            else if (value.VariantType.ToString() == "Dictionary")
            {
                effectState = BattleStatusEffectState.FromDictionary(value.AsGodotDictionary());
            }
            if (effectState != null && !effectState.IsEmpty())
            {
                result[effectState.status_id] = effectState.DuplicateState();
            }
        }
        return result;
    }

    private static AttributeSnapshot DuplicateAttributeSnapshot(AttributeSnapshot source)
    {
        AttributeSnapshot result = NewAttributeSnapshot();
        if (source == null)
        {
            return result;
        }
        foreach ((StringName key, int value) in source.GetAllValuesTyped())
        {
            result.SetValue(key, value);
        }
        return result;
    }

    private static GDictionary StringNameIntMapToStringDict(GDictionary values)
    {
        GDictionary result = new();
        if (values == null)
        {
            return result;
        }
        foreach (var key in values.Keys)
        {
            result[key.AsString()] = values[key].AsInt32();
        }
        return result;
    }

    private static GDictionary AttributeSnapshotToDict(AttributeSnapshot snapshot)
    {
        return snapshot?.ToDictionary() ?? new GDictionary();
    }

    private static GDictionary EquipmentViewToDict(EquipmentState view)
    {
        return view?.ToDictionary() ?? new GDictionary();
    }

    private static EquipmentState EquipmentFromDict(GDictionary payload)
    {
        return EquipmentState.FromDictionary(payload);
    }

    private static AttributeSnapshot NewAttributeSnapshot()
    {
        return new AttributeSnapshot();
    }

    private static EquipmentState NewEquipmentState()
    {
        return new EquipmentState();
    }

    private static bool IsValidBodySizeCategory(StringName category)
    {
        string text = category.ToString();
        return text == "tiny"
            || text == "small"
            || text == "medium"
            || text == "large"
            || text == "huge"
            || text == "gargantuan"
            || text == "boss";
    }

    private static bool IsValidBodySize(int size)
    {
        return size >= BodySizeSmall && size <= BodySizeBoss;
    }

    private static int GetBodySizeForCategory(StringName category)
    {
        return category.ToString() switch
        {
            "tiny" => BodySizeTiny,
            "small" => BodySizeSmall,
            "large" => BodySizeLarge,
            "huge" => BodySizeHuge,
            "gargantuan" => BodySizeGargantuan,
            "boss" => BodySizeBoss,
            _ => BodySizeMedium,
        };
    }


    private static bool BodySizeMatchesCategory(StringName category, int size)
    {
        return GetBodySizeForCategory(category) == size;
    }

    private static StringName GetBodySizeCategoryForSize(int size)
    {
        return size switch
        {
            BodySizeMedium => "medium",
            BodySizeLarge => "large",
            BodySizeHuge => "huge",
            BodySizeGargantuan => "gargantuan",
            BodySizeBoss => "boss",
            _ => "small",
        };
    }

    private static Vector2I GetFootprintForBodySize(int size)
    {
        return size switch
        {
            BodySizeLarge => new Vector2I(2, 2),
            BodySizeHuge => new Vector2I(3, 3),
            BodySizeGargantuan => new Vector2I(4, 4),
            BodySizeBoss => new Vector2I(5, 5),
            _ => Vector2I.One,
        };
    }

    private static bool Vector2IArraysEqual(GVector2IArray left, GVector2IArray right)
    {
        if (left == null || right == null || left.Count != right.Count)
        {
            return false;
        }
        for (int i = 0; i < left.Count; i++)
        {
            if (left[i] != right[i])
            {
                return false;
            }
        }
        return true;
    }

    private static List<StringName> SortedStatusEffectIds(GDictionary values)
    {
        List<StringName> result = new();
        if (values == null)
        {
            return result;
        }
        foreach (Variant key in values.Keys)
        {
            if (key.VariantType != Variant.Type.StringName)
            {
                continue;
            }
            StringName statusId = key.AsStringName();
            if (IsEmpty(statusId) || result.Contains(statusId))
            {
                continue;
            }
            result.Add(statusId);
        }
        result.Sort((left, right) => StringComparer.Ordinal.Compare(left.ToString(), right.ToString()));
        return result;
    }
}
