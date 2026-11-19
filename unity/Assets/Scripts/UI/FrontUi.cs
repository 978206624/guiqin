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
    // 账号密码、区服列表、角色增删、名字校验全部走后端；客户端在 Windows 通过系统凭据管理器记住密码。
    public class FrontUi
    {
        enum Scr { Login, Server, Roles, Create }

        readonly GameObject root; readonly Image curtain; readonly RectTransform content; readonly Func<ModalUi> getModal; ModalUi modal { get { return getModal(); } }
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
                const float stageRatio = 720f / 1280f;
                float textureRatio = tex.width / (float)tex.height;
                float w = Mathf.Min(1, stageRatio / textureRatio);
                float h = Mathf.Min(1, textureRatio / stageRatio);
                raw.uvRect = new Rect((1 - w) * 0.5f, (1 - h) * 0.5f, w, h);
                Ui.Anchor(raw.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            }
            var shade = WebUi.Gradient("shade", bg.transform, WebUi.C("#0a061c26"), WebUi.C("#0a061ce0"));
            Ui.Anchor(shade.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            curtain = Ui.Box("curtain", bg.transform, WebUi.C("#0a061cb8"));
            curtain.raycastTarget = false;
            Ui.Anchor(curtain.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
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
        Text Label(string id, string value, int size, Color color, int x, int y, int w, int h,
            TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var text = Ui.Label(id, content, value, size, color, anchor);
            WebUi.At(text.rectTransform, x, y, w, h);
            return text;
        }

        Button ButtonAt(string id, string value, int size, int x, int y, int w, int h,
            UnityEngine.Events.UnityAction action, Color tone)
        {
            var button = Ui.Btn(id, content, value, size, tone, action);
            WebUi.At((RectTransform)button.transform, x, y, w, h);
            return button;
        }

        void Header(string title, string back, UnityEngine.Events.UnityAction onBack)
        {
            WebUi.Title(Label("title", title, 44, WebUi.Amber, 0, 30, 720, 60));
            ButtonAt("back", back, 19, 20, 38, 100, 44, onBack, WebUi.Purple);
        }

        void Portrait(string key, int top, int height, int pedestalTop)
        {
            var glow = Ui.Box("pedestal", content, WebUi.C("#ffce7085"));
            glow.raycastTarget = false;
            glow.sprite = Sprite.Create(Art.SoftTex(), new Rect(0, 0, 64, 64), Vector2.one * 0.5f);
            WebUi.At(glow.rectTransform, 150, pedestalTop, 420, 110);
            var image = WebUi.Icon("art", content, "cls_" + key);
            WebUi.At(image.rectTransform, 0, top, 720, height);
        }

        static void HeadIn(Transform parent, Sprite sprite, int x, int y, int size)
        {
            var border = Ui.Box("border", parent, WebUi.Gold);
            border.sprite = WebUi.Circle(); border.raycastTarget = false;
            WebUi.At(border.rectTransform, x, y, size, size);
            var mask = WebUi.RoundMask("clip", parent);
            WebUi.At(mask.rectTransform, x + 2, y + 2, size - 4, size - 4);
            var picture = Ui.Box("head", mask.transform, Color.white);
            picture.sprite = sprite; picture.enabled = sprite != null; picture.raycastTarget = false;
            Ui.Anchor(picture.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
        }

        void Show(Scr s)
        {
            Clear();
            curtain.enabled = s != Scr.Login;
            switch (s) { case Scr.Login: DrawLogin(); break; case Scr.Server: DrawServers(); break; case Scr.Roles: DrawRoles(); break; case Scr.Create: DrawCreate(); break; }
        }

        // ---------------- 登录 ----------------
        void DrawLogin()
        {
            var t = WebUi.Title(Label("logo", "归秦", 190, WebUi.Amber, 0, 139, 720, 222));
            t.color = WebUi.Light;
            WebUi.Edge(t);
            var sub = Label("slogan", "大 秦 风 云　侠 义 江 湖", 24, WebUi.Light, 0, 370, 720, 42);
            WebUi.Edge(sub);
            string endpoint = (net() as WsBackend)?.Endpoint;
            bool canRemember = WindowsLoginCredential.Supported && !string.IsNullOrEmpty(endpoint);
            int shift = canRemember ? 0 : 32;
            var panel = WebUi.Panel("form", content);
            WebUi.At(panel.rectTransform, 90, 716, 540, 424 - shift);
            panel.color = new Color(1, 1, 1, 0.92f);
            Label("accountLabel", "账号", 17, WebUi.Light, 120, 738, 480, 24, TextAnchor.MiddleLeft);
            var acc = Ui.Input("account", content, "请输入账号", 22, false, 24);
            acc.text = PlayerPrefs.GetString("gq_account", "");
            WebUi.At((RectTransform)acc.transform, 120, 766, 480, 54);
            Label("passwordLabel", "密码", 17, WebUi.Light, 120, 833, 480, 24, TextAnchor.MiddleLeft);
            var pwd = Ui.Input("password", content, "请输入密码", 22, true, 32);
            WebUi.At((RectTransform)pwd.transform, 120, 860, 480, 54);
            bool remember = canRemember && PlayerPrefs.GetInt("gq_remember_login", 1) != 0;
            if (remember && WindowsLoginCredential.TryRead(endpoint, out var savedAccount, out var savedPassword))
            {
                acc.text = savedAccount;
                pwd.text = savedPassword;
            }
            var err = Label("err", "", 17, WebUi.C("#ff9a8a"), 120, 956 - shift, 480, 30);
            if (canRemember)
            {
                const string rememberLabel = " 记住密码";
                var button = ButtonAt("remember", (remember ? "☑" : "☐") + rememberLabel, 16, 440, 924, 160, 28, null, Color.clear);
                var label = button.GetComponentInChildren<Text>();
                label.alignment = TextAnchor.MiddleRight; label.color = WebUi.Muted;
                button.onClick.AddListener(() =>
                {
                    if (busy) return;
                    remember = !remember;
                    label.text = (remember ? "☑" : "☐") + rememberLabel;
                    PlayerPrefs.SetInt("gq_remember_login", remember ? 1 : 0);
                    PlayerPrefs.Save();
                    if (!remember)
                    {
                        pwd.text = "";
                        if (!WindowsLoginCredential.Clear(endpoint)) err.text = "未能删除系统凭据，请重试";
                    }
                });
            }
            Func<bool, Task> go = async register =>
            {
                if (busy) return;
                string a = acc.text.Trim();
                if (a == "" || string.IsNullOrEmpty(pwd.text)) { err.text = "请输入账号和密码"; return; }
                err.text = register ? "注册中…" : "登录中…"; busy = true;
                try
                {
                    string password = pwd.text;
                    var r = await net().Login(new LoginReq { account = a, password = password, register = register, device = SystemInfo.deviceModel, platform = Application.platform.ToString(), clientVer = Application.version });
                    pwd.text = "";
                    if (!r.Ok) { err.text = r.msg ?? "登录失败"; return; }
                    if (remember && !WindowsLoginCredential.Save(endpoint, a, password))
                    {
                        modal.Open("记住密码", "登录成功，但 Windows 未能保存凭据。", new ModalUi.Btn("知道了", null));
                    }
                    login = r.data; account = a; PlayerPrefs.SetString("gq_account", a);
                    PlayerPrefs.Save();
                    server = null; Show(Scr.Server);
                }
                finally { busy = false; }
            };
            var enter = ButtonAt("login", "登 录", 36, 120, 992 - shift, 480, 68, () => { var _ = go(false); }, WebUi.Red);
            WebUi.Title(enter.GetComponentInChildren<Text>());
            var registerButton = ButtonAt("register", "注册账号", 19, 120, 1073 - shift, 480, 44, () => { var _ = go(true); }, Color.clear);
            registerButton.GetComponentInChildren<Text>().color = WebUi.Light;
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

        Button ServerBtn(ServerInfo server, int x, int y, int width)
        {
            var b = ButtonAt("srv_" + server.id, "", 21, x, y, width, 88, () => PickServer(server), WebUi.Purple);
            b.interactable = server.status != "down";
            var color = StatusColor.TryGetValue(server.status, out var statusColor) ? statusColor : WebUi.Muted;
            var dot = Ui.Box("statusDot", b.transform, color); dot.sprite = Art.Dot(); dot.raycastTarget = false;
            WebUi.At(dot.rectTransform, 16, 37, 13, 13);
            var name = b.GetComponentInChildren<Text>(); name.alignment = TextAnchor.MiddleLeft;
            name.text = "秦 " + server.no + " 区 " + server.name;
            WebUi.At(name.rectTransform, 38, 11, width - 100, 66);
            var status = Ui.Label("status", b.transform, StatusName.TryGetValue(server.status, out var label) ? label : "",
                14, color, TextAnchor.MiddleRight);
            WebUi.At(status.rectTransform, width - 62, 10, 48, 20);
            if (server.roles > 0)
            {
                var count = Ui.Label("count", b.transform, server.roles + " 个角色", 14, WebUi.Amber, TextAnchor.MiddleRight);
                WebUi.At(count.rectTransform, width - 90, 57, 76, 20);
            }
            return b;
        }

        void DrawServers()
        {
            Header("选择区服", "‹ 返回", () => Show(Scr.Login));
            Label("account", "账号 " + account, 16, WebUi.Muted, 520, 42, 176, 30, TextAnchor.MiddleRight);
            Label("lastLabel", "上次登录", 16, WebUi.Muted, 30, 124, 660, 24, TextAnchor.MiddleLeft);
            var last = login.servers?.Find(s => s.id == login.last);
            if (last != null) ServerBtn(last, 30, 154, 660);
            else Label("lastEmpty", "还没有进过任何区。", 18, WebUi.Muted, 30, 154, 660, 52, TextAnchor.MiddleLeft);

            string[][] groups = { new[] { "rec", "推荐" }, new[] { "mine", "我的" }, new[] { "a", "1-6 区" }, new[] { "b", "7-12 区" } };
            for (int i = 0; i < groups.Length; i++)
            {
                string id = groups[i][0];
                ButtonAt("tab_" + id, groups[i][1], 20, 30 + i * 168, 286, 156, 52,
                    () => { group = id; Show(Scr.Server); }, group == id ? WebUi.C("#8a5a14") : WebUi.Purple);
            }
            var list = Servers(group);
            if (list.Count == 0)
                Label("empty", "这个账号还没有在任何区创建角色。", 18, WebUi.Muted, 30, 360, 660, 80);
            for (int i = 0; i < list.Count; i++)
                ServerBtn(list[i], 30 + i % 2 * 337, 360 + i / 2 * 102, 323);
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
            Header("秦 " + server.no + " 区 · " + server.name, "‹ 换区", () => { server = null; Show(Scr.Server); });
            var role = picked < roles.Count ? roles[picked] : null;
            if (role != null)
            {
                Portrait(role.cls, 200, 640, 790);
                var name = WebUi.Title(Label("name", role.name, 46, WebUi.Light, 0, 852, 720, 54));
                name.color = WebUi.Light;
                string gender = (string)Cls(role.cls)?["gender"] == "m" ? "男" : "女";
                Label("info", role.lv + " 级 · " + ClsTitle(role.cls) + " · " + gender, 19, WebUi.Light, 0, 905, 720, 32);
            }
            for (int i = 0; i < MaxRoles; i++)
            {
                int index = i;
                var slot = i < roles.Count ? roles[i] : null;
                var button = ButtonAt("slot" + i, "", 20, 30 + i * 225, 958, 210, 156,
                    () => { if (index < roles.Count) { picked = index; Show(Scr.Roles); } else Show(Scr.Create); },
                    i == picked && slot != null ? WebUi.C("#8a5a14") : WebUi.Purple);
                var caption = button.GetComponentInChildren<Text>();
                if (slot == null)
                {
                    caption.text = "<size=43>＋</size>\n创建角色\n<size=14>空栏位</size>";
                    continue;
                }
                HeadIn(button.transform, Head(slot.cls), 68, 10, 74);
                WebUi.At(caption.rectTransform, 0, 88, 210, 56);
                caption.text = slot.name + "\n<size=14>" + slot.lv + " 级 " + ClsTitle(slot.cls) + "</size>";
            }
            ButtonAt("enter", "进入游戏", 36, 150, 1140, 420, 68, EnterPicked, WebUi.Red).interactable = role != null;
            ButtonAt("delete", "删除角色", 19, 585, 38, 115, 44, () =>
            {
                if (role == null) return;
                modal.Open("删除角色", "确定要删除「" + role.name + "」吗？\n<color=#ff8a8a>删除后角色和进度无法恢复。</color>",
                    new ModalUi.Btn("删除", () => DeleteRole(role), true), new ModalUi.Btn("再想想", null));
            }, WebUi.Red).interactable = role != null;
            errLabel = Label("err", "", 17, WebUi.C("#ff9a8a"), 30, 1210, 660, 38);
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
            var visible = ((JObject)GameData.Raw["classes"]).Properties().Where(p => !((bool?)p.Value["hidden"] ?? false)).ToList();
            var genders = visible.Select(p => (string)p.Value["gender"]).Distinct().ToList();
            if (!genders.Contains(draftGender)) draftGender = genders[0];
            var ids = visible.Where(p => (string)p.Value["gender"] == draftGender).Select(p => p.Name).ToList();
            if (!ids.Contains(draftCls)) draftCls = ids[0];
            var data = Cls(draftCls);

            Header("创建角色", roles.Count > 0 ? "‹ 返回" : "换区",
                () => { if (roles.Count > 0) Show(Scr.Roles); else { server = null; Show(Scr.Server); } });
            if (genders.Count > 1)
            {
                ButtonAt("male", "男", 20, 210, 108, 145, 52, () => { draftGender = "m"; Show(Scr.Create); },
                    draftGender == "m" ? WebUi.C("#8a5a14") : WebUi.Purple);
                ButtonAt("female", "女", 20, 365, 108, 145, 52, () => { draftGender = "f"; Show(Scr.Create); },
                    draftGender == "f" ? WebUi.C("#8a5a14") : WebUi.Purple);
            }
            Portrait(draftCls, 176, 560, 690);
            for (int i = 0; i < ids.Count; i++)
            {
                string id = ids[i];
                var button = ButtonAt("class_" + id, ClsTitle(id), 23, 30 + i * 225, 752, 210, 96,
                    () => { draftCls = id; Show(Scr.Create); }, id == draftCls ? WebUi.C("#8a5a14") : WebUi.Purple);
                HeadIn(button.transform, Head(id), 15, 16, 64);
                WebUi.At(button.GetComponentInChildren<Text>().rectTransform, 84, 0, 118, 96);
            }

            var panel = WebUi.Panel("classInfo", content);
            WebUi.At(panel.rectTransform, 30, 866, 660, 270);
            WebUi.Title(Label("className", ClsTitle(draftCls), 34, WebUi.Amber, 50, 880, 178, 42, TextAnchor.MiddleLeft));
            Label("classRole", (string)data["role"], 16, WebUi.Sky, 230, 888, 410, 33, TextAnchor.MiddleLeft);
            Label("desc", (string)data["desc"], 18, WebUi.Text, 50, 926, 620, 66, TextAnchor.UpperLeft);
            int k = 0;
            foreach (var stat in ((JObject)data["stats"]).Properties())
            {
                int x = 50 + k % 2 * 320, y = 1000 + k / 2 * 32;
                Label("stat_" + k, stat.Name, 16, WebUi.Light, x, y, 45, 25, TextAnchor.MiddleLeft);
                var back = WebUi.Gradient("statBack", content, WebUi.C("#1a0f14"), WebUi.C("#1a0f14"), 4);
                WebUi.At(back.rectTransform, x + 55, y + 7, 230, 10);
                var fill = WebUi.Gradient("statFill", content, WebUi.Light, WebUi.Gold, 4);
                WebUi.At(fill.rectTransform, x + 55, y + 7, 46 * Mathf.Clamp((int)stat.Value, 0, 5), 10);
                k++;
            }

            var name = Ui.Input("name", content, "角色名（2–6 字）", 22, false, 6);
            WebUi.At((RectTransform)name.transform, 30, 1156, 390, 54);
            ButtonAt("dice", "随机", 20, 432, 1156, 112, 54, () => name.text = RandomName(draftGender), WebUi.Purple);
            var err = Label("err", "", 17, WebUi.C("#ff9a8a"), 30, 1214, 660, 32);
            ButtonAt("create", "创建", 28, 556, 1156, 134, 54, () => { var _ = CreateRole(name.text, err); }, WebUi.Red);
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
                Show(Scr.Roles);
            }
            finally { busy = false; }
        }
    }
}
