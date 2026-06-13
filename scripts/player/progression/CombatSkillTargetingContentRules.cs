using Godot;

internal static class CombatSkillTargetingContentRules
{
    internal static bool IsValidCombatTargetMode(StringName value)
    {
        return IsSupportedTargetMode(BattleTypedNames.ToTargetMode(value));
    }

    internal static bool IsValidCastVariantTargetMode(StringName value)
    {
        return IsSupportedTargetMode(BattleTypedNames.ToTargetMode(value));
    }

    internal static bool IsValidTargetSelectionMode(StringName value)
    {
        return BattleTypedNames.ToTargetSelectionMode(value)
            is BattleTargetSelectionMode.SingleUnit
                or BattleTargetSelectionMode.MultiUnit
                or BattleTargetSelectionMode.RandomChain
                or BattleTargetSelectionMode.Self
                or BattleTargetSelectionMode.SingleCoord
                or BattleTargetSelectionMode.CoordPair;
    }

    internal static bool IsValidSelectionOrderMode(StringName value)
    {
        return BattleTypedNames.ToTargetSelectionOrderMode(value)
            != BattleTargetSelectionOrderMode.Unknown;
    }

    internal static bool IsValidAreaPattern(StringName value)
    {
        return BattleTypedNames.ToAreaPattern(value)
            is BattleAreaPattern.Single
                or BattleAreaPattern.Self
                or BattleAreaPattern.Diamond
                or BattleAreaPattern.Square
                or BattleAreaPattern.Radius
                or BattleAreaPattern.Cross
                or BattleAreaPattern.Line
                or BattleAreaPattern.Cone
                or BattleAreaPattern.NarrowCone
                or BattleAreaPattern.FrontArc;
    }

    internal static bool IsValidFootprintPattern(StringName value)
    {
        return CombatCastVariantDef.ToFootprintPattern(value)
            != CombatCastFootprintPattern.Unknown;
    }

    internal static string ValidCombatTargetModeLabel()
    {
        return "ground, unit";
    }

    internal static string ValidCastVariantTargetModeLabel()
    {
        return "ground, unit";
    }

    internal static string ValidTargetSelectionModeLabel()
    {
        return "coord_pair, multi_unit, random_chain, self, single_coord, single_unit";
    }

    internal static string ValidSelectionOrderModeLabel()
    {
        return "manual, stable";
    }

    internal static string ValidAreaPatternLabel()
    {
        return "cone, cross, diamond, front_arc, line, narrow_cone, radius, self, single, square";
    }

    internal static string ValidFootprintPatternLabel()
    {
        return "line2, single, square2, unordered";
    }

    private static bool IsSupportedTargetMode(BattleTargetMode mode)
    {
        return mode is BattleTargetMode.Unit or BattleTargetMode.Ground;
    }
}
