using System;
using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

internal static class BattleEventBatchProjection
{
    internal static GodotProjectionLease<GDictionary> BuildLease(BattleEventBatch batch)
    {
        GDictionary root = new();
        GodotProjectionLease<GDictionary> lease =
            GodotProjectionLease<GDictionary>.CreateOwnedRoot(
                root,
                "battle-event-batch",
                LifetimeDomain.Request,
                "BattleEventBatchProjection.root"
            );
        try
        {
            WriteInto(lease, root, batch);
            return lease;
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }

    internal static void WriteInto<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        GDictionary target,
        BattleEventBatch batch
    )
        where TLeaseRoot : class, IDisposable
    {
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentNullException.ThrowIfNull(target);
        batch ??= new BattleEventBatch();

        target["phase_changed"] = batch.phase_changed;
        target["battle_ended"] = batch.battle_ended;
        target["changed_unit_ids"] = WriteArray(
            lease,
            batch.ChangedUnitIdsTyped,
            "BattleEventBatchProjection.changed_unit_ids"
        );
        target["changed_coords"] = WriteArray(
            lease,
            batch.ChangedCoordsTyped,
            "BattleEventBatchProjection.changed_coords"
        );
        target["log_lines"] = WriteArray(
            lease,
            batch.LogLinesTyped,
            "BattleEventBatchProjection.log_lines"
        );
        target["report_entries"] = WriteReportEntries(
            lease,
            batch.ReportEntriesTyped
        );
        target["progression_deltas"] = WriteProgressionDeltas(
            lease,
            batch.ProgressionDeltasTyped
        );
        target["modal_requested"] = batch.modal_requested;
    }

    private static GArray WriteReportEntries<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        IReadOnlyList<IReadOnlyDictionary<string, object>> entries
    )
        where TLeaseRoot : class, IDisposable
    {
        GArray result = lease.Own(
            new GArray(),
            "BattleEventBatchProjection.report_entries"
        );
        if (entries == null)
            return result;
        for (int index = 0; index < entries.Count; index++)
        {
            result.Add(
                TraceDictionaryProjection.WriteDictionary(
                    lease,
                    entries[index],
                    $"BattleEventBatchProjection.report_entries[{index}]"
                )
            );
        }
        return result;
    }

    private static GArray WriteProgressionDeltas<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        IReadOnlyList<CharacterProgressionDelta> deltas
    )
        where TLeaseRoot : class, IDisposable
    {
        GArray result = lease.Own(
            new GArray(),
            "BattleEventBatchProjection.progression_deltas"
        );
        if (deltas == null)
            return result;
        for (int index = 0; index < deltas.Count; index++)
        {
            CharacterProgressionDelta delta = deltas[index];
            if (delta != null)
                result.Add(WriteProgressionDelta(lease, delta, index));
        }
        return result;
    }

    private static GDictionary WriteProgressionDelta<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        CharacterProgressionDelta delta,
        int index
    )
        where TLeaseRoot : class, IDisposable
    {
        string reason = $"BattleEventBatchProjection.progression_deltas[{index}]";
        GDictionary result = lease.Own(new GDictionary(), reason);
        result["member_id"] = delta.member_id;
        result["leveled_skill_ids"] = WriteArray(
            lease,
            delta.LeveledSkillIdsTyped,
            $"{reason}.leveled_skill_ids"
        );
        result["granted_skill_ids"] = WriteArray(
            lease,
            delta.GrantedSkillIdsTyped,
            $"{reason}.granted_skill_ids"
        );
        result["changed_profession_ids"] = WriteArray(
            lease,
            delta.ChangedProfessionIdsTyped,
            $"{reason}.changed_profession_ids"
        );
        result["character_level_before"] = delta.character_level_before;
        result["character_level_after"] = delta.character_level_after;
        result["pending_profession_choices"] = WritePendingChoices(
            lease,
            delta.PendingProfessionChoicesTyped,
            $"{reason}.pending_profession_choices"
        );
        result["needs_promotion_modal"] = delta.needs_promotion_modal;
        result["unlocked_achievement_ids"] = WriteArray(
            lease,
            delta.UnlockedAchievementIdsTyped,
            $"{reason}.unlocked_achievement_ids"
        );
        result["mastery_changes"] = WriteMasteryChanges(
            lease,
            delta.MasteryChangesTyped,
            $"{reason}.mastery_changes"
        );
        result["knowledge_changes"] = WriteKnowledgeChanges(
            lease,
            delta.KnowledgeChangesTyped,
            $"{reason}.knowledge_changes"
        );
        result["attribute_changes"] = WriteAttributeChanges(
            lease,
            delta.AttributeChangesTyped,
            $"{reason}.attribute_changes"
        );
        return result;
    }

    private static GArray WritePendingChoices<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        IReadOnlyList<PendingProfessionChoice> choices,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        GArray result = lease.Own(new GArray(), reason);
        if (choices == null)
            return result;
        for (int index = 0; index < choices.Count; index++)
        {
            PendingProfessionChoice choice = choices[index];
            GDictionary payload = lease.Own(new GDictionary(), $"{reason}[{index}]");
            if (choice != null)
            {
                payload["trigger_skill_ids"] = WriteStringArray(
                    lease,
                    choice.TriggerSkillIdsTyped,
                    $"{reason}[{index}].trigger_skill_ids"
                );
                payload["candidate_profession_ids"] = WriteStringArray(
                    lease,
                    choice.CandidateProfessionIdsTyped,
                    $"{reason}[{index}].candidate_profession_ids"
                );
                payload["target_rank_map"] = WriteTargetRankMap(
                    lease,
                    choice.TargetRankMapTyped,
                    $"{reason}[{index}].target_rank_map"
                );
                payload["qualifier_skill_pool_ids"] = WriteStringArray(
                    lease,
                    choice.QualifierSkillPoolIdsTyped,
                    $"{reason}[{index}].qualifier_skill_pool_ids"
                );
                payload["assignable_skill_candidate_ids"] = WriteStringArray(
                    lease,
                    choice.AssignableSkillCandidateIdsTyped,
                    $"{reason}[{index}].assignable_skill_candidate_ids"
                );
                payload["required_qualifier_count"] = choice.required_qualifier_count;
                payload["required_assigned_core_count"] = choice.required_assigned_core_count;
            }
            result.Add(payload);
        }
        return result;
    }

    private static GArray WriteMasteryChanges<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        IReadOnlyList<CharacterMasteryChangeFact> changes,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        GArray result = lease.Own(new GArray(), reason);
        if (changes == null)
            return result;
        for (int index = 0; index < changes.Count; index++)
        {
            CharacterMasteryChangeFact change = changes[index];
            if (change == null)
                continue;
            GDictionary payload = lease.Own(new GDictionary(), $"{reason}[{index}]");
            payload["skill_id"] = change.SkillId;
            payload["skill_name"] = change.SkillName;
            payload["mastery_amount"] = change.MasteryAmount;
            payload["source_type"] = change.SourceType;
            payload["source_label"] = change.SourceLabel;
            payload["reason_text"] = change.ReasonText;
            result.Add(payload);
        }
        return result;
    }

    private static GArray WriteKnowledgeChanges<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        IReadOnlyList<CharacterKnowledgeChangeFact> changes,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        GArray result = lease.Own(new GArray(), reason);
        if (changes == null)
            return result;
        for (int index = 0; index < changes.Count; index++)
        {
            CharacterKnowledgeChangeFact change = changes[index];
            if (change == null)
                continue;
            GDictionary payload = lease.Own(new GDictionary(), $"{reason}[{index}]");
            payload["knowledge_id"] = change.KnowledgeId;
            payload["knowledge_label"] = change.KnowledgeLabel;
            payload["reason_text"] = change.ReasonText;
            result.Add(payload);
        }
        return result;
    }

    private static GArray WriteAttributeChanges<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        IReadOnlyList<CharacterAttributeChangeFact> changes,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        GArray result = lease.Own(new GArray(), reason);
        if (changes == null)
            return result;
        for (int index = 0; index < changes.Count; index++)
        {
            CharacterAttributeChangeFact change = changes[index];
            if (change == null)
                continue;
            GDictionary payload = lease.Own(new GDictionary(), $"{reason}[{index}]");
            payload["attribute_id"] = change.AttributeId;
            payload["attribute_label"] = change.AttributeLabel;
            payload["delta"] = change.Delta;
            payload["reason_text"] = change.ReasonText;
            AddOptional(payload, "progress_delta", change.ProgressDelta);
            AddOptional(payload, "progress_before", change.ProgressBefore);
            AddOptional(payload, "progress_after", change.ProgressAfter);
            AddOptional(payload, "attribute_before", change.AttributeBefore);
            AddOptional(payload, "attribute_after", change.AttributeAfter);
            result.Add(payload);
        }
        return result;
    }

    private static GDictionary WriteTargetRankMap<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        IReadOnlyDictionary<StringName, int> values,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        GDictionary result = lease.Own(new GDictionary(), reason);
        if (values == null)
            return result;
        foreach (KeyValuePair<StringName, int> entry in values)
            result[entry.Key.ToString()] = entry.Value;
        return result;
    }

    private static GArray WriteStringArray<TLeaseRoot>(
        GodotProjectionLease<TLeaseRoot> lease,
        IEnumerable<StringName> values,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        GArray result = lease.Own(new GArray(), reason);
        if (values == null)
            return result;
        foreach (StringName value in values)
            result.Add(value.ToString());
        return result;
    }

    private static GArray WriteArray<TLeaseRoot, TValue>(
        GodotProjectionLease<TLeaseRoot> lease,
        IEnumerable<TValue> values,
        string reason
    )
        where TLeaseRoot : class, IDisposable
    {
        GArray result = lease.Own(new GArray(), reason);
        if (values == null)
            return result;
        foreach (TValue value in values)
            result.Add(ToVariant(value, reason));
        return result;
    }

    private static Variant ToVariant<TValue>(TValue value, string reason)
    {
        object boxed = value;
        return boxed switch
        {
            null => Variant.From(""),
            string text => Variant.From(text),
            StringName name => Variant.From(name),
            Vector2I coord => Variant.From(coord),
            int number => Variant.From(number),
            bool flag => Variant.From(flag),
            _ => throw new InvalidOperationException(
                $"Unsupported BattleEventBatch projection value type: {boxed.GetType().FullName} ({reason})."
            ),
        };
    }

    private static void AddOptional(GDictionary target, string key, int? value)
    {
        if (value.HasValue)
            target[key] = value.Value;
    }
}
