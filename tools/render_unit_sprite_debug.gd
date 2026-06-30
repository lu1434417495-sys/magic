extends SceneTree

const MODEL_PATH := "res://assets/main/battle/units/player/warrior_knight_sample.glb"
const OUTPUT_PATH := "res://assets/main/battle/units/player/warrior_knight_debug.png"

func _initialize():
	print("[debug] 启动调试渲染...")
	
	var root := get_root()
	root.set_title("Debug Render")
	root.set_size(Vector2i(320, 240))
	
	var viewport := SubViewport.new()
	viewport.size = Vector2i(384, 512)
	viewport.transparent_bg = false
	viewport.render_target_update_mode = SubViewport.UPDATE_ONCE
	root.add_child(viewport)
	
	var world_root := Node3D.new()
	viewport.add_child(world_root)
	
	var world := World3D.new()
	world.environment = Environment.new()
	world.environment.background_mode = Environment.BG_COLOR
	world.environment.background_color = Color(0.15, 0.15, 0.18)
	world.environment.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	world.environment.ambient_light_color = Color(1, 1, 1)
	world.environment.ambient_light_energy = 1.0
	viewport.world_3d = world
	
	var light := DirectionalLight3D.new()
	world_root.add_child(light)
	light.position = Vector3(2, 4, 3)
	light.look_at_from_position(light.position, Vector3.ZERO)
	light.light_energy = 1.5
	
	var camera := Camera3D.new()
	world_root.add_child(camera)
	camera.projection = Camera3D.PROJECTION_ORTHOGONAL
	camera.position = Vector3(2, 2, 2)
	camera.look_at_from_position(camera.position, Vector3.ZERO)
	camera.size = 5.0
	
	var doc := GLTFDocument.new()
	var state := GLTFState.new()
	var err := doc.append_from_file(MODEL_PATH, state, 0)
	print("[debug] append_from_file err: ", err)
	if err != OK:
		printerr("[debug] 无法加载 GLB: ", err)
		quit()
		return
	
	var model := doc.generate_scene(state)
	print("[debug] generate_scene 类型: ", model.get_class())
	print("[debug] 节点树:")
	_print_tree(model, 0)
	
	model.position = Vector3.ZERO
	world_root.add_child(model)
	
	await create_timer(1.0).timeout
	viewport.render_target_update_mode = SubViewport.UPDATE_ONCE
	await RenderingServer.frame_post_draw
	
	var img := viewport.get_texture().get_image()
	if img == null:
		printerr("[debug] 无法获取图像")
	else:
		img.save_png(OUTPUT_PATH)
		print("[debug] 已保存调试图: ", OUTPUT_PATH)
	
	quit()

func _print_tree(node: Node, depth: int):
	var indent := "  ".repeat(depth)
	print(indent, node.name, " [", node.get_class(), "]")
	if node is MeshInstance3D:
		print(indent, "  mesh: ", node.mesh)
		if node.mesh != null:
			print(indent, "  aabb: ", node.mesh.get_aabb())
	for child in node.get_children():
		_print_tree(child, depth + 1)
