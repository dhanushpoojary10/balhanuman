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
        if (assetPath.Contains("/Idle24/"))
        {
            // the 24-frame idle is drawn larger than the other clips: pixels-per-unit keeps Hanuman the same size in game
            string f = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(assetPath), "_ppu.txt");
            if (System.IO.File.Exists(f) && float.TryParse(System.IO.File.ReadAllText(f).Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float ppu))
                ti.spritePixelsPerUnit = ppu;
            ti.maxTextureSize = 512; ti.textureCompression = TextureImporterCompression.Uncompressed;
        }
        // any folder with a _ppu.txt ("<pixels per unit> <bottom|center>") is a set of frames cut for a known world size
        {
            string dir = System.IO.Path.GetDirectoryName(assetPath);
            string f2 = System.IO.Path.Combine(dir, "_ppu.txt");
            if (System.IO.File.Exists(f2))
            {
                var parts = System.IO.File.ReadAllText(f2).Trim().Split(new[] { ' ', '\t', '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0 && float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float ppu2))
                {
                    ti.spritePixelsPerUnit = ppu2;
                    var gs = new TextureImporterSettings();
                    ti.ReadTextureSettings(gs);
                    gs.spriteAlignment = (int)((parts.Length > 1 && parts[1] == "center") ? SpriteAlignment.Center : SpriteAlignment.BottomCenter);
                    ti.SetTextureSettings(gs);
                    ti.textureCompression = TextureImporterCompression.Uncompressed;
                    ti.maxTextureSize = 1024;
                }
            }
        }
        if (assetPath.Contains("/Character/Gada/"))
        {   // (runs after the generic _ppu.txt rule above, so its per-pose feet anchors win)
            // Hanuman with his gada (cut from one sheet): one pixels-per-unit for the set (_ppu.txt) so he matches his
            // other frames, and each pose stands on its own feet point (_pivots.txt: "name x-fraction" per line)
            string dir = System.IO.Path.GetDirectoryName(assetPath);
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            string pf = System.IO.Path.Combine(dir, "_ppu.txt");
            if (System.IO.File.Exists(pf) && float.TryParse(System.IO.File.ReadAllText(pf).Trim(), System.Globalization.NumberStyles.Float, inv, out float ppu))
                ti.spritePixelsPerUnit = ppu;
            string vf = System.IO.Path.Combine(dir, "_pivots.txt");
            string me = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            if (System.IO.File.Exists(vf))
                foreach (var line in System.IO.File.ReadAllLines(vf))
                {
                    var parts = line.Trim().Split(' ');
                    if (parts.Length == 2 && parts[0] == me && float.TryParse(parts[1], System.Globalization.NumberStyles.Float, inv, out float px))
                    {
                        var gs = new TextureImporterSettings(); ti.ReadTextureSettings(gs);
                        gs.spriteAlignment = (int)SpriteAlignment.Custom; gs.spritePivot = new Vector2(px, 0f);
                        ti.SetTextureSettings(gs);
                    }
                }
            ti.textureCompression = TextureImporterCompression.Uncompressed;
        }
        if (assetPath.Contains("/Water/") || assetPath.Contains("/Temple/"))
        {
            // stream and temple pieces: crisp, uncompressed; props stand on their bottom edge, strips and glows use the centre
            string n = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            bool standing = n.StartsWith("log_") || n.StartsWith("stone_") || n == "waterfall" || n == "int_altar"
                            || (n.StartsWith("ext_") && !n.StartsWith("ext_garland"));
            var ts = new TextureImporterSettings();
            ti.ReadTextureSettings(ts);
            ts.spriteAlignment = (int)(standing ? SpriteAlignment.BottomCenter : SpriteAlignment.Center);
            ti.SetTextureSettings(ts);
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.maxTextureSize = 2048;
        }
        // stepping rocks: stretchable between the grass cap (top 70 px) and the plants at the base (bottom 60 px),
        // so a rock can be any height and still rise out of the water (borders: left, bottom, right, top)
        if (assetPath.EndsWith("Ground/stone.png")) ti.spriteBorder = new Vector4(0, 60, 0, 70);
        if (assetPath.Contains("/Backdrop/")) { ti.maxTextureSize = 4096; ti.textureCompression = TextureImporterCompression.CompressedHQ; ti.filterMode = FilterMode.Bilinear; }
        if (assetPath.Contains("/UI2/"))
        {
            string n = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            switch (n)
            {
                case "panel_large":
                case "panel_large_frame":                                                     // gold frame only (see-through middle)
                case "panel_large_fill": ti.spriteBorder = new Vector4(78, 78, 78, 78); break; // glass fill shaped like its inside
                case "hud_wide": ti.spriteBorder = new Vector4(74, 52, 74, 52); break;
                case "hud_mid": ti.spriteBorder = new Vector4(60, 38, 60, 38); break;
                case "hud_pill":
                case "hud_pill_primary": ti.spriteBorder = new Vector4(46, 30, 46, 30); break;
            }
            ti.maxTextureSize = 2048; ti.textureCompression = TextureImporterCompression.Uncompressed;
        }
        if (assetPath.EndsWith("UI/panel.png")) ti.spriteBorder = new Vector4(72, 72, 72, 72);
    }
}
