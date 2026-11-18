using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GuiQin.Pets;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace GuiQin
{
    // 宠物面板（属性 / 技能 / 图鉴 + 左侧已拥有宠物切换出战）和神兽使者（用神兽之魂兑换神兽、办神兽祭）。对应网页版 openPet / openEnvoy。
    // 面板里所有动作都走后端（pet.use / pet.exch / pet.rite / pet.gift），这里只负责画。
    public class PetUi
    {
        enum Mode { Pet, Envoy }

        readonly GameObject root; readonly RectTransform box;
        readonly Func<RoleState> getRole; readonly Func<string, string, Task<string>> run; readonly Action<string> toast;
        Mode mode; string tab = "attr", sel = "fox"; bool busy;

        public bool Visible { get { return root.activeSelf; } }

        // run(cmd, id)：发给后端并把结果合进角色，返回要提示的文字
        public PetUi(Transform parent, Func<RoleState> role, Func<string, string, Task<string>> run, Action<string> toast)
        {
            getRole = role; this.run = run; this.toast = toast;
            var dim = Ui.Box("petUi", parent, WebUi.Dim); root = dim.gameObject;
            Ui.Anchor(dim.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var panel = WebUi.Panel("box", dim.transform); box = panel.rectTransform;
            WebUi.At(box, 24, 110, 672, 1060);
            root.SetActive(false);
        }

        public void Open() { mode = Mode.Pet; tab = "attr"; sel = PetService.ActiveId(getRole()); root.SetActive(true); Draw(); }
        public void OpenEnvoy() { mode = Mode.Envoy; root.SetActive(true); Draw(); }
        public void Close() { root.SetActive(false); }

        // ---------------- 小工具 ----------------
        static JToken Quality(int q) { return GameData.Raw["petQuality"][q.ToString()]; }
        static Color QColor(int q) { return WebUi.C((string)Quality(q)["color"]); }
        static string Hex(Color c) { return "#" + ColorUtility.ToHtmlStringRGB(c); }
        static string Stars(int n, Color c) { return "<color=" + Hex(c) + ">" + new string('★', n) + "</color><color=#ffffff33>" + new string('★', 5 - n) + "</color>"; }
        static string Show(JToken dx) { var s = dx["show"] != null ? (string)dx["show"] : null; return s != null && Art.Has(s) ? s : (string)dx["sprite"]; }

        Text T(string text, int size, float x, float y, float w, float h, Color? color = null, TextAnchor a = TextAnchor.UpperLeft)
        {
            var t = Ui.Label("t", box, text, size, color ?? WebUi.Text, a); t.supportRichText = true; t.lineSpacing = 1.15f;
            WebUi.At(t.rectTransform, x, y, w, h); return t;
        }

        Image Pic(string key, float x, float y, float w, float h)
        {
            var im = WebUi.Icon("pic", box, key); WebUi.At(im.rectTransform, x, y, w, h); return im;
        }

        Button B(string label, int size, float x, float y, float w, float h, Color top, Color bottom, Action click, bool on = false)
        {
            var b = Ui.Btn("b_" + label, box, label, size, Color.clear, () => { if (!busy) click(); });
            WebUi.Dress(b.image, top, bottom, on ? WebUi.Amber : WebUi.Gold, 8);
            b.GetComponentInChildren<Text>().color = on ? WebUi.Light : WebUi.Text;
            WebUi.At((RectTransform)b.transform, x, y, w, h); return b;
        }

        Button Tone(string label, int size, float x, float y, float w, float h, bool red, Action click)
        {
            var b = B(label, size, x, y, w, h, red ? WebUi.Red : WebUi.C("#3b2a5c"), red ? WebUi.C("#5a1418") : WebUi.C("#1d1433"), click);
            b.GetComponentInChildren<Text>().color = WebUi.Light; return b;
        }

        void Clear() { for (int i = box.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(box.GetChild(i).gameObject); }

        async void Do(string cmd, string id)
        {
            if (busy) return; busy = true;
            try { string msg = await run(cmd, id); if (!string.IsNullOrEmpty(msg)) toast(msg); }
            finally { busy = false; }
            if (Visible) Draw();
        }

        void Header(string title)
        {
            var t = T(title, 34, 0, 8, 672, 52, null, TextAnchor.MiddleCenter); WebUi.Title(t);
            var line = Ui.Box("divider", box, WebUi.C("#e2b04a73")); line.raycastTarget = false; WebUi.At(line.rectTransform, 0, 66, 672, 1);
            var close = WebUi.Close(box, Close); WebUi.At((RectTransform)close.transform, 618, 12, 40, 40);
        }

        void Draw() { Clear(); if (mode == Mode.Envoy) DrawEnvoy(); else DrawPet(); }

        // ---------------- 宠物面板 ----------------
        void DrawPet()
        {
            var role = getRole(); string act = PetService.ActiveId(role);
            Header("宠物");
            // 左侧：已拥有的宠物，点一下切换出战
            var owned = PetService.AllIds().Where(id => PetService.Owned(role, id)).ToList();
            for (int i = 0; i < owned.Count; i++)
            {
                string id = owned[i]; var dx = PetService.Dex(id); var qc = QColor((int)dx["q"]); bool on = id == act;
                var chip = B("", 18, 14, 80 + i * 106, 112, 98, on ? WebUi.C("#8a5a14") : WebUi.C("#3b2a5c"), on ? WebUi.C("#5a3a0c") : WebUi.C("#1d1433"), () => { if (on) { sel = id; Draw(); } else Do(PetCmd.Use, id); }, on);
                var pic = Pic(Show(dx), 14 + 16, 80 + i * 106 + 4, 80, 60);
                T((string)dx["name"], 16, 14, 80 + i * 106 + 66, 112, 24, qc, TextAnchor.MiddleCenter);
                if (on) T("出战", 13, 14 + 70, 80 + i * 106 + 4, 40, 20, WebUi.Amber, TextAnchor.MiddleCenter);
            }
            // 右侧：当前选中的宠物（默认出战的那只）
            if (!owned.Contains(sel)) sel = act;
            DrawMain(role, act);
            if (!role.ssGift) Tone("领取神兽体验福利", 18, 14, 80 + owned.Count * 106 + 10, 112, 64, true, () => Do(PetCmd.Gift, null));
        }

        void DrawMain(RoleState role, string id)
        {
            var dx = PetService.Dex(id); int q = (int)dx["q"]; var qc = QColor(q); var s = PetService.Stats(role, id); var bd = PetService.BondOf(role);
            Pic(Show(dx), 150, 76, 170, 200);
            var name = T((string)dx["name"] + "  <size=16><color=" + Hex(qc) + ">【" + (string)Quality(q)["name"] + "】</color></size>", 30, 332, 80, 330, 40, WebUi.Light); WebUi.Title(name); name.color = WebUi.Light;
            T(Stars((int)Quality(q)["stars"], qc) + "　" + (string)dx["kind"], 17, 332, 124, 330, 28);
            T("等级 <color=#ffd56a>" + role.lv + "</color>（随主人成长）　战力 <color=#ffd56a>" + PetService.Power(s).ToString("N0") + "</color>", 17, 332, 156, 330, 28);
            T("亲密度 · <color=#ffd9a0>" + bd.name + "</color>　" + bd.n + (bd.to > 0 ? " / " + bd.to : ""), 15, 332, 190, 330, 24, WebUi.Muted);
            var back = WebUi.Gradient("bondBack", box, WebUi.C("#1a0f14"), WebUi.C("#1a0f14"), 5); WebUi.At(back.rectTransform, 332, 218, 320, 10);
            float pct = bd.to > 0 ? Mathf.Clamp01((bd.n - bd.from) / (float)(bd.to - bd.from)) : 1f;
            var fill = WebUi.Gradient("bondFill", box, WebUi.C("#fff0b8"), WebUi.C("#e2b04a"), 5); WebUi.At(fill.rectTransform, 332, 218, Mathf.Max(10f, 320f * pct), 10);

            string[][] tabs = { new[] { "attr", "属性" }, new[] { "skill", "技能" }, new[] { "dex", "图鉴" } };
            for (int i = 0; i < tabs.Length; i++)
            {
                string t = tabs[i][0]; bool on = tab == t;
                B(tabs[i][1], 20, 140 + i * 172, 290, 160, 48, on ? WebUi.C("#8a5a14") : WebUi.C("#3b2a5c"), on ? WebUi.C("#5a3a0c") : WebUi.C("#1d1433"), () => { tab = t; Draw(); }, on);
            }
            if (tab == "attr") DrawAttr(role, id, dx, s, bd);
            else if (tab == "skill") DrawSkills(dx, s, bd);
            else DrawDex(role);
        }

        void DrawAttr(RoleState role, string id, JToken dx, PetStats s, BondInfo bd)
        {
            var b = GameData.Raw["base"]; var g = GameData.Raw["grow"]; var m = PetService.Mult(id); int L = role.lv; float kb = 1f + bd.bonus;
            Func<string, string, float, string> note = (k, mk, kk) => "(基础 " + Mathf.Round((float)b[k] * (float)m[mk]) + " + 成长 " + Mathf.Round((float)g[k] * L * (float)m[mk]) + ")" + (bd.i > 0 && kk > 1f ? "  <color=#ffd9a0>× 亲密 " + kb.ToString("0.00") + "</color>" : "");
            var rows = new[] { new object[] { "气血", s.maxHp, note("hp", "hp", kb) }, new object[] { "攻击", s.atk, note("atk", "atk", kb) }, new object[] { "防御", s.def, note("def", "def", 1f) }, new object[] { "速度", s.spd, note("spd", "spd", 1f) } };
            for (int i = 0; i < rows.Length; i++)
            {
                float y = 362 + i * 60;
                var bg = WebUi.Gradient("row", box, WebUi.C("#22183a"), WebUi.C("#120c20"), 8); WebUi.At(bg.rectTransform, 140, y, 518, 52);
                T((string)rows[i][0], 20, 156, y, 70, 52, WebUi.Light, TextAnchor.MiddleLeft);
                T(rows[i][1].ToString(), 24, 226, y, 100, 52, WebUi.Amber, TextAnchor.MiddleLeft);
                T((string)rows[i][2], 14, 326, y, 330, 52, WebUi.Muted, TextAnchor.MiddleLeft);
            }
            var T5 = (JArray)GameData.Raw["bondTiers"];
            for (int i = 0; i < T5.Count; i++)
            {
                float x = 140 + i * 105; bool on = i <= bd.i;
                var c = WebUi.Gradient("tier", box, on ? WebUi.C("#8a5a14") : WebUi.C("#22183a"), on ? WebUi.C("#5a3a0c") : WebUi.C("#120c20"), 8); WebUi.At(c.rectTransform, x, 620, 98, 66);
                T((string)T5[i]["name"], 16, x, 624, 98, 28, on ? WebUi.Light : WebUi.Muted, TextAnchor.MiddleCenter);
                T(i == 0 ? "初始" : "≥ " + (int)T5[i]["at"] + " 战 · +" + i * 3 + "%", 12, x, 652, 98, 28, WebUi.Muted, TextAnchor.MiddleCenter);
            }
            T((string)dx["name"] + "的属性取主人裸属性的一部分，随等级成长；每打赢一场战斗亲密度 +1，亲密度升档后气血和攻击提高。", 15, 140, 708, 518, 80, WebUi.Muted);
        }

        void DrawSkills(JToken dx, PetStats s, BondInfo bd)
        {
            var items = new List<string[]>();     // {图标字, 名字, 副标题, 说明}
            if (dx["ss"] != null && (bool)dx["ss"])
            {
                foreach (var a in dx["act"].Where(a => (string)a[0] != "ss_hit"))
                {
                    var x = GameData.Raw["skills"][(string)a[0]];
                    items.Add(new[] { ((string)x["name"]).Substring(0, 1), (string)x["name"], "主动" + (x["cd"] != null ? " · 冷却 " + (int)x["cd"] + " 回合" : ""), (string)x["des"] });
                }
                if (dx["pasList"] != null) foreach (var a in dx["pasList"]) items.Add(new[] { ((string)a[0]).Substring(0, 1), (string)a[0], "被动", (string)a[1] });
            }
            else
            {
                items.Add(new[] { "咬", "撕咬", "普通招式", "扑上去撕咬一名敌人，造成 " + s.atk + " 点基础伤害。出手时有 65% 的几率使用。" });
                items.Add(new[] { "火", "狐火", "主动 · 35% 触发", "喷出一团幽蓝狐火，对一名敌人造成 " + Mathf.Round(s.atk * 1.6f) + " 点伤害（约 1.6 倍攻击），并优先攻击气血最低的敌人。" });
                items.Add(new[] { "灵", "灵犀", "被动", "与主人越亲近越强：当前「" + bd.name + "」档，气血、攻击" + (bd.i > 0 ? " +" + bd.i * 3 + "%" : "尚无加成") + "。" });
            }
            float y0 = 362;
            for (int i = 0; i < items.Count && i < 5; i++)
            {
                float y = y0 + i * 124;
                var bg = WebUi.Gradient("skill", box, WebUi.C("#22183a"), WebUi.C("#120c20"), 8); WebUi.At(bg.rectTransform, 140, y, 518, 116);
                var ico = WebUi.Gradient("ico", box, WebUi.C("#8a5a14"), WebUi.C("#5a3a0c"), 24); WebUi.At(ico.rectTransform, 154, y + 30, 54, 54);
                T(items[i][0], 26, 154, y + 30, 54, 54, WebUi.Light, TextAnchor.MiddleCenter);
                T(items[i][1] + "  <size=14><color=#a99db4>" + items[i][2] + "</color></size>", 20, 222, y + 8, 430, 28, WebUi.Amber);
                T(items[i][3], 14, 222, y + 38, 430, 74, WebUi.Text);
            }
            T("宠物在战斗中自动出手，不需要手动指挥。", 14, 140, y0 + Mathf.Min(items.Count, 5) * 124 + 4, 518, 30, WebUi.Muted);
        }

        void DrawDex(RoleState role)
        {
            var ids = PetService.AllIds();
            for (int i = 0; i < ids.Count; i++)
            {
                string id = ids[i]; var dx = PetService.Dex(id); bool own = PetService.Owned(role, id), on = id == sel; var qc = QColor((int)dx["q"]);
                float x = 140 + (i % 4) * 132, y = 360 + (i / 4) * 126;
                var cell = B("", 14, x, y, 122, 118, on ? WebUi.C("#8a5a14") : WebUi.C("#22183a"), on ? WebUi.C("#5a3a0c") : WebUi.C("#120c20"), () => { sel = id; Draw(); }, on);
                var pic = Pic(Show(dx), x + 11, y + 6, 100, 78); if (!own) pic.color = new Color(0, 0, 0, 0.75f);
                T(own ? (string)dx["name"] : "？？？", 15, x, y + 88, 122, 24, own ? qc : WebUi.Muted, TextAnchor.MiddleCenter);
            }
            int rows = (ids.Count + 3) / 4; float dy = 360 + rows * 126 + 6;
            var d = PetService.Dex(sel); int q = (int)d["q"]; bool owns = PetService.Owned(role, sel);
            T((string)d["name"] + "  <size=15><color=" + Hex(QColor(q)) + ">【" + (string)Quality(q)["name"] + "】</color></size>　" + Stars((int)Quality(q)["stars"], QColor(q)), 20, 140, dy, 518, 30, WebUi.Amber);
            T((string)d["kind"] + "\n" + (string)d["desc"], 15, 140, dy + 32, 518, 84);
            string how = owns ? "<color=#8ff08a>已拥有</color>　" + (PetService.IsSs(sel) ? "可在左侧切换出战。" : "出生自带，一直跟随主人。") : "获得途径：" + (string)d["how"] + (PetService.IsSs(sel) ? "" : "（暂未开放）");
            T(how, 15, 140, dy + 118, 518, 44, WebUi.Light);
            T("已收集 " + ids.Count(i => PetService.Owned(role, i)) + " / " + ids.Count, 14, 140, dy + 164, 518, 24, WebUi.Muted);
        }

        // ---------------- 神兽使者 ----------------
        void DrawEnvoy()
        {
            var role = getRole();
            int have = 0; if (role.bag != null) role.bag.items.TryGetValue("ss_soul", out have);
            int cost = (int)GameData.Raw["ssCost"], rite = (int)GameData.Raw["ssRite"]["coin"];
            Header("神兽使者");
            T("神兽之魂 <color=#ffd56a>" + have + "</color>　银两 <color=#ffd56a>" + role.coin.ToString("N0") + "</color>", 20, 0, 80, 672, 34, null, TextAnchor.MiddleCenter);
            var ids = PetService.AllIds().Where(PetService.IsSs).ToList();
            for (int i = 0; i < ids.Count; i++)
            {
                string id = ids[i]; var dx = PetService.Dex(id); bool own = PetService.Owned(role, id); float y = 128 + i * 106;
                var bg = WebUi.Gradient("row", box, WebUi.C("#22183a"), WebUi.C("#120c20"), 10); WebUi.At(bg.rectTransform, 20, y, 632, 98);
                Pic(Show(dx), 30, y + 4, 90, 90);
                T((string)dx["name"] + "  <size=14><color=#a99db4>" + (string)dx["kind"] + "</color></size>", 20, 134, y + 10, 340, 28, QColor((int)dx["q"]));
                T(own ? "<color=#8ff08a>已拥有</color>" : "需要神兽之魂 ×" + cost, 15, 134, y + 46, 340, 24, WebUi.Muted);
                var b = Tone(own ? "已拥有" : "兑换", 22, 500, y + 22, 140, 54, !own, () => Do(PetCmd.Exchange, id));
                b.interactable = !own && have >= cost;
            }
            float by = 128 + ids.Count * 106 + 8;
            Tone("办神兽祭　银两 " + rite, 24, 150, by, 372, 60, true, () => Do(PetCmd.Rite, null)).interactable = role.coin >= rite;
            T("神兽祭：奉上祭品，天神会赐下 " + (int)GameData.Raw["ssRite"]["min"] + "~" + (int)GameData.Raw["ssRite"]["max"] + " 枚神兽之魂。（原作兑换需要 99 枚，演示版 " + cost + " 枚）", 14, 30, by + 68, 612, 50, WebUi.Muted, TextAnchor.UpperCenter);
        }
    }
}
