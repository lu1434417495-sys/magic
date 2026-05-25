using Godot;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class AiCandidateSummary : RefCounted
{
	public string label = "";
	public AiCommandSummary command;
	public int total_score = 0;
	public GDictionary score_input = new();

	public AiCandidateSummary()
	{
		command = new AiCommandSummary();
	}

	public AiCandidateSummary(
		string p_label,
		AiCommandSummary p_command = null,
		int p_total_score = 0,
		GDictionary p_score_input = null
	)
	{
		label = p_label;
		command = p_command ?? new AiCommandSummary();
		total_score = p_total_score;
		score_input = p_score_input != null ? p_score_input.Duplicate(true) : new GDictionary();
	}

	public GDictionary to_dict()
	{
		return new GDictionary
		{
			["label"] = label,
			["command"] = command != null ? command.to_dict() : new GDictionary(),
			["total_score"] = total_score,
			["score_input"] = score_input.Duplicate(true),
		};
	}
}
