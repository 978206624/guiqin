using System;
using System.Collections.Generic;
using UnityEngine;

namespace GuiQin
{
    // 一张地图的运行时数据：可行走判定 + 网格 A* 寻路。逻辑和 js/game.js 的 inside / buildGrid / findPath 一一对应。
    // 所有坐标都是“舞台像素”（原点在左上，y 向下），和网页版相同；只有在显示时才换成 Unity 世界坐标。
    public class MapRuntime
    {
        public string Id;
        public MapDef Def;
        public float W, H;                       // 舞台大小
        public float Sc;                         // 数据坐标 → 舞台像素的系数 = scale * k
        public List<Vector2[]> Walk = new List<Vector2[]>();
        public List<Vector2[]> Opens = new List<Vector2[]>();
        public List<Vector2[]> Blocks = new List<Vector2[]>();
        MaskDef mask; byte[] maskBytes;
        public int Cell = 16, Gw, Gh;
        byte[] grid;

        public Vector2 ToMap(float x, float y) { return new Vector2(x * Sc, y * Sc); }
        public Vector2 ToMap(float[] p) { return new Vector2(p[0] * Sc, p[1] * Sc); }

        public MapRuntime(string id, MapDef def)
        {
            Id = id; Def = def;
            Sc = def.scale * def.k;
            W = def.iw * def.scale; H = def.ih * def.scale;
            mask = GameData.Mask(id);
            if (mask != null) { maskBytes = Convert.FromBase64String(mask.bits); Cell = 8; } else Cell = 16;
            if (def.walk != null) foreach (var poly in def.walk) Walk.Add(Poly(poly));
            if (def.opens != null) foreach (var poly in def.opens) Opens.Add(Poly(poly));
            if (def.blocks != null)
                foreach (var b in def.blocks)                      // 障碍：椭圆（八边形近似，竖向压扁 0.6）
                {
                    var c = ToMap(b.at); float w = b.r * Sc; var o = new Vector2[8];
                    for (int k = 0; k < 8; k++) o[k] = new Vector2(c.x + Mathf.Cos(k * Mathf.PI / 4) * w, c.y + Mathf.Sin(k * Mathf.PI / 4) * w * 0.6f);
                    Blocks.Add(o);
                }
            if (def.roads != null)
                foreach (var r in def.roads)                       // 道路：每段一个矩形，每个拐点一个八边形
                {
                    var pts = new List<Vector2>(); foreach (var p in r.pts) pts.Add(ToMap(p));
                    float w = r.r * Sc;
                    for (int i = 0; i < pts.Count; i++)
                    {
                        var p = pts[i]; var oct = new Vector2[8];
                        for (int k = 0; k < 8; k++) oct[k] = new Vector2(p.x + Mathf.Cos(k * Mathf.PI / 4) * w, p.y + Mathf.Sin(k * Mathf.PI / 4) * w);
                        Walk.Add(oct);
                        if (i > 0)
                        {
                            var a = pts[i - 1]; float dx = p.x - a.x, dy = p.y - a.y, len = Mathf.Sqrt(dx * dx + dy * dy); if (len < 1e-4f) len = 1f;
                            float nx = -dy / len * w, ny = dx / len * w;
                            Walk.Add(new[] { new Vector2(a.x + nx, a.y + ny), new Vector2(p.x + nx, p.y + ny), new Vector2(p.x - nx, p.y - ny), new Vector2(a.x - nx, a.y - ny) });
                        }
                    }
                }
            BuildGrid();
        }

        Vector2[] Poly(float[][] pts) { var o = new Vector2[pts.Length]; for (int i = 0; i < pts.Length; i++) o[i] = ToMap(pts[i]); return o; }

        public static bool InPoly(Vector2[] poly, Vector2 p)
        {
            bool c = false;
            for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            {
                Vector2 a = poly[i], b = poly[j];
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) c = !c;
            }
            return c;
        }

        public bool Inside(Vector2 p)
        {
            foreach (var poly in Opens) if (InPoly(poly, p)) return true;            // 手工打通的口子（台阶等位图漏掉的地方）
            if (mask != null)                                                           // 有碰撞位图：直接查位图，再减去 blocks
            {
                float u = Sc * mask.cell; int gx = Mathf.FloorToInt(p.x / u), gy = Mathf.FloorToInt(p.y / u);
                if (gx < 0 || gy < 0 || gx >= mask.w || gy >= mask.h) return false;
                foreach (var poly in Blocks) if (InPoly(poly, p)) return false;
                int i = gy * mask.w + gx; return ((maskBytes[i >> 3] >> (7 - (i & 7))) & 1) == 1;
            }
            foreach (var poly in Blocks) if (InPoly(poly, p)) return false;           // 路中间的障碍（井、树）
            foreach (var poly in Walk) if (InPoly(poly, p)) return true;
            return false;
        }

        void BuildGrid()
        {
            Gw = Mathf.CeilToInt(W / Cell); Gh = Mathf.CeilToInt(H / Cell); grid = new byte[Gw * Gh];
            for (int y = 0; y < Gh; y++)
                for (int x = 0; x < Gw; x++)
                    if (Inside(new Vector2((x + 0.5f) * Cell, (y + 0.5f) * Cell))) grid[y * Gw + x] = 1;
        }

        bool CellOk(int x, int y) { return x >= 0 && y >= 0 && x < Gw && y < Gh && grid[y * Gw + x] == 1; }
        Vector2 CellCenter(int x, int y) { return new Vector2((x + 0.5f) * Cell, (y + 0.5f) * Cell); }

        // 离某点最近的可走格子（点在障碍物上时用）
        public bool NearestCell(Vector2 p, out int cx, out int cy)
        {
            cx = Mathf.FloorToInt(p.x / Cell); cy = Mathf.FloorToInt(p.y / Cell);
            if (CellOk(cx, cy)) return true;
            int bx = 0, by = 0; float bd = 1e9f; bool found = false;
            for (int r = 1; r <= 60 && !found; r++)
                for (int dy = -r; dy <= r; dy++)
                    for (int dx = -r; dx <= r; dx++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy)) != r || !CellOk(cx + dx, cy + dy)) continue;
                        var q = CellCenter(cx + dx, cy + dy); float d = Vector2.Distance(q, p);
                        if (d < bd) { bd = d; bx = cx + dx; by = cy + dy; found = true; }
                    }
            if (found) { cx = bx; cy = by; }
            return found;
        }

        // 点到走不了的地方：取最近的可走点
        public bool Snap(Vector2 p, out Vector2 snapped)
        {
            snapped = p;
            if (Inside(p)) return true;
            int cx, cy; if (!NearestCell(p, out cx, out cy)) return false;
            snapped = CellCenter(cx, cy); return true;
        }

        bool Los(Vector2 a, Vector2 b)
        {
            float d = Vector2.Distance(a, b); int n = Mathf.Max(1, Mathf.CeilToInt(d / 6f));
            for (int i = 1; i < n; i++) if (!Inside(Vector2.Lerp(a, b, (float)i / n))) return false;
            return true;
        }

        // ---- 最小堆 ----
        struct HeapItem { public float f; public int i; }
        static void Push(List<HeapItem> h, float f, int i)
        {
            h.Add(new HeapItem { f = f, i = i }); int k = h.Count - 1;
            while (k > 0) { int p = (k - 1) >> 1; if (h[p].f <= h[k].f) break; var t = h[p]; h[p] = h[k]; h[k] = t; k = p; }
        }
        static HeapItem Pop(List<HeapItem> h)
        {
            var top = h[0]; var last = h[h.Count - 1]; h.RemoveAt(h.Count - 1);
            if (h.Count > 0)
            {
                h[0] = last; int k = 0;
                for (;;)
                {
                    int m = k, l = 2 * k + 1, r = l + 1;
                    if (l < h.Count && h[l].f < h[m].f) m = l;
                    if (r < h.Count && h[r].f < h[m].f) m = r;
                    if (m == k) break;
                    var t = h[m]; h[m] = h[k]; h[k] = t; k = m;
                }
            }
            return top;
        }

        // 网格 A*（八方向、不许擦墙角）+ 拉直。返回途经点（不含起点），找不到返回 null
        public List<Vector2> FindPath(Vector2 origin, Vector2 target)
        {
            int sx, sy, gx, gy;
            if (!NearestCell(origin, out sx, out sy) || !NearestCell(target, out gx, out gy)) return null;
            int W0 = Gw, N = Gw * Gh, si = sy * W0 + sx, gi = gy * W0 + gx;
            var cost = new float[N]; for (int i = 0; i < N; i++) cost[i] = 1e9f;
            var prev = new int[N]; for (int i = 0; i < N; i++) prev[i] = -1;
            var closed = new bool[N];
            var heap = new List<HeapItem>();
            Func<int, float> hx = i =>
            {
                int dx = Mathf.Abs(i % W0 - gx), dy = Mathf.Abs(i / W0 - gy);
                return Mathf.Max(dx, dy) + 0.414f * Mathf.Min(dx, dy);
            };
            cost[si] = 0; Push(heap, hx(si), si);
            bool found = false;
            while (heap.Count > 0)
            {
                int i = Pop(heap).i; if (closed[i]) continue; closed[i] = true;
                if (i == gi) { found = true; break; }
                int x = i % W0, y = i / W0;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dy == 0) continue;
                        int nx = x + dx, ny = y + dy; if (!CellOk(nx, ny)) continue;
                        if (dx != 0 && dy != 0 && (!CellOk(x + dx, y) || !CellOk(x, y + dy))) continue;
                        int j = ny * W0 + nx; if (closed[j]) continue;
                        float c = cost[i] + (dx != 0 && dy != 0 ? 1.414f : 1f);
                        if (c < cost[j]) { cost[j] = c; prev[j] = i; Push(heap, c + hx(j), j); }
                    }
            }
            if (!found) return null;
            var pts = new List<Vector2>();
            for (int i = gi; i != -1; i = prev[i]) pts.Add(CellCenter(i % W0, i / W0));
            pts.Reverse();
            if (Inside(origin)) pts[0] = origin; else pts.Insert(0, origin);
            if (Inside(target)) pts[pts.Count - 1] = target;
            var res = new List<Vector2> { pts[0] };
            int a = 0;
            while (a < pts.Count - 1)
            {
                int b = pts.Count - 1;
                while (b > a + 1 && !Los(pts[a], pts[b])) b--;
                res.Add(pts[b]); a = b;
            }
            res.RemoveAt(0);
            return res;
        }
    }
}
