using Godot;
using Godot.Collections;

[GlobalClass]
public partial class WorldMapSettlementNamePool : Resource
{
    [Export]
    public Array<string> settlement_display_names = new();

    public Array<string> BuildUniqueDisplayNames()
    {
        var uniqueNames = new Array<string>();
        var seenNames = new Dictionary();
        foreach (var rawName in settlement_display_names)
        {
            var normalizedName = rawName.StripEdges();
            if (string.IsNullOrEmpty(normalizedName) || seenNames.ContainsKey(normalizedName))
                continue;
            seenNames[normalizedName] = true;
            uniqueNames.Add(normalizedName);
        }
        return uniqueNames;
    }

    public Array<string> build_unique_display_names()
    {
        return BuildUniqueDisplayNames();
    }
}
