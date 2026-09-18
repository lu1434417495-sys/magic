using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class PartyManagementWindow
{
    private string _build_achievements_text(UnitProgress progression)
    {
        if (progression == null || _achievement_defs.Count == 0)
            return "暂无成就数据。";

        var unlocked = _achievement_defs.Values
            .Select(definition => (Definition: definition,
                Progress: progression.GetAchievementProgressState(definition.AchievementId)))
            .Where(entry => entry.Progress?.is_unlocked == true)
            .OrderByDescending(entry => entry.Progress.unlocked_at_unix_time)
            .ThenBy(entry => entry.Definition.AchievementId.ToString(), StringComparer.Ordinal).ToList();
        if (unlocked.Count == 0)
            return "尚未获得成就。";
        var lines = new List<string> { $"已获得 {unlocked.Count} 项成就" };
        foreach (var entry in unlocked)
        {
            lines.Add("");
            lines.Add(entry.Definition.DisplayName);
            lines.Add(entry.Definition.Description);
            if (entry.Progress.unlocked_at_unix_time > 0)
            {
                DateTimeOffset time = DateTimeOffset.FromUnixTimeSeconds(entry.Progress.unlocked_at_unix_time).ToLocalTime();
                lines.Add($"取得时间：{time:yyyy-MM-dd HH:mm}");
            }
            foreach (AchievementRewardDefinition reward in entry.Definition.Rewards)
                lines.Add($"成就奖励：{_format_achievement_reward(reward)}");
        }
        return string.Join("\n", lines);
    }

    private string _format_achievement_reward(AchievementRewardDefinition reward)
    {
        string target = reward.TargetLabel;
        if (string.IsNullOrWhiteSpace(target))
            target = GetTypedObject(_skill_definitions, reward.TargetId)?.DisplayName ?? reward.TargetId.ToString();
        return reward.RewardKind switch
        {
            PendingCharacterRewardEntryKind.AttributeDelta => $"{target} {reward.Amount:+0;-0;0}",
            PendingCharacterRewardEntryKind.SkillMastery => $"{target} 熟练度 {reward.Amount:+0;-0;0}",
            PendingCharacterRewardEntryKind.SkillUnlock => $"解锁技能：{target}",
            PendingCharacterRewardEntryKind.KnowledgeUnlock => $"解锁知识：{target}",
            _ => $"{target} × {reward.Amount}",
        };
    }

    public void SetIdentityCatalog(ProgressionIdentityCatalogData catalog)
    {
        _identity_catalog = catalog ?? new ProgressionIdentityCatalogData();
        if (Visible)
            RefreshView();
    }

    private IReadOnlyList<GameRuntimeCharacterInfoEntry> _build_trait_entries(
        PartyMemberState member, IEnumerable<BattleEffectiveTraitInstanceReadView> instances) =>
        CharacterTraitDisplayText.BuildEntries(instances, member.equipment_state, _identity_catalog,
            id => GetTypedObject(_trait_defs, id),
            id => GetTypedObject(_skill_definitions, id),
            id => GetTypedObject(_itemDefinitions, id));

    private static string _format_detail_entries(IEnumerable<GameRuntimeCharacterInfoEntry> entries,
        string separator = "\n")
    {
        var lines = new List<string>();
        foreach (GameRuntimeCharacterInfoEntry entry in entries)
            lines.Add(entry.Kind == GameRuntimeCharacterInfoEntryKind.Pair
                ? $"{entry.Label}：{entry.Value}" : entry.Text);
        return string.Join(separator, lines);
    }

    private string _build_race_text(PartyMemberState member,
        IReadOnlyList<BattleEffectiveTraitInstanceReadView> effectiveTraits)
    {
        var lines = _build_identity_overview_lines(member);
        lines.Add($"生理年龄：{member.biological_age_years} 岁");
        lines.Add($"星界记忆：{member.astral_memory_years} 年");
        _identity_catalog.RaceDefs.TryGetValue(member.race_id, out var race);
        _identity_catalog.SubraceDefs.TryGetValue(member.subrace_id, out var subrace);
        AppendIdentityDescription(lines, race?.DisplayName, race?.Description);
        AppendIdentityDescription(lines, subrace?.DisplayName, subrace?.Description);
        if (_identity_catalog.BloodlineDefs.TryGetValue(member.bloodline_id, out var bloodline))
            AppendIdentityDescription(lines, bloodline.DisplayName, bloodline.Description);
        if (_identity_catalog.BloodlineStageDefs.TryGetValue(member.bloodline_stage_id, out var bloodlineStage))
            AppendIdentityDescription(lines, bloodlineStage.DisplayName, bloodlineStage.Description);
        if (_identity_catalog.AscensionDefs.TryGetValue(member.ascension_id, out var ascension))
            AppendIdentityDescription(lines, ascension.DisplayName, ascension.Description);
        if (_identity_catalog.AscensionStageDefs.TryGetValue(member.ascension_stage_id, out var ascensionStage))
            AppendIdentityDescription(lines, ascensionStage.DisplayName, ascensionStage.Description);

        // Identity summary is the existing detached UI boundary owned by CharacterManagement.
        GDictionary summary = _get_identity_summary(member.member_id);
        lines.Add("");
        lines.Add("种族能力：");
        AppendIdentityArray(lines, summary, "trait_summary", "身份能力");
        AppendIdentityArray(lines, summary, "racial_skill_lines", "种族技能");
        AppendIdentityArray(lines, summary, "save_advantage_tags", "豁免优势");
        AppendIdentityArray(lines, summary, "save_disadvantage_tags", "豁免劣势");
        AppendIdentityArray(lines, summary, "save_immunity_tags", "豁免免疫");
        if (summary.TryGetValue("damage_resistances", out Variant resistanceValue)
            && resistanceValue.VariantType == Variant.Type.Dictionary)
        {
            GDictionary resistances = resistanceValue.AsGodotDictionary();
            foreach (Variant key in resistances.Keys)
                lines.Add($"伤害抗性：{IdentityLabel(key.AsString())} · {IdentityLabel(resistances[key].AsString())}");
        }
        if (race != null)
        {
            lines.Add($"种族基础速度：{race.BaseSpeed}");
            AppendIdentityTags(lines, "种族熟练", race.ProficiencyTags);
            AppendIdentityTags(lines, "种族视觉", race.VisionTags);
            AppendIdentityModifiers(lines, "种族属性修正", race.AttributeModifiers);
        }
        if (subrace != null)
        {
            if (subrace.SpeedBonus != 0)
                lines.Add($"亚种速度修正：{subrace.SpeedBonus:+0;-0;0}");
            AppendIdentityTags(lines, "亚种熟练", subrace.ProficiencyTags);
            AppendIdentityTags(lines, "亚种视觉", subrace.VisionTags);
            AppendIdentityModifiers(lines, "亚种属性修正", subrace.AttributeModifiers);
        }
        lines.Add("");
        lines.Add("当前生效的种族、血脉与升华特性：");
        lines.Add(_format_detail_entries(_build_trait_entries(member,
            effectiveTraits.Where(instance => TraitContentRules.ToSourceKind(instance.SourceType)
                == TraitSourceKind.Identity)), "\n\n"));
        return string.Join("\n", lines);
    }

    private static void AppendIdentityDescription(List<string> lines, string title, string description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return;
        lines.Add("");
        lines.Add($"{title}：{description}");
    }

    private void AppendIdentityModifiers(List<string> lines, string title,
        IReadOnlyList<AttributeModifierDefinition> modifiers)
    {
        List<string> parts = _build_modifier_lines(modifiers, 1);
        if (parts.Count > 0)
            lines.Add($"{title}：{string.Join("，", parts)}");
    }

    private static void AppendIdentityArray(List<string> lines, GDictionary summary, string key, string title)
    {
        if (!summary.TryGetValue(key, out Variant value) || value.VariantType != Variant.Type.Array)
            return;
        foreach (Variant entry in value.AsGodotArray())
            lines.Add($"{title}：{IdentityLabel(entry.AsString())}");
    }

    private static void AppendIdentityTags(List<string> lines, string title, IEnumerable<StringName> tags)
    {
        string text = string.Join("、", tags.Select(tag => IdentityLabel(tag.ToString())));
        if (text.Length > 0)
            lines.Add($"{title}：{text}");
    }

    private static string IdentityLabel(string id) => id switch
    {
        "fire" => "火焰", "cold" => "寒冷", "lightning" => "闪电", "acid" => "强酸",
        "poison" => "毒素", "necrotic" => "黯蚀", "radiant" => "光耀", "psychic" => "心灵",
        "thunder" => "雷鸣", "force" => "力场", "physical" => "物理",
        "resistance" or "resistant" => "抗性", "immunity" or "immune" => "免疫",
        "vulnerability" or "vulnerable" => "易伤", "charm" or "charmed" => "魅惑",
        "half" => "伤害减半", "zero" => "免疫", "double" => "伤害加倍",
        "sleep" => "睡眠", "fear" or "frightened" => "恐惧", "disease" => "疾病",
        "darkvision" => "黑暗视觉", "superior_darkvision" => "高等黑暗视觉",
        "low_light_vision" => "昏暗视觉", "normal_vision" => "普通视觉",
        "perception" => "察觉", "stealth" => "潜行",
        "weapon_type_longsword" => "长剑", "weapon_type_shortsword" => "短剑",
        "weapon_type_shortbow" => "短弓", "weapon_type_longbow" => "长弓",
        "weapon_type_battleaxe" => "战斧", "weapon_type_handaxe" => "手斧",
        "weapon_type_light_hammer" => "轻锤", "weapon_type_warhammer" => "战锤",
        "weapon_type_glaive" => "长柄刀", "weapon_type_greatsword" => "巨剑",
        "weapon_type_halberd" => "戟", "weapon_type_hand_crossbow" => "手弩",
        "weapon_type_pike" => "长矛", "weapon_type_rapier" => "细剑", "weapon_type_spear" => "矛",
        "civilian" => "民兵武器", "light_armor" => "轻甲", "medium_armor" => "中甲", "shield" => "盾牌",
        "artificer_lore" => "工匠学识", "astral_knowledge" => "星界知识",
        "dwarven_toughness" => "矮人坚韧", "forest_lore" => "森林学识", "stone_lore" => "石工学识",
        "intelligence" => "智力", "willpower" => "意志", "intimidation" => "威吓",
        "magic" => "魔法", "illusion" => "幻术", "paralysis" => "麻痹", "freeze" => "寒冰",
        _ => id,
    };

    private void _append_equipment_instance_lines(List<string> lines, EquipmentInstanceState instance)
    {
        if (instance == null)
            return;
        string rarity = ((EquipmentInstanceState.RarityTier)instance.rarity) switch
        {
            EquipmentInstanceState.RarityTier.COMMON => "普通",
            EquipmentInstanceState.RarityTier.UNCOMMON => "精良",
            EquipmentInstanceState.RarityTier.RARE => "稀有",
            EquipmentInstanceState.RarityTier.EPIC => "史诗",
            EquipmentInstanceState.RarityTier.LEGENDARY => "传说",
            _ => "未知",
        };
        lines.Add($"品质：{rarity}  |  耐久：{instance.current_durability} / {EquipmentDurabilityRules.GetMaxDurabilityForRarity(instance.rarity)}");
        foreach (TraitInstanceState trait in instance.trait_instances)
        {
            TraitDefinition definition = _get_trait_def(trait.trait_id);
            string name = definition?.DisplayName ?? trait.trait_id.ToString();
            lines.Add($"[b]随机词条：{_escape_bbcode(name)}[/b]（等级 {trait.rank}，层数 {trait.stacks}）");
            if (!string.IsNullOrWhiteSpace(definition?.Description))
                lines.Add(_escape_bbcode(definition.Description));
            string rolls = CharacterTraitDisplayText.FormatRollValues(trait.roll_values);
            if (rolls.Length > 0)
                lines.Add(_escape_bbcode(rolls));
        }
    }
}
