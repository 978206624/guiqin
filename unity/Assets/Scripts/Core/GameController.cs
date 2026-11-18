using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using GuiQin.Net;
using GuiQin.Battle;

namespace GuiQin
{
    // 主控：相机 / 界面 / 地图装载 / 点击行走 / 对话 / 传送。对应网页版 game.js 的“地图与行走”整块。
    public class GameController : MonoBehaviour
    {
        const float Speed = 230f;

        Camera cam; Transform worldRoot; Canvas tagCanvas, uiCanvas;
        MapRuntime map; GameObject mapRoot;
        PlayerActor player; PetActor fox;
        readonly List<Actor> list = new List<Actor>();
        List<Vector2> route = new List<Vector2>();
        Actor talkTarget;
        DialogUi dialog; ToastUi toast; HudUi hud; DebugMenuUi menu;
        WeatherSystem wx; WeatherUi wxUi; WorldMapUi worldUi;
        MapFx mapFx; WaterFx waterFx;
        readonly List<Vector2[]> fadePolys = new List<Vector2[]>();
        string[] looks;
        IGameBackend net; RoleState role; bool entered;
        BattleController battle;
        SpriteRenderer mark; float markT = 99f;
        bool holding, downOnUi; float holdAt; Vector2 lastHoldPos;
        float time;

        void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.orientation = ScreenOrientation.Portrait;
            GameData.Load();
            BuildCamera(); BuildEventSystem();
            wx = new WeatherSystem(cam);
            BuildUi();
            var ls = new List<string>();
            foreach (var k in GameData.Assets.size.Keys) if (k.StartsWith("walk_f_")) ls.Add(k.Substring(7));
            ls.Sort(); looks = ls.ToArray();
            worldRoot = new GameObject("World").transform;
            CreatePlayerAndPet();
        }

        // 进游戏：登录 → 取角色 → 进入角色所在的地图。状态都来自后端，客户端不存档。
        async void Start() { await EnterWorld(); }

        async System.Threading.Tasks.Task EnterWorld()
        {
            entered = false;
            if (net != null) net.Close();
            net = BackendFactory.Create();
            net.OnPush += OnPush; net.OnDisconnected += why => toast.Show(why);
            var lr = await net.Login(new LoginReq { account = SystemInfo.deviceUniqueIdentifier, device = SystemInfo.deviceModel, platform = Application.platform.ToString(), clientVer = Application.version });
            if (!lr.Ok || lr.data.roles == null || lr.data.roles.Count == 0) { toast.Show("登录失败：" + lr.msg); return; }
            var er = await net.EnterGame(lr.data.roles[0].id);
            if (!er.Ok) { toast.Show("进入游戏失败：" + er.msg); return; }
            role = er.data.role;
            player.DisplayName = role.name; player.SetLook(role.cls); player.SetTag();
            MapDef d; if (!GameData.Maps.TryGetValue(role.map, out d)) role.map = "village";
            LoadMap(role.map, StageAt(role.map, role.x, role.y));
            entered = true;
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
            if (cmd == Cmd.PushKick) { toast.Show("你已被下线：" + (data != null ? (string)data["reason"] : "")); entered = false; }
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
            hud = new HudUi(uiCanvas.transform, () => menu.Toggle(), OpenWeather, OpenWorld);
            dialog = new DialogUi(uiCanvas.transform);
            toast = new ToastUi(uiCanvas.transform);
            menu = new DebugMenuUi(uiCanvas.transform, id => GoWorld(id), NextLook, ResetRole);
            wxUi = new WeatherUi(uiCanvas.transform, wx, () => map != null ? map.Def.name : "");
            worldUi = new WorldMapUi(uiCanvas.transform, GoWorld, () => map != null ? map.Id : "village");
            wx.Changed += (k, announce) => { hud.SetWeather(WeatherSystem.NameOf(k)); if (announce) toast.Show("天气转为 " + WeatherSystem.NameOf(k)); };
            var mk = new GameObject("clickMark"); mk.transform.SetParent(transform, false);
            mark = mk.AddComponent<SpriteRenderer>(); mark.sprite = Art.Dot(); mark.color = new Color(1f, 0.9f, 0.4f, 0f); mark.sortingOrder = 30000;
        }

        void CreatePlayerAndPet()
        {
            player = Actor.Make<PlayerActor>("player", worldRoot, null, Vector2.zero, 50f);
            player.Kind = ActorKind.Player; player.Id = "player"; player.DisplayName = "云中客"; player.Title = "【游侠】";
            player.NameColor = new Color(0.54f, 1f, 0.69f); player.Hop = 7f;
            player.SetLook("dao_ty");
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

        // ---------------- 角色（状态在后端）----------------
        void ResetRole()
        {
            var m = net as MockBackend;
            if (m == null) { toast.Show("只有本地演示后端能重置"); return; }
            m.ResetRole(); var _ = EnterWorld();
        }

        void NextLook()
        {
            if (looks.Length == 0) return;
            int i = Array.IndexOf(looks, player.ClassId); player.SetLook(looks[(i + 1) % looks.Length]);
            player.SetTag(); toast.Show("外观：" + player.ClassId);
            if (net != null) net.SetLook(player.ClassId);
        }

        // ---------------- 载入地图 ----------------
        bool ModalOpen { get { return menu.Visible || wxUi.Visible || worldUi.Visible; } }

        void OpenWeather() { if (!wxUi.Visible) { menu.Close(); worldUi.Close(); wxUi.Open(); } else wxUi.Close(); }
        void OpenWorld() { Stop(); menu.Close(); wxUi.Close(); worldUi.Open(); }

        // 世界地图点名传送：同一张图只提示；其它由服务端判定（等级 / 费用），通过了才切图
        async void GoWorld(string id)
        {
            MapDef d; if (!GameData.Maps.TryGetValue(id, out d) || !entered) return;
            if (map != null && id == map.Id) { toast.Show("你已经在" + d.name); return; }
            var r = await net.Teleport(id);
            if (!r.Ok || !r.data.ok) { toast.Show(r.Ok ? (r.data.msg ?? "现在不能传送") : r.msg); return; }
            LoadMap(r.data.map, StageAt(r.data.map, r.data.x, r.data.y)); toast.Show("传送到 " + d.name);
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
            ClearMap();
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
            fadePolys.Clear();
            if (def.fade != null) foreach (var z in def.fade) { var poly = new Vector2[z.poly.Length]; for (int i = 0; i < poly.Length; i++) poly[i] = new Vector2(z.poly[i][0], z.poly[i][1]); fadePolys.Add(poly); }
            if (def.npcs != null) foreach (var n in def.npcs) AddNpc(n);
            if (def.portals != null) foreach (var p in def.portals) AddPortal(p);
            if (def.foes != null) foreach (var f in def.foes) AddFoe(f);

            Vector2 start = at.HasValue && map.Inside(at.Value) ? at.Value : map.ToMap(def.start);
            player.Pos = start; player.Face = 1; player.Dir = 'f'; player.Dist = 0;
            Vector2 fp = new Vector2(start.x + (fox.H > 70f ? 96f : 46f), start.y + 18f);
            fox.Pos = map.Inside(fp) ? fp : start;
            Stop();
            hud.SetPlace(def.name);
            wx.SetMap(map);
            SnapCamera();
        }

        void AddNpc(NpcPlace n)
        {
            NpcInfo info;
            if (!GameData.Npcs.TryGetValue(n.id, out info)) { Debug.LogWarning("没有 NPC 数据：" + n.id); return; }
            string key = !string.IsNullOrEmpty(info.sprite) ? info.sprite : n.id;
            var sprite = Art.One(key);
            if (sprite == null) { Debug.LogWarning("NPC 缺图：" + n.id + "（" + key + "）"); return; }
            var sz = Art.Size(key);
            var a = Actor.Make<Actor>("npc_" + n.id, worldRoot, sprite, map.ToMap(n.at), sz != null ? sz[0] * 0.6f : 50f);
            a.Kind = ActorKind.Npc; a.Id = n.id; a.Info = info; a.DisplayName = info.name; a.Title = info.title;
            a.NameColor = Ui.Hex(info.color, a.NameColor); a.TitleColor = Ui.Hex(info.titleColor, a.TitleColor);
            a.H = sz != null ? sz[1] : 80f;
            a.Tag = new TagLabel(tagCanvas.transform); a.SetTag();
            list.Add(a);
        }

        void AddPortal(PortalDef p)
        {
            Sprite s = !string.IsNullOrEmpty(p.art) ? Art.One(p.art, true) : null;
            var a = Actor.Make<PortalActor>("portal_" + p.to, worldRoot, s != null ? s : Art.Dot(), map.ToMap(p.at), 0f);
            if (s == null) a.Body.color = new Color(0.45f, 0.9f, 1f, 0.7f);
            a.Shadow.enabled = false;
            a.Body.sortingOrder = s != null ? -5000 : Mathf.RoundToInt(a.Pos.y);      // 带贴图的传送阵贴在地面上，在所有人物下面
            a.Kind = ActorKind.Portal; a.Id = "portal_" + p.to; a.Portal = p; a.H = 50f;
            a.DisplayName = p.name + " ▶"; a.NameColor = new Color(0.85f, 1f, 1f);
            a.Tag = new TagLabel(tagCanvas.transform); a.SetTag();
            list.Add(a);
        }

        void AddFoe(FoePlace f)
        {
            StageDef st; EnemyDef en;
            if (!GameData.Stages.TryGetValue(f.stage, out st)) { Debug.LogWarning("没有关卡：" + f.stage); return; }
            string lead = st.enemies.Contains("boss") ? "boss" : st.enemies[0];
            if (!GameData.Enemies.TryGetValue(lead, out en)) return;
            var sprite = Art.One(en.sprite);
            if (sprite == null) { Debug.LogWarning("敌人缺图：" + en.sprite); return; }
            var sz = Art.Size(en.sprite);
            var a = Actor.Make<Actor>("foe_" + st.id, worldRoot, sprite, map.ToMap(f.at), sz != null ? sz[0] * 0.6f : 50f);
            a.Kind = ActorKind.Foe; a.Id = "foe_" + st.id; a.StageInfo = st; a.DisplayName = st.name; a.Title = "建议 " + st.rec + " 级";
            a.NameColor = new Color(1f, 0.6f, 0.54f); a.TitleColor = new Color(1f, 0.84f, 0.42f);
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
            else if (n.Kind == ActorKind.Foe) StartBattle(n);
            else Talk(n);
        }

        // ---------------- 战斗 ----------------
        // 点敌人 → 向服务端申请开战 → 切到战场界面。战斗的一切结算在服务端，这里只是表现（见 Battle/BattleController）。
        async void StartBattle(Actor foe)
        {
            if (battle != null || foe.StageInfo == null || !entered) return;
            var r = await net.BattleStart(foe.StageInfo.id);
            if (!r.Ok || !r.data.ok) { toast.Show(r.Ok ? (r.data.msg ?? "现在不能开战") : r.msg); return; }
            Stop(); menu.Close(); wxUi.Close(); worldUi.Close();
            ShowWorld(false);
            string bid = r.data.battle.id;
            battle = BattleController.Begin(cam, uiCanvas.transform, r.data.battle, cmd => net.BattleAct(bid, cmd), end => EndBattle(end, foe), msg => toast.Show(msg));
        }

        void ShowWorld(bool on)
        {
            worldRoot.gameObject.SetActive(on); if (mapRoot != null) mapRoot.SetActive(on);
            tagCanvas.gameObject.SetActive(on); hud.SetVisible(on); wx.SetVisible(on); if (mapFx != null) mapFx.SetVisible(on);
        }

        void EndBattle(BattleEnd end, Actor foe)
        {
            battle.Dispose(); battle = null;
            ShowWorld(true);
            if (end.result == "lose" && !string.IsNullOrEmpty(end.respawnMap)) { LoadMap(end.respawnMap, null); toast.Show("战败，回到" + map.Def.name); return; }
            if (end.result == "win" && foe != null && foe.StageInfo != null && !foe.StageInfo.tower) { if (foe.Tag != null) foe.Tag.Destroy(); list.Remove(foe); Destroy(foe.gameObject); }
            SnapCamera();
        }

        // 走传送阵：任务锁 / 等级由服务端判，客户端只显示结果（PortalDef 里的 needQuest 等字段服务端也有一份）
        async void Travel(PortalDef p)
        {
            string from = map.Id;
            var r = await net.Portal(from, p.to);
            if (!r.Ok) { toast.Show(r.msg); return; }
            if (!r.data.ok) { toast.Show(!string.IsNullOrEmpty(r.data.msg) ? r.data.msg : (string.IsNullOrEmpty(p.lockMsg) ? "现在还不能过去" : p.lockMsg)); return; }
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

        void Talk(Actor n)
        {
            var info = n.Info;
            string key = !string.IsNullOrEmpty(info.head) ? info.head : "head_" + n.Id;
            string text = string.IsNullOrEmpty(info.idle) ? "……" : info.idle;
            dialog.Open(n.DisplayName, n.NameColor, Art.One(key, true), text, () => AfterTalk(info));
        }

        // 商店 / 锻造 / 塔 / 竞技场 / 门派：界面还没移植，先提示
        void AfterTalk(NpcInfo info)
        {
            if (!string.IsNullOrEmpty(info.shop)) toast.Show("商店界面待移植：" + info.shop);
            else if (!string.IsNullOrEmpty(info.act)) toast.Show("功能待移植：" + info.act);
            else if (info.rest) toast.Show(string.IsNullOrEmpty(info.restMsg) ? "歇了一会儿，气血法力回满" : info.restMsg);
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
            if (battle != null) { toast.Tick(dt); return; }                // 战斗中：地图那边整个停住
            if (map == null) { toast.Tick(dt); return; }                // 还在登录 / 进图，场景没建好
            HandleInput();
            StepPlayer(dt); StepFox(dt);
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
            a.Tag.SetVisible(vis);
            if (vis) a.Tag.Root.position = new Vector3(sp.x, sp.y, 0f);
        }
    }
}
