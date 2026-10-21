using System.Collections.Generic;

namespace RollOfTheScout
{
    /// <summary>
    /// 物品预制体名常量（取自游戏 ItemDatabase 的 GameObject 名，已核对）。
    /// </summary>
    public static class ItemNames
    {
        // 开路（先驱）
        public const string RopeSpool = "RopeSpool";           // 绳索
        public const string ClimbingSpike = "ClimbingSpike";   // 岩钉
        public const string ScoutCookies = "ScoutCookies";     // 童军饼干
        public const string ChainShooter = "ChainShooter";     // 锁链发射器
        public const string RescueHook = "RescueHook";         // 救援抓钩
        public const string AntiZooka = "AntiZooka";           // 反重力炮

        // 高代谢·加速：让玩家「饮用」能量饮料即可获得其加速效果（约 10 秒）
        public const string EnergyDrink = "Energy Drink";      // 能量饮料

        // 随身医疗箱
        public const string Bandages = "Bandages";             // 绷带
        public const string MedicinalRoot = "MedicinalRoot";   // 药用根茎
        public const string FirstAidKit = "FirstAidKit";       // 医疗箱
        public const string CureAll = "Cure-All";              // 万灵药
        public const string HealingPuffShroom = "HealingPuffShroom"; // 灵药菇

        // 备战充分
        public const string Backpack = "Backpack";             // 背包
        public const string Jetpack = "Jetpack";               // 喷气背包

        // 刺杀（刺客）
        public const string RitualDagger = "RitualDagger";        // 仪式匕首
        public const string Blowgun = "HealingDart Variant";      // 吹箭筒

        // 潘多拉（潘多拉之子）
        public const string PandorasBox = "PandorasBox";          // 潘多拉魔盒

        // 骸骨（死灵法师）
        public const string BookOfBones = "BookOfBones";          // 骸骨之书

        // 野人（Savage）
        public const string BerrynanaPink = "Berrynana Pink";     // 粉香蕉（不拿拿发放物）
        public const string FireWood = "FireWood";                // 木棍（知识污染替换目标）
        public const string RopeShooter = "RopeShooter";          // 绳索炮（知识污染来源之一）

        // 执旗（旗手）
        public const string CheckpointFlag = "Flag_Plantable_Checkpoint"; // 检查点旗标（可插旗）
        public const string Lollipop = "Lollipop";                        // 大棒棒糖（无限体力）
        public const string FortifiedMilk = "FortifiedMilk";              // 奶白金（无敌）

        // 生产 / 模因污染（待皓）
        public const string NestEgg = "NestEgg";                          // 好大蛋（可砸开，产出煎蛋 Egg / 稀有产出火鸡 EggTurkey）
        public const string Egg = "Egg";                                  // 煎蛋（好大蛋 NestEgg 砸开后产出）

        // 魔法卷轴 / 黑魔法（法师）
        public const string GuidebookScroll = "GuidebookPageScroll Variant"; // 卷轴（使用后变成一张教程纸页）
        public const string WarpFungus = "WarpFungus";                       // 曲迁菇
        public const string Beehive = "Beehive";                             // 蜂巢
        public const string Mandrake = "Mandrake";                           // 曼德拉草
        public const string BugfixOnYou = "BugfixOnYou";                     // 「虫上身」特效预制体（非 0_Items）
        public const string Tornado = "Tornado";                             // 龙卷风（非 0_Items）
        public const string AntiSphere = "AntiSphere";                       // 反重力场（反重力炮开炮产生）
        public const string Beetle = "Beetle";                               // 甲虫（森蕈关卡会追玩家顶玩家，非蜱虫）
        public const string MushroomZombie = "MushroomZombie";               // 蘑菇僵尸
        public const string CursedSkull = "Cursed Skull";                    // 诅咒头骨（使用后会造成诅咒）
        public const string Scoutmaster = "Character_Scoutmaster";           // 童军领队（房间对象）

        // 顶级玩家 / 不屑（挑战者）
        public const string GoldenHook = "RescueHook_Infinite";              // 黄金抓钩（抓钩模式的无限金色救援爪）
        public const string Glider = "Glider";                               // 滑翔翼

        // 自带厨具 / 后勤保障（特级厨师 / 炊事员）
        public const string PortableStove = "PortableStovetopItem";          // 便携烤炉（Silver 物品表：便携火炉）

        // 化妆盒 / 精致 / 化学中毒（自恋狂）
        public const string Sunscreen = "Sunscreen";                         // 防晒喷雾

        // 天灾（法师·黑魔法）：捆绑气球
        public const string BalloonBunch = "BalloonBunch";                   // 一束气球（使用后给玩家绑 3 个气球）

        // 开炮 / 炸膛（炮手）
        public const string ScoutCannonItem = "ScoutCannonItem";             // 童子军大炮
        public const string Dynamite = "Dynamite";                          // 炸药

        // 乐器 / 演奏（乐队成员）
        public const string Bugle = "Bugle";                                 // 小号（通用喇叭）
        public const string BugleScoutmaster = "Bugle_Scoutmaster Variant";  // 童军领队的号角
        public const string BugleMagic = "Bugle_Magic";                      // 友谊喇叭

        // 封印之力（领队·迈尔斯残魂）：童军宝石道具
        public const string AmuletHealing = "Amulet_Healing";                // 童军的毅力
        public const string AmuletSuperJump = "Amulet_SuperJump";            // 童军的进取
        public const string AmuletClone = "Amulet_Clone";                    // 童军的盛情
        public const string AmuletInfiniteStamina = "Amulet_InfiniteStamina";// 童军的野心
        public const string ScoutsHonor = "ScoutsHonor";                     // 童军的荣耀
        public const string MushroomGlow = "Mushroom Glow";                  // 诡异菇
        public const string Flare = "Flare";                                 // 信号棒
        public const string StrangeGem = "Strange Gem";                      // 奇怪水晶
        public const string Fannypack = "Fannypack";                         // 滑稽背包（小丑背包）

        // 区（Qu）
        public const string EarlyWorm = "EarlyWorm";                         // 早起虫儿（可烤制；虫儿音效挂在它的 EarlyWorm 组件上）
        public const string Torch = "Torch";                                 // 火把
        public const string CactusBall = "CactusBall";                       // 仙人球
        public const string MagicBean = "MagicBean";                         // 魔豆
        public const string BounceShroom = "BounceShroom";                   // 弹力菇
        public const string ShelfShroom = "ShelfShroom";                     // 踏板菇
        public const string CloudFungus = "CloudFungus";                     // 云雾菇
        public const string Guidebook = "Guidebook";                         // 旅行指南

        // 香蕉皮（四种）
        public const string BerrynanaPeelPink = "Berrynana Peel Pink Variant";
        public const string BerrynanaPeelBlue = "Berrynana Peel Blue Variant";
        public const string BerrynanaPeelBrown = "Berrynana Peel Brown Variant";
        public const string BerrynanaPeelYellow = "Berrynana Peel Yellow";

        // 有毒蘑菇（三种）
        public const string MushroomLacePoison = "Mushroom Lace Poison";     // 有毒喇叭菇
        public const string MushroomClusterPoison = "Mushroom Cluster Poison"; // 有毒银针菇
        public const string MushroomNormiePoison = "Mushroom Normie Poison"; // 有毒馒头菇

        // 撕下的书页 / 卷轴：真实预制体名统一以 "GuidebookPage" 开头
        // （实测 GuidebookPage_0_Intro / GuidebookPage_3_Revival / GuidebookPage_10_Sleepy /
        //  GuidebookPage_7_BurningSun …，各版本的编号与名称都会变），实际判定按前缀命中，不逐一列举。
        public const string GuidebookPagePrefix = "GuidebookPage";

        // 轻飘飘（太空人）：单只气球 / 一束气球（拾取即消失）
        public const string Balloon = "Balloon";                             // 气球

        // 飞起来（太空人）：拾取物品按概率变成火箭背包 / 喷气背包，并在脚下生成
        public const string Rocketpack = "Rocketpack";                       // 火箭背包

        // 精品大果（榔王）：荔莓（默认取王莓系列：绿王莓 / 紫王莓）
        public const string KingberryGreen = "Kingberry Green";              // 绿王莓（青荔莓）
        public const string KingberryPurple = "Kingberry Purple";            // 紫王莓（紫荔莓）
    }

    /// <summary>带权重的物品候选。</summary>
    public struct WeightedItem
    {
        public string PrefabName;
        public int Weight;

        public WeightedItem(string prefabName, int weight)
        {
            PrefabName = prefabName;
            Weight = weight;
        }
    }

    public static class WeightedPicker
    {
        /// <summary>按权重随机取一个元素；权重全为 0 或列表为空时返回 null。</summary>
        public static string Pick(IList<WeightedItem> pool)
        {
            if (pool == null || pool.Count == 0) return null;

            int total = 0;
            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i].Weight > 0) total += pool[i].Weight;
            }
            if (total <= 0)
            {
                return pool[UnityEngine.Random.Range(0, pool.Count)].PrefabName;
            }

            int roll = UnityEngine.Random.Range(0, total);
            int acc = 0;
            for (int i = 0; i < pool.Count; i++)
            {
                if (pool[i].Weight <= 0) continue;
                acc += pool[i].Weight;
                if (roll < acc) return pool[i].PrefabName;
            }
            return pool[pool.Count - 1].PrefabName;
        }

        /// <summary>
        /// 从候选池中抽取 count 个。每次抽取互相独立（有放回），因此允许抽到重复物品。
        /// </summary>
        public static List<string> PickMany(IList<WeightedItem> pool, int count)
        {
            var result = new List<string>();
            if (pool == null || pool.Count == 0 || count <= 0) return result;

            for (int i = 0; i < count; i++)
            {
                var picked = Pick(pool);
                if (picked == null) break;
                result.Add(picked);
            }
            return result;
        }

        /// <summary>把「英文逗号分隔」的配置字符串解析成名字数组（自动去空格、忽略空项）。</summary>
        public static List<string> ParseList(string raw)
        {
            var list = new List<string>();
            if (string.IsNullOrWhiteSpace(raw)) return list;
            foreach (var part in raw.Split(','))
            {
                var name = part.Trim();
                if (name.Length > 0) list.Add(name);
            }
            return list;
        }

        /// <summary>
        /// 从名字列表里**不重复**地随机取 <paramref name="count"/> 个（洗牌后取前 count）。
        /// <paramref name="count"/> 超过列表长度时取全部；列表为空或 count ≤ 0 时返回空列表。
        /// 用于童军·【行李】「滑稽背包中的 2 个随机物品（不重复）」。
        /// </summary>
        public static List<string> PickUnique(IList<string> names, int count)
        {
            var result = new List<string>();
            if (names == null || names.Count == 0 || count <= 0) return result;

            var copy = new List<string>(names);
            // Fisher–Yates 洗牌：O(n)，避免多次抽取时的重复判定
            for (int i = copy.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                var tmp = copy[i];
                copy[i] = copy[j];
                copy[j] = tmp;
            }

            int take = count < copy.Count ? count : copy.Count;
            for (int i = 0; i < take; i++) result.Add(copy[i]);
            return result;
        }
    }
}