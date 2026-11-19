using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using GuiQin.Net;

namespace GuiQin.Inventory
{
    // 背包 / 装备 / 商店 / 锻造 / 宝石 的服务端逻辑（参考实现）。每个方法：校验 → 改状态 → 返回 (ok, msg)。
    // 真服务器在这里还要：数据库事务、物品流水（item_flow）、并发锁。规则对应网页版 game.js 的 ACTS.* / forgeDo / usePotion。
    public class InventoryService
    {
        readonly RoleState role; readonly Random rng = new Random();
        BagState Bag { get { return role.bag; } }
        static JToken D { get { return GameData.Raw; } }
        static JToken Item(string id) { return id == null ? null : D["items"][id]; }
        static string Name(string id) { var it = Item(id); return it != null ? (string)it["name"] : id; }
        static string Type(JToken it) { return (string)it["type"]; }

        public string Msg; public int Star = -1;

        public InventoryService(RoleState role) { this.role = role; }

        public static int InitialCapacity => (int)D["bagSize"];
        public static int MaxCapacity => (int)D["bagExpand"]["max"];
        public static int ExpansionStep => (int)D["bagExpand"]["step"];
        public static int ExpansionPrice => (int)D["bagExpand"]["yuanbao"];

        // 旧存档补足基础格子，保留玩家已解锁的容量。
        public static void EnsureBaseCapacity(BagState bag) => bag.cap = Math.Max(bag.cap, InitialCapacity);

        // 新角色的初始背包（data.start）
        public static BagState NewBag()
        {
            var st = D["start"]; var b = new BagState();
            foreach (var p in ((JObject)st["items"]).Properties()) b.items[p.Name] = (int)p.Value;
            foreach (var p in ((JObject)st["equip"]).Properties()) b.equip[p.Name] = (string)p.Value;
            b.cap = InitialCapacity;
            return b;
        }

        bool Fail(string m) { Msg = m; return false; }
        bool Ok(string m) { Msg = m; return true; }
        int Count(string id) { int n; return Bag.items.TryGetValue(id, out n) ? n : 0; }
        void AddItem(string id, int n) { int c = Count(id) + n; if (c <= 0) Bag.items.Remove(id); else Bag.items[id] = c; }
        bool CanHold(string id) { return Bag.items.ContainsKey(id) || Bag.items.Count < Bag.cap; }
        GearState Gear(string slot) { GearState g; if (!Bag.gear.TryGetValue(slot, out g)) Bag.gear[slot] = g = new GearState(); return g; }
        StatBlock Stats() { return RoleStats.Compute(role.cls, role.lv, Bag); }
        int Hp { get { return role.hp < 0 ? Stats().maxHp : role.hp; } }
        int Mp { get { return role.mp < 0 ? Stats().maxMp : role.mp; } }
        static int SocketCount(int star) { return ((JArray)D["forge"]["sockets"]).Count(n => star >= (int)n); }

        // 战斗里 / 背包里用药：返回 [气血, 法力] 的恢复量
        public bool Use(string id)
        {
            var it = Item(id); if (it == null || Count(id) <= 0) return Fail("没有这个物品");
            if (Type(it) != "potion") return Fail("这个物品不能直接使用");
            var st = Stats(); int hp = it["hp"] != null ? (int)it["hp"] : 0, mp = it["mp"] != null ? (int)it["mp"] : 0;
            if ((hp > 0 && Hp >= st.maxHp && mp == 0) || (mp > 0 && Mp >= st.maxMp && hp == 0)) return Fail(hp > 0 ? "气血已满" : "法力已满");
            AddItem(id, -1);
            role.hp = Math.Min(st.maxHp, Hp + hp); role.mp = Math.Min(st.maxMp, Mp + mp);
            return Ok("使用" + Name(id) + (hp > 0 ? "，气血 +" + hp : "") + (mp > 0 ? "，法力 +" + mp : ""));
        }

        // 战斗中使用：成功返回 (hp, mp) 恢复量，不检查“已满”
        public bool UseInBattle(string id, out int hp, out int mp)
        {
            hp = mp = 0; var it = Item(id);
            if (it == null || Type(it) != "potion" || Count(id) <= 0) return false;
            hp = it["hp"] != null ? (int)it["hp"] : 0; mp = it["mp"] != null ? (int)it["mp"] : 0;
            AddItem(id, -1); return true;
        }
        public int ItemCount(string id) { return Count(id); }
        public bool Take(string id, int n) { if (n <= 0 || Count(id) < n) return false; AddItem(id, -n); return true; }

        // 药囊：战斗打赢后自动回血，回复量 = 药囊自带气血（含星级加成）。护符先不做，坑位保留
        public int PouchHeal()
        {
            float v; if (!RoleStats.SlotBonus(Bag, "pouch").TryGetValue("hp", out v) || v <= 0) return 0;
            int max = Stats().maxHp, cur = Hp, add = Math.Min((int)v, max - cur);
            if (add <= 0) return 0;
            role.hp = cur + add; return add;
        }

        // 战斗掉落 / 任务奖励入包：满了装不下的丢弃（真服务器应走邮件）
        public bool Give(string id, int n)
        {
            if (Item(id) == null || n <= 0) return false;
            if (!CanHold(id)) return false;
            AddItem(id, n); return true;
        }

        // 战斗中带进去的药品清单（开战时快照，供指令面板显示）
        public Dictionary<string, int> Potions()
        {
            var o = new Dictionary<string, int>();
            foreach (var kv in Bag.items) { var it = Item(kv.Key); if (it != null && Type(it) == "potion") o[kv.Key] = kv.Value; }
            return o;
        }

        public bool Equip(string id)
        {
            var it = Item(id); if (it == null || Type(it) != "equip" || it["slot"] == null) return Fail("这个不能装备");
            if (Count(id) <= 0) return Fail("背包里没有这件装备");
            string slot = (string)it["slot"], old; Bag.equip.TryGetValue(slot, out old);
            AddItem(id, -1); if (!string.IsNullOrEmpty(old)) AddItem(old, 1);
            Bag.equip[slot] = id;
            role.hp = Math.Min(Hp, Stats().maxHp);
            return Ok("装备了" + Name(id));
        }

        public bool Unequip(string slot)
        {
            string id; if (!Bag.equip.TryGetValue(slot, out id) || string.IsNullOrEmpty(id)) return Fail("这个部位是空的");
            if (!CanHold(id)) return Fail("背包已满");
            Bag.equip.Remove(slot); AddItem(id, 1);
            role.hp = Math.Min(Hp, Stats().maxHp);
            return Ok("卸下了" + Name(id));
        }

        public bool Expand(int expectedCapacity)
        {
            if (Bag.cap >= MaxCapacity) return Fail("背包已达容量上限");
            if (expectedCapacity != Bag.cap) return Fail("背包容量已变化，请重新确认");
            int price = ExpansionPrice;
            if (role.yuanbao < price) return Fail("元宝不足");

            role.yuanbao -= price;
            Bag.cap = Math.Min(MaxCapacity, Bag.cap + ExpansionStep);
            return Ok("背包已扩容至 " + Bag.cap + " 格");
        }

        // 购买只接受货号和件数，币种、价格与物品由共享目录确定。
        public bool BuyMall(string offerId, int quantity, out MallPurchase purchase)
        {
            purchase = null;
            if (quantity < 1 || quantity > 9999) return Fail("购买数量必须为 1 到 9999");
            var offer = GameData.MallOffers.FirstOrDefault(x => x.id == offerId);
            if (offer == null) return Fail("商品不存在或已下架");

            int count = Count(offer.item);
            if (offer.kind == "装备")
            {
                if (quantity != 1) return Fail("装备类商品单次只能购买 1 件");
                if (count > 0 || Bag.equip.ContainsValue(offer.item)) return Fail("已持有该装备");
            }
            int balance = offer.Balance(role);
            long total = (long)offer.price * quantity;
            if (total > balance) return Fail(MallOffer.CurrencyName(offer.currency) + "不足");
            if (quantity > int.MaxValue - count) return Fail("物品数量已达上限");
            if (!Give(offer.item, quantity)) return Fail("背包已满");

            int remaining = balance - (int)total;
            switch (offer.currency)
            {
                case "coin": role.coin = remaining; break;
                case "yuanbao": role.yuanbao = remaining; break;
                case "silverNote": role.silverNote = remaining; break;
            }
            purchase = new MallPurchase
            {
                Offer = offer, BalanceBefore = balance, BalanceAfter = remaining,
                CountBefore = count, CountAfter = count + quantity
            };
            return Ok("已购买 " + offer.name + " ×" + quantity);
        }

        public bool Sell(string id)
        {
            var it = Item(id); if (it == null || Count(id) <= 0) return Fail("没有这个物品");
            int price = it["price"] != null ? (int)it["price"] : 0;
            if (price <= 0) return Fail("任务物品不能出售");
            if (Type(it) == "potion") return Fail("药品不能出售");
            AddItem(id, -1); role.coin += price;
            return Ok("卖出" + Name(id) + "，银两 +" + price);
        }

        public bool Sort()
        {
            var order = new Dictionary<string, int> { { "equip", 0 }, { "gem", 1 }, { "potion", 2 }, { "mat", 3 } };
            Func<string, int> q = i => Item(i)["q"] != null ? (int)Item(i)["q"] : 1;
            var ids = Bag.items.Keys.Where(i => Item(i) != null).OrderBy(i => order[Type(Item(i))]).ThenByDescending(q).ToList();
            var o = new Dictionary<string, int>(); foreach (var i in ids) o[i] = Bag.items[i];
            Bag.items = o; return Ok("已整理背包");
        }

        // ---------- 商店 ----------
        public bool Buy(string shop, string id)
        {
            var sh = D["shops"][shop]; if (sh == null || !sh["sell"].Any(x => (string)x == id)) return Fail("这家店不卖这个");
            int price = (int)Item(id)["price"];
            if (role.coin < price) return Fail("银两不够");
            if (!CanHold(id)) return Fail("背包已满");
            role.coin -= price; AddItem(id, 1); return Ok("买下了" + Name(id));
        }

        public bool ShopSell(string shop, string id)
        {
            if (D["shops"][shop] == null) return Fail("没有这家店");
            var it = Item(id); if (it == null || Count(id) <= 0) return Fail("没有这个物品");
            if (Type(it) != "mat" || !(it["price"] != null && (int)it["price"] > 0)) return Fail("这里只收材料");
            AddItem(id, -1); role.coin += (int)it["price"]; return Ok("卖出" + Name(id));
        }

        // ---------- 锻造 / 宝石 ----------
        // 锻造报价（客户端只用来显示，真正的成功率服务端自己算）
        public static void ForgeQuote(string itemId, int star, string gemId, out int coin, out float rate)
        {
            var F = D["forge"]; var it = Item(itemId); var gi = Item(gemId);
            float mul = (float)F["qMul"][(it != null && it["q"] != null ? (int)it["q"] : 1).ToString()];
            coin = star >= (int)F["max"] ? 0 : (int)Math.Round((float)F["coin"][star] * mul, MidpointRounding.AwayFromZero);
            int fl = gi != null && gi["forge"] != null ? (int)gi["forge"] : 0;
            rate = star >= (int)F["max"] || fl == 0 ? 0f : fl >= 5 ? 1f : Math.Min(1f, (float)F["rate"][star] + (float)F["gemBonus"][fl - 1]);
        }

        public bool Forge(string slot, string gemId)
        {
            var F = D["forge"]; string id; if (!Bag.equip.TryGetValue(slot, out id) || id == null) return Fail("这个部位没有装备");
            var it = Item(id); var gr = Gear(slot); int s0 = gr.star;
            if (s0 >= (int)F["max"]) return Fail("已经满星");
            var gi = Item(gemId); if (gi == null || gi["forge"] == null) return Fail("先选一颗锻造宝石");
            float mul = (float)F["qMul"][(it["q"] != null ? (int)it["q"] : 1).ToString()];
            int coin = (int)Math.Round((float)F["coin"][s0] * mul, MidpointRounding.AwayFromZero);
            if (role.coin < coin) return Fail("银两不够");
            if (Count(gemId) <= 0) return Fail("没有" + Name(gemId) + "了");
            role.coin -= coin; AddItem(gemId, -1);
            int fl = (int)gi["forge"]; float rate = fl >= 5 ? 1f : Math.Min(1f, (float)F["rate"][s0] + (float)F["gemBonus"][fl - 1]);
            if (rng.NextDouble() < rate) { gr.star++; Star = gr.star; return Ok("锻造成功！" + Name(id) + " " + gr.star + " 星"); }
            if (s0 >= (int)F["dropFrom"]) { gr.star--; Star = gr.star; return Ok("锻造失败……" + Name(id) + " 掉到了 " + gr.star + " 星"); }
            Star = gr.star; return Ok("锻造失败，星级没有变化");
        }

        public bool Inlay(string slot, int i, string gemId)
        {
            if (!Bag.equip.ContainsKey(slot)) return Fail("这个部位没有装备");
            var gr = Gear(slot);
            if (i < 0 || i >= SocketCount(gr.star)) return Fail("这个孔还没解锁");
            var g = Item(gemId); if (g == null || Type(g) != "gem" || Count(gemId) <= 0) return Fail("没有这颗宝石");
            while (gr.gems.Count <= i) gr.gems.Add(null);
            if (gr.gems[i] != null) return Fail("这个孔已经镶了宝石");
            gr.gems[i] = gemId; AddItem(gemId, -1);
            role.hp = Math.Min(Hp, Stats().maxHp);
            return Ok("镶嵌了" + Name(gemId));
        }

        public bool Uninlay(string slot, int i)
        {
            var gr = Gear(slot); string gid = i >= 0 && i < gr.gems.Count ? gr.gems[i] : null;
            if (gid == null) return Fail("这个孔是空的");
            if (!CanHold(gid)) return Fail("背包已满");
            gr.gems[i] = null; AddItem(gid, 1);
            role.hp = Math.Min(Hp, Stats().maxHp);
            return Ok("摘下了" + Name(gid));
        }

        public bool Merge(string id)
        {
            var it = Item(id); if (it == null || Type(it) != "gem" || (int)it["lv"] >= 5) return Fail("这个不能合成");
            if (Count(id) < 3) return Fail("需要三颗");
            string next = System.Text.RegularExpressions.Regex.Replace(id, "_\\d$", "_" + ((int)it["lv"] + 1));
            if (Item(next) == null) return Fail("没有更高阶的宝石");
            AddItem(id, -3); AddItem(next, 1); return Ok("合成了" + Name(next));
        }
    }
}
