using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

[GlobalClass]
public partial class SettlementForgeService : RefCounted
{
    private const string MasterReforgeInteractionId = "service_master_reforge";
    private static readonly GDictionary GenericForgeInteractionIds = new()
    {
        ["service_repair_gear"] = true,
    };

    private readonly RecipeContentRegistry _recipeRegistry = new();

    public bool is_supported_interaction(string interaction_script_id)
    {
        string normalizedInteractionId = (interaction_script_id ?? "").StripEdges();
        return normalizedInteractionId == MasterReforgeInteractionId || GenericForgeInteractionIds.ContainsKey(normalizedInteractionId);
    }

    public bool has_available_recipe(GDictionary settlement, GDictionary payload, GDictionary item_defs, GDictionary recipe_defs = null)
    {
        GDictionary resolvedRecipeDefs = _resolve_recipe_defs(item_defs, recipe_defs ?? new GDictionary());
        if (resolvedRecipeDefs.Count == 0)
        {
            return false;
        }
        return _resolve_recipe(settlement ?? new GDictionary(), payload ?? new GDictionary(), resolvedRecipeDefs, null) != null;
    }

    public bool has_available_master_reforge_recipe(GDictionary settlement, GDictionary payload, GDictionary item_defs, GDictionary recipe_defs = null)
    {
        return has_available_recipe(settlement, payload, item_defs, recipe_defs);
    }

    public GDictionary execute_recipe(
        GDictionary settlement,
        GDictionary payload,
        GDictionary item_defs,
        GDictionary recipe_defs,
        GodotObject warehouse_service,
        GodotObject party_state,
        GArray quest_progress_events = null)
    {
        settlement ??= new GDictionary();
        payload ??= new GDictionary();
        item_defs ??= new GDictionary();
        recipe_defs ??= new GDictionary();
        quest_progress_events ??= new GArray();

        GDictionary serviceProfile = _resolve_service_profile(payload);
        if (warehouse_service == null || party_state == null)
        {
            return _build_result(false, "当前工坊服务尚未准备完成。", quest_progress_events);
        }

        GDictionary resolvedRecipeDefs = _resolve_recipe_defs(item_defs, recipe_defs);
        if (resolvedRecipeDefs.Count == 0)
        {
            return _build_result(false, "当前配方配置缺失，暂时无法执行。", quest_progress_events);
        }

        RecipeDef recipe = _resolve_recipe(settlement, payload, resolvedRecipeDefs, warehouse_service);
        if (recipe == null)
        {
            return _build_result(false, GdInterop.GetString(serviceProfile, "no_recipe_message", "当前工坊没有可执行的配方。"), quest_progress_events);
        }

        GDictionary inputValidation = _validate_recipe_items(recipe, item_defs);
        if (!GdInterop.GetBool(inputValidation, "ok", false))
        {
            return _build_result(false, GdInterop.GetString(inputValidation, "message", "当前配方引用了无效物品。"), quest_progress_events);
        }

        var warehouse = (PartyWarehouseService)warehouse_service;
        GArray withdrawalItems = _expand_input_items(recipe);
        GArray depositItems = _build_repeated_item_array(recipe.output_item_id, recipe.output_quantity);
        GDictionary previewResult = warehouse.preview_batch_swap_entries(withdrawalItems, depositItems);
        if (!GdInterop.GetBool(previewResult, "allowed", false))
        {
            return _build_result(false, _build_failed_forge_message(recipe, item_defs, warehouse_service, previewResult, payload), quest_progress_events);
        }

        GDictionary commitResult = warehouse.commit_batch_swap_entries(withdrawalItems, depositItems);
        if (!GdInterop.GetBool(commitResult, "allowed", false))
        {
            return _build_result(false, _build_failed_forge_message(recipe, item_defs, warehouse_service, commitResult, payload), quest_progress_events);
        }

        GodotObject outputItemDef = GetItemDef(item_defs, recipe.output_item_id);
        string message = _build_success_message(recipe, item_defs, settlement, payload, outputItemDef);
        return _build_result(
            true,
            message,
            quest_progress_events,
            true,
            new GDictionary
            {
                ["recipe_id"] = recipe.recipe_id.ToString(),
                ["removed_entries"] = _build_recipe_entry_options(recipe.input_item_ids, recipe.input_item_quantities),
                ["added_entries"] = _build_recipe_entry_options(new Godot.Collections.Array<StringName> { recipe.output_item_id }, new[] { recipe.output_quantity }),
            },
            new GDictionary
            {
                ["recipe_id"] = recipe.recipe_id.ToString(),
                ["facility_tags"] = _build_facility_tags(settlement, payload),
                ["output_item_id"] = recipe.output_item_id.ToString(),
                ["output_quantity"] = recipe.output_quantity,
            });
    }

    public GDictionary execute_master_reforge(
        GDictionary settlement,
        GDictionary payload,
        GDictionary item_defs,
        GDictionary recipe_defs,
        GodotObject warehouse_service,
        GodotObject party_state,
        GArray quest_progress_events = null)
    {
        return execute_recipe(settlement, payload, item_defs, recipe_defs, warehouse_service, party_state, quest_progress_events);
    }

    public GDictionary build_window_data(
        string interaction_script_id,
        GDictionary settlement_record,
        GDictionary payload,
        GDictionary item_defs,
        GDictionary recipe_defs,
        GodotObject warehouse_service,
        string feedback_text = "")
    {
        settlement_record ??= new GDictionary();
        payload ??= new GDictionary();
        item_defs ??= new GDictionary();
        recipe_defs ??= new GDictionary();

        GDictionary serviceProfile = _resolve_service_profile(payload, interaction_script_id);
        GDictionary resolvedRecipeDefs = _resolve_recipe_defs(item_defs, recipe_defs);
        GArray recipeEntries = _build_recipe_window_entries(settlement_record, payload, item_defs, resolvedRecipeDefs, warehouse_service, interaction_script_id);
        string facilityName = GdInterop.GetString(payload, "facility_name", GdInterop.GetString(serviceProfile, "default_facility_name", "工坊"));
        string settlementName = GdInterop.GetString(settlement_record, "display_name", "据点");
        string summaryText = GdInterop.GetString(serviceProfile, "summary_text", "选择一个配方后即可消耗材料并将结果原子写入共享仓库。");
        if (recipeEntries.Count == 0)
        {
            summaryText = GdInterop.GetString(serviceProfile, "empty_summary_text", "当前没有可用的配方。");
        }

        return new GDictionary
        {
            ["title"] = $"{settlementName} · {GdInterop.GetString(serviceProfile, "title_suffix", "工坊")}",
            ["meta"] = $"工坊：{facilityName}  |  规则：消耗材料并原子写入共享仓库。",
            ["summary_text"] = summaryText,
            ["state_summary_text"] = GdInterop.GetString(payload, "state_summary_text"),
            ["feedback_text"] = !string.IsNullOrEmpty(feedback_text) ? feedback_text : GdInterop.GetString(serviceProfile, "default_feedback_text", "选择一条配方后即可执行配方操作。"),
            ["settlement_id"] = GdInterop.GetString(settlement_record, "settlement_id"),
            ["interaction_script_id"] = interaction_script_id,
            ["action_id"] = GdInterop.GetString(payload, "action_id", GdInterop.GetString(serviceProfile, "action_id", _build_default_action_id(interaction_script_id))),
            ["facility_id"] = GdInterop.GetString(payload, "facility_id"),
            ["facility_name"] = facilityName,
            ["npc_id"] = GdInterop.GetString(payload, "npc_id"),
            ["npc_name"] = GdInterop.GetString(payload, "npc_name"),
            ["service_type"] = GdInterop.GetString(payload, "service_type", GdInterop.GetString(serviceProfile, "service_type", "工坊")),
            ["panel_kind"] = "forge",
            ["confirm_label"] = GdInterop.GetString(serviceProfile, "confirm_label", "确认"),
            ["cancel_label"] = "返回",
            ["show_member_selector"] = false,
            ["default_member_id"] = GdInterop.GetString(payload, "member_id", GdInterop.GetString(payload, "default_member_id")),
            ["selected_member_id"] = GdInterop.GetString(payload, "member_id", GdInterop.GetString(payload, "selected_member_id")),
            ["entry_title"] = "可选配方",
            ["summary_title"] = "工坊概况",
            ["state_title"] = "配方状态",
            ["cost_title"] = "材料消耗",
            ["details_title"] = "配方说明",
            ["member_title"] = "工坊成员",
            ["empty_state_label"] = "状态：暂无配方",
            ["empty_cost_label"] = "材料：暂无配方",
            ["empty_details_text"] = "当前没有可用配方。",
            ["entries"] = recipeEntries,
        };
    }

    private RecipeDef _resolve_recipe(GDictionary settlement, GDictionary payload, GDictionary recipeDefs, GodotObject warehouseService)
    {
        if (recipeDefs == null || recipeDefs.Count == 0)
        {
            return null;
        }

        StringName requestedRecipeId = GdInterop.GetStringName(payload, "recipe_id");
        if (!GdInterop.IsEmpty(requestedRecipeId))
        {
            RecipeDef requestedRecipe = GetRecipeDef(recipeDefs, requestedRecipeId);
            return requestedRecipe != null && _recipe_matches_facility(requestedRecipe, settlement, payload) ? requestedRecipe : null;
        }

        var matchedRecipes = new System.Collections.Generic.List<RecipeDef>();
        foreach (var recipeValue in recipeDefs.Values)
        {
            RecipeDef recipe = recipeValue.AsGodotObject() as RecipeDef;
            if (recipe == null || !_recipe_matches_facility(recipe, settlement, payload))
            {
                continue;
            }
            matchedRecipes.Add(recipe);
        }

        if (matchedRecipes.Count == 0)
        {
            return null;
        }
        if (warehouseService == null)
        {
            return matchedRecipes[0];
        }

        foreach (RecipeDef recipe in matchedRecipes)
        {
            if (_can_fulfill_recipe_inputs(recipe, warehouseService))
            {
                return recipe;
            }
        }
        return matchedRecipes[0];
    }

    private GArray _list_matching_recipes(GDictionary settlement, GDictionary payload, GDictionary recipeDefs)
    {
        var matchedRecipes = new GArray();
        foreach (string recipeIdString in ProgressionDataUtils.sorted_string_keys(recipeDefs ?? new GDictionary()))
        {
            RecipeDef recipe = GetRecipeDef(recipeDefs, new StringName(recipeIdString));
            if (recipe == null || !_recipe_matches_facility(recipe, settlement, payload))
            {
                continue;
            }
            matchedRecipes.Add(recipe);
        }
        return matchedRecipes;
    }

    private GDictionary _resolve_recipe_defs(GDictionary itemDefs, GDictionary recipeDefs = null)
    {
        if (recipeDefs != null && recipeDefs.Count > 0)
        {
            return recipeDefs;
        }
        _recipeRegistry.setup(itemDefs ?? new GDictionary());
        if (_recipeRegistry.validate().Count > 0)
        {
            return new GDictionary();
        }
        return _recipeRegistry.get_recipe_defs();
    }

    private GArray _build_recipe_window_entries(
        GDictionary settlement,
        GDictionary payload,
        GDictionary itemDefs,
        GDictionary recipeDefs,
        GodotObject warehouseService,
        string interactionScriptId)
    {
        var entries = new GArray();
        foreach (var recipeValue in _list_matching_recipes(settlement, payload, recipeDefs))
        {
            RecipeDef recipe = recipeValue.AsGodotObject() as RecipeDef;
            if (recipe != null)
            {
                entries.Add(_build_recipe_window_entry(recipe, settlement, payload, itemDefs, warehouseService, interactionScriptId));
            }
        }
        return entries;
    }

    private GDictionary _build_recipe_window_entry(
        RecipeDef recipe,
        GDictionary settlement,
        GDictionary payload,
        GDictionary itemDefs,
        GodotObject warehouseService,
        string interactionScriptId)
    {
        string outputSummary = _build_item_label(recipe.output_item_id, itemDefs, recipe.output_quantity, GetItemDef(itemDefs, recipe.output_item_id));
        string materialSummary = _build_recipe_input_summary(recipe, itemDefs);
        string stateLabel = "状态：可重铸";
        string disabledReason = "";
        bool isEnabled = true;
        GDictionary serviceProfile = _resolve_service_profile(payload, interactionScriptId);

        if (warehouseService == null)
        {
            isEnabled = false;
            stateLabel = "状态：不可用";
            disabledReason = "共享仓库服务尚未准备完成。";
        }
        else if (!_can_fulfill_recipe_inputs(recipe, warehouseService))
        {
            isEnabled = false;
            stateLabel = "状态：材料不足";
            disabledReason = $"缺少材料：{string.Join("、", ToStringList(_build_missing_input_entries(recipe, itemDefs, warehouseService)))}。";
        }
        else
        {
            var warehouse = (PartyWarehouseService)warehouseService;
            GDictionary previewResult = warehouse.preview_batch_swap_entries(_expand_input_items(recipe), _build_repeated_item_array(recipe.output_item_id, recipe.output_quantity));
            if (!GdInterop.GetBool(previewResult, "allowed", false))
            {
                isEnabled = false;
                stateLabel = "状态：无法写入";
                disabledReason = _build_failed_forge_message(recipe, itemDefs, warehouseService, previewResult, payload);
            }
        }

        string detailsText = recipe.description;
        if (string.IsNullOrEmpty(detailsText))
        {
            detailsText = $"消耗 {materialSummary}，可{GdInterop.GetString(serviceProfile, "recipe_action_phrase", "制作为")} {outputSummary}。";
        }
        else
        {
            detailsText += $"\n消耗：{materialSummary}\n产出：{outputSummary}";
        }
        GArray facilityTags = _build_facility_tags(settlement, payload);
        if (facilityTags.Count > 0)
        {
            detailsText += $"\n设施标签：{string.Join(" / ", ToStringList(facilityTags))}";
        }

        return new GDictionary
        {
            ["entry_id"] = $"recipe:{recipe.recipe_id}",
            ["recipe_id"] = recipe.recipe_id.ToString(),
            ["display_name"] = !string.IsNullOrEmpty(recipe.display_name) ? recipe.display_name : recipe.recipe_id.ToString(),
            ["summary_text"] = $"{materialSummary} -> {outputSummary}",
            ["details_text"] = detailsText,
            ["state_label"] = stateLabel,
            ["cost_label"] = $"材料：{materialSummary}",
            ["is_enabled"] = isEnabled,
            ["disabled_reason"] = disabledReason,
            ["interaction_script_id"] = interactionScriptId,
        };
    }

    private bool _recipe_matches_facility(RecipeDef recipe, GDictionary settlement, GDictionary payload)
    {
        if (recipe.required_facility_tags.Count == 0)
        {
            return true;
        }
        GDictionary availableTags = _build_facility_tag_set(settlement, payload);
        foreach (var rawTag in recipe.required_facility_tags)
        {
            StringName normalizedTag = ProgressionDataUtils.to_string_name(rawTag);
            if (GdInterop.IsEmpty(normalizedTag))
            {
                continue;
            }
            if (!availableTags.ContainsKey(normalizedTag))
            {
                return false;
            }
        }
        return true;
    }

    private GDictionary _build_facility_tag_set(GDictionary settlement, GDictionary payload)
    {
        var tags = new GDictionary();
        GDictionary facility = _resolve_facility(settlement, payload);
        foreach (var rawTag in _build_facility_tags(settlement, payload))
        {
            StringName normalizedTag = ProgressionDataUtils.to_string_name(rawTag);
            if (!GdInterop.IsEmpty(normalizedTag))
            {
                tags[normalizedTag] = true;
            }
        }
        if (facility.Count > 0)
        {
            StringName facilityTemplateId = ProgressionDataUtils.to_string_name(_resolve_facility_template_id(facility));
            if (!GdInterop.IsEmpty(facilityTemplateId))
            {
                tags[facilityTemplateId] = true;
            }
        }
        return tags;
    }

    private GArray _build_facility_tags(GDictionary settlement, GDictionary payload)
    {
        var tags = new GArray();
        GDictionary facility = _resolve_facility(settlement, payload);
        string interactionScriptId = GdInterop.GetString(payload, "interaction_script_id");

        void PushTag(Variant rawValue)
        {
            string rawText = rawValue.VariantType == Variant.Type.Nil ? "" : rawValue.ToString();
            if (string.IsNullOrEmpty(rawText) || tags.Contains(rawText))
            {
                return;
            }
            tags.Add(rawText);
        }

        PushTag(interactionScriptId);
        PushTag(GdInterop.GetString(payload, "service_type"));

        if (facility.Count > 0)
        {
            PushTag(_resolve_facility_template_id(facility));
            PushTag(GdInterop.GetString(facility, "category"));
            PushTag(GdInterop.GetString(facility, "interaction_type"));
            PushTag(GdInterop.GetString(facility, "slot_tag"));
            string category = GdInterop.GetString(facility, "category");
            string interactionType = GdInterop.GetString(facility, "interaction_type");
            if (interactionType == "craft" || category == "craft" || category == "support")
            {
                PushTag("forge");
                PushTag("craft");
            }
        }

        if (is_supported_interaction(interactionScriptId))
        {
            PushTag("forge");
            PushTag("craft");
        }
        if (_is_master_reforge_interaction(interactionScriptId))
        {
            PushTag("master_reforge");
        }
        return tags;
    }

    private static GDictionary _resolve_facility(GDictionary settlement, GDictionary payload)
    {
        string targetFacilityId = GdInterop.GetString(payload, "facility_id");
        string targetFacilityTemplateId = GdInterop.GetString(payload, "facility_template_id").StripEdges();
        foreach (var facilityValue in GdInterop.GetArray(settlement, "facilities"))
        {
            if (facilityValue.VariantType != Variant.Type.Dictionary)
            {
                continue;
            }
            GDictionary facility = facilityValue.AsGodotDictionary();
            if (!string.IsNullOrEmpty(targetFacilityId) && GdInterop.GetString(facility, "facility_id") == targetFacilityId)
            {
                return facility;
            }
            if (!string.IsNullOrEmpty(targetFacilityTemplateId) && _resolve_facility_template_id(facility) == targetFacilityTemplateId)
            {
                return facility;
            }
        }
        return new GDictionary();
    }

    private static string _resolve_facility_template_id(GDictionary facility)
    {
        if (facility == null || facility.Count == 0)
        {
            return "";
        }
        return GdInterop.GetString(facility, "template_id", GdInterop.GetString(facility, "facility_id")).StripEdges();
    }

    private static bool _can_fulfill_recipe_inputs(RecipeDef recipe, GodotObject warehouseService)
    {
        var warehouse = (PartyWarehouseService)warehouseService;
        for (int inputIndex = 0; inputIndex < recipe.input_item_ids.Count; inputIndex++)
        {
            StringName itemId = ProgressionDataUtils.to_string_name(recipe.input_item_ids[inputIndex]);
            int requiredQuantity = inputIndex < recipe.input_item_quantities.Length ? recipe.input_item_quantities[inputIndex] : 0;
            if (warehouse.count_item(itemId) < requiredQuantity)
            {
                return false;
            }
        }
        return true;
    }

    private static GDictionary _validate_recipe_items(RecipeDef recipe, GDictionary itemDefs)
    {
        foreach (var inputItemId in recipe.input_item_ids)
        {
            StringName normalizedInput = ProgressionDataUtils.to_string_name(inputItemId);
            if (GdInterop.IsEmpty(normalizedInput) || !itemDefs.ContainsKey(normalizedInput))
            {
                return new GDictionary
                {
                    ["ok"] = false,
                    ["message"] = $"配方 {recipe.recipe_id} 引用了缺失的输入物品 {normalizedInput}。",
                };
            }
        }
        if (GdInterop.IsEmpty(recipe.output_item_id) || !itemDefs.ContainsKey(recipe.output_item_id))
        {
            return new GDictionary
            {
                ["ok"] = false,
                ["message"] = $"配方 {recipe.recipe_id} 引用了缺失的产出物品 {recipe.output_item_id}。",
            };
        }
        return new GDictionary { ["ok"] = true };
    }

    private string _build_failed_forge_message(RecipeDef recipe, GDictionary itemDefs, GodotObject warehouseService, GDictionary warehouseResult, GDictionary payload = null)
    {
        payload ??= new GDictionary();
        GDictionary serviceProfile = _resolve_service_profile(payload);
        string errorCode = GdInterop.GetString(warehouseResult, "error_code");
        if (errorCode == "warehouse_blocked_swap")
        {
            return GdInterop.GetString(serviceProfile, "blocked_output_message", "共享仓库空间不足，无法放入配方成品。");
        }
        if (errorCode == "warehouse_missing_item")
        {
            GArray missingItems = _build_missing_input_entries(recipe, itemDefs, warehouseService);
            if (missingItems.Count > 0)
            {
                return $"{GdInterop.GetString(serviceProfile, "missing_material_prefix", "缺少配方材料：")}{string.Join("、", ToStringList(missingItems))}。";
            }
        }
        return !string.IsNullOrEmpty(recipe.failure_reason)
            ? recipe.failure_reason
            : GdInterop.GetString(serviceProfile, "fallback_failure_message", "当前无法完成该配方。");
    }

    private GArray _build_missing_input_entries(RecipeDef recipe, GDictionary itemDefs, GodotObject warehouseService)
    {
        var missingEntries = new GArray();
        var warehouse = (PartyWarehouseService)warehouseService;
        for (int inputIndex = 0; inputIndex < recipe.input_item_ids.Count; inputIndex++)
        {
            StringName itemId = ProgressionDataUtils.to_string_name(recipe.input_item_ids[inputIndex]);
            int requiredQuantity = inputIndex < recipe.input_item_quantities.Length ? recipe.input_item_quantities[inputIndex] : 0;
            int ownedQuantity = warehouse.count_item(itemId);
            if (ownedQuantity >= requiredQuantity)
            {
                continue;
            }
            int shortage = requiredQuantity - ownedQuantity;
            missingEntries.Add(_build_item_label(itemId, itemDefs, shortage, GetItemDef(itemDefs, itemId)));
        }
        return missingEntries;
    }

    private static GArray _expand_input_items(RecipeDef recipe)
    {
        var itemIds = new GArray();
        for (int inputIndex = 0; inputIndex < recipe.input_item_ids.Count; inputIndex++)
        {
            StringName itemId = ProgressionDataUtils.to_string_name(recipe.input_item_ids[inputIndex]);
            int quantity = inputIndex < recipe.input_item_quantities.Length ? recipe.input_item_quantities[inputIndex] : 0;
            foreach (var repeated in _build_repeated_item_array(itemId, quantity))
            {
                itemIds.Add(repeated);
            }
        }
        return itemIds;
    }

    private static GArray _build_repeated_item_array(StringName itemId, int quantity)
    {
        var itemIds = new GArray();
        int resolvedQuantity = Mathf.Max(quantity, 0);
        for (int index = 0; index < resolvedQuantity; index++)
        {
            itemIds.Add(itemId);
        }
        return itemIds;
    }

    private static string _build_recipe_input_summary(RecipeDef recipe, GDictionary itemDefs)
    {
        var parts = new System.Collections.Generic.List<string>();
        for (int inputIndex = 0; inputIndex < recipe.input_item_ids.Count; inputIndex++)
        {
            StringName itemId = ProgressionDataUtils.to_string_name(recipe.input_item_ids[inputIndex]);
            int quantity = inputIndex < recipe.input_item_quantities.Length ? recipe.input_item_quantities[inputIndex] : 0;
            parts.Add(_build_item_label(itemId, itemDefs, quantity, GetItemDef(itemDefs, itemId)));
        }
        return string.Join("、", parts);
    }

    private static GArray _build_recipe_entry_options(Godot.Collections.Array<StringName> itemIds, int[] quantities)
    {
        var entries = new GArray();
        for (int entryIndex = 0; entryIndex < itemIds.Count; entryIndex++)
        {
            StringName itemId = ProgressionDataUtils.to_string_name(itemIds[entryIndex]);
            int quantity = entryIndex < quantities.Length ? quantities[entryIndex] : 0;
            if (GdInterop.IsEmpty(itemId) || quantity <= 0)
            {
                continue;
            }
            entries.Add(new GDictionary
            {
                ["item_id"] = itemId.ToString(),
                ["quantity"] = quantity,
            });
        }
        return entries;
    }

    private static string _build_item_label(StringName itemId, GDictionary itemDefs, int quantity, GodotObject itemDef = null)
    {
        string displayName = itemId.ToString();
        itemDef ??= GetItemDef(itemDefs, itemId);
        string itemDisplayName = itemDef != null ? GdInterop.GetString(itemDef, "display_name") : "";
        if (!string.IsNullOrEmpty(itemDisplayName))
        {
            displayName = itemDisplayName;
        }
        return $"{Mathf.Max(quantity, 0)} 件 {displayName}";
    }

    private string _build_success_message(RecipeDef recipe, GDictionary itemDefs, GDictionary settlement, GDictionary payload, GodotObject outputItemDef = null)
    {
        GDictionary serviceProfile = _resolve_service_profile(payload);
        string inputSummary = _build_recipe_input_summary(recipe, itemDefs);
        string outputSummary = _build_item_label(recipe.output_item_id, itemDefs, recipe.output_quantity, outputItemDef);
        if (_is_master_reforge_interaction(GdInterop.GetString(payload, "interaction_script_id")))
        {
            return $"大师工坊已将 {inputSummary} 重铸为 {outputSummary}。";
        }
        string actorLabel = GdInterop.GetString(payload, "npc_name");
        if (string.IsNullOrEmpty(actorLabel))
        {
            actorLabel = GdInterop.GetString(payload, "facility_name");
        }
        if (string.IsNullOrEmpty(actorLabel))
        {
            actorLabel = GdInterop.GetString(settlement, "display_name", GdInterop.GetString(serviceProfile, "default_facility_name", "工坊"));
        }
        return $"{actorLabel} 已将 {inputSummary} {GdInterop.GetString(serviceProfile, "recipe_action_phrase", "制作为")} {outputSummary}。";
    }

    private GDictionary _resolve_service_profile(GDictionary payload, string interactionScriptId = "")
    {
        string resolvedInteractionId = (interactionScriptId ?? "").StripEdges();
        if (string.IsNullOrEmpty(resolvedInteractionId))
        {
            resolvedInteractionId = GdInterop.GetString(payload, "interaction_script_id").StripEdges();
        }
        if (_is_master_reforge_interaction(resolvedInteractionId))
        {
            return new GDictionary
            {
                ["title_suffix"] = "大师重铸",
                ["summary_text"] = "选择一个配方后即可消耗材料并将结果原子写入共享仓库。",
                ["empty_summary_text"] = "当前没有可用的重铸配方。",
                ["default_feedback_text"] = "选择一条配方后即可执行重铸。",
                ["confirm_label"] = "重铸",
                ["service_type"] = "重铸",
                ["recipe_action_phrase"] = "重铸为",
                ["no_recipe_message"] = "当前大师工坊没有可执行的重铸配方。",
                ["fallback_failure_message"] = "当前无法完成该重铸。",
                ["blocked_output_message"] = "共享仓库空间不足，无法放入重铸成品。",
                ["missing_material_prefix"] = "缺少重铸材料：",
                ["default_facility_name"] = "大师工坊",
                ["action_id"] = "service:master_reforge",
            };
        }

        string genericTitleSuffix = GdInterop.GetString(payload, "service_type", "锻造").StripEdges();
        if (string.IsNullOrEmpty(genericTitleSuffix))
        {
            genericTitleSuffix = "锻造";
        }
        return new GDictionary
        {
            ["title_suffix"] = genericTitleSuffix,
            ["summary_text"] = "选择一个配方后即可消耗材料并将结果原子写入共享仓库。",
            ["empty_summary_text"] = "当前没有可用的锻造配方。",
            ["default_feedback_text"] = $"选择一条配方后即可执行{genericTitleSuffix}。",
            ["confirm_label"] = genericTitleSuffix,
            ["service_type"] = genericTitleSuffix,
            ["recipe_action_phrase"] = "打造为",
            ["no_recipe_message"] = "当前工坊没有可执行的锻造配方。",
            ["fallback_failure_message"] = "当前无法完成该锻造。",
            ["blocked_output_message"] = "共享仓库空间不足，无法放入锻造成品。",
            ["missing_material_prefix"] = "缺少配方材料：",
            ["default_facility_name"] = "工坊",
            ["action_id"] = _build_default_action_id(resolvedInteractionId),
        };
    }

    private static string _build_default_action_id(string interactionScriptId)
    {
        string normalizedInteractionId = (interactionScriptId ?? "").StripEdges();
        if (normalizedInteractionId.StartsWith("service_"))
        {
            return $"service:{normalizedInteractionId["service_".Length..]}";
        }
        return "service:master_reforge";
    }

    private static bool _is_master_reforge_interaction(string interactionScriptId)
    {
        return (interactionScriptId ?? "").StripEdges() == MasterReforgeInteractionId;
    }

    private static GDictionary _build_result(
        bool success,
        string message,
        GArray questProgressEvents,
        bool persistPartyState = false,
        GDictionary inventoryDelta = null,
        GDictionary serviceSideEffects = null)
    {
        var result = new SettlementServiceResult
        {
            Success = success,
            Message = message,
            PersistPartyState = persistPartyState,
        };
        result.SetInventoryDelta(inventoryDelta);
        result.SetQuestProgressEventPayloads(DuplicateDictionaryArrayUntyped(questProgressEvents ?? new GArray()));
        result.SetServiceSideEffects(serviceSideEffects);
        return result.ToDictionary();
    }

    private static GArray DuplicateDictionaryArrayUntyped(GArray value)
    {
        var result = new GArray();
        foreach (var entryValue in value)
        {
            if (entryValue.VariantType == Variant.Type.Dictionary)
            {
                result.Add(entryValue.AsGodotDictionary().Duplicate(true));
            }
        }
        return result;
    }

    private static RecipeDef GetRecipeDef(GDictionary recipeDefs, StringName key)
    {
        if (recipeDefs == null || key == null)
        {
            return null;
        }
        Variant value;
        if (recipeDefs.ContainsKey(key))
        {
            value = recipeDefs[key];
        }
        else
        {
            string stringKey = key.ToString();
            if (!recipeDefs.ContainsKey(stringKey))
                return null;
            value = recipeDefs[stringKey];
        }
        if (value.VariantType != Variant.Type.Object)
        {
            return null;
        }
        return value.AsGodotObject() as RecipeDef;
    }

    private static GodotObject GetItemDef(GDictionary itemDefs, StringName key)
    {
        if (itemDefs == null || key == null)
        {
            return null;
        }
        Variant value;
        if (itemDefs.ContainsKey(key))
        {
            value = itemDefs[key];
        }
        else
        {
            string stringKey = key.ToString();
            if (!itemDefs.ContainsKey(stringKey))
                return null;
            value = itemDefs[stringKey];
        }
        if (value.VariantType != Variant.Type.Object)
        {
            return null;
        }
        return value.AsGodotObject();
    }

    private static System.Collections.Generic.List<string> ToStringList(GArray values)
    {
        var result = new System.Collections.Generic.List<string>();
        foreach (var value in values)
        {
            result.Add(value.ToString());
        }
        return result;
    }
}
