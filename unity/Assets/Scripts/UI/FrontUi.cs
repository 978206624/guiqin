using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GuiQin.Net;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace GuiQin
{
    // 进游戏之前的界面：登录 → 选区 → 角色列表 / 创建角色。对应网页版 front.js。
    // 账号密码、区服列表、角色增删、名字校验全部走后端；客户端只记“上次的账号”这种设备便利信息（不记密码）。
    public class FrontUi
    {
        enum Scr { Login, Server, Roles, Create }

        readonly GameObject root; readonly RectTransform content; readonly Func<ModalUi> getModal; ModalUi modal { get { return getModal(); } }
        readonly Func<IGameBackend> net; readonly Func<string, Task<string>> enterGame;      // enterGame：进游戏，返回错误文字（成功返回 null）
        string account = ""; ServerInfo server; LoginResp login; List<RoleBrief> roles = new List<RoleBrief>();
        int picked; string group = "rec"; bool busy;
        string draftGender = "f", draftCls = "novice_f";
        const int MaxRoles = 3;
        static readonly string[] NamesA = { "云", "沈", "陆", "苏", "顾", "白", "叶", "楚", "燕", "秦", "江", "温", "谢", "萧" };
        static readonly string[] NamesM = { "中客", "长风", "惊鸿", "无咎", "临渊", "照夜", "停云", "归雁", "怀瑾", "行舟" }, NamesF = { "清歌", "晚照", "听雪", "疏影", "知微", "若水", "采薇", "映月", "流萤", "锦瑟" };
        static readonly Dictionary<string, string> StatusName = new Dictionary<string, string> { { "new", "新服" }, { "ok", "流畅" }, { "busy", "繁忙" }, { "hot", "爆满" }, { "down", "维护" } };
        static readonly Dictionary<string, Color> StatusColor = new Dictionary<string, Color> { { "new", new Color(0.4f, 0.9f, 1f) }, { "ok", new Color(0.5f, 0.9f, 0.55f) }, { "busy", new Color(1f, 0.8f, 0.4f) }, { "hot", new Color(1f, 0.5f, 0.45f) }, { "down", new Color(0.6f, 0.6f, 0.65f) } };

        public bool Visible { get { return root.activeSelf; } }

        public FrontUi(Transform parent, Func<ModalUi> modal, Func<IGameBackend> net, Func<string, Task<string>> enterGame)
        {
            this.getModal = modal; this.net = net; this.enterGame = enterGame;
            var bg = Ui.Box("front", parent, new Color(0.05f, 0.05f, 0.09f, 1f)); root = bg.gameObject;
            Ui.Anchor(bg.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var tex = Art.Tex("login");
            if (tex != null)
            {
                var raw = new GameObject("art", typeof(RectTransform)).AddComponent<RawImage>(); raw.transform.SetParent(bg.transform, false); raw.texture = tex; raw.raycastTarget = false;
                Ui.Anchor(raw.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            }
            content = Ui.Rect("content", bg.transform); Ui.Anchor(content, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            root.SetActive(false);
        }

        // 打开：toRoles=true 且已经选过区（比如游戏里“切换角色”）就直接回角色列表
        public void Open(bool toRoles)
        {
            root.SetActive(true);
            if (toRoles && server != null && login != null) { RefreshRoles(); } else Show(Scr.Login);
        }
        public void Hide() { root.SetActive(false); }

        // ---------------- 通用 ----------------
        static Sprite Head(string cls) { string k = "head_" + cls; return Art.Has(k) ? Art.One(k, true) : null; }
        static JToken Cls(string id) { return GameData.Raw["classes"][id]; }
        static string ClsTitle(string id) { return GuiQin.Sect.SectService.ClsName(id); }

        void Clear() { for (int i = content.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(content.GetChild(i).gameObject); }
        Text Err(float yTop) { var t = Ui.Label("err", content, "", 24, new Color(1f, 0.55f, 0.5f), TextAnchor.MiddleCenter); Ui.Anchor(t.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(30, yTop), new Vector2(-30, yTop + 40)); return t; }
        Button Big(string label, Vector2 min, Vector2 max, bool red, UnityEngine.Events.UnityAction a, Vector2? anchor = null)
        {
            var b = Ui.Btn("btn_" + label, content, label, 30, red ? new Color(0.7f, 0.25f, 0.2f) : new Color(0.25f, 0.3f, 0.5f), a);
            Ui.Anchor(b.GetComponent<RectTransform>(), anchor ?? new Vector2(0, 0), anchor ?? new Vector2(0, 0), min, max); return b;
        }
        void Panel(Vector2 min, Vector2 max) { var p = Ui.Box("panel", content, new Color(0.04f, 0.04f, 0.08f, 0.72f)); p.raycastTarget = true; Ui.Anchor(p.rectTransform, new Vector2(0, 0), new Vector2(1, 1), min, max); }

        void Show(Scr s)
        {
            Clear();
            switch (s) { case Scr.Login: DrawLogin(); break; case Scr.Server: DrawServers(); break; case Scr.Roles: DrawRoles(); break; case Scr.Create: DrawCreate(); break; }
        }

        // ---------------- 登录 ----------------
        void DrawLogin()
        {
            var t = Ui.Label("title", content, "归  秦", 110, new Color(1f, 0.92f, 0.65f), TextAnchor.MiddleCenter);
            Ui.Anchor(t.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -420), new Vector2(0, -250));
            var sub = Ui.Label("sub", content, "2.5D 武侠 · 手游", 28, new Color(0.9f, 0.92f, 1f), TextAnchor.MiddleCenter);
            Ui.Anchor(sub.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -470), new Vector2(0, -420));
            Panel(new Vector2(60, 90), new Vector2(-60, 590));
            var acc = Ui.Input("account", content, "账号", 30, false, 24); acc.text = PlayerPrefs.GetString("gq_account", "");
            Ui.Anchor(acc.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(1, 0), new Vector2(100, 470), new Vector2(-100, 550));
            var pwd = Ui.Input("password", content, "密码", 30, true, 32);
            Ui.Anchor(pwd.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(1, 0), new Vector2(100, 370), new Vector2(-100, 450));
            bool agree = false;
            var ag = Ui.Btn("agree", content, "☐ 我已阅读并同意用户协议", 24, new Color(0, 0, 0, 0), null);
            ag.onClick.AddListener(() => { agree = !agree; ag.GetComponentInChildren<Text>().text = (agree ? "☑" : "☐") + " 我已阅读并同意用户协议"; });
            Ui.Anchor(ag.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(1, 0), new Vector2(100, 310), new Vector2(-100, 360));
            var err = Err(262);
            Func<bool, Task> go = async guest =>
            {
                if (busy) return;
                string a = acc.text.Trim();
                if (!guest && a == "") { err.text = "请输入账号，或者点“游客试玩”"; return; }
                if (!agree) { err.text = "请先勾选同意用户协议"; return; }
                err.text = "登录中…"; busy = true;
                try
                {
                    var r = await net().Login(new LoginReq { account = guest ? "" : a, password = pwd.text, guest = guest, device = SystemInfo.deviceModel, platform = Application.platform.ToString(), clientVer = Application.version });
                    pwd.text = "";                                       // 密码用完即弃
                    if (!r.Ok) { err.text = r.msg ?? "登录失败"; return; }
                    login = r.data; account = guest ? "游客" : a; PlayerPrefs.SetString("gq_account", guest ? "" : a);
                    server = null; Show(Scr.Server);
                }
                finally { busy = false; }
            };
            var b1 = Big("登录", new Vector2(100, 170), new Vector2(340, 250), true, () => { var _ = go(false); }); b1.name = "login";
            var b2 = Big("游客试玩", new Vector2(370, 170), new Vector2(620, 250), false, () => { var _ = go(true); });
        }

        // ---------------- 选区 ----------------
        List<ServerInfo> Servers(string g)
        {
            var all = login.servers ?? new List<ServerInfo>();
            if (g == "rec") return all.Where(s => s.status == "new" || s.status == "ok").Take(4).ToList();
            if (g == "mine") return all.Where(s => s.roles > 0).ToList();
            if (g == "a") return all.Where(s => s.no <= 6).ToList();
            return all.Where(s => s.no > 6).ToList();
        }

        Button ServerBtn(ServerInfo s, Vector2 min, Vector2 max)
        {
            var b = Ui.Btn("srv_" + s.id, content, "", 28, new Color(0.16f, 0.18f, 0.28f, 0.95f), () => PickServer(s)); b.interactable = s.status != "down";
            Ui.Anchor(b.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(1, 1), min, max);
            var l = b.GetComponentInChildren<Text>(); l.alignment = TextAnchor.MiddleLeft; Ui.Anchor(l.rectTransform, Vector2.zero, Vector2.one, new Vector2(20, 0), new Vector2(-250, 0));
            l.text = "秦 " + s.no + " 区　" + s.name;
            var st = Ui.Label("st", b.transform, StatusName.ContainsKey(s.status) ? StatusName[s.status] : "", 26, StatusColor.ContainsKey(s.status) ? StatusColor[s.status] : Color.white, TextAnchor.MiddleRight);
            Ui.Anchor(st.rectTransform, Vector2.zero, Vector2.one, new Vector2(0, 0), new Vector2(-20, 0));
            if (s.roles > 0) { var n = Ui.Label("n", b.transform, s.roles + " 个角色", 22, new Color(1f, 0.9f, 0.6f), TextAnchor.MiddleRight); Ui.Anchor(n.rectTransform, Vector2.zero, Vector2.one, new Vector2(0, 0), new Vector2(-110, 0)); }
            return b;
        }

        void DrawServers()
        {
            Panel(new Vector2(30, 40), new Vector2(-30, -40));
            var who = Ui.Label("who", content, "账号：" + account, 28, new Color(1f, 0.92f, 0.65f), TextAnchor.MiddleLeft);
            Ui.Anchor(who.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(60, -120), new Vector2(-200, -60));
            var back = Ui.Btn("back", content, "换号", 24, new Color(0.4f, 0.25f, 0.25f), () => Show(Scr.Login));
            Ui.Anchor(back.GetComponent<RectTransform>(), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-190, -116), new Vector2(-60, -64));
            var last = login.servers != null ? login.servers.Find(s => s.id == login.last) : null;
            var lt = Ui.Label("lt", content, "上次登录", 22, new Color(0.7f, 0.75f, 0.9f), TextAnchor.MiddleLeft);
            Ui.Anchor(lt.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(60, -160), new Vector2(-60, -130));
            if (last != null) ServerBtn(last, new Vector2(60, -250), new Vector2(-60, -166));
            else { var e = Ui.Label("e", content, "还没有进过任何区。", 24, new Color(0.6f, 0.62f, 0.72f), TextAnchor.MiddleLeft); Ui.Anchor(e.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(60, -230), new Vector2(-60, -170)); }
            string[][] gs = { new[] { "rec", "推荐" }, new[] { "mine", "我的" }, new[] { "a", "1-6 区" }, new[] { "b", "7-12 区" } };
            for (int i = 0; i < gs.Length; i++)
            {
                string g = gs[i][0];
                var tb = Ui.Btn("tab_" + g, content, gs[i][1], 24, g == group ? new Color(0.55f, 0.4f, 0.15f) : new Color(0.2f, 0.22f, 0.34f), () => { group = g; Show(Scr.Server); });
                Ui.Anchor(tb.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(0, 1), new Vector2(60 + i * 152, -320), new Vector2(60 + i * 152 + 144, -264));
            }
            var list = Servers(group);
            if (list.Count == 0) { var e = Ui.Label("e", content, "这个账号还没有在任何区创建角色。", 24, new Color(0.6f, 0.62f, 0.72f), TextAnchor.MiddleCenter); Ui.Anchor(e.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(60, -480), new Vector2(-60, -400)); }
            for (int i = 0; i < list.Count; i++) ServerBtn(list[i], new Vector2(60, -420 - i * 92), new Vector2(-60, -336 - i * 92));
        }

        async void PickServer(ServerInfo s)
        {
            if (busy) return; busy = true;
            try
            {
                var r = await net().RoleList(s.id);
                if (!r.Ok) { modal.Open("提示", r.msg ?? "取角色列表失败", new ModalUi.Btn("知道了", null)); return; }
                server = s; roles = r.data.roles; picked = 0;
                Show(roles.Count > 0 ? Scr.Roles : Scr.Create);
            }
            finally { busy = false; }
        }

        async void RefreshRoles()
        {
            var r = await net().RoleList(server.id);
            if (r.Ok) roles = r.data.roles;
            picked = Mathf.Clamp(picked, 0, Math.Max(0, roles.Count - 1));
            Show(roles.Count > 0 ? Scr.Roles : Scr.Create);
        }

        // ---------------- 角色列表 ----------------
        void DrawRoles()
        {
            var title = Ui.Label("t", content, "秦 " + server.no + " 区 · " + server.name, 32, new Color(1f, 0.92f, 0.65f), TextAnchor.MiddleCenter);
            Ui.Anchor(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -110), new Vector2(0, -50));
            var r = picked < roles.Count ? roles[picked] : null;
            if (r != null)
            {
                string key = "cls_" + r.cls;
                if (Art.Has(key)) { var art = Ui.Box("art", content, Color.white); art.sprite = Art.One(key, true); art.preserveAspect = true; art.raycastTarget = false; Ui.Anchor(art.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-230, -40), new Vector2(230, 470)); }
                var c = Cls(r.cls);
                var nm = Ui.Label("nm", content, r.name, 44, Color.white, TextAnchor.MiddleCenter); Ui.Anchor(nm.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 560), new Vector2(0, 620));
                var info = Ui.Label("info", content, r.lv + " 级 · " + ClsTitle(r.cls) + " · " + ((string)c["gender"] == "m" ? "男" : "女"), 26, new Color(0.85f, 0.9f, 1f), TextAnchor.MiddleCenter);
                Ui.Anchor(info.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 510), new Vector2(0, 560));
            }
            for (int i = 0; i < MaxRoles; i++)
            {
                int idx = i; var x = i < roles.Count ? roles[i] : null;
                var b = Ui.Btn("slot" + i, content, "", 22, i == picked && x != null ? new Color(0.45f, 0.36f, 0.15f, 0.95f) : new Color(0.14f, 0.15f, 0.24f, 0.92f), () => { if (idx < roles.Count) { picked = idx; Show(Scr.Roles); } else Show(Scr.Create); });
                Ui.Anchor(b.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(0, 0), new Vector2(30 + i * 222, 230), new Vector2(30 + i * 222 + 212, 430));
                var l = b.GetComponentInChildren<Text>(); l.alignment = TextAnchor.LowerCenter; Ui.Anchor(l.rectTransform, Vector2.zero, Vector2.one, new Vector2(4, 8), new Vector2(-4, -4)); l.supportRichText = true;
                if (x != null)
                {
                    l.text = x.name + "\n<size=18>" + x.lv + " 级 " + (string)Cls(x.cls)["name"] + "</size>";
                    var h = Ui.Box("head", b.transform, Color.white); h.sprite = Head(x.cls); h.enabled = h.sprite != null; h.preserveAspect = true; h.raycastTarget = false;
                    Ui.Anchor(h.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-48, -100), new Vector2(48, -8));
                }
                else l.text = "＋\n<size=22>创建角色</size>";
            }
            var enter = Big("进入游戏", new Vector2(30, 120), new Vector2(430, 200), true, () => EnterPicked());
            enter.interactable = r != null; Ui.Anchor(enter.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(0, 0), new Vector2(30, 110), new Vector2(430, 200));
            var del = Big("删除角色", new Vector2(450, 120), new Vector2(570, 200), false, () =>
            {
                if (r == null) return;
                modal.Open("删除角色", "确定要删除「" + r.name + "」吗？\n<color=#ff8a8a>删除后角色和进度无法恢复。</color>", new ModalUi.Btn("删除", () => DeleteRole(r), true), new ModalUi.Btn("再想想", null));
            });
            del.interactable = r != null; Ui.Anchor(del.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(0, 0), new Vector2(450, 110), new Vector2(590, 200));
            var sv = Big("换区", new Vector2(600, 110), new Vector2(690, 200), false, () => { server = null; Show(Scr.Server); });
            Ui.Anchor(sv.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(0, 0), new Vector2(600, 110), new Vector2(690, 200));
            errLabel = Err(40);
        }

        Text errLabel;
        async void EnterPicked()
        {
            if (busy || picked >= roles.Count) return; busy = true;
            try { string e = await enterGame(roles[picked].id); if (e != null) { if (errLabel != null) errLabel.text = e; } }
            finally { busy = false; }
        }

        async void DeleteRole(RoleBrief r)
        {
            var res = await net().RoleDelete(r.id);
            if (!res.Ok) { modal.Open("提示", res.msg ?? "删除失败", new ModalUi.Btn("知道了", null)); return; }
            roles.RemoveAll(x => x.id == r.id); picked = 0; Show(roles.Count > 0 ? Scr.Roles : Scr.Create);
        }

        // ---------------- 创建角色 ----------------
        static string RandomName(string gender) { var rnd = new System.Random(); var b = gender == "m" ? NamesM : NamesF; return NamesA[rnd.Next(NamesA.Length)] + b[rnd.Next(b.Length)]; }

        void DrawCreate()
        {
            var vis = ((JObject)GameData.Raw["classes"]).Properties().Where(p => p.Value["hidden"] == null || !(bool)p.Value["hidden"]).ToList();
            var genders = vis.Select(p => (string)p.Value["gender"]).Distinct().ToList();
            if (!genders.Contains(draftGender)) draftGender = genders[0];
            var ids = vis.Where(p => (string)p.Value["gender"] == draftGender).Select(p => p.Name).ToList();
            if (!ids.Contains(draftCls)) draftCls = ids[0];
            var c = Cls(draftCls);
            var title = Ui.Label("t", content, "创建角色", 34, new Color(1f, 0.92f, 0.65f), TextAnchor.MiddleCenter);
            Ui.Anchor(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -110), new Vector2(0, -50));
            string[][] gs = { new[] { "m", "男" }, new[] { "f", "女" } };
            if (genders.Count > 1)
                for (int i = 0; i < gs.Length; i++)
                {
                    string g = gs[i][0];
                    var tb = Ui.Btn("g_" + g, content, gs[i][1], 26, g == draftGender ? new Color(0.55f, 0.4f, 0.15f) : new Color(0.2f, 0.22f, 0.34f), () => { draftGender = g; Show(Scr.Create); });
                    Ui.Anchor(tb.GetComponent<RectTransform>(), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-150 + i * 160, -180), new Vector2(-10 + i * 160, -124));
                }
            for (int i = 0; i < ids.Count; i++)
            {
                string id = ids[i]; int n = ids.Count; float w = 120f, gap = 10f, x0 = -(n * w + (n - 1) * gap) / 2f;
                var b = Ui.Btn("c_" + id, content, "", 22, id == draftCls ? new Color(0.45f, 0.36f, 0.15f, 0.95f) : new Color(0.14f, 0.15f, 0.24f, 0.92f), () => { draftCls = id; Show(Scr.Create); });
                Ui.Anchor(b.GetComponent<RectTransform>(), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(x0 + i * (w + gap), -330), new Vector2(x0 + i * (w + gap) + w, -196));
                var l = b.GetComponentInChildren<Text>(); l.text = (string)Cls(id)["name"]; l.alignment = TextAnchor.LowerCenter; Ui.Anchor(l.rectTransform, Vector2.zero, Vector2.one, new Vector2(2, 4), new Vector2(-2, -2));
                var h = Ui.Box("head", b.transform, Color.white); h.sprite = Head(id); h.enabled = h.sprite != null; h.preserveAspect = true; h.raycastTarget = false;
                Ui.Anchor(h.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-40, -80), new Vector2(40, -4));
            }
            string key = "cls_" + draftCls;
            if (Art.Has(key)) { var art = Ui.Box("art", content, Color.white); art.sprite = Art.One(key, true); art.preserveAspect = true; art.raycastTarget = false; Ui.Anchor(art.rectTransform, new Vector2(0, 0), new Vector2(0.5f, 1), new Vector2(10, 330), new Vector2(0, -340)); }
            Panel(new Vector2(360, 330), new Vector2(-20, -340)); 
            var nm = Ui.Label("cn", content, ClsTitle(draftCls), 34, new Color(1f, 0.92f, 0.65f), TextAnchor.UpperLeft); Ui.Anchor(nm.rectTransform, new Vector2(0.5f, 1), new Vector2(1, 1), new Vector2(10, -400), new Vector2(-34, -350));
            var role = Ui.Label("cr", content, (string)c["role"], 24, new Color(0.7f, 0.85f, 1f), TextAnchor.UpperLeft); Ui.Anchor(role.rectTransform, new Vector2(0.5f, 1), new Vector2(1, 1), new Vector2(10, -440), new Vector2(-34, -402));
            var desc = Ui.Label("cd", content, (string)c["desc"], 22, Color.white, TextAnchor.UpperLeft); Ui.Anchor(desc.rectTransform, new Vector2(0.5f, 1), new Vector2(1, 1), new Vector2(10, -640), new Vector2(-34, -446));
            int k = 0;
            foreach (var st in ((JObject)c["stats"]).Properties())
            {
                var sl = Ui.Label("sl" + k, content, st.Name, 22, new Color(0.8f, 0.85f, 1f), TextAnchor.MiddleLeft); Ui.Anchor(sl.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(20, -690 - k * 38), new Vector2(100, -660 - k * 38));
                var back = Ui.Box("sb" + k, content, new Color(1, 1, 1, 0.12f)); back.raycastTarget = false; Ui.Anchor(back.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(110, -684 - k * 38), new Vector2(330, -668 - k * 38));
                var fill = Ui.Box("sf" + k, content, new Color(0.95f, 0.75f, 0.3f)); fill.raycastTarget = false; Ui.Anchor(fill.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(110, -684 - k * 38), new Vector2(110 + 44 * (int)st.Value, -668 - k * 38));
                k++;
            }
            var name = Ui.Input("name", content, "给自己取个名字（2～6 个字）", 28, false, 6);
            Ui.Anchor(name.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(0, 0), new Vector2(30, 190), new Vector2(530, 270));
            var dice = Ui.Btn("dice", content, "随机", 26, new Color(0.25f, 0.3f, 0.5f), () => name.text = RandomName(draftGender));
            Ui.Anchor(dice.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(0, 0), new Vector2(545, 190), new Vector2(690, 270));
            var err = Err(135);
            Big("创建并进入", new Vector2(30, 40), new Vector2(430, 120), true, () => { var _ = CreateRole(name.text, err); });
            Big(roles.Count > 0 ? "返回" : "换区", new Vector2(450, 40), new Vector2(690, 120), false, () => { if (roles.Count > 0) Show(Scr.Roles); else { server = null; Show(Scr.Server); } });
        }

        async Task CreateRole(string name, Text err)
        {
            if (busy) return;
            name = (name ?? "").Trim();
            if (name.Length < 2 || name.Length > 6) { err.text = "名字要 2 到 6 个字"; return; }     // 同样的规则服务端也会再判一遍
            busy = true; err.text = "";
            try
            {
                var r = await net().RoleCreate(server.id, name, draftCls);
                if (!r.Ok) { err.text = r.msg ?? "创建失败"; return; }
                if (!r.data.ok) { err.text = r.data.msg; return; }
                roles.Add(r.data.role); picked = roles.Count - 1;
                string e = await enterGame(r.data.role.id);
                if (e != null) { Show(Scr.Roles); }
            }
            finally { busy = false; }
        }
    }
}
