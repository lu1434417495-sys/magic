# 基础战斗人物贴图

生成日期：2026-09-13。使用内置 imagegen，每个角色单独生成，原始透明 RGBA PNG 直接复制入项目，未裁切或修改像素。保留初版默认站姿，并提供三种角色各四方向的基础静态站姿。实际朝向状态与转向规则尚未引入。

## 四方向素材

| 外观 | 图集 | 单方向资源 |
| --- | --- | --- |
| 战士 | `assets/main/battle/units/player/basic_warrior_4way.png` | `basic_warrior_{direction}.tres` |
| 法师 | `assets/main/battle/units/player/basic_mage_4way.png` | `basic_mage_{direction}.tres` |
| 弓箭手 | `assets/main/battle/units/player/basic_archer_4way.png` | `basic_archer_{direction}.tres` |

单方向资源与 PNG 同目录，使用 `AtlasTexture` 的 region / margin，不复制或改写 PNG 像素。三张实际图集尺寸均为 1086 × 1448，按 2 × 2 排列。方向名称以屏幕视角定义，不代表战斗网格的坐标轴：

| 图集位置 | direction | 人物朝向 |
| --- | --- | --- |
| 左上 | `front_left` | 左前，面向屏幕左下，可见正面 |
| 右上 | `front_right` | 右前，面向屏幕右下，可见正面 |
| 左下 | `back_left` | 左后，面向屏幕左上，可见背面 |
| 右下 | `back_right` | 右后，面向屏幕右上，可见背面 |

12 个资源已登记到 engine asset catalog，ID 为 `battle.unit.player.{warrior|mage|archer}.{direction}`。每帧暴露 768 × 1024 的统一逻辑画布，alpha ≥ 128 的主体边界底边中点对齐 `(384, 940)`，半像素横向边界允许 0.5 像素舍入。PNG 的低 alpha 边缘保留，导入启用 mipmap。

现有棋盘可以通过显式 `battle_sprite_asset_id` 与 unit delta 显示任意一帧。默认按武器选择的三个不带方向 ID 仍使用下面的初版站姿。后续朝向系统可选择带方向 ID；这里没有增加朝向字段、自动转向、存档数据或动作动画。

## 初版默认站姿

| 外观 | 文件 | Engine asset ID |
| --- | --- | --- |
| 剑盾战士 | `assets/main/battle/units/player/basic_warrior.png` | `battle.unit.player.warrior` |
| 持杖法师 | `assets/main/battle/units/player/basic_mage.png` | `battle.unit.player.mage` |
| 弓箭手 | `assets/main/battle/units/player/basic_archer.png` | `battle.unit.player.archer` |

完整生成提示如下。

## warrior

```text
Use case: stylized-concept. Asset type: production 2D character sprite for a Godot isometric tactical fantasy RPG, not a concept sheet. ONE full-body character, three-quarter isometric view from slightly above, facing down-right. Refined hand-painted medieval fantasy strategy-game art, clear chunky readable silhouette, softly painted volume, understated dark contour, visually readable when reduced to about 90 pixels tall. Grounded adult proportions with slightly enlarged head and hands for readability, about five heads tall, not chibi. Centered standing idle combat-ready pose. Transparent RGBA background with actual alpha, no scenery, no floor, no base, no shadow, no text, no border, no grid. Entire head, boots and weapon visible, no cropping. Portrait canvas 1024x1536 with only 8 percent clear safety margin around character; feet on same horizontal baseline. Warm neutral directional light from upper left. Subject: basic human WARRIOR, practical steel helmet with visible face, steel shoulder armor and breastplate, muted red cloth tabard, brown leather belt and boots. One simple steel sword held safely lowered to the character's right and one modest round wooden shield with metal rim on left arm. Compact solid veteran foot-soldier silhouette. No giant ornaments, no effects.
```

## mage

```text
Use case: stylized-concept. Asset type: production 2D character sprite for a Godot isometric tactical fantasy RPG, not a concept sheet. ONE full-body character, three-quarter isometric view from slightly above, facing down-right. Refined hand-painted medieval fantasy strategy-game art, clear chunky readable silhouette, softly painted volume, understated dark contour, visually readable when reduced to about 90 pixels tall. Grounded adult proportions with slightly enlarged head and hands for readability, about five heads tall, not chibi. Centered standing idle combat-ready pose. Transparent RGBA background with actual alpha, no scenery, no floor, no base, no shadow, no text, no border, no grid. Entire head, boots and weapon visible, no cropping. Portrait canvas 1024x1536 with only 8 percent clear safety margin around character; feet on same horizontal baseline. Warm neutral directional light from upper left. Subject: basic human MAGE, deep indigo-blue robe with muted antique-gold trim, simple hood lowered around neck, medium grey hair, calm adult face. A single straight wooden staff held vertically at the side topped by a small blue crystal, free hand relaxed near waist. Brown leather belt and visible brown boots below robe. Compact composed silhouette. No swirling effects, no magic circle, no giant ornaments.
```

## archer

```text
Use case: stylized-concept. Asset type: production 2D character sprite for a Godot isometric tactical fantasy RPG, not a concept sheet. ONE full-body character, three-quarter isometric view from slightly above, facing down-right. Refined hand-painted medieval fantasy strategy-game art, clear chunky readable silhouette, softly painted volume, understated dark contour, visually readable when reduced to about 90 pixels tall. Grounded adult proportions with slightly enlarged head and hands for readability, about five heads tall, not chibi. Centered standing idle combat-ready pose. Transparent RGBA background with actual alpha, no scenery, no floor, no base, no shadow, no text, no border, no grid. Entire head, boots and weapon visible, no cropping. Portrait canvas 1024x1536 with only 8 percent clear safety margin around character; feet on same horizontal baseline. Warm neutral directional light from upper left. Subject: basic human ARCHER, muted forest-green hood and short green cloak, brown leather tunic and bracers, practical trousers and brown boots. A single recognizable curved wooden longbow held at the side, quiver of arrows on back. Calm alert adult face visible inside hood. Compact balanced ranger silhouette. No drawn arrow, no effects, no giant ornaments.
```

## 四方向最终生成提示

采用内置 imagegen 新图生成模式，每种角色单独一张图集；初稿因伪透明及重复视角弃用，未进入项目。以下为最终采用的完整提示，生成器实际输出尺寸以文件为准。

### warrior 四方向

```text
Production game sprite sheet: exactly four full-body views of one medieval fantasy character on a genuinely TRANSPARENT RGBA background. Background pixels alpha=0. DO NOT draw checkerboard squares; do not draw any background, floor, shadows or text. A 2-by-2 grid, equal portrait cells, 1536x2048 overall. Top-left character faces LOWER LEFT (southwest), front and left-facing profile visible. Top-right faces LOWER RIGHT (southeast), front and right-facing profile visible. Bottom-left faces UPPER LEFT (northwest), back and left side visible, boots point left. Bottom-right faces UPPER RIGHT (northeast), back and right side visible, boots point right. These must be four clearly different whole-body rotations at 45-degree diagonals, NOT front views with only the head turned. Bottom row shows only backs, never faces. Consistent orthographic camera slightly above, hand-painted fantasy tactical game art. Same model, same scale, same neutral standing pose, unchanged clothing in every view. Generous transparent spacing between sprites, entire weapons and feet inside each cell. Feet aligned to 92 percent cell height. No labels. Subject: Steel helmet, steel breastplate and shoulder plates, muted red tabard, brown trousers and boots. A lowered steel sword in own right hand, round wood shield on own left arm. Both rear diagonal silhouettes must have noticeably opposite facing directions.
```

### mage 四方向

```text
Production game sprite sheet: exactly four full-body views of one medieval fantasy character on a genuinely TRANSPARENT RGBA background. Background pixels alpha=0. DO NOT draw checkerboard squares; do not draw any background, floor, shadows or text. A 2-by-2 grid, equal portrait cells, 1536x2048 overall. Top-left character faces LOWER LEFT (southwest), front and left-facing profile visible. Top-right faces LOWER RIGHT (southeast), front and right-facing profile visible. Bottom-left faces UPPER LEFT (northwest), back and left side visible, boots point left. Bottom-right faces UPPER RIGHT (northeast), back and right side visible, boots point right. These must be four clearly different whole-body rotations at 45-degree diagonals, NOT front views with only the head turned. Bottom row shows only backs, never faces. Consistent orthographic camera slightly above, hand-painted fantasy tactical game art. Same model, same scale, same neutral standing pose, unchanged clothing in every view. Generous transparent spacing between sprites, entire weapons and feet inside each cell. Feet aligned to 92 percent cell height. No labels. Subject: Grey-haired male mage with lowered hood, blue robe and restrained antique gold trim, brown belt and boots. Wooden staff with small blue crystal in own right hand. Four distinct diagonal full-body views.
```

### archer 四方向

```text
Production game sprite sheet: exactly four full-body views of one medieval fantasy character on a genuinely TRANSPARENT RGBA background. Background pixels alpha=0. DO NOT draw checkerboard squares; do not draw any background, floor, shadows or text. A 2-by-2 grid, equal portrait cells, 1536x2048 overall. Top-left character faces LOWER LEFT (southwest), front and left-facing profile visible. Top-right faces LOWER RIGHT (southeast), front and right-facing profile visible. Bottom-left faces UPPER LEFT (northwest), back and left side visible, boots point left. Bottom-right faces UPPER RIGHT (northeast), back and right side visible, boots point right. These must be four clearly different whole-body rotations at 45-degree diagonals, NOT front views with only the head turned. Bottom row shows only backs, never faces. Consistent orthographic camera slightly above, hand-painted fantasy tactical game art. Same model, same scale, same neutral standing pose, unchanged clothing in every view. Generous transparent spacing between sprites, entire weapons and feet inside each cell. Feet aligned to 92 percent cell height. No labels. Subject: Green hood and short forest-green cloak over brown leather tunic and bracers, brown boots. Longbow held in own left hand, quiver fixed on back. Four distinct diagonal full-body views.
```
