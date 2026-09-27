using System.Collections.Generic;
using UnityEngine;

namespace KartGame
{
    /// <summary>Two-slot item inventory (item race). Ctrl uses the front slot.</summary>
    public class KartItems : MonoBehaviour
    {
        public const int MaxSlots = 2;
        public readonly List<ItemType> Slots = new List<ItemType>();
        public KartController Kart { get; private set; }
        public float IncomingMissile { get; set; }   // > 0 while a missile is homing on us
        public float RouletteTimer { get; private set; }
        public event System.Action<ItemType> OnUse;
        public event System.Action<ItemType> OnGet;

        void Awake() { Kart = GetComponent<KartController>(); }
        public void Bind(KartController kart) => Kart = kart;

        void Update()
        {
            if (IncomingMissile > 0f) IncomingMissile -= Time.deltaTime;
            if (RouletteTimer > 0f) RouletteTimer -= Time.deltaTime;
        }

        public bool HasRoom => Slots.Count < MaxSlots;

        public void Give(ItemType type)
        {
            if (!HasRoom || type == ItemType.None) return;
            Slots.Add(type);
            RouletteTimer = 0.6f;
            OnGet?.Invoke(type);
        }

        public ItemType Front => Slots.Count > 0 ? Slots[0] : ItemType.None;

        public void UseItem()
        {
            if (Slots.Count == 0 || RouletteTimer > 0f) return;
            var type = Slots[0];
            Slots.RemoveAt(0);
            ItemWorld.Use(Kart, type);
            OnUse?.Invoke(type);
        }

        /// <summary>Weighted roll: leaders get defensive items, trailers get catch-up items.</summary>
        public static ItemType Roll(int position, int racers)
        {
            float t = racers > 1 ? (position - 1f) / (racers - 1f) : 0.5f;
            var table = new (ItemType type, float lead, float back)[]
            {
                (ItemType.Booster, 15f, 32f),
                (ItemType.Missile, 5f, 30f),
                (ItemType.WaterBomb, 22f, 14f),
                (ItemType.Banana, 32f, 6f),
                (ItemType.Shield, 24f, 6f),
                (ItemType.Magnet, 2f, 18f),
            };
            float total = 0f;
            foreach (var e in table) total += Mathf.Lerp(e.lead, e.back, t);
            float roll = Random.value * total;
            foreach (var e in table)
            {
                roll -= Mathf.Lerp(e.lead, e.back, t);
                if (roll <= 0f) return e.type;
            }
            return ItemType.Booster;
        }
    }

    /// <summary>Spawns item effects into the world.</summary>
    public static class ItemWorld
    {
        public static void Use(KartController kart, ItemType type)
        {
            switch (type)
            {
                case ItemType.Booster:
                    kart.StartBoost(BoostKind.Item, 1.34f, 1.6f, 3f);
                    break;
                case ItemType.Shield:
                    kart.GiveShield(6f);
                    break;
                case ItemType.Magnet:
                    kart.StartBoost(BoostKind.Item, 1.2f, 2.6f, 1.5f);
                    Magnet.Attach(kart, FindTargetAhead(kart, 160f));
                    break;
                case ItemType.Missile:
                    Missile.Launch(kart, FindTargetAhead(kart, 300f));
                    break;
                case ItemType.WaterBomb:
                    WaterBomb.Throw(kart);
                    break;
                case ItemType.Banana:
                    Banana.Drop(kart);
                    break;
            }
        }

        /// <summary>The racer directly ahead in the standings (within range along the track).</summary>
        public static KartController FindTargetAhead(KartController kart, float range)
        {
            var rm = RaceManager.Instance;
            if (rm == null) return null;
            var me = rm.StateOf(kart);
            if (me == null) return null;
            KartController best = null;
            float bestGap = float.MaxValue;
            foreach (var r in rm.Racers)
            {
                if (r.kart == kart || r.Finished) continue;
                float gap = r.Progress - me.Progress;
                if (gap > 0.5f && gap < range && gap < bestGap) { bestGap = gap; best = r.kart; }
            }
            return best;
        }
    }

    // ================================================================ item box

    public class ItemBox : MonoBehaviour
    {
        Renderer[] renderers;
        Collider trigger;
        float respawn;

        public static ItemBox Create(Transform parent, Vector3 pos)
        {
            var go = new GameObject("ItemBox");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var vis = new GameObject("Cube");
            vis.transform.SetParent(go.transform, false);
            var b = new MeshBuilder();
            b.Gloss = 0.7f;
            b.RoundedBox(Vector3.zero, Vector3.one * 1.15f, 0.18f, Color.white, 3);
            var mat = Mats.Cached("ItemBoxMat", () =>
            {
                var m = Mats.Lit(Color.white, 0.7f, ProcTex.QuestionBox(), "ItemBox");
                m.SetColor("_Emission", new Color(0.18f, 0.2f, 0.35f));
                m.SetColor("_RimColor", new Color(0.6f, 0.85f, 1f, 0.8f));
                return m;
            });
            var mf = vis.AddComponent<MeshFilter>();
            mf.sharedMesh = Mats.CachedMesh("ItemBoxMesh", () => b.ToMesh("ItemBox"));
            var mr = vis.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            vis.transform.localRotation = Quaternion.Euler(20f, 0f, 35f);
            vis.AddComponent<Spinner>().speed = new Vector3(0f, 90f, 0f);

            // glow halo
            var halo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.Destroy(halo.GetComponent<Collider>());
            halo.transform.SetParent(go.transform, false);
            halo.transform.localScale = Vector3.one * 2.4f;
            halo.GetComponent<Renderer>().sharedMaterial = Mats.Cached("ItemHalo", () => Mats.Particle(ProcTex.SoftDot(), true, new Color(0.5f, 0.75f, 1f, 0.55f)));
            halo.AddComponent<Billboard>();

            var col = go.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 1.3f;
            var box = go.AddComponent<ItemBox>();
            box.renderers = go.GetComponentsInChildren<Renderer>();
            box.trigger = col;
            return box;
        }

        void Update()
        {
            if (respawn > 0f)
            {
                respawn -= Time.deltaTime;
                if (respawn <= 0f)
                {
                    foreach (var r in renderers) r.enabled = true;
                    trigger.enabled = true;
                    transform.localScale = Vector3.one * 0.2f;
                }
            }
            if (transform.localScale.x < 1f)
                transform.localScale = Vector3.one * Mathf.MoveTowards(transform.localScale.x, 1f, Time.deltaTime * 3f);
        }

        void OnTriggerEnter(Collider other)
        {
            var kart = other.GetComponentInParent<KartController>();
            if (kart == null || respawn > 0f) return;
            var items = kart.Items;
            var rm = RaceManager.Instance;
            if (items != null && items.HasRoom && rm != null)
            {
                var st = rm.StateOf(kart);
                items.Give(KartItems.Roll(st != null ? rm.GetPosition(st) : 3, rm.Racers.Count));
            }
            foreach (var r in renderers) r.enabled = false;
            trigger.enabled = false;
            respawn = 2.5f;
            Fx.Burst(transform.position, new Color(0.5f, 0.8f, 1f), 26, 7f);
            GameAudio.Play3D(Sfx.ItemPickup, transform.position, 0.8f);
        }
    }

    /// <summary>Faces the main camera.</summary>
    public class Billboard : MonoBehaviour
    {
        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam != null) transform.rotation = cam.transform.rotation;
        }
    }

    // ================================================================ missile

    public class Missile : MonoBehaviour
    {
        KartController owner, target;
        Vector3 velocity;
        float life = 6f;
        int trackIdx;

        public static void Launch(KartController owner, KartController target)
        {
            var go = new GameObject("Missile");
            go.transform.position = owner.transform.position + Vector3.up * 1.1f + owner.transform.forward * 1.8f;
            go.transform.rotation = owner.transform.rotation;
            var b = new MeshBuilder();
            b.Gloss = 0.7f;
            b.Tube(new[] { new Vector3(0, 0, -0.55f), new Vector3(0, 0, 0.35f) }, 0.16f, 12, new Color(0.95f, 0.2f, 0.15f));
            b.Cone(new Vector3(0, 0, 0.35f), new Vector3(0, 0, 0.75f), 0.16f, 12, Color.white);
            for (int k = 0; k < 4; k++)
            {
                b.Push(Vector3.zero, Quaternion.Euler(0, 0, k * 90f));
                b.Box(new Vector3(0, 0.22f, -0.45f), new Vector3(0.03f, 0.18f, 0.25f), new Color(0.25f, 0.25f, 0.3f));
                b.Pop();
            }
            b.Build("Body", go.transform, Mats.VertexLit, false);
            Fx.AttachTrail(go.transform, new Vector3(0, 0, -0.6f), new Color(1f, 0.6f, 0.2f), 0.3f, 0.35f);
            var m = go.AddComponent<Missile>();
            m.owner = owner;
            m.target = target;
            m.velocity = owner.transform.forward * Mathf.Max(owner.Speed + 10f, 30f);
            var rm = RaceManager.Instance;
            m.trackIdx = rm != null ? rm.StateOf(owner).trackIndex : 0;
            if (target != null && target.Items != null) target.Items.IncomingMissile = 6f;
            GameAudio.Play3D(Sfx.Missile, go.transform.position, 1f);
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            life -= dt;
            var rm = RaceManager.Instance;
            Vector3 pos = transform.position;
            Vector3 aim;
            if (target != null && (target.transform.position - pos).sqrMagnitude < 30f * 30f)
                aim = target.transform.position + Vector3.up * 0.6f;
            else if (rm != null)
            {
                var t = rm.Track;
                trackIdx = t.FindNearest(pos, trackIdx, 12);
                aim = t.Pos[t.Wrap(trackIdx + 14)] + Vector3.up * 1.2f;
            }
            else aim = pos + velocity;
            float speed = 58f;
            Vector3 desired = (aim - pos).normalized * speed;
            velocity = Vector3.RotateTowards(velocity, desired, 5f * dt, 60f * dt);
            transform.position = pos + velocity * dt;
            transform.rotation = Quaternion.LookRotation(velocity);
            if (target != null && target.Items != null) target.Items.IncomingMissile = 0.5f;

            if (target != null && (target.transform.position + Vector3.up * 0.6f - transform.position).sqrMagnitude < 2.2f)
            {
                target.Hit(HitKind.Launch);
                Explode();
                return;
            }
            if (life <= 0f) Explode();
        }

        void Explode()
        {
            Fx.Explosion(transform.position);
            GameAudio.Play3D(Sfx.Explosion, transform.position, 1f);
            Destroy(gameObject);
        }
    }

    // ================================================================ water bomb

    public class WaterBomb : MonoBehaviour
    {
        KartController owner;
        Vector3 velocity;
        float life = 4f;

        public static void Throw(KartController owner)
        {
            var go = new GameObject("WaterBomb");
            go.transform.position = owner.transform.position + Vector3.up * 1.4f + owner.transform.forward * 1.5f;
            var b = new MeshBuilder();
            b.Gloss = 0.9f;
            b.Sphere(Vector3.zero, 0.45f, 16, 12, new Color(0.25f, 0.6f, 1f));
            b.Cone(new Vector3(0, 0.4f, 0), new Vector3(0, 0.7f, 0), 0.1f, 8, new Color(0.25f, 0.6f, 1f));
            b.Build("Ball", go.transform, Mats.VertexLit, false);
            var wb = go.AddComponent<WaterBomb>();
            wb.owner = owner;
            wb.velocity = owner.transform.forward * (Mathf.Max(owner.ForwardSpeed, 0f) + 16f) + Vector3.up * 7f;
            GameAudio.Play3D(Sfx.Throw, go.transform.position, 0.9f);
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            life -= dt;
            velocity += Vector3.down * 22f * dt;
            Vector3 p = transform.position;
            Vector3 next = p + velocity * dt;
            if (Physics.Raycast(p, (next - p).normalized, out var hit, (next - p).magnitude + 0.4f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                || life <= 0f)
            {
                WaterZone.Spawn(hit.collider != null ? hit.point : p, owner);
                Destroy(gameObject);
                return;
            }
            transform.position = next;
            transform.Rotate(200f * dt, 0f, 0f);
            var rm = RaceManager.Instance;
            if (rm != null)
                foreach (var r in rm.Racers)
                    if (r.kart != owner && (r.kart.transform.position + Vector3.up * 0.6f - next).sqrMagnitude < 2f)
                    {
                        WaterZone.Spawn(r.kart.transform.position, owner);
                        Destroy(gameObject);
                        return;
                    }
        }
    }

    public class WaterZone : MonoBehaviour
    {
        float life = 3.2f;
        KartController owner;
        float ownerGrace = 0.8f;
        readonly HashSet<KartController> caught = new HashSet<KartController>();

        public static void Spawn(Vector3 pos, KartController owner)
        {
            var go = new GameObject("WaterZone");
            go.transform.position = pos;
            var b = new MeshBuilder();
            b.Sphere(Vector3.zero, new Vector3(3.2f, 2.6f, 3.2f), 20, 12, new Color(0.5f, 0.8f, 1f));
            b.Build("Dome", go.transform, Mats.Cached("WaterDome", () => Mats.Particle(ProcTex.Bubble(), false, new Color(0.55f, 0.85f, 1f, 0.55f))), false);
            var z = go.AddComponent<WaterZone>();
            z.owner = owner;
            go.transform.localScale = Vector3.one * 0.2f;
            Fx.Burst(pos + Vector3.up, new Color(0.5f, 0.8f, 1f), 40, 9f);
            GameAudio.Play3D(Sfx.Splash, pos, 1f);
        }

        void Update()
        {
            life -= Time.deltaTime;
            ownerGrace -= Time.deltaTime;
            float s = Mathf.Min(1f, transform.localScale.x + Time.deltaTime * 5f);
            if (life < 0.4f) s = Mathf.Max(0.01f, life / 0.4f);
            transform.localScale = Vector3.one * s;
            var rm = RaceManager.Instance;
            if (rm != null)
                foreach (var r in rm.Racers)
                {
                    if (caught.Contains(r.kart) || (r.kart == owner && ownerGrace > 0f)) continue;
                    Vector3 d = r.kart.transform.position - transform.position;
                    if (new Vector2(d.x, d.z).magnitude < 3.3f && Mathf.Abs(d.y) < 3f)
                    {
                        caught.Add(r.kart);
                        if (r.kart.Hit(HitKind.Trap)) GameAudio.Play3D(Sfx.Splash, r.kart.transform.position, 0.8f);
                    }
                }
            if (life <= 0f) Destroy(gameObject);
        }
    }

    // ================================================================ banana

    public class Banana : MonoBehaviour
    {
        KartController owner;
        float grace = 0.8f, life = 40f;

        public static void Drop(KartController owner)
        {
            var go = new GameObject("Banana");
            Vector3 p = owner.transform.position - owner.transform.forward * 2.2f + Vector3.up * 0.5f;
            if (Physics.Raycast(p + Vector3.up, Vector3.down, out var hit, 5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                p = hit.point;
            go.transform.position = p;
            go.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            var mesh = Mats.CachedMesh("BananaMesh", () =>
            {
                var b = new MeshBuilder();
                var path = new List<Vector3>();
                var scales = new List<float>();
                for (int k = 0; k <= 12; k++)
                {
                    float a = Mathf.Lerp(-70f, 70f, k / 12f) * Mathf.Deg2Rad;
                    path.Add(new Vector3(Mathf.Sin(a) * 0.55f, 0.18f + (1f - Mathf.Cos(a)) * 0.45f, 0f));
                    scales.Add(Mathf.Lerp(0.35f, 1f, Mathf.Sin(k / 12f * Mathf.PI)));
                }
                var shape = MeshBuilder.Superellipse(0.24f, 0.24f, 2.4f, 10);
                b.Gloss = 0.5f;
                b.Sweep(path, shape, (q, nn) => new Color(1f, 0.85f, 0.15f), Vector3.forward, true, true, scales);
                b.Sphere(path[0], 0.05f, 6, 4, new Color(0.35f, 0.25f, 0.1f));
                b.Sphere(path[path.Count - 1], 0.05f, 6, 4, new Color(0.35f, 0.25f, 0.1f));
                return b.ToMesh("Banana");
            });
            var vis = new GameObject("Peel", typeof(MeshFilter), typeof(MeshRenderer));
            vis.transform.SetParent(go.transform, false);
            vis.GetComponent<MeshFilter>().sharedMesh = mesh;
            vis.GetComponent<MeshRenderer>().sharedMaterial = Mats.VertexLit;
            var ban = go.AddComponent<Banana>();
            ban.owner = owner;
            GameAudio.Play3D(Sfx.Drop, p, 0.8f);
        }

        void Update()
        {
            grace -= Time.deltaTime;
            life -= Time.deltaTime;
            if (life <= 0f) { Destroy(gameObject); return; }
            var rm = RaceManager.Instance;
            if (rm == null) return;
            foreach (var r in rm.Racers)
            {
                if (r.kart == owner && grace > 0f) continue;
                Vector3 d = r.kart.transform.position - transform.position;
                if (d.sqrMagnitude < 1.6f * 1.6f)
                {
                    if (r.kart.Hit(HitKind.Spin)) GameAudio.Play3D(Sfx.Spin, transform.position, 1f);
                    Destroy(gameObject);
                    return;
                }
            }
        }
    }

    // ================================================================ magnet (tractor beam toward the kart ahead)

    public class Magnet : MonoBehaviour
    {
        KartController kart, target;
        float life = 2.6f;
        LineRenderer line;

        public static void Attach(KartController kart, KartController target)
        {
            if (target == null) return;
            var m = kart.gameObject.AddComponent<Magnet>();
            m.kart = kart; m.target = target;
            var go = new GameObject("MagnetBeam");
            go.transform.SetParent(kart.transform, false);
            m.line = go.AddComponent<LineRenderer>();
            m.line.material = Mats.Cached("Beam", () => Mats.Particle(ProcTex.Streak(), true, new Color(1f, 0.4f, 0.4f, 0.8f)));
            m.line.widthMultiplier = 0.25f;
            m.line.positionCount = 2;
            GameAudio.Play3D(Sfx.Magnet, kart.transform.position, 0.8f);
        }

        void FixedUpdate()
        {
            life -= Time.fixedDeltaTime;
            if (life <= 0f || target == null) { if (line) Destroy(line.gameObject); Destroy(this); return; }
            Vector3 to = target.transform.position - kart.transform.position;
            to.y = 0f;
            if (to.magnitude < 3f) { life = 0f; return; }
            kart.Body.linearVelocity += to.normalized * 9f * Time.fixedDeltaTime;
            line.SetPosition(0, kart.transform.position + Vector3.up * 0.8f);
            line.SetPosition(1, target.transform.position + Vector3.up * 0.8f);
        }
    }
}
