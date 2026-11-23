using System;
using System.Collections.Generic;
using System.Linq;
using GuiQin.Inventory;
using GuiQin.Quest;
using GuiQin.Sect;
using GuiQin.Train;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace GuiQin
{
    public partial class BagUi
    {
        const int RoleWidth = 632;
        readonly Dictionary<Tab, Button> roleTabs = new Dictionary<Tab, Button>();
        RectTransform roleHeader, roleScroll;

        void BuildRoleTabs(Transform parent)
        {
            roleHeader = Ui.Rect("roleHeader", parent);
            foreach (var entry in new[] { (Tab.Attributes, "属性"), (Tab.Equip, "装备"), (Tab.Skills, "技能") })
            {
                var target = entry.Item1;
                roleTabs.Add(target, Ui.Btn("role_" + target, parent, entry.Item2, 22, WebUi.Purple, () =>
                {
                    if (tab == target) return;
                    tab = target; roleScroll = null; Redraw();
                }));
            }
        }

        public void OpenRole()
        {
            Open(Tab.Attributes, fromRole: true);
        }

        void DrawRole(float scrollY)
        {
            var stats = RoleStats.Compute(Role.cls, Role.lv, Bag);
            var cls = GameData.Raw["classes"][Role.cls] ?? GameData.Raw["classes"]["novice_f"];
            bool equipped = tab == Tab.Equip;
            roleHeader.gameObject.SetActive(!equipped);
            if (!equipped)
            {
                for (int i = roleHeader.childCount - 1; i >= 0; i--)
                {
                    var old = roleHeader.GetChild(i).gameObject;
                    old.SetActive(false); UnityEngine.Object.Destroy(old);
                }
                WebUi.At(roleHeader, 20, 72, RoleWidth, 195);
                DrawRoleHeader(cls);
            }
            int iTab = 0, tabY = equipped ? 76 : 275;
            foreach (var button in roleTabs.Values)
                WebUi.At((RectTransform)button.transform, 20 + iTab++ * 214, tabY, 206, 44);

            Ui.Anchor(body, Vector2.zero, Vector2.one, new Vector2(20, 56), new Vector2(-20, -tabY - 54));
            var viewport = Ui.Rect("roleViewport", body);
            Ui.Anchor(viewport, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            viewport.gameObject.AddComponent<RectMask2D>();
            viewport.gameObject.AddComponent<Image>().color = Color.clear;
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            roleScroll = Ui.Rect("roleContent", viewport);
            Ui.Anchor(roleScroll, Vector2.up, Vector2.up, Vector2.zero, new Vector2(RoleWidth, 0));
            roleScroll.pivot = Vector2.up;
            roleScroll.anchoredPosition = Vector2.zero;
            scroll.viewport = viewport; scroll.content = roleScroll;
            scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 36f;
            int height = equipped ? DrawRoleEquipment(stats) : tab == Tab.Skills ? DrawRoleSkills(cls) : DrawRoleAttributes(cls, stats);
            roleScroll.sizeDelta = new Vector2(RoleWidth, height);
            roleScroll.anchoredPosition = new Vector2(0, Mathf.Clamp(scrollY, 0, Mathf.Max(0, height - viewport.rect.height)));
        }

        Text RoleText(Transform parent, string id, string value, float x, float y, float width, float height, int size,
            Color color, TextAnchor align = TextAnchor.MiddleLeft)
        {
            var label = Ui.Label(id, parent, value, size, color, align);
            WebUi.At(label.rectTransform, x, y, width, height);
            return label;
        }

        void DrawRoleHeader(JToken cls)
        {
            var figure = WebUi.Icon("roleFigure", roleHeader, "cls_" + Role.cls);
            WebUi.At(figure.rectTransform, 0, 0, 184, 190);
            WebUi.Title(RoleText(roleHeader, "roleName", Role.name, 202, 58, 420, 46, 34, WebUi.Light));
            var gender = (string)cls["gender"] == "m" ? "男" : "女";
            RoleText(roleHeader, "class", $"{SectService.ClsName(Role.cls)} · {cls["role"]} · {gender}", 202, 103, 420, 27, 19, WebUi.Muted);
            RoleText(roleHeader, "level", $"等级 {Role.lv}", 202, 132, 420, 26, 20, WebUi.Amber);
            int need = Rewards.ExpNeed(Role.lv);
            RoleText(roleHeader, "xp", $"经验 {Role.exp} / {need}", 202, 158, 420, 23, 15, WebUi.Muted);
            RoleBar(roleHeader, "xpBar", Role.exp, need, 202, 185, 418, 8, "#ffe08a", "#d9972a");
        }

        void RoleBar(Transform parent, string id, int current, int max, int x, int y, int width, int height, string top, string bottom)
        {
            var track = WebUi.Gradient(id, parent, WebUi.C("#1a0f14"), WebUi.C("#0a0612"), 5);
            WebUi.At(track.rectTransform, x, y, width, height);
            var fill = WebUi.Gradient("fill", track.transform, WebUi.C(top), WebUi.C(bottom), 5);
            Ui.Anchor(fill.rectTransform, Vector2.zero, new Vector2(max > 0 ? Mathf.Clamp01((float)current / max) : 0, 1), Vector2.zero, Vector2.zero);
        }

        int DrawRoleAttributes(JToken cls, StatBlock stats)
        {
            int hp = Role.hp < 0 ? stats.maxHp : Role.hp, mp = Role.mp < 0 ? stats.maxMp : Role.mp;
            RoleText(roleScroll, "hpLabel", "生命", 0, 0, 310, 22, 16, WebUi.Muted);
            RoleText(roleScroll, "mpLabel", "魔法", 322, 0, 310, 22, 16, WebUi.Muted);
            RoleBar(roleScroll, "hpBar", hp, stats.maxHp, 0, 24, 310, 20, "#ff7a7a", "#c22d2d");
            RoleBar(roleScroll, "mpBar", mp, stats.maxMp, 322, 24, 310, 20, "#7ab6ff", "#2d63c2");
            RoleText(roleScroll, "hpValue", $"{hp} / {stats.maxHp}", 0, 24, 310, 20, 13, Color.white, TextAnchor.MiddleCenter);
            RoleText(roleScroll, "mpValue", $"{mp} / {stats.maxMp}", 322, 24, 310, 20, 13, Color.white, TextAnchor.MiddleCenter);

            var rows = new (string key, float value)[] { ("atk", stats.atk), ("matk", stats.matk), ("def", stats.def),
                ("mdef", stats.mdef), ("hp", stats.maxHp), ("mp", stats.maxMp), ("spd", stats.spd),
                ("crit", stats.crit), ("dodge", stats.dodge) };
            var data = GameData.Raw;
            var gear = RoleStats.GearSum(Bag);
            int y = 56;
            foreach (var row in rows)
            {
                var k = row.key;
                float multiplier = k == "spd" ? 1 : ((float?)cls["mod"]?[k] ?? 1);
                int basic = (int)Math.Round(((float?)data["base"][k] ?? 0) * multiplier, MidpointRounding.AwayFromZero);
                gear.TryGetValue(k, out float extra);
                int heart = (int)(((float?)cls["heart"]?["perLv"]?[k] ?? 0) * Role.lv);
                int growth = (int)row.value - (int)extra - basic - heart;
                string detail = k == "crit" ? "暴击时伤害 ×1.5" : k == "dodge" ? "被攻击时有几率完全躲开" :
                    "基础 " + Fmt(k, basic) + (growth == 0 ? "" : " + 成长 " + Fmt(k, growth)) + (heart == 0 ? "" : " + 心法 " + heart);
                if (extra != 0)
                    detail += " <color=#7ee08a>+ 装备 " + Fmt(k, extra) + "</color>";
                RoleAttributeRow(k, StatName[k], Fmt(k, row.value), detail, y);
                y += 48;
            }
            RoleAttributeRow("coin", "银两", Role.coin.ToString("N0"), "", y);
            return y + 58;
        }

        void RoleAttributeRow(string id, string name, string value, string detail, int y)
        {
            RoleText(roleScroll, id, name, 0, y, 104, 44, 20, WebUi.Text);
            RoleText(roleScroll, id + "Value", value, 105, y, 110, 44, 20, WebUi.Amber, TextAnchor.MiddleRight);
            RoleText(roleScroll, id + "Detail", detail, 232, y, 400, 44, 15, WebUi.Muted).supportRichText = true;
            var line = Ui.Box("line", roleScroll, WebUi.C("#e2b04a40"));
            line.raycastTarget = false;
            WebUi.At(line.rectTransform, 0, y + 45, RoleWidth, 1);
        }

        int DrawRoleSkills(JToken cls)
        {
            int y = 0;
            var heart = cls["heart"];
            if (heart != null)
                y += DrawSkillRow((string)heart["name"], (string)heart["desc"], "被动", y, true);
            var learned = cls["skills"];
            foreach (var key in learned ?? GameData.Raw["playerSkills"])
            {
                if (!Battle.BattleFactory.Skills.TryGetValue((string)key, out var skill)) continue;
                int level = TrainService.Level(Role, (string)key);
                bool usable = learned != null ? level > 0 : Role.lv >= Math.Max(1, skill.lv);
                string status = usable ? "法力 " + skill.mp.ToString("0.#") : learned != null ? "未学习" : Math.Max(1, skill.lv) + " 级解锁";
                y += DrawSkillRow(skill.name + (learned != null && usable ? "  Lv." + level : ""), skill.desc, status, y, usable);
            }
            return y + 10;
        }

        int DrawSkillRow(string name, string description, string status, int y, bool usable)
        {
            var panel = WebUi.Panel("skill", roleScroll);
            RoleText(panel.transform, "name", name, 12, 3, 460, 32, 22, usable ? WebUi.Light : WebUi.Muted);
            var text = RoleText(panel.transform, "description", description ?? "", 12, 38, 471, 28, 16, WebUi.Muted, TextAnchor.UpperLeft);
            int textHeight = Mathf.Max(23, Mathf.CeilToInt(text.preferredHeight) + 2);
            WebUi.At(text.rectTransform, 12, 38, 471, textHeight);
            RoleText(panel.transform, "status", status, 482, 10, 134, 40, 17, usable ? WebUi.Amber : WebUi.Muted, TextAnchor.MiddleRight);
            WebUi.At(panel.rectTransform, 0, y, RoleWidth, 47 + textHeight);
            return 55 + textHeight;
        }

        int DrawRoleEquipment(StatBlock stats)
        {
            var doll = WebUi.Panel("doll", roleScroll);
            WebUi.At(doll.rectTransform, 0, 0, RoleWidth, 518);
            var figure = WebUi.Icon("equippedRole", doll.transform, "cls_" + Role.cls);
            WebUi.At(figure.rectTransform, 115, 60, 400, 396);
            RoleText(doll.transform, "name", Role.name + "  Lv." + Role.lv + " · " + SectService.ClsName(Role.cls), 113, 5, 406, 29, 21, WebUi.Light, TextAnchor.MiddleCenter);
            RoleText(doll.transform, "stats", $"攻击 {stats.atk}　防御 {stats.def}　速度 {stats.spd}　气血 {stats.maxHp}", 100, 469, 432, 39, 17, WebUi.Amber, TextAnchor.MiddleCenter);

            var d = GameData.Raw;
            var slots = new[] { d["slotLeft"], d["slotRight"] };
            for (int column = 0; column < slots.Length; column++)
            {
                int row = 0;
                foreach (var item in slots[column])
                {
                    string slot = (string)item;
                    Bag.equip.TryGetValue(slot, out var equipped);
                    Bag.gear.TryGetValue(slot, out var gear);
                    RoleItemCell(doll.transform, equipped, SlotName(slot), gear?.star > 0 ? "★" + gear.star : "",
                        column == 0 ? 10 : 530, 9 + row++ * 83, 92, 74, selSlot == slot,
                        () => { selSlot = slot; selId = null; Redraw(); });
                }
            }

            var ids = Bag.items.Keys.Where(id => Items[id] != null && Bag.items[id] > 0).ToList();
            if (selSlot == null && selId == null)
                selId = ids.FirstOrDefault(id => (string)Items[id]["type"] == "equip") ?? ids.FirstOrDefault();
            int count = Math.Max(10, ((ids.Count + 4) / 5) * 5);
            for (int i = 0; i < count; i++)
            {
                string id = i < ids.Count ? ids[i] : null;
                var slot = id == null ? null : (string)Items[id]["slot"];
                RoleItemCell(roleScroll, id, slot == null ? "" : SlotName(slot), id == null ? "" : Bag.items[id].ToString(),
                    i % 5 * 128, 530 + i / 5 * 115, 120, 107, id != null && selId == id,
                    id == null ? (Action)null : () => { selId = id; selSlot = null; Redraw(); });
            }
            int y = 530 + count / 5 * 115;
            y += DrawRoleItemDetails(y);
            WebUi.At((RectTransform)Ui.Btn("fullBag", roleScroll, "打开完整背包", 20, WebUi.Purple, () => Open(Tab.Bag)).transform, 206, y + 8, 220, 44);
            return y + 59;
        }

        void RoleItemCell(Transform parent, string id, string label, string count, int x, int y, int w, int h, bool selected, Action action)
        {
            var min = new Vector2(x, -y - h);
            var max = new Vector2(x + w, -y);
            var cell = Cell(parent, id, count, min, max, selected, action == null ? (UnityEngine.Events.UnityAction)null : () => action());
            cell.interactable = action != null;
            if (id != null)
            {
                var icon = (RectTransform)cell.transform.Find("ic");
                float size = Math.Min(72, h - 26);
                WebUi.At(icon, (w - size) / 2, 4, size, size);
                cell.transform.Find("n").gameObject.SetActive(false);
            }
            if (label.Length > 0)
            {
                var text = RoleText(cell.transform, "slotLabel", label, 3, h - 23, w - 6, 20, 15, WebUi.Light, TextAnchor.MiddleCenter);
                WebUi.Edge(text);
            }
        }

        int DrawRoleItemDetails(int y)
        {
            var box = WebUi.Panel("roleItemDetails", roleScroll);
            string id = selId;
            if (selSlot != null) Bag.equip.TryGetValue(selSlot, out id);
            var item = id == null ? null : Items[id];
            if (item == null)
            {
                WebUi.At(box.rectTransform, 0, y, RoleWidth, 64);
                RoleText(box.transform, "empty", selSlot == null ? "选择背包物品或装备部位" : SlotName(selSlot) + "还空着，可从背包物品格选择装备。", 12, 0, 607, 62, 18, WebUi.Muted);
                return 64;
            }
            PlaceIcon(box.transform, Icon(id), 12, 15, 72);
            RoleText(box.transform, "name", ItemName(id), 98, 10, 395, 35, 22, QColor(id));
            string type = (string)item["type"];
            string desc = (string)item["desc"] ?? "";
            if (type == "equip")
                desc = (selSlot == null ? StatText(id) + "\n" + CompareText(id) :
                    BonusText(RoleStats.SlotBonus(Bag, selSlot))) + "\n" + desc;
            RoleText(box.transform, "description", desc, 98, 49, 385, 112, 16, WebUi.Muted, TextAnchor.UpperLeft).supportRichText = true;
            string command = selSlot != null ? BagCmd.Unequip : type == "equip" ? BagCmd.Equip : type == "potion" ? BagCmd.Use : null;
            if (command != null)
            {
                string slot = selSlot;
                string label = slot != null ? "卸下" : type == "equip" ? "装备" : "使用";
                WebUi.At((RectTransform)Ui.Btn("itemAction", box.transform, label, 19, WebUi.Red,
                    () => Do(command, new BagOpReq { id = slot == null ? id : null, slot = slot })).transform, 496, 18, 123, 44);
            }
            WebUi.At(box.rectTransform, 0, y, RoleWidth, 169);
            return 169;
        }
    }
}
