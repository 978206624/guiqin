using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using GuiQin.Net;
using GuiQin.Chat;
using System.Linq;
using GuiQin.Battle;
using GuiQin.Quest;

namespace GuiQin
{
    // 主控：相机 / 界面 / 地图装载 / 点击行走 / 对话 / 传送。对应网页版 game.js 的“地图与行走”整块。
    public class GameController : MonoBehaviour
    {
        const float Speed = 230f;

        Camera cam; Transform worldRoot; Canvas tagCanvas, uiCanvas; RectTransform uiStage;
        MapRuntime map; GameObject mapRoot;
        PlayerActor player; PetActor fox;
        readonly List<Actor> list = new List<Actor>();
        List<Vector2> route = new List<Vector2>();
        Actor talkTarget;
        DialogUi dialog; ToastUi toast; HudUi hud; SettingsUi menu; FrontUi front;
        WeatherSystem wx; WeatherUi wxUi; WorldMapUi worldUi; SceneMapUi sceneUi; PetUi petUi; TrainUi trainUi; ChatUi chatUi; BagUi bagUi; QuestUi questUi; ModalUi modal; GameObject netBanner; Text netText; readonly HashSet<int> preSeen = new HashSet<int>();
        MapFx mapFx; WaterFx waterFx;
        readonly List<Vector2[]> fadePolys = new List<Vector2[]>();
        IGameBackend net; RoleState role; bool entered;
        BattleController battle;
        SpriteRenderer mark; float markT = 99f;
        bool holding, downOnUi; float holdAt; Vector2 lastHoldPos;
        float time;

        void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.orientation = ScreenOrientation.Portrait;
            GameData.Load(new UnityRulesDataSource());
            BuildCamera(); BuildEventSystem();
            wx = new WeatherSystem(cam);
            BuildUi();
            worldRoot = new GameObject("World").transform;
            CreatePlayerAndPet();
        }

        // 进游戏前先走登录 → 选区 → 角色界面（FrontUi）；进入角色后状态都来自后端，客户端不存档。
        void Start()
        {
            NewNet();
            Settings.Changed += ApplySettings;
            ShowWorld(false); front.Open(false);
        }

        void NewNet()
        {
            if (net != null) net.Close();
            net = BackendFactory.Create();
            net.OnPush += OnPush; net.OnDisconnected += OnLost;
            net.OnReconnecting += n => { netBanner.transform.SetAsLastSibling(); netBanner.SetActive(true); netText.text = n == 0 ? "网络断开，正在重连…" : "网络断开，正在重连…（第 " + n + " 次）"; };
            net.OnReconnected += () => { netBanner.SetActive(false); toast.Show("已重新连接"); Resync(); };
        }

        // 连接彻底断了（重连用尽 / 会话失效 / 被顶号）：丢掉当前场景，提示后回登录界面
        void OnLost(string why)
        {
            netBanner.SetActive(false); entered = false;
            if (battle != null) { battle.Dispose(); battle = null; }
            ClearMap(); map = null; menu.Close(); wxUi.Close(); worldUi.Close(); sceneUi.Close(); petUi.Close(); trainUi.Close(); chatUi.BindRole(null); bagUi.Close(); dialog.Close();
            ShowWorld(false);
            modal.Open("连接已断开", string.IsNullOrEmpty(why) ? "和服务器的连接断开了。" : why, new ModalUi.Btn("返回登录", () => { NewNet(); front.Open(false); }));
        }

        // 重连成功后对一次状态：重新取角色（服务端是准的），地图变了就重载。战斗中的请求靠“原 seq 补发”自己接上，不在这里重建战场。
        async void Resync()
        {
            if (!entered || role == null) return;
            string currentRole = role.id;
            var backend = net;
            var er = await backend.EnterGame(currentRole);
            if (!entered || role?.id != currentRole || net != backend) return;
            if (!er.Ok) { toast.Show("同步失败：" + er.msg); return; }
            role = er.data.role; ApplyRoleLook(); ApplyPetLook();
            if (battle == null && map != null && role.map != map.Id) LoadMap(role.map, StageAt(role.map, role.x, role.y));
            await chatUi.LoadHistory();
            if (entered && role?.id == currentRole && net == backend) await RefreshBag();
        }

        void ApplySettings()
        {
            if (waterFx != null) waterFx.SetVisible(Settings.Water);
            if (mapFx != null) mapFx.ApplyButterflies();
            wx.ApplyFx();
        }

        // 角色界面点“进入游戏”：返回 null 表示成功，否则是要显示给玩家的错误文字
        async System.Threading.Tasks.Task<string> EnterRole(string roleId)
        {
            var backend = net;
            var er = await backend.EnterGame(roleId);
            if (net != backend || !backend.Connected) return "连接已断开";
            if (!er.Ok) return "进入游戏失败：" + er.msg;
            role = er.data.role;
            ApplyRoleLook(); ApplyPetLook();
            MapDef d; if (!GameData.Maps.TryGetValue(role.map, out d)) role.map = "village";
            chatUi.BindRole(role.id);
            LoadMap(role.map, StageAt(role.map, role.x, role.y));
            await chatUi.LoadHistory();
            if (net != backend || map == null || role?.id != roleId) return "连接已断开";
            entered = true; front.Hide(); ShowWorld(true);
            var br = await backend.BattleResume();                      // 上次退出 / 掉线时战斗还没打完：服务端给快照，接着打
            if (entered && net == backend && role?.id == roleId && br.Ok && br.data != null && br.data.ok) OpenBattle(br.data.battle, null);
            return null;
        }

        // 游戏里“切换角色”：退出当前场景，回到角色列表（战斗中不允许）
        void SwitchRole()
        {
            if (battle != null) { toast.Show("战斗中不能切换角色"); return; }
            menu.Close(); Stop(); entered = false; chatUi.BindRole(null); ClearMap(); map = null;
            ShowWorld(false); front.Open(true);
        }

        void ResetRole()
        {
            menu.Close();
            modal.Open("重新开始", "当前角色的等级、装备、任务进度会全部清空，回到新手状态。\n<size=22>（仅本地演示后端可用）</size>", new ModalUi.Btn("重新开始", () =>
            {
                var m = net as MockBackend;
                if (m == null) { toast.Show("只有本地演示后端能重置"); return; }
                m.ResetRole(); SwitchRole();
            }, true), new ModalUi.Btn("再想想", null));
        }

        // 数据坐标（服务端存的）→ 舞台像素；x<0 表示出生点
        static Vector2? StageAt(string mapId, float x, float y)
        {
            MapDef d;
            if (x < 0f || !GameData.Maps.TryGetValue(mapId, out d)) return null;
            return new Vector2(x * d.scale * d.k, y * d.scale * d.k);
        }

        void OnPush(string cmd, Newtonsoft.Json.Linq.JToken data)
        {
            if (cmd == ChatCmd.Push) { if (data != null) chatUi.Add(data.ToObject<ChatMsg>()); return; }
            if (cmd == Cmd.PushParty) { if (data != null) chatUi.ApplyParty(data.ToObject<PartyState>()); return; }
            if (cmd == Cmd.PushKick) OnLost("你已被下线：" + (data != null && data["reason"] != null ? (string)data["reason"] : "账号在别处登录"));
            // push.map / push.weather / push.role：多人同屏、服务端天气、属性变化，后续阶段接
        }

        // ---------------- 搭建 ----------------
        void BuildCamera()
        {
            cam = Camera.main;
            if (cam == null) { var go = new GameObject("Main Camera"); go.tag = "MainCamera"; cam = go.AddComponent<Camera>(); go.AddComponent<AudioListener>(); }
            cam.orthographic = true; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = Color.black;
            cam.nearClipPlane = 0.1f; cam.farClipPlane = 50f;
            cam.transform.position = new Vector3(0, 0, -10f); cam.transform.rotation = Quaternion.identity;
        }

        void BuildEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;
            var go = new GameObject("EventSystem"); go.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            go.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            go.AddComponent<StandaloneInputModule>();
#endif
        }

        void BuildUi()
        {
            tagCanvas = Ui.MakeCanvas("Tags", 0, transform);
            tagCanvas.GetComponent<UnityEngine.UI.GraphicRaycaster>().enabled = false;
            uiCanvas = Ui.MakeCanvas("UI", 10, transform);
            uiStage = Ui.Rect("PortraitStage", uiCanvas.transform);
            var aspect = uiStage.gameObject.AddComponent<UnityEngine.UI.AspectRatioFitter>();
            aspect.aspectMode = UnityEngine.UI.AspectRatioFitter.AspectMode.FitInParent;
            aspect.aspectRatio = 720f / 1280f;
            hud = new HudUi(uiStage, ToggleSettings, OpenWeather, OpenWorld, OpenBag,
                OpenRole, OpenPet, OpenSceneMap, OnHudKey);
            questUi = new QuestUi(uiStage, QuestGo);
            front = new FrontUi(uiStage, () => modal, () => net, EnterRole);
            modal = new ModalUi(uiStage);
            dialog = new DialogUi(uiStage);
            toast = new ToastUi(uiStage);
            var nb = Ui.Box("netBanner", uiStage, new Color(0.05f, 0.05f, 0.1f, 0.82f)); netBanner = nb.gameObject;       // 掉线遮罩：挡住点击，只显示一行字
            Ui.Anchor(nb.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            netText = Ui.Label("t", nb.transform, "", 34, new Color(1f, 0.92f, 0.65f), TextAnchor.MiddleCenter);
            Ui.Anchor(netText.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero); netBanner.SetActive(false);
            menu = new SettingsUi(uiStage, SwitchRole, ResetRole);
            wxUi = new WeatherUi(uiStage, wx, () => map != null ? map.Def.name : "");
            worldUi = new WorldMapUi(uiStage, id => GoWorld(id), () => map != null ? map.Id : "village");
            sceneUi = new SceneMapUi(uiStage, OpenWorld);
            petUi = new PetUi(uiStage, () => role, PetRun, m => toast.Show(m));
            trainUi = new TrainUi(uiStage, () => role, TrainRun, m => toast.Show(m));
            chatUi = new ChatUi(uiStage, () => net, () => role, hud.SetChatPreview);
            bagUi = new BagUi(uiStage, (c, q) => net.BagOp(c, q), () => role, m => toast.Show(m), RefreshHud);
            wx.Changed += (k, announce) => { hud.SetWeather(WeatherSystem.NameOf(k)); if (announce) toast.Show("天气转为 " + WeatherSystem.NameOf(k)); };
            var mk = new GameObject("clickMark"); mk.transform.SetParent(transform, false);
            mark = mk.AddComponent<SpriteRenderer>(); mark.sprite = Art.Dot(); mark.color = new Color(1f, 0.9f, 0.4f, 0f); mark.sortingOrder = 30000;
        }

        void CreatePlayerAndPet()
        {
            player = Actor.Make<PlayerActor>("player", worldRoot, null, Vector2.zero, 50f);
            player.Kind = ActorKind.Player; player.Id = "player"; player.DisplayName = ""; player.Title = "";
            player.NameColor = new Color(0.54f, 1f, 0.69f); player.Hop = 7f;
            player.Tag = new TagLabel(tagCanvas.transform); player.SetTag();

            var sz = Art.Size("fox");
            fox = Actor.Make<PetActor>("fox", worldRoot, Art.One("fox"), Vector2.zero, sz != null ? sz[0] * 0.8f : 40f);
            fox.Kind = ActorKind.Pet; fox.Id = "fox"; fox.DisplayName = "玄晶天狐"; fox.Hop = 5f;
            fox.NameColor = new Color(0.66f, 0.9f, 1f);
            fox.SpriteF = Art.One("fox"); fox.SpriteB = Art.One("b_fox");
            fox.H = sz != null ? sz[1] : 50f;
            var szb = Art.Size("b_fox"); if (szb != null) fox.BackScale = (fox.H + 4f) / szb[1];
            fox.Tag = new TagLabel(tagCanvas.transform); fox.SetTag();
        }

        // ---------------- 载入地图 ----------------
        bool ModalOpen { get { return menu.Visible || wxUi.Visible || worldUi.Visible || sceneUi.Visible || petUi.Visible || trainUi.Visible || chatUi.Visible || bagUi.Visible || modal.Visible; } }

        void OpenWeather() { if (!wxUi.Visible) { menu.Close(); worldUi.Close(); sceneUi.Close(); bagUi.Close(); wxUi.Open(); } else wxUi.Close(); }
        void OpenWorld() { Stop(); menu.Close(); wxUi.Close(); bagUi.Close(); sceneUi.Close(); worldUi.Open(); }

        void ToggleSettings()
        {
            wxUi.Close(); worldUi.Close(); sceneUi.Close(); bagUi.Close();
            menu.Toggle();
        }

        void OpenSceneMap()
        {
            if (map == null || battle != null) return;
            Stop(); menu.Close(); wxUi.Close(); worldUi.Close(); bagUi.Close();
            sceneUi.Open(map, player.Pos, list, GoTalk, p => MoveTo(p, false));
        }

        void RefreshHud()
        {
            if (role == null) return;
            hud.SetGiftDot(role.giftDay != ServerToday());
            hud.SetRole(role, GuiQin.Inventory.RoleStats.Compute(role.cls, role.lv, role.bag));
        }

        static string ServerToday() { return DateTime.UtcNow.AddHours(8).ToString("yyyy-MM-dd"); }       // 每日福利按北京时间换日，和服务端一致

        // ---------------- HUD 快捷入口 ----------------
        bool wander; float wanderWait;

        void OnHudKey(string key)
        {
            if (!entered || battle != null) return;
            switch (key)
            {
                case "act": toast.Show("中秋灯会将在戌时三刻开始"); break;
                case "gift": DailyGift(); break;
                case "mall": OpenMall(); break;
                case "rank": modal.Open("竞技场", "五对五的实时对战，需要对战服务端支持，现在还没有开放。", new ModalUi.Btn("知道了", null)); break;
                case "guild": OpenChat("guild"); break;
                case "friend": OpenChat("friends"); break;
                case "chat": OpenChat("all"); break;
                case "auto": SetWander(!wander); toast.Show(wander ? "开始挂机闲逛，再点一次停止" : "停止挂机"); break;
                case "ride": toast.Show("「坐骑」暂未开放"); break;
                case "potion": QuickPotion("hp_potion"); break;
                case "mpPotion": QuickPotion("mp_potion"); break;
            }
        }

        void OpenChat(string tab) { Stop(); menu.Close(); wxUi.Close(); worldUi.Close(); sceneUi.Close(); bagUi.Close(); chatUi.Open(tab); }

        void SetWander(bool on) { bool was = wander; wander = on; wanderWait = 0f; hud.SetAuto(on); if (!on && was && player != null) Stop(); }

        void StepWander(float dt)
        {
            if (!wander) return;
            if (ModalOpen || dialog.Visible) { SetWander(false); return; }
            if (player.Moving) return;
            wanderWait -= dt;
            if (wanderWait > 0f) return;
            wanderWait = 0.6f + UnityEngine.Random.value * 0.8f;
            var p = player.Pos + new Vector2(UnityEngine.Random.Range(-320f, 320f), UnityEngine.Random.Range(-260f, 260f));
            if (map.Inside(p)) MoveTo(p, true);
        }

        async void DailyGift()
        {
            var r = await net.BagOp(GuiQin.Inventory.BagCmd.Gift, null);
            if (!r.Ok) { toast.Show(r.msg); return; }
            role.bag = r.data.bag; role.coin = r.data.coin;
            if (r.data.ok) role.giftDay = ServerToday();
            RefreshHud();
            if (!string.IsNullOrEmpty(r.data.msg)) toast.Show(r.data.msg);
        }

        async void QuickPotion(string id)
        {
            int n = 0; if (role != null && role.bag != null) role.bag.items.TryGetValue(id, out n);
            if (n <= 0) { toast.Show("没有" + (string)GameData.Raw["items"][id]["name"] + "了，找货郎买点吧"); return; }
            var r = await net.BagOp(GuiQin.Inventory.BagCmd.Use, new GuiQin.Inventory.BagOpReq { id = id });
            if (!r.Ok) { toast.Show(r.msg); return; }
            role.bag = r.data.bag; role.coin = r.data.coin; role.hp = r.data.hp; role.mp = r.data.mp; RefreshHud();
            if (!string.IsNullOrEmpty(r.data.msg)) toast.Show(r.data.msg);
        }

        void OpenRole()
        {
            if (role == null || ModalOpen) return;
            var s = GuiQin.Inventory.RoleStats.Compute(role.cls, role.lv, role.bag);
            modal.OpenWithPortrait(role.name, "cls_" + role.cls,
                $"Lv.{role.lv} · {GuiQin.Sect.SectService.ClsName(role.cls)}\n战力 {HudUi.Power(s):N0}\n\n气血 {s.maxHp}　法力 {s.maxMp}\n物攻 {s.atk}　物防 {s.def}\n法攻 {s.matk}　法防 {s.mdef}\n速度 {s.spd}　暴击 {s.crit:0.#}%",
                new ModalUi.Btn("装备", OpenEquipment), new ModalUi.Btn("关闭", null));
        }

        async void OpenEquipment()
        {
            Stop(); await RefreshBag();
            if (entered) bagUi.Open(BagUi.Tab.Equip, fromRole: true);
        }

        void OpenPet()
        {
            if (role == null || ModalOpen) return;
            Stop(); menu.Close(); wxUi.Close(); worldUi.Close(); sceneUi.Close(); bagUi.Close();
            petUi.Open();
        }

        // 宠物面板里的动作都发给后端，结果整包合进角色，再刷新外观 / HUD
        async System.Threading.Tasks.Task<string> PetRun(string cmd, string id)
        {
            var r = await net.PetOp(cmd, id);
            if (!r.Ok) return r.msg;
            var d = r.data;
            role.petOwn = d.petOwn; role.petAct = d.petAct; role.bond = d.bond; role.ssGift = d.ssGift; role.coin = d.coin; if (d.bag != null) role.bag = d.bag;
            ApplyPetLook(); RefreshHud();
            return d.msg;
        }

        // 技能修炼：找门主 / 堂主。动作发给后端，结果（技能等级、银两）合进角色
        async System.Threading.Tasks.Task<string> TrainRun(string who, string id)
        {
            var r = await net.Train(who, id);
            if (!r.Ok) return r.msg;
            role.skl = r.data.skl; role.coin = r.data.coin; RefreshHud();
            return r.data.msg;
        }

        void OpenTrain(string who) { Stop(); menu.Close(); wxUi.Close(); worldUi.Close(); sceneUi.Close(); bagUi.Close(); trainUi.Open(who); }

        async void OpenEnvoy()
        {
            Stop(); menu.Close(); wxUi.Close(); worldUi.Close(); sceneUi.Close(); bagUi.Close();
            await RefreshBag();
            if (entered) petUi.OpenEnvoy();
        }

        // 跟随主角的宠物换成出战宠物的形象：正面用立绘（pd_ 开头，没有就用战斗图），背影用战斗图；身高按图鉴 h 的 0.8 倍
        void ApplyPetLook()
        {
            if (role == null || fox == null) return;
            string id = GuiQin.Pets.PetService.ActiveId(role); var dx = GuiQin.Pets.PetService.Dex(id);
            string back = (string)dx["sprite"], front = dx["show"] != null && Art.Has((string)dx["show"]) ? (string)dx["show"] : back;
            var szF = Art.Size(front); var szB = Art.Size(back);
            float h = (dx["h"] != null ? (float)dx["h"] : 62f) * 0.8f;
            fox.SpriteF = Art.One(front); fox.SpriteB = Art.One(back); fox.H = h;
            fox.FrontScale = szF != null ? h / szF[1] : 1f; fox.BackScale = szB != null ? h / szB[1] : 1f;
            if (szF != null) fox.Shadow.transform.localScale = new Vector3(szF[0] * fox.FrontScale * 0.7f / Art.PX, szF[0] * fox.FrontScale * 0.21f / Art.PX, 1f);
            fox.DisplayName = (string)dx["name"]; fox.SetTag();
            hud.SetPet("ph_" + (back.StartsWith("b_") ? back.Substring(2) : back), front);
            if (player != null && map != null) { var fp = new Vector2(player.Pos.x + (h > 70f ? 96f : 46f), player.Pos.y + 18f); if (map.Inside(fp)) fox.Pos = fp; }
        }

        // 背包 / 商店：先向服务端要一份最新的（战斗、任务都会改背包），再打开
        async void OpenBag() { if (bagUi.Visible) { bagUi.Close(); return; } Stop(); menu.Close(); wxUi.Close(); worldUi.Close(); sceneUi.Close(); await RefreshBag(); bagUi.Open(BagUi.Tab.Bag); }
        async void OpenShop(string shop) { Stop(); menu.Close(); wxUi.Close(); worldUi.Close(); sceneUi.Close(); await RefreshBag(); bagUi.Open(BagUi.Tab.Shop, shop); }
        async void OpenMall()
        {
            Stop(); menu.Close(); wxUi.Close(); worldUi.Close(); sceneUi.Close();
            if (await RefreshBag()) bagUi.Open(BagUi.Tab.Mall);
        }

        async System.Threading.Tasks.Task<bool> RefreshBag()
        {
            var current = role;
            var r = await net.BagOp(GuiQin.Inventory.BagCmd.Get, null);
            if (role != current) return false;
            if (!r.Ok || !r.data.ok) { if (!r.Ok) toast.Show(r.msg); return false; }
            role.bag = r.data.bag; role.coin = r.data.coin; role.hp = r.data.hp; role.mp = r.data.mp;
            role.yuanbao = r.data.yuanbao; role.silverNote = r.data.silverNote;
            RefreshHud();
            return true;
        }

        // 世界地图点名直达，服务端确认目的地后切图。
        async void GoWorld(string id, string escort = null)
        {
            MapDef d; if (!GameData.Maps.TryGetValue(id, out d) || !entered) return;
            if (map != null && id == map.Id) { toast.Show("你已经在" + d.name); return; }
            Stop();
            var r = await net.Teleport(id, escort);
            if (!r.Ok || !r.data.ok) { toast.Show(r.Ok ? (r.data.msg ?? "现在不能传送") : r.msg); return; }
            LoadMap(r.data.map, StageAt(r.data.map, r.data.x, r.data.y)); toast.Show("传送到 " + d.name);
            if (escort == GuiQin.Tower.ZiyinService.Guard) await RefreshBag();
        }

        void ClearMap()
        {
            if (waterFx != null) { waterFx.Destroy(); waterFx = null; }
            if (mapFx != null) { mapFx.Destroy(); mapFx = null; }
            foreach (var a in list) { if (a.Tag != null) a.Tag.Destroy(); Destroy(a.gameObject); }
            list.Clear();
            if (mapRoot != null) Destroy(mapRoot);
        }

        void LoadMap(string id, Vector2? at)
        {
            ClearMap(); SetWander(false);
            var def = GameData.Maps[id];
            map = new MapRuntime(id, def);
            mapRoot = new GameObject("map_" + id);
            var bgSprite = Art.Background(def.img, map.W);
            if (bgSprite != null)
            {
                var bg = new GameObject("bg"); bg.transform.SetParent(mapRoot.transform, false);
                var sr = bg.AddComponent<SpriteRenderer>(); sr.sprite = bgSprite; sr.sortingOrder = -10000;
                bg.transform.localScale = new Vector3(1f, (map.H / Art.PX) / bgSprite.bounds.size.y, 1f);      // 底图按舞台宽高拉伸（和网页版一致）
            }
            waterFx = WaterFx.Create(mapRoot.transform, id, map);
            mapFx = new MapFx(mapRoot.transform, cam.transform, map, cam);
            ApplySettings();
            fadePolys.Clear();
            if (def.fade != null) foreach (var z in def.fade) { var poly = new Vector2[z.poly.Length]; for (int i = 0; i < poly.Length; i++) poly[i] = new Vector2(z.poly[i][0], z.poly[i][1]); fadePolys.Add(poly); }
            if (def.npcs != null) foreach (var n in def.npcs) AddNpc(n);
            if (def.portals != null) foreach (var p in def.portals) AddPortal(p);
            if (def.tower > 0) AddTowerFoes(def);
            else if (def.foes != null) foreach (var f in def.foes) AddFoe(f);

            Vector2 start = at.HasValue && map.Inside(at.Value) ? at.Value : map.ToMap(def.start);
            player.Pos = start; player.Face = 1; player.Dir = 'f'; player.Dist = 0;
            Vector2 fp = new Vector2(start.x + (fox.H > 70f ? 96f : 46f), start.y + 18f);
            fox.Pos = map.Inside(fp) ? fp : start;
            Stop();
            hud.SetPlace(map);
            wx.SetMap(map);
            SnapCamera();
            SyncStory(); RefreshQuest();
        }

        void AddNpc(NpcPlace n)
        {
            NpcInfo info;
            if (!GameData.Npcs.TryGetValue(n.id, out info)) { Debug.LogWarning("没有 NPC 数据：" + n.id); return; }
            string key = !string.IsNullOrEmpty(info.sprite) ? info.sprite : n.id;
            var sprite = Art.One(key);
            if (sprite == null) { Debug.LogWarning("NPC 缺图：" + n.id + "（" + key + "）"); return; }
            var sz = Art.Size(key);
            const float playerHeight = 118f;    // 男女新手主角的站立身高约为 117～119 舞台像素。
            float spriteHeight = sprite.bounds.size.y * Art.PX;
            float scale = Mathf.Max(1f, playerHeight / spriteHeight);
            float shadowW = (sz != null ? sz[0] * 0.6f : 50f) * scale;
            var a = Actor.Make<Actor>("npc_" + n.id, worldRoot, sprite, map.ToMap(n.at), shadowW, scale);
            a.Kind = ActorKind.Npc; a.Id = n.id; a.Info = info; a.DisplayName = info.name; a.Title = info.title;
            a.NameColor = Ui.Hex(info.color, a.NameColor); a.TitleColor = Ui.Hex(info.titleColor, a.TitleColor);
            a.H = spriteHeight * scale;
            a.Tag = new TagLabel(tagCanvas.transform); a.SetTag();
            list.Add(a);
        }

        void AddPortal(PortalDef p)
        {
            var sprite = PortalActor.FirstFrame();
            var a = Actor.Make<PortalActor>("portal_" + p.to, worldRoot, sprite, map.ToMap(p.at), 0f);
            a.Kind = ActorKind.Portal; a.Id = "portal_" + p.to; a.Portal = p;
            a.SetGroundEffect();
            a.DisplayName = p.name + " ▶"; a.NameColor = new Color(0.85f, 1f, 1f);
            a.Tag = new TagLabel(tagCanvas.transform); a.SetTag();
            list.Add(a);
        }

        // 玄兵塔：只显示本层已通关的守卫和下一名；已通关的变灰
        void AddTowerFoes(MapDef def)
        {
            if (def.slots == null) return;
            for (int i = 0; i < def.slots.Length; i++)
            {
                int n = (def.tower - 1) * 10 + i + 1; if (n > role.tower + 1) break;
                AddFoe(new FoePlace { stage = "blxt_" + n, at = def.slots[i], face = 1 }, n <= role.tower);
            }
        }

        void AddFoe(FoePlace f, bool done = false)
        {
            StageDef st; EnemyDef en;
            if (!GameData.Stages.TryGetValue(f.stage, out st)) { Debug.LogWarning("没有关卡：" + f.stage); return; }
            string lead = st.enemies.Contains("boss") ? "boss" : st.enemies[0];
            if (!GameData.Enemies.TryGetValue(lead, out en)) return;
            var sprite = Art.One(en.sprite);
            if (sprite == null) { Debug.LogWarning("敌人缺图：" + en.sprite); return; }
            var sz = Art.Size(en.sprite);
            var a = Actor.Make<Actor>("foe_" + st.id, worldRoot, sprite, map.ToMap(f.at), sz != null ? sz[0] * 0.6f : 50f);
            a.Kind = ActorKind.Foe; a.Id = "foe_" + st.id; a.StageInfo = st; a.DisplayName = st.name; a.Done = done;
            a.Title = done ? "已通关" : (st.tower && st.n % 10 == 0 ? "首领 " : "") + "建议 " + st.rec + " 级";
            a.NameColor = done ? new Color(0.6f, 0.64f, 0.71f) : new Color(1f, 0.6f, 0.54f); a.TitleColor = done ? new Color(0.56f, 0.82f, 0.63f) : new Color(1f, 0.84f, 0.42f);
            a.Face = f.face == 0 ? 1 : f.face; a.H = sz != null ? sz[1] : 80f;
            a.Tag = new TagLabel(tagCanvas.transform); a.SetTag();
            list.Add(a);
        }

        // ---------------- 行走 ----------------
        Vector2 ScreenToStage(Vector2 sp)
        {
            var w = cam.ScreenToWorldPoint(new Vector3(sp.x, sp.y, -cam.transform.position.z));
            return Stage.FromWorld(w);
        }

        bool MoveTo(Vector2 dest, bool quiet)
        {
            Vector2 d;
            if (!map.Snap(dest, out d)) return false;
            var path = map.FindPath(player.Pos, d);
            if (path == null || path.Count == 0) return false;
            if (!player.Moving) player.MoveT = 0f;
            route = path; player.Moving = true;
            if (!quiet) { mark.transform.position = Stage.ToWorld(d); markT = 0f; }
            return true;
        }

        void GoTalk(Actor n)
        {
            talkTarget = n;
            int side = player.Pos.x >= n.Pos.x ? 1 : -1;
            Vector2 c = n.Pos;
            if (n.Kind != ActorKind.Portal)
            {
                var cands = new[] { new Vector2(n.Pos.x + 54 * side, n.Pos.y + 16), new Vector2(n.Pos.x - 54 * side, n.Pos.y + 16), new Vector2(n.Pos.x, n.Pos.y + 40), new Vector2(n.Pos.x + 34 * side, n.Pos.y + 44) };
                bool ok = false; foreach (var q in cands) if (map.Inside(q)) { c = q; ok = true; break; }
                if (!ok) c = n.Pos;
            }
            if (Vector2.Distance(c, player.Pos) < 8f) { Arrive(); return; }
            if (!MoveTo(c, true)) Arrive();
        }

        void Stop() { player.Moving = false; route.Clear(); talkTarget = null; }

        void Arrive()
        {
            var n = talkTarget; bool near = n != null && Vector2.Distance(n.Pos, player.Pos) < 150f;
            Stop(); ReportPos();
            if (!near) return;
            if (n.Kind != ActorKind.Portal) { player.Face = n.Pos.x >= player.Pos.x ? 1 : -1; player.Dir = n.Pos.y < player.Pos.y - 24f ? 'b' : 'f'; }
            if (n.Kind == ActorKind.Portal) Travel(n.Portal);
            else if (n.Kind == ActorKind.Foe) FoeClick(n);
            else Talk(n);
        }

        // ---------------- 战斗 ----------------
        // 点敌人 → 向服务端申请开战 → 切到战场界面。战斗的一切结算在服务端，这里只是表现（见 Battle/BattleController）。
        async void StartBattle(Actor foe)
        {
            if (battle != null || foe.StageInfo == null || !entered) return;
            var r = await net.BattleStart(foe.StageInfo.id);
            if (!r.Ok || !r.data.ok) { toast.Show(r.Ok ? (r.data.msg ?? "现在不能开战") : r.msg); return; }
            OpenBattle(r.data.battle, foe);
        }

        void OpenBattle(BattleView view, Actor foe)
        {
            Stop(); menu.Close(); wxUi.Close(); worldUi.Close(); sceneUi.Close(); petUi.Close(); trainUi.Close(); bagUi.Close();
            SetWander(false); chatUi.Close();
            ShowWorld(false);
            hud.SetBattleVisible();
            string bid = view.id;
            battle = BattleController.Begin(cam, uiStage, view, cmd => net.BattleAct(bid, cmd), end => EndBattle(end, foe), msg => toast.Show(msg), hud.SetVitals);
        }

        void ShowWorld(bool on)
        {
            worldRoot.gameObject.SetActive(on); if (mapRoot != null) mapRoot.SetActive(on);
            tagCanvas.gameObject.SetActive(on); hud.SetVisible(on); questUi.SetVisible(on); wx.SetVisible(on); if (mapFx != null) mapFx.SetVisible(on);
        }

        void EndBattle(BattleEnd end, Actor foe)
        {
            battle.Dispose(); battle = null;
            ShowWorld(true);
            var beat = Cur; bool advanced = end.step != role.step;
            ApplyExperience(end.exp, end.lv);
            role.lv = end.lv; role.step = end.step; role.cnt = end.cnt; role.tower = end.tower; role.bond = end.bond; RefreshBag();
            if (end.result == "lose" && !string.IsNullOrEmpty(end.respawnMap)) { LoadMap(end.respawnMap, null); toast.Show("战败，回到" + map.Def.name); return; }
            if (end.result == "win" && foe != null && foe.StageInfo != null && !foe.StageInfo.tower) { if (foe.Tag != null) foe.Tag.Destroy(); list.Remove(foe); Destroy(foe.gameObject); }
            if (end.result == "win" && foe != null && foe.StageInfo != null && foe.StageInfo.tower) LoadMap(map.Id, player.Pos);      // 塔里：守卫留在原地变灰，下一名守卫现身
            SnapCamera();
            SyncStory(); RefreshQuest();
            if (advanced && beat != null)
            {
                var pages = new List<string[]>();
                if (beat.last) { pages.AddRange(beat.outro); var nx = Cur; if (nx != null && nx.first && !nx.IsTalk) pages.AddRange(nx.accept); }
                toast.Show(Cur != null ? "任务更新：" + Cur.goal : "第一章已完成，后续剧情敬请期待");
                if (pages.Count > 0) ShowPages(pages, null);
            }
        }

        // 走传送阵：服务端核对出发地图和实际传送点。
        async void Travel(PortalDef p)
        {
            string from = map.Id;
            var r = await net.Portal(from, p.to);
            if (!r.Ok) { toast.Show(r.msg); return; }
            if (!r.data.ok) { toast.Show(r.data.msg ?? "现在还不能过去"); return; }
            if (map == null || map.Id != from) return;                       // 等待期间已经换图了
            MapDef dest = GameData.Maps[r.data.map];
            LoadMap(r.data.map, StageAt(r.data.map, r.data.x, r.data.y));
            toast.Show("来到 " + dest.name);
        }

        // 把当前位置报给服务端（数据坐标）。服务端用同一份碰撞数据校验；不合法会把角色拉回它认为的位置。
        async void ReportPos()
        {
            if (net == null || !entered || map == null) return;
            var req = new MoveReq { map = map.Id, to = new[] { player.Pos.x / map.Sc, player.Pos.y / map.Sc } };
            var r = await net.Move(req);
            if (r.Ok && !r.data.ok && map != null && map.Id == req.map)
            {
                var back = new Vector2(r.data.x * map.Sc, r.data.y * map.Sc);
                if (map.Inside(back)) { Stop(); player.Pos = back; toast.Show("位置已校正"); }
            }
        }

        // 和 NPC / 采集点说话：当前任务指向它 → 向服务端报“我找到他了”，服务端判定后推进；否则就是普通对话
        async void Talk(Actor n)
        {
            var b = Cur;
            if (b != null && b.IsTalk && b.npc == n.Id)
            {
                var r = await net.QuestTalk(n.Id);
                if (!r.Ok) { toast.Show(r.msg); return; }
                if (r.data.ok) { OnQuestBeat(b, r.data); return; }
            }
            TalkIdle(n);
        }

        void TalkIdle(Actor n)
        {
            var info = n.Info;
            string key = !string.IsNullOrEmpty(info.head) ? info.head : "head_" + n.Id;
            string text = string.IsNullOrEmpty(info.idle) ? "……" : info.idle;
            dialog.Open(n.DisplayName, n.NameColor, Art.One(key, true), text, () =>
            {
                if (info.escort != null && GameData.Maps.TryGetValue(info.escort, out var destination))
                    modal.Open(n.DisplayName, "要我送你去" + destination.name + "吗？",
                        new ModalUi.Btn("前往门派", () => GoWorld(info.escort, n.Id), true), new ModalUi.Btn("暂不前往", null));
                else AfterTalk(info);
            });
        }

        // ---------------- 主线任务 ----------------
        Beat Cur { get { return role != null ? StoryBeats.Current(role) : StoryBeats.At(0); } }

        Sprite FaceOf(string who)
        {
            if (who == "你") { string k = "head_" + role.cls; return Art.One(Art.Has(k) ? k : "head_player", true); }
            foreach (var kv in GameData.Npcs)
                if (!kv.Value.spot && kv.Value.name == who) return Art.One(!string.IsNullOrEmpty(kv.Value.head) ? kv.Value.head : "head_" + kv.Key, true);
            var sp = GameData.Raw["speakers"] != null ? GameData.Raw["speakers"][who] : null;
            return sp != null && sp.Type == Newtonsoft.Json.Linq.JTokenType.String ? Art.One((string)sp, true) : null;
        }

        // 连着播一组对白，点一下下一句；播完调 done
        void ShowPages(List<string[]> pages, Action done)
        {
            int i = 0; Action next = null;
            next = () =>
            {
                if (i >= pages.Count) { if (done != null) done(); return; }
                var pg = pages[i++]; bool me = pg[0] == "你";
                dialog.Open(me ? role.name : pg[0], me ? new Color(0.54f, 1f, 0.69f) : new Color(1f, 0.88f, 0.4f), FaceOf(pg[0]), pg[1], next);
            };
            next();
        }

        void ApplyExperience(int gain, int newLevel)
        {
            role.exp += gain;
            for (int lv = role.lv; lv < newLevel; lv++)
                role.exp = Mathf.Max(0, role.exp - Rewards.ExpNeed(lv));
        }

        void ApplyProgress(int step, int cnt, int lv, bool levelUp)
        {
            role.step = step; role.cnt = cnt; if (lv > 0) role.lv = lv; if (levelUp) { role.hp = -1; role.mp = -1; }
        }

        void OnQuestBeat(Beat b, QuestTalkResp d)
        {
            ApplyExperience(d.exp, d.lv);
            ApplyProgress(d.step, d.cnt, d.lv, d.levelUp);
            var pages = new List<string[]>(); if (b.first) pages.AddRange(b.accept); pages.AddRange(b.pages);
            ShowPages(pages, () => FinishBeat(b, d));
        }

        static string RewardText(int exp, int coin, Dictionary<string, int> items)
        {
            var parts = new List<string>(); if (exp > 0) parts.Add("经验 +" + exp); if (coin > 0) parts.Add("银两 +" + coin);
            if (items != null) foreach (var kv in items) { var it = GameData.Raw["items"][kv.Key]; parts.Add((it != null ? (string)it["name"] : kv.Key) + " ×" + kv.Value); }
            return string.Join("，", parts.ToArray());
        }

        void FinishBeat(Beat b, QuestTalkResp d)
        {
            SyncStory(); RefreshQuest();
            var pages = new List<string[]>();
            if (d.done)
            {
                pages.AddRange(b.outro);
                var nx = Cur; if (nx != null && nx.first && !nx.IsTalk) pages.AddRange(nx.accept);        // 下一个任务不是“找人”：顺带播它的接取对白
                toast.Show(d.levelUp ? "升到 " + role.lv + " 级！气血法力已回满" : "完成「" + b.title + "」：" + RewardText(d.exp, d.coin, d.items));
                var _ = RefreshBag();
            }
            else toast.Show(Cur != null ? "任务更新：" + Cur.goal : "第一章已完成，后续剧情敬请期待");
            if (!string.IsNullOrEmpty(d.msg)) toast.Show(d.msg);
            if (pages.Count > 0) ShowPages(pages, null);
        }

        void FoeClick(Actor foe)
        {
            if (foe.Done) { toast.Show("这名守卫你已经打败了，不能重复挑战"); return; }
            var b = Cur;
            if (foe.StageInfo != null && foe.StageInfo.story && b != null && b.stage == foe.StageInfo.id && b.pre.Count > 0 && !preSeen.Contains(role.step))
            { preSeen.Add(role.step); ShowPages(b.pre, () => StartBattle(foe)); return; }
            StartBattle(foe);
        }

        // 剧情首领 / 采集点：只在对应任务进行到那一拍时出现在地图上；和首领同名的 NPC 同时藏起来
        void SyncStory()
        {
            if (map == null || role == null) return;
            var q = Cur;
            StageDef fs = null; if (q != null && q.type == "fight" && q.map == map.Id) GameData.Stages.TryGetValue(q.stage, out fs);
            foreach (var a in list) if (a.Kind == ActorKind.Npc && a.Sb < 0) { bool hide = fs != null && a.DisplayName == fs.name; a.Hidden = hide; a.gameObject.SetActive(!hide); }
            foreach (var a in list.Where(x => x.Sb >= 0 && !(q != null && x.Sb == role.step)).ToList()) { if (a.Tag != null) a.Tag.Destroy(); list.Remove(a); Destroy(a.gameObject); }
            if (q == null || q.map != map.Id || list.Any(a => a.Sb == role.step)) return;
            if (q.type == "fight" || q.type == "kill")
            {
                int n0 = list.Count; AddFoe(new FoePlace { stage = q.stage, at = q.at, face = 1 });
                if (list.Count > n0) list[list.Count - 1].Sb = role.step;
            }
            else if (q.type == "spot")
            {
                var a = Actor.Make<Actor>("spot_" + q.npc, worldRoot, Art.Dot(), map.ToMap(q.at), 0f);
                a.Body.color = new Color(0.62f, 0.94f, 1f, 0.85f); a.Shadow.enabled = false;
                a.Kind = ActorKind.Npc; a.Id = q.npc; a.Info = GameData.Npcs[q.npc]; a.DisplayName = q.spot; a.NameColor = new Color(0.62f, 0.94f, 1f); a.H = 40f; a.Sb = role.step;
                a.Tag = new TagLabel(tagCanvas.transform); a.SetTag(); list.Add(a);
            }
        }

        // 任务目标：目标在本图就是那个人；不在本图，指向通往那张图的传送阵
        Actor QuestTarget()
        {
            var q = Cur; if (q == null || map == null) return null;
            if (q.IsSectGate) return list.Find(x => x.Kind == ActorKind.Npc &&
                (x.Id == "dao_menzhu" || x.Id == "taixu_mo") && x.gameObject.activeSelf);
            Actor a = q.IsTalk ? list.Find(x => x.Kind == ActorKind.Npc && x.Id == q.npc && x.gameObject.activeSelf) : list.Find(x => x.Sb == role.step);
            return a ?? HopTo(q.map);
        }

        Actor HopTo(string target)
        {
            if (string.IsNullOrEmpty(target) || target == map.Id) return null;
            var from = new Dictionary<string, string> { { map.Id, null } }; var queue = new Queue<string>(); queue.Enqueue(map.Id);
            while (queue.Count > 0)
            {
                string m = queue.Dequeue();
                if (m == target) { string c = m; while (from[c] != map.Id) c = from[c]; return list.Find(a => a.Kind == ActorKind.Portal && a.Portal.to == c); }
                MapDef md; if (!GameData.Maps.TryGetValue(m, out md) || md.portals == null) continue;
                foreach (var p in md.portals) if (!from.ContainsKey(p.to)) { from[p.to] = m; queue.Enqueue(p.to); }
            }
            return null;
        }

        void RefreshQuest()
        {
            if (role == null) return;
            RefreshHud();
            var q = Cur;
            questUi.Set(q != null ? q.title : "江湖路远", q != null ? q.goal + (q.type == "kill" ? "（" + Mathf.Min(role.cnt, q.n) + "/" + q.n + "）" : "") : "第一章已完成，去各处历练升级吧", q != null ? q.chapter : "待续");
            var tgt = QuestTarget();
            foreach (var a in list) { bool m = a == tgt; if (a.Marked != m) { a.Marked = m; a.SetTag(); } }
            hud.SetMapActors(list);
        }

        void QuestGo()
        {
            if (ModalOpen || dialog.Visible || battle != null || !entered) return;
            var t = QuestTarget();
            if (t == null && Cur?.IsSectGate == true)
            {
                modal.Open(Cur.title, Cur.summary,
                    new ModalUi.Btn("去太虚之林·道家", () => GoWorld("taixuforest"), true),
                    new ModalUi.Btn("去太虚之峰·墨家", () => GoWorld("taixupeak")));
                return;
            }
            if (t == null) t = list.Find(a => a.Kind == ActorKind.Portal);
            if (t != null) { GoTalk(t); toast.Show("自动寻路：" + t.DisplayName); }
        }

        // NPC 功能：商店 / 铁匠 / 休息 / 门派 / 玄兵塔。竞技场（要和别的玩家实时对战）、神兽使者、技能修炼还没做
        void AfterTalk(NpcInfo info)
        {
            if (info.shop == "smith") OpenSmith();
            else if (!string.IsNullOrEmpty(info.shop)) OpenShop(info.shop);
            else if (info.act == "fbEntry")
            {
                var cfg = GuiQin.Tower.ZiyinService.Config;
                var text = (string)cfg["intro"] + "\n\n等级 " + (int)cfg["lv"] + "，每日 " + (int)cfg["daily"] +
                    " 次，入场 " + (int)cfg["fee"] + " 银两。";
                modal.Open((string)cfg["name"], text,
                    new ModalUi.Btn("进入副本", () => GoWorld(GuiQin.Tower.ZiyinService.Entrance, GuiQin.Tower.ZiyinService.Guard), true),
                    new ModalUi.Btn("取消", null));
            }
            else if (info.act == "towerEnter") TowerEnterModal();
            else if (info.act == "towerTp") TowerTpModal();
            else if (info.act == "joinSect") JoinSect("dao");
            else if (info.act == "joinMo") JoinSect("mo");
            else if (info.act == "ssEnvoy") OpenEnvoy();
            else if (info.act == "pickHall" || info.act == "pickMoHall") PickHall(info.hall);
            else if (!string.IsNullOrEmpty(info.act)) toast.Show("功能待移植：" + info.act);
            else if (info.rest) Rest(info);
        }

        async void Rest(NpcInfo info)
        {
            var r = await net.BagOp(GuiQin.Inventory.BagCmd.Rest, null);
            if (!r.Ok) { toast.Show(r.msg); return; }
            role.hp = r.data.hp; role.mp = r.data.mp;
            RefreshHud();
            if (!string.IsNullOrEmpty(r.data.msg)) toast.Show(string.IsNullOrEmpty(info.restMsg) ? r.data.msg : info.restMsg);
        }

        async void OpenSmith() { Stop(); menu.Close(); wxUi.Close(); worldUi.Close(); sceneUi.Close(); await RefreshBag(); bagUi.Open(BagUi.Tab.Smith); }

        // ---------------- 门派 ----------------
        static Newtonsoft.Json.Linq.JToken Cls(string id) { return GameData.Raw["classes"][id]; }

        void ApplyRoleLook()
        {
            player.DisplayName = role.name; player.SetLook(role.cls);
            player.Title = "【" + GuiQin.Sect.SectService.ClsName(role.cls) + "】"; player.SetTag();
        }

        void JoinSect(string sect)
        {
            string name = sect == "mo" ? "墨家" : "道家", other = sect == "mo" ? "道家" : "墨家"; var c = Cls(role.cls);
            if (!string.IsNullOrEmpty(role.sect))
            {
                if (role.sect != sect) toast.Show("你已拜入" + other + "，不可再入" + name);
                else if ((string)c["name"] == name + "弟子") OpenTrain("menzhu");
                else toast.Show("分堂技能请找本堂堂主学习、升级");
                return;
            }
            if (c["joinSect"] == null) { toast.Show("你已是门派弟子"); return; }
            if (role.lv < GuiQin.Sect.SectService.SectLv) { toast.Show("需达到 " + GuiQin.Sect.SectService.SectLv + " 级方可入门（现在 " + role.lv + " 级）"); return; }
            string say = sect == "mo" ? "墨巨子说：兼爱非攻，机关为用。入我门下，再去猛士、遁甲两位堂主处择一堂修行。" : "道家门主说：道法自然。入我门下，再去琴魔、天音两位堂主处择一堂修行。";
            modal.Open("拜入" + name, say, new ModalUi.Btn("拜入" + name, () => DoSect(true, sect), true), new ModalUi.Btn("再想想", null));
        }

        void PickHall(string hall)
        {
            bool dao = hall == "qm" || hall == "ty"; string pre = dao ? "dao" : "mo", sectName = dao ? "道家" : "墨家";
            string hname = hall == "qm" ? "琴魔" : hall == "ty" ? "天音" : hall == "ms" ? "猛士" : "遁甲"; var c = Cls(role.cls);
            if (!string.IsNullOrEmpty(role.sect) && role.sect != pre) { toast.Show("你已拜入" + (dao ? "墨家" : "道家")); return; }
            if (c["joinSect"] == null) { if ((string)c["sect"] == sectName && (string)c["name"] == hname) OpenTrain("hall"); else toast.Show("你已入另一堂，分堂不可改选"); return; }
            if (role.sect != pre) { toast.Show("先去找" + (dao ? "道家门主" : "墨巨子") + "入门（" + GuiQin.Sect.SectService.SectLv + " 级）"); return; }
            string id = GuiQin.Sect.SectService.HallClass(role.cls, hall); if (id == null) { toast.Show("这个堂不收你"); return; }
            var d = Cls(id);
            modal.Open("选择分堂", "<b>" + (string)d["name"] + "</b>（" + (string)d["weapon"] + "）\n<size=24>" + (string)d["desc"] + "</size>\n\n分堂选定后不能改选。",
                new ModalUi.Btn("拜入" + hname, () => DoSect(false, hall), true), new ModalUi.Btn("再想想", null));
        }

        async void DoSect(bool join, string key)
        {
            bool required = join && Cur?.IsSectGate == true;
            var r = join ? await net.SectJoin(key) : await net.SectHall(key);
            if (!r.Ok) { toast.Show(r.msg); return; }
            if (!r.data.ok) { toast.Show(r.data.msg); return; }
            role.cls = r.data.cls; role.sect = r.data.sect; role.hp = r.data.hp; role.mp = r.data.mp;
            if (r.data.skl != null) role.skl = r.data.skl;
            ApplyRoleLook(); SyncStory(); RefreshQuest(); toast.Show(r.data.msg);
            if (join)
            {
                var next = required ? Cur : null;
                Action continueStory = next != null && next.first && !next.IsTalk ? () => ShowPages(next.accept, null) : null;
                dialog.Open("提示", new Color(1f, 0.88f, 0.4f), null, key == "mo" ? "你拜入墨家，背起长剑。日后可找猛士堂主或遁甲堂主选择分堂。" : "你拜入道家，背起箜篌。日后可找琴魔堂主或天音堂主选择分堂。", continueStory);
            }
        }

        // ---------------- 玄兵塔 ----------------
        void TowerEnterModal()
        {
            var T = GameData.Raw["tower"]; int p = role.tower, f = GuiQin.Tower.TowerService.FloorOf(p), min = GuiQin.Tower.TowerService.MinLv;
            if (role.lv < min) { modal.Open("百炼玄兵塔", "塔里的守卫不是闹着玩的。\n等你到 <b>" + min + " 级</b> 再来（你现在 " + role.lv + " 级）。", new ModalUi.Btn("知道了", null)); return; }
            modal.Open("百炼玄兵塔", "已通关守卫 <b>" + p + " / 70</b>　" + (p >= 70 ? "全塔通关" : "当前所在：第 " + f + " 层 · " + (string)T["floors"][f - 1]["name"]) + "\n<size=22>每层十名守卫，按顺序挑战；第十名是本层首领，打败后才能上楼。</size>",
                new ModalUi.Btn("进入玄兵塔", () => TowerGo("enter"), true), new ModalUi.Btn("以后再说", null));
        }

        void TowerTpModal()
        {
            int f = map.Def.tower, p = role.tower; bool up = f < 7 && p >= f * 10;
            var btns = new List<ModalUi.Btn>();
            if (up) btns.Add(new ModalUi.Btn("前往第 " + (f + 1) + " 层", () => TowerGo("next"), true));
            btns.Add(new ModalUi.Btn("离开玄兵塔", () => TowerGo("leave"))); btns.Add(new ModalUi.Btn("再等等", null));
            modal.Open("塔内传送师", up ? "本层首领已破，上楼的路开了。" : f >= 7 && p >= 70 ? "塔已经通了。你想出去，我送你。" : "要上楼，先打败本层的首领（第 " + f + " 层第十名守卫）。", btns.ToArray());
        }

        async void TowerGo(string op)
        {
            var r = await net.Tower(op);
            if (!r.Ok) { toast.Show(r.msg); return; }
            if (!r.data.ok) { toast.Show(r.data.msg); return; }
            LoadMap(r.data.map, StageAt(r.data.map, r.data.x, r.data.y));
            toast.Show(op == "leave" ? "走出了玄兵塔" : "来到 " + map.Def.name);
        }

        void StepPlayer(float dt)
        {
            if (!player.Moving) return;
            player.MoveT += dt;
            float left = Speed * Mathf.Min(1f, 0.4f + player.MoveT / 0.2f) * dt;      // 起步有一小段加速
            Vector2 p0 = player.Pos;
            while (left > 0f && route.Count > 0)
            {
                Vector2 t = route[0], dv = t - player.Pos; float d = dv.magnitude;
                if (d <= left) { player.Pos = t; left -= d; route.RemoveAt(0); }
                else { player.Pos += dv / d * left; left = 0f; }
            }
            // 朝向：上下决定用正面还是背影，左右决定翻转；变化不大时保持，免得来回抖
            Vector2 mv = player.Pos - p0; float m = mv.magnitude;
            if (m > 0.01f)
            {
                player.Dist += m;
                if (Mathf.Abs(mv.y) > m * 0.3f) player.Dir = mv.y < 0 ? 'b' : 'f';
                if (Mathf.Abs(mv.x) > m * 0.25f) player.Face = mv.x > 0 ? 1 : -1;
            }
            if (route.Count == 0) Arrive();
        }

        void StepFox(float dt)
        {
            Vector2 to = new Vector2(player.Pos.x - fox.Pos.x, player.Pos.y + 10f - fox.Pos.y); float dist = to.magnitude;
            float nr = fox.H > 70f ? 104f : 62f;
            if (dist > nr + 22f) fox.Moving = true;
            if (dist < nr) fox.Moving = false;
            if (dist > 600f) { fox.Pos = player.Pos; return; }
            if (!fox.Moving || dist < 0.01f) return;
            float len = Mathf.Min(dist - (nr - 4f), (Speed + 30f) * dt); float sx = to.x / dist * len;
            fox.Pos += new Vector2(sx, to.y / dist * len);
            if (Mathf.Abs(sx) > 0.05f) fox.Face = sx > 0 ? 1 : -1;
            if (Mathf.Abs(to.y) > dist * 0.35f) fox.Dir = to.y < 0 ? 'b' : 'f';
        }

        // ---------------- 输入 ----------------
        void HandleInput()
        {
            PointerInput.Poll();
            if (ModalOpen) { holding = false; return; }
            if (PointerInput.Down)
            {
                downOnUi = PointerInput.OverUi();
                if (!downOnUi) SetWander(false);                          // 自己点地图 = 接管，挂机停
                holding = !downOnUi && !dialog.Visible;
                holdAt = time; lastHoldPos = PointerInput.Pos;
            }
            // 按住拖动：手指不抬起时，主角一直朝手指的位置走
            if (PointerInput.Held && holding && time - holdAt >= 0.16f && (PointerInput.Pos - lastHoldPos).sqrMagnitude > 16f)
            {
                holdAt = time; lastHoldPos = PointerInput.Pos; talkTarget = null;
                MoveTo(ScreenToStage(PointerInput.Pos), true);
            }
            if (PointerInput.Up)
            {
                holding = false;
                if (dialog.Visible) { dialog.Advance(); return; }          // 对话框开着：点哪儿都是“下一句 / 关闭”
                if (!downOnUi) OnClick(PointerInput.Pos);
            }
        }

        void OnClick(Vector2 sp)
        {
            Vector2 p = ScreenToStage(sp);
            Actor hit = null;
            foreach (var n in list)
            {
                if (!n.gameObject.activeSelf) continue;
                float h = n.Kind == ActorKind.Portal ? n.H : n.H * 1.35f;
                if (Mathf.Abs(p.x - n.Pos.x) < 54f && p.y < n.Pos.y + 20f && p.y > n.Pos.y - h - 40f) hit = n;
            }
            if (hit != null) GoTalk(hit); else { talkTarget = null; MoveTo(p, false); }
        }

        // ---------------- 相机 ----------------
        Vector3 CamTarget()
        {
            float aspect = (float)Screen.width / Mathf.Max(1, Screen.height);
            float size = Mathf.Max(6.4f, 3.6f / aspect);                // 竖屏设计 720×1280：至少完整露出这么大
            cam.orthographicSize = size;
            float hw = size * aspect * Art.PX, hh = size * Art.PX;
            Vector2 c = new Vector2(player.Pos.x, player.Pos.y - 60f);    // 主角在画面中心偏下
            c.x = map.W <= 2f * hw ? map.W / 2f : Mathf.Clamp(c.x, hw, map.W - hw);
            c.y = map.H <= 2f * hh ? map.H / 2f : Mathf.Clamp(c.y, hh, map.H - hh);
            var w = Stage.ToWorld(c); w.z = -10f; return w;
        }

        void SnapCamera() { cam.transform.position = CamTarget(); }

        // ---------------- 每帧 ----------------
        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f); time += dt;
            if (net != null) net.Tick();
            chatUi.Tick(dt, entered && battle == null && map != null);
            if (battle != null) { toast.Tick(dt); return; }                // 战斗中：地图那边整个停住
            if (map == null) { toast.Tick(dt); return; }                // 还在登录 / 进图，场景没建好
            HandleInput();
            StepWander(dt);
            StepPlayer(dt); StepFox(dt);
            hud.SetPosition(player.Pos);
            var target = CamTarget();
            cam.transform.position = Vector3.Lerp(cam.transform.position, target, 1f - Mathf.Exp(-14f * dt));
            player.Sync(); player.Animate(time);
            fox.Sync(); fox.Animate(time);
            foreach (var a in list) a.Animate(time);
            dialog.Tick(dt); toast.Tick(dt);
            worldUi.Tick(dt);
            wx.Tick(dt);
            if (mapFx != null) mapFx.Tick(dt, time, wx.Wet, wx.Precip);
            if (waterFx != null) waterFx.Tick(dt, time);
            ApplySeeThru(dt);
            if (markT < 0.6f)                                           // 点击落点的小圈
            {
                markT += dt; float k = markT / 0.6f;
                mark.transform.localScale = Vector3.one * Mathf.Lerp(0.15f, 0.9f, k);
                mark.color = new Color(1f, 0.9f, 0.4f, (1f - k) * 0.8f);
            }
            else if (mark.color.a > 0f) mark.color = new Color(1f, 0.9f, 0.4f, 0f);
        }

        // 遮挡虚化：脚在 base 之上（更靠后）且身体与 fade 多边形有重叠时变半透明。逻辑同网页版 seeThru（取 4×5 个采样点，≥3 个落在多边形里）
        bool SeeThru(Actor a)
        {
            var zs = map.Def.fade; if (zs == null) return false;
            float f = map.Sc, x = a.Pos.x / f, y = a.Pos.y / f, hh = a.H * 1.35f / f, hw = 34f / f;
            for (int zi = 0; zi < zs.Count; zi++)
            {
                if (y >= zs[zi].baseY) continue;
                int n = 0;
                for (int i = 0; i < 4 && n < 3; i++)
                    for (int j = 0; j < 5; j++)
                        if (MapRuntime.InPoly(fadePolys[zi], new Vector2(x - hw + i * hw * 2f / 3f, y - hh * j / 4f))) n++;
                if (n >= 3) return true;
            }
            return false;
        }

        void ApplySeeThru(float dt)
        {
            if (map.Def.fade == null) { player.ApplyThru(false, dt); fox.ApplyThru(false, dt); foreach (var a in list) if (a.Kind != ActorKind.Portal) a.ApplyThru(false, dt); return; }
            player.ApplyThru(SeeThru(player), dt); fox.ApplyThru(SeeThru(fox), dt);
            foreach (var a in list) if (a.Kind == ActorKind.Npc || a.Kind == ActorKind.Foe) a.ApplyThru(SeeThru(a), dt);
        }

        void LateUpdate()
        {
            UpdateTag(player); UpdateTag(fox);
            foreach (var a in list) UpdateTag(a);
        }

        void UpdateTag(Actor a)
        {
            if (a.Tag == null || battle != null) return;
            var top = Stage.ToWorld(a.Pos) + new Vector3(0f, (a.H + 6f) / Art.PX, 0f);
            Vector3 sp = cam.WorldToScreenPoint(top);
            bool vis = sp.z > 0f && sp.x > -150f && sp.x < Screen.width + 150f && sp.y > -100f && sp.y < Screen.height + 150f;
            a.Tag.SetVisible(vis && !a.Hidden);
            if (vis) a.Tag.Root.position = new Vector3(sp.x, sp.y, 0f);
        }
    }
}
