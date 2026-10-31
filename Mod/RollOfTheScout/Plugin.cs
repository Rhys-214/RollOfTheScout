using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using RollOfTheScout.Patches;
using Photon.Pun;
using UnityEngine;

namespace RollOfTheScout
{
    /// <summary>
    /// 方案 B「仅主机安装即可游玩」职业模组入口。
    /// 主机执行完整职业逻辑；装有主模组的客机另通过共用组件处理本机残魂宝石结算。
    /// </summary>
    [BepInPlugin(Guid, "RollOfTheScout", "1.1.0")]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.peakmodding.RollOfTheScout";

        internal static ManualLogSource Log;

        /// <summary>本模组的 Harmony 实例（第三方兼容层也需要它来挂补丁）。</summary>
        internal static Harmony HarmonyInstance;

        private Harmony _harmony;

        /// <summary>
        /// 当前配置文件结构的版本标记。**每次改动 Section / Key 结构时都要改这个值**：
        /// 标记不一致时，旧 cfg 会被备份（.old-时间戳）并删除，由 BepInEx 重新生成默认文件。
        /// </summary>
        private const string ConfigRevision = "2026-10-31-halloween-hat-1";

        /// <summary>
        /// 自动清理「旧结构」配置文件（在第一次 Bind 之前调用才有效：BepInEx 的 ConfigFile 是惰性加载的）。
        /// 备份而非直接丢弃，便于用户对照旧值。
        /// </summary>
        private static void ResetOutdatedConfig(BepInEx.Configuration.ConfigFile config)
        {
            try
            {
                string path = config != null ? config.ConfigFilePath : null;
                if (string.IsNullOrEmpty(path)) return;

                string marker = path + ".rev";
                string rev = System.IO.File.Exists(marker)
                    ? System.IO.File.ReadAllText(marker).Trim()
                    : null;

                if (!System.IO.File.Exists(path))
                {
                    // 首次运行：先写标记，避免下次启动把刚生成的新配置当成旧配置删掉。
                    System.IO.File.WriteAllText(marker, ConfigRevision);
                    return;
                }

                if (string.Equals(rev, ConfigRevision, System.StringComparison.Ordinal)) return;

                string backup = path + ".old-" + System.DateTime.Now.ToString("yyyyMMdd-HHmmss");
                System.IO.File.Move(path, backup);
                System.IO.File.WriteAllText(marker, ConfigRevision);
                Log.LogWarning(
                    $"[RollOfTheScout] 配置文件结构已更新，旧配置已备份为 {System.IO.Path.GetFileName(backup)}，"
                    + "并将重新生成包含固定中英双语键名 / 注释的默认配置。");
            }
            catch (System.Exception e)
            {
                // 清理失败不影响启动：BepInEx 仍会以旧文件运行（新键自动补默认值）。
                Log.LogWarning($"[RollOfTheScout] 检查旧配置文件失败（已忽略）：{e.Message}");
            }
        }

        private void Awake()
        {
            Log = Logger;
            // 配置项已改为**固定中英双语**（板块 / 键 / 注释）。旧版 cfg 的键完全不同，直接沿用会读到一堆
            // 无意义条目、且新键全部缺失 —— 因此在首次 Bind **之前**做一次版本检查：不匹配就备份并删除旧文件，
            // 让 BepInEx 重新生成默认配置（详见 ResetOutdatedConfig）。
            ResetOutdatedConfig(Config);

            // 配置项为固定中英双语（见 ConfigLocalization）；绑定到插件自身的 Config，ModConfig 才能读到。
            ModConfig.Initialize(Config);

            // 驱动器：主机执行完整职业逻辑；客机模式另由共用组件处理本机残魂宝石结算。
            gameObject.AddComponent<HostClassManager>();
            var milesLocal = gameObject.AddComponent<RollOfTheScout.Shared.MilesSoulLocal>();
            milesLocal.Configure(
                msg => Log.LogInfo($"[RollOfTheScout] {msg}"),
                msg => Log.LogWarning($"[RollOfTheScout] {msg}"));

            _harmony = new Harmony(Guid);
            HarmonyInstance = _harmony;

            // 关卡切换
            var lightRpc = AccessTools.Method(typeof(Campfire), "Light_Rpc");
            TryPatch("Campfire.Light_Rpc", lightRpc,
                postfix: AccessTools.Method(typeof(CampfireLightPatch), nameof(CampfireLightPatch.Postfix)));

            // 古老雕像使用事件。
            // 必须挂 Prefix：RespawnChest.SpawnItems 内部会先复活所有死亡玩家，
            // Prefix 才能在复活前统计到真实的死亡人数（见 RespawnChestPatch）。
            var spawnItems = AccessTools.Method(typeof(RespawnChest), "SpawnItems",
                new[] { typeof(List<Transform>) });
            TryPatch("RespawnChest.SpawnItems", spawnItems,
                prefix: AccessTools.Method(typeof(RespawnChestPatch), nameof(RespawnChestPatch.Prefix)));

            // 「复活继承」（领袖 / 后勤 / 挑战者·极限）的确定性通道：挂原版 Character.ReviveCharacter(bool)。
            // 该方法由 RPCA_ReviveAtPosition 在每个客户端（含主机）本地调用，因此**任何**复活来源
            // （童军雕像 / 古老雕像 / 骸骨之书 / 篝火与虚空复活 / 中途加入者首次复活，乃至「仅昏迷倒地被雕像复活」）
            // 都能被覆盖；非 [PunRPC] 方法，签名不会被 PEAKERRpcInfo 改写，故用宽松解析取「首参为 bool」的重载。
            var reviveCharacter = ResolveMethod(typeof(Character), "ReviveCharacter", typeof(bool));
            TryPatch("Character.ReviveCharacter (复活继承·确定性通道)", reviveCharacter,
                prefix: AccessTools.Method(typeof(ReviveCharacterPatch), nameof(ReviveCharacterPatch.Prefix)),
                postfix: AccessTools.Method(typeof(ReviveCharacterPatch), nameof(ReviveCharacterPatch.Postfix)));

            // 背人状态自愈（主机侧兜底）：修复「被背起后苏醒/死亡仍吸附在背人者身上」。
            // 原版 CharacterCarrying.Update 的自动放下只由**背人者的拥有者客户端**执行
            // （条件带 refs.view.IsMine），且死亡 / 复活都不会清理 isCarried / carrier；
            // 这里让主机在那一步没执行时代为广播原版 RPCA_Drop（客机零安装也能被修复）。
            var carryingUpdate = AccessTools.Method(typeof(CharacterCarrying), "Update");
            TryPatch("CharacterCarrying.Update (背人状态自愈)", carryingUpdate,
                postfix: AccessTools.Method(typeof(CarryReleaseWatchdogPatch), nameof(CarryReleaseWatchdogPatch.Postfix)));

            // 刺客·刺杀：获得物品按概率替换为仪式匕首。
            // 拦截 Player.AddItem —— 物品真正进入玩家背包/物品栏的唯一漏斗；
            // 拾取时由主机执行（Item.RequestPickup 内部调用），只在主机生效（见 AssassinPickupPatch）。
            var addItem = AccessTools.Method(typeof(Player), "AddItem");
            TryPatch("Player.AddItem", addItem,
                prefix: AccessTools.Method(typeof(AssassinPickupPatch), nameof(AssassinPickupPatch.Prefix)));

            // 野人·知识污染：拾取岩钉 / 绳索炮 / 锁链发射器 / 救援抓钩 / 反重力炮时变成木棍。
            // 与刺客·刺杀共用同一拦截点 Player.AddItem（Harmony 支持多 Prefix；玩家单一职业，不冲突）。
            TryPatch("Player.AddItem (野人·知识污染)", addItem,
                prefix: AccessTools.Method(typeof(SavagePickupPatch), nameof(SavagePickupPatch.Prefix)));

            // 死灵法师·骸骨：拾取潘多拉魔盒时直接变成骸骨之书。
            // 同样共用 Player.AddItem 拦截点（Harmony 支持多 Prefix；玩家单一职业，不冲突）。
            TryPatch("Player.AddItem (死灵法师·骸骨·魔盒变书)", addItem,
                prefix: AccessTools.Method(typeof(NecromancerPickupPatch), nameof(NecromancerPickupPatch.Prefix)));

            // 石像鬼·石化改挂 Item.RequestPickup（见下方与潘多拉共用的拦截点）：
            // 本轮调整后石头在多数分支需要「直接消失」，只有 RequestPickup 能拿到世界物品实例并销毁。

            // 程序员·【溢出】：把「额外体力超过上限」的结果改为取模。
            // 挂原版 Character.AddExtraStamina(float)（非 [PunRPC]、签名稳定）。
            // 该方法受 view.IsMine 保护，只在拥有者客户端执行 —— 覆盖「主机自己的角色」；
            // 「主机下发给任意玩家的增量」由 HostApi.ExtraStaminaAdjust 钩子覆盖（见 ProgrammerOverflowPatch 注释）。
            var addExtraStamina = ResolveMethod(typeof(Character), "AddExtraStamina", typeof(float));
            TryPatch("Character.AddExtraStamina (程序员·溢出)", addExtraStamina,
                prefix: AccessTools.Method(typeof(ProgrammerOverflowPatch), nameof(ProgrammerOverflowPatch.Prefix)));

            // 潘多拉之子·潘多拉：拾取物品 50% 变潘多拉魔盒 / 50% 直接消失。
            // 拦截 Item.RequestPickup —— [PunRPC]，拾取者以 RpcTarget.MasterClient 调用，只在主机执行。
            // 与 Item.Consume 同理，PEAKERRpcInfo 会给 [PunRPC] 方法追加 PhotonMessageInfo 参数，
            // 精确签名匹配会失败，故同样用宽松解析（按名 + 首参 PhotonView）。
            var requestPickup = ResolveItemRequestPickup();
            if (requestPickup == null)
            {
                Log.LogWarning("[RollOfTheScout] 未找到 Item.RequestPickup，潘多拉·拾取效果将不可用。");
            }
            else
            {
                TryPatch("Item.RequestPickup", requestPickup,
                    prefix: AccessTools.Method(typeof(PandoraPickupPatch), nameof(PandoraPickupPatch.Prefix)));

                // 圣人·完美主义：拾取物品按概率直接消失。
                // 与潘多拉之子共用同一拦截点 Item.RequestPickup（Harmony 支持多 Prefix；玩家单一职业，不冲突）。
                TryPatch("Item.RequestPickup (圣人·完美主义)", requestPickup,
                    prefix: AccessTools.Method(typeof(SaintPickupPatch), nameof(SaintPickupPatch.Prefix)));

                // 挑战者·不屑：拾取潘多拉魔盒时物品直接消失。
                // 同样共用 Item.RequestPickup 拦截点（Harmony 支持多 Prefix；玩家单一职业，不冲突）。
                TryPatch("Item.RequestPickup (挑战者·不屑)", requestPickup,
                    prefix: AccessTools.Method(typeof(ChallengerDisdainPickupPatch), nameof(ChallengerDisdainPickupPatch.Prefix)));

                // 领队·迈尔斯残魂·封印之力：除特殊物品外，拾取物品直接消失。
                // 同样共用 Item.RequestPickup 拦截点（Harmony 支持多 Prefix；玩家单一职业，不冲突）。
                TryPatch("Item.RequestPickup (迈尔斯残魂·封印之力)", requestPickup,
                    prefix: AccessTools.Method(typeof(MilesPickupPatch), nameof(MilesPickupPatch.Prefix)));

                // 区·分解者：拾取「分解者物品池」中的物品时，物品直接消失并使自身饥饿下降。
                // 同样共用 Item.RequestPickup 拦截点（Harmony 支持多 Prefix；玩家单一职业，不冲突）。
                TryPatch("Item.RequestPickup (区·分解者)", requestPickup,
                    prefix: AccessTools.Method(typeof(QuDecomposerPickupPatch), nameof(QuDecomposerPickupPatch.Prefix)));

                // 太空人·轻飘飘 / 飞起来：拾取气球直接消失；拾取物品按概率变为火箭背包 / 喷气背包。
                // 同样共用 Item.RequestPickup 拦截点（Harmony 支持多 Prefix；玩家单一职业，不冲突）。
                TryPatch("Item.RequestPickup (太空人·轻飘飘/飞起来)", requestPickup,
                    prefix: AccessTools.Method(typeof(AstronautPickupPatch), nameof(AstronautPickupPatch.Prefix)));

                // 混乱末影人·混乱：拾取物品按概率直接消失，并强制对自己使用一次潘多拉餐盒。
                // 同样共用 Item.RequestPickup 拦截点（Harmony 支持多 Prefix；玩家单一职业，不冲突）。
                TryPatch("Item.RequestPickup (混乱末影人·混乱)", requestPickup,
                    prefix: AccessTools.Method(typeof(ChaosEndermanPickupPatch), nameof(ChaosEndermanPickupPatch.Prefix)));

                // 石像鬼·石化：食物变石头；拾取石头按概率消失并结算收益 / 化雕像。
                // 必须挂 RequestPickup（而非 Player.AddItem），因为需要销毁世界物品。
                TryPatch("Item.RequestPickup (石像鬼·石化)", requestPickup,
                    prefix: AccessTools.Method(typeof(GargoylePickupPatch), nameof(GargoylePickupPatch.Prefix)));

                // 炼金术士·等价交换 / 能量汲取：工具转化为随机物品、可食用物品直接销毁。
                // 同样必须挂 RequestPickup：两者都要销毁世界物品。
                TryPatch("Item.RequestPickup (炼金术士·等价交换/能量汲取)", requestPickup,
                    prefix: AccessTools.Method(typeof(AlchemistPickupPatch), nameof(AlchemistPickupPatch.Prefix)));

                // 捣蛋鬼·拾取变糖：可食用物品按概率替换为随机糖果（销毁原物品后发放糖果）。
                // 同样共用 Item.RequestPickup 拦截点（Harmony 支持多 Prefix；玩家单一职业，不冲突）。
                TryPatch("Item.RequestPickup (捣蛋鬼·拾取变糖)", requestPickup,
                    prefix: AccessTools.Method(typeof(TricksterPickupPatch), nameof(TricksterPickupPatch.Prefix)));
            }

            // 混乱末影人·末影：直接丢弃曲迁菇 → 清除；蓄力扔出 → 正常生效。
            // 两者靠原版 RPC_SetThrownData 的蓄力值区分（主机可判定，客机零安装）。
            // 按名解析（与 Consume 同理：运行时 [PunRPC] 方法可能被 PEAKERRpcInfo 重写）。
            var rpcSetThrownData = AccessTools.Method(typeof(Item), "RPC_SetThrownData");
            if (rpcSetThrownData == null)
            {
                Log.LogWarning("[RollOfTheScout] 未找到 Item.RPC_SetThrownData，混乱末影人·末影的「丢弃清除」效果将不生效。");
            }
            else
            {
                TryPatch("Item.RPC_SetThrownData (混乱末影人·末影)", rpcSetThrownData,
                    prefix: AccessTools.Method(typeof(ChaosEndermanDropClearPatch), nameof(ChaosEndermanDropClearPatch.Prefix)));
            }

            // 转职：食用指定物品（默认诡异菇）后更换职业。
            // 拦截 Item.Consume —— [PunRPC]，食用者以 RpcTarget.All 广播，主机也收得到（见 ReclassConsumePatch）。
            //
            // 注意：不能用 AccessTools.Method(typeof(Item), "Consume", new[]{ typeof(int) }) 精确匹配 ——
            // 游戏运行时 Assembly-CSharp 会被预加载补丁（PEAKERRpcInfo）重写，[PunRPC] 方法的签名
            // 可能被改动，实测该精确匹配会返回 null（日志：未找到目标方法，跳过补丁：Item.Consume）。
            // 因此这里改用宽松解析（按名 + 首个参数为 int），并在失败时依赖 GlobalEvents.OnItemConsumed 兜底。
            var consume = ResolveItemConsume();
            if (consume == null)
            {
                Log.LogWarning(
                    "[RollOfTheScout] 未找到 Item.Consume，转职、野人·野性未泯与待皓·模因污染将只依赖 GlobalEvents.OnItemConsumed 兜底通道。");
            }
            else
            {
                TryPatch("Item.Consume", consume,
                    prefix: AccessTools.Method(typeof(ReclassConsumePatch), nameof(ReclassConsumePatch.Prefix)));

                // 野人·野性未泯：食用包装食物 / 烤过的食物时增加损伤。
                // 与转职共用同一拦截点 Item.Consume（Harmony 支持多 Prefix；玩家单一职业，不冲突）。
                TryPatch("Item.Consume (野人·野性未泯)", consume,
                    prefix: AccessTools.Method(typeof(SavageEatPatch), nameof(SavageEatPatch.Prefix)));

                // 待皓·模因污染：本局存在待皓职业玩家时，其他玩家食用触发物品（好大蛋砸出的煎蛋 Egg）直接转职成待皓。
                // 同样共用 Item.Consume（Harmony 支持多 Prefix；玩家单一职业，不冲突）。
                TryPatch("Item.Consume (待皓·模因污染)", consume,
                    prefix: AccessTools.Method(typeof(MemeConsumePatch), nameof(MemeConsumePatch.Prefix)));

                // 太空人·太空食品安全：食用「包装食物 / 烤熟的食物」以外的食物时 +中毒。
                // 同样共用 Item.Consume（Harmony 支持多 Prefix；玩家单一职业，不冲突）。
                TryPatch("Item.Consume (太空人·太空食品安全)", consume,
                    prefix: AccessTools.Method(typeof(AstronautEatPatch), nameof(AstronautEatPatch.Prefix)));

                // 榔王：精品大果 / 上瘾 / 纤维化 / 没吃的赶紧戒。
                // 同样共用 Item.Consume（Harmony 支持多 Prefix；玩家单一职业，不冲突）。
                TryPatch("Item.Consume (榔王)", consume,
                    prefix: AccessTools.Method(typeof(LangwangEatPatch), nameof(LangwangEatPatch.Prefix)));

                // 混乱末影人·末影：食用运动饮料 / 万灵药后 +损伤。
                // 同样共用 Item.Consume（Harmony 支持多 Prefix；玩家单一职业，不冲突）。
                TryPatch("Item.Consume (混乱末影人·末影)", consume,
                    prefix: AccessTools.Method(typeof(ChaosEndermanEatPatch), nameof(ChaosEndermanEatPatch.Prefix)));

                // 捣蛋鬼·蛀牙：食用棉花糖 / 大棒棒糖后 +损伤。
                // 同样共用 Item.Consume（Harmony 支持多 Prefix；玩家单一职业，不冲突）。
                TryPatch("Item.Consume (捣蛋鬼·蛀牙)", consume,
                    prefix: AccessTools.Method(typeof(TricksterEatPatch), nameof(TricksterEatPatch.Prefix)));
            }

            // 旗手·执旗：检测「检查点旗标被插下」。
            // 拦 CheckpointFlag.SetColor —— 插旗者客户端在 CheckpointFlag.Initialize 末尾以
            // RpcTarget.AllBuffered 广播，主机也收得到；插旗者身份由旗标的 PhotonView.Owner 反查。
            // 同样用宽松解析：PEAKERRpcInfo 会给 [PunRPC] 方法追加 PhotonMessageInfo。
            var setFlagColor = ResolveMethod(typeof(CheckpointFlag), "SetColor", typeof(Vector3));
            TryPatch("CheckpointFlag.SetColor", setFlagColor,
                postfix: AccessTools.Method(typeof(CheckpointFlagPatch), nameof(CheckpointFlagPatch.Postfix)));

            // 舞王·舞动青春 / 全场欢呼：感知「谁做了表情 / 跳舞」。
            // 拦 CharacterAnimations.RPCA_PlayRemove —— [PunRPC]，做表情的客户端以 RpcTarget.All 广播，
            // 主机也收得到；同一 RPC 也用于「主机强制任意玩家跳舞」。同样用宽松解析。
            var playEmote = ResolveMethod(typeof(CharacterAnimations), "RPCA_PlayRemove", typeof(string));
            TryPatch("CharacterAnimations.RPCA_PlayRemove", playEmote,
                postfix: AccessTools.Method(typeof(EmotePatch), nameof(EmotePatch.Postfix)));

            // 挑战者·不屑：抓钩（黄金抓钩 / 救援抓钩）钩到玩家时，持钩的挑战者立刻灌满困倦（直接昏迷）。
            // 拦 RescueHook.RPCA_RescueCharacter —— [PunRPC]，钩到角色时以 RpcTarget.All 广播，主机也收得到；
            // 钩到墙壁走 RPCA_RescueWall（另一 RPC），不会误触发。同样用宽松解析（按名 + 首参 PhotonView）。
            var rescueChar = ResolveMethod(typeof(RescueHook), "RPCA_RescueCharacter", typeof(PhotonView));
            TryPatch("RescueHook.RPCA_RescueCharacter (挑战者·不屑)", rescueChar,
                postfix: AccessTools.Method(typeof(ChallengerHookPatch), nameof(ChallengerHookPatch.Postfix)));

            // 黑魔法·天灾兜底：关卡内无 TornadoSpawner（无 TornadoPoints 目标点）时，
            // 龙卷风仍会生成，但 targetParent 为空，需跳过原版 PickTarget，否则每帧 NullReferenceException。
            var tornadoPickTarget = ResolveMethod(typeof(Tornado), "PickTarget", null);
            TryPatch("Tornado.PickTarget (法师·天灾兜底)", tornadoPickTarget,
                prefix: AccessTools.Method(typeof(TornadoPickTargetPatch), nameof(TornadoPickTargetPatch.Prefix)));

            // 特级厨师 / 炊事员·烹饪类效果：感知「谁烤制了什么」。
            // 拦 ItemCooking.FinishCookingImpl —— 它由 [PunRPC] SyncCookingStateRPC(FinishCooking)（RpcTarget.All）
            // 在**所有客户端（含主机）**上调用，因此主机能零客机安装地收到任意玩家的烤制完成事件；
            // 该方法是私有、无参、非 [PunRPC]，签名稳定，用宽松解析（按名）挂载即可。
            var finishCooking = ResolveMethod(typeof(ItemCooking), "FinishCookingImpl", null);
            TryPatch("ItemCooking.FinishCookingImpl (特级厨师/炊事员·烹饪感知)", finishCooking,
                prefix: AccessTools.Method(typeof(CookingFinishPatch), nameof(CookingFinishPatch.Prefix)));

            // 和平主义者·和平？：把「右键（原本拉人）」改为踢人。
            // 拦 CharacterGrabbing.RPCA_StartReaching —— [PunRPC]，玩家按下右键时由本人以 RpcTarget.All 广播，
            // 主机也收得到；主机随即取消拉人（RPCA_StopReaching）并在主机侧完成踢击判定与表现。
            // ⚠ 刻意不调用原版 RPCA_Kick：它会把 isKicking 置真，而该标记只在「踢人模式」下才会被复位，
            // 正常模式下会导致角色永久无法移动（实测 bug）。
            var startReaching = ResolveMethod(typeof(CharacterGrabbing), "RPCA_StartReaching", null);
            TryPatch("CharacterGrabbing.RPCA_StartReaching (和平主义者·右键改踢人)", startReaching,
                postfix: AccessTools.Method(typeof(PacifistReachPatch), nameof(PacifistReachPatch.Postfix)));

            // 自恋狂·精致 / 化学中毒：感知「用完 1 瓶防晒喷雾」。
            // 拦 Action_ReduceUses.ReduceUsesRPC —— [PunRPC]，使用者以 RpcTarget.All 广播，主机也收得到；
            // 递减后 ItemUses == 0 即「这瓶用完」。与其它 [PunRPC] 同理，用宽松解析（按名）应对
            // PEAKERRpcInfo 追加 PhotonMessageInfo 参数导致的签名变化。
            var reduceUses = ResolveMethod(typeof(Action_ReduceUses), "ReduceUsesRPC", null);
            TryPatch("Action_ReduceUses.ReduceUsesRPC (自恋狂·防晒喷雾用完)", reduceUses,
                postfix: AccessTools.Method(typeof(SunscreenUsePatch), nameof(SunscreenUsePatch.Postfix)));

            // 自恋狂·精致（补充）：感知「谁获得了防晒状态」。
            // 挂原版 Affliction_Sunscreen.OnApplied（首次套上）与 Affliction_Sunscreen.Stack（已持有时再次套上刷新）：
            // 两回调均由 CharacterAfflictions 在「新增 / 刷新」时调用；主机对远端角色经 SyncAfflictionsRPC
            // 也会走到（首次 fromRPC:true / 刷新直接 Stack），故零客机安装也感知得到。
            // 用 DeclaredOnly 确保取到 Affliction_Sunscreen 自身的重写（而非基类 Affliction 的空实现）。
            const System.Reflection.BindingFlags declaredInstance =
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.DeclaredOnly;
            var sunscreenApplied = typeof(Peak.Afflictions.Affliction_Sunscreen).GetMethod("OnApplied", declaredInstance);
            TryPatch("Affliction_Sunscreen.OnApplied (自恋狂·精致·获得防晒状态)", sunscreenApplied,
                postfix: AccessTools.Method(typeof(NarcissistSunscreenStatusPatch), nameof(NarcissistSunscreenStatusPatch.OnAppliedPostfix)));

            var sunscreenStack = typeof(Peak.Afflictions.Affliction_Sunscreen).GetMethod("Stack", declaredInstance);
            TryPatch("Affliction_Sunscreen.Stack (自恋狂·精致·获得防晒状态·刷新)", sunscreenStack,
                postfix: AccessTools.Method(typeof(NarcissistSunscreenStatusPatch), nameof(NarcissistSunscreenStatusPatch.StackPostfix)));

            // 乐队成员·乐器：拾取物品按权重替换为乐器，命中黑名单则保持原样。
            // 与刺客/野人/死灵法师共用 Player.AddItem 拦截点（Harmony 支持多 Prefix；玩家单一职业，不冲突）。
            TryPatch("Player.AddItem (乐队成员·乐器)", addItem,
                prefix: AccessTools.Method(typeof(BandPickupPatch), nameof(BandPickupPatch.Prefix)));

            // 乐队成员·演奏：感知「谁开始吹响乐器」。
            // 拦 BugleSFX.RPC_StartToot —— [PunRPC]，持乐器者以 RpcTarget.All 广播，
            // 主机也收得到；角色由 item.holderCharacter（兜底 photonView.Owner）反查。
            // 与其它 [PunRPC] 同理，用宽松解析（按名 + 首参）应对 PEAKERRpcInfo 追加 PhotonMessageInfo。
            var tootStart = ResolveMethod(typeof(BugleSFX), "RPC_StartToot", typeof(int));
            TryPatch("BugleSFX.RPC_StartToot (乐队成员·演奏)", tootStart,
                postfix: AccessTools.Method(typeof(BandBuglePatch), nameof(BandBuglePatch.StartTootPostfix)));

            // 领队·迈尔斯残魂·童军的盛情（克隆成功）：感知「谁用童军的盛情成功复制了物品」。
            // 拦 Action_CloneSelectedItem.RPCA_Shoot —— [PunRPC]，使用者以 RpcTarget.All 广播，
            // 主机也收得到；success == true 即该次使用成功复制了物品。角色由 ItemActionBase.item.holderCharacter 反查。
            var cloneShoot = ResolveMethod(typeof(Peak.Action_CloneSelectedItem), "RPCA_Shoot", typeof(Vector3));
            TryPatch("Action_CloneSelectedItem.RPCA_Shoot (迈尔斯残魂·童军的盛情)", cloneShoot,
                postfix: AccessTools.Method(typeof(MilesClonePatch), nameof(MilesClonePatch.Postfix)));
            TryPatch("Action_CloneSelectedItem.RPCA_Shoot (残魂本机·童军的盛情)", cloneShoot,
                postfix: AccessTools.Method(typeof(RollOfTheScout.Shared.MilesLocalClonePatch),
                    nameof(RollOfTheScout.Shared.MilesLocalClonePatch.Postfix)));

            // 领队·迈尔斯残魂·童军宝石「直接使用」感知（毅力 / 进取 / 野心）。
            // 这些宝石在原版是「口袋行为」，按下使用键时由使用者客户端本地执行对应动作，
            // 既不广播 RPC 也不经过 Item.Consume —— 因此挂这些**本地动作**的 Postfix：
            //   · AmuletBase.ToggleGem()（Action_StrangeGem 里调用，野心等）
            //   · Action_HealingGem.RunAction()（毅力）
            //   · Action_SuperJumpAmulet.RunAction()（进取）
            // Prefix 用来在动作**之前**记录使用者的石化值（零号守则的「使用前」抵消基准）。
            var toggleGem = ResolveMethod(typeof(Peak.AmuletBase), "ToggleGem", null);
            TryPatch("AmuletBase.ToggleGem (迈尔斯残魂·宝石使用)", toggleGem,
                prefix: AccessTools.Method(typeof(MilesGemUsePatch), nameof(MilesGemUsePatch.ToggleGemPrefix)),
                postfix: AccessTools.Method(typeof(MilesGemUsePatch), nameof(MilesGemUsePatch.ToggleGemPostfix)));

            var healingGem = ResolveMethod(typeof(Peak.Action_HealingGem), "RunAction", null);
            TryPatch("Action_HealingGem.RunAction (迈尔斯残魂·童军的毅力)", healingGem,
                prefix: AccessTools.Method(typeof(MilesGemUsePatch), nameof(MilesGemUsePatch.HealingGemPrefix)),
                postfix: AccessTools.Method(typeof(MilesGemUsePatch), nameof(MilesGemUsePatch.HealingGemPostfix)));

            var superJumpAmulet = ResolveMethod(typeof(Peak.Action_SuperJumpAmulet), "RunAction", null);
            TryPatch("Action_SuperJumpAmulet.RunAction (迈尔斯残魂·童军的进取)", superJumpAmulet,
                prefix: AccessTools.Method(typeof(MilesGemUsePatch), nameof(MilesGemUsePatch.SuperJumpAmuletPrefix)),
                postfix: AccessTools.Method(typeof(MilesGemUsePatch), nameof(MilesGemUsePatch.SuperJumpAmuletPostfix)));

            // 装有主模组的客机：与附属模组共用本机残魂动作检测，绕过主机旧副本的启发式写回。
            TryPatch("AmuletBase.ToggleGem (残魂本机·宝石使用)", toggleGem,
                prefix: AccessTools.Method(typeof(RollOfTheScout.Shared.MilesLocalGemUsePatch),
                    nameof(RollOfTheScout.Shared.MilesLocalGemUsePatch.ToggleGemPrefix)),
                postfix: AccessTools.Method(typeof(RollOfTheScout.Shared.MilesLocalGemUsePatch),
                    nameof(RollOfTheScout.Shared.MilesLocalGemUsePatch.ToggleGemPostfix)));
            TryPatch("Action_HealingGem.RunAction (残魂本机·童军的毅力)", healingGem,
                prefix: AccessTools.Method(typeof(RollOfTheScout.Shared.MilesLocalGemUsePatch),
                    nameof(RollOfTheScout.Shared.MilesLocalGemUsePatch.HealingGemPrefix)),
                postfix: AccessTools.Method(typeof(RollOfTheScout.Shared.MilesLocalGemUsePatch),
                    nameof(RollOfTheScout.Shared.MilesLocalGemUsePatch.HealingGemPostfix)));
            TryPatch("Action_SuperJumpAmulet.RunAction (残魂本机·童军的进取)", superJumpAmulet,
                prefix: AccessTools.Method(typeof(RollOfTheScout.Shared.MilesLocalGemUsePatch),
                    nameof(RollOfTheScout.Shared.MilesLocalGemUsePatch.SuperJumpPrefix)),
                postfix: AccessTools.Method(typeof(RollOfTheScout.Shared.MilesLocalGemUsePatch),
                    nameof(RollOfTheScout.Shared.MilesLocalGemUsePatch.SuperJumpPostfix)));

            // 领队·迈尔斯残魂·童军宝石「喂食」感知（毅力救人等）。
            // 喂食走 Item.FinishCastSecondary → FeedItem + FeederAction，既不过 RunAction/ToggleGem，
            // 也被 Item.Consume 的「消费者」通道排除（被喂的是昏迷队友）—— 此前主机本人喂宝石完全没有感知。
            // 该方法是普通实例方法、签名稳定（无参），用宽松解析按名挂载即可。
            var finishCast = ResolveMethod(typeof(Item), "FinishCastSecondary", null);
            TryPatch("Item.FinishCastSecondary (迈尔斯残魂·宝石喂食)", finishCast,
                prefix: AccessTools.Method(typeof(MilesFeedGemPatch), nameof(MilesFeedGemPatch.Prefix)),
                postfix: AccessTools.Method(typeof(MilesFeedGemPatch), nameof(MilesFeedGemPatch.Postfix)));
            TryPatch("Item.FinishCastSecondary (残魂本机·宝石喂食)", finishCast,
                prefix: AccessTools.Method(typeof(RollOfTheScout.Shared.MilesLocalFeedPatch),
                    nameof(RollOfTheScout.Shared.MilesLocalFeedPatch.Prefix)),
                postfix: AccessTools.Method(typeof(RollOfTheScout.Shared.MilesLocalFeedPatch),
                    nameof(RollOfTheScout.Shared.MilesLocalFeedPatch.Postfix)));

            // 领队·迈尔斯残魂·宝石「喂食开始」感知（区分「喂给昏迷者 / 喂给未昏迷者」的必需信息）。
            // 原版 Item.StartUseSecondary 在开始喂食时以 RpcTarget.All 广播
            // SendFeedDataRPC(giverID, recieverID, itemID, totalUsingTime) —— 主机由此可还原
            // 「谁在喂谁」并判断被喂者是否昏迷（客机喂食时主机看不到被喂者，只能靠这条广播）。
            // [PunRPC] 同理由宽松解析（按名 + 首参 int）应对 PEAKERRpcInfo 追加 PhotonMessageInfo。
            var sendFeedData = ResolveMethod(typeof(Item), "SendFeedDataRPC", typeof(int));
            TryPatch("Item.SendFeedDataRPC (迈尔斯残魂·宝石喂食意图)", sendFeedData,
                postfix: AccessTools.Method(typeof(MilesFeedStartPatch), nameof(MilesFeedStartPatch.Postfix)));

            // 领队·迈尔斯残魂·宝石「激活」广播（客机可靠通道）。
            // AmuletBase.ToggleGem() 会自己以 RpcTarget.All 广播 RPC_GemActiveChanged(bool)，
            // 因此**任意玩家（含客机）**激活童军宝石时主机都收得到 —— 不再只靠石化增量的启发式猜测。
            var gemActiveChanged = ResolveMethod(typeof(Peak.AmuletBase), "RPC_GemActiveChanged", typeof(bool));
            TryPatch("AmuletBase.RPC_GemActiveChanged (迈尔斯残魂·宝石激活)", gemActiveChanged,
                postfix: AccessTools.Method(typeof(MilesGemActivePatch), nameof(MilesGemActivePatch.Postfix)));

            // 领队·迈尔斯残魂·宝珠「本局第一次拾取」兜底通道：挂 Player.AddItem
            // （物品真正进入玩家物品栏 / 主手槽的唯一漏斗），补上 RequestPickup 可能漏掉的路径。
            // 与刺客/野人/死灵法师/乐队成员共用同一拦截点（Harmony 支持多 Prefix；玩家单一职业，不冲突）。
            TryPatch("Player.AddItem (迈尔斯残魂·宝石首拾)", addItem,
                prefix: AccessTools.Method(typeof(MilesAddItemPickupPatch), nameof(MilesAddItemPickupPatch.Prefix)));

            // 「头顶职业前缀」：把头顶玩家名称改成「【职业】昵称」。
            // 挂在 UIPlayerNames.UpdateName —— 该方法由每个角色的 IsLookedAt.Update() 每帧调用，
            // 是唯一稳定、每帧都会经过、且能拿到「该名称对应角色」的挂点。
            // ⚠ 纯本地 UI 改动：主机与「装了本模组的客机」都能各自看到（职业数据来源不同，见 HostClassManager）。
            // 非 [PunRPC] 方法、签名稳定（int, Vector3, bool, int），用宽松解析（按名 + 首参 int）挂载即可。
            var updateName = ResolveMethod(typeof(UIPlayerNames), "UpdateName", typeof(int));
            TryPatch("UIPlayerNames.UpdateName (头顶职业前缀)", updateName,
                postfix: AccessTools.Method(typeof(DisplayNamePatch), nameof(DisplayNamePatch.Postfix)));

            // 第三方「头顶id显示」模组兼容层（BetterPlayerDistance / Sense of Direction）：
            // 这两个模组会隐藏原版名牌、改用自己绘制的标签，因此仅靠上面的原版名牌补丁在其标签上看不到前缀。
            // 兼容层以运行时反射挂补丁（软依赖，未安装则跳过），实际挂载由 HostClassManager.Update 每帧驱动（限流重试）。
            PeakOverheadCompat.ThirdPartyOverheadNameCompat.Initialize(
                actor => ModConfig.General.NamePrefix.Value && HostClassManager.Instance != null
                    ? HostClassManager.Instance.GetLocalClassNameByActor(actor)
                    : string.Empty,
                msg => Log.LogWarning(msg));

            Log.LogInfo("[RollOfTheScout] 已加载。仅主机（房主）需要安装本模组。");
        }

        /// <summary>
        /// 按方法名宽松解析目标方法，优先返回「首个参数为 <paramref name="firstParam"/>」的重载。
        /// 无 <paramref name="firstParam"/> 时返回第一个同名方法。用于应对 PEAKERRpcInfo 对
        /// <c>[PunRPC]</c> 方法追加 <c>PhotonMessageInfo</c> 参数导致精确签名匹配失败的场景。
        /// </summary>
        private static System.Reflection.MethodInfo ResolveMethod(
            Type type, string name, Type firstParam)
        {
            const System.Reflection.BindingFlags flags =
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static;

            System.Reflection.MethodInfo loose = null;
            foreach (var m in type.GetMethods(flags))
            {
                if (m.Name != name) continue;
                if (firstParam == null) return m;

                var ps = m.GetParameters();
                if (ps.Length >= 1 && ps[0].ParameterType == firstParam) return m;
                if (loose == null) loose = m;
            }
            return loose;
        }

        /// <summary>
        /// 宽松解析 <c>Item.Consume</c>：按方法名查找，优先取「首个参数为 int」的重载。
        /// 之所以不用 AccessTools 的精确匹配，是因为游戏运行时 Assembly-CSharp 会被预加载补丁重写，
        /// [PunRPC] 方法的签名可能被改动（详见 Awake 中的注释）。
        /// </summary>
        private static System.Reflection.MethodInfo ResolveItemConsume()
        {
            const System.Reflection.BindingFlags flags =
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static;

            System.Reflection.MethodInfo loose = null;
            foreach (var m in typeof(Item).GetMethods(flags))
            {
                if (m.Name != "Consume") continue;

                var ps = m.GetParameters();
                if (ps.Length >= 1 && ps[0].ParameterType == typeof(int)) return m;
                if (loose == null) loose = m;
            }
            return loose;
        }

        /// <summary>
        /// 宽松解析 <c>Item.RequestPickup</c>：按方法名查找，优先取「首个参数为 PhotonView」的重载。
        /// 与 <see cref="ResolveItemConsume"/> 同理：PEAKERRpcInfo 会给 [PunRPC] 方法追加
        /// <c>PhotonMessageInfo</c> 参数，导致 AccessTools 的精确签名匹配失败。
        /// </summary>
        private static System.Reflection.MethodInfo ResolveItemRequestPickup()
        {
            const System.Reflection.BindingFlags flags =
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static;

            System.Reflection.MethodInfo loose = null;
            foreach (var m in typeof(Item).GetMethods(flags))
            {
                if (m.Name != "RequestPickup") continue;

                var ps = m.GetParameters();
                if (ps.Length >= 1 && ps[0].ParameterType == typeof(PhotonView)) return m;
                if (loose == null) loose = m;
            }
            return loose;
        }

        private void TryPatch(string label, System.Reflection.MethodBase target,
            System.Reflection.MethodInfo prefix = null, System.Reflection.MethodInfo postfix = null)
        {
            if (target == null)
            {
                Log.LogWarning($"[RollOfTheScout] 未找到目标方法，跳过补丁：{label}（游戏版本可能不兼容）");
                return;
            }
            try
            {
                var prefixMethod = prefix != null ? new HarmonyMethod(prefix) : null;
                var postfixMethod = postfix != null ? new HarmonyMethod(postfix) : null;
                _harmony.Patch(target, prefix: prefixMethod, postfix: postfixMethod);
                Log.LogInfo($"[RollOfTheScout] 补丁成功：{label}");
            }
            catch (Exception e)
            {
                Log.LogError($"[RollOfTheScout] 补丁失败：{label} → {e.Message}");
            }
        }
    }
}