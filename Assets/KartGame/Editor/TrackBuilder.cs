using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace KartGame.EditorTools
{
    /// <summary>
    /// One-click procedural race scene: spline road mesh, curbs, walls,
    /// checkpoints, boost pads, karts (player + AI), decorations, camera,
    /// lighting and the race manager. Menu: KartGame/Build Race Scene.
    /// </summary>
    public static class TrackBuilder
    {
        const string GenDir = "Assets/KartGame/Generated";
        const string SceneDir = "Assets/KartGame/Scenes";
        const string ScenePath = SceneDir + "/RaceTrack.unity";

        const float RoadWidth = 13f;
        const int Samples = 260;

        [MenuItem("KartGame/Build Race Scene")]
        public static void BuildScene()
        {
            PrepareFolders();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

            // ---- spline ----
            Vector3[] control = TrackControlPoints();
            var pts = new Vector3[Samples];
            var tangents = new Vector3[Samples];
            SampleClosedCatmullRom(control, pts, tangents);

            // ---- environment ----
            SetupLighting();
            BuildGround();
            var roadRoot = new GameObject("Track");
            BuildRoad(roadRoot.transform, pts, tangents);
            BuildWalls(roadRoot.transform, pts, tangents);
            BuildStartGate(roadRoot.transform, pts[0], tangents[0]);
            BuildDecorations(roadRoot.transform, pts);

            // ---- race logic ----
            var manager = new GameObject("RaceManager");
            var rm = manager.AddComponent<RaceManager>();
            manager.AddComponent<KartGameUI>();

            Transform[] waypoints = BuildWaypoints(roadRoot.transform, pts, tangents);
            int checkpointCount = BuildCheckpoints(roadRoot.transform, pts, tangents);
            BuildBoostPads(roadRoot.transform, pts, tangents);

            rm.waypoints = waypoints;
            rm.checkpointCount = checkpointCount;
            rm.totalLaps = 3;

            // ---- karts ----
            var startRot = Quaternion.LookRotation(tangents[0], Vector3.up);
            Vector3 right = Vector3.Cross(Vector3.up, tangents[0]).normalized;
            var player = BuildKart("Player", new Color(0.15f, 0.5f, 1f), true, "kart-oopi");
            var aiSpecs = new (string name, Color color, float skill, string model)[]
            {
                ("Oodi", new Color(0.95f, 0.45f, 0.7f), 0.95f, "kart-oodi"),
                ("Ooli", new Color(0.65f, 0.45f, 0.25f), 0.88f, "kart-ooli"),
                ("Oobi", new Color(0.55f, 0.4f, 0.9f),  0.82f, "kart-oobi"),
            };

            var grid = new[]
            {
                pts[0] - tangents[0] * 8f  + right * 2.4f,
                pts[0] - tangents[0] * 8f  - right * 2.4f,
                pts[0] - tangents[0] * 13f + right * 2.4f,
                pts[0] - tangents[0] * 13f - right * 2.4f,
            };

            var ai0 = BuildKart(aiSpecs[0].name, aiSpecs[0].color, false, aiSpecs[0].model);
            var ai1 = BuildKart(aiSpecs[1].name, aiSpecs[1].color, false, aiSpecs[1].model);
            var ai2 = BuildKart(aiSpecs[2].name, aiSpecs[2].color, false, aiSpecs[2].model);
            PlaceKart(ai0, grid[0], startRot);
            PlaceKart(ai1, grid[1], startRot);
            PlaceKart(player, grid[2], startRot);
            PlaceKart(ai2, grid[3], startRot);

            WireAI(ai0, waypoints, aiSpecs[0].skill);
            WireAI(ai1, waypoints, aiSpecs[1].skill);
            WireAI(ai2, waypoints, aiSpecs[2].skill);

            // ---- camera ----
            var cam = Camera.main;
            var follow = cam.gameObject.AddComponent<FollowCamera>();
            follow.target = player.transform;
            follow.SnapToTarget();

            // ---- save ----
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("KartGame: race scene built and saved to " + ScenePath);
        }

        // ================= track layout =================

        static Vector3[] TrackControlPoints()
        {
            // A kart-style circuit: long start straight, hairpin, S-curves, sweeper.
            return new[]
            {
                new Vector3(  0f, 0f, -55f),
                new Vector3( 45f, 0f, -55f),
                new Vector3( 75f, 0f, -40f),
                new Vector3( 82f, 0f, -10f),
                new Vector3( 65f, 0f,  12f),
                new Vector3( 45f, 0f,  22f),
                new Vector3( 45f, 0f,  45f),
                new Vector3( 68f, 0f,  60f),
                new Vector3( 55f, 0f,  82f),
                new Vector3( 20f, 0f,  85f),
                new Vector3( -8f, 0f,  70f),
                new Vector3(-30f, 0f,  78f),
                new Vector3(-60f, 0f,  70f),
                new Vector3(-72f, 0f,  45f),
                new Vector3(-55f, 0f,  25f),
                new Vector3(-30f, 0f,  18f),
                new Vector3(-25f, 0f,  -5f),
                new Vector3(-45f, 0f, -20f),
                new Vector3(-65f, 0f, -40f),
                new Vector3(-50f, 0f, -55f),
                new Vector3(-25f, 0f, -55f),
            };
        }

        static void SampleClosedCatmullRom(Vector3[] cp, Vector3[] outPts, Vector3[] outTangents)
        {
            int n = cp.Length;
            int samples = outPts.Length;
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / samples * n;
                int seg = Mathf.FloorToInt(t) % n;
                float u = t - Mathf.Floor(t);
                Vector3 p0 = cp[(seg - 1 + n) % n];
                Vector3 p1 = cp[seg];
                Vector3 p2 = cp[(seg + 1) % n];
                Vector3 p3 = cp[(seg + 2) % n];
                outPts[i] = CatmullRom(p0, p1, p2, p3, u);
            }
            for (int i = 0; i < samples; i++)
            {
                Vector3 next = outPts[(i + 1) % samples];
                Vector3 prev = outPts[(i - 1 + samples) % samples];
                outTangents[i] = (next - prev).normalized;
            }
        }

        static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t, t3 = t2 * t;
            return 0.5f * ((2f * p1) + (-p0 + p2) * t
                + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2
                + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        // ================= environment =================

        static void SetupLighting()
        {
            var lightGo = Object.FindAnyObjectByType<Light>();
            if (lightGo != null)
            {
                lightGo.transform.rotation = Quaternion.Euler(55f, -35f, 0f);
                lightGo.intensity = 1.25f;
                lightGo.color = new Color(1f, 0.96f, 0.88f);
                lightGo.shadows = LightShadows.Soft;
            }
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 90f;
            RenderSettings.fogEndDistance = 320f;
            RenderSettings.fogColor = new Color(0.75f, 0.85f, 0.95f);
        }

        static PhysicsMaterial slickMat;

        static PhysicsMaterial SlickMaterial()
        {
            if (slickMat != null) return slickMat;
            // Grip is fully script-driven (KartController); colliders stay slick.
            slickMat = new PhysicsMaterial("Slick")
            {
                dynamicFriction = 0f,
                staticFriction = 0f,
                bounciness = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum,
                bounceCombine = PhysicsMaterialCombine.Minimum,
            };
            AssetDatabase.CreateAsset(slickMat, GenDir + "/Slick.asset");
            return slickMat;
        }

        static void BuildGround()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "Ground";
            ground.transform.position = new Vector3(0f, -0.05f, 10f);
            ground.transform.localScale = new Vector3(45f, 1f, 40f);
            ground.GetComponent<Collider>().sharedMaterial = SlickMaterial();
            var mat = NewMat("Grass", new Color(0.36f, 0.62f, 0.25f));
            mat.SetFloat("_Glossiness", 0.05f);
            ground.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        static void BuildRoad(Transform parent, Vector3[] pts, Vector3[] tangents)
        {
            int n = pts.Length;
            var verts = new Vector3[n * 2 + 2];
            var uvs = new Vector2[n * 2 + 2];
            var tris = new List<int>(n * 6);

            float half = RoadWidth * 0.5f;
            float v = 0f;
            for (int i = 0; i <= n; i++)
            {
                int idx = i % n;
                if (i > 0) v += Vector3.Distance(pts[idx], pts[(i - 1) % n]) / RoadWidth;
                Vector3 side = Vector3.Cross(Vector3.up, tangents[idx]).normalized;
                verts[i * 2] = pts[idx] + side * half + Vector3.up * 0.02f;
                verts[i * 2 + 1] = pts[idx] - side * half + Vector3.up * 0.02f;
                uvs[i * 2] = new Vector2(0f, v);
                uvs[i * 2 + 1] = new Vector2(1f, v);
            }
            for (int i = 0; i < n; i++)
            {
                int a = i * 2, b = i * 2 + 1, c = (i + 1) * 2, d = (i + 1) * 2 + 1;
                tris.Add(a); tris.Add(b); tris.Add(c);
                tris.Add(b); tris.Add(d); tris.Add(c);
            }

            var mesh = new Mesh { name = "RoadMesh", vertices = verts, uv = uvs, triangles = tris.ToArray() };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            SaveAsset(mesh, "RoadMesh.asset");

            // Visual only: karts drive on the flat ground plane, so the thin road
            // mesh never wedges or tunnels them.
            var road = new GameObject("Road", typeof(MeshFilter), typeof(MeshRenderer));
            road.transform.SetParent(parent, false);
            road.GetComponent<MeshFilter>().sharedMesh = mesh;

            var tex = MakeRoadTexture();
            var mat = NewMat("Road", Color.white);
            mat.mainTexture = tex;
            mat.SetFloat("_Glossiness", 0.15f);
            road.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        static Texture2D MakeRoadTexture()
        {
            const int W = 256, H = 256;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, true) { name = "RoadTex", wrapMode = TextureWrapMode.Repeat };
            var asphalt = new Color(0.23f, 0.23f, 0.25f);
            var curbRed = new Color(0.85f, 0.15f, 0.12f);
            var curbWhite = new Color(0.95f, 0.95f, 0.95f);
            var line = new Color(0.92f, 0.92f, 0.92f);

            var rnd = new System.Random(7);
            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    float u = (float)x / W;
                    float vv = (float)y / H;
                    Color c;
                    if (u < 0.05f || u > 0.95f)
                    {
                        bool red = Mathf.FloorToInt(vv * 4f) % 2 == 0;
                        c = red ? curbRed : curbWhite;
                    }
                    else if (Mathf.Abs(u - 0.085f) < 0.006f || Mathf.Abs(u - 0.915f) < 0.006f)
                        c = line;
                    else if (Mathf.Abs(u - 0.5f) < 0.005f && (vv % 0.5f) < 0.25f)
                        c = line;
                    else
                    {
                        float noise = (float)rnd.NextDouble() * 0.035f;
                        c = new Color(asphalt.r + noise, asphalt.g + noise, asphalt.b + noise);
                    }
                    tex.SetPixel(x, y, c);
                }
            }
            tex.Apply();
            SaveAsset(tex, "RoadTex.asset");
            return tex;
        }

        static void BuildWalls(Transform parent, Vector3[] pts, Vector3[] tangents)
        {
            BuildWall(parent, pts, tangents, +1);
            BuildWall(parent, pts, tangents, -1);
        }

        static void BuildWall(Transform parent, Vector3[] pts, Vector3[] tangents, int sideSign)
        {
            int n = pts.Length;
            float offset = RoadWidth * 0.5f + 0.7f;
            const float height = 1.1f;

            // two vertex blocks (front + back copy) so each face keeps its own
            // normals — sharing vertices across opposing faces averages them to zero
            int block = (n + 1) * 2;
            var verts = new Vector3[block * 2];
            var uvs = new Vector2[block * 2];
            var tris = new List<int>(n * 12);
            float v = 0f;
            for (int i = 0; i <= n; i++)
            {
                int idx = i % n;
                if (i > 0) v += Vector3.Distance(pts[idx], pts[(i - 1) % n]) / 4f;
                Vector3 side = Vector3.Cross(Vector3.up, tangents[idx]).normalized * sideSign;
                Vector3 basePos = pts[idx] + side * offset;
                verts[i * 2] = basePos;
                verts[i * 2 + 1] = basePos + Vector3.up * height;
                uvs[i * 2] = new Vector2(v, 0f);
                uvs[i * 2 + 1] = new Vector2(v, 1f);
                verts[block + i * 2] = verts[i * 2];
                verts[block + i * 2 + 1] = verts[i * 2 + 1];
                uvs[block + i * 2] = uvs[i * 2];
                uvs[block + i * 2 + 1] = uvs[i * 2 + 1];
            }
            for (int i = 0; i < n; i++)
            {
                int a = i * 2, b = i * 2 + 1, c = (i + 1) * 2, d = (i + 1) * 2 + 1;
                tris.Add(a); tris.Add(b); tris.Add(c);
                tris.Add(c); tris.Add(b); tris.Add(d);
                tris.Add(block + c); tris.Add(block + b); tris.Add(block + a);
                tris.Add(block + d); tris.Add(block + b); tris.Add(block + c);
            }

            var mesh = new Mesh { name = "WallMesh" + sideSign, vertices = verts, uv = uvs, triangles = tris.ToArray() };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            SaveAsset(mesh, $"WallMesh{(sideSign > 0 ? "L" : "R")}.asset");

            var wall = new GameObject("Wall" + (sideSign > 0 ? "L" : "R"),
                typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider));
            wall.transform.SetParent(parent, false);
            wall.GetComponent<MeshFilter>().sharedMesh = mesh;
            wall.GetComponent<MeshCollider>().sharedMesh = mesh;
            wall.GetComponent<MeshCollider>().sharedMaterial = SlickMaterial();

            var tex = MakeStripeTexture("WallTex", new Color(0.2f, 0.45f, 0.95f), Color.white);
            var mat = NewMat("Wall" + (sideSign > 0 ? "L" : "R"), Color.white);
            mat.mainTexture = tex;
            wall.GetComponent<MeshRenderer>().sharedMaterial = mat;
        }

        static Texture2D MakeStripeTexture(string name, Color a, Color b)
        {
            const int W = 64, H = 8;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Repeat };
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    tex.SetPixel(x, y, (x / 8) % 2 == 0 ? a : b);
            tex.Apply();
            SaveAsset(tex, name + ".asset");
            return tex;
        }

        static void BuildStartGate(Transform parent, Vector3 pos, Vector3 tangent)
        {
            var gate = new GameObject("StartGate");
            gate.transform.SetParent(parent, false);
            Vector3 side = Vector3.Cross(Vector3.up, tangent).normalized;
            var pillarMat = NewMat("Pillar", new Color(0.85f, 0.85f, 0.9f));

            foreach (int s in new[] { -1, 1 })
            {
                var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pillar.name = "Pillar" + s;
                pillar.transform.SetParent(gate.transform, false);
                pillar.transform.position = pos + side * s * (RoadWidth * 0.5f + 1.6f) + Vector3.up * 3.5f;
                pillar.transform.localScale = new Vector3(0.8f, 3.5f, 0.8f);
                pillar.GetComponent<MeshRenderer>().sharedMaterial = pillarMat;
            }

            var banner = GameObject.CreatePrimitive(PrimitiveType.Cube);
            banner.name = "Banner";
            banner.transform.SetParent(gate.transform, false);
            banner.transform.position = pos + Vector3.up * 7.2f;
            banner.transform.rotation = Quaternion.LookRotation(tangent, Vector3.up);
            banner.transform.localScale = new Vector3(RoadWidth + 4.5f, 1.6f, 0.3f);
            var checkerTex = MakeCheckerTexture();
            var bannerMat = NewMat("Banner", Color.white);
            bannerMat.mainTexture = checkerTex;
            bannerMat.mainTextureScale = new Vector2(8f, 1f);
            banner.GetComponent<MeshRenderer>().sharedMaterial = bannerMat;

            // start line painted on the road
            var lineGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            lineGo.name = "StartLine";
            Object.DestroyImmediate(lineGo.GetComponent<Collider>());
            lineGo.transform.SetParent(gate.transform, false);
            lineGo.transform.position = pos + Vector3.up * 0.045f;
            lineGo.transform.rotation = Quaternion.LookRotation(Vector3.down, tangent);
            lineGo.transform.localScale = new Vector3(RoadWidth - 1f, 3f, 1f);
            var lineMat = NewMat("StartLineMat", Color.white);
            lineMat.mainTexture = checkerTex;
            lineMat.mainTextureScale = new Vector2(6f, 2f);
            lineGo.GetComponent<MeshRenderer>().sharedMaterial = lineMat;
        }

        static Texture2D MakeCheckerTexture()
        {
            const int S = 64;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { name = "CheckerTex", wrapMode = TextureWrapMode.Repeat };
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                    tex.SetPixel(x, y, ((x / 32) + (y / 32)) % 2 == 0 ? Color.white : Color.black);
            tex.Apply();
            SaveAsset(tex, "CheckerTex.asset");
            return tex;
        }

        static void BuildDecorations(Transform parent, Vector3[] pts)
        {
            var decor = new GameObject("Decorations");
            decor.transform.SetParent(parent, false);
            var trunkMat = NewMat("Trunk", new Color(0.45f, 0.3f, 0.18f));
            var leafMat = NewMat("Leaves", new Color(0.15f, 0.5f, 0.2f));
            var balloonColors = new[]
            {
                new Color(1f, 0.3f, 0.3f), new Color(1f, 0.8f, 0.2f),
                new Color(0.3f, 0.6f, 1f), new Color(0.5f, 0.9f, 0.4f),
            };
            var balloonMats = new Material[balloonColors.Length];
            for (int i = 0; i < balloonColors.Length; i++)
                balloonMats[i] = NewMat("Balloon" + i, balloonColors[i]);

            Random.InitState(42);
            int placed = 0, tries = 0;
            while (placed < 30 && tries < 400)
            {
                tries++;
                Vector3 p = new Vector3(Random.Range(-95f, 100f), 0f, Random.Range(-75f, 100f));
                if (DistanceToTrack(p, pts) < RoadWidth * 0.5f + 4f) continue;

                if (placed % 4 == 3)
                {
                    var balloon = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    balloon.name = "Balloon";
                    Object.DestroyImmediate(balloon.GetComponent<Collider>());
                    balloon.transform.SetParent(decor.transform, false);
                    balloon.transform.position = p + Vector3.up * Random.Range(6f, 11f);
                    balloon.transform.localScale = Vector3.one * Random.Range(1.4f, 2.4f);
                    balloon.GetComponent<MeshRenderer>().sharedMaterial = balloonMats[placed % balloonMats.Length];
                }
                else
                {
                    var tree = new GameObject("Tree");
                    tree.transform.SetParent(decor.transform, false);
                    tree.transform.position = p;
                    var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    trunk.transform.SetParent(tree.transform, false);
                    trunk.transform.localPosition = new Vector3(0f, 1.4f, 0f);
                    trunk.transform.localScale = new Vector3(0.5f, 1.4f, 0.5f);
                    trunk.GetComponent<MeshRenderer>().sharedMaterial = trunkMat;
                    var crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    Object.DestroyImmediate(crown.GetComponent<Collider>());
                    crown.transform.SetParent(tree.transform, false);
                    float s = Random.Range(2.6f, 4.2f);
                    crown.transform.localPosition = new Vector3(0f, 2.6f + s * 0.35f, 0f);
                    crown.transform.localScale = Vector3.one * s;
                    crown.GetComponent<MeshRenderer>().sharedMaterial = leafMat;
                }
                placed++;
            }
        }

        static float DistanceToTrack(Vector3 p, Vector3[] pts)
        {
            float best = float.MaxValue;
            for (int i = 0; i < pts.Length; i += 3)
            {
                float d = Vector2.Distance(new Vector2(p.x, p.z), new Vector2(pts[i].x, pts[i].z));
                if (d < best) best = d;
            }
            return best;
        }

        // ================= race logic objects =================

        static Transform[] BuildWaypoints(Transform parent, Vector3[] pts, Vector3[] tangents)
        {
            var root = new GameObject("Waypoints");
            root.transform.SetParent(parent, false);
            var list = new List<Transform>();
            for (int i = 0; i < pts.Length; i += 6)
            {
                var wp = new GameObject("WP_" + list.Count);
                wp.transform.SetParent(root.transform, false);
                wp.transform.SetPositionAndRotation(pts[i] + Vector3.up * 0.5f,
                    Quaternion.LookRotation(tangents[i], Vector3.up));
                list.Add(wp.transform);
            }
            return list.ToArray();
        }

        static int BuildCheckpoints(Transform parent, Vector3[] pts, Vector3[] tangents)
        {
            var root = new GameObject("Checkpoints");
            root.transform.SetParent(parent, false);
            int step = pts.Length / 12;
            int count = 0;
            for (int i = 0; i < pts.Length - step / 2; i += step)
            {
                var go = new GameObject("Checkpoint_" + count);
                go.transform.SetParent(root.transform, false);
                go.transform.SetPositionAndRotation(pts[i] + Vector3.up * 2.5f,
                    Quaternion.LookRotation(tangents[i], Vector3.up));
                var box = go.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(RoadWidth + 4f, 6f, 1.5f);
                var cp = go.AddComponent<Checkpoint>();
                cp.index = count;
                count++;
            }
            return count;
        }

        static void BuildBoostPads(Transform parent, Vector3[] pts, Vector3[] tangents)
        {
            var root = new GameObject("BoostPads");
            root.transform.SetParent(parent, false);
            var tex = MakeBoostTexture();
            var mat = NewMat("BoostPad", Color.white);
            mat.mainTexture = tex;
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(1f, 0.45f, 0.05f) * 0.9f);
            mat.SetTexture("_EmissionMap", tex);

            // pick straight-ish, well separated sample indices
            var chosen = new List<int>();
            for (int i = 0; i < pts.Length && chosen.Count < 4; i += 5)
            {
                Vector3 t0 = tangents[i];
                Vector3 t1 = tangents[(i + 8) % pts.Length];
                if (Vector3.Angle(t0, t1) < 6f &&
                    (chosen.Count == 0 || Mathf.Abs(i - chosen[chosen.Count - 1]) > pts.Length / 6) &&
                    i > 12)
                    chosen.Add(i);
            }

            foreach (int i in chosen)
            {
                var pad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                pad.name = "BoostPad";
                Object.DestroyImmediate(pad.GetComponent<Collider>());
                pad.transform.SetParent(root.transform, false);
                pad.transform.position = pts[i] + Vector3.up * 0.06f;
                pad.transform.rotation = Quaternion.LookRotation(Vector3.down, tangents[i]);
                pad.transform.localScale = new Vector3(4.5f, 6f, 1f);
                pad.GetComponent<MeshRenderer>().sharedMaterial = mat;

                var trigger = pad.AddComponent<BoxCollider>();
                trigger.isTrigger = true;
                trigger.size = new Vector3(1f, 1f, 2.5f);   // z = vertical after the quad's rotation
                pad.AddComponent<SpeedBoostPad>();
            }
        }

        static Texture2D MakeBoostTexture()
        {
            const int S = 64;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { name = "BoostTex", wrapMode = TextureWrapMode.Repeat };
            var orange = new Color(1f, 0.5f, 0.05f);
            var yellow = new Color(1f, 0.85f, 0.25f);
            for (int y = 0; y < S; y++)
            {
                for (int x = 0; x < S; x++)
                {
                    // chevron arrows pointing +v
                    float cx = Mathf.Abs(x - S / 2f) / (S / 2f);
                    float band = ((y / (float)S) + cx * 0.35f) % 0.34f;
                    tex.SetPixel(x, y, band < 0.17f ? orange : yellow);
                }
            }
            tex.Apply();
            SaveAsset(tex, "BoostTex.asset");
            return tex;
        }

        // ================= karts =================

        static GameObject BuildKart(string name, Color color, bool isPlayer, string modelFile = null)
        {
            var root = new GameObject(name);
            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 150f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

            // Collider bottom sits 0.07 below the root so the root rests at road
            // height (0.02) when the collider touches the ground plane (-0.05).
            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.33f, 0f);
            col.size = new Vector3(1.5f, 0.8f, 2.3f);
            col.sharedMaterial = SlickMaterial();

            var visualRoot = new GameObject("Visual");
            visualRoot.transform.SetParent(root.transform, false);

            Transform[] wheelTransforms = null;
            bool usedModel = modelFile != null &&
                TryBuildKenneyVisual(visualRoot.transform, modelFile, out wheelTransforms);
            if (!usedModel)
                wheelTransforms = BuildPrimitiveVisual(visualRoot.transform, name, color);

            var kart = root.AddComponent<KartController>();
            kart.isPlayer = isPlayer;
            kart.wheels = wheelTransforms;
            kart.body = visualRoot.transform;
            root.AddComponent<KartEffects>();
            return root;
        }

        /// <summary>
        /// Instantiates a Kenney kart FBX under parent, auto-scaled so the model
        /// is ~2.2 units long with its wheels on the ground. Returns false when
        /// the asset is missing (caller falls back to primitives).
        /// </summary>
        static bool TryBuildKenneyVisual(Transform parent, string modelFile, out Transform[] wheels)
        {
            wheels = null;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/KartGame/Kenney/" + modelFile + ".fbx");
            if (prefab == null)
            {
                Debug.LogWarning("KartGame: missing Kenney model " + modelFile + ", using primitive kart.");
                return false;
            }

            var model = (GameObject)Object.Instantiate(prefab);
            model.name = modelFile;
            model.transform.SetParent(parent, false);

            // strip any colliders the importer may have added
            foreach (var c in model.GetComponentsInChildren<Collider>())
                Object.DestroyImmediate(c);

            // measure and fit: ~2.2 units long, wheels touching local y = 0
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return false;
            Bounds b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            float length = Mathf.Max(b.size.z, 0.01f);
            float scale = 2.2f / length;
            model.transform.localScale = Vector3.one * scale;

            // recompute bounds after scaling to sit the model on the ground
            b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            Vector3 worldOffset = parent.position - new Vector3(b.center.x, b.min.y, b.center.z);
            model.transform.position += worldOffset;

            var found = new List<Transform>();
            foreach (var t in model.GetComponentsInChildren<Transform>())
                if (t != model.transform && t.name.ToLowerInvariant().Contains("wheel"))
                    found.Add(t);
            wheels = found.ToArray();
            return true;
        }

        static Transform[] BuildPrimitiveVisual(Transform visual, string name, Color color)
        {
            var bodyMat = NewMat("Kart_" + name, color);
            bodyMat.SetFloat("_Glossiness", 0.6f);
            bodyMat.SetFloat("_Metallic", 0.4f);
            var darkMat = NewMat("KartDark_" + name, new Color(0.12f, 0.12f, 0.14f));
            var skinMat = NewMat("KartSkin_" + name, new Color(1f, 0.83f, 0.66f));

            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.transform.SetParent(visual, false);
            body.transform.localPosition = new Vector3(0f, 0.42f, 0f);
            body.transform.localScale = new Vector3(1.25f, 0.35f, 2.1f);
            body.GetComponent<MeshRenderer>().sharedMaterial = bodyMat;

            var nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nose.name = "Nose";
            Object.DestroyImmediate(nose.GetComponent<Collider>());
            nose.transform.SetParent(visual, false);
            nose.transform.localPosition = new Vector3(0f, 0.45f, 1.05f);
            nose.transform.localScale = new Vector3(0.6f, 0.22f, 0.5f);
            nose.GetComponent<MeshRenderer>().sharedMaterial = bodyMat;

            var seat = GameObject.CreatePrimitive(PrimitiveType.Cube);
            seat.name = "Seat";
            Object.DestroyImmediate(seat.GetComponent<Collider>());
            seat.transform.SetParent(visual, false);
            seat.transform.localPosition = new Vector3(0f, 0.75f, -0.55f);
            seat.transform.localScale = new Vector3(0.7f, 0.55f, 0.25f);
            seat.GetComponent<MeshRenderer>().sharedMaterial = darkMat;

            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "DriverHead";
            Object.DestroyImmediate(head.GetComponent<Collider>());
            head.transform.SetParent(visual, false);
            head.transform.localPosition = new Vector3(0f, 1.05f, -0.35f);
            head.transform.localScale = Vector3.one * 0.42f;
            head.GetComponent<MeshRenderer>().sharedMaterial = skinMat;

            var helmet = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            helmet.name = "Helmet";
            Object.DestroyImmediate(helmet.GetComponent<Collider>());
            helmet.transform.SetParent(visual, false);
            helmet.transform.localPosition = new Vector3(0f, 1.12f, -0.35f);
            helmet.transform.localScale = Vector3.one * 0.46f;
            helmet.GetComponent<MeshRenderer>().sharedMaterial = bodyMat;

            var wheelMat = NewMat("Wheel_" + name, new Color(0.08f, 0.08f, 0.09f));
            var wheelPositions = new[]
            {
                new Vector3(-0.72f, 0.28f,  0.75f), new Vector3(0.72f, 0.28f,  0.75f),
                new Vector3(-0.72f, 0.28f, -0.75f), new Vector3(0.72f, 0.28f, -0.75f),
            };
            var wheelMounts = new Transform[4];
            for (int i = 0; i < 4; i++)
            {
                var mount = new GameObject("WheelMount" + i);
                mount.transform.SetParent(visual, false);
                mount.transform.localPosition = wheelPositions[i];
                var wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Object.DestroyImmediate(wheel.GetComponent<Collider>());
                wheel.transform.SetParent(mount.transform, false);
                wheel.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                wheel.transform.localScale = new Vector3(0.56f, 0.14f, 0.56f);
                wheel.GetComponent<MeshRenderer>().sharedMaterial = wheelMat;
                wheelMounts[i] = mount.transform;
            }

            return wheelMounts;
        }

        static void PlaceKart(GameObject kart, Vector3 pos, Quaternion rot)
        {
            kart.transform.SetPositionAndRotation(pos + Vector3.up * 0.3f, rot);
        }

        static void WireAI(GameObject kart, Transform[] waypoints, float skill)
        {
            var ai = kart.AddComponent<KartAI>();
            ai.waypoints = waypoints;
            ai.skill = skill;
        }

        // ================= assets =================

        static void PrepareFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/KartGame"))
                AssetDatabase.CreateFolder("Assets", "KartGame");
            if (AssetDatabase.IsValidFolder(GenDir))
                AssetDatabase.DeleteAsset(GenDir);
            AssetDatabase.CreateFolder("Assets/KartGame", "Generated");
            if (!AssetDatabase.IsValidFolder(SceneDir))
                AssetDatabase.CreateFolder("Assets/KartGame", "Scenes");
        }

        static Material NewMat(string name, Color color)
        {
            var mat = new Material(Shader.Find("Standard")) { name = name, color = color };
            SaveAsset(mat, "M_" + name + ".mat");
            return mat;
        }

        static T SaveAsset<T>(T obj, string file) where T : Object
        {
            AssetDatabase.CreateAsset(obj, GenDir + "/" + file);
            return obj;
        }
    }
}
