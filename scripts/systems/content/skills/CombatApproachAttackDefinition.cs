public sealed class CombatApproachAttackDefinition
{
    public CombatApproachAttackDefinition(int maximumPathHeightDeltaFromOrigin)
    {
        // 不在这里钳位：负值要原样交给 SkillDefinitionCombatProfileValidator 在内容期拒绝，
        // 钳成 0 会让作者写错的高差悄悄变成"不允许任何高差"。
        MaximumPathHeightDeltaFromOrigin = maximumPathHeightDeltaFromOrigin;
    }

    public int MaximumPathHeightDeltaFromOrigin { get; }

    public static CombatApproachAttackDefinition FromDiagnosticFixture(
        CombatApproachAttackDef source
    ) =>
        source == null
            ? null
            : new CombatApproachAttackDefinition(
                source.maximum_path_height_delta_from_origin
            );
}
