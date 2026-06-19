using System.Collections.Generic;
using Godot;

[GlobalClass]
public partial class PartyMemberState : RefCounted
{
    private static readonly Godot.Collections.Array<string> TO_DICT_FIELDS = new()
    {
        "member_id",
        "display_name",
        "faction_id",
        "portrait_id",
        "progression",
        "equipment_state",
        "control_mode",
        "current_hp",
        "current_mp",
        "current_aura",
        "is_dead",
        "race_id",
        "subrace_id",
        "age_years",
        "birth_at_world_step",
        "age_profile_id",
        "natural_age_stage_id",
        "effective_age_stage_id",
        "effective_age_stage_source_type",
        "effective_age_stage_source_id",
        "body_size",
        "body_size_category",
        "versatility_pick",
        "active_stage_advancement_modifier_ids",
        "bloodline_id",
        "bloodline_stage_id",
        "ascension_id",
        "ascension_stage_id",
        "ascension_started_at_world_step",
        "original_race_id_before_ascension",
        "biological_age_years",
        "astral_memory_years",
        "trait_instances",
    };

    public StringName member_id = "";
    public string display_name = "";
    public StringName faction_id = "player";
    public StringName portrait_id = "";
    public UnitProgress progression;
    public EquipmentState equipment_state = new EquipmentState();
    public Godot.Collections.Array<TraitInstanceState> trait_instances = new();
    public StringName control_mode = "manual";
    internal BattleUnitControlMode ControlModeKind
    {
        get => BattleTypedNames.ToControlMode(control_mode);
        set => control_mode = BattleTypedNames.ToStringName(value);
    }
    public int current_hp { get; internal set; } = 1;
    public int current_mp { get; internal set; }
    public int current_aura { get; internal set; }
    public bool is_dead { get; internal set; }
    public StringName race_id { get; internal set; } = "human";
    public StringName subrace_id { get; internal set; } = "common_human";
    public int age_years { get; internal set; } = 24;
    public int birth_at_world_step { get; internal set; }
    public StringName age_profile_id { get; internal set; } = "human_age_profile";
    public StringName natural_age_stage_id { get; internal set; } = "adult";
    public StringName effective_age_stage_id { get; internal set; } = "adult";
    public StringName effective_age_stage_source_type { get; internal set; } = "";
    public StringName effective_age_stage_source_id { get; internal set; } = "";
    public int body_size { get; internal set; } = 2;
    public StringName body_size_category { get; internal set; } = "medium";
    public StringName versatility_pick { get; internal set; } = "";
    public Godot.Collections.Array<StringName> active_stage_advancement_modifier_ids { get; internal set; } = new();
    public StringName bloodline_id { get; internal set; } = "";
    public StringName bloodline_stage_id { get; internal set; } = "";
    public StringName ascension_id { get; internal set; } = "";
    public StringName ascension_stage_id { get; internal set; } = "";
    public int ascension_started_at_world_step { get; internal set; } = -1;
    public StringName original_race_id_before_ascension { get; internal set; } = "";
    public int biological_age_years { get; internal set; } = 24;
    public int astral_memory_years { get; internal set; }

    public PartyMemberState()
    {
        progression = new UnitProgress();
    }

    public PartyMemberState DuplicateState()
    {
        return new PartyMemberState
        {
            member_id = member_id,
            display_name = display_name,
            faction_id = faction_id,
            portrait_id = portrait_id,
            progression = progression?.DuplicateState() ?? new UnitProgress(),
            equipment_state = equipment_state?.DuplicateState() ?? new EquipmentState(),
            control_mode = control_mode,
            current_hp = current_hp,
            current_mp = current_mp,
            current_aura = current_aura,
            is_dead = is_dead,
            race_id = race_id,
            subrace_id = subrace_id,
            age_years = age_years,
            birth_at_world_step = birth_at_world_step,
            age_profile_id = age_profile_id,
            natural_age_stage_id = natural_age_stage_id,
            effective_age_stage_id = effective_age_stage_id,
            effective_age_stage_source_type = effective_age_stage_source_type,
            effective_age_stage_source_id = effective_age_stage_source_id,
            body_size = body_size,
            body_size_category = body_size_category,
            versatility_pick = versatility_pick,
            active_stage_advancement_modifier_ids =
                new Godot.Collections.Array<StringName>(active_stage_advancement_modifier_ids),
            bloodline_id = bloodline_id,
            bloodline_stage_id = bloodline_stage_id,
            ascension_id = ascension_id,
            ascension_stage_id = ascension_stage_id,
            ascension_started_at_world_step = ascension_started_at_world_step,
            original_race_id_before_ascension = original_race_id_before_ascension,
            biological_age_years = biological_age_years,
            astral_memory_years = astral_memory_years,
            trait_instances = TraitInstanceCollection.Duplicate(trait_instances),
        };
    }

    public int GetHiddenLuckAtBirth()
    {
        var a = _get_unit_base_attributes();
        return a?.GetHiddenLuckAtBirth() ?? 0;
    }

    public int GetFaithLuckBonus()
    {
        var a = _get_unit_base_attributes();
        return a?.GetFaithLuckBonus() ?? 0;
    }

    public int GetEffectiveLuck()
    {
        var a = _get_unit_base_attributes();
        return a?.GetEffectiveLuck() ?? 0;
    }

    public int GetCombatLuckScore()
    {
        var a = _get_unit_base_attributes();
        return a?.GetCombatLuckScore() ?? 0;
    }

    public int GetDropLuck()
    {
        var a = _get_unit_base_attributes();
        return a?.GetDropLuck() ?? 0;
    }

    public int GetCurrentHp() => current_hp;

    public int GetCurrentMp() => current_mp;

    public int GetCurrentAura() => current_aura;

    public bool IsDead() => is_dead;

    public void SetCurrentHp(int hp, bool syncDeathState = true)
    {
        current_hp = Mathf.Max(hp, 0);
        if (syncDeathState)
            is_dead = current_hp <= 0;
    }

    public void SetCurrentMp(int mp)
    {
        current_mp = Mathf.Max(mp, 0);
    }

    public void SetCurrentAura(int aura)
    {
        current_aura = Mathf.Max(aura, 0);
    }

    public void SetVitals(int hp, int mp, int aura)
    {
        SetVitals(hp, mp, aura, Mathf.Max(hp, 0) <= 0);
    }

    public void SetVitals(int hp, int mp, int aura, bool dead)
    {
        current_hp = Mathf.Max(hp, 0);
        current_mp = Mathf.Max(mp, 0);
        current_aura = Mathf.Max(aura, 0);
        is_dead = dead;
    }

    public void ClampVitals(int hpMax, int mpMax, int auraMax)
    {
        current_hp = ClampResource(current_hp, hpMax);
        current_mp = ClampResource(current_mp, mpMax);
        current_aura = ClampResource(current_aura, auraMax);
        is_dead = current_hp <= 0;
    }

    public void RestoreVitals(int hpMax, int mpMax, int auraMax)
    {
        SetVitals(Mathf.Max(hpMax, 1), Mathf.Max(mpMax, 0), Mathf.Max(auraMax, 0));
    }

    public void MarkDead()
    {
        current_hp = 0;
        current_mp = 0;
        current_aura = 0;
        is_dead = true;
    }

    public void ReviveWithVitals(int hp, int mp, int aura)
    {
        SetVitals(Mathf.Max(hp, 1), mp, aura, false);
    }

    public void SetIdentity(StringName raceId, StringName subraceId)
    {
        if (raceId != "")
            race_id = raceId;
        if (subraceId != "")
            subrace_id = subraceId;
    }

    public void SetAgeProjection(
        int ageYears,
        int biologicalAgeYears,
        int astralMemoryYears,
        int birthAtWorldStep
    )
    {
        age_years = Mathf.Max(ageYears, 0);
        biological_age_years = Mathf.Max(biologicalAgeYears, 0);
        astral_memory_years = Mathf.Max(astralMemoryYears, 0);
        birth_at_world_step = Mathf.Max(birthAtWorldStep, 0);
    }

    public void SetAgeStageProjection(
        StringName ageProfileId,
        StringName naturalAgeStageId,
        StringName effectiveAgeStageId,
        StringName effectiveAgeStageSourceType,
        StringName effectiveAgeStageSourceId
    )
    {
        if (ageProfileId != "")
            age_profile_id = ageProfileId;
        if (naturalAgeStageId != "")
            natural_age_stage_id = naturalAgeStageId;
        SetEffectiveAgeStage(
            effectiveAgeStageId,
            effectiveAgeStageSourceType,
            effectiveAgeStageSourceId
        );
    }

    public void SetEffectiveAgeStage(
        StringName stageId,
        StringName sourceType,
        StringName sourceId
    )
    {
        if (stageId != "")
            effective_age_stage_id = stageId;
        effective_age_stage_source_type = sourceType;
        effective_age_stage_source_id = sourceId;
    }

    public void SetVersatilityPick(StringName versatilityPick)
    {
        versatility_pick = versatilityPick;
    }

    public void SetActiveStageAdvancementModifierIds(IEnumerable<StringName> modifierIds)
    {
        active_stage_advancement_modifier_ids = new Godot.Collections.Array<StringName>();
        if (modifierIds == null)
            return;

        HashSet<StringName> seen = new();
        foreach (StringName modifierId in modifierIds)
        {
            if (modifierId == "" || !seen.Add(modifierId))
                continue;
            active_stage_advancement_modifier_ids.Add(modifierId);
        }
    }

    public bool SetBodySizeCategory(StringName category)
    {
        if (!BodySizeContentRules.IsValidBodySizeCategory(category))
            return false;
        body_size_category = category;
        body_size = BodySizeContentRules.GetBodySizeForCategory(category);
        return true;
    }

    public bool SetBloodline(StringName bloodlineId, StringName stageId)
    {
        if ((bloodlineId == "") != (stageId == ""))
            return false;
        bloodline_id = bloodlineId;
        bloodline_stage_id = bloodlineId == "" ? "" : stageId;
        return true;
    }

    public void ClearBloodline()
    {
        bloodline_id = "";
        bloodline_stage_id = "";
    }

    public bool SetAscension(
        StringName ascensionId,
        StringName stageId,
        int startedAtWorldStep,
        StringName originalRaceIdBeforeAscension
    )
    {
        if ((ascensionId == "") != (stageId == ""))
            return false;
        ascension_id = ascensionId;
        ascension_stage_id = ascensionId == "" ? "" : stageId;
        ascension_started_at_world_step = ascensionId == ""
            ? -1
            : Mathf.Max(startedAtWorldStep, 0);
        original_race_id_before_ascension = ascensionId == ""
            ? ""
            : originalRaceIdBeforeAscension;
        return true;
    }

    public void ClearAscension()
    {
        ascension_id = "";
        ascension_stage_id = "";
        ascension_started_at_world_step = -1;
        original_race_id_before_ascension = "";
    }

    public Godot.Collections.Dictionary ToDictionary()
    {
        return new Godot.Collections.Dictionary
        {
            { "member_id", (string)member_id },
            { "display_name", display_name },
            { "faction_id", (string)faction_id },
            { "portrait_id", (string)portrait_id },
            { "progression", progression?.ToDictionary() ?? new Godot.Collections.Dictionary() },
            { "equipment_state", equipment_state?.ToDictionary() ?? new Godot.Collections.Dictionary() },
            { "control_mode", (string)control_mode },
            { "current_hp", current_hp },
            { "current_mp", current_mp },
            { "current_aura", current_aura },
            { "is_dead", is_dead },
            { "race_id", (string)race_id },
            { "subrace_id", (string)subrace_id },
            { "age_years", age_years },
            { "birth_at_world_step", birth_at_world_step },
            { "age_profile_id", (string)age_profile_id },
            { "natural_age_stage_id", (string)natural_age_stage_id },
            { "effective_age_stage_id", (string)effective_age_stage_id },
            { "effective_age_stage_source_type", (string)effective_age_stage_source_type },
            { "effective_age_stage_source_id", (string)effective_age_stage_source_id },
            { "body_size", body_size },
            { "body_size_category", (string)body_size_category },
            { "versatility_pick", (string)versatility_pick },
            {
                "active_stage_advancement_modifier_ids",
                ProgressionDataUtils.string_name_array_to_string_array(
                    active_stage_advancement_modifier_ids
                )
            },
            { "bloodline_id", (string)bloodline_id },
            { "bloodline_stage_id", (string)bloodline_stage_id },
            { "ascension_id", (string)ascension_id },
            { "ascension_stage_id", (string)ascension_stage_id },
            { "ascension_started_at_world_step", ascension_started_at_world_step },
            { "original_race_id_before_ascension", (string)original_race_id_before_ascension },
            { "biological_age_years", biological_age_years },
            { "astral_memory_years", astral_memory_years },
            { "trait_instances", TraitInstanceCollection.ToPayloadArray(trait_instances) },
        };
    }

    public static PartyMemberState FromDictionary(Godot.Collections.Dictionary data)
    {
        if (data.Count == 0)
            return null;
        if (!_has_exact_fields(data, TO_DICT_FIELDS))
            return null;
        if (!TryGetDictionary(data, "progression", out Godot.Collections.Dictionary progData))
            return null;
        if (!TryGetDictionary(data, "equipment_state", out Godot.Collections.Dictionary esData))
            return null;
        var memberId = _parse_string_name_field(data["member_id"], false, out bool o1);
        if (!o1)
            return null;
        if (!TryGetStrictString(data, "display_name", out string displayName))
            return null;
        if (displayName.StripEdges().Length == 0)
            return null;
        var factionId = _parse_string_name_field(data["faction_id"], false, out bool o2);
        if (!o2)
            return null;
        var portraitId = _parse_string_name_field(data["portrait_id"], true, out bool o3);
        if (!o3)
            return null;
        var ctrl = _parse_string_name_field(data["control_mode"], false, out bool o4);
        if (!o4 || BattleTypedNames.ToControlMode(ctrl) == BattleUnitControlMode.Unknown)
            return null;
        if (!TryGetStrictInt(data, "current_hp", out int currentHp) || currentHp < 0)
            return null;
        if (!TryGetStrictInt(data, "current_mp", out int currentMp) || currentMp < 0)
            return null;
        if (!TryGetStrictInt(data, "current_aura", out int currentAura) || currentAura < 0)
            return null;
        if (!TryReadBoolField(data, "is_dead", out bool isDeadValue))
            return null;
        var raceId = _parse_string_name_field(data["race_id"], false, out bool o5);
        if (!o5)
            return null;
        var subraceId = _parse_string_name_field(data["subrace_id"], false, out bool o6);
        if (!o6)
            return null;
        if (!TryGetStrictInt(data, "age_years", out int ageYears) || ageYears < 0)
            return null;
        if (!TryGetStrictInt(data, "birth_at_world_step", out int birthAtWorldStep)
            || birthAtWorldStep < 0)
            return null;
        var ageProfId = _parse_string_name_field(data["age_profile_id"], false, out bool o7);
        if (!o7)
            return null;
        var natAgeStage = _parse_string_name_field(
            data["natural_age_stage_id"],
            false,
            out bool o8
        );
        if (!o8)
            return null;
        var effAgeStage = _parse_string_name_field(
            data["effective_age_stage_id"],
            false,
            out bool o9
        );
        if (!o9)
            return null;
        var effAgeSrcType = _parse_string_name_field(
            data["effective_age_stage_source_type"],
            true,
            out bool o10
        );
        if (!o10)
            return null;
        var effAgeSrcId = _parse_string_name_field(
            data["effective_age_stage_source_id"],
            true,
            out bool o11
        );
        if (!o11)
            return null;
        if (!TryGetStrictInt(data, "body_size", out int bsVal))
            return null;
        if (bsVal < 1)
            return null;
        var bsCat = _parse_string_name_field(data["body_size_category"], false, out bool o12);
        if (!o12)
            return null;
        if (
            !BodySizeContentRules.IsValidBodySizeCategory(bsCat)
            || !BodySizeContentRules.BodySizeMatchesCategory(bsCat, bsVal)
        )
            return null;
        var versPick = _parse_string_name_field(data["versatility_pick"], true, out bool o13);
        if (!o13)
            return null;
        if (!TryGetArray(
                data,
                "active_stage_advancement_modifier_ids",
                out Godot.Collections.Array activeStageModifierIdValues
            ))
            return null;
        var asami = _parse_unique_string_name_array(activeStageModifierIdValues);
        if (asami == null)
            return null;
        var blId = _parse_string_name_field(data["bloodline_id"], true, out bool o14);
        if (!o14)
            return null;
        var blStId = _parse_string_name_field(data["bloodline_stage_id"], true, out bool o15);
        if (!o15)
            return null;
        var ascId = _parse_string_name_field(data["ascension_id"], true, out bool o16);
        if (!o16)
            return null;
        var ascStId = _parse_string_name_field(data["ascension_stage_id"], true, out bool o17);
        if (!o17)
            return null;
        if (
            !TryGetStrictInt(
                data,
                "ascension_started_at_world_step",
                out int ascensionStartedAtWorldStep
            )
            || ascensionStartedAtWorldStep < -1
        )
            return null;
        var origRace = _parse_string_name_field(
            data["original_race_id_before_ascension"],
            true,
            out bool o18
        );
        if (!o18)
            return null;
        if (!TryGetStrictInt(data, "biological_age_years", out int biologicalAgeYears)
            || biologicalAgeYears < 0)
            return null;
        if (!TryGetStrictInt(data, "astral_memory_years", out int astralMemoryYears)
            || astralMemoryYears < 0)
            return null;
        var traitInstances = TraitInstanceCollection.FromPayloadArray(
            data["trait_instances"],
            TraitSourceKind.Character
        );
        if (traitInstances == null)
            return null;

        var ms = new PartyMemberState
        {
            member_id = memberId,
            display_name = displayName,
            faction_id = factionId,
            portrait_id = portraitId,
            control_mode = ctrl,
            current_hp = currentHp,
            current_mp = currentMp,
            current_aura = currentAura,
            is_dead = isDeadValue,
            race_id = raceId,
            subrace_id = subraceId,
            age_years = ageYears,
            birth_at_world_step = birthAtWorldStep,
            age_profile_id = ageProfId,
            natural_age_stage_id = natAgeStage,
            effective_age_stage_id = effAgeStage,
            effective_age_stage_source_type = effAgeSrcType,
            effective_age_stage_source_id = effAgeSrcId,
            body_size = bsVal,
            body_size_category = bsCat,
            versatility_pick = versPick,
            active_stage_advancement_modifier_ids = asami,
            bloodline_id = blId,
            bloodline_stage_id = blStId,
            ascension_id = ascId,
            ascension_stage_id = ascStId,
            ascension_started_at_world_step = ascensionStartedAtWorldStep,
            original_race_id_before_ascension = origRace,
            biological_age_years = biologicalAgeYears,
            astral_memory_years = astralMemoryYears,
            trait_instances = traitInstances,
        };
        ms.progression = UnitProgress.FromDictionary(progData);
        ms.equipment_state = EquipmentState.FromDictionary(esData);
        if (ms.progression == null || ms.equipment_state == null)
            return null;
        if (ms.progression.unit_id == "" || ms.progression.unit_id != ms.member_id)
            return null;
        if (ms.progression.display_name.StripEdges().Length == 0)
            return null;
        return ms;
    }

    private UnitBaseAttributes _get_unit_base_attributes() => progression?.unit_base_attributes;

    private static int ClampResource(int value, int maxValue)
    {
        int normalizedMax = Mathf.Max(maxValue, 0);
        return normalizedMax > 0 ? Mathf.Clamp(value, 0, normalizedMax) : 0;
    }

    private static StringName _parse_string_name_field(object rawValue, bool allowEmpty, out bool ok)
    {
        ok = false;
        if (!TryAsStringLike(rawValue, out string rawText))
        {
            return new StringName("");
        }
        var p = new StringName(rawText);
        if (p == "" && !allowEmpty)
            return new StringName("");
        ok = true;
        return p;
    }

    private static Godot.Collections.Array<StringName> _parse_unique_string_name_array(
        Godot.Collections.Array a
    )
    {
        var r = new Godot.Collections.Array<StringName>();
        var s = new Godot.Collections.Dictionary();
        foreach (var raw in a)
        {
            var p = _parse_string_name_field(raw, false, out bool o);
            if (!o || s.ContainsKey(p))
                return null;
            s[p] = true;
            r.Add(p);
        }
        return r;
    }

    private static bool _has_exact_fields(
        Godot.Collections.Dictionary d,
        Godot.Collections.Array<string> e
    )
    {
        if (d.Count != e.Count)
            return false;
        var el = new Godot.Collections.Dictionary();
        foreach (string fn in e)
            el[fn] = true;
        foreach (var k in d.Keys)
        {
            if (!TryAsStringLike(k, out string ks))
                return false;
            if (!el.ContainsKey(ks))
                return false;
            if ((bool)el[ks])
            {
                el[ks] = false;
            }
            else
                return false;
        }
        return true;
    }

    private static bool TryGetStrictString(
        Godot.Collections.Dictionary data,
        string key,
        out string value
    )
    {
        if (TryGetExactValue(data, key, out object rawValue)
            && TryAsStrictString(rawValue, out value))
        {
            return true;
        }
        value = "";
        return false;
    }

    private static bool TryGetStrictInt(
        Godot.Collections.Dictionary data,
        string key,
        out int value
    )
    {
        if (TryGetExactValue(data, key, out object rawValue)
            && TryAsStrictInt(rawValue, out value))
        {
            return true;
        }
        value = 0;
        return false;
    }

    private static bool TryReadBoolField(
        Godot.Collections.Dictionary data,
        string key,
        out bool value
    )
    {
        if (TryGetExactValue(data, key, out object rawValue) && TryAsBool(rawValue, out value))
        {
            return true;
        }
        value = false;
        return false;
    }

    private static bool TryGetDictionary(
        Godot.Collections.Dictionary data,
        string key,
        out Godot.Collections.Dictionary value
    )
    {
        if (TryGetExactValue(data, key, out object rawValue)
            && TryAsDictionary(rawValue, out value))
        {
            return true;
        }
        value = new Godot.Collections.Dictionary();
        return false;
    }

    private static bool TryGetArray(
        Godot.Collections.Dictionary data,
        string key,
        out Godot.Collections.Array value
    )
    {
        if (TryGetExactValue(data, key, out object rawValue) && TryAsArray(rawValue, out value))
        {
            return true;
        }
        value = new Godot.Collections.Array();
        return false;
    }

    private static bool TryAsStringLike(object rawValue, out string value)
    {
        if (rawValue is Variant variant)
        {
            if (variant.VariantType == Variant.Type.String)
            {
                value = variant.AsString();
                return true;
            }
            if (variant.VariantType == Variant.Type.StringName)
            {
                value = variant.AsStringName().ToString();
                return true;
            }
            value = "";
            return false;
        }
        if (rawValue is string stringValue)
        {
            value = stringValue;
            return true;
        }
        if (rawValue is StringName stringNameValue)
        {
            value = stringNameValue.ToString();
            return true;
        }
        value = "";
        return false;
    }

    private static bool TryAsStrictString(object rawValue, out string value)
    {
        if (rawValue is Variant variant && variant.VariantType == Variant.Type.String)
        {
            value = variant.AsString();
            return true;
        }
        if (rawValue is string stringValue)
        {
            value = stringValue;
            return true;
        }
        value = "";
        return false;
    }

    private static bool TryAsStrictInt(object rawValue, out int value)
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

    private static bool TryAsBool(object rawValue, out bool value)
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

    private static bool TryAsDictionary(
        object rawValue,
        out Godot.Collections.Dictionary value
    )
    {
        if (rawValue is Variant variant && variant.VariantType == Variant.Type.Dictionary)
        {
            value = variant.AsGodotDictionary();
            return true;
        }
        if (rawValue is Godot.Collections.Dictionary dictionary)
        {
            value = dictionary;
            return true;
        }
        value = new Godot.Collections.Dictionary();
        return false;
    }

    private static bool TryAsArray(object rawValue, out Godot.Collections.Array value)
    {
        if (rawValue is Variant variant && variant.VariantType == Variant.Type.Array)
        {
            value = variant.AsGodotArray();
            return true;
        }
        if (rawValue is Godot.Collections.Array array)
        {
            value = array;
            return true;
        }
        value = new Godot.Collections.Array();
        return false;
    }

    private static bool TryGetExactValue(
        Godot.Collections.Dictionary data,
        string key,
        out object value
    )
    {
        if (data != null && data.ContainsKey(key))
        {
            value = data[key];
            return true;
        }
        value = null;
        return false;
    }
}
