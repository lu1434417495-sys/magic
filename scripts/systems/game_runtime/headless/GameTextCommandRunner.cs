using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

// Development-only text command protocol over the headless runtime.
// Keep command coverage aligned with automation needs, not player UX.
public sealed class GameTextCommandRunner : IDisposable
{
    private readonly struct IntParseResult
    {
        public IntParseResult(bool ok, int value, string errorMessage)
        {
            Ok = ok;
            Value = value;
            ErrorMessage = errorMessage ?? "";
        }

        public bool Ok { get; }
        public int Value { get; }
        public string ErrorMessage { get; }
    }

    private readonly struct CoordParseResult
    {
        public CoordParseResult(bool ok, Vector2I value, string errorMessage)
        {
            Ok = ok;
            Value = value;
            ErrorMessage = errorMessage ?? "";
        }

        public bool Ok { get; }
        public Vector2I Value { get; }
        public string ErrorMessage { get; }
    }

    private sealed class ExpectationResult
    {
        public bool Ok;
        public GameRuntimeFacade.RuntimeCommandCode Code = GameRuntimeFacade.RuntimeCommandCode.None;
        public string Summary = "";
        public string Actual = "";
        public string Expected = "";
    }

    private sealed class CommandOutcome
    {
        public bool Ok;
        public string Message = "";
        public GameRuntimeFacade.RuntimeCommandCode Code = GameRuntimeFacade.RuntimeCommandCode.None;
    }

    private HeadlessGameTestSession _session = new();
    private bool _disposed;

    public void initialize()
    {
        _session.initialize();
    }

    public HeadlessGameTestSession GetSession()
    {
        return _session;
    }

    public void Dispose()
    {
        Dispose(false);
    }

    public void Dispose(bool clear_persisted_game)
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        GC.SuppressFinalize(this);
        _session?.Dispose(clear_persisted_game);
        _session = null;
    }

    public GameTextCommandResult ExecuteLine(string command_text)
    {
        var result = new GameTextCommandResult { command_text = (command_text ?? "").StripEdges() };
        if (
            string.IsNullOrEmpty(result.command_text)
            || result.command_text.StartsWith("#", StringComparison.Ordinal)
        )
        {
            result.skipped = true;
            result.code = GameRuntimeFacade.RuntimeCommandCode.None;
            return result;
        }

        List<string> tokens = Tokenize(result.command_text);
        if (tokens.Count == 0)
        {
            result.skipped = true;
            result.code = GameRuntimeFacade.RuntimeCommandCode.None;
            return result;
        }

        if (tokens[0] == "expect")
        {
            FinalizeExpectResult(result, tokens);
            return result;
        }

        CommandOutcome commandResult = ExecuteCommand(tokens);
        _session.SettleFrames();
        result.ok = commandResult.Ok;
        result.message = commandResult.Message;
        result.code = commandResult.Code;
        result.SetSnapshot(_session.BuildSnapshotTyped());
        result.human_log = $"{(result.ok ? "OK" : "ERR")} {result.command_text}";
        result.snapshot_text = _session.BuildTextSnapshot();
        return result;
    }

    private void FinalizeExpectResult(GameTextCommandResult result, List<string> tokens)
    {
        IReadOnlyDictionary<string, object> snapshot = _session.BuildSnapshotTyped();
        result.SetSnapshot(snapshot);
        ExpectationResult assertionResult = ExecuteExpect(tokens, snapshot);
        result.ok = assertionResult.Ok;
        result.code = assertionResult.Code;
        result.message = assertionResult.Ok ? "" : "Expectation failed.";
        result.AddAssertion(
            assertionResult.Ok,
            result.message,
            assertionResult.Summary,
            assertionResult.Actual,
            assertionResult.Expected
        );
        result.snapshot_text = _session.BuildTextSnapshot();
    }

    private CommandOutcome ExecuteCommand(List<string> tokens)
    {
        return tokens[0] switch
        {
            "help" => Result(
                true,
                "Commands: preset/save/game/world/submap/party/quest/settlement/shop/stagecoach/warehouse/battle/reward/promotion/close/snapshot/expect"
            ),
            "preset" => ExecutePresetCommand(tokens),
            "save" => ExecuteSaveCommand(tokens),
            "game" => ExecuteGameCommand(tokens),
            "world" => ExecuteWorldCommand(tokens),
            "submap" => ExecuteSubmapCommand(tokens),
            "party" => ExecutePartyCommand(tokens),
            "quest" => ExecuteQuestCommand(tokens),
            "settlement" => ExecuteSettlementCommand(tokens),
            "shop" => ExecuteShopCommand(tokens),
            "stagecoach" => ExecuteStagecoachCommand(tokens),
            "warehouse" => ExecuteWarehouseCommand(tokens),
            "battle" => ExecuteBattleCommand(tokens),
            "reward" => ExecuteRewardCommand(tokens),
            "promotion" => ExecutePromotionCommand(tokens),
            "close" => ExecuteCloseCommand(tokens),
            "snapshot" => Result(true, "Snapshot generated."),
            _ => Result(false, $"未知命令域 {tokens[0]}。"),
        };
    }

    private CommandOutcome ExecutePresetCommand(List<string> tokens)
    {
        if (tokens.Count < 2 || tokens[1] != "list")
            return Result(false, "用法: preset list");
        return Result(true, $"Listed {_session.ListPresets().Count} presets.");
    }

    private CommandOutcome ExecuteSaveCommand(List<string> tokens)
    {
        if (tokens.Count < 2 || tokens[1] != "list")
            return Result(false, "用法: save list");
        return Result(true, $"Listed {_session.ListSaveSlots().Count} saves.");
    }

    private CommandOutcome ExecuteGameCommand(List<string> tokens)
    {
        if (tokens.Count < 3)
            return Result(false, "用法: game new <preset_id> | game load <save_id>");
        switch (tokens[1])
        {
            case "new":
            {
                HeadlessGameTestSession.SessionCommandOutcome outcome =
                    _session.CreateNewGameTyped(new StringName(tokens[2]));
                return Result(outcome.Ok, outcome.Message, outcome.Code);
            }
            case "load":
            {
                HeadlessGameTestSession.SessionCommandOutcome outcome =
                    _session.LoadGameTyped(tokens[2]);
                return Result(outcome.Ok, outcome.Message, outcome.Code);
            }
            default:
                return Result(false, $"未知 game 子命令 {tokens[1]}。");
        }
    }

    private CommandOutcome ExecuteWorldCommand(List<string> tokens)
    {
        CommandOutcome ensureResult = EnsureWorldContext();
        if (!ensureResult.Ok)
            return ensureResult;
        GameRuntimeFacade runtime = _session.GetRuntimeFacadeTyped();
        if (runtime == null)
            return MissingWorldError();
        if (tokens.Count < 2)
            return Result(false, "用法: world move/select/open/inspect ...");

        switch (tokens[1])
        {
            case "move":
            {
                if (tokens.Count < 3)
                    return Result(false, "用法: world move <up|down|left|right> [count]");
                Vector2I direction = ParseDirection(tokens[2]);
                int count = 1;
                if (tokens.Count >= 4)
                {
                    IntParseResult countResult = ParseIntArgument(tokens[3], "移动次数");
                    if (!countResult.Ok)
                        return Result(false, countResult.ErrorMessage);
                    count = countResult.Value;
                }
                return ResultFromRuntimeOutcome(runtime.CommandWorldMoveTyped(direction, count));
            }
            case "select":
            {
                if (tokens.Count < 4)
                    return Result(false, "用法: world select <x> <y>");
                CoordParseResult coordResult = ParseCoordArgument(tokens[2], tokens[3], "世界坐标");
                if (!coordResult.Ok)
                    return Result(false, coordResult.ErrorMessage);
                return ResultFromRuntimeOutcome(runtime.CommandWorldSelectTyped(coordResult.Value));
            }
            case "open":
            {
                if (tokens.Count >= 4)
                {
                    CoordParseResult coordResult = ParseCoordArgument(tokens[2], tokens[3], "聚落坐标");
                    if (!coordResult.Ok)
                        return Result(false, coordResult.ErrorMessage);
                    return ResultFromRuntimeOutcome(
                        runtime.CommandOpenSettlementTyped(coordResult.Value)
                    );
                }
                return ResultFromRuntimeOutcome(runtime.CommandOpenSettlementTyped());
            }
            case "inspect":
            {
                if (tokens.Count < 4)
                    return Result(false, "用法: world inspect <x> <y>");
                CoordParseResult coordResult = ParseCoordArgument(tokens[2], tokens[3], "世界坐标");
                if (!coordResult.Ok)
                    return Result(false, coordResult.ErrorMessage);
                return ResultFromRuntimeOutcome(runtime.CommandWorldInspectTyped(coordResult.Value));
            }
            case "click":
            {
                if (tokens.Count < 4)
                    return Result(false, "用法: world click <x> <y>");
                CoordParseResult coordResult = ParseCoordArgument(tokens[2], tokens[3], "世界坐标");
                if (!coordResult.Ok)
                    return Result(false, coordResult.ErrorMessage);
                return ResultFromRuntimeOutcome(runtime.SelectWorldCellTyped(coordResult.Value));
            }
            default:
                return Result(false, $"未知 world 子命令 {tokens[1]}。");
        }
    }

    private CommandOutcome ExecuteSubmapCommand(List<string> tokens)
    {
        CommandOutcome ensureResult = EnsureWorldContext();
        if (!ensureResult.Ok)
            return ensureResult;
        GameRuntimeFacade runtime = _session.GetRuntimeFacadeTyped();
        if (runtime == null)
            return MissingWorldError();
        if (tokens.Count < 2)
            return Result(false, "用法: submap confirm|cancel|return");
        return tokens[1] switch
        {
            "confirm" => ResultFromRuntimeOutcome(runtime.CommandConfirmSubmapEntryTyped()),
            "cancel" => ResultFromRuntimeOutcome(runtime.CommandCancelSubmapEntryTyped()),
            "return" => ResultFromRuntimeOutcome(runtime.CommandReturnFromSubmapTyped()),
            _ => Result(false, $"未知 submap 子命令 {tokens[1]}。"),
        };
    }

    private CommandOutcome ExecutePartyCommand(List<string> tokens)
    {
        CommandOutcome ensureResult = EnsureWorldContext();
        if (!ensureResult.Ok)
            return ensureResult;
        GameRuntimeFacade runtime = _session.GetRuntimeFacadeTyped();
        if (runtime == null)
            return MissingWorldError();
        if (tokens.Count < 2)
            return Result(
                false,
                "用法: party open/select/leader/activate/reserve/equip/unequip/warehouse ..."
            );

        switch (tokens[1])
        {
            case "open":
                return ResultFromRuntimeOutcome(runtime.CommandOpenPartyTyped());
            case "select":
                if (tokens.Count < 3)
                    return Result(false, "用法: party select <member_id>");
                return ResultFromRuntimeOutcome(
                    runtime.CommandSelectPartyMemberTyped(new StringName(tokens[2]))
                );
            case "leader":
                if (tokens.Count < 3)
                    return Result(false, "用法: party leader <member_id>");
                return ResultFromRuntimeOutcome(
                    runtime.CommandSetPartyLeaderTyped(new StringName(tokens[2]))
                );
            case "activate":
                if (tokens.Count < 3)
                    return Result(false, "用法: party activate <member_id>");
                return ResultFromRuntimeOutcome(
                    runtime.CommandMoveMemberToActiveTyped(new StringName(tokens[2]))
                );
            case "reserve":
                if (tokens.Count < 3)
                    return Result(false, "用法: party reserve <member_id>");
                return ResultFromRuntimeOutcome(
                    runtime.CommandMoveMemberToReserveTyped(new StringName(tokens[2]))
                );
            case "equip":
            {
                if (tokens.Count < 4)
                    return Result(
                        false,
                        "用法: party equip <member_id> <item_id> [slot_id] [instance_id=<instance_id>]"
                    );
                int argsStart = 4;
                StringName slotId = "";
                if (tokens.Count >= 5 && !tokens[4].Contains('='))
                {
                    slotId = new StringName(tokens[4]);
                    argsStart = 5;
                }
                if (!TryParseNamedStringArgs(tokens, argsStart, out var namedArgs, out string namedArgError))
                    return Result(false, namedArgError);
                return ResultFromRuntimeOutcome(
                    runtime.CommandPartyEquipItemTyped(
                        new StringName(tokens[2]),
                        new StringName(tokens[3]),
                        slotId,
                        namedArgs.TryGetValue("instance_id", out string instanceId)
                            ? new StringName(instanceId)
                            : new StringName()
                    )
                );
            }
            case "unequip":
                if (tokens.Count < 4)
                    return Result(false, "用法: party unequip <member_id> <slot_id>");
                return ResultFromRuntimeOutcome(
                    runtime.CommandPartyUnequipItemTyped(
                        new StringName(tokens[2]),
                        new StringName(tokens[3])
                    )
                );
            case "warehouse":
            {
                GameRuntimeFacade typedRuntime = _session.GetRuntimeFacadeTyped();
                return typedRuntime != null
                    ? ResultFromRuntimeOutcome(typedRuntime.CommandOpenPartyWarehouseTyped())
                    : MissingWorldError();
            }
            default:
                return Result(false, $"未知 party 子命令 {tokens[1]}。");
        }
    }

    private CommandOutcome ExecuteQuestCommand(List<string> tokens)
    {
        CommandOutcome ensureResult = EnsureWorldContext();
        if (!ensureResult.Ok)
            return ensureResult;
        GameRuntimeFacade runtime = _session.GetRuntimeFacadeTyped();
        if (runtime == null)
            return MissingWorldError();
        if (tokens.Count < 2)
            return Result(false, "用法: quest accept|progress|complete <quest_id> ...");
        switch (tokens[1])
        {
            case "accept":
                if (tokens.Count < 3)
                    return Result(false, "用法: quest accept <quest_id>");
                return ResultFromRuntimeOutcome(
                    runtime.CommandAcceptQuestTyped(new StringName(tokens[2]), false)
                );
            case "progress":
            {
                if (tokens.Count < 5)
                    return Result(
                        false,
                        "用法: quest progress <quest_id> <objective_id> <amount> [key=value ...]"
                    );
                IntParseResult amountResult = ParseIntArgument(tokens[4], "任务进度增量");
                if (!amountResult.Ok)
                    return Result(false, amountResult.ErrorMessage);
                if (!TryParseNamedStringArgs(tokens, 5, out var namedArgs, out string namedArgError))
                    return Result(false, namedArgError);
                var payload = QuestProgressCommandPayloadData.FromNamedArgs(
                    namedArgs,
                    runtime.GetWorldStep()
                );
                return ResultFromRuntimeOutcome(
                    runtime.CommandProgressQuestTyped(
                        new StringName(tokens[2]),
                        new StringName(tokens[3]),
                        amountResult.Value,
                        payload
                    )
                );
            }
            case "complete":
                if (tokens.Count < 3)
                    return Result(false, "用法: quest complete <quest_id>");
                return ResultFromRuntimeOutcome(
                    runtime.CommandCompleteQuestTyped(new StringName(tokens[2]))
                );
            default:
                return Result(false, $"未知 quest 子命令 {tokens[1]}。");
        }
    }

    private CommandOutcome ExecuteSettlementCommand(List<string> tokens)
    {
        CommandOutcome ensureResult = EnsureWorldContext();
        if (!ensureResult.Ok)
            return ensureResult;
        GameRuntimeFacade runtime = _session.GetRuntimeFacadeTyped();
        if (runtime == null)
            return MissingWorldError();
        if (tokens.Count < 3 || tokens[1] != "action")
            return Result(false, "用法: settlement action <action_id> [key=value ...]");
        return ResultFromRuntimeOutcome(
            runtime.CommandExecuteSettlementActionTyped(
                tokens[2],
                ProjectTypedDictionary(ParseNamedArgsTyped(tokens, 3))
            )
        );
    }

    private CommandOutcome ExecuteShopCommand(List<string> tokens)
    {
        CommandOutcome ensureResult = EnsureWorldContext();
        if (!ensureResult.Ok)
            return ensureResult;
        GameRuntimeFacade runtime = _session.GetRuntimeFacadeTyped();
        if (runtime == null)
            return MissingWorldError();
        if (tokens.Count < 3)
            return Result(
                false,
                "用法: shop buy|sell <item_id> [quantity] [instance_id=<instance_id>]"
            );

        int quantity = 1;
        int argsStart = 3;
        if (tokens.Count >= 4 && !tokens[3].Contains('='))
        {
            IntParseResult quantityResult = ParseIntArgument(tokens[3], "商品数量");
            if (!quantityResult.Ok)
                return Result(false, quantityResult.ErrorMessage);
            quantity = quantityResult.Value;
            argsStart = 4;
        }
        if (!TryParseNamedStringArgs(tokens, argsStart, out var namedArgs, out string namedArgError))
            return Result(false, namedArgError);
        return tokens[1] switch
        {
            "buy" => ResultFromRuntimeOutcome(
                runtime.CommandShopBuyTyped(new StringName(tokens[2]), quantity)
            ),
            "sell" => ResultFromRuntimeOutcome(
                runtime.CommandShopSellTyped(
                    new StringName(tokens[2]),
                    quantity,
                    namedArgs.TryGetValue("instance_id", out string instanceId)
                        ? new StringName(instanceId)
                        : new StringName()
                )
            ),
            _ => Result(false, $"未知 shop 子命令 {tokens[1]}。"),
        };
    }

    private CommandOutcome ExecuteStagecoachCommand(List<string> tokens)
    {
        CommandOutcome ensureResult = EnsureWorldContext();
        if (!ensureResult.Ok)
            return ensureResult;
        GameRuntimeFacade runtime = _session.GetRuntimeFacadeTyped();
        if (runtime == null)
            return MissingWorldError();
        if (tokens.Count < 3 || tokens[1] != "travel")
            return Result(false, "用法: stagecoach travel <settlement_id>");
        return ResultFromRuntimeOutcome(runtime.CommandStagecoachTravelTyped(tokens[2]));
    }

    private CommandOutcome ExecuteWarehouseCommand(List<string> tokens)
    {
        CommandOutcome ensureResult = EnsureWorldContext();
        if (!ensureResult.Ok)
            return ensureResult;
        GameRuntimeFacade runtime = _session.GetRuntimeFacadeTyped();
        if (runtime == null)
            return MissingWorldError();
        if (tokens.Count < 3)
            return Result(
                false,
                "用法: warehouse add <item_id> <quantity> | warehouse use <item_id> [member_id] | warehouse discard-one|discard-all <item_id> [instance_id=<instance_id>] | warehouse capacity <value>"
            );

        switch (tokens[1])
        {
            case "add":
            {
                if (tokens.Count < 4)
                    return Result(false, "用法: warehouse add <item_id> <quantity>");
                IntParseResult quantityResult = ParseIntArgument(tokens[3], "仓库数量");
                if (!quantityResult.Ok)
                    return Result(false, quantityResult.ErrorMessage);
                return ResultFromRuntimeOutcome(
                    runtime.CommandWarehouseAddItemTyped(
                        new StringName(tokens[2]),
                        quantityResult.Value
                    )
                );
            }
            case "use":
            {
                var options = new PartyItemUseService.PartyItemUseOptions(tokens.Contains("confirm"));
                StringName memberId = "";
                if (tokens.Count >= 4 && tokens[3] != "confirm")
                    memberId = new StringName(tokens[3]);
                return ResultFromRuntimeOutcome(
                    runtime.CommandWarehouseUseItemTyped(
                        new StringName(tokens[2]),
                        memberId,
                        options
                    )
                );
            }
            case "capacity":
            {
                IntParseResult capacityResult = ParseIntArgument(tokens[2], "仓库容量");
                if (!capacityResult.Ok)
                    return Result(false, capacityResult.ErrorMessage);
                HeadlessGameTestSession.SessionCommandOutcome outcome =
                    _session.SetPartyStorageCapacityTyped(capacityResult.Value);
                return Result(outcome.Ok, outcome.Message, outcome.Code);
            }
            case "discard-one":
            {
                if (!TryParseNamedStringArgs(tokens, 3, out var namedArgs, out string namedArgError))
                    return Result(false, namedArgError);
                return ResultFromRuntimeOutcome(
                    runtime.CommandWarehouseDiscardOneTyped(
                        new StringName(tokens[2]),
                        namedArgs.TryGetValue("instance_id", out string instanceId)
                            ? new StringName(instanceId)
                            : new StringName()
                    )
                );
            }
            case "discard-all":
            {
                if (!TryParseNamedStringArgs(tokens, 3, out var namedArgs, out string namedArgError))
                    return Result(false, namedArgError);
                return ResultFromRuntimeOutcome(
                    runtime.CommandWarehouseDiscardAllTyped(
                        new StringName(tokens[2]),
                        namedArgs.TryGetValue("instance_id", out string instanceId)
                            ? new StringName(instanceId)
                            : new StringName()
                    )
                );
            }
            default:
                return Result(false, $"未知 warehouse 子命令 {tokens[1]}。");
        }
    }

    private CommandOutcome ExecuteBattleCommand(List<string> tokens)
    {
        CommandOutcome ensureResult = EnsureWorldContext();
        if (!ensureResult.Ok)
            return ensureResult;
        GameRuntimeFacade runtime = _session.GetRuntimeFacadeTyped();
        if (runtime == null)
            return MissingWorldError();
        if (tokens.Count < 2)
            return Result(
                false,
                "用法: battle start/confirm/tick/skill/option/move/equip/unequip/wait/cancel-cast/inspect/finish ..."
            );

        switch (tokens[1])
        {
            case "start":
                if (tokens.Count < 3)
                    return Result(false, "用法: battle start <settlement|single>");
            {
                HeadlessGameTestSession.SessionCommandOutcome outcome =
                    _session.StartBattleByKindTyped(new StringName(tokens[2]));
                return Result(outcome.Ok, outcome.Message, outcome.Code);
            }
            case "confirm":
            {
                GameRuntimeFacade.RuntimeCommandResult outcome =
                    runtime.CommandConfirmBattleStartTyped();
                return Result(outcome.Ok, outcome.Message, outcome.Code);
            }
            case "tick":
            {
                if (tokens.Count < 3)
                    return Result(false, "用法: battle tick <ticks>");
                IntParseResult tickResult = ParseIntArgument(tokens[2], "战斗推进 tick");
                if (!tickResult.Ok)
                    return Result(false, tickResult.ErrorMessage);
                return ResultFromRuntimeOutcome(runtime.CommandBattleTickTyped(tickResult.Value));
            }
            case "skill":
            {
                if (tokens.Count < 3)
                    return Result(false, "用法: battle skill <slot>");
                IntParseResult slotResult = ParseIntArgument(tokens[2], "技能栏位");
                if (!slotResult.Ok)
                    return Result(false, slotResult.ErrorMessage);
                return ResultFromRuntimeOutcome(
                    runtime.CommandBattleSelectSkillTyped(slotResult.Value - 1)
                );
            }
            case "option":
                if (tokens.Count < 3)
                    return Result(false, "用法: battle option <next|prev>");
                return ResultFromRuntimeOutcome(
                    runtime.CommandBattleCycleVariantTyped(tokens[2] == "next" ? 1 : -1)
                );
            case "move":
                if (tokens.Count == 3)
                    return ResultFromRuntimeOutcome(
                        runtime.CommandBattleMoveDirectionTyped(ParseDirection(tokens[2]))
                    );
                if (tokens.Count >= 4)
                {
                    CoordParseResult coordResult = ParseCoordArgument(tokens[2], tokens[3], "战斗坐标");
                    if (!coordResult.Ok)
                        return Result(false, coordResult.ErrorMessage);
                    return ResultFromRuntimeOutcome(runtime.CommandBattleMoveToTyped(coordResult.Value));
                }
                return Result(
                    false,
                    "用法: battle move <up|down|left|right> | battle move <x> <y>"
                );
            case "equip":
            {
                if (tokens.Count < 4)
                    return Result(
                        false,
                        "用法: battle equip <slot_id> <item_id> [instance_id=<instance_id>]"
                    );
                if (!TryParseNamedStringArgs(tokens, 4, out var namedArgs, out string namedArgError))
                    return Result(false, namedArgError);
                {
                    HeadlessGameTestSession.SessionCommandOutcome outcome =
                        _session.ChangeBattleEquipmentTyped(
                        "equip",
                        new StringName(tokens[2]),
                        new StringName(tokens[3]),
                        namedArgs.TryGetValue("instance_id", out string instanceId)
                            ? new StringName(instanceId)
                            : new StringName(),
                        namedArgs.TryGetValue("target_unit_id", out string targetUnitId)
                            ? new StringName(targetUnitId)
                            : new StringName()
                        );
                    return Result(outcome.Ok, outcome.Message, outcome.Code);
                }
            }
            case "unequip":
            {
                if (tokens.Count < 3)
                    return Result(
                        false,
                        "用法: battle unequip <slot_id> [instance_id=<instance_id>]"
                    );
                int argsStart = 3;
                string instanceId = "";
                if (tokens.Count >= 4 && !tokens[3].Contains('='))
                {
                    instanceId = tokens[3];
                    argsStart = 4;
                }
                if (!TryParseNamedStringArgs(tokens, argsStart, out var namedArgs, out string namedArgError))
                    return Result(false, namedArgError);
                if (string.IsNullOrEmpty(instanceId) && namedArgs.TryGetValue("instance_id", out string namedInstanceId))
                    instanceId = namedInstanceId;
                {
                    HeadlessGameTestSession.SessionCommandOutcome outcome =
                        _session.ChangeBattleEquipmentTyped(
                        "unequip",
                        new StringName(tokens[2]),
                        "",
                        new StringName(instanceId),
                        namedArgs.TryGetValue("target_unit_id", out string targetUnitId)
                            ? new StringName(targetUnitId)
                            : new StringName()
                        );
                    return Result(outcome.Ok, outcome.Message, outcome.Code);
                }
            }
            case "wait":
            {
                GameRuntimeFacade.RuntimeCommandResult outcome =
                    runtime.CommandBattleWaitOrResolveTyped();
                return Result(outcome.Ok, outcome.Message, outcome.Code);
            }
            case "cancel-cast":
            case "cancel_cast":
            {
                StringName unitId = tokens.Count >= 3 ? new StringName(tokens[2]) : new StringName();
                GameRuntimeFacade.RuntimeCommandResult outcome =
                    runtime.CommandBattleCancelCastTyped(unitId);
                return Result(outcome.Ok, outcome.Message, outcome.Code);
            }
            case "inspect":
            {
                if (tokens.Count < 4)
                    return Result(false, "用法: battle inspect <x> <y>");
                CoordParseResult coordResult = ParseCoordArgument(tokens[2], tokens[3], "战斗坐标");
                if (!coordResult.Ok)
                    return Result(false, coordResult.ErrorMessage);
                return ResultFromRuntimeOutcome(runtime.CommandBattleInspectTyped(coordResult.Value));
            }
            case "finish":
                if (tokens.Count < 3)
                    return Result(false, "用法: battle finish <player|hostile>");
            {
                HeadlessGameTestSession.SessionCommandOutcome outcome =
                    _session.FinishActiveBattleTyped(new StringName(tokens[2]));
                return Result(outcome.Ok, outcome.Message, outcome.Code);
            }
            case "clear":
                return ResultFromRuntimeOutcome(runtime.CommandBattleClearSkillTyped());
            default:
                return Result(false, $"未知 battle 子命令 {tokens[1]}。");
        }
    }

    private CommandOutcome ExecuteRewardCommand(List<string> tokens)
    {
        CommandOutcome ensureResult = EnsureWorldContext();
        if (!ensureResult.Ok)
            return ensureResult;
        GameRuntimeFacade runtime = _session.GetRuntimeFacadeTyped();
        if (runtime == null)
            return MissingWorldError();
        if (tokens.Count < 2 || tokens[1] != "confirm")
            return Result(false, "用法: reward confirm");
        {
            GameRuntimeFacade.RuntimeCommandResult outcome =
                runtime.CommandConfirmPendingRewardTyped();
            return Result(outcome.Ok, outcome.Message, outcome.Code);
        }
    }

    private CommandOutcome ExecutePromotionCommand(List<string> tokens)
    {
        CommandOutcome ensureResult = EnsureWorldContext();
        if (!ensureResult.Ok)
            return ensureResult;
        GameRuntimeFacade runtime = _session.GetRuntimeFacadeTyped();
        if (runtime == null)
            return MissingWorldError();
        if (tokens.Count < 3 || tokens[1] != "choose")
            return Result(false, "用法: promotion choose <profession_id>");
        {
            GameRuntimeFacade.RuntimeCommandResult outcome =
                runtime.CommandChoosePromotionTyped(new StringName(tokens[2]));
            return Result(outcome.Ok, outcome.Message, outcome.Code);
        }
    }

    private CommandOutcome ExecuteCloseCommand(List<string> tokens)
    {
        CommandOutcome ensureResult = EnsureWorldContext();
        if (!ensureResult.Ok)
            return ensureResult;
        GameRuntimeFacade runtime = _session.GetRuntimeFacadeTyped();
        if (runtime == null)
            return MissingWorldError();
        {
            GameRuntimeFacade.RuntimeCommandResult outcome =
                runtime.CommandCloseActiveModalTyped();
            return Result(outcome.Ok, outcome.Message, outcome.Code);
        }
    }

    private ExpectationResult ExecuteExpect(
        List<string> tokens,
        IReadOnlyDictionary<string, object> snapshot
    )
    {
        if (tokens.Count < 3)
            return ExpectError("用法: expect status/window/field/list ...", "", "");
        switch (tokens[1])
        {
            case "status":
            {
                if (tokens.Count < 4 || tokens[2] != "contains")
                    return ExpectError("expect status contains <text>", "", "");
                string statusText = ReadSnapshotString(ReadSnapshotDictionary(snapshot, "status"), "text");
                string expectedText = JoinTokens(tokens, 3);
                return statusText.Contains(expectedText, StringComparison.Ordinal)
                    ? ExpectOk($"status contains {expectedText}", statusText, expectedText)
                    : ExpectError($"status contains {expectedText}", statusText, expectedText);
            }
            case "window":
            {
                if (tokens.Count < 4 || tokens[2] != "==")
                    return ExpectError("expect window == <id>", "", "");
                string actualWindow = ReadSnapshotString(ReadSnapshotDictionary(snapshot, "modal"), "id");
                string expectedWindow = tokens[3];
                return actualWindow == expectedWindow
                    ? ExpectOk($"window == {expectedWindow}", actualWindow, expectedWindow)
                    : ExpectError($"window == {expectedWindow}", actualWindow, expectedWindow);
            }
            case "field":
            {
                if (tokens.Count < 5 || tokens[3] != "==")
                    return ExpectError("expect field <path> == <value>", "", "");
                if (!ResolvePath(snapshot, tokens[2], out object actualValue, out string pathError))
                    return ExpectError(pathError, "", tokens[4]);
                object expectedValue = ParseScalar(JoinTokens(tokens, 4));
                return ValuesEqual(actualValue, expectedValue)
                    ? ExpectOk(
                        $"field {tokens[2]} == {StringifyForSummary(expectedValue)}",
                        StringifyValue(actualValue),
                        StringifyValue(expectedValue)
                    )
                    : ExpectError(
                        $"field {tokens[2]} == {StringifyForSummary(expectedValue)}",
                        StringifyValue(actualValue),
                        StringifyValue(expectedValue)
                    );
            }
            case "list":
            {
                if (tokens.Count < 5 || tokens[3] != "contains")
                    return ExpectError("expect list <path> contains <value>", "", "");
                if (!ResolvePath(snapshot, tokens[2], out object listValue, out string pathError))
                    return ExpectError(pathError, "", tokens[4]);
                if (listValue is not IReadOnlyList<object> array)
                    return ExpectError(
                        $"path {tokens[2]} is not a list",
                        StringifyValue(listValue),
                        tokens[4]
                    );
                object expectedItem = ParseScalar(JoinTokens(tokens, 4));
                foreach (var itemValue in array)
                {
                    if (ValuesEqual(itemValue, expectedItem))
                        return ExpectOk(
                            $"list {tokens[2]} contains {StringifyForSummary(expectedItem)}",
                            StringifyValue(array),
                            StringifyValue(expectedItem)
                        );
                }
                return ExpectError(
                    $"list {tokens[2]} contains {StringifyForSummary(expectedItem)}",
                    StringifyValue(array),
                    StringifyValue(expectedItem)
                );
            }
            case "warehouse":
            {
                if (tokens.Count < 5 || tokens[3] != "==")
                    return ExpectError("expect warehouse <item_id> == <quantity>", "", "");
                string itemId = tokens[2];
                IntParseResult expectedQuantityResult = ParseIntArgument(
                    JoinTokens(tokens, 4),
                    "期望仓库数量"
                );
                if (!expectedQuantityResult.Ok)
                    return ExpectError(
                        expectedQuantityResult.ErrorMessage,
                        "",
                        JoinTokens(tokens, 4)
                    );
                int expectedQuantity = expectedQuantityResult.Value;
                int actualQuantity = GetWarehouseItemTotal(snapshot, itemId);
                return actualQuantity == expectedQuantity
                    ? ExpectOk(
                        $"warehouse {itemId} == {expectedQuantity}",
                        actualQuantity.ToString(CultureInfo.GetCultureInfo("")),
                        expectedQuantity.ToString(CultureInfo.GetCultureInfo(""))
                    )
                    : ExpectError(
                        $"warehouse {itemId} == {expectedQuantity}",
                        actualQuantity.ToString(CultureInfo.GetCultureInfo("")),
                        expectedQuantity.ToString(CultureInfo.GetCultureInfo(""))
                    );
            }
            default:
                return ExpectError($"unknown expect target {tokens[1]}", "", "");
        }
    }

    private CommandOutcome EnsureWorldContext()
    {
        HeadlessGameTestSession.SessionCommandOutcome outcome = _session.EnsureWorldLoadedTyped();
        return Result(outcome.Ok, outcome.Message, outcome.Code);
    }

    private static CommandOutcome MissingWorldError()
    {
        return Result(
            false,
            "当前世界地图不可用。",
            GameRuntimeFacade.RuntimeCommandCode.RuntimeUnavailable
        );
    }

    private static CommandOutcome ResultFromRuntimeOutcome(GameRuntimeFacade.RuntimeCommandResult outcome)
    {
        return Result(
            outcome?.Ok ?? false,
            outcome?.Message ?? "",
            outcome?.Code ?? GameRuntimeFacade.RuntimeCommandCode.Failed
        );
    }

    private static List<string> Tokenize(string line)
    {
        var tokens = new List<string>();
        string current = "";
        bool inQuotes = false;
        bool escaping = false;
        foreach (char ch in line)
        {
            if (escaping)
            {
                current += ch;
                escaping = false;
                continue;
            }
            if (inQuotes && ch == '\\')
            {
                escaping = true;
                continue;
            }
            if (ch == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }
            if (!inQuotes && (ch == ' ' || ch == '\t'))
            {
                if (!string.IsNullOrEmpty(current))
                {
                    tokens.Add(current);
                    current = "";
                }
                continue;
            }
            current += ch;
        }
        if (!string.IsNullOrEmpty(current))
            tokens.Add(current);
        return tokens;
    }

    private static Vector2I ParseDirection(string token)
    {
        return (token ?? "").ToLower(System.Globalization.CultureInfo.GetCultureInfo("")) switch
        {
            "up" => Vector2I.Up,
            "down" => Vector2I.Down,
            "left" => Vector2I.Left,
            "right" => Vector2I.Right,
            _ => Vector2I.Zero,
        };
    }

    private static IntParseResult ParseIntArgument(string token, string label)
    {
        object value = ParseScalar(token);
        if (value is int intValue)
            return new IntParseResult(true, intValue, "");
        return new IntParseResult(false, 0, $"{label} 必须是整数，收到 {token}。");
    }

    private static CoordParseResult ParseCoordArgument(string xToken, string yToken, string label)
    {
        IntParseResult xResult = ParseIntArgument(xToken, $"{label} X");
        if (!xResult.Ok)
            return new CoordParseResult(false, default, xResult.ErrorMessage);
        IntParseResult yResult = ParseIntArgument(yToken, $"{label} Y");
        if (!yResult.Ok)
            return new CoordParseResult(false, default, yResult.ErrorMessage);
        return new CoordParseResult(true, new Vector2I(xResult.Value, yResult.Value), "");
    }

    private static Dictionary<string, object> ParseNamedArgsTyped(List<string> tokens, int startIndex)
    {
        var result = new Dictionary<string, object>(StringComparer.Ordinal);
        for (int index = startIndex; index < tokens.Count; index++)
        {
            string token = tokens[index];
            int equalsIndex = token.IndexOf('=', StringComparison.Ordinal);
            if (equalsIndex <= 0)
                continue;
            string key = token[..equalsIndex];
            string valueText = token[(equalsIndex + 1)..];
            result[key] = ParseScalar(valueText);
        }
        return result;
    }

    private static bool TryParseNamedStringArgs(
        List<string> tokens,
        int startIndex,
        out Dictionary<string, string> namedArgs,
        out string errorMessage
    )
    {
        namedArgs = new Dictionary<string, string>(StringComparer.Ordinal);
        errorMessage = "";
        for (int index = startIndex; index < tokens.Count; index++)
        {
            string token = tokens[index];
            int equalsIndex = token.IndexOf('=', StringComparison.Ordinal);
            if (equalsIndex <= 0)
            {
                errorMessage = $"命名参数必须使用 key=value 形式，收到 {token}。";
                return false;
            }
            string key = token[..equalsIndex];
            string valueText = token[(equalsIndex + 1)..];
            namedArgs[key] = valueText;
        }
        return true;
    }

    private static object ParseScalar(string token)
    {
        string normalized = (token ?? "").StripEdges();
        if (normalized == "true")
            return true;
        if (normalized == "false")
            return false;
        if (
            int.TryParse(
                normalized,
                NumberStyles.Integer,
                CultureInfo.GetCultureInfo(""),
                out int intValue
            )
        )
            return intValue;
        if (
            float.TryParse(
                normalized,
                NumberStyles.Float,
                CultureInfo.GetCultureInfo(""),
                out float floatValue
            )
        )
            return floatValue;
        return normalized;
    }

    private static bool ResolvePath(
        IReadOnlyDictionary<string, object> root,
        string path,
        out object resolvedValue,
        out string errorMessage
    )
    {
        object current = root;
        foreach (string segment in (path ?? "").Split('.'))
        {
            if (current is IReadOnlyDictionary<string, object> dictionary)
            {
                if (!dictionary.TryGetValue(segment, out current))
                {
                    resolvedValue = null;
                    errorMessage = $"path {path} is missing at {segment}";
                    return false;
                }
                continue;
            }
            if (current is IReadOnlyList<object> array)
            {
                if (
                    !int.TryParse(
                        segment,
                        NumberStyles.Integer,
                        CultureInfo.GetCultureInfo(""),
                        out int arrayIndex
                    )
                )
                {
                    resolvedValue = null;
                    errorMessage = $"path {path} expected numeric index at {segment}";
                    return false;
                }
                if (arrayIndex < 0 || arrayIndex >= array.Count)
                {
                    resolvedValue = null;
                    errorMessage = $"path {path} index out of range at {segment}";
                    return false;
                }
                current = array[arrayIndex];
                continue;
            }
            resolvedValue = null;
            errorMessage = $"path {path} cannot descend into {segment}";
            return false;
        }
        resolvedValue = current;
        errorMessage = "";
        return true;
    }

    private static bool ValuesEqual(object actual, object expected)
    {
        if (
            actual is IReadOnlyDictionary<string, object>
            || actual is IReadOnlyList<object>
            || expected is IReadOnlyDictionary<string, object>
            || expected is IReadOnlyList<object>
        )
            return StringifyValue(actual) == StringifyValue(expected);
        if (TryConvertToDouble(actual, out double actualDouble) && TryConvertToDouble(expected, out double expectedDouble))
            return Math.Abs(actualDouble - expectedDouble) < 0.0001;
        return Equals(actual, expected);
    }

    private static string StringifyValue(object value)
    {
        return value switch
        {
            IReadOnlyDictionary<string, object> dictionary => Json.Stringify(ProjectTypedDictionary(dictionary)),
            IReadOnlyList<object> array => Json.Stringify(ProjectTypedArray(array)),
            bool boolValue => boolValue ? "true" : "false",
            float floatValue => floatValue.ToString(CultureInfo.GetCultureInfo("")),
            double doubleValue => doubleValue.ToString(CultureInfo.GetCultureInfo("")),
            int intValue => intValue.ToString(CultureInfo.GetCultureInfo("")),
            long longValue => longValue.ToString(CultureInfo.GetCultureInfo("")),
            _ => value?.ToString() ?? "",
        };
    }

    private static string StringifyForSummary(object value)
    {
        return value switch
        {
            bool boolValue => boolValue ? "True" : "False",
            _ => value?.ToString() ?? "",
        };
    }

    private static string JoinTokens(List<string> tokens, int startIndex)
    {
        if (startIndex >= tokens.Count)
            return "";
        return string.Join(" ", tokens.GetRange(startIndex, tokens.Count - startIndex));
    }

    private static int GetWarehouseItemTotal(
        IReadOnlyDictionary<string, object> snapshot,
        string itemId
    )
    {
        IReadOnlyDictionary<string, object> warehouse = ReadSnapshotDictionary(snapshot, "warehouse");
        IReadOnlyDictionary<string, object> windowData = ReadSnapshotDictionary(warehouse, "window_data");
        IReadOnlyList<object> entries = ReadSnapshotArray(windowData, "entries");
        foreach (object entryValue in entries)
        {
            if (entryValue is not IReadOnlyDictionary<string, object> entry)
                continue;
            if (ReadSnapshotString(entry, "item_id") != itemId)
                continue;
            return ReadSnapshotInt(entry, "total_quantity");
        }
        return 0;
    }

    private static IReadOnlyDictionary<string, object> ReadSnapshotDictionary(
        IReadOnlyDictionary<string, object> source,
        string key
    )
    {
        return source != null
            && source.TryGetValue(key, out object rawValue)
            && rawValue is IReadOnlyDictionary<string, object> dictionary
            ? dictionary
            : new Dictionary<string, object>(StringComparer.Ordinal);
    }

    private static IReadOnlyList<object> ReadSnapshotArray(
        IReadOnlyDictionary<string, object> source,
        string key
    )
    {
        return source != null
            && source.TryGetValue(key, out object rawValue)
            && rawValue is IReadOnlyList<object> array
            ? array
            : Array.Empty<object>();
    }

    private static string ReadSnapshotString(
        IReadOnlyDictionary<string, object> source,
        string key,
        string fallback = ""
    )
    {
        if (source == null || !source.TryGetValue(key, out object rawValue))
            return fallback;
        return rawValue switch
        {
            string stringValue => stringValue,
            StringName stringNameValue => stringNameValue.ToString(),
            _ => fallback,
        };
    }

    private static int ReadSnapshotInt(
        IReadOnlyDictionary<string, object> source,
        string key,
        int fallback = 0
    )
    {
        if (source == null || !source.TryGetValue(key, out object rawValue))
            return fallback;
        return rawValue switch
        {
            int intValue => intValue,
            long longValue => (int)longValue,
            _ => fallback,
        };
    }

    private static bool TryConvertToDouble(object value, out double result)
    {
        switch (value)
        {
            case byte byteValue:
                result = byteValue;
                return true;
            case sbyte sbyteValue:
                result = sbyteValue;
                return true;
            case short shortValue:
                result = shortValue;
                return true;
            case ushort ushortValue:
                result = ushortValue;
                return true;
            case int intValue:
                result = intValue;
                return true;
            case uint uintValue:
                result = uintValue;
                return true;
            case long longValue:
                result = longValue;
                return true;
            case ulong ulongValue:
                result = ulongValue;
                return true;
            case float floatValue:
                result = floatValue;
                return true;
            case double doubleValue:
                result = doubleValue;
                return true;
            default:
                result = 0;
                return false;
        }
    }

    private static GDictionary ProjectTypedDictionary(IReadOnlyDictionary<string, object> source)
    {
        var projection = new GDictionary();
        foreach ((string key, object value) in source)
            projection[key] = ProjectTypedValue(value);
        return projection;
    }

    private static GArray ProjectTypedArray(IReadOnlyList<object> source)
    {
        var projection = new GArray();
        foreach (object value in source)
            projection.Add(ProjectTypedValue(value));
        return projection;
    }

    private static Variant ProjectTypedValue(object value)
    {
        if (value == null)
            return default;
        if (value is IReadOnlyDictionary<string, object> dictionaryValue)
            return ProjectTypedDictionary(dictionaryValue);
        if (value is IReadOnlyList<object> listValue)
            return ProjectTypedArray(listValue);
        return value switch
        {
            Variant variantValue => variantValue,
            bool boolValue => boolValue,
            int intValue => intValue,
            long longValue => longValue,
            float floatValue => floatValue,
            double doubleValue => doubleValue,
            string stringValue => stringValue,
            StringName stringNameValue => stringNameValue,
            Vector2I vectorValue => vectorValue,
            GodotObject godotObject => godotObject,
            _ => value.ToString() ?? "",
        };
    }

    private static ExpectationResult ExpectOk(string summary, string actual, string expected)
    {
        return new ExpectationResult
        {
            Ok = true,
            Code = GameRuntimeFacade.RuntimeCommandCode.Ok,
            Summary = summary ?? "",
            Actual = actual ?? "",
            Expected = expected ?? "",
        };
    }

    private static ExpectationResult ExpectError(string summary, string actual, string expected)
    {
        return new ExpectationResult
        {
            Ok = false,
            Code = GameRuntimeFacade.RuntimeCommandCode.Failed,
            Summary = summary ?? "",
            Actual = actual ?? "",
            Expected = expected ?? "",
        };
    }

    private static CommandOutcome Result(
        bool ok,
        string message,
        GameRuntimeFacade.RuntimeCommandCode code = GameRuntimeFacade.RuntimeCommandCode.None
    )
    {
        return new CommandOutcome
        {
            Ok = ok,
            Message = message ?? "",
            Code =
                code != GameRuntimeFacade.RuntimeCommandCode.None
                    ? code
                    : ok
                        ? GameRuntimeFacade.RuntimeCommandCode.Ok
                        : GameRuntimeFacade.RuntimeCommandCode.Failed,
        };
    }

}
