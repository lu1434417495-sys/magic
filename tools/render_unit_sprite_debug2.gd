extends SceneTree

const MODEL_PATH := "res://assets/main/battle/units/player/knight_character.glb"

func _initialize():
	print("[debug] 启动多角度调试渲染...")
	
	var root := get_root()
	root.set_title("Debug Render")
	root.set_size(Vector2i(320, 240))
	
	for rot in [0, 90, 180, 270]:
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
		world.environment.background_color = Color(0.2, 0.2, 0.22)
		world.environment.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
		world.environment.ambient_light_color = Color(1, 1, 1)
		world.environment.ambient_light_energy = 1.2
		viewport.world_3d = world
		
		var light := DirectionalLight3D.new()
		world_root.add_child(light)
		light.position = Vector3(4, 8, 6)
		light.look_at_from_position(light.position, Vector3.ZERO)
		light.light_energy = 2.0
		
		var camera := Camera3D.new()
		world_root.add_child(camera)
		camera.projection = Camera3D.PROJECTION_ORTHOGONAL
		camera.position = Vector3(3.5, 6.0, 4.0)
		camera.look_at_from_position(camera.position, Vector3(0, 2.8, 0))
		camera.size = 6.2
		
		var doc := GLTFDocument.new()
		var state := GLTFState.new()
		doc.append_from_file(MODEL_PATH, state, 0)
		var model := doc.generate_scene(state) as Node3D
		model.rotation_degrees = Vector3(0, rot, 0)
		world_root.add_child(model)
		
		await create_timer(0.5).timeout
		viewport.render_target_update_mode = SubViewport.UPDATE_ONCE
		await RenderingServer.frame_post_draw
		
		var img := viewport.get_texture().get_image()
		var out := "res://assets/main/battle/units/player/knight_debug_rot_%d.png" % rot
		img.save_png(out)
		print("[debug] 已保存 ", out)
		
		viewport.queue_free()
	
	quit()
