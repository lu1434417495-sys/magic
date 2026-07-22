using System;
using System.Collections.Generic;
using Godot;
using Godot.Collections;

internal sealed class GameRuntimeCharacterInfoBuilder
{
    private static readonly StringName FortuneMarkedStatId = "fortune_marked";
    private static readonly StringName DoomMarkedStatId = "doom_marked";
    private static readonly StringName DoomAuthorityStatId = "doom_authority";

    private WeakReference<IGameRuntimeCharacterInfoQuery> _queryRef;

    private IGameRuntimeCharacterInfoQuery _query
    {
        get => ResolveWeakRef(_queryRef);
        set =>
            _queryRef =
                value != null ? new WeakReference<IGameRuntimeCharacterInfoQuery>(value) : null;
    }

    internal void Setup(IGameRuntimeCharacterInfoQuery query)
    {
        _query = query;
    }

    internal void Dispose()
    {
        _query = null;
    }

    internal string BuildCharacterInfoMetaLabel(string typeLabel, string factionLabel, Vector2I coord)
    {
        return string.Format(
            "{0}  |  阵营 {1}  |  坐标 {2}",
            typeLabel,
            factionLabel,
            FormatCoord(coord)
        );
    }

    internal IReadOnlyList<GameRuntimeCharacterInfoSection> BuildWorldCharacterInfoSections(
        WorldMapNpcData npc,
        Vector2I coord,
        string factionLabel
    )
    {
        var entries = new List<GameRuntimeCharacterInfoEntry>
        {
            GameRuntimeCharacterInfoEntry.Pair("类型", "世界 NPC"),
            GameRuntimeCharacterInfoEntry.Pair("阵营", factionLabel),
            GameRuntimeCharacterInfoEntry.Pair("坐标", FormatCoord(coord)),
        };
        string serviceType = npc?.ServiceType ?? "";
        if (!string.IsNullOrEmpty(serviceType))
            entries.Add(GameRuntimeCharacterInfoEntry.Pair("服务", serviceType));
        string facilityName = npc?.FacilityName ?? "";
        if (!string.IsNullOrEmpty(facilityName))
            entries.Add(GameRuntimeCharacterInfoEntry.Pair("所属设施", facilityName));
        return new List<GameRuntimeCharacterInfoSection>
        {
            new("基础概览", entries),
        }.AsReadOnly();
    }

    internal IReadOnlyList<GameRuntimeCharacterInfoSection> BuildBattleCharacterInfoSections(
        BattleUnitState unit,
        string typeLabel,
        string factionLabel
    )
    {
        var sections = new List<GameRuntimeCharacterInfoSection>
        {
            new(
                "基础概览",
                BuildBattleCharacterInfoBaseEntries(unit, typeLabel, factionLabel)
            ),
        };
        IReadOnlyList<GameRuntimeCharacterInfoEntry> identityEntries =
            BuildBattleCharacterIdentityEntries(unit);
        if (identityEntries.Count > 0)
            sections.Add(new GameRuntimeCharacterInfoSection("身份与特性", identityEntries));
        IReadOnlyList<GameRuntimeCharacterInfoEntry> equipmentEntries =
            BuildBattleCharacterEquipmentEntries(unit);
        if (equipmentEntries.Count > 0)
            sections.Add(new GameRuntimeCharacterInfoSection("装备", equipmentEntries));
        IReadOnlyList<GameRuntimeCharacterInfoEntry> statusEntries =
            BuildBattleCharacterStatusEntries(unit);
        if (statusEntries.Count > 0)
            sections.Add(new GameRuntimeCharacterInfoSection("状态效果", statusEntries));
        IReadOnlyList<GameRuntimeCharacterInfoEntry> skillEntries =
            BuildBattleCharacterSkillEntries(unit);
        if (skillEntries.Count > 0)
            sections.Add(new GameRuntimeCharacterInfoSection("技能摘要", skillEntries));
        return sections.AsReadOnly();
    }

    internal IReadOnlyList<GameRuntimeCharacterInfoEntry> BuildBattleCharacterIdentityEntries(
        BattleUnitState unit
    )
    {
        var summary = GetBattleUnitIdentitySummary(unit);
        if (summary.Count == 0)
            return System.Array.Empty<GameRuntimeCharacterInfoEntry>();
        var entries = new List<GameRuntimeCharacterInfoEntry>
        {
            GameRuntimeCharacterInfoEntry.Pair(
                "种族",
                DictionaryString(summary, "race_label")
            ),
            GameRuntimeCharacterInfoEntry.Pair(
                "亚种",
                DictionaryString(summary, "subrace_label")
            ),
            GameRuntimeCharacterInfoEntry.Pair(
                "年龄",
                string.Format(
                    "{0} 岁",
                    DictionaryInt(summary, "age_years")
                )
            ),
            GameRuntimeCharacterInfoEntry.Pair(
                "自然阶段",
                DictionaryString(summary, "natural_age_stage_label")
            ),
            GameRuntimeCharacterInfoEntry.Pair(
                "有效阶段",
                DictionaryString(summary, "effective_age_stage_label")
            ),
            GameRuntimeCharacterInfoEntry.Pair(
                "体型",
                string.Format(
                    "{0}（{1}）",
                    DictionaryString(summary, "body_size_category"),
                    DictionaryInt(summary, "body_size")
                )
            ),
        };
        var bloodlineLabel = DictionaryString(summary, "bloodline_label").StripEdges();
        if (!string.IsNullOrEmpty(bloodlineLabel))
            entries.Add(
                GameRuntimeCharacterInfoEntry.Pair(
                    "血脉",
                    JoinIdentityLabelPair(
                        bloodlineLabel,
                        DictionaryString(summary, "bloodline_stage_label").StripEdges()
                    )
                )
            );
        var ascensionLabel = DictionaryString(summary, "ascension_label").StripEdges();
        if (!string.IsNullOrEmpty(ascensionLabel))
            entries.Add(
                GameRuntimeCharacterInfoEntry.Pair(
                    "升华",
                    JoinIdentityLabelPair(
                        ascensionLabel,
                        DictionaryString(summary, "ascension_stage_label").StripEdges()
                    )
                )
            );
        var damageResistanceText = FormatIdentityMap(DictionaryDictionary(summary, "damage_resistances"));
        if (!string.IsNullOrEmpty(damageResistanceText))
            entries.Add(
                GameRuntimeCharacterInfoEntry.Pair("伤害抗性", damageResistanceText)
            );
        var saveAdvantageText = FormatIdentityArray(DictionaryArray(summary, "save_advantage_tags"));
        if (!string.IsNullOrEmpty(saveAdvantageText))
            entries.Add(GameRuntimeCharacterInfoEntry.Pair("豁免优势", saveAdvantageText));
        foreach (
            var line in IdentityTextArray(
                DictionaryArray(summary, "trait_summary")
            )
        )
            entries.Add(GameRuntimeCharacterInfoEntry.TextEntry(string.Format("特性：{0}", line)));
        foreach (
            var line in IdentityTextArray(
                DictionaryArray(summary, "racial_skill_lines")
            )
        )
            entries.Add(
                GameRuntimeCharacterInfoEntry.TextEntry(string.Format("种族法术：{0}", line))
            );
        return entries.AsReadOnly();
    }

    internal GameRuntimeCharacterInfoFate BuildBattleCharacterInfoFate(BattleUnitState unit)
    {
        if (unit == null || unit.attribute_snapshot == null)
            return null;

        var hiddenLuckAtBirth = GetBattleUnitAttributeValue(unit, "hidden_luck_at_birth");
        var faithLuckBonus = GetBattleUnitAttributeValue(unit, "faith_luck_bonus");
        var fortuneMarked = GetBattleUnitAttributeValue(unit, FortuneMarkedStatId);
        var doomMarked = GetBattleUnitAttributeValue(unit, DoomMarkedStatId);
        var doomAuthority = GetBattleUnitAttributeValue(unit, DoomAuthorityStatId);
        bool hasSourceMember =
            unit.source_member_id != "" && (_query?.HasPartyMember(unit.source_member_id) ?? false);
        if (
            !hasSourceMember
            && hiddenLuckAtBirth == 0
            && faithLuckBonus == 0
            && fortuneMarked == 0
            && doomMarked == 0
            && doomAuthority == 0
        )
            return null;

        return new GameRuntimeCharacterInfoFate(
            hiddenLuckAtBirth,
            faithLuckBonus,
            fortuneMarked,
            doomMarked,
            doomAuthority
        );
    }

    internal IReadOnlyList<GameRuntimeCharacterInfoEntry> BuildBattleCharacterInfoBaseEntries(
        BattleUnitState unit,
        string typeLabel,
        string factionLabel
    )
    {
        var entries = new List<GameRuntimeCharacterInfoEntry>
        {
            GameRuntimeCharacterInfoEntry.Pair("类型", typeLabel),
            GameRuntimeCharacterInfoEntry.Pair("阵营", factionLabel),
            GameRuntimeCharacterInfoEntry.Pair("坐标", FormatCoord(unit.coord)),
            GameRuntimeCharacterInfoEntry.Pair(
                "HP",
                string.Format(
                    "{0} / {1}",
                    (int)(unit.current_hp),
                    Mathf.Max(GetBattleUnitAttributeValue(unit, "hp_max"), 1)
                )
            ),
            GameRuntimeCharacterInfoEntry.Pair(
                "MP",
                string.Format(
                    "{0} / {1}",
                    (int)(unit.current_mp),
                    Mathf.Max(GetBattleUnitAttributeValue(unit, "mp_max"), 0)
                )
            ),
            GameRuntimeCharacterInfoEntry.Pair(
                "AP",
                string.Format("{0}", (int)(unit.current_ap))
            ),
            GameRuntimeCharacterInfoEntry.Pair(
                "行动",
                string.Format("{0}", (int)(unit.current_move_points))
            ),
        };
        var staminaMax = GetBattleUnitAttributeValue(unit, "stamina_max");
        if (staminaMax > 0 || (int)(unit.current_stamina) > 0)
            entries.Add(
                GameRuntimeCharacterInfoEntry.Pair(
                    "ST",
                    string.Format(
                        "{0} / {1}",
                        (int)(unit.current_stamina),
                        Mathf.Max(staminaMax, 0)
                    )
                )
            );
        var auraMax = GetBattleUnitAttributeValue(unit, "aura_max");
        if (auraMax > 0 || (int)(unit.current_aura) > 0)
            entries.Add(
                GameRuntimeCharacterInfoEntry.Pair(
                    "Aura",
                    string.Format(
                        "{0} / {1}",
                        (int)(unit.current_aura),
                        Mathf.Max(auraMax, 0)
                    )
                )
            );
        return entries.AsReadOnly();
    }

    internal IReadOnlyList<GameRuntimeCharacterInfoEntry> BuildBattleCharacterEquipmentEntries(
        BattleUnitState unit
    )
    {
        var entries = new List<GameRuntimeCharacterInfoEntry>();
        if (unit == null)
            return entries.AsReadOnly();
        IGameRuntimeCharacterInfoQuery query = _query;
        if (query == null)
            return entries.AsReadOnly();

        var renderedItemIds = new System.Collections.Generic.HashSet<StringName>();
        EquipmentState equipmentView = unit.GetEquipmentView();
        if (equipmentView != null)
        {
            foreach (StringName slotId in EquipmentRules.GetAllSlotIdsTyped())
            {
                StringName entrySlotId = equipmentView.GetEntrySlotForSlot(slotId);
                // Skip secondary slots occupied by a multi-slot item anchored elsewhere.
                if (entrySlotId != (StringName)"" && entrySlotId != slotId)
                    continue;
                StringName itemId = equipmentView.GetEquippedItemId(slotId);
                if (itemId == (StringName)"")
                    continue;
                AppendEquipmentItemEntries(
                    entries,
                    query,
                    itemId,
                    EquipmentRules.GetSlotLabel(slotId),
                    renderedItemIds
                );
            }
        }

        // Enemy units carry their weapon via weapon_item_id with an empty equipment view;
        // surface it too so their weapon traits are inspectable.
        if (unit.weapon_item_id != "" && !renderedItemIds.Contains(unit.weapon_item_id))
            AppendEquipmentItemEntries(
                entries,
                query,
                unit.weapon_item_id,
                "武器",
                renderedItemIds
            );
        return entries.AsReadOnly();
    }

    private void AppendEquipmentItemEntries(
        ICollection<GameRuntimeCharacterInfoEntry> entries,
        IGameRuntimeCharacterInfoQuery query,
        StringName itemId,
        string slotLabel,
        System.Collections.Generic.HashSet<StringName> renderedItemIds
    )
    {
        if (!query.TryGetItemDefinition(itemId, out ItemDefinition itemDef) || itemDef == null)
            return;
        renderedItemIds.Add(itemId);
        string itemName = string.IsNullOrEmpty(itemDef.DisplayName)
            ? itemId.ToString()
            : itemDef.DisplayName;

        // Detail (flavor + trait mechanics) is revealed only on hover, so the row stays
        // compact and the character info window is not flooded with every slot's mechanics.
        var traitDefs = new System.Collections.Generic.Dictionary<StringName, TraitDefinition>();
        foreach (StringName traitId in itemDef.GetTraitIdsTyped())
        {
            if (
                query.TryGetTraitDefinition(traitId, out TraitDefinition traitDef)
                && traitDef != null
            )
                traitDefs[traitId] = traitDef;
        }
        string detail = ItemTraitDetailText.Compose(itemDef.Description, itemDef, traitDefs);
        if (string.IsNullOrEmpty(detail))
        {
            entries.Add(GameRuntimeCharacterInfoEntry.Pair(slotLabel, itemName));
            return;
        }
        entries.Add(
            GameRuntimeCharacterInfoEntry.Pair(slotLabel, $"{itemName} ⓘ", detail)
        );
    }

    internal IReadOnlyList<GameRuntimeCharacterInfoEntry> BuildBattleCharacterStatusEntries(
        BattleUnitState unit
    )
    {
        var entries = new List<GameRuntimeCharacterInfoEntry>();
        if (unit == null)
            return entries.AsReadOnly();
        foreach (StringName statusId in unit.GetSortedStatusEffectIdsTyped())
        {
            var effectState = unit.GetStatusEffect(statusId);
            if (effectState == null)
                continue;
            var line = statusId.ToString();
            if ((int)(effectState.stacks) > 1)
                line += string.Format(" x{0}", (int)(effectState.stacks));
            if (effectState.HasDuration())
                line += string.Format(" · {0} TU", (int)(effectState.duration));
            entries.Add(GameRuntimeCharacterInfoEntry.TextEntry(line));
        }
        return entries.AsReadOnly();
    }

    internal IReadOnlyList<GameRuntimeCharacterInfoEntry> BuildBattleCharacterSkillEntries(
        BattleUnitState unit
    )
    {
        var entries = new List<GameRuntimeCharacterInfoEntry>();
        foreach (var skillId in unit.known_active_skill_ids)
        {
            var resolvedSkillId = ProgressionDataUtils.to_string_name(skillId);
            if (resolvedSkillId == "")
                continue;
            entries.Add(
                GameRuntimeCharacterInfoEntry.TextEntry(GetSkillDisplayName(resolvedSkillId))
            );
            if (entries.Count >= 6)
                break;
        }
        return entries.AsReadOnly();
    }

    internal int GetBattleUnitAttributeValue(BattleUnitState unit, StringName attributeId)
    {
        if (unit == null || unit.attribute_snapshot == null)
            return 0;
        return unit.attribute_snapshot.GetValue(attributeId);
    }

    private string FormatCoord(Vector2I coord)
    {
        return _query != null
            ? _query.FormatCoord(coord)
            : string.Format("({0},{1})", coord.X, coord.Y);
    }

    private string GetSkillDisplayName(StringName skillId)
    {
        return _query != null
            ? _query.GetSkillDisplayName(skillId)
            : skillId.ToString();
    }

    private Dictionary GetBattleUnitIdentitySummary(BattleUnitState unit)
    {
        if (unit == null || unit.source_member_id == "" || _query == null)
            return new Dictionary();
        return _query.GetIdentitySummary(unit.source_member_id) ?? new Dictionary();
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
        {
            if (!TryAsDisplayString(key, out string keyText))
                continue;
            if (!TryAsDisplayString(data[key], out string valueText))
                continue;
            if (string.IsNullOrEmpty(keyText) || string.IsNullOrEmpty(valueText))
                continue;
            parts.Add(string.Format("{0}={1}", keyText, valueText));
        }
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
            if (!TryAsDisplayString(entry, out string text))
                continue;
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
        return TryAsExactString(dictionary[key], out string value) ? value : fallback;
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

    private static bool TryAsExactString(object rawValue, out string value)
    {
        switch (rawValue)
        {
            case string text:
                value = text.StripEdges();
                return true;
            case Variant variant when variant.VariantType == Variant.Type.String:
                value = variant.AsString().StripEdges();
                return true;
            default:
                value = "";
                return false;
        }
    }

    private static bool TryAsDisplayString(object rawValue, out string value)
    {
        switch (rawValue)
        {
            case string text:
                value = text.StripEdges();
                return true;
            case StringName stringName:
                value = stringName.ToString().StripEdges();
                return true;
            case Variant variant when variant.VariantType == Variant.Type.String:
                value = variant.AsString().StripEdges();
                return true;
            case Variant variant when variant.VariantType == Variant.Type.StringName:
                value = variant.AsStringName().ToString().StripEdges();
                return true;
            default:
                value = "";
                return false;
        }
    }

    private static IGameRuntimeCharacterInfoQuery ResolveWeakRef(
        WeakReference<IGameRuntimeCharacterInfoQuery> weakRef
    )
    {
        if (
            weakRef == null
            || !weakRef.TryGetTarget(out IGameRuntimeCharacterInfoQuery target)
        )
            return null;
        return target;
    }
}
