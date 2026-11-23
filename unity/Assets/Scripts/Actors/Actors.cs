using UnityEngine;
using GuiQin.Quest;

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
        public int Sb = -1;                      // >=0：剧情临时演员（首领 / 采集点），值是它对应的节拍序号
        public bool Done;                        // 玄兵塔里已通关的守卫（变灰，不能再打）
        public bool Hidden;                      // 剧情里和首领同名的 NPC：战斗前后只显示一个
        public bool Marked;                      // 小地图上的任务寻路目标
        public QuestNpcMark QuestMark;           // NPC 的任务接取或交付状态
        public StageDef StageInfo;               // 敌人：对应的关卡
        public PortalDef Portal;
        public NpcInfo Info;
        protected Transform bodyT;
        float thruA = 1f;
        float bodyScale = 1f;

        // 躲在屋顶 / 栏杆后面时变半透明（网页版 .thru：身体 .42、影子 .3）
        public void ApplyThru(bool thru, float dt)
        {
            float tg = thru ? 0.42f : 1f;
            if (Mathf.Approximately(thruA, tg) && (thruA >= 1f)) return;
            thruA = Mathf.MoveTowards(thruA, tg, dt / 0.25f * 0.58f);
            if (Body != null) { var c = Body.color; c.a = thruA; Body.color = c; }
            if (Shadow != null) { var c = Shadow.color; c.a = 0.35f * Mathf.Lerp(1f, 0.3f, (1f - thruA) / 0.58f); Shadow.color = c; }
        }

        public static T Make<T>(string name, Transform parent, Sprite sprite, Vector2 pos, float shadowW, float scale = 1f) where T : Actor
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            var a = go.AddComponent<T>();
            var sh = new GameObject("shadow"); sh.transform.SetParent(go.transform, false);
            a.Shadow = sh.AddComponent<SpriteRenderer>(); a.Shadow.sprite = Art.Dot(); a.Shadow.color = new Color(0, 0, 0, 0.35f);
            sh.transform.localScale = new Vector3(shadowW / Art.PX, shadowW * 0.3f / Art.PX, 1f);
            var b = new GameObject("body"); b.transform.SetParent(go.transform, false);
            a.bodyT = b.transform; a.Body = b.AddComponent<SpriteRenderer>(); a.Body.sprite = sprite;
            a.bodyScale = scale; b.transform.localScale = new Vector3(scale, scale, 1f);
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
            Tag.Set(Title, TitleColor, DisplayName, NameColor);
            Tag.SetQuestMarker(QuestMark);
        }

        // 站着时轻微呼吸，走路时上下起伏（宠物、NPC 用）；主角在子类里换帧
        public virtual void Animate(float t)
        {
            float bob = Moving ? Mathf.Abs(Mathf.Sin(t * 11f)) * Hop : 0f;
            float br = Moving ? 1f : 1f + Mathf.Sin(t * 2.2f + Pos.x) * 0.012f;
            bodyT.localPosition = new Vector3(0, bob / Art.PX, 0);
            bodyT.localScale = new Vector3(Face * bodyScale, br * bodyScale, 1f);
        }
    }

    // 野怪只在出生点附近走短直线；沿途用地图的同一套通行判定。
    public class RoamingFoeActor : Actor
    {
        const float Radius = 110f;
        const float Speed = 46f;
        const float PortalClearance = 72f;

        Vector2 home, destination;
        float wait;
        int rightFacing;

        public void InitRoam(bool spriteFacesLeft)
        {
            home = Pos;
            rightFacing = spriteFacesLeft ? -1 : 1;
            wait = Random.Range(0.8f, 2.8f);
            Hop = 3f;
        }

        public void PauseRoam(Vector2 playerPos)
        {
            Moving = false;
            wait = Random.Range(1.2f, 2.8f);
            FaceToward(playerPos);
        }

        void FaceToward(Vector2 point)
        {
            if (Mathf.Abs(point.x - Pos.x) > 1f) Face = point.x > Pos.x ? rightFacing : -rightFacing;
        }

        public void TickRoam(MapRuntime map, float dt)
        {
            if (!Moving)
            {
                wait -= dt;
                if (wait > 0f) return;

                wait = Random.Range(1.2f, 2.8f);
                for (int i = 0; i < 8; i++)
                {
                    var target = home + Random.insideUnitCircle * Radius;
                    if (!CanWalk(map, target)) continue;
                    destination = target;
                    FaceToward(target);
                    Moving = true;
                    break;
                }
                if (!Moving) return;
            }

            Pos = Vector2.MoveTowards(Pos, destination, Speed * dt);
            if (Pos == destination) Moving = false;
            Sync();
        }

        bool CanWalk(MapRuntime map, Vector2 target)
        {
            var delta = target - Pos;
            float distance = delta.magnitude;
            if (distance < 24f || distance > 100f) return false;

            // 整条短路线都要绕开传送阵，而不只是落脚点。
            if (map.Def.portals != null)
                foreach (var portal in map.Def.portals)
                {
                    var center = map.ToMap(portal.at);
                    float t = Mathf.Clamp01(Vector2.Dot(center - Pos, delta) / delta.sqrMagnitude);
                    if ((center - (Pos + delta * t)).sqrMagnitude < PortalClearance * PortalClearance) return false;
                }

            int steps = Mathf.CeilToInt(distance / 4f);
            for (int i = 0; i <= steps; i++)
                if (!map.Inside(Pos + delta * ((float)i / steps))) return false;
            return true;
        }
    }

    public class PortalActor : Actor
    {
        static Sprite[] frames;

        // Cocos 图集按原始画布还原为两行五列，保留每帧的中心和偏移。
        public static Sprite FirstFrame()
        {
            if (frames == null)
            {
                const int cell = 256;
                var texture = Resources.Load<Texture2D>("GuiQin/Art/eff_transport");
                if (texture == null || texture.width != cell * 5 || texture.height != cell * 2)
                    return Art.One("portal_array", true) ?? Art.Dot();
                frames = new Sprite[10];
                for (int i = 0; i < frames.Length; i++)
                    frames[i] = Sprite.Create(texture, new Rect(i % 5 * cell, (1 - i / 5) * cell, cell, cell),
                        new Vector2(0.5f, 0.5f), Art.PX, 0, SpriteMeshType.FullRect);
            }
            return frames[0];
        }

        public void SetGroundEffect()
        {
            H = 104f;
            Shadow.enabled = false;
            Body.sortingOrder = -5000;
        }

        public override void Animate(float t)
        {
            if (frames != null) Body.sprite = frames[Mathf.FloorToInt(t * 12f) % frames.Length];
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
        public float FrontScale = 1f, BackScale = 1f;      // 正面 / 背影图各自缩到想要的身高（神兽立绘比灵狐大得多）
        public char Dir = 'f';

        public override void Animate(float t)
        {
            base.Animate(t);
            bool back = Dir == 'b' && SpriteB != null;
            Body.sprite = back ? SpriteB : SpriteF;
            var s = bodyT.localScale; float k = back ? BackScale : FrontScale;
            bodyT.localScale = new Vector3((Dir == 'b' ? -Face : Face) * k, s.y * k, 1f);
        }
    }
}
