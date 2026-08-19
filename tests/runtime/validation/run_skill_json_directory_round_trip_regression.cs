#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class run_skill_json_directory_round_trip_regression : LifecycleTestSceneTree
{
    private const string JsonDirectory = "res://data/configs/json/skills";
    private readonly TestHarness _test = new();

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private void Run()
    {
        try
        {
            var sourceReader = new GodotContentJsonSourceReader();
            ContentImportBatch<SkillImportModel> batch =
                SkillContentJsonAuthoringDomain.CreateImportDescriptor(
                    JsonDirectory,
                    sourceReader
                ).Import();
            _test.False(
                batch.HasErrors,
                "canonical skill directory should import: "
                    + string.Join(
                        " | ",
                        batch.Diagnostics.Select(
                            static value => $"{value.RuleId}@{value.SourceLabel}{value.JsonPointer}:{value.Message}"
                        )
                    )
            );

            Dictionary<string, SkillImportModel> jsonById = batch.Entries.ToDictionary(
                static entry => entry.Context.EntryId,
                static entry => entry.Import,
                StringComparer.Ordinal
            );
            _test.Eq(jsonById.Count, 703, "canonical JSON should contain every migrated skill exactly once");
        }
        catch (Exception exception)
        {
            _test.Fail($"Unexpected canonical skill directory round-trip exception: {exception}");
        }

        RequestTestExit(_test.Finish("Skill JSON directory round-trip regression"));
    }
}
