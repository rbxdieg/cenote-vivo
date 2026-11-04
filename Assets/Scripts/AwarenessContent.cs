namespace CenoteVivo
{
    // Textos de concientización. Centralizados aquí para poder traducirlos (español, inglés, maya).
    public static class AwarenessContent
    {
        public const string Title = "CENOTE VIVO";
        public const string Subtitle = "El agua que no ves también se contamina";

        public static readonly string[] ImpactPhrases =
        {
            "Bajo tus pies corre el agua que bebes.",
            "En Yucatán no hay ríos en la superficie: toda el agua dulce viaja bajo tierra.",
            "Lo que se tira en la superficie, tarde o temprano llega al cenote.",
            "La roca caliza filtra la lluvia… pero también deja pasar los contaminantes.",
            "Un cenote limpio es vida. Uno contaminado es una advertencia.",
            "Cuidar el subsuelo es cuidar el agua de mañana.",
        };

        public const string Banner =
            "Explora un cenote de Yucatán y descubre cómo la contaminación del subsuelo llega al agua que todos bebemos.";

        public const string InfoTitle = "¿Por qué importa el agua subterránea?";

        public const string InfoBody =
            "<b>Un sistema conectado</b>\n" +
            "La península de Yucatán está formada por roca caliza muy porosa. La lluvia se infiltra casi de inmediato y forma un acuífero: una red de ríos subterráneos y cavernas que conecta a los cenotes entre sí. Para millones de personas este acuífero es la principal fuente de agua potable.\n\n" +
            "<b>Filtración de residuos</b>\n" +
            "Como el suelo es delgado y la roca está fracturada, casi no hay una capa que detenga los contaminantes. Aguas negras de fosas sépticas mal construidas, lixiviados de basureros a cielo abierto, desechos de granjas porcícolas, fertilizantes, plaguicidas y derrames de combustible bajan con la lluvia y pueden recorrer kilómetros por los conductos subterráneos hasta aparecer en un cenote lejano.\n\n" +
            "<b>Pérdida de biodiversidad</b>\n" +
            "Los cenotes albergan especies que solo existen en estos ecosistemas, como peces y crustáceos ciegos adaptados a la oscuridad. El exceso de nutrientes provoca la proliferación de algas que consumen el oxígeno del agua (eutrofización); los metales y químicos se acumulan en los organismos. Cuando un cenote pierde su fauna, rara vez la recupera.\n\n" +
            "<b>Riesgos para el consumo humano</b>\n" +
            "El agua contaminada puede contener bacterias fecales, nitratos, plaguicidas y metales pesados. Su consumo se asocia con enfermedades gastrointestinales, infecciones de la piel y, a largo plazo, daños a la salud por exposición a químicos. Limpiar un acuífero es lento y costoso: prevenir es la única solución realista.\n\n" +
            "<b>¿Qué puedes hacer?</b>\n" +
            "• No arrojes basura, aceite ni químicos al suelo o al drenaje.\n" +
            "• Usa biodigestores o fosas sépticas bien selladas.\n" +
            "• Prefiere productos de limpieza biodegradables y reduce los agroquímicos.\n" +
            "• Al visitar un cenote, retira el bloqueador y el repelente antes de entrar al agua.\n" +
            "• Comparte lo que aprendiste: el agua subterránea es de todos.";

        public const string CleanState = "Limpio";
        public const string PollutedState = "Contaminado";

        public static string PollutionCaption(float level)
        {
            if (level < 0.15f) return "Agua cristalina: la luz atraviesa hasta el fondo.";
            if (level < 0.45f) return "Los lixiviados comienzan a filtrarse por las grietas de la roca.";
            if (level < 0.75f) return "El agua se enturbia: la luz ya no llega al fondo y baja el oxígeno.";
            return "Agua contaminada: no apta para consumo y sin vida visible.";
        }
    }
}
