using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using GuiQin.Battle;
using GuiQin.Inventory;
using GuiQin.Quest;

namespace GuiQin
{
    // 参数是逻辑文件名，不带 .json。缺少可选文件时返回 null。
    public interface IDataSource
    {
        string ReadText(string name);
    }

    // 宿主启动时加载一次，开始处理请求后只读配置。
    public static class GameData
    {
        static readonly object Gate = new object();
        static volatile bool loaded;
        static Dictionary<string, MaskDef> masks;
        internal static IReadOnlyDictionary<string, SkillDef> Skills { get; private set; }
        internal static IReadOnlyDictionary<string, string> ItemNames { get; private set; }
        public static IReadOnlyList<MallOffer> MallOffers { get; private set; }
        public static JObject Raw { get; private set; }
        public static JObject Story { get; private set; }
        public static AssetsMeta Assets { get; private set; }
        public static Dictionary<string, MapDef> Maps { get; private set; }
        public static Dictionary<string, NpcInfo> Npcs { get; private set; }
        public static Dictionary<string, EnemyDef> Enemies { get; private set; }
        public static Dictionary<string, StageDef> Stages { get; private set; }
        public static readonly string[] MapOrder = { "village", "outskirts", "creek", "mist", "pine", "wolfvalley", "town", "wild", "taixuforest", "taixupeak", "taixuvalley" };

        static string Required(IDataSource source, string name)
        {
            return source.ReadText(name) ?? throw new InvalidOperationException("缺少规则数据：" + name + ".json");
        }

        public static void Load(IDataSource source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (loaded) return;
            lock (Gate)
            {
                if (loaded) return;
                var raw = JObject.Parse(Required(source, "data"));
                var mallText = source.ReadText("mall");
                var mall = mallText == null ? null : JObject.Parse(mallText);
                if (mall != null)
                    foreach (var item in ((JObject)mall["items"]).Properties())
                        ((JObject)raw["items"]).Add(item.Name, item.Value.DeepClone());
                var offers = mall == null ? new List<MallOffer>() : mall["offers"].ToObject<List<MallOffer>>();
                var offerIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (var offer in offers)
                    if (!offerIds.Add(offer.id) || offer.price <= 0 || raw["items"][offer.item] == null ||
                        (offer.currency != "coin" && offer.currency != "yuanbao" && offer.currency != "silverNote"))
                        throw new InvalidOperationException("商城货架配置错误：" + offer.id);
                var story = JObject.Parse(Required(source, "story"));
                var assets = JsonConvert.DeserializeObject<AssetsMeta>(Required(source, "assets"));
                var maps = raw["maps"].ToObject<Dictionary<string, MapDef>>();
                var npcs = raw["npcs"].ToObject<Dictionary<string, NpcInfo>>();
                var enemies = raw["enemies"].ToObject<Dictionary<string, EnemyDef>>();
                var stages = new Dictionary<string, StageDef>();
                foreach (var stage in raw["stages"].ToObject<List<StageDef>>()) stages[stage.id] = stage;
                var skills = raw["skills"].ToObject<Dictionary<string, SkillDef>>();
                var names = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var item in ((JObject)raw["items"]).Properties()) names.Add(item.Name, (string)item.Value["name"]);
                var mapMasks = new Dictionary<string, MaskDef>();
                foreach (var id in maps.Keys)
                {
                    var text = source.ReadText("mask_" + id);
                    if (text != null) mapMasks.Add(id, JsonConvert.DeserializeObject<MaskDef>(text));
                }

                StoryBeats.Build(story, npcs);
                MallOffers = offers.AsReadOnly();
                Raw = raw; Story = story; Assets = assets;
                Maps = maps; Npcs = npcs; Enemies = enemies; Stages = stages;
                Skills = new ReadOnlyDictionary<string, SkillDef>(skills);
                ItemNames = new ReadOnlyDictionary<string, string>(names);
                masks = mapMasks;
                loaded = true;
            }
        }

        // 舆图地点和玄兵塔楼层可直达，独立副本使用自己的入口。
        public static bool CanWorldTeleport(MapDef map) => map.world != null || map.tower > 0;

        // 没有碰撞位图的地图使用道路和多边形。
        public static MaskDef Mask(string mapId) => masks.TryGetValue(mapId, out var mask) ? mask : null;
    }
}
