using System.Collections.Generic;
using GuiQin.Net;

namespace GuiQin.Inventory
{
    public class GearState { public int star; public List<string> gems = new List<string>(); }

    // 背包 / 装备 / 锻造状态：服务端权威，客户端只显示。位置（槽）→ 装备 id；星级和宝石跟着“部位”走，换装不丢。
    public class BagState
    {
        public Dictionary<string, int> items = new Dictionary<string, int>();      // 背包里的物品（未穿戴）
        public Dictionary<string, string> equip = new Dictionary<string, string>();  // 部位 → 装备 id
        public Dictionary<string, GearState> gear = new Dictionary<string, GearState>();
        public int manaReserve;                                                    // 回气丹转存的法力点数
        public int cap = 40;                                                        // 格子数（不同物品各占一格，同种叠加）
    }

    public class BagOpReq { public string shop, id, slot, gem; public int i, quantity, capacity; public bool storeInPouch; }      // 各命令用到哪几个字段见 InventoryService
    public class BagResp { public bool ok; public string msg; public BagState bag; public int coin, yuanbao, silverNote, hp = -1, mp = -1, star = -1; public Dictionary<string, int> petMana; }

    public class MallOffer
    {
        public string id, item, name, currency, color, kind, desc;
        public int price, level;
        public bool bound;

        public int Balance(RoleState role) => currency == "coin" ? role.coin : currency == "yuanbao" ? role.yuanbao : role.silverNote;
        public static string CurrencyName(string currency) => currency == "coin" ? "银两" : currency == "yuanbao" ? "元宝" : "银票";
    }

    public class MallPurchase
    {
        public MallOffer Offer;
        public int BalanceBefore, BalanceAfter, CountBefore, CountAfter;
    }

    public static class BagCmd
    {
        public const string Get = "bag.get";
        public const string Expand = "bag.expand";
        public const string Use = "bag.use";
        public const string Equip = "bag.equip";
        public const string Unequip = "bag.unequip";
        public const string Sell = "bag.sell";
        public const string Sort = "bag.sort";
        public const string ShopBuy = "shop.buy";
        public const string ShopSell = "shop.sell";
        public const string MallBuy = "mall.buy";
        public const string Forge = "forge.make";
        public const string Inlay = "forge.inlay";
        public const string Uninlay = "forge.uninlay";
        public const string Merge = "forge.merge";
        public const string Gift = "gift.daily";       // 每日福利：银两 + 金创药，一天一次（服务端按日期判）
        public const string Rest = "role.rest";        // 找客栈老板 / 阿秀歇脚：气血法力回满
    }
}
