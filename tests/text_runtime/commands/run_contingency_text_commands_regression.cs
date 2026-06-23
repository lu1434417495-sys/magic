using System;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;
using GStringNameArray = Godot.Collections.Array<Godot.StringName>;

public partial class run_contingency_text_commands_regression : SceneTree
{
    private static readonly StringName GemId = "special_contingency_gem";
    private readonly TestHarness _test = new();

    public override void _Initialize()
    {
        var runner = new GameTextCommandRunner();
        runner.initialize();

        RunCommand(runner, "game new test");
        InstallGemItemDef(runner);

        string missingSkillMemberId = PrepareContingencyMemberFixture(
            runner,
            "player_sword_missing_skill",
            includeMirrorImage: false
        );
        GameTextCommandResult missingSkillSave = RunCommandExpectFail(
            runner,
            $"{CommandPrefix(missingSkillMemberId)} save {missingSkillMemberId} hp_mirror_self"
        );
        AssertLastResult(missingSkillSave.snapshot, missingSkillMemberId, "hp_mirror_self", ok: false, "missing_required_skill", charged: false, reservedMpMax: 0, materialQuantity: 0);
        AssertNoSavedSetup(missingSkillSave.snapshot, missingSkillMemberId, "missing required skill should not save a contingency setup.");

        string memberId = PrepareContingencyMemberFixture(runner);
        RunCommand(runner, $"{CommandPrefix(memberId)} status {memberId}");
        AssertEmptyStatusSnapshot(runner.GetSession().BuildSnapshot(), memberId);

        GameTextCommandResult saveResult = RunCommand(
            runner,
            $"{CommandPrefix(memberId)} save {memberId} hp_mirror_self"
        );
        AssertLastResult(saveResult.snapshot, memberId, "hp_mirror_self", ok: true, "ok", charged: false, reservedMpMax: 0, materialQuantity: 0);
        AssertSavedUnchargedStatus(saveResult.snapshot, saveResult.snapshot_text, memberId);

        GameTextCommandResult chargeResult = RunCommand(
            runner,
            $"{CommandPrefix(memberId)} charge {memberId} hp_mirror_self"
        );
        AssertLastResult(chargeResult.snapshot, memberId, "hp_mirror_self", ok: true, "ok", charged: true, reservedMpMax: 6, materialQuantity: 1);
        AssertChargedStatus(chargeResult.snapshot, chargeResult.snapshot_text, memberId);
        AssertWarehouseQuantity(chargeResult.snapshot, 0, "charge should deduct one contingency gem.");

        GameTextCommandResult editResult = RunCommandExpectFail(
            runner,
            $"{CommandPrefix(memberId)} edit {memberId} hp_mirror_self"
        );
        AssertLastResult(editResult.snapshot, memberId, "hp_mirror_self", ok: false, "setup_charged", charged: true, reservedMpMax: 6, materialQuantity: 1);
        AssertChargedStatus(editResult.snapshot, editResult.snapshot_text, memberId);
        AssertWarehouseQuantity(editResult.snapshot, 0, "charged edit failure should not refund contingency gem.");

        GameTextCommandResult clearResult = RunCommand(
            runner,
            $"{CommandPrefix(memberId)} clear {memberId} hp_mirror_self"
        );
        AssertLastResult(clearResult.snapshot, memberId, "hp_mirror_self", ok: true, "ok", charged: false, reservedMpMax: 0, materialQuantity: 0);
        AssertClearedStatus(clearResult.snapshot, clearResult.snapshot_text, memberId);
        AssertWarehouseQuantity(clearResult.snapshot, 0, "clear should not refund contingency gem.");

        runner.GetSession().GetGameSessionTyped().SetBattleSaveLock(true);
        GameTextCommandResult lockedSave = RunCommandExpectFail(
            runner,
            $"{CommandPrefix(memberId)} save {memberId} hp_mirror_self"
        );
        AssertLastResult(lockedSave.snapshot, memberId, "hp_mirror_self", ok: false, "battle_mutation_blocked", charged: false, reservedMpMax: 0, materialQuantity: 0);
        GameTextCommandResult lockedCharge = RunCommandExpectFail(
            runner,
            $"{CommandPrefix(memberId)} charge {memberId} hp_mirror_self"
        );
        AssertLastResult(lockedCharge.snapshot, memberId, "hp_mirror_self", ok: false, "battle_mutation_blocked", charged: false, reservedMpMax: 0, materialQuantity: 0);
        runner.GetSession().GetGameSessionTyped().SetBattleSaveLock(false);

        runner.Dispose(true);
        GodotSharpCleanup.CollectPendingFinalizers();
        Quit(_test.Finish("Contingency text commands regression"));
    }

    private static string CommandPrefix(string memberId) => "party contingency";

    private void InstallGemItemDef(GameTextCommandRunner runner)
    {
        GameSession gameSession = runner.GetSession().GetGameSessionTyped();
        _test.True(gameSession != null, "contingency text fixture requires a GameSession.");
        if (gameSession == null)
            return;
        _test.Eq(
            (Error)gameSession.InstallTestContentDef("item", GemId, BuildGemItemDef()),
            Error.Ok,
            "contingency text fixture should install the contingency gem item def."
        );
    }

    private string PrepareContingencyMemberFixture(
        GameTextCommandRunner runner,
        string memberId = "player_sword_01",
        bool includeChainContingency = true,
        bool includeMirrorImage = true
    )
    {
        GameRuntimeFacade runtime = runner.GetSession().GetRuntimeFacadeTyped();
        _test.True(runtime != null, "contingency text fixture requires a loaded runtime.");
        PartyState partyState = BuildPartyState(
            memberId,
            includeChainContingency,
            includeMirrorImage
        );
        SeedGemStack(partyState, 1);
        runner.GetSession().GetGameSessionTyped().SetPartyState(partyState);
        runtime.SetPartyState(partyState);
        runtime.SyncPartyStateServices();
        return memberId;
    }

    private static PartyState BuildPartyState(
        string memberId,
        bool includeChainContingency,
        bool includeMirrorImage
    )
    {
        PartyState partyState = new()
        {
            leader_member_id = memberId,
            main_character_member_id = memberId,
            active_member_ids = new GStringNameArray { memberId },
            warehouse_state = new WarehouseState(),
        };
        PartyMemberState member = new()
        {
            member_id = memberId,
            display_name = "Contingency Tester",
            current_hp = 20,
            current_mp = 30,
            current_aura = 0,
            progression = BuildProgress(memberId, includeChainContingency, includeMirrorImage),
        };
        partyState.SetMemberState(member);
        return partyState;
    }

    private static UnitProgress BuildProgress(
        string memberId,
        bool includeChainContingency,
        bool includeMirrorImage
    )
    {
        UnitProgress progress = new()
        {
            unit_id = memberId,
            display_name = "Contingency Tester",
        };
        progress.unit_base_attributes.SetAttributeValue(AttributeService.HP_MAX, 20);
        progress.unit_base_attributes.SetAttributeValue(AttributeService.MP_MAX, 30);
        progress.unit_base_attributes.SetAttributeValue(AttributeService.AURA_MAX, 0);
        progress.unit_base_attributes.SetAttributeValue(PartyWarehouseService.StorageSpaceAttributeId, 12);
        if (includeChainContingency)
            progress.SetSkillProgress(LearnedSkill("mage_chain_contingency", 5));
        if (includeMirrorImage)
            progress.SetSkillProgress(LearnedSkill("mage_mirror_image", 2));
        return progress;
    }

    private static void SeedGemStack(PartyState partyState, int quantity)
    {
        partyState.warehouse_state ??= new WarehouseState();
        partyState.warehouse_state.AddStack(
            new WarehouseStackState
            {
                item_id = GemId,
                quantity = quantity,
            }
        );
    }

    private void AssertEmptyStatusSnapshot(GDictionary snapshot, string memberId)
    {
        GDictionary status = MemberStatus(snapshot, memberId);
        _test.Eq(DictString(status, "member_id"), memberId, "status command should create member contingency status.");
        _test.Eq(DictInt(status, "setup_count", -1), 0, "status before save should report zero setups.");
    }

    private void AssertSavedUnchargedStatus(GDictionary snapshot, string textSnapshot, string memberId)
    {
        GDictionary status = MemberStatus(snapshot, memberId);
        _test.Eq(DictInt(status, "setup_count", -1), 1, "save should create one setup.");
        GDictionary setup = FirstSetup(status);
        _test.Eq(DictString(setup, "setup_id"), "hp_mirror_self", "save should write hp_mirror_self setup id.");
        _test.Eq(DictString(setup, "display_name"), "濒死镜影", "save should write stable display name.");
        _test.False(DictBool(setup, "charged", true), "saved setup should be uncharged.");
        _test.Eq(DictInt(setup, "reserved_mp_max", -1), 0, "saved setup should reserve no MP.");
        _test.Eq(DictInt(setup, "material_quantity", -1), 0, "saved setup should show zero material receipt.");
        _test.Eq(DictString(Dict(setup, "trigger"), "type"), "hp_below_percent", "saved setup should expose trigger type.");
        _test.Eq(DictInt(Dict(setup, "trigger"), "percent", -1), 30, "saved setup should expose trigger percent.");
        _test.Eq(DictString(setup, "release_mode"), "burst_release", "saved setup should expose release mode.");
        GDictionary spell = FirstDictionary(ArrayValue(setup, "stored_spells"));
        _test.Eq(DictString(spell, "stored_skill_id"), "mage_mirror_image", "saved setup should expose stored spell id.");
        _test.Eq(DictInt(spell, "cast_level", -1), 2, "saved setup should cap mirror image at learned level 2.");
        _test.Eq(DictInt(spell, "order", -1), 1, "saved setup should expose stored spell order.");
        _test.Eq(DictString(Dict(spell, "target_resolver"), "type"), "self", "saved setup should expose target resolver.");
        _test.True(textSnapshot.Contains($"member_contingency={memberId} | hp_mirror_self"), "text snapshot should render compact contingency status.");
        _test.True(textSnapshot.Contains("charged=no"), "text snapshot should render uncharged state.");
        _test.True(textSnapshot.Contains("trigger=hp_below_percent:30"), "text snapshot should render trigger summary.");
        _test.True(textSnapshot.Contains("spells=mage_mirror_image@2:self"), "text snapshot should render stored spell summary.");
    }

    private void AssertChargedStatus(GDictionary snapshot, string textSnapshot, string memberId)
    {
        GDictionary setup = FirstSetup(MemberStatus(snapshot, memberId));
        _test.True(DictBool(setup, "charged", false), "charged status should report charged=true.");
        _test.Eq(DictInt(setup, "reserved_mp_max", -1), 6, "charged status should reserve matrix_load * 2 MP.");
        _test.Eq(DictInt(setup, "material_quantity", -1), 1, "charged status should show one gem receipt.");
        _test.True(textSnapshot.Contains("charged=yes"), "text snapshot should render charged state.");
        _test.True(textSnapshot.Contains("reserved_mp_max=6"), "text snapshot should render reserved MP.");
        _test.True(textSnapshot.Contains("material=special_contingency_gem:1"), "text snapshot should render material receipt.");
    }

    private void AssertClearedStatus(GDictionary snapshot, string textSnapshot, string memberId)
    {
        GDictionary setup = FirstSetup(MemberStatus(snapshot, memberId));
        _test.False(DictBool(setup, "charged", true), "clear status should report charged=false.");
        _test.Eq(DictInt(setup, "reserved_mp_max", -1), 0, "clear status should remove MP reservation.");
        _test.Eq(DictInt(setup, "material_quantity", -1), 0, "clear status should remove material receipt.");
        _test.True(textSnapshot.Contains("charged=no"), "text snapshot should render cleared uncharged state.");
        _test.True(textSnapshot.Contains("material=special_contingency_gem:0"), "text snapshot should render cleared material receipt.");
    }

    private void AssertNoSavedSetup(GDictionary snapshot, string memberId, string message)
    {
        GDictionary status = MemberStatus(snapshot, memberId);
        _test.Eq(DictInt(status, "setup_count", -1), 0, message);
    }

    private void AssertLastResult(
        GDictionary snapshot,
        string memberId,
        string setupId,
        bool ok,
        string reasonId,
        bool charged,
        int reservedMpMax,
        int materialQuantity
    )
    {
        GDictionary result = Dict(Dict(snapshot, "party"), "contingency_last_result");
        _test.Eq(DictBool(result, "ok", !ok), ok, "last contingency result ok mismatch.");
        _test.Eq(DictString(result, "reason_id"), reasonId, "last contingency result reason mismatch.");
        _test.Eq(DictString(result, "member_id"), memberId, "last contingency result member mismatch.");
        _test.Eq(DictString(result, "setup_id"), setupId, "last contingency result setup mismatch.");
        _test.Eq(DictBool(result, "charged", !charged), charged, "last contingency result charged mismatch.");
        _test.Eq(DictInt(result, "reserved_mp_max", -1), reservedMpMax, "last contingency result reserved MP mismatch.");
        _test.Eq(DictString(result, "material_item_id"), GemId.ToString(), "last contingency result material id mismatch.");
        _test.Eq(DictInt(result, "material_quantity", -1), materialQuantity, "last contingency result material quantity mismatch.");
    }

    private void AssertWarehouseQuantity(GDictionary snapshot, int expected, string message)
    {
        int actual = 0;
        foreach (Variant entryValue in ArrayValue(Dict(Dict(snapshot, "warehouse"), "window_data"), "entries"))
        {
            GDictionary entry = entryValue.AsGodotDictionary();
            if (DictString(entry, "item_id") == GemId.ToString())
                actual += DictInt(entry, "total_quantity");
        }
        _test.Eq(actual, expected, message);
    }

    private GameTextCommandResult RunCommand(GameTextCommandRunner runner, string commandText)
    {
        GameTextCommandResult result = runner.ExecuteLine(commandText);
        if (result.skipped)
            return result;
        if (!result.ok)
        {
            GD.Print(result.Render());
            _test.Fail($"命令失败：{commandText} | {result.message}");
        }
        return result;
    }

    private GameTextCommandResult RunCommandExpectFail(GameTextCommandRunner runner, string commandText)
    {
        GameTextCommandResult result = runner.ExecuteLine(commandText);
        if (result.skipped)
        {
            _test.Fail($"命令被跳过，无法验证失败：{commandText}");
            return result;
        }
        if (result.ok)
        {
            GD.Print(result.Render());
            _test.Fail($"命令应失败但成功：{commandText}");
        }
        return result;
    }

    private static ItemDef BuildGemItemDef() =>
        new()
        {
            item_id = GemId,
            display_name = "Special Contingency Gem",
            CategoryKind = ItemCategoryKind.Misc,
            item_category = "misc",
            is_stackable = true,
            max_stack = 99,
        };

    private static UnitSkillProgress LearnedSkill(string skillId, int level) =>
        new()
        {
            skill_id = skillId,
            is_learned = true,
            skill_level = level,
            current_mastery = 0,
            total_mastery_earned = 0,
            is_core = false,
            granted_source_type = "player",
        };

    private static GDictionary MemberStatus(GDictionary snapshot, string memberId) =>
        Dict(Dict(Dict(snapshot, "party"), "contingency_status_by_member"), memberId);

    private static GDictionary FirstSetup(GDictionary status) =>
        FirstDictionary(ArrayValue(status, "setups"));

    private static GDictionary FirstDictionary(GArray values)
    {
        foreach (Variant value in values)
            return value.AsGodotDictionary();
        return new GDictionary();
    }

    private static GArray ArrayValue(GDictionary dictionary, string key) =>
        dictionary != null && dictionary.ContainsKey(key)
            ? dictionary[key].AsGodotArray()
            : new GArray();

    private static GDictionary Dict(GDictionary dictionary, string key) =>
        dictionary != null && dictionary.ContainsKey(key)
            ? dictionary[key].AsGodotDictionary()
            : new GDictionary();

    private static bool DictBool(GDictionary dictionary, string key, bool fallback) =>
        dictionary != null && dictionary.ContainsKey(key) ? dictionary[key].AsBool() : fallback;

    private static int DictInt(GDictionary dictionary, string key, int fallback = 0) =>
        dictionary != null && dictionary.ContainsKey(key) ? dictionary[key].AsInt32() : fallback;

    private static string DictString(GDictionary dictionary, string key) =>
        dictionary != null && dictionary.ContainsKey(key) ? dictionary[key].AsString() : "";
}
