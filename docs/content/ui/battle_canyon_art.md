# 手绘峡谷战场素材

制作日期：2026-09-13。使用内置 imagegen，以本次获确认的峡谷概念图作为画风参考，分别生成四张正式素材；未使用 CLI / API fallback。生成结果原样复制进项目，透明图保留原始 alpha；缩放和重复取样由 Godot 完成。

| 文件（位于 `assets/main/battle/terrain/canyon_painted/`） | 实际尺寸 | 用途 |
| --- | --- | --- |
| `sandstone_ground.png` | 1254 × 1254 RGB | 连续地表及外围环境基底 |
| `sandstone_cliff.png` | 1254 × 1254 RGB | 连续岩壁表面 |
| `canyon_oak.png` | 1254 × 1254 RGBA | 森林格树木 |
| `canyon_scrub.png` | 1774 × 887 RGBA | 岩缘与外围碎石灌木 |

这些是重复使用的材质和透明装饰，不是整幅战场背景。4K 指原生游戏 viewport 的 3840 × 2160 输出；生成提示中的期望尺寸不代表实际返回尺寸。导入设置启用 mipmap，消费侧使用 LinearWithMipmaps。

渲染 owner、地形数据与验证边界见 `docs/design/ui/battle_map_presentation.md`。当前仍保留规则化的等距台地轮廓；这批素材建立统一画风，不能等同于完整手绘整张地图的自然构图。

## 完整提示词

每次调用均把同一张已确认的峡谷概念图作为 `referenced_image_paths` 的样式参考。

### 砂岩地面

```text
Use case: stylized-concept. Asset type: seamless albedo game ground material, square 2048 x 2048. Input image is STYLE REFERENCE ONLY. Create ONE flat top-down seamless repeatable texture of the pale golden sandstone canyon floor seen in the reference: a broad quiet expanse of pale warm ochre dusty earth, painterly angular sandstone patches embedded flush into the ground, subtle soft mottling and very sparse tiny pebbles, elegant restrained hand-painted gouache fantasy tactical RPG art. Broad low contrast forms, warm ivory / muted apricot / sandstone ochre, same light painted brushwork as reference. Orthographic straight down material scan, uniform light across frame, no perspective, no isometric diamond, no cliff sides, no trees, no grass, no shadows of objects, no water, no tile borders, no grid, no UI, no text, no transparency, no vignette. Edge-to-edge ground surface. Must tile seamlessly in both axes, no central focal object. Large coherent brush strokes with subtle organic variation, NOT photorealistic noise or rough gritty microdetail.
```

### 树木

```text
Use case stylized-concept. Asset: ONE production 2D game tree sprite on genuinely transparent alpha background. Reference image is art style only. Paint a beautiful mature canyon oak tree for the same hand-painted tactical RPG, 2:1 orthographic isometric camera looking down from 30 degrees. Broad spreading sage olive green foliage in painterly clustered faceted shapes, desaturated deep teal green shadows and warm yellow olive sunlit upper left tips, gracefully crooked weathered ochre brown trunk with a fork and visible roots. Gouache brushwork, high end game illustration, NOT photorealistic. Whole tree centered, fills 85% of a square image, all leaves and roots fully in frame, some holes between foliage showing transparent background, clear trunk visible below crown. A few little sage grasses at the roots only. Strong cohesive silhouette readable at 200px. Soft light upper left. No ground tile, no diamond base, no pedestal, no scenery, no cast shadow outside tree, no grid, no text, no checkerboard image, actual transparent background. Single tree only.
```

### 岩壁

```text
Use case stylized-concept. Asset type: seamless 2D game cliff ROCK FACE material texture, square. Reference image is painting style only. Edge-to-edge frontal flat elevation view of warm ochre sandstone natural cliff rock face, hand-painted gouache fantasy tactical RPG art. Irregular broad weathered angular stone plates, subtle horizontal sedimentary stratification and occasional dark irregular vertical crevices, warm amber upper edges, muted brown and terracotta midtones, deep desaturated brown shadow recesses. Continuous natural rock formation NOT stacked masonry bricks. Painterly facets and controlled broad brushwork matching reference. Even soft illumination with gentle light from upper left. Seamless repeating rock face both axes, no perspective foreshortening, no top surface, no isometric diamond, no objects, no grass, no backdrop, no edge of cliff, no borders, no grid, no text, no vignette. Every pixel filled with rock. Designed to cover continuous vertical cliff meshes without visible tile cells.
```

### 碎石灌木

```text
Use case stylized-concept. Asset type ONE low ground decoration sprite for a painterly canyon tactical RPG, actual transparent alpha background. Use reference only for painting style and palette. A small natural asymmetric cluster of weathered pale warm ochre sandstone stones with a few sage green dry grasses and a compact olive scrub bush nestled between them. Largest rock is knee high, low broad cluster, no large tree. Orthographic 2:1 isometric looking down 30 degrees, coherent upper left golden sunlight, soft desaturated brown shadows. Hand-painted gouache with visible angular brush shapes, elegant fantasy environment art matching the reference. Single isolated decoration, all objects within central 80% of frame, ground footprint wide and shallow ellipse, small soft contact shadow within footprint only. No platform, no diamond tile, no background floor, no backdrop, no floating debris, no text, no labels, no grid, no checkerboard. Actual transparent background.
```
