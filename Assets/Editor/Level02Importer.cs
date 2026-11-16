using UnityEditor;
using UnityEngine;

namespace BalHanuman.EditorTools
{
    /// <summary>Rig part sprites under Assets/Resources/Level02: sprites, full quality, centre pivot (the rig places them by skin.json).</summary>
    public class Level02Importer : AssetPostprocessor
    {
        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith("Assets/Resources/Level02/")) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.spritePixelsPerUnit = 100;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.filterMode = FilterMode.Bilinear;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.maxTextureSize = 2048;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            var s = new TextureImporterSettings();
            ti.ReadTextureSettings(s);
            s.spriteMeshType = SpriteMeshType.FullRect;
            s.spriteAlignment = (int)SpriteAlignment.Center;
            ti.SetTextureSettings(s);

            // phones: ASTC keeps the 400+ painted frames at a fraction of the memory (uncompressed they would need hundreds of MB)
            var android = ti.GetPlatformTextureSettings("Android");
            android.overridden = true;
            android.maxTextureSize = 2048;
            android.format = TextureImporterFormat.ASTC_6x6;
            android.compressionQuality = 50;
            ti.SetPlatformTextureSettings(android);
        }

        public override uint GetVersion() => 2;      // bump to re-import the Level 2 textures when these settings change
    }
}
