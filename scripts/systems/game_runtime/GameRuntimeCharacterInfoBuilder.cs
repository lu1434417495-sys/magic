using System;
using Godot;
using Godot.Collections;

internal sealed class GameRuntimeCharacterInfoBuilder
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

    internal void Setup(GameRuntimeFacade runtime)
    {
        _runtime = runtime;
    }

    internal void Dispose()
    {
        _runtime = null;
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

    internal Godot.Collections.Array<Dictionary> BuildWorldCharacterInfoSections(
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

    internal Godot.Collections.Array<Dictionary> BuildBattleCharacterInfoSections(
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
        var equipmentEntries = BuildBattleCharacterEquipmentEntries(unit);
        if (equipmentEntries.Count > 0)
            sections.Add(new Dictionary { ["title"] = "装备", ["entries"] = equipmentEntries });
        var statusEntries = BuildBattleCharacterStatusEntries(unit);
        if (statusEntries.Count > 0)
            sections.Add(new Dictionary { ["title"] = "状态效果", ["entries"] = statusEntries });
        var skillEntries = BuildBattleCharacterSkillEntries(unit);
        if (skillEntries.Count > 0)
            sections.Add(new Dictionary { ["title"] = "技能摘要", ["entries"] = skillEntries });
        return sections;
    }

    internal Godot.Collections.Array<Dictionary> BuildBattleCharacterIdentityEntries(
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

    internal Dictionary BuildBattleCharacterInfoFatePayload(BattleUnitState unit)
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
        PartyState partyState = _runtime?.GetPartyState();
        if (partyState != null && unit.source_member_id != "")
            hasSourceMember = partyState.GetMemberState(unit.source_member_id) != null;
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

    internal Godot.Collections.Array<Dictionary> BuildBattleCharacterInfoBaseEntries(
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

    internal Godot.Collections.Array<Dictionary> BuildBattleCharacterEquipmentEntries(
        BattleUnitState unit
    )
    {
        var entries = new Godot.Collections.Array<Dictionary>();
        if (unit == null)
            return entries;
        var itemDefs = _runtime?.GetItemDefsTyped();
        if (itemDefs == null)
            return entries;
        var traitDefs = _runtime?.GetContentCatalogTyped()?.GetTraitDefsTyped();

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
                    itemDefs,
                    traitDefs,
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
                itemDefs,
                traitDefs,
                unit.weapon_item_id,
                "武器",
                renderedItemIds
            );
        return entries;
    }

    private void AppendEquipmentItemEntries(
        Godot.Collections.Array<Dictionary> entries,
        System.Collections.Generic.IReadOnlyDictionary<StringName, ItemDefinition> itemDefs,
        System.Collections.Generic.IReadOnlyDictionary<StringName, TraitDefinition> traitDefs,
        StringName itemId,
        string slotLabel,
        System.Collections.Generic.HashSet<StringName> renderedItemIds
    )
    {
        if (!itemDefs.TryGetValue(itemId, out ItemDefinition itemDef) || itemDef == null)
            return;
        renderedItemIds.Add(itemId);
        string itemName = string.IsNullOrEmpty(itemDef.DisplayName)
            ? itemId.ToString()
            : itemDef.DisplayName;

        // Detail (flavor + trait mechanics) is revealed only on hover, so the row stays
        // compact and the character info window is not flooded with every slot's mechanics.
        string detail = ItemTraitDetailText.Compose(itemDef.Description, itemDef, traitDefs);
        if (string.IsNullOrEmpty(detail))
        {
            entries.Add(new Dictionary { ["label"] = slotLabel, ["value"] = itemName });
            return;
        }
        entries.Add(
            new Dictionary
            {
                ["label"] = slotLabel,
                ["value"] = $"{itemName} ⓘ",
                ["tooltip"] = detail,
            }
        );
    }

    internal Godot.Collections.Array<Dictionary> BuildBattleCharacterStatusEntries(
        BattleUnitState unit
    )
    {
        var entries = new Godot.Collections.Array<Dictionary>();
        if (unit == null)
            return entries;
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
            entries.Add(new Dictionary { ["text"] = line });
        }
        return entries;
    }

    internal Godot.Collections.Array<Dictionary> BuildBattleCharacterSkillEntries(
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

    internal int GetBattleUnitAttributeValue(BattleUnitState unit, StringName attributeId)
    {
        if (unit == null || unit.attribute_snapshot == null)
            return 0;
        return unit.attribute_snapshot.GetValue(attributeId);
    }

    private string FormatCoord(Vector2I coord)
    {
        return _runtime != null
            ? _runtime.FormatCoord(coord)
            : string.Format("({0},{1})", coord.X, coord.Y);
    }

    private string GetSkillDisplayName(StringName skillId)
    {
        return _runtime != null
            ? _runtime.GetSkillDisplayName(skillId)
            : skillId.ToString();
    }

    private Dictionary GetBattleUnitIdentitySummary(BattleUnitState unit)
    {
        if (unit == null || unit.source_member_id == "" || _runtime == null)
            return new Dictionary();
        CharacterManagementModule characterManagement = _runtime.GetCharacterManagement();
        if (characterManagement == null)
            return new Dictionary();
        return characterManagement.GetIdentitySummaryForMember(unit.source_member_id);
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

    private static GameRuntimeFacade ResolveWeakRef(WeakReference<GameRuntimeFacade> weakRef)
    {
        if (weakRef == null || !weakRef.TryGetTarget(out GameRuntimeFacade target))
            return null;
        return target;
    }
}
