using System;
using UnityEngine;

namespace CenoteVivo
{
    // Función principal: lleva el cenote de "Limpio" a "Contaminado".
    // El slider fija un objetivo y el nivel real lo alcanza de forma gradual, de modo que
    // el agua se oscurece y enturbia poco a poco y las filtraciones avanzan por la roca.
    public class PollutionSimulator : MonoBehaviour
    {
        [Header("Agua")]
        [SerializeField] Renderer waterRenderer;
        [SerializeField, Range(0f, 1f)] float cleanTurbidity = 0.05f;

        [Header("Efectos de contaminación")]
        [Tooltip("Goteo de lixiviados que escurre desde las fisuras de la roca hacia el agua.")]
        [SerializeField] ParticleSystem[] leachates;
        [SerializeField] float maxLeachateRate = 18f;
        [Tooltip("Residuos y espuma sobre la superficie del agua.")]
        [SerializeField] ParticleSystem floatingTrash;
        [SerializeField] float maxTrashRate = 6f;
        [SerializeField] FloatingDebris debris;

        [Header("Transición")]
        [SerializeField, Range(0f, 1f)] float target;
        [Tooltip("Unidades de contaminación por segundo.")]
        [SerializeField] float changeSpeed = 0.35f;

        static readonly int PollutionId = Shader.PropertyToID("_PollutionLevel");
        static readonly int TurbidityId = Shader.PropertyToID("_Turbidity");
        static readonly int GlobalPollutionId = Shader.PropertyToID("_CenotePollution");

        public float Level { get; private set; }
        public float Target => target;
        public bool IsPolluted => target >= 0.5f;
        public event Action<float> OnLevelChanged;

        Material waterMaterial;

        void Awake()
        {
            // Instancia propia para no modificar el asset del material en el editor.
            if (waterRenderer != null) waterMaterial = waterRenderer.material;
            Level = target;
            Apply();
        }

        void OnDestroy()
        {
            Shader.SetGlobalFloat(GlobalPollutionId, 0f);
            if (waterMaterial != null) Destroy(waterMaterial);
        }

        // Llamado por el slider de la UI (0 = limpio, 1 = contaminado).
        public void SetTarget(float value) => target = Mathf.Clamp01(value);

        // Llamado por el botón: alterna entre los dos extremos.
        public void Toggle() => SetTarget(IsPolluted ? 0f : 1f);

        public void SetImmediate(float value)
        {
            target = Level = Mathf.Clamp01(value);
            Apply();
        }

        void Update()
        {
            if (Mathf.Approximately(Level, target)) return;
            Level = Mathf.MoveTowards(Level, target, changeSpeed * Time.deltaTime);
            Apply();
        }

        void Apply()
        {
            // Curva suave: los primeros contaminantes casi no se notan; luego el deterioro se acelera.
            float eased = Level * Level * (3f - 2f * Level);
            if (waterMaterial != null)
            {
                waterMaterial.SetFloat(PollutionId, eased);
                waterMaterial.SetFloat(TurbidityId, Mathf.Lerp(cleanTurbidity, 1f, eased));
            }
            Shader.SetGlobalFloat(GlobalPollutionId, eased);

            foreach (var ps in leachates) SetRate(ps, maxLeachateRate * Mathf.InverseLerp(0.15f, 1f, Level));
            SetRate(floatingTrash, maxTrashRate * Mathf.InverseLerp(0.3f, 1f, Level));
            if (debris != null) debris.Amount = Mathf.InverseLerp(0.35f, 1f, Level);

            OnLevelChanged?.Invoke(Level);
        }

        static void SetRate(ParticleSystem ps, float rate)
        {
            if (ps == null) return;
            var emission = ps.emission;
            emission.rateOverTime = rate;
            if (rate > 0f && !ps.isPlaying) ps.Play();
        }
    }
}
