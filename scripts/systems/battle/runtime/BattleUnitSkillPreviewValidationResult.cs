using System.Collections.Generic;
using Godot;

internal readonly record struct BattleUnitSkillPreviewValidationResult(
    bool Allowed,
    string Message,
    IReadOnlyList<StringName> TargetUnitIds,
    IReadOnlyList<BattleUnitReadView> TargetUnits,
    IReadOnlyList<StringName> RandomChainCandidateUnitIds,
    IReadOnlyList<Vector2I> PreviewCoords
)
{
    public static BattleUnitSkillPreviewValidationResult Denied(string message) =>
        new(
            false,
            string.IsNullOrEmpty(message) ? "技能或目标无效。" : message,
            System.Array.Empty<StringName>(),
            System.Array.Empty<BattleUnitReadView>(),
            System.Array.Empty<StringName>(),
            System.Array.Empty<Vector2I>()
        );

    public static BattleUnitSkillPreviewValidationResult AllowedResult(
        IReadOnlyList<StringName> targetUnitIds,
        IReadOnlyList<BattleUnitReadView> targetUnits,
        IReadOnlyList<StringName> randomChainCandidateUnitIds = null,
        IReadOnlyList<Vector2I> previewCoords = null,
        string message = ""
    ) =>
        new(
            true,
            message ?? "",
            targetUnitIds ?? System.Array.Empty<StringName>(),
            targetUnits ?? System.Array.Empty<BattleUnitReadView>(),
            randomChainCandidateUnitIds ?? System.Array.Empty<StringName>(),
            previewCoords ?? System.Array.Empty<Vector2I>()
        );
}
