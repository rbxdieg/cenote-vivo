# Cenote Vivo — guía del proyecto

App Android (Unity 6000.5.7f1 · URP) para concientizar sobre la contaminación del agua subterránea en los cenotes.
La escena original `Assets/Scenes/CENOTE.unity` **no se modifica**: todo se construye en una copia.

## Estructura

```
Assets/
  Models/Rocas.fbx                  rocas roca/roca2 con _LOD0.._LOD2 (generadas en Blender)
  CenoteVivo/
    Scenes/CenoteVivo.unity         escena final (menú + HUD + escena 3D)
    Scripts/                        DayNightController, PollutionSimulator, PerspectiveCameraController,
                                    UIManager, UnderwaterFog, FloatingDebris, SafeArea, AwarenessContent,
                                    MetricsProbe (solo development builds)
    Shaders/                        CenoteWater (agua), CenoteSeep (filtraciones), ParticleSoft (partículas)
    Materials/ Prefabs/ Textures/   generados por el setup (roca.prefab y roca2.prefab con LOD Group)
    Editor/                         CenoteVivoSetup (construye la escena), CenoteCapture (capturas),
                                    CenoteBuild (APK y players de medición), CenoteInspector
Tools/
  blender/make_rocas.py             genera las rocas, aplica Decimate y exporta el FBX
  blender/render_lods.py            renders alámbricos de evidencia
  captures/                         capturas y métricas usadas en los entregables
  docs/                             scripts que generan los 6 .docx a partir de PORTADA.docx
Entregables/                        los 6 documentos Word
```

## Menú "Cenote Vivo" en Unity

| Opción | Qué hace |
|---|---|
| Construir escena | Regenera `CenoteVivo.unity`, materiales, prefabs LOD, UI y Player Settings de Android |
| Capturas y métricas | Entra a Play Mode y guarda capturas en `Tools/captures` |
| Compilar APK (Android) | Genera `Builds/Android/CenoteVivo.apk` (requiere el módulo Android) |

## Compilar el APK

1. Unity Hub → Installs → 6000.5.7f1 → Add modules → **Android Build Support** (con OpenJDK y Android SDK & NDK Tools).
2. Abrir el proyecto y usar *Cenote Vivo → Compilar APK (Android)*, o en consola:

```
"C:\Program Files\Unity\Hub\Editor\6000.5.7f1\Editor\Unity.exe" -batchmode -quit -projectPath "C:\Users\Diego\ejercicios" -executeMethod CenoteVivo.EditorTools.CenoteBuild.BuildApk -logFile build_android.log
```

## Regenerar los entregables

```
cd Tools/docs
py figs.py
py doc1.py   (… doc6.py)
```
