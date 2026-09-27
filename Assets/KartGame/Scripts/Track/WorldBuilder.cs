using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace KartGame
{
    /// <summary>Everything the race needs to know about the generated circuit.</summary>
    public class World
    {
        public TrackDef Def;
        public TrackData Track;
        public ThemeStyle Style;
        public Transform Root;
        public Vector3[] GridPos;
        public Quaternion GridRot;
        public StartLights Lights;
        public Texture2D Minimap;
        public Vector2 MapMin;
        public float MapSize;
        public readonly List<ItemBox> ItemBoxes = new List<ItemBox>();
    }

    /// <summary>
    /// Runtime circuit generator: road with raised curbs and shoulders, barrier
    /// blocks / tire walls / bridge rails, terrain that hugs the road, lake +
    /// bridge, tunnel, start gate with lights, boost pads, jump ramps, item boxes
    /// and themed scenery batched into a few meshes.
    /// </summary>
    public static class WorldBuilder
    {
        const float CurbW = 1.0f;
        const float ShoulderW = 2.3f;
        const float WallH = 3.2f;

        public static World Build(TrackDef def, bool itemMode)
        {
            var w = new World { Def = def, Track = new TrackData(def), Style = ThemeStyle.For(def.Theme) };
            w.Root = new GameObject("World").transform;
            SetupEnvironment(w.Style);

            var tr = w.Track;
            var rng = new Rng(def.Seed);
            BuildRoad(w);
            BuildWalls(w);
            BuildBarriers(w, rng);
            BuildTerrain(w);
            if (def.LakeRadius > 0f) BuildLake(w);
            BuildTunnels(w);
            BuildStartGate(w);
            BuildBoostPads(w);
            BuildRamps(w);
            if (itemMode) BuildItemBoxes(w);
            BuildScenery(w, rng);
            BuildMinimap(w);

            // starting grid: 2 columns, staggered, behind the line
            w.GridPos = new Vector3[8];
            for (int k = 0; k < 8; k++)
            {
                int row = k / 2, col = k % 2;
                float back = 8f + row * 6.5f + col * 3.2f;
                int idx = tr.Wrap(-Mathf.RoundToInt(back / TrackData.Spacing));
                w.GridPos[k] = tr.Surface(idx, col == 0 ? -2.8f : 2.8f) + Vector3.up * 0.1f;
            }
            Vector3 f0 = tr.Fwd[0]; f0.y = 0;
            w.GridRot = Quaternion.LookRotation(f0.normalized, Vector3.up);
            return w;
        }

        // ================================================================ environment

        public static void SetupEnvironment(ThemeStyle s)
        {
            var sky = Mats.Sky();
            sky.SetColor("_Top", s.SkyTop);
            sky.SetColor("_Horizon", s.SkyHorizon);
            sky.SetColor("_Bottom", s.SkyBottom);
            sky.SetColor("_SunColor", s.Sun);
            sky.SetColor("_CloudShade", s.CloudShade);
            sky.SetFloat("_CloudAmount", s.CloudAmount);
            Vector3 sunDir = -(Quaternion.Euler(s.SunEuler) * Vector3.forward);
            sky.SetVector("_SunDir", sunDir);
            RenderSettings.skybox = sky;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = s.AmbSky;
            RenderSettings.ambientEquatorColor = s.AmbEq;
            RenderSettings.ambientGroundColor = s.AmbGround;
            var sh = new SphericalHarmonicsL2();
            sh.AddAmbientLight(s.AmbEq.linear);
            sh.AddDirectionalLight(Vector3.up, (s.AmbSky.linear - s.AmbEq.linear) * 1.6f, 1f);
            sh.AddDirectionalLight(Vector3.down, (s.AmbGround.linear - s.AmbEq.linear) * 1.6f, 1f);
            RenderSettings.ambientProbe = sh;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = s.Fog;
            RenderSettings.fogStartDistance = s.FogStart;
            RenderSettings.fogEndDistance = s.FogEnd;

            Shader.SetGlobalColor("_KG_SkyTop", s.SkyTop);
            Shader.SetGlobalColor("_KG_SkyHorizon", s.SkyHorizon);
            Shader.SetGlobalColor("_KG_Ground", s.AmbGround);

            Light sun = null;
            foreach (var l in Object.FindObjectsByType<Light>())
                if (l.type == LightType.Directional) { sun = l; break; }
            if (sun == null)
            {
                sun = new GameObject("Sun").AddComponent<Light>();
                sun.type = LightType.Directional;
            }
            sun.transform.rotation = Quaternion.Euler(s.SunEuler);
            sun.color = s.Sun;
            sun.intensity = s.SunIntensity;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.75f;
            sun.shadowBias = 0.04f;
            sun.shadowNormalBias = 0.3f;
            RenderSettings.sun = sun;
            QualitySettings.shadowDistance = 110f;
            QualitySettings.shadowCascades = 2;
        }

        // ================================================================ road

        static float EdgeOffset => CurbW + ShoulderW;

        static void BuildRoad(World w)
        {
            var t = w.Track;
            var s = w.Style;
            float hw = t.HalfWidth;
            int n = t.Count;

            var road = new MeshBuilder();
            var curb = new MeshBuilder();
            var shoulder = new MeshBuilder();
            var col = new MeshBuilder();

            for (int i = 0; i <= n; i++)
            {
                int k = i % n;
                float v = i * TrackData.Spacing;
                Vector3 p = t.Pos[k], r = t.Right[k], up = t.Up[k];
                // asphalt
                road.Vert(p - r * hw, up, Color.white, new Vector2(0, v / t.Def.Width));
                road.Vert(p + r * hw, up, Color.white, new Vector2(1, v / t.Def.Width));
                // raised curbs (left then right), 4 verts each
                float[] cx = { 0f, 0.15f, 0.85f, 1f };
                float[] cy = { 0f, 0.07f, 0.07f, 0f };
                for (int side = -1; side <= 1; side += 2)
                    for (int c = 0; c < 4; c++)
                    {
                        Vector3 q = p + r * side * (hw + cx[c] * CurbW) + up * cy[c];
                        Vector3 nrm = up;
                        if (c == 0) nrm = (up - r * side * 0.6f).normalized;
                        if (c == 3) nrm = (up + r * side * 0.6f).normalized;
                        curb.Vert(q, nrm, Color.white, new Vector2(side < 0 ? 1f - cx[c] : cx[c], v / 4f));
                    }
                // shoulders + outside skirt
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 a = p + r * side * (hw + CurbW);
                    Vector3 b = p + r * side * (hw + EdgeOffset + 0.6f);
                    Vector3 c2 = b + r * side * 2.2f - Vector3.up * (t.Bridge[k] ? 0.9f : 4.5f);
                    Color gc = t.Bridge[k] || t.Tunnel[k] ? new Color(0.55f, 0.52f, 0.48f) : Color.white;
                    shoulder.Vert(a, up, gc);
                    shoulder.Vert(b, up, gc);
                    shoulder.Vert(c2, (r * side + up).normalized, t.Bridge[k] ? new Color(0.5f, 0.48f, 0.45f) : new Color(0.85f, 0.8f, 0.72f));
                }
                // collider strip (flat across everything drivable)
                col.Vert(p - r * (hw + EdgeOffset + 1.2f), up, Color.white);
                col.Vert(p + r * (hw + EdgeOffset + 1.2f), up, Color.white);
            }
            for (int i = 0; i < n; i++)
            {
                Vector3 up = t.Up[i];
                int a = i * 2, b = a + 1, c = a + 2, d = a + 3;
                road.TriFacing(a, c, b, up); road.TriFacing(b, c, d, up);
                col.TriFacing(a, c, b, up); col.TriFacing(b, c, d, up);
                for (int side = 0; side < 2; side++)
                    for (int q = 0; q < 3; q++)
                    {
                        int i0 = i * 8 + side * 4 + q, i1 = i0 + 1, i2 = i0 + 8, i3 = i1 + 8;
                        curb.TriFacing(i0, i2, i1, up); curb.TriFacing(i1, i2, i3, up);
                    }
                for (int side = 0; side < 2; side++)
                {
                    int s0 = i * 6 + side * 3;
                    int s1 = s0 + 6;
                    Vector3 outN = t.Right[i] * (side == 0 ? -1f : 1f);
                    shoulder.TriFacing(s0, s1, s0 + 1, up); shoulder.TriFacing(s0 + 1, s1, s1 + 1, up);
                    Vector3 skirtN = (outN + up).normalized;
                    shoulder.TriFacing(s0 + 1, s1 + 1, s0 + 2, skirtN); shoulder.TriFacing(s0 + 2, s1 + 1, s1 + 2, skirtN);
                }
            }

            var roadMat = Mats.Lit(Color.white, 0.12f, ProcTex.Road(s.Asphalt, s.Line), "Road");
            road.Build("Road", w.Root, roadMat, false).GetComponent<MeshRenderer>().receiveShadows = true;
            var curbMat = Mats.Lit(Color.white, 0.25f, ProcTex.Curb(s.CurbA, s.CurbB), "Curb");
            curb.Build("Curbs", w.Root, curbMat, false);
            var shoulderMat = GroundMaterial(s, "Shoulder", s.Shoulder);
            shoulder.Build("Shoulders", w.Root, shoulderMat, false);

            var colGo = new GameObject("RoadCollider");
            colGo.transform.SetParent(w.Root, false);
            var mc = colGo.AddComponent<MeshCollider>();
            mc.sharedMesh = col.ToMesh("RoadCollider");
            mc.sharedMaterial = Mats.Slick;
        }

        static Material GroundMaterial(ThemeStyle s, string name, Color tint)
        {
            var tex = ProcTex.Ground(s.Snowy ? "snow" : "grass", Color.white, new Color(0.86f, 0.86f, 0.86f), s.Snowy ? 0.03f : 0.12f);
            var m = Mats.Lit(tint, s.Snowy ? 0.25f : 0.02f, tex, name);
            m.SetFloat("_WorldUV", 0.18f);
            m.SetFloat("_Detail", 0.5f);
            return m;
        }

        static void BuildWalls(World w)
        {
            var t = w.Track;
            int n = t.Count;
            var b = new MeshBuilder();
            float off = t.HalfWidth + EdgeOffset + 0.35f;
            for (int side = -1; side <= 1; side += 2)
            {
                int start = b.VertexCount;
                for (int i = 0; i <= n; i++)
                {
                    int k = i % n;
                    Vector3 basePos = t.Pos[k] + t.Right[k] * side * off;
                    b.Vert(basePos - Vector3.up * 1.5f, -t.Right[k] * side, Color.white);
                    b.Vert(basePos + Vector3.up * WallH, -t.Right[k] * side, Color.white);
                }
                for (int i = 0; i < n; i++)
                {
                    int a = start + i * 2, c = a + 2;
                    // double sided so nothing slips through from either side
                    b.Tri(a, a + 1, c); b.Tri(c, a + 1, c + 1);
                    b.Tri(a, c, a + 1); b.Tri(c, c + 1, a + 1);
                }
            }
            var go = new GameObject("WallColliders");
            go.transform.SetParent(w.Root, false);
            var mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = b.ToMesh("Walls");
            mc.sharedMaterial = Mats.Slick;
        }

        // ================================================================ barriers

        static void BuildBarriers(World w, Rng rng)
        {
            var t = w.Track;
            var s = w.Style;
            int n = t.Count;
            var blocks = new MeshBuilder();
            var tires = new MeshBuilder();
            var rails = new MeshBuilder();
            float off = t.HalfWidth + EdgeOffset + 0.35f;
            const int seg = 4;
            var jersey = new[]
            {
                new Vector2(0.32f, 0f), new Vector2(0.32f, 0.14f), new Vector2(0.15f, 0.34f), new Vector2(0.12f, 0.86f),
                new Vector2(-0.12f, 0.86f), new Vector2(-0.15f, 0.34f), new Vector2(-0.32f, 0.14f), new Vector2(-0.32f, 0f),
            };
            for (int side = -1; side <= 1; side += 2)
            {
                int blockIdx = 0;
                for (int i = 0; i < n; i += seg)
                {
                    if (t.Tunnel[i]) continue;
                    float curvAvg = 0f;
                    for (int k = -6; k <= 6; k++) curvAvg += t.Curv[t.Wrap(i + k)];
                    curvAvg /= 13f;
                    bool outerOfCorner = Mathf.Abs(curvAvg) > 2.1f && Mathf.Sign(curvAvg) != side;
                    if (t.Bridge[i])
                    {
                        // steel railing
                        rails.Gloss = 0.7f;
                        Vector3 p = t.Pos[i] + t.Right[i] * side * off;
                        rails.Cylinder(p - Vector3.up * 0.1f, p + Vector3.up * 1.1f, 0.06f, 6, new Color(0.85f, 0.86f, 0.9f));
                        int j = t.Wrap(i + seg);
                        Vector3 q = t.Pos[j] + t.Right[j] * side * off;
                        rails.Tube(new[] { p + Vector3.up * 1.05f, q + Vector3.up * 1.05f }, 0.05f, 6, s.BarrierA, false);
                        rails.Tube(new[] { p + Vector3.up * 0.6f, q + Vector3.up * 0.6f }, 0.04f, 6, new Color(0.9f, 0.9f, 0.92f), false);
                        rails.Gloss = 0f;
                        continue;
                    }
                    if (outerOfCorner)
                    {
                        for (int k = 0; k < seg; k++)
                        {
                            int j = t.Wrap(i + k);
                            Vector3 p = t.Pos[j] + t.Right[j] * side * (off + 0.1f);
                            Props.TireStack(tires, p - Vector3.up * 0.05f, 3, (j / 2) % 2 == 0 ? s.BarrierA : s.BarrierB, rng);
                        }
                        continue;
                    }
                    var path = new List<Vector3>();
                    for (int k = 0; k <= seg; k++)
                    {
                        int j = t.Wrap(i + k);
                        float along = k == 0 ? 0.04f : (k == seg ? -0.04f : 0f);
                        path.Add(t.Pos[j] + t.Right[j] * side * off + t.Fwd[j] * along - Vector3.up * 0.05f);
                    }
                    Color c = (blockIdx++ % 2 == 0) ? s.BarrierA : s.BarrierB;
                    blocks.Gloss = 0.3f;
                    blocks.Sweep(path, jersey, (p, nn) => p.y < 0f ? c * 0.8f : c, Vector3.up);
                }
            }
            blocks.Build("Barriers", w.Root, Mats.VertexLit);
            if (tires.VertexCount > 0) tires.Build("TireWalls", w.Root, Mats.VertexLit);
            if (rails.VertexCount > 0) rails.Build("BridgeRails", w.Root, Mats.VertexLit);
        }

        // ================================================================ terrain

        class Nearest
        {
            readonly TrackData t;
            readonly Dictionary<long, List<int>> buckets = new Dictionary<long, List<int>>();
            const float Cell = 16f;
            public Nearest(TrackData t)
            {
                this.t = t;
                for (int i = 0; i < t.Count; i++)
                {
                    long key = Key(Mathf.FloorToInt(t.Pos[i].x / Cell), Mathf.FloorToInt(t.Pos[i].z / Cell));
                    if (!buckets.TryGetValue(key, out var list)) buckets[key] = list = new List<int>();
                    list.Add(i);
                }
            }
            static long Key(int x, int z) => ((long)x << 32) ^ (uint)z;
            public float Query(float x, float z, out int idx)
            {
                int cx = Mathf.FloorToInt(x / Cell), cz = Mathf.FloorToInt(z / Cell);
                float best = float.MaxValue; idx = -1;
                for (int dx = -3; dx <= 3; dx++)
                    for (int dz = -3; dz <= 3; dz++)
                    {
                        if (!buckets.TryGetValue(Key(cx + dx, cz + dz), out var list)) continue;
                        foreach (int i in list)
                        {
                            float ddx = t.Pos[i].x - x, ddz = t.Pos[i].z - z;
                            float d = ddx * ddx + ddz * ddz;
                            if (d < best) { best = d; idx = i; }
                        }
                    }
                return idx < 0 ? 1e4f : Mathf.Sqrt(best);
            }
        }

        static Nearest nearest;
        static Vector3 trackCenter;

        public static float TerrainHeight(World w, float x, float z)
        {
            var t = w.Track;
            var def = w.Def;
            float hw = t.HalfWidth;
            float natural = (Noise.Fbm2(x / 150f + 3.1f, z / 150f + 7.7f, 4, def.Seed) - 0.5f) * 18f
                          + (Noise.Fbm2(x / 38f, z / 38f, 3, def.Seed + 5) - 0.5f) * 4f + 1.5f;
            float dc = new Vector2(x - trackCenter.x, z - trackCenter.z).magnitude;
            natural += Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(170f, 300f, dc)) * 30f;

            float d = nearest.Query(x, z, out int idx);
            float h = natural;
            if (idx >= 0)
            {
                float edgeDrop = Mathf.Abs(t.Right[idx].y) * (hw + EdgeOffset + 2f);
                float roadEdge = t.Pos[idx].y - edgeDrop - 0.45f;
                float wgt = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(hw + EdgeOffset + 2.4f, hw + 34f, d));
                if (natural < roadEdge) wgt = Mathf.Min(1f, wgt * 1.4f);   // fall off embankments a bit faster
                h = Mathf.Lerp(roadEdge, natural, wgt);
            }
            if (def.LakeRadius > 0f)
            {
                float dl = new Vector2(x - def.LakeCenter.x, z - def.LakeCenter.z).magnitude;
                float mask = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(def.LakeRadius * 0.55f, def.LakeRadius + 9f, dl));
                h = Mathf.Lerp(h, def.LakeCenter.y - 2.6f, mask);
            }
            return h;
        }

        static void BuildTerrain(World w)
        {
            var t = w.Track;
            var s = w.Style;
            nearest = new Nearest(t);
            Vector3 min = t.Pos[0], max = t.Pos[0];
            foreach (var p in t.Pos) { min = Vector3.Min(min, p); max = Vector3.Max(max, p); }
            trackCenter = (min + max) * 0.5f;
            const float margin = 230f;
            const float cell = 4f;
            float x0 = min.x - margin, z0 = min.z - margin;
            int nx = Mathf.CeilToInt((max.x - min.x + margin * 2f) / cell) + 1;
            int nz = Mathf.CeilToInt((max.z - min.z + margin * 2f) / cell) + 1;

            var heights = new float[nx, nz];
            for (int ix = 0; ix < nx; ix++)
                for (int iz = 0; iz < nz; iz++)
                    heights[ix, iz] = TerrainHeight(w, x0 + ix * cell, z0 + iz * cell);

            var b = new MeshBuilder();
            float water = w.Def.LakeRadius > 0f ? w.Def.LakeCenter.y : -99f;
            for (int iz = 0; iz < nz; iz++)
                for (int ix = 0; ix < nx; ix++)
                {
                    float h = heights[ix, iz];
                    float hl = heights[Mathf.Max(ix - 1, 0), iz], hr = heights[Mathf.Min(ix + 1, nx - 1), iz];
                    float hd = heights[ix, Mathf.Max(iz - 1, 0)], hu = heights[ix, Mathf.Min(iz + 1, nz - 1)];
                    Vector3 nrm = new Vector3(hl - hr, 2f * cell, hd - hu).normalized;
                    float x = x0 + ix * cell, z = z0 + iz * cell;
                    float n1 = Noise.Fbm2(x / 30f, z / 30f, 3, 77);
                    Color c = Color.Lerp(s.Grass, s.GrassAlt, Mathf.SmoothStep(0.3f, 0.7f, n1));
                    if (!s.Snowy && Noise.Value2(x / 9f, z / 9f, 91) > 0.82f) c = Color.Lerp(c, new Color(0.95f, 0.85f, 0.35f), 0.35f);  // flower meadows
                    float steep = 1f - nrm.y;
                    c = Color.Lerp(c, s.Rock, Mathf.SmoothStep(0.18f, 0.4f, steep));
                    if (h < water + 0.7f) c = Color.Lerp(c, s.Sand, Mathf.Clamp01((water + 0.7f - h) * 1.2f));
                    if (!s.Snowy && h > 26f) c = Color.Lerp(c, s.MountainCap, Mathf.InverseLerp(26f, 34f, h));
                    b.Vert(new Vector3(x, h, z), nrm, c);
                }
            for (int iz = 0; iz < nz - 1; iz++)
                for (int ix = 0; ix < nx - 1; ix++)
                {
                    int a = iz * nx + ix, bb = a + 1, c = a + nx, d = c + 1;
                    b.Tri(a, c, bb); b.Tri(bb, c, d);
                }
            var mat = GroundMaterial(s, "Terrain", Color.white);
            var go = b.Build("Terrain", w.Root, mat, false);
            go.GetComponent<MeshRenderer>().receiveShadows = true;
            var mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
            mc.sharedMaterial = Mats.Slick;

            BuildMountains(w, min, max);
        }

        static void BuildMountains(World w, Vector3 min, Vector3 max)
        {
            var s = w.Style;
            var b = new MeshBuilder();
            var rng = new Rng(w.Def.Seed * 7 + 3);
            float radius = Mathf.Max(max.x - min.x, max.z - min.z) * 0.5f + 300f;
            int count = 26;
            for (int i = 0; i < count; i++)
            {
                float a = i * Mathf.PI * 2f / count + rng.Range(-0.08f, 0.08f);
                float r = radius + rng.Range(0f, 90f);
                Vector3 c = trackCenter + new Vector3(Mathf.Cos(a) * r, -10f, Mathf.Sin(a) * r);
                float sw = rng.Range(70f, 130f), sh = rng.Range(55f, 120f);
                float cap = sh * rng.Range(0.45f, 0.65f) - 10f;
                b.Blob(c, new Vector3(sw, sh, sw * rng.Range(0.7f, 1f)), 2, 0.22f, 1.7f, rng.Range(0, 999), (p, n) =>
                {
                    float y = p.y;
                    Color col = Color.Lerp(s.Mountain * 0.85f, s.Mountain * 1.1f, n.y * 0.5f + 0.5f);
                    if (y > cap && n.y > 0.25f) col = s.MountainCap;
                    return col;
                }, true, 0.6f);
            }
            var go = b.Build("Mountains", w.Root, Mats.VertexLit, false);
            go.GetComponent<MeshRenderer>().receiveShadows = false;
        }

        // ================================================================ lake + bridge

        static void BuildLake(World w)
        {
            var def = w.Def;
            var t = w.Track;
            var s = w.Style;
            var b = new MeshBuilder();
            float R = def.LakeRadius + 12f;
            int seg = 64, rings = 8;
            Vector3 c = new Vector3(def.LakeCenter.x, def.LakeCenter.y, def.LakeCenter.z);
            for (int r = 0; r <= rings; r++)
                for (int k = 0; k < seg; k++)
                {
                    float rr = R * r / rings;
                    float a = k * Mathf.PI * 2f / seg;
                    float depth = 1f - Mathf.Clamp01((rr - def.LakeRadius * 0.4f) / (def.LakeRadius * 0.7f));
                    b.Vert(c + new Vector3(Mathf.Cos(a) * rr, 0, Mathf.Sin(a) * rr), Vector3.up, new Color(depth, 0, 0, 1));
                }
            for (int r = 0; r < rings; r++)
                for (int k = 0; k < seg; k++)
                {
                    int a = r * seg + k, bb = r * seg + (k + 1) % seg, cc = a + seg, d = bb + seg;
                    b.TriFacing(a, cc, bb, Vector3.up); b.TriFacing(bb, cc, d, Vector3.up);
                }
            var mat = Mats.Water();
            mat.SetColor("_Shallow", s.Water);
            mat.SetColor("_Deep", s.WaterDeep);
            b.Build("Lake", w.Root, mat, false);

            // bridge deck sides, underside and pillars
            var deck = new MeshBuilder();
            float half = t.HalfWidth + EdgeOffset + 0.6f;
            Color concrete = new Color(0.72f, 0.7f, 0.66f);
            for (int i = 0; i < t.Count; i++)
            {
                if (!t.Bridge[i]) continue;
                int j = t.Wrap(i + 1);
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 a0 = t.Pos[i] + t.Right[i] * side * half, a1 = t.Pos[j] + t.Right[j] * side * half;
                    deck.Quad(a0 - Vector3.up * 1.4f, a1 - Vector3.up * 1.4f, a1, a0, concrete);
                    deck.Quad(a1 - Vector3.up * 1.4f, a0 - Vector3.up * 1.4f, a0, a1, concrete);
                }
                Vector3 l0 = t.Pos[i] - t.Right[i] * half - Vector3.up * 1.4f, r0 = t.Pos[i] + t.Right[i] * half - Vector3.up * 1.4f;
                Vector3 l1 = t.Pos[j] - t.Right[j] * half - Vector3.up * 1.4f, r1 = t.Pos[j] + t.Right[j] * half - Vector3.up * 1.4f;
                deck.Quad(l0, r0, r1, l1, concrete * 0.8f);
                deck.Quad(l1, r1, r0, l0, concrete * 0.8f);
                if (i % 14 == 0)
                {
                    deck.Gloss = 0.1f;
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Vector3 top = t.Pos[i] + t.Right[i] * side * (t.HalfWidth - 1f) - Vector3.up * 1.3f;
                        deck.Cylinder(new Vector3(top.x, def.LakeCenter.y - 3f, top.z), top, 0.85f, 12, concrete);
                    }
                    deck.Gloss = 0f;
                }
            }
            if (deck.VertexCount > 0) deck.Build("BridgeDeck", w.Root, Mats.VertexLit);
        }

        // ================================================================ tunnel

        static void BuildTunnels(World w)
        {
            var t = w.Track;
            var s = w.Style;
            foreach (var tun in w.Def.Tunnels)
            {
                int a = t.IndexAtT(tun.x), bIdx = t.IndexAtT(tun.y);
                var idx = new List<int>();
                for (int i = a; i != bIdx; i = t.Wrap(i + 1)) if ((i - a + t.Count) % 2 == 0) idx.Add(i);
                idx.Add(bIdx);

                float ix = t.HalfWidth + EdgeOffset + 0.7f, iy = 7.5f;
                float ox = ix + 9f, oy = iy + 5.5f;
                const int arcSeg = 18;
                var inner = new List<Vector3[]>();
                var outer = new List<Vector3[]>();
                foreach (int i in idx)
                {
                    Vector3 p = t.Pos[i], r = t.FlatRight[i];
                    var ringIn = new Vector3[arcSeg + 1];
                    var ringOut = new Vector3[arcSeg + 5];
                    for (int k = 0; k <= arcSeg; k++)
                    {
                        float ang = Mathf.PI * k / arcSeg;
                        ringIn[k] = p + r * (-Mathf.Cos(ang) * ix) + Vector3.up * (Mathf.Sin(ang) * iy - 0.3f);
                    }
                    ringOut[0] = p - r * (ox + 7f) + Vector3.up * -3f;
                    ringOut[1] = p - r * (ox + 2f) + Vector3.up * -0.5f;
                    for (int k = 0; k <= arcSeg; k++)
                    {
                        float ang = Mathf.PI * k / arcSeg;
                        Vector3 q = p + r * (-Mathf.Cos(ang) * ox) + Vector3.up * (Mathf.Sin(ang) * oy);
                        float nz = Noise.Value3(q * 0.18f) - 0.5f;
                        ringOut[k + 2] = q + (q - p).normalized * nz * 3f;
                    }
                    ringOut[arcSeg + 3] = p + r * (ox + 2f) + Vector3.up * -0.5f;
                    ringOut[arcSeg + 4] = p + r * (ox + 7f) + Vector3.up * -3f;
                    inner.Add(ringIn);
                    outer.Add(ringOut);
                }
                var tb = new MeshBuilder();
                tb.Gloss = 0.05f;
                Color wall = s.Snowy ? new Color(0.62f, 0.8f, 0.95f) : new Color(0.62f, 0.58f, 0.52f);
                tb.Loft(inner, (p, n) => wall * (0.85f + Noise.Value3(p * 0.7f) * 0.25f), false, false, false, true);
                tb.Loft(outer, (p, n) =>
                {
                    if (n.y > 0.55f) return s.Snowy ? s.MountainCap : s.Grass * (0.9f + Noise.Value3(p * 0.3f) * 0.2f);
                    return s.Rock * (0.85f + Noise.Value3(p * 0.5f) * 0.3f);
                }, false, false, false);
                // portal faces joining inner and outer shells
                foreach (bool atStart in new[] { true, false })
                {
                    var ri = atStart ? inner[0] : inner[inner.Count - 1];
                    var ro = atStart ? outer[0] : outer[outer.Count - 1];
                    int ti = atStart ? idx[0] : idx[idx.Count - 1];
                    Vector3 facing = atStart ? -t.Fwd[ti] : t.Fwd[ti];
                    Color stone = s.Snowy ? new Color(0.75f, 0.85f, 0.95f) : new Color(0.58f, 0.55f, 0.5f);
                    for (int k = 0; k < arcSeg; k++)
                    {
                        Vector3 i0 = ri[k], i1 = ri[k + 1], o0 = ro[k + 2], o1 = ro[k + 3];
                        int v0 = tb.Vert(i0, facing, stone), v1 = tb.Vert(i1, facing, stone), v2 = tb.Vert(o1, facing, stone), v3 = tb.Vert(o0, facing, stone);
                        tb.TriFacing(v0, v1, v2, facing); tb.TriFacing(v0, v2, v3, facing);
                    }
                    // stone frame
                    var frame = new List<Vector3>(ri);
                    tb.Tube(frame, 0.45f, 8, stone * 0.85f, true);
                }
                // ceiling lights
                tb.Emissive = 1f;
                for (int q = 0; q < idx.Count; q += 3)
                {
                    int i = idx[q];
                    tb.Box(t.Pos[i] + Vector3.up * (iy - 0.55f), new Vector3(0.9f, 0.08f, 0.9f), new Color(1f, 0.93f, 0.75f));
                    foreach (int side in new[] { -1, 1 })
                        tb.Box(t.Pos[i] + t.FlatRight[i] * side * (ix - 0.4f) + Vector3.up * 1.2f, new Vector3(0.15f, 0.12f, 1.2f),
                               s.Snowy ? new Color(0.5f, 0.9f, 1f) : new Color(1f, 0.6f, 0.2f));
                }
                tb.Emissive = 0f;
                tb.Build("Tunnel", w.Root, Mats.VertexLit);
            }
        }

        // ================================================================ start gate

        static void BuildStartGate(World w)
        {
            var t = w.Track;
            var s = w.Style;
            Vector3 p = t.Pos[0];
            Vector3 r = t.FlatRight[0];
            Vector3 f = new Vector3(t.Fwd[0].x, 0, t.Fwd[0].z).normalized;
            Quaternion rot = Quaternion.LookRotation(f);
            float span = t.HalfWidth + EdgeOffset + 1.4f;
            var b = new MeshBuilder();
            Color frame = new Color(0.2f, 0.22f, 0.28f);
            b.Gloss = 0.4f;
            foreach (int side in new[] { -1, 1 })
            {
                Vector3 c = p + r * side * span;
                b.Push(c, rot);
                b.RoundedBox(new Vector3(0, 4.4f, 0), new Vector3(1.3f, 8.8f, 1.3f), 0.2f, (q, n) => q.y > 7.8f || q.y < 0.6f ? s.BarrierA : frame, 2);
                b.Pop();
            }
            b.Push(p + Vector3.up * 8.2f, rot);
            b.RoundedBox(Vector3.zero, new Vector3(span * 2f + 1.3f, 1.8f, 1.0f), 0.2f, frame, 2);
            b.Pop();
            b.Build("StartGate", w.Root, Mats.VertexLit);

            // checkered banner faces
            var ban = new MeshBuilder();
            foreach (int dir in new[] { -1, 1 })
            {
                Vector3 fc = p + Vector3.up * 8.2f + f * dir * 0.52f;
                Vector3 left = fc - r * dir * span, right = fc + r * dir * span;
                ban.Quad(left - Vector3.up * 0.75f, right - Vector3.up * 0.75f, right + Vector3.up * 0.75f, left + Vector3.up * 0.75f, Color.white);
            }
            var bm = Mats.Lit(Color.white, 0.2f, ProcTex.Checker(), "Checker");
            bm.mainTextureScale = new Vector2(span * 2f / 1.5f, 2f);
            ban.Build("GateBanner", w.Root, bm, false);

            // start line
            var line = new MeshBuilder();
            float lh = t.HalfWidth;
            Vector3 u = t.Up[0] * 0.025f;
            line.Quad(p - t.Right[0] * lh - t.Fwd[0] * 1f + u, p + t.Right[0] * lh - t.Fwd[0] * 1f + u,
                      p + t.Right[0] * lh + t.Fwd[0] * 1f + u, p - t.Right[0] * lh + t.Fwd[0] * 1f + u, Color.white);
            var lm = Mats.Lit(Color.white, 0.2f, ProcTex.Checker(), "StartLine");
            lm.mainTextureScale = new Vector2(lh * 2f / 1f, 2f);
            var lineGo = line.Build("StartLine", w.Root, lm, false);
            lineGo.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;

            // light pod with 4 lamps (3 red + green) facing the grid
            var pod = new GameObject("StartLights").transform;
            pod.SetParent(w.Root, false);
            pod.SetPositionAndRotation(p + Vector3.up * 8.2f - f * 0.55f, Quaternion.LookRotation(-f));
            var lights = pod.gameObject.AddComponent<StartLights>();
            var lamps = new List<Renderer>();
            for (int k = 0; k < 4; k++)
            {
                var lb = new MeshBuilder();
                lb.Gloss = 0.8f;
                lb.Sphere(Vector3.zero, new Vector3(0.45f, 0.45f, 0.2f), 14, 10, Color.white);
                var go = lb.Build("Lamp" + k, pod, Mats.Lit(k < 3 ? new Color(0.35f, 0.05f, 0.05f) : new Color(0.05f, 0.3f, 0.08f), 0.8f, null, "Lamp"), false);
                go.transform.localPosition = new Vector3((k - 1.5f) * 1.2f, 0, 0);
                lamps.Add(go.GetComponent<Renderer>());
            }
            lights.lamps = lamps.ToArray();
            w.Lights = lights;
        }

        // ================================================================ pads, ramps, item boxes

        static void BuildBoostPads(World w)
        {
            var t = w.Track;
            var mat = Mats.Lit(Color.white, 0.4f, ProcTex.BoostPad(), "BoostPad");
            mat.SetVector("_Scroll", new Vector4(0f, -1.4f, 0, 0));
            mat.SetColor("_Emission", new Color(0.55f, 0.22f, 0.02f));
            int n = 0;
            foreach (float tt in w.Def.BoostPads)
            {
                int i = t.IndexAtT(tt);
                float lat = (n++ % 3 == 1) ? 3.2f : (n % 3 == 0 ? -3.2f : 0f);
                var b = new MeshBuilder();
                const int L = 4;
                for (int k = -L; k <= L; k++)
                {
                    int j = t.Wrap(i + k);
                    Vector3 c = t.Surface(j, lat) + t.Up[j] * 0.035f;
                    float v = (k + L) / (2f * L);
                    b.Vert(c - t.Right[j] * 2.2f, t.Up[j], Color.white, new Vector2(0, v * 1.4f));
                    b.Vert(c + t.Right[j] * 2.2f, t.Up[j], Color.white, new Vector2(1, v * 1.4f));
                }
                for (int k = 0; k < 2 * L; k++)
                {
                    int a = k * 2;
                    b.TriFacing(a, a + 2, a + 1, t.Up[i]); b.TriFacing(a + 1, a + 2, a + 3, t.Up[i]);
                }
                var go = b.Build("BoostPad", w.Root, mat, false);
                var trig = new GameObject("BoostTrigger");
                trig.transform.SetParent(go.transform, false);
                trig.transform.SetPositionAndRotation(t.Surface(i, lat) + t.Up[i] * 0.8f, t.RotationAt(i));
                var box = trig.AddComponent<BoxCollider>();
                box.isTrigger = true;
                box.size = new Vector3(4.4f, 2f, 8f);
                trig.AddComponent<BoostPad>();
            }
        }

        static void BuildRamps(World w)
        {
            var t = w.Track;
            var tex = ProcTex.Chevron();
            foreach (float tt in w.Def.Jumps)
            {
                int i0 = t.IndexAtT(tt);
                const int L = 9;
                const float H = 1.45f;
                float half = t.HalfWidth + CurbW;
                var top = new MeshBuilder();
                var side = new MeshBuilder();
                var col = new MeshBuilder();
                for (int k = 0; k <= L; k++)
                {
                    int j = t.Wrap(i0 + k);
                    float h = H * Mathf.Pow(k / (float)L, 1.7f);
                    Vector3 c = t.Pos[j] + t.Up[j] * h;
                    Vector3 l = c - t.Right[j] * half, r = c + t.Right[j] * half;
                    top.Vert(l, t.Up[j], Color.white, new Vector2(0, k / (float)L * 2f));
                    top.Vert(r, t.Up[j], Color.white, new Vector2(half * 2f / 4f, k / (float)L * 2f));
                    col.Vert(l, t.Up[j], Color.white);
                    col.Vert(r, t.Up[j], Color.white);
                }
                for (int k = 0; k < L; k++)
                {
                    int a = k * 2;
                    top.TriFacing(a, a + 2, a + 1, t.Up[i0]); top.TriFacing(a + 1, a + 2, a + 3, t.Up[i0]);
                    col.TriFacing(a, a + 2, a + 1, t.Up[i0]); col.TriFacing(a + 1, a + 2, a + 3, t.Up[i0]);
                }
                // side walls + back face
                int je = t.Wrap(i0 + L);
                Color dark = new Color(0.2f, 0.2f, 0.24f);
                side.Gloss = 0.3f;
                for (int k = 0; k < L; k++)
                {
                    int ja = t.Wrap(i0 + k), jb = t.Wrap(i0 + k + 1);
                    float ha = H * Mathf.Pow(k / (float)L, 1.7f), hb = H * Mathf.Pow((k + 1) / (float)L, 1.7f);
                    foreach (int sgn in new[] { -1, 1 })
                    {
                        Vector3 a = t.Pos[ja] + t.Right[ja] * sgn * half, b2 = t.Pos[jb] + t.Right[jb] * sgn * half;
                        side.DoubleQuad(a, b2, b2 + t.Up[jb] * hb, a + t.Up[ja] * ha, dark);
                    }
                }
                Vector3 bl = t.Pos[je] - t.Right[je] * half, br = t.Pos[je] + t.Right[je] * half;
                side.DoubleQuad(bl, br, br + t.Up[je] * H, bl + t.Up[je] * H, dark);
                // glowing lip
                side.Emissive = 0.9f;
                side.Box(t.Pos[je] + t.Up[je] * (H + 0.02f), new Vector3(0.1f, 0.1f, 0.1f), Color.white);
                side.Emissive = 0f;
                side.Push(t.Pos[je] + t.Up[je] * (H + 0.02f), t.RotationAt(je));
                side.Emissive = 0.8f;
                side.Box(Vector3.zero, new Vector3(half * 2f, 0.08f, 0.2f), new Color(1f, 0.85f, 0.2f));
                side.Emissive = 0f;
                side.Pop();

                var mat = Mats.Lit(Color.white, 0.3f, tex, "Ramp");
                top.Build("Ramp", w.Root, mat);
                side.Build("RampSides", w.Root, Mats.VertexLit);
                var cgo = new GameObject("RampCollider");
                cgo.transform.SetParent(w.Root, false);
                var mc = cgo.AddComponent<MeshCollider>();
                mc.sharedMesh = col.ToMesh("RampCollider");
                mc.sharedMaterial = Mats.Slick;
            }
        }

        static void BuildItemBoxes(World w)
        {
            var t = w.Track;
            foreach (float tt in w.Def.ItemRows)
            {
                int i = t.IndexAtT(tt);
                for (int k = -2; k <= 2; k++)
                {
                    Vector3 p = t.Surface(i, k * 2.7f) + t.Up[i] * 1.1f;
                    w.ItemBoxes.Add(ItemBox.Create(w.Root, p));
                }
            }
        }

        // ================================================================ scenery

        static void BuildScenery(World w, Rng rng)
        {
            var t = w.Track;
            var s = w.Style;
            var def = w.Def;
            float hw = t.HalfWidth;
            var chunks = new Dictionary<long, MeshBuilder>();
            MeshBuilder Chunk(Vector3 p)
            {
                long key = ((long)Mathf.FloorToInt(p.x / 90f) << 32) ^ (uint)Mathf.FloorToInt(p.z / 90f);
                if (!chunks.TryGetValue(key, out var mb)) chunks[key] = mb = new MeshBuilder();
                return mb;
            }
            var furniture = new MeshBuilder();
            var signs = new MeshBuilder();
            var signsL = new MeshBuilder();
            var boards = new List<MeshBuilder> { new MeshBuilder(), new MeshBuilder(), new MeshBuilder() };

            // --- trackside furniture
            float off = hw + EdgeOffset + 1.4f;
            for (int i = 0; i < t.Count; i++)
            {
                if (t.Tunnel[i] || t.Bridge[i]) continue;
                if (i % 46 == 10)
                {
                    int side = (i / 46) % 2 == 0 ? 1 : -1;
                    Vector3 p = t.Pos[i] + t.FlatRight[i] * side * (off + 0.6f);
                    p.y = t.Pos[i].y - 0.2f;
                    Props.LampPost(furniture, p, -t.FlatRight[i] * side, s.Snowy ? new Color(0.25f, 0.3f, 0.45f) : new Color(0.3f, 0.32f, 0.36f));
                }
                float c = t.Curv[i];
                if (Mathf.Abs(c) > 2.3f && i % 7 == 0)
                {
                    int side = c > 0 ? -1 : 1;           // outside of the corner
                    Vector3 p = t.Pos[i] + t.FlatRight[i] * side * (off + 1.2f);
                    p.y = t.Pos[i].y;
                    Props.ChevronSign(c > 0 ? signs : signsL, furniture, p, -t.FlatRight[i] * side, 3.2f);
                }
                if (i % 90 == 45 && t.MaxCurvAhead(i, -15f, 30f) < 0.8f && i > 60)
                {
                    int side = (i / 90) % 2 == 0 ? 1 : -1;
                    Vector3 p = t.Pos[i] + t.FlatRight[i] * side * (off + 3f);
                    p.y = t.Pos[i].y - 0.3f;
                    Props.Billboard(boards[(i / 90) % 3], furniture, p, -t.FlatRight[i] * side, 9f, 3f);
                }
            }
            // flags + grandstand along the start straight
            Vector3 f0 = new Vector3(t.Fwd[0].x, 0, t.Fwd[0].z).normalized;
            for (int k = -40; k <= 40; k += 10)
            {
                int i = t.Wrap(k);
                foreach (int side in new[] { -1, 1 })
                {
                    Vector3 p = t.Pos[i] + t.FlatRight[i] * side * (off + 0.8f);
                    p.y = t.Pos[i].y - 0.1f;
                    Props.FlagPole(furniture, p, rng.Pick(new[] { s.BarrierA, new Color(1f, 0.85f, 0.2f), new Color(0.2f, 0.6f, 1f), new Color(0.3f, 0.8f, 0.35f) }));
                }
            }
            {
                int i = t.Wrap(-6);
                Vector3 p = t.Pos[i] - t.FlatRight[i] * (off + 5.5f);
                p.y = t.Pos[i].y - 0.2f;
                Props.Grandstand(furniture, p, Quaternion.LookRotation(-t.FlatRight[i]), 46f, rng,
                    s.Snowy ? new Color(0.25f, 0.5f, 0.95f) : new Color(0.95f, 0.3f, 0.2f));
                int i2 = t.Wrap(20);
                Vector3 p2 = t.Pos[i2] + t.FlatRight[i2] * (off + 5.5f);
                p2.y = t.Pos[i2].y - 0.2f;
                Props.Grandstand(furniture, p2, Quaternion.LookRotation(t.FlatRight[i2]), 30f, rng,
                    new Color(1f, 0.8f, 0.2f));
            }

            // --- scatter
            Vector3 min = t.Pos[0], max = t.Pos[0];
            foreach (var p in t.Pos) { min = Vector3.Min(min, p); max = Vector3.Max(max, p); }
            float step = 7f;
            int houses = 0, windmills = 0;
            for (float x = min.x - 150f; x < max.x + 150f; x += step)
                for (float z = min.z - 150f; z < max.z + 150f; z += step)
                {
                    float px = x + rng.Range(-3f, 3f), pz = z + rng.Range(-3f, 3f);
                    float d = nearest.Query(px, pz, out int idx);
                    if (d < hw + EdgeOffset + 4.5f) continue;
                    if (idx >= 0 && t.Tunnel[idx] && d < hw + 22f) continue;
                    if (def.LakeRadius > 0f && new Vector2(px - def.LakeCenter.x, pz - def.LakeCenter.z).magnitude < def.LakeRadius + 6f) continue;
                    if (idx >= 0 && (idx < 50 || idx > t.Count - 50) && d < hw + 22f) continue;  // keep the grandstands clear
                    float density = d < 40f ? 0.55f : (d < 90f ? 0.3f : 0.12f);
                    if (!rng.Chance(density)) continue;
                    float y = TerrainHeight(w, px, pz);
                    Vector3 pos = new Vector3(px, y - 0.1f, pz);
                    var mb = Chunk(pos);
                    float roll = rng.Value;
                    if (s.Snowy)
                    {
                        if (roll < 0.62f) Props.PineTree(mb, pos, rng.Range(6f, 12f), rng, rng.Pick(new[] { s.Leaf, s.LeafAlt }), true);
                        else if (roll < 0.75f) Props.Rock(mb, pos, rng.Range(1f, 3f), rng, s.Rock, true);
                        else if (roll < 0.83f && d < 40f) Props.Snowman(mb, pos, rng.Range(0.9f, 1.4f), rng);
                        else if (roll < 0.9f) Props.IceCrystal(mb, pos, rng.Range(1.5f, 3.5f), rng);
                        else if (roll < 0.93f && d > 30f && d < 70f && houses < 8) { Props.Cabin(mb, pos, rng.Range(0f, 360f), rng); houses++; }
                        else Props.Bush(mb, pos, rng.Range(0.8f, 1.4f), rng, s.MountainCap);
                    }
                    else
                    {
                        if (roll < 0.34f) Props.RoundTree(mb, pos, rng.Range(6f, 10f), rng, rng.Pick(new[] { s.Leaf, s.LeafAlt, new Color(0.55f, 0.72f, 0.2f) }));
                        else if (roll < 0.6f) Props.PineTree(mb, pos, rng.Range(7f, 13f), rng, new Color(0.16f, 0.42f, 0.22f), false);
                        else if (roll < 0.74f) Props.Bush(mb, pos, rng.Range(0.8f, 1.5f), rng, s.LeafAlt,
                            rng.Chance(0.5f) ? rng.Pick(new[] { new Color(1f, 0.4f, 0.5f), new Color(1f, 0.9f, 0.3f), Color.white, new Color(0.7f, 0.5f, 1f) }) : (Color?)null);
                        else if (roll < 0.82f) Props.Rock(mb, pos, rng.Range(0.8f, 2.5f), rng, s.Rock, false);
                        else if (roll < 0.88f && d < 45f) Props.Mushroom(mb, pos, rng.Range(1.5f, 3.2f), rng);
                        else if (roll < 0.93f && d > 26f && d < 70f && houses < 12)
                        {
                            Vector3 toTrack = t.Pos[idx] - pos; toTrack.y = 0;
                            float yaw = Quaternion.LookRotation(toTrack).eulerAngles.y + rng.Range(-20f, 20f);
                            Props.House(mb, pos, yaw, rng, rng.Pick(new[] { new Color(0.98f, 0.93f, 0.82f), new Color(0.95f, 0.85f, 0.7f), new Color(0.85f, 0.92f, 0.98f) }),
                                rng.Pick(new[] { new Color(0.8f, 0.25f, 0.2f), new Color(0.25f, 0.4f, 0.75f), new Color(0.35f, 0.55f, 0.3f), new Color(0.9f, 0.55f, 0.2f) }), false);
                            houses++;
                        }
                        else if (roll < 0.94f && d > 45f && d < 110f && windmills < 3) { Props.Windmill(w.Root, pos, rng.Range(0f, 360f)); windmills++; }
                        else Props.Bush(mb, pos, rng.Range(0.6f, 1.1f), rng, s.Leaf);
                    }
                }

            foreach (var kv in chunks)
                kv.Value.Build("Scenery", w.Root, Mats.VertexLit);
            furniture.Build("Furniture", w.Root, Mats.VertexLit);
            if (signs.VertexCount > 0) signs.Build("ChevronsR", w.Root, Mats.Lit(Color.white, 0.2f, ProcTex.Chevron(), "ChevronR"), false);
            if (signsL.VertexCount > 0)
            {
                var ml = Mats.Lit(Color.white, 0.2f, ProcTex.Chevron(), "ChevronL");
                ml.mainTextureScale = new Vector2(-1f, 1f);
                signsL.Build("ChevronsL", w.Root, ml, false);
            }
            Color[,] boardCols =
            {
                { new Color(0.1f, 0.35f, 0.9f), new Color(1f, 0.85f, 0.2f) },
                { new Color(0.9f, 0.2f, 0.25f), Color.white },
                { new Color(0.2f, 0.7f, 0.35f), new Color(1f, 0.95f, 0.8f) },
            };
            for (int k = 0; k < boards.Count; k++)
                if (boards[k].VertexCount > 0)
                {
                    var m = Mats.Lit(Color.white, 0.3f, ProcTex.Banner(boardCols[k, 0], boardCols[k, 1]), "Board");
                    m.SetColor("_Emission", new Color(0.08f, 0.08f, 0.08f));
                    boards[k].Build("Billboards", w.Root, m, false);
                }

            // balloons drifting over the circuit
            if (!s.Snowy)
                for (int k = 0; k < 5; k++)
                {
                    Vector3 p = new Vector3(rng.Range(min.x, max.x), rng.Range(30f, 55f), rng.Range(min.z, max.z));
                    Props.HotAirBalloon(w.Root, p, rng);
                }
        }

        // ================================================================ minimap

        static void BuildMinimap(World w)
        {
            var t = w.Track;
            Vector3 min = t.Pos[0], max = t.Pos[0];
            foreach (var p in t.Pos) { min = Vector3.Min(min, p); max = Vector3.Max(max, p); }
            float size = Mathf.Max(max.x - min.x, max.z - min.z) + 30f;
            Vector2 c = new Vector2((min.x + max.x) * 0.5f, (min.z + max.z) * 0.5f);
            w.MapMin = c - Vector2.one * size * 0.5f;
            w.MapSize = size;
            const int S = 256;
            var dist = new float[S * S];
            for (int k = 0; k < dist.Length; k++) dist[k] = 1e9f;
            var pts = new Vector2[t.Count];
            for (int i = 0; i < t.Count; i++)
                pts[i] = (new Vector2(t.Pos[i].x, t.Pos[i].z) - w.MapMin) / size * S;
            for (int i = 0; i < t.Count; i++)
            {
                Vector2 a = pts[i], b = pts[(i + 1) % t.Count];
                int x0 = Mathf.Max(0, (int)Mathf.Min(a.x, b.x) - 8), x1 = Mathf.Min(S - 1, (int)Mathf.Max(a.x, b.x) + 8);
                int y0 = Mathf.Max(0, (int)Mathf.Min(a.y, b.y) - 8), y1 = Mathf.Min(S - 1, (int)Mathf.Max(a.y, b.y) + 8);
                for (int y = y0; y <= y1; y++)
                    for (int x = x0; x <= x1; x++)
                    {
                        float d = ProcTex.SegDist(new Vector2(x + 0.5f, y + 0.5f), a, b);
                        if (d < dist[y * S + x]) dist[y * S + x] = d;
                    }
            }
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Minimap" };
            var px = new Color32[S * S];
            for (int k = 0; k < px.Length; k++)
            {
                float d = dist[k];
                Color col = new Color(0, 0, 0, 0);
                float outline = Mathf.Clamp01(5.5f - d);
                float inner = Mathf.Clamp01(3.5f - d);
                col = Color.Lerp(col, new Color(0.05f, 0.08f, 0.15f, 0.85f), outline);
                col = Color.Lerp(col, new Color(0.95f, 0.97f, 1f, 1f), inner);
                px[k] = col;
            }
            tex.SetPixels32(px);
            tex.Apply();
            w.Minimap = tex;
        }
    }

    /// <summary>Countdown lamps on the start gate.</summary>
    public class StartLights : MonoBehaviour
    {
        public Renderer[] lamps;
        int lit = -1;
        bool green;

        public void Set(int redCount, bool go)
        {
            if (redCount == lit && go == green) return;
            lit = redCount; green = go;
            for (int k = 0; k < lamps.Length; k++)
            {
                var m = lamps[k].material;
                bool on = k < 3 ? (!go && k < redCount) : go;
                Color e = k < 3 ? new Color(3f, 0.25f, 0.15f) : new Color(0.3f, 3f, 0.6f);
                m.SetColor("_Emission", on ? e : Color.black);
            }
        }
    }

    /// <summary>Road booster strip (trigger).</summary>
    public class BoostPad : MonoBehaviour
    {
        void OnTriggerEnter(Collider other)
        {
            var kart = other.GetComponentInParent<KartController>();
            if (kart != null) kart.HitBoostPad();
        }
    }
}
