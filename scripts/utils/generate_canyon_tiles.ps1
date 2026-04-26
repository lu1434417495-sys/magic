Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing

$Width = 64
$Height = 32
$FaceHeight = 36
$OutputDir = Join-Path (Resolve-Path ".").Path "assets\main\battle\terrain\canyon"

function New-Color([int]$a, [int]$r, [int]$g, [int]$b) {
	return [System.Drawing.Color]::FromArgb($a, $r, $g, $b)
}

function New-Point([int]$x, [int]$y) {
	return [System.Drawing.Point]::new($x, $y)
}

function New-Points([object[]]$points) {
	return [System.Drawing.Point[]]$points
}

function New-Random([int]$seed) {
	return [System.Random]::new($seed)
}

function New-Canvas([int]$canvasWidth = $Width, [int]$canvasHeight = $Height) {
	$bitmap = [System.Drawing.Bitmap]::new($canvasWidth, $canvasHeight, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
	$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
	$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
	$graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
	$graphics.Clear([System.Drawing.Color]::FromArgb(0, 0, 0, 0))
	return @($bitmap, $graphics)
}

function Save-Canvas($bitmap, $graphics, [string]$fileName) {
	$path = Join-Path $OutputDir $fileName
	$bitmap.Save($path, [System.Drawing.Imaging.ImageFormat]::Png)
	$graphics.Dispose()
	$bitmap.Dispose()
}

function New-PolygonPath([System.Drawing.Point[]]$points) {
	$path = [System.Drawing.Drawing2D.GraphicsPath]::new()
	$path.AddPolygon($points)
	return $path
}

function Use-Clip([System.Drawing.Graphics]$graphics, [System.Drawing.Point[]]$points, [scriptblock]$body) {
	$path = New-PolygonPath $points
	$state = $graphics.Save()
	$graphics.SetClip($path)
	& $body
	$graphics.Restore($state)
	$path.Dispose()
}

function Fill-Polygon([System.Drawing.Graphics]$graphics, [System.Drawing.Point[]]$points, [System.Drawing.Color]$color) {
	$brush = [System.Drawing.SolidBrush]::new($color)
	$graphics.FillPolygon($brush, $points)
	$brush.Dispose()
}

function Fill-Gradient([System.Drawing.Graphics]$graphics, [System.Drawing.Point[]]$points, [System.Drawing.Color]$top, [System.Drawing.Color]$bottom) {
	$path = New-PolygonPath $points
	$bounds = $path.GetBounds()
	$brush = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
		[System.Drawing.PointF]::new($bounds.Left, $bounds.Top),
		[System.Drawing.PointF]::new($bounds.Left, $bounds.Bottom),
		$top,
		$bottom
	)
	$graphics.FillPath($brush, $path)
	$brush.Dispose()
	$path.Dispose()
}

function Draw-Outline([System.Drawing.Graphics]$graphics, [System.Drawing.Point[]]$points, [System.Drawing.Color]$color, [single]$width) {
	$pen = [System.Drawing.Pen]::new($color, $width)
	$graphics.DrawPolygon($pen, $points)
	$pen.Dispose()
}

function Fill-Ellipse([System.Drawing.Graphics]$graphics, [System.Drawing.Color]$color, [single]$x, [single]$y, [single]$width, [single]$height) {
	$brush = [System.Drawing.SolidBrush]::new($color)
	$graphics.FillEllipse($brush, $x, $y, $width, $height)
	$brush.Dispose()
}

function Fill-Pie([System.Drawing.Graphics]$graphics, [System.Drawing.Color]$color, [single]$x, [single]$y, [single]$width, [single]$height, [single]$start, [single]$sweep) {
	$brush = [System.Drawing.SolidBrush]::new($color)
	$graphics.FillPie($brush, $x, $y, $width, $height, $start, $sweep)
	$brush.Dispose()
}

function Draw-Line([System.Drawing.Graphics]$graphics, [System.Drawing.Color]$color, [single]$width, [int]$x1, [int]$y1, [int]$x2, [int]$y2) {
	$pen = [System.Drawing.Pen]::new($color, $width)
	$graphics.DrawLine($pen, $x1, $y1, $x2, $y2)
	$pen.Dispose()
}

function Draw-Arc([System.Drawing.Graphics]$graphics, [System.Drawing.Color]$color, [single]$width, [int]$x, [int]$y, [int]$arcWidth, [int]$arcHeight, [single]$start, [single]$sweep) {
	$pen = [System.Drawing.Pen]::new($color, $width)
	$graphics.DrawArc($pen, $x, $y, $arcWidth, $arcHeight, $start, $sweep)
	$pen.Dispose()
}

function Add-Grain([System.Drawing.Graphics]$graphics, [System.Drawing.Point[]]$mask, [System.Random]$random, [int]$count, [System.Drawing.Color]$dark, [System.Drawing.Color]$light) {
	Use-Clip $graphics $mask {
		for ($i = 0; $i -lt $count; $i++) {
			$x = $random.Next(8, 55)
			$y = $random.Next(5, 27)
			$w = $random.Next(2, 5)
			$h = $random.Next(1, 4)
			Fill-Ellipse $graphics $dark $x $y $w $h
			if ($random.NextDouble() -lt 0.5) {
				Fill-Ellipse $graphics $light ($x + 0.8) ($y + 0.3) ([Math]::Max(1, $w - 2)) ([Math]::Max(1, $h - 1))
			}
		}
	}
}

function Add-Cracks([System.Drawing.Graphics]$graphics, [System.Drawing.Point[]]$mask, [System.Random]$random, [int]$count, [System.Drawing.Color]$color) {
	Use-Clip $graphics $mask {
		for ($i = 0; $i -lt $count; $i++) {
			$x1 = $random.Next(12, 48)
			$y1 = $random.Next(7, 22)
			$x2 = $x1 + $random.Next(-8, 9)
			$y2 = $y1 + $random.Next(4, 9)
			Draw-Line $graphics $color 1.0 $x1 $y1 $x2 $y2
			if ($random.NextDouble() -lt 0.55) {
				Draw-Line $graphics $color 1.0 $x2 $y2 ($x2 + $random.Next(-5, 6)) ($y2 + $random.Next(2, 6))
			}
		}
	}
}

function Add-StrataBands([System.Drawing.Graphics]$graphics, [System.Drawing.Point[]]$mask, [System.Random]$random, [int]$count, [System.Drawing.Color]$light, [System.Drawing.Color]$shadow, [int]$xMin, [int]$xMax) {
	Use-Clip $graphics $mask {
		for ($i = 0; $i -lt $count; $i++) {
			$y = 8 + ($i * 5) + $random.Next(-1, 2)
			Draw-Line $graphics $light 1.2 $xMin $y $xMax ($y + $random.Next(-1, 2))
			Draw-Line $graphics $shadow 1.0 $xMin ($y + 1) $xMax ($y + 2 + $random.Next(-1, 2))
		}
	}
}

function Get-Diamond() {
	return New-Points @(
		(New-Point 32 0),
		(New-Point 63 16),
		(New-Point 32 31),
		(New-Point 0 16)
	)
}

function Draw-LandTop([string]$fileName, [int]$variant) {
	$canvas = New-Canvas
	$bitmap = $canvas[0]
	$graphics = $canvas[1]
	$diamond = Get-Diamond
	$topPalette = @(
		@( (New-Color 255 214 180 126), (New-Color 255 154 112 68), (New-Color 208 104 72 42) ),
		@( (New-Color 255 202 162 108), (New-Color 255 143 97 56), (New-Color 214 90 60 35) ),
		@( (New-Color 255 222 190 136), (New-Color 255 160 118 74), (New-Color 212 106 74 42) )
	)
	$palette = $topPalette[$variant - 1]
	Fill-Gradient $graphics $diamond $palette[0] $palette[1]
	Fill-Polygon $graphics (New-Points @(
		(New-Point 32 2),
		(New-Point 56 15),
		(New-Point 32 10),
		(New-Point 8 15)
	)) (New-Color 46 255 245 224)
	Fill-Polygon $graphics (New-Points @(
		(New-Point 32 19),
		(New-Point 58 16),
		(New-Point 32 31),
		(New-Point 6 16)
	)) (New-Color 38 92 58 32)
	$rand = New-Random (7100 + $variant * 37)
	Add-Grain $graphics $diamond $rand 26 (New-Color 120 116 83 51) (New-Color 96 236 206 166)
	Add-Cracks $graphics $diamond $rand 7 (New-Color 104 96 61 36)
	Use-Clip $graphics $diamond {
		for ($i = 0; $i -lt 3; $i++) {
			$ridgeX = 10 + $i * 15 + $rand.Next(-2, 3)
			Draw-Line $graphics (New-Color 55 255 228 184) 1.0 $ridgeX 18 ($ridgeX + 9) 12
		}
	}
	Draw-Outline $graphics $diamond (New-Color 210 112 74 43) 1.1
	Save-Canvas $bitmap $graphics $fileName
}

function Draw-WaterTop([string]$fileName, [int]$variant) {
	$canvas = New-Canvas
	$bitmap = $canvas[0]
	$graphics = $canvas[1]
	$diamond = Get-Diamond
	$palette = @(
		@( (New-Color 255 110 149 171), (New-Color 255 48 88 109), (New-Color 170 215 238 247) ),
		@( (New-Color 255 96 135 157), (New-Color 255 38 72 91), (New-Color 168 208 232 242) ),
		@( (New-Color 255 120 160 171), (New-Color 255 56 92 102), (New-Color 160 226 242 247) )
	)[$variant - 1]
	Fill-Gradient $graphics $diamond $palette[0] $palette[1]
	Fill-Polygon $graphics (New-Points @(
		(New-Point 32 3),
		(New-Point 57 16),
		(New-Point 32 9),
		(New-Point 7 16)
	)) (New-Color 42 242 250 255)
	Use-Clip $graphics $diamond {
		$rand = New-Random (8200 + $variant * 41)
		for ($i = 0; $i -lt 7; $i++) {
			$x = 10 + $rand.Next(0, 36)
			$y = 7 + $rand.Next(0, 14)
			$w = 10 + $rand.Next(0, 10)
			$h = 4 + $rand.Next(0, 3)
			Draw-Arc $graphics $palette[2] 1.0 $x $y $w $h 0 180
			if ($rand.NextDouble() -lt 0.4) {
				Fill-Ellipse $graphics (New-Color 48 255 255 255) ($x + 2) ($y + 1) 4 1
			}
		}
		Fill-Ellipse $graphics (New-Color 70 255 255 255) 20 7 16 4
		Fill-Ellipse $graphics (New-Color 38 20 34 44) 22 18 21 7
	}
	Draw-Outline $graphics $diamond (New-Color 190 36 60 77) 1.0
	Save-Canvas $bitmap $graphics $fileName
}

function Draw-MudTop([string]$fileName, [int]$variant) {
	$canvas = New-Canvas
	$bitmap = $canvas[0]
	$graphics = $canvas[1]
	$diamond = Get-Diamond
	$palette = @(
		@( (New-Color 255 157 109 72), (New-Color 255 76 47 28), (New-Color 110 240 212 181) ),
		@( (New-Color 255 145 97 61), (New-Color 255 67 40 24), (New-Color 105 232 200 170) ),
		@( (New-Color 255 134 86 52), (New-Color 255 57 34 20), (New-Color 108 226 192 160) )
	)[$variant - 1]
	Fill-Gradient $graphics $diamond $palette[0] $palette[1]
	Fill-Polygon $graphics (New-Points @(
		(New-Point 32 3),
		(New-Point 54 16),
		(New-Point 32 11),
		(New-Point 10 16)
	)) (New-Color 36 255 228 198)
	Use-Clip $graphics $diamond {
		$rand = New-Random (9300 + $variant * 43)
		for ($i = 0; $i -lt 5; $i++) {
			$x = 12 + $rand.Next(0, 30)
			$y = 9 + $rand.Next(0, 11)
			$w = 9 + $rand.Next(0, 9)
			$h = 4 + $rand.Next(0, 4)
			Fill-Ellipse $graphics (New-Color 120 66 39 22) $x $y $w $h
			Fill-Ellipse $graphics $palette[2] ($x + 1) ($y + 1) ([Math]::Max(3, $w - 3)) ([Math]::Max(2, $h - 3))
		}
		for ($i = 0; $i -lt 4; $i++) {
			$x = 14 + $rand.Next(0, 30)
			$y = 8 + $rand.Next(0, 12)
			Draw-Line $graphics (New-Color 96 100 60 34) 1.0 $x $y ($x + $rand.Next(4, 9)) ($y + $rand.Next(2, 5))
		}
	}
	Draw-Outline $graphics $diamond (New-Color 208 72 41 24) 1.1
	Save-Canvas $bitmap $graphics $fileName
}

function Draw-ScrubOverlay([string]$fileName, [int]$variant) {
	$canvas = New-Canvas
	$bitmap = $canvas[0]
	$graphics = $canvas[1]
	$diamond = Get-Diamond
	$clusters = @(
		@(@(18, 18), @(30, 10), @(43, 17)),
		@(@(14, 15), @(31, 11), @(46, 18)),
		@(@(20, 11), @(34, 18), @(45, 12))
	)[$variant - 1]
	Use-Clip $graphics $diamond {
		foreach ($cluster in $clusters) {
			$x = $cluster[0]
			$y = $cluster[1]
			for ($blade = 0; $blade -lt 6; $blade++) {
				$offsetX = $blade - 2
				Draw-Line $graphics (New-Color 138 84 112 55) 1.1 ($x + $offsetX) ($y + 3) ($x + $offsetX + (($blade % 2) * 2) - 1) ($y - 3 - ($blade % 3))
			}
			Fill-Ellipse $graphics (New-Color 170 88 118 60) ($x - 5) ($y - 1) 10 6
			Fill-Ellipse $graphics (New-Color 182 112 144 74) ($x - 2) ($y - 5) 8 6
			Fill-Ellipse $graphics (New-Color 145 68 94 44) ($x + 1) ($y - 1) 7 5
		}
	}
	Save-Canvas $bitmap $graphics $fileName
}

function Draw-RubbleOverlay([string]$fileName, [int]$variant) {
	$canvas = New-Canvas
	$bitmap = $canvas[0]
	$graphics = $canvas[1]
	$diamond = Get-Diamond
	$rand = New-Random (10400 + $variant * 47)
	Use-Clip $graphics $diamond {
		for ($i = 0; $i -lt 6; $i++) {
			$x = 12 + $rand.Next(0, 38)
			$y = 9 + $rand.Next(0, 14)
			$poly = New-Points @(
				(New-Point $x $y),
				(New-Point ($x + 3 + $rand.Next(0, 2)) ($y - 3 - $rand.Next(0, 2))),
				(New-Point ($x + 7 + $rand.Next(0, 2)) ($y + $rand.Next(-1, 2))),
				(New-Point ($x + 2 + $rand.Next(0, 2)) ($y + 2 + $rand.Next(0, 2)))
			)
			Fill-Polygon $graphics $poly (New-Color 182 126 99 75)
			Draw-Outline $graphics $poly (New-Color 120 76 57 42) 1.0
			Fill-Ellipse $graphics (New-Color 92 235 211 176) ($x + 2) ($y - 1) 3 1
			Fill-Ellipse $graphics (New-Color 88 82 60 42) ($x - 1) ($y + 2) 6 2
		}
	}
	Save-Canvas $bitmap $graphics $fileName
}

function Draw-CliffSouth([string]$fileName, [int]$variant) {
	$canvas = New-Canvas $Width $FaceHeight
	$bitmap = $canvas[0]
	$graphics = $canvas[1]
	$poly = New-Points @(
		(New-Point 32 0),
		(New-Point 63 16),
		(New-Point 63 35),
		(New-Point 32 20)
	)
	$palette = @(
		@((New-Color 255 170 126 84), (New-Color 255 72 47 31)),
		@((New-Color 255 161 117 76), (New-Color 255 66 43 28)),
		@((New-Color 255 179 135 91), (New-Color 255 79 53 35))
	)
	$colors = $palette[$variant - 1]
	Fill-Gradient $graphics $poly $colors[0] $colors[1]
	Use-Clip $graphics $poly {
		$rand = New-Random (11500 + $variant * 53)
		Add-StrataBands $graphics $poly $rand 4 (New-Color 112 220 192 150) (New-Color 82 98 66 46) 34 62
		Add-Cracks $graphics $poly $rand 5 (New-Color 102 67 43 26)
		Add-Grain $graphics $poly $rand 16 (New-Color 42 66 42 26) (New-Color 40 246 226 204)
		Fill-Polygon $graphics (New-Points @(
			(New-Point 44 7),
			(New-Point 63 18),
			(New-Point 63 35),
			(New-Point 32 20)
		)) (New-Color 70 34 22 18)
		for ($chip = 0; $chip -lt 3; $chip++) {
			$baseX = 39 + $chip * 6 + $rand.Next(-1, 2)
			$baseY = 9 + $chip * 5 + $rand.Next(-1, 2)
			$chipPoly = New-Points @(
				(New-Point $baseX $baseY),
				(New-Point ($baseX + 3) ($baseY + 2)),
				(New-Point $baseX ($baseY + 5))
			)
			Fill-Polygon $graphics $chipPoly (New-Color 82 246 218 178)
		}
	}
	Draw-Line $graphics (New-Color 222 228 206 164) 1.2 33 1 62 16
	Draw-Line $graphics (New-Color 180 65 40 26) 1.0 32 1 32 20
	Draw-Line $graphics (New-Color 176 82 50 31) 1.0 63 17 63 34
	Draw-Line $graphics (New-Color 198 50 31 20) 1.1 33 20 62 34
	Save-Canvas $bitmap $graphics $fileName
}

function Draw-WallEast([string]$fileName, [int]$variant) {
	$canvas = New-Canvas $Width $FaceHeight
	$bitmap = $canvas[0]
	$graphics = $canvas[1]
	$poly = New-Points @(
		(New-Point 0 16),
		(New-Point 32 0),
		(New-Point 32 20),
		(New-Point 0 35)
	)
	$palette = @(
		@((New-Color 255 165 156 148), (New-Color 255 82 76 71)),
		@((New-Color 255 156 148 140), (New-Color 255 75 69 64)),
		@((New-Color 255 174 166 158), (New-Color 255 88 82 77))
	)
	$colors = $palette[$variant - 1]
	Fill-Gradient $graphics $poly $colors[0] $colors[1]
	Use-Clip $graphics $poly {
		$rand = New-Random (13600 + $variant * 41)
		for ($course = 0; $course -lt 4; $course++) {
			$y = 31 - $course * 5 + $rand.Next(-1, 2)
			Draw-Line $graphics (New-Color 124 82 76 70) 1.0 1 $y 31 ($y - 15)
		}
		for ($joint = 0; $joint -lt 3; $joint++) {
			$x = 8 + $joint * 7 + $rand.Next(-1, 2)
			Draw-Line $graphics (New-Color 96 68 64 60) 1.0 $x 15 $x 32
		}
		Fill-Polygon $graphics (New-Points @(
			(New-Point 0 26),
			(New-Point 13 20),
			(New-Point 17 35),
			(New-Point 0 35)
		)) (New-Color 48 54 46 40)
	}
	Draw-Line $graphics (New-Color 222 218 210 188) 1.2 1 16 31 1
	Draw-Line $graphics (New-Color 126 86 80 74) 1.0 1 34 31 20
	Draw-Line $graphics (New-Color 110 74 70 64) 1.0 0 17 0 34
	Draw-Line $graphics (New-Color 102 70 66 60) 1.0 32 1 32 19
	Save-Canvas $bitmap $graphics $fileName
}

function Draw-WallSouth([string]$fileName, [int]$variant) {
	$canvas = New-Canvas $Width $FaceHeight
	$bitmap = $canvas[0]
	$graphics = $canvas[1]
	$poly = New-Points @(
		(New-Point 32 0),
		(New-Point 63 16),
		(New-Point 63 35),
		(New-Point 32 20)
	)
	$palette = @(
		@((New-Color 255 156 148 140), (New-Color 255 74 68 64)),
		@((New-Color 255 148 140 133), (New-Color 255 68 62 58)),
		@((New-Color 255 166 158 150), (New-Color 255 82 76 72))
	)
	$colors = $palette[$variant - 1]
	Fill-Gradient $graphics $poly $colors[0] $colors[1]
	Use-Clip $graphics $poly {
		$rand = New-Random (14200 + $variant * 47)
		for ($course = 0; $course -lt 4; $course++) {
			$y = 6 + $course * 6 + $rand.Next(-1, 2)
			Draw-Line $graphics (New-Color 118 78 74 66) 1.0 33 $y 62 ($y + 15)
		}
		for ($joint = 0; $joint -lt 3; $joint++) {
			$x = 40 + $joint * 7 + $rand.Next(-1, 2)
			Draw-Line $graphics (New-Color 92 66 62 56) 1.0 $x 8 $x 32
		}
		Fill-Polygon $graphics (New-Points @(
			(New-Point 45 8),
			(New-Point 63 17),
			(New-Point 63 35),
			(New-Point 32 20)
		)) (New-Color 46 44 40 34)
	}
	Draw-Line $graphics (New-Color 214 210 202 182) 1.2 33 1 62 16
	Draw-Line $graphics (New-Color 122 84 78 70) 1.0 33 20 62 34
	Draw-Line $graphics (New-Color 106 72 68 62) 1.0 32 1 32 19
	Draw-Line $graphics (New-Color 100 68 64 58) 1.0 63 17 63 34
	Save-Canvas $bitmap $graphics $fileName
}

function Draw-CliffEast([string]$fileName, [int]$variant) {
	$canvas = New-Canvas $Width $FaceHeight
	$bitmap = $canvas[0]
	$graphics = $canvas[1]
	$poly = New-Points @(
		(New-Point 0 16),
		(New-Point 32 0),
		(New-Point 32 20),
		(New-Point 0 35)
	)
	$palette = @(
		@((New-Color 255 179 132 87), (New-Color 255 79 51 33)),
		@((New-Color 255 169 121 78), (New-Color 255 72 46 29)),
		@((New-Color 255 187 139 93), (New-Color 255 87 58 37))
	)
	$colors = $palette[$variant - 1]
	Fill-Gradient $graphics $poly $colors[0] $colors[1]
	Use-Clip $graphics $poly {
		$rand = New-Random (12600 + $variant * 59)
		Add-StrataBands $graphics $poly $rand 4 (New-Color 118 232 200 154) (New-Color 86 105 70 48) 2 30
		Add-Cracks $graphics $poly $rand 4 (New-Color 102 70 46 28)
		Add-Grain $graphics $poly $rand 16 (New-Color 38 70 45 28) (New-Color 42 248 230 208)
		Fill-Polygon $graphics (New-Points @(
			(New-Point 0 23),
			(New-Point 19 14),
			(New-Point 32 20),
			(New-Point 0 35)
		)) (New-Color 56 52 33 19)
		for ($chip = 0; $chip -lt 3; $chip++) {
			$baseX = 5 + $chip * 7 + $rand.Next(-1, 2)
			$baseY = 27 - $chip * 5 + $rand.Next(-1, 2)
			$chipPoly = New-Points @(
				(New-Point $baseX $baseY),
				(New-Point ($baseX + 4) ($baseY - 2)),
				(New-Point ($baseX + 5) ($baseY + 1))
			)
			Fill-Polygon $graphics $chipPoly (New-Color 78 248 220 184)
		}
	}
	Draw-Line $graphics (New-Color 230 244 218 176) 1.2 1 16 31 1
	Draw-Line $graphics (New-Color 188 71 43 27) 1.0 0 17 0 34
	Draw-Line $graphics (New-Color 182 88 53 33) 1.0 32 1 32 19
	Draw-Line $graphics (New-Color 208 56 34 22) 1.1 1 34 31 20
	Save-Canvas $bitmap $graphics $fileName
}

function Draw-MarkerSelected([string]$fileName) {
	$canvas = New-Canvas
	$bitmap = $canvas[0]
	$graphics = $canvas[1]
	$outer = New-Points @(
		(New-Point 32 2),
		(New-Point 61 16),
		(New-Point 32 29),
		(New-Point 3 16)
	)
	$mid = New-Points @(
		(New-Point 32 5),
		(New-Point 54 16),
		(New-Point 32 26),
		(New-Point 10 16)
	)
	Draw-Outline $graphics $outer (New-Color 110 255 206 116) 2.4
	Draw-Outline $graphics $mid (New-Color 235 255 246 222) 1.6
	Fill-Polygon $graphics (New-Points @(
		(New-Point 32 12),
		(New-Point 37 16),
		(New-Point 32 20),
		(New-Point 27 16)
	)) (New-Color 160 255 233 174)
	Fill-Ellipse $graphics (New-Color 72 255 247 214) 20 11 24 10
	Save-Canvas $bitmap $graphics $fileName
}

function Draw-MarkerPreview([string]$fileName) {
	$canvas = New-Canvas
	$bitmap = $canvas[0]
	$graphics = $canvas[1]
	$outer = New-Points @(
		(New-Point 32 3),
		(New-Point 58 16),
		(New-Point 32 28),
		(New-Point 6 16)
	)
	$mid = New-Points @(
		(New-Point 32 6),
		(New-Point 52 16),
		(New-Point 32 25),
		(New-Point 12 16)
	)
	Draw-Outline $graphics $outer (New-Color 95 255 152 76) 1.9
	Draw-Outline $graphics $mid (New-Color 190 255 210 152) 1.2
	Fill-Ellipse $graphics (New-Color 56 255 184 110) 22 12 20 8
	Save-Canvas $bitmap $graphics $fileName
}

New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

Draw-LandTop "top_land_01.png" 1
Draw-LandTop "top_land_02.png" 2
Draw-LandTop "top_land_03.png" 3

Draw-WaterTop "top_water_01.png" 1
Draw-WaterTop "top_water_02.png" 2
Draw-WaterTop "top_water_03.png" 3

Draw-MudTop "top_mud_01.png" 1
Draw-MudTop "top_mud_02.png" 2
Draw-MudTop "top_mud_03.png" 3

Draw-ScrubOverlay "overlay_scrub_01.png" 1
Draw-ScrubOverlay "overlay_scrub_02.png" 2
Draw-ScrubOverlay "overlay_scrub_03.png" 3

Draw-RubbleOverlay "overlay_rubble_01.png" 1
Draw-RubbleOverlay "overlay_rubble_02.png" 2
Draw-RubbleOverlay "overlay_rubble_03.png" 3

Draw-CliffEast "cliff_east_01.png" 1
Draw-CliffEast "cliff_east_02.png" 2
Draw-CliffEast "cliff_east_03.png" 3

Draw-CliffSouth "cliff_south_01.png" 1
Draw-CliffSouth "cliff_south_02.png" 2
Draw-CliffSouth "cliff_south_03.png" 3

Draw-WallEast "wall_east_01.png" 1
Draw-WallEast "wall_east_02.png" 2
Draw-WallEast "wall_east_03.png" 3

Draw-WallSouth "wall_south_01.png" 1
Draw-WallSouth "wall_south_02.png" 2
Draw-WallSouth "wall_south_03.png" 3

Draw-MarkerSelected "marker_selected.png"
Draw-MarkerPreview "marker_preview.png"

Write-Host "Generated refined canyon tiles in $OutputDir"
