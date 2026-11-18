using System.Collections.Generic;
using UnityEngine;

namespace GuiQin.Battle
{
    // 神兽逐帧动画：把图集切成一帧一个 Sprite（按“站立高度 H”标定，使用时再乘 单位高度 / H）。待机循环由 BattleController.Update 驱动，
    // 攻击 / 技能 / 受击 / 闪避由 BattleController.PetPlay 插播（Playing=true 期间待机不动），播完回待机。
    public class PetAnim
    {
        public PetMeta M; public Sprite[] Frames; public bool Playing; public int Gen; public float K;
        float idleT; int idleI;
        static readonly Dictionary<string, Sprite[]> petCache = new Dictionary<string, Sprite[]>(), pfxCache = new Dictionary<string, Sprite[]>();

        public static PetAnim Create(PetMeta m, float unitH)
        {
            if (m == null || m.seq == null || !m.seq.ContainsKey("idle") || m.seq["idle"].Count == 0) return null;
            Sprite[] fr;
            if (!petCache.TryGetValue(m.sheet, out fr)) { fr = Cut(m); petCache[m.sheet] = fr; }
            if (fr == null) return null;
            return new PetAnim { M = m, Frames = fr, K = unitH / m.H };
        }

        // 切格子：图片左上角是第 0 格，往右、往下数；Unity 的 Rect 原点在左下，所以行号要翻一下
        static Sprite[] Cut(PetMeta m)
        {
            var tex = Art.Tex(m.sheet); if (tex == null) return null;
            int cw = tex.width / m.cols, ch = tex.height / m.rows;
            float ppu = cw / (m.cw / Art.PX);                       // 一格在世界里 m.cw 个舞台像素宽（k = 1 时）
            var pivot = new Vector2(m.ox / m.cw, m.by / m.ch);
            var arr = new Sprite[m.cols * m.rows];
            for (int f = 0; f < arr.Length; f++)
            {
                int c = f % m.cols, r = f / m.cols;
                arr[f] = Sprite.Create(tex, new Rect(c * cw, (m.rows - 1 - r) * ch, cw, ch), pivot, ppu, 0, SpriteMeshType.FullRect);
            }
            return arr;
        }

        public static Sprite[] Pfx(PfxMeta p)
        {
            if (p == null || string.IsNullOrEmpty(p.sheet)) return null;
            Sprite[] fr; if (pfxCache.TryGetValue(p.sheet, out fr)) return fr;
            var tex = Art.Tex(p.sheet);
            if (tex != null)
            {
                int cw = tex.width / p.n; float ppu = cw / (p.cw / Art.PX); fr = new Sprite[p.n];
                for (int i = 0; i < p.n; i++) fr[i] = Sprite.Create(tex, new Rect(i * cw, 0, cw, tex.height), new Vector2(0f, 0.5f), ppu, 0, SpriteMeshType.FullRect);   // 原图朝右，枢轴在左边中点，方便按方向旋转
            }
            pfxCache[p.sheet] = fr; return fr;
        }

        public Sprite Frame(int f) { return Frames[Mathf.Clamp(f, 0, Frames.Length - 1)]; }

        public Sprite IdleSprite(float dt)
        {
            var seq = M.seq["idle"]; idleT += dt;
            if (idleI >= seq.Count) idleI = 0;
            if (idleT >= seq[idleI][1] / 1000f) { idleT = 0f; idleI = (idleI + 1) % seq.Count; }
            return Frame((int)seq[idleI][0]);
        }
    }
}
