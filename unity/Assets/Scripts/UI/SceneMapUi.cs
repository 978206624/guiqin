using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace GuiQin
{
    internal sealed class SceneMapUi
    {
        readonly GameObject root;
        readonly Text title, subtitle, playerMarker;
        readonly RectTransform frame, markers;
        readonly Image picture;
        readonly MapRouteGraphic routeGraphic;
        Vector2 mapSize;

        public bool Visible => root.activeSelf;

        public SceneMapUi(Transform parent, Action onWorld)
        {
            var bg = Ui.Box("sceneMap", parent, WebUi.C("#11091af5"));
            root = bg.gameObject;
            Ui.Anchor(bg.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            title = WebUi.Title(Ui.Label("title", root.transform, "", 42, WebUi.Amber, TextAnchor.MiddleCenter));
            WebUi.At(title.rectTransform, 0, 32, 720, 60);
            subtitle = Ui.Label("subtitle", root.transform, "", 17, WebUi.Light, TextAnchor.MiddleCenter);
            WebUi.At(subtitle.rectTransform, 24, 100, 672, 42);
            frame = Ui.Rect("map", root.transform);
            picture = Ui.Box("image", frame, Color.white);
            Ui.Anchor(picture.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            routeGraphic = MapRouteGraphic.Create(frame);
            markers = Ui.Rect("markers", frame);
            Ui.Anchor(markers, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            playerMarker = Ui.Label("player", frame, "▲", 18, Color.white, TextAnchor.MiddleCenter);
            WebUi.Edge(playerMarker);
            playerMarker.raycastTarget = false;
            var legend = Ui.Label("legend", root.transform,
                "<color=#ffd24a>●</color> 人物　<color=#ff6a5a>●</color> 敌人　<color=#5ec8ff>◆</color> 出口　▲ 当前位置　<color=#ffd24a>◆</color> 目的地",
                18, WebUi.Text, TextAnchor.MiddleCenter);
            WebUi.At(legend.rectTransform, 20, 1060, 680, 45);
            var world = Ui.Btn("world", root.transform, "天下舆图", 22, WebUi.Purple, () => { Close(); onWorld(); });
            WebUi.At((RectTransform)world.transform, 165, 1168, 180, 50);
            var close = Ui.Btn("back", root.transform, "返回", 22, WebUi.Red, Close);
            WebUi.At((RectTransform)close.transform, 375, 1168, 180, 50);
            Close();
        }

        public void Open(MapRuntime map, Vector2 player, IEnumerable<Actor> actors, Action<Actor> onActor, Func<Vector2, bool> onGround)
        {
            title.text = map.Def.name;
            string instructions = "场景地图 · " + (map.Def.note ?? "") + "　点击地面或标记寻路";
            subtitle.text = instructions;
            mapSize = new Vector2(map.W, map.H);
            picture.sprite = Art.One(map.Def.img, true);
            float scale = Mathf.Min(676 / map.W, 880 / map.H);
            float w = map.W * scale, h = map.H * scale;
            WebUi.At(frame, (720 - w) * 0.5f, 150 + (880 - h) * 0.5f, w, h);
            var trigger = picture.GetComponent<EventTrigger>() ?? picture.gameObject.AddComponent<EventTrigger>();
            trigger.triggers.Clear();
            var click = new EventTrigger.Entry { eventID = EventTriggerType.PointerClick };
            click.callback.AddListener(e =>
            {
                var pointer = (PointerEventData)e;
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(frame, pointer.position,
                    pointer.pressEventCamera, out var p)) return;
                var rect = frame.rect;
                float x = Mathf.InverseLerp(rect.xMin, rect.xMax, p.x);
                float y = Mathf.InverseLerp(rect.yMax, rect.yMin, p.y);
                subtitle.text = onGround(new Vector2(x * map.W, y * map.H))
                    ? instructions : "无法前往这里，请选择其他位置";
            });
            trigger.triggers.Add(click);

            for (int i = markers.childCount - 1; i >= 0; i--)
            {
                var child = markers.GetChild(i).gameObject;
                child.SetActive(false);
                Object.Destroy(child);
            }
            foreach (var actor in actors)
            {
                if (actor.Hidden) continue;
                var color = actor.Kind == ActorKind.Portal ? WebUi.Sky :
                    actor.Kind == ActorKind.Foe ? WebUi.C("#ff6a5a") : WebUi.Amber;
                bool hasQuestMark = actor.QuestMark != GuiQin.Quest.QuestNpcMark.None;
                if (hasQuestMark) color = TagLabel.QuestColor(actor.QuestMark);
                var label = (hasQuestMark ? TagLabel.QuestSymbol(actor.QuestMark) + " " : "● ") + actor.DisplayName;
                Mark(map, actor.Pos, label, color, () => { Close(); onActor(actor); });
            }
            root.SetActive(true);
            SetRoute(player, null);
        }

        void Mark(MapRuntime map, Vector2 pos, string value, Color color, UnityAction onClick)
        {
            var button = Ui.Btn("marker", markers, value, 14, Color.clear, onClick);
            var label = button.GetComponentInChildren<Text>();
            var rect = (RectTransform)button.transform;
            label.color = color;
            WebUi.Edge(label);
            var point = new Vector2(Mathf.Clamp01(pos.x / map.W), 1 - Mathf.Clamp01(pos.y / map.H));
            Ui.Anchor(rect, point, point, new Vector2(-53, -12), new Vector2(53, 12));
        }

        public void SetRoute(Vector2 player, IReadOnlyList<Vector2> route)
        {
            routeGraphic.Show(player, route, mapSize);
            if (!Visible && route == null) return;
            MovePlayerMarker(player);
        }

        void MovePlayerMarker(Vector2 player)
        {
            var point = new Vector2(player.x / mapSize.x, 1f - player.y / mapSize.y);
            Ui.Anchor(playerMarker.rectTransform, point, point, new Vector2(-12, -12), new Vector2(12, 12));
        }

        public void Close() => root.SetActive(false);
    }
}
