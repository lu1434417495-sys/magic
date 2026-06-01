using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public readonly record struct BattleSaveSource(
    StringName SourceId,
    string Type,
    StringName Tag,
    StringName Mode
)
{
    public GDictionary ToDictionary()
    {
        return new GDictionary
        {
            ["source_id"] = SourceId.ToString(),
            ["type"] = Type ?? "",
            ["tag"] = Tag.ToString(),
            ["mode"] = Mode.ToString(),
        };
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

    public GDictionary ToDictionary()
    {
        return new GDictionary
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

    private static GArray SourceArray(IReadOnlyList<BattleSaveSource> sources)
    {
        var result = new GArray();
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

    public GDictionary ToDictionary()
    {
        return new GDictionary
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

    private static GArray SourceArray(IReadOnlyList<BattleSaveSource> sources)
    {
        var result = new GArray();
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

// 翻译自 battle_save_resolver.gd（2026-05-24，Battle 非 AI 规则 C# 迁移）。
// GDScript 不能稳定读取 C# static field；外部常量统一暴露为 static 方法。
[GlobalClass]
public partial class BattleSaveResolver : RefCounted
{
    private static readonly StringName SaveTagSleep = "sleep";
    private static readonly StringName SaveTagParalysis = "paralysis";
    private static readonly StringName SaveTagCharm = "charm";
    private static readonly StringName SaveTagPoison = "poison";
    private static readonly StringName SaveTagDragonBreath = "dragon_breath";
    private static readonly StringName SaveTagFireball = "fireball";
    private static readonly StringName SaveTagChainLightning = "chain_lightning";
    private static readonly StringName SaveTagEquipmentDisjunction = "equipment_disjunction";
    private static readonly StringName SaveTagMagic = "magic";
    private static readonly StringName SaveTagIllusion = "illusion";
    private static readonly StringName SaveTagFrightened = "frightened";
    private static readonly StringName SaveTagExecute = "execute";
    private static readonly StringName SaveTagStrength = "strength";
    private static readonly StringName SaveTagAgility = "agility";
    private static readonly StringName SaveTagConstitution = "constitution";
    private static readonly StringName SaveTagPerception = "perception";
    private static readonly StringName SaveTagIntelligence = "intelligence";
    private static readonly StringName SaveTagWillpower = "willpower";

    private static readonly StringName AdvantageStateNormal = "normal";
    private static readonly StringName AdvantageStateAdvantage = "advantage";
    private static readonly StringName AdvantageStateDisadvantage = "disadvantage";
    private static readonly StringName SaveDcModeStatic = "static";
    private static readonly StringName SaveDcModeCasterSpell = "caster_spell";
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

    public static StringName SAVE_TAG_SLEEP() => SaveTagSleep;

    public static StringName SAVE_TAG_PARALYSIS() => SaveTagParalysis;

    public static StringName SAVE_TAG_CHARM() => SaveTagCharm;

    public static StringName SAVE_TAG_POISON() => SaveTagPoison;

    public static StringName SAVE_TAG_DRAGON_BREATH() => SaveTagDragonBreath;

    public static StringName SAVE_TAG_FIREBALL() => SaveTagFireball;

    public static StringName SAVE_TAG_CHAIN_LIGHTNING() => SaveTagChainLightning;

    public static StringName SAVE_TAG_EQUIPMENT_DISJUNCTION() => SaveTagEquipmentDisjunction;

    public static StringName SAVE_TAG_MAGIC() => SaveTagMagic;

    public static StringName SAVE_TAG_ILLUSION() => SaveTagIllusion;

    public static StringName SAVE_TAG_FRIGHTENED() => SaveTagFrightened;

    public static StringName SAVE_TAG_EXECUTE() => SaveTagExecute;

    public static StringName SAVE_TAG_STRENGTH() => SaveTagStrength;

    public static StringName SAVE_TAG_AGILITY() => SaveTagAgility;

    public static StringName SAVE_TAG_CONSTITUTION() => SaveTagConstitution;

    public static StringName SAVE_TAG_PERCEPTION() => SaveTagPerception;

    public static StringName SAVE_TAG_INTELLIGENCE() => SaveTagIntelligence;

    public static StringName SAVE_TAG_WILLPOWER() => SaveTagWillpower;

    public static StringName ADVANTAGE_STATE_NORMAL() => AdvantageStateNormal;

    public static StringName ADVANTAGE_STATE_ADVANTAGE() => AdvantageStateAdvantage;

    public static StringName ADVANTAGE_STATE_DISADVANTAGE() => AdvantageStateDisadvantage;

    public static StringName SAVE_DC_MODE_STATIC() => SaveDcModeStatic;

    public static StringName SAVE_DC_MODE_CASTER_SPELL() => SaveDcModeCasterSpell;

    public static GDictionary resolve_save(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        CombatEffectDef effect_def,
        GDictionary context = null
    )
    {
        return resolve_save_result(source_unit, target_unit, effect_def, context).ToDictionary();
    }

    public static BattleSaveResult resolve_save_result(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        CombatEffectDef effect_def,
        GDictionary context = null
    )
    {
        GDictionary normalizedContext = context ?? new GDictionary();
        int resolvedDc = _resolve_save_dc(source_unit, effect_def, normalizedContext);
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
        int naturalRoll = RollSaveDie(advantageState, normalizedContext);
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

    public static GDictionary estimate_save_success_probability(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        CombatEffectDef effect_def,
        GDictionary context = null
    )
    {
        return estimate_save_success_probability_result(
                source_unit,
                target_unit,
                effect_def,
                context
            )
            .ToDictionary();
    }

    public static BattleSaveProbabilityResult estimate_save_success_probability_result(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        CombatEffectDef effect_def,
        GDictionary context = null
    )
    {
        GDictionary normalizedContext = context ?? new GDictionary();
        int resolvedDc = _resolve_save_dc(source_unit, effect_def, normalizedContext);
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
            normalizedContext
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

    public static int resolve_save_dc(BattleUnitState source_unit, CombatEffectDef effect_def)
    {
        return _resolve_save_dc(source_unit, effect_def, new GDictionary());
    }

    public static int _resolve_save_dc(
        BattleUnitState source_unit,
        CombatEffectDef effect_def,
        GDictionary context = null
    )
    {
        if (effect_def == null)
        {
            return 0;
        }
        GDictionary normalizedContext = context ?? new GDictionary();
        int lockedSkillHitBonus = GetSkillLockHitBonusFromContext(source_unit, normalizedContext);
        StringName saveDcMode = ToStringName(effect_def.save_dc_mode);
        if (saveDcMode == SaveDcModeCasterSpell)
        {
            int casterDc = ResolveCasterSpellSaveDc(source_unit, effect_def, normalizedContext);
            return casterDc > 0 ? casterDc + lockedSkillHitBonus : 0;
        }

        int staticDc = Math.Max(effect_def.save_dc, 0);
        return staticDc > 0 ? staticDc + lockedSkillHitBonus : 0;
    }

    public static bool is_immune(BattleUnitState unit_state, StringName save_tag)
    {
        return CollectSaveTagState(unit_state, save_tag).Immune;
    }

    private static int ResolveCasterSpellSaveDc(
        BattleUnitState sourceUnit,
        CombatEffectDef effectDef,
        GDictionary context
    )
    {
        if (sourceUnit == null || sourceUnit.attribute_snapshot == null || effectDef == null)
        {
            return 0;
        }
        StringName sourceAbility = ToStringName(effectDef.save_dc_source_ability);
        if (IsEmpty(sourceAbility) && context != null)
        {
            sourceAbility = GetStringName(context, "save_dc_source_ability");
        }
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
            ToUntypedArray(unitState.save_advantage_tags),
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
                GetArrayParam(statusEntry.@params, "save_advantage_tags"),
                saveTag,
                statusId,
                "save_advantage_tags",
                AdvantageStateAdvantage
            );
            ApplySaveTagValues(
                state,
                GetArrayParam(statusEntry.@params, "save_disadvantage_tags"),
                saveTag,
                statusId,
                "save_disadvantage_tags",
                AdvantageStateDisadvantage
            );
            ApplySaveTagValues(
                state,
                GetArrayParam(statusEntry.@params, "save_immunity_tags"),
                saveTag,
                statusId,
                "save_immunity_tags",
                SaveModeImmunity
            );
            ApplySaveTagValues(
                state,
                GetArrayParam(statusEntry.@params, "save_tags"),
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
        GArray values,
        StringName saveTag,
        StringName sourceId,
        string sourceType,
        StringName forcedMode
    )
    {
        foreach (var rawValue in values)
        {
            StringName parsedValue = ToStringName(rawValue);
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

    private static int RollSaveDie(StringName advantageState, GDictionary context)
    {
        GArray rolls = GetSaveRollOverrides(context);
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

    private static int SelectSaveRollForAdvantageState(StringName advantageState, GArray rolls)
    {
        if (rolls.Count <= 0)
        {
            return 1;
        }
        if (advantageState == AdvantageStateAdvantage && rolls.Count >= 2)
        {
            return Math.Max(rolls[0].AsInt32(), rolls[1].AsInt32());
        }
        if (advantageState == AdvantageStateDisadvantage && rolls.Count >= 2)
        {
            return Math.Min(rolls[0].AsInt32(), rolls[1].AsInt32());
        }
        return Math.Clamp(rolls[0].AsInt32(), 1, 20);
    }

    private static int EstimateSuccessProbabilityBasisPoints(
        StringName advantageState,
        int dc,
        int abilityModifier,
        int saveBonus,
        GDictionary context
    )
    {
        GArray rolls = GetSaveRollOverrides(context);
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

    private static GArray GetSaveRollOverrides(GDictionary context)
    {
        var result = new GArray();
        if (context == null)
        {
            return result;
        }
        if (TryGetValue(context, "save_roll_override", out dynamic overrideValue))
        {
            result.Add(Math.Clamp(overrideValue.AsInt32(), 1, 20));
            return result;
        }
        if (
            TryGetValue(context, "save_roll_overrides", out dynamic rollsValue)
        )
        {
            foreach (var rawRoll in rollsValue.AsGodotArray())
            {
                result.Add(Math.Clamp(rawRoll.AsInt32(), 1, 20));
            }
        }
        return result;
    }

    public static GDictionary resolve_save_with_context(
        BattleUnitState source_unit,
        BattleUnitState target_unit,
        CombatEffectDef effect_def,
        GDictionary context
    )
    {
        return resolve_save(source_unit, target_unit, effect_def, context);
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
        GDictionary context
    )
    {
        if (sourceUnit == null || context == null)
        {
            return 0;
        }
        StringName skillId = GetStringName(context, "skill_id");
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

    private static GArray GetArrayParam(GDictionary @params, string key)
    {
        if (@params == null)
        {
            return new GArray();
        }
        return GetArray(@params, key);
    }

    private static GArray GetArray(GDictionary source, object key)
    {
        return TryGetValue(source, key, out dynamic value) ? value.AsGodotArray() : new GArray();
    }

    private static int GetInt(GDictionary source, object key, int fallback = 0)
    {
        if (!TryGetValue(source, key, out dynamic value))
        {
            return fallback;
        }
        try
        {
            return value.AsInt32();
        }
        catch
        {
            return int.TryParse(value.ToString(), out int parsed) ? parsed : fallback;
        }
    }

    private static StringName GetStringName(GDictionary source, object key)
    {
        if (!TryGetValue(source, key, out dynamic value))
        {
            return "";
        }
        return ProgressionDataUtils.to_string_name(value);
    }

    private static bool TryGetValue(GDictionary source, object key, out dynamic value)
    {
        if (source == null)
        {
            value = default;
            return false;
        }
        try
        {
            dynamic dynamicKey = key;
            if (source.ContainsKey(dynamicKey))
            {
                value = source[dynamicKey];
                return true;
            }
        }
        catch
        {
        }
        if (key is StringName stringNameKey)
        {
            string keyText = stringNameKey.ToString();
            if (source.ContainsKey(keyText))
            {
                value = source[keyText];
                return true;
            }
        }
        else if (key is string stringKey)
        {
            var stringName = new StringName(stringKey);
            if (source.ContainsKey(stringName))
            {
                value = source[stringName];
                return true;
            }
        }
        value = default;
        return false;
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
        if (attributeId == SaveTagStrength)
            return StrengthModifier;
        if (attributeId == SaveTagAgility)
            return AgilityModifier;
        if (attributeId == SaveTagConstitution)
            return ConstitutionModifier;
        if (attributeId == SaveTagPerception)
            return PerceptionModifier;
        if (attributeId == SaveTagIntelligence)
            return IntelligenceModifier;
        if (attributeId == SaveTagWillpower)
            return WillpowerModifier;
        return "";
    }

    private static bool IsControlSaveTag(StringName saveTag)
    {
        return saveTag == SaveTagSleep
            || saveTag == SaveTagParalysis
            || saveTag == SaveTagCharm
            || saveTag == SaveTagIllusion
            || saveTag == SaveTagFrightened;
    }

    private static GArray ToUntypedArray(GArray values)
    {
        var result = new GArray();
        if (values == null)
        {
            return result;
        }
        foreach (var value in values)
        {
            result.Add(value);
        }
        return result;
    }

    private static GArray ToUntypedArray(GStringNameArray values)
    {
        var result = new GArray();
        if (values == null)
        {
            return result;
        }
        foreach (StringName value in values)
        {
            result.Add(value);
        }
        return result;
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
