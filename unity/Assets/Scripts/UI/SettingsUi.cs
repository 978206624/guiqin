using System;
using UnityEngine;
using UnityEngine.UI;

namespace GuiQin
{
    // 设置：画面特效开关、战斗速度、切换角色。音效暂缓。
    public class SettingsUi
    {
        readonly GameObject root; readonly RectTransform body; readonly Action onSwitch;
        public bool Visible { get { return root.activeSelf; } }

        public SettingsUi(Transform parent, Action onSwitchRole)
        {
            onSwitch = onSwitchRole;
            var bg = WebUi.Panel("settings", parent); root = bg.gameObject;
            Ui.Anchor(bg.rectTransform, Vector2.zero, Vector2.one, new Vector2(30, 120), new Vector2(-30, -110));
            var title = Ui.Label("title", bg.transform, "设置", 36, new Color(1f, 0.92f, 0.65f), TextAnchor.UpperCenter);
            WebUi.Title(title);
            Ui.Anchor(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -64), new Vector2(0, -14));
            body = Ui.Rect("body", bg.transform); Ui.Anchor(body, Vector2.zero, Vector2.one, new Vector2(24, 100), new Vector2(-24, -80));
            var close = WebUi.Close(bg.transform, Close);
            Ui.Anchor(close.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(1, 0), new Vector2(24, 20), new Vector2(-24, 86));
            root.SetActive(false);
        }

        public void Toggle() { if (root.activeSelf) Close(); else { root.SetActive(true); Redraw(); } }
        public void Close() { root.SetActive(false); }

        void Row(int i, string label, string note, Transform right)
        {
            var l = Ui.Label("l" + i, body, label + (note != null ? "\n<size=20><color=#9aa4c0>" + note + "</color></size>" : ""), 28, Color.white, TextAnchor.MiddleLeft); l.supportRichText = true;
            Ui.Anchor(l.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -i * 100 - 92), new Vector2(-230, -i * 100));
        }

        void Switch(int i, string label, string note, Func<bool> get, Action<bool> set)
        {
            Row(i, label, note, null);
            bool on = get();
            var b = Ui.Btn("sw" + i, body, on ? "开" : "关", 26, on ? new Color(0.7f, 0.25f, 0.2f) : new Color(0.3f, 0.32f, 0.4f), () => { set(!get()); Redraw(); });
            Ui.Anchor(b.GetComponent<RectTransform>(), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-110, -i * 100 - 76), new Vector2(0, -i * 100 - 14));
        }

        void Redraw()
        {
            for (int k = body.childCount - 1; k >= 0; k--) UnityEngine.Object.Destroy(body.GetChild(k).gameObject);
            Switch(0, "水面动效", "流水、折射、浪花；关闭可减轻发热", () => Settings.Water, v => Settings.Water = v);
            Switch(1, "蝴蝶", "晴天的村庄蝴蝶", () => Settings.Butterflies, v => Settings.Butterflies = v);
            Switch(2, "天气特效", "雨、雷、雪的画面", () => Settings.WeatherFx, v => Settings.WeatherFx = v);
            Switch(3, "震屏", "战斗受击的屏幕抖动", () => Settings.Shake, v => Settings.Shake = v);
            Row(4, "战斗速度", "战斗动作的快慢", null);
            float[] sp = { 1f, 1.5f, 2f };
            for (int j = 0; j < sp.Length; j++)
            {
                float v = sp[j]; bool on = Mathf.Approximately(Settings.Speed, v);
                var b = Ui.Btn("sp" + j, body, v + "×", 24, on ? new Color(0.7f, 0.25f, 0.2f) : new Color(0.3f, 0.32f, 0.4f), () => { Settings.Speed = v; Redraw(); });
                Ui.Anchor(b.GetComponent<RectTransform>(), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-230 + j * 76 - 0, -4 * 100 - 76), new Vector2(-230 + j * 76 + 70, -4 * 100 - 14));
            }
            var sw = Ui.Btn("switch", body, "切换角色", 28, new Color(0.25f, 0.3f, 0.5f), () => { Close(); onSwitch(); });
            Ui.Anchor(sw.GetComponent<RectTransform>(), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-140, -5 * 100 - 90), new Vector2(140, -5 * 100 - 20));
            var tip = Ui.Label("tip", body, "这些设置只保存在这台设备上。", 22, new Color(0.65f, 0.7f, 0.85f), TextAnchor.UpperLeft);
            Ui.Anchor(tip.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -5 * 100 - 190), new Vector2(0, -5 * 100 - 110));
        }
    }
}
