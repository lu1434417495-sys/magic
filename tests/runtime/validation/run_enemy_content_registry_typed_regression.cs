using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class run_enemy_content_registry_typed_regression : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize() => RunAfterProcessStartup(Run);

    private void Run()
    {
        TestCodeOwnedJsonDiscoveryAndImmutableProjection();
        TestClosedActionSpec();
        TestClosedActionImportRejections();
        TestSharedWolfEncounterBehavior();
        RequestTestExit(_test.Finish("Enemy JSON content registry regression"));
    }

    private void TestCodeOwnedJsonDiscoveryAndImmutableProjection()
    {
        ContentSnapshot snapshot = GameSessionTestFactory.GetProcessSnapshot();
        _test.Eq(snapshot.EnemyBrains.Count, 9, "production 应从固定 JSON 目录发现 9 个 AI brain。");
        _test.Eq(snapshot.EnemyTemplates.Count, 40, "production 应从固定 JSON 目录发现 40 个 enemy template。");
        _test.Eq(snapshot.EncounterRosters.Count, 5, "production 应从固定 JSON 目录发现 5 个 roster。");
        _test.True(snapshot.EnemyTemplates.ContainsKey("red_dragon"), "JSON template 应包含红龙。");
        _test.True(snapshot.EnemyBrains.ContainsKey("dragon_tyrant"), "JSON brain 应包含 dragon_tyrant。");
        _test.True(snapshot.EnemyTemplates["red_dragon"].SkillIds.Contains(new StringName("dragon_frightful_presence")), "转换必须保留工作树中的红龙威压技能。");

        EnemyTemplateDefinition wolf = snapshot.EnemyTemplates["wolf_raider"];
        _test.Eq(wolf.BattleSpriteAssetId, new StringName("battle.unit.enemy.wolf"), "敌人贴图应发布稳定 asset ID。");
        _test.False(wolf.BattleSpriteAssetId.ToString().StartsWith("res://", StringComparison.Ordinal), "Definition 不得携带贴图路径。");
        _test.True(Throws<NotSupportedException>(() => ((IDictionary<StringName, EnemyTemplateDefinition>)snapshot.EnemyTemplates).Add("forbidden", wolf)), "enemy snapshot dictionary 应不可变。");
        _test.True(Throws<NotSupportedException>(() => ((IList<StringName>)wolf.Tags).Add("forbidden")), "enemy template nested list 应不可变。");

        var reader = new GodotContentJsonSourceReader();
        _test.Eq(EnemyContentJsonAuthoringDomains.CreateBrainDescriptor(EnemyContentJsonDomains.BrainDirectory, reader).Import().Entries.Count, 9, "brain JSON importer 应产出 9 个 plain import model。");
        _test.Eq(EnemyContentJsonAuthoringDomains.CreateTemplateDescriptor(EnemyContentJsonDomains.TemplateDirectory, reader).Import().Entries.Count, 40, "template JSON importer 应产出 40 个 DTO。");
        _test.Eq(EnemyContentJsonAuthoringDomains.CreateRosterDescriptor(EnemyContentJsonDomains.RosterDirectory, reader).Import().Entries.Count, 5, "roster JSON importer 应产出 5 个 DTO。");
    }

    private void TestClosedActionSpec()
    {
        var spec = new EnemyAiActionClosedKindSchemaSpec();
        _test.Eq(spec.Branches.Count, 12, "EnemyAiActionKind closed spec 必须完整登记 12 个分支。");
        var kinds = new HashSet<string>(StringComparer.Ordinal);
        foreach (ContentJsonSchemaClosedKindBranch branch in spec.Branches) kinds.Add(branch.Kind);
        _test.Eq(kinds.Count, 12, "closed action spec 的 kind 必须唯一。");
    }

    private void TestClosedActionImportRejections()
    {
        const string validWaitPayload =
            "{\"action_id\":\"wait\",\"score_bucket_id\":\"\",\"action_intent\":\"wait\","
            + "\"active_rest_action_base_score\":0,\"active_rest_min_stamina_residue\":0}";
        AssertBrainImportRejected(
            BrainDocument("unknown_kind", $"{{\"kind\":\"mystery\",\"payload\":{validWaitPayload}}}"),
            EnemyContentJsonRules.UnknownActionKind,
            "/kind",
            "未知 action kind 必须在 normalize 阶段 fail closed。"
        );
        AssertBrainImportRejected(
            BrainDocument(
                "mismatched_payload",
                "{\"kind\":\"wait\",\"payload\":{\"action_id\":\"cast\","
                    + "\"score_bucket_id\":\"test\",\"action_intent\":\"offense\","
                    + "\"skill_ids\":[\"basic_attack\"],\"target_selector\":\"nearest_enemy\","
                    + "\"minimum_effective_target_count\":1,\"maximum_friendly_fire_target_count\":0,"
                    + "\"allow_friendly_lethal\":false,\"desired_min_distance\":1,"
                    + "\"desired_max_distance\":1,\"distance_reference\":\"target_unit\"}}"
            ),
            EnemyContentJsonRules.InvalidActionPayload,
            "/payload",
            "kind 与 payload 形状错配必须由严格 DTO 拒绝。"
        );
        AssertBrainImportRejected(
            BrainDocument(
                "extra_payload_field",
                "{\"kind\":\"wait\",\"payload\":{\"action_id\":\"wait\","
                    + "\"score_bucket_id\":\"\",\"action_intent\":\"wait\","
                    + "\"active_rest_action_base_score\":0,\"active_rest_min_stamina_residue\":0,"
                    + "\"unexpected\":true}}"
            ),
            EnemyContentJsonRules.InvalidActionPayload,
            "/payload",
            "payload 额外字段必须由 JsonUnmappedMemberHandling.Disallow 拒绝。"
        );

        ContentImportBatch<EnemyAiBrainImportModel> invalidActionValues = ImportBrainDocument(
            BrainDocument(
                "invalid_action_values",
                "{\"kind\":\"use_unit_skill\",\"payload\":{\"action_id\":\"cast\","
                    + "\"score_bucket_id\":\"test\",\"action_intent\":\"offense\","
                    + "\"skill_ids\":[\"basic_attack\"],\"target_selector\":\"not_a_selector\","
                    + "\"minimum_effective_target_count\":1,\"maximum_friendly_fire_target_count\":0,"
                    + "\"allow_friendly_lethal\":false,\"desired_min_distance\":1,"
                    + "\"desired_max_distance\":1,\"distance_reference\":\"not_a_reference\"}}"
            )
        );
        AssertRejectedPointer(invalidActionValues, "/target_selector", "非法 selector 必须在发布前被拒绝。");
        AssertRejectedPointer(invalidActionValues, "/distance_reference", "非法 distance reference 必须在发布前被拒绝。");

        const string invalidFamilySlot =
            "[{\"slot_id\":\"offense\",\"slot_role\":\"offense\",\"order\":0,"
            + "\"allowed_affordances\":[\"unit_hostile.damage\"],"
            + "\"action_families\":[\"not_a_family\"],\"style_template_action_id\":\"\","
            + "\"score_bucket_id\":\"\",\"target_selector\":\"\","
            + "\"desired_min_distance\":-1,\"desired_max_distance\":-1,"
            + "\"distance_reference\":\"\",\"suppression_policy\":\"suppress_matching_family\"}]";
        ContentImportBatch<EnemyAiBrainImportModel> invalidFamily = ImportBrainDocument(
            BrainDocument(
                "invalid_action_family",
                $"{{\"kind\":\"wait\",\"payload\":{validWaitPayload}}}",
                invalidFamilySlot
            )
        );
        AssertRejectedPointer(invalidFamily, "/action_families/0", "非法 action family 必须在发布前被拒绝。");
    }

    private void AssertBrainImportRejected(
        string json,
        string ruleId,
        string pointerSuffix,
        string message
    )
    {
        ContentImportBatch<EnemyAiBrainImportModel> batch = ImportBrainDocument(json);
        _test.Eq(batch.Entries.Count, 0, message);
        _test.True(
            batch.Diagnostics.Any(diagnostic =>
                diagnostic.RuleId == ruleId
                && diagnostic.JsonPointer.Contains(pointerSuffix, StringComparison.Ordinal)
            ),
            message
        );
    }

    private void AssertRejectedPointer(
        ContentImportBatch<EnemyAiBrainImportModel> batch,
        string pointerSuffix,
        string message
    )
    {
        _test.Eq(batch.Entries.Count, 0, message);
        _test.True(
            batch.Diagnostics.Any(diagnostic =>
                diagnostic.RuleId == EnemyContentImportRules.ValueUnsupported
                && diagnostic.JsonPointer.EndsWith(pointerSuffix, StringComparison.Ordinal)
            ),
            message
        );
    }

    private static ContentImportBatch<EnemyAiBrainImportModel> ImportBrainDocument(string json) =>
        EnemyContentJsonAuthoringDomains
            .CreateBrainDescriptor(
                "res://tests/fixtures/enemy_json",
                new FakeSourceReader(new ContentJsonSourceText("fixture.json", json))
            )
            .Import();

    private static string BrainDocument(
        string brainId,
        string action,
        string generationSlots = "[]"
    ) =>
        "{\"schema\":1,\"domain\":\"enemy_ai_brains\",\"family\":\"fixture\","
        + "\"templates\":{},\"entries\":[{"
        + $"\"brain_id\":\"{brainId}\",\"default_state_id\":\"idle\",\"score_profile\":null,"
        + "\"states\":[{\"state_id\":\"idle\","
        + $"\"actions\":[{action}],\"generation_slots\":{generationSlots}}}],"
        + "\"transition_rules\":[]}]}";

    private sealed class FakeSourceReader : IContentJsonSourceReader
    {
        private readonly IReadOnlyList<ContentJsonSourceText> _sources;

        internal FakeSourceReader(params ContentJsonSourceText[] sources) => _sources = sources;

        public IReadOnlyList<ContentJsonSourceText> ReadUtf8Documents(string directoryPath) =>
            _sources;
    }

    private void TestSharedWolfEncounterBehavior()
    {
        ContentSnapshot snapshot = GameSessionTestFactory.GetProcessSnapshot();
        _test.True(snapshot.EncounterRosters.TryGetValue("wolf_pack_skirmish", out WildEncounterRosterDefinition roster), "共享荒狼 roster 应继续发布。");
        if (roster == null) return;
        _test.Eq(roster.GetStageUnitEntries(0).Count, 1, "stage 0 应保留单一狼群条目。");
        _test.True(snapshot.BattleEncounters.ContainsKey("wolf_wilds"), "依赖 roster 的 battle encounter 应继续发布。");
    }

    private static bool Throws<TException>(Action action) where TException : Exception
    {
        try { action(); return false; }
        catch (TException) { return true; }
    }
}
