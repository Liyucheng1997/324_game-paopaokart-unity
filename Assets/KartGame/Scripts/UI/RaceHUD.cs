using System.Collections.Generic;
using UnityEngine;

namespace KartGame
{
    /// <summary>In-race HUD, countdown, popups, pause menu and results board.</summary>
    public class RaceHUD : MonoBehaviour
    {
        RaceManager rm;
        struct Popup { public string text; public Color color; public float t; }
        readonly List<Popup> popups = new List<Popup>();
        Texture2D[] icons;
        Texture2D dot;
        float warnBlink;

        void Start()
        {
            rm = GetComponent<RaceManager>();
            rm.OnMessage += (text, c) =>
            {
                popups.Add(new Popup { text = text, color = c, t = 0f });
                if (popups.Count > 3) popups.RemoveAt(0);
            };
            icons = new Texture2D[System.Enum.GetValues(typeof(ItemType)).Length];
            foreach (ItemType t in System.Enum.GetValues(typeof(ItemType)))
                if (t != ItemType.None) icons[(int)t] = ProcTex.ItemIcon(t);
            dot = ProcTex.Circle("dot", Color.white, 2f);
        }

        void Update()
        {
            for (int i = popups.Count - 1; i >= 0; i--)
            {
                var p = popups[i];
                p.t += Time.unscaledDeltaTime;
                popups[i] = p;
                if (p.t > 1.8f) popups.RemoveAt(i);
            }
            warnBlink += Time.unscaledDeltaTime;
        }

        void OnGUI()
        {
            if (rm == null || rm.World == null || rm.Player == null) return;
            UIKit.Begin();
            GUI.depth = 0;
            var p = rm.Player;
            var kart = p.kart;
            float W = UIKit.W, H = UIKit.H;

            if (rm.Phase == RacePhase.Intro)
            {
                DrawIntro(W, H);
                return;
            }

            DrawTopLeft(p);
            DrawStandings(p);
            DrawTimes(p, W);
            DrawMinimap(W);
            DrawSpeedo(kart, W, H);
            if (rm.Mode == RaceMode.Speed) DrawNitro(kart, W, H); else DrawItems(kart, W, H);
            DrawCountdown(W, H);
            DrawPopups(W, H);
            DrawWarnings(p, kart, W, H);

            if (rm.Phase == RacePhase.Results) DrawResults(W, H);
            else if (rm.Paused) DrawPause(W, H);
            else
                UIKit.Text(new Rect(0, H - 34, W, 30), "WASD drive · SHIFT drift · re-tap W after a drift = mini boost · CTRL " +
                    (rm.Mode == RaceMode.Speed ? "nitro" : "item") + " · R respawn · ESC pause", 17, new Color(1, 1, 1, 0.55f), TextAnchor.MiddleCenter, 1f);
        }

        // ------------------------------------------------------------ pieces

        void DrawIntro(float W, float H)
        {
            var def = rm.World.Def;
            float a = Mathf.Clamp01(rm.PhaseTime * 2f) * Mathf.Clamp01((3.1f - rm.PhaseTime) * 2f);
            UIKit.Rect(new Rect(0, H * 0.68f, W, 150), new Color(0.03f, 0.05f, 0.12f, 0.6f * a));
            UIKit.Text(new Rect(80, H * 0.68f + 12, W, 80), def.Name, 64, new Color(1, 1, 1, a), TextAnchor.MiddleLeft, 3f);
            UIKit.Text(new Rect(84, H * 0.68f + 84, W, 40), def.Subtitle + "   ·   " + rm.TotalLaps + " LAPS   ·   " +
                (rm.Mode == RaceMode.Speed ? "SPEED RACE" : "ITEM RACE"), 26, new Color(UIKit.Accent.r, UIKit.Accent.g, UIKit.Accent.b, a));
        }

        void DrawTopLeft(RaceManager.RacerState p)
        {
            int pos = rm.GetPosition(p);
            string ord = UIKit.Ordinal(pos);
            Color c = pos == 1 ? UIKit.Accent : (pos <= 3 ? new Color(0.6f, 0.9f, 1f) : Color.white);
            UIKit.Text(new Rect(36, 18, 300, 130), ord, 110, c, TextAnchor.UpperLeft, 4f);
            float ordW = UIKit.Style(110, TextAnchor.UpperLeft).CalcSize(new GUIContent(ord)).x;
            UIKit.Text(new Rect(48 + ordW, 82, 200, 60), "/ " + rm.Racers.Count, 36, new Color(1, 1, 1, 0.8f), TextAnchor.UpperLeft);
            int lap = Mathf.Clamp(p.lap, 1, rm.TotalLaps);
            UIKit.Panel(new Rect(36, 150, 230, 52), UIKit.PanelDark);
            UIKit.Text(new Rect(52, 150, 230, 52), "LAP", 22, new Color(1, 1, 1, 0.7f));
            UIKit.Text(new Rect(110, 150, 150, 52), lap + " / " + rm.TotalLaps, 32, lap == rm.TotalLaps ? UIKit.Accent : Color.white);
        }

        void DrawStandings(RaceManager.RacerState player)
        {
            var list = rm.Standings();
            float y = 222;
            for (int i = 0; i < list.Count; i++)
            {
                var r = list[i];
                bool me = r == player;
                var row = new Rect(36, y + i * 44, 250, 38);
                UIKit.Panel(row, me ? new Color(1f, 0.78f, 0.15f, 0.92f) : new Color(0.06f, 0.09f, 0.18f, 0.7f));
                UIKit.Tex(new Rect(row.x + 44, row.y + 11, 16, 16), dot, r.color);
                Color tc = me ? new Color(0.08f, 0.08f, 0.15f) : Color.white;
                UIKit.Text(new Rect(row.x + 12, row.y, 40, 38), (i + 1).ToString(), 22, tc, TextAnchor.MiddleLeft, me ? 0f : 1.5f);
                UIKit.Text(new Rect(row.x + 70, row.y, 170, 38), r.name, 21, tc, TextAnchor.MiddleLeft, me ? 0f : 1.5f);
                if (r.Finished)
                    UIKit.Text(new Rect(row.x + 70, row.y, 170, 38), r.retired ? "RET" : "FIN", 17, me ? tc : UIKit.Accent, TextAnchor.MiddleRight, 0f);
                else if (r.kart.Items != null && r.kart.Items.Front != ItemType.None && rm.Mode == RaceMode.Item && !me)
                    UIKit.Tex(new Rect(row.x + 210, row.y + 4, 30, 30), icons[(int)r.kart.Items.Front], Color.white);
            }
        }

        void DrawTimes(RaceManager.RacerState p, float W)
        {
            var r = new Rect(W * 0.5f - 190, 18, 380, 92);
            UIKit.Panel(r, UIKit.PanelDark);
            float t = rm.RaceStarted ? (p.Finished && p.finishTime > 0f ? p.finishTime : rm.RaceTime) : 0f;
            UIKit.Text(new Rect(r.x, r.y + 4, r.width, 50), GameSession.FormatTime(t), 44, Color.white, TextAnchor.MiddleCenter);
            float lapT = rm.RaceStarted && !p.Finished ? rm.RaceTime - p.lapStart : 0f;
            string best = p.bestLap > 0f ? GameSession.FormatTime(p.bestLap) : "--";
            UIKit.Text(new Rect(r.x + 20, r.y + 52, r.width - 40, 34), "LAP " + GameSession.FormatTime(lapT), 20, new Color(1, 1, 1, 0.75f), TextAnchor.MiddleLeft, 1f);
            UIKit.Text(new Rect(r.x + 20, r.y + 52, r.width - 40, 34), "BEST " + best, 20, new Color(0.6f, 0.9f, 1f, 0.9f), TextAnchor.MiddleRight, 1f);
            if (rm.RetireCountdown > 0f && !p.Finished)
                UIKit.Text(new Rect(r.x, r.y + 96, r.width, 40), "TIME LEFT " + Mathf.CeilToInt(rm.RetireCountdown), 28,
                    Mathf.Repeat(warnBlink, 0.6f) < 0.3f ? new Color(1f, 0.4f, 0.3f) : Color.white, TextAnchor.MiddleCenter);
        }

        void DrawMinimap(float W)
        {
            var w = rm.World;
            float size = 300;
            var r = new Rect(W - size - 30, 20, size, size);
            UIKit.Panel(new Rect(r.x - 6, r.y - 6, size + 12, size + 12), new Color(0.05f, 0.08f, 0.16f, 0.55f));
            GUI.DrawTexture(r, w.Minimap);
            Vector2 Map(Vector3 wp)
            {
                Vector2 uv = (new Vector2(wp.x, wp.z) - w.MapMin) / w.MapSize;
                return new Vector2(r.x + uv.x * size, r.y + (1f - uv.y) * size);
            }
            // start line marker
            Vector2 s = Map(w.Track.Pos[0]);
            UIKit.Rect(new Rect(s.x - 5, s.y - 5, 10, 10), new Color(0.1f, 0.1f, 0.1f));
            UIKit.Rect(new Rect(s.x - 3, s.y - 3, 6, 6), Color.white);
            foreach (var racer in rm.Racers)
            {
                if (racer.isPlayer) continue;
                Vector2 m = Map(racer.kart.transform.position);
                UIKit.Tex(new Rect(m.x - 9, m.y - 9, 18, 18), dot, new Color(0, 0, 0, 0.7f));
                UIKit.Tex(new Rect(m.x - 7, m.y - 7, 14, 14), dot, racer.color);
            }
            Vector2 pm = Map(rm.Player.kart.transform.position);
            float pulse = 1f + Mathf.Sin(Time.unscaledTime * 6f) * 0.15f;
            UIKit.Tex(new Rect(pm.x - 13 * pulse, pm.y - 13 * pulse, 26 * pulse, 26 * pulse), dot, new Color(0, 0, 0, 0.8f));
            UIKit.Tex(new Rect(pm.x - 10, pm.y - 10, 20, 20), dot, UIKit.Accent);
            UIKit.Tex(new Rect(pm.x - 5, pm.y - 5, 10, 10), dot, Color.white);
        }

        void DrawSpeedo(KartController kart, float W, float H)
        {
            Vector2 c = new Vector2(W - 190, H - 140);
            float radius = 125f;
            UIKit.Tex(new Rect(c.x - radius - 22, c.y - radius - 22, (radius + 22) * 2, (radius + 22) * 2), dot, new Color(0.04f, 0.06f, 0.14f, 0.6f));
            float kmh = kart.SpeedKmh;
            float frac = Mathf.Clamp01(kmh / 300f);
            int segs = 30;
            bool boost = kart.BoostTimer > 0f;
            for (int i = 0; i < segs; i++)
            {
                float u = i / (float)(segs - 1);
                float ang = Mathf.Lerp(-225f, 45f, u);
                bool lit = u <= frac;
                Color col = lit ? (u > 0.8f ? new Color(1f, 0.35f, 0.25f) : (boost ? new Color(0.4f, 0.85f, 1f) : Color.Lerp(new Color(1f, 0.85f, 0.25f), new Color(1f, 0.5f, 0.15f), u))) : new Color(1, 1, 1, 0.15f);
                UIKit.RotatedRect(c, new Rect(c.x + radius - 26, c.y - 5, lit ? 28 : 20, 10), ang, col);
            }
            UIKit.Text(new Rect(c.x - 120, c.y - 50, 240, 90), Mathf.RoundToInt(kmh).ToString(), 76, boost ? new Color(0.55f, 0.9f, 1f) : Color.white, TextAnchor.MiddleCenter, 3f);
            UIKit.Text(new Rect(c.x - 120, c.y + 28, 240, 40), "km/h", 24, new Color(1, 1, 1, 0.7f), TextAnchor.MiddleCenter, 1f);
        }

        void DrawNitro(KartController kart, float W, float H)
        {
            float bw = Mathf.Min(620f, W * 0.34f);
            var bar = new Rect((W - bw) * 0.5f + 70, H - 108, bw, 30);
            // tanks
            for (int i = 0; i < kart.maxNitroTanks; i++)
            {
                var slot = new Rect(bar.x - 150 + i * 70, bar.y - 22, 60, 74);
                bool full = i < kart.NitroTanks;
                UIKit.Panel(slot, new Color(0.05f, 0.08f, 0.16f, 0.8f));
                if (full)
                {
                    float glow = 0.75f + Mathf.Sin(Time.unscaledTime * 6f + i) * 0.25f;
                    UIKit.Panel(new Rect(slot.x + 8, slot.y + 12, slot.width - 16, slot.height - 20), new Color(0.3f * glow, 0.72f * glow, 1f, 1f));
                    UIKit.Panel(new Rect(slot.x + 20, slot.y + 4, slot.width - 40, 12), new Color(0.8f, 0.9f, 1f));
                    UIKit.Text(new Rect(slot.x, slot.y + 12, slot.width, slot.height - 20), "N", 30, Color.white, TextAnchor.MiddleCenter, 2f);
                }
                else UIKit.Outline(slot, new Color(1, 1, 1, 0.25f));
            }
            // charge gauge
            float charge = kart.NitroCharge01;
            bool maxed = kart.NitroTanks >= kart.maxNitroTanks;
            Color fill = maxed ? new Color(0.35f, 0.78f, 1f) : Color.Lerp(new Color(1f, 0.55f, 0.1f), new Color(1f, 0.9f, 0.25f), charge);
            UIKit.Bar(bar, charge, fill, new Color(0.05f, 0.08f, 0.16f, 0.8f));
            UIKit.Outline(bar, new Color(1, 1, 1, 0.35f));
            string label = kart.IsDrifting ? "CHARGING" : (maxed ? "NITRO FULL" : "DRIFT TO CHARGE");
            UIKit.Text(new Rect(bar.x, bar.y - 34, bar.width, 30), label, 20, kart.IsDrifting ? UIKit.Accent : new Color(1, 1, 1, 0.7f), TextAnchor.MiddleLeft, 1.5f);
            if (kart.NitroTanks > 0)
                UIKit.Text(new Rect(bar.x, bar.y - 34, bar.width, 30), "CTRL  >  NITRO", 22, new Color(0.55f, 0.9f, 1f), TextAnchor.MiddleRight, 1.5f);
        }

        void DrawItems(KartController kart, float W, float H)
        {
            var items = kart.Items;
            if (items == null) return;
            for (int i = 0; i < KartItems.MaxSlots; i++)
            {
                float s = i == 0 ? 120 : 84;
                var slot = new Rect(W * 0.5f - 130 + (i == 0 ? 0 : 140), H - 40 - s, s, s);
                UIKit.Panel(slot, new Color(0.05f, 0.08f, 0.16f, 0.82f));
                UIKit.Outline(slot, i == 0 ? UIKit.Accent : new Color(1, 1, 1, 0.3f));
                if (i < items.Slots.Count)
                {
                    var type = items.Slots[i];
                    if (items.RouletteTimer > 0f && i == items.Slots.Count - 1)
                        type = (ItemType)(1 + (int)(Time.unscaledTime * 18f) % 6);
                    UIKit.Tex(new Rect(slot.x + 10, slot.y + 10, s - 20, s - 20), icons[(int)type], Color.white);
                }
            }
            UIKit.Text(new Rect(W * 0.5f - 130, H - 200, 300, 34), items.Front != ItemType.None ? "CTRL  >  " + items.Front.ToString().ToUpper() : "GRAB ITEM BOXES", 20,
                items.Front != ItemType.None ? UIKit.Accent : new Color(1, 1, 1, 0.6f), TextAnchor.MiddleLeft, 1.5f);
        }

        void DrawCountdown(float W, float H)
        {
            if (rm.Phase == RacePhase.Countdown)
            {
                int n = Mathf.CeilToInt(rm.CountdownRemaining);
                float frac = n - rm.CountdownRemaining;       // 0 -> 1 within the second
                float scale = 1.6f - Mathf.Clamp01(frac * 4f) * 0.6f;
                int size = (int)(170 * scale);
                UIKit.Text(new Rect(0, H * 0.22f, W, 260), n.ToString(), size, n == 1 ? new Color(1f, 0.4f, 0.3f) : Color.white, TextAnchor.MiddleCenter, 6f);
                UIKit.Text(new Rect(0, H * 0.22f + 220, W, 50), "Press W right at GO for a start boost!", 26, new Color(1, 1, 1, 0.75f), TextAnchor.MiddleCenter, 1.5f);
            }
            else if (rm.Phase == RacePhase.Racing && rm.RaceTime < 1.2f)
            {
                float t = rm.RaceTime / 1.2f;
                int size = (int)Mathf.Lerp(200, 260, t);
                UIKit.Text(new Rect(0, H * 0.22f, W, 260), "GO!", size, new Color(0.4f, 1f, 0.45f, 1f - t * t), TextAnchor.MiddleCenter, 6f);
            }
        }

        void DrawPopups(float W, float H)
        {
            for (int i = 0; i < popups.Count; i++)
            {
                var p = popups[i];
                float a = Mathf.Clamp01(p.t * 6f) * Mathf.Clamp01((1.8f - p.t) * 3f);
                float scale = 1f + Mathf.Clamp01(0.15f - p.t) * 3f;
                float y = H * 0.36f - (popups.Count - 1 - i) * 62 - p.t * 14f;
                UIKit.Text(new Rect(0, y, W, 70), p.text, (int)(52 * scale), new Color(p.color.r, p.color.g, p.color.b, a), TextAnchor.MiddleCenter, 3f);
            }
        }

        void DrawWarnings(RaceManager.RacerState p, KartController kart, float W, float H)
        {
            bool blink = Mathf.Repeat(warnBlink, 0.5f) < 0.3f;
            if (p.wrongWay > 1.2f && blink)
            {
                UIKit.Panel(new Rect(W * 0.5f - 230, H * 0.5f - 60, 460, 90), new Color(0.8f, 0.1f, 0.1f, 0.85f));
                UIKit.Text(new Rect(0, H * 0.5f - 60, W, 90), "WRONG WAY!", 54, Color.white, TextAnchor.MiddleCenter, 2f);
            }
            if (kart.Items != null && kart.Items.IncomingMissile > 0f && blink)
                UIKit.Text(new Rect(0, 130, W, 60), "!! MISSILE LOCKED !!", 38, new Color(1f, 0.3f, 0.25f), TextAnchor.MiddleCenter, 2f);
            if (kart.MiniWindow > 0f && !kart.IsDrifting)
                UIKit.Text(new Rect(0, H - 250, W, 50), "W!", 44, new Color(0.5f, 0.9f, 1f, 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 30f)), TextAnchor.MiddleCenter, 2f);
            if (kart.ShieldTimer > 0f)
                UIKit.Text(new Rect(W * 0.5f - 300, H - 250, 600, 40), "SHIELD " + kart.ShieldTimer.ToString("0.0"), 22, new Color(1f, 0.9f, 0.45f), TextAnchor.MiddleCenter, 1.5f);
        }

        void DrawPause(float W, float H)
        {
            UIKit.Rect(new Rect(0, 0, W, H), new Color(0.02f, 0.03f, 0.08f, 0.55f));
            var r = new Rect(W * 0.5f - 230, H * 0.5f - 200, 460, 400);
            UIKit.Panel(r, new Color(0.06f, 0.09f, 0.18f, 0.95f));
            UIKit.Text(new Rect(r.x, r.y + 20, r.width, 70), "PAUSED", 56, UIKit.Accent, TextAnchor.MiddleCenter, 2f);
            if (UIKit.Button(new Rect(r.x + 60, r.y + 120, r.width - 120, 66), "RESUME", 28)) rm.SetPaused(false);
            if (UIKit.Button(new Rect(r.x + 60, r.y + 200, r.width - 120, 66), "RESTART", 28)) rm.Restart();
            if (UIKit.Button(new Rect(r.x + 60, r.y + 280, r.width - 120, 66), "MAIN MENU", 28)) rm.BackToMenu();
        }

        void DrawResults(float W, float H)
        {
            float a = Mathf.Clamp01(rm.PhaseTime * 3f);
            UIKit.Rect(new Rect(0, 0, W, H), new Color(0.02f, 0.03f, 0.08f, 0.5f * a));
            var r = new Rect(W * 0.5f - 420, H * 0.5f - 330, 840, 660);
            UIKit.Panel(r, new Color(0.06f, 0.09f, 0.18f, 0.95f * a));
            UIKit.Panel(new Rect(r.x, r.y, r.width, 100), new Color(1f, 0.78f, 0.15f, a));
            int place = rm.Player.place;
            string head = rm.Player.retired ? "RETIRED" : (place == 1 ? "VICTORY!" : UIKit.Ordinal(place) + " PLACE");
            UIKit.Text(new Rect(r.x, r.y, r.width, 100), head, 60, new Color(0.08f, 0.08f, 0.15f, a), TextAnchor.MiddleCenter, 0f);
            if (rm.NewRecord)
                UIKit.Text(new Rect(r.x, r.y + 104, r.width, 40), "NEW TRACK RECORD!", 26, new Color(0.5f, 1f, 0.6f, a), TextAnchor.MiddleCenter, 1.5f);

            var list = rm.Standings();
            float y = r.y + 150;
            UIKit.Text(new Rect(r.x + 60, y, 200, 34), "POS", 20, new Color(1, 1, 1, 0.5f * a));
            UIKit.Text(new Rect(r.x + 160, y, 200, 34), "RACER", 20, new Color(1, 1, 1, 0.5f * a));
            UIKit.Text(new Rect(r.x + 430, y, 200, 34), "TIME", 20, new Color(1, 1, 1, 0.5f * a));
            UIKit.Text(new Rect(r.x + 620, y, 200, 34), "BEST LAP", 20, new Color(1, 1, 1, 0.5f * a));
            for (int i = 0; i < list.Count; i++)
            {
                var s = list[i];
                float ry = y + 42 + i * 56;
                if (s.isPlayer) UIKit.Panel(new Rect(r.x + 40, ry - 4, r.width - 80, 52), new Color(1f, 0.78f, 0.15f, 0.25f * a));
                Color c = new Color(1, 1, 1, a);
                UIKit.Text(new Rect(r.x + 60, ry, 100, 44), (i + 1).ToString(), 30, i == 0 ? new Color(1f, 0.8f, 0.2f, a) : c);
                UIKit.Tex(new Rect(r.x + 160, ry + 13, 18, 18), dot, s.color);
                UIKit.Text(new Rect(r.x + 190, ry, 240, 44), s.name, 28, c);
                string time = s.retired ? "RETIRED" : (s.finishTime > 0f ? GameSession.FormatTime(s.finishTime) : "racing...");
                UIKit.Text(new Rect(r.x + 430, ry, 200, 44), time, 26, c);
                UIKit.Text(new Rect(r.x + 620, ry, 200, 44), s.bestLap > 0f ? GameSession.FormatTime(s.bestLap) : "--", 24, new Color(0.6f, 0.9f, 1f, a));
            }
            if (UIKit.Button(new Rect(r.x + 90, r.yMax - 100, 300, 70), "RACE AGAIN", 28, true)) rm.Restart();
            if (UIKit.Button(new Rect(r.xMax - 390, r.yMax - 100, 300, 70), "MAIN MENU", 28)) rm.BackToMenu();
        }
    }
}
