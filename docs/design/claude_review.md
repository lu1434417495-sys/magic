下面是 CU-01 到 CU-04 的代码 review，按单元分档，标注 🔴 真实风险、🟡 改进建议、🟢 亮点。

---

CU-01　登录壳 / 预设 / 存档 / 显示设置🟢 亮点                                                                                                                                                                                                  - login_screen.gd 把 4 个 modal 的互斥交给 _is_modal_open() 统一判定，_unhandled_input 只在非 transitioning + 无 modal 时才触发 Enter→开始游戏，键盘/鼠标流并行清晰。

- 三个窗口脚本（preset picker / save list / display settings）结构对称：show_window/hide_window/_cancel/_apply + shade 点击即关。行为一致好维护。
- display_settings_service.gd 把 ConfigFile 读写、归一化、窗口应用做了彻底隔离，apply_settings 内部处理 fullscreen 和 windowed 的切换顺序（先 windowed+size，再切 fullscreen），避免尺寸被 fullscreen
  吞掉。

🟡 改进建议

- login_screen.gd:148-153 把 ERR_INVALID_DATA 解释成"旧仓库格式"——但 SaveSerializer.decode_v5_payload 对 payload 结构不对、save_slot_meta 缺失、world_data 为空、party_state 反序列化失败都会返回
  ERR_INVALID_DATA。现在 V4 早已下线，这条文案对普通"存档损坏"会误导用户。建议改为中性的"存档数据不完整或版本不匹配"。
- display_settings_service.gd:105-107 normalize_resolution 只要不在 COMMON_RESOLUTIONS 就静默回 1280×720。如果外部（比如未来新增 UI 缩放预览）传入任意分辨率，会被悄悄改写；当前调用方都只来自
  picker，没问题，但如果以后想支持自定义值，就得扩 COMMON 列表或放开归一化。
- world_preset_picker_window.gd:118 过滤掉 TEST_PRESET_ID：假定 test 只能从 TestButton 进入，和 _PRESETS 的"隐式契约"。最好在 world_preset_registry.gd 里直接加 is_formal: bool 标志，而不是在 UI 层靠
  preset_id 白名单——registry 变动时 UI 无感。

---

CU-02　GameSession / 日志 / 存档序列化

🟢 亮点

- 已经按文档承诺把序列化细节彻底挪到 SaveSerializer，GameSession 只剩状态机与装配；_load_v5_payload 走 decode_v5_payload 单入口，错误码回传一致。
- _load_save_index_entries + _rebuild_save_index_entries_from_save_files + _merge_save_index_entries 形成了"索引+rebuild 兜底"双源机制，即使 index.dat 丢失/损坏也能从 .dat 文件自愈——对 agent headless   回归非常稳。
- GameLogService 作为 sidecar，不进 SAVE_VERSION、不参与 payload，职责切分干净；_normalize_variant 递归处理 Vector2i/StringName，JSON Line 输出可被外部 tail。

🔴 真实风险

- SaveSerializer.normalize_save_meta (L180-213) 对 display_name / world_preset_name / world_size_cells 做硬拒（任一为空/零就返回 {}）。对于 bundled save 来说，load_bundled_save 会先补 display_name =
  display_name if not display_name.is_empty() else save_id 再传，但如果调用方把空的 display_name 传进来而 save_id 又不是空，路径是 OK 的。问题出在 _rebuild_save_index_entries_from_save_files
  里：如果有个旧/半写 save 的 payload 缺少 save_slot_meta.display_name，extract_save_meta_from_payload → normalize_save_meta 直接返回 {}，该条目就会从 rebuild 结果里消失，然后被 merge
  掉。也就是一个损坏了 display_name 字段的存档永远不会再出现在列表里，玩家看不到也改不了。建议 rebuild 路径里做软降级（display_name = save_id），而不是直接丢。
- GameSession.save_serializer._is_supported_vector2i_value (L437-443) 要求 Dictionary 必须同时包含 x 和 y 键。但 serialize_world_data → store_var(..., full_objects=false) 写入的是 Vector2i
  本体，round-trip 没问题；唯一隐患是如果 world_data 经过 JSON 中转（例如未来 GameLogService 想复用 normalize 链），拒绝条件会触发。非阻塞，但值得留意。

🟡 改进建议

- GameSession._generate_unique_save_id (L639-656) 每次重试都调用 _load_save_index_entries()（L712 又会做一次全目录扫描）。128 次碰撞概率几乎为 0，但每次 loop 成本 = O(saves 目录大小)。建议把 index
  结果 cache 成局部变量后再循环检查。
- GameSession._load_save_index_entries (L712-739) 无论 index 是否正常，都会无条件再跑一次 _rebuild_save_index_entries_from_save_files 做 merge，导致每次 list
  都扫全目录。对少量存档没问题；量大了（比如 headless 批量）是明显 I/O 浪费。可以改成 index 校验通过就跳过 rebuild，只在解析失败 / 条目数与文件数不一致时 rebuild。
- GameSession._create_default_party_state + _grant_random_starting_book_skill（L1017-1049）是逻辑最重的一段，放在 session 层感觉越位——它生成的是玩家队伍"内容"，属于 progression 域。后续如果要支持
  preset-specific 起始队伍，这里会很难扩展。可以单独抽 default_party_factory.gd。
- SaveSerializer.normalize_party_state (L298-299) 若 active_member_ids 最终为空，会把 leader_member_id = &""。L295 有保证（member_states 非空时强塞第一个），但当 member_states 为空时没有
  warning。如果任何外部流程保存了空队伍，就会复活一个"无 leader"状态——后续 get_leader_member_state 返回 null。建议加 push_warning。

---

CU-03　world config 资源 & 共享 bundle

🟢 亮点

- 把"通用主世界内容"从每个 world_map_config.tres 抽到 shared/ 下的 5 个 pool + 2 个 bundle，这是本批改动的核心收益。回归 run_world_map_shared_content_injection_regression.gd 覆盖齐了：模板 id
  前缀、各 pool 数量、pool 语义后缀（镇/城/王都/帝都）、master_reforge 服务注入、mist_hollow profile。很扎实。
- WorldMapSettlementNamePool.build_unique_display_names 做了 strip + 去重，配合生成阶段的 Fisher-Yates 洗牌（_build_shuffled_display_names_from_pool），既可复现又稳定不重名。
- test/small/medium/demo 四个 .tres 现在都是"参数壳"，settlement_library / facility_library / wild_monster_distribution / settlement_distribution 全部空数组 + inject_default_main_world_content =
  true，和文档 CU-03 承诺一致。

🔴 真实风险

- data/configs/world_map/demo_world_map_config.tres 缺 procedural_metropolis_count 字段，默认 0。这是"巨型"(2000×2000) 世界，却一个都会都不生成。而 test/small/medium（尺度都更小）都显式设了
  1。如果是有意的，请注释说明；否则这是配置 regression——demo 原本可能有更多 metropolis，被抽取 shared 时漏填。另外 procedural_capital_count = 3、procedural_world_stronghold_count = 2 但
  metropolis_count = 0，语义上不自然。

🟡 改进建议

- world_map_generation_config.gd:35 procedural_metropolis_count := 0 默认和其它 tier（默认 1）的不对称需要注释说明"metropolis 是稀有顶层据点，默认不生成"，否则下一个改这个文件的人会以为是遗漏。
- world_map_settlement_bundle.gd / world_map_wild_spawn_bundle.gd 没有任何 validate()/describe()，空数组加载静默通过（只有 spawn system 那里
  push_warning）。共享资源改错时，agent/人都只能等运行时报警。建议给 bundle 加一个 get_validation_errors() -> Array[String]，供 CU-02 _refresh_* 一起验收。
- metropolis_spacing_cells := 340 vs. 其他 tier 间距递增是 80→110→150→220→280→340。乍一看挺合理，但 test 世界只有 200×200，340 大于对角线，意味着只能放 1 个 metropolis；test_world_map_config.tres
  里显式把各 tier 间距压到 16~46，和默认完全脱耦，说明默认值就是给 small+ 世界写的。可以考虑把默认改到 test 常用尺度，或者干脆把 spacing 体系挪到专门的 distance resource。

---

CU-04　world spawn system

🟢 亮点

- _build_libraries → _resolve_effective_*_library → _*_by_id 三层把共享注入 + 本地覆盖 + id 查表的职责切得很清楚。per-build 缓存（_resolved_*_library,
  _remaining_default_main_world_*_display_names）保证了每次 build_world 都是幂等的、rng 可复现。
- 名称池按 template id 分发（L969-978）：template_town → 镇池、template_city → 城池、template_capital → 王都池、template_metropolis → 帝都池，fallback 到通用村池。这条分发链是 shared content
  注入的重点，回归里也 assert 了 4 个 tier 的结尾字符。
- _collect_services 末尾兜底注入 party_warehouse 服务（L429-441）——即便据点模板忘了配仓储 NPC，玩家也永远能在任意据点走仓储流程。这是 headless 回归的关键保障。
- encounter_anchor / world_event / mounted_submap 的 to_dict / from_dict 成对存在，normalize_world_data 识别 runtime instance vs Dictionary 两种形态，支持 savefile round-trip。

🔴 真实风险

- _resolve_settlement_display_name (L968-983) 把 template_world_stronghold（tier=4）走通用村池 fallback，pool 里全是 "白鹿渡口村"、"银松磨坊村" 一类以"村"结尾的村名。world_stronghold
  实例会被命名为"某某某村"，语义完全错位。要么给 world_stronghold 加专门 name pool（参考都会/王都模式），要么在 fallback 里 if template_id == "template_world_stronghold": continue 让它回落到
  settlement_config.display_name（显示为"世界据点"/"世界据点 02"）。
- _generate_procedural_encounter_anchors (L657-659) 硬编码 north_rule = _resolved_wild_spawn_rules[0]、south_rule = _resolved_wild_spawn_rules[min(1, size-1)]，然后按 chunk_y < midpoint_chunk_y
  切南北。这假设共享 bundle 里rule 顺序 = [north, south]（目前 main_world_default_wild_spawn_bundle.tres 正好这样）。只要有人调整 bundle 内顺序，或把本地 wild_monster_distribution 放到 bundle
  前面（_resolve_effective_wild_spawn_rules 做的是 default + local 拼接），南北就会错乱。建议按 region_tag（north_wilds / south_wilds）或 rule 里的某个字段显式筛，不要靠数组位置。

🟡 改进建议

- _generate_procedural_encounter_anchors L667 posmod(chunk_seed, 6) != 0 这个密度 gate 是魔数；暴露成 procedural_wild_chunk_density_divisor 会更易调参。
- _resolve_effective_settlement_library (L853-860) / _resolve_effective_facility_library (L863-870) 不做 template_id 冲突检测：如果本地 facility_library 里有人定义 facility_id = "inn" 想覆盖共享
  inn，当前逻辑是后者覆盖前者（因为是 Dict 写入），但没有 log。希望的覆盖是 ok 的，但误写重复 id 就看不到。可以在 _build_libraries 里记录"本地覆盖了共享的 X"并 log。
- _resolve_effective_wild_spawn_rules 也是 default+local 纯拼接，没 dedup。如果玩家/config 又写一条 north_wilds rule，会在 procedural 路径里直接替代共享 north（因为是 index 0 还是 1 取决于顺序）；在
  fixed 路径里会并行生成两套狼群。
- _ensure_default_settlement_encounter (L801-836) 要求 rule 里存在 monster_template_id == &"wolf_pack" 才生成"荒狼巢穴"兜底。共享 bundle 里确实有，但本地配置可以把 wolf_pack
  规则全部去掉，这时兜底静默失败。建议 push_warning。
- _generate_world_npcs (L578-607) 的 npc_names 硬编码在代码里（"巡路信使"/"驿站商人"/...），和 CU-03 把名字挪到 pool 资源的方向背道而驰。后续补一个 world_npc_name_pool.tres 保持一致性。
- _build_settlement_instance_id L520 "%s_%02d"——2 位补零，procedural_village_count = 48 时够用，demo_world_map_config.tres 没有 metropolis 但 village 上限 48 也没超 2 位；不过上限是 256（见
  @export_range），超过 100 就会变 template_village_100。不影响正确性，只是 id 形式不整齐。

---

跨单元关注点

1. 文档一致性：docs/design/project_context_units.md L256 "其template_capital / template_metropolis 分别额外优先使用..." 这段和 _resolve_settlement_display_name 的实际分发链匹配；但没提到
   template_world_stronghold 的命名行为，正好对应上面指出的真实风险。修掉 stronghold 命名问题时，同步更新文档。
2. demo 世界 metropolis 丢失 + world_stronghold 被命名成村：两条合并起来看，推测共享抽取是分阶段做的，metropolis/stronghold 的收尾没走完。建议作为下一步 focused fix。
3. 回归覆盖：run_world_map_shared_content_injection_regression.gd 目前只对 SMALL_WORLD_CONFIG 断言了唯一性与分 pool 命名。把 demo 也加进断言（found_metropolis_instance 针对 demo），能立刻抓到
   procedural_metropolis_count = 0 这种 regression。

---

建议修复优先级

┌────────┬─────────────────────────────────────────┬───────────────────────────────────────────┐
│ 优先级 │                  事项                   │                   位置                    │
├────────┼─────────────────────────────────────────┼───────────────────────────────────────────┤
│ P0     │ world_stronghold 用村名（语义错）       │ world_map_spawn_system.gd:977             │
├────────┼─────────────────────────────────────────┼───────────────────────────────────────────┤
│ P0     │ demo 世界缺 procedural_metropolis_count │ demo_world_map_config.tres                │
├────────┼─────────────────────────────────────────┼───────────────────────────────────────────┤
│ P1     │ 南北 rule 靠数组下标                    │ world_map_spawn_system.gd:657-659         │
├────────┼─────────────────────────────────────────┼───────────────────────────────────────────┤
│ P1     │ 登录错误提示"旧仓库格式"误导            │ login_screen.gd:149                       │
├────────┼─────────────────────────────────────────┼───────────────────────────────────────────┤
│ P1     │ rebuild 丢弃缺字段存档                  │ save_serializer.gd:180-213 via _rebuild_* │
├────────┼─────────────────────────────────────────┼───────────────────────────────────────────┤
│ P2     │ metropolis 默认 0 需注释                │ world_map_generation_config.gd:35         │
├────────┼─────────────────────────────────────────┼───────────────────────────────────────────┤
│ P2     │ bundle / pool 无 validate 接口          │ world_map_settlement_bundle.gd 等         │
├────────┼─────────────────────────────────────────┼───────────────────────────────────────────┤
│ P2     │ index rebuild 无条件全扫                │ game_session.gd:712-739                   │
└────────┴─────────────────────────────────────────┴───────────────────────────────────────────┘
