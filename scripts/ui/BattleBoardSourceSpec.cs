using System.Collections.Generic;
using Godot;
using GArray = Godot.Collections.Array;
using GDictionary = Godot.Collections.Dictionary;

internal sealed class BattleBoardSourceSpec
{
    internal BattleBoardSourceSpec(
        StringName key,
        IEnumerable<string> files,
        StringName layerRole,
        Vector2I atlasRegionSize,
        Vector2I boardTileSize,
        Vector2I textureOrigin,
        Vector2I visualOrigin,
        bool allowGeneratedFallback
    )
    {
        Key = key;
        LayerRole = layerRole;
        AtlasRegionSize = atlasRegionSize;
        BoardTileSize = boardTileSize;
        TextureOrigin = textureOrigin;
        VisualOrigin = visualOrigin;
        AllowGeneratedFallback = allowGeneratedFallback;
        Files = files != null ? new List<string>(files) : new List<string>();
    }

    internal StringName Key { get; }
    internal IReadOnlyList<string> Files { get; }
    internal StringName LayerRole { get; }
    internal Vector2I AtlasRegionSize { get; }
    internal Vector2I BoardTileSize { get; }
    internal Vector2I TextureOrigin { get; }
    internal Vector2I VisualOrigin { get; }
    internal bool AllowGeneratedFallback { get; }

    internal GDictionary ToDictionary()
    {
        var fileArray = new GArray();
        foreach (string file in Files)
            fileArray.Add(file);

        var result = new GDictionary
        {
            ["key"] = Key,
            ["files"] = fileArray,
            ["layer_role"] = LayerRole,
            ["atlas_region_size"] = AtlasRegionSize,
            ["board_tile_size"] = BoardTileSize,
            ["texture_origin"] = TextureOrigin,
            ["visual_origin"] = VisualOrigin,
            ["allow_generated_fallback"] = AllowGeneratedFallback,
        };
        GodotCollectionDisposer.DisposeWrapperOnly(fileArray);
        return result;
    }

    internal static BattleBoardSourceSpec FromDictionary(
        GDictionary source,
        Vector2I fallbackBoardTileSize
    )
    {
        var files = new List<string>();
        GArray rawFiles = source.ReadArrayOrEmpty("files");
        try
        {
            foreach (Variant fileValue in rawFiles)
            {
                try
                {
                    if (fileValue.VariantType == Variant.Type.String)
                        files.Add(fileValue.AsString());
                    else if (fileValue.VariantType == Variant.Type.StringName)
                        files.Add(fileValue.AsStringName().ToString());
                }
                finally
                {
                    fileValue.Dispose();
                }
            }
        }
        finally
        {
            GodotCollectionDisposer.DisposeWrapperOnly(rawFiles);
        }

        return new BattleBoardSourceSpec(
            source.ReadStringName("key"),
            files,
            source.ReadStringName("layer_role"),
            source.ReadVector2I("atlas_region_size", fallbackBoardTileSize),
            source.ReadVector2I("board_tile_size", fallbackBoardTileSize),
            source.ReadVector2I("texture_origin", Vector2I.Zero),
            source.ReadVector2I("visual_origin", Vector2I.Zero),
            source.ReadBool("allow_generated_fallback", true)
        );
    }
}
