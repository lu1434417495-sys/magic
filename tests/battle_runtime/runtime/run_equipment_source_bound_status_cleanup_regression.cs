using System;
using System.Collections.Generic;
using Godot;

// §8.7：opt-in source-bound buff 清除。
// 覆盖：authoring→校验→immutable 投影链携带 remove_on_source_deactivated；
// apply_status 执行时记录 typed provenance（source kind + effective key +
// binding/action id）；source 刷新（换装拆套跨阈值）只清除声明 opt-in 且
// provenance 精确匹配失效 source 的 status；未声明/同名其他来源/其他单位的
// status 不受影响；失败换装不留半清状态；duplicate/preview/AI mutation-exact
// 保留 provenance。
public partial class run_equipment_source_bound_status_cleanup_regression : LifecycleTestSceneTree
{
    private static readonly StringName BodyItemId = "item.test.boil_body";
    private static readonly StringName HeadItemId = "item.test.boil_head";
    private static readonly StringName BodyInstanceId = "eq_test_boil_body";
    private static readonly StringName HeadInstanceId = "eq_test_boil_head";
    private static readonly StringName GearSetId = "gear_set.test.boil";
    private static readonly StringName SetTraitId = "trait.test.set_boil";
    private static readonly StringName BindingId = "binding.test.set_boil";
    private static readonly StringName DerivedBindingId = "binding.test.derived_marker";
    private static readonly StringName ActivationStatusId = "test_source_bound_activation";
    private static readonly StringName OptInStatusId = "test_set_boil_bound_buff";
    private static readonly StringName PlainStatusId = "test_set_boil_plain_buff";
    private static readonly StringName SharedStatusId = "test_set_boil_shared_buff";
    private static readonly StringName OptInActionId = "action.test.apply_bound_buff";
    private static readonly StringName PlainActionId = "action.test.apply_plain_buff";
    private static readonly StringName TestSkillId = "test_source_bound_skill";
    private static readonly StringName GearSetSourceKindName = "player_persistent_gear_set_threshold";
    private static readonly StringName EquipmentSourceKindName = "player_persistent_equipment";
    private static readonly StringName DerivedSourceKindName = "battle_status_derived";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestAuthoringProjectionCarriesOptInFlag();
        TestApplyStatusRecordsTypedProvenance();
        TestDuplicatePreviewAndMutationExactPreserveProvenance();
        TestOptInStatusRemovedWhenGearSetSourceDies();
        TestNonOptInStatusSurvivesSourceRemoval();
        TestSameStatusFromOtherSourceSurvives();
        TestProvenanceMatchRequiresExactKindKeyAndBinding();
        TestSourceDefinitionStackingKeepsOtherSourceContribution();
        TestFailedChangeEquipmentLeavesBuffAndSourceIntact();
        TestSuccessfulUnequipCrossingThresholdClearsAtomically();

        RequestTestExit(_test.Finish("Equipment source-bound status cleanup regression"));
    }

    private void TestAuthoringProjectionCarriesOptInFlag()
    {
        using var registry = new EquipmentAbilityContentRegistry();

        EquipmentAbilityRegistryBuildResult optIn = registry.Rebuild(
            new[] { BuildAuthoringPack(removeOnSourceDeactivated: true) },
            BuildValidationContext()
        );
        _test.True(
            optIn.Success,
            $"声明 remove_on_source_deactivated 的 pack 应通过校验：{FormatErrors(optIn.Errors)}"
        );
        ApplyStatusActionPayloadDefinition optInPayload = ReadFirstApplyStatusPayload(
            registry,
            BindingId
        );
        _test.True(
            optInPayload?.RemoveOnSourceDeactivated == true,
            "immutable definition 应携带 remove_on_source_deactivated=true。"
        );

        EquipmentAbilityRegistryBuildResult defaulted = registry.Rebuild(
            new[] { BuildAuthoringPack(removeOnSourceDeactivated: false) },
            BuildValidationContext()
        );
        _test.True(
            defaulted.Success,
            $"未声明 remove_on_source_deactivated 的 pack 应继续通过校验：{FormatErrors(defaulted.Errors)}"
        );
        ApplyStatusActionPayloadDefinition defaultPayload = ReadFirstApplyStatusPayload(
            registry,
            BindingId
        );
        _test.True(
            defaultPayload != null && !defaultPayload.RemoveOnSourceDeactivated,
            "未声明时 remove_on_source_deactivated 应投影为默认 false。"
        );
    }

    private void TestApplyStatusRecordsTypedProvenance()
    {
        using GearSetFixture fixture = GearSetFixture.Create();
        fixture.ApplyBuffsViaAttackHit();

        BattleStatusEffectState optIn = fixture.Holder.GetStatusEffect(OptInStatusId);
        _test.True(optIn != null, "命中后 opt-in buff 应施加到持有者。");
        if (optIn == null)
            return;
        _test.True(
            optIn.remove_on_source_deactivated,
            "opt-in buff 应记录 remove_on_source_deactivated=true。"
        );
        _test.Eq(
            optIn.source_provenance_unit_id,
            fixture.Holder.unit_id,
            "provenance 应记录来源单位 id。"
        );
        _test.Eq(
            optIn.source_provenance_source_kind,
            GearSetSourceKindName,
            "provenance 应记录 gear-set threshold source kind。"
        );
        _test.Eq(
            optIn.source_provenance_effective_key,
            fixture.GearSetSource()?.EffectiveInstanceKey ?? "<missing>",
            "provenance 应记录投影来源的 effective source key。"
        );
        _test.Eq(
            optIn.source_provenance_binding_id,
            BindingId,
            "provenance 应记录 binding id。"
        );
        _test.Eq(
            optIn.source_provenance_action_id,
            OptInActionId,
            "provenance 应记录 action id。"
        );

        BattleStatusEffectState plain = fixture.Holder.GetStatusEffect(PlainStatusId);
        _test.True(
            plain != null && !plain.remove_on_source_deactivated,
            "未声明的 buff 不应携带 remove_on_source_deactivated。"
        );
        _test.Eq(
            plain?.source_provenance_binding_id ?? "<missing>",
            BindingId,
            "未声明的 buff 仍应记录 typed provenance 供诊断。"
        );
    }

    private void TestDuplicatePreviewAndMutationExactPreserveProvenance()
    {
        BattleStatusEffectState status = BuildProvenancedStatus(
            "unit_x",
            GearSetSourceKindName,
            "gear_set::gear_set.test.boil::threshold.2::trait.test.set_boil",
            BindingId,
            OptInActionId,
            optIn: true
        );

        BattleStatusEffectState duplicate = status.DuplicateState();
        AssertProvenanceEqual(duplicate, status, "DuplicateState");
        duplicate.source_provenance_binding_id = "binding.test.mutated";
        _test.Eq(
            status.source_provenance_binding_id,
            BindingId,
            "DuplicateState 后修改副本不得影响原 status。"
        );

        BattleStatusEffectState exact = status.DuplicateForMutationSnapshotExact();
        AssertProvenanceEqual(exact, status, "DuplicateForMutationSnapshotExact");

        // preview candidate 路径：preview branch 通过整单位 DuplicateState 克隆 status。
        using GearSetFixture fixture = GearSetFixture.Create();
        fixture.ApplyBuffsViaAttackHit();
        BattleUnitState previewClone = fixture.Holder.DuplicateForPreview();
        BattleStatusEffectState clonedStatus = previewClone?.GetStatusEffect(OptInStatusId);
        _test.True(
            clonedStatus != null
                && clonedStatus.remove_on_source_deactivated
                && clonedStatus.source_provenance_binding_id == BindingId
                && clonedStatus.source_provenance_source_kind == GearSetSourceKindName,
            "preview/AI 单位级克隆必须保留 source-bound provenance。"
        );

        // AI stable projection / mutation-exact：provenance 必须进入 stable map 且可检出差异。
        StableMap baselineStable = BattleAiMutationStableProjection.StableStatusEffect(status);
        List<StableDiff> duplicateDiffs = new();
        BattleAiMutationGuard.CollectDiffs(
            baselineStable,
            BattleAiMutationStableProjection.StableStatusEffect(status.DuplicateState()),
            "status_duplicate",
            duplicateDiffs
        );
        _test.Eq(
            duplicateDiffs.Count,
            0,
            "DuplicateState 后的 AI stable projection 应无 provenance 差异。"
        );
        BattleStatusEffectState mutated = status.DuplicateState();
        mutated.source_provenance_effective_key = "gear_set::other::key";
        List<StableDiff> mutationDiffs = new();
        BattleAiMutationGuard.CollectDiffs(
            baselineStable,
            BattleAiMutationStableProjection.StableStatusEffect(mutated),
            "status_provenance",
            mutationDiffs
        );
        _test.True(
            mutationDiffs.Count > 0,
            "mutation-exact guard 必须能检出 provenance effective key 差异。"
        );
    }

    private void TestOptInStatusRemovedWhenGearSetSourceDies()
    {
        using GearSetFixture fixture = GearSetFixture.Create();
        fixture.ApplyBuffsViaAttackHit();
        _test.True(
            fixture.GearSetSource() != null,
            "测试前提：两件套应投影 gear-set threshold source。"
        );

        fixture.Holder.GetEquipmentView().PopEquippedInstance("head");
        IReadOnlyList<StringName> changedUnitIds = fixture.RefreshHolder();

        _test.True(
            fixture.GearSetSource() == null,
            "拆套后 gear-set threshold source 应消失。"
        );
        _test.False(
            fixture.Holder.HasStatusEffect(OptInStatusId),
            "声明 opt-in 的 buff 应在 source 失效的同一重投影中被清除。"
        );
        _test.True(
            ContainsUnitId(changedUnitIds, fixture.Holder.unit_id),
            "source-bound 清除应把持有者加入 changed unit ids。"
        );
    }

    private void TestNonOptInStatusSurvivesSourceRemoval()
    {
        using GearSetFixture fixture = GearSetFixture.Create();
        fixture.ApplyBuffsViaAttackHit();

        fixture.Holder.GetEquipmentView().PopEquippedInstance("head");
        fixture.RefreshHolder();

        _test.True(
            fixture.Holder.HasStatusEffect(PlainStatusId),
            "未声明 opt-in 的同名 source buff 在 source 失效后应保留（凤凰范式不变）。"
        );
    }

    private void TestSameStatusFromOtherSourceSurvives()
    {
        using DirectFixture fixture = DirectFixture.Create();
        // A、B 两单位持有同名 status；A 的 provenance 指向 A 已失效的 source，
        // B 的 provenance 指向 B 仍活跃的 source。
        fixture.AttachSource(fixture.UnitA, "key_a", BindingId);
        fixture.AttachSource(fixture.UnitB, "key_b", BindingId);
        fixture.UnitA.SetStatusEffect(
            BuildProvenancedStatus(
                fixture.UnitA.unit_id,
                EquipmentSourceKindName,
                "key_a",
                BindingId,
                OptInActionId,
                optIn: true,
                statusId: SharedStatusId
            )
        );
        fixture.UnitB.SetStatusEffect(
            BuildProvenancedStatus(
                fixture.UnitB.unit_id,
                EquipmentSourceKindName,
                "key_b",
                BindingId,
                OptInActionId,
                optIn: true,
                statusId: SharedStatusId
            )
        );

        // A 的 source 消失（换成不含 key_a 的投影）。
        fixture.AttachSource(fixture.UnitA, "key_a_replacement", "binding.test.unrelated");
        fixture.Cleanup(fixture.UnitA);

        _test.False(
            fixture.UnitA.HasStatusEffect(SharedStatusId),
            "A 的 opt-in status provenance 匹配失效 source，应被清除。"
        );
        _test.True(
            fixture.UnitB.HasStatusEffect(SharedStatusId),
            "同名 status 在 B 身上由仍活跃 source 持有，不得被误删。"
        );

        fixture.Cleanup(fixture.UnitB);
        _test.True(
            fixture.UnitB.HasStatusEffect(SharedStatusId),
            "B 的 source 仍活跃时清理不得移除 B 的 status。"
        );
    }

    private void TestProvenanceMatchRequiresExactKindKeyAndBinding()
    {
        using DirectFixture fixture = DirectFixture.Create();
        fixture.AttachSource(fixture.UnitA, "key_a", BindingId);

        // 1) kind 不匹配：同 key 同 binding 但 provenance kind 是 gear-set。
        fixture.UnitA.SetStatusEffect(
            BuildProvenancedStatus(
                fixture.UnitA.unit_id,
                GearSetSourceKindName,
                "key_a",
                BindingId,
                OptInActionId,
                optIn: true,
                statusId: "test_prov_kind_mismatch"
            )
        );
        // 2) key 不匹配：provenance 指向不存在的 effective key。
        fixture.UnitA.SetStatusEffect(
            BuildProvenancedStatus(
                fixture.UnitA.unit_id,
                EquipmentSourceKindName,
                "key_gone",
                BindingId,
                OptInActionId,
                optIn: true,
                statusId: "test_prov_key_mismatch"
            )
        );
        // 3) binding 不在 source 的 ability 列表内。
        fixture.UnitA.SetStatusEffect(
            BuildProvenancedStatus(
                fixture.UnitA.unit_id,
                EquipmentSourceKindName,
                "key_a",
                "binding.test.unrelated",
                OptInActionId,
                optIn: true,
                statusId: "test_prov_binding_mismatch"
            )
        );
        // 4) 精确匹配：source 仍活跃 → 保留。
        fixture.UnitA.SetStatusEffect(
            BuildProvenancedStatus(
                fixture.UnitA.unit_id,
                EquipmentSourceKindName,
                "key_a",
                BindingId,
                OptInActionId,
                optIn: true,
                statusId: "test_prov_exact"
            )
        );
        // 5) 未 opt-in：即使 provenance source 失效也保留。
        fixture.UnitA.SetStatusEffect(
            BuildProvenancedStatus(
                fixture.UnitA.unit_id,
                EquipmentSourceKindName,
                "key_gone",
                BindingId,
                OptInActionId,
                optIn: false,
                statusId: "test_prov_not_optin"
            )
        );
        // 6) 其他单位的 provenance：清理 A 时不得触碰。
        fixture.UnitA.SetStatusEffect(
            BuildProvenancedStatus(
                fixture.UnitB.unit_id,
                EquipmentSourceKindName,
                "key_gone",
                BindingId,
                OptInActionId,
                optIn: true,
                statusId: "test_prov_other_unit"
            )
        );
        // 7) battle_status_derived：activation status 仍在即视为活跃。
        fixture.UnitA.SetStatusEffect(
            new BattleStatusEffectState
            {
                status_id = ActivationStatusId,
                source_unit_id = fixture.UnitA.unit_id,
                stacks = 1,
                duration = 60,
            }
        );
        fixture.UnitA.SetStatusEffect(
            BuildProvenancedStatus(
                fixture.UnitA.unit_id,
                DerivedSourceKindName,
                "battle_status:test",
                DerivedBindingId,
                OptInActionId,
                optIn: true,
                statusId: "test_prov_derived_alive"
            )
        );

        fixture.Cleanup(fixture.UnitA);

        _test.False(
            fixture.UnitA.HasStatusEffect("test_prov_kind_mismatch"),
            "kind 不匹配（即使 key/binding 相同）不得视为活跃 source。"
        );
        _test.False(
            fixture.UnitA.HasStatusEffect("test_prov_key_mismatch"),
            "effective key 不匹配时应清除 opt-in status。"
        );
        _test.False(
            fixture.UnitA.HasStatusEffect("test_prov_binding_mismatch"),
            "binding 不在活跃 source 的 ability 列表内时应清除。"
        );
        _test.True(
            fixture.UnitA.HasStatusEffect("test_prov_exact"),
            "kind + key + binding 精确匹配活跃 source 的 status 应保留。"
        );
        _test.True(
            fixture.UnitA.HasStatusEffect("test_prov_not_optin"),
            "未声明 opt-in 的 status 不得因 source 失效被清除。"
        );
        _test.True(
            fixture.UnitA.HasStatusEffect("test_prov_other_unit"),
            "provenance 属于其他单位的 status 不得在本次清理中被触碰。"
        );
        _test.True(
            fixture.UnitA.HasStatusEffect("test_prov_derived_alive"),
            "battle_status_derived provenance 在 activation status 存活时应保留。"
        );

        fixture.UnitA.EraseStatusEffect(ActivationStatusId);
        fixture.Cleanup(fixture.UnitA);
        _test.False(
            fixture.UnitA.HasStatusEffect("test_prov_derived_alive"),
            "activation status 消失后 derived provenance 的 opt-in status 应被清除。"
        );
    }

    private void TestSourceDefinitionStackingKeepsOtherSourceContribution()
    {
        using DirectFixture fixture = DirectFixture.Create();
        fixture.AttachSource(fixture.UnitA, "key_a", BindingId);

        // source-definition 叠加语义：status 同时持有技能来源 contribution，
        // equipment 来源失效只摘除 equipment 份额，不抹掉整个 status。
        var status = BuildProvenancedStatus(
            fixture.UnitA.unit_id,
            EquipmentSourceKindName,
            "key_gone",
            BindingId,
            OptInActionId,
            optIn: true,
            statusId: "test_prov_stacked"
        );
        status.SetSourceContributionTyped(
            new BattleStatusSourceContributionState
            {
                Identity = BattleStatusSourceIdentity.Skill(fixture.UnitA.unit_id, TestSkillId),
                Power = 2,
                Stacks = 3,
                DurationTu = 90,
            }
        );
        status.RebuildSourceContributionAggregateTyped();
        fixture.UnitA.SetStatusEffect(status);

        fixture.Cleanup(fixture.UnitA);

        BattleStatusEffectState surviving = fixture.UnitA.GetStatusEffect("test_prov_stacked");
        _test.True(
            surviving != null,
            "仍有其他来源 contribution 的 status 不得被整体删除。"
        );
        _test.Eq(
            surviving?.stacks ?? 0,
            3,
            "技能来源 contribution 的层数应保留。"
        );
        _test.True(
            surviving != null && !surviving.remove_on_source_deactivated,
            "equipment 份额摘除后 stale provenance 应被清空。"
        );
    }

    private void TestFailedChangeEquipmentLeavesBuffAndSourceIntact()
    {
        using GearSetFixture fixture = GearSetFixture.Create();
        fixture.ApplyBuffsViaAttackHit();
        fixture.ActivateHolder(ap: 3);

        BattleEventBatch batch = fixture.Runtime.IssueCommand(
            BuildUnequipCommand(fixture.Holder.unit_id, "head", "eq_wrong_instance")
        );
        _test.True(batch != null, "失败换装仍应返回事件批次。");
        _test.Eq(
            fixture.Holder.GetCurrentAp(),
            3,
            "失败换装不得扣 AP。"
        );
        _test.Eq(
            fixture.Holder.GetEquipmentView().GetEquippedInstanceId("head"),
            HeadInstanceId,
            "失败换装不得改动 battle-local 装备 view。"
        );
        _test.True(
            fixture.GearSetSource() != null,
            "失败换装不得清除仍活跃的 gear-set source。"
        );
        _test.True(
            fixture.Holder.HasStatusEffect(OptInStatusId),
            "失败换装不得留下半清状态：opt-in buff 应完整保留。"
        );
    }

    private void TestSuccessfulUnequipCrossingThresholdClearsAtomically()
    {
        using GearSetFixture fixture = GearSetFixture.Create();
        fixture.ApplyBuffsViaAttackHit();
        fixture.ActivateHolder(ap: 3);

        BattleEventBatch batch = fixture.Runtime.IssueCommand(
            BuildUnequipCommand(fixture.Holder.unit_id, "head", HeadInstanceId)
        );
        _test.True(batch != null, "成功换装应返回事件批次。");
        _test.Eq(
            fixture.Holder.GetCurrentAp(),
            1,
            "成功换装应扣 2 AP。"
        );
        _test.True(
            fixture.GearSetSource() == null,
            "卸下套装件后 gear-set source 应在同一命令内关闭。"
        );
        _test.False(
            fixture.Holder.HasStatusEffect(OptInStatusId),
            "跨过套装阈值时 opt-in buff 清除应与 source 关闭原子发生。"
        );
        _test.True(
            fixture.Holder.HasStatusEffect(PlainStatusId),
            "同一命令内未声明 opt-in 的 buff 应保留。"
        );
    }

    private static bool ContainsUnitId(
        IEnumerable<StringName> unitIds,
        StringName expected
    )
    {
        foreach (StringName unitId in unitIds ?? Array.Empty<StringName>())
        {
            if (unitId == expected)
                return true;
        }
        return false;
    }

    private static BattleStatusEffectState BuildProvenancedStatus(
        StringName sourceUnitId,
        StringName sourceKind,
        StringName effectiveKey,
        StringName bindingId,
        StringName actionId,
        bool optIn,
        StringName statusId = default
    )
    {
        return new BattleStatusEffectState
        {
            status_id = statusId == default || statusId == "" ? OptInStatusId : statusId,
            source_unit_id = sourceUnitId,
            stacks = 1,
            duration = 120,
            remove_on_source_deactivated = optIn,
            source_provenance_unit_id = sourceUnitId,
            source_provenance_source_kind = sourceKind,
            source_provenance_effective_key = effectiveKey,
            source_provenance_binding_id = bindingId,
            source_provenance_action_id = actionId,
        };
    }

    private void AssertProvenanceEqual(
        BattleStatusEffectState actual,
        BattleStatusEffectState expected,
        string lane
    )
    {
        _test.True(actual != null, $"{lane} 应返回非空 status。");
        if (actual == null)
            return;
        _test.Eq(
            actual.remove_on_source_deactivated,
            expected.remove_on_source_deactivated,
            $"{lane} 应保留 remove_on_source_deactivated。"
        );
        _test.Eq(
            actual.source_provenance_unit_id,
            expected.source_provenance_unit_id,
            $"{lane} 应保留 provenance unit id。"
        );
        _test.Eq(
            actual.source_provenance_source_kind,
            expected.source_provenance_source_kind,
            $"{lane} 应保留 provenance source kind。"
        );
        _test.Eq(
            actual.source_provenance_effective_key,
            expected.source_provenance_effective_key,
            $"{lane} 应保留 provenance effective key。"
        );
        _test.Eq(
            actual.source_provenance_binding_id,
            expected.source_provenance_binding_id,
            $"{lane} 应保留 provenance binding id。"
        );
        _test.Eq(
            actual.source_provenance_action_id,
            expected.source_provenance_action_id,
            $"{lane} 应保留 provenance action id。"
        );
    }

    private static EquipmentAbilityContentPackImportModel BuildAuthoringPack(
        bool removeOnSourceDeactivated
    )
    {
        return new EquipmentAbilityContentPackImportModel
        {
            pack_id = "pack.test.source_bound_status",
            schema_version = 1,
            load_order = 10,
            bindings = new[]
            {
                new EquipmentAbilityBindingImportModel
                {
                    binding_id = BindingId.ToString(),
                    trait_id = SetTraitId.ToString(),
                    override_mode = "add",
                    allowed_source_kinds = new[] { "gear_set_threshold" },
                    reactions = new[]
                    {
                        new EquipmentAbilityReactionImportModel
                        {
                            reaction_id = "reaction.test.source_bound",
                            trigger = "on_attack_hit",
                            timing = "after_hit",
                            actions = new[]
                            {
                                new EquipmentAbilityActionImportModel
                                {
                                    action_id = OptInActionId.ToString(),
                                    kind = "apply_status",
                                    payload = new ApplyStatusActionPayloadImportModel
                                    {
                                        target_selector = "source",
                                        status_id = OptInStatusId.ToString(),
                                        duration_tu = 180,
                                        stack_delta = 1,
                                        remove_on_source_deactivated = removeOnSourceDeactivated,
                                    },
                                },
                            },
                        },
                    },
                },
            },
        };
    }

    private static EquipmentAbilityContentValidationContext BuildValidationContext()
    {
        return new EquipmentAbilityContentValidationContext
        {
            KnownTraitIds = new HashSet<StringName> { SetTraitId },
            KnownSkillDefinitions = new Dictionary<StringName, SkillDefinition> { ["known_skill"] = TestSkillDefinitionProjection.BuildSkill("known_skill") },
            KnownStatusIds = new HashSet<StringName> { OptInStatusId },
        };
    }

    private static ApplyStatusActionPayloadDefinition ReadFirstApplyStatusPayload(
        EquipmentAbilityContentRegistry registry,
        StringName bindingId
    )
    {
        EquipmentAbilityBindingDefinition definition =
            registry.GetBindingDefinitionsTyped()[bindingId];
        foreach (
            EquipmentAbilityReactionDefinition reaction
            in definition?.Reactions ?? Array.Empty<EquipmentAbilityReactionDefinition>()
        )
        {
            foreach (
                EquipmentAbilityActionDefinition action
                in reaction?.Actions ?? Array.Empty<EquipmentAbilityActionDefinition>()
            )
            {
                if (action?.PayloadDefinition is ApplyStatusActionPayloadDefinition payload)
                    return payload;
            }
        }
        return null;
    }

    private static string FormatErrors(IEnumerable<string> errors)
    {
        List<string> values = new();
        foreach (string error in errors ?? Array.Empty<string>())
            values.Add(error ?? "");
        return values.Count == 0 ? "[]" : $"[{string.Join(" | ", values)}]";
    }

    private static BattleCommand BuildUnequipCommand(
        StringName unitId,
        StringName slotId,
        StringName instanceId
    )
    {
        return new BattleCommand
        {
            command_type = BattleTypedNames.ToStringName(BattleCommandKind.ChangeEquipment),
            unit_id = unitId,
            target_unit_id = unitId,
            equipment_operation = BattleTypedNames.ToStringName(BattleEquipmentOperationKind.Unequip),
            equipment_slot_id = slotId,
            equipment_instance_id = instanceId,
        };
    }

    // 真实 2 件套 gear-set fixture：成员装备 → 有效 trait → gear-set threshold
    // source 投影 → on_attack_hit 施加 opt-in/plain 双 buff → 换装重投影。
    private sealed class GearSetFixture : IDisposable
    {
        private readonly CharacterManagementModule _characterManagement;
        private bool _disposed;

        private GearSetFixture(
            CharacterManagementModule characterManagement,
            BattleRuntimeModule runtime,
            FixedRollDamageResolver resolver,
            BattleState state,
            BattleUnitState holder,
            BattleUnitState enemy
        )
        {
            _characterManagement = characterManagement;
            Runtime = runtime;
            Resolver = resolver;
            State = state;
            Holder = holder;
            Enemy = enemy;
        }

        internal BattleRuntimeModule Runtime { get; }
        internal FixedRollDamageResolver Resolver { get; }
        internal BattleState State { get; }
        internal BattleUnitState Holder { get; }
        internal BattleUnitState Enemy { get; }

        internal static GearSetFixture Create()
        {
            ContentSnapshot snapshot = GameSessionTestFactory.GetProcessSnapshot();
            PartyState partyState = BuildPartyState("hero");
            PartyMemberState member = partyState.GetMemberState("hero");
            member.equipment_state = new EquipmentState();
            member.equipment_state.SetEquippedEntry(
                "body",
                BodyItemId,
                new[] { new StringName("body") },
                EquipmentInstanceState.CreateInstance(BodyItemId, BodyInstanceId)
            );
            member.equipment_state.SetEquippedEntry(
                "head",
                HeadItemId,
                new[] { new StringName("head") },
                EquipmentInstanceState.CreateInstance(HeadItemId, HeadInstanceId)
            );

            var itemDefs = new Dictionary<StringName, ItemDefinition>
            {
                [BodyItemId] = BuildArmorItem(BodyItemId, "body"),
                [HeadItemId] = BuildArmorItem(HeadItemId, "head"),
            };
            var traitDefs = new Dictionary<StringName, TraitDefinition>
            {
                [SetTraitId] = BuildSetTraitDefinition(),
            };
            var gearSetDefs = new Dictionary<StringName, GearSetDefinition>
            {
                [GearSetId] = BuildGearSetDefinition(),
            };
            var bindings = new Dictionary<StringName, EquipmentAbilityBindingDefinition>
            {
                [BindingId] = BuildSetBindingDefinition(),
            };

            var characterManagement = new CharacterManagementModule();
            characterManagement.setup(
                partyState,
                snapshot.Skills,
                snapshot.Professions,
                new Dictionary<StringName, AchievementDefinition>(),
                itemDefs,
                new Dictionary<StringName, QuestDefinition>(),
                traitDefs,
                null,
                new ProgressionIdentityCatalogData(),
                gearSetDefs
            );
            var runtime = new BattleRuntimeModule();
            runtime.setup(
                characterManagement,
                snapshot.Skills,
                item_defs: itemDefs,
                trait_defs: traitDefs,
                equipment_ability_bindings: bindings
            );
            var resolver = new FixedRollDamageResolver();
            BattleTestFixture.ConfigureDamageResolverForTests(runtime, resolver);

            IReadOnlyList<BattleUnitState> units = runtime._unit_factory.BuildAllyUnits(
                partyState,
                null
            );
            if (units.Count != 1)
                throw new InvalidOperationException("gear-set fixture 应只构建一个友方单位。");
            BattleUnitState holder = units[0];
            holder.faction_id = "ally";
            PrimeUnit(holder, hp: 80, maxHp: 100, ap: 3, new Vector2I(2, 2));
            BattleUnitState enemy = BattleTestFixture.BuildUnit(
                "source_bound_enemy",
                "enemy",
                new Vector2I(4, 2)
            );
            BattleState state = BattleTestFixture.BuildFlatState(
                "source_bound_status_cleanup",
                new Vector2I(7, 7)
            );
            BattleTestFixture.InstallUnits(state, new[] { holder }, new[] { enemy });
            runtime.SetupStateForTests(state);
            return new GearSetFixture(characterManagement, runtime, resolver, state, holder, enemy);
        }

        internal void ApplyBuffsViaAttackHit()
        {
            using var reactionBatch = new BattleEventBatch();
            BattleReactionRootTestHelper.ExecuteLogicalAttack(
                Runtime, reactionBatch, Holder, new[]
                {
                    TestSkillDefinitionProjection.BuildEffect("damage", damageTag: "fire", power: 4),
                },
                actionContext => Resolver.ResolveAttackEffects(
                    Holder,
                    Enemy,
                    new[]
                {
                    TestSkillDefinitionProjection.BuildEffect("damage", damageTag: "fire", power: 4),
                },
                    new AttackCheckInput(forceHitNoCrit: true, skillId: TestSkillId),
                    new AttackContext
                {
                    Action = actionContext,
                    EventBatch = reactionBatch,
                    DamageOriginKind = BattleDamageOriginKind.MainDirectEffect,
                    BattleState = State,
                    SkillId = TestSkillId,
                }
                )
            );
        }

        internal BattleEquipmentAbilitySourceReadView GearSetSource()
        {
            foreach (
                BattleEquipmentAbilitySourceReadView source
                in Holder.GetEquipmentAbilitySourcesReadViewTyped()
            )
            {
                if (source?.SourceKind == EquipmentAbilitySourceKind.PlayerPersistentGearSetThreshold)
                    return source;
            }
            return null;
        }

        internal IReadOnlyList<StringName> RefreshHolder() =>
            Runtime._unit_factory.RefreshEquipmentProjection(Holder);

        internal void ActivateHolder(int ap)
        {
            State.PhaseKind = BattlePhaseKind.UnitActing;
            State.active_unit_id = Holder.unit_id;
            Holder.SetCurrentAp(ap);
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            BattleTestFixture.DisposeBattleFixture(Runtime, State);
            _characterManagement?.Dispose();
        }

        private static void PrimeUnit(
            BattleUnitState unit,
            int hp,
            int maxHp,
            int ap,
            Vector2I coord
        )
        {
            unit.attribute_snapshot.SetValue(AttributeService.HP_MAX, maxHp);
            unit.SetCombatResources(hp, mp: 30, stamina: 30, aura: 0, ap, movePoints: 4);
            unit.SetAnchorCoord(coord);
        }

        private static PartyState BuildPartyState(StringName memberId)
        {
            var party = new PartyState();
            var member = new PartyMemberState
            {
                member_id = memberId,
                display_name = memberId.ToString(),
                progression = new UnitProgress
                {
                    unit_id = memberId,
                    display_name = memberId.ToString(),
                    unit_base_attributes = new UnitBaseAttributes(),
                },
                equipment_state = new EquipmentState(),
            };
            member.progression.unit_base_attributes.custom_stats["storage_space"] = 4;
            party.SetMemberState(member);
            party.active_member_ids.Add(memberId);
            party.leader_member_id = memberId;
            return party;
        }

        private static ItemDefinition BuildArmorItem(StringName itemId, string slotId) =>
            new TestItemDefinitionBuilder
            {
                item_id = itemId,
                display_name = itemId.ToString(),
                CategoryKind = ItemCategoryKind.Equipment,
                EquipmentTypeKind = ItemEquipmentTypeKind.Armor,
                is_stackable = false,
                max_stack = 1,
                equipment_slot_ids = new Godot.Collections.Array<string> { slotId },
            }.ToDefinition();

        private static TraitDefinition BuildSetTraitDefinition() =>
            new(
                SetTraitId,
                "Test Set Boil",
                "Fixture gear-set threshold trait.",
                new[] { new StringName("gear_set_bonus"), new StringName("equipment_ability") },
                new[] { new StringName("gear_set_threshold") },
                "equipment_ability",
                "passive",
                "unique_by_trait",
                "none",
                "none",
                "",
                0,
                0,
                Array.Empty<AttributeModifierDefinition>(),
                Array.Empty<StringName>(),
                Array.Empty<StringName>(),
                Array.Empty<StringName>(),
                Array.Empty<TraitDamageResistanceEntryDefinition>(),
                Array.Empty<TraitSaveBonusEntryDefinition>(),
                Array.Empty<TraitSaveTagBonusEntryDefinition>(),
                Array.Empty<TraitPassiveStatusEffectDefinition>(),
                Array.Empty<TraitRollValueSchemaEntryDefinition>()
            );

        private static GearSetDefinition BuildGearSetDefinition() =>
            new(
                GearSetId,
                "Test Boil Set",
                "Fixture gear set.",
                new[] { BodyItemId, HeadItemId },
                BodyItemId,
                new[]
                {
                    new GearSetThresholdDefinition(
                        "threshold.2",
                        2,
                        "2pc",
                        "Fixture 2-piece threshold.",
                        Array.Empty<StringName>(),
                        Array.Empty<AttributeModifierDefinition>(),
                        new[] { SetTraitId }
                    ),
                }
            );

        private static EquipmentAbilityBindingDefinition BuildSetBindingDefinition() =>
            new()
            {
                BindingId = BindingId,
                TraitId = SetTraitId,
                AllowedSourceKinds = new HashSet<StringName> { "gear_set_threshold" },
                RequiredTraitCategories = new HashSet<StringName> { "equipment_ability" },
                Reactions = new[]
                {
                    new EquipmentAbilityReactionDefinition
                    {
                        ReactionId = "reaction.test.set_boil",
                        Trigger = EquipmentAbilityTriggerKind.OnAttackHit,
                        Timing = EquipmentAbilityTimingKind.AfterHit,
                        Actions = new EquipmentAbilityActionDefinition[]
                        {
                            new()
                            {
                                ActionId = OptInActionId,
                                Kind = "apply_status",
                                PayloadDefinition = new ApplyStatusActionPayloadDefinition
                                {
                                    TargetSelector = "source",
                                    StatusId = OptInStatusId,
                                    DurationTu = 180,
                                    StackDelta = 1,
                                    RemoveOnSourceDeactivated = true,
                                },
                            },
                            new()
                            {
                                ActionId = PlainActionId,
                                Kind = "apply_status",
                                PayloadDefinition = new ApplyStatusActionPayloadDefinition
                                {
                                    TargetSelector = "source",
                                    StatusId = PlainStatusId,
                                    DurationTu = 180,
                                    StackDelta = 1,
                                },
                            },
                        },
                    },
                },
            };
    }

    // 轻量 fixture：手工挂接投影 source + 手工构造 provenance status，
    // 直接驱动 ClearSourceBoundStatusesForRemovedEquipmentSources 做精确匹配矩阵。
    private sealed class DirectFixture : IDisposable
    {
        private DirectFixture(BattleRuntimeModule runtime, BattleState state)
        {
            Runtime = runtime;
            State = state;
        }

        internal BattleRuntimeModule Runtime { get; }
        internal BattleState State { get; }
        internal BattleUnitState UnitA { get; private set; }
        internal BattleUnitState UnitB { get; private set; }

        internal static DirectFixture Create()
        {
            var bindings = new Dictionary<StringName, EquipmentAbilityBindingDefinition>
            {
                [BindingId] = new EquipmentAbilityBindingDefinition
                {
                    BindingId = BindingId,
                    TraitId = SetTraitId,
                },
                [DerivedBindingId] = new EquipmentAbilityBindingDefinition
                {
                    BindingId = DerivedBindingId,
                    TraitId = SetTraitId,
                    ActivationStatusId = ActivationStatusId,
                },
                ["binding.test.unrelated"] = new EquipmentAbilityBindingDefinition
                {
                    BindingId = "binding.test.unrelated",
                    TraitId = SetTraitId,
                },
            };
            var runtime = new BattleRuntimeModule();
            runtime.setup(equipment_ability_bindings: bindings);
            BattleState state = BattleTestFixture.BuildFlatState(
                "source_bound_status_direct",
                new Vector2I(5, 5)
            );
            var fixture = new DirectFixture(runtime, state)
            {
                UnitA = BattleTestFixture.BuildUnit("unit_a", "ally", new Vector2I(1, 1)),
                UnitB = BattleTestFixture.BuildUnit("unit_b", "ally", new Vector2I(3, 3)),
            };
            BattleTestFixture.InstallUnits(
                state,
                new[] { fixture.UnitA, fixture.UnitB },
                Array.Empty<BattleUnitState>()
            );
            runtime.SetupStateForTests(state);
            return fixture;
        }

        internal void AttachSource(
            BattleUnitState unit,
            StringName effectiveKey,
            params StringName[] bindingIds
        )
        {
            unit.ReplaceEquipmentAbilityProjectionTyped(
                new[]
                {
                    new BattleEquipmentAbilitySourceState
                    {
                        EffectiveInstanceKey = effectiveKey,
                        EquipmentDefId = "item.test.direct",
                        SourceEquipmentInstanceId = new StringName($"inst_{effectiveKey}"),
                        SourceKind = EquipmentAbilitySourceKind.PlayerPersistentEquipment,
                        AbilityIds = new List<StringName>(bindingIds),
                    },
                },
                temporalProgressModifiers: null
            );
        }

        internal IReadOnlyList<StringName> Cleanup(BattleUnitState sourceUnit) =>
            Runtime
                .GetEquipmentAbilityRuntimeService()
                .ClearSourceBoundStatusesForRemovedEquipmentSources(State, sourceUnit);

        public void Dispose()
        {
            BattleTestFixture.DisposeBattleFixture(Runtime, State);
        }
    }
}
