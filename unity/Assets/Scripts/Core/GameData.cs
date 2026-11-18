using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace GuiQin
{
    // 全部静态数据：启动时从 Resources/GuiQin/Data 读 JSON（由 tools/export_unity_assets.py 生成）。
    public static class GameData
    {
        public static JObject Raw;                       // data.json 原样保留，后续系统（战斗/背包/任务）按需取
        public static JObject Story;
        public static AssetsMeta Assets;
        public static Dictionary<string, MapDef> Maps;
        public static Dictionary<string, NpcInfo> Npcs;
        public static Dictionary<string, EnemyDef> Enemies;
        public static Dictionary<string, StageDef> Stages = new Dictionary<string, StageDef>();
        public static readonly string[] MapOrder = { "village", "outskirts", "creek", "mist", "pine", "wolfvalley", "town", "wild", "taixuforest", "taixupeak", "taixuvalley" };
        static readonly Dictionary<string, MaskDef> masks = new Dictionary<string, MaskDef>();
        static bool loaded;

        static string Text(string name)
        {
            var ta = Resources.Load<TextAsset>("GuiQin/Data/" + name);
            if (ta == null) { Debug.LogError("缺数据文件：Resources/GuiQin/Data/" + name + ".json（先运行 tools/export_unity_assets.py）"); return "{}"; }
            return ta.text;
        }

        public static void Load()
        {
            if (loaded) return;
            Raw = JObject.Parse(Text("data"));
            Story = JObject.Parse(Text("story"));
            Assets = JsonConvert.DeserializeObject<AssetsMeta>(Text("assets"));
            Maps = Raw["maps"].ToObject<Dictionary<string, MapDef>>();
            Npcs = Raw["npcs"].ToObject<Dictionary<string, NpcInfo>>();
            Enemies = Raw["enemies"].ToObject<Dictionary<string, EnemyDef>>();
            foreach (var st in Raw["stages"].ToObject<List<StageDef>>()) Stages[st.id] = st;
            loaded = true;
        }

        // 只有 village / town 有碰撞位图，其它地图靠 walk / roads 多边形
        public static MaskDef Mask(string mapId)
        {
            MaskDef m;
            if (masks.TryGetValue(mapId, out m)) return m;
            var ta = Resources.Load<TextAsset>("GuiQin/Data/mask_" + mapId);
            m = ta == null ? null : JsonConvert.DeserializeObject<MaskDef>(ta.text);
            masks[mapId] = m;
            return m;
        }
    }
}
