using System.Collections.Generic;
using Newtonsoft.Json;

// 数据模型：字段名和 data/data.json 里的一一对应（Newtonsoft 按字段名反序列化，JSON 里多出来的字段直接忽略）。
// 坐标一律是“底图原图像素 / k”，乘 scale*k 得到舞台像素（见 MapRuntime）。
namespace GuiQin
{
    public class RoadDef { public float r; public float[][] pts; }
    public class BlockDef { public float[] at; public float r; }
    public class NpcPlace { public string id; public float[] at; }
    public class FoePlace { public string stage; public float[] at; public int face = 1; }

    public class PortalDef
    {
        public string to, name, art, needQuest, lockMsg;
        public float[] at, arrive;
        public int? needStep;
    }

    public class WaterZone { public float[] rect, dir; public float speed; }
    public class WaterDef { public float[] dir; public float speed; public List<WaterZone> zones; }
    public class FogDef { public int n = 26; public float lo = 0.25f, hi = 0.8f; }
    public class FadeZone { public float[][] poly; [JsonProperty("base")] public float baseY; }

    public class MapDef
    {
        public string name, img, mini, alt, battleBg, note, home;
        public float iw, ih, scale = 1f, k = 1f;
        public float[] start, world;
        public float[][][] walk;
        public float[][][] opens;
        public List<RoadDef> roads;
        public List<BlockDef> blocks;
        public List<NpcPlace> npcs;
        public List<PortalDef> portals;
        public List<FoePlace> foes;
        public float[][] glows;
        public float[][] slots;
        public bool sect, indoor, petals;
        public int tower, butterflies;
        public WaterDef water;
        public FogDef fog;
        public List<FadeZone> fade;
    }

    public class NpcInfo
    {
        public string name, title, color, titleColor, sprite, head, idle, shop, act, hall, restMsg, escort;
        public bool rest, spot;
    }

    public class EnemyDef { public string name, sprite; public float h; public bool boss; }

    public class StageDef
    {
        public string id, name, desc;
        public int rec, n;
        public bool tower, story;
        public string need;
        public List<string> enemies;
    }

    // 碰撞位图：bits 是 base64，1 = 可走
    public class MaskDef
    {
        public int cell, w, h;
        public string bits;
    }

    // 素材清单：key → 文件名（Resources/GuiQin/Art 下，不含扩展名也能找）；size：key → [宽, 高, 头部中心x, 帧数]
    public class AssetsMeta
    {
        public Dictionary<string, string> img;
        public Dictionary<string, float[]> size;
        public Dictionary<string, PetMeta> pet;          // 神兽战斗图集：键是战斗精灵名（b_ss_bs20 …）
    }

    // 神兽逐帧图集（原作高清帧）：cols×rows 个格子，每格 cw×ch；ox 是格子里“脚底中点”的横坐标，by 是脚底离格子底边的高度；H 是一格对应的“站立高度”，
    // 战斗里按 单位高度 / H 缩放。seq：动作名 → [[帧号, 毫秒], …]；rel：动作名 → 出招（释放）帧在序列里的下标。
    public class PetMeta
    {
        public string sheet; public int cols, rows; public float cw, ch, ox, by, H;
        public Dictionary<string, List<float[]>> seq; public Dictionary<string, int> rel; public PfxMeta pfx;
    }
    // 技能弹道特效图集：n 帧横排，每帧 cw×ch；map：技能 → 要循环的帧号；sc：技能 → 缩放
    public class PfxMeta { public string sheet; public int n; public float cw, ch; public Dictionary<string, int[]> map; public Dictionary<string, float> sc; }
}
