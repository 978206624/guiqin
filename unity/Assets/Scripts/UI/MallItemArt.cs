using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace GuiQin
{
    // 商城图标索引从图集左上角起，按行排列。商城和背包按同一个物品 ID 取图。
    internal static class MallItemArt
    {
        const int Cell = 80, Columns = 12;
        static Dictionary<string, int> indices;
        static readonly Dictionary<string, Sprite> Sprites = new Dictionary<string, Sprite>();
        static Texture2D atlas;

        public static Sprite Get(string item)
        {
            if (string.IsNullOrEmpty(item)) return null;
            if (indices == null)
            {
                indices = new Dictionary<string, int>();
                var json = Resources.Load<TextAsset>("GuiQin/Mall/icons");
                if (json != null)
                {
                    var ids = JsonConvert.DeserializeObject<string[]>(json.text);
                    for (int i = 0; i < ids.Length; i++) indices.Add(ids[i], i);
                }
            }
            if (!indices.TryGetValue(item, out int index)) return null;
            if (Sprites.TryGetValue(item, out var sprite)) return sprite;
            if (atlas == null) atlas = Resources.Load<Texture2D>("GuiQin/Art/mall_items");
            if (atlas == null) return null;

            int x = index % Columns * Cell;
            int y = atlas.height - (index / Columns + 1) * Cell;
            sprite = Sprite.Create(atlas, new Rect(x, y, Cell, Cell), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
            sprite.name = item;
            Sprites.Add(item, sprite);
            return sprite;
        }
    }
}
