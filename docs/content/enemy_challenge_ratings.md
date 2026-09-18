# 敌人挑战等级内容基线

更新日期：2026-09-16。

`challenge_rating` 是单只敌人的独立内容评级，用于控制世界区域可出现的怪物。它与 `creature_level`（生物等级，参与生命等派生）分开，不按数量相加，也不修改现有战斗属性或技能。允许非负有限小数；正式 JSON 必须明确配置，缺失值不可由生物等级补齐。

本表是首版人工评级，参考当前模板的生命/防御、行动、攻击与技能组合，尚未经过狼群实战或系统平衡校准。它不是胜率保证：多只 CR ≤ 2 的敌人仍可能构成困难战斗。强控制、范围伤害、回复及高阶技能组合应单独考虑，不能只抄生物等级。

出生点周围 200×200 的主世界区域上限为单怪 CR 2。普通荒狼 CR 1、座狼 CR 2 可以出现；狼王 CR 3、萨满 CR 4、雾沼异兽 CR 4 被排除。巢穴升级和任务生成使用同一限制。运行时不按这些具体模板 ID 写特殊分支。

## 当前配置

| 模板 | 名称 | 生物等级 | 挑战等级 |
|---|---|---:|---:|
| `bandit` | 盗匪 | 1 | 1 |
| `bugbear` | 熊地精 | 3 | 3 |
| `dire_wolf` | 恐狼 | 4 | 3 |
| `ettin` | 双头巨人 | 7 | 6 |
| `fire_elemental` | 火元素 | 6 | 6 |
| `ghoul` | 食尸鬼 | 4 | 3 |
| `giant_bat` | 巨蝠 | 0 | 0.5 |
| `giant_rat` | 巨鼠 | 0 | 0.25 |
| `giant_spider` | 巨蛛 | 3 | 3 |
| `gnoll_hunter` | 豺狼人猎手 | 2 | 2 |
| `goblin_archer` | 哥布林射手 | 1 | 1 |
| `goblin_raider` | 哥布林劫掠者 | 1 | 1 |
| `green_dragon_wyrmling` | 绿龙幼崽 | 5 | 6 |
| `hill_giant` | 山丘巨人 | 8 | 7 |
| `hobgoblin_captain` | 大哥布林队长 | 4 | 4 |
| `kobold_skirmisher` | 狗头人游击兵 | 1 | 0.5 |
| `lizardfolk` | 蜥蜴人 | 2 | 2 |
| `militia` | 民兵 | 1 | 1 |
| `minotaur_charger` | 弥诺陶冲角者 | 6 | 5 |
| `mist_beast` | 雾沼异兽 | 3 | 4 |
| `mist_harrier` | 雾沼猎压者 | 5 | 5 |
| `mist_weaver` | 雾沼织咒者 | 4 | 4 |
| `mummy_guardian` | 木乃伊守卫 | 7 | 6 |
| `ogre_bruiser` | 食人魔 | 5 | 3 |
| `orc_grunt` | 兽人步卒 | 3 | 2 |
| `owlbear` | 鸮熊 | 5 | 4 |
| `red_dragon` | 红龙 | 10 | 10 |
| `skeleton_soldier` | 骷髅卫士 | 2 | 2 |
| `specter` | 怨灵 | 5 | 4 |
| `troll_ravager` | 巨魔撕裂者 | 6 | 5 |
| `white_dragon_wyrmling` | 白龙幼崽 | 4 | 5 |
| `wight_blade` | 亡灵剑士 | 5 | 4 |
| `wolf_alpha` | 荒狼头目 | 4 | 3 |
| `wolf_pack` | 荒狼群 | 0 | 1 |
| `wolf_raider` | 荒狼 | 0 | 1 |
| `wolf_shaman` | 荒狼祭司 | 4 | 4 |
| `wolf_vanguard` | 荒狼先锋 | 4 | 3 |
| `worg` | 座狼 | 1 | 2 |
| `wraith` | 幽魂 | 6 | 6 |
| `zombie_shambler` | 蹒跚腐尸 | 2 | 1 |
