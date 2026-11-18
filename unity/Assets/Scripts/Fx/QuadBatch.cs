using UnityEngine;

namespace GuiQin
{
    // 一批四边形（顶点色 + 同一张贴图）一次画出：雨丝、雪点、雾团、蝴蝶、发光点……都用它。
    // 每帧 Begin() → 若干 Quad()/Line() → End()。坐标是父物体的本地坐标（单位：世界单位）。
    public class QuadBatch
    {
        public readonly GameObject Go;
        readonly Mesh mesh; readonly MeshRenderer mr;
        readonly Vector3[] v; readonly Color32[] c; readonly Vector2[] uv; readonly int[] idx;
        readonly int cap; int n;
        static Shader shader;

        public QuadBatch(string name, Transform parent, Texture tex, bool additive, int sortingOrder, int capacity)
        {
            cap = capacity;
            v = new Vector3[cap * 4]; c = new Color32[cap * 4]; uv = new Vector2[cap * 4]; idx = new int[cap * 6];
            for (int i = 0; i < cap; i++)
            {
                int a = i * 4, b = i * 6;
                idx[b] = a; idx[b + 1] = a + 1; idx[b + 2] = a + 2; idx[b + 3] = a; idx[b + 4] = a + 2; idx[b + 5] = a + 3;
            }
            Go = new GameObject(name); Go.transform.SetParent(parent, false);
            mesh = new Mesh { name = name }; mesh.MarkDynamic();
            Go.AddComponent<MeshFilter>().sharedMesh = mesh;
            mr = Go.AddComponent<MeshRenderer>();
            if (shader == null) shader = Shader.Find("GuiQin/Unlit");
            var mat = new Material(shader);
            mat.mainTexture = tex;
            mat.SetFloat("_Src", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_Dst", (float)(additive ? UnityEngine.Rendering.BlendMode.One : UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha));
            mr.sharedMaterial = mat; mr.sortingOrder = sortingOrder;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
        }

        public void SetVisible(bool on) { if (Go.activeSelf != on) Go.SetActive(on); }
        public void Begin() { n = 0; }

        // 中心 + 半宽半高 + 旋转（度，逆时针）
        public void Quad(Vector2 center, float halfW, float halfH, float rotDeg, Color32 col)
        {
            if (n >= cap) return;
            float r = rotDeg * Mathf.Deg2Rad, cs = Mathf.Cos(r), sn = Mathf.Sin(r);
            Vector2 ax = new Vector2(cs * halfW, sn * halfW), ay = new Vector2(-sn * halfH, cs * halfH);
            int i = n * 4;
            v[i] = new Vector3(center.x - ax.x - ay.x, center.y - ax.y - ay.y, 0f);
            v[i + 1] = new Vector3(center.x + ax.x - ay.x, center.y + ax.y - ay.y, 0f);
            v[i + 2] = new Vector3(center.x + ax.x + ay.x, center.y + ax.y + ay.y, 0f);
            v[i + 3] = new Vector3(center.x - ax.x + ay.x, center.y - ax.y + ay.y, 0f);
            uv[i] = new Vector2(0, 0); uv[i + 1] = new Vector2(1, 0); uv[i + 2] = new Vector2(1, 1); uv[i + 3] = new Vector2(0, 1);
            c[i] = c[i + 1] = c[i + 2] = c[i + 3] = col;
            n++;
        }

        // 一条粗线（雨丝）
        public void Line(Vector2 a, Vector2 b, float width, Color32 col)
        {
            Vector2 d = b - a; float len = d.magnitude; if (len < 1e-5f) return;
            Quad((a + b) * 0.5f, len * 0.5f, width * 0.5f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, col);
        }

        public void End()
        {
            mesh.Clear();
            if (n == 0) return;
            mesh.SetVertices(v, 0, n * 4);
            mesh.SetColors(c, 0, n * 4);
            mesh.SetUVs(0, uv, 0, n * 4);
            mesh.SetIndices(idx, 0, n * 6, MeshTopology.Triangles, 0, false);
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(4000f, 4000f, 10f));
        }

        public static Color32 Col(float r, float g, float b, float a)
        {
            return new Color32((byte)Mathf.RoundToInt(Mathf.Clamp01(r) * 255f), (byte)Mathf.RoundToInt(Mathf.Clamp01(g) * 255f), (byte)Mathf.RoundToInt(Mathf.Clamp01(b) * 255f), (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
        }
        public static Color32 Rgb(int r, int g, int b, float a) { return Col(r / 255f, g / 255f, b / 255f, a); }
    }
}
