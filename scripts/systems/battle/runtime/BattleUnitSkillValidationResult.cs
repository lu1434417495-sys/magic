using System.Collections.Generic;
using Godot;

public readonly record struct BattleUnitSkillValidationResult(
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

	private Godot.Collections.Array<StringName> ToTargetUnitIdsArray() =>
		ToStringNameArray(TargetUnitIds);

	private Godot.Collections.Array ToTargetUnitsArray() => ToUnitArray(TargetUnits);

	private Godot.Collections.Array<StringName> ToRandomChainCandidateUnitIdsArray() =>
		ToStringNameArray(RandomChainCandidateUnitIds);

	private Godot.Collections.Array<Vector2I> ToPreviewCoordsArray() =>
		ToVector2IArray(PreviewCoords);

	internal Godot.Collections.Dictionary ToDictionary() =>
		new()
		{
			["allowed"] = Allowed,
			["message"] = Message ?? "",
			["target_unit_ids"] = ToTargetUnitIdsArray(),
			["target_units"] = ToTargetUnitsArray(),
			["random_chain_candidate_unit_ids"] = ToRandomChainCandidateUnitIdsArray(),
			["preview_coords"] = ToPreviewCoordsArray(),
		};

	private static Godot.Collections.Array<StringName> ToStringNameArray(
		IReadOnlyList<StringName> ids
	)
	{
		var result = new Godot.Collections.Array<StringName>();
		if (ids == null)
		{
			return result;
		}
		foreach (StringName id in ids)
		{
			result.Add(id);
		}
		return result;
	}

	private static Godot.Collections.Array ToUnitArray(IReadOnlyList<BattleUnitState> units)
	{
		var result = new Godot.Collections.Array();
		if (units == null)
		{
			return result;
		}
		foreach (BattleUnitState unit in units)
		{
			if (unit != null)
			{
				result.Add(unit);
			}
		}
		return result;
	}

	private static Godot.Collections.Array<Vector2I> ToVector2IArray(
		IReadOnlyList<Vector2I> coords
	)
	{
		var result = new Godot.Collections.Array<Vector2I>();
		if (coords == null)
		{
			return result;
		}
		foreach (Vector2I coord in coords)
		{
			result.Add(coord);
		}
		return result;
	}
}
