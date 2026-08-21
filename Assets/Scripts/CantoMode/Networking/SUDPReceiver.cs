using System;

/// <summary>
/// Compatibilidad con escenas que aún referencian SUDPReceiver.
/// El análisis de voz ahora es local via VocalPitchAnalyzer.
/// </summary>
[Obsolete("SUDPReceiver fue reemplazado por VocalPitchAnalyzer. Actualiza la escena cuando sea posible.")]
public class SUDPReceiver : VocalPitchAnalyzer
{
}
