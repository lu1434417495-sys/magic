using System;
using System.Collections.Generic;
using Godot;
using VT = Godot.Variant.Type;

[GlobalClass]
public partial class EnemyTemplateDef : Resource
{
    private static readonly StringName DROP_TYPE_ITEM = "item",
        DROP_TYPE_RANDOM_EQUIPMENT = "random_equipment";
    private static readonly StringName[] UNSUPPORTED_WEAPON_ATTRIBUTE_OVERRIDE_KEYS =
    {
        "weapon_attack_range",
        "weapon_physical_damage_tag",
    };
    private static readonly StringName TagBeast = "beast",
        NATURAL_WEAPON_PROFILE_TYPE_ID = "natural_weapon",
        NATURAL_WEAPON_DEFAULT_DAMAGE_TAG = "physical_blunt";
    private static readonly StringName[] BaseAttributeIds =
    {
        "strength",
        "agility",
        "constitution",
        "perception",
        "intelligence",
        "willpower",
    };
    private const int NATURAL_WEAPON_DEFAULT_ATTACK_RANGE = 1;

    [Export]
    public StringName template_id { get; set; } = "";

    [Export]
    public string display_name { get; set; } = "";

    [Export]
    public Texture2D battle_sprite_texture { get; set; }

    [Export]
    public StringName brain_id { get; set; } = "";

    [Export]
    public StringName initial_state_id { get; set; } = "";

    [Export]
    public int enemy_count { get; set; } = 1;

    [Export]
    public int body_size { get; set; } = BattleUnitState.BodySizeMedium;

    [Export]
    public int creature_level { get; set; } = 1;

    [Export]
    public int hit_die_sides { get; set; } = 8;

    [Export]
    public int action_threshold { get; set; } = BattleUnitState.DefaultActionThreshold;

    [Export]
    public Godot.Collections.Array<StringName> tags { get; set; } = new();

    [Export]
    public Godot.Collections.Array<StringName> save_advantage_tags { get; set; } = new();

    [Export]
    public Godot.Collections.Array<StringName> save_disadvantage_tags { get; set; } = new();

    [Export]
    public Godot.Collections.Array<StringName> save_immunity_tags { get; set; } = new();

    [Export]
    public Godot.Collections.Dictionary damage_resistances { get; set; } = new();

    [Export]
    public StringName attack_equipment_item_id { get; set; } = "";

    [Export]
    public StringName natural_weapon_damage_tag { get; set; } = "";

    [Export]
    public int natural_weapon_attack_range { get; set; } = NATURAL_WEAPON_DEFAULT_ATTACK_RANGE;

    [Export]
    public Godot.Collections.Dictionary base_attribute_overrides { get; set; } = new();

    [Export]
    public Godot.Collections.Array<StringName> skill_ids { get; set; } = new();

    [Export]
    public Godot.Collections.Dictionary skill_level_map { get; set; } = new();

    [Export]
    public Godot.Collections.Dictionary attribute_overrides { get; set; } = new();

    [Export]
    public StringName target_rank { get; set; } = "normal";
    internal EnemyTargetRankKind TargetRankKind
    {
        get => BattleTypedNames.ToEnemyTargetRank(target_rank);
        set => target_rank = BattleTypedNames.ToStringName(value);
    }

    [Export]
    public Godot.Collections.Array<DropEntryDef> drop_entries { get; set; } = new();

    internal StringName GetInitialStateId(EnemyAiBrainDef brain)
    {
        if (initial_state_id != "")
            return initial_state_id;
        if (brain != null && brain.HasState(brain.default_state_id))
            return brain.default_state_id;
        return "engage";
    }

    internal bool HasTag(StringName tag) => tag != "" && tags.Contains(tag);

    private StringName GetAttackEquipmentItemIdResolved() =>
        ProgressionDataUtils.to_string_name(attack_equipment_item_id);

    internal WeaponProjection GetWeaponProjectionTyped(
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefinitions = null
    )
    {
        if (HasTag(TagBeast))
            return GetNaturalWeaponProjectionTyped();
        WeaponProjection aep = GetAttackEquipmentProjectionTyped(itemDefinitions);
        if (aep != null && !aep.IsEmpty())
            return aep;
        return GetUnarmedWeaponProjectionTyped();
    }

    internal WeaponProjection GetAttackEquipmentProjectionTyped(
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefinitions = null
    )
    {
        var itemId = GetAttackEquipmentItemIdResolved();
        if (itemId == "")
            return new WeaponProjection();
        ItemDefinition itemDefinition = _resolve_attack_equipment_item_definition(
            itemId,
            itemDefinitions
        );
        return itemDefinition != null
            ? _build_weapon_projection_from_item_definition(itemDefinition)
            : new WeaponProjection();
    }

    internal WeaponProjection GetNaturalWeaponProjectionTyped()
    {
        if (!HasTag(TagBeast))
            return new WeaponProjection();
        return new WeaponProjection
        {
            weapon_profile_kind = BattleUnitState.ToStringName(BattleWeaponProfileKind.Natural),
            weapon_profile_type_id = NATURAL_WEAPON_PROFILE_TYPE_ID,
            weapon_current_grip = BattleUnitState.ToStringName(BattleWeaponGripKind.OneHanded),
            weapon_attack_range = Mathf.Max(natural_weapon_attack_range, 1),
            weapon_one_handed_dice = _build_natural_weapon_dice(),
            weapon_physical_damage_tag = GetNaturalWeaponDamageTagResolved(),
        };
    }

    internal WeaponProjection GetUnarmedWeaponProjectionTyped()
    {
        return new WeaponProjection
        {
            weapon_profile_kind = BattleUnitState.ToStringName(BattleWeaponProfileKind.Unarmed),
            weapon_profile_type_id = "unarmed",
            weapon_current_grip = BattleUnitState.ToStringName(BattleWeaponGripKind.OneHanded),
            weapon_attack_range = 1,
            weapon_one_handed_dice = new WeaponDice
            {
                dice_count = 1,
                dice_sides = 4,
                flat_bonus = 0,
            },
            weapon_physical_damage_tag = "physical_blunt",
        };
    }

    private StringName GetNaturalWeaponDamageTagResolved()
    {
        var et = ProgressionDataUtils.to_string_name(natural_weapon_damage_tag);
        if (et != "")
            return et;
        foreach (var tt in tags)
        {
            var mt = _natural_weapon_damage_tag_for_template_tag(tt);
            if (mt != "")
                return mt;
        }
        return NATURAL_WEAPON_DEFAULT_DAMAGE_TAG;
    }

    internal IReadOnlyDictionary<StringName, int> GetBaseAttributeOverridesResolvedTyped()
    {
        var result = new Dictionary<StringName, int>();
        foreach (StringName attributeId in BaseAttributeIds)
        {
            if (TryGetDictionaryIntValue(base_attribute_overrides, attributeId, out int value))
            {
                result[attributeId] = value;
            }
        }
        return result;
    }

    internal IReadOnlyDictionary<StringName, int> GetAttributeOverridesTyped() =>
        BuildIntDictionary(attribute_overrides);

    internal EnemyTemplateDefinition ToDefinition(
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefinitions
    ) => EnemyTemplateDefinition.FromResource(this, itemDefinitions);

    internal int GetFootprintCellCountTyped()
    {
        Vector2I footprint = BattleUnitState.GetFootprintSizeForBodySize(
            Mathf.Max(body_size, 1)
        );
        return Mathf.Max(footprint.X * footprint.Y, 1);
    }

    internal int GetDerivedHpMaxTyped()
    {
        int level = Mathf.Max(creature_level, 0);
        int sides = Mathf.Max(hit_die_sides, 1);
        IReadOnlyDictionary<StringName, int> baseAttributes =
            GetBaseAttributeOverridesResolvedTyped();
        int constitution = baseAttributes.TryGetValue("constitution", out int conValue)
            ? conValue
            : 0;
        int constitutionModifier = AttributeSnapshot.CalculateScoreModifier(constitution);
        // 半点定点整数运算：×2 表示 0.5 粒度，末尾整除 2 取整。
        // 0 级即享受首级满骰底子；1 级起每级追加一颗骰均值 (sides+1)/2；每级最低半点 2（即 1 HP）。
        int firstLevelHalfPoints = Math.Max(2, (sides + constitutionModifier * 2) * 2);
        int perLevelHalfPoints = Math.Max(2, (sides + 1) + constitutionModifier * 4);
        int totalHalfPoints = firstLevelHalfPoints + perLevelHalfPoints * Math.Max(level - 1, 0);
        return (totalHalfPoints / 2) * GetFootprintCellCountTyped();
    }

    internal int GetDerivedAttackBonusTyped(
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefinitions = null
    )
    {
        WeaponProjection projection = GetWeaponProjectionTyped(itemDefinitions);
        int attackRange =
            projection != null && !projection.IsEmpty() ? projection.weapon_attack_range : 1;
        StringName abilityId = attackRange > 2 ? "perception" : "strength";
        IReadOnlyDictionary<StringName, int> baseAttributes =
            GetBaseAttributeOverridesResolvedTyped();
        int abilityScore = baseAttributes.TryGetValue(abilityId, out int scoreValue)
            ? scoreValue
            : 0;
        return AttributeSnapshot.CalculateScoreModifier(abilityScore);
    }

    internal IReadOnlyDictionary<StringName, StringName> GetDamageResistancesTyped()
    {
        var result = new Dictionary<StringName, StringName>();
        if (damage_resistances == null)
            return result;
        foreach (Variant rawKey in damage_resistances.Keys)
        {
            if (rawKey.VariantType != VT.StringName)
                continue;
            StringName damageTag = rawKey.AsStringName();
            Variant rawValue = damage_resistances[rawKey];
            if (rawValue.VariantType != VT.StringName)
                continue;
            StringName tier = rawValue.AsStringName();
            if (damageTag == "" || tier == "")
                continue;
            result[damageTag] = tier;
        }
        return result;
    }

    internal int GetSkillLevelTyped(StringName skillId, int fallback = 1)
    {
        if (skillId == "")
        {
            return fallback;
        }
        return TryGetDictionaryIntValue(skill_level_map, skillId, out int value) ? value : fallback;
    }

    internal Godot.Collections.Array<string> ValidateSchemaTyped(
        IReadOnlyDictionary<StringName, EnemyAiBrainDef> knownBrains = null,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefinitions = null,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions = null
    )
    {
        var errors = new Godot.Collections.Array<string>();
        if (template_id == "")
        {
            errors.Add("Enemy template is missing template_id.");
            return errors;
        }
        if (display_name.StripEdges().Length == 0)
            errors.Add($"Enemy template {template_id} is missing display_name.");
        EnemyAiBrainDef resolvedBrainForCompatibility = null;
        if (brain_id == "")
            errors.Add($"Enemy template {template_id} is missing brain_id.");
        else
        {
            knownBrains ??= new Dictionary<StringName, EnemyAiBrainDef>();
            var brain = knownBrains.TryGetValue(brain_id, out EnemyAiBrainDef resolvedBrain)
                ? resolvedBrain
                : null;
            resolvedBrainForCompatibility = brain;
            if (brain == null)
                errors.Add($"Enemy template {template_id} references missing brain {brain_id}.");
            else if (initial_state_id != "" && !brain.HasState(initial_state_id))
                errors.Add(
                    $"Enemy template {template_id} initial_state_id {initial_state_id} is not declared by brain {brain_id}."
                );
        }
        if (enemy_count <= 0)
            errors.Add($"Enemy template {template_id} must have enemy_count >= 1.");
        if (body_size <= 0)
            errors.Add($"Enemy template {template_id} must have body_size >= 1.");
        if (creature_level < 0)
            errors.Add($"Enemy template {template_id} must have creature_level >= 0.");
        if (
            hit_die_sides != 4
            && hit_die_sides != 6
            && hit_die_sides != 8
            && hit_die_sides != 10
            && hit_die_sides != 12
            && hit_die_sides != 20
        )
            errors.Add(
                $"Enemy template {template_id} hit_die_sides must be one of 4, 6, 8, 10, 12, 20; got {hit_die_sides}."
            );
        if (action_threshold <= 0)
            errors.Add($"Enemy template {template_id} action_threshold must be > 0.");
        else if (action_threshold % 5 != 0)
            errors.Add(
                $"Enemy template {template_id} action_threshold must be a multiple of 5 TU."
            );
        if (TargetRankKind == EnemyTargetRankKind.Unknown)
            errors.Add(
                $"Enemy template {template_id} target_rank must be normal, elite, or boss; got {target_rank}."
            );
        foreach (var e in _validate_save_advantage_tags())
            errors.Add(e);
        foreach (var e in _validate_damage_resistances())
            errors.Add(e);
        foreach (
            var e in _validate_int_dictionary_key_types(
                base_attribute_overrides,
                "base_attribute_overrides"
            )
        )
            errors.Add(e);
        foreach (var e in _validate_int_dictionary_key_types(attribute_overrides, "attribute_overrides"))
            errors.Add(e);
        foreach (var fk in new StringName[] { "boss_target", "fortune_mark_target" })
            if (_dictionary_has_unsupported_key(attribute_overrides, fk))
                errors.Add(
                    $"Enemy template {template_id} attribute_overrides must not declare {fk}; use target_rank instead."
                );
        if (_dictionary_has_unsupported_key(attribute_overrides, "armor_class"))
            errors.Add(
                $"Enemy template {template_id} must not declare attribute_overrides.armor_class; use base attributes and AC component bonuses."
            );
        skillDefinitions ??= new Dictionary<StringName, SkillDefinition>();
        itemDefinitions ??= new Dictionary<StringName, ItemDefinition>();
        HashSet<StringName> declaredSkillIds = _build_declared_skill_id_set();
        foreach (var e in _validate_template_skill_ids(skillDefinitions))
            errors.Add(e);
        foreach (var e in _validate_template_skill_level_map(skillDefinitions, declaredSkillIds))
            errors.Add(e);
        foreach (
            var e in _validate_brain_action_skill_levels(
                resolvedBrainForCompatibility,
                skillDefinitions,
                declaredSkillIds
            )
        )
            errors.Add(e);
        foreach (var uk in UNSUPPORTED_WEAPON_ATTRIBUTE_OVERRIDE_KEYS)
            if (_dictionary_has_unsupported_key(attribute_overrides, uk))
                errors.Add(
                    $"Enemy template {template_id} must not declare attribute_overrides.{uk}; use attack_equipment_item_id or beast natural weapon config."
                );
        IReadOnlyDictionary<StringName, int> eba = GetBaseAttributeOverridesResolvedTyped();
        foreach (StringName san in BaseAttributeIds)
        {
            if (!eba.ContainsKey(san))
                errors.Add($"Enemy template {template_id} is missing base attribute {san}.");
            else if (eba[san] <= 0)
                errors.Add($"Enemy template {template_id} base attribute {san} must be > 0.");
        }
        if (HasTag(TagBeast))
        {
            if (natural_weapon_attack_range < 1)
                errors.Add(
                    $"Enemy template {template_id} natural_weapon_attack_range must be >= 1."
                );
            var endt = ProgressionDataUtils.to_string_name(natural_weapon_damage_tag);
            if (endt != "" && !_is_valid_weapon_physical_damage_tag(endt))
                errors.Add(
                    $"Enemy template {template_id} natural_weapon_damage_tag {endt} is not supported."
                );
        }
        else
        {
            foreach (var e in _validate_attack_equipment(itemDefinitions))
                errors.Add(e);
        }
        foreach (var entry in drop_entries)
        {
            if (entry == null)
            {
                errors.Add($"Enemy template {template_id} contains a null drop entry.");
                continue;
            }
            var dei = ProgressionDataUtils.to_string_name(entry.drop_entry_id);
            var dt = ProgressionDataUtils.to_string_name(entry.drop_type);
            var ii = ProgressionDataUtils.to_string_name(entry.item_id);
            if (dei == "")
                errors.Add(
                    $"Enemy template {template_id} contains a drop entry without drop_entry_id."
                );
            if (dt != DROP_TYPE_ITEM && dt != DROP_TYPE_RANDOM_EQUIPMENT)
                errors.Add(
                    $"Enemy template {template_id} drop {dei} declares unsupported drop_type {dt}."
                );
            if (ii == "")
                errors.Add($"Enemy template {template_id} drop {dei} is missing item_id.");
            else if (_try_get_indexed_item_definition(ii, itemDefinitions) == null)
                errors.Add(
                    $"Enemy template {template_id} drop {dei} references missing item_id {ii}."
                );
            if (entry.quantity <= 0)
                errors.Add($"Enemy template {template_id} drop {dei} must have quantity >= 1.");
        }
        return errors;
    }

    private Godot.Collections.Array<string> _validate_damage_resistances()
    {
        var errors = new Godot.Collections.Array<string>();
        if (damage_resistances == null)
            return errors;
        foreach (Variant rawKey in damage_resistances.Keys)
        {
            if (rawKey.VariantType != VT.StringName)
            {
                errors.Add(
                    $"Enemy template {template_id} damage_resistances key {rawKey} must be a StringName."
                );
                continue;
            }
            StringName damageTag = rawKey.AsStringName();
            if (
                DamageTagContentRules.ToDamageTagKind(damageTag) == DamageTagKind.Unknown
            )
            {
                errors.Add(
                    $"Enemy template {template_id} damage_resistances declares unsupported damage tag {damageTag}; expected one of {DamageTagContentRules.ValidDamageTagLabel()}."
                );
            }
            Variant rawValue = damage_resistances[rawKey];
            if (rawValue.VariantType != VT.StringName)
            {
                errors.Add(
                    $"Enemy template {template_id} damage_resistances value for {rawKey} must be a StringName mitigation tier."
                );
                continue;
            }
            StringName tier = rawValue.AsStringName();
            if (
                DamageTagContentRules.ToMitigationTierKind(tier)
                == DamageMitigationTierKind.Unknown
            )
            {
                errors.Add(
                    $"Enemy template {template_id} damage_resistances tier {tier} for {damageTag} is not supported; expected one of {DamageTagContentRules.ValidMitigationTierLabel()}."
                );
            }
        }
        return errors;
    }

    private Godot.Collections.Array<string> _validate_save_advantage_tags()
    {
        var errors = new Godot.Collections.Array<string>();
        SaveTagListContentRules.AppendValidationErrors(
            errors,
            $"Enemy template {template_id}.save_advantage_tags",
            save_advantage_tags
        );
        SaveTagListContentRules.AppendValidationErrors(
            errors,
            $"Enemy template {template_id}.save_disadvantage_tags",
            save_disadvantage_tags
        );
        SaveTagListContentRules.AppendValidationErrors(
            errors,
            $"Enemy template {template_id}.save_immunity_tags",
            save_immunity_tags
        );
        return errors;
    }

    private Godot.Collections.Array<string> _validate_template_skill_ids(
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions
    )
    {
        var errors = new Godot.Collections.Array<string>();
        var seen = new HashSet<StringName>();
        foreach (var rsi in skill_ids)
        {
            StringName si = ProgressionDataUtils.to_string_name(rsi);
            if (si == "")
            {
                errors.Add($"Enemy template {template_id} contains an empty skill_id.");
                continue;
            }
            if (!seen.Add(si))
            {
                errors.Add($"Enemy template {template_id} declares duplicate skill_id {si}.");
                continue;
            }
            if (skillDefinitions == null || !skillDefinitions.ContainsKey(si))
                errors.Add($"Enemy template {template_id} references missing skill {si}.");
        }
        return errors;
    }

    private Godot.Collections.Array<string> _validate_brain_action_skill_levels(
        EnemyAiBrainDef brain,
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions,
        IReadOnlySet<StringName> declaredSkillIds
    )
    {
        var errors = new Godot.Collections.Array<string>();
        if (brain == null || skillDefinitions == null || declaredSkillIds == null)
            return errors;

        foreach (EnemyAiStateDef state in brain.GetResolvedStates())
        {
            if (state == null)
                continue;
            foreach (EnemyAiAction action in state.GetTypedActions())
            {
                if (action == null)
                    continue;
                foreach (StringName skillId in action.GetDeclaredSkillIds())
                {
                    if (
                        skillId == ""
                        || !declaredSkillIds.Contains(skillId)
                        || !skillDefinitions.TryGetValue(
                            skillId,
                            out SkillDefinition skillDefinition
                        )
                        || skillDefinition == null
                    )
                    {
                        continue;
                    }

                    EnemyAiActionSkillCompatibilityResult definitionCompatibility =
                        action.EvaluateSkillCompatibility(skillDefinition);
                    if (!definitionCompatibility.IsCompatible)
                    {
                        // Brain validation already owns level-invariant diagnostics.
                        continue;
                    }

                    if (
                        !_try_resolve_skill_level_for_compatibility(
                            skillId,
                            skillDefinition,
                            out int skillLevel
                        )
                    )
                    {
                        // The skill-level schema validator owns malformed authored values.
                        // Do not manufacture a second action-compatibility error from a
                        // fallback/clamped level that the template never validly declared.
                        continue;
                    }
                    EnemyAiActionSkillCompatibilityResult levelCompatibility =
                        action.EvaluateSkillCompatibility(skillDefinition, skillLevel);
                    if (!levelCompatibility.IsCompatible)
                    {
                        errors.Add(
                            $"Enemy template {template_id} brain {brain.brain_id} state "
                                + $"{state.state_id} action {action.action_id} references "
                                + $"incompatible skill {skillId} at level {skillLevel}: "
                                + $"{levelCompatibility.Reason}."
                        );
                    }
                }
            }
        }
        return errors;
    }

    private bool _try_resolve_skill_level_for_compatibility(
        StringName skillId,
        SkillDefinition skillDefinition,
        out int skillLevel
    )
    {
        skillLevel = 1;
        if (skillId == "" || skill_level_map == null)
            return true;

        if (!skill_level_map.ContainsKey(skillId))
        {
            // A text-equivalent String key is malformed because this map requires
            // StringName keys. Let the schema diagnostic stand on its own instead
            // of treating the malformed entry as an absent level and falling back.
            string invalidStringKey = skillId.ToString();
            return !skill_level_map.ContainsKey(invalidStringKey);
        }

        Variant rawLevel = skill_level_map[skillId];
        if (rawLevel.VariantType != Variant.Type.Int)
            return false;

        int configuredLevel = rawLevel.AsInt32();
        if (
            configuredLevel < 1
            || (
                skillDefinition != null
                && skillDefinition.MaxLevel > 0
                && configuredLevel > skillDefinition.MaxLevel
            )
        )
        {
            return false;
        }

        skillLevel = configuredLevel;
        return true;
    }

    private Godot.Collections.Array<string> _validate_template_skill_level_map(
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions,
        IReadOnlySet<StringName> declaredSkillIds
    )
    {
        var errors = new Godot.Collections.Array<string>();
        foreach (var lk in skill_level_map.Keys)
        {
            var rlv = skill_level_map[lk];
            if (lk.VariantType != Variant.Type.StringName)
            {
                errors.Add(
                    $"Enemy template {template_id} skill_level_map key {lk} must be a StringName."
                );
                continue;
            }
            var si = lk.AsStringName();
            if (declaredSkillIds == null || !declaredSkillIds.Contains(si))
                errors.Add(
                    $"Enemy template {template_id} skill_level_map key {si} does not match any declared skill_id."
                );
            if (rlv.VariantType != Variant.Type.Int)
            {
                errors.Add($"Enemy template {template_id} skill_level_map[{si}] must be an int.");
                continue;
            }
            int lv = rlv.AsInt32();
            if (lv < 1)
                errors.Add($"Enemy template {template_id} skill_level_map[{si}] must be >= 1.");
            else if (
                skillDefinitions != null
                && skillDefinitions.TryGetValue(si, out SkillDefinition skillDefinition)
            )
            {
                if (
                    skillDefinition != null
                    && skillDefinition.MaxLevel > 0
                    && lv > skillDefinition.MaxLevel
                )
                    errors.Add(
                        $"Enemy template {template_id} skill_level_map[{si}] = {lv} exceeds skill max_level {skillDefinition.MaxLevel}."
                    );
            }
        }
        return errors;
    }

    private HashSet<StringName> _build_declared_skill_id_set()
    {
        var result = new HashSet<StringName>();
        foreach (var ri in skill_ids)
            result.Add(ProgressionDataUtils.to_string_name(ri));
        return result;
    }

    private Godot.Collections.Array<string> _validate_int_dictionary_key_types(
        Godot.Collections.Dictionary source,
        string fieldName
    )
    {
        var errors = new Godot.Collections.Array<string>();
        if (source == null)
            return errors;
        foreach (Variant rawKey in source.Keys)
        {
            if (rawKey.VariantType != VT.StringName)
            {
                errors.Add(
                    $"Enemy template {template_id} {fieldName} key {rawKey} must be a StringName."
                );
            }
        }
        return errors;
    }

    private Godot.Collections.Array<string> _validate_attack_equipment(
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefinitions
    )
    {
        var errors = new Godot.Collections.Array<string>();
        var iid = GetAttackEquipmentItemIdResolved();
        if (iid == "")
        {
            errors.Add(
                $"Enemy template {template_id} must declare attack_equipment_item_id for non-beast attack equipment."
            );
            return errors;
        }
        ItemDefinition itemDefinition = _try_get_indexed_item_definition(iid, itemDefinitions);
        if (itemDefinition == null)
        {
            errors.Add(
                $"Enemy template {template_id} references missing attack_equipment_item_id {iid}."
            );
            return errors;
        }
        if (!itemDefinition.IsWeapon())
            errors.Add(
                $"Enemy template {template_id} attack_equipment_item_id {iid} must reference a weapon equipment item."
            );
        if (itemDefinition.GetWeaponAttackRange() <= 0)
            errors.Add(
                $"Enemy template {template_id} attack_equipment_item_id {iid} must project weapon attack range >= 1."
            );
        if (itemDefinition.GetWeaponPhysicalDamageTag() == "")
            errors.Add(
                $"Enemy template {template_id} attack_equipment_item_id {iid} must project a weapon physical damage tag."
            );
        return errors;
    }

    private static ItemDefinition _resolve_attack_equipment_item_definition(
        StringName iid,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefinitions
    ) => _try_get_indexed_item_definition(iid, itemDefinitions);

    private static ItemDefinition _try_get_indexed_item_definition(
        StringName iid,
        IReadOnlyDictionary<StringName, ItemDefinition> itemDefinitions
    ) =>
        itemDefinitions != null
        && itemDefinitions.TryGetValue(iid, out ItemDefinition itemDefinition)
        && itemDefinition != null
            ? itemDefinition
            : null;

    internal static Dictionary<StringName, EnemyAiBrainDef> BuildBrainIndex(
        Godot.Collections.Dictionary knownBrains
    )
    {
        var result = new Dictionary<StringName, EnemyAiBrainDef>();
        if (knownBrains == null)
        {
            return result;
        }
        foreach (Variant rawKey in knownBrains.Keys)
        {
            if (rawKey.VariantType != VT.StringName)
            {
                continue;
            }
            EnemyAiBrainDef brain = knownBrains[rawKey].As<EnemyAiBrainDef>();
            if (brain == null)
            {
                continue;
            }
            StringName keyBrainId = rawKey.AsStringName();
            if (keyBrainId != "")
            {
                result[keyBrainId] = brain;
            }
        }
        return result;
    }

    internal static Dictionary<StringName, SkillDefinition> CloneSkillDefinitionIndex(
        IReadOnlyDictionary<StringName, SkillDefinition> skillDefinitions
    )
    {
        return skillDefinitions != null
            ? new Dictionary<StringName, SkillDefinition>(skillDefinitions)
            : new Dictionary<StringName, SkillDefinition>();
    }

    private static Dictionary<StringName, int> BuildIntDictionary(Godot.Collections.Dictionary source)
    {
        var result = new Dictionary<StringName, int>();
        if (source == null)
        {
            return result;
        }
        foreach (Variant rawKey in source.Keys)
        {
            if (rawKey.VariantType != VT.StringName)
            {
                continue;
            }
            StringName key = rawKey.AsStringName();
            if (key == "")
            {
                continue;
            }
            result[key] = TryReadIntLike(source[rawKey], out int value) ? value : 0;
        }
        return result;
    }

    private static bool TryGetDictionaryIntValue(
        Godot.Collections.Dictionary source,
        StringName key,
        out int value
    )
    {
        if (source != null && source.ContainsKey(key))
        {
            return TryReadIntLike(source[key], out value);
        }
        value = 0;
        return false;
    }

    private static bool TryGetDictionaryIntValue(
        Godot.Collections.Dictionary source,
        string key,
        out int value
    )
    {
        if (source != null && source.ContainsKey(key))
        {
            return TryReadIntLike(source[key], out value);
        }
        value = 0;
        return false;
    }

    private static bool TryReadIntLike(object rawValue, out int value)
    {
        if (rawValue is int intValue)
        {
            value = intValue;
            return true;
        }
        if (rawValue is long longValue)
        {
            value = (int)longValue;
            return true;
        }
        if (rawValue is Variant variant)
        {
            if (variant.VariantType == VT.Nil)
            {
                value = 0;
                return false;
            }
            value = variant.AsInt32();
            return true;
        }
        value = 0;
        return false;
    }

    private WeaponProjection _build_weapon_projection_from_item_definition(
        ItemDefinition itemDefinition
    )
    {
        if (itemDefinition == null || !itemDefinition.IsWeapon())
            return new WeaponProjection();
        WeaponProfileDefinition profile = itemDefinition.WeaponProfile;
        if (profile == null)
            return new WeaponProjection();
        WeaponDice ohd = WeaponDice.FromDefinition(profile.OneHandedDice);
        WeaponDice thd = WeaponDice.FromDefinition(profile.TwoHandedDice);
        var props = _weapon_profile_properties(profile);
        bool isV = props.Contains("versatile");
        bool ut = _resolve_weapon_uses_two_hands(itemDefinition, ohd, thd, isV);
        return new WeaponProjection
        {
            weapon_profile_kind = BattleUnitState.ToStringName(BattleWeaponProfileKind.Equipped),
            weapon_item_id = itemDefinition.ItemId,
            weapon_profile_type_id = ProgressionDataUtils.to_string_name(profile.WeaponTypeId),
            weapon_current_grip = _resolve_weapon_current_grip(ohd, thd, ut),
            weapon_attack_range = Mathf.Max(profile.AttackRange, 0),
            weapon_one_handed_dice = ohd,
            weapon_two_handed_dice = thd,
            weapon_is_versatile = isV,
            weapon_uses_two_hands = ut,
            weapon_physical_damage_tag = itemDefinition.GetWeaponPhysicalDamageTag(),
        };
    }

    private static bool _resolve_weapon_uses_two_hands(
        ItemDefinition itemDefinition,
        WeaponDice ohd,
        WeaponDice thd,
        bool isV
    )
    {
        if (itemDefinition == null)
            return false;
        if (
            itemDefinition
                .GetFinalOccupiedSlotIdsTyped("main_hand")
                .Contains(new StringName("off_hand"))
        )
            return true;
        if (ohd.IsEmpty() && !thd.IsEmpty())
            return true;
        return isV && !thd.IsEmpty();
    }

    private static StringName _resolve_weapon_current_grip(WeaponDice ohd, WeaponDice thd, bool ut)
    {
        if (ut)
            return BattleUnitState.ToStringName(BattleWeaponGripKind.TwoHanded);
        if (!ohd.IsEmpty())
            return BattleUnitState.ToStringName(BattleWeaponGripKind.OneHanded);
        if (!thd.IsEmpty())
            return BattleUnitState.ToStringName(BattleWeaponGripKind.TwoHanded);
        return BattleUnitState.ToStringName(BattleWeaponGripKind.None);
    }

    private static Godot.Collections.Array<StringName> _weapon_profile_properties(
        WeaponProfileDefinition profile
    )
    {
        var r = new Godot.Collections.Array<StringName>();
        if (profile == null)
            return r;
        foreach (var rp in profile.GetPropertiesTyped())
        {
            var pid = ProgressionDataUtils.to_string_name(rp);
            if (pid != "" && !r.Contains(pid))
                r.Add(pid);
        }
        return r;
    }

    private static WeaponDice _build_natural_weapon_dice() =>
        new WeaponDice
        {
            dice_count = 1,
            dice_sides = 6,
            flat_bonus = 0,
        };

    private static StringName _natural_weapon_damage_tag_for_template_tag(StringName tag)
    {
        var n = (string)tag;
        if (n == "bite" || n == "sting" || n == "horn")
            return "physical_pierce";
        if (n == "claw" || n == "tear")
            return "physical_slash";
        if (n == "slam" || n == "charge" || n == "trample")
            return "physical_blunt";
        return new StringName("");
    }

    private static bool _dictionary_has_unsupported_key(
        Godot.Collections.Dictionary data,
        StringName key
    ) => data.ContainsKey(key);

    private static bool _is_valid_weapon_physical_damage_tag(StringName dt) =>
        ItemDefinition.ToWeaponPhysicalDamageTagKind(dt)
        != WeaponPhysicalDamageTagKind.Unknown;
}
