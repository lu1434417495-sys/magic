using Godot;

internal enum BodySizeCategoryKind
{
    Unknown = 0,
    Tiny,
    Small,
    Medium,
    Large,
    Huge,
    Gargantuan,
    Boss,
}

internal static class BodySizeContentRules
{
    private static readonly StringName BodySizeCategoryTiny = "tiny";
    private static readonly StringName BodySizeCategorySmall = "small";
    private static readonly StringName BodySizeCategoryMedium = "medium";
    private static readonly StringName BodySizeCategoryLarge = "large";
    private static readonly StringName BodySizeCategoryHuge = "huge";
    private static readonly StringName BodySizeCategoryGargantuan = "gargantuan";
    private static readonly StringName BodySizeCategoryBoss = "boss";

    private const int BodySizeTiny = 1;
    private const int BodySizeSmall = 1;
    private const int BodySizeMedium = 2;
    private const int BodySizeLarge = 3;
    private const int BodySizeHuge = 4;
    private const int BodySizeGargantuan = 5;
    private const int BodySizeBoss = 6;

    internal static bool IsValidBodySizeCategory(StringName category) =>
        ToCategoryKind(category) != BodySizeCategoryKind.Unknown;

    internal static bool IsValidBodySize(int bodySize) =>
        ToCanonicalCategoryKind(bodySize) != BodySizeCategoryKind.Unknown;

    internal static int GetBodySizeForCategory(StringName category) =>
        ToBodySize(ToCategoryKind(category));

    internal static bool BodySizeMatchesCategory(StringName category, int bodySize) =>
        IsValidBodySizeCategory(category) && GetBodySizeForCategory(category) == bodySize;

    internal static Vector2I GetFootprintForBodySize(int bodySize) =>
        ToCanonicalCategoryKind(bodySize) switch
        {
            BodySizeCategoryKind.Large or BodySizeCategoryKind.Huge => new Vector2I(2, 2),
            BodySizeCategoryKind.Gargantuan or BodySizeCategoryKind.Boss => new Vector2I(3, 3),
            _ => Vector2I.One,
        };

    internal static Vector2I GetFootprintForCategory(StringName category) =>
        GetFootprintForBodySize(GetBodySizeForCategory(category));

    internal static BodySizeCategoryKind ToCategoryKind(StringName category)
    {
        if (category == BodySizeCategoryTiny)
            return BodySizeCategoryKind.Tiny;
        if (category == BodySizeCategorySmall)
            return BodySizeCategoryKind.Small;
        if (category == BodySizeCategoryMedium)
            return BodySizeCategoryKind.Medium;
        if (category == BodySizeCategoryLarge)
            return BodySizeCategoryKind.Large;
        if (category == BodySizeCategoryHuge)
            return BodySizeCategoryKind.Huge;
        if (category == BodySizeCategoryGargantuan)
            return BodySizeCategoryKind.Gargantuan;
        if (category == BodySizeCategoryBoss)
            return BodySizeCategoryKind.Boss;
        return BodySizeCategoryKind.Unknown;
    }

    internal static BodySizeCategoryKind ToCanonicalCategoryKind(int bodySize)
    {
        return bodySize switch
        {
            BodySizeSmall => BodySizeCategoryKind.Small,
            BodySizeMedium => BodySizeCategoryKind.Medium,
            BodySizeLarge => BodySizeCategoryKind.Large,
            BodySizeHuge => BodySizeCategoryKind.Huge,
            BodySizeGargantuan => BodySizeCategoryKind.Gargantuan,
            BodySizeBoss => BodySizeCategoryKind.Boss,
            _ => BodySizeCategoryKind.Unknown,
        };
    }

    internal static StringName ToStringName(BodySizeCategoryKind kind)
    {
        return kind switch
        {
            BodySizeCategoryKind.Tiny => BodySizeCategoryTiny,
            BodySizeCategoryKind.Small => BodySizeCategorySmall,
            BodySizeCategoryKind.Medium => BodySizeCategoryMedium,
            BodySizeCategoryKind.Large => BodySizeCategoryLarge,
            BodySizeCategoryKind.Huge => BodySizeCategoryHuge,
            BodySizeCategoryKind.Gargantuan => BodySizeCategoryGargantuan,
            BodySizeCategoryKind.Boss => BodySizeCategoryBoss,
            _ => "",
        };
    }

    internal static int ToBodySize(BodySizeCategoryKind kind)
    {
        return kind switch
        {
            BodySizeCategoryKind.Tiny => BodySizeTiny,
            BodySizeCategoryKind.Small => BodySizeSmall,
            BodySizeCategoryKind.Medium => BodySizeMedium,
            BodySizeCategoryKind.Large => BodySizeLarge,
            BodySizeCategoryKind.Huge => BodySizeHuge,
            BodySizeCategoryKind.Gargantuan => BodySizeGargantuan,
            BodySizeCategoryKind.Boss => BodySizeBoss,
            _ => 0,
        };
    }
}
