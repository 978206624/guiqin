using System;
using GuiQin.Inventory;
using GuiQin.Net;
using GuiQin.Quest;
using GuiQin.Sect;
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
        readonly RectTransform root, town, hpFill, mpFill, expFill, miniLayer, miniDots;
        const float MiniScale = 0.09f;
        Vector2 miniSize;
        readonly CanvasGroup hudInput;
        readonly RectTransform systemLog;
        readonly GameObject unreadDot;
        readonly Text place, weather, name, level, sect, power, coin, hpText, mpText;
        readonly Image face, mini;

        public HudUi(Transform parent, Action onMenu, Action onWeather, Action onWorld, Action onBag,
            Action onRole, Action onPet, Action onScene, Action<string> onKey)
        {
            root = Ui.Rect("hud", parent);
            Ui.Anchor(root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            hudInput = root.gameObject.AddComponent<CanvasGroup>();
            var plate = WebUi.Panel("plate", root);
            plate.raycastTarget = false;
            WebUi.At(plate.rectTransform, 56, 24, 280, 92);
            face = Portrait("role", 10, 20, 96, onRole);
            petFace = Portrait("pet", 335, 48, 62, onPet);
            petFace.sprite = Art.One("ph_ss_bs20", true);
            var badge = WebUi.Gradient("levelBadge", root, WebUi.C("#a12a20"), WebUi.C("#761913"), 12);
            WebUi.At(badge.rectTransform, 76, 88, 30, 30);
            level = Label("level", "", 14, WebUi.Light, 76, 88, 30, 30, TextAnchor.MiddleCenter);
            name = Label("name", "", 19, WebUi.Light, 118, 28, 130, 23);
            name.resizeTextForBestFit = true; name.resizeTextMinSize = 12; name.resizeTextMaxSize = 19;
            sect = Label("sect", "", 13, WebUi.Sky, 250, 28, 66, 23);
            hpFill = Bar("hp", 116, 60, 190, 14, "#ff8a78", "#c0241c", out hpText);
            mpFill = Bar("mp", 116, 78, 190, 14, "#84ccff", "#2060c0", out mpText);
            power = Label("power", "", 14, WebUi.Amber, 118, 94, 195, 20);
            expFill = Bar("exp", 0, 1272, 720, 8, "#f6e66a", "#8fe06a", out _);

            town = Ui.Rect("TownControls", root);
            Ui.Anchor(town, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var mapButton = Ui.Btn("mini", town, "", 14, Color.clear, () => onScene());
            mapButton.image.sprite = WebUi.Circle();
            mapButton.image.color = WebUi.Gold;
            WebUi.At((RectTransform)mapButton.transform, 580, 16, 124, 124);
            var clip = WebUi.RoundMask("clip", mapButton.transform);
            WebUi.At(clip.rectTransform, 7, 7, 110, 110);
            var miniBack = Ui.Box("mapBack", clip.transform, WebUi.C("#224433"));
            miniBack.raycastTarget = false;
            WebUi.At(miniBack.rectTransform, 0, 0, 110, 110);
            miniLayer = Ui.Rect("mapLayer", clip.transform);
            mini = Ui.Box("mapImage", miniLayer, Color.white);
            mini.raycastTarget = false;
            Ui.Anchor(mini.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            miniDots = Ui.Rect("dots", miniLayer);
            Ui.Anchor(miniDots, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var marker = Ui.Label("playerMarker", clip.transform, "▲", 14, Color.white, TextAnchor.MiddleCenter);
            WebUi.At(marker.rectTransform, 48, 46, 14, 14);
            WebUi.Edge(marker);
            TownLabel("north", "北", 13, WebUi.Light, 630, 8, 24, 24);
            place = MapButton("place", "", 566, 152, 138, onWorld);
            weather = MapButton("weather", "晴", 520, 152, 40, onWeather);
            coin = TownLabel("coin", "", 14, WebUi.Amber, 566, 188, 138, 24);
            WebUi.Edge(coin);
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
            // 右下角：自动挂机（大）、坐骑、回气丹和金创药快捷键
            autoBtn = RoundIcon("auto", "icon_auto", 609, 1143, 86, () => onKey("auto"));
            RoundIcon("ride", "icon_ride", 534, 1186, 56, () => onKey("ride"));
            RoundIcon("mpPotion", "item_mp_potion", 502, 1116, 56, () => onKey("mpPotion"));
            RoundIcon("potion", "icon_potion", 566, 1116, 56, () => onKey("potion"));

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

        Image Portrait(string id, float x, float y, float size, Action open)
        {
            var button = Ui.Btn(id, root, "", 14, Color.clear, () => open());
            var ring = Ui.Box("ring", button.transform, WebUi.Gold);
            ring.sprite = WebUi.Circle(); ring.raycastTarget = false;
            WebUi.At(ring.rectTransform, 0, 0, size, size);
            WebUi.At((RectTransform)button.transform, x, y, size, size);
            var clip = WebUi.RoundMask("clip", button.transform);
            WebUi.At(clip.rectTransform, 5, 5, size - 10, size - 10);
            var image = Ui.Box("portrait", clip.transform, Color.white);
            image.raycastTarget = false;
            Ui.Anchor(image.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            return image;
        }

        Text Label(string id, string value, int size, Color color, float x, float y, float w, float h,
            TextAnchor align = TextAnchor.MiddleLeft)
        {
            var label = Ui.Label(id, root, value, size, color, align);
            WebUi.At(label.rectTransform, x, y, w, h);
            return label;
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
            WebUi.Dress(button.image, WebUi.C("#fbf0cc"), WebUi.C("#dcc48a"), WebUi.C("#8a5a14"), 4);
            WebUi.At((RectTransform)button.transform, x, y, w, 34);
            var label = WebUi.Title(button.GetComponentInChildren<Text>());
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

        RectTransform Bar(string id, float x, float y, float w, float h, string top, string bottom, out Text label)
        {
            var back = WebUi.Gradient(id, root, WebUi.C("#1a0f14"), WebUi.C("#0a0612"), 4);
            WebUi.At(back.rectTransform, x, y, w, h);
            var fill = WebUi.Gradient("fill", back.transform, WebUi.C(top), WebUi.C(bottom), 4);
            Ui.Anchor(fill.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            label = id == "exp" ? null : Label(id + "Value", "", 11, Color.white, x, y, w, h, TextAnchor.MiddleCenter);
            if (label != null) WebUi.Edge(label);
            return fill.rectTransform;
        }

        static void SetBar(RectTransform fill, float value)
        {
            fill.anchorMax = new Vector2(Mathf.Clamp01(value), 1);
        }

        Image petFace;
        public void SetPet(string headKey, string fallbackKey)      // HUD 宠物按钮：只显示头部（ph_ 开头），没有就用整图
        {
            string k = Art.Has(headKey) ? headKey : fallbackKey; petFace.sprite = Art.One(k, true); petFace.enabled = petFace.sprite != null;
        }

        public void SetRole(RoleState role, StatBlock stats)
        {
            name.text = role.name;
            sect.text = SectService.ClsName(role.cls);
            level.text = role.lv.ToString();
            string portrait = "head_" + role.cls;
            face.sprite = Art.One(Art.Has(portrait) ? portrait : "head_player", true);
            int hp = role.hp < 0 ? stats.maxHp : role.hp;
            int mp = role.mp < 0 ? stats.maxMp : role.mp;
            SetVitals(hp, stats.maxHp, mp, stats.maxMp);
            SetBar(expFill, role.exp / (float)Mathf.Max(1, Rewards.ExpNeed(role.lv)));
            power.text = "战力 " + Power(stats).ToString("N0");
            coin.text = "银两 " + role.coin.ToString("N0");
        }

        public void SetVitals(int hp, int maxHp, int mp, int maxMp)
        {
            hpText.text = hp + "/" + maxHp;
            mpText.text = mp + "/" + maxMp;
            SetBar(hpFill, hp / (float)Mathf.Max(1, maxHp));
            SetBar(mpFill, mp / (float)Mathf.Max(1, maxMp));
        }

        public void SetChatPreview(System.Collections.Generic.IReadOnlyList<GuiQin.Chat.ChatMsg> lines, bool unread)
        {
            ChatUi.DrawMini(systemLog, lines);
            unreadDot.SetActive(unread);
        }

        public static int Power(StatBlock s) => Mathf.RoundToInt(s.atk * 6 + s.matk * 4 + s.def * 5 + s.mdef * 3
            + s.maxHp / 2f + s.maxMp / 4f + s.spd * 3 + s.crit * 12 + s.dodge * 12);

        public void SetPlace(MapRuntime map)
        {
            place.text = map.Def.name;
            mini.sprite = Art.One(Art.Has(map.Def.mini) ? map.Def.mini : map.Def.img, true);
            miniSize = new Vector2(map.W, map.H) * MiniScale;
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
                WebUi.At(dot.rectTransform, actor.Pos.x * MiniScale - 3, actor.Pos.y * MiniScale - 3, 6, 6);
            }
        }

        public void SetPosition(Vector2 p)
        {
            WebUi.At(miniLayer, 55 - p.x * MiniScale, 55 - p.y * MiniScale, miniSize.x, miniSize.y);
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
        public void SetWeather(string value) => weather.text = value;
    }

    public class QuestUi
    {
        readonly GameObject root;
        readonly Text title, goal, progress;

        public QuestUi(Transform parent, Action onGo)
        {
            var panel = WebUi.Panel("quest", parent); root = panel.gameObject;
            WebUi.At(panel.rectTransform, 10, 136, 214, 146);
            var strip = WebUi.Gradient("header", panel.transform, WebUi.C("#8a5a14"), WebUi.C("#5a3a0c"), 5);
            WebUi.At(strip.rectTransform, 1, 1, 212, 28);
            var heading = Ui.Label("heading", panel.transform, "任务", 15, WebUi.Light, TextAnchor.MiddleCenter);
            WebUi.At(heading.rectTransform, 0, 0, 214, 29);
            var b = Ui.Btn("current", panel.transform, "", 15, Color.clear, () => onGo());
            WebUi.Dress(b.image, WebUi.C("#e2b04a35"), WebUi.C("#e2b04a18"), Color.clear, 4);
            WebUi.At((RectTransform)b.transform, 5, 34, 204, 60);
            title = Ui.Label("title", b.transform, "", 15, WebUi.Light, TextAnchor.MiddleLeft);
            title.horizontalOverflow = HorizontalWrapMode.Wrap; title.verticalOverflow = VerticalWrapMode.Truncate;
            WebUi.At(title.rectTransform, 8, 3, 188, 23);
            goal = Ui.Label("goal", b.transform, "", 13, WebUi.Jade, TextAnchor.UpperLeft);
            goal.verticalOverflow = VerticalWrapMode.Truncate;
            WebUi.At(goal.rectTransform, 8, 27, 188, 32);
            progress = Ui.Label("progress", panel.transform, "", 13, WebUi.Muted, TextAnchor.UpperLeft);
            WebUi.At(progress.rectTransform, 13, 101, 190, 34);
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
