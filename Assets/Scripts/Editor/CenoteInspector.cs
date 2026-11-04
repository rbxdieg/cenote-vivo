using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CenoteVivo.EditorTools
{
    // Vuelca al log la jerarquía, bounds y materiales de una escena (uso en batchmode).
    public static class CenoteInspector
    {
        // Abre la escena del prototipo (uso: Unity.exe -projectPath ... -executeMethod CenoteVivo.EditorTools.CenoteInspector.OpenCenoteVivo).
        [MenuItem("Cenote Vivo/Abrir escena")]
        public static void OpenCenoteVivo()
        {
            EditorSceneManager.OpenScene(CenoteVivoSetup.ScenePath, OpenSceneMode.Single);
        }

        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/CENOTE.unity");
            var sb = new StringBuilder("INSPECT\n");
            foreach (var root in scene.GetRootGameObjects())
                Dump(root.transform, 0, sb);
            Debug.Log(sb.ToString());
        }

        static void Dump(Transform t, int depth, StringBuilder sb)
        {
            sb.Append(' ', depth * 2).Append(t.name)
              .Append(" pos=").Append(t.position.ToString("F2"))
              .Append(" rot=").Append(t.eulerAngles.ToString("F1"))
              .Append(" scl=").Append(t.lossyScale.ToString("F2"));
            var r = t.GetComponent<Renderer>();
            if (r != null)
            {
                sb.Append(" bounds=").Append(r.bounds.center.ToString("F2")).Append(" size=").Append(r.bounds.size.ToString("F2"));
                foreach (var m in r.sharedMaterials)
                    sb.Append(" mat=").Append(m ? m.name + "(" + m.shader.name + ")" : "null");
            }
            var l = t.GetComponent<Light>();
            if (l != null) sb.Append(" light=").Append(l.type).Append(" I=").Append(l.intensity).Append(" range=").Append(l.range);
            var c = t.GetComponent<Camera>();
            if (c != null) sb.Append(" cam fov=").Append(c.fieldOfView).Append(" far=").Append(c.farClipPlane);
            sb.Append('\n');
            if (depth < 1)
                foreach (Transform ch in t) Dump(ch, depth + 1, sb);
        }
    }
}
