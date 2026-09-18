# 基础战斗人物贴图

生成日期：2026-09-13；补充日期：2026-09-15。使用内置 imagegen，每个角色单独生成，原始透明 RGBA PNG 直接复制入项目，未裁切或修改像素。保留初版默认站姿，并提供九种外观各四方向的基础静态站姿。实际朝向状态与转向规则尚未引入。

## 四方向素材

| 外观 | 图集 | 单方向资源 |
| --- | --- | --- |
| 战士 | `assets/main/battle/units/player/basic_warrior_4way.png` | `basic_warrior_{direction}.tres` |
| 法师 | `assets/main/battle/units/player/basic_mage_4way.png` | `basic_mage_{direction}.tres` |
| 弓箭手 | `assets/main/battle/units/player/basic_archer_4way.png` | `basic_archer_{direction}.tres` |
| 长枪战士 | `assets/main/battle/units/player/basic_spear_warrior_4way.png` | `basic_spear_warrior_{direction}.tres` |
| 持盾战士 | `assets/main/battle/units/player/basic_shield_warrior_4way.png` | `basic_shield_warrior_{direction}.tres` |
| 术士 | `assets/main/battle/units/player/basic_warlock_4way.png` | `basic_warlock_{direction}.tres` |
| 刺客 | `assets/main/battle/units/player/basic_assassin_4way.png` | `basic_assassin_{direction}.tres` |
| 狂战士 | `assets/main/battle/units/player/basic_berserker_4way.png` | `basic_berserker_{direction}.tres` |
| 骑士 | `assets/main/battle/units/player/basic_knight_4way.png` | `basic_knight_{direction}.tres` |

单方向资源与 PNG 同目录，使用 `AtlasTexture` 的 region / margin，不复制或改写 PNG 像素。图集按 2 × 2 排列。方向名称以屏幕视角定义，不代表战斗网格的坐标轴：

| 图集位置 | direction | 人物朝向 |
| --- | --- | --- |
| 左上 | `front_left` | 左前，面向屏幕左下，可见正面 |
| 右上 | `front_right` | 右前，面向屏幕右下，可见正面 |
| 左下 | `back_left` | 左后，面向屏幕左上，可见背面 |
| 右下 | `back_right` | 右后，面向屏幕右上，可见背面 |

36 个资源已登记到 engine asset catalog，ID 为 `battle.unit.player.{kind}.{direction}`，kind 对应 `warrior`、`mage`、`archer`、`spear_warrior`、`shield_warrior`、`warlock`、`assassin`、`berserker`、`knight`。每帧暴露 768 × 1024 的统一逻辑画布，alpha ≥ 128 的主体边界底边中点保留在 `(384, 940)`，但此点不再作为站立锚点。资源使用 `BattleUnitSpriteTexture` 保存逻辑画布内的 `LeftFootContact` / `RightFootContact`，标定在两只靴子的实际鞋底；站立锚点取双脚中点，使前后两只脚围绕格子中心落地。半像素边界允许 0.5 像素舍入。PNG 的低 alpha 边缘保留，导入启用 mipmap。

长枪战士使用木杆钢刃长枪、中型钢甲和赭色布衣；持盾战士使用大鸢盾、重甲和蓝灰布衣，与初版小圆盾战士区分；术士使用紫黑兜帽长袍、魔典和短仪式杖，与蓝袍长杖法师区分。它们是可显式选择的外观资源，不承担职业、技能或装备规则。

刺客采用深色兜帽、蒙面轻皮甲和双匕首；狂战士采用裸露手臂、皮革毛皮装备和双手战斧；骑士采用步战站姿、封闭头盔、完整钢制板甲、白蓝战袍和双手持握的长剑。三者共用四方向静态素材规格。

现有棋盘可以通过显式 `battle_sprite_asset_id` 与 unit delta 显示任意一帧。默认按武器选择的三个不带方向 ID 仍使用下面的初版站姿。后续朝向系统可选择带方向 ID；这里没有增加朝向字段、自动转向、存档数据或动作动画。

## 初版默认站姿

| 外观 | 文件 | Engine asset ID |
| --- | --- | --- |
| 剑盾战士 | `assets/main/battle/units/player/basic_warrior.png` | `battle.unit.player.warrior` |
| 持杖法师 | `assets/main/battle/units/player/basic_mage.png` | `battle.unit.player.mage` |
| 弓箭手 | `assets/main/battle/units/player/basic_archer.png` | `battle.unit.player.archer` |

三个默认 ID 通过同目录的 `basic_warrior.tres`、`basic_mage.tres`、`basic_archer.tres` 引用原始整张 PNG，并保存各自的双脚接触点。像素和对外 asset ID 均不变；所有人物站姿统一使用双脚中点定位，血条同步跟随。

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

## 2026-09-15 补充素材的最终提示

使用内置 imagegen 新图生成模式；每种外观单独生成。三张输出均为 1086 × 1448 RGBA。长枪战士采用复核后的四个不同视角版本；未采用的生成草稿留在工作区外。

### spear_warrior

```text
A production transparent PNG game sprite atlas of one basic human SPEAR WARRIOR, exactly four standing full-body views in a 2x2 grid. Genuinely transparent RGBA, background pixels alpha=0, no checkerboard or backdrop. Top left: WHOLE BODY facing diagonally down-left, nose pointing LEFT, front of face visible. Top right: WHOLE BODY facing diagonally down-right, nose pointing RIGHT, front of face visible. Bottom left: back view facing diagonally up-left, boots pointing LEFT, face invisible. Bottom right: back view facing diagonally up-right, boots pointing RIGHT, face invisible. These FOUR directions are the main requirement. SAME adult warrior wearing open-face steel helmet, practical steel breastplate and shoulder plates, muted ochre cloth tunic and scarf, leather belt, metal greaves and brown boots. Each view carries ONE long, straight, continuous wooden spear with steel leaf-shaped point, held nearly upright beside body, tip just above helmet, shaft down near boots. No shield, sword or firearm. Hand-painted medieval fantasy tactical RPG style, restrained realistic material texture, soft even light, readable silhouette, adult proportions, fixed orthographic camera slightly above looking down. Same scale and pose in each cell, feet at matching baseline, entire spear and boots visible, ample transparent gutters. Canvas 1536x2048. No text, numbers, labels, shadows, smoke or effects. Keep all four bodies separate.
```

### shield_warrior

```text
Production game sprite sheet: exactly FOUR full-body views of ONE medieval fantasy character on a genuinely TRANSPARENT RGBA background. Background alpha=0. DO NOT draw checkerboard squares, background, floor, ground shadows, text or labels. A strict 2-by-2 grid, equal portrait cells, 1536x2048 overall. Top-left faces LOWER LEFT (southwest), front three-quarter left-facing view. Top-right faces LOWER RIGHT (southeast), front three-quarter right-facing view. Bottom-left faces UPPER LEFT (northwest), back three-quarter left-facing view, boots pointing left. Bottom-right faces UPPER RIGHT (northeast), back three-quarter right-facing view, boots pointing right. Four clearly different whole-body rotations, NOT only head turns. Bottom row shows backs of heads and clothing, faces hidden. Fixed orthographic camera slightly above looking down, painterly medieval fantasy tactical RPG sprite art, grounded adult proportions, substantial readable silhouettes, detailed steel/leather/cloth with restrained dark contours, muted natural colors and soft light. Same character, clothing, equipment, body scale and calm standing combat-ready pose across all four views. Entire boots and weapons fit well inside their own cell, clear transparent gutters. Each body occupies about 75 percent of cell height, feet on a matching baseline near 92 percent cell height. No magical circles, smoke, glowing background, particles, or scenery. Subject: A human SHIELD WARRIOR, distinctly heavy defensive infantry. A large blue-grey kite shield covering from chest to shin, broad steel rim and one simple muted bronze central boss, held on the anatomical left arm. A short plain steel sword lowered in anatomical right hand, no spear. Enclosed steel helmet, heavy steel shoulder plates, chainmail and breastplate, blue-grey cloth skirt, sturdy greaves and brown boots. Compact broad stance. Rear views clearly show the back of shield with straps and arm as appropriate. The large shield is the visual focus, bigger than a small round buckler.
```

### warlock

```text
Production game sprite sheet: exactly FOUR full-body views of ONE medieval fantasy character on a genuinely TRANSPARENT RGBA background. Background alpha=0. DO NOT draw checkerboard squares, background, floor, ground shadows, text or labels. A strict 2-by-2 grid, equal portrait cells, 1536x2048 overall. Top-left faces LOWER LEFT (southwest), front three-quarter left-facing view. Top-right faces LOWER RIGHT (southeast), front three-quarter right-facing view. Bottom-left faces UPPER LEFT (northwest), back three-quarter left-facing view, boots pointing left. Bottom-right faces UPPER RIGHT (northeast), back three-quarter right-facing view, boots pointing right. Four clearly different whole-body rotations, NOT only head turns. Bottom row shows backs of heads and clothing, faces hidden. Fixed orthographic camera slightly above looking down, painterly medieval fantasy tactical RPG sprite art, grounded adult proportions, substantial readable silhouettes, detailed steel/leather/cloth with restrained dark contours, muted natural colors and soft light. Same character, clothing, equipment, body scale and calm standing combat-ready pose across all four views. Entire boots and weapons fit well inside their own cell, clear transparent gutters. Each body occupies about 75 percent of cell height, feet on a matching baseline near 92 percent cell height. No magical circles, smoke, glowing background, particles, or scenery. Subject: A human WARLOCK, dark plum-purple hooded robe layered over charcoal tunic, restrained tarnished-silver clasps, leather belt and small occult talisman, brown boots. Adult pale face visible inside hood only in front views. One CLOSED compact leather grimoire held under the left arm and a short ritual wand topped with a small muted violet stone in the right hand, no long staff and no sword. Back views show the back of hood and layered purple mantle. Quiet, composed sinister spellcaster, readable silhouette without oversized ornaments. NO spell effects or aura.
```

## 2026-09-15 刺客、狂战士与骑士的最终提示

使用内置 imagegen 新图生成模式，每种外观单独生成。三张输出均为 1086 × 1448 RGBA，原始像素保留在项目内，Godot AtlasTexture 负责四方向切片与锚点。

### assassin

```text
A production transparent PNG game sprite atlas, exactly FOUR full-body standing views of ONE medieval fantasy character in a strict 2x2 grid. Genuinely transparent RGBA, background pixels alpha=0, no checkerboard or backdrop. TOP LEFT: whole body faces diagonally DOWN-LEFT, nose pointing LEFT, front of face visible. TOP RIGHT: whole body faces diagonally DOWN-RIGHT, nose pointing RIGHT, front of face visible. BOTTOM LEFT: rear three-quarter view facing diagonally UP-LEFT, boots pointing LEFT, face invisible. BOTTOM RIGHT: rear three-quarter view facing diagonally UP-RIGHT, boots pointing RIGHT, face invisible. These FOUR distinct whole-body directions are the main requirement, no duplicate poses with only heads turned. SAME character, outfit, equipment, body proportions, lighting and body scale in all four cells. Hand-painted medieval fantasy tactical RPG sprite style, restrained realistic steel/leather/cloth texture, soft even light, muted colors, dark fine contours, grounded adult proportions and clear readable silhouettes. Fixed orthographic camera slightly above looking down. Calm combat-ready standing idle, entire head, boots and weapons visible inside each cell, ample transparent gutters, feet at matching baseline near 92 percent of cell height. Canvas 1536x2048. No text, numbers, labels, ground shadows, smoke, aura, particles or scenery. Subject: Human ASSASSIN with TWO short steel daggers, one in each hand held lowered, compact blade poses inside body silhouette. Slim agile adult, dark charcoal hood, lower face covered by a dark cloth mask, eyes visible in front views. Fitted dark leather armor, subtle muted teal sash, layered leather bracers, tapered trousers and soft boots, short split cloth shoulder cape ending at waist. No long robe, no bow, no shield. Rear views show hood back and crossed short leather belts. Understated stealth silhouette, not bulky.
```

### berserker

```text
A production transparent PNG game sprite atlas, exactly FOUR full-body standing views of ONE medieval fantasy character in a strict 2x2 grid. Genuinely transparent RGBA, background pixels alpha=0, no checkerboard or backdrop. TOP LEFT: whole body faces diagonally DOWN-LEFT, nose pointing LEFT, front of face visible. TOP RIGHT: whole body faces diagonally DOWN-RIGHT, nose pointing RIGHT, front of face visible. BOTTOM LEFT: rear three-quarter view facing diagonally UP-LEFT, boots pointing LEFT, face invisible. BOTTOM RIGHT: rear three-quarter view facing diagonally UP-RIGHT, boots pointing RIGHT, face invisible. These FOUR distinct whole-body directions are the main requirement, no duplicate poses with only heads turned. SAME character, outfit, equipment, body proportions, lighting and body scale in all four cells. Hand-painted medieval fantasy tactical RPG sprite style, restrained realistic steel/leather/cloth texture, soft even light, muted colors, dark fine contours, grounded adult proportions and clear readable silhouettes. Fixed orthographic camera slightly above looking down. Calm combat-ready standing idle, entire head, boots and weapons visible inside each cell, ample transparent gutters, feet at matching baseline near 92 percent of cell height. Canvas 1536x2048. No text, numbers, labels, ground shadows, smoke, aura, particles or scenery. Subject: Human BERSERKER, broad muscular adult man, rugged bearded face, short rough reddish-brown hair, no helmet. Bare muscular arms and much of upper chest, crossed brown leather harness, one modest fur shoulder mantle, worn red-brown kilt panels, heavy belt, leather trousers and rugged fur-trimmed boots. Holds ONE large two-handed steel battleaxe nearly upright beside body, axehead at shoulder/head height and shaft toward boots, fully inside cell. Very strong broad silhouette, recognizable axe in every direction. No shield, no magic, no wounds or gore.
```

### knight

```text
A production transparent PNG game sprite atlas, exactly FOUR full-body standing views of ONE medieval fantasy character in a strict 2x2 grid. Genuinely transparent RGBA, background pixels alpha=0, no checkerboard or backdrop. TOP LEFT: whole body faces diagonally DOWN-LEFT, nose pointing LEFT, front of face visible. TOP RIGHT: whole body faces diagonally DOWN-RIGHT, nose pointing RIGHT, front of face visible. BOTTOM LEFT: rear three-quarter view facing diagonally UP-LEFT, boots pointing LEFT, face invisible. BOTTOM RIGHT: rear three-quarter view facing diagonally UP-RIGHT, boots pointing RIGHT, face invisible. These FOUR distinct whole-body directions are the main requirement, no duplicate poses with only heads turned. SAME character, outfit, equipment, body proportions, lighting and body scale in all four cells. Hand-painted medieval fantasy tactical RPG sprite style, restrained realistic steel/leather/cloth texture, soft even light, muted colors, dark fine contours, grounded adult proportions and clear readable silhouettes. Fixed orthographic camera slightly above looking down. Calm combat-ready standing idle, entire head, boots and weapons visible inside each cell, ample transparent gutters, feet at matching baseline near 92 percent of cell height. Canvas 1536x2048. No text, numbers, labels, ground shadows, smoke, aura, particles or scenery. Subject: Human KNIGHT ON FOOT, full polished steel plate armor, elegant closed greathelm with narrow visor slit, restrained brass edging, white surcoat with muted royal-blue panels, small blue shoulder half-cape, steel gauntlets and greaves. ONE plain long steel longsword held in both gauntleted hands, blade safely lowered diagonally toward ground, entire sword inside cell. Noble upright disciplined posture, substantial but orderly silhouette, a baseline armored knight without excessive ornaments. No mount, no shield, no spear, no wings. Rear views show plated back, half-cape and armored boots.
```
