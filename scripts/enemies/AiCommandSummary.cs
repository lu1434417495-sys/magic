using Godot;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;
using GVector2IArray = Godot.Collections.Array<Godot.Vector2I>;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class AiCommandSummary : RefCounted
{
	public string command_type = "";
	public string unit_id = "";
	public string skill_id = "";
	public string skill_variant_id = "";
	public string target_unit_id = "";
	public GStringNameArray target_unit_ids = new();
	public Vector2I target_coord = Vector2I.Zero;
	public GVector2IArray target_coords = new();

	public AiCommandSummary() { }

	public AiCommandSummary(
		string p_command_type,
		string p_unit_id,
		string p_skill_id,
		string p_skill_variant_id,
		string p_target_unit_id,
		GStringNameArray p_target_unit_ids,
		Vector2I p_target_coord,
		GVector2IArray p_target_coords
	)
	{
		command_type = p_command_type;
		unit_id = p_unit_id;
		skill_id = p_skill_id;
		skill_variant_id = p_skill_variant_id;
		target_unit_id = p_target_unit_id;
		target_unit_ids = p_target_unit_ids != null ? p_target_unit_ids.Duplicate() : new GStringNameArray();
		target_coord = p_target_coord;
		target_coords = p_target_coords != null ? p_target_coords.Duplicate() : new GVector2IArray();
	}

	public static AiCommandSummary from_command(GodotObject command)
	{
		if (command == null)
			return new AiCommandSummary();

		var target_unit_ids_copy = new GStringNameArray();
		foreach (Variant uid in GdInterop.GetArray(command, "target_unit_ids"))
			target_unit_ids_copy.Add(GdInterop.ToStringName(uid));

		var target_coords_copy = new GVector2IArray();
		foreach (Variant coord in GdInterop.GetArray(command, "target_coords"))
		{
			if (coord.VariantType == Variant.Type.Vector2I)
				target_coords_copy.Add(coord.AsVector2I());
		}

		return new AiCommandSummary(
			GdInterop.GetString(command, "command_type"),
			GdInterop.GetString(command, "unit_id"),
			GdInterop.GetString(command, "skill_id"),
			GdInterop.GetString(command, "skill_variant_id"),
			GdInterop.GetString(command, "target_unit_id"),
			target_unit_ids_copy,
			GdInterop.GetVector2I(command, "target_coord", Vector2I.Zero),
			target_coords_copy
		);
	}

	public GDictionary to_dict()
	{
		return new GDictionary
		{
			["command_type"] = command_type,
			["unit_id"] = unit_id,
			["skill_id"] = skill_id,
			["skill_variant_id"] = skill_variant_id,
			["target_unit_id"] = target_unit_id,
			["target_unit_ids"] = target_unit_ids.Duplicate(),
			["target_coord"] = target_coord,
			["target_coords"] = target_coords.Duplicate(),
		};
	}
}
