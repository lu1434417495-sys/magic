using System.Collections.Generic;
using Godot;

public partial class UnitProgress
{
    public bool HasUsedGrowthTrigger(StringName skillId)
    {
        if (skillId == "")
            return false;
        foreach (UnitProfessionProgress profession in _professions.Values)
            foreach (ProfessionPromotionRecord record in profession.PromotionHistoryTyped)
                if (record.growth_trigger_skill_id == skillId)
                    return true;
        return false;
    }

    public IReadOnlyList<StringName> GetUsedGrowthTriggerIds()
    {
        List<StringName> result = new();
        foreach (StringName professionId in GetSortedProfessionIdsTyped())
            foreach (ProfessionPromotionRecord record in _professions[professionId].PromotionHistoryTyped)
                result.Add(record.growth_trigger_skill_id);
        return result.AsReadOnly();
    }

    internal bool TryAppendPromotionRecord(StringName professionId, ProfessionPromotionRecord record)
    {
        UnitProfessionProgress profession = GetProfessionProgress(professionId);
        if (profession == null || record == null || record.growth_trigger_skill_id == ""
            || record.growth_trigger_level <= 0 || record.new_rank != profession.rank + 1
            || (!record.consumed_skill_ids.Contains(record.growth_trigger_skill_id)
                && !record.qualifier_skill_ids.Contains(record.growth_trigger_skill_id))
            || HasUsedGrowthTrigger(record.growth_trigger_skill_id))
            return false;
        profession.AddPromotionRecord(record);
        profession.rank = record.new_rank;
        return true;
    }

    internal bool HasValidPromotionHistory()
    {
        HashSet<StringName> used = new();
        long rankTotal = 0;
        foreach (UnitProfessionProgress profession in _professions.Values)
        {
            if (profession == null || profession.rank < 0
                || profession.PromotionHistoryTyped.Count != profession.rank)
                return false;
            int expectedRank = 1;
            foreach (ProfessionPromotionRecord record in profession.PromotionHistoryTyped)
            {
                if (record == null || record.new_rank != expectedRank++
                    || record.growth_trigger_skill_id == "" || record.growth_trigger_level <= 0
                    || (!record.consumed_skill_ids.Contains(record.growth_trigger_skill_id)
                        && !record.qualifier_skill_ids.Contains(record.growth_trigger_skill_id))
                    || !used.Add(record.growth_trigger_skill_id))
                    return false;
            }
            rankTotal += profession.rank;
        }
        return rankTotal == character_level;
    }
}
