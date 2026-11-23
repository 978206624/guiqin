using System;
using GuiQin.Inventory;
using GuiQin.Net;
using GuiQin.Quest;
using GuiQin.Pets;
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
            var bg = WebUi.Panel("dialog", parent); root = bg.gameObject;
            WebUi.At(bg.rectTransform, 20, 986, 680, 172);
            face = Ui.Box("face", bg.transform, Color.white); face.preserveAspect = true; face.raycastTarget = false;
            WebUi.At(face.rectTransform, 14, 14, 118, 118);
            nameT = Ui.Label("name", bg.transform, "", 30, new Color(1f, 0.88f, 0.4f), TextAnchor.UpperLeft);
            WebUi.Title(nameT);
            WebUi.At(nameT.rectTransform, 150, 10, 504, 40);
            body = Ui.Label("text", bg.transform, "", 19, WebUi.Text, TextAnchor.UpperLeft);
            WebUi.At(body.rectTransform, 150, 54, 512, 89);
            var hint = Ui.Label("hint", bg.transform, "点击继续 ▶", 13, WebUi.Muted, TextAnchor.MiddleRight);
            WebUi.At(hint.rectTransform, 525, 145, 139, 19);
            body.lineSpacing = 1.15f;
            root.SetActive(false);
        }

        public void Open(string who, Color whoColor, Sprite head, string text, Action onClose)
        {
            nameT.text = who; nameT.color = whoColor;
            face.sprite = head; face.enabled = head != null;
            full = text ?? ""; shown = 0; body.text = ""; after = onClose; root.SetActive(true);
        }

        public void Close() { root.SetActive(false); after = null; }          // 强行收起（掉线 / 被踢），不触发后续回调

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
            var bg = Ui.Box("toast", parent, Color.clear);
            Ui.Anchor(bg.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-300, 318), new Vector2(300, 350));
            g = bg.gameObject.AddComponent<CanvasGroup>(); g.alpha = 0; g.blocksRaycasts = false; g.interactable = false;
            t = Ui.Label("t", bg.transform, "", 20, WebUi.Amber, TextAnchor.MiddleCenter);
            WebUi.Edge(t);
            Ui.Anchor(t.rectTransform, Vector2.zero, Vector2.one, new Vector2(12, 0), new Vector2(-12, 0));
        }

        public void Show(string s) { t.text = s; left = 1.8f; g.alpha = 1; }
        public void Tick(float dt) { if (left > 0) { left -= dt; g.alpha = Mathf.Clamp01(left / 0.4f); } }
    }

    // 左上角色状态与右上小地图沿用 HTML 舞台的位置。
    public class HudUi
    {
        readonly RectTransform root, town, hpFill, mpFill, petHpFill, petMpFill, expFill, miniLayer, miniDots, miniPlayer;
        const float MiniDiameter = 110f, MinMapScale = 0.09f;
        Vector2 miniSize, mapSize;
        float miniScale, mapCoordScale;
        Vector2Int lastMapCoord;
        readonly CanvasGroup hudInput;
        readonly RectTransform systemLog;
        readonly GameObject unreadDot, routeStatus;
        readonly Text place, mapCoord, name, level, petLevel;
        readonly Image face, petFace, mini, weather;
        readonly MapRouteGraphic miniRoute;
        readonly Sprite[] weatherIcons = new Sprite[4];

        public HudUi(Transform parent, Action onMenu, Action onWorld, Action onBag,
            Action onRole, Action onPet, Action onScene, Action<string> onKey)
        {
            root = Ui.Rect("hud", parent);
            Ui.Anchor(root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            hudInput = root.gameObject.AddComponent<CanvasGroup>();
            var roleVitals = VitalsPanel("roleVitals", 95, 26);
            var petVitals = VitalsPanel("petVitals", 275, 30);
            name = Ui.Label("name", roleVitals, "", 14, WebUi.C("#ffe7b6"), TextAnchor.MiddleLeft);
            name.fontStyle = FontStyle.Bold;
            name.resizeTextForBestFit = true; name.resizeTextMinSize = 10; name.resizeTextMaxSize = 14;
            name.verticalOverflow = VerticalWrapMode.Truncate;
            WebUi.At(name.rectTransform, 7, 6, 72, 19);
            hpFill = StatusBar("hp", roleVitals, 25, "#ffa994", "#a02d25");
            mpFill = StatusBar("mp", roleVitals, 40, "#b8ebff", "#286599");
            petHpFill = StatusBar("hp", petVitals, 22, "#ffa994", "#a02d25");
            petMpFill = StatusBar("mp", petVitals, 37, "#b8ebff", "#286599");
            face = Portrait("role", 12, 18, onRole, out level);
            petFace = Portrait("pet", 192, 22, onPet, out petLevel);
            var expBack = WebUi.Gradient("exp", root, WebUi.C("#1a0f14"), WebUi.C("#0a0612"), 4);
            WebUi.At(expBack.rectTransform, 0, 1272, 720, 8);
            expFill = BarFill(expBack.transform, "#f6e66a", "#8fe06a");

            town = Ui.Rect("TownControls", root);
            Ui.Anchor(town, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var mapButton = Ui.Btn("mini", town, "", 14, Color.clear, () => onScene());
            mapButton.image.sprite = WebUi.Circle();
            mapButton.image.color = WebUi.Gold;
            WebUi.At((RectTransform)mapButton.transform, 580, 16, 124, 124);
            var clip = WebUi.RoundMask("clip", mapButton.transform, geometric: true);
            WebUi.At(clip.rectTransform, 7, 7, MiniDiameter, MiniDiameter);
            var miniBack = Ui.Box("mapBack", clip.transform, WebUi.C("#224433"));
            miniBack.raycastTarget = false;
            WebUi.At(miniBack.rectTransform, 0, 0, 110, 110);
            miniLayer = Ui.Rect("mapLayer", clip.transform);
            mini = Ui.Box("mapImage", miniLayer, Color.white);
            mini.raycastTarget = false;
            Ui.Anchor(mini.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            miniRoute = MapRouteGraphic.Create(miniLayer);
            miniDots = Ui.Rect("dots", miniLayer);
            Ui.Anchor(miniDots, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var marker = Ui.Label("playerMarker", clip.transform, "▲", 14, Color.white, TextAnchor.MiddleCenter);
            miniPlayer = marker.rectTransform;
            WebUi.At(miniPlayer, 48, 46, 14, 14);
            WebUi.Edge(marker);
            TownLabel("north", "北", 13, WebUi.Light, 630, 8, 24, 24);
            var coordPlate = WebUi.Gradient("coordPlate", town, WebUi.C("#8a5a14e8"), WebUi.C("#51301cea"), 4);
            coordPlate.raycastTarget = false;
            WebUi.At(coordPlate.rectTransform, 437, 49, 124, 29);
            mapCoord = Ui.Label("mapCoord", coordPlate.transform, "", 16, WebUi.Light, TextAnchor.MiddleCenter);
            WebUi.At(mapCoord.rectTransform, 0, 3, 124, 24);
            place = MapButton("place", "", 430, 18, 138, onWorld);
            var routing = Ui.Label("routeStatus", town, "自动寻路中", 18, WebUi.Amber, TextAnchor.MiddleRight);
            WebUi.At(routing.rectTransform, 430, 80, 138, 25);
            WebUi.Edge(routing);
            routing.raycastTarget = false;
            routeStatus = routing.gameObject;
            routeStatus.SetActive(false);
            var weatherBadge = WebUi.Panel("weatherBadge", town);
            weatherBadge.raycastTarget = false;
            WebUi.Dress(weatherBadge, WebUi.C("#fbf0cc"), WebUi.C("#dcc48a"), WebUi.C("#8a5a14"), 4);
            WebUi.At(weatherBadge.rectTransform, 389, 19, 34, 34);
            weather = Ui.Box("weather", weatherBadge.transform, Color.white);
            weather.raycastTarget = false;
            WebUi.At(weather.rectTransform, 4, 4, 26, 26);
            // 图集沿用网页版图样，顺序为晴、雨、雷暴、雪。
            var weatherAtlas = Resources.Load<Texture2D>("GuiQin/Art/hud_weather");
            if (weatherAtlas != null)
            {
                int size = weatherAtlas.height;
                for (int i = 0; i < weatherIcons.Length; i++)
                    weatherIcons[i] = Sprite.Create(weatherAtlas, new Rect(i * size, 0, size, size), Vector2.one * 0.5f);
            }
            SetWeather("clear");
            // 右侧一列快捷入口（和网页版同样的顺序、位置）：活动 福利 商城 竞技 背包 帮派 好友 设置
            Shortcut("act", "活动", "icon_act", 219, () => onKey("act"));
            Shortcut("gift", "福利", "icon_gift", 295, () => onKey("gift"));
            giftDot = Ui.Box("dot", town, WebUi.C("#ff3b30")); giftDot.sprite = WebUi.Circle(); giftDot.raycastTarget = false; WebUi.At(giftDot.rectTransform, 688, 297, 14, 14);
            Shortcut("mall", "商城", "icon_mall", 371, () => onKey("mall"));
            Shortcut("rank", "竞技", "icon_rank", 447, () => onKey("rank"));
            Shortcut("bag", "背包", "icon_bag", 523, onBag);
            Shortcut("guild", "帮派", "icon_guild", 599, () => onKey("guild"));
            Shortcut("friend", "好友", "icon_friend", 675, () => onKey("friend"));
            Shortcut("settings", "设置", "icon_gear", 751, onMenu);
            // 右下角：自动挂机（大）、回气丹和金创药快捷键
            autoBtn = RoundIcon("auto", "icon_auto", 609, 1143, 86, () => onKey("auto"));
            RoundIcon("mpPotion", "item_mp_potion", 575, 1079, 56, () => onKey("mpPotion"));
            RoundIcon("potion", "icon_potion", 639, 1079, 56, () => onKey("potion"));

            var log = WebUi.Panel("systemMessages", town);
            var logBtn = log.gameObject.AddComponent<Button>(); logBtn.targetGraphic = log; logBtn.transition = Selectable.Transition.None; logBtn.onClick.AddListener(() => onKey("chat"));       // 点这块打开聊天
            WebUi.At(log.rectTransform, 10, 1146, 446, 122);
            systemLog = Ui.Rect("log", log.transform);
            systemLog.gameObject.AddComponent<RectMask2D>();
            WebUi.At(systemLog, 12, 8, 420, 72);
            var inputHint = WebUi.Gradient("chatHint", log.transform, WebUi.C("#ffffff27"), WebUi.C("#ffffff18"), 12);
            WebUi.At(inputHint.rectTransform, 12, 86, 422, 26);
            var hint = Ui.Label("hint", inputHint.transform, "点这里聊天…", 13, WebUi.Muted, TextAnchor.MiddleLeft);
            WebUi.At(hint.rectTransform, 16, 0, 390, 26);
            var border = Ui.Box("chatDot", log.transform, Color.white);
            border.sprite = WebUi.Circle(); border.raycastTarget = false;
            WebUi.At(border.rectTransform, 424, 8, 14, 14);
            var dot = Ui.Box("dot", border.transform, WebUi.C("#e8362e"));
            dot.sprite = WebUi.Circle(); dot.raycastTarget = false;
            WebUi.At(dot.rectTransform, 2, 2, 10, 10);
            unreadDot = border.gameObject;
            unreadDot.SetActive(false);
        }

        RectTransform VitalsPanel(string id, float x, float y)
        {
            var panel = Ui.Box(id, root, Color.white);
            panel.raycastTarget = false;
            WebUi.Dress(panel, WebUi.C("#705038"), WebUi.C("#302620"), WebUi.C("#b9905e"), 11);
            WebUi.At(panel.rectTransform, x, y, 87, 69);
            return panel.rectTransform;
        }

        Image Portrait(string id, float x, float y, Action open, out Text levelLabel)
        {
            var button = Ui.Btn(id, root, "", 14, Color.clear, () => open());
            WebUi.Dress(button.image, WebUi.C("#f4e2b5"), WebUi.C("#94613a"), WebUi.C("#f0d8a2"), 7);
            WebUi.At((RectTransform)button.transform, x, y, 86, 86);
            var clip = Ui.Rect("clip", button.transform);
            clip.gameObject.AddComponent<RectMask2D>();
            WebUi.At(clip, 4, 4, 78, 78);
            var backing = WebUi.Gradient("backing", clip, WebUi.C("#665142"), WebUi.C("#2e2725"), 3);
            Ui.Anchor(backing.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var image = Ui.Box("portrait", clip, Color.white);
            image.raycastTarget = false;
            image.preserveAspect = true;
            Ui.Anchor(image.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var frame = Ui.Box("innerFrame", button.transform, Color.white);
            frame.raycastTarget = false;
            WebUi.Dress(frame, Color.clear, Color.clear, WebUi.C("#f5dbab85"), 4);
            WebUi.At(frame.rectTransform, 3, 3, 80, 80);
            var badge = Ui.Box("levelBadge", button.transform, Color.white);
            badge.raycastTarget = false;
            WebUi.Dress(badge, WebUi.C("#9f4b36"), WebUi.C("#5e281f"), WebUi.C("#e5bd77"), 4);
            WebUi.At(badge.rectTransform, 57, 62, 25, 20);
            levelLabel = Ui.Label("level", badge.transform, "", 13, WebUi.C("#fff0ca"), TextAnchor.MiddleCenter);
            levelLabel.fontStyle = FontStyle.Bold;
            levelLabel.resizeTextForBestFit = true; levelLabel.resizeTextMinSize = 9; levelLabel.resizeTextMaxSize = 13;
            WebUi.At(levelLabel.rectTransform, 0, 0, 25, 20);
            return image;
        }

        Text TownLabel(string id, string value, int size, Color color, float x, float y, float w, float h)
        {
            var label = Ui.Label(id, town, value, size, color, TextAnchor.MiddleCenter);
            WebUi.At(label.rectTransform, x, y, w, h);
            return label;
        }

        Text MapButton(string id, string value, float x, float y, float w, Action open)
        {
            var button = Ui.Btn(id, town, value, 20, Color.clear, () => open());
            WebUi.Dress(button.image, WebUi.C("#fff3d8"), WebUi.C("#e7c493"), WebUi.C("#ad7b41"), 4);
            WebUi.At((RectTransform)button.transform, x, y, w, 34);
            var label = button.GetComponentInChildren<Text>();
            label.fontStyle = FontStyle.Bold;
            label.color = WebUi.C("#4a2a10");
            label.resizeTextForBestFit = true; label.resizeTextMinSize = 12; label.resizeTextMaxSize = 20;
            return label;
        }

        Image giftDot, autoBtn;
        public void SetGiftDot(bool on) { if (giftDot != null) giftDot.enabled = on; }
        public void SetAuto(bool on) { if (autoBtn != null) autoBtn.color = on ? WebUi.Amber : WebUi.Gold; }

        Image RoundIcon(string id, string icon, float x, float y, float size, Action click)
        {
            var button = Ui.Btn(id, town, "", 14, Color.clear, () => click());
            WebUi.At((RectTransform)button.transform, x, y, size, size);
            var ring = Ui.Box("ring", button.transform, WebUi.Gold); ring.sprite = WebUi.Circle(); ring.raycastTarget = false;
            WebUi.At(ring.rectTransform, 0, 0, size, size);
            var img = WebUi.Icon("icon", button.transform, icon); WebUi.At(img.rectTransform, 4, 4, size - 8, size - 8);
            return ring;
        }

        void Shortcut(string id, string title, string icon, float y, Action open)
        {
            var button = Ui.Btn(id, town, title, 14, Color.clear, () => open());
            WebUi.At((RectTransform)button.transform, 648, y, 54, 72);
            WebUi.At(WebUi.Icon("icon", button.transform, icon).rectTransform, 0, 0, 54, 54);
            var caption = button.GetComponentInChildren<Text>();
            WebUi.At(caption.rectTransform, 0, 52, 54, 20);
            WebUi.Edge(caption);
        }

        static RectTransform StatusBar(string id, Transform parent, float y, string top, string bottom)
        {
            var back = Ui.Box(id, parent, Color.white);
            back.raycastTarget = false;
            WebUi.Dress(back, WebUi.C("#241712"), WebUi.C("#694a32"), WebUi.C("#9c784d"), 4);
            WebUi.At(back.rectTransform, 7, y, 72, 12);
            var inset = Ui.Rect("track", back.transform);
            WebUi.At(inset, 1, 1, 70, 10);
            return BarFill(inset, top, bottom);
        }

        static RectTransform BarFill(Transform parent, string top, string bottom)
        {
            var fill = WebUi.Gradient("fill", parent, WebUi.C(top), WebUi.C(bottom), 3);
            Ui.Anchor(fill.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return fill.rectTransform;
        }

        static void SetBar(RectTransform fill, float value)
        {
            fill.anchorMax = new Vector2(Mathf.Clamp01(value), 1);
        }

        public void SetPet(string headKey, string fallbackKey)      // HUD 宠物按钮：只显示头部（ph_ 开头），没有就用整图
        {
            string k = Art.Has(headKey) ? headKey : fallbackKey; petFace.sprite = Art.One(k, true); petFace.enabled = petFace.sprite != null;
        }

        public void SetRole(RoleState role, StatBlock stats)
        {
            name.text = role.name;
            petLevel.text = role.lv.ToString();
            string petId = PetService.ActiveId(role);
            var petStats = PetService.Stats(role, petId);
            // 地图显示已存档的法力，战斗中由服务端快照和回合事件刷新。
            SetPetVitals(petStats.maxHp, petStats.maxHp, PetService.CurrentMana(role, petId), petStats.maxMp);
            level.text = role.lv.ToString();
            string portrait = "head_" + role.cls;
            face.sprite = Art.One(Art.Has(portrait) ? portrait : "head_player", true);
            int hp = role.hp < 0 ? stats.maxHp : role.hp;
            int mp = role.mp < 0 ? stats.maxMp : role.mp;
            SetVitals(hp, stats.maxHp, mp, stats.maxMp);
            SetBar(expFill, role.exp / (float)Mathf.Max(1, Rewards.ExpNeed(role.lv)));
        }

        public void SetVitals(int hp, int maxHp, int mp, int maxMp)
        {
            SetBar(hpFill, hp / (float)Mathf.Max(1, maxHp));
            SetBar(mpFill, mp / (float)Mathf.Max(1, maxMp));
        }

        public void SetPetVitals(int hp, int maxHp, int mp, int maxMp)
        {
            SetBar(petHpFill, hp / (float)Mathf.Max(1, maxHp));
            SetBar(petMpFill, mp / (float)Mathf.Max(1, maxMp));
        }

        public void SetChatPreview(System.Collections.Generic.IReadOnlyList<GuiQin.Chat.ChatMsg> lines, bool unread)
        {
            ChatUi.DrawMini(systemLog, lines);
            unreadDot.SetActive(unread);
        }

        public void SetPlace(MapRuntime map)
        {
            place.text = map.Def.name;
            mapCoordScale = map.Sc;
            lastMapCoord = new Vector2Int(-1, -1);
            mini.sprite = Art.One(Art.Has(map.Def.mini) ? map.Def.mini : map.Def.img, true);
            mapSize = new Vector2(map.W, map.H);
            miniScale = Mathf.Max(MinMapScale, MiniDiameter / Mathf.Min(map.W, map.H));
            miniSize = mapSize * miniScale;
        }

        public void SetMapActors(System.Collections.Generic.IEnumerable<Actor> actors)
        {
            for (int i = miniDots.childCount - 1; i >= 0; i--)
            {
                var old = miniDots.GetChild(i).gameObject;
                old.SetActive(false);
                UnityEngine.Object.Destroy(old);
            }
            foreach (var actor in actors)
            {
                if (actor.Hidden) continue;
                var color = actor.Marked ? Color.white : actor.Kind == ActorKind.Portal ? WebUi.Sky :
                    actor.Kind == ActorKind.Foe ? WebUi.C("#ff6a5a") : WebUi.Amber;
                var dot = Ui.Box("dot", miniDots, color);
                dot.sprite = WebUi.Circle();
                dot.raycastTarget = false;
                WebUi.At(dot.rectTransform, actor.Pos.x * miniScale - 3, actor.Pos.y * miniScale - 3, 6, 6);
            }
        }

        public void SetRoute(Vector2 player, System.Collections.Generic.IReadOnlyList<Vector2> route)
        {
            miniRoute.Show(player, route, mapSize);
            bool routing = route != null && route.Count > 0;
            if (routeStatus.activeSelf != routing) routeStatus.SetActive(routing);
        }

        public void SetPosition(Vector2 p)
        {
            // 舞台像素换回地图配置坐标，跨过一格时才刷新文字。
            if (mapCoordScale > 0f)
            {
                var coordinate = Vector2Int.FloorToInt(p / mapCoordScale);
                if (coordinate != lastMapCoord)
                {
                    lastMapCoord = coordinate;
                    mapCoord.text = $"({coordinate.x}, {coordinate.y})";
                }
            }
            float radius = MiniDiameter * 0.5f;
            var center = Vector2.one * radius;
            var point = p * miniScale;
            // 靠近地图边缘时停止滚动，角色箭头在圆内继续移动。
            var offset = new Vector2(
                Mathf.Clamp(radius - point.x, Mathf.Min(0f, MiniDiameter - miniSize.x), 0f),
                Mathf.Clamp(radius - point.y, Mathf.Min(0f, MiniDiameter - miniSize.y), 0f));
            WebUi.At(miniLayer, offset.x, offset.y, miniSize.x, miniSize.y);
            var marker = center + Vector2.ClampMagnitude(point + offset - center, radius - 8f);
            WebUi.At(miniPlayer, marker.x - 7f, marker.y - 7f, 14, 14);
        }
        public void SetVisible(bool visible)
        {
            root.gameObject.SetActive(visible);
            hudInput.blocksRaycasts = visible;
            town.gameObject.SetActive(visible);
        }

        public void SetBattleVisible()
        {
            root.gameObject.SetActive(true);
            hudInput.blocksRaycasts = false;
            town.gameObject.SetActive(false);
        }
        public void SetWeather(string kind)
        {
            int index = kind == "rain" ? 1 : kind == "storm" ? 2 : kind == "snow" ? 3 : 0;
            weather.sprite = weatherIcons[index];
            weather.enabled = weather.sprite != null;
        }
    }

    public class QuestUi
    {
        readonly GameObject root;
        readonly Text title, goal, progress, toggleLabel;
        readonly RectTransform frame, details;
        bool collapsed;

        public QuestUi(Transform parent, Action onOpenDetails)
        {
            var panel = WebUi.Panel("quest", parent); root = panel.gameObject;
            frame = panel.rectTransform;
            WebUi.At(frame, 10, 136, 214, 146);
            var strip = WebUi.Gradient("header", panel.transform, WebUi.C("#8a5a14"), WebUi.C("#5a3a0c"), 5);
            WebUi.At(strip.rectTransform, 1, 1, 212, 28);
            var heading = Ui.Label("heading", panel.transform, "任务", 15, WebUi.Light, TextAnchor.MiddleCenter);
            WebUi.At(heading.rectTransform, 0, 0, 214, 29);
            var toggle = Ui.Btn("toggle", panel.transform, "▾", 18, Color.clear, Toggle);
            WebUi.At((RectTransform)toggle.transform, 174, 0, 40, 30);
            toggleLabel = toggle.GetComponentInChildren<Text>();
            details = Ui.Rect("details", panel.transform);
            WebUi.At(details, 0, 0, 214, 146);
            var b = Ui.Btn("current", details, "", 15, Color.clear, () => onOpenDetails());
            WebUi.Dress(b.image, WebUi.C("#e2b04a35"), WebUi.C("#e2b04a18"), Color.clear, 4);
            WebUi.At((RectTransform)b.transform, 5, 34, 204, 60);
            title = Ui.Label("title", b.transform, "", 15, WebUi.Light, TextAnchor.MiddleLeft);
            title.horizontalOverflow = HorizontalWrapMode.Wrap; title.verticalOverflow = VerticalWrapMode.Truncate;
            WebUi.At(title.rectTransform, 8, 3, 188, 23);
            goal = Ui.Label("goal", b.transform, "", 13, WebUi.Jade, TextAnchor.UpperLeft);
            goal.verticalOverflow = VerticalWrapMode.Truncate;
            WebUi.At(goal.rectTransform, 8, 27, 188, 32);
            progress = Ui.Label("progress", details, "", 13, WebUi.Muted, TextAnchor.UpperLeft);
            WebUi.At(progress.rectTransform, 13, 101, 190, 34);
        }

        void Toggle()
        {
            collapsed = !collapsed;
            details.gameObject.SetActive(!collapsed);
            WebUi.At(frame, 10, 136, 214, collapsed ? 30 : 146);
            toggleLabel.text = collapsed ? "▸" : "▾";
        }

        public void Set(string label, string target, string chapter)
        {
            title.text = "<color=#ffd56a>[主线]</color> " + label;
            goal.text = target;
            progress.text = chapter;
        }
        public void SetVisible(bool visible) => root.SetActive(visible);
    }
}
