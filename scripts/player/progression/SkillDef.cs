using System.Collections.Generic;
using Godot;
using GdDictionary = Godot.Collections.Dictionary;

internal enum SkillTypeKind
{
    Unknown = 0,
    Active,
    Passive,
}

internal enum SkillLearnSourceKind
{
    Unknown = 0,
    Book,
    Innate,
    Player,
    Profession,
    Race,
    Subrace,
    Ascension,
    Bloodline,
}

internal enum SkillPracticeTierKind
{
    None = 0,
    Unknown,
    Basic,
    Intermediate,
    Advanced,
    Ultimate,
}

internal enum SkillUnlockMode
{
    Unknown = 0,
    Standard,
    CompositeUpgrade,
}

internal enum CoreSkillTransitionMode
{
    Unknown = 0,
    Inherit,
    ReplaceSourcesWithResult,
}

[GlobalClass]
public partial class SkillDef : Resource
{
    private static readonly StringName UnlockModeStandard = "standard";
    private static readonly StringName UnlockModeCompositeUpgrade = "composite_upgrade";
    private static readonly StringName CoreTransitionInherit = "inherit";
    private static readonly StringName CoreTransitionReplaceSources = "replace_sources_with_result";
    private static readonly StringName SkillTypeActive = "active";
    private static readonly StringName SkillTypePassive = "passive";
    private static readonly StringName LearnSourceBook = "book";
    private static readonly StringName LearnSourceInnate = "innate";
    private static readonly StringName LearnSourcePlayer = "player";
    private static readonly StringName LearnSourceProfession = "profession";
    private static readonly StringName LearnSourceRace = "race";
    private static readonly StringName LearnSourceSubrace = "subrace";
    private static readonly StringName LearnSourceAscension = "ascension";
    private static readonly StringName LearnSourceBloodline = "bloodline";
    private static readonly StringName PracticeTierBasic = "basic";
    private static readonly StringName PracticeTierIntermediate = "intermediate";
    private static readonly StringName PracticeTierAdvanced = "advanced";
    private static readonly StringName PracticeTierUltimate = "ultimate";

    private readonly Dictionary<StringName, int> _attributeGrowthProgress = new();
    private readonly List<AttributeGrowthProgressEntryData> _attributeGrowthProgressEntries =
        new();
    private readonly List<AttributeModifier> _attributeModifiers = new();
    private readonly List<StringName> _tags = new();
    private readonly List<StringName> _learnRequirements = new();
    private readonly List<StringName> _knowledgeRequirements = new();
    private readonly Dictionary<StringName, int> _skillLevelRequirements = new();
    private readonly List<IntRequirementEntryData> _skillLevelRequirementEntries = new();
    private readonly Dictionary<StringName, int> _attributeRequirements = new();
    private readonly List<IntRequirementEntryData> _attributeRequirementEntries = new();
    private readonly List<StringName> _achievementRequirements = new();
    private readonly List<StringName> _upgradeSourceSkillIds = new();
    private readonly List<StringName> _masterySources = new();
    private readonly Dictionary<int, Dictionary<string, Variant>> _levelDescriptionConfigs =
        new();
    private readonly List<LevelDescriptionConfigEntryData> _levelDescriptionConfigEntries =
        new();
    private Godot.Collections.Dictionary _attributeGrowthProgressProjection = new();
    private Godot.Collections.Dictionary _skillLevelRequirementsProjection = new();
    private Godot.Collections.Dictionary _attributeRequirementsProjection = new();
    private Godot.Collections.Dictionary _levelDescriptionConfigsProjection = new();
    private Godot.Collections.Array<Resource> _attributeModifiersProjection = new();
    private Godot.Collections.Array<StringName> _tagsProjection = new();

    [Export]
    public StringName skill_id { get; set; } = "";

    [Export]
    public string display_name { get; set; } = "";

    [Export]
    public StringName icon_id { get; set; } = "";

    [Export(PropertyHint.MultilineText)]
    public string description { get; set; } = "";

    [Export]
    public StringName skill_type { get; set; } = SkillTypeActive;
    internal SkillTypeKind SkillTypeKind
    {
        get => ToSkillType(skill_type);
        set => skill_type = ToStringName(value);
    }

    [Export]
    public int max_level { get; set; } = 1;

    [Export]
    public int non_core_max_level { get; set; }

    [Export]
    public StringName dynamic_max_level_stat_id { get; set; } = "";

    [Export]
    public int dynamic_max_level_base { get; set; }

    [Export]
    public int dynamic_max_level_per_stat { get; set; }

    [Export]
    public int[] mastery_curve { get; set; } = System.Array.Empty<int>();

    [Export]
    public Godot.Collections.Array<StringName> tags
    {
        get => BuildStringNameReadProjection(_tagsProjection, "SkillDef.tags.get");
        set => SetTags(value);
    }

    [Export]
    public StringName learn_source { get; set; } = LearnSourceBook;
    internal SkillLearnSourceKind LearnSourceKind
    {
        get => ToLearnSource(learn_source);
        set => learn_source = ToStringName(value);
    }

    [Export]
    public Godot.Collections.Array<StringName> learn_requirements
    {
        get => BuildStringNameReadProjection(_learnRequirements, "SkillDef.learn_requirements.get");
        set => SetUniqueStringNames(_learnRequirements, value);
    }

    [Export]
    public StringName unlock_mode { get; set; } = "standard";

    internal SkillUnlockMode UnlockModeKind
    {
        get => ToUnlockMode(unlock_mode);
        set => unlock_mode = ToStringName(value);
    }

    [Export]
    public Godot.Collections.Array<StringName> knowledge_requirements
    {
        get => BuildStringNameReadProjection(
            _knowledgeRequirements,
            "SkillDef.knowledge_requirements.get"
        );
        set => SetUniqueStringNames(_knowledgeRequirements, value);
    }

    [Export]
    public Godot.Collections.Dictionary skill_level_requirements
    {
        get => CopyReadProjectionDictionary(
            _skillLevelRequirementsProjection,
            "SkillDef.skill_level_requirements.get"
        );
        set => SetSkillLevelRequirements(value);
    }

    [Export]
    public Godot.Collections.Dictionary attribute_requirements
    {
        get => CopyReadProjectionDictionary(
            _attributeRequirementsProjection,
            "SkillDef.attribute_requirements.get"
        );
        set => SetAttributeRequirements(value);
    }

    [Export]
    public Godot.Collections.Array<StringName> achievement_requirements
    {
        get => BuildStringNameReadProjection(
            _achievementRequirements,
            "SkillDef.achievement_requirements.get"
        );
        set => SetUniqueStringNames(_achievementRequirements, value);
    }

    [Export]
    public Godot.Collections.Array<StringName> upgrade_source_skill_ids
    {
        get => BuildStringNameReadProjection(
            _upgradeSourceSkillIds,
            "SkillDef.upgrade_source_skill_ids.get"
        );
        set => SetUniqueStringNames(_upgradeSourceSkillIds, value);
    }

    [Export]
    public bool retain_source_skills_on_unlock { get; set; } = true;

    [Export]
    public StringName core_skill_transition_mode { get; set; } = "inherit";

    internal CoreSkillTransitionMode CoreSkillTransitionModeKind
    {
        get => ToCoreSkillTransitionMode(core_skill_transition_mode);
        set => core_skill_transition_mode = ToStringName(value);
    }

    [Export]
    public Godot.Collections.Array<StringName> mastery_sources
    {
        get => BuildStringNameReadProjection(_masterySources, "SkillDef.mastery_sources.get");
        set => SetUniqueStringNames(_masterySources, value);
    }

    [Export]
    public StringName growth_tier { get; set; } = "";

    [Export]
    public Godot.Collections.Dictionary attribute_growth_progress
    {
        get => CopyReadProjectionDictionary(
            _attributeGrowthProgressProjection,
            "SkillDef.attribute_growth_progress.get"
        );
        set => SetAttributeGrowthProgress(value);
    }

    [Export]
    public StringName practice_tier { get; set; } = "";
    internal SkillPracticeTierKind PracticeTierKind
    {
        get => ToPracticeTier(practice_tier);
        set => practice_tier = ToStringName(value);
    }

    [Export]
    public Godot.Collections.Array<Resource> attribute_modifiers
    {
        get => BuildResourceReadProjection(
            _attributeModifiersProjection,
            "SkillDef.attribute_modifiers.get"
        );
        set => SetAttributeModifiers(value);
    }

    [Export(PropertyHint.MultilineText)]
    public string level_description_template { get; set; } = "";

    [Export]
    public Godot.Collections.Dictionary level_description_configs
    {
        get => CopyReadProjectionDictionary(
            _levelDescriptionConfigsProjection,
            "SkillDef.level_description_configs.get"
        );
        set => SetLevelDescriptionConfigs(value);
    }

    [Export]
    public CombatSkillDef combat_profile { get; set; }

    [Export]
    public ContingencyAutomationDef contingency_automation_profile { get; set; }

    internal IReadOnlyDictionary<StringName, int> AttributeGrowthProgressTyped =>
        _attributeGrowthProgress;
    internal IReadOnlyList<AttributeGrowthProgressEntryData> AttributeGrowthProgressEntriesTyped =>
        _attributeGrowthProgressEntries;
    internal IReadOnlyList<AttributeModifier> AttributeModifiersTyped => _attributeModifiers;
    internal IReadOnlyList<StringName> TagsTyped => _tags;
    internal IReadOnlyList<StringName> LearnRequirementsTyped => _learnRequirements;
    internal IReadOnlyList<StringName> KnowledgeRequirementsTyped => _knowledgeRequirements;
    internal IReadOnlyDictionary<StringName, int> SkillLevelRequirementsTyped =>
        _skillLevelRequirements;
    internal IReadOnlyList<IntRequirementEntryData> SkillLevelRequirementEntriesTyped =>
        _skillLevelRequirementEntries;
    internal IReadOnlyDictionary<StringName, int> AttributeRequirementsTyped =>
        _attributeRequirements;
    internal IReadOnlyList<IntRequirementEntryData> AttributeRequirementEntriesTyped =>
        _attributeRequirementEntries;
    internal IReadOnlyList<StringName> AchievementRequirementsTyped => _achievementRequirements;
    internal IReadOnlyList<StringName> UpgradeSourceSkillIdsTyped => _upgradeSourceSkillIds;
    internal IReadOnlyList<StringName> MasterySourcesTyped => _masterySources;
    internal IReadOnlyDictionary<int, Dictionary<string, Variant>> LevelDescriptionConfigsTyped =>
        _levelDescriptionConfigs;
    internal IReadOnlyList<LevelDescriptionConfigEntryData> LevelDescriptionConfigEntriesTyped =>
        _levelDescriptionConfigEntries;

    private void SetAttributeGrowthProgress(Godot.Collections.Dictionary values)
    {
        _attributeGrowthProgressProjection =
            values?.Duplicate(true) ?? new Godot.Collections.Dictionary();
        _attributeGrowthProgress.Clear();
        _attributeGrowthProgressEntries.Clear();
        foreach (Variant rawKey in _attributeGrowthProgressProjection.Keys)
        {
            Variant rawValue = _attributeGrowthProgressProjection[rawKey];
            AttributeGrowthProgressEntryData entry = AttributeGrowthProgressEntryData.FromRaw(
                rawKey,
                rawValue
            );
            _attributeGrowthProgressEntries.Add(entry);
            if (
                entry.HasStrictStringKey
                && entry.HasNonEmptyKey
                && entry.HasStrictIntAmount
                && entry.AttributeId != ""
            )
            {
                _attributeGrowthProgress[entry.AttributeId] = entry.Amount;
            }
        }
    }

    public void SetAttributeGrowthProgress(IEnumerable<KeyValuePair<StringName, int>> values)
    {
        _attributeGrowthProgressProjection = new Godot.Collections.Dictionary();
        _attributeGrowthProgress.Clear();
        _attributeGrowthProgressEntries.Clear();
        if (values == null)
            return;
        foreach (KeyValuePair<StringName, int> pair in values)
        {
            if (pair.Key == "")
                continue;
            _attributeGrowthProgress[pair.Key] = pair.Value;
            string keyText = pair.Key.ToString();
            _attributeGrowthProgressProjection[keyText] = pair.Value;
            _attributeGrowthProgressEntries.Add(
                AttributeGrowthProgressEntryData.Valid(pair.Key, pair.Value)
            );
        }
    }

    private void SetTags(Godot.Collections.Array<StringName> values)
    {
        _tagsProjection = BuildStringNameArray(values);
        _tags.Clear();
        if (values == null)
            return;
        foreach (StringName value in values)
            _tags.Add(value);
    }

    public void SetTags(IEnumerable<StringName> values)
    {
        _tagsProjection = BuildStringNameArray(values);
        _tags.Clear();
        if (values == null)
            return;
        foreach (StringName value in values)
            _tags.Add(value);
    }

    public bool HasTag(StringName tag)
    {
        foreach (StringName value in _tags)
        {
            if (value == tag)
                return true;
        }
        return false;
    }

    private void SetSkillLevelRequirements(Godot.Collections.Dictionary values)
    {
        _skillLevelRequirementsProjection =
            values?.Duplicate(true) ?? new Godot.Collections.Dictionary();
        _skillLevelRequirements.Clear();
        _skillLevelRequirementEntries.Clear();
        foreach (Variant rawKey in _skillLevelRequirementsProjection.Keys)
        {
            Variant rawValue = _skillLevelRequirementsProjection[rawKey];
            IntRequirementEntryData entry = IntRequirementEntryData.FromRaw(rawKey, rawValue);
            _skillLevelRequirementEntries.Add(entry);
            if (entry.HasNonEmptyKey && entry.HasStrictIntAmount && entry.RequirementId != "")
                _skillLevelRequirements[entry.RequirementId] = entry.Amount;
        }
    }

    public void SetSkillLevelRequirements(IEnumerable<KeyValuePair<StringName, int>> values)
    {
        _skillLevelRequirementsProjection = new Godot.Collections.Dictionary();
        _skillLevelRequirements.Clear();
        _skillLevelRequirementEntries.Clear();
        if (values == null)
            return;
        foreach (KeyValuePair<StringName, int> pair in values)
        {
            if (pair.Key == "")
                continue;
            string keyText = pair.Key.ToString();
            _skillLevelRequirementsProjection[keyText] = pair.Value;
            _skillLevelRequirements[pair.Key] = pair.Value;
            _skillLevelRequirementEntries.Add(IntRequirementEntryData.Valid(pair.Key, pair.Value));
        }
    }

    private void SetAttributeRequirements(Godot.Collections.Dictionary values)
    {
        _attributeRequirementsProjection =
            values?.Duplicate(true) ?? new Godot.Collections.Dictionary();
        _attributeRequirements.Clear();
        _attributeRequirementEntries.Clear();
        foreach (Variant rawKey in _attributeRequirementsProjection.Keys)
        {
            Variant rawValue = _attributeRequirementsProjection[rawKey];
            IntRequirementEntryData entry = IntRequirementEntryData.FromRaw(rawKey, rawValue);
            _attributeRequirementEntries.Add(entry);
            if (entry.HasNonEmptyKey && entry.HasStrictIntAmount && entry.RequirementId != "")
                _attributeRequirements[entry.RequirementId] = entry.Amount;
        }
    }

    public void SetAttributeRequirements(IEnumerable<KeyValuePair<StringName, int>> values)
    {
        _attributeRequirementsProjection = new Godot.Collections.Dictionary();
        _attributeRequirements.Clear();
        _attributeRequirementEntries.Clear();
        if (values == null)
            return;
        foreach (KeyValuePair<StringName, int> pair in values)
        {
            if (pair.Key == "")
                continue;
            string keyText = pair.Key.ToString();
            _attributeRequirementsProjection[keyText] = pair.Value;
            _attributeRequirements[pair.Key] = pair.Value;
            _attributeRequirementEntries.Add(IntRequirementEntryData.Valid(pair.Key, pair.Value));
        }
    }

    private void SetAttributeModifiers(Godot.Collections.Array<Resource> values)
    {
        _attributeModifiersProjection = BuildResourceProjection(values);
        _attributeModifiers.Clear();
        if (values == null)
            return;
        foreach (Resource value in values)
        {
            if (value is AttributeModifier modifier)
                _attributeModifiers.Add(modifier);
        }
    }

    public void SetAttributeModifiers(IEnumerable<AttributeModifier> values)
    {
        _attributeModifiersProjection = new Godot.Collections.Array<Resource>();
        _attributeModifiers.Clear();
        if (values == null)
            return;
        foreach (AttributeModifier modifier in values)
        {
            if (modifier == null)
                continue;
            _attributeModifiers.Add(modifier);
            _attributeModifiersProjection.Add(modifier);
        }
    }

    private void SetLevelDescriptionConfigs(Godot.Collections.Dictionary values)
    {
        _levelDescriptionConfigsProjection =
            values?.Duplicate(true) ?? new Godot.Collections.Dictionary();
        _levelDescriptionConfigs.Clear();
        _levelDescriptionConfigEntries.Clear();
        foreach (Variant rawKey in _levelDescriptionConfigsProjection.Keys)
        {
            Variant rawValue = _levelDescriptionConfigsProjection[rawKey];
            LevelDescriptionConfigEntryData entry = LevelDescriptionConfigEntryData.FromRaw(
                rawKey,
                rawValue
            );
            _levelDescriptionConfigEntries.Add(entry);
            if (entry.HasParsedLevelKey && entry.ValueIsDictionary)
                _levelDescriptionConfigs[entry.Level] = entry.ConfigValues;
        }
    }

    public void SetLevelDescriptionConfigs(
        IEnumerable<KeyValuePair<int, Dictionary<string, Variant>>> values
    )
    {
        _levelDescriptionConfigsProjection = new Godot.Collections.Dictionary();
        _levelDescriptionConfigs.Clear();
        _levelDescriptionConfigEntries.Clear();
        if (values == null)
            return;
        foreach (KeyValuePair<int, Dictionary<string, Variant>> pair in values)
        {
            if (pair.Key < 0 || pair.Value == null)
                continue;
            GdDictionary configProjection = BuildVariantProjection(pair.Value);
            string levelKey = pair.Key.ToString();
            _levelDescriptionConfigsProjection[levelKey] = configProjection;
            _levelDescriptionConfigs[pair.Key] = CloneVariantMap(pair.Value);
            _levelDescriptionConfigEntries.Add(
                LevelDescriptionConfigEntryData.Valid(pair.Key, pair.Value)
            );
        }
    }

    public int GetMasteryRequiredForLevel(int level)
    {
        if (level < 0)
            return 0;

        if (level < mastery_curve.Length)
            return mastery_curve[level];

        if (mastery_curve.Length <= 0)
            return 0;

        if (mastery_curve.Length == 1)
            return mastery_curve[0];

        int lastIndex = mastery_curve.Length - 1;

        int delta = Mathf.Max(mastery_curve[lastIndex] - mastery_curve[lastIndex - 1], 1);

        return mastery_curve[lastIndex] + delta * (level - lastIndex);
    }

    public bool IsProfessionSkill() => LearnSourceKind == SkillLearnSourceKind.Profession;

    public bool CanUseInCombat() => combat_profile != null;

    internal static SkillTypeKind ToSkillType(StringName value)
    {
        if (value == SkillTypeActive)
            return SkillTypeKind.Active;
        if (value == SkillTypePassive)
            return SkillTypeKind.Passive;
        return SkillTypeKind.Unknown;
    }

    internal static StringName ToStringName(SkillTypeKind value)
    {
        return value switch
        {
            SkillTypeKind.Active => SkillTypeActive,
            SkillTypeKind.Passive => SkillTypePassive,
            _ => "",
        };
    }

    internal static SkillLearnSourceKind ToLearnSource(StringName value)
    {
        if (value == LearnSourceBook)
            return SkillLearnSourceKind.Book;
        if (value == LearnSourceInnate)
            return SkillLearnSourceKind.Innate;
        if (value == LearnSourcePlayer)
            return SkillLearnSourceKind.Player;
        if (value == LearnSourceProfession)
            return SkillLearnSourceKind.Profession;
        if (value == LearnSourceRace)
            return SkillLearnSourceKind.Race;
        if (value == LearnSourceSubrace)
            return SkillLearnSourceKind.Subrace;
        if (value == LearnSourceAscension)
            return SkillLearnSourceKind.Ascension;
        if (value == LearnSourceBloodline)
            return SkillLearnSourceKind.Bloodline;
        return SkillLearnSourceKind.Unknown;
    }

    internal static StringName ToStringName(SkillLearnSourceKind value)
    {
        return value switch
        {
            SkillLearnSourceKind.Book => LearnSourceBook,
            SkillLearnSourceKind.Innate => LearnSourceInnate,
            SkillLearnSourceKind.Player => LearnSourcePlayer,
            SkillLearnSourceKind.Profession => LearnSourceProfession,
            SkillLearnSourceKind.Race => LearnSourceRace,
            SkillLearnSourceKind.Subrace => LearnSourceSubrace,
            SkillLearnSourceKind.Ascension => LearnSourceAscension,
            SkillLearnSourceKind.Bloodline => LearnSourceBloodline,
            _ => "",
        };
    }

    internal static SkillPracticeTierKind ToPracticeTier(StringName value)
    {
        if (value == "")
            return SkillPracticeTierKind.None;
        if (value == PracticeTierBasic)
            return SkillPracticeTierKind.Basic;
        if (value == PracticeTierIntermediate)
            return SkillPracticeTierKind.Intermediate;
        if (value == PracticeTierAdvanced)
            return SkillPracticeTierKind.Advanced;
        if (value == PracticeTierUltimate)
            return SkillPracticeTierKind.Ultimate;
        return SkillPracticeTierKind.Unknown;
    }

    internal static StringName ToStringName(SkillPracticeTierKind value)
    {
        return value switch
        {
            SkillPracticeTierKind.Basic => PracticeTierBasic,
            SkillPracticeTierKind.Intermediate => PracticeTierIntermediate,
            SkillPracticeTierKind.Advanced => PracticeTierAdvanced,
            SkillPracticeTierKind.Ultimate => PracticeTierUltimate,
            _ => "",
        };
    }

    internal static SkillUnlockMode ToUnlockMode(StringName value)
    {
        if (value == UnlockModeStandard)
            return SkillUnlockMode.Standard;
        if (value == UnlockModeCompositeUpgrade)
            return SkillUnlockMode.CompositeUpgrade;
        return SkillUnlockMode.Unknown;
    }

    internal static StringName ToStringName(SkillUnlockMode mode)
    {
        return mode switch
        {
            SkillUnlockMode.Standard => UnlockModeStandard,
            SkillUnlockMode.CompositeUpgrade => UnlockModeCompositeUpgrade,
            _ => "",
        };
    }

    internal static CoreSkillTransitionMode ToCoreSkillTransitionMode(StringName value)
    {
        if (value == CoreTransitionInherit)
            return CoreSkillTransitionMode.Inherit;
        if (value == CoreTransitionReplaceSources)
            return CoreSkillTransitionMode.ReplaceSourcesWithResult;
        return CoreSkillTransitionMode.Unknown;
    }

    internal static StringName ToStringName(CoreSkillTransitionMode mode)
    {
        return mode switch
        {
            CoreSkillTransitionMode.Inherit => CoreTransitionInherit,
            CoreSkillTransitionMode.ReplaceSourcesWithResult => CoreTransitionReplaceSources,
            _ => "",
        };
    }

    internal sealed class AttributeGrowthProgressEntryData
    {
        public readonly string RawKeyLabel;
        public readonly StringName AttributeId;
        public readonly bool HasStrictStringKey;
        public readonly bool HasNonEmptyKey;
        public readonly bool HasStrictIntAmount;
        public readonly int Amount;

        private AttributeGrowthProgressEntryData(
            string rawKeyLabel,
            StringName attributeId,
            bool hasStrictStringKey,
            bool hasNonEmptyKey,
            bool hasStrictIntAmount,
            int amount
        )
        {
            RawKeyLabel = rawKeyLabel ?? "";
            AttributeId = attributeId;
            HasStrictStringKey = hasStrictStringKey;
            HasNonEmptyKey = hasNonEmptyKey;
            HasStrictIntAmount = hasStrictIntAmount;
            Amount = amount;
        }

        public static AttributeGrowthProgressEntryData FromRaw(Variant rawKey, Variant rawValue)
        {
            bool hasStrictStringKey = rawKey.VariantType == Variant.Type.String;
            string rawKeyLabel = rawKey.ToString();
            string keyText = hasStrictStringKey ? rawKey.AsString().StripEdges() : "";
            bool hasNonEmptyKey = hasStrictStringKey && keyText.Length > 0;
            bool hasStrictIntAmount = rawValue.VariantType == Variant.Type.Int;
            int amount = hasStrictIntAmount ? rawValue.AsInt32() : 0;
            return new AttributeGrowthProgressEntryData(
                rawKeyLabel,
                hasNonEmptyKey ? new StringName(keyText) : new StringName(),
                hasStrictStringKey,
                hasNonEmptyKey,
                hasStrictIntAmount,
                amount
            );
        }

        public static AttributeGrowthProgressEntryData Valid(StringName attributeId, int amount)
        {
            string keyText = attributeId.ToString();
            return new AttributeGrowthProgressEntryData(
                keyText,
                attributeId,
                true,
                keyText.Length > 0,
                true,
                amount
            );
        }
    }

    internal sealed class IntRequirementEntryData
    {
        public readonly string RawKeyLabel;
        public readonly StringName RequirementId;
        public readonly bool HasStringLikeKey;
        public readonly bool HasNonEmptyKey;
        public readonly bool HasStrictIntAmount;
        public readonly int Amount;

        private IntRequirementEntryData(
            string rawKeyLabel,
            StringName requirementId,
            bool hasStringLikeKey,
            bool hasNonEmptyKey,
            bool hasStrictIntAmount,
            int amount
        )
        {
            RawKeyLabel = rawKeyLabel ?? "";
            RequirementId = requirementId;
            HasStringLikeKey = hasStringLikeKey;
            HasNonEmptyKey = hasNonEmptyKey;
            HasStrictIntAmount = hasStrictIntAmount;
            Amount = amount;
        }

        public static IntRequirementEntryData FromRaw(Variant rawKey, Variant rawValue)
        {
            bool hasStringLikeKey =
                rawKey.VariantType == Variant.Type.String
                || rawKey.VariantType == Variant.Type.StringName;
            string rawKeyLabel = rawKey.ToString();
            string keyText =
                rawKey.VariantType == Variant.Type.String
                    ? rawKey.AsString().StripEdges()
                    : rawKey.VariantType == Variant.Type.StringName
                        ? rawKey.AsStringName().ToString().StripEdges()
                        : "";
            bool hasNonEmptyKey = hasStringLikeKey && keyText.Length > 0;
            bool hasStrictIntAmount = rawValue.VariantType == Variant.Type.Int;
            int amount = hasStrictIntAmount ? rawValue.AsInt32() : 0;
            return new IntRequirementEntryData(
                rawKeyLabel,
                hasNonEmptyKey ? new StringName(keyText) : new StringName(),
                hasStringLikeKey,
                hasNonEmptyKey,
                hasStrictIntAmount,
                amount
            );
        }

        public static IntRequirementEntryData Valid(StringName requirementId, int amount)
        {
            string keyText = requirementId.ToString();
            return new IntRequirementEntryData(
                keyText,
                requirementId,
                true,
                keyText.Length > 0,
                true,
                amount
            );
        }
    }

    internal sealed class LevelDescriptionConfigEntryData
    {
        public readonly string DisplayKey;
        public readonly bool KeyIsStrictString;
        public readonly string KeyText;
        public readonly bool HasParsedLevelKey;
        public readonly int Level;
        public readonly bool ValueIsDictionary;
        public readonly Dictionary<string, Variant> ConfigValues;

        private LevelDescriptionConfigEntryData(
            string displayKey,
            bool keyIsStrictString,
            string keyText,
            bool hasParsedLevelKey,
            int level,
            bool valueIsDictionary,
            Dictionary<string, Variant> configValues
        )
        {
            DisplayKey = displayKey ?? "";
            KeyIsStrictString = keyIsStrictString;
            KeyText = keyText ?? "";
            HasParsedLevelKey = hasParsedLevelKey;
            Level = level;
            ValueIsDictionary = valueIsDictionary;
            ConfigValues = configValues ?? new Dictionary<string, Variant>(System.StringComparer.Ordinal);
        }

        public static LevelDescriptionConfigEntryData FromRaw(Variant rawKey, Variant rawValue)
        {
            bool keyIsStrictString = rawKey.VariantType == Variant.Type.String;
            string keyText = keyIsStrictString ? rawKey.AsString().StripEdges() : "";
            bool hasParsedLevelKey = TryParseLevelKey(keyText, out int parsedLevel);
            bool valueIsDictionary = rawValue.VariantType == Variant.Type.Dictionary;
            Dictionary<string, Variant> configValues = valueIsDictionary
                ? ReadVariantMap(rawValue.AsGodotDictionary())
                : new Dictionary<string, Variant>(System.StringComparer.Ordinal);
            return new LevelDescriptionConfigEntryData(
                rawKey.ToString(),
                keyIsStrictString,
                keyText,
                hasParsedLevelKey,
                parsedLevel,
                valueIsDictionary,
                configValues
            );
        }

        public static LevelDescriptionConfigEntryData Valid(
            int level,
            Dictionary<string, Variant> configValues
        )
        {
            string keyText = level.ToString();
            return new LevelDescriptionConfigEntryData(
                keyText,
                true,
                keyText,
                true,
                level,
                true,
                CloneVariantMap(configValues)
            );
        }
    }

    private static Godot.Collections.Array<StringName> BuildStringNameArray(
        IEnumerable<StringName> values
    ) => values != null ? new Godot.Collections.Array<StringName>(values) : new();

    private static Godot.Collections.Array<StringName> BuildStringNameReadProjection(
        IEnumerable<StringName> values,
        string reason
    )
    {
        Godot.Collections.Array<StringName> result = BuildStringNameArray(values);
        RuntimeStateLifecycle.MarkValueGraphFinalizerless(result, reason);
        return result;
    }

    private static Godot.Collections.Array<Resource> BuildResourceProjection(
        IEnumerable<Resource> values
    )
    {
        var result = new Godot.Collections.Array<Resource>();
        if (values == null)
            return result;
        foreach (Resource value in values)
            result.Add(value);
        return result;
    }

    private static Godot.Collections.Array<Resource> BuildResourceReadProjection(
        IEnumerable<Resource> values,
        string reason
    )
    {
        Godot.Collections.Array<Resource> result = BuildResourceProjection(values);
        RuntimeStateLifecycle.MarkValueGraphFinalizerless(result, reason);
        return result;
    }

    private static GdDictionary CopyReadProjectionDictionary(GdDictionary source, string reason) =>
        RuntimePayloadCopy.Dictionary(source, reason);

    private static GdDictionary BuildVariantProjection(IReadOnlyDictionary<string, Variant> values)
    {
        GdDictionary result = new();
        if (values == null)
            return result;
        foreach (KeyValuePair<string, Variant> pair in values)
        {
            if (string.IsNullOrEmpty(pair.Key))
                continue;
            result[pair.Key] = pair.Value;
        }
        return result;
    }

    private static Dictionary<string, Variant> CloneVariantMap(
        IReadOnlyDictionary<string, Variant> values
    )
    {
        var result = new Dictionary<string, Variant>(System.StringComparer.Ordinal);
        if (values == null)
            return result;
        foreach (KeyValuePair<string, Variant> pair in values)
        {
            if (string.IsNullOrEmpty(pair.Key) || result.ContainsKey(pair.Key))
                continue;
            result[pair.Key] = pair.Value;
        }
        return result;
    }

    private static Dictionary<string, Variant> ReadVariantMap(GdDictionary values)
    {
        var result = new Dictionary<string, Variant>(System.StringComparer.Ordinal);
        if (values == null)
            return result;
        foreach (Variant rawKey in values.Keys)
        {
            string keyText = rawKey.VariantType switch
            {
                Variant.Type.String => rawKey.AsString(),
                Variant.Type.StringName => rawKey.AsStringName().ToString(),
                _ => rawKey.ToString(),
            };
            if (string.IsNullOrEmpty(keyText) || result.ContainsKey(keyText))
                continue;
            result[keyText] = values[rawKey];
        }
        return result;
    }

    private static bool TryParseLevelKey(string keyText, out int parsedLevel)
    {
        parsedLevel = -1;
        if (string.IsNullOrEmpty(keyText) || !int.TryParse(keyText, out parsedLevel))
            return false;
        if (parsedLevel < 0)
            return false;
        return parsedLevel.ToString() == keyText;
    }

    private static void SetUniqueStringNames(List<StringName> target, System.Collections.IEnumerable values)
    {
        target.Clear();
        if (values == null)
            return;
        var seen = new HashSet<StringName>();
        foreach (object value in values)
        {
            StringName stringName = ProgressionDataUtils.to_string_name(value);
            if (stringName == "" || !seen.Add(stringName))
                continue;
            target.Add(stringName);
        }
    }
}
