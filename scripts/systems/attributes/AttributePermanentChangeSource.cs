using Godot;

public enum AttributePermanentChangeSourceKind
{
    Unknown = 0,
    CharacterCreation,
    StoryScript,
}

public readonly record struct AttributePermanentChangeSource(
    AttributePermanentChangeSourceKind SourceKind,
    StringName SourceId,
    bool AllowProtectedCustomStatWrite
)
{
    public static readonly AttributePermanentChangeSource None = new(
        AttributePermanentChangeSourceKind.Unknown,
        "",
        false
    );

    public StringName SourceType => ToStringName(SourceKind);

    public static StringName ToStringName(AttributePermanentChangeSourceKind sourceKind)
    {
        return sourceKind switch
        {
            AttributePermanentChangeSourceKind.CharacterCreation => "character_creation",
            AttributePermanentChangeSourceKind.StoryScript => "story_script",
            _ => "",
        };
    }
}
