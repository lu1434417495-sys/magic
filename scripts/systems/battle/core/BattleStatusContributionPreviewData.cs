using Godot;

internal sealed record BattleStatusContributionPreviewData(
    StringName TargetUnitId,
    string TargetDisplayName,
    StringName StatusId,
    StringName SourceKind,
    StringName SourceDefinitionId,
    bool AppliesOnSaveFailure,
    bool AddsNewSource,
    int PreviousSourceStacks,
    int ResultSourceStacks,
    int SourceStackLimit,
    int ResultAggregateStacks,
    int ResultSourceCount,
    int ResultDurationTu,
    int TickIntervalTu,
    string StatusDisplayName = "",
    int? HealMultiplierPercent = null,
    MitigationTierKind MitigationTier = MitigationTierKind.None
)
{
    internal string SummaryText
    {
        get
        {
            string targetLabel = string.IsNullOrWhiteSpace(TargetDisplayName)
                ? TargetUnitId.ToString()
                : TargetDisplayName;
            string condition = AppliesOnSaveFailure ? "豁免失败时" : "命中时";
            string statusLabel = string.IsNullOrWhiteSpace(StatusDisplayName)
                ? StatusId.ToString()
                : StatusDisplayName;
            if (MitigationTier == MitigationTierKind.Double)
            {
                return $"{targetLabel}：伤害结算后若仍存活，施加{statusLabel}，持续{ResultDurationTu}TU；对应伤害在豁免后翻倍，相同易伤不叠加。";
            }
            if (HealMultiplierPercent is int healMultiplierPercent)
            {
                string stacks = SourceStackLimit > 1 ? $"（{ResultSourceStacks}/{SourceStackLimit}层）" : "";
                return $"{targetLabel}：{condition}施加{statusLabel}{stacks}，获得的生命治疗至多为正常值的{healMultiplierPercent}%，持续{ResultDurationTu}TU。";
            }
            string sourceAction = AddsNewSource ? "新增独立来源" : "叠加同源层数";
            string stackText = SourceStackLimit > 0
                ? $"{ResultSourceStacks}/{SourceStackLimit} 层"
                : $"{ResultSourceStacks} 层";
            return $"{targetLabel}：{condition}{sourceAction}，本来源 {stackText}、持续 {ResultDurationTu}TU、每 {TickIntervalTu}TU 结算；结算后合计 {ResultAggregateStacks} 层/{ResultSourceCount} 个来源。";
        }
    }
}
