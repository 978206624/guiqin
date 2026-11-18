using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace GuiQin
{
    // 素材读取：网页版的 AS.size[key] = [宽, 高, (头部中心x), (帧数)]，单位是舞台像素；这里换成 Sprite。
    // 世界单位：1 单位 = 100 舞台像素（PPU 按贴图实际像素算，保证显示尺寸和网页版一致）。
    public static class Art
    {
        public const float PX = 100f;
        static readonly Dictionary<string, Texture2D> tex = new Dictionary<string, Texture2D>();
        static readonly Dictionary<string, Sprite> single = new Dictionary<string, Sprite>();
        static readonly Dictionary<string, Sprite[]> sheets = new Dictionary<string, Sprite[]>();

        public static bool Has(string key) { return key != null && GameData.Assets.img.ContainsKey(key); }
        public static float[] Size(string key) { float[] s; return key != null && GameData.Assets.size.TryGetValue(key, out s) ? s : null; }

        public static Texture2D Tex(string key)
        {
            Texture2D t;
            if (key == null) return null;
            if (tex.TryGetValue(key, out t)) return t;
            string file;
            if (!GameData.Assets.img.TryGetValue(key, out file)) { Debug.LogWarning("没有素材：" + key); tex[key] = null; return null; }
            t = Resources.Load<Texture2D>("GuiQin/Art/" + Path.GetFileNameWithoutExtension(file));
            if (t == null) Debug.LogWarning("素材没导入：" + file);
            else { t.wrapMode = TextureWrapMode.Clamp; t.filterMode = FilterMode.Bilinear; }
            tex[key] = t;
            return t;
        }

        // 整张图一个 Sprite。显示尺寸取 size 表（没有就用像素尺寸）。pivot 默认脚底中点，center=true 时取图片中心
        public static Sprite One(string key, bool center = false)
        {
            string ck = key + (center ? "#c" : "#b");
            Sprite s;
            if (single.TryGetValue(ck, out s)) return s;
            var t = Tex(key);
            if (t == null) { single[ck] = null; return null; }
            var sz = Size(key);
            float ppu = sz != null ? t.width / (sz[0] / PX) : 100f;
            s = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, center ? 0.5f : 0f), ppu, 0, SpriteMeshType.FullRect);
            single[ck] = s;
            return s;
        }

        // 一整张是用图（地图底图）：pivot 左上角，ppu 让 1 个原图像素 = 舞台上 scale 个像素
        public static Sprite Background(string key, float stageW)
        {
            var t = Tex(key);
            if (t == null) return null;
            float ppu = t.width / (stageW / PX);
            return Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0f, 1f), ppu, 0, SpriteMeshType.FullRect);
        }

        // 行走精灵表：横排 N 格（size[3] 存“走路帧数”，最后多出一格是待机帧；没有 size[3] 就是老的 4 格）
        public static Sprite[] Sheet(string key)
        {
            Sprite[] arr;
            if (sheets.TryGetValue(key, out arr)) return arr;
            var t = Tex(key);
            var sz = Size(key);
            if (t == null || sz == null) { sheets[key] = null; return null; }
            int walk = sz.Length > 3 ? (int)sz[3] : 4;
            int count = sz.Length > 3 ? walk + 1 : 4;
            int fw = t.width / count;
            float ppu = fw / (sz[0] / PX);
            var pivot = new Vector2(sz[2] / sz[0], 0f);
            arr = new Sprite[count];
            for (int i = 0; i < count; i++) arr[i] = Sprite.Create(t, new Rect(i * fw, 0, fw, t.height), pivot, ppu, 0, SpriteMeshType.FullRect);
            sheets[key] = arr;
            return arr;
        }

        // 精灵表里走路循环的帧数、待机帧序号
        public static int WalkFrames(string key) { var sz = Size(key); return sz != null && sz.Length > 3 ? (int)sz[3] : 4; }
        public static int IdleFrame(string key) { var sz = Size(key); return sz != null && sz.Length > 3 ? (int)sz[3] : 1; }

        static Texture2D soft, fog, ring, white, ellipse;

        static Texture2D MakeTex(int n, System.Func<float, float, float> alpha)     // alpha(dx, dy)：dx,dy ∈ [-1,1]
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    byte a = (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha(dx, dy)) * 255f);
                    px[y * n + x] = new Color32(255, 255, 255, a);
                }
            t.SetPixels32(px); t.Apply(false, true); t.wrapMode = TextureWrapMode.Clamp; t.filterMode = FilterMode.Bilinear;
            return t;
        }

        // 径向渐变：中心不透明、边缘透明（和 CSS 的 radial-gradient(closest-side, a, 0) 一样）
        public static Texture2D SoftTex() { if (soft == null) soft = MakeTex(64, (x, y) => 1f - Mathf.Sqrt(x * x + y * y)); return soft; }
        // 雾团：中心 .9 → 45% 处 .5 → 边缘 0
        public static Texture2D FogTex()
        {
            if (fog == null) fog = MakeTex(64, (x, y) => { float r = Mathf.Sqrt(x * x + y * y); return r < 0.45f ? Mathf.Lerp(0.9f, 0.5f, r / 0.45f) : Mathf.Lerp(0.5f, 0f, (r - 0.45f) / 0.55f); });
            return fog;
        }
        // 细圆环（涟漪）
        public static Texture2D RingTex() { if (ring == null) ring = MakeTex(128, (x, y) => { float r = Mathf.Sqrt(x * x + y * y); return 1f - Mathf.Abs(r - 0.92f) / 0.07f; }); return ring; }
        static Sprite ringSprite;
        public static Sprite RingSprite()        // 细圆环精灵：技能释放 / 受击的扩散圈（1 单位 = 128 像素直径）
        {
            if (ringSprite == null) ringSprite = Sprite.Create(RingTex(), new Rect(0, 0, 128, 128), new Vector2(0.5f, 0.5f), 128f);
            return ringSprite;
        }
        public static Texture2D WhiteTex() { if (white == null) white = MakeTex(4, (x, y) => 1f); return white; }
        // 实心椭圆（翅膀、落叶），边缘一点点抗锯齿
        public static Texture2D EllipseTex() { if (ellipse == null) ellipse = MakeTex(64, (x, y) => (1f - Mathf.Sqrt(x * x + y * y)) * 14f); return ellipse; }

        static Sprite dot;
        // 程序生成的软边圆（脚下阴影、点击标记）
        public static Sprite Dot()
        {
            if (dot != null) return dot;
            const int n = 64;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float a = Mathf.Clamp01((1f - Mathf.Sqrt(dx * dx + dy * dy)) * 2.5f);
                    t.SetPixel(x, y, new Color(1, 1, 1, a));
                }
            t.Apply(); t.wrapMode = TextureWrapMode.Clamp;
            dot = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), n);
            return dot;
        }
    }
}
