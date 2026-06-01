using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;

[GlobalClass]
public partial class BattleUnitState : RefCounted
{
    private static readonly StringName WeaponProfileKindNone = "none";
    private static readonly StringName WeaponProfileKindUnarmed = "unarmed";
    private static readonly StringName WeaponProfileKindNatural = "natural";
    private static readonly StringName WeaponProfileKindEquipped = "equipped";
    private static readonly StringName WeaponGripNone = "none";
    private static readonly StringName WeaponGripOneHanded = "one_handed";
    private static readonly StringName WeaponGripTwoHanded = "two_handed";
    private static readonly StringName CombatResourceHp = "hp";
    private static readonly StringName CombatResourceStamina = "stamina";
    private static readonly StringName CombatResourceMp = "mp";
    private static readonly StringName CombatResourceAura = "aura";
    private static readonly StringName BodySizeCategoryMedium = "medium";

    private const int DefaultMovePointsPerTurn = 2;
    private const int DefaultActionThreshold = 120;
    private const int BodySizeTiny = 1;
    private const int BodySizeSmall = 1;
    private const int BodySizeMedium = 2;
    private const int BodySizeLarge = 3;
    private const int BodySizeHuge = 4;
    private const int BodySizeGargantuan = 5;
    private const int BodySizeBoss = 6;

    private static readonly HashSet<StringName> ValidMitigationTiers = new()
    {
        "normal",
        "half",
        "double",
        "immune",
    };

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
        "race_trait_ids",
        "subrace_trait_ids",
        "ascension_trait_ids",
        "bloodline_trait_ids",
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

    public static int DEFAULT_MOVE_POINTS_PER_TURN() => DefaultMovePointsPerTurn;

    public static int DEFAULT_ACTION_THRESHOLD() => DefaultActionThreshold;

    public static StringName WEAPON_PROFILE_KIND_NONE() => WeaponProfileKindNone;

    public static StringName WEAPON_PROFILE_KIND_UNARMED() => WeaponProfileKindUnarmed;

    public static StringName WEAPON_PROFILE_KIND_NATURAL() => WeaponProfileKindNatural;

    public static StringName WEAPON_PROFILE_KIND_EQUIPPED() => WeaponProfileKindEquipped;

    public static StringName WEAPON_GRIP_NONE() => WeaponGripNone;

    public static StringName WEAPON_GRIP_ONE_HANDED() => WeaponGripOneHanded;

    public static StringName WEAPON_GRIP_TWO_HANDED() => WeaponGripTwoHanded;

    public static StringName COMBAT_RESOURCE_HP() => CombatResourceHp;

    public static StringName COMBAT_RESOURCE_STAMINA() => CombatResourceStamina;

    public static StringName COMBAT_RESOURCE_MP() => CombatResourceMp;

    public static StringName COMBAT_RESOURCE_AURA() => CombatResourceAura;

    public static int BODY_SIZE_TINY() => BodySizeTiny;

    public static int BODY_SIZE_SMALL() => BodySizeSmall;

    public static int BODY_SIZE_MEDIUM() => BodySizeMedium;

    public static int BODY_SIZE_LARGE() => BodySizeLarge;

    public static int BODY_SIZE_HUGE() => BodySizeHuge;

    public static int BODY_SIZE_GARGANTUAN() => BodySizeGargantuan;

    public static int BODY_SIZE_BOSS() => BodySizeBoss;

    public static GStringNameArray DEFAULT_UNLOCKED_COMBAT_RESOURCE_IDS()
    {
        return new GStringNameArray { CombatResourceHp, CombatResourceStamina };
    }

    public static GStringNameArray VALID_COMBAT_RESOURCE_IDS()
    {
        return new GStringNameArray
        {
            CombatResourceHp,
            CombatResourceStamina,
            CombatResourceMp,
            CombatResourceAura,
        };
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
    public BattleAiBlackboard ai_blackboard = new();
    public Vector2I coord = Vector2I.Zero;
    public int body_size = BodySizeMedium;
    public StringName body_size_category = BodySizeCategoryMedium;
    public Vector2I footprint_size = Vector2I.One;
    public GVector2IArray occupied_coords = new();
    public bool is_alive = true;
    public AttributeSnapshot attribute_snapshot = NewAttributeSnapshot();
    public EquipmentState equipment_view = NewEquipmentState();
    public bool equipment_view_initialized;
    public int current_hp;
    public int current_mp;
    public int current_stamina;
    public int current_aura;
    public int current_ap;
    public int current_move_points = DefaultMovePointsPerTurn;
    public GStringNameArray unlocked_combat_resource_ids = DEFAULT_UNLOCKED_COMBAT_RESOURCE_IDS();
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
    public GStringNameArray known_active_skill_ids = new();
    public GDictionary known_skill_level_map = new();
    public GDictionary known_skill_lock_hit_bonus_map = new();
    public GStringNameArray movement_tags = new();
    public GStringNameArray vision_tags = new();
    public GStringNameArray proficiency_tags = new();
    public GStringNameArray save_advantage_tags = new();
    public GDictionary damage_resistances = new();
    public GStringNameArray race_trait_ids = new();
    public GStringNameArray subrace_trait_ids = new();
    public GStringNameArray ascension_trait_ids = new();
    public GStringNameArray bloodline_trait_ids = new();
    public StringName versatility_pick = "";
    public StringName weapon_profile_kind = WeaponProfileKindNone;
    public StringName weapon_item_id = "";
    public StringName weapon_profile_type_id = "";
    public StringName weapon_family = "";
    public StringName weapon_current_grip = WeaponGripNone;
    public int weapon_attack_range;
    public GDictionary weapon_one_handed_dice = new();
    public GDictionary weapon_two_handed_dice = new();
    public bool weapon_is_versatile;
    public bool weapon_uses_two_hands;
    public StringName weapon_physical_damage_tag = "";
    public GDictionary cooldowns = new();
    public int last_turn_tu = -1;
    public GDictionary status_effects = new();
    public GDictionary per_battle_charges = new();
    public GDictionary per_turn_charges = new();
    public GDictionary per_turn_charge_limits = new();
    public GDictionary fumble_protection_used = new();
    public bool death_ward_consumed_this_battle;

    public BattleUnitState()
    {
        refresh_footprint();
    }

    public void set_anchor_coord(Vector2I anchor_coord)
    {
        coord = anchor_coord;
        refresh_footprint();
    }

    public void refresh_footprint()
    {
        footprint_size = get_footprint_size_for_body_size(body_size);
        occupied_coords = _build_occupied_coords(coord, footprint_size);
    }

    public bool occupies_coord(Vector2I target_coord)
    {
        return occupied_coords.Contains(target_coord);
    }

    public bool has_movement_tag(StringName tag)
    {
        return movement_tags.Contains(tag);
    }

    public bool set_body_size_category(StringName category)
    {
        if (!IsValidBodySizeCategory(category))
        {
            return false;
        }
        body_size_category = category;
        body_size = GetBodySizeForCategory(category);
        refresh_footprint();
        return true;
    }

    public void normalize_body_size_projection()
    {
        if (BodySizeMatchesCategory(body_size_category, body_size))
        {
            refresh_footprint();
            return;
        }
        throw new InvalidOperationException(
            $"BattleUnitState body_size/body_size_category 不一致: " +
            $"body_size={body_size}, body_size_category='{body_size_category}'。 " +
            $"请检查数据构造路径是否绕过 set_body_size_category()。"
        );
    }

    public bool has_status_effect(StringName status_id)
    {
        return get_status_effect(status_id) != null;
    }

    public bool has_shield()
    {
        return current_shield_hp > 0 && shield_max_hp > 0 && shield_duration > 0;
    }

    public int get_aura_max()
    {
        return attribute_snapshot?.get_value("aura_max") ?? 0;
    }

    public void sync_default_combat_resource_unlocks()
    {
        foreach (StringName resourceId in DEFAULT_UNLOCKED_COMBAT_RESOURCE_IDS())
        {
            unlock_combat_resource(resourceId);
        }
    }

    public bool has_combat_resource_unlocked(StringName resource_id)
    {
        return unlocked_combat_resource_ids.Contains(resource_id);
    }

    public bool unlock_combat_resource(StringName resource_id)
    {
        if (IsEmpty(resource_id))
        {
            return false;
        }
        if (!VALID_COMBAT_RESOURCE_IDS().Contains(resource_id))
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

    public void set_unlocked_combat_resource_ids(GStringNameArray resource_ids)
    {
        unlocked_combat_resource_ids = new GStringNameArray();
        if (resource_ids != null)
        {
            foreach (StringName resourceId in resource_ids)
            {
                unlock_combat_resource(resourceId);
            }
        }
        sync_default_combat_resource_unlocks();
    }

    public void clear_shield()
    {
        current_shield_hp = 0;
        shield_max_hp = 0;
        shield_duration = -1;
        shield_family = "";
        shield_source_unit_id = "";
        shield_source_skill_id = "";
    }

    public void normalize_shield_state()
    {
        if (current_shield_hp <= 0 || shield_max_hp <= 0 || shield_duration <= 0)
        {
            clear_shield();
            return;
        }
        shield_max_hp = Math.Max(shield_max_hp, 1);
        current_shield_hp = Math.Clamp(current_shield_hp, 0, shield_max_hp);
        if (current_shield_hp <= 0)
        {
            clear_shield();
        }
    }

    public EquipmentState get_equipment_view()
    {
        if (equipment_view == null)
        {
            equipment_view = NewEquipmentState();
        }
        return equipment_view;
    }

    public void set_equipment_view(EquipmentState source_equipment_state)
    {
        equipment_view_initialized = true;
        equipment_view = source_equipment_state?.duplicate_state() ?? NewEquipmentState();
    }

    public void clear_weapon_projection()
    {
        weapon_profile_kind = WeaponProfileKindNone;
        weapon_item_id = "";
        weapon_profile_type_id = "";
        weapon_family = "";
        weapon_current_grip = WeaponGripNone;
        weapon_attack_range = 0;
        weapon_one_handed_dice = new GDictionary();
        weapon_two_handed_dice = new GDictionary();
        weapon_is_versatile = false;
        weapon_uses_two_hands = false;
        weapon_physical_damage_tag = "";
    }

    public void set_unarmed_weapon_projection(
        StringName damage_tag = default,
        GDictionary dice = null,
        int attack_range = 1
    )
    {
        if (IsEmpty(damage_tag))
        {
            damage_tag = "physical_blunt";
        }
        dice ??= new GDictionary
        {
            ["dice_count"] = 1,
            ["dice_sides"] = 4,
            ["flat_bonus"] = 0,
        };
        apply_weapon_projection(
            new GDictionary
            {
                ["weapon_profile_kind"] = WeaponProfileKindUnarmed.ToString(),
                ["weapon_profile_type_id"] = "unarmed",
                ["weapon_family"] = "unarmed",
                ["weapon_current_grip"] = WeaponGripOneHanded.ToString(),
                ["weapon_attack_range"] = attack_range,
                ["weapon_one_handed_dice"] = dice,
                ["weapon_uses_two_hands"] = false,
                ["weapon_physical_damage_tag"] = damage_tag.ToString(),
            }
        );
    }

    public void set_natural_weapon_projection(
        StringName profile_type_id,
        StringName damage_tag,
        int attack_range,
        GDictionary dice = null,
        StringName family = default
    )
    {
        dice ??= new GDictionary();
        apply_weapon_projection(
            new GDictionary
            {
                ["weapon_profile_kind"] = WeaponProfileKindNatural.ToString(),
                ["weapon_profile_type_id"] = profile_type_id.ToString(),
                ["weapon_family"] = family.ToString(),
                ["weapon_current_grip"] = (
                    attack_range > 0 ? WeaponGripOneHanded : WeaponGripNone
                ).ToString(),
                ["weapon_attack_range"] = attack_range,
                ["weapon_one_handed_dice"] = dice,
                ["weapon_uses_two_hands"] = false,
                ["weapon_physical_damage_tag"] = damage_tag.ToString(),
            }
        );
    }

    public void apply_weapon_projection(GDictionary projection)
    {
        if (projection == null || projection.Count == 0)
        {
            clear_weapon_projection();
            return;
        }
        weapon_profile_kind = _normalize_weapon_profile_kind(
            ToStringName(
                projection.GetValueOrDefault(
                    "weapon_profile_kind",
                    WeaponProfileKindNone.ToString()
                )
            )
        );
        weapon_item_id = ToStringName(projection.GetValueOrDefault("weapon_item_id", ""));
        weapon_profile_type_id = ToStringName(
            projection.GetValueOrDefault("weapon_profile_type_id", "")
        );
        weapon_family = ToStringName(projection.GetValueOrDefault("weapon_family", ""));
        weapon_current_grip = _normalize_weapon_grip(
            ToStringName(
                projection.GetValueOrDefault(
                    "weapon_current_grip",
                    WeaponGripNone.ToString()
                )
            )
        );
        weapon_attack_range = Math.Max(GetInt(projection, "weapon_attack_range", 0), 0);
        weapon_one_handed_dice = _normalize_weapon_dice(
            GetDictionary(projection, "weapon_one_handed_dice")
        );
        weapon_two_handed_dice = _normalize_weapon_dice(
            GetDictionary(projection, "weapon_two_handed_dice")
        );
        weapon_is_versatile = false;
        if (projection.ContainsKey("weapon_is_versatile"))
            weapon_is_versatile = ReadBool(projection, "weapon_is_versatile");
        bool hasWeaponUsesTwoHands = false;
        weapon_uses_two_hands = weapon_current_grip == WeaponGripTwoHanded;
        if (projection.ContainsKey("weapon_uses_two_hands"))
        {
            hasWeaponUsesTwoHands = true;
            weapon_uses_two_hands = ReadBool(projection, "weapon_uses_two_hands");
        }
        if (weapon_uses_two_hands)
        {
            weapon_current_grip = WeaponGripTwoHanded;
        }
        else if (hasWeaponUsesTwoHands && weapon_current_grip == WeaponGripTwoHanded)
        {
            weapon_current_grip =
                weapon_one_handed_dice.Count > 0 ? WeaponGripOneHanded : WeaponGripNone;
        }
        weapon_physical_damage_tag = ToStringName(
            projection.GetValueOrDefault("weapon_physical_damage_tag", "")
        );
        if (weapon_profile_kind == WeaponProfileKindNone)
        {
            clear_weapon_projection();
            return;
        }
        if (weapon_attack_range <= 0)
        {
            weapon_current_grip = WeaponGripNone;
            weapon_uses_two_hands = false;
        }
    }

    public int get_weapon_attack_range()
    {
        return Math.Max(weapon_attack_range, 0);
    }

    public BattleStatusEffectState get_status_effect(StringName status_id)
    {
        StringName normalized = ToStringName(status_id);
        if (IsEmpty(normalized) || !status_effects.ContainsKey(normalized))
        {
            return null;
        }
        var effectValue = status_effects[normalized];
        BattleStatusEffectState effectState = null;
        if (effectValue.VariantType.ToString() == "Object")
        {
            effectState = effectValue.As<BattleStatusEffectState>();
        }
        if (effectState != null && !effectState.is_empty())
        {
            return effectState;
        }
        effectState =
            effectValue.VariantType.ToString() == "Dictionary"
                ? BattleStatusEffectState.from_dict(effectValue.AsGodotDictionary())
                : null;
        if (effectState == null || effectState.is_empty())
        {
            status_effects.Remove(normalized);
            return null;
        }
        status_effects[normalized] = effectState;
        return effectState;
    }

    public void set_status_effect(BattleStatusEffectState effect_state)
    {
        if (effect_state == null || effect_state.is_empty())
        {
            return;
        }
        status_effects[effect_state.status_id] = effect_state;
    }

    public void SetStatusEffect(BattleStatusEffectState effect_state)
    {
        set_status_effect(effect_state);
    }

    public void erase_status_effect(StringName status_id)
    {
        StringName normalized = ToStringName(status_id);
        if (!IsEmpty(normalized))
        {
            status_effects.Remove(normalized);
        }
    }

    public void EraseStatusEffect(StringName status_id)
    {
        erase_status_effect(status_id);
    }

    public void reset_per_turn_charges()
    {
        per_turn_charges.Clear();
        foreach (var chargeKeyValue in per_turn_charge_limits.Keys)
        {
            StringName chargeKey = ToStringName(chargeKeyValue);
            int chargeLimit = Math.Max(per_turn_charge_limits[chargeKeyValue].AsInt32(), 0);
            if (IsEmpty(chargeKey) || chargeLimit <= 0)
            {
                continue;
            }
            per_turn_charges[chargeKey] = chargeLimit;
        }
    }

    public BattleUnitState clone()
    {
        normalize_body_size_projection();
        normalize_shield_state();
        apply_weapon_projection(_build_current_weapon_projection_payload());
        sync_default_combat_resource_unlocks();

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
            equipment_view = get_equipment_view()?.duplicate_state() ?? NewEquipmentState(),
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
            known_skill_level_map = DuplicateDictionary(known_skill_level_map, true),
            known_skill_lock_hit_bonus_map = DuplicateDictionary(
                known_skill_lock_hit_bonus_map,
                true
            ),
            movement_tags = DuplicateStringNameArray(movement_tags),
            vision_tags = DuplicateStringNameArray(vision_tags),
            proficiency_tags = DuplicateStringNameArray(proficiency_tags),
            save_advantage_tags = DuplicateStringNameArray(save_advantage_tags),
            damage_resistances = DuplicateDictionary(damage_resistances, true),
            race_trait_ids = DuplicateStringNameArray(race_trait_ids),
            subrace_trait_ids = DuplicateStringNameArray(subrace_trait_ids),
            ascension_trait_ids = DuplicateStringNameArray(ascension_trait_ids),
            bloodline_trait_ids = DuplicateStringNameArray(bloodline_trait_ids),
            versatility_pick = versatility_pick,
            weapon_profile_kind = weapon_profile_kind,
            weapon_item_id = weapon_item_id,
            weapon_profile_type_id = weapon_profile_type_id,
            weapon_family = weapon_family,
            weapon_current_grip = weapon_current_grip,
            weapon_attack_range = weapon_attack_range,
            weapon_one_handed_dice = DuplicateDictionary(weapon_one_handed_dice, true),
            weapon_two_handed_dice = DuplicateDictionary(weapon_two_handed_dice, true),
            weapon_is_versatile = weapon_is_versatile,
            weapon_uses_two_hands = weapon_uses_two_hands,
            weapon_physical_damage_tag = weapon_physical_damage_tag,
            cooldowns = DuplicateDictionary(cooldowns, true),
            last_turn_tu = last_turn_tu,
            status_effects = DuplicateStatusEffects(status_effects),
            per_battle_charges = DuplicateDictionary(per_battle_charges, true),
            per_turn_charges = DuplicateDictionary(per_turn_charges, true),
            per_turn_charge_limits = DuplicateDictionary(per_turn_charge_limits, true),
        };
    }

    public static Vector2I get_footprint_size_for_body_size(int size_value)
    {
        return GetFootprintForBodySize(Math.Max(size_value, BodySizeSmall));
    }

    public GDictionary to_dict()
    {
        normalize_body_size_projection();
        normalize_shield_state();
        apply_weapon_projection(_build_current_weapon_projection_payload());
        sync_default_combat_resource_unlocks();

        GDictionary statusPayloads = new();
        foreach (string statusIdString in SortedStringKeys(status_effects))
        {
            StringName statusId = statusIdString;
            BattleStatusEffectState effectState = get_status_effect(statusId);
            if (effectState == null)
            {
                continue;
            }
            statusPayloads[statusIdString] = effectState.to_dict();
        }

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
            ["equipment_view"] = EquipmentViewToDict(get_equipment_view()),
            ["current_hp"] = current_hp,
            ["current_mp"] = current_mp,
            ["current_stamina"] = current_stamina,
            ["current_aura"] = current_aura,
            ["aura_max"] = get_aura_max(),
            ["current_ap"] = current_ap,
            ["current_move_points"] = current_move_points,
            ["unlocked_combat_resource_ids"] = _string_name_array_to_strings(
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
            ["known_active_skill_ids"] = _string_name_array_to_strings(known_active_skill_ids),
            ["known_skill_level_map"] = StringNameIntMapToStringDict(known_skill_level_map),
            ["known_skill_lock_hit_bonus_map"] = StringNameIntMapToStringDict(
                known_skill_lock_hit_bonus_map
            ),
            ["movement_tags"] = _string_name_array_to_strings(movement_tags),
            ["vision_tags"] = _string_name_array_to_strings(vision_tags),
            ["proficiency_tags"] = _string_name_array_to_strings(proficiency_tags),
            ["save_advantage_tags"] = _string_name_array_to_strings(save_advantage_tags),
            ["damage_resistances"] = _string_name_map_to_string_dict(damage_resistances),
            ["race_trait_ids"] = _string_name_array_to_strings(race_trait_ids),
            ["subrace_trait_ids"] = _string_name_array_to_strings(subrace_trait_ids),
            ["ascension_trait_ids"] = _string_name_array_to_strings(ascension_trait_ids),
            ["bloodline_trait_ids"] = _string_name_array_to_strings(bloodline_trait_ids),
            ["versatility_pick"] = versatility_pick.ToString(),
            ["weapon_profile_kind"] = weapon_profile_kind.ToString(),
            ["weapon_item_id"] = weapon_item_id.ToString(),
            ["weapon_profile_type_id"] = weapon_profile_type_id.ToString(),
            ["weapon_family"] = weapon_family.ToString(),
            ["weapon_current_grip"] = weapon_current_grip.ToString(),
            ["weapon_attack_range"] = weapon_attack_range,
            ["weapon_one_handed_dice"] = DuplicateDictionary(weapon_one_handed_dice, true),
            ["weapon_two_handed_dice"] = DuplicateDictionary(weapon_two_handed_dice, true),
            ["weapon_is_versatile"] = weapon_is_versatile,
            ["weapon_uses_two_hands"] = weapon_uses_two_hands,
            ["weapon_physical_damage_tag"] = weapon_physical_damage_tag.ToString(),
            ["cooldowns"] = DuplicateDictionary(cooldowns, true),
            ["last_turn_tu"] = last_turn_tu,
            ["status_effects"] = statusPayloads,
        };
    }

    public static BattleUnitState from_dict(GDictionary payload)
    {
        if (payload == null)
            return null;
        if (payload.Count == 0)
        {
            return null;
        }
        if (!_has_exact_fields(payload, ToDictFields))
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
        Vector2I expectedFootprint = get_footprint_size_for_body_size(bodySizeInt);
        GVector2IArray expectedOccupied = _build_occupied_coords(
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

        AttributeSnapshot parsedAttributeSnapshot = _attribute_snapshot_from_dict(
            payload["attribute_snapshot"].AsGodotDictionary()
        );
        if (parsedAttributeSnapshot == null)
        {
            return null;
        }
        GDictionary parsedKnownSkillLevelMap = _string_name_int_map_from_dict(
            payload["known_skill_level_map"].AsGodotDictionary(),
            true
        );
        if (parsedKnownSkillLevelMap == null)
        {
            return null;
        }
        GDictionary parsedKnownSkillLockHitBonusMap = _string_name_int_map_from_dict(
            payload["known_skill_lock_hit_bonus_map"].AsGodotDictionary(),
            true
        );
        if (parsedKnownSkillLockHitBonusMap == null)
        {
            return null;
        }
        foreach (var skillId in parsedKnownSkillLockHitBonusMap.Keys)
        {
            if (parsedKnownSkillLockHitBonusMap[skillId].AsInt32() < 0)
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
        GStringNameArray parsedRaceTraitIds = _unique_string_name_array_from_payload(
            GetArray(payload, "race_trait_ids")
        );
        if (parsedRaceTraitIds == null)
        {
            return null;
        }
        GStringNameArray parsedSubraceTraitIds = _unique_string_name_array_from_payload(
            GetArray(payload, "subrace_trait_ids")
        );
        if (parsedSubraceTraitIds == null)
        {
            return null;
        }
        GStringNameArray parsedAscensionTraitIds = _unique_string_name_array_from_payload(
            GetArray(payload, "ascension_trait_ids")
        );
        if (parsedAscensionTraitIds == null)
        {
            return null;
        }
        GStringNameArray parsedBloodlineTraitIds = _unique_string_name_array_from_payload(
            GetArray(payload, "bloodline_trait_ids")
        );
        if (parsedBloodlineTraitIds == null)
        {
            return null;
        }
        GDictionary parsedDamageResistances = _damage_resistance_map_from_dict(
            payload["damage_resistances"].AsGodotDictionary()
        );
        if (parsedDamageResistances == null)
        {
            return null;
        }

        StringName parsedWeaponProfileKind = ToStringName(payload["weapon_profile_kind"]);
        if (!_is_valid_weapon_profile_kind(parsedWeaponProfileKind))
        {
            return null;
        }
        StringName parsedWeaponCurrentGrip = ToStringName(payload["weapon_current_grip"]);
        if (!_is_valid_weapon_grip(parsedWeaponCurrentGrip))
        {
            return null;
        }
        GDictionary parsedWeaponOneHandedDice = _strict_weapon_dice_from_dict(
            payload["weapon_one_handed_dice"].AsGodotDictionary()
        );
        if (parsedWeaponOneHandedDice == null)
        {
            return null;
        }
        GDictionary parsedWeaponTwoHandedDice = _strict_weapon_dice_from_dict(
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
        GDictionary parsedStatusEffects = _status_effects_from_dict(
            payload["status_effects"].AsGodotDictionary()
        );
        if (parsedStatusEffects == null)
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
            race_trait_ids = parsedRaceTraitIds,
            subrace_trait_ids = parsedSubraceTraitIds,
            ascension_trait_ids = parsedAscensionTraitIds,
            bloodline_trait_ids = parsedBloodlineTraitIds,
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
            cooldowns = DuplicateDictionary(payload["cooldowns"].AsGodotDictionary(), true),
            last_turn_tu = payload["last_turn_tu"].AsInt32(),
            status_effects = parsedStatusEffects,
        };
        unitState.attribute_snapshot.set_value("aura_max", payload["aura_max"].AsInt32());
        unitState.normalize_shield_state();
        unitState.refresh_footprint();
        return unitState;
    }

    public static AttributeSnapshot _attribute_snapshot_from_dict(GDictionary values)
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
            snapshot.set_value(ToStringName(key), values[key].AsInt32());
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

    public static GDictionary _status_effects_from_dict(GDictionary values)
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
            BattleStatusEffectState effectState = BattleStatusEffectState.from_dict(
                effectValue.AsGodotDictionary()
            );
            if (effectState == null || effectState.is_empty())
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

    public static GVector2IArray _build_occupied_coords(Vector2I anchor_coord, Vector2I footprint)
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

    public static bool _has_exact_fields(GDictionary data, string[] expected_fields)
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

    private static GDictionary _damage_resistance_map_from_dict(GDictionary values)
    {
        if (values == null)
            return null;
        GDictionary result = new();
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
            if (IsEmpty(mitigationTier) || !ValidMitigationTiers.Contains(mitigationTier))
            {
                return null;
            }
            result[damageTag] = mitigationTier;
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

    private static GStringNameArray _combat_resource_array_from_payload(GArray values)
    {
        GStringNameArray parsed = _unique_string_name_array_from_payload(values);
        if (parsed == null)
        {
            return new GStringNameArray();
        }
        if (!parsed.Contains(CombatResourceHp) || !parsed.Contains(CombatResourceStamina))
        {
            return new GStringNameArray();
        }
        foreach (StringName resourceId in parsed)
        {
            if (!VALID_COMBAT_RESOURCE_IDS().Contains(resourceId))
            {
                return new GStringNameArray();
            }
        }
        return parsed;
    }

    public static bool _is_valid_weapon_profile_kind(StringName value)
    {
        return value == WeaponProfileKindNone
            || value == WeaponProfileKindUnarmed
            || value == WeaponProfileKindNatural
            || value == WeaponProfileKindEquipped;
    }

    public static bool _is_valid_weapon_grip(StringName value)
    {
        return value == WeaponGripNone
            || value == WeaponGripOneHanded
            || value == WeaponGripTwoHanded;
    }

    public static GDictionary _strict_weapon_dice_from_dict(GDictionary diceData)
    {
        if (diceData == null)
            return null;
        if (diceData.Count == 0)
        {
            return new GDictionary();
        }
        if (!_has_exact_fields(diceData, new[] { "dice_count", "dice_sides", "flat_bonus" }))
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
        return new GDictionary
        {
            ["dice_count"] = diceCount,
            ["dice_sides"] = diceSides,
            ["flat_bonus"] = diceData["flat_bonus"].AsInt32(),
        };
    }

    public GDictionary _build_current_weapon_projection_payload()
    {
        return new GDictionary
        {
            ["weapon_profile_kind"] = weapon_profile_kind,
            ["weapon_item_id"] = weapon_item_id,
            ["weapon_profile_type_id"] = weapon_profile_type_id,
            ["weapon_family"] = weapon_family,
            ["weapon_current_grip"] = weapon_current_grip,
            ["weapon_attack_range"] = weapon_attack_range,
            ["weapon_one_handed_dice"] = weapon_one_handed_dice,
            ["weapon_two_handed_dice"] = weapon_two_handed_dice,
            ["weapon_is_versatile"] = weapon_is_versatile,
            ["weapon_uses_two_hands"] = weapon_uses_two_hands,
            ["weapon_physical_damage_tag"] = weapon_physical_damage_tag,
        };
    }

    public static StringName _normalize_weapon_profile_kind(StringName value)
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

    public static StringName _normalize_weapon_grip(StringName value)
    {
        StringName normalized = ToStringName(value);
        if (normalized == WeaponGripOneHanded || normalized == WeaponGripTwoHanded)
        {
            return normalized;
        }
        return WeaponGripNone;
    }

    public static GDictionary _normalize_weapon_dice(GDictionary value)
    {
        if (value == null)
        {
            return new GDictionary();
        }
        GDictionary diceData = value;
        int diceCount = GetInt(diceData, "dice_count", 0);
        int diceSides = GetInt(diceData, "dice_sides", 0);
        if (diceCount <= 0 || diceSides <= 0)
        {
            return new GDictionary();
        }
        return new GDictionary
        {
            ["dice_count"] = diceCount,
            ["dice_sides"] = diceSides,
            ["flat_bonus"] = GetInt(diceData, "flat_bonus", 0),
        };
    }

    public static GStringArray _string_name_array_to_strings(GStringNameArray values)
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

    public static GDictionary _string_name_map_to_string_dict(GDictionary values)
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

    public static GStringNameArray _strings_to_string_name_array(GArray values)
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
        return string.IsNullOrEmpty(value.ToString());
    }

    private static StringName ToStringName(StringName value)
    {
        return value;
    }

    private static StringName ToStringName<TValue>(TValue rawValue)
    {
        return ProgressionDataUtils.to_string_name(rawValue);
    }

    private static GArray GetArray(GDictionary values, string key)
    {
        var value = values.GetValueOrDefault(key, new GArray());
        return value.VariantType.ToString() == "Array" ? value.AsGodotArray() : null;
    }

    private static GDictionary GetDictionary(GDictionary values, string key)
    {
        var value = values.GetValueOrDefault(key, new GDictionary());
        return value.VariantType.ToString() == "Dictionary" ? value.AsGodotDictionary() : null;
    }

    private static int GetInt(GDictionary values, string key, int fallback)
    {
        var value = values.GetValueOrDefault(key, fallback);
        return value.VariantType.ToString() == "Int" ? value.AsInt32() : fallback;
    }

    private static GDictionary DuplicateDictionary(GDictionary source, bool deep)
    {
        return source != null ? source.Duplicate(deep) : new GDictionary();
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
        foreach (string statusIdString in SortedStringKeys(source))
        {
            BattleStatusEffectState effectState = null;
            var value = source.ContainsKey(statusIdString)
                ? source[statusIdString]
                : source[new StringName(statusIdString)];
            if (value.VariantType.ToString() == "Object")
            {
                effectState = value.As<BattleStatusEffectState>();
            }
            else if (value.VariantType.ToString() == "Dictionary")
            {
                effectState = BattleStatusEffectState.from_dict(value.AsGodotDictionary());
            }
            if (effectState != null && !effectState.is_empty())
            {
                result[effectState.status_id] = effectState.duplicate_state();
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
        GDictionary values = source.get_all_values();
        foreach (var key in values.Keys)
        {
            if (values[key].VariantType.ToString() == "Int")
            {
                result.set_value(ToStringName(key), values[key].AsInt32());
            }
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
        return snapshot?.to_dict() ?? new GDictionary();
    }

    private static GDictionary EquipmentViewToDict(EquipmentState view)
    {
        return view?.to_dict() ?? new GDictionary();
    }

    private static EquipmentState EquipmentFromDict(GDictionary payload)
    {
        return EquipmentState.from_dict(payload);
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

    private static List<string> SortedStringKeys(GDictionary values)
    {
        List<string> result = new();
        if (values == null)
        {
            return result;
        }
        foreach (var key in values.Keys)
        {
            result.Add(key.AsString());
        }
        result.Sort(StringComparer.Ordinal);
        return result;
    }
}
