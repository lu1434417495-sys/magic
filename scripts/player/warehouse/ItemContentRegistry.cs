using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using Godot;
using VT = Godot.Variant.Type;

public class ItemContentRegistry : System.IDisposable
{
    private const string ItemConfigDirectory = "res://data/configs/items";
    private const string ItemTemplateDirectory = "res://data/configs/items_templates";

    private readonly IContentResourceLoader _loader;
    private readonly Dictionary<StringName, ItemDefinition> _itemDefs = new();
    private readonly Dictionary<StringName, ItemDefinition> _templateDefs = new();
    private readonly Dictionary<StringName, ItemDefinition> _resolvedTemplateCache = new();
    private readonly List<string> _validationErrors = new();
    private bool _hasBuilt;
    private bool _disposed;

    internal ItemContentRegistry(IContentResourceLoader loader)
    {
        _loader = loader ?? throw new System.ArgumentNullException(nameof(loader));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }
        System.GC.SuppressFinalize(this);
        DisposeManagedRegistry();
    }

    private void DisposeManagedRegistry()
    {
        if (_disposed)
        {
            return;
        }
        _disposed = true;
        _itemDefs.Clear();
        _templateDefs.Clear();
        _resolvedTemplateCache.Clear();
        _validationErrors.Clear();
    }

    public void Rebuild()
    {
        RebuildFromDirectories(
            new Godot.Collections.Array { ItemConfigDirectory },
            new Godot.Collections.Array { ItemTemplateDirectory }
        );
    }

    public void RebuildFromDirectories(
        Godot.Collections.Array itemDirectories,
        Godot.Collections.Array templateDirectories
    )
    {
        _itemDefs.Clear();
        _templateDefs.Clear();
        _resolvedTemplateCache.Clear();
        _validationErrors.Clear();
        _hasBuilt = true;

        foreach (var templateDirectory in templateDirectories)
        {
            string templatePath = templateDirectory.AsString();
            if (string.IsNullOrEmpty(templatePath))
                continue;
            ScanTemplateDirectory(templatePath);
        }

        ResolveAllTemplates();

        foreach (var itemDirectory in itemDirectories)
        {
            string itemPath = itemDirectory.AsString();
            if (string.IsNullOrEmpty(itemPath))
                continue;
            ScanDirectory(itemPath);
        }
    }

    public Godot.Collections.Array<string> Validate()
    {
        EnsureBuilt();
        var result = new Godot.Collections.Array<string>();
        foreach (string error in _validationErrors)
            result.Add(error);
        return result;
    }

    internal IReadOnlyDictionary<StringName, ItemDefinition> GetItemDefsTyped()
    {
        EnsureBuilt();
        return new ReadOnlyDictionary<StringName, ItemDefinition>(_itemDefs);
    }

    internal IReadOnlyList<string> ValidateTyped()
    {
        EnsureBuilt();
        return _validationErrors;
    }

    private void EnsureBuilt()
    {
        if (!_hasBuilt)
            Rebuild();
    }

    private void ScanTemplateDirectory(string directoryPath)
    {
        if (!DirAccess.DirExistsAbsolute(ProjectSettings.GlobalizePath(directoryPath)))
            return;

        DirAccess directory = DirAccess.Open(directoryPath);
        if (directory == null)
        {
            _validationErrors.Add($"ItemContentRegistry could not open templates {directoryPath}.");
            return;
        }

        try
        {
            directory.ListDirBegin();
            while (true)
            {
                string entryName = directory.GetNext();
                if (string.IsNullOrEmpty(entryName))
                    break;
                if (entryName == "." || entryName == "..")
                    continue;

                string entryPath = $"{directoryPath}/{entryName}";
                if (directory.CurrentIsDir())
                {
                    ScanTemplateDirectory(entryPath);
                    continue;
                }
                if (!entryName.EndsWith(".tres") && !entryName.EndsWith(".res"))
                    continue;
                RegisterTemplateResource(entryPath);
            }
            directory.ListDirEnd();
        }
        finally
        {
            GodotObjectLifecycle.DisposeGodotObject(directory);
        }
    }

    private void RegisterTemplateResource(string resourcePath)
    {
        var resource = _loader.LoadCanonical<Resource>(resourcePath);
        if (resource == null)
        {
            _validationErrors.Add($"Failed to load item template {resourcePath}.");
            return;
        }
        if (resource is not ItemDef templateDef)
        {
            _validationErrors.Add($"Item template {resourcePath} is not an ItemDef.");
            return;
        }

        if (templateDef.item_id == "")
        {
            _validationErrors.Add($"Item template {resourcePath} is missing item_id.");
            return;
        }
        if (_templateDefs.ContainsKey(templateDef.item_id))
        {
            _validationErrors.Add($"Duplicate item template id: {(string)templateDef.item_id}");
            return;
        }
        if (!ValidateRawWeaponProfilePropertiesMode(templateDef, $"Item template {resourcePath}"))
        {
            return;
        }
        try
        {
            _templateDefs[templateDef.item_id] = ItemDefinition.FromResource(templateDef);
        }
        catch (InvalidDataException exception)
        {
            _validationErrors.Add(
                $"Item template {resourcePath} projection failed: {exception.Message}"
            );
        }
    }

    private void ResolveAllTemplates()
    {
        foreach (var entry in _templateDefs)
        {
            if (_resolvedTemplateCache.ContainsKey(entry.Key))
                continue;
            var resolved = ResolveWithTemplateChain(
                entry.Value,
                _templateDefs,
                new List<StringName>(),
                _resolvedTemplateCache,
                _validationErrors
            );
            if (resolved != null)
            {
                _resolvedTemplateCache[entry.Key] = resolved;
            }
        }
    }

    private void ScanDirectory(string directoryPath)
    {
        if (!DirAccess.DirExistsAbsolute(ProjectSettings.GlobalizePath(directoryPath)))
        {
            _validationErrors.Add($"ItemContentRegistry could not find {directoryPath}.");
            return;
        }

        DirAccess directory = DirAccess.Open(directoryPath);
        if (directory == null)
        {
            _validationErrors.Add($"ItemContentRegistry could not open {directoryPath}.");
            return;
        }

        try
        {
            directory.ListDirBegin();
            while (true)
            {
                string entryName = directory.GetNext();
                if (string.IsNullOrEmpty(entryName))
                    break;
                if (entryName == "." || entryName == "..")
                    continue;

                string entryPath = $"{directoryPath}/{entryName}";
                if (directory.CurrentIsDir())
                {
                    ScanDirectory(entryPath);
                    continue;
                }
                if (!entryName.EndsWith(".tres") && !entryName.EndsWith(".res"))
                    continue;
                RegisterItemResource(entryPath);
            }
            directory.ListDirEnd();
        }
        finally
        {
            GodotObjectLifecycle.DisposeGodotObject(directory);
        }
    }

    private void RegisterItemResource(string resourcePath)
    {
        var resource = _loader.LoadCanonical<Resource>(resourcePath);
        if (resource == null)
        {
            _validationErrors.Add($"Failed to load item config {resourcePath}.");
            return;
        }
        if (resource is not ItemDef rawDef)
        {
            _validationErrors.Add($"Item config {resourcePath} is not an ItemDef.");
            return;
        }

        if (rawDef.item_id == "")
        {
            _validationErrors.Add($"Item config {resourcePath} is missing item_id.");
            return;
        }
        if (_templateDefs.ContainsKey(rawDef.item_id))
        {
            _validationErrors.Add(
                $"Item config {resourcePath} reuses template id {(string)rawDef.item_id}; templates and instances must use distinct ids."
            );
            return;
        }
        if (!ValidateRawWeaponProfilePropertiesMode(rawDef, $"Item config {resourcePath}"))
        {
            return;
        }

        ItemDefinition itemDef;
        try
        {
            itemDef = ResolveWithTemplateChain(
                ItemDefinition.FromResource(rawDef),
                _templateDefs,
                new List<StringName>(),
                _resolvedTemplateCache,
                _validationErrors
            );
        }
        catch (InvalidDataException exception)
        {
            _validationErrors.Add(
                $"Item config {resourcePath} projection failed: {exception.Message}"
            );
            return;
        }
        if (itemDef == null)
            return;

        var itemTags = itemDef.GetTagsTyped();
        var itemCraftingGroups = itemDef.GetCraftingGroupsTyped();
        if (_itemDefs.ContainsKey(itemDef.ItemId))
        {
            _validationErrors.Add($"Duplicate item_id registered: {(string)itemDef.ItemId}");
            return;
        }

        if (itemDef.CategoryKind == ItemCategoryKind.Unknown)
        {
            _validationErrors.Add(
                $"Item {(string)itemDef.ItemId} declares unsupported item_category {(string)itemDef.ItemCategory}; expected one of misc / equipment / skill_book."
            );
            return;
        }
        if (itemDef.IsStackable && itemDef.MaxStack <= 0)
        {
            _validationErrors.Add($"Item {(string)itemDef.ItemId} must have max_stack >= 1.");
            return;
        }
        if (itemDef.BasePrice > 0 && itemDef.Sellable && itemDef.BuyPrice <= 0)
        {
            _validationErrors.Add(
                $"Sellable item {(string)itemDef.ItemId} must declare explicit buy_price."
            );
            return;
        }
        if (itemDef.BasePrice > 0 && itemDef.Sellable && itemDef.SellPrice <= 0)
        {
            _validationErrors.Add(
                $"Sellable item {(string)itemDef.ItemId} must declare explicit sell_price."
            );
            return;
        }
        if (itemTags.Contains(new StringName("material")) && itemCraftingGroups.Count == 0)
        {
            _validationErrors.Add(
                $"Material item {(string)itemDef.ItemId} must declare at least one crafting_group."
            );
            return;
        }
        if (
            itemTags.Contains(new StringName("quest_item"))
            && itemDef.GetQuestGroupsTyped().Count == 0
        )
        {
            _validationErrors.Add(
                $"Quest item {(string)itemDef.ItemId} must declare at least one quest_group."
            );
            return;
        }
        if (
            itemDef.CategoryKind == ItemCategoryKind.SkillBook && itemDef.GrantedSkillId == ""
        )
        {
            _validationErrors.Add(
                $"Skill book item {(string)itemDef.ItemId} must declare granted_skill_id."
            );
            return;
        }

        if (itemDef.HasEquipmentCategory())
        {
            if (itemDef.IsStackable || itemDef.GetEffectiveMaxStack() != 1)
            {
                _validationErrors.Add(
                    $"Equipment item {(string)itemDef.ItemId} must be non-stackable."
                );
                return;
            }
            if (itemDef.EquipmentSlotIds.Count == 0)
            {
                _validationErrors.Add(
                    $"Equipment item {(string)itemDef.ItemId} must declare at least one slot."
                );
                return;
            }
            foreach (string rawSlotId in itemDef.EquipmentSlotIds)
            {
                if (
                    EquipmentRules.IsValidSlot(
                        ProgressionDataUtils.to_string_name(Variant.From(rawSlotId))
                    )
                )
                    continue;
                _validationErrors.Add(
                    $"Equipment item {(string)itemDef.ItemId} declares invalid slot {rawSlotId}."
                );
                return;
            }
            if (!itemDef.HasValidEquipmentType())
            {
                _validationErrors.Add(
                    $"Equipment item {(string)itemDef.ItemId} must declare equipment_type_id as weapon, armor, or accessory."
                );
                return;
            }
            if (itemDef.IsWeapon() && !ValidateWeaponProfile(itemDef))
                return;

            for (
                int modifierIndex = 0;
                modifierIndex < itemDef.AttributeModifiers.Count;
                modifierIndex++
            )
            {
                AttributeModifierDefinition modifier = itemDef.AttributeModifiers[modifierIndex];
                string modifierLabel =
                    $"Item {(string)itemDef.ItemId} attribute_modifiers[{modifierIndex}]";
                if (modifier == null)
                {
                    _validationErrors.Add(
                        $"{modifierLabel} must be an AttributeModifier (got null)."
                    );
                    return;
                }
                if (modifier.AttributeId == "")
                {
                    _validationErrors.Add($"{modifierLabel}.attribute_id must be non-empty.");
                    return;
                }
                if (!AttributeModifier.IsValidMode(modifier.Mode))
                {
                    _validationErrors.Add(
                        $"{modifierLabel}.mode uses unsupported value {(string)modifier.Mode}."
                    );
                    return;
                }
            }

            if (itemDef.OccupiedSlotIds.Count > 0)
            {
                if (itemDef.EquipmentSlotIds.Count != 1)
                {
                    _validationErrors.Add(
                        $"Equipment item {(string)itemDef.ItemId} declares occupied_slot_ids but equipment_slot_ids must be exactly 1 entry slot."
                    );
                    return;
                }

                var seenOccupied = new HashSet<StringName>();
                var entrySlotId = ProgressionDataUtils.to_string_name(
                    Variant.From(itemDef.EquipmentSlotIds[0])
                );
                bool containsEntrySlot = false;
                foreach (string rawSlotId in itemDef.OccupiedSlotIds)
                {
                    var slotId = ProgressionDataUtils.to_string_name(Variant.From(rawSlotId));
                    if (!EquipmentRules.IsValidSlot(slotId))
                    {
                        _validationErrors.Add(
                            $"Equipment item {(string)itemDef.ItemId} declares invalid occupied_slot {rawSlotId}."
                        );
                        return;
                    }
                    if (seenOccupied.Contains(slotId))
                    {
                        _validationErrors.Add(
                            $"Equipment item {(string)itemDef.ItemId} declares duplicate occupied_slot {(string)slotId}."
                        );
                        return;
                    }
                    seenOccupied.Add(slotId);
                    if (slotId == entrySlotId)
                        containsEntrySlot = true;
                }
                if (!containsEntrySlot)
                {
                    _validationErrors.Add(
                        $"Equipment item {(string)itemDef.ItemId} occupied_slot_ids must include the entry_slot {(string)entrySlotId}."
                    );
                    return;
                }
            }
        }

        _itemDefs[itemDef.ItemId] = itemDef;
    }

    private bool ValidateRawWeaponProfilePropertiesMode(ItemDef itemDef, string label)
    {
        WeaponProfileDef profile = itemDef?.weapon_profile;
        if (profile == null)
        {
            return true;
        }
        if (WeaponProfileDef.IsValidPropertiesMode(profile.properties_mode))
        {
            return true;
        }
        _validationErrors.Add(
            $"{label} weapon_profile.properties_mode uses unsupported value {profile.properties_mode}."
        );
        return false;
    }

    private bool ValidateWeaponProfile(ItemDefinition itemDef)
    {
        WeaponProfileDefinition resolvedProfile = itemDef.WeaponProfile;
        if (resolvedProfile == null)
        {
            _validationErrors.Add(
                $"Weapon item {(string)itemDef.ItemId} must declare weapon_profile."
            );
            return false;
        }
        if (resolvedProfile.WeaponTypeId == "")
        {
            _validationErrors.Add(
                $"Weapon item {(string)itemDef.ItemId} weapon_profile.weapon_type_id must be non-empty."
            );
            return false;
        }
        if (resolvedProfile.Family == "")
        {
            _validationErrors.Add(
                $"Weapon item {(string)itemDef.ItemId} weapon_profile.family must be non-empty."
            );
            return false;
        }
        if (resolvedProfile.RangeType == "")
        {
            _validationErrors.Add(
                $"Weapon item {(string)itemDef.ItemId} weapon_profile.range_type must be non-empty."
            );
            return false;
        }
        if (resolvedProfile.DamageTag == "")
        {
            _validationErrors.Add(
                $"Weapon item {(string)itemDef.ItemId} weapon_profile.damage_tag must be non-empty."
            );
            return false;
        }
        if (resolvedProfile.AttackRange <= 0)
        {
            _validationErrors.Add(
                $"Weapon item {(string)itemDef.ItemId} weapon_profile.attack_range must be >= 1 (got {resolvedProfile.AttackRange})."
            );
            return false;
        }
        if (resolvedProfile.OneHandedDice == null && resolvedProfile.TwoHandedDice == null)
        {
            _validationErrors.Add(
                $"Weapon item {(string)itemDef.ItemId} weapon_profile must declare at least one of one_handed_dice or two_handed_dice."
            );
            return false;
        }

        var diceErrors = new Godot.Collections.Array<string>();
        if (resolvedProfile.OneHandedDice != null)
        {
            foreach (
                string error in WeaponDamageDiceDefinition.ValidateDice(
                    $"Weapon item {(string)itemDef.ItemId} weapon_profile.one_handed_dice",
                    resolvedProfile.OneHandedDice
                )
            )
                diceErrors.Add(error);
        }
        if (resolvedProfile.TwoHandedDice != null)
        {
            foreach (
                string error in WeaponDamageDiceDefinition.ValidateDice(
                    $"Weapon item {(string)itemDef.ItemId} weapon_profile.two_handed_dice",
                    resolvedProfile.TwoHandedDice
                )
            )
                diceErrors.Add(error);
        }
        if (diceErrors.Count > 0)
        {
            foreach (string diceError in diceErrors)
                _validationErrors.Add(diceError);
            return false;
        }
        if (itemDef.GetWeaponPhysicalDamageTag() == "")
        {
            _validationErrors.Add(
                $"Weapon item {(string)itemDef.ItemId} must declare one valid weapon_profile.damage_tag."
            );
            return false;
        }
        return true;
    }

    private static ItemDefinition ResolveWithTemplateChain(
        ItemDefinition itemDef,
        IReadOnlyDictionary<StringName, ItemDefinition> templateDefs,
        List<StringName> visited,
        Dictionary<StringName, ItemDefinition> cache,
        List<string> errors
    )
    {
        if (itemDef == null)
        {
            errors.Add("resolve_with_template_chain received null item_def.");
            return null;
        }

        StringName currentId = itemDef.ItemId;
        if (visited.Contains(currentId))
        {
            string chainText = string.Join(", ", visited) + $" -> {(string)currentId}";
            errors.Add(
                $"Item template inheritance cycle detected at {(string)currentId} (chain: {chainText})."
            );
            return null;
        }

        if (itemDef.BaseItemId == "")
            return itemDef;

        StringName templateId = itemDef.BaseItemId;
        if (!templateDefs.ContainsKey(templateId))
        {
            errors.Add(
                $"Item {(string)currentId} references missing template {(string)templateId}."
            );
            return null;
        }

        ItemDefinition resolvedTemplate;
        if (cache.ContainsKey(templateId))
        {
            resolvedTemplate = cache[templateId];
        }
        else
        {
            var nextVisited = new List<StringName>(visited) { currentId };
            resolvedTemplate = ResolveWithTemplateChain(
                templateDefs[templateId],
                templateDefs,
                nextVisited,
                cache,
                errors
            );
            if (resolvedTemplate != null)
                cache[templateId] = resolvedTemplate;
        }

        return resolvedTemplate == null
            ? null
            : ItemDefinition.MergeWithTemplate(resolvedTemplate, itemDef);
    }
}
