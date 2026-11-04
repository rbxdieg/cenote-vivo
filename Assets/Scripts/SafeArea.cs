using UnityEngine;

namespace CenoteVivo
{
    // Ajusta el RectTransform al área segura de la pantalla (muescas y esquinas redondeadas).
    [RequireComponent(typeof(RectTransform))]
    public class SafeArea : MonoBehaviour
    {
        Rect applied;

        void Update()
        {
            var area = Screen.safeArea;
            if (area == applied || Screen.width == 0 || Screen.height == 0) return;
            applied = area;
            var rt = (RectTransform)transform;
            rt.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
            rt.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
        }
    }
}
