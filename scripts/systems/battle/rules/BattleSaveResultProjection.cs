internal static class BattleSaveResultProjection
{
    internal static Godot.Collections.Dictionary Project(BattleSaveSource source)
    {
        return new Godot.Collections.Dictionary
        {
            ["source_id"] = source.SourceId.ToString(),
            ["type"] = source.Type ?? "",
            ["tag"] = source.Tag.ToString(),
            ["mode"] = source.Mode.ToString(),
        };
    }

    internal static Godot.Collections.Dictionary Project(BattleSaveResult result)
    {
        return new Godot.Collections.Dictionary
        {
            ["has_save"] = result.HasSave,
            ["immune"] = result.Immune,
            ["success"] = result.Success,
            ["natural_roll"] = result.NaturalRoll,
            ["roll_total"] = result.RollTotal,
            ["dc"] = result.Dc,
            ["ability"] = result.Ability.ToString(),
            ["save_tag"] = result.SaveTag.ToString(),
            ["advantage_state"] = result.AdvantageState.ToString(),
            ["ability_value"] = result.AbilityValue,
            ["ability_modifier"] = result.AbilityModifier,
            ["bonus"] = result.Bonus,
            ["degree"] = result.Degree.ToString(),
            ["sources"] = SourceArray(result.Sources),
        };
    }

    internal static Godot.Collections.Dictionary Project(BattleSaveProbabilityResult result)
    {
        return new Godot.Collections.Dictionary
        {
            ["has_save"] = result.HasSave,
            ["immune"] = result.Immune,
            ["success_probability_basis_points"] = result.SuccessProbabilityBasisPoints,
            ["failure_probability_basis_points"] = result.FailureProbabilityBasisPoints,
            ["dc"] = result.Dc,
            ["ability"] = result.Ability.ToString(),
            ["save_tag"] = result.SaveTag.ToString(),
            ["advantage_state"] = result.AdvantageState.ToString(),
            ["ability_value"] = result.AbilityValue,
            ["ability_modifier"] = result.AbilityModifier,
            ["bonus"] = result.Bonus,
            ["sources"] = SourceArray(result.Sources),
        };
    }

    private static Godot.Collections.Array SourceArray(
        System.Collections.Generic.IReadOnlyList<BattleSaveSource> sources
    )
    {
        var result = new Godot.Collections.Array();
        if (sources == null)
            return result;
        foreach (BattleSaveSource source in sources)
        {
            result.Add(Project(source));
        }
        return result;
    }
}
