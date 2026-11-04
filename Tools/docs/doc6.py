from docs_lib import Report, FIGS, CAPTURES as C

r = Report("Entregable 6 - Deteccion de Datos del Profiler y Generacion de APK.docx",
           "Detección de Datos del Profiler y Generación de APK",
           "Análisis de rendimiento en tres distancias de cámara · Proyecto Cenote Vivo")

r.h1("1. Metodología de medición")
r.p("Las métricas se obtuvieron de una **development build** de la aplicación, no del editor. En el editor, Unity suma el costo de sus propias ventanas y los contadores de renderizado no reflejan lo que ocurre en un ejecutable. Para que las mediciones fueran repetibles se escribió una sonda, `MetricsProbe.cs`, que solo se compila en builds de desarrollo y se activa con el argumento `-cenoteMetrics`. Al iniciar, la sonda:")
r.numbered(["Selecciona el perfil de calidad **Mobile** (el que usa Android: `Mobile_RPAsset`, MSAA 4×, LOD Bias 1) y desactiva el VSync para medir el costo real y no el límite de la pantalla.",
            "Entra a la pantalla principal y lleva la contaminación al 100 %, el escenario más exigente: lixiviados, residuos y basura flotante activos.",
            "Coloca la cámara en tres posiciones —**alejada** (vista panorámica), **acercándose** (punto intermedio) y **acercada** (primer plano de una roca a 3.5 m)—, espera 90 cuadros para estabilizar y registra 240 cuadros con `ProfilerRecorder`.",
            "Lee los contadores del Profiler: *SetPass Calls*, draw calls por cada ruta de envío (estándar, SRP Batcher, instanciado, estático), *Triangles*, *Vertices*, *Main Thread*, *Total Used Memory* y *Gfx Used Memory*; el tiempo de GPU se obtiene con `FrameTimingManager`."])
r.p("Se compilaron dos variantes de la misma escena: la **optimizada** y una **sin optimizar**, en la que se retiraron las marcas de *static batching* y la sonda fuerza LOD0 en todas las rocas y desactiva el SRP Batcher. Así cada solución puede evaluarse contra la escena de partida.")
r.note("**Equipo de medición:** Alienware m15 R6 · Intel Core i7-11800H · NVIDIA GeForce RTX 3060 Laptop · Direct3D 12 · 1600×900. Las mediciones se hicieron en PC porque al momento del análisis no había un teléfono conectado para depuración. Los conteos de draw calls, SetPass, triángulos y vértices dependen de la escena y del pipeline, no de la GPU, y son **los mismos que tendrá Android**; los tiempos (FPS, ms) y la memoria sí cambiarán en el dispositivo. La misma sonda funciona en una development build de Android para repetir la medición.")
r.figures_row([(C / "metricas_optimizado_0.png", "Alejada"), (C / "metricas_optimizado_1.png", "Acercándose"), (C / "metricas_optimizado_2.png", "Acercada")], width_cm=5.2)
r.caption("Las tres posiciones de cámara utilizadas (escena con contaminación al 100 %).")

r.h1("2. Tabla de métricas del Profiler (escena optimizada)")
r.table(["Métrica", "Cámara alejada (panorámica)", "Cámara acercándose (transición)", "Cámara acercada (primer plano)"],
        [["**FPS**", "898", "1 108", "1 162"],
         ["**Frame time (ms)**", "1.11", "0.90", "0.86"],
         ["**CPU usage** (hilo principal, ms)", "1.16", "0.98", "0.90"],
         ["**GPU usage** (tiempo de GPU, ms)", "0.36", "0.32", "0.27"],
         ["**Batches** (draw calls totales)", "109", "48", "23"],
         ["**SetPass calls**", "19", "18", "12"],
         ["**Memoria** RAM usada / gráfica", "320 MB / 163 MB", "320 MB / 163 MB", "320 MB / 163 MB"],
         ["**Vértices**", "40 564", "47 184", "34 308"],
         ["Triángulos", "39 411", "51 887", "41 793"],
         ["Desglose de draw calls", "14 estándar · 87 SRP Batcher · 4 lotes estáticos", "10 estándar · 34 SRP Batcher · 4 lotes estáticos", "9 estándar · 11 SRP Batcher · 1 lote estático"]],
        widths_cm=[4.4, 3.9, 3.9, 3.8], caption="Métricas registradas en la development build optimizada (mediana de 240 cuadros).", font_pt=9.5)
r.p("La vista alejada es la más costosa en draw calls porque abarca toda la escena: las 16 rocas, las 12 lianas, la escalera, el muelle, los 10 sistemas de lixiviados y las 26 piezas de basura. Al acercarse, el *frustum culling* descarta lo que queda fuera de cuadro y el número de batches baja de 109 a 23. Los triángulos no siguen la misma tendencia: en el punto intermedio aumentan porque las rocas más próximas pasan de LOD2 a LOD1 y LOD0, y en el primer plano vuelven a bajar porque la cámara ve menos objetos aunque los vea con todo su detalle. Este comportamiento es el que se espera de un sistema LOD.")

r.h1("3. Detección de problemas de optimización")
r.h2("3.1 Draw calls y batches elevados por elementos separados")
r.p("La escena original está formada por muchas piezas pequeñas e independientes: 10 peldaños, 6 tablones y 12 lianas, a los que el prototipo añade 16 rocas, filtraciones, partículas y basura. En la build sin optimizar cada pieza es un draw call propio con su cambio de estado. En la vista panorámica se registraron **173 batches y 25 SetPass calls**, con 95 draw calls por la ruta estándar. En un teléfono, cada draw call consume tiempo de CPU en el controlador gráfico; con varios cientos el hilo de render se convierte en el cuello de botella antes que la GPU.")
r.p("El segundo problema era geométrico: con todas las rocas en máximo detalle la vista lejana procesaba **299 807 triángulos**, siete veces más que la versión optimizada, para un resultado visual indistinguible.")
r.h2("3.2 Overdraw en partículas y capas transparentes")
r.p("El overdraw ocurre cuando un mismo píxel se sombrea varias veces en un cuadro. En esta escena hay tres fuentes potenciales: el agua (una superficie que cubre buena parte de la pantalla en la Vista 2), las filtraciones (franjas semitransparentes sobre la roca) y las partículas de lixiviados, residuos y bioluminiscencia. Al revisar el diseño se identificaron estos riesgos y se atacaron desde el material:")
r.bullets(["El agua original usaba el shader Lit en modo transparente, que mezcla con el framebuffer. El nuevo shader escribe un color **opaco** (`Blend One Zero`) con la refracción ya compuesta, por lo que se dibuja una sola vez y escribe profundidad.",
           "Las partículas se limitaron en número máximo (60 por sistema de lixiviados, 120 de residuos, 200 de bioluminiscencia) y en tamaño en pantalla, y su shader no usa texturas, de modo que cada fragmento extra es barato.",
           "Las partículas solo emiten cuando la contaminación supera su umbral (15 %, 30 %) o cuando es de noche: en el estado limpio de día su costo es nulo."])
r.p("Para una verificación píxel a píxel se recomienda el modo *Overdraw* de la vista de escena y el *Frame Debugger* de Unity conectado al teléfono, ya que el impacto del overdraw depende mucho del ancho de banda del dispositivo.")

r.h1("4. Soluciones aplicadas y justificación")
r.h2("4.1 Static Batching y SRP Batcher para los elementos estáticos de la cueva")
r.p("La caverna, la plataforma, la escalera, el muelle, las lianas y las rocas se marcaron como **Batching Static**. Al cargar la escena Unity combina sus mallas por material en búferes compartidos, de modo que muchas piezas con el mismo material se dibujan con una sola configuración de estado. Además se mantiene activo el **SRP Batcher** de URP, que conserva los datos de cada material en la GPU y reduce el costo de CPU de cada draw call aunque los objetos usen materiales distintos.")
r.h2("4.2 GPU Instancing")
r.p("Los materiales de las rocas, las filtraciones y la basura flotante tienen **GPU Instancing** habilitado. En URP el SRP Batcher tiene prioridad sobre el instancing para los objetos compatibles, así que en la mayoría de los casos los objetos repetidos se agrupan por el SRP Batcher (de ahí que los contadores muestren las instancias dentro de esa ruta). El instancing queda como respaldo si el SRP Batcher se desactiva o en materiales que no sean compatibles con él.")
r.h2("4.3 LOD Group en las rocas")
r.p("Cada roca usa tres niveles (9 826, 4 420 y 2 456 triángulos) y se descarta por debajo del 1 % de la pantalla; el nivel más bajo no proyecta sombras (detalle en el Entregable 4).")
r.h2("4.4 Compresión de texturas ASTC para Android")
r.p("Se configuraron sobrescrituras para la plataforma Android en el importador de cada textura: **ASTC 6×6** para texturas de color (roca, cueva, agua, madera, escaleras; 3.56 bits por píxel) y **ASTC 4×4** para los mapas de normales (8 bits por píxel, que preserva mejor la dirección de las normales), con un tamaño máximo de 1024 y 512 px respectivamente. El subobjetivo de compresión de la build también quedó en ASTC. Frente a una textura RGBA de 32 bits, ASTC 6×6 reduce la memoria unas nueve veces y, sobre todo, reduce el ancho de banda de lectura de texturas en cada fragmento, que es uno de los principales consumidores de energía en una GPU móvil. Todas las GPU compatibles con OpenGL ES 3.2 y Vulkan en Android admiten ASTC.")
r.h2("4.5 Verificación de la mejora")
r.table(["Métrica (vista alejada)", "Sin optimizar", "Optimizado", "Cambio"],
        [["Batches (draw calls)", "173", "109", "−37 %"],
         ["Draw calls por la ruta estándar (sin agrupar)", "95", "14", "−85 %"],
         ["SetPass calls", "25", "19", "−24 %"],
         ["Triángulos", "299 807", "39 411", "−87 %"],
         ["Vértices", "226 908", "40 564", "−82 %"],
         ["Tiempo de GPU", "0.42 ms", "0.36 ms", "−14 %"],
         ["Frame time", "1.05 ms", "1.11 ms", "+6 % (dentro del ruido)"],
         ["Memoria RAM / gráfica", "312 / 157 MB", "320 / 163 MB", "+8 / +6 MB"]],
        widths_cm=[6.2, 3.2, 3.2, 3.4], caption="Comparación entre la escena sin optimizar y la optimizada.")
r.figure(FIGS / "e6_comparativa.png", "Triángulos, batches y SetPass calls en las tres distancias, antes y después de optimizar.")
r.p("Las métricas que dependen de la escena mejoran de forma clara: la geometría se reduce casi nueve veces en la vista general y los draw calls que llegan al controlador sin agrupar bajan de 95 a 14. El tiempo de GPU baja en las tres distancias (0.42 → 0.36, 0.35 → 0.32 y 0.29 → 0.27 ms).")
r.p("Dos resultados merecen una lectura honesta. Primero, en la GPU de escritorio el **frame time** total no mejoró: a más de 900 FPS el cuadro dura alrededor de 1 ms y está dominado por costos fijos del sistema y del controlador, así que las diferencias de ±0.1 ms entre corridas son ruido. Por eso la justificación se apoya en el tiempo de GPU y en los contadores, que son los que escalan en un teléfono, donde la GPU es varias decenas de veces menos potente y el trabajo por cuadro sí determina si se alcanzan 30 o 60 FPS. Segundo, la **memoria aumenta ligeramente** (+8 MB de RAM y +6 MB gráficos) porque el static batching crea copias combinadas de las mallas y porque los tres niveles de LOD están cargados. Es el intercambio esperado de estas técnicas: un poco más de memoria a cambio de mucho menos trabajo por cuadro, aceptable en dispositivos con 4 GB de RAM o más.")

r.h1("5. Generación del APK")
r.h2("5.1 Configuración del proyecto para Android")
r.table(["Ajuste", "Valor"],
        [["Plataforma / Render Pipeline", "Android · Universal Render Pipeline (perfil de calidad Mobile)"],
         ["Nombre del producto / paquete", "Cenote Vivo · `com.marista.cenotevivo`"],
         ["Versión", "1.0.0 (bundleVersionCode 1)"],
         ["API mínima", "Android 10 (API 29)"],
         ["Backend de scripting / arquitectura", "IL2CPP · ARM64"],
         ["APIs gráficas", "Vulkan, con OpenGL ES 3 como respaldo"],
         ["Espacio de color", "Lineal"],
         ["Compresión de texturas", "ASTC"],
         ["Orientación", "Horizontal (landscape izquierda y derecha)"],
         ["Renderizado multihilo", "Activado"],
         ["Escenas incluidas", "`Assets/CenoteVivo/Scenes/CenoteVivo.unity`"]],
        widths_cm=[6.0, 10.0], caption="Player Settings configurados por CenoteVivoSetup.cs.")
r.h2("5.2 Android Manifest")
r.p("Unity genera el `AndroidManifest.xml` a partir de los Player Settings, por lo que no fue necesario un manifiesto personalizado. El manifiesto del APK compilado, leído con `aapt dump badging`, contiene:")
r.table(["Entrada del manifiesto", "Valor en el APK"],
        [["Paquete / versión", "`com.marista.cenotevivo` · versionCode 1 · versionName 1.0.0"],
         ["SDK", "minSdkVersion 29 (Android 10) · compileSdk 36"],
         ["Actividad principal", "`com.unity3d.player.UnityPlayerGameActivity`, orientación *userLandscape*"],
         ["Requisito gráfico", "`uses-gl-es 0x30000` (OpenGL ES 3.0 como mínimo)"],
         ["Código nativo", "arm64-v8a"],
         ["Permisos", "`INTERNET` (agregado por Unity de forma predeterminada) y un permiso interno de receptores no exportados"]],
        widths_cm=[4.6, 11.4], caption="Contenido verificado del AndroidManifest.xml.")
r.p("La aplicación no solicita permisos peligrosos (cámara, ubicación, micrófono ni almacenamiento), por lo que se instala sin diálogos de permisos. El permiso `INTERNET` es de nivel normal y la aplicación no lo utiliza; si se quisiera eliminar, se puede agregar un manifiesto personalizado con `tools:node=\"remove\"`.")
r.h2("5.3 Proceso de compilación")
r.p("La compilación se automatizó en `CenoteBuild.cs`, accesible desde el menú **Cenote Vivo › Compilar APK (Android)** o desde la línea de comandos:")
r.code('''"C:\\Program Files\\Unity\\Hub\\Editor\\6000.5.7f1\\Editor\\Unity.exe" -batchmode -quit ^
    -projectPath "C:\\Users\\Diego\\ejercicios" ^
    -executeMethod CenoteVivo.EditorTools.CenoteBuild.BuildApk -logFile build_android.log''', caption="Compilación del APK en modo batch.")
r.p("El método verifica que el módulo de Android esté instalado, cambia la plataforma activa, fija la compresión ASTC, desactiva el formato AAB (para obtener un `.apk` instalable directamente) y genera `Builds/Android/CenoteVivo.apk`. El APK se instala en el teléfono con `adb install -r Builds/Android/CenoteVivo.apk` o copiándolo al dispositivo con la instalación de orígenes desconocidos habilitada.")
r.p("Antes de compilar se instaló en Unity Hub el módulo *Android Build Support* para la versión 6000.5.7f1, junto con *OpenJDK* y *Android SDK & NDK Tools* (SDK Platforms 34, 36 y 37, Build Tools, NDK y CMake).")
r.table(["Resultado de la compilación", "Valor"],
        [["Estado", "**Succeeded** · 0 errores"],
         ["Archivo", "`Builds/Android/CenoteVivo.apk`"],
         ["Tamaño del APK", "54.2 MB (libil2cpp.so 14.7 MB y libunity.so 9.8 MB comprimidos; datos y shaders el resto)"],
         ["Tiempo de compilación", "779 s (primera compilación: incluye la reimportación de todos los assets para Android y la compilación de variantes de shader para Vulkan y GLES3)"],
         ["Backend / ABI", "IL2CPP · arm64-v8a"]],
        widths_cm=[5.0, 11.0], caption="Resultado de la compilación del APK.")
r.p("El reporte de Unity indica un «Complete build size» de 840 MB, pero ese valor suma los datos sin comprimir y todas las variantes de shader antes de empaquetar; el archivo real que se instala pesa 54 MB. El reporte también mostró que la mayor parte de los recursos sin usar provienen del paquete `com.unity.ai.inference` (Sentis), que el proyecto no utiliza: quitarlo del *Package Manager* es la siguiente optimización de tamaño recomendada.")
r.h2("5.4 Verificación en el dispositivo")
r.p("El APK se instaló en un teléfono Android y la aplicación se ejecutó correctamente: abre el menú de inicio, entra a la escena y responden los controles de día/noche, contaminación y cambio de vista. Esta prueba fue funcional; no se registraron métricas del Profiler en el teléfono.")
r.p("Para documentar el rendimiento en el dispositivo, la ejecución fluida se confirma con una development build de Android (*Development Build* y *Autoconnect Profiler* activos) y el Profiler conectado por USB, o ejecutando la misma sonda con `adb shell am start` y el argumento `-cenoteMetrics`. Los criterios de aceptación propuestos para un teléfono de gama media son: 30 FPS estables (frame time ≤ 33 ms) en las tres vistas con la contaminación al 100 %, sin caídas al cambiar de vista, y sin *thermal throttling* tras cinco minutos de uso. Los conteos de este documento —menos de 110 batches y unos 50 000 triángulos en el peor caso— están muy por debajo de los límites habituales para ese tipo de dispositivos (alrededor de 200–300 draw calls y varios cientos de miles de triángulos por cuadro).")

r.h1("6. Conclusiones")
r.p("El Profiler permitió pasar de la intuición a los datos. La escena original, con muchas piezas pequeñas y rocas siempre en máximo detalle, generaba 173 batches y casi 300 000 triángulos en la vista general. Con static batching, SRP Batcher, LOD Group y texturas ASTC, esa misma vista se dibuja con 109 batches, 19 cambios de estado y unos 40 000 triángulos, con un tiempo de GPU 14 % menor y a cambio de unos pocos megabytes de memoria. El APK se compiló sin errores (54 MB, IL2CPP ARM64) y queda listo para instalarse en un teléfono Android 10 o superior.")
print(r.save())
