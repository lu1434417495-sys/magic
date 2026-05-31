using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;

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

	public GStringNameArray TargetUnitIdsArray() => ToStringNameArray(TargetUnitIds);

	public GArray TargetUnitsArray() => ToUnitArray(TargetUnits);

	public GStringNameArray RandomChainCandidateUnitIdsArray() =>
		ToStringNameArray(RandomChainCandidateUnitIds);

	public GVector2IArray PreviewCoordsArray() => ToVector2IArray(PreviewCoords);

	public GDictionary ToDictionary() =>
		new()
		{
			["allowed"] = Allowed,
			["message"] = Message ?? "",
			["target_unit_ids"] = TargetUnitIdsArray(),
			["target_units"] = TargetUnitsArray(),
			["random_chain_candidate_unit_ids"] = RandomChainCandidateUnitIdsArray(),
			["preview_coords"] = PreviewCoordsArray(),
		};

	private static GStringNameArray ToStringNameArray(IReadOnlyList<StringName> ids)
	{
		var result = new GStringNameArray();
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

	private static GArray ToUnitArray(IReadOnlyList<BattleUnitState> units)
	{
		var result = new GArray();
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

	private static GVector2IArray ToVector2IArray(IReadOnlyList<Vector2I> coords)
	{
		var result = new GVector2IArray();
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
