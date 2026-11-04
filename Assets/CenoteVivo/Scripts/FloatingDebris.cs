using UnityEngine;

namespace CenoteVivo
{
    // Basura flotante (botellas y bolsas) generada una sola vez y mostrada según Amount.
    // Comparte materiales con GPU Instancing y mueve las piezas con un vaivén barato en CPU.
    public class FloatingDebris : MonoBehaviour
    {
        [System.Serializable]
        public struct Kind
        {
            public Mesh mesh;
            public Material material;
            public Vector3 scale;
            [Tooltip("Inclinación sobre X: 90 = acostado (botella), 0 = plano (bolsa).")]
            public float tilt;
        }

        [SerializeField] Kind[] kinds;
        [SerializeField] int count = 24;
        [SerializeField] float radius = 10f;
        [SerializeField] int seed = 12;

        public float Amount { get; set; }

        Transform[] pieces;
        Vector3[] basePos;
        float[] phase;
        float[] threshold;

        void Start()
        {
            var rng = new System.Random(seed);
            pieces = new Transform[count];
            basePos = new Vector3[count];
            phase = new float[count];
            threshold = new float[count];
            for (int i = 0; i < count; i++)
            {
                var go = new GameObject("Basura_" + i);
                go.transform.SetParent(transform, false);
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                float r = Mathf.Sqrt((float)rng.NextDouble()) * radius;
                basePos[i] = new Vector3(Mathf.Cos(a) * r, 0.02f, Mathf.Sin(a) * r);
                var kind = kinds[i % kinds.Length];
                go.transform.localPosition = basePos[i];
                go.transform.localRotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f)
                                           * Quaternion.Euler(kind.tilt + (float)rng.NextDouble() * 16f - 8f, 0f, 0f);
                go.transform.localScale = kind.scale * Mathf.Lerp(0.7f, 1.3f, (float)rng.NextDouble());
                go.AddComponent<MeshFilter>().sharedMesh = kind.mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = kind.material;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                pieces[i] = go.transform;
                phase[i] = (float)rng.NextDouble() * 10f;
                threshold[i] = (float)i / count;
                go.SetActive(false);
            }
        }

        void Update()
        {
            if (pieces == null) return;
            float t = Time.time;
            for (int i = 0; i < pieces.Length; i++)
            {
                bool visible = Amount > threshold[i];
                if (pieces[i].gameObject.activeSelf != visible) pieces[i].gameObject.SetActive(visible);
                if (!visible) continue;
                var p = basePos[i];
                p.y += Mathf.Sin(t * 1.3f + phase[i]) * 0.04f;
                p.x += Mathf.Sin(t * 0.11f + phase[i]) * 0.6f;
                pieces[i].localPosition = p;
            }
        }
    }
}
