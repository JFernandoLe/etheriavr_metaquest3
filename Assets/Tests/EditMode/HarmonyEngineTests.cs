using UnityEngine;

/// <summary>
/// Pruebas unitarias de HarmonyEngine ejecutadas en runtime.
/// Resultados visibles en adb logcat con tag [HarmonyTest].
/// </summary>
public static class HarmonyEngineTests
{
    private const float PerfectWindow = 0.04f;
    private const float ScoringWindow = 0.24f;
    private const float HitWindow     = 0.18f;

    private static bool yaEjecutado = false;

    [UnityEngine.Scripting.Preserve]
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void EjecutarTodas()
    {
        if (yaEjecutado) return;
        yaEjecutado = true;

        int pasadas = 0;
        int falladas = 0;

        float r;

        r = HarmonyEngine.EvaluarCalidadTiming(0f, PerfectWindow, ScoringWindow);
        Run("EvaluarCalidadTiming_OffsetCero_RetornaUno",
            $"offset=0.000s  ventanaPerfecta={PerfectWindow}s  ventanaScoring={ScoringWindow}s  ->  obtenido={r:F3}  esperado=1.000",
            r == 1f, ref pasadas, ref falladas);

        r = HarmonyEngine.EvaluarCalidadTiming(0.02f, PerfectWindow, ScoringWindow);
        Run("EvaluarCalidadTiming_DentroVentanaPerfecta_RetornaUno",
            $"offset=0.020s  ventanaPerfecta={PerfectWindow}s  ventanaScoring={ScoringWindow}s  ->  obtenido={r:F3}  esperado=1.000",
            r == 1f, ref pasadas, ref falladas);

        r = HarmonyEngine.EvaluarCalidadTiming(0.04f, PerfectWindow, ScoringWindow);
        Run("EvaluarCalidadTiming_EnBordeVentanaPerfecta_RetornaUno",
            $"offset=0.040s  ventanaPerfecta={PerfectWindow}s  ventanaScoring={ScoringWindow}s  ->  obtenido={r:F3}  esperado=1.000",
            r == 1f, ref pasadas, ref falladas);

        r = HarmonyEngine.EvaluarCalidadTiming(0.24f, PerfectWindow, ScoringWindow);
        Run("EvaluarCalidadTiming_EnLimiteScoringWindow_RetornaCero",
            $"offset=0.240s  ventanaPerfecta={PerfectWindow}s  ventanaScoring={ScoringWindow}s  ->  obtenido={r:F3}  esperado=0.000",
            Mathf.Approximately(r, 0f), ref pasadas, ref falladas);

        r = HarmonyEngine.EvaluarCalidadTiming(0.14f, PerfectWindow, ScoringWindow);
        Run("EvaluarCalidadTiming_AMitadDeVentana_RetornaCero75",
            $"offset=0.140s  ventanaPerfecta={PerfectWindow}s  ventanaScoring={ScoringWindow}s  ->  obtenido={r:F3}  esperado=0.750",
            Mathf.Abs(r - 0.75f) < 0.001f, ref pasadas, ref falladas);

        bool b;

        b = HarmonyEngine.ValidarAcierto(60, 60, 0.045f, HitWindow);
        Run("ValidarAcierto_NotaCorrectaDentroVentana_RetornaTrue",
            $"notaEsperada=60  notaTocada=60  desviacion=0.045s  hitWindow={HitWindow}s  ->  obtenido={b}  esperado=True",
            b, ref pasadas, ref falladas);

        b = HarmonyEngine.ValidarAcierto(60, 60, 0.20f, HitWindow);
        Run("ValidarAcierto_NotaCorrectaFueraVentana_RetornaFalse",
            $"notaEsperada=60  notaTocada=60  desviacion=0.200s  hitWindow={HitWindow}s  ->  obtenido={b}  esperado=False",
            !b, ref pasadas, ref falladas);

        b = HarmonyEngine.ValidarAcierto(60, 61, 0.01f, HitWindow);
        Run("ValidarAcierto_NotaIncorrectaDentroVentana_RetornaFalse",
            $"notaEsperada=60  notaTocada=61  desviacion=0.010s  hitWindow={HitWindow}s  ->  obtenido={b}  esperado=False",
            !b, ref pasadas, ref falladas);

        b = HarmonyEngine.ValidarAcierto(72, 72, 0f, HitWindow);
        Run("ValidarAcierto_NotaYTimingExactos_RetornaTrue",
            $"notaEsperada=72  notaTocada=72  desviacion=0.000s  hitWindow={HitWindow}s  ->  obtenido={b}  esperado=True",
            b, ref pasadas, ref falladas);

        Debug.Log($"[HarmonyTest] RESULTADO FINAL: {pasadas} pasadas / {pasadas + falladas} total");
    }

    private static void Run(string nombre, string condicion, bool resultado, ref int pasadas, ref int falladas)
    {
        if (resultado)
        {
            Debug.Log($"[HarmonyTest] PASS  {nombre}  |  {condicion}");
            pasadas++;
        }
        else
        {
            Debug.LogError($"[HarmonyTest] FAIL  {nombre}  |  {condicion}");
            falladas++;
        }
    }
}

