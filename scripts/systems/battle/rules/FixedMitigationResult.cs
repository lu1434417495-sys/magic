using System;
using System.Collections.Generic;

internal sealed class FixedMitigationResult
{
    public int BuffReduction { get; set; }
    public int StanceReduction { get; set; }
    public int PassiveReduction { get; set; }
    public int ContentDr { get; set; }
    public int GuardBlock { get; set; }
    public int GuardIgnoreApplied { get; set; }
    public bool LowLuckBlackStarWedgeTriggered { get; set; }
    public List<MitigationSourceResult> Sources { get; } = new();

    public int Total =>
        Math.Max(BuffReduction, 0)
        + Math.Max(StanceReduction, 0)
        + Math.Max(PassiveReduction, 0)
        + Math.Max(ContentDr, 0)
        + Math.Max(GuardBlock, 0);

    public string[] SourceLabels()
    {
        var labels = new List<string>();
        foreach (MitigationSourceResult source in Sources)
        {
            if (string.IsNullOrEmpty(source.StatusId) || labels.Contains(source.StatusId))
                continue;
            labels.Add(source.StatusId);
        }
        return labels.ToArray();
    }

    public void TrimSources()
    {
        var filtered = new List<MitigationSourceResult>();
        foreach (MitigationSourceResult source in Sources)
        {
            int remaining = source.Type switch
            {
                "buff_reduction" => BuffReduction,
                "stance_reduction" => StanceReduction,
                "passive_reduction" => PassiveReduction,
                "content_dr" => ContentDr,
                "guard_block" => GuardBlock,
                _ => 0,
            };
            if (remaining <= 0)
                continue;

            MitigationSourceResult updated = source;
            updated.Value = remaining;
            filtered.Add(updated);
        }
        Sources.Clear();
        Sources.AddRange(filtered);
    }

    public int ApplyGuardIgnore(int amount)
    {
        int remainingIgnore = Math.Max(amount, 0);
        int ignoredTotal = ApplyIgnoreToGuardBlock(ref remainingIgnore);
        ignoredTotal += ApplyIgnoreToStanceReduction(ref remainingIgnore);
        GuardIgnoreApplied += ignoredTotal;
        return ignoredTotal;
    }

    private int ApplyIgnoreToGuardBlock(ref int remainingIgnore)
    {
        int ignored = ApplyIgnoreToValue(GuardBlock, ref remainingIgnore);
        GuardBlock -= ignored;
        return ignored;
    }

    private int ApplyIgnoreToStanceReduction(ref int remainingIgnore)
    {
        int ignored = ApplyIgnoreToValue(StanceReduction, ref remainingIgnore);
        StanceReduction -= ignored;
        return ignored;
    }

    private static int ApplyIgnoreToValue(int value, ref int remainingIgnore)
    {
        if (remainingIgnore <= 0 || value <= 0)
            return 0;
        int ignored = Math.Min(value, remainingIgnore);
        remainingIgnore -= ignored;
        return ignored;
    }
}
