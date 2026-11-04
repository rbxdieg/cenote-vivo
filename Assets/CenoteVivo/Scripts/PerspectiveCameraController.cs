using System;
using System.Collections;
using UnityEngine;

namespace CenoteVivo
{
    // Cambia de forma suave entre tres puntos de vista fijos:
    // 1) Panorámica aérea, 2) Nivel del agua, 3) Corte subterráneo / sumergido.
    public class PerspectiveCameraController : MonoBehaviour
    {
        [Serializable]
        public struct ViewPoint
        {
            public string label;
            public Transform anchor;
            [Range(20f, 100f)] public float fieldOfView;
            [Tooltip("Objetos que solo se muestran en esta vista (p. ej. el corte de estratos).")]
            public GameObject[] showOnlyHere;
        }

        [SerializeField] Camera cam;
        [SerializeField] ViewPoint[] views = new ViewPoint[3];
        [SerializeField, Min(0.05f)] float transitionDuration = 1.6f;
        [SerializeField] AnimationCurve easing = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        [Header("Órbita lenta (menú de inicio)")]
        [SerializeField] Transform orbitPivot;
        [SerializeField] float orbitDegreesPerSecond = 4f;

        public int Current { get; private set; }
        public string CurrentLabel => views.Length > 0 ? views[Current].label : string.Empty;
        public bool Orbiting { get; set; }
        public event Action<int, string> OnViewChanged;

        Coroutine move;

        void Start()
        {
            GoTo(0, instant: true);
        }

        public void Next() => GoTo((Current + 1) % views.Length);

        public void GoTo(int index, bool instant = false)
        {
            if (views.Length == 0 || cam == null) return;
            Current = Mathf.Clamp(index, 0, views.Length - 1);
            if (move != null) StopCoroutine(move);
            var v = views[Current];
            if (instant)
            {
                cam.transform.SetPositionAndRotation(v.anchor.position, v.anchor.rotation);
                cam.fieldOfView = v.fieldOfView;
                RefreshExclusiveObjects();
            }
            else move = StartCoroutine(Move(v));
            OnViewChanged?.Invoke(Current, v.label);
        }

        IEnumerator Move(ViewPoint v)
        {
            Vector3 p0 = cam.transform.position;
            Quaternion r0 = cam.transform.rotation;
            float f0 = cam.fieldOfView;
            // Los objetos exclusivos de la vista destino aparecen al inicio para que se vean llegar.
            RefreshExclusiveObjects();
            for (float t = 0f; t < 1f; t += Time.deltaTime / transitionDuration)
            {
                float k = easing.Evaluate(t);
                cam.transform.SetPositionAndRotation(Vector3.Lerp(p0, v.anchor.position, k), Quaternion.Slerp(r0, v.anchor.rotation, k));
                cam.fieldOfView = Mathf.Lerp(f0, v.fieldOfView, k);
                yield return null;
            }
            cam.transform.SetPositionAndRotation(v.anchor.position, v.anchor.rotation);
            cam.fieldOfView = v.fieldOfView;
            move = null;
        }

        void RefreshExclusiveObjects()
        {
            for (int i = 0; i < views.Length; i++)
            {
                if (views[i].showOnlyHere == null) continue;
                foreach (var go in views[i].showOnlyHere)
                    if (go != null) go.SetActive(i == Current);
            }
        }

        void LateUpdate()
        {
            if (!Orbiting || move != null || orbitPivot == null) return;
            cam.transform.RotateAround(orbitPivot.position, Vector3.up, orbitDegreesPerSecond * Time.deltaTime);
        }
    }
}
