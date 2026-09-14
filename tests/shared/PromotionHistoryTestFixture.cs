using Godot;

/// <summary>Builds explicit historical state for tests that do not exercise the promotion command.</summary>
internal static class PromotionHistoryTestFixture
{
    // For non-progression tests whose fixture requires a character level and no live profession rules.
    internal static void InitializeHistoricalLevel(UnitProgress progress, int level)
    {
        for (int index = 1; index <= level; index++)
            Record(progress, $"fixture_growth_{index}");
    }

    internal static void Record(UnitProgress progress, StringName skillId, StringName professionId = default, int level = 1)
    {
        if (professionId == null || professionId == "") professionId = "fixture_history";
        var profession = progress.GetProfessionProgress(professionId);
        if (profession == null)
        {
            profession = new UnitProfessionProgress { profession_id = professionId };
            progress.SetProfessionProgress(profession);
        }
        if (progress.HasUsedGrowthTrigger(skillId)) return;
        if (!progress.TryAppendPromotionRecord(professionId, new ProfessionPromotionRecord
        {
            new_rank = profession.rank + 1,
            growth_trigger_skill_id = skillId,
            growth_trigger_level = level,
            consumed_skill_ids = new StringNameList(new[] { skillId }),
        }))
            throw new System.InvalidOperationException("Invalid promotion history fixture.");
        progress.character_level = 0;
        foreach (StringName id in progress.GetSortedProfessionIdsTyped())
            progress.character_level += progress.GetProfessionProgress(id).rank;
    }
}
