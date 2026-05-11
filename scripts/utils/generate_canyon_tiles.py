#!/usr/bin/env python3

from __future__ import annotations

import random
import sys
from pathlib import Path
from typing import Callable, Iterable

try:
	from PIL import Image, ImageChops, ImageDraw
except ImportError as exc:
	raise SystemExit("Pillow is required to generate canyon tiles. Install it with: python -m pip install pillow") from exc


WIDTH = 64
HEIGHT = 32
FACE_HEIGHT = 36
SCALE = 3
OUTPUT_DIR = Path.cwd() / "assets" / "main" / "battle" / "terrain" / "canyon"
RESAMPLE_LANCZOS = getattr(Image, "Resampling", Image).LANCZOS

Point = tuple[float, float]
Color = tuple[int, int, int, int]


def color(a: int, r: int, g: int, b: int) -> Color:
	return (r, g, b, a)


def point(x: float, y: float) -> Point:
	return (x, y)


def points(values: Iterable[Point]) -> list[Point]:
	return list(values)


def new_random(seed: int) -> random.Random:
	return random.Random(seed)


def _scale_value(value: float) -> int:
	return int(round(value * SCALE))


def _scale_point(value: Point) -> tuple[int, int]:
	return (_scale_value(value[0]), _scale_value(value[1]))


def _scale_points(values: Iterable[Point]) -> list[tuple[int, int]]:
	return [_scale_point(value) for value in values]


def _scale_box(x: float, y: float, width: float, height: float) -> tuple[int, int, int, int]:
	return (_scale_value(x), _scale_value(y), _scale_value(x + width), _scale_value(y + height))


class Canvas:
	def __init__(self, width: int = WIDTH, height: int = HEIGHT) -> None:
		self.width = width
		self.height = height
		self.size = (width * SCALE, height * SCALE)
		self.image = Image.new("RGBA", self.size, color(0, 0, 0, 0))
		self._draw = ImageDraw.Draw(self.image, "RGBA")
		self._clip_stack: list[Image.Image | None] = []

	@property
	def _clip(self) -> Image.Image | None:
		if not self._clip_stack:
			return None
		return self._clip_stack[-1]

	def _with_layer(self, draw_body: Callable[[ImageDraw.ImageDraw], None]) -> None:
		if self._clip is None:
			draw_body(self._draw)
			return

		layer = Image.new("RGBA", self.size, color(0, 0, 0, 0))
		draw_body(ImageDraw.Draw(layer, "RGBA"))
		alpha = ImageChops.multiply(layer.getchannel("A"), self._clip)
		layer.putalpha(alpha)
		self.image.alpha_composite(layer)

	def clip(self, mask_points: list[Point], body: Callable[[], None]) -> None:
		mask = Image.new("L", self.size, 0)
		ImageDraw.Draw(mask).polygon(_scale_points(mask_points), fill=255)
		if self._clip is not None:
			mask = ImageChops.multiply(mask, self._clip)
		self._clip_stack.append(mask)
		try:
			body()
		finally:
			self._clip_stack.pop()

	def fill_polygon(self, polygon: list[Point], fill: Color) -> None:
		self._with_layer(lambda draw: draw.polygon(_scale_points(polygon), fill=fill))

	def draw_outline(self, polygon: list[Point], fill: Color, width: float) -> None:
		scaled = _scale_points(polygon)
		self._with_layer(lambda draw: draw.line(scaled + [scaled[0]], fill=fill, width=max(1, _scale_value(width)), joint="curve"))

	def fill_ellipse(self, fill: Color, x: float, y: float, width: float, height: float) -> None:
		self._with_layer(lambda draw: draw.ellipse(_scale_box(x, y, width, height), fill=fill))

	def fill_pie(self, fill: Color, x: float, y: float, width: float, height: float, start: float, sweep: float) -> None:
		self._with_layer(lambda draw: draw.pieslice(_scale_box(x, y, width, height), start=start, end=start + sweep, fill=fill))

	def draw_line(self, fill: Color, width: float, x1: float, y1: float, x2: float, y2: float) -> None:
		self._with_layer(lambda draw: draw.line([_scale_point((x1, y1)), _scale_point((x2, y2))], fill=fill, width=max(1, _scale_value(width))))

	def draw_arc(self, fill: Color, width: float, x: float, y: float, arc_width: float, arc_height: float, start: float, sweep: float) -> None:
		self._with_layer(lambda draw: draw.arc(_scale_box(x, y, arc_width, arc_height), start=start, end=start + sweep, fill=fill, width=max(1, _scale_value(width))))

	def fill_gradient(self, polygon: list[Point], top: Color, bottom: Color) -> None:
		scaled_polygon = _scale_points(polygon)
		x_values = [item[0] for item in scaled_polygon]
		y_values = [item[1] for item in scaled_polygon]
		top_y = min(y_values)
		bottom_y = max(y_values)
		height = max(1, bottom_y - top_y)
		gradient = Image.new("RGBA", self.size, color(0, 0, 0, 0))
		draw = ImageDraw.Draw(gradient, "RGBA")
		for y in range(self.size[1]):
			t = min(1.0, max(0.0, (y - top_y) / height))
			line_color = tuple(int(round(top[index] + (bottom[index] - top[index]) * t)) for index in range(4))
			draw.line([(min(x_values), y), (max(x_values), y)], fill=line_color)

		mask = Image.new("L", self.size, 0)
		ImageDraw.Draw(mask).polygon(scaled_polygon, fill=255)
		if self._clip is not None:
			mask = ImageChops.multiply(mask, self._clip)
		gradient.putalpha(ImageChops.multiply(gradient.getchannel("A"), mask))
		self.image.alpha_composite(gradient)

	def save(self, file_name: str) -> None:
		OUTPUT_DIR.mkdir(parents=True, exist_ok=True)
		output = self.image.resize((self.width, self.height), RESAMPLE_LANCZOS)
		output.save(OUTPUT_DIR / file_name, "PNG")


def get_diamond() -> list[Point]:
	return points(
		(
			point(32, 0),
			point(63, 16),
			point(32, 31),
			point(0, 16),
		)
	)


def add_grain(canvas: Canvas, mask: list[Point], rng: random.Random, count: int, dark: Color, light: Color) -> None:
	def body() -> None:
		for _ in range(count):
			x = rng.randrange(8, 55)
			y = rng.randrange(5, 27)
			w = rng.randrange(2, 5)
			h = rng.randrange(1, 4)
			canvas.fill_ellipse(dark, x, y, w, h)
			if rng.random() < 0.5:
				canvas.fill_ellipse(light, x + 0.8, y + 0.3, max(1, w - 2), max(1, h - 1))

	canvas.clip(mask, body)


def add_cracks(canvas: Canvas, mask: list[Point], rng: random.Random, count: int, fill: Color) -> None:
	def body() -> None:
		for _ in range(count):
			x1 = rng.randrange(12, 48)
			y1 = rng.randrange(7, 22)
			x2 = x1 + rng.randrange(-8, 9)
			y2 = y1 + rng.randrange(4, 9)
			canvas.draw_line(fill, 1.0, x1, y1, x2, y2)
			if rng.random() < 0.55:
				canvas.draw_line(fill, 1.0, x2, y2, x2 + rng.randrange(-5, 6), y2 + rng.randrange(2, 6))

	canvas.clip(mask, body)


def add_strata_bands(canvas: Canvas, mask: list[Point], rng: random.Random, count: int, light: Color, shadow: Color, x_min: int, x_max: int) -> None:
	def body() -> None:
		for index in range(count):
			y = 8 + (index * 5) + rng.randrange(-1, 2)
			canvas.draw_line(light, 1.2, x_min, y, x_max, y + rng.randrange(-1, 2))
			canvas.draw_line(shadow, 1.0, x_min, y + 1, x_max, y + 2 + rng.randrange(-1, 2))

	canvas.clip(mask, body)


def draw_land_top(file_name: str, variant: int) -> None:
	canvas = Canvas()
	diamond = get_diamond()
	top_palette = (
		(color(255, 214, 180, 126), color(255, 154, 112, 68), color(208, 104, 72, 42)),
		(color(255, 202, 162, 108), color(255, 143, 97, 56), color(214, 90, 60, 35)),
		(color(255, 222, 190, 136), color(255, 160, 118, 74), color(212, 106, 74, 42)),
	)
	palette = top_palette[variant - 1]
	canvas.fill_gradient(diamond, palette[0], palette[1])
	canvas.fill_polygon(points((point(32, 2), point(56, 15), point(32, 10), point(8, 15))), color(46, 255, 245, 224))
	canvas.fill_polygon(points((point(32, 19), point(58, 16), point(32, 31), point(6, 16))), color(38, 92, 58, 32))
	rng = new_random(7100 + variant * 37)
	add_grain(canvas, diamond, rng, 26, color(120, 116, 83, 51), color(96, 236, 206, 166))
	add_cracks(canvas, diamond, rng, 7, color(104, 96, 61, 36))

	def ridges() -> None:
		for index in range(3):
			ridge_x = 10 + index * 15 + rng.randrange(-2, 3)
			canvas.draw_line(color(55, 255, 228, 184), 1.0, ridge_x, 18, ridge_x + 9, 12)

	canvas.clip(diamond, ridges)
	canvas.draw_outline(diamond, color(210, 112, 74, 43), 1.1)
	canvas.save(file_name)


def draw_water_top(file_name: str, variant: int) -> None:
	canvas = Canvas()
	diamond = get_diamond()
	palette = (
		(color(255, 110, 149, 171), color(255, 48, 88, 109), color(170, 215, 238, 247)),
		(color(255, 96, 135, 157), color(255, 38, 72, 91), color(168, 208, 232, 242)),
		(color(255, 120, 160, 171), color(255, 56, 92, 102), color(160, 226, 242, 247)),
	)[variant - 1]
	canvas.fill_gradient(diamond, palette[0], palette[1])
	canvas.fill_polygon(points((point(32, 3), point(57, 16), point(32, 9), point(7, 16))), color(42, 242, 250, 255))

	def water_detail() -> None:
		rng = new_random(8200 + variant * 41)
		for _ in range(7):
			x = 10 + rng.randrange(0, 36)
			y = 7 + rng.randrange(0, 14)
			w = 10 + rng.randrange(0, 10)
			h = 4 + rng.randrange(0, 3)
			canvas.draw_arc(palette[2], 1.0, x, y, w, h, 0, 180)
			if rng.random() < 0.4:
				canvas.fill_ellipse(color(48, 255, 255, 255), x + 2, y + 1, 4, 1)
		canvas.fill_ellipse(color(70, 255, 255, 255), 20, 7, 16, 4)
		canvas.fill_ellipse(color(38, 20, 34, 44), 22, 18, 21, 7)

	canvas.clip(diamond, water_detail)
	canvas.draw_outline(diamond, color(190, 36, 60, 77), 1.0)
	canvas.save(file_name)


def draw_mud_top(file_name: str, variant: int) -> None:
	canvas = Canvas()
	diamond = get_diamond()
	palette = (
		(color(255, 157, 109, 72), color(255, 76, 47, 28), color(110, 240, 212, 181)),
		(color(255, 145, 97, 61), color(255, 67, 40, 24), color(105, 232, 200, 170)),
		(color(255, 134, 86, 52), color(255, 57, 34, 20), color(108, 226, 192, 160)),
	)[variant - 1]
	canvas.fill_gradient(diamond, palette[0], palette[1])
	canvas.fill_polygon(points((point(32, 3), point(54, 16), point(32, 11), point(10, 16))), color(36, 255, 228, 198))

	def mud_detail() -> None:
		rng = new_random(9300 + variant * 43)
		for _ in range(5):
			x = 12 + rng.randrange(0, 30)
			y = 9 + rng.randrange(0, 11)
			w = 9 + rng.randrange(0, 9)
			h = 4 + rng.randrange(0, 4)
			canvas.fill_ellipse(color(120, 66, 39, 22), x, y, w, h)
			canvas.fill_ellipse(palette[2], x + 1, y + 1, max(3, w - 3), max(2, h - 3))
		for _ in range(4):
			x = 14 + rng.randrange(0, 30)
			y = 8 + rng.randrange(0, 12)
			canvas.draw_line(color(96, 100, 60, 34), 1.0, x, y, x + rng.randrange(4, 9), y + rng.randrange(2, 5))

	canvas.clip(diamond, mud_detail)
	canvas.draw_outline(diamond, color(208, 72, 41, 24), 1.1)
	canvas.save(file_name)


def draw_scrub_overlay(file_name: str, variant: int) -> None:
	canvas = Canvas()
	diamond = get_diamond()
	clusters = (
		((18, 18), (30, 10), (43, 17)),
		((14, 15), (31, 11), (46, 18)),
		((20, 11), (34, 18), (45, 12)),
	)[variant - 1]

	def scrub_detail() -> None:
		for x, y in clusters:
			for blade in range(6):
				offset_x = blade - 2
				canvas.draw_line(color(138, 84, 112, 55), 1.1, x + offset_x, y + 3, x + offset_x + ((blade % 2) * 2) - 1, y - 3 - (blade % 3))
			canvas.fill_ellipse(color(170, 88, 118, 60), x - 5, y - 1, 10, 6)
			canvas.fill_ellipse(color(182, 112, 144, 74), x - 2, y - 5, 8, 6)
			canvas.fill_ellipse(color(145, 68, 94, 44), x + 1, y - 1, 7, 5)

	canvas.clip(diamond, scrub_detail)
	canvas.save(file_name)


def draw_rubble_overlay(file_name: str, variant: int) -> None:
	canvas = Canvas()
	diamond = get_diamond()
	rng = new_random(10400 + variant * 47)

	def rubble_detail() -> None:
		for _ in range(6):
			x = 12 + rng.randrange(0, 38)
			y = 9 + rng.randrange(0, 14)
			polygon = points(
				(
					point(x, y),
					point(x + 3 + rng.randrange(0, 2), y - 3 - rng.randrange(0, 2)),
					point(x + 7 + rng.randrange(0, 2), y + rng.randrange(-1, 2)),
					point(x + 2 + rng.randrange(0, 2), y + 2 + rng.randrange(0, 2)),
				)
			)
			canvas.fill_polygon(polygon, color(182, 126, 99, 75))
			canvas.draw_outline(polygon, color(120, 76, 57, 42), 1.0)
			canvas.fill_ellipse(color(92, 235, 211, 176), x + 2, y - 1, 3, 1)
			canvas.fill_ellipse(color(88, 82, 60, 42), x - 1, y + 2, 6, 2)

	canvas.clip(diamond, rubble_detail)
	canvas.save(file_name)


def draw_cliff_south(file_name: str, variant: int) -> None:
	canvas = Canvas(WIDTH, FACE_HEIGHT)
	polygon = points((point(32, 0), point(63, 16), point(63, 35), point(32, 20)))
	palette = (
		(color(255, 170, 126, 84), color(255, 72, 47, 31)),
		(color(255, 161, 117, 76), color(255, 66, 43, 28)),
		(color(255, 179, 135, 91), color(255, 79, 53, 35)),
	)
	colors = palette[variant - 1]
	canvas.fill_gradient(polygon, colors[0], colors[1])

	def cliff_detail() -> None:
		rng = new_random(11500 + variant * 53)
		add_strata_bands(canvas, polygon, rng, 4, color(112, 220, 192, 150), color(82, 98, 66, 46), 34, 62)
		add_cracks(canvas, polygon, rng, 5, color(102, 67, 43, 26))
		add_grain(canvas, polygon, rng, 16, color(42, 66, 42, 26), color(40, 246, 226, 204))
		canvas.fill_polygon(points((point(44, 7), point(63, 18), point(63, 35), point(32, 20))), color(70, 34, 22, 18))
		for chip in range(3):
			base_x = 39 + chip * 6 + rng.randrange(-1, 2)
			base_y = 9 + chip * 5 + rng.randrange(-1, 2)
			chip_polygon = points((point(base_x, base_y), point(base_x + 3, base_y + 2), point(base_x, base_y + 5)))
			canvas.fill_polygon(chip_polygon, color(82, 246, 218, 178))

	canvas.clip(polygon, cliff_detail)
	canvas.draw_line(color(222, 228, 206, 164), 1.2, 33, 1, 62, 16)
	canvas.draw_line(color(180, 65, 40, 26), 1.0, 32, 1, 32, 20)
	canvas.draw_line(color(176, 82, 50, 31), 1.0, 63, 17, 63, 34)
	canvas.draw_line(color(198, 50, 31, 20), 1.1, 33, 20, 62, 34)
	canvas.save(file_name)


def draw_wall_east(file_name: str, variant: int) -> None:
	canvas = Canvas(WIDTH, FACE_HEIGHT)
	polygon = points((point(0, 16), point(32, 0), point(32, 20), point(0, 35)))
	palette = (
		(color(255, 165, 156, 148), color(255, 82, 76, 71)),
		(color(255, 156, 148, 140), color(255, 75, 69, 64)),
		(color(255, 174, 166, 158), color(255, 88, 82, 77)),
	)
	colors = palette[variant - 1]
	canvas.fill_gradient(polygon, colors[0], colors[1])

	def wall_detail() -> None:
		rng = new_random(13600 + variant * 41)
		for course in range(4):
			y = 31 - course * 5 + rng.randrange(-1, 2)
			canvas.draw_line(color(124, 82, 76, 70), 1.0, 1, y, 31, y - 15)
		for joint in range(3):
			x = 8 + joint * 7 + rng.randrange(-1, 2)
			canvas.draw_line(color(96, 68, 64, 60), 1.0, x, 15, x, 32)
		canvas.fill_polygon(points((point(0, 26), point(13, 20), point(17, 35), point(0, 35))), color(48, 54, 46, 40))

	canvas.clip(polygon, wall_detail)
	canvas.draw_line(color(222, 218, 210, 188), 1.2, 1, 16, 31, 1)
	canvas.draw_line(color(126, 86, 80, 74), 1.0, 1, 34, 31, 20)
	canvas.draw_line(color(110, 74, 70, 64), 1.0, 0, 17, 0, 34)
	canvas.draw_line(color(102, 70, 66, 60), 1.0, 32, 1, 32, 19)
	canvas.save(file_name)


def draw_wall_south(file_name: str, variant: int) -> None:
	canvas = Canvas(WIDTH, FACE_HEIGHT)
	polygon = points((point(32, 0), point(63, 16), point(63, 35), point(32, 20)))
	palette = (
		(color(255, 156, 148, 140), color(255, 74, 68, 64)),
		(color(255, 148, 140, 133), color(255, 68, 62, 58)),
		(color(255, 166, 158, 150), color(255, 82, 76, 72)),
	)
	colors = palette[variant - 1]
	canvas.fill_gradient(polygon, colors[0], colors[1])

	def wall_detail() -> None:
		rng = new_random(14200 + variant * 47)
		for course in range(4):
			y = 6 + course * 6 + rng.randrange(-1, 2)
			canvas.draw_line(color(118, 78, 74, 66), 1.0, 33, y, 62, y + 15)
		for joint in range(3):
			x = 40 + joint * 7 + rng.randrange(-1, 2)
			canvas.draw_line(color(92, 66, 62, 56), 1.0, x, 8, x, 32)
		canvas.fill_polygon(points((point(45, 8), point(63, 17), point(63, 35), point(32, 20))), color(46, 44, 40, 34))

	canvas.clip(polygon, wall_detail)
	canvas.draw_line(color(214, 210, 202, 182), 1.2, 33, 1, 62, 16)
	canvas.draw_line(color(122, 84, 78, 70), 1.0, 33, 20, 62, 34)
	canvas.draw_line(color(106, 72, 68, 62), 1.0, 32, 1, 32, 19)
	canvas.draw_line(color(100, 68, 64, 58), 1.0, 63, 17, 63, 34)
	canvas.save(file_name)


def draw_cliff_east(file_name: str, variant: int) -> None:
	canvas = Canvas(WIDTH, FACE_HEIGHT)
	polygon = points((point(0, 16), point(32, 0), point(32, 20), point(0, 35)))
	palette = (
		(color(255, 179, 132, 87), color(255, 79, 51, 33)),
		(color(255, 169, 121, 78), color(255, 72, 46, 29)),
		(color(255, 187, 139, 93), color(255, 87, 58, 37)),
	)
	colors = palette[variant - 1]
	canvas.fill_gradient(polygon, colors[0], colors[1])

	def cliff_detail() -> None:
		rng = new_random(12600 + variant * 59)
		add_strata_bands(canvas, polygon, rng, 4, color(118, 232, 200, 154), color(86, 105, 70, 48), 2, 30)
		add_cracks(canvas, polygon, rng, 4, color(102, 70, 46, 28))
		add_grain(canvas, polygon, rng, 16, color(38, 70, 45, 28), color(42, 248, 230, 208))
		canvas.fill_polygon(points((point(0, 23), point(19, 14), point(32, 20), point(0, 35))), color(56, 52, 33, 19))
		for chip in range(3):
			base_x = 5 + chip * 7 + rng.randrange(-1, 2)
			base_y = 27 - chip * 5 + rng.randrange(-1, 2)
			chip_polygon = points((point(base_x, base_y), point(base_x + 4, base_y - 2), point(base_x + 5, base_y + 1)))
			canvas.fill_polygon(chip_polygon, color(78, 248, 220, 184))

	canvas.clip(polygon, cliff_detail)
	canvas.draw_line(color(230, 244, 218, 176), 1.2, 1, 16, 31, 1)
	canvas.draw_line(color(188, 71, 43, 27), 1.0, 0, 17, 0, 34)
	canvas.draw_line(color(182, 88, 53, 33), 1.0, 32, 1, 32, 19)
	canvas.draw_line(color(208, 56, 34, 22), 1.1, 1, 34, 31, 20)
	canvas.save(file_name)


def draw_marker_selected(file_name: str) -> None:
	canvas = Canvas()
	outer = points((point(32, 2), point(61, 16), point(32, 29), point(3, 16)))
	mid = points((point(32, 5), point(54, 16), point(32, 26), point(10, 16)))
	canvas.draw_outline(outer, color(110, 255, 206, 116), 2.4)
	canvas.draw_outline(mid, color(235, 255, 246, 222), 1.6)
	canvas.fill_polygon(points((point(32, 12), point(37, 16), point(32, 20), point(27, 16))), color(160, 255, 233, 174))
	canvas.fill_ellipse(color(72, 255, 247, 214), 20, 11, 24, 10)
	canvas.save(file_name)


def draw_marker_preview(file_name: str) -> None:
	canvas = Canvas()
	outer = points((point(32, 3), point(58, 16), point(32, 28), point(6, 16)))
	mid = points((point(32, 6), point(52, 16), point(32, 25), point(12, 16)))
	canvas.draw_outline(outer, color(95, 255, 152, 76), 1.9)
	canvas.draw_outline(mid, color(190, 255, 210, 152), 1.2)
	canvas.fill_ellipse(color(56, 255, 184, 110), 22, 12, 20, 8)
	canvas.save(file_name)


def main() -> int:
	for variant in range(1, 4):
		draw_land_top(f"top_land_{variant:02d}.png", variant)
		draw_water_top(f"top_water_{variant:02d}.png", variant)
		draw_mud_top(f"top_mud_{variant:02d}.png", variant)
		draw_scrub_overlay(f"overlay_scrub_{variant:02d}.png", variant)
		draw_rubble_overlay(f"overlay_rubble_{variant:02d}.png", variant)
		draw_cliff_east(f"cliff_east_{variant:02d}.png", variant)
		draw_cliff_south(f"cliff_south_{variant:02d}.png", variant)
		draw_wall_east(f"wall_east_{variant:02d}.png", variant)
		draw_wall_south(f"wall_south_{variant:02d}.png", variant)

	draw_marker_selected("marker_selected.png")
	draw_marker_preview("marker_preview.png")
	print(f"Generated refined canyon tiles in {OUTPUT_DIR}")
	return 0


if __name__ == "__main__":
	raise SystemExit(main())
