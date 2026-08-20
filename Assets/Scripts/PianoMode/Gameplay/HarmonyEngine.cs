using UnityEngine;

/// <summary>
/// Motor de evaluación armónica puro. Sin dependencias de MonoBehaviour ni red.
/// Expuesto como clase estática para permitir pruebas unitarias aisladas.
/// </summary>
public static class HarmonyEngine
{
    /// <summary>
    /// Calidad de timing entre 0.0 y 1.0: vale 1.0 dentro de la ventana perfecta y
    /// decae cuadráticamente hasta 0.0 al llegar al límite de la ventana de scoring.
    /// </summary>
    /// <param name="onsetOffset">Desviación absoluta en segundos (|tiempoPulsado - tiempoEsperado|)</param>
    /// <param name="perfectTimingWindow">Umbral de precisión perfecta en segundos (ej. 0.04)</param>
    /// <param name="scoringWindow">Ventana máxima de evaluación en segundos (ej. 0.24)</param>
    public static float EvaluarCalidadTiming(float onsetOffset, float perfectTimingWindow, float scoringWindow)
    {
        float safeWindow = Mathf.Max(scoringWindow, 0.0001f);
        float clampedPerfectWindow = Mathf.Clamp(perfectTimingWindow, 0f, safeWindow);
        if (onsetOffset <= clampedPerfectWindow) return 1f;

        float normalizedOffset = Mathf.InverseLerp(clampedPerfectWindow, safeWindow, onsetOffset);
        return 1f - (normalizedOffset * normalizedOffset);
    }

    /// <summary>
    /// Valida si una nota MIDI tocada coincide con la esperada dentro de la ventana de tiempo.
    /// </summary>
    /// <param name="tiempoDesviacion">Desviación absoluta en segundos entre el momento tocado y el esperado</param>
    /// <param name="hitWindow">Tolerancia máxima de tiempo en segundos (ej. 0.18)</param>
    public static bool ValidarAcierto(int notaEsperada, int notaTocada, float tiempoDesviacion, float hitWindow) =>
        notaEsperada == notaTocada && tiempoDesviacion <= hitWindow;
}
