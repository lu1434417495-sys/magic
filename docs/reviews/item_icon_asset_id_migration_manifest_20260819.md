# Item icon → asset ID 迁移清单

迁移提交：`b4beafe7 feat: migrate item authoring to flat JSON`

重建日期：2026-08-24

状态：Historical field-level audit artifact

## 核对口径

- 旧值从 `b4beafe7^` 的 129 个 `data/configs/items/*.tres` 读取；未显式声明 `icon` 的 item 按 `base_item_id` 解析 `data/configs/items_templates/<base_item_id>.tres`，表中 `inherited:*` 明确记录该来源。
- 新值从 `b4beafe7:data/configs/json/items/items.json` 按 `item_id` 对齐读取，不使用当前 checkout 的后续新增内容。
- 对齐结果：129/129；旧有效值为 109 条 `res://icon.svg`、20 条空值；新值为 109 条 `ui.item.icon.default`、20 条空值；缺失 ID 为 0。
- 本文件只保留迁移证据，不恢复 production authored-path 反向索引或运行时兼容路径。

## 字段级清单

| item_id | 旧有效 `icon` | 新 `icon_asset_id` | 旧值来源 |
|---|---|---|---|
| `acc_phoenix_rebirth_badge` | `<empty>` | `<empty>` | `direct` |
| `acc_phoenix_rebirth_cloak` | `<empty>` | `<empty>` | `direct` |
| `acc_phoenix_rebirth_necklace` | `<empty>` | `<empty>` | `direct` |
| `acc_phoenix_rebirth_ring_1` | `<empty>` | `<empty>` | `direct` |
| `acc_phoenix_rebirth_ring_2` | `<empty>` | `<empty>` | `direct` |
| `acc_phoenix_rebirth_trinket` | `<empty>` | `<empty>` | `direct` |
| `acc_time_traveler_badge` | `<empty>` | `<empty>` | `direct` |
| `acc_time_traveler_cloak` | `<empty>` | `<empty>` | `direct` |
| `acc_time_traveler_necklace` | `<empty>` | `<empty>` | `direct` |
| `acc_time_traveler_ring_1` | `<empty>` | `<empty>` | `direct` |
| `acc_time_traveler_ring_2` | `<empty>` | `<empty>` | `direct` |
| `acc_time_traveler_trinket` | `<empty>` | `<empty>` | `direct` |
| `antidote_herb` | `res://icon.svg` | `ui.item.icon.default` | `direct` |
| `armor_phoenix_rebirth_body` | `<empty>` | `<empty>` | `direct` |
| `armor_phoenix_rebirth_feet` | `<empty>` | `<empty>` | `direct` |
| `armor_phoenix_rebirth_hands` | `<empty>` | `<empty>` | `direct` |
| `armor_phoenix_rebirth_head` | `<empty>` | `<empty>` | `direct` |
| `armor_time_traveler_body` | `<empty>` | `<empty>` | `direct` |
| `armor_time_traveler_feet` | `<empty>` | `<empty>` | `direct` |
| `armor_time_traveler_hands` | `<empty>` | `<empty>` | `direct` |
| `armor_time_traveler_head` | `<empty>` | `<empty>` | `direct` |
| `ash_longbow` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_longbow_base` |
| `ash_shortbow` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_shortbow_base` |
| `bandage_roll` | `res://icon.svg` | `ui.item.icon.default` | `direct` |
| `bandit_insignia` | `res://icon.svg` | `ui.item.icon.default` | `direct` |
| `beast_hide` | `res://icon.svg` | `ui.item.icon.default` | `direct` |
| `black_crown_core` | `res://icon.svg` | `ui.item.icon.default` | `direct` |
| `black_star_wedge` | `res://icon.svg` | `ui.item.icon.default` | `direct` |
| `blood_debt_shawl` | `res://icon.svg` | `ui.item.icon.default` | `direct` |
| `bronze_sword` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_shortsword_base` |
| `calamity_shard` | `res://icon.svg` | `ui.item.icon.default` | `direct` |
| `compact_hand_crossbow` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_hand_crossbow_base` |
| `curved_scimitar` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_scimitar_base` |
| `dead_road_lantern` | `res://icon.svg` | `ui.item.icon.default` | `direct` |
| `dragon_scale` | `res://icon.svg` | `ui.item.icon.default` | `direct` |
| `duelist_rapier` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_rapier_base` |
| `farmer_sickle` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_sickle_base` |
| `forge_coal` | `res://icon.svg` | `ui.item.icon.default` | `direct` |
| `guard_trident` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_trident_base` |
| `hardwood_lumber` | `res://icon.svg` | `ui.item.icon.default` | `direct` |
| `healing_herb` | `res://icon.svg` | `ui.item.icon.default` | `direct` |
| `hunting_javelin` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_javelin_base` |
| `iron_dagger` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_dagger_base` |
| `iron_flail` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_flail_base` |
| `iron_greatclub` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_greatclub_base` |
| `iron_greatsword` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_greatsword_base` |
| `iron_morningstar` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_morningstar_base` |
| `iron_ore` | `res://icon.svg` | `ui.item.icon.default` | `direct` |
| `iron_scale_mail` | `res://icon.svg` | `ui.item.icon.default` | `direct` |
| `iron_war_pick` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_war_pick_base` |
| `iron_warhammer` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_warhammer_base` |
| `leather_cap` | `res://icon.svg` | `ui.item.icon.default` | `direct` |
| `leather_jerkin` | `res://icon.svg` | `ui.item.icon.default` | `direct` |
| `linen_cloth` | `res://icon.svg` | `ui.item.icon.default` | `direct` |
| `militia_axe` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_handaxe_base` |
| `militia_light_crossbow` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_light_crossbow_base` |
| `militia_spear` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_spear_base` |
| `moonfern_sample` | `res://icon.svg` | `ui.item.icon.default` | `direct` |
| `oak_club` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_club_base` |
| `oak_quarterstaff` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_quarterstaff_base` |
| `raider_greataxe` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_greataxe_base` |
| `reverse_fate_amulet` | `res://icon.svg` | `ui.item.icon.default` | `direct` |
| `scout_charm` | `res://icon.svg` | `ui.item.icon.default` | `direct` |
| `sealed_dispatch` | `res://icon.svg` | `ui.item.icon.default` | `direct` |
| `siege_heavy_crossbow` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_heavy_crossbow_base` |
| `smith_light_hammer` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_light_hammer_base` |
| `soldier_battleaxe` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_battleaxe_base` |
| `soldier_glaive` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_glaive_base` |
| `soldier_pike` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_pike_base` |
| `steel_halberd` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_halberd_base` |
| `steel_longsword` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_longsword_base` |
| `stone_maul` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_maul_base` |
| `torch_bundle` | `res://icon.svg` | `ui.item.icon.default` | `direct` |
| `travel_ration` | `res://icon.svg` | `ui.item.icon.default` | `direct` |
| `watchman_mace` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_mace_base` |
| `weapon_unique_axe_bonecrusher_088` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_greataxe_base` |
| `weapon_unique_axe_butcher_094` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_greataxe_base` |
| `weapon_unique_axe_dragonbone_096` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_greataxe_base` |
| `weapon_unique_axe_echo_095` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_handaxe_base` |
| `weapon_unique_axe_frostbite_097` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_battleaxe_base` |
| `weapon_unique_axe_glutton_090` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_greataxe_base` |
| `weapon_unique_axe_plague_tongue_099` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_battleaxe_base` |
| `weapon_unique_axe_shieldbreaker_098` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_greataxe_base` |
| `weapon_unique_axe_starfragment_100` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_greataxe_base` |
| `weapon_unique_axe_storms_eye_091` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_battleaxe_base` |
| `weapon_unique_axe_thunderfang_086` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_greataxe_base` |
| `weapon_unique_battleaxe_dragon_scale` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_battleaxe_base` |
| `weapon_unique_battleaxe_hunter_382` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_battleaxe_base` |
| `weapon_unique_battleaxe_lumberjack_383` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_battleaxe_base` |
| `weapon_unique_battleaxe_lunareclipse` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_battleaxe_base` |
| `weapon_unique_bow_phoenix_330` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_longbow_base` |
| `weapon_unique_bow_scorpion_339` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_shortbow_base` |
| `weapon_unique_bow_thunderbow_156` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_longbow_base` |
| `weapon_unique_bow_titanbow_173` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_longbow_base` |
| `weapon_unique_bow_windbow_151` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_longbow_base` |
| `weapon_unique_bow_wolf_325` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_longbow_base` |
| `weapon_unique_crossbow_gorgon_329` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_heavy_crossbow_base` |
| `weapon_unique_exotic_umbrella_232` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_rapier_base` |
| `weapon_unique_greataxe_executioner_384` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_greataxe_base` |
| `weapon_unique_greataxe_mountainbreaker` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_greataxe_base` |
| `weapon_unique_greataxe_rustanchor` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_greataxe_base` |
| `weapon_unique_greataxe_void` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_greataxe_base` |
| `weapon_unique_greatsword_giants_heel_024` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_greatsword_base` |
| `weapon_unique_hammer_tremor_102` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_maul_base` |
| `weapon_unique_longsword_courage` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_longsword_base` |
| `weapon_unique_longsword_eternity_edge` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_longsword_base` |
| `weapon_unique_longsword_smiths_regret` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_longsword_base` |
| `weapon_unique_mace_frost_207` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_mace_base` |
| `weapon_unique_morningstar_flame_208` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_morningstar_base` |
| `weapon_unique_morningstar_viper_206` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_morningstar_base` |
| `weapon_unique_polearm_rock_halberd_148` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_halberd_base` |
| `weapon_unique_polearm_spider_spear_136` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_spear_base` |
| `weapon_unique_polearm_thunder_halberd_137` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_halberd_base` |
| `weapon_unique_rapier_memoryeater_vine` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_rapier_base` |
| `weapon_unique_sands_time_480` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_dagger_base` |
| `weapon_unique_shortsword_cowardice` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_shortsword_base` |
| `weapon_unique_sword_double_edged_263` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_longsword_base` |
| `weapon_unique_sword_glory_261` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_longsword_base` |
| `weapon_unique_sword_heartbane_004` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_rapier_base` |
| `weapon_unique_sword_last_lesson_020` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_longsword_base` |
| `weapon_unique_sword_oathscar_003` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_longsword_base` |
| `weapon_unique_sword_ravenplume_017` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_shortsword_base` |
| `weapon_unique_sword_rustoath_006` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_shortsword_base` |
| `weapon_unique_sword_starfell_016` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_greatsword_base` |
| `weapon_unique_sword_threadweaver_019` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_rapier_base` |
| `weapon_unique_sword_twilight_edge_005` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_scimitar_base` |
| `weapon_unique_sword_wyrmbreak_007` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_longsword_base` |
| `weapon_unique_warhammer_sacred_408` | `res://icon.svg` | `ui.item.icon.default` | `inherited:weapon_type_warhammer_base` |
| `whetstone` | `res://icon.svg` | `ui.item.icon.default` | `direct` |
