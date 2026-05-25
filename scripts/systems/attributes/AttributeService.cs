using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class AttributeService : RefCounted
{
    public static readonly StringName HP_MAX = "hp_max";
    public static readonly StringName CHARACTER_HP_MAX_PERCENT_BONUS =
        "character_hp_max_percent_bonus";
    public static readonly StringName MP_MAX = "mp_max";
    public static readonly StringName STAMINA_MAX = "stamina_max";
    public static readonly StringName STAMINA_RECOVERY_PERCENT_BONUS =
        "stamina_recovery_percent_bonus";
    public static readonly StringName AURA_MAX = "aura_max";
    public static readonly StringName ACTION_POINTS = "action_points";
    public static readonly StringName ACTION_THRESHOLD = UnitBaseAttributes.ACTION_THRESHOLD;
    public static readonly StringName ATTACK_BONUS = "attack_bonus";
    public static readonly StringName WEAPON_ATTACK_RANGE = "weapon_attack_range";
    public static readonly StringName STRENGTH_MODIFIER = AttributeSnapshot.STRENGTH_MODIFIER();
    public static readonly StringName AGILITY_MODIFIER = AttributeSnapshot.AGILITY_MODIFIER();
    public static readonly StringName CONSTITUTION_MODIFIER =
        AttributeSnapshot.CONSTITUTION_MODIFIER();
    public static readonly StringName PERCEPTION_MODIFIER = AttributeSnapshot.PERCEPTION_MODIFIER();
    public static readonly StringName INTELLIGENCE_MODIFIER =
        AttributeSnapshot.INTELLIGENCE_MODIFIER();
    public static readonly StringName WILLPOWER_MODIFIER = AttributeSnapshot.WILLPOWER_MODIFIER();
    public static readonly StringName ARMOR_CLASS = "armor_class";
    public static readonly StringName ARMOR_AC_BONUS = "armor_ac_bonus";
    public static readonly StringName SHIELD_AC_BONUS = "shield_ac_bonus";
    public static readonly StringName DODGE_BONUS = "dodge_bonus";
    public static readonly StringName DEFLECTION_BONUS = "deflection_bonus";
    public static readonly StringName ARMOR_MAX_DEX_BONUS = "armor_max_dex_bonus";
    public static readonly StringName BASE_ATTACK_BONUS = AttributeSnapshot.BASE_ATTACK_BONUS();
    public static readonly StringName SPELL_PROFICIENCY_BONUS =
        AttributeSnapshot.SPELL_PROFICIENCY_BONUS();

    private const int DEFAULT_CHARACTER_ACTION_THRESHOLD = 30;
    private const int ACTION_THRESHOLD_GRANULARITY = 5;
    private const int BASE_ARMOR_CLASS = 8;

    public static readonly Godot.Collections.Array<StringName> RESOURCE_ATTRIBUTE_IDS = new()
    {
        HP_MAX,
        CHARACTER_HP_MAX_PERCENT_BONUS,
        MP_MAX,
        STAMINA_MAX,
        STAMINA_RECOVERY_PERCENT_BONUS,
        AURA_MAX,
        ACTION_POINTS,
        ACTION_THRESHOLD,
    };

    public static readonly Godot.Collections.Array<StringName> COMBAT_ATTRIBUTE_IDS = new()
    {
        ARMOR_CLASS,
        ARMOR_AC_BONUS,
        SHIELD_AC_BONUS,
        DODGE_BONUS,
        DEFLECTION_BONUS,
        ARMOR_MAX_DEX_BONUS,
    };

    public static readonly Godot.Collections.Array<StringName> AC_COMPONENT_ATTRIBUTE_IDS = new()
    {
        ARMOR_AC_BONUS,
        SHIELD_AC_BONUS,
        DODGE_BONUS,
        DEFLECTION_BONUS,
    };

    public static readonly Godot.Collections.Array<StringName> PROTECTED_CUSTOM_STAT_KEYS = new()
    {
        UnitBaseAttributes.HIDDEN_LUCK_AT_BIRTH,
    };

    public static readonly StringName PROTECTED_CUSTOM_STAT_SOURCE_CHARACTER_CREATION =
        "character_creation";
    public static readonly StringName PROTECTED_CUSTOM_STAT_SOURCE_STORY_SCRIPT = "story_script";
    public static readonly StringName PROTECTED_CUSTOM_STAT_WRITE_FLAG =
        "allow_protected_custom_stat_write";

    public static StringName HP_MAX_ID() => HP_MAX;

    public static StringName CHARACTER_HP_MAX_PERCENT_BONUS_ID() => CHARACTER_HP_MAX_PERCENT_BONUS;

    public static StringName MP_MAX_ID() => MP_MAX;

    public static StringName STAMINA_MAX_ID() => STAMINA_MAX;

    public static StringName STAMINA_RECOVERY_PERCENT_BONUS_ID() => STAMINA_RECOVERY_PERCENT_BONUS;

    public static StringName AURA_MAX_ID() => AURA_MAX;

    public static StringName ACTION_POINTS_ID() => ACTION_POINTS;

    public static StringName ACTION_THRESHOLD_ID() => ACTION_THRESHOLD;

    public static StringName ATTACK_BONUS_ID() => ATTACK_BONUS;

    public static StringName WEAPON_ATTACK_RANGE_ID() => WEAPON_ATTACK_RANGE;

    public static StringName STRENGTH_MODIFIER_ID() => STRENGTH_MODIFIER;

    public static StringName AGILITY_MODIFIER_ID() => AGILITY_MODIFIER;

    public static StringName CONSTITUTION_MODIFIER_ID() => CONSTITUTION_MODIFIER;

    public static StringName PERCEPTION_MODIFIER_ID() => PERCEPTION_MODIFIER;

    public static StringName INTELLIGENCE_MODIFIER_ID() => INTELLIGENCE_MODIFIER;

    public static StringName WILLPOWER_MODIFIER_ID() => WILLPOWER_MODIFIER;

    public static StringName ARMOR_CLASS_ID() => ARMOR_CLASS;

    public static StringName ARMOR_AC_BONUS_ID() => ARMOR_AC_BONUS;

    public static StringName SHIELD_AC_BONUS_ID() => SHIELD_AC_BONUS;

    public static StringName DODGE_BONUS_ID() => DODGE_BONUS;

    public static StringName DEFLECTION_BONUS_ID() => DEFLECTION_BONUS;

    public static StringName ARMOR_MAX_DEX_BONUS_ID() => ARMOR_MAX_DEX_BONUS;

    public static StringName BASE_ATTACK_BONUS_ID() => BASE_ATTACK_BONUS;

    public static StringName SPELL_PROFICIENCY_BONUS_ID() => SPELL_PROFICIENCY_BONUS;

    public static StringName PROTECTED_CUSTOM_STAT_SOURCE_CHARACTER_CREATION_ID() =>
        PROTECTED_CUSTOM_STAT_SOURCE_CHARACTER_CREATION;

    public static StringName PROTECTED_CUSTOM_STAT_SOURCE_STORY_SCRIPT_ID() =>
        PROTECTED_CUSTOM_STAT_SOURCE_STORY_SCRIPT;

    public static StringName PROTECTED_CUSTOM_STAT_WRITE_FLAG_ID() =>
        PROTECTED_CUSTOM_STAT_WRITE_FLAG;

    public static int DEFAULT_CHARACTER_ACTION_THRESHOLD_VALUE() =>
        DEFAULT_CHARACTER_ACTION_THRESHOLD;

    public static int BASE_ARMOR_CLASS_VALUE() => BASE_ARMOR_CLASS;

    public static Godot.Collections.Array<StringName> RESOURCE_ATTRIBUTE_IDS_ARRAY()
    {
        var result = new Godot.Collections.Array<StringName>();
        foreach (var key in RESOURCE_ATTRIBUTE_IDS)
            result.Add(key);
        return result;
    }

    public static Godot.Collections.Array<StringName> COMBAT_ATTRIBUTE_IDS_ARRAY()
    {
        var result = new Godot.Collections.Array<StringName>();
        foreach (var key in COMBAT_ATTRIBUTE_IDS)
            result.Add(key);
        return result;
    }

    public static Godot.Collections.Array<StringName> AC_COMPONENT_ATTRIBUTE_IDS_ARRAY()
    {
        var result = new Godot.Collections.Array<StringName>();
        foreach (var key in AC_COMPONENT_ATTRIBUTE_IDS)
            result.Add(key);
        return result;
    }

    public static Godot.Collections.Array<StringName> PROTECTED_CUSTOM_STAT_KEYS_ARRAY()
    {
        var result = new Godot.Collections.Array<StringName>();
        foreach (var key in PROTECTED_CUSTOM_STAT_KEYS)
            result.Add(key);
        return result;
    }

    private GodotObject _unit_progress;
    private GDictionary _skill_defs = new();
    private GDictionary _profession_defs = new();
    private Variant _equipment_state;
    private Variant _passive_state;
    private Variant _temporary_effects;
    private GDictionary _derived_rules = new();
    private AttributeSourceContext _context;
    private AttributeSnapshot _cached_snapshot;
    private bool _snapshot_dirty = true;

    public AttributeService()
    {
        _derived_rules = BuildDefaultRules();
    }

    public void setup(GodotObject unit_progress)
    {
        SetupInternal(unit_progress, default, default, default, default, default);
    }

    public void setup(GodotObject unit_progress, Variant skill_defs, Variant profession_defs)
    {
        SetupInternal(unit_progress, skill_defs, profession_defs, default, default, default);
    }

    public void setup(
        GodotObject unit_progress,
        Variant skill_defs,
        Variant profession_defs,
        Variant equipment_state
    )
    {
        SetupInternal(
            unit_progress,
            skill_defs,
            profession_defs,
            equipment_state,
            default,
            default
        );
    }

    public void setup(
        GodotObject unit_progress,
        Variant skill_defs,
        Variant profession_defs,
        Variant equipment_state,
        Variant passive_state,
        Variant temporary_effects
    )
    {
        SetupInternal(
            unit_progress,
            skill_defs,
            profession_defs,
            equipment_state,
            passive_state,
            temporary_effects
        );
    }

    private void SetupInternal(
        GodotObject unitProgress,
        Variant skillDefs,
        Variant professionDefs,
        Variant equipmentState,
        Variant passiveState,
        Variant temporaryEffects
    )
    {
        var context = new AttributeSourceContext
        {
            unit_progress = unitProgress,
            skill_defs = IndexSkillDefs(skillDefs),
            profession_defs = IndexProfessionDefs(professionDefs),
            equipment_state = equipmentState,
            passive_state = passiveState,
            temporary_effects = temporaryEffects,
        };
        setup_context(context);
    }

    public void setup_context(AttributeSourceContext context)
    {
        _context = context ?? new AttributeSourceContext();
        _unit_progress = _context.unit_progress;
        _skill_defs = IndexSkillDefs(Variant.From(_context.skill_defs));
        _profession_defs = IndexProfessionDefs(Variant.From(_context.profession_defs));
        _equipment_state = _context.equipment_state;
        _passive_state = _context.passive_state;
        _temporary_effects = _context.temporary_effects;
        invalidate_snapshot();
    }

    public void set_equipment_state(Variant equipment_state)
    {
        _equipment_state = equipment_state;
        if (_context != null)
            _context.equipment_state = equipment_state;
        invalidate_snapshot();
    }

    public void set_passive_state(Variant passive_state)
    {
        _passive_state = passive_state;
        if (_context != null)
            _context.passive_state = passive_state;
        invalidate_snapshot();
    }

    public void set_temporary_effects(Variant temporary_effects)
    {
        _temporary_effects = temporary_effects;
        if (_context != null)
            _context.temporary_effects = temporary_effects;
        invalidate_snapshot();
    }

    public void invalidate_snapshot()
    {
        _snapshot_dirty = true;
    }

    public int get_base_value(StringName attribute_id)
    {
        var unitBaseAttributes = GetUnitBaseAttributes();
        return unitBaseAttributes?.get_attribute_value(attribute_id) ?? 0;
    }

    public int get_total_value(StringName attribute_id)
    {
        return get_snapshot().get_value(attribute_id);
    }

    public int get_modifier(StringName attribute_id)
    {
        return CalculateScoreModifier(get_total_value(attribute_id));
    }

    public int get_action_points()
    {
        return get_total_value(ACTION_POINTS);
    }

    public AttributeSnapshot get_snapshot()
    {
        if (!_snapshot_dirty && _cached_snapshot != null)
            return _cached_snapshot;
        _cached_snapshot = BuildSnapshot();
        _snapshot_dirty = false;
        return _cached_snapshot;
    }

    private AttributeSnapshot BuildSnapshot()
    {
        var snapshot = new AttributeSnapshot();
        var modifierEntries = CollectAllModifierEntries();
        var resolvedBaseValues = ResolveBaseAttributeValues(modifierEntries);

        foreach (StringName attributeId in UnitBaseAttributes.BASE_ATTRIBUTE_IDS)
            snapshot.set_value(attributeId, GetDictInt(resolvedBaseValues, attributeId, 0));

        foreach (StringName attributeId in GetKnownNonBaseAttributeIds())
        {
            int derivedValue = GetPersistentBaseValue(attributeId);
            if (attributeId == HP_MAX)
            {
                derivedValue = CalculateCharacterHpMax(
                    GetPersistentBaseValue(HP_MAX),
                    modifierEntries
                );
            }
            else if (attributeId == CHARACTER_HP_MAX_PERCENT_BONUS)
            {
                snapshot.set_value(attributeId, ResolveCharacterHpMaxPercentBonus(modifierEntries));
                continue;
            }
            else if (attributeId == ARMOR_CLASS)
            {
                derivedValue = CalculateBaseArmorClass(resolvedBaseValues, modifierEntries);
            }
            else if (attributeId == ARMOR_MAX_DEX_BONUS)
            {
                snapshot.set_value(attributeId, ResolveArmorMaxDexBonus(modifierEntries));
                continue;
            }
            else if (_derived_rules.ContainsKey(attributeId))
            {
                var rule = _derived_rules[attributeId].AsGodotObject() as DerivedAttributeRule;
                if (rule != null)
                    derivedValue += rule.evaluate(resolvedBaseValues);
            }

            snapshot.set_value(
                attributeId,
                ApplyModifierPipeline(attributeId, derivedValue, modifierEntries)
            );
        }

        foreach (StringName attributeId in GetAdditionalAttributeIds(modifierEntries))
        {
            if (snapshot.has_value(attributeId))
                continue;
            snapshot.set_value(
                attributeId,
                ApplyModifierPipeline(
                    attributeId,
                    GetPersistentBaseValue(attributeId),
                    modifierEntries
                )
            );
        }

        snapshot.set_value(BASE_ATTACK_BONUS, CalculateBaseAttackBonus());
        snapshot.set_value(SPELL_PROFICIENCY_BONUS, CalculateSpellProficiencyBonus());
        return snapshot;
    }

    public bool apply_permanent_attribute_change(
        StringName attribute_id,
        int delta,
        GDictionary source_context
    )
    {
        var unitBaseAttributes = GetUnitBaseAttributes();
        if (unitBaseAttributes == null)
            return false;

        source_context ??= new GDictionary();
        if (IsProtectedCustomStat(attribute_id) && !CanWriteProtectedCustomStat(source_context))
        {
            GD.PushWarning(
                BuildProtectedCustomStatRejectionMessage(attribute_id, delta, source_context)
            );
            return false;
        }

        if (
            !UnitBaseAttributes.BASE_ATTRIBUTE_IDS.Contains(attribute_id)
            && !CanWriteCustomStat(attribute_id)
        )
        {
            GD.PushWarning(
                $"AttributeService: refuse permanent change for unsupported attribute {(string)attribute_id}."
            );
            return false;
        }

        unitBaseAttributes.set_attribute_value(
            attribute_id,
            unitBaseAttributes.get_attribute_value(attribute_id) + delta
        );
        invalidate_snapshot();
        return true;
    }

    private UnitBaseAttributes GetUnitBaseAttributes()
    {
        if (_unit_progress == null)
            return null;
        Variant value = _unit_progress.Get("unit_base_attributes");
        return value.VariantType == Variant.Type.Object
            ? value.AsGodotObject() as UnitBaseAttributes
            : null;
    }

    private bool IsProtectedCustomStat(StringName attributeId)
    {
        return !UnitBaseAttributes.BASE_ATTRIBUTE_IDS.Contains(attributeId)
            && PROTECTED_CUSTOM_STAT_KEYS.Contains(attributeId);
    }

    private bool CanWriteCustomStat(StringName attributeId)
    {
        if (attributeId == "" || UnitBaseAttributes.BASE_ATTRIBUTE_IDS.Contains(attributeId))
            return false;
        var unitBaseAttributes = GetUnitBaseAttributes();
        if (unitBaseAttributes == null)
            return false;
        return unitBaseAttributes.custom_stats.ContainsKey(attributeId)
            || PROTECTED_CUSTOM_STAT_KEYS.Contains(attributeId);
    }

    private static bool CanWriteProtectedCustomStat(GDictionary sourceContext)
    {
        var sourceType = ProgressionDataUtils.to_string_name(
            GetDictValue(sourceContext, "source_type", "")
        );
        if (sourceType == PROTECTED_CUSTOM_STAT_SOURCE_CHARACTER_CREATION)
            return true;
        if (sourceType != PROTECTED_CUSTOM_STAT_SOURCE_STORY_SCRIPT)
            return false;
        return GetDictBool(sourceContext, PROTECTED_CUSTOM_STAT_WRITE_FLAG, false);
    }

    private static string BuildProtectedCustomStatRejectionMessage(
        StringName attributeId,
        int delta,
        GDictionary sourceContext
    )
    {
        var sourceType = ProgressionDataUtils.to_string_name(
            GetDictValue(sourceContext, "source_type", "")
        );
        var sourceId = ProgressionDataUtils.to_string_name(
            GetDictValue(sourceContext, "source_id", "")
        );
        return $"AttributeService: reject protected custom stat write {(string)attributeId} delta={delta} source_type={(string)sourceType} source_id={(string)sourceId}.";
    }

    private static GDictionary IndexSkillDefs(Variant skillDefs)
    {
        var indexedDefs = new GDictionary();
        if (skillDefs.VariantType == Variant.Type.Dictionary)
        {
            var defs = skillDefs.AsGodotDictionary();
            foreach (Variant key in defs.Keys)
            {
                var skillDef = ToSkillDef(defs[key]);
                if (skillDef == null)
                    continue;
                var indexedId =
                    skillDef.skill_id != ""
                        ? skillDef.skill_id
                        : ProgressionDataUtils.to_string_name(key);
                indexedDefs[indexedId] = skillDef;
            }
        }
        else if (skillDefs.VariantType == Variant.Type.Array)
        {
            foreach (Variant rawDef in skillDefs.AsGodotArray())
            {
                var skillDef = ToSkillDef(rawDef);
                if (skillDef != null && skillDef.skill_id != "")
                    indexedDefs[skillDef.skill_id] = skillDef;
            }
        }
        return indexedDefs;
    }

    private static GDictionary IndexProfessionDefs(Variant professionDefs)
    {
        var indexedDefs = new GDictionary();
        if (professionDefs.VariantType == Variant.Type.Dictionary)
        {
            var defs = professionDefs.AsGodotDictionary();
            foreach (Variant key in defs.Keys)
            {
                var professionDef = ToProfessionDef(defs[key]);
                if (professionDef == null)
                    continue;
                var indexedId =
                    professionDef.profession_id != ""
                        ? professionDef.profession_id
                        : ProgressionDataUtils.to_string_name(key);
                indexedDefs[indexedId] = professionDef;
            }
        }
        else if (professionDefs.VariantType == Variant.Type.Array)
        {
            foreach (Variant rawDef in professionDefs.AsGodotArray())
            {
                var professionDef = ToProfessionDef(rawDef);
                if (professionDef != null && professionDef.profession_id != "")
                    indexedDefs[professionDef.profession_id] = professionDef;
            }
        }
        return indexedDefs;
    }

    private GDictionary ResolveBaseAttributeValues(GArray modifierEntries)
    {
        var resolvedValues = new GDictionary();
        foreach (StringName attributeId in UnitBaseAttributes.BASE_ATTRIBUTE_IDS)
            resolvedValues[attributeId] = ApplyModifierPipeline(
                attributeId,
                get_base_value(attributeId),
                modifierEntries
            );
        return resolvedValues;
    }

    private GArray CollectAllModifierEntries()
    {
        var entries = new GArray();
        AppendRaceModifierEntries(entries);
        AppendSubraceModifierEntries(entries);
        AppendAgeModifierEntries(entries);
        AppendBloodlineModifierEntries(entries);
        AppendAscensionModifierEntries(entries);
        AppendAscensionStageModifierEntries(entries);
        AppendStageAdvancementModifierEntries(entries);
        AppendVersatilityModifierEntries(entries);
        AppendProfessionModifierEntries(entries);
        AppendSkillModifierEntries(entries);
        AppendExternalModifierEntries(entries, _equipment_state, "equipment");
        AppendExternalModifierEntries(entries, _passive_state, "passive");
        AppendExternalModifierEntries(entries, _temporary_effects, "temporary");
        return entries;
    }

    private void AppendRaceModifierEntries(GArray entries)
    {
        if (_context?.race_def == null)
            return;
        AppendModifierEntries(
            entries,
            Variant.From(_context.race_def.attribute_modifiers),
            "race",
            _context.race_def.race_id,
            1
        );
    }

    private void AppendSubraceModifierEntries(GArray entries)
    {
        if (_context?.subrace_def == null)
            return;
        AppendModifierEntries(
            entries,
            Variant.From(_context.subrace_def.attribute_modifiers),
            "subrace",
            _context.subrace_def.subrace_id,
            1
        );
    }

    private void AppendAgeModifierEntries(GArray entries)
    {
        if (_context?.age_stage_rule == null)
            return;
        StringName sourceType =
            _context.age_stage_source_type != "" ? _context.age_stage_source_type : "age";
        StringName sourceId =
            _context.age_stage_source_id != ""
                ? _context.age_stage_source_id
                : _context.age_stage_rule.stage_id;
        AppendModifierEntries(
            entries,
            Variant.From(_context.age_stage_rule.attribute_modifiers),
            sourceType,
            sourceId,
            1
        );
    }

    private void AppendBloodlineModifierEntries(GArray entries)
    {
        if (_context == null)
            return;
        if (_context.bloodline_def != null)
            AppendModifierEntries(
                entries,
                Variant.From(_context.bloodline_def.attribute_modifiers),
                "bloodline",
                _context.bloodline_def.bloodline_id,
                1
            );
        if (_context.bloodline_stage_def != null)
            AppendModifierEntries(
                entries,
                Variant.From(_context.bloodline_stage_def.attribute_modifiers),
                "bloodline",
                _context.bloodline_stage_def.stage_id,
                1
            );
    }

    private void AppendAscensionModifierEntries(GArray entries)
    {
        if (_context?.ascension_def == null)
            return;
        Variant modifiers = _context.ascension_def.Get("attribute_modifiers");
        if (modifiers.VariantType == Variant.Type.Array)
            AppendModifierEntries(
                entries,
                modifiers,
                "ascension",
                _context.ascension_def.ascension_id,
                1
            );
    }

    private void AppendAscensionStageModifierEntries(GArray entries)
    {
        if (_context?.ascension_stage_def == null)
            return;
        AppendModifierEntries(
            entries,
            Variant.From(_context.ascension_stage_def.attribute_modifiers),
            "ascension",
            _context.ascension_stage_def.stage_id,
            1
        );
    }

    private static void AppendStageAdvancementModifierEntries(GArray entries) { }

    private void AppendVersatilityModifierEntries(GArray entries)
    {
        if (_context == null || _context.versatility_pick == "")
            return;
        if (!UnitBaseAttributes.BASE_ATTRIBUTE_IDS.Contains(_context.versatility_pick))
            return;

        var modifier = new AttributeModifier
        {
            attribute_id = _context.versatility_pick,
            mode = AttributeModifier.MODE_FLAT(),
            value = 1,
        };
        StringName sourceId = _context.race_def != null ? _context.race_def.race_id : "versatility";
        var modifiers = new GArray { modifier };
        AppendModifierEntries(entries, Variant.From(modifiers), "versatility", sourceId, 1);
    }

    private void AppendProfessionModifierEntries(GArray entries)
    {
        if (_unit_progress == null)
            return;
        foreach (Variant professionKey in GetDictionaryProperty(_unit_progress, "professions").Keys)
        {
            var professionId = ProgressionDataUtils.to_string_name(professionKey);
            var professionProgress = GetProfessionProgress(professionId);
            if (professionProgress == null || professionProgress.rank <= 0)
                continue;
            if (!professionProgress.is_active || professionProgress.is_hidden)
                continue;

            var professionDef = _profession_defs.ContainsKey(professionId)
                ? ToProfessionDef(_profession_defs[professionId])
                : null;
            if (professionDef == null)
                continue;
            AppendModifierEntries(
                entries,
                Variant.From(professionDef.attribute_modifiers),
                "profession",
                professionId,
                professionProgress.rank
            );
        }
    }

    private void AppendSkillModifierEntries(GArray entries)
    {
        if (_unit_progress == null)
            return;
        foreach (Variant skillKey in GetDictionaryProperty(_unit_progress, "skills").Keys)
        {
            var skillId = ProgressionDataUtils.to_string_name(skillKey);
            var skillProgress = GetSkillProgress(skillId);
            if (skillProgress == null || !skillProgress.is_learned)
                continue;
            if (!IsSkillModifierActive(skillProgress))
                continue;

            var skillDef = _skill_defs.ContainsKey(skillId)
                ? ToSkillDef(_skill_defs[skillId])
                : null;
            if (skillDef == null)
                continue;

            int effectiveRank = Mathf.Max(skillProgress.skill_level, 1);
            AppendModifierEntries(
                entries,
                Variant.From(skillDef.attribute_modifiers),
                "skill",
                skillId,
                effectiveRank
            );
        }
    }

    private bool IsSkillModifierActive(UnitSkillProgress skillProgress)
    {
        if (skillProgress == null)
            return false;
        if (skillProgress.profession_granted_by == "")
            return true;
        if (_unit_progress == null)
            return false;

        var professionProgress = GetProfessionProgress(skillProgress.profession_granted_by);
        return professionProgress != null
            && professionProgress.is_active
            && !professionProgress.is_hidden
            && professionProgress.rank > 0;
    }

    private void AppendExternalModifierEntries(
        GArray entries,
        Variant state,
        StringName defaultSourceType
    )
    {
        if (state.VariantType == Variant.Type.Nil)
            return;

        if (state.VariantType == Variant.Type.Array)
        {
            AppendModifierEntries(entries, state, defaultSourceType, defaultSourceType, 1);
            return;
        }

        if (state.VariantType == Variant.Type.Dictionary)
        {
            var stateDict = state.AsGodotDictionary();
            if (
                GetDictValue(stateDict, "attribute_modifiers", default).VariantType
                == Variant.Type.Array
            )
            {
                AppendModifierEntries(
                    entries,
                    GetDictValue(stateDict, "attribute_modifiers", new GArray()),
                    defaultSourceType,
                    ProgressionDataUtils.to_string_name(
                        GetDictValue(stateDict, "source_id", defaultSourceType)
                    ),
                    GetDictInt(stateDict, "rank", 1)
                );
                return;
            }

            foreach (Variant key in stateDict.Keys)
            {
                Variant modifiers = stateDict[key];
                if (modifiers.VariantType == Variant.Type.Array)
                    AppendModifierEntries(
                        entries,
                        modifiers,
                        defaultSourceType,
                        ProgressionDataUtils.to_string_name(key),
                        1
                    );
            }
            return;
        }

        if (state.VariantType == Variant.Type.Object)
        {
            var stateObject = state.AsGodotObject();
            if (stateObject == null || !stateObject.HasMethod("get_attribute_modifiers"))
                return;
            StringName sourceId = defaultSourceType;
            if (stateObject.HasMethod("get_source_id"))
                sourceId = ProgressionDataUtils.to_string_name(stateObject.Call("get_source_id"));
            AppendModifierEntries(
                entries,
                stateObject.Call("get_attribute_modifiers"),
                defaultSourceType,
                sourceId,
                1
            );
        }
    }

    private static void AppendModifierEntries(
        GArray entries,
        Variant modifiers,
        StringName sourceType,
        StringName sourceId,
        int rank
    )
    {
        if (modifiers.VariantType != Variant.Type.Array)
            return;

        foreach (Variant modifierVariant in modifiers.AsGodotArray())
        {
            var modifier = ToAttributeModifier(modifierVariant);
            if (modifier == null || modifier.attribute_id == "")
                continue;

            entries.Add(
                new GDictionary
                {
                    ["attribute_id"] = modifier.attribute_id,
                    ["mode"] = modifier.mode,
                    ["value"] = modifier.get_value_for_rank(rank),
                    ["source_type"] = sourceType != "" ? sourceType : modifier.source_type,
                    ["source_id"] = sourceId != "" ? sourceId : modifier.source_id,
                }
            );
        }
    }

    private int GetPersistentBaseValue(StringName attributeId)
    {
        var unitBaseAttributes = GetUnitBaseAttributes();
        if (unitBaseAttributes == null)
            return 0;
        if (attributeId == HP_MAX && !unitBaseAttributes.custom_stats.ContainsKey(HP_MAX))
            return 1;
        if (
            attributeId == ACTION_THRESHOLD
            && !unitBaseAttributes.custom_stats.ContainsKey(ACTION_THRESHOLD)
        )
            return DEFAULT_CHARACTER_ACTION_THRESHOLD;
        return unitBaseAttributes.get_attribute_value(attributeId);
    }

    private int ApplyModifierPipeline(StringName attributeId, int baseValue, GArray modifierEntries)
    {
        int flatDelta = 0;
        int percentDelta = 0;

        foreach (Variant entryVariant in modifierEntries)
        {
            if (entryVariant.VariantType != Variant.Type.Dictionary)
                continue;
            var entry = entryVariant.AsGodotDictionary();
            var modifierAttributeId = ProgressionDataUtils.to_string_name(
                GetDictValue(entry, "attribute_id", "")
            );
            if (!ModifierEntryAppliesToAttribute(attributeId, modifierAttributeId))
                continue;

            int value = GetDictInt(entry, "value", 0);
            var mode = ProgressionDataUtils.to_string_name(GetDictValue(entry, "mode", "flat"));
            if (mode == AttributeModifier.MODE_PERCENT())
                percentDelta += value;
            else
                flatDelta += value;
        }

        int result = baseValue + flatDelta;
        if (percentDelta != 0)
            result = Mathf.FloorToInt((float)result * (100 + percentDelta) / 100.0f);

        return ClampAttributeValue(attributeId, result);
    }

    private int CalculateCharacterHpMax(int baseValue, GArray modifierEntries)
    {
        int percentBonus = ResolveCharacterHpMaxPercentBonus(modifierEntries);
        if (percentBonus <= 0)
            return baseValue;
        return Mathf.FloorToInt((float)baseValue * (100 + percentBonus) / 100.0f);
    }

    private int ResolveCharacterHpMaxPercentBonus(GArray modifierEntries)
    {
        int percentBonus = 0;
        foreach (Variant entryVariant in modifierEntries)
        {
            if (entryVariant.VariantType != Variant.Type.Dictionary)
                continue;
            var entry = entryVariant.AsGodotDictionary();
            var attributeId = ProgressionDataUtils.to_string_name(
                GetDictValue(entry, "attribute_id", "")
            );
            if (attributeId != CHARACTER_HP_MAX_PERCENT_BONUS)
                continue;
            var mode = ProgressionDataUtils.to_string_name(GetDictValue(entry, "mode", "flat"));
            if (mode == AttributeModifier.MODE_PERCENT())
                continue;
            percentBonus += Mathf.Max(GetDictInt(entry, "value", 0), 0);
        }
        return percentBonus;
    }

    private int CalculateBaseAttackBonus()
    {
        if (_unit_progress == null)
            return 0;
        var pairs = new GArray();
        foreach (Variant professionKey in GetDictionaryProperty(_unit_progress, "professions").Keys)
        {
            var professionId = ProgressionDataUtils.to_string_name(professionKey);
            var professionProgress = GetProfessionProgress(professionId);
            if (professionProgress == null || professionProgress.rank <= 0)
                continue;
            if (!professionProgress.is_active || professionProgress.is_hidden)
                continue;
            var professionDef = _profession_defs.ContainsKey(professionId)
                ? ToProfessionDef(_profession_defs[professionId])
                : null;
            if (professionDef == null)
                continue;
            pairs.Add(new GArray { professionProgress.rank, professionDef.bab_progression });
        }
        return AttributeSnapshot.calculate_base_attack_bonus(pairs);
    }

    private int CalculateSpellProficiencyBonus()
    {
        if (_unit_progress == null)
            return AttributeSnapshot.calculate_spell_proficiency_bonus(0);
        return AttributeSnapshot.calculate_spell_proficiency_bonus(
            _unit_progress.Get("character_level").AsInt32()
        );
    }

    private int CalculateBaseArmorClass(GDictionary resolvedBaseValues, GArray modifierEntries)
    {
        int agility = GetDictInt(resolvedBaseValues, UnitBaseAttributes.AGILITY, 0);
        int agilityModifier = CalculateScoreModifier(agility);
        int cappedAgilityModifier = agilityModifier;
        int maxDexBonus = ResolveArmorMaxDexBonus(modifierEntries);
        if (maxDexBonus >= 0 && agilityModifier > maxDexBonus)
            cappedAgilityModifier = maxDexBonus;
        return BASE_ARMOR_CLASS + cappedAgilityModifier + ResolvePersistentAcComponentTotal();
    }

    private int ResolvePersistentAcComponentTotal()
    {
        int total = 0;
        foreach (StringName componentId in AC_COMPONENT_ATTRIBUTE_IDS)
            total += Mathf.Max(GetPersistentBaseValue(componentId), 0);
        return total;
    }

    private static int CalculateScoreModifier(int score)
    {
        return AttributeSnapshot.calculate_score_modifier(score);
    }

    private int ResolveArmorMaxDexBonus(GArray modifierEntries)
    {
        int resolvedCap = -1;
        foreach (Variant entryVariant in modifierEntries)
        {
            if (entryVariant.VariantType != Variant.Type.Dictionary)
                continue;
            var entry = entryVariant.AsGodotDictionary();
            var attributeId = ProgressionDataUtils.to_string_name(
                GetDictValue(entry, "attribute_id", "")
            );
            if (attributeId != ARMOR_MAX_DEX_BONUS)
                continue;
            var mode = ProgressionDataUtils.to_string_name(GetDictValue(entry, "mode", "flat"));
            if (mode == AttributeModifier.MODE_PERCENT())
                continue;
            int value = GetDictInt(entry, "value", -1);
            if (value < 0)
                continue;
            resolvedCap = resolvedCap < 0 ? value : Mathf.Min(resolvedCap, value);
        }
        return resolvedCap;
    }

    private static bool ModifierEntryAppliesToAttribute(
        StringName attributeId,
        StringName modifierAttributeId
    )
    {
        if (modifierAttributeId == attributeId)
            return true;
        return attributeId == ARMOR_CLASS
            && AC_COMPONENT_ATTRIBUTE_IDS.Contains(modifierAttributeId);
    }

    private static int ClampAttributeValue(StringName attributeId, int value)
    {
        if (attributeId == HP_MAX)
            return Mathf.Max(value, 1);
        if (attributeId == CHARACTER_HP_MAX_PERCENT_BONUS)
            return Mathf.Max(value, 0);
        if (attributeId == STAMINA_RECOVERY_PERCENT_BONUS)
            return Mathf.Max(value, 0);
        if (attributeId == MP_MAX || attributeId == STAMINA_MAX || attributeId == AURA_MAX)
            return Mathf.Max(value, 0);
        if (attributeId == ACTION_POINTS)
            return Mathf.Max(value, 1);
        if (attributeId == ACTION_THRESHOLD)
            return NormalizeActionThreshold(value);
        if (attributeId == ARMOR_CLASS)
            return Mathf.Clamp(value, 1, 99);
        if (
            attributeId == ARMOR_AC_BONUS
            || attributeId == SHIELD_AC_BONUS
            || attributeId == DODGE_BONUS
            || attributeId == DEFLECTION_BONUS
        )
            return Mathf.Max(value, 0);
        if (attributeId == ARMOR_MAX_DEX_BONUS)
            return Mathf.Max(value, -1);
        return value;
    }

    private static Godot.Collections.Array<StringName> GetKnownNonBaseAttributeIds()
    {
        var result = new Godot.Collections.Array<StringName>();
        foreach (StringName attributeId in RESOURCE_ATTRIBUTE_IDS)
            result.Add(attributeId);
        foreach (StringName attributeId in COMBAT_ATTRIBUTE_IDS)
            result.Add(attributeId);
        return result;
    }

    private static int NormalizeActionThreshold(int value)
    {
        int threshold = Mathf.Max(value, ACTION_THRESHOLD_GRANULARITY);
        return Mathf.Max(
            Mathf.RoundToInt((float)threshold / ACTION_THRESHOLD_GRANULARITY)
                * ACTION_THRESHOLD_GRANULARITY,
            ACTION_THRESHOLD_GRANULARITY
        );
    }

    private Godot.Collections.Array<StringName> GetAdditionalAttributeIds(GArray modifierEntries)
    {
        var result = new Godot.Collections.Array<StringName>();
        var seen = new GDictionary();
        var knownAttributeIds = GetKnownNonBaseAttributeIds();

        foreach (StringName attributeId in UnitBaseAttributes.BASE_ATTRIBUTE_IDS)
        {
            knownAttributeIds.Add(attributeId);
            seen[attributeId] = true;
        }
        foreach (StringName attributeId in knownAttributeIds)
            seen[attributeId] = true;

        var unitBaseAttributes = GetUnitBaseAttributes();
        if (unitBaseAttributes != null)
        {
            foreach (Variant key in unitBaseAttributes.custom_stats.Keys)
            {
                var attributeId = ProgressionDataUtils.to_string_name(key);
                if (seen.ContainsKey(attributeId))
                    continue;
                seen[attributeId] = true;
                result.Add(attributeId);
            }
        }

        foreach (Variant entryVariant in modifierEntries)
        {
            if (entryVariant.VariantType != Variant.Type.Dictionary)
                continue;
            var entry = entryVariant.AsGodotDictionary();
            var attributeId = ProgressionDataUtils.to_string_name(
                GetDictValue(entry, "attribute_id", "")
            );
            if (attributeId == "" || seen.ContainsKey(attributeId))
                continue;
            seen[attributeId] = true;
            result.Add(attributeId);
        }

        return result;
    }

    private static GDictionary BuildDefaultRules()
    {
        var rules = new GDictionary();
        rules[STAMINA_MAX] = new DerivedAttributeRule(
            STAMINA_MAX,
            24,
            new GDictionary
            {
                [UnitBaseAttributes.CONSTITUTION] = 5,
                [UnitBaseAttributes.STRENGTH] = 1,
                [UnitBaseAttributes.AGILITY] = 1,
            },
            1,
            0,
            0,
            0
        );
        rules[ACTION_POINTS] = new DerivedAttributeRule(
            ACTION_POINTS,
            1,
            new GDictionary { [UnitBaseAttributes.AGILITY] = 1 },
            10,
            1,
            0,
            0
        );
        return rules;
    }

    private UnitProfessionProgress GetProfessionProgress(StringName professionId)
    {
        if (_unit_progress == null)
            return null;
        return ToProfessionProgress(_unit_progress.Call("get_profession_progress", professionId));
    }

    private UnitSkillProgress GetSkillProgress(StringName skillId)
    {
        if (_unit_progress == null)
            return null;
        return ToSkillProgress(_unit_progress.Call("get_skill_progress", skillId));
    }

    private static GDictionary GetDictionaryProperty(GodotObject source, string propertyName)
    {
        if (source == null)
            return new GDictionary();
        Variant value = source.Get(propertyName);
        return value.VariantType == Variant.Type.Dictionary
            ? value.AsGodotDictionary()
            : new GDictionary();
    }

    private static int GetDictInt(GDictionary data, Variant key, int fallback)
    {
        Variant value = GetDictValue(data, key, fallback);
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : fallback;
    }

    private static bool GetDictBool(GDictionary data, Variant key, bool fallback)
    {
        Variant value = GetDictValue(data, key, fallback);
        return value.VariantType == Variant.Type.Bool ? value.AsBool() : fallback;
    }

    private static Variant GetDictValue(GDictionary data, Variant key, Variant fallback)
    {
        if (data == null)
            return fallback;
        if (data.ContainsKey(key))
            return data[key];
        if (key.VariantType == Variant.Type.String)
        {
            var stringNameKey = new StringName(key.AsString());
            if (data.ContainsKey(stringNameKey))
                return data[stringNameKey];
        }
        if (key.VariantType == Variant.Type.StringName)
        {
            string stringKey = key.AsString();
            if (data.ContainsKey(stringKey))
                return data[stringKey];
        }
        return fallback;
    }

    private static AttributeModifier ToAttributeModifier(Variant value)
    {
        return value.VariantType == Variant.Type.Object
            ? value.AsGodotObject() as AttributeModifier
            : null;
    }

    private static SkillDef ToSkillDef(Variant value)
    {
        return value.VariantType == Variant.Type.Object ? value.AsGodotObject() as SkillDef : null;
    }

    private static ProfessionDef ToProfessionDef(Variant value)
    {
        return value.VariantType == Variant.Type.Object
            ? value.AsGodotObject() as ProfessionDef
            : null;
    }

    private static UnitSkillProgress ToSkillProgress(Variant value)
    {
        return value.VariantType == Variant.Type.Object
            ? value.AsGodotObject() as UnitSkillProgress
            : null;
    }

    private static UnitProfessionProgress ToProfessionProgress(Variant value)
    {
        return value.VariantType == Variant.Type.Object
            ? value.AsGodotObject() as UnitProfessionProgress
            : null;
    }
}

