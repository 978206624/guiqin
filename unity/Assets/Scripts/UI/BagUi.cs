using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GuiQin.Inventory;
using GuiQin.Net;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace GuiQin
{
    // 背包 / 装备 / 锻造（含宝石镶嵌与合成）/ 商店。所有改动都发给后端，收到整包背包后重画——界面自己不改任何数据。
    public class BagUi
    {
        public enum Tab { Bag, Equip, Smith, Shop, Mall }

        readonly GameObject root; readonly RectTransform body; readonly Text title, coinLabel, msg;
        readonly ModalUi expandConfirm;
        readonly Func<string, BagOpReq, Task<Result<BagResp>>> op; readonly Func<RoleState> getRole; readonly Action<string> toast; readonly Action onChanged;
        Tab tab = Tab.Bag; string shopId; string filter = "all"; int page; string selId, selSlot; string forgeGem; bool busy;
        readonly List<Button> tabBtns = new List<Button>();

        static readonly string[] MallCurrencies = { "yuanbao", "coin", "silverNote" };
        int mallCurrency;
        static readonly string[] SlotOrder = { "weapon", "head", "chest", "waist", "legs", "feet", "wrist", "neck", "finger", "treasure", "pouch", "amulet" };
        static readonly Dictionary<string, string> StatName = new Dictionary<string, string> { { "atk", "物攻" }, { "matk", "法攻" }, { "hp", "气血" }, { "def", "物防" }, { "mdef", "法防" }, { "mp", "法力" }, { "dodge", "闪避" }, { "spd", "速度" }, { "crit", "暴击" } };
        static readonly string[][] Filters = { new[] { "all", "全部" }, new[] { "potion", "药品" }, new[] { "equip", "装备" }, new[] { "gem", "宝石" }, new[] { "mat", "材料" } };

        public bool Visible { get { return root.activeSelf; } }
        RoleState Role { get { return getRole(); } }
        BagState Bag { get { return Role.bag; } }
        static JToken Items { get { return GameData.Raw["items"]; } }

        public BagUi(Transform parent, Func<string, BagOpReq, Task<Result<BagResp>>> op, Func<RoleState> role, Action<string> toast, Action onChanged)
        {
            this.op = op; getRole = role; this.toast = toast; this.onChanged = onChanged;
            var shade = Ui.Box("bagOverlay", parent, WebUi.Dim);
            root = shade.gameObject;
            Ui.Anchor(shade.rectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var bg = WebUi.Panel("bagUi", shade.transform);
            Ui.Anchor(bg.rectTransform, Vector2.zero, Vector2.one, new Vector2(20, 100), new Vector2(-20, -100));
            title = WebUi.Title(Ui.Label("title", bg.transform, "背包", 34, WebUi.Amber, TextAnchor.MiddleCenter));
            Ui.Anchor(title.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(205, -70), new Vector2(475, -12));
            coinLabel = Ui.Label("coin", bg.transform, "", 28, new Color(1f, 0.85f, 0.4f), TextAnchor.MiddleRight);
            Ui.Anchor(coinLabel.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-215, -70), new Vector2(-64, -12));
            var x = WebUi.Close(bg.transform, Close);
            Ui.Anchor(x.GetComponent<RectTransform>(), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-50, -54), new Vector2(-10, -14));
            string[][] ts = { new[] { "Bag", "背包" }, new[] { "Equip", "装备" }, new[] { "Smith", "锻造" } };
            for (int i = 0; i < ts.Length; i++)
            {
                var t = (Tab)Enum.Parse(typeof(Tab), ts[i][0]);
                var b = Ui.Btn("tab_" + ts[i][0], bg.transform, ts[i][1], 28, new Color(0.25f, 0.3f, 0.5f), () => { tab = t; selSlot = null; Redraw(); });
                Ui.Anchor(b.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(0, 1), new Vector2(24 + i * 150, -140), new Vector2(24 + i * 150 + 140, -80));
                tabBtns.Add(b);
            }
            msg = Ui.Label("msg", bg.transform, "", 24, new Color(1f, 0.92f, 0.65f), TextAnchor.MiddleLeft);
            Ui.Anchor(msg.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(24, 8), new Vector2(-24, 52));
            body = Ui.Rect("body", bg.transform); Ui.Anchor(body, Vector2.zero, Vector2.one, new Vector2(16, 56), new Vector2(-16, -150));
            expandConfirm = new ModalUi(root.transform);
            root.SetActive(false);
        }

        public void Open(Tab t, string shop = null)
        {
            tab = t; shopId = shop; selId = null; selSlot = null; page = 0; mallCurrency = 0; filter = "all"; msg.text = "";
            root.SetActive(true); Redraw();
        }
        public void Close() { expandConfirm.Close(); root.SetActive(false); }

        // ---------------- 通用 ----------------
        static string ItemName(string id) { var it = Items[id]; return it != null ? (string)it["name"] : id; }
        static Color QColor(string id)
        {
            var it = Items[id]; int q = it != null && it["q"] != null ? (int)it["q"] : 1;
            var qd = GameData.Raw["quality"][q.ToString()]; return Ui.Hex(qd != null ? (string)qd["color"] : "#eef0f4", Color.white);
        }
        static Sprite Icon(string id)
        {
            var mallIcon = MallItemArt.Get(id);
            if (mallIcon != null) return mallIcon;
            var it = Items[id]; if (it == null) return null;
            string k = "item_" + (string)it["icon"];
            return Art.Has(k) ? Art.One(k, true) : null;
        }

        static void PlaceIcon(Transform parent, Sprite sprite, int x, int y, int size)
        {
            if (sprite == null) return;
            var icon = Ui.Box("itemIcon", parent, Color.white);
            icon.sprite = sprite;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            WebUi.At(icon.rectTransform, x, y, size, size);
        }
        static string Fmt(string k, float v) { return StatName[k] + " +" + (RoleStats.IsPct(k) ? v.ToString("0.#") + "%" : ((int)v).ToString()); }
        static string SlotName(string s) { return (string)GameData.Raw["slots"][s]; }

        static string StatText(string id)
        {
            var it = Items[id]; var sb = new StringBuilder();
            foreach (var k in RoleStats.Keys) if (it[k] != null) sb.Append(Fmt(k, (float)it[k])).Append("  ");
            return sb.ToString().TrimEnd();
        }
        static string BonusText(Dictionary<string, float> d)
        {
            var sb = new StringBuilder(); foreach (var k in RoleStats.Keys) if (d.ContainsKey(k) && d[k] != 0) sb.Append(Fmt(k, d[k])).Append("  ");
            return sb.ToString().TrimEnd();
        }
        static string Stars(int n) { return n <= 0 ? "" : " ★" + n; }

        void Clear() { for (int i = body.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(body.GetChild(i).gameObject); }

        Button Cell(Transform parent, string id, string sub, Vector2 min, Vector2 max, bool selected, UnityEngine.Events.UnityAction click)
        {
            var b = Ui.Btn("c_" + id, parent, "", 20, selected ? new Color(0.45f, 0.38f, 0.14f) : new Color(0.16f, 0.17f, 0.26f), click);
            Ui.Anchor(b.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(0, 1), min, max);
            if (id != null)
            {
                WebUi.Dress(b.image, WebUi.C("#332642"), WebUi.C("#1a122c"),
                    selected ? WebUi.Amber : QColor(id), 10);
                var ic = Ui.Box("ic", b.transform, Color.white); ic.sprite = Icon(id); ic.preserveAspect = true; ic.raycastTarget = false; ic.enabled = ic.sprite != null;
                Ui.Anchor(ic.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(-36, -80), new Vector2(36, -8));
                var nm = Ui.Label("n", b.transform, ItemName(id), 20, QColor(id), TextAnchor.LowerCenter);
                Ui.Anchor(nm.rectTransform, Vector2.zero, new Vector2(1, 0), new Vector2(2, 4), new Vector2(-2, 50));
            }
            if (!string.IsNullOrEmpty(sub))
            {
                var st = Ui.Label("s", b.transform, sub, 22, new Color(1f, 0.95f, 0.7f), TextAnchor.UpperRight);
                Ui.Anchor(st.rectTransform, Vector2.zero, Vector2.one, new Vector2(4, 4), new Vector2(-6, -4));
            }
            return b;
        }

        Text Info(Transform parent, string text, Vector2 min, Vector2 max, int size = 24)
        {
            var t = Ui.Label("info", parent, text, size, new Color(0.9f, 0.93f, 1f), TextAnchor.UpperLeft); t.supportRichText = true;
            Ui.Anchor(t.rectTransform, new Vector2(0, 0), new Vector2(1, 0), min, max); return t;
        }

        Button Act(Transform parent, string label, int slot, Color c, UnityEngine.Events.UnityAction a, bool on = true)
        {
            var b = Ui.Btn("act_" + label, parent, label, 26, on ? c : new Color(0.25f, 0.25f, 0.3f), a); b.interactable = on;
            Ui.Anchor(b.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(0, 0), new Vector2(slot * 166, 0), new Vector2(slot * 166 + 156, 60));
            return b;
        }

        // 发命令 → 覆盖本地背包 → 重画
        async void Do(string cmd, BagOpReq req)
        {
            if (busy) return; busy = true;
            try
            {
                var current = Role;
                var r = await op(cmd, req);
                if (Role != current) return;
                if (!r.Ok) { msg.text = r.msg ?? "操作失败"; return; }
                var d = r.data; var role = Role;
                if (d.bag != null) role.bag = d.bag;
                role.coin = d.coin; role.hp = d.hp; role.mp = d.mp;
                role.yuanbao = d.yuanbao; role.silverNote = d.silverNote;
                msg.text = d.msg ?? "";
                if (cmd == BagCmd.Sell || cmd == BagCmd.ShopSell || cmd == BagCmd.ShopBuy) { }
                onChanged?.Invoke();
            }
            finally { busy = false; Redraw(); }
        }

        // ---------------- 重画 ----------------
        void Redraw()
        {
            Clear();
            var role = Role; if (role == null || role.bag == null) return;
            coinLabel.gameObject.SetActive(tab != Tab.Mall);
            coinLabel.text = "银两 " + role.coin.ToString("N0");
            title.text = tab == Tab.Mall ? "商城" : tab == Tab.Shop ? ((string)GameData.Raw["shops"]?[shopId]?["name"] ?? "商店") :
                tab == Tab.Smith ? "铁匠铺" : tab == Tab.Equip ? "装备" : "背包";
            bool storefront = tab == Tab.Shop || tab == Tab.Mall;
            for (int i = 0; i < tabBtns.Count; i++) { tabBtns[i].gameObject.SetActive(!storefront); WebUi.Button(tabBtns[i], (int)tab == i ? WebUi.C("#8a5a14") : WebUi.Purple); }
            if (selId != null && !Bag.items.ContainsKey(selId) && !storefront) selId = null;
            switch (tab) { case Tab.Bag: DrawBag(); break; case Tab.Equip: DrawEquip(); break; case Tab.Smith: DrawSmith(); break; case Tab.Shop: DrawShop(); break; case Tab.Mall: DrawMall(); break; }
        }

        List<string> Filtered()
        {
            return Bag.items.Keys.Where(i => Items[i] != null && (filter == "all" || (string)Items[i]["type"] == filter)).ToList();
        }

        // ---- 背包 ----
        void DrawBag()
        {
            // 过滤标签 + 整理
            for (int i = 0; i < Filters.Length; i++)
            {
                string f = Filters[i][0];
                var b = Ui.Btn("f_" + f, body, Filters[i][1], 22, filter == f ? new Color(0.55f, 0.4f, 0.15f) : new Color(0.2f, 0.22f, 0.34f), () => { filter = f; page = 0; selId = null; Redraw(); });
                Ui.Anchor(b.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(0, 1), new Vector2(i * 96, -44), new Vector2(i * 96 + 90, 0));
            }
            var sort = Ui.Btn("sort", body, "整理", 22, new Color(0.25f, 0.3f, 0.5f), () => Do(BagCmd.Sort, null));
            Ui.Anchor(sort.GetComponent<RectTransform>(), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-90, -44), new Vector2(0, 0));

            var ids = Filtered(); const int per = 20;
            int slots = filter == "all" ? Math.Max(Bag.cap, ids.Count) : ids.Count;
            int pages = Math.Max(1, (slots + per - 1) / per); page = Mathf.Clamp(page, 0, pages - 1);
            var grid = Ui.Rect("grid", body); Ui.Anchor(grid, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -50 - 4 * 112), new Vector2(0, -50));
            for (int k = 0; k < per; k++)
            {
                int idx = page * per + k; int col = k % 5, row = k / 5;
                if (filter == "all" && idx >= slots) break;
                Vector2 mn = new Vector2(col * 130, -row * 112 - 105), mx = new Vector2(col * 130 + 122, -row * 112);
                if (idx < ids.Count)
                {
                    string id = ids[idx]; int n = Bag.items[id];
                    Cell(grid, id, n > 1 ? "×" + n : "", mn, mx, selId == id, () => { selId = id; Redraw(); });
                }
                else { var e = Ui.Box("empty", grid, new Color(1, 1, 1, 0.05f)); Ui.Anchor(e.rectTransform, new Vector2(0, 1), new Vector2(0, 1), mn, mx); e.raycastTarget = false; }
            }
            var pg = Ui.Label("pg", body, "第 " + (page + 1) + "/" + pages + " 页　格子 " + Bag.items.Count + "/" + Bag.cap, 22, new Color(0.7f, 0.75f, 0.9f), TextAnchor.MiddleLeft);
            Ui.Anchor(pg.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -50 - 4 * 112 - 40), new Vector2(270, -50 - 4 * 112));
            int added = Math.Min(InventoryService.ExpansionStep, InventoryService.MaxCapacity - Bag.cap);
            var expand = Ui.Btn("expand", body, added > 0 ? "扩容 +" + added : "已达上限", 22, WebUi.Purple, ConfirmExpansion);
            WebUi.At(expand.GetComponent<RectTransform>(), 302, 498, 152, 40);
            expand.interactable = added > 0 && !busy;
            var pv = Ui.Btn("prev", body, "上页", 22, new Color(0.25f, 0.3f, 0.5f), () => { page--; Redraw(); }); pv.interactable = page > 0;
            Ui.Anchor(pv.GetComponent<RectTransform>(), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-170, -50 - 4 * 112 - 40), new Vector2(-90, -50 - 4 * 112));
            var nx = Ui.Btn("next", body, "下页", 22, new Color(0.25f, 0.3f, 0.5f), () => { page++; Redraw(); }); nx.interactable = page < pages - 1;
            Ui.Anchor(nx.GetComponent<RectTransform>(), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-80, -50 - 4 * 112 - 40), new Vector2(0, -50 - 4 * 112));

            DrawItemDetail();
        }

        void ConfirmExpansion()
        {
            var role = Role;
            int capacity = Bag.cap, cost = InventoryService.ExpansionPrice;
            int next = Math.Min(InventoryService.MaxCapacity, capacity + InventoryService.ExpansionStep);
            if (busy || next <= capacity) return;
            string details = "背包容量 " + capacity + " → " + next + " 格\n消耗 " + cost + " 元宝\n当前元宝 " + role.yuanbao.ToString("N0");
            if (role.yuanbao < cost)
            {
                expandConfirm.Open("背包扩容", details + "\n元宝不足", new ModalUi.Btn("知道了", null));
                return;
            }
            expandConfirm.Open("背包扩容", details, new ModalUi.Btn("确认扩容", () =>
            {
                if (Visible && Role == role && tab == Tab.Bag)
                    Do(BagCmd.Expand, new BagOpReq { capacity = capacity });
            }), new ModalUi.Btn("取消", null));
        }

        void DrawItemDetail()
        {
            var box = WebUi.Panel("detail", body); Ui.Anchor(box.rectTransform, Vector2.zero, new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, 250)); box.raycastTarget = false;
            if (selId == null) { var h = Info(box.transform, "点一件物品看详情", new Vector2(16, 150), new Vector2(-16, 230)); return; }
            var it = Items[selId]; string type = (string)it["type"];
            var sb = new StringBuilder();
            sb.Append("<b>").Append(ItemName(selId)).Append("</b>　").Append(type == "potion" ? "药品" : type == "equip" ? SlotName((string)it["slot"]) : type == "gem" ? "宝石" : "材料");
            sb.Append("　×").Append(Bag.items[selId]).Append("\n");
            string st = StatText(selId); if (st != "") sb.Append(st).Append("\n");
            if (type == "equip") sb.Append(CompareText(selId)).Append("\n");
            if (it["desc"] != null) sb.Append("<size=20>").Append((string)it["desc"]).Append("</size>");
            var mallIcon = MallItemArt.Get(selId);
            PlaceIcon(box.transform, mallIcon, 12, 12, 80);
            Info(box.transform, sb.ToString(), new Vector2(mallIcon != null ? 104 : 16, 76), new Vector2(-16, 240));
            var row = Ui.Rect("acts", box.transform); Ui.Anchor(row, new Vector2(0, 0), new Vector2(1, 0), new Vector2(12, 8), new Vector2(-12, 68));
            int slot = 0; string id = selId;
            if (type == "potion") Act(row, "使用", slot++, new Color(0.2f, 0.5f, 0.3f), () => Do(BagCmd.Use, new BagOpReq { id = id }));
            if (type == "equip") Act(row, "装备", slot++, new Color(0.2f, 0.5f, 0.3f), () => Do(BagCmd.Equip, new BagOpReq { id = id }));
            if (type == "gem" && (int)it["lv"] < 5) Act(row, "合成", slot++, new Color(0.45f, 0.3f, 0.6f), () => Do(BagCmd.Merge, new BagOpReq { id = id }), Bag.items[id] >= 3);
            if (type != "potion" && it["price"] != null && (int)it["price"] > 0) Act(row, "出售 " + (int)it["price"], slot++, new Color(0.55f, 0.35f, 0.15f), () => Do(BagCmd.Sell, new BagOpReq { id = id }));
        }

        // 和身上同部位装备对比（只看自带属性，不含星级和宝石）
        string CompareText(string id)
        {
            var it = Items[id]; string slot = (string)it["slot"]; string cur; Bag.equip.TryGetValue(slot, out cur);
            if (string.IsNullOrEmpty(cur)) return "<color=#8affb0>该部位空着，装上就是净提升</color>";
            var c = Items[cur]; var sb = new StringBuilder("对比身上的" + ItemName(cur) + "：");
            bool any = false;
            foreach (var k in RoleStats.Keys)
            {
                float a = it[k] != null ? (float)it[k] : 0f, b = c[k] != null ? (float)c[k] : 0f; if (a == b) continue; any = true;
                sb.Append(StatName[k]).Append(a > b ? " <color=#8affb0>+" : " <color=#ff8a8a>").Append((a - b).ToString("0.#")).Append("</color>  ");
            }
            return any ? sb.ToString() : sb.Append("属性相同").ToString();
        }

        // ---- 装备 ----
        void DrawEquip()
        {
            for (int k = 0; k < SlotOrder.Length; k++)
            {
                string slot = SlotOrder[k]; int col = k % 2, row = k / 2;
                string id; Bag.equip.TryGetValue(slot, out id);
                GearState g; Bag.gear.TryGetValue(slot, out g);
                Vector2 mn = new Vector2(col == 0 ? 0 : 496, -row * 91), mx = new Vector2(col == 0 ? 150 : 646, -row * 91 - 84);
                Vector2 a = new Vector2(mn.x, mx.y), b = new Vector2(mx.x, mn.y);
                var c = Cell(body, string.IsNullOrEmpty(id) ? null : id, g != null && g.star > 0 ? "★" + g.star : "", a, b, selSlot == slot, () => { selSlot = slot; Redraw(); });
                var sl = Ui.Label("slot", c.transform, SlotName(slot), 22, new Color(0.7f, 0.75f, 0.9f), string.IsNullOrEmpty(id) ? TextAnchor.MiddleCenter : TextAnchor.UpperLeft);
                Ui.Anchor(sl.rectTransform, Vector2.zero, Vector2.one, new Vector2(6, 4), new Vector2(-6, -4));
            }
            var figure = WebUi.Icon("equippedRole", body, "cls_" + Role.cls);
            WebUi.At(figure.rectTransform, 160, 0, 326, 305);
            var tot = RoleStats.Compute(Role.cls, Role.lv, Bag);
            var box = WebUi.Panel("detail", body); Ui.Anchor(box.rectTransform, Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 328)); box.raycastTarget = false;
            var sb = new StringBuilder();
            string eid = null; if (selSlot != null) Bag.equip.TryGetValue(selSlot, out eid);
            if (selSlot == null) sb.Append("点一个部位看详情");
            else if (string.IsNullOrEmpty(eid)) sb.Append(SlotName(selSlot) + "：空。去背包里点装备再点“装备”。");
            else
            {
                GearState g; Bag.gear.TryGetValue(selSlot, out g); g = g ?? new GearState();
                sb.Append("<b>").Append(ItemName(eid)).Append(Stars(g.star)).Append("</b>\n").Append(BonusText(RoleStats.SlotBonus(Bag, selSlot))).Append("\n");
                int socks = ((JArray)GameData.Raw["forge"]["sockets"]).Count(n => g.star >= (int)n);
                sb.Append("宝石孔 ").Append(socks).Append("/4：");
                for (int i = 0; i < 4; i++) sb.Append(i < socks ? (i < g.gems.Count && g.gems[i] != null ? "[" + ItemName(g.gems[i]) + "]" : "[空]") : "[锁]").Append(" ");
            }
            Info(box.transform, sb.ToString(), new Vector2(16, 70), new Vector2(-16, 144), 19);
            if (selSlot != null && !string.IsNullOrEmpty(eid)) { var row = Ui.Rect("acts", box.transform); Ui.Anchor(row, new Vector2(0, 0), new Vector2(1, 0), new Vector2(12, 8), new Vector2(-12, 68)); string s = selSlot; Act(row, "卸下", 0, new Color(0.55f, 0.35f, 0.15f), () => Do(BagCmd.Unequip, new BagOpReq { slot = s })); }
            var pan = Ui.Label("tot", box.transform, "<b>人物属性</b>  Lv." + Role.lv + "\n气血 " + tot.maxHp + "　法力 " + tot.maxMp + "\n物攻 " + tot.atk + "　法攻 " + tot.matk + "\n物防 " + tot.def + "　法防 " + tot.mdef + "\n速度 " + tot.spd + "　暴击 " + tot.crit.ToString("0.#") + "%　闪避 " + tot.dodge.ToString("0.#") + "%", 24, new Color(0.9f, 0.95f, 1f), TextAnchor.UpperLeft);
            pan.supportRichText = true; Ui.Anchor(pan.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(16, -160), new Vector2(-16, -8));
        }

        // ---- 锻造 / 镶嵌 ----
        void DrawSmith()
        {
            var worn = SlotOrder.Where(s => Bag.equip.ContainsKey(s) && !string.IsNullOrEmpty(Bag.equip[s])).ToList();
            if (selSlot == null || !worn.Contains(selSlot)) selSlot = worn.FirstOrDefault();
            if (selSlot == null) { Info(body, "身上没有装备可锻造。", new Vector2(16, 400), new Vector2(-16, 480)); return; }
            for (int k = 0; k < worn.Count; k++)
            {
                string slot = worn[k]; int col = k % 4, row = k / 4; GearState gg; Bag.gear.TryGetValue(slot, out gg);
                var b = Ui.Btn("s_" + slot, body, SlotName(slot) + Stars(gg != null ? gg.star : 0), 22, selSlot == slot ? new Color(0.55f, 0.4f, 0.15f) : new Color(0.2f, 0.22f, 0.34f), () => { selSlot = slot; forgeGem = null; Redraw(); });
                Ui.Anchor(b.GetComponent<RectTransform>(), new Vector2(0, 1), new Vector2(0, 1), new Vector2(col * 164, -row * 56 - 50), new Vector2(col * 164 + 156, -row * 56));
            }
            string eid = Bag.equip[selSlot]; GearState g; Bag.gear.TryGetValue(selSlot, out g); g = g ?? new GearState();
            var box = WebUi.Panel("detail", body); Ui.Anchor(box.rectTransform, Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 820)); box.raycastTarget = false;
            var forgeGems = Bag.items.Keys.Where(i => Items[i] != null && Items[i]["forge"] != null).OrderBy(i => (int)Items[i]["forge"]).ToList();
            if (forgeGem == null || !forgeGems.Contains(forgeGem)) forgeGem = forgeGems.FirstOrDefault();
            int maxStar = (int)GameData.Raw["forge"]["max"]; var sb = new StringBuilder();
            sb.Append("<b>").Append(ItemName(eid)).Append(Stars(g.star)).Append("</b>　").Append(BonusText(RoleStats.SlotBonus(Bag, selSlot))).Append("\n");
            if (g.star >= maxStar) sb.Append("已满星。");
            else if (forgeGem == null) sb.Append("没有锻造宝石——去百宝摊买锻造石。");
            else
            {
                int coin; float rate; InventoryService.ForgeQuote(eid, g.star, forgeGem, out coin, out rate);
                sb.Append("锻到 ").Append(g.star + 1).Append(" 星：银两 ").Append(coin).Append("，成功率 ").Append(Mathf.RoundToInt(rate * 100f)).Append("%");
                if (g.star >= (int)GameData.Raw["forge"]["dropFrom"]) sb.Append("\n<color=#ff8a8a>失败会掉一星！</color>");
            }
            Info(box.transform, sb.ToString(), new Vector2(16, 640), new Vector2(-16, 810));
            // 锻造石选择
            for (int i = 0; i < forgeGems.Count; i++)
            {
                string id = forgeGems[i];
                Cell(box.transform, id, "×" + Bag.items[id], new Vector2(10 + i * 124, -0), new Vector2(10 + i * 124 + 116, -110), forgeGem == id, () => { forgeGem = id; Redraw(); });
                var rt = box.transform.GetChild(box.transform.childCount - 1).GetComponent<RectTransform>(); rt.anchorMin = rt.anchorMax = new Vector2(0, 0); rt.pivot = new Vector2(0, 0); rt.anchoredPosition = new Vector2(10 + i * 124, 520); rt.sizeDelta = new Vector2(116, 110);
            }
            var fr = Ui.Rect("fr", box.transform); Ui.Anchor(fr, new Vector2(0, 0), new Vector2(1, 0), new Vector2(12, 450), new Vector2(-12, 510));
            string s0 = selSlot, fg = forgeGem;
            Act(fr, "锻造", 0, new Color(0.7f, 0.3f, 0.15f), () => Do(BagCmd.Forge, new BagOpReq { slot = s0, gem = fg }), g.star < maxStar && forgeGem != null);

            // 宝石孔
            int socks = ((JArray)GameData.Raw["forge"]["sockets"]).Count(n => g.star >= (int)n);
            var sl = Ui.Label("sl", box.transform, "宝石孔（点空孔下方的宝石来镶嵌，点已镶的宝石摘下）", 22, new Color(0.7f, 0.75f, 0.9f), TextAnchor.MiddleLeft);
            Ui.Anchor(sl.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(12, 400), new Vector2(-12, 440));
            for (int i = 0; i < 4; i++)
            {
                int si = i; string gid = i < g.gems.Count ? g.gems[i] : null;
                var b = Ui.Btn("sock" + i, box.transform, i >= socks ? "锁\n" + ((JArray)GameData.Raw["forge"]["sockets"])[i] + "星" : gid != null ? ItemName(gid) : "空", 22,
                    i >= socks ? new Color(0.2f, 0.2f, 0.22f) : gid != null ? new Color(0.35f, 0.25f, 0.5f) : new Color(0.16f, 0.17f, 0.26f), () => { if (gid != null) Do(BagCmd.Uninlay, new BagOpReq { slot = s0, i = si }); });
                b.interactable = i < socks && gid != null;
                Ui.Anchor(b.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(0, 0), new Vector2(12 + i * 166, 316), new Vector2(12 + i * 166 + 156, 392));
            }
            // 背包里的宝石（点一颗镶进第一个空孔）
            var gems = Bag.items.Keys.Where(i => Items[i] != null && (string)Items[i]["type"] == "gem").ToList();
            for (int i = 0; i < gems.Count && i < 8; i++)
            {
                string id = gems[i]; int col = i % 4, row = i / 4;
                int empty = -1; for (int j = 0; j < socks; j++) if (j >= g.gems.Count || g.gems[j] == null) { empty = j; break; }
                int target = empty;
                var c = Cell(box.transform, id, "×" + Bag.items[id], Vector2.zero, Vector2.zero, false, () => { if (target >= 0) Do(BagCmd.Inlay, new BagOpReq { slot = s0, i = target, id = id }); else msg.text = "没有空孔"; });
                var rt = c.GetComponent<RectTransform>(); rt.anchorMin = rt.anchorMax = new Vector2(0, 0); rt.pivot = new Vector2(0, 0); rt.anchoredPosition = new Vector2(12 + col * 166, 176 - row * 124 + 0); rt.sizeDelta = new Vector2(156, 112);
            }
        }

        // ---- 商店 ----
        void DrawShop()
        {
            var sh = GameData.Raw["shops"][shopId]; if (sh == null) { Close(); return; }
            var title = Ui.Label("shopT", body, (string)sh["name"], 30, new Color(1f, 0.92f, 0.65f), TextAnchor.MiddleLeft);
            Ui.Anchor(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -44), new Vector2(0, 0));
            var wares = sh["sell"].Select(x => (string)x).ToList(); const int per = 12; int pages = Math.Max(1, (wares.Count + per - 1) / per); page = Mathf.Clamp(page, 0, pages - 1);
            var grid = Ui.Rect("grid", body); Ui.Anchor(grid, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -50 - 3 * 128), new Vector2(0, -50));
            for (int k = 0; k < per; k++)
            {
                int idx = page * per + k; if (idx >= wares.Count) break; string id = wares[idx]; int col = k % 4, row = k / 4;
                Cell(grid, id, ((int)Items[id]["price"]).ToString(), new Vector2(col * 164, -row * 128 - 120), new Vector2(col * 164 + 156, -row * 128), selId == id, () => { selId = id; Redraw(); });
            }
            if (pages > 1)
            {
                var pv = Ui.Btn("prev", body, "上页", 22, new Color(0.25f, 0.3f, 0.5f), () => { page--; Redraw(); }); pv.interactable = page > 0;
                Ui.Anchor(pv.GetComponent<RectTransform>(), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-170, -50 - 3 * 128 - 40), new Vector2(-90, -50 - 3 * 128));
                var nx = Ui.Btn("next", body, "下页", 22, new Color(0.25f, 0.3f, 0.5f), () => { page++; Redraw(); }); nx.interactable = page < pages - 1;
                Ui.Anchor(nx.GetComponent<RectTransform>(), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-80, -50 - 3 * 128 - 40), new Vector2(0, -50 - 3 * 128));
            }
            var box = WebUi.Panel("detail", body); Ui.Anchor(box.rectTransform, Vector2.zero, new Vector2(1, 0), Vector2.zero, new Vector2(0, 330)); box.raycastTarget = false;
            if (selId == null || Items[selId] == null) { Info(box.transform, "点一件商品看详情", new Vector2(16, 240), new Vector2(-16, 320)); }
            else
            {
                var it = Items[selId]; var sb = new StringBuilder();
                sb.Append("<b>").Append(ItemName(selId)).Append("</b>　售价 ").Append((int)it["price"]).Append("　已有 ").Append(Bag.items.ContainsKey(selId) ? Bag.items[selId] : 0).Append("\n");
                string st = StatText(selId); if (st != "") sb.Append(st).Append("\n");
                if ((string)it["type"] == "equip") sb.Append(CompareText(selId)).Append("\n");
                if (it["desc"] != null) sb.Append("<size=20>").Append((string)it["desc"]).Append("</size>");
                Info(box.transform, sb.ToString(), new Vector2(16, 90), new Vector2(-16, 320));
                var row = Ui.Rect("acts", box.transform); Ui.Anchor(row, new Vector2(0, 0), new Vector2(1, 0), new Vector2(12, 8), new Vector2(-12, 68));
                string id = selId, sid = shopId;
                Act(row, "购买", 0, new Color(0.2f, 0.5f, 0.3f), () => Do(BagCmd.ShopBuy, new BagOpReq { shop = sid, id = id }), role_coin() >= (int)it["price"]);
            }
            // 卖材料
            var mats = Bag.items.Keys.Where(i => Items[i] != null && (string)Items[i]["type"] == "mat" && Items[i]["price"] != null && (int)Items[i]["price"] > 0).ToList();
            var ml = Ui.Label("ml", box.transform, mats.Count == 0 ? "" : "收购材料（点一下卖一个）：", 22, new Color(0.7f, 0.75f, 0.9f), TextAnchor.MiddleLeft);
            Ui.Anchor(ml.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(16, 70), new Vector2(-16, 100));
            for (int i = 0; i < mats.Count && i < 5; i++)
            {
                string id = mats[i]; string sid = shopId;
                var b = Ui.Btn("m_" + id, box.transform, ItemName(id) + " ×" + Bag.items[id], 20, new Color(0.35f, 0.25f, 0.15f), () => Do(BagCmd.ShopSell, new BagOpReq { shop = sid, id = id }));
                Ui.Anchor(b.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(0, 0), new Vector2(12 + i * 130, 0), new Vector2(12 + i * 130 + 124, 0)); var rt = b.GetComponent<RectTransform>(); rt.anchoredPosition = new Vector2(12 + i * 130, 104); rt.sizeDelta = new Vector2(124, 56);
            }
        }
        void DrawMall()
        {
            string currency = MallCurrencies[mallCurrency], currencyName = MallOffer.CurrencyName(currency);
            int[] balances = { Role.yuanbao, Role.coin, Role.silverNote };
            for (int i = 0; i < MallCurrencies.Length; i++)
            {
                int index = i;
                MallButton("currency_" + MallCurrencies[i], body,
                    MallOffer.CurrencyName(MallCurrencies[i]) + " " + balances[i].ToString("N0"),
                    i * 217, 0, 208, 52, i == mallCurrency, () => { mallCurrency = index; page = 0; selId = null; msg.text = ""; Redraw(); });
            }

            var offers = GameData.MallOffers.Where(x => x.currency == currency).ToList();
            const int perPage = 12;
            int pages = Math.Max(1, (offers.Count + perPage - 1) / perPage);
            page = Mathf.Clamp(page, 0, pages - 1);
            for (int index = page * perPage; index < Math.Min((page + 1) * perPage, offers.Count); index++)
            {
                var offer = offers[index];
                int offset = index - page * perPage;
                var row = MallButton("offer_" + offer.id, body, "", offset % 2 * 326, 67 + offset / 2 * 74, 318, 68,
                    offer.id == selId, () => { selId = offer.id; msg.text = ""; Redraw(); });
                PlaceIcon(row.transform, Icon(offer.item), 8, 6, 56);
                Color color = offer.color == "#000000" ? WebUi.Text :
                    offer.color == "#009933" ? WebUi.Jade : offer.color == "#0066CC" ? WebUi.Sky : WebUi.C(offer.color);
                var name = Ui.Label("name", row.transform, offer.name, 22, color, TextAnchor.MiddleLeft);
                name.resizeTextForBestFit = true; name.resizeTextMinSize = 16;
                WebUi.At(name.rectTransform, 72, 4, 234, 34);
                var price = Ui.Label("price", row.transform, offer.price.ToString("N0") + " " + currencyName, 18, WebUi.Light, TextAnchor.MiddleRight);
                WebUi.At(price.rectTransform, 72, 36, 234, 24);
            }
            var previous = MallButton("mall_prev", body, "上页", 166, 516, 100, 44, false, () => { page--; selId = null; Redraw(); });
            var next = MallButton("mall_next", body, "下页", 382, 516, 100, 44, false, () => { page++; selId = null; Redraw(); });
            previous.interactable = page > 0; next.interactable = page + 1 < pages;
            var pageLabel = Ui.Label("mall_page", body, (page + 1) + " / " + pages, 22, WebUi.Light, TextAnchor.MiddleCenter);
            WebUi.At(pageLabel.rectTransform, 270, 516, 108, 44);

            var detail = WebUi.Panel("mall_detail", body);
            WebUi.At(detail.rectTransform, 0, 583, 644, 270);
            var selected = offers.Find(x => x.id == selId);
            if (selected == null)
            {
                var hint = Ui.Label("mall_hint", detail.transform, offers.Count > 0 ? "请选择商品" : "暂无商品", 26, WebUi.Muted, TextAnchor.MiddleCenter);
                WebUi.At(hint.rectTransform, 12, 10, 620, 90);
                return;
            }

            Bag.items.TryGetValue(selected.item, out int owned);
            string desc = selected.kind + " · 等级 " + selected.level + " · 已有 " + owned +
                (selected.bound ? " · 绑定" : "") + "\n" + selected.desc;
            PlaceIcon(detail.transform, Icon(selected.item), 16, 16, 88);
            var heading = Ui.Label("name", detail.transform, selected.name, 27, WebUi.Light, TextAnchor.MiddleLeft);
            WebUi.At(heading.rectTransform, 116, 12, 510, 42);
            var summary = Ui.Label("summary", detail.transform, desc, 22, WebUi.Text, TextAnchor.UpperLeft);
            WebUi.At(summary.rectTransform, 116, 60, 510, 105);

            bool equipment = selected.kind == "装备";
            var quantity = Ui.Input("mall_quantity", detail.transform, "数量", 24, false, 4);
            quantity.contentType = InputField.ContentType.IntegerNumber;
            quantity.text = "1";
            quantity.interactable = !equipment;
            WebUi.At(quantity.GetComponent<RectTransform>(), 16, 182, 92, 56);
            var total = Ui.Label("mall_total", detail.transform, "", 22, WebUi.Light, TextAnchor.MiddleLeft);
            WebUi.At(total.rectTransform, 125, 182, 326, 56);
            int amount = 1;
            var buy = MallButton("mall_buy", detail.transform, "购买", 470, 182, 156, 56, true,
                () => Do(BagCmd.MallBuy, new BagOpReq { id = selected.id, quantity = amount }));
            void UpdateTotal(string value)
            {
                if (!int.TryParse(value, out amount) || amount < 1 || amount > 9999)
                {
                    total.text = "数量须为 1–9999"; buy.interactable = false; return;
                }
                long cost = (long)selected.price * amount;
                bool enough = cost <= selected.Balance(Role);
                total.text = cost.ToString("N0") + " " + currencyName + (enough ? "" : "（余额不足）");
                buy.interactable = enough && (!equipment || owned == 0);
            }
            quantity.onValueChanged.AddListener(UpdateTotal);
            UpdateTotal(quantity.text);
        }

        static Button MallButton(string name, Transform parent, string label, int x, int y, int width, int height,
            bool selected, UnityEngine.Events.UnityAction click)
        {
            var button = Ui.Btn(name, parent, label, 23, WebUi.Purple, click);
            WebUi.At(button.GetComponent<RectTransform>(), x, y, width, height);
            WebUi.Button(button, selected ? WebUi.C("#8a5a14") : WebUi.Purple);
            return button;
        }

        int role_coin() { return Role.coin; }
    }
}
