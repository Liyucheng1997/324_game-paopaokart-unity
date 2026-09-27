using System.Collections.Generic;
using UnityEngine;

namespace KartGame
{
    /// <summary>Runtime material factory for the KartGame shaders (cached).</summary>
    public static class Mats
    {
        static Shader litShader, particleShader, waterShader, skyShader;
        static Material vertexLit;
        static PhysicsMaterial slick;
        static readonly Dictionary<string, Material> cache = new Dictionary<string, Material>();

        public static Shader LitShader
        {
            get
            {
                if (litShader == null) litShader = Shader.Find("KartGame/Lit");
                return litShader;
            }
        }

        public static Shader ParticleShader
        {
            get
            {
                if (particleShader == null) particleShader = Shader.Find("KartGame/Particle");
                return particleShader;
            }
        }

        /// <summary>Shared white material: all color comes from vertex colors.</summary>
        public static Material VertexLit
        {
            get
            {
                if (vertexLit == null) vertexLit = Lit(Color.white, 0f, null, "VertexLit");
                return vertexLit;
            }
        }

        public static PhysicsMaterial Slick
        {
            get
            {
                if (slick == null)
                    slick = new PhysicsMaterial("Slick")
                    {
                        dynamicFriction = 0f,
                        staticFriction = 0f,
                        bounciness = 0f,
                        frictionCombine = PhysicsMaterialCombine.Minimum,
                        bounceCombine = PhysicsMaterialCombine.Minimum,
                    };
                return slick;
            }
        }

        public static Material Lit(Color color, float gloss = 0.1f, Texture tex = null, string name = null)
        {
            var m = new Material(LitShader) { name = name ?? "Lit", color = color };
            m.SetFloat("_Gloss", gloss);
            if (tex != null) m.mainTexture = tex;
            return m;
        }

        public static Material Cached(string key, System.Func<Material> make)
        {
            if (cache.TryGetValue(key, out var m) && m != null) return m;
            m = make();
            cache[key] = m;
            return m;
        }

        static readonly Dictionary<string, Mesh> meshCache = new Dictionary<string, Mesh>();

        public static Mesh CachedMesh(string key, System.Func<Mesh> make)
        {
            if (meshCache.TryGetValue(key, out var m) && m != null) return m;
            m = make();
            meshCache[key] = m;
            return m;
        }

        public static Material Particle(Texture tex, bool additive, Color tint, float intensity = 1f, bool onTop = false)
        {
            var m = new Material(ParticleShader) { name = additive ? "ParticleAdd" : "ParticleAlpha" };
            m.mainTexture = tex;
            m.SetColor("_TintColor", tint);
            m.SetFloat("_Boost", intensity);
            m.SetFloat("_DstBlend", additive ? (float)UnityEngine.Rendering.BlendMode.One
                                             : (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZTest", onTop ? (float)UnityEngine.Rendering.CompareFunction.Always
                                       : (float)UnityEngine.Rendering.CompareFunction.LessEqual);
            return m;
        }

        public static Material Water()
        {
            if (waterShader == null) waterShader = Shader.Find("KartGame/Water");
            return new Material(waterShader) { name = "Water" };
        }

        public static Material Sky()
        {
            if (skyShader == null) skyShader = Shader.Find("KartGame/Sky");
            return new Material(skyShader) { name = "Sky" };
        }
    }
}
