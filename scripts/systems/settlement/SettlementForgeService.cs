using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

public sealed class SettlementForgeService : System.IDisposable
{
    private const string MasterReforgeInteractionId = "service_master_reforge";
    private static readonly HashSet<string> GenericForgeInteractionIds = new(
        System.StringComparer.Ordinal
    )
    {
        "service_repair_gear",
    };

    private readonly RecipeContentRegistry _recipeRegistry = new();

    public void Dispose()
    {
        System.GC.SuppressFinalize(this);
        _recipeRegistry.Dispose();
    }

    private sealed class RecipeItemValidationResult
    {
        public readonly bool Ok;
        public readonly string Message;

        private RecipeItemValidationResult(bool ok, string message)
        {
            Ok = ok;
            Message = message ?? "";
        }

        public static RecipeItemValidationResult Success() => new(true, "");

        public static RecipeItemValidationResult Failed(string message) => new(false, message);
    }

    public bool IsSupportedInteraction(string interaction_script_id)
    {
        string normalizedInteractionId = (interaction_script_id ?? "").StripEdges();
        return normalizedInteractionId == MasterReforgeInteractionId
            || GenericForgeInteractionIds.Contains(normalizedInteractionId);
    }

    public bool HasAvailableRecipeTyped(
        GDictionary settlement,
        GDictionary payload,
        IReadOnlyDictionary<StringName, ItemDef> item_defs,
        IReadOnlyDictionary<StringName, RecipeDef> recipe_defs = null
    )
    {
        IReadOnlyDictionary<StringName, RecipeDef> resolvedRecipeDefs = _resolve_recipe_defs(
            item_defs,
            recipe_defs
        );
        if (resolvedRecipeDefs.Count == 0)
        {
            return false;
        }
        return
            _resolve_recipe(
                settlement ?? new GDictionary(),
                payload ?? new GDictionary(),
                resolvedRecipeDefs,
                null
            ) != null;
    }

    internal SettlementServiceResult ExecuteRecipeResultTyped(
        GDictionary settlement,
        GDictionary payload,
        IReadOnlyDictionary<StringName, ItemDef> item_defs,
        IReadOnlyDictionary<StringName, RecipeDef> recipe_defs,
        PartyWarehouseService warehouse,
        PartyState partyState,
        IEnumerable<QuestProgressService.QuestProgressEventData> quest_progress_events = null)
    {
        settlement ??= new GDictionary();
        payload ??= new GDictionary();
        recipe_defs ??= new Dictionary<StringName, RecipeDef>();

        GDictionary serviceProfile = _resolve_service_profile(payload);
        if (warehouse == null || partyState == null)
        {
            return _build_result(false, "当前工坊服务尚未准备完成。", quest_progress_events);
        }

        IReadOnlyDictionary<StringName, RecipeDef> resolvedRecipeDefs = _resolve_recipe_defs(
            item_defs,
            recipe_defs
        );
        if (resolvedRecipeDefs.Count == 0)
        {
            return _build_result(false, "当前配方配置缺失，暂时无法执行。", quest_progress_events);
        }

        RecipeDef recipe = _resolve_recipe(settlement, payload, resolvedRecipeDefs, warehouse);
        if (recipe == null)
        {
            return _build_result(false, ReadString(serviceProfile, "no_recipe_message", "当前工坊没有可执行的配方。"), quest_progress_events);
        }

        RecipeItemValidationResult inputValidation = _validate_recipe_items(recipe, item_defs);
        if (!inputValidation.Ok)
        {
            return _build_result(false, !string.IsNullOrEmpty(inputValidation.Message) ? inputValidation.Message : "当前配方引用了无效物品。", quest_progress_events);
        }

        GArray withdrawalItems = _expand_input_items(recipe);
        GArray depositItems = _build_repeated_item_array(recipe.output_item_id, recipe.output_quantity);
        var previewResult = warehouse.PreviewBatchSwapEntriesTyped(withdrawalItems, depositItems);
        if (!previewResult.Allowed)
        {
            return _build_result(false, _build_failed_forge_message(recipe, item_defs, warehouse, previewResult.ErrorCode, payload), quest_progress_events);
        }

        var commitResult = warehouse.CommitBatchSwapEntriesTyped(withdrawalItems, depositItems);
        if (!commitResult.Allowed)
        {
            return _build_result(false, _build_failed_forge_message(recipe, item_defs, warehouse, commitResult.ErrorCode, payload), quest_progress_events);
        }

        ItemDef outputItemDef = GetItemDef(item_defs, recipe.output_item_id);
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

    public GDictionary BuildWindowDataTyped(
        string interaction_script_id,
        GDictionary settlement_record,
        GDictionary payload,
        IReadOnlyDictionary<StringName, ItemDef> item_defs,
        IReadOnlyDictionary<StringName, RecipeDef> recipe_defs,
        PartyWarehouseService warehouse,
        string feedback_text = "")
    {
        settlement_record ??= new GDictionary();
        payload ??= new GDictionary();
        recipe_defs ??= new Dictionary<StringName, RecipeDef>();

        GDictionary serviceProfile = _resolve_service_profile(payload, interaction_script_id);
        IReadOnlyDictionary<StringName, RecipeDef> resolvedRecipeDefs = _resolve_recipe_defs(
            item_defs,
            recipe_defs
        );
        GArray recipeEntries = _build_recipe_window_entries(
            settlement_record,
            payload,
            item_defs,
            resolvedRecipeDefs,
            warehouse,
            interaction_script_id
        );
        string facilityName = ReadString(
            payload,
            "facility_name",
            ReadString(serviceProfile, "default_facility_name", "工坊")
        );
        string settlementName = ReadString(settlement_record, "display_name", "据点");
        string summaryText = ReadString(
            serviceProfile,
            "summary_text",
            "选择一个配方后即可消耗材料并将结果原子写入共享仓库。"
        );
        if (recipeEntries.Count == 0)
        {
            summaryText = ReadString(serviceProfile, "empty_summary_text", "当前没有可用的配方。");
        }

        return new GDictionary
        {
            ["title"] = $"{settlementName} · {ReadString(serviceProfile, "title_suffix", "工坊")}",
            ["meta"] = $"工坊：{facilityName}  |  规则：消耗材料并原子写入共享仓库。",
            ["summary_text"] = summaryText,
            ["state_summary_text"] = ReadString(payload, "state_summary_text"),
            ["feedback_text"] = !string.IsNullOrEmpty(feedback_text) ? feedback_text : ReadString(serviceProfile, "default_feedback_text", "选择一条配方后即可执行配方操作。"),
            ["settlement_id"] = ReadString(settlement_record, "settlement_id"),
            ["interaction_script_id"] = interaction_script_id,
            ["action_id"] = ReadString(payload, "action_id", ReadString(serviceProfile, "action_id", _build_default_action_id(interaction_script_id))),
            ["facility_id"] = ReadString(payload, "facility_id"),
            ["facility_name"] = facilityName,
            ["npc_id"] = ReadString(payload, "npc_id"),
            ["npc_name"] = ReadString(payload, "npc_name"),
            ["service_type"] = ReadString(payload, "service_type", ReadString(serviceProfile, "service_type", "工坊")),
            ["panel_kind"] = SettlementPanelKinds.ToPayloadValue(SettlementPanelKind.Forge),
            ["confirm_label"] = ReadString(serviceProfile, "confirm_label", "确认"),
            ["cancel_label"] = "返回",
            ["show_member_selector"] = false,
            ["default_member_id"] = ReadString(payload, "member_id", ReadString(payload, "default_member_id")),
            ["selected_member_id"] = ReadString(payload, "member_id", ReadString(payload, "selected_member_id")),
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

    private RecipeDef _resolve_recipe(
        GDictionary settlement,
        GDictionary payload,
        IReadOnlyDictionary<StringName, RecipeDef> recipeDefs,
        PartyWarehouseService warehouse)
    {
        if (recipeDefs == null || recipeDefs.Count == 0)
        {
            return null;
        }

        StringName requestedRecipeId = ReadStringName(payload, "recipe_id");
        if (requestedRecipeId != "")
        {
            RecipeDef requestedRecipe = GetRecipeDef(recipeDefs, requestedRecipeId);
            return requestedRecipe != null && _recipe_matches_facility(requestedRecipe, settlement, payload) ? requestedRecipe : null;
        }

        var matchedRecipes = new System.Collections.Generic.List<RecipeDef>();
        foreach (RecipeDef recipe in recipeDefs.Values)
        {
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
        if (warehouse == null)
        {
            return matchedRecipes[0];
        }

        foreach (RecipeDef recipe in matchedRecipes)
        {
            if (_can_fulfill_recipe_inputs(recipe, warehouse))
            {
                return recipe;
            }
        }
        return matchedRecipes[0];
    }

    private List<RecipeDef> _list_matching_recipes(
        GDictionary settlement,
        GDictionary payload,
        IReadOnlyDictionary<StringName, RecipeDef> recipeDefs
    )
    {
        var matchedRecipes = new List<RecipeDef>();
        if (recipeDefs == null)
        {
            return matchedRecipes;
        }
        foreach (StringName recipeId in SortedRecipeIds(recipeDefs))
        {
            RecipeDef recipe = GetRecipeDef(recipeDefs, recipeId);
            if (recipe == null || !_recipe_matches_facility(recipe, settlement, payload))
            {
                continue;
            }
            matchedRecipes.Add(recipe);
        }
        return matchedRecipes;
    }

    private IReadOnlyDictionary<StringName, RecipeDef> _resolve_recipe_defs(
        IReadOnlyDictionary<StringName, ItemDef> itemDefs,
        IReadOnlyDictionary<StringName, RecipeDef> recipeDefs = null
    )
    {
        if (recipeDefs != null && recipeDefs.Count > 0)
        {
            return recipeDefs;
        }
        _recipeRegistry.Setup(itemDefs);
        if (_recipeRegistry.ValidateTyped().Count > 0)
        {
            return new Dictionary<StringName, RecipeDef>();
        }
        return _recipeRegistry.GetRecipeDefsTyped();
    }

    private GArray _build_recipe_window_entries(
        GDictionary settlement,
        GDictionary payload,
        IReadOnlyDictionary<StringName, ItemDef> itemDefs,
        IReadOnlyDictionary<StringName, RecipeDef> recipeDefs,
        PartyWarehouseService warehouse,
        string interactionScriptId)
    {
        var entries = new GArray();
        foreach (RecipeDef recipe in _list_matching_recipes(settlement, payload, recipeDefs))
        {
            if (recipe != null)
            {
                entries.Add(_build_recipe_window_entry(recipe, settlement, payload, itemDefs, warehouse, interactionScriptId));
            }
        }
        return entries;
    }

    private GDictionary _build_recipe_window_entry(
        RecipeDef recipe,
        GDictionary settlement,
        GDictionary payload,
        IReadOnlyDictionary<StringName, ItemDef> itemDefs,
        PartyWarehouseService warehouse,
        string interactionScriptId)
    {
        string outputSummary = _build_item_label(recipe.output_item_id, itemDefs, recipe.output_quantity, GetItemDef(itemDefs, recipe.output_item_id));
        string materialSummary = _build_recipe_input_summary(recipe, itemDefs);
        string stateLabel = "状态：可重铸";
        string disabledReason = "";
        bool isEnabled = true;
        GDictionary serviceProfile = _resolve_service_profile(payload, interactionScriptId);

        if (warehouse == null)
        {
            isEnabled = false;
            stateLabel = "状态：不可用";
            disabledReason = "共享仓库服务尚未准备完成。";
        }
        else if (!_can_fulfill_recipe_inputs(recipe, warehouse))
        {
            isEnabled = false;
            stateLabel = "状态：材料不足";
            disabledReason = $"缺少材料：{string.Join("、", ToStringList(_build_missing_input_entries(recipe, itemDefs, warehouse)))}。";
        }
        else
        {
            var previewResult = warehouse.PreviewBatchSwapEntriesTyped(_expand_input_items(recipe), _build_repeated_item_array(recipe.output_item_id, recipe.output_quantity));
            if (!previewResult.Allowed)
            {
                isEnabled = false;
                stateLabel = "状态：无法写入";
                disabledReason = _build_failed_forge_message(recipe, itemDefs, warehouse, previewResult.ErrorCode, payload);
            }
        }

        string detailsText = recipe.description;
        if (string.IsNullOrEmpty(detailsText))
        {
            detailsText = $"消耗 {materialSummary}，可{ReadString(serviceProfile, "recipe_action_phrase", "制作为")} {outputSummary}。";
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
            if (normalizedTag == "")
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
            if (normalizedTag != "")
            {
                tags[normalizedTag] = true;
            }
        }
        if (facility.Count > 0)
        {
            StringName facilityTemplateId = ProgressionDataUtils.to_string_name(_resolve_facility_template_id(facility));
            if (facilityTemplateId != "")
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
        string interactionScriptId = ReadString(payload, "interaction_script_id");

        void PushTag(Variant rawValue)
        {
            string rawText = rawValue.VariantType switch
            {
                Variant.Type.Nil => "",
                Variant.Type.String => rawValue.AsString(),
                Variant.Type.StringName => rawValue.AsStringName().ToString(),
                _ => rawValue.ToString(),
            };
            if (string.IsNullOrEmpty(rawText) || tags.Contains(rawText))
            {
                return;
            }
            tags.Add(rawText);
        }

        PushTag(interactionScriptId);
        PushTag(ReadString(payload, "service_type"));

        if (facility.Count > 0)
        {
            PushTag(_resolve_facility_template_id(facility));
            PushTag(ReadString(facility, "category"));
            PushTag(ReadString(facility, "interaction_type"));
            PushTag(ReadString(facility, "slot_tag"));
            string category = ReadString(facility, "category");
            string interactionType = ReadString(facility, "interaction_type");
            if (interactionType == "craft" || category == "craft" || category == "support")
            {
                PushTag("forge");
                PushTag("craft");
            }
        }

        if (IsSupportedInteraction(interactionScriptId))
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
        string targetFacilityId = ReadString(payload, "facility_id");
        string targetFacilityTemplateId = ReadString(payload, "facility_template_id").StripEdges();
        foreach (var facilityValue in ReadArray(settlement, "facilities"))
        {
            if (facilityValue.VariantType != Variant.Type.Dictionary)
            {
                continue;
            }
            GDictionary facility = facilityValue.AsGodotDictionary();
            if (!string.IsNullOrEmpty(targetFacilityId) && ReadString(facility, "facility_id") == targetFacilityId)
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
        return ReadString(facility, "template_id", ReadString(facility, "facility_id")).StripEdges();
    }

    private static bool _can_fulfill_recipe_inputs(RecipeDef recipe, PartyWarehouseService warehouse)
    {
        for (int inputIndex = 0; inputIndex < recipe.input_item_ids.Count; inputIndex++)
        {
            StringName itemId = ProgressionDataUtils.to_string_name(recipe.input_item_ids[inputIndex]);
            int requiredQuantity = inputIndex < recipe.input_item_quantities.Length ? recipe.input_item_quantities[inputIndex] : 0;
            if (warehouse.CountItem(itemId) < requiredQuantity)
            {
                return false;
            }
        }
        return true;
    }

    private static RecipeItemValidationResult _validate_recipe_items(
        RecipeDef recipe,
        IReadOnlyDictionary<StringName, ItemDef> itemDefs
    )
    {
        foreach (var inputItemId in recipe.input_item_ids)
        {
            StringName normalizedInput = ProgressionDataUtils.to_string_name(inputItemId);
            if (normalizedInput == "" || itemDefs == null || !itemDefs.ContainsKey(normalizedInput))
            {
                return RecipeItemValidationResult.Failed(
                    $"配方 {recipe.recipe_id} 引用了缺失的输入物品 {normalizedInput}。"
                );
            }
        }
        if (recipe.output_item_id == "" || itemDefs == null || !itemDefs.ContainsKey(recipe.output_item_id))
        {
            return RecipeItemValidationResult.Failed(
                $"配方 {recipe.recipe_id} 引用了缺失的产出物品 {recipe.output_item_id}。"
            );
        }
        return RecipeItemValidationResult.Success();
    }

    private string _build_failed_forge_message(
        RecipeDef recipe,
        IReadOnlyDictionary<StringName, ItemDef> itemDefs,
        PartyWarehouseService warehouse,
        string warehouseErrorCode,
        GDictionary payload = null)
    {
        payload ??= new GDictionary();
        GDictionary serviceProfile = _resolve_service_profile(payload);
        string errorCode = warehouseErrorCode ?? "";
        if (errorCode == "warehouse_blocked_swap")
        {
            return ReadString(serviceProfile, "blocked_output_message", "共享仓库空间不足，无法放入配方成品。");
        }
        if (errorCode == "warehouse_missing_item")
        {
            GArray missingItems = _build_missing_input_entries(recipe, itemDefs, warehouse);
            if (missingItems.Count > 0)
            {
                return $"{ReadString(serviceProfile, "missing_material_prefix", "缺少配方材料：")}{string.Join("、", ToStringList(missingItems))}。";
            }
        }
        return !string.IsNullOrEmpty(recipe.failure_reason)
            ? recipe.failure_reason
            : ReadString(serviceProfile, "fallback_failure_message", "当前无法完成该配方。");
    }

    private GArray _build_missing_input_entries(
        RecipeDef recipe,
        IReadOnlyDictionary<StringName, ItemDef> itemDefs,
        PartyWarehouseService warehouse)
    {
        var missingEntries = new GArray();
        for (int inputIndex = 0; inputIndex < recipe.input_item_ids.Count; inputIndex++)
        {
            StringName itemId = ProgressionDataUtils.to_string_name(recipe.input_item_ids[inputIndex]);
            int requiredQuantity = inputIndex < recipe.input_item_quantities.Length ? recipe.input_item_quantities[inputIndex] : 0;
            int ownedQuantity = warehouse.CountItem(itemId);
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
            itemIds.Add(new GDictionary { ["item_id"] = itemId });
        }
        return itemIds;
    }

    private static string _build_recipe_input_summary(
        RecipeDef recipe,
        IReadOnlyDictionary<StringName, ItemDef> itemDefs
    )
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
            if (itemId == "" || quantity <= 0)
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

    private static string _build_item_label(
        StringName itemId,
        IReadOnlyDictionary<StringName, ItemDef> itemDefs,
        int quantity,
        ItemDef itemDef = null)
    {
        string displayName = itemId.ToString();
        itemDef ??= GetItemDef(itemDefs, itemId);
        if (itemDef != null && !string.IsNullOrEmpty(itemDef.display_name))
        {
            displayName = itemDef.display_name;
        }
        return $"{Mathf.Max(quantity, 0)} 件 {displayName}";
    }

    private string _build_success_message(
        RecipeDef recipe,
        IReadOnlyDictionary<StringName, ItemDef> itemDefs,
        GDictionary settlement,
        GDictionary payload,
        ItemDef outputItemDef = null)
    {
        GDictionary serviceProfile = _resolve_service_profile(payload);
        string inputSummary = _build_recipe_input_summary(recipe, itemDefs);
        string outputSummary = _build_item_label(recipe.output_item_id, itemDefs, recipe.output_quantity, outputItemDef);
        if (_is_master_reforge_interaction(ReadString(payload, "interaction_script_id")))
        {
            return $"大师工坊已将 {inputSummary} 重铸为 {outputSummary}。";
        }
        string actorLabel = ReadString(payload, "npc_name");
        if (string.IsNullOrEmpty(actorLabel))
        {
            actorLabel = ReadString(payload, "facility_name");
        }
        if (string.IsNullOrEmpty(actorLabel))
        {
            actorLabel = ReadString(settlement, "display_name", ReadString(serviceProfile, "default_facility_name", "工坊"));
        }
        return $"{actorLabel} 已将 {inputSummary} {ReadString(serviceProfile, "recipe_action_phrase", "制作为")} {outputSummary}。";
    }

    private GDictionary _resolve_service_profile(GDictionary payload, string interactionScriptId = "")
    {
        string resolvedInteractionId = (interactionScriptId ?? "").StripEdges();
        if (string.IsNullOrEmpty(resolvedInteractionId))
        {
            resolvedInteractionId = ReadString(payload, "interaction_script_id").StripEdges();
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

        string genericTitleSuffix = ReadString(payload, "service_type", "锻造").StripEdges();
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

    private static SettlementServiceResult _build_result(
        bool success,
        string message,
        IEnumerable<QuestProgressService.QuestProgressEventData> questProgressEvents,
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
        result.SetQuestProgressEventsTyped(questProgressEvents);
        result.SetServiceSideEffects(serviceSideEffects);
        return result;
    }

    private static RecipeDef GetRecipeDef(
        IReadOnlyDictionary<StringName, RecipeDef> recipeDefs,
        StringName key
    )
    {
        if (recipeDefs == null || key == null)
        {
            return null;
        }
        return recipeDefs.TryGetValue(key, out RecipeDef value) ? value : null;
    }

    private static List<StringName> SortedRecipeIds(
        IReadOnlyDictionary<StringName, RecipeDef> recipeDefs
    )
    {
        var result = new List<StringName>();
        if (recipeDefs == null)
        {
            return result;
        }
        foreach (StringName recipeId in recipeDefs.Keys)
        {
            result.Add(recipeId);
        }
        result.Sort((left, right) => string.CompareOrdinal(left.ToString(), right.ToString()));
        return result;
    }

    private static ItemDef GetItemDef(
        IReadOnlyDictionary<StringName, ItemDef> itemDefs,
        StringName key
    )
    {
        if (itemDefs == null || key == null)
        {
            return null;
        }
        return itemDefs.TryGetValue(key, out ItemDef itemDef) ? itemDef : null;
    }

    private static string ReadString(GDictionary data, string key, string fallback = "")
    {
        if (data == null || string.IsNullOrEmpty(key) || !data.ContainsKey(key))
        {
            return fallback;
        }
        Variant value = data[key];
        if (value.VariantType == Variant.Type.String)
        {
            return value.AsString();
        }
        return fallback;
    }

    private static StringName ReadStringName(GDictionary data, string key)
    {
        string value = ReadString(data, key);
        return !string.IsNullOrEmpty(value) ? new StringName(value) : new StringName("");
    }

    private static GArray ReadArray(GDictionary data, string key)
    {
        if (data == null || string.IsNullOrEmpty(key) || !data.ContainsKey(key))
        {
            return new GArray();
        }
        Variant value = data[key];
        return value.VariantType == Variant.Type.Array ? value.AsGodotArray() : new GArray();
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
