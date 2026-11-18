using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GuiQin
{
    // HTML 舞台坐标从左上开始。所有位置均为 720×1280 设计像素。
    internal static class WebUi
    {
        public static readonly Color Gold = C("#e2b04a"), Amber = C("#ffd56a"), Text = C("#f4ecda"),
            Light = C("#fff0b8"), Muted = C("#a99db4"), Jade = C("#8ff08a"), Sky = C("#9fe0ff"),
            Dim = C("#0804129e"), Purple = C("#241a3c"), Red = C("#a8362e");
        static readonly Dictionary<(Color32, Color32, Color32, int), Sprite> skins = new();
        static Font display;
        static Sprite circle;

        public static Color C(string hex) => Ui.Hex(hex, Color.white);

        public static void At(RectTransform rect, float x, float y, float width, float height)
        {
            Ui.Anchor(rect, Vector2.up, Vector2.up, new Vector2(x, -y - height), new Vector2(x + width, -y));
        }

        public static Text Title(Text label)
        {
            if (display == null)
                display = Font.CreateDynamicFontFromOSFont(new[] { "Ma Shan Zheng", "KaiTi", "Kaiti SC", "STKaiti" }, 32);
            if (display != null) label.font = display;
            label.color = Amber;
            return label;
        }

        public static void Edge(Text label)
        {
            var outline = label.gameObject.AddComponent<Outline>();
            outline.effectColor = C("#120a14e6");
            outline.effectDistance = new Vector2(1, -1);
        }

        public static Image Panel(string name, Transform parent)
        {
            var image = Ui.Box(name, parent, Color.white);
            Dress(image, Purple, C("#140d24"), Gold, 12);
            return image;
        }

        public static Image Gradient(string name, Transform parent, Color top, Color bottom, int radius = 0)
        {
            var image = Ui.Box(name, parent, Color.white);
            image.raycastTarget = false;
            Dress(image, top, bottom, Color.clear, radius);
            return image;
        }

        public static void Dress(Image image, Color top, Color bottom, Color border, int radius)
        {
            var key = ((Color32)top, (Color32)bottom, (Color32)border, radius);
            if (!skins.TryGetValue(key, out var sprite))
            {
                const int n = 48;
                var texture = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "GuiQin UI", wrapMode = TextureWrapMode.Clamp };
                var pixels = new Color32[n * n];
                for (int y = 0; y < n; y++)
                {
                    var fill = Color.Lerp(bottom, top, y / (float)(n - 1));
                    for (int x = 0; x < n; x++)
                    {
                        var d = new Vector2(Mathf.Abs(x + 0.5f - n / 2f), Mathf.Abs(y + 0.5f - n / 2f));
                        d -= Vector2.one * (n / 2f - radius);
                        float distance = Vector2.Max(Vector2.zero, d).magnitude + Mathf.Min(Mathf.Max(d.x, d.y), 0) - radius;
                        var color = border.a > 0 ? Color.Lerp(fill, border, Mathf.Clamp01(distance + 1.7f)) : fill;
                        color.a *= Mathf.Clamp01(0.5f - distance);
                        pixels[y * n + x] = color;
                    }
                }
                texture.SetPixels32(pixels);
                texture.Apply(false, true);
                int inset = Mathf.Max(radius + 1, 2);
                sprite = Sprite.Create(texture, new Rect(0, 0, n, n), Vector2.one * 0.5f, 100, 0,
                    SpriteMeshType.FullRect, Vector4.one * inset);
                skins.Add(key, sprite);
            }
            image.sprite = sprite;
            image.color = Color.white;
            image.type = Image.Type.Sliced;
        }

        public static void Button(Button button, Color tone, int radius = 8)
        {
            if (tone.a <= 0) return;
            bool red = tone.r > tone.b * 1.35f && tone.g < tone.r * 0.53f;
            bool selected = !red && tone.r > tone.b * 1.3f && tone.g > tone.b * 1.15f && tone.r >= 0.4f;
            var top = red ? Red : selected ? C("#8a5a14") : C("#3b2a5c");
            var bottom = red ? C("#5a1418") : selected ? C("#5a3a0c") : C("#1d1433");
            Dress(button.image, top, bottom, Gold, radius);
            button.GetComponentInChildren<Text>().color = Light;
        }

        public static Image Icon(string name, Transform parent, string key)
        {
            var image = Ui.Box(name, parent, Color.white);
            image.sprite = Art.One(key, true);
            image.enabled = image.sprite != null;
            image.preserveAspect = true;
            image.raycastTarget = false;
            return image;
        }

        public static Sprite Circle()
        {
            if (circle != null) return circle;
            const int n = 64;
            var texture = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "GuiQin UI Circle", wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[n * n];
            var center = Vector2.one * (n / 2f - 0.5f);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float distance = (new Vector2(x, y) - center).magnitude;
                    pixels[y * n + x] = new Color(1, 1, 1, Mathf.Clamp01(n / 2f - distance));
                }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            circle = Sprite.Create(texture, new Rect(0, 0, n, n), Vector2.one * 0.5f, 100, 0, SpriteMeshType.FullRect);
            return circle;
        }

        public static Image RoundMask(string name, Transform parent)
        {
            var image = Ui.Box(name, parent, Color.white);
            image.sprite = Circle();
            image.raycastTarget = false;
            image.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            return image;
        }

        public static Button Close(Transform parent, UnityEngine.Events.UnityAction close)
        {
            return Ui.Btn("close", parent, "×", 24, Red, close);
        }
    }
}
