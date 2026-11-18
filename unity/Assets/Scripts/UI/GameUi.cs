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

    // 顶部栏：地图名 + 调试菜单按钮（世界地图、背包等正式界面移植完之前，用它切地图 / 切角色）
    public class HudUi
    {
        readonly Text place;
        public HudUi(Transform parent, Action onMenu)
        {
            var bar = Ui.Box("hud", parent, new Color(0.05f, 0.05f, 0.08f, 0.55f));
            Ui.Anchor(bar.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -84), new Vector2(0, 0));
            bar.raycastTarget = false;
            place = Ui.Label("place", bar.transform, "", 34, new Color(1f, 0.92f, 0.65f), TextAnchor.MiddleCenter);
            Ui.Anchor(place.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var b = Ui.Btn("menu", bar.transform, "菜单", 26, new Color(0.25f, 0.22f, 0.4f, 0.95f), () => onMenu());
            Ui.Anchor(b.GetComponent<RectTransform>(), new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-150, -30), new Vector2(-16, 30));
        }
        public void SetPlace(string s) { place.text = s; }
    }

    // 调试菜单：切地图、切角色外观、重置存档
    public class DebugMenuUi
    {
        readonly GameObject root;
        public DebugMenuUi(Transform parent, Action<string> goMap, Action nextLook, Action resetSave)
        {
            var bg = Ui.Box("debugMenu", parent, new Color(0.04f, 0.04f, 0.08f, 0.94f)); root = bg.gameObject;
            Ui.Anchor(bg.rectTransform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(40, 140), new Vector2(-40, -110));
            var title = Ui.Label("title", bg.transform, "菜单（移植过程中的临时入口）", 30, new Color(1f, 0.92f, 0.65f), TextAnchor.UpperCenter);
            Ui.Anchor(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -60), new Vector2(0, -12));
            int i = 0;
            foreach (var id in GameData.MapOrder)
            {
                GameData.Maps.TryGetValue(id, out var def); if (def == null) continue;
                string mid = id; int col = i % 2, row = i / 2; i++;
                var b = Ui.Btn("map_" + id, bg.transform, def.name, 28, new Color(0.22f, 0.3f, 0.45f, 1f), () => { root.SetActive(false); goMap(mid); });
                Ui.Anchor(b.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(0, 1), new Vector2(24 + col * 300, -140 - row * 80), new Vector2(24 + col * 300 + 280, -140 - row * 80 + 66));
            }
            var look = Ui.Btn("look", bg.transform, "换角色外观", 28, new Color(0.45f, 0.3f, 0.25f, 1f), () => nextLook());
            Ui.Anchor(look.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(0, 0), new Vector2(24, 100), new Vector2(304, 166));
            var rs = Ui.Btn("reset", bg.transform, "重置存档", 28, new Color(0.5f, 0.2f, 0.2f, 1f), () => { root.SetActive(false); resetSave(); });
            Ui.Anchor(rs.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(0, 0), new Vector2(324, 100), new Vector2(604, 166));
            var close = Ui.Btn("close", bg.transform, "关闭", 28, new Color(0.3f, 0.3f, 0.34f, 1f), () => root.SetActive(false));
            Ui.Anchor(close.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(0, 0), new Vector2(24, 20), new Vector2(604, 86));
            root.SetActive(false);
        }
        public void Toggle() { root.SetActive(!root.activeSelf); }
        public bool Visible { get { return root.activeSelf; } }
    }
}
