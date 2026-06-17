using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Godot;

public partial class run_direct_field_write_guard_regression : SceneTree
{
    private readonly TestHarness _test = new();

    private static readonly Regex ForbiddenWritePattern = new(
        @"\.(current_hp|current_mp|current_stamina|current_aura|current_ap|current_move_points|is_alive|is_dead|coord|body_size|body_size_category|status_effects|known_active_skill_ids|member_states|race_id|subrace_id|age_years|birth_at_world_step|age_profile_id|natural_age_stage_id|effective_age_stage_id|effective_age_stage_source_type|effective_age_stage_source_id|bloodline_id|bloodline_stage_id|ascension_id|ascension_stage_id|ascension_started_at_world_step|original_race_id_before_ascension|biological_age_years|astral_memory_years|versatility_pick|active_stage_advancement_modifier_ids)\s*=(?!=)",
        RegexOptions.Compiled
    );

    private static readonly HashSet<string> OwnerOrSnapshotFiles = new(StringComparer.Ordinal)
    {
        "scripts/player/progression/PartyMemberState.cs",
        "scripts/systems/battle/ai/BattleAiUnitSnapshot.cs",
        "scripts/systems/battle/core/BattleCellState.cs",
        "scripts/systems/battle/core/BattleUnitState.cs",
    };

    public override void _Initialize()
    {
        Run();
    }

    private void Run()
    {
        string repoRoot = ProjectSettings.GlobalizePath("res://");
        var violations = new List<string>();
        foreach (string path in Directory.EnumerateFiles(
            Path.Combine(repoRoot, "scripts"),
            "*.cs",
            SearchOption.AllDirectories
        ))
        {
            string repoPath = Path.GetRelativePath(repoRoot, path).Replace('\\', '/');
            if (OwnerOrSnapshotFiles.Contains(repoPath))
                continue;

            string[] lines = File.ReadAllLines(path);
            for (int index = 0; index < lines.Length; index++)
            {
                Match match = ForbiddenWritePattern.Match(lines[index]);
                if (!match.Success)
                    continue;
                violations.Add(
                    $"{repoPath}:{index + 1}: 禁止直接写 {match.Groups[1].Value}，请改用 owner 类型接口。"
                );
            }
        }

        if (violations.Count > 0)
        {
            _test.Fail("Direct field write guard failed:\n" + string.Join("\n", violations));
        }

        Quit(_test.Finish("Direct field write guard regression"));
    }
}
