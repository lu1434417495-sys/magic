using System;
using System.Reflection;
using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_battle_local_writeback_failure_boundary_regression
    : LifecycleTestSceneTree
{
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        AssertCandidateValidationStoresPlainFailure();
        AssertConflictFailureKeepsProjectionSchema();
        RequestTestExit(_test.Finish("Battle-local writeback failure boundary regression"));
    }

    private void AssertCandidateValidationStoresPlainFailure()
    {
        Type candidateResultType = typeof(GameRuntimeBattleWritebackService).GetNestedType(
            "BattleLocalCandidateValidationResult",
            BindingFlags.NonPublic
        );
        PropertyInfo failureProperty = candidateResultType?.GetProperty(
            "Failure",
            BindingFlags.Instance | BindingFlags.NonPublic
        );

        _test.True(candidateResultType != null, "候选校验结果类型应存在。");
        _test.Eq(
            failureProperty?.PropertyType,
            typeof(GameRuntimeBattleWritebackService.BattleLocalWritebackFailure),
            "候选校验失败应持有 plain failure DTO。"
        );
        _test.False(
            failureProperty?.PropertyType == typeof(GDictionary),
            "候选校验结果不得持久保存 Godot Dictionary wrapper。"
        );
    }

    private void AssertConflictFailureKeepsProjectionSchema()
    {
        StringName mainHand = EquipmentRules.ToStringName(EquipmentSlotKind.MainHand);
        EquipmentInstanceState equippedInstance = EquipmentInstanceState.CreateInstance(
            "iron_sword",
            "eq_writeback_conflict"
        );
        PartyMemberState member = new()
        {
            member_id = "hero",
            equipment_state = new EquipmentState(),
        };
        _test.True(
            member.equipment_state.SetEquippedEntry(
                mainHand,
                "iron_sword",
                new[] { mainHand },
                equippedInstance
            ),
            "回归前置：成员应持有测试装备实例。"
        );

        PartyState partyState = new();
        partyState.SetMemberState(member);
        WarehouseState backpack = new();
        backpack.AddEquipmentInstance(equippedInstance.DuplicateState());
        BattleState battleState = new();
        battleState.SetPartyBackpackView(backpack);

        using GameRuntimeBattleWritebackService service = new();
        try
        {
            GameRuntimeBattleWritebackService.BattleLocalWritebackResult result =
                service.CommitBattleLocalViewsToPartyStateTyped(battleState, partyState);

            _test.False(result.Ok, "背包与装备槽共享 instance_id 时写回应失败。");
            _test.Eq(
                result.ErrorCode,
                "battle_local_writeback_instance_conflict",
                "typed failure 应保留原有错误码。"
            );
            _test.False(
                (object)result.Details is GDictionary,
                "typed failure details 应保持 plain managed graph。"
            );
            _test.Eq(
                ReadPlainString(result.Details, "instance_id"),
                "eq_writeback_conflict",
                "plain details 应保留冲突实例。"
            );
            _test.Eq(
                ReadPlainString(result.Details, "owner"),
                "equipment:hero:main_hand",
                "plain details 应保留当前 owner。"
            );
            _test.Eq(
                ReadPlainString(result.Details, "previous_owner"),
                "backpack",
                "plain details 应保留先前 owner。"
            );

            LifecycleAuditSnapshot baseline = LifecycleAuditRegistry.Shared.CaptureSnapshot();
            using (
                GodotProjectionLease<GDictionary> lease =
                    GameRuntimeBattleWritebackProjection.ProjectLease(result)
            )
            {
                GDictionary payload = lease.Value;
                _test.False(payload["ok"].AsBool(), "投影 schema 应保留 ok=false。");
                _test.Eq(
                    payload["error_code"].AsString(),
                    "battle_local_writeback_instance_conflict",
                    "投影 schema 应保留错误码。"
                );
                using GDictionary details = payload["details"].AsGodotDictionary();
                _test.Eq(
                    details["instance_id"].AsString(),
                    "eq_writeback_conflict",
                    "投影 details 应保留冲突实例。"
                );
                _test.Eq(
                    LifecycleAuditRegistry.Shared.CaptureSnapshot().ActiveLeaseCount,
                    baseline.ActiveLeaseCount + 1,
                    "writeback failure 投影应只登记当前 root lease。"
                );
            }

            LifecycleAuditSnapshot drained = LifecycleAuditRegistry.Shared.CaptureSnapshot();
            _test.Eq(
                drained.ActiveLeaseCount,
                baseline.ActiveLeaseCount,
                "writeback failure 投影关闭后 lease 应回基线。"
            );
            _test.Eq(
                drained.ActiveOwnerCount,
                baseline.ActiveOwnerCount,
                "writeback failure 投影关闭后 owner 应回基线。"
            );
        }
        finally
        {
            BattleTestFixture.DisposeBattleState(battleState);
        }
    }

    private static string ReadPlainString(
        System.Collections.Generic.IReadOnlyDictionary<string, object> source,
        string key
    ) =>
        source != null && source.TryGetValue(key, out object value)
            ? value?.ToString() ?? ""
            : "";
}
