using UnityEngine;
using TMPro;

/// <summary>
/// Vuelca los datos de una sesión de práctica en el panel de detalle.
/// La componente principal cambia según el modo: armonía en piano, afinación en canto.
/// </summary>
public class DetallesSesionUI : MonoBehaviour
{
    [Header("Título Principal (Arriba)")]
    public TextMeshProUGUI tituloSuperiorTxt;

    [Header("Círculos de Puntaje")]
    public TextMeshProUGUI etiquetaAfinacionOArmonia;
    public TextMeshProUGUI valorAfinacionOArmoniaTxt;
    public TextMeshProUGUI ritmoTxt;
    public TextMeshProUGUI globalTxt;

    [Header("Tabla de Detalles Inferior")]
    public TextMeshProUGUI cancionDetalleTxt;
    public TextMeshProUGUI modoDetalleTxt;
    public TextMeshProUGUI fechaDetalleTxt;
    public TextMeshProUGUI horaDetalleTxt;
    public TextMeshProUGUI duracionDetalleTxt;

    public void MostrarDatos(PracticeSessionResponse datos)
    {
        bool esPiano = datos.practice_mode.ToLower().Contains("piano");
        float componentePrincipal = esPiano ? datos.harmony_score : datos.tuning_score;

        SetText(tituloSuperiorTxt, datos.song_title);
        SetText(etiquetaAfinacionOArmonia, esPiano ? "Armonía" : "Afinación");
        SetText(valorAfinacionOArmoniaTxt, FormatPercentage(componentePrincipal));
        SetText(ritmoTxt, FormatPercentage(datos.rhythm_score));
        SetText(globalTxt, FormatPercentage((datos.rhythm_score + componentePrincipal) / 2f));

        SetText(cancionDetalleTxt, datos.song_title);
        SetText(modoDetalleTxt, datos.practice_mode);
        MostrarFechaYHora(datos.practice_datetime);
        SetText(duracionDetalleTxt, "03:45");
    }

    /// <summary>Acepta tanto "2026-04-15 23:29:06" como el formato ISO con 'T'.</summary>
    private void MostrarFechaYHora(string practiceDateTime)
    {
        if (string.IsNullOrEmpty(practiceDateTime)) return;

        string fechaLimpia = practiceDateTime.Replace("T", " ");
        int separador = fechaLimpia.IndexOf(' ');

        if (separador < 0)
        {
            SetText(fechaDetalleTxt, fechaLimpia);
            return;
        }

        string hora = fechaLimpia.Substring(separador + 1);

        SetText(fechaDetalleTxt, fechaLimpia.Substring(0, separador));
        SetText(horaDetalleTxt, hora.Length >= 5 ? hora.Substring(0, 5) : hora);
    }

    private static string FormatPercentage(float value) => $"{value:F0}%";

    private static void SetText(TextMeshProUGUI target, string value)
    {
        if (target != null) target.text = value;
    }
}
