using System;
using Godot;
using Godot.Collections;

[GlobalClass]
public partial class GameRuntimeCharacterInfoBuilder : RefCounted
{
    private static readonly StringName FortuneMarkedStatId = "fortune_marked";
    private static readonly StringName DoomMarkedStatId = "doom_marked";
    private static readonly StringName DoomAuthorityStatId = "doom_authority";

    private WeakReference<GameRuntimeFacade> _runtimeRef;

    private GameRuntimeFacade _runtime
    {
        get => ResolveWeakRef(_runtimeRef);
        set => _runtimeRef = value != null ? new WeakReference<GameRuntimeFacade>(value) : null;
    }

    public void Setup(GameRuntimeFacade runtime)
    {
        _runtime = runtime;
    }

    public void setup(GameRuntimeFacade runtime) => Setup(runtime);

    public new void Dispose()
    {
        _runtime = null;
    }

    public void dispose() => Dispose();

    public string build_character_info_meta_label(
        string typeLabel,
        string factionLabel,
        Vector2I coord
    ) => BuildCharacterInfoMetaLabel(typeLabel, factionLabel, coord);

    public Godot.Collections.Array<Dictionary> build_world_character_info_sections(
        Dictionary npc,
        Vector2I coord,
        string factionLabel
    ) => BuildWorldCharacterInfoSections(npc, coord, factionLabel);

    public Godot.Collections.Array<Dictionary> build_battle_character_info_sections(
        BattleUnitState unit,
        string typeLabel,
        string factionLabel
    ) => BuildBattleCharacterInfoSections(unit, typeLabel, factionLabel);

    public Dictionary build_battle_character_info_fate_payload(BattleUnitState unit) =>
        BuildBattleCharacterInfoFatePayload(unit);

    public Godot.Collections.Array<Dictionary> build_battle_character_info_base_entries(
        BattleUnitState unit,
        string typeLabel,
        string factionLabel
    ) => BuildBattleCharacterInfoBaseEntries(unit, typeLabel, factionLabel);

    public Godot.Collections.Array<Dictionary> build_battle_character_status_entries(
        BattleUnitState unit
    ) => BuildBattleCharacterStatusEntries(unit);

    public Godot.Collections.Array<Dictionary> build_battle_character_skill_entries(
        BattleUnitState unit
    ) => BuildBattleCharacterSkillEntries(unit);

    public int get_battle_unit_attribute_value(BattleUnitState unit, StringName attributeId) =>
        GetBattleUnitAttributeValue(unit, attributeId);

    public string BuildCharacterInfoMetaLabel(string typeLabel, string factionLabel, Vector2I coord)
    {
        return string.Format(
            "{0}  |  阵营 {1}  |  坐标 {2}",
            typeLabel,
            factionLabel,
            FormatCoord(coord)
        );
    }

    public Godot.Collections.Array<Dictionary> BuildWorldCharacterInfoSections(
        Dictionary npc,
        Vector2I coord,
        string factionLabel
    )
    {
        var entries = new Godot.Collections.Array<Dictionary>
        {
            new Dictionary { ["label"] = "类型", ["value"] = "世界 NPC" },
            new Dictionary { ["label"] = "阵营", ["value"] = factionLabel },
            new Dictionary { ["label"] = "坐标", ["value"] = FormatCoord(coord) },
        };
        var serviceType = DictionaryString(npc, "service_type").StripEdges();
        if (!string.IsNullOrEmpty(serviceType))
            entries.Add(new Dictionary { ["label"] = "服务", ["value"] = serviceType });
        var facilityName = DictionaryString(npc, "facility_name").StripEdges();
        if (!string.IsNullOrEmpty(facilityName))
            entries.Add(new Dictionary { ["label"] = "所属设施", ["value"] = facilityName });
        return new Godot.Collections.Array<Dictionary>
        {
            new Dictionary { ["title"] = "基础概览", ["entries"] = entries },
        };
    }

    public Godot.Collections.Array<Dictionary> BuildBattleCharacterInfoSections(
        BattleUnitState unit,
        string typeLabel,
        string factionLabel
    )
    {
        var sections = new Godot.Collections.Array<Dictionary>
        {
            new Dictionary
            {
                ["title"] = "基础概览",
                ["entries"] = BuildBattleCharacterInfoBaseEntries(unit, typeLabel, factionLabel),
            },
        };
        var identityEntries = BuildBattleCharacterIdentityEntries(unit);
        if (identityEntries.Count > 0)
            sections.Add(
                new Dictionary { ["title"] = "身份与特性", ["entries"] = identityEntries }
            );
        var statusEntries = BuildBattleCharacterStatusEntries(unit);
        if (statusEntries.Count > 0)
            sections.Add(new Dictionary { ["title"] = "状态效果", ["entries"] = statusEntries });
        var skillEntries = BuildBattleCharacterSkillEntries(unit);
        if (skillEntries.Count > 0)
            sections.Add(new Dictionary { ["title"] = "技能摘要", ["entries"] = skillEntries });
        return sections;
    }

    public Godot.Collections.Array<Dictionary> BuildBattleCharacterIdentityEntries(
        BattleUnitState unit
    )
    {
        var summary = GetBattleUnitIdentitySummary(unit);
        if (summary.Count == 0)
            return new Godot.Collections.Array<Dictionary>();
        var entries = new Godot.Collections.Array<Dictionary>
        {
            new Dictionary
            {
                ["label"] = "种族",
                ["value"] = DictionaryString(summary, "race_label"),
            },
            new Dictionary
            {
                ["label"] = "亚种",
                ["value"] = DictionaryString(summary, "subrace_label"),
            },
            new Dictionary
            {
                ["label"] = "年龄",
                ["value"] = string.Format(
                    "{0} 岁",
                    DictionaryInt(summary, "age_years")
                ),
            },
            new Dictionary
            {
                ["label"] = "自然阶段",
                ["value"] = DictionaryString(summary, "natural_age_stage_label"),
            },
            new Dictionary
            {
                ["label"] = "有效阶段",
                ["value"] = DictionaryString(summary, "effective_age_stage_label"),
            },
            new Dictionary
            {
                ["label"] = "体型",
                ["value"] = string.Format(
                    "{0}（{1}）",
                    DictionaryString(summary, "body_size_category"),
                    DictionaryInt(summary, "body_size")
                ),
            },
        };
        var bloodlineLabel = DictionaryString(summary, "bloodline_label").StripEdges();
        if (!string.IsNullOrEmpty(bloodlineLabel))
            entries.Add(
                new Dictionary
                {
                    ["label"] = "血脉",
                    ["value"] = JoinIdentityLabelPair(
                        bloodlineLabel,
                        DictionaryString(summary, "bloodline_stage_label").StripEdges()
                    ),
                }
            );
        var ascensionLabel = DictionaryString(summary, "ascension_label").StripEdges();
        if (!string.IsNullOrEmpty(ascensionLabel))
            entries.Add(
                new Dictionary
                {
                    ["label"] = "升华",
                    ["value"] = JoinIdentityLabelPair(
                        ascensionLabel,
                        DictionaryString(summary, "ascension_stage_label").StripEdges()
                    ),
                }
            );
        var damageResistanceText = FormatIdentityMap(DictionaryDictionary(summary, "damage_resistances"));
        if (!string.IsNullOrEmpty(damageResistanceText))
            entries.Add(
                new Dictionary { ["label"] = "伤害抗性", ["value"] = damageResistanceText }
            );
        var saveAdvantageText = FormatIdentityArray(DictionaryArray(summary, "save_advantage_tags"));
        if (!string.IsNullOrEmpty(saveAdvantageText))
            entries.Add(new Dictionary { ["label"] = "豁免优势", ["value"] = saveAdvantageText });
        foreach (
            var line in IdentityTextArray(
                DictionaryArray(summary, "trait_summary")
            )
        )
            entries.Add(new Dictionary { ["text"] = string.Format("特性：{0}", line) });
        foreach (
            var line in IdentityTextArray(
                DictionaryArray(summary, "racial_skill_lines")
            )
        )
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
        PartyState partyState = _runtime?.get_party_state();
        if (partyState != null && unit.source_member_id != "")
            hasSourceMember = partyState.get_member_state(unit.source_member_id) != null;
        if (
            !hasSourceMember
            && hiddenLuckAtBirth == 0
            && faithLuckBonus == 0
            && fortuneMarked == 0
            && doomMarked == 0
            && doomAuthority == 0
        )
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

    public Godot.Collections.Array<Dictionary> BuildBattleCharacterInfoBaseEntries(
        BattleUnitState unit,
        string typeLabel,
        string factionLabel
    )
    {
        var entries = new Godot.Collections.Array<Dictionary>
        {
            new Dictionary { ["label"] = "类型", ["value"] = typeLabel },
            new Dictionary { ["label"] = "阵营", ["value"] = factionLabel },
            new Dictionary { ["label"] = "坐标", ["value"] = FormatCoord(unit.coord) },
            new Dictionary
            {
                ["label"] = "HP",
                ["value"] = string.Format(
                    "{0} / {1}",
                    (int)(unit.current_hp),
                    Mathf.Max(GetBattleUnitAttributeValue(unit, "hp_max"), 1)
                ),
            },
            new Dictionary
            {
                ["label"] = "MP",
                ["value"] = string.Format(
                    "{0} / {1}",
                    (int)(unit.current_mp),
                    Mathf.Max(GetBattleUnitAttributeValue(unit, "mp_max"), 0)
                ),
            },
            new Dictionary
            {
                ["label"] = "AP",
                ["value"] = string.Format("{0}", (int)(unit.current_ap)),
            },
            new Dictionary
            {
                ["label"] = "行动",
                ["value"] = string.Format("{0}", (int)(unit.current_move_points)),
            },
        };
        var staminaMax = GetBattleUnitAttributeValue(unit, "stamina_max");
        if (staminaMax > 0 || (int)(unit.current_stamina) > 0)
            entries.Add(
                new Dictionary
                {
                    ["label"] = "ST",
                    ["value"] = string.Format(
                        "{0} / {1}",
                        (int)(unit.current_stamina),
                        Mathf.Max(staminaMax, 0)
                    ),
                }
            );
        var auraMax = GetBattleUnitAttributeValue(unit, "aura_max");
        if (auraMax > 0 || (int)(unit.current_aura) > 0)
            entries.Add(
                new Dictionary
                {
                    ["label"] = "Aura",
                    ["value"] = string.Format(
                        "{0} / {1}",
                        (int)(unit.current_aura),
                        Mathf.Max(auraMax, 0)
                    ),
                }
            );
        return entries;
    }

    public Godot.Collections.Array<Dictionary> BuildBattleCharacterStatusEntries(
        BattleUnitState unit
    )
    {
        var entries = new Godot.Collections.Array<Dictionary>();
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

    public Godot.Collections.Array<Dictionary> BuildBattleCharacterSkillEntries(
        BattleUnitState unit
    )
    {
        var entries = new Godot.Collections.Array<Dictionary>();
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
        return unit.attribute_snapshot.get_value(attributeId);
    }

    private string FormatCoord(Vector2I coord)
    {
        return _runtime != null
            ? _runtime.format_coord(coord)
            : string.Format("({0},{1})", coord.X, coord.Y);
    }

    private string GetSkillDisplayName(StringName skillId)
    {
        return _runtime != null
            ? _runtime._get_skill_display_name(skillId)
            : skillId.ToString();
    }

    private Dictionary GetBattleUnitIdentitySummary(BattleUnitState unit)
    {
        if (unit == null || unit.source_member_id == "" || _runtime == null)
            return new Dictionary();
        CharacterManagementModule characterManagement = _runtime.get_character_management();
        if (characterManagement == null)
            return new Dictionary();
        return characterManagement.get_identity_summary_for_member(unit.source_member_id);
    }

    private string JoinIdentityLabelPair(string primaryLabel, string secondaryLabel)
    {
        if (string.IsNullOrEmpty(secondaryLabel))
            return primaryLabel;
        return string.Format("{0} · {1}", primaryLabel, secondaryLabel);
    }

    private string FormatIdentityMap(Dictionary data)
    {
        if (data == null || data.Count == 0)
            return "";
        var parts = new Godot.Collections.Array<string>();
        foreach (var key in data.Keys)
            parts.Add(string.Format("{0}={1}", key.AsString(), data[key].AsString()));
        parts.Sort();
        return string.Join("，", parts);
    }

    private string FormatIdentityArray(Godot.Collections.Array value)
    {
        return string.Join("，", IdentityTextArray(value));
    }

    private Godot.Collections.Array<string> IdentityTextArray(Godot.Collections.Array value)
    {
        var result = new Godot.Collections.Array<string>();
        if (value == null)
            return result;
        foreach (var entry in value)
        {
            var text = entry.AsString().StripEdges();
            if (string.IsNullOrEmpty(text))
                continue;
            result.Add(text);
        }
        return result;
    }

    private static string DictionaryString(Dictionary dictionary, string key, string fallback = "")
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
            return fallback;
        var value = dictionary[key];
        return value.VariantType != Variant.Type.Nil ? value.AsString() : fallback;
    }

    private static int DictionaryInt(Dictionary dictionary, string key, int fallback = 0)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
            return fallback;
        var value = dictionary[key];
        return value.VariantType == Variant.Type.Int ? value.AsInt32() : fallback;
    }

    private static Dictionary DictionaryDictionary(Dictionary dictionary, string key)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
            return new Dictionary();
        var value = dictionary[key];
        return value.VariantType == Variant.Type.Dictionary
            ? value.AsGodotDictionary()
            : new Dictionary();
    }

    private static Godot.Collections.Array DictionaryArray(Dictionary dictionary, string key)
    {
        if (dictionary == null || !dictionary.ContainsKey(key))
            return new Godot.Collections.Array();
        var value = dictionary[key];
        return value.VariantType == Variant.Type.Array
            ? value.AsGodotArray()
            : new Godot.Collections.Array();
    }

    private static GameRuntimeFacade ResolveWeakRef(WeakReference<GameRuntimeFacade> weakRef)
    {
        if (weakRef == null || !weakRef.TryGetTarget(out GameRuntimeFacade target))
            return null;
        return target;
    }
}
