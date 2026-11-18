using UnityEditor;
using UnityEngine;

namespace GuiQin.EditorTools
{
    // Resources/GuiQin/Art 下的图：默认导入设置会把贴图缩到 2048 并加糊，这里统一改成：
    // 最大 8192、不要 mipmap、不缩放 NPOT、高质量压缩（桌面 BC7 / 手机 ASTC）、透明边缘处理。
    public class ArtImporter : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.Replace('\\', '/').Contains("/Resources/GuiQin/Art/")) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Default;
            ti.npotScale = TextureImporterNPOTScale.None;
            ti.mipmapEnabled = false;
            ti.maxTextureSize = 8192;
            ti.isReadable = false;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.filterMode = FilterMode.Bilinear;
            ti.alphaIsTransparency = true;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
        }
    }
}
