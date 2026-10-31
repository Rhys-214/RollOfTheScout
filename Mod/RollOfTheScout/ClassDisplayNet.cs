using System.Collections.Generic;
using System.Text;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;

namespace RollOfTheScout
{
    /// <summary>
    /// 主机 → 客机「职业显示」网络广播。
    ///
    /// 背景：职业账本只存在主机内存、原版也没有任何能携带任意文本到远端客户端 UI 的
    /// 网络接口，因此「职业分配面板」「职业效果提示（Toast）」「头顶职业前缀」原本只能作用在
    /// 主机自己的屏幕上。现在为「安装了本模组或附属模组 <c>com.peakmodding.RollOfTheScoutClient</c> 的客机」
    /// 补上这条通道：主机用 Photon 自定义事件（<see cref="EventCode"/>）把数据广播给其它客户端，
    /// 由接收端在**自己屏幕上**本地显示。
    ///
    /// 硬约束（不得违背）：
    /// · 显示数据只由主机（MasterClient）发送；残魂本机握手 / 使用报告只发给主机并仅用于该职业结算；
    /// · 客机**不装任何模组时本广播被直接忽略**，因此「客机零安装」的主线不受影响；
    /// · 广播默认不发给发送者（<c>ReceiverGroup.Others</c>），主机自身始终用本地账本显示。
    ///
    /// 数据格式：<c>object[]{ ModId(string), Kind(byte), Data(string) }</c>。
    /// 之所以把结构化数据压成 <c>string</c>，是为规避 Photon 对混合类型数组 / 数组字段序列化的
    /// 不确定性 —— <c>string</c> 一定可序列化。
    ///
    /// · <see cref="KindClassSnapshot"/>：<c>Data</c> = <c>"actor=class;actor=class;…"</c>
    ///   （ActorNumber=ClassType 数值），接收端自行解析成「昵称：职业」与「头顶前缀」；
    /// · <see cref="KindToast"/>：<c>Data</c> = 提示原文（与主机左上角提示完全一致）。
    /// </summary>
    public static class ClassDisplayNet
    {
        /// <summary>与附属模组约定的模组标识，用于把本事件与其它模组的同码事件区分开。</summary>
        public const string ModId = "com.peakmodding.RollOfTheScout";

        /// <summary>自定义事件码。刻意避开 Trapped Luggage 使用的 199，避免两模组同房时串扰。</summary>
        public const byte EventCode = 197;

        /// <summary>Kind：职业分配快照（<c>"actor=class;…"</c>）。</summary>
        public const byte KindClassSnapshot = 1;

        /// <summary>Kind：职业效果提示（Toast 文本）。</summary>
        public const byte KindToast = 2;

        /// <summary>
        /// Kind：职业名对照表（<c>"code=名称;…"</c>）。
        ///
        /// 随每次快照一起下发，让**客机端不必内置职业名表**：主模组将来新增职业时，
        /// 装了附属模组（或只装主模组）的客机无需任何更新即可正确显示新职业名，
        /// 而不会显示成「无职业」。旧版附属模组不认识本 Kind，会直接忽略（不产生任何回归）。
        /// </summary>
        public const byte KindClassNames = 3;

        /// <summary>
        /// Kind：职业「关键词」文本表（<c>"code=关键词;…"</c>），与 <see cref="KindClassNames"/> 同格式。
        ///
        /// 用途：让**旧版附属模组 / 旧版主模组客机模式**也能显示主模组后来新增职业的介绍
        /// （客机本地文案表里没有该职业时，退回主机广播来的文本）。
        /// 静态文本，只在「职业集合变化」或每 <see cref="ClassTextResendSeconds"/> 秒补发一次。
        /// </summary>
        public const byte KindClassKeywords = 4;

        /// <summary>Kind：职业「简介」文本表（<c>"code=简介;…"</c>），用途同 <see cref="KindClassKeywords"/>。</summary>
        public const byte KindClassIntro = 5;

        /// <summary>
        /// Kind：**定向提示**（<c>"ActorNumber\t文本"</c>）—— 只有该 ActorNumber 的客机显示。
        /// 用于「只有相应职业的玩家才看得到」的提醒（如舞王 / 待皓的强制后空翻预警）。
        /// </summary>
        public const byte KindTargetedToast = 6;

        /// <summary>残魂本机能力握手（客机 → 主机）与使用报告（主机据此只计数，不重复结算）。</summary>
        public const byte KindMilesClientHello = 7;
        public const byte KindMilesUsageReport = 9;

        /// <summary>残魂本机已收到职业 / 配置的确认（客机 → 主机）；确认前仍由主机兜底。</summary>
        public const byte KindMilesClientReady = 10;

        /// <summary>残魂本机职业 / 生效配置（主机 → 指定客机）。</summary>
        public const byte KindMilesGameplayState = 8;

        /// <summary>关键词 / 简介文本的保底重发间隔（秒）。仅为让中途加入的客机也能收到；文本是静态的，无需频繁发送。</summary>
        public const float ClassTextResendSeconds = 10f;

        /// <summary>职业快照的保底重发间隔（秒）：保证中途加入的客机也能尽快收到，且实时跟随职业变动。</summary>
        public const float SnapshotIntervalSeconds = 5f;

        // ---------------------------------------------------------------
        // 发送（仅主机）
        // ---------------------------------------------------------------

        /// <summary>广播职业分配快照。仅主机、且「职业显示-网络同步」开启时发送。</summary>
        public static void BroadcastClassSnapshot(IList<KeyValuePair<int, int>> actorClassPairs)
        {
            if (!ShouldBroadcast()) return;
            if (actorClassPairs == null || actorClassPairs.Count == 0) return;

            var sb = new StringBuilder();
            for (int i = 0; i < actorClassPairs.Count; i++)
            {
                if (i > 0) sb.Append(';');
                sb.Append(actorClassPairs[i].Key).Append('=').Append(actorClassPairs[i].Value);
            }
            Send(KindClassSnapshot, sb.ToString());
        }

        /// <summary>广播「职业数值 → 职业名」对照表（只发当前快照里出现过的职业即可）。</summary>
        public static void BroadcastClassNames(IList<KeyValuePair<int, string>> codeToName)
        {
            if (!ShouldBroadcast()) return;
            if (codeToName == null || codeToName.Count == 0) return;

            var sb = new StringBuilder();
            for (int i = 0; i < codeToName.Count; i++)
            {
                if (i > 0) sb.Append(';');
                sb.Append(codeToName[i].Key).Append('=').Append(Sanitize(codeToName[i].Value));
            }
            Send(KindClassNames, sb.ToString());
        }

        /// <summary>广播「职业数值 → 关键词」文本表（旧版客机据此显示新职业的介绍）。</summary>
        public static void BroadcastClassKeywords(IList<KeyValuePair<int, string>> codeToKeywords)
        {
            SendTextMap(KindClassKeywords, codeToKeywords);
        }

        /// <summary>广播「职业数值 → 简介」文本表（旧版客机据此显示新职业的介绍）。</summary>
        public static void BroadcastClassIntro(IList<KeyValuePair<int, string>> codeToIntro)
        {
            SendTextMap(KindClassIntro, codeToIntro);
        }

        /// <summary>广播一条**只给指定玩家**看的提示（其余客机收到后忽略）。</summary>
        public static void BroadcastTargetedToast(int actorNumber, string text)
        {
            if (!ShouldBroadcast()) return;
            if (actorNumber <= 0 || string.IsNullOrEmpty(text)) return;
            // 定向提示允许换行（中英双语两行）；只把会破坏「ActorNumber\t文本」解析的制表符替换掉。
            Send(KindTargetedToast, actorNumber.ToString() + "\t" + SanitizeToast(text));
        }

        /// <summary>把 <c>"code=文本;…"</c> 形式的文本表发出。</summary>
        private static void SendTextMap(byte kind, IList<KeyValuePair<int, string>> pairs)
        {
            if (!ShouldBroadcast()) return;
            if (pairs == null || pairs.Count == 0) return;

            var sb = new StringBuilder();
            for (int i = 0; i < pairs.Count; i++)
            {
                if (i > 0) sb.Append(';');
                sb.Append(pairs[i].Key).Append('=').Append(Sanitize(pairs[i].Value));
            }
            Send(kind, sb.ToString());
        }

        /// <summary>
        /// 把分隔符替换掉，避免文本里的 <c>;</c> / <c>=</c> / 换行破坏解析
        /// （职业名 / 关键词 / 简介均为固定常量，实际不会命中）。
        /// </summary>
        private static string Sanitize(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;
            return name.Replace(';', '_').Replace('=', '_').Replace('\n', ' ').Replace('\r', ' ');
        }

        /// <summary>
        /// 定向提示专用：保留换行（客户端只在**第一个** Tab 处切分 ActorNumber 与文本），
        /// 仅替换制表符，避免破坏「ActorNumber\t文本」的解析。
        /// </summary>
        private static string SanitizeToast(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text.Replace('\t', ' ');
        }

        /// <summary>广播一条职业效果提示（与主机左上角 Toast 文本一致）。</summary>
        public static void BroadcastToast(string text)
        {
            if (!ShouldBroadcast()) return;
            if (string.IsNullOrEmpty(text)) return;
            Send(KindToast, text);
        }

        /// <summary>
        /// 向指定客机下发残魂职业与生效配置。该通道属于玩法同步，不受「职业显示-网络同步」开关影响。
        /// </summary>
        public static void SendMilesGameplayState(int actorNumber, string data)
        {
            if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient) return;
            if (actorNumber <= 0 || string.IsNullOrEmpty(data)) return;
            try
            {
                PhotonNetwork.RaiseEvent(
                    EventCode,
                    new object[] { ModId, KindMilesGameplayState, data },
                    new RaiseEventOptions { TargetActors = new[] { actorNumber } },
                    SendOptions.SendReliable);
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"[RollOfTheScout] 残魂本机配置同步失败：{e.Message}");
            }
        }

        private static bool ShouldBroadcast()
        {
            if (!ModConfig.General.DisplaySync.Value) return false;
            if (!PhotonNetwork.InRoom) return false;
            return PhotonNetwork.IsMasterClient;
        }

        private static void Send(byte kind, string data)
        {
            try
            {
                object[] payload = { ModId, kind, data };
                bool sent = PhotonNetwork.RaiseEvent(
                    EventCode, payload,
                    new RaiseEventOptions { Receivers = ReceiverGroup.Others },
                    SendOptions.SendReliable);
                if (!sent)
                {
                    Plugin.Log.LogWarning(
                        $"[RollOfTheScout] 职业显示广播未被 Photon 接受（kind={kind}，payload={payload.Length} 段）。");
                }
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning($"[RollOfTheScout] 职业显示网络广播失败（kind={kind}）：{e.Message}");
            }
        }

        // ---------------------------------------------------------------
        // 解析（接收端：本模组客机模式 / 附属模组共用同一格式）
        // ---------------------------------------------------------------

        /// <summary>把 <c>"actor=class;…"</c> 解析为 (ActorNumber, ClassType 数值) 列表（保持主机侧顺序）。</summary>
        public static List<KeyValuePair<int, int>> ParseSnapshot(string data)
        {
            var list = new List<KeyValuePair<int, int>>();
            if (string.IsNullOrEmpty(data)) return list;

            foreach (var part in data.Split(';'))
            {
                if (string.IsNullOrEmpty(part)) continue;
                int eq = part.IndexOf('=');
                if (eq <= 0) continue;
                if (int.TryParse(part.Substring(0, eq), out int actor) &&
                    int.TryParse(part.Substring(eq + 1), out int cls))
                {
                    list.Add(new KeyValuePair<int, int>(actor, cls));
                }
            }
            return list;
        }

        /// <summary>把 <c>"code=名称;…"</c> 解析为 (职业数值, 职业名) 列表。</summary>
        public static List<KeyValuePair<int, string>> ParseClassNames(string data)
        {
            var list = new List<KeyValuePair<int, string>>();
            if (string.IsNullOrEmpty(data)) return list;

            foreach (var part in data.Split(';'))
            {
                if (string.IsNullOrEmpty(part)) continue;
                int eq = part.IndexOf('=');
                if (eq <= 0) continue;
                if (int.TryParse(part.Substring(0, eq), out int code) && eq + 1 < part.Length)
                {
                    list.Add(new KeyValuePair<int, string>(code, part.Substring(eq + 1)));
                }
            }
            return list;
        }
    }
}
