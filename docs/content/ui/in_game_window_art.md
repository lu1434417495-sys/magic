# 游戏弹窗标题插画

生成日期：2026-09-12。使用内置 imagegen 工具生成，未使用本地模型或外部 API key。原图尺寸均为 1672 × 941。运行时只在弹窗标题右侧显示最多 320 × 96 逻辑像素，并通过 Shader 渐隐；不替换游戏场景背景。原图未经过额外位图编辑。

| 资源 | 用途 | 原始生成文件 |
| --- | --- | --- |
| `assets/ui/windows/settlement_vignette.png` | 据点、委托、进入提示等标题 | `C:/Users/lu/.codex/generated_images/01a09420-1288-7c20-a3bd-bdf824f0f197/exec-90f99578-6951-4c6a-9869-3605cb547907.png` |
| `assets/ui/windows/travel_kit_vignette.png` | 队伍、仓库、商店、战中背包标题 | `C:/Users/lu/.codex/generated_images/01a09420-1288-7c20-a3bd-bdf824f0f197/exec-a1b46b6f-a537-40ce-b7a9-030872d896e0.png` |

人物、触发术、成长与设置类标题复用已有 `assets/ui/character_creation/fate_observatory.png`。显示边界与 owner 见 [当前实现](../../design/ui/in_game_window_presentation.md)。

## 据点插画最终生成提示

```text
Use case: stylized-concept. Asset type: finished production illustration used as a dark fantasy CRPG in-game window background. Landscape 16:9, 2048x1152 if available. Painterly realism, exceptionally beautiful old-world dark fantasy, desaturated charcoal and slate shadows, restrained antiqued gold and warm candlelight, tactile materials, same sophisticated aesthetic as an antique astronomical library. Unified full-bleed painting. IMPORTANT composition: main subject in RIGHT third, center x=78%, with upper right detail; LEFT two thirds quiet very dark low-contrast shadows for live typography. This is actual game artwork, not a mockup. Absolutely NO text, readable runes, lettering, logos, watermark, interface elements, buttons, panels, frames or collage. No saturated neon, no cartoon. Subject: a medieval hilltop settlement at dusk, weathered stone towers and timber rooftops, a winding cobbled approach, warm tiny lanterns, distant slate mountains and drifting mist. Beautiful castle silhouette in right third; no large people. A storyteller's view across ancient ramparts, believable scale and depth, subtle golden sky behind right-hand towers while left stays deep blue-black shadow.
```

## 旅具插画最终生成提示

```text
Use case: stylized-concept. Asset type: finished production illustration used as a dark fantasy CRPG in-game window background. Landscape 16:9, 2048x1152 if available. Painterly realism, exceptionally beautiful old-world dark fantasy, desaturated charcoal and slate shadows, restrained antiqued gold and warm candlelight, tactile materials, same sophisticated aesthetic as an antique astronomical library. Unified full-bleed painting. IMPORTANT composition: main subject in RIGHT third, center x=78%, with upper right detail; LEFT two thirds quiet very dark low-contrast shadows for live typography. This is actual game artwork, not a mockup. Absolutely NO text, readable runes, lettering, logos, watermark, interface elements, buttons, panels, frames or collage. No saturated neon, no cartoon. Subject: an adventurer's quartermaster table in a dim stone chamber. At RIGHT third: an open weathered leather travel satchel, elegant worn steel sword hilt, coiled leather belt, folded map WITHOUT writing, a few old brass coins, glass potion flask and candle. Rich leather, engraved brass, hand-crafted fabrics, premium still life with convincing material details. LEFT two thirds dissolve into quiet charcoal room shadows. No excessive treasure or piles of objects.
```
