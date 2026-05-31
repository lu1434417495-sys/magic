using System;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

[Tool]
[GlobalClass]
public partial class BattleSimUnitSpec : Resource
{
    private static readonly StringName HpMax = "hp_max";
    private static readonly StringName MpMax = "mp_max";
    private static readonly StringName StaminaMax = "stamina_max";
    private static readonly StringName AuraMax = "aura_max";
    private static readonly StringName ActionPoints = "action_points";
    private static readonly StringName ActionThreshold = "action_threshold";
    private static readonly StringName AttackBonus = "attack_bonus";
    private static readonly StringName ArmorClass = "armor_class";
    private static readonly StringName ArmorAcBonus = "armor_ac_bonus";
    private static readonly StringName ShieldAcBonus = "shield_ac_bonus";
    private static readonly StringName DodgeBonus = "dodge_bonus";
    private static readonly StringName DeflectionBonus = "deflection_bonus";
    private static readonly StringName SpellProficiencyBonus = "spell_proficiency_bonus";

    private static readonly StringName Strength = "strength";
    private static readonly StringName Agility = "agility";
    private static readonly StringName Constitution = "constitution";
    private static readonly StringName Perception = "perception";
    private static readonly StringName Intelligence = "intelligence";
    private static readonly StringName Willpower = "willpower";

    private static readonly StringName[] BaseAttributeIds =
    {
        Strength,
        Agility,
        Constitution,
        Perception,
        Intelligence,
        Willpower,
    };

    private static readonly StringName[] AcComponentAttributeIds =
    {
        ArmorAcBonus,
        ShieldAcBonus,
        DodgeBonus,
        DeflectionBonus,
    };

    private const int DefaultCharacterActionThreshold = 30;
    private const int InitialHpBase = 14;

    [Export]
    public StringName unit_id = "";

    [Export]
    public StringName source_member_id = "";

    [Export]
    public string display_name = "";

    [Export]
    public StringName faction_id = "";

    [Export]
    public StringName control_mode = "manual";

    [Export]
    public StringName ai_brain_id = "";

    [Export]
    public StringName ai_state_id = "";

    [Export]
    public Vector2I coord = Vector2I.Zero;

    [Export]
    public int body_size = 2;

    [Export]
    public StringName body_size_category = "medium";

    [Export]
    public int current_hp = 30;

    [Export]
    public int current_mp = 0;

    [Export]
    public int current_stamina = 0;

    [Export]
    public int current_aura = 0;

    [Export]
    public int current_ap = 1;

    [Export]
    public int current_move_points = BattleUnitState.DEFAULT_MOVE_POINTS_PER_TURN();

    [Export]
    public int action_threshold = DefaultCharacterActionThreshold;

    [Export]
    public GDictionary attribute_overrides = new();

    [Export]
    public GArray skill_ids = new();

    [Export]
    public GDictionary skill_level_map = new();

    [Export]
    public GArray movement_tags = new();

    [Export]
    public Godot.Collections.Array<GDictionary> status_effects = new();

    [Export]
    public GDictionary weapon_projection = new();

    [Export]
    public GDictionary base_attributes = new();

    public BattleUnitState to_battle_unit_state(
        StringName default_faction_id = default,
        StringName default_control_mode = default
    )
    {
        if (IsEmpty(default_control_mode))
        {
            default_control_mode = "manual";
        }

        var unitState = new BattleUnitState
        {
            unit_id = unit_id,
            source_member_id = source_member_id,
            display_name = !string.IsNullOrEmpty(display_name) ? display_name : unit_id.ToString(),
            faction_id = !IsEmpty(faction_id) ? faction_id : default_faction_id,
            control_mode = !IsEmpty(control_mode) ? control_mode : default_control_mode,
            ai_brain_id = ai_brain_id,
            ai_state_id = ai_state_id,
        };

        if (!unitState.set_body_size_category(body_size_category))
        {
            throw new InvalidOperationException(
                $"BattleSimUnitSpec body_size_category '{body_size_category}' 非法。 " +
                $"合法值: tiny, small, medium, large, huge, gargantuan, boss"
            );
        }
        unitState.set_anchor_coord(coord);
        ApplyAttributeDefaults(unitState);
        ApplyAttributeOverrides(unitState);
        unitState.current_hp = Mathf.Clamp(
            current_hp,
            0,
            Mathf.Max(GetSnapshotValue(unitState.attribute_snapshot, HpMax), 1)
        );
        unitState.current_mp = Mathf.Clamp(
            current_mp,
            0,
            Mathf.Max(GetSnapshotValue(unitState.attribute_snapshot, MpMax), 0)
        );
        unitState.current_stamina = Mathf.Clamp(
            current_stamina,
            0,
            Mathf.Max(GetSnapshotValue(unitState.attribute_snapshot, StaminaMax), 0)
        );
        unitState.current_aura = Mathf.Clamp(
            current_aura,
            0,
            Mathf.Max(GetSnapshotValue(unitState.attribute_snapshot, AuraMax), 0)
        );
        unitState.current_ap = Mathf.Clamp(
            current_ap,
            0,
            Mathf.Max(GetSnapshotValue(unitState.attribute_snapshot, ActionPoints), 1)
        );
        unitState.current_move_points = Mathf.Clamp(
            current_move_points,
            0,
            BattleUnitState.DEFAULT_MOVE_POINTS_PER_TURN()
        );
        unitState.action_threshold = ResolveActionThreshold(unitState);
        unitState.known_active_skill_ids.Clear();
        foreach (var rawSkillId in skill_ids)
        {
            StringName skillId = ProgressionDataUtils.to_string_name(rawSkillId);
            if (IsEmpty(skillId))
            {
                continue;
            }
            unitState.known_active_skill_ids.Add(skillId);
            unitState.known_skill_level_map[skillId] = GetSkillLevel(skillId);
        }
        foreach (var rawTag in movement_tags)
        {
            StringName tag = ProgressionDataUtils.to_string_name(rawTag);
            if (IsEmpty(tag) || unitState.movement_tags.Contains(tag))
            {
                continue;
            }
            unitState.movement_tags.Add(tag);
        }
        foreach (GDictionary statusEntry in status_effects)
        {
            StringName statusId = ReadStringName(statusEntry, "status_id");
            if (IsEmpty(statusId))
            {
                continue;
            }
            unitState.status_effects[statusId] = statusEntry.Duplicate(true);
        }
        if (weapon_projection.Count > 0)
        {
            unitState.apply_weapon_projection(weapon_projection);
        }
        unitState.is_alive = unitState.current_hp > 0;
        return unitState;
    }

    public GDictionary to_dict()
    {
        return to_battle_unit_state(faction_id, control_mode).to_dict();
    }

    private void ApplyAttributeDefaults(BattleUnitState unitState)
    {
        if (unitState?.attribute_snapshot == null)
        {
            return;
        }
        if (base_attributes.Count > 0)
        {
            unitState.attribute_snapshot = BuildFormalAttributeSnapshot();
            return;
        }

        SetSnapshotValue(unitState.attribute_snapshot, HpMax, Mathf.Max(current_hp, 1));
        SetSnapshotValue(unitState.attribute_snapshot, MpMax, Mathf.Max(current_mp, 0));
        SetSnapshotValue(unitState.attribute_snapshot, StaminaMax, Mathf.Max(current_stamina, 0));
        SetSnapshotValue(unitState.attribute_snapshot, AuraMax, Mathf.Max(current_aura, 0));
        SetSnapshotValue(unitState.attribute_snapshot, ActionPoints, Mathf.Max(current_ap, 1));
        SetSnapshotValue(
            unitState.attribute_snapshot,
            ActionThreshold,
            Mathf.Max(action_threshold, 1)
        );
        SetSnapshotValue(
            unitState.attribute_snapshot,
            AttackBonus,
            GetAttributeOverride("attack_bonus", 4)
        );
        SetSnapshotValue(
            unitState.attribute_snapshot,
            ArmorAcBonus,
            GetAttributeOverride("armor_ac_bonus", 0)
        );
        SetSnapshotValue(
            unitState.attribute_snapshot,
            ShieldAcBonus,
            GetAttributeOverride("shield_ac_bonus", 0)
        );
        SetSnapshotValue(
            unitState.attribute_snapshot,
            DodgeBonus,
            GetAttributeOverride("dodge_bonus", 0)
        );
        SetSnapshotValue(
            unitState.attribute_snapshot,
            DeflectionBonus,
            GetAttributeOverride("deflection_bonus", 0)
        );
        SetSnapshotValue(
            unitState.attribute_snapshot,
            SpellProficiencyBonus,
            GetAttributeOverride("spell_proficiency_bonus", 2)
        );
    }

    private AttributeSnapshot BuildFormalAttributeSnapshot()
    {
        var unitProgress = new UnitProgress();
        var unitBaseAttributes = unitProgress.unit_base_attributes;
        int constitution = GetBaseAttributeValue(Constitution, 10);
        foreach (StringName attributeId in BaseAttributeIds)
        {
            unitBaseAttributes?.set_attribute_value(attributeId, GetBaseAttributeValue(attributeId, 10));
        }
        ApplyAcComponentOverridesToProgress(unitProgress);

        if (HasAttributeOverride(HpMax))
        {
            unitBaseAttributes?.set_attribute_value(HpMax, GetAttributeOverride(HpMax, current_hp));
        }
        else
        {
            unitBaseAttributes?.set_attribute_value(HpMax, CalculateInitialHpMax(constitution));
        }
        if (HasAttributeOverride(ActionThreshold))
        {
            unitBaseAttributes?.set_attribute_value(
                ActionThreshold,
                GetAttributeOverride(ActionThreshold, action_threshold)
            );
        }

        var attributeService = new AttributeService();
        attributeService.setup(unitProgress);
        return attributeService.get_snapshot();
    }

    private static int CalculateInitialHpMax(int constitutionValue)
    {
        return Mathf.Max(
            1,
            InitialHpBase + AttributeSnapshot.calculate_score_modifier(constitutionValue) * 2
        );
    }

    private void ApplyAcComponentOverridesToProgress(UnitProgress unitProgress)
    {
        var unitBaseAttributes = unitProgress?.unit_base_attributes;
        if (unitBaseAttributes == null)
        {
            return;
        }
        foreach (StringName componentId in AcComponentAttributeIds)
        {
            if (HasAttributeOverride(componentId))
            {
                unitBaseAttributes.set_attribute_value(
                    componentId,
                    Mathf.Max(GetAttributeOverride(componentId, 0), 0)
                );
            }
        }
    }

    private void ApplyAttributeOverrides(BattleUnitState unitState)
    {
        if (unitState?.attribute_snapshot == null)
        {
            return;
        }
        foreach (var attributeKey in attribute_overrides.Keys)
        {
            StringName attributeId = ProgressionDataUtils.to_string_name(attributeKey);
            if (IsFormalAttributeOverride(attributeId))
            {
                continue;
            }
            SetSnapshotValue(
                unitState.attribute_snapshot,
                attributeId,
                ReadInt(attribute_overrides, attributeKey, 0)
            );
        }
    }

    private bool IsFormalAttributeOverride(StringName attributeId)
    {
        if (attributeId == ArmorClass)
        {
            return true;
        }
        if (base_attributes.Count == 0)
        {
            return false;
        }
        return attributeId == HpMax || attributeId == ActionThreshold;
    }

    private int GetBaseAttributeValue(StringName attributeId, int defaultValue)
    {
        if (TryRead(base_attributes, attributeId, out Variant value) && value.VariantType == Variant.Type.Int)
        {
            return value.AsInt32();
        }
        return defaultValue;
    }

    private bool HasAttributeOverride(StringName attributeId)
    {
        return TryRead(attribute_overrides, attributeId, out _);
    }

    private int GetAttributeOverride(StringName attributeId, int defaultValue)
    {
        return ReadInt(attribute_overrides, attributeId, defaultValue);
    }

    private int GetSkillLevel(StringName skillId)
    {
        if (
            TryRead(skill_level_map, skillId.ToString(), out Variant stringKeyValue)
            && stringKeyValue.VariantType == Variant.Type.Int
        )
        {
            return stringKeyValue.AsInt32();
        }
        if (
            TryRead(skill_level_map, skillId, out Variant stringNameKeyValue)
            && stringNameKeyValue.VariantType == Variant.Type.Int
        )
        {
            return stringNameKeyValue.AsInt32();
        }
        return 1;
    }

    private int ResolveActionThreshold(BattleUnitState unitState)
    {
        if (unitState?.attribute_snapshot != null)
        {
            int snapshotThreshold = GetSnapshotValue(unitState.attribute_snapshot, ActionThreshold);
            if (snapshotThreshold > 0)
            {
                return snapshotThreshold;
            }
        }
        return Mathf.Max(action_threshold, 1);
    }

    private static void SetSnapshotValue(AttributeSnapshot snapshot, StringName attributeId, int value)
    {
        snapshot?.set_value(attributeId, value);
    }

    private static int GetSnapshotValue(AttributeSnapshot snapshot, StringName attributeId)
    {
        if (snapshot == null)
        {
            return 0;
        }
        return snapshot.get_value(attributeId);
    }

    private static bool TryRead(GDictionary source, object key, out Variant value)
    {
        value = default;
        if (source == null || key == null)
            return false;
        Variant variantKey = key switch
        {
            Variant valueKey => valueKey,
            string stringKey => stringKey,
            StringName stringNameKey => stringNameKey,
            _ => default,
        };
        if (variantKey.VariantType == Variant.Type.Nil)
            return false;
        if (source.ContainsKey(variantKey))
        {
            value = source[variantKey];
            return value.VariantType != Variant.Type.Nil;
        }
        if (variantKey.VariantType == Variant.Type.String)
        {
            StringName stringNameKey = new(variantKey.AsString());
            if (source.ContainsKey(stringNameKey))
            {
                value = source[stringNameKey];
                return value.VariantType != Variant.Type.Nil;
            }
        }
        else if (variantKey.VariantType == Variant.Type.StringName)
        {
            string stringKey = variantKey.AsStringName().ToString();
            if (source.ContainsKey(stringKey))
            {
                value = source[stringKey];
                return value.VariantType != Variant.Type.Nil;
            }
        }
        return false;
    }

    private static int ReadInt(GDictionary source, object key, int fallback)
    {
        return TryRead(source, key, out Variant value) && value.VariantType == Variant.Type.Int
            ? value.AsInt32()
            : fallback;
    }

    private static StringName ReadStringName(GDictionary source, object key)
    {
        if (!TryRead(source, key, out Variant value))
            return "";
        return value.VariantType switch
        {
            Variant.Type.StringName => value.AsStringName(),
            Variant.Type.String => new StringName(value.AsString()),
            _ => new StringName(""),
        };
    }

    private static bool IsEmpty(StringName value)
    {
        return value == null || value.ToString().Length == 0;
    }
}
