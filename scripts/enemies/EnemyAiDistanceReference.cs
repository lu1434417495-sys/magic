using Godot;

internal enum EnemyAiDistanceReference
{
    Unknown = 0,
    None,
    TargetUnit,
    TargetCoord,
    CandidatePool,
    EnemyFrontline,
}

internal static class EnemyAiDistanceReferences
{
    private static readonly StringName TargetUnit = "target_unit";
    private static readonly StringName TargetCoord = "target_coord";
    private static readonly StringName CandidatePool = "candidate_pool";
    private static readonly StringName EnemyFrontline = "enemy_frontline";

    internal static EnemyAiDistanceReference ToKind(StringName value)
    {
        if (value == "")
            return EnemyAiDistanceReference.None;
        if (value == TargetUnit)
            return EnemyAiDistanceReference.TargetUnit;
        if (value == TargetCoord)
            return EnemyAiDistanceReference.TargetCoord;
        if (value == CandidatePool)
            return EnemyAiDistanceReference.CandidatePool;
        if (value == EnemyFrontline)
            return EnemyAiDistanceReference.EnemyFrontline;
        return EnemyAiDistanceReference.Unknown;
    }

    internal static StringName ToStringName(EnemyAiDistanceReference reference)
    {
        return reference switch
        {
            EnemyAiDistanceReference.TargetUnit => TargetUnit,
            EnemyAiDistanceReference.TargetCoord => TargetCoord,
            EnemyAiDistanceReference.CandidatePool => CandidatePool,
            EnemyAiDistanceReference.EnemyFrontline => EnemyFrontline,
            _ => "",
        };
    }
}
