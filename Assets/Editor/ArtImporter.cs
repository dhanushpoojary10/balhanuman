using UnityEditor;
using UnityEngine;

/// <summary>Imports everything under Assets/Art as sprites with sensible 2D defaults.</summary>
public class ArtImporter : AssetPostprocessor
{
    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/Art")) return;
        var ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = 100;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        ti.filterMode = FilterMode.Bilinear;
        ti.wrapMode = TextureWrapMode.Clamp;
        ti.maxTextureSize = 2048;

        var settings = new TextureImporterSettings();
        ti.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteAlignment = (int)SpriteAlignment.Center;

        bool bottom = assetPath.Contains("/Character/") || assetPath.Contains("/Enemies/") || assetPath.Contains("/NPCs/")
                      || assetPath.Contains("/Props/") || assetPath.Contains("/Interactive/") || assetPath.Contains("/Hazards/")
                      || assetPath.Contains("/Vegetation/") || assetPath.Contains("/Interactive_Hazards/") || assetPath.Contains("/Animals/") || assetPath.Contains("/generated/");
        if (assetPath.Contains("/bat/") || assetPath.Contains("/Expressions/")) bottom = false;
        if (bottom) settings.spriteAlignment = (int)SpriteAlignment.BottomCenter;
        ti.SetTextureSettings(settings);
        if (assetPath.EndsWith("UI/panel.png")) ti.spriteBorder = new Vector4(72, 72, 72, 72);
    }
}
