using System.Collections.Generic;
using Godot;

public sealed class BattleSpecialProfileGateResult
{
    public bool Allowed { get; set; }
    public StringName ProfileId { get; set; } = "";
    public StringName SkillId { get; set; } = "";
    public StringName BlockCode { get; set; } = "";
    public string PlayerMessage { get; set; } = "";
    public Dictionary<string, object> DebugDetails { get; } = new(System.StringComparer.Ordinal);

}
