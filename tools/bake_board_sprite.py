#!/usr/bin/env python3
"""把一张 AI 立绘烘焙成战斗棋盘单位贴图。

把高分辨率的角色立绘(最好已是透明底)裁剪、缩放、对齐到棋盘规格画布:
脚底接地点对齐锚点,主体高度落在规格区间,可选很弱的接地阴影 + 锐化。

规格来源:docs/design/battle_unit_sprite_requirements.md
  1x1 普通单位:画布 96x128,锚点 (48,112),接地宽 20-28px,主体高 72-96px
  2x2 大体型  :画布 192x160,锚点 (96,136),接地宽 64-96px,主体高 96-132px

用法:
  python tools/bake_board_sprite.py 输入立绘.png 输出_board.png
  python tools/bake_board_sprite.py 输入.png 输出.png --size 2x2
  python tools/bake_board_sprite.py 输入.png 输出.png --bg-color 255,255,255 --bg-tol 24

若立绘是纯色底(非透明),用 --bg-color 指定背景色做阈值抠图;留空则默认取四角颜色。
"""
from __future__ import annotations

import argparse
import sys
from dataclasses import dataclass

from PIL import Image, ImageDraw, ImageFilter

# 每种棋盘尺寸的画布与构图规格。body_height 取规格区间中点偏上,
# ground_width 仅用于打印参考,不强制(立绘脚距由原图决定)。
@dataclass(frozen=True)
class BoardSpec:
    canvas: tuple[int, int]
    anchor: tuple[int, int]      # 脚底接地点在画布上的像素坐标
    body_height: int             # 缩放后主体目标可见高度(px)
    bottom_pad: int              # 锚点下方保留的透明/阴影空间(px)
    shadow_width: int            # 接地阴影椭圆宽
    shadow_height: int           # 接地阴影椭圆高

SPECS: dict[str, BoardSpec] = {
    "1x1": BoardSpec((96, 128), (48, 112), body_height=92, bottom_pad=14,
                     shadow_width=26, shadow_height=9),
    "2x2": BoardSpec((192, 160), (96, 136), body_height=126, bottom_pad=18,
                     shadow_width=80, shadow_height=20),
}


_FLOOD_SENTINEL = (255, 0, 255)  # 不太可能出现在角色里的洋红,用作 flood 标记


def _strip_solid_background(img: Image.Image, bg_color, tol: int) -> Image.Image:
    """从四周边缘 flood fill 抠掉与边界连通的纯/渐变背景。

    只抠和图像边缘连通的背景,不会在角色内部的浅色区域(浅甲、战裙、盔甲高光)
    打洞。tol 是 flood 的颜色容差(越大越激进)。bg_color 仅用于兜底的全局阈值,
    清掉 flood 漏掉的零散背景碎点。
    """
    img = img.convert("RGBA")
    rgb = img.convert("RGB")
    w, h = img.size

    # 沿四条边每隔几像素取一个种子点做 flood fill,覆盖渐变/暗角背景
    step = max(2, min(w, h) // 64)
    seeds = []
    for x in range(0, w, step):
        seeds.append((x, 0))
        seeds.append((x, h - 1))
    for y in range(0, h, step):
        seeds.append((0, y))
        seeds.append((w - 1, y))
    for seed in seeds:
        if rgb.getpixel(seed) == _FLOOD_SENTINEL:
            continue
        ImageDraw.floodfill(rgb, seed, _FLOOD_SENTINEL, thresh=tol)

    src = img.load()
    flooded = rgb.load()
    # 兜底全局阈值:flood 没连到的零散背景碎点(用四角均色判定)
    px_corners = [img.getpixel(p)[:3] for p in [(0, 0), (w - 1, 0), (0, h - 1), (w - 1, h - 1)]]
    gb = bg_color[:3] if bg_color else tuple(sum(c[i] for c in px_corners) // 4 for i in range(3))
    near_tol = 12
    for y in range(h):
        for x in range(w):
            r, g, b, a = src[x, y]
            if flooded[x, y] == _FLOOD_SENTINEL:
                src[x, y] = (r, g, b, 0)
            elif (abs(r - gb[0]) <= near_tol and abs(g - gb[1]) <= near_tol
                  and abs(b - gb[2]) <= near_tol):
                src[x, y] = (r, g, b, 0)
    return img


def _alpha_bbox(img: Image.Image):
    bbox = img.getchannel("A").getbbox()
    if bbox is None:
        raise SystemExit("错误:抠图后整张图全透明,检查 --bg-color/--bg-tol 或输入图。")
    return bbox


def _add_contact_shadow(canvas: Image.Image, spec: BoardSpec) -> None:
    ax, ay = spec.anchor
    shadow = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    from PIL import ImageDraw
    d = ImageDraw.Draw(shadow)
    half_w, half_h = spec.shadow_width // 2, spec.shadow_height // 2
    d.ellipse(
        [ax - half_w, ay - half_h, ax + half_w, ay + half_h],
        fill=(0, 0, 0, 70),
    )
    shadow = shadow.filter(ImageFilter.GaussianBlur(2.0))
    canvas.alpha_composite(shadow)


def bake(src_path: str, out_path: str, spec: BoardSpec, *,
         bg_color, bg_tol: int, remove_bg: bool, shadow: bool, sharpen: bool,
         body_height: int | None = None) -> None:
    img = Image.open(src_path).convert("RGBA")
    target_body_height = body_height or spec.body_height

    if remove_bg:
        img = _strip_solid_background(img, bg_color, bg_tol)

    # 1) 裁到不透明主体的外接框
    img = img.crop(_alpha_bbox(img))

    # 2) 按目标主体高度等比缩放
    src_w, src_h = img.size
    scale = target_body_height / src_h
    new_w = max(1, round(src_w * scale))
    new_h = max(1, round(src_h * scale))
    img = img.resize((new_w, new_h), Image.LANCZOS)

    # 3) 贴到画布:水平中心对齐锚点 x,主体底边落在锚点 y
    canvas = Image.new("RGBA", spec.canvas, (0, 0, 0, 0))
    if shadow:
        _add_contact_shadow(canvas, spec)
    ax, ay = spec.anchor
    paste_x = round(ax - new_w / 2)
    paste_y = ay - new_h
    canvas.alpha_composite(img, (paste_x, paste_y))

    # 4) 轻锐化,补偿缩放带来的发糊(只锐化 RGB,不动 alpha)
    if sharpen:
        rgb = canvas.convert("RGB").filter(
            ImageFilter.UnsharpMask(radius=1.4, percent=110, threshold=2)
        )
        canvas = Image.merge("RGBA", (*rgb.split(), canvas.getchannel("A")))

    canvas.save(out_path)

    cw, ch = spec.canvas
    over_top = max(0, -paste_y)
    over_bottom = max(0, (paste_y + new_h) - ch)
    print(f"[bake] {src_path} -> {out_path}")
    print(f"  画布 {cw}x{ch} 锚点 {spec.anchor}")
    print(f"  主体缩放后 {new_w}x{new_h}px(目标主体高 {target_body_height}px)")
    if over_top or over_bottom:
        print(f"  警告:主体超出画布上/下 {over_top}/{over_bottom}px,考虑调小 body_height 或裁源图")
    bottom_gap = ch - ay
    print(f"  锚点下方留白 {bottom_gap}px(规格建议 >= {spec.bottom_pad}px)")


def main(argv=None) -> int:
    p = argparse.ArgumentParser(description="AI 立绘 -> 战斗棋盘单位贴图")
    p.add_argument("src", help="输入立绘 PNG")
    p.add_argument("out", help="输出棋盘贴图 PNG")
    p.add_argument("--size", choices=sorted(SPECS), default="1x1", help="棋盘占格(默认 1x1)")
    p.add_argument("--no-remove-bg", action="store_true",
                   help="输入已是透明底,跳过抠图")
    p.add_argument("--bg-color", default=None,
                   help="纯色背景的 RGB,如 255,255,255;留空取四角色")
    p.add_argument("--bg-tol", type=int, default=24, help="背景色阈值容差(默认 24)")
    p.add_argument("--body-height", type=int, default=None,
                   help="覆盖主体目标高度(px),用于把四足/大型单位调小,如狼 --body-height 64")
    p.add_argument("--no-shadow", action="store_true", help="不加接地阴影")
    p.add_argument("--no-sharpen", action="store_true", help="不锐化")
    args = p.parse_args(argv)

    bg_color = None
    if args.bg_color:
        try:
            bg_color = tuple(int(v) for v in args.bg_color.split(","))
        except ValueError:
            print("错误:--bg-color 需形如 255,255,255", file=sys.stderr)
            return 2

    bake(
        args.src, args.out, SPECS[args.size],
        bg_color=bg_color, bg_tol=args.bg_tol,
        remove_bg=not args.no_remove_bg,
        shadow=not args.no_shadow,
        sharpen=not args.no_sharpen,
        body_height=args.body_height,
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
