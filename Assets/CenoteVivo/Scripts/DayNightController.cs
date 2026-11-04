using System;
using System.Collections;
using UnityEngine;

namespace CenoteVivo
{
    // Alterna la iluminación global entre día y noche con una transición suave:
    // rotación, temperatura de color e intensidad de la luz direccional, luz ambiental,
    // exposición del cielo y bioluminiscencia sutil en el agua.
    public class DayNightController : MonoBehaviour
    {
        [Serializable]
        public struct LightingProfile
        {
            public Vector3 sunEuler;
            [Range(1500f, 20000f)] public float colorTemperature;
            public Color filterColor;
            public float intensity;
            public Color ambientSky;
            public Color ambientEquator;
            public Color ambientGround;
            public float skyExposure;
            public Color cameraBackground;
        }

        [Header("Referencias")]
        [SerializeField] Light sun;
        [SerializeField] Camera targetCamera;
        [SerializeField] ParticleSystem bioluminescence;
        [SerializeField] Light bioluminescenceLight;
        [SerializeField] PollutionSimulator pollution;

        [Header("Perfiles")]
        [SerializeField] LightingProfile day = new LightingProfile
        {
            sunEuler = new Vector3(62f, 35f, 0f),
            colorTemperature = 5600f,
            filterColor = Color.white,
            intensity = 2.2f,
            ambientSky = new Color(0.62f, 0.72f, 0.82f),
            ambientEquator = new Color(0.42f, 0.46f, 0.44f),
            ambientGround = new Color(0.20f, 0.19f, 0.16f),
            skyExposure = 1.25f,
            cameraBackground = new Color(0.55f, 0.75f, 0.95f)
        };
        [SerializeField] LightingProfile night = new LightingProfile
        {
            sunEuler = new Vector3(38f, 215f, 0f),
            colorTemperature = 9500f,
            filterColor = new Color(0.70f, 0.80f, 1f),
            intensity = 0.28f,
            ambientSky = new Color(0.05f, 0.07f, 0.14f),
            ambientEquator = new Color(0.03f, 0.04f, 0.07f),
            ambientGround = new Color(0.01f, 0.01f, 0.02f),
            skyExposure = 0.08f,
            cameraBackground = new Color(0.02f, 0.03f, 0.07f)
        };

        [SerializeField, Min(0.05f)] float transitionDuration = 2.5f;
        [SerializeField] float maxBioEmission = 25f;
        [SerializeField] float maxBioLightIntensity = 3f;

        static readonly int NightId = Shader.PropertyToID("_CenoteNight");
        static readonly int ExposureId = Shader.PropertyToID("_Exposure");

        public bool IsNight { get; private set; }
        public float NightBlend { get; private set; }
        public event Action<bool> OnModeChanged;

        Material skyboxInstance;
        Coroutine transition;

        void Awake()
        {
            if (sun != null) sun.useColorTemperature = true;
            if (RenderSettings.skybox != null)
            {
                skyboxInstance = new Material(RenderSettings.skybox);
                RenderSettings.skybox = skyboxInstance;
            }
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            Apply(0f);
        }

        void OnDestroy()
        {
            Shader.SetGlobalFloat(NightId, 0f);
        }

        public void Toggle() => SetNight(!IsNight);

        public void SetNight(bool value, bool instant = false)
        {
            IsNight = value;
            if (transition != null) StopCoroutine(transition);
            if (instant) Apply(value ? 1f : 0f);
            else transition = StartCoroutine(Transition(value ? 1f : 0f));
            OnModeChanged?.Invoke(value);
        }

        IEnumerator Transition(float target)
        {
            float start = NightBlend;
            for (float t = 0f; t < 1f; t += Time.deltaTime / transitionDuration)
            {
                Apply(Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t)));
                yield return null;
            }
            Apply(target);
            transition = null;
        }

        void Update()
        {
            // La bioluminiscencia depende también de la contaminación, que cambia de forma independiente.
            UpdateBioluminescence();
        }

        void Apply(float n)
        {
            NightBlend = n;
            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Slerp(Quaternion.Euler(day.sunEuler), Quaternion.Euler(night.sunEuler), n);
                sun.colorTemperature = Mathf.Lerp(day.colorTemperature, night.colorTemperature, n);
                sun.color = Color.Lerp(day.filterColor, night.filterColor, n);
                sun.intensity = Mathf.Lerp(day.intensity, night.intensity, n);
            }
            RenderSettings.ambientSkyColor = Color.Lerp(day.ambientSky, night.ambientSky, n);
            RenderSettings.ambientEquatorColor = Color.Lerp(day.ambientEquator, night.ambientEquator, n);
            RenderSettings.ambientGroundColor = Color.Lerp(day.ambientGround, night.ambientGround, n);
            if (skyboxInstance != null && skyboxInstance.HasProperty(ExposureId))
                skyboxInstance.SetFloat(ExposureId, Mathf.Lerp(day.skyExposure, night.skyExposure, n));
            if (targetCamera != null)
                targetCamera.backgroundColor = Color.Lerp(day.cameraBackground, night.cameraBackground, n);
            Shader.SetGlobalFloat(NightId, n);
            UpdateBioluminescence();
        }

        void UpdateBioluminescence()
        {
            float clean = pollution != null ? 1f - pollution.Level : 1f;
            float glow = NightBlend * NightBlend * clean;
            if (bioluminescence != null)
            {
                var emission = bioluminescence.emission;
                emission.rateOverTime = maxBioEmission * glow;
                if (glow > 0.01f && !bioluminescence.isPlaying) bioluminescence.Play();
            }
            if (bioluminescenceLight != null)
            {
                bioluminescenceLight.intensity = maxBioLightIntensity * glow;
                bioluminescenceLight.enabled = glow > 0.01f;
            }
        }
    }
}
