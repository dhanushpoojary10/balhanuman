using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace BalHanuman.EditorTools
{
    /// <summary>Builds prefabs + the Level01_MissingFruitOffering and MainMenu scenes from the HQ art. Menu: Bal Hanuman > Build Level 1 (HQ).</summary>
    public static class Level01Builder
    {
        const string ScenePath = "Assets/Scenes/Level01_MissingFruitOffering.unity";
        const string MenuScenePath = "Assets/Scenes/MainMenu.unity";
        const string Art = "Assets/ArtHQ/";

        static Transform envRoot, propRoot, animalRoot, pickupRoot, interactRoot, decorFront;
        static PhysicsMaterial2D noFriction;
        static readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>();

        // ============================================================ helpers
        static Sprite S(string p)
        {
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(Art + p + ".png");
            if (s == null) Debug.LogError("Missing sprite: " + p);
            return s;
        }

        static Sprite[] F(string folder, string prefix, int a, int b)
        {
            var l = new List<Sprite>();
            for (int i = a; i <= b; i++) l.Add(S(folder + "/" + prefix + "_" + i.ToString("00")));
            return l.ToArray();
        }

        static GameObject Spr(string path, float x, float y, float h, int order, Transform parent, bool flip = false)
        {
            var s = S(path);
            var go = new GameObject(System.IO.Path.GetFileName(path));
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(x, y, 0);
            go.transform.localScale = Vector3.one * (h / s.bounds.size.y);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = s; sr.sortingOrder = order; sr.flipX = flip;
            return go;
        }

        static GameObject SprW(string path, float x, float y, float w, int order, Transform parent, bool flip = false)
        {
            var s = S(path);
            return Spr(path, x, y, w / s.bounds.size.x * s.bounds.size.y, order, parent, flip);
        }

        static void EnsureLayers()
        {
            var tm = new SerializedObject(AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/TagManager.asset"));
            var layers = tm.FindProperty("layers");
            string[] want = { "Ground", "Player" };
            for (int i = 0; i < want.Length; i++)
            {
                var p = layers.GetArrayElementAtIndex(6 + i);
                if (string.IsNullOrEmpty(p.stringValue) || p.stringValue == want[i]) p.stringValue = want[i];
                else Debug.LogWarning("Layer slot " + (6 + i) + " in use by " + p.stringValue);
            }
            tm.ApplyModifiedProperties();
        }

        static void EnsureFolders()
        {
            foreach (var f in new[] { "Assets/Scenes", "Assets/Prefabs", "Assets/Prefabs/Player", "Assets/Prefabs/Environment", "Assets/Prefabs/Collectibles", "Assets/Prefabs/Interactables", "Assets/Prefabs/UI" })
            {
                if (AssetDatabase.IsValidFolder(f)) continue;
                int i = f.LastIndexOf('/');
                AssetDatabase.CreateFolder(f.Substring(0, i), f.Substring(i + 1));
            }
        }

        static GameObject SavePrefab(GameObject go, string path)
        {
            var p = PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            prefabs[System.IO.Path.GetFileNameWithoutExtension(path)] = p;
            return p;
        }

        static GameObject Place(string prefabName, Vector3 pos, Transform parent = null)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefabs[prefabName]);
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.position = pos;
            return go;
        }

        static Sprite Sparkle => S("VFX/fx_05");

        static GameObject AddGlow(GameObject go, float worldDiameter, Color c, int order)
        {
            var g = new GameObject("Glow"); g.transform.SetParent(go.transform, false);
            var sr = g.AddComponent<SpriteRenderer>(); sr.sprite = S("UI/glow"); sr.color = c; sr.sortingOrder = order;
            float parent = go.transform.localScale.x;
            g.transform.localScale = Vector3.one * (worldDiameter / sr.sprite.bounds.size.x / parent);
            return g;
        }

        static GameObject AddShadow(Transform owner, float width, bool follow)
        {
            var sh = new GameObject("Shadow");
            sh.transform.SetParent(owner, false);
            var sr = sh.AddComponent<SpriteRenderer>(); sr.sprite = S("UI/circle"); sr.sortingOrder = 4;
            sr.color = new Color(0, 0, 0, 0.3f);
            if (follow)
            {
                var bs = sh.AddComponent<BlobShadow>(); bs.owner = owner; bs.groundMask = LayerMask.GetMask("Ground"); bs.baseWidth = width; sr.sortingOrder = 9;
            }
            else
            {
                float parent = owner.lossyScale.x;
                sh.transform.localPosition = new Vector3(0, 0.03f, 0);
                sh.transform.localScale = new Vector3(width / sr.sprite.bounds.size.x / parent, width * 0.22f / sr.sprite.bounds.size.x / parent, 1);
            }
            return sh;
        }

        // ============================================================ prefabs
        static void BuildPrefabs()
        {
            // ---- Player
            var player = new GameObject("BalHanuman");
            player.layer = LayerMask.NameToLayer("Player");
            var cc = player.AddComponent<CapsuleCollider2D>();
            cc.size = new Vector2(0.7f, 1.5f); cc.offset = new Vector2(0, 0.75f); cc.sharedMaterial = noFriction;
            var rb = player.AddComponent<Rigidbody2D>();
            rb.freezeRotation = true;
            var vis = new GameObject("Visual"); vis.transform.SetParent(player.transform, false);
            var idle0 = S("Character/Idle24/idle24_01");
            vis.transform.localScale = Vector3.one * (1.9f / idle0.bounds.size.y);
            var psr = vis.AddComponent<SpriteRenderer>(); psr.sprite = idle0; psr.sortingOrder = 10;
            var anim = vis.AddComponent<Animator>();
            anim.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/BalHanuman/BalHanuman.controller");
            var gc = new GameObject("GroundCheck"); gc.transform.SetParent(player.transform, false); gc.transform.localPosition = new Vector3(0, -0.04f, 0);
            var pc = player.AddComponent<PlayerController2D>();
            pc.groundCheck = gc.transform; pc.groundMask = LayerMask.GetMask("Ground"); pc.animator = anim; pc.spriteRenderer = psr; pc.dustSprite = S("VFX/fx_13");
            pc.acceleration = 42f; pc.deceleration = 34f;     // a little slide when he lets go of the stick, instead of stopping dead
            var pi = player.AddComponent<PlayerInteractor>(); pi.player = pc;
            AddShadow(player.transform, 1.0f, true);
            var carried = BasketVisual("CarriedBasket", 0.9f);
            carried.transform.SetParent(player.transform, false);
            carried.transform.localPosition = new Vector3(0.0f, 2.25f, 0);
            SavePrefab(player, "Assets/Prefabs/Player/BalHanuman.prefab");

            // ---- collectibles
            prefabs["Mango"] = MakeCollectible("Mango", "Items/item_01", 0.75f, CollectibleKind.Mango, 1, 0.4f);
            prefabs["GoldCoin"] = MakeCollectible("GoldCoin", "Items/item_03", 0.5f, CollectibleKind.GoldCoin, 1, 0.35f);
            prefabs["Lotus"] = MakeCollectible("Lotus", "Items/item_10", 0.7f, CollectibleKind.Lotus, 1, 0.45f);

            // ---- checkpoint flag
            var cp = new GameObject("Checkpoint");
            var flag = Spr("Items/item_29", 0, 0, 2.6f, 3, cp.transform); flag.name = "Flag";
            var bc = cp.AddComponent<BoxCollider2D>(); bc.isTrigger = true; bc.size = new Vector2(2.2f, 5f); bc.offset = new Vector2(0, 2.5f);
            var cpc = cp.AddComponent<Checkpoint>(); cpc.flag = flag.GetComponent<SpriteRenderer>(); cpc.sparkle = Sparkle;
            SavePrefab(cp, "Assets/Prefabs/Environment/Checkpoint.prefab");

            // ---- sage
            var sage = new GameObject("Sage");
            var sv = Spr("NPCs/elder_01", 0, 0, 2.0f, 5, sage.transform); sv.name = "Visual";
            var sa = sv.AddComponent<SpriteAnimator>(); sa.frames = F("NPCs", "elder", 1, 3); sa.fps = 1.6f;
            var marker = Spr("Items/item_27", 0, 2.45f, 0.5f, 7, sage.transform); marker.name = "Marker";
            var sn = sage.AddComponent<SageNPC>(); sn.sprite = sv.GetComponent<SpriteRenderer>(); sn.marker = marker.transform;
            AddShadow(sage.transform, 1.2f, false);
            SetRadius(sn, 3.2f);
            SavePrefab(sage, "Assets/Prefabs/Interactables/Sage.prefab");

            // ---- baby monkey
            var mk = new GameObject("BabyMonkey");
            var mv = Spr("Animals/animal_04", 0, 0, 1.35f, 6, mk.transform); mv.name = "Visual";
            var am = mk.AddComponent<AmbientAnimal>(); am.bobAmount = 0.03f; am.hopHeight = 0.25f;
            var bm = mk.AddComponent<BabyMonkey>(); bm.sparkle = Sparkle;
            AddShadow(mk.transform, 0.9f, false);
            SetRadius(bm, 2.8f);
            SavePrefab(mk, "Assets/Prefabs/Interactables/BabyMonkey.prefab");

            // ---- lever
            var lv = new GameObject("Lever");
            var lvs = Spr("Interactive_Hazards/obj_01", 0, 0, 1.1f, 6, lv.transform); lvs.name = "Visual";
            var lc = lv.AddComponent<Lever>(); lc.sprite = lvs.GetComponent<SpriteRenderer>();
            SetRadius(lc, 2.4f);
            SavePrefab(lv, "Assets/Prefabs/Interactables/Lever.prefab");

            // ---- mango basket
            var bk = BasketVisual("MangoBasket", 1.1f);
            AddGlow(bk, 2.2f, new Color(1f, 0.9f, 0.5f, 0.6f), 5);
            var bkc = bk.AddComponent<MangoBasket>(); bkc.sparkle = Sparkle;
            SetRadius(bkc, 2.6f);
            SavePrefab(bk, "Assets/Prefabs/Interactables/MangoBasket.prefab");

            // ---- return portal
            var pt = new GameObject("ReturnPortal");
            var pv = new GameObject("Visual"); pv.transform.SetParent(pt.transform, false);
            var beam = Spr("VFX/fx_14", 0, 0.05f, 3.6f, 6, pv.transform); beam.name = "Beam";
            var ring = Spr("VFX/fx_18", 0, 0.1f, 0.9f, 5, pv.transform); ring.name = "Ring";
            ring.transform.localScale = new Vector3(ring.transform.localScale.x * 1.7f, ring.transform.localScale.y * 1.7f, 1);
            var rp = pt.AddComponent<ReturnPortal>(); rp.visual = pv;
            SetRadius(rp, 2.2f);
            SavePrefab(pt, "Assets/Prefabs/Interactables/ReturnPortal.prefab");

            // ---- moving log
            var lg = new GameObject("MovingLog");
            lg.layer = LayerMask.NameToLayer("Ground");
            var lgs = SprW("Water/log_0", 0, -0.42f, 3.1f, 9, lg.transform); lgs.name = "Visual";
            var lbc = lg.AddComponent<BoxCollider2D>(); lbc.size = new Vector2(2.5f, 0.35f); lbc.offset = new Vector2(0, -0.12f); lbc.sharedMaterial = noFriction;
            var lrb = lg.AddComponent<Rigidbody2D>(); lrb.bodyType = RigidbodyType2D.Kinematic;
            var lmp = lg.AddComponent<MovingPlatform>();
            lmp.bobAmount = 0.05f; lmp.dipOnRide = 0.1f; lmp.ripple = S("Water/ripple_0");   // floats on the stream and sinks a little under his weight
            SavePrefab(lg, "Assets/Prefabs/Environment/MovingLog.prefab");

            // ---- vine
            var vn = new GameObject("ClimbableVine");
            var vbc = vn.AddComponent<BoxCollider2D>(); vbc.isTrigger = true; vbc.size = new Vector2(1.9f, 6.2f); vbc.offset = new Vector2(0, 3.1f);
            vn.AddComponent<ClimbableVine>();
            SavePrefab(vn, "Assets/Prefabs/Environment/ClimbableVine.prefab");
        }

        static void SetRadius(InteractableBase b, float r)
        {
            var so = new SerializedObject(b);
            so.FindProperty("interactionRadius").floatValue = r;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static GameObject MakeCollectible(string name, string sprite, float h, CollectibleKind kind, int value, float radius)
        {
            var go = Spr(sprite, 0, 0, h, 12, null); go.name = name;
            var cc = go.AddComponent<CircleCollider2D>(); cc.isTrigger = true;
            cc.radius = radius / go.transform.localScale.x; // radius given in world units
            AddGlow(go, kind == CollectibleKind.Mango ? 1.5f : 1.1f, kind == CollectibleKind.Lotus ? new Color(1f, 0.6f, 0.9f, 0.5f) : new Color(0.02f, 0.12f, 0.2f, 0.62f), 11);
            var c = go.AddComponent<Collectible>(); c.kind = kind; c.value = value; c.sparkle = Sparkle;
            var path = "Assets/Prefabs/Collectibles/" + name + ".prefab";
            return SavePrefab(go, path);
        }

        /// <summary>Basket with a few mangoes on top, built from the available basket + mango sprites.</summary>
        static GameObject BasketVisual(string name, float width)
        {
            var root = new GameObject(name);
            var b = SprW("Props/misc/misc_03", 0, 0, width, 6, root.transform); b.name = "Basket";
            var bs = b.GetComponent<SpriteRenderer>().sprite;
            float bh = width / bs.bounds.size.x * bs.bounds.size.y;
            float mh = width * 0.42f;
            var m1 = Spr("Items/item_01", -width * 0.2f, bh * 0.78f, mh, 7, root.transform); m1.name = "Mango1";
            var m2 = Spr("Items/item_02", width * 0.2f, bh * 0.78f, mh, 7, root.transform); m2.name = "Mango2";
            var m3 = Spr("Items/item_01", 0, bh * 0.95f, mh * 1.05f, 8, root.transform); m3.name = "Mango3";
            // root collider so the basket can be picked up when the object is a scene interactable
            var bc = root.AddComponent<BoxCollider2D>(); bc.isTrigger = true; bc.size = new Vector2(width, bh * 1.3f); bc.offset = new Vector2(0, bh * 0.6f);
            return root;
        }

        // ============================================================ terrain helpers
        static readonly string[] TopTiles = { "Platforms/tile_05", "Platforms/tile_02", "Platforms/tile_06", "Platforms/tile_01" };
        static readonly string[] FillTiles = { "Platforms/tile_09", "Platforms/tile_10" };
        static readonly List<Vector3> groundSegs = new List<Vector3>();
        static int FgPlantCount = 6;

        static GameObject Piece(Sprite sp, string name, float cx, float cy, float sx, float sy, int order, bool flipX = false, bool flipY = false)
        {
            var g = new GameObject(name); g.transform.SetParent(envRoot, false);
            g.transform.position = new Vector3(cx, cy, 0);
            g.transform.localScale = new Vector3(sx, sy, 1);
            var sr = g.AddComponent<SpriteRenderer>(); sr.sprite = sp; sr.sortingOrder = order; sr.flipX = flipX; sr.flipY = flipY;
            return g;
        }

        /// <summary>Solid ground: grass-and-boulder strips with end caps on top, big deep-rock fill below. Standing line = y.</summary>
        static void Ground(float x0, float x1, float y, bool scatter = true)
        {
            if (scatter) groundSegs.Add(new Vector3(x0, x1, y));
            var seg = new GameObject("Ground_" + x0 + "_" + x1);
            seg.transform.SetParent(envRoot, false);
            seg.layer = LayerMask.NameToLayer("Ground");
            seg.transform.position = new Vector3((x0 + x1) / 2f, y - 3f, 0);
            var bc = seg.AddComponent<BoxCollider2D>(); bc.size = new Vector2(x1 - x0, 6f); bc.sharedMaterial = noFriction;

            var cl = S("Ground/cap_left"); var cr = S("Ground/cap_right"); var st = S("Ground/strip"); var df = S("Ground/deep_fill");
            const float H = 1.9f;                 // world height of the strip
            float k = H / st.bounds.size.y;       // world units per sprite unit
            const float topPad = 0.5f;            // sprite top sits this far above the standing line (ferns and flowers)
            float capWL = cl.bounds.size.x * k, capWR = cr.bounds.size.x * k;
            float capHL = cl.bounds.size.y * k, capHR = cr.bounds.size.y * k;
            Piece(cl, "CapLeft", x0 + capWL / 2f, y + topPad - capHL / 2f, k, k, 3);
            Piece(cr, "CapRight", x1 - capWR / 2f, y + topPad - capHR / 2f, k, k, 3);
            float m0 = x0 + capWL - 0.2f, m1 = x1 - capWR + 0.2f, mid = m1 - m0;
            if (mid > 0.5f)
            {
                int n = Mathf.Max(1, Mathf.RoundToInt(mid / (st.bounds.size.x * k)));
                float w = mid / n;
                for (int i = 0; i < n; i++)
                    Piece(st, "Strip" + i, m0 + w * (i + 0.5f), y + topPad - H / 2f, (w + 0.34f) / st.bounds.size.x, k, 2, i % 2 == 1);
            }
            // deep rock fill below the strip (exact fit left to right so nothing overhangs a pit)
            float fillTop = y + topPad - H + 0.65f;
            const float tile = 1.7f;
            float fx0 = x0 + 0.18f, fw = (x1 - x0) - 0.36f;
            int cols = Mathf.Max(1, Mathf.CeilToInt(fw / tile));
            float tw = fw / cols;
            for (int row = 0; fillTop - row * tile > -3.6f; row++)
                for (int c = 0; c < cols; c++)
                {
                    var g = Piece(df, "Deep", fx0 + tw * (c + 0.5f), fillTop - tile * (row + 0.5f), tw / df.bounds.size.x, tile / df.bounds.size.y, 0, (c + row) % 2 == 1, row % 2 == 1);
                    float shade = Mathf.Lerp(0.82f, 0.42f, Mathf.Clamp01(row / 4f));
                    g.GetComponent<SpriteRenderer>().color = new Color(shade, shade * 0.97f, shade * 0.95f);
                }
            var fill = new GameObject("GroundFill"); fill.transform.SetParent(envRoot, false);
            float top = Mathf.Min(fillTop - 3.4f, -3.5f), bottom = -14f;
            fill.transform.position = new Vector3((x0 + x1) / 2f, (top + bottom) / 2f, 0);
            fill.transform.localScale = new Vector3((x1 - x0) / 0.08f, (top - bottom) / 0.08f, 1);
            var fsr = fill.AddComponent<SpriteRenderer>(); fsr.sprite = S("UI/white"); fsr.color = new Color(0.16f, 0.1f, 0.08f); fsr.sortingOrder = -1;
        }

        /// <summary>Floating one-way platform. topY is where the player stands. The size picks the sprite.</summary>
        static GameObject Float(float cx, float topY, float width, string unused)
        {
            string name = width >= 3.0f ? "Ground/float_large" : (width >= 2.35f ? "Ground/float_medium" : "Ground/float_small");
            var s = S(name);
            float h = width / s.bounds.size.x * s.bounds.size.y;
            float d = 0.27f * h;                           // standing line is this far below the sprite top
            var go = SprW(name, cx, topY + d - h / 2f, width, 1, envRoot);
            go.name = "Float_" + cx; go.layer = LayerMask.NameToLayer("Ground");
            float sc = go.transform.localScale.x;
            var bc = go.AddComponent<BoxCollider2D>();
            float lh = 0.3f / sc;
            bc.size = new Vector2(s.bounds.size.x * 0.82f, lh);
            bc.offset = new Vector2(0, s.bounds.size.y / 2f - d / sc - lh / 2f);
            bc.sharedMaterial = noFriction; bc.usedByEffector = true;
            var eff = go.AddComponent<PlatformEffector2D>(); eff.useOneWay = true; eff.surfaceArc = 150f; eff.useSideFriction = false;
            return go;
        }

        /// <summary>Solid stepping stone / rock pillar rising out of the water.</summary>
        static void Stone(float cx, float topY, float width, string unused)
        {
            var s = S("Ground/stone");
            float h = width / s.bounds.size.x * s.bounds.size.y;
            float d = 0.26f * h;
            var go = SprW("Ground/stone", cx, topY + d - h / 2f, width, 2, envRoot);
            go.name = "Stone_" + cx; go.layer = LayerMask.NameToLayer("Ground");
            float sc = go.transform.localScale.x;
            float colTop = s.bounds.size.y / 2f - d / sc, colBottom = -s.bounds.size.y / 2f + 0.05f / sc;
            var bc = go.AddComponent<BoxCollider2D>();
            bc.size = new Vector2(s.bounds.size.x * 0.7f, colTop - colBottom);
            bc.offset = new Vector2(0, (colTop + colBottom) / 2f);
            bc.sharedMaterial = noFriction;
        }

        static void Scatter(float x0, float x1, float y, int seed)
        {
            var r = new System.Random(seed);
            // a few low bushes behind the player, well inside the segment (nothing near a drop)
            for (float x = x0 + 2.5f; x < x1 - 2.5f; x += 6.5f + (float)r.NextDouble() * 6f)
                Spr("Props/ruins/ruin_12", x, y - 0.02f, 0.9f + (float)r.NextDouble() * 0.4f, 2, propRoot, r.NextDouble() < 0.5);
        }

        static void ParallaxPropAt(string path, float worldX, float bottomRel, float width, float factor, int order, float alpha, Transform parent)
        {
            var s = S(path);
            float h = width * s.bounds.size.y / s.bounds.size.x;
            var go = new GameObject(System.IO.Path.GetFileName(path));
            go.transform.localScale = Vector3.one * (width / s.bounds.size.x);
            var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = s; sr.sortingOrder = order; sr.color = new Color(1f, 1f, 1f, alpha);
            var pp = go.AddComponent<ParallaxProp>(); pp.baseX = worldX; pp.factor = factor; pp.yOffset = bottomRel + h / 2f;
            go.transform.SetParent(parent);
        }

        static void ParallaxProp(string path, float worldX, float yRel, float width, float factor, int order, Transform parent)
        {
            var s = S(path);
            var go = new GameObject(System.IO.Path.GetFileName(path));
            go.transform.localScale = Vector3.one * (width / s.bounds.size.x);
            var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = s; sr.sortingOrder = order;
            var pp = go.AddComponent<ParallaxProp>(); pp.baseX = worldX; pp.factor = factor; pp.yOffset = yRel;
            go.transform.SetParent(parent);
        }

        static void Parallax(string path, float factor, float yOff, float width, int order, bool followY = true, float alpha = 1f)
        {
            var s = S(path);
            var go = new GameObject(System.IO.Path.GetFileName(path));
            go.transform.localScale = Vector3.one * (width / s.bounds.size.x);
            var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = s; sr.sortingOrder = order; sr.color = new Color(1f, 1f, 1f, alpha);
            var pl = go.AddComponent<ParallaxLayer>(); pl.factor = factor; pl.yOffset = yOff; pl.width = width; pl.followCameraY = followY;
        }

        static void MangoLine(float x0, float x1, float y, int n)
        {
            for (int i = 0; i < n; i++) Place("Mango", new Vector3(Mathf.Lerp(x0, x1, n == 1 ? 0.5f : i / (float)(n - 1)), y, 0), pickupRoot);
        }

        static void MangoArc(float xc, float y0, float height, float span, int n)
        {
            for (int i = 0; i < n; i++)
            {
                float t = n == 1 ? 0.5f : i / (float)(n - 1);
                Place("Mango", new Vector3(xc - span / 2f + t * span, y0 + Mathf.Sin(t * Mathf.PI) * height, 0), pickupRoot);
            }
        }

        static GameObject Animal(string path, float x, float y, float h, bool flip, bool hops = true, bool flies = false)
        {
            var root = new GameObject("Animal_" + System.IO.Path.GetFileName(path));
            root.transform.SetParent(animalRoot, false);
            root.transform.position = new Vector3(x, y, 0);
            var v = Spr(path, 0, 0, h, 5, root.transform, flip); v.name = "Visual"; v.transform.localPosition = Vector3.zero;
            // animate the visual itself so the root keeps its world position
            var am = v.AddComponent<AmbientAnimal>(); am.hops = hops; am.flies = flies; am.hopHeight = Mathf.Min(0.5f, h * 0.35f);
            return root;
        }

        // ============================================================ scene
        [MenuItem("Bal Hanuman/Build Level 1 (HQ)")]
        public static void BuildAll()
        {
            EnsureLayers();
            EnsureFolders();
            AssetDatabase.Refresh();

            noFriction = AssetDatabase.LoadAssetAtPath<PhysicsMaterial2D>("Assets/Prefabs/NoFriction.physicsMaterial2D");
            if (noFriction == null)
            {
                noFriction = new PhysicsMaterial2D("NoFriction") { friction = 0f, bounciness = 0f };
                AssetDatabase.CreateAsset(noFriction, "Assets/Prefabs/NoFriction.physicsMaterial2D");
            }

            prefabs.Clear(); groundSegs.Clear();
            PlayerAnimationBuilder.Build();
            AssetDatabase.Refresh();
            BuildPrefabs();
            AssetDatabase.ImportAsset("Assets/Prefabs/Player/BalHanuman.prefab", ImportAssetOptions.ForceUpdate);
            BuildLevelScene();
            BuildMenuScene();

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(MenuScenePath, true),
                new EditorBuildSettingsScene(ScenePath, true),
            };
            ApplyProjectSettings();
            EditorSceneManager.OpenScene(ScenePath);
            Debug.Log("Bal Hanuman Level 1 built.");
        }

        public static void ApplyProjectSettings()
        {
            PlayerSettings.productName = "Bal Hanuman";
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.codecraft.balhanuman");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            AssetDatabase.SaveAssets();
        }

        static void BuildLevelScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            envRoot = new GameObject("Environment").transform;
            propRoot = new GameObject("Props").transform;
            animalRoot = new GameObject("Animals").transform;
            pickupRoot = new GameObject("Mangoes & Bonus").transform;
            interactRoot = new GameObject("Interactables").transform;

            // ---- camera + light
            var camGo = new GameObject("Main Camera"); camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true; cam.orthographicSize = 4.6f; cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.47f, 0.74f, 0.95f);
            camGo.transform.position = new Vector3(8, 2.2f, -10);
            camGo.AddComponent<AudioListener>();
            var lg = new GameObject("Global Light 2D").AddComponent<Light2D>();
            lg.lightType = Light2D.LightType.Global; lg.intensity = 1f;

            // ---- parallax (factors from the brief; mirrored tiling because the art is not seamless)
            var par = new GameObject("Parallax").transform;
            // Watercolour backdrop. Layer widths are chosen so every layer is shown at (or close to) native resolution.
            // yOffset is the layer centre relative to the camera; at the lowest camera position the ground line sits at -2.0.
            Parallax("Backdrop/bg_sky", 0.02f, 0f, 24f, -100);                 // sky: covers the whole view
            Parallax("Backdrop/bg_far", 0.05f, -0.17f, 30f, -95);              // mountains and boulder hills (fades into the sky above)
            ParallaxProp("Backdrop/bg_hill", 68f, -1.0f, 7f, 0.08f, -93, par);       // Anjanadri hill: drifts past in the first half
            ParallaxProp("Backdrop/bg_gopuram", 197f, -0.81f, 7f, 0.08f, -93, par);  // temple tower and waterfall: appears in the second half
            Parallax("Backdrop/bg_mid", 0.15f, -1.08f, 22f, -90);              // misty forest
            // stone ruins only where they suit the story: the ancient bridge and the forgotten temple
            ParallaxPropAt("Backdrop/bg_ruin_0", 104f, -2.35f, 4.6f, 0.4f, -86, 0.65f, par);   // pavilion near the bridge
            foreach (var p in Object.FindObjectsByType<ParallaxLayer>(FindObjectsSortMode.None)) p.transform.SetParent(par);

            // ---- terrain (x ranges from the brief; unit = 1m)
            Ground(-16f, 23f, 0f);          // 1 The Request
            Ground(26.5f, 36f, 0f);        // 2 Forest Path
            Ground(39.5f, 47f, 0f);
            Ground(70f, 89f, 0f);          // 4 Hanging Vines
            Ground(89f, 97f, 5.5f);        //   cliff with the baby monkey
            Ground(97f, 105f, 0f);         // 5 Moving Bridge
            Ground(117f, 164f, 0f);        // 6 Forgotten Temple
            Float(24.75f, 1.1f, 2.4f, "Platforms/tile_06");
            Float(31.2f, 2.1f, 3.4f, "Platforms/tile_05");
            Float(34.6f, 3.4f, 2.4f, "Platforms/tile_02");
            Float(37.75f, 1.1f, 2.4f, "Platforms/tile_06");
            Float(75.2f, 2.3f, 3.0f, "Platforms/tile_05");
            Float(79.2f, 3.7f, 2.6f, "Platforms/tile_02");
            Wall(-6.5f); Wall(152.5f);
            foreach (var g in groundSegs) Scatter(g.x, g.y, g.z, (int)g.x * 13 + 5);

            // ---- the stream (47 to 70)
            BuildStream();

            // ---- bridge (105 to 117)
            BuildBridge(out var bridge);

            // ---- vines (climb to the cliff top at x ~ 89)
            var vine = Place("ClimbableVine", new Vector3(87.4f, 0f, 0), interactRoot);
            vine.GetComponent<ClimbableVine>().exitDirection = 1f;
            // the hanging pieces hang from one pivot at the top so the whole vine can sway and swing when grabbed
            var sway = new GameObject("VineSway"); sway.transform.SetParent(propRoot, false); sway.transform.position = new Vector3(87.4f, 6.0f, 0);
            vine.GetComponent<ClimbableVine>().swayRoot = sway.transform;
            for (int i = 0; i < 3; i++) { var seg = Spr("Platforms/tile_17", 87.4f, 0.2f + i * 1.95f, 2.0f, 3, propRoot); seg.name = "VineSeg"; seg.transform.SetParent(sway.transform, true); }
            var vtop = Spr("Platforms/tile_17", 87.4f, 5.9f, 1.6f, 3, propRoot); vtop.name = "VineTop"; vtop.transform.SetParent(sway.transform, true);
            SprW("Platforms/tile_18", 87.4f, 5.4f, 3.6f, 4, propRoot).name = "VineCanopy";

            // ---- decor: start area
            Spr("Props/generated/signpost_placeholder", 14.0f, 0f, 1.7f, 3, propRoot).name = "Signpost";
            Spr("Props/pillars/pillar_01", 20.8f, 0f, 2.2f, -5, propRoot);
            // ---- decor: forest
            Spr("Props/pillars/pillar_03", 71f, 0f, 2.4f, -5, propRoot);
            Spr("Props/ruins/ruin_01", 99.5f, 0f, 3.4f, -5, propRoot);
            // ---- decor: temple
            BuildTemple();

            // ---- NPC + level interactables
            var sage = Place("Sage", new Vector3(9.0f, 0f, 0), interactRoot);
            var monkey = Place("BabyMonkey", new Vector3(93.5f, 5.5f, 0), interactRoot);
            var landing = new GameObject("MonkeyLanding").transform; landing.SetParent(interactRoot); landing.position = new Vector3(99.5f, 0f, 0);
            monkey.GetComponent<BabyMonkey>().landingPoint = landing;
            var lever = Place("Lever", new Vector3(102.5f, 0f, 0), interactRoot);
            lever.GetComponent<Lever>().bridge = bridge;

            var portal = Place("ReturnPortal", new Vector3(149.6f, 0f, 0), interactRoot);
            var basket = Place("MangoBasket", new Vector3(144.0f, 1.1f, 0), interactRoot);
            basket.GetComponent<MangoBasket>().portal = portal.GetComponent<ReturnPortal>();

            // ---- checkpoints
            Place("Checkpoint", new Vector3(3.4f, 0f, 0), interactRoot).GetComponent<Checkpoint>().silent = true;
            Place("Checkpoint", new Vector3(44.5f, 0f, 0), interactRoot);
            Place("Checkpoint", new Vector3(99.0f, 0f, 0), interactRoot);

            // ---- respawn zones: water surface + a safety net under every pit
            var wz = new GameObject("StreamWater_Respawn"); wz.transform.SetParent(interactRoot);
            wz.transform.position = new Vector3(58.5f, -1.6f, 0);
            var wbc = wz.AddComponent<BoxCollider2D>(); wbc.isTrigger = true; wbc.size = new Vector2(23f, 2.2f);
            var wr = wz.AddComponent<RespawnZone>(); wr.isWater = true; wr.splashSprite = S("VFX/fx_04");
            var safety = new GameObject("PitSafetyNet_Respawn"); safety.transform.SetParent(interactRoot);
            safety.transform.position = new Vector3(73f, -5.5f, 0);
            var sbc = safety.AddComponent<BoxCollider2D>(); sbc.isTrigger = true; sbc.size = new Vector2(180f, 2f);
            safety.AddComponent<RespawnZone>();

            // ---- collectibles
            PlaceCollectibles();

            // ---- animals
            Animal("Animals/animal_01", 16.0f, 0f, 2.0f, true, false);
            Animal("Animals/animal_07", 133.5f, 0f, 1.9f, false, false);

            // ---- ambient fx
            var amb = new GameObject("AmbientFx").AddComponent<AmbientFx>();
            amb.leaves = new Sprite[0];
            amb.sparkle = Sparkle;

            // ---- player + camera + session + UI
            var playerGo = (GameObject)PrefabUtility.InstantiatePrefab(prefabs["BalHanuman"]);
            playerGo.transform.position = new Vector3(4.6f, 0.05f, 0);
            var pc = playerGo.GetComponent<PlayerController2D>();
            var carried = playerGo.transform.Find("CarriedBasket").gameObject;

            var follow = camGo.AddComponent<SideScrollCamera>();
            follow.target = playerGo.transform; follow.minBounds = new Vector2(2.2f, 2.0f); follow.maxBounds = new Vector2(144f, 9.5f);
            follow.fitToWorld = true; follow.worldLeft = -6.5f; follow.worldRight = 152.5f;

            var ui = new GameObject("UI");
            var hud = ui.AddComponent<HudController>();
            var skin = BuildSkin();
            hud.skin = skin;
            var ov = ui.AddComponent<OverlayUI>();
            ov.skin = skin;
            ov.portraits = new[]
            {
                new Portrait { speaker = "Sage", sprite = S("NPCs/elder_01") },
                new Portrait { speaker = "Hanuman", sprite = S("Character/Expressions/face_happy") },
                new Portrait { speaker = "Baby Monkey", sprite = S("Animals/animal_04") },
            };

            var gsGo = new GameObject("GameSession");
            var gs = gsGo.AddComponent<GameSession>();
            var quest = gsGo.AddComponent<QuestManager>();
            gs.player = pc; gs.interactor = playerGo.GetComponent<PlayerInteractor>(); gs.quest = quest;
            gs.cameraRig = follow; gs.hud = hud; gs.overlay = ov; gs.carriedBasket = carried;
            gs.sageReturnPosition = new Vector2(10.6f, 0.1f);
            gs.sparkleSprite = Sparkle; gs.goldSprite = S("Items/item_03");

            EditorSceneManager.SaveScene(scene, ScenePath);
        }

        static UiSkin BuildSkin()
        {
            const string path = "Assets/Prefabs/UI/UiSkin.asset";
            var skin = AssetDatabase.LoadAssetAtPath<UiSkin>(path);
            if (skin == null) { skin = ScriptableObject.CreateInstance<UiSkin>(); AssetDatabase.CreateAsset(skin, path); }
            skin.titleFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Cinzel-Variable.ttf");
            skin.bodyFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/NunitoSans-Variable.ttf");
            skin.panelLarge = S("UI2/panel_large"); skin.hudWide = S("UI2/hud_wide"); skin.hudMid = S("UI2/hud_mid");
            skin.hudPill = S("UI2/hud_pill"); skin.hudPillPrimary = S("UI2/hud_pill_primary"); skin.divider = S("UI2/divider");
            skin.btnNormal = S("UI2/btn_normal"); skin.btnPressed = S("UI2/btn_pressed"); skin.btnPrimary = S("UI2/btn_primary"); skin.btnDisabled = S("UI2/btn_disabled");
            skin.portraitRing = S("UI2/portrait_ring");
            skin.barFrame = S("UI2/bar_frame"); skin.fillCoral = S("UI2/fill_coral"); skin.fillGold = S("UI2/fill_gold"); skin.fillTeal = S("UI2/fill_teal");
            skin.btnPause = S("UI2/btn2_pause"); skin.btnRoll = S("UI2/btn2_roll"); skin.btnJump = S("UI2/btn2_jump"); skin.btnBend = S("UI2/btn2_bend");
            skin.btnInteract = S("UI2/btn2_interact"); skin.btnClimb = S("UI2/btn2_climb"); skin.btnLeft = S("UI2/btn2_left"); skin.btnRight = S("UI2/btn2_right");
            skin.iconPause = S("UI2/icon2_pause"); skin.iconRoll = S("UI2/icon2_roll"); skin.iconJump = S("UI2/icon_jump"); skin.iconBend = S("UI2/icon_bend");
            skin.iconInteract = S("UI2/icon2_interact"); skin.iconClimb = S("UI2/icon2_climb"); skin.iconMango = S("UI2/icon2_mango"); skin.iconHeart = S("UI2/icon2_heart");
            skin.iconEnergy = S("UI2/icon2_energy"); skin.iconFlag = S("UI2/icon2_flag"); skin.iconOm = S("UI2/icon2_om"); skin.iconLotus = S("UI2/icon2_lotus");
            skin.iconCheck = S("UI2/icon2_check"); skin.iconStar = S("UI2/icon2_star"); skin.iconPin = S("UI2/icon2_pin"); skin.iconTalk = S("UI2/icon2_talk");
            skin.failFrames = F("Character/Death", "death", 1, 5);
            skin.circle = S("UI/circle"); skin.white = S("UI/white"); skin.heroPortrait = S("Character/Expressions/face_happy");
            EditorUtility.SetDirty(skin);
            AssetDatabase.SaveAssets();
            return skin;
        }

        static void Wall(float x)
        {
            var w = new GameObject("Wall_" + x); w.transform.SetParent(envRoot, false);
            w.layer = LayerMask.NameToLayer("Ground");
            w.transform.position = new Vector3(x, 6, 0);
            var bc = w.AddComponent<BoxCollider2D>(); bc.size = new Vector2(1, 60); bc.sharedMaterial = noFriction;
        }

        static void BuildStream()
        {
            // stream bed + water body
            var bed = new GameObject("StreamBed"); bed.transform.SetParent(envRoot, false);
            bed.transform.position = new Vector3(58.5f, -8f, 0);
            bed.transform.localScale = new Vector3(23f / 0.08f, 14f / 0.08f, 1);
            var bsr = bed.AddComponent<SpriteRenderer>(); bsr.sprite = S("UI/white"); bsr.color = new Color(0.08f, 0.22f, 0.36f); bsr.sortingOrder = -2;

            var wGo = new GameObject("Water"); wGo.transform.SetParent(envRoot, false);
            var ws = S("Water/body");
            wGo.transform.position = new Vector3(58.5f, -0.55f - 1.75f, 0);
            wGo.transform.localScale = new Vector3(23.6f / ws.bounds.size.x, 3.5f / ws.bounds.size.y, 1);
            var wsr = wGo.AddComponent<SpriteRenderer>(); wsr.sprite = ws; wsr.sortingOrder = 6;
            wGo.AddComponent<WaterDrift>().driftX = 0.12f;
            // foam-crested surface strip along the top of the water
            var sfs = S("Water/surface");
            var sfGo = new GameObject("WaterSurfaceStrip"); sfGo.transform.SetParent(envRoot, false);
            sfGo.transform.position = new Vector3(58.5f, -0.42f - 0.4f, 0);
            sfGo.transform.localScale = new Vector3(23.6f / sfs.bounds.size.x, 0.8f / sfs.bounds.size.y, 1);
            var sfr = sfGo.AddComponent<SpriteRenderer>(); sfr.sprite = sfs; sfr.sortingOrder = 7;
            var sfd = sfGo.AddComponent<WaterDrift>(); sfd.driftX = 0.2f; sfd.driftSpeed = 0.5f; sfd.swellY = 0.03f;
            var surf = new GameObject("WaterSurfaceFx"); surf.transform.SetParent(envRoot, false);
            surf.transform.position = new Vector3(58.5f, -0.55f, 0);
            var wf = surf.AddComponent<WaterSurface>(); wf.ripple = S("Water/ripple_1"); wf.width = 22f;
            var flow = new GameObject("WaterFlow"); flow.transform.SetParent(envRoot, false);
            flow.transform.position = new Vector3(58.5f, -0.6f, 0);
            var wfl = flow.AddComponent<WaterFlow>(); wfl.streak = S("UI/white"); wfl.width = 22.5f; wfl.height = 2.6f;
            var deep = new GameObject("WaterDeep"); deep.transform.SetParent(envRoot, false);
            deep.transform.position = new Vector3(58.5f, -2.9f, 0);
            deep.transform.localScale = new Vector3(23f / 0.08f, 2.4f / 0.08f, 1);
            var dsr = deep.AddComponent<SpriteRenderer>(); dsr.sprite = S("UI/white"); dsr.color = new Color(0.03f, 0.12f, 0.26f, 0.0f); dsr.sortingOrder = 5;   // the new water art has its own depth gradient; kept (invisible) so old references stay valid

            // waterfall decor
            SprW("Water/waterfall", 64.0f, -0.75f, 3.1f, -10, propRoot).name = "Waterfall";
            Spr("VFX/fx_17", 64.0f, -0.62f, 0.55f, 9, propRoot).name = "WaterfallSplash";

            // mossy boulders half sunk in the stream (decoration only, nothing to stand on)
            SprW("Water/stone_1", 47.7f, -0.52f, 1.5f, 9, propRoot).name = "BankBoulder";
            SprW("Water/stone_2", 69.7f, -0.52f, 1.3f, 9, propRoot).name = "BankBoulder";
            SprW("Water/stone_0", 61.2f, -0.55f, 1.1f, 3, propRoot).name = "StreamBoulder";

            // stepping stones + raised rocks
            Stone(49.6f, 0.7f, 1.5f, "Platforms/tile_14");
            Stone(52.6f, 1.3f, 2.2f, "Platforms/tile_12");
            Stone(59.6f, 1.3f, 2.2f, "Platforms/tile_12");
            Stone(62.6f, 2.0f, 2.2f, "Platforms/tile_14");
            Stone(68.8f, 1.0f, 1.8f, "Platforms/tile_06");

            // floating logs
            // the logs sit a little lower than before so the ring of water at their base lies on the surface
            var l1 = Place("MovingLog", new Vector3(55.2f, -0.1f, 0), envRoot).GetComponent<MovingPlatform>();
            l1.travel = new Vector2(1.7f, 0f); l1.speed = 1.0f;
            SetLogArt(l1.gameObject, "Water/log_1");
            var l2 = Place("MovingLog", new Vector3(65.4f, -0.1f, 0), envRoot).GetComponent<MovingPlatform>();
            l2.travel = new Vector2(1.7f, 0f); l2.speed = 1.0f; l2.phaseOffset = 1.4f;
            SetLogArt(l2.gameObject, "Water/log_2");
        }

        static void SetLogArt(GameObject log, string path)
        {
            var v = log.transform.Find("Visual");
            var s = S(path);
            v.GetComponent<SpriteRenderer>().sprite = s;
            float k = 3.1f / s.bounds.size.x;
            v.localScale = Vector3.one * k;
            v.localPosition = new Vector3(0f, -0.42f, 0f);
        }

        static void BuildBridge(out BridgeController bridge)
        {
            var root = new GameObject("MovingBridge"); root.transform.SetParent(envRoot, false);
            root.transform.position = new Vector3(105f, 0, 0);
            var pivot = new GameObject("Pivot"); pivot.transform.SetParent(root.transform, false);
            for (int i = 0; i < 3; i++)
            {
                var seg = SprW("Platforms/Sliced/tile_15_bridge", 105f + 2f + i * 4f, 0.2f, 4.05f, 5, pivot.transform);
                seg.name = "BridgeSegment" + i;
            }
            var deck = new GameObject("Deck"); deck.transform.SetParent(pivot.transform, false);
            deck.layer = LayerMask.NameToLayer("Ground");
            deck.transform.position = new Vector3(111f, -0.15f, 0);
            var bc = deck.AddComponent<BoxCollider2D>(); bc.size = new Vector2(12f, 0.3f); bc.sharedMaterial = noFriction;
            bridge = root.AddComponent<BridgeController>();
            bridge.pivot = pivot.transform; bridge.deck = bc; bridge.dust = S("VFX/fx_13"); bridge.raisedAngle = 48f;
            var anchor = new GameObject("RopeAnchor"); anchor.transform.SetParent(root.transform, false); anchor.transform.position = new Vector3(117.2f, 2.5f, 0);
            bridge.ropeAnchor = anchor.transform;
            // supports + rope anchors
            Spr("Props/ruins/ruin_05", 104.2f, 0f, 3.0f, -4, propRoot);
            Spr("Props/ruins/ruin_10", 117.8f, 0f, 3.0f, -4, propRoot);
        }

        static void BuildTemple()
        {
            // ---- the inner hall: a wide painted backdrop behind the altar (its floor hides under the ground, so the ground line is at y = 0)
            float hallH = 558f * 0.01925f;                                       // 0.01925 world units per pixel
            Spr("Temple/int_hall", 144.0f, -1.5f + hallH / 2f, hallH, -8, propRoot).name = "TempleHall";

            // ---- the temple front, where the bridge ends
            Spr("Temple/ext_temple", 124.5f, -0.1f, 6.3f, -6, propRoot).name = "TempleFront";
            Spr("Temple/ext_pillar_a", 120.3f, -0.05f, 3.3f, -5, propRoot).name = "PillarLeft";
            Spr("Temple/ext_pillar_b", 128.7f, -0.05f, 3.3f, -5, propRoot).name = "PillarRight";
            Spr("Temple/ext_lamp", 122.4f, 0f, 1.45f, -4, propRoot).name = "LampLeft";
            Spr("Temple/ext_lamp", 126.7f, 0f, 1.45f, -4, propRoot).name = "LampRight";
            SprW("Temple/ext_garland_b", 124.5f, 3.0f, 2.5f, -4, propRoot).name = "Garland";    // centre pivot: hangs over the doorway
            SprW("Temple/ext_garland_a", 120.3f, 3.0f, 1.9f, -4, propRoot).name = "GarlandLeft";
            SprW("Temple/ext_garland_c", 128.7f, 3.0f, 1.9f, -4, propRoot).name = "GarlandRight";
            Spr("Temple/ext_bellpost", 131.4f, 0f, 3.2f, -5, propRoot).name = "BellPost";

            // fallen stones scattered on the hall floor
            Spr("Temple/ext_block_a", 134.2f, 0f, 0.95f, -3, propRoot).name = "Block";
            Spr("Temple/ext_block_b", 139.3f, 0f, 0.7f, -3, propRoot).name = "Block";
            Spr("Temple/ext_fallen", 151.0f, 0f, 0.8f, -3, propRoot).name = "FallenPillar";

            // ---- altar (the basket rests on top at y = 1.1)
            Spr("Temple/int_altar", 144.0f, -0.05f, 1.15f, 2, propRoot).name = "Altar";
            // golden light
            var glow = Spr("Temple/int_glow", 144.0f, 1.45f, 3.4f, 1, propRoot); glow.name = "AltarGlow";
            glow.GetComponent<SpriteRenderer>().color = new Color(1f, 1f, 1f, 0.7f);
            var fx = Spr("VFX/fx_07", 144.0f, 1.0f, 3.6f, -7, propRoot); fx.name = "GoldenLight";
            fx.GetComponent<SpriteRenderer>().color = new Color(1f, 0.9f, 0.5f, 0.28f);
        }

        static void PlaceCollectibles()
        {
            MangoLine(12f, 21f, 1.3f, 5);
            MangoArc(24.75f, 1.6f, 1.6f, 4.8f, 5);
            MangoLine(27.5f, 35f, 1.3f, 5);
            MangoLine(29.6f, 32.8f, 3.4f, 3);
            MangoLine(34.0f, 35.2f, 4.7f, 2);
            MangoArc(37.75f, 1.8f, 1.5f, 4.5f, 5);
            MangoLine(40.5f, 46.5f, 1.3f, 4);

            Place("Mango", new Vector3(49.6f, 1.9f, 0), pickupRoot);
            Place("Mango", new Vector3(52.6f, 2.5f, 0), pickupRoot);
            Place("Mango", new Vector3(55.8f, 1.5f, 0), pickupRoot);
            Place("Mango", new Vector3(59.6f, 2.5f, 0), pickupRoot);
            Place("Mango", new Vector3(62.6f, 3.2f, 0), pickupRoot);
            Place("Mango", new Vector3(66.2f, 1.5f, 0), pickupRoot);
            Place("Mango", new Vector3(68.8f, 2.2f, 0), pickupRoot);

            MangoLine(71f, 79f, 1.3f, 5);
            MangoLine(74f, 76.4f, 3.5f, 3);
            MangoLine(78.4f, 80f, 4.9f, 2);
            for (int i = 0; i < 5; i++) Place("Mango", new Vector3(87.4f + (i % 2 == 0 ? -0.5f : 0.5f), 1.2f + i * 1.1f, 0), pickupRoot);
            MangoLine(90f, 96f, 6.8f, 4);
            MangoLine(98f, 104f, 1.3f, 4);
            MangoLine(106f, 116f, 1.3f, 6);
            MangoLine(118.5f, 128f, 1.3f, 5);

            // optional bonus: golden coins and hidden lotus flowers
            foreach (var p in new[] { new Vector3(31.2f, 5.2f, 0), new Vector3(34.6f, 6.0f, 0), new Vector3(75.2f, 4.9f, 0), new Vector3(92.5f, 8.5f, 0), new Vector3(135f, 1.3f, 0) })
                Place("GoldCoin", p, pickupRoot);
            Place("Lotus", new Vector3(47.9f, -0.05f, 0), pickupRoot).transform.position = new Vector3(48.0f, 0.9f, 0);
            Place("Lotus", new Vector3(125.0f, 0.9f, 0), pickupRoot);
            Place("Lotus", new Vector3(81.0f, 0.9f, 0), pickupRoot);
        }

        // ============================================================ menu scene
        static void BuildMenuScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camGo = new GameObject("Main Camera"); camGo.tag = "MainCamera";
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true; cam.orthographicSize = 4.6f; cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.47f, 0.74f, 0.95f);
            camGo.transform.position = new Vector3(0, 0, -10);
            camGo.AddComponent<AudioListener>();
            var lg = new GameObject("Global Light 2D").AddComponent<Light2D>(); lg.lightType = Light2D.LightType.Global;

            // static parallax composition behind the title
            SprW("Backdrop/bg_sky", 0, 0, 24f, -100, null);
            SprW("Backdrop/bg_far", 0, -0.17f, 30f, -95, null);
            SprW("Backdrop/bg_mid", 0, -1.08f, 22f, -90, null);
            var mm = new GameObject("MainMenu").AddComponent<MainMenuController>();
            mm.skin = BuildSkin();
            EditorSceneManager.SaveScene(scene, MenuScenePath);
        }
    }
}
