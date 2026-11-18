using GuiQin.Net;
using System;
using System.Threading.Tasks;
using GuiQin.Train;
using UnityEngine;
using UnityEngine.UI;

namespace GuiQin
{
    // 技能修炼面板：门主（弟子的基础技能）/ 堂主（分堂技能）。对应网页版 openTrain。所有判定在服务端（train.do），这里只画和发请求。
    public class TrainUi
    {
        readonly GameObject root; readonly RectTransform box;
        readonly Func<RoleState> getRole; readonly Func<string, string, Task<string>> run; readonly Action<string> toast;
        string who; bool busy;

        public bool Visible { get { return root.activeSelf; } }

        public TrainUi(Transform parent, Func<RoleState> role, Func<string, string, Task<string>> run, Action<string> toast)
        {
            getRole = role; this.run = run; this.toast = toast;
            var dim = Ui.Box("trainUi", parent, WebUi.Dim); root = dim.gameObject;
            Ui.Anchor(dim.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var panel = WebUi.Panel("box", dim.transform); box = panel.rectTransform; WebUi.At(box, 24, 150, 672, 980);
            root.SetActive(false);
        }

        public void Open(string who) { this.who = who; root.SetActive(true); Draw(); }
        public void Close() { root.SetActive(false); }

        Text T(string text, int size, float x, float y, float w, float h, Color? c = null, TextAnchor a = TextAnchor.UpperLeft)
        {
            var t = Ui.Label("t", box, text, size, c ?? WebUi.Text, a); t.supportRichText = true; t.lineSpacing = 1.15f;
            WebUi.At(t.rectTransform, x, y, w, h); return t;
        }

        async void Do(string id)
        {
            if (busy) return; busy = true;
            try { string msg = await run(who, id); if (!string.IsNullOrEmpty(msg)) toast(msg); }
            finally { busy = false; }
            if (Visible) Draw();
        }

        void Draw()
        {
            for (int i = box.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(box.GetChild(i).gameObject);
            var role = getRole(); var cls = TrainService.Cls(role.cls); var T0 = GameData.Raw["train"];
            string name = who == "menzhu" ? (role.sect == "mo" ? "墨巨子" : "道家门主") : (string)cls["name"] + "堂主";
            var title = T("技能修炼", 34, 0, 8, 672, 52, null, TextAnchor.MiddleCenter); WebUi.Title(title);
            var line = Ui.Box("divider", box, WebUi.C("#e2b04a73")); line.raycastTarget = false; WebUi.At(line.rectTransform, 0, 66, 672, 1);
            var close = WebUi.Close(box, Close); WebUi.At((RectTransform)close.transform, 618, 12, 40, 40);
            T("银两 <color=#ffd56a>" + role.coin.ToString("N0") + "</color>", 20, 0, 78, 672, 30, null, TextAnchor.MiddleCenter);
            T(name + "：学习新技能、把学过的技能升级（技能每升一级，威力与疗效 +12%）。", 15, 30, 110, 612, 44, WebUi.Muted, TextAnchor.UpperCenter);
            var ids = TrainService.List(role, who);
            for (int i = 0; i < ids.Count; i++)
            {
                string id = ids[i]; var sk = GameData.Raw["skills"][id]; int cur = TrainService.Level(role, id); var ct = TrainService.Cost(role, id);
                float y = 168 + i * 132;
                var bg = WebUi.Gradient("row", box, WebUi.C("#22183a"), WebUi.C("#120c20"), 10); WebUi.At(bg.rectTransform, 20, y, 632, 122);
                T((string)sk["name"] + (cur > 0 ? "  <size=15><color=#a99db4>Lv." + cur + "</color></size>" : "  <size=15><color=#a99db4>未学习</color></size>"), 22, 36, y + 10, 380, 32, WebUi.Amber);
                T((string)sk["desc"], 14, 36, y + 44, 400, 72, WebUi.Text);
                if (cur >= TrainService.MaxLv) T("已满级", 16, 470, y + 10, 160, 100, WebUi.Muted, TextAnchor.MiddleCenter);
                else if (role.lv < ct.lvReq) T("需 " + ct.lvReq + " 级", 16, 470, y + 10, 160, 100, WebUi.Muted, TextAnchor.MiddleCenter);
                else
                {
                    bool afford = role.coin >= ct.coin;
                    var b = Ui.Btn("train_" + id, box, (cur > 0 ? "升到 " + (cur + 1) + " 级" : "学习") + "\n<size=14>银两 " + ct.coin.ToString("N0") + "</size>", 20, Color.clear, () => Do(id));
                    WebUi.Dress(b.image, afford ? WebUi.Red : WebUi.C("#3b2a5c"), afford ? WebUi.C("#5a1418") : WebUi.C("#1d1433"), WebUi.Gold, 8);
                    var bt = b.GetComponentInChildren<Text>(); bt.supportRichText = true; bt.color = WebUi.Light;
                    WebUi.At((RectTransform)b.transform, 470, y + 28, 160, 66); b.interactable = afford;
                }
            }
            if (who == "menzhu" && cls["heart"] != null)
                T((string)cls["heart"]["name"] + "（被动）：" + (string)cls["heart"]["desc"] + "。随人物等级成长，不需要修炼。", 14, 30, 168 + ids.Count * 132 + 6, 612, 60, WebUi.Muted);
        }
    }
}
