using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

[Tool]
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
        AttributeContentRules.ArmorAcBonus,
        AttributeContentRules.ShieldAcBonus,
        AttributeContentRules.DodgeBonus,
        AttributeContentRules.DeflectionBonus,
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
    internal BattleUnitControlMode ControlModeKind
    {
        get => BattleTypedNames.ToControlMode(control_mode);
        set => control_mode = BattleTypedNames.ToStringName(value);
    }

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
    public int current_move_points = BattleUnitState.DefaultMovePointsPerTurn;

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

    // Combat resource ids (e.g. "mp", "aura") to unlock on top of the always-unlocked
    // defaults (hp, stamina). A sim unit given an mp pool still cannot spend it unless mp
    // is unlocked here — real units get this from progression/profession, which sim specs
    // do not run. Empty = defaults only (backward compatible).
    [Export]
    public GArray unlocked_combat_resource_ids = new();

    [Export]
    public Godot.Collections.Array<GDictionary> status_effects = new();

    [Export]
    public GDictionary weapon_projection = new();

    [Export]
    public GDictionary base_attributes = new();

    internal BattleSimUnitDefinition ToDefinition(
        StringName default_faction_id = default,
        StringName default_control_mode = default
    )
    {
        BattleUnitState projectedState = BuildProjectedRuntimeState(
            default_faction_id,
            default_control_mode
        );
        return BattleSimUnitDefinition.FromProjectedState(
            projectedState,
            $"BattleSimUnitSpec[{unit_id}]"
        );
    }

    private BattleUnitState BuildProjectedRuntimeState(
        StringName default_faction_id,
        StringName default_control_mode
    )
    {
        if (IsEmpty(default_control_mode))
        {
            default_control_mode = BattleTypedNames.ControlModeManual;
        }
        BattleUnitControlMode resolvedControlMode = !IsEmpty(control_mode)
            ? ControlModeKind
            : BattleTypedNames.ToControlMode(default_control_mode);

        var unitState = new BattleUnitState
        {
            unit_id = unit_id,
            source_member_id = source_member_id,
            display_name = !string.IsNullOrEmpty(display_name) ? display_name : unit_id.ToString(),
            faction_id = !IsEmpty(faction_id) ? faction_id : default_faction_id,
            ControlModeKind = resolvedControlMode,
            ai_brain_id = ai_brain_id,
            ai_state_id = ai_state_id,
        };

        if (!unitState.SetBodySizeCategory(body_size_category))
        {
            throw new InvalidOperationException(
                $"BattleSimUnitSpec body_size_category '{body_size_category}' 非法。 " +
                $"合法值: tiny, small, medium, large, huge, gargantuan, boss"
            );
        }
        unitState.SetAnchorCoord(coord);
        ApplyAttributeDefaults(unitState);
        ApplyAttributeOverrides(unitState);
        unitState.SetCombatResources(
            Mathf.Clamp(
                current_hp,
                0,
                Mathf.Max(GetSnapshotValue(unitState.attribute_snapshot, HpMax), 1)
            ),
            Mathf.Clamp(
                current_mp,
                0,
                Mathf.Max(GetSnapshotValue(unitState.attribute_snapshot, MpMax), 0)
            ),
            Mathf.Clamp(
                current_stamina,
                0,
                Mathf.Max(GetSnapshotValue(unitState.attribute_snapshot, StaminaMax), 0)
            ),
            Mathf.Clamp(
                current_aura,
                0,
                Mathf.Max(GetSnapshotValue(unitState.attribute_snapshot, AuraMax), 0)
            ),
            Mathf.Clamp(
                current_ap,
                0,
                Mathf.Max(GetSnapshotValue(unitState.attribute_snapshot, ActionPoints), 1)
            ),
            Mathf.Clamp(
                current_move_points,
                0,
                BattleUnitState.DefaultMovePointsPerTurn
            )
        );
        unitState.action_threshold = ResolveActionThreshold(unitState);
        var normalizedSkillIds = new List<StringName>();
        foreach (var rawSkillId in skill_ids)
        {
            StringName skillId = ProgressionDataUtils.to_string_name(rawSkillId);
            if (IsEmpty(skillId))
            {
                continue;
            }
            normalizedSkillIds.Add(skillId);
        }
        unitState.SetKnownActiveSkillIds(normalizedSkillIds);
        foreach (StringName skillId in normalizedSkillIds)
            unitState.SetKnownSkillLevelTyped(skillId, GetSkillLevel(skillId), preserveZero: true);
        foreach (var rawTag in movement_tags)
        {
            StringName tag = ProgressionDataUtils.to_string_name(rawTag);
            if (IsEmpty(tag) || unitState.movement_tags.Contains(tag))
            {
                continue;
            }
            unitState.movement_tags.Add(tag);
        }
        foreach (var rawResourceId in unlocked_combat_resource_ids)
        {
            StringName resourceId = ProgressionDataUtils.to_string_name(rawResourceId);
            if (!IsEmpty(resourceId))
            {
                unitState.UnlockCombatResource(resourceId);
            }
        }
        foreach (GDictionary statusEntry in status_effects)
        {
            BattleStatusEffectState parsedStatus =
                BattleStatusEffectState.FromDictionary(statusEntry);
            if (parsedStatus == null || parsedStatus.IsEmpty())
            {
                continue;
            }
            unitState.SetStatusEffect(parsedStatus);
        }
        if (weapon_projection.Count > 0)
        {
            unitState.ApplyWeaponProjectionTyped(WeaponProjection.FromDictionary(weapon_projection));
        }
        return unitState;
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
            AttributeContentRules.ArmorAcBonus,
            GetAttributeOverride(AttributeContentRules.ArmorAcBonus, 0)
        );
        SetSnapshotValue(
            unitState.attribute_snapshot,
            AttributeContentRules.ShieldAcBonus,
            GetAttributeOverride(AttributeContentRules.ShieldAcBonus, 0)
        );
        SetSnapshotValue(
            unitState.attribute_snapshot,
            AttributeContentRules.DodgeBonus,
            GetAttributeOverride(AttributeContentRules.DodgeBonus, 0)
        );
        SetSnapshotValue(
            unitState.attribute_snapshot,
            AttributeContentRules.DeflectionBonus,
            GetAttributeOverride(AttributeContentRules.DeflectionBonus, 0)
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
            unitBaseAttributes?.SetAttributeValue(attributeId, GetBaseAttributeValue(attributeId, 10));
        }
        ApplyAcComponentOverridesToProgress(unitProgress);

        if (HasAttributeOverride(HpMax))
        {
            unitBaseAttributes?.SetAttributeValue(HpMax, GetAttributeOverride(HpMax, current_hp));
        }
        else
        {
            unitBaseAttributes?.SetAttributeValue(HpMax, CalculateInitialHpMax(constitution));
        }
        if (HasAttributeOverride(ActionThreshold))
        {
            unitBaseAttributes?.SetAttributeValue(
                ActionThreshold,
                GetAttributeOverride(ActionThreshold, action_threshold)
            );
        }

        var attributeService = new AttributeService();
        attributeService.Setup(unitProgress);
        return attributeService.GetSnapshot();
    }

    private static int CalculateInitialHpMax(int constitutionValue)
    {
        return Mathf.Max(
            1,
            InitialHpBase + AttributeSnapshot.CalculateScoreModifier(constitutionValue) * 2
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
                unitBaseAttributes.SetAttributeValue(
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
        snapshot?.SetValue(attributeId, value);
    }

    private static int GetSnapshotValue(AttributeSnapshot snapshot, StringName attributeId)
    {
        if (snapshot == null)
        {
            return 0;
        }
        return snapshot.GetValue(attributeId);
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
