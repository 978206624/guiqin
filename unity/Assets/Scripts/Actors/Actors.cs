using UnityEngine;

namespace GuiQin
{
    public enum ActorKind { Player, Pet, Npc, Foe, Portal }

    // 逻辑坐标 = 舞台像素（y 向下）；世界坐标 = 舞台像素 / 100，y 取反
    public static class Stage
    {
        public static Vector3 ToWorld(Vector2 p) { return new Vector3(p.x / Art.PX, -p.y / Art.PX, 0f); }
        public static Vector2 FromWorld(Vector3 w) { return new Vector2(w.x * Art.PX, -w.y * Art.PX); }
    }

    // 场景里的一个“角色”：NPC / 地图上的敌人 / 传送点 / 主角 / 宠物都是它。
    public class Actor : MonoBehaviour
    {
        public ActorKind Kind;
        public string Id, DisplayName, Title;
        public Color NameColor = new Color(1f, 0.88f, 0.4f), TitleColor = new Color(1f, 0.69f, 0.35f);
        public Vector2 Pos;
        public int Face = 1;
        public bool Moving;
        public float H = 80f;                    // 身高（舞台像素），名牌、点击判定用
        public float Hop = 6f;                   // 走路时的上下起伏（舞台像素）
        public SpriteRenderer Body, Shadow;
        public TagLabel Tag;
        public bool Marked;                      // 头顶感叹号（任务）
        public StageDef StageInfo;               // 敌人：对应的关卡
        public PortalDef Portal;
        public NpcInfo Info;
        protected Transform bodyT;
        float thruA = 1f;

        // 躲在屋顶 / 栏杆后面时变半透明（网页版 .thru：身体 .42、影子 .3）
        public void ApplyThru(bool thru, float dt)
        {
            float tg = thru ? 0.42f : 1f;
            if (Mathf.Approximately(thruA, tg) && (thruA >= 1f)) return;
            thruA = Mathf.MoveTowards(thruA, tg, dt / 0.25f * 0.58f);
            if (Body != null) { var c = Body.color; c.a = thruA; Body.color = c; }
            if (Shadow != null) { var c = Shadow.color; c.a = 0.35f * Mathf.Lerp(1f, 0.3f, (1f - thruA) / 0.58f); Shadow.color = c; }
        }

        public static T Make<T>(string name, Transform parent, Sprite sprite, Vector2 pos, float shadowW) where T : Actor
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            var a = go.AddComponent<T>();
            var sh = new GameObject("shadow"); sh.transform.SetParent(go.transform, false);
            a.Shadow = sh.AddComponent<SpriteRenderer>(); a.Shadow.sprite = Art.Dot(); a.Shadow.color = new Color(0, 0, 0, 0.35f);
            sh.transform.localScale = new Vector3(shadowW / Art.PX, shadowW * 0.3f / Art.PX, 1f);
            var b = new GameObject("body"); b.transform.SetParent(go.transform, false);
            a.bodyT = b.transform; a.Body = b.AddComponent<SpriteRenderer>(); a.Body.sprite = sprite;
            a.Pos = pos; a.Sync();
            return a;
        }

        public void Sync()
        {
            transform.position = GuiQin.Stage.ToWorld(Pos);
            int order = Mathf.RoundToInt(Pos.y);                  // 越靠下越在前
            if (Body != null) Body.sortingOrder = order;
            if (Shadow != null) Shadow.sortingOrder = order - 1;
        }

        public void SetTag()
        {
            if (Tag == null) return;
            Tag.Set((Marked ? "! " : "") + (Title ?? ""), TitleColor, DisplayName, NameColor);
        }

        // 站着时轻微呼吸，走路时上下起伏（宠物、NPC 用）；主角在子类里换帧
        public virtual void Animate(float t)
        {
            float bob = Moving ? Mathf.Abs(Mathf.Sin(t * 11f)) * Hop : 0f;
            float br = Moving ? 1f : 1f + Mathf.Sin(t * 2.2f + Pos.x) * 0.012f;
            bodyT.localPosition = new Vector3(0, bob / Art.PX, 0);
            bodyT.localScale = new Vector3(Face, br, 1f);
        }
    }

    public class PortalActor : Actor
    {
        public override void Animate(float t)
        {
            if (Body.sprite == Art.Dot()) { float s = 0.9f + Mathf.Sin(t * 3f) * 0.08f; bodyT.localScale = new Vector3(s, s * 0.55f, 1f); }
        }
    }

    // 主角：行走精灵表（正面 / 背面各一张），按走过的距离切帧
    public class PlayerActor : Actor
    {
        public const float StepLen = 26f;
        public string ClassId = "novice_m", KeyF = "walk_f", KeyB = "walk_b";
        public char Dir = 'f';
        public float Dist, MoveT;
        Sprite[] sheetF, sheetB;

        public void SetLook(string cls)
        {
            ClassId = cls;
            KeyF = Art.Size("walk_f_" + cls) != null ? "walk_f_" + cls : "walk_f";
            KeyB = Art.Size("walk_b_" + cls) != null ? "walk_b_" + cls : "walk_b";
            sheetF = Art.Sheet(KeyF); sheetB = Art.Sheet(KeyB);
            var sz = Art.Size(KeyF); if (sz != null) H = sz[1];
            Shadow.transform.localScale = new Vector3(0.5f, 0.15f, 1f);
        }

        public override void Animate(float t)
        {
            var sheet = Dir == 'b' ? sheetB : sheetF; var key = Dir == 'b' ? KeyB : KeyF;
            if (sheet == null) return;
            float stp = Dir == 'b' && ClassId.StartsWith("dao_") ? 24f : StepLen;      // 道家背面步幅小：步频加快，脚不滑
            int nf = Art.WalkFrames(key);
            int frame = Moving ? Mathf.FloorToInt(Dist / (stp * 4f / nf)) % nf : Art.IdleFrame(key);       // 站着用“待机帧”
            float bob = Moving && Dir == 'b' ? Mathf.Abs(Mathf.Sin(Dist / stp * Mathf.PI / 2f)) * 3f : 0f;
            Body.sprite = sheet[Mathf.Clamp(frame, 0, sheet.Length - 1)];
            bodyT.localPosition = new Vector3(0, bob / Art.PX, 0);
            bodyT.localScale = new Vector3(Dir == 'f' ? Face : -Face, 1f, 1f);          // 正面图朝右下，背影图朝左上：左右靠翻转
        }
    }

    // 跟在主角身后的宠物（灵狐）：正面 / 背影两张静态图
    public class PetActor : Actor
    {
        public Sprite SpriteF, SpriteB;
        public float BackScale = 1f;                 // 背影图比正面图大一点，按身高缩回去
        public char Dir = 'f';

        public override void Animate(float t)
        {
            base.Animate(t);
            bool back = Dir == 'b' && SpriteB != null;
            Body.sprite = back ? SpriteB : SpriteF;
            var s = bodyT.localScale; float k = back ? BackScale : 1f;
            bodyT.localScale = new Vector3((Dir == 'b' ? -Face : Face) * k, s.y * k, 1f);
        }
    }
}
