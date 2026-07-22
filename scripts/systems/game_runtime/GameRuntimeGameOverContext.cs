using System.Collections.Generic;
using Godot;

internal sealed class GameRuntimeGameOverContext
{
    internal GameRuntimeGameOverContext(
        string title,
        string description,
        string confirmText,
        StringName mainCharacterMemberId,
        string mainCharacterName,
        bool mainCharacterDead
    )
    {
        Title = title ?? "";
        Description = description ?? "";
        ConfirmText = confirmText ?? "";
        MainCharacterMemberId = mainCharacterMemberId;
        MainCharacterName = mainCharacterName ?? "";
        MainCharacterDead = mainCharacterDead;
    }

    internal string Title { get; }

    internal string Description { get; }

    internal string ConfirmText { get; }

    internal StringName MainCharacterMemberId { get; }

    internal string MainCharacterName { get; }

    internal bool MainCharacterDead { get; }

    internal IReadOnlyDictionary<string, object> BuildSnapshotPlain() =>
        new Dictionary<string, object>(System.StringComparer.Ordinal)
        {
            ["title"] = Title,
            ["description"] = Description,
            ["confirm_text"] = ConfirmText,
            ["main_character_member_id"] = MainCharacterMemberId.ToString(),
            ["main_character_name"] = MainCharacterName,
            ["main_character_dead"] = MainCharacterDead,
        };
}
