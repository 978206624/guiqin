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
            string path = assetPath.Replace('\\', '/');
            var ti = (TextureImporter)assetImporter;
            if (path.Contains("/Resources/GuiQin/Water/"))
            {
                // 水面蒙版：灰度数据，不是颜色。要能被脚本读（取水面采样点 / 瀑布落点），不压缩、不做 sRGB、不要 mipmap
                ti.textureType = TextureImporterType.Default;
                ti.sRGBTexture = false;
                ti.isReadable = true;
                ti.mipmapEnabled = false;
                ti.npotScale = TextureImporterNPOTScale.None;
                ti.maxTextureSize = 4096;
                ti.wrapMode = TextureWrapMode.Clamp;
                ti.filterMode = FilterMode.Bilinear;
                ti.alphaSource = TextureImporterAlphaSource.None;
                ti.textureCompression = TextureImporterCompression.Uncompressed;
                return;
            }
            if (!path.Contains("/Resources/GuiQin/Art/")) return;
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
