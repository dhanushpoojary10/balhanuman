using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEngine;

namespace BalHanuman.EditorTools
{
    /// <summary>Applies the Bal Hanuman app icon (Assets/Art/AppIcon) to the default, Android (legacy / round / adaptive) and iOS icon slots.</summary>
    public static class AppIconSetup
    {
        const string Dir = "Assets/AppIcon/";

        static Texture2D Load(string name)
        {
            string p = Dir + name;
            var imp = (TextureImporter)AssetImporter.GetAtPath(p);
            if (imp != null)
            {
                imp.textureType = TextureImporterType.Default; imp.mipmapEnabled = false; imp.npotScale = TextureImporterNPOTScale.None;
                imp.isReadable = false; imp.textureCompression = TextureImporterCompression.Uncompressed; imp.maxTextureSize = 2048;
                imp.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(p);
        }

        [MenuItem("Bal Hanuman/Apply App Icon")]
        public static void Apply()
        {
            AssetDatabase.Refresh();
            var full = Load("icon_1024.png"); var bg = Load("icon_adaptive_bg.png"); var fg = Load("icon_adaptive_fg.png");
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { full }, IconKind.Application);

            var android = NamedBuildTarget.Android;
            foreach (var kind in new PlatformIconKind[] { AndroidPlatformIconKind.Adaptive })
            {
                var icons = PlayerSettings.GetPlatformIcons(android, kind);
                foreach (var ic in icons)
                {
                    if (kind == AndroidPlatformIconKind.Adaptive) ic.SetTextures(bg, fg);
                    else ic.SetTextures(full);
                }
                PlayerSettings.SetPlatformIcons(android, kind, icons);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[Icon] applied");
        }
    }
}
