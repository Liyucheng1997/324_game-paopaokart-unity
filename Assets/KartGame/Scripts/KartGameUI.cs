using UnityEngine;

namespace KartGame
{
    /// <summary>
    /// Immediate-mode HUD: countdown, speed, lap, position, drift charge,
    /// and the final result board. Scales with screen height.
    /// </summary>
    public class KartGameUI : MonoBehaviour
    {
        GUIStyle big, mid, small, badge;
        Texture2D barBg, barFill, panelTex;

        static Texture2D Solid(Color c)
        {
            var t = new Texture2D(1, 1);
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }

        void EnsureStyles()
        {
            if (big != null) return;
            barBg = Solid(new Color(0f, 0f, 0f, 0.45f));
            barFill = Solid(Color.white);   // tinted with GUI.color per drift level
            panelTex = Solid(new Color(0.05f, 0.07f, 0.15f, 0.85f));

            big = new GUIStyle { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            big.normal.textColor = Color.white;
            mid = new GUIStyle(big);
            small = new GUIStyle(big) { fontStyle = FontStyle.Bold };
            badge = new GUIStyle(big);
        }

        void OnGUI()
        {
            var rm = RaceManager.Instance;
            if (rm == null) return;
            EnsureStyles();

            float h = Screen.height;
            big.fontSize = (int)(h * 0.12f);
            mid.fontSize = (int)(h * 0.05f);
            small.fontSize = (int)(h * 0.03f);
            badge.fontSize = (int)(h * 0.045f);

            // countdown
            if (!rm.RaceStarted)
            {
                int n = Mathf.CeilToInt(rm.CountdownRemaining);
                big.normal.textColor = n <= 1 ? new Color(1f, 0.85f, 0.2f) : Color.white;
                GUI.Label(new Rect(0, h * 0.25f, Screen.width, h * 0.2f), n.ToString(), big);
                return;
            }
            if (rm.RaceTime < 1.2f)
            {
                big.normal.textColor = new Color(0.35f, 1f, 0.4f);
                GUI.Label(new Rect(0, h * 0.25f, Screen.width, h * 0.2f), "GO!", big);
            }

            var p = rm.Player;
            if (p == null) return;

            // top-left: lap + time
            small.alignment = TextAnchor.MiddleLeft;
            small.normal.textColor = Color.white;
            GUI.DrawTexture(new Rect(10, 10, h * 0.36f, h * 0.105f), barBg);
            GUI.Label(new Rect(24, 12, 400, h * 0.05f),
                $"LAP  {Mathf.Min(p.lap + 1, rm.totalLaps)} / {rm.totalLaps}", small);
            GUI.Label(new Rect(24, 12 + h * 0.045f, 400, h * 0.05f),
                $"TIME  {FormatTime(rm.RaceTime)}", small);

            // top-left below: stored nitro tanks (fire with CTRL)
            {
                float slotW = h * 0.10f, slotH = h * 0.05f, gap = h * 0.012f;
                float y0 = 10 + h * 0.115f;
                Color prevColor = GUI.color;
                for (int i = 0; i < p.kart.maxNitroTanks; i++)
                {
                    var slot = new Rect(10 + i * (slotW + gap), y0, slotW, slotH);
                    GUI.color = Color.white;
                    GUI.DrawTexture(slot, barBg);
                    if (i < p.kart.NitroTanks)
                    {
                        GUI.color = new Color(0.35f, 0.75f, 1f, 0.95f);
                        GUI.DrawTexture(new Rect(slot.x + 3, slot.y + 3, slot.width - 6, slot.height - 6), barFill);
                    }
                }
                GUI.color = prevColor;
                if (p.kart.NitroTanks > 0)
                {
                    small.alignment = TextAnchor.MiddleLeft;
                    small.normal.textColor = new Color(0.6f, 0.9f, 1f);
                    GUI.Label(new Rect(10 + p.kart.maxNitroTanks * (slotW + gap) + 6, y0, 300, slotH),
                        "CTRL!", small);
                }
            }

            // top-right: position badge
            int pos = rm.GetPosition(p);
            GUI.DrawTexture(new Rect(Screen.width - h * 0.22f, 10, h * 0.21f, h * 0.105f), barBg);
            badge.normal.textColor = pos == 1 ? new Color(1f, 0.85f, 0.2f) : Color.white;
            GUI.Label(new Rect(Screen.width - h * 0.22f, 10, h * 0.21f, h * 0.105f),
                $"{pos} / {rm.Racers.Count}", badge);

            // bottom-right: speed
            mid.alignment = TextAnchor.MiddleRight;
            float kmh = Mathf.Abs(p.kart.CurrentSpeed) * 3.6f;
            mid.normal.textColor = p.kart.BoostTimer > 0f ? new Color(1f, 0.55f, 0.1f) : Color.white;
            GUI.Label(new Rect(Screen.width - 420, h - h * 0.12f, 380, h * 0.06f),
                $"{kmh:0} km/h", mid);

            // persistent nitro charge meter (fills while drifting, never resets)
            {
                float w = h * 0.3f;
                var rect = new Rect((Screen.width - w) / 2f, h * 0.8f, w, h * 0.035f);
                GUI.DrawTexture(rect, barBg);
                float charge = p.kart.NitroCharge01;
                Color fillColor = p.kart.NitroTanks >= p.kart.maxNitroTanks
                    ? new Color(0.35f, 0.75f, 1f, 0.95f)                                   // everything maxed
                    : Color.Lerp(new Color(1f, 0.6f, 0.1f, 0.95f),
                                 new Color(1f, 0.95f, 0.3f, 0.95f), charge);               // orange -> yellow
                Color prevColor = GUI.color;
                GUI.color = fillColor;
                GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * charge, rect.height), barFill);
                GUI.color = prevColor;
                if (p.kart.IsDrifting)
                {
                    small.alignment = TextAnchor.MiddleCenter;
                    small.normal.textColor = new Color(1f, 1f, 1f, 0.9f);
                    GUI.Label(new Rect(0, rect.y - h * 0.045f, Screen.width, h * 0.04f), "CHARGING...", small);
                }
            }

            // hint
            small.alignment = TextAnchor.MiddleCenter;
            small.normal.textColor = new Color(1f, 1f, 1f, 0.45f);
            GUI.Label(new Rect(0, h - h * 0.05f, Screen.width, h * 0.04f),
                "WASD / Arrows  drive     SHIFT  drift = charge nitro     CTRL  fire nitro     R  restart", small);

            // finish board
            if (p.Finished)
                DrawResults(rm);
        }

        void DrawResults(RaceManager rm)
        {
            float h = Screen.height;
            float w = h * 0.7f, ph = h * 0.6f;
            var rect = new Rect((Screen.width - w) / 2f, (h - ph) / 2f, w, ph);
            GUI.DrawTexture(rect, panelTex);

            mid.alignment = TextAnchor.MiddleCenter;
            mid.normal.textColor = new Color(1f, 0.85f, 0.2f);
            GUI.Label(new Rect(rect.x, rect.y + 10, rect.width, h * 0.08f), "RACE RESULTS", mid);

            var ordered = new System.Collections.Generic.List<RaceManager.RacerState>(rm.Racers);
            ordered.Sort((a, b) =>
            {
                if (a.Finished && b.Finished) return a.finishTime.CompareTo(b.finishTime);
                if (a.Finished) return -1;
                if (b.Finished) return 1;
                return b.progress.CompareTo(a.progress);
            });

            small.alignment = TextAnchor.MiddleLeft;
            for (int i = 0; i < ordered.Count; i++)
            {
                var r = ordered[i];
                small.normal.textColor = r == rm.Player ? new Color(0.4f, 1f, 0.5f) : Color.white;
                string time = r.Finished ? FormatTime(r.finishTime) : "—";
                GUI.Label(new Rect(rect.x + w * 0.12f, rect.y + h * 0.11f + i * h * 0.055f, w * 0.8f, h * 0.05f),
                    $"{i + 1}.   {r.name,-14}   {time}", small);
            }

            small.alignment = TextAnchor.MiddleCenter;
            small.normal.textColor = new Color(1f, 1f, 1f, 0.7f);
            GUI.Label(new Rect(rect.x, rect.y + ph - h * 0.07f, rect.width, h * 0.05f),
                "Press  R  to race again", small);
        }

        static string FormatTime(float t)
        {
            int m = (int)(t / 60f);
            float s = t - m * 60f;
            return $"{m:0}:{s:00.00}";
        }
    }
}
