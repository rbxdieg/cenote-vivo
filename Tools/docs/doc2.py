from docs_lib import Report, FIGS, CAPTURES

r = Report("Entregable 2 - Ejercicio Tecnicas de Anti-Aliasing.docx",
           "Ejercicio: Técnicas de Anti-Aliasing",
           "Comparación aplicada en la escena del proyecto Cenote Vivo (Unity 6 · URP · perfil Mobile)")

r.h1("1. Marco teórico")
r.p("Una imagen digital es una cuadrícula de muestras. Cuando la escena contiene transiciones más abruptas de lo que esa cuadrícula puede representar —el borde de una roca contra el fondo, una liana de pocos centímetros de grosor vista a varios metros— la señal se submuestrea y aparece el **aliasing**: escalones en los bordes inclinados, líneas finas que se cortan y patrones que parpadean cuando la cámara se mueve.")
r.p("Las técnicas de anti-aliasing se agrupan según el momento en que actúan. Los **métodos de reconstrucción** obtienen más información de la escena durante la rasterización: el *supersampling* (SSAA) sombrea varias muestras por píxel y el *multisampling* (MSAA) toma varias muestras de cobertura y profundidad pero ejecuta el fragment shader una sola vez por píxel, por lo que suaviza las siluetas a un costo menor. Los **métodos de filtrado** trabajan sobre la imagen ya generada: FXAA busca contrastes de luminancia y difumina a lo largo del borde detectado, sin información geométrica adicional. Los **métodos temporales** como TAA reparten las muestras en el tiempo: desplazan la proyección una fracción de píxel en cada cuadro (*jitter*) y mezclan el resultado con los cuadros anteriores reproyectados mediante vectores de movimiento, con lo que corrigen también el parpadeo del sombreado.")
r.p("En Unity URP, MSAA se configura en el *Render Pipeline Asset* (`Mobile_RPAsset`, campo *Anti Aliasing (MSAA)*), mientras que FXAA, SMAA y TAA se eligen por cámara en *Rendering › Anti-aliasing* y solo se ejecutan cuando la cámara tiene activo el post-proceso.")

r.h1("2. Metodología")
r.p("Para que la comparación fuera válida se fijaron condiciones idénticas en las cuatro técnicas (más la variante MSAA 2×):")
r.bullets(["**Escena y encuadre:** `CenoteVivo.unity`, Vista 2 «Nivel del agua», agua limpia, iluminación de día. El encuadre contiene los bordes que más sufren el aliasing: siluetas de las rocas con LOD, lianas verticales de 15 cm de grosor, los tablones del muelle y la superficie del agua con normales animadas.",
           "**Tiempo congelado** (`Time.timeScale = 0`) durante las capturas, de modo que el oleaje y las partículas son idénticos en todas las imágenes.",
           "**Perfil de calidad Mobile** (`Mobile_RPAsset`, LOD Bias 1), el mismo que usa el APK de Android, renderizado a 1600×900.",
           "**Capturas** generadas por la herramienta `CenoteCapture.cs` (Play Mode), que cambia la técnica, deja correr 24 cuadros para que TAA acumule historial y guarda la imagen sin la interfaz.",
           "**Mediciones de costo** obtenidas en una *development build* de Windows con la sonda `MetricsProbe.cs`: 90 cuadros de calentamiento y 240 cuadros medidos por técnica, tiempo de GPU con `FrameTimingManager` y memoria gráfica con el contador *Gfx Used Memory* del Profiler."])
r.note("**Alcance de los números.** Las mediciones se tomaron en el equipo de desarrollo (NVIDIA RTX 3060 Laptop, Direct3D 12). Los valores absolutos de un teléfono serán mayores; lo que sí es transferible es la comparación relativa entre técnicas y la memoria que cada una reserva, que depende de la resolución y no de la GPU.")

r.figure(CAPTURES / "aa_sin_aa.png", "Encuadre de referencia (Vista 2, sin anti-aliasing). Las zonas ampliadas en las figuras siguientes son la roca con la liana del centro y los tablones del muelle.")

r.h1("3. Análisis de las capturas")
r.figure(CAPTURES / "aa_comparativa_liana_roca.png", "Ampliación ×3 (vecino más cercano) de la silueta de una roca y una liana con cada técnica.")
r.figure(CAPTURES / "aa_comparativa_tablones.png", "Ampliación ×3 de las aristas de los tablones del muelle sobre el agua.")

r.h2("3.1 Sin AA")
r.p("La silueta de la roca muestra escalones de un píxel de alto en todos los tramos inclinados y la liana, al ser casi vertical, presenta un borde duro sin transición con el fondo. En los tablones, las aristas diagonales forman la típica escalera de dientes de sierra. En movimiento este es el caso más molesto: al girar la cámara los escalones «caminan» a lo largo de los bordes y las lianas más lejanas se fragmentan porque ocupan menos de un píxel de ancho. Es la referencia de costo mínimo: 0.268 ms de GPU y 128 MB de memoria gráfica.")

r.h2("3.2 MSAA 2× y 4×")
r.p("Con MSAA los píxeles del borde reciben un color intermedio proporcional a cuántas muestras cubre la geometría, y la escalera se convierte en un degradado. Con 2× la mejora ya es clara; con 4× las siluetas de la roca y los tablones se ven continuas. La textura interior de la roca y del agua permanece **exactamente igual de nítida**, porque MSAA solo actúa donde hay un borde geométrico: la diferencia media de píxeles respecto a la imagen sin AA fue de apenas 0.3 niveles (sobre 255), concentrada en las siluetas.")
r.p("El costo se paga en memoria del framebuffer: cada muestra adicional almacena color y profundidad. En la medición, MSAA 2× elevó la memoria gráfica de 128.0 a 142.1 MB y MSAA 4× a 163.2 MB (+35 MB). El tiempo de GPU pasó de 0.268 a 0.324 y 0.345 ms. Su limitación es que no corrige el aliasing de sombreado: los brillos especulares del agua pueden seguir parpadeando.")

r.h2("3.3 FXAA")
r.p("FXAA suaviza los escalones de la roca y de los tablones con un costo de cálculo pequeño, pero al no distinguir entre un borde geométrico y un contraste de textura también **difumina el detalle interno**: la superficie rocosa y las ondas del agua pierden nitidez y la imagen se ve ligeramente «lavada». La diferencia media respecto a la referencia fue de unos 5 niveles por canal, casi veinte veces más que MSAA, lo que refleja ese desenfoque generalizado.")
r.p("Aunque el filtro en sí es barato, en URP activarlo obliga a habilitar el post-proceso de la cámara, lo que añade texturas intermedias (de 7 a 22 *render textures*) y una pasada a pantalla completa. En la prueba fue más costoso que MSAA 4× en tiempo de GPU (0.471 ms) aunque usó menos memoria (137.5 MB).")

r.h2("3.4 TAA")
r.p("TAA produjo los bordes más estables de la imagen fija: la silueta de la roca y las aristas del muelle quedan suavizadas y, al acumular varias muestras desplazadas, también se reduce el parpadeo de los reflejos especulares del agua, que ninguna otra técnica corrige. A cambio presenta un desenfoque general similar al de FXAA y es la opción más cara: 0.643 ms de GPU (2.4 veces la referencia) y 148.1 MB de memoria, debido a los búferes de historial y de vectores de movimiento.")
r.p("Su problema característico es el **ghosting**. La superficie del agua del cenote se anima desplazando normales en el shader, sin mover los vértices; por tanto, el búfer de vectores de movimiento no registra ese desplazamiento y TAA reproyecta el historial como si el agua estuviera quieta. En movimiento esto se percibe como estelas y como una pérdida del detalle de las ondas. Las capturas de este ejercicio se tomaron con el tiempo congelado para comparar en igualdad de condiciones, por lo que muestran a TAA en su mejor caso; el ghosting debe evaluarse en el dispositivo con la cámara y el agua en movimiento. Se puede mitigar reduciendo el peso del historial o excluyendo el agua de TAA, pero eso añade complejidad.")

r.h1("4. Resultados cuantitativos")
r.table(["Técnica", "FPS", "Frame (ms)", "GPU (ms)", "Δ GPU", "Memoria gráfica (MB)", "Render Textures", "Calidad de bordes", "Nitidez de texturas"],
        [["Sin AA", "1131", "0.88", "0.268", "—", "128.0", "7", "Escalonados", "Máxima"],
         ["MSAA 2×", "982", "1.02", "0.324", "+21 %", "142.1", "7", "Buena", "Máxima"],
         ["MSAA 4×", "1058", "0.95", "0.345", "+29 %", "163.2", "7", "Muy buena", "Máxima"],
         ["FXAA", "791", "1.26", "0.471", "+76 %", "137.5", "22", "Buena", "Reducida"],
         ["TAA", "656", "1.52", "0.643", "+140 %", "148.1", "24", "Muy buena (estática)", "Reducida; ghosting en movimiento"]],
        widths_cm=[1.7, 1.1, 1.4, 1.4, 1.3, 2.0, 1.6, 2.4, 2.6], caption="Costo y calidad de cada técnica (development build, 1600×900, perfil Mobile).", font_pt=8.5)
r.figure(FIGS / "e2_aa_costos.png", "Tiempo de GPU y memoria gráfica por técnica. La escala de memoria no parte de cero para hacer visible la diferencia.")
r.p("Las cifras de FPS de la build de escritorio son muy altas porque la escena es ligera para esa GPU, y oscilan por variaciones del sistema operativo; por eso el análisis se apoya en el tiempo de GPU, que mide únicamente el trabajo de renderizado. El patrón es claro: MSAA cuesta memoria pero muy poco cómputo; FXAA y TAA ahorran memoria de muestras pero añaden pasadas a pantalla completa.")

r.h1("5. Conclusión: técnica elegida para Android")
r.p("Para *Cenote Vivo* se eligió **MSAA 4×**, configurado en `Mobile_RPAsset`, con el post-proceso de la cámara desactivado. La decisión se justifica por tres razones:")
r.numbered(["**Arquitectura de las GPU móviles.** Mali, Adreno y PowerVR renderizan por teselas en memoria interna del chip. Las muestras de MSAA se resuelven ahí mismo y solo el resultado final se escribe en la RAM, de modo que el aumento de memoria que se observa en escritorio no se traduce en un aumento equivalente de ancho de banda. Por el contrario, FXAA y TAA obligan a escribir la escena completa en memoria principal y volver a leerla, justo lo que más energía consume en un teléfono.",
           "**Calidad adecuada al contenido.** Los defectos más visibles de la escena son geométricos (rocas, lianas, muelle). MSAA los corrige sin difuminar la textura de la roca caliza ni las ondas del agua, que son elementos centrales del mensaje sobre la transparencia del agua.",
           "**Estabilidad con el agua animada.** TAA introduciría ghosting en la superficie del agua, y la transición de limpio a contaminado —la función principal— depende precisamente de que el agua se lea con claridad."])
r.p("Como alternativa para equipos de gama baja que no alcancen 30 FPS estables, se recomienda bajar a **MSAA 2×**, que conserva la mayor parte de la mejora (21 % de costo de GPU frente a 29 % de 4× en la medición) y reduce la memoria a la mitad. FXAA queda como último recurso si un dispositivo no soporta MSAA; TAA se descarta para esta aplicación.")
print(r.save())
