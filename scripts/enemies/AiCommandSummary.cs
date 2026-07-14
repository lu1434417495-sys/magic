using System;
using System.Collections.Generic;
using Godot;

public sealed class AiCommandSummary
{
    public string CommandType { get; set; } = "";
    public string UnitId { get; set; } = "";
    public string SkillId { get; set; } = "";
    public string SkillVariantId { get; set; } = "";
    public string TargetUnitId { get; set; } = "";
    public List<StringName> TargetUnitIds { get; } = new();
    public Vector2I TargetCoord { get; set; } = Vector2I.Zero;
    public List<Vector2I> TargetCoords { get; } = new();

    public AiCommandSummary() { }

    public AiCommandSummary(
        string p_command_type,
        string p_unit_id,
        string p_skill_id,
        string p_skill_variant_id,
        string p_target_unit_id,
        IEnumerable<StringName> p_target_unit_ids,
        Vector2I p_target_coord,
        IEnumerable<Vector2I> p_target_coords
    )
    {
        CommandType = p_command_type ?? "";
        UnitId = p_unit_id ?? "";
        SkillId = p_skill_id ?? "";
        SkillVariantId = p_skill_variant_id ?? "";
        TargetUnitId = p_target_unit_id ?? "";
        AddStringNames(TargetUnitIds, p_target_unit_ids);
        TargetCoord = p_target_coord;
        AddCoords(TargetCoords, p_target_coords);
    }

    public static AiCommandSummary FromCommand(BattleCommand command)
    {
        if (command == null)
            return new AiCommandSummary();

        var targetUnitIds = new List<StringName>();
        foreach (StringName uid in command.TargetUnitIdsTyped)
            targetUnitIds.Add(ProgressionDataUtils.to_string_name(uid));

        var targetCoords = new List<Vector2I>();
        foreach (Vector2I coord in command.TargetCoordsTyped)
            targetCoords.Add(coord);

        return new AiCommandSummary(
            command.command_type.ToString(),
            command.unit_id.ToString(),
            command.skill_id.ToString(),
            command.skill_variant_id.ToString(),
            command.target_unit_id.ToString(),
            targetUnitIds,
            command.target_coord,
            targetCoords
        );
    }

    internal AiCommandSummary Clone()
    {
        return new AiCommandSummary(
            CommandType,
            UnitId,
            SkillId,
            SkillVariantId,
            TargetUnitId,
            TargetUnitIds,
            TargetCoord,
            TargetCoords
        );
    }

    internal Dictionary<string, object> ToTraceDictionary()
    {
        return new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["command_type"] = CommandType,
            ["unit_id"] = UnitId,
            ["skill_id"] = SkillId,
            ["skill_variant_id"] = SkillVariantId,
            ["target_unit_id"] = TargetUnitId,
            ["target_unit_ids"] = new List<StringName>(TargetUnitIds),
            ["target_coord"] = TargetCoord,
            ["target_coords"] = new List<Vector2I>(TargetCoords),
        };
    }

    private static void AddStringNames(List<StringName> target, IEnumerable<StringName> values)
    {
        if (values == null)
        {
            return;
        }
        foreach (StringName value in values)
        {
            target.Add(ProgressionDataUtils.to_string_name(value));
        }
    }

    private static void AddCoords(List<Vector2I> target, IEnumerable<Vector2I> values)
    {
        if (values == null)
        {
            return;
        }
        foreach (Vector2I value in values)
        {
            target.Add(value);
        }
    }
}
