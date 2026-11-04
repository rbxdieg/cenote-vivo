using UnityEngine;

namespace CenoteVivo
{
    // Activa niebla exponencial cuando la cámara queda bajo la superficie del agua.
    // La densidad y el color siguen el nivel de contaminación: el agua sucia limita la visibilidad.
    public class UnderwaterFog : MonoBehaviour
    {
        [SerializeField] Camera cam;
        [SerializeField] Transform waterSurface;
        [SerializeField] PollutionSimulator pollution;
        [SerializeField] DayNightController dayNight;
        [SerializeField] Color cleanColor = new Color(0.05f, 0.42f, 0.48f);
        [SerializeField] Color pollutedColor = new Color(0.17f, 0.15f, 0.06f);
        [SerializeField] float cleanDensity = 0.035f;
        [SerializeField] float pollutedDensity = 0.085f;

        bool savedFog;
        Color savedColor;
        FogMode savedMode;
        float savedDensity;
        bool underwater;

        void LateUpdate()
        {
            bool below = cam.transform.position.y < waterSurface.position.y;
            if (below != underwater)
            {
                underwater = below;
                if (below)
                {
                    savedFog = RenderSettings.fog; savedColor = RenderSettings.fogColor;
                    savedMode = RenderSettings.fogMode; savedDensity = RenderSettings.fogDensity;
                    RenderSettings.fog = true;
                    RenderSettings.fogMode = FogMode.ExponentialSquared;
                }
                else
                {
                    RenderSettings.fog = savedFog; RenderSettings.fogColor = savedColor;
                    RenderSettings.fogMode = savedMode; RenderSettings.fogDensity = savedDensity;
                }
            }
            if (!underwater) return;

            float p = pollution != null ? pollution.Level : 0f;
            float light = dayNight != null ? Mathf.Lerp(1f, 0.25f, dayNight.NightBlend) : 1f;
            RenderSettings.fogColor = Color.Lerp(cleanColor, pollutedColor, p) * light;
            RenderSettings.fogDensity = Mathf.Lerp(cleanDensity, pollutedDensity, p * p);
        }
    }
}
