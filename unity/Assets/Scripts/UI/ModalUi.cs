using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GuiQin
{
    // 通用确认弹窗：标题 + 一段文字（支持 <b> / <color>）+ 至多三个按钮。拜入门派、玄兵塔入口、确认删除之类都用它。
    public class ModalUi
    {
        public struct Btn { public string Label; public bool Red; public Action OnClick; public Btn(string l, Action a, bool red = false) { Label = l; OnClick = a; Red = red; } }

        readonly GameObject root; readonly Text title, body; readonly RectTransform row; readonly Image portrait;
        public bool Visible { get { return root.activeSelf; } }

        public ModalUi(Transform parent)
        {
            var dim = Ui.Box("modal", parent, WebUi.Dim); root = dim.gameObject;
            Ui.Anchor(dim.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var box = WebUi.Panel("box", dim.transform);
            Ui.Anchor(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-310, -250), new Vector2(310, 250));
            title = Ui.Label("title", box.transform, "", 34, new Color(1f, 0.92f, 0.65f), TextAnchor.MiddleCenter);
            Ui.Anchor(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -70), new Vector2(0, -10));
            WebUi.Title(title);
            var line = Ui.Box("divider", box.transform, WebUi.C("#e2b04a73")); line.raycastTarget = false;
            WebUi.At(line.rectTransform, 0, 66, 620, 1);
            var close = WebUi.Close(box.transform, Close);
            WebUi.At((RectTransform)close.transform, 570, 10, 40, 40);
            portrait = Ui.Box("portrait", box.transform, Color.white);
            portrait.raycastTarget = false; portrait.preserveAspect = true;
            WebUi.At(portrait.rectTransform, 20, 82, 228, 305);
            body = Ui.Label("body", box.transform, "", 22, WebUi.Text, TextAnchor.UpperCenter); body.supportRichText = true; body.lineSpacing = 1.2f;
            Ui.Anchor(body.rectTransform, Vector2.zero, Vector2.one, new Vector2(30, 100), new Vector2(-30, -84));
            row = Ui.Rect("row", box.transform); Ui.Anchor(row, new Vector2(0, 0), new Vector2(1, 0), new Vector2(20, 18), new Vector2(-20, 84));
            root.SetActive(false);
        }

        public void Open(string t, string text, params Btn[] btns)
        {
            Show(t, text, null, btns);
        }

        public void OpenWithPortrait(string t, string key, string text, params Btn[] btns)
        {
            Show(t, text, Art.Has(key) ? Art.One(key, true) : null, btns);
        }

        void Show(string t, string text, Sprite art, Btn[] btns)
        {
            title.text = t; body.text = text;
            portrait.sprite = art;
            portrait.enabled = art != null;
            body.alignment = art == null ? TextAnchor.UpperCenter : TextAnchor.MiddleLeft;
            Ui.Anchor(body.rectTransform, Vector2.zero, Vector2.one,
                new Vector2(art == null ? 30 : 275, 100), new Vector2(-30, -84));
            for (int i = row.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(row.GetChild(i).gameObject);
            int n = btns.Length; float w = 1f / Math.Max(1, n);
            for (int i = 0; i < n; i++)
            {
                var b = btns[i];
                var btn = Ui.Btn("b" + i, row, b.Label, 28, b.Red ? new Color(0.7f, 0.25f, 0.2f) : new Color(0.25f, 0.3f, 0.5f), () => { Close(); if (b.OnClick != null) b.OnClick(); });
                Ui.Anchor(btn.GetComponent<RectTransform>(), new Vector2(i * w, 0), new Vector2((i + 1) * w, 1), new Vector2(6, 0), new Vector2(-6, 0));
            }
            root.SetActive(true);
        }

        public void Close() { root.SetActive(false); }
    }
}
