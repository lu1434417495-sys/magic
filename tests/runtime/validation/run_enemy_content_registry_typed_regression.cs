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
        TestScoreProfileProjectionPreservesTypedBehavior();
        TestClosedActionSpec();
        TestClosedActionImportRejections();
        TestSharedWolfEncounterBehavior();
        RequestTestExit(_test.Finish("Enemy JSON content registry regression"));
    }

    private void TestScoreProfileProjectionPreservesTypedBehavior()
    {
        var source = new BattleAiScoreProfileJsonDto
        {
            DamageWeight = 101,
            HealWeight = 102,
            StatusWeight = 103,
            TerrainWeight = 104,
            HeightWeight = 105,
            LethalTargetWeight = 106,
            LethalThreatTargetWeight = 107,
            TargetCountWeight = 108,
            FriendlyFireDamageWeight = 109,
            FriendlyFireTargetWeight = 110,
            FriendlyControlTargetWeight = 111,
            FriendlyLethalTargetWeight = 112,
            ApCostWeight = 113,
            MpCostWeight = 114,
            StaminaCostWeight = 115,
            AuraCostWeight = 116,
            CooldownWeight = 117,
            DelayedResolutionCostPer5Tu = 118,
            MovementCostWeight = 119,
            MpReserveFloorBp = 120,
            MpReservePressureWeight = 121,
            MpReserveBreachPenalty = 122,
            StaminaReserveFloorBp = 123,
            StaminaReservePressureWeight = 124,
            StaminaReserveBreachPenalty = 125,
            AuraReserveFloorBp = 126,
            AuraReservePressureWeight = 127,
            AuraReserveBreachPenalty = 128,
            ResourceConservationWeight = 129,
            PositionBaseScore = 130,
            PositionDistanceStep = 131,
            PositionUndershootPenalty = 132,
            PositionOvershootPenalty = 133,
            SurvivalMarginGainWeight = 134,
            PostActionThreatDamageWeight = 135,
            PostActionThreatCountWeight = 136,
            LethalSurvivalRiskPenalty = 137,
            IncomingThreatReliefWeight = 138,
            LowHpUrgencyThresholdBp = 139,
            LowHpUrgencyWeight = 140,
            ExecuteTargetHpThresholdBp = 141,
            ExecuteBonusWeight = 142,
            OverkillDamagePenaltyWeight = 143,
            RoleThreatMinEffectiveRange = 144,
            RoleThreatDistanceWindow = 145,
            RoleThreatMaxApproachDistance = 146,
            RoleThreatMaxContactRange = 147,
            RoleThreatInRangeScoreStep = 148,
            EnemyTargetCountWeight = 149,
            ChainEnemyTargetWeight = 150,
            FocusFireWoundedTargetWeight = 151,
            HitRateReliabilityWeight = 152,
            SaveReliableDamageWeight = 153,
            ShieldAbsorbedWeight = 154,
            ControlWeight = 155,
            GroundControlWeight = 156,
            StatusRedundancyPenalty = 157,
            PositionObjectiveWeight = 158,
            SafeDistanceAdherenceWeight = 159,
            ThreatHealerBiasBasisPoints = 160,
            ThreatControlBiasBasisPoints = 161,
            ThreatRangedBiasBasisPoints = 162,
            ThreatRangeStepBiasBasisPoints = 163,
            ThreatMultiplierCapBasisPoints = 164,
            MeteorHighPriorityThreatMultiplierBp = 165,
            MeteorHighPriorityDamageHpPercent = 166,
            MeteorHighPriorityTargetPriorityScore = 167,
            MeteorTopThreatRank = 168,
            MeteorFriendlyFireProfile = "reckless",
            MeteorFriendlyFireSoftExpectedHpPercent = 170,
            MeteorFriendlyFireHardExpectedHpPercent = 171,
            MeteorFriendlyFireHardWorstCaseHpPercent = 172,
            ActionBaseScores = new Dictionary<string, int>
            {
                ["skill"] = 173,
                ["move"] = 174,
            },
            DefaultBucketPriority = 175,
            BucketPriorities = new Dictionary<string, int> { ["pressure"] = 176 },
        };
        BattleAiScoreProfileDefinition definition =
            EnemyContentDefinitionProjector.ProjectScoreProfile(source);

        _test.Eq(definition.DamageWeight, source.DamageWeight, "damage weight 应保真。");
        _test.Eq(definition.HealWeight, source.HealWeight, "heal weight 应保真。");
        _test.Eq(definition.StatusWeight, source.StatusWeight, "status weight 应保真。");
        _test.Eq(definition.TerrainWeight, source.TerrainWeight, "terrain weight 应保真。");
        _test.Eq(definition.HeightWeight, source.HeightWeight, "height weight 应保真。");
        _test.Eq(definition.LethalTargetWeight, source.LethalTargetWeight, "lethal target weight 应保真。");
        _test.Eq(definition.LethalThreatTargetWeight, source.LethalThreatTargetWeight, "lethal threat target weight 应保真。");
        _test.Eq(definition.TargetCountWeight, source.TargetCountWeight, "target count weight 应保真。");
        _test.Eq(definition.FriendlyFireDamageWeight, source.FriendlyFireDamageWeight, "friendly-fire damage weight 应保真。");
        _test.Eq(definition.FriendlyFireTargetWeight, source.FriendlyFireTargetWeight, "friendly-fire target weight 应保真。");
        _test.Eq(definition.FriendlyControlTargetWeight, source.FriendlyControlTargetWeight, "friendly control target weight 应保真。");
        _test.Eq(definition.FriendlyLethalTargetWeight, source.FriendlyLethalTargetWeight, "friendly lethal target weight 应保真。");
        _test.Eq(definition.ApCostWeight, source.ApCostWeight, "AP cost weight 应保真。");
        _test.Eq(definition.MpCostWeight, source.MpCostWeight, "MP cost weight 应保真。");
        _test.Eq(definition.StaminaCostWeight, source.StaminaCostWeight, "stamina cost weight 应保真。");
        _test.Eq(definition.AuraCostWeight, source.AuraCostWeight, "aura cost weight 应保真。");
        _test.Eq(definition.CooldownWeight, source.CooldownWeight, "cooldown weight 应保真。");
        _test.Eq(definition.DelayedResolutionCostPer5Tu, source.DelayedResolutionCostPer5Tu, "delayed-resolution cost 应保真。");
        _test.Eq(definition.MovementCostWeight, source.MovementCostWeight, "movement cost weight 应保真。");
        _test.Eq(definition.MpReserveFloorBp, source.MpReserveFloorBp, "MP reserve floor 应保真。");
        _test.Eq(definition.MpReservePressureWeight, source.MpReservePressureWeight, "MP reserve pressure weight 应保真。");
        _test.Eq(definition.MpReserveBreachPenalty, source.MpReserveBreachPenalty, "MP reserve breach penalty 应保真。");
        _test.Eq(definition.StaminaReserveFloorBp, source.StaminaReserveFloorBp, "stamina reserve floor 应保真。");
        _test.Eq(definition.StaminaReservePressureWeight, source.StaminaReservePressureWeight, "stamina reserve pressure weight 应保真。");
        _test.Eq(definition.StaminaReserveBreachPenalty, source.StaminaReserveBreachPenalty, "stamina reserve breach penalty 应保真。");
        _test.Eq(definition.AuraReserveFloorBp, source.AuraReserveFloorBp, "aura reserve floor 应保真。");
        _test.Eq(definition.AuraReservePressureWeight, source.AuraReservePressureWeight, "aura reserve pressure weight 应保真。");
        _test.Eq(definition.AuraReserveBreachPenalty, source.AuraReserveBreachPenalty, "aura reserve breach penalty 应保真。");
        _test.Eq(definition.ResourceConservationWeight, source.ResourceConservationWeight, "resource conservation weight 应保真。");
        _test.Eq(definition.PositionBaseScore, source.PositionBaseScore, "position base score 应保真。");
        _test.Eq(definition.PositionDistanceStep, source.PositionDistanceStep, "position distance step 应保真。");
        _test.Eq(definition.PositionUndershootPenalty, source.PositionUndershootPenalty, "position undershoot penalty 应保真。");
        _test.Eq(definition.PositionOvershootPenalty, source.PositionOvershootPenalty, "position overshoot penalty 应保真。");
        _test.Eq(definition.SurvivalMarginGainWeight, source.SurvivalMarginGainWeight, "survival margin weight 应保真。");
        _test.Eq(definition.PostActionThreatDamageWeight, source.PostActionThreatDamageWeight, "post-action threat damage weight 应保真。");
        _test.Eq(definition.PostActionThreatCountWeight, source.PostActionThreatCountWeight, "post-action threat count weight 应保真。");
        _test.Eq(definition.LethalSurvivalRiskPenalty, source.LethalSurvivalRiskPenalty, "lethal survival risk penalty 应保真。");
        _test.Eq(definition.IncomingThreatReliefWeight, source.IncomingThreatReliefWeight, "incoming threat relief weight 应保真。");
        _test.Eq(definition.LowHpUrgencyThresholdBp, source.LowHpUrgencyThresholdBp, "low-HP urgency threshold 应保真。");
        _test.Eq(definition.LowHpUrgencyWeight, source.LowHpUrgencyWeight, "low-HP urgency weight 应保真。");
        _test.Eq(definition.ExecuteTargetHpThresholdBp, source.ExecuteTargetHpThresholdBp, "execute threshold 应保真。");
        _test.Eq(definition.ExecuteBonusWeight, source.ExecuteBonusWeight, "execute bonus weight 应保真。");
        _test.Eq(definition.OverkillDamagePenaltyWeight, source.OverkillDamagePenaltyWeight, "overkill penalty 应保真。");
        _test.Eq(definition.RoleThreatMinEffectiveRange, source.RoleThreatMinEffectiveRange, "role threat minimum range 应保真。");
        _test.Eq(definition.RoleThreatDistanceWindow, source.RoleThreatDistanceWindow, "role threat distance window 应保真。");
        _test.Eq(definition.RoleThreatMaxApproachDistance, source.RoleThreatMaxApproachDistance, "role threat approach distance 应保真。");
        _test.Eq(definition.RoleThreatMaxContactRange, source.RoleThreatMaxContactRange, "role threat contact range 应保真。");
        _test.Eq(definition.RoleThreatInRangeScoreStep, source.RoleThreatInRangeScoreStep, "role threat score step 应保真。");
        _test.Eq(definition.EnemyTargetCountWeight, source.EnemyTargetCountWeight, "enemy target count weight 应保真。");
        _test.Eq(definition.ChainEnemyTargetWeight, source.ChainEnemyTargetWeight, "chain enemy target weight 应保真。");
        _test.Eq(definition.FocusFireWoundedTargetWeight, source.FocusFireWoundedTargetWeight, "focus-fire wounded target weight 应保真。");
        _test.Eq(definition.HitRateReliabilityWeight, source.HitRateReliabilityWeight, "hit-rate reliability weight 应保真。");
        _test.Eq(definition.SaveReliableDamageWeight, source.SaveReliableDamageWeight, "save-reliable damage weight 应保真。");
        _test.Eq(definition.ShieldAbsorbedWeight, source.ShieldAbsorbedWeight, "shield absorbed weight 应保真。");
        _test.Eq(definition.ControlWeight, source.ControlWeight, "control weight 应保真。");
        _test.Eq(definition.GroundControlWeight, source.GroundControlWeight, "ground-control weight 应保真。");
        _test.Eq(definition.StatusRedundancyPenalty, source.StatusRedundancyPenalty, "status redundancy penalty 应保真。");
        _test.Eq(definition.PositionObjectiveWeight, source.PositionObjectiveWeight, "position objective weight 应保真。");
        _test.Eq(definition.SafeDistanceAdherenceWeight, source.SafeDistanceAdherenceWeight, "safe-distance weight 应保真。");
        _test.Eq(definition.ThreatHealerBiasBasisPoints, source.ThreatHealerBiasBasisPoints, "healer threat bias 应保真。");
        _test.Eq(definition.ThreatControlBiasBasisPoints, source.ThreatControlBiasBasisPoints, "control threat bias 应保真。");
        _test.Eq(definition.ThreatRangedBiasBasisPoints, source.ThreatRangedBiasBasisPoints, "ranged threat bias 应保真。");
        _test.Eq(definition.ThreatRangeStepBiasBasisPoints, source.ThreatRangeStepBiasBasisPoints, "range-step threat bias 应保真。");
        _test.Eq(definition.ThreatMultiplierCapBasisPoints, source.ThreatMultiplierCapBasisPoints, "threat multiplier cap 应保真。");
        _test.Eq(definition.MeteorHighPriorityThreatMultiplierBp, source.MeteorHighPriorityThreatMultiplierBp, "meteor threat multiplier 应保真。");
        _test.Eq(definition.MeteorHighPriorityDamageHpPercent, source.MeteorHighPriorityDamageHpPercent, "meteor damage threshold 应保真。");
        _test.Eq(definition.MeteorHighPriorityTargetPriorityScore, source.MeteorHighPriorityTargetPriorityScore, "meteor target priority 应保真。");
        _test.Eq(definition.MeteorTopThreatRank, source.MeteorTopThreatRank, "meteor top-threat rank 应保真。");
        _test.Eq(definition.MeteorFriendlyFireProfileKind, BattleAiMeteorFriendlyFireProfile.Reckless, "meteor friendly-fire profile 应投影为 typed kind。");
        _test.Eq(definition.MeteorFriendlyFireSoftExpectedHpPercent, source.MeteorFriendlyFireSoftExpectedHpPercent, "meteor soft friendly-fire threshold 应保真。");
        _test.Eq(definition.MeteorFriendlyFireHardExpectedHpPercent, source.MeteorFriendlyFireHardExpectedHpPercent, "meteor hard expected threshold 应保真。");
        _test.Eq(definition.MeteorFriendlyFireHardWorstCaseHpPercent, source.MeteorFriendlyFireHardWorstCaseHpPercent, "meteor hard worst-case threshold 应保真。");
        _test.Eq(definition.GetActionBaseScore("move"), 174, "action score 应通过 typed lookup 保真。");
        _test.Eq(definition.GetActionBaseScore("unknown"), 173, "未知 action 应回退到 skill score。");
        _test.Eq(definition.GetBucketPriority("pressure"), 176, "bucket priority 应通过 typed lookup 保真。");
        _test.Eq(definition.GetBucketPriority("unknown"), 175, "未知 bucket 应回退到默认优先级。");
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
        _test.Eq(
            wolf.Weapon.WeaponProfileKind,
            BattleUnitState.ToStringName(BattleWeaponProfileKind.Natural),
            "JSON beast template 应直接投影天生武器。"
        );
        _test.Eq(
            wolf.Weapon.WeaponPhysicalDamageTag,
            new StringName("physical_pierce"),
            "JSON beast template 应从 bite tag 推导穿刺伤害。"
        );
        _test.Eq(wolf.Weapon.WeaponOneHandedDice.DiceCount, 1, "天生武器应投影 1D6。");
        _test.Eq(wolf.Weapon.WeaponOneHandedDice.DiceSides, 6, "天生武器应投影 1D6。");
        _test.Eq(wolf.DerivedHpMax, 12, "JSON template 应直接计算 wolf_raider 派生 HP。");
        _test.Eq(wolf.DerivedAttackBonus, 1, "近战天生武器应按力量计算攻击加值。");

        EnemyTemplateDefinition goblinArcher = snapshot.EnemyTemplates["goblin_archer"];
        _test.Eq(
            goblinArcher.Weapon.WeaponProfileKind,
            BattleUnitState.ToStringName(BattleWeaponProfileKind.Equipped),
            "JSON non-beast template 应从 item Definition 投影装备武器。"
        );
        _test.Eq(
            goblinArcher.Weapon.WeaponItemId,
            new StringName("militia_light_crossbow"),
            "装备武器投影应保留稳定 item ID。"
        );
        _test.Eq(
            goblinArcher.DerivedAttackBonus,
            1,
            "远程装备武器应按感知计算攻击加值。"
        );
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
