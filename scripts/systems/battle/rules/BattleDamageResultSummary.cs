using System.Collections.Generic;

internal sealed class BattleDamageResultSummary
{
    internal int Damage;
    internal int Healing;
    internal int ShieldAbsorbed;
    internal bool ShieldBroken;
    internal bool HasDamageEvent;
    internal bool AnyImmune;
    internal bool AnyHalf;
    internal bool AnyDouble;
    internal int FixedMitigationTotal;
    internal List<string> AbsorbLabels = new();
    internal List<string> HalfSourceLabels = new();
    internal List<string> DoubleSourceLabels = new();
    internal List<string> ImmuneSourceLabels = new();
    internal List<string> FixedMitigationSourceLabels = new();
    internal string AbsorbReasonText = "";
    internal string FixedMitigationSourceText = "";
}
