using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GuiQin
{
    // 用实际途经点绘制剩余路线，两种地图共用一层网格，避免给每个路点创建物体。
    internal sealed class MapRouteGraphic : MaskableGraphic
    {
        static readonly Color32 Gold = new Color32(255, 210, 74, 255);
        static readonly Color32 Shadow = new Color32(71, 37, 23, 255);

        Vector2 player, mapSize;
        IReadOnlyList<Vector2> route;
        int pointCount;

        public static MapRouteGraphic Create(RectTransform parent)
        {
            var rect = Ui.Rect("route", parent);
            Ui.Anchor(rect, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var graphic = rect.gameObject.AddComponent<MapRouteGraphic>();
            graphic.raycastTarget = false;
            return graphic;
        }

        public void Show(Vector2 origin, IReadOnlyList<Vector2> points, Vector2 size)
        {
            int count = points?.Count ?? 0;
            if (count == pointCount && (count == 0 || (points == route && origin == player && size == mapSize)))
                return;
            player = origin;
            mapSize = size;
            route = points;
            pointCount = count;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            if (route == null || route.Count == 0 || mapSize.x <= 0 || mapSize.y <= 0) return;

            var rect = rectTransform.rect;
            Vector2 MapPoint(Vector2 pos) => new Vector2(
                rect.xMin + pos.x * rect.width / mapSize.x,
                rect.yMax - pos.y * rect.height / mapSize.y);

            Vector2 end = MapPoint(route[route.Count - 1]);
            Vector2 from = end;
            float gap = 12f;
            int dots = 0;
            // 从终点向角色铺点。跑过的点自然消失，其余点停在原处。
            for (int i = route.Count - 2; i >= -1 && dots < 1024; i--)
            {
                Vector2 to = MapPoint(i >= 0 ? route[i] : player);
                float length = Vector2.Distance(from, to);
                while (gap < length && dots < 1024)
                {
                    var point = Vector2.LerpUnclamped(from, to, gap / length);
                    Diamond(mesh, point, 3.5f, Shadow);
                    Diamond(mesh, point, 2.4f, Gold);
                    dots++;
                    gap += 9f;
                }
                gap -= length;
                from = to;
            }

            Diamond(mesh, end, 8f, Shadow);
            Diamond(mesh, end, 6f, Gold);
            Diamond(mesh, end, 2.4f, Shadow);
        }

        static void Diamond(VertexHelper mesh, Vector2 point, float radius, Color32 color)
        {
            int first = mesh.currentVertCount;
            mesh.AddVert(point + Vector2.up * radius, color, Vector2.zero);
            mesh.AddVert(point + Vector2.right * radius, color, Vector2.zero);
            mesh.AddVert(point + Vector2.down * radius, color, Vector2.zero);
            mesh.AddVert(point + Vector2.left * radius, color, Vector2.zero);
            mesh.AddTriangle(first, first + 1, first + 2);
            mesh.AddTriangle(first, first + 2, first + 3);
        }
    }
}
