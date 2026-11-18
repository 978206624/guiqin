using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GuiQin
{
    // 对白框：头像 + 名字 + 逐字出现的文字。点一下：没出完就补全，出完就关闭并触发 after。
    public class DialogUi
    {
        readonly GameObject root; readonly Image face; readonly Text nameT, body;
        string full = ""; float shown; Action after;
        public bool Visible { get { return root.activeSelf; } }

        public DialogUi(Transform parent)
        {
            var bg = Ui.Box("dialog", parent, new Color(0.08f, 0.07f, 0.12f, 0.92f)); root = bg.gameObject;
            Ui.Anchor(bg.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(24, 40), new Vector2(-24, 300));
            face = Ui.Box("face", bg.transform, Color.white); face.preserveAspect = true; face.raycastTarget = false;
            Ui.Anchor(face.rectTransform, new Vector2(0, 0), new Vector2(0, 1), new Vector2(16, 16), new Vector2(196, -16));
            nameT = Ui.Label("name", bg.transform, "", 30, new Color(1f, 0.88f, 0.4f), TextAnchor.UpperLeft);
            Ui.Anchor(nameT.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(216, -58), new Vector2(-20, -14));
            body = Ui.Label("text", bg.transform, "", 28, Color.white, TextAnchor.UpperLeft);
            Ui.Anchor(body.rectTransform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(216, 14), new Vector2(-20, -64));
            body.lineSpacing = 1.15f;
            root.SetActive(false);
        }

        public void Open(string who, Color whoColor, Sprite head, string text, Action onClose)
        {
            nameT.text = who; nameT.color = whoColor;
            face.sprite = head; face.enabled = head != null;
            full = text ?? ""; shown = 0; body.text = ""; after = onClose; root.SetActive(true);
        }

        public void Tick(float dt)
        {
            if (!root.activeSelf || shown >= full.Length) return;
            shown = Mathf.Min(full.Length, shown + dt / 0.026f);
            body.text = full.Substring(0, Mathf.FloorToInt(shown));
        }

        public void Advance()
        {
            if (!root.activeSelf) return;
            if (shown < full.Length) { shown = full.Length; body.text = full; return; }
            root.SetActive(false);
            var f = after; after = null; if (f != null) f();
        }
    }

    // 底部浮出的一行提示
    public class ToastUi
    {
        readonly Text t; readonly CanvasGroup g; float left;

        public ToastUi(Transform parent)
        {
            var bg = Ui.Box("toast", parent, new Color(0.05f, 0.05f, 0.08f, 0.85f));
            Ui.Anchor(bg.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-260, 330), new Vector2(260, 392));
            g = bg.gameObject.AddComponent<CanvasGroup>(); g.alpha = 0; g.blocksRaycasts = false; g.interactable = false;
            t = Ui.Label("t", bg.transform, "", 26, Color.white, TextAnchor.MiddleCenter);
            Ui.Anchor(t.rectTransform, Vector2.zero, Vector2.one, new Vector2(12, 0), new Vector2(-12, 0));
        }

        public void Show(string s) { t.text = s; left = 1.8f; g.alpha = 1; }
        public void Tick(float dt) { if (left > 0) { left -= dt; g.alpha = Mathf.Clamp01(left / 0.4f); } }
    }

    // 顶部栏：天气按钮 + 地图名（点它打开世界地图）+ 世界 / 菜单按钮
    public class HudUi
    {
        readonly Text place, wxLabel; readonly GameObject barGo;
        public HudUi(Transform parent, Action onMenu, Action onWeather, Action onWorld, Action onBag)
        {
            var bar = Ui.Box("hud", parent, new Color(0.05f, 0.05f, 0.08f, 0.55f));
            Ui.Anchor(bar.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -84), new Vector2(0, 0));
            bar.raycastTarget = false; barGo = bar.gameObject;
            var pb = Ui.Btn("placeBtn", bar.transform, "", 34, new Color(0, 0, 0, 0), () => onWorld());      // 透明按钮：点地名 = 开世界地图
            Ui.Anchor(pb.GetComponent<RectTransform>(), Vector2.zero, Vector2.one, new Vector2(130, 0), new Vector2(-370, 0));
            place = pb.GetComponentInChildren<Text>(); place.color = new Color(1f, 0.92f, 0.65f); place.raycastTarget = false;
            var wb = Ui.Btn("weather", bar.transform, "晴", 26, new Color(0.2f, 0.36f, 0.55f, 0.95f), () => onWeather());
            Ui.Anchor(wb.GetComponent<RectTransform>(), new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(16, -30), new Vector2(116, 30));
            wxLabel = wb.GetComponentInChildren<Text>();
            var bb = Ui.Btn("bag", bar.transform, "行囊", 26, new Color(0.15f, 0.42f, 0.3f, 0.95f), () => onBag());
            Ui.Anchor(bb.GetComponent<RectTransform>(), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-358, -30), new Vector2(-272, 30));
            var wm = Ui.Btn("world", bar.transform, "世界", 26, new Color(0.45f, 0.3f, 0.12f, 0.95f), () => onWorld());
            Ui.Anchor(wm.GetComponent<RectTransform>(), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-262, -30), new Vector2(-166, 30));
            var b = Ui.Btn("menu", bar.transform, "设置", 26, new Color(0.25f, 0.22f, 0.4f, 0.95f), () => onMenu());
            Ui.Anchor(b.GetComponent<RectTransform>(), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-150, -30), new Vector2(-16, 30));
        }
        public void SetPlace(string s) { place.text = s; }
        public void SetVisible(bool v) { barGo.SetActive(v); }
        public void SetWeather(string s) { wxLabel.text = s; }
    }

    // 任务栏：挂在顶栏下面，点一下自动寻路到任务目标
    public class QuestUi
    {
        readonly GameObject go; readonly Text label, side;
        public QuestUi(Transform parent, Action onGo)
        {
            var b = Ui.Btn("quest", parent, "", 24, new Color(0.05f, 0.05f, 0.09f, 0.62f), () => onGo()); go = b.gameObject;
            Ui.Anchor(b.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, -172), new Vector2(-16, -92));
            label = b.GetComponentInChildren<Text>(); label.alignment = TextAnchor.MiddleLeft; label.supportRichText = true;
            Ui.Anchor(label.rectTransform, Vector2.zero, Vector2.one, new Vector2(14, 0), new Vector2(-150, 0));
            side = Ui.Label("side", b.transform, "", 20, new Color(0.7f, 0.75f, 0.9f), TextAnchor.MiddleRight);
            Ui.Anchor(side.rectTransform, Vector2.zero, Vector2.one, new Vector2(0, 0), new Vector2(-12, 0));
        }
        public void Set(string title, string goal, string chapter) { label.text = "<color=#ffe066><b>" + title + "</b></color>\n" + goal; side.text = chapter; }
        public void SetVisible(bool v) { go.SetActive(v); }
    }
}
