using System;
using System.Collections.Generic;
using RollOfTheScout.Patches;
using Photon.Pun;
using UnityEngine;
using Zorro.Core;

namespace RollOfTheScout
{
    /// <summary>
    /// 对原版「主机可作用于任意远端玩家」通道的封装。
    /// 所有方法**只能在主机（MasterClient）上调用**。
    /// 证据见 Mod/方案B_职业设计文档.md 第一节接口清单。
    /// </summary>
    public static class HostApi
    {
        /// <summary>STATUSTYPE 枚举数量（float[] 按此下标索引）。</summary>
        public const int StatusArrayLength = 15;

        // ---------------------------------------------------------------
        // 通道 #1：RPC_ApplyStatusesFromFloatArray
        //   守卫：info.Sender.IsMasterClient
        //   内部：AddStatus/SubtractStatus(..., fromRPC: true) 绕过 IsMine
        //   排除：Weight(7) / Thorns(9) / Arrow(12)
        // ---------------------------------------------------------------

        /// <summary>给目标角色施加一个状态增量（正值变糟，负值治疗/净化）。</summary>
        public static void ApplyStatus(Character target, CharacterAfflictions.STATUSTYPE type, float amount)
        {
            if (target == null || Mathf.Approximately(amount, 0f)) return;
            int idx = (int)type;
            if (idx < 0 || idx >= StatusArrayLength) return;

            var data = new float[StatusArrayLength];
            data[idx] = amount;
            SendStatuses(target, data);
        }

        /// <summary>
        /// 削减「被原版状态数组通道硬性排除」的三项（`Weight(7)` / `Thorns(9)` / `Arrow(12)`）之一。
        /// 原版 <c>ApplyStatusesFromFloatArray</c> 对这三项**一律跳过**（本地路径同样跳过，见技术文档 §1.1 通道 #1 的排除表），
        /// 唯一可行的办法是直接调用**拥有者本机**的 <c>SubtractStatus</c>：
        ///   · 目标就是主机自己的角色 → 立即生效，并由原版内部（`!fromRPC` 分支）<c>PushStatuses()</c> 广播给其他人；
        ///   · 目标是客机角色 → 返回 false（主机直接调用会被原版 `!IsMine &amp;&amp; !fromRPC` 拦掉），由调用方自行决定是否记日志。
        /// </summary>
        public static bool TrySubtractStatusLocalOnly(Character target, CharacterAfflictions.STATUSTYPE type, float amount)
        {
            if (target == null || amount <= 0f) return false;
            var view = target.photonView;
            if (view == null || !view.IsMine) return false;
            if (target.refs == null || target.refs.afflictions == null) return false;

            target.refs.afflictions.SubtractStatus(type, amount);
            return true;
        }

        /// <summary>
        /// 一次性向同一目标下发「两种状态增量」，由单条 RPC 承载两个下标。
        /// 用于「同一时刻对同一玩家施加两项状态」的效果（童军·集体、野人·自然韧性），
        /// 以减少 RPC 数量（网络开销）。与依次调用两次 <see cref="ApplyStatus"/> 语义完全等价：
        /// 原版 ApplyStatusesFromFloatArray 会跳过为 0 的下标，故两下标互不影响。
        /// </summary>
        public static void ApplyStatusPair(Character target, CharacterAfflictions.STATUSTYPE typeA, float amountA,
            CharacterAfflictions.STATUSTYPE typeB, float amountB)
        {
            bool hasA = !Mathf.Approximately(amountA, 0f);
            bool hasB = !Mathf.Approximately(amountB, 0f);
            if (!hasA && !hasB) return;
            if (!hasB) { ApplyStatus(target, typeA, amountA); return; }
            if (!hasA) { ApplyStatus(target, typeB, amountB); return; }

            if (target == null) return;
            int ia = (int)typeA;
            int ib = (int)typeB;
            // 下标相同（或越界）时退回逐条下发，避免数组覆盖导致与「两次 ApplyStatus」语义不一致
            if (ia == ib || ia < 0 || ia >= StatusArrayLength) { ApplyStatus(target, typeA, amountA); ApplyStatus(target, typeB, amountB); return; }
            if (ib < 0 || ib >= StatusArrayLength) { ApplyStatus(target, typeB, amountB); ApplyStatus(target, typeA, amountA); return; }

            var data = new float[StatusArrayLength];
            data[ia] = amountA;
            data[ib] = amountB;
            SendStatuses(target, data);
        }

        /// <summary>
        /// 一次性向同一目标下发「三种状态增量」，由单条 RPC 承载三个下标
        /// （与 <see cref="ApplyStatusPair"/> 同思路，用于「同一 tick 对同一玩家施加三项状态」的效果，
        /// 如炊事员·创伤应激的损伤 / 中毒 / 困倦）。
        ///
        /// 安全性：只有当三个下标**互不相同**且都合法时才合并下发；存在下标冲突 / 越界时
        /// 退回「逐条 <see cref="ApplyStatus"/>」，因此与原实现逐条下发的语义完全一致。
        /// </summary>
        public static void ApplyStatuses(Character target,
            CharacterAfflictions.STATUSTYPE typeA, float amountA,
            CharacterAfflictions.STATUSTYPE typeB, float amountB,
            CharacterAfflictions.STATUSTYPE typeC, float amountC)
        {
            int ia = (int)typeA, ib = (int)typeB, ic = (int)typeC;
            bool okA = !Mathf.Approximately(amountA, 0f) && ia >= 0 && ia < StatusArrayLength;
            bool okB = !Mathf.Approximately(amountB, 0f) && ib >= 0 && ib < StatusArrayLength;
            bool okC = !Mathf.Approximately(amountC, 0f) && ic >= 0 && ic < StatusArrayLength;

            if ((okA || okB || okC) && target != null && ia != ib && ia != ic && ib != ic)
            {
                var data = new float[StatusArrayLength];
                if (okA) data[ia] = amountA;
                if (okB) data[ib] = amountB;
                if (okC) data[ic] = amountC;
                SendStatuses(target, data);
                return;
            }

            // 索引冲突 / 越界（或全部为 0）：退回逐条下发，保证与旧行为逐项等价
            if (okA) ApplyStatus(target, typeA, amountA);
            if (okB) ApplyStatus(target, typeB, amountB);
            if (okC) ApplyStatus(target, typeC, amountC);
        }

        /// <summary>一次性下发整条状态数组（长度 15，按 STATUSTYPE 下标填充）。</summary>
        public static void SendStatuses(Character target, float[] data)
        {
            if (target == null || data == null) return;
            var view = target.photonView;
            if (view == null) return;

            if (view.IsMine)
            {
                // 主机自己操作自己的角色，直接走本地逻辑，省一次往返
                target.refs.afflictions.ApplyStatusesFromFloatArray(data);
                return;
            }

            Photon.Realtime.Player owner = view.Owner;
            if (owner == null) return;
            view.RPC("RPC_ApplyStatusesFromFloatArray", owner, (object)data);
        }

        // ---------------------------------------------------------------
        // 通道 #2：Character.MoraleBoost(float staminaAdd, int scoutCount)
        //   无发送者校验；AddExtraStamina 受 view.IsMine 保护（在目标客户端生效）
        // ---------------------------------------------------------------

        /// <summary>
        /// 「额外体力增量改写」钩子（由 HostClassManager 注册，当前仅程序员·【溢出】使用）。
        /// 在增量下发**之前**调用，允许把「(当前额外体力 + 本次增量) 超过上限」的结果改写为取模后的增量
        /// （原版 <c>Character.AddExtraStamina</c> 会把结果 Clamp 到上限，无法产生「溢出取模」语义）。
        /// 参数：目标角色、本次拟增加的额外体力；返回改写后的增量。
        /// </summary>
        public static System.Func<Character, float, float> ExtraStaminaAdjust;

        /// <summary>给任意角色增加/削减额外体力（负值即扣体力）。</summary>
        public static void BoostExtraStamina(Character target, float amount)
        {
            if (target == null) return;
            // 程序员·【溢出】：由 HostClassManager 注册的改写钩子，在真正下发增量前把「(当前 + 增量) 超上限」的结果改为取模。
            if (ExtraStaminaAdjust != null) amount = ExtraStaminaAdjust(target, amount);
            if (Mathf.Approximately(amount, 0f)) return;
            var view = target.photonView;
            if (view == null) return;

            if (view.IsMine)
            {
                target.MoraleBoost(amount, 1);
                return;
            }
            if (view.Owner == null) return;
            view.RPC("MoraleBoost", view.Owner, amount, 1);
        }

        // ---------------------------------------------------------------
        // 通道 #3：Player.AddItem —— 仅主机可用，且会自动把 Backpack 放进背包槽
        // ---------------------------------------------------------------

        /// <summary>把一件原版物品塞进目标玩家的背包/物品栏。</summary>
        public static bool GiveItem(Character target, string prefabName, out ItemSlot slot)
        {
            slot = null;
            if (target == null || string.IsNullOrEmpty(prefabName)) return false;
            if (!PhotonNetwork.IsMasterClient) return false;

            if (!ItemDatabase.TryGetItem(prefabName, out Item itemPrefab) || itemPrefab == null)
            {
                Plugin.Log.LogWarning($"[HostApi] 未找到物品预制体：{prefabName}");
                return false;
            }

            var inventory = target.player;
            if (inventory == null) return false;

            // 内部发放不属于「拾取」，屏蔽刺客的拾取替换逻辑，避免把发放物也换成匕首
            AssassinPickupPatch.PushSuppress();
            try
            {
                return inventory.AddItem(itemPrefab.itemID, null, out slot);
            }
            finally
            {
                AssassinPickupPatch.PopSuppress();
            }
        }

        /// <summary>
        /// 只把物品放进「3 个物品栏」中：三个物品栏都满时不发放（不会塞进主手）。
        /// 原理：原版 Player.AddItem 在物品栏满时会退化到 tempFullSlot（主手/临时槽），
        /// 这里先检查物品栏是否有空位，无空位直接返回 false，从源头避免塞进主手。
        /// </summary>
        public static bool GiveItemToInventorySlots(Character target, string prefabName, out ItemSlot slot)
        {
            slot = null;
            if (string.IsNullOrEmpty(prefabName)) return false;
            if (!ItemDatabase.TryGetItem(prefabName, out Item itemPrefab) || itemPrefab == null)
            {
                Plugin.Log.LogWarning($"[HostApi] 未找到物品预制体：{prefabName}");
                return false;
            }
            return GiveItemToInventorySlots(target, itemPrefab, out slot);
        }

        /// <summary>同上，但直接使用已解析的物品预制体。</summary>
        public static bool GiveItemToInventorySlots(Character target, Item itemPrefab, out ItemSlot slot)
        {
            slot = null;
            if (target == null || itemPrefab == null) return false;
            if (!PhotonNetwork.IsMasterClient) return false;

            var inventory = target.player;
            if (inventory == null) return false;

            // 背包类物品在原版里会进「背包槽」而非 3 个物品栏，不符合本效果要求，直接跳过
            if (itemPrefab is Backpack)
            {
                Plugin.Log.LogWarning($"[HostApi] 物品 {itemPrefab.name} 属于背包类，无法放入物品栏，已跳过。");
                return false;
            }

            if (!HasEmptyItemSlot(inventory)) return false;

            // 内部发放不属于「拾取」，屏蔽刺客的拾取替换逻辑，避免把发放物也换成匕首
            AssassinPickupPatch.PushSuppress();
            try
            {
                return inventory.AddItem(itemPrefab.itemID, null, out slot);
            }
            finally
            {
                AssassinPickupPatch.PopSuppress();
            }
        }

        /// <summary>
        /// 强制把一批物品写入「物品栏最前面的 N 个槽位」（覆盖原有内容，不做空位检查）。
        /// 用于挑战者【顶级玩家】：即使 3 个物品栏已满（中途转职）或玩家刚复活（中途加入），
        /// 也能确保拿到黄金抓钩与滑翔翼。
        ///
        /// 实现沿用原版公开 API（与 <see cref="EquipBackpack"/> 的覆盖范式一致）：
        /// 先 <c>EmptyOut()</c> 清空这些槽位（旧物品连同其内容一并丢弃），再按顺序走原版
        /// <c>Player.AddItem</c> —— 它会把物品落到这些刚空出的槽位并广播 <c>SyncInventoryRPC</c>，
        /// 客机零安装也能看到。
        /// </summary>
        public static bool ForceGiveItemsToInventorySlots(Character target, IList<string> prefabNames)
        {
            if (target == null || !PhotonNetwork.IsMasterClient) return false;
            if (prefabNames == null || prefabNames.Count == 0) return false;

            var inventory = target.player;
            if (inventory == null) return false;
            var slots = inventory.itemSlots;
            if (slots == null || slots.Length == 0) return false;

            int n = Mathf.Min(prefabNames.Count, slots.Length);

            // 先全部预校验，避免「清空后才发现预制体缺失」导致本地已清空、客户端未同步的状态不一致。
            for (int i = 0; i < n; i++)
            {
                if (!ItemDatabase.TryGetItem(prefabNames[i], out Item prefab) || prefab == null)
                {
                    Plugin.Log.LogWarning($"[HostApi] 强制发放：未找到物品预制体：{prefabNames[i]}");
                    return false;
                }
                if (prefab is Backpack)
                {
                    Plugin.Log.LogWarning($"[HostApi] 强制发放：物品 {prefab.name} 属于背包类，无法放入物品栏。");
                    return false;
                }
            }

            for (int i = 0; i < n; i++) slots[i].EmptyOut();

            int ok = 0;
            for (int i = 0; i < n; i++)
            {
                if (GiveItemToInventorySlots(target, prefabNames[i], out _)) ok++;
            }
            return ok == n;
        }

        /// <summary>
        /// 把物品发放给目标玩家：优先放进 3 个物品栏；若物品栏全满，则落入主手临时槽
        /// <c>tempFullSlot</c>（物品栏下标 250），并复刻原版「拾取」流程让该角色**立即握住**它。
        ///
        /// 原版拾取链路（<c>Item.RequestPickup[MasterClient] → Player.AddItem → 属主 OnPickupAccepted</c>）中，
        /// <c>Player.AddItem</c> 在物品栏全满且未攀爬时会退化到 <c>tempFullSlot</c>；
        /// 随后主机以 <c>refs.view.RPC("OnPickupAccepted", 属主, slot.itemSlotID)</c> 通知属主
        /// <c>EquipSlot(Some(250))</c> → 角色把该物品握在手里（<c>CurrentItem</c>）。
        /// 这里等价复刻该链路，仅用于炮手【炸膛】（物品栏全满时强制握在手里）。
        /// </summary>
        public static bool GiveItemForceHold(Character target, string prefabName, out ItemSlot slot)
        {
            slot = null;
            if (target == null || string.IsNullOrEmpty(prefabName)) return false;
            if (!PhotonNetwork.IsMasterClient) return false;

            if (!ItemDatabase.TryGetItem(prefabName, out Item itemPrefab) || itemPrefab == null)
            {
                Plugin.Log.LogWarning($"[HostApi] 未找到物品预制体：{prefabName}");
                return false;
            }

            var inventory = target.player;
            if (inventory == null) return false;

            // 背包类物品在原版里会进「背包槽」而非物品栏/主手，不符合本效果要求，直接跳过
            if (itemPrefab is Backpack)
            {
                Plugin.Log.LogWarning($"[HostApi] 物品 {itemPrefab.name} 属于背包类，无法走此发放通道。");
                return false;
            }

            // 内部发放不属于「拾取」，屏蔽刺客的拾取替换逻辑，避免把发放物也换成匕首
            AssassinPickupPatch.PushSuppress();
            bool ok;
            try
            {
                ok = inventory.AddItem(itemPrefab.itemID, null, out slot);
            }
            finally
            {
                AssassinPickupPatch.PopSuppress();
            }
            if (!ok || slot == null) return false;

            // 落到主手临时槽（物品栏已满）→ 让角色握住它（复刻原版 RequestPickup 成功后的 OnPickupAccepted）
            if (slot.itemSlotID == 250)
            {
                var view = target.refs != null ? target.refs.view : null;
                var owner = inventory.photonView != null ? inventory.photonView.Owner : null;
                if (view != null && owner != null)
                    view.RPC("OnPickupAccepted", owner, slot.itemSlotID);
            }
            return true;
        }

        /// <summary>3 个物品栏中是否至少有一个空位。</summary>
        public static bool HasEmptyItemSlot(Character target)
        {
            if (target == null || target.player == null) return false;
            return HasEmptyItemSlot(target.player);
        }

        /// <summary>
        /// 该角色的「物品栏」是否已经就绪（可以接受发放）。
        ///
        /// 用于「中途加入」场景：客机的 Character / Player 是两个先后实例化的预制体，
        /// 角色刚出现时 <c>character.player</c>（以及其 <c>itemSlots</c>）可能尚未赋值，
        /// 此时发放必然失败。若代码把「失败 → 改在脚下生成」当成成功，就会在该角色**尚未被传送**
        /// 的坐标上掉出一件永远拿不到的物品（实测：骸骨之书落在 (0.08, 4961, -4999)）。
        /// 因此发放前先判就绪：未就绪时**直接跳过、下一帧再试**（不消耗重试次数）。
        /// </summary>
        public static bool IsInventoryReady(Character target)
        {
            if (target == null) return false;
            var inventory = target.player;
            return inventory != null && inventory.itemSlots != null;
        }

        private static bool HasEmptyItemSlot(Player inventory)
        {
            var slots = inventory.itemSlots;
            if (slots == null) return false;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null && slots[i].IsEmpty()) return true;
            }
            return false;
        }

        /// <summary>
        /// 强制清除目标玩家物品栏（3 个物品栏 + 主手临时槽）中所有「指定预制体」的物品，返回清除数量。
        /// 与 <see cref="ForceGiveItemsToInventorySlots"/> 的发放范式对称：走原版
        /// <c>Player.EmptySlot(slot, andBroadcast: true)</c> —— 主机调用时它会先本地清空槽位，
        /// 再向 Others 广播 <c>SyncInventoryRPC</c>（全量同步物品栏），因此客机零安装也能看到物品被移除。
        /// 用于「挑战者转职离开后清除【顶级玩家】给的黄金抓钩（`RescueHook_Infinite`）」。
        /// </summary>
        public static int RemoveItemsFromInventorySlots(Character target, string prefabName)
        {
            if (target == null || target.player == null) return 0;
            if (!PhotonNetwork.IsMasterClient) return 0;
            if (string.IsNullOrEmpty(prefabName)) return 0;
            if (!ItemDatabase.TryGetItem(prefabName, out Item prefab) || prefab == null) return 0;

            var player = target.player;
            int removed = 0;

            var slots = player.itemSlots;
            if (slots != null)
            {
                for (byte i = 0; i < slots.Length; i++)
                {
                    var slot = slots[i];
                    if (slot == null || slot.IsEmpty()) continue;
                    var item = slot.prefab;
                    if (item == null || item.itemID != prefab.itemID) continue;
                    player.EmptySlot(Optionable<byte>.Some(i), true);
                    removed++;
                }
            }

            var temp = player.tempFullSlot;
            if (temp != null && !temp.IsEmpty() && temp.prefab != null && temp.prefab.itemID == prefab.itemID)
            {
                player.EmptySlot(Optionable<byte>.Some((byte)250), true);
                removed++;
            }
            return removed;
        }

        /// <summary>
        /// 清空目标玩家的**背包槽**（下标 3），并由主机广播 <c>SyncInventoryRPC</c>（客机零安装可见）。
        /// 用于「程序员·【开挂·复制】在下次检测时清除上一轮复制到的背包类效果」。
        /// </summary>
        public static bool RemoveBackpack(Character target)
        {
            if (target == null || target.player == null) return false;
            if (!PhotonNetwork.IsMasterClient) return false;

            var player = target.player;
            if (player.backpackSlot == null || player.backpackSlot.IsEmpty()) return false;

            player.EmptySlot(Optionable<byte>.Some((byte)3), true);
            return true;
        }

        /// <summary>
        /// 目标玩家是否**已装备背包**（背包槽下标 3 非空）。
        /// 供「程序员·【开挂·复制】获得背包类效果」判断：已装备则**不放进已装备背包、也不覆盖它**。
        /// </summary>
        public static bool HasBackpack(Character target)
        {
            if (target == null || target.player == null) return false;
            var player = target.player;
            if (player.backpackSlot == null) return false;
            return !player.backpackSlot.IsEmpty();
        }

        // ---------------------------------------------------------------
        // 玩家稳定标识：用于让职业在「退出重进 / 断线重连」后仍能继承
        //   候选顺序：Photon UserId（PublishUserId，每人唯一且重连不变）→ 自定义属性 UserID
        //            → data.userID（远端副本通常为空）→ ActorNumber（最后兜底）
        //   由 ClassLedger.IdOf 逐个尝试，取第一个未被其他「在场」玩家占用的候选。
        // ---------------------------------------------------------------

        /// <summary>
        /// 按优先级填充该角色的「稳定标识候选」，供 <see cref="ClassLedger.IdOf"/> 逐个尝试：
        /// 取第一个**未被其他「在场」玩家占用**的候选 —— 这样既不会串号，又能尽量保留可继承的来源
        /// （「退出重进继承职业」依赖「重连后能解析出同一个标识」）。
        ///
        /// 顺序（越靠前越「既每人唯一、又跨重连不变」）：
        ///   ① Photon <c>Player.UserId</c>：游戏 <c>AuthValues.UserId = GetBestUserID()</c>
        ///      （SteamID，或非 Steam 环境下持久化在 PlayerPrefs 的随机数），且房间 <c>PublishUserId = true</c>
        ///      → **对房间内所有人可见、每人各不相同、重连不变**。
        ///   ② 自定义属性 <c>"UserID"</c>：由 <c>AssignUserID</c> 写入（同一来源，但可能被
        ///      <c>PlatformUserID</c> 覆盖成一份「共用值」，故排在 UserId 之后）。
        ///   ③ <c>CharacterData.userID</c>：只在**本机自己的角色**上被赋值（`Player.AssignUserID` 仅在
        ///      <c>view.IsMine</c> 时执行），远端角色在主机侧的副本通常为空 → 只作兜底。
        ///   ④ <c>ActorNumber</c>：本局内唯一，但重连后会变（**会失去职业继承**），最后的兜底。
        /// </summary>
        public static void FillStableIdCandidates(Character target, List<string> into)
        {
            if (into == null) return;

            var owner = target != null && target.photonView != null ? target.photonView.Owner : null;

            if (owner != null && !string.IsNullOrEmpty(owner.UserId))
                into.Add("u:" + owner.UserId);

            string prop = UserPropertyId(owner);
            if (!string.IsNullOrEmpty(prop)) into.Add(prop);

            if (target != null && target.data != null && !string.IsNullOrEmpty(target.data.userID))
                into.Add("u:" + target.data.userID);

            if (owner != null) into.Add("a:" + owner.ActorNumber);
        }

        /// <summary>稳定标识候选中的「最优一个」（= <see cref="FillStableIdCandidates"/> 的第一项）。</summary>
        public static string StableId(Character target)
        {
            var candidates = new List<string>(4);
            FillStableIdCandidates(target, candidates);
            return candidates.Count > 0 ? candidates[0] : null;
        }

        public static string StableId(Photon.Realtime.Player owner)
        {
            if (owner == null) return null;

            if (!string.IsNullOrEmpty(owner.UserId)) return "u:" + owner.UserId;

            string prop = UserPropertyId(owner);
            if (!string.IsNullOrEmpty(prop)) return prop;

            return "a:" + owner.ActorNumber;
        }

        /// <summary>
        /// 读取玩家自定义属性 <c>"UserID"</c> 并归一化为 <c>"u:xxx"</c>。
        /// 游戏写入的是字符串，但为稳妥起见也接受其它数值类型
        /// （Photon 的 Hashtable 值类型可能随版本变化），避免因类型不符而漏读。
        /// 返回 null 表示「该玩家没有可用账号标识」，调用方应继续向下回退。
        /// </summary>
        private static string UserPropertyId(Photon.Realtime.Player owner)
        {
            if (owner == null) return null;
            try
            {
                var props = owner.CustomProperties;
                if (props == null) return null;
                if (!props.TryGetValue("UserID", out object raw) || raw == null) return null;

                if (raw is string s)
                    return s.Length > 0 && s != "0" ? "u:" + s : null;

                string text = raw.ToString();
                return !string.IsNullOrEmpty(text) && text != "0" ? "u:" + text : null;
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogError($"[HostApi] 读取玩家自定义属性 UserID 时异常：{e.Message}");
                return null;
            }
        }

        // ---------------------------------------------------------------
        // 通道 #8：世界物品生成（主机权威）
        // ---------------------------------------------------------------

        public static GameObject SpawnWorldItem(string prefabName, Vector3 position, Quaternion rotation)
        {
            if (!PhotonNetwork.IsMasterClient || string.IsNullOrEmpty(prefabName)) return null;
            return PhotonNetwork.Instantiate("0_Items/" + prefabName, position, rotation, 0);
        }

        /// <summary>原版 <c>Character.DeathPos()</c> 的「死亡哨兵坐标」：死亡 / 尚未定位的角色会被拉到这里。</summary>
        private static readonly Vector3 DeathSentinelPosition = new Vector3(0f, 5000f, -5000f);

        /// <summary>
        /// 在角色「脚下」生成一件世界物品（物品栏已满时的兜底，落地后供其拾取）。
        /// 位置取角色躯干中心略下方，交由物理自然落地。
        ///
        /// 防护：角色仍处于「死亡哨兵坐标」（尚未复活 / 尚未 warp 到位）时直接返回 null，**不生成** ——
        /// 此时生成会落在永远拿不到的位置（实测书本 / 旗标曾落在 (0.08, 4961, -4999)）。
        /// 返回 null 会让调用方按「发放失败」处理，等角色复活到位后于后续帧重试。
        /// </summary>
        public static GameObject SpawnItemAtFeet(Character target, string prefabName)
        {
            if (target == null) return null;
            if (Vector3.Distance(target.Center, DeathSentinelPosition) < 250f) return null;
            return SpawnWorldItem(prefabName, target.Center + Vector3.down * 0.6f, Quaternion.identity);
        }

        /// <summary>「喂食」用临时物品的生成位置：场景之外，玩家看不到（同原版生成到 (0,-500,0) 的做法）。</summary>
        private static readonly Vector3 TransientSpawnPosition = new Vector3(0f, -500f, 0f);

        /// <summary>
        /// 生成一件「临时物品」，供 <see cref="Character.FeedItem"/> 使用 ——
        /// 物品在场景外生成（玩家看不到），随后由目标玩家的客户端「吃掉」它，
        /// 从而在**目标玩家自己的客户端**上触发该物品的消耗效果（如能量饮料的加速）。
        ///
        /// 这是「仅房主安装即可给所有玩家加速」的实现方式：加速是客户端本地物理量
        /// （Affliction_FasterBoi 改 movementModifier），无法靠主机直接下发，
        /// 只能借原版的喂食通道让目标客户端自行施加（参考模组 PeakRandomizedChaos 同法）。
        /// 调用方用完后需自行销毁该对象（见 HostClassManager 的延迟销毁）。
        /// </summary>
        public static GameObject SpawnTransientItem(string prefabName)
        {
            if (!PhotonNetwork.IsMasterClient || string.IsNullOrEmpty(prefabName)) return null;
            return PhotonNetwork.Instantiate("0_Items/" + prefabName, TransientSpawnPosition, Quaternion.identity, 0);
        }

        /// <summary>
        /// 构建一个「已装入若干物品的背包」的 ItemInstanceData。
        /// 与游戏自身后备箱/背包拾取一致：数据写在 BackpackData 里，随背包物品一起同步。
        /// </summary>
        private static ItemInstanceData BuildBackpackInstanceData(IList<string> prefabNames)
        {
            var instanceData = new ItemInstanceData(Guid.NewGuid());
            ItemInstanceDataHandler.AddInstanceData(instanceData);
            var backpackData = instanceData.RegisterNewEntry<BackpackData>(DataEntryKey.BackpackData);

            int slotIndex = 0;
            if (prefabNames != null)
            {
                foreach (var name in prefabNames)
                {
                    if (slotIndex >= BackpackData.slots) break;
                    if (string.IsNullOrEmpty(name)) continue;
                    if (!ItemDatabase.TryGetItem(name, out Item itemPrefab) || itemPrefab == null)
                    {
                        Plugin.Log.LogWarning($"[HostApi] 背包物品未找到：{name}");
                        continue;
                    }

                    var itemData = new ItemInstanceData(Guid.NewGuid());
                    ItemInstanceDataHandler.AddInstanceData(itemData);
                    backpackData.AddItem(itemPrefab, itemData, (byte)slotIndex);
                    slotIndex++;
                }
            }
            return instanceData;
        }

        /// <summary>
        /// 直接把一个「装满物品的背包」装备到目标玩家身上（不生成在世界里），背包用默认的 `Backpack`。
        /// 若该玩家已装备背包，则连同旧背包内的物品一起被覆盖丢弃
        /// （先清空背包槽，再走原版 Player.AddItem：它会写入背包槽并广播 SyncInventoryRPC）。
        /// </summary>
        public static bool EquipBackpack(Character target, IList<string> prefabNames)
        {
            return EquipBackpack(target, ItemNames.Backpack, prefabNames);
        }

        /// <summary>
        /// 同 <see cref="EquipBackpack(Character, IList{string})"/>，但可指定背包预制体
        /// （童军·【行李】用「滑稽背包」`Fannypack`；那时 `BackpackData.slots` 的槽位数由该背包预制体决定）。
        /// </summary>
        public static bool EquipBackpack(Character target, string backpackPrefab, IList<string> prefabNames)
        {
            if (target == null || !PhotonNetwork.IsMasterClient) return false;

            var inventory = target.player;
            if (inventory == null || inventory.backpackSlot == null) return false;

            if (string.IsNullOrEmpty(backpackPrefab)) backpackPrefab = ItemNames.Backpack;
            if (!ItemDatabase.TryGetItem(backpackPrefab, out Item backpackPrefabItem) || backpackPrefabItem == null)
            {
                Plugin.Log.LogWarning($"[HostApi] 未找到背包预制体：{backpackPrefab}");
                return false;
            }

            var instanceData = BuildBackpackInstanceData(prefabNames);

            // 已装备背包 → 直接清空背包槽，实现「覆盖」（旧背包及其内容一并丢弃）
            if (!inventory.backpackSlot.IsEmpty())
            {
                inventory.backpackSlot.EmptyOut();
            }

            return inventory.AddItem(backpackPrefabItem.itemID, instanceData, out _);
        }

        /// <summary>
        /// 随机挑一件「神秘物品」预制体。
        /// 优先使用配置的「神启-物品池」白名单（等概率抽取）；
        /// 白名单留空时才退化为「按原版 Mystical 标签筛选」——
        /// 注意原版标签会把未实装物品（例如 ClimbingChalk）也算进来，故默认使用白名单。
        /// </summary>
        public static Item PickRandomMysticalPrefab()
        {
            var pool = WeightedPicker.ParseList(ModConfig.Leader.DivineItemPool.Value);
            if (pool.Count > 0)
            {
                var whitelist = new List<Item>();
                foreach (var name in pool)
                {
                    if (ItemDatabase.TryGetItem(name, out Item item) && item != null)
                    {
                        whitelist.Add(item);
                    }
                    else
                    {
                        Plugin.Log.LogWarning($"[HostApi] 神启物品池中未找到物品，已跳过：{name}");
                    }
                }
                if (whitelist.Count == 0) return null;
                return whitelist[UnityEngine.Random.Range(0, whitelist.Count)];
            }

            var database = SingletonAsset<ItemDatabase>.Instance;
            if (database == null || database.Objects == null) return null;

            var candidates = new List<Item>();
            foreach (var item in database.Objects)
            {
                if (item == null) continue;
                if (!item.itemTags.HasFlag(Item.ItemTags.Mystical)) continue;
                if (item.itemTags.HasFlag(Item.ItemTags.NonCloneable)) continue;
                if (item.itemTags.HasFlag(Item.ItemTags.ScoutAmulet)) continue;
                candidates.Add(item);
            }
            if (candidates.Count == 0) return null;

            return candidates[UnityEngine.Random.Range(0, candidates.Count)];
        }

        /// <summary>
        /// 把一件随机神秘物品直接塞进目标玩家的 3 个物品栏；3 栏全满时返回 false（不塞主手）。
        /// </summary>
        public static bool GiveRandomMysticalItemToInventory(Character target, out string prefabName)
        {
            prefabName = null;
            var prefab = PickRandomMysticalPrefab();
            if (prefab == null) return false;
            if (!GiveItemToInventorySlots(target, prefab, out _)) return false;
            prefabName = prefab.name;
            return true;
        }

        /// <summary>
        /// 在「篝火附近」的地面上生成一件固定住的物品（kinematic，拾取前不会滚动/掉落），
        /// 做法与原版篝火附近生成棉花糖/热狗肠一致：生成后广播 SetKinematicRPC(true)。
        /// fallbackOrigin 用于在找不到篝火时定位（一般是古老雕像的位置）。
        /// </summary>
        public static GameObject SpawnItemFixedNearCampfire(string prefabName, Vector3 fallbackOrigin)
        {
            if (!PhotonNetwork.IsMasterClient || string.IsNullOrEmpty(prefabName)) return null;
            if (!ItemDatabase.TryGetItem(prefabName, out Item itemPrefab) || itemPrefab == null) return null;

            Vector3 position = FindGroundNearCampfire(fallbackOrigin);
            var go = PhotonNetwork.Instantiate("0_Items/" + prefabName, position, Quaternion.identity, 0);
            if (go == null) return null;

            var item = go.GetComponent<Item>();
            if (item != null)
            {
                // 固定：不再受重力/碰撞影响，拾取前保持原位
                item.SetKinematicNetworked(true, position, Quaternion.identity);
            }
            return go;
        }

        /// <summary>找到离参照点最近的篝火（仅场景中的实例，排除预制体资源）。</summary>
        private static Campfire FindNearestCampfire(Vector3 position)
        {
            Campfire best = null;
            float bestDist = float.MaxValue;
            var all = Resources.FindObjectsOfTypeAll<Campfire>();
            if (all == null) return null;
            for (int i = 0; i < all.Length; i++)
            {
                var campfire = all[i];
                if (campfire == null) continue;
                if (!campfire.gameObject.scene.IsValid()) continue;
                float dist = Vector3.Distance(campfire.transform.position, position);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = campfire;
                }
            }
            return best;
        }

        /// <summary>在篝火旁约 2 米处落地的位置（向下探测地面；探测失败则退回篝火旁水平位置）。</summary>
        private static Vector3 FindGroundNearCampfire(Vector3 fallbackOrigin)
        {
            Vector3 origin = fallbackOrigin;
            var campfire = FindNearestCampfire(fallbackOrigin);
            if (campfire != null) origin = campfire.transform.position;

            Vector3 dir = UnityEngine.Random.insideUnitSphere;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0001f) dir = Vector3.forward;
            dir.Normalize();

            Vector3 spot = origin + dir * 2f;
            if (Physics.Raycast(spot + Vector3.up * 4f, Vector3.down, out RaycastHit hit, 12f, ~0,
                    QueryTriggerInteraction.Ignore))
            {
                spot = hit.point;
            }
            return spot + Vector3.up * 0.05f;
        }

        // ---------------------------------------------------------------
        // 查询辅助
        // ---------------------------------------------------------------

        /// <summary>读取目标角色当前已同步到主机的状态值。</summary>
        public static float GetStatus(Character target, CharacterAfflictions.STATUSTYPE type)
        {
            if (target == null || target.refs == null || target.refs.afflictions == null) return 0f;
            return target.refs.afflictions.GetCurrentStatus(type);
        }

        // ---------------------------------------------------------------
        // 石化（Petrify）专用读写
        //   原版把「石化」单独存放在 CharacterData.petrifyAmount（0~100 的整数）：
        //     · SetStatus/AddStatus/SubtractStatus 遇到 Petrify 都直接改 data.petrifyAmount 并 return，
        //       从不写 currentStatuses[13]；SetAll（状态同步）也显式跳过下标 13。
        //     · 因此 GetCurrentStatus(Petrify) 恒为 0 —— 必须改读 data.petrifyAmount。
        //   写入用原版自带的 RPC_SyncPetrify(int)，它是「绝对赋值」且幂等，
        //   在其所属客户端上还会自动向 Others 广播，是本模组唯一可靠的石化写入通道。
        //   注意：原版复活流程会 AdjustStatus(Petrify, -0.75)（-75 点），且远端角色的这次减少
        //   要等其客户端同步回主机才可见，所以调用方需容忍时序差（见 HostClassManager 的石化校正）。
        // ---------------------------------------------------------------

        /// <summary>读取目标角色的石化值（0~100 的整数百分比）。</summary>
        public static int GetPetrify(Character target)
        {
            if (target == null || target.data == null) return 0;
            return Mathf.Clamp((int)target.data.petrifyAmount, 0, 100);
        }

        /// <summary>把目标角色的石化值设为绝对值（0~100）。幂等：重复调用不会累加。</summary>
        public static void SetPetrify(Character target, int percent)
        {
            if (target == null || target.data == null) return;

            var view = target.photonView;
            if (view == null) return;

            int amount = Mathf.Clamp(percent, 0, 100);

            if (view.IsMine)
            {
                // 主机自己的角色：直接本地赋值，原版会自动广播给 Others
                target.data.SetPetrify(amount);
                return;
            }

            if (view.Owner == null) return;
            // 远端角色：交给「该角色所属客户端」执行，由它赋值并广播给其他人
            view.RPC("RPC_SyncPetrify", view.Owner, amount);
        }

        /// <summary>
        /// 把某个「走状态数组」的状态设为绝对值（幂等：重复调用不会累加）。
        ///
        /// 原版没有「状态数组绝对赋值」的对外 RPC（SetAll 只在属主端由同步触发），
        /// 因此这里用「先清零、再设置」两步增量模拟绝对赋值：
        ///   · -1f（=100 点）远超任何状态上限，原版 SubtractStatus 会把结果夹到 [0, cap] → 实际清零；
        ///   · 随后 +value 即得到目标值。
        /// 用于修复「客机迟到初始化（ApplyReconnectData→SetAll）把主机下发的宿命/积损覆盖掉」的问题：
        /// 反复下发也是幂等的，不会叠加。
        /// </summary>
        public static void SetStatusAbsolute(Character target, CharacterAfflictions.STATUSTYPE type, float value)
        {
            if (target == null) return;
            ApplyStatus(target, type, -1f);
            if (value > 0f) ApplyStatus(target, type, value);
        }

        /// <summary>
        /// 清除目标角色的全部「负面状态」，并可选地保留诅咒 / 石化。
        /// 语义等同原版 ClearAllStatus(excludeCurse, excludePetrify)：
        /// 覆盖 Injury / Hunger / Cold / Poison / Drowsy / Hot / Spores / Web / FlyTrap
        /// （以及未排除时的 Curse / Petrify）；
        /// 与原版一致地「不清除」 Crab / Weight / Thorns / Arrow（原版 StatusIsCurable 判定这四类不可治愈）。
        /// </summary>
        public static void ClearNegativeStatuses(Character target, bool excludeCurse, bool excludePetrify)
        {
            if (target == null || target.data == null) return;

            var view = target.photonView;
            if (view == null) return;

            if (view.IsMine)
            {
                // 主机自己的角色：直接调用原生「清除全部状态」，它会在末尾 PushStatuses() 广播给其他人
                if (target.refs == null || target.refs.afflictions == null) return;
                target.refs.afflictions.ClearAllStatus(excludeCurse, excludePetrify);
                return;
            }

            if (view.Owner == null) return;

            // 远端角色：把「一个大负增量」发给该角色的所属客户端。
            //   · 属主端才是状态权威，原版 ClearAllStatus 也只在属主端生效；
            //   · 用大负增量而非「-当前值」，是为了避免主机读到的是尚未同步回来的旧值（远端状态同步有延迟）；
            //   · 原版 SubtractStatus 会把结果夹到 [0, 状态上限]，所以 -1（=100 点）足以把任何状态清零；
            //   · 下标 7/9/12（Weight/Thorns/Arrow）会被原版 ApplyStatusesFromFloatArray 跳过，与 ClearAllStatus 一致；
            //   · 下标 13（石化）在该通道里走 AddPetrify 分支，在属主端可正确清零，故需排除时置 0；
            //   · 被排除的状态传 0（不改动），其余的传 -1（清零）。
            const int curseIndex = (int)CharacterAfflictions.STATUSTYPE.Curse;
            const int petrifyIndex = (int)CharacterAfflictions.STATUSTYPE.Petrify;

            var data = new float[StatusArrayLength];
            for (int i = 0; i < StatusArrayLength; i++)
            {
                bool skip = (excludeCurse && i == curseIndex) || (excludePetrify && i == petrifyIndex);
                data[i] = skip ? 0f : -1f;
            }
            view.RPC("RPC_ApplyStatusesFromFloatArray", view.Owner, (object)data);
        }

        /// <summary>清除目标角色的全部「负面状态」（含诅咒与石化）。</summary>
        public static void ClearAllNegativeStatuses(Character target)
        {
            ClearNegativeStatuses(target, excludeCurse: false, excludePetrify: false);
        }

        /// <summary>取房间内所有「已出生且在关卡内」的角色（不含机器人与机场中的角色）。</summary>
        public static List<Character> GetActiveCharacters()
        {
            var result = new List<Character>();
            FillActiveCharacters(result);
            return result;
        }

        /// <summary>
        /// 把房间内所有「已出生且在关卡内」的角色写入调用方提供的列表（写入前先清空）。
        /// 用于每帧驱动循环复用同一个列表实例，避免像 <see cref="GetActiveCharacters"/> 那样每帧分配新列表。
        /// </summary>
        public static void FillActiveCharacters(List<Character> into)
        {
            if (into == null) return;
            into.Clear();

            var all = Character.AllCharacters;
            if (all == null) return;
            for (int i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (c == null) continue;
                if (c.inAirport) continue;
                if (c.photonView == null || c.photonView.Owner == null) continue;
                into.Add(c);
            }
        }

        /// <summary>半径内的所有角色（含自己）。</summary>
        public static List<Character> GetCharactersInRadius(Vector3 origin, float radius)
        {
            var result = new List<Character>();
            var all = Character.AllCharacters;
            if (all == null) return result;
            for (int i = 0; i < all.Count; i++)
            {
                var c = all[i];
                if (c == null) continue;
                if (radius >= 0f && Vector3.Distance(c.Center, origin) > radius) continue;
                result.Add(c);
            }
            return result;
        }

        /// <summary>当前是否全员存活（没有任何人死亡或完全昏迷）。</summary>
        public static bool EveryoneAlive()
        {
            return !Character.PlayerIsDeadOrDown();
        }
    }
}