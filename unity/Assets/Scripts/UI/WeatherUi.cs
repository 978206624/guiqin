using UnityEngine;
using UnityEngine.UI;

namespace GuiQin
{
    // 天气面板：当前地图的天气、还有多久变天、演示用的“固定天气”按钮。对应网页版 openWeather。
    public class WeatherUi
    {
        readonly GameObject root; readonly WeatherSystem wx; readonly MapRuntimeProvider cur;
        readonly Text head, desc, eta, autoLabel; readonly Image[] kindBg = new Image[4]; readonly Image autoBg;
        public delegate string MapRuntimeProvider();

        static readonly Color Idle = new Color(0.2f, 0.2f, 0.3f, 1f), On = new Color(0.55f, 0.4f, 0.15f, 1f);

        public WeatherUi(Transform parent, WeatherSystem wx, MapRuntimeProvider placeName)
        {
            this.wx = wx; cur = placeName;
            var bg = WebUi.Panel("weatherPanel", parent); root = bg.gameObject;
            Ui.Anchor(bg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-320, -280), new Vector2(320, 280));
            var title = Ui.Label("title", bg.transform, "天气", 32, new Color(1f, 0.92f, 0.65f), TextAnchor.UpperCenter);
            WebUi.Title(title);
            Ui.Anchor(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -54), new Vector2(0, -10));
            head = Ui.Label("head", bg.transform, "", 40, new Color(1f, 0.95f, 0.77f), TextAnchor.UpperLeft);
            Ui.Anchor(head.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(30, -120), new Vector2(-30, -66));
            desc = Ui.Label("desc", bg.transform, "", 26, new Color(0.8f, 0.82f, 0.9f), TextAnchor.UpperLeft);
            Ui.Anchor(desc.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(30, -170), new Vector2(-30, -124));
            eta = Ui.Label("eta", bg.transform, "", 22, new Color(0.65f, 0.68f, 0.78f), TextAnchor.UpperLeft);
            Ui.Anchor(eta.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(30, -208), new Vector2(-30, -174));
            var demo = Ui.Label("demo", bg.transform, "演示：直接切换这张地图的天气", 22, new Color(0.65f, 0.68f, 0.78f), TextAnchor.UpperLeft);
            Ui.Anchor(demo.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(30, -250), new Vector2(-30, -216));
            for (int i = 0; i < 4; i++)
            {
                string k = WeatherSystem.Kinds[i];
                var b = Ui.Btn("k_" + k, bg.transform, WeatherSystem.NameOf(k), 28, Idle, () => { wx.SetLock(k); Refresh(); });
                kindBg[i] = b.GetComponent<Image>();
                Ui.Anchor(b.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(0, 1), new Vector2(30 + i * 148, -330), new Vector2(30 + i * 148 + 136, -258));
            }
            var ab = Ui.Btn("auto", bg.transform, "", 26, Idle, () => { wx.Auto(); Refresh(); });
            autoBg = ab.GetComponent<Image>(); autoLabel = ab.GetComponentInChildren<Text>();
            Ui.Anchor(ab.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -410), new Vector2(330, -346));
            var close = Ui.Btn("close", bg.transform, "关闭", 28, new Color(0.3f, 0.3f, 0.34f, 1f), () => root.SetActive(false));
            Ui.Anchor(close.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(-120, 24), new Vector2(120, 90));
            root.SetActive(false);
        }

        public bool Visible { get { return root.activeSelf; } }
        public void Open() { Refresh(); root.SetActive(true); }
        public void Close() { root.SetActive(false); }

        void Refresh()
        {
            string k = wx.Kind;
            head.text = cur() + " · " + WeatherSystem.NameOf(k);
            desc.text = WeatherSystem.DescOf(k);
            int m = wx.MinutesLeft();
            eta.text = m < 0 ? "天气会一直保持" : "约 " + m + " 分钟后转天气";
            for (int i = 0; i < 4; i++) kindBg[i].color = (WeatherSystem.Kinds[i] == k && wx.Lock != null) ? On : Idle;
            autoBg.color = wx.Lock == null ? On : Idle;
            autoLabel.text = wx.Lock == null ? "自动天气中" : "恢复自动天气";
        }
    }
}
