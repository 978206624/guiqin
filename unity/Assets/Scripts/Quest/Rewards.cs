using System.Collections.Generic;
using GuiQin.Inventory;
using GuiQin.Net;

namespace GuiQin.Quest
{
    // 发奖励：经验 / 银两 / 物品，升级。战斗结算和任务奖励共用。返回是否升级。
    public static class Rewards
    {
        public static int ExpNeed(int lv) { return (int)GameData.Raw["expNeed"]["base"] + (int)GameData.Raw["expNeed"]["perLevel"] * lv; }

        public static bool Grant(RoleState role, int exp, int coin, Dictionary<string, int> items, out List<string> lost)
        {
            lost = new List<string>(); bool up = false;
            role.coin += coin; role.exp += exp;
            if (items != null)
            {
                var inv = new InventoryService(role);
                foreach (var kv in items) if (!inv.Give(kv.Key, kv.Value)) lost.Add(kv.Key);
            }
            while (role.exp >= ExpNeed(role.lv)) { role.exp -= ExpNeed(role.lv); role.lv++; up = true; }
            if (up) { role.hp = -1; role.mp = -1; }
            return up;
        }
    }
}
