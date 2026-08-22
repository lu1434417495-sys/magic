using Godot;

internal enum StageAdvancementTargetAxis
{
    Unknown,
    Full,
    Physical,
    Mental,
    Bloodline,
    Divine,
    Martial,
    Domain,
}

internal static class StageAdvancementContentRules
{
    internal static StageAdvancementTargetAxis ToTargetAxis(StringName targetAxis) =>
        targetAxis.ToString() switch
        {
            "full" => StageAdvancementTargetAxis.Full,
            "physical" => StageAdvancementTargetAxis.Physical,
            "mental" => StageAdvancementTargetAxis.Mental,
            "bloodline" => StageAdvancementTargetAxis.Bloodline,
            "divine" => StageAdvancementTargetAxis.Divine,
            "martial" => StageAdvancementTargetAxis.Martial,
            "domain" => StageAdvancementTargetAxis.Domain,
            _ => StageAdvancementTargetAxis.Unknown,
        };

    internal static StringName ToStringName(StageAdvancementTargetAxis targetAxis) =>
        targetAxis switch
        {
            StageAdvancementTargetAxis.Full => "full",
            StageAdvancementTargetAxis.Physical => "physical",
            StageAdvancementTargetAxis.Mental => "mental",
            StageAdvancementTargetAxis.Bloodline => "bloodline",
            StageAdvancementTargetAxis.Divine => "divine",
            StageAdvancementTargetAxis.Martial => "martial",
            StageAdvancementTargetAxis.Domain => "domain",
            _ => "",
        };
}
