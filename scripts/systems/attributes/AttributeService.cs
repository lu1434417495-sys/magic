using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

internal enum AttributeIdKind
{
    Unknown = 0,
    HpMax,
    CharacterHpMaxPercentBonus,
    MpMax,
    StaminaMax,
    StaminaRecoveryPercentBonus,
    AuraMax,
    ActionPoints,
    ActionThreshold,
    AttackBonus,
    WeaponAttackRange,
    StrengthModifier,
    AgilityModifier,
    ConstitutionModifier,
    PerceptionModifier,
    IntelligenceModifier,
    WillpowerModifier,
    ArmorClass,
    ArmorAcBonus,
    ShieldAcBonus,
    DodgeBonus,
    DeflectionBonus,
    ArmorMaxDexBonus,
    BaseAttackBonus,
    SpellProficiencyBonus,
}

internal enum AttributeSourceKind
{
    Unknown = 0,
    CharacterCreation,
    StoryScript,
}

internal enum AttributeServiceKeyKind
{
    Unknown = 0,
    ProtectedCustomStatWriteFlag,
}

public sealed class AttributeService
{
    internal static readonly StringName HP_MAX = "hp_max";
    internal static readonly StringName CHARACTER_HP_MAX_PERCENT_BONUS =
        "character_hp_max_percent_bonus";
    internal static readonly StringName MP_MAX = "mp_max";
    internal static readonly StringName STAMINA_MAX = "stamina_max";
    internal static readonly StringName STAMINA_RECOVERY_PERCENT_BONUS =
        "stamina_recovery_percent_bonus";
    internal static readonly StringName AURA_MAX = "aura_max";
    internal static readonly StringName ACTION_POINTS = "action_points";
    internal static readonly StringName ACTION_THRESHOLD =
        UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.ActionThreshold);
    internal static readonly StringName ATTACK_BONUS = "attack_bonus";
    internal static readonly StringName WEAPON_ATTACK_RANGE = "weapon_attack_range";
    internal static readonly StringName STRENGTH_MODIFIER =
        AttributeSnapshot.ToStringName(AttributeSnapshotIdKind.StrengthModifier);
    internal static readonly StringName AGILITY_MODIFIER =
        AttributeSnapshot.ToStringName(AttributeSnapshotIdKind.AgilityModifier);
    internal static readonly StringName CONSTITUTION_MODIFIER =
        AttributeSnapshot.ToStringName(AttributeSnapshotIdKind.ConstitutionModifier);
    internal static readonly StringName PERCEPTION_MODIFIER =
        AttributeSnapshot.ToStringName(AttributeSnapshotIdKind.PerceptionModifier);
    internal static readonly StringName INTELLIGENCE_MODIFIER =
        AttributeSnapshot.ToStringName(AttributeSnapshotIdKind.IntelligenceModifier);
    internal static readonly StringName WILLPOWER_MODIFIER =
        AttributeSnapshot.ToStringName(AttributeSnapshotIdKind.WillpowerModifier);
    internal static readonly StringName ARMOR_CLASS = "armor_class";
    internal static readonly StringName ARMOR_AC_BONUS = "armor_ac_bonus";
    internal static readonly StringName SHIELD_AC_BONUS = "shield_ac_bonus";
    internal static readonly StringName DODGE_BONUS = "dodge_bonus";
    internal static readonly StringName DEFLECTION_BONUS = "deflection_bonus";
    internal static readonly StringName ARMOR_MAX_DEX_BONUS = "armor_max_dex_bonus";
    internal static readonly StringName BASE_ATTACK_BONUS =
        AttributeSnapshot.ToStringName(AttributeSnapshotIdKind.BaseAttackBonus);
    internal static readonly StringName SPELL_PROFICIENCY_BONUS =
        AttributeSnapshot.ToStringName(AttributeSnapshotIdKind.SpellProficiencyBonus);

    internal const int DEFAULT_CHARACTER_ACTION_THRESHOLD = 30;
    private const int ACTION_THRESHOLD_GRANULARITY = 5;
    internal const int BASE_ARMOR_CLASS = 8;

    internal static readonly StringName[] RESOURCE_ATTRIBUTE_IDS =
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

    internal static readonly StringName[] COMBAT_ATTRIBUTE_IDS =
    {
        ARMOR_CLASS,
        ARMOR_AC_BONUS,
        SHIELD_AC_BONUS,
        DODGE_BONUS,
        DEFLECTION_BONUS,
        ARMOR_MAX_DEX_BONUS,
    };

    internal static readonly StringName[] AC_COMPONENT_ATTRIBUTE_IDS =
    {
        ARMOR_AC_BONUS,
        SHIELD_AC_BONUS,
        DODGE_BONUS,
        DEFLECTION_BONUS,
    };

    internal static readonly StringName[] PROTECTED_CUSTOM_STAT_KEYS =
    {
        UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.HiddenLuckAtBirth),
    };

    internal static readonly StringName PROTECTED_CUSTOM_STAT_SOURCE_CHARACTER_CREATION =
        "character_creation";
    internal static readonly StringName PROTECTED_CUSTOM_STAT_SOURCE_STORY_SCRIPT = "story_script";
    internal static readonly StringName PROTECTED_CUSTOM_STAT_WRITE_FLAG =
        "allow_protected_custom_stat_write";

    internal static StringName ToStringName(AttributeIdKind kind)
    {
        return kind switch
        {
            AttributeIdKind.HpMax => HP_MAX,
            AttributeIdKind.CharacterHpMaxPercentBonus => CHARACTER_HP_MAX_PERCENT_BONUS,
            AttributeIdKind.MpMax => MP_MAX,
            AttributeIdKind.StaminaMax => STAMINA_MAX,
            AttributeIdKind.StaminaRecoveryPercentBonus => STAMINA_RECOVERY_PERCENT_BONUS,
            AttributeIdKind.AuraMax => AURA_MAX,
            AttributeIdKind.ActionPoints => ACTION_POINTS,
            AttributeIdKind.ActionThreshold => ACTION_THRESHOLD,
            AttributeIdKind.AttackBonus => ATTACK_BONUS,
            AttributeIdKind.WeaponAttackRange => WEAPON_ATTACK_RANGE,
            AttributeIdKind.StrengthModifier => STRENGTH_MODIFIER,
            AttributeIdKind.AgilityModifier => AGILITY_MODIFIER,
            AttributeIdKind.ConstitutionModifier => CONSTITUTION_MODIFIER,
            AttributeIdKind.PerceptionModifier => PERCEPTION_MODIFIER,
            AttributeIdKind.IntelligenceModifier => INTELLIGENCE_MODIFIER,
            AttributeIdKind.WillpowerModifier => WILLPOWER_MODIFIER,
            AttributeIdKind.ArmorClass => ARMOR_CLASS,
            AttributeIdKind.ArmorAcBonus => ARMOR_AC_BONUS,
            AttributeIdKind.ShieldAcBonus => SHIELD_AC_BONUS,
            AttributeIdKind.DodgeBonus => DODGE_BONUS,
            AttributeIdKind.DeflectionBonus => DEFLECTION_BONUS,
            AttributeIdKind.ArmorMaxDexBonus => ARMOR_MAX_DEX_BONUS,
            AttributeIdKind.BaseAttackBonus => BASE_ATTACK_BONUS,
            AttributeIdKind.SpellProficiencyBonus => SPELL_PROFICIENCY_BONUS,
            _ => new StringName(""),
        };
    }

    internal static StringName ToStringName(AttributeSourceKind kind)
    {
        return kind switch
        {
            AttributeSourceKind.CharacterCreation => PROTECTED_CUSTOM_STAT_SOURCE_CHARACTER_CREATION,
            AttributeSourceKind.StoryScript => PROTECTED_CUSTOM_STAT_SOURCE_STORY_SCRIPT,
            _ => new StringName(""),
        };
    }

    internal static StringName ToStringName(AttributeServiceKeyKind kind)
    {
        return kind switch
        {
            AttributeServiceKeyKind.ProtectedCustomStatWriteFlag => PROTECTED_CUSTOM_STAT_WRITE_FLAG,
            _ => new StringName(""),
        };
    }

    private UnitProgress _unit_progress;
    private Dictionary<StringName, SkillDef> _skill_defs = new();
    private Dictionary<StringName, ProfessionDef> _profession_defs = new();
    private List<AttributeModifier> _equipment_state = new();
    private List<AttributeModifier> _passive_state = new();
    private List<AttributeModifier> _temporary_effects = new();
    private Dictionary<StringName, DerivedAttributeRule> _derived_rules = new();
    private AttributeSourceContext _context;
    private AttributeSnapshot _cached_snapshot;
    private bool _snapshot_dirty = true;

    private readonly record struct AttributeModifierEntry(
        StringName AttributeId,
        AttributeModifierMode Mode,
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

    public void Setup(UnitProgress unit_progress)
    {
        SetupContext(new AttributeSourceContext { unit_progress = unit_progress });
    }

    internal void SetupContext(AttributeSourceContext context)
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
        InvalidateSnapshot();
    }

    public void InvalidateSnapshot()
    {
        _snapshot_dirty = true;
    }

    public int GetBaseValue(StringName attribute_id)
    {
        var unitBaseAttributes = GetUnitBaseAttributes();
        return unitBaseAttributes?.GetAttributeValue(attribute_id) ?? 0;
    }

    public int GetTotalValue(StringName attribute_id)
    {
        return GetSnapshot().GetValue(attribute_id);
    }

    public int GetModifier(StringName attribute_id)
    {
        return CalculateScoreModifier(GetTotalValue(attribute_id));
    }

    public int GetActionPoints()
    {
        return GetTotalValue(ACTION_POINTS);
    }

    public AttributeSnapshot GetSnapshot()
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

        foreach (StringName attributeId in UnitBaseAttributes.GetBaseAttributeIdsTyped())
            snapshot.SetValue(attributeId, GetDictInt(resolvedBaseValues, attributeId, 0));

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
                snapshot.SetValue(attributeId, ResolveCharacterHpMaxPercentBonus(modifierEntries));
                continue;
            }
            else if (attributeId == ARMOR_CLASS)
            {
                derivedValue = CalculateBaseArmorClass(resolvedBaseValues, modifierEntries);
            }
            else if (attributeId == ARMOR_MAX_DEX_BONUS)
            {
                snapshot.SetValue(attributeId, ResolveArmorMaxDexBonus(modifierEntries));
                continue;
            }
            else if (_derived_rules.TryGetValue(attributeId, out DerivedAttributeRule rule))
            {
                if (rule != null)
                    derivedValue += rule.evaluate(resolvedBaseValues);
            }

            snapshot.SetValue(
                attributeId,
                ApplyModifierPipeline(attributeId, derivedValue, modifierEntries)
            );
        }

        foreach (StringName attributeId in GetAdditionalAttributeIds(modifierEntries))
        {
            if (snapshot.HasValue(attributeId))
                continue;
            snapshot.SetValue(
                attributeId,
                ApplyModifierPipeline(
                    attributeId,
                    GetPersistentBaseValue(attributeId),
                    modifierEntries
                )
            );
        }

        snapshot.SetValue(BASE_ATTACK_BONUS, CalculateBaseAttackBonus());
        snapshot.SetValue(SPELL_PROFICIENCY_BONUS, CalculateSpellProficiencyBonus());
        return snapshot;
    }

    public bool ApplyPermanentAttributeChange(
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
            !UnitBaseAttributes.IsBaseAttributeId(attribute_id)
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

        unitBaseAttributes.SetAttributeValue(
            attribute_id,
            unitBaseAttributes.GetAttributeValue(attribute_id) + delta
        );
        InvalidateSnapshot();
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
        return !UnitBaseAttributes.IsBaseAttributeId(attributeId)
            && ContainsAttributeId(PROTECTED_CUSTOM_STAT_KEYS, attributeId);
    }

    private bool CanWriteCustomStat(StringName attributeId)
    {
        if (attributeId == "" || UnitBaseAttributes.IsBaseAttributeId(attributeId))
            return false;
        var unitBaseAttributes = GetUnitBaseAttributes();
        if (unitBaseAttributes == null)
            return false;
        return unitBaseAttributes.custom_stats.ContainsKey(attributeId)
            || ContainsAttributeId(PROTECTED_CUSTOM_STAT_KEYS, attributeId);
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

    private Dictionary<StringName, int> ResolveBaseAttributeValues(
        List<AttributeModifierEntry> modifierEntries
    )
    {
        var resolvedValues = new Dictionary<StringName, int>();
        foreach (StringName attributeId in UnitBaseAttributes.GetBaseAttributeIdsTyped())
            resolvedValues[attributeId] = ApplyModifierPipeline(
                attributeId,
                GetBaseValue(attributeId),
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
        if (!UnitBaseAttributes.IsBaseAttributeId(_context.versatility_pick))
            return;

        var modifier = new AttributeModifier
        {
            attribute_id = _context.versatility_pick,
            mode = AttributeModifier.ToStringName(AttributeModifierMode.Flat),
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
        foreach (StringName professionId in _unit_progress.GetSortedProfessionIdsTyped())
        {
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
        foreach (StringName skillId in _unit_progress.GetSortedSkillIdsTyped())
        {
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
                skillDef.AttributeModifiersTyped,
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
                modifier.ModeKind,
                modifier.GetValueForRank(rank),
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
        return unitBaseAttributes.GetAttributeValue(attributeId);
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

            if (entry.Mode == AttributeModifierMode.Percent)
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
            if (entry.Mode == AttributeModifierMode.Percent)
                continue;
            percentBonus += Mathf.Max(entry.Value, 0);
        }
        return percentBonus;
    }

    private int CalculateBaseAttackBonus()
    {
        if (_unit_progress == null)
            return 0;
        var pairs = new List<AttributeSnapshot.BaseAttackProgressionPair>();
        foreach (StringName professionId in _unit_progress.GetSortedProfessionIdsTyped())
        {
            var professionProgress = GetProfessionProgress(professionId);
            if (professionProgress == null || professionProgress.rank <= 0)
                continue;
            if (!professionProgress.is_active || professionProgress.is_hidden)
                continue;
            if (!_profession_defs.TryGetValue(professionId, out var professionDef))
                continue;
            pairs.Add(
                new AttributeSnapshot.BaseAttackProgressionPair(
                    professionProgress.rank,
                    professionDef.BabProgressionKind
                )
            );
        }
        return AttributeSnapshot.CalculateBaseAttackBonus(pairs);
    }

    private int CalculateSpellProficiencyBonus()
    {
        if (_unit_progress == null)
            return AttributeSnapshot.CalculateSpellProficiencyBonus(0);
        return AttributeSnapshot.CalculateSpellProficiencyBonus(
            _unit_progress.character_level
        );
    }

    private int CalculateBaseArmorClass(
        Dictionary<StringName, int> resolvedBaseValues,
        List<AttributeModifierEntry> modifierEntries
    )
    {
        int agility = GetDictInt(resolvedBaseValues, UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Agility), 0);
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
        return AttributeSnapshot.CalculateScoreModifier(score);
    }

    private int ResolveArmorMaxDexBonus(List<AttributeModifierEntry> modifierEntries)
    {
        int resolvedCap = -1;
        foreach (var entry in modifierEntries)
        {
            if (entry.AttributeId != ARMOR_MAX_DEX_BONUS)
                continue;
            if (entry.Mode == AttributeModifierMode.Percent)
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
            && ContainsAttributeId(AC_COMPONENT_ATTRIBUTE_IDS, modifierAttributeId);
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

    private static List<StringName> GetKnownNonBaseAttributeIds()
    {
        var result = new List<StringName>();
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

    private List<StringName> GetAdditionalAttributeIds(
        List<AttributeModifierEntry> modifierEntries
    )
    {
        var result = new List<StringName>();
        var seen = new HashSet<StringName>();
        var knownAttributeIds = GetKnownNonBaseAttributeIds();

        foreach (StringName attributeId in UnitBaseAttributes.GetBaseAttributeIdsTyped())
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

    private static Dictionary<StringName, DerivedAttributeRule> BuildDefaultRules()
    {
        var rules = new Dictionary<StringName, DerivedAttributeRule>();
        rules[STAMINA_MAX] = new DerivedAttributeRule(
            STAMINA_MAX,
            24,
            new GDictionary
            {
                [UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Constitution)] = 5,
                [UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Strength)] = 1,
                [UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Agility)] = 1,
            },
            1,
            0,
            0,
            0
        );
        rules[ACTION_POINTS] = new DerivedAttributeRule(
            ACTION_POINTS,
            1,
            new GDictionary { [UnitBaseAttributes.ToStringName(UnitBaseAttributeKind.Agility)] = 1 },
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
        return _unit_progress.GetProfessionProgress(professionId);
    }

    private UnitSkillProgress GetSkillProgress(StringName skillId)
    {
        if (_unit_progress == null)
            return null;
        return _unit_progress.GetSkillProgress(skillId);
    }

    private static List<AttributeModifier> CopyAttributeModifierList(
        List<AttributeModifier> values
    )
    {
        return values != null ? new List<AttributeModifier>(values) : new List<AttributeModifier>();
    }

    private static bool ContainsAttributeId(StringName[] values, StringName attributeId)
    {
        foreach (StringName value in values)
        {
            if (value == attributeId)
                return true;
        }
        return false;
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
