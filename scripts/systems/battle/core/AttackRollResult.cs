using Godot;

public readonly struct AttackRollResult
{
    public readonly int Roll;
    public readonly StringName RollDisposition;
    public readonly bool Success;
    public readonly string ResolutionText;
    public readonly int RerollDie;
    public readonly int RerolledRoll;
    public readonly bool Triggered;
    public readonly string AttackRollNonce;

    public AttackRollResult(
        int roll = 0,
        StringName rollDisposition = null,
        bool success = false,
        string resolutionText = "",
        int rerollDie = 0,
        int rerolledRoll = 0,
        bool triggered = false,
        string attackRollNonce = ""
    )
    {
        Roll = roll;
        RollDisposition = rollDisposition ?? new StringName("");
        Success = success;
        ResolutionText = resolutionText ?? "";
        RerollDie = rerollDie;
        RerolledRoll = rerolledRoll;
        Triggered = triggered;
        AttackRollNonce = attackRollNonce ?? "";
    }

}
