/// <summary>
/// Carries final candidate facts resolved by canonical battle rules into the
/// generic AI scorer. The scorer consumes these values and must not reproduce
/// the gameplay rule that produced them.
/// </summary>
internal readonly record struct BattleAiSkillCandidateScoreFacts(
    int? FinalStaminaCost,
    int DelayedResolutionTu
);
