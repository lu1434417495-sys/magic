using System;
using System.Collections.Generic;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

// Partial slice of GameSession — validation over the immutable bound content snapshot.
public partial class GameSession
{
    private void RefreshContentValidationSnapshotState()
    {
        var validation = new ContentValidationSnapshotData();
        validation.Domains["progression"] = new ContentValidationDomainSnapshotData();
        validation.Domains["battle_special_profile"] =
            new ContentValidationDomainSnapshotData();
        validation.Domains["item"] = BuildItemContentValidationDomainSnapshot();
        validation.Domains["recipe"] = new ContentValidationDomainSnapshotData();
        validation.Domains["enemy"] = new ContentValidationDomainSnapshotData();
        validation.Domains["world"] = BuildWorldContentValidationDomainSnapshot();
        validation.Domains["quest"] = BuildQuestContentValidationDomainSnapshot();
        _contentValidationSnapshotData = validation;
    }

    private ContentValidationDomainSnapshotData BuildWorldContentValidationDomainSnapshot()
    {
        ContentSnapshot snapshot = RequireContentSnapshot();
        var errors = new List<string>();
        var validator = new WorldMapContentValidator();
        foreach ((string path, WorldGenerationDefinition definition) in snapshot.WorldGenerations)
        {
            AppendErrors(
                errors,
                validator.ValidateGenerationConfigTyped(
                    definition,
                    path,
                    snapshot.EnemyTemplates.Keys,
                    snapshot.EncounterRosters.Keys
                )
            );
        }
        return BuildContentValidationDomainSnapshotFromErrors(errors);
    }

    private ContentValidationDomainSnapshotData BuildItemContentValidationDomainSnapshot()
    {
        ContentSnapshot snapshot = RequireContentSnapshot();
        var errors = new List<string>();
        AppendErrors(
            errors,
            ItemTraitContentValidator.Validate(snapshot.Items, snapshot.Traits)
        );
        AppendErrors(
            errors,
            SkillBookItemContentValidator.Validate(snapshot.Items, snapshot.Skills)
        );
        return BuildContentValidationDomainSnapshotFromErrors(errors);
    }

    private ContentValidationDomainSnapshotData BuildQuestContentValidationDomainSnapshot()
    {
        ContentSnapshot snapshot = RequireContentSnapshot();
        if (snapshot.Quests.Count == 0)
            return new ContentValidationDomainSnapshotData();
        return BuildContentValidationDomainSnapshotFromErrors(
            QuestContentValidator.ValidateTyped(
                snapshot.Quests,
                snapshot.Items,
                snapshot.Skills,
                snapshot.EnemyTemplates,
                Array.Empty<string>()
            )
        );
    }

    private static ContentValidationDomainSnapshotData BuildContentValidationDomainSnapshotFromErrors(
        IEnumerable<string> errors
    )
    {
        var snapshot = new ContentValidationDomainSnapshotData();
        AppendErrors(snapshot.Errors, errors);
        return snapshot;
    }

    private int RequireContentValidationForRuntime(StringName operationId)
    {
        if (_contentSnapshot == null)
        {
            PushSessionError(
                "session.content.unbound",
                "GameSession blocked formal runtime entry because content is not bound."
            );
            return (int)Error.Unconfigured;
        }

        RefreshContentValidationSnapshotState();
        if (IsContentValidationOk())
            return (int)Error.Ok;
        int errorCount = _contentValidationSnapshotData?.ErrorCount ?? 0;
        PushSessionError(
            "session.content.validation_blocked",
            "GameSession blocked formal runtime entry because content validation failed.",
            Json.Stringify(
                new GDictionary
                {
                    ["operation_id"] = operationId.ToString(),
                    ["error_count"] = errorCount,
                }
            )
        );
        return (int)Error.InvalidData;
    }

    private void ReportContentValidationErrors()
    {
        foreach (string domainId in ContentValidationDomainOrder)
        {
            foreach (
                string validationError in _contentValidationSnapshotData.EnumerateDomainErrors(domainId)
            )
            {
                ReportContentValidationError(domainId, validationError);
            }
        }
    }

    private void ReportContentValidationError(string domainId, string validationError)
    {
        PushSessionError(
            $"session.content.{domainId}_validation_failed",
            $"{domainId} content error: {validationError}"
        );
    }
}
