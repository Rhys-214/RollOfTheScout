using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PeakNativeUi
{
    /// <summary>
    /// 原生字体解析：取「PEAK 游戏原生默认 UI 字体」（TMP 资产）。
    ///
    /// 游戏里**没有任何 legacy <c>UnityEngine.Font</c>**（IMGUI 的 <c>GUIStyle.font</c> 装不进 TMP 资产），
    /// 原版 UI 一律是 TMP：主字体 <c>DarumaDropOne-Regular</c>（见 <c>FontFallbackSwapper.mainBaseFont</c>），
    /// 中文由该字体的 <c>fallbackFontAssetTable</c> 兜底（<c>FontFallbackSwapper</c> 按语言把下标 1
    /// 换成简体 / 繁体中文字体）。因此本模组也改用 TMP 绘制，字体直接取游戏的那一份，
    /// 中文即可走 TMP 的 fallback 链，与游戏原生渲染一致。
    /// </summary>
    public static class NativeFont
    {
        private static TMP_FontAsset _resolved;

        /// <summary>解析原生字体（失败返回 null，调用方应稍后重试）。</summary>
        public static TMP_FontAsset Resolve()
        {
            if (_resolved != null) return _resolved;

            // ① 游戏的语言字体重定向器持有的主字体（最关键、最原生的来源）
            try
            {
                var swapper = FontFallbackSwapper.instance;
                if (swapper != null && swapper.mainBaseFont != null && HasCjk(swapper.mainBaseFont))
                    return _resolved = swapper.mainBaseFont;
            }
            catch { /* 尚未初始化等：继续下一个来源 */ }

            // ② 任意原版头顶名牌的字体（与玩家名同款，必然是可用的原生字体）
            try
            {
                var names = Resources.FindObjectsOfTypeAll<PlayerName>();
                foreach (var pn in names)
                {
                    if (pn == null || pn.text == null) continue;
                    if (HasCjk(pn.text.font)) return _resolved = pn.text.font;
                }
            }
            catch { }

            // ③ 全量 TMP 字体：优先名字含 Daruma 的原生主字体；并另记一个含中文字形的字体兜底
            TMP_FontAsset daruma = null;
            TMP_FontAsset cjk = null;
            try
            {
                var fonts = Resources.FindObjectsOfTypeAll<TMP_FontAsset>();
                foreach (var f in fonts)
                {
                    if (f == null) continue;
                    if (daruma == null && f.name != null &&
                        f.name.IndexOf("Daruma", StringComparison.OrdinalIgnoreCase) >= 0) daruma = f;
                    if (cjk == null && HasCjk(f)) cjk = f;
                    if (daruma != null && cjk != null) break;
                }
            }
            catch { }

            if (daruma != null && HasCjk(daruma)) return _resolved = daruma;

            // 游戏语言不是中文时，主字体的 fallback 表未必接了中文字体 —— 此时直接用「含中文字形」的资产
            // （游戏自带 15 种语言，简中资产必在包内），保证中文不出现豆腐块。
            if (cjk != null) return _resolved = cjk;

            // ④ TMP 默认字体（至少是原版风格；无法渲染中文时宁可不用）
            try
            {
                var def = TMP_Settings.defaultFontAsset;
                if (def != null && HasCjk(def)) return _resolved = def;
            }
            catch { }

            return null;
        }

        /// <summary>该字体（含其 fallback 链）能否渲染中文字形 —— 用于避免「英文字体画中文变豆腐块」。</summary>
        private static bool HasCjk(TMP_FontAsset font)
        {
            try { return font != null && font.HasCharacter('职', true); }
            catch { return false; }
        }
    }

    /// <summary>
    /// 用**游戏原生字体**绘制的屏幕叠加文本（替代原来的 IMGUI 自绘面板）。
    ///
    /// · 自建 screen-space-overlay Canvas（<b>不挂 GraphicRaycaster</b>、文本 / 背景
    ///   <c>raycastTarget=false</c> → 绝不影响任何点击输入）；
    /// · <c>DontDestroyOnLoad</c> 单例，切场景不重建；
    /// · 懒创建 + 限流重试：字体尚未解析出来时返回「未就绪」，由调用方回退到旧的 IMGUI 绘制；
    /// · 两个槽位：左上「提示 Toast」与右上「职业分配面板」，排版沿用原有规则
    ///   （字号 ≈ 屏幕高/48、左/右各留 12px、标题行黄色加粗、最多若干行）。
    /// </summary>
    public static class NativeOverlay
    {
        private sealed class Block
        {
            public RectTransform Root;
            public Image Bg;
            public TextMeshProUGUI Text;
            public float WidthFraction;
            public bool Bold;
            public string Last;
            public float LastSize;
            public int LastScreenHeight;

            /// <summary>
            /// 调用方给出的「内容版本号」（-1 = 未提供，每次都比对文本）。
            /// 版本号未变时 <see cref="SetToast(IList{string},int)"/> 会**直接返回、连拼接都不做**，
            /// 用于消除「每帧把同一份提示重新拼成字符串」造成的 GC 抖动。
            /// </summary>
            public int LastVersion = -1;
        }

        private static bool _ready;
        private static float _nextTry;
        private static GameObject _root;
        private static Block _toast;
        private static Block _panel;

        public static bool Ready
        {
            get
            {
                if (_ready && _root == null) _ready = false;   // 被意外销毁（重载等）
                return _ready;
            }
        }

        /// <summary>尝试解析字体并创建叠加层；返回 true 表示原生字体 UI 可用。</summary>
        public static bool Ensure()
        {
            if (Ready) return true;
            if (Time.time < _nextTry) return false;
            _nextTry = Time.time + 1f;

            try
            {
                var font = NativeFont.Resolve();
                if (font == null) return false;

                Build(font);
                _ready = true;
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[PeakNativeUi] 创建原生字体叠加层失败（将回退旧绘制）：" + e.Message);
                return false;
            }
        }

        /// <summary>左上角提示（与主机端 Toast 队列同一份文本）。</summary>
        public static void SetToast(IList<string> lines)
        {
            if (!Ready) return;
            SetBlock(_toast, Join(lines, null), _toast.Bold);
        }

        /// <summary>
        /// 左上角提示的「带版本号」重载。同一版本号再次调用时**不拼接字符串**，
        /// 因此调用方可以每帧无成本地推送同一份提示（内容变化时自行递增 <paramref name="version"/>）。
        /// </summary>
        public static void SetToast(IList<string> lines, int version)
        {
            if (!Ready) return;
            var b = _toast;
            if (b == null) return;
            if (version >= 0 && version == b.LastVersion) return;   // 内容未变：连字符串都不重建
            b.LastVersion = version;
            SetBlock(b, Join(lines, null), b.Bold);
        }

        /// <summary>右上角职业分配面板。</summary>
        public static void SetPanel(string title, IList<string> lines, bool visible)
        {
            if (!Ready) return;
            if (!visible) { SetBlock(_panel, null, _panel.Bold); return; }
            SetBlock(_panel, Join(lines, title), _panel.Bold);
        }

        /// <summary>
        /// 只把右上角面板「隐藏」。用于「面板当前不可见」的分支 ——
        /// 与 <see cref="SetPanel"/> 的隐藏语义完全一致，但不拼接任何字符串（无每帧分配）。
        /// </summary>
        public static void SetPanelHidden()
        {
            if (!Ready) return;
            SetBlock(_panel, null, _panel.Bold);
        }

        /// <summary>销毁叠加层（插件卸载 / 重载时调用）。</summary>
        public static void Dispose()
        {
            try { if (_root != null) UnityEngine.Object.Destroy(_root); } catch { }
            _root = null;
            _toast = null;
            _panel = null;
            _ready = false;
            _nextTry = 0f;
        }

        // ---------------------------------------------------------------

        private static void Build(TMP_FontAsset font)
        {
            _root = new GameObject("RollOfTheScout.NativeOverlay", typeof(RectTransform));
            UnityEngine.Object.DontDestroyOnLoad(_root);

            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;   // 压在原版 HUD 之上
            // 刻意不添加 GraphicRaycaster：叠加层只显示、不接收任何输入

            _toast = CreateBlock("Toast", font, rightSide: false, widthFraction: 0.45f, bold: true);
            _panel = CreateBlock("ClassPanel", font, rightSide: true, widthFraction: 0.32f, bold: false);
        }

        private static Block CreateBlock(string name, TMP_FontAsset font, bool rightSide, float widthFraction, bool bold)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(_root.transform, false);

            var rect = go.GetComponent<RectTransform>();
            float side = rightSide ? 1f : 0f;
            rect.anchorMin = rect.anchorMax = new Vector2(side, 1f);
            rect.pivot = new Vector2(side, 1f);
            rect.anchoredPosition = rightSide ? new Vector2(-12f, -12f) : new Vector2(12f, -12f);
            rect.sizeDelta = new Vector2(400f, 40f);

            var bg = go.AddComponent<Image>();
            bg.color = new Color(0f, 0f, 0f, 0.6f);
            bg.raycastTarget = false;

            var textGo = new GameObject("Text", typeof(RectTransform));
            textGo.transform.SetParent(go.transform, false);
            var textRect = textGo.GetComponent<RectTransform>();
            textRect.anchorMin = textRect.anchorMax = new Vector2(0f, 1f);
            textRect.pivot = new Vector2(0f, 1f);
            textRect.anchoredPosition = new Vector2(10f, -6f);

            var text = textGo.AddComponent<TextMeshProUGUI>();
            text.font = font;
            if (font.material != null) text.fontSharedMaterial = font.material;
            text.raycastTarget = false;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.textWrappingMode = TextWrappingModes.Normal;
            text.overflowMode = TextOverflowModes.Overflow;
            text.color = Color.white;
            text.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;

            return new Block
            {
                Root = rect,
                Bg = bg,
                Text = text,
                WidthFraction = widthFraction,
                Bold = bold,
            };
        }

        private static void SetBlock(Block b, string content, bool bold)
        {
            if (b == null || b.Root == null) return;

            if (string.IsNullOrEmpty(content))
            {
                if (b.Root.gameObject.activeSelf) b.Root.gameObject.SetActive(false);
                b.Last = null;
                return;
            }
            if (!b.Root.gameObject.activeSelf) b.Root.gameObject.SetActive(true);

            float size = CurrentFontSize();
            if (content == b.Last && Mathf.Approximately(size, b.LastSize) && Screen.height == b.LastScreenHeight)
                return;   // 内容与字号都没变：不做 TMP 重排

            b.Last = content;
            b.LastSize = size;
            b.LastScreenHeight = Screen.height;

            float width = Screen.width * b.WidthFraction;
            var text = b.Text;

            text.fontSize = size;
            text.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
            text.text = content;

            // 先按固定宽度（减去左右内边距）排版，量出实际高度后再定面板大小
            var textRect = text.rectTransform;
            textRect.sizeDelta = new Vector2(width - 20f, 4000f);
            text.ForceMeshUpdate();

            float height = text.preferredHeight;
            textRect.sizeDelta = new Vector2(width - 20f, height);
            b.Root.sizeDelta = new Vector2(width, height + 12f);
        }

        private static float CurrentFontSize()
        {
            return Mathf.Max(16, Mathf.RoundToInt(Screen.height / 48f));
        }

        private static string Join(IList<string> lines, string title)
        {
            if ((lines == null || lines.Count == 0) && string.IsNullOrEmpty(title)) return null;

            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(title))
                sb.Append("<color=#FFE666><b>").Append(title).Append("</b></color>");
            if (lines != null)
            {
                for (int i = 0; i < lines.Count; i++)
                {
                    if (string.IsNullOrEmpty(lines[i])) continue;
                    if (sb.Length > 0) sb.Append('\n');
                    sb.Append(lines[i]);
                }
            }
            return sb.Length == 0 ? null : sb.ToString();
        }
    }
}
