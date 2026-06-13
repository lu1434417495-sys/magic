extends Node3D
## Renders a Kenney nature GLB tree under the same 2:1 isometric camera as the
## terrain tiles, into a tall transparent sprite. Run:
##   godot --path . res://scenes/tools/tree_baker.tscn -- tree_oak
## Writes res://tmp/tile_bake_debug/tree_<name>.png and prints the base anchor.

const SS := 4
const OUT_DIR := "res://tmp/tile_bake_debug/"
const VP_W := 320
const VP_H := 440
const PITCH_DEG := -26.565
const YAW_DEG := 45.0

func _ready() -> void:
	var model_name := "tree_oak"
	var ua := OS.get_cmdline_user_args()
	if ua.size() > 0:
		model_name = ua[0]
	await _bake(model_name)
	get_tree().quit()

func _bake(model_name: String) -> void:
	var vp := SubViewport.new()
	vp.size = Vector2i(VP_W * SS, VP_H * SS)
	vp.transparent_bg = true
	vp.msaa_3d = Viewport.MSAA_8X
	vp.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	add_child(vp)

	var world := Node3D.new()
	vp.add_child(world)

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
	e.ambient_light_color = Color(0.6, 0.62, 0.66)
	e.ambient_light_energy = 0.7
	env.environment = e
	world.add_child(env)

	var cam := Camera3D.new()
	cam.projection = Camera3D.PROJECTION_ORTHOGONAL
	cam.near = 0.01
	cam.far = 1000.0
	var pivot := Node3D.new()
	pivot.rotation_degrees = Vector3(PITCH_DEG, YAW_DEG, 0.0)
	world.add_child(pivot)
	cam.position = Vector3(0, 0, 100)
	pivot.add_child(cam)

	var path := _model_path(model_name)
	var scene: PackedScene = load(path)
	var tree := scene.instantiate()
	world.add_child(tree)
	await get_tree().process_frame
	_apply_foliage_alpha(tree, model_name)

	var aabb := _node_aabb(tree, tree.global_transform)
	# move tree so its base-centre sits at world origin
	tree.position = Vector3(
		-(aabb.position.x + aabb.size.x * 0.5),
		-aabb.position.y,
		-(aabb.position.z + aabb.size.z * 0.5)
	)
	var height := aabb.size.y
	var footprint: float = max(aabb.size.x, aabb.size.z)
	pivot.position = Vector3(0.0, height * 0.5, 0.0)
	# fit by the largest dimension so low/wide rocks aren't clipped (trees fit by height)
	cam.size = max(height * 1.5 * (float(VP_H) / float(VP_W)), footprint * 1.6)

	for i in range(4):
		await RenderingServer.frame_post_draw

	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(OUT_DIR))
	var img := vp.get_texture().get_image()
	img.save_png(OUT_DIR + "tree_%s.png" % model_name)
	var base_px := cam.unproject_position(Vector3.ZERO)
	print("[tree_baker] %s size=%s base_px=%s height=%.2f" % [model_name, img.get_size(), base_px, height])

func _model_path(model_name: String) -> String:
	var dir := "res://assets/main/battle/terrain/_models/"
	# Poly Haven realistic trees live in their own folder as .gltf
	if FileAccess.file_exists(dir + model_name + "/" + model_name + "_1k.gltf"):
		return dir + model_name + "/" + model_name + "_1k.gltf"
	return dir + model_name + ".glb"

# Replace leaf-card materials (jpg gltf -> no alpha) with an alpha-cutout material
# built from the composed leaves_rgba.png so foliage isn't a solid block.
func _apply_foliage_alpha(root: Node, model_name: String) -> void:
	var tdir := "res://assets/main/battle/terrain/_models/%s/textures/" % model_name
	var rgba_path := tdir + "leaves_rgba.png"
	if not FileAccess.file_exists(rgba_path):
		return
	var rgba := _load_tex(rgba_path)
	var nrm := _load_tex(tdir + "%s_leaves_nor_gl_1k.jpg" % model_name)
	var leaf_mat := StandardMaterial3D.new()
	leaf_mat.albedo_texture = rgba
	leaf_mat.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA_SCISSOR
	leaf_mat.alpha_scissor_threshold = 0.25
	leaf_mat.cull_mode = BaseMaterial3D.CULL_DISABLED
	if nrm != null:
		leaf_mat.normal_enabled = true
		leaf_mat.normal_texture = nrm
	leaf_mat.roughness = 0.9
	_override_leaf_surfaces(root, leaf_mat)

func _override_leaf_surfaces(node: Node, mat: Material) -> void:
	if node is MeshInstance3D:
		var mi := node as MeshInstance3D
		var mesh := mi.mesh
		if mesh != null:
			for s in range(mesh.get_surface_count()):
				var existing := mi.get_active_material(s)
				var nm := ""
				if existing != null:
					nm = existing.resource_name.to_lower()
				if nm.find("leaf") != -1 or nm.find("leaves") != -1:
					mi.set_surface_override_material(s, mat)
	for child in node.get_children():
		_override_leaf_surfaces(child, mat)

func _load_tex(res_path: String) -> ImageTexture:
	var img := Image.load_from_file(ProjectSettings.globalize_path(res_path))
	if img == null:
		return null
	return ImageTexture.create_from_image(img)

func _node_aabb(node: Node, _xf: Transform3D) -> AABB:
	var result := AABB()
	var has := false
	for child in node.get_children():
		if child is MeshInstance3D:
			var m := child as MeshInstance3D
			var local := m.get_aabb()
			var world_aabb := m.global_transform * local
			if not has:
				result = world_aabb
				has = true
			else:
				result = result.merge(world_aabb)
		var sub := _node_aabb(child, _xf)
		if sub.size != Vector3.ZERO:
			if not has:
				result = sub
				has = true
			else:
				result = result.merge(sub)
	return result
