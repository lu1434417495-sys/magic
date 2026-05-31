using Godot;
using GDictionary = Godot.Collections.Dictionary;

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
    ) =>
        new(false, true, memberId, message ?? "", calamityCost, remainingCalamity, false);

    public GDictionary ToDictionary()
    {
        var result = new GDictionary { ["ok"] = Ok };
        if (!Gated)
        {
            result["gated"] = false;
        }
        if (MemberId != default && MemberId != "")
        {
            result["member_id"] = MemberId.ToString();
        }
        if (!string.IsNullOrEmpty(Message))
        {
            result["message"] = Message;
        }
        if (CalamityCost > 0)
        {
            result["calamity_cost"] = CalamityCost;
        }
        if (RemainingCalamity > 0)
        {
            result["remaining_calamity"] = RemainingCalamity;
        }
        if (FreeCast)
        {
            result["free_cast"] = true;
        }
        return result;
    }
}
