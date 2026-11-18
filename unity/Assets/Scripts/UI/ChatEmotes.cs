using System;
using System.Collections.Generic;
using UnityEngine;

namespace GuiQin
{
    // 用 24×24 坐标绘制表情。精灵首次使用时生成，并与表情选择条共用。
    internal static class ChatEmotes
    {
        public static readonly string[] Names = { "笑", "大笑", "哭", "怒", "惊", "酷", "晕", "睡", "害羞", "心", "赞", "眨眼" };
        static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
        static readonly Color Ink = WebUi.C("#5a2d0a"), Gold = WebUi.C("#ffd24a"), Edge = WebUi.C("#b9750d");

        public static bool Has(string name) => Array.IndexOf(Names, name) >= 0;

        public static Sprite Get(string name)
        {
            if (Cache.TryGetValue(name, out var sprite)) return sprite;
            const int size = 48;
            var canvas = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "聊天表情_" + name, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[size * size];
            void Paint(Func<Vector2, bool> inside, Color color)
            {
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                        if (inside(new Vector2((x + 0.5f) / 2, (size - y - 0.5f) / 2))) pixels[y * size + x] = color;
            }
            void Oval(float x, float y, float w, float h, Color color) =>
                Paint(p => Mathf.Pow((p.x - x) / w, 2) + Mathf.Pow((p.y - y) / h, 2) <= 1, color);
            void Stroke(Color color, params float[] points)
            {
                for (int i = 0; i + 3 < points.Length; i += 2)
                {
                    var a = new Vector2(points[i], points[i + 1]);
                    var d = new Vector2(points[i + 2], points[i + 3]) - a;
                    Paint(v => (v - a - d * Mathf.Clamp01(Vector2.Dot(v - a, d) / d.sqrMagnitude)).sqrMagnitude <= 0.65f, color);
                }
            }
            void Polygon(Color color, params Vector2[] vertices) => Paint(p =>
            {
                bool hit = false;
                for (int i = 0, j = vertices.Length - 1; i < vertices.Length; j = i++)
                    if ((vertices[i].y > p.y) != (vertices[j].y > p.y)
                        && p.x < (vertices[j].x - vertices[i].x) * (p.y - vertices[i].y) / (vertices[j].y - vertices[i].y) + vertices[i].x)
                        hit = !hit;
                return hit;
            }, color);

            if (name == "心")
            {
                var red = WebUi.C("#ff4d6d");
                Oval(8, 9, 5, 5, red); Oval(16, 9, 5, 5, red);
                Polygon(red, new Vector2(3, 10), new Vector2(21, 10), new Vector2(12, 21));
            }
            else if (name == "赞")
            {
                Polygon(Edge, new Vector2(2, 10), new Vector2(8, 10), new Vector2(12, 2), new Vector2(16, 3),
                    new Vector2(15, 9), new Vector2(21, 9), new Vector2(23, 12), new Vector2(19, 21), new Vector2(2, 21));
                Polygon(Gold, new Vector2(3, 11), new Vector2(8, 11), new Vector2(13, 3), new Vector2(15, 4),
                    new Vector2(14, 10), new Vector2(21, 10), new Vector2(22, 12), new Vector2(18, 20), new Vector2(3, 20));
                Stroke(Edge, 7, 11, 7, 21);
            }
            else
            {
                Oval(12, 12, 11, 11, Edge);
                Oval(12, 12, 10, 10, name == "怒" ? WebUi.C("#ff8a5a") : Gold);
                bool closed = name == "笑" || name == "大笑" || name == "害羞" || name == "睡";
                for (int x = 8; x <= 16; x += 8)
                {
                    if (name == "晕") { Stroke(Ink, x - 2, 8, x + 2, 12); Stroke(Ink, x + 2, 8, x - 2, 12); }
                    else if (name == "酷") Oval(x, 11, 3.4f, 2.6f, Ink);
                    else if (closed || name == "眨眼" && x == 8) Stroke(Ink, x - 2, 11, x, name == "睡" ? 12 : 9, x + 2, 11);
                    else
                    {
                        if (name == "惊") Oval(x, 10, 2.4f, 2.4f, Color.white);
                        Oval(x, 10, 1.2f, 1.2f, Ink);
                        if (name == "哭") Stroke(WebUi.Sky, x, 12, x, 17);
                        if (name == "怒") Stroke(Ink, x - 2, x == 8 ? 7 : 9, x + 2, x == 8 ? 9 : 7);
                    }
                }
                if (name == "酷") Stroke(Ink, 10, 10, 14, 10);
                if (name == "大笑" || name == "惊" || name == "睡")
                {
                    Oval(12, 16, name == "大笑" ? 4.8f : 2, name == "睡" ? 1.3f : 2.7f, Ink);
                    if (name == "大笑") Oval(12, 17.4f, 2.6f, 1.0f, WebUi.C("#ff8a7a"));
                    if (name == "睡") Stroke(WebUi.C("#3a78c8"), 16, 4, 20, 4, 16, 8, 20, 8);
                }
                else if (name == "哭" || name == "怒") Stroke(Ink, 8, 18, 11, 16, 13, 16, 16, 18);
                else if (name == "晕") Stroke(Ink, 8, 17, 10, 15.5f, 12, 17, 14, 15.5f, 16, 17);
                else Stroke(Ink, 8, 15, 10, 17, 14, 17, 16, 15);
                if (name == "害羞")
                {
                    var pink = WebUi.C("#ff7f9a");
                    Oval(6.5f, 14, 1.7f, 1.4f, pink); Oval(17.5f, 14, 1.7f, 1.4f, pink);
                }
            }
            canvas.SetPixels(pixels); canvas.Apply(false, true);
            sprite = Sprite.Create(canvas, new Rect(0, 0, size, size), Vector2.one * 0.5f, 100, 0, SpriteMeshType.FullRect);
            Cache[name] = sprite;
            return sprite;
        }
    }
}
