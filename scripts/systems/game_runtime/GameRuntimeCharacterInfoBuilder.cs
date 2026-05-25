using System;
using Godot;
using Godot.Collections;

[GlobalClass]
public partial class GameRuntimeCharacterInfoBuilder : RefCounted
{
    private static readonly StringName FortuneMarkedStatId = "fortune_marked";
    private static readonly StringName DoomMarkedStatId = "doom_marked";
    private static readonly StringName DoomAuthorityStatId = "doom_authority";

    private WeakReference<GodotObject> _runtimeRef;

    private GodotObject _runtime
    {
        get => ResolveWeakRef(_runtimeRef);
        set => _runtimeRef = value != null ? new WeakReference<GodotObject>(value) : null;
    }

    public void Setup(GodotObject runtime)
    {
        _runtime = runtime;
    }

    public new void Dispose()
    {
        _runtime = null;
    }

    public string BuildCharacterInfoMetaLabel(string typeLabel, string factionLabel, Vector2I coord)
    {
        return string.Format("{0}  |  阵营 {1}  |  坐标 {2}", typeLabel, factionLabel, FormatCoord(coord));
    }

    public Array<Dictionary> BuildWorldCharacterInfoSections(Dictionary npc, Vector2I coord, string factionLabel)
    {
        var entries = new Array<Dictionary>
        {
            new Dictionary { ["label"] = "类型", ["value"] = "世界 NPC" },
            new Dictionary { ["label"] = "阵营", ["value"] = factionLabel },
            new Dictionary { ["label"] = "坐标", ["value"] = FormatCoord(coord) },
        };
        var serviceType = DictionaryGet(npc, "service_type", "").AsString().StripEdges();
        if (!string.IsNullOrEmpty(serviceType))
            entries.Add(new Dictionary { ["label"] = "服务", ["value"] = serviceType });
        var facilityName = DictionaryGet(npc, "facility_name", "").AsString().StripEdges();
        if (!string.IsNullOrEmpty(facilityName))
            entries.Add(new Dictionary { ["label"] = "所属设施", ["value"] = facilityName });
        return new Array<Dictionary> { new Dictionary { ["title"] = "基础概览", ["entries"] = entries } };
    }

    public Array<Dictionary> BuildBattleCharacterInfoSections(BattleUnitState unit, string typeLabel, string factionLabel)
    {
        var sections = new Array<Dictionary>
        {
            new Dictionary { ["title"] = "基础概览", ["entries"] = BuildBattleCharacterInfoBaseEntries(unit, typeLabel, factionLabel) },
        };
        var identityEntries = BuildBattleCharacterIdentityEntries(unit);
        if (identityEntries.Count > 0)
            sections.Add(new Dictionary { ["title"] = "身份与特性", ["entries"] = identityEntries });
        var statusEntries = BuildBattleCharacterStatusEntries(unit);
        if (statusEntries.Count > 0)
            sections.Add(new Dictionary { ["title"] = "状态效果", ["entries"] = statusEntries });
        var skillEntries = BuildBattleCharacterSkillEntries(unit);
        if (skillEntries.Count > 0)
            sections.Add(new Dictionary { ["title"] = "技能摘要", ["entries"] = skillEntries });
        return sections;
    }

    public Array<Dictionary> BuildBattleCharacterIdentityEntries(BattleUnitState unit)
    {
        var summary = GetBattleUnitIdentitySummary(unit);
        if (summary.Count == 0)
            return new Array<Dictionary>();
        var entries = new Array<Dictionary>
        {
            new Dictionary { ["label"] = "种族", ["value"] = DictionaryGet(summary, "race_label", "").AsString() },
            new Dictionary { ["label"] = "亚种", ["value"] = DictionaryGet(summary, "subrace_label", "").AsString() },
            new Dictionary { ["label"] = "年龄", ["value"] = string.Format("{0} 岁", DictionaryGet(summary, "age_years", 0).AsInt32()) },
            new Dictionary { ["label"] = "自然阶段", ["value"] = DictionaryGet(summary, "natural_age_stage_label", "").AsString() },
            new Dictionary { ["label"] = "有效阶段", ["value"] = DictionaryGet(summary, "effective_age_stage_label", "").AsString() },
            new Dictionary { ["label"] = "体型", ["value"] = string.Format("{0}（{1}）", DictionaryGet(summary, "body_size_category", "").AsString(), DictionaryGet(summary, "body_size", 0).AsInt32()) },
        };
        var bloodlineLabel = DictionaryGet(summary, "bloodline_label", "").AsString().StripEdges();
        if (!string.IsNullOrEmpty(bloodlineLabel))
            entries.Add(new Dictionary { ["label"] = "血脉", ["value"] = JoinIdentityLabelPair(bloodlineLabel, DictionaryGet(summary, "bloodline_stage_label", "").AsString().StripEdges()) });
        var ascensionLabel = DictionaryGet(summary, "ascension_label", "").AsString().StripEdges();
        if (!string.IsNullOrEmpty(ascensionLabel))
            entries.Add(new Dictionary { ["label"] = "升华", ["value"] = JoinIdentityLabelPair(ascensionLabel, DictionaryGet(summary, "ascension_stage_label", "").AsString().StripEdges()) });
        var damageResistanceText = FormatIdentityMap(DictionaryGet(summary, "damage_resistances", new Dictionary()));
        if (!string.IsNullOrEmpty(damageResistanceText))
            entries.Add(new Dictionary { ["label"] = "伤害抗性", ["value"] = damageResistanceText });
        var saveAdvantageText = FormatIdentityArray(DictionaryGet(summary, "save_advantage_tags", new Array()));
        if (!string.IsNullOrEmpty(saveAdvantageText))
            entries.Add(new Dictionary { ["label"] = "豁免优势", ["value"] = saveAdvantageText });
        foreach (var line in IdentityTextArray(DictionaryGet(summary, "trait_summary", new Array())))
            entries.Add(new Dictionary { ["text"] = string.Format("特性：{0}", line) });
        foreach (var line in IdentityTextArray(DictionaryGet(summary, "racial_skill_lines", new Array())))
            entries.Add(new Dictionary { ["text"] = string.Format("种族法术：{0}", line) });
        return entries;
    }

    public Dictionary BuildBattleCharacterInfoFatePayload(BattleUnitState unit)
    {
        if (unit == null || unit.attribute_snapshot == null)
            return new Dictionary();

        var hiddenLuckAtBirth = GetBattleUnitAttributeValue(unit, "hidden_luck_at_birth");
        var faithLuckBonus = GetBattleUnitAttributeValue(unit, "faith_luck_bonus");
        var effectiveLuck = Mathf.Clamp(hiddenLuckAtBirth + faithLuckBonus, -6, 7);
        var fortuneMarked = GetBattleUnitAttributeValue(unit, FortuneMarkedStatId);
        var doomMarked = GetBattleUnitAttributeValue(unit, DoomMarkedStatId);
        var doomAuthority = GetBattleUnitAttributeValue(unit, DoomAuthorityStatId);
        var hasSourceMember = false;
        var partyState = _runtime != null ? _runtime.Call("get_party_state").AsGodotObject() : null;
        if (partyState != null && unit.source_member_id != "" && partyState.HasMethod("get_member_state"))
            hasSourceMember = partyState.Call("get_member_state", unit.source_member_id).AsGodotObject() != null;
        if (!hasSourceMember
            && hiddenLuckAtBirth == 0
            && faithLuckBonus == 0
            && fortuneMarked == 0
            && doomMarked == 0
            && doomAuthority == 0)
            return new Dictionary();

        return new Dictionary
        {
            ["hidden_luck_at_birth"] = hiddenLuckAtBirth,
            ["faith_luck_bonus"] = faithLuckBonus,
            ["effective_luck"] = effectiveLuck,
            ["fortune_marked"] = fortuneMarked,
            ["doom_marked"] = doomMarked,
            ["doom_authority"] = doomAuthority,
            ["has_misfortune"] = doomAuthority > 0,
        };
    }

    public Array<Dictionary> BuildBattleCharacterInfoBaseEntries(BattleUnitState unit, string typeLabel, string factionLabel)
    {
        var entries = new Array<Dictionary>
        {
            new Dictionary { ["label"] = "类型", ["value"] = typeLabel },
            new Dictionary { ["label"] = "阵营", ["value"] = factionLabel },
            new Dictionary { ["label"] = "坐标", ["value"] = FormatCoord(unit.coord) },
            new Dictionary { ["label"] = "HP", ["value"] = string.Format("{0} / {1}", (int)(unit.current_hp), Mathf.Max(GetBattleUnitAttributeValue(unit, "hp_max"), 1)) },
            new Dictionary { ["label"] = "MP", ["value"] = string.Format("{0} / {1}", (int)(unit.current_mp), Mathf.Max(GetBattleUnitAttributeValue(unit, "mp_max"), 0)) },
            new Dictionary { ["label"] = "AP", ["value"] = string.Format("{0}", (int)(unit.current_ap)) },
            new Dictionary { ["label"] = "行动", ["value"] = string.Format("{0}", (int)(unit.current_move_points)) },
        };
        var staminaMax = GetBattleUnitAttributeValue(unit, "stamina_max");
        if (staminaMax > 0 || (int)(unit.current_stamina) > 0)
            entries.Add(new Dictionary { ["label"] = "ST", ["value"] = string.Format("{0} / {1}", (int)(unit.current_stamina), Mathf.Max(staminaMax, 0)) });
        var auraMax = GetBattleUnitAttributeValue(unit, "aura_max");
        if (auraMax > 0 || (int)(unit.current_aura) > 0)
            entries.Add(new Dictionary { ["label"] = "Aura", ["value"] = string.Format("{0} / {1}", (int)(unit.current_aura), Mathf.Max(auraMax, 0)) });
        return entries;
    }

    public Array<Dictionary> BuildBattleCharacterStatusEntries(BattleUnitState unit)
    {
        var entries = new Array<Dictionary>();
        foreach (var statusKey in ProgressionDataUtils.sorted_string_keys(unit.status_effects))
        {
            var statusId = (StringName)statusKey;
            var effectState = unit.get_status_effect(statusId);
            if (effectState == null)
                continue;
            var line = statusId.ToString();
            if ((int)(effectState.stacks) > 1)
                line += string.Format(" x{0}", (int)(effectState.stacks));
            if (effectState.has_duration())
                line += string.Format(" · {0} TU", (int)(effectState.duration));
            entries.Add(new Dictionary { ["text"] = line });
        }
        return entries;
    }

    public Array<Dictionary> BuildBattleCharacterSkillEntries(BattleUnitState unit)
    {
        var entries = new Array<Dictionary>();
        foreach (var skillId in unit.known_active_skill_ids)
        {
            var resolvedSkillId = ProgressionDataUtils.to_string_name(skillId);
            if (resolvedSkillId == "")
                continue;
            entries.Add(new Dictionary { ["text"] = GetSkillDisplayName(resolvedSkillId) });
            if (entries.Count >= 6)
                break;
        }
        return entries;
    }

    public int GetBattleUnitAttributeValue(BattleUnitState unit, StringName attributeId)
    {
        if (unit == null || unit.attribute_snapshot == null)
            return 0;
        return unit.attribute_snapshot.Call("get_value", attributeId).AsInt32();
    }

    private string FormatCoord(Vector2I coord)
    {
        return _runtime != null ? _runtime.Call("format_coord", coord).AsString() : string.Format("({0},{1})", coord.X, coord.Y);
    }

    private string GetSkillDisplayName(StringName skillId)
    {
        return _runtime != null ? _runtime.Call("_get_skill_display_name", skillId).AsString() : skillId.ToString();
    }

    private Dictionary GetBattleUnitIdentitySummary(BattleUnitState unit)
    {
        if (unit == null || unit.source_member_id == "" || _runtime == null)
            return new Dictionary();
        if (!_runtime.HasMethod("get_character_management"))
            return new Dictionary();
        var characterManagement = _runtime.Call("get_character_management").AsGodotObject();
        if (characterManagement == null || !characterManagement.HasMethod("get_identity_summary_for_member"))
            return new Dictionary();
        var summary = characterManagement.Call("get_identity_summary_for_member", unit.source_member_id);
        return summary.VariantType == Variant.Type.Dictionary ? summary.AsGodotDictionary() : new Dictionary();
    }

    private string JoinIdentityLabelPair(string primaryLabel, string secondaryLabel)
    {
        if (string.IsNullOrEmpty(secondaryLabel))
            return primaryLabel;
        return string.Format("{0} · {1}", primaryLabel, secondaryLabel);
    }

    private string FormatIdentityMap(Variant value)
    {
        if (value.VariantType != Variant.Type.Dictionary)
            return "";
        var data = value.AsGodotDictionary();
        var parts = new Array<string>();
        foreach (var key in data.Keys)
            parts.Add(string.Format("{0}={1}", key.AsString(), data[key].AsString()));
        parts.Sort();
        return "，".Join(parts);
    }

    private string FormatIdentityArray(Variant value)
    {
        return string.Join("，", IdentityTextArray(value));
    }

    private Array<string> IdentityTextArray(Variant value)
    {
        var result = new Array<string>();
        if (value.VariantType != Variant.Type.Array)
            return result;
        var array = value.AsGodotArray();
        foreach (var entry in array)
        {
            var text = entry.AsString().StripEdges();
            if (string.IsNullOrEmpty(text))
                continue;
            result.Add(text);
        }
        return result;
    }

    private static Variant DictionaryGet(Dictionary dictionary, Variant key, Variant fallback)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
            return fallback;
        return dictionary[key];
    }

    private static GodotObject ResolveWeakRef(WeakReference<GodotObject> weakRef)
    {
        if (weakRef == null || !weakRef.TryGetTarget(out GodotObject target) || !GodotObject.IsInstanceValid(target))
            return null;
        return target;
    }
}

