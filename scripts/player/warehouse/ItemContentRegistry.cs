using System.Collections.Generic;
using System.Collections.ObjectModel;
using Godot;

public sealed class ItemContentRegistry : System.IDisposable
{
    private readonly Dictionary<StringName, ItemDefinition> _itemDefs = new();
    private readonly List<string> _validationErrors = new();
    private bool _hasBuilt;
    private bool _disposed;

    internal ItemContentRegistry() { }

    public void Dispose()
    {
        if (_disposed)
            return;
        System.GC.SuppressFinalize(this);
        _disposed = true;
        _itemDefs.Clear();
        _validationErrors.Clear();
    }

    public void Rebuild()
    {
        RebuildFromJsonDirectory(
            ItemContentJsonAuthoringDomain.ProductionDirectory,
            new GodotContentJsonSourceReader()
        );
    }

    internal void RebuildFromJsonDirectory(
        string directoryPath,
        IContentJsonSourceReader sourceReader
    )
    {
        ClearBuildState();
        _hasBuilt = true;

        ContentImportBatch<ItemImportModel> batch =
            ItemContentJsonAuthoringDomain.CreateImportDescriptor(
                directoryPath,
                sourceReader
            ).Import();
        foreach (ContentJsonDiagnostic diagnostic in batch.Diagnostics)
        {
            _validationErrors.Add(
                $"{diagnostic.RuleId} {diagnostic.SourceLabel}{diagnostic.JsonPointer}: {diagnostic.Message}"
            );
        }
        if (batch.HasErrors)
            return;

        foreach (ContentImportEntry<ItemImportModel> entry in batch.Entries)
            RegisterJsonImport(entry);
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

    private void ClearBuildState()
    {
        _itemDefs.Clear();
        _validationErrors.Clear();
    }

    private void RegisterJsonImport(ContentImportEntry<ItemImportModel> entry)
    {
        ItemImportModel import = entry.Import;
        var itemId = new StringName(import.ItemId);
        if (_itemDefs.ContainsKey(itemId))
        {
            _validationErrors.Add($"Duplicate item_id registered: {itemId}");
            return;
        }

        ItemDefinition definition;
        try
        {
            definition = ItemDefinitionProjector.Project(import);
        }
        catch (System.Exception exception)
            when (exception is System.IO.InvalidDataException
                or System.InvalidOperationException)
        {
            _validationErrors.Add(
                $"Item {itemId} projection failed at {entry.Context.SourceLabel}{entry.Context.JsonPointer}: {exception.GetType().Name}: {exception.Message}"
            );
            return;
        }

        int sellPriceCap = (definition.BuyPrice + 1) / 2;
        if (
            definition.Sellable
            && definition.BuyPrice > 0
            && definition.SellPrice > sellPriceCap
        )
        {
            // 原先只发一条运行时警告然后按 clamp 后的价格卖，JSON 写的数和游戏跑的数
            // 不一致且无人察觉。作者写错就该在内容校验期拒绝。
            _validationErrors.Add(
                $"Item {itemId} at {entry.Context.SourceLabel}{entry.Context.JsonPointer} declares "
                    + $"sell_price {definition.SellPrice} above half of buy_price "
                    + $"{definition.BuyPrice} (max {sellPriceCap}); shop arbitrage is not allowed."
            );
            return;
        }

        _itemDefs.Add(itemId, definition);
    }
}
