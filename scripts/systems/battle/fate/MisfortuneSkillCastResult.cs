using Godot;

public readonly record struct MisfortuneSkillCastResult(
    bool Ok,
    bool Gated,
    StringName MemberId,
    string Message,
    int CalamityCost,
    int RemainingCalamity,
    bool FreeCast
)
{
    public static MisfortuneSkillCastResult Success(
        StringName memberId = default,
        bool gated = true,
        int calamityCost = 0,
        int remainingCalamity = 0,
        bool freeCast = false
    ) =>
        new(true, gated, memberId, "", calamityCost, remainingCalamity, freeCast);

    public static MisfortuneSkillCastResult Failure(
        string message,
        StringName memberId = default,
        int calamityCost = 0,
        int remainingCalamity = 0
    ) => new(false, true, memberId, message ?? "", calamityCost, remainingCalamity, false);
}
