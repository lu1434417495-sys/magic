using System;

public static class SocialStandingRules
{
    public const int MinWorldRenown = 0;
    public const int MaxWorldRenown = 100;
    public const int MinCountryReputation = -100;
    public const int MaxCountryReputation = 100;

    public static int ClampWorldRenown(int value) =>
        Math.Clamp(value, MinWorldRenown, MaxWorldRenown);

    public static int ClampWorldRenown(long value) =>
        (int)Math.Clamp(value, MinWorldRenown, MaxWorldRenown);

    public static int ClampCountryReputation(int value) =>
        Math.Clamp(value, MinCountryReputation, MaxCountryReputation);

    public static int ClampCountryReputation(long value) =>
        (int)Math.Clamp(value, MinCountryReputation, MaxCountryReputation);

    public static bool IsValidWorldRenown(int value) =>
        IsValidWorldRenown((long)value);

    public static bool IsValidWorldRenown(long value) =>
        value >= MinWorldRenown && value <= MaxWorldRenown;

    public static bool IsValidCountryReputation(int value) =>
        IsValidCountryReputation((long)value);

    public static bool IsValidCountryReputation(long value) =>
        value >= MinCountryReputation && value <= MaxCountryReputation;
}
