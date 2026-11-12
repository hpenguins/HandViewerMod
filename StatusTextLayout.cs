using System;
using System.Collections.Generic;
using System.Text;

namespace HandViewerMod
{
    /// <summary>
    /// 把状态文案(服务器返回的提示、错误信息)排进固定尺寸的框里。
    ///
    /// 背景: FairyGUI 的 <c>GTextField</c> 默认 <c>autoSize = Both</c> 且不换行,
    /// 一段中文提示会被排成一整行并溢出边框; 这里先按可用宽高折行, 再挑能放下的字号,
    /// 实在放不下就截断(完整文案仍由 tooltip 展示)。
    /// </summary>
    public sealed class StatusTextLayout
    {
        /// <summary>FairyGUI 默认 lineSpacing。</summary>
        public const float LineSpacing = 3f;
        /// <summary>字体的行高系数(与 FairyGUI 的行距算法一致, 留一点余量)。</summary>
        public const float LineHeightRatio = 1.2f;

        readonly List<string> lines = new List<string>();
        public IReadOnlyList<string> Lines { get { return lines; } }
        /// <summary>选中的字号。</summary>
        public int FontSize { get; private set; }
        /// <summary>文案是否被截断(完整内容需要看 tooltip)。</summary>
        public bool Truncated { get; private set; }
        /// <summary>折行后的文本, 用 "\n" 分隔。</summary>
        public string Text { get { return string.Join("\n", lines); } }

        /// <summary>某字号下的单行高度。</summary>
        public static float LineHeight(int fontSize) { return fontSize * LineHeightRatio + LineSpacing; }

        /// <summary>
        /// 把 <paramref name="text"/> 排进 <paramref name="width"/> × <paramref name="height"/> 的框里。
        /// 从 <paramref name="maxSize"/> 起逐级缩小字号, 取第一个能完整放下的字号;
        /// 最小字号仍放不下时截断到能放下的行数并加省略号。
        /// </summary>
        public static StatusTextLayout Fit(string text, float width, float height, int maxSize, int minSize)
        {
            var layout = new StatusTextLayout();
            if (maxSize < 1) maxSize = 1;
            if (minSize < 6) minSize = 6;
            if (maxSize < minSize) maxSize = minSize;
            layout.FontSize = maxSize;
            if (string.IsNullOrEmpty(text) || width <= 0f || height <= 0f) return layout;

            string normalized = Normalize(text);
            List<string> smallest = null;
            for (int size = maxSize; size >= minSize; size--)
            {
                var wrapped = Wrap(normalized, width, size);
                if (wrapped.Count * LineHeight(size) <= height + 0.5f)
                {
                    layout.lines.AddRange(wrapped);
                    layout.FontSize = size;
                    return layout;
                }
                if (size == minSize) smallest = wrapped;
            }

            // 最小字号也放不下: 只保留能显示的行, 最后一行加省略号。
            layout.FontSize = minSize;
            if (smallest == null || smallest.Count == 0) return layout;
            int maxLines = Math.Max(1, (int)Math.Floor(height / LineHeight(minSize) + 0.5f));
            int keep = Math.Min(smallest.Count, maxLines);
            for (int i = 0; i < keep; i++) layout.lines.Add(smallest[i]);
            if (keep < smallest.Count)
            {
                layout.Truncated = true;
                layout.lines[keep - 1] = Ellipsize(layout.lines[keep - 1], width, minSize);
            }
            return layout;
        }

        /// <summary>统一换行符并去掉控制字符, 避免文本框里出现方块或换行错位。</summary>
        public static string Normalize(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            var sb = new StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '\r')
                {
                    if (i + 1 < text.Length && text[i + 1] == '\n') continue;
                    sb.Append('\n');
                }
                else if (c == '\n') sb.Append('\n');
                else if (char.IsControl(c)) sb.Append(' ');
                else sb.Append(c);
            }
            return sb.ToString();
        }

        /// <summary>估算字符宽度: 全角按一个字宽, 其余按 0.55 倍字号。</summary>
        public static float CharWidth(char c, int size)
        {
            if (c == '\t') return size * 2f;
            return c < 0x2E80 ? size * 0.55f : size;
        }

        /// <summary>按给定宽度折行; 拉丁词优先在空格处断开, 中文逐字断行。</summary>
        public static List<string> Wrap(string text, float width, int size)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(text)) { result.Add(string.Empty); return result; }
            string[] paragraphs = text.Split('\n');
            foreach (var paragraph in paragraphs)
            {
                if (paragraph.Length == 0) { result.Add(string.Empty); continue; }
                var current = new StringBuilder();
                float currentWidth = 0f;
                int spaceBreak = -1; // current 中可以断行的位置(空格之后)
                foreach (char c in paragraph)
                {
                    float charWidth = CharWidth(c, size);
                    if (currentWidth + charWidth > width && current.Length > 0)
                    {
                        if (spaceBreak > 0)
                        {
                            result.Add(current.ToString(0, spaceBreak - 1).TrimEnd());
                            string rest = current.ToString(spaceBreak, current.Length - spaceBreak);
                            current.Clear();
                            current.Append(rest);
                            currentWidth = Measure(rest, size);
                        }
                        else
                        {
                            result.Add(current.ToString());
                            current.Clear();
                            currentWidth = 0f;
                        }
                        spaceBreak = -1;
                    }
                    current.Append(c);
                    currentWidth += charWidth;
                    if (c == ' ') spaceBreak = current.Length;
                }
                if (current.Length > 0) result.Add(current.ToString());
            }
            return result;
        }

        /// <summary>估算一段文本的宽度。</summary>
        public static float Measure(string text, int size)
        {
            if (string.IsNullOrEmpty(text)) return 0f;
            float width = 0f;
            for (int i = 0; i < text.Length; i++) width += CharWidth(text[i], size);
            return width;
        }

        /// <summary>在行尾加省略号, 并保证加完仍不超出给定宽度。</summary>
        public static string Ellipsize(string line, float width, int size)
        {
            if (string.IsNullOrEmpty(line)) return "…";
            string result = line;
            while (result.Length > 0 && Measure(result + "…", size) > width) result = result.Substring(0, result.Length - 1);
            return result + "…";
        }
    }
}
