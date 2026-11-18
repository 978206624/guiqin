using UnityEngine;

namespace GuiQin
{
    // Unity 只负责从自己的资源系统读取共享规则所需的文本。
    internal sealed class UnityRulesDataSource : IDataSource
    {
        public string ReadText(string name)
        {
            var asset = Resources.Load<TextAsset>("GuiQin/Data/" + name);
            return asset == null ? null : asset.text;
        }
    }
}
