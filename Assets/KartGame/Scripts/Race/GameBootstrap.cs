using UnityEngine;

namespace KartGame
{
    /// <summary>
    /// Scene entry point. The single scene is either the menu showroom or a race,
    /// chosen by <see cref="GameSession.GoRace"/>; everything is generated at runtime.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        [Tooltip("Skip the menu and start racing immediately (handy for testing).")]
        public bool autoStartRace;

        void Awake()
        {
            Application.runInBackground = true;
            Application.targetFrameRate = 60;
            Time.fixedDeltaTime = 1f / 60f;
            QualitySettings.antiAliasing = 4;
            QualitySettings.shadowDistance = 110f;
            QualitySettings.shadowCascades = 2;
            QualitySettings.shadowResolution = ShadowResolution.High;
            QualitySettings.lodBias = 2f;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
            Physics.defaultContactOffset = 0.02f;
            var cam = Camera.main;
            if (cam != null)
            {
                cam.allowHDR = true;
                if (cam.GetComponent<PostFX>() == null) cam.gameObject.AddComponent<PostFX>();
            }
        }

        void Start()
        {
            if (autoStartRace) GameSession.GoRace = true;
#if UNITY_EDITOR
            if (UnityEditor.SessionState.GetBool("KartGame.QuickRace", false))
            {
                UnityEditor.SessionState.SetBool("KartGame.QuickRace", false);
                GameSession.GoRace = true;
            }
#endif
            autoStartRace = false;
            if (GameSession.GoRace)
            {
                var rm = new GameObject("RaceManager").AddComponent<RaceManager>();
                rm.Begin(GameSession.TrackIndex, GameSession.Mode, GameSession.Laps, GameSession.Difficulty,
                         GameSession.KartIndex, GameSession.ColorIndex);
            }
            else
            {
                gameObject.AddComponent<MenuScreen>();
            }
        }
    }
}
