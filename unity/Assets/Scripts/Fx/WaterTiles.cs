using UnityEngine;

namespace GuiQin
{
    // 流动纹理：256×256 可无缝平铺的小图，程序生成（对应网页版 wtTiles）。H = 横向流水（短弯亮纹 + 暗纹 + 焦散小弧），V = 瀑布（竖向长纹）。
    // 网页版用的是 canvas 随机数，这里用 System.Random 固定种子，细节不会逐像素一致，但密度、粗细、明暗是同一套参数。
    public static class WaterTiles
    {
        const int N = 256;
        static Texture2D h, v;
        public static Texture2D H { get { if (h == null) h = Make(false); return h; } }
        public static Texture2D V { get { if (v == null) v = Make(true); return v; } }

        static Texture2D Make(bool vert)
        {
            var buf = new float[N * N * 4];                 // 直通道 RGBA（0~1），先铺淡青色的全透明底，避免双线性采样时边缘发黑
            for (int i = 0; i < N * N; i++) { buf[i * 4] = 0.86f; buf[i * 4 + 1] = 0.96f; buf[i * 4 + 2] = 0.94f; buf[i * 4 + 3] = 0f; }
            var rng = new System.Random(vert ? 11 : 5);
            System.Func<float> R = () => (float)rng.NextDouble();

            for (int i = 0; i < 150; i++)                   // 细密的亮纹 + 暗纹
            {
                float px = R() * N, py = R() * N, L = 6f + R() * 20f; bool bright = R() > 0.4f; float bend = (R() - 0.5f) * 5f;
                float cr, cg, cb, ca, w;
                if (bright) { cr = 0.93f; cg = 1f; cb = 0.98f; ca = 0.3f + R() * 0.4f; w = 0.7f + R() * 0.9f; }
                else { cr = 0.02f; cg = 0.29f; cb = 0.33f; ca = 0.14f + R() * 0.16f; w = 1f + R() * 1.1f; }
                Vector2 p0 = new Vector2(px, py), p1, p2;
                if (vert) { p1 = new Vector2(px + bend, py + L * 0.7f); p2 = new Vector2(px + bend * 0.4f, py + L * 1.6f); }
                else { p1 = new Vector2(px + L / 2f, py + bend); p2 = new Vector2(px + L, py + L * 0.1f); }
                Curve(buf, p0, p1, p2, w, cr, cg, cb, ca);
            }
            for (int i = 0; i < 34; i++)                    // 焦散：小段亮弧
            {
                float px = R() * N, py = R() * N, r = 4f + R() * 8f, a0 = R() * 6.28f;
                float ca = 0.22f + R() * 0.25f, w = 0.6f + R() * 0.6f, span = 1.2f + R() * 1.2f;
                Vector2 prev = new Vector2(px + Mathf.Cos(a0) * r, py + Mathf.Sin(a0) * r);
                for (int s = 1; s <= 8; s++)
                {
                    float a = a0 + span * s / 8f; var cur = new Vector2(px + Mathf.Cos(a) * r, py + Mathf.Sin(a) * r);
                    Seg(buf, prev, cur, w, 1f, 1f, 0.94f, ca); prev = cur;
                }
            }
            var px32 = new Color32[N * N];
            for (int i = 0; i < N * N; i++)
                px32[i] = new Color32((byte)(buf[i * 4] * 255f), (byte)(buf[i * 4 + 1] * 255f), (byte)(buf[i * 4 + 2] * 255f), (byte)(buf[i * 4 + 3] * 255f));
            var t = new Texture2D(N, N, TextureFormat.RGBA32, false, true);       // linear：不做 sRGB 转换，和 canvas 里直接按数值混合一致
            t.SetPixels32(px32); t.Apply(false, true);
            t.wrapMode = TextureWrapMode.Repeat; t.filterMode = FilterMode.Bilinear;
            return t;
        }

        static void Curve(float[] buf, Vector2 a, Vector2 c, Vector2 b, float w, float r, float g, float bl, float al)
        {
            Vector2 prev = a;
            for (int s = 1; s <= 8; s++)
            {
                float t = s / 8f, u = 1f - t;
                var cur = u * u * a + 2f * u * t * c + t * t * b;
                Seg(buf, prev, cur, w, r, g, bl, al); prev = cur;
            }
        }

        // 一小段圆头线：按到线段的距离做抗锯齿，坐标环绕（所以平铺时无缝）
        static void Seg(float[] buf, Vector2 a, Vector2 b, float w, float r, float g, float bl, float al)
        {
            float half = w * 0.5f + 0.5f;
            int x0 = Mathf.FloorToInt(Mathf.Min(a.x, b.x) - half), x1 = Mathf.CeilToInt(Mathf.Max(a.x, b.x) + half);
            int y0 = Mathf.FloorToInt(Mathf.Min(a.y, b.y) - half), y1 = Mathf.CeilToInt(Mathf.Max(a.y, b.y) + half);
            Vector2 ab = b - a; float l2 = Mathf.Max(1e-6f, ab.sqrMagnitude);
            for (int y = y0; y <= y1; y++)
                for (int x = x0; x <= x1; x++)
                {
                    var p = new Vector2(x + 0.5f, y + 0.5f);
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / l2);
                    float d = (p - (a + ab * t)).magnitude;
                    float cov = Mathf.Clamp01(w * 0.5f + 0.5f - d);
                    if (cov <= 0f) continue;
                    int ix = ((x % N) + N) % N, iy = ((y % N) + N) % N, o = (iy * N + ix) * 4;
                    float sa = al * cov, da = buf[o + 3], oa = sa + da * (1f - sa);
                    if (oa <= 1e-5f) continue;
                    buf[o] = (r * sa + buf[o] * da * (1f - sa)) / oa;
                    buf[o + 1] = (g * sa + buf[o + 1] * da * (1f - sa)) / oa;
                    buf[o + 2] = (bl * sa + buf[o + 2] * da * (1f - sa)) / oa;
                    buf[o + 3] = oa;
                }
        }
    }
}
