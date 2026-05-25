using Godot;

[GlobalClass]
public partial class VisionSourceData : RefCounted
{
    public string source_id;
    public Vector2I center;
    public int range;
    public string faction_id;

    public VisionSourceData() { }

    public VisionSourceData(string sourceIdentifier = "", Vector2I sourceCenter = default, int sourceRange = 0, string sourceFactionId = "")
    {
        source_id = sourceIdentifier;
        center = sourceCenter;
        range = sourceRange;
        faction_id = sourceFactionId;
    }
}
