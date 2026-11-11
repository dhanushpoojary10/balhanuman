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
            var idle0 = S("Character/Idle/idle_01");
            vis.transform.localScale = Vector3.one * (1.9f / idle0.bounds.size.y);
            var psr = vis.AddComponent<SpriteRenderer>(); psr.sprite = idle0; psr.sortingOrder = 10;
            var anim = vis.AddComponent<Animator>();
            anim.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/Animations/BalHanuman/BalHanuman.controller");
            var gc = new GameObject("GroundCheck"); gc.transform.SetParent(player.transform, false); gc.transform.localPosition = new Vector3(0, -0.04f, 0);
            var pc = player.AddComponent<PlayerController2D>();
            pc.groundCheck = gc.transform; pc.groundMask = LayerMask.GetMask("Ground"); pc.animator = anim; pc.spriteRenderer = psr; pc.dustSprite = S("VFX/fx_13");
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
            var lgs = Spr("Props/generated/log_placeholder", 0, -0.55f, 0.85f, 4, lg.transform); lgs.name = "Visual";
            var lbc = lg.AddComponent<BoxCollider2D>(); lbc.size = new Vector2(2.5f, 0.35f); lbc.offset = new Vector2(0, -0.12f); lbc.sharedMaterial = noFriction;
            var lrb = lg.AddComponent<Rigidbody2D>(); lrb.bodyType = RigidbodyType2D.Kinematic;
            lg.AddComponent<MovingPlatform>();
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
            float fillTop = y + topPad - H + 0.12f;
            const float tile = 1.7f;
            int cols = Mathf.Max(1, Mathf.CeilToInt((x1 - x0) / tile));
            float tw = (x1 - x0) / cols;
            for (int row = 0; fillTop - row * tile > -3.6f; row++)
                for (int c = 0; c < cols; c++)
                {
                    var g = Piece(df, "Deep", x0 + tw * (c + 0.5f), fillTop - tile * (row + 0.5f), tw / df.bounds.size.x, tile / df.bounds.size.y, 0, (c + row) % 2 == 1, row % 2 == 1);
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
            // back bushes
            for (float x = x0 + 1.2f; x < x1 - 0.8f; x += 2.8f + (float)r.NextDouble() * 3.5f)
                Spr("Props/ruins/ruin_12", x, y - 0.02f, 1.0f + (float)r.NextDouble() * 0.6f, 2, propRoot, r.NextDouble() < 0.5);
            // foreground plant clusters: strictly inside the ground segment so nothing hangs over a pit
            for (float x = x0 + 2.0f; x < x1 - 2.0f; x += 4.5f + (float)r.NextDouble() * 5f)
                Spr("Props/generated/fgplant_" + r.Next(0, FgPlantCount).ToString("00"), x, y - 0.1f, 0.7f + (float)r.NextDouble() * 0.4f, 26, propRoot, r.NextDouble() < 0.5);
        }

        static void Parallax(string path, float factor, float yOff, float width, int order, bool followY = true)
        {
            var s = S(path);
            var go = new GameObject(System.IO.Path.GetFileName(path));
            go.transform.localScale = Vector3.one * (width / s.bounds.size.x);
            var sr = go.AddComponent<SpriteRenderer>(); sr.sprite = s; sr.sortingOrder = order;
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
            Parallax("Parallax/L1_sky_clouds", 0.02f, 2.2f, 40f, -100);
            Parallax("Parallax/L2_distant_mountains", 0.05f, 1.2f, 40f, -95);
            Parallax("Parallax/L3_temple_waterfall", 0.10f, -0.05f, 40f, -90);
            Parallax("Parallax/L4_mid_forest_mist", 0.15f, -1.2f, 40f, -85);
            // soft horizon haze for depth between the far and near layers
            var haze = new GameObject("HorizonHaze");
            haze.transform.localScale = new Vector3(60f / S("UI/haze").bounds.size.x, 3.2f / S("UI/haze").bounds.size.y, 1);
            var hz = haze.AddComponent<SpriteRenderer>(); hz.sprite = S("UI/haze"); hz.sortingOrder = -83;
            var hzl = haze.AddComponent<ParallaxLayer>(); hzl.factor = 0f; hzl.yOffset = -1.7f; hzl.width = 60f;
            // soft base under the forest layers: hides their flat lower edges at any camera height
            var horizon = new GameObject("HorizonFill");
            horizon.transform.localScale = new Vector3(60f / S("UI/fill_gradient").bounds.size.x, 9f / S("UI/fill_gradient").bounds.size.y, 1);
            var hsr = horizon.AddComponent<SpriteRenderer>(); hsr.sprite = S("UI/fill_gradient"); hsr.color = Color.white; hsr.sortingOrder = -88;
            var hpl = horizon.AddComponent<ParallaxLayer>(); hpl.factor = 0f; hpl.yOffset = -5.6f; hpl.width = 60f;
            foreach (var p in Object.FindObjectsByType<ParallaxLayer>(FindObjectsSortMode.None)) p.transform.SetParent(par);

            // ---- terrain (x ranges from the brief; unit = 1m)
            Ground(-6f, 23f, 0f);          // 1 The Request
            Ground(26.5f, 36f, 0f);        // 2 Forest Path
            Ground(39.5f, 47f, 0f);
            Ground(70f, 89f, 0f);          // 4 Hanging Vines
            Ground(89f, 97f, 5.5f);        //   cliff with the baby monkey
            Ground(97f, 105f, 0f);         // 5 Moving Bridge
            Ground(117f, 152f, 0f);        // 6 Forgotten Temple
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
            for (int i = 0; i < 3; i++) Spr("Platforms/tile_17", 87.4f, 0.2f + i * 1.95f, 2.0f, 3, propRoot).name = "VineSeg";
            Spr("Platforms/tile_17", 87.4f, 5.9f, 1.6f, 3, propRoot).name = "VineTop";
            SprW("Platforms/tile_18", 87.4f, 5.4f, 3.6f, 4, propRoot).name = "VineCanopy";
            var jt = Spr("Props/generated/banyan_tree2", 82.0f, -0.3f, 3.8f, -20, propRoot); jt.name = "BigJungleTree";
            jt.GetComponent<SpriteRenderer>().color = new Color(0.82f, 0.9f, 0.88f, 0.9f);

            // ---- decor: start area
            var banyan = Spr("Props/generated/banyan_tree", 1.2f, -0.3f, 4.0f, -20, propRoot); banyan.name = "Banyan";
            banyan.GetComponent<SpriteRenderer>().color = new Color(0.82f, 0.9f, 0.88f, 0.9f);
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
            Animal("Animals/animal_10", 18.2f, 0f, 0.95f, false);
            Animal("Animals/animal_13", 6.2f, 0f, 0.8f, false);
            Animal("Animals/animal_05", 33.2f, 3.4f, 0.95f, true);
            Animal("Animals/animal_09", 30f, 6.0f, 1.0f, false, false, true);
            Animal("Animals/animal_08", 62f, 4.6f, 0.9f, true, false, true);
            Animal("Animals/animal_09", 78f, 7.2f, 1.0f, true, false, true);
            Animal("Animals/animal_12", 73.2f, 0f, 1.2f, false);
            Animal("Animals/animal_05", 128.5f, 0f, 1.0f, false);
            Animal("Animals/animal_07", 133.5f, 0f, 1.9f, false, false);
            Animal("Animals/animal_14", 123f, 0f, 0.8f, true);

            // ---- ambient fx
            var amb = new GameObject("AmbientFx").AddComponent<AmbientFx>();
            amb.leaves = new[] { S("Props/misc/misc_01"), S("Props/misc/misc_02"), S("VFX/fx_20") };
            amb.sparkle = Sparkle;

            // ---- player + camera + session + UI
            var playerGo = (GameObject)PrefabUtility.InstantiatePrefab(prefabs["BalHanuman"]);
            playerGo.transform.position = new Vector3(4.6f, 0.05f, 0);
            var pc = playerGo.GetComponent<PlayerController2D>();
            var carried = playerGo.transform.Find("CarriedBasket").gameObject;

            var follow = camGo.AddComponent<SideScrollCamera>();
            follow.target = playerGo.transform; follow.minBounds = new Vector2(2.2f, 2.0f); follow.maxBounds = new Vector2(144f, 9.5f);

            var ui = new GameObject("UI");
            var hud = ui.AddComponent<HudController>();
            hud.sprites = UiSprites();
            hud.mangoIcon = S("Items/item_01"); hud.arrowSprite = S("UI/arrow"); hud.pauseIcon = S("UI/pause");
            hud.jumpIcon = S("UI/arrow"); hud.interactIcon = S("Items/item_27"); hud.rollIcon = S("UI/roll");
            var ov = ui.AddComponent<OverlayUI>();
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

        static UiKit.Sprites UiSprites()
        {
            return new UiKit.Sprites { circle = S("UI/circle"), ring = S("UI/ring"), panel = S("UI/panel"), white = S("UI/white") };
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
            var ws = S("UI/water");
            wGo.transform.position = new Vector3(58.5f, -0.55f - 1.75f, 0);
            wGo.transform.localScale = new Vector3(23f / ws.bounds.size.x, 3.5f / ws.bounds.size.y, 1);
            var wsr = wGo.AddComponent<SpriteRenderer>(); wsr.sprite = ws; wsr.sortingOrder = 8;
            var surf = new GameObject("WaterSurfaceFx"); surf.transform.SetParent(envRoot, false);
            surf.transform.position = new Vector3(58.5f, -0.5f, 0);
            var wf = surf.AddComponent<WaterSurface>(); wf.ripple = S("VFX/fx_12"); wf.width = 22f;
            var flow = new GameObject("WaterFlow"); flow.transform.SetParent(envRoot, false);
            flow.transform.position = new Vector3(58.5f, -0.6f, 0);
            var wfl = flow.AddComponent<WaterFlow>(); wfl.streak = S("UI/white"); wfl.width = 22.5f; wfl.height = 2.6f;
            var deep = new GameObject("WaterDeep"); deep.transform.SetParent(envRoot, false);
            deep.transform.position = new Vector3(58.5f, -2.9f, 0);
            deep.transform.localScale = new Vector3(23f / 0.08f, 2.4f / 0.08f, 1);
            var dsr = deep.AddComponent<SpriteRenderer>(); dsr.sprite = S("UI/white"); dsr.color = new Color(0.03f, 0.12f, 0.26f, 0.45f); dsr.sortingOrder = 8;

            // waterfall decor
            SprW("Platforms/tile_22", 64.0f, -0.6f, 3.2f, -10, propRoot).name = "SmallWaterfall";
            SprW("Platforms/tile_01", 64.0f, 3.1f, 3.6f, -11, propRoot).name = "WaterfallRock";
            Spr("VFX/fx_17", 64.0f, -0.62f, 0.55f, 9, propRoot).name = "WaterfallSplash";

            // stepping stones + raised rocks
            Stone(49.6f, 0.7f, 1.5f, "Platforms/tile_14");
            Stone(52.6f, 1.3f, 2.2f, "Platforms/tile_12");
            Stone(59.6f, 1.3f, 2.2f, "Platforms/tile_12");
            Stone(62.6f, 2.0f, 2.2f, "Platforms/tile_14");
            Stone(68.8f, 1.0f, 1.8f, "Platforms/tile_06");

            // floating logs
            var l1 = Place("MovingLog", new Vector3(55.2f, 0.1f, 0), envRoot).GetComponent<MovingPlatform>();
            l1.travel = new Vector2(1.7f, 0f); l1.speed = 1.0f;
            var l2 = Place("MovingLog", new Vector3(65.4f, 0.1f, 0), envRoot).GetComponent<MovingPlatform>();
            l2.travel = new Vector2(1.7f, 0f); l2.speed = 1.0f; l2.phaseOffset = 1.4f;
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
            Spr("Props/ruins/ruin_02", 126.0f, 0f, 5.6f, -6, propRoot).name = "TempleArch";
            Spr("Props/pillars/pillar_02", 122.0f, 0f, 3.2f, -5, propRoot);
            Spr("Props/pillars/pillar_04", 130.5f, 0f, 3.2f, -5, propRoot);
            Spr("Props/ruins/ruin_04", 137.0f, 0f, 5.2f, -6, propRoot).name = "StatueShrine";
            Spr("Props/ruins/ruin_07", 147.0f, 0f, 3.6f, -5, propRoot);
            Spr("Props/ruins/ruin_13", 150.8f, 0f, 3.3f, -4, propRoot).name = "GoldenBell";
            Spr("Items/item_29", 140.0f, 0f, 3.4f, -3, propRoot).name = "Flag";
            Spr("Items/item_29", 146.0f, 0f, 3.4f, -3, propRoot).name = "Flag";
            Spr("Items/item_29", 119.5f, 0f, 3.0f, -3, propRoot).name = "Flag";
            // altar
            SprW("Platforms/tile_10", 144.0f, -0.05f, 2.6f, 2, propRoot).name = "Altar";
            // golden light
            var glow = Spr("VFX/fx_09", 144.0f, 1.2f, 2.6f, 1, propRoot); glow.name = "AltarGlow";
            glow.GetComponent<SpriteRenderer>().color = new Color(1f, 0.9f, 0.5f, 0.55f);
            var fx = Spr("VFX/fx_07", 144.0f, 1.0f, 3.6f, -7, propRoot); fx.name = "GoldenLight";
            fx.GetComponent<SpriteRenderer>().color = new Color(1f, 0.9f, 0.5f, 0.35f);
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
            string[] layers = { "Parallax/L1_sky_clouds", "Parallax/L2_distant_mountains", "Parallax/L3_temple_waterfall", "Parallax/L4_mid_forest_mist", "Parallax/L5_large_trees_canopy" };
            float[] ys = { 2.4f, 1.4f, -0.4f, -1.9f, -1.6f };
            for (int i = 0; i < layers.Length; i++)
            {
                var g = SprW(layers[i], 0, ys[i], 20f, -100 + i * 5, null);
                g.name = System.IO.Path.GetFileName(layers[i]);
            }
            var floor = new GameObject("MenuForestFloor");
            floor.transform.position = new Vector3(0, -4.2f, 0); floor.transform.localScale = new Vector3(40f / 0.08f, 5f / 0.08f, 1);
            var fsr = floor.AddComponent<SpriteRenderer>(); fsr.sprite = S("UI/white"); fsr.color = new Color(0.17f, 0.34f, 0.30f); fsr.sortingOrder = -101;
            var mm = new GameObject("MainMenu").AddComponent<MainMenuController>();
            mm.sprites = UiSprites();
            mm.hero = S("Character/Expressions/face_happy");
            EditorSceneManager.SaveScene(scene, MenuScenePath);
        }
    }
}
