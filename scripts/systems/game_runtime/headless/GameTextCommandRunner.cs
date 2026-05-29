using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

// Development-only text command protocol over the headless runtime.
// Keep command coverage aligned with automation needs, not player UX.
[GlobalClass]
public partial class GameTextCommandRunner : RefCounted
{
    private HeadlessGameTestSession _session = new();

    public void initialize()
    {
        _session.initialize();
    }

    public HeadlessGameTestSession get_session()
    {
        return _session;
    }

    public void dispose()
    {
        dispose(false);
    }

    public void dispose(bool clear_persisted_game)
    {
        _session?.dispose(clear_persisted_game);
        _session = null;
    }

    public GameTextCommandResult execute_line(string command_text)
    {
        var result = new GameTextCommandResult { command_text = (command_text ?? "").StripEdges() };
        if (
            string.IsNullOrEmpty(result.command_text)
            || result.command_text.StartsWith("#", StringComparison.Ordinal)
        )
        {
            result.skipped = true;
            return result;
        }

        List<string> tokens = Tokenize(result.command_text);
        if (tokens.Count == 0)
        {
            result.skipped = true;
            return result;
        }

        if (tokens[0] == "expect")
        {
            FinalizeExpectResult(result, tokens);
            return result;
        }

        GDictionary commandResult = ExecuteCommand(tokens);
        _session.settle_frames();
        result.ok = GdInterop.GetBool(commandResult, "ok", false);
        result.message = GdInterop.GetString(commandResult, "message");
        result.snapshot = _session.build_snapshot();
        result.human_log = $"{(result.ok ? "OK" : "ERR")} {result.command_text}";
        result.snapshot_text = _session.build_text_snapshot();
        return result;
    }

    private void FinalizeExpectResult(GameTextCommandResult result, List<string> tokens)
    {
        result.snapshot = _session.build_snapshot();
        GDictionary assertionResult = ExecuteExpect(tokens, result.snapshot);
        result.ok = GdInterop.GetBool(assertionResult, "ok", false);
        result.message = GdInterop.GetString(assertionResult, "message");
        result.assertions.Add(assertionResult);
        result.snapshot_text = _session.build_text_snapshot();
    }

    private GDictionary ExecuteCommand(List<string> tokens)
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

    private GDictionary ExecutePresetCommand(List<string> tokens)
    {
        if (tokens.Count < 2 || tokens[1] != "list")
            return Result(false, "用法: preset list");
        return Result(true, $"Listed {_session.list_presets().Count} presets.");
    }

    private GDictionary ExecuteSaveCommand(List<string> tokens)
    {
        if (tokens.Count < 2 || tokens[1] != "list")
            return Result(false, "用法: save list");
        return Result(true, $"Listed {_session.list_save_slots().Count} saves.");
    }

    private GDictionary ExecuteGameCommand(List<string> tokens)
    {
        if (tokens.Count < 3)
            return Result(false, "用法: game new <preset_id> | game load <save_id>");
        return tokens[1] switch
        {
            "new" => _session.create_new_game(new StringName(tokens[2])),
            "load" => _session.load_game(tokens[2]),
            _ => Result(false, $"未知 game 子命令 {tokens[1]}。"),
        };
    }

    private GDictionary ExecuteWorldCommand(List<string> tokens)
    {
        GDictionary ensureResult = EnsureWorldContext();
        if (!GdInterop.GetBool(ensureResult, "ok", false))
            return ensureResult;
        GodotObject runtime = _session.get_runtime_facade();
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
                    GDictionary countResult = ParseIntArgument(tokens[3], "移动次数");
                    if (!GdInterop.GetBool(countResult, "ok", false))
                        return countResult;
                    count = GdInterop.GetInt(countResult, "value", 1);
                }
                return ToDictionary(Call(runtime, "command_world_move", direction, count));
            }
            case "select":
            {
                if (tokens.Count < 4)
                    return Result(false, "用法: world select <x> <y>");
                GDictionary coordResult = ParseCoordArgument(tokens[2], tokens[3], "世界坐标");
                if (!GdInterop.GetBool(coordResult, "ok", false))
                    return coordResult;
                return ToDictionary(
                    Call(
                        runtime,
                        "command_world_select",
                        GdInterop.GetVector2I(coordResult, "value")
                    )
                );
            }
            case "open":
            {
                if (tokens.Count >= 4)
                {
                    GDictionary coordResult = ParseCoordArgument(tokens[2], tokens[3], "聚落坐标");
                    if (!GdInterop.GetBool(coordResult, "ok", false))
                        return coordResult;
                    return ToDictionary(
                        Call(
                            runtime,
                            "command_open_settlement",
                            GdInterop.GetVector2I(coordResult, "value")
                        )
                    );
                }
                return ToDictionary(Call(runtime, "command_open_settlement"));
            }
            case "inspect":
            {
                if (tokens.Count < 4)
                    return Result(false, "用法: world inspect <x> <y>");
                GDictionary coordResult = ParseCoordArgument(tokens[2], tokens[3], "世界坐标");
                if (!GdInterop.GetBool(coordResult, "ok", false))
                    return coordResult;
                return ToDictionary(
                    Call(
                        runtime,
                        "command_world_inspect",
                        GdInterop.GetVector2I(coordResult, "value")
                    )
                );
            }
            case "click":
            {
                if (tokens.Count < 4)
                    return Result(false, "用法: world click <x> <y>");
                GDictionary coordResult = ParseCoordArgument(tokens[2], tokens[3], "世界坐标");
                if (!GdInterop.GetBool(coordResult, "ok", false))
                    return coordResult;
                return ToDictionary(
                    Call(runtime, "select_world_cell", GdInterop.GetVector2I(coordResult, "value"))
                );
            }
            default:
                return Result(false, $"未知 world 子命令 {tokens[1]}。");
        }
    }

    private GDictionary ExecuteSubmapCommand(List<string> tokens)
    {
        GDictionary ensureResult = EnsureWorldContext();
        if (!GdInterop.GetBool(ensureResult, "ok", false))
            return ensureResult;
        GodotObject runtime = _session.get_runtime_facade();
        if (runtime == null)
            return MissingWorldError();
        if (tokens.Count < 2)
            return Result(false, "用法: submap confirm|cancel|return");
        return tokens[1] switch
        {
            "confirm" => ToDictionary(Call(runtime, "command_confirm_submap_entry")),
            "cancel" => ToDictionary(Call(runtime, "command_cancel_submap_entry")),
            "return" => ToDictionary(Call(runtime, "command_return_from_submap")),
            _ => Result(false, $"未知 submap 子命令 {tokens[1]}。"),
        };
    }

    private GDictionary ExecutePartyCommand(List<string> tokens)
    {
        GDictionary ensureResult = EnsureWorldContext();
        if (!GdInterop.GetBool(ensureResult, "ok", false))
            return ensureResult;
        GodotObject runtime = _session.get_runtime_facade();
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
                return ToDictionary(Call(runtime, "command_open_party"));
            case "select":
                if (tokens.Count < 3)
                    return Result(false, "用法: party select <member_id>");
                return ToDictionary(
                    Call(runtime, "command_select_party_member", new StringName(tokens[2]))
                );
            case "leader":
                if (tokens.Count < 3)
                    return Result(false, "用法: party leader <member_id>");
                return ToDictionary(
                    Call(runtime, "command_set_party_leader", new StringName(tokens[2]))
                );
            case "activate":
                if (tokens.Count < 3)
                    return Result(false, "用法: party activate <member_id>");
                return ToDictionary(
                    Call(runtime, "command_move_member_to_active", new StringName(tokens[2]))
                );
            case "reserve":
                if (tokens.Count < 3)
                    return Result(false, "用法: party reserve <member_id>");
                return ToDictionary(
                    Call(runtime, "command_move_member_to_reserve", new StringName(tokens[2]))
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
                GDictionary args = ParseNamedArgs(tokens, argsStart);
                return ToDictionary(
                    Call(
                        runtime,
                        "command_party_equip_item",
                        new StringName(tokens[2]),
                        new StringName(tokens[3]),
                        slotId,
                        new StringName(GdInterop.GetString(args, "instance_id"))
                    )
                );
            }
            case "unequip":
                if (tokens.Count < 4)
                    return Result(false, "用法: party unequip <member_id> <slot_id>");
                return ToDictionary(
                    Call(
                        runtime,
                        "command_party_unequip_item",
                        new StringName(tokens[2]),
                        new StringName(tokens[3])
                    )
                );
            case "warehouse":
                return ToDictionary(Call(runtime, "command_open_party_warehouse"));
            default:
                return Result(false, $"未知 party 子命令 {tokens[1]}。");
        }
    }

    private GDictionary ExecuteQuestCommand(List<string> tokens)
    {
        GDictionary ensureResult = EnsureWorldContext();
        if (!GdInterop.GetBool(ensureResult, "ok", false))
            return ensureResult;
        GodotObject runtime = _session.get_runtime_facade();
        if (runtime == null)
            return MissingWorldError();
        if (tokens.Count < 2)
            return Result(false, "用法: quest accept|progress|complete <quest_id> ...");
        switch (tokens[1])
        {
            case "accept":
                if (tokens.Count < 3)
                    return Result(false, "用法: quest accept <quest_id>");
                return ToDictionary(
                    Call(runtime, "command_accept_quest", new StringName(tokens[2]))
                );
            case "progress":
            {
                if (tokens.Count < 5)
                    return Result(
                        false,
                        "用法: quest progress <quest_id> <objective_id> <amount> [key=value ...]"
                    );
                GDictionary amountResult = ParseIntArgument(tokens[4], "任务进度增量");
                if (!GdInterop.GetBool(amountResult, "ok", false))
                    return amountResult;
                return ToDictionary(
                    Call(
                        runtime,
                        "command_progress_quest",
                        new StringName(tokens[2]),
                        new StringName(tokens[3]),
                        GdInterop.GetInt(amountResult, "value"),
                        ParseNamedArgs(tokens, 5)
                    )
                );
            }
            case "complete":
                if (tokens.Count < 3)
                    return Result(false, "用法: quest complete <quest_id>");
                return ToDictionary(
                    Call(runtime, "command_complete_quest", new StringName(tokens[2]))
                );
            default:
                return Result(false, $"未知 quest 子命令 {tokens[1]}。");
        }
    }

    private GDictionary ExecuteSettlementCommand(List<string> tokens)
    {
        GDictionary ensureResult = EnsureWorldContext();
        if (!GdInterop.GetBool(ensureResult, "ok", false))
            return ensureResult;
        GodotObject runtime = _session.get_runtime_facade();
        if (runtime == null)
            return MissingWorldError();
        if (tokens.Count < 3 || tokens[1] != "action")
            return Result(false, "用法: settlement action <action_id> [key=value ...]");
        return ToDictionary(
            Call(runtime, "command_execute_settlement_action", tokens[2], ParseNamedArgs(tokens, 3))
        );
    }

    private GDictionary ExecuteShopCommand(List<string> tokens)
    {
        GDictionary ensureResult = EnsureWorldContext();
        if (!GdInterop.GetBool(ensureResult, "ok", false))
            return ensureResult;
        GodotObject runtime = _session.get_runtime_facade();
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
            GDictionary quantityResult = ParseIntArgument(tokens[3], "商品数量");
            if (!GdInterop.GetBool(quantityResult, "ok", false))
                return quantityResult;
            quantity = GdInterop.GetInt(quantityResult, "value", 1);
            argsStart = 4;
        }
        GDictionary args = ParseNamedArgs(tokens, argsStart);
        return tokens[1] switch
        {
            "buy" => ToDictionary(
                Call(runtime, "command_shop_buy", new StringName(tokens[2]), quantity)
            ),
            "sell" => ToDictionary(
                Call(
                    runtime,
                    "command_shop_sell",
                    new StringName(tokens[2]),
                    quantity,
                    new StringName(GdInterop.GetString(args, "instance_id"))
                )
            ),
            _ => Result(false, $"未知 shop 子命令 {tokens[1]}。"),
        };
    }

    private GDictionary ExecuteStagecoachCommand(List<string> tokens)
    {
        GDictionary ensureResult = EnsureWorldContext();
        if (!GdInterop.GetBool(ensureResult, "ok", false))
            return ensureResult;
        GodotObject runtime = _session.get_runtime_facade();
        if (runtime == null)
            return MissingWorldError();
        if (tokens.Count < 3 || tokens[1] != "travel")
            return Result(false, "用法: stagecoach travel <settlement_id>");
        return ToDictionary(Call(runtime, "command_stagecoach_travel", tokens[2]));
    }

    private GDictionary ExecuteWarehouseCommand(List<string> tokens)
    {
        GDictionary ensureResult = EnsureWorldContext();
        if (!GdInterop.GetBool(ensureResult, "ok", false))
            return ensureResult;
        GodotObject runtime = _session.get_runtime_facade();
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
                GDictionary quantityResult = ParseIntArgument(tokens[3], "仓库数量");
                if (!GdInterop.GetBool(quantityResult, "ok", false))
                    return quantityResult;
                return ToDictionary(
                    Call(
                        runtime,
                        "command_warehouse_add_item",
                        new StringName(tokens[2]),
                        GdInterop.GetInt(quantityResult, "value")
                    )
                );
            }
            case "use":
            {
                var options = new GDictionary();
                if (tokens.Contains("confirm"))
                    options["confirm_practice_replacement"] = true;
                StringName memberId = "";
                if (tokens.Count >= 4 && tokens[3] != "confirm")
                    memberId = new StringName(tokens[3]);
                return ToDictionary(
                    Call(
                        runtime,
                        "command_warehouse_use_item",
                        new StringName(tokens[2]),
                        memberId,
                        options
                    )
                );
            }
            case "capacity":
            {
                GDictionary capacityResult = ParseIntArgument(tokens[2], "仓库容量");
                if (!GdInterop.GetBool(capacityResult, "ok", false))
                    return capacityResult;
                return _session.set_party_storage_capacity(
                    GdInterop.GetInt(capacityResult, "value")
                );
            }
            case "discard-one":
            {
                GDictionary args = ParseNamedArgs(tokens, 3);
                return ToDictionary(
                    Call(
                        runtime,
                        "command_warehouse_discard_one",
                        new StringName(tokens[2]),
                        new StringName(GdInterop.GetString(args, "instance_id"))
                    )
                );
            }
            case "discard-all":
            {
                GDictionary args = ParseNamedArgs(tokens, 3);
                return ToDictionary(
                    Call(
                        runtime,
                        "command_warehouse_discard_all",
                        new StringName(tokens[2]),
                        new StringName(GdInterop.GetString(args, "instance_id"))
                    )
                );
            }
            default:
                return Result(false, $"未知 warehouse 子命令 {tokens[1]}。");
        }
    }

    private GDictionary ExecuteBattleCommand(List<string> tokens)
    {
        GDictionary ensureResult = EnsureWorldContext();
        if (!GdInterop.GetBool(ensureResult, "ok", false))
            return ensureResult;
        GodotObject runtime = _session.get_runtime_facade();
        if (runtime == null)
            return MissingWorldError();
        if (tokens.Count < 2)
            return Result(
                false,
                "用法: battle start/confirm/tick/skill/option/move/equip/unequip/wait/inspect/finish ..."
            );

        switch (tokens[1])
        {
            case "start":
                if (tokens.Count < 3)
                    return Result(false, "用法: battle start <settlement|single>");
                return _session.start_battle_by_kind(new StringName(tokens[2]));
            case "confirm":
                return ToDictionary(Call(runtime, "command_confirm_battle_start"));
            case "tick":
            {
                if (tokens.Count < 3)
                    return Result(false, "用法: battle tick <ticks>");
                GDictionary tickResult = ParseIntArgument(tokens[2], "战斗推进 tick");
                if (!GdInterop.GetBool(tickResult, "ok", false))
                    return tickResult;
                return ToDictionary(
                    Call(runtime, "command_battle_tick", GdInterop.GetInt(tickResult, "value"))
                );
            }
            case "skill":
            {
                if (tokens.Count < 3)
                    return Result(false, "用法: battle skill <slot>");
                GDictionary slotResult = ParseIntArgument(tokens[2], "技能栏位");
                if (!GdInterop.GetBool(slotResult, "ok", false))
                    return slotResult;
                return ToDictionary(
                    Call(
                        runtime,
                        "command_battle_select_skill",
                        GdInterop.GetInt(slotResult, "value") - 1
                    )
                );
            }
            case "option":
                if (tokens.Count < 3)
                    return Result(false, "用法: battle option <next|prev>");
                return ToDictionary(
                    Call(runtime, "command_battle_cycle_variant", tokens[2] == "next" ? 1 : -1)
                );
            case "move":
                if (tokens.Count == 3)
                    return ToDictionary(
                        Call(runtime, "command_battle_move_direction", ParseDirection(tokens[2]))
                    );
                if (tokens.Count >= 4)
                {
                    GDictionary coordResult = ParseCoordArgument(tokens[2], tokens[3], "战斗坐标");
                    if (!GdInterop.GetBool(coordResult, "ok", false))
                        return coordResult;
                    return ToDictionary(
                        Call(
                            runtime,
                            "command_battle_move_to",
                            GdInterop.GetVector2I(coordResult, "value")
                        )
                    );
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
                GDictionary args = ParseNamedArgs(tokens, 4);
                return _session.change_battle_equipment(
                    "equip",
                    new StringName(tokens[2]),
                    new StringName(tokens[3]),
                    new StringName(GdInterop.GetString(args, "instance_id")),
                    args
                );
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
                GDictionary args = ParseNamedArgs(tokens, argsStart);
                if (string.IsNullOrEmpty(instanceId))
                    instanceId = GdInterop.GetString(args, "instance_id");
                return _session.change_battle_equipment(
                    "unequip",
                    new StringName(tokens[2]),
                    "",
                    new StringName(instanceId),
                    args
                );
            }
            case "wait":
                return ToDictionary(Call(runtime, "command_battle_wait_or_resolve"));
            case "inspect":
            {
                if (tokens.Count < 4)
                    return Result(false, "用法: battle inspect <x> <y>");
                GDictionary coordResult = ParseCoordArgument(tokens[2], tokens[3], "战斗坐标");
                if (!GdInterop.GetBool(coordResult, "ok", false))
                    return coordResult;
                return ToDictionary(
                    Call(
                        runtime,
                        "command_battle_inspect",
                        GdInterop.GetVector2I(coordResult, "value")
                    )
                );
            }
            case "finish":
                if (tokens.Count < 3)
                    return Result(false, "用法: battle finish <player|hostile>");
                return _session.finish_active_battle(new StringName(tokens[2]));
            case "clear":
                return ToDictionary(Call(runtime, "command_battle_clear_skill"));
            default:
                return Result(false, $"未知 battle 子命令 {tokens[1]}。");
        }
    }

    private GDictionary ExecuteRewardCommand(List<string> tokens)
    {
        GDictionary ensureResult = EnsureWorldContext();
        if (!GdInterop.GetBool(ensureResult, "ok", false))
            return ensureResult;
        GodotObject runtime = _session.get_runtime_facade();
        if (runtime == null)
            return MissingWorldError();
        if (tokens.Count < 2 || tokens[1] != "confirm")
            return Result(false, "用法: reward confirm");
        return ToDictionary(Call(runtime, "command_confirm_pending_reward"));
    }

    private GDictionary ExecutePromotionCommand(List<string> tokens)
    {
        GDictionary ensureResult = EnsureWorldContext();
        if (!GdInterop.GetBool(ensureResult, "ok", false))
            return ensureResult;
        GodotObject runtime = _session.get_runtime_facade();
        if (runtime == null)
            return MissingWorldError();
        if (tokens.Count < 3 || tokens[1] != "choose")
            return Result(false, "用法: promotion choose <profession_id>");
        return ToDictionary(Call(runtime, "command_choose_promotion", new StringName(tokens[2])));
    }

    private GDictionary ExecuteCloseCommand(List<string> tokens)
    {
        GDictionary ensureResult = EnsureWorldContext();
        if (!GdInterop.GetBool(ensureResult, "ok", false))
            return ensureResult;
        GodotObject runtime = _session.get_runtime_facade();
        return runtime == null
            ? MissingWorldError()
            : ToDictionary(Call(runtime, "command_close_active_modal"));
    }

    private GDictionary ExecuteExpect(List<string> tokens, GDictionary snapshot)
    {
        if (tokens.Count < 3)
            return ExpectError("用法: expect status/window/field/list ...", "", "");
        switch (tokens[1])
        {
            case "status":
            {
                if (tokens.Count < 4 || tokens[2] != "contains")
                    return ExpectError("expect status contains <text>", "", "");
                string statusText = GdInterop.GetString(
                    GdInterop.GetDictionary(snapshot, "status"),
                    "text"
                );
                string expectedText = JoinTokens(tokens, 3);
                return statusText.Contains(expectedText, StringComparison.Ordinal)
                    ? ExpectOk($"status contains {expectedText}", statusText, expectedText)
                    : ExpectError($"status contains {expectedText}", statusText, expectedText);
            }
            case "window":
            {
                if (tokens.Count < 4 || tokens[2] != "==")
                    return ExpectError("expect window == <id>", "", "");
                string actualWindow = GdInterop.GetString(
                    GdInterop.GetDictionary(snapshot, "modal"),
                    "id"
                );
                string expectedWindow = tokens[3];
                return actualWindow == expectedWindow
                    ? ExpectOk($"window == {expectedWindow}", actualWindow, expectedWindow)
                    : ExpectError($"window == {expectedWindow}", actualWindow, expectedWindow);
            }
            case "field":
            {
                if (tokens.Count < 5 || tokens[3] != "==")
                    return ExpectError("expect field <path> == <value>", "", "");
                GDictionary actualField = ResolvePath(snapshot, tokens[2]);
                if (!GdInterop.GetBool(actualField, "ok", false))
                    return ExpectError(GdInterop.GetString(actualField, "message"), "", tokens[4]);
                object expectedValue = ParseScalar(JoinTokens(tokens, 4));
                object actualValue = VariantToObject(
                    GdInterop.TryGet(actualField, "value", out var _fieldVal) ? _fieldVal : default
                );
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
                GDictionary actualList = ResolvePath(snapshot, tokens[2]);
                if (!GdInterop.GetBool(actualList, "ok", false))
                    return ExpectError(GdInterop.GetString(actualList, "message"), "", tokens[4]);
                object listValue = VariantToObject(
                    GdInterop.TryGet(actualList, "value", out var _listVal) ? _listVal : default
                );
                if (listValue is not GArray array)
                    return ExpectError(
                        $"path {tokens[2]} is not a list",
                        StringifyValue(listValue),
                        tokens[4]
                    );
                object expectedItem = ParseScalar(JoinTokens(tokens, 4));
                foreach (var itemValue in array)
                {
                    if (ValuesEqual(VariantToObject(itemValue), expectedItem))
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
                GDictionary expectedQuantityResult = ParseIntArgument(
                    JoinTokens(tokens, 4),
                    "期望仓库数量"
                );
                if (!GdInterop.GetBool(expectedQuantityResult, "ok", false))
                    return ExpectError(
                        GdInterop.GetString(expectedQuantityResult, "message"),
                        "",
                        JoinTokens(tokens, 4)
                    );
                int expectedQuantity = GdInterop.GetInt(expectedQuantityResult, "value");
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

    private GDictionary EnsureWorldContext()
    {
        return _session.ensure_world_loaded();
    }

    private static GDictionary MissingWorldError()
    {
        return Result(false, "当前世界地图不可用。");
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

    private static GDictionary ParseIntArgument(string token, string label)
    {
        object value = ParseScalar(token);
        if (value is int intValue)
            return new GDictionary { ["ok"] = true, ["value"] = intValue };
        return Result(false, $"{label} 必须是整数，收到 {token}。");
    }

    private static GDictionary ParseCoordArgument(string xToken, string yToken, string label)
    {
        GDictionary xResult = ParseIntArgument(xToken, $"{label} X");
        if (!GdInterop.GetBool(xResult, "ok", false))
            return xResult;
        GDictionary yResult = ParseIntArgument(yToken, $"{label} Y");
        if (!GdInterop.GetBool(yResult, "ok", false))
            return yResult;
        return new GDictionary
        {
            ["ok"] = true,
            ["value"] = new Vector2I(
                GdInterop.GetInt(xResult, "value"),
                GdInterop.GetInt(yResult, "value")
            ),
        };
    }

    private static GDictionary ParseNamedArgs(List<string> tokens, int startIndex)
    {
        var result = new GDictionary();
        for (int index = startIndex; index < tokens.Count; index++)
        {
            string token = tokens[index];
            int equalsIndex = token.IndexOf('=', StringComparison.Ordinal);
            if (equalsIndex <= 0)
                continue;
            string key = token[..equalsIndex];
            string valueText = token[(equalsIndex + 1)..];
            result[key] = GdInterop.ToVariant(ObjectToValue(ParseScalar(valueText)));
        }
        return result;
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

    private static GDictionary ResolvePath(object root, string path)
    {
        object current = root;
        foreach (string segment in (path ?? "").Split('.'))
        {
            current = UnwrapValue(current);
            if (current is GDictionary dictionary)
            {
                if (!GdInterop.TryGet(dictionary, segment, out var value))
                    return Result(false, $"path {path} is missing at {segment}");
                current = value;
                continue;
            }
            if (current is GArray array)
            {
                if (
                    !int.TryParse(
                        segment,
                        NumberStyles.Integer,
                        CultureInfo.GetCultureInfo(""),
                        out int arrayIndex
                    )
                )
                    return Result(false, $"path {path} expected numeric index at {segment}");
                if (arrayIndex < 0 || arrayIndex >= array.Count)
                    return Result(false, $"path {path} index out of range at {segment}");
                current = array[arrayIndex];
                continue;
            }
            return Result(false, $"path {path} cannot descend into {segment}");
        }
        return new GDictionary
        {
            ["ok"] = true,
            ["value"] = GdInterop.ToVariant(ObjectToValue(UnwrapValue(current))),
        };
    }

    private static bool ValuesEqual(object actual, object expected)
    {
        actual = UnwrapValue(actual);
        expected = UnwrapValue(expected);
        if (
            actual is GDictionary
            || actual is GArray
            || expected is GDictionary
            || expected is GArray
        )
            return StringifyValue(actual) == StringifyValue(expected);
        if (actual is double actualDouble && expected is float expectedFloat)
            return Math.Abs(actualDouble - expectedFloat) < 0.0001;
        if (actual is float actualFloat && expected is double expectedDouble)
            return Math.Abs(actualFloat - expectedDouble) < 0.0001;
        return Equals(actual, expected);
    }

    private static string StringifyValue(object value)
    {
        value = UnwrapValue(value);
        return value switch
        {
            GDictionary dictionary => Json.Stringify(dictionary),
            GArray array => Json.Stringify(array),
            bool boolValue => boolValue ? "true" : "false",
            float floatValue => floatValue.ToString(CultureInfo.GetCultureInfo("")),
            double doubleValue => doubleValue.ToString(CultureInfo.GetCultureInfo("")),
            int intValue => intValue.ToString(CultureInfo.GetCultureInfo("")),
            _ => value?.ToString() ?? "",
        };
    }

    private static string StringifyForSummary(object value)
    {
        value = UnwrapValue(value);
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

    private static int GetWarehouseItemTotal(GDictionary snapshot, string itemId)
    {
        GDictionary warehouse = GdInterop.GetDictionary(snapshot, "warehouse");
        GDictionary windowData = GdInterop.GetDictionary(warehouse, "window_data");
        GArray entries = GdInterop.GetArray(windowData, "entries");
        foreach (GDictionary entry in GdInterop.ReadDictionaryItems(entries))
        {
            if (GdInterop.GetString(entry, "item_id") != itemId)
                continue;
            return GdInterop.GetInt(entry, "total_quantity");
        }
        return 0;
    }

    private static GDictionary ExpectOk(string summary, string actual, string expected)
    {
        return new GDictionary
        {
            ["ok"] = true,
            ["message"] = "Expectation passed.",
            ["summary"] = summary,
            ["actual"] = actual,
            ["expected"] = expected,
        };
    }

    private static GDictionary ExpectError(string summary, string actual, string expected)
    {
        return new GDictionary
        {
            ["ok"] = false,
            ["message"] = "Expectation failed.",
            ["summary"] = summary,
            ["actual"] = actual,
            ["expected"] = expected,
        };
    }

    private static GDictionary Result(bool ok, string message)
    {
        return new GDictionary { ["ok"] = ok, ["message"] = message ?? "" };
    }

    private static Variant Call(GodotObject target, StringName method, params object[] args)
    {
        if (target == null)
        {
            return default;
        }
        var values = new Variant[args?.Length ?? 0];
        for (int index = 0; index < values.Length; index++)
        {
            values[index] = GdInterop.ToVariant(args[index]);
        }
        return target.Call(method, values);
    }

    private static GDictionary ToDictionary(Variant rawValue)
    {
        return rawValue.VariantType == Variant.Type.Dictionary
            ? rawValue.AsGodotDictionary()
            : new GDictionary();
    }

    private static object VariantToObject(Variant rawValue)
    {
        return rawValue.VariantType switch
        {
            Variant.Type.Nil => (object)null,
            Variant.Type.Bool => rawValue.AsBool(),
            Variant.Type.Int => rawValue.AsInt32(),
            Variant.Type.Float => rawValue.AsDouble(),
            Variant.Type.String => rawValue.AsString(),
            Variant.Type.StringName => rawValue.AsStringName().ToString(),
            Variant.Type.Vector2I => rawValue.AsVector2I(),
            Variant.Type.Dictionary => rawValue.AsGodotDictionary(),
            Variant.Type.Array => rawValue.AsGodotArray(),
            _ => rawValue,
        };
    }

    private static object UnwrapValue(object value)
    {
        return value is Variant option ? VariantToObject(option) : value;
    }

    private static object ObjectToValue(object value)
    {
        value = UnwrapValue(value);
        return value switch
        {
            null => null,
            bool boolValue => boolValue,
            int intValue => intValue,
            float floatValue => floatValue,
            double doubleValue => doubleValue,
            string stringValue => stringValue,
            Vector2I vector2IValue => vector2IValue,
            GDictionary dictionary => dictionary,
            GArray array => array,
            StringName stringName => stringName,
            GodotObject godotObject => godotObject,
            _ => value.ToString(),
        };
    }
}
