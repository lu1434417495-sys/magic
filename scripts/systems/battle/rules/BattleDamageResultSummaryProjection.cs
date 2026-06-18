using System.Collections.Generic;
using GDictionary = Godot.Collections.Dictionary;
using GStringArray = Godot.Collections.Array<string>;

internal static class BattleDamageResultSummaryProjection
{
    internal static GDictionary Project(BattleDamageResultSummary summary)
    {
        summary ??= new BattleDamageResultSummary();
        return new GDictionary
        {
            ["damage"] = summary.Damage,
            ["healing"] = summary.Healing,
            ["shield_absorbed"] = summary.ShieldAbsorbed,
            ["shield_broken"] = summary.ShieldBroken,
            ["has_damage_event"] = summary.HasDamageEvent,
            ["any_immune"] = summary.AnyImmune,
            ["any_half"] = summary.AnyHalf,
            ["any_double"] = summary.AnyDouble,
            ["fixed_mitigation_total"] = summary.FixedMitigationTotal,
            ["absorb_labels"] = ToStringArray(summary.AbsorbLabels),
            ["half_source_labels"] = ToStringArray(summary.HalfSourceLabels),
            ["double_source_labels"] = ToStringArray(summary.DoubleSourceLabels),
            ["immune_source_labels"] = ToStringArray(summary.ImmuneSourceLabels),
            ["fixed_mitigation_source_labels"] = ToStringArray(
                summary.FixedMitigationSourceLabels
            ),
            ["absorb_reason_text"] = summary.AbsorbReasonText,
            ["fixed_mitigation_source_text"] = summary.FixedMitigationSourceText,
        };
    }

    private static GStringArray ToStringArray(IEnumerable<string> values)
    {
        var result = new GStringArray();
        if (values == null)
            return result;
        foreach (string value in values)
        {
            if (!string.IsNullOrEmpty(value))
                result.Add(value);
        }
        return result;
    }
}
