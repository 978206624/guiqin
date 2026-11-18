using System.Collections.Generic;
using UnityEngine;

namespace GuiQin
{
    // 一张地图的流水：水面着色器（折射 + 岸边水花 + 三层流动纹理）+ 涟漪 / 闪光点 / 落叶 / 瀑布水雾（粒子）。
    // 对应网页版 waterSetup / waterDraw。水面遮罩来自 Resources/GuiQin/Water/<地图id>.png（tools/water.py 抠出的灰度图，亮度 = 水的不透明度）。
    public class WaterFx
    {
        struct Fall { public float x, y, w; }
        class Mist { public float x, y, vx, vy, r, a, life; public bool drop; }
        class Leaf { public float x, y, ph, a, life; public Color32 col; }
        class Spark { public int i; public float ph, fr; }
        class Ripple { public float x, y, a, life; }

        readonly MapRuntime map; readonly WaterDef def;
        readonly int w, h; readonly float f, kk;          // 蒙版像素 → 舞台像素的系数 kk = 舞台宽 / 蒙版宽
        readonly Material mat; readonly GameObject go;
        readonly bool[] solid;
        readonly List<int> pts = new List<int>();
        readonly List<Fall> falls = new List<Fall>();
        readonly List<Mist> mist = new List<Mist>();
        readonly List<Leaf> leaves = new List<Leaf>();
        readonly List<Ripple> rips = new List<Ripple>();
        readonly List<Spark> sparks = new List<Spark>();
        readonly QuadBatch ringB, softB, whiteB, leafB;

        public static WaterFx Create(Transform parent, string mapId, MapRuntime map)
        {
            var def = map.Def.water;
            if (def == null) return null;
            var mask = Resources.Load<Texture2D>("GuiQin/Water/" + mapId);
            var bg = Art.Tex(map.Def.img);
            var shader = Shader.Find("GuiQin/Water");
            if (mask == null || bg == null || shader == null) { Debug.LogWarning("水面没开：缺蒙版 / 底图 / 着色器（" + mapId + "）"); return null; }
            if (!mask.isReadable) { Debug.LogWarning("水面蒙版不可读：" + mapId + "（确认 Editor/ArtImporter 对 Resources/GuiQin/Water 生效）"); return null; }
            return new WaterFx(parent, map, def, mask, bg, shader);
        }

        WaterFx(Transform parent, MapRuntime map, WaterDef def, Texture2D mask, Texture2D bg, Shader shader)
        {
            this.map = map; this.def = def;
            w = mask.width; h = mask.height; f = w / 1000f; kk = map.W / w;
            solid = new bool[w * h];
            var px = mask.GetPixels32();
            var zones = new List<WaterZone>(def.zones ?? new List<WaterZone>());
            var flow = new Color32[w * h];                               // 流向图（贴图第 0 行在底部，所以 y 向下的像素要翻一下）
            var restDir = Norm(def.dir ?? new[] { -1f, 0.3f }); float restSp = def.speed > 0 ? def.speed : 20f;
            for (int y = 0; y < h; y++)
            {
                int row = (h - 1 - y) * w;
                for (int x = 0; x < w; x++)
                {
                    byte m = px[row + x].r;
                    if (m <= 6) continue;
                    int i = y * w + x;
                    if (m > 128) { solid[i] = true; if (i % 37 == 0) pts.Add(i); }
                    float[] d = restDir; float sp = restSp; bool vert = Mathf.Abs(restDir[1]) > Mathf.Abs(restDir[0]) * 1.5f;
                    foreach (var z in zones)
                        if (x >= z.rect[0] * f && x < z.rect[2] * f && y >= z.rect[1] * f && y < z.rect[3] * f)
                        { d = Norm(z.dir ?? restDir); sp = z.speed > 0 ? z.speed : 20f; vert = Mathf.Abs(d[1]) > Mathf.Abs(d[0]) * 1.5f; break; }
                    flow[row + x] = new Color32((byte)((d[0] * 0.5f + 0.5f) * 255f), (byte)((d[1] * 0.5f + 0.5f) * 255f),
                                                (byte)Mathf.Clamp(sp * f / 128f * 255f, 0f, 255f), vert ? (byte)255 : (byte)0);
                }
            }
            // 瀑布落点：竖向区域里，从下往上第一行出现 ≥3 个水像素的位置
            foreach (var z in zones)
            {
                if (z.dir == null || Mathf.Abs(z.dir[1]) <= Mathf.Abs(z.dir[0]) * 1.5f) continue;
                int bx0 = Mathf.Max(0, Mathf.RoundToInt(z.rect[0] * f)), by0 = Mathf.RoundToInt(z.rect[1] * f);
                int bx1 = Mathf.Min(w, Mathf.RoundToInt(z.rect[2] * f)), by1 = Mathf.Min(h, Mathf.RoundToInt(z.rect[3] * f));
                for (int y = by1 - 1; y > by0; y--)
                {
                    int n = 0; float sx = 0;
                    for (int x = bx0; x < bx1; x++) if (solid[y * w + x]) { n++; sx += x; }
                    if (n >= 3) { falls.Add(new Fall { x = sx / n, y = y, w = n }); break; }
                }
            }
            var flowTex = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
            flowTex.SetPixels32(flow); flowTex.Apply(false, true); flowTex.filterMode = FilterMode.Point; flowTex.wrapMode = TextureWrapMode.Clamp;

            mat = new Material(shader);
            mat.SetTexture("_MainTex", mask); mat.SetTexture("_BgTex", bg); mat.SetTexture("_FlowTex", flowTex);
            mat.SetTexture("_TileH", WaterTiles.H); mat.SetTexture("_TileV", WaterTiles.V);
            mat.SetVector("_MaskSize", new Vector4(w, h, f, 0f));
            go = new GameObject("water"); go.transform.SetParent(parent, false);
            var mesh = new Mesh { name = "waterQuad" };
            float W = map.W / Art.PX, H = map.H / Art.PX;
            mesh.vertices = new[] { new Vector3(0, 0, 0), new Vector3(W, 0, 0), new Vector3(W, -H, 0), new Vector3(0, -H, 0) };
            mesh.uv = new[] { new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0), new Vector2(0, 0) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>(); mr.sharedMaterial = mat; mr.sortingOrder = -9990;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;

            ringB = new QuadBatch("ripples", parent, Art.RingTex(), false, -9989, 16);
            softB = new QuadBatch("mist", parent, Art.SoftTex(), false, -9988, 96);
            whiteB = new QuadBatch("sparks", parent, Art.WhiteTex(), false, -9987, 256);
            leafB = new QuadBatch("leaves", parent, Art.EllipseTex(), false, -9986, 12);
            if (pts.Count > 0) for (int i = 0; i < 46; i++) sparks.Add(new Spark { i = pts[Random.Range(0, pts.Count)], ph = Random.value * 6.28f, fr = 0.8f + Random.value * 1.8f });
        }

        static float[] Norm(float[] d)
        {
            float l = Mathf.Sqrt(d[0] * d[0] + d[1] * d[1]); if (l < 1e-5f) l = 1f;
            return new[] { d[0] / l, d[1] / l };
        }

        // 蒙版像素 → 父物体本地坐标（世界单位）
        Vector2 P(float mx, float my) { return new Vector2(mx * kk / Art.PX, -my * kk / Art.PX); }
        float S(float maskPx) { return maskPx * kk / Art.PX; }

        public void Tick(float dt, float t)
        {
            mat.SetFloat("_T", t);
            if (pts.Count == 0) return;

            // 涟漪
            if (Random.value < dt * 2.2f && rips.Count < 8) { int i = pts[Random.Range(0, pts.Count)]; rips.Add(new Ripple { x = i % w, y = i / w, life = 1.6f + Random.value }); }
            ringB.Begin();
            for (int i = rips.Count - 1; i >= 0; i--)
            {
                var r = rips[i]; r.a += dt; float k = r.a / r.life;
                if (k >= 1f) { rips.RemoveAt(i); continue; }
                float rx = (3f + k * 16f) * f;
                ringB.Quad(P(r.x, r.y), S(rx), S(rx * 0.45f), 0f, QuadBatch.Rgb(240, 255, 252, 0.55f * (1f - k)));
            }
            ringB.End();

            // 闪光点 + 溅起的水珠（同一张白贴图）
            whiteB.Begin();
            foreach (var sp in sparks)
            {
                float v = Mathf.Sin(t * sp.fr * 2f + sp.ph);
                if (v < 0.55f) { if (v < -0.98f) sp.i = pts[Random.Range(0, pts.Count)]; continue; }
                float a = (v - 0.55f) / 0.45f, r = (1f + a * 1.4f) * f; float x = sp.i % w, y = sp.i / w;
                var c = QuadBatch.Rgb(255, 255, 245, 0.85f * a);
                whiteB.Quad(P(x, y), S(r), S(r * 0.5f), 0f, c);
                whiteB.Quad(P(x, y), S(r * 0.5f), S(r * 1.4f), 0f, c);
            }

            // 瀑布水雾
            softB.Begin();
            foreach (var fl in falls)
            {
                if (Random.value < dt * 7f) mist.Add(new Mist { x = fl.x + (Random.value - 0.5f) * fl.w * 0.9f, y = fl.y, vx = (Random.value - 0.5f) * 8f * f, vy = -(9f + Random.value * 14f) * f, r = (5f + Random.value * 6f) * f, life = 1.2f + Random.value * 0.9f });
                if (Random.value < dt * 5f) mist.Add(new Mist { x = fl.x + (Random.value - 0.5f) * fl.w * 0.8f, y = fl.y, vx = (Random.value - 0.5f) * 40f * f, vy = -(28f + Random.value * 30f) * f, r = 1.3f * f, life = 0.7f + Random.value * 0.3f, drop = true });
            }
            if (mist.Count > 90) mist.RemoveRange(0, mist.Count - 90);
            for (int i = mist.Count - 1; i >= 0; i--)
            {
                var p = mist[i]; p.a += dt; float k = p.a / p.life;
                if (k >= 1f) { mist.RemoveAt(i); continue; }
                p.x += p.vx * dt; p.y += p.vy * dt; if (p.drop) p.vy += 95f * f * dt;
                if (p.drop) whiteB.Quad(P(p.x, p.y), S(p.r), S(p.r), 0f, QuadBatch.Rgb(245, 252, 255, 0.8f * (1f - k)));
                else { float r = p.r * (1f + k * 1.8f); softB.Quad(P(p.x, p.y), S(r), S(r), 0f, QuadBatch.Rgb(235, 248, 255, 0.42f * Mathf.Sin(Mathf.PI * Mathf.Min(1f, k * 1.15f)))); }
            }
            softB.End(); whiteB.End();

            // 落叶：顺着水流漂，漂出水面就消失
            if (leaves.Count < 6 && Random.value < dt * 0.8f)
            {
                int i = pts[Random.Range(0, pts.Count)];
                leaves.Add(new Leaf { x = i % w, y = i / w, ph = Random.value * 6f, life = 10f + Random.value * 8f, col = Random.value > 0.5f ? QuadBatch.Rgb(217, 164, 65, 1f) : QuadBatch.Rgb(127, 176, 74, 1f) });
            }
            leafB.Begin();
            for (int i = leaves.Count - 1; i >= 0; i--)
            {
                var l = leaves[i]; l.a += dt;
                float[] d = def.dir ?? new[] { -1f, 0.3f }; float sp = def.speed > 0 ? def.speed : 20f;
                if (def.zones != null)
                    foreach (var q in def.zones)
                        if (l.x / f >= q.rect[0] && l.x / f <= q.rect[2] && l.y / f >= q.rect[1] && l.y / f <= q.rect[3]) { if (q.dir != null) d = q.dir; sp = q.speed; break; }
                var n = Norm(d); l.x += n[0] * sp * f * 0.8f * dt; l.y += n[1] * sp * f * 0.8f * dt;
                int xi = (int)l.x, yi = (int)l.y;
                if (l.a > l.life || xi < 0 || yi < 0 || xi >= w || yi >= h || !solid[yi * w + xi]) { leaves.RemoveAt(i); continue; }
                float fade = Mathf.Min(1f, Mathf.Min(l.a * 2f, (l.life - l.a) * 2f)) * 0.9f;
                var c = l.col; c.a = (byte)(fade * 255f);
                leafB.Quad(P(l.x, l.y + Mathf.Sin(t * 2f + l.ph) * 1.2f * f), S(5f * f * 0.8f), S(2.4f * f * 0.8f), Mathf.Sin(t * 1.4f + l.ph) * 0.8f * Mathf.Rad2Deg, c);
            }
            leafB.End();
        }

        public void SetVisible(bool v) { if (go != null) go.SetActive(v); foreach (var b in new[] { ringB, softB, whiteB, leafB }) if (b != null) b.SetVisible(v); }

        public void Destroy()
        {
            foreach (var b in new[] { ringB, softB, whiteB, leafB }) if (b != null && b.Go != null) Object.Destroy(b.Go);
            if (go != null) Object.Destroy(go);
        }
    }
}
