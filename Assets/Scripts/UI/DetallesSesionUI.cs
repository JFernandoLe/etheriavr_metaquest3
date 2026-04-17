using UnityEngine;
using TMPro;

public class DetallesSesionUI : MonoBehaviour
{
    [Header("Título Principal (Arriba)")]
    public TextMeshProUGUI tituloSuperiorTxt; // El que quieres que se repita arriba

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
        // --- 1. Título Superior ---
        if (tituloSuperiorTxt != null) tituloSuperiorTxt.text = datos.song_title;

        // --- 2. Lógica de Círculos (Afinación vs Armonía) ---
        bool esPiano = datos.practice_mode.ToLower().Contains("piano");

        if (etiquetaAfinacionOArmonia != null)
            etiquetaAfinacionOArmonia.text = esPiano ? "Armonía" : "Afinación";

        if (valorAfinacionOArmoniaTxt != null)
            valorAfinacionOArmoniaTxt.text = (esPiano ? datos.harmony_score : datos.tuning_score).ToString("F0") + "%";

        if (ritmoTxt != null) ritmoTxt.text = datos.rhythm_score.ToString("F0") + "%";

        float promedio = (datos.rhythm_score + (esPiano ? datos.harmony_score : datos.tuning_score)) / 2f;
        if (globalTxt != null) globalTxt.text = promedio.ToString("F0") + "%";


        // --- 3. Lógica de la Tabla Inferior ---
        if (cancionDetalleTxt != null) cancionDetalleTxt.text = datos.song_title;
        if (modoDetalleTxt != null) modoDetalleTxt.text = datos.practice_mode;

        // --- Procesar la Fecha y Hora (Soporta espacio o "T") ---
        if (!string.IsNullOrEmpty(datos.practice_datetime))
        {
            // Reemplazamos la 'T' por un espacio por si viene en formato ISO
            string fechaLimpia = datos.practice_datetime.Replace("T", " ");

            if (fechaLimpia.Contains(" "))
            {
                string[] partes = fechaLimpia.Split(' ');

                // partes[0] es la fecha (2026-04-15)
                if (fechaDetalleTxt != null) fechaDetalleTxt.text = partes[0];

                // partes[1] es la hora (23:29:06)
                if (horaDetalleTxt != null)
                {
                    if (partes[1].Length >= 5)
                        horaDetalleTxt.text = partes[1].Substring(0, 5); // "23:29"
                    else
                        horaDetalleTxt.text = partes[1];
                }
            }
            else
            {
                // Si por algo no se pudo separar, ponemos el string completo en Fecha
                if (fechaDetalleTxt != null) fechaDetalleTxt.text = fechaLimpia;
            }
        }

        if (duracionDetalleTxt != null) duracionDetalleTxt.text = "03:45";
    }
}