using Godot;

public enum BattleCognitionKind
{
    Unknown = 0,
    Mindless = 1,
    Instinctive = 2,
    Sapient = 3,
}

internal static class BattleCognitionContentRules
{
    internal static BattleCognitionKind ToKind(StringName value) =>
        ProgressionDataUtils.to_string_name(value).ToString() switch
        {
            "mindless" => BattleCognitionKind.Mindless,
            "instinctive" => BattleCognitionKind.Instinctive,
            "sapient" => BattleCognitionKind.Sapient,
            _ => BattleCognitionKind.Unknown,
        };

    internal static StringName ToStringName(BattleCognitionKind kind) =>
        kind switch
        {
            BattleCognitionKind.Mindless => "mindless",
            BattleCognitionKind.Instinctive => "instinctive",
            BattleCognitionKind.Sapient => "sapient",
            _ => new StringName(""),
        };

    internal static bool IsKnown(BattleCognitionKind kind) =>
        kind is BattleCognitionKind.Mindless
            or BattleCognitionKind.Instinctive
            or BattleCognitionKind.Sapient;

    internal static string ValidValueLabel() =>
        "mindless, instinctive, sapient";
}
