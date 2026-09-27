using UnityEngine;

namespace KartGame
{
    /// <summary>Main menu: 3D kart showroom + mode / track / kart / color / difficulty / laps selection.</summary>
    public class MenuScreen : MonoBehaviour
    {
        Transform showroom, preview;
        int builtKart = -1, builtColor = -1, builtTheme = -1;
        float orbit;
        Material groundMat;
        Texture2D dot;

        void Start()
        {
            dot = ProcTex.Circle("dot", Color.white, 2f);
            BuildShowroom();
            GameAudio.PlayMusic(0);
            GameAudio.SetMusicVolume(0.3f);
        }

        void BuildShowroom()
        {
            showroom = new GameObject("Showroom").transform;
            var b = new MeshBuilder();
            // turntable
            var plate = new[] { new Vector2(0, 0.0f), new Vector2(3.4f, 0.0f), new Vector2(3.6f, 0.12f), new Vector2(3.6f, 0.12f), new Vector2(3.5f, 0.28f), new Vector2(3.3f, 0.3f), new Vector2(0, 0.3f) };
            b.Gloss = 0.6f;
            b.Lathe(plate, 48, (p, n) => new Vector2(p.x, p.z).magnitude > 3.2f ? UIKit.Accent : new Color(0.2f, 0.22f, 0.3f));
            b.Emissive = 1f;
            b.Torus(new Vector3(0, 0.3f, 0), Quaternion.identity, 3.25f, 0.035f, 64, 4, new Color(0.4f, 0.8f, 1f));
            b.Emissive = 0f;
            b.Build("Turntable", showroom, Mats.VertexLit);

            // ground disc
            var g = new MeshBuilder();
            g.Disc(new Vector3(0, -0.02f, 0), Vector3.up, 160f, 64, Color.white);
            groundMat = Mats.Lit(Color.white, 0.02f, ProcTex.Ground("grass", Color.white, new Color(0.86f, 0.86f, 0.86f), 0.12f), "MenuGround");
            groundMat.SetFloat("_WorldUV", 0.18f);
            groundMat.SetFloat("_Detail", 0.5f);
            g.Build("Ground", showroom, groundMat, false);

            // scenery ring
            var rng = new Rng(5);
            var s = new MeshBuilder();
            for (int i = 0; i < 70; i++)
            {
                float a = rng.Range(0f, Mathf.PI * 2f);
                float r = rng.Range(16f, 70f);
                Vector3 p = new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
                if (rng.Chance(0.5f)) Props.PineTree(s, p, rng.Range(6f, 11f), rng, new Color(0.16f, 0.42f, 0.22f), false);
                else Props.RoundTree(s, p, rng.Range(5f, 9f), rng, new Color(0.3f, 0.6f, 0.22f));
            }
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f + 0.4f;
                Props.TireStack(s, new Vector3(Mathf.Cos(a) * 6.5f, 0, Mathf.Sin(a) * 6.5f), 3, i % 2 == 0 ? new Color(0.9f, 0.15f, 0.12f) : Color.white, rng);
            }
            Props.FlagPole(s, new Vector3(-5f, 0, 4f), new Color(1f, 0.8f, 0.2f));
            Props.FlagPole(s, new Vector3(5f, 0, 4f), new Color(0.25f, 0.55f, 1f));
            s.Build("Scenery", showroom, Mats.VertexLit);
            snowRoot = new GameObject("Deco").transform;
            snowRoot.SetParent(showroom, false);
        }

        Transform snowRoot;

        void RefreshTheme()
        {
            int theme = (int)Tracks.All[GameSession.TrackIndex].Theme;
            if (theme == builtTheme) return;
            builtTheme = theme;
            var style = ThemeStyle.For((Theme)theme);
            WorldBuilder.SetupEnvironment(style);
            RenderSettings.fogStartDistance = 40f;
            RenderSettings.fogEndDistance = 220f;
            groundMat.color = style.Grass;
        }

        void RefreshKart()
        {
            if (builtKart == GameSession.KartIndex && builtColor == GameSession.ColorIndex && preview != null) return;
            builtKart = GameSession.KartIndex;
            builtColor = GameSession.ColorIndex;
            float yaw = preview != null ? preview.eulerAngles.y : 200f;
            if (preview != null) Destroy(preview.gameObject);
            preview = new GameObject("Preview").transform;
            preview.position = new Vector3(0, 0.3f, 0);
            preview.rotation = Quaternion.Euler(0, yaw, 0);
            KartModel.Build(preview, KartStats.All[builtKart].Design, GameSession.Liveries[builtColor]);
            preview.localScale = Vector3.one * 1.25f;
            Fx.Burst(preview.position + Vector3.up, UIKit.Accent, 30, 6f, 0.25f, 0.6f);
        }

        void Update()
        {
            RefreshTheme();
            RefreshKart();
            if (preview != null) preview.Rotate(0f, 22f * Time.deltaTime, 0f);
            orbit += Time.deltaTime * 4f;
            var cam = Camera.main;
            if (cam != null)
            {
                float a = (orbit + 200f) * Mathf.Deg2Rad;
                Vector3 target = new Vector3(0, 1.0f, 0);
                // frame the kart on the right half of the screen
                Vector3 pos = new Vector3(Mathf.Sin(a) * 7.5f, 2.6f, Mathf.Cos(a) * 7.5f);
                cam.transform.position = pos;
                cam.transform.rotation = Quaternion.LookRotation(target - pos);
                cam.transform.position -= cam.transform.right * 2.6f;
                cam.fieldOfView = 45f;
            }

            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter)) StartRace();
            if (Input.GetKeyDown(KeyCode.RightArrow) || Input.GetKeyDown(KeyCode.D)) GameSession.KartIndex = (GameSession.KartIndex + 1) % KartStats.All.Length;
            if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.A)) GameSession.KartIndex = (GameSession.KartIndex + KartStats.All.Length - 1) % KartStats.All.Length;
        }

        void StartRace()
        {
            GameAudio.Play(Sfx.Go, 0.7f);
            GameSession.GoRace = true;
            UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }

        void OnGUI()
        {
            UIKit.Begin();
            float W = UIKit.W, H = UIKit.H;

            // title
            UIKit.Rect(new Rect(0, 0, W, 190), new Color(0.02f, 0.04f, 0.12f, 0.35f));
            UIKit.Text(new Rect(60, 26, 900, 110), "PAOPAO KART", 104, UIKit.Accent, TextAnchor.MiddleLeft, 5f);
            UIKit.Text(new Rect(66, 128, 900, 40), "Drift  ·  Nitro  ·  Items  —  a KartRider / QQ Speed style racer", 26, Color.white, TextAnchor.MiddleLeft, 2f);

            // left column
            var col = new Rect(60, 210, 600, 760);
            UIKit.Panel(col, new Color(0.05f, 0.08f, 0.17f, 0.86f));
            float x = col.x + 34, w = col.width - 68;
            float y = col.y + 26;

            Section(x, ref y, "MODE");
            if (UIKit.Button(new Rect(x, y, w * 0.49f, 62), "SPEED RACE", 26, GameSession.Mode == RaceMode.Speed)) GameSession.Mode = RaceMode.Speed;
            if (UIKit.Button(new Rect(x + w * 0.51f, y, w * 0.49f, 62), "ITEM RACE", 26, GameSession.Mode == RaceMode.Item)) GameSession.Mode = RaceMode.Item;
            y += 70;
            UIKit.Text(new Rect(x, y, w, 30), GameSession.Mode == RaceMode.Speed
                ? "Drift to fill nitro, CTRL to fire. Pure driving skill."
                : "Grab ? boxes: missiles, water bombs, bananas, shields...", 19, new Color(1, 1, 1, 0.7f), TextAnchor.MiddleLeft, 1f);
            y += 50;

            Section(x, ref y, "TRACK");
            var def = Tracks.All[GameSession.TrackIndex];
            var card = new Rect(x, y, w, 130);
            UIKit.Panel(card, new Color(0.12f, 0.17f, 0.32f, 0.9f));
            if (UIKit.Button(new Rect(card.x + 10, card.y + 35, 56, 60), "<", 30)) GameSession.TrackIndex = (GameSession.TrackIndex + Tracks.All.Length - 1) % Tracks.All.Length;
            if (UIKit.Button(new Rect(card.xMax - 66, card.y + 35, 56, 60), ">", 30)) GameSession.TrackIndex = (GameSession.TrackIndex + 1) % Tracks.All.Length;
            UIKit.Text(new Rect(card.x + 80, card.y + 10, card.width - 160, 46), def.Name, 34, Color.white, TextAnchor.MiddleCenter, 2f);
            UIKit.Text(new Rect(card.x + 80, card.y + 54, card.width - 160, 30), def.Subtitle, 16, new Color(1, 1, 1, 0.75f), TextAnchor.MiddleCenter, 1f);
            float best = GameSession.GetBest(GameSession.TrackIndex, GameSession.Mode);
            UIKit.Text(new Rect(card.x + 80, card.y + 88, card.width - 160, 30), "RECORD  " + GameSession.FormatTime(best), 20, new Color(0.6f, 0.9f, 1f), TextAnchor.MiddleCenter, 1f);
            y += 150;

            Section(x, ref y, "DIFFICULTY");
            string[] diffs = { "EASY", "NORMAL", "HARD" };
            for (int i = 0; i < 3; i++)
                if (UIKit.Button(new Rect(x + i * (w / 3f), y, w / 3f - 8, 56), diffs[i], 24, (int)GameSession.Difficulty == i)) GameSession.Difficulty = (Difficulty)i;
            y += 76;

            Section(x, ref y, "LAPS");
            int[] laps = { 1, 2, 3, 5 };
            for (int i = 0; i < laps.Length; i++)
                if (UIKit.Button(new Rect(x + i * (w / 4f), y, w / 4f - 8, 56), laps[i].ToString(), 26, GameSession.Laps == laps[i])) GameSession.Laps = laps[i];
            y += 76;

            UIKit.Text(new Rect(x, col.yMax - 64, w, 50), "WASD drive · SHIFT drift · CTRL nitro/item · R respawn · ESC pause", 17, new Color(1, 1, 1, 0.6f), TextAnchor.MiddleLeft, 1f);

            // kart panel (bottom right)
            var stats = KartStats.All[GameSession.KartIndex];
            var kp = new Rect(W - 700, H - 470, 640, 330);
            UIKit.Panel(kp, new Color(0.05f, 0.08f, 0.17f, 0.86f));
            if (UIKit.Button(new Rect(kp.x + 20, kp.y + 22, 60, 60), "<", 30)) GameSession.KartIndex = (GameSession.KartIndex + KartStats.All.Length - 1) % KartStats.All.Length;
            if (UIKit.Button(new Rect(kp.xMax - 80, kp.y + 22, 60, 60), ">", 30)) GameSession.KartIndex = (GameSession.KartIndex + 1) % KartStats.All.Length;
            UIKit.Text(new Rect(kp.x + 90, kp.y + 16, kp.width - 180, 50), stats.Name, 44, UIKit.Accent, TextAnchor.MiddleCenter, 2f);
            UIKit.Text(new Rect(kp.x + 90, kp.y + 62, kp.width - 180, 30), stats.Blurb, 19, new Color(1, 1, 1, 0.8f), TextAnchor.MiddleCenter, 1f);
            string[] names = { "SPEED", "ACCEL", "HANDLING", "DRIFT" };
            int[] vals = { stats.BarSpeed, stats.BarAccel, stats.BarHandling, stats.BarDrift };
            for (int i = 0; i < 4; i++)
            {
                float ry = kp.y + 108 + i * 36;
                UIKit.Text(new Rect(kp.x + 40, ry, 200, 30), names[i], 20, new Color(1, 1, 1, 0.8f), TextAnchor.MiddleLeft, 1f);
                for (int k = 0; k < 5; k++)
                    UIKit.Panel(new Rect(kp.x + 200 + k * 76, ry + 6, 68, 18), k < vals[i] ? Color.Lerp(new Color(0.3f, 0.75f, 1f), UIKit.Accent, k / 4f) : new Color(1, 1, 1, 0.12f));
            }
            // color swatches
            float sy = kp.yMax - 62;
            for (int i = 0; i < GameSession.Liveries.Length; i++)
            {
                var sw = new Rect(kp.x + 40 + i * 72, sy, 56, 44);
                var liv = GameSession.Liveries[i];
                UIKit.Panel(sw, liv.Primary);
                UIKit.Panel(new Rect(sw.x + 16, sw.y + 30, 24, 10), liv.Secondary);
                if (GameSession.ColorIndex == i) UIKit.Outline(new Rect(sw.x - 4, sw.y - 4, sw.width + 8, sw.height + 8), Color.white);
                if (GUI.Button(sw, GUIContent.none, GUIStyle.none)) { GameSession.ColorIndex = i; GameAudio.Play(Sfx.Click, 0.6f); }
            }

            // start
            if (UIKit.Button(new Rect(W - 700, H - 120, 640, 84), "START RACE   (ENTER)", 36, true)) StartRace();
        }

        static void Section(float x, ref float y, string title)
        {
            UIKit.Text(new Rect(x, y, 400, 30), title, 20, new Color(0.55f, 0.8f, 1f), TextAnchor.MiddleLeft, 1f);
            y += 34;
        }
    }
}
