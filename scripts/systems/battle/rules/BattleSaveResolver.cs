using System;
using System.Collections.Generic;
using Godot;

public readonly record struct BattleSaveSource(
    StringName SourceId,
    string Type,
    StringName Tag,
    StringName Mode
)
{
    internal Godot.Collections.Dictionary ToDictionary()
    {
        return new Godot.Collections.Dictionary
        {
            ["source_id"] = SourceId.ToString(),
            ["type"] = Type ?? "",
            ["tag"] = Tag.ToString(),
            ["mode"] = Mode.ToString(),
        };
    }
}

public readonly struct BattleSaveContext
{
    private static readonly int[] EmptyRollOverrides = Array.Empty<int>();
    private readonly int[] _saveRollOverrides;

    public StringName SkillId { get; }

    public IReadOnlyList<int> SaveRollOverrides => _saveRollOverrides ?? EmptyRollOverrides;

    public static BattleSaveContext Empty => default;

    public BattleSaveContext(StringName skillId, IReadOnlyList<int> saveRollOverrides)
    {
        SkillId = skillId ?? new StringName("");
        _saveRollOverrides = CopyRollOverrides(saveRollOverrides);
    }

    public static BattleSaveContext ForSkill(StringName skillId) =>
        new(skillId, EmptyRollOverrides);

    public static BattleSaveContext WithSaveRollOverride(
        int saveRollOverride,
        StringName skillId = default
    ) => new(skillId, new[] { saveRollOverride });

    public static BattleSaveContext WithSaveRollOverrides(
        IReadOnlyList<int> saveRollOverrides,
        StringName skillId = default
    ) => new(skillId, saveRollOverrides);

    private static int[] CopyRollOverrides(IReadOnlyList<int> saveRollOverrides)
    {
        if (saveRollOverrides == null || saveRollOverrides.Count == 0)
        {
            return EmptyRollOverrides;
        }

        int[] result = new int[saveRollOverrides.Count];
        for (int index = 0; index < saveRollOverrides.Count; index++)
        {
            result[index] = Math.Clamp(saveRollOverrides[index], 1, 20);
        }
        return result;
    }
}

public readonly record struct BattleSaveResult(
    bool HasSave,
    bool Immune,
    bool Success,
    int NaturalRoll,
    int RollTotal,
    int Dc,
    StringName Ability,
    StringName SaveTag,
    StringName AdvantageState,
    int AbilityValue,
    int AbilityModifier,
    int Bonus,
    IReadOnlyList<BattleSaveSource> Sources
)
{
    public static BattleSaveResult Empty(StringName advantageState)
    {
        return new BattleSaveResult(
            false,
            false,
            false,
            0,
            0,
            0,
            "",
            "",
            advantageState,
            0,
            0,
            0,
            Array.Empty<BattleSaveSource>()
        );
    }

    internal Godot.Collections.Dictionary ToDictionary()
    {
        return new Godot.Collections.Dictionary
        {
            ["has_save"] = HasSave,
            ["immune"] = Immune,
            ["success"] = Success,
            ["natural_roll"] = NaturalRoll,
            ["roll_total"] = RollTotal,
            ["dc"] = Dc,
            ["ability"] = Ability.ToString(),
            ["save_tag"] = SaveTag.ToString(),
            ["advantage_state"] = AdvantageState.ToString(),
            ["ability_value"] = AbilityValue,
            ["ability_modifier"] = AbilityModifier,
            ["bonus"] = Bonus,
            ["sources"] = SourceArray(Sources),
        };
    }

    private static Godot.Collections.Array SourceArray(IReadOnlyList<BattleSaveSource> sources)
    {
        var result = new Godot.Collections.Array();
        if (sources == null)
        {
            return result;
        }
        foreach (BattleSaveSource source in sources)
        {
            result.Add(source.ToDictionary());
        }
        return result;
    }
}

public readonly record struct BattleSaveProbabilityResult(
    bool HasSave,
    bool Immune,
    int SuccessProbabilityBasisPoints,
    int FailureProbabilityBasisPoints,
    int Dc,
    StringName Ability,
    StringName SaveTag,
    StringName AdvantageState,
    int AbilityValue,
    int AbilityModifier,
    int Bonus,
    IReadOnlyList<BattleSaveSource> Sources
)
{
    public static BattleSaveProbabilityResult Empty(StringName advantageState)
    {
        return new BattleSaveProbabilityResult(
            false,
            false,
            0,
            10000,
            0,
            "",
            "",
            advantageState,
            0,
            0,
            0,
            Array.Empty<BattleSaveSource>()
        );
    }

    internal Godot.Collections.Dictionary ToDictionary()
    {
        return new Godot.Collections.Dictionary
        {
            ["has_save"] = HasSave,
            ["immune"] = Immune,
            ["success_probability_basis_points"] = SuccessProbabilityBasisPoints,
            ["failure_probability_basis_points"] = FailureProbabilityBasisPoints,
            ["dc"] = Dc,
            ["ability"] = Ability.ToString(),
            ["save_tag"] = SaveTag.ToString(),
            ["advantage_state"] = AdvantageState.ToString(),
            ["ability_value"] = AbilityValue,
            ["ability_modifier"] = AbilityModifier,
            ["bonus"] = Bonus,
            ["sources"] = SourceArray(Sources),
        };
    }

    private static Godot.Collections.Array SourceArray(IReadOnlyList<BattleSaveSource> sources)
    {
        var result = new Godot.Collections.Array();
        if (sources == null)
        {
            return result;
        }
        foreach (BattleSaveSource source in sources)
        {
            result.Add(source.ToDictionary());
        }
        return result;
    }
}

public static class BattleSaveResolver
{
    private static readonly StringName AdvantageStateNormal =
        BattleSaveContentRules.ADVANTAGE_STATE_NORMAL;
    private static readonly StringName AdvantageStateAdvantage =
        BattleSaveContentRules.ADVANTAGE_STATE_ADVANTAGE;
    private static readonly StringName AdvantageStateDisadvantage =
        BattleSaveContentRules.ADVANTAGE_STATE_DISADVANTAGE;
    private static readonly StringName SaveModeImmunity = "immunity";

    private static readonly StringName StrengthModifier = "strength_modifier";
    private static readonly StringName AgilityModifier = "agility_modifier";
    private static readonly StringName ConstitutionModifier = "constitution_modifier";
    private static readonly StringName PerceptionModifier = "perception_modifier";
    private static readonly StringName IntelligenceModifier = "intelligence_modifier";
    private static readonly StringName WillpowerModifier = "willpower_modifier";
    private static readonly StringName SpellProficiencyBonus = "spell_proficiency_bonus";

    private const int SpellSaveDcBase = 8;

    private sealed class BattleSaveTagState
    {
        public bool Immune { get; set; }
        public bool Advantage { get; set; }
        public bool Disadvantage { get; set; }
        public List<BattleSaveSource> Sources { get; } = new();
    }

    public static BattleSaveResult ResolveSaveResult(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        CombatEffectDef effect_def,
        BattleSaveContext context = default
    )
    {
        int resolvedDc = ResolveSaveDc(source_unit, effect_def, context);
        if (target_unit == null || effect_def == null || resolvedDc <= 0)
        {
            return BattleSaveResult.Empty(AdvantageStateNormal);
        }

        StringName saveTag = ToStringName(effect_def.save_tag);
        StringName saveAbility = ToStringName(effect_def.save_ability);
        BattleSaveTagState tagState = CollectSaveTagState(target_unit, saveTag);
        if (tagState.Immune)
        {
            return new BattleSaveResult(
                true,
                true,
                true,
                0,
                0,
                resolvedDc,
                saveAbility,
                saveTag,
                AdvantageStateNormal,
                GetTargetAbilityValue(target_unit, saveAbility),
                GetTargetAbilityModifier(target_unit, saveAbility),
                0,
                tagState.Sources.ToArray()
            );
        }

        StringName advantageState = ResolveAdvantageState(tagState);
        int naturalRoll = RollSaveDie(advantageState, context);
        int abilityValue = GetTargetAbilityValue(target_unit, saveAbility);
        int abilityModifier = GetTargetAbilityModifier(target_unit, saveAbility);
        int saveBonus = GetStatusSaveBonus(target_unit, saveTag);
        int rollTotal = naturalRoll + abilityModifier + saveBonus;
        bool success = DoesNaturalSaveRollSucceed(
            naturalRoll,
            resolvedDc,
            abilityModifier,
            saveBonus
        );
        return new BattleSaveResult(
            true,
            false,
            success,
            naturalRoll,
            rollTotal,
            resolvedDc,
            saveAbility,
            saveTag,
            advantageState,
            abilityValue,
            abilityModifier,
            saveBonus,
            tagState.Sources.ToArray()
        );
    }

    public static BattleSaveProbabilityResult EstimateSaveSuccessProbabilityResult(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        CombatEffectDef effect_def,
        BattleSaveContext context = default
    )
    {
        int resolvedDc = ResolveSaveDc(source_unit, effect_def, context);
        if (target_unit == null || effect_def == null || resolvedDc <= 0)
        {
            return BattleSaveProbabilityResult.Empty(AdvantageStateNormal);
        }

        StringName saveTag = ToStringName(effect_def.save_tag);
        StringName saveAbility = ToStringName(effect_def.save_ability);
        int abilityValue = GetTargetAbilityValue(target_unit, saveAbility);
        int abilityModifier = GetTargetAbilityModifier(target_unit, saveAbility);
        BattleSaveTagState tagState = CollectSaveTagState(target_unit, saveTag);
        if (tagState.Immune)
        {
            return new BattleSaveProbabilityResult(
                true,
                true,
                10000,
                0,
                resolvedDc,
                saveAbility,
                saveTag,
                AdvantageStateNormal,
                abilityValue,
                abilityModifier,
                0,
                tagState.Sources.ToArray()
            );
        }

        StringName advantageState = ResolveAdvantageState(tagState);
        int saveBonus = GetStatusSaveBonus(target_unit, saveTag);
        int successBasisPoints = EstimateSuccessProbabilityBasisPoints(
            advantageState,
            resolvedDc,
            abilityModifier,
            saveBonus,
            context
        );
        return new BattleSaveProbabilityResult(
            true,
            false,
            successBasisPoints,
            Math.Max(10000 - successBasisPoints, 0),
            resolvedDc,
            saveAbility,
            saveTag,
            advantageState,
            abilityValue,
            abilityModifier,
            saveBonus,
            tagState.Sources.ToArray()
        );
    }

    public static int ResolveSaveDc(
        BattleUnitState source_unit,
        CombatEffectDef effect_def,
        BattleSaveContext context = default
    )
    {
        if (effect_def == null)
        {
            return 0;
        }
        int lockedSkillHitBonus = GetSkillLockHitBonusFromContext(source_unit, context);
        StringName saveDcMode = ToStringName(effect_def.save_dc_mode);
        if (saveDcMode == BattleSaveContentRules.SAVE_DC_MODE_CASTER_SPELL)
        {
            int casterDc = ResolveCasterSpellSaveDc(source_unit, effect_def);
            return casterDc > 0 ? casterDc + lockedSkillHitBonus : 0;
        }

        int staticDc = Math.Max(effect_def.save_dc, 0);
        return staticDc > 0 ? staticDc + lockedSkillHitBonus : 0;
    }

    public static bool IsImmune(BattleUnitState unit_state, StringName save_tag)
    {
        return CollectSaveTagState(unit_state, save_tag).Immune;
    }

    private static int ResolveCasterSpellSaveDc(
        BattleUnitState sourceUnit,
        CombatEffectDef effectDef
    )
    {
        if (sourceUnit == null || sourceUnit.attribute_snapshot == null || effectDef == null)
        {
            return 0;
        }
        StringName sourceAbility = ToStringName(effectDef.save_dc_source_ability);
        if (IsEmpty(sourceAbility))
        {
            return 0;
        }
        int abilityModifier = GetSourceAbilityModifier(sourceUnit, sourceAbility);
        int proficiencyBonus = GetSourceSpellProficiencyBonus(sourceUnit);
        return Math.Max(SpellSaveDcBase + abilityModifier + proficiencyBonus, 1);
    }

    private static BattleSaveTagState CollectSaveTagState(
        BattleUnitState unitState,
        StringName saveTag
    )
    {
        var state = new BattleSaveTagState();
        if (unitState == null || IsEmpty(saveTag))
        {
            return state;
        }

        ApplySaveTagValues(
            state,
            unitState.save_advantage_tags,
            saveTag,
            "unit",
            "save_advantage_tags",
            ""
        );
        foreach (var statusIdValue in unitState.status_effects.Keys)
        {
            StringName statusId = ToStringName(statusIdValue);
            BattleStatusEffectState statusEntry = unitState.get_status_effect(statusId);
            if (statusEntry == null || statusEntry.@params == null)
            {
                continue;
            }
            ApplySaveTagValues(
                state,
                GetStringNameListParam(statusEntry.@params, "save_advantage_tags"),
                saveTag,
                statusId,
                "save_advantage_tags",
                AdvantageStateAdvantage
            );
            ApplySaveTagValues(
                state,
                GetStringNameListParam(statusEntry.@params, "save_disadvantage_tags"),
                saveTag,
                statusId,
                "save_disadvantage_tags",
                AdvantageStateDisadvantage
            );
            ApplySaveTagValues(
                state,
                GetStringNameListParam(statusEntry.@params, "save_immunity_tags"),
                saveTag,
                statusId,
                "save_immunity_tags",
                SaveModeImmunity
            );
            ApplySaveTagValues(
                state,
                GetStringNameListParam(statusEntry.@params, "save_tags"),
                saveTag,
                statusId,
                "save_tags",
                ""
            );
        }
        return state;
    }

    private static void ApplySaveTagValues(
        BattleSaveTagState state,
        IEnumerable<StringName> values,
        StringName saveTag,
        StringName sourceId,
        string sourceType,
        StringName forcedMode
    )
    {
        if (values == null)
        {
            return;
        }
        foreach (StringName parsedValue in values)
        {
            StringName mode = ResolveSaveTagMode(parsedValue, saveTag, forcedMode);
            if (IsEmpty(mode))
            {
                continue;
            }
            if (mode == AdvantageStateAdvantage)
            {
                state.Advantage = true;
            }
            else if (mode == AdvantageStateDisadvantage)
            {
                state.Disadvantage = true;
            }
            else if (mode == SaveModeImmunity)
            {
                state.Immune = true;
            }

            state.Sources.Add(new BattleSaveSource(sourceId, sourceType, parsedValue, mode));
        }
    }

    private static StringName ResolveSaveTagMode(
        StringName value,
        StringName saveTag,
        StringName forcedMode
    )
    {
        if (IsEmpty(value) || IsEmpty(saveTag))
        {
            return "";
        }
        string saveTagText = saveTag.ToString();
        if (!IsEmpty(forcedMode))
        {
            if (
                value == saveTag
                || value == new StringName($"{saveTagText}_advantage")
                || value == new StringName($"{saveTagText}_disadvantage")
                || value == new StringName($"{saveTagText}_immunity")
            )
            {
                return forcedMode;
            }
            return "";
        }
        if (value == saveTag || value == new StringName($"{saveTagText}_advantage"))
        {
            return AdvantageStateAdvantage;
        }
        if (value == new StringName($"{saveTagText}_disadvantage"))
        {
            return AdvantageStateDisadvantage;
        }
        if (value == new StringName($"{saveTagText}_immunity"))
        {
            return SaveModeImmunity;
        }
        return "";
    }

    private static StringName ResolveAdvantageState(BattleSaveTagState tagState)
    {
        bool hasAdvantage = tagState?.Advantage ?? false;
        bool hasDisadvantage = tagState?.Disadvantage ?? false;
        if (hasAdvantage && !hasDisadvantage)
        {
            return AdvantageStateAdvantage;
        }
        if (hasDisadvantage && !hasAdvantage)
        {
            return AdvantageStateDisadvantage;
        }
        return AdvantageStateNormal;
    }

    private static int RollSaveDie(StringName advantageState, BattleSaveContext context)
    {
        IReadOnlyList<int> rolls = context.SaveRollOverrides;
        if (rolls.Count > 0)
        {
            return SelectSaveRollForAdvantageState(advantageState, rolls);
        }
        int firstRoll = TrueRandomSeedService.randi_range(1, 20);
        if (advantageState == AdvantageStateNormal)
        {
            return firstRoll;
        }
        int secondRoll = TrueRandomSeedService.randi_range(1, 20);
        return advantageState == AdvantageStateAdvantage
            ? Math.Max(firstRoll, secondRoll)
            : Math.Min(firstRoll, secondRoll);
    }

    private static int SelectSaveRollForAdvantageState(
        StringName advantageState,
        IReadOnlyList<int> rolls
    )
    {
        if (rolls.Count <= 0)
        {
            return 1;
        }
        if (advantageState == AdvantageStateAdvantage && rolls.Count >= 2)
        {
            return Math.Max(rolls[0], rolls[1]);
        }
        if (advantageState == AdvantageStateDisadvantage && rolls.Count >= 2)
        {
            return Math.Min(rolls[0], rolls[1]);
        }
        return Math.Clamp(rolls[0], 1, 20);
    }

    private static int EstimateSuccessProbabilityBasisPoints(
        StringName advantageState,
        int dc,
        int abilityModifier,
        int saveBonus,
        BattleSaveContext context
    )
    {
        IReadOnlyList<int> rolls = context.SaveRollOverrides;
        if (rolls.Count > 0)
        {
            int selectedRoll = SelectSaveRollForAdvantageState(advantageState, rolls);
            return DoesNaturalSaveRollSucceed(selectedRoll, dc, abilityModifier, saveBonus)
                ? 10000
                : 0;
        }

        int successCount = 0;
        int totalCount = 0;
        if (advantageState == AdvantageStateAdvantage)
        {
            for (int firstRoll = 1; firstRoll <= 20; firstRoll++)
            {
                for (int secondRoll = 1; secondRoll <= 20; secondRoll++)
                {
                    totalCount++;
                    int naturalRoll = Math.Max(firstRoll, secondRoll);
                    if (DoesNaturalSaveRollSucceed(naturalRoll, dc, abilityModifier, saveBonus))
                    {
                        successCount++;
                    }
                }
            }
        }
        else if (advantageState == AdvantageStateDisadvantage)
        {
            for (int firstRoll = 1; firstRoll <= 20; firstRoll++)
            {
                for (int secondRoll = 1; secondRoll <= 20; secondRoll++)
                {
                    totalCount++;
                    int naturalRoll = Math.Min(firstRoll, secondRoll);
                    if (DoesNaturalSaveRollSucceed(naturalRoll, dc, abilityModifier, saveBonus))
                    {
                        successCount++;
                    }
                }
            }
        }
        else
        {
            for (int naturalRoll = 1; naturalRoll <= 20; naturalRoll++)
            {
                totalCount++;
                if (DoesNaturalSaveRollSucceed(naturalRoll, dc, abilityModifier, saveBonus))
                {
                    successCount++;
                }
            }
        }
        if (totalCount <= 0)
        {
            return 0;
        }
        return Math.Clamp(Mathf.RoundToInt((float)successCount * 10000.0f / totalCount), 0, 10000);
    }

    private static bool DoesNaturalSaveRollSucceed(
        int naturalRoll,
        int dc,
        int abilityModifier,
        int saveBonus
    )
    {
        if (naturalRoll <= 1)
        {
            return false;
        }
        if (naturalRoll >= 20)
        {
            return true;
        }
        return naturalRoll + abilityModifier + saveBonus >= dc;
    }

    private static int GetTargetAbilityValue(BattleUnitState targetUnit, StringName saveAbility)
    {
        if (targetUnit == null || targetUnit.attribute_snapshot == null || IsEmpty(saveAbility))
        {
            return 0;
        }
        return GetAttributeValue(targetUnit.attribute_snapshot, saveAbility);
    }

    private static int GetTargetAbilityModifier(BattleUnitState targetUnit, StringName saveAbility)
    {
        if (targetUnit == null || targetUnit.attribute_snapshot == null || IsEmpty(saveAbility))
        {
            return 0;
        }
        StringName modifierId = GetBaseAttributeModifierId(saveAbility);
        return IsEmpty(modifierId)
            ? 0
            : GetAttributeValue(targetUnit.attribute_snapshot, modifierId);
    }

    private static int GetSourceAbilityModifier(
        BattleUnitState sourceUnit,
        StringName sourceAbility
    )
    {
        if (sourceUnit == null || sourceUnit.attribute_snapshot == null || IsEmpty(sourceAbility))
        {
            return 0;
        }
        StringName modifierId = GetBaseAttributeModifierId(sourceAbility);
        return IsEmpty(modifierId)
            ? 0
            : GetAttributeValue(sourceUnit.attribute_snapshot, modifierId);
    }

    private static int GetSourceSpellProficiencyBonus(BattleUnitState sourceUnit)
    {
        if (sourceUnit == null || sourceUnit.attribute_snapshot == null)
        {
            return 0;
        }
        return Math.Max(GetAttributeValue(sourceUnit.attribute_snapshot, SpellProficiencyBonus), 0);
    }

    private static int GetSkillLockHitBonusFromContext(
        BattleUnitState sourceUnit,
        BattleSaveContext context
    )
    {
        if (sourceUnit == null)
        {
            return 0;
        }
        StringName skillId = context.SkillId;
        if (IsEmpty(skillId))
        {
            return 0;
        }
        return Math.Max(GetInt(sourceUnit.known_skill_lock_hit_bonus_map, skillId), 0);
    }

    private static int GetStatusSaveBonus(BattleUnitState targetUnit, StringName saveTag)
    {
        if (targetUnit == null)
        {
            return 0;
        }
        int bonus = 0;
        foreach (var statusIdValue in targetUnit.status_effects.Keys)
        {
            StringName statusId = ToStringName(statusIdValue);
            BattleStatusEffectState statusEntry = targetUnit.get_status_effect(statusId);
            if (statusEntry == null || statusEntry.@params == null)
            {
                continue;
            }
            bonus = Math.Max(bonus, GetInt(statusEntry.@params, "save_bonus"));
            if (IsControlSaveTag(saveTag))
            {
                bonus = Math.Max(bonus, GetInt(statusEntry.@params, "control_save_bonus"));
            }
        }
        return bonus;
    }

    private static IReadOnlyList<StringName> GetStringNameListParam(
        Godot.Collections.Dictionary @params,
        string key
    )
    {
        if (@params == null)
        {
            return Array.Empty<StringName>();
        }
        var result = new List<StringName>();
        foreach (var rawValue in GetArray(@params, key))
        {
            StringName value = ToStringName(rawValue);
            if (!IsEmpty(value))
            {
                result.Add(value);
            }
        }
        return result.Count == 0 ? Array.Empty<StringName>() : result.ToArray();
    }

    private static Godot.Collections.Array GetArray(Godot.Collections.Dictionary source, string key)
    {
        return source != null && !string.IsNullOrEmpty(key) && source.ContainsKey(key)
            ? source[key].AsGodotArray()
            : new Godot.Collections.Array();
    }

    private static int GetInt(Godot.Collections.Dictionary source, string key, int fallback = 0)
    {
        if (source == null || string.IsNullOrEmpty(key) || !source.ContainsKey(key))
        {
            return fallback;
        }
        try
        {
            return source[key].AsInt32();
        }
        catch
        {
            return int.TryParse(source[key].ToString(), out int parsed) ? parsed : fallback;
        }
    }

    private static int GetInt(
        Godot.Collections.Dictionary source,
        StringName key,
        int fallback = 0
    )
    {
        if (source == null || IsEmpty(key) || !source.ContainsKey(key))
        {
            return fallback;
        }
        try
        {
            return source[key].AsInt32();
        }
        catch
        {
            return int.TryParse(source[key].ToString(), out int parsed) ? parsed : fallback;
        }
    }

    private static int GetAttributeValue(AttributeSnapshot attributeSnapshot, StringName attributeId)
    {
        if (attributeSnapshot == null || IsEmpty(attributeId))
        {
            return 0;
        }
        return attributeSnapshot.get_value(attributeId);
    }

    private static StringName GetBaseAttributeModifierId(StringName attributeId)
    {
        if (attributeId == BattleSaveContentRules.SAVE_TAG_STRENGTH)
            return StrengthModifier;
        if (attributeId == BattleSaveContentRules.SAVE_TAG_AGILITY)
            return AgilityModifier;
        if (attributeId == BattleSaveContentRules.SAVE_TAG_CONSTITUTION)
            return ConstitutionModifier;
        if (attributeId == BattleSaveContentRules.SAVE_TAG_PERCEPTION)
            return PerceptionModifier;
        if (attributeId == BattleSaveContentRules.SAVE_TAG_INTELLIGENCE)
            return IntelligenceModifier;
        if (attributeId == BattleSaveContentRules.SAVE_TAG_WILLPOWER)
            return WillpowerModifier;
        return "";
    }

    private static bool IsControlSaveTag(StringName saveTag)
    {
        return BattleSaveContentRules.is_control_save_tag(saveTag);
    }

    private static StringName ToStringName(object rawValue)
    {
        return ProgressionDataUtils.to_string_name(rawValue);
    }

    private static StringName ToStringName(StringName value)
    {
        return value ?? new StringName("");
    }

    private static bool IsEmpty(StringName value)
    {
        return value == null || string.IsNullOrEmpty(value.ToString());
    }
}
