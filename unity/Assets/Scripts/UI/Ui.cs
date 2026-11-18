using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace GuiQin
{
    // 界面小工具：全部用代码搭 uGUI，不依赖任何场景/预制体资产。
    // 字体用系统字体（中文），免得去做 CJK 字库；设计分辨率 720×1280，和网页版一致。
    public static class Ui
    {
        static Font font;
        public static Font F
        {
            get
            {
                if (font == null)
                    font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei UI", "Microsoft YaHei", "PingFang SC", "Heiti SC", "Noto Sans CJK SC", "Noto Sans SC", "Source Han Sans SC", "WenQuanYi Micro Hei", "Droid Sans Fallback", "Arial" }, 32);
                return font;
            }
        }

        public static Color Hex(string h, Color fallback)
        {
            Color c;
            return !string.IsNullOrEmpty(h) && ColorUtility.TryParseHtmlString(h, out c) ? c : fallback;
        }

        public static Canvas MakeCanvas(string name, int order, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var cv = go.AddComponent<Canvas>(); cv.renderMode = RenderMode.ScreenSpaceOverlay; cv.sortingOrder = order;
            var sc = go.AddComponent<CanvasScaler>();
            sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(720, 1280); sc.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return cv;
        }

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static void Anchor(RectTransform r, Vector2 min, Vector2 max, Vector2 offMin, Vector2 offMax)
        {
            r.anchorMin = min; r.anchorMax = max; r.offsetMin = offMin; r.offsetMax = offMax;
        }

        public static Image Box(string name, Transform parent, Color c)
        {
            var r = Rect(name, parent); var im = r.gameObject.AddComponent<Image>(); im.color = c; return im;
        }

        public static Text Label(string name, Transform parent, string s, int size, Color c, TextAnchor anchor)
        {
            var r = Rect(name, parent); var t = r.gameObject.AddComponent<Text>();
            t.font = F; t.text = s; t.fontSize = size; t.color = c; t.alignment = anchor; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        // 单行输入框（旧版 InputField，带占位字；password=true 时显示成圆点）
        public static InputField Input(string name, Transform parent, string placeholder, int size, bool password = false, int limit = 0)
        {
            var im = Box(name, parent, new Color(0.1f, 0.11f, 0.18f, 0.95f)); var f = im.gameObject.AddComponent<InputField>(); f.targetGraphic = im;
            var t = Label("text", im.transform, "", size, Color.white, TextAnchor.MiddleLeft); t.horizontalOverflow = HorizontalWrapMode.Overflow; t.supportRichText = false;
            Anchor(t.rectTransform, Vector2.zero, Vector2.one, new Vector2(16, 4), new Vector2(-16, -4));
            var ph = Label("ph", im.transform, placeholder, size, new Color(0.55f, 0.58f, 0.7f), TextAnchor.MiddleLeft); ph.fontStyle = FontStyle.Italic;
            Anchor(ph.rectTransform, Vector2.zero, Vector2.one, new Vector2(16, 4), new Vector2(-16, -4));
            f.textComponent = t; f.placeholder = ph; f.lineType = InputField.LineType.SingleLine; f.characterLimit = limit;
            if (password) f.contentType = InputField.ContentType.Password;
            return f;
        }

        public static Button Btn(string name, Transform parent, string label, int size, Color bg, UnityAction onClick)
        {
            var im = Box(name, parent, bg); var b = im.gameObject.AddComponent<Button>(); b.targetGraphic = im;
            var t = Label("t", im.transform, label, size, Color.white, TextAnchor.MiddleCenter);
            Anchor(t.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            if (onClick != null) b.onClick.AddListener(onClick);
            return b;
        }
    }

    // 角色头顶的名牌（称号 + 名字），挂在屏幕空间的 Canvas 上，每帧跟着世界位置走
    public class TagLabel
    {
        public readonly RectTransform Root;
        readonly Text title, name;

        public TagLabel(Transform parent)
        {
            Root = Ui.Rect("tag", parent); Root.sizeDelta = new Vector2(260, 60); Root.pivot = new Vector2(0.5f, 0f);
            name = Ui.Label("name", Root, "", 24, Color.white, TextAnchor.LowerCenter);
            Ui.Anchor(name.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, 30));
            title = Ui.Label("title", Root, "", 18, Color.white, TextAnchor.LowerCenter);
            Ui.Anchor(title.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 28), new Vector2(0, 54));
            AddShadow(name); AddShadow(title);
        }

        static void AddShadow(Text t) { var o = t.gameObject.AddComponent<Outline>(); o.effectColor = new Color(0, 0, 0, 0.85f); o.effectDistance = new Vector2(1.5f, -1.5f); }

        public void Set(string titleText, Color titleColor, string nameText, Color nameColor)
        {
            title.text = titleText ?? ""; title.color = titleColor;
            name.text = nameText ?? ""; name.color = nameColor;
        }

        public void SetVisible(bool v) { if (Root.gameObject.activeSelf != v) Root.gameObject.SetActive(v); }
        public void Destroy() { if (Root != null) Object.Destroy(Root.gameObject); }
    }
}
