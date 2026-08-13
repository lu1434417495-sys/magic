using System;
using Godot;

internal sealed class BattleEquipmentDurabilityPreviewData
{
    internal bool HasEffect { get; init; }
    internal bool HasMatchingEquipment { get; init; }
    internal StringName TargetUnitId { get; init; }
    internal StringName EntrySlotId { get; init; }
    internal StringName SlotId { get; init; }
    internal StringName ItemId { get; init; }
    internal string ItemDisplayName { get; init; } = "";
    internal int Rarity { get; init; }
    internal int CurrentDurability { get; init; }
    internal int MaximumDurability { get; init; }
    internal int DurabilityLossOnFailedSave { get; init; }
    internal int DurabilityAfterFailedSave { get; init; }
    internal int SaveDc { get; init; }
    internal StringName SaveAbility { get; init; }
    internal int EquipmentRaritySaveBonus { get; init; }
    internal int SaveSuccessProbabilityBasisPoints { get; init; }
    internal int SaveFailureProbabilityBasisPoints { get; init; }
    internal int ExpectedDurabilityLossBasisPoints { get; init; }
    internal int DestructionProbabilityBasisPoints { get; init; }
    internal int CandidateCount { get; init; }

    internal BattleEquipmentDurabilityPreviewData Clone() =>
        (BattleEquipmentDurabilityPreviewData)MemberwiseClone();

    internal string SummaryText
    {
        get
        {
            if (!HasEffect)
                return "";
            if (!HasMatchingEquipment)
                return "耐久：目标没有可受影响的装备，耐久效果无收益。";
            string itemLabel = string.IsNullOrWhiteSpace(ItemDisplayName)
                ? ItemId.ToString()
                : ItemDisplayName;
            string saveRate = FormatBasisPoints(SaveSuccessProbabilityBasisPoints);
            string outcome = DurabilityAfterFailedSave <= 0
                ? "失败时将摧毁该装备"
                : $"失败后预计剩余 {DurabilityAfterFailedSave} 耐久";
            return $"耐久：{itemLabel} {CurrentDurability}/{MaximumDurability}；{GetAbilityLabel(SaveAbility)}豁免 DC {SaveDc}（稀有度 +{EquipmentRaritySaveBonus}，成功率 {saveRate}）；失败损失 {DurabilityLossOnFailedSave}，{outcome}。";
        }
    }

    private static string FormatBasisPoints(int basisPoints)
    {
        int clamped = Math.Clamp(basisPoints, 0, 10000);
        return clamped % 100 == 0
            ? $"{clamped / 100}%"
            : $"{clamped / 100.0f:0.#}%";
    }

    private static string GetAbilityLabel(StringName ability) =>
        ProgressionDataUtils.to_string_name(ability).ToString() switch
        {
            "strength" => "力量",
            "agility" => "敏捷",
            "constitution" => "体质",
            "perception" => "感知",
            "intelligence" => "智力",
            "willpower" => "意志",
            _ => "属性",
        };
}
