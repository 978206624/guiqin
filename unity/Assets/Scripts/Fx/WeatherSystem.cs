using System;
using System.Collections.Generic;
using UnityEngine;

namespace GuiQin
{
    // 天气：每张地图各有自己的天气（种类 + 到期时间），到点按权重随机换一个；室内永远是晴。
    // 画面是屏幕空间的一层（挂在相机下）：暗角 / 云影 / 雾 / 雨丝 / 雪点 / 闪电，
    // 各种效果都有 0~1 的“强度”，朝目标值缓缓靠拢，所以换天气是渐变的。对应网页版 wxDraw。
    public class WeatherSystem
    {
        public static readonly string[] Kinds = { "clear", "rain", "storm", "snow" };
        static readonly Dictionary<string, float[]> Target = new Dictionary<string, float[]>   // rain, storm, snow, fog, cloud, dark
        {
            { "clear", new float[] { 0, 0, 0, 0, 0, 0 } },
            { "rain",  new[] { .7f, 0, 0, .25f, 1, .55f } },
            { "storm", new[] { 1f, 1, 0, .2f, 1, 1 } },
            { "snow",  new[] { 0, 0, 1f, .3f, .6f, .2f } } };

        class State { public string kind; public float until; }
        class Drop { public float x, y, v, l; }
        class Flake { public float x, y, v, r, ph; }
        class FogP { public float x, y, r, v, ph; }

        public event Action<string, bool> Changed;           // (新天气, 是否要提示)

        readonly Camera cam;
        readonly Dictionary<string, State> states = new Dictionary<string, State>();
        readonly float[] lv = new float[6];                  // 当前强度
        readonly List<Drop> rain = new List<Drop>(); readonly List<Flake> snow = new List<Flake>(); readonly List<FogP> fogs = new List<FogP>();
        readonly QuadBatch softB, whiteB, flakeB;
        MapRuntime map; string lockKind, shown;
        float t, flash, flashAt = 4f, flash2;

        public string Lock { get { return lockKind; } }
        public float Wet { get { return Mathf.Min(1f, lv[0] + lv[2] * 0.8f + lv[3] * 0.5f); } }
        public float Precip { get { return lv[0] + lv[1] + lv[2]; } }

        public WeatherSystem(Camera cam)
        {
            this.cam = cam;
            softB = new QuadBatch("wxSoft", cam.transform, Art.SoftTex(), false, 20000, 16);
            whiteB = new QuadBatch("wxFill", cam.transform, Art.WhiteTex(), false, 20001, 460);
            flakeB = new QuadBatch("wxSnow", cam.transform, Art.EllipseTex(), false, 20002, 140);
            foreach (var b in new[] { softB, whiteB, flakeB }) b.Go.transform.localPosition = new Vector3(0f, 0f, 10f);
            for (int i = 0; i < 420; i++) rain.Add(new Drop { x = UnityEngine.Random.value * 820f, y = UnityEngine.Random.value * 1280f, v = 0.8f + UnityEngine.Random.value * 0.5f, l = 20f + UnityEngine.Random.value * 28f });
            for (int i = 0; i < 130; i++) snow.Add(new Flake { x = UnityEngine.Random.value * 720f, y = UnityEngine.Random.value * 1280f, v = 30f + UnityEngine.Random.value * 50f, r = 1.4f + UnityEngine.Random.value * 2.6f, ph = UnityEngine.Random.value * 10f });
            for (int i = 0; i < 7; i++) fogs.Add(new FogP { x = i * 130f, y = 160f + (i % 4) * 280f + UnityEngine.Random.value * 120f, r = 260f + UnityEngine.Random.value * 160f, v = 10f + UnityEngine.Random.value * 14f, ph = UnityEngine.Random.value * 10f });
        }

        // ---------- 数据 ----------
        public static string NameOf(string kind) { return (string)GameData.Raw["weather"][kind]["name"]; }
        public static string DescOf(string kind) { return (string)GameData.Raw["weather"][kind]["desc"]; }

        string Pick(string mapId, string not)
        {
            var w = new Dictionary<string, int>();
            var by = GameData.Raw["weather"]["byMap"][mapId];
            if (by != null) foreach (var kv in by.ToObject<Dictionary<string, int>>()) w[kv.Key] = kv.Value; else w["clear"] = 1;
            if (not != null && w.Count > 1) w.Remove(not);
            int sum = 0; foreach (var kv in w) sum += kv.Value;
            float r = UnityEngine.Random.value * sum;
            foreach (var kv in w) { r -= kv.Value; if (r <= 0f) return kv.Key; }
            return "clear";
        }

        State Roll(string mapId, string not)
        {
            var span = GameData.Raw["weather"]["span"];
            float lo = (float)span[0], hi = (float)span[1];
            return new State { kind = Pick(mapId, not), until = Time.unscaledTime + lo + UnityEngine.Random.value * (hi - lo) };
        }

        public string Kind
        {
            get
            {
                if (map == null || map.Def.indoor) return "clear";           // 室内（玄兵塔）永远是晴
                State s;
                return lockKind ?? (states.TryGetValue(map.Id, out s) ? s.kind : "clear");
            }
        }

        // 还有多久换天气（分钟），固定天气时返回 -1
        public int MinutesLeft()
        {
            State s;
            if (lockKind != null || map == null || !states.TryGetValue(map.Id, out s)) return -1;
            return Mathf.Max(1, Mathf.RoundToInt((s.until - Time.unscaledTime) / 60f));
        }

        public void SetLock(string kind) { lockKind = kind; Refresh(true); }
        public void Auto() { lockKind = null; Refresh(true); }

        // ---------- 状态 ----------
        public void SetMap(MapRuntime m)         // 进新地图：不渐变，直接是那里的天气
        {
            map = m; shown = null; Refresh(false);
            Array.Copy(Target[Kind], lv, 6);
        }

        void Refresh(bool announce)
        {
            if (map == null) return;
            State s;
            if (!states.TryGetValue(map.Id, out s)) states[map.Id] = Roll(map.Id, null);
            else if (lockKind == null && Time.unscaledTime > s.until) states[map.Id] = Roll(map.Id, s.kind);
            string k = Kind;
            if (k != shown) { bool first = shown == null; shown = k; if (Changed != null) Changed(k, announce && !first); }
        }

        // ---------- 每帧 ----------
        static Vector2 D(float x, float y) { return new Vector2((x - 360f) * 0.01f, (640f - y) * 0.01f); }

        public void Tick(float dt)
        {
            Refresh(true);
            t += dt;
            var tg = Target[Kind]; float e = 1f - Mathf.Exp(-1.1f * dt);
            for (int i = 0; i < 6; i++) lv[i] += (tg[i] - lv[i]) * e;
            float rainV = lv[0], storm = lv[1], snowV = lv[2], fog = lv[3], cloud = lv[4], dark = lv[5];

            float halfH = cam.orthographicSize * 100f, halfW = halfH * cam.aspect;          // 可见范围（设计像素）
            float L = 360f - halfW, R = 360f + halfW, T = 640f - halfH, B = 640f + halfH;

            softB.Begin(); whiteB.Begin(); flakeB.Begin();
            if (dark > 0.01f) whiteB.Quad(Vector2.zero, 40f, 40f, 0f, QuadBatch.Rgb(24, 34, 60, dark * 0.5f));
            if (cloud > 0.02f)                                          // 云影：几块缓缓飘过的暗斑
                for (int i = 0; i < 4; i++)
                {
                    float x = ((t * (14f + i * 5f) + i * 260f) % 1100f) - 190f, y = 180f + i * 290f, r = 230f + i * 30f;
                    softB.Quad(D(x, y), r * 0.01f, r * 0.01f, 0f, QuadBatch.Rgb(20, 28, 45, cloud * 0.2f));
                }
            if (fog > 0.02f)
            {
                foreach (var f in fogs)
                {
                    float x = ((f.x + t * f.v) % 1000f) - 140f, y = f.y + Mathf.Sin(t * 0.25f + f.ph) * 30f;
                    softB.Quad(D(x, y), f.r * 0.01f, f.r * 0.01f, 0f, QuadBatch.Rgb(232, 238, 244, fog * 0.34f));
                }
                whiteB.Quad(Vector2.zero, 40f, 40f, 0f, QuadBatch.Rgb(226, 232, 238, fog * 0.1f));
            }
            if (rainV > 0.02f)                                          // 雨丝：雷雨更密更斜
            {
                int n = Mathf.FloorToInt(rain.Count * Mathf.Min(1f, rainV)); float slant = 0.16f + storm * 0.16f, sp = 1050f * dt * (1f + storm * 0.3f);
                var col = QuadBatch.Rgb(214, 228, 250, 0.36f + rainV * 0.24f);
                for (int i = 0; i < n; i++)
                {
                    var p = rain[i]; p.y += sp * p.v; p.x -= sp * p.v * slant;
                    if (p.y > B + 20f || p.x < L - 40f) { p.y = T - 40f; p.x = L + UnityEngine.Random.value * (R - L + 100f); }
                    whiteB.Line(D(p.x, p.y), D(p.x + p.l * slant, p.y - p.l), 0.017f, col);
                }
            }
            if (snowV > 0.02f)
            {
                int n = Mathf.FloorToInt(snow.Count * snowV);
                for (int i = 0; i < n; i++)
                {
                    var p = snow[i]; p.y += p.v * dt; p.x += Mathf.Sin(t * 1.1f + p.ph) * 22f * dt - 8f * dt;
                    if (p.y > B + 20f) { p.y = T - 10f; p.x = L + UnityEngine.Random.value * (R - L); }
                    if (p.x < L - 10f) p.x = R + 10f;
                    flakeB.Quad(D(p.x, p.y), p.r * 0.01f, p.r * 0.01f, 0f, QuadBatch.Rgb(255, 255, 255, 0.85f));
                }
            }
            if (storm > 0.5f)                                           // 闪电：隔几秒闪一两下
            {
                flashAt -= dt;
                if (flashAt <= 0f) { flash = 1f; flashAt = 4f + UnityEngine.Random.value * 7f; if (UnityEngine.Random.value < 0.5f) flash2 = 0.17f; }
            }
            if (flash2 > 0f) { flash2 -= dt; if (flash2 <= 0f) flash = 0.8f; }
            if (flash > 0.01f) { whiteB.Quad(Vector2.zero, 40f, 40f, 0f, QuadBatch.Rgb(236, 242, 255, flash * 0.62f)); flash *= Mathf.Exp(-7f * dt); }
            softB.End(); whiteB.End(); flakeB.End();
        }

        public void SetVisible(bool v) { foreach (var b in new[] { softB, whiteB, flakeB }) b.SetVisible(v); }

        public void Destroy()
        {
            foreach (var b in new[] { softB, whiteB, flakeB }) if (b != null && b.Go != null) UnityEngine.Object.Destroy(b.Go);
        }
    }
}
