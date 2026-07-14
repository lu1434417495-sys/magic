extends SceneTree

# 离线 3D -> 2D 渲染脚本
# 用法：godot --path . --script tools/render_unit_sprite.gd

const MODEL_PATH := "res://assets/main/battle/units/player/knight_character.glb"
const SWORD_PATH := "res://assets/main/battle/units/player/sword.glb"
const OUTPUT_PATH := "res://assets/main/battle/units/player/warrior_knight_render.png"

# 输出画布尺寸（按项目规范 1x1 单位）
const CANVAS_WIDTH := 96
const CANVAS_HEIGHT := 128
const SUPERSAMPLE := 8  # 8x 超采

func _initialize():
	print("[render_unit_sprite] 启动离线渲染...")
	
	var root := get_root()
	root.set_title("Unit Sprite Render")
	root.set_size(Vector2i(320, 240))
	
	# 创建离屏 viewport
	var viewport := SubViewport.new()
	viewport.size = Vector2i(CANVAS_WIDTH * SUPERSAMPLE, CANVAS_HEIGHT * SUPERSAMPLE)
	viewport.transparent_bg = true
	viewport.msaa_3d = Viewport.MSAA_8X
	viewport.render_target_update_mode = SubViewport.UPDATE_ONCE
	root.add_child(viewport)
	
	# 创建世界场景
	var world_root := Node3D.new()
	viewport.add_child(world_root)
	
	# 环境光照
	var world := World3D.new()
	world.environment = Environment.new()
	world.environment.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	world.environment.ambient_light_color = Color(1.0, 1.0, 1.0)
	world.environment.ambient_light_energy = 2.5
	viewport.world_3d = world
	
	# 方向光（从相机方向照向角色正面）
	var light := DirectionalLight3D.new()
	world_root.add_child(light)
	light.position = Vector3(4.0, 8.0, 6.0)
	light.look_at_from_position(light.position, Vector3(0, 2.5, 0))
	light.light_energy = 4.0
	light.light_color = Color(1.0, 0.98, 0.95)
	
	# 正面补光（从相机方向直照，提亮深色盔甲）
	var fill := DirectionalLight3D.new()
	world_root.add_child(fill)
	fill.position = Vector3(3.5, 6.0, 4.0)
	fill.look_at_from_position(fill.position, Vector3(0, 2.5, 0))
	fill.light_energy = 3.0
	fill.light_color = Color(1.0, 0.98, 0.92)
	
	# 左侧轮廓光
	var rim := DirectionalLight3D.new()
	world_root.add_child(rim)
	rim.position = Vector3(-5.0, 4.0, 3.0)
	rim.look_at_from_position(rim.position, Vector3(0, 2.5, 0))
	rim.light_energy = 2.0
	rim.light_color = Color(0.9, 0.95, 1.0)
	
	# 相机：正交，3/4 视角
	var camera := Camera3D.new()
	world_root.add_child(camera)
	camera.projection = Camera3D.PROJECTION_ORTHOGONAL
	camera.position = Vector3(3.5, 6.0, 4.0)
	camera.look_at_from_position(camera.position, Vector3(0, 2.8, 0))
	camera.near = 0.01
	camera.far = 100.0
	camera.size = 6.2
	
	# 加载骑士模型（用 GLTFDocument 直接加载，无需预导入）
	var doc := GLTFDocument.new()
	var state := GLTFState.new()
	var err := doc.append_from_file(MODEL_PATH, state, 0)
	if err != OK:
		printerr("[render_unit_sprite] 无法加载 GLB: ", MODEL_PATH, " 错误码: ", err)
		quit()
		return
	
	var model := doc.generate_scene(state) as Node3D
	if model == null:
		printerr("[render_unit_sprite] generate_scene 返回 null")
		quit()
		return
	
	model.scale = Vector3(1.0, 1.0, 1.0)
	model.position = Vector3.ZERO
	model.rotation_degrees = Vector3(0, 0, 0)
	world_root.add_child(model)
	
	# 加载剑并附加到右手
	var sword_doc := GLTFDocument.new()
	var sword_state := GLTFState.new()
	var sword_err := sword_doc.append_from_file(SWORD_PATH, sword_state, 0)
	if sword_err == OK:
		var sword := sword_doc.generate_scene(sword_state) as Node3D
		if sword != null:
			# 尝试找到右手骨骼/节点
			var right_hand := _find_node_by_name(model, "Palm.R")
			if right_hand == null:
				right_hand = _find_node_by_name(model, "Hand.R")
			if right_hand == null:
				right_hand = _find_node_by_name(model, "RightHand")
			if right_hand == null:
				# 直接附加到模型上
				right_hand = model
			
			sword.scale = Vector3(1.0, 1.0, 1.0)
			sword.position = Vector3(1.2, 2.9, 0.5)
			sword.rotation_degrees = Vector3(0, 90, 0)
			right_hand.add_child(sword)
			print("[render_unit_sprite] 已附加剑到: ", right_hand.name)
	else:
		print("[render_unit_sprite] 剑加载失败，继续渲染无剑版本")
	
	# 等待渲染稳定
	await create_timer(1.0).timeout
	viewport.render_target_update_mode = SubViewport.UPDATE_ONCE
	await RenderingServer.frame_post_draw
	await create_timer(0.3).timeout
	viewport.render_target_update_mode = SubViewport.UPDATE_ONCE
	await RenderingServer.frame_post_draw
	
	# 获取图像
	var img := viewport.get_texture().get_image()
	if img == null:
		printerr("[render_unit_sprite] 无法获取 viewport 图像")
		quit()
		return
	
	# 超采缩放到最终尺寸：低多边形容器用 NEAREST 保持硬边缘
	img.resize(CANVAS_WIDTH, CANVAS_HEIGHT, Image.INTERPOLATE_NEAREST)
	
	# 保存 PNG
	var save_err := img.save_png(OUTPUT_PATH)
	if save_err != OK:
		printerr("[render_unit_sprite] 保存 PNG 失败: ", save_err)
	else:
		print("[render_unit_sprite] 已保存: ", OUTPUT_PATH, " 尺寸: ", img.get_size())
	
	quit()

func _find_node_by_name(node: Node, target: String) -> Node:
	if node.name.contains(target):
		return node
	for child in node.get_children(true):
		var found := _find_node_by_name(child, target)
		if found != null:
			return found
	return null
