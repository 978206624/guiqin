using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using GuiQin.Chat;
using UnityEngine;
using UnityEngine.UI;

namespace GuiQin
{
    // 每条消息按字素换行，正文、表情、名字和物品分别用原生控件排在同一行。
    internal sealed class ChatLineUi
    {
        const float Step = 29;
        readonly float width, rowHeight, leftPadding;
        readonly int bodyFont;
        readonly bool singleLine;
        static readonly TextGenerator Measure = new TextGenerator();
        static readonly Regex Tags = new Regex(@"\[([^\[\]]{1,8})\]");
        readonly RectTransform row;
        float x, y = 6;

        ChatLineUi(Transform parent, bool notice, bool mini = false)
        {
            width = mini ? 421 : 684;
            rowHeight = mini ? 23 : Step;
            bodyFont = mini ? 15 : 19;
            leftPadding = mini ? 0 : 18;
            singleLine = mini;
            if (mini) y = 0;
            row = Ui.Rect("message", parent);
            if (notice)
            {
                var bg = WebUi.Gradient("notice", row, WebUi.C("#ffb02032"), WebUi.C("#ffb02012"), 4);
                Ui.Anchor(bg.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                var bar = Ui.Box("noticeEdge", row, WebUi.C("#ffb020")); bar.raycastTarget = false;
                Ui.Anchor(bar.rectTransform, Vector2.zero, Vector2.up, Vector2.zero, new Vector2(3, 0));
            }
        }

        public static RectTransform Draw(Transform parent, ChatMsg m, string label, Color color,
            Action<ChatPerson, RectTransform> onName, Action<string> onItem)
        {
            var line = new ChatLineUi(parent, m.ch == "notice");
            line.Plain("[" + label + "] ", color);
            if (m.ch != "system" && m.ch != "notice")
            {
                string name = m.ch == "pm" && m.self ? m.peer : m.from;
                if (m.ch == "pm" && m.self) line.Plain("你对 ", WebUi.Text);
                var person = new ChatPerson { name = name, cls = m.cls, lv = m.lv };
                if (!m.self && !string.IsNullOrEmpty(name))
                    line.Atom(name, WebUi.Sky, button: rect => onName(person, rect));
                else line.Plain(name, WebUi.Jade);
                if (m.ch == "pm") line.Plain(m.self ? " 说：" : " 对你说：", WebUi.Text);
                else
                {
                    var cls = (string)GameData.Raw["classes"][m.cls ?? ""]?["name"];
                    if (!string.IsNullOrEmpty(cls)) line.Plain(" " + cls + " " + m.lv + "级", WebUi.Muted, 13);
                    line.Plain("：", WebUi.Text);
                }
            }

            line.DrawBody(m, onItem);
            var time = DateTimeOffset.FromUnixTimeMilliseconds(Math.Max(0, Math.Min(253402300799999L, m.t))).ToLocalTime();
            line.Plain("  " + time.ToString("HH:mm"), WebUi.Muted, 12);
            line.row.gameObject.AddComponent<LayoutElement>().preferredHeight = line.y + Step + 3;
            return line.row;
        }

        public static void DrawMini(Transform parent, ChatMsg m, string channel, Color color, int index)
        {
            var line = new ChatLineUi(parent, false, true);
            WebUi.At(line.row, 0, index * 24, 421, 23);
            line.Plain("[" + channel + "] ", color);
            string author = m.ch == "pm" && m.self ? "你对 " + m.peer : m.from;
            if (!string.IsNullOrEmpty(author)) line.Plain(author + "：", m.self ? WebUi.Jade : WebUi.Sky);
            line.DrawBody(m, null);
        }

        void DrawBody(ChatMsg m, Action<string> onItem)
        {
            string text = m.text ?? "";
            var links = (m.links ?? new List<ChatItemLink>()).Where(link => link != null).ToLookup(link => link.start);
            int offset = 0;
            foreach (Match match in Tags.Matches(text))
            {
                Plain(text.Substring(offset, match.Index - offset), WebUi.Text);
                string token = match.Groups[1].Value;
                var link = links[match.Index].FirstOrDefault(l => l.length == match.Length && ItemLabel(l.itemId) == token);
                if (ChatEmotes.Has(token)) Atom(token, Color.white, sprite: ChatEmotes.Get(token));
                else if (link != null) Atom(match.Value, ItemColor(link.itemId),
                    button: onItem == null ? null : _ => onItem(link.itemId));
                else Plain(match.Value, WebUi.Text);
                offset = match.Index + match.Length;
            }
            Plain(text.Substring(offset), WebUi.Text);
        }

        public static string ItemLabel(string id) => id == null ? null : (string)GameData.Raw["items"][id]?["name"];
        public static Color ItemColor(string id)
        {
            var it = GameData.Raw["items"][id];
            string quality = ((int?)it?["q"] ?? 1).ToString();
            return WebUi.C((string)GameData.Raw["quality"][quality]?["color"] ?? "#f4ecda");
        }

        static float Size(string text, int font, bool bold = false)
        {
            var s = new TextGenerationSettings { font = Ui.F, fontSize = font, fontStyle = bold ? FontStyle.Bold : FontStyle.Normal,
                scaleFactor = 1, lineSpacing = 1, horizontalOverflow = HorizontalWrapMode.Overflow, verticalOverflow = VerticalWrapMode.Overflow };
            return Mathf.Ceil(Measure.GetPreferredWidth(text, s));
        }

        void Break() { x = 0; y += Step; }
        void Plain(string text, Color color, int font = 0)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (font == 0) font = bodyFont;
            if (singleLine) { Atom(text.Replace("\r", "").Replace("\n", " "), color, font); return; }
            var lines = text.Replace("\r", "").Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                if (i != 0) Break();
                string left = lines[i];
                while (left.Length > 0)
                {
                    float width = Size(left, font);
                    if (x + width <= this.width) { Atom(left, color, font); break; }
                    var glyphs = StringInfo.ParseCombiningCharacters(left);
                    int n = 0;
                    float chunk = 0;
                    for (int j = 1; j <= glyphs.Length; j++)
                    {
                        int end = j == glyphs.Length ? left.Length : glyphs[j];
                        float w = Size(left.Substring(0, end), font);
                        if (x + w > this.width) break;
                        n = end; chunk = w;
                    }
                    if (n == 0 && x > 0) { Break(); continue; }
                    if (n == 0) { n = glyphs.Length == 1 ? left.Length : glyphs[1]; chunk = this.width; }
                    Put(left.Substring(0, n), color, font, chunk, null, null);
                    left = left.Substring(n);
                    Break();
                }
            }
        }

        void Atom(string text, Color color, int size = 0, Sprite sprite = null, Action<RectTransform> button = null)
        {
            if (singleLine && x >= width) return;
            if (size == 0) size = bodyFont;
            float span = sprite != null ? rowHeight - 4 : Size(text, size, button != null);
            if (!singleLine && x > 0 && x + span > width) Break();
            Put(text, color, size, Mathf.Min(span, width), sprite, button);
        }

        void Put(string text, Color color, int size, float w, Sprite sprite, Action<RectTransform> click)
        {
            if (sprite != null)
            {
                var image = Ui.Box("emoji_" + text, row, Color.white);
                image.sprite = sprite; image.raycastTarget = false;
                WebUi.At(image.rectTransform, leftPadding + x + 1, y + 3, rowHeight - 6, rowHeight - 6);
            }
            else
            {
                var label = Ui.Label(click == null ? "text" : "link", row, text, size, color, TextAnchor.MiddleLeft);
                label.horizontalOverflow = HorizontalWrapMode.Overflow;
                label.supportRichText = false;
                WebUi.At(label.rectTransform, leftPadding + x, y, Mathf.Max(w, 1), rowHeight);
                if (click != null)
                {
                    label.fontStyle = FontStyle.Bold;
                    label.raycastTarget = true;
                    var button = label.gameObject.AddComponent<Button>();
                    button.targetGraphic = label; button.transition = Selectable.Transition.None;
                    button.onClick.AddListener(() => click(label.rectTransform));
                }
            }
            x += w;
        }
    }
}
