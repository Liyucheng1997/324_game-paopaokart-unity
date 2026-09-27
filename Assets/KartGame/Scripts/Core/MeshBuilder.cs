using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace KartGame
{
    public delegate Color ColorFn(Vector3 localPos, Vector3 normal);

    /// <summary>
    /// Procedural mesh toolkit. Accumulates vertex-colored geometry under a
    /// transform stack: rounded boxes, lathes, lofts, sweeps/tubes, spheres,
    /// noise blobs. uv2.x carries per-vertex gloss for KartGame/Lit; a vertex
    /// alpha below 1 marks emissive parts.
    /// </summary>
    public class MeshBuilder
    {
        public readonly List<Vector3> V = new List<Vector3>();
        public readonly List<Vector3> N = new List<Vector3>();
        public readonly List<Color> C = new List<Color>();
        public readonly List<Vector2> UV = new List<Vector2>();
        public readonly List<Vector2> UV2 = new List<Vector2>();
        public readonly List<int> T = new List<int>();

        public float Gloss;
        public float Emissive;          // 0..1 -> vertex alpha 1..0.5 (x2 emission in the shader)

        Matrix4x4 xf = Matrix4x4.identity;
        Matrix4x4 nxf = Matrix4x4.identity;
        readonly Stack<Matrix4x4> stack = new Stack<Matrix4x4>();
        static readonly bool Linear = QualitySettings.activeColorSpace == ColorSpace.Linear;

        public int VertexCount => V.Count;

        // ------------------------------------------------------------ transform

        public void Push(Vector3 pos, Quaternion rot, Vector3 scale) => Push(Matrix4x4.TRS(pos, rot, scale));
        public void Push(Vector3 pos, Quaternion rot) => Push(Matrix4x4.TRS(pos, rot, Vector3.one));
        public void Push(Vector3 pos) => Push(Matrix4x4.Translate(pos));

        public void Push(Matrix4x4 m)
        {
            stack.Push(xf);
            xf = xf * m;
            nxf = xf.inverse.transpose;
        }

        public void Pop()
        {
            xf = stack.Pop();
            nxf = xf.inverse.transpose;
        }

        // ------------------------------------------------------------ raw

        public int Vert(Vector3 p, Vector3 n, Color c, Vector2 uv = default)
        {
            V.Add(xf.MultiplyPoint3x4(p));
            Vector3 wn = nxf.MultiplyVector(n);
            N.Add(wn.sqrMagnitude > 1e-12f ? wn.normalized : Vector3.up);
            Color cc = Linear ? c.linear : c;
            cc.a = Emissive > 0f ? 1f - Emissive * 0.5f : 1f;
            C.Add(cc);
            UV.Add(uv);
            UV2.Add(new Vector2(Gloss, 0f));
            return V.Count - 1;
        }

        public void Tri(int a, int b, int c) { T.Add(a); T.Add(b); T.Add(c); }

        /// <summary>Triangle whose winding is chosen so its face normal agrees with <paramref name="outward"/> (world).</summary>
        public void TriFacing(int a, int b, int c, Vector3 outward)
        {
            Vector3 fn = Vector3.Cross(V[b] - V[a], V[c] - V[a]);
            if (Vector3.Dot(fn, outward) >= 0f) Tri(a, b, c); else Tri(a, c, b);
        }

        /// <summary>Quad grid cell: (i,j) bottom-left, u to the right, v up as seen from the front.</summary>
        void Cell(int bl, int br, int tl, int tr)
        {
            Tri(bl, tl, br);
            Tri(tl, tr, br);
        }

        void CellFacing(int bl, int br, int tl, int tr)
        {
            Vector3 n = N[bl] + N[br] + N[tl] + N[tr];
            TriFacing(bl, tl, br, n);
            TriFacing(tl, tr, br, n);
        }

        // ------------------------------------------------------------ flat shapes

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color col)
        {
            // a b c d counter-clockwise when seen from the front
            Vector3 n = Vector3.Cross(b - a, d - a).normalized;
            n = -n; // convert CCW input to Unity front face
            int i0 = Vert(a, n, col, new Vector2(0, 0));
            int i1 = Vert(b, n, col, new Vector2(1, 0));
            int i2 = Vert(c, n, col, new Vector2(1, 1));
            int i3 = Vert(d, n, col, new Vector2(0, 1));
            Vector3 wn = xf.MultiplyVector(n);
            TriFacing(i0, i1, i2, wn);
            TriFacing(i0, i2, i3, wn);
        }

        public void DoubleQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color col)
        {
            Quad(a, b, c, d, col);
            Quad(d, c, b, a, col);
        }

        public void Box(Vector3 c, Vector3 size, Color col)
        {
            Vector3 h = size * 0.5f;
            for (int axis = 0; axis < 3; axis++)
                for (int s = -1; s <= 1; s += 2)
                {
                    Vector3 n = Vector3.zero; n[axis] = s;
                    Vector3 u = Vector3.zero; u[(axis + 1) % 3] = 1f;
                    Vector3 w = Vector3.zero; w[(axis + 2) % 3] = 1f;
                    Vector3 fc = c + Vector3.Scale(n, h);
                    Vector3 du = Vector3.Scale(u, h), dw = Vector3.Scale(w, h);
                    int i0 = Vert(fc - du - dw, n, col, new Vector2(0, 0));
                    int i1 = Vert(fc + du - dw, n, col, new Vector2(1, 0));
                    int i2 = Vert(fc + du + dw, n, col, new Vector2(1, 1));
                    int i3 = Vert(fc - du + dw, n, col, new Vector2(0, 1));
                    Vector3 wn = xf.MultiplyVector(n);
                    TriFacing(i0, i1, i2, wn);
                    TriFacing(i0, i2, i3, wn);
                }
        }

        public void Disc(Vector3 center, Vector3 normal, float radius, int seg, Color col)
        {
            Quaternion q = Quaternion.FromToRotation(Vector3.up, normal.normalized);
            int ci = Vert(center, normal, col, new Vector2(0.5f, 0.5f));
            int first = V.Count;
            for (int i = 0; i < seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg;
                Vector3 p = center + q * new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius);
                Vert(p, normal, col, new Vector2(Mathf.Cos(a) * 0.5f + 0.5f, Mathf.Sin(a) * 0.5f + 0.5f));
            }
            Vector3 wn = xf.MultiplyVector(normal);
            for (int i = 0; i < seg; i++)
                TriFacing(ci, first + i, first + (i + 1) % seg, wn);
        }

        // ------------------------------------------------------------ rounded box

        static List<float> RoundCoords(float h, float r, int rs)
        {
            var list = new List<float>();
            for (int k = 0; k <= rs; k++)
            {
                float phi = Mathf.PI * 0.25f * (rs - k) / rs;
                list.Add(-(h - r) - r * Mathf.Tan(phi));
            }
            if (h - r > 1e-4f)
            {
                for (int k = rs; k >= 0; k--)
                {
                    float phi = Mathf.PI * 0.25f * (rs - k) / rs;
                    list.Add((h - r) + r * Mathf.Tan(phi));
                }
            }
            else
            {
                for (int k = rs - 1; k >= 0; k--)
                {
                    float phi = Mathf.PI * 0.25f * (rs - k) / rs;
                    list.Add((h - r) + r * Mathf.Tan(phi));
                }
            }
            return list;
        }

        public void RoundedBox(Vector3 c, Vector3 size, float r, Color col, int rs = 3)
        {
            RoundedBox(c, size, r, (p, n) => col, rs);
        }

        public void RoundedBox(Vector3 c, Vector3 size, float r, ColorFn colFn, int rs = 3)
        {
            Vector3 h = size * 0.5f;
            r = Mathf.Min(r, Mathf.Min(h.x, Mathf.Min(h.y, h.z)) * 0.999f);
            r = Mathf.Max(r, 1e-4f);
            Vector3 inner = h - Vector3.one * r;
            var coords = new[] { RoundCoords(h.x, r, rs), RoundCoords(h.y, r, rs), RoundCoords(h.z, r, rs) };

            for (int axis = 0; axis < 3; axis++)
                for (int s = -1; s <= 1; s += 2)
                {
                    int ua = (axis + 1) % 3, va = (axis + 2) % 3;
                    var cu = coords[ua];
                    var cv = coords[va];
                    int baseIdx = V.Count;
                    for (int j = 0; j < cv.Count; j++)
                        for (int i = 0; i < cu.Count; i++)
                        {
                            Vector3 p = Vector3.zero;
                            p[axis] = s * h[axis];
                            p[ua] = cu[i];
                            p[va] = cv[j];
                            Vector3 inn = new Vector3(
                                Mathf.Clamp(p.x, -inner.x, inner.x),
                                Mathf.Clamp(p.y, -inner.y, inner.y),
                                Mathf.Clamp(p.z, -inner.z, inner.z));
                            Vector3 n = (p - inn).normalized;
                            Vector3 pos = inn + n * r;
                            Vert(c + pos, n, colFn(c + pos, n),
                                new Vector2((float)i / (cu.Count - 1), (float)j / (cv.Count - 1)));
                        }
                    Vector3 fn = Vector3.zero; fn[axis] = s;
                    Vector3 wfn = xf.MultiplyVector(fn);
                    for (int j = 0; j < cv.Count - 1; j++)
                        for (int i = 0; i < cu.Count - 1; i++)
                        {
                            int bl = baseIdx + j * cu.Count + i;
                            int br = bl + 1, tl = bl + cu.Count, tr = tl + 1;
                            TriFacing(bl, tl, br, wfn);
                            TriFacing(tl, tr, br, wfn);
                        }
                }
        }

        // ------------------------------------------------------------ lathe

        /// <summary>
        /// Revolves a (radius, height) profile around local Y. Repeat a point to
        /// get a crease. Profile should run bottom -> top for outward normals.
        /// </summary>
        public void Lathe(IList<Vector2> profile, int seg, Color col, float arcStart = 0f, float arcEnd = 360f)
        {
            Lathe(profile, seg, (p, n) => col, arcStart, arcEnd);
        }

        public void Lathe(IList<Vector2> profile, int seg, ColorFn colFn, float arcStart = 0f, float arcEnd = 360f)
        {
            int m = profile.Count;
            var n2 = new Vector2[m];
            for (int i = 0; i < m; i++)
            {
                Vector2 t = Vector2.zero;
                bool dupPrev = i > 0 && (profile[i] - profile[i - 1]).sqrMagnitude < 1e-10f;
                bool dupNext = i < m - 1 && (profile[i + 1] - profile[i]).sqrMagnitude < 1e-10f;
                if (i > 0 && !dupPrev) t += (profile[i] - profile[i - 1]).normalized;
                if (i < m - 1 && !dupNext) t += (profile[i + 1] - profile[i]).normalized;
                if (t.sqrMagnitude < 1e-10f)
                {
                    if (dupPrev && i < m - 1) t = profile[i + 1] - profile[i];
                    else if (dupNext && i > 0) t = profile[i] - profile[i - 1];
                    else t = Vector2.up;
                }
                t.Normalize();
                n2[i] = new Vector2(t.y, -t.x);
            }

            int baseIdx = V.Count;
            float a0 = arcStart * Mathf.Deg2Rad, a1 = arcEnd * Mathf.Deg2Rad;
            for (int j = 0; j <= seg; j++)
            {
                float a = Mathf.Lerp(a0, a1, (float)j / seg);
                float ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                for (int i = 0; i < m; i++)
                {
                    Vector3 p = new Vector3(profile[i].x * ca, profile[i].y, profile[i].x * sa);
                    Vector3 n = new Vector3(n2[i].x * ca, n2[i].y, n2[i].x * sa);
                    Vert(p, n, colFn(p, n), new Vector2((float)j / seg, (float)i / (m - 1)));
                }
            }
            for (int j = 0; j < seg; j++)
                for (int i = 0; i < m - 1; i++)
                {
                    if ((profile[i + 1] - profile[i]).sqrMagnitude < 1e-10f) continue;
                    int bl = baseIdx + j * m + i;
                    int tl = bl + 1;
                    int br = bl + m;
                    int tr = br + 1;
                    CellFacing(bl, br, tl, tr);
                }
        }

        /// <summary>Lathe whose axis runs from a to b.</summary>
        public void LatheBetween(Vector3 a, Vector3 b, IList<Vector2> profileNormalized, int seg, Color col)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            var prof = new Vector2[profileNormalized.Count];
            for (int i = 0; i < prof.Length; i++)
                prof[i] = new Vector2(profileNormalized[i].x, profileNormalized[i].y * len);
            Push(a, Quaternion.FromToRotation(Vector3.up, d / Mathf.Max(len, 1e-5f)));
            Lathe(prof, seg, col);
            Pop();
        }

        public void Cylinder(Vector3 a, Vector3 b, float radius, int seg, Color col, bool caps = true)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            var prof = caps
                ? new[] { new Vector2(0, 0), new Vector2(radius, 0), new Vector2(radius, 0), new Vector2(radius, len), new Vector2(radius, len), new Vector2(0, len) }
                : new[] { new Vector2(radius, 0), new Vector2(radius, len) };
            Push(a, Quaternion.FromToRotation(Vector3.up, d / Mathf.Max(len, 1e-5f)));
            Lathe(prof, seg, col);
            Pop();
        }

        public void Cone(Vector3 baseCenter, Vector3 tip, float radius, int seg, Color col)
        {
            Vector3 d = tip - baseCenter;
            float len = d.magnitude;
            var prof = new[] { new Vector2(0, 0), new Vector2(radius, 0), new Vector2(radius, 0), new Vector2(0, len) };
            Push(baseCenter, Quaternion.FromToRotation(Vector3.up, d / Mathf.Max(len, 1e-5f)));
            Lathe(prof, seg, col);
            Pop();
        }

        public void Torus(Vector3 center, Quaternion rot, float R, float r, int segR, int segr, Color col)
        {
            var prof = new Vector2[segr + 1];
            for (int i = 0; i <= segr; i++)
            {
                float a = -Mathf.PI * 0.5f + i * Mathf.PI * 2f / segr;
                // profile loops around the tube: start at the bottom, go outward then up
                prof[i] = new Vector2(R + Mathf.Cos(a) * r, Mathf.Sin(a) * r);   // CCW -> outward normals
            }
            Push(center, rot);
            Lathe(prof, segR, col);
            Pop();
        }

        // ------------------------------------------------------------ spheres

        public void Sphere(Vector3 c, Vector3 radii, int lon, int lat, Color col)
        {
            Sphere(c, radii, lon, lat, (p, n) => col);
        }

        public void Sphere(Vector3 c, Vector3 radii, int lon, int lat, ColorFn colFn,
                           float latMin = -90f, float latMax = 90f, float lonMin = 0f, float lonMax = 360f)
        {
            int baseIdx = V.Count;
            for (int j = 0; j <= lon; j++)
            {
                float a = Mathf.Lerp(lonMin, lonMax, (float)j / lon) * Mathf.Deg2Rad;
                for (int i = 0; i <= lat; i++)
                {
                    float b = Mathf.Lerp(latMin, latMax, (float)i / lat) * Mathf.Deg2Rad;
                    Vector3 u = new Vector3(Mathf.Cos(b) * Mathf.Cos(a), Mathf.Sin(b), Mathf.Cos(b) * Mathf.Sin(a));
                    Vector3 p = Vector3.Scale(u, radii);
                    Vector3 n = new Vector3(u.x / radii.x, u.y / radii.y, u.z / radii.z).normalized;
                    Vert(c + p, n, colFn(c + p, n), new Vector2((float)j / lon, (float)i / lat));
                }
            }
            for (int j = 0; j < lon; j++)
                for (int i = 0; i < lat; i++)
                {
                    int bl = baseIdx + j * (lat + 1) + i;
                    int tl = bl + 1, br = bl + lat + 1, tr = br + 1;
                    CellFacing(bl, br, tl, tr);
                }
        }

        public void Sphere(Vector3 c, float r, int lon, int lat, Color col) => Sphere(c, Vector3.one * r, lon, lat, col);

        // ------------------------------------------------------------ loft / sweep

        /// <summary>
        /// Connects a sequence of rings (same point count, closed loops) into a
        /// smooth shell. Normals point away from each ring's centroid.
        /// </summary>
        public void Loft(IList<Vector3[]> rings, ColorFn colFn, bool capStart = true, bool capEnd = true, bool closedRing = true, bool invert = false)
        {
            int rc = rings.Count;
            int pc = rings[0].Length;
            int baseIdx = V.Count;
            var centroids = new Vector3[rc];
            for (int r = 0; r < rc; r++)
            {
                Vector3 s = Vector3.zero;
                foreach (var p in rings[r]) s += p;
                centroids[r] = s / pc;
            }

            for (int r = 0; r < rc; r++)
            {
                for (int k = 0; k < pc; k++)
                {
                    Vector3 p = rings[r][k];
                    int kp = closedRing ? (k - 1 + pc) % pc : Mathf.Max(k - 1, 0);
                    int kn = closedRing ? (k + 1) % pc : Mathf.Min(k + 1, pc - 1);
                    Vector3 du = rings[r][kn] - rings[r][kp];
                    Vector3 dv = rings[Mathf.Min(r + 1, rc - 1)][k] - rings[Mathf.Max(r - 1, 0)][k];
                    Vector3 n = Vector3.Cross(du, dv);
                    if (n.sqrMagnitude < 1e-12f) n = p - centroids[r];
                    n.Normalize();
                    if (Vector3.Dot(n, p - centroids[r]) < 0f) n = -n;
                    if (invert) n = -n;
                    Vert(p, n, colFn(p, n), new Vector2((float)k / pc, (float)r / (rc - 1)));
                }
                if (closedRing)
                {
                    // duplicate the seam vertex so UVs don't wrap badly
                    Vector3 p = rings[r][0];
                    Vert(p, N[baseIdx + r * (pc + 1)] , colFn(p, Vector3.zero), new Vector2(1f, (float)r / (rc - 1)));
                    // fix normal from world back to local is not needed: copy the world normal directly
                    N[N.Count - 1] = N[baseIdx + r * (pc + 1)];
                }
            }
            int stride = closedRing ? pc + 1 : pc;
            int cols = closedRing ? pc : pc - 1;
            for (int r = 0; r < rc - 1; r++)
                for (int k = 0; k < cols; k++)
                {
                    int bl = baseIdx + r * stride + k;
                    int br = bl + 1, tl = bl + stride, tr = tl + 1;
                    CellFacing(bl, br, tl, tr);
                }

            if (closedRing)
            {
                if (capStart) Cap(rings[0], centroids[0], centroids[0] - centroids[Mathf.Min(1, rc - 1)], colFn);
                if (capEnd) Cap(rings[rc - 1], centroids[rc - 1], centroids[rc - 1] - centroids[Mathf.Max(rc - 2, 0)], colFn);
            }
        }

        void Cap(Vector3[] ring, Vector3 center, Vector3 outward, ColorFn colFn)
        {
            Vector3 n = outward.normalized;
            int ci = Vert(center, n, colFn(center, n));
            int first = V.Count;
            foreach (var p in ring) Vert(p, n, colFn(p, n));
            Vector3 wn = xf.MultiplyVector(n);
            for (int k = 0; k < ring.Length; k++)
                TriFacing(ci, first + k, first + (k + 1) % ring.Length, wn);
        }

        /// <summary>Sweeps a 2D cross-section (x = side, y = up) along a path using parallel-transport frames.</summary>
        public void Sweep(IList<Vector3> path, IList<Vector2> shape, ColorFn colFn, Vector3 upHint,
                          bool capStart = true, bool capEnd = true, IList<float> scales = null,
                          bool closedShape = true, bool invert = false)
        {
            int n = path.Count;
            var rings = new List<Vector3[]>(n);
            Vector3 prevUp = upHint.normalized;
            for (int i = 0; i < n; i++)
            {
                Vector3 t = (path[Mathf.Min(i + 1, n - 1)] - path[Mathf.Max(i - 1, 0)]).normalized;
                Vector3 up = Vector3.ProjectOnPlane(prevUp, t);
                if (up.sqrMagnitude < 1e-6f) up = Vector3.ProjectOnPlane(Vector3.forward, t);
                up.Normalize();
                prevUp = up;
                Vector3 side = Vector3.Cross(up, t).normalized;
                float s = scales != null ? scales[i] : 1f;
                var ring = new Vector3[shape.Count];
                for (int k = 0; k < shape.Count; k++)
                    ring[k] = path[i] + (side * shape[k].x + up * shape[k].y) * s;
                rings.Add(ring);
            }
            Loft(rings, colFn, capStart, capEnd, closedShape, invert);
        }

        public void Tube(IList<Vector3> path, float radius, int sides, Color col, bool caps = true)
        {
            var shape = new Vector2[sides];
            for (int k = 0; k < sides; k++)
            {
                float a = k * Mathf.PI * 2f / sides;
                shape[k] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
            }
            Vector3 d = path[Mathf.Min(1, path.Count - 1)] - path[0];
            Vector3 hint = Mathf.Abs(Vector3.Dot(d.normalized, Vector3.up)) > 0.9f ? Vector3.forward : Vector3.up;
            Sweep(path, shape, (p, nn) => col, hint, caps, caps);
        }

        public static Vector2[] RoundRect(float w, float h, float r, int cornerSeg = 3)
        {
            // counter-clockwise rounded rectangle centered at the origin
            var pts = new List<Vector2>();
            r = Mathf.Min(r, Mathf.Min(w, h) * 0.5f * 0.999f);
            Vector2[] centers =
            {
                new Vector2(w * 0.5f - r, h * 0.5f - r), new Vector2(-w * 0.5f + r, h * 0.5f - r),
                new Vector2(-w * 0.5f + r, -h * 0.5f + r), new Vector2(w * 0.5f - r, -h * 0.5f + r),
            };
            for (int c = 0; c < 4; c++)
                for (int k = 0; k <= cornerSeg; k++)
                {
                    float a = (c * 90f + 90f * k / cornerSeg) * Mathf.Deg2Rad;
                    pts.Add(centers[c] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
                }
            return pts.ToArray();
        }

        public static Vector2[] Superellipse(float w, float h, float power, int count, float bottomFlat = 0f)
        {
            var pts = new Vector2[count];
            for (int k = 0; k < count; k++)
            {
                float a = k * Mathf.PI * 2f / count;
                float c = Mathf.Cos(a), s = Mathf.Sin(a);
                float x = Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), 2f / power) * w * 0.5f;
                float y = Mathf.Sign(s) * Mathf.Pow(Mathf.Abs(s), 2f / power) * h * 0.5f;
                if (y < 0f) y *= 1f - bottomFlat;
                pts[k] = new Vector2(x, y);
            }
            return pts;
        }

        // ------------------------------------------------------------ blobs (foliage / rocks)

        static readonly Dictionary<long, int> midCache = new Dictionary<long, int>();

        public void Blob(Vector3 c, Vector3 radii, int subdiv, float noiseAmp, float noiseFreq, int seed,
                         ColorFn colFn, bool flat = true, float squashBottom = 0f)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            float t = (1f + Mathf.Sqrt(5f)) / 2f;
            verts.AddRange(new[]
            {
                new Vector3(-1, t, 0), new Vector3(1, t, 0), new Vector3(-1, -t, 0), new Vector3(1, -t, 0),
                new Vector3(0, -1, t), new Vector3(0, 1, t), new Vector3(0, -1, -t), new Vector3(0, 1, -t),
                new Vector3(t, 0, -1), new Vector3(t, 0, 1), new Vector3(-t, 0, -1), new Vector3(-t, 0, 1),
            });
            for (int i = 0; i < verts.Count; i++) verts[i] = verts[i].normalized;
            tris.AddRange(new[]
            {
                0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
                3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1,
            });
            for (int s = 0; s < subdiv; s++)
            {
                midCache.Clear();
                var nt = new List<int>(tris.Count * 4);
                for (int i = 0; i < tris.Count; i += 3)
                {
                    int a = tris[i], b = tris[i + 1], cc = tris[i + 2];
                    int ab = Mid(verts, a, b), bc = Mid(verts, b, cc), ca = Mid(verts, cc, a);
                    nt.AddRange(new[] { a, ab, ca, b, bc, ab, cc, ca, bc, ab, bc, ca });
                }
                tris = nt;
            }

            var disp = new Vector3[verts.Count];
            for (int i = 0; i < verts.Count; i++)
            {
                Vector3 u = verts[i];
                float nz = Noise.Value3(u * noiseFreq + new Vector3(seed * 1.37f, seed * 2.11f, seed * 0.73f)) * 2f - 1f;
                Vector3 p = Vector3.Scale(u * (1f + nz * noiseAmp), radii);
                if (squashBottom > 0f && p.y < 0f) p.y *= 1f - squashBottom;
                disp[i] = p;
            }

            if (flat)
            {
                for (int i = 0; i < tris.Count; i += 3)
                {
                    Vector3 a = disp[tris[i]], b = disp[tris[i + 1]], d = disp[tris[i + 2]];
                    Vector3 n = Vector3.Cross(b - a, d - a).normalized;
                    Vector3 mid = (a + b + d) / 3f;
                    if (Vector3.Dot(n, mid) < 0f) n = -n;
                    Color col = colFn(c + mid, n);
                    int i0 = Vert(c + a, n, col), i1 = Vert(c + b, n, col), i2 = Vert(c + d, n, col);
                    TriFacing(i0, i1, i2, xf.MultiplyVector(n));
                }
            }
            else
            {
                var normals = new Vector3[disp.Length];
                for (int i = 0; i < tris.Count; i += 3)
                {
                    Vector3 a = disp[tris[i]], b = disp[tris[i + 1]], d = disp[tris[i + 2]];
                    Vector3 n = Vector3.Cross(b - a, d - a);
                    if (Vector3.Dot(n, a + b + d) < 0f) n = -n;
                    normals[tris[i]] += n; normals[tris[i + 1]] += n; normals[tris[i + 2]] += n;
                }
                int baseIdx = V.Count;
                for (int i = 0; i < disp.Length; i++)
                {
                    Vector3 n = normals[i].normalized;
                    Vert(c + disp[i], n, colFn(c + disp[i], n));
                }
                for (int i = 0; i < tris.Count; i += 3)
                {
                    Vector3 mid = (disp[tris[i]] + disp[tris[i + 1]] + disp[tris[i + 2]]);
                    TriFacing(baseIdx + tris[i], baseIdx + tris[i + 1], baseIdx + tris[i + 2], xf.MultiplyVector(mid));
                }
            }
        }

        static int Mid(List<Vector3> verts, int a, int b)
        {
            long key = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
            if (midCache.TryGetValue(key, out int idx)) return idx;
            verts.Add(((verts[a] + verts[b]) * 0.5f).normalized);
            idx = verts.Count - 1;
            midCache[key] = idx;
            return idx;
        }

        // ------------------------------------------------------------ output

        public void Append(MeshBuilder other)
        {
            int off = V.Count;
            V.AddRange(other.V); N.AddRange(other.N); C.AddRange(other.C);
            UV.AddRange(other.UV); UV2.AddRange(other.UV2);
            foreach (int t in other.T) T.Add(t + off);
        }

        public void Clear()
        {
            V.Clear(); N.Clear(); C.Clear(); UV.Clear(); UV2.Clear(); T.Clear();
            xf = Matrix4x4.identity; nxf = Matrix4x4.identity; stack.Clear();
        }

        public Mesh ToMesh(string name)
        {
            var mesh = new Mesh { name = name };
            if (V.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(V);
            mesh.SetNormals(N);
            mesh.SetColors(C);
            mesh.SetUVs(0, UV);
            mesh.SetUVs(1, UV2);
            mesh.SetTriangles(T, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Creates a GameObject with this mesh and material under parent.</summary>
        public GameObject Build(string name, Transform parent, Material mat, bool shadows = true, bool collider = false)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            var mesh = ToMesh(name);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            if (collider)
            {
                var mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = mesh;
                mc.sharedMaterial = Mats.Slick;
            }
            return go;
        }
    }
}
