extends Node3D
## Isometric tile baker: renders a stone cube under a fixed 2:1 orthographic
## camera and slices it into top / east-face / south-face tiles. Run via:
##   godot --path . res://scenes/tools/tile_baker.tscn
## It writes PNGs into OUT_DIR then quits. See docs note in chat history.

const OUT_DIR := "res://assets/main/battle/terrain/canyon/"
const DEBUG_DIR := "res://tmp/tile_bake_debug/"

const SS := 16                # supersample factor before downscale (higher = more detail for 8x tiles)
const TILE_W := 64            # target diamond width
const TILE_H := 32            # target diamond height (2:1)
const FACE_H := 36            # target cliff face height (matches existing masks)

# World cube: 1x1 footprint, height tuned so the side face ~= FACE_H px.
const CUBE_HEIGHT := 1.10

# Camera framing (tuned empirically against the debug dump).
const PITCH_DEG := -26.565    # atan(0.5) -> exact 2:1 diamond
const YAW_DEG := 45.0
const ORTHO_SIZE := 2.6       # vertical world units across viewport height (tune)

var _vp: SubViewport
var _cam: Camera3D
var _cube: MeshInstance3D

func _ready() -> void:
	_build()
	# let it render a few frames
	for i in range(4):
		await RenderingServer.frame_post_draw
	_dump_debug()
	get_tree().quit()

func _build() -> void:
	_vp = SubViewport.new()
	_vp.size = Vector2i(TILE_W * SS, (TILE_H + FACE_H + 16) * SS)
	_vp.transparent_bg = true
	_vp.msaa_3d = Viewport.MSAA_8X
	_vp.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	add_child(_vp)

	var world := Node3D.new()
	_vp.add_child(world)

	_cam = Camera3D.new()
	_cam.projection = Camera3D.PROJECTION_ORTHOGONAL
	_cam.size = ORTHO_SIZE
	_cam.near = 0.01
	_cam.far = 100.0
	# place on a rig pointing at origin
	var pivot := Node3D.new()
	pivot.rotation_degrees = Vector3(PITCH_DEG, YAW_DEG, 0.0)
	world.add_child(pivot)
	_cam.position = Vector3(0, 0, 10)
	pivot.add_child(_cam)

	var key := DirectionalLight3D.new()
	key.rotation_degrees = Vector3(-55, -50, 0)
	key.light_energy = 1.25
	world.add_child(key)
	var fill := DirectionalLight3D.new()
	fill.rotation_degrees = Vector3(-20, 130, 0)
	fill.light_energy = 0.35
	world.add_child(fill)

	var env := WorldEnvironment.new()
	var e := Environment.new()
	e.background_mode = Environment.BG_CLEAR_COLOR
	e.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	e.ambient_light_color = Color(0.55, 0.56, 0.6)
	e.ambient_light_energy = 0.7
	env.environment = e
	world.add_child(env)

	_cube = MeshInstance3D.new()
	var box := BoxMesh.new()
	box.size = Vector3(1.0, CUBE_HEIGHT, 1.0)
	_cube.mesh = box
	# top of cube at y=0 so the top diamond sits at world origin plane
	_cube.position = Vector3(0, -CUBE_HEIGHT * 0.5, 0)
	_cube.material_override = _material_for_mode(_mode())
	world.add_child(_cube)

func _mode() -> String:
	var ua := OS.get_cmdline_user_args()
	return ua[0] if ua.size() > 0 else "stone"

func _material_for_mode(mode: String) -> Material:
	match mode:
		"water":
			return _water_material(0)
		"mud":
			return _mud_material(0)
		"dground":
			return _real_pbr_material("Ground080", 2.0)
		"drock":
			return _real_pbr_material("Rock029", 1.5)
		"grass":
			return _real_pbr_material("Grass004", 2.5)
		_:
			return _stone_material(0)

# Builds a material from real CC0 PBR maps in res://assets/main/battle/terrain/_textures/.
func _real_pbr_material(id: String, uv_scale: float) -> StandardMaterial3D:
	var dir := "res://assets/main/battle/terrain/_textures/"
	var m := StandardMaterial3D.new()
	var col := _load_image_texture(dir + id + "_Color.jpg")
	if col != null:
		m.albedo_texture = col
	var nrm := _load_image_texture(dir + id + "_Normal.jpg")
	if nrm != null:
		m.normal_enabled = true
		m.normal_texture = nrm
	var rgh := _load_image_texture(dir + id + "_Rough.jpg")
	if rgh != null:
		m.roughness_texture = rgh
	m.uv1_scale = Vector3(uv_scale, uv_scale, uv_scale)
	return m

func _load_image_texture(res_path: String) -> ImageTexture:
	var abs_path := ProjectSettings.globalize_path(res_path)
	var img := Image.load_from_file(abs_path)
	if img == null:
		push_error("[tile_baker] failed to load " + abs_path)
		return null
	return ImageTexture.create_from_image(img)

func _mud_material(seed: int) -> StandardMaterial3D:
	var m := StandardMaterial3D.new()
	# Lumpy mud: low-freq fractal blobs for big wet/dry patches.
	var n := FastNoiseLite.new()
	n.noise_type = FastNoiseLite.TYPE_SIMPLEX
	n.frequency = 0.03
	n.seed = seed
	n.fractal_octaves = 5
	var albedo := NoiseTexture2D.new()
	albedo.width = 256
	albedo.height = 256
	albedo.seamless = true
	albedo.noise = n
	albedo.color_ramp = _mud_ramp()
	m.albedo_texture = albedo
	# Cracked-earth ridges from cellular borders.
	var nn := FastNoiseLite.new()
	nn.noise_type = FastNoiseLite.TYPE_CELLULAR
	nn.frequency = 0.06
	nn.seed = seed + 5
	nn.cellular_jitter = 1.0
	nn.cellular_return_type = FastNoiseLite.RETURN_DISTANCE2_SUB
	var nrm := NoiseTexture2D.new()
	nrm.width = 256
	nrm.height = 256
	nrm.seamless = true
	nrm.as_normal_map = true
	nrm.bump_strength = 5.0
	nrm.noise = nn
	m.normal_enabled = true
	m.normal_texture = nrm
	m.roughness = 0.97
	m.metallic = 0.0
	m.uv1_scale = Vector3(1.5, 1.5, 1.5)
	return m

func _mud_ramp() -> Gradient:
	var g := Gradient.new()
	g.offsets = PackedFloat32Array([0.0, 0.4, 0.7, 1.0])
	g.colors = PackedColorArray([
		Color(0.20, 0.13, 0.07),
		Color(0.33, 0.22, 0.12),
		Color(0.46, 0.33, 0.19),
		Color(0.56, 0.43, 0.27),
	])
	return g

func _water_material(seed: int) -> StandardMaterial3D:
	var m := StandardMaterial3D.new()
	# deep -> shallow teal albedo from low-freq noise
	var n := FastNoiseLite.new()
	n.noise_type = FastNoiseLite.TYPE_SIMPLEX
	n.frequency = 0.03
	n.seed = seed
	var albedo := NoiseTexture2D.new()
	albedo.width = 256
	albedo.height = 256
	albedo.seamless = true
	albedo.noise = n
	albedo.color_ramp = _water_ramp()
	m.albedo_texture = albedo
	# ripple normal map (bigger, smoother waves)
	var nn := FastNoiseLite.new()
	nn.noise_type = FastNoiseLite.TYPE_SIMPLEX
	nn.frequency = 0.11
	nn.seed = seed + 11
	nn.fractal_octaves = 3
	var nrm := NoiseTexture2D.new()
	nrm.width = 256
	nrm.height = 256
	nrm.seamless = true
	nrm.as_normal_map = true
	nrm.bump_strength = 2.0
	nrm.noise = nn
	m.normal_enabled = true
	m.normal_texture = nrm
	m.metallic = 0.25
	m.metallic_specular = 0.9
	m.roughness = 0.12
	m.uv1_scale = Vector3(2, 2, 2)
	m.rim_enabled = true
	m.rim = 0.4
	return m

func _water_ramp() -> Gradient:
	var g := Gradient.new()
	g.offsets = PackedFloat32Array([0.0, 0.5, 0.8, 1.0])
	g.colors = PackedColorArray([
		Color(0.05, 0.22, 0.38),
		Color(0.10, 0.40, 0.58),
		Color(0.20, 0.58, 0.72),
		Color(0.45, 0.80, 0.88),
	])
	return g

func _stone_material(seed: int) -> StandardMaterial3D:
	var m := StandardMaterial3D.new()
	# Clean sandstone: smooth low-contrast tonal variation, not busy cracked facets.
	var n := FastNoiseLite.new()
	n.noise_type = FastNoiseLite.TYPE_SIMPLEX
	n.frequency = 0.016
	n.seed = seed
	n.fractal_octaves = 3
	n.fractal_gain = 0.38
	var albedo := NoiseTexture2D.new()
	albedo.width = 256
	albedo.height = 256
	albedo.seamless = true
	albedo.noise = n
	albedo.color_ramp = _stone_ramp()
	m.albedo_texture = albedo
	# Very soft surface undulation only -> gentle shading, no harsh ridges.
	var nn := FastNoiseLite.new()
	nn.noise_type = FastNoiseLite.TYPE_SIMPLEX
	nn.frequency = 0.04
	nn.seed = seed + 7
	var nrm := NoiseTexture2D.new()
	nrm.width = 256
	nrm.height = 256
	nrm.seamless = true
	nrm.as_normal_map = true
	nrm.bump_strength = 1.4
	nrm.noise = nn
	m.normal_enabled = true
	m.normal_texture = nrm
	m.roughness = 0.92
	m.uv1_scale = Vector3(1.0, 1.0, 1.0)
	return m

func _stone_ramp() -> Gradient:
	# Warm sandstone: tan / beige with warm undertones.
	var g := Gradient.new()
	g.offsets = PackedFloat32Array([0.0, 0.4, 0.7, 1.0])
	g.colors = PackedColorArray([
		Color(0.67, 0.57, 0.43),
		Color(0.75, 0.65, 0.50),
		Color(0.81, 0.71, 0.56),
		Color(0.86, 0.77, 0.62),
	])
	return g

func _dump_debug() -> void:
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(DEBUG_DIR))
	var img := _vp.get_texture().get_image()
	var fn := "raw_full_%s.png" % _mode()
	img.save_png(DEBUG_DIR + fn)
	print("[tile_baker] dumped %s size=%s" % [fn, img.get_size()])
