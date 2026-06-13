# 传奇装备套装饰品设计文档（套装 1–5）

> 5 套传奇套装的饰品部分，共 30 件饰品装备，覆盖 cloak / necklace / ring_1 / ring_2 / special_trinket / badge。

---

## 套装一：晨光圣骑士（Dawn Paladin）饰品

---

### 1.5 晨光圣骑士斗篷

```gdscript
item_id = "acc_dawn_paladin_cloak_1"
display_name = "晨光圣骑士斗篷"
description = "一件由晨光丝线与传统骑士披风布料交织而成的金色斗篷，表面不断有微小的金色光点如晨露般闪烁。斗篷的内衬绣有晨曦之手骑士团的十二位殉道骑士的名字，以金线绣就——不是装饰，而是封印，每一个名字都承载着一个灵魂的守护之力。\n\n埃拉里克在末日之战前夜，将自己斗篷上的所有徽章和勋章都取下，只留下了这道晨曦之光的封印。他说："荣誉属于过去，守护属于未来。这件斗篷不需要勋章，因为它本身就是勋章。"他将斗篷与铠甲一同熔铸，但斗篷的布料奇迹般地未被烧毁——晨光丝线保护了它。\n\n据说，当穿戴者在日出时披上这件斗篷，会感受到十二道温暖的目光从背后注视着自己——那是十二位殉道骑士的灵魂，他们依然在守护，只是换了一种方式。斗篷上的金色光点会在危险来临时变得更加明亮，仿佛在提醒穿戴者："黎明即将到来，坚持下去。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "dawn_paladin_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 12000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_dawn_paladin_cloak_1" },
    { attribute_id = "resistance_radiant", mode = "flat", value = 5, source_type = "equipment", source_id = "acc_dawn_paladin_cloak_1" }
]
```

---

### 1.6 晨光圣骑士项链

```gdscript
item_id = "acc_dawn_paladin_necklace_1"
display_name = "晨光圣骑士项链"
description = "一条由秘银链与一颗晨曦水晶串联而成的项链，水晶内部封存着一缕永不消散的晨光——那是埃拉里克在末日火山口捕捉到的第一缕黎明之光。水晶佩戴时，会随着穿戴者的心跳微微脉动，光芒在黑暗中如同一颗微型太阳。\n\n埃拉里克在捕捉这缕晨光时，用自己的身体挡住了火山口的毒烟和热浪。他说："晨光不会主动照进黑暗，必须有人将它带进去。"他将捕捉到的晨光封入水晶，不是为了照明，而是为了"记住"——记住黎明永远会到来，无论黑夜多么漫长。\n\n项链的水晶会在穿戴者陷入绝望时变得更加明亮——那不是魔法，而是埃拉里克的意志在回应。一位后来的圣骑士在日记中写道："当我被围困在地下墓穴中七天七夜，以为再也看不到阳光时，项链的光芒突然变得像正午的太阳一样明亮。那不是光，那是希望。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "dawn_paladin_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "wisdom_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_dawn_paladin_necklace_1" },
    { attribute_id = "max_mana", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_dawn_paladin_necklace_1" }
]
```

---

### 1.7 晨光圣骑士戒指·左

```gdscript
item_id = "acc_dawn_paladin_ring_1_1"
display_name = "晨光圣骑士戒指·黎明之誓"
description = "一枚由圣钢与晨光碎片锻造的精致戒指，戒指表面刻有晨曦之手的骑士誓言："以光之名，守护至暗。"戒指内侧刻着埃拉里克的名字——不是作为拥有者，而是作为誓言的见证人。\n\n这枚戒指是埃拉里克在创建晨曦之手时，用自己的第一滴圣血与晨光碎片一同锻造的。他说："这枚戒指不是我的，它是所有晨曦之手骑士的共同誓言。任何佩戴它的人，都是晨曦之手的一员。"他将戒指传给了骑士团的第一位成员，那位成员又将它传给了下一位——直到末日之战，最后一位佩戴它的骑士在倒下前，用最后的力气将戒指抛向了远方。\n\n戒指会在佩戴者说出誓言时微微发热——那不是魔法，而是历任佩戴者的意志在共鸣。一位后来的寻宝者在废墟中发现这枚戒指时，发现它已经被埋在地下三百年，但依然温暖——仿佛刚刚有人佩戴过它。
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "dawn_paladin_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_dawn_paladin_ring_1_1" },
    { attribute_id = "max_mana", mode = "flat", value = 5, source_type = "equipment", source_id = "acc_dawn_paladin_ring_1_1" }
]
```

---

### 1.8 晨光圣骑士戒指·右

```gdscript
item_id = "acc_dawn_paladin_ring_2_1"
display_name = "晨光圣骑士戒指·守护之护"
description = "一枚由圣钢与守护符文锻造的厚重戒指，戒指表面刻有十二道微型守护符文——每一道符文代表一位殉道骑士的灵魂封印。戒指佩戴时，符文会随危险接近而依次亮起，如同十二盏微弱的灯塔。\n\n这枚戒指是埃拉里克在末日之战前夜，命令骑士团的符文师制作的。他说："如果明天我们都不在了，至少让这十二道符文继续守护后来的人。"符文师花了整夜时间，将十二位骑士的灵魂印记刻入戒指。当最后一道符文完成时，东方的天空正好亮起——那是末日之战前的最后一个黎明。\n\n戒指的十二道符文会在佩戴者受到致命攻击时全部亮起，形成一道金色护盾——那是十二位骑士最后一次并肩作战。一位后来的冒险者在面对一条恶龙时，戒指的符文突然全部亮起，金色的光芒将龙焰偏转了数寸。他在战后写道："那不是护盾，那是十二位骑士的手，推了我一把。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "dawn_paladin_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "saving_throw_wisdom", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_dawn_paladin_ring_2_1" },
    { attribute_id = "max_mana", mode = "flat", value = 5, source_type = "equipment", source_id = "acc_dawn_paladin_ring_2_1" }
]
```

---

### 1.9 晨光圣骑士特殊饰品

```gdscript
item_id = "acc_dawn_paladin_trinket_1"
display_name = "晨光圣骑士晨曦圣徽"
description = "一个由晨曦水晶与圣钢制成的小型圣徽，圣徽上刻有晨曦之手的标志——一轮从地平线升起的太阳，光芒化作剑刃。圣徽佩戴时，会随日出而微微发光，光芒在正午达到最强，在日落时逐渐暗淡，但永远不会完全熄灭。\n\n这个圣徽是埃拉里克在创建晨曦之手时，用末日火山口的熔岩与晨光一同铸造的。他说："这个圣徽不是装饰，它是'黎明的承诺'——无论黑夜多么漫长，太阳终将升起。"他将圣徽放在了骑士团要塞的最高塔楼上，让它成为所有迷失者的灯塔。\n\n圣徽的特殊效果是：在日出后的一小时内，佩戴者及周围 15 尺内所有盟友获得 +1 攻击检定和 +1 豁免检定（黎明的祝福）。且可以通过圣徽「号召」——每日一次，发出一道金色光芒，30 尺内所有恶魔和亡灵须通过 DC14 智慧豁免，失败则 frightened 1 回合（黎明的威压）。
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "dawn_paladin_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "persuasion_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_dawn_paladin_trinket_1" },
    { attribute_id = "religion_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_dawn_paladin_trinket_1" }
]
```

---

### 1.10 晨光圣骑士徽章

```gdscript
item_id = "acc_dawn_paladin_badge_1"
display_name = "晨光圣骑士徽章"
description = "一枚由纯金与晨光碎片锻造的庄严徽章，徽章上刻有晨曦之手的完整纹章——一轮升起的太阳，光芒化作十二把剑，环绕着一座要塞。徽章背面刻着埃拉里克的遗言："当黑暗再次笼罩大地，穿上这套铠甲的人，将成为新的黎明。"\n\n这枚徽章是埃拉里克在末日之战前夜，亲手为每一位骑士打造的。他说："这枚徽章不是荣誉的象征，它是责任的象征。佩戴它的人，必须在黎明到来前守住黑夜。"十二位骑士在出发前，都将这枚徽章别在了心口——那是他们与埃拉里克的最后约定。\n\n徽章会在佩戴者面临道德抉择时微微发热——那不是魔法，而是埃拉里克的意志在提醒。一位后来的圣骑士在面对一个"牺牲一人拯救千人"的抉择时，徽章突然变得滚烫。他说："那不是答案，那是问题——埃拉里克在问我：'你愿意成为新的黎明吗？'"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "dawn_paladin_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 7000
attribute_modifiers = [
    { attribute_id = "intimidation_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_dawn_paladin_badge_1" },
    { attribute_id = "persuasion_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_dawn_paladin_badge_1" }
]
```

---

## 套装二：暗影刺客（Shadow Assassin）饰品

---

### 2.5 暗影刺客斗篷

```gdscript
item_id = "acc_shadow_assassin_cloak_2"
display_name = "暗影刺客斗篷"
description = "一件由夜影蚕丝与阴影布料编织而成的纯黑斗篷，表面覆盖着一层能够吸收光线的微型暗影鳞片——在 candlelight 下，斗篷仿佛是一个不断吞噬周围光芒的黑洞。斗篷的边缘没有流苏，只有无数细密的暗影丝线，在移动时会留下短暂的黑色残影。\n\n维克多在创建无声之刃时，从南方沙漠运来了第一批夜影蚕丝。但他发现普通的夜影蚕丝只能吸收光线，无法吸收声音。他花了三年时间，在沙漠深处的暗影洞穴中研究了一种只在 absolute darkness 中存在的微生物——"寂静菌"。他将这种微生物培养在夜影蚕丝上，创造出了一种能够同时吸收光线和声音的"暗影蚕丝"。\n\n这件斗篷是维克多用第一批暗影蚕丝编织的。他说："声音是敌人，寂静是朋友。这件斗篷不是遮挡身体的，它是遮挡'存在'的——让穿戴者从世界上暂时消失。"他在斗篷完成后，戴着它完成了无声之刃的第一次正式任务：潜入一座城堡，在三十名守卫的注视下取走了一封信，而没有被任何人察觉。
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "shadow_assassin_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 11000
attribute_modifiers = [
    { attribute_id = "stealth_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_shadow_assassin_cloak_2" },
    { attribute_id = "sleight_of_hand_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_shadow_assassin_cloak_2" }
]
```

---

### 2.6 暗影刺客项链

```gdscript
item_id = "acc_shadow_assassin_necklace_2"
display_name = "暗影刺客项链"
description = "一条由暗影丝与一颗微型暗影水晶串联而成的项链，水晶内部不断有黑色的漩涡在旋转——那不是普通的漩涡，而是被压缩的"寂静"，一种能够将周围声音吞噬的微型黑洞。项链佩戴时，水晶会随穿戴者的心跳微微脉动，脉动越慢，周围的寂静越深。\n\n这条项链是维克多的妻子「寂静之声」艾琳娜制作的。艾琳娜原本是一位宫廷歌手，她的歌声能够让听众忘记一切烦恼。但在维克多被通缉后，她发誓永远不再唱歌——"如果声音会带来危险，那我就成为寂静本身"。她将自己的声音封入了一颗暗影水晶，制作成了这条项链。\n\n维克多在艾琳娜去世后，将这条项链戴在了自己的脖子上。他说："她的歌声曾经是世界上最美的声音，现在她的寂静是世上最深的寂静。"项链会在穿戴者需要绝对安静时自动激活——将周围 5 尺内的所有声音吞噬，持续 1 分钟。一位后来的刺客在描述这项链时说："那不是寂静，那是'声音的坟墓'。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "shadow_assassin_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "dexterity_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_shadow_assassin_necklace_2" },
    { attribute_id = "max_mana", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_shadow_assassin_necklace_2" }
]
```

---

### 2.7 暗影刺客戒指·左

```gdscript
item_id = "acc_shadow_assassin_ring_1_2"
display_name = "暗影刺客戒指·寂静"
description = "一枚由暗影金属与寂静菌丝锻造的黑色戒指，戒指表面没有任何装饰——光滑得如同一面镜子，但镜子反射的不是影像，是"无"。戒指佩戴时，会根据周围的噪音水平变化温度——噪音越大，戒指越冷，仿佛在"渴望"寂静。\n\n这枚戒指是维克多为无声之刃的第一位成员「无声者」塞拉斯打造的。塞拉斯原本是一位聋哑人，他听不见声音，也因此无法被任何声音-based 探测发现。维克多说："他的缺陷是他的优势，我要将这优势封入戒指。"他将塞拉斯的"寂静"提取出来，封入了这枚戒指。\n\n塞拉斯在临终前，将戒指还给了维克多，说："寂静不是听不见，而是选择不听。这枚戒指应该属于所有选择寂静的人。"戒指会在佩戴者进行暗杀时自动消除所有声音——包括脚步、呼吸、武器碰撞，持续 1 回合，每日三次。一位后来的刺客说："戴上这枚戒指后，我甚至连自己的心跳都听不见了。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "shadow_assassin_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 7000
attribute_modifiers = [
    { attribute_id = "sleight_of_hand_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_shadow_assassin_ring_1_2" },
    { attribute_id = "max_mana", mode = "flat", value = 5, source_type = "equipment", source_id = "acc_shadow_assassin_ring_1_2" }
]
```

---

### 2.8 暗影刺客戒指·右

```gdscript
item_id = "acc_shadow_assassin_ring_2_2"
display_name = "暗影刺客戒指·无形"
description = "一枚由暗影金属与扭曲水晶锻造的奇异戒指，戒指表面不断有微弱的扭曲波纹——那不是光线折射，而是"存在感"在被吞噬。戒指佩戴时，会根据周围注视的目光数量变化温度——被注视越多，戒指越热，仿佛在"抵抗"被看见。\n\n这枚戒指是维克多为无声之刃的最强成员「影子」莫里安打造的。莫里安的特殊能力不是隐身，而是"存在感稀薄"——他即使站在你面前，你也会下意识地忽略他。维克多花了两年时间研究这种能力，最终发现它与一种叫做"认知滤镜"的心理现象有关。他将莫里安的"稀薄存在感"提取出来，封入了这枚戒指。\n\n莫里安在一次过于自信的暗杀中失败身亡。临终前，他将自己的影子撕下，封入了戒指，说："影子比身体更真实，因为它永远在跟随。让这枚戒指跟随所有需要隐形的人。"戒指会在佩戴者需要隐藏时，自动降低其存在感——30 尺内所有生物对其的感知检定 -3，持续 1 分钟，每日一次。
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "shadow_assassin_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 7000
attribute_modifiers = [
    { attribute_id = "stealth_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_shadow_assassin_ring_2_2" },
    { attribute_id = "max_mana", mode = "flat", value = 5, source_type = "equipment", source_id = "acc_shadow_assassin_ring_2_2" }
]
```

---

### 2.9 暗影刺客特殊饰品

```gdscript
item_id = "acc_shadow_assassin_trinket_2"
display_name = "暗影刺客暗影之匣"
description = "一个由暗影木与寂静菌丝制成的小型匣子，匣子内部有二十七个小格子——每一个格子中都封存着一种不同的暗杀工具：微型匕首、毒针、钢丝、烟雾弹、迷幻粉……匣子表面没有任何锁扣，但它只会在绝对黑暗中打开——有光时，匣子仿佛是一个实心木块。\n\n这个匣子是「毒师」莉莉丝的遗物。莉莉丝在临终前，将自己一生的二十七种暗杀毒素和工具都封入了这个匣子。她说："最好的暗杀者不是杀死目标的人，而是让目标在微笑中死去的人。这个匣子里的每一种工具，都是为了'优雅的死亡'而存在。"\n\n维克多在莉莉丝去世后，将匣子传给了无声之刃的每一任首领。他说："这不是武器库，这是'艺术收藏'——每一件工具都是莉莉丝的一件作品。"匣子的特殊效果是：每日一次，可以从匣子中取出一件工具进行暗杀（选择：毒针：2D6 poison damage 并中毒 3 回合；钢丝：沉默击杀，若目标 HP 低于 20% 则即死；烟雾弹：15 尺半径 heavy obscurement，持续 1 回合）。
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "shadow_assassin_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "sleight_of_hand_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_shadow_assassin_trinket_2" },
    { attribute_id = "stealth_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_shadow_assassin_trinket_2" }
]
```

---

### 2.10 暗影刺客徽章

```gdscript
item_id = "acc_shadow_assassin_badge_2"
display_name = "暗影刺客徽章"
description = "一枚由暗影金属与夜影蚕丝锻造的黑色徽章，徽章上刻有无声之刃的标志——一把没有柄的匕首，悬浮在绝对黑的背景中。徽章的背面刻着维克多的暗语："声音是敌人，寂静是朋友。"\n\n这枚徽章是维克多在创建无声之刃时，为每一位成员亲手打造的。他说："这枚徽章不是荣誉的象征，它是'无声誓言'的见证。佩戴它的人，必须发誓永远不在任务中发出任何不必要的声音。"二十七位成员在加入时，都将自己的一滴血滴入了徽章的熔炉——那是他们与维克多的无声契约。\n\n徽章会在佩戴者违反"无声誓言"时微微刺痛——那不是惩罚，而是二十七位成员的提醒。一位后来的刺客在描述这枚徽章时说："它不是徽章，它是二十七双耳朵——永远在倾听，永远在提醒：'安静。'"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "shadow_assassin_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 6000
attribute_modifiers = [
    { attribute_id = "intimidation_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_shadow_assassin_badge_2" },
    { attribute_id = "stealth_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_shadow_assassin_badge_2" }
]
```

---

## 套装三：霜冻守望者（Frost Warden）饰品

---

### 3.5 霜冻守望者斗篷

```gdscript
item_id = "acc_frost_warden_cloak_3"
display_name = "霜冻守望者斗篷"
description = "一件由霜巨人毛发与玄冰丝编织而成的厚重斗篷，表面覆盖着一层永不融化的薄霜——那不是普通的霜，是永冻堡冰封法阵的"残余"，每一粒冰晶都携带着永恒的寒冷。斗篷的边缘有特殊的"冰晶流苏"——每一根流苏都是一根微型冰柱，在空气中会发出微弱的清脆声响，如同风铃。\n\n索拉娜在创建永冻堡时，用自己的头发与玄冰丝混合编织了这件斗篷。她说："守望者不需要温暖，守望者需要清醒。寒冷让人清醒。"她将斗篷挂在了要塞最高的塔楼上，让它成为守望者的标志——任何在冻原上迷失的人，只要看到塔楼上的白色斗篷，就知道方向。\n\n在霜喉袭击的那个夜晚，索拉娜将斗篷抛给了最近的副官，说："如果我倒下了，至少让这件斗篷继续守望。"副官接过斗篷，发现它在龙焰的逼近下不仅没有融化，反而结出了更多的冰霜——冰封法阵的残余在最后的时刻觉醒了。后来的守望者们说："索拉娜的斗篷还在塔楼上飘扬，它永远不会停止守望。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "frost_warden_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 11000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_frost_warden_cloak_3" },
    { attribute_id = "resistance_cold", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_frost_warden_cloak_3" }
]
```

---

### 3.6 霜冻守望者项链

```gdscript
item_id = "acc_frost_warden_necklace_3"
display_name = "霜冻守望者项链"
description = "一条由玄冰碎片与银链串联而成的项链，项链中央镶嵌着一颗"冰封之心"——一颗从永冻堡最深处的冰窖中取出的核心水晶，内部封存着一滴来自冰川时代的远古之水。项链佩戴时，水晶会随周围的温度变化颜色——越冷越蓝，越热越白。\n\n这条项链是永冻堡的第二任守望者「冰镜」卡莉斯塔制作的。卡莉斯塔是一位来自南方的炼金术士，她为了研究"永恒的寒冷"，独自来到了永冻堡。她在冰窖中待了整整一年，最终从一块万年玄冰中取出了这颗核心水晶。她说："这滴水中蕴含着一万年的寒冷记忆——每一年的冬天都被封存在这一滴水中。"\n\n卡莉斯塔将水晶制成项链后，发现自己的体温开始逐渐降低——不是疾病，而是水晶中的寒冷记忆在与她的身体共鸣。她最终融入了冰窖的墙壁，成为了永冻堡的一部分。后来的守望者们说："卡莉斯塔还在冰窖中，只是她现在变成了冰窖本身——她在用另一种方式守望。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "frost_warden_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 9000
attribute_modifiers = [
    { attribute_id = "constitution_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_frost_warden_necklace_3" },
    { attribute_id = "max_hp", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_frost_warden_necklace_3" }
]
```

---

### 3.7 霜冻守望者戒指·左

```gdscript
item_id = "acc_frost_warden_ring_1_3"
display_name = "霜冻守望者戒指·寒冰"
description = "一枚由玄冰铁与霜巨人心脏碎片锻造的冰冷戒指，戒指表面不断有微弱的白色雾气升起——那不是热气，而是空气中的水分被戒指的寒冷瞬间冻结后形成的微型冰晶。戒指佩戴时，周围的温度会下降约五度，手指会变得苍白但不会被冻伤。\n\n这枚戒指是「冰拳」托里克在一次与霜巨人的战斗中，从对方碎裂的心脏中取出的碎片锻造的。托里克将碎片与自己的玄冰铁拳套一同熔铸，制作成了这枚戒指。他说："敌人的心脏可以成为我们的力量——但不是以仇恨的方式，而是以理解的方式。我理解了寒冷，所以寒冷不再伤害我。"\n\n戒指会在佩戴者受到火焰攻击时自动释放寒气——每日一次，将一次火焰伤害的一半转化为寒冷伤害（冰封中和）。且在寒冷环境中，戒指会让佩戴者的拳头覆盖一层薄冰——徒手攻击附加 1D4 cold damage。托里克说："寒冷不是敌人，它是另一种力量——学会使用它，你就不会再害怕它。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "frost_warden_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 7000
attribute_modifiers = [
    { attribute_id = "attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_frost_warden_ring_1_3" },
    { attribute_id = "max_mana", mode = "flat", value = 5, source_type = "equipment", source_id = "acc_frost_warden_ring_1_3" }
]
```

---

### 3.8 霜冻守望者戒指·右

```gdscript
item_id = "acc_frost_warden_ring_2_3"
display_name = "霜冻守望者戒指·守望"
description = "一枚由玄冰铁与冰封符文锻造的精致戒指，戒指表面刻有微型冰封符文——与永冻堡链甲上的符文同源，但更加精细。戒指佩戴时，符文会随周围的温度变化亮度——越冷越亮，在 absolute zero 附近会发出耀眼的蓝光。\n\n这枚戒指是「雪狐」艾拉在一次暴风雪中，从一块被雷冰封住的陨石中取出的玄冰铁锻造的。艾拉说："这块陨石来自比星星还远的地方，它带来的寒冷不属于这个世界。"她将陨石中的玄冰铁提取出来，与永冻堡的冰封符文结合，制作成了这枚戒指。\n\n戒指的特殊效果是：佩戴者可以「感知」到方圆 1 里内的温度变化——温度异常升高（例如龙焰、火山、大量生物聚集）会被自动感知，方向通过戒指的符文亮度变化指示。艾拉说："守望者的眼睛不应该只盯着前方，它们应该盯着所有方向——包括温度的方向。"且戒指可以让佩戴者在冰面或雪地上行走时不留下任何痕迹。
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "frost_warden_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 7000
attribute_modifiers = [
    { attribute_id = "perception_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_frost_warden_ring_2_3" },
    { attribute_id = "survival_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_frost_warden_ring_2_3" }
]
```

---

### 3.9 霜冻守望者特殊饰品

```gdscript
item_id = "acc_frost_warden_trinket_3"
display_name = "霜冻守望者冰封之心"
description = "一个由万年玄冰与银丝制成的小型心形饰品，心形内部封存着一滴「永恒之泪」——那是索拉娜在冰封法阵启动前，因不舍而流下的最后一滴眼泪。眼泪在离开眼眶的瞬间被冻结，成为了一滴永远不会融化的冰晶。饰品佩戴时，会随穿戴者的情绪变化温度——悲伤时更加寒冷，平静时微微温暖。\n\n这个饰品是索拉娜的副官在要塞被冰封后，从索拉娜的遗体旁取下的。副官说："守望者不应该有眼泪，但索拉娜有——因为她是人，不是冰。这滴眼泪是她最后的人性，也是她最后的温暖。"他将冰封之心带出了永冻堡，成为了守望者传承的象征。\n\n冰封之心的特殊效果是：每日一次，当佩戴者 HP 降至 0 时，自动触发「冰封庇护」——将佩戴者冻结在一块保护性冰块中（HP 锁定在 1，免疫所有伤害，持续 1 回合），然后恢复 2D8 HP。索拉娜说："冰封不是死亡，是等待——等待春天的到来。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "frost_warden_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "constitution_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_frost_warden_trinket_3" },
    { attribute_id = "medicine_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_frost_warden_trinket_3" }
]
```

---

### 3.10 霜冻守望者徽章

```gdscript
item_id = "acc_frost_warden_badge_3"
display_name = "霜冻守望者徽章"
description = "一枚由玄冰铁与霜巨人骨骼碎片锻造的冰冷徽章，徽章上刻有永冻堡的标志——一座被冰封的要塞，上方有一只展翅的冰龙（不是霜喉，而是守望者的守护灵）。徽章背面刻着索拉娜的遗言："如果死亡是不可避免的，那就让死亡成为守护。"\n\n这枚徽章是索拉娜在成为守望者时，亲手为自己锻造的。她说："这枚徽章不是荣誉，它是'守望的誓言'——我发誓永远站在这里，无论风雪多大，无论敌人多强。"她将徽章别在了斗篷上，成为了永冻堡最显眼的标志——在白色的雪原上，那枚银色的徽章如同一只眼睛，永远在注视着北方。\n\n徽章会在佩戴者面临放弃的诱惑时变得更加寒冷——那不是惩罚，而是索拉娜的提醒。一位后来的守望者在极度寒冷中想要放弃岗位时，徽章突然变得像玄冰一样冷，冷得他手指发麻无法取下。他说："那不是寒冷，那是索拉娜的手——她在说：'再坚持一会儿，黎明就要来了。'"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "frost_warden_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 6000
attribute_modifiers = [
    { attribute_id = "intimidation_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_frost_warden_badge_3" },
    { attribute_id = "survival_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_frost_warden_badge_3" }
]
```

---

## 套装四：烈焰法师（Flame Mage）饰品

---

### 4.5 烈焰法师斗篷

```gdscript
item_id = "acc_flame_mage_cloak_4"
display_name = "烈焰法师斗篷"
description = "一件由凤凰灰烬与炎魔丝绸混织而成的深红色斗篷，斗篷表面不断有火焰纹路在流动——那不是装饰，而是真正的火焰被封印在了丝线中。斗篷非常轻盈——因为凤凰灰烬几乎没有重量，但也因为这轻盈，穿戴者在施法时会微微飘起。斗篷的边缘没有流苏，只有不断跳跃的微型火星。\n\n伊格尼斯在被正统法师学院开除后，在火山中冥想了一百天。第一百天，一只垂死的凤凰将它的灰烬赠予了他，自动编织成了这件斗篷。凤凰说："火焰不是毁灭，而是重生。穿上这件斗篷，你就是火焰的使者。"伊格尼斯穿着这件斗篷创建了灰烬之环，招收了第一批学徒。\n\n在学派焚毁的那一天，斗篷上的火焰纹路突然爆发，将整个学派焚毁。伊格尼斯在火焰中微笑着说："看，这就是转化的真谛。"当火焰熄灭时，他的身体化为灰烬，但斗篷完好无损，上面的火焰纹路比以前更加鲜艳——那是凤凰的最后一次涅槃，也是伊格尼斯的最终转化。
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "flame_mage_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "spell_attack_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_flame_mage_cloak_4" },
    { attribute_id = "resistance_fire", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_flame_mage_cloak_4" }
]
```

---

### 4.6 烈焰法师项链

```gdscript
item_id = "acc_flame_mage_necklace_4"
display_name = "烈焰法师项链"
description = "一条由凝固的岩浆链与一颗火焰核心串联而成的项链，火焰核心被封装在一枚耐高温水晶中。水晶内部不断有微型火焰在跳动——那不是普通的火焰，是伊格尼斯在火山冥想时从岩浆池中捕捉到的"第一缕火焰"，据说那是世界上最古老、最纯粹的火焰。\n\n伊格尼斯在捕捉这缕火焰时，用自己的手掌伸入了岩浆池中。他的皮肤瞬间被烧毁，但他的意志比火焰更坚定——他说："火焰不会伤害理解它的人。"他在岩浆中坚持了三秒钟，最终将那缕火焰封入了水晶。他的手掌留下了永久的烧伤疤痕，但他笑着说："这是火焰给我的印记，也是我给火焰的印记。"\n\n项链的火焰核心会在佩戴者施放火焰法术时变得更加明亮——火焰似乎在"回应"佩戴者。一位后来的火焰法师在描述这项链时说："当我戴上它施法时，我感觉不是在操控火焰，而是在与火焰对话——它听懂了我在说什么。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "flame_mage_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "intelligence_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_flame_mage_necklace_4" },
    { attribute_id = "max_mana", mode = "flat", value = 15, source_type = "equipment", source_id = "acc_flame_mage_necklace_4" }
]
```

---

### 4.7 烈焰法师戒指·左

```gdscript
item_id = "acc_flame_mage_ring_1_4"
display_name = "烈焰法师戒指·燃烧"
description = "一枚由凝固的岩浆与火焰符文锻造的粗糙戒指，戒指表面不断有微弱的火焰在跳动——在黑暗中会照亮周围数尺。戒指佩戴时，周围的空气会微微温暖——在寒冷的环境中如同一个小型火炉，但在炎热的环境中不会增加热度（火焰懂得"分寸"）。\n\n这枚戒指是伊格尼斯在火山冥想时，用岩浆池边缘的凝固岩浆亲手雕刻的。他在戒指上刻下了二十七道火焰符文中的第一道——"燃烧"。他说："燃烧是火焰最基本的形态，也是最纯粹的形态。不懂得燃烧的人，不配操控火焰。"他将戒指戴在了左手——"左手是接受的手，它接受火焰的馈赠"。\n\n戒指会在佩戴者施放火焰法术时增强火焰——每日三次，施放的火焰法术伤害 +1D6（燃烧的强化）。且在徒手触碰易燃物时，可以「点燃」—— bonus action，点燃一个不超过 1 立方尺的物体（每日三次）。伊格尼斯说："火焰不是用来毁灭的，它是用来转化的——将黑暗转化为光明，将寒冷转化为温暖，将死亡转化为新生。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "flame_mage_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 6000
attribute_modifiers = [
    { attribute_id = "spell_damage_fire", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_flame_mage_ring_1_4" },
    { attribute_id = "max_mana", mode = "flat", value = 5, source_type = "equipment", source_id = "acc_flame_mage_ring_1_4" }
]
```

---

### 4.8 烈焰法师戒指·右

```gdscript
item_id = "acc_flame_mage_ring_2_4"
display_name = "烈焰法师戒指·转化"
description = "一枚由凤凰骨与火焰符文锻造的奇异戒指，戒指表面刻有二十七道火焰符文中的最后一道——"转化"。戒指呈现出半透明的红色，内部不断有火焰在流转——如同一枚被封印的微型太阳。\n\n这枚戒指是伊格尼斯在学派焚毁前，用凤凰的遗骨制作的最后一件作品。他在戒指上刻下了"转化"符文——那是他毕生追求的终极目标。他说："燃烧只是开始，转化才是终点。不懂得转化的人，永远只是火焰的奴隶。"他将戒指戴在了右手——"右手是给予的手，它将转化的力量传递给世界"。\n\n戒指的特殊效果是：每日一次，可以将一次受到的伤害转化为治疗——当佩戴者受到伤害时，可以激活戒指，将伤害的 50% 转化为 HP 恢复（转化的真谛）。但转化的前提是"接受"——佩戴者必须自愿承受完整的伤害，不能躲避或阻挡。伊格尼斯说："转化的第一步是接受——接受痛苦，接受损失，接受死亡。只有接受了，才能转化。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "flame_mage_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 6000
attribute_modifiers = [
    { attribute_id = "spell_dc_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_flame_mage_ring_2_4" },
    { attribute_id = "max_mana", mode = "flat", value = 5, source_type = "equipment", source_id = "acc_flame_mage_ring_2_4" }
]
```

---

### 4.9 烈焰法师特殊饰品

```gdscript
item_id = "acc_flame_mage_trinket_4"
display_name = "烈焰法师灰烬之瓶"
description = "一个由耐火玻璃与凤凰灰烬制成的小型瓶子，瓶内封存着伊格尼斯的最后灰烬——不是普通的灰烬，是"转化的结晶"，每一粒都蕴含着伊格尼斯毕生的火焰知识。瓶子佩戴时，会随周围的温度变化颜色——寒冷时呈现深红色，温暖时呈现金黄色，炎热时呈现纯白色。\n\n这个瓶子是灰烬之环唯一的幸存者在学派焚毁后，从废墟中收集的。他将伊格尼斯的灰烬小心翼翼地封存起来，说："老师不是死了，他只是转化了——从肉体转化为灰烬，从灰烬转化为知识，从知识转化为永恒。"他将瓶子传承给了后来的火焰法师，作为灰烬之环的最后遗产。\n\n灰烬之瓶的特殊效果是：每日一次，可以从瓶中取出一粒灰烬，将其「转化」为火焰（释放一道 3D10 fire damage 的火球，30 尺射程，15 尺半径）。且在研究火焰知识时，佩戴者获得 advantage（伊格尼斯的智慧仍在指导）。一位后来的法师说："当我将灰烬撒入火焰中时，我仿佛听到了伊格尼斯的声音——'看，这就是转化的真谛。'"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "flame_mage_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 7000
attribute_modifiers = [
    { attribute_id = "arcana_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_flame_mage_trinket_4" },
    { attribute_id = "investigation_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_flame_mage_trinket_4" }
]
```

---

### 4.10 烈焰法师徽章

```gdscript
item_id = "acc_flame_mage_badge_4"
display_name = "烈焰法师徽章"
description = "一枚由凝固的火焰与凤凰灰烬锻造的奇异徽章，徽章上刻有灰烬之环的标志——一团不断旋转的火焰，火焰中心有一只凤凰的眼睛。徽章没有固定的形状——它的边缘不断变化，如同真实的火焰在跳动。\n\n这枚徽章是伊格尼斯在创建灰烬之环时，用火山口的熔岩与凤凰的羽毛一同铸造的。他说："这枚徽章不是荣誉，它是'转化之约'——佩戴它的人，必须发誓用火焰来转化世界，而不是毁灭世界。"第一批学徒在加入时，都将自己的一小缕头发投入了徽章的熔炉——那是他们与火焰的契约。\n\n徽章会在佩戴者使用火焰伤害无辜时剧烈燃烧——那不是魔法，而是契约的惩罚。伊格尼斯说："火焰是最诚实的力量——它不会撒谎，也不会原谅。如果你用火焰来毁灭，火焰就会先毁灭你。"一位后来的火焰法师在不小心烧毁了无辜者的房屋后，徽章在他胸口留下了永久的烧伤疤痕——他说："那不是疤痕，那是火焰的教诲。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "flame_mage_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 5000
attribute_modifiers = [
    { attribute_id = "persuasion_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_flame_mage_badge_4" },
    { attribute_id = "intimidation_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_flame_mage_badge_4" }
]
```

---

## 套装五：大地守护者（Earth Warden）饰品

---

### 5.5 大地守护者斗篷

```gdscript
item_id = "acc_earth_warden_cloak_5"
display_name = "大地守护者斗篷"
description = "一件由远古树皮、藤蔓与地衣编织而成的厚重斗篷，表面覆盖着一层真正的苔藓和微型蕨类植物——它们与斗篷形成了一种奇妙的共生关系，从穿戴者的体温中获取少量的热量，同时释放出清新的氧气。斗篷的重量随季节变化——春天轻盈（新叶萌发），夏天沉重（枝叶茂盛），秋天金黄（落叶纷飞），冬天雪白（霜雪覆盖）。\n\n格朗姆在「石化」前的最后一天，从自己的身上取下了最后一块还能移动的皮肤——那是一块已经半石化的树皮。他将树皮交给了磐石之环的德鲁伊们，说："这是我最后的'移动'，也是我最后的'赠予'。用它编织一件斗篷，让后来的守望者可以穿着我，继续守护大地。"德鲁伊们花了三年时间，将格朗姆的树皮与远古藤蔓编织成了这件斗篷。\n\n斗篷会在穿戴者站立不动时变得更加温暖——那不是体温，而是大地的热量在通过斗篷传递。一位后来的德鲁伊在森林中站立守望了七天七夜，他说："当我穿上这件斗篷时，我感觉不到寒冷和饥饿——大地在通过斗篷喂养我，如同母亲喂养婴儿。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "cloak", "earth_warden_set"]
equipment_slot_ids = ["cloak"]
equipment_type_id = "accessory"
base_price = 10000
attribute_modifiers = [
    { attribute_id = "armor_ac_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_earth_warden_cloak_5" },
    { attribute_id = "nature_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_earth_warden_cloak_5" }
]
```

---

### 5.6 大地守护者项链

```gdscript
item_id = "acc_earth_warden_necklace_5"
display_name = "大地守护者项链"
description = "一条由远古石片与藤蔓串联而成的项链，项链中央镶嵌着一颗「地心之心」——一颗从世界最深处的地核中取出的微型水晶，内部封存着一滴来自地球诞生之初的原始岩浆。项链佩戴时，水晶会随大地的脉动微微发光——脉动与地震、火山爆发、板块运动同步。\n\n这条项链是磐石之环的第二位大德鲁伊「地语者」塞拉菲娜制作的。塞拉菲娜是一位能够与大地对话的奇特德鲁伊，她声称能够"听到"岩石的"声音"。她花了十年时间，从世界的十二座最古老山脉的核心各取出一小块岩石，将它们研磨成粉末，混合后熔铸成了这颗地心之心。\n\n塞拉菲娜在制作完成后的第二天，将自己埋入了大地——不是死亡，而是"回归"。她说："我来自大地，现在我要回到大地。但我的知识会留在这颗心中，永远与大地的脉动同步。"后来的德鲁伊们发现，佩戴这条项链时，能够感知到方圆数里内的地下震动和自然灾害的预兆。
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "necklace", "earth_warden_set"]
equipment_slot_ids = ["necklace"]
equipment_type_id = "accessory"
base_price = 8000
attribute_modifiers = [
    { attribute_id = "wisdom_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_earth_warden_necklace_5" },
    { attribute_id = "max_mana", mode = "flat", value = 10, source_type = "equipment", source_id = "acc_earth_warden_necklace_5" }
]
```

---

### 5.7 大地守护者戒指·左

```gdscript
item_id = "acc_earth_warden_ring_1_5"
display_name = "大地守护者戒指·磐石"
description = "一枚由整块花岗岩雕刻而成的厚重戒指，戒指表面没有任何装饰——只有自然形成的石英结晶和苔藓斑点。戒指非常沉重——比普通戒指重十倍，因为它的材质是真正的岩石。佩戴时，手指会被岩石的冰冷触感包围，但不会被磨伤——戒指内侧有一层由格朗姆的树皮粉末制成的天然缓冲层。\n\n这枚戒指是「碎岩者」布隆在一次与山岭巨人的决斗中，从对方碎裂的膝盖骨中取出的碎片雕刻的。布隆将碎片与自己的拳套一同熔铸，制作成了这枚戒指。他说："敌人的骨头可以成为我们的力量——不是以征服的方式，而是以理解的方式。我理解了岩石，所以岩石不再阻挡我。"\n\n戒指会在佩戴者面对物理攻击时变得更加坚硬——每日一次，可以完全吸收一次 bludgeoning damage（磐石之坚）。且在进行力量检定时，检定 +2（大地的力量）。布隆说："力量不是来自肌肉，而是来自大地——当你站在大地上时，你站在了整个世界的力量之上。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_1", "earth_warden_set"]
equipment_slot_ids = ["ring_1"]
equipment_type_id = "accessory"
base_price = 6000
attribute_modifiers = [
    { attribute_id = "strength_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_earth_warden_ring_1_5" },
    { attribute_id = "max_mana", mode = "flat", value = 5, source_type = "equipment", source_id = "acc_earth_warden_ring_1_5" }
]
```

---

### 5.8 大地守护者戒指·右

```gdscript
item_id = "acc_earth_warden_ring_2_5"
display_name = "大地守护者戒指·扎根"
description = "一枚由远古树根与地心水晶锻造的有机戒指，戒指表面有类似根系的纹理——纹理会随时间生长，每天长出约 0.01 毫米的新纹路。戒指佩戴时，会根据周围土壤的肥力变化颜色——肥沃时深棕色，贫瘠时灰白色，有毒时黑色。\n\n这枚戒指是「扎根者」塔拉在"扎根"前，从自己最喜欢的古树上取下的一截树根制作的。塔拉说："这棵树已经站在这里一千年了，它的根比任何城墙都深。我要将它的一小部分带走，让它继续陪伴我。"她将树根与自己的地心水晶结合，制作成了这枚戒指。\n\n戒指的特殊效果是：当佩戴者站立不动时，戒指会让其双脚与大地产生"共鸣"——免疫被击倒（prone）、推离（shove）和强制移动效果（扎根之力）。且在站立不动时，AC +1（大地之护）。塔拉说："根不需要移动，只需要向下。当你扎根时，你就成为了大地的一部分——没有什么能够移动大地。"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "ring_2", "earth_warden_set"]
equipment_slot_ids = ["ring_2"]
equipment_type_id = "accessory"
base_price = 6000
attribute_modifiers = [
    { attribute_id = "constitution_bonus", mode = "flat", value = 1, source_type = "equipment", source_id = "acc_earth_warden_ring_2_5" },
    { attribute_id = "max_mana", mode = "flat", value = 5, source_type = "equipment", source_id = "acc_earth_warden_ring_2_5" }
]
```

---

### 5.9 大地守护者特殊饰品

```gdscript
item_id = "acc_earth_warden_trinket_5"
display_name = "大地守护者种子袋"
description = "一个由远古树皮与藤蔓制成的小型种子袋，种子袋内永远有各种魔法种子——不是普通的种子，是「大地之种」，每一粒都蕴含着生命的全部信息，可以在任何环境中瞬间生长。种子袋表面覆盖着一层苔藓，苔藓会随季节变化颜色——春天嫩绿，夏天深绿，秋天金黄，冬天雪白。\n\n这个种子袋是塞拉菲娜在"回归"大地前，将自己收集的所有种子都封入了这个袋子。她说："生命是大地的语言，种子是生命的字母。这个袋子里的每一粒种子，都是大地的一句话。"她将袋子交给了磐石之环的下一任大德鲁伊，说："当你需要大地的帮助时，就撒下一粒种子——大地会回应你。"\n\n种子袋的特殊效果是：每日一次，可以撒下一粒种子，让它瞬间生长（选择：荆棘藤蔓：5 尺半径困难地形，持续 1 分钟；治疗之花：5 尺半径内所有盟友恢复 2D8 HP；庇护之树：5 尺内出现一棵大树，提供 full cover，持续 1 分钟）。一位后来的德鲁伊说："当我撒下种子时，我仿佛听到了塞拉菲娜的声音——'大地永远不会拒绝真诚的请求。'"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "special_trinket", "earth_warden_set"]
equipment_slot_ids = ["special_trinket"]
equipment_type_id = "accessory"
base_price = 7000
attribute_modifiers = [
    { attribute_id = "nature_bonus", mode = "flat", value = 3, source_type = "equipment", source_id = "acc_earth_warden_trinket_5" },
    { attribute_id = "animal_handling_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_earth_warden_trinket_5" }
]
```

---

### 5.10 大地守护者徽章

```gdscript
item_id = "acc_earth_warden_badge_5"
display_name = "大地守护者徽章"
description = "一枚由远古花岗岩与格朗姆的树皮粉末锻造的厚重徽章，徽章上刻有磐石之环的标志——一棵从岩石中生长出来的古树，树根深深扎入大地，树冠触及天空。徽章背面刻着格朗姆的最后话语："我将成为大地的一部分，大地将成为我的铠甲。"\n\n这枚徽章是格朗姆在「石化」前的最后一天，亲手为自己打造的。他说："这枚徽章不是荣誉，它是'守护的誓言'——我发誓永远站在这里，无论风雨多大，无论敌人多强，我都将与大地同在。"他将徽章别在了胸口——那是他最后一件还能佩戴的饰品，因为身体的其他部分已经开始石化。\n\n徽章会在佩戴者离开大地（例如在船上、飞行中、悬空）时变得更加沉重——那不是魔法，而是格朗姆的提醒。一位后来的德鲁伊在被迫乘船渡海时，徽章变得像石头一样重，压得他几乎无法站立。他说："那不是惩罚，那是格朗姆在提醒我——'不要忘记，你来自大地，终将回归大地。'"
icon = ""
is_stackable = false
max_stack = 1
item_category = "equipment"
tags = ["accessory", "badge", "earth_warden_set"]
equipment_slot_ids = ["badge"]
equipment_type_id = "accessory"
base_price = 5000
attribute_modifiers = [
    { attribute_id = "persuasion_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_earth_warden_badge_5" },
    { attribute_id = "nature_bonus", mode = "flat", value = 2, source_type = "equipment", source_id = "acc_earth_warden_badge_5" }
]
```

---

*套装 1–5 饰品部分完结 · 共 30 件饰品装备*
