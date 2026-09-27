using System.Collections.Generic;
using UnityEngine;

namespace KartGame
{
    /// <summary>
    /// Immediate-mode UI helpers on a virtual 1080-px-tall canvas: rounded
    /// 9-slice panels, outlined text, hover buttons, bars and rotated quads.
    /// </summary>
    public static class UIKit
    {
        public static float Scale { get; private set; } = 1f;
        public static float W { get; private set; } = 1920f;
        public const float H = 1080f;

        static GUIStyle panelStyle, outlineStyle, buttonHit;
        static readonly Dictionary<long, GUIStyle> textStyles = new Dictionary<long, GUIStyle>();
        static Texture2D white;

        public static readonly Color Accent = new Color(1f, 0.78f, 0.15f);
        public static readonly Color Blue = new Color(0.25f, 0.62f, 1f);
        public static readonly Color PanelDark = new Color(0.06f, 0.09f, 0.18f, 0.82f);

        public static Texture2D White => white != null ? white : (white = ProcTex.Solid(Color.white));

        public static void Begin()
        {
            Scale = Screen.height / H;
            W = Screen.width / Scale;
            GUI.matrix = Matrix4x4.Scale(new Vector3(Scale, Scale, 1f));
            if (panelStyle == null)
            {
                panelStyle = new GUIStyle { border = new RectOffset(18, 18, 18, 18) };
                panelStyle.normal.background = ProcTex.RoundRect("panel", 64, 18f, Color.white, Color.white, 0f);
                outlineStyle = new GUIStyle { border = new RectOffset(18, 18, 18, 18) };
                outlineStyle.normal.background = ProcTex.RoundRect("outline", 64, 18f, new Color(1, 1, 1, 0), Color.white, 3f);
                buttonHit = new GUIStyle();
            }
        }

        public static Vector2 Mouse => Event.current.mousePosition;

        public static GUIStyle Style(int size, TextAnchor align, FontStyle fs = FontStyle.Bold)
        {
            long key = size * 100 + (int)align * 10 + (int)fs;
            if (!textStyles.TryGetValue(key, out var st))
            {
                st = new GUIStyle { fontSize = size, alignment = align, fontStyle = fs, wordWrap = false, clipping = TextClipping.Overflow };
                st.normal.textColor = Color.white;
                st.richText = false;
                textStyles[key] = st;
            }
            return st;
        }

        public static void Text(Rect r, string text, int size, Color color, TextAnchor align = TextAnchor.MiddleLeft,
                                float outline = 2f, FontStyle fs = FontStyle.Bold)
        {
            var st = Style(size, align, fs);
            Color prev = GUI.color;
            if (outline > 0f)
            {
                GUI.color = new Color(0.02f, 0.03f, 0.08f, 0.85f * color.a);
                GUI.Label(new Rect(r.x + outline, r.y + outline * 1.3f, r.width, r.height), text, st);
                GUI.Label(new Rect(r.x - outline * 0.6f, r.y + outline * 0.4f, r.width, r.height), text, st);
                GUI.Label(new Rect(r.x + outline * 0.6f, r.y - outline * 0.4f, r.width, r.height), text, st);
            }
            GUI.color = color;
            GUI.Label(r, text, st);
            GUI.color = prev;
        }

        public static void Panel(Rect r, Color c)
        {
            if (Event.current.type != EventType.Repaint) return;
            Color prev = GUI.color;
            GUI.color = c;
            panelStyle.Draw(r, false, false, false, false);
            GUI.color = prev;
        }

        public static void Outline(Rect r, Color c)
        {
            if (Event.current.type != EventType.Repaint) return;
            Color prev = GUI.color;
            GUI.color = c;
            outlineStyle.Draw(r, false, false, false, false);
            GUI.color = prev;
        }

        public static void Rect(Rect r, Color c)
        {
            Color prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, White);
            GUI.color = prev;
        }

        public static void Tex(Rect r, Texture t, Color c)
        {
            if (t == null) return;
            Color prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, t, ScaleMode.StretchToFill, true);
            GUI.color = prev;
        }

        public static void Gradient(Rect r, Color bottom, Color top)
        {
            Color prev = GUI.color;
            GUI.color = bottom;
            GUI.DrawTexture(r, White);
            GUI.color = top;
            GUI.DrawTexture(r, ProcTex.VerticalGradient("uigrad", new Color(1, 1, 1, 0), Color.white));
            GUI.color = prev;
        }

        public static bool Hover(Rect r) => r.Contains(Mouse);

        /// <summary>Rounded button; returns true when clicked.</summary>
        public static bool Button(Rect r, string label, int size, bool selected = false, Color? accent = null, bool enabled = true)
        {
            Color ac = accent ?? Accent;
            bool hover = enabled && Hover(r);
            Color bg = selected ? ac : (hover ? new Color(0.22f, 0.3f, 0.5f, 0.95f) : new Color(0.1f, 0.14f, 0.26f, 0.9f));
            if (!enabled) bg = new Color(0.1f, 0.1f, 0.14f, 0.6f);
            Panel(r, bg);
            if (hover || selected) Outline(r, selected ? Color.white : new Color(ac.r, ac.g, ac.b, 0.9f));
            Color tc = selected ? new Color(0.08f, 0.08f, 0.15f) : (enabled ? Color.white : new Color(1, 1, 1, 0.35f));
            Text(r, label, size, tc, TextAnchor.MiddleCenter, selected ? 0f : 1.5f);
            if (!enabled) return false;
            if (GUI.Button(r, GUIContent.none, buttonHit))
            {
                GameAudio.Play(Sfx.Click, 0.6f);
                return true;
            }
            return false;
        }

        public static void Bar(Rect r, float fill, Color c, Color bg)
        {
            Panel(r, bg);
            if (fill <= 0.001f) return;
            float w = Mathf.Max(r.height, r.width * Mathf.Clamp01(fill));
            Panel(new Rect(r.x, r.y, w, r.height), c);
        }

        public static void RotatedRect(Vector2 pivot, Rect r, float angle, Color c)
        {
            var m = GUI.matrix;
            GUIUtility.RotateAroundPivot(angle, pivot * Scale);
            Rect(r, c);
            GUI.matrix = m;
        }

        public static string Ordinal(int n)
        {
            if (n % 100 >= 11 && n % 100 <= 13) return n + "th";
            switch (n % 10) { case 1: return n + "st"; case 2: return n + "nd"; case 3: return n + "rd"; default: return n + "th"; }
        }
    }
}
