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
    private static readonly Script AttributeSnapshotScript = GD.Load<Script>("res://scripts/player/progression/attribute_snapshot.gd");
    private static readonly Script EquipmentStateScript = GD.Load<Script>("res://scripts/player/equipment/equipment_state.gd");

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
        "ai_blackboard",
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
        "shield_params",
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
        "combo_state",
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
        return new GStringNameArray { CombatResourceHp, CombatResourceStamina, CombatResourceMp, CombatResourceAura };
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
    public GDictionary ai_blackboard = new();
    public Vector2I coord = Vector2I.Zero;
    public int body_size = BodySizeMedium;
    public StringName body_size_category = BodySizeCategoryMedium;
    public Vector2I footprint_size = Vector2I.One;
    public GVector2IArray occupied_coords = new();
    public bool is_alive = true;
    public GodotObject attribute_snapshot = NewAttributeSnapshot();
    public GodotObject equipment_view = NewEquipmentState();
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
    public GDictionary shield_params = new();
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
    public GDictionary combo_state = new();
    public GDictionary per_battle_charges = new();
    public GDictionary per_turn_charges = new();
    public GDictionary per_turn_charge_limits = new();
    public GDictionary fumble_protection_used = new();

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

    public void sync_body_size_category_from_body_size()
    {
        body_size_category = GetCategoryForBodySize(body_size);
    }

    public void normalize_body_size_projection()
    {
        if (BodySizeMatchesCategory(body_size_category, body_size))
        {
            refresh_footprint();
            return;
        }
        if (IsValidBodySize(body_size))
        {
            sync_body_size_category_from_body_size();
        }
        else if (IsValidBodySizeCategory(body_size_category))
        {
            body_size = GetBodySizeForCategory(body_size_category);
        }
        else
        {
            sync_body_size_category_from_body_size();
        }
        refresh_footprint();
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
        return attribute_snapshot != null && attribute_snapshot.HasMethod("get_value")
            ? attribute_snapshot.Call("get_value", Variant.From("aura_max")).AsInt32()
            : 0;
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
        shield_params = new GDictionary();
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

    public GodotObject get_equipment_view()
    {
        if (equipment_view == null || !equipment_view.HasMethod("get_equipped_item_id"))
        {
            equipment_view = NewEquipmentState();
        }
        return equipment_view;
    }

    public void set_equipment_view(Variant source_equipment_state)
    {
        equipment_view_initialized = true;
        if (source_equipment_state.VariantType == Variant.Type.Object)
        {
            GodotObject source = source_equipment_state.AsGodotObject();
            if (source != null && source.HasMethod("duplicate_state"))
            {
                GodotObject duplicated = source.Call("duplicate_state").AsGodotObject();
                equipment_view = duplicated ?? NewEquipmentState();
                return;
            }
        }
        if (source_equipment_state.VariantType == Variant.Type.Dictionary)
        {
            GodotObject restored = EquipmentFromDict(source_equipment_state.AsGodotDictionary());
            equipment_view = restored ?? NewEquipmentState();
            return;
        }
        equipment_view = NewEquipmentState();
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

    public void set_unarmed_weapon_projection(StringName damage_tag = default, GDictionary dice = null, int attack_range = 1)
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
        apply_weapon_projection(new GDictionary
        {
            ["weapon_profile_kind"] = WeaponProfileKindUnarmed.ToString(),
            ["weapon_profile_type_id"] = "unarmed",
            ["weapon_family"] = "unarmed",
            ["weapon_current_grip"] = WeaponGripOneHanded.ToString(),
            ["weapon_attack_range"] = attack_range,
            ["weapon_one_handed_dice"] = dice,
            ["weapon_uses_two_hands"] = false,
            ["weapon_physical_damage_tag"] = damage_tag.ToString(),
        });
    }

    public void set_natural_weapon_projection(
        StringName profile_type_id,
        StringName damage_tag,
        int attack_range,
        GDictionary dice = null,
        StringName family = default)
    {
        dice ??= new GDictionary();
        apply_weapon_projection(new GDictionary
        {
            ["weapon_profile_kind"] = WeaponProfileKindNatural.ToString(),
            ["weapon_profile_type_id"] = profile_type_id.ToString(),
            ["weapon_family"] = family.ToString(),
            ["weapon_current_grip"] = (attack_range > 0 ? WeaponGripOneHanded : WeaponGripNone).ToString(),
            ["weapon_attack_range"] = attack_range,
            ["weapon_one_handed_dice"] = dice,
            ["weapon_uses_two_hands"] = false,
            ["weapon_physical_damage_tag"] = damage_tag.ToString(),
        });
    }

    public void apply_weapon_projection(GDictionary projection)
    {
        if (projection == null || projection.Count == 0)
        {
            clear_weapon_projection();
            return;
        }
        weapon_profile_kind = _normalize_weapon_profile_kind(GetVariant(projection, "weapon_profile_kind", WeaponProfileKindNone.ToString()));
        weapon_item_id = ToStringName(GetVariant(projection, "weapon_item_id", ""));
        weapon_profile_type_id = ToStringName(GetVariant(projection, "weapon_profile_type_id", ""));
        weapon_family = ToStringName(GetVariant(projection, "weapon_family", ""));
        weapon_current_grip = _normalize_weapon_grip(GetVariant(projection, "weapon_current_grip", WeaponGripNone.ToString()));
        weapon_attack_range = Math.Max(GetInt(projection, "weapon_attack_range", 0), 0);
        weapon_one_handed_dice = _normalize_weapon_dice(GetVariant(projection, "weapon_one_handed_dice", new GDictionary()));
        weapon_two_handed_dice = _normalize_weapon_dice(GetVariant(projection, "weapon_two_handed_dice", new GDictionary()));
        weapon_is_versatile = GetBool(projection, "weapon_is_versatile", false);
        weapon_uses_two_hands = GetBool(projection, "weapon_uses_two_hands", weapon_current_grip == WeaponGripTwoHanded);
        if (weapon_uses_two_hands)
        {
            weapon_current_grip = WeaponGripTwoHanded;
        }
        else if (projection.ContainsKey("weapon_uses_two_hands") && weapon_current_grip == WeaponGripTwoHanded)
        {
            weapon_current_grip = weapon_one_handed_dice.Count > 0 ? WeaponGripOneHanded : WeaponGripNone;
        }
        weapon_physical_damage_tag = ToStringName(GetVariant(projection, "weapon_physical_damage_tag", ""));
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
        Variant effectVariant = status_effects[normalized];
        BattleStatusEffectState effectState = null;
        if (effectVariant.VariantType == Variant.Type.Object)
        {
            effectState = effectVariant.AsGodotObject() as BattleStatusEffectState;
        }
        if (effectState != null && !effectState.is_empty())
        {
            return effectState;
        }
        effectState = BattleStatusEffectState.from_dict(effectVariant);
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
        foreach (Variant chargeKeyVariant in per_turn_charge_limits.Keys)
        {
            StringName chargeKey = ToStringName(chargeKeyVariant);
            int chargeLimit = Math.Max(GetVariantInt(per_turn_charge_limits[chargeKeyVariant]), 0);
            if (IsEmpty(chargeKey) || chargeLimit <= 0)
            {
                continue;
            }
            per_turn_charges[chargeKey] = chargeLimit;
        }
    }

    public BattleUnitState clone()
    {
        BattleUnitState clonedState = from_dict(to_dict());
        if (clonedState == null)
        {
            return null;
        }
        clonedState.per_battle_charges = DuplicateDictionary(per_battle_charges, true);
        clonedState.per_turn_charges = DuplicateDictionary(per_turn_charges, true);
        clonedState.per_turn_charge_limits = DuplicateDictionary(per_turn_charge_limits, true);
        clonedState.has_moved_this_turn = has_moved_this_turn;
        return clonedState;
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
            ["ai_blackboard"] = DuplicateDictionary(ai_blackboard, true),
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
            ["unlocked_combat_resource_ids"] = _string_name_array_to_strings(unlocked_combat_resource_ids),
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
            ["shield_params"] = DuplicateDictionary(shield_params, true),
            ["action_progress"] = action_progress,
            ["action_threshold"] = action_threshold,
            ["known_active_skill_ids"] = _string_name_array_to_strings(known_active_skill_ids),
            ["known_skill_level_map"] = StringNameIntMapToStringDict(known_skill_level_map),
            ["known_skill_lock_hit_bonus_map"] = StringNameIntMapToStringDict(known_skill_lock_hit_bonus_map),
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
            ["combo_state"] = DuplicateDictionary(combo_state, true),
        };
    }

    public static BattleUnitState from_dict(Variant data)
    {
        if (data.VariantType != Variant.Type.Dictionary)
        {
            return null;
        }
        GDictionary payload = data.AsGodotDictionary();
        if (payload.Count == 0)
        {
            return null;
        }
        if (!_has_exact_fields(payload, ToDictFields))
        {
            return null;
        }

        Variant coordValue = payload["coord"];
        Variant bodySizeValue = payload["body_size"];
        Variant bodySizeCategoryValue = payload["body_size_category"];
        Variant footprintSizeValue = payload["footprint_size"];
        Variant occupiedCoordsValue = payload["occupied_coords"];
        if (coordValue.VariantType != Variant.Type.Vector2I || bodySizeValue.VariantType != Variant.Type.Int || footprintSizeValue.VariantType != Variant.Type.Vector2I)
        {
            return null;
        }
        int bodySizeInt = bodySizeValue.AsInt32();
        if (bodySizeInt < 1)
        {
            return null;
        }
        if (!_is_non_empty_string_name_payload_value(bodySizeCategoryValue))
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
        GVector2IArray expectedOccupied = _build_occupied_coords(coordValue.AsVector2I(), expectedFootprint);
        if (footprintSizeValue.AsVector2I() != expectedFootprint)
        {
            return null;
        }
        if (occupiedCoordsValue.VariantType != Variant.Type.Array)
        {
            return null;
        }
        GVector2IArray parsedOccupiedCoords = new();
        foreach (Variant coordVariant in occupiedCoordsValue.AsGodotArray())
        {
            if (coordVariant.VariantType != Variant.Type.Vector2I)
            {
                return null;
            }
            parsedOccupiedCoords.Add(coordVariant.AsVector2I());
        }
        if (!Vector2IArraysEqual(parsedOccupiedCoords, expectedOccupied))
        {
            return null;
        }

        foreach (string fieldName in new[] { "unit_id", "display_name", "faction_id", "control_mode" })
        {
            if (!_is_non_empty_string_name_payload_value(payload[fieldName]))
            {
                return null;
            }
        }
        foreach (string fieldName in new[]
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
        })
        {
            if (!_is_string_name_payload_value(payload[fieldName]))
            {
                return null;
            }
        }
        foreach (string fieldName in new[]
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
        })
        {
            if (payload[fieldName].VariantType != Variant.Type.Int)
            {
                return null;
            }
        }
        if (payload["current_move_points"].AsInt32() < 0)
        {
            return null;
        }
        foreach (string fieldName in new[]
        {
            "is_alive",
            "is_resting",
            "has_taken_action_this_turn",
            "can_use_locked_move_points_this_turn",
            "weapon_is_versatile",
            "weapon_uses_two_hands",
        })
        {
            if (payload[fieldName].VariantType != Variant.Type.Bool)
            {
                return null;
            }
        }
        foreach (string fieldName in new[]
        {
            "ai_blackboard",
            "attribute_snapshot",
            "equipment_view",
            "shield_params",
            "weapon_one_handed_dice",
            "weapon_two_handed_dice",
            "cooldowns",
            "known_skill_level_map",
            "known_skill_lock_hit_bonus_map",
            "status_effects",
            "combo_state",
            "damage_resistances",
        })
        {
            if (payload[fieldName].VariantType != Variant.Type.Dictionary)
            {
                return null;
            }
        }

        GodotObject parsedAttributeSnapshot = _attribute_snapshot_from_dict(payload["attribute_snapshot"]);
        if (parsedAttributeSnapshot == null)
        {
            return null;
        }
        GDictionary parsedKnownSkillLevelMap = _string_name_int_map_from_dict(payload["known_skill_level_map"], true);
        if (parsedKnownSkillLevelMap == null)
        {
            return null;
        }
        GDictionary parsedKnownSkillLockHitBonusMap = _string_name_int_map_from_dict(payload["known_skill_lock_hit_bonus_map"], true);
        if (parsedKnownSkillLockHitBonusMap == null)
        {
            return null;
        }
        foreach (Variant skillId in parsedKnownSkillLockHitBonusMap.Keys)
        {
            if (GetVariantInt(parsedKnownSkillLockHitBonusMap[skillId]) < 0)
            {
                return null;
            }
        }
        GStringNameArray parsedUnlockedResources = _combat_resource_array_from_payload(payload["unlocked_combat_resource_ids"]);
        if (parsedUnlockedResources.Count == 0)
        {
            return null;
        }
        GStringNameArray parsedKnownActiveSkillIds = _unique_string_name_array_from_payload(payload["known_active_skill_ids"]);
        if (parsedKnownActiveSkillIds == null)
        {
            return null;
        }
        GStringNameArray parsedMovementTags = _unique_string_name_array_from_payload(payload["movement_tags"]);
        if (parsedMovementTags == null)
        {
            return null;
        }
        GStringNameArray parsedVisionTags = _unique_string_name_array_from_payload(payload["vision_tags"]);
        if (parsedVisionTags == null)
        {
            return null;
        }
        GStringNameArray parsedProficiencyTags = _unique_string_name_array_from_payload(payload["proficiency_tags"]);
        if (parsedProficiencyTags == null)
        {
            return null;
        }
        GStringNameArray parsedSaveAdvantageTags = _unique_string_name_array_from_payload(payload["save_advantage_tags"]);
        if (parsedSaveAdvantageTags == null)
        {
            return null;
        }
        GStringNameArray parsedRaceTraitIds = _unique_string_name_array_from_payload(payload["race_trait_ids"]);
        if (parsedRaceTraitIds == null)
        {
            return null;
        }
        GStringNameArray parsedSubraceTraitIds = _unique_string_name_array_from_payload(payload["subrace_trait_ids"]);
        if (parsedSubraceTraitIds == null)
        {
            return null;
        }
        GStringNameArray parsedAscensionTraitIds = _unique_string_name_array_from_payload(payload["ascension_trait_ids"]);
        if (parsedAscensionTraitIds == null)
        {
            return null;
        }
        GStringNameArray parsedBloodlineTraitIds = _unique_string_name_array_from_payload(payload["bloodline_trait_ids"]);
        if (parsedBloodlineTraitIds == null)
        {
            return null;
        }
        GDictionary parsedDamageResistances = _damage_resistance_map_from_dict(payload["damage_resistances"]);
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
        GDictionary parsedWeaponOneHandedDice = _strict_weapon_dice_from_dict(payload["weapon_one_handed_dice"]);
        if (parsedWeaponOneHandedDice == null)
        {
            return null;
        }
        GDictionary parsedWeaponTwoHandedDice = _strict_weapon_dice_from_dict(payload["weapon_two_handed_dice"]);
        if (parsedWeaponTwoHandedDice == null)
        {
            return null;
        }

        GodotObject parsedEquipmentState = EquipmentFromDict(payload["equipment_view"].AsGodotDictionary());
        if (parsedEquipmentState == null)
        {
            return null;
        }
        GDictionary parsedStatusEffects = _status_effects_from_dict(payload["status_effects"]);
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
            ai_blackboard = DuplicateDictionary(payload["ai_blackboard"].AsGodotDictionary(), true),
            coord = coordValue.AsVector2I(),
            body_size = bodySizeInt,
            body_size_category = parsedBodySizeCategory,
            footprint_size = footprintSizeValue.AsVector2I(),
            occupied_coords = parsedOccupiedCoords,
            is_alive = payload["is_alive"].AsBool(),
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
            is_resting = payload["is_resting"].AsBool(),
            has_taken_action_this_turn = payload["has_taken_action_this_turn"].AsBool(),
            can_use_locked_move_points_this_turn = payload["can_use_locked_move_points_this_turn"].AsBool(),
            current_shield_hp = payload["current_shield_hp"].AsInt32(),
            shield_max_hp = payload["shield_max_hp"].AsInt32(),
            shield_duration = payload["shield_duration"].AsInt32(),
            shield_family = ToStringName(payload["shield_family"]),
            shield_source_unit_id = ToStringName(payload["shield_source_unit_id"]),
            shield_source_skill_id = ToStringName(payload["shield_source_skill_id"]),
            shield_params = DuplicateDictionary(payload["shield_params"].AsGodotDictionary(), true),
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
            weapon_is_versatile = payload["weapon_is_versatile"].AsBool(),
            weapon_uses_two_hands = payload["weapon_uses_two_hands"].AsBool(),
            weapon_physical_damage_tag = ToStringName(payload["weapon_physical_damage_tag"]),
            cooldowns = DuplicateDictionary(payload["cooldowns"].AsGodotDictionary(), true),
            last_turn_tu = payload["last_turn_tu"].AsInt32(),
            status_effects = parsedStatusEffects,
            combo_state = DuplicateDictionary(payload["combo_state"].AsGodotDictionary(), true),
        };
        unitState.attribute_snapshot.Call("set_value", Variant.From("aura_max"), payload["aura_max"].AsInt32());
        unitState.normalize_shield_state();
        unitState.refresh_footprint();
        return unitState;
    }

    public static GodotObject _attribute_snapshot_from_dict(Variant data)
    {
        if (data.VariantType != Variant.Type.Dictionary)
        {
            return null;
        }
        GodotObject snapshot = NewAttributeSnapshot();
        if (snapshot == null)
        {
            return null;
        }
        GDictionary values = data.AsGodotDictionary();
        foreach (Variant key in values.Keys)
        {
            if (!_is_string_name_payload_value(key))
            {
                return null;
            }
            if (values[key].VariantType != Variant.Type.Int)
            {
                return null;
            }
            snapshot.Call("set_value", ToStringName(key), values[key].AsInt32());
        }
        return snapshot;
    }

    public static GDictionary _status_effects_from_dict(Variant data)
    {
        GDictionary results = new();
        if (data.VariantType != Variant.Type.Dictionary)
        {
            return null;
        }
        GDictionary values = data.AsGodotDictionary();
        foreach (Variant statusKey in values.Keys)
        {
            if (!_is_non_empty_string_name_payload_value(statusKey))
            {
                return null;
            }
            BattleStatusEffectState effectState = BattleStatusEffectState.from_dict(values[statusKey]);
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
        foreach (Variant key in data.Keys)
        {
            if (!_is_string_name_payload_value(key))
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

    public static bool _is_string_name_payload_value(Variant value)
    {
        return value.VariantType == Variant.Type.String || value.VariantType == Variant.Type.StringName;
    }

    public static bool _is_non_empty_string_name_payload_value(Variant value)
    {
        return _is_string_name_payload_value(value) && !IsEmpty(ToStringName(value));
    }

    public static GDictionary _string_name_int_map_from_dict(Variant data, bool require_non_empty_key)
    {
        if (data.VariantType != Variant.Type.Dictionary)
        {
            return null;
        }
        GDictionary result = new();
        GDictionary values = data.AsGodotDictionary();
        foreach (Variant key in values.Keys)
        {
            if (!_is_string_name_payload_value(key))
            {
                return null;
            }
            StringName keyName = ToStringName(key);
            if (require_non_empty_key && IsEmpty(keyName))
            {
                return null;
            }
            if (values[key].VariantType != Variant.Type.Int)
            {
                return null;
            }
            result[keyName] = values[key].AsInt32();
        }
        return result;
    }

    public static GDictionary _damage_resistance_map_from_dict(Variant data)
    {
        if (data.VariantType != Variant.Type.Dictionary)
        {
            return null;
        }
        GDictionary result = new();
        GDictionary values = data.AsGodotDictionary();
        foreach (Variant key in values.Keys)
        {
            if (!_is_non_empty_string_name_payload_value(key))
            {
                return null;
            }
            StringName damageTag = ToStringName(key);
            if (result.ContainsKey(damageTag))
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

    public static GStringNameArray _unique_string_name_array_from_payload(Variant values)
    {
        if (values.VariantType != Variant.Type.Array)
        {
            return null;
        }
        GStringNameArray result = new();
        HashSet<StringName> seen = new();
        foreach (Variant value in values.AsGodotArray())
        {
            if (!_is_non_empty_string_name_payload_value(value))
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

    public static GStringNameArray _combat_resource_array_from_payload(Variant values)
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
        return value == WeaponGripNone || value == WeaponGripOneHanded || value == WeaponGripTwoHanded;
    }

    public static GDictionary _strict_weapon_dice_from_dict(Variant data)
    {
        if (data.VariantType != Variant.Type.Dictionary)
        {
            return null;
        }
        GDictionary diceData = data.AsGodotDictionary();
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
            if (diceData[fieldName].VariantType != Variant.Type.Int)
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

    public static StringName _normalize_weapon_profile_kind(Variant value)
    {
        StringName normalized = ToStringName(value);
        if (normalized == WeaponProfileKindUnarmed || normalized == WeaponProfileKindNatural || normalized == WeaponProfileKindEquipped)
        {
            return normalized;
        }
        return WeaponProfileKindNone;
    }

    public static StringName _normalize_weapon_grip(Variant value)
    {
        StringName normalized = ToStringName(value);
        if (normalized == WeaponGripOneHanded || normalized == WeaponGripTwoHanded)
        {
            return normalized;
        }
        return WeaponGripNone;
    }

    public static GDictionary _normalize_weapon_dice(Variant value)
    {
        if (value.VariantType != Variant.Type.Dictionary)
        {
            return new GDictionary();
        }
        GDictionary diceData = value.AsGodotDictionary();
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
        foreach (Variant key in values.Keys)
        {
            results[key.AsString()] = values[key].AsString();
        }
        return results;
    }

    public static GStringNameArray _strings_to_string_name_array(Variant values)
    {
        GStringNameArray results = new();
        if (values.VariantType != Variant.Type.Array)
        {
            return results;
        }
        foreach (Variant value in values.AsGodotArray())
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

    private static StringName ToStringName(Variant value)
    {
        if (value.VariantType == Variant.Type.Nil)
        {
            return "";
        }
        string text = value.AsString();
        if (text == "<null>")
        {
            return "";
        }
        text = text.Trim();
        return string.IsNullOrEmpty(text) ? "" : new StringName(text);
    }

    private static Variant GetVariant(GDictionary values, Variant key, Variant fallback)
    {
        return values != null && values.ContainsKey(key) ? values[key] : fallback;
    }

    private static int GetInt(GDictionary values, Variant key, int fallback)
    {
        if (values == null || !values.ContainsKey(key))
        {
            return fallback;
        }
        Variant value = values[key];
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : fallback;
    }

    private static bool GetBool(GDictionary values, Variant key, bool fallback)
    {
        if (values == null || !values.ContainsKey(key))
        {
            return fallback;
        }
        Variant value = values[key];
        return value.VariantType == Variant.Type.Bool ? value.AsBool() : fallback;
    }

    private static int GetVariantInt(Variant value)
    {
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : 0;
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

    private static GDictionary StringNameIntMapToStringDict(GDictionary values)
    {
        GDictionary result = new();
        if (values == null)
        {
            return result;
        }
        foreach (Variant key in values.Keys)
        {
            result[key.AsString()] = GetVariantInt(values[key]);
        }
        return result;
    }

    private static GDictionary AttributeSnapshotToDict(GodotObject snapshot)
    {
        if (snapshot == null || !snapshot.HasMethod("to_dict"))
        {
            return new GDictionary();
        }
        Variant payload = snapshot.Call("to_dict");
        return payload.VariantType == Variant.Type.Dictionary ? payload.AsGodotDictionary() : new GDictionary();
    }

    private static GDictionary EquipmentViewToDict(GodotObject view)
    {
        if (view == null || !view.HasMethod("to_dict"))
        {
            return new GDictionary();
        }
        Variant payload = view.Call("to_dict");
        return payload.VariantType == Variant.Type.Dictionary ? payload.AsGodotDictionary() : new GDictionary();
    }

    private static GodotObject EquipmentFromDict(GDictionary payload)
    {
        if (EquipmentStateScript == null)
        {
            return null;
        }
        Variant restored = EquipmentStateScript.Call("from_dict", payload);
        return restored.VariantType == Variant.Type.Object ? restored.AsGodotObject() : null;
    }

    private static GodotObject NewAttributeSnapshot()
    {
        return AttributeSnapshotScript?.New().AsGodotObject();
    }

    private static GodotObject NewEquipmentState()
    {
        return EquipmentStateScript?.New().AsGodotObject();
    }

    private static bool IsValidBodySizeCategory(StringName category)
    {
        string text = category.ToString();
        return text == "tiny" || text == "small" || text == "medium" || text == "large" || text == "huge" || text == "gargantuan" || text == "boss";
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

    private static StringName GetCategoryForBodySize(int size)
    {
        return size switch
        {
            BodySizeTiny => "small",
            BodySizeLarge => "large",
            BodySizeHuge => "huge",
            BodySizeGargantuan => "gargantuan",
            BodySizeBoss => "boss",
            _ => "medium",
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
        foreach (Variant key in values.Keys)
        {
            result.Add(key.AsString());
        }
        result.Sort(StringComparer.Ordinal);
        return result;
    }
}
