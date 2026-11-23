using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace GuiQin
{
    // 天气：户外使用同一份 UTC 天气表；室内不播放天气。
    // 画面是屏幕空间的一层（挂在相机下）：暗角 / 云影 / 雾 / 雨丝 / 雪点 / 闪电，
    // 各种效果都有 0~1 的“强度”，朝目标值缓缓靠拢，所以换天气是渐变的。对应网页版 wxDraw。
    public class WeatherSystem
    {
        static readonly string[] Kinds = { "clear", "rain", "storm", "snow" };
        static readonly Dictionary<string, float[]> Target = new Dictionary<string, float[]>   // rain, storm, snow, fog, cloud, dark
        {
            { "clear", new float[] { 0, 0, 0, 0, 0, 0 } },
            { "rain",  new[] { .7f, 0, 0, .25f, 1, .55f } },
            { "storm", new[] { 1f, 1, 0, .2f, 1, 1 } },
            { "snow",  new[] { 0, 0, 1f, .3f, .6f, .2f } } };

        class Drop { public float x, y, v, l; }
        class Flake { public float x, y, v, r, ph; }
        class FogP { public float x, y, r, v, ph; }

        public event Action<string, bool> Changed;           // (新天气, 是否要提示)

        readonly Camera cam;
        readonly Func<long> utcNow;
        readonly long intervalMs;
        readonly float[] lv = new float[6];                  // 当前强度
        readonly List<Drop> rain = new List<Drop>(); readonly List<Flake> snow = new List<Flake>(); readonly List<FogP> fogs = new List<FogP>();
        readonly QuadBatch softB, whiteB, flakeB;
        MapRuntime map; string shown;
        long shownSlot = long.MinValue;
        float t, flash, flashAt = 4f, flash2;

        public float Wet { get { return Mathf.Min(1f, lv[0] + lv[2] * 0.8f + lv[3] * 0.5f); } }
        public float Precip { get { return lv[0] + lv[1] + lv[2]; } }

        public WeatherSystem(Camera cam, Func<long> utcNow)
        {
            this.cam = cam; this.utcNow = utcNow;
            intervalMs = (long)GameData.Raw["weather"]["intervalSeconds"] * 1000;
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
        // 固定的加权选择。网页版使用相同的 FNV-1a 与键顺序。
        static string KindAtSlot(long slot)
        {
            var weights = GameData.Raw["weather"]["weights"];
            int total = 0;
            foreach (var kind in Kinds) total += (int)weights[kind];
            uint hash = 2166136261;
            foreach (char c in slot.ToString(CultureInfo.InvariantCulture))
                hash = unchecked((hash ^ c) * 16777619);
            int roll = (int)(hash % (uint)total);
            foreach (var kind in Kinds)
            {
                roll -= (int)weights[kind];
                if (roll < 0) return kind;
            }
            return "clear";
        }

        public string Kind => shown ?? "clear";

        public void SetMap(MapRuntime m)
        {
            map = m; shown = null; shownSlot = long.MinValue; Refresh(false);
            Array.Copy(Target[Kind], lv, 6);
        }

        void Refresh(bool announce)
        {
            if (map == null) return;
            long slot = utcNow() / intervalMs;
            if (slot == shownSlot && shown != null) return;
            shownSlot = slot;
            string next = map.Def.indoor ? "clear" : KindAtSlot(slot);
            if (next == shown) return;
            bool first = shown == null;
            shown = next;
            Changed?.Invoke(next, announce && !first);
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

        bool visible = true;
        public void SetVisible(bool v) { visible = v; ApplyFx(); }
        public void ApplyFx() { foreach (var b in new[] { softB, whiteB, flakeB }) b.SetVisible(visible && Settings.WeatherFx); }

        public void Destroy()
        {
            foreach (var b in new[] { softB, whiteB, flakeB }) if (b != null && b.Go != null) UnityEngine.Object.Destroy(b.Go);
        }
    }
}
