using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

namespace RollOfTheScout
{
    /// <summary>
    /// 主机侧「职业账本」。只存在主机内存里，不写 Photon 自定义属性
    /// （客户端零安装，没有接收方；且玩家无法替他人写属性）。
    ///
    /// 键使用「玩家稳定标识」（平台 userID，见 HostApi.StableId）而不是 ActorNumber，
    /// 这样玩家中途退出游戏后再重进（ActorNumber 会变化）仍能继承原来的职业。
    /// </summary>
    public sealed class ClassLedger
    {
        private readonly Dictionary<string, ClassType> _byId = new Dictionary<string, ClassType>();

        /// <summary>PhotonView 实例 → 稳定标识的缓存，避免每帧重复解析（以实例为键，不会串号）。</summary>
        private readonly Dictionary<PhotonView, string> _idByView = new Dictionary<PhotonView, string>();

        /// <summary>
        /// 稳定标识 → 首个使用它的「占位键」（ActorNumber；Owner 为空时为负的 ViewID）。
        /// 用于**运行时冲突检测**：若两个不同玩家解析出同一个标识，说明解析出的是一份「共用常量」，
        /// 必须立刻退化为唯一标识，否则账本会串号（实测故障：除房主外所有客机同职业、
        /// 任一人转职全员跟着转职）。
        /// </summary>
        private readonly Dictionary<string, int> _claimById = new Dictionary<string, int>();

        /// <summary>
        /// PhotonView 实例 → 职业 的缓存。GetClass 是所有职业驱动循环（每帧数十个角色遍历）的热路径，
        /// 直接以 PhotonView 为键缓存结果，省去「先查 idByView 再查 byId」两次字典查找与字符串哈希。
        /// 仅在账本被改写（Assign / TryReassign / ForceAssign / Clear）时整体失效，语义与逐次查询完全一致。
        /// </summary>
        private readonly Dictionary<PhotonView, ClassType> _classByView = new Dictionary<PhotonView, ClassType>();

        /// <summary>
        /// 职业 → 当前账本中该职业的人数（O(1) 索引）。
        ///
        /// 用途：<see cref="HasClassInRoom"/> 是每帧被调用多次的「职业是否在场」判定 ——
        /// 各每帧驱动函数（Ensure* / Grant* / Tick*）都先用它做**提前返回**，
        /// 从而在某个职业不在场时**完全跳过对全体玩家的遍历**。
        /// 若靠遍历 <see cref="_byId"/> 实现（旧做法），该判定本身就成了 O(玩家数) 的扫描，
        /// 人数越多越贵，反而抵消了提前返回的收益。这里改为随写入同步维护计数，使判定恒为 O(1)。
        /// </summary>
        private readonly Dictionary<ClassType, int> _countByType = new Dictionary<ClassType, int>();

        public int Count => _byId.Count;

        public void Clear()
        {
            _byId.Clear();
            _idByView.Clear();
            _classByView.Clear();
            _claimById.Clear();
            _countByType.Clear();
        }

        /// <summary>记账：把某个「稳定标识 → 职业」写入账本并同步维护职业人数索引。</summary>
        private void SetEntry(string id, ClassType type)
        {
            if (_byId.TryGetValue(id, out var old) && old == type) return;
            if (_byId.TryGetValue(id, out var prev) && prev != ClassType.None)
                _countByType[prev] = Mathf.Max(0, (_countByType.TryGetValue(prev, out int pc) ? pc : 0) - 1);

            _byId[id] = type;
            if (type != ClassType.None)
                _countByType[type] = (_countByType.TryGetValue(type, out int nc) ? nc : 0) + 1;

            _classByView.Clear();   // 职业已改写：让 GetClass 缓存整体失效，下次按新值重算
        }

        /// <summary>账本中当前是否存在该职业的玩家（O(1)，语义与旧的遍历判定完全一致）。</summary>
        public bool HasClassInRoom(ClassType type)
        {
            return _countByType.TryGetValue(type, out int n) && n > 0;
        }

        public bool TryGet(string id, out ClassType type)
        {
            type = ClassType.None;
            return !string.IsNullOrEmpty(id) && _byId.TryGetValue(id, out type);
        }

        /// <summary>取得某角色的稳定标识（带缓存）。</summary>
        public string IdOf(Character character)
        {
            if (character == null) return null;
            var view = character.photonView;
            if (view == null) return null;

            if (_idByView.TryGetValue(view, out string cached)) return cached;

            // 该角色在「占位表」里的键：优先 ActorNumber；尚未拿到 Owner 时退化为负的 ViewID
            // （PhotonView.ViewID 在房间内唯一），保证即使 Owner 为空也能识别出「同一 id 被两方解析」。
            int claimKey = view.Owner != null ? view.Owner.ActorNumber : -view.ViewID;

            // ★ 逐个尝试候选标识（第 81 批紧急修复）：
            // 取第一个「未被其他**在场**玩家占用」的候选 —— 既杜绝「多个玩家共用一条账本记录」
            //（实测故障：除房主外全员同职业 / 一人转职全员跟转），又**尽量保住「退出重进继承职业」**
            //（只有真正的重名候选才会被跳过，而不是一律退化成会变的 ActorNumber）。
            var candidates = new List<string>(4);
            HostApi.FillStableIdCandidates(character, candidates);
            if (candidates.Count == 0) return null;

            string picked = null;
            for (int i = 0; i < candidates.Count; i++)
            {
                string cand = candidates[i];
                if (_claimById.TryGetValue(cand, out int heldBy) && heldBy != claimKey && IsClaimActive(heldBy))
                    continue;   // 已被「仍在本房间」的其他玩家占用 → 试下一个候选
                picked = cand;
                break;
            }

            // 兜底：理论上不会发生（最后一个候选是 ActorNumber），仍然给一个唯一键
            if (string.IsNullOrEmpty(picked))
                picked = view.Owner != null ? "a:" + view.Owner.ActorNumber : "v:" + view.ViewID;

            _idByView[view] = picked;
            _claimById[picked] = claimKey;

            if (candidates.Count > 1)
            {
                Plugin.Log.LogInfo(
                    $"[ClassLedger] 稳定标识：{Nick(character)} → {picked}（候选：{string.Join(" | ", candidates)}）");
            }
            else
            {
                Plugin.Log.LogInfo($"[ClassLedger] 稳定标识：{Nick(character)} → {picked}");
            }
            return picked;
        }

        /// <summary>
        /// 该「占位键」当前是否仍被房间内**在场的玩家 / 视图**占用。
        /// 用于让「玩家离开后释放占位」——这样同一个玩家重连（新 ActorNumber）时仍能解析回原标识，
        /// 「退出重进继承职业」才不会被我方的防冲突逻辑误伤。
        /// </summary>
        private static bool IsClaimActive(int claimKey)
        {
            if (claimKey == 0) return true;

            if (claimKey > 0)
            {
                // 正键 = ActorNumber：仍能在当前房间找到该玩家 → 视为占用中
                var room = PhotonNetwork.CurrentRoom;
                if (room == null) return true;
                return room.GetPlayer(claimKey) != null;
            }

            // 负键 = ViewID（解析时 Owner 尚未就绪）：该 PhotonView 仍存在则视为占用中
            return PhotonView.Find(-claimKey) != null;
        }

        public ClassType GetClass(Character character)
        {
            if (character == null) return ClassType.None;
            var view = character.photonView;
            if (view == null) return ClassType.None;

            if (_classByView.TryGetValue(view, out var cached)) return cached;

            var type = TryGet(IdOf(character), out var t) ? t : ClassType.None;
            _classByView[view] = type;
            return type;
        }

        // ---------------------------------------------------------------
        // 名额
        // ---------------------------------------------------------------

        /// <summary>全部已启用职业的最大人数之和；只要存在「不限制」的职业则视为无限。</summary>
        public int TotalCapacity()
        {
            int total = 0;
            if (ModConfig.Explorer.Enabled.Value)
            {
                int m = MaxOf(ClassType.Explorer);
                if (m <= 0) return int.MaxValue;
                total += m;
            }
            if (ModConfig.Leader.Enabled.Value)
            {
                int m = MaxOf(ClassType.Leader);
                if (m <= 0) return int.MaxValue;
                total += m;
            }
            if (ModConfig.Logistician.Enabled.Value)
            {
                int m = MaxOf(ClassType.Logistician);
                if (m <= 0) return int.MaxValue;
                total += m;
            }
            if (ModConfig.Doctor.Enabled.Value)
            {
                int m = MaxOf(ClassType.Doctor);
                if (m <= 0) return int.MaxValue;
                total += m;
            }
            if (ModConfig.Assassin.Enabled.Value)
            {
                int m = MaxOf(ClassType.Assassin);
                if (m <= 0) return int.MaxValue;
                total += m;
            }
            if (ModConfig.Pandora.Enabled.Value)
            {
                int m = MaxOf(ClassType.Pandora);
                if (m <= 0) return int.MaxValue;
                total += m;
            }
            if (ModConfig.Necromancer.Enabled.Value)
            {
                int m = MaxOf(ClassType.Necromancer);
                if (m <= 0) return int.MaxValue;
                total += m;
            }
            if (ModConfig.M.Enabled.Value)
            {
                int m = MaxOf(ClassType.M);
                if (m <= 0) return int.MaxValue;
                total += m;
            }
            if (ModConfig.Savage.Enabled.Value)
            {
                int m = MaxOf(ClassType.Savage);
                if (m <= 0) return int.MaxValue;
                total += m;
            }
            if (ModConfig.Scout.Enabled.Value)
            {
                int m = MaxOf(ClassType.Scout);
                if (m <= 0) return int.MaxValue;
                total += m;
            }
            if (ModConfig.StandardBearer.Enabled.Value)
            {
                int m = MaxOf(ClassType.StandardBearer);
                if (m <= 0) return int.MaxValue;
                total += m;
            }
            if (ModConfig.Dancer.Enabled.Value)
            {
                int m = MaxOf(ClassType.Dancer);
                if (m <= 0) return int.MaxValue;
                total += m;
            }
            if (ModConfig.Daihao.Enabled.Value)
            {
                int m = MaxOf(ClassType.Daihao);
                if (m <= 0) return int.MaxValue;
                total += m;
            }
            if (ModConfig.Saint.Enabled.Value)
            {
                int m = MaxOf(ClassType.Saint);
                if (m <= 0) return int.MaxValue;
                total += m;
            }
            if (ModConfig.Mage.Enabled.Value)
            {
                int m = MaxOf(ClassType.Mage);
                if (m <= 0) return int.MaxValue;
                total += m;
            }
            if (ModConfig.Challenger.Enabled.Value)
            {
                int m = MaxOf(ClassType.Challenger);
                if (m <= 0) return int.MaxValue;
                total += m;
            }
            if (ModConfig.MasterChef.Enabled.Value)
            {
                int m = MaxOf(ClassType.MasterChef);
                if (m <= 0) return int.MaxValue;
                total += m;
            }
            if (ModConfig.Cook.Enabled.Value)
            {
                int m = MaxOf(ClassType.Cook);
                if (m <= 0) return int.MaxValue;
                total += m;
            }
            if (ModConfig.Pacifist.Enabled.Value)
            {
                int m = MaxOf(ClassType.Pacifist);
                if (m <= 0) return int.MaxValue;
                total += m;
            }
            if (ModConfig.Hamster.Enabled.Value)
            {
                int m = MaxOf(ClassType.Hamster);
                if (m <= 0) return int.MaxValue;
                total += m;
            }
            if (ModConfig.Firefighter.Enabled.Value)
            {
                int m = MaxOf(ClassType.Firefighter);
                if (m <= 0) return int.MaxValue;
                total += m;
            }
            if (ModConfig.Narcissist.Enabled.Value)
            {
                int m = MaxOf(ClassType.Narcissist);
                if (m <= 0) return int.MaxValue;
                total += m;
            }
            if (ModConfig.LoneWolf.Enabled.Value)
            {
                int m = MaxOf(ClassType.LoneWolf);
                if (m <= 0) return int.MaxValue;
                total += m;
            }
            if (ModConfig.Gunner.Enabled.Value)
            {
                int m = MaxOf(ClassType.Gunner);
                if (m <= 0) return int.MaxValue;
                total += m;
            }
            if (ModConfig.Band.Enabled.Value)
            {
                int m = MaxOf(ClassType.Band);
                if (m <= 0) return int.MaxValue;
                total += m;
            }
            if (ModConfig.MilesSoul.Enabled.Value)
            {
                int m = MaxOf(ClassType.MilesSoul);
                if (m <= 0) return int.MaxValue;
                total += m;
            }
            if (ModConfig.Qu.Enabled.Value)
            {
                int m = MaxOf(ClassType.Qu);
                if (m <= 0) return int.MaxValue;
                total += m;
            }
            if (ModConfig.Astronaut.Enabled.Value)
            {
                int m = MaxOf(ClassType.Astronaut);
                if (m <= 0) return int.MaxValue;
                total += m;
            }
            if (ModConfig.Langwang.Enabled.Value)
            {
                int m = MaxOf(ClassType.Langwang);
                if (m <= 0) return int.MaxValue;
                total += m;
            }
            if (ModConfig.Programmer.Enabled.Value)
            {
                int m = MaxOf(ClassType.Programmer);
                if (m <= 0) return int.MaxValue;
                total += m;
            }
            if (ModConfig.ChaosEnderman.Enabled.Value)
            {
                int m = MaxOf(ClassType.ChaosEnderman);
                if (m <= 0) return int.MaxValue;
                total += m;
            }
            if (ModConfig.Gargoyle.Enabled.Value)
            {
                int m = MaxOf(ClassType.Gargoyle);
                if (m <= 0) return int.MaxValue;
                total += m;
            }
            if (ModConfig.Alchemist.Enabled.Value)
            {
                int m = MaxOf(ClassType.Alchemist);
                if (m <= 0) return int.MaxValue;
                total += m;
            }
            if (ModConfig.Trickster.Enabled.Value)
            {
                int m = MaxOf(ClassType.Trickster);
                if (m <= 0) return int.MaxValue;
                total += m;
            }
            return total;
        }

        /// <summary>所有职业名额是否都已被占满（此时剩余玩家不分配职业）。</summary>
        public bool IsFull() => Count >= TotalCapacity();

        // ---------------------------------------------------------------
        // 分配
        // ---------------------------------------------------------------

        /// <summary>
        /// 为新玩家分配一个职业（含权重、每职业最大/最小人数约束）。
        /// 若所有职业名额都已满，返回 <see cref="ClassType.None"/> 且不记录 ——
        /// 该玩家就是「无职业玩家」，之后若有名额释放会再次尝试。
        /// </summary>
        public ClassType Assign(Character character)
        {
            string id = IdOf(character);
            if (string.IsNullOrEmpty(id)) return ClassType.None;

            var enabled = GetEnabledClasses();
            if (enabled.Count == 0) return ClassType.None;

            ClassType picked = ModConfig.General.RandomAssign.Value
                ? PickByWeight(enabled)
                : PickInOrder(enabled);

            if (picked == ClassType.None)
            {
                Plugin.Log.LogInfo($"[ClassLedger] 所有职业名额已满，{Nick(character)} 本次不分配职业。");
                return ClassType.None;
            }

            SetEntry(id, picked);
            Plugin.Log.LogInfo($"[ClassLedger] 分配职业：{Nick(character)} → {Describe(picked)}");
            return picked;
        }

        /// <summary>
        /// 转职：把该玩家重新随机分配到一个「尚未占满名额」的**其他**职业。
        /// <paramref name="oldType"/> 为转职前职业，<paramref name="newType"/> 为转职后职业。
        /// 若当前无职业、或除当前职业外其余职业名额都已被占满，则返回 <c>false</c> 且不改动账本。
        /// </summary>
        public bool TryReassign(Character character, out ClassType oldType, out ClassType newType)
        {
            oldType = ClassType.None;
            newType = ClassType.None;

            string id = IdOf(character);
            if (string.IsNullOrEmpty(id)) return false;
            if (!_byId.TryGetValue(id, out oldType)) return false; // 当前无职业 → 不转职

            // 候选：已启用、非当前职业、且名额未占满
            var pool = new List<ClassType>();
            foreach (var t in GetEnabledClasses())
            {
                if (t == oldType) continue;
                if (IsCapped(t, CountOf(t))) continue;
                pool.Add(t);
            }
            if (pool.Count == 0) return false;

            newType = WeightedPick(pool);
            if (newType == ClassType.None) return false;

            SetEntry(id, newType);
            return true;
        }

        /// <summary>
        /// 强制把玩家（重新）分配为指定职业，**绕过「最大人数」限制**。
        /// 用于「传染式」改职（如待皓·模因污染）：待皓默认最大人数为 1，若受名额限制，
        /// 则「本局已有待皓」的前提下永远无法再把其他人改成待皓，效果将形同虚设。
        /// 返回 true 表示账本已更新；若该玩家原本已是该职业则返回 false（不做改动）。
        /// </summary>
        public bool ForceAssign(Character character, ClassType type)
        {
            if (type == ClassType.None) return false;
            string id = IdOf(character);
            if (string.IsNullOrEmpty(id)) return false;
            if (_byId.TryGetValue(id, out var current) && current == type) return false;

            SetEntry(id, type);
            return true;
        }

        private static string Nick(Character character)
        {
            if (character == null || character.photonView == null || character.photonView.Owner == null) return "?";
            return $"{character.photonView.Owner.NickName}(#{character.photonView.Owner.ActorNumber})";
        }

        // ---------------------------------------------------------------

        private static List<ClassType> GetEnabledClasses()
        {
            var list = new List<ClassType>();
            if (ModConfig.Explorer.Enabled.Value) list.Add(ClassType.Explorer);
            if (ModConfig.Leader.Enabled.Value) list.Add(ClassType.Leader);
            if (ModConfig.Logistician.Enabled.Value) list.Add(ClassType.Logistician);
            if (ModConfig.Doctor.Enabled.Value) list.Add(ClassType.Doctor);
            if (ModConfig.Assassin.Enabled.Value) list.Add(ClassType.Assassin);
            if (ModConfig.Pandora.Enabled.Value) list.Add(ClassType.Pandora);
            if (ModConfig.Necromancer.Enabled.Value) list.Add(ClassType.Necromancer);
            if (ModConfig.M.Enabled.Value) list.Add(ClassType.M);
            if (ModConfig.Savage.Enabled.Value) list.Add(ClassType.Savage);
            if (ModConfig.Scout.Enabled.Value) list.Add(ClassType.Scout);
            if (ModConfig.StandardBearer.Enabled.Value) list.Add(ClassType.StandardBearer);
            if (ModConfig.Dancer.Enabled.Value) list.Add(ClassType.Dancer);
            if (ModConfig.Daihao.Enabled.Value) list.Add(ClassType.Daihao);
            if (ModConfig.Saint.Enabled.Value) list.Add(ClassType.Saint);
            if (ModConfig.Mage.Enabled.Value) list.Add(ClassType.Mage);
            if (ModConfig.Challenger.Enabled.Value) list.Add(ClassType.Challenger);
            if (ModConfig.MasterChef.Enabled.Value) list.Add(ClassType.MasterChef);
            if (ModConfig.Cook.Enabled.Value) list.Add(ClassType.Cook);
            if (ModConfig.Pacifist.Enabled.Value) list.Add(ClassType.Pacifist);
            if (ModConfig.Hamster.Enabled.Value) list.Add(ClassType.Hamster);
            if (ModConfig.Firefighter.Enabled.Value) list.Add(ClassType.Firefighter);
            if (ModConfig.Narcissist.Enabled.Value) list.Add(ClassType.Narcissist);
            if (ModConfig.LoneWolf.Enabled.Value) list.Add(ClassType.LoneWolf);
            if (ModConfig.Gunner.Enabled.Value) list.Add(ClassType.Gunner);
            if (ModConfig.Band.Enabled.Value) list.Add(ClassType.Band);
            if (ModConfig.MilesSoul.Enabled.Value && MilesSoulSelectableNow()) list.Add(ClassType.MilesSoul);
            if (ModConfig.Qu.Enabled.Value) list.Add(ClassType.Qu);
            if (ModConfig.Astronaut.Enabled.Value) list.Add(ClassType.Astronaut);
            if (ModConfig.Langwang.Enabled.Value) list.Add(ClassType.Langwang);
            if (ModConfig.Programmer.Enabled.Value) list.Add(ClassType.Programmer);
            if (ModConfig.ChaosEnderman.Enabled.Value) list.Add(ClassType.ChaosEnderman);
            if (ModConfig.Gargoyle.Enabled.Value) list.Add(ClassType.Gargoyle);
            if (ModConfig.Alchemist.Enabled.Value) list.Add(ClassType.Alchemist);
            if (ModConfig.Trickster.Enabled.Value) list.Add(ClassType.Trickster);
            return list;
        }

        /// <summary>
        /// 【受诅残魂】「海滩之后无法成为本职业」：只在**尚未离开第一关（海滩 / Shore）**时把残魂放进候选池。
        ///
        /// 候选池同时供「开局分配 + 中途加入者分配」（<see cref="Assign"/>）与「转职」（<see cref="TryReassign"/>）使用，
        /// 故一处过滤即三处生效（转职无候选时按既有规则「不生效、不消耗次数」）。
        /// 已经分配到的残魂玩家不受影响 —— 本方法只决定「能否新生效」。
        /// </summary>
        private static bool MilesSoulSelectableNow()
        {
            return !ModConfig.MilesSoul.BeachOnly.Value || !HostClassManager.MilesPastBeach();
        }

        /// <summary>该职业当前人数（O(1)，读自 <c>_countByType</c> 索引）。</summary>
        private int CountOf(ClassType type)
        {
            return _countByType.TryGetValue(type, out int n) ? n : 0;
        }

        private static bool IsCapped(ClassType type, int current)
        {
            int max = MaxOf(type);
            return max > 0 && current >= max;
        }

        /// <summary>随机加权：优先补齐低于「最小人数」的职业，再套用「最大人数」上限。</summary>
        private ClassType PickByWeight(List<ClassType> enabled)
        {
            var underMin = new List<ClassType>();
            foreach (var t in enabled)
            {
                int c = CountOf(t);
                if (c < MinOf(t) && !IsCapped(t, c)) underMin.Add(t);
            }

            var pool = underMin.Count > 0 ? underMin : enabled;

            var capped = new List<ClassType>();
            foreach (var t in pool)
            {
                if (!IsCapped(t, CountOf(t))) capped.Add(t);
            }

            // 所有职业名额已满 → 无职业
            if (capped.Count == 0) return ClassType.None;

            return WeightedPick(capped);
        }

        /// <summary>非随机模式：按职业顺序依次填满（仍受最大人数限制）。</summary>
        private ClassType PickInOrder(List<ClassType> enabled)
        {
            foreach (var t in enabled)
            {
                if (!IsCapped(t, CountOf(t))) return t;
            }
            return ClassType.None;
        }

        private ClassType WeightedPick(List<ClassType> pool)
        {
            int total = 0;
            foreach (var t in pool) total += WeightOf(t);
            if (total <= 0) return pool[Random.Range(0, pool.Count)];

            int roll = Random.Range(0, total);
            int acc = 0;
            foreach (var t in pool)
            {
                acc += WeightOf(t);
                if (roll < acc) return t;
            }
            return pool[pool.Count - 1];
        }

        private static int WeightOf(ClassType type)
        {
            switch (type)
            {
                case ClassType.Explorer: return Mathf.Max(0, ModConfig.Explorer.Weight.Value);
                case ClassType.Leader: return Mathf.Max(0, ModConfig.Leader.Weight.Value);
                case ClassType.Logistician: return Mathf.Max(0, ModConfig.Logistician.Weight.Value);
                case ClassType.Doctor: return Mathf.Max(0, ModConfig.Doctor.Weight.Value);
                case ClassType.Assassin: return Mathf.Max(0, ModConfig.Assassin.Weight.Value);
                case ClassType.Pandora: return Mathf.Max(0, ModConfig.Pandora.Weight.Value);
                case ClassType.Necromancer: return Mathf.Max(0, ModConfig.Necromancer.Weight.Value);
                case ClassType.M: return Mathf.Max(0, ModConfig.M.Weight.Value);
                case ClassType.Savage: return Mathf.Max(0, ModConfig.Savage.Weight.Value);
                case ClassType.Scout: return Mathf.Max(0, ModConfig.Scout.Weight.Value);
                case ClassType.StandardBearer: return Mathf.Max(0, ModConfig.StandardBearer.Weight.Value);
                case ClassType.Dancer: return Mathf.Max(0, ModConfig.Dancer.Weight.Value);
                case ClassType.Daihao: return Mathf.Max(0, ModConfig.Daihao.Weight.Value);
                case ClassType.Saint: return Mathf.Max(0, ModConfig.Saint.Weight.Value);
                case ClassType.Mage: return Mathf.Max(0, ModConfig.Mage.Weight.Value);
                case ClassType.Challenger: return Mathf.Max(0, ModConfig.Challenger.Weight.Value);
                case ClassType.MasterChef: return Mathf.Max(0, ModConfig.MasterChef.Weight.Value);
                case ClassType.Cook: return Mathf.Max(0, ModConfig.Cook.Weight.Value);
                case ClassType.Pacifist: return Mathf.Max(0, ModConfig.Pacifist.Weight.Value);
                case ClassType.Hamster: return Mathf.Max(0, ModConfig.Hamster.Weight.Value);
                case ClassType.Firefighter: return Mathf.Max(0, ModConfig.Firefighter.Weight.Value);
                case ClassType.Narcissist: return Mathf.Max(0, ModConfig.Narcissist.Weight.Value);
                case ClassType.LoneWolf: return Mathf.Max(0, ModConfig.LoneWolf.Weight.Value);
                case ClassType.Gunner: return Mathf.Max(0, ModConfig.Gunner.Weight.Value);
                case ClassType.Band: return Mathf.Max(0, ModConfig.Band.Weight.Value);
                case ClassType.MilesSoul: return Mathf.Max(0, ModConfig.MilesSoul.Weight.Value);
                case ClassType.Qu: return Mathf.Max(0, ModConfig.Qu.Weight.Value);
                case ClassType.Astronaut: return Mathf.Max(0, ModConfig.Astronaut.Weight.Value);
                case ClassType.Langwang: return Mathf.Max(0, ModConfig.Langwang.Weight.Value);
                case ClassType.Programmer: return Mathf.Max(0, ModConfig.Programmer.Weight.Value);
                case ClassType.ChaosEnderman: return Mathf.Max(0, ModConfig.ChaosEnderman.Weight.Value);
                case ClassType.Gargoyle: return Mathf.Max(0, ModConfig.Gargoyle.Weight.Value);
                case ClassType.Alchemist: return Mathf.Max(0, ModConfig.Alchemist.Weight.Value);
                case ClassType.Trickster: return Mathf.Max(0, ModConfig.Trickster.Weight.Value);
                default: return 0;
            }
        }

        /// <summary>每职业「最大人数」，0 = 不限制。</summary>
        private static int MaxOf(ClassType type)
        {
            switch (type)
            {
                case ClassType.Explorer: return Mathf.Max(0, ModConfig.Explorer.MaxPlayers.Value);
                case ClassType.Leader: return Mathf.Max(0, ModConfig.Leader.MaxPlayers.Value);
                case ClassType.Logistician: return Mathf.Max(0, ModConfig.Logistician.MaxPlayers.Value);
                case ClassType.Doctor: return Mathf.Max(0, ModConfig.Doctor.MaxPlayers.Value);
                case ClassType.Assassin: return Mathf.Max(0, ModConfig.Assassin.MaxPlayers.Value);
                case ClassType.Pandora: return Mathf.Max(0, ModConfig.Pandora.MaxPlayers.Value);
                case ClassType.Necromancer: return Mathf.Max(0, ModConfig.Necromancer.MaxPlayers.Value);
                case ClassType.M: return Mathf.Max(0, ModConfig.M.MaxPlayers.Value);
                case ClassType.Savage: return Mathf.Max(0, ModConfig.Savage.MaxPlayers.Value);
                case ClassType.Scout: return Mathf.Max(0, ModConfig.Scout.MaxPlayers.Value);
                case ClassType.StandardBearer: return Mathf.Max(0, ModConfig.StandardBearer.MaxPlayers.Value);
                case ClassType.Dancer: return Mathf.Max(0, ModConfig.Dancer.MaxPlayers.Value);
                case ClassType.Daihao: return Mathf.Max(0, ModConfig.Daihao.MaxPlayers.Value);
                case ClassType.Saint: return Mathf.Max(0, ModConfig.Saint.MaxPlayers.Value);
                case ClassType.Mage: return Mathf.Max(0, ModConfig.Mage.MaxPlayers.Value);
                case ClassType.Challenger: return Mathf.Max(0, ModConfig.Challenger.MaxPlayers.Value);
                case ClassType.MasterChef: return Mathf.Max(0, ModConfig.MasterChef.MaxPlayers.Value);
                case ClassType.Cook: return Mathf.Max(0, ModConfig.Cook.MaxPlayers.Value);
                case ClassType.Pacifist: return Mathf.Max(0, ModConfig.Pacifist.MaxPlayers.Value);
                case ClassType.Hamster: return Mathf.Max(0, ModConfig.Hamster.MaxPlayers.Value);
                case ClassType.Firefighter: return Mathf.Max(0, ModConfig.Firefighter.MaxPlayers.Value);
                case ClassType.Narcissist: return Mathf.Max(0, ModConfig.Narcissist.MaxPlayers.Value);
                case ClassType.LoneWolf: return Mathf.Max(0, ModConfig.LoneWolf.MaxPlayers.Value);
                case ClassType.Gunner: return Mathf.Max(0, ModConfig.Gunner.MaxPlayers.Value);
                case ClassType.Band: return Mathf.Max(0, ModConfig.Band.MaxPlayers.Value);
                case ClassType.MilesSoul: return Mathf.Max(0, ModConfig.MilesSoul.MaxPlayers.Value);
                case ClassType.Qu: return Mathf.Max(0, ModConfig.Qu.MaxPlayers.Value);
                case ClassType.Astronaut: return Mathf.Max(0, ModConfig.Astronaut.MaxPlayers.Value);
                case ClassType.Langwang: return Mathf.Max(0, ModConfig.Langwang.MaxPlayers.Value);
                case ClassType.Programmer: return Mathf.Max(0, ModConfig.Programmer.MaxPlayers.Value);
                case ClassType.ChaosEnderman: return Mathf.Max(0, ModConfig.ChaosEnderman.MaxPlayers.Value);
                case ClassType.Gargoyle: return Mathf.Max(0, ModConfig.Gargoyle.MaxPlayers.Value);
                case ClassType.Alchemist: return Mathf.Max(0, ModConfig.Alchemist.MaxPlayers.Value);
                case ClassType.Trickster: return Mathf.Max(0, ModConfig.Trickster.MaxPlayers.Value);
                default: return 0;
            }
        }

        /// <summary>每职业「最小人数」。</summary>
        private static int MinOf(ClassType type)
        {
            switch (type)
            {
                case ClassType.Explorer: return Mathf.Max(0, ModConfig.Explorer.MinPlayers.Value);
                case ClassType.Leader: return Mathf.Max(0, ModConfig.Leader.MinPlayers.Value);
                case ClassType.Logistician: return Mathf.Max(0, ModConfig.Logistician.MinPlayers.Value);
                case ClassType.Doctor: return Mathf.Max(0, ModConfig.Doctor.MinPlayers.Value);
                case ClassType.Assassin: return Mathf.Max(0, ModConfig.Assassin.MinPlayers.Value);
                case ClassType.Pandora: return Mathf.Max(0, ModConfig.Pandora.MinPlayers.Value);
                case ClassType.Necromancer: return Mathf.Max(0, ModConfig.Necromancer.MinPlayers.Value);
                case ClassType.M: return Mathf.Max(0, ModConfig.M.MinPlayers.Value);
                case ClassType.Savage: return Mathf.Max(0, ModConfig.Savage.MinPlayers.Value);
                case ClassType.Scout: return Mathf.Max(0, ModConfig.Scout.MinPlayers.Value);
                case ClassType.StandardBearer: return Mathf.Max(0, ModConfig.StandardBearer.MinPlayers.Value);
                case ClassType.Dancer: return Mathf.Max(0, ModConfig.Dancer.MinPlayers.Value);
                case ClassType.Daihao: return Mathf.Max(0, ModConfig.Daihao.MinPlayers.Value);
                case ClassType.Saint: return Mathf.Max(0, ModConfig.Saint.MinPlayers.Value);
                case ClassType.Mage: return Mathf.Max(0, ModConfig.Mage.MinPlayers.Value);
                case ClassType.Challenger: return Mathf.Max(0, ModConfig.Challenger.MinPlayers.Value);
                case ClassType.MasterChef: return Mathf.Max(0, ModConfig.MasterChef.MinPlayers.Value);
                case ClassType.Cook: return Mathf.Max(0, ModConfig.Cook.MinPlayers.Value);
                case ClassType.Pacifist: return Mathf.Max(0, ModConfig.Pacifist.MinPlayers.Value);
                case ClassType.Hamster: return Mathf.Max(0, ModConfig.Hamster.MinPlayers.Value);
                case ClassType.Firefighter: return Mathf.Max(0, ModConfig.Firefighter.MinPlayers.Value);
                case ClassType.Narcissist: return Mathf.Max(0, ModConfig.Narcissist.MinPlayers.Value);
                case ClassType.LoneWolf: return Mathf.Max(0, ModConfig.LoneWolf.MinPlayers.Value);
                case ClassType.Gunner: return Mathf.Max(0, ModConfig.Gunner.MinPlayers.Value);
                case ClassType.Band: return Mathf.Max(0, ModConfig.Band.MinPlayers.Value);
                case ClassType.MilesSoul: return Mathf.Max(0, ModConfig.MilesSoul.MinPlayers.Value);
                case ClassType.Qu: return Mathf.Max(0, ModConfig.Qu.MinPlayers.Value);
                case ClassType.Astronaut: return Mathf.Max(0, ModConfig.Astronaut.MinPlayers.Value);
                case ClassType.Langwang: return Mathf.Max(0, ModConfig.Langwang.MinPlayers.Value);
                case ClassType.Programmer: return Mathf.Max(0, ModConfig.Programmer.MinPlayers.Value);
                case ClassType.ChaosEnderman: return Mathf.Max(0, ModConfig.ChaosEnderman.MinPlayers.Value);
                case ClassType.Gargoyle: return Mathf.Max(0, ModConfig.Gargoyle.MinPlayers.Value);
                case ClassType.Alchemist: return Mathf.Max(0, ModConfig.Alchemist.MinPlayers.Value);
                case ClassType.Trickster: return Mathf.Max(0, ModConfig.Trickster.MinPlayers.Value);
                default: return 0;
            }
        }

        public static string Describe(ClassType type)
        {
            switch (type)
            {
                case ClassType.Explorer: return "先驱";
                case ClassType.Leader: return "领袖";
                case ClassType.Logistician: return "后勤";
                case ClassType.Doctor: return "医生";
                case ClassType.Assassin: return "刺客";
                case ClassType.Pandora: return "潘多拉之子";
                case ClassType.Necromancer: return "死灵法师";
                case ClassType.M: return "M";
                case ClassType.Savage: return "野人";
                case ClassType.Scout: return "童军";
                case ClassType.StandardBearer: return "旗手";
                case ClassType.Dancer: return "舞王";
                case ClassType.Daihao: return "待皓";
                case ClassType.Saint: return "圣人";
                case ClassType.Mage: return "法师";
                case ClassType.Challenger: return "挑战者";
                case ClassType.MasterChef: return "特级厨师";
                case ClassType.Cook: return "炊事员";
                case ClassType.Pacifist: return "和平主义者";
                case ClassType.Hamster: return "仓鼠";
                case ClassType.Firefighter: return "救火队员";
                case ClassType.Narcissist: return "自恋狂";
                case ClassType.LoneWolf: return "独狼";
                case ClassType.Gunner: return "炮手";
                case ClassType.Band: return "乐队成员";
                case ClassType.MilesSoul: return "迈尔斯残魂";
                case ClassType.Qu: return "区";
                case ClassType.Astronaut: return "太空人";
                case ClassType.Langwang: return "榔王";
                case ClassType.Programmer: return "程序员";
                case ClassType.ChaosEnderman: return "混乱末影人";
                case ClassType.Gargoyle: return "石像鬼";
                case ClassType.Alchemist: return "炼金术士";
                case ClassType.Trickster: return "捣蛋鬼";
                default: return "无职业";
            }
        }

        /// <summary>职业数值 → 英文职业名（用于面板 / 广播的「英文 · 中文」双语显示）。</summary>
        public static string DescribeEnglish(ClassType type)
        {
            switch (type)
            {
                case ClassType.Explorer: return "Explorer";
                case ClassType.Leader: return "Leader";
                case ClassType.Logistician: return "Logistician";
                case ClassType.Doctor: return "Doctor";
                case ClassType.Assassin: return "Assassin";
                case ClassType.Pandora: return "Pandora's Child";
                case ClassType.Necromancer: return "Necromancer";
                case ClassType.M: return "M";
                case ClassType.Savage: return "Savage";
                case ClassType.Scout: return "Scout";
                case ClassType.StandardBearer: return "StandardBearer";
                case ClassType.Dancer: return "Dancer";
                case ClassType.Daihao: return "Daihao";
                case ClassType.Saint: return "Saint";
                case ClassType.Mage: return "Mage";
                case ClassType.Challenger: return "Challenger";
                case ClassType.MasterChef: return "MasterChef";
                case ClassType.Cook: return "Cook";
                case ClassType.Pacifist: return "Pacifist";
                case ClassType.Hamster: return "Hamster";
                case ClassType.Firefighter: return "Firefighter";
                case ClassType.Narcissist: return "Narcissist";
                case ClassType.LoneWolf: return "LoneWolf";
                case ClassType.Gunner: return "Gunner";
                case ClassType.Band: return "Band";
                case ClassType.MilesSoul: return "MilesSoul";
                case ClassType.Qu: return "Qu";
                case ClassType.Astronaut: return "Astronaut";
                case ClassType.Langwang: return "Langwang";
                case ClassType.Programmer: return "Programmer";
                case ClassType.ChaosEnderman: return "ChaosEnderman";
                case ClassType.Gargoyle: return "Gargoyle";
                case ClassType.Alchemist: return "Alchemist";
                case ClassType.Trickster: return "Trickster";
                default: return "None";
            }
        }

        // 「英文 · 中文」双语职业名缓存（按职业数值索引，避免每次调用都拼接字符串）。
        private static readonly string[] _bilingualNames = new string[64];

        /// <summary>职业数值 → 「英文职业名 · 中文职业名」（如 <c>Qu · 区</c>）。</summary>
        public static string DescribeBilingual(ClassType type)
        {
            int code = (int)type;
            if (code > 0 && code < _bilingualNames.Length && _bilingualNames[code] != null)
                return _bilingualNames[code];

            string text = code <= 0
                ? "None · 无职业"
                : DescribeEnglish(type) + " · " + Describe(type);

            if (code > 0 && code < _bilingualNames.Length) _bilingualNames[code] = text;
            return text;
        }
    }
}