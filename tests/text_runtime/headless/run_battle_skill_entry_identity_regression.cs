using Godot;
using GDictionary = Godot.Collections.Dictionary;

public partial class run_battle_skill_entry_identity_regression : LifecycleTestSceneTree
{
    private const string SkillId = "archer_long_draw";
    private const string ExpectedEntryId = "known_skill:archer_long_draw";
    private const string SecondarySkillId = "basic_attack";
    private const string ExpectedSecondaryEntryId = "known_skill:basic_attack";
    private const string StaleEntryId = "known_skill:not_currently_available";

    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        RunAfterProcessStartup(Run);
    }

    private void Run()
    {
        TestKnownSkillSelectionCarriesStableEntryIdentity();

        RequestTestExit(_test.Finish("Battle skill entry identity regression"));
    }

    private void TestKnownSkillSelectionCarriesStableEntryIdentity()
    {
        GameTextCommandRunner runner = new();
        runner.initialize();
        try
        {
            AssertCommandOk(runner.ExecuteLine("game new test"), "game new test 应成功。");
            AssertCommandOk(runner.ExecuteLine("battle start settlement"), "battle start settlement 应成功。");
            AdvanceUntilBattleActive(runner);
            AssertCommandOk(runner.ExecuteLine("battle confirm"), "battle confirm 应成功。");
            AdvanceToManualBattleTurn(runner);

            HeadlessGameTestSession session = runner.GetSession();
            GameRuntimeFacade runtime = session?.GetRuntimeFacadeTyped();
            _test.True(runtime != null, "skill entry 回归应拿到 typed runtime。");
            if (runtime == null)
                return;

            BattleUnitState activeUnit = PrimeActiveManualKnownSkill(runtime);
            _test.True(activeUnit != null, "skill entry 回归应拿到当前手动单位。");
            if (activeUnit == null)
                return;

            AssertKnownSkillAvailabilityView(runtime, activeUnit);

            AssertCommandOk(runner.ExecuteLine("battle skill 1"), "battle skill 1 应选择已知技能。");

            _test.Eq(
                runtime.GetSelectedBattleSkillId(),
                new StringName(SkillId),
                "选择已知技能后应保留 catalog skill_id。"
            );
            _test.Eq(
                runtime.GetSelectedBattleSkillEntryId(),
                new StringName(ExpectedEntryId),
                "选择已知技能后应保存稳定 selected_skill_entry_id。"
            );

            BattleCommand previewCommand = BuildSelectedSkillPreviewCommand(runtime, activeUnit);
            _test.True(previewCommand != null, "应能构建已选技能 preview command。");
            if (previewCommand != null)
            {
                _test.Eq(
                    previewCommand.skill_entry_id,
                    new StringName(ExpectedEntryId),
                    "已选技能 command 应写入 skill_entry_id。"
                );
                _test.Eq(
                    previewCommand.skill_id,
                    new StringName(SkillId),
                    "已选技能 command 应继续写入 catalog skill_id。"
                );
            }

            using GodotProjectionLease<GDictionary> selectedSnapshotLease =
                session.BuildSnapshotLease();
            GDictionary battleSnapshot = Dict(selectedSnapshotLease.Value, "battle");
            _test.Eq(
                DictString(battleSnapshot, "selected_skill_entry_id"),
                ExpectedEntryId,
                "battle snapshot 应暴露 selected_skill_entry_id。"
            );
            _test.Eq(
                DictString(battleSnapshot, "selected_skill_id"),
                SkillId,
                "battle snapshot 应继续暴露 selected_skill_id。"
            );
            _test.True(
                HasTextLine(session.BuildTextSnapshot(), $"selected_skill_entry_id={ExpectedEntryId}"),
                "文本快照应在 selected_skill_id 旁暴露 selected_skill_entry_id。"
            );

            runtime.SetBattleSelectionTargetCoordsStateTyped(new[] { activeUnit.GetAnchorCoord() });
            runtime.SetBattleSelectionTargetUnitIdsStateTyped(new[] { activeUnit.unit_id });
            AssertCommandOk(runner.ExecuteLine("battle clear"), "battle clear 应成功。");

            _test.Eq(
                runtime.GetSelectedBattleSkillEntryId(),
                new StringName(),
                "清除技能选择应同时清空 selected_skill_entry_id。"
            );
            _test.Eq(
                runtime.GetSelectedBattleSkillId(),
                new StringName(),
                "清除技能选择应清空 selected_skill_id。"
            );
            _test.Eq(
                runtime.GetSelectedBattleSkillVariantId(),
                new StringName(),
                "清除技能选择应清空 selected_skill_variant_id。"
            );
            _test.Eq(
                runtime.GetBattleSelectionTargetCoordsStateTyped().Count,
                0,
                "清除技能选择应清空目标坐标队列。"
            );
            _test.Eq(
                runtime.GetBattleSelectionTargetUnitIdsStateTyped().Count,
                0,
                "清除技能选择应清空目标单位队列。"
            );

            runtime.SetBattleSelectionSkillEntryId(StaleEntryId);
            runtime.SetBattleSelectionSkillId(SkillId);
            runtime.SetBattleSelectionSkillVariantId("");
            BattleCommand stalePreviewCommand = BuildSelectedSkillPreviewCommand(runtime, activeUnit);
            _test.True(
                stalePreviewCommand == null,
                "stale selected_skill_entry_id 不应回退到 skill_id 构建 preview command。"
            );
        }
        finally
        {
            runner.Dispose(true);
        }
    }

    private void AssertKnownSkillAvailabilityView(
        GameRuntimeFacade runtime,
        BattleUnitState activeUnit
    )
    {
        BattleSkillAvailabilityService service = new(runtime.GetSkillCatalogTyped());
        BattleSkillAvailabilityView view = service.BuildView(
            new BattleSkillAvailabilityQuery
            {
                User = activeUnit,
                Consumer = BattleSkillAvailabilityConsumer.ManualSelection,
            }
        );

        _test.Eq(view.SkillEntries.Count, 2, "availability view 应保留已知主动技能数量。");
        AssertKnownEntry(view.SkillEntries[0], SkillId, ExpectedEntryId, 2);
        AssertKnownEntry(view.SkillEntries[1], SecondarySkillId, ExpectedSecondaryEntryId, 1);

        _test.True(
            view.TryResolveSkillEntry(new StringName(ExpectedEntryId), out BattleAvailableSkillEntry entry)
                && entry.EntryRef.SkillId == new StringName(SkillId),
            "availability view 应可按 known skill entry id 解析。"
        );
        _test.False(
            view.TryResolveSkillEntry(new StringName(StaleEntryId), out _),
            "availability view 应拒绝 stale/unknown entry id。"
        );
    }

    private void AssertKnownEntry(
        BattleAvailableSkillEntry entry,
        string skillId,
        string expectedEntryId,
        int expectedLevel
    )
    {
        _test.True(entry != null, $"{skillId} availability entry 不应为空。");
        if (entry == null)
            return;
        _test.Eq(
            entry.EntryRef.SkillEntryId,
            new StringName(expectedEntryId),
            $"{skillId} availability entry id 应稳定。"
        );
        _test.Eq(
            entry.EntryRef.SkillId,
            new StringName(skillId),
            $"{skillId} availability skill id 应保持 catalog id。"
        );
        _test.Eq(
            entry.EntryRef.SourceKind,
            BattleSkillEntrySourceKind.KnownSkill,
            $"{skillId} availability source kind 应为 known skill。"
        );
        _test.Eq(entry.SkillLevel, expectedLevel, $"{skillId} availability 应读取已知技能等级。");
        _test.True(entry.IsSelectable, $"{skillId} known entry 应可手动选择。");
        _test.True(
            entry.SkillDefinition != null && entry.SkillDefinition.SkillId == new StringName(skillId),
            $"{skillId} availability 应携带 catalog skill definition。"
        );
    }

    private BattleUnitState PrimeActiveManualKnownSkill(GameRuntimeFacade runtime)
    {
        BattleState battleState = runtime?.GetBattleState();
        if (battleState == null || battleState.IsEmpty() || battleState.active_unit_id == "")
            return null;
        BattleUnitState activeUnit = battleState.ContainsUnit(battleState.active_unit_id)
            ? battleState.GetUnit(battleState.active_unit_id)
            : null;
        if (activeUnit == null)
            return null;

        activeUnit.SetKnownActiveSkillIds(
            new[] { new StringName(SkillId), new StringName(SecondarySkillId) }
        );
        activeUnit.SetKnownSkillLevelsTyped(null);
        activeUnit.SetKnownSkillLevelTyped(SkillId, 2);
        activeUnit.SetKnownSkillLevelTyped(SecondarySkillId, 1);
        activeUnit.SetCurrentAp(2);
        activeUnit.SetCurrentStamina(50);
        activeUnit.SetCooldownsTyped(null);
        if (activeUnit.attribute_snapshot != null)
        {
            activeUnit.attribute_snapshot.SetValue("action_points", 2);
            activeUnit.attribute_snapshot.SetValue("stamina_max", 50);
        }
        runtime.CommandBattleClearSkillTyped();
        runtime.RefreshBattleSelectionState();
        return activeUnit;
    }

    private BattleCommand BuildSelectedSkillPreviewCommand(
        GameRuntimeFacade runtime,
        BattleUnitState activeUnit
    )
        => runtime._battle_selection.BuildSelectedSkillPreviewCommand(
            activeUnit,
            activeUnit.GetAnchorCoord()
        );

    private void AdvanceUntilBattleActive(GameTextCommandRunner runner, int maxTicks = 64)
    {
        for (int tick = 0; tick < maxTicks; tick++)
        {
            HeadlessGameTestSession session = runner.GetSession();
            if (session == null)
                break;
            bool battleActive;
            using (GodotProjectionLease<GDictionary> snapshotLease = session.BuildSnapshotLease())
                battleActive = DictBool(Dict(snapshotLease.Value, "battle"), "active");
            if (battleActive)
                return;
            runner.ExecuteLine("battle tick 1");
        }
        _test.Fail("skill entry 回归未能进入 active battle。");
    }

    private void AdvanceToManualBattleTurn(GameTextCommandRunner runner, int maxTicks = 64)
    {
        for (int tick = 0; tick < maxTicks; tick++)
        {
            HeadlessGameTestSession session = runner.GetSession();
            if (session == null)
                break;
            bool battleActive;
            bool manualTurn;
            using (GodotProjectionLease<GDictionary> snapshotLease = session.BuildSnapshotLease())
            {
                GDictionary battleSnapshot = Dict(snapshotLease.Value, "battle");
                battleActive = DictBool(battleSnapshot, "active");
                string activeUnitId = DictString(battleSnapshot, "active_unit_id");
                GDictionary activeUnit = FindBattleUnit(battleSnapshot, activeUnitId);
                manualTurn = DictString(activeUnit, "control_mode") == "manual";
            }
            if (!battleActive)
                break;
            if (manualTurn)
                return;
            AssertCommandOk(runner.ExecuteLine("battle tick 1"), "推进到手动回合的 battle tick 应成功。");
        }
        _test.Fail("skill entry 回归未能进入手动单位回合。");
    }

    private static GDictionary FindBattleUnit(GDictionary battleSnapshot, string unitId)
    {
        foreach (Variant unitValue in DictArray(battleSnapshot, "units"))
        {
            if (unitValue.VariantType != Variant.Type.Dictionary)
                continue;
            GDictionary unit = unitValue.AsGodotDictionary();
            if (DictString(unit, "unit_id") == unitId)
                return unit;
        }
        return new GDictionary();
    }

    private static bool HasTextLine(string text, string expected)
    {
        string normalized = (text ?? "").Replace("\r", "");
        foreach (string line in normalized.Split('\n'))
        {
            if (line == expected)
                return true;
        }
        return false;
    }

    private static GDictionary Dict(GDictionary dictionary, string key) =>
        dictionary != null && dictionary.ContainsKey(key)
            ? dictionary[key].AsGodotDictionary()
            : new GDictionary();

    private static Godot.Collections.Array DictArray(GDictionary dictionary, string key) =>
        dictionary != null && dictionary.ContainsKey(key)
            ? dictionary[key].AsGodotArray()
            : new Godot.Collections.Array();

    private static bool DictBool(GDictionary dictionary, string key) =>
        dictionary != null && dictionary.ContainsKey(key) && dictionary[key].AsBool();

    private static string DictString(GDictionary dictionary, string key) =>
        dictionary != null && dictionary.ContainsKey(key)
            ? dictionary[key].AsString()
            : "";

    private void AssertCommandOk(GameTextCommandResult result, string message)
    {
        _test.True(result != null && result.ok, $"{message} message={result?.message}");
    }
}
