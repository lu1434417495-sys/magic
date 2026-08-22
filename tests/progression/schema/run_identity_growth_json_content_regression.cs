using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class run_identity_growth_json_content_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private void Run()
    {
        TestProductionSnapshotUsesJsonDefinitions();
        TestEveryDomainImportsStrictPlainContent();
        TestClosedKindsAndUnknownMembersFailClosed();
        TestSchemaAndOfflineRegistrationsAreComplete();
        RequestTestExit(_test.Finish("Identity growth JSON content regression"));
    }

    private void TestProductionSnapshotUsesJsonDefinitions()
    {
        ContentSnapshot snapshot = GameSessionTestFactory.GetProcessSnapshot();
        _test.Eq(snapshot.Professions.Count, 7, "production snapshot 应发布 7 个 profession definition。");
        _test.Eq(snapshot.Races.Count, 11, "production snapshot 应发布 11 个 race definition。");
        _test.Eq(snapshot.Subraces.Count, 31, "production snapshot 应发布 31 个 subrace definition。");
        _test.Eq(snapshot.FaithDeities.Count, 2, "production snapshot 应发布 2 个 faith definition。");
        _test.Eq(snapshot.AgeProfiles.Count, 11, "production snapshot 应发布 11 个 age profile definition。");
        _test.Eq(snapshot.Bloodlines.Count, 2, "production snapshot 应发布 2 个 bloodline root definition。");
        _test.Eq(snapshot.BloodlineStages.Count, 1, "production snapshot 应发布 1 个 bloodline stage definition。");
        _test.Eq(snapshot.Ascensions.Count, 2, "production snapshot 应发布 2 个 ascension root definition。");
        _test.Eq(snapshot.AscensionStages.Count, 1, "production snapshot 应发布 1 个 ascension stage definition。");
        _test.Eq(snapshot.StageAdvancements.Count, 1, "production snapshot 应发布 1 个 stage advancement definition。");

        RaceDefinition human = snapshot.Races["human"];
        _test.Eq(human.DefaultSubraceId, new StringName("common_human"), "race 到 subrace 应只保留 ID 关系。");
        _test.Eq(human.AgeProfileId, new StringName("human_age_profile"), "race 到 age profile 应只保留 ID 关系。");
        _test.Eq(snapshot.Bloodlines["titan"].StageIds[0], new StringName("titan_awakened"), "bloodline stage 应通过 ID 连接。");
        _test.Eq(snapshot.AscensionStages["titan_avatar"].AscensionId, new StringName("titan_blood_ascension"), "ascension stage 应通过 ID 连接。");
    }

    private void TestEveryDomainImportsStrictPlainContent()
    {
        var reader = new GodotContentJsonSourceReader();
        AssertBatch(ProfessionIdentityJsonImport.CreateProfessionDescriptor(ProfessionIdentityJsonDomains.ProfessionDirectory, reader).Import(), 7, "professions");
        AssertBatch(ProfessionIdentityJsonImport.CreateRaceDescriptor(ProfessionIdentityJsonDomains.RaceDirectory, reader).Import(), 11, "races");
        AssertBatch(ProfessionIdentityJsonImport.CreateSubraceDescriptor(ProfessionIdentityJsonDomains.SubraceDirectory, reader).Import(), 31, "subraces");
        AssertBatch(ProfessionIdentityJsonImport.CreateFaithDescriptor(ProfessionIdentityJsonDomains.FaithDirectory, reader).Import(), 2, "faith");
        AssertBatch(ProfessionIdentityJsonImport.CreateAgeProfileDescriptor(ProfessionIdentityJsonDomains.AgeProfileDirectory, reader).Import(), 11, "age_profiles");
        AssertBatch(ProfessionIdentityJsonImport.CreateBloodlineDescriptor(ProfessionIdentityJsonDomains.BloodlineDirectory, reader).Import(), 3, "bloodlines");
        AssertBatch(ProfessionIdentityJsonImport.CreateAscensionDescriptor(ProfessionIdentityJsonDomains.AscensionDirectory, reader).Import(), 3, "ascensions");
        AssertBatch(ProfessionIdentityJsonImport.CreateStageAdvancementDescriptor(ProfessionIdentityJsonDomains.StageAdvancementDirectory, reader).Import(), 1, "stage_advancements");
    }

    private void TestClosedKindsAndUnknownMembersFailClosed()
    {
        const string unknownRaceField = """
        {"schema":1,"domain":"races","family":"test","templates":{},"entries":[{
          "race_id":"human","display_name":"Human","description":"Human","age_profile_id":"human_age_profile",
          "default_subrace_id":"common_human","subrace_ids":["common_human"],"body_size_category":"medium",
          "base_speed":6,"attribute_modifiers":[],"trait_ids":[],"racial_granted_skills":[],"proficiency_tags":[],
          "vision_tags":[],"save_advantage_tags":[],"save_disadvantage_tags":[],"save_immunity_tags":[],
          "damage_resistances":{},"dialogue_tags":[],"racial_trait_summary":[],"unexpected":true
        }]}
        """;
        ContentImportBatch<RaceImportModel> raceBatch = ProfessionIdentityJsonImport
            .CreateRaceDescriptor("res://fake/races", new FakeReader("race.json", unknownRaceField))
            .Import();
        _test.True(raceBatch.HasErrors, "strict race DTO 应拒绝额外字段。");
        _test.True(raceBatch.Diagnostics.Any(value => value.RuleId == "identity.json.dto.invalid"), "额外字段应给出稳定 strict DTO rule id。");

        const string unknownBloodlineKind = """
        {"schema":1,"domain":"bloodlines","family":"test","templates":{},"entries":[{
          "entry_id":"mystery","kind":"mystery","payload":{}
        }]}
        """;
        ContentImportBatch<BloodlineImportModel> bloodlineBatch = ProfessionIdentityJsonImport
            .CreateBloodlineDescriptor("res://fake/bloodlines", new FakeReader("bloodline.json", unknownBloodlineKind))
            .Import();
        _test.True(bloodlineBatch.HasErrors, "bloodline closed kind 应拒绝未知 kind。");
        _test.True(bloodlineBatch.Diagnostics.Any(value => value.RuleId == "identity.json.kind.unknown" && value.JsonPointer.EndsWith("/kind", StringComparison.Ordinal)), "未知 kind 诊断应定位 discriminator。");
    }

    private void TestSchemaAndOfflineRegistrationsAreComplete()
    {
        _test.Eq(ProfessionIdentityJsonDomains.SchemaRegistrations.Count, 8, "身份/成长切片应暴露 8 个唯一 schema registration。");
        IReadOnlyList<IContentJsonOfflineValidationDomain> offline = ProfessionIdentityJsonImport.CreateOfflineDomains().ToArray();
        _test.Eq(offline.Count, 8, "身份/成长切片应暴露 8 个 offline validation domain。");
        _test.Eq(offline.Select(value => value.DomainId).Distinct(StringComparer.Ordinal).Count(), 8, "offline domain id 必须唯一。");
    }

    private void AssertBatch<T>(ContentImportBatch<T> batch, int expectedCount, string domain)
        where T : notnull
    {
        _test.Eq(batch.Diagnostics.Count, 0, $"{domain} production JSON 应通过 strict/import validation。");
        _test.Eq(batch.Entries.Count, expectedCount, $"{domain} 应完整导入 canonical entry。");
    }

    private sealed class FakeReader : IContentJsonSourceReader
    {
        private readonly IReadOnlyList<ContentJsonSourceText> _sources;

        internal FakeReader(string fileName, string json)
        {
            _sources = new[] { new ContentJsonSourceText(fileName, json) };
        }

        public IReadOnlyList<ContentJsonSourceText> ReadUtf8Documents(string directoryPath) => _sources;
    }
}
