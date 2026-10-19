using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace PeakOverheadCompat
{
    /// <summary>
    /// 第三方「玩家头顶 id 显示」模组兼容层（主模组与附属模组共用同一份实现，见两个 csproj 的
    /// <c>&lt;Compile Include="..\Shared\ThirdPartyOverheadNameCompat.cs" /&gt;</c>）。
    ///
    /// 背景：以下两个模组都会**隐藏原版头顶名牌**、改由自己绘制「名字 + 距离」标签，
    /// 因此只挂原版 <c>UIPlayerNames.UpdateName</c> 的「头顶职业前缀」在这些模组自己的标签上看不到：
    ///   · <b>BetterPlayerDistance</b>（GUID <c>com.AiAe.betterplayerdistance</c>）：
    ///     其主标签的名字在构造时由 <c>Tracking.ExtractDisplayName(character.name)</c>
    ///     （`Character [昵称 : ActorNumber]`）**只取一次**并缓存在 <c>PlayerLabel._displayName</c>；
    ///     昏迷 / 死亡标记则每帧重新取一次名。
    ///   · <b>Sense of Direction</b>（GUID <c>OnlyCook.SenseOfDirection</c>）：
    ///     其玩家屏幕标签每帧调用 <c>PlayerLabel.Refresh(character.characterName, …)</c> 写名。
    ///
    /// 本层以**运行时反射**挂补丁（不引用对方程序集，因此对方未安装 / 未加载时本模组照常工作，
    /// 是**软依赖**）：某个模组不存在时该分支直接跳过；全部逻辑包裹在 try/catch 中，仅在失败时告警。
    ///
    /// 职业数据来源由调用方注入（主机读本局账本，客机读主机广播的职业表），键统一为
    /// <b>ActorNumber</b>（因为 BPD 的名字里只带 ActorNumber）。皮肤色按 ActorNumber **只读一次**并缓存，
    /// 与前缀的着色要求一致。配置项「头顶职业前缀」关闭时，注入的查询返回 0，本层会自动还原为纯昵称。
    /// </summary>
    public static class ThirdPartyOverheadNameCompat
    {
        // 目标模组的 DLL 文件名（用于「是否安装」的探测，避免为未安装的模组空转重试）
        private const string BpdDll = "BetterPlayerDistance.dll";
        private const string SodDll = "SenseOfDirection.dll";

        // ---- 由调用方注入 ----
        private static Func<int, string> _classNameByActor;   // ActorNumber → 职业名（空 = 无职业 / 未启用）
        private static Action<string> _warn;
        private static bool _initialized;

        // ---- 皮肤色 / 角色缓存 ----
        // 皮肤色以 **PhotonView.ViewID 为键**：玩家中途退出再加入会得到新的 ViewID（即使 ActorNumber 被复用），
        // 因此不会沿用上一个角色的颜色。角色副本缓存按 ActorNumber。
        private static readonly Dictionary<int, string> _colorByViewId = new Dictionary<int, string>();
        private static readonly Dictionary<int, Character> _characterByActor = new Dictionary<int, Character>();

        // ---- 重试调度 / 结果 ----
        private static float _nextTry;
        private static bool _bpdDone;
        private static bool _sodDone;
        private static bool _bpdApplied;
        private static bool _sodApplied;
        private static bool _loggedApplied;

        /// <summary>
        /// 注入「ActorNumber → 职业名」查询（**空字符串表示无职业或本功能关闭**）与日志回调。
        /// 必须在 <see cref="Tick"/> 之前调用一次。
        ///
        /// 刻意只传「职业名」而不传职业数值 + 名称表：名称由主机侧统一提供，
        /// 主模组新增职业时客机无需任何更新即可显示。
        /// </summary>
        public static void Initialize(Func<int, string> classNameByActor, Action<string> warn)
        {
            _classNameByActor = classNameByActor;
            _warn = warn;
            _initialized = true;
        }

        /// <summary>
        /// 每帧调用（由宿主模组的 Update 驱动）：限流重试挂补丁，直到目标模组全部处理完或确认未安装。
        /// 未调用 <see cref="Initialize"/> 时不做任何事。
        /// </summary>
        public static void Tick(Harmony harmony)
        {
            if (!_initialized || harmony == null) return;
            if (_bpdDone && _sodDone) return;
            if (Time.time < _nextTry) return;
            _nextTry = Time.time + 1f;

            // 未安装 → 直接视为「已处理」；已安装但程序集尚未加载 → 本轮返回 false，下轮再试。
            if (!_bpdDone) _bpdDone = !Installed(BpdDll) || ApplyBpd(harmony);
            if (!_sodDone) _sodDone = !Installed(SodDll) || ApplySod(harmony);

            if (_bpdApplied && _sodApplied && !_loggedApplied)
            {
                _loggedApplied = true;
                Warn("[兼容] 已挂载第三方头顶id显示模组兼容补丁（BetterPlayerDistance / Sense of Direction）。");
            }
        }

        private static bool Installed(string dllFileName)
        {
            try
            {
                string dir = Paths.PluginPath;
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                    return Directory.GetFiles(dir, dllFileName, SearchOption.AllDirectories).Length > 0;
            }
            catch { /* 目录不可访问：按未安装处理 */ }
            return false;
        }

        private static void Warn(string msg)
        {
            try { _warn?.Invoke(msg); } catch { }
        }

        /// <summary>按「程序集简单名 + 类型简单名」查类型：不依赖命名空间 / 嵌套层级，跨版本更稳。</summary>
        private static Type FindType(string assemblyName, string simpleName)
        {
            try
            {
                var asm = FindAssembly(assemblyName);
                if (asm != null)
                {
                    foreach (var t in asm.GetTypes())
                    {
                        if (t != null && t.Name == simpleName) return t;
                    }
                }
            }
            catch { /* 反射加载异常：按未找到处理，下一轮重试 */ }
            return null;
        }

        private static Assembly FindAssembly(string simpleName)
        {
            try
            {
                foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (a != null && a.GetName().Name == simpleName) return a;
                }
            }
            catch { }
            return null;
        }

        // ---------------------------------------------------------------
        // 目标解析：BetterPlayerDistance
        // ---------------------------------------------------------------
        private static FieldInfo _bpdCharacterField;
        private static FieldInfo _bpdDisplayNameField;
        private static FieldInfo _bpdLastContentField;

        private static bool ApplyBpd(Harmony harmony)
        {
            try
            {
                var tracking = FindType("BetterPlayerDistance", "Tracking");
                var labelType = FindType("BetterPlayerDistance", "PlayerLabel");
                if (tracking == null || labelType == null) return false;   // 程序集尚未加载：下次再试

                const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic;
                var extract = tracking.GetMethod("ExtractDisplayName", all | BindingFlags.Static);
                var updateState = labelType.GetMethod("UpdateState", all | BindingFlags.Instance);

                const BindingFlags priv = BindingFlags.NonPublic | BindingFlags.Instance;
                _bpdCharacterField = labelType.GetField("_character", priv);
                _bpdDisplayNameField = labelType.GetField("_displayName", priv);
                _bpdLastContentField = labelType.GetField("_lastContent", priv);

                if (extract == null || updateState == null ||
                    _bpdCharacterField == null || _bpdDisplayNameField == null || _bpdLastContentField == null)
                    return false;

                harmony.Patch(extract, postfix: new HarmonyMethod(
                    AccessTools.Method(typeof(BpdExtractNamePatch), nameof(BpdExtractNamePatch.Postfix))));
                harmony.Patch(updateState, postfix: new HarmonyMethod(
                    AccessTools.Method(typeof(BpdLabelPatch), nameof(BpdLabelPatch.Postfix))));
                _bpdApplied = true;
                return true;
            }
            catch (Exception e)
            {
                Warn("[兼容] BetterPlayerDistance 补丁失败：" + e.Message);
                return true;   // 已尝试且失败：不再空转重试
            }
        }

        // ---------------------------------------------------------------
        // 目标解析：Sense of Direction
        // ---------------------------------------------------------------
        private static FieldInfo _sodEntriesField;
        private static FieldInfo _sodEntryLabelField;
        private static PropertyInfo _sodInstanceProp;
        private static FieldInfo _sodInstanceField;

        private static bool ApplySod(Harmony harmony)
        {
            try
            {
                var ctrlType = FindType("SenseOfDirection", "PlayerLabelController");
                var labelType = FindType("SenseOfDirection", "PlayerLabel");
                if (ctrlType == null || labelType == null) return false;   // 程序集尚未加载：下次再试

                const BindingFlags priv = BindingFlags.NonPublic | BindingFlags.Instance;
                const BindingFlags stat = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
                _sodEntriesField = ctrlType.GetField("_entries", priv);
                _sodInstanceProp = ctrlType.GetProperty("Instance", stat);
                _sodInstanceField = _sodInstanceProp == null ? ctrlType.GetField("_instance", stat) : null;
                var entryType = ctrlType.GetNestedType("Entry", BindingFlags.NonPublic);
                _sodEntryLabelField = entryType?.GetField("Label",
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

                // PlayerLabel 上可能有多个 Refresh 重载，取「首个参数为 string」的那个（玩家屏幕标签）。
                MethodInfo refresh = null;
                foreach (var m in labelType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    if (m.Name != "Refresh") continue;
                    var ps = m.GetParameters();
                    if (ps.Length >= 1 && ps[0].ParameterType == typeof(string)) { refresh = m; break; }
                }

                if (refresh == null || _sodEntriesField == null ||
                    (_sodInstanceProp == null && _sodInstanceField == null) || _sodEntryLabelField == null)
                    return false;

                harmony.Patch(refresh, prefix: new HarmonyMethod(
                    AccessTools.Method(typeof(SodRefreshPatch), nameof(SodRefreshPatch.Prefix))));
                _sodApplied = true;
                return true;
            }
            catch (Exception e)
            {
                Warn("[兼容] Sense of Direction 补丁失败：" + e.Message);
                return true;
            }
        }

        // ---------------------------------------------------------------
        // 公共工具
        // ---------------------------------------------------------------

        internal static bool Active => _initialized && _classNameByActor != null;

        /// <summary>按 ActorNumber 取职业名（空 = 无职业 / 未启用）。</summary>
        internal static string ClassNameOf(int actor)
        {
            if (_classNameByActor == null) return null;
            try { return _classNameByActor(actor); } catch { return null; }
        }

        /// <summary>按 ActorNumber 找本机上的角色副本（用于取皮肤色）。</summary>
        internal static Character CharacterOf(int actor)
        {
            if (_characterByActor.TryGetValue(actor, out var cached) && cached != null) return cached;

            var all = Character.AllCharacters;
            if (all != null)
            {
                for (int i = 0; i < all.Count; i++)
                {
                    var c = all[i];
                    if (c == null || c.photonView == null || c.photonView.Owner == null) continue;
                    if (c.photonView.Owner.ActorNumber != actor) continue;
                    _characterByActor[actor] = c;
                    return c;
                }
            }
            return null;
        }

        /// <summary>
        /// 皮肤色（"RRGGBB"），以 **PhotonView.ViewID** 为键缓存。
        ///
        /// · 玩家中途退出再加入 → 新的 ViewID（即便 ActorNumber 被复用）→ 自然重新读取，
        ///   不会沿用上一个角色的颜色；
        /// · **只在读取成功后才缓存**：玩家刚加入 / 刚进关卡时装扮数据可能尚未同步
        ///   （<c>PlayerColor</c> 会抛异常），此时先回退白色并按 <see cref="ColorRetrySeconds"/> 重试，
        ///   直到读到真实颜色为止 —— 避免「首次读到白色就永久是白色」。
        /// </summary>
        internal static string ColorHexOf(int actor)
        {
            var ch = CharacterOf(actor);
            int viewId = ch != null && ch.photonView != null ? ch.photonView.ViewID : 0;

            if (viewId != 0 && _colorByViewId.TryGetValue(viewId, out var cached)) return cached;

            if (_colorRetryAt.TryGetValue(actor, out float next) && Time.time < next)
                return ColorFallback;

            if (TryReadColorOf(ch, out var hex))
            {
                if (viewId != 0) _colorByViewId[viewId] = hex;
                return hex;
            }

            _colorRetryAt[actor] = Time.time + ColorRetrySeconds;   // 未就绪：限流重试
            return ColorFallback;
        }

        private const string ColorFallback = "FFFFFF";
        private const float ColorRetrySeconds = 0.5f;
        private static readonly Dictionary<int, float> _colorRetryAt = new Dictionary<int, float>();

        /// <summary>尝试读取该角色的皮肤色；返回 false 表示装扮数据尚未就绪（应稍后重试）。</summary>
        private static bool TryReadColorOf(Character ch, out string hex)
        {
            hex = ColorFallback;
            try
            {
                if (ch == null || ch.refs == null || ch.refs.customization == null) return false;
                hex = ColorUtility.ToHtmlStringRGB(ch.refs.customization.PlayerColor);
                return true;
            }
            catch { return false; }
        }

        /// <summary>构造「【职业】」富文本前缀（无职业时返回 null）。</summary>
        internal static string BuildPrefix(int actor)
        {
            if (!Active) return null;

            string name = ClassNameOf(actor);
            if (string.IsNullOrEmpty(name)) return null;
            return $"<color=#{ColorHexOf(actor)}>【{name}】</color>";
        }

        /// <summary>剥掉本层加过的「【职业】」前缀，得到纯昵称（用于幂等重建）。</summary>
        internal static string Strip(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;
            if (!text.StartsWith("<color=#", StringComparison.Ordinal)) return text;

            int end = text.IndexOf("</color>", StringComparison.Ordinal);
            if (end < 0) return text;
            int bracket = text.IndexOf('】');
            if (bracket < 0 || bracket > end) return text;
            return text.Substring(end + "</color>".Length);
        }

        /// <summary>用「【职业】昵称」重建文本（幂等；无职业 / 未启用时返回纯昵称）。</summary>
        internal static string Rebuild(string text, int actor)
        {
            string plain = Strip(text);
            string prefix = BuildPrefix(actor);
            return prefix == null ? plain : prefix + plain;
        }

        /// <summary>从 <c>Character [昵称 : ActorNumber]</c> 这类物体名里解析 ActorNumber。</summary>
        internal static bool TryActorFromObjName(string objName, out int actor)
        {
            actor = 0;
            if (string.IsNullOrEmpty(objName)) return false;

            int open = objName.IndexOf('[');
            int colon = objName.LastIndexOf(':');
            if (open == -1 || colon == -1 || colon <= open) return false;

            string tail = objName.Substring(colon + 1).Trim().TrimEnd(']').Trim();
            return int.TryParse(tail, out actor);
        }

        // ---------------------------------------------------------------
        // 补丁实现
        // ---------------------------------------------------------------

        /// <summary>
        /// BetterPlayerDistance：<c>Tracking.ExtractDisplayName(string objName)</c> 的 Postfix。
        /// 覆盖其「昏迷 / 死亡标记」等每帧重新取名的标签（物体名里带 ActorNumber，可据此查职业）。
        /// </summary>
        internal static class BpdExtractNamePatch
        {
            public static void Postfix(string objName, ref string __result)
            {
                try
                {
                    if (!Active || string.IsNullOrEmpty(__result)) return;
                    if (!TryActorFromObjName(objName, out int actor)) return;   // 非玩家物体名：不动
                    if (string.IsNullOrEmpty(ClassNameOf(actor))) return;
                    __result = Rebuild(__result, actor);
                }
                catch { /* 兼容层不得影响主流程 */ }
            }
        }

        /// <summary>
        /// BetterPlayerDistance：<c>PlayerLabel.UpdateState(bool globalShow)</c> 的 Postfix。
        /// 其主标签的名字在构造时缓存于 <c>_displayName</c>，因此这里在「目标文本」变化时改写该字段并清空
        /// <c>_lastContent</c>，让其按既有排版逻辑重建（不改动其字号 / 距离 / 缓存机制）。
        ///
        /// 比较的是**最终文本**而不是职业数值：这样职业变化、皮肤色由「未就绪的白色」变为真实色、
        /// 前缀被关闭等情形都能被正确触发。
        /// </summary>
        internal static class BpdLabelPatch
        {
            private static readonly ConditionalWeakTable<object, LabelState> States =
                new ConditionalWeakTable<object, LabelState>();

            private sealed class LabelState
            {
                public string Original;   // 纯昵称（不含前缀）
                public string Desired;    // 上次写入的目标文本
            }

            public static void Postfix(object __instance)
            {
                try
                {
                    if (__instance == null) return;
                    if (_bpdDisplayNameField == null || _bpdCharacterField == null || _bpdLastContentField == null) return;

                    var character = _bpdCharacterField.GetValue(__instance) as Character;
                    if (character == null || character.photonView == null || character.photonView.Owner == null) return;
                    int actor = character.photonView.Owner.ActorNumber;

                    var st = States.GetOrCreateValue(__instance);
                    if (st.Original == null)
                        st.Original = Strip(_bpdDisplayNameField.GetValue(__instance) as string);

                    string desired = string.IsNullOrEmpty(st.Original)
                        ? st.Original
                        : Rebuild(st.Original, actor);

                    if (st.Desired == desired) return;   // 目标未变化：不做任何事
                    st.Desired = desired;

                    _bpdDisplayNameField.SetValue(__instance, desired);
                    _bpdLastContentField.SetValue(__instance, null);   // 触发其下一帧重建文本
                }
                catch { /* 兼容层不得影响主流程 */ }
            }
        }

        /// <summary>
        /// Sense of Direction：<c>PlayerLabel.Refresh(string name, …)</c> 的 Prefix。
        /// 其玩家屏幕标签每帧由 <c>PlayerLabelController.UpdateImpl</c> 调用
        /// <c>Refresh(character.characterName, …)</c> 写名；这里把传入的名字改写成带前缀的版本
        /// （改在 Refresh 之内，因此其「文本宽度 / 防重叠框」也会按带前缀的名字计算，排版不乱）。
        ///
        /// 角色取自 <c>PlayerLabelController._entries</c>（Character → Entry → PlayerLabel）：
        /// 找到 Label 字段正好等于当前 <paramref name="__instance"/> 的那一项即可。
        /// </summary>
        internal static class SodRefreshPatch
        {
            public static void Prefix(object __instance, ref string name)
            {
                try
                {
                    if (__instance == null || string.IsNullOrEmpty(name)) return;
                    if (!Active) return;   // 未启用：名字本就是纯昵称，不动
                    if (_sodEntriesField == null || _sodEntryLabelField == null) return;
                    if (_sodInstanceProp == null && _sodInstanceField == null) return;

                    var controller = _sodInstanceProp != null
                        ? _sodInstanceProp.GetValue(null)
                        : _sodInstanceField.GetValue(null);
                    if (controller == null) return;
                    if (!(_sodEntriesField.GetValue(controller) is IDictionary entries)) return;

                    foreach (DictionaryEntry kv in entries)
                    {
                        if (_sodEntryLabelField.GetValue(kv.Value) != __instance) continue;
                        if (!(kv.Key is Character ch)) return;
                        if (ch.photonView == null || ch.photonView.Owner == null) return;
                        name = Rebuild(name, ch.photonView.Owner.ActorNumber);
                        return;
                    }
                }
                catch { /* 兼容层不得影响主流程 */ }
            }
        }
    }
}
