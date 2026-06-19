using System.Collections.Generic;
using Godot;

internal readonly record struct BattleUnitSkillValidationResult(
	bool Allowed,
	string Message,
	IReadOnlyList<StringName> TargetUnitIds,
	IReadOnlyList<BattleUnitState> TargetUnits,
	IReadOnlyList<StringName> RandomChainCandidateUnitIds,
	IReadOnlyList<Vector2I> PreviewCoords
)
{
	public static BattleUnitSkillValidationResult Denied(string message) =>
		new(
			false,
			string.IsNullOrEmpty(message) ? "技能或目标无效。" : message,
			System.Array.Empty<StringName>(),
			System.Array.Empty<BattleUnitState>(),
			System.Array.Empty<StringName>(),
			System.Array.Empty<Vector2I>()
		);

	public static BattleUnitSkillValidationResult AllowedResult(
		IReadOnlyList<StringName> targetUnitIds,
		IReadOnlyList<BattleUnitState> targetUnits,
		IReadOnlyList<StringName> randomChainCandidateUnitIds = null,
		IReadOnlyList<Vector2I> previewCoords = null,
		string message = ""
	) =>
		new(
			true,
			message ?? "",
			targetUnitIds ?? System.Array.Empty<StringName>(),
			targetUnits ?? System.Array.Empty<BattleUnitState>(),
			randomChainCandidateUnitIds ?? System.Array.Empty<StringName>(),
			previewCoords ?? System.Array.Empty<Vector2I>()
		);
}

internal readonly record struct BattleUnitSkillTargetAffordance(bool Allowed, string Reason)
{
	public static BattleUnitSkillTargetAffordance AllowedResult() => new(true, "");

	public static BattleUnitSkillTargetAffordance Denied(string reason) =>
		new(false, string.IsNullOrEmpty(reason) ? "技能目标无效。" : reason);
}
