using System;
using GuiQin.Net;
using Newtonsoft.Json.Linq;

namespace GuiQin.Tower
{
    // 紫阴古城的准入值跟随守卫配置，层内通关记录在每次重新入场时清除。
    public static class ZiyinService
    {
        public const string Guard = "fb50_guard", Entrance = "zy1";
        public static JToken Config => GameData.Raw["npcs"][Guard]["fb"];

        public static string Enter(RoleState role, DateTime utcNow)
        {
            int lv = (int)Config["lv"], fee = (int)Config["fee"], limit = (int)Config["daily"];
            string today = utcNow.AddHours(8).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
            int entered = role.ziyinDay == today ? role.ziyinEntries : 0;
            if (role.lv < lv) return "达到 " + lv + " 级才能进入紫阴古城";
            if (entered >= limit) return "今天的紫阴古城进入次数已用完";
            if (role.coin < fee) return "银两不够";
            role.coin -= fee;
            role.ziyinDay = today;
            role.ziyinEntries = entered + 1;
            string prefix = (string)Config["prefix"];
            role.cleared.RemoveAll(stage => stage.StartsWith(prefix, StringComparison.Ordinal));
            return null;
        }
    }
}
