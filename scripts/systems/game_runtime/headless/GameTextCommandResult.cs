using Godot;

[GlobalClass]
public partial class GameTextCommandResult : RefCounted
{
    public string command_text = "";
    public bool ok = true;
    public bool skipped;
    public string message = "";
    public Godot.Collections.Dictionary snapshot = new();
    public string human_log = "";
    public string snapshot_text = "";
    public Godot.Collections.Array<Godot.Collections.Dictionary> assertions = new();

    public string render()
    {
        var lines = new System.Collections.Generic.List<string>();
        if (skipped)
            lines.Add($"SKIP {command_text}");
        else
            lines.Add($"{(ok ? "OK" : "ERR")} {command_text}");
        if (message.Length > 0)
            lines.Add(message);
        foreach (var assertionVariant in assertions)
        {
            var assertion = assertionVariant;
            lines.Add($"ASSERT {(string)(assertion.ContainsKey("summary") ? assertion["summary"] : "")} | actual={(string)(assertion.ContainsKey("actual") ? assertion["actual"] : "")} | expected={(string)(assertion.ContainsKey("expected") ? assertion["expected"] : "")}");
        }
        if (snapshot_text.Length > 0)
        {
            lines.Add("");
            lines.Add(snapshot_text);
        }
        return string.Join("\n", lines);
    }
}
