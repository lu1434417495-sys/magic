using System.Collections.Generic;
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
    public static readonly StringName ACTION_THRESHOLD = UnitBaseAttributes.ACTION_THRESHOLD();
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
        UnitBaseAttributes.HIDDEN_LUCK_AT_BIRTH(),
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

    private UnitProgress _unit_progress;
    private Dictionary<StringName, SkillDef> _skill_defs = new();
    private Dictionary<StringName, ProfessionDef> _profession_defs = new();
    private List<AttributeModifier> _equipment_state = new();
    private List<AttributeModifier> _passive_state = new();
    private List<AttributeModifier> _temporary_effects = new();
    private GDictionary _derived_rules = new();
    private AttributeSourceContext _context;
    private AttributeSnapshot _cached_snapshot;
    private bool _snapshot_dirty = true;

    private readonly record struct AttributeModifierEntry(
        StringName AttributeId,
        StringName Mode,
        int Value,
        StringName SourceType,
        StringName SourceId
    );

    private readonly record struct AttributePermanentChangeSource(
        StringName SourceType,
        StringName SourceId,
        bool AllowProtectedCustomStatWrite
    )
    {
        public static AttributePermanentChangeSource FromDictionary(GDictionary sourceContext)
        {
            if (sourceContext == null)
                return default;
            return new AttributePermanentChangeSource(
                GetDictStringName(sourceContext, "source_type"),
                GetDictStringName(sourceContext, "source_id"),
                ReadProtectedWriteFlag(sourceContext)
            );
        }

        private static bool ReadProtectedWriteFlag(GDictionary sourceContext)
        {
            return TryGetDictValue(
                    sourceContext,
                    PROTECTED_CUSTOM_STAT_WRITE_FLAG,
                    out object rawValue
                )
                && TryAsStrictBool(rawValue, out bool value)
                && value;
        }
    }

    public AttributeService()
    {
        _derived_rules = BuildDefaultRules();
    }

    public void setup(UnitProgress unit_progress)
    {
        SetupInternal(unit_progress, default, default, default, default, default);
    }

    public void setup(UnitProgress unit_progress, GDictionary skill_defs, GDictionary profession_defs)
    {
        SetupInternal(unit_progress, skill_defs, profession_defs, default, default, default);
    }

    public void setup(
        UnitProgress unit_progress,
        GDictionary skill_defs,
        GDictionary profession_defs,
        GArray equipment_state
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
        UnitProgress unit_progress,
        GDictionary skill_defs,
        GDictionary profession_defs,
        GArray equipment_state,
        GArray passive_state,
        GArray temporary_effects
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
        UnitProgress unitProgress,
        GDictionary skillDefs,
        GDictionary professionDefs,
        GArray equipmentState,
        GArray passiveState,
        GArray temporaryEffects
    )
    {
        var context = new AttributeSourceContext
        {
            unit_progress = unitProgress,
            skill_defs = IndexSkillDefs(skillDefs),
            profession_defs = IndexProfessionDefs(professionDefs),
            equipment_state = ToAttributeModifierList(equipmentState),
            passive_state = ToAttributeModifierList(passiveState),
            temporary_effects = ToAttributeModifierList(temporaryEffects),
        };
        setup_context(context);
    }

    internal void setup_context(AttributeSourceContext context)
    {
        _context = context ?? new AttributeSourceContext();
        _unit_progress = _context.unit_progress;
        _skill_defs =
            _context.skill_defs != null
                ? new Dictionary<StringName, SkillDef>(_context.skill_defs)
                : new Dictionary<StringName, SkillDef>();
        _profession_defs =
            _context.profession_defs != null
                ? new Dictionary<StringName, ProfessionDef>(_context.profession_defs)
                : new Dictionary<StringName, ProfessionDef>();
        _equipment_state = CopyAttributeModifierList(_context.equipment_state);
        _passive_state = CopyAttributeModifierList(_context.passive_state);
        _temporary_effects = CopyAttributeModifierList(_context.temporary_effects);
        _context.skill_defs = _skill_defs;
        _context.profession_defs = _profession_defs;
        _context.equipment_state = _equipment_state;
        _context.passive_state = _passive_state;
        _context.temporary_effects = _temporary_effects;
        invalidate_snapshot();
    }

    public void set_equipment_state(GArray equipment_state)
    {
        _equipment_state = ToAttributeModifierList(equipment_state);
        if (_context != null)
            _context.equipment_state = _equipment_state;
        invalidate_snapshot();
    }

    public void set_passive_state(GArray passive_state)
    {
        _passive_state = ToAttributeModifierList(passive_state);
        if (_context != null)
            _context.passive_state = _passive_state;
        invalidate_snapshot();
    }

    public void set_temporary_effects(GArray temporary_effects)
    {
        _temporary_effects = ToAttributeModifierList(temporary_effects);
        if (_context != null)
            _context.temporary_effects = _temporary_effects;
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

        foreach (StringName attributeId in UnitBaseAttributes.BASE_ATTRIBUTE_IDS())
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
        return ApplyPermanentAttributeChange(
            attribute_id,
            delta,
            AttributePermanentChangeSource.FromDictionary(source_context)
        );
    }

    private bool ApplyPermanentAttributeChange(
        StringName attribute_id,
        int delta,
        AttributePermanentChangeSource sourceContext
    )
    {
        var unitBaseAttributes = GetUnitBaseAttributes();
        if (unitBaseAttributes == null)
            return false;

        if (IsProtectedCustomStat(attribute_id) && !CanWriteProtectedCustomStat(sourceContext))
        {
            GameLog.Warning(
                BuildProtectedCustomStatRejectionMessage(attribute_id, delta, sourceContext),
                "attribute.protected_stat_rejected",
                "attribute"
            );
            return false;
        }

        if (
            !UnitBaseAttributes.BASE_ATTRIBUTE_IDS().Contains(attribute_id)
            && !CanWriteCustomStat(attribute_id)
        )
        {
            GameLog.Warning(
                $"AttributeService: refuse permanent change for unsupported attribute {(string)attribute_id}.",
                "attribute.unsupported_permanent_change",
                "attribute"
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
        return _unit_progress.unit_base_attributes;
    }

    private bool IsProtectedCustomStat(StringName attributeId)
    {
        return !UnitBaseAttributes.BASE_ATTRIBUTE_IDS().Contains(attributeId)
            && PROTECTED_CUSTOM_STAT_KEYS.Contains(attributeId);
    }

    private bool CanWriteCustomStat(StringName attributeId)
    {
        if (attributeId == "" || UnitBaseAttributes.BASE_ATTRIBUTE_IDS().Contains(attributeId))
            return false;
        var unitBaseAttributes = GetUnitBaseAttributes();
        if (unitBaseAttributes == null)
            return false;
        return unitBaseAttributes.custom_stats.ContainsKey(attributeId)
            || PROTECTED_CUSTOM_STAT_KEYS.Contains(attributeId);
    }

    private static bool CanWriteProtectedCustomStat(AttributePermanentChangeSource sourceContext)
    {
        if (sourceContext.SourceType == PROTECTED_CUSTOM_STAT_SOURCE_CHARACTER_CREATION)
            return true;
        if (sourceContext.SourceType != PROTECTED_CUSTOM_STAT_SOURCE_STORY_SCRIPT)
            return false;
        return sourceContext.AllowProtectedCustomStatWrite;
    }

    private static string BuildProtectedCustomStatRejectionMessage(
        StringName attributeId,
        int delta,
        AttributePermanentChangeSource sourceContext
    )
    {
        return $"AttributeService: reject protected custom stat write {(string)attributeId} delta={delta} source_type={(string)sourceContext.SourceType} source_id={(string)sourceContext.SourceId}.";
    }

    private static Dictionary<StringName, SkillDef> IndexSkillDefs(GDictionary skillDefs)
    {
        var indexedDefs = new Dictionary<StringName, SkillDef>();
        if (skillDefs == null)
            return indexedDefs;

        foreach (object key in skillDefs.Keys)
        {
            if (
                !TryGetDictValue(
                    skillDefs,
                    ProgressionDataUtils.to_string_name(key),
                    out object _skillV
                )
            )
                continue;
            if (!TryAsObject(_skillV, out SkillDef skillDef))
                continue;
            var indexedId =
                skillDef.skill_id != "" ? skillDef.skill_id : ProgressionDataUtils.to_string_name(key);
            indexedDefs[indexedId] = skillDef;
        }
        return indexedDefs;
    }

    private static Dictionary<StringName, ProfessionDef> IndexProfessionDefs(GDictionary professionDefs)
    {
        var indexedDefs = new Dictionary<StringName, ProfessionDef>();
        if (professionDefs == null)
            return indexedDefs;

        foreach (object key in professionDefs.Keys)
        {
            if (
                !TryGetDictValue(
                    professionDefs,
                    ProgressionDataUtils.to_string_name(key),
                    out object _profV
                )
            )
                continue;
            if (!TryAsObject(_profV, out ProfessionDef professionDef))
                continue;
            var indexedId =
                professionDef.profession_id != ""
                    ? professionDef.profession_id
                    : ProgressionDataUtils.to_string_name(key);
            indexedDefs[indexedId] = professionDef;
        }
        return indexedDefs;
    }

    private Dictionary<StringName, int> ResolveBaseAttributeValues(
        List<AttributeModifierEntry> modifierEntries
    )
    {
        var resolvedValues = new Dictionary<StringName, int>();
        foreach (StringName attributeId in UnitBaseAttributes.BASE_ATTRIBUTE_IDS())
            resolvedValues[attributeId] = ApplyModifierPipeline(
                attributeId,
                get_base_value(attributeId),
                modifierEntries
            );
        return resolvedValues;
    }

    private List<AttributeModifierEntry> CollectAllModifierEntries()
    {
        var entries = new List<AttributeModifierEntry>();
        AppendRaceModifierEntries(entries);
        AppendSubraceModifierEntries(entries);
        AppendAgeModifierEntries(entries);
        AppendBloodlineModifierEntries(entries);
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

    private void AppendRaceModifierEntries(List<AttributeModifierEntry> entries)
    {
        if (_context?.race_def == null)
            return;
        AppendModifierEntries(
            entries,
            _context.race_def.attribute_modifiers,
            "race",
            _context.race_def.race_id,
            1
        );
    }

    private void AppendSubraceModifierEntries(List<AttributeModifierEntry> entries)
    {
        if (_context?.subrace_def == null)
            return;
        AppendModifierEntries(
            entries,
            _context.subrace_def.attribute_modifiers,
            "subrace",
            _context.subrace_def.subrace_id,
            1
        );
    }

    private void AppendAgeModifierEntries(List<AttributeModifierEntry> entries)
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
            _context.age_stage_rule.attribute_modifiers,
            sourceType,
            sourceId,
            1
        );
    }

    private void AppendBloodlineModifierEntries(List<AttributeModifierEntry> entries)
    {
        if (_context == null)
            return;
        if (_context.bloodline_def != null)
            AppendModifierEntries(
                entries,
                _context.bloodline_def.attribute_modifiers,
                "bloodline",
                _context.bloodline_def.bloodline_id,
                1
            );
        if (_context.bloodline_stage_def != null)
            AppendModifierEntries(
                entries,
                _context.bloodline_stage_def.attribute_modifiers,
                "bloodline",
                _context.bloodline_stage_def.stage_id,
                1
            );
    }

    private void AppendAscensionStageModifierEntries(List<AttributeModifierEntry> entries)
    {
        if (_context?.ascension_stage_def == null)
            return;
        AppendModifierEntries(
            entries,
            _context.ascension_stage_def.attribute_modifiers,
            "ascension",
            _context.ascension_stage_def.stage_id,
            1
        );
    }

    private static void AppendStageAdvancementModifierEntries(
        List<AttributeModifierEntry> entries
    ) { }

    private void AppendVersatilityModifierEntries(List<AttributeModifierEntry> entries)
    {
        if (_context == null || _context.versatility_pick == "")
            return;
        if (!UnitBaseAttributes.BASE_ATTRIBUTE_IDS().Contains(_context.versatility_pick))
            return;

        var modifier = new AttributeModifier
        {
            attribute_id = _context.versatility_pick,
            mode = AttributeModifier.MODE_FLAT(),
            value = 1,
        };
        StringName sourceId = _context.race_def != null ? _context.race_def.race_id : "versatility";
        var modifiers = new List<AttributeModifier> { modifier };
        AppendModifierEntries(entries, modifiers, "versatility", sourceId, 1);
    }

    private void AppendProfessionModifierEntries(List<AttributeModifierEntry> entries)
    {
        if (_unit_progress == null)
            return;
        foreach (Variant professionKey in _unit_progress.professions.Keys)
        {
            var professionId = ProgressionDataUtils.to_string_name(professionKey);
            var professionProgress = GetProfessionProgress(professionId);
            if (professionProgress == null || professionProgress.rank <= 0)
                continue;
            if (!professionProgress.is_active || professionProgress.is_hidden)
                continue;

            if (!_profession_defs.TryGetValue(professionId, out var professionDef))
                continue;
            AppendModifierEntries(
                entries,
                professionDef.attribute_modifiers,
                "profession",
                professionId,
                professionProgress.rank
            );
        }
    }

    private void AppendSkillModifierEntries(List<AttributeModifierEntry> entries)
    {
        if (_unit_progress == null)
            return;
        foreach (Variant skillKey in _unit_progress.skills.Keys)
        {
            var skillId = ProgressionDataUtils.to_string_name(skillKey);
            var skillProgress = GetSkillProgress(skillId);
            if (skillProgress == null || !skillProgress.is_learned)
                continue;
            if (!IsSkillModifierActive(skillProgress))
                continue;

            if (!_skill_defs.TryGetValue(skillId, out var skillDef))
                continue;

            int effectiveRank = Mathf.Max(skillProgress.skill_level, 1);
            AppendModifierEntries(
                entries,
                skillDef.attribute_modifiers,
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
        List<AttributeModifierEntry> entries,
        List<AttributeModifier> state,
        StringName defaultSourceType
    )
    {
        if (state == null || state.Count == 0)
            return;
        AppendModifierEntries(entries, state, defaultSourceType, defaultSourceType, 1);
    }

    private static void AppendModifierEntries<T>(
        List<AttributeModifierEntry> entries,
        IEnumerable<T> modifiers,
        StringName sourceType,
        StringName sourceId,
        int rank
    )
    {
        if (modifiers == null)
            return;

        foreach (T modifierValue in modifiers)
        {
            if (modifierValue is AttributeModifier modifier)
                AppendModifierEntry(entries, modifier, sourceType, sourceId, rank);
        }
    }

    private static void AppendModifierEntry(
        List<AttributeModifierEntry> entries,
        AttributeModifier modifier,
        StringName sourceType,
        StringName sourceId,
        int rank
    )
    {
        if (modifier == null || modifier.attribute_id == "")
            return;

        entries.Add(
            new AttributeModifierEntry(
                modifier.attribute_id,
                modifier.mode,
                modifier.get_value_for_rank(rank),
                sourceType != "" ? sourceType : modifier.source_type,
                sourceId != "" ? sourceId : modifier.source_id
            )
        );
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

    private int ApplyModifierPipeline(
        StringName attributeId,
        int baseValue,
        List<AttributeModifierEntry> modifierEntries
    )
    {
        int flatDelta = 0;
        int percentDelta = 0;

        foreach (var entry in modifierEntries)
        {
            if (!ModifierEntryAppliesToAttribute(attributeId, entry.AttributeId))
                continue;

            if (entry.Mode == AttributeModifier.MODE_PERCENT())
                percentDelta += entry.Value;
            else
                flatDelta += entry.Value;
        }

        int result = baseValue + flatDelta;
        if (percentDelta != 0)
            result = Mathf.FloorToInt((float)result * (100 + percentDelta) / 100.0f);

        return ClampAttributeValue(attributeId, result);
    }

    private int CalculateCharacterHpMax(int baseValue, List<AttributeModifierEntry> modifierEntries)
    {
        int percentBonus = ResolveCharacterHpMaxPercentBonus(modifierEntries);
        if (percentBonus <= 0)
            return baseValue;
        return Mathf.FloorToInt((float)baseValue * (100 + percentBonus) / 100.0f);
    }

    private int ResolveCharacterHpMaxPercentBonus(List<AttributeModifierEntry> modifierEntries)
    {
        int percentBonus = 0;
        foreach (var entry in modifierEntries)
        {
            if (entry.AttributeId != CHARACTER_HP_MAX_PERCENT_BONUS)
                continue;
            if (entry.Mode == AttributeModifier.MODE_PERCENT())
                continue;
            percentBonus += Mathf.Max(entry.Value, 0);
        }
        return percentBonus;
    }

    private int CalculateBaseAttackBonus()
    {
        if (_unit_progress == null)
            return 0;
        var pairs = new GArray();
        foreach (Variant professionKey in _unit_progress.professions.Keys)
        {
            var professionId = ProgressionDataUtils.to_string_name(professionKey);
            var professionProgress = GetProfessionProgress(professionId);
            if (professionProgress == null || professionProgress.rank <= 0)
                continue;
            if (!professionProgress.is_active || professionProgress.is_hidden)
                continue;
            if (!_profession_defs.TryGetValue(professionId, out var professionDef))
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
            _unit_progress.character_level
        );
    }

    private int CalculateBaseArmorClass(
        Dictionary<StringName, int> resolvedBaseValues,
        List<AttributeModifierEntry> modifierEntries
    )
    {
        int agility = GetDictInt(resolvedBaseValues, UnitBaseAttributes.AGILITY(), 0);
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

    private int ResolveArmorMaxDexBonus(List<AttributeModifierEntry> modifierEntries)
    {
        int resolvedCap = -1;
        foreach (var entry in modifierEntries)
        {
            if (entry.AttributeId != ARMOR_MAX_DEX_BONUS)
                continue;
            if (entry.Mode == AttributeModifier.MODE_PERCENT())
                continue;
            if (entry.Value < 0)
                continue;
            resolvedCap = resolvedCap < 0 ? entry.Value : Mathf.Min(resolvedCap, entry.Value);
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

    private Godot.Collections.Array<StringName> GetAdditionalAttributeIds(
        List<AttributeModifierEntry> modifierEntries
    )
    {
        var result = new Godot.Collections.Array<StringName>();
        var seen = new HashSet<StringName>();
        var knownAttributeIds = GetKnownNonBaseAttributeIds();

        foreach (StringName attributeId in UnitBaseAttributes.BASE_ATTRIBUTE_IDS())
        {
            knownAttributeIds.Add(attributeId);
            seen.Add(attributeId);
        }
        foreach (StringName attributeId in knownAttributeIds)
            seen.Add(attributeId);

        var unitBaseAttributes = GetUnitBaseAttributes();
        if (unitBaseAttributes != null)
        {
            foreach (object key in unitBaseAttributes.custom_stats.Keys)
            {
                var attributeId = ProgressionDataUtils.to_string_name(key);
                if (seen.Contains(attributeId))
                    continue;
                seen.Add(attributeId);
                result.Add(attributeId);
            }
        }

        foreach (var entry in modifierEntries)
        {
            var attributeId = entry.AttributeId;
            if (attributeId == "" || seen.Contains(attributeId))
                continue;
            seen.Add(attributeId);
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
                [UnitBaseAttributes.CONSTITUTION()] = 5,
                [UnitBaseAttributes.STRENGTH()] = 1,
                [UnitBaseAttributes.AGILITY()] = 1,
            },
            1,
            0,
            0,
            0
        );
        rules[ACTION_POINTS] = new DerivedAttributeRule(
            ACTION_POINTS,
            1,
            new GDictionary { [UnitBaseAttributes.AGILITY()] = 1 },
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
        return _unit_progress.get_profession_progress(professionId);
    }

    private UnitSkillProgress GetSkillProgress(StringName skillId)
    {
        if (_unit_progress == null)
            return null;
        return _unit_progress.get_skill_progress(skillId);
    }

    private static List<AttributeModifier> ToAttributeModifierList(GArray values)
    {
        var result = new List<AttributeModifier>();
        if (values == null)
            return result;
        foreach (object rawValue in values)
        {
            if (TryAsObject(rawValue, out AttributeModifier modifier))
                result.Add(modifier);
        }
        return result;
    }

    private static List<AttributeModifier> CopyAttributeModifierList(
        List<AttributeModifier> values
    )
    {
        return values != null ? new List<AttributeModifier>(values) : new List<AttributeModifier>();
    }

    private static int GetDictInt(GDictionary data, string key, int fallback)
    {
        if (data == null)
            return fallback;
        return TryGetDictValue(data, key, out object value) && TryAsInt(value, out int parsed)
            ? parsed
            : fallback;
    }

    private static int GetDictInt(GDictionary data, StringName key, int fallback)
    {
        if (data == null || key == null)
            return fallback;
        return TryGetDictValue(data, key, out object value) && TryAsInt(value, out int parsed)
            ? parsed
            : fallback;
    }

    private static int GetDictInt(Dictionary<StringName, int> data, StringName key, int fallback)
    {
        return data.TryGetValue(key, out var value) ? value : fallback;
    }

    private static StringName GetDictStringName(
        GDictionary data,
        string key,
        StringName fallback = default
    )
    {
        if (data == null)
            return fallback;
        if (!TryGetDictValue(data, key, out object value))
            return fallback;
        StringName parsed = ProgressionDataUtils.to_string_name(value);
        return parsed != "" ? parsed : fallback;
    }

    private static StringName GetDictStringName(
        GDictionary data,
        StringName key,
        StringName fallback = default
    )
    {
        if (data == null || key == null)
            return fallback;
        if (!TryGetDictValue(data, key, out object value))
            return fallback;
        StringName parsed = ProgressionDataUtils.to_string_name(value);
        return parsed != "" ? parsed : fallback;
    }

    private static bool TryAsObject<T>(object rawValue, out T value)
        where T : GodotObject
    {
        if (rawValue is Variant variant && variant.VariantType == Variant.Type.Object)
        {
            value = variant.AsGodotObject() as T;
            return value != null;
        }
        if (rawValue is T typedValue)
        {
            value = typedValue;
            return true;
        }
        value = null;
        return false;
    }

    private static bool TryAsInt(object rawValue, out int value)
    {
        if (rawValue is Variant variant && variant.VariantType == Variant.Type.Int)
        {
            value = variant.AsInt32();
            return true;
        }
        if (rawValue is int intValue)
        {
            value = intValue;
            return true;
        }
        value = 0;
        return false;
    }

    private static bool TryAsStrictBool(object rawValue, out bool value)
    {
        if (rawValue is Variant variant && variant.VariantType == Variant.Type.Bool)
        {
            value = variant.AsBool();
            return true;
        }
        if (rawValue is bool boolValue)
        {
            value = boolValue;
            return true;
        }
        value = false;
        return false;
    }

    private static bool TryGetDictValue(GDictionary data, string key, out object value)
    {
        if (data.ContainsKey(key))
        {
            value = data[key];
            return true;
        }
        var stringNameKey = new StringName(key);
        if (data.ContainsKey(stringNameKey))
        {
            value = data[stringNameKey];
            return true;
        }
        value = null;
        return false;
    }

    private static bool TryGetDictValue(GDictionary data, StringName key, out object value)
    {
        if (data.ContainsKey(key))
        {
            value = data[key];
            return true;
        }
        string stringKey = key.ToString();
        if (data.ContainsKey(stringKey))
        {
            value = data[stringKey];
            return true;
        }
        value = null;
        return false;
    }
}
