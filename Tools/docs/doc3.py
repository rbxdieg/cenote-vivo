from docs_lib import Report, FIGS, CAPTURES as C

r = Report("Entregable 3 - Documento de Funcionalidades del Prototipo.docx",
           "Documento de Funcionalidades del Prototipo",
           "Cenote Vivo · aplicación interactiva para Android (Unity 6 · URP)")

r.h1("1. Descripción general")
r.p("*Cenote Vivo* es una aplicación móvil que usa el modelo 3D de un cenote de Yucatán para mostrar, de forma visual e interactiva, cómo la contaminación del subsuelo llega al agua subterránea. El modelo original del proyecto —la caverna, la plataforma, la escalera, el muelle y las lianas— se conservó tal como se construyó en Blender y se le integraron nuevos elementos: rocas con niveles de detalle, un agua con shader propio, filtraciones de lixiviados en las paredes, basura flotante y bioluminiscencia nocturna.")
r.p("El prototipo se organiza en dos pantallas y cuatro interacciones. Toda la lógica está repartida en componentes C# independientes, coordinados por la interfaz:")
r.table(["Script", "Responsabilidad"],
        [["`UIManager.cs`", "Menú de inicio, frases rotativas, banner, panel informativo y conexión de botones y slider con los sistemas."],
         ["`DayNightController.cs`", "Transición día/noche de la luz direccional, luz ambiental, cielo y bioluminiscencia."],
         ["`PollutionSimulator.cs`", "Nivel de contaminación gradual: material del agua, filtraciones, lixiviados y basura."],
         ["`PerspectiveCameraController.cs`", "Cambio suave entre las tres vistas fijas y órbita lenta en el menú."],
         ["`UnderwaterFog.cs` · `FloatingDebris.cs` · `SafeArea.cs`", "Niebla bajo el agua, residuos flotantes y adaptación a muescas de pantalla."],
         ["`AwarenessContent.cs`", "Textos de concientización centralizados (listos para traducir a inglés y maya)."]],
        widths_cm=[6.0, 10.0], caption="Componentes principales del prototipo.")

r.h1("2. Flujo de usuario y diagramación de la interfaz")
r.figure(FIGS / "e3_flujo.png", "Flujo de pantallas. El botón «Atrás» de Android cierra el panel informativo o regresa al menú.")
r.p("Al abrir la aplicación aparece el **menú de inicio**: el título, una frase guía y una frase de impacto que cambia cada cinco segundos con un fundido, sobre la escena 3D que gira lentamente al fondo. El botón «Explorar el cenote» lleva a la **pantalla principal**, que deja casi toda el área libre para la escena y concentra los controles en dos franjas: un banner superior con el propósito de la aplicación y el acceso al panel «¿Por qué importa?», y una barra inferior con los tres controles de la experiencia.")
r.figure(FIGS / "e3_bosquejo_menu.png", "Bosquejo del menú de inicio.", width_cm=14.5)
r.figure(C / "ui_01_menu.png", "Menú de inicio implementado en Unity.", width_cm=14.5)
r.figure(FIGS / "e3_bosquejo_hud.png", "Bosquejo de la pantalla principal con las zonas de alcance de los pulgares.", width_cm=14.5)
r.p("La distribución responde a cómo se sostiene un teléfono en horizontal: con ambas manos y los pulgares en las esquinas inferiores. Por eso los controles más usados quedan abajo —iluminación a la izquierda y cambio de vista a la derecha— y la contaminación, que es la función principal, ocupa el centro con el control más grande. Los controles de la barra inferior miden entre 140 y 150 px de alto sobre una resolución de referencia de 1920×1080 (≈ 1 cm en un teléfono típico) y el botón del banner 84 px, todos por encima del mínimo de 48 dp que recomienda Android. La interfaz se escala con `CanvasScaler` y se ajusta al área segura con `SafeArea.cs`. Como la información nunca depende solo del color, el estado de la contaminación se expresa siempre también con texto y porcentaje.")
r.figure(C / "ui_02_hud_dia_vista1.png", "Pantalla principal implementada (Vista 1, día, agua limpia).", width_cm=14.5)
r.figure(C / "ui_12_panel_info.png", "Panel desplegable «¿Por qué importa el agua subterránea?» con la explicación de las consecuencias de la contaminación.", width_cm=14.5)

r.h1("3. Funcionalidad 1: Conmutador Día/Noche")
r.h2("3.1 Cómo funciona")
r.p("El botón «Iluminación» llama a `DayNightController.Toggle()`, que inicia una corrutina de 2.5 s. En cada cuadro se calcula un factor `n` entre 0 (día) y 1 (noche) con una curva *SmoothStep* para que la transición acelere y frene con suavidad, y se interpolan dos perfiles de iluminación:")
r.table(["Parámetro", "Día", "Noche"],
        [["Rotación de la luz direccional «Sol»", "(62°, 35°, 0°)", "(38°, 215°, 0°) — luz de luna"],
         ["Temperatura de color", "5600 K (luz de mediodía)", "9500 K, filtro azulado"],
         ["Intensidad", "2.2", "0.28"],
         ["Luz ambiental (cielo / horizonte / suelo)", "Azul claro / gris / ocre", "Azul muy oscuro, casi negro"],
         ["Exposición del cielo y fondo de cámara", "1.25 · azul cielo", "0.08 · azul noche"],
         ["Bioluminiscencia", "Apagada", "Partículas cian y luz puntual; chispas en el shader del agua"]],
        widths_cm=[6.0, 4.6, 5.4], caption="Perfiles de iluminación interpolados.")
r.p("La rotación se interpola con `Quaternion.Slerp` para que el sol recorra un arco y no una línea recta. El factor nocturno se publica como variable global de shader (`_CenoteNight`), de modo que el agua puede reaccionar sin referencias directas entre componentes.")
r.h2("3.2 Qué cambia")
r.bullets(["**Sombras arrojadas:** cambian de dirección y longitud con la rotación del sol; de noche se vuelven tenues porque la luz de luna es débil.",
           "**Tono del agua:** el shader suma la luz ambiental y la del sol a su color; de día predomina el turquesa, de noche el agua se oscurece y aparecen destellos cian.",
           "**Visibilidad de la caverna:** de noche las paredes quedan casi en penumbra y solo destacan las zonas iluminadas por los focos turísticos originales del proyecto, lo que refuerza la atmósfera del cenote.",
           "**Bioluminiscencia ligada a la contaminación:** su intensidad se multiplica por (1 − contaminación). Un cenote contaminado de noche se ve completamente apagado: la pérdida de vida se vuelve visible."])
r.figures_row([(C / "ui_03_hud_vista2_limpio.png", "Día"), (C / "ui_09_hud_noche_limpio.png", "Noche")])
r.caption("Mismo encuadre de día y de noche con el agua limpia.")
r.h2("3.3 Ubicación del botón")
r.p("El botón ocupa la esquina inferior izquierda, en la zona natural del pulgar izquierdo, con color ámbar para asociarlo a la luz. Muestra el estado actual («Día» o «Noche») en texto grande y un rótulo superior que explica su función.")

r.h1("4. Funcionalidad 2: Simulador de Contaminación del Subsuelo (principal)")
r.h2("4.1 Cómo funciona")
r.p("El slider fija un **objetivo** de contaminación entre 0 y 1 (`PollutionSimulator.SetTarget`), y el nivel real avanza hacia él a 0.35 unidades por segundo con `Mathf.MoveTowards`. Así, aunque el usuario mueva el dedo bruscamente, el cenote se degrada de manera gradual, como ocurre en la realidad. El botón «Contaminar / Restaurar» lleva el objetivo directamente a uno de los dos extremos.")
r.p("En cada cambio el nivel se suaviza con la curva *smoothstep* (3x² − 2x³): al principio los contaminantes casi no se notan y después el deterioro se acelera. Con ese valor se actualizan todos los efectos:")
r.table(["Elemento", "Variable", "Rango de activación"],
        [["Material del agua", "`_PollutionLevel`, `_Turbidity`", "0 → 100 %"],
         ["Filtraciones en las paredes (10 franjas sobre la roca)", "Global `_CenotePollution`", "Avanzan de arriba hacia abajo desde el 0 %"],
         ["Goteo de lixiviados (10 sistemas de partículas)", "Emisión 0 → 18 partículas/s", "A partir del 15 %"],
         ["Manchas de residuos en la superficie", "Emisión 0 → 6 partículas/s", "A partir del 30 %"],
         ["Basura flotante (26 botellas, bolsas y latas)", "`FloatingDebris.Amount`", "Aparece progresivamente desde el 35 %"],
         ["Niebla bajo el agua", "Densidad 0.035 → 0.085", "Solo en la Vista 3"]],
        widths_cm=[6.4, 5.0, 4.6], caption="Efectos controlados por el nivel de contaminación.")
r.h2("4.2 Qué cambia")
r.bullets(["**Turbidez y absorción de luz:** la distancia a la que el agua se vuelve opaca pasa de 6 m a unos 36 cm, así que el fondo deja de verse.",
           "**Pérdida de transparencia:** la refracción de la escena bajo el agua se reduce hasta desaparecer y el color vira de turquesa a marrón verdoso.",
           "**Superficie:** el agua se vuelve menos brillante (la suavidad cae 65 %), las ondas se aplanan y aparece una película de residuos.",
           "**Residuos y filtraciones:** basura flotando, manchas oscuras que escurren por la roca caliza y goteo de lixiviados desde las fisuras hacia el agua.",
           "**Mensaje:** el rótulo pasa de «Agua cristalina: la luz atraviesa hasta el fondo» a «Agua contaminada: no apta para consumo y sin vida visible»."])
r.figure(FIGS / "e3_limpio_vs_contaminado.png", "Bosquejo comparativo de los dos estados.", width_cm=14.5)
r.figures_row([(C / "ui_03_hud_vista2_limpio.png", "Limpio (0 %)"), (C / "ui_05_hud_vista2_medio.png", "Intermedio (50 %)")])
r.figures_row([(C / "ui_06_hud_vista2_contaminado.png", "Contaminado (100 %)"), (C / "ui_11_hud_noche_contaminado.png", "Contaminado de noche")])
r.caption("Resultado implementado: progresión de la contaminación en la Vista 2 y el mismo estado de noche, sin bioluminiscencia.")
r.h2("4.3 Ubicación del control")
r.p("El simulador ocupa el centro de la barra inferior, el área más grande de la interfaz, porque es la función que transmite el mensaje del proyecto. El estado se muestra en texto y porcentaje encima del slider, los extremos están rotulados («Limpio» y «Contaminado») y debajo aparece una frase que explica lo que ocurre en cada etapa. El botón de acción rápida se coloca a la derecha del slider con un color ocre que anticipa el cambio.")

r.h1("5. Funcionalidad 3: Selector Dinámico de Perspectivas de Cámara")
r.h2("5.1 Cómo funciona")
r.p("Cada vista es un `Transform` vacío en la escena (`Vista_1_Panoramica`, `Vista_2_NivelAgua`, `Vista_3_Subterranea`) con un campo de visión asociado. Al pulsar «Cambiar vista» se avanza al siguiente índice de forma cíclica y una corrutina interpola durante 1.6 s la posición (`Vector3.Lerp`), la rotación (`Quaternion.Slerp`) y el campo de visión con una curva *ease-in-out*. Usar una sola cámara que se desplaza, en lugar de tres cámaras que se activan, evita cortes bruscos, mantiene un único render por cuadro y permite que el usuario entienda espacialmente hacia dónde se mueve.")
r.h2("5.2 Qué cambia")
r.table(["Vista", "Posición", "FOV", "Qué muestra"],
        [["1 · Panorámica", "La cámara original del proyecto, sobre la boca del cenote", "60°", "La forma completa de la caverna, el agua, el muelle y la escalera; contexto general."],
         ["2 · Nivel del agua", "A ~0.9 m sobre la superficie, junto al muelle", "62°", "La transparencia, el color y los residuos del agua; las rocas con LOD en primer plano."],
         ["3 · Subterránea", "4.5 m bajo la superficie", "70°", "El agua vista desde abajo y la pérdida de visibilidad por turbidez; el acuífero como espacio habitado."]],
        widths_cm=[3.0, 5.4, 1.4, 6.2], caption="Puntos de vista de la cámara.")
r.p("En la Vista 3 el componente `UnderwaterFog` detecta que la cámara está bajo la superficie y activa una niebla exponencial cuyo color y densidad siguen el nivel de contaminación: con el agua limpia se distinguen las paredes y la superficie iluminada; con el agua contaminada la visibilidad cae a pocos metros, que es la forma más directa de comunicar lo que significa la turbidez para la fauna del acuífero.")
r.figure(FIGS / "e3_camaras.png", "Bosquejo de la ubicación de las tres cámaras en un corte lateral del cenote.", width_cm=14.5)
r.figures_row([(C / "ui_02_hud_dia_vista1.png", "Vista 1 · Panorámica"), (C / "ui_03_hud_vista2_limpio.png", "Vista 2 · Nivel del agua")])
r.figures_row([(C / "ui_04_hud_vista3_limpio.png", "Vista 3 · Subterránea (limpio)"), (C / "ui_07_hud_vista3_contaminado.png", "Vista 3 · Subterránea (contaminado)")])
r.caption("Las tres vistas implementadas y el efecto de la contaminación en la vista sumergida.")
r.h2("5.3 Distribución para pantallas táctiles")
r.p("El botón de vista está en la esquina inferior derecha, al alcance del pulgar derecho y simétrico al de iluminación. Su texto principal indica la vista actual y el rótulo superior «Cambiar vista >» deja clara la acción. Durante la transición la interfaz permanece activa, de modo que el usuario puede seguir ajustando la contaminación mientras la cámara se desplaza.")

r.h1("6. Textos de concientización")
r.p("Las frases del menú se diseñaron para ser breves, verificables y conectadas con la vida cotidiana de la península:")
r.bullets(["«Bajo tus pies corre el agua que bebes.»",
           "«En Yucatán no hay ríos en la superficie: toda el agua dulce viaja bajo tierra.»",
           "«Lo que se tira en la superficie, tarde o temprano llega al cenote.»",
           "«La roca caliza filtra la lluvia… pero también deja pasar los contaminantes.»",
           "«Un cenote limpio es vida. Uno contaminado es una advertencia.»",
           "«Cuidar el subsuelo es cuidar el agua de mañana.»"])
r.p("El banner de la pantalla principal resume el propósito: *«Explora un cenote de Yucatán y descubre cómo la contaminación del subsuelo llega al agua que todos bebemos.»* El panel informativo desarrolla cinco apartados: el acuífero como sistema conectado, la filtración de residuos (fosas sépticas, lixiviados de basureros, granjas porcícolas, agroquímicos y combustibles), la pérdida de biodiversidad, los riesgos para el consumo humano y acciones concretas que el usuario puede tomar.")

r.h1("7. Conclusión")
r.p("Las tres funcionalidades se complementan: el ciclo día/noche da contexto y atmósfera, el cambio de perspectiva permite observar el problema desde la superficie hasta el interior del acuífero, y el simulador de contaminación —la función principal— conecta cada cambio visual con una consecuencia real. Al estar implementadas como componentes desacoplados que se comunican por eventos y variables globales de shader, cada una puede ajustarse o ampliarse sin modificar las demás, por ejemplo para añadir audio explicativo o los idiomas inglés y maya que contempla la propuesta original.")
print(r.save())
