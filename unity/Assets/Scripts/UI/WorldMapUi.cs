using System;
using UnityEngine;
using UnityEngine.UI;

namespace GuiQin
{
    // 世界地图：一张 2300×2300 的大图，拖动平移（松手有惯性），上面是各地图的地名牌，点地名传送。对应网页版 openWorld。
    public class WorldMapUi
    {
        readonly GameObject root; readonly RectTransform view, pan; readonly Text hint;
        readonly Action<string> onGo; readonly Func<string> curId;
        Vector2 vel; bool drag; float moved; Vector2 lastPos; float lastT;
        string here; Image[] dots;
        float mapW = 2300f, mapH = 2300f;

        public WorldMapUi(Transform parent, Action<string> onGo, Func<string> curId)
        {
            this.onGo = onGo; this.curId = curId;
            var bg = Ui.Box("worldMap", parent, new Color(0.85f, 0.77f, 0.6f, 1f)); root = bg.gameObject; view = bg.rectTransform;
            Ui.Anchor(view, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            bg.gameObject.AddComponent<RectMask2D>();

            pan = Ui.Rect("pan", view);
            pan.anchorMin = pan.anchorMax = pan.pivot = new Vector2(0, 1);               // 左上角为原点，和网页版坐标一致
            var wsz = Art.Size("world"); if (wsz != null) { mapW = wsz[0]; mapH = wsz[1]; }
            pan.sizeDelta = new Vector2(mapW, mapH);
            var img = new GameObject("img", typeof(RectTransform)).AddComponent<RawImage>();
            img.transform.SetParent(pan, false); img.texture = Art.Tex("world"); img.raycastTarget = false;
            Ui.Anchor(img.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            var ids = new System.Collections.Generic.List<string>();
            foreach (var kv in GameData.Maps) if (kv.Value.world != null) ids.Add(kv.Key);
            dots = new Image[ids.Count];
            for (int i = 0; i < ids.Count; i++) MakeNode(ids[i], i);

            var title = Ui.Label("title", bg.transform, "天下地图", 40, new Color(0.29f, 0.16f, 0.06f), TextAnchor.MiddleCenter);
            Ui.Anchor(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-170, -96), new Vector2(170, -30));
            hint = Ui.Label("hint", bg.transform, "拖动查看 · 点地名传送", 22, new Color(0.98f, 0.94f, 0.8f), TextAnchor.MiddleCenter);
            Ui.Anchor(hint.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-200, 110), new Vector2(200, 146));
            var home = Ui.Btn("home", bg.transform, "回到当前位置", 26, new Color(0.45f, 0.3f, 0.12f, 0.95f), () => Center(curId()));
            Ui.Anchor(home.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-250, 30), new Vector2(-10, 94));
            var close = Ui.Btn("close", bg.transform, "关闭", 26, new Color(0.3f, 0.3f, 0.34f, 0.95f), () => root.SetActive(false));
            Ui.Anchor(close.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(10, 30), new Vector2(250, 94));
            root.SetActive(false);
        }

        void MakeNode(string id, int idx)
        {
            var d = GameData.Maps[id];
            var node = Ui.Rect("node_" + id, pan);
            node.anchorMin = node.anchorMax = new Vector2(0, 1); node.pivot = new Vector2(0.5f, 0f);
            node.anchoredPosition = new Vector2(d.world[0], -d.world[1] + 22f);                // 牌子悬在标记点上方 22px
            node.sizeDelta = new Vector2(Mathf.Max(150f, d.name.Length * 34f + 44f), 76f);
            var card = node.gameObject.AddComponent<Image>(); card.color = d.sect ? new Color(0.66f, 0.78f, 0.6f, 1f) : new Color(0.93f, 0.85f, 0.62f, 1f);
            var btn = node.gameObject.AddComponent<Button>(); btn.targetGraphic = card;
            string mid = id; btn.onClick.AddListener(() => Click(mid));
            var name = Ui.Label("name", node, d.name, 28, new Color(0.29f, 0.16f, 0.06f), TextAnchor.UpperCenter);
            Ui.Anchor(name.rectTransform, Vector2.zero, Vector2.one, new Vector2(4, 0), new Vector2(-4, -6));
            var note = Ui.Label("note", node, d.note ?? "", 16, new Color(0.29f, 0.16f, 0.06f), TextAnchor.LowerCenter);
            Ui.Anchor(note.rectTransform, Vector2.zero, Vector2.one, new Vector2(4, 4), new Vector2(-4, 0));
            note.name = "note";
            var dot = Ui.Rect("dot", pan); dot.anchorMin = dot.anchorMax = new Vector2(0, 1); dot.pivot = new Vector2(0.5f, 0.5f);
            dot.anchoredPosition = new Vector2(d.world[0], -d.world[1]); dot.sizeDelta = new Vector2(18, 18);
            var di = dot.gameObject.AddComponent<Image>(); di.sprite = Art.Dot(); di.color = new Color(0.78f, 0.27f, 0.23f, 1f); di.raycastTarget = false;
            dots[idx] = di;
            var stem = Ui.Box("stem", pan, new Color(0.42f, 0.24f, 0.07f, 1f)); stem.raycastTarget = false;
            var sr = stem.rectTransform; sr.anchorMin = sr.anchorMax = new Vector2(0, 1); sr.pivot = new Vector2(0.5f, 0f);
            sr.anchoredPosition = new Vector2(d.world[0], -d.world[1] + 6f); sr.sizeDelta = new Vector2(3, 18);
        }

        void Click(string id)
        {
            if (moved > 10f) { moved = 0f; return; }                  // 拖动不算点击
            root.SetActive(false); onGo(id);
        }

        public bool Visible { get { return root.activeSelf; } }

        public void Open()
        {
            here = curId();
            foreach (Transform c in pan)                              // 标出“你在这里”
                if (c.name.StartsWith("node_"))
                {
                    bool me = c.name == "node_" + here;
                    var im = c.GetComponent<Image>(); var d = GameData.Maps[c.name.Substring(5)];
                    im.color = me ? new Color(0.78f, 0.27f, 0.23f, 1f) : (d.sect ? new Color(0.66f, 0.78f, 0.6f, 1f) : new Color(0.93f, 0.85f, 0.62f, 1f));
                    var texts = c.GetComponentsInChildren<Text>();
                    foreach (var t in texts) { if (t.gameObject.name == "note") t.text = me ? "你在这里" : (d.note ?? ""); t.color = me ? new Color(1f, 0.95f, 0.77f) : new Color(0.29f, 0.16f, 0.06f); }
                }
            root.SetActive(true); vel = Vector2.zero; drag = false; moved = 0f;
            Canvas.ForceUpdateCanvases();
            Center(here);
        }

        public void Close() { root.SetActive(false); }

        Vector2 ViewSize() { return view.rect.size; }

        void Clamp()
        {
            var v = ViewSize(); var p = pan.anchoredPosition;
            p.x = mapW <= v.x ? (v.x - mapW) / 2f : Mathf.Clamp(p.x, v.x - mapW, 0f);
            p.y = mapH <= v.y ? -(v.y - mapH) / 2f : Mathf.Clamp(p.y, 0f, mapH - v.y);
            pan.anchoredPosition = p;
        }

        void Center(string id)
        {
            vel = Vector2.zero;
            GameData.Maps.TryGetValue(id ?? "", out var d);
            var c = d != null && d.world != null ? new Vector2(d.world[0], d.world[1]) : new Vector2(mapW / 2f, mapH / 2f);
            var v = ViewSize();
            pan.anchoredPosition = new Vector2(v.x / 2f - c.x, -(v.y / 2f - c.y) + 0f);       // 让地图上的点 c 落在视口中心（y 向下 ↔ anchoredPosition.y 向上为正）
            Clamp();
        }

        // 每帧由 GameController 调用（指针状态已经 Poll 过）
        public void Tick(float dt)
        {
            if (!root.activeSelf) return;
            float k = view.rect.width / Mathf.Max(1, Screen.width);      // 屏幕像素 → 画布单位
            if (PointerInput.Down) { drag = true; moved = 0f; vel = Vector2.zero; lastPos = PointerInput.Pos; lastT = Time.unscaledTime; }
            if (drag && PointerInput.Held)
            {
                Vector2 dp = (PointerInput.Pos - lastPos) * k;          // 屏幕 y 向上；anchoredPosition.y 也是向上为正，直接相加
                moved += Mathf.Abs(dp.x) + Mathf.Abs(dp.y);
                if (moved > 10f)
                {
                    pan.anchoredPosition += dp; Clamp();
                    float dtt = Mathf.Max(0.001f, Time.unscaledTime - lastT);
                    vel = vel * 0.6f + dp / dtt * 0.4f;
                }
                lastPos = PointerInput.Pos; lastT = Time.unscaledTime;
            }
            if (PointerInput.Up) drag = false;
            if (!drag && vel.sqrMagnitude > 1f)                          // 惯性
            {
                pan.anchoredPosition += vel * dt; vel *= Mathf.Pow(0.93f, dt * 60f); Clamp();
            }
        }
    }
}
