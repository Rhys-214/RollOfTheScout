using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

namespace RollOfTheScout.Patches
{
    /// <summary>
    /// 关卡切换：篝火被点亮（updateSegment = true）时，主机为先驱/医生发放背包。
    /// 补丁通过 AccessTools 手动挂载（见 Plugin.cs），不使用特性，便于单个失败时降级。
    /// </summary>
    public static class CampfireLightPatch
    {
        /// <summary>对应 Campfire.Light_Rpc(bool updateSegment, float burningFor)。</summary>
        public static void Postfix(bool updateSegment)
        {
            if (!updateSegment) return;
            if (!PhotonNetwork.IsMasterClient) return;
            if (HostClassManager.Instance == null) return;
            HostClassManager.Instance.NotifyNewLevel();
        }
    }

    /// <summary>
    /// 古老雕像（篝火附近的复活雕像 RespawnChest）：领袖的神启 / 圣洁在此触发。
    /// 该方法本身就是主机权威（!IsMasterClient 直接 return），因此天然适配方案 B。
    ///
    /// 必须使用 Prefix：SpawnItems 内部会先调用 RespawnAllPlayersHere() 把所有死亡玩家复活，
    /// 若放到 Postfix，圣洁统计到的「死亡人数」将恒为 0、神启的「全员存活」也恒为真（实测问题 2.2）。
    /// </summary>
    public static class RespawnChestPatch
    {
        /// <summary>对应 RespawnChest.SpawnItems(List&lt;Transform&gt; spawnSpots)。</summary>
        public static void Prefix(RespawnChest __instance, List<Transform> spawnSpots)
        {
            if (!PhotonNetwork.IsMasterClient) return;
            if (!ModConfig.General.Enabled.Value) return;
            if (HostClassManager.Instance == null) return;

            HostClassManager.Instance.OnAncientStatueUsed(__instance, spawnSpots);
        }
    }

    /// <summary>
    /// 「复活继承」（领袖 / 后勤 / 挑战者【极限】）的确定性通道：直接挂在原版
    /// <c>Character.ReviveCharacter(bool applyStatus)</c> 上。
    ///
    /// 原版复活流程（反编译 L2853-2878）对**任何**复活来源都执行同一套改写：
    ///   <c>ClearAllStatus()</c>（默认不清诅咒 / 石化）→ <c>AdjustStatus(Petrify, -0.75f)</c>（石化 -75 点），
    /// 因此石化几乎必然被清零，而这三个职业需要把诅咒 / 石化恢复到目标值。
    /// 复活来源很多：童军雕像 <c>ScoutEffigy</c>、古老雕像 <c>RespawnChest</c>、骸骨之书 <c>Skelleton</c>、
    /// 篝火 / 虚空复活、中途加入者的首次复活；且玩家**仅昏迷倒地**时同样会被雕像复活
    /// （<c>RPCA_ReviveAtPosition</c> 的目标集合是 <c>data.dead || data.fullyPassedOut</c>）。
    /// 主机的 0.5s 轮询只能识别「真死亡 → 复活」这一种跳变，会漏掉后者（实测问题：
    /// 挑战者被童军雕像复活后石化没回到 70），故在此补上确定性的那一条：
    ///   · Prefix：在原版改状态**之前**记录该角色当前的诅咒 / 石化；
    ///   · Postfix：立刻按记录恢复到目标值（只由主机执行）。
    ///
    /// 该方法由 <c>RPCA_ReviveAtPosition</c> 在每个客户端（含主机）本地调用一次，因此主机必然被触发。
    /// 轮询通道保留为兜底（补丁万一挂不上时仍能覆盖常规死亡复活）。
    /// </summary>
    public static class ReviveCharacterPatch
    {
        /// <summary>对应 Character.ReviveCharacter(bool applyStatus)。</summary>
        public static void Prefix(Character __instance)
        {
            if (!PhotonNetwork.IsMasterClient) return;
            if (HostClassManager.Instance == null) return;
            HostClassManager.Instance.NotifyBeforeRevive(__instance);
        }

        /// <summary>对应 Character.ReviveCharacter(bool applyStatus)。</summary>
        public static void Postfix(Character __instance)
        {
            if (!PhotonNetwork.IsMasterClient) return;
            if (HostClassManager.Instance == null) return;
            HostClassManager.Instance.NotifyAfterRevive(__instance);
        }
    }

    /// <summary>
    /// 刺客·刺杀（拾取）：刺客获得任何物品时，有概率把该物品替换为仪式匕首。
    ///
    /// 拦截点选原版 <c>Player.AddItem(ushort itemID, ItemInstanceData, out ItemSlot)</c>：
    /// 它是「物品真正进入玩家背包/物品栏」的唯一漏斗 —— 拾取物品时，由拾取方以
    /// <c>RpcTarget.MasterClient</c> 调用 <c>Item.RequestPickup</c>，其内部再调用
    /// <c>component.player.AddItem(...)</c>，因此在主机上执行，天然满足「仅主机安装」。
    /// （对标参考模组 Witherstyle-BingBongTouch 的写法：同样只挂 Player.AddItem 的
    ///  <c>Prefix(ref ushort itemID)</c>。）
    ///
    /// 之前挂 <c>Item.RequestPickup</c> 的方案实测无效：运行时 AccessTools 报
    /// 「Could not find method for type Item and name RequestPickup」，补丁根本没挂上。
    ///
    /// 另支持「拾取变匕首黑名单」（`刺杀-拾取变匕首黑名单`）：命中的物品永不替换
    /// （默认排除吹箭筒、信号棒 Flare、5 件童军护符）。
    /// </summary>
    public static class AssassinPickupPatch
    {
        /// <summary>
        /// 内部发放物品（如刺客开局自带的匕首/吹箭筒）时临时置位，
        /// 避免把自己发放的物品也随机替换掉（尤其会让自带的吹箭筒被换成匕首）。
        /// </summary>
        private static int _suppress;

        public static void PushSuppress() => _suppress++;

        public static void PopSuppress()
        {
            if (_suppress > 0) _suppress--;
        }

        /// <summary>黑名单解析缓存（配置字符串 → 预制体名集合）。</summary>
        private static string _blacklistRaw;
        private static readonly HashSet<string> _blacklist = new HashSet<string>();

        /// <summary>该 itemID 对应的预制体名是否在「拾取变匕首黑名单」中。</summary>
        private static bool IsPickupBlacklisted(ushort itemID, out string itemName)
        {
            itemName = null;
            string raw = ModConfig.Assassin.PickupBlacklist.Value;
            if (raw != _blacklistRaw)
            {
                _blacklistRaw = raw;
                _blacklist.Clear();
                foreach (var name in WeightedPicker.ParseList(raw)) _blacklist.Add(name);
            }
            if (_blacklist.Count == 0) return false;

            if (!ItemDatabase.TryGetItem(itemID, out Item item) || item == null) return false;
            itemName = item.gameObject.name;
            return _blacklist.Contains(itemName);
        }

        /// <summary>对应 Player.AddItem(ushort itemID, ItemInstanceData instanceData, out ItemSlot slot)。</summary>
        public static void Prefix(Player __instance, ref ushort itemID)
        {
            try
            {
                if (_suppress > 0) return;
                if (!PhotonNetwork.IsMasterClient) return;
                if (__instance == null) return;
                if (HostClassManager.Instance == null) return;

                var character = __instance.character;
                if (character == null) return;

                // 只有刺客触发；其他职业获得的物品保持原样
                if (HostClassManager.Instance.GetClassOf(character) != ClassType.Assassin) return;

                float chance = ModConfig.Assassin.PickupDaggerChance.Value;
                if (chance <= 0f) return;
                if (UnityEngine.Random.value >= chance) return;

                // 黑名单：这些物品永不变成仪式匕首 / 吹箭筒（如吹箭筒、信号棒、5 件童军护符）
                if (IsPickupBlacklisted(itemID, out string blockedName))
                {
                    Plugin.Log.LogInfo(
                        $"[RollOfTheScout] 刺杀：{character.characterName} 拾取 {blockedName} 命中黑名单，保持原样。");
                    return;
                }

                // 命中概率后，按权重在「仪式匕首 / 吹箭筒」之间随机取一个作为替换目标。
                // 两项权重都设为 0 时视为「不替换」（沿用配置文案的约定；WeightedPicker.Pick
                // 在权重全为 0 时会退化成等概率随机，这里必须先拦下）。
                if (ModConfig.Assassin.PickupDaggerWeight.Value <= 0
                    && ModConfig.Assassin.PickupBlowgunWeight.Value <= 0) return;

                var pool = new List<WeightedItem>
                {
                    new WeightedItem(ItemNames.RitualDagger, ModConfig.Assassin.PickupDaggerWeight.Value),
                    new WeightedItem(ItemNames.Blowgun, ModConfig.Assassin.PickupBlowgunWeight.Value),
                };
                string targetName = WeightedPicker.Pick(pool);
                if (string.IsNullOrEmpty(targetName)) return;

                if (!ItemDatabase.TryGetItem(targetName, out Item target) || target == null) return;
                if (itemID == target.itemID) return;

                itemID = target.itemID;
                Plugin.Log.LogInfo(
                    $"[RollOfTheScout] 刺杀：{character.characterName} 获得的物品被替换为 {targetName}（概率 {chance * 100f:0.#}%）。");
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[RollOfTheScout] 刺杀·替换物品时异常：{e.Message}");
            }
        }
    }

    /// <summary>
    /// 潘多拉之子·潘多拉（拾取）：拾取任何物品时，按「变魔盒概率」变成潘多拉魔盒，
    /// 剩余概率（1 − 变魔盒概率）下物品直接消失；命中「潘多拉-拾取黑名单」的物品不受影响。
    ///
    /// 拦截点选原版 <c>Item.RequestPickup(PhotonView characterView)</c>：
    /// 它是 <c>[PunRPC]</c>，由拾取方以 <c>RpcTarget.MasterClient</c> 调用，天然只在主机执行
    /// （原版 <c>Item.Interact</c> → <c>view.RPC("RequestPickup", RpcTarget.MasterClient, ...)</c>）。
    /// 之所以不像刺客那样挂 <c>Player.AddItem</c>：那条路只能「换 itemID / 拒绝拾取」，
    /// 无法让物品真正消失（拒绝拾取时原版会走 <c>DenyPickupRPC</c> 把物品放回地面）。
    /// 挂 RequestPickup 才能在「消失」分支直接 <c>PhotonNetwork.Destroy</c> 销毁世界物品。
    ///
    /// 注意：游戏运行时 Assembly-CSharp 会被预加载补丁 PEAKERRpcInfo 重写 ——
    /// 它会给每个 <c>[PunRPC]</c> 方法追加一个 <c>PhotonMessageInfo</c> 参数，
    /// 使 AccessTools 的精确签名匹配失败。因此 Plugin 侧用宽松解析（按名 + 首参 PhotonView）。
    ///
    /// 本补丁只处理世界拾取，不影响模组自身的直接发放（走 Player.AddItem，不经过 RequestPickup）。
    /// </summary>
    public static class PandoraPickupPatch
    {
        /// <summary>黑名单解析缓存（配置字符串 → 预制体名集合）。</summary>
        private static string _blacklistRaw;
        private static readonly HashSet<string> _blacklist = new HashSet<string>();

        /// <summary>该 itemID 对应的预制体名是否在「潘多拉-拾取黑名单」中（黑名单开关关闭时恒为 false）。</summary>
        private static bool IsPickupBlacklisted(ushort itemID, out string itemName)
        {
            itemName = null;
            if (!ModConfig.Pandora.PickupBlacklistEnabled.Value) return false;

            string raw = ModConfig.Pandora.PickupBlacklist.Value;
            if (raw != _blacklistRaw)
            {
                _blacklistRaw = raw;
                _blacklist.Clear();
                foreach (var name in WeightedPicker.ParseList(raw)) _blacklist.Add(name);
            }
            if (_blacklist.Count == 0) return false;

            if (!ItemDatabase.TryGetItem(itemID, out Item item) || item == null) return false;
            itemName = item.gameObject.name;
            return _blacklist.Contains(itemName);
        }

        /// <summary>对应 Item.RequestPickup(PhotonView characterView)。</summary>
        public static bool Prefix(Item __instance, PhotonView characterView)
        {
            try
            {
                if (!PhotonNetwork.IsMasterClient) return true;
                if (__instance == null || characterView == null) return true;
                if (HostClassManager.Instance == null) return true;

                var character = characterView.GetComponent<Character>();
                if (character == null) return true;

                // 只有潘多拉之子触发；其他职业拾取物品保持原样
                if (HostClassManager.Instance.GetClassOf(character) != ClassType.Pandora) return true;

                // 黑名单：命中的物品不受潘多拉效果影响（既不变魔盒，也不消失）
                if (IsPickupBlacklisted(__instance.itemID, out string blockedName))
                {
                    Plugin.Log.LogInfo(
                        $"[RollOfTheScout] 潘多拉：{character.characterName} 拾取 {blockedName} 命中黑名单，保持原样。");
                    return true;
                }

                // 「变魔盒概率」与「消失概率」互补：消失概率 = 1 − 该值。
                // 因此 0 = 100% 消失、1 = 100% 变魔盒（不再把 0 当作「关闭」特例）。
                float chance = Mathf.Clamp01(ModConfig.Pandora.PickupBoxChance.Value);

                if (UnityEngine.Random.value < chance)
                {
                    // 变成潘多拉魔盒：改写 itemID（RequestPickup 内部会用它走 AddItem 入包），
                    // 同时清掉「秘密物品」包装，避免走 isSecretlyOtherItemPrefab 分支。
                    if (!ItemDatabase.TryGetItem(ItemNames.PandorasBox, out Item box) || box == null) return true;

                    __instance.isSecretlyOtherItemPrefab = null;
                    __instance.itemID = box.itemID;
                    // 关键修复：入包时会连同「原物品的 ItemInstanceData」一起带走，
                    // 必须把其中的 ItemUses 按魔盒预制体重置，否则魔盒会变成「无耐久条、可无限使用」。
                    ResetInstanceDataToBox(__instance, box);
                    Plugin.Log.LogInfo(
                        $"[RollOfTheScout] 潘多拉：{character.characterName} 拾取的物品变成了潘多拉魔盒" +
                        $"（概率 {chance * 100f:0.#}%，已按魔盒重置耐久 = {(box.totalUses < 0 ? "无限(-1)" : box.totalUses.ToString())}）。");
                    return true; // 交给原版流程，用改写后的 itemID 入包
                }

                // 直接消失：销毁世界物品并跳过原版入包
                Plugin.Log.LogInfo(
                    $"[RollOfTheScout] 潘多拉：{character.characterName} 拾取的物品直接消失了（概率 {(1f - chance) * 100f:0.#}%）。");
                HostApi.ConsumePickedItem(__instance);
                return false;
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[RollOfTheScout] 潘多拉·处理拾取时异常：{e.Message}");
                return true;
            }
        }

        /// <summary>
        /// 把「被替换物品」的实例数据按潘多拉魔盒预制体重置。
        ///
        /// 背景（实测问题 3.x）：原版 <c>Item.RequestPickup</c> 会把<b>世界物品原有的
        /// ItemInstanceData</b> 一并交给 <c>Player.AddItem(itemID, data, out slot)</c> 入包
        /// （见 Assembly-CSharp: RequestPickup → AddItem → ItemSlot.SetItem(prefab, data)）。
        /// 因此只改写 <c>itemID</c> 时，魔盒会继承原物品的数据 —— 其中 ItemUses（剩余使用次数/耐久）
        /// 仍是原物品的：绝大多数世界物品 <c>totalUses == -1</c>，对应 <c>ItemUses.HasData == false</c>，
        /// 于是魔盒变成「没有耐久条、可无限使用」——
        /// <c>Action_ReduceUses.ReduceUsesRPC</c> 见 HasData 为 false 时直接不扣次数，魔盒永不消耗。
        ///
        /// 这里按原版 <c>Item.Start()</c> 的初始化方式，用魔盒预制体的 <c>totalUses</c> 重写 ItemUses，
        /// 并清空「熟度」，避免原物品若是烤熟食物时魔盒显示成「烤熟的潘多拉魔盒」。
        /// </summary>
        private static void ResetInstanceDataToBox(Item item, Item boxPrefab)
        {
            var uses = item.GetData<OptionableIntItemData>(DataEntryKey.ItemUses);
            uses.HasData = boxPrefab.totalUses != -1;
            uses.Value = boxPrefab.totalUses;
            if (boxPrefab.totalUses > 0) item.SetUseRemainingPercentage(1f);

            item.GetData<IntItemData>(DataEntryKey.CookedAmount).Value = 0;
        }
    }

    /// <summary>
    /// 圣人·完美主义（拾取）：圣人拾取任何物品时，按「拾取消失概率」直接让物品消失；
    /// 命中「完美主义-拾取黑名单」的物品不受影响（保持原样入包）。
    ///
    /// 拦截点与潘多拉之子·潘多拉完全一致：原版 <c>Item.RequestPickup(PhotonView characterView)</c>
    /// —— 它是 <c>[PunRPC]</c>，由拾取方以 <c>RpcTarget.MasterClient</c> 调用，天然只在主机执行；
    /// 只有挂在这里才能在「消失」分支直接 <c>PhotonNetwork.Destroy</c> 销毁世界物品
    /// （挂 Player.AddItem 时拒绝拾取会被原版 DenyPickupRPC 把物品放回地面）。
    ///
    /// 与潘多拉补丁同挂 Item.RequestPickup（Harmony 支持多 Prefix；玩家单一职业，两者不会同时命中）。
    /// 同样用宽松解析应对 PEAKERRpcInfo 给 <c>[PunRPC]</c> 追加 PhotonMessageInfo 参数。
    /// </summary>
    public static class SaintPickupPatch
    {
        /// <summary>黑名单解析缓存（配置字符串 → 预制体名集合）。</summary>
        private static string _blacklistRaw;
        private static readonly HashSet<string> _blacklist = new HashSet<string>();

        /// <summary>该 itemID 对应的预制体名是否在「完美主义-拾取黑名单」中。</summary>
        private static bool IsPickupBlacklisted(ushort itemID, out string itemName)
        {
            itemName = null;
            string raw = ModConfig.Saint.PerfectionPickupBlacklist.Value;
            if (raw != _blacklistRaw)
            {
                _blacklistRaw = raw;
                _blacklist.Clear();
                foreach (var name in WeightedPicker.ParseList(raw)) _blacklist.Add(name);
            }
            if (_blacklist.Count == 0) return false;

            if (!ItemDatabase.TryGetItem(itemID, out Item item) || item == null) return false;
            itemName = item.gameObject.name;
            return _blacklist.Contains(itemName);
        }

        /// <summary>对应 Item.RequestPickup(PhotonView characterView)。</summary>
        public static bool Prefix(Item __instance, PhotonView characterView)
        {
            try
            {
                if (!PhotonNetwork.IsMasterClient) return true;
                if (__instance == null || characterView == null) return true;
                if (HostClassManager.Instance == null) return true;

                var character = characterView.GetComponent<Character>();
                if (character == null) return true;

                // 只有圣人触发；其他职业拾取物品保持原样
                if (HostClassManager.Instance.GetClassOf(character) != ClassType.Saint) return true;

                float chance = Mathf.Clamp01(ModConfig.Saint.PerfectionPickupDisappearChance.Value);
                if (chance <= 0f) return true;

                // 黑名单：命中的物品不受完美主义影响（不会消失）
                if (IsPickupBlacklisted(__instance.itemID, out string blockedName))
                {
                    Plugin.Log.LogInfo(
                        $"[RollOfTheScout] 完美主义：{character.characterName} 拾取 {blockedName} 命中黑名单，保持原样。");
                    return true;
                }

                if (UnityEngine.Random.value >= chance) return true;

                // 直接消失：销毁世界物品并跳过原版入包
                Plugin.Log.LogInfo(
                    $"[RollOfTheScout] 完美主义：{character.characterName} 拾取的物品直接消失了（概率 {chance * 100f:0.#}%）。");
                HostApi.ConsumePickedItem(__instance);
                return false;
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[RollOfTheScout] 完美主义·处理拾取时异常：{e.Message}");
                return true;
            }
        }
    }

    /// <summary>
    /// 挑战者·不屑（拾取）：挑战者拾取「潘多拉魔盒」时，物品直接消失、不进背包。
    ///
    /// 拦截点与圣人·完美主义完全一致，选 <c>Item.RequestPickup(PhotonView characterView)</c>：
    /// 它是 <c>[PunRPC]</c>，由拾取方以 <c>RpcTarget.MasterClient</c> 调用，天然只在主机执行；
    /// 且「直接消失」必须走这里才能在销毁世界物品的同时跳过原版入包
    /// （<c>Player.AddItem</c> 那条路只能换 itemID / 拒绝拾取，拒绝时原版会走 <c>DenyPickupRPC</c> 把物品放回地面）。
    ///
    /// 与潘多拉之子、圣人共用同一拦截点（Harmony 支持多 Prefix；玩家单一职业，不冲突）。
    /// </summary>
    public static class ChallengerDisdainPickupPatch
    {
        /// <summary>对应 Item.RequestPickup(PhotonView characterView)。</summary>
        public static bool Prefix(Item __instance, PhotonView characterView)
        {
            try
            {
                if (!PhotonNetwork.IsMasterClient) return true;
                if (__instance == null || characterView == null) return true;
                if (HostClassManager.Instance == null) return true;

                var character = characterView.GetComponent<Character>();
                if (character == null) return true;

                // 只有挑战者触发；其他职业拾取物品保持原样
                if (HostClassManager.Instance.GetClassOf(character) != ClassType.Challenger) return true;
                if (!ModConfig.Challenger.DisdainBoxDisappear.Value) return true;

                // 只对潘多拉魔盒生效（其余物品照常拾取）
                if (!IsPandorasBox(__instance.itemID)) return true;

                // 直接消失：销毁世界物品并跳过原版入包
                Plugin.Log.LogInfo(
                    $"[RollOfTheScout] 不屑：{character.characterName} 拾取的潘多拉魔盒直接消失了。");
                HostApi.ConsumePickedItem(__instance);
                return false;
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[RollOfTheScout] 不屑·处理拾取魔盒时异常：{e.Message}");
                return true;
            }
        }

        /// <summary>该 itemID 对应的预制体名是否为潘多拉魔盒（PandorasBox）。</summary>
        private static bool IsPandorasBox(ushort itemID)
        {
            if (!ItemDatabase.TryGetItem(itemID, out Item item) || item == null) return false;
            return item.gameObject.name == ItemNames.PandorasBox;
        }
    }

    /// <summary>
    /// 野人·野性未泯（食用）：野人食用「包装食物」或「烤过的食物」时增加损伤。
    ///
    /// 拦截点选原版 <c>Item.Consume(int consumerID)</c>：它是 <c>[PunRPC]</c>，
    /// 由食用者以 <c>RpcTarget.All</c> 广播，主机也会收到，且 <c>consumerID</c> 就是食用者的
    /// <c>PhotonView.ViewID</c> —— 因此主机能可靠识别「谁吃了什么」，无需客机安装任何东西。
    /// 判定标准（游戏原生标签）见 <see cref="HostClassManager.OnSavageItemConsumed"/>。
    ///
    /// 与转职补丁（<see cref="ReclassConsumePatch"/>）挂在同一方法上，Harmony 支持多个 Prefix；
    /// 玩家同一时刻只有一个职业，两者不会互相干扰。与转职同理，游戏运行时 Assembly-CSharp
    /// 会被预加载补丁重写，Plugin 侧统一用宽松解析挂载，并由 HostClassManager 的全局食用事件兜底。
    /// </summary>
    public static class SavageEatPatch
    {
        /// <summary>对应 Item.Consume(int consumerID)。</summary>
        public static void Prefix(Item __instance, int consumerID)
        {
            try
            {
                if (!PhotonNetwork.IsMasterClient) return;
                if (__instance == null) return;
                if (HostClassManager.Instance == null) return;

                var view = PhotonNetwork.GetPhotonView(consumerID);
                if (view == null) return;
                var character = view.GetComponent<Character>();
                if (character == null || character.player == null) return;

                HostClassManager.Instance.OnSavageItemConsumed(__instance, character);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[RollOfTheScout] 野性未泯·处理食用事件时异常：{e.Message}");
            }
        }
    }

    /// <summary>
    /// 待皓·模因污染（食用）：本局存在待皓职业玩家时，其他玩家食用「模因污染-触发物品」
    /// （默认煎蛋 Egg，由好大蛋 NestEgg 砸开后产出）会直接转职成待皓。
    ///
    /// 与野人·野性未泯补丁（<see cref="SavageEatPatch"/>）挂在同一方法上，Harmony 支持多个 Prefix；
    /// 玩家同一时刻只有一个职业，两者不会互相干扰。Plugin 侧统一用宽松解析挂载，
    /// 并由 HostClassManager 的全局食用事件兜底（两条通道由 OnDaihaoMemeConsumed 内的去重窗口保证只生效一次）。
    /// </summary>
    public static class MemeConsumePatch
    {
        /// <summary>对应 Item.Consume(int consumerID)。</summary>
        public static void Prefix(Item __instance, int consumerID)
        {
            try
            {
                if (!PhotonNetwork.IsMasterClient) return;
                if (__instance == null) return;
                if (HostClassManager.Instance == null) return;

                var view = PhotonNetwork.GetPhotonView(consumerID);
                if (view == null) return;
                var character = view.GetComponent<Character>();
                if (character == null || character.player == null) return;

                HostClassManager.Instance.OnDaihaoMemeConsumed(__instance, character);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[RollOfTheScout] 模因污染·处理食用事件时异常：{e.Message}");
            }
        }
    }

    /// <summary>
    /// 野人·知识污染（拾取）：野人获得「岩钉 / 绳索炮 / 锁链发射器 / 救援抓钩 / 反重力炮」时，
    /// 这些物品会被直接替换为木棍。
    ///
    /// 拦截点与刺客完全一致：原版 <c>Player.AddItem(ushort itemID, ItemInstanceData, out ItemSlot)</c>
    /// —— 物品真正进入玩家背包/物品栏的唯一漏斗，拾取时由主机执行，天然满足「仅主机安装」。
    /// 来源物品与替换目标均可配置（默认来源为 ClimbingSpike / RopeShooter / ChainShooter /
    /// RescueHook / AntiZooka，目标为 FireWood 木棍）。
    /// </summary>
    public static class SavagePickupPatch
    {
        /// <summary>来源物品名集合的解析缓存（配置字符串 → 预制体名集合）。</summary>
        private static string _sourceRaw;
        private static bool _sourceParsed;
        private static readonly HashSet<string> _sourceNames = new HashSet<string>();

        /// <summary>对应 Player.AddItem(ushort itemID, ItemInstanceData instanceData, out ItemSlot slot)。</summary>
        public static void Prefix(Player __instance, ref ushort itemID)
        {
            try
            {
                if (!PhotonNetwork.IsMasterClient) return;
                if (__instance == null) return;
                if (HostClassManager.Instance == null) return;

                var character = __instance.character;
                if (character == null) return;

                // 只有野人触发；其他职业获得的物品保持原样
                if (HostClassManager.Instance.GetClassOf(character) != ClassType.Savage) return;

                string raw = ModConfig.Savage.PickupSourceItems.Value;
                if (!_sourceParsed || raw != _sourceRaw)
                {
                    _sourceRaw = raw;
                    _sourceParsed = true;
                    _sourceNames.Clear();
                    foreach (var name in WeightedPicker.ParseList(raw)) _sourceNames.Add(name);
                }
                if (_sourceNames.Count == 0) return;

                if (!ItemDatabase.TryGetItem(itemID, out Item source) || source == null) return;
                string sourceName = source.gameObject.name;
                if (!_sourceNames.Contains(sourceName)) return;

                string target = ModConfig.Savage.PickupTargetItem.Value;
                if (string.IsNullOrEmpty(target)) return;
                if (!ItemDatabase.TryGetItem(target, out Item targetItem) || targetItem == null) return;
                if (itemID == targetItem.itemID) return;

                itemID = targetItem.itemID;
                Plugin.Log.LogInfo(
                    $"[RollOfTheScout] 知识污染：{character.characterName} 获得的 {sourceName} 被替换为 {targetItem.gameObject.name}。");
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[RollOfTheScout] 知识污染·替换物品时异常：{e.Message}");
            }
        }
    }

    /// <summary>
    /// 死灵法师·骸骨（拾取）：死灵法师拾取的潘多拉魔盒 PandorasBox 直接变为骸骨之书 BookOfBones。
    ///
    /// 拦截点与刺客/野人完全一致：原版 <c>Player.AddItem(ushort itemID, ItemInstanceData, out ItemSlot)</c>
    /// —— 物品真正进入玩家背包/物品栏的唯一漏斗，拾取时由主机执行，天然满足「仅主机安装」。
    /// 可通过「骸骨-魔盒变骸骨之书」开关关闭。
    ///
    /// 第 54 批修复：与潘多拉之子「拾取变魔盒」当年一样，只改写 <c>itemID</c> 会让<b>魔盒的
    /// ItemInstanceData 一并被书本继承</b> —— 魔盒若 <c>HasData == false</c>（无耐久），
    /// 书本就会变成「没有耐久条、可无限使用」。因此这里按 <c>BookOfBones</c> 预制体的
    /// <c>totalUses</c> 强制重置耐久，保证<b>一本骸骨之书只能使用一次</b>。
    /// </summary>
    public static class NecromancerPickupPatch
    {
        /// <summary>对应 Player.AddItem(ushort itemID, ItemInstanceData instanceData, out ItemSlot slot)。</summary>
        public static void Prefix(Player __instance, ref ushort itemID, ref ItemInstanceData instanceData)
        {
            try
            {
                if (!PhotonNetwork.IsMasterClient) return;
                if (__instance == null) return;
                if (HostClassManager.Instance == null) return;
                if (!ModConfig.Necromancer.BoxToBook.Value) return;

                var character = __instance.character;
                if (character == null) return;

                // 只有死灵法师触发；其他职业拾取魔盒保持原样
                if (HostClassManager.Instance.GetClassOf(character) != ClassType.Necromancer) return;

                if (!ItemDatabase.TryGetItem(ItemNames.PandorasBox, out Item box) || box == null) return;
                if (itemID != box.itemID) return;

                if (!ItemDatabase.TryGetItem(ItemNames.BookOfBones, out Item book) || book == null) return;

                itemID = book.itemID;

                // 强制按书本预制体重置耐久 / 熟度，避免继承潘多拉魔盒的实例数据
                ResetInstanceDataToBook(ref instanceData, book);

                Plugin.Log.LogInfo(
                    $"[RollOfTheScout] 骸骨：{character.characterName} 拾取的潘多拉魔盒被转化为骸骨之书" +
                    $"（已按书本重置耐久 = {(book.totalUses < 0 ? "无限(-1)" : book.totalUses.ToString())}）。");
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[RollOfTheScout] 骸骨·魔盒变书时异常：{e.Message}");
            }
        }

        /// <summary>
        /// 把「入包用」的实例数据按骸骨之书预制体重置。
        ///
        /// 原版 <c>Item.RequestPickup</c> 会把世界物品原有的 <c>ItemInstanceData</c> 一并交给
        /// <c>Player.AddItem(itemID, data, out slot)</c>；只改写 itemID 时书本会继承魔盒的数据，
        /// 其中 <c>ItemUses</c> 决定「能不能用 / 能用几次」：魔盒 <c>HasData == false</c> 时
        /// <c>Action_ReduceUses.ReduceUsesRPC</c> 不会扣次数 → 书本可无限使用。
        /// 这里按原版 <c>Item.Start()</c> 的初始化方式，用书本预制体的 <c>totalUses</c> 重写 ItemUses，
        /// 并清空熟度（避免若原物品是烤熟食物时书本显示成「烤熟的骸骨之书」）。
        /// </summary>
        private static void ResetInstanceDataToBook(ref ItemInstanceData data, Item bookPrefab)
        {
            // data 为 null 时原版 AddItem 会自建一份「没有任何条目的新数据」——
            // 那样 ItemUses 条目缺失，同样会被判为「无耐久、可无限使用」，故这里主动补上。
            if (data == null)
            {
                data = new ItemInstanceData(System.Guid.NewGuid());
                ItemInstanceDataHandler.AddInstanceData(data);
            }

            var uses = GetOrCreateEntry<OptionableIntItemData>(data, DataEntryKey.ItemUses);
            uses.HasData = bookPrefab.totalUses != -1;
            uses.Value = bookPrefab.totalUses;

            GetOrCreateEntry<IntItemData>(data, DataEntryKey.CookedAmount).Value = 0;
        }

        /// <summary>取已有条目；不存在时新建（等价于原版 <c>Item.GetData&lt;T&gt;(key)</c> 的 get-or-create 语义）。</summary>
        private static T GetOrCreateEntry<T>(ItemInstanceData data, DataEntryKey key)
            where T : DataEntryValue, new()
        {
            if (data.TryGetDataEntry<T>(key, out T existing) && existing != null) return existing;
            return data.RegisterNewEntry<T>(key);
        }
    }

    /// <summary>
    /// 石像鬼·【石化】（拾取）：石化达到阈值时食物变石头；拾取石头时按概率触发状态收益 / 清负 / 化雕像。
    ///
    /// 拦截点选原版 <c>Item.RequestPickup(PhotonView characterView)</c>（与潘多拉 / 太空人 / 混乱末影人一致），
    /// 因为只有在这里才能拿到世界物品实例并 <c>PhotonNetwork.Destroy</c>：
    /// 本轮调整后除「30% 无变化」外，其余石头结果都会让石头**消失**，<c>Player.AddItem</c> 前缀无法销毁世界物品。
    /// 食物变石头 / 石头化雕像由主机在销毁原物品后，通过 <c>HostApi.GiveItem</c> 重新发放目标物品；
    /// 若发放失败则在脚下生成，避免物品凭空消失。
    /// </summary>
    public static class GargoylePickupPatch
    {
        /// <summary>对应 Item.RequestPickup(PhotonView characterView)。</summary>
        public static bool Prefix(Item __instance, PhotonView characterView)
        {
            try
            {
                if (!PhotonNetwork.IsMasterClient) return true;
                if (__instance == null || characterView == null) return true;

                var host = HostClassManager.Instance;
                if (host == null) return true;
                if (!ModConfig.General.Enabled.Value || !ModConfig.Gargoyle.Enabled.Value) return true;

                var character = characterView.GetComponent<Character>();
                if (character == null) return true;
                if (host.GetClassOf(character) != ClassType.Gargoyle) return true;

                var result = host.OnGargoylePickup(__instance, character);
                if (result == HostClassManager.GargoylePickupResult.Keep) return true;

                // 先销毁世界物品，再按结果发放替代物 / 直接消失；返回 false 跳过原版入包。
                HostApi.ConsumePickedItem(__instance);

                if (result == HostClassManager.GargoylePickupResult.GiveStone)
                {
                    GiveOrDrop(character, ModConfig.Gargoyle.FoodToStoneItem.Value, "石头");
                }
                else if (result == HostClassManager.GargoylePickupResult.GiveStatue)
                {
                    GiveOrDrop(character, ModConfig.Gargoyle.StatueItem.Value, "童军雕像");
                }

                return false;
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[RollOfTheScout] 石化·处理拾取时异常：{e.Message}");
                return true;
            }
        }

        /// <summary>
        /// 把替代物品以「原版拾取」的方式发给玩家（空物品栏优先，全满则进主手并立即握持）；
        /// 发放失败（主手也被占用 / 正在攀爬）时在脚下生成，避免「石头已销毁但什么都没拿到」。
        /// </summary>
        private static void GiveOrDrop(Character character, string prefabName, string label)
        {
            string name = prefabName != null ? prefabName.Trim() : string.Empty;
            if (string.IsNullOrEmpty(name)) return;

            if (HostApi.GiveItemAsPickup(character, name, out _)) return;

            HostApi.SpawnItemAtFeet(character, name);
            Plugin.Log.LogWarning(
                $"[RollOfTheScout] 石化：{character.characterName} 的{label} {name} 未能入包，已改为生成在脚下。");
        }
    }

    /// <summary>
    /// 炼金术士·【等价交换】/【能量汲取】（拾取）：
    /// · 工具物品 → 随机负面状态 + 随机转化为另一件物品（带 5 秒冷却）；
    /// · 可食用物品（豁免表以外）→ 直接销毁，削减负面并回复额外体力。
    ///
    /// 与潘多拉 / 圣人 / 挑战者 / 迈尔斯残魂 / 区 / 太空人 / 混乱末影人 / 石像鬼共用
    /// <c>Item.RequestPickup(PhotonView characterView)</c>（<c>[PunRPC]</c>，<c>RpcTarget.MasterClient</c>，
    /// 只在主机执行）。只有这里能在销毁世界物品的同时跳过原版入包；
    /// 「转化为另一件物品」由主机销毁原物品后通过 <c>HostApi.GiveItem</c> 发放替代物。
    /// </summary>
    public static class AlchemistPickupPatch
    {
        /// <summary>对应 Item.RequestPickup(PhotonView characterView)。</summary>
        public static bool Prefix(Item __instance, PhotonView characterView)
        {
            try
            {
                if (!PhotonNetwork.IsMasterClient) return true;
                if (__instance == null || characterView == null) return true;

                var host = HostClassManager.Instance;
                if (host == null) return true;
                if (!ModConfig.General.Enabled.Value || !ModConfig.Alchemist.Enabled.Value) return true;

                var character = characterView.GetComponent<Character>();
                if (character == null) return true;
                if (host.GetClassOf(character) != ClassType.Alchemist) return true;

                var result = host.OnAlchemistPickup(__instance, character, out string givePrefab);
                if (result == HostClassManager.AlchemistPickupResult.Keep) return true;

                // 先销毁世界物品，再按结果发放替代物 / 直接消失；返回 false 跳过原版入包。
                HostApi.ConsumePickedItem(__instance);

                if (result == HostClassManager.AlchemistPickupResult.Give
                    && !string.IsNullOrEmpty(givePrefab))
                {
                    GiveOrDrop(character, givePrefab);
                }

                return false;
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[RollOfTheScout] 炼金术士·处理拾取时异常：{e.Message}");
                return true;
            }
        }

        /// <summary>
        /// 把转化 / 炼制物品以「原版拾取」的方式发给玩家（空物品栏优先，全满则进主手并立即握持）；
        /// 发放失败时在脚下生成，避免「原物品已销毁但什么都没拿到」。
        /// </summary>
        private static void GiveOrDrop(Character character, string prefabName)
        {
            if (HostApi.GiveItemAsPickup(character, prefabName, out _)) return;

            HostApi.SpawnItemAtFeet(character, prefabName);
            Plugin.Log.LogWarning(
                $"[RollOfTheScout] 炼金术士：{character.characterName} 的 {prefabName} 未能入包，已改为生成在脚下。");
        }
    }

    /// <summary>
    /// 转职：玩家食用「转职物品」（默认诡异菇 Mushroom Glow）后更换职业。
    ///
    /// 拦截点选原版 <c>Item.Consume(int consumerID)</c>：
    /// 它是 <c>[PunRPC]</c>，由食用者以 <c>RpcTarget.All</c> 广播（原版 <c>ConsumeDelayed</c> 里调用），
    /// 主机也会收到，且参数 <c>consumerID</c> 就是食用者的 <c>PhotonView.ViewID</c> ——
    /// 因此主机能可靠地识别「谁吃了什么」，无需客机安装任何东西。
    /// 具体转职逻辑见 <see cref="HostClassManager.OnReclassItemConsumed"/>。
    ///
    /// 注意：游戏运行时 Assembly-CSharp 会被预加载补丁（PEAKERRpcInfo）重写，<c>[PunRPC]</c> 方法签名
    /// 可能被改动，导致 AccessTools 精确匹配挂不上本补丁。因此 Plugin 侧改用宽松解析，
    /// 且 HostClassManager 同时订阅了游戏自身的 <c>GlobalEvents.OnItemConsumed</c> 作为兜底通道
    /// （两条通道由 <c>OnReclassItemConsumed</c> 内的去重窗口保证只生效一次）。
    /// </summary>
    public static class ReclassConsumePatch
    {
        /// <summary>对应 Item.Consume(int consumerID)。</summary>
        public static void Prefix(Item __instance, int consumerID)
        {
            try
            {
                if (!PhotonNetwork.IsMasterClient) return;
                if (__instance == null) return;
                if (HostClassManager.Instance == null) return;
                if (!ModConfig.General.ReclassEnabled.Value) return;

                // 只认配置的转职物品；用 itemID → 预制体名比对，避免实例名带的 "(Clone)" 后缀干扰
                string trigger = ModConfig.General.ReclassItem.Value;
                if (string.IsNullOrEmpty(trigger)) return;
                if (!ItemDatabase.TryGetItem(__instance.itemID, out Item prefab) || prefab == null) return;
                if (prefab.gameObject.name != trigger) return;

                var view = PhotonNetwork.GetPhotonView(consumerID);
                if (view == null) return;
                var character = view.GetComponent<Character>();
                if (character == null || character.player == null) return;

                HostClassManager.Instance.OnReclassItemConsumed(character);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[RollOfTheScout] 转职·处理食用事件时异常：{e.Message}");
            }
        }
    }

    /// <summary>
    /// 旗手·执旗：检测「检查点旗标被插下」。
    ///
    /// 插旗在插旗者自己的客户端本地完成（<c>Constructable.FinishConstruction</c> 实例化旗标 →
    /// <c>CheckpointFlag.Initialize</c>），主机不会执行这段逻辑；但 <c>Initialize</c> 末尾会广播一条
    /// <c>[PunRPC] SetColor</c>（<c>RpcTarget.AllBuffered</c>），**主机也收得到**，因此把补丁挂在它上面。
    ///
    /// 插旗者身份用「旗标的 PhotonView 所有者」反查：旗标由插旗者 <c>PhotonNetwork.Instantiate</c> 创建，
    /// owner 即插旗者。这样就不必依赖 <c>PhotonMessageInfo</c>（它会被预加载补丁 PEAKERRpcInfo 追加，
    /// 导致签名不稳定）。每个旗标恰好广播一次 SetColor，因此不会重复触发。
    /// </summary>
    public static class CheckpointFlagPatch
    {
        /// <summary>对应 CheckpointFlag.SetColor(Vector3 c)。</summary>
        public static void Postfix(CheckpointFlag __instance)
        {
            try
            {
                if (!PhotonNetwork.IsMasterClient) return;
                if (__instance == null) return;
                if (HostClassManager.Instance == null) return;

                var view = __instance.GetComponent<PhotonView>();
                if (view == null || view.Owner == null) return;

                var planter = HostClassManager.Instance.FindCharacterOfPlayer(view.Owner);
                if (planter == null) return;

                HostClassManager.Instance.OnCheckpointFlagPlanted(planter);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[RollOfTheScout] 执旗·检测插旗时异常：{e.Message}");
            }
        }
    }

    /// <summary>
    /// 舞王：感知「谁做了表情 / 跳舞」，并在需要时由主机主动让任意玩家跳舞。
    ///
    /// 拦截点选原版 <c>CharacterAnimations.RPCA_PlayRemove(string emoteName, bool succeeded)</c>：
    /// 它是 <c>[PunRPC]</c>，由做表情的客户端以 <c>RpcTarget.All</c> 广播
    /// （见原版 <c>CharacterAnimations.PlayEmote</c>），**主机也收得到**，因此主机可零客机安装地
    /// 识别「谁做了哪个表情」。反过来，主机对任意角色的 PhotonView 直接调用同名 RPC
    /// （<c>RpcTarget.All</c>）即可让该玩家（含客机）播放指定表情 —— 原版表情系统无归属权校验。
    ///
    /// 注意：与其它 <c>[PunRPC]</c> 一样，游戏运行时 Assembly-CSharp 会被预加载补丁 PEAKERRpcInfo
    /// 追加 <c>PhotonMessageInfo</c> 参数，导致精确签名匹配失败；因此 Plugin 侧用宽松解析
    /// （按名 + 首参 string），Postfix 只声明自己需要的参数（<c>__instance</c> 与 <c>emoteName</c>）。
    /// </summary>
    public static class EmotePatch
    {
        /// <summary>对应 CharacterAnimations.RPCA_PlayRemove(string emoteName, bool succeeded)。</summary>
        public static void Postfix(CharacterAnimations __instance, string emoteName)
        {
            try
            {
                if (!PhotonNetwork.IsMasterClient) return;
                if (__instance == null || string.IsNullOrEmpty(emoteName)) return;
                if (HostClassManager.Instance == null) return;

                var character = __instance.GetComponent<Character>();
                if (character == null) return;

                HostClassManager.Instance.OnEmotePlayed(character, emoteName);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[RollOfTheScout] 舞王·处理表情事件时异常：{e.Message}");
            }
        }
    }

    /// <summary>
    /// 挑战者·不屑：抓钩钩到玩家时，持钩的挑战者立刻灌满困倦（直接昏迷）。
    ///
    /// 拦截点：原版 <c>RescueHook.RPCA_RescueCharacter(PhotonView characterView)</c> ——
    /// 抓钩命中「角色」时由持钩者客户端以 <c>RpcTarget.All</c> 广播，主机也收得到；
    /// 命中墙壁走的是 <c>RPCA_RescueWall</c>，不会触发本效果。
    /// 黄金抓钩（`RescueHook_Infinite`）与救援抓钩（`RescueHook`）共用同一个 <c>RescueHook</c> 组件，两者都会命中。
    ///
    /// 持钩者由 <c>Item.holderCharacter</c> 反查（由原版 <c>EquipSlotRpc</c>（RpcTarget.All）在主机端设置，可靠）。
    /// 与其它 <c>[PunRPC]</c> 补丁同理，用宽松解析（按名 + 首参 PhotonView）挂载，Postfix 只声明 __instance。
    /// </summary>
    public static class ChallengerHookPatch
    {
        /// <summary>对应 RescueHook.RPCA_RescueCharacter(PhotonView characterView)。</summary>
        public static void Postfix(RescueHook __instance)
        {
            try
            {
                if (!PhotonNetwork.IsMasterClient) return;
                if (__instance == null) return;
                if (HostClassManager.Instance == null) return;

                var item = __instance.item;
                if (item == null) return;

                HostClassManager.Instance.OnChallengerHookedCharacter(item.holderCharacter);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[RollOfTheScout] 挑战者·不屑：处理抓钩命中时异常：{e.Message}");
            }
        }
    }

    /// <summary>
    /// 黑魔法·天灾兜底：当关卡内不存在带 TornadoPoints 子物体的 <c>TornadoSpawner</c> 时，
    /// 我们仍会生成龙卷风，但不调用 <c>RPCA_InitTornado</c>（其内部 <c>PhotonView.Find</c> 会拿到 null）。
    /// 此时龙卷风的 <c>targetParent</c> 为 null，原版 <c>PickTarget</c> 会因访问
    /// <c>targetParent.childCount</c> 而每帧 NullReferenceException（且该 RPC 是 RpcTarget.All，
    /// 客机同样会报错）。故在 targetParent 为空时直接跳过原版 PickTarget ——
    /// 龙卷风原地停留，仍由主机按时长销毁（符合「只存在 5 秒」的需求）。
    /// </summary>
    public static class TornadoPickTargetPatch
    {
        private static readonly System.Reflection.FieldInfo TargetParentField =
            HarmonyLib.AccessTools.Field(typeof(Tornado), "targetParent");

        /// <summary>对应 Tornado.PickTarget()。</summary>
        public static bool Prefix(Tornado __instance)
        {
            try
            {
                if (__instance == null || TargetParentField == null) return true;
                var parent = TargetParentField.GetValue(__instance) as Transform;
                return parent != null;   // targetParent 为空 → 跳过原版 PickTarget，避免 NRE
            }
            catch
            {
                return true;
            }
        }
    }

    /// <summary>
    /// 特级厨师 / 炊事员·烹饪类效果的「烤制完成」感知。
    ///
    /// 拦截点选 <c>ItemCooking.FinishCookingImpl()</c>（私有、无参、**非** [PunRPC]）：
    ///   · 原版 <c>FinishCooking()</c> 只在「物品控制器（属主）」上执行，主机收不到远端玩家的这次调用；
    ///     但它随后广播 <c>SyncCookingStateRPC(FinishCooking)</c>（<c>RpcTarget.All</c>），**主机必然收到**，
    ///     而该 RPC 内部会调用 <c>FinishCookingImpl()</c> —— 于是主机能零客机安装地感知「任意玩家烤制了什么」。
    ///   · 之所以不直接挂 <c>SyncCookingStateRPC</c>：它的参数是 <c>ItemCooking</c> 内嵌的**私有枚举**
    ///     <c>NetworkedCooking</c>，补丁方法无法声明该类型；且它是 [PunRPC]，会被 PEAKERRpcInfo 追加
    ///     <c>PhotonMessageInfo</c> 参数，签名不稳定。
    ///   · 选 Prefix 而非 Postfix：Prefix 执行时 <c>CookedAmount</c> **尚未自增**，
    ///     因此能判出「这次烤制之前是否烤过」（炊事员·饮食供给要求「从未烤过」）。
    ///
    /// 烤制者取 <c>item.holderCharacter</c>（篝火 / 便携烤炉烤制时物品正被该玩家手持）；
    /// 丢进岩浆烤制的物品没有持有者，忽略。判定见 <see cref="HostClassManager.OnItemCooked"/>。
    /// </summary>
    public static class CookingFinishPatch
    {
        /// <summary>对应 ItemCooking.FinishCookingImpl()。</summary>
        public static void Prefix(ItemCooking __instance)
        {
            try
            {
                if (!PhotonNetwork.IsMasterClient) return;
                if (__instance == null) return;
                if (HostClassManager.Instance == null) return;

                HostClassManager.Instance.OnItemCooked(__instance);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[RollOfTheScout] 烹饪·处理烤制完成事件时异常：{e.Message}");
            }
        }
    }

    /// <summary>
    /// 和平主义者·和平？：把「右键（原本是拉人）」改为踢人。
    ///
    /// 拦截点选原版 <c>CharacterGrabbing.RPCA_StartReaching()</c>：它是 <c>[PunRPC]</c>，
    /// 玩家按下右键（reach）时由**本人**以 <c>RpcTarget.All</c> 广播（见 <c>CharacterGrabbing.Update</c>：
    /// <c>sincePressReach &lt; 0.2f</c> 时 <c>RPC("RPCA_StartReaching", RpcTarget.All)</c>），**主机必然收到** ——
    /// 于是主机可零客机安装地感知「某玩家按下了右键」。
    /// 主机随后取消原版拉人（<c>RPCA_StopReaching</c>）并在**主机侧**完成踢击判定与表现
    /// （详见 <see cref="HostClassManager.OnPacifistReachPressed"/>）。
    ///
    /// ⚠ 刻意**不**调用原版 <c>RPCA_Kick</c>：它会把 <c>CharacterGrabbing.data.isKicking</c> 置真，
    /// 而该标记只在「帮助之手（踢人）模式」下才会被自动复位，正常模式下会导致角色**永久无法移动**。
    ///
    /// 注意：该 RPC 只在**非踢人模式**下发送（原版踢人模式由 run setting <c>HelpingHand</c> 决定，
    /// 那种模式下 <c>RPCA_StartReaching</c> 不会被调用，本效果也就不会触发）。
    /// </summary>
    public static class PacifistReachPatch
    {
        /// <summary>对应 CharacterGrabbing.RPCA_StartReaching()。</summary>
        public static void Postfix(CharacterGrabbing __instance)
        {
            try
            {
                if (!PhotonNetwork.IsMasterClient) return;
                if (__instance == null) return;
                if (HostClassManager.Instance == null) return;

                var character = __instance.GetComponent<Character>();
                if (character == null) return;

                HostClassManager.Instance.OnPacifistReachPressed(character);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[RollOfTheScout] 和平？·处理右键事件时异常：{e.Message}");
            }
        }
    }

    /// <summary>
    /// 自恋狂·精致 / 化学中毒：感知「用完 1 瓶防晒喷雾」。
    ///
    /// 拦截点选原版 <c>Action_ReduceUses.ReduceUsesRPC()</c>：它是 <c>[PunRPC]</c>，
    /// 由使用者客户端在 <c>Action_ReduceUses.RunAction</c> 里以 <c>RpcTarget.All</c> 广播，
    /// **主机也收得到**；该 RPC 内部把物品的 <c>ItemUses</c> 递减，故 Postfix 里
    /// 「递减后 == 0」即「这瓶用完了」，与预制体的 <c>consumeOnFullyUsed</c> 无关，最可靠。
    /// （另一条兜底通道见 <see cref="HostClassManager.OnNarcissistSunscreenConsumed"/>：
    ///  若原版开启 <c>consumeOnFullyUsed</c>，用完会再走 <c>Item.Consume</c> 广播。）
    ///
    /// 与其它 <c>[PunRPC]</c> 补丁同理，用宽松解析（按名）应对 PEAKERRpcInfo 追加
    /// <c>PhotonMessageInfo</c> 参数导致签名变化。
    /// </summary>
    public static class SunscreenUsePatch
    {
        /// <summary><c>ItemActionBase.item</c>（protected 字段），用于从动作组件反查所属物品。</summary>
        private static readonly System.Reflection.FieldInfo ItemField =
            HarmonyLib.AccessTools.Field(typeof(ItemActionBase), "item");

        /// <summary>对应 Action_ReduceUses.ReduceUsesRPC()。</summary>
        public static void Postfix(Action_ReduceUses __instance)
        {
            try
            {
                if (!PhotonNetwork.IsMasterClient) return;
                if (__instance == null) return;
                if (HostClassManager.Instance == null) return;

                var item = ItemField != null ? ItemField.GetValue(__instance) as Item : null;
                if (item == null) return;

                var holder = item.holderCharacter;
                if (holder == null) return;

                // 递减后仍 > 0 → 这瓶还没用完
                var uses = item.GetData<OptionableIntItemData>(DataEntryKey.ItemUses);
                if (uses == null || !uses.HasData || uses.Value != 0) return;

                // 由该方法统一做「物品名 = 防晒喷雾」「职业 = 自恋狂」与去重校验
                HostClassManager.Instance.OnNarcissistSunscreenConsumed(item, holder);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[RollOfTheScout] 防晒喷雾·处理用完事件时异常：{e.Message}");
            }
        }
    }

    /// <summary>
    /// 自恋狂·精致（补充）：感知「谁获得了防晒状态」。
    ///
    /// 拦截点选原版 <c>Affliction_Sunscreen</c> 的两个回调：
    /// 1. <c>OnApplied()</c> —— 首次套上防晒状态时调用（<c>CharacterAfflictions.AddAffliction</c> 的「新增」分支）；
    /// 2. <c>Stack(Affliction)</c> —— 已处于防晒状态时再次套上（刷新）时调用。
    /// 两者合起来恰好覆盖「每一次获得防晒状态」，且互不重叠（首次走 OnApplied、刷新走 Stack），每次事件恰好结算一次。
    ///
    /// 主机视角：主机自身角色走本地 <c>AddAffliction</c>；远端角色走 <c>SyncAfflictionsRPC</c> →
    /// <c>AddAffliction(fromRPC:true)</c>（首次）或直接 <c>Stack</c>（刷新），因此零客机安装也感知得到。
    /// 角色对象从 <c>Affliction.character</c> 字段取得（原版在调用回调前已赋值）。
    /// </summary>
    public static class NarcissistSunscreenStatusPatch
    {
        /// <summary>对应 Affliction_Sunscreen.OnApplied()。</summary>
        public static void OnAppliedPostfix(Peak.Afflictions.Affliction_Sunscreen __instance)
        {
            Handle(__instance);
        }

        /// <summary>对应 Affliction_Sunscreen.Stack(Affliction incomingAffliction)。</summary>
        public static void StackPostfix(Peak.Afflictions.Affliction_Sunscreen __instance)
        {
            Handle(__instance);
        }

        private static void Handle(Peak.Afflictions.Affliction_Sunscreen affliction)
        {
            try
            {
                if (!PhotonNetwork.IsMasterClient) return;
                if (affliction == null) return;
                if (HostClassManager.Instance == null) return;

                // 原版在调用 OnApplied / Stack 前已把 character 赋好（见 CharacterAfflictions.AddAffliction）
                var character = affliction.character;
                if (character == null) return;

                // 由该方法统一做「职业 = 自恋狂」校验并结算额外体力
                HostClassManager.Instance.OnNarcissistSunscreenStatusGained(character);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[RollOfTheScout] 防晒状态·处理获得事件时异常：{e.Message}");
            }
        }
    }

    /// <summary>
    /// 乐队成员·乐器（拾取）：乐队成员拾取任何物品时，按权重把该物品随机替换为
    /// 小号 <c>Bugle</c> / 童军领队的号角 <c>Bugle_Scoutmaster Variant</c> / 友谊喇叭 <c>Bugle_Magic</c>，
    /// 或「保持不变」；命中「乐器-黑名单」的物品永不被替换。
    ///
    /// 拦截点与刺客/野人/死灵法师完全一致：原版 <c>Player.AddItem(ushort itemID, ItemInstanceData, out ItemSlot)</c>
    /// —— 物品真正进入玩家背包/物品栏的唯一漏斗，拾取时由主机执行，天然满足「仅主机安装」。
    /// 与其它挂在 Player.AddItem 上的补丁共用同一拦截点（Harmony 支持多 Prefix；玩家单一职业，不冲突）。
    /// </summary>
    public static class BandPickupPatch
    {
        /// <summary>
        /// 内部发放物品时临时置位，避免把本模组自己发放的物品也随机替换掉。
        /// 与 <see cref="AssassinPickupPatch.PushSuppress"/> 同套写法。
        /// </summary>
        private static int _suppress;

        public static void PushSuppress() => _suppress++;

        public static void PopSuppress()
        {
            if (_suppress > 0) _suppress--;
        }

        /// <summary>黑名单解析缓存（配置字符串 → 预制体名集合）。</summary>
        private static string _blacklistRaw;
        private static readonly HashSet<string> _blacklist = new HashSet<string>();

        /// <summary>该 itemID 对应的预制体名是否在「乐器-黑名单」中。</summary>
        private static bool IsPickupBlacklisted(ushort itemID, out string itemName)
        {
            itemName = null;
            string raw = ModConfig.Band.InstrumentBlacklist.Value;
            if (raw != _blacklistRaw)
            {
                _blacklistRaw = raw;
                _blacklist.Clear();
                foreach (var name in WeightedPicker.ParseList(raw)) _blacklist.Add(name);
            }
            if (_blacklist.Count == 0) return false;

            if (!ItemDatabase.TryGetItem(itemID, out Item item) || item == null) return false;
            itemName = item.gameObject.name;
            return _blacklist.Contains(itemName);
        }

        /// <summary>对应 Player.AddItem(ushort itemID, ItemInstanceData instanceData, out ItemSlot slot)。</summary>
        public static void Prefix(Player __instance, ref ushort itemID)
        {
            try
            {
                if (_suppress > 0) return;
                if (!PhotonNetwork.IsMasterClient) return;
                if (__instance == null) return;
                if (HostClassManager.Instance == null) return;

                var character = __instance.character;
                if (character == null) return;

                // 只有乐队成员触发；其他职业获得的物品保持原样
                if (HostClassManager.Instance.GetClassOf(character) != ClassType.Band) return;

                // 黑名单：命中的物品永不替换（默认排除诡异菇、三种乐器本体、信号棒与 5 件童军护符）
                if (IsPickupBlacklisted(itemID, out string blockedName))
                {
                    Plugin.Log.LogInfo(
                        $"[RollOfTheScout] 乐器：{character.characterName} 拾取 {blockedName} 命中黑名单，保持原样。");
                    return;
                }

                // 食物（带 Action_RestoreHunger 的可食用物品）：不参与乐器替换，保持原样。
                // 与炊事员 / 特级厨师的「食物」判定同一口径。
                if (ItemDatabase.TryGetItem(itemID, out Item foodItem) && foodItem != null
                    && foodItem.GetComponent<Action_RestoreHunger>() != null)
                {
                    Plugin.Log.LogInfo(
                        $"[RollOfTheScout] 乐器：{character.characterName} 拾取 {foodItem.gameObject.name} 为食物，保持原样。");
                    return;
                }

                // 四选一：不变 / 小号 / 号角 / 友谊喇叭
                int noChange = Mathf.Max(0, ModConfig.Band.InstrumentNoChangeWeight.Value);
                int wBugle = Mathf.Max(0, ModConfig.Band.InstrumentBugleWeight.Value);
                int wScout = Mathf.Max(0, ModConfig.Band.InstrumentScoutmasterWeight.Value);
                int wMagic = Mathf.Max(0, ModConfig.Band.InstrumentMagicWeight.Value);
                if (noChange == 0 && wBugle == 0 && wScout == 0 && wMagic == 0) return;

                var pool = new List<WeightedItem>
                {
                    new WeightedItem(null, noChange),
                    new WeightedItem(ItemNames.Bugle, wBugle),
                    new WeightedItem(ItemNames.BugleScoutmaster, wScout),
                    new WeightedItem(ItemNames.BugleMagic, wMagic),
                };
                string targetName = WeightedPicker.Pick(pool);
                if (string.IsNullOrEmpty(targetName)) return; // 「不变」分支

                if (!ItemDatabase.TryGetItem(targetName, out Item target) || target == null) return;
                if (itemID == target.itemID) return;

                itemID = target.itemID;
                Plugin.Log.LogInfo(
                    $"[RollOfTheScout] 乐器：{character.characterName} 获得的物品被替换为 {targetName}。");
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[RollOfTheScout] 乐器·替换物品时异常：{e.Message}");
            }
        }
    }

    /// <summary>
    /// 乐队成员·演奏：感知「谁吹响乐器」。
    ///
    /// 拦截点选原版 <c>BugleSFX.RPC_StartToot(int clip, float pitch)</c>：它是 <c>[PunRPC]</c>，
    /// 由持乐器的那一方客户端在 <c>UpdateTooting()</c> 里以 <c>RpcTarget.All</c> 广播（开始吹各一次），
    /// **主机必然收到** —— 于是主机可零客机安装地感知「某玩家开始吹响乐器」。
    ///
    /// 角色反查：优先 <c>item.holderCharacter</c>（由原版装备链路设置，可靠），
    /// 兜底用 <c>BugleSFX.photonView.Owner</c>（乐器由持方实例化，owner 即持方）。
    /// 预制体名反查：经 <c>Item.itemID</c> 回查 <c>ItemDatabase</c> 预制体名（取数据库名，避免 (Clone) 后缀）。
    /// 与其它 <c>[PunRPC]</c> 补丁同理，用宽松解析（按名 + 首参）挂载，Postfix 只声明 __instance。
    /// </summary>
    public static class BandBuglePatch
    {
        /// <summary>对应 BugleSFX.RPC_StartToot(int clip, float pitch)。</summary>
        public static void StartTootPostfix(BugleSFX __instance)
        {
            Handle(__instance);
        }

        private static void Handle(BugleSFX sfx)
        {
            try
            {
                if (!PhotonNetwork.IsMasterClient) return;
                if (sfx == null) return;
                if (HostClassManager.Instance == null) return;

                var item = sfx.GetComponent<Item>();
                var holder = item != null ? item.holderCharacter : null;
                if (holder == null)
                {
                    var owner = sfx.photonView != null ? sfx.photonView.Owner : null;
                    if (owner == null) return;
                    holder = HostClassManager.Instance.FindCharacterOfPlayer(owner);
                }
                if (holder == null) return;

                // 乐器预制体名：优先用数据库里的预制体名（实例名可能带 (Clone) 后缀）
                string prefabName = null;
                if (item != null)
                {
                    if (ItemDatabase.TryGetItem(item.itemID, out Item prefab) && prefab != null)
                        prefabName = prefab.gameObject.name;
                    else
                        prefabName = item.gameObject.name;
                }

                HostClassManager.Instance.OnBandBlowStarted(holder, prefabName);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[RollOfTheScout] 演奏·处理吹响事件时异常：{e.Message}");
            }
        }
    }

    /// <summary>
    /// 领队·迈尔斯残魂·【封印之力】（拾取）：除「特殊物品」外，拾取任何物品时物品直接消失、不进背包。
    ///
    /// 拦截点与潘多拉之子 / 圣人 / 挑战者完全一致：原版 <c>Item.RequestPickup(PhotonView characterView)</c>
    /// —— 它是 <c>[PunRPC]</c>，由拾取方以 <c>RpcTarget.MasterClient</c> 调用，天然只在主机执行；
    /// 且「直接消失」必须走这里才能在销毁世界物品的同时跳过原版入包。
    ///
    /// 特殊物品（默认：诡异菇、信号棒、童军的毅力 / 进取 / 盛情 / 野心 / 荣耀）正常拾取；
    /// 若拾取的是童军宝石道具，还额外通知主机结算「本局第一次拾取」效果（见
    /// <see cref="HostClassManager.OnMilesGemPicked"/>）。
    ///
    /// 与其它挂在 Item.RequestPickup 上的补丁共用同一拦截点（Harmony 支持多 Prefix；玩家单一职业，不冲突）。
    /// </summary>
    public static class MilesPickupPatch
    {
        /// <summary>特殊物品名集合的解析缓存（配置字符串 → 预制体名集合）。</summary>
        private static string _specialRaw;
        private static readonly HashSet<string> _special = new HashSet<string>();

        private static bool IsSpecial(string name)
        {
            string raw = ModConfig.MilesSoul.SpecialItems.Value;
            if (raw != _specialRaw)
            {
                _specialRaw = raw;
                _special.Clear();
                foreach (var n in WeightedPicker.ParseList(raw)) _special.Add(n);
            }
            return _special.Contains(name);
        }

        /// <summary>对应 Item.RequestPickup(PhotonView characterView)。</summary>
        public static bool Prefix(Item __instance, PhotonView characterView)
        {
            try
            {
                if (!PhotonNetwork.IsMasterClient) return true;
                if (__instance == null || characterView == null) return true;
                if (HostClassManager.Instance == null) return true;
                if (!ModConfig.MilesSoul.SealEnabled.Value) return true;

                var character = characterView.GetComponent<Character>();
                if (character == null) return true;

                // 只有迈尔斯残魂触发；其他职业拾取物品保持原样
                if (HostClassManager.Instance.GetClassOf(character) != ClassType.MilesSoul) return true;

                string name = null;
                if (ItemDatabase.TryGetItem(__instance.itemID, out Item prefab) && prefab != null)
                    name = prefab.gameObject.name;

                if (name != null && IsSpecial(name))
                {
                    // 特殊物品：正常拾取（其中童军宝石还需结算「本局第一次拾取」）
                    HostClassManager.Instance.OnMilesGemPicked(character, name);
                    return true;
                }

                // 其余物品：直接消失（销毁世界物品并跳过原版入包）
                Plugin.Log.LogInfo(
                    $"[RollOfTheScout] 封印之力：{character.characterName} 拾取的 {name ?? __instance.itemID.ToString()} 直接消失了。");
                HostApi.ConsumePickedItem(__instance);
                return false;
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[RollOfTheScout] 封印之力·处理拾取时异常：{e.Message}");
                return true;
            }
        }
    }

    /// <summary>
    /// 领队·迈尔斯残魂·【童军的盛情】（克隆成功）：感知「谁用童军的盛情成功复制了物品」。
    ///
    /// 拦截点选原版 <c>Action_CloneSelectedItem.RPCA_Shoot(Vector3 endPoint, bool success)</c>：
    /// 它是 <c>[PunRPC]</c>，由使用者客户端在 <c>RunAction</c> 末尾以 <c>RpcTarget.All</c> 广播，
    /// **主机也收得到**；<c>success == true</c> 即代表该次使用成功复制了物品（否则是失败表现）。
    /// 使用者由 <c>ItemActionBase.item.holderCharacter</c> 反查（与防晒喷雾补丁同法）。
    ///
    /// 注意：与其它 <c>[PunRPC]</c> 补丁同理，用宽松解析（按名 + 首参 Vector3）应对
    /// PEAKERRpcInfo 追加 <c>PhotonMessageInfo</c> 参数导致的签名变化。
    /// </summary>
    public static class MilesClonePatch
    {
        /// <summary><c>ItemActionBase.item</c>（protected 字段），用于从动作组件反查所属物品。</summary>
        private static readonly System.Reflection.FieldInfo ItemField =
            HarmonyLib.AccessTools.Field(typeof(ItemActionBase), "item");

        /// <summary>对应 Action_CloneSelectedItem.RPCA_Shoot(Vector3 endPoint, bool success)。</summary>
        public static void Postfix(Peak.Action_CloneSelectedItem __instance, bool success)
        {
            try
            {
                if (!PhotonNetwork.IsMasterClient) return;
                if (__instance == null || !success) return;
                if (HostClassManager.Instance == null) return;

                var item = ItemField != null ? ItemField.GetValue(__instance) as Item : null;
                if (item == null) return;

                var holder = item.holderCharacter;
                if (holder == null) return;

                HostClassManager.Instance.OnMilesGemUsed(holder, ItemNames.AmuletClone, cloneSuccess: true);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[RollOfTheScout] 盛情·处理克隆成功事件时异常：{e.Message}");
            }
        }
    }

    /// <summary>
    /// 领队·迈尔斯残魂·【封印之力】（宝石「直接使用」感知）：
    /// 童军宝石道具（毅力 / 进取 / 野心）在原版里是「口袋行为（pocket behavior）」，按下使用键时
    /// 由**使用者的客户端本地**执行对应动作（<c>Action_StrangeGem.RunAction → AmuletBase.ToggleGem</c>、
    /// <c>Action_HealingGem.RunAction</c>、<c>Action_SuperJumpAmulet.RunAction</c>），
    /// 既不广播 RPC、也不经过 <c>Item.Consume</c>，因此主机无法通过「食用事件」感知。
    ///
    /// 这里挂这三个**本地动作**的 Postfix 来捕获「谁使用了哪件宝石」：
    /// 角色由物品的 <c>holderCharacter</c> 反查，宝石名经 <c>Item.itemID</c> 回查 <c>ItemDatabase</c>。
    /// （<c>Action_CloneSelectedItem</c> 的克隆走 <see cref="MilesClonePatch"/> 的广播 RPC，不在此列。）
    ///
    /// ⚠ 局限：这些动作只在**使用者本人**的客户端执行，因此仅当「使用者就是主机本人」时可被感知；
    /// 客机玩家使用宝石时主机收不到信号（原版无对应 RPC 可借）。这与「仅主机安装」约束下
    /// 少数本地动作类效果（如普通小号吹响）的局限相同。
    /// </summary>
    public static class MilesGemUsePatch
    {
        /// <summary><c>ItemActionBase.item</c>（protected 字段），用于从动作组件反查所属物品。</summary>
        private static readonly System.Reflection.FieldInfo ItemField =
            HarmonyLib.AccessTools.Field(typeof(ItemActionBase), "item");

        /// <summary>对应 <c>AmuletBase.ToggleGem()</c>（宝石「使用」时把 PowerEnabled 置真）。</summary>
        public static void ToggleGemPostfix(Peak.AmuletBase __instance)
        {
            try
            {
                if (!PhotonNetwork.IsMasterClient) return;
                if (__instance == null || HostClassManager.Instance == null) return;

                var item = __instance.item;
                if (item == null) return;
                // 直接使用 = 对自己使用（喂食走的是 FinishCastSecondary，不在此列）
                HostClassManager.Instance.NotifyMilesGemUsed(item, item.holderCharacter,
                    HostClassManager.MilesGemUsage.SelfUse);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[RollOfTheScout] 宝石使用(ToggleGem)·异常：{e.Message}");
            }
        }

        /// <summary>对应 <c>Action_HealingGem.RunAction()</c>（童军的毅力）。</summary>
        public static void HealingGemPostfix(Peak.Action_HealingGem __instance) => FromAction(__instance);

        /// <summary>对应 <c>Action_SuperJumpAmulet.RunAction()</c>（童军的进取）。</summary>
        public static void SuperJumpAmuletPostfix(Peak.Action_SuperJumpAmulet __instance) => FromAction(__instance);

        // ---------------- Prefix：记录「使用前」石化值（零号守则的抵消基准） ----------------
        // 这些动作执行时/之后，原版才把宝石的石化代价加到使用者身上；
        // 因此必须在**动作之前**取一次石化值，零号守则才能把「本次追加的石化」整段清除。

        /// <summary>对应 <c>AmuletBase.ToggleGem()</c> 的 Prefix（记录使用前石化）。</summary>
        public static void ToggleGemPrefix(Peak.AmuletBase __instance)
        {
            try
            {
                if (!PhotonNetwork.IsMasterClient || __instance == null || HostClassManager.Instance == null) return;
                Snapshot(__instance.item);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[RollOfTheScout] 宝石使用(ToggleGem)·前缀异常：{e.Message}");
            }
        }

        /// <summary>对应 <c>Action_HealingGem.RunAction()</c> 的 Prefix。</summary>
        public static void HealingGemPrefix(Peak.Action_HealingGem __instance) => FromActionPrefix(__instance);

        /// <summary>对应 <c>Action_SuperJumpAmulet.RunAction()</c> 的 Prefix。</summary>
        public static void SuperJumpAmuletPrefix(Peak.Action_SuperJumpAmulet __instance) => FromActionPrefix(__instance);

        private static void FromActionPrefix(ItemActionBase action)
        {
            try
            {
                if (!PhotonNetwork.IsMasterClient || action == null || HostClassManager.Instance == null) return;
                Snapshot(ItemField != null ? ItemField.GetValue(action) as Item : null);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[RollOfTheScout] 宝石使用(RunAction)·前缀异常：{e.Message}");
            }
        }

        /// <summary>记录该宝石持有者的「使用前」石化值（仅童军宝石、仅残魂）。</summary>
        internal static void Snapshot(Item item)
        {
            if (item == null) return;
            var holder = item.holderCharacter;
            if (holder == null) return;
            if (!ItemDatabase.TryGetItem(item.itemID, out Item prefab) || prefab == null) return;
            if (!HostClassManager.IsMilesGemName(prefab.gameObject.name)) return;
            HostClassManager.Instance.SnapshotMilesPreUsePetrify(holder);
        }

        private static void FromAction(ItemActionBase action)
        {
            try
            {
                if (!PhotonNetwork.IsMasterClient) return;
                if (action == null || HostClassManager.Instance == null) return;

                var item = ItemField != null ? ItemField.GetValue(action) as Item : null;
                if (item == null) return;
                HostClassManager.Instance.NotifyMilesGemUsed(item, item.holderCharacter,
                    HostClassManager.MilesGemUsage.SelfUse);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[RollOfTheScout] 宝石使用(RunAction)·异常：{e.Message}");
            }
        }
    }

    /// <summary>
    /// 领队·迈尔斯残魂·【封印之力 / 零号守则】（宝石「喂食」感知 —— <b>主机自己喂食</b>）。
    ///
    /// 童军宝石最主要的用法是**喂给附近的队友**（毅力救人），而喂食链路
    /// <c>Item.FinishCastSecondary → FeedItem + FeederAction</c>：
    ///   · 不经过 <c>RunAction</c> / <c>ToggleGem</c>（<see cref="MilesGemUsePatch"/> 挂的那三条都收不到）；
    ///   · 也不经过 <c>Item.Consume</c> 的「消费者」通道（被喂的是队友、不是残魂本人，职业检查会直接跳过）。
    /// 因此主机本人喂宝石时，此前**完全没有任何感知通道**。
    ///
    /// 本补丁用 Prefix/Postfix 的 <c>__state</c> 传递「这次是喂食、且被喂者是否昏迷」：
    ///   · Prefix 在喂食动作**之前**记录喂食者的石化（零号守则的「使用前」基准），
    ///     并把 <see cref="HostClassManager.MilesGemUsage"/> 写进 <c>__state</c>（<c>Unknown</c> = 本次没走喂食分支）；
    ///   · Postfix 按 <c>__state</c> 结算（喂给昏迷者 = 零号守则；喂给未昏迷者 = 无职业效果）。
    /// 客机的喂食由 <see cref="MilesFeedStartPatch"/> + 启发式轮询共同覆盖。
    /// </summary>
    public static class MilesFeedGemPatch
    {
        /// <summary><c>Interaction.bestCharacter</c>（<c>internal CharacterInteractible</c>）—— 当前喂食/交互目标。</summary>
        private static readonly System.Reflection.FieldInfo BestCharacterField =
            HarmonyLib.AccessTools.Field(typeof(Interaction), "bestCharacter");

        /// <summary>取当前「对友方使用」的目标角色（没有合法目标则返回 null）。</summary>
        private static Character GetFeedTarget()
        {
            if (Interaction.instance == null || BestCharacterField == null) return null;
            var interactible = BestCharacterField.GetValue(Interaction.instance) as CharacterInteractible;
            return interactible != null ? interactible.character : null;
        }

        /// <summary>对应 <c>Item.FinishCastSecondary()</c>（喂食动作完成）。</summary>
        public static void Prefix(Item __instance, out HostClassManager.MilesGemUsage __state)
        {
            __state = HostClassManager.MilesGemUsage.Unknown;
            try
            {
                if (!PhotonNetwork.IsMasterClient) return;
                if (__instance == null || HostClassManager.Instance == null) return;
                if (!__instance.canUseOnFriend) return;

                var target = GetFeedTarget();
                if (target == null) return;   // 不是「对友方使用」分支（空放 / 非喂食物品）

                __state = HostClassManager.IsDowned(target)
                    ? HostClassManager.MilesGemUsage.FeedDowned
                    : HostClassManager.MilesGemUsage.FeedNormal;

                MilesGemUsePatch.Snapshot(__instance);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[RollOfTheScout] 喂食宝石·前缀异常：{e.Message}");
            }
        }

        /// <summary>对应 <c>Item.FinishCastSecondary()</c> 的 Postfix（按喂食者与喂食情形结算）。</summary>
        public static void Postfix(Item __instance, HostClassManager.MilesGemUsage __state)
        {
            try
            {
                if (__state == HostClassManager.MilesGemUsage.Unknown) return;
                if (!PhotonNetwork.IsMasterClient) return;
                if (__instance == null || HostClassManager.Instance == null) return;

                var holder = __instance.holderCharacter;
                if (holder == null) return;
                if (!ItemDatabase.TryGetItem(__instance.itemID, out Item prefab) || prefab == null) return;
                if (!HostClassManager.IsMilesGemName(prefab.gameObject.name)) return;

                HostClassManager.Instance.NotifyMilesGemUsed(__instance, holder, __state);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[RollOfTheScout] 喂食宝石·异常：{e.Message}");
            }
        }
    }

    /// <summary>
    /// 领队·迈尔斯残魂·【封印之力 / 零号守则】（宝石「喂食」感知 —— <b>任意玩家（含客机）</b>）。
    ///
    /// 原版喂食在**开始时**由喂食者以 <c>RpcTarget.All</c> 广播
    /// <c>Item.SendFeedDataRPC(giverID, recieverID, itemID, totalUsingTime)</c>（见 <c>Item.StartUseSecondary</c>），
    /// 主机据此即可还原「谁在喂谁、喂的哪件宝石」—— 这正是区分毅力三种情形所必需的
    /// 「被喂者是否昏迷」（客机喂食时主机看不到被喂者）。
    ///
    /// 这里只把这条信息记成「喂食意图」（<see cref="HostClassManager.OnMilesFeedStarted"/>）；
    /// 真正的结算仍由其他通道（喂食补丁 / 客机启发式）触发，结算时再认领该意图。
    /// </summary>
    public static class MilesFeedStartPatch
    {
        /// <summary>对应 <c>Item.SendFeedDataRPC(int giverID, int recieverID, int itemID, float totalUsingTime)</c>。</summary>
        public static void Postfix(Item __instance, int giverID, int recieverID, float totalUsingTime)
        {
            try
            {
                if (!PhotonNetwork.IsMasterClient) return;
                if (__instance == null || HostClassManager.Instance == null) return;

                if (!ItemDatabase.TryGetItem(__instance.itemID, out Item prefab) || prefab == null) return;
                if (!HostClassManager.IsMilesGemName(prefab.gameObject.name)) return;

                var giver = FindCharacterByViewId(giverID);
                var receiver = FindCharacterByViewId(recieverID);
                if (giver == null || receiver == null) return;

                HostClassManager.Instance.OnMilesFeedStarted(giver, receiver, prefab.gameObject.name, totalUsingTime);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[RollOfTheScout] 喂食开始·异常：{e.Message}");
            }
        }

        /// <summary>按 PhotonViewID 在场上角色里反查（避免依赖 Photon 的 Find 封装）。</summary>
        private static Character FindCharacterByViewId(int viewId)
        {
            var all = Character.AllCharacters;
            if (all == null) return null;
            for (int i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (c != null && c.photonView != null && c.photonView.ViewID == viewId) return c;
            }
            return null;
        }
    }

    /// <summary>
    /// 领队·迈尔斯残魂·【封印之力】（宝石「激活」感知，客机可靠通道）。
    ///
    /// <c>AmuletBase.ToggleGem()</c> 在把 <c>PowerEnabled</c> 置真后，会以 <c>RpcTarget.All</c> 广播
    /// <c>RPC_GemActiveChanged(bool)</c> —— 注意这条 RPC 是 <c>ToggleGem</c> **自己直接发的**
    /// （不依赖 <c>SetGemActive</c>），因此**即使是 <c>oneWayUse => true</c> 的童军宝石也会广播**。
    /// 这意味着主机能零客机安装地收到**任意玩家**（含客机）的宝石激活信号，
    /// 不再依赖「手执宝石 + 石化增量」的启发式猜测（<see cref="HostClassManager.PollMilesRemoteGemUse"/>）。
    ///
    /// 与本地动作补丁 / 启发式轮询共用同一次结算（由 HostClassManager 内的 0.3s 去重窗口合并）。
    /// </summary>
    public static class MilesGemActivePatch
    {
        /// <summary>对应 <c>AmuletBase.RPC_GemActiveChanged(bool active)</c>。</summary>
        public static void Postfix(Peak.AmuletBase __instance, bool active)
        {
            try
            {
                if (!PhotonNetwork.IsMasterClient) return;
                if (__instance == null || HostClassManager.Instance == null) return;
                if (!active) return;

                var item = __instance.item;
                if (item == null) return;
                if (!ItemDatabase.TryGetItem(item.itemID, out Item prefab) || prefab == null) return;
                if (!HostClassManager.IsMilesGemName(prefab.gameObject.name)) return;   // 只认童军宝石（排除奇怪水晶等）

                var holder = item.holderCharacter;
                if (holder == null) return;
                // 激活 = 持宝者对自己使用
                HostClassManager.Instance.NotifyMilesGemUsed(item, holder, HostClassManager.MilesGemUsage.SelfUse);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[RollOfTheScout] 宝石激活广播·异常：{e.Message}");
            }
        }
    }

    /// <summary>
    /// 领队·迈尔斯残魂·【封印之力】（宝石「本局第一次拾取」感知，兜底通道）：
    /// <see cref="MilesPickupPatch"/> 挂在 <c>Item.RequestPickup</c>，但在「物品栏已满、物品被收纳到
    /// 主手临时槽」等路径下可能不触发，导致「本局第一次拾取 -5 诅咒」漏发（需玩家丢弃再拾取才行）。
    ///
    /// 这里再挂 <c>Player.AddItem</c> —— 物品真正进入玩家物品栏 / 主手槽的唯一漏斗（拾取、克隆、发放都经过它），
    /// 保证「宝石第一次进入玩家手中」一定能被结算。与 <see cref="MilesPickupPatch"/> 互为备份，
    /// 由 <c>HostClassManager.OnMilesGemPicked</c> 内部的「本局已首拾」集合去重，不会重复扣诅咒。
    /// 与刺客 / 野人 / 死灵法师 / 乐队成员共用同一拦截点（Harmony 支持多 Prefix；玩家单一职业，不冲突）。
    /// </summary>
    public static class MilesAddItemPickupPatch
    {
        /// <summary>对应 Player.AddItem(ushort itemID, ItemInstanceData instanceData, out ItemSlot slot)。</summary>
        public static void Prefix(Player __instance, ushort itemID)
        {
            try
            {
                if (!PhotonNetwork.IsMasterClient) return;
                if (__instance == null || HostClassManager.Instance == null) return;
                if (!ModConfig.MilesSoul.SealEnabled.Value) return;

                var character = __instance.character;
                if (character == null) return;
                if (HostClassManager.Instance.GetClassOf(character) != ClassType.MilesSoul) return;

                if (!ItemDatabase.TryGetItem(itemID, out Item prefab) || prefab == null) return;
                HostClassManager.Instance.OnMilesGemPicked(character, prefab.gameObject.name);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[RollOfTheScout] 封印之力·处理入包首拾时异常：{e.Message}");
            }
        }
    }

    // ===============================================================
    // 区（Qu）
    // ===============================================================
    /// <summary>
    /// 区·【分解者】（拾取）：区拾取「分解者物品池」中的物品时，物品直接消失，并使自身饥饿下降。
    ///
    /// 拦截点与潘多拉之子·潘多拉 / 圣人·完美主义完全一致：原版 <c>Item.RequestPickup(PhotonView characterView)</c>
    /// —— 它是 <c>[PunRPC]</c>，由拾取方以 <c>RpcTarget.MasterClient</c> 调用，天然只在主机执行；
    /// 且「直接消失」必须走这里才能在销毁世界物品的同时跳过原版入包
    /// （<c>Player.AddItem</c> 那条路只能换 itemID / 拒绝拾取，拒绝时原版会走 <c>DenyPickupRPC</c> 把物品放回地面）。
    ///
    /// 与潘多拉 / 圣人 / 挑战者 / 迈尔斯共用同一拦截点（Harmony 支持多 Prefix；玩家单一职业，不冲突）。
    /// 同样用宽松解析应对 PEAKERRpcInfo 给 <c>[PunRPC]</c> 追加 PhotonMessageInfo 参数。
    /// </summary>
    public static class QuDecomposerPickupPatch
    {
        /// <summary>对应 Item.RequestPickup(PhotonView characterView)。</summary>
        public static bool Prefix(Item __instance, PhotonView characterView)
        {
            try
            {
                if (!PhotonNetwork.IsMasterClient) return true;
                if (__instance == null || characterView == null) return true;

                var host = HostClassManager.Instance;
                if (host == null) return true;
                if (!ModConfig.General.Enabled.Value || !ModConfig.Qu.Enabled.Value) return true;
                if (!ModConfig.Qu.DecomposerEnabled.Value) return true;
                if (!host.HasQuInRoom) return true;

                var character = characterView.GetComponent<Character>();
                if (character == null) return true;

                // 只有区触发；其他职业拾取物品保持原样
                if (host.GetClassOf(character) != ClassType.Qu) return true;
                if (!host.IsDecomposerItem(__instance.itemID, out string itemName)) return true;

                // 直接消失：销毁世界物品并跳过原版入包
                Plugin.Log.LogInfo($"[RollOfTheScout] 分解者：{character.characterName} 拾取的 {itemName} 被直接分解了。");
                HostApi.ConsumePickedItem(__instance);
                host.OnQuDecomposed(character);
                return false;
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[RollOfTheScout] 分解者·处理拾取时异常：{e.Message}");
                return true;
            }
        }
    }

    // ===============================================================
    // 头顶职业前缀（显示类，与职业逻辑无关）
    // ===============================================================
    /// <summary>
    /// 「头顶职业前缀」：把头顶玩家名称改写成「【职业】昵称」。
    ///
    /// 挂 <c>UIPlayerNames.UpdateName(int index, Vector3 position, bool visible, int speakingAmplitude)</c>
    /// 的 Postfix —— 该方法由 <c>IsLookedAt.Update()</c> 对**场上每个其它玩家每帧**调用，是唯一
    /// 稳定、每帧都会经过、且能拿到「该名称对应角色」的挂点（名称文本本身由
    /// <c>UIPlayerNames.Init</c> 内的协程只设置一次，之后不再变动，无法在那里挂到职业已知的时刻）。
    ///
    /// ⚠ 这是纯**本地 UI** 改动：只影响**本机屏幕**上看到的名字。职业数据来源见
    /// <see cref="HostClassManager.OnOverheadNameTick"/>（主机用本地账本，客机用主机广播的数据）。
    /// </summary>
    public static class DisplayNamePatch
    {
        /// <summary>对应 <c>UIPlayerNames.UpdateName(int, Vector3, bool, int)</c>。</summary>
        public static void Postfix(UIPlayerNames __instance, int index)
        {
            var host = HostClassManager.Instance;
            if (host == null) return;
            try
            {
                host.OnOverheadNameTick(__instance, index);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[RollOfTheScout] 头顶职业前缀·异常：{e.Message}");
            }
        }
    }

    // ===============================================================
    // 太空人（Astronaut）
    // ===============================================================

    /// <summary>
    /// 太空人·【轻飘飘】/【飞起来】（拾取）：
    ///   · 【轻飘飘】拾取「气球 Balloon」或「一束气球 BalloonBunch」时物品**直接消失**；
    ///   · 【飞起来】拾取非食物物品（或配置允许时的食物）时，按概率变为火箭背包 / 喷气背包：
    ///     命中即拾取物消失，并在脚下生成对应背包。
    ///
    /// 与潘多拉之子 / 圣人 / 挑战者 / 迈尔斯残魂 / 区 共用同一拦截点
    /// <c>Item.RequestPickup(PhotonView characterView)</c>：它是 <c>[PunRPC]</c>，
    /// 由拾取方以 <c>RpcTarget.MasterClient</c> 调用，天然只在主机执行；且「直接消失」必须走这里，
    /// 才能在销毁世界物品的同时跳过原版入包（<c>Player.AddItem</c> 那条路只能换 itemID / 拒绝拾取，
    /// 拒绝时原版会走 <c>DenyPickupRPC</c> 把物品放回地面）。
    ///
    /// 具体判定与「脚下生成背包」全部由 <see cref="HostClassManager.OnAstronautPickup"/> 完成。
    /// </summary>
    public static class AstronautPickupPatch
    {
        /// <summary>对应 Item.RequestPickup(PhotonView characterView)。</summary>
        public static bool Prefix(Item __instance, PhotonView characterView)
        {
            try
            {
                if (!PhotonNetwork.IsMasterClient) return true;
                if (__instance == null || characterView == null) return true;

                var host = HostClassManager.Instance;
                if (host == null) return true;
                if (!ModConfig.General.Enabled.Value || !ModConfig.Astronaut.Enabled.Value) return true;

                var character = characterView.GetComponent<Character>();
                if (character == null) return true;

                // 只有太空人触发；其他职业拾取物品保持原样
                if (host.GetClassOf(character) != ClassType.Astronaut) return true;

                if (!host.OnAstronautPickup(__instance, character)) return true;

                // 直接消失：销毁世界物品并跳过原版入包
                HostApi.ConsumePickedItem(__instance);
                return false;
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[RollOfTheScout] 太空人·处理拾取时异常：{e.Message}");
                return true;
            }
        }
    }

    /// <summary>
    /// 太空人·【太空食品安全】（食用）：食用「包装食物」与「烤熟的食物」以外的食物时 +中毒。
    ///
    /// 与转职 / 野人·野性未泯 / 待皓·模因污染共用同一拦截点 <c>Item.Consume(int consumerID)</c>
    /// （Harmony 支持多 Prefix；玩家单一职业，不冲突）。另外 <c>GlobalEvents.OnItemConsumed</c> 兜底，
    /// 两条通道由 <see cref="HostClassManager.OnAstronautItemConsumed"/> 内的去重窗口保证只生效一次。
    /// </summary>
    public static class AstronautEatPatch
    {
        /// <summary>对应 Item.Consume(int consumerID)。</summary>
        public static void Prefix(Item __instance, int consumerID)
        {
            try
            {
                if (!PhotonNetwork.IsMasterClient) return;
                if (__instance == null) return;
                if (HostClassManager.Instance == null) return;

                var view = PhotonNetwork.GetPhotonView(consumerID);
                if (view == null) return;
                var character = view.GetComponent<Character>();
                if (character == null || character.player == null) return;

                HostClassManager.Instance.OnAstronautItemConsumed(__instance, character);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[RollOfTheScout] 太空食品安全·处理食用事件时异常：{e.Message}");
            }
        }
    }

    /// <summary>
    /// 榔王（食用）：【精品大果】/【上瘾】/【纤维化】/【没吃的赶紧戒】。
    ///
    /// 与上面的食用补丁同挂 <c>Item.Consume(int consumerID)</c>（Harmony 支持多 Prefix；
    /// 玩家单一职业，不冲突），并由 <c>GlobalEvents.OnItemConsumed</c> 兜底；
    /// 两条通道由 <see cref="HostClassManager.OnLangwangItemConsumed"/> 内的去重窗口保证只生效一次。
    /// </summary>
    public static class LangwangEatPatch
    {
        /// <summary>对应 Item.Consume(int consumerID)。</summary>
        public static void Prefix(Item __instance, int consumerID)
        {
            try
            {
                if (!PhotonNetwork.IsMasterClient) return;
                if (__instance == null) return;
                if (HostClassManager.Instance == null) return;

                var view = PhotonNetwork.GetPhotonView(consumerID);
                if (view == null) return;
                var character = view.GetComponent<Character>();
                if (character == null || character.player == null) return;

                HostClassManager.Instance.OnLangwangItemConsumed(__instance, character);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[RollOfTheScout] 榔王·处理食用事件时异常：{e.Message}");
            }
        }
    }

    /// <summary>
    /// 混乱末影人·【混乱】（拾取）：拾取物品时有一定概率物品直接消失，并强制对自己使用一次潘多拉餐盒。
    ///
    /// 与潘多拉之子 / 圣人 / 挑战者 / 迈尔斯残魂 / 区 / 太空人 共用同一拦截点
    /// <c>Item.RequestPickup(PhotonView characterView)</c>：它是 <c>[PunRPC]</c>，
    /// 由拾取方以 <c>RpcTarget.MasterClient</c> 调用，天然只在主机执行；且「直接消失」必须走这里，
    /// 才能在销毁世界物品的同时跳过原版入包。
    ///
    /// 具体判定与「强制使用潘多拉餐盒」全部由 <see cref="HostClassManager.OnChaosEndermanPickup"/> 完成。
    /// </summary>
    public static class ChaosEndermanPickupPatch
    {
        /// <summary>对应 Item.RequestPickup(PhotonView characterView)。</summary>
        public static bool Prefix(Item __instance, PhotonView characterView)
        {
            try
            {
                if (!PhotonNetwork.IsMasterClient) return true;
                if (__instance == null || characterView == null) return true;

                var host = HostClassManager.Instance;
                if (host == null) return true;
                if (!ModConfig.General.Enabled.Value || !ModConfig.ChaosEnderman.Enabled.Value) return true;

                var character = characterView.GetComponent<Character>();
                if (character == null) return true;

                // 只有混乱末影人触发；其他职业拾取物品保持原样
                if (host.GetClassOf(character) != ClassType.ChaosEnderman) return true;

                if (!host.OnChaosEndermanPickup(__instance, character)) return true;

                // 直接消失：销毁世界物品并跳过原版入包
                HostApi.ConsumePickedItem(__instance);
                return false;
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[RollOfTheScout] 混乱末影人·处理拾取时异常：{e.Message}");
                return true;
            }
        }
    }

    /// <summary>
    /// 混乱末影人·【末影】（食用）：食用运动饮料 / 万灵药后 +损伤。
    ///
    /// 与转职 / 野人 / 待皓 / 太空人 / 榔王共用 <c>Item.Consume(int consumerID)</c>
    /// （Harmony 支持多 Prefix；玩家单一职业，不冲突），并由 <c>GlobalEvents.OnItemConsumed</c> 兜底；
    /// 两条通道由 <see cref="HostClassManager.OnChaosEndermanItemConsumed"/> 内的去重窗口保证只生效一次。
    /// </summary>
    public static class ChaosEndermanEatPatch
    {
        /// <summary>对应 Item.Consume(int consumerID)。</summary>
        public static void Prefix(Item __instance, int consumerID)
        {
            try
            {
                if (!PhotonNetwork.IsMasterClient) return;
                if (__instance == null) return;
                if (HostClassManager.Instance == null) return;

                var view = PhotonNetwork.GetPhotonView(consumerID);
                if (view == null) return;
                var character = view.GetComponent<Character>();
                if (character == null || character.player == null) return;

                HostClassManager.Instance.OnChaosEndermanItemConsumed(__instance, character);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[RollOfTheScout] 混乱末影人·处理食用事件时异常：{e.Message}");
            }
        }
    }

    /// <summary>
    /// 混乱末影人·【末影】（丢弃）：自身**直接丢弃**曲迁菇时立即清除该物品；**蓄力扔出**则保持原版行为。
    ///
    /// 原版所有丢出（普通丢弃 / 蓄力扔出 / DropAllItems）最终都会调用
    /// <c>Item.RPC_SetThrownData(int characterID, float thrownAmount)</c>（RpcTarget.All），
    /// 第二个参数就是蓄力值：直接丢弃 = 0，蓄力扔出 &gt; 0。该 RPC 主客机都会执行，
    /// 主机据此即可稳定区分两种行为，**不需要客机安装任何模组**。
    ///
    /// 判定与延迟销毁见 <see cref="HostClassManager.OnChaosEndermanItemDropped"/> / <see cref="HostClassManager.ProcessChaosPendingDropClears"/>。
    /// </summary>
    public static class ChaosEndermanDropClearPatch
    {
        /// <summary>对应 Item.RPC_SetThrownData(int characterID, float thrownAmount)。</summary>
        public static void Prefix(Item __instance, int characterID, float thrownAmount)
        {
            try
            {
                if (!PhotonNetwork.IsMasterClient) return;
                if (__instance == null) return;
                if (HostClassManager.Instance == null) return;
                if (!ModConfig.General.Enabled.Value || !ModConfig.ChaosEnderman.Enabled.Value) return;

                var view = PhotonNetwork.GetPhotonView(characterID);
                if (view == null) return;
                var character = view.GetComponent<Character>();
                if (character == null) return;

                HostClassManager.Instance.OnChaosEndermanItemDropped(__instance, character, thrownAmount);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[RollOfTheScout] 混乱末影人·处理丢弃事件时异常：{e.Message}");
            }
        }
    }

    /// <summary>
    /// 程序员·【溢出】：把「(当前额外体力 + 本次增量) 超过上限」的结果改为**取模**。
    ///
    /// 原版 <c>Character.AddExtraStamina(float add)</c> 内部为
    /// <c>data.extraStamina += add; Clamp(extraStamina, 0f, 1f - petrify×0.01f)</c>，
    /// 超过上限的部分会被直接丢弃；本补丁在方法执行**之前**把 <c>add</c> 改写为
    /// 「(当前 + add) 取模后的值 − 当前值」，从而真正落地「溢出取模」（例：95 点 + 10 点 → 5 点）。
    /// 仅对程序员生效。
    ///
    /// ⚠ 该方法受 <c>view.IsMine</c> 保护，只在**角色拥有者自己的客户端**上执行；本模组仅主机安装，
    /// 因此本补丁实际覆盖「主机自己的角色」的额外体力增加（含食用食物等本地来源）。
    /// 「主机下发给任意玩家的额外体力增加」由 <see cref="HostApi.ExtraStaminaAdjust"/> 钩子覆盖。
    /// </summary>
    public static class ProgrammerOverflowPatch
    {
        /// <summary>对应 Character.AddExtraStamina(float add)。</summary>
        public static void Prefix(Character __instance, ref float add)
        {
            try
            {
                if (__instance == null || add <= 0f) return;
                if (!ModConfig.Programmer.OverflowEnabled.Value) return;

                var host = HostClassManager.Instance;
                if (host == null) return;
                if (host.GetClassOf(__instance) != ClassType.Programmer) return;

                float limit = ModConfig.Programmer.OverflowLimit.Value / 100f;   // 100 点 → 1.0
                if (limit <= 0f) return;

                float cur = __instance.data != null ? __instance.data.extraStamina : 0f;
                float sum = cur + add;
                if (sum <= limit) return;

                add = Mathf.Repeat(sum, limit) - cur;
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[RollOfTheScout] 程序员·溢出处理额外体力时异常：{e.Message}");
            }
        }
    }

    /// <summary>
    /// 背人状态自愈（主机侧兜底）：修复「被背起后永远吸附在背人者身上、死亡复活也不解除」。
    ///
    /// 原版背人的解除契约（反编译 CharacterCarrying）：
    ///   · <c>Update()</c> 中的自动放下**只由背人者的拥有者客户端执行**
    ///     （条件末尾带 <c>character.refs.view.IsMine</c>）：当被背者 <c>dead</c> / <c>!fullyPassedOut</c>、
    ///     或背人者 <c>fullyPassedOut</c> / <c>dead</c> 时调用 <c>Drop()</c> → 广播 <c>RPCA_Drop</c>。
    ///   · <c>RPCA_Die</c>、<c>ReviveCharacter</c>、<c>RPCA_ReviveAtPosition</c> 都**不会**清理
    ///     <c>isCarried</c> / <c>carrier</c>；<c>CarrierGone()</c> 也只恢复碰撞、不落标记。
    /// 因此只要那一次「放下」没有真正执行，被背者的 <c>carrier</c> 就永久残留：
    /// <c>CharacterCarrying.FixedUpdate</c> 每帧朝 <c>carrier.refs.carryPosRef</c> 施加 500 的力，
    /// 表现为「粘在背人者身上、无法攀爬」，且死亡→复活后仍然如此（同一 Character 对象）。
    ///
    /// 本模组的【火线救援】恰好最容易踩中这时机：把救火队员**直接传送到昏迷者身上**
    /// （<c>WarpPlayerRPC</c>）后立刻背起，再由【温暖臂膀】在背上为被背者治疗、促使其在背上苏醒 ——
    /// 苏醒那一刻正是原版唯一解除契约的触发点。
    ///
    /// 本补丁让**主机**在「本该放下却没放下」时代为广播原版 <c>RPCA_Drop</c>（无需客机安装）：
    ///   ① 背人者仍记着被背者，但被背者已死亡 / **曾在背上昏迷且已苏醒**，或背人者已死亡 / 昏迷
    ///      （仅对主机不拥有的角色执行，拥有者客户端自己会正常放下）；
    ///   ② 被背者残留 <c>isCarried</c> / <c>carrier</c>（背人者掉线、自己已死亡、或自己曾昏迷且已苏醒）——
    ///      **这一条对主机自己的角色同样执行**：原版只由背人者广播放下，被背者自己从不清理，
    ///      因此主机被客机背起时同样可能永久吸附。
    ///
    /// **与 Piggyback 等「允许背起清醒玩家」的第三方模组兼容**：原实现把「被背者未昏迷」一律当作异常，
    /// 于是只要有人用 Piggyback 背起清醒玩家，0.5 秒后就会被主机强行拆散（实测反馈：联机无法背起未昏迷玩家）。
    /// 现在改为记录「**本次背人期间被背者是否曾昏迷 / 死亡**」：只有经历过昏迷的被背者苏醒后才代为放下
    /// （复刻原版「苏醒即放下」）；全程清醒的背人（Piggyback）不干预。检测到 Piggyback 时更进一步——
    /// 连「昏迷后苏醒」也不再拆散（该模组本就允许继续背着清醒玩家）。
    /// 异常状态需「持续 0.5 秒」才动手（避免与正常进行的背人抢跑）；RPC 若再丢失会按 0.5 秒重试。
    /// </summary>
    public static class CarryReleaseWatchdogPatch
    {
        /// <summary>「异常状态」需要持续多久才代为放下（秒）。</summary>
        private const float ConfirmSeconds = 0.5f;

        /// <summary>角色 PhotonView.ViewID → 首次观测到「异常状态」的时刻（Time.time）。</summary>
        private static readonly System.Collections.Generic.Dictionary<int, float> SinceSeen =
            new System.Collections.Generic.Dictionary<int, float>();

        /// <summary>
        /// 被背者 PhotonView.ViewID → **本次被背期间**是否曾处于昏迷 / 死亡状态。
        /// 只有经历过昏迷的被背者，苏醒后才应回到原版「苏醒即放下」；全程清醒的背人不算异常。
        /// 背人结束时清除该记录（见 Postfix 末尾）。
        /// </summary>
        private static readonly System.Collections.Generic.Dictionary<int, bool> DownedDuringCarry =
            new System.Collections.Generic.Dictionary<int, bool>();

        /// <summary>是否检测到「允许背起清醒玩家」的第三方模组（当前已知：Piggyback）。null = 尚未检测。</summary>
        private static bool? _awakeCarryModPresent;
        private static bool _awakeCarryModLogged;

        /// <summary>
        /// 检测「允许背起清醒玩家」的第三方模组。三重兜底：已加载程序集名 → BepInEx 插件 GUID → 配置文件存在。
        /// 结果缓存（只判定一次），任何一步异常都不影响主逻辑。
        /// </summary>
        private static bool AwakeCarryModPresent()
        {
            if (_awakeCarryModPresent.HasValue) return _awakeCarryModPresent.Value;

            bool found = false;

            try
            {
                foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    var name = asm != null ? asm.GetName().Name : null;
                    if (string.IsNullOrEmpty(name)) continue;
                    if (name.IndexOf("piggyback", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        found = true;
                        break;
                    }
                }
            }
            catch { /* 枚举程序集失败：忽略 */ }

            if (!found)
            {
                try
                {
                    foreach (var key in BepInEx.Bootstrap.Chainloader.PluginInfos.Keys)
                    {
                        if (key != null && key.IndexOf("piggyback", System.StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            found = true;
                            break;
                        }
                    }
                }
                catch { /* BepInEx API 差异：忽略 */ }
            }

            if (!found)
            {
                try
                {
                    string cfg = System.IO.Path.Combine(
                        BepInEx.Paths.ConfigPath, "nakazora.peak.piggyback.cfg");
                    if (System.IO.File.Exists(cfg)) found = true;
                }
                catch { /* 路径 API 差异：忽略 */ }
            }

            _awakeCarryModPresent = found;
            if (found && !_awakeCarryModLogged)
            {
                _awakeCarryModLogged = true;
                Plugin.Log.LogInfo(
                    "[RollOfTheScout] 检测到「允许背起清醒玩家」的第三方模组（Piggyback）。"
                    + "背人自愈将只处理被背者死亡 / 背人者倒地 / 背人者消失，不再把「背起清醒玩家」当作异常拆散。");
            }
            return found;
        }

        // 原版 CharacterCarrying 的私有字段 character（在 Start 里赋值）。
        // 直接按字段读取，避免**每帧每个角色**都调用一次 GetComponent（原生调用，是这条每帧补丁里最贵的部分）。
        private static HarmonyLib.AccessTools.FieldRef<CharacterCarrying, Character> _characterRef;
        private static bool _characterRefResolved;

        /// <summary>取 CharacterCarrying 对应的 Character（字段直读；解析失败才退回 GetComponent）。</summary>
        private static Character GetCharacter(CharacterCarrying carrying)
        {
            if (!_characterRefResolved)
            {
                _characterRefResolved = true;   // 只尝试一次：失败则永久使用 GetComponent 兜底
                try
                {
                    _characterRef = HarmonyLib.AccessTools.FieldRefAccess<CharacterCarrying, Character>("character");
                }
                catch
                {
                    _characterRef = null;
                }
            }

            if (_characterRef != null)
            {
                try
                {
                    var cached = _characterRef(carrying);
                    if (cached != null) return cached;
                }
                catch { /* 字段读取异常：走兜底 */ }
            }
            return carrying.GetComponent<Character>();
        }

        /// <summary>对应 CharacterCarrying.Update()（原版为私有方法，每帧在每个角色上执行）。</summary>
        public static void Postfix(CharacterCarrying __instance)
        {
            try
            {
                if (!PhotonNetwork.IsMasterClient) return;
                if (__instance == null) return;

                var character = GetCharacter(__instance);
                if (character == null || character.data == null) return;

                var view = character.photonView;
                if (view == null) return;

                var carried = character.data.carriedPlayer;
                var carrier = character.data.carrier;
                bool carriedFlag = character.data.isCarried;

                // ---- 记录「本次背人期间被背者是否曾昏迷 / 死亡」 ----
                // 这是兼容 Piggyback 的关键：全程清醒的被背（允许背起清醒玩家的模组）不能被当作异常；
                // 只有「昏迷 → 苏醒」才复刻原版「苏醒即放下」。
                if (carried != null && carried.data != null && carried.photonView != null)
                {
                    if (carried.data.fullyPassedOut || carried.data.dead)
                        DownedDuringCarry[carried.photonView.ViewID] = true;
                }
                if (carriedFlag)
                {
                    if (character.data.fullyPassedOut || character.data.dead)
                        DownedDuringCarry[view.ViewID] = true;
                }
                else if (carrier == null && DownedDuringCarry.Count > 0)
                {
                    // 自己已不处于「被背」状态：本次背人结束，清掉期间标记
                    DownedDuringCarry.Remove(view.ViewID);
                }

                bool awakeCarryMod = AwakeCarryModPresent();
                bool carriedWasDowned = carried != null && carried.photonView != null
                    && DownedDuringCarry.TryGetValue(carried.photonView.ViewID, out bool carriedDowned)
                    && carriedDowned;
                bool selfWasDowned = DownedDuringCarry.TryGetValue(view.ViewID, out bool selfDowned)
                    && selfDowned;

                // ---- ① 背人者仍记着被背者，但这次早该放下了 ----
                // 拥有者客户端会执行原版放下判定（Update 条件末尾带 refs.view.IsMine），
                // 因此只对「主机不拥有的角色」介入，避免与拥有者抢发同一份 RPC。
                // 「被背者未昏迷」只有在**本次背人期间曾昏迷过**时才算异常（苏醒）；
                // 检测到 Piggyback 时连苏醒也不拆散（该模组允许继续背着清醒玩家）。
                bool carriedOk = carried != null && carried.data != null && carried.photonView != null;
                bool awakeStale = carriedOk && !carried.data.fullyPassedOut && !carried.data.dead
                    && carriedWasDowned && !awakeCarryMod;
                bool carrierStale = !view.IsMine && carriedOk
                    && (carried.data.dead || awakeStale
                        || character.data.fullyPassedOut || character.data.dead);

                // ---- ② 自己是被背的一方：早该解除，但「被背」标记仍挂着 ----
                // 原版只有「背人者的客户端」会广播 RPCA_Drop，被背者自己永远不会清理，
                // 所以这一条对「主机自己的角色」也必须执行 —— 否则主机被客机背起、
                // 而那次 RPCA_Drop 丢失时，主机自己也会永久吸附（实测日志里主机就被背起过）。
                // 三种情形：背人者已不存在（掉线 / 角色销毁，原版 CarrierGone 只恢复碰撞）、
                // 自己已死亡、自己曾昏迷且已苏醒。
                //
                // 自己正背着别人时不能走这条通道：RPCA_Drop 会把自己背的人一起放下。
                bool selfAwakeStale = !character.data.fullyPassedOut && !character.data.dead
                    && selfWasDowned && !awakeCarryMod;
                bool victimStale = carried == null
                    && (carriedFlag || carrier != null)
                    && (carrier == null || character.data.dead || selfAwakeStale);

                if (!carrierStale && !victimStale)
                {
                    // 状态正常：清掉计时，下次异常重新计时（无记录时跳过字典探测）
                    if (SinceSeen.Count > 0) SinceSeen.Remove(view.ViewID);
                    return;
                }

                // 异常必须「持续」一段时间才动手：RPCA_PassOut / RPCA_StartCarry 来自不同发送端，
                // RPC 到达顺序不保证，单帧内的状态不一致可能只是正常的中间态，
                // 立刻代为放下会误伤正在正常进行的背人。
                float now = Time.time;
                if (!SinceSeen.TryGetValue(view.ViewID, out float since))
                {
                    SinceSeen[view.ViewID] = now;
                    return;
                }
                if (now - since < ConfirmSeconds) return;
                SinceSeen[view.ViewID] = now;   // 确认后重新计时：RPC 若再丢失，按 0.5s 继续重试

                if (carrierStale)
                {
                    string reason = carried.data.dead ? "被背者已死亡"
                        : awakeStale ? "被背者曾在背上昏迷后苏醒"
                        : character.data.dead ? "背人者已死亡"
                        : "背人者已昏迷";

                    // 与拥有者客户端同一条通道：广播原版 RPCA_Drop（RpcTarget.All）
                    view.RPC("RPCA_Drop", RpcTarget.All, carried.photonView);
                    if (carried.photonView != null) DownedDuringCarry.Remove(carried.photonView.ViewID);
                    Plugin.Log.LogInfo(
                        $"[RollOfTheScout] 背人状态自愈：{NameOf(character)} 早该放下 {NameOf(carried)}"
                        + $"（{reason}），主机代为广播放下。");
                    return;
                }

                // 以自己的 PhotonView 为目标广播 RPCA_Drop：该 RPC 内部以「接收端角色」为 component，
                // 因此会把自身的 isCarried / carrier 一并清掉（上一条已确保自身没有背着别人）。
                view.RPC("RPCA_Drop", RpcTarget.All, view);
                DownedDuringCarry.Remove(view.ViewID);
                Plugin.Log.LogInfo(
                    $"[RollOfTheScout] 背人状态自愈：{NameOf(character)} 仍残留「被背」标记"
                    + $"（{(carrier == null ? "背人者已不存在" : character.data.dead ? "自己已死亡" : "自己曾昏迷后苏醒")}），"
                    + "主机代为清除。");
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[RollOfTheScout] 背人状态自愈异常：{e.Message}");
            }
        }

        private static string NameOf(Character character)
        {
            if (character == null) return "(null)";
            return string.IsNullOrEmpty(character.characterName) ? "(未命名)" : character.characterName;
        }
    }

    /// <summary>
    /// 捣蛋鬼·拾取变糖：拾取**可食用物品**时按概率替换为随机糖果；
    /// 命中「拾取变糖黑名单」的物品（诡异菇 / 糖果本身 / 万灵药 / 潘多拉餐盒）不受影响。
    ///
    /// 与潘多拉 / 圣人 / 挑战者 / 迈尔斯残魂 / 区 / 太空人 / 混乱末影人 / 石像鬼 / 炼金术士共用
    /// <c>Item.RequestPickup(PhotonView characterView)</c>（<c>[PunRPC]</c>，<c>RpcTarget.MasterClient</c>，
    /// 只在主机执行）。只有这里能在销毁世界物品的同时跳过原版入包；
    /// 糖果由主机销毁原物品后通过 <c>HostApi.GiveItemAsPickup</c> 按原版拾取落点发放。
    ///
    /// 性能：先做 O(1) 的职业判定与概率判定，只有「确属捣蛋鬼 + 可食用 + 未命中黑名单」时才解析糖果池；
    /// 糖果池 / 黑名单的解析结果均在 <see cref="HostClassManager"/> 内按配置原文缓存。
    /// </summary>
    public static class TricksterPickupPatch
    {
        /// <summary>对应 Item.RequestPickup(PhotonView characterView)。</summary>
        public static bool Prefix(Item __instance, PhotonView characterView)
        {
            try
            {
                if (!PhotonNetwork.IsMasterClient) return true;
                if (__instance == null || characterView == null) return true;

                var host = HostClassManager.Instance;
                if (host == null) return true;
                if (!ModConfig.General.Enabled.Value || !ModConfig.Trickster.Enabled.Value) return true;

                var character = characterView.GetComponent<Character>();
                if (character == null) return true;
                if (host.GetClassOf(character) != ClassType.Trickster) return true;

                var result = host.OnTricksterPickup(__instance, character, out string candyPrefab);
                if (result == HostClassManager.TricksterPickupResult.Keep) return true;

                // 先销毁世界物品，再把糖果按原版拾取落点发放；返回 false 跳过原版入包。
                HostApi.ConsumePickedItem(__instance);

                if (!string.IsNullOrEmpty(candyPrefab))
                {
                    if (!HostApi.GiveItemAsPickup(character, candyPrefab, out _))
                    {
                        HostApi.SpawnItemAtFeet(character, candyPrefab);
                        Plugin.Log.LogWarning(
                            $"[RollOfTheScout] 拾取变糖：{character.characterName} 的 {candyPrefab} 未能入包，已改为生成在脚下。");
                    }
                }

                return false;
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[RollOfTheScout] 捣蛋鬼·处理拾取时异常：{e.Message}");
                return true;
            }
        }
    }

    /// <summary>
    /// 捣蛋鬼·【蛀牙】：食用棉花糖 / 大棒棒糖后自身 +损伤。
    ///
    /// 与转职 / 野人 / 待皓 / 太空人 / 榔王 / 混乱末影人共用 <c>Item.Consume(int consumerID)</c>
    /// （<c>[PunRPC]</c>，食用者以 <c>RpcTarget.All</c> 广播，主机也收得到）。
    /// <c>GlobalEvents.OnItemConsumed</c> 为兜底通道，两条通道由去重窗口保证只生效一次。
    /// </summary>
    public static class TricksterEatPatch
    {
        /// <summary>对应 Item.Consume(int consumerID)。</summary>
        public static void Prefix(Item __instance, int consumerID)
        {
            try
            {
                if (!PhotonNetwork.IsMasterClient) return;
                if (__instance == null) return;
                if (HostClassManager.Instance == null) return;
                if (!ModConfig.General.Enabled.Value || !ModConfig.Trickster.Enabled.Value) return;

                var view = PhotonNetwork.GetPhotonView(consumerID);
                if (view == null) return;
                var character = view.GetComponent<Character>();
                if (character == null) return;

                HostClassManager.Instance.OnTricksterItemConsumed(__instance, character);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[RollOfTheScout] 捣蛋鬼·处理食用事件时异常：{e.Message}");
            }
        }
    }
}
