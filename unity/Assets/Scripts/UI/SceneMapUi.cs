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
        readonly Text title, subtitle;
        readonly RectTransform frame, markers;
        readonly Image picture;

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
            markers = Ui.Rect("markers", frame);
            Ui.Anchor(markers, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var legend = Ui.Label("legend", root.transform,
                "<color=#ffd24a>●</color> 人物　<color=#ff6a5a>●</color> 敌人　<color=#5ec8ff>◆</color> 出口　▲ 当前位置",
                18, WebUi.Text, TextAnchor.MiddleCenter);
            WebUi.At(legend.rectTransform, 20, 1060, 680, 45);
            var world = Ui.Btn("world", root.transform, "天下舆图", 22, WebUi.Purple, () => { Close(); onWorld(); });
            WebUi.At((RectTransform)world.transform, 165, 1168, 180, 50);
            var close = Ui.Btn("back", root.transform, "返回", 22, WebUi.Red, Close);
            WebUi.At((RectTransform)close.transform, 375, 1168, 180, 50);
            Close();
        }

        public void Open(MapRuntime map, Vector2 player, IEnumerable<Actor> actors, Action<Actor> onActor, Action<Vector2> onGround)
        {
            title.text = map.Def.name;
            subtitle.text = "场景地图 · " + (map.Def.note ?? "") + "　点击地面或标记寻路";
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
                Close();
                onGround(new Vector2(x * map.W, y * map.H));
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
                var label = (actor.Marked ? "！" : "● ") + actor.DisplayName;
                Mark(map, actor.Pos, label, color, () => { Close(); onActor(actor); });
            }
            Mark(map, player, "▲", Color.white, null);
            root.SetActive(true);
        }

        void Mark(MapRuntime map, Vector2 pos, string value, Color color, UnityAction onClick)
        {
            Text label;
            RectTransform rect;
            if (onClick != null)
            {
                var button = Ui.Btn("marker", markers, value, 14, Color.clear, onClick);
                label = button.GetComponentInChildren<Text>();
                rect = (RectTransform)button.transform;
            }
            else
            {
                label = Ui.Label("player", markers, value, 18, color, TextAnchor.MiddleCenter);
                rect = label.rectTransform;
            }
            label.color = color;
            WebUi.Edge(label);
            var point = new Vector2(Mathf.Clamp01(pos.x / map.W), 1 - Mathf.Clamp01(pos.y / map.H));
            Ui.Anchor(rect, point, point, new Vector2(-53, -12), new Vector2(53, 12));
        }

        public void Close() => root.SetActive(false);
    }
}
