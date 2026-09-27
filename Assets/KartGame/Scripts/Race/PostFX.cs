using UnityEngine;

namespace KartGame
{
    /// <summary>Built-in pipeline image effect: bloom, speed radial blur, grading, vignette.</summary>
    [RequireComponent(typeof(Camera))]
    public class PostFX : MonoBehaviour
    {
        public float threshold = 1.15f;
        public float knee = 0.5f;
        public float bloomIntensity = 0.18f;
        [Range(0.25f, 2f)] public float exposure = 0.9f;
        public float speedBlur;
        public float saturation = 1.12f;
        public float contrast = 1.06f;
        public float vignette = 0.9f;
        public Color tint = new Color(1f, 0.99f, 0.97f);

        Material mat;

        void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            if (mat == null)
            {
                var sh = Shader.Find("Hidden/KartGame/PostFX");
                if (sh == null || !sh.isSupported) { Graphics.Blit(src, dst); return; }
                mat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
            }
            mat.SetVector("_Threshold", new Vector4(threshold, knee, 0, 0));
            mat.SetFloat("_BloomIntensity", bloomIntensity);
            mat.SetFloat("_Exposure", exposure);
            mat.SetFloat("_SpeedBlur", speedBlur);
            mat.SetFloat("_Saturation", saturation);
            mat.SetFloat("_Contrast", contrast);
            mat.SetFloat("_Vignette", vignette);
            mat.SetColor("_Tint", tint);

            var fmt = src.format;
            int w = Mathf.Max(1, src.width / 2), h = Mathf.Max(1, src.height / 2);
            var chain = new RenderTexture[4];
            chain[0] = RenderTexture.GetTemporary(w, h, 0, fmt);
            Graphics.Blit(src, chain[0], mat, 0);
            int count = 1;
            for (int i = 1; i < chain.Length; i++)
            {
                w /= 2; h /= 2;
                if (w < 8 || h < 8) break;
                chain[i] = RenderTexture.GetTemporary(w, h, 0, fmt);
                Graphics.Blit(chain[i - 1], chain[i], mat, 1);
                count++;
            }
            for (int i = count - 1; i > 0; i--)
                Graphics.Blit(chain[i], chain[i - 1], mat, 2);
            mat.SetTexture("_BloomTex", chain[0]);
            Graphics.Blit(src, dst, mat, 3);
            for (int i = 0; i < count; i++) RenderTexture.ReleaseTemporary(chain[i]);
        }

        void OnDestroy()
        {
            if (mat != null) Destroy(mat);
        }
    }
}
