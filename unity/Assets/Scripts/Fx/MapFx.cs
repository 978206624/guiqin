using System.Collections.Generic;
using UnityEngine;

namespace GuiQin
{
    // 一张地图的“氛围层”：发光点（灯笼 / 萤火）、动态雾、落花（屏幕空间）、蝴蝶。
    // 对应网页版 loadMap 里的 glows / fog / petals，以及 bfSetup / bfStep。
    public class MapFx
    {
        class Cloud { public Vector2 c; public float w, h, dx, dy, d, delay; }
        class Petal { public float x, y, vx, vy, ph; }
        class Bf { public Vector2 p, v, t; public float sp, ph, rest, size, flap; public Color32 c1, c2; }

        readonly MapRuntime map; readonly Camera cam;
        readonly List<Vector2> glows = new List<Vector2>();
        readonly List<Cloud> clouds = new List<Cloud>();
        readonly List<Petal> petals = new List<Petal>();
        readonly List<Bf> bfs = new List<Bf>();
        readonly QuadBatch glowB, fogB, petalB, bfB, bsB;
        readonly FogDef fogDef;
        float bfVis = 1f;

        static readonly Color32[][] BfCols = {
            new[] { QuadBatch.Rgb(255, 216, 74, 1), QuadBatch.Rgb(240, 138, 36, 1) },
            new[] { QuadBatch.Rgb(255, 255, 255, 1), QuadBatch.Rgb(185, 205, 238, 1) },
            new[] { QuadBatch.Rgb(127, 194, 255, 1), QuadBatch.Rgb(58, 111, 208, 1) },
            new[] { QuadBatch.Rgb(255, 165, 200, 1), QuadBatch.Rgb(224, 85, 143, 1) },
            new[] { QuadBatch.Rgb(255, 233, 160, 1), QuadBatch.Rgb(217, 164, 65, 1) } };

        public MapFx(Transform mapRoot, Transform camT, MapRuntime map, Camera cam)
        {
            this.map = map; this.cam = cam; var def = map.Def;

            // 发光点
            if (def.glows != null) foreach (var g in def.glows) glows.Add(map.ToMap(g));
            glowB = new QuadBatch("glows", mapRoot, Art.SoftTex(), true, -4000, 32);

            // 动态雾：固定种子的一批大雾团，缓慢漂移、忽浓忽淡
            fogDef = def.fog;
            if (fogDef != null)
            {
                long seed = 7; System.Func<float> rnd = () => { seed = (seed * 16807L) % 2147483647L; return seed / 2147483647f; };
                for (int i = 0; i < fogDef.n; i++)
                {
                    float s = 380f + rnd() * 520f, hh = s * (0.55f + rnd() * 0.3f);
                    float left = rnd() * (map.W + s) - s * 0.5f, top = rnd() * (map.H + s) - s * 0.5f;
                    clouds.Add(new Cloud { c = new Vector2(left + s * 0.5f, top + hh * 0.5f), w = s, h = hh, dx = 60f + rnd() * 160f, dy = -30f + rnd() * 60f, d = 14f + rnd() * 18f, delay = -rnd() * 20f });
                }
            }
            fogB = new QuadBatch("mapfog", mapRoot, Art.FogTex(), false, 25000, Mathf.Max(4, clouds.Count + 2));

            // 落花：屏幕空间，挂在相机下
            petalB = new QuadBatch("petals", camT, Art.EllipseTex(), false, 25100, 24);
            petalB.Go.transform.localPosition = new Vector3(0f, 0f, 10f);
            if (def.petals)
                for (int i = 0; i < 16; i++) petals.Add(new Petal { x = Random.value * 820f, y = Random.value * 1000f, vx = -25f - Random.value * 30f, vy = 40f + Random.value * 45f, ph = Random.value * 10f });

            // 蝴蝶：在可走区域里飞来飞去
            bsB = new QuadBatch("bflyShadow", mapRoot, Art.EllipseTex(), false, -4500, 16);
            bfB = new QuadBatch("bfly", mapRoot, Art.EllipseTex(), false, 24000, Mathf.Max(8, def.butterflies * 8));
            for (int i = 0; i < def.butterflies; i++)
            {
                Vector2 p = Vector2.zero; bool ok = false;
                for (int k = 0; k < 200 && !ok; k++) { var q = new Vector2(Random.value * map.W, Random.value * map.H); if (map.Inside(q)) { p = q; ok = true; } }
                if (!ok) continue;
                var cols = BfCols[i % BfCols.Length];
                bfs.Add(new Bf { p = p, t = p, sp = 38f + Random.value * 34f, ph = Random.value * 10f, rest = Random.value * 2f, size = 27f + Random.value * 11f, flap = 0.12f + Random.value * 0.08f, c1 = cols[0], c2 = cols[1] });
            }
        }

        static float Flick(float u)                // 对应 CSS flick：0%,100% .25 / 35% .8 / 60% .4 / 80% .7
        {
            u = u - Mathf.Floor(u);
            if (u < 0.35f) return Mathf.Lerp(0.25f, 0.8f, u / 0.35f);
            if (u < 0.60f) return Mathf.Lerp(0.8f, 0.4f, (u - 0.35f) / 0.25f);
            if (u < 0.80f) return Mathf.Lerp(0.4f, 0.7f, (u - 0.6f) / 0.2f);
            return Mathf.Lerp(0.7f, 0.25f, (u - 0.8f) / 0.2f);
        }

        static Vector2 W(Vector2 stage) { return new Vector2(stage.x / Art.PX, -stage.y / Art.PX); }
        static Vector2 Scr(float px, float py) { return new Vector2((px - 360f) * 0.01f, (640f - py) * 0.01f); }       // 设计分辨率 720×1280 → 相机本地

        // wet：雨雪雾的湿度（落花变淡）；precip：雨 + 雷 + 雪（蝴蝶躲起来）
        public void Tick(float dt, float t, float wet, float precip)
        {
            // 发光点
            glowB.Begin();
            for (int i = 0; i < glows.Count; i++)
            {
                float dur = 2.4f + (i % 4) * 0.5f, a = Flick((t + i * 0.47f) / dur) * 0.55f;
                glowB.Quad(W(glows[i]), 0.55f, 0.55f, 0f, QuadBatch.Rgb(255, 172, 82, a));
            }
            glowB.End();

            // 雾
            fogB.Begin();
            if (fogDef != null)
                foreach (var c in clouds)
                {
                    float u = Mathf.Repeat((t - c.delay) / c.d, 2f), tri = u < 1f ? u : 2f - u, e = tri * tri * (3f - 2f * tri);      // ease-in-out 往返
                    float sc = Mathf.Lerp(0.92f, 1.08f, e), op = Mathf.Lerp(fogDef.lo, fogDef.hi, e);
                    var pos = c.c + new Vector2(Mathf.Lerp(-c.dx, c.dx, e), Mathf.Lerp(-c.dy, c.dy, e));
                    fogB.Quad(W(pos), c.w * sc * 0.5f / Art.PX, c.h * sc * 0.5f / Art.PX, 0f, QuadBatch.Rgb(240, 246, 255, op));
                }
            fogB.End();

            // 落花
            petalB.Begin();
            if (petals.Count > 0 && wet < 0.99f)
                foreach (var p in petals)
                {
                    p.x += (p.vx + Mathf.Sin(t * 1.3f + p.ph) * 30f) * dt; p.y += p.vy * dt;
                    if (p.y > 1040f || p.x < -20f) { p.x = 100f + Random.value * 740f; p.y = -20f + Random.value * 200f; }
                    petalB.Quad(Scr(p.x, p.y), 0.045f, 0.025f, -Mathf.Sin(t * 2f + p.ph) * 60f, QuadBatch.Rgb(255, 200, 220, 0.9f * (1f - wet)));
                }
            petalB.End();

            // 蝴蝶
            float vis = 1f - Mathf.Min(1f, precip * 1.8f);
            bfVis = vis;
            bfB.Begin(); bsB.Begin();
            if (vis >= 0.02f)
                foreach (var b in bfs) StepButterfly(b, dt, t, vis);
            bfB.End(); bsB.End();
        }

        void StepButterfly(Bf b, float dt, float t, float vis)
        {
            b.rest -= dt;
            if (b.rest <= 0f && Vector2.Distance(b.t, b.p) < 16f)             // 到了目标：偶尔停一会儿（盘旋），再选下一处
            {
                b.rest = Random.value < 0.35f ? 0.6f + Random.value * 1.6f : 0f;
                for (int k = 0; k < 40; k++)
                {
                    var q = new Vector2(b.p.x + (Random.value - 0.5f) * 560f, b.p.y + (Random.value - 0.5f) * 560f);
                    if (map.Inside(q)) { b.t = q; break; }
                }
            }
            Vector2 d = b.t - b.p; float L = Mathf.Max(0.0001f, d.magnitude); bool hov = b.rest > 0f; float k2 = 1f - Mathf.Exp(-2.2f * dt);
            float wob = Mathf.Sin(t * 2.3f + b.ph) * 26f;                     // 飞行时左右摇摆，不走直线
            Vector2 want = hov ? new Vector2(Mathf.Sin(t * 1.7f + b.ph) * 14f, Mathf.Cos(t * 1.3f + b.ph) * 10f)
                               : new Vector2(d.x / L * b.sp - d.y / L * wob, d.y / L * b.sp + d.x / L * wob);
            b.v += (want - b.v) * k2;
            var np = b.p + b.v * dt;
            if (map.Inside(np)) b.p = np; else { b.t = b.p; b.rest = 0f; }

            float bob = Mathf.Sin(t * 5f + b.ph) * 3f - 24f;                  // 离地 24px 左右，上下起伏
            float tilt = Mathf.Clamp(b.v.x * 0.35f, -18f, 18f);
            var center = W(new Vector2(b.p.x, b.p.y + bob));
            float sz = b.size / Art.PX;                                       // 蝴蝶整体宽度（世界单位）
            bsB.Quad(W(new Vector2(b.p.x, b.p.y + 4f)), 12f / Art.PX, 4f / Art.PX, 0f, QuadBatch.Rgb(20, 30, 10, 0.22f * vis));

            // 翅膀：左右各一对（上翅大、下翅小），扇动 = 横向缩放在 1 ↔ 0.18 之间来回
            float fl = Mathf.Lerp(1f, 0.18f, 0.5f + 0.5f * Mathf.Sin(t * Mathf.PI / b.flap + b.ph * 7f));
            Color32 c1 = b.c1, c2 = b.c2; c1.a = c2.a = (byte)(vis * 255f);
            float rot = -tilt;
            for (int s = -1; s <= 1; s += 2)
            {
                Vector2 up = Rot(new Vector2(s * 0.225f * sz * fl, 0.19f * sz), rot), lo = Rot(new Vector2(s * 0.16f * sz * fl, -0.17f * sz), rot);
                bfB.Quad(center + up, 0.255f * sz * fl, 0.2f * sz, rot, c2);                  // 描边：略大一圈的深色
                bfB.Quad(center + up, 0.225f * sz * fl, 0.17f * sz, rot, c1);
                bfB.Quad(center + lo, 0.16f * sz * fl, 0.11f * sz, rot, c2);
            }
            var body = QuadBatch.Rgb(58, 42, 26, vis);
            bfB.Quad(center, 0.05f * sz, 0.2f * sz, rot, body);
        }

        static Vector2 Rot(Vector2 v, float deg) { float r = deg * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r); return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c); }

        public void SetVisible(bool v) { foreach (var b in new[] { glowB, fogB, petalB, bfB, bsB }) b.SetVisible(v); }

        public void Destroy()
        {
            foreach (var b in new[] { glowB, fogB, petalB, bfB, bsB }) if (b != null && b.Go != null) Object.Destroy(b.Go);
        }
    }
}
