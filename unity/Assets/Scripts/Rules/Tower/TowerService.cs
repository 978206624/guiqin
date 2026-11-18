using System;
using GuiQin.Net;

namespace GuiQin.Tower
{
    public static class TowerCmd { public const string Enter = "tower.enter", Next = "tower.next", Leave = "tower.leave"; }

    // 百炼玄兵塔：七层，每层十名守卫，按顺序挑战；第十名是本层首领，打败后才能上楼。进度 role.tower = 最高通关守卫编号（0～70）。
    public class TowerService
    {
        readonly RoleState role;
        public TowerService(RoleState role) { this.role = role; }

        public static int MinLv { get { return (int)GameData.Raw["tower"]["minLv"]; } }
        public static int FloorOf(int progress) { return Math.Max(1, Math.Min(7, (int)Math.Ceiling(progress / 10.0))); }

        // 返回目的地地图（null = 不行，msg 是原因）
        public string Enter(out string msg)
        {
            msg = null;
            if (role.lv < MinLv) { msg = "塔里的守卫不是闹着玩的。等你到 " + MinLv + " 级再来（你现在 " + role.lv + " 级）。"; return null; }
            return "tower" + FloorOf(role.tower);
        }

        public string Next(out string msg)
        {
            msg = null; int f = CurFloor();
            if (f <= 0) { msg = "你不在塔里"; return null; }
            if (f >= 7 || role.tower < f * 10) { msg = "要上楼，先打败本层的首领（第 " + f + " 层第十名守卫）。"; return null; }
            return "tower" + (f + 1);
        }

        public bool Leave(out string msg) { msg = null; if (CurFloor() <= 0) { msg = "你不在塔里"; return false; } return true; }

        int CurFloor()
        {
            MapDef m; return GameData.Maps.TryGetValue(role.map, out m) ? m.tower : 0;
        }
    }
}
