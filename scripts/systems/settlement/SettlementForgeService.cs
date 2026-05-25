using Godot;

[GlobalClass]
public partial class SettlementForgeService : RefCounted
{
    private const string MASTER_REFORGE_INTERACTION_ID = "service_master_reforge";
    private static readonly Godot.Collections.Dictionary GENERIC_FORGE_INTERACTION_IDS = new() { { "service_repair_gear", true } };
    private readonly RecipeContentRegistry _recipe_registry = new RecipeContentRegistry();

    public bool is_supported_interaction(string interactionScriptId) { var n = interactionScriptId.StripEdges(); return n == MASTER_REFORGE_INTERACTION_ID || GENERIC_FORGE_INTERACTION_IDS.ContainsKey(n); }

    public bool has_available_recipe(Godot.Collections.Dictionary settlement, Godot.Collections.Dictionary payload, Godot.Collections.Dictionary itemDefs, Godot.Collections.Dictionary recipeDefs = null) { var rrd = _resolve_recipe_defs(itemDefs, recipeDefs ?? new Godot.Collections.Dictionary()); return rrd.Count > 0 && _resolve_recipe(settlement, payload, rrd, null) != null; }
    public bool has_available_master_reforge_recipe(Godot.Collections.Dictionary settlement, Godot.Collections.Dictionary payload, Godot.Collections.Dictionary itemDefs, Godot.Collections.Dictionary recipeDefs = null) => has_available_recipe(settlement, payload, itemDefs, recipeDefs);

    public Godot.Collections.Dictionary execute_recipe(Godot.Collections.Dictionary settlement, Godot.Collections.Dictionary payload, Godot.Collections.Dictionary itemDefs, Godot.Collections.Dictionary recipeDefs, GodotObject warehouseService, GodotObject partyState, Godot.Collections.Array questProgressEvents = null)
    {
        questProgressEvents ??= new Godot.Collections.Array();
        var sp = _resolve_service_profile(payload);
        if (warehouseService == null || partyState == null) return _build_result(false, "当前工坊服务尚未准备完成。", questProgressEvents);
        var rrd = _resolve_recipe_defs(itemDefs, recipeDefs); if (rrd.Count == 0) return _build_result(false, "当前配方配置缺失，暂时无法执行。", questProgressEvents);
        var recipe = _resolve_recipe(settlement, payload, rrd, warehouseService); if (recipe == null) return _build_result(false, sp.ContainsKey("no_recipe_message") ? sp["no_recipe_message"].AsString() : "当前工坊没有可执行的配方。", questProgressEvents);
        var iv = _validate_recipe_items(recipe, itemDefs); if (!iv.ContainsKey("ok") || !iv["ok"].AsBool()) return _build_result(false, iv.ContainsKey("message") ? iv["message"].AsString() : "当前配方引用了无效物品。", questProgressEvents);
        var wi = _expand_input_items(recipe); var di = _build_repeated_item_array(recipe.output_item_id, recipe.output_quantity);
        var preview = warehouseService.Call("preview_batch_swap", wi, di).AsGodotDictionary(); if (!preview.ContainsKey("allowed") || !preview["allowed"].AsBool()) return _build_result(false, _build_failed_forge_message(recipe, itemDefs, warehouseService, preview, payload), questProgressEvents);
        var commit = warehouseService.Call("commit_batch_swap", wi, di).AsGodotDictionary(); if (!commit.ContainsKey("allowed") || !commit["allowed"].AsBool()) return _build_result(false, _build_failed_forge_message(recipe, itemDefs, warehouseService, commit, payload), questProgressEvents);
        var oid = itemDefs.ContainsKey(recipe.output_item_id) ? itemDefs[recipe.output_item_id].AsGodotObject() : null;
        var msg = _build_success_message(recipe, itemDefs, settlement, payload, oid);
        var changes = new Godot.Collections.Dictionary { {"recipe_id",(string)recipe.recipe_id},{"removed_entries",_build_recipe_entry_variants(recipe.input_item_ids, recipe.input_item_quantities)},{"added_entries",_build_recipe_entry_variants(new Godot.Collections.Array<StringName>{recipe.output_item_id}, new int[]{recipe.output_quantity})} };
        return _build_result(true, msg, questProgressEvents, true, changes, new Godot.Collections.Dictionary { {"settlement_id", settlement.ContainsKey("settlement_id") ? settlement["settlement_id"].AsString() : ""} });
    }

    private static Godot.Collections.Dictionary _resolve_recipe_defs(Godot.Collections.Dictionary itemDefs, Godot.Collections.Dictionary recipeDefs) { var r = new Godot.Collections.Dictionary(); foreach (var kv in recipeDefs) { var rd = kv.Value.AsGodotObject() as RecipeDef; if (rd != null) r[kv.Key] = rd; } var rr = new RecipeContentRegistry(); rr.setup(itemDefs); foreach (var kv in rr.get_recipe_defs()) { var k = kv.Key.AsStringName(); if (!r.ContainsKey(k)) r[k] = kv.Value; } return r; }

    private static RecipeDef _resolve_recipe(Godot.Collections.Dictionary settlement, Godot.Collections.Dictionary payload, Godot.Collections.Dictionary recipeDefs, GodotObject warehouseService) { foreach (var kv in recipeDefs) { var rd = kv.Value.AsGodotObject() as RecipeDef; if (rd == null) continue; if (!_recipe_matches_facility(rd, settlement)) continue; if (warehouseService != null && !_can_fulfill_recipe_inputs(rd, warehouseService)) continue; return rd; } return null; }

    private static bool _recipe_matches_facility(RecipeDef rd, Godot.Collections.Dictionary settlement) { var ftv = settlement.ContainsKey("facility_tags") ? settlement["facility_tags"] : default(Variant); if (ftv.VariantType != Variant.Type.Array) return false; var facilityTags = new Godot.Collections.Dictionary(); foreach (var ft in ftv.AsGodotArray()) facilityTags[ProgressionDataUtils.to_string_name(ft)] = true; foreach (var rt in rd.required_facility_tags) if (!facilityTags.ContainsKey(rt)) return false; return true; }

    private static bool _can_fulfill_recipe_inputs(RecipeDef rd, GodotObject warehouseService) { for (int i = 0; i < rd.input_item_ids.Count; i++) { if (warehouseService.Call("count_item", rd.input_item_ids[i]).AsInt32() < rd.input_item_quantities[i]) return false; } return true; }

    private static Godot.Collections.Dictionary _validate_recipe_items(RecipeDef rd, Godot.Collections.Dictionary itemDefs) { for (int i = 0; i < rd.input_item_ids.Count; i++) { if (!itemDefs.ContainsKey(rd.input_item_ids[i])) return new Godot.Collections.Dictionary { {"ok",false},{"message",$"缺少输入物品 {rd.input_item_ids[i]} "} }; } if (!itemDefs.ContainsKey(rd.output_item_id)) return new Godot.Collections.Dictionary { {"ok",false},{"message",$"缺少输出物品 {rd.output_item_id} "} }; return new Godot.Collections.Dictionary { {"ok",true} }; }

    private static Godot.Collections.Array _expand_input_items(RecipeDef rd) { var r = new Godot.Collections.Array(); for (int i = 0; i < rd.input_item_ids.Count; i++) for (int j = 0; j < rd.input_item_quantities[i]; j++) r.Add(rd.input_item_ids[i]); return r; }
    private static Godot.Collections.Array _build_repeated_item_array(StringName itemId, int count) { var r = new Godot.Collections.Array(); for (int i = 0; i < count; i++) r.Add(itemId); return r; }

    private static string _build_failed_forge_message(RecipeDef rd, Godot.Collections.Dictionary itemDefs, GodotObject ws, Godot.Collections.Dictionary result, Godot.Collections.Dictionary payload) { var oid = itemDefs.ContainsKey(rd.output_item_id) ? itemDefs[rd.output_item_id].AsGodotObject() : null; string on = oid?.Get("display_name").AsString() ?? (string)rd.output_item_id; if (result.ContainsKey("message") && result["message"].AsString().Length > 0) return result["message"].AsString(); return $"重铸 {on} 失败，材料不足。"; }

    private static string _build_success_message(RecipeDef rd, Godot.Collections.Dictionary itemDefs, Godot.Collections.Dictionary settlement, Godot.Collections.Dictionary payload, GodotObject outputItemDef) { string on = outputItemDef?.Get("display_name").AsString() ?? (string)rd.output_item_id; string fn = settlement.ContainsKey("facility_name") ? settlement["facility_name"].AsString() : "工坊"; return $"在{fn}成功重铸了 {on}。"; }

    private static Godot.Collections.Array _build_recipe_entry_variants(Godot.Collections.Array<StringName> ids, int[] quantities) { var r = new Godot.Collections.Array(); for (int i = 0; i < ids.Count && i < quantities.Length; i++) r.Add(new Godot.Collections.Dictionary { {"item_id",(string)ids[i]},{"quantity",quantities[i]} }); return r; }

    private static Godot.Collections.Dictionary _resolve_service_profile(Godot.Collections.Dictionary payload) { return new Godot.Collections.Dictionary { {"no_recipe_message","当前工坊没有可执行的配方。"} }; }

    private static Godot.Collections.Dictionary _build_result(bool ok, string message, Godot.Collections.Array questProgressEvents, bool hasChanges = false, Godot.Collections.Dictionary changes = null, Godot.Collections.Dictionary settlementContext = null) { return new Godot.Collections.Dictionary { {"ok",ok},{"message",message},{"quest_progress_events",questProgressEvents},{"has_changes",hasChanges},{"changes",changes??new Godot.Collections.Dictionary()},{"settlement_context",settlementContext??new Godot.Collections.Dictionary()} }; }
}

